using System;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    // Controlled rule fixtures. Shipped balance is tested separately from these deliberately simple values.
    public static class CombatTestWorldFactory
    {
        internal static AttackDefinition Attack(int damage = 10, int range = 1000, int cooldown = 5, int projectileSpeed = 0)
            => new AttackDefinition
            {
                Damage = damage, RangeMillimetres = range, AcquireRangeMillimetres = Math.Max(6000, range),
                CooldownTicks = cooldown, ProjectileSpeedMillimetresPerSecond = projectileSpeed,
                Bonuses = Array.Empty<DamageBonus>()
            };

        internal static UnitDefinition UnitDefinition(string id, int damage = 0) => new UnitDefinition
        {
            Id = id, DisplayName = id, MaxHealth = 200, MoveSpeedMillimetresPerSecond = 3000,
            RadiusMillimetres = 250, Tags = CombatTags.Infantry, Attack = Attack(damage),
            PopulationCost = 1, Cost = new ResourceAmount(10, 0, 0, 0), TrainTicks = 100,
            CarryCapacity = 10, GatherAmount = 2, GatherIntervalTicks = 2
        };

        internal static GameDefinition Definitions()
        {
            var worker = UnitDefinition("worker");
            worker.IsWorker = true; worker.Tags = CombatTags.Worker;
            return new GameDefinition
            {
                StartingResources = new ResourceAmount(1000, 1000, 1000, 1000), BasePopulationCapacity = 10,
                Units = new[] { UnitDefinition("striker", 10), UnitDefinition("dummy"), worker },
                Buildings = new[]
                {
                    new BuildingDefinition { Id = "hall", DisplayName = "Hall", MaxHealth = 500,
                        WidthCells = 3, DepthCells = 3, BuildTicks = 100, PopulationCapacity = 4,
                        CanDropOff = true, Cost = new ResourceAmount(0, 30, 0, 0),
                        TrainableUnitIds = new[] { "worker", "striker" } },
                    new BuildingDefinition { Id = "house", DisplayName = "House", MaxHealth = 200,
                        WidthCells = 1, DepthCells = 1, BuildTicks = 100, PopulationCapacity = 3,
                        Cost = new ResourceAmount(0, 20, 0, 0) }
                },
                Resources = new[] { new ResourceDefinition { Id = "wood", DisplayName = "Wood", Kind = ResourceKind.Wood, InitialAmount = 100 } }
            };
        }

        internal static UnitSpawnDefinition Spawn(int id, int owner, string definition, int x, int z)
            => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
        internal static BuildingSpawnDefinition BuildingSpawn(int id, int owner, string definition, int x, int z)
            => new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };

        internal static MapDefinition Map() => new MapDefinition
        {
            Id = "combat-rules", WidthCells = 32, HeightCells = 24, CellSizeMillimetres = 1000,
            UnitSpawns = new[] { Spawn(1, 1, "striker", 4500, 4500), Spawn(2, 2, "dummy", 5500, 4500), Spawn(3, 1, "worker", 1500, 1500) },
            BuildingSpawns = new[] { BuildingSpawn(100, 1, "hall", 3500, 9500), BuildingSpawn(101, 2, "hall", 23500, 18500) },
            ResourceSpawns = new[] { new ResourceSpawnDefinition { Id = 201, DefinitionId = "wood", Position = new SimPoint(4500, 12500) } }
        };

        internal static World Create() => new World(Definitions(), Map());
        internal static UnitState Unit(World world, int id = 1)
        { Assert.That(world.TryGetUnit(id, out var unit), Is.True, "Missing live unit " + id); return unit; }
        internal static BuildingState Building(World world, int id = 100)
        { Assert.That(world.TryGetBuilding(id, out var building), Is.True, "Missing live building " + id); return building; }
        internal static PlayerState Player(World world, int id = 1)
        { Assert.That(world.TryGetPlayer(id, out var player), Is.True); return player; }
        internal static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);
        internal static void Tick(World world, int count) { for (int i = 0; i < count; i++) world.Tick(); }
        internal static void Until(World world, Func<bool> predicate, string reason, int maximumTicks = 3000)
        {
            for (int i = 0; i < maximumTicks && !predicate(); i++) world.Tick();
            Assert.That(predicate(), Is.True, reason + " (tick " + world.TickIndex + ")");
        }

        // Explicit mirror of shipped Phase 3 balance. Root PlayMode checks load the actual authored JSON.
        // Each military unit costs 80 total resources, uses one population and trains in 160 ticks.
        public static GameDefinition ShippedCombatDefinitions()
        {
            var reedguard = UnitDefinition("reedguard", 12);
            reedguard.MaxHealth = 100; reedguard.Armor = 1; reedguard.MoveSpeedMillimetresPerSecond = 3600;
            reedguard.RadiusMillimetres = 300; reedguard.Tags = CombatTags.Infantry | CombatTags.Light;
            reedguard.Attack = Attack(12, 1100, 24);
            reedguard.Attack.Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Cavalry, MultiplierPermille = 1800 } };
            reedguard.Cost = new ResourceAmount(60, 20, 0, 0); reedguard.TrainTicks = 160;

            var stringwarden = UnitDefinition("stringwarden", 10);
            stringwarden.MaxHealth = 70; stringwarden.Armor = 0; stringwarden.MoveSpeedMillimetresPerSecond = 3200;
            stringwarden.RadiusMillimetres = 300; stringwarden.Tags = CombatTags.Ranged | CombatTags.Light;
            stringwarden.Attack = Attack(10, 5000, 20, 12000);
            stringwarden.Attack.Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Infantry, MultiplierPermille = 1800 } };
            stringwarden.Cost = new ResourceAmount(30, 50, 0, 0); stringwarden.TrainTicks = 160;

            var strider = UnitDefinition("strider", 12);
            strider.MaxHealth = 110; strider.Armor = 1; strider.MoveSpeedMillimetresPerSecond = 5000;
            strider.RadiusMillimetres = 350; strider.Tags = CombatTags.Cavalry | CombatTags.Light;
            strider.Attack = Attack(12, 900, 24);
            strider.Attack.Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Ranged, MultiplierPermille = 1300 } };
            strider.Cost = new ResourceAmount(80, 0, 0, 0); strider.TrainTicks = 160;

            return new GameDefinition { Units = new[] { reedguard, stringwarden, strider }, BasePopulationCapacity = 100 };
        }

        public static World CreateCounterBattle(string firstDefinition, int firstCount, string secondDefinition, int secondCount)
        {
            var units = new UnitSpawnDefinition[firstCount + secondCount];
            for (int i = 0; i < firstCount; i++) units[i] = Spawn(i + 1, 1, firstDefinition, 8500, 8500 + i * 1500);
            for (int i = 0; i < secondCount; i++) units[firstCount + i] = Spawn(i + 101, 2, secondDefinition, 15500, 8500 + i * 1500);
            var world = new World(ShippedCombatDefinitions(), new MapDefinition
            { Id = "shipped-combat-balance", WidthCells = 32, HeightCells = 24, CellSizeMillimetres = 1000, UnitSpawns = units });
            var firstIds = new int[firstCount]; var secondIds = new int[secondCount];
            for (int i = 0; i < firstCount; i++) firstIds[i] = i + 1;
            for (int i = 0; i < secondCount; i++) secondIds[i] = i + 101;
            Accepted(world.Submit(new AttackCommand(1, firstIds, 101)));
            Accepted(world.Submit(new AttackCommand(2, secondIds, 1)));
            return world;
        }

        public static int SurvivingUnits(World world, int owner)
        {
            int count = 0;
            foreach (var unit in world.Units) if (unit.OwnerId == owner) count++;
            return count;
        }
    }
}
