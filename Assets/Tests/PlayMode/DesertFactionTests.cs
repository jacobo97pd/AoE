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
    /// The Sultanato and the Confederación del Sahel (docs/design/DESERT_FACTIONS.md) on the shipped rules, maps and art:
    /// their realm, a scripted match that builds and trains everything they may, their own soldier and research, a
    /// natural AI match between them, the counter probes the design predicts, and the culture each one is drawn in.
    /// </summary>
    public sealed class DesertFactionTests
    {
        private static void Accepted(CommandResult result, string what) => Assert.IsTrue(result.Accepted, what + ": " + result.Message);
        private static FactionDefinition Faction(World world, string id) => world.Definition.Factions.First(f => f.Id == id);
        private static UnitDefinition UnitOf(World world, string id) => world.Definition.Units.First(u => u.Id == id);

        [Test]
        public void TheDesertPairJoinsTheHistoricalRealmAndMeetsEachOtherOnItsDesert()
        {
            CollectionAssert.AreEqual(new[] { "aven", "serevin", "english", "sultanate", "sahel" }, ContentRealms.FactionsForRealm(ContentRealms.Historical));
            foreach (string id in new[] { "sultanate", "sahel" })
            {
                Assert.AreEqual(ContentRealms.Historical, ContentRealms.RealmForFaction(id));
                foreach (string realm in ContentRealms.All) Assert.AreEqual(realm == ContentRealms.Historical, ContentRealms.IsPlayableFactionInRealm(id, realm), id + " in " + realm);
                Assert.AreEqual("tender", ContentRealms.StartingWorkerForFaction(id));
                Assert.AreEqual("sunscar_basin", FrontierCodex.HomeMap(id));
                foreach (string map in FrontierCodex.MapIds) Assert.AreEqual(map != "legend_lands", ContentRealms.IsMapAllowedInRealm(map, ContentRealms.Historical), map);
                Assert.IsNotEmpty(FrontierCodex.Identity(id)); Assert.IsNotEmpty(FrontierCodex.Description(id));
            }
            Assert.AreEqual("sahel", ContentRealms.OpponentFaction("sultanate"));
            Assert.AreEqual("sultanate", ContentRealms.OpponentFaction("sahel"));
            Assert.AreEqual("aven", ContentRealms.OpponentFaction("english"), "The English keep the rival they had.");
            Assert.AreEqual("serevin", ContentRealms.OpponentFaction("aven"));
            Assert.AreEqual("amber_crossing", FrontierCodex.HomeMap("english"));
            var world = DefinitionLoader.CreateOfflineWorld("sultanate", VictoryMode.Conquest, "sunscar_basin");
            Assert.AreEqual(ContentRealms.Historical, world.Map.RealmId);
            CollectionAssert.AreEqual(new[] { "sultanate", "sahel" }, world.Map.PlayerFactions.Select(p => p.FactionId).ToArray());
            Assert.IsTrue(world.Units.Where(u => u.OwnerId is 1 or 2).All(u => u.DefinitionId == "tender"));
            Assert.Throws<ArgumentException>(() => DefinitionLoader.CreateOfflineWorld("sahel", VictoryMode.Conquest, "legend_lands"));
        }

        [TestCase("sultanate", FactionKind.DesertSultanate, "camel_archer", "sultanate_composite_bows", "sahel")]
        [TestCase("sahel", FactionKind.SahelConfederation, "quilted_lancer", "sahel_quilted_barding", "sultanate")]
        public void TheDefinitionsLoadAndEachSoldierAndResearchBelongsToItsFactionAlone(string id, FactionKind kind, string unique, string technology, string rival)
        {
            // World creation runs every definition check, including each faction's reachability closure.
            var world = DefinitionLoader.CreateOfflineWorld(id, VictoryMode.Conquest, "sunscar_basin");
            var faction = Faction(world, id);
            Assert.AreEqual(kind, faction.Kind); Assert.AreEqual(ContentRealms.Historical, faction.RealmId);
            Assert.AreEqual(unique, faction.UniqueUnitId); Assert.AreEqual(technology, faction.UniqueTechnologyId);
            Assert.AreEqual(id, MeshyUnitVisuals.FactionId(kind));
            Assert.AreEqual(unique, FrontierCodex.SignatureUnit(id));
            var unit = UnitOf(world, unique);
            Assert.AreEqual(id, unit.RequiredFactionId); Assert.AreEqual("settlement", unit.RequiredEraId);
            Assert.AreNotEqual(CombatTags.None, unit.Tags & CombatTags.Cavalry, "Both desert soldiers are cavalry, so spears answer them.");
            CollectionAssert.Contains(world.Definition.Buildings.First(b => b.Id == "muster_hall").TrainableUnitIds, unique);
            var research = world.Definition.Technologies.First(t => t.Id == technology);
            Assert.AreEqual(id, research.RequiredFactionId); Assert.AreEqual("archive", research.ResearchBuildingId); Assert.AreEqual("dominion", research.RequiredEraId);
            Assert.IsTrue(research.Effects.All(e => e.TargetUnitId == unique));
            Assert.IsTrue(world.ValidateUnitRecruitment(1, unique).Accepted);
            Assert.IsFalse(world.ValidateUnitRecruitment(2, unique).Accepted, "The rival cannot field another faction's soldier.");
            Assert.IsFalse(world.ValidateUnitRecruitment(1, Faction(world, rival).UniqueUnitId).Accepted);
            Assert.IsFalse(world.ValidateFactionRequirement(1, rival).Accepted, "Nor research what only the rival may.");
            var aven = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            Assert.IsFalse(aven.ValidateUnitRecruitment(1, unique).Accepted, "Aven cannot field the desert soldier either.");
        }

        [Test]
        public void TheDesertSoldiersSpeakTheSharedCounterLanguage()
        {
            var world = DefinitionLoader.CreateOfflineWorld("sultanate", VictoryMode.Conquest, "sunscar_basin");
            var camel = UnitOf(world, "camel_archer"); var lancer = UnitOf(world, "quilted_lancer");
            Assert.AreEqual(CombatTags.Cavalry | CombatTags.Ranged | CombatTags.Light, camel.Tags);
            Assert.Greater(camel.Attack.ProjectileSpeedMillimetresPerSecond, 0, "The camel archer shoots.");
            Assert.AreEqual(CombatTags.Infantry, camel.Attack.Bonuses.Single().TargetTags);
            Assert.AreEqual(CombatTags.Cavalry | CombatTags.Heavy, lancer.Tags);
            Assert.AreEqual(0, lancer.Attack.ProjectileSpeedMillimetresPerSecond, "The quilted lancer strikes in melee.");
            Assert.AreEqual(CombatTags.Ranged, lancer.Attack.Bonuses.Single().TargetTags);
            // The existing counters reach them: spears against both riders, cavalry against the mounted archer.
            Assert.IsTrue(UnitOf(world, "reedguard").Attack.Bonuses.Any(b => (b.TargetTags & camel.Tags) != 0 && (b.TargetTags & lancer.Tags) != 0));
            Assert.IsTrue(UnitOf(world, "strider").Attack.Bonuses.Any(b => (b.TargetTags & camel.Tags) != 0));
            // The camel archer, even with the Sultanato's ten percent, stays slower than a Strider and shorter-ranged than a Stringwarden.
            Assert.Less(camel.MoveSpeedMillimetresPerSecond * (1000 + Faction(world, "sultanate").CavalrySpeedBonusPermille) / 1000, UnitOf(world, "strider").MoveSpeedMillimetresPerSecond);
            Assert.Less(camel.Attack.RangeMillimetres, UnitOf(world, "stringwarden").Attack.RangeMillimetres);
        }

        // ------------------------------------------------------------------ a scripted match on Sunscar Basin

        [TestCase("sultanate")]
        [TestCase("sahel")]
        public void EachFactionBuildsEveryBuildingTrainsEveryUnitAndResearchesItsOwnTechnology(string id)
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(id, VictoryMode.Conquest, "sunscar_basin");
            var rules = baseline.Definition;
            rules.StartingResources = new ResourceAmount(60000, 60000, 60000, 60000); rules.BasePopulationCapacity = 100;
            var world = new World(rules, baseline.Map);
            var faction = Faction(world, id);
            var hearth = world.Buildings.First(b => b.OwnerId == 1 && b.DefinitionId == "hearth");
            int[] workers = world.Units.Where(u => u.OwnerId == 1 && u.IsWorker).Select(u => u.Id).ToArray();
            Assert.IsNotEmpty(workers);
            var built = new Dictionary<string, BuildingState> { { "hearth", hearth } };
            // The dock needs a shore in sight, which the basin's wadi is not from the Hearth; NavalTests raise it on the coast.
            var buildable = world.Definition.Buildings.Where(b => world.ValidateFactionRequirement(1, b.RequiredFactionId).Accepted && !b.RequiresShore).ToList();
            Assert.IsFalse(buildable.Any(b => b.Id == "supply_outpost"), "The outpost is Serevin's.");

            void Research(string building, string technology)
            {
                var site = built[building];
                Accepted(world.Submit(new ResearchCommand(1, site.Id, technology)), id + " researches " + technology);
                for (int tick = 0; tick < 3000 && site.ActiveResearch != null; tick++) world.Tick();
                Assert.IsTrue(world.TryGetPlayer(1, out var player) && player.HasTechnology(technology), id + " completed " + technology);
            }
            void Build(string building)
            {
                var definition = world.Definition.Buildings.First(b => b.Id == building);
                SimPoint site = default; bool found = false;
                // Rings of candidate sites around the Hearth, nearest first.
                for (int ring = 3; ring < 40 && !found; ring++)
                    for (int step = 0; step < ring * 8 && !found; step++)
                    {
                        double angle = step * Math.PI * 2 / (ring * 8);
                        var candidate = new SimPoint(hearth.Position.X + (int)(Math.Cos(angle) * ring * 1000), hearth.Position.Z + (int)(Math.Sin(angle) * ring * 1000));
                        candidate = new SimPoint(candidate.X / 1000 * 1000 + (definition.WidthCells % 2 == 1 ? 500 : 0), candidate.Z / 1000 * 1000 + (definition.DepthCells % 2 == 1 ? 500 : 0));
                        if (world.ValidatePlacement(1, workers, building, candidate).Accepted) { site = candidate; found = true; }
                    }
                Assert.IsTrue(found, id + " finds a site for " + building);
                var before = new HashSet<int>(world.Buildings.Select(b => b.Id));
                Accepted(world.Submit(new BuildCommand(1, workers, building, site)), id + " builds " + building);
                BuildingState placed = null;
                for (int tick = 0; tick < 6000 && (placed == null || !placed.IsComplete); tick++)
                {
                    world.Tick();
                    placed ??= world.Buildings.FirstOrDefault(b => !before.Contains(b.Id) && b.OwnerId == 1 && b.DefinitionId == building);
                }
                Assert.IsNotNull(placed, id + " placed " + building); Assert.IsTrue(placed.IsComplete, id + " completed " + building);
                built[building] = placed;
            }
            void BuildEra(string era) { foreach (var b in buildable.Where(b => (b.RequiredEraId ?? "settlement") == era && !built.ContainsKey(b.Id))) Build(b.Id); }

            BuildEra("settlement");
            Research("hearth", "advance_kingdom");
            BuildEra("kingdom");
            Research("hearth", "advance_dominion");
            BuildEra("dominion");
            CollectionAssert.AreEquivalent(buildable.Select(b => b.Id), built.Keys, id + " raised every building open to it.");

            // Every unit some building of its trains for this faction, in its own roster and realm.
            var trainable = new List<(string Unit, string Building)>();
            foreach (var building in built.Values)
                foreach (string unit in world.Definition.Buildings.First(b => b.Id == building.DefinitionId).TrainableUnitIds ?? Array.Empty<string>())
                    if (world.ValidateUnitRecruitment(1, unit).Accepted && world.ValidateRequirements(1, UnitOf(world, unit).RequiredEraId, UnitOf(world, unit).RequiredTechnologyIds).Accepted)
                        trainable.Add((unit, building.DefinitionId));
            CollectionAssert.IsSubsetOf(new[] { "tender", "reedguard", "stringwarden", "strider", faction.UniqueUnitId, "siege_ram", "siege_ladder", "siege_tower" },
                trainable.Select(t => t.Unit).ToArray());
            CollectionAssert.IsEmpty(trainable.Where(t => !string.IsNullOrEmpty(UnitOf(world, t.Unit).RequiredFactionId) && UnitOf(world, t.Unit).RequiredFactionId != id));
            foreach (var (unit, building) in trainable)
            {
                int before = world.Units.Count(u => u.OwnerId == 1 && u.DefinitionId == unit);
                Accepted(world.Submit(new TrainCommand(1, built[building].Id, unit)), id + " trains " + unit);
                for (int tick = 0; tick < 1200 && world.Units.Count(u => u.OwnerId == 1 && u.DefinitionId == unit) == before; tick++) world.Tick();
                Assert.AreEqual(before + 1, world.Units.Count(u => u.OwnerId == 1 && u.DefinitionId == unit), id + " fielded " + unit);
            }

            // Its own research makes its own soldier better, the one already fielded as well as the next.
            var soldier = world.Units.First(u => u.OwnerId == 1 && u.DefinitionId == faction.UniqueUnitId);
            int damage = soldier.AttackDamage, armor = soldier.Armor;
            Research("archive", faction.UniqueTechnologyId);
            var effect = world.Definition.Technologies.First(t => t.Id == faction.UniqueTechnologyId).Effects.Single();
            Assert.AreEqual(damage + (effect.Kind == TechnologyEffectKind.AttackDamage ? effect.Amount : 0), soldier.AttackDamage, id + " damage after research");
            Assert.AreEqual(armor + (effect.Kind == TechnologyEffectKind.Armor ? effect.Amount : 0), soldier.Armor, id + " armor after research");
            Assert.AreEqual(2, effect.Amount);
            var reedguard = world.Units.First(u => u.OwnerId == 1 && u.DefinitionId == "reedguard");
            Assert.AreEqual(UnitOf(world, "reedguard").Armor + (id == "sahel" ? 1 : 0), reedguard.Armor, "Only the Sahel's woven shields add infantry armor.");
        }

        [Test]
        public void TheSultanatoQuarriesMoreStoneAndTheSahelGathersMoreFood()
        {
            var sultanate = Faction(DefinitionLoader.CreateOfflineWorld("sultanate", VictoryMode.Conquest, "sunscar_basin"), "sultanate");
            Assert.AreEqual(ResourceKind.Stone, sultanate.BonusGatherResource); Assert.AreEqual(1, sultanate.ResourceGatherBonus);
            Assert.AreEqual(100, sultanate.CavalrySpeedBonusPermille); Assert.AreEqual(0, sultanate.WorkerCarryBonus);
            var sahel = Faction(DefinitionLoader.CreateOfflineWorld("sahel", VictoryMode.Conquest, "sunscar_basin"), "sahel");
            Assert.AreEqual(ResourceKind.Food, sahel.BonusGatherResource); Assert.AreEqual(1, sahel.ResourceGatherBonus);
            Assert.AreEqual(CombatTags.Infantry, sahel.ArmorBonusTags); Assert.AreEqual(1, sahel.ArmorBonus);
        }

        // ------------------------------------------------------------------ the offline AI plays them

        [Test]
        public void ANaturalHardMatchBetweenTheDesertFactionsIsWonByConquest()
        {
            var world = DefinitionLoader.CreateOfflineWorld("sahel", VictoryMode.Conquest, "sunscar_basin");
            var ai = new[] { new OfflineAi(world, 1, OfflineAiDifficulty.Hard), new OfflineAi(world, 2, OfflineAiDifficulty.Hard) };
            var fielded = new[] { new HashSet<int>(), new HashSet<int>() };
            string[] uniques = { "quilted_lancer", "camel_archer" };
            // Forty-five minutes: the probe runs recorded in docs/design/DESERT_FACTIONS.md end in 8 to 25.
            while (world.TickIndex < World.TickRate * 60 * 45 && !world.Match.IsFinished)
            {
                // The offline render probe's alternating order, so neither seat always thinks first.
                if (world.TickIndex % 20 < 10) { ai[0].Tick(); ai[1].Tick(); } else { ai[1].Tick(); ai[0].Tick(); }
                world.Tick();
                if (world.TickIndex % 20 == 0)
                    foreach (var unit in world.Units)
                        if (unit.OwnerId is 1 or 2 && unit.DefinitionId == uniques[unit.OwnerId - 1]) fielded[unit.OwnerId - 1].Add(unit.Id);
            }
            Assert.IsTrue(world.Match.IsFinished, "The match ends.");
            Assert.AreEqual(MatchEndReason.Conquest, world.Match.Reason);
            Assert.That(world.Match.WinnerId, Is.EqualTo(1).Or.EqualTo(2));
            Assert.Greater(fielded[0].Count, 0, "The Sahel AI fields Quilted Lancers."); Assert.Greater(fielded[1].Count, 0, "The Sultanato AI fields Camel Archers.");
            Assert.IsFalse(world.Buildings.Any(b => b.OwnerId is 1 or 2 && b.DefinitionId == "beast_lodge"), "Neither raises a Beast Lodge it has no creature for.");
            TestContext.WriteLine($"Natural match: {world.Match.Reason} by player {world.Match.WinnerId} after {world.TickIndex / (double)World.TickRate / 60:0.0} min; lancers {fielded[0].Count}, camel archers {fielded[1].Count}.");
        }

        // ------------------------------------------------------------------ counter probes (docs/design/DESERT_FACTIONS.md §7)

        private static readonly Dictionary<string, string> Owners = new Dictionary<string, string> { { "camel_archer", "sultanate" }, { "quilted_lancer", "sahel" } };

        // Two groups 7 m apart on open 1 m ground, each soldier ordered on its nearest enemy and re-ordered every second.
        // A kiting Camel Archer instead falls back five metres, curving, whenever an enemy closes to three.
        private static (int Winner, double Seconds, int Survivors, int Health) Fight(GameDefinition rules, string left, int leftCount, string right, int rightCount, bool kite = false)
        {
            var spawns = new List<UnitSpawnDefinition>(); int next = 1;
            for (int i = 0; i < leftCount; i++) spawns.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 1, DefinitionId = left, Position = new SimPoint(50500 - i / 5 * 1000, 57500 + i % 5 * 1000) });
            for (int i = 0; i < rightCount; i++) spawns.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 2, DefinitionId = right, Position = new SimPoint(57500 + i / 5 * 1000, 57500 + i % 5 * 1000) });
            var world = new World(rules, new MapDefinition { Id = "desert_counter_probe", WidthCells = 120, HeightCells = 120, CellSizeMillimetres = 1000, UnitSpawns = spawns.ToArray(),
                PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = Owners.TryGetValue(left, out var a) ? a : "aven" },
                    new PlayerFactionDefinition { PlayerId = 2, FactionId = Owners.TryGetValue(right, out var b) ? b : "aven" } } });
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
                    if ((owner == 1 && kite) || (unit.AttackTargetId != 0 && world.TryGetUnit(unit.AttackTargetId, out _))) continue;
                    var target = Nearest(unit, 3 - owner); if (target != null) world.Submit(new AttackCommand(owner, new[] { unit.Id }, target.Id));
                }
            }
            var fleeing = new Dictionary<int, long>();
            int blue = leftCount, red = rightCount;
            while (world.TickIndex < World.TickRate * 180 && ((blue > 0 && red > 0) || world.Projectiles.Count > 0))
            {
                if (world.TickIndex % World.TickRate == 0) { Order(1); Order(2); }
                if (kite && world.TickIndex % 4 == 0)
                    foreach (var unit in world.Units.Where(u => u.OwnerId == 1).ToList())
                    {
                        var enemy = Nearest(unit, 2); if (enemy == null) continue;
                        long dx = unit.Position.X - enemy.Position.X, dz = unit.Position.Z - enemy.Position.Z;
                        bool resting = !fleeing.TryGetValue(unit.Id, out long until) || world.TickIndex >= until;
                        if (dx * dx + dz * dz < 3000L * 3000L)
                        {
                            if (!resting) continue;
                            double angle = Math.Atan2(dz, dx) + .6;
                            var away = new SimPoint((int)Math.Max(2000, Math.Min(118000, unit.Position.X + Math.Cos(angle) * 5000)), (int)Math.Max(2000, Math.Min(118000, unit.Position.Z + Math.Sin(angle) * 5000)));
                            if (world.Submit(new MoveCommand(1, new[] { unit.Id }, away)).Accepted) fleeing[unit.Id] = world.TickIndex + 16;
                        }
                        else if (resting && (unit.AttackTargetId == 0 || unit.Order == UnitOrder.Idle)) world.Submit(new AttackCommand(1, new[] { unit.Id }, enemy.Id));
                    }
                world.Tick(); blue = red = 0;
                foreach (var unit in world.Units) { if (unit.OwnerId == 1) blue++; else if (unit.OwnerId == 2) red++; }
            }
            int winner = blue > 0 && red == 0 ? 1 : red > 0 && blue == 0 ? 2 : 0;
            int survivors = winner == 1 ? blue : winner == 2 ? red : 0;
            int health = world.Units.Where(u => u.OwnerId == winner).Sum(u => u.Health);
            return (winner, world.TickIndex / (double)World.TickRate, survivors, health);
        }

        // Equal nominal resources: 1,680 buys 16 Camel Archers (105), 14 Quilted Lancers (120) or 21 of an 80-resource soldier.
        [TestCase("quilted_lancer", "camel_archer", 1, 1)]
        [TestCase("strider", "camel_archer", 1, 1)]
        [TestCase("reedguard", "camel_archer", 1, 1)]
        [TestCase("stringwarden", "camel_archer", 1, 1)]
        [TestCase("reedguard", "quilted_lancer", 1, 1)]
        [TestCase("quilted_lancer", "stringwarden", 1, 1)]
        [TestCase("quilted_lancer", "strider", 1, 1)]
        [TestCase("quilted_lancer", "camel_archer", 14, 16)]
        [TestCase("strider", "camel_archer", 21, 16)]
        [TestCase("reedguard", "camel_archer", 21, 16)]
        [TestCase("stringwarden", "camel_archer", 21, 16)]
        [TestCase("reedguard", "quilted_lancer", 21, 14)]
        [TestCase("strider", "quilted_lancer", 21, 14)]
        public void TheFavouredSideWinsAStandingFightFromEitherSeat(string favoured, string opposed, int favouredCount, int opposedCount)
        {
            var rules = DefinitionLoader.CreateWorld().Definition;
            var first = Fight(rules, favoured, favouredCount, opposed, opposedCount);
            var second = Fight(rules, opposed, opposedCount, favoured, favouredCount);
            TestContext.WriteLine($"{favouredCount} {favoured} vs {opposedCount} {opposed}: seat 1 winner {first.Winner} in {first.Seconds:0.00} s, {first.Survivors} left / {first.Health} health; " +
                $"seat 2 winner {second.Winner} in {second.Seconds:0.00} s, {second.Survivors} left / {second.Health} health");
            Assert.AreEqual(1, first.Winner, favoured + " from the first seat");
            Assert.AreEqual(2, second.Winner, favoured + " from the second seat");
        }

        [TestCase(1, 1)]
        [TestCase(16, 21)]
        public void AKitingCamelArcherBeatsTheSpearsThatWouldCatchItStanding(int archers, int spears)
        {
            var rules = DefinitionLoader.CreateWorld().Definition;
            var standing = Fight(rules, "camel_archer", archers, "reedguard", spears);
            var kiting = Fight(rules, "camel_archer", archers, "reedguard", spears, kite: true);
            TestContext.WriteLine($"{archers} camel_archer vs {spears} reedguard: standing winner {standing.Winner}; kiting winner {kiting.Winner} in {kiting.Seconds:0.00} s, {kiting.Survivors} left / {kiting.Health} health");
            Assert.AreEqual(2, standing.Winner, "Standing, the spears reach the archers.");
            Assert.AreEqual(1, kiting.Winner, "Falling back, the archers are never caught.");
            Assert.GreaterOrEqual(kiting.Survivors, archers - 1);
        }

        // ------------------------------------------------------------------ the culture each one is drawn in

        [TestCase(FactionKind.DesertSultanate, "sultanate")]
        [TestCase(FactionKind.SahelConfederation, "sahel")]
        public void EachDesertFactionIsDrawnInItsOwnCulture(FactionKind kind, string culture)
        {
            var world = DefinitionLoader.CreateOfflineWorld(culture, VictoryMode.Conquest, "sunscar_basin");
            foreach (string unit in new[] { "tender", "reedguard", "stringwarden", "strider", Faction(world, culture).UniqueUnitId })
            {
                var entry = MeshyUnitVisuals.Resolve(unit, kind);
                Assert.IsNotNull(entry, culture + " draws a Meshy " + unit);
                Assert.AreEqual(culture, entry.culture, unit);
                Assert.IsNotNull(Resources.Load<GameObject>(entry.prefab), entry.id + " prefab");
            }
            // Every culture's dock is the coast's shared harbour quay (WorldView.DockModel), not a building of its own.
            foreach (var building in world.Definition.Buildings.Where(b => world.ValidateFactionRequirement(1, b.RequiredFactionId).Accepted && !b.RequiresShore).Select(b => b.Id).Append(MeshyBuildingVisuals.GateLeaf))
            {
                var entry = MeshyBuildingVisuals.Resolve(building, kind);
                Assert.IsNotNull(entry, culture + " draws a Meshy " + building);
                Assert.AreEqual(culture, entry.style, building);
                Assert.IsNotNull(Resources.Load<GameObject>(entry.prefab), entry.id + " prefab");
            }
            Assert.AreEqual(culture + "_siege_ladder", MeshyPropVisuals.ResolveSiege("siege_ladder", kind)?.id);
            // The desert soldiers have a procedural stand-in too, for -emberfieldProceduralUnits and the art-free paths.
            Assert.IsTrue(AlphaWorldArt.Mounted(Faction(world, culture).UniqueUnitId));
            Assert.Contains(Faction(world, culture).UniqueUnitId, AlphaWorldArt.UnitIds);
        }
    }
}
