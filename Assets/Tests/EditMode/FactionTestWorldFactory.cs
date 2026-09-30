using System;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    // Fast invariant fixtures, intentionally independent of the authored faction balance.
    internal static class FactionTestWorldFactory
    {
        internal static GameDefinition Definitions()
        {
            var worker = CombatTestWorldFactory.UnitDefinition("worker");
            worker.IsWorker = true; worker.Tags = CombatTags.Worker;
            worker.CarryCapacity = 5; worker.GatherAmount = 2; worker.GatherIntervalTicks = 1;
            worker.MoveSpeedMillimetresPerSecond = 2000; worker.TrainTicks = 4;
            var spear = CombatTestWorldFactory.UnitDefinition("spear", 10);
            spear.Attack.CooldownTicks = 20; spear.MoveSpeedMillimetresPerSecond = 2000; spear.TrainTicks = 4;
            spear.Attack.Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Cavalry, MultiplierPermille = 1800 } };
            var cavalry = CombatTestWorldFactory.UnitDefinition("cavalry");
            cavalry.Tags = CombatTags.Cavalry | CombatTags.Light; cavalry.MoveSpeedMillimetresPerSecond = 2000;
            var thread = CombatTestWorldFactory.UnitDefinition("thread");
            thread.RequiredFactionId = "aven"; thread.IsThreadkeeper = true;
            var ash = CombatTestWorldFactory.UnitDefinition("ash", 10);
            ash.RequiredFactionId = "serevin"; ash.CanReposition = true; ash.Tags = CombatTags.Cavalry | CombatTags.Light;
            ash.MoveSpeedMillimetresPerSecond = 2000; ash.Attack.CooldownTicks = 1;
            var wagon = CombatTestWorldFactory.UnitDefinition("wagon");
            wagon.RequiredFactionId = "serevin"; wagon.MaxHealth = 300; wagon.PopulationCost = 0;
            wagon.MoveSpeedMillimetresPerSecond = 2000; wagon.Tags = CombatTags.Structure;
            return new GameDefinition
            {
                StartingResources = new ResourceAmount(1000, 1000, 1000, 1000), BasePopulationCapacity = 30,
                Units = new[] { worker, spear, cavalry, thread, ash, wagon },
                Buildings = new[]
                {
                    new BuildingDefinition { Id = "hall", DisplayName = "Hall", WidthCells = 3, DepthCells = 3,
                        MaxHealth = 300, CanDropOff = true, BuildTicks = 3, Cost = new ResourceAmount(0, 30, 0, 0),
                        TrainableUnitIds = new[] { "worker", "spear", "thread", "ash" } },
                    new BuildingDefinition { Id = "yard", DisplayName = "Yard", WidthCells = 1, DepthCells = 1,
                        MaxHealth = 300, CanDropOff = true, CanCharter = true, RequiredFactionId = "aven",
                        BuildTicks = 3, Cost = new ResourceAmount(0, 30, 0, 0), TrainableUnitIds = new[] { "worker" } },
                    new BuildingDefinition { Id = "outpost", DisplayName = "Outpost", WidthCells = 1, DepthCells = 1,
                        MaxHealth = 300, CanDropOff = true, RequiredFactionId = "serevin", TransportUnitId = "wagon",
                        BuildTicks = 3, Cost = new ResourceAmount(0, 40, 0, 0), TrainableUnitIds = new[] { "worker" } }
                },
                Resources = new[] { new ResourceDefinition { Id = "wood", DisplayName = "Wood", Kind = ResourceKind.Wood, InitialAmount = 100 } },
                Technologies = new[]
                {
                    new TechnologyDefinition { Id = "reciprocal", DisplayName = "Reciprocal", ResearchBuildingId = "hall",
                        RequiredFactionId = "aven", Cost = new ResourceAmount(10, 5, 0, 0), ResearchTicks = 2,
                        Effects = new[] { new TechnologyEffect { Kind = TechnologyEffectKind.Armor, TargetTags = CombatTags.Infantry, Amount = 1 } } },
                    new TechnologyDefinition { Id = "prepared", DisplayName = "Prepared", ResearchBuildingId = "hall",
                        RequiredFactionId = "serevin", Cost = new ResourceAmount(10, 5, 0, 0), ResearchTicks = 2,
                        Effects = new[] { new TechnologyEffect { Kind = TechnologyEffectKind.Armor, TargetTags = CombatTags.Cavalry, Amount = 1 } } },
                    new TechnologyDefinition { Id = "outpost-study", DisplayName = "Outpost study", ResearchBuildingId = "outpost",
                        RequiredFactionId = "serevin", Cost = new ResourceAmount(10, 0, 0, 0), ResearchTicks = 4 }
                },
                Factions = new[]
                {
                    new FactionDefinition { Id = "aven", DisplayName = "Aven", Kind = FactionKind.AvenCompact,
                        WorkerCarryBonus = 2, CharterCost = new ResourceAmount(10, 5, 0, 0), CharterTicks = 3,
                        CharterRadiusMillimetres = 6000, LogisticsCarryBonus = 3, MusterWorkBonusPermille = 500,
                        ReciprocalStoresTechnologyId = "reciprocal", ReciprocalRadiusBonusMillimetres = 2000,
                        ThreadkeeperDeployTicks = 4, ThreadkeeperCooldownTicks = 6, RelayRadiusMillimetres = 4000 },
                    new FactionDefinition { Id = "serevin", DisplayName = "Serevin", Kind = FactionKind.SerevinMarch,
                        CavalrySpeedBonusPermille = 50, PackTicks = 3, DeployTicks = 3,
                        PreparedEncampmentsTechnologyId = "prepared", PreparationReductionTicks = 1,
                        RepositionTicks = 3, RepositionCooldownTicks = 5, RepositionSpeedBonusPermille = 500 }
                }
            };
        }

        internal static MapDefinition Map() => new MapDefinition
        {
            Id = "faction-rules", WidthCells = 40, HeightCells = 32, CellSizeMillimetres = 1000,
            PlayerFactions = new[]
            {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = "aven" },
                new PlayerFactionDefinition { PlayerId = 2, FactionId = "serevin" },
                new PlayerFactionDefinition { PlayerId = 4, FactionId = "aven" }
            },
            UnitSpawns = new[]
            {
                CombatTestWorldFactory.Spawn(1, 1, "worker", 4500, 6500),
                CombatTestWorldFactory.Spawn(2, 1, "spear", 8500, 4500),
                CombatTestWorldFactory.Spawn(3, 2, "worker", 24500, 6500),
                CombatTestWorldFactory.Spawn(4, 2, "ash", 24500, 4500),
                CombatTestWorldFactory.Spawn(5, 1, "thread", 8500, 8500),
                CombatTestWorldFactory.Spawn(6, 1, "worker", 11500, 8500),
                CombatTestWorldFactory.Spawn(7, 3, "worker", 4500, 18500),
                CombatTestWorldFactory.Spawn(8, 3, "cavalry", 8500, 18500),
                CombatTestWorldFactory.Spawn(9, 2, "cavalry", 18500, 4500)
            },
            BuildingSpawns = new[]
            {
                CombatTestWorldFactory.BuildingSpawn(100, 1, "hall", 4500, 12500),
                CombatTestWorldFactory.BuildingSpawn(101, 2, "hall", 24500, 18500),
                CombatTestWorldFactory.BuildingSpawn(102, 3, "hall", 4500, 22500),
                CombatTestWorldFactory.BuildingSpawn(110, 1, "yard", 4500, 8500),
                CombatTestWorldFactory.BuildingSpawn(111, 4, "yard", 14500, 18500),
                CombatTestWorldFactory.BuildingSpawn(112, 1, "yard", 4500, 9500),
                CombatTestWorldFactory.BuildingSpawn(120, 2, "outpost", 24500, 12500)
            },
            ResourceSpawns = new[]
            {
                new ResourceSpawnDefinition { Id = 201, DefinitionId = "wood", Position = new SimPoint(6500, 6500) }
            }
        };

        internal static World Create() => new World(Definitions(), Map());
        internal static UnitState Unit(World world, int id = 1) => CombatTestWorldFactory.Unit(world, id);
        internal static BuildingState Building(World world, int id = 110) => CombatTestWorldFactory.Building(world, id);
        internal static PlayerState Player(World world, int id = 1) => CombatTestWorldFactory.Player(world, id);
        internal static void Accepted(CommandResult result) => CombatTestWorldFactory.Accepted(result);
        internal static void Tick(World world, int ticks) => CombatTestWorldFactory.Tick(world, ticks);
        internal static void Until(World world, Func<bool> predicate, string reason, int maximum = 500)
            => CombatTestWorldFactory.Until(world, predicate, reason, maximum);
        internal static void Charter(World world, StoreyardCharter charter, int yard = 110)
        {
            Accepted(world.Submit(new SetCharterCommand(1, yard, charter)));
            Until(world, () => Building(world, yard).ActiveCharter == charter, "Charter completes", 10);
        }
        internal static void Pack(World world, int id = 120)
        {
            Accepted(world.Submit(new PackOutpostCommand(2, id)));
            Until(world, () => world.TryGetUnit(id, out _), "Outpost packs", 10);
        }
        internal static void Research(World world, string technology, int owner = 1, int hall = 100)
        {
            Accepted(world.Submit(new ResearchCommand(owner, hall, technology)));
            Until(world, () => Player(world, owner).HasTechnology(technology), "Unique technology completes", 10);
        }
        internal static World WithAttacker(int damage, int targetBuilding = 120)
        {
            var definitions = Definitions();
            var attacker = CombatTestWorldFactory.UnitDefinition("attacker", damage);
            attacker.Attack.CooldownTicks = 100;
            var units = new UnitDefinition[definitions.Units.Length + 1];
            Array.Copy(definitions.Units, units, definitions.Units.Length); units[units.Length - 1] = attacker; definitions.Units = units;
            var map = Map();
            BuildingSpawnDefinition target = null;
            foreach (var building in map.BuildingSpawns) if (building.Id == targetBuilding) target = building;
            Assert.That(target, Is.Not.Null);
            var spawns = new UnitSpawnDefinition[map.UnitSpawns.Length + 1];
            Array.Copy(map.UnitSpawns, spawns, map.UnitSpawns.Length);
            spawns[spawns.Length - 1] = CombatTestWorldFactory.Spawn(20, 3, "attacker", target.Position.X + 1000, target.Position.Z);
            map.UnitSpawns = spawns;
            var world = new World(definitions, map);
            Accepted(world.Submit(new StopCommand(3, new[] { 20 })));
            return world;
        }
    }
}
