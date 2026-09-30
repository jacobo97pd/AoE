using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// A ladder parked beside an enemy wall is drawn leaning on it, and a soldier boarding that wall goes up the
    /// ladder's rungs instead of rising straight through the stone. The simulation's own boarding decides when the
    /// climb starts and ends; these tests watch the drawing follow it, for every culture's ladder and climber.
    /// </summary>
    public sealed class SiegeLadderVisualsTests
    {
        private const int Wall = 900, Ladder = 901, Soldier = 902;
        private const int WallX = 20500, WallZ = 30500;
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

        [TestCase("aven")]
        [TestCase("skeld")]
        [TestCase("drakeforged")]
        [TestCase("verdant")]
        [TestCase("ashen")]
        [TestCase("sultanate")]
        [TestCase("sahel")]
        public void ALadderLeansOnTheEnemyWallAndTheSoldierClimbsItsRungs(string faction)
        {
            var world = CreateWorld(faction);
            root = new GameObject("Siege ladder view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            Assert.IsTrue(world.TryGetBuilding(Wall, out var wall));
            float wallHeight = 3;
            foreach (var definition in world.Definition.Buildings) if (definition.Id == "wall") wallHeight = definition.WallHeightMillimetres * .001f;
            // The ladder stands on the wall's north side; the face it leans on is the footprint's north edge.
            float face = (WallZ + wall.DepthCells * world.Map.CellSizeMillimetres / 2) * .001f;
            var ladderRoot = view.RootFor(Ladder);
            Assert.IsNotNull(ladderRoot);
            var model = ladderRoot.Find("Model");
            Assert.Less(Extent(model).Highest.y, wallHeight, faction + ": stowed, the ladder is shorter than the wall.");

            for (int tick = 0; tick < 24; tick++) Step(world);
            var raised = Extent(model);
            Assert.GreaterOrEqual(raised.Highest.y, wallHeight, faction + ": raised, the ladder reaches the top of the wall.");
            Assert.Less(raised.Highest.z, ladderRoot.position.z, faction + ": its top leans toward the wall, not away from it.");
            // The kingdom's sled already stands with its front at the face and its top over it, so it tips only a few degrees.
            Assert.Greater(Vector3.Dot(model.up, Vector3.back), 0, faction + ": the model is tipped toward the wall.");
            Assert.GreaterOrEqual(raised.Nearest, face - .25f, faction + ": the ladder rests on the face instead of sinking into it.");
            Assert.GreaterOrEqual(raised.Lowest, -.02f, faction + ": tipping about the base's front edge keeps it out of the ground.");

            var boarding = world.Submit(new BoardWallCommand(1, new[] { Soldier }, Wall, Ladder));
            Assert.IsTrue(boarding.Accepted, faction + ": " + boarding.Message);
            var soldier = view.RootFor(Soldier);
            var character = soldier.GetComponentInChildren<CorsairAnimationDriver>();
            bool climbs = character != null && character.HasState("Climb");
            int climbHash = Animator.StringToHash("Climb");
            bool onRungs = false, sawClimb = false; float previous = 0;
            for (int tick = 0; tick < 200 && world.TryGetUnit(Soldier, out var unit) && unit.BoardingWallId != 0; tick++)
            {
                Step(world);
                var at = soldier.position;
                if (at.y > .6f && at.y < wallHeight - .3f)
                {
                    // The straight lift would have crossed the face by now; on the ladder the soldier stays outside it.
                    onRungs = true;
                    Assert.Greater(at.z, face, faction + ": mid-climb the soldier is on the ladder, outside the wall.");
                    Assert.GreaterOrEqual(at.y, previous - .02f, faction + ": the climb only goes up.");
                    var marker = soldier.Find("Owner marker");
                    if (marker != null) Assert.IsFalse(marker.gameObject.activeSelf, faction + ": the ground ring does not hang in the air on the rungs.");
                    if (climbs) sawClimb |= character.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == climbHash;
                }
                previous = at.y;
            }
            Assert.IsTrue(world.TryGetUnit(Soldier, out var boarded) && boarded.WallId == Wall, faction + ": the simulation put the soldier on the deck.");
            Assert.IsTrue(onRungs, faction + ": the soldier was seen between the ground and the deck.");
            if (climbs) Assert.IsTrue(sawClimb, faction + ": the model's own ladder climb played on the rungs.");

            for (int tick = 0; tick < 20; tick++) Step(world);
            Assert.AreEqual(wallHeight, soldier.position.y, .05f, faction + ": on the deck.");
            var deckMarker = soldier.Find("Owner marker");
            if (deckMarker != null) Assert.IsTrue(deckMarker.gameObject.activeSelf, faction + ": on the deck the owner ring is back at the feet.");
            if (climbs)
                Assert.AreNotEqual(climbHash, character.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash, faction + ": the driver has its own states back.");

            // Driven off, the ladder lowers onto its base again.
            Assert.IsTrue(world.Submit(new MoveCommand(1, new[] { Ladder }, new SimPoint(WallX, WallZ + 3500), MovementFormation.Loose)).Accepted);
            for (int tick = 0; tick < 30; tick++) Step(world);
            Assert.Less(Extent(model).Highest.y, wallHeight - .4f, faction + ": on the move the ladder is stowed.");
        }

        [Test]
        public void ALadderBesideItsOwnSidesWallStaysOnItsBase()
        {
            var world = CreateWorld("aven", 1);
            root = new GameObject("Siege ladder view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            var model = view.RootFor(Ladder).Find("Model");
            float stowed = Extent(model).Highest.y;
            for (int tick = 0; tick < 24; tick++) Step(world);
            Assert.AreEqual(stowed, Extent(model).Highest.y, .01f, "Ladders are raised only against enemy walls, as the simulation boards them.");
        }

        /// <summary>
        /// The impact flash is a flat disc at a unit's feet, meant to mark a blow just taken. Boarding a ladder used
        /// to hide it only once the climber's feet cleared y .05, so a soldier walking up to the ladder, or still
        /// waiting at its foot, showed the disc at full size on the ground - in any match, not only the film. It
        /// must stay hidden through the whole boarding walk and climb, and come back once the soldier is off the
        /// ladder and takes an ordinary hit.
        /// </summary>
        [Test]
        public void TheImpactFlashStaysHiddenWhileBoardingAndReturnsOnceOffTheLadder()
        {
            var world = CreateWorld("aven");
            root = new GameObject("Siege ladder view");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            view = new WorldView(world, root.transform, camera);
            for (int tick = 0; tick < 24; tick++) Step(world); // Raise the ladder against the wall first.

            var boarding = world.Submit(new BoardWallCommand(1, new[] { Soldier }, Wall, Ladder));
            Assert.IsTrue(boarding.Accepted, boarding.Message);
            var soldier = view.RootFor(Soldier);
            var flash = soldier.Find("Impact");
            Assert.IsNotNull(flash);
            var visuals = (System.Collections.IDictionary)typeof(WorldView).GetField("visuals", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

            bool sawWaitingAtTheFoot = false, sawOnTheRungs = false, stillBoarding = true;
            for (int tick = 0; tick < 200 && stillBoarding; tick++)
            {
                world.Tick();
                stillBoarding = world.TryGetUnit(Soldier, out var unit) && unit.BoardingWallId != 0;
                // Stand in for a blow just landed (boiling oil, an archer on the wall) without waiting on real time.
                var visual = visuals[Soldier];
                visual.GetType().GetField("HitUntil").SetValue(visual, Time.unscaledTime + .15f);
                view.Sync(.5f, None);
                view.Sync(1, None);
                // The tick that lands the soldier on the deck also releases the ladder pose; the ordinary flash
                // is expected to be back from here on, and is checked once the loop ends below.
                if (!stillBoarding) continue;
                if (soldier.position.y <= .05f) sawWaitingAtTheFoot = true; else sawOnTheRungs = true;
                Assert.IsFalse(flash.gameObject.activeSelf, "y=" + soldier.position.y + ": a hit while boarding must not show the flash.");
            }
            Assert.IsTrue(sawWaitingAtTheFoot, "The soldier must be seen walking to or waiting at the ladder's foot, at ground height.");
            Assert.IsTrue(sawOnTheRungs, "The soldier must be seen climbing, above the ladder's foot.");
            Assert.IsTrue(world.TryGetUnit(Soldier, out var boarded) && boarded.WallId == Wall, "The soldier reached the deck.");
            Assert.IsTrue(flash.gameObject.activeSelf, "Off the ladder, an ordinary hit must still show the flash.");
        }

        private void Step(World world)
        {
            world.Tick();
            view.Sync(.5f, None);
            view.Sync(1, None);
        }

        /// <summary>The model's near-level vertices in the world: highest point, lowest height and the extent toward the wall.</summary>
        private static (Vector3 Highest, float Lowest, float Nearest) Extent(Transform model)
        {
            var highest = new Vector3(0, float.MinValue, 0); float lowest = float.MaxValue, nearest = float.MaxValue;
            var vertices = new List<Vector3>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name == "LOD1" || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                filter.sharedMesh.GetVertices(vertices);
                foreach (var vertex in vertices)
                {
                    var point = filter.transform.TransformPoint(vertex);
                    if (point.y > highest.y) highest = point;
                    lowest = Mathf.Min(lowest, point.y); nearest = Mathf.Min(nearest, point.z);
                }
            }
            Assert.Greater(highest.y, 0, "The ladder model has readable geometry.");
            return (highest, lowest, nearest);
        }

        /// <summary>
        /// The faction's own start on amber_crossing, plus a finished wall of <paramref name="wallOwner"/> on the clear
        /// row north of the kingdom's hearth, one of player 1's ladders parked 0.7 m off its face and a spearman
        /// 1.2 m off it, a metre to the side.
        /// </summary>
        private static World CreateWorld(string faction, int wallOwner = 2)
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, "amber_crossing");
            var map = baseline.Map;
            map.BuildingSpawns = new List<BuildingSpawnDefinition>(map.BuildingSpawns)
            {
                new BuildingSpawnDefinition { Id = Wall, OwnerId = wallOwner, DefinitionId = "wall", Position = new SimPoint(WallX, WallZ) },
            }.ToArray();
            map.UnitSpawns = new List<UnitSpawnDefinition>(map.UnitSpawns)
            {
                new UnitSpawnDefinition { Id = Ladder, OwnerId = 1, DefinitionId = "siege_ladder", Position = new SimPoint(WallX, WallZ + 1200) },
                new UnitSpawnDefinition { Id = Soldier, OwnerId = 1, DefinitionId = "reedguard", Position = new SimPoint(WallX - 1200, WallZ + 1700) },
            }.ToArray();
            return new World(baseline.Definition, map);
        }
    }
}
