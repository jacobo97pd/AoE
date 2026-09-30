using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>Imports the supplied pirate as a persistent animated character and builds a real review player.</summary>
    public static class CrimsonCorsairBaker
    {
        private const string Root = "Assets/Game/CrimsonCorsair";
        private const string Review = "Artifacts/ArtReview/crimson-corsair";
        private const string ScenePath = "Assets/Game/Scenes/CrimsonCorsair.unity";
        private const string ModelPath = Root + "/Model/CrimsonCorsair.fbx";
        private const string PrefabPath = Root + "/Resources/ImportedUnits/CrimsonCorsair.prefab";
        private static readonly string[] States = { "Idle", "Walk", "Run", "Attack", "Hit", "Death" };

        [Serializable] private sealed class MeshEvidence
        {
            public string name, path;
            public int vertices, triangles, bones;
            public bool uv, normals, weights;
        }
        [Serializable] private sealed class ClipEvidence
        {
            public string name, path;
            public float seconds, sampleDisplacement;
            public bool loop, motion;
        }
        [Serializable] private sealed class Evidence
        {
            public string generatedUtc, unity, scene = ScenePath, prefab = PrefabPath;
            public bool passed;
            public int lods;
            public List<MeshEvidence> meshes = new List<MeshEvidence>();
            public List<ClipEvidence> clips = new List<ClipEvidence>();
            public List<string> errors = new List<string>();
            public string scope = "Real imported skinned geometry, persistent URP material, six animation clips and three LOD levels. This checks asset wiring and sampled vertex deformation, not artistic acceptance or army frame rate.";
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use tools/Build-CrimsonCorsair.ps1.");
            string oldQuality = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            string oldDefault = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            int oldAA = QualitySettings.antiAliasing, exit = 0;
            try
            {
                foreach (string folder in new[] { Root + "/Materials", Root + "/Rendering", Root + "/Animations", Root + "/Resources/ImportedUnits", Review }) Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                var args = Environment.GetCommandLineArgs();
                bool playerOnly = args.Contains("-corsairPlayerOnly"), sceneOnly = args.Contains("-corsairSceneOnly");
                var pipeline = playerOnly ? AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/Rendering/CorsairPipeline.asset") : MakePipeline();
                if (!pipeline) throw new InvalidOperationException("Build the corsair scene first.");
                QualitySettings.renderPipeline = pipeline;
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.antiAliasing = 4;
                if (!playerOnly)
                {
                    if (!sceneOnly) ImportCharacter();
                    var material = sceneOnly ? AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/CrimsonCorsair.mat") : MakeMaterial();
                    var controller = sceneOnly ? AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animations/CrimsonCorsair.controller") : MakeController();
                    BuildScene(material, controller, pipeline);
                    AssetDatabase.SaveAssets();
                }
                EditorSceneManager.OpenScene(ScenePath);
                var viewer = Object.FindFirstObjectByType<CrimsonCorsairReview>();
                Validate(viewer);
                if (!playerOnly) Capture(viewer);
                if (args.Contains("-corsairPlayer"))
                {
                    var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { ScenePath, "Assets/Game/Scenes/Greybox.unity" },
                        target = BuildTarget.StandaloneWindows64,
                        locationPathName = "Builds/CrimsonCorsair/CrimsonCorsair.exe",
                        options = BuildOptions.Development
                    });
                    File.WriteAllText(Review + "/build.txt", $"Result: {build.summary.result}\nErrors: {build.summary.totalErrors}\nWarnings: {build.summary.totalWarnings}\nBytes: {build.summary.totalSize}\nDuration: {build.summary.totalTime}\n");
                    File.AppendAllLines(Review + "/build.txt", build.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Warning || m.type == LogType.Error).Select(m => m.type + ": " + m.content));
                    if (build.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Corsair player build failed.");
                }
                Debug.Log("CRIMSON_CORSAIR_OK");
            }
            catch (Exception e) { Debug.LogException(e); exit = 1; }
            finally
            {
                QualitySettings.renderPipeline = string.IsNullOrEmpty(oldQuality) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldQuality);
                GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(oldDefault) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldDefault);
                QualitySettings.antiAliasing = oldAA;
                AssetDatabase.SaveAssets();
            }
            if (exit != 0) EditorApplication.Exit(exit);
        }

        public static void CaptureOnly()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use tools/Build-CrimsonCorsair.ps1 -CaptureOnly.");
            var oldQuality = QualitySettings.renderPipeline;
            var oldDefault = GraphicsSettings.defaultRenderPipeline;
            int oldAA = QualitySettings.antiAliasing, exit = 0;
            try
            {
                Directory.CreateDirectory(Review);
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/Rendering/CorsairPipeline.asset");
                if (!pipeline) throw new InvalidOperationException("The saved corsair pipeline is missing.");
                QualitySettings.renderPipeline = pipeline; GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.antiAliasing = 4;
                EditorSceneManager.OpenScene(ScenePath);
                var viewer = Object.FindFirstObjectByType<CrimsonCorsairReview>();
                Validate(viewer);
                Capture(viewer);
                Debug.Log("CRIMSON_CORSAIR_CAPTURE_OK");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally
            {
                QualitySettings.renderPipeline = oldQuality;
                GraphicsSettings.defaultRenderPipeline = oldDefault;
                QualitySettings.antiAliasing = oldAA;
                // Deliberately no scene, prefab or AssetDatabase.SaveAssets call.
            }
            if (exit != 0) EditorApplication.Exit(exit);
        }

        private static T Save<T>(T value, string path) where T : Object
        {
            var old = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!old) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, old);
            EditorUtility.SetDirty(old);
            Object.DestroyImmediate(value);
            return old;
        }

        private static void ImportCharacter()
        {
            if (!File.Exists(ModelPath)) throw new FileNotFoundException("The rigged pirate FBX is missing.", ModelPath);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false; importer.importLights = false;
            importer.globalScale = 1; importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var available = importer.defaultClipAnimations;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (string state in States)
            {
                var clip = available.FirstOrDefault(c => c.name.IndexOf("Corsair_" + state, StringComparison.OrdinalIgnoreCase) >= 0 || c.takeName.IndexOf("Corsair_" + state, StringComparison.OrdinalIgnoreCase) >= 0);
                if (clip == null) throw new InvalidOperationException("Missing FBX animation " + state + "; available: " + string.Join(", ", available.Select(c => c.name)));
                clip.name = "Corsair_" + state;
                clip.loopTime = state == "Idle" || state == "Walk" || state == "Run";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static Texture2D Map(string name, bool normal, bool srgb)
        {
            string path = Root + "/Textures/" + name;
            if (!File.Exists(path)) throw new FileNotFoundException("Missing supplied pirate texture.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.convertToNormalmap = false;
            importer.maxTextureSize = 4096; importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MakeMaterial()
        {
            // Decode data maps as linear bytes. Smoothness is explicitly 1 - roughness.
            var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!metallic.LoadImage(File.ReadAllBytes(Root + "/Textures/Metallic.png")) || !roughness.LoadImage(File.ReadAllBytes(Root + "/Textures/Roughness.png"))) throw new InvalidOperationException("Unable to decode the supplied PBR maps.");
                if (metallic.width != roughness.width || metallic.height != roughness.height) throw new InvalidOperationException("Metallic and roughness texture sizes differ.");
                var metal = metallic.GetPixels32(); var rough = roughness.GetPixels32();
                for (int i = 0; i < metal.Length; i++) metal[i] = new Color32(metal[i].r, 0, 0, (byte)(255 - rough[i].r));
                metallic.SetPixels32(metal); metallic.Apply();
                File.WriteAllBytes(Root + "/Textures/MetallicSmoothness.png", metallic.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(metallic); Object.DestroyImmediate(roughness); }
            AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit is unavailable.");
            var material = new Material(shader) { name = "CrimsonCorsair", enableInstancing = true };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Map("BaseColor.png", false, true));
            material.SetTexture("_BumpMap", Map("Normal.png", true, false));
            material.SetTexture("_MetallicGlossMap", Map("MetallicSmoothness.png", false, false));
            material.SetFloat("_WorkflowMode", 1); material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_BumpScale", 1); material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2);
            material.SetFloat("_SrcBlend", 1); material.SetFloat("_DstBlend", 0); material.SetFloat("_ZWrite", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            return Save(material, Root + "/Materials/CrimsonCorsair.mat");
        }

        private static AnimationClip Clip(string state)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && c.name == "Corsair_" + state)
                ?? throw new InvalidOperationException("The imported clip is missing: " + state);
        }

        private static AnimatorController MakeController()
        {
            string path = Root + "/Animations/CrimsonCorsair.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
            foreach (string state in States)
            {
                var node = machine.AddState(state);
                node.motion = Clip(state); node.writeDefaultValues = true;
                if (state == "Idle") machine.defaultState = node;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject Character(Material material, AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(source);
            character.name = "Corsario Carmesí";
            foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                if (renderer is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen = true; skin.quality = SkinQuality.Bone4; }
            }
            var animator = character.GetComponent<Animator>();
            if (!animator) animator = character.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var driver = character.GetComponent<CorsairAnimationDriver>();
            if (!driver) driver = character.AddComponent<CorsairAnimationDriver>();
            PirateCharacterAssetImport.ApplyLocomotion(driver, Root + "/model-manifest.json");
            var group = character.GetComponent<LODGroup>();
            if (!group) group = character.AddComponent<LODGroup>();
            var all = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in all)
            {
                // Blender viewport/export visibility must not defeat Unity's LODGroup.
                skin.enabled = true;
                for (var node = skin.transform; node != null && node != character.transform; node = node.parent)
                    node.gameObject.SetActive(true);
            }
            var lods = new List<LOD>();
            float[] thresholds = { .32f, .12f, .012f };
            for (int level = 0; level < 3; level++)
            {
                string key = "LOD" + level;
                var renderers = all.Where(r => r.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).Cast<Renderer>().ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No skinned renderer found for " + key + ".");
                lods.Add(new LOD(thresholds[level], renderers));
            }
            group.SetLODs(lods.ToArray()); group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAssetAndConnect(character, PrefabPath, InteractionMode.AutomatedAction);
            return character;
        }

        private static void BuildScene(Material material, AnimatorController controller, UniversalRenderPipelineAsset pipeline)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var character = Character(material, controller);
            character.transform.rotation = Quaternion.Euler(0, 180, 0);
            var stageSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/RoyalSoldier/Stage/Stage.fbx");
            if (!stageSource) throw new FileNotFoundException("The presentation courtyard stage is missing.");
            var stage = (GameObject)PrefabUtility.InstantiatePrefab(stageSource);
            stage.name = "Patio del corsario"; stage.transform.rotation = Quaternion.Euler(0, 180, 0);
            var crimson = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CorsairBanner" };
            crimson.SetColor("_BaseColor", new Color(.28f, .022f, .035f)); crimson.SetFloat("_Smoothness", .23f);
            crimson = Save(crimson, Root + "/Materials/CorsairBanner.mat");
            foreach (var renderer in stage.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    string key = System.Text.RegularExpressions.Regex.Replace(m.name, @"\.\d{3}$", "");
                    return key == "RS_BlueCloth" ? crimson : AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/RoyalSoldier/Materials/" + key + ".mat") ?? throw new FileNotFoundException("Missing stage material " + key);
                }).ToArray();
            }
            var viewer = new GameObject("Inspección del Corsario Carmesí").AddComponent<CrimsonCorsairReview>();
            viewer.Character = character; viewer.Animator = character.GetComponent<Animator>(); viewer.Pipeline = pipeline;
            viewer.Cameras = new[]
            {
                MakeCamera("Cercana", new Vector3(-2.5f, 2.1f, -5.8f), new Vector3(0, 1.25f, 0), false, 31, 0),
                MakeCamera("Media", new Vector3(-4.1f, 3.1f, -7.2f), new Vector3(0, 1.2f, 0), false, 32, 0),
                MakeCamera("RTS", new Vector3(-5, 7, -8), new Vector3(0, .85f, 0), true, 40, 3.3f)
            };
            viewer.Cameras[0].tag = "MainCamera"; viewer.gameObject.AddComponent<AudioListener>();
            Lighting(); viewer.SetView(0);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static UniversalRenderPipelineAsset MakePipeline()
        {
            string path = Root + "/Rendering/CorsairRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (!renderer)
            {
                renderer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
                renderer.name = "CorsairRenderer"; renderer.rendererFeatures.Clear(); AssetDatabase.CreateAsset(renderer, path);
            }
            var rendererData = new SerializedObject(renderer); rendererData.FindProperty("m_RenderingMode").intValue = 0; rendererData.ApplyModifiedPropertiesWithoutUndo();
            var ao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (!ao) { ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ao.name = "Corsair contact shadows"; AssetDatabase.AddObjectToAsset(ao, renderer); renderer.rendererFeatures.Add(ao); }
            var settings = new SerializedObject(ao);
            settings.FindProperty("m_Settings.AOMethod").intValue = 1; settings.FindProperty("m_Settings.Source").intValue = 1;
            settings.FindProperty("m_Settings.Downsample").boolValue = false; settings.FindProperty("m_Settings.Samples").intValue = 0;
            settings.FindProperty("m_Settings.Intensity").floatValue = .38f; settings.FindProperty("m_Settings.Radius").floatValue = .13f;
            settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .04f; settings.FindProperty("m_Settings.Falloff").floatValue = 30;
            settings.ApplyModifiedPropertiesWithoutUndo(); ao.SetActive(true); ao.Create(); renderer.SetDirty(); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(ao);
            var pipeline = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset")); pipeline.name = "CorsairPipeline";
            var data = new SerializedObject(pipeline); data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            data.FindProperty("m_MSAA").intValue = 4; data.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            data.FindProperty("m_ShadowDistance").floatValue = 30; data.FindProperty("m_ShadowCascadeCount").intValue = 2;
            data.FindProperty("m_UseSRPBatcher").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            return Save(pipeline, Root + "/Rendering/CorsairPipeline.asset");
        }

        private static Camera MakeCamera(string name, Vector3 position, Vector3 target, bool orthographic, float fov, float size)
        {
            var camera = new GameObject(name, typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = position; camera.transform.LookAt(target); camera.orthographic = orthographic;
            camera.orthographicSize = size; camera.fieldOfView = fov; camera.nearClipPlane = .03f; camera.farClipPlane = 100;
            camera.allowHDR = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.25f, .29f, .32f); camera.aspect = 16f / 9;
            var extra = camera.GetUniversalAdditionalCameraData(); extra.renderPostProcessing = true;
            extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; extra.antialiasingQuality = AntialiasingQuality.High;
            return camera;
        }

        private static void Lighting()
        {
            var reflection = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Game/RoyalSoldier/Textures/SoldierReflection.hdr");
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = reflection; RenderSettings.reflectionIntensity = .55f;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.48f, .56f, .66f);
            RenderSettings.ambientEquatorColor = new Color(.34f, .37f, .41f); RenderSettings.ambientGroundColor = new Color(.14f, .125f, .10f); RenderSettings.fog = false;
            var sun = Light("Luz principal cálida", new Color(1, .92f, .82f), 1.55f, new Vector3(38, -35, 0));
            sun.shadows = LightShadows.Soft; sun.shadowBias = .012f; sun.shadowNormalBias = .10f; sun.shadowStrength = .7f; RenderSettings.sun = sun;
            Light("Relleno del cielo", new Color(.72f, .82f, 1), .55f, new Vector3(20, 35, 0));
            Light("Contorno del abrigo", new Color(1, .86f, .67f), .45f, new Vector3(35, 160, 0));
            string path = Root + "/Rendering/CorsairGrade.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "CorsairGrade"; AssetDatabase.CreateAsset(profile, path); }
            foreach (var old in profile.components.ToArray()) Object.DestroyImmediate(old, true);
            profile.components.Clear();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true); grade.postExposure.Override(.1f); grade.contrast.Override(3); grade.saturation.Override(-2);
            profile.Add<Vignette>(true).intensity.Override(.08f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Color de presentación", typeof(Volume)).GetComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
        }

        private static Light Light(string name, Color color, float intensity, Vector3 angles)
        {
            var light = new GameObject(name, typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
            light.color = color; light.intensity = intensity; light.transform.rotation = Quaternion.Euler(angles); return light;
        }

        private static void Validate(CrimsonCorsairReview viewer)
        {
            var evidence = new Evidence { generatedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            if (!viewer || !viewer.Character || !viewer.Animator) throw new InvalidOperationException("Incomplete corsair presentation scene.");
            var skins = viewer.Character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in skins)
            {
                var mesh = skin.sharedMesh;
                var record = new MeshEvidence { name = skin.name, path = AssetDatabase.GetAssetPath(mesh), vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3, bones = skin.bones.Length, uv = mesh.uv.Length == mesh.vertexCount, normals = mesh.normals.Length == mesh.vertexCount, weights = mesh.boneWeights.Length == mesh.vertexCount };
                evidence.meshes.Add(record);
                if (string.IsNullOrEmpty(record.path) || !record.uv || !record.normals || !record.weights || record.bones < 10) evidence.errors.Add("Incomplete skinned mesh " + skin.name);
            }
            if (skins.Length == 0) evidence.errors.Add("No skinned renderers.");
            var group = viewer.Character.GetComponent<LODGroup>(); evidence.lods = group ? group.lodCount : 0;
            if (evidence.lods != 3) evidence.errors.Add("Three LOD levels are required.");
            if (!viewer.Animator.avatar || !viewer.Animator.avatar.isValid) evidence.errors.Add("Generic avatar is invalid.");
            foreach (var material in skins.SelectMany(r => r.sharedMaterials).Distinct())
            {
                if (!material || !material.shader || !material.shader.isSupported || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material))) evidence.errors.Add("Invalid corsair material.");
                else foreach (var message in ShaderUtil.GetShaderMessages(material.shader)) if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) evidence.errors.Add(message.message);
            }
            AnimationMode.StartAnimationMode();
            try
            {
                var skin = skins.FirstOrDefault(s => s.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0);
                foreach (string state in States)
                {
                    var clip = Clip(state); float movement = 0;
                    if (skin)
                    {
                        var baked = new Mesh();
                        Sample(viewer, clip, 0); skin.BakeMesh(baked); var before = baked.vertices;
                        Sample(viewer, clip, clip.length * .45f); skin.BakeMesh(baked); var after = baked.vertices;
                        for (int i = 0; i < before.Length; i += Math.Max(1, before.Length / 1000)) movement = Mathf.Max(movement, Vector3.Distance(before[i], after[i]));
                        Object.DestroyImmediate(baked);
                    }
                    evidence.clips.Add(new ClipEvidence { name = clip.name, path = AssetDatabase.GetAssetPath(clip), seconds = clip.length, loop = clip.isLooping, sampleDisplacement = movement, motion = movement > .0001f });
                    if (clip.length <= .05f || movement <= .0001f) evidence.errors.Add("Clip has no sampled deformation: " + state);
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            evidence.passed = evidence.errors.Count == 0;
            File.WriteAllText(Review + "/asset-validation.json", JsonUtility.ToJson(evidence, true));
            if (!evidence.passed) throw new InvalidOperationException(string.Join("; ", evidence.errors));
        }

        private static void Sample(CrimsonCorsairReview viewer, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(viewer.Animator.gameObject, clip, time);
            AnimationMode.EndSampling();
        }

        private static void Capture(CrimsonCorsairReview viewer)
        {
            viewer.Controls = false;
            var lod = viewer.Character.GetComponent<LODGroup>(); lod.ForceLOD(0);
            AnimationMode.StartAnimationMode();
            try
            {
                Sample(viewer, Clip("Idle"), .2f);
                string[] names = { "close", "medium", "rts" };
                for (int i = 0; i < 3; i++)
                {
                    viewer.SetView(i);
                    CaptureSampledPose(viewer, i == 0 ? 1500 : 2560, i == 0 ? 1900 : 1440, Review + "/" + names[i] + ".png");
                }
                viewer.SetView(1);
                foreach (string state in States)
                {
                    var clip = Clip(state); Sample(viewer, clip, clip.length * (state == "Death" ? .98f : .45f));
                    CaptureSampledPose(viewer, 1500, 1500, Review + "/animation-" + state.ToLowerInvariant() + ".png");
                }
            }
            finally { AnimationMode.StopAnimationMode(); lod.ForceLOD(-1); }
            EditorSceneManager.OpenScene(ScenePath);
        }

        internal static void CaptureSampledPose(CrimsonCorsairReview viewer, int width, int height, string path)
        {
            // AnimationMode updates the bones synchronously, but an immediate SRP
            // render request can still read the previous GPU skinning buffer. Bake
            // that exact sampled pose into disposable geometry for this editor shot.
            // Runtime captures continue to use the real animated SkinnedMeshRenderer.
            var skins = viewer.Character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var previousEnabled = skins.Select(s => s.enabled).ToArray();
            var group = viewer.Character.GetComponent<LODGroup>();
            bool previousGroupEnabled = group && group.enabled;
            var snapshots = new List<GameObject>();
            var meshes = new List<Mesh>();
            try
            {
                if (group) group.enabled = false;
                foreach (var skin in skins)
                {
                    skin.enabled = false;
                    // These art inspection images use LOD0 at all three camera
                    // distances. The actual game and its smoke retain automatic LOD.
                    if (skin.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var mesh = new Mesh { name = "Capture pose " + skin.name, hideFlags = HideFlags.HideAndDontSave };
                    meshes.Add(mesh);
                    skin.BakeMesh(mesh, false);
                    mesh.RecalculateBounds(); mesh.UploadMeshData(false);
                    var snapshot = new GameObject("Capture pose " + skin.name, typeof(MeshFilter), typeof(MeshRenderer));
                    snapshots.Add(snapshot); snapshot.hideFlags = HideFlags.HideAndDontSave;
                    // BakeMesh(false) already returns metre-scale vertices for this
                    // imported rig. Its renderer's FBX conversion scale is 100, so
                    // inheriting that transform would apply unit conversion twice.
                    // Preserve the renderer's position/orientation in an unscaled frame.
                    snapshot.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    snapshot.transform.localScale = Vector3.one;
                    snapshot.layer = skin.gameObject.layer;
                    snapshot.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = snapshot.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.shadowCastingMode = skin.shadowCastingMode;
                    renderer.receiveShadows = skin.receiveShadows;
                    renderer.lightProbeUsage = skin.lightProbeUsage;
                    renderer.reflectionProbeUsage = skin.reflectionProbeUsage;
                    renderer.renderingLayerMask = skin.renderingLayerMask;
                    Debug.Log($"CORSAIR_CAPTURE_BOUNDS {Path.GetFileName(path)} skin={skin.name} scale={skin.transform.lossyScale:F5} rootScale={(skin.rootBone ? skin.rootBone.lossyScale.ToString("F5") : "none")} baked={mesh.bounds} skinWorld={skin.bounds} snapshotWorld={renderer.bounds} rotation={skin.transform.eulerAngles:F3}");
                }
                if (snapshots.Count == 0) throw new InvalidOperationException("No LOD0 skin was available for the editor pose capture.");
                CrimsonCorsairReview.CaptureCamera(viewer.Active, width, height, path);
            }
            finally
            {
                foreach (var snapshot in snapshots) if (snapshot) Object.DestroyImmediate(snapshot);
                foreach (var mesh in meshes) if (mesh) Object.DestroyImmediate(mesh);
                for (int i = 0; i < skins.Length; i++) if (skins[i]) skins[i].enabled = previousEnabled[i];
                if (group) group.enabled = previousGroupEnabled;
            }
        }
    }
}
