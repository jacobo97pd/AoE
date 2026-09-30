using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.EconomyTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class EconomyTests
    {
        [Test]
        public void PlayersStartWithIndependentInventoriesAndPopulationFromOwnedUnits()
        {
            var world = Create();
            Assert.That(world.Players.Count, Is.EqualTo(2));
            Inventory(Player(world).Resources, 100, 100, 100, 100);
            Assert.That(Player(world).PopulationUsed, Is.EqualTo(4));
            Assert.That(Player(world, 2).PopulationUsed, Is.EqualTo(1));
            Assert.That(Player(world).PopulationReserved, Is.Zero);
            Assert.That(Player(world).PopulationCapacity, Is.EqualTo(10));
            Assert.That(Building(world).IsComplete, Is.True);

            Accepted(world.Submit(new TrainCommand(1, 101, "worker")));
            Inventory(Player(world).Resources, 96, 100, 100, 100);
            Inventory(Player(world, 2).Resources, 100, 100, 100, 100);
        }

        [TestCase(ResourceKind.Food, 201)]
        [TestCase(ResourceKind.Wood, 202)]
        [TestCase(ResourceKind.Metal, 203)]
        [TestCase(ResourceKind.Stone, 204)]
        public void EveryResourceDepletesIntoMatchingInventoryIncludingPartialLastLoad(ResourceKind kind, int nodeId)
        {
            var definitions = Definitions();
            definitions.StartingResources = default;
            foreach (var resource in definitions.Resources) resource.InitialAmount = 7;
            var world = new World(definitions, Map());
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, nodeId)));

            Until(world, () => Resource(world, nodeId).RemainingAmount == 0 && worker.CarriedAmount == 0 &&
                worker.WorkerTask == WorkerTask.None, "Depletion, partial-load delivery, and idle transition");

            foreach (ResourceKind other in Enum.GetValues(typeof(ResourceKind)))
                Assert.That(Player(world).Resources.Get(other), Is.EqualTo(other == kind ? 7 : 0));
            Assert.That(Resource(world, nodeId).RemainingAmount, Is.Zero);
            Assert.That(worker.Order, Is.EqualTo(UnitOrder.Idle));
            Tick(world, 80);
            Assert.That(Player(world).Resources.Get(kind), Is.EqualTo(7), "Depletion must not duplicate income");
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, nodeId)).Accepted, Is.False);
        }

        [Test]
        public void DepletedResourceCellBecomesTraversableAndBuildableWhileKeepingItsEntityIdentity()
        {
            var definitions = Definitions();
            definitions.Resources[1].InitialAmount = 1;
            var world = new World(definitions, Map());
            var resource = Resource(world, 202);
            var site = resource.Position;
            Assert.That(world.IsWalkable(site), Is.False);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, resource.Id)));
            Until(world, () => resource.RemainingAmount == 0 && Unit(world).CarriedAmount == 0 &&
                Unit(world).WorkerTask == WorkerTask.None, "Last resource delivered");

            Assert.That(world.IsWalkable(site), Is.True);
            Assert.That(Resource(world, 202), Is.SameAs(resource));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, site)));
            Until(world, () => Unit(world).Position == site, "Worker traverses the exhausted node cell");
            Assert.That(world.ValidatePlacement(1, new[] { 2 }, "house", site).Accepted, Is.False,
                "The worker still prevents placement while occupying the cleared cell");
            var exit = new SimPoint(4500, 3500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, exit)));
            Until(world, () => Unit(world).Position == exit, "Worker vacates the cleared cell");
            Accepted(world.ValidatePlacement(1, new[] { 2 }, "house", site));
            var result = world.Submit(new BuildCommand(1, new[] { 2 }, "house", site));
            Accepted(result);
            Assert.That(Building(world, result.EntityId).Position, Is.EqualTo(site));
            Assert.That(Resource(world, 202), Is.SameAs(resource));
            Assert.That(resource.RemainingAmount, Is.Zero);
        }

        [Test]
        public void HarvestIsBoundedByCarryCapacityAndInventoryIsCreditedOnlyAtDropOffArrival()
        {
            var definitions = Definitions();
            definitions.StartingResources = default;
            var world = new World(definitions, Map());
            var worker = Unit(world);
            var node = Resource(world, 202);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, node.Id)));
            Until(world, () => worker.CarriedAmount > 0, "First harvest");
            Assert.That(worker.CarriedKind, Is.EqualTo(ResourceKind.Wood));
            Assert.That(worker.CarriedAmount, Is.EqualTo(2));
            Assert.That(Player(world).Resources.Wood, Is.Zero);
            Assert.That(node.RemainingAmount + worker.CarriedAmount, Is.EqualTo(21));

            Until(world, () => worker.WorkerTask == WorkerTask.ReturningResources, "Full load return");
            Assert.That(worker.CarriedAmount, Is.EqualTo(5), "A two-unit harvest must clamp to the last free carry slot");
            Assert.That(Player(world).Resources.Wood, Is.Zero);
            Assert.That(worker.Order, Is.EqualTo(UnitOrder.Moving));
            Until(world, () => Player(world).Resources.Wood > 0, "Arrival at an owned drop-off");
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(5));
            Assert.That(worker.CarriedAmount, Is.Zero);
            long dx = worker.Position.X - Building(world).Position.X;
            long dz = worker.Position.Z - Building(world).Position.Z;
            Assert.That(dx * dx + dz * dz, Is.LessThanOrEqualTo(1500L * 1500L));
        }

        [Test]
        public void GatherIntervalDoesNotProduceResourcesOnEverySimulationTick()
        {
            var definitions = Definitions();
            definitions.Units[0].GatherIntervalTicks = 5;
            var world = new World(definitions, Map());
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => worker.CarriedAmount > 0, "First timed harvest");
            int remaining = Resource(world, 202).RemainingAmount;
            Tick(world, 4);
            Assert.That(Resource(world, 202).RemainingAmount, Is.EqualTo(remaining));
            world.Tick();
            Assert.That(Resource(world, 202).RemainingAmount, Is.EqualTo(remaining - 2));
        }

        [Test]
        public void MoveAndStopReassignmentKeepCargoWithoutCreditingItRemotely()
        {
            var world = Create();
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => worker.CarriedAmount > 0, "Worker carries gathered wood");
            int carried = worker.CarriedAmount;
            int remaining = Resource(world, 202).RemainingAmount;
            var destination = new SimPoint(4500, 3500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, destination)));
            Assert.That(worker.WorkerTask, Is.EqualTo(WorkerTask.None));
            Until(world, () => worker.Position == destination, "Explicit move completes");
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Tick(world, 40);
            Assert.That(worker.CarriedAmount, Is.EqualTo(carried));
            Assert.That(worker.CarriedKind, Is.EqualTo(ResourceKind.Wood));
            Assert.That(Resource(world, 202).RemainingAmount, Is.EqualTo(remaining));
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(100));
        }

        [Test]
        public void ReturnCargoDeliversStoppedLastLoadAfterAllResourceNodesAreExhausted()
        {
            var definitions = Definitions();
            definitions.StartingResources = default;
            definitions.Resources[1].InitialAmount = 3;
            var map = Map();
            map.ResourceSpawns = new[] { map.ResourceSpawns[1] };
            var world = new World(definitions, map);
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => Resource(world, 202).RemainingAmount == 0, "Last load harvested");
            Assert.That(worker.CarriedAmount, Is.EqualTo(3));
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Tick(world, 20);
            Assert.That(Player(world).Resources.Wood, Is.Zero);
            Assert.That(worker.CarriedAmount, Is.EqualTo(3));
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, 202)).Accepted, Is.False);

            var stoppedAt = worker.Position;
            Accepted(world.Submit(new ReturnCargoCommand(1, new[] { 1 }, 0)));
            Assert.That(worker.Position, Is.EqualTo(stoppedAt), "Submitting a return must not teleport cargo");
            Assert.That(worker.WorkerTask, Is.EqualTo(WorkerTask.ReturningResources));
            Assert.That(worker.TargetResourceId, Is.Zero);
            Assert.That(worker.TargetBuildingId, Is.EqualTo(101));
            Assert.That(Player(world).Resources.Wood, Is.Zero);
            world.Tick();
            Assert.That(Player(world).Resources.Wood, Is.Zero, "Inventory changes only after physical arrival");
            Assert.That(worker.CarriedAmount, Is.EqualTo(3));
            Until(world, () => worker.CarriedAmount == 0, "Stopped cargo arrives at the nearest own drop-off");
            Inventory(Player(world).Resources, 0, 3, 0, 0);
            Assert.That(worker.WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(worker.Order, Is.EqualTo(UnitOrder.Idle));
            Tick(world, 20);
            Inventory(Player(world).Resources, 0, 3, 0, 0);
        }

        [Test]
        public void ReturnCargoToForeignDropOffRejectsWithoutChangingAssignmentsOrInventory()
        {
            var world = Create();
            var carrier = Unit(world);
            var emptyWorker = Unit(world, 2);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => carrier.CarriedAmount > 0, "Worker holds cargo before rejected return");
            var emptyDestination = new SimPoint(12500, 3500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, emptyDestination)));
            var originalTask = carrier.WorkerTask;
            var originalDestination = carrier.Destination;
            int originalTarget = carrier.TargetResourceId;
            int originalDropOff = carrier.TargetBuildingId;
            int originalCargo = carrier.CarriedAmount;

            Assert.That(world.Submit(new ReturnCargoCommand(1, new[] { 1, 2 }, 102)).Accepted, Is.False);

            Assert.That(carrier.WorkerTask, Is.EqualTo(originalTask));
            Assert.That(carrier.Destination, Is.EqualTo(originalDestination));
            Assert.That(carrier.TargetResourceId, Is.EqualTo(originalTarget));
            Assert.That(carrier.TargetBuildingId, Is.EqualTo(originalDropOff));
            Assert.That(carrier.CarriedAmount, Is.EqualTo(originalCargo));
            Assert.That(emptyWorker.Destination, Is.EqualTo(emptyDestination));
            Assert.That(emptyWorker.Order, Is.EqualTo(UnitOrder.Moving));
            Inventory(Player(world).Resources, 100, 100, 100, 100);
            Inventory(Player(world, 2).Resources, 100, 100, 100, 100);
        }

        [Test]
        public void ReturnCargoLeavesSelectedEmptyWorkersMovingTowardTheirExistingDestination()
        {
            var world = Create();
            var carrier = Unit(world);
            var emptyWorker = Unit(world, 2);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => carrier.CarriedAmount > 0, "Worker holds cargo before explicit return");
            int cargo = carrier.CarriedAmount;
            int remaining = Resource(world, 202).RemainingAmount;
            var emptyDestination = new SimPoint(16500, 12500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, emptyDestination)));
            Assert.That(emptyWorker.CarriedAmount, Is.Zero);

            Accepted(world.Submit(new ReturnCargoCommand(1, new[] { 1, 2 }, 101)));

            Assert.That(emptyWorker.WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(emptyWorker.Order, Is.EqualTo(UnitOrder.Moving));
            Assert.That(emptyWorker.Destination, Is.EqualTo(emptyDestination));
            Assert.That(carrier.TargetResourceId, Is.Zero);
            Assert.That(carrier.WorkerTask, Is.EqualTo(WorkerTask.ReturningResources));
            Until(world, () => carrier.CarriedAmount == 0, "Selected carrier completes explicit delivery");
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(100 + cargo));
            Assert.That(carrier.WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(emptyWorker.Destination, Is.EqualTo(emptyDestination));
            Until(world, () => emptyWorker.Position == emptyDestination, "Empty worker completes its preserved movement order");
            Assert.That(Resource(world, 202).RemainingAmount, Is.EqualTo(remaining), "Explicit return must end the previous gathering assignment");
            Assert.That(carrier.CarriedAmount, Is.Zero);
        }

        [Test]
        public void ChangingResourceKindDepositsExistingCargoBeforeHarvestingAnotherKind()
        {
            var world = Create();
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => worker.CarriedAmount > 0, "Wood cargo acquired");
            int wood = worker.CarriedAmount;
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201)));
            Assert.That(worker.CarriedKind, Is.EqualTo(ResourceKind.Wood));
            Assert.That(worker.CarriedAmount, Is.EqualTo(wood));
            while (Player(world).Resources.Wood == 100 && world.TickIndex < 600)
            {
                Assert.That(Resource(world, 201).RemainingAmount, Is.EqualTo(21), "Food cannot be mixed into wood cargo");
                world.Tick();
            }
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(100 + wood));
            Until(world, () => worker.CarriedAmount > 0 && worker.CarriedKind == ResourceKind.Food, "Harvest of replacement target");
            Assert.That(Player(world).Resources.Food, Is.EqualTo(100));
        }

        [Test]
        public void GatherRejectsMixedOwnershipAndNonWorkersWithoutChangingValidWorkers()
        {
            var world = Create();
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            var task = worker.WorkerTask;
            var destination = worker.Destination;
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1, 3 }, 201)).Accepted, Is.False);
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1, 4 }, 201)).Accepted, Is.False);
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, 999)).Accepted, Is.False);
            Assert.That(worker.TargetResourceId, Is.EqualTo(202));
            Assert.That(worker.WorkerTask, Is.EqualTo(task));
            Assert.That(worker.Destination, Is.EqualTo(destination));
            Assert.That(Unit(world, 3).WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(Unit(world, 4).WorkerTask, Is.EqualTo(WorkerTask.None));
            Inventory(Player(world).Resources, 100, 100, 100, 100);
        }

        [Test]
        public void EnemyDropOffDoesNotMakeGatheringValid()
        {
            var map = Map();
            map.BuildingSpawns = new[] { map.BuildingSpawns[1] };
            var world = new World(Definitions(), map);
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, 202)).Accepted, Is.False);
            Assert.That(Unit(world).WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(Resource(world, 202).RemainingAmount, Is.EqualTo(21));
        }

        [Test]
        public void UnreachableOwnDropOffRejectsGatheringBeforeHarvestStarts()
        {
            var map = Map();
            map.BuildingSpawns[0].Position = new SimPoint(2500, 11500);
            map.BlockedCells = new GridCell[map.WidthCells];
            for (int x = 0; x < map.WidthCells; x++) map.BlockedCells[x] = new GridCell(x, 9);
            var world = new World(Definitions(), map);
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, 202)).Accepted, Is.False);
            Assert.That(Unit(world).WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(Resource(world, 202).RemainingAmount, Is.EqualTo(21));
        }

        [Test]
        public void NearestReachableOwnedDropOffIsSelected()
        {
            var map = Map();
            map.BuildingSpawns = new[]
            {
                map.BuildingSpawns[0], map.BuildingSpawns[1],
                new BuildingSpawnDefinition { Id = 103, OwnerId = 1, DefinitionId = "depot", Position = new SimPoint(5500, 5500) }
            };
            var world = new World(Definitions(), map);
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => worker.WorkerTask == WorkerTask.ReturningResources, "Return to nearest own depot");
            Assert.That(worker.TargetBuildingId, Is.EqualTo(103));
        }

        [Test]
        public void FullInventoryRetainsUndeliverableCargoAndCreditsItAfterSpaceBecomesAvailable()
        {
            var definitions = Definitions();
            definitions.StartingResources = new ResourceAmount(0, int.MaxValue - 1, 0, 0);
            definitions.Resources[1].InitialAmount = 3;
            var world = new World(definitions, Map());
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => Player(world).Resources.Wood == int.MaxValue, "Bounded delivery fills inventory");
            Assert.That(worker.CarriedAmount, Is.EqualTo(2), "Undelivered cargo must not overflow or disappear");
            Tick(world, 20);
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(int.MaxValue));
            Assert.That(worker.CarriedAmount, Is.EqualTo(2));
            Accepted(world.Submit(new BuildCommand(1, new[] { 2 }, "house", new SimPoint(11500, 10500))));
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(int.MaxValue - 5));
            Until(world, () => worker.CarriedAmount == 0, "Retained cargo delivered after spending");
            Inventory(Player(world).Resources, 0, int.MaxValue - 3, 0, 0);
        }

        [Test]
        public void PlacementPreviewDoesNotSpendCreateOrReplaceWorkerOrders()
        {
            var world = Create();
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            var destination = worker.Destination;
            for (int i = 0; i < 10; i++)
                Accepted(world.ValidatePlacement(1, new[] { 1 }, "barracks", new SimPoint(5000, 8000)));
            Assert.That(world.Buildings.Count, Is.EqualTo(2));
            Assert.That(world.TickIndex, Is.Zero);
            Inventory(Player(world).Resources, 100, 100, 100, 100);
            Assert.That(worker.TargetResourceId, Is.EqualTo(202));
            Assert.That(worker.Destination, Is.EqualTo(destination));
            Assert.That(world.IsWalkable(new SimPoint(4500, 7500)), Is.True);
        }

        [Test]
        public void BuildingDeductsAllCostsOnceAndFoundationBlocksItsFootprintImmediately()
        {
            var world = Create();
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "barracks", new SimPoint(5000, 8000)));
            Accepted(result);
            Assert.That(result.EntityId, Is.GreaterThan(204));
            var building = Building(world, result.EntityId);
            Assert.That(building.IsComplete, Is.False);
            Assert.That(building.BuildProgressTicks, Is.Zero);
            Assert.That(building.ConstructionProgress, Is.Zero);
            Assert.That(building.BuildRequiredTicks, Is.EqualTo(8));
            Inventory(Player(world).Resources, 100, 92, 98, 99);
            Assert.That(world.IsWalkable(new SimPoint(4500, 7500)), Is.False);
            Assert.That(world.IsWalkable(new SimPoint(5500, 8500)), Is.False);
            Assert.That(world.Submit(new TrainCommand(1, building.Id, "soldier")).Accepted, Is.False);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 2 }, "barracks", building.Position)).Accepted, Is.False);
            Inventory(Player(world).Resources, 100, 92, 98, 99);
            Until(world, () => building.IsComplete, "Construction completion");
            Assert.That(building.BuildProgressTicks, Is.EqualTo(building.BuildRequiredTicks));
            Assert.That(building.ConstructionProgress, Is.EqualTo(1f));
            Assert.That(Unit(world).WorkerTask, Is.EqualTo(WorkerTask.None));
            Tick(world, 30);
            Inventory(Player(world).Resources, 100, 92, 98, 99);
        }

        [TestCase(0, 0)]
        [TestCase(18000, 14000)]
        [TestCase(4501, 7500)]
        [TestCase(2500, 10500)]
        [TestCase(7500, 5500)]
        [TestCase(1500, 1500)]
        public void InvalidFootprintsRejectWithoutSpendingOrChangingWorkerOrders(int x, int z)
        {
            var world = Create();
            var destination = new SimPoint(4500, 3500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, destination)));
            var target = new SimPoint(x, z);
            Assert.That(world.ValidatePlacement(1, new[] { 1 }, "house", target).Accepted, Is.False);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "house", target)).Accepted, Is.False);
            Assert.That(world.Buildings.Count, Is.EqualTo(2));
            Inventory(Player(world).Resources, 100, 100, 100, 100);
            Assert.That(Unit(world).Destination, Is.EqualTo(destination));
            Assert.That(Unit(world).Order, Is.EqualTo(UnitOrder.Moving));
        }

        [Test]
        public void UnaffordableAndMixedOwnershipConstructionRejectAtomically()
        {
            var definitions = Definitions();
            definitions.StartingResources = new ResourceAmount(100, 8, 1, 100);
            var world = new World(definitions, Map());
            var target = new SimPoint(5000, 8000);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "barracks", target)).Accepted, Is.False);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1, 3 }, "house", new SimPoint(11500, 10500))).Accepted, Is.False);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1, 4 }, "house", new SimPoint(11500, 10500))).Accepted, Is.False);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "unknown", target)).Accepted, Is.False);
            Inventory(Player(world).Resources, 100, 8, 1, 100);
            Assert.That(world.Buildings.Count, Is.EqualTo(2));
            Assert.That(Unit(world).WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(world.IsWalkable(target), Is.True);
        }

        [Test]
        public void UnreachableBuilderMakesConstructionGroupFailWithoutCreatingFoundation()
        {
            var map = Map();
            map.BlockedCells = new GridCell[map.HeightCells];
            for (int z = 0; z < map.HeightCells; z++) map.BlockedCells[z] = new GridCell(6, z);
            map.UnitSpawns[1].Position = new SimPoint(9500, 11500);
            var world = new World(Definitions(), map);
            var target = new SimPoint(5000, 8000);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1, 2 }, "barracks", target)).Accepted, Is.False);
            Assert.That(world.Buildings.Count, Is.EqualTo(2));
            Inventory(Player(world).Resources, 100, 100, 100, 100);
            Assert.That(Unit(world).WorkerTask, Is.EqualTo(WorkerTask.None));
            Assert.That(Unit(world, 2).WorkerTask, Is.EqualTo(WorkerTask.None));
        }

        [Test]
        public void ConstructionCanBeStoppedAndResumedWithoutPayingAgain()
        {
            var definitions = Definitions();
            definitions.Buildings[1].BuildTicks = 40;
            var world = new World(definitions, Map());
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "barracks", new SimPoint(5000, 8000)));
            Accepted(result);
            var building = Building(world, result.EntityId);
            Until(world, () => building.BuildProgressTicks > 0, "First construction work");
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            int progress = building.BuildProgressTicks;
            Tick(world, 25);
            Assert.That(building.BuildProgressTicks, Is.EqualTo(progress));
            Assert.That(building.IsComplete, Is.False);
            Assert.That(world.Submit(new ConstructCommand(2, new[] { 3 }, building.Id)).Accepted, Is.False);
            Accepted(world.Submit(new ConstructCommand(1, new[] { 2 }, building.Id)));
            Until(world, () => building.IsComplete, "A replacement worker completes the foundation");
            Inventory(Player(world).Resources, 100, 92, 98, 99);
            Assert.That(world.Submit(new ConstructCommand(1, new[] { 1 }, building.Id)).Accepted, Is.False);
        }

        [Test]
        public void EachAdjacentBuilderContributesOneWorkTick()
        {
            var definitions = Definitions();
            definitions.Buildings[1].BuildTicks = 60;
            var map = Map();
            map.UnitSpawns[0].Position = new SimPoint(3500, 7500);
            map.UnitSpawns[1].Position = new SimPoint(3500, 8500);
            var world = new World(definitions, map);
            var result = world.Submit(new BuildCommand(1, new[] { 2, 1 }, "barracks", new SimPoint(5000, 8000)));
            Accepted(result);
            var building = Building(world, result.EntityId);
            Until(world, () => Unit(world).WorkerTask == WorkerTask.Constructing &&
                Unit(world, 2).WorkerTask == WorkerTask.Constructing, "Both workers arrive at the foundation");
            int progress = building.BuildProgressTicks;
            world.Tick();
            Assert.That(building.BuildProgressTicks - progress, Is.EqualTo(2));
        }

        [Test]
        public void ConstructionReassignmentPreservesGatheredCargo()
        {
            var world = Create();
            var worker = Unit(world);
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => worker.CarriedAmount > 0, "Wood cargo acquired");
            int cargo = worker.CarriedAmount;
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(11500, 10500)));
            Accepted(result);
            Until(world, () => Building(world, result.EntityId).IsComplete, "Worker constructs while retaining cargo");
            Assert.That(worker.CarriedAmount, Is.EqualTo(cargo));
            Assert.That(worker.CarriedKind, Is.EqualTo(ResourceKind.Wood));
            Assert.That(Player(world).Resources.Wood, Is.EqualTo(95));
        }

        [Test]
        public void HousingAddsPopulationOnlyOnCompletionAndAllowsTrainingAfterInitialOverCap()
        {
            var definitions = Definitions();
            definitions.BasePopulationCapacity = 3;
            var world = new World(definitions, Map());
            Assert.That(Player(world).PopulationUsed, Is.EqualTo(4));
            Assert.That(world.Submit(new TrainCommand(1, 101, "worker")).Accepted, Is.False);
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "house", new SimPoint(11500, 10500)));
            Accepted(result);
            Assert.That(Player(world).PopulationCapacity, Is.EqualTo(3));
            Assert.That(world.Submit(new TrainCommand(1, 101, "worker")).Accepted, Is.False);
            Until(world, () => Building(world, result.EntityId).IsComplete, "Housing becomes usable");
            Assert.That(Player(world).PopulationCapacity, Is.EqualTo(7));
            Accepted(world.Submit(new TrainCommand(1, 101, "worker")));
            Tick(world, 20);
            Assert.That(Player(world).PopulationCapacity, Is.EqualTo(7), "Completed housing must not repeatedly grant capacity");
        }

        [Test]
        public void IncompleteDepotCannotReceiveResourcesButCompletedDepotCan()
        {
            var map = Map();
            map.BuildingSpawns = new[] { map.BuildingSpawns[1] };
            var world = new World(Definitions(), map);
            var result = world.Submit(new BuildCommand(1, new[] { 2 }, "depot", new SimPoint(5500, 5500)));
            Accepted(result);
            Assert.That(world.Submit(new GatherCommand(1, new[] { 1 }, 202)).Accepted, Is.False);
            Until(world, () => Building(world, result.EntityId).IsComplete, "Own depot completes");
            Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 202)));
            Until(world, () => Player(world).Resources.Wood > 97, "Income arrives at the new depot");
            Assert.That(Player(world, 2).Resources.Wood, Is.EqualTo(100));
        }

        [Test]
        public void NewFoundationRepathsExistingMovementWithoutCrossingItsFootprint()
        {
            var map = Map();
            map.UnitSpawns[0].Position = new SimPoint(1500, 7500);
            var world = new World(Definitions(), map);
            var destination = new SimPoint(12500, 7500);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, destination)));
            var result = world.Submit(new BuildCommand(1, new[] { 2 }, "barracks", new SimPoint(5000, 8000)));
            Accepted(result);
            for (int tick = 0; tick < 120; tick++)
            {
                world.Tick();
                var point = Unit(world).Position;
                Assert.That(world.IsWalkable(point), Is.True, "An old route entered a new foundation at tick " + tick);
                Assert.That(point.X >= 4000 && point.X < 6000 && point.Z >= 7000 && point.Z < 9000,
                    Is.False, "A moving worker clipped the new building footprint");
            }
            Assert.That(Unit(world).Position, Is.EqualTo(destination));
        }

        [Test]
        public void TrainingChargesAndReservesImmediatelyThenSpawnsExactlyOnceAfterRequiredTicks()
        {
            var world = new World(Definitions(), WithBarracks());
            var player = Player(world);
            int initialCount = world.Units.Count;
            Accepted(world.Submit(new TrainCommand(1, 103, "soldier")));
            Inventory(player.Resources, 97, 98, 99, 99);
            Assert.That(player.PopulationUsed, Is.EqualTo(4));
            Assert.That(player.PopulationReserved, Is.EqualTo(2));
            Assert.That(Building(world, 103).ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(Building(world, 103).ProductionQueue[0].RemainingTicks, Is.EqualTo(6));
            Tick(world, 5);
            Assert.That(world.Units.Count, Is.EqualTo(initialCount));
            world.Tick();
            Assert.That(world.Units.Count, Is.EqualTo(initialCount + 1));
            Assert.That(player.PopulationUsed, Is.EqualTo(6));
            Assert.That(player.PopulationReserved, Is.Zero);
            Assert.That(Building(world, 103).ProductionQueue.Count, Is.Zero);
            Assert.That(world.Units[world.Units.Count - 1].DefinitionId, Is.EqualTo("soldier"));
            Tick(world, 30);
            Assert.That(world.Units.Count, Is.EqualTo(initialCount + 1));
            Inventory(player.Resources, 97, 98, 99, 99);
        }

        [Test]
        public void QueueIsFifoAndOnlyHeadEntryAdvances()
        {
            var world = new World(Definitions(), WithBarracks());
            Accepted(world.Submit(new TrainCommand(1, 103, "soldier")));
            Accepted(world.Submit(new TrainCommand(1, 103, "scout")));
            var building = Building(world, 103);
            Assert.That(building.ProductionQueue[0].UnitDefinitionId, Is.EqualTo("soldier"));
            Assert.That(building.ProductionQueue[1].UnitDefinitionId, Is.EqualTo("scout"));
            Tick(world, 5);
            Assert.That(building.ProductionQueue[0].RemainingTicks, Is.EqualTo(1));
            Assert.That(building.ProductionQueue[1].RemainingTicks, Is.EqualTo(3));
            world.Tick();
            Assert.That(world.Units[world.Units.Count - 1].DefinitionId, Is.EqualTo("soldier"));
            Assert.That(building.ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(building.ProductionQueue[0].UnitDefinitionId, Is.EqualTo("scout"));
            Assert.That(building.ProductionQueue[0].RemainingTicks, Is.EqualTo(3));
            Assert.That(Player(world).PopulationReserved, Is.EqualTo(1));
            Tick(world, 3);
            Assert.That(world.Units[world.Units.Count - 1].DefinitionId, Is.EqualTo("scout"));
            Assert.That(building.ProductionQueue.Count, Is.Zero);
            Assert.That(Player(world).PopulationUsed, Is.EqualTo(7));
            Assert.That(Player(world).PopulationReserved, Is.Zero);
        }

        [Test]
        public void PopulationReservationPreventsOversubscriptionAcrossProductionBuildings()
        {
            var definitions = Definitions();
            definitions.BasePopulationCapacity = 6;
            var world = new World(definitions, WithBarracks());
            Accepted(world.Submit(new TrainCommand(1, 103, "soldier")));
            Assert.That(world.Submit(new TrainCommand(1, 101, "worker")).Accepted, Is.False);
            Inventory(Player(world).Resources, 97, 98, 99, 99);
            Assert.That(Player(world).PopulationReserved, Is.EqualTo(2));
            Assert.That(Building(world).ProductionQueue.Count, Is.Zero);
            Tick(world, 6);
            Assert.That(Player(world).PopulationUsed, Is.EqualTo(6));
            Assert.That(Player(world).PopulationReserved, Is.Zero);
            Assert.That(world.Submit(new TrainCommand(1, 101, "worker")).Accepted, Is.False);
        }

        [Test]
        public void FullQueueAndInvalidTrainingDoNotChargeOrReservePopulation()
        {
            var definitions = Definitions();
            definitions.BasePopulationCapacity = 30;
            var world = new World(definitions, WithBarracks());
            for (int i = 0; i < 3; i++) Accepted(world.Submit(new TrainCommand(1, 103, "scout")));
            Assert.That(world.Submit(new TrainCommand(1, 103, "scout")).Accepted, Is.False);
            Assert.That(world.Submit(new TrainCommand(1, 102, "worker")).Accepted, Is.False);
            Assert.That(world.Submit(new TrainCommand(1, 101, "soldier")).Accepted, Is.False);
            Assert.That(world.Submit(new TrainCommand(1, 103, "unknown")).Accepted, Is.False);
            Assert.That(world.Submit(new TrainCommand(1, 999, "worker")).Accepted, Is.False);
            Inventory(Player(world).Resources, 97, 97, 100, 100);
            Assert.That(Player(world).PopulationReserved, Is.EqualTo(3));
            Assert.That(Building(world, 103).ProductionQueue.Count, Is.EqualTo(3));
            Assert.That(Building(world, 102).ProductionQueue.Count, Is.Zero);
        }

        [Test]
        public void UnaffordableTrainingDoesNotPartiallyDeductOtherResourceKinds()
        {
            var definitions = Definitions();
            definitions.StartingResources = new ResourceAmount(100, 100, 0, 100);
            var world = new World(definitions, WithBarracks());
            Assert.That(world.Submit(new TrainCommand(1, 103, "soldier")).Accepted, Is.False);
            Inventory(Player(world).Resources, 100, 100, 0, 100);
            Assert.That(Player(world).PopulationReserved, Is.Zero);
            Assert.That(Building(world, 103).ProductionQueue.Count, Is.Zero);
        }

        [Test]
        public void BlockedSpawnWaitsWithPaidCostAndReservationThenSpawnsWhenWorkerMovesAway()
        {
            var map = Map();
            map.UnitSpawns[0].Position = new SimPoint(2500, 9500);
            var blocked = new List<GridCell>();
            for (int z = 9; z <= 11; z++)
            for (int x = 1; x <= 3; x++)
                if (!(x == 2 && (z == 9 || z == 10))) blocked.Add(new GridCell(x, z));
            map.BlockedCells = blocked.ToArray();
            var world = new World(Definitions(), map);
            Accepted(world.Submit(new TrainCommand(1, 101, "worker")));
            Tick(world, 20);
            Assert.That(world.Units.Count, Is.EqualTo(4));
            Assert.That(Building(world).ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(Building(world).ProductionQueue[0].RemainingTicks, Is.Zero);
            Assert.That(Player(world).PopulationReserved, Is.EqualTo(1));
            Inventory(Player(world).Resources, 96, 100, 100, 100);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(2500, 8500))));
            Until(world, () => world.Units.Count == 5, "Completed training spawns after exit clears");
            Assert.That(Building(world).ProductionQueue.Count, Is.Zero);
            Assert.That(Player(world).PopulationReserved, Is.Zero);
            Assert.That(Player(world).PopulationUsed, Is.EqualTo(5));
            Assert.That(world.Units[4].Position, Is.Not.EqualTo(Unit(world).Position));
            Tick(world, 20);
            Assert.That(world.Units.Count, Is.EqualTo(5));
        }

        [Test]
        public void RallyPointIsOwnedValidatedAndAppliedToNewlyTrainedUnit()
        {
            var world = new World(Definitions(), WithBarracks());
            var destination = new SimPoint(12500, 8500);
            Accepted(world.Submit(new SetRallyCommand(1, 103, destination)));
            Assert.That(Building(world, 103).HasRallyPoint, Is.True);
            Assert.That(Building(world, 103).RallyPoint, Is.EqualTo(destination));
            Assert.That(world.Submit(new SetRallyCommand(2, 103, new SimPoint(13500, 8500))).Accepted, Is.False);
            Assert.That(world.Submit(new SetRallyCommand(1, 103, new SimPoint(-1, 8500))).Accepted, Is.False);
            Assert.That(world.Submit(new SetRallyCommand(1, 103, Building(world, 103).Position)).Accepted, Is.False);
            Assert.That(Building(world, 103).RallyPoint, Is.EqualTo(destination));
            Accepted(world.Submit(new TrainCommand(1, 103, "soldier")));
            Until(world, () => world.Units.Count == 5, "Rallied unit finishes training");
            var trained = world.Units[4];
            Assert.That(trained.Destination, Is.EqualTo(destination));
            Until(world, () => trained.Position == destination, "Trained unit reaches rally point");
        }

        [Test]
        public void ResourceGatheringFundsProductionBuildingAndThenTrainedUnit()
        {
            var definitions = Definitions();
            definitions.StartingResources = default;
            var world = new World(definitions, Map());
            var site = new SimPoint(5000, 8000);
            Assert.That(world.Submit(new BuildCommand(1, new[] { 1 }, "barracks", site)).Accepted, Is.False);
            var needs = new[] { 3, 10, 3, 2 };
            for (int i = 0; i < needs.Length; i++)
            {
                ResourceKind kind = (ResourceKind)i;
                Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 201 + i)));
                Until(world, () => Player(world).Resources.Get(kind) >= needs[i], "Enough " + kind + " delivered for building and training");
                Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            }
            var result = world.Submit(new BuildCommand(1, new[] { 1 }, "barracks", site));
            Accepted(result);
            var building = Building(world, result.EntityId);
            Until(world, () => building.IsComplete, "Gathered materials become a production building");
            Accepted(world.Submit(new TrainCommand(1, building.Id, "soldier")));
            Until(world, () => world.Units.Count == 5, "Production building trains the first funded soldier");
            Assert.That(world.Units[4].DefinitionId, Is.EqualTo("soldier"));
            Assert.That(world.Units[4].OwnerId, Is.EqualTo(1));
            Assert.That(world.Units[4].Id, Is.GreaterThan(building.Id));
            Assert.That(Player(world).PopulationUsed, Is.EqualTo(6));
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
                Assert.That(Player(world).Resources.Get(kind), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ReplayedEconomyCommandsProduceIdenticalStateAndEntityIds()
        {
            var a = Create();
            var b = Create();
            for (int tick = 0; tick < 180; tick++)
            {
                if (tick == 0)
                {
                    Accepted(a.Submit(new GatherCommand(1, new[] { 1 }, 202)));
                    Accepted(b.Submit(new GatherCommand(1, new[] { 1 }, 202)));
                }
                if (tick == 5)
                {
                    var first = a.Submit(new BuildCommand(1, new[] { 2 }, "house", new SimPoint(11500, 10500)));
                    var second = b.Submit(new BuildCommand(1, new[] { 2 }, "house", new SimPoint(11500, 10500)));
                    Accepted(first); Accepted(second);
                    Assert.That(first.EntityId, Is.EqualTo(second.EntityId));
                }
                if (tick == 50 || tick == 90)
                {
                    Accepted(a.Submit(new TrainCommand(1, 101, "worker")));
                    Accepted(b.Submit(new TrainCommand(1, 101, "worker")));
                }
                a.Tick(); b.Tick();
                Assert.That(a.Units.Count, Is.EqualTo(b.Units.Count));
                Assert.That(Player(a).PopulationUsed, Is.EqualTo(Player(b).PopulationUsed));
                Assert.That(Player(a).PopulationReserved, Is.EqualTo(Player(b).PopulationReserved));
                Assert.That(Player(a).Resources, Is.EqualTo(Player(b).Resources));
                for (int i = 0; i < a.Units.Count; i++)
                {
                    Assert.That(a.Units[i].Id, Is.EqualTo(b.Units[i].Id));
                    Assert.That(a.Units[i].Position, Is.EqualTo(b.Units[i].Position));
                    Assert.That(a.Units[i].CarriedAmount, Is.EqualTo(b.Units[i].CarriedAmount));
                    Assert.That(a.Units[i].WorkerTask, Is.EqualTo(b.Units[i].WorkerTask));
                }
                for (int i = 0; i < a.Resources.Count; i++)
                    Assert.That(a.Resources[i].RemainingAmount, Is.EqualTo(b.Resources[i].RemainingAmount));
                for (int i = 0; i < a.Buildings.Count; i++)
                    Assert.That(a.Buildings[i].BuildProgressTicks, Is.EqualTo(b.Buildings[i].BuildProgressTicks));
            }
        }

        [TestCase("starting-resources")]
        [TestCase("unit-cost")]
        [TestCase("building-cost")]
        [TestCase("train-ticks")]
        [TestCase("build-ticks")]
        [TestCase("carry-capacity")]
        [TestCase("gather-interval")]
        [TestCase("gather-amount")]
        [TestCase("queue-capacity")]
        [TestCase("unknown-trainable")]
        public void InvalidEconomyConfigurationFailsBeforeWorldStarts(string invalidField)
        {
            var definitions = Definitions();
            switch (invalidField)
            {
                case "starting-resources": definitions.StartingResources = new ResourceAmount(-1, 0, 0, 0); break;
                case "unit-cost": definitions.Units[0].Cost = new ResourceAmount(0, -1, 0, 0); break;
                case "building-cost": definitions.Buildings[0].Cost = new ResourceAmount(0, 0, 0, -1); break;
                case "train-ticks": definitions.Units[0].TrainTicks = 0; break;
                case "build-ticks": definitions.Buildings[0].BuildTicks = 0; break;
                case "carry-capacity": definitions.Units[0].CarryCapacity = 0; break;
                case "gather-interval": definitions.Units[0].GatherIntervalTicks = 0; break;
                case "gather-amount": definitions.Units[0].GatherAmount = 0; break;
                case "queue-capacity": definitions.MaxProductionQueue = 0; break;
                case "unknown-trainable": definitions.Buildings[0].TrainableUnitIds = new[] { "missing" }; break;
            }
            Assert.Throws<ArgumentException>(() => new World(definitions, Map()));
        }
    }
}
