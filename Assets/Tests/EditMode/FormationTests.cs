using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public class FormationTests
    {
        private static readonly SimPoint Target = new SimPoint(32500, 18500);

        [Test]
        public void OpenLineFormsOnePerpendicularRowAndSettlesSafely()
        {
            var world = Create();
            Accepted(world.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Line)));
            var goals = Goals(world);
            var zValues = new HashSet<int>();
            foreach (var unit in world.Units)
            {
                Assert.AreEqual(MovementFormation.Line, unit.Formation);
                Assert.AreEqual(Target.X, unit.Destination.X, "Eastward travel puts the lateral line along Z.");
                zValues.Add(unit.Destination.Z);
            }
            Assert.AreEqual(8, zValues.Count);
            Finish(world, goals);
        }

        [Test]
        public void OpenBoxFormsNearSquareGridAndSettlesSafely()
        {
            var world = Create();
            Accepted(world.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Box)));
            var xValues = new HashSet<int>(); var zValues = new HashSet<int>();
            foreach (var unit in world.Units)
            {
                Assert.AreEqual(MovementFormation.Box, unit.Formation);
                xValues.Add(unit.Destination.X); zValues.Add(unit.Destination.Z);
            }
            Assert.AreEqual(3, xValues.Count, "Eight units use a three-column near-square box.");
            Assert.AreEqual(3, zValues.Count);
            Finish(world, Goals(world));
        }

        [Test]
        public void ChangingFormationAtIdenticalRawTargetReassignsDestinations()
        {
            var world = Create();
            Accepted(world.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Line)));
            var lineGoals = Goals(world);
            for (int tick = 0; tick < 5; tick++) Step(world);
            Accepted(world.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Box)));
            bool changed = false;
            foreach (var unit in world.Units)
            {
                Assert.AreEqual(MovementFormation.Box, unit.Formation);
                changed |= unit.Destination != lineGoals[unit.Id];
            }
            Assert.IsTrue(changed, "Formation mode must participate in repeated-order identity.");
            Finish(world, Goals(world));
        }

        [Test]
        public void ObstructedPreferredLineFallsBackSafelyAndReplaysByStableIds()
        {
            var obstacle = new[] { new GridCell(32, 17) }; // Intersects one preferred line slot; the clicked cell remains clear.
            var a = Create(blocked: obstacle); var b = Create(reverseSpawns: true, blocked: obstacle);
            Accepted(a.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Line)));
            var reversedIds = Ids(8); Array.Reverse(reversedIds);
            Accepted(b.Submit(new MoveCommand(1, reversedIds, Target, MovementFormation.Line)));
            bool usedFallback = false;
            foreach (var unit in a.Units)
            {
                usedFallback |= unit.Destination.X != Target.X;
                Assert.IsTrue(a.IsWalkable(unit.Destination));
            }
            Assert.IsTrue(usedFallback, "The blocked preferred shape needs nearby free slots.");
            FinishReplay(a, b, Goals(a));
        }

        [Test]
        public void InvalidFormationRejectsAtomicallyWithoutChangingActiveRoutes()
        {
            var a = Create(); var b = Create();
            Accepted(a.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Line)));
            Accepted(b.Submit(new MoveCommand(1, Ids(8), Target, MovementFormation.Line)));
            for (int tick = 0; tick < 20; tick++) { Step(a); Step(b); }
            var goals = Goals(a);
            var result = a.Submit(new MoveCommand(1, Ids(8), new SimPoint(12500, 8500), (MovementFormation)999));
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CommandRejection.InvalidCommand, result.Reason);
            foreach (var unit in a.Units)
            {
                Assert.AreEqual(goals[unit.Id], unit.Destination);
                Assert.AreEqual(MovementFormation.Line, unit.Formation);
            }
            FinishReplay(a, b, goals);
        }

        [Test]
        public void RepeatedLooseOrdersPreserveAlreadySpacedFormationExtent()
        {
            var world = Create(4);
            foreach (var unit in world.Units) Assert.AreEqual(MovementFormation.Loose, unit.Formation);
            Accepted(world.Submit(new MoveCommand(1, Ids(4), Target)));
            Finish(world, Goals(world));
            var firstExtent = Extent(world);
            foreach (var target in new[] { new SimPoint(14500, 18500), new SimPoint(34500, 24500), new SimPoint(16500, 13500) })
            {
                Accepted(world.Submit(new MoveCommand(1, Ids(4), target)));
                var extent = DestinationExtent(world);
                Assert.That(extent.X, Is.EqualTo(firstExtent.X).Within(4), "Fresh Loose orders must not repeatedly multiply spacing.");
                Assert.That(extent.Z, Is.EqualTo(firstExtent.Z).Within(4));
                Finish(world, Goals(world));
            }
        }

        private static World Create(int count = 8, bool reverseSpawns = false, GridCell[] blocked = null)
        {
            var spawns = new UnitSpawnDefinition[count];
            int rows = count / 2;
            for (int i = 0; i < count; i++)
                spawns[i] = new UnitSpawnDefinition
                {
                    Id = i + 1, OwnerId = 1, DefinitionId = "mover",
                    Position = new SimPoint(5500 + i % 2 * 1000, 18500 - (rows - 1) * 500 + i / 2 * 1000)
                };
            if (reverseSpawns) Array.Reverse(spawns);
            return new World(new GameDefinition
            {
                BasePopulationCapacity = 20,
                Units = new[] { new UnitDefinition { Id = "mover", RadiusMillimetres = 300, MoveSpeedMillimetresPerSecond = 3200 } }
            }, new MapDefinition
            {
                Id = "formation-test", WidthCells = 48, HeightCells = 36,
                UnitSpawns = spawns, BlockedCells = blocked ?? Array.Empty<GridCell>()
            });
        }

        private static int[] Ids(int count)
        {
            var ids = new int[count]; for (int i = 0; i < count; i++) ids[i] = i + 1; return ids;
        }

        private static Dictionary<int, SimPoint> Goals(World world)
        {
            var result = new Dictionary<int, SimPoint>();
            foreach (var unit in world.Units) result.Add(unit.Id, unit.Destination);
            return result;
        }

        private static bool Settled(World world, Dictionary<int, SimPoint> goals)
        {
            foreach (var unit in world.Units)
                if (unit.Order != UnitOrder.Idle || unit.Position != goals[unit.Id]) return false;
            return true;
        }

        private static void Finish(World world, Dictionary<int, SimPoint> goals)
        {
            for (int tick = 0; tick < World.TickRate * 120 && !Settled(world, goals); tick++) Step(world);
            Assert.IsTrue(Settled(world, goals), "Every unit must reach its assigned position and settle Idle.");
            GroupMovementTests.AssertSafe(world);
        }

        private static void FinishReplay(World a, World b, Dictionary<int, SimPoint> goals)
        {
            for (int tick = 0; tick < World.TickRate * 120 && !Settled(a, goals); tick++)
            {
                Step(a); Step(b);
                foreach (var unit in a.Units)
                {
                    Assert.IsTrue(b.TryGetUnit(unit.Id, out var other));
                    Assert.AreEqual(unit.Position, other.Position);
                    Assert.AreEqual(unit.Destination, other.Destination);
                    Assert.AreEqual(unit.Order, other.Order);
                }
            }
            Assert.IsTrue(Settled(a, goals)); Assert.IsTrue(Settled(b, goals));
        }

        private static SimPoint Extent(World world) => Extent(world, false);
        private static SimPoint DestinationExtent(World world) => Extent(world, true);
        private static SimPoint Extent(World world, bool destinations)
        {
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var unit in world.Units)
            {
                var p = destinations ? unit.Destination : unit.Position;
                minX = Math.Min(minX, p.X); minZ = Math.Min(minZ, p.Z); maxX = Math.Max(maxX, p.X); maxZ = Math.Max(maxZ, p.Z);
            }
            return new SimPoint(maxX - minX, maxZ - minZ);
        }

        private static void Step(World world) { world.Tick(); GroupMovementTests.AssertSafe(world); }
        private static void Accepted(CommandResult result) => Assert.IsTrue(result.Accepted, result.Message);
    }
}
