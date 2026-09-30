using System;
using System.Linq;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.OfflineMatchTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class NetworkObservationTests
    {
        [TestCase("realm")] [TestCase("biome")]
        public void ChangingCompetitiveContentInSnapshotIsRejectedWithoutReplacingTheReplica(string field)
        {
            var world = Create(); var initial = NetworkObservation.Export(world, 1, 1);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, initial);
            var next = NetworkObservation.Export(world, 1, 2);
            if (field == "realm") next.RealmId = "fantasy"; else next.BiomeId = "desert";
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(next));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
            Assert.That(replica.Map.RealmId, Is.EqualTo("historical"));
            Assert.That(replica.Map.BiomeId, Is.EqualTo("forest"));
        }

        [Test]
        public void InitialObservationContainsOnlyOwnedAndCurrentlyVisibleEntities()
        {
            var world = Create(); var snapshot = NetworkObservation.Export(world, 1, 1);
            Assert.That(snapshot.Units.Select(x => x.Id), Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(snapshot.Buildings.Select(x => x.Id), Is.EquivalentTo(new[] { 100 }));
            Assert.That(snapshot.Resources.Select(x => x.Id), Is.EquivalentTo(new[] { 201 }));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, snapshot);
            Assert.That(replica.Units.Select(x => x.Id), Is.EquivalentTo(snapshot.Units.Select(x => x.Id)));
            Assert.That(replica.TryGetUnit(4, out _), Is.False);
            Assert.That(replica.TryGetBuilding(101, out _), Is.False);
            Assert.That(replica.TryGetResource(202, out _), Is.False);
            Assert.That(replica.Map.UnitSpawns, Is.Empty); Assert.That(replica.Map.BuildingSpawns, Is.Empty); Assert.That(replica.Map.ResourceSpawns, Is.Empty);
            Assert.That(world.Map.UnitSpawns.Length, Is.EqualTo(5), "Replica creation does not mutate installed map data.");
            Assert.That(replica.TryGetPlayer(2, out var opponent), Is.True);
            Assert.That(opponent.Resources, Is.EqualTo(default(ResourceAmount))); Assert.That(opponent.PopulationUsed, Is.Zero);
        }

        [TestCase(1)] [TestCase(2)]
        public void EveryVisionCellMatchesOnlyTheRecipientsMask(int recipient)
        {
            var world = Create(); var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, recipient, 1));
            for (int z = 0; z < world.Map.HeightCells; z++) for (int x = 0; x < world.Map.WidthCells; x++)
            {
                var point = new SimPoint(x * 1000 + 500, z * 1000 + 500);
                Assert.That(replica.Vision.IsVisible(1, point), Is.EqualTo(world.Vision.IsVisible(recipient, point)), point.ToString());
                Assert.That(replica.Vision.IsExplored(1, point), Is.EqualTo(world.Vision.IsExplored(recipient, point)), point.ToString());
                Assert.That(replica.Vision.IsVisible(2, point), Is.False); Assert.That(replica.Vision.IsExplored(2, point), Is.False);
            }
        }

        [Test]
        public void SecondSeatBecomesLocalOneWithStableEntityIdsAndNormalizedWinner()
        {
            var world = Create(); Accepted(world.Submit(new SurrenderCommand(1)));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 2, 1));
            Assert.That(replica.TryGetUnit(4, out var own), Is.True); Assert.That(own.OwnerId, Is.EqualTo(1));
            Assert.That(replica.TryGetBuilding(101, out var hearth), Is.True); Assert.That(hearth.OwnerId, Is.EqualTo(1));
            Assert.That(replica.Match.IsFinished, Is.True); Assert.That(replica.Match.WinnerId, Is.EqualTo(1));
            Assert.That(replica.Match.Reason, Is.EqualTo(MatchEndReason.Surrender));
            Assert.That(replica.NetworkServerPlayerId, Is.EqualTo(2));
        }

        [Test]
        public void ReplicaNeverSimulatesAndSubmissionOnlyInvokesItsTransport()
        {
            var world = Create(); var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var original = Unit(replica, 2).Position; var inventory = Player(replica).Resources;
            Assert.That(replica.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(10500, 12500))).Accepted, Is.False);
            int sent = 0;
            replica.NetworkCommandSink = command => { sent++; Assert.That(command, Is.TypeOf<MoveCommand>()); return NetworkCommandResult.Queued(); };
            Accepted(replica.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(10500, 12500))));
            Tick(replica, 500);
            Assert.That(sent, Is.EqualTo(1)); Assert.That(replica.TickIndex, Is.Zero); Assert.That(Unit(replica, 2).Position, Is.EqualTo(original));
            Assert.That(Player(replica).Resources, Is.EqualTo(inventory)); Assert.That(Unit(replica, 2).Order, Is.EqualTo(UnitOrder.Idle));
        }

        [Test]
        public void RevealAndLoseSightRemovesEnemyStateButRetainsExploredTerrain()
        {
            var world = Create(); var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(16500, 12500))));
            Until(world, () => world.Vision.IsEntityVisible(1, 4));
            replica.ApplyNetworkSnapshot(NetworkObservation.Export(world, 1, 2)); Assert.That(replica.TryGetUnit(4, out _), Is.True);
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(6500, 12500))));
            Until(world, () => Unit(world, 2).Order == UnitOrder.Idle);
            replica.ApplyNetworkSnapshot(NetworkObservation.Export(world, 1, 3));
            Assert.That(replica.TryGetUnit(4, out _), Is.False);
            Assert.That(replica.Vision.IsExplored(1, Unit(world, 4).Position), Is.True);
            Assert.That(replica.Vision.IsVisible(1, Unit(world, 4).Position), Is.False);
        }

        [Test]
        public void VisibleEnemyDoesNotDiscloseOrdersResearchOrProduction()
        {
            var definition = Definitions(); definition.Units[1].VisionCells = 128;
            var world = Create(definition);
            Accepted(world.Submit(new TrainCommand(2, 101, "guard")));
            Accepted(world.Submit(new MoveCommand(2, new[] { 4 }, new SimPoint(20500, 8500))));
            var snapshot = NetworkObservation.Export(world, 1, 1);
            var unit = snapshot.Units.Single(x => x.Id == 4); var building = snapshot.Buildings.Single(x => x.Id == 101);
            Assert.That(unit.Destination, Is.EqualTo(unit.Position)); Assert.That(unit.TargetResourceId, Is.Zero); Assert.That(unit.AttackTargetId, Is.Zero);
            Assert.That(building.ProductionQueue, Is.Empty); Assert.That(building.ResearchId, Is.Null); Assert.That(building.HasRallyPoint, Is.False);
            Assert.That(Building(world, 101).ProductionQueue.Count, Is.EqualTo(1));
        }

        [Test]
        public void OwnInventoryQueueRallyAndEffectiveFactionStatsRoundTrip()
        {
            var world = FactionMatch();
            Accepted(world.Submit(new TrainCommand(1, 100, "worker")));
            Accepted(world.Submit(new SetRallyCommand(1, 100, new SimPoint(12500, 12500))));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            Assert.That(Player(replica).Resources, Is.EqualTo(Player(world).Resources));
            Assert.That(Player(replica).PopulationReserved, Is.EqualTo(Player(world).PopulationReserved));
            Assert.That(Building(replica, 100).ProductionQueue[0].RemainingTicks, Is.EqualTo(Building(world, 100).ProductionQueue[0].RemainingTicks));
            Assert.That(Building(replica, 100).RallyPoint, Is.EqualTo(Building(world, 100).RallyPoint));
            Assert.That(Unit(replica, 1).CarryCapacity, Is.EqualTo(Unit(world, 1).CarryCapacity));
            Assert.That(Unit(replica, 1).CarryCapacity, Is.GreaterThan(world.Definition.Units[0].CarryCapacity));
        }

        [Test]
        public void ResearchPendingCompletionAndChangedStatsRemainAuthoritative()
        {
            var world = FactionMatch(); Accepted(world.Submit(new ResearchCommand(1, 100, "reciprocal")));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            Assert.That(Building(replica, 100).ActiveResearch.TechnologyId, Is.EqualTo("reciprocal"));
            Assert.That(replica.ValidateResearch(new ResearchCommand(1, 100, "reciprocal")).Reason, Is.EqualTo(CommandRejection.ResearchInProgress));
            Tick(world, 2); replica.ApplyNetworkSnapshot(NetworkObservation.Export(world, 1, 2));
            Assert.That(Player(replica).HasTechnology("reciprocal"), Is.True);
            Assert.That(Building(replica, 100).ActiveResearch, Is.Null);
            Assert.That(Unit(replica, 2).Armor, Is.EqualTo(Unit(world, 2).Armor));
        }

        [Test]
        public void OwnPackedOutpostSurvivesObservationAndNormalizesSecondSeat()
        {
            var world = FactionMatch();
            var outpost = world.Buildings.First(x => x.OwnerId == 2 && x.DefinitionId == "outpost");
            Accepted(world.Submit(new PackOutpostCommand(2, outpost.Id))); Tick(world, 3);
            Assert.That(world.TryGetUnit(outpost.Id, out var transport), Is.True); Assert.That(transport.IsPackedOutpost, Is.True);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 2, 1));
            Assert.That(replica.TryGetUnit(outpost.Id, out var copy), Is.True); Assert.That(copy.IsPackedOutpost, Is.True);
            Assert.That(copy.PackedBuildingDefinitionId, Is.EqualTo("outpost")); Assert.That(copy.OwnerId, Is.EqualTo(1));
            Assert.That(copy.Health, Is.EqualTo(transport.Health)); Assert.That(copy.RelocationStage, Is.EqualTo(RelocationStage.Packed));
        }

        [Test]
        public void VisibleEnemyPackingCartAndDeploymentKeepFinitePublicProgressWithoutPrivatePackedState()
        {
            var world = FactionMatch(true);
            Accepted(world.Submit(new PackOutpostCommand(2, 120)));
            var snapshot = NetworkObservation.Export(world, 1, 1);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, snapshot);
            Assert.That(Building(replica, 120).RelocationTotalTicks, Is.GreaterThan(0));
            Assert.That(Building(replica, 120).RelocationRemainingTicks, Is.EqualTo(Building(world, 120).RelocationRemainingTicks));
            Tick(world, 3); snapshot = NetworkObservation.Export(world, 1, 2); replica.ApplyNetworkSnapshot(snapshot);
            var cart = snapshot.Units.Single(x => x.Id == 120);
            Assert.That(cart.PackedBuilding, Is.Null); Assert.That(cart.PackedBuildingDefinitionId, Is.EqualTo("outpost"));
            Assert.That(Unit(replica, 120).IsPackedOutpost, Is.True); Assert.That(Unit(replica, 120).OwnerId, Is.EqualTo(2));
            Accepted(world.Submit(new DeployOutpostCommand(2, 120, new SimPoint(24500, 13500))));
            replica.ApplyNetworkSnapshot(NetworkObservation.Export(world, 1, 3));
            Assert.That(Unit(replica, 120).RelocationStage, Is.EqualTo(RelocationStage.Deploying));
            Assert.That(Unit(replica, 120).RelocationTotalTicks, Is.GreaterThan(0));
            Assert.That(Unit(replica, 120).RelocationRemainingTicks, Is.EqualTo(Unit(world, 120).RelocationRemainingTicks));
            Assert.That(Unit(replica, 120).DeploymentPosition, Is.EqualTo(Unit(replica, 120).Position), "An enemy destination is not disclosed.");
        }

        [Test]
        public void CargoRetainedAfterLosingCapacityBonusStillAppliesToReplica()
        {
            var world = FactionMatch();
            Accepted(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics))); Tick(world, 3);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => Unit(world, 1).CarriedAmount > 7);
            Accepted(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Muster)));
            Assert.That(Unit(world, 1).CarriedAmount, Is.GreaterThan(Unit(world, 1).CarryCapacity));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            Assert.That(Unit(replica, 1).CarriedAmount, Is.EqualTo(Unit(world, 1).CarriedAmount));
            Assert.That(Unit(replica, 1).CarryCapacity, Is.EqualTo(Unit(world, 1).CarryCapacity));
        }

        [Test]
        public void PublicDominionOwnershipAndHoldClockNormalizeForSecondSeat()
        {
            var world = Create(map: DominionMap()); Tick(world, 20);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 2, 1));
            Assert.That(replica.Match.GetHoldTicks(2), Is.EqualTo(world.Match.GetHoldTicks(1)));
            Assert.That(replica.Match.Objectives[0].OwnerId, Is.EqualTo(2)); Assert.That(replica.Match.Objectives[1].OwnerId, Is.EqualTo(2));
            Assert.That(replica.Match.Objectives[0].HoldTicks, Is.EqualTo(world.Match.Objectives[0].HoldTicks));
        }

        [TestCase("version")] [TestCase("sequence")] [TestCase("tick")] [TestCase("seat")]
        [TestCase("mask")] [TestCase("duplicate")] [TestCase("unknown-definition")] [TestCase("position")] [TestCase("overlap")]
        [TestCase("zero-relocation-duration")] [TestCase("negative-unit-timer")] [TestCase("faction")]
        public void MalformedSnapshotIsRejectedWithoutPartialApplication(string corruption)
        {
            var world = Create(); Tick(world, 1);
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            Tick(world, 10); var snapshot = NetworkObservation.Export(world, 1, 2);
            switch (corruption)
            {
                case "version": snapshot.Version++; break;
                case "sequence": snapshot.Sequence = 1; break;
                case "tick": snapshot.Tick = 0; break;
                case "seat": snapshot.ServerPlayerId = 2; break;
                case "mask": snapshot.Vision = "!"; break;
                case "duplicate": snapshot.Units[1].Id = snapshot.Units[0].Id; break;
                case "unknown-definition": snapshot.Units[0].DefinitionId = "missing"; break;
                case "position": snapshot.Units[0].Position = new SimPoint(-1, 5); break;
                case "overlap": snapshot.Resources[0].Position = snapshot.Buildings[0].Position; break;
                case "zero-relocation-duration": snapshot.Units[0].RelocationStage = RelocationStage.Deploying; break;
                case "negative-unit-timer": snapshot.Units[0].ThreadkeeperRemainingTicks = -1; break;
                case "faction": snapshot.LocalPlayer.FactionId = "forged"; break;
            }
            var unit = Unit(replica, 1); var player = Player(replica);
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(snapshot));
            Assert.That(replica.TickIndex, Is.EqualTo(1)); Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
            Assert.That(Unit(replica, 1), Is.SameAs(unit)); Assert.That(Player(replica), Is.SameAs(player));
        }

        [Test]
        public void AuthoritativeWorldRejectsReplicaApplicationAndReplicaCannotExportAuthority()
        {
            var world = Create(); var snapshot = NetworkObservation.Export(world, 1, 1);
            Assert.Throws<InvalidOperationException>(() => world.ApplyNetworkSnapshot(snapshot));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, snapshot);
            Assert.Throws<ArgumentException>(() => NetworkObservation.Export(replica, 1, 2));
            Assert.Throws<ArgumentException>(() => NetworkObservation.Export(world, 99, 2));
        }

        private static World FactionMatch(bool revealAll = false)
        {
            var definitions = FactionTestWorldFactory.Definitions(); var map = FactionTestWorldFactory.Map();
            if (revealAll) foreach (var unit in definitions.Units) unit.VisionCells = 128;
            map.PlayerFactions = map.PlayerFactions.Where(x => x.PlayerId == 1 || x.PlayerId == 2).ToArray();
            map.UnitSpawns = map.UnitSpawns.Where(x => x.OwnerId == 1 || x.OwnerId == 2).ToArray();
            map.BuildingSpawns = map.BuildingSpawns.Where(x => x.OwnerId == 1 || x.OwnerId == 2).ToArray();
            map.OfflineMatch = new OfflineMatchDefinition { Enabled = true, CentralBuildingId = "hall" };
            return new World(definitions, map);
        }
    }
}
