using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emberfield.Diagnostics;
using Emberfield.Simulation;

// Rules audit harness: targeted probes of the shipped rules, pirate matchups, and complete
// matches of the scripted player against the game's own OfflineAi. Run from the repo root.
internal static class RulesAudit
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
    // --rules <file> loads an alternative definition set, so balance candidates can be measured
    // side by side without editing the shipped greybox.json.
    private static readonly string RulesPath = Argument(Environment.GetCommandLineArgs(), "--rules", "Assets/Game/Resources/Definitions/greybox.json");
    private static GameDefinition Rules() => JsonSerializer.Deserialize<GameDefinition>(File.ReadAllBytes(RulesPath), Json);
    private static MapDefinition Map(string id) => JsonSerializer.Deserialize<MapDefinition>(File.ReadAllBytes("Assets/Game/Resources/Maps/" + id + ".json"), Json);
    private static readonly List<object> Results = new List<object>();
    private static string Argument(string[] args, string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }

    private static int Main(string[] args)
    {
        string output = Argument(args, "--output", "TestResults/RulesAudit/results.json");
        if (args.Contains("--probes")) { DominionCapture(); FoundationSurvival(); Matchups(); }
        if (args.Contains("--mirror")) Mirror();
        if (args.Contains("--ladder")) Ladder(Argument(args, "--difficulty", "Hard"), Argument(args, "--opponent", "Normal"), int.Parse(Argument(args, "--seconds", "2400")));
        if (args.Contains("--natural")) NaturalSample(Argument(args, "--map", "all"), Argument(args, "--mode", "all"), Argument(args, "--difficulty", "Normal"), args.Contains("--mirror-factions"),
            args.Contains("--renumber"), int.Parse(Argument(args, "--seconds", "1800")), Argument(args, "--faction", "all"));
        if (args.Contains("--race")) Race();
        if (args.Contains("--divergence"))
            foreach (string m in Argument(args, "--map", "sunscar_basin").Split(','))
                foreach (string f in Argument(args, "--faction", "aven").Split(','))
                    Divergence(m, f, (VictoryMode)Enum.Parse(typeof(VictoryMode), Argument(args, "--mode", "Dominion")), int.Parse(Argument(args, "--ticks", "6000")));
        if (args.Contains("--match"))
        {
            string map = Argument(args, "--map", "sapphire_coast"), faction = Argument(args, "--faction", "aven");
            var mode = (VictoryMode)Enum.Parse(typeof(VictoryMode), Argument(args, "--mode", "Conquest"));
            int seconds = int.Parse(Argument(args, "--seconds", "2400"));
            foreach (string m in map.Split(',')) foreach (string f in faction.Split(',')) Match(m, f, mode, seconds, args.Contains("--verbose"));
        }
        File.WriteAllText(output, JsonSerializer.Serialize(Results, Json));
        Console.WriteLine("RULES_AUDIT_DONE " + Results.Count + " results -> " + output);
        return 0;
    }

    private static PlayerFactionDefinition[] Seats(string faction) => new[] {
        new PlayerFactionDefinition { PlayerId = 1, FactionId = faction },
        new PlayerFactionDefinition { PlayerId = 2, FactionId = ContentRealms.OpponentFaction(faction) } };
    private static World OfflineWorld(GameDefinition rules, string mapId, string faction, VictoryMode mode, Action<MapDefinition> edit = null)
    {
        var map = Map(mapId); map.RealmId = ContentRealms.RealmForFaction(faction); map.PlayerFactions = Seats(faction); map.OfflineMatch.Mode = mode;
        edit?.Invoke(map);
        return new World(rules, map);
    }
    private static void Tick(World world, int ticks) { for (int i = 0; i < ticks && (world.Match == null || !world.Match.IsFinished); i++) world.Tick(); }
    private static UnitSpawnDefinition U(int id, int owner, string definition, SimPoint p) => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = p };

    // Free, walkable spots in a spiral around a point on a map, spaced for a given radius.
    private static List<SimPoint> Spots(World probe, SimPoint centre, int count, int spacing)
    {
        var spots = new List<SimPoint>();
        for (int ring = 0; ring < 20 && spots.Count < count; ring++)
        for (int z = -ring; z <= ring && spots.Count < count; z++)
        for (int x = -ring; x <= ring && spots.Count < count; x++)
        {
            if (Math.Abs(x) != ring && Math.Abs(z) != ring) continue;
            // Cell centres whose whole neighbourhood is open, so a 0.3 m body clears every obstacle.
            var p = new SimPoint((centre.X / 1000 + x * spacing / 1000) * 1000 + 500, (centre.Z / 1000 + z * spacing / 1000) * 1000 + 500);
            bool open = true;
            for (int oz = -1; oz <= 1 && open; oz++) for (int ox = -1; ox <= 1 && open; ox++) open = probe.IsWalkable(new SimPoint(p.X + ox * 1000, p.Z + oz * 1000));
            if (!open || probe.Units.Any(u => SimPoint.DistanceCeiling(u.Position, p) < spacing) || spots.Contains(p)) continue;
            spots.Add(p);
        }
        return spots;
    }

    // ------------------------------------------------------------------ probes

    private static void DominionCapture()
    {
        var cases = new[] { ("reedguard", "aven"), ("boarding_raider", "aven"), ("crimson_corsair", "aven"), ("gunpowder_corsair", "aven"), ("siege_ram", "aven"),
            ("frostguard", "skeld"), ("dune_elephant", "miraj"), ("sun_lion", "solar"), ("grove_guardian", "verdant"), ("war_troll", "ashen"), ("ember_drake", "drakeforged") };
        var rules = Rules();
        foreach (var (unit, faction) in cases)
        {
            SimPoint beacon = default; string beaconId = null;
            var world = OfflineWorld(rules, "sapphire_coast", faction, VictoryMode.Dominion, map =>
            {
                var objective = map.OfflineMatch.Objectives[0]; beacon = objective.Position; beaconId = objective.Id;
                map.UnitSpawns = map.UnitSpawns.Concat(new[] { U(90001, 1, unit, beacon) }).ToArray();
            });
            Tick(world, 400);
            var state = world.Match.Objectives.First(o => o.Id == beaconId);
            var definition = rules.Units.Single(u => u.Id == unit);
            Results.Add(new { Probe = "dominion-capture", Unit = unit, Faction = faction, Tags = definition.Tags.ToString(), SecondsOnBeacon = 20, CapturedBy = state.OwnerId, Captured = state.OwnerId == 1 });
            Console.WriteLine($"CAPTURE {unit,-18} {definition.Tags,-32} owner={state.OwnerId}");
        }
    }

    private static void FoundationSurvival()
    {
        var rules = Rules(); rules.StartingResources = new ResourceAmount(5000, 5000, 5000, 5000);
        var probe = OfflineWorld(rules, "sapphire_coast", "aven", VictoryMode.Conquest);
        var enemyHearth = probe.Buildings.Single(b => b.OwnerId == 2 && b.DefinitionId == "hearth");
        var spots = Spots(probe, new SimPoint(enemyHearth.Position.X - 4000, enemyHearth.Position.Z - 4000), 20, 1000);
        var world = OfflineWorld(rules, "sapphire_coast", "aven", VictoryMode.Conquest, map =>
            map.UnitSpawns = map.UnitSpawns.Concat(spots.Select((p, i) => U(90000 + i, 1, "reedguard", p))).ToArray());
        var hearth = world.Buildings.Single(b => b.OwnerId == 2 && b.DefinitionId == "hearth");
        var tender = world.Units.First(u => u.OwnerId == 2 && u.IsWorker);
        // The defender places a replacement Hearth foundation it can see, as a player could.
        int foundation = 0;
        for (int dx = -14; dx <= 14 && foundation == 0; dx += 2)
        for (int dz = -14; dz <= 14 && foundation == 0; dz += 2)
        {
            var p = new SimPoint(hearth.Position.X + dx * 1000, hearth.Position.Z + dz * 1000);
            var result = world.Submit(new BuildCommand(2, new[] { tender.Id }, "hearth", p));
            if (result.Accepted) foundation = result.EntityId;
        }
        var raiders = world.Units.Where(u => u.OwnerId == 1 && u.DefinitionId == "reedguard").Select(u => u.Id).ToArray();
        var attack = world.Submit(new AttackCommand(1, raiders, hearth.Id));
        while (world.TryGetBuilding(hearth.Id, out _) && world.TickIndex < 6000) world.Tick();
        long original = world.TickIndex; bool alive = world.TryGetBuilding(foundation, out var site); int health = site?.Health ?? 0; int progress = site?.BuildProgressTicks ?? 0;
        bool finishedAfterOriginal = world.Match.IsFinished;
        if (alive && world.Vision.IsEntityVisible(1, foundation)) world.Submit(new AttackCommand(1, raiders.Where(id => world.TryGetUnit(id, out _)).ToArray(), foundation));
        else if (alive) world.Submit(new MoveCommand(1, raiders.Where(id => world.TryGetUnit(id, out _)).ToArray(), site.Position));
        for (int i = 0; i < 2400 && !world.Match.IsFinished; i++)
        {
            if (i % 20 == 0 && world.TryGetBuilding(foundation, out _) && world.Vision.IsEntityVisible(1, foundation))
                world.Submit(new AttackCommand(1, raiders.Where(id => world.TryGetUnit(id, out var u) && u.AttackTargetId != foundation).ToArray(), foundation));
            world.Tick();
        }
        Results.Add(new { Probe = "hearth-foundation", FoundationPlaced = foundation != 0, AttackAccepted = attack.Accepted, OriginalHearthDestroyedSeconds = original / 20d,
            MatchFinishedWhenOriginalFell = finishedAfterOriginal, FoundationHealthThen = health, FoundationProgressTicks = progress,
            Finished = world.Match.IsFinished, Winner = world.Match.WinnerId, Reason = world.Match.Reason.ToString(), FinishedSeconds = world.Match.ElapsedTicks / 20d });
        Console.WriteLine($"FOUNDATION placed={foundation != 0} originalFell={original / 20d}s finishedThen={finishedAfterOriginal} foundationHP={health} final={world.Match.Reason}@{world.Match.ElapsedTicks / 20d}s");
    }

    private static void Matchups()
    {
        var rules = Rules(); rules.BasePopulationCapacity = 200;
        int Price(string id) { var cost = rules.Units.Single(u => u.Id == id).Cost; return cost.Food + cost.Wood + cost.Metal + cost.Stone; }
        // Equal-cost armies are sized from the current definitions, so rebalancing never leaves stale labels.
        (int, string, int, string, string) Even(string a, string b, int budget)
        {
            int countA = Math.Max(1, budget / Price(a)), countB = Math.Max(1, budget / Price(b));
            return (countA, a, countB, b, $"igual coste {countA * Price(a)} vs {countB * Price(b)}");
        }
        (int, string, int, string, string) Hero(string troop) => (1, "crimson_corsair", 3, troop, $"heroe {Price("crimson_corsair")} vs {3 * Price(troop)}");
        var fights = new List<(int, string, int, string, string)>
        {
            (1, "boarding_raider", 1, "reedguard", "1v1"), (1, "boarding_raider", 1, "strider", "1v1"), (1, "boarding_raider", 1, "stringwarden", "1v1"),
            (1, "gunpowder_corsair", 1, "stringwarden", "1v1"), (1, "gunpowder_corsair", 1, "reedguard", "1v1"), (1, "gunpowder_corsair", 1, "strider", "1v1"),
            Hero("reedguard"), Hero("strider"), Hero("stringwarden"),
            Even("boarding_raider", "reedguard", 720), Even("boarding_raider", "strider", 720), Even("boarding_raider", "stringwarden", 720),
            Even("gunpowder_corsair", "stringwarden", 720), Even("gunpowder_corsair", "reedguard", 720), Even("gunpowder_corsair", "strider", 720),
            Even("reedguard", "stringwarden", 720), Even("reedguard", "strider", 720), Even("strider", "stringwarden", 720),
            (6, "boarding_raider", 6, "gunpowder_corsair", "6v6 piratas"),
        };
        foreach (var (countA, unitA, countB, unitB, label) in fights)
            foreach (bool swapped in new[] { false, true })
            {
                var spawns = new List<UnitSpawnDefinition>(); int id = 1;
                void Line(int owner, string unit, int count, int x) { for (int i = 0; i < count; i++) spawns.Add(U(id++, owner, unit, new SimPoint(x + (i % 3) * 1000, 14000 + (i / 3) * 1200 + (i % 3) * 400))); }
                Line(swapped ? 2 : 1, unitA, countA, 20000); Line(swapped ? 1 : 2, unitB, countB, 32000);
                var world = new World(rules, new MapDefinition { RealmId = "historical", WidthCells = 56, HeightCells = 32, UnitSpawns = spawns.ToArray() });
                for (int t = 0; t < 1800 && world.Units.Any(u => u.OwnerId == 1) && world.Units.Any(u => u.OwnerId == 2); t++)
                {
                    if (t % 20 == 0) foreach (int owner in new[] { 1, 2 })
                        foreach (var unit in world.Units.Where(u => u.OwnerId == owner && u.AttackTargetId == 0).ToList())
                        {
                            var enemy = world.Units.Where(e => e.OwnerId != owner).OrderBy(e => SimPoint.DistanceCeiling(e.Position, unit.Position)).FirstOrDefault();
                            if (enemy != null) world.Submit(new AttackCommand(owner, new[] { unit.Id }, enemy.Id));
                        }
                    world.Tick();
                }
                int ownerA = swapped ? 2 : 1;
                var survivorsA = world.Units.Where(u => u.OwnerId == ownerA).ToList(); var survivorsB = world.Units.Where(u => u.OwnerId != ownerA).ToList();
                var defA = rules.Units.Single(u => u.Id == unitA); var defB = rules.Units.Single(u => u.Id == unitB);
                string winner = survivorsA.Count > 0 && survivorsB.Count == 0 ? unitA : survivorsB.Count > 0 && survivorsA.Count == 0 ? unitB : "empate";
                Results.Add(new { Probe = "matchup", Label = label, A = unitA, CountA = countA, B = unitB, CountB = countB, Swapped = swapped, Seconds = world.TickIndex / 20d, Winner = winner,
                    SurvivorsA = survivorsA.Count, HealthA = survivorsA.Sum(u => u.Health), MaxA = countA * defA.MaxHealth, SurvivorsB = survivorsB.Count, HealthB = survivorsB.Sum(u => u.Health), MaxB = countB * defB.MaxHealth });
                Console.WriteLine($"FIGHT {label,-22} {countA}x{unitA} vs {countB}x{unitB} swapped={swapped}: {winner} in {world.TickIndex / 20d:0.0}s A {survivorsA.Count}/{countA} ({survivorsA.Sum(u => u.Health)} hp) B {survivorsB.Count}/{countB} ({survivorsB.Sum(u => u.Health)} hp)");
            }
    }

    // Natural matches between two AIs of one difficulty, each played from both starting bases and
    // with three think-timing offsets. Swapping bases separates a seat advantage from a side
    // advantage, and the offsets give every faction a larger sample than the verifier's 48 matches.
    // With renumber, a swapped match also gives seat 1 the lower spawn ids and the first places in the
    // spawn lists, as on the unswapped map, so an entity-order effect would follow the seat.
    private static void NaturalSample(string mapFilter, string modeFilter, string difficultyName, bool mirrorFactions, bool renumber, int seconds, string factionFilter = "all")
    {
        var factions = factionFilter == "all" ? null : new HashSet<string>(factionFilter.Split(','));
        var difficulty = (OfflineAiDifficulty)Enum.Parse(typeof(OfflineAiDifficulty), difficultyName, true);
        var phases = new[] { (0, 0), (0, 5), (5, 0) };
        foreach (string mapId in new[] { "amber_crossing", "sapphire_coast", "sunscar_basin" })
        {
            if (mapFilter != "all" && mapFilter != mapId) continue;
            foreach (string faction in new[] { "aven", "serevin", "miraj", "skeld", "solar", "verdant", "ashen", "drakeforged" })
            foreach (var mode in new[] { VictoryMode.Conquest, VictoryMode.Dominion })
            foreach (bool swapped in new[] { false, true })
            foreach (var (first, second) in phases)
            {
                if (modeFilter != "all" && !mode.ToString().Equals(modeFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (factions != null && !factions.Contains(faction)) continue;
                var world = OfflineWorld(Rules(), mapId, faction, mode, map =>
                {
                    if (mirrorFactions) map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = faction }, new PlayerFactionDefinition { PlayerId = 2, FactionId = faction } };
                    if (!swapped) return;
                    foreach (var unit in map.UnitSpawns ?? Array.Empty<UnitSpawnDefinition>()) if (unit.OwnerId != 0) unit.OwnerId = 3 - unit.OwnerId;
                    foreach (var building in map.BuildingSpawns ?? Array.Empty<BuildingSpawnDefinition>()) if (building.OwnerId != 0) building.OwnerId = 3 - building.OwnerId;
                    if (!renumber) return;
                    var units = map.UnitSpawns.OrderBy(s => s.OwnerId == 0 ? 3 : s.OwnerId).ThenBy(s => s.Id).ToArray();
                    var unitIds = map.UnitSpawns.Select(s => s.Id).OrderBy(id => id).ToArray();
                    for (int i = 0; i < units.Length; i++) units[i].Id = unitIds[i];
                    map.UnitSpawns = units;
                    var buildings = map.BuildingSpawns.OrderBy(s => s.OwnerId == 0 ? 3 : s.OwnerId).ThenBy(s => s.Id).ToArray();
                    var buildingIds = map.BuildingSpawns.Select(s => s.Id).OrderBy(id => id).ToArray();
                    for (int i = 0; i < buildings.Length; i++) buildings[i].Id = buildingIds[i];
                    map.BuildingSpawns = buildings;
                });
                var ais = new[] { new OfflineAi(world, 1, difficulty), new OfflineAi(world, 2, difficulty) };
                int[] start = { first, second };
                while (!world.Match.IsFinished && world.TickIndex < seconds * 20L)
                {
                    int lead = world.TickIndex % 20 < 10 ? 0 : 1;
                    for (int k = 0; k < 2; k++) { int i = (lead + k) % 2; if (world.TickIndex >= start[i]) ais[i].Tick(); }
                    world.Tick();
                }
                string opponent = mirrorFactions ? faction : ContentRealms.OpponentFaction(faction);
                int seat = world.Match.IsFinished ? world.Match.WinnerId : 0;
                string winner = seat == 1 ? faction : seat == 2 ? opponent : "none";
                // Seat 1 normally starts on the map's first base; a swapped match starts it on the second.
                string side = seat == 0 ? "none" : (seat == 1) != swapped ? "first" : "second";
                Results.Add(new { Probe = "natural-sample", Map = mapId, Faction = faction, Opponent = opponent, Mode = mode.ToString(), Swapped = swapped, Renumbered = swapped && renumber,
                    Phase = first + "/" + second, Difficulty = difficulty.ToString(), WinnerSeat = seat, WinnerFaction = winner, WinnerBase = side,
                    Seconds = world.TickIndex / 20d, Reason = world.Match.Reason.ToString() });
                Console.WriteLine($"NATURAL {mapId} {faction} vs {opponent} {mode} swapped={swapped} phase={first}/{second}: seat {seat} ({winner}, {side} base) at {world.TickIndex / 1200}:{world.TickIndex / 20 % 60:00}");
            }
        }
    }

    // AI against AI at two difficulties. The stronger one plays both seats on every map and
    // historical pairing, so a well-ordered ladder shows up as a clear majority.
    private static void Ladder(string high, string low, int seconds)
    {
        var strong = (OfflineAiDifficulty)Enum.Parse(typeof(OfflineAiDifficulty), high, true);
        var weak = (OfflineAiDifficulty)Enum.Parse(typeof(OfflineAiDifficulty), low, true);
        int strongWins = 0, weakWins = 0, open = 0;
        foreach (string mapId in new[] { "sapphire_coast", "amber_crossing", "sunscar_basin" })
        foreach (string faction in new[] { "aven", "serevin", "miraj", "skeld" })
        foreach (bool strongFirst in new[] { true, false })
        {
            var world = OfflineWorld(Rules(), mapId, faction, VictoryMode.Conquest);
            var ais = new[] { new OfflineAi(world, 1, strongFirst ? strong : weak), new OfflineAi(world, 2, strongFirst ? weak : strong) };
            while (!world.Match.IsFinished && world.TickIndex < seconds * 20L)
            {
                // Alternate which AI thinks first, as the natural-match verifier does.
                if (world.TickIndex % 20 < 10) { ais[0].Tick(); ais[1].Tick(); } else { ais[1].Tick(); ais[0].Tick(); }
                world.Tick();
            }
            int strongSeat = strongFirst ? 1 : 2;
            string winner = !world.Match.IsFinished || world.Match.WinnerId == 0 ? "none" : world.Match.WinnerId == strongSeat ? strong.ToString() : weak.ToString();
            if (winner == strong.ToString()) strongWins++; else if (winner == weak.ToString()) weakWins++; else open++;
            Results.Add(new { Probe = "ladder", Map = mapId, Faction = faction, Strong = strong.ToString(), Weak = weak.ToString(), StrongSeat = strongSeat,
                Winner = winner, Seconds = world.TickIndex / 20d, Reason = world.Match.Reason.ToString() });
            Console.WriteLine($"LADDER {mapId,-15} {faction,-8} P1={(strongFirst ? strong : weak),-6} P2={(strongFirst ? weak : strong),-6}: {winner} at {world.TickIndex / 1200}:{world.TickIndex / 20 % 60:00}");
        }
        Console.WriteLine($"LADDER_SUMMARY {strong} vs {weak}: {strong} {strongWins}, {weak} {weakWins}, unfinished or drawn {open}");
    }

    // Plays one mirror-faction natural match twice: on the map as authored, and turned through the
    // centre (seat 1 on the second base, keeping the lower entity ids). In a simulation that respects
    // the maps' 180-degree symmetry the second world is the first one rotated at every tick, command
    // for command. The first tick where the AI commands or the entities differ locates the asymmetry.
    private static void Divergence(string mapId, string faction, VictoryMode mode, int ticks)
    {
        World Build(bool turned) => OfflineWorld(Rules(), mapId, faction, mode, map =>
        {
            map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = faction }, new PlayerFactionDefinition { PlayerId = 2, FactionId = faction } };
            if (!turned) return;
            foreach (var unit in map.UnitSpawns) if (unit.OwnerId != 0) unit.OwnerId = 3 - unit.OwnerId;
            foreach (var building in map.BuildingSpawns) if (building.OwnerId != 0) building.OwnerId = 3 - building.OwnerId;
            var units = map.UnitSpawns.OrderBy(s => s.OwnerId == 0 ? 3 : s.OwnerId).ThenBy(s => s.Id).ToArray(); var unitIds = map.UnitSpawns.Select(s => s.Id).OrderBy(id => id).ToArray();
            for (int i = 0; i < units.Length; i++) units[i].Id = unitIds[i];
            map.UnitSpawns = units;
            var buildings = map.BuildingSpawns.OrderBy(s => s.OwnerId == 0 ? 3 : s.OwnerId).ThenBy(s => s.Id).ToArray(); var buildingIds = map.BuildingSpawns.Select(s => s.Id).OrderBy(id => id).ToArray();
            for (int i = 0; i < buildings.Length; i++) buildings[i].Id = buildingIds[i];
            map.BuildingSpawns = buildings;
        });
        var worlds = new[] { Build(false), Build(true) };
        int width = worlds[0].Map.WidthCells * worlds[0].Map.CellSizeMillimetres, height = worlds[0].Map.HeightCells * worlds[0].Map.CellSizeMillimetres;
        SimPoint Turn(SimPoint p, int w) => w == 0 ? p : new SimPoint(width - p.X, height - p.Z);
        var logs = new[] { new List<string>(), new List<string>() };
        var ais = new OfflineAi[2, 2];
        for (int w = 0; w < 2; w++)
            for (int seat = 0; seat < 2; seat++)
            {
                int wi = w; var world = worlds[w]; ais[w, seat] = new OfflineAi(world, seat + 1);
                ais[w, seat].CommandIssued += (command, result) => logs[wi].Add(Describe(world, command, p => Turn(p, wi)) + (result.Accepted ? "" : " REJECTED " + result.Reason));
            }
        for (int t = 0; t < ticks; t++)
        {
            logs[0].Clear(); logs[1].Clear();
            int lead = t % 20 < 10 ? 0 : 1;
            for (int k = 0; k < 2; k++) { int seat = (lead + k) % 2; ais[0, seat].Tick(); ais[1, seat].Tick(); }
            if (!logs[0].SequenceEqual(logs[1]))
            {
                Console.WriteLine($"DIVERGENCE {mapId} {faction} {mode}: AI commands differ at tick {t} ({t / 1200}:{t / 20 % 60:00})");
                Console.WriteLine("  authored: " + string.Join(" | ", logs[0])); Console.WriteLine("  turned:   " + string.Join(" | ", logs[1]));
                Results.Add(new { Probe = "divergence", Map = mapId, Faction = faction, Mode = mode.ToString(), Tick = t, Kind = "commands", Authored = logs[0].ToArray(), Turned = logs[1].ToArray() });
                return;
            }
            worlds[0].Tick(); worlds[1].Tick();
            var states = new[] { new List<string>(), new List<string>() };
            for (int w = 0; w < 2; w++)
            {
                foreach (var u in worlds[w].Units) { var p = Turn(u.Position, w); states[w].Add($"{u.Id} U{u.OwnerId} {u.DefinitionId} @{p.X},{p.Z} {u.Order} {u.WorkerTask} hp{u.Health} carry{u.CarriedAmount}"); }
                foreach (var b in worlds[w].Buildings) { var p = Turn(b.Position, w); states[w].Add($"{b.Id} B{b.OwnerId} {b.DefinitionId} @{p.X},{p.Z} hp{b.Health} q{b.ProductionQueue.Count}"); }
                states[w].Sort(StringComparer.Ordinal);
            }
            var onlyAuthored = states[0].Except(states[1]).Take(6).ToArray(); var onlyTurned = states[1].Except(states[0]).Take(6).ToArray();
            if (onlyAuthored.Length > 0 || onlyTurned.Length > 0)
            {
                Console.WriteLine($"DIVERGENCE {mapId} {faction} {mode}: entities differ after tick {t} ({t / 1200}:{t / 20 % 60:00})");
                foreach (var line in onlyAuthored) Console.WriteLine("  authored: " + line);
                foreach (var line in onlyTurned) Console.WriteLine("  turned:   " + line);
                Results.Add(new { Probe = "divergence", Map = mapId, Faction = faction, Mode = mode.ToString(), Tick = t, Kind = "entities", Authored = onlyAuthored, Turned = onlyTurned });
                return;
            }
        }
        Console.WriteLine($"DIVERGENCE {mapId} {faction} {mode}: none in {ticks} ticks");
        Results.Add(new { Probe = "divergence", Map = mapId, Faction = faction, Mode = mode.ToString(), Tick = -1, Kind = "none" });
    }

    // One AI command in words, with positions turned into the authored frame and resources named
    // by position, since resource ids are not renumbered.
    private static string Describe(World world, IGameCommand command, Func<SimPoint, SimPoint> turn)
    {
        string Resource(int id) { foreach (var r in world.Resources) if (r.Id == id) { var p = turn(r.Position); return $"res@{p.X},{p.Z}"; } return "res#" + id; }
        string Point(SimPoint p) { var q = turn(p); return $"@{q.X},{q.Z}"; }
        switch (command)
        {
            case MoveCommand move: return $"P{move.PlayerId} Move[{string.Join(",", move.UnitIds)}]{Point(move.Destination)}";
            case GatherCommand gather: return $"P{gather.PlayerId} Gather[{string.Join(",", gather.WorkerIds)}]{Resource(gather.ResourceId)}";
            case BuildCommand build: return $"P{build.PlayerId} Build[{string.Join(",", build.WorkerIds)}]{build.BuildingDefinitionId}{Point(build.Position)}";
            case ConstructCommand construct: return $"P{construct.PlayerId} Construct[{string.Join(",", construct.WorkerIds)}]";
            case TrainCommand train: return $"P{train.PlayerId} Train#{train.BuildingId} {train.UnitDefinitionId}";
            case ResearchCommand research: return $"P{research.PlayerId} Research#{research.BuildingId}";
            case AttackCommand attack: return $"P{attack.PlayerId} Attack[{string.Join(",", attack.UnitIds)}]#{attack.TargetEntityId}";
            case ReturnCargoCommand cargo: return $"P{cargo.PlayerId} Return[{string.Join(",", cargo.WorkerIds)}]";
            default: return command.GetType().Name + " P" + command.PlayerId;
        }
    }

    // Each side's starting workers become identical soldiers and set off at the same tick for mirrored
    // beacons: the central one, and the one beyond the centre. The maps are exact 180-degree mirrors,
    // so arrival times that differ by side point to movement or pathfinding that is not.
    private static void Race()
    {
        var rules = Rules();
        foreach (string mapId in new[] { "amber_crossing", "sapphire_coast", "sunscar_basin" })
        foreach (string unitId in new[] { "reedguard", "strider" })
        foreach (bool far in new[] { false, true })
        {
            // The same faction on both sides, so no speed passive separates them.
            var map = Map(mapId); map.RealmId = "historical";
            map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "aven" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "aven" } };
            var objectives = map.OfflineMatch.Objectives; map.OfflineMatch = null;
            foreach (var spawn in map.UnitSpawns) spawn.DefinitionId = unitId;
            var world = new World(rules, map);
            var goal = new Dictionary<int, SimPoint>();
            foreach (int owner in new[] { 1, 2 })
            {
                var hearth = map.BuildingSpawns.First(b => b.OwnerId == owner).Position;
                var ordered = objectives.OrderBy(o => SimPoint.DistanceCeiling(o.Position, hearth)).ToArray();
                goal[owner] = far ? ordered[2].Position : ordered[1].Position;
                world.Submit(new MoveCommand(owner, world.Units.Where(u => u.OwnerId == owner).Select(u => u.Id).ToArray(), goal[owner]));
            }
            var arrived = new Dictionary<int, long> { { 1, -1 }, { 2, -1 } };
            for (int t = 1; t <= 2400 && (arrived[1] < 0 || arrived[2] < 0); t++)
            {
                world.Tick();
                foreach (int owner in new[] { 1, 2 })
                    if (arrived[owner] < 0 && world.Units.Count(u => u.OwnerId == owner && SimPoint.DistanceCeiling(u.Position, goal[owner]) <= 4500) >= 3) arrived[owner] = t;
            }
            Results.Add(new { Probe = "race", Map = mapId, Unit = unitId, Target = far ? "far" : "central", FirstBaseTicks = arrived[1], SecondBaseTicks = arrived[2] });
            Console.WriteLine($"RACE {mapId,-15} {unitId,-10} {(far ? "far beacon" : "central"),-11}: first base {arrived[1]} ticks, second base {arrived[2]} ticks");
        }
    }

    // Identical armies placed as mirror images. Swapping which side spawns first (lower ids)
    // separates an owner advantage from an entity-order advantage in combat resolution.
    private static void Mirror()
    {
        var rules = Rules(); rules.BasePopulationCapacity = 200;
        foreach (string unitId in new[] { "reedguard", "stringwarden", "strider", "boarding_raider" })
            foreach (bool secondFirst in new[] { false, true })
            {
                var spawns = new List<UnitSpawnDefinition>(); int id = 1;
                void Side(int owner, int x, int step) { for (int i = 0; i < 9; i++) spawns.Add(U(id++, owner, unitId, new SimPoint(x + step * (i % 3) * 1000, 14000 + (i / 3) * 1200 + (i % 3) * 400))); }
                if (secondFirst) { Side(2, 36000, -1); Side(1, 20000, 1); } else { Side(1, 20000, 1); Side(2, 36000, -1); }
                var world = new World(rules, new MapDefinition { RealmId = "historical", WidthCells = 56, HeightCells = 32, UnitSpawns = spawns.ToArray() });
                for (int t = 0; t < 1800 && world.Units.Any(u => u.OwnerId == 1) && world.Units.Any(u => u.OwnerId == 2); t++)
                {
                    if (t % 20 == 0) foreach (int owner in new[] { 1, 2 })
                        foreach (var unit in world.Units.Where(u => u.OwnerId == owner && u.AttackTargetId == 0).ToList())
                        {
                            var enemy = world.Units.Where(e => e.OwnerId != owner).OrderBy(e => SimPoint.DistanceCeiling(e.Position, unit.Position)).FirstOrDefault();
                            if (enemy != null) world.Submit(new AttackCommand(owner, new[] { unit.Id }, enemy.Id));
                        }
                    world.Tick();
                }
                var first = world.Units.Where(u => u.OwnerId == 1).ToList(); var second = world.Units.Where(u => u.OwnerId == 2).ToList();
                string winner = first.Count > 0 && second.Count == 0 ? "P1" : second.Count > 0 && first.Count == 0 ? "P2" : "empate";
                Results.Add(new { Probe = "mirror", Unit = unitId, SecondSpawnedFirst = secondFirst, Winner = winner, P1 = first.Count, P1Health = first.Sum(u => u.Health), P2 = second.Count, P2Health = second.Sum(u => u.Health) });
                Console.WriteLine($"MIRROR 9v9 {unitId,-16} {(secondFirst ? "P2 spawned first" : "P1 spawned first")}: {winner} P1 {first.Count} ({first.Sum(u => u.Health)} hp) P2 {second.Count} ({second.Sum(u => u.Health)} hp)");
            }
    }

    // ------------------------------------------------------------------ full match

    private static void Match(string mapId, string faction, VictoryMode mode, int seconds, bool verbose)
    {
        var world = OfflineWorld(Rules(), mapId, faction, mode);
        var commander = new ScriptedCommander(world, 1);
        if (int.TryParse(Argument(Environment.GetCommandLineArgs(), "--wave", ""), out int wave)) commander.FirstWaveSize = wave;
        if (int.TryParse(Argument(Environment.GetCommandLineArgs(), "--earliest", ""), out int earliest)) commander.EarliestAttackTick = earliest * 20L;
        var difficulty = (OfflineAiDifficulty)Enum.Parse(typeof(OfflineAiDifficulty), Argument(Environment.GetCommandLineArgs(), "--difficulty", "Normal"), true);
        var ai = new OfflineAi(world, 2, difficulty);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long deadline = seconds * 20L;
        while (!world.Match.IsFinished && world.TickIndex < deadline)
        {
            // The same order as a real match: the local player's orders, then the rival AI, then the tick.
            commander.Tick(); ai.Tick(); world.Tick();
            if (verbose && world.TickIndex % 1200 == 0)
            {
                world.TryGetPlayer(1, out var p1); world.TryGetPlayer(2, out var p2);
                Console.WriteLine($"  {world.TickIndex / 1200:00}:00 me workers={commander.WorkerCount} army={commander.ArmyCount} pop={p1.PopulationUsed}/{p1.PopulationCapacity} era={p1.EraTier} stock={p1.Resources.Food}/{p1.Resources.Wood}/{p1.Resources.Metal}/{p1.Resources.Stone} | ai workers={ai.WorkerCount} army={ai.ArmyCount} era={p2.EraTier} status={ai.Status} | {commander.Plan} | lost={commander.Losses} rej={commander.Rejected} last={commander.LastRejection}");
            }
        }
        world.TryGetPlayer(1, out var me); world.TryGetPlayer(2, out var rival);
        var timeline = commander.Timeline.Select(e => $"{e.Tick / 1200:00}:{e.Tick / 20 % 60:00} {e.Text}").ToArray();
        Results.Add(new { Probe = "full-match", Map = mapId, Faction = faction, Rival = rival.FactionId, Mode = mode.ToString(), Finished = world.Match.IsFinished, Winner = world.Match.WinnerId,
            Reason = world.Match.Reason.ToString(), Seconds = world.TickIndex / 20d, Deaths = world.DeathCount, CommanderAccepted = commander.Accepted, CommanderRejected = commander.Rejected,
            MyEra = me.EraTier, RivalEra = rival.EraTier, MyUnits = world.Units.Count(u => u.OwnerId == 1), RivalUnits = world.Units.Count(u => u.OwnerId == 2),
            RivalStats = ai.Statistics, WallSeconds = watch.Elapsed.TotalSeconds, Timeline = timeline });
        Console.WriteLine($"MATCH {mapId} {faction} vs {rival.FactionId} ({ai.Difficulty}) {mode} wave={commander.FirstWaveSize} earliest={commander.EarliestAttackTick / 20}s: finished={world.Match.IsFinished} winner={world.Match.WinnerId} reason={world.Match.Reason} at {world.TickIndex / 1200}:{world.TickIndex / 20 % 60:00} waves={commander.Waves} myLosses={commander.Losses} deaths={world.DeathCount} aiArmyNow={ai.ArmyCount} (commands {commander.Accepted}/{commander.Accepted + commander.Rejected}, wall {watch.Elapsed.TotalSeconds:0.0}s)");
        if (verbose) foreach (var line in timeline) Console.WriteLine("    " + line);
    }
}
