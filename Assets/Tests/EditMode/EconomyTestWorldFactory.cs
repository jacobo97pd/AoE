using System;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    internal static class EconomyTestWorldFactory
    {
        internal static GameDefinition Definitions() => new GameDefinition
        {
            StartingResources = new ResourceAmount(100, 100, 100, 100),
            BasePopulationCapacity = 10,
            MaxProductionQueue = 3,
            Units = new[]
            {
                new UnitDefinition
                {
                    Id = "worker", DisplayName = "Tender", IsWorker = true,
                    MoveSpeedMillimetresPerSecond = 10000, RadiusMillimetres = 200,
                    Cost = new ResourceAmount(4, 0, 0, 0), PopulationCost = 1, TrainTicks = 4,
                    CarryCapacity = 5, GatherIntervalTicks = 2, GatherAmount = 2
                },
                new UnitDefinition
                {
                    Id = "soldier", DisplayName = "Warden", IsWorker = false,
                    MoveSpeedMillimetresPerSecond = 10000, RadiusMillimetres = 200,
                    Cost = new ResourceAmount(3, 2, 1, 1), PopulationCost = 2, TrainTicks = 6
                },
                new UnitDefinition
                {
                    Id = "scout", DisplayName = "Wayfinder", IsWorker = false,
                    MoveSpeedMillimetresPerSecond = 10000, RadiusMillimetres = 200,
                    Cost = new ResourceAmount(1, 1, 0, 0), PopulationCost = 1, TrainTicks = 3
                }
            },
            Buildings = new[]
            {
                new BuildingDefinition
                {
                    Id = "hall", DisplayName = "Gathering Hall", WidthCells = 1, DepthCells = 1,
                    BuildTicks = 8, CanDropOff = true, TrainableUnitIds = new[] { "worker" }
                },
                new BuildingDefinition
                {
                    Id = "barracks", DisplayName = "Warden Lodge", WidthCells = 2, DepthCells = 2,
                    Cost = new ResourceAmount(0, 8, 2, 1), BuildTicks = 8,
                    TrainableUnitIds = new[] { "soldier", "scout" }
                },
                new BuildingDefinition
                {
                    Id = "house", DisplayName = "Hearth", WidthCells = 1, DepthCells = 1,
                    Cost = new ResourceAmount(0, 5, 0, 0), BuildTicks = 8, PopulationCapacity = 4
                },
                new BuildingDefinition
                {
                    Id = "depot", DisplayName = "Store", WidthCells = 1, DepthCells = 1,
                    Cost = new ResourceAmount(0, 3, 0, 0), BuildTicks = 8, CanDropOff = true
                }
            },
            Resources = new[]
            {
                new ResourceDefinition { Id = "food", Kind = ResourceKind.Food, InitialAmount = 21 },
                new ResourceDefinition { Id = "wood", Kind = ResourceKind.Wood, InitialAmount = 21 },
                new ResourceDefinition { Id = "metal", Kind = ResourceKind.Metal, InitialAmount = 21 },
                new ResourceDefinition { Id = "stone", Kind = ResourceKind.Stone, InitialAmount = 21 }
            }
        };

        internal static MapDefinition Map() => new MapDefinition
        {
            Id = "economy-field", WidthCells = 18, HeightCells = 14, CellSizeMillimetres = 1000,
            UnitSpawns = new[]
            {
                new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(1500, 1500) },
                new UnitSpawnDefinition { Id = 2, OwnerId = 1, DefinitionId = "worker", Position = new SimPoint(1500, 2500) },
                new UnitSpawnDefinition { Id = 3, OwnerId = 2, DefinitionId = "worker", Position = new SimPoint(15500, 1500) },
                new UnitSpawnDefinition { Id = 4, OwnerId = 1, DefinitionId = "soldier", Position = new SimPoint(3500, 1500) }
            },
            BuildingSpawns = new[]
            {
                new BuildingSpawnDefinition { Id = 101, OwnerId = 1, DefinitionId = "hall", Position = new SimPoint(2500, 10500) },
                new BuildingSpawnDefinition { Id = 102, OwnerId = 2, DefinitionId = "hall", Position = new SimPoint(15500, 10500) }
            },
            ResourceSpawns = new[]
            {
                new ResourceSpawnDefinition { Id = 201, DefinitionId = "food", Position = new SimPoint(7500, 2500) },
                new ResourceSpawnDefinition { Id = 202, DefinitionId = "wood", Position = new SimPoint(7500, 5500) },
                new ResourceSpawnDefinition { Id = 203, DefinitionId = "metal", Position = new SimPoint(10500, 2500) },
                new ResourceSpawnDefinition { Id = 204, DefinitionId = "stone", Position = new SimPoint(10500, 5500) }
            }
        };

        internal static World Create() => new World(Definitions(), Map());

        internal static PlayerState Player(World world, int id = 1)
        {
            Assert.That(world.TryGetPlayer(id, out var value), Is.True, "Expected player " + id);
            return value;
        }

        internal static UnitState Unit(World world, int id = 1)
        {
            Assert.That(world.TryGetUnit(id, out var value), Is.True, "Expected unit " + id);
            return value;
        }

        internal static BuildingState Building(World world, int id = 101)
        {
            Assert.That(world.TryGetBuilding(id, out var value), Is.True, "Expected building " + id);
            return value;
        }

        internal static ResourceNodeState Resource(World world, int id)
        {
            Assert.That(world.TryGetResource(id, out var value), Is.True, "Expected resource " + id);
            return value;
        }

        internal static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);

        internal static void Tick(World world, int count)
        {
            for (int i = 0; i < count; i++) world.Tick();
        }

        internal static void Until(World world, Func<bool> condition, string description, int limit = 600)
        {
            for (int i = 0; i < limit && !condition(); i++) world.Tick();
            Assert.That(condition(), Is.True, description + " within " + limit + " ticks (tick " + world.TickIndex + ")");
        }

        internal static void Inventory(ResourceAmount actual, int food, int wood, int metal, int stone)
        {
            Assert.That(actual.Food, Is.EqualTo(food), "Food inventory");
            Assert.That(actual.Wood, Is.EqualTo(wood), "Wood inventory");
            Assert.That(actual.Metal, Is.EqualTo(metal), "Metal inventory");
            Assert.That(actual.Stone, Is.EqualTo(stone), "Stone inventory");
        }

        internal static MapDefinition WithBarracks()
        {
            var map = Map();
            map.BuildingSpawns = new[]
            {
                map.BuildingSpawns[0], map.BuildingSpawns[1],
                new BuildingSpawnDefinition { Id = 103, OwnerId = 1, DefinitionId = "barracks", Position = new SimPoint(5000, 8000) }
            };
            return map;
        }
    }
}
