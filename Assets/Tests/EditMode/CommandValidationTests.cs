using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class CommandValidationTests
    {
        [Test]
        public void MixedOwnershipRejectsWholeGroupWithoutMutatingExistingOrders()
        {
            var world = TestWorldFactory.Create();
            var original = new SimPoint(4500, 1500);
            world.Submit(new MoveCommand(1, new[] { 1 }, original));
            var result = world.Submit(new MoveCommand(1, new[] { 1, 3 }, new SimPoint(3500, 3500)));
            Assert.That(result.Reason, Is.EqualTo(CommandRejection.NotOwner));
            Assert.That(TestWorldFactory.Unit(world, 1).Destination, Is.EqualTo(original));
            Assert.That(TestWorldFactory.Unit(world, 3).Order, Is.EqualTo(UnitOrder.Idle));
            Assert.That(world.Submit(new StopCommand(1, new[] { 1, 3 })).Reason, Is.EqualTo(CommandRejection.NotOwner));
            Assert.That(TestWorldFactory.Unit(world, 1).Order, Is.EqualTo(UnitOrder.Moving));
        }

        [TestCase(-1, 1500)]
        [TestCase(16000, 1500)]
        [TestCase(1500, 12000)]
        [TestCase(1500, -1)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void OutOfBoundsDestinationIsRejected(int x, int z)
        {
            var world = TestWorldFactory.Create();
            var result = world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(x, z)));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo(CommandRejection.InvalidTarget));
            Assert.That(TestWorldFactory.Unit(world).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [TestCase(11500, 8500)]
        [TestCase(6500, 6500)]
        [TestCase(100, 1500)]
        [TestCase(9900, 8500)]
        public void BuildingResourceAndInsufficientClearanceTargetsAreRejected(int x, int z)
        {
            var world = TestWorldFactory.Create();
            var result = world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(x, z)));
            Assert.That(result.Reason, Is.EqualTo(CommandRejection.DestinationBlocked));
        }

        [Test]
        public void DisconnectedDestinationDoesNotReplaceExistingOrder()
        {
            var map = TestWorldFactory.Map();
            map.BlockedCells = new GridCell[map.HeightCells];
            for (int z = 0; z < map.HeightCells; z++) map.BlockedCells[z] = new GridCell(4, z);
            var world = new World(TestWorldFactory.Definitions(), map);
            var original = new SimPoint(3500, 1500);
            world.Submit(new MoveCommand(1, new[] { 1 }, original));
            var result = world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(8500, 1500)));
            Assert.That(result.Reason, Is.EqualTo(CommandRejection.NoPath));
            Assert.That(TestWorldFactory.Unit(world).Destination, Is.EqualTo(original));
        }

        [Test]
        public void GroupOrderRejectsAtomicallyIfAnyUnitCannotReachTargetRegion()
        {
            var map = TestWorldFactory.Map();
            map.BlockedCells = new GridCell[map.HeightCells];
            for (int z = 0; z < map.HeightCells; z++) map.BlockedCells[z] = new GridCell(4, z);
            map.UnitSpawns[1].Position = new SimPoint(8500, 1500);
            var world = new World(TestWorldFactory.Definitions(), map);
            var result = world.Submit(new MoveCommand(1, new[] { 1, 2 }, new SimPoint(3500, 3500)));
            Assert.That(result.Reason, Is.EqualTo(CommandRejection.NoPath));
            Assert.That(TestWorldFactory.Unit(world, 1).Order, Is.EqualTo(UnitOrder.Idle));
            Assert.That(TestWorldFactory.Unit(world, 2).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [Test]
        public void InvalidSelectionsAndUnknownCommandTypesHaveExplicitRejections()
        {
            var world = TestWorldFactory.Create();
            Assert.That(world.Submit(null).Reason, Is.EqualTo(CommandRejection.InvalidCommand));
            Assert.That(world.Submit(new UnsupportedCommand()).Reason, Is.EqualTo(CommandRejection.InvalidCommand));
            Assert.That(world.Submit(new StopCommand(0, new[] { 1 })).Reason, Is.EqualTo(CommandRejection.InvalidPlayer));
            Assert.That(world.Submit(new StopCommand(1, null)).Reason, Is.EqualTo(CommandRejection.EmptySelection));
            Assert.That(world.Submit(new StopCommand(1, new[] { 999 })).Reason, Is.EqualTo(CommandRejection.UnknownUnit));
            Assert.That(world.Submit(new StopCommand(1, new[] { 1, 1 })).Reason, Is.EqualTo(CommandRejection.DuplicateUnit));
            Assert.That(world.Submit(new StopCommand(1, new int[1025])).Reason, Is.EqualTo(CommandRejection.TooManyUnits));
        }

        [Test]
        public void CommandCopiesSelectionSoCallerCannotChangeItsOwnershipScope()
        {
            var world = TestWorldFactory.Create();
            var ids = new[] { 1 };
            var command = new MoveCommand(1, ids, new SimPoint(3500, 1500));
            ids[0] = 3;
            Assert.That(world.Submit(command).Accepted, Is.True);
            Assert.That(TestWorldFactory.Unit(world, 1).Order, Is.EqualTo(UnitOrder.Moving));
            Assert.That(TestWorldFactory.Unit(world, 3).Order, Is.EqualTo(UnitOrder.Idle));
        }

        private sealed class UnsupportedCommand : IGameCommand { public int PlayerId => 1; }
    }
}
