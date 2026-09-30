using System.Collections;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class ArtKitTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup() { if (root != null) Object.Destroy(root); yield return null; }

        [Test]
        public void BakedCatalogContainsElevenDistinctPairsWithValidBoundsFlatNormalsMasksAndLowerDetailBudgets()
        {
            var catalog = ArtKit.Catalog;
            Assert.IsNotNull(catalog, "Run Emberfield.Editor.ArtKitBaker.Bake before validating or packaging the art slice.");
            Assert.AreEqual("emberfield-original-faceted-v1", catalog.SourceVersion);
            Assert.AreEqual(4, catalog.Units.Length); Assert.AreEqual(3, catalog.Buildings.Length); Assert.AreEqual(4, catalog.Resources.Length);
            Assert.IsNotNull(catalog.Material); Assert.AreEqual("Emberfield/Faceted Team", catalog.Material.shader.name);
            Assert.IsTrue(catalog.Material.enableInstancing);
            foreach (var entry in catalog.Units)
            {
                ValidatePair(entry, 1500, 900, true);
                Assert.That(entry.Lod0.bounds.min.y, Is.GreaterThanOrEqualTo(-.001f));
                Assert.That(entry.Lod0.bounds.max.y, Is.LessThanOrEqualTo(2.3f));
            }
            foreach (var entry in catalog.Buildings)
            {
                ValidatePair(entry, 2000, 1000, true);
                foreach (var mesh in new[] { entry.Lod0, entry.Lod1 })
                {
                    Assert.That(mesh.bounds.min.x, Is.GreaterThanOrEqualTo(-.5f)); Assert.That(mesh.bounds.max.x, Is.LessThanOrEqualTo(.5f));
                    Assert.That(mesh.bounds.min.z, Is.GreaterThanOrEqualTo(-.5f)); Assert.That(mesh.bounds.max.z, Is.LessThanOrEqualTo(.5f));
                    Assert.That(mesh.bounds.min.y, Is.GreaterThanOrEqualTo(-.001f)); Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(2.3f));
                }
            }
            foreach (var entry in catalog.Resources)
            {
                ValidatePair(entry, 1000, 500, false);
                Assert.That(entry.Lod0.bounds.size.x, Is.LessThanOrEqualTo(1.4f)); Assert.That(entry.Lod0.bounds.size.z, Is.LessThanOrEqualTo(1.4f));
                Assert.That(entry.Lod0.bounds.min.y, Is.GreaterThanOrEqualTo(-.001f)); Assert.That(entry.Lod0.bounds.max.y, Is.LessThanOrEqualTo(2.5f));
            }
            var tender = Find(catalog.Units, "tender").Lod0.bounds;
            var spear = Find(catalog.Units, "reedguard").Lod0.bounds;
            var archer = Find(catalog.Units, "stringwarden").Lod0.bounds;
            var cavalry = Find(catalog.Units, "strider").Lod0.bounds;
            Assert.That(spear.max.y, Is.GreaterThan(tender.max.y + .3f), "The spear should remain a tall role silhouette.");
            Assert.That(archer.size.x, Is.GreaterThan(.9f), "The bow should have a broad overhead silhouette.");
            Assert.That(cavalry.size.z, Is.GreaterThan(spear.size.z * 2), "A mounted unit needs a clear long body profile.");
        }

        [UnityTest]
        public IEnumerator InstancesShareTwoMergedLodMeshesAndOneMaterialWhileKeepingIndependentTeamMasks()
        {
            root = new GameObject("Art kit instances");
            var blue = ArtKit.CreateUnit("reedguard", root.transform, 1);
            var red = ArtKit.CreateUnit("reedguard", root.transform, 2);
            Assert.IsNotNull(blue); Assert.IsNotNull(red);
            var blueMeshes = blue.GetComponentsInChildren<MeshFilter>(); var redMeshes = red.GetComponentsInChildren<MeshFilter>();
            var blueRenderers = blue.GetComponentsInChildren<MeshRenderer>(); var redRenderers = red.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(2, blueMeshes.Length); Assert.AreEqual(2, blueRenderers.Length);
            Assert.AreEqual(2, redMeshes.Length); Assert.AreEqual(2, redRenderers.Length);
            var blueBlock = new MaterialPropertyBlock(); var redBlock = new MaterialPropertyBlock();
            for (int i = 0; i < 2; i++)
            {
                Assert.AreSame(blueMeshes[i].sharedMesh, redMeshes[i].sharedMesh);
                Assert.AreSame(ArtKit.Catalog.Material, blueRenderers[i].sharedMaterial);
                Assert.AreSame(blueRenderers[i].sharedMaterial, redRenderers[i].sharedMaterial);
                blueRenderers[i].GetPropertyBlock(blueBlock); redRenderers[i].GetPropertyBlock(redBlock);
                AssertColor(ArtKit.OwnerColor(1), blueBlock.GetColor("_TeamColor"));
                AssertColor(ArtKit.OwnerColor(2), redBlock.GetColor("_TeamColor"));
                Assert.AreNotEqual(blueBlock.GetColor("_TeamColor"), redBlock.GetColor("_TeamColor"));
            }
            var lods = blue.GetComponent<LODGroup>().GetLODs();
            Assert.AreEqual(2, lods.Length); Assert.AreEqual(1, lods[0].renderers.Length); Assert.AreEqual(1, lods[1].renderers.Length);
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            var hall = ArtKit.CreateBuilding("hearth", root.transform, 1, 4, 5);
            Assert.AreEqual(new Vector3(4, 1, 5), hall.localScale);
            Assert.IsNull(ArtKit.CreateUnit("ashrunner", root.transform, 2), "Unauthored units must return control to the existing presentation fallback.");
            yield return null;
        }

        private static ArtKitEntry Find(ArtKitEntry[] entries, string id)
        { foreach (var entry in entries) if (entry.Id == id) return entry; Assert.Fail("Missing recipe: " + id); return null; }
        private static void AssertColor(Color expected, Color actual)
        {
            // Native material-property storage can round-trip a channel with float rounding.
            for (int channel = 0; channel < 4; channel++)
                Assert.That(actual[channel], Is.EqualTo(expected[channel]).Within(.000001f), "Team tint channel " + channel);
        }
        private static void ValidatePair(ArtKitEntry entry, int highBudget, int lowBudget, bool teamMasked)
        {
            Assert.IsNotNull(entry.Lod0, entry.Id); Assert.IsNotNull(entry.Lod1, entry.Id); Assert.AreNotSame(entry.Lod0, entry.Lod1);
            Assert.That(entry.Lod0.triangles.Length / 3, Is.LessThanOrEqualTo(highBudget));
            Assert.That(entry.Lod1.triangles.Length / 3, Is.LessThanOrEqualTo(lowBudget));
            Assert.That(entry.Lod1.triangles.Length, Is.LessThan(entry.Lod0.triangles.Length));
            foreach (var mesh in new[] { entry.Lod0, entry.Lod1 })
            {
                Assert.AreEqual(1, mesh.subMeshCount); Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                var vertices = mesh.vertices; var triangles = mesh.triangles; var colors = mesh.colors; var normals = mesh.normals;
                Assert.AreEqual(vertices.Length, colors.Length); Assert.AreEqual(vertices.Length, normals.Length);
                bool masked = false, plain = false;
                foreach (var color in colors) { if (color.a >= .99f) masked = true; if (color.a <= .01f) plain = true; }
                Assert.IsTrue(plain); Assert.AreEqual(teamMasked, masked, entry.Id + " team-mask coverage");
                foreach (var vertex in vertices)
                    Assert.IsFalse(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                        float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z));
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    int a = triangles[triangle], b = triangles[triangle + 1], c = triangles[triangle + 2];
                    Assert.That(a, Is.InRange(0, vertices.Length - 1)); Assert.That(b, Is.InRange(0, vertices.Length - 1)); Assert.That(c, Is.InRange(0, vertices.Length - 1));
                    var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    Assert.That(cross.sqrMagnitude, Is.GreaterThan(.000000001f));
                    Assert.That(Vector3.Dot(cross.normalized, normals[a]), Is.GreaterThan(.999f));
                }
            }
        }
    }
}
