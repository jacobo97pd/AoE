using System;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.PlayMode
{
    public sealed class FactionRealmIntegrationTests
    {
        [TestCase("aven", "historical")]
        [TestCase("serevin", "historical")]
        [TestCase("english", "historical")]
        [TestCase("sultanate", "historical")]
        [TestCase("sahel", "historical")]
        [TestCase("ashen", "fantasy")]
        [TestCase("drakeforged", "fantasy")]
        [TestCase("skeld", "fantasy")]
        [TestCase("verdant", "fantasy")]
        [TestCase("pirates", "naval")]
        [TestCase("english_navy", "naval")]
        [TestCase("spanish_navy", "naval")]
        public void PlayableFactionStartsWithCompatibleRivalAndWorkers(string faction, string realm)
        {
            var world = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, ContentRealms.DefaultMapForRealm(realm));
            Assert.That(world.Map.RealmId, Is.EqualTo(realm));
            Assert.That(ContentRealms.IsPlayableFactionInRealm(world.Map.PlayerFactions[1].FactionId, realm), Is.True);
            foreach (int owner in new[] { 1, 2 })
            {
                // Each seat's own faction: the pirates recruit their crew, everyone else (the navies included) the tender.
                string seatFaction = world.Map.PlayerFactions[owner - 1].FactionId;
                var units = world.Units.Where(u => u.OwnerId == owner).ToArray();
                Assert.That(units, Is.Not.Empty);
                Assert.That(units.All(u => u.DefinitionId == ContentRealms.StartingWorkerForFaction(seatFaction)), Is.True);
                Assert.That(world.ValidateUnitRecruitment(owner, ContentRealms.StartingWorkerForFaction(seatFaction)).Accepted, Is.True);
                Assert.That(world.ValidateUnitRecruitment(owner, "tender").Accepted, Is.EqualTo(seatFaction != "pirates"));
                foreach (string pirate in new[] { "crimson_corsair", "boarding_raider", "gunpowder_corsair", "treasure_seeker" })
                    Assert.That(world.ValidateUnitRecruitment(owner, pirate).Accepted, Is.EqualTo(seatFaction == "pirates"), pirate);
            }
            for (int i = 0; i < 20; i++) world.Tick();
            Assert.That(world.TickIndex, Is.EqualTo(20));
        }

        [TestCase("aven", "pirates", "historical")]
        [TestCase("pirates", "aven", "naval")]
        [TestCase("aven", "skeld", "historical")]
        [TestCase("skeld", "english", "fantasy")]
        [TestCase("sultanate", "verdant", "historical")]
        [TestCase("sahel", "pirates", "naval")]
        public void MixedRealmAssignmentsCannotEnterSimulation(string first, string second, string realm)
        {
            var source = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            source.Map.RealmId = realm;
            source.Map.PlayerFactions = new[] {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = first },
                new PlayerFactionDefinition { PlayerId = 2, FactionId = second }
            };
            Assert.Throws<ArgumentException>(() => new World(source.Definition, source.Map));
        }

        [TestCase("skeleton_fleet")]
        [TestCase("miraj")]
        [TestCase("solar")]
        public void PlannedAndRetiredFactionsAreNotOfferedToNewMatches(string faction)
        {
            foreach (string realm in ContentRealms.All)
                Assert.That(ContentRealms.IsPlayableFactionInRealm(faction, realm), Is.False);
            Assert.That(ContentRealms.OpponentFaction(faction), Is.Null);
        }

        [Test]
        public void NavalReplicaUsesEachFleetsWorkersAndKeepsTheSeatIdentity()
        {
            // The pirates' offline rival is the English navy: treasure seekers on one seat, the kingdom's tenders on the other.
            var source = DefinitionLoader.CreateOfflineWorld("pirates", VictoryMode.Conquest, "sapphire_coast");
            string[] factions = { "pirates", "english_navy" }, workers = { "treasure_seeker", "tender" };
            foreach (int seat in new[] { 1, 2 })
            {
                var snapshot = NetworkObservation.Export(source, seat, 1);
                var replica = World.CreateNetworkReplica(source.Definition, source.Map, snapshot);
                Assert.That(replica.Map.RealmId, Is.EqualTo(ContentRealms.Naval));
                Assert.That(replica.NetworkServerPlayerId, Is.EqualTo(seat));
                Assert.That(replica.Units.Where(u => u.OwnerId == 1).Select(u => u.DefinitionId).Distinct(), Is.EqualTo(new[] { workers[seat - 1] }));
                Assert.That(snapshot.OpponentFactionId, Is.EqualTo(factions[2 - seat]));
                Assert.That(snapshot.LocalPlayer.FactionId, Is.EqualTo(factions[seat - 1]));
            }
        }

        [TestCase("amber_crossing")]
        [TestCase("sunscar_basin")]
        public void NavalOfflineGamesRejectInlandBattlefields(string map)
        {
            Assert.Throws<ArgumentException>(() => DefinitionLoader.CreateOfflineWorld("pirates", VictoryMode.Conquest, map));
        }
    }
}
