using System;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    // Small, deliberately simple rule fixtures; authored balance and UI are tested separately.
    internal static class TechnologyTestWorldFactory
    {
        internal static TechnologyDefinition Technology(string id, TechnologyEffectKind kind, CombatTags tags, int amount)
            => new TechnologyDefinition
            {
                Id = id, DisplayName = id, Description = "Controlled technology test fixture.",
                ResearchBuildingId = "hall", RequiredEraId = "camp", ResearchTicks = 3,
                Cost = new ResourceAmount(20, 10, 0, 0),
                Effects = new[] { new TechnologyEffect { Kind = kind, TargetTags = tags, Amount = amount } }
            };

        internal static TechnologyDefinition Advance(string id, string requiredEra, string nextEra, string previous = null)
            => new TechnologyDefinition
            {
                Id = id, DisplayName = id, ResearchBuildingId = "hall", RequiredEraId = requiredEra,
                AdvancesToEraId = nextEra, RequiredTechnologyIds = previous == null ? Array.Empty<string>() : new[] { previous },
                Cost = new ResourceAmount(20, 10, 0, 0), ResearchTicks = 3
            };

        internal static GameDefinition Definitions()
        {
            var definitions = CombatTestWorldFactory.Definitions();
            definitions.BasePopulationCapacity = 20;
            definitions.Units[0].MaxHealth = 100;
            definitions.Units[0].TrainTicks = 4;
            definitions.Units[0].Armor = 1;
            definitions.Units[0].Tags = CombatTags.Infantry | CombatTags.Light;
            definitions.Units[0].Attack.CooldownTicks = 20;
            definitions.Units[2].CarryCapacity = 5;
            definitions.Units[2].GatherAmount = 2;
            definitions.Units[2].GatherIntervalTicks = 2;
            definitions.Buildings[0].MaxHealth = 100;
            definitions.Buildings[0].BuildTicks = 100;
            definitions.Buildings[1].Id = "shed";
            var guard = CombatTestWorldFactory.UnitDefinition("guard", 10);
            guard.TrainTicks = 4;
            guard.RequiredEraId = "village";
            guard.RequiredTechnologyIds = new[] { "edge" };
            definitions.Units = new[] { definitions.Units[0], definitions.Units[1], definitions.Units[2], guard };
            definitions.Buildings[0].TrainableUnitIds = new[] { "worker", "striker", "guard" };
            var workshop = new BuildingDefinition
            {
                Id = "workshop", DisplayName = "Workshop", WidthCells = 1, DepthCells = 1,
                BuildTicks = 3, Cost = new ResourceAmount(0, 20, 0, 0),
                RequiredEraId = "village", RequiredTechnologyIds = new[] { "edge" }
            };
            definitions.Buildings = new[] { definitions.Buildings[0], definitions.Buildings[1], workshop };
            definitions.Eras = new[]
            {
                new EraDefinition { Id = "camp", DisplayName = "Camp", Tier = 1 },
                new EraDefinition { Id = "village", DisplayName = "Village", Tier = 2 },
                new EraDefinition { Id = "city", DisplayName = "City", Tier = 3 },
                new EraDefinition { Id = "citadel", DisplayName = "Citadel", Tier = 4 }
            };
            var city = Advance("advance-city", "village", "city", "advance-village");
            city.RequiredBuildingIds = new[] { "workshop" };
            var edge2 = Technology("edge2", TechnologyEffectKind.AttackDamage, CombatTags.Infantry, 4);
            edge2.RequiredTechnologyIds = new[] { "edge" };
            definitions.Technologies = new[]
            {
                Advance("advance-village", "camp", "village"), city,
                Advance("advance-citadel", "city", "citadel", "advance-city"),
                Technology("edge", TechnologyEffectKind.AttackDamage, CombatTags.Infantry, 3),
                Technology("plate", TechnologyEffectKind.Armor, CombatTags.Infantry, 2),
                Technology("harvest", TechnologyEffectKind.GatherAmount, CombatTags.Worker, 9), edge2
            };
            return definitions;
        }

        internal static TechnologyDefinition Tech(GameDefinition definitions, string id)
        {
            foreach (var technology in definitions.Technologies) if (technology.Id == id) return technology;
            throw new ArgumentException("Missing fixture technology " + id);
        }

        internal static MapDefinition Map() => new MapDefinition
        {
            Id = "technology-rules", WidthCells = 32, HeightCells = 24, CellSizeMillimetres = 1000,
            UnitSpawns = new[]
            {
                CombatTestWorldFactory.Spawn(1, 1, "worker", 4500, 4500),
                CombatTestWorldFactory.Spawn(2, 1, "striker", 8500, 4500),
                CombatTestWorldFactory.Spawn(3, 2, "worker", 27500, 15500)
            },
            BuildingSpawns = new[]
            {
                CombatTestWorldFactory.BuildingSpawn(100, 1, "hall", 4500, 8500),
                CombatTestWorldFactory.BuildingSpawn(101, 1, "hall", 18500, 8500),
                CombatTestWorldFactory.BuildingSpawn(102, 2, "hall", 24500, 18500),
                CombatTestWorldFactory.BuildingSpawn(103, 1, "shed", 8500, 14500)
            },
            ResourceSpawns = new[]
            {
                new ResourceSpawnDefinition { Id = 201, DefinitionId = "wood", Position = new SimPoint(6500, 4500) }
            }
        };

        internal static World Create() => new World(Definitions(), Map());
        internal static UnitState Unit(World world, int id = 1) => CombatTestWorldFactory.Unit(world, id);
        internal static BuildingState Building(World world, int id = 100) => CombatTestWorldFactory.Building(world, id);
        internal static PlayerState Player(World world, int id = 1) => CombatTestWorldFactory.Player(world, id);
        internal static void Accepted(CommandResult result) => CombatTestWorldFactory.Accepted(result);
        internal static void Tick(World world, int ticks) => CombatTestWorldFactory.Tick(world, ticks);
        internal static void Until(World world, Func<bool> predicate, string reason, int ticks = 500)
            => CombatTestWorldFactory.Until(world, predicate, reason, ticks);
        internal static void Research(World world, string technology, int building = 100, int player = 1)
        {
            Accepted(world.Submit(new ResearchCommand(player, building, technology)));
            Until(world, () => Player(world, player).HasTechnology(technology), "Research completes: " + technology, 20);
        }

        internal static World WithHallKiller()
        {
            var definitions = Definitions();
            definitions.Units[1].Attack = CombatTestWorldFactory.Attack(1000);
            var map = Map();
            map.UnitSpawns = new[] { map.UnitSpawns[0], map.UnitSpawns[1], map.UnitSpawns[2],
                CombatTestWorldFactory.Spawn(4, 2, "dummy", 6500, 8500) };
            var world = new World(definitions, map);
            Accepted(world.Submit(new StopCommand(2, new[] { 4 })));
            return world;
        }

        internal static void DestroyHall(World world)
        {
            Accepted(world.Submit(new AttackCommand(2, new[] { 4 }, 100)));
            world.Tick();
            Assert.That(world.TryGetBuilding(100, out _), Is.False, "Controlled lethal attack destroys the producer.");
            Accepted(world.Submit(new StopCommand(2, new[] { 4 })));
        }
    }
}
