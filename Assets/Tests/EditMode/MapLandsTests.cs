using System;
using System.Linq;
using Emberfield.Simulation;
using NUnit.Framework;
using static Emberfield.Tests.EditMode.OfflineMatchTestWorldFactory;

namespace Emberfield.Tests.EditMode
{
    public sealed class MapLandsTests
    {
        // Six cells by two: land 1 on the west pair of columns, the neutral pair in the middle, land 2 on the east.
        private static MapDefinition Strip(string first, string second, int westOwner = 1, int eastOwner = 2) => new MapDefinition
        {
            Id = "lands-strip", BiomeId = MapLands.Highland, WidthCells = 6, HeightCells = 2, CellSizeMillimetres = 1000,
            ZoneRuns = "1:2 0:2 2:2 1:2 0:2 2:2",
            BuildingSpawns = new[]
            {
                new BuildingSpawnDefinition { Id = 100, OwnerId = westOwner, DefinitionId = "hearth", Position = new SimPoint(500, 500) },
                new BuildingSpawnDefinition { Id = 101, OwnerId = eastOwner, DefinitionId = "hearth", Position = new SimPoint(5500, 1500) }
            },
            PlayerFactions = new[]
            {
                new PlayerFactionDefinition { PlayerId = 1, FactionId = first },
                new PlayerFactionDefinition { PlayerId = 2, FactionId = second }
            }
        };

        [TestCase("verdant", "ashen", "elven", "volcanic")]
        [TestCase("ashen", "verdant", "volcanic", "elven")]
        [TestCase("skeld", "drakeforged", "highland", "highland")]
        [TestCase("drakeforged", "skeld", "highland", "highland")]
        [TestCase("verdant", "drakeforged", "elven", "highland")]
        [TestCase("verdant", "verdant", "elven", "elven")]
        [TestCase("ashen", "ashen", "volcanic", "volcanic")]
        [TestCase("skeld", "skeld", "highland", "highland")]
        [TestCase("drakeforged", "drakeforged", "highland", "highland")]
        public void EachStartingLandWearsTheCultureOfTheHearthInIt(string first, string second, string west, string east)
        {
            var lands = MapLands.Resolve(Strip(first, second));
            Assert.That(lands.HasLands, Is.True);
            for (int z = 0; z < 2; z++)
            {
                Assert.That(lands.BiomeAt(0, z), Is.EqualTo(west)); Assert.That(lands.BiomeAt(1, z), Is.EqualTo(west));
                Assert.That(lands.BiomeAt(2, z), Is.EqualTo(MapLands.Highland), "The neutral ground wears the map's own biome.");
                Assert.That(lands.BiomeAt(4, z), Is.EqualTo(east)); Assert.That(lands.BiomeAt(5, z), Is.EqualTo(east));
            }
            Assert.That(lands.BiomeAt(new SimPoint(1999, 999)), Is.EqualTo(west));
            Assert.That(lands.Biomes, Is.EquivalentTo(new[] { MapLands.Highland, west, east }.Distinct()));
        }

        [Test]
        public void EveryFantasyPairingShowsOnlyTheLandsOfTheCulturesPlaying()
        {
            foreach (string first in ContentRealms.FactionsForRealm(ContentRealms.Fantasy))
            foreach (string second in ContentRealms.FactionsForRealm(ContentRealms.Fantasy))
            {
                var lands = MapLands.Resolve(Strip(first, second));
                Assert.That(lands.BiomeOf(lands.ZoneOf(1)), Is.EqualTo(MapLands.CultureBiome(first)), first + " vs " + second);
                Assert.That(lands.BiomeOf(lands.ZoneOf(2)), Is.EqualTo(MapLands.CultureBiome(second)), first + " vs " + second);
                Assert.That(lands.Biomes.Contains(MapLands.Volcanic), Is.EqualTo(first == "ashen" || second == "ashen"), first + " vs " + second);
                Assert.That(lands.Biomes.Contains(MapLands.Elven), Is.EqualTo(first == "verdant" || second == "verdant"), first + " vs " + second);
            }
        }

        [Test]
        public void TheHearthDecidesALandNotThePlayerNumber()
        {
            // As on a replica of the second seat: the player now numbered 1 starts in the east.
            var lands = MapLands.Resolve(Strip("ashen", "verdant", westOwner: 2, eastOwner: 1));
            Assert.That(lands.BiomeAt(0, 0), Is.EqualTo(MapLands.Elven));
            Assert.That(lands.BiomeAt(5, 0), Is.EqualTo(MapLands.Volcanic));
            Assert.That(lands.OwnerOf(1), Is.EqualTo(2)); Assert.That(lands.OwnerOf(2), Is.EqualTo(1));
            Assert.That(lands.ZoneOf(1), Is.EqualTo(2)); Assert.That(lands.ZoneOf(2), Is.EqualTo(1));
            Assert.That(lands.OwnerOf(MapLands.Neutral), Is.Zero); Assert.That(lands.ZoneOf(7), Is.EqualTo(MapLands.Neutral));
        }

        [Test]
        public void AMapWithoutZonesIsOneLandOfItsOwnBiome()
        {
            var map = Strip("verdant", "ashen"); map.ZoneRuns = ""; map.BiomeId = "desert";
            var lands = MapLands.Resolve(map);
            Assert.That(lands.HasLands, Is.False);
            for (int x = -1; x <= 6; x++) Assert.That(lands.BiomeAt(x, 0), Is.EqualTo("desert"));
            Assert.That(lands.Biomes, Is.EqualTo(new[] { "desert" }));
            Assert.That(lands.LandmarkKind(new MapLandmarkDefinition { Kind = "pyramid", Position = new SimPoint(500, 500) }), Is.EqualTo("pyramid"));
            Assert.That(MapLands.LandmarkBiome("pyramid", "desert"), Is.EqualTo("desert"));
        }

