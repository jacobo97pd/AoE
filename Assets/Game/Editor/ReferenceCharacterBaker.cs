using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    public static class ReferenceCharacterBaker
    {
        public const string Root = "Assets/Game/ReferenceCharacters";
        public const string ScenePath = "Assets/Game/Scenes/CharacterCollection.unity";
        private const string Sources = Root + "/SourceModels/";
        [Serializable] private sealed class Document { public Entry[] entries; }
        [Serializable] private sealed class Entry
        {
            public string id, name, family, role, suggestedFaction, realm, modelPath, sourceBlendPath, referenceImage, interpretationNotes;
            public float heightMetres;
            public Surface[] materials;
            public Locomotion locomotion;
        }
        [Serializable] private sealed class Locomotion { public float walkMetresPerSecond, runMetresPerSecond; }
        [Serializable] private sealed class Surface
        {
            public string name, baseMap, normalMap, metallicGlossMap;
            public float[] baseColor, tiling;
            public float metallic, smoothness, normalScale = 1;
        }
        [Serializable] private sealed class Evidence
        {
            public string generatedUtc, unity, scene = ScenePath;
            public bool passed;
            public List<CharacterEvidence> characters = new List<CharacterEvidence>();
        }
        [Serializable] private sealed class CharacterEvidence
        {
            public string id, name, prefab;
            public bool validRig, materialMaps, runDeforms, fallDeforms;
            public int bones, lods;
            public int[] triangles;
            public string[] clips, fallbackStates;
        }
        private static readonly HashSet<string> importedTextures = new HashSet<string>();

        [MenuItem("Emberfield/Art/Import reference character collection")]
        public static void Run()
        {
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Animations");
            Directory.CreateDirectory(Root + "/Resources/ReferenceUnits"); Directory.CreateDirectory(Root + "/Resources/ReferenceCharacters");
            AssetDatabase.Refresh(); importedTextures.Clear();
            var manifest = JsonUtility.FromJson<Document>(File.ReadAllText(Sources + "manifest.json"));
            if (manifest?.entries == null || manifest.entries.Length == 0) throw new InvalidOperationException("Reference manifest has no exports.");
            var catalog = new List<ReferenceCharacterEntry>();
            var evidence = new Evidence { generatedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, passed = true };
            foreach (var entry in manifest.entries.OrderBy(e => e.id))
            {
                var materials = entry.materials.ToDictionary(m => m.name, m => Material(entry.id, m), StringComparer.OrdinalIgnoreCase);
                string path = Sources + entry.modelPath;
                Import(path, materials);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                var fallback = new List<string>();
                var controller = Controller(entry, clips, fallback);
                var prefab = Prefab(entry, path, materials, controller);
                var record = Validate(entry, prefab, clips, fallback); evidence.characters.Add(record);
                evidence.passed &= record.validRig && record.materialMaps && record.runDeforms && record.fallDeforms && record.lods == 3;
                catalog.Add(new ReferenceCharacterEntry { id = entry.id, name = entry.name, family = entry.family, role = entry.role, suggestedFaction = entry.suggestedFaction, realm = entry.realm, modelPath = path, sourceBlendPath = entry.sourceBlendPath, referenceImage = entry.referenceImage, interpretationNotes = entry.interpretationNotes, heightMetres = entry.heightMetres, prefabResourcePath = "ReferenceUnits/" + entry.id });
            }
            File.WriteAllText(Root + "/Resources/ReferenceCharacters/catalog.json", JsonUtility.ToJson(new ReferenceCharacterDocument { entries = catalog.ToArray() }, true));
            AssetDatabase.Refresh(); BuildScene(); AssetDatabase.SaveAssets();
            const string folder = "Artifacts/ArtReview/reference-characters";
            Directory.CreateDirectory(folder); File.WriteAllText(folder + "/unity-import-report.json", JsonUtility.ToJson(evidence, true));
            if (!evidence.passed) throw new InvalidOperationException("Character import validation failed; see unity-import-report.json.");
            if (Environment.GetCommandLineArgs().Contains("-requireAllCharacters") && catalog.Count != 26) throw new InvalidOperationException("Expected all 26 figures, imported " + catalog.Count);
            Debug.Log("REFERENCE_CHARACTER_IMPORT_OK count=" + catalog.Count);
        }
        private static Texture2D Texture(string path, bool normal = false, bool srgb = true)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!File.Exists(path)) throw new FileNotFoundException("Material map missing", path);
            if (importedTextures.Add(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && srgb; importer.convertToNormalmap = false;
                importer.maxTextureSize = 2048; importer.mipmapEnabled = true; importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        private static Material Material(string id, Surface source)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name, enableInstancing = true };
            float[] color = source.baseColor ?? new[] { 1f, 1f, 1f, 1f };
            mat.SetColor("_BaseColor", new Color(color[0], color[1], color[2], color.Length > 3 ? color[3] : 1));
            mat.SetTexture("_BaseMap", Texture(source.baseMap)); mat.SetFloat("_Metallic", source.metallic); mat.SetFloat("_Smoothness", source.smoothness);
            if (!string.IsNullOrEmpty(source.normalMap)) { mat.SetTexture("_BumpMap", Texture(source.normalMap, true, false)); mat.SetFloat("_BumpScale", source.normalScale); mat.EnableKeyword("_NORMALMAP"); }
            if (!string.IsNullOrEmpty(source.metallicGlossMap)) { mat.SetTexture("_MetallicGlossMap", Texture(source.metallicGlossMap, false, false)); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (source.tiling != null && source.tiling.Length == 2) mat.SetTextureScale("_BaseMap", new Vector2(source.tiling[0], source.tiling[1]));
            mat.SetFloat("_Cull", 0); mat.doubleSidedGI = true;
            return PirateCharacterAssetImport.Save(mat, Root + "/Materials/" + id + "_" + source.name + ".mat");
        }
        private static bool Matches(string name, string state) => !string.IsNullOrEmpty(name) && (name.Equals(state, StringComparison.OrdinalIgnoreCase) || name.EndsWith("_" + state, StringComparison.OrdinalIgnoreCase) || name.EndsWith("|" + state, StringComparison.OrdinalIgnoreCase));
        private static void Import(string path, Dictionary<string, Material> materials)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false; importer.importLights = false; importer.globalScale = 1; importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = true; importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            var clips = new List<ModelImporterClipAnimation>();
            foreach (string state in new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Work", "Aim" })
            {
                var source = importer.defaultClipAnimations.FirstOrDefault(c => Matches(c.name, state) || Matches(c.takeName, state) || state == "Death" && (Matches(c.name, "Fall") || Matches(c.takeName, "Fall")));
                if (source == null) { if (state == "Idle" || state == "Run" || state == "Death") throw new InvalidOperationException(path + " missing " + state); else continue; }
                source.name = "Corsair_" + state;
                source.loopTime = state == "Idle" || state == "Walk" || state == "Run" || state == "Work" || state == "Aim"; source.loopPose = source.loopTime;
                source.lockRootRotation = true; source.lockRootHeightY = true; source.lockRootPositionXZ = true;
                source.keepOriginalOrientation = true; source.keepOriginalPositionY = true; source.keepOriginalPositionXZ = true;
                clips.Add(source);
            }
            importer.clipAnimations = clips.ToArray(); importer.SaveAndReimport();
        }
        private static AnimatorController Controller(Entry entry, AnimationClip[] clips, List<string> fallback)
        {
            string path = Root + "/Animations/" + entry.id + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var old in machine.states) machine.RemoveState(old.state);
            foreach (string state in new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death" })
            {
                var clip = clips.FirstOrDefault(c => c.name == "Corsair_" + state);
                bool substitute = !clip;
                if (!clip) { clip = clips.First(c => c.name == (state == "Walk" ? "Corsair_Run" : "Corsair_Idle")); fallback.Add(state + " uses " + clip.name + (state == "Walk" ? " at half speed" : " (combat animation pending)")); }
                var node = machine.AddState(state); node.motion = clip; node.writeDefaultValues = true;
                if (substitute && state == "Walk") node.speed = .5f;
                if (state == "Idle") machine.defaultState = node;
            }
            EditorUtility.SetDirty(controller); return controller;
        }
        private static GameObject Prefab(Entry entry, string path, Dictionary<string, Material> materials, AnimatorController controller)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); model.name = entry.id;
            try
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = true;
                    for (var parent = renderer.transform; parent && parent != model.transform; parent = parent.parent) parent.gameObject.SetActive(true);
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) if (slots[i] && materials.TryGetValue(slots[i].name, out var replacement)) slots[i] = replacement;
                    renderer.sharedMaterials = slots; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen = true; skin.quality = SkinQuality.Bone4; }
                }
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var driver = model.GetComponent<CorsairAnimationDriver>() ?? model.AddComponent<CorsairAnimationDriver>();
                driver.WalkMetresPerSecond = entry.locomotion?.walkMetresPerSecond ?? 1.7f; driver.RunMetresPerSecond = entry.locomotion?.runMetresPerSecond ?? 3.4f;
                var group = model.GetComponent<LODGroup>() ?? model.AddComponent<LODGroup>();
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var lods = new List<LOD>(); float[] heights = { .45f, .18f, .012f };
                for (int i = 0; i < 3; i++)
                {
                    var selected = skins.Where(s => s.name.IndexOf("LOD" + i, StringComparison.OrdinalIgnoreCase) >= 0).Cast<Renderer>().ToArray();
                    if (selected.Length == 0) throw new InvalidOperationException(entry.id + " missing LOD" + i);
                    lods.Add(new LOD(heights[i], selected));
                }
                group.SetLODs(lods.ToArray()); group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(model, Root + "/Resources/ReferenceUnits/" + entry.id + ".prefab");
            }
            finally { Object.DestroyImmediate(model); }
        }
        private static CharacterEvidence Validate(Entry entry, GameObject prefab, AnimationClip[] clips, List<string> fallback)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                var driver = instance.GetComponent<CorsairAnimationDriver>(); var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var skin = skins.OrderByDescending(s => s.sharedMesh.vertexCount).First();
                return new CharacterEvidence { id = entry.id, name = entry.name, prefab = AssetDatabase.GetAssetPath(prefab), validRig = driver.HasValidRig, bones = skin.bones.Length, lods = instance.GetComponent<LODGroup>().lodCount,
                    materialMaps = skins.All(s => s.sharedMaterials.All(m => m && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap"))),
                    triangles = skins.OrderBy(s => s.name).Select(s => s.sharedMesh.triangles.Length / 3).ToArray(), clips = clips.Select(c => c.name).ToArray(), fallbackStates = fallback.ToArray(),
                    runDeforms = Deforms(instance, skin, clips.First(c => c.name == "Corsair_Run")), fallDeforms = Deforms(instance, skin, clips.First(c => c.name == "Corsair_Death")) };
            }
            finally { Object.DestroyImmediate(instance); }
        }
        private static bool Deforms(GameObject model, SkinnedMeshRenderer skin, AnimationClip clip)
        {
            var mesh = new Mesh();
            try
            {
                clip.SampleAnimation(model, 0); skin.BakeMesh(mesh); var before = mesh.vertices;
                clip.SampleAnimation(model, clip.length * .43f); skin.BakeMesh(mesh); var after = mesh.vertices;
                for (int i = 0; i < before.Length; i += Math.Max(1, before.Length / 2000)) if (Vector3.Distance(before[i], after[i]) > .00001f) return true;
                return false;
            }
            finally { Object.DestroyImmediate(mesh); }
        }
        [MenuItem("Emberfield/Characters/Refresh presentation scene")]
        public static void RefreshPresentation()
        {
            BuildScene(); AssetDatabase.SaveAssets();
        }
        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.49f, .56f, .66f); RenderSettings.ambientEquatorColor = new Color(.31f, .33f, .36f); RenderSettings.ambientGroundColor = new Color(.18f, .16f, .13f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.16f, .185f, .21f); RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .04f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Game/RoyalSoldier/Textures/SoldierReflection.hdr");
            RenderSettings.reflectionIntensity = .55f;
            var camera = new GameObject("Presentation camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.backgroundColor = RenderSettings.fogColor; camera.clearFlags = CameraClearFlags.SolidColor; camera.nearClipPlane = .05f; camera.farClipPlane = 150;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>(); cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cameraData.antialiasingQuality = AntialiasingQuality.High;
            var viewer = new GameObject("Reference character collection").AddComponent<ReferenceCharacterReview>(); viewer.ViewCamera = camera;
            viewer.Pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Game/CrimsonCorsair/Rendering/CorsairPipeline.asset");
            if (!viewer.Pipeline || !RenderSettings.customReflectionTexture) throw new InvalidOperationException("Character presentation requires the existing desktop renderer and reflection map.");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Slate presentation floor"; floor.transform.localScale = Vector3.one * 100;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Slate" }; material.SetColor("_BaseColor", new Color(.115f, .14f, .16f)); material.SetFloat("_Smoothness", .26f);
            floor.GetComponent<Renderer>().sharedMaterial = PirateCharacterAssetImport.Save(material, Root + "/Materials/PresentationFloor.mat");
            AddLight("Warm key", new Vector3(55, -38, 0), new Color(1, .91f, .81f), 1.6f, true);
            AddLight("Cool fill", new Vector3(28, 128, 0), new Color(.61f, .76f, 1), .7f, false);
            AddLight("Edge", new Vector3(18, 180, 0), new Color(1, .80f, .58f), 1, false);
            var volume = new GameObject("Presentation grade").AddComponent<Volume>(); volume.isGlobal = true;
            const string profilePath = Root + "/Materials/PresentationVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            foreach (var component in profile.components) if (component) Object.DestroyImmediate(component, true);
            profile.components.Clear(); profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>().postExposure.Override(.15f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile); volume.sharedProfile = profile;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        private static void AddLight(string name, Vector3 angle, Color color, float intensity, bool shadow)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(angle); light.color = color; light.intensity = intensity;
            light.shadows = shadow ? LightShadows.Soft : LightShadows.None; light.shadowBias = .025f; light.shadowNormalBias = .12f;
        }
    }
}
