using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Emberfield.Presentation;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    [Serializable]
    public sealed class OfflineAiPlayerReport
    {
        public int PlayerId, Units, Workers, Buildings, EraTier, ResearchCount, Commands, Accepted, Rejected;
        public int GatherOrders, BuildingsStarted, UnitsQueued, ResearchStarted, Attacks, Scouts, Retreats;
        public string FactionId, RejectionReasons;
        public ResourceAmount Stock, Spent, Deposited;
        public long PeakMajorityHoldTicks;
    }
    [Serializable]
    public sealed class OfflineAiMatchReport
    {
        public string Mode, LocalFaction, Reason, FinalStateSha256;
        public bool Finished, Passed;
        public int WinnerId, Deaths, ObjectiveOwnershipChanges;
        public long Ticks;
        public double SimulatedSeconds, RunnerWallSeconds;
        public OfflineAiPlayerReport[] Players;
    }
    [Serializable]
    public sealed class OfflineAiRunReport
    {
        public string UnityVersion, RulesSha256, MapSha256, UtcTime;
        public string Method = "Ordinary shipped-data AI versus AI; accelerated fixed ticks; no grants, forced result, hidden observations or activated handicap. These are pacing/progression proxies, not human fun, competitive balance or rendered performance measurements.";
        public int DeadlineSeconds;
        public bool Passed;
        public OfflineAiMatchReport[] Cases;
    }

    public static class OfflineMatchVerification
    {
        /// <summary>Share of a controller's orders that may be refused before the run is treated as broken.
        /// Ordinary play sits near two per cent; this leaves generous room above that.</summary>
        public const int MaximumRejectedPercent = 10;

        [MenuItem("Emberfield/Verify natural offline AI matches")]
        public static void Run()
        {
            int seconds = 1800;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (arguments[index] == "-offlineSeconds" && (!int.TryParse(arguments[index + 1], out seconds) || seconds < 60 || seconds > 7200))
                    throw new ArgumentException("offlineSeconds must be between60 and7200.");
            var report = new OfflineAiRunReport
            {
                UnityVersion = Application.unityVersion, UtcTime = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                RulesSha256 = Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, "Game/Resources/Definitions/greybox.json"))),
                MapSha256 = Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, "Game/Resources/Maps/amber_crossing.json"))),
                DeadlineSeconds = seconds, Passed = true
            };
            var cases = new List<OfflineAiMatchReport>();
            foreach (var mode in new[] { VictoryMode.Conquest, VictoryMode.Dominion })
            foreach (string faction in new[] { "aven", "serevin" })
            {
                UnityEngine.Debug.Log("EMBERFIELD_OFFLINE_AI_START " + mode + " " + faction);
                var world = DefinitionLoader.CreateOfflineWorld(faction, mode);
                var result = RunMatch(world, seconds);
                result.LocalFaction = faction;
                cases.Add(result); report.Passed &= result.Passed;
                report.Cases = cases.ToArray(); Write(report);
                UnityEngine.Debug.Log($"EMBERFIELD_OFFLINE_AI_CASE {mode} {faction} finished={result.Finished} winner={result.WinnerId} seconds={result.SimulatedSeconds} deaths={result.Deaths}");
            }
            Write(report);
            if (!report.Passed) throw new InvalidOperationException($"At least one natural AI match did not finish, or a controller had over {MaximumRejectedPercent}% of its orders refused. Inspect TestResults/offline-matches.json/.md; no result was forced.");
            UnityEngine.Debug.Log("EMBERFIELD_OFFLINE_MATCHES_OK 4 natural matches");
        }

        public static OfflineAiMatchReport RunMatch(World world, int deadlineSeconds)
        {
            if (world == null || world.Match == null || deadlineSeconds < 1) throw new ArgumentException("A real offline world and positive deadline are required.");
            int first = world.Match.PlayerIds[0], second = world.Match.PlayerIds[1];
            var ais = new[] { new OfflineAi(world, first), new OfflineAi(world, second) };
            var peaks = new long[2]; var owners = new int[world.Match.Objectives.Count]; int ownershipChanges = 0;
            var timer = Stopwatch.StartNew(); long deadline = world.TickIndex + deadlineSeconds * (long)World.TickRate;
            while (!world.Match.IsFinished && world.TickIndex < deadline)
            {
                // Rotate controller submission order; both still observe the same pre-movement tick.
                if (world.TickIndex % 20 < 10) { ais[0].Tick(); ais[1].Tick(); } else { ais[1].Tick(); ais[0].Tick(); }
                world.Tick();
                for (int index = 0; index < 2; index++) peaks[index] = Math.Max(peaks[index], world.Match.GetHoldTicks(ais[index].PlayerId));
                for (int index = 0; index < owners.Length; index++)
                {
                    if (owners[index] != world.Match.Objectives[index].OwnerId) ownershipChanges++;
                    owners[index] = world.Match.Objectives[index].OwnerId;
                }
                if (world.TickIndex % 2400 == 0) UnityEngine.Debug.Log($"EMBERFIELD_OFFLINE_AI_PROGRESS tick={world.TickIndex} units={world.Units.Count} deaths={world.DeathCount} p1={ais[0].Status} p2={ais[1].Status}");
            }
            timer.Stop();
            var result = new OfflineAiMatchReport
            {
                Mode = world.Match.Mode.ToString(), Finished = world.Match.IsFinished, WinnerId = world.Match.WinnerId,
                Reason = world.Match.Reason.ToString(), Ticks = world.TickIndex, SimulatedSeconds = world.TickIndex / (double)World.TickRate,
                RunnerWallSeconds = timer.Elapsed.TotalSeconds, Deaths = world.DeathCount, ObjectiveOwnershipChanges = ownershipChanges,
                Passed = world.Match.IsFinished && (world.Match.Mode == VictoryMode.Dominion
                    ? world.Match.Reason == MatchEndReason.Dominion || world.Match.Reason == MatchEndReason.SimultaneousDominion
                    : world.Match.Reason == MatchEndReason.Conquest || world.Match.Reason == MatchEndReason.SimultaneousElimination),
                Players = new OfflineAiPlayerReport[2]
            };
            for (int index = 0; index < 2; index++)
            {
                var ai = ais[index]; world.TryGetPlayer(ai.PlayerId, out var player); var stats = ai.Statistics;
                var entry = new OfflineAiPlayerReport
                {
                    PlayerId = player.Id, FactionId = player.FactionId, EraTier = player.EraTier, ResearchCount = player.CompletedTechnologyIds.Count,
                    Commands = stats.Commands, Accepted = stats.Accepted, Rejected = stats.Rejected, GatherOrders = stats.GatherOrders,
                    BuildingsStarted = stats.BuildingsStarted, UnitsQueued = stats.UnitsQueued, ResearchStarted = stats.ResearchStarted,
                    Attacks = stats.AttackOrders, Scouts = stats.ScoutOrders, Retreats = stats.RetreatOrders, Spent = stats.Spent,
                    Stock = player.Resources, PeakMajorityHoldTicks = peaks[index], RejectionReasons = Reasons(stats)
                };
                var start = world.Definition.StartingResources;
                entry.Deposited = new ResourceAmount(player.Resources.Food + stats.Spent.Food - start.Food, player.Resources.Wood + stats.Spent.Wood - start.Wood,
                    player.Resources.Metal + stats.Spent.Metal - start.Metal, player.Resources.Stone + stats.Spent.Stone - start.Stone);
                foreach (var unit in world.Units) if (unit.OwnerId == player.Id) { entry.Units++; if (unit.IsWorker) entry.Workers++; }
                foreach (var building in world.Buildings) if (building.OwnerId == player.Id) entry.Buildings++;
                result.Players[index] = entry;
                // A controller that spends a large share of its thinking on orders the rules refuse is
                // starving its own economy and army of the budget those orders consume. One case once
                // reached 44 % this way, unnoticed, because only the total was reported.
                if (entry.Commands >= 200 && entry.Rejected * 100 > entry.Commands * MaximumRejectedPercent) result.Passed = false;
            }
            // This fingerprint identifies the observed final result, not a cross-runtime replay proof.
            var state = new StringBuilder(); state.Append(world.TickIndex).Append(':').Append(world.Match.WinnerId).Append(':').Append(world.Match.Reason);
            foreach (var unit in world.Units) state.Append('|').Append(unit.Id).Append(':').Append(unit.OwnerId).Append(':').Append(unit.DefinitionId).Append(':').Append(unit.Position.X).Append(':').Append(unit.Position.Z).Append(':').Append(unit.Health);
            foreach (var building in world.Buildings) state.Append('|').Append(building.Id).Append(':').Append(building.OwnerId).Append(':').Append(building.DefinitionId).Append(':').Append(building.Health);
            result.FinalStateSha256 = Hash(Encoding.UTF8.GetBytes(state.ToString()));
            return result;
        }

        private static void Write(OfflineAiRunReport report)
        {
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/offline-matches.json", JsonUtility.ToJson(report, true));
            var text = new StringBuilder("# Natural offline AI matches\n\n" + report.Method + "\n\n");
            text.AppendLine($"Unity: {report.UnityVersion}; UTC: {report.UtcTime}; deadline: {report.DeadlineSeconds} simulated seconds per case.");
            text.AppendLine($"\nRules SHA256: `{report.RulesSha256}`\n\nMap SHA256: `{report.MapSha256}`\n");
            text.AppendLine("Each mode uses both Aven/Serevin ownership assignments on the same authored map. Both sides start with four Tenders, a Hearth and ordinary starting stock. Fog observations, legal spending and public Dominion objectives are used. The game retains Conquest as the Dominion backup, but this verification requires an actual beacon-hold outcome in its Dominion cases. A timeout is a failed completion criterion, not a draw or manufactured win. Hold ticks are continuous-majority peaks. Deposits are end stock plus recorded spending minus initial stock; resources still carried are excluded.\n");
            text.AppendLine("| Mode | Player1 faction | Finished / reason | Winner | Simulated seconds | Deaths | Objective ownership changes | Pass |\n| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var item in report.Cases ?? Array.Empty<OfflineAiMatchReport>())
                text.AppendLine($"| {item.Mode} | {item.LocalFaction} | {item.Finished} / {item.Reason} | {item.WinnerId} | {item.SimulatedSeconds.ToString("0.00", CultureInfo.InvariantCulture)} | {item.Deaths} | {item.ObjectiveOwnershipChanges} | {item.Passed} |");
            foreach (var item in report.Cases ?? Array.Empty<OfflineAiMatchReport>())
            {
                text.AppendLine($"\n## {item.Mode}, Player1 {item.LocalFaction}\n\nFinal-state fingerprint: `{item.FinalStateSha256}`. Accelerated runner wall time: {item.RunnerWallSeconds.ToString("0.000", CultureInfo.InvariantCulture)} s; not a frame-rate measurement.\n");
                foreach (var player in item.Players)
                    text.AppendLine($"Player {player.PlayerId} ({player.FactionId}): {player.Workers} workers / {player.Units} units / {player.Buildings} buildings remain; Era {player.EraTier}, {player.ResearchCount} technologies; {player.Accepted}/{player.Commands} orders accepted ({player.Rejected} rejected). Gather {player.GatherOrders}, build {player.BuildingsStarted}, train {player.UnitsQueued}, research {player.ResearchStarted}, attack {player.Attacks}, scout {player.Scouts}, retreat {player.Retreats}. Deposited F/W/M/S: {Amounts(player.Deposited)}; spent: {Amounts(player.Spent)}; stock: {Amounts(player.Stock)}. Peak majority hold: {player.PeakMajorityHoldTicks} ticks. Rejections: {player.RejectionReasons}.\n");
            }
            text.AppendLine("\nPassed: " + report.Passed);
            File.WriteAllText("TestResults/offline-matches.md", text.ToString());
        }
        // Rejected orders are wasted thinking. Naming the reason turns a number into something fixable.
        private static string Reasons(OfflineAiStatistics stats)
        {
            var ordered = new List<KeyValuePair<CommandRejection, int>>(stats.Rejections);
            ordered.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key.ToString(), b.Key.ToString()));
            if (ordered.Count == 0) return "none";
            var text = new StringBuilder();
            foreach (var entry in ordered) { if (text.Length > 0) text.Append(", "); text.Append(entry.Key).Append(' ').Append(entry.Value); }
            return text.ToString();
        }
        private static string Amounts(ResourceAmount value) => $"{value.Food}/{value.Wood}/{value.Metal}/{value.Stone}";
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
