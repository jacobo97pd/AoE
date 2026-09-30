using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Emberfield.Networking;
using Emberfield.Simulation;

// Private stdin/stdout protocol. Only the HTTP service starts and addresses this worker.
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, MaxDepth = 48 };
    private static readonly Dictionary<string, HostedMatch> Matches = new Dictionary<string, HostedMatch>(StringComparer.Ordinal);
    private static string resources;
    private static int Main(string[] args)
    {
        resources = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("Assets/Game/Resources");
        Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
        var requests = Channel.CreateBounded<string>(new BoundedChannelOptions(256) { SingleReader = true, SingleWriter = true });
        _ = Task.Run(async () => {
            try { string line; while ((line = await Console.In.ReadLineAsync()) != null) { if (line.Length > 65536) break; await requests.Writer.WriteAsync(line); } }
            finally { requests.Writer.TryComplete(); }
        });
        var clock = Stopwatch.StartNew();
        double nextTick = clock.Elapsed.TotalSeconds + World.TickSeconds;
        while (true)
        {
            int processed = 0;
            while (processed++ < 64 && requests.Reader.TryRead(out string line)) Handle(line);
            if (requests.Reader.Completion.IsCompleted) return 0;
            int steps = 0;
            while (clock.Elapsed.TotalSeconds >= nextTick && steps++ < 5)
            {
                foreach (var pair in Matches)
                {
                    try { pair.Value.World.Tick(); PublishResult(pair.Key, pair.Value); }
                    catch { Abort(pair.Key, pair.Value, "authority_failure"); }
                }
                nextTick += World.TickSeconds;
            }
            // Preserve simulation ticks under sustained load; never synthesize gameplay from wall time.
            if (clock.Elapsed.TotalSeconds - nextTick > 1) nextTick = clock.Elapsed.TotalSeconds + World.TickSeconds;
            Thread.Sleep(1);
        }
    }

    private static void Handle(string line)
    {
        long requestId = 0;
        try
        {
            var request = JsonSerializer.Deserialize<Request>(line, Json);
            if (request == null || request.requestId < 1) throw new ArgumentException();
            requestId = request.requestId;
            if (string.IsNullOrEmpty(request.matchId) || request.matchId.Length > 100) throw new ArgumentException();
            if (request.op == "create")
            {
                if (Matches.ContainsKey(request.matchId) || Matches.Count >= 16 || request.players == null || request.players.Length != 2 ||
                    request.players[0].slot != 1 || request.players[1].slot != 2 ||
                    !Enum.TryParse(request.mode, false, out VictoryMode mode) || !Enum.IsDefined(typeof(VictoryMode), mode)) throw new ArgumentException();
                var rules = Read<GameDefinition>("Definitions/greybox.json");
                if (!ContentRealms.IsMapAllowedInRealm(request.mapId, request.realmId)) throw new ArgumentException();
                var map = Read<MapDefinition>("Maps/" + ContentRealms.MapResourceId(request.mapId, request.realmId) + ".json");
                foreach (var player in request.players)
                    if (!ContentRealms.IsPlayableFactionInRealm(player.factionId, request.realmId)) throw new ArgumentException();
                map.RealmId = request.realmId;
                map.PlayerFactions = request.players.Select(p => new PlayerFactionDefinition { PlayerId = p.slot, FactionId = p.factionId }).ToArray();
                ContentRealms.PrepareStartingUnits(map);
                map.OfflineMatch.Mode = mode;
                Matches.Add(request.matchId, new HostedMatch { World = new World(rules, map) });
                Write(new { requestId, ok = true }); return;
            }
            if (!Matches.TryGetValue(request.matchId, out var match)) { Write(new { requestId, ok = false, error = "Unknown match." }); return; }
            if (request.op == "close") { Matches.Remove(request.matchId); Write(new { requestId, ok = true }); return; }
            if (request.playerId != 1 && request.playerId != 2) throw new ArgumentException();
            switch (request.op)
            {
                case "command":
                    if (!NetworkCommandCodec.TryDecode(request.command, request.playerId, match.World.TickIndex, out var command, out var error))
                    { Write(new { requestId, ok = true, accepted = false, error }); return; }
                    if (!NetworkCommandCodec.TryAuthorizeObservation(match.World, command, out error))
                    { Write(new { requestId, ok = true, accepted = false, error }); return; }
                    var result = match.World.Submit(command);
                    Write(new { requestId, ok = true, accepted = result.Accepted, error = result.Accepted ? "" : result.Message, entityId = result.EntityId });
                    PublishResult(request.matchId, match); break;
                case "snapshot":
                    Write(new { requestId, ok = true, observation = NetworkObservation.Export(match.World, request.playerId, ++match.Sequence) }); break;
                case "forfeit":
                    if (request.reason != "disconnect" && request.reason != "afk" && request.reason != "surrender") throw new ArgumentException();
                    if (!match.World.Match.IsFinished) { match.ForfeitReason = request.reason; match.World.Submit(new SurrenderCommand(request.playerId)); }
                    Write(new { requestId, ok = true }); PublishResult(request.matchId, match); break;
                default: throw new ArgumentException();
            }
        }
        catch { Write(new { requestId, ok = false, error = "Invalid authority request." }); }
    }

    private static T Read<T>(string relative) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(resources, relative)), Json);
    private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Json));
    private static void PublishResult(string matchId, HostedMatch match)
    {
        if (!match.World.Match.IsFinished || match.Published) return;
        match.Published = true;
        Write(new { @event = "result", matchId, winnerPlayerId = match.World.Match.WinnerId,
            reason = match.ForfeitReason ?? match.World.Match.Reason.ToString(), tick = match.World.TickIndex,
            durationSeconds = match.World.TickIndex / (double)World.TickRate,
            players = match.World.Players.Select(p => new { playerId = p.Id, statistics = new {
                eraTier = p.EraTier, workersRemaining = match.World.Units.Count(u => u.OwnerId == p.Id && u.IsWorker) + match.World.Units.Sum(u => u.Cargo.Count(c => c.OwnerId == p.Id && c.IsWorker)),
                armyRemaining = match.World.Units.Count(u => u.OwnerId == p.Id && !u.IsWorker && u.AttackDamage > 0) + match.World.Units.Sum(u => u.Cargo.Count(c => c.OwnerId == p.Id && !c.IsWorker && c.AttackDamage > 0)),
                buildingsRemaining = match.World.Buildings.Count(b => b.OwnerId == p.Id),
                food = p.Resources.Food, wood = p.Resources.Wood, metal = p.Resources.Metal, stone = p.Resources.Stone,
                technologiesCompleted = p.CompletedTechnologyIds.Count } }).ToArray() });
    }
    private static void Abort(string matchId, HostedMatch match, string reason)
    {
        if (match.Published) return; match.Published = true;
        Write(new { @event = "result", matchId, winnerPlayerId = 0, reason, tick = match.World.TickIndex,
            durationSeconds = match.World.TickIndex / (double)World.TickRate, players = Array.Empty<object>() });
    }
    private sealed class HostedMatch { public World World; public long Sequence; public bool Published; public string ForfeitReason; }
    private sealed class Request { public long requestId; public string op; public string matchId; public string mode; public string realmId = "historical"; public string mapId = "amber_crossing"; public Seat[] players; public int playerId; public string reason; public NetworkCommandEnvelope command; }
    private sealed class Seat { public int slot; public string factionId; }
}
