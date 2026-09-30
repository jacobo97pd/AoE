using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emberfield.Simulation;

// Simulation tick harness: runs fixed, fully scripted workloads (natural AI matches, a heavy
// field battle, mass move orders and a siege) on the Unity-free simulation, hashes the complete
// simulation state every N ticks and times every World.Tick, AI think and scripted order.
// Two builds of the simulation that produce the same hashes behave identically tick for tick.
//   ./tools/Verify-SimulationTicks.ps1 --scenario all --json TestResults/SimulationTicks/a.json
//   ./tools/Verify-SimulationTicks.ps1 --compare TestResults/SimulationTicks/a.json TestResults/SimulationTicks/b.json
internal static class SimulationTicks
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
    private static GameDefinition Rules() => JsonSerializer.Deserialize<GameDefinition>(File.ReadAllBytes("Assets/Game/Resources/Definitions/greybox.json"), Json);
    private static MapDefinition Map(string id) => JsonSerializer.Deserialize<MapDefinition>(File.ReadAllBytes("Assets/Game/Resources/Maps/" + id + ".json"), Json);
    private static string Argument(string[] args, string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
    private static readonly string[] AllScenarios = { "match-amber", "match-legend", "match-sunscar", "battle", "mass-order", "siege" };

    private static int Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        // Fewer preemptions by the rest of the desktop: timings are compared between separate runs.
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch (Exception) { }
        int compare = Array.IndexOf(args, "--compare");
        if (compare >= 0 && compare + 2 < args.Length) return Compare(args[compare + 1], args[compare + 2]);
        string output = Argument(args, "--json", "TestResults/SimulationTicks/results.json");
        string requested = Argument(args, "--scenario", "all");
        int hashEvery = int.Parse(Argument(args, "--hash-every", "100"));
        int repeat = int.Parse(Argument(args, "--repeat", "1"));
        var names = requested == "all" ? AllScenarios : requested.Split(',');
        var results = new List<ScenarioResult>();
        // A first, unrecorded pass compiles every method a scenario reaches, so no timed tick pays JIT.
        foreach (string name in names) Run(name, int.MaxValue);
        for (int r = 0; r < repeat; r++)
            foreach (string name in names)
            {
                var result = Run(name, hashEvery);
                results.Add(result);
                Console.WriteLine($"{name,-14} ticks={result.Ticks,6} final={result.FinalHash.Substring(0, 16)} world p50={result.World.P50:F3} p95={result.World.P95:F3} p99={result.World.P99:F3} max={result.World.Max:F3} ms " +
                    $"alloc/tick={result.WorldAllocatedBytes / Math.Max(1, result.Ticks)} B allocTicks={result.WorldAllocatingTicks} ai p99={result.Ai.P99:F3} max={result.Ai.Max:F3} orders p99={result.Orders.P99:F3} max={result.Orders.Max:F3} " +
                    $"end={result.EndReason} deaths={result.Deaths} peak units={result.PeakUnits} moving={result.PeakMoving} projectiles={result.PeakProjectiles} orders={result.OrdersAccepted}/{result.OrdersIssued}");
            }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, JsonSerializer.Serialize(results, Json));
        Console.WriteLine("SIMULATION_TICKS_DONE " + results.Count + " -> " + output);
        return 0;
    }

    // Exit code 1 unless every run of every scenario in both files hashed identically at every checkpoint.
    // Timings are the median over a file's runs of each scenario, so interleaved repeats can be merged.
    private static int Compare(string before, string after)
    {
        var a = JsonSerializer.Deserialize<List<ScenarioResult>>(File.ReadAllText(before), Json).GroupBy(r => r.Scenario).ToDictionary(g => g.Key, g => g.ToList());
        var b = JsonSerializer.Deserialize<List<ScenarioResult>>(File.ReadAllText(after), Json).GroupBy(r => r.Scenario).ToDictionary(g => g.Key, g => g.ToList());
        bool identical = true;
        double Median(List<ScenarioResult> runs, Func<ScenarioResult, double> value) { var v = runs.Select(value).OrderBy(x => x).ToList(); return v[(v.Count - 1) / 2]; }
        foreach (var pair in b)
        {
            if (!a.TryGetValue(pair.Key, out var first)) { Console.WriteLine(pair.Key + ": missing from " + before); identical = false; continue; }
            var reference = first[0].Hashes;
            string diverged = null;
            foreach (var run in first.Concat(pair.Value))
                for (int i = 0; i < Math.Max(reference.Count, run.Hashes.Count) && diverged == null; i++)
                    if (i >= reference.Count || i >= run.Hashes.Count || reference[i] != run.Hashes[i]) diverged = i < reference.Count ? reference[i].Split(':')[0] : "end";
            identical &= diverged == null;
            string Row(string label, Func<ScenarioResult, double> value) => $"{label} {Median(first, value):F3}->{Median(pair.Value, value):F3}";
            Console.WriteLine($"{pair.Key,-14} {(diverged == null ? "IDENTICAL" : "DIVERGES at tick " + diverged)} ({reference.Count} checkpoints) world ms: " +
                string.Join(" ", Row("p50", r => r.World.P50), Row("p95", r => r.World.P95), Row("p99", r => r.World.P99), Row("p99.9", r => r.World.P999), Row("max", r => r.World.Max), Row("mean", r => r.World.Mean)) +
                $" | B/tick {Median(first, r => r.WorldAllocatedBytes / (double)r.Ticks):F0}->{Median(pair.Value, r => r.WorldAllocatedBytes / (double)r.Ticks):F0}" +
                " | " + Row("ai p99", r => r.Ai.P99) + " " + Row("orders max", r => r.Orders.Max));
        }
        Console.WriteLine(identical ? "SIMULATION_TICKS_IDENTICAL" : "SIMULATION_TICKS_DIVERGED");
        return identical ? 0 : 1;
    }

    internal sealed class Distribution
    {
        public double P50, P95, P99, P999, Max, Mean, Total; public int Count;
        internal static Distribution From(List<double> values)
        {
            var d = new Distribution { Count = values.Count };
            if (values.Count == 0) return d;
            var sorted = values.OrderBy(v => v).ToArray();
            double At(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1)];
            d.P50 = At(.5); d.P95 = At(.95); d.P99 = At(.99); d.P999 = At(.999); d.Max = sorted[sorted.Length - 1];
            d.Total = sorted.Sum(); d.Mean = d.Total / sorted.Length; return d;
        }
    }
    internal sealed class Slow { public long Tick; public double Milliseconds; public long AllocatedBytes; public int Units, Moving, Projectiles; }
    internal sealed class ScenarioResult
    {
        public string Scenario, FinalHash, EndReason; public long Ticks; public int PeakUnits, PeakMoving, PeakProjectiles, Deaths;
        public Distribution World, Ai, Orders; public long WorldAllocatedBytes, AiAllocatedBytes, OrderAllocatedBytes; public int WorldAllocatingTicks;
        public int OrdersIssued, OrdersAccepted; public List<string> Hashes = new List<string>(); public List<Slow> SlowestTicks;
    }

    // ------------------------------------------------------------------ driver

    private sealed class Scenario
    {
        internal World World; internal long Ticks; internal OfflineAi[] Ai = Array.Empty<OfflineAi>();
        internal Func<World, long, IEnumerable<IGameCommand>> Orders;
    }

    private static ScenarioResult Run(string name, int hashEvery)
    {
        var scenario = Create(name);
        var world = scenario.World;
        var result = new ScenarioResult { Scenario = name };
        var worldTimes = new List<double>(); var aiTimes = new List<double>(); var orderTimes = new List<double>();
        var slow = new List<Slow>();
        var trace = new StringBuilder();
        foreach (var ai in scenario.Ai) ai.CommandIssued += (command, response) => trace.Append(world.TickIndex).Append(command.GetType().Name).Append((int)response.Reason).Append(response.EntityId).Append(';');
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        double frequency = Stopwatch.Frequency / 1000.0;
        while (world.TickIndex < scenario.Ticks && (world.Match == null || !world.Match.IsFinished))
        {
            if (hashEvery != int.MaxValue && world.TickIndex % hashEvery == 0) result.Hashes.Add(world.TickIndex + ":" + StateHash(world, trace));
            if (scenario.Orders != null)
            {
                var orders = scenario.Orders(world, world.TickIndex).ToList();
                if (orders.Count > 0)
                {
                    long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                    var responses = new CommandResult[orders.Count];
                    for (int i = 0; i < orders.Count; i++) responses[i] = world.Submit(orders[i]);
                    orderTimes.Add((Stopwatch.GetTimestamp() - started) / frequency);
                    result.OrderAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
                    for (int i = 0; i < orders.Count; i++)
                    {
                        result.OrdersIssued++; if (responses[i].Accepted) result.OrdersAccepted++;
                        trace.Append(world.TickIndex).Append(orders[i].GetType().Name).Append((int)responses[i].Reason).Append(responses[i].EntityId).Append(';');
                    }
                }
            }
            if (scenario.Ai.Length > 0)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                // The offline render probe's alternating order, so neither seat always thinks first.
                if (world.TickIndex % 20 < 10) foreach (var ai in scenario.Ai) ai.Tick();
                else for (int i = scenario.Ai.Length - 1; i >= 0; i--) scenario.Ai[i].Tick();
                aiTimes.Add((Stopwatch.GetTimestamp() - started) / frequency);
                result.AiAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
            int moving = 0; foreach (var unit in world.Units) if (unit.Order == UnitOrder.Moving) moving++;
            result.PeakUnits = Math.Max(result.PeakUnits, world.Units.Count); result.PeakMoving = Math.Max(result.PeakMoving, moving);
            result.PeakProjectiles = Math.Max(result.PeakProjectiles, world.Projectiles.Count);
            long tick = world.TickIndex, before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            world.Tick();
            double elapsed = (Stopwatch.GetTimestamp() - start) / frequency;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            worldTimes.Add(elapsed); result.WorldAllocatedBytes += bytes; if (bytes > 0) result.WorldAllocatingTicks++;
            slow.Add(new Slow { Tick = tick, Milliseconds = elapsed, AllocatedBytes = bytes, Units = world.Units.Count, Moving = moving, Projectiles = world.Projectiles.Count });
            if (slow.Count > 64) { slow.Sort((a, b) => b.Milliseconds.CompareTo(a.Milliseconds)); slow.RemoveRange(16, slow.Count - 16); }
        }
        result.Ticks = world.TickIndex; result.Deaths = world.DeathCount;
        result.EndReason = world.Match == null ? "Drill" : world.Match.IsFinished ? world.Match.Reason + ":" + world.Match.WinnerId : "Running";
        result.FinalHash = StateHash(world, trace); result.Hashes.Add(world.TickIndex + ":" + result.FinalHash);
        result.World = Distribution.From(worldTimes); result.Ai = Distribution.From(aiTimes); result.Orders = Distribution.From(orderTimes);
        slow.Sort((a, b) => b.Milliseconds.CompareTo(a.Milliseconds)); result.SlowestTicks = slow.Take(10).ToList();
        return result;
    }

    private static Scenario Create(string name)
    {
        switch (name)
        {
            case "match-amber": return Natural("amber_crossing", "aven", "serevin", VictoryMode.Dominion, 14400);
            case "match-legend": return Natural("legend_lands", "verdant", "ashen", VictoryMode.Conquest, 14400);
            case "match-sunscar": return Natural("sunscar_basin", "serevin", "aven", VictoryMode.Conquest, 14400);
            case "battle": return Battle();
            case "mass-order": return MassOrder();
            case "siege": return Siege();
            // Not in "all": the naval realm's own match, pirate fleets on the ringed sea. Run it by name.
            case "match-coast": return Natural("sapphire_coast", "pirates", "pirates", VictoryMode.Conquest, 14400);
        }
        throw new ArgumentException("Unknown scenario " + name);
    }

    // Two ordinary Hard AIs play a shipped map from a fresh start: economy, construction, research,
    // faction actions, pathfinding, fog and combat as a player meets them.
    private static Scenario Natural(string mapId, string faction, string opponent, VictoryMode mode, long ticks)
    {
        string realm = ContentRealms.RealmForFaction(faction);
        var map = Map(ContentRealms.MapResourceId(mapId, realm)); map.RealmId = realm; map.OfflineMatch.Mode = mode;
        map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = faction }, new PlayerFactionDefinition { PlayerId = 2, FactionId = opponent } };
        // The pirates start with their own workers, as a shipped match does.
        ContentRealms.PrepareStartingUnits(map);
        var world = new World(Rules(), map);
        return new Scenario { World = world, Ticks = ticks, Ai = new[] { new OfflineAi(world, 1, OfflineAiDifficulty.Hard), new OfflineAi(world, 2, OfflineAiDifficulty.Hard) } };
    }

    // ------------------------------------------------------------------ synthetic maps

    private static GameDefinition shipped;
    private static GameDefinition Shipped => shipped ?? (shipped = Rules());
    private static int Width(string building) => Shipped.Buildings.First(b => b.Id == building).WidthCells;
    private static int Depth(string building) => Shipped.Buildings.First(b => b.Id == building).DepthCells;
    // Centre of a footprint whose lower-left cell is (x, z).
    private static BuildingSpawnDefinition B(int id, int owner, string definition, int x, int z) => new BuildingSpawnDefinition
    { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x * 1000 + Width(definition) * 500, z * 1000 + Depth(definition) * 500) };
    private static UnitSpawnDefinition U(int id, int owner, string definition, int x, int z) => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };

    private static MapDefinition Field(string id, int width, int height, List<UnitSpawnDefinition> units, List<BuildingSpawnDefinition> buildings, List<GridCell> blocked)
    {
        var resources = new List<ResourceSpawnDefinition>();
        return new MapDefinition
        {
            Id = id, WidthCells = width, HeightCells = height, CellSizeMillimetres = 1000, RealmId = ContentRealms.Historical,
            UnitSpawns = units.ToArray(), BuildingSpawns = buildings.ToArray(), ResourceSpawns = resources.ToArray(), BlockedCells = blocked.ToArray(),
            PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "aven" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "serevin" } },
            OfflineMatch = new OfflineMatchDefinition { Enabled = true, Mode = VictoryMode.Conquest }
        };
    }
    private static GameDefinition Wealthy() { var rules = Rules(); rules.StartingResources = new ResourceAmount(20000, 20000, 20000, 20000); rules.BasePopulationCapacity = 1000; return rules; }

    // A block of units laid out on a grid from (x0, z0) in millimetres, one metre apart.
    private static List<int> Block(List<UnitSpawnDefinition> units, ref int nextId, int owner, string definition, int count, int x0, int z0, int columns)
    {
        var ids = new List<int>();
        for (int i = 0; i < count; i++) { units.Add(U(nextId, owner, definition, x0 + i % columns * 1000, z0 + i / columns * 1000)); ids.Add(nextId++); }
        return ids;
    }

    private static IEnumerable<int[]> Chunks(IEnumerable<int> ids, int size)
    {
        var list = ids.ToList();
        for (int i = 0; i < list.Count; i += size) yield return list.Skip(i).Take(size).ToArray();
    }

    // Idle armed units without a target, grouped and sent at the nearest visible enemy, else towards a point.
    private static IEnumerable<IGameCommand> Engage(World world, int owner, SimPoint fallback, int squad, bool buildings)
    {
        var idle = world.Units.Where(u => u.OwnerId == owner && u.AttackDamage > 0 && u.Order == UnitOrder.Idle && u.AttackTargetId == 0 && u.WallId == 0 && u.BoardingWallId == 0 && !u.IsWorker).Select(u => u.Id).ToList();
        foreach (var chunk in Chunks(idle, squad))
        {
            world.TryGetUnit(chunk[0], out var lead);
            int target = 0; long best = long.MaxValue;
            foreach (var enemy in world.Units)
            {
                if (enemy.OwnerId == owner || !world.Vision.IsEntityVisible(owner, enemy.Id)) continue;
                long d = Squared(lead.Position, enemy.Position); if (d < best) { best = d; target = enemy.Id; }
            }
            if (buildings)
                foreach (var enemy in world.Buildings)
                {
                    if (enemy.OwnerId == owner || !world.Vision.IsEntityVisible(owner, enemy.Id)) continue;
                    long d = Squared(lead.Position, enemy.Position); if (d < best) { best = d; target = enemy.Id; }
                }
            if (target != 0) yield return new AttackCommand(owner, chunk, target);
            else yield return new MoveCommand(owner, chunk, fallback, MovementFormation.Loose);
        }
    }
    private static long Squared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }

    // Two 150-strong mixed armies meet in an open field with rock outcrops and two watchtowers a side,
    // fed by three muster halls each; idle squads are sent at the nearest visible enemy.
    private static Scenario Battle()
    {
        var units = new List<UnitSpawnDefinition>(); var buildings = new List<BuildingSpawnDefinition>(); var blocked = new List<GridCell>();
        int next = 1;
        var redA = Block(units, ref next, 1, "reedguard", 60, 18500, 30500, 6);
        var redB = Block(units, ref next, 1, "stringwarden", 50, 11500, 32500, 5);
        var redC = Block(units, ref next, 1, "strider", 40, 25500, 34500, 4);
        var blueA = Block(units, ref next, 2, "reedguard", 60, 104500, 30500, 6);
        var blueB = Block(units, ref next, 2, "stringwarden", 40, 111500, 32500, 5);
        var blueC = Block(units, ref next, 2, "ashrunner", 30, 98500, 34500, 3);
        var blueD = Block(units, ref next, 2, "strider", 20, 116500, 34500, 2);
        buildings.Add(B(9001, 1, "hearth", 2, 4)); buildings.Add(B(9002, 2, "hearth", 122, 4));
        buildings.Add(B(9003, 1, "watchtower", 50, 40)); buildings.Add(B(9004, 2, "watchtower", 76, 40));
        buildings.Add(B(9005, 1, "watchtower", 50, 58)); buildings.Add(B(9006, 2, "watchtower", 76, 58));
        for (int i = 0; i < 3; i++) { buildings.Add(B(9011 + i, 1, "muster_hall", 6, 24 + i * 16)); buildings.Add(B(9021 + i, 2, "muster_hall", 119, 24 + i * 16)); }
        for (int i = 0; i < 6; i++) for (int dz = 0; dz < 3; dz++) for (int dx = 0; dx < 2; dx++) blocked.Add(new GridCell(56 + (i % 3) * 7 + dx, 30 + (i / 3) * 24 + dz * 3));
        var world = new World(Wealthy(), Field("battle", 128, 96, units, buildings, blocked));
        var red = redA.Concat(redB).Concat(redC).ToList(); var blue = blueA.Concat(blueB).Concat(blueC).Concat(blueD).ToList();
        IEnumerable<IGameCommand> Orders(World w, long tick)
        {
            if (tick == 0)
            {
                int i = 0;
                foreach (var chunk in Chunks(red, 30)) yield return new MoveCommand(1, chunk, new SimPoint(58000, 38000 + i++ * 7000), (MovementFormation)(i % 3));
                i = 0;
                foreach (var chunk in Chunks(blue, 30)) yield return new MoveCommand(2, chunk, new SimPoint(70000, 38000 + i++ * 7000), (MovementFormation)(i % 3));
                for (int h = 0; h < 3; h++) { yield return new SetRallyCommand(1, 9011 + h, new SimPoint(40500, 32500 + h * 8000)); yield return new SetRallyCommand(2, 9021 + h, new SimPoint(88500, 32500 + h * 8000)); }
                yield break;
            }
            // Three muster halls a side keep reinforcements walking to the front.
            string[] recruits = { "reedguard", "stringwarden", "strider" };
            if (tick % 40 == 10) for (int h = 0; h < 3; h++) { yield return new TrainCommand(1, 9011 + h, recruits[(tick / 40 + h) % 3]); yield return new TrainCommand(2, 9021 + h, recruits[(tick / 40 + h + 1) % 3]); }
            if (tick % 40 == 0) foreach (var order in Engage(w, 1, new SimPoint(90000, 48000), 12, false)) yield return order;
            if (tick % 40 == 20) foreach (var order in Engage(w, 2, new SimPoint(38000, 48000), 12, false)) yield return order;
        }
        return new Scenario { World = world, Ticks = 6000, Orders = Orders };
    }

    // Two 240-strong armies behind a solid ridge take whole-army and split move orders every
    // ten seconds while 40 workers each keep gathering: movement, formations and group routes.
    private static Scenario MassOrder()
    {
        var units = new List<UnitSpawnDefinition>(); var buildings = new List<BuildingSpawnDefinition>(); var blocked = new List<GridCell>();
        var resources = new List<ResourceSpawnDefinition>();
        int next = 1;
        var red = Block(units, ref next, 1, "reedguard", 120, 20500, 10500, 12).Concat(Block(units, ref next, 1, "strider", 60, 20500, 21500, 12)).Concat(Block(units, ref next, 1, "stringwarden", 60, 20500, 27500, 12)).ToList();
        var redWorkers = Block(units, ref next, 1, "tender", 40, 60500, 10500, 10);
        var blue = Block(units, ref next, 2, "reedguard", 120, 20500, 120500, 12).Concat(Block(units, ref next, 2, "strider", 60, 20500, 131500, 12)).Concat(Block(units, ref next, 2, "stringwarden", 60, 20500, 137500, 12)).ToList();
        var blueWorkers = Block(units, ref next, 2, "tender", 40, 60500, 140500, 10);
        buildings.Add(B(9001, 1, "hearth", 2, 2)); buildings.Add(B(9002, 2, "hearth", 2, 150));
        buildings.Add(B(9003, 1, "storeyard", 66, 20)); buildings.Add(B(9004, 2, "storeyard", 66, 132));
        string[] kinds = { "food_source", "wood_source", "metal_source", "stone_source" };
        for (int i = 0; i < 16; i++)
        {
            resources.Add(new ResourceSpawnDefinition { Id = 8001 + i, DefinitionId = kinds[i % 4], Position = new SimPoint(72500 + i % 8 * 2000, 24500 + i / 8 * 2000) });
            resources.Add(new ResourceSpawnDefinition { Id = 8101 + i, DefinitionId = kinds[i % 4], Position = new SimPoint(72500 + i % 8 * 2000, 128500 - i / 8 * 2000) });
        }
        for (int z = 70; z <= 86; z++) for (int x = 0; x < 144; x++) blocked.Add(new GridCell(x, z));
        for (int i = 0; i < 12; i++) for (int dz = 0; dz < 4; dz++) blocked.Add(new GridCell(30 + i * 8, 40 + (i % 3) * 6 + dz));
        for (int i = 0; i < 12; i++) for (int dz = 0; dz < 4; dz++) blocked.Add(new GridCell(30 + i * 8, 104 + (i % 3) * 6 + dz));
        var map = Field("mass-order", 144, 156, units, buildings, blocked); map.ResourceSpawns = resources.ToArray();
        var world = new World(Wealthy(), map);
        SimPoint[] redPoints = { new SimPoint(120500, 50500), new SimPoint(20500, 50500), new SimPoint(120500, 12500), new SimPoint(70500, 60500) };
        SimPoint[] bluePoints = { new SimPoint(120500, 100500), new SimPoint(20500, 100500), new SimPoint(120500, 140500), new SimPoint(70500, 94500) };
        IEnumerable<IGameCommand> Orders(World w, long tick)
        {
            if (tick % 600 == 0)
            {
                int i = 0;
                foreach (var chunk in Chunks(redWorkers, 5)) yield return new GatherCommand(1, chunk, 8001 + i++ % 16);
                i = 0;
                foreach (var chunk in Chunks(blueWorkers, 5)) yield return new GatherCommand(2, chunk, 8101 + i++ % 16);
            }
            if (tick % 200 == 0)
            {
                int step = (int)(tick / 200);
                var formation = (MovementFormation)(step % 4);
                if (step % 3 != 2)
                {
                    yield return new MoveCommand(1, Alive(w, red), redPoints[step % 4], formation);
                    yield return new MoveCommand(2, Alive(w, blue), bluePoints[step % 4], formation);
                }
                else
                {
                    int i = 0; foreach (var chunk in Chunks(Alive(w, red), 60)) yield return new MoveCommand(1, chunk, redPoints[(step + i++) % 4], formation);
                    i = 0; foreach (var chunk in Chunks(Alive(w, blue), 60)) yield return new MoveCommand(2, chunk, bluePoints[(step + i++) % 4], formation);
                }
            }
        }
        return new Scenario { World = world, Ticks = 3600, Orders = Orders };
    }
    private static int[] Alive(World world, List<int> ids) => ids.Where(id => world.TryGetUnit(id, out _)).ToArray();

    // An Aven army storms a Serevin wall line: rams on the gate and walls, ladders and boarding
    // parties, archers on the wall deck and watchtowers behind it, a keep and a gate that opens and closes.
    private static Scenario Siege()
    {
        var units = new List<UnitSpawnDefinition>(); var buildings = new List<BuildingSpawnDefinition>(); var blocked = new List<GridCell>();
        int next = 1, wallRow = 60;
        var walls = new List<int>(); int gate = 9500;
        blocked.Add(new GridCell(0, wallRow)); blocked.Add(new GridCell(96, wallRow)); blocked.Add(new GridCell(97, wallRow));
        for (int x = 1; x < 46; x += 3) { buildings.Add(B(9100 + x, 2, "wall", x, wallRow)); walls.Add(9100 + x); }
        buildings.Add(B(gate, 2, "gate", 46, wallRow));
        for (int x = 48; x < 96; x += 3) { buildings.Add(B(9100 + x, 2, "wall", x, wallRow)); walls.Add(9100 + x); }
        foreach (int x in new[] { 18, 38, 56, 76 }) buildings.Add(B(9300 + x, 2, "watchtower", x, 63));
        buildings.Add(B(9400, 2, "keep", 46, 78)); buildings.Add(B(9401, 2, "hearth", 46, 88)); buildings.Add(B(9402, 1, "hearth", 46, 2));
        // Four archers stand behind each of the ten central wall segments, ready to climb.
        var deck = new Dictionary<int, int[]>();
        foreach (int wall in walls.Where(w => Math.Abs(w - 9100 - 46) <= 16))
        {
            var ids = new List<int>(); int centre = (wall - 9100) * 1000 + 1500;
            foreach (int dx in new[] { -1050, -350, 350, 1050 }) { units.Add(U(next, 2, "stringwarden", centre + dx, (wallRow + 1) * 1000 + 800)); ids.Add(next++); }
            deck[wall] = ids.ToArray();
        }
        var guards = Block(units, ref next, 2, "reedguard", 36, 38500, 66500, 12).Concat(Block(units, ref next, 2, "strider", 12, 38500, 70500, 12)).ToList();
        var rams = Block(units, ref next, 1, "siege_ram", 6, 38500, 30500, 6);
        var ladders = Block(units, ref next, 1, "siege_ladder", 4, 30500, 33500, 4);
        var infantry = Block(units, ref next, 1, "reedguard", 60, 30500, 20500, 12);
        var archers = Block(units, ref next, 1, "stringwarden", 40, 50500, 20500, 10);
        var riders = Block(units, ref next, 1, "strider", 20, 62500, 20500, 10);
        var world = new World(Wealthy(), Field("siege", 98, 96, units, buildings, blocked));
        int[] ladderWalls = { walls.First(w => w - 9100 == 37), walls.First(w => w - 9100 == 54) };
        IEnumerable<IGameCommand> Orders(World w, long tick)
        {
            if (tick == 0)
            {
                foreach (var pair in deck) yield return new BoardWallCommand(2, pair.Value, pair.Key);
                yield return new MoveCommand(1, rams.ToArray(), new SimPoint(47000, 52500), MovementFormation.Line);
                yield return new MoveCommand(1, infantry.ToArray(), new SimPoint(44500, 50500), MovementFormation.Box);
                yield return new MoveCommand(1, archers.ToArray(), new SimPoint(47500, 53500), MovementFormation.Line);
                yield return new MoveCommand(1, riders.ToArray(), new SimPoint(60500, 48500), MovementFormation.Loose);
                for (int i = 0; i < ladders.Count; i++)
                    yield return new MoveCommand(1, new[] { ladders[i] }, new SimPoint((ladderWalls[i % 2] - 9100) * 1000 + 500 + (i / 2) * 2000, wallRow * 1000 - 900));
                yield break;
            }
            if (tick == 400)
            {
                yield return new AttackCommand(1, rams.Take(2).ToArray(), gate);
                yield return new AttackCommand(1, rams.Skip(2).Take(2).ToArray(), walls.First(x => x - 9100 == 43));
                yield return new AttackCommand(1, rams.Skip(4).ToArray(), walls.First(x => x - 9100 == 48));
            }
            if (tick >= 400 && tick % 100 == 0)
            {
                // Boarding parties of four climb each laddered wall whenever its deck has room.
                var free = Alive(w, infantry).Where(id => w.TryGetUnit(id, out var u) && u.WallId == 0 && u.BoardingWallId == 0).ToList();
                for (int i = 0; i < ladders.Count && free.Count >= 4; i++)
                {
                    if (!w.TryGetUnit(ladders[i], out var ladder) || ladder.Order != UnitOrder.Idle) continue;
                    var party = free.OrderBy(id => { w.TryGetUnit(id, out var u); return Squared(u.Position, ladder.Position); }).ThenBy(id => id).Take(4).ToArray();
                    foreach (int id in party) free.Remove(id);
                    yield return new MoveCommand(1, party, new SimPoint(ladder.Position.X, ladder.Position.Z - 1200), MovementFormation.Loose);
                    yield return new BoardWallCommand(1, party, ladderWalls[i % 2], ladders[i]);
                }
            }
            if (tick % 300 == 150 && w.TryGetBuilding(gate, out var g)) yield return new SetGateCommand(2, gate, !g.GateOpen);
            if (tick >= 200 && tick % 50 == 0) foreach (var order in Engage(w, 1, new SimPoint(47000, 70500), 10, true)) yield return order;
            if (tick >= 200 && tick % 50 == 25) foreach (var order in Engage(w, 2, new SimPoint(47000, 56500), 10, false)) yield return order;
        }
        return new Scenario { World = world, Ticks = 4800, Orders = Orders };
    }

    // ------------------------------------------------------------------ state hash

    // Every field of every entity, player, projectile and objective (public, internal and private,
    // including routes, timers and scratch that later ticks read), the fog masks, the blocked grid and
    // the command trace. Rules data referenced from entities is immutable and hashed by type only.
    internal static string StateHash(World world, StringBuilder trace)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(world.TickIndex); writer.Write(world.DeathCount);
            Value(writer, Field(world, "nextEntityId"), 0);
            foreach (var player in world.Players) Value(writer, player, 0);
            writer.Write(world.Units.Count); foreach (var unit in world.Units) Value(writer, unit, 0);
            writer.Write(world.Buildings.Count); foreach (var building in world.Buildings) Value(writer, building, 0);
            writer.Write(world.Resources.Count); foreach (var resource in world.Resources) Value(writer, resource, 0);
            writer.Write(world.Projectiles.Count); foreach (var projectile in world.Projectiles) Value(writer, projectile, 0);
            if (world.Match != null)
            {
                writer.Write(world.Match.IsFinished); writer.Write(world.Match.WinnerId); writer.Write((int)world.Match.Reason); writer.Write(world.Match.ElapsedTicks);
                foreach (int id in world.Match.PlayerIds) writer.Write(world.Match.GetHoldTicks(id));
                foreach (var objective in world.Match.Objectives) Value(writer, objective, 0);
                var cells = new byte[world.Vision.WidthCells * world.Vision.HeightCells];
                foreach (var player in world.Players) if (world.Vision.CopyCells(player.Id, cells)) writer.Write(cells);
            }
            var blocked = (bool[])Field(world.navigation, "blocked"); foreach (bool cell in blocked) writer.Write(cell);
            Value(writer, Field(world.movement, "nextGroup"), 0);
            writer.Write(trace.ToString());
            writer.Flush();
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "");
        }
    }

    private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);
    private static readonly Dictionary<Type, FieldInfo[]> Fields = new Dictionary<Type, FieldInfo[]>();
    private static FieldInfo[] FieldsOf(Type type)
    {
        if (Fields.TryGetValue(type, out var fields)) return fields;
        fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType) && f.FieldType != typeof(World) && !IsRulesData(f.FieldType))
            .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        Fields.Add(type, fields); return fields;
    }
    // Definitions are loaded once and never mutated by a match.
    private static bool IsRulesData(Type type) => type.Namespace == "Emberfield.Simulation" && type.IsClass && type.Name.EndsWith("Definition", StringComparison.Ordinal);

    private static void Value(BinaryWriter writer, object value, int depth)
    {
        switch (value)
        {
            case null: writer.Write((byte)0xFF); return;
            case int i: writer.Write(i); return;
            case long l: writer.Write(l); return;
            case bool b: writer.Write(b); return;
            case string s: writer.Write(s); return;
            case byte y: writer.Write(y); return;
            case short h: writer.Write(h); return;
        }
        var type = value.GetType();
        if (type.IsEnum) { writer.Write(Convert.ToInt64(value)); return; }
        if (value is IEnumerable sequence)
        {
            var items = sequence.Cast<object>().ToList();
            // Unordered sets hash in a stable order; lists keep their order.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>)) items = items.OrderBy(item => item.ToString(), StringComparer.Ordinal).ToList();
            writer.Write(items.Count); foreach (var item in items) Value(writer, item, depth + 1);
            return;
        }
        if (depth > 6) { writer.Write(type.Name); return; }
        foreach (var field in FieldsOf(type))
        {
            var item = field.GetValue(value);
            // Preserve the committed land-state byte stream, including every OLD zero/null field.
            // Only the explicitly added naval fields may be absent at their default; once set they
            // enter the hash like any other state, so embark/unload changes cannot disappear.
            if (field.DeclaringType == typeof(UnitState) && NavalUnitFields.Contains(field.Name) && IsDefault(item, field.FieldType)) continue;
            writer.Write(field.Name); Value(writer, item, depth + 1);
        }
    }

    // UnitState's additions after 4f89b74. Property storage uses the compiler's backing-field name.
    // Keep this explicit: suppressing defaults for every field would invalidate all older hashes.
    private static readonly HashSet<string> NavalUnitFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "<Domain>k__BackingField", "<CargoCapacity>k__BackingField", "cargo", "<CarrierId>k__BackingField",
        "<EmbarkShipId>k__BackingField", "<EmbarkRemainingTicks>k__BackingField", "<IsUnloading>k__BackingField",
        "<UnloadPoint>k__BackingField", "NavalRouteDestination", "NextNavalRetryTick"
    };

    private static readonly Dictionary<Type, object> Defaults = new Dictionary<Type, object>();
    private static bool IsDefault(object item, Type type)
    {
        if (item == null) return true;
        if (!type.IsValueType) return false;
        if (!Defaults.TryGetValue(type, out var empty)) Defaults.Add(type, empty = Activator.CreateInstance(type));
        return item.Equals(empty);
    }
}
