using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Emberfield.Localization;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>The fantasy realm's Lands of Legend: a fair mirrored map whose lands follow the cultures that start in them.</summary>
    public sealed class LegendLandsTests
    {
        private const string MapId = "legend_lands";
        private GameObject actor;
        [UnityTearDown] public IEnumerator Cleanup() { if (actor != null) Object.Destroy(actor); yield return null; }

        // Any pairing, as the authority and the offline loader would install it.
        private static World Match(string first, string second)
        {
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/" + MapId).text);
            map.RealmId = ContentRealms.Fantasy;
            map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = first }, new PlayerFactionDefinition { PlayerId = 2, FactionId = second } };
            ContentRealms.PrepareStartingUnits(map);
            return new World(rules, map);
        }
        private static string Faction(World world, int playerId) => world.Map.PlayerFactions.First(p => p.PlayerId == playerId).FactionId;
        private static IEnumerable<BuildingSpawnDefinition> Hearths(MapDefinition map) => map.BuildingSpawns.Where(b => b.DefinitionId == "hearth");

        // What a pairing's landmarks must become: a monument and two peaks per land, two stones and two massifs in the neutral band.
        private static Dictionary<string, int> ExpectedLandmarks(string first, string second)
        {
            var expected = new Dictionary<string, int> { ["standing_stones"] = 2, ["mountain"] = 2 };
            void Add(string kind, int count) => expected[kind] = (expected.TryGetValue(kind, out int known) ? known : 0) + count;
            foreach (string faction in new[] { first, second })
            {
                string biome = MapLands.CultureBiome(faction);
                Add(MapLands.LandmarkKind(MapLands.MonumentSlot, biome), 1);
                Add(MapLands.LandmarkKind(MapLands.PeakSlot, biome), 2);
            }
            return expected;
        }

        [Test]
        public void TheMapIsMirroredReachableAndEveryStartSitsInALandOfItsOwn()
        {
            var world = DefinitionLoader.CreateOfflineWorld("verdant", VictoryMode.Dominion, MapId); var map = world.Map;
            Assert.AreEqual(MapLands.Highland, map.BiomeId); Assert.AreEqual(ContentRealms.Fantasy, map.RealmId);
            Assert.AreEqual(144, map.WidthCells); Assert.AreEqual(112, map.HeightCells);
            int width = map.WidthCells, height = map.HeightCells, count = width * height;
            int Mirror(int index) => count - 1 - index;
            var blocked = new HashSet<int>(map.BlockedCells.Select(c => c.Z * width + c.X));
            var water = new HashSet<int>(map.WaterCells.Select(c => c.Z * width + c.X));
            foreach (int cell in blocked) Assert.IsTrue(blocked.Contains(Mirror(cell)), "Terrain must retain 180 degree symmetry.");
            foreach (int cell in water) Assert.IsTrue(water.Contains(Mirror(cell)), "Water must retain 180 degree symmetry.");
            Assert.Greater(water.Count, 100); Assert.IsTrue(water.Any(cell => !blocked.Contains(cell)), "The land roads ford their streams.");

            // Zones: exactly mirrored with the two lands swapped, each land about a third of the map.
            var zones = MapLands.DecodeRuns(map.ZoneRuns, width, height);
            for (int index = 0; index < count; index++)
                if (zones[Mirror(index)] != (zones[index] == MapLands.Neutral ? MapLands.Neutral : 3 - zones[index])) Assert.Fail("Lands do not mirror at " + index % width + "," + index / width);
            foreach (int land in new[] { 1, 2 })
            {
                float share = zones.Count(z => z == land) / (float)count;
                Assert.That(share, Is.InRange(.28f, .40f), "Land " + land);
            }
            Assert.That(zones.Count(z => z == MapLands.Neutral) / (float)count, Is.InRange(.25f, .40f));
            var hearthLands = Hearths(map).Select(h => (int)zones[h.Position.Z / 1000 * width + h.Position.X / 1000]).ToArray();
            Assert.That(hearthLands, Is.EquivalentTo(new[] { 1, 2 }), "Each start stands in a land of its own.");
            foreach (var hearth in Hearths(map)) Assert.AreEqual(world.Lands.ZoneOf(hearth.OwnerId), world.Lands.ZoneAt(hearth.Position));
            foreach (var objective in map.OfflineMatch.Objectives)
                Assert.AreEqual(MapLands.Neutral, world.Lands.ZoneAt(objective.Position), "Beacons stand on neutral ground: " + objective.Id);
            // No pool or stream is half one land and half another: it would be half lava, half water.
            var seen = new HashSet<int>();
            foreach (int first in water)
            {
                if (!seen.Add(first)) continue;
                var body = new Queue<int>(); body.Enqueue(first);
                while (body.Count > 0)
                {
                    int current = body.Dequeue();
                    if (zones[current] != zones[first]) Assert.Fail("Water crosses a land's edge at " + current % width + "," + current / width);
                    foreach (int next in new[] { current - 1, current + 1, current - width, current + width })
                        if (next >= 0 && next < count && Math.Abs(next % width - current % width) <= 1 && water.Contains(next) && seen.Add(next)) body.Enqueue(next);
                }
            }

            // Reachability, four starting workers a side, mirrored resources and beacons.
            var reached = new HashSet<int>(); var queue = new Queue<int>();
            int origin = map.UnitSpawns[0].Position.Z / 1000 * width + map.UnitSpawns[0].Position.X / 1000; reached.Add(origin); queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(), x = current % width, z = current / width;
                foreach (var step in new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) })
                {
                    int nx = x + step.x, nz = z + step.y, index = nz * width + nx;
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height || blocked.Contains(index) || !reached.Add(index)) continue;
                    queue.Enqueue(index);
                }
            }
            int own = 0, rival = 0;
            foreach (var unit in map.UnitSpawns) { if (unit.OwnerId == 1) own++; else if (unit.OwnerId == 2) rival++; Assert.IsTrue(reached.Contains(unit.Position.Z / 1000 * width + unit.Position.X / 1000)); }
            Assert.AreEqual(4, own); Assert.AreEqual(own, rival);
            foreach (var node in map.ResourceSpawns)
            {
                Assert.IsTrue(reached.Contains(node.Position.Z / 1000 * width + node.Position.X / 1000), "Resource approach terrain: " + node.Id);
                Assert.IsTrue(map.ResourceSpawns.Any(other => other.DefinitionId == node.DefinitionId && other.Position.X + node.Position.X == width * 1000 && other.Position.Z + node.Position.Z == height * 1000),
                    "Resources must have identical mirrored counterparts: " + node.Id);
            }
            Assert.AreEqual(46, map.ResourceSpawns.Length, "The same resource template as the other skirmish maps.");
            foreach (var objective in map.OfflineMatch.Objectives) Assert.IsTrue(reached.Contains(objective.Position.Z / 1000 * width + objective.Position.X / 1000), objective.Id);
            Assert.AreEqual(3, map.OfflineMatch.Objectives.Length);
            foreach (var landmark in map.Landmarks)
                Assert.IsTrue(map.Landmarks.Any(other => other.Kind == landmark.Kind && other.Position.X + landmark.Position.X == width * 1000 && other.Position.Z + landmark.Position.Z == height * 1000), landmark.Kind);
            Assert.AreEqual(4, map.Landmarks.Count(l => l.Kind == MapLands.MonumentSlot)); Assert.AreEqual(6, map.Landmarks.Count(l => l.Kind == MapLands.PeakSlot));
            Assert.AreEqual(0, world.TickIndex, "Map inspection must not advance the simulation.");
        }

        [TestCase("verdant", "ashen")]
        [TestCase("ashen", "verdant")]
        [TestCase("skeld", "drakeforged")]
        [TestCase("drakeforged", "skeld")]
        [TestCase("verdant", "drakeforged")]
        [TestCase("ashen", "skeld")]
        [TestCase("verdant", "verdant")]
        [TestCase("ashen", "ashen")]
        [TestCase("skeld", "skeld")]
        [TestCase("drakeforged", "drakeforged")]
        public void EachPairingDressesTheLandsItsCulturesStartIn(string first, string second)
        {
            var world = Match(first, second);
            foreach (var hearth in Hearths(world.Map))
            {
                string culture = MapLands.CultureBiome(Faction(world, hearth.OwnerId));
                Assert.AreEqual(culture, world.Lands.BiomeAt(hearth.Position), "Player " + hearth.OwnerId + " starts in their culture's land.");
                Assert.AreEqual(hearth.OwnerId, world.Lands.OwnerOf(world.Lands.ZoneAt(hearth.Position)));
            }
            Assert.AreEqual(MapLands.Highland, world.Lands.BiomeAt(new SimPoint(72000, 56000)), "The centre is neutral highland.");
            bool orcs = first == "ashen" || second == "ashen";
            Assert.AreEqual(orcs, world.Lands.Biomes.Contains(MapLands.Volcanic), "Volcanic land appears only when orcs play.");
            Assert.AreEqual(first == "verdant" || second == "verdant", world.Lands.Biomes.Contains(MapLands.Elven));
            Assert.AreEqual(orcs, world.Map.WaterCells.Any(cell => world.Lands.BiomeAt(cell.X, cell.Z) == MapLands.Volcanic), "Lava runs only in a volcanic land.");
            var resolved = world.Map.Landmarks.GroupBy(world.Lands.LandmarkKind).ToDictionary(group => group.Key, group => group.Count());
            Assert.That(resolved, Is.EquivalentTo(ExpectedLandmarks(first, second)));
            for (int i = 0; i < 5; i++) world.Tick();
            Assert.AreEqual(5, world.TickIndex);
        }

        [TestCase(1)] [TestCase(2)]
        public void AnOnlineReplicaDressesTheSameLandsFromEitherSeat(int seat)
        {
            var world = Match("verdant", "ashen");
            var replica = World.CreateNetworkReplica(world.Definition, world.Map, NetworkObservation.Export(world, seat, 1));
            Assert.AreEqual(world.Map.ZoneRuns, replica.Map.ZoneRuns);
            Assert.AreEqual(world.Map.WaterCells.Length, replica.Map.WaterCells.Length);
            Assert.AreEqual(world.Map.Landmarks.Length, replica.Map.Landmarks.Length);
            for (int z = 0; z < world.Map.HeightCells; z++) for (int x = 0; x < world.Map.WidthCells; x++)
                if (world.Lands.BiomeAt(x, z) != replica.Lands.BiomeAt(x, z)) Assert.Fail("Seat " + seat + " sees another land at " + x + "," + z);
            // The replica renumbers the local seat to 1, and the land it owns is still the one its hearth stands in.
            var local = Hearths(world.Map).First(h => h.OwnerId == seat);
            Assert.AreEqual(1, replica.Lands.OwnerOf(replica.Lands.ZoneAt(local.Position)));
            Assert.AreEqual(MapLands.CultureBiome(Faction(world, seat)), replica.Lands.BiomeOf(replica.Lands.ZoneOf(1)));
        }

        [Test]
        public void OnlyTheFantasyRealmPlaysTheLandsAndOpensOnThem()
        {
            Assert.IsTrue(ContentRealms.IsMapAllowedInRealm(MapId, ContentRealms.Fantasy));
            Assert.IsFalse(ContentRealms.IsMapAllowedInRealm(MapId, ContentRealms.Historical));
            Assert.IsFalse(ContentRealms.IsMapAllowedInRealm(MapId, ContentRealms.Naval));
            Assert.AreEqual(MapId, ContentRealms.DefaultMapForRealm(ContentRealms.Fantasy));
            Assert.AreEqual("amber_crossing", ContentRealms.DefaultMapForRealm(ContentRealms.Historical));
            Assert.That(FrontierCodex.MapsForRealm(ContentRealms.Fantasy), Is.EquivalentTo(new[] { "amber_crossing", "sapphire_coast", "sunscar_basin", MapId }));
            Assert.That(FrontierCodex.MapsForRealm(ContentRealms.Historical), Has.No.Member(MapId));
            Assert.That(FrontierCodex.MapsForRealm(ContentRealms.Naval), Is.EqualTo(new[] { "sapphire_coast" }));
            Assert.Throws<ArgumentException>(() => DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest, MapId));
            Assert.Throws<ArgumentException>(() => DefinitionLoader.CreateOfflineWorld("pirates", VictoryMode.Conquest, MapId));
            var spanish = SpanishCatalog.Create();
            Assert.AreEqual("Tierras de Leyenda", spanish.Translate(FrontierCodex.MapName(MapId)));
            Assert.AreEqual("PATRIAS\nTierras de Leyenda", spanish.Translate(FrontierCodex.BiomeName(MapId) + "\n" + FrontierCodex.MapName(MapId)));
            Assert.AreNotEqual(FrontierCodex.MapDescription(MapId), spanish.Translate(FrontierCodex.MapDescription(MapId)));
            Assert.AreEqual("Campo de batalla: Tierras de Leyenda / Patrias", spanish.Translate("Battlefield: Lands of Legend / Homelands"));
        }

        [UnityTest]
        public IEnumerator ChoosingTheFantasyRealmOpensItsLandsAndOtherRealmsCannotPickThem()
        {
            actor = new GameObject("Lands of Legend selector"); actor.SetActive(false);
            var match = actor.AddComponent<MatchController>(); match.InitialOfflineFactionId = "aven";
            actor.SetActive(true); match.Shell.Open(); yield return null;
            match.Shell.Navigate("skirmish");
            string Selected() => (string)typeof(ProductShell).GetField("mapId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match.Shell);
            Button Find(string name) => match.Shell.Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == name);
            Assert.IsFalse(Find("Choose map " + MapId).interactable, "Historical armies do not play the Lands of Legend.");
            Find("Choose realm fantasy").onClick.Invoke(); yield return null;
            Assert.AreEqual(MapId, Selected());
            Assert.IsTrue(Find("Choose map " + MapId).interactable); Assert.IsTrue(Find("Choose map amber_crossing").interactable);
            Find("Choose map sunscar_basin").onClick.Invoke(); Assert.AreEqual("sunscar_basin", Selected());
            Find("Choose realm naval").onClick.Invoke(); yield return null;
            Assert.AreEqual("sapphire_coast", Selected()); Assert.IsFalse(Find("Choose map " + MapId).interactable);
            Find("Choose realm historical").onClick.Invoke(); yield return null;
            Assert.AreEqual("amber_crossing", Selected());
            var controls = match.OfflineControls;
            controls.ChooseRealm(); Assert.AreEqual(ContentRealms.Fantasy, controls.ChosenRealm); Assert.AreEqual(MapId, controls.ChosenMap);
            controls.ChooseRealm(); Assert.AreEqual("sapphire_coast", controls.ChosenMap);
        }

        [Test]
        public void TheBattlefieldDrawsEachLandsOwnMonuments()
        {
            var holder = new GameObject("Lands of Legend view"); WorldView view = null;
            try
            {
                var world = Match("verdant", "ashen");
                var camera = new GameObject("Lands camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(holder.transform);
                camera.orthographic = true; camera.orthographicSize = 30; camera.transform.position = new Vector3(72, 60, 20); camera.transform.LookAt(new Vector3(72, 0, 56));
                view = new WorldView(world, holder.transform, camera);
                var drawn = holder.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Landmark ", StringComparison.Ordinal))
                    .GroupBy(t => t.name.Substring("Landmark ".Length)).ToDictionary(group => group.Key, group => group.Count());
                Assert.That(drawn, Is.EquivalentTo(ExpectedLandmarks("verdant", "ashen")));
            }
            finally { view?.Dispose(); Object.DestroyImmediate(holder); }
        }
    }
}
