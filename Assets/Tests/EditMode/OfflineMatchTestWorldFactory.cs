using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    internal static class OfflineMatchTestWorldFactory
    {
        internal static GameDefinition Definitions() => new GameDefinition
        {
            StartingResources = new ResourceAmount(500, 500, 500, 500), BasePopulationCapacity = 20,
            Units = new[]
            {
                new UnitDefinition { Id = "worker", IsWorker = true, Tags = CombatTags.Worker, MoveSpeedMillimetresPerSecond = 2000 },
                new UnitDefinition { Id = "scout", Tags = CombatTags.Light, VisionCells = 5, MoveSpeedMillimetresPerSecond = 4000 },
                new UnitDefinition { Id = "guard", MaxHealth = 500, Tags = CombatTags.Infantry, VisionCells = 2,
                    MoveSpeedMillimetresPerSecond = 2000, Attack = new AttackDefinition { Damage = 10, RangeMillimetres = 1000, AcquireRangeMillimetres = 15000, CooldownTicks = 20 },
                    Cost = new ResourceAmount(20, 0, 0, 0), TrainTicks = 3 },
                new UnitDefinition { Id = "enemy", MaxHealth = 500, Tags = CombatTags.Infantry, VisionCells = 2, MoveSpeedMillimetresPerSecond = 6000 }
            },
            Buildings = new[]
            {
                new BuildingDefinition { Id = "hearth", MaxHealth = 100, WidthCells = 1, DepthCells = 1,
                    Cost = new ResourceAmount(0, 20, 0, 0), BuildTicks = 10000, CanDropOff = true, TrainableUnitIds = new[] { "guard" } },
                new BuildingDefinition { Id = "house", WidthCells = 1, DepthCells = 1, BuildTicks = 3, Cost = new ResourceAmount(0, 20, 0, 0) }
            },
            Resources = new[] { new ResourceDefinition { Id = "wood", Kind = ResourceKind.Wood, InitialAmount = 100 } }
        };

        internal static MapDefinition Map(VictoryMode mode = VictoryMode.Conquest) => new MapDefinition
        {
            Id = "offline-invariants", WidthCells = 40, HeightCells = 32,
            UnitSpawns = new[]
            {
                Spawn(1, 1, "worker", 4500, 8500), Spawn(2, 1, "scout", 6500, 12500),
                Spawn(3, 1, "guard", 7500, 8500), Spawn(4, 2, "enemy", 18500, 8500), Spawn(5, 2, "worker", 28500, 20500)
            },
            BuildingSpawns = new[]
            {
                new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(4500, 4500) },
                new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(28500, 24500) }
            },
            ResourceSpawns = new[]
            {
                new ResourceSpawnDefinition { Id = 201, DefinitionId = "wood", Position = new SimPoint(2500, 10500) },
                new ResourceSpawnDefinition { Id = 202, DefinitionId = "wood", Position = new SimPoint(32500, 8500) }
            },
            OfflineMatch = new OfflineMatchDefinition
            {
                Enabled = true, Mode = mode, VisionUnitCells = 3, VisionBuildingCells = 3, CaptureTicks = 3, ContinuousHoldTicks = 1000,
                Objectives = mode == VictoryMode.Conquest ? Array.Empty<DominionObjectiveDefinition>() : new[]
                {
                    new DominionObjectiveDefinition { Id = "west", DisplayName = "West", Position = new SimPoint(16500, 8500), RadiusMillimetres = 1000 },
                    new DominionObjectiveDefinition { Id = "middle", DisplayName = "Middle", Position = new SimPoint(16500, 16500), RadiusMillimetres = 1000 },
                    new DominionObjectiveDefinition { Id = "east", DisplayName = "East", Position = new SimPoint(24500, 16500), RadiusMillimetres = 1000 }
                }
            }
        };

        internal static World Create(GameDefinition definitions = null, MapDefinition map = null)
        {
            var world = new World(definitions ?? Definitions(), map ?? Map());
            foreach (var player in world.Players)
            {
                var ids = new List<int>();
                foreach (var unit in world.Units) if (unit.OwnerId == player.Id) ids.Add(unit.Id);
                if (ids.Count > 0) Accepted(world.Submit(new StopCommand(player.Id, ids.ToArray())));
            }
            return world;
        }
        internal static UnitSpawnDefinition Spawn(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
        internal static UnitState Unit(World world, int id) { Assert.That(world.TryGetUnit(id, out var unit), Is.True); return unit; }
        internal static BuildingState Building(World world, int id) { Assert.That(world.TryGetBuilding(id, out var building), Is.True); return building; }
        internal static PlayerState Player(World world, int id = 1) { Assert.That(world.TryGetPlayer(id, out var player), Is.True); return player; }
        internal static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Reason + ": " + result.Message);
        internal static void Tick(World world, int ticks) { for (int i = 0; i < ticks; i++) world.Tick(); }
        internal static void Until(World world, Func<bool> condition, int ticks = 1000)
        { for (int i = 0; i < ticks && !condition(); i++) world.Tick(); Assert.That(condition(), Is.True, "Bounded simulation condition did not become true."); }

        internal static World LethalBattle(bool twoAttackers = false)
        {
            var definitions = Definitions();
            foreach (int index in twoAttackers ? new[] { 2, 3 } : new[] { 3 })
            {
                definitions.Units[index].Attack = new AttackDefinition { Damage = 200, RangeMillimetres = 50000, AcquireRangeMillimetres = 50000 };
                definitions.Units[index].VisionCells = 128;
            }
            return Create(definitions);
        }

        internal static MapDefinition DominionMap(int holdTicks = 1000)
        {
            var map = Map(VictoryMode.Dominion);
            map.OfflineMatch.ContinuousHoldTicks = holdTicks;
            map.UnitSpawns[2].Position = map.OfflineMatch.Objectives[0].Position;
            map.UnitSpawns[4] = Spawn(5, 1, "guard", 16500, 16500);
            map.UnitSpawns[3].Position = new SimPoint(24500, 16500);
            return map;
        }
    }
}
