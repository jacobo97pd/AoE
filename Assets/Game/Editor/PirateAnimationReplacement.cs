using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Emberfield.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>Targeted pirate animation imports and the compatible Meshy biped replacement for BoardingRaider.</summary>
    public static class PirateAnimationReplacement
    {
        private const string ReviewRoot = "Artifacts/ArtReview/pirate-animation-replacement";
        private const string ReviewScene = "Assets/Game/Scenes/PirateCrew.unity";
        private static readonly string[] Characters = { "BoardingRaider", "GunpowderCorsair", "TreasureSeeker", "CrimsonCorsair" };
        private static readonly string[] ReplacedStates = { "Run", "Death" };

        private static string CharacterRoot(string name) => name == "CrimsonCorsair" ? "Assets/Game/CrimsonCorsair" : "Assets/Game/PirateCrew/" + name;
        private static string ControllerPath(string name) => CharacterRoot(name) + "/Animations/" + name + ".controller";
        private static string PrefabPath(string name) => (name == "CrimsonCorsair" ? CharacterRoot(name) : "Assets/Game/PirateCrew") + "/Resources/ImportedUnits/" + name + ".prefab";
        private static string OverridePath(string name, string state) => CharacterRoot(name) + "/AnimationOverrides/" + state + ".anim";
        private static string DefinitionId(string name) => name == "BoardingRaider" ? "boarding_raider" : name == "GunpowderCorsair" ? "gunpowder_corsair" : name == "TreasureSeeker" ? "treasure_seeker" : "crimson_corsair";

        // Called by the existing bakers before their original FBX clip fallback.
        // No import or creation side effects: only persistent, permitted overrides.
        internal static AnimationClip ResolveOverride(string characterName, string state)
        {
            if (!Characters.Contains(characterName) || !ReplacedStates.Contains(state)) return null;
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(OverridePath(characterName, state));
        }

        [Serializable] private sealed class MeshEvidence
        {
            public string name;
            public int vertices;
            public float maxVertexDisplacement;
        }
        [Serializable] private sealed class ClipEvidence
        {
            public string state, sourceFbx, sourceSha256, persistentClip;
            public float seconds, frameRate;
            public bool loop, bindingsResolve, skeletalRotationChanges, deforms;
            public int curveBindings, animatedTransformPaths;
            public List<string> bindingPaths = new List<string>();
            public List<string> missingBindings = new List<string>();
            public List<MeshEvidence> meshes = new List<MeshEvidence>();
        }
        [Serializable] private sealed class MotionEvidence
        {
            public string statePath, before, after;
            public bool replacement, unchanged;
        }
        [Serializable] private sealed class Evidence
        {
            public string target, definitionId, generatedUtc, unity, controller, prefab;
            public string model, material, sourceModelSha256, modelGuidBefore, modelGuidAfter, prefabGuidBefore, prefabGuidAfter, controllerGuidBefore, controllerGuidAfter;
            public bool passed, applied, rolledBack, protectedAssetsUnchanged, otherMotionsUnchanged;
            public bool modelGuidPreserved, prefabGuidPreserved, controllerStateIdentitiesPreserved, compatibleGenericAvatar, ownMaterialOnEveryLod, validClipLoopSettings;
            public int protectedFiles;
            public string scope = "One existing pirate controller: only Run and Death motions are replaced by retargeted Generic clips. Original models, prefabs, materials, scenes and all other pirate controllers are protected by file hashes. Editor screenshots use the current review scene and temporary CPU-baked poses; they are not native-player captures.";
            public List<ClipEvidence> clips = new List<ClipEvidence>();
            public List<MotionEvidence> motions = new List<MotionEvidence>();
            public List<string> screenshots = new List<string>();
            public List<string> changedProtectedFiles = new List<string>();
            public List<string> errors = new List<string>();
            public List<FileEvidence> protectedAssets = new List<FileEvidence>();
            public List<RendererEvidence> renderers = new List<RendererEvidence>();
        }
        [Serializable] private sealed class FileEvidence
        {
            public string path, beforeSha256, afterSha256;
        }
        [Serializable] private sealed class RendererEvidence
        {
            public string name;
            public int vertices, triangles, bones;
            public Vector3 localScale, worldScale, boundsSize;
        }
        private sealed class StateRecord
        {
            public string Path, Identity;
            public AnimatorState State;
            public Motion Motion;
        }
        private sealed class SavedClip
        {
            public string Path;
            public AnimationClip Asset, Backup;
            public bool Existed;
        }

        /// <summary>
        /// Import the replacement mesh already exported over the original BoardingRaider
        /// FBX path. Keep controller state objects and all other character assets intact.
        /// The source/export pipeline owns backups and the new rig's six compatible takes.
        /// </summary>
        public static void ApplyMeshyRaiderAndValidate()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use batch -executeMethod Emberfield.Editor.PirateAnimationReplacement.ApplyMeshyRaiderAndValidate.");
            const string target = "BoardingRaider";
            string folder = ReviewRoot + "/" + target;
            Directory.CreateDirectory(folder);
            var evidence = new Evidence
            {
                target = target, definitionId = DefinitionId(target), generatedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                controller = ControllerPath(target), prefab = PrefabPath(target), model = PirateCharacterAssetImport.ModelPath(target),
                material = CharacterRoot(target) + "/Materials/MeshyRaider.mat",
                scope = "Only BoardingRaider is replaced with the supplied Meshy biped mesh, Generic rig, six compatible motions and its own PBR atlas. Existing controller state objects, model GUID and prefab GUID are preserved. Other pirates, shared atlases, saved scenes and project render settings are compared by SHA-256. Screenshots are editor renders of CPU-baked poses; native-player and gameplay validation run separately. No claim of retaining the old raider mesh or retargeting raw Run/Death onto that mesh."
            };
            Dictionary<string, string> protectedBefore = null;
            try
            {
                var args = Environment.GetCommandLineArgs(); int targetOption = Array.IndexOf(args, "-pirateTarget");
                Require(targetOption < 0 || targetOption + 1 < args.Length && args[targetOption + 1] == target, "This mesh replacement is restricted to BoardingRaider.");
                Require(File.Exists(evidence.model) && File.Exists(evidence.model + ".meta"), "The replacement must overwrite the original FBX while retaining its .meta.");
                Require(File.Exists(CharacterRoot(target) + "/model-manifest.json"), "The replacement rig's locomotion manifest is required.");
                foreach (string map in new[] { "BaseColor", "Normal", "Metallic", "Roughness" })
                    Require(File.Exists(CharacterRoot(target) + "/Textures/" + map + ".png"), "Missing replacement texture: " + map);

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(evidence.controller);
                Require(controller && AssetDatabase.LoadAssetAtPath<GameObject>(evidence.prefab), "The original raider controller and prefab must exist.");
                var before = Snapshot(controller);
                var stateIdentities = before.ToDictionary(s => s.Path, s => Identity(s.State));
                var defaults = controller.layers.Select(layer => Identity(layer.stateMachine.defaultState)).ToArray();
                foreach (string state in PirateCharacterAssetImport.BaseStates)
                    Require(before.Count(s => s.State.name == state) == 1, "Expected one existing state named " + state);
                evidence.modelGuidBefore = AssetDatabase.AssetPathToGUID(evidence.model);
                evidence.prefabGuidBefore = AssetDatabase.AssetPathToGUID(evidence.prefab);
                evidence.controllerGuidBefore = AssetDatabase.AssetPathToGUID(evidence.controller);
                Require(!string.IsNullOrEmpty(evidence.modelGuidBefore) && !string.IsNullOrEmpty(evidence.prefabGuidBefore), "The original imported asset GUIDs are missing.");
                evidence.sourceModelSha256 = Hash(evidence.model);
                protectedBefore = MeshyProtectedHashes(); evidence.protectedFiles = protectedBefore.Count;

                PirateCharacterAssetImport.ImportModel(target);
                var clips = AssetDatabase.LoadAllAssetsAtPath(evidence.model).OfType<AnimationClip>()
                    .Where(c => PirateCharacterAssetImport.BaseStates.Any(state => c.name == "Corsair_" + state))
                    .ToDictionary(c => c.name.Substring("Corsair_".Length), c => c);
                Require(clips.Count == PirateCharacterAssetImport.BaseStates.Length, "The replacement must contain all six compatible Corsair motions.");
                foreach (string state in PirateCharacterAssetImport.BaseStates)
                {
                    var clip = clips[state];
                    Require(!clip.humanMotion && clip.length > .05f, "The replacement motion must be a nonempty Generic clip: " + state);
                    bool loop = state == "Idle" || state == "Walk" || state == "Run";
                    Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == loop, "Incorrect loop setting for " + state);
                    var node = before.Single(s => s.State.name == state).State;
                    node.motion = clip; EditorUtility.SetDirty(node);
                }
                evidence.validClipLoopSettings = true;
                EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
                var material = PirateCharacterAssetImport.BuildMeshyRaiderMaterial();
                AssetDatabase.SaveAssetIfDirty(material);
                var prefab = PirateCharacterAssetImport.BuildPrefab(target, material, controller);
                evidence.applied = true;

                var animator = prefab.GetComponentInChildren<Animator>(true);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(evidence.model).OfType<Avatar>().SingleOrDefault();
                evidence.compatibleGenericAvatar = animator && avatar && avatar.isValid && !avatar.isHuman && animator.avatar == avatar &&
                    !animator.applyRootMotion && animator.runtimeAnimatorController == controller;
                Require(evidence.compatibleGenericAvatar, "The raider prefab must use the new model's valid Generic avatar and the existing controller, without root motion.");
                var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                var lods = prefab.GetComponent<LODGroup>();
                Require(skins.Length == 3 && lods && lods.lodCount == 3, "The replacement must retain exactly three skinned LODs.");
                evidence.ownMaterialOnEveryLod = skins.All(s => s.sharedMaterials.Length > 0 && s.sharedMaterials.All(m => m == material));
                Require(evidence.ownMaterialOnEveryLod && AssetDatabase.GetAssetPath(material) == evidence.material, "Every replacement LOD must use only its own MeshyRaider material.");
                Require(material.shader.name == "Universal Render Pipeline/Lit" && material.GetColor("_BaseColor") == Color.white, "The replacement must preserve its supplied colors through URP Lit.");
                Require(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) == CharacterRoot(target) + "/Textures/BaseColor.png" &&
                    AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) == CharacterRoot(target) + "/Textures/Normal.png" &&
                    AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")) == CharacterRoot(target) + "/Textures/MetallicSmoothness.png", "The replacement material references an unrelated atlas.");
                foreach (var skin in skins)
                {
                    Require(skin.sharedMesh && skin.bones.Length > 0 && skin.bones.All(b => b), "The new skinned mesh has missing bone bindings: " + skin.name);
                    evidence.renderers.Add(new RendererEvidence { name = skin.name, vertices = skin.sharedMesh.vertexCount,
                        triangles = skin.sharedMesh.triangles.Length / 3, bones = skin.bones.Length, localScale = skin.transform.localScale,
                        worldScale = skin.transform.lossyScale, boundsSize = skin.bounds.size });
                }
                foreach (string state in PirateCharacterAssetImport.BaseStates)
                {
                    var clipEvidence = new ClipEvidence { state = state, sourceFbx = evidence.model, sourceSha256 = evidence.sourceModelSha256, persistentClip = Identity(clips[state]) };
                    evidence.clips.Add(clipEvidence); ValidateClip(prefab, clips[state], clipEvidence);
                }

                var after = Snapshot(controller);
                evidence.controllerStateIdentitiesPreserved = before.Count == after.Count && after.All(s => stateIdentities.TryGetValue(s.Path, out string id) && id == Identity(s.State)) &&
                    defaults.SequenceEqual(controller.layers.Select(layer => Identity(layer.stateMachine.defaultState)));
                Require(evidence.controllerStateIdentitiesPreserved, "A controller state identity, state count or default state changed.");
                foreach (var original in before)
                {
                    var current = after.Single(s => s.Path == original.Path);
                    bool replacement = PirateCharacterAssetImport.BaseStates.Contains(original.State.name);
                    var record = new MotionEvidence { statePath = original.Path, before = original.Identity, after = current.Identity, replacement = replacement, unchanged = original.Identity == current.Identity };
                    evidence.motions.Add(record);
                    Require(replacement ? current.Motion == clips[original.State.name] : record.unchanged, "Unexpected motion reference at " + original.Path);
                }
                evidence.otherMotionsUnchanged = evidence.motions.Where(m => !m.replacement).All(m => m.unchanged);
                evidence.modelGuidAfter = AssetDatabase.AssetPathToGUID(evidence.model);
                evidence.prefabGuidAfter = AssetDatabase.AssetPathToGUID(evidence.prefab);
                evidence.controllerGuidAfter = AssetDatabase.AssetPathToGUID(evidence.controller);
                evidence.modelGuidPreserved = evidence.modelGuidBefore == evidence.modelGuidAfter;
                evidence.prefabGuidPreserved = evidence.prefabGuidBefore == evidence.prefabGuidAfter;
                Require(evidence.modelGuidPreserved && evidence.prefabGuidPreserved && evidence.controllerGuidBefore == evidence.controllerGuidAfter, "The model, prefab or controller asset GUID changed.");
                Require(Hash(evidence.model) == evidence.sourceModelSha256, "The import modified the supplied model source bytes.");
                Capture(target, clips, folder, "meshy", evidence);
                evidence.passed = true;
            }
            catch (Exception error) { evidence.errors.Add(error.ToString()); Debug.LogException(error); }
            finally
            {
                if (protectedBefore != null)
                {
                    var protectedAfter = MeshyProtectedHashes();
                    foreach (string path in protectedBefore.Keys.Union(protectedAfter.Keys).OrderBy(p => p, StringComparer.Ordinal))
                    {
                        protectedBefore.TryGetValue(path, out string beforeHash); protectedAfter.TryGetValue(path, out string afterHash);
                        evidence.protectedAssets.Add(new FileEvidence { path = path, beforeSha256 = beforeHash, afterSha256 = afterHash });
                        if (beforeHash != afterHash) evidence.changedProtectedFiles.Add(path);
                    }
                    evidence.protectedAssetsUnchanged = evidence.changedProtectedFiles.Count == 0;
                    if (!evidence.protectedAssetsUnchanged) { evidence.passed = false; evidence.errors.Add("Protected assets changed: " + string.Join(", ", evidence.changedProtectedFiles)); }
                }
                File.WriteAllText(folder + "/meshy-raider-validation.json", JsonUtility.ToJson(evidence, true));
            }
            if (!evidence.passed) EditorApplication.Exit(1);
            else Debug.Log("MESHY_RAIDER_REPLACEMENT_OK " + folder);
        }

        private static Dictionary<string, string> MeshyProtectedHashes()
        {
            const string target = "BoardingRaider";
            string permitted = CharacterRoot(target) + "/", prefab = PrefabPath(target);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in new[] { "Assets/Game/PirateCrew", "Assets/Game/CrimsonCorsair" })
                if (Directory.Exists(root)) foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (!path.StartsWith(permitted, StringComparison.OrdinalIgnoreCase) && path != prefab && path != prefab + ".meta") files.Add(path);
                }
            foreach (string path in new[] { ReviewScene, "Assets/Game/Scenes/CrimsonCorsair.unity", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
                if (File.Exists(path)) files.Add(path);
            return files.ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
        }

        public static void ApplyAndValidate()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use batch -executeMethod Emberfield.Editor.PirateAnimationReplacement.ApplyAndValidate -pirateTarget <character>.");
            var evidence = new Evidence { generatedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            string folder = ReviewRoot;
            var candidates = new Dictionary<string, AnimationClip>();
            var saved = new List<SavedClip>();
            List<StateRecord> before = null;
            AnimatorController controller = null;
            bool commit = false;
            try
            {
                var args = Environment.GetCommandLineArgs(); int option = Array.IndexOf(args, "-pirateTarget");
                Require(option >= 0 && option + 1 < args.Length && Characters.Contains(args[option + 1]), "Specify exactly one supported pirate with -pirateTarget.");
                string target = args[option + 1]; folder += "/" + target; Directory.CreateDirectory(folder);
                evidence.target = target; evidence.definitionId = DefinitionId(target);
                evidence.controller = ControllerPath(target); evidence.prefab = PrefabPath(target);
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(evidence.controller);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(evidence.prefab);
                Require(controller && prefab, "The target's existing controller and prefab must already exist.");
                var animator = prefab.GetComponentInChildren<Animator>(true);
                Require(animator && animator.avatar && animator.avatar.isValid && !animator.avatar.isHuman && !animator.applyRootMotion,
                    "The original prefab must have its valid Generic avatar and root motion disabled.");
                Require(animator.runtimeAnimatorController == controller, "The prefab does not use the expected existing controller.");
                before = Snapshot(controller);
                foreach (string state in ReplacedStates)
                {
                    var matches = before.Where(s => s.State.name == state).ToArray();
                    Require(matches.Length == 1 && matches[0].Motion is AnimationClip, "Expected one existing AnimationClip state named " + state + ".");
                    string source = CharacterRoot(target) + "/AnimationOverrides/" + state + ".fbx";
                    Require(File.Exists(source), "Missing already-retargeted animation-only FBX: " + source);
                }
                var protectedFiles = ProtectedHashes(target);
                evidence.protectedFiles = protectedFiles.Count;
                foreach (string state in ReplacedStates)
                {
                    var clipEvidence = new ClipEvidence { state = state, sourceFbx = CharacterRoot(target) + "/AnimationOverrides/" + state + ".fbx", persistentClip = OverridePath(target, state) };
                    evidence.clips.Add(clipEvidence);
                    clipEvidence.sourceSha256 = Hash(clipEvidence.sourceFbx);
                    var clip = ImportClip(clipEvidence.sourceFbx, state, animator.avatar);
                    candidates.Add(state, clip);
                    ValidateClip(prefab, clip, clipEvidence);
                }
                Capture(target, before.Where(s => ReplacedStates.Contains(s.State.name)).ToDictionary(s => s.State.name, s => (AnimationClip)s.Motion), folder, "before", evidence);

                foreach (string state in ReplacedStates)
                {
                    string path = OverridePath(target, state);
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    var record = new SavedClip { Path = path, Asset = existing, Existed = existing != null, Backup = existing ? Object.Instantiate(existing) : null };
                    saved.Add(record);
                    if (existing)
                    {
                        EditorUtility.CopySerialized(candidates[state], existing);
                        EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing);
                    }
                    else
                    {
                        record.Asset = Object.Instantiate(candidates[state]); record.Asset.name = "Corsair_" + state;
                        AssetDatabase.CreateAsset(record.Asset, path);
                    }
                    var node = before.Single(s => s.State.name == state).State;
                    node.motion = record.Asset; EditorUtility.SetDirty(node);
                }
                foreach (var record in before.Where(s => ReplacedStates.Contains(s.State.name))) AssetDatabase.SaveAssetIfDirty(record.State);
                EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
                evidence.applied = true;

                var after = Snapshot(controller);
                Require(before.Count == after.Count, "Changing motions unexpectedly altered the controller state count.");
                foreach (var original in before)
                {
                    var current = after.Single(s => s.Path == original.Path);
                    bool replacement = ReplacedStates.Contains(original.State.name);
                    var motion = new MotionEvidence { statePath = original.Path, before = original.Identity, after = current.Identity, replacement = replacement, unchanged = original.Identity == current.Identity };
                    evidence.motions.Add(motion);
                    if (replacement) Require(current.Motion == ResolveOverride(target, original.State.name), "The replacement was not assigned to " + original.Path);
                    else Require(motion.unchanged, "An unrelated state motion changed: " + original.Path);
                }
                evidence.otherMotionsUnchanged = evidence.motions.Where(m => !m.replacement).All(m => m.unchanged);
                foreach (string state in ReplacedStates)
                {
                    var stored = ResolveOverride(target, state);
                    Require(stored && AnimationUtility.GetAnimationClipSettings(stored).loopTime == (state == "Run"), "The persistent clip has incorrect loop settings: " + state);
                    Require(AnimationUtility.GetCurveBindings(stored).Length == AnimationUtility.GetCurveBindings(candidates[state]).Length,
                        "Serializing the override changed its animation bindings: " + state);
                }
                Capture(target, ReplacedStates.ToDictionary(state => state, state => ResolveOverride(target, state)), folder, "after", evidence);
                evidence.changedProtectedFiles = protectedFiles.Where(pair => !File.Exists(pair.Key) || Hash(pair.Key) != pair.Value).Select(pair => pair.Key).ToList();
                evidence.protectedAssetsUnchanged = evidence.changedProtectedFiles.Count == 0;
                Require(evidence.protectedAssetsUnchanged, "Protected assets were modified: " + string.Join(", ", evidence.changedProtectedFiles));
                evidence.passed = true; commit = true;
                Debug.Log("PIRATE_ANIMATION_REPLACEMENT_OK " + target);
            }
            catch (Exception error) { evidence.errors.Add(error.ToString()); Debug.LogException(error); }
            finally
            {
                if (!commit && saved.Count > 0)
                {
                    try
                    {
                        foreach (var record in saved)
                            if (record.Existed && record.Asset && record.Backup)
                            { EditorUtility.CopySerialized(record.Backup, record.Asset); EditorUtility.SetDirty(record.Asset); AssetDatabase.SaveAssetIfDirty(record.Asset); }
                        if (before != null)
                            foreach (var original in before.Where(s => ReplacedStates.Contains(s.State.name)))
                            { original.State.motion = original.Motion; EditorUtility.SetDirty(original.State); AssetDatabase.SaveAssetIfDirty(original.State); }
                        foreach (var record in saved.Where(s => !s.Existed)) AssetDatabase.DeleteAsset(record.Path);
                        evidence.applied = false; evidence.rolledBack = true;
                    }
                    catch (Exception rollback) { evidence.errors.Add("Rollback: " + rollback); }
                }
                foreach (var candidate in candidates.Values) if (candidate) Object.DestroyImmediate(candidate);
                foreach (var record in saved) if (record.Backup) Object.DestroyImmediate(record.Backup);
                Directory.CreateDirectory(folder); File.WriteAllText(folder + "/replacement-validation.json", JsonUtility.ToJson(evidence, true));
            }
            if (!commit) EditorApplication.Exit(1);
        }

        private static AnimationClip ImportClip(string path, string state, Avatar avatar)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer != null, "The override source is not a model asset: " + path);
            importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; importer.sourceAvatar = avatar;
            importer.optimizeGameObjects = false; importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false; importer.importLights = false; importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.globalScale = 1; importer.useFileScale = true; importer.SaveAndReimport();
            var takes = importer.defaultClipAnimations;
            var matches = takes.Where(t => Matches(t.name, state) || Matches(t.takeName, state)).ToArray();
            // Each delivered FBX is explicitly one requested motion. A single unnamed
            // take is safe; several ambiguous takes require correcting the export.
            var take = matches.Length == 1 ? matches[0] : takes.Length == 1 ? takes[0] : null;
            Require(take != null, "Cannot identify one " + state + " take in " + path + ": " + string.Join(", ", takes.Select(t => t.name)));
            take.name = "Corsair_" + state; take.loopTime = state == "Run"; take.loopPose = take.loopTime;
            take.lockRootRotation = true; take.lockRootHeightY = true; take.lockRootPositionXZ = true;
            take.keepOriginalOrientation = true; take.keepOriginalPositionY = true; take.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { take }; importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAllAssetsAtPath(path);
            Require(!imported.OfType<Mesh>().Any() && !imported.OfType<Material>().Any(), "Animation-only FBXs must not contain meshes or materials: " + path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(model && model.GetComponentsInChildren<Renderer>(true).Length == 0 && model.GetComponentsInChildren<Camera>(true).Length == 0 && model.GetComponentsInChildren<Light>(true).Length == 0,
                "The override import must contain only its compatible skeleton and animation.");
            var source = imported.OfType<AnimationClip>().SingleOrDefault(c => c.name == "Corsair_" + state);
            Require(source && !source.humanMotion && source.length > .05f, "The requested Generic clip is missing or empty: " + state);
            var clip = Object.Instantiate(source); clip.name = "Corsair_" + state; clip.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = state == "Run"; settings.loopBlend = settings.loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings); AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            return clip;
        }

        private static bool Matches(string name, string state) => !string.IsNullOrEmpty(name) &&
            (name.Equals(state, StringComparison.OrdinalIgnoreCase) || name.EndsWith("_" + state, StringComparison.OrdinalIgnoreCase) || name.EndsWith("|" + state, StringComparison.OrdinalIgnoreCase));

        private static void ValidateClip(GameObject prefab, AnimationClip clip, ClipEvidence evidence)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            var paths = new HashSet<string>(animator.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t, animator.transform)), StringComparer.Ordinal);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            evidence.curveBindings = bindings.Length; evidence.seconds = clip.length; evidence.frameRate = clip.frameRate;
            evidence.loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
            evidence.bindingPaths = bindings.Select(b => b.path).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            evidence.animatedTransformPaths = evidence.bindingPaths.Count;
            foreach (var binding in bindings)
            {
                if (binding.type != typeof(Transform) || !paths.Contains(binding.path)) evidence.missingBindings.Add(binding.path + " :: " + binding.type.Name + "." + binding.propertyName);
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                Require(curve != null && curve.keys.All(k => !float.IsNaN(k.value) && !float.IsInfinity(k.value)), "Non-finite curve in " + evidence.state + ": " + binding.path);
                if (binding.propertyName.IndexOf("Rotation", StringComparison.OrdinalIgnoreCase) >= 0 && curve.length > 1)
                    evidence.skeletalRotationChanges |= curve.keys.Max(k => k.value) - curve.keys.Min(k => k.value) > .00001f;
            }
            Require(AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == 0, "Overrides may not animate meshes, materials or other object references.");
            evidence.bindingsResolve = evidence.missingBindings.Count == 0 && bindings.Length > 0;
            Require(evidence.bindingsResolve, "Animation paths must match the original prefab exactly. Missing: " + string.Join(", ", evidence.missingBindings.Take(12)));
            Require(evidence.skeletalRotationChanges, "The override has no changing skeletal rotations: " + evidence.state);
            Require(!AnimationMode.InAnimationMode(), "Another editor animation sampling session is active.");
            var scene = EditorSceneManager.NewPreviewScene(); GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var instanceAnimator = instance.GetComponentInChildren<Animator>(true);
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                Require(skins.Length == 3, "The existing pirate prefab must retain its three skinned LODs.");
                AnimationMode.StartAnimationMode(); Sample(instanceAnimator.gameObject, clip, 0);
                var baseline = skins.Select(Bake).ToArray();
                for (int i = 0; i < skins.Length; i++) evidence.meshes.Add(new MeshEvidence { name = skins[i].name, vertices = baseline[i].Length });
                foreach (float phase in new[] { .23f, .47f, .71f, .97f })
                {
                    Sample(instanceAnimator.gameObject, clip, clip.length * phase);
                    for (int i = 0; i < skins.Length; i++)
                    {
                        var vertices = Bake(skins[i]); Require(vertices.Length == baseline[i].Length, "Animation changed the original mesh topology.");
                        float max = 0;
                        for (int v = 0; v < vertices.Length; v++)
                        {
                            Require(Finite(vertices[v]), "Animation generated non-finite skinned vertices.");
                            max = Mathf.Max(max, (vertices[v] - baseline[i][v]).sqrMagnitude);
                        }
                        evidence.meshes[i].maxVertexDisplacement = Mathf.Max(evidence.meshes[i].maxVertexDisplacement, Mathf.Sqrt(max));
                    }
                }
                evidence.deforms = evidence.meshes.All(m => m.maxVertexDisplacement > .0001f);
                Require(evidence.deforms, "The retargeted motion did not deform every original LOD: " + evidence.state);
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                if (instance) Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Vector3[] Bake(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try { skin.BakeMesh(mesh, false); return mesh.vertices; }
            finally { Object.DestroyImmediate(mesh); }
        }
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        private static void Sample(GameObject root, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            try { AnimationMode.SampleAnimationClip(root, clip, time); }
            finally { AnimationMode.EndSampling(); }
        }

        private static void Capture(string target, Dictionary<string, AnimationClip> clips, string folder, string stage, Evidence evidence)
        {
            string previousQuality = AssetDatabase.GetAssetPath(QualitySettings.renderPipeline), previousDefault = AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);
            int previousAA = QualitySettings.antiAliasing;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(ReviewScene);
                var viewer = Object.FindFirstObjectByType<CrimsonCorsairReview>();
                Require(viewer && viewer.Roster != null, "The existing PirateCrew review scene is missing its character roster.");
                int index = Array.FindIndex(viewer.Roster, c => c.Id == DefinitionId(target));
                Require(index >= 0, "The requested character is absent from the saved review scene.");
                viewer.SelectCharacter(index); viewer.Controls = false;
                Require(viewer.Pipeline && viewer.Character, "The saved review character or render pipeline is missing.");
                QualitySettings.renderPipeline = viewer.Pipeline; GraphicsSettings.defaultRenderPipeline = viewer.Pipeline; QualitySettings.antiAliasing = 4;
                var animator = viewer.Character.GetComponentInChildren<Animator>(true);
                foreach (string state in clips.Keys)
                {
                    AnimationMode.StartAnimationMode();
                    try
                    {
                        Sample(animator.gameObject, clips[state], clips[state].length * (state == "Death" ? .98f : .45f));
                        foreach (int camera in new[] { 0, 1, 2 })
                        {
                            viewer.SetView(camera);
                            string file = stage + "-" + state.ToLowerInvariant() + "-" + new[] { "close", "medium", "rts" }[camera] + ".png";
                            CrimsonCorsairBaker.CaptureSampledPose(viewer, 1920, 1080, folder + "/" + file);
                            evidence.screenshots.Add(file);
                        }
                    }
                    finally { AnimationMode.StopAnimationMode(); }
                }
            }
            finally
            {
                QualitySettings.renderPipeline = string.IsNullOrEmpty(previousQuality) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousQuality);
                GraphicsSettings.defaultRenderPipeline = string.IsNullOrEmpty(previousDefault) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(previousDefault);
                QualitySettings.antiAliasing = previousAA;
                // Discard all transient sampling and camera changes; never save a scene.
                if (setup.Any(scene => scene.isLoaded) && setup.Count(scene => scene.isActive) == 1)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static List<StateRecord> Snapshot(AnimatorController controller)
        {
            var result = new List<StateRecord>();
            foreach (var layer in controller.layers) Collect(layer.stateMachine, layer.name, result);
            return result;
        }
        private static void Collect(AnimatorStateMachine machine, string prefix, List<StateRecord> result)
        {
            foreach (var child in machine.states)
                result.Add(new StateRecord { Path = prefix + "/" + child.state.name, State = child.state, Motion = child.state.motion, Identity = Identity(child.state.motion) });
            foreach (var child in machine.stateMachines) Collect(child.stateMachine, prefix + "/" + child.stateMachine.name, result);
        }
        private static string Identity(Object asset)
        {
            if (!asset) return "null";
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
            return AssetDatabase.GetAssetPath(asset) + "|" + guid + "|" + localId;
        }
        private static Dictionary<string, string> ProtectedHashes(string target)
        {
            string permitted = CharacterRoot(target) + "/AnimationOverrides/", controller = ControllerPath(target);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in new[] { "Assets/Game/PirateCrew", "Assets/Game/CrimsonCorsair" })
                if (Directory.Exists(root)) foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (!path.StartsWith(permitted, StringComparison.OrdinalIgnoreCase) && path != controller) files.Add(path);
                }
            foreach (string path in new[] { ReviewScene, "Assets/Game/Scenes/CrimsonCorsair.unity", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
                if (File.Exists(path)) files.Add(path);
            return files.ToDictionary(path => path, Hash, StringComparer.OrdinalIgnoreCase);
        }
        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
