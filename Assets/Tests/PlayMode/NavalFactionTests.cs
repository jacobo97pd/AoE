using System;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The naval realm's three fleets (docs/design/NAVAL_SLICE.md) on the shipped rules, coast and art: the realm and its
    /// rivals, each fleet's own hull, a scripted Sapphire Coast match that raises a dock and trains the hull and the land
    /// army, the hull counters in controlled duels, a natural AI match for each pairing, a snapshot round trip with a loaded
    /// hull, and the culture each navy is drawn in.
    /// </summary>
    public sealed class NavalFactionTests
    {
        private static readonly string[] Fleets = { "pirates", "english_navy", "spanish_navy" };
        private static readonly Dictionary<string, string> Hulls = new Dictionary<string, string>
            { { "pirates", "pirate_sloop" }, { "english_navy", "english_frigate" }, { "spanish_navy", "spanish_galleon" } };
        private static void Accepted(CommandResult result, string what) => Assert.IsTrue(result.Accepted, what + ": " + result.Reason + " " + result.Message);
        private static UnitDefinition UnitOf(World world, string id) => world.Definition.Units.First(u => u.Id == id);

        [Test]
        public void TheNaviesJoinThePiratesInTheNavalRealmAndEachMeetsTheNextFleet()
        {
            CollectionAssert.AreEqual(Fleets, ContentRealms.FactionsForRealm(ContentRealms.Naval));
            foreach (string id in Fleets)
            {
                Assert.AreEqual(ContentRealms.Naval, ContentRealms.RealmForFaction(id));
                foreach (string realm in ContentRealms.All) Assert.AreEqual(realm == ContentRealms.Naval, ContentRealms.IsPlayableFactionInRealm(id, realm), id + " in " + realm);
                Assert.AreEqual(id == "pirates" ? "treasure_seeker" : "tender", ContentRealms.StartingWorkerForFaction(id));
                Assert.AreEqual("sapphire_coast", FrontierCodex.HomeMap(id));
                // A navy's signature is its warship; the pirates keep their captain.
                Assert.AreEqual(id == "pirates" ? "crimson_corsair" : Hulls[id], FrontierCodex.SignatureUnit(id));
                Assert.IsNotEmpty(FrontierCodex.Identity(id)); Assert.IsNotEmpty(FrontierCodex.Description(id));
                Assert.Throws<ArgumentException>(() => DefinitionLoader.CreateOfflineWorld(id, VictoryMode.Conquest, "amber_crossing"));
            }
            // The offline rivals are the three pairings: sloops against frigates, frigates against galleons, galleons against sloops.
            Assert.AreEqual("english_navy", ContentRealms.OpponentFaction("pirates"));
            Assert.AreEqual("spanish_navy", ContentRealms.OpponentFaction("english_navy"));
            Assert.AreEqual("pirates", ContentRealms.OpponentFaction("spanish_navy"));
            // The Skeleton Fleet stays locked until its crews exist.
            CollectionAssert.AreEqual(new[] { "skeleton_fleet" }, FrontierCodex.PlannedNavalFactions);
            Assert.IsFalse(ContentRealms.IsPlayableFactionInRealm("skeleton_fleet", ContentRealms.Naval));
            Assert.IsNull(ContentRealms.OpponentFaction("skeleton_fleet"));
            var world = DefinitionLoader.CreateOfflineWorld("english_navy", VictoryMode.Conquest, "sapphire_coast");
            Assert.AreEqual(ContentRealms.Naval, world.Map.RealmId);
            Assert.IsTrue(world.HasDeepWater, "The navies sail the connected naval coast.");
            CollectionAssert.AreEqual(new[] { "english_navy", "spanish_navy" }, world.Map.PlayerFactions.Select(p => p.FactionId).ToArray());
            Assert.IsTrue(world.Units.Where(u => u.OwnerId is 1 or 2).All(u => u.DefinitionId == "tender"), "Both navies start with the kingdom's tenders.");
            var coast = DefinitionLoader.CreateOfflineWorld("spanish_navy", VictoryMode.Conquest, "sapphire_coast");
            Assert.IsTrue(coast.Units.Where(u => u.OwnerId == 2).All(u => u.DefinitionId == "treasure_seeker"), "The pirates keep their own workers.");
        }

        [TestCase("english_navy", FactionKind.EnglishNavy, "english_frigate", "spanish_navy")]
        [TestCase("spanish_navy", FactionKind.SpanishNavy, "spanish_galleon", "pirates")]
        public void TheDefinitionsLoadAndEachHullBelongsToItsFleetAlone(string id, FactionKind kind, string hull, string rival)
        {
            // World creation runs every definition check, including each faction's reachability closure.
            var world = DefinitionLoader.CreateOfflineWorld(id, VictoryMode.Conquest, "sapphire_coast");
            var faction = world.Definition.Factions.First(f => f.Id == id);
            Assert.AreEqual(kind, faction.Kind); Assert.AreEqual(ContentRealms.Naval, faction.RealmId);
            Assert.AreEqual(hull, faction.UniqueUnitId);
            var unit = UnitOf(world, hull);
            Assert.AreEqual(id, unit.RequiredFactionId); Assert.AreEqual(ContentRealms.Naval, unit.RequiredRealmId);
            Assert.AreEqual(MovementDomain.Water, unit.Domain); Assert.AreNotEqual(CombatTags.None, unit.Tags & CombatTags.Naval);
            CollectionAssert.Contains(world.Definition.Buildings.First(b => b.Id == "dock").TrainableUnitIds, hull);
            Assert.IsTrue(world.ValidateUnitRecruitment(1, hull).Accepted);
            Assert.IsFalse(world.ValidateUnitRecruitment(2, hull).Accepted, "The rival cannot launch another fleet's hull.");
            Assert.IsFalse(world.ValidateUnitRecruitment(1, Hulls[rival]).Accepted, "Nor can the navy launch its rival's.");
            var galley = world.ValidateUnitRecruitment(1, "war_galley");
            Assert.IsFalse(galley.Accepted); Assert.AreEqual(CommandRejection.WrongFaction, galley.Reason, "A navy sails its own warship, not the galley.");
            foreach (string shared in new[] { "tender", "reedguard", "stringwarden", "strider" }) Assert.IsTrue(world.ValidateUnitRecruitment(1, shared).Accepted, shared);
            Assert.IsFalse(world.ValidateUnitRecruitment(1, "boarding_raider").Accepted, "The pirates' crew stays theirs.");
            var historical = DefinitionLoader.CreateOfflineWorld("english", VictoryMode.Conquest, "sapphire_coast");
            Assert.IsFalse(historical.ValidateUnitRecruitment(1, hull).Accepted, "The historical English cannot field the navy's hull.");
        }

        [Test]
        public void TheHullsAreALightRaiderALongGunAndAHeavyBroadside()
        {
            var world = DefinitionLoader.CreateOfflineWorld("pirates", VictoryMode.Conquest, "sapphire_coast");
            UnitDefinition sloop = UnitOf(world, "pirate_sloop"), frigate = UnitOf(world, "english_frigate"), galleon = UnitOf(world, "spanish_galleon");
            int Price(UnitDefinition u) => u.Cost.Food + u.Cost.Wood + u.Cost.Metal + u.Cost.Stone;
            Assert.AreEqual(CombatTags.Naval | CombatTags.Light, sloop.Tags);
            Assert.AreEqual(CombatTags.Naval, frigate.Tags, "A medium hull: neither light nor heavy.");
            Assert.AreEqual(CombatTags.Naval | CombatTags.Heavy, galleon.Tags);
            // The sloop is the cheapest and fastest; the frigate is fast and the longest gun; the galleon is slow, the toughest,
            // fires the heaviest shot and carries the most troops.
            Assert.Less(Price(sloop), Price(frigate)); Assert.Less(Price(frigate), Price(galleon));
            Assert.Greater(sloop.MoveSpeedMillimetresPerSecond, frigate.MoveSpeedMillimetresPerSecond);
            Assert.Greater(frigate.MoveSpeedMillimetresPerSecond, galleon.MoveSpeedMillimetresPerSecond);
            Assert.Greater(frigate.Attack.RangeMillimetres, galleon.Attack.RangeMillimetres);
            Assert.Greater(galleon.Attack.RangeMillimetres, sloop.Attack.RangeMillimetres);
            Assert.Greater(galleon.MaxHealth, frigate.MaxHealth); Assert.Greater(frigate.MaxHealth, sloop.MaxHealth);
            Assert.Greater(galleon.Armor, frigate.Armor); Assert.Greater(galleon.Armor, sloop.Armor);
            Assert.Greater(galleon.Attack.Damage, frigate.Attack.Damage); Assert.Greater(frigate.Attack.Damage, sloop.Attack.Damage);
            Assert.Greater(galleon.CargoCapacity, sloop.CargoCapacity); Assert.Greater(sloop.CargoCapacity, frigate.CargoCapacity);
            // The counters are in the shot: the sloop's boarders strike any hull hard, the frigate's long guns pierce heavy hulls.
            Assert.IsTrue(sloop.Attack.Bonuses.Any(b => b.TargetTags == CombatTags.Naval && b.MultiplierPermille > 1000));
            Assert.IsTrue(frigate.Attack.Bonuses.Any(b => b.TargetTags == CombatTags.Heavy && b.MultiplierPermille > 1000));
            Assert.IsFalse(galleon.Attack.Bonuses.Any(b => (b.TargetTags & (CombatTags.Light | CombatTags.Infantry)) != 0), "Grapeshot does not single out troops ashore.");
        }

        // ------------------------------------------------------------------ a scripted match on Sapphire Coast

        [TestCase("pirates")]
        [TestCase("english_navy")]
        [TestCase("spanish_navy")]
        public void EachFleetRaisesADockLaunchesItsHullAndTrainsItsLandArmy(string id)
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(id, VictoryMode.Conquest, "sapphire_coast");
            var rules = baseline.Definition;
            rules.StartingResources = new ResourceAmount(20000, 20000, 20000, 20000); rules.BasePopulationCapacity = 100;
            var map = baseline.Map; map.OfflineMatch = null; // No fog: the drill places on terrain alone.
            var world = new World(rules, map);
            var hearth = world.Buildings.First(b => b.OwnerId == 1 && b.DefinitionId == "hearth");
            int[] workers = world.Units.Where(u => u.OwnerId == 1 && u.IsWorker).Select(u => u.Id).ToArray();
            Assert.IsNotEmpty(workers);

            BuildingState Raise(string building, IEnumerable<SimPoint> candidates)
            {
                SimPoint site = default; bool found = false;
                foreach (var candidate in candidates)
                    if (world.ValidatePlacement(1, workers, building, candidate).Accepted) { site = candidate; found = true; break; }
                Assert.IsTrue(found, id + " finds a site for " + building);
                var before = new HashSet<int>(world.Buildings.Select(b => b.Id));
                Accepted(world.Submit(new BuildCommand(1, workers, building, site)), id + " builds " + building);
                BuildingState placed = null;
                for (int tick = 0; tick < 9000 && (placed == null || !placed.IsComplete); tick++)
                {
                    world.Tick();
                    placed ??= world.Buildings.FirstOrDefault(b => !before.Contains(b.Id) && b.OwnerId == 1 && b.DefinitionId == building);
                }
                Assert.IsNotNull(placed, id + " placed " + building); Assert.IsTrue(placed.IsComplete, id + " completed " + building);
                return placed;
            }
            UnitState Train(BuildingState site, string unit)
            {
                var before = new HashSet<int>(world.Units.Select(u => u.Id));
                Accepted(world.Submit(new TrainCommand(1, site.Id, unit)), id + " trains " + unit);
                UnitState fielded = null;
                for (int tick = 0; tick < 1500 && fielded == null; tick++)
                {
                    world.Tick();
                    fielded = world.Units.FirstOrDefault(u => !before.Contains(u.Id) && u.OwnerId == 1 && u.DefinitionId == unit);
                }
                Assert.IsNotNull(fielded, id + " fielded " + unit);
                return fielded;
            }

            var hall = Raise("muster_hall", Rings(hearth.Position, rules.Buildings.First(b => b.Id == "muster_hall")));
            var dockDefinition = rules.Buildings.First(b => b.Id == "dock");
            var dock = Raise("dock", ShoreSites(world, dockDefinition, hearth.Position));
            // The hull rides the water beside the dock; every soldier stands on land.
            var ship = Train(dock, Hulls[id]);
            Assert.AreEqual(MovementDomain.Water, ship.Domain); Assert.IsTrue(world.IsSailable(ship.Position), id + " launches onto deep water");
            Assert.AreEqual(UnitOf(world, Hulls[id]).CargoCapacity, ship.CargoCapacity);
            var army = new List<string> { ContentRealms.StartingWorkerForFaction(id) };
            Train(hearth, army[0]);
            foreach (string soldier in id == "pirates" ? new[] { "reedguard", "stringwarden", "boarding_raider", "gunpowder_corsair" } : new[] { "reedguard", "stringwarden", "strider" })
            {
                var trained = Train(hall, soldier);
                Assert.AreEqual(MovementDomain.Land, trained.Domain); Assert.IsTrue(world.IsWalkable(trained.Position), soldier + " stands on land");
            }
            // The dock trains no land unit, and no hall launches a ship.
            Assert.IsFalse(world.Submit(new TrainCommand(1, dock.Id, "reedguard")).Accepted);
            Assert.IsFalse(world.Submit(new TrainCommand(1, hall.Id, Hulls[id])).Accepted);
        }

        private static IEnumerable<SimPoint> Rings(SimPoint centre, BuildingDefinition definition)
        {
            for (int ring = 4; ring < 30; ring++)
                for (int step = 0; step < ring * 8; step++)
                {
                    double angle = step * Math.PI * 2 / (ring * 8);
                    var point = new SimPoint(centre.X + (int)(Math.Cos(angle) * ring * 1000), centre.Z + (int)(Math.Sin(angle) * ring * 1000));
                    yield return new SimPoint(point.X / 1000 * 1000 + (definition.WidthCells % 2 == 1 ? 500 : 0), point.Z / 1000 * 1000 + (definition.DepthCells % 2 == 1 ? 500 : 0));
                }
        }

        // Open land footprints with deep water beside them, nearest the origin first.
        private static IEnumerable<SimPoint> ShoreSites(World world, BuildingDefinition dock, SimPoint origin)
        {
            int cell = world.Map.CellSizeMillimetres; var sites = new List<SimPoint>();
            bool Sea(int x, int z) => world.IsSailable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2));
            for (int z = 1; z + dock.DepthCells < world.Map.HeightCells; z++)
            for (int x = 1; x + dock.WidthCells < world.Map.WidthCells; x++)
            {
                bool clear = true, shore = false;
                for (int dz = 0; dz < dock.DepthCells && clear; dz++)
                    for (int dx = 0; dx < dock.WidthCells && clear; dx++)
                        clear = world.IsWalkable(new SimPoint((x + dx) * cell + cell / 2, (z + dz) * cell + cell / 2));
                if (!clear) continue;
                for (int i = 0; i < dock.WidthCells; i++) shore |= Sea(x + i, z - 1) || Sea(x + i, z + dock.DepthCells);
                for (int i = 0; i < dock.DepthCells; i++) shore |= Sea(x - 1, z + i) || Sea(x + dock.WidthCells, z + i);
                if (shore) sites.Add(new SimPoint(x * cell + dock.WidthCells * cell / 2, z * cell + dock.DepthCells * cell / 2));
            }
            long Squared(SimPoint p) { long dx = p.X - origin.X, dz = p.Z - origin.Z; return dx * dx + dz * dz; }
            return sites.OrderBy(Squared).ThenBy(p => p.X).ThenBy(p => p.Z).Take(200);
        }

        // ------------------------------------------------------------------ the hull counters (docs/design/NAVAL_SLICE.md)

        // Two flotillas 12 m apart on open water, each hull ordered on its nearest enemy and re-ordered every second.
        private static (int Winner, double Seconds, int Survivors, int Health) Fight(GameDefinition rules, string left, int leftCount, string right, int rightCount)
        {
            string Owner(string unit) => rules.Units.First(u => u.Id == unit).RequiredFactionId;
            const int width = 80, height = 50;
            var water = new List<GridCell>();
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++) water.Add(new GridCell(x, z));
            var spawns = new List<UnitSpawnDefinition>(); int next = 1;
            for (int i = 0; i < leftCount; i++) spawns.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 1, DefinitionId = left, Position = new SimPoint(30500 - i / 4 * 1500, 21500 + i % 4 * 1500) });
            for (int i = 0; i < rightCount; i++) spawns.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 2, DefinitionId = right, Position = new SimPoint(42500 + i / 4 * 1500, 21500 + i % 4 * 1500) });
            var world = new World(rules, new MapDefinition { Id = "naval_counter_probe", RealmId = ContentRealms.Naval, WidthCells = width, HeightCells = height, CellSizeMillimetres = 1000,
                BlockedCells = water.ToArray(), WaterCells = water.ToArray(), UnitSpawns = spawns.ToArray(),
                PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = Owner(left) }, new PlayerFactionDefinition { PlayerId = 2, FactionId = Owner(right) } } });
            UnitState Nearest(UnitState from, int owner)
            {
                UnitState best = null; long distance = long.MaxValue;
                foreach (var unit in world.Units)
                {
                    if (unit.OwnerId != owner) continue;
                    long dx = unit.Position.X - from.Position.X, dz = unit.Position.Z - from.Position.Z;
                    if (dx * dx + dz * dz < distance) { best = unit; distance = dx * dx + dz * dz; }
                }
                return best;
            }
            void Order(int owner)
            {
                foreach (var unit in world.Units.Where(u => u.OwnerId == owner).ToList())
                {
                    if (unit.AttackTargetId != 0 && world.TryGetUnit(unit.AttackTargetId, out _)) continue;
                    var target = Nearest(unit, 3 - owner); if (target != null) world.Submit(new AttackCommand(owner, new[] { unit.Id }, target.Id));
                }
            }
            int blue = leftCount, red = rightCount;
            while (world.TickIndex < World.TickRate * 240 && ((blue > 0 && red > 0) || world.Projectiles.Count > 0))
            {
                if (world.TickIndex % World.TickRate == 0) { Order(1); Order(2); }
                world.Tick(); blue = red = 0;
                foreach (var unit in world.Units) { if (unit.OwnerId == 1) blue++; else if (unit.OwnerId == 2) red++; }
            }
            int winner = blue > 0 && red == 0 ? 1 : red > 0 && blue == 0 ? 2 : 0;
            return (winner, world.TickIndex / (double)World.TickRate, winner == 1 ? blue : winner == 2 ? red : 0, world.Units.Where(u => u.OwnerId == winner).Sum(u => u.Health));
        }

        // One against one, then equal nominal resources: 1,200 buys 8 sloops (150) or 5 frigates (240); about 1,000 buys
        // 7 sloops or 3 galleons (340); about 1,700 buys 7 frigates or 5 galleons.
        [TestCase("pirate_sloop", 1, "english_frigate", 1)]
        [TestCase("english_frigate", 1, "spanish_galleon", 1)]
        [TestCase("spanish_galleon", 1, "pirate_sloop", 1)]
        [TestCase("pirate_sloop", 8, "english_frigate", 5)]
        [TestCase("english_frigate", 7, "spanish_galleon", 5)]
        [TestCase("spanish_galleon", 3, "pirate_sloop", 7)]
        public void TheFavouredHullWinsADuelFromEitherSeat(string favoured, int favouredCount, string opposed, int opposedCount)
        {
            var rules = DefinitionLoader.CreateWorld().Definition;
            var first = Fight(rules, favoured, favouredCount, opposed, opposedCount);
            var second = Fight(rules, opposed, opposedCount, favoured, favouredCount);
            TestContext.WriteLine($"{favouredCount} {favoured} vs {opposedCount} {opposed}: seat 1 winner {first.Winner} in {first.Seconds:0.0} s, {first.Survivors} left / {first.Health} health; " +
                $"seat 2 winner {second.Winner} in {second.Seconds:0.0} s, {second.Survivors} left / {second.Health} health");
            Assert.AreEqual(1, first.Winner, favoured + " from the first seat");
            Assert.AreEqual(2, second.Winner, favoured + " from the second seat");
            if (favouredCount > 1) Assert.Greater(first.Survivors, 0); // At equal cost the counter wins clearly, keeping hulls afloat.
        }

        // ------------------------------------------------------------------ the offline AI plays each pairing

        [TestCase("pirates", "english_navy")]
        [TestCase("english_navy", "spanish_navy")]
        [TestCase("spanish_navy", "pirates")]
        public void ANaturalHardNavalMatchEndsInConquest(string first, string second)
        {
            var source = DefinitionLoader.CreateOfflineWorld(first, VictoryMode.Conquest, "sapphire_coast");
            Assert.AreEqual(second, source.Map.PlayerFactions[1].FactionId, "The offline rival is the pairing's second fleet.");
            var world = source;
            var ai = new[] { new OfflineAi(world, 1, OfflineAiDifficulty.Hard), new OfflineAi(world, 2, OfflineAiDifficulty.Hard) };
            var hulls = new[] { new HashSet<int>(), new HashSet<int>() };
            int landings = 0, embarks = 0, docks = 0;
            foreach (var brain in ai)
                brain.CommandIssued += (command, result) =>
                {
                    if (!result.Accepted) return;
                    if (command is DisembarkCommand) landings++;
                    if (command is EmbarkCommand) embarks++;
                    if (command is BuildCommand build && build.BuildingDefinitionId == "dock") docks++;
                };
            // An hour at most: the probe runs recorded in docs/design/NAVAL_SLICE.md end within twenty minutes.
            while (world.TickIndex < World.TickRate * 60 * 60 && !world.Match.IsFinished)
            {
                // The offline render probe's alternating order, so neither seat always thinks first.
                if (world.TickIndex % 20 < 10) { ai[0].Tick(); ai[1].Tick(); } else { ai[1].Tick(); ai[0].Tick(); }
                world.Tick();
                if (world.TickIndex % 20 == 0)
                    foreach (var unit in world.Units)
                        if (unit.OwnerId is 1 or 2 && unit.Domain == MovementDomain.Water)
                        {
                            Assert.AreEqual(Hulls[world.Map.PlayerFactions[unit.OwnerId - 1].FactionId], unit.DefinitionId, "Each fleet sails only its own hull.");
                            hulls[unit.OwnerId - 1].Add(unit.Id);
                        }
            }
            TestContext.WriteLine($"{first} vs {second}: {world.Match.Reason} by player {world.Match.WinnerId} after {world.TickIndex / (double)World.TickRate / 60:0.0} min; " +
                $"hulls {hulls[0].Count}/{hulls[1].Count}, docks {docks}, embarks {embarks}, landings {landings}.");
            Assert.IsTrue(world.Match.IsFinished, "The match ends.");
            Assert.AreEqual(MatchEndReason.Conquest, world.Match.Reason);
            Assert.That(world.Match.WinnerId, Is.EqualTo(1).Or.EqualTo(2));
            Assert.GreaterOrEqual(docks, 2, "Both fleets raise a dock.");
            Assert.Greater(hulls[0].Count, 0, first + " launches its hull."); Assert.Greater(hulls[1].Count, 0, second + " launches its hull.");
            Assert.Greater(embarks, 0, "The AI embarks a landing party."); Assert.Greater(landings, 0, "The AI lands troops across the water.");
            Assert.IsFalse(world.Buildings.Any(b => b.OwnerId is 1 or 2 && b.DefinitionId == "beast_lodge"), "No fleet raises a Beast Lodge it has no creature for.");
        }

        // ------------------------------------------------------------------ the network carries the new hulls

        [Test]
        public void ALoadedNavyHullSurvivesASnapshotRoundTripForBothSeats()
        {
            var source = DefinitionLoader.CreateOfflineWorld("english_navy", VictoryMode.Conquest, "sapphire_coast");
            var map = source.Map; int cell = map.CellSizeMillimetres;
            var spawns = map.UnitSpawns.ToList(); int next = spawns.Max(s => s.Id) + 1000;
            int frigateId = next, galleonId = next + 1;
            foreach (var (owner, hull, id) in new[] { (1, "english_frigate", frigateId), (2, "spanish_galleon", galleonId) })
            {
                var hearth = source.Buildings.First(b => b.OwnerId == owner && b.DefinitionId == "hearth");
                // The deep water nearest that seat's Hearth, two cells off the shore so the hull lies clear of the beach.
                SimPoint best = default; long nearest = long.MaxValue;
                for (int z = 2; z < map.HeightCells - 2; z++)
                for (int x = 2; x < map.WidthCells - 2; x++)
                {
                    var point = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                    bool deep = true;
                    for (int dz = -1; dz <= 1 && deep; dz++) for (int dx = -1; dx <= 1 && deep; dx++) deep = source.IsSailable(new SimPoint(point.X + dx * cell, point.Z + dz * cell));
                    if (!deep) continue;
                    long dX = point.X - hearth.Position.X, dZ = point.Z - hearth.Position.Z;
                    if (dX * dX + dZ * dZ < nearest) { nearest = dX * dX + dZ * dZ; best = point; }
                }
                spawns.Add(new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = hull, Position = best });
            }
            map.UnitSpawns = spawns.ToArray(); map.OfflineMatch.Enabled = true;
            var world = new World(source.Definition, map);
            Assert.IsTrue(world.TryGetUnit(frigateId, out var frigate) && world.TryGetUnit(galleonId, out var galleon));
            var tender = world.Units.Where(u => u.OwnerId == 1 && u.IsWorker).OrderBy(u => (long)(u.Position.X - frigate.Position.X) * (u.Position.X - frigate.Position.X) + (long)(u.Position.Z - frigate.Position.Z) * (u.Position.Z - frigate.Position.Z)).First();
            Accepted(world.Submit(new EmbarkCommand(1, new[] { tender.Id }, frigateId)), "A tender boards the frigate");
            for (int tick = 0; tick < 2400 && frigate.CargoCount == 0; tick++) world.Tick();
            Assert.AreEqual(1, frigate.CargoCount, "The tender climbed aboard the frigate.");

            foreach (int seat in new[] { 1, 2 })
            {
                var snapshot = JsonUtility.FromJson<NetworkSnapshot>(JsonUtility.ToJson(NetworkObservation.Export(world, seat, 1)));
                var replica = World.CreateNetworkReplica(world.Definition, map, snapshot);
                Assert.AreEqual(ContentRealms.Naval, replica.Map.RealmId);
                Assert.AreEqual(seat == 1 ? "english_navy" : "spanish_navy", snapshot.LocalPlayer.FactionId);
                Assert.AreEqual(seat == 1 ? "spanish_navy" : "english_navy", snapshot.OpponentFactionId);
                // The seat's own hull comes back as the same hull, afloat, with its own manifest.
                int own = seat == 1 ? frigateId : galleonId;
                Assert.IsTrue(replica.TryGetUnit(own, out var hull), "seat " + seat + " sees its hull");
                Assert.AreEqual(seat == 1 ? "english_frigate" : "spanish_galleon", hull.DefinitionId);
                Assert.AreEqual(MovementDomain.Water, hull.Domain); Assert.IsTrue(replica.IsSailable(hull.Position));
                Assert.AreEqual(1, hull.OwnerId, "The replica's own seat is player one.");
                Assert.AreEqual(seat == 1 ? 1 : 0, hull.CargoCount);
                if (seat == 1) Assert.IsTrue(replica.TryGetPassenger(tender.Id, out var passenger) && passenger.CarrierId == frigateId);
                // The next authoritative snapshot replaces this one.
                for (int tick = 0; tick < 40; tick++) world.Tick();
                replica.ApplyNetworkSnapshot(JsonUtility.FromJson<NetworkSnapshot>(JsonUtility.ToJson(NetworkObservation.Export(world, seat, 2))));
                Assert.AreEqual(2, replica.NetworkSnapshotSequence);
            }
        }

        // ------------------------------------------------------------------ the culture each navy is drawn in

        [TestCase(FactionKind.EnglishNavy, FactionKind.EnglishKingdom, "english", "english_frigate")]
        [TestCase(FactionKind.SpanishNavy, FactionKind.SerevinMarch, "serevin", "spanish_galleon")]
        public void EachNavyFieldsItsKingdomsArmyAndSailsItsOwnHull(FactionKind navy, FactionKind kingdom, string culture, string hull)
        {
            Assert.AreEqual(kingdom, AlphaWorldArt.Culture(navy));
            Assert.AreEqual(FactionKind.PirateBrotherhood, AlphaWorldArt.Culture(FactionKind.PirateBrotherhood), "Every other faction is drawn as itself.");
            foreach (string unit in new[] { "tender", "reedguard", "stringwarden", "strider" })
            {
                // The kingdom's shared models, listed for that kingdom; the owner's colour comes from the team mask.
                var entry = MeshyUnitVisuals.Resolve(unit, AlphaWorldArt.Culture(navy));
                Assert.IsNotNull(entry, culture + " draws a Meshy " + unit);
                Assert.AreEqual("kingdom", entry.culture, unit); CollectionAssert.Contains(entry.factions, culture, unit);
                Assert.IsNotNull(Resources.Load<GameObject>(entry.prefab), entry.id + " prefab");
            }
            foreach (string building in new[] { "hearth", "shelter", "muster_hall", "storeyard" })
                Assert.IsNotNull(MeshyBuildingVisuals.Resolve(building, AlphaWorldArt.Culture(navy)), culture + " town draws " + building);
            var ship = MeshyPropVisuals.ResolveShip(hull, navy);
            Assert.IsNotNull(ship, hull); Assert.AreEqual(hull, ship.id);
            Assert.IsNotNull(Resources.Load<GameObject>(ship.prefab), ship.id + " prefab");
            Assert.AreEqual("pirate_sloop", MeshyPropVisuals.ResolveShip("pirate_sloop", FactionKind.PirateBrotherhood)?.id);
            Assert.IsTrue(AlphaWorldArt.IsShip(hull));
        }
    }
}
