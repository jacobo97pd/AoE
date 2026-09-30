using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Emberfield.Simulation;

internal static class ExpansionVerification
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
    private static string RulesPath = "Assets/Game/Resources/Definitions/greybox.json";
    private static byte[] frozenRules;
    private static string simulationSha;
    private static readonly Dictionary<string, byte[]> frozenMaps = new Dictionary<string, byte[]>();
    private static readonly List<object> Results = new List<object>();
    private static string output;
    private static int failures;
    private static GameDefinition Rules() => JsonSerializer.Deserialize<GameDefinition>(frozenRules, Json);
    private static MapDefinition Map(string id) => JsonSerializer.Deserialize<MapDefinition>(frozenMaps[id], Json);
    private static string Argument(string[] args, string name, string fallback) { int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
    private static int Main(string[] args)
    {
        output = Argument(args, "--output", "TestResults/ExpansionSimulation/expansion-verification.json");
        RulesPath = Argument(args, "--rules", RulesPath); frozenRules = File.ReadAllBytes(RulesPath);
        using (var source = new MemoryStream())
        {
            foreach (string path in Directory.GetFiles("Assets/Game/Simulation", "*.cs").OrderBy(p => p, StringComparer.Ordinal))
            {
                var name = System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\n"); source.Write(name);
                source.Write(File.ReadAllBytes(path));
            }
            simulationSha = Convert.ToHexString(SHA256.HashData(source.ToArray()));
        }
        foreach (string map in new[] { "amber_crossing", "sapphire_coast", "sunscar_basin" }) frozenMaps.Add(map, File.ReadAllBytes("Assets/Game/Resources/Maps/" + map + ".json"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        int seconds = int.Parse(Argument(args, "--seconds", "1800"));
        string mapFilter = Argument(args, "--map", "all"), factionFilter = Argument(args, "--faction", "all"), modeFilter = Argument(args, "--mode", "all");
        if (!args.Contains("--natural-only"))
        {
            foreach (var faction in Rules().Factions) Run("production " + faction.Id, () => Production(faction.Id));
            foreach (string creature in new[] { "dune_elephant", "sun_lion", "grove_guardian", "war_troll", "ember_drake" })
            {
                foreach (string counter in new[] { "reedguard", "stringwarden", "strider" })
                foreach (bool equalCost in new[] { false, true })
                    Run("counter " + creature + " " + counter + " equalCost=" + equalCost, () => Counter(creature, counter, equalCost));
                Run("DPS " + creature, () => DamageBudget(creature));
            }
            Run("dispersed ranged focus ember_drake", () => Counter("ember_drake", "stringwarden", false, true));
        }
        if (!args.Contains("--fixtures-only"))
        foreach (string map in new[] { "amber_crossing", "sapphire_coast", "sunscar_basin" })
        foreach (var faction in Rules().Factions)
        foreach (var mode in new[] { VictoryMode.Conquest, VictoryMode.Dominion })
        {
            if (mapFilter != "all" && map != mapFilter || factionFilter != "all" && faction.Id != factionFilter || modeFilter != "all" && mode.ToString() != modeFilter) continue;
            Run("natural " + map + " " + faction.Id + " " + mode, () => Natural(map, faction.Id, mode, seconds));
        }
        Write(); Console.WriteLine("EXPANSION_VERIFICATION_COMPLETE failures=" + failures + " cases=" + Results.Count); return failures == 0 ? 0 : 1;
    }
    private static void Run(string name, Action action)
    {
        Console.WriteLine("START " + name);
        try { action(); Console.WriteLine("DONE " + name); }
        catch (Exception error) { failures++; Results.Add(new { Case = name, Error = error.ToString() }); Console.WriteLine("FAIL " + name + " " + error.Message); }
        Write();
    }
    private static void Write() => File.WriteAllText(output, JsonSerializer.Serialize(new {
        Utc = DateTime.UtcNow, RulesSha256 = Convert.ToHexString(SHA256.HashData(frozenRules)), SimulationSha256 = simulationSha,
        Method = "Natural matches use authored resources, ordinary commands and restricted AI observations. Production and combat fixtures explicitly spawn infrastructure/armies with test inventory; they verify rules and report pacing, not ranked balance or rendering performance.",
        Failures = failures, Cases = Results }, Json));
    private static void Accepted(CommandResult result) { if (!result.Accepted) throw new Exception(result.Reason + ": " + result.Message); }
    private static UnitSpawnDefinition U(int id, int owner, string definition, int x, int z) => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
    private static BuildingSpawnDefinition B(int id, int owner, string definition, int x, int z) => new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
    private static PlayerFactionDefinition[] Factions(string faction) => new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = faction }, new PlayerFactionDefinition { PlayerId = 2, FactionId = ContentRealms.OpponentFaction(faction) } };
    private static void Tick(World world, int ticks) { for (int i = 0; i < ticks; i++) world.Tick(); }

    private static void Production(string factionId)
    {
        var rules = Rules(); rules.StartingResources = new ResourceAmount(20000, 20000, 20000, 20000); rules.BasePopulationCapacity = 100;
        var faction = rules.Factions.Single(f => f.Id == factionId);
        var map = new MapDefinition { RealmId = faction.RealmId, PlayerFactions = Factions(factionId),
            BuildingSpawns = new[] { B(100, 1, "hearth", 6000, 6000), B(101, 1, "archive", 12500, 6500), B(102, 1, "muster_hall", 6500, 13500),
                B(103, 1, "beast_lodge", 15000, 13500), B(104, 1, "siege_workshop", 22500, 13500) } };
        var world = new World(rules, map);
        foreach (string technology in new[] { "advance_kingdom", "gather_1", "advance_dominion", "gather_2", "advance_empire", faction.UniqueTechnologyId })
        {
            var definition = rules.Technologies.Single(t => t.Id == technology); var site = world.Buildings.First(b => b.DefinitionId == definition.ResearchBuildingId);
            Accepted(world.Submit(new ResearchCommand(1, site.Id, technology))); Tick(world, definition.ResearchTicks);
        }
        foreach (string unitId in new[] { faction.UniqueUnitId, "siege_ram", "siege_ladder", "siege_tower" })
        {
            var producer = rules.Buildings.First(b => b.TrainableUnitIds.Contains(unitId)); var site = world.Buildings.First(b => b.DefinitionId == producer.Id);
            Accepted(world.Submit(new TrainCommand(1, site.Id, unitId))); Tick(world, rules.Units.Single(u => u.Id == unitId).TrainTicks + 20);
            if (!world.Units.Any(u => u.DefinitionId == unitId)) throw new Exception("Training did not produce " + unitId);
        }
        world.TryGetPlayer(1, out var player);
        Results.Add(new { Type = "production", Faction = factionId, player.EraTier, Unique = faction.UniqueUnitId, Research = player.CompletedTechnologyIds, Units = world.Units.Select(u => new { u.DefinitionId, u.AttackDamage, u.Armor }).ToArray(), Passed = true });
    }

    private static void Counter(string creatureId, string counterId, bool equalCost, bool dispersed = false)
    {
        var rules = Rules(); rules.BasePopulationCapacity = 100;
        var creature = rules.Units.Single(u => u.Id == creatureId); var counter = rules.Units.Single(u => u.Id == counterId);
        int Total(ResourceAmount value) => value.Food + value.Wood + value.Metal + value.Stone;
        int count = Math.Max(1, equalCost ? Total(creature.Cost) / Total(counter.Cost) : creature.PopulationCost / counter.PopulationCost);
        var spawns = new List<UnitSpawnDefinition> { U(1, 1, creatureId, 22000, 22000) };
        var spread = new[] { new SimPoint(17000, 22000), new SimPoint(22000, 17000), new SimPoint(27000, 22000), new SimPoint(22000, 27000), new SimPoint(18500, 18500) };
        for (int i = 0; i < count; i++) spawns.Add(U(10 + i, 2, counterId, dispersed ? spread[i].X : 17000, dispersed ? spread[i].Z : 20000 + i * 1000));
        var world = new World(rules, new MapDefinition { RealmId = ContentRealms.RealmForFaction(creature.RequiredFactionId), PlayerFactions = Factions(creature.RequiredFactionId), UnitSpawns = spawns.ToArray() });
        Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 10)));
        Accepted(world.Submit(new AttackCommand(2, Enumerable.Range(10, count).ToArray(), 1)));
        while (world.TickIndex < 2400 && world.Units.Any(u => u.OwnerId == 1) && world.Units.Any(u => u.OwnerId == 2)) world.Tick();
        Results.Add(new { Type = dispersed ? "dispersed-ranged-focus" : equalCost ? "equal-resource-budget-counter" : "equal-population-counter", Creature = creatureId, Counter = counterId, CounterCount = count, creature.PopulationCost,
            CreatureCost = creature.Cost, CounterCostEach = counter.Cost, Seconds = world.TickIndex / 20d,
            CreatureSurvivors = world.Units.Count(u => u.OwnerId == 1), CounterSurvivors = world.Units.Count(u => u.OwnerId == 2),
            RemainingHealth = world.Units.Select(u => new { u.OwnerId, u.DefinitionId, u.Health }).ToArray() });
    }

    private static void DamageBudget(string creatureId)
    {
        var rules = Rules(); var creature = rules.Units.Single(u => u.Id == creatureId);
        var dummy = new UnitDefinition { Id = "verification_target", MaxHealth = 100000, Armor = 2, Tags = CombatTags.Infantry };
        rules.Units = rules.Units.Concat(new[] { dummy }).ToArray();
        var world = new World(rules, new MapDefinition { RealmId = ContentRealms.RealmForFaction(creature.RequiredFactionId), PlayerFactions = Factions(creature.RequiredFactionId),
            UnitSpawns = new[] { U(1, 1, creatureId, 20000, 20000), U(2, 2, dummy.Id, 21000, 20000) } });
        Accepted(world.Submit(new AttackCommand(1, new[] { 1 }, 2))); Tick(world, 400);
        world.TryGetUnit(2, out var target);
        Results.Add(new { Type = "DPS-against-two-armor-infantry", Creature = creatureId, creature.MaxHealth, creature.PopulationCost, creature.Cost,
            creature.MoveSpeedMillimetresPerSecond, creature.Attack.RangeMillimetres, DamageOverTwentySeconds = dummy.MaxHealth - target.Health,
            DamagePerSecond = (dummy.MaxHealth - target.Health) / 20d, creature.TrainTicks });
    }

    private static void Natural(string mapId, string factionId, VictoryMode mode, int seconds)
    {
        var map = Map(mapId); map.RealmId = ContentRealms.RealmForFaction(factionId); map.PlayerFactions = Factions(factionId); map.OfflineMatch.Mode = mode;
        var world = new World(Rules(), map); var ais = new[] { new OfflineAi(world, 1), new OfflineAi(world, 2) };
        var seenUnits = new HashSet<string>[2] { new HashSet<string>(), new HashSet<string>() };
        var seenBuildings = new HashSet<string>[2] { new HashSet<string>(), new HashSet<string>() };
        var rejects = new Dictionary<string, int>();
        foreach (var ai in ais) ai.CommandIssued += (command, result) =>
        {
            if (result.Accepted && command is AttackCommand attack && !world.Vision.IsEntityVisible(command.PlayerId, attack.TargetEntityId))
                throw new Exception("AI attacked an entity outside its visible observation.");
            if (!result.Accepted) { string key = command.GetType().Name + ":" + result.Reason; rejects.TryGetValue(key, out int count); rejects[key] = count + 1; }
        };
        var timer = Stopwatch.StartNew();
        while (!world.Match.IsFinished && world.TickIndex < seconds * (long)World.TickRate)
        {
            if (world.TickIndex % 20 < 10) { ais[0].Tick(); ais[1].Tick(); } else { ais[1].Tick(); ais[0].Tick(); }
            world.Tick();
            if (world.TickIndex % 20 != 0) continue;
            foreach (var unit in world.Units) seenUnits[unit.OwnerId - 1].Add(unit.DefinitionId);
            foreach (var building in world.Buildings) seenBuildings[building.OwnerId - 1].Add(building.DefinitionId);
        }
        var players = ais.Select(ai => { world.TryGetPlayer(ai.PlayerId, out var player); return new {
            player.Id, player.FactionId, player.EraTier, TechnologyCount = player.CompletedTechnologyIds.Count, player.Resources,
            player.PopulationUsed, player.PopulationCapacity, ai.Status, ai.Statistics, ProducedUnitTypes = seenUnits[player.Id - 1], BuiltTypes = seenBuildings[player.Id - 1] }; }).ToArray();
        Console.WriteLine("RESULT " + mapId + " " + factionId + " " + mode + " finished=" + world.Match.IsFinished + " seconds=" + world.TickIndex / 20d + " deaths=" + world.DeathCount + " eras=" + string.Join(",", players.Select(p => p.EraTier)));
        Results.Add(new { Type = "natural-match", Map = mapId, MapSha256 = Convert.ToHexString(SHA256.HashData(frozenMaps[mapId])), Faction = factionId, Mode = mode.ToString(),
            world.Match.IsFinished, world.Match.WinnerId, Reason = world.Match.Reason.ToString(), Seconds = world.TickIndex / 20d, world.DeathCount,
            WallSeconds = timer.Elapsed.TotalSeconds, Players = players, Rejections = rejects, DeadlineReached = !world.Match.IsFinished });
    }
}
