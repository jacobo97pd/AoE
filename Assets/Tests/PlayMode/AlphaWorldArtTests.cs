using System;
using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class AlphaWorldArtTests
    {
        private GameObject root;
        private AlphaEnvironment environment;
        private WorldView view;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            view?.Dispose(); environment?.Dispose();
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [TestCase(FactionKind.AvenCompact)]
        [TestCase(FactionKind.SerevinMarch)]
        [TestCase(FactionKind.MirajSultanate)]
        [TestCase(FactionKind.SkeldClans)]
        [TestCase(FactionKind.SolarKingdom)]
        [TestCase(FactionKind.VerdantCovenant)]
        [TestCase(FactionKind.AshenDominion)]
        [TestCase(FactionKind.DrakeforgedClans)]
        public void EveryPlayableArchetypeHasSharedShadowedLodsWithinGeometryAndFootprintBudgets(FactionKind faction)
        {
            root = new GameObject("Alpha archetypes");
            foreach (string id in AlphaWorldArt.UnitIds)
            {
                var first = AlphaWorldArt.Unit(id, faction, root.transform, 1);
                var second = AlphaWorldArt.Unit(id, faction, root.transform, 2);
                CheckPair(first, second, true, 4000, 2500, 5.0f);
            }
            foreach (string id in AlphaWorldArt.BuildingIds)
            {
                var first = AlphaWorldArt.Building(id, faction, root.transform, 1, 3, 4);
                var second = AlphaWorldArt.Building(id, faction, root.transform, 2, 3, 4);
                CheckPair(first, second, true, 4000, 2500, 5.0f);
                Assert.AreEqual(new Vector3(3, 1, 4), first.localScale);
                foreach (var filter in first.GetComponentsInChildren<MeshFilter>())
                {
                    var bounds = filter.sharedMesh.bounds;
                    Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-.501f), id);
                    Assert.That(bounds.max.x, Is.LessThanOrEqualTo(.501f), id);
                    Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-.501f), id);
                    Assert.That(bounds.max.z, Is.LessThanOrEqualTo(.501f), id);
                }
            }
            Assert.IsNull(AlphaWorldArt.Unit("unknown", faction, root.transform, 1));
            Assert.IsNull(AlphaWorldArt.Building("unknown", faction, root.transform, 1, 1, 1));
        }

        [Test]
        public void FactionsHaveDifferentGeometryAndResourcesHaveColliderFreeSharedLods()
        {
            root = new GameObject("Alpha factions and resources");
            var aven = AlphaWorldArt.Unit("reedguard", FactionKind.AvenCompact, root.transform, 1).GetComponentInChildren<MeshFilter>().sharedMesh;
            var march = AlphaWorldArt.Unit("reedguard", FactionKind.SerevinMarch, root.transform, 1).GetComponentInChildren<MeshFilter>().sharedMesh;
            CollectionAssert.AreNotEqual(aven.vertices, march.vertices, "Faction silhouette differences must extend beyond recoloring.");
            foreach (var kind in new[] { ResourceKind.Food, ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Metal })
                CheckPair(AlphaWorldArt.Resource(kind, root.transform), AlphaWorldArt.Resource(kind, root.transform), false, 1600, 1000, 4.2f);
            CheckPair(AlphaWorldArt.Beacon(root.transform), AlphaWorldArt.Beacon(root.transform), false, 1200, 800, 3.0f);
            int maximumPairs = Enum.GetValues(typeof(FactionKind)).Length * (AlphaWorldArt.UnitIds.Length + AlphaWorldArt.BuildingIds.Length) + 3 * 4 + 1 + 3;
            Assert.That(AlphaWorldArt.CachedMeshCount, Is.LessThanOrEqualTo(maximumPairs * 2), "Meshes are bounded by public faction/archetype, biome resources, one beacon and three shared animated parts.");
        }

        [UnityTest]
        public IEnumerator LandscapeIsCosmeticAndExplorationGatesEveryTallDecoration()
        {
            root = new GameObject("Alpha landscape privacy");
            var world = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            int units = world.Units.Count, buildings = world.Buildings.Count, resources = world.Resources.Count;
            var blocked = (GridCell[])world.Map.BlockedCells.Clone();
            environment = new AlphaEnvironment(world, root.transform);
            Assert.IsTrue(environment.HasStream);
            Assert.That(environment.DecorationCount, Is.InRange(100, 2500));
            int hidden = 0, visible = 0;
            foreach (Transform item in environment.Root)
            {
                if (!item.name.StartsWith("Landscape ", StringComparison.Ordinal)) continue;
                bool explored = world.Vision.IsExplored(1, DefinitionLoader.ToSimulation(item.position));
                Assert.AreEqual(explored, item.gameObject.activeSelf, item.name);
                if (explored) visible++; else hidden++;
            }
            Assert.Greater(hidden, 0); Assert.Greater(visible, 0);
            Assert.AreEqual(0, environment.Root.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, world.TickIndex); Assert.AreEqual(units, world.Units.Count);
            Assert.AreEqual(buildings, world.Buildings.Count); Assert.AreEqual(resources, world.Resources.Count);
            CollectionAssert.AreEqual(blocked, world.Map.BlockedCells);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayableMapUsesAlphaArtForEitherLocalFactionAndKeepsSelectionAndHealthContextual()
        {
            root = new GameObject("Alpha playable view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            var world = DefinitionLoader.CreateOfflineWorld("serevin", VictoryMode.Dominion);
            view = new WorldView(world, root.transform, camera);
            UnitState local = null;
            foreach (var unit in world.Units) if (unit.OwnerId == 1) { local = unit; break; }
            Assert.IsNotNull(local);
            var rendered = view.RootFor(local.Id);
            Assert.IsNotNull(rendered.GetComponentInChildren<LODGroup>());
            Assert.IsNotNull(rendered.Find("Selection").GetComponent<LineRenderer>());
            Assert.IsFalse(rendered.Find("Health bar").gameObject.activeSelf);
            view.Sync(1, new[] { local.Id });
            Assert.IsTrue(rendered.Find("Selection").gameObject.activeSelf);
            Assert.IsTrue(rendered.Find("Health bar").gameObject.activeSelf);
            foreach (var enemy in world.Units)
                if (enemy.OwnerId == 2 && !world.Vision.IsEntityVisible(1, enemy.Id)) Assert.IsNull(view.RootFor(enemy.Id));
            yield return null;
        }

        private static void CheckPair(Transform first, Transform second, bool team, int highBudget, int lowBudget, float maxHeight)
        {
            Assert.IsNotNull(first); Assert.IsNotNull(second);
            var meshes = first.GetComponentsInChildren<MeshFilter>(); var otherMeshes = second.GetComponentsInChildren<MeshFilter>();
            var renderers = first.GetComponentsInChildren<MeshRenderer>(); var otherRenderers = second.GetComponentsInChildren<MeshRenderer>();
            Assert.GreaterOrEqual(meshes.Length, 2); Assert.AreEqual(meshes.Length, otherMeshes.Length); Assert.AreEqual(meshes.Length, renderers.Length);
            Assert.AreEqual(0, first.GetComponentsInChildren<Collider>(true).Length);
            foreach (var group in first.GetComponentsInChildren<LODGroup>())
            {
                var levels = group.GetLODs(); Assert.AreEqual(2, levels.Length);
                var near = levels[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                var far = levels[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.Less(far.triangles.Length, near.triangles.Length, group.name);
            }
            for (int indexMesh = 0; indexMesh < meshes.Length; indexMesh++)
            {
                var mesh = meshes[indexMesh].sharedMesh;
                Assert.AreSame(mesh, otherMeshes[indexMesh].sharedMesh); Assert.AreSame(ArtKit.Catalog.Material, renderers[indexMesh].sharedMaterial);
                Assert.AreEqual(ShadowCastingMode.On, renderers[indexMesh].shadowCastingMode); Assert.IsTrue(renderers[indexMesh].receiveShadows);
                Assert.That(mesh.triangles.Length / 3, Is.InRange(1, meshes[indexMesh].name == "LOD0" ? highBudget : lowBudget), mesh.name);
                var local = first.worldToLocalMatrix * meshes[indexMesh].transform.localToWorldMatrix;
                var minimum = local.MultiplyPoint3x4(mesh.bounds.min); var maximum = local.MultiplyPoint3x4(mesh.bounds.max);
                Assert.That(minimum.y, Is.GreaterThanOrEqualTo(-.001f), mesh.name);
                Assert.That(maximum.y, Is.LessThanOrEqualTo(maxHeight), mesh.name);
                var vertices = mesh.vertices; var normals = mesh.normals;
                Assert.AreEqual(vertices.Length, normals.Length); Assert.AreEqual(vertices.Length, mesh.colors.Length);
                for (int index = 0; index < vertices.Length; index++)
                {
                    Assert.IsFalse(float.IsNaN(vertices[index].sqrMagnitude) || float.IsInfinity(vertices[index].sqrMagnitude), mesh.name);
                    Assert.That(normals[index].sqrMagnitude, Is.InRange(.999f, 1.001f), mesh.name);
                }
                if (team)
                {
                    // Portcullis is an unpainted mechanical subpart; the parent gate carries the team banner.
                    if (!mesh.name.Contains("portcullis")) Assert.IsTrue(Array.Exists(mesh.colors, color => color.a > .99f && color.a < 1.01f), "Missing ownership mask: " + mesh.name);
                    var one = new MaterialPropertyBlock(); var two = new MaterialPropertyBlock();
                    renderers[indexMesh].GetPropertyBlock(one); otherRenderers[indexMesh].GetPropertyBlock(two);
                    Assert.AreNotEqual(one.GetColor("_TeamColor"), two.GetColor("_TeamColor"));
                }
            }
        }
    }
}
