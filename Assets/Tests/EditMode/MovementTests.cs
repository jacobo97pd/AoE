using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class MovementTests
    {
        [Test]
        public void OrdersDoNotAdvanceTimeOrMoveUnitsUntilTick()
        {
            var world = TestWorldFactory.Create();
            var unit = TestWorldFactory.Unit(world);
            var initial = unit.Position;
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(4500, 1500))).Accepted, Is.True);
            Assert.That(world.TickIndex, Is.Zero);
            Assert.That(unit.Position, Is.EqualTo(initial));
            Assert.That(unit.Order, Is.EqualTo(UnitOrder.Moving));
            world.Tick();
            Assert.That(world.TickIndex, Is.EqualTo(1));
            Assert.That(unit.PreviousPosition, Is.EqualTo(initial));
            Assert.That(unit.Position, Is.EqualTo(new SimPoint(1650, 1500)));
        }

        [Test]
        public void OneSecondMovementUsesConfiguredSpeedAndArrivesExactly()
        {
            var world = TestWorldFactory.Create();
            var destination = new SimPoint(4500, 1500);
            world.Submit(new MoveCommand(1, new[] { 1 }, destination));
            TestWorldFactory.Tick(world, World.TickRate);
            var unit = TestWorldFactory.Unit(world);
            Assert.That(unit.Position, Is.EqualTo(destination));
            Assert.That(unit.Order, Is.EqualTo(UnitOrder.Idle));
            TestWorldFactory.Tick(world, 100);
            Assert.That(unit.Position, Is.EqualTo(destination));
        }

        [Test]
        public void FractionalSpeedPerTickDoesNotLoseMovementOverOneSecond()
        {
            var world = TestWorldFactory.Create(3021);
            world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(13500, 1500)));
            TestWorldFactory.Tick(world, 20);
            Assert.That(TestWorldFactory.Unit(world).Position, Is.EqualTo(new SimPoint(4521, 1500)));
        }

        [Test]
        public void VerySlowDiagonalMovementEventuallyArrives()
        {
            var world = TestWorldFactory.Create(1);
            var destination = new SimPoint(1501, 1501);
            world.Submit(new MoveCommand(1, new[] { 1 }, destination));
            TestWorldFactory.Tick(world, 40);
            Assert.That(TestWorldFactory.Unit(world).Position, Is.EqualTo(destination));
            Assert.That(TestWorldFactory.Unit(world).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [Test]
        public void RepeatingMoveEveryTickDoesNotResetProgress()
        {
            var world = TestWorldFactory.Create();
            var destination = new SimPoint(4700, 3300);
            for (int i = 0; i < 120; i++)
            {
                Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, destination)).Accepted, Is.True);
                world.Tick();
            }
            Assert.That(TestWorldFactory.Unit(world).Position, Is.EqualTo(destination));
            Assert.That(TestWorldFactory.Unit(world).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [Test]
        public void StopPreservesPositionAcrossFutureTicks()
        {
            var world = TestWorldFactory.Create();
            world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(9500, 4500)));
            TestWorldFactory.Tick(world, 7);
            var stoppedAt = TestWorldFactory.Unit(world).Position;
            Assert.That(world.Submit(new StopCommand(1, new[] { 1 })).Accepted, Is.True);
            TestWorldFactory.Tick(world, 100);
            Assert.That(TestWorldFactory.Unit(world).Position, Is.EqualTo(stoppedAt));
            Assert.That(TestWorldFactory.Unit(world).Destination, Is.EqualTo(stoppedAt));
            Assert.That(TestWorldFactory.Unit(world).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [Test]
        public void PathDetoursAroundWallWithoutEnteringBlockedCells()
        {
            var map = TestWorldFactory.Map();
            map.BlockedCells = new GridCell[9];
            for (int z = 0; z < 9; z++) map.BlockedCells[z] = new GridCell(4, z);
            var world = new World(TestWorldFactory.Definitions(), map);
            var destination = new SimPoint(8500, 1500);
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, destination)).Accepted, Is.True);
            bool usedGap = false;
            for (int tick = 0; tick < 240; tick++)
            {
                world.Tick();
                var position = TestWorldFactory.Unit(world).Position;
                Assert.That(world.IsWalkable(position), Is.True, "Walked inside an obstacle at tick " + tick);
                // Smoothed routes need circle clearance above the wall, not the old BFS cell center.
                usedGap |= position.Z >= 9000 + TestWorldFactory.Unit(world).RadiusMillimetres;
            }
            Assert.That(usedGap, Is.True);
            Assert.That(TestWorldFactory.Unit(world).Position, Is.EqualTo(destination));
        }

        [Test]
        public void GroupDestinationsAreDistinctAndIndependentOfSelectionOrder()
        {
            var first = TestWorldFactory.Create();
            var second = TestWorldFactory.Create();
            var target = new SimPoint(8500, 4500);
            Assert.That(first.Submit(new MoveCommand(1, new[] { 2, 1 }, target)).Accepted, Is.True);
            Assert.That(second.Submit(new MoveCommand(1, new[] { 1, 2 }, target)).Accepted, Is.True);
            Assert.That(TestWorldFactory.Unit(first, 1).Destination, Is.Not.EqualTo(TestWorldFactory.Unit(first, 2).Destination));
            for (int tick = 0; tick < 120; tick++)
            {
                first.Tick(); second.Tick();
                for (int id = 1; id <= 3; id++)
                    Assert.That(TestWorldFactory.Unit(first, id).Position, Is.EqualTo(TestWorldFactory.Unit(second, id).Position));
            }
            Assert.That(TestWorldFactory.Unit(first, 1).Order, Is.EqualTo(UnitOrder.Idle));
            Assert.That(TestWorldFactory.Unit(first, 2).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [Test]
        public void IdenticalCommandReplayProducesIdenticalWorldStateAtEveryTick()
        {
            var a = TestWorldFactory.Create(3127);
            var b = TestWorldFactory.Create(3127);
            for (int tick = 0; tick < 300; tick++)
            {
                if (tick == 0 || tick == 77 || tick == 161)
                {
                    var command = new MoveCommand(1, new[] { 1, 2 }, new SimPoint(3500 + tick * 10, 4500));
                    Assert.That(a.Submit(command).Accepted, Is.EqualTo(b.Submit(command).Accepted));
                }
                if (tick == 43) { a.Submit(new StopCommand(1, new[] { 2 })); b.Submit(new StopCommand(1, new[] { 2 })); }
                a.Tick(); b.Tick();
                Assert.That(a.TickIndex, Is.EqualTo(b.TickIndex));
                for (int i = 0; i < a.Units.Count; i++)
                {
                    Assert.That(a.Units[i].Position, Is.EqualTo(b.Units[i].Position));
                    Assert.That(a.Units[i].Order, Is.EqualTo(b.Units[i].Order));
                }
            }
        }
    }
}
