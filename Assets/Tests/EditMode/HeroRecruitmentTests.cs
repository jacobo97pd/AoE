using System;
using System.Linq;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public sealed class HeroRecruitmentTests
    {
        private const string Hero = "crimson_corsair";
        private static GameDefinition Rules() => new GameDefinition
        {
            BasePopulationCapacity = 100,
            StartingResources = new ResourceAmount(1000, 1000, 1000, 1000),
            Units = new[]
            {
                new UnitDefinition { Id = "guard", Attack = new AttackDefinition { Damage = 1000, RangeMillimetres = 1200, AcquireRangeMillimetres = 1200 } },
                new UnitDefinition { Id = Hero, DisplayName = "Corsario Carmesí", MaxHealth = 220,
                    RequiredRealmId = ContentRealms.Naval, RequiredFactionId = "pirates", MaxAlivePerPlayer = 1,
                    PopulationCost = 3, TrainTicks = 2, Cost = new ResourceAmount(200, 0, 100, 0),
                    Tags = CombatTags.Infantry | CombatTags.Light, Attack = new AttackDefinition { Damage = 18 } }
            },
            Buildings = new[] { new BuildingDefinition { Id = "hearth", TrainableUnitIds = new[] { "guard", Hero } } },
            Factions = new[] {
                new FactionDefinition { Id = "pirates", Kind = FactionKind.PirateBrotherhood, RealmId = ContentRealms.Naval },
                new FactionDefinition { Id = "aven", Kind = FactionKind.AvenCompact },
                new FactionDefinition { Id = "serevin", Kind = FactionKind.SerevinMarch },
                new FactionDefinition { Id = "solar", Kind = FactionKind.SolarKingdom, RealmId = ContentRealms.Fantasy },
                new FactionDefinition { Id = "ashen", Kind = FactionKind.AshenDominion, RealmId = ContentRealms.Fantasy }
            }
        };

        private static UnitSpawnDefinition U(int id, int owner, string kind, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = kind, Position = new SimPoint(x, z) };
        private static MapDefinition Map(string realm = ContentRealms.Naval, bool hero = false, bool network = false)
        {
            return new MapDefinition { Id = "hero_recruitment", WidthCells = 40, HeightCells = 40,
                RealmId = realm,
                PlayerFactions = new[] {
                    new PlayerFactionDefinition { PlayerId = 1, FactionId = realm == ContentRealms.Naval ? "pirates" : realm == ContentRealms.Fantasy ? "solar" : "aven" },
                    new PlayerFactionDefinition { PlayerId = 2, FactionId = realm == ContentRealms.Naval ? "pirates" : realm == ContentRealms.Fantasy ? "ashen" : "serevin" }
                },
                UnitSpawns = hero ? new[] { U(1, 1, "guard", 10500, 10500), U(2, 2, "guard", 13100, 12000), U(3, 1, Hero, 12000, 12000) } :
                    new[] { U(1, 1, "guard", 10500, 10500), U(2, 2, "guard", 22500, 22500) },
                BuildingSpawns = new[] {
                    new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(6500, 6500) },
                    new BuildingSpawnDefinition { Id = 101, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(6500, 15500) },
                    new BuildingSpawnDefinition { Id = 102, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(25500, 25500) }
                },
                OfflineMatch = network ? new OfflineMatchDefinition { Enabled = true } : null };
        }
        private static void Accepted(CommandResult result) => Assert.That(result.Accepted, Is.True, result.Message);

        [Test]
        public void HeroReservationSpansAllOwnedBuildingsAndRejectsWithoutCharging()
        {
            var world = new World(Rules(), Map()); world.TryGetPlayer(1, out var player);
            Accepted(world.Submit(new TrainCommand(1, 100, Hero)));
            Assert.That(player.PopulationReserved, Is.EqualTo(3));
            var resources = player.Resources;
            Assert.That(world.Submit(new TrainCommand(1, 101, Hero)).Reason, Is.EqualTo(CommandRejection.CannotTrain));
            Assert.That(player.Resources, Is.EqualTo(resources)); Assert.That(player.PopulationReserved, Is.EqualTo(3));
            Accepted(world.Submit(new TrainCommand(2, 102, Hero)));
            world.Tick(); world.Tick();
            Assert.That(world.Units.Count(u => u.DefinitionId == Hero), Is.EqualTo(2));
            Assert.That(player.PopulationReserved, Is.Zero);
            Assert.That(world.Submit(new TrainCommand(1, 101, Hero)).Reason, Is.EqualTo(CommandRejection.CannotTrain));
        }

        [Test]
        public void ADeadHeroCanBeRecruitedAgainUsingNormalResources()
        {
            var world = new World(Rules(), Map(hero: true));
            Accepted(world.Submit(new StopCommand(1, new[] { 1, 3 })));
            Assert.That(world.ValidateUnitRecruitment(1, Hero).Accepted, Is.False);
            Accepted(world.Submit(new AttackCommand(2, new[] { 2 }, 3)));
            world.Tick(); Assert.That(world.TryGetUnit(3, out _), Is.False);
            Accepted(world.Submit(new TrainCommand(1, 100, Hero)));
            world.TryGetPlayer(1, out var player);
            Assert.That(player.Resources.Food, Is.EqualTo(800)); Assert.That(player.Resources.Metal, Is.EqualTo(900));
        }

        [TestCase(ContentRealms.Historical)] [TestCase(ContentRealms.Fantasy)]
        public void OtherRealmsCannotRecruitOrSpawnNavalPirateHero(string realm)
        {
            var world = new World(Rules(), Map(realm: realm)); world.TryGetPlayer(1, out var player);
            var resources = player.Resources;
            Assert.That(world.ValidateUnitRecruitment(1, Hero).Reason, Is.EqualTo(CommandRejection.WrongFaction));
            Assert.That(world.Submit(new TrainCommand(1, 100, Hero)).Reason, Is.EqualTo(CommandRejection.WrongFaction));
            Assert.That(player.Resources, Is.EqualTo(resources));
            Assert.Throws<ArgumentException>(() => new World(Rules(), Map(realm: realm, hero: true)));
        }

        [Test]
        public void AuthoredDuplicateHeroesAreRejectedButOnePerOwnerIsValid()
        {
            var map = Map(hero: true);
            map.UnitSpawns = map.UnitSpawns.Concat(new[] { U(4, 1, Hero, 16000, 18000) }).ToArray();
            Assert.Throws<ArgumentException>(() => new World(Rules(), map));
            map.UnitSpawns[3].OwnerId = 2;
            Assert.DoesNotThrow(() => new World(Rules(), map));
        }

        [TestCase(true, false)] [TestCase(false, true)] [TestCase(false, false)]
        public void AiChoosesAvailableInfantryInsteadOfAnIneligibleHero(bool fantasy, bool livingHero)
        {
            var rules = Rules();
            // Make the hero the preferred affordable soldier, so the AI must filter
            // its eligibility before choosing the alternative ordinary infantry.
            rules.Units[0].Cost = new ResourceAmount(400, 0, 0, 0);
            var world = new World(rules, Map(realm: fantasy ? ContentRealms.Fantasy : ContentRealms.Naval, hero: livingHero, network: true));
            if (!fantasy && !livingHero) Accepted(world.Submit(new TrainCommand(1, 100, Hero)));
            var ai = new OfflineAi(world, 1);
            int heroOrders = 0, infantryOrders = 0;
            ai.CommandIssued += (command, result) =>
            {
                if (!(command is TrainCommand train)) return;
                if (train.UnitDefinitionId == Hero) heroOrders++;
                else if (train.UnitDefinitionId == "guard") infantryOrders++;
                Assert.That(result.Accepted, Is.True, result.Message);
            };
            ai.Tick();
            Assert.That(heroOrders, Is.Zero);
            Assert.That(infantryOrders, Is.GreaterThan(0), "An unavailable hero must not stall ordinary recruitment.");
        }

        [TestCase("unknown", 1)] [TestCase("naval", -1)] [TestCase("naval", 1025)]
        public void InvalidRealmOrLimitFailsDefinitionValidation(string realm, int limit)
        {
            var rules = Rules(); rules.Units[1].RequiredRealmId = realm; rules.Units[1].MaxAlivePerPlayer = limit;
            Assert.Throws<ArgumentException>(() => new World(rules, Map()));
        }

        [TestCase(false)] [TestCase(true)]
        public void ForgedReplicaCannotBypassRealmOrLivingHeroLimit(bool fantasy)
        {
            var world = new World(Rules(), Map(realm: fantasy ? ContentRealms.Fantasy : ContentRealms.Naval, hero: !fantasy, network: true));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var snapshot = NetworkObservation.Export(world, 1, 2);
            snapshot.Units.First(u => u.Id == 1).DefinitionId = Hero;
            replica.TryGetUnit(1, out var before);
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(snapshot));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
            replica.TryGetUnit(1, out var after); Assert.That(after, Is.SameAs(before));
        }

        [Test]
        public void ForgedReplicaCannotReserveTheSameHeroAtTwoBuildings()
        {
            var world = new World(Rules(), Map(network: true)); Accepted(world.Submit(new TrainCommand(1, 100, Hero)));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, 1, 1));
            var snapshot = NetworkObservation.Export(world, 1, 2);
            snapshot.Buildings.First(b => b.Id == 101).ProductionQueue = snapshot.Buildings.First(b => b.Id == 100).ProductionQueue;
            Assert.Throws<ArgumentException>(() => replica.ApplyNetworkSnapshot(snapshot));
            Assert.That(replica.NetworkSnapshotSequence, Is.EqualTo(1));
        }
    }
}
