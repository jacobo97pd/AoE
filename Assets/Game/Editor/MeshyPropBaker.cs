using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Quality;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>
    /// Builds the scenery prefabs listed in tools/art/meshy_props.json from what tools/art/meshy_prop.py left in
    /// Assets/Models/Props/&lt;biome&gt;/&lt;id&gt;: one FBX holding &lt;id&gt;_LOD0 (and _LOD1), and the texture maps.
    /// The far level is about 40% of the near one; the blocked-cell fillers have one too, for the phone tiers.
    /// Each LOD is baked into a mesh asset whose vertices are in metres, standing on y = 0 and centred, whatever
    /// transform the FBX carried: obstacles are scaled unevenly per cell and the felling sway bends by object-space
    /// height, so the numbers in the mesh must be the real ones. Writes Resources/MeshyProps/catalog.json.
    /// </summary>
    public static class MeshyPropBaker
    {
        private const string Root = "Assets/Models/Props";
        private const string Output = Root + "/Resources/MeshyProps";

        [Serializable] private sealed class Roster { public Prop[] props; }
        [Serializable] private sealed class Prop { public string id, biome, role, kind; public int lod0, lod1, size; public float height; public string[] factions; }

        [MenuItem("Emberfield/Art/Import Meshy props")]
        public static void Run()
        {
            var problems = new List<string>();
            int count = Bake(problems);
            foreach (var problem in problems) Debug.LogError("EMBERFIELD_MESHY_PROP_FAILED " + problem);
            Debug.Log("EMBERFIELD_MESHY_PROPS_OK count=" + count + " failed=" + problems.Count);
            if (Application.isBatchMode && problems.Count > 0) EditorApplication.Exit(1);
        }

        public static int Bake(List<string> problems)
        {
            var roster = JsonUtility.FromJson<Roster>(File.ReadAllText(Path.Combine(Application.dataPath, "../tools/art/meshy_props.json")));
            Directory.CreateDirectory(Output);
            var catalog = new List<MeshyPropVisuals.Entry>();
            foreach (var prop in roster.props)
            {
                string folder = Root + "/" + prop.biome + "/" + prop.id, fbx = folder + "/" + prop.id + ".fbx";
                if (!File.Exists(fbx)) continue;
                try { catalog.Add(BakeProp(prop, folder, fbx)); }
                catch (Exception error) { problems.Add(prop.id + ": " + error.Message); }
            }
            File.WriteAllText(Output + "/catalog.json", JsonUtility.ToJson(new MeshyPropVisuals.Document { entries = catalog.ToArray() }, true));
            AssetDatabase.ImportAsset(Output + "/catalog.json");
            AssetDatabase.SaveAssets();
            return catalog.Count;
        }

        private static MeshyPropVisuals.Entry BakeProp(Prop prop, string folder, string fbx)
        {
            int size = prop.size > 0 ? prop.size : 512;
            var baseMap = Texture(folder + "/" + prop.id + "_BaseColor.png", TextureImporterType.Default, true, size);
            var normal = Texture(folder + "/" + prop.id + "_Normal.png", TextureImporterType.NormalMap, false, size);
            var mask = Texture(folder + "/" + prop.id + "_Mask.png", TextureImporterType.Default, false, size);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false; importer.isReadable = true;
            importer.addCollider = false; importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();

            // Siege engines and ships take their owner's colour through the unit shader, like the armies they belong to.
            bool siege = prop.role == "siege" || prop.role == "ship";
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + prop.id + ".mat");
            var shader = Shader.Find(siege ? "Emberfield/Meshy Unit" : "Emberfield/Meshy Prop");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, folder + "/" + prop.id + ".mat"); }
            material.shader = shader; material.enableInstancing = true;
            material.SetTexture("_BaseMap", baseMap); material.SetTexture("_BumpMap", normal); material.SetTexture("_MaskMap", mask);
            if (siege) material.SetFloat("_RimStrength", .04f);
            EditorUtility.SetDirty(material);

            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            try
            {
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                // A file with one mesh comes in with that mesh on its root, named after the file rather than _LOD0.
                var near = filters.FirstOrDefault(f => f.name.EndsWith("_LOD0", StringComparison.Ordinal)) ?? (filters.Length == 1 ? filters[0] : null)
                    ?? throw new InvalidOperationException("no " + prop.id + "_LOD0 in " + fbx);
                var far = filters.FirstOrDefault(f => f.name.EndsWith("_LOD1", StringComparison.Ordinal));
                // The near level's centre places both levels, so they stand exactly on top of each other.
                var nearMesh = Baked(model.transform, near, prop.id + "_LOD0");
                var bounds = nearMesh.bounds;
                var shift = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                Shift(nearMesh, shift);
                var farMesh = far != null ? Baked(model.transform, far, prop.id + "_LOD1") : null;
                // Decimation can lift the far level's lowest vertex; it is grounded on its own so it never floats.
                if (farMesh != null) Shift(farMesh, new Vector3(shift.x, -farMesh.bounds.min.y, shift.z));
                int triangles = nearMesh.triangles.Length / 3, farTriangles = farMesh != null ? farMesh.triangles.Length / 3 : 0;
                if (prop.lod0 > 0 && triangles > prop.lod0 * 1.15f) throw new InvalidOperationException(triangles + " triangles, over the budget of " + prop.lod0);
                if (farMesh != null && farTriangles * 1.5f > triangles) throw new InvalidOperationException("the far level keeps " + farTriangles + " of " + triangles + " triangles");
                nearMesh = Save(nearMesh, folder + "/" + prop.id + "_LOD0.asset");
                if (farMesh != null) farMesh = Save(farMesh, folder + "/" + prop.id + "_LOD1.asset");

                var root = new GameObject(prop.id);
                try
                {
                    if (prop.role == "obstacle")
                    {
                        // Fillers are drawn by the thousand. The loader shares the root's mesh and the "LOD1" child's across
                        // the map, one renderer per filler, and a phone tier swaps between them with the zoom
                        // (SceneryDetail); the prefab's own group switches where a desktop would, which is never at a game
                        // zoom.
                        var nearLevel = Render(root, nearMesh, material, true);
                        if (farMesh != null)
                        {
                            var farLevel = Render(Child(root, "LOD1"), farMesh, material, true);
                            var group = root.AddComponent<LODGroup>();
                            group.SetLODs(new[] { new LOD(MobileTiers.SceneryLodThreshold(MobileTiers.DesktopSceneryDetailHeight, 2), new Renderer[] { nearLevel }),
                                new LOD(MobileTiers.SceneryCullHeight, new Renderer[] { farLevel }) });
                            group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                        }
                    }
                    else
                    {
                        var levels = new List<LOD> { new LOD(farMesh != null ? .06f : .008f, new Renderer[] { Render(Child(root, "LOD0"), nearMesh, material, true) }) };
                        if (farMesh != null) levels.Add(new LOD(.008f, new Renderer[] { Render(Child(root, "LOD1"), farMesh, material, true) }));
                        var group = root.AddComponent<LODGroup>();
                        group.SetLODs(levels.ToArray()); group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + prop.id + ".prefab");
                }
                finally { Object.DestroyImmediate(root); }
                float height = nearMesh.bounds.size.y;
                Debug.Log("EMBERFIELD_MESHY_PROP " + prop.id + " role=" + prop.role + " triangles=" + triangles + (farMesh != null ? "/" + farTriangles : "") +
                    " size=" + nearMesh.bounds.size.ToString("0.00"));
                // The factions travel into the catalogue so each culture's siege ladder finds its own army at runtime.
                return new MeshyPropVisuals.Entry { id = prop.id, biome = prop.biome, role = prop.role, kind = prop.kind, prefab = "MeshyProps/" + prop.id, triangles = triangles, farTriangles = farTriangles, height = height,
                    factions = prop.factions ?? Array.Empty<string>() };
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
        }

        private static Renderer Render(GameObject target, Mesh mesh, Material material, bool shadows)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; renderer.receiveShadows = true;
            return renderer;
        }

        // The FBX node transform (axis conversion, file units) goes into the vertices, and every sub-mesh into one.
        private static Mesh Baked(Transform root, MeshFilter filter, string name)
        {
            var source = filter.sharedMesh;
            // A file with one mesh has it on the root, so the axis conversion is the root's own turn and scale, which
            // the root-relative matrix would drop: the one-level fillers were baked lying on their sides that way.
            var matrix = filter.transform == root ? Matrix4x4.TRS(Vector3.zero, root.localRotation, root.localScale)
                : root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var mesh = new Mesh { name = name };
            mesh.indexFormat = source.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = source.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            mesh.normals = source.normals.Select(n => matrix.MultiplyVector(n).normalized).ToArray();
            mesh.uv = source.uv;
            var indices = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++) indices.AddRange(source.GetTriangles(sub));
            // A mirroring transform would turn the faces inside out.
            if (matrix.determinant < 0) for (int i = 0; i < indices.Count; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        private static void Shift(Mesh mesh, Vector3 shift)
        {
            mesh.vertices = mesh.vertices.Select(v => v + shift).ToArray();
            mesh.RecalculateBounds();
        }

        // Replacing the asset in place keeps its GUID, so prefabs that reference it stay wired across re-bakes.
        private static Mesh Save(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Texture2D Texture(string path, TextureImporterType type, bool colour, int size)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("texture missing", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type; importer.sRGBTexture = colour && type == TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.maxTextureSize = size; importer.mipmapEnabled = true; importer.anisoLevel = 2;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            MobileTextureImport.Configure(importer, path);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
