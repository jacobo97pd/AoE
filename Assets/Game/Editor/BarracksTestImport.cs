using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>
    /// Imports the Meshy-generated test barracks: the FBX that tools/art/convert_meshy_building.py produced from the
    /// source GLB, one URP/Lit material carrying its PBR maps, and a prefab under a Resources folder so the game can
    /// load it at runtime. Same recipe as the Crimson Corsair import: materials are never taken from the FBX, because
    /// URP's FBX preprocessor keeps only diffuse and normal and would silently drop metallic and roughness.
    /// </summary>
    public static class BarracksTestImport
    {
        private const string Root = "Assets/Models/Buildings/Barracks";
        private const string ModelPath = Root + "/Barracks.fbx";
        private const string MaterialPath = Root + "/Barracks.mat";
        private const string PrefabPath = Root + "/Resources/ImportedBuildings/Barracks.prefab";

        [MenuItem("Emberfield/Import test barracks")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            if (!File.Exists(ModelPath)) throw new FileNotFoundException("Run convert_meshy_building.py first.", ModelPath);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            model.globalScale = 1; model.useFileScale = true; model.bakeAxisConversion = true;
            model.importAnimation = false; model.importCameras = false; model.importLights = false;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.meshCompression = ModelImporterMeshCompression.Off;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit is unavailable.");
                material = new Material(shader) { name = "Barracks" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.enableInstancing = true;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_WorkflowMode", 1); material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2);
            var baseMap = Map("Barracks_BaseColor.png", false, true);
            var normalMap = Map("Barracks_Normal.png", true, false);
            var packed = Map("Barracks_MetallicSmoothness.png", false, false);
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                // Kept weak: the neighbours are flat-shaded and diffuse only, so strong relief would stand out.
                material.SetFloat("_BumpScale", .5f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.SetFloat("_Metallic", 1);
                // Scaled down: the procedural buildings have no specular at all, and a glossy one reads as pasted in.
                material.SetFloat("_Smoothness", .45f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else { material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .2f); }
            EditorUtility.SetDirty(material);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                instance.name = "Barracks";
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                AssetDatabase.Refresh();
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally { Object.DestroyImmediate(instance); }
            AssetDatabase.SaveAssets();
            Debug.Log("EMBERFIELD_BARRACKS_IMPORT_OK base=" + (baseMap != null) + " normal=" + (normalMap != null) + " metalSmooth=" + (packed != null));
        }

        private static Texture2D Map(string name, bool normal, bool srgb)
        {
            string path = Root + "/Textures/" + name;
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true;
            // A building seen from an RTS camera never earns 2k; the art target caps world textures at 1024.
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
