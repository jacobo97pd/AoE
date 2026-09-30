using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class ExpansionSimulationTests
    {
        private static UnitDefinition Soldier(string id, bool ranged = false) => new UnitDefinition
        {
            Id = id, MaxHealth = 200, Tags = ranged ? CombatTags.Ranged : CombatTags.Infantry,
            Attack = new AttackDefinition { Damage = 20, RangeMillimetres = ranged ? 5000 : 1000,
                AcquireRangeMillimetres = 6500, CooldownTicks = 20, ProjectileSpeedMillimetresPerSecond = ranged ? 10000 : 0 }
        };
        private static GameDefinition Definitions()
        {
            var ram = Soldier("ram"); ram.Tags = CombatTags.Siege; ram.SiegeEquipment = SiegeEquipmentKind.Ram;
            ram.Attack.Damage = 100; ram.Attack.Bonuses = new[] { new DamageBonus { TargetTags = CombatTags.Structure, MultiplierPermille = 3000 } };
            var ladder = Soldier("ladder"); ladder.Tags = CombatTags.Siege; ladder.Attack.Damage = 0; ladder.SiegeEquipment = SiegeEquipmentKind.Ladder;
            var tower = Soldier("siege_tower"); tower.Tags = CombatTags.Siege; tower.Attack.Damage = 0; tower.SiegeEquipment = SiegeEquipmentKind.Tower;
            return new GameDefinition
            {
                BasePopulationCapacity = 100,
                StartingResources = new ResourceAmount(1000, 1000, 1000, 1000),
                Units = new[] { Soldier("soldier"), Soldier("archer", true), ram, ladder, tower },
                Buildings = new[]
                {
                    new BuildingDefinition { Id = "wall", WidthCells = 3, DepthCells = 1, IsWall = true, MaxHealth = 500, WallCapacity = 4 },
                    new BuildingDefinition { Id = "gate", WidthCells = 2, DepthCells = 1, IsGate = true },
                    new BuildingDefinition { Id = "watchtower", WidthCells = 2, DepthCells = 2, Attack = new AttackDefinition
                        { Damage = 15, RangeMillimetres = 6000, AcquireRangeMillimetres = 6000, ProjectileSpeedMillimetresPerSecond = 10000 } }
                },
                Factions = new[]
                {
                    new FactionDefinition { Id = "aven", Kind = FactionKind.AvenCompact },
                    new FactionDefinition { Id = "serevin", Kind = FactionKind.SerevinMarch },
                    new FactionDefinition { Id = "solar", Kind = FactionKind.SolarKingdom, RealmId = ContentRealms.Fantasy },
                    new FactionDefinition { Id = "ashen", Kind = FactionKind.AshenDominion, RealmId = ContentRealms.Fantasy }
                }
            };
        }
        private static UnitSpawnDefinition U(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
        private static BuildingSpawnDefinition B(int id, int owner, string definition, int x, int z) =>
            new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
        private static void Tick(World world, int ticks) { for (int i = 0; i < ticks; i++) world.Tick(); }
        private static UnitState Unit(World world, int id) { Assert.That(world.TryGetUnit(id, out var value), Is.True); return value; }
        private static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);

        [TestCase("aven", "solar", "historical")]
        [TestCase("solar", "aven", "fantasy")]
        [TestCase("aven", "serevin", "fantasy")]
        public void CrossRealmWorldCreationIsRejected(string first, string second, string realm)
        {
            var map = new MapDefinition { RealmId = realm, PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = first }, new PlayerFactionDefinition { PlayerId = 2, FactionId = second } } };
            Assert.Throws<ArgumentException>(() => new World(Definitions(), map));
        }

        [TestCase("historical", "aven", "serevin")]
        [TestCase("fantasy", "solar", "ashen")]
        public void SameRealmWorldIsAccepted(string realm, string first, string second)
        {
            var world = new World(Definitions(), new MapDefinition { RealmId = realm, PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = first }, new PlayerFactionDefinition { PlayerId = 2, FactionId = second } } });
            Assert.That(world.Players.Count, Is.EqualTo(2));
        }

        [Test]
        public void FactionCannotRelabelFantasyAsHistorical()
        {
            var definitions = Definitions(); definitions.Factions[2].RealmId = ContentRealms.Historical;
            Assert.Throws<ArgumentException>(() => new World(definitions, new MapDefinition()));
        }

        [Test]
        public void DefendersClimbAndGroundMeleeCannotStrikeTheDeck()
        {
            var world = new World(Definitions(), new MapDefinition { BuildingSpawns = new[] { B(100, 1, "wall", 7500, 6500) },
                UnitSpawns = new[] { U(1, 1, "archer", 7500, 4500), U(2, 2, "soldier", 7500, 8500) } });
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100)));
            Tick(world, 39); Assert.That(Unit(world, 1).WallId, Is.Zero);
            world.Tick(); Assert.That(Unit(world, 1).WallId, Is.EqualTo(100));
            Assert.That(Unit(world, 1).ElevationMillimetres, Is.EqualTo(3000));
            Assert.That(world.Submit(new AttackCommand(2, new[] { 2 }, 1)).Accepted, Is.False);
            Tick(world, 25); Assert.That(Unit(world, 2).Health, Is.LessThan(200), "Wall archers must fire actual projectiles at ground enemies.");
            Accepted(world.Submit(new LeaveWallCommand(1, new[] { 1 }, new SimPoint(7500, 4500))));
            Assert.That(Unit(world, 1).WallId, Is.Zero); Assert.That(world.IsWalkable(Unit(world, 1).Position), Is.True);
        }

        [TestCase("ladder", 100)]
        [TestCase("siege_tower", 40)]
        public void AssaultEquipmentBoardsEnemyWallsAfterItsDistinctClimbDuration(string equipment, int duration)
        {
            var world = new World(Definitions(), new MapDefinition { BuildingSpawns = new[] { B(100, 2, "wall", 7500, 6500) },
                UnitSpawns = new[] { U(1, 1, "soldier", 7500, 4500), U(2, 1, equipment, 6500, 4500) } });
            Assert.That(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100)).Accepted, Is.False);
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100, 2)));
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(3500, 4500))).Accepted, Is.False);
            Tick(world, duration - 1); Assert.That(Unit(world, 1).WallId, Is.Zero);
            world.Tick(); Assert.That(Unit(world, 1).WallId, Is.EqualTo(100));
            Accepted(world.Submit(new LeaveWallCommand(1, new[] { 1 }, new SimPoint(7500, 8500))));
            Assert.That(Unit(world, 1).Position.Z, Is.GreaterThan(6500), "A completed assault crosses a live fortification through the deck.");
        }

        [Test]
        public void MovingAssaultEquipmentCancelsBoarding()
        {
            var world = new World(Definitions(), new MapDefinition { BuildingSpawns = new[] { B(100, 2, "wall", 7500, 6500) },
                UnitSpawns = new[] { U(1, 1, "soldier", 7500, 4500), U(2, 1, "ladder", 6500, 4500) } });
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100, 2)));
            Accepted(world.Submit(new MoveCommand(1, new[] { 2 }, new SimPoint(3500, 4500))));
            Tick(world, 100); Assert.That(Unit(world, 1).WallId, Is.Zero); Assert.That(Unit(world, 1).BoardingWallId, Is.Zero);
        }

        [Test]
        public void RamBreachOpensGroundNavigationAndWallCollapseEvacuatesDefenders()
        {
            var world = new World(Definitions(), new MapDefinition { BuildingSpawns = new[] { B(100, 2, "wall", 7500, 6500) },
                UnitSpawns = new[] { U(1, 1, "ram", 7500, 8500), U(2, 2, "archer", 7500, 4500) } });
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Accepted(world.Submit(new BoardWallCommand(2, new[] { 2 }, 100))); Tick(world, 40);
            Assert.That(world.IsWalkable(new SimPoint(7500, 6500)), Is.False);
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 100))); Tick(world, 60);
            Assert.That(world.TryGetBuilding(100, out _), Is.False);
            Assert.That(world.IsWalkable(new SimPoint(7500, 6500)), Is.True);
            Assert.That(Unit(world, 2).WallId, Is.Zero); Assert.That(Unit(world, 2).Health, Is.LessThan(200));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(7500, 4500)))); Tick(world, 70);
            Assert.That(Unit(world, 1).Position.Z, Is.LessThan(6500));
        }

        [Test]
        public void GateOpeningCreatesAPathAndClosingRejectsOccupiedDoorway()
        {
            var blocked = new List<GridCell>(); for (int z = 0; z < 14; z++) if (z != 6) blocked.Add(new GridCell(5, z));
            var world = new World(Definitions(), new MapDefinition { WidthCells = 14, HeightCells = 14, BlockedCells = blocked.ToArray(),
                BuildingSpawns = new[] { B(100, 1, "gate", 6000, 6500) }, UnitSpawns = new[] { U(1, 1, "soldier", 4500, 6500) } });
            Assert.That(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(8500, 6500))).Accepted, Is.False);
            Assert.That(world.Submit(new SetGateCommand(2, 100, true)).Accepted, Is.False);
            Accepted(world.Submit(new SetGateCommand(1, 100, true)));
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(5500, 6500)))); Tick(world, 30);
            Assert.That(world.Submit(new SetGateCommand(1, 100, false)).Accepted, Is.False);
            Accepted(world.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(8500, 6500)))); Tick(world, 50);
            Accepted(world.Submit(new SetGateCommand(1, 100, false)));
            Assert.That(world.IsWalkable(new SimPoint(5500, 6500)), Is.False);
        }

        [Test]
        public void WatchtowerProducesAuthoritativeProjectilesAndDamage()
        {
            var world = new World(Definitions(), new MapDefinition { BuildingSpawns = new[] { B(100, 1, "watchtower", 6000, 6000) },
                UnitSpawns = new[] { U(1, 2, "soldier", 10500, 6500) } });
            Accepted(world.Submit(new StopCommand(2, new[] { 1 }))); world.Tick();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1)); Assert.That(world.Projectiles[0].SourceEntityId, Is.EqualTo(100));
            Tick(world, 20); Assert.That(Unit(world, 1).Health, Is.LessThan(200));
        }

        [Test]
        public void FactionArmorIsStableAcrossTicksAndAddsToPaidUniqueResearch()
        {
            var definition = Definitions();
            definition.Factions = new[] { new FactionDefinition { Id = "skeld", RealmId = ContentRealms.Fantasy, Kind = FactionKind.SkeldClans, ArmorBonusTags = CombatTags.Infantry, ArmorBonus = 1 } };
            definition.Technologies = new[] { new TechnologyDefinition { Id = "oath", ResearchBuildingId = "watchtower", ResearchTicks = 2,
                RequiredFactionId = "skeld", Cost = new ResourceAmount(0, 0, 40, 0), Effects = new[] {
                    new TechnologyEffect { Kind = TechnologyEffectKind.Armor, TargetTags = CombatTags.Infantry, TargetUnitId = "soldier", Amount = 2 } } } };
            var world = new World(definition, new MapDefinition { RealmId = ContentRealms.Fantasy, PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "skeld" } },
                BuildingSpawns = new[] { B(100, 1, "watchtower", 14000, 14000) }, UnitSpawns = new[] { U(1, 1, "soldier", 4500, 4500), U(2, 1, "archer", 7500, 4500) } });
            Assert.That(Unit(world, 1).Armor, Is.EqualTo(1));
            Tick(world, 50); Assert.That(Unit(world, 1).Armor, Is.EqualTo(1));
            Accepted(world.Submit(new ResearchCommand(1, 100, "oath"))); Tick(world, 2);
            Assert.That(Unit(world, 1).Armor, Is.EqualTo(3)); Assert.That(Unit(world, 2).Armor, Is.Zero);
            Tick(world, 50); Assert.That(Unit(world, 1).Armor, Is.EqualTo(3), "Refreshes must neither stack nor erase permanent research.");
            world.TryGetPlayer(1, out var player); Assert.That(player.Resources.Metal, Is.EqualTo(960));
        }

        [Test]
        public void AshenLifeStealRequiresAnActualMeleeHit()
        {
            var definition = Definitions(); definition.Factions[3].MeleeLifeSteal = 2;
            var world = new World(definition, new MapDefinition { RealmId = "fantasy", PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = "ashen" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "solar" } },
                UnitSpawns = new[] { U(1, 1, "soldier", 4500, 4500), U(2, 2, "soldier", 5500, 4500) } });
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, 1))); world.Tick();
            Assert.That(Unit(world, 1).Health, Is.EqualTo(180));
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); world.Tick();
            Assert.That(Unit(world, 1).Health, Is.EqualTo(182));
            Assert.That(Unit(world, 2).Health, Is.EqualTo(180));
        }

        [Test]
        public void AshenLifeStealIgnoresHitsOnStructures()
        {
            var definition = Definitions(); definition.Factions[3].MeleeLifeSteal = 2;
            var world = new World(definition, new MapDefinition { RealmId = "fantasy", PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = "ashen" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "solar" } },
                UnitSpawns = new[] { U(1, 1, "soldier", 4500, 4500), U(2, 2, "soldier", 5500, 4500) },
                BuildingSpawns = new[] { B(100, 2, "wall", 4500, 6500) } });
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, 1))); world.Tick();
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            Assert.That(Unit(world, 1).Health, Is.EqualTo(180));
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 100)));
            Tick(world, 60);
            Assert.That(world.TryGetBuilding(100, out var wall), Is.True);
            Assert.That(wall.Health, Is.LessThan(wall.MaxHealth), "The soldier must actually strike the wall.");
            Assert.That(Unit(world, 1).Health, Is.EqualTo(180), "Demolishing a structure must not heal the attacker.");
        }

        [Test]
        public void CreatureRegenerationWaitsSixSecondsAfterDamageAndStopsAtMaximumHealth()
        {
            var definition = Definitions(); definition.Units[0].RegenerationPerSecond = 4;
            var world = new World(definition, new MapDefinition { UnitSpawns = new[] { U(1, 1, "soldier", 4500, 4500), U(2, 2, "soldier", 5500, 4500) } });
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, 1))); world.Tick();
            Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            Tick(world, 119); Assert.That(Unit(world, 1).Health, Is.EqualTo(180));
            world.Tick(); Assert.That(Unit(world, 1).Health, Is.EqualTo(184));
            Tick(world, 300); Assert.That(Unit(world, 1).Health, Is.EqualTo(200));
        }

        [Test]
        public void ForestHarvestBonusAppliesOnlyToItsResourceAndOwningFaction()
        {
            int Harvest(ResourceKind resourceKind, bool verdant)
            {
                var definition = Definitions();
                definition.Units = new[] { new UnitDefinition { Id = "worker", IsWorker = true, Tags = CombatTags.Worker, GatherIntervalTicks = 1, GatherAmount = 2, CarryCapacity = 100 } };
                definition.Resources = new[] { new ResourceDefinition { Id = "node", Kind = resourceKind, InitialAmount = 100 } };
                definition.Buildings = new[] { new BuildingDefinition { Id = "dropoff", WidthCells = 2, DepthCells = 2, CanDropOff = true } };
                definition.Factions = new[] { new FactionDefinition { Id = "verdant", Kind = FactionKind.VerdantCovenant, RealmId = "fantasy", BonusGatherResource = ResourceKind.Wood, ResourceGatherBonus = 1 } };
                var map = new MapDefinition { RealmId = verdant ? "fantasy" : "historical", UnitSpawns = new[] { U(1, 1, "worker", 4500, 4500) },
                    BuildingSpawns = new[] { B(100, 1, "dropoff", 2000, 2000) },
                    ResourceSpawns = new[] { new ResourceSpawnDefinition { Id = 200, DefinitionId = "node", Position = new SimPoint(5500, 4500) } } };
                if (verdant) map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "verdant" } };
                var world = new World(definition, map); Accepted(world.Submit(new GatherCommand(1, new[] { 1 }, 200))); Tick(world, 5);
                return Unit(world, 1).CarriedAmount;
            }
            Assert.That(Harvest(ResourceKind.Wood, true), Is.GreaterThan(Harvest(ResourceKind.Wood, false)));
            Assert.That(Harvest(ResourceKind.Food, true), Is.EqualTo(Harvest(ResourceKind.Food, false)));
        }

        [Test]
        public void ReservedWallCapacityAndMountedCreatureRestrictionsAreAuthoritative()
        {
            var definition = Definitions(); definition.Buildings[0].WallCapacity = 1; definition.Units[2].Tags |= CombatTags.Creature;
            var world = new World(definition, new MapDefinition { BuildingSpawns = new[] { B(100, 1, "wall", 7500, 6500) },
                UnitSpawns = new[] { U(1, 1, "soldier", 7500, 4500), U(2, 1, "archer", 6500, 4500), U(3, 1, "ram", 8500, 4500) } });
            Assert.That(world.Submit(new BoardWallCommand(1, new[] { 3 }, 100)).Accepted, Is.False);
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100)));
            Assert.That(world.Submit(new BoardWallCommand(1, new[] { 2 }, 100)).Accepted, Is.False);
            Accepted(world.Submit(new StopCommand(1, new[] { 1 })));
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 2 }, 100)));
        }

        [Test]
        public void CloseDefenseOilHitsAttackersButPreservesFriendlyUnits()
        {
            var definition = Definitions(); definition.Buildings[0].OilDamage = 12; definition.Buildings[0].OilCooldownTicks = 40;
            var world = new World(definition, new MapDefinition { BuildingSpawns = new[] { B(100, 1, "wall", 7500, 6500) },
                UnitSpawns = new[] { U(1, 1, "soldier", 7500, 5500), U(2, 2, "soldier", 7500, 7500) } });
            Accepted(world.Submit(new StopCommand(1, new[] { 1 }))); Accepted(world.Submit(new StopCommand(2, new[] { 2 })));
            world.Tick(); Assert.That(Unit(world, 1).Health, Is.EqualTo(200)); Assert.That(Unit(world, 2).Health, Is.EqualTo(188));
            Tick(world, 39); Assert.That(Unit(world, 2).Health, Is.EqualTo(188));
            world.Tick(); Assert.That(Unit(world, 2).Health, Is.EqualTo(176));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SplashUsesSecondaryArmorOnceAndPreservesFriendsAndDistantEnemies(bool projectile)
        {
            var definition = Definitions();
            var attacker = definition.Units[projectile ? 1 : 0]; attacker.Attack.SplashRadiusMillimetres = 1200; attacker.Attack.SplashDamagePermille = 500;
            var armored = Soldier("armored"); armored.Armor = 5;
            var definitions = new List<UnitDefinition>(definition.Units) { armored }; definition.Units = definitions.ToArray();
            var world = new World(definition, new MapDefinition { UnitSpawns = new[] { U(1, 1, attacker.Id, 4500, 4500),
                U(2, 2, "soldier", 5500, 4500), U(3, 2, "armored", 5500, 5500), U(4, 2, "soldier", 5500, 7500), U(5, 1, "soldier", 6500, 4500) } });
            Accepted(world.Submit(new StopCommand(1, new[] { 1, 5 }))); Accepted(world.Submit(new StopCommand(2, new[] { 2, 3, 4 })));
            Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); Tick(world, projectile ? 3 : 1);
            Assert.That(Unit(world, 2).Health, Is.EqualTo(180), "Primary target receives one direct hit.");
            Assert.That(Unit(world, 3).Health, Is.EqualTo(193), "Secondary damage is floor((20 - 5 armor) * 50%).");
            Assert.That(Unit(world, 4).Health, Is.EqualTo(200)); Assert.That(Unit(world, 5).Health, Is.EqualTo(200));
        }

        [Test]
        public void DescendingCannotJumpAcrossAnotherWallSegment()
        {
            var world = new World(Definitions(), new MapDefinition { BuildingSpawns = new[] { B(100, 1, "wall", 7500, 6500), B(101, 2, "wall", 7500, 7500) },
                UnitSpawns = new[] { U(1, 1, "soldier", 7500, 4500) } });
            Accepted(world.Submit(new BoardWallCommand(1, new[] { 1 }, 100))); Tick(world, 40);
            Assert.That(world.Submit(new LeaveWallCommand(1, new[] { 1 }, new SimPoint(7500, 8500))).Accepted, Is.False);
            Assert.That(Unit(world, 1).WallId, Is.EqualTo(100));
        }
    }
}
