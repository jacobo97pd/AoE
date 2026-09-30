using System;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class NetworkSiegeObservationTests
    {
        private static World Create(bool oilTarget = false) => new World(new GameDefinition {
            Units = new[] { new UnitDefinition { Id = "archer", Tags = CombatTags.Infantry } },
            Buildings = new[] {
                new BuildingDefinition { Id = "hearth", WidthCells = 1, DepthCells = 1 },
                new BuildingDefinition { Id = "wall", IsWall = true, WidthCells = 3, DepthCells = 1, OilDamage = oilTarget ? 10 : 0, OilCooldownTicks = 60 },
                new BuildingDefinition { Id = "gate", IsGate = true, WidthCells = 2, DepthCells = 1 } }
        }, new MapDefinition {
            Id = "network-siege", WidthCells = 24, HeightCells = 16,
            UnitSpawns = oilTarget ? new[] {
                new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "archer", Position = new SimPoint(7500, 4500) },
                new UnitSpawnDefinition { Id = 2, OwnerId = 2, DefinitionId = "archer", Position = new SimPoint(7500, 7800) } }
                : new[] { new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "archer", Position = new SimPoint(7500, 4500) } },
            BuildingSpawns = new[] {
                new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "wall", Position = new SimPoint(7500, 6500) },
                new BuildingSpawnDefinition { Id = 101, OwnerId = 1, DefinitionId = "gate", Position = new SimPoint(16000, 6500) },
                new BuildingSpawnDefinition { Id = 102, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(1500, 1500) },
                new BuildingSpawnDefinition { Id = 103, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(22500, 14500) } },
            OfflineMatch = new OfflineMatchDefinition { Enabled = true, VisionUnitCells = 3, VisionBuildingCells = 3 }
        });

        [Test]
        public void ReplicaShowsBoardingDeckHeightAndOpenedGateWithoutSimulatingEither()
        {
            var world = Create();
            Assert.That(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100)).Accepted, Is.True);
            world.Tick();
            var boarding = NetworkObservation.Export(world, 1, 1);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, boarding);
            Assert.That(replica.TryGetUnit(1, out var unit), Is.True);
            Assert.That(unit.BoardingWallId, Is.EqualTo(100)); Assert.That(unit.BoardingRemainingTicks, Is.EqualTo(39));
            for (int i = 1; i < 40; i++) world.Tick();
            Assert.That(world.Submit(new SetGateCommand(1, 101, true)).Accepted, Is.True);
            replica.ApplyNetworkSnapshot(NetworkObservation.Export(world, 1, 2));
            Assert.That(replica.TryGetUnit(1, out unit), Is.True);
            Assert.That(unit.WallId, Is.EqualTo(100)); Assert.That(unit.ElevationMillimetres, Is.EqualTo(3000));
            Assert.That(replica.TryGetBuilding(101, out var gate), Is.True); Assert.That(gate.GateOpen, Is.True);
            Assert.That(replica.IsWalkable(new SimPoint(15500, 6500)), Is.True);
            Assert.That(replica.IsWalkable(new SimPoint(7500, 6500)), Is.False);
            replica.Tick(); Assert.That(unit.WallId, Is.EqualTo(100)); Assert.That(replica.TickIndex, Is.EqualTo(40));
        }

        [TestCase(1)] [TestCase(2)]
        public void ActualOilAttackExportsVisibleCooldownForEitherParticipant(int recipient)
        {
            var world = Create(true); world.Tick();
            Assert.IsTrue(world.TryGetUnit(2, out var target)); Assert.AreEqual(90, target.Health);
            var snapshot = NetworkObservation.Export(world, recipient, 1);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, snapshot);
            Assert.IsTrue(replica.TryGetBuilding(100, out var wall)); Assert.AreEqual(60, wall.OilCooldownTicks);
            var corrupt = NetworkObservation.Export(world, recipient, 2);
            foreach (var building in corrupt.Buildings) if (building.Id == 100) building.OilCooldownTicks = -1;
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(corrupt));
            Assert.AreEqual(1, replica.NetworkSnapshotSequence); Assert.AreEqual(60, wall.OilCooldownTicks);
        }

        [Test]
        public void InvalidObservedWallReferenceCannotReplaceAValidReplica()
        {
            var world = Create();
            Assert.That(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100)).Accepted, Is.True);
            for (int i = 0; i < 40; i++) world.Tick();
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var corrupt = NetworkObservation.Export(world, 1, 2); corrupt.Units[0].WallId = 999;
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(corrupt));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
        }
    }
}
