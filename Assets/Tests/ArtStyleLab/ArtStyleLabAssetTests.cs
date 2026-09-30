using System;
using System.Collections.Generic;
using System.Reflection;
using Emberfield.Editor;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.ArtStyleLab
{
    public sealed class ArtStyleLabAssetTests
    {
        [Test] public void CandidateAssetsMeetMeasuredIntegrityChecks()
        {
            var report = ArtAssetValidator.ValidateProject();
            Assert.That(report.passed, Is.True, string.Join("\n", report.problems));
            Assert.That(report.prefabs.Count, Is.EqualTo(2));
            Assert.That(report.materialCount, Is.EqualTo(1));
        }

        [TestCase(ArtAssetValidator.WorkerPath)]
        [TestCase(ArtAssetValidator.WarriorPath)]
        public void RestPoseBakesToAuthoredMeshAndClipsDeformWeightedVertices(string path)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(source, Is.Not.Null, path);
            var instance = Object.Instantiate(source); var baked = new Mesh();
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); instance.transform.localScale = Vector3.one;
                var animator = instance.GetComponent<Animator>(); animator.enabled = false;
                var group = instance.GetComponent<LODGroup>(); group.ForceLOD(0);
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var positions = new Vector3[transforms.Length]; var rotations = new Quaternion[transforms.Length]; var scales = new Vector3[transforms.Length];
                for (int index = 0; index < transforms.Length; index++) { positions[index] = transforms[index].localPosition; rotations[index] = transforms[index].localRotation; scales[index] = transforms[index].localScale; }
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.BakeMesh(baked); var rest = baked.vertices; var authored = renderer.sharedMesh.vertices;
                    Assert.That(rest.Length, Is.EqualTo(authored.Length));
                    float maxError = 0;
                    for (int index = 0; index < rest.Length; index++) maxError = Mathf.Max(maxError, Vector3.Distance(rest[index], authored[index]));
                    Assert.That(maxError, Is.LessThan(.005f), renderer.name + " bindpose changes geometry at rest");
                }
                var lod0 = (SkinnedMeshRenderer)group.GetLODs()[0].renderers[0]; var clips = animator.runtimeAnimatorController.animationClips;
                Assert.That(clips.Length, Is.GreaterThanOrEqualTo(3));
                foreach (var clip in clips)
                {
                    Restore(); clip.SampleAnimation(instance, 0); lod0.BakeMesh(baked); var start = baked.vertices; float movement = 0;
                    foreach (float fraction in new[] { .17f, .39f, .67f, .83f })
                    {
                        Restore(); clip.SampleAnimation(instance, clip.length * fraction); lod0.BakeMesh(baked); var moved = baked.vertices;
                        for (int index = 0; index < start.Length; index++) movement = Mathf.Max(movement, Vector3.Distance(start[index], moved[index]));
                    }
                    Assert.That(movement, Is.GreaterThan(.0001f), clip.name + " animates curves without deforming weighted vertices");
                    Assert.That(instance.transform.localPosition, Is.EqualTo(Vector3.zero), clip.name + " changes authoritative root position");
                }
                void Restore()
                {
                    for (int index = 0; index < transforms.Length; index++) { transforms[index].localPosition = positions[index]; transforms[index].localRotation = rotations[index]; transforms[index].localScale = scales[index]; }
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(instance); }
        }

        [TestCase(ArtAssetValidator.WorkerPath)]
        [TestCase(ArtAssetValidator.WarriorPath)]
        public void TeamAndCosmeticChangesKeepSharedMaterialMeshAndUnitIdentity(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null);
            var first = Object.Instantiate(prefab); var second = Object.Instantiate(prefab);
            var skin = ScriptableObject.CreateInstance<CosmeticSkinDefinition>();
            try
            {
                var a = first.GetComponent<ArtStyleUnit>(); var b = second.GetComponent<ArtStyleUnit>();
                var definition = a.Definition; Assert.That(b.Definition, Is.SameAs(definition));
                a.SetTeam(Color.blue); b.SetTeam(Color.red);
                skin.SkinId = "test-only-visual-tint"; skin.UnitVisual = definition; skin.SurfaceTint = new Color(.3f, .8f, .4f);
                a.Skin = skin; a.RefreshAppearance();
                var left = first.GetComponentsInChildren<SkinnedMeshRenderer>(true); var right = second.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var blockA = new MaterialPropertyBlock(); var blockB = new MaterialPropertyBlock();
                for (int index = 0; index < left.Length; index++)
                {
                    Assert.That(left[index].sharedMesh, Is.SameAs(right[index].sharedMesh));
                    Assert.That(left[index].sharedMaterial, Is.SameAs(right[index].sharedMaterial));
                    left[index].GetPropertyBlock(blockA); right[index].GetPropertyBlock(blockB);
                    Assert.That(blockA.GetColor("_TeamColor"), Is.EqualTo(Color.blue));
                    Assert.That(blockB.GetColor("_TeamColor"), Is.EqualTo(Color.red));
                    // Color properties round-trip through Unity's native color conversion.
                    // Compare channels within floating-point tolerance, not struct bit equality.
                    var actualTint = blockA.GetColor("_FactionTint");
                    for (int channel = 0; channel < 4; channel++)
                        Assert.That(actualTint[channel], Is.EqualTo(skin.SurfaceTint[channel]).Within(.000001f));
                    Assert.That(blockB.GetColor("_FactionTint"), Is.Not.EqualTo(skin.SurfaceTint));
                }
                Assert.That(a.Definition, Is.SameAs(definition)); Assert.That(a.Definition.GameplayDefinitionId, Is.EqualTo(b.Definition.GameplayDefinitionId));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(skin); }
        }

        [Test] public void ValidatorRejectsMissingLodMeshAndUnnormalizedWeights()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArtAssetValidator.WorkerPath); Assert.That(source, Is.Not.Null);
            var instance = Object.Instantiate(source); Mesh damaged = null;
            try
            {
                var renderer = (SkinnedMeshRenderer)instance.GetComponent<LODGroup>().GetLODs()[0].renderers[0]; var original = renderer.sharedMesh;
                renderer.sharedMesh = null;
                var missing = ArtAssetValidator.ValidatePrefab(instance, "tender");
                Assert.That(missing.passed, Is.False); Assert.That(missing.problems.Exists(problem => problem.Contains("Missing mesh")), Is.True);
                damaged = Object.Instantiate(original); var weights = damaged.boneWeights;
                weights[0].weight0 = weights[0].weight1 = weights[0].weight2 = weights[0].weight3 = 0;
                damaged.boneWeights = weights; renderer.sharedMesh = damaged;
                var invalid = ArtAssetValidator.ValidatePrefab(instance, "tender");
                Assert.That(invalid.passed, Is.False); Assert.That(invalid.problems.Exists(problem => problem.Contains("not normalized")), Is.True);
            }
            finally { Object.DestroyImmediate(instance); if (damaged != null) Object.DestroyImmediate(damaged); }
        }

        [Test] public void VisualDefinitionsContainNoCombatOrEconomyData()
        {
            var prohibited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Health", "MaxHealth", "Damage", "Armor", "Speed", "MoveSpeed", "Cost", "Population", "PopulationCost", "Attack", "Range", "GatherAmount", "TrainTicks" };
            foreach (var type in new[] { typeof(UnitVisualDefinition), typeof(CosmeticSkinDefinition), typeof(FactionVisualDefinition) })
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    Assert.That(prohibited.Contains(field.Name), Is.False, type.Name + "." + field.Name);
                    Assert.That(field.FieldType.Namespace, Is.Not.EqualTo("Emberfield.Simulation"), type.Name + "." + field.Name + " imports simulation data");
                }
        }
    }
}
