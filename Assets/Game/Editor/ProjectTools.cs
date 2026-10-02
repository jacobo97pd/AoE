using System;
using System.IO;
using System.Linq;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Editor
{
    public static class ProjectTools
    {
        public const string MainScene = "Assets/Game/Scenes/Greybox.unity";
        public const string InputScene = "Assets/Game/Scenes/MobileInputTest.unity";
        public const string CombatScene = "Assets/Game/Scenes/CombatSandbox.unity";
        public const string StressScene = "Assets/Game/Scenes/RTSPerformanceStress.unity";
        private static string[] PlayerScenes => new[] { MainScene, InputScene, CombatScene, StressScene, "Assets/Game/Scenes/CharacterCollection.unity" }.Where(File.Exists).ToArray();

        [MenuItem("Emberfield/Verify project and prepare scenes")]
        public static void Verify()
        {
            var world = DefinitionLoader.CreateWorld();
            if (world.Units.Count == 0) throw new InvalidOperationException("Map has no playable units.");
            Configure();
            AlphaAssetImport.Prepare();
            EnsureScene(MainScene);
            EnsureScene(InputScene);
            DefinitionLoader.CreateWorld("Maps/combat_sandbox");
            EnsureScene(CombatScene, "Maps/combat_sandbox");
            EnsureScene(StressScene, performanceStress: true);
            EditorBuildSettings.scenes = PlayerScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"EMBERFIELD_VERIFY_OK Unity={Application.unityVersion} units={world.Units.Count} buildings={world.Buildings.Count} resources={world.Resources.Count}");
        }

        private static void Configure()
        {
            PlayerSettings.companyName = "Emberfield Project";
            PlayerSettings.productName = "Emberfield";
            PlayerSettings.bundleVersion = "0.3.2";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.emberfield.jpedrero");
            PlayerSettings.Android.bundleVersionCode = 3;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.emberfield.jpedrero");
            PlayerSettings.iOS.buildNumber = "3";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            // The mobile renderer bundled with this exact editor is shared across prototype quality levels.
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (pipeline == null) throw new InvalidOperationException("Mobile URP asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            const string materialPath = "Assets/Game/Resources/Materials/Greybox.mat";
            if (!File.Exists(materialPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
                AssetDatabase.Refresh();
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
                AssetDatabase.CreateAsset(new Material(shader) { enableInstancing = true }, materialPath);
            }
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; QualitySettings.vSyncCount = 0; }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static void EnsureScene(string path, string mapResourcePath = "Maps/amber_reach", bool performanceStress = false)
        {
            if (File.Exists(path)) return; // Never overwrite authored scene work.
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var match = new GameObject("Emberfield match").AddComponent<MatchController>();
            match.MapResourcePath = mapResourcePath;
            match.PerformanceStress = performanceStress;
            EditorSceneManager.SaveScene(scene, path);
        }

        [MenuItem("Emberfield/Build Windows development player")]
        public static void BuildWindows()
        {
            Verify();
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Emberfield.exe");
        }

        [MenuItem("Emberfield/Build Android development APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Unity Android Build Support is absent. Add it to the editor version pinned in ProjectSettings/ProjectVersion.txt with its matching SDK/NDK/JDK before running this target.");
            Verify();
            Build(BuildTarget.Android, "Builds/Android/Emberfield.apk");
        }

        private static void Build(BuildTarget target, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = PlayerScenes, target = target, locationPathName = destination, options = BuildOptions.Development });
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/build-summary-" + target + ".txt", $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nOutput: {report.summary.outputPath}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException(report.summary.result.ToString());
            Debug.Log("EMBERFIELD_BUILD_OK " + destination);
        }
    }
}
