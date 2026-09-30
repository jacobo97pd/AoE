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
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>Builds the three supplied crew assets and reuses the captain's proven presentation scene.</summary>
    public static class PirateCrewBaker
    {
        private const string Review = "Artifacts/ArtReview/pirate-crew";
        private const string ScenePath = "Assets/Game/Scenes/PirateCrew.unity";
        private const string CaptainScene = "Assets/Game/Scenes/CrimsonCorsair.unity";
        private const string PipelinePath = "Assets/Game/CrimsonCorsair/Rendering/CorsairPipeline.asset";
        private static readonly string[] Names = { "BoardingRaider", "GunpowderCorsair", "TreasureSeeker", "CrimsonCorsair" };
        private static readonly string[] Ids = { "boarding_raider", "gunpowder_corsair", "treasure_seeker", "crimson_corsair" };
        private static readonly string[] Titles = { "Saqueador de abordaje", "Corsario de pólvora", "Buscadora de tesoros", "Corsario Carmesí" };
        private static readonly string[] Roles = { "Infantería de abordaje", "Tirador de pólvora", "Exploración y recursos", "Capitán · Héroe pirata" };

        [Serializable] private sealed class MeshEvidence
        {
            public string name, asset;
            public int vertices, triangles, bones;
            public bool uv, normals, weights;
        }
        [Serializable] private sealed class ClipEvidence
        {
            public string state, name;
            public float seconds, displacement;
            public bool loop;
        }
        [Serializable] private sealed class CharacterEvidence
        {
            public string id, name;
            public int lods;
            public bool passed, muzzle;
            public List<MeshEvidence> meshes = new List<MeshEvidence>();
            public List<ClipEvidence> clips = new List<ClipEvidence>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Evidence
        {
            public string generatedUtc, unity, scene = ScenePath;
            public bool passed;
            public int characters, animationStates;
            public List<CharacterEvidence> roster = new List<CharacterEvidence>();
            public string scope = "Persistent skinned assets, shared source atlas, three LODs per character and actual sampled deformation. Editor captures use temporary CPU-baked LOD0 poses; the native player uses the real animated skins.";
        }

        public static void Run()
        {
            Execute(false);
        }
        public static void CaptureOnly()
        {
            Execute(true);
        }

        private static void Execute(bool captureOnly)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use tools/Build-PirateCrew.ps1.");
            // Remember asset paths, not object references: the build reloads assets, and a stale
            // reference restores "no pipeline" and saves it into the project settings.
            string previousQuality = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline);
            string previousDefault = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            int previousAA = QualitySettings.antiAliasing, exit = 0;
            try
            {
                Directory.CreateDirectory(Review); AssetDatabase.Refresh();
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
                if (!pipeline) throw new InvalidOperationException("The validated captain presentation pipeline is missing.");
                QualitySettings.renderPipeline = pipeline; GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.antiAliasing = 4;
                var args = Environment.GetCommandLineArgs();
                bool playerOnly = args.Contains("-pirateCrewPlayerOnly"), sceneOnly = args.Contains("-pirateCrewSceneOnly");
                if (!captureOnly && !playerOnly)
                {
                    if (!sceneOnly)
                    {
                        var material = PirateCharacterAssetImport.BuildSharedMaterial();
                        foreach (string name in Names.Take(3))
                        {
                            PirateCharacterAssetImport.ImportModel(name);
                            var controller = PirateCharacterAssetImport.BuildController(name);
                            PirateCharacterAssetImport.BuildPrefab(name, material, controller);
                        }
                    }
                    BuildScene(); AssetDatabase.SaveAssets();
                }
                EditorSceneManager.OpenScene(ScenePath);
                var viewer = Object.FindFirstObjectByType<CrimsonCorsairReview>();
                Validate(viewer);
                if (!playerOnly) Capture(viewer);
                if (!captureOnly && args.Contains("-pirateCrewPlayer")) BuildPlayer();
                Debug.Log(captureOnly ? "PIRATE_CREW_CAPTURE_OK" : "PIRATE_CREW_OK");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally
            {
                QualitySettings.renderPipeline = string.IsNullOrEmpty(previousQuality) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousQuality);
                GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(previousDefault) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousDefault);
                QualitySettings.antiAliasing = previousAA;
                if (!captureOnly) AssetDatabase.SaveAssets();
            }
            if (exit != 0) EditorApplication.Exit(exit);
        }

        private static void BuildScene()
        {
            // Reuse the saved courtyard, lights, cameras, grade and pipeline exactly.
            // SaveAs below writes a separate scene; the captain scene is never saved.
            var scene = EditorSceneManager.OpenScene(CaptainScene);
            var viewer = Object.FindFirstObjectByType<CrimsonCorsairReview>();
            if (!viewer || !viewer.Character) throw new InvalidOperationException("The captain review scene is incomplete.");
            Vector3 position = viewer.Character.transform.position;
            Quaternion rotation = viewer.Character.transform.rotation;
            Object.DestroyImmediate(viewer.Character);
            var roster = new List<CrimsonCorsairReview.CharacterEntry>();
            for (int i = 0; i < Names.Length; i++)
            {
                string prefabPath = i == 3 ? "Assets/Game/CrimsonCorsair/Resources/ImportedUnits/CrimsonCorsair.prefab" : PirateCharacterAssetImport.PrefabPath(Names[i]);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!source) throw new FileNotFoundException("Missing crew review prefab.", prefabPath);
                var character = (GameObject)PrefabUtility.InstantiatePrefab(source);
                character.name = Titles[i]; character.transform.SetPositionAndRotation(position, rotation);
                roster.Add(new CrimsonCorsairReview.CharacterEntry { Id = Ids[i], DisplayName = Titles[i], Role = Roles[i], Character = character });
            }
            viewer.name = "Revisión de la tripulación pirata";
            viewer.Roster = roster.ToArray(); viewer.CrewReview = true; viewer.Controls = true;
            viewer.SelectCharacter(0); viewer.SetView(0);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static AnimationClip Clip(CrimsonCorsairReview viewer, string state)
        {
            return viewer.Animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c && c.name == "Corsair_" + state)
                ?? throw new InvalidOperationException("The selected crew member is missing " + state);
        }

        private static void Validate(CrimsonCorsairReview viewer)
        {
            if (!viewer || viewer.Roster == null || viewer.Roster.Length != 4) throw new InvalidOperationException("The crew scene needs all four characters.");
            var evidence = new Evidence { generatedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, passed = true };
            var sharedCrewMaterial = AssetDatabase.LoadAssetAtPath<Material>(PirateCharacterAssetImport.Root + "/Materials/PirateCrew.mat");
            for (int i = 0; i < viewer.Roster.Length; i++)
            {
                viewer.SelectCharacter(i);
                var record = new CharacterEvidence { id = Ids[i], name = Titles[i], passed = true };
                var skins = viewer.Character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skin in skins)
                {
                    var mesh = skin.sharedMesh;
                    var info = new MeshEvidence { name = skin.name, asset = AssetDatabase.GetAssetPath(mesh), vertices = mesh.vertexCount, triangles = mesh.triangles.Length / 3, bones = skin.bones.Length, uv = mesh.uv.Length == mesh.vertexCount, normals = mesh.normals.Length == mesh.vertexCount, weights = mesh.boneWeights.Length == mesh.vertexCount };
                    record.meshes.Add(info);
                    if (string.IsNullOrEmpty(info.asset) || !info.uv || !info.normals || !info.weights || info.bones < 10) record.errors.Add("Incomplete skin " + skin.name);
                }
                var group = viewer.Character.GetComponent<LODGroup>(); record.lods = group ? group.lodCount : 0;
                if (skins.Length != 3 || record.lods != 3) record.errors.Add("Each character needs three skinned LODs.");
                if (!viewer.Animator || !viewer.Animator.avatar || !viewer.Animator.avatar.isValid || viewer.Animator.applyRootMotion) record.errors.Add("Invalid Generic Animator configuration.");
                record.muzzle = viewer.Character.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Muzzle");
                if (Names[i] == "GunpowderCorsair" && !record.muzzle) record.errors.Add("Missing authored muzzle.");
                foreach (var material in skins.SelectMany(s => s.sharedMaterials).Distinct())
                {
                    if (i < 3)
                    {
                        var ownMaterial = AssetDatabase.LoadAssetAtPath<Material>(PirateCharacterAssetImport.CharacterRoot(Names[i]) + "/Materials/MeshyRaider.mat");
                        var expectedMaterial = Names[i] == "BoardingRaider" && ownMaterial ? ownMaterial : sharedCrewMaterial;
                        if (material != expectedMaterial) record.errors.Add("Crew member must use its supplied source-atlas material.");
                    }
                    if (!material || !material.shader || !material.shader.isSupported || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material))) record.errors.Add("Invalid persistent character material.");
                    else foreach (var message in ShaderUtil.GetShaderMessages(material.shader)) if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) record.errors.Add(message.message);
                }
                var detailed = skins.FirstOrDefault(s => s.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0);
                AnimationMode.StartAnimationMode();
                try
                {
                    foreach (string state in PirateCharacterAssetImport.States(Names[i]))
                    {
                        var clip = Clip(viewer, state); float movement = 0;
                        if (detailed)
                        {
                            var baked = new Mesh();
                            try
                            {
                                Sample(viewer, clip, 0); detailed.BakeMesh(baked); var before = baked.vertices;
                                Sample(viewer, clip, clip.length * .45f); detailed.BakeMesh(baked); var after = baked.vertices;
                                for (int v = 0; v < before.Length; v += Math.Max(1, before.Length / 1000)) movement = Mathf.Max(movement, Vector3.Distance(before[v], after[v]));
                            }
                            finally { Object.DestroyImmediate(baked); }
                        }
                        record.clips.Add(new ClipEvidence { state = state, name = clip.name, seconds = clip.length, displacement = movement, loop = clip.isLooping });
                        if (clip.length <= .05f || movement <= .0001f) record.errors.Add("No sampled deformation in " + state);
                    }
                }
                finally { AnimationMode.StopAnimationMode(); }
                record.passed = record.errors.Count == 0; evidence.passed &= record.passed;
                evidence.characters++; evidence.animationStates += record.clips.Count; evidence.roster.Add(record);
            }
            viewer.SelectCharacter(0);
            File.WriteAllText(Review + "/asset-validation.json", JsonUtility.ToJson(evidence, true));
            if (!evidence.passed) throw new InvalidOperationException(string.Join("; ", evidence.roster.SelectMany(r => r.errors.Select(error => r.id + ": " + error))));
        }

        private static void Sample(CrimsonCorsairReview viewer, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(viewer.Animator.gameObject, clip, time); AnimationMode.EndSampling();
        }

        private static void Capture(CrimsonCorsairReview viewer)
        {
            viewer.Controls = false;
            for (int i = 0; i < viewer.Roster.Length; i++)
            {
                viewer.SelectCharacter(i);
                string folder = Review + "/" + Ids[i]; Directory.CreateDirectory(folder);
                AnimationMode.StartAnimationMode();
                try
                {
                    Sample(viewer, Clip(viewer, "Idle"), .2f);
                    string[] cameras = { "close", "medium", "rts" };
                    for (int c = 0; c < cameras.Length; c++)
                    {
                        viewer.SetView(c);
                        CrimsonCorsairBaker.CaptureSampledPose(viewer, c == 0 ? 1500 : 2560, c == 0 ? 1900 : 1440, folder + "/" + cameras[c] + ".png");
                    }
                    viewer.SetView(1);
                    foreach (string state in PirateCharacterAssetImport.States(Names[i]))
                    {
                        var clip = Clip(viewer, state); Sample(viewer, clip, clip.length * (state == "Death" ? .98f : .45f));
                        CrimsonCorsairBaker.CaptureSampledPose(viewer, 1500, 1500, folder + "/animation-" + state.ToLowerInvariant() + ".png");
                    }
                }
                finally { AnimationMode.StopAnimationMode(); }
            }
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void BuildPlayer()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, "Assets/Game/Scenes/Greybox.unity", CaptainScene },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/PirateCrew/PirateCrew.exe",
                options = BuildOptions.Development
            });
            File.WriteAllText(Review + "/build.txt", $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            File.AppendAllLines(Review + "/build.txt", report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Warning || m.type == LogType.Error).Select(m => m.type + ": " + m.content));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("The crew player build failed.");
        }
    }
}
