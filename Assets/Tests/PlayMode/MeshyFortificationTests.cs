using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// Each Meshy culture draws its own walls and gates. A segment is built at the game's size and meets its neighbours
    /// along X, a gate carries its culture's leaf where WorldView's opening finds it, and the factions with no set of
    /// their own keep the procedural wall and gate.
    /// </summary>
    public sealed class MeshyFortificationTests
    {
        private const int WestWall = 900, Gate = 901, EastWall = 902;
        private const int Row = 30500, WestX = 23500, GateX = 26000, EastX = 28500;
        private const float Tolerance = .02f;
        private static readonly int[] None = new int[0];
        private GameObject root;
        private WorldView view;

        // A frame passes so this case's world is gone before the next one draws its own on the same spot.
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            view?.Dispose(); view = null;
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [TestCase(FactionKind.AvenCompact, "kingdom")]
        [TestCase(FactionKind.SerevinMarch, "kingdom")]
        [TestCase(FactionKind.EnglishKingdom, "kingdom")]
        [TestCase(FactionKind.SkeldClans, "mountain")]
        [TestCase(FactionKind.DrakeforgedClans, "dwarf")]
        [TestCase(FactionKind.VerdantCovenant, "elf")]
        [TestCase(FactionKind.AshenDominion, "orc")]
        [TestCase(FactionKind.DesertSultanate, "sultanate")]
        [TestCase(FactionKind.SahelConfederation, "sahel")]
        public void EachCultureHasItsOwnWallGateAndLeaf(FactionKind faction, string style)
        {
            foreach (var building in new[] { "wall", "gate", MeshyBuildingVisuals.GateLeaf })
            {
                var entry = MeshyBuildingVisuals.Resolve(building, faction);
                Assert.IsNotNull(entry, faction + " has a Meshy " + building + ".");
                Assert.AreEqual(style, entry.style, faction + " " + building);
                Assert.IsNotNull(Resources.Load<GameObject>(entry.prefab), entry.id + " prefab");
            }
        }

        [Test]
        public void TheFortificationsAreTheWallAndGateDefinitions()
        {
            var world = CreateWorld("aven");
            foreach (var definition in world.Definition.Buildings)
                Assert.AreEqual(definition.IsWall || definition.IsGate, MeshyBuildingVisuals.IsFortification(definition.Id), definition.Id);
        }

        [TestCase("aven")]
        [TestCase("english")]
        [TestCase("skeld")]
        [TestCase("drakeforged")]
        [TestCase("verdant")]
        [TestCase("ashen")]
        [TestCase("sultanate")]
        [TestCase("sahel")]
        public void AWallGateWallLineTilesAndTheGateOpens(string faction)
        {
            var world = CreateWorld(faction);
            Show(world);
            Assert.IsTrue(world.TryGetPlayer(1, out var player));
            var kind = FactionKind.AvenCompact;
            foreach (var definition in world.Definition.Factions) if (definition.Id == player.FactionId) kind = definition.Kind;
            var west = Meshy(WestWall, "wall", kind);
            var gate = Meshy(Gate, "gate", kind);
            var east = Meshy(EastWall, "wall", kind);

            var westBounds = Extent(west, null);
            var eastBounds = Extent(east, null);
            var leaf = gate.Find("Portcullis");
            Assert.IsNotNull(leaf, faction + ": the gate's leaf is a direct child named Portcullis, where WorldView opens it.");
            Assert.IsNotNull(leaf.GetComponentInChildren<Renderer>(), faction + ": the leaf is drawn.");
            var gateBounds = Extent(gate, leaf);
            foreach (var (name, bounds, x) in new[] { ("west wall", westBounds, WestX), ("east wall", eastBounds, EastX) })
            {
                Assert.AreEqual(3, bounds.size.x, Tolerance, faction + ": the " + name + " takes its whole 3 m along X.");
                Assert.AreEqual(x * .001f, bounds.center.x, Tolerance, faction + ": the " + name + " is centred on its footprint.");
                Assert.AreEqual(0, bounds.min.y, Tolerance, faction + ": the " + name + " stands on the ground.");
                Assert.AreEqual(AlphaWorldArt.BuildingHeight("wall"), bounds.max.y, Tolerance, faction + ": the " + name + " is as tall as the wall.");
                Assert.LessOrEqual(bounds.size.z, 1 + Tolerance, faction + ": the " + name + " stays inside its footprint's depth.");
            }
            Assert.AreEqual(2, gateBounds.size.x, Tolerance, faction + ": the gate takes its whole 2 m along X.");
            Assert.AreEqual(westBounds.max.x, gateBounds.min.x, Tolerance, faction + ": the west wall meets the gate.");
            Assert.AreEqual(gateBounds.max.x, eastBounds.min.x, Tolerance, faction + ": the gate meets the east wall.");

            // Closed, the leaf stands on the ground in the middle of the passage, inside the gate's own outline.
            var closed = Extent(leaf, null);
            Assert.AreEqual(0, closed.min.y, Tolerance, faction + ": the closed leaf stands on the ground.");
            Assert.AreEqual(GateX * .001f, closed.center.x, Tolerance, faction + ": the leaf is centred in the passage.");
            Assert.AreEqual(Row * .001f, closed.center.z, .1f, faction + ": the leaf stands in the wall's thickness.");
            Assert.Greater(closed.size.x, 1.2f, faction + ": the leaf fills the passage's width.");
            Assert.Greater(closed.size.y, 2.3f, faction + ": the leaf fills the passage's height.");
            Assert.IsTrue(gateBounds.Contains(closed.min + Vector3.one * .01f) && gateBounds.Contains(closed.max - Vector3.one * .01f), faction + ": the leaf is inside the gate.");

            Assert.IsTrue(world.Submit(new SetGateCommand(1, Gate, true)).Accepted, faction + ": the gate opens.");
            Step(world);
            var open = Extent(leaf, null);
            Assert.Greater(open.min.y, 2.3f, faction + ": open, the leaf is lifted clear of the 2.4 m passage.");
            Assert.LessOrEqual(open.max.y, gateBounds.max.y, faction + ": open, the leaf stays inside the gate's top.");
            Assert.AreEqual(closed.center.x, open.center.x, Tolerance, faction + ": the leaf rises straight up.");

            Assert.IsTrue(world.Submit(new SetGateCommand(1, Gate, false)).Accepted, faction + ": the gate closes.");
            Step(world);
            Assert.AreEqual(0, Extent(leaf, null).min.y, Tolerance, faction + ": closed again, the leaf is back on the ground.");
        }

        [TestCase(FactionKind.MirajSultanate)]
        [TestCase(FactionKind.SolarKingdom)]
        [TestCase(FactionKind.PirateBrotherhood)]
        public void FactionsWithoutASetOfTheirOwnHaveNoMeshyWallOrGate(FactionKind faction)
        {
            Assert.IsNull(MeshyBuildingVisuals.Resolve("wall", faction), faction + " wall");
            Assert.IsNull(MeshyBuildingVisuals.Resolve("gate", faction), faction + " gate");
            Assert.IsNull(MeshyBuildingVisuals.Resolve(MeshyBuildingVisuals.GateLeaf, faction), faction + " leaf");
        }

        // Each is drawn in a start of its own realm: factions of two realms cannot share a match.
        [TestCase("aven", "miraj")]
        [TestCase("ashen", "solar")]
        public void FactionsWithoutASetOfTheirOwnKeepTheProceduralWallAndGate(string start, string faction)
        {
            var world = CreateWorld(start, faction);
            Show(world);
            foreach (var (id, definition) in new[] { (WestWall, "wall"), (Gate, "gate") })
            {
                var model = view.RootFor(id)?.Find("Model");
                Assert.IsNotNull(model, faction + " " + definition + " is drawn.");
                Assert.IsNull(model.Find("Meshy " + definition), faction + " " + definition + " keeps the procedural art.");
                Assert.IsNotNull(model.GetComponentInChildren<LODGroup>(), faction + " " + definition + " has its procedural model.");
            }
            var procedural = view.RootFor(Gate).GetComponentsInChildren<Transform>();
            Assert.IsTrue(System.Array.Exists(procedural, part => part.name == "Portcullis"), faction + ": the procedural gate keeps its portcullis.");
        }

        private void Show(World world)
        {
            root = new GameObject("Fortification view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            view.Sync(1, None);
        }

        private void Step(World world)
        {
            world.Tick();
            view.Sync(1, None);
        }

        /// <summary>The Meshy model drawing this building, checked to be the owner culture's own.</summary>
        private Transform Meshy(int id, string definition, FactionKind faction)
        {
            var model = view.RootFor(id)?.Find("Model");
            Assert.IsNotNull(model, definition + " " + id + " is drawn.");
            var meshy = model.Find("Meshy " + definition);
            Assert.IsNotNull(meshy, faction + " " + definition + " is drawn with its Meshy model.");
            var entry = MeshyBuildingVisuals.Resolve(definition, faction);
            Assert.IsNotNull(meshy.Find(entry.id), faction + " " + definition + " is " + entry.id + ".");
            return meshy;
        }

        /// <summary>The world bounds of everything drawn under <paramref name="part"/>, leaving out <paramref name="except"/>.</summary>
        private static Bounds Extent(Transform part, Transform except)
        {
            Bounds? bounds = null;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>())
            {
                if (except != null && renderer.transform.IsChildOf(except)) continue;
                if (bounds is Bounds known) { known.Encapsulate(renderer.bounds); bounds = known; }
                else bounds = renderer.bounds;
            }
            Assert.IsTrue(bounds.HasValue, part.name + " has renderers.");
            return bounds.Value;
        }

        /// <summary>
        /// The faction's own start on amber_crossing (optionally redrawn as <paramref name="drawnAs"/>), plus a finished
        /// wall, gate and wall of player 1 side by side on the clear row north of its hearth, where the siege showcase
        /// builds its own.
        /// </summary>
        private static World CreateWorld(string faction, string drawnAs = null)
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, "amber_crossing");
            var map = baseline.Map;
            if (drawnAs != null) map.PlayerFactions[0].FactionId = drawnAs;
            map.BuildingSpawns = new List<BuildingSpawnDefinition>(map.BuildingSpawns)
            {
                new BuildingSpawnDefinition { Id = WestWall, OwnerId = 1, DefinitionId = "wall", Position = new SimPoint(WestX, Row) },
                new BuildingSpawnDefinition { Id = Gate, OwnerId = 1, DefinitionId = "gate", Position = new SimPoint(GateX, Row) },
                new BuildingSpawnDefinition { Id = EastWall, OwnerId = 1, DefinitionId = "wall", Position = new SimPoint(EastX, Row) },
            }.ToArray();
            return new World(baseline.Definition, map);
        }
    }
}
