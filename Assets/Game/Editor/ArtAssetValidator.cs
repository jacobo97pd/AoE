using System;
using System.Collections.Generic;
using System.IO;
using Emberfield.Presentation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    /// <summary>Measured engineering checks for the two unapproved ArtStyleLab candidates; no FPS or visual-parity claim.</summary>
    public static class ArtAssetValidator
    {
        public const string AssetRoot = "Assets/Game/ArtStyleLab";
        public const string WorkerPath = AssetRoot + "/Prefabs/KingdomWorker.prefab";
        public const string WarriorPath = AssetRoot + "/Prefabs/KingdomWarrior.prefab";
        private static readonly int[] Ceilings = { 8000, 4000, 1500 };
        private static readonly int[] TargetMin = { 3000, 1500, 500 }, TargetMax = { 6000, 3000, 1200 };

        [Serializable] public sealed class LodReport
        {
            public int level, triangles, vertices, bones, bindposes, weightedVertices, teamVertices, neutralVertices;
            public float screenRelativeHeight, maxWeightError, maxRestMatrixError;
            public string mesh, material, shader;
        }
        [Serializable] public sealed class ClipReport
        {
            public string name;
            public float length;
            public int boneBindings, changingBoneBindings;
        }
        [Serializable] public sealed class PrefabReport
        {
            public string path, gameplayId;
            public bool passed;
            public int rendererCount, materialCount;
            public List<LodReport> lods = new List<LodReport>();
            public List<ClipReport> clips = new List<ClipReport>();
            public List<string> problems = new List<string>(), warnings = new List<string>();
        }
        [Serializable] public sealed class TextureReport { public string path, name; public int width, height; }
        [Serializable] public sealed class Report
        {
            public string generatedUtc, unityVersion;
            public string scope = "Two ArtStyleLab candidate prefabs. Asset integrity and authoring budgets only; no physical-device performance or visual-parity result.";
            public bool passed;
            public int materialCount, textureCount, visualDefinitionCount, cosmeticDefinitionCount;
            public List<PrefabReport> prefabs = new List<PrefabReport>();
            public List<TextureReport> textures = new List<TextureReport>();
            public List<string> problems = new List<string>();
        }

        public static Report ValidateOrThrow(string reportPath)
        {
            if (string.IsNullOrEmpty(reportPath)) throw new ArgumentException("Choose an asset-validation report path.", nameof(reportPath));
            var report = ValidateProject();
            string absolute = Path.GetFullPath(reportPath), directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(absolute, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("ArtStyleLab validation failed. Inspect " + absolute + ": " + string.Join("; ", report.problems));
            return report;
        }

        public static Report ValidateProject()
        {
            var result = new Report { generatedUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            var materials = new HashSet<Material>(); var textures = new HashSet<Texture>();
            foreach (var pair in new[] { new[] { WorkerPath, "tender" }, new[] { WarriorPath, "reedguard" } })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pair[0]);
                var inspected = ValidatePrefab(prefab, pair[1]); inspected.path = pair[0]; result.prefabs.Add(inspected);
                foreach (string problem in inspected.problems) result.problems.Add(pair[1] + ": " + problem);
                if (prefab == null) continue;
                foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !materials.Add(material)) continue;
                        foreach (string property in material.GetTexturePropertyNames())
                        {
                            var texture = material.GetTexture(property); if (texture == null || !textures.Add(texture)) continue;
                            result.textures.Add(new TextureReport { path = AssetDatabase.GetAssetPath(texture), name = texture.name, width = texture.width, height = texture.height });
                            if (texture.width > 2048 || texture.height > 2048 || !Mathf.IsPowerOfTwo(texture.width) || !Mathf.IsPowerOfTwo(texture.height))
                                result.problems.Add("Texture dimensions must be powers of two and <=2048: " + texture.name);
                        }
                    }
            }
            result.materialCount = materials.Count; result.textureCount = textures.Count;
            if (materials.Count != 1) result.problems.Add("The two candidates must share one material; found " + materials.Count + ".");
            var definitions = AssetDatabase.FindAssets("t:UnitVisualDefinition", new[] { AssetRoot });
            result.visualDefinitionCount = definitions.Length;
            var identities = new HashSet<string>();
            foreach (string guid in definitions)
            {
                var definition = AssetDatabase.LoadAssetAtPath<UnitVisualDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.Prefab == null || definition.Faction == null) { result.problems.Add("Broken unit visual definition: " + guid); continue; }
                if (definition.GameplayDefinitionId != "tender" && definition.GameplayDefinitionId != "reedguard") result.problems.Add("Unexpected lab gameplay identity: " + definition.GameplayDefinitionId);
                if (!identities.Add(definition.GameplayDefinitionId)) result.problems.Add("Duplicate candidate visual definition for " + definition.GameplayDefinitionId);
                if (definition.Faction.FactionId != "aven") result.problems.Add("First lab candidate must preserve the Aven identity.");
                if (definition.ProductionApproved) result.problems.Add("The lab candidate is marked production-approved before checkpoint review.");
                var unit = definition.Prefab.GetComponent<ArtStyleUnit>();
                if (unit == null || unit.Definition != definition) result.problems.Add("Visual definition and prefab do not reference each other: " + definition.name);
            }
            if (!identities.Contains("tender") || !identities.Contains("reedguard")) result.problems.Add("Both tender and reedguard visual definitions must exist.");
            var skins = AssetDatabase.FindAssets("t:CosmeticSkinDefinition", new[] { AssetRoot }); result.cosmeticDefinitionCount = skins.Length;
            foreach (string guid in skins)
            {
                var skin = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (skin == null || string.IsNullOrEmpty(skin.SkinId) || skin.UnitVisual == null || skin.UnitVisual.Prefab == null)
                    result.problems.Add("Broken cosmetic visual definition: " + guid);
            }
            result.passed = result.problems.Count == 0; return result;
        }

        public static PrefabReport ValidatePrefab(GameObject prefab, string expectedGameplayId)
        {
            var result = new PrefabReport { path = prefab == null ? "<missing>" : AssetDatabase.GetAssetPath(prefab), gameplayId = expectedGameplayId };
            if (prefab == null) { result.problems.Add("Missing prefab."); return result; }
            var unit = prefab.GetComponent<ArtStyleUnit>();
            if (unit == null || unit.Definition == null || unit.Definition.GameplayDefinitionId != expectedGameplayId)
                result.problems.Add("Missing or incorrect ArtStyleUnit/UnitVisualDefinition reference.");
            if (unit != null && (unit.Skin == null || unit.Skin.UnitVisual != unit.Definition)) result.problems.Add("Missing or mismatched default cosmetic definition.");
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) result.problems.Add("Missing script on " + transform.name);
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true); result.rendererCount = renderers.Length;
            var group = prefab.GetComponent<LODGroup>(); var lods = group == null ? Array.Empty<LOD>() : group.GetLODs();
            if (lods.Length != 3) result.problems.Add("Expected three authored LOD levels, found " + lods.Length + ".");
            if (renderers.Length != 3) result.problems.Add("Expected one SkinnedMeshRenderer per LOD.");
            var materials = new HashSet<Material>(); var usedRenderers = new HashSet<Renderer>();
            int previousTriangles = int.MaxValue; float previousHeight = float.PositiveInfinity;
            for (int level = 0; level < lods.Length; level++)
            {
                var lod = lods[level];
                if (lod.renderers == null || lod.renderers.Length != 1 || !(lod.renderers[0] is SkinnedMeshRenderer renderer))
                { result.problems.Add("LOD" + level + " must reference one skinned renderer."); continue; }
                if (!usedRenderers.Add(renderer)) result.problems.Add("LOD levels reuse the same renderer.");
                if (!renderer.transform.IsChildOf(prefab.transform)) result.problems.Add("LOD renderer is outside the prefab hierarchy.");
                if (lod.screenRelativeTransitionHeight <= 0 || lod.screenRelativeTransitionHeight >= previousHeight) result.problems.Add("LOD screen thresholds must decrease and remain positive.");
                previousHeight = lod.screenRelativeTransitionHeight;
                var measured = new LodReport { level = level, screenRelativeHeight = lod.screenRelativeTransitionHeight }; result.lods.Add(measured);
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) { result.problems.Add("Missing mesh at LOD" + level + "."); continue; }
                measured.mesh = AssetDatabase.GetAssetPath(mesh); measured.vertices = mesh.vertexCount; measured.triangles = mesh.triangles.Length / 3;
                if (measured.vertices == 0 || measured.triangles == 0) result.problems.Add("Empty mesh at LOD" + level + ".");
                // The lab recipe compacts unused vertices. A stale native vertex buffer after
                // regeneration can otherwise pair old geometry with new triangle indices.
                var referenced = new HashSet<int>(mesh.triangles);
                if (referenced.Count != mesh.vertexCount) result.problems.Add("Unused or stale vertex buffer at LOD" + level + ".");
                if (mesh.subMeshCount != 1) result.problems.Add("Each LOD must use one material-compatible submesh.");
                if (mesh.uv.Length != mesh.vertexCount || mesh.uv2.Length != mesh.vertexCount) result.problems.Add("Missing atlas UVs or per-vertex material surface data.");
                if (measured.triangles >= previousTriangles) result.problems.Add("Triangles must strictly decrease from LOD0 through LOD2.");
                previousTriangles = measured.triangles;
                if (level < Ceilings.Length && measured.triangles > Ceilings[level]) result.problems.Add("LOD" + level + " exceeds hard triangle ceiling " + Ceilings[level] + ".");
                if (level < TargetMin.Length && (measured.triangles < TargetMin[level] || measured.triangles > TargetMax[level])) result.warnings.Add("LOD" + level + " is outside the suggested authoring band " + TargetMin[level] + "–" + TargetMax[level] + "; review silhouette and cost.");
                foreach (var vertex in mesh.vertices) if (!Finite(vertex)) { result.problems.Add("Non-finite mesh vertex."); break; }
                var normals = mesh.normals;
                if (normals.Length != mesh.vertexCount) result.problems.Add("Missing mesh normals.");
                else foreach (var normal in normals) if (!Finite(normal) || normal.sqrMagnitude < .25f) { result.problems.Add("Invalid mesh normal."); break; }
                var colors = mesh.colors;
                if (colors.Length != mesh.vertexCount) result.problems.Add("Missing vertex team-mask colors.");
                else foreach (var color in colors)
                {
                    if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a)) { result.problems.Add("Non-finite mesh color."); break; }
                    if (color.a >= .5f) measured.teamVertices++; else measured.neutralVertices++;
                }
                if (measured.teamVertices == 0 || measured.neutralVertices == 0) result.problems.Add("Every LOD needs both team-masked and unmasked surfaces.");
                ValidateSkinning(renderer, mesh, measured, result.problems);
                if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null) result.problems.Add("LOD" + level + " must have one valid shared material.");
                else
                {
                    var material = renderer.sharedMaterial; materials.Add(material); measured.material = AssetDatabase.GetAssetPath(material);
                    var shader = material.shader; measured.shader = shader == null ? "<missing>" : shader.name;
                    if (shader == null || shader.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(shader)) result.problems.Add("Missing or erroneous material shader.");
                    if (material.HasProperty("_NormalStrength") && material.GetFloat("_NormalStrength") > .001f && mesh.tangents.Length != mesh.vertexCount)
                        result.problems.Add("An enabled normal map requires authored mesh tangents.");
                    foreach (string property in new[] { "_BaseMap", "_NormalMap", "_Metallic", "_Smoothness", "_TeamColor", "_TeamMask", "_FactionTint", "_RimStrength", "_Selection", "_DamageFlash" })
                        if (!material.HasProperty(property)) result.problems.Add("Shader property missing: " + property);
                }
            }
            result.materialCount = materials.Count;
            if (materials.Count != 1) result.problems.Add("LOD levels must share one material.");
            ValidateAnimations(prefab, expectedGameplayId, result);
            result.passed = result.problems.Count == 0; return result;
        }

        private static void ValidateSkinning(SkinnedMeshRenderer renderer, Mesh mesh, LodReport measured, List<string> problems)
        {
            var bones = renderer.bones; var bindposes = mesh.bindposes; var weights = mesh.boneWeights;
            measured.bones = bones.Length; measured.bindposes = bindposes.Length; measured.weightedVertices = weights.Length;
            if (renderer.rootBone == null || bones.Length == 0 || bones.Length != bindposes.Length) problems.Add("Missing root bone or unmatched bones/bindposes.");
            if (weights.Length != mesh.vertexCount) problems.Add("Every vertex needs skin weights.");
            for (int index = 0; index < bindposes.Length; index++)
            {
                var bindpose = bindposes[index];
                for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                    if (!Finite(bindpose[row, column])) { problems.Add("Non-finite bindpose."); return; }
                if (index >= bones.Length || bones[index] == null) { problems.Add("Null/missing bone reference."); continue; }
                var rest = renderer.transform.worldToLocalMatrix * bones[index].localToWorldMatrix * bindpose;
                for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                {
                    if (!Finite(rest[row, column])) { problems.Add("Non-finite skeleton transform."); return; }
                    measured.maxRestMatrixError = Mathf.Max(measured.maxRestMatrixError, Mathf.Abs(rest[row, column] - (row == column ? 1 : 0)));
                }
            }
            if (measured.maxRestMatrixError > .005f) problems.Add("Skeleton rest pose does not match bindposes.");
            foreach (var weight in weights)
            {
                var values = new[] { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
                var indices = new[] { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 };
                float total = 0;
                for (int index = 0; index < 4; index++)
                {
                    if (!Finite(values[index]) || values[index] < 0) { problems.Add("Invalid skin weight."); return; }
                    if (values[index] > 0 && (indices[index] < 0 || indices[index] >= bones.Length)) { problems.Add("Skin weight references a missing bone."); return; }
                    total += values[index];
                }
                measured.maxWeightError = Mathf.Max(measured.maxWeightError, Mathf.Abs(total - 1));
            }
            if (measured.maxWeightError > .001f) problems.Add("Skin weights are not normalized.");
        }

        private static void ValidateAnimations(GameObject prefab, string gameplayId, PrefabReport result)
        {
            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) { result.problems.Add("Missing Animator/controller."); return; }
            if (animator.applyRootMotion) result.problems.Add("Candidate animation must remain in place.");
            var bonePaths = new HashSet<string>();
            foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var bone in renderer.bones) if (bone != null) bonePaths.Add(AnimationUtility.CalculateTransformPath(bone, prefab.transform));
            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) { result.problems.Add("Animator has a missing clip."); continue; }
                if (clips.ContainsKey(clip.name)) continue; clips.Add(clip.name, clip);
                var measured = new ClipReport { name = clip.name, length = clip.length }; result.clips.Add(measured);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Transform) || !bonePaths.Contains(binding.path)) continue;
                    measured.boneBindings++; var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null || curve.length < 2) continue;
                    float first = curve.keys[0].value;
                    foreach (var key in curve.keys) if (Mathf.Abs(key.value - first) > .00001f) { measured.changingBoneBindings++; break; }
                }
                if (measured.length <= 0 || measured.changingBoneBindings == 0) result.problems.Add("Animation has no changing bone curves: " + clip.name);
            }
            foreach (string required in new[] { "Idle", "Walk", gameplayId == "tender" ? "Gather" : "Attack01" })
                if (!clips.ContainsKey(required)) result.problems.Add("Missing initial animation clip: " + required);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
