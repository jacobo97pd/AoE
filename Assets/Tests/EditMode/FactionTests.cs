using System;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.FactionTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class FactionTests
    {
        [Test]
        public void AssignmentsAreCopiedWhileStartingResourcesAndSharedDefinitionsRemainEqual()
        {
            var definitions = Definitions(); var map = Map();
            var world = new World(definitions, map);
            Assert.That(Player(world).FactionId, Is.EqualTo("aven"));
            Assert.That(Player(world, 2).FactionId, Is.EqualTo("serevin"));
            Assert.That(Player(world, 3).FactionId, Is.Null);
            foreach (var player in world.Players) Assert.That(player.Resources, Is.EqualTo(definitions.StartingResources));
            map.PlayerFactions[0].FactionId = "serevin";
            Assert.That(Player(world).FactionId, Is.EqualTo("aven"), "Authored assignment mutation cannot switch an existing player.");
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            Assert.That(Unit(world, 3).CarryCapacity, Is.EqualTo(5));
            Assert.That(Unit(world, 7).CarryCapacity, Is.EqualTo(5));
            Assert.That(Unit(world, 4).MoveSpeedMillimetresPerSecond, Is.EqualTo(2100));
            Assert.That(Unit(world, 9).MoveSpeedMillimetresPerSecond, Is.EqualTo(2100));
            Assert.That(Unit(world, 8).MoveSpeedMillimetresPerSecond, Is.EqualTo(2000));
            Assert.That(Unit(world, 3).MoveSpeedMillimetresPerSecond, Is.EqualTo(2000));
            Assert.That(definitions.Units[0].CarryCapacity, Is.EqualTo(5));
            Assert.That(definitions.Units[4].MoveSpeedMillimetresPerSecond, Is.EqualTo(2000));
        }

        [TestCase(1, 100, "ash", "outpost", "prepared")]
        [TestCase(2, 101, "thread", "yard", "reciprocal")]
        [TestCase(3, 102, "thread", "yard", "reciprocal")]
        public void FactionLocksRejectUnitBuildingAndTechnologyPurchasesAtomically(int owner, int hall, string unit, string building, string technology)
        {
            var world = Create();
            int worker = owner == 1 ? 1 : owner == 2 ? 3 : 7;
            var before = Player(world, owner).Resources;
            var destination = new SimPoint(Unit(world, worker).Position.X + 1000, Unit(world, worker).Position.Z);
            Accepted(world.Submit(new MoveCommand(owner, new[] { worker }, destination)));
            Assert.That(world.Submit(new TrainCommand(owner, hall, unit)).Accepted, Is.False);
            Assert.That(world.Submit(new BuildCommand(owner, new[] { worker }, building, new SimPoint(32500, 24500))).Accepted, Is.False);
            Assert.That(world.Submit(new ResearchCommand(owner, hall, technology)).Accepted, Is.False);
            Assert.That(Player(world, owner).Resources, Is.EqualTo(before));
            Assert.That(Player(world, owner).PopulationReserved, Is.Zero);
            Assert.That(Building(world, hall).ProductionQueue, Is.Empty);
            Assert.That(Building(world, hall).ActiveResearch, Is.Null);
            Assert.That(Unit(world, worker).Destination, Is.EqualTo(destination));
            Assert.That(world.Buildings.Count, Is.EqualTo(7));
        }

        [Test]
        public void LegacyNeutralWorldKeepsUnmodifiedWorkerAndCavalryRules()
        {
            var definitions = CombatTestWorldFactory.Definitions();
            var world = new World(definitions, CombatTestWorldFactory.Map());
            Assert.That(Player(world).FactionId, Is.Null);
            Assert.That(Unit(world, 3).CarryCapacity, Is.EqualTo(definitions.Units[2].CarryCapacity));
            Assert.That(Unit(world, 1).MoveSpeedMillimetresPerSecond, Is.EqualTo(definitions.Units[0].MoveSpeedMillimetresPerSecond));
            Assert.That(world.Submit(new SetCharterCommand(1, 100, StoreyardCharter.Logistics)).Accepted, Is.False);
            Accepted(world.Submit(new TrainCommand(1, 100, "worker")));
        }

        [Test]
        public void CharterValidationIsReadOnlyAndPaymentPrecedesExactTimedActivation()
        {
            var world = Create(); var before = Player(world).Resources;
            var command = new SetCharterCommand(1, 110, StoreyardCharter.Logistics);
            Accepted(world.ValidateFactionAction(command)); Accepted(world.ValidateFactionAction(command));
            Assert.That(Player(world).Resources, Is.EqualTo(before));
            Assert.That(Building(world).ActiveCharter, Is.EqualTo(StoreyardCharter.None));
            Accepted(world.Submit(command));
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(990, 995, 1000, 1000)));
            Assert.That(Building(world).PendingCharter, Is.EqualTo(StoreyardCharter.Logistics));
            Assert.That(Building(world).CharterRemainingTicks, Is.EqualTo(3));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            Tick(world, 2);
            Assert.That(Building(world).CharterRemainingTicks, Is.EqualTo(1));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            world.Tick();
            Assert.That(Building(world).ActiveCharter, Is.EqualTo(StoreyardCharter.Logistics));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(10));
            Assert.That(world.Submit(command).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(990, 995, 1000, 1000)));
        }

        [TestCase(1, 111, StoreyardCharter.Logistics)]
        [TestCase(2, 120, StoreyardCharter.Logistics)]
        [TestCase(1, 100, StoreyardCharter.Logistics)]
        [TestCase(1, 110, StoreyardCharter.None)]
        [TestCase(1, 110, (StoreyardCharter)99)]
        public void InvalidCharterCommandsCannotSpendOrStartTimers(int owner, int yard, StoreyardCharter charter)
        {
            var world = Create(); var before = Player(world, owner).Resources;
            Assert.That(world.Submit(new SetCharterCommand(owner, yard, charter)).Accepted, Is.False);
            Assert.That(Player(world, owner).Resources, Is.EqualTo(before));
            Assert.That(Building(world).CharterRemainingTicks, Is.Zero);
        }

        [Test]
        public void CharterSwitchCostsAgainAndSuppressesOldBenefitForItsFullDowntime()
        {
            var world = Create(); Charter(world, StoreyardCharter.Logistics);
            Accepted(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Muster)));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            Assert.That(Building(world, 100).ProductionWorkPermille, Is.EqualTo(1000));
            Assert.That(Building(world).IsOperational, Is.True, "Charter switching preserves ordinary drop-off service.");
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(980, 990, 1000, 1000)));
            world.Tick();
            Assert.That(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics)).Accepted, Is.False);
            Assert.That(Building(world).CharterRemainingTicks, Is.EqualTo(2));
            world.Tick();
            Assert.That(Building(world, 100).ProductionWorkPermille, Is.EqualTo(1000));
            world.Tick();
            Assert.That(Building(world).ActiveCharter, Is.EqualTo(StoreyardCharter.Muster));
            Assert.That(Building(world, 100).ProductionWorkPermille, Is.EqualTo(1500));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(980, 990, 1000, 1000)));
        }

        [Test]
        public void CharterChangesAndProducerWorkExcludeEachOtherWithoutLosingReservations()
        {
            var definitions = Definitions(); definitions.Technologies[0].ResearchBuildingId = "yard";
            var world = new World(definitions, Map());
            Accepted(world.Submit(new TrainCommand(1, 110, "worker")));
            var before = Player(world).Resources;
            Assert.That(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics)).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(before));
            Assert.That(Player(world).PopulationReserved, Is.EqualTo(1));
            Assert.That(Building(world).ProductionQueue.Count, Is.EqualTo(1));
            Tick(world, 4);
            Accepted(world.Submit(new ResearchCommand(1, 110, "reciprocal")));
            before = Player(world).Resources;
            Assert.That(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics)).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(before));
            Tick(world, 2);
            Accepted(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics)));
            before = Player(world).Resources;
            Assert.That(world.Submit(new TrainCommand(1, 110, "worker")).Accepted, Is.False);
            Assert.That(Building(world).ProductionQueue, Is.Empty);
            Assert.That(Player(world).PopulationReserved, Is.Zero);
            Assert.That(Player(world).Resources, Is.EqualTo(before));
        }

        [Test]
        public void OverlappingLogisticsYardsNeverStackAndApplyToNewOwnedWorkers()
        {
            var world = Create(); Charter(world, StoreyardCharter.Logistics); Charter(world, StoreyardCharter.Logistics, 112);
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(10));
            Assert.That(Unit(world).CharterSourceId, Is.EqualTo(110));
            Assert.That(Unit(world, 3).CarryCapacity, Is.EqualTo(5));
            Assert.That(Unit(world, 7).CarryCapacity, Is.EqualTo(5));
            Accepted(world.Submit(new TrainCommand(1, 100, "worker")));
            Tick(world, 4);
            Assert.That(Unit(world, 202).CarryCapacity, Is.EqualTo(10));
            Assert.That(world.Definition.Units[0].CarryCapacity, Is.EqualTo(5));
        }

        [Test]
        public void OverlappingMusterYardsAccelerateTrainingOnlyOnce()
        {
            var world = Create(); Charter(world, StoreyardCharter.Muster); Charter(world, StoreyardCharter.Muster, 112);
            Assert.That(Building(world, 100).ProductionWorkPermille, Is.EqualTo(1500));
            Assert.That(Unit(world).MoveSpeedMillimetresPerSecond, Is.EqualTo(2000));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            int count = world.Units.Count;
            Accepted(world.Submit(new TrainCommand(1, 100, "worker")));
            Tick(world, 2);
            Assert.That(world.Units.Count, Is.EqualTo(count), "Two overlapping 50% sources must not become double work.");
            world.Tick();
            Assert.That(world.Units.Count, Is.EqualTo(count + 1));
            Assert.That(Player(world).PopulationReserved, Is.Zero);
        }

        [Test]
        public void CargoAboveReducedTemporaryCapacityIsPreservedAndDeliveredWithoutCreatingResources()
        {
            var world = Create(); Charter(world, StoreyardCharter.Logistics);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Until(world, () => Unit(world).CarriedAmount == 10, "Logistics load fills");
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Accepted(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Muster)));
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            Assert.That(Unit(world).CarriedAmount, Is.EqualTo(10), "Ending a temporary bonus cannot destroy existing cargo.");
            int inventory = Player(world).Resources.Wood;
            Assert.That(world.TryGetResource(201, out var resource), Is.True);
            Assert.That(resource.RemainingAmount, Is.EqualTo(90));
            Accepted(world.Submit(new ReturnCargoCommand(1, new[] { 1 }, 110)));
            Until(world, () => Unit(world).CarriedAmount == 0, "Oversize preserved cargo arrives at ordinary drop-off");
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(inventory + 10));
            Assert.That(resource.RemainingAmount, Is.EqualTo(90));
        }

        [Test]
        public void YardDestroyedOnCharterCompletionTickCannotApplyBenefitOrRefundCost()
        {
            var world = WithAttacker(300, 110);
            Accepted(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics)));
            Tick(world, 2);
            Accepted(world.Submit(new AttackCommand(3, new[] { 20 }, 110)));
            world.Tick();
            Assert.That(world.TryGetBuilding(110, out _), Is.False);
            Assert.That(Unit(world).CarryCapacity, Is.EqualTo(7));
            Assert.That(Player(world).Resources, Is.EqualTo(new ResourceAmount(990, 995, 1000, 1000)));
            Assert.That(world.DeathCount, Is.EqualTo(1));
        }

        [Test]
        public void ThreadkeeperRelayIsTimedImmobileAndExtendsOnlyItsOwnedCharteredYard()
        {
            var world = Create();
            Assert.That(world.Submit(new DeployThreadkeeperCommand(1, 5, 110)).Accepted, Is.False, "An inactive yard supplies no charter to relay.");
            Charter(world, StoreyardCharter.Logistics);
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(7));
            var stock = Player(world).Resources; int buildings = world.Buildings.Count;
            Accepted(world.Submit(new DeployThreadkeeperCommand(1, 5, 110)));
            var position = Unit(world, 5).Position;
            Assert.That(Unit(world, 5).LinkedStoreyardId, Is.EqualTo(110));
            Assert.That(Unit(world, 5).ThreadkeeperRemainingTicks, Is.EqualTo(4));
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(10));
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1, 5 }, new SimPoint(15500, 8500))).Accepted, Is.False);
            Assert.That(world.Submit(new StopCommand(1, new[] { 5 })).Accepted, Is.False);
            Assert.That(Unit(world).Order, Is.EqualTo(UnitOrder.Idle), "A mixed rejected order must not partially move the worker.");
            Tick(world, 3);
            Assert.That(Unit(world, 5).Position, Is.EqualTo(position));
            Assert.That(Unit(world, 5).ThreadkeeperRemainingTicks, Is.EqualTo(1));
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(10));
            world.Tick();
            Assert.That(Unit(world, 5).ThreadkeeperRemainingTicks, Is.Zero);
            Assert.That(Unit(world, 5).ThreadkeeperCooldownTicks, Is.EqualTo(6));
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(7));
            Assert.That(world.Submit(new DeployThreadkeeperCommand(1, 5, 110)).Accepted, Is.False);
            Tick(world, 5);
            Assert.That(Unit(world, 5).ThreadkeeperCooldownTicks, Is.EqualTo(1));
            world.Tick();
            Accepted(world.Submit(new DeployThreadkeeperCommand(1, 5, 110)));
            Assert.That(Player(world).Resources, Is.EqualTo(stock));
            Assert.That(world.Buildings.Count, Is.EqualTo(buildings), "Relay creates no free building or new drop-off.");
            Assert.That(world.TryGetBuilding(5, out _), Is.False);
        }

        [TestCase(2, 5, 110)]
        [TestCase(1, 1, 110)]
        [TestCase(1, 5, 111)]
        [TestCase(1, 5, 100)]
        [TestCase(1, 5, 999)]
        public void RelayRejectsForeignWrongAndMissingEntitiesAtomically(int owner, int unit, int yard)
        {
            var world = Create(); Charter(world, StoreyardCharter.Logistics);
            var before = Player(world, owner).Resources;
            Assert.That(world.Submit(new DeployThreadkeeperCommand(owner, unit, yard)).Accepted, Is.False);
            Assert.That(Unit(world, 5).ThreadkeeperRemainingTicks, Is.Zero);
            Assert.That(Unit(world, 5).LinkedStoreyardId, Is.Zero);
            Assert.That(Player(world, owner).Resources, Is.EqualTo(before));
        }

        [Test]
        public void DestroyedYardImmediatelyEndsItsRelayWithoutResourcesOrLingeringBonus()
        {
            var world = WithAttacker(300, 110); Charter(world, StoreyardCharter.Logistics);
            Accepted(world.Submit(new DeployThreadkeeperCommand(1, 5, 110)));
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(10));
            var before = Player(world).Resources;
            Accepted(world.Submit(new AttackCommand(3, new[] { 20 }, 110)));
            world.Tick();
            Assert.That(Unit(world, 5).ThreadkeeperRemainingTicks, Is.Zero);
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(7));
            Assert.That(Player(world).Resources, Is.EqualTo(before));
        }

        [Test]
        public void InsufficientFundsAndIncompleteYardRejectCharterWithoutPartialPayment()
        {
            var definitions = Definitions(); definitions.StartingResources = new ResourceAmount(9, 1000, 0, 0);
            var world = new World(definitions, Map());
            Assert.That(world.Submit(new SetCharterCommand(1, 110, StoreyardCharter.Logistics)).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(definitions.StartingResources));
            Assert.That(Building(world).CharterRemainingTicks, Is.Zero);
            world = Create();
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "yard", new SimPoint(12500, 12500)));
            Accepted(result);
            var before = Player(world).Resources;
            Assert.That(Building(world, result.EntityId).IsComplete, Is.False);
            Assert.That(world.Submit(new SetCharterCommand(1, result.EntityId, StoreyardCharter.Logistics)).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(before));
        }

        [Test]
        public void PaidOutpostPacksWithSameIdentityWithoutRefundDuplicateOrPopulationChange()
        {
            var world = Create();
            var built = world.Submit(new BuildCommand(2, new[] { 3 }, "outpost", new SimPoint(18500, 8500)));
            Accepted(built);
            Assert.That(Player(world, 2).Resources.Wood, Is.EqualTo(960));
            Assert.That(world.Submit(new PackOutpostCommand(2, built.EntityId)).Accepted, Is.False, "An unfinished foundation cannot pack.");
            Until(world, () => Building(world, built.EntityId).IsComplete, "Originally paid outpost completes");
            var stock = Player(world, 2).Resources;
            int population = Player(world, 2).PopulationUsed, buildingCount = world.Buildings.Count, unitCount = world.Units.Count;
            Accepted(world.Submit(new PackOutpostCommand(2, built.EntityId)));
            Assert.That(Building(world, built.EntityId).IsOperational, Is.False);
            Assert.That(Building(world, built.EntityId).RelocationStage, Is.EqualTo(RelocationStage.Packing));
            Assert.That(Building(world, built.EntityId).RelocationRemainingTicks, Is.EqualTo(3));
            Tick(world, 2);
            Assert.That(world.TryGetBuilding(built.EntityId, out _), Is.True);
            Assert.That(world.TryGetUnit(built.EntityId, out _), Is.False);
            world.Tick();
            Assert.That(world.TryGetBuilding(built.EntityId, out _), Is.False);
            var transport = Unit(world, built.EntityId);
            Assert.That(transport.IsPackedOutpost, Is.True);
            Assert.That(transport.RelocationStage, Is.EqualTo(RelocationStage.Packed));
            Assert.That(transport.PackedBuildingDefinitionId, Is.EqualTo("outpost"));
            Assert.That(transport.PopulationCost, Is.Zero);
            Assert.That(transport.AttackDamage, Is.Zero);
            Assert.That(transport.IsWorker, Is.False);
            Assert.That(world.Buildings.Count, Is.EqualTo(buildingCount - 1));
            Assert.That(world.Units.Count, Is.EqualTo(unitCount + 1));
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(population));
            Assert.That(Player(world, 2).PopulationReserved, Is.Zero);
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock));
            Assert.That(world.DeathCount, Is.Zero);
        }

        [Test]
        public void PackingRejectsQueuedTrainingAndResearchWithoutLosingTheirInvestments()
        {
            var world = Create();
            Accepted(world.Submit(new TrainCommand(2, 120, "worker")));
            var before = Player(world, 2).Resources;
            Assert.That(world.Submit(new PackOutpostCommand(2, 120)).Accepted, Is.False);
            Assert.That(Player(world, 2).Resources, Is.EqualTo(before));
            Assert.That(Player(world, 2).PopulationReserved, Is.EqualTo(1));
            Assert.That(Building(world, 120).ProductionQueue.Count, Is.EqualTo(1));
            Tick(world, 4);
            Accepted(world.Submit(new ResearchCommand(2, 120, "outpost-study")));
            before = Player(world, 2).Resources;
            Assert.That(world.Submit(new PackOutpostCommand(2, 120)).Accepted, Is.False);
            Assert.That(Building(world, 120).ActiveResearch.TechnologyId, Is.EqualTo("outpost-study"));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(before));
            Tick(world, 4);
            Accepted(world.Submit(new PackOutpostCommand(2, 120)));
            before = Player(world, 2).Resources;
            Assert.That(world.Submit(new TrainCommand(2, 120, "worker")).Accepted, Is.False);
            Assert.That(world.Submit(new PackOutpostCommand(2, 120)).Accepted, Is.False);
            Assert.That(Player(world, 2).Resources, Is.EqualTo(before));
            Assert.That(Player(world, 2).PopulationReserved, Is.Zero);
        }

        [Test]
        public void PackingStopsDropOffImmediatelyButRetainsTheWorkersCargo()
        {
            var map = Map(); map.ResourceSpawns[0].Position = new SimPoint(22500, 6500);
            var world = new World(Definitions(), map);
            Accepted(world.Submit(new GatherCommand(2, new[] { 3 }, 201)));
            Until(world, () => Unit(world, 3).CarriedAmount > 0, "Serevin worker has cargo");
            Accepted(world.Submit(new StopCommand(2, new[] { 3 })));
            int cargo = Unit(world, 3).CarriedAmount;
            var stock = Player(world, 2).Resources;
            Accepted(world.Submit(new PackOutpostCommand(2, 120)));
            Assert.That(Building(world, 120).IsOperational, Is.False);
            Assert.That(world.Submit(new ReturnCargoCommand(2, new[] { 3 }, 120)).Accepted, Is.False);
            Assert.That(Unit(world, 3).CarriedAmount, Is.EqualTo(cargo));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock));
        }

        [TestCase(1, 120)]
        [TestCase(2, 101)]
        [TestCase(2, 999)]
        public void InvalidPackCannotTransformForeignOrOrdinaryBuildings(int owner, int id)
        {
            var world = Create(); var before = Player(world, owner).Resources;
            Assert.That(world.Submit(new PackOutpostCommand(owner, id)).Accepted, Is.False);
            Assert.That(world.Buildings.Count, Is.EqualTo(7));
            Assert.That(world.Units.Count, Is.EqualTo(9));
            Assert.That(Player(world, owner).Resources, Is.EqualTo(before));
            Assert.That(world.DeathCount, Is.Zero);
        }

        [Test]
        public void TransportMovesNormallyAndRedeploymentPreservesExactDamageAndIdentity()
        {
            var world = WithAttacker(75);
            Accepted(world.Submit(new AttackCommand(3, new[] { 20 }, 120)));
            world.Tick();
            Accepted(world.Submit(new StopCommand(3, new[] { 20 })));
            Assert.That(Building(world, 120).Health, Is.EqualTo(225));
            var stock = Player(world, 2).Resources;
            int population = Player(world, 2).PopulationUsed;
            Pack(world);
            Assert.That(Unit(world, 120).Health, Is.EqualTo(225));
            Assert.That(world.Submit(new AttackCommand(2, new[] { 120 }, 20)).Accepted, Is.False);
            Assert.That(world.Submit(new GatherCommand(2, new[] { 120 }, 201)).Accepted, Is.False);
            var destination = new SimPoint(18500, 12500);
            var oldPosition = Unit(world, 120).Position;
            Accepted(world.Submit(new MoveCommand(2, new[] { 120 }, destination)));
            world.Tick();
            Assert.That(Unit(world, 120).Position, Is.Not.EqualTo(oldPosition));
            Assert.That(Unit(world, 120).Position, Is.Not.EqualTo(destination), "Relocation uses real movement, not teleportation.");
            Until(world, () => Unit(world, 120).Order == UnitOrder.Idle, "Packed outpost arrives");
            Assert.That(Unit(world, 120).Position, Is.EqualTo(destination));
            var site = new SimPoint(18500, 13500);
            Accepted(world.Submit(new DeployOutpostCommand(2, 120, site)));
            Assert.That(Unit(world, 120).DeploymentPosition, Is.EqualTo(site));
            Assert.That(Unit(world, 120).RelocationRemainingTicks, Is.EqualTo(3));
            Assert.That(world.Submit(new MoveCommand(2, new[] { 120 }, oldPosition)).Accepted, Is.False);
            Tick(world, 2);
            Assert.That(Unit(world, 120).Position, Is.EqualTo(destination));
            Assert.That(world.TryGetBuilding(120, out _), Is.False);
            world.Tick();
            Assert.That(world.TryGetUnit(120, out _), Is.False);
            Assert.That(Building(world, 120).Position, Is.EqualTo(site));
            Assert.That(Building(world, 120).Health, Is.EqualTo(225));
            Assert.That(Building(world, 120).MaxHealth, Is.EqualTo(300));
            Assert.That(Building(world, 120).IsComplete, Is.True);
            Assert.That(Building(world, 120).IsOperational, Is.True);
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(population));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock), "The same paid asset has no extra redeploy charge or refund.");
            Assert.That(world.DeathCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyingPackedOrDeployingTransportLosesAssetOnceWithoutPopulationDrift(bool deploying)
        {
            var world = WithAttacker(300); Pack(world);
            int population = Player(world, 2).PopulationUsed;
            var stock = Player(world, 2).Resources;
            if (deploying)
            {
                Accepted(world.Submit(new DeployOutpostCommand(2, 120, new SimPoint(24500, 13500))));
                Tick(world, 2);
            }
            Accepted(world.Submit(new AttackCommand(3, new[] { 20 }, 120)));
            world.Tick(); Tick(world, 4);
            Assert.That(world.TryGetUnit(120, out _), Is.False);
            Assert.That(world.TryGetBuilding(120, out _), Is.False);
            Assert.That(world.DeathCount, Is.EqualTo(1));
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(population));
            Assert.That(Player(world, 2).PopulationReserved, Is.Zero);
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock));
        }

        [Test]
        public void DestroyedBuildingCannotFinishPackingOnTheSameTick()
        {
            var world = WithAttacker(300);
            Accepted(world.Submit(new PackOutpostCommand(2, 120))); Tick(world, 2);
            Accepted(world.Submit(new AttackCommand(3, new[] { 20 }, 120)));
            world.Tick();
            Assert.That(world.TryGetBuilding(120, out _), Is.False);
            Assert.That(world.TryGetUnit(120, out _), Is.False);
            Assert.That(world.DeathCount, Is.EqualTo(1));
        }

        [TestCase(1, 24500, 13500)]
        [TestCase(2, -500, 12500)]
        [TestCase(2, 31500, 12500)]
        [TestCase(2, 24500, 12500)]
        public void ForeignDistantOutsideOrSelfOccupiedRedeploymentRejectsWithoutMutation(int owner, int x, int z)
        {
            var world = Create(); Pack(world);
            var stock = Player(world, 2).Resources; var position = Unit(world, 120).Position;
            Assert.That(world.Submit(new DeployOutpostCommand(owner, 120, new SimPoint(x, z))).Accepted, Is.False);
            Assert.That(Unit(world, 120).RelocationStage, Is.EqualTo(RelocationStage.Packed));
            Assert.That(Unit(world, 120).Position, Is.EqualTo(position));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock));
            Assert.That(world.TryGetBuilding(120, out _), Is.False);
        }

        [Test]
        public void OccupancyEnteringAfterDeploymentAcceptanceDelaysCompletionUntilClear()
        {
            var map = Map(); map.UnitSpawns[2].Position = new SimPoint(19500, 13500);
            var world = new World(Definitions(), map); Pack(world);
            Accepted(world.Submit(new MoveCommand(2, new[] { 120 }, new SimPoint(18500, 12500))));
            Until(world, () => Unit(world, 120).Order == UnitOrder.Idle, "Transport reaches setup point");
            var site = new SimPoint(18500, 13500);
            Accepted(world.Submit(new DeployOutpostCommand(2, 120, site)));
            Accepted(world.Submit(new MoveCommand(2, new[] { 3 }, site)));
            Tick(world, 10);
            Assert.That(world.TryGetBuilding(120, out _), Is.False, "Completion must recheck a newly occupied footprint.");
            Assert.That(Unit(world, 120).RelocationStage, Is.EqualTo(RelocationStage.Deploying));
            Assert.That(Unit(world, 120).RelocationRemainingTicks, Is.Zero);
            var stock = Player(world, 2).Resources;
            Accepted(world.Submit(new MoveCommand(2, new[] { 3 }, new SimPoint(21500, 13500))));
            Until(world, () => world.TryGetBuilding(120, out _), "Delayed deployment completes once obstruction leaves");
            Assert.That(Building(world, 120).Position, Is.EqualTo(site));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock));
            Assert.That(world.DeathCount, Is.Zero);
        }

        [Test]
        public void StopCancelsDeploymentWithoutLosingOrHealingTheTransport()
        {
            var world = Create(); Pack(world);
            var before = Player(world, 2).Resources;
            Accepted(world.Submit(new DeployOutpostCommand(2, 120, new SimPoint(24500, 13500))));
            world.Tick();
            Accepted(world.Submit(new StopCommand(2, new[] { 120 })));
            Assert.That(Unit(world, 120).RelocationStage, Is.EqualTo(RelocationStage.Packed));
            Tick(world, 5);
            Assert.That(world.TryGetBuilding(120, out _), Is.False);
            Assert.That(Unit(world, 120).Health, Is.EqualTo(300));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(before));
            Accepted(world.Submit(new MoveCommand(2, new[] { 120 }, new SimPoint(21500, 12500))));
        }

        [Test]
        public void RepositionAddsBriefMovementSpeedThenRequiresItsFullCooldown()
        {
            var world = Create(); var start = Unit(world, 4).Position;
            var command = new RepositionCommand(2, new[] { 4 });
            var stock = Player(world, 2).Resources;
            Accepted(world.ValidateFactionAction(command));
            Assert.That(Unit(world, 4).RepositionRemainingTicks, Is.Zero);
            Accepted(world.Submit(command));
            Assert.That(Unit(world, 4).RepositionRemainingTicks, Is.EqualTo(3));
            Assert.That(Unit(world, 4).MoveSpeedMillimetresPerSecond, Is.EqualTo(3100));
            Accepted(world.Submit(new MoveCommand(2, new[] { 4 }, new SimPoint(28500, 4500))));
            Tick(world, 2);
            Assert.That(Unit(world, 4).Position.X - start.X, Is.EqualTo(310), "Two actual movement ticks use the passive plus temporary speed.");
            Assert.That(Unit(world, 4).RepositionRemainingTicks, Is.EqualTo(1));
            Assert.That(world.Submit(command).Accepted, Is.False);
            Accepted(world.Submit(new StopCommand(2, new[] { 4 })));
            Assert.That(Unit(world, 4).RepositionRemainingTicks, Is.EqualTo(1), "Stop cancels locomotion, not the ability's commitment.");
            world.Tick();
            Assert.That(Unit(world, 4).RepositionRemainingTicks, Is.Zero);
            Assert.That(Unit(world, 4).RepositionCooldownTicks, Is.EqualTo(5));
            Assert.That(Unit(world, 4).MoveSpeedMillimetresPerSecond, Is.EqualTo(2100));
            Assert.That(world.Submit(command).Accepted, Is.False);
            Tick(world, 4);
            Assert.That(Unit(world, 4).RepositionCooldownTicks, Is.EqualTo(1));
            world.Tick();
            Accepted(world.Submit(command));
            Assert.That(Player(world, 2).Resources, Is.EqualTo(stock));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(999)]
        public void MixedInvalidRepositionSelectionRejectsAllUnitsWithoutStartingCooldown(int invalidId)
        {
            var world = Create();
            Assert.That(world.Submit(new RepositionCommand(2, new[] { 4, invalidId })).Accepted, Is.False);
            Assert.That(Unit(world, 4).RepositionRemainingTicks, Is.Zero);
            Assert.That(Unit(world, 4).RepositionCooldownTicks, Is.Zero);
            Assert.That(Unit(world, 4).MoveSpeedMillimetresPerSecond, Is.EqualTo(2100));
        }

        [Test]
        public void RepositionCancelsAttackAndPreventsAutomaticAndExplicitAttacksForItsDuration()
        {
            var map = Map(); map.UnitSpawns[3].Position = new SimPoint(9500, 4500);
            var world = new World(Definitions(), map);
            Accepted(world.Submit(new StopCommand(1, new[] { 2 })));
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 2)));
            Accepted(world.Submit(new RepositionCommand(2, new[] { 4 })));
            Assert.That(Unit(world, 4).AttackTargetId, Is.Zero);
            Assert.That(world.Submit(new AttackCommand(2, new[] { 4 }, 2)).Accepted, Is.False);
            int health = Unit(world, 2).Health;
            Tick(world, 3);
            Assert.That(Unit(world, 2).Health, Is.EqualTo(health), "An idle repositioning cavalry unit cannot autoattack either.");
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 2)));
            world.Tick();
            Assert.That(Unit(world, 2).Health, Is.EqualTo(health - 10));
        }

        [Test]
        public void RepositionDoesNotRemoveTheAshrunnersSpearCounterOrMakeItInvulnerable()
        {
            var map = Map(); map.UnitSpawns[3].Position = new SimPoint(9500, 4500);
            var world = new World(Definitions(), map);
            Accepted(world.Submit(new RepositionCommand(2, new[] { 4 })));
            Accepted(world.Submit(new AttackCommand(1, new[] { 2 }, 4)));
            int health = Unit(world, 4).Health;
            world.Tick();
            Assert.That(Unit(world, 4).Health, Is.EqualTo(health - 18), "The normal 1.8x cavalry tag counter still applies during reposition.");
        }

        [Test]
        public void ReciprocalTechnologyExpandsExistingCharterReachWithoutStackingAndUpgradesFutureOwnedUnits()
        {
            var world = Create(); Charter(world, StoreyardCharter.Logistics);
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(7));
            Research(world, "reciprocal");
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(10), "Paid unique research reaches the worker 7 m from the yard.");
            Assert.That(Unit(world, 2).Armor, Is.EqualTo(1));
            Assert.That(Unit(world, 4).Armor, Is.Zero);
            var stock = Player(world).Resources;
            Assert.That(world.Submit(new ResearchCommand(1, 100, "reciprocal")).Accepted, Is.False);
            Assert.That(Player(world).Resources, Is.EqualTo(stock));
            Accepted(world.Submit(new TrainCommand(1, 100, "spear"))); Tick(world, 4);
            Assert.That(Unit(world, 202).Armor, Is.EqualTo(1));
            Assert.That(world.Definition.Units[1].Armor, Is.Zero);
        }

        [Test]
        public void PreparedTechnologyDoesNotShortenAnAlreadyAcceptedPackButReducesTheNextDeployment()
        {
            var world = Create();
            Accepted(world.Submit(new PackOutpostCommand(2, 120)));
            Assert.That(Building(world, 120).RelocationTotalTicks, Is.EqualTo(3));
            Accepted(world.Submit(new ResearchCommand(2, 101, "prepared")));
            Tick(world, 2);
            Assert.That(Player(world, 2).HasTechnology("prepared"), Is.True);
            Assert.That(Building(world, 120).RelocationRemainingTicks, Is.EqualTo(1));
            Assert.That(Building(world, 120).RelocationTotalTicks, Is.EqualTo(3), "Mid-action research cannot change the progress denominator.");
            world.Tick();
            Assert.That(Unit(world, 120).IsPackedOutpost, Is.True);
            Assert.That(Unit(world, 120).RelocationTotalTicks, Is.Zero);
            Assert.That(Unit(world, 4).Armor, Is.EqualTo(1));
            Assert.That(Unit(world, 8).Armor, Is.Zero);
            Accepted(world.Submit(new DeployOutpostCommand(2, 120, new SimPoint(24500, 13500))));
            Assert.That(Unit(world, 120).RelocationRemainingTicks, Is.EqualTo(2));
            Assert.That(Unit(world, 120).RelocationTotalTicks, Is.EqualTo(2));
            world.Tick(); Assert.That(world.TryGetBuilding(120, out _), Is.False);
            Assert.That(Unit(world, 120).RelocationTotalTicks, Is.EqualTo(2));
            world.Tick(); Assert.That(Building(world, 120).IsOperational, Is.True);
            Assert.That(Building(world, 120).RelocationTotalTicks, Is.Zero);
            Accepted(world.Submit(new PackOutpostCommand(2, 120)));
            Assert.That(Building(world, 120).RelocationRemainingTicks, Is.EqualTo(2));
            Assert.That(Building(world, 120).RelocationTotalTicks, Is.EqualTo(2));
            Accepted(world.Submit(new TrainCommand(2, 101, "ash")));
            Until(world, () => world.TryGetUnit(202, out _), "New unique cavalry receives completed faction effects");
            Assert.That(Unit(world, 202).Armor, Is.EqualTo(1));
            Assert.That(Unit(world, 202).MoveSpeedMillimetresPerSecond, Is.EqualTo(2100));
        }

        [Test]
        public void RelayCannotChainASecondThreadkeeperBeyondItsActualYardRadius()
        {
            var map = Map();
            var spawns = new UnitSpawnDefinition[map.UnitSpawns.Length + 1];
            Array.Copy(map.UnitSpawns, spawns, map.UnitSpawns.Length);
            spawns[spawns.Length - 1] = CombatTestWorldFactory.Spawn(10, 1, "thread", 11500, 9500);
            map.UnitSpawns = spawns;
            var world = new World(Definitions(), map); Charter(world, StoreyardCharter.Logistics);
            Accepted(world.Submit(new DeployThreadkeeperCommand(1, 5, 110)));
            Assert.That(Unit(world, 6).CarryCapacity, Is.EqualTo(10));
            Assert.That(world.Submit(new DeployThreadkeeperCommand(1, 10, 110)).Accepted, Is.False,
                "The second support unit is inside the relay, but outside the source yard's 6 m radius.");
            Assert.That(Unit(world, 10).ThreadkeeperRemainingTicks, Is.Zero);
        }

        [TestCase("unknown-assignment")]
        [TestCase("duplicate-assignment")]
        [TestCase("null-assignment")]
        [TestCase("duplicate-faction")]
        [TestCase("unknown-kind")]
        [TestCase("unknown-unit-faction")]
        [TestCase("unknown-building-faction")]
        [TestCase("unknown-technology-faction")]
        [TestCase("foreign-unique-unit-spawn")]
        [TestCase("foreign-unique-building-spawn")]
        [TestCase("negative-carry-bonus")]
        [TestCase("overflowing-carry-bonus")]
        [TestCase("negative-charter-cost")]
        [TestCase("zero-charter-duration")]
        [TestCase("instant-prepared-relocation")]
        [TestCase("unknown-linked-technology")]
        [TestCase("transport-health-mismatch")]
        [TestCase("transport-population")]
        [TestCase("transport-worker")]
        [TestCase("transport-armed")]
        [TestCase("directly-trainable-transport")]
        [TestCase("mobile-population-capacity")]
        public void InvalidFactionAssignmentsReferencesAndTransformationsRejectBeforeWorldCreation(string fault)
        {
            var definitions = Definitions(); var map = Map();
            switch (fault)
            {
                case "unknown-assignment": map.PlayerFactions[0].FactionId = "missing"; break;
                case "duplicate-assignment": map.PlayerFactions[1].PlayerId = 1; break;
                case "null-assignment": map.PlayerFactions[0] = null; break;
                case "duplicate-faction": definitions.Factions[1].Id = "aven"; break;
                case "unknown-kind": definitions.Factions[0].Kind = (FactionKind)9; break;
                case "unknown-unit-faction": definitions.Units[3].RequiredFactionId = "missing"; break;
                case "unknown-building-faction": definitions.Buildings[1].RequiredFactionId = "missing"; break;
                case "unknown-technology-faction": definitions.Technologies[0].RequiredFactionId = "missing"; break;
                case "foreign-unique-unit-spawn": map.UnitSpawns[4].OwnerId = 2; break;
                case "foreign-unique-building-spawn": map.BuildingSpawns[3].OwnerId = 2; break;
                case "negative-carry-bonus": definitions.Factions[0].WorkerCarryBonus = -1; break;
                case "overflowing-carry-bonus": definitions.Factions[0].WorkerCarryBonus = int.MaxValue; break;
                case "negative-charter-cost": definitions.Factions[0].CharterCost = new ResourceAmount(-1, 0, 0, 0); break;
                case "zero-charter-duration": definitions.Factions[0].CharterTicks = 0; break;
                case "instant-prepared-relocation": definitions.Factions[1].PreparationReductionTicks = 3; break;
                case "unknown-linked-technology": definitions.Factions[0].ReciprocalStoresTechnologyId = "missing"; break;
                case "transport-health-mismatch": definitions.Units[5].MaxHealth = 301; break;
                case "transport-population": definitions.Units[5].PopulationCost = 1; break;
                case "transport-worker": definitions.Units[5].IsWorker = true; break;
                case "transport-armed": definitions.Units[5].Attack.Damage = 1; break;
                case "directly-trainable-transport": definitions.Buildings[0].TrainableUnitIds = new[] { "worker", "wagon" }; break;
                case "mobile-population-capacity": definitions.Buildings[2].PopulationCapacity = 1; break;
                default: throw new ArgumentException("Unknown fixture fault: " + fault);
            }
            Assert.Throws<ArgumentException>(() => new World(definitions, map), fault);
        }
    }
}
