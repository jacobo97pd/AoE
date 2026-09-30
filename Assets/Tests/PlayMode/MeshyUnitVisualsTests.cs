using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// Meshy units arrive with their colours painted in. Two players of the same faction must still field armies you
    /// can tell apart, and every model must stay inside the budget the RTS camera can afford.
    /// </summary>
    public sealed class MeshyUnitVisualsTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        [TearDown] public void TearDown() { foreach (var go in created) if (go) Object.Destroy(go); created.Clear(); }

        [Test]
        public void EveryCatalogueModelIsRiggedAnimatedAndWithinBudget()
        {
            var entries = MeshyUnitVisuals.Entries;
            Assert.IsNotEmpty(entries, "The catalogue ships with at least the pilot unit.");
            foreach (var entry in entries)
            {
                var prefab = Resources.Load<GameObject>(entry.prefab);
                Assert.IsNotNull(prefab, entry.id + " prefab");
                // An animator answers for its states only once it is live in the scene.
                var instance = Object.Instantiate(prefab); created.Add(instance);
                var driver = instance.GetComponent<CorsairAnimationDriver>();
                Assert.IsTrue(driver != null && driver.HasValidRig, entry.id + " rig");
                foreach (var state in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
                    Assert.IsTrue(driver.HasState(state), entry.id + " plays " + state);
                int triangles = 0;
                foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    // Index counts need no read access; the shipped meshes are not readable.
                    for (int sub = 0; sub < skin.sharedMesh.subMeshCount; sub++) triangles += (int)skin.sharedMesh.GetIndexCount(sub) / 3;
                    Assert.IsFalse(skin.updateWhenOffscreen, entry.id + " must not recompute its bounds every frame");
                    foreach (var material in skin.sharedMaterials)
                    {
                        Assert.AreEqual("Emberfield/Meshy Unit", material.shader.name, entry.id + " shader");
                        var baseMap = material.GetTexture("_BaseMap");
                        Assert.IsTrue(baseMap != null && baseMap.width <= 1024, entry.id + " base map at most 1024");
                    }
                }
                Assert.LessOrEqual(triangles, 6000, entry.id + " triangles");
                Assert.IsNotEmpty(entry.factions, entry.id + " names the factions that field it");
            }
        }

        [Test]
        public void SameModelTakesEachOwnersColour()
        {
            var world = MeshyUnitReview.CreateWorld("aven");
            UnitState ours = null, theirs = null;
            foreach (var unit in world.Units)
                if (unit.DefinitionId == "reedguard" && unit.Id >= 600) { if (unit.OwnerId == 1) ours ??= unit; else if (unit.OwnerId == 2) theirs ??= unit; }
            Assert.IsNotNull(ours); Assert.IsNotNull(theirs);
            var a = MeshyUnitVisuals.TryCreate(world, ours, FactionKind.AvenCompact, Parent());
            var b = MeshyUnitVisuals.TryCreate(world, theirs, FactionKind.SerevinMarch, Parent());
            Assert.IsNotNull(a, "Aven spears use the kingdom model.");
            Assert.IsNotNull(b, "Serevin shares the kingdom models: the rival is the same figure.");
            var first = a.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            var second = b.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            Assert.AreNotSame(first, second);
            AssertColour(AlphaWorldArt.OwnerColor(1), first.GetColor("_TeamColor"));
            AssertColour(AlphaWorldArt.OwnerColor(2), second.GetColor("_TeamColor"));
            // A second unit of the same owner reuses the material, so a hundred spears cost one material, not a hundred.
            var c = MeshyUnitVisuals.TryCreate(world, ours, FactionKind.AvenCompact, Parent());
            Assert.AreSame(first, c.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial);
        }

        [Test]
        public void FactionsWithoutAModelKeepTheirCurrentArt()
        {
            Assert.IsNull(MeshyUnitVisuals.Resolve("reedguard", FactionKind.PirateBrotherhood), "Pirates keep their own crew.");
            Assert.IsNull(MeshyUnitVisuals.Resolve("siege_ram", FactionKind.AvenCompact), "Siege engines are not characters.");
            Assert.IsNull(MeshyUnitVisuals.Resolve("reedguard", FactionKind.MirajSultanate), "The retired desert realm is not drawn.");
        }

        [Test]
        public void BuildingModelsStayInsideTheirFootprintAndTakeTheirOwnersColour()
        {
            var world = MeshyUnitReview.CreateWorld("aven");
            Assert.IsNotEmpty(MeshyBuildingVisuals.Entries, "The catalogue ships with at least the barracks.");
            BuildingState hearth = null;
            foreach (var building in world.Buildings) if (building.DefinitionId == "hearth" && building.OwnerId == 1) hearth = building;
            Assert.IsNotNull(hearth);
            foreach (var entry in MeshyBuildingVisuals.Entries)
            {
                Assert.IsNotNull(Resources.Load<GameObject>(entry.prefab), entry.id + " prefab");
                Assert.LessOrEqual(entry.triangles, 10000, entry.id + " triangles");
            }
            if (MeshyBuildingVisuals.Resolve("hearth", FactionKind.AvenCompact) == null) Assert.Ignore("No kingdom hearth model yet.");
            float cell = world.Map.CellSizeMillimetres * .001f;
            var model = MeshyBuildingVisuals.TryCreate(hearth, FactionKind.AvenCompact, Parent(), hearth.WidthCells * cell, hearth.DepthCells * cell);
            Assert.IsNotNull(model);
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.LessOrEqual(bounds.size.x, hearth.WidthCells * cell * .97f);
            Assert.LessOrEqual(bounds.size.z, hearth.DepthCells * cell * .97f);
            AssertColour(AlphaWorldArt.OwnerColor(1), renderers[0].sharedMaterial.GetColor("_TeamColor"));
        }

        // A material colour goes through the project's colour space and back, so it returns within rounding.
        private static void AssertColour(Color expected, Color actual)
        {
            Assert.Less(Mathf.Max(Mathf.Abs(expected.r - actual.r), Mathf.Abs(expected.g - actual.g), Mathf.Abs(expected.b - actual.b)), .01f,
                "expected " + expected + " but was " + actual);
        }

        private Transform Parent()
        {
            var go = new GameObject("Meshy unit test");
            created.Add(go);
            return go.transform;
        }
    }
}
