using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>
    /// Builds the game's prefabs for the Meshy units listed in tools/art/meshy_units.json, from what
    /// tools/art/meshy_unit_finish.py left in Assets/Models/Units/&lt;id&gt;: one FBX holding the mesh and every clip,
    /// and three 1024 textures. Writes Resources/MeshyUnits/catalog.json, which MeshyUnitVisuals reads at runtime.
    /// A unit whose files are not there yet is skipped, so the roster can run ahead of the generation.
    /// </summary>
    public static class MeshyUnitBaker
    {
        public const string Root = "Assets/Models/Units";
        private const string Output = Root + "/Resources/MeshyUnits";
        private const int TriangleBudget = 6000;
        // Climb is Meshy's ladder loop, fetched by tools/art/meshy_climb.py for the wall-boarding infantry only, and
        // climbed in place (WorldView lifts the soldier up the ladder); a unit without the take has no Climb state.
        private static readonly string[] Looping = { "Idle", "Walk", "Run", "Work", "Aim", "Climb" };
        private static readonly string[] States = { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Work", "Aim", "Climb" };

        [Serializable] private sealed class Roster { public RosterUnit[] units; }
        [Serializable] private sealed class RosterUnit { public string id, name, culture, unit, role; public string[] factions; public float gameHeight; public bool cosmetic; }
        [Serializable] private sealed class Manifest { public Finish finish; }
        [Serializable] private sealed class Finish { public float walkMetresPerSecond, runMetresPerSecond, heightMetres; public int triangles; }
        [Serializable] private sealed class BuildingRoster { public string output; public BuildingStyleMap styles; public RosterBuilding[] buildings; }
        [Serializable] private sealed class BuildingStyleMap { public BuildingStyle kingdom, mountain, dwarf, elf, orc, sultanate, sahel; }
        [Serializable] private sealed class BuildingStyle { public string[] factions; public float grade; }
        [Serializable] private sealed class RosterBuilding { public string id, style, building; public float height, grade; }
        private const string BuildingOutput = "Assets/Models/Buildings/Resources/MeshyBuildings";

        [MenuItem("Emberfield/Art/Import Meshy units")]
        public static void Run()
        {
            var roster = JsonUtility.FromJson<Roster>(File.ReadAllText(Path.Combine(Application.dataPath, "../tools/art/meshy_units.json")));
            Directory.CreateDirectory(Output);
            var catalog = new List<MeshyUnitVisuals.Entry>();
            var problems = new List<string>();
            foreach (var unit in roster.units)
            {
                string folder = Root + "/" + unit.id, fbx = folder + "/" + unit.id + ".fbx";
                if (!File.Exists(fbx)) continue;
                try { catalog.Add(Bake(unit, folder, fbx)); }
                catch (Exception error) { problems.Add(unit.id + ": " + error.Message); }
            }
            File.WriteAllText(Output + "/catalog.json", JsonUtility.ToJson(new MeshyUnitVisuals.Document { entries = catalog.ToArray() }, true));
            AssetDatabase.ImportAsset(Output + "/catalog.json");
            AssetDatabase.SaveAssets();
            int buildings = RunBuildings(problems);
            int props = MeshyPropBaker.Bake(problems);
            MeshyGroundImport.Configure();
            foreach (var problem in problems) Debug.LogError("EMBERFIELD_MESHY_UNIT_FAILED " + problem);
            Debug.Log("EMBERFIELD_MESHY_UNITS_OK count=" + catalog.Count + " buildings=" + buildings + " props=" + props + " failed=" + problems.Count);
            if (Application.isBatchMode && problems.Count > 0) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Some units alone, named on the command line (<c>-meshyUnits id,id</c>): their prefabs, controllers and catalogue
        /// entries. Every other unit, and the buildings and props, stay as they are. Run rebakes all of them.
        /// </summary>
        public static void RunUnitsFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-meshyUnits");
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("name the units to bake: -meshyUnits id,id");
            RunUnits(args[at + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static void RunUnits(params string[] ids)
        {
            var roster = JsonUtility.FromJson<Roster>(File.ReadAllText(Path.Combine(Application.dataPath, "../tools/art/meshy_units.json")));
            string catalogPath = Output + "/catalog.json";
            var entries = (JsonUtility.FromJson<MeshyUnitVisuals.Document>(File.ReadAllText(catalogPath))?.entries ?? new MeshyUnitVisuals.Entry[0]).ToList();
            var problems = new List<string>();
            int baked = 0;
            foreach (string id in ids)
            {
                var unit = roster.units.FirstOrDefault(u => u.id == id);
                string folder = Root + "/" + id, fbx = folder + "/" + id + ".fbx";
                if (unit == null || !File.Exists(fbx)) { problems.Add(id + ": " + (unit == null ? "not in the roster" : "no " + fbx)); continue; }
                try
                {
                    var entry = Bake(unit, folder, fbx);
                    int index = entries.FindIndex(e => e.id == id);
                    if (index >= 0) entries[index] = entry; else entries.Add(entry);
                    baked++;
                }
                catch (Exception error) { problems.Add(id + ": " + error.Message); }
            }
            File.WriteAllText(catalogPath, JsonUtility.ToJson(new MeshyUnitVisuals.Document { entries = entries.ToArray() }, true));
            AssetDatabase.ImportAsset(catalogPath);
            AssetDatabase.SaveAssets();
            foreach (var problem in problems) Debug.LogError("EMBERFIELD_MESHY_UNIT_FAILED " + problem);
            Debug.Log("EMBERFIELD_MESHY_UNITS_OK count=" + baked + " failed=" + problems.Count);
            if (Application.isBatchMode && problems.Count > 0) EditorApplication.Exit(1);
        }

        /// <summary>The buildings alone, leaving every unit's prefab and controller as it is.</summary>
        [MenuItem("Emberfield/Art/Import Meshy buildings")]
        public static void RunBuildingsOnly()
        {
            var problems = new List<string>();
            int buildings = RunBuildings(problems);
            foreach (var problem in problems) Debug.LogError("EMBERFIELD_MESHY_UNIT_FAILED " + problem);
            Debug.Log("EMBERFIELD_MESHY_BUILDINGS_OK count=" + buildings + " failed=" + problems.Count);
            if (Application.isBatchMode && problems.Count > 0) EditorApplication.Exit(1);
        }

        /// <summary>
        /// The units' Mesh LOD levels alone: the importer settings Bake gives each unit's FBX, and a reimport, leaving
        /// every prefab, controller and catalogue as it is. The levels live in the mesh the prefabs already use.
        /// </summary>
        [MenuItem("Emberfield/Art/Generate Meshy unit mesh levels")]
        public static void RunUnitMeshLevels()
        {
            var roster = JsonUtility.FromJson<Roster>(File.ReadAllText(Path.Combine(Application.dataPath, "../tools/art/meshy_units.json")));
            int count = 0;
            foreach (var unit in roster.units)
            {
                string fbx = Root + "/" + unit.id + "/" + unit.id + ".fbx";
                if (!File.Exists(fbx)) continue;
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
                MeshLevels(importer);
                importer.SaveAndReimport();
                var mesh = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
                Debug.Log("EMBERFIELD_MESHY_UNIT_LEVELS " + unit.id + " levels=" + (mesh != null ? mesh.lodCount : 0) +
                    (mesh != null ? " triangles=" + string.Join("/", Enumerable.Range(0, mesh.lodCount).Select(level => mesh.GetIndexCount(0, level) / 3)) : ""));
                count++;
            }
            Debug.Log("EMBERFIELD_MESHY_UNIT_LEVELS_OK count=" + count);
        }

        // Unity Mesh LOD levels inside the unit's own mesh, each about half the one before. UnitMeshDetail forces one
        // from the zoom on the phone tiers; three below the whole mesh reach the sixth a unit needs at the widest zoom.
        private static void MeshLevels(ModelImporter importer) { importer.generateMeshLods = true; importer.maximumMeshLod = 3; }

        private static MeshyUnitVisuals.Entry Bake(RosterUnit unit, string folder, string fbx)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + "/meshy-manifest.json"));
            if (manifest?.finish == null) throw new InvalidOperationException("run meshy_unit_finish.py first");
            var baseMap = Texture(folder + "/" + unit.id + "_BaseColor.png", TextureImporterType.Default, true);
            var normal = Texture(folder + "/" + unit.id + "_Normal.png", TextureImporterType.NormalMap, false);
            var mask = Texture(folder + "/" + unit.id + "_Mask.png", TextureImporterType.Default, false);

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
            importer.optimizeGameObjects = false; importer.isReadable = false;
            MeshLevels(importer);
            importer.SaveAndReimport();
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var take in importer.defaultClipAnimations)
            {
                string state = States.FirstOrDefault(s => take.takeName == s || take.takeName.EndsWith("|" + s, StringComparison.Ordinal));
                if (state == null || clips.Any(c => c.name == state)) continue;
                take.name = state;
                take.loopTime = take.loopPose = Array.IndexOf(Looping, state) >= 0;
                take.lockRootRotation = take.lockRootHeightY = take.lockRootPositionXZ = true;
                take.keepOriginalOrientation = take.keepOriginalPositionY = take.keepOriginalPositionXZ = true;
                clips.Add(take);
            }
            foreach (string required in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
                if (clips.All(c => c.name != required)) throw new InvalidOperationException("no " + required + " take in " + fbx);
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + unit.id + ".mat");
            if (material == null) { material = new Material(Shader.Find("Emberfield/Meshy Unit")); AssetDatabase.CreateAsset(material, folder + "/" + unit.id + ".mat"); }
            material.shader = Shader.Find("Emberfield/Meshy Unit");
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", baseMap); material.SetTexture("_BumpMap", normal); material.SetTexture("_MaskMap", mask);
            EditorUtility.SetDirty(material);

            var animations = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            string controllerPath = folder + "/" + unit.id + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            foreach (var state in States)
            {
                var clip = animations.FirstOrDefault(c => c.name == state);
                if (clip == null) continue;
                var node = machine.AddState(state); node.motion = clip; node.writeDefaultValues = true;
                if (state == "Idle") machine.defaultState = node;
            }
            EditorUtility.SetDirty(controller);

            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            model.name = unit.id;
            try
            {
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) throw new InvalidOperationException("no skinned mesh");
                int triangles = skins.Sum(s => s.sharedMesh.triangles.Length / 3);
                if (triangles > TriangleBudget) throw new InvalidOperationException(triangles + " triangles, over the budget of " + TriangleBudget);
                // Standing height, from the mesh posed at the first frame of Idle. The renderer's own bounds would
                // not do: the importer widens them to hold every clip, falls and swings included.
                float height = StandingHeight(model, skins, animations.First(c => c.name == "Idle"));
                foreach (var skin in skins)
                {
                    skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray();
                    skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
                    // Recomputing bounds every frame costs a skin walk per unit; attacks and deaths stay inside this.
                    skin.updateWhenOffscreen = false; skin.quality = SkinQuality.Bone4;
                    var box = skin.localBounds; box.Expand(box.size.magnitude * .1f); skin.localBounds = box;
                }
                if (!model.TryGetComponent<Animator>(out var animator)) animator = model.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Fit the standing height to the figure it replaces; the stride speeds follow the same scale so the
                // planted foot keeps pace with the ground.
                float fit = unit.gameHeight / Mathf.Max(.01f, height);
                model.transform.localScale *= fit;
                float units = height / Mathf.Max(.01f, manifest.finish.heightMetres);
                if (!model.TryGetComponent<CorsairAnimationDriver>(out var driver)) driver = model.AddComponent<CorsairAnimationDriver>();
                driver.WalkMetresPerSecond = Mathf.Max(.3f, manifest.finish.walkMetresPerSecond * units * fit * AlphaWorldArt.UnitScale);
                driver.RunMetresPerSecond = Mathf.Max(driver.WalkMetresPerSecond * 1.5f, manifest.finish.runMetresPerSecond * units * fit * AlphaWorldArt.UnitScale);
                // One level: past a speck on screen the unit is not drawn at all, which is also what the other
                // imported characters do and what the view expects of every art unit.
                if (!model.TryGetComponent<LODGroup>(out var group)) group = model.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(.012f, skins.Cast<Renderer>().ToArray()) });
                group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                var prefab = PrefabUtility.SaveAsPrefabAsset(model, Output + "/" + unit.id + ".prefab");
                if (!prefab.GetComponent<CorsairAnimationDriver>().HasValidRig) throw new InvalidOperationException("rig does not validate");
                Debug.Log("EMBERFIELD_MESHY_UNIT " + unit.id + " triangles=" + triangles + " clips=" + string.Join(",", animations.Select(c => c.name)) +
                    " height=" + height.ToString("0.00") + " fit=" + fit.ToString("0.000") + " walk=" + driver.WalkMetresPerSecond.ToString("0.00") + " run=" + driver.RunMetresPerSecond.ToString("0.00"));
                return new MeshyUnitVisuals.Entry { id = unit.id, name = unit.name, culture = unit.culture, unit = unit.unit, factions = unit.factions, prefab = "MeshyUnits/" + unit.id, triangles = triangles, cosmetic = unit.cosmetic };
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static float StandingHeight(GameObject model, SkinnedMeshRenderer[] skins, AnimationClip idle)
        {
            idle.SampleAnimation(model, 0);
            float low = float.MaxValue, high = float.MinValue;
            var baked = new Mesh();
            try
            {
                foreach (var skin in skins)
                {
                    // Without scale the baked vertices are in world units, relative to the renderer's position and turn.
                    skin.BakeMesh(baked, false);
                    foreach (var vertex in baked.vertices)
                    {
                        float y = (skin.transform.position + skin.transform.rotation * vertex).y;
                        low = Mathf.Min(low, y); high = Mathf.Max(high, y);
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); }
            return high - low;
        }

        /// <summary>Buildings from tools/art/meshy_buildings.json: one static mesh, the same material as the units.</summary>
        private static int RunBuildings(List<string> problems)
        {
            var roster = JsonUtility.FromJson<BuildingRoster>(File.ReadAllText(Path.Combine(Application.dataPath, "../tools/art/meshy_buildings.json")));
            Directory.CreateDirectory(BuildingOutput);
            var catalog = new List<MeshyBuildingVisuals.Entry>();
            foreach (var building in roster.buildings)
            {
                string style = char.ToUpperInvariant(building.style[0]) + building.style.Substring(1);
                string folder = "Assets/Models/Buildings/" + style + "/" + building.id, fbx = folder + "/" + building.id + ".fbx";
                if (!File.Exists(fbx)) continue;
                try
                {
                    var culture = building.style == "kingdom" ? roster.styles.kingdom : building.style == "mountain" ? roster.styles.mountain
                        : building.style == "dwarf" ? roster.styles.dwarf : building.style == "elf" ? roster.styles.elf : building.style == "orc" ? roster.styles.orc
                        : building.style == "sultanate" ? roster.styles.sultanate : building.style == "sahel" ? roster.styles.sahel : null;
                    // A style missing from BuildingStyleMap used to bake with no factions, so the building never drew for anyone.
                    if (culture == null) throw new InvalidOperationException("style '" + building.style + "' is not in BuildingStyleMap; add a field there and a branch here");
                    var factions = culture?.factions ?? Array.Empty<string>();
                    // An entry may carry its own grade, as the orc palisade does: darker than the orc houses around it.
                    float grade = building.grade > 0 ? building.grade : culture != null && culture.grade > 0 ? culture.grade : 1;
                    catalog.Add(BakeBuilding(building, factions, folder, fbx, grade));
                }
                catch (Exception error) { problems.Add(building.id + ": " + error.Message); }
            }
            File.WriteAllText(BuildingOutput + "/catalog.json", JsonUtility.ToJson(new MeshyBuildingVisuals.Document { entries = catalog.ToArray() }, true));
            AssetDatabase.ImportAsset(BuildingOutput + "/catalog.json");
            AssetDatabase.SaveAssets();
            return catalog.Count;
        }

        private static MeshyBuildingVisuals.Entry BakeBuilding(RosterBuilding building, string[] factions, string folder, string fbx, float grade)
        {
            var baseMap = Texture(folder + "/" + building.id + "_BaseColor.png", TextureImporterType.Default, true);
            var normal = Texture(folder + "/" + building.id + "_Normal.png", TextureImporterType.NormalMap, false);
            var mask = Texture(folder + "/" + building.id + "_Mask.png", TextureImporterType.Default, false);
            // The far level, where tools/art/meshy_lod.py made one, comes in with the same settings as the near one.
            string farFbx = folder + "/" + building.id + "_LOD1.fbx";
            ImportStatic(fbx);
            if (File.Exists(farFbx)) ImportStatic(farFbx);
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + building.id + ".mat");
            if (material == null) { material = new Material(Shader.Find("Emberfield/Meshy Unit")); AssetDatabase.CreateAsset(material, folder + "/" + building.id + ".mat"); }
            material.shader = Shader.Find("Emberfield/Meshy Unit");
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", baseMap); material.SetTexture("_BumpMap", normal); material.SetTexture("_MaskMap", mask);
            material.SetFloat("_RimStrength", .03f);
            // Some cultures paint dark (dwarf granite, orc logs); a grade lifts them off the dark forest floor.
            material.SetColor("_Grade", new Color(grade, grade, grade, 1));
            EditorUtility.SetDirty(material);
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            model.name = building.id;
            try
            {
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("no mesh");
                int triangles = 0;
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    triangles += renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                }
                if (triangles > 10000) throw new InvalidOperationException(triangles + " triangles, over the building budget of 10000");
                int farTriangles = File.Exists(farFbx) ? AddFarLevel(model, renderers, farFbx, material) : 0;
                PrefabUtility.SaveAsPrefabAsset(model, BuildingOutput + "/" + building.id + ".prefab");
                Debug.Log("EMBERFIELD_MESHY_BUILDING " + building.id + " triangles=" + triangles + (farTriangles > 0 ? "/" + farTriangles : ""));
                return new MeshyBuildingVisuals.Entry { id = building.id, style = building.style, building = building.building, factions = factions,
                    prefab = "MeshyBuildings/" + building.id, triangles = triangles, farTriangles = farTriangles, height = building.height };
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void ImportStatic(string fbx)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false;
            importer.importCameras = false; importer.importLights = false; importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Hangs the far level under the building as "LOD1" and gives the building a LOD group. The far FBX was written
        /// from the near one's own vertices, so both roots import the same way and the far level keeps its world pose.
        /// It is never culled: a building is play information at every zoom.
        /// </summary>
        private static int AddFarLevel(GameObject model, MeshRenderer[] near, string farFbx, Material material)
        {
            var farModel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(farFbx));
            try
            {
                var far = farModel.GetComponentsInChildren<MeshRenderer>(true);
                if (far.Length != 1) throw new InvalidOperationException(far.Length + " meshes in " + farFbx);
                var level = far[0].transform;
                level.SetParent(model.transform, true); level.name = "LOD1";
                far[0].sharedMaterials = Enumerable.Repeat(material, far[0].sharedMaterials.Length).ToArray();
                far[0].shadowCastingMode = ShadowCastingMode.On; far[0].receiveShadows = true;
                Bounds Measure(Renderer[] renderers) { var box = renderers[0].bounds; foreach (var r in renderers) box.Encapsulate(r.bounds); return box; }
                Bounds nearBox = Measure(near), farBox = Measure(far);
                // Collapsing rounds corners in, never much: a far level off by more than this came in on the wrong axes.
                if ((nearBox.size - farBox.size).magnitude > nearBox.size.magnitude * .05f || (nearBox.center - farBox.center).magnitude > nearBox.size.magnitude * .05f)
                    throw new InvalidOperationException("the far level does not stand where the near one does: " + nearBox + " / " + farBox);
                if (!model.TryGetComponent<LODGroup>(out var group)) group = model.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(MeshyBuildingVisuals.FarLevelHeight, near.Cast<Renderer>().ToArray()), new LOD(0, far.Cast<Renderer>().ToArray()) });
                group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                int triangles = far[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, nearTriangles = near.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
                if (triangles * 1.5f > nearTriangles) throw new InvalidOperationException("the far level keeps " + triangles + " of " + nearTriangles + " triangles");
                return triangles;
            }
            // A file with one mesh comes in with it on its root, which is then the far level itself and stays.
            finally { if (farModel && farModel.transform.parent == null) Object.DestroyImmediate(farModel); }
        }

        private static Texture2D Texture(string path, TextureImporterType type, bool colour)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("texture missing", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type; importer.sRGBTexture = colour && type == TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.anisoLevel = 2;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            MobileTextureImport.Configure(importer, path);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
