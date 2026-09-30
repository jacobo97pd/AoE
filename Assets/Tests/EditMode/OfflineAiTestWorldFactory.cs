using System;
using System.Collections.Generic;
using Emberfield.Simulation;

namespace Emberfield.Tests.EditMode
{
    internal static class OfflineAiTestWorldFactory
    {
        internal static GameDefinition Definitions() => WithStock(new ResourceAmount(120, 80, 0, 40));
        internal static GameDefinition WithStock(ResourceAmount stock) => new GameDefinition
        {
            StartingResources = stock, BasePopulationCapacity = 0,
            Units = new[]
            {
                new UnitDefinition { Id = "tender", Tags = CombatTags.Worker, IsWorker = true, MoveSpeedMillimetresPerSecond = 3200, RadiusMillimetres = 300,
                    Cost = new ResourceAmount(10, 0, 0, 0), TrainTicks = 4, GatherAmount = 2, GatherIntervalTicks = 2, CarryCapacity = 10 },
                new UnitDefinition { Id = "reedguard", Tags = CombatTags.Infantry, MoveSpeedMillimetresPerSecond = 3200, RadiusMillimetres = 300,
                    Cost = new ResourceAmount(15, 5, 0, 0), TrainTicks = 8, Attack = new AttackDefinition { Damage = 12, RangeMillimetres = 1100, AcquireRangeMillimetres = 6000,
                        Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Cavalry, MultiplierPermille = 1800 } } } }
            },
            Buildings = new[]
            {
                new BuildingDefinition { Id = "hearth", WidthCells = 3, DepthCells = 3, MaxHealth = 500, CanDropOff = true, PopulationCapacity = 6, TrainableUnitIds = new[] { "tender" } },
                new BuildingDefinition { Id = "shelter", WidthCells = 1, DepthCells = 1, Cost = new ResourceAmount(0, 20, 0, 0), BuildTicks = 8, PopulationCapacity = 5 },
                new BuildingDefinition { Id = "muster_hall", WidthCells = 3, DepthCells = 3, Cost = new ResourceAmount(0, 40, 0, 10), BuildTicks = 10, TrainableUnitIds = new[] { "reedguard" } },
                new BuildingDefinition { Id = "storeyard", WidthCells = 1, DepthCells = 1, Cost = new ResourceAmount(0, 10, 0, 0), BuildTicks = 8, CanDropOff = true }
            },
            Resources = new[]
            {
                new ResourceDefinition { Id = "food", Kind = ResourceKind.Food, InitialAmount = 2000 },
                new ResourceDefinition { Id = "wood", Kind = ResourceKind.Wood, InitialAmount = 2000 },
                new ResourceDefinition { Id = "metal", Kind = ResourceKind.Metal, InitialAmount = 2000 },
                new ResourceDefinition { Id = "stone", Kind = ResourceKind.Stone, InitialAmount = 2000 }
            }
        };
        internal static MapDefinition Map() => new MapDefinition
        {
            Id = "ai_invariant", WidthCells = 64, HeightCells = 48,
            OfflineMatch = new OfflineMatchDefinition { Enabled = true, VisionUnitCells = 5, VisionBuildingCells = 6 },
            BuildingSpawns = new[]
            {
                new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(10500, 10500) },
                new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(54500, 37500) }
            },
            UnitSpawns = new[]
            {
                Spawn(1, 1, 12500, 8500), Spawn(2, 1, 13500, 8500), Spawn(3, 1, 14500, 8500), Spawn(4, 1, 15500, 8500), Spawn(11, 2, 50500, 37500)
            },
            ResourceSpawns = new[]
            {
                Resource(200,"food",14500,10500), Resource(201,"wood",8500,14500), Resource(202,"metal",16500,14500), Resource(203,"stone",14500,16500), Resource(204,"food",57500,39500)
            }
        };
        internal static MapDefinition WithArmy(int count)
        {
            var map = Map(); var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            for (int i = 0; i < count; i++) units.Add(new UnitSpawnDefinition { Id = 20 + i, OwnerId = 1, DefinitionId = "reedguard", Position = new SimPoint(18500 + i * 1000, 8500) });
            map.UnitSpawns = units.ToArray(); return map;
        }
        internal static World World() => new World(Definitions(), Map());
        private static UnitSpawnDefinition Spawn(int id, int owner, int x, int z) => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = "tender", Position = new SimPoint(x, z) };
        private static ResourceSpawnDefinition Resource(int id, string definition, int x, int z) => new ResourceSpawnDefinition { Id = id, DefinitionId = definition, Position = new SimPoint(x, z) };
    }
}