        [Test]
        public void UnclaimedLandsAndCellsOutsideTheMapStayNeutral()
        {
            var map = Strip("verdant", "ashen"); map.BuildingSpawns = map.BuildingSpawns.Take(1).ToArray();
            var lands = MapLands.Resolve(map);
            Assert.That(lands.BiomeAt(5, 1), Is.EqualTo(MapLands.Highland), "A land nobody starts in keeps the map's biome.");
            Assert.That(lands.ZoneAt(-1, 0), Is.EqualTo(MapLands.Neutral)); Assert.That(lands.ZoneAt(6, 0), Is.EqualTo(MapLands.Neutral));
            Assert.That(lands.ZoneAt(new SimPoint(-1, 500)), Is.EqualTo(MapLands.Neutral));
            Assert.That(lands.Biomes, Is.EquivalentTo(new[] { MapLands.Highland, MapLands.Elven }));
        }

        [TestCase("verdant", "elven")] [TestCase("ashen", "volcanic")] [TestCase("skeld", "highland")] [TestCase("drakeforged", "highland")]
        [TestCase("aven", "highland")] [TestCase("pirates", "highland")] [TestCase(null, "highland")]
        public void CulturesChooseTheirLand(string faction, string biome) => Assert.That(MapLands.CultureBiome(faction), Is.EqualTo(biome));

        [TestCase(MapLands.MonumentSlot, "elven", "moonwell", "elven")]
        [TestCase(MapLands.MonumentSlot, "volcanic", "spiked_tower", "volcanic")]
        [TestCase(MapLands.MonumentSlot, "highland", "standing_stones", "highland")]
        [TestCase(MapLands.PeakSlot, "volcanic", "volcano", "volcanic")]
        [TestCase(MapLands.PeakSlot, "elven", "mountain", "highland")]
        [TestCase(MapLands.PeakSlot, "highland", "mountain", "highland")]
        [TestCase("bridge", "volcanic", "bridge", "volcanic")]
        public void LandSlotsBecomeTheMonumentOfTheirLand(string slot, string biome, string kind, string art)
        {
            Assert.That(MapLands.LandmarkKind(slot, biome), Is.EqualTo(kind));
            Assert.That(MapLands.LandmarkBiome(kind, biome), Is.EqualTo(art));
        }

        [Test]
        public void ZoneRunsRoundTripAndRejectAnythingThatDoesNotCoverTheMapOnce()
        {
            var cells = new byte[] { 1, 1, 1, 0, 0, 2, 2, 2, 2, 0, 1, 1 };
            string runs = MapLands.EncodeRuns(cells);
            Assert.That(runs, Is.EqualTo("1:3 0:2 2:4 0:1 1:2"));
            Assert.That(MapLands.DecodeRuns(runs, 4, 3), Is.EqualTo(cells));
            Assert.That(MapLands.EncodeRuns(Array.Empty<byte>()), Is.Empty);
            foreach (string bad in new[] { "1:11", "1:13", "1:3 0:2 2:4 0:1 1:2 1:1", "a:12", "10:12", "1:0 1:12", "1:-12", "1-12", "1:+12", "1:12x" })
                Assert.Throws<ArgumentException>(() => MapLands.DecodeRuns(bad, 4, 3), bad);
            var map = Strip("verdant", "ashen"); map.ZoneRuns = "1:11";
            Assert.Throws<ArgumentException>(() => MapLands.Resolve(map));
        }

        [TestCase(1)] [TestCase(2)]
        public void AReplicaCarriesTheLandsAndResolvesThemFromItsRenumberedSeats(int seat)
        {
            var map = Map();
            // Land 1 on the west third, land 2 on the east third of the 40 by 32 test field; its hearths sit at (4,4) and (28,24).
            var cells = new byte[map.WidthCells * map.HeightCells];
            for (int index = 0; index < cells.Length; index++) { int x = index % map.WidthCells; cells[index] = (byte)(x < 12 ? 1 : x >= 26 ? 2 : 0); }
            map.ZoneRuns = MapLands.EncodeRuns(cells);
            map.WaterCells = new[] { new GridCell(20, 3) };
            map.Landmarks = new[] { new MapLandmarkDefinition { Kind = MapLands.PeakSlot, Position = new SimPoint(2500, 30500) } };
            var world = Create(null, map);
            Assert.That(world.Lands.OwnerOf(1), Is.EqualTo(1)); Assert.That(world.Lands.OwnerOf(2), Is.EqualTo(2));
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, seat, 1));
            Assert.That(replica.Map.ZoneRuns, Is.EqualTo(map.ZoneRuns));
            Assert.That(replica.Map.WaterCells, Is.EqualTo(map.WaterCells)); Assert.That(replica.Map.WaterCells, Is.Not.SameAs(map.WaterCells));
            Assert.That(replica.Map.Landmarks.Select(l => l.Kind), Is.EqualTo(new[] { MapLands.PeakSlot }));
            Assert.That(replica.Map.BuildingSpawns, Is.Empty, "The replica still scrubs its starts once the lands are resolved.");
            // The local player is always 1 on a replica, so the land it owns is the one its seat's hearth stands in.
            Assert.That(replica.Lands.OwnerOf(seat), Is.EqualTo(1)); Assert.That(replica.Lands.OwnerOf(3 - seat), Is.EqualTo(2));
            Assert.That(replica.Lands.ZoneAt(new SimPoint(4500, 4500)), Is.EqualTo(1));
        }
    }
}
