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
    /// A wall or gate standing north-south is its own model turned a quarter, not one squeezed into the swapped
    /// footprint: a wall, gate and wall down one column meet end to end like the east-west line does, the gate's leaf
    /// still fills and lifts out of its passage, and the procedural fallback turns the same way. A ladder raised against
    /// such a wall leans on its east face, and the soldier who climbs it lands on the deck that now runs north-south.
    /// </summary>
    public sealed class TurnedFortificationTests
    {
        private const int SouthWall = 900, Gate = 901, NorthWall = 902, Plain = 903, Ladder = 904, Soldier = 905;
        // Column x20 of amber_crossing is clear from z29 to z38, north of the kingdom's hearth.
        private const int Column = 20500, SouthZ = 30500, GateZ = 33000, NorthZ = 35500;
        private const float Tolerance = .02f;
        private static readonly int[] None = new int[0];
        private GameObject root;
        private WorldView view;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            view?.Dispose(); view = null;
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [TestCase("aven")]
        [TestCase("verdant")]
        [TestCase("ashen")]
        public void ANorthSouthLineIsTheCulturesWallAndGateTurnedAndTheGateStillOpens(string faction)
        {
            var world = CreateWorld(faction, 1, true);
            Show(world);
            var kind = Kind(world);
            var south = Meshy(SouthWall, "wall", kind);
            var gate = Meshy(Gate, "gate", kind);
            var north = Meshy(NorthWall, "wall", kind);
            var plain = Meshy(Plain, "wall", kind);
            // Turned, not stretched: the art is the east-west segment's, at its scale, with its length running north.
            Assert.AreEqual(Holder(plain).localScale.x, Holder(south).localScale.x, .0001f, faction + ": a turned wall keeps the plain wall's fit.");
            Assert.AreEqual(Holder(plain).localScale.z, Holder(south).localScale.z, .0001f, faction + ": a turned wall keeps the plain wall's thickness.");
            Assert.AreEqual(1, Mathf.Abs(Vector3.Dot(south.right, Vector3.forward)), .001f, faction + ": the wall's length runs north-south.");
            Assert.AreEqual(1, Mathf.Abs(Vector3.Dot(gate.right, Vector3.forward)), .001f, faction + ": the gate's width runs north-south.");

            var leaf = gate.Find("Portcullis");
            Assert.IsNotNull(leaf, faction + ": the turned gate keeps its leaf where WorldView opens it.");
            var southBounds = Extent(south, null); var northBounds = Extent(north, null); var gateBounds = Extent(gate, leaf);
            foreach (var (name, bounds, z) in new[] { ("south wall", southBounds, SouthZ), ("north wall", northBounds, NorthZ) })
            {
                Assert.AreEqual(3, bounds.size.z, Tolerance, faction + ": the " + name + " takes its whole 3 m along Z.");
                Assert.LessOrEqual(bounds.size.x, 1 + Tolerance, faction + ": the " + name + " stays inside its footprint's width.");
                Assert.AreEqual(Column * .001f, bounds.center.x, Tolerance, faction + ": the " + name + " is centred on its column.");
                Assert.AreEqual(z * .001f, bounds.center.z, Tolerance, faction + ": the " + name + " is centred on its footprint.");
                Assert.AreEqual(AlphaWorldArt.BuildingHeight("wall"), bounds.max.y, Tolerance, faction + ": the " + name + " is as tall as the wall.");
            }
            Assert.AreEqual(2, gateBounds.size.z, Tolerance, faction + ": the gate takes its whole 2 m along Z.");
            Assert.AreEqual(southBounds.max.z, gateBounds.min.z, Tolerance, faction + ": the south wall meets the gate.");
            Assert.AreEqual(gateBounds.max.z, northBounds.min.z, Tolerance, faction + ": the gate meets the north wall.");

            var closed = Extent(leaf, null);
            Assert.AreEqual(0, closed.min.y, Tolerance, faction + ": the closed leaf stands on the ground.");
            Assert.AreEqual(GateZ * .001f, closed.center.z, Tolerance, faction + ": the leaf is centred in the passage.");
            Assert.AreEqual(Column * .001f, closed.center.x, .1f, faction + ": the leaf stands in the wall's thickness.");
            Assert.Greater(closed.size.z, 1.2f, faction + ": the leaf fills the passage's width, now north-south.");
            Assert.Less(closed.size.x, 1, faction + ": the leaf is thin across the passage.");
            Assert.IsTrue(world.Submit(new SetGateCommand(1, Gate, true)).Accepted);
            Step(world);
            var open = Extent(leaf, null);
            Assert.Greater(open.min.y, 2.3f, faction + ": open, the leaf is lifted clear of the passage.");
            Assert.AreEqual(closed.center.z, open.center.z, Tolerance, faction + ": the leaf rises straight up.");
            Assert.IsTrue(world.Submit(new SetGateCommand(1, Gate, false)).Accepted);
            Step(world);
            Assert.AreEqual(0, Extent(leaf, null).min.y, Tolerance, faction + ": closed again, the leaf is back on the ground.");
        }

        [Test]
        public void TheProceduralWallAndGateTurnTheSameWay()
        {
            var world = CreateWorld("aven", 1, true, "miraj");
            Show(world);
            foreach (var (id, definition, length) in new[] { (SouthWall, "wall", 3f), (Gate, "gate", 2f) })
            {
                var model = view.RootFor(id).Find("Model");
                Assert.IsNull(model.Find("Meshy " + definition), definition + " keeps the procedural art.");
                Assert.AreEqual(90, model.localEulerAngles.y, .01f, definition + ": the model turns a quarter.");
                var bounds = Extent(model, null);
                Assert.AreEqual(length, bounds.size.z, .1f, definition + " runs its length north-south.");
                Assert.Less(bounds.size.x, 1.05f, definition + " stays inside its column.");
            }
            Assert.IsTrue(System.Array.Exists(view.RootFor(Gate).GetComponentsInChildren<Transform>(), part => part.name == "Portcullis"));
            Assert.AreEqual(0, view.RootFor(Plain).Find("Model").localEulerAngles.y, .01f, "An east-west wall is not turned.");
        }

        [TestCase("aven")]
        [TestCase("verdant")]
        public void ALadderLeansOnANorthSouthWallsEastFaceAndTheSoldierLandsOnItsDeck(string faction)
        {
            var world = CreateWorld(faction, 2, false);
            root = new GameObject("Turned siege view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            Assert.IsTrue(world.TryGetBuilding(SouthWall, out var wall));
            Assert.IsTrue(wall.IsTurned);
            float wallHeight = 3;
            foreach (var definition in world.Definition.Buildings) if (definition.Id == "wall") wallHeight = definition.WallHeightMillimetres * .001f;
            // The ladder stands east of the wall; the face it leans on is the footprint's east edge.
            float face = (Column + wall.WidthCells * world.Map.CellSizeMillimetres / 2) * .001f;
            var ladderRoot = view.RootFor(Ladder);
            var model = ladderRoot.Find("Model");
            for (int tick = 0; tick < 24; tick++) Step(world);
            var raised = LadderExtent(model);
            Assert.GreaterOrEqual(raised.Highest.y, wallHeight, faction + ": raised, the ladder reaches the top of the wall.");
            Assert.Less(raised.Highest.x, ladderRoot.position.x, faction + ": its top leans west, toward the wall.");
            Assert.Greater(Vector3.Dot(model.up, Vector3.left), 0, faction + ": the model is tipped toward the wall.");
            Assert.GreaterOrEqual(raised.Westmost, face - .25f, faction + ": the ladder rests on the face instead of sinking into it.");

            var boarding = world.Submit(new BoardWallCommand(1, new[] { Soldier }, SouthWall, Ladder));
            Assert.IsTrue(boarding.Accepted, faction + ": " + boarding.Message);
            var soldier = view.RootFor(Soldier);
            bool onRungs = false;
            for (int tick = 0; tick < 200 && world.TryGetUnit(Soldier, out var unit) && unit.BoardingWallId != 0; tick++)
            {
                Step(world);
                var at = soldier.position;
                if (at.y > .6f && at.y < wallHeight - .3f)
                {
                    onRungs = true;
                    Assert.Greater(at.x, face, faction + ": mid-climb the soldier is on the ladder, outside the east face.");
                }
            }
            Assert.IsTrue(onRungs, faction + ": the soldier was seen on the rungs.");
            Assert.IsTrue(world.TryGetUnit(Soldier, out var boarded) && boarded.WallId == SouthWall, faction + ": the simulation put the soldier on the deck.");
            for (int tick = 0; tick < 20; tick++) Step(world);
            var slot = DefinitionLoader.ToWorld(boarded.Position);
            Assert.AreEqual(Column * .001f, slot.x, .001f, faction + ": the deck runs down the middle of the turned wall.");
            Assert.AreEqual(wallHeight, soldier.position.y, .05f, faction + ": on the deck.");
            Assert.AreEqual(slot.x, soldier.position.x, .05f, faction + ": drawn where the simulation put the soldier.");
            Assert.AreEqual(slot.z, soldier.position.z, .05f, faction + ": drawn where the simulation put the soldier.");
            Assert.That(slot.z, Is.InRange((SouthZ - 1500) * .001f, (SouthZ + 1500) * .001f), faction + ": the slot lies along the wall's length.");
        }

        private void Show(World world)
        {
            root = new GameObject("Turned fortification view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            view.Sync(1, None);
        }

        private void Step(World world)
        {
            world.Tick();
            view.Sync(.5f, None);
            view.Sync(1, None);
        }

        private static FactionKind Kind(World world)
        {
            Assert.IsTrue(world.TryGetPlayer(1, out var player));
            foreach (var definition in world.Definition.Factions) if (definition.Id == player.FactionId) return definition.Kind;
            return FactionKind.AvenCompact;
        }

        private Transform Meshy(int id, string definition, FactionKind faction)
        {
            var model = view.RootFor(id)?.Find("Model");
            Assert.IsNotNull(model, definition + " " + id + " is drawn.");
            var meshy = model.Find("Meshy " + definition);
            Assert.IsNotNull(meshy, faction + " " + definition + " is drawn with its Meshy model.");
            return meshy;
        }

        // The holder the fit is applied to, under the Meshy container (MeshyBuildingVisuals.Place).
        private static Transform Holder(Transform meshy)
        {
            foreach (Transform child in meshy) if (child.name != "Portcullis") return child;
            Assert.Fail(meshy.name + " has no model.");
            return null;
        }

        private static Bounds Extent(Transform part, Transform except)
        {
            Bounds? bounds = null;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>())
            {
                if (except != null && renderer.transform.IsChildOf(except)) continue;
                if (renderer is LineRenderer) continue;
                if (bounds is Bounds known) { known.Encapsulate(renderer.bounds); bounds = known; }
                else bounds = renderer.bounds;
            }
            Assert.IsTrue(bounds.HasValue, part.name + " has renderers.");
            return bounds.Value;
        }

        private static (Vector3 Highest, float Westmost) LadderExtent(Transform model)
        {
            var highest = new Vector3(0, float.MinValue, 0); float westmost = float.MaxValue;
            var vertices = new List<Vector3>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name == "LOD1" || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                filter.sharedMesh.GetVertices(vertices);
                foreach (var vertex in vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if (point.y > highest.y) highest = point;
                    westmost = Mathf.Min(westmost, point.x);
                }
            }
            Assert.Greater(highest.y, 0, "The ladder model has readable geometry.");
            return (highest, westmost);
        }

        /// <summary>
        /// The faction's own start on amber_crossing (optionally drawn as <paramref name="drawnAs"/>) with a finished
        /// wall of <paramref name="owner"/> turned north-south down column x20 and, for the line, a gate and a second
        /// wall above it plus a plain east-west wall to compare against; for the siege, player 1's ladder 0.7 m off the
        /// wall's east face and a spearman 1.2 m off it.
        /// </summary>
        private static World CreateWorld(string faction, int owner, bool line, string drawnAs = null)
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, "amber_crossing");
            var map = baseline.Map;
            if (drawnAs != null) map.PlayerFactions[0].FactionId = drawnAs;
            var buildings = new List<BuildingSpawnDefinition>(map.BuildingSpawns)
            {
                new BuildingSpawnDefinition { Id = SouthWall, OwnerId = owner, DefinitionId = "wall", Position = new SimPoint(Column, SouthZ), Turned = true },
            };
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            if (line)
            {
                buildings.Add(new BuildingSpawnDefinition { Id = Gate, OwnerId = owner, DefinitionId = "gate", Position = new SimPoint(Column, GateZ), Turned = true });
                buildings.Add(new BuildingSpawnDefinition { Id = NorthWall, OwnerId = owner, DefinitionId = "wall", Position = new SimPoint(Column, NorthZ), Turned = true });
                buildings.Add(new BuildingSpawnDefinition { Id = Plain, OwnerId = owner, DefinitionId = "wall", Position = new SimPoint(26500, SouthZ) });
            }
            else
            {
                units.Add(new UnitSpawnDefinition { Id = Ladder, OwnerId = 1, DefinitionId = "siege_ladder", Position = new SimPoint(Column + 1200, SouthZ) });
                units.Add(new UnitSpawnDefinition { Id = Soldier, OwnerId = 1, DefinitionId = "reedguard", Position = new SimPoint(Column + 1700, SouthZ - 1200) });
            }
            map.BuildingSpawns = buildings.ToArray();
            map.UnitSpawns = units.ToArray();
            return new World(baseline.Definition, map);
        }
    }
}
