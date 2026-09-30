using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Emberfield.Diagnostics;
using Emberfield.Presentation;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    [Serializable]
    public sealed class OfflinePerformanceReport
    {
        public string Label, FixtureVersion, UtcTime, UnityVersion, Runtime, OperatingSystem, Processor, GraphicsDevice;
        public string SourceSha256, RunnerSha256, RulesSha256, MapSha256;
        public string MeasurementMethod = OfflinePerformanceBenchmark.MeasurementMethod;
        public string FingerprintMethod = OfflinePerformanceBenchmark.FingerprintMethod;
        public string WarmupMethod = "Unmeasured separate12-mover synthetic world advances1200ticks before any sampled case. Every measured case starts with a new World and fresh navigation caches. Shipped mid samples first advance their own AI/world to the requested start tick; that progression is excluded from timing windows.";
        public int ProcessorCount, SystemMemoryMegabytes, StressSeconds, ShippedSeconds, MidStartSeconds, Repetitions, VerifiedRepetitionPairs;
        public long StopwatchFrequency;
        public double RunnerWallSeconds;
        public bool IncludeShipped, Complete, RepetitionsMatch, SyntheticFixtureValid = true;
        public OfflinePerformanceResult[] Cases;
    }

    public static class OfflinePerformanceVerification
    {
        [MenuItem("Emberfield/Measure offline CPU workload")]
        public static void Run()
        {
            string label = Argument("-offlinePerfLabel", "baseline");
            if (!Regex.IsMatch(label, @"^[A-Za-z0-9_-]+$")) throw new ArgumentException("offlinePerfLabel must contain letters, digits, underscore or dash.");
            int seconds = Number(Argument("-offlinePerfSeconds", "120"), 1, 600);
            int shippedSeconds = Number(Argument("-offlinePerfShippedSeconds", "60"), 1, 600);
            int midStart = Number(Argument("-offlinePerfMidStartSeconds", "300"), 0, 3600);
            int repetitions = Number(Argument("-offlinePerfRepetitions", "2"), 1, 10);
            if (!bool.TryParse(Argument("-offlinePerfIncludeShipped", "true"), out bool includeShipped))
                throw new ArgumentException("offlinePerfIncludeShipped must be true or false.");
            var counts = new List<int>();
            foreach (string token in Argument("-offlinePerfCounts", "50,100,200,300,500").Split(','))
            { int count = Number(token, 1, 500); if (!counts.Contains(count)) counts.Add(count); }
            var rulesAsset = Resources.Load<TextAsset>("Definitions/greybox");
            var mapAsset = Resources.Load<TextAsset>("Maps/amber_crossing");
            if (rulesAsset == null || mapAsset == null) throw new InvalidOperationException("Shipped rules and Amber Crossing map are required.");
            var rules = JsonUtility.FromJson<GameDefinition>(rulesAsset.text);
            var report = new OfflinePerformanceReport
            {
                Label = label, FixtureVersion = OfflinePerformanceBenchmark.FixtureVersion,
                UtcTime = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), UnityVersion = Application.unityVersion,
                Runtime = Environment.Version.ToString(), OperatingSystem = SystemInfo.operatingSystem,
                Processor = SystemInfo.processorType, ProcessorCount = SystemInfo.processorCount, SystemMemoryMegabytes = SystemInfo.systemMemorySize,
                GraphicsDevice = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType,
                StopwatchFrequency = Stopwatch.Frequency, StressSeconds = seconds, ShippedSeconds = shippedSeconds,
                MidStartSeconds = midStart, Repetitions = repetitions, IncludeShipped = includeShipped, RepetitionsMatch = true,
                SourceSha256 = SourceHash(), RunnerSha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/OfflinePerformanceVerification.cs")),
                RulesSha256 = Hash(File.ReadAllBytes("Assets/Game/Resources/Definitions/greybox.json")),
                MapSha256 = Hash(File.ReadAllBytes("Assets/Game/Resources/Maps/amber_crossing.json"))
            };
            var timer = Stopwatch.StartNew();
            OfflinePerformanceBenchmark.RunStress(rules, 12, 1200);
            var cases = new List<OfflinePerformanceResult>();
            var firstRuns = new Dictionary<string, OfflinePerformanceResult>(StringComparer.Ordinal);
            foreach (int count in counts) for (int repetition = 1; repetition <= repetitions; repetition++)
            {
                UnityEngine.Debug.Log($"EMBERFIELD_OFFLINE_PERF_START SyntheticMovers N={count} repetition={repetition}");
                Add(report, cases, firstRuns, OfflinePerformanceBenchmark.RunStress(rules, count, seconds * World.TickRate, repetition));
                Write(report);
            }
            if (includeShipped) foreach (var mode in new[] { VictoryMode.Conquest, VictoryMode.Dominion })
            foreach (string faction in new[] { "aven", "serevin" })
            foreach (int warmup in new[] { 0, midStart * World.TickRate })
            for (int repetition = 1; repetition <= repetitions; repetition++)
            {
                UnityEngine.Debug.Log($"EMBERFIELD_OFFLINE_PERF_START Shipped {mode} {faction} start={warmup} repetition={repetition}");
                var world = DefinitionLoader.CreateOfflineWorld(faction, mode);
                Add(report, cases, firstRuns, OfflinePerformanceBenchmark.RunShipped(world, warmup, shippedSeconds * World.TickRate, repetition));
                Write(report);
            }
            report.Complete = true; report.RunnerWallSeconds = timer.Elapsed.TotalSeconds; Write(report);
            if (!report.RepetitionsMatch) throw new InvalidOperationException("Offline performance repetitions changed observed state or command trace. Inspect adjacent JSON before comparing timing.");
            if (!report.SyntheticFixtureValid) throw new InvalidOperationException("Synthetic workload lost requested movers, rejected a scripted action, or ended early. Inspect fixture results before comparing timing.");
            UnityEngine.Debug.Log($"EMBERFIELD_OFFLINE_PERFORMANCE_OK {cases.Count} cases; verified repeat pairs={report.VerifiedRepetitionPairs}");
        }

        private static void Add(OfflinePerformanceReport report, List<OfflinePerformanceResult> cases,
            Dictionary<string, OfflinePerformanceResult> firstRuns, OfflinePerformanceResult result)
        {
            string key = result.Fixture + "/" + result.RequestedMovers + "/" + result.Mode + "/" + result.PlayerOneFaction + "/" + result.RequestedWarmupTicks;
            result.RepetitionMatchesFirst = true;
            if (firstRuns.TryGetValue(key, out var first))
            {
                report.VerifiedRepetitionPairs++;
                result.RepetitionMatchesFirst = first.StartStateSha256 == result.StartStateSha256 && first.EndStateSha256 == result.EndStateSha256 &&
                    first.CommandTraceSha256 == result.CommandTraceSha256 && first.MeasuredTicks == result.MeasuredTicks;
                report.RepetitionsMatch &= result.RepetitionMatchesFirst;
            }
            else firstRuns.Add(key, result);
            if (result.RequestedMovers > 0) report.SyntheticFixtureValid &= result.SurvivingRequestedMovers == result.RequestedMovers &&
                result.ScriptAccepted == result.ScriptCommands && result.MeasuredTicks == result.RequestedSampleTicks;
            cases.Add(result); report.Cases = cases.ToArray();
            UnityEngine.Debug.Log($"EMBERFIELD_OFFLINE_PERF_CASE {key} ticks={result.MeasuredTicks} worldP95={Format(result.WorldTicks.P95Milliseconds)}ms aiThinkP95={Format(result.AiThinkingTicks.P95Milliseconds)}ms repeat={result.RepetitionMatchesFirst}");
        }
        private static void Write(OfflinePerformanceReport report)
        {
            Directory.CreateDirectory("TestResults"); string prefix = "TestResults/offline-performance-" + report.Label;
            File.WriteAllText(prefix + ".json", JsonUtility.ToJson(report, true));
            var text = new StringBuilder("# Offline CPU workload: " + report.Label + "\n\n");
            text.AppendLine($"UTC {report.UtcTime}; Unity {report.UnityVersion}; runtime {report.Runtime}; {report.OperatingSystem}; {report.Processor} ({report.ProcessorCount} logical processors); system memory {report.SystemMemoryMegabytes}MB. Graphics device reported by runtime: {report.GraphicsDevice}; no graphics measurement is performed.");
            text.AppendLine("\n" + report.MeasurementMethod + "\n\n" + report.WarmupMethod);
            text.AppendLine($"\nFixture `{report.FixtureVersion}`; Simulation+Diagnostics source SHA256 `{report.SourceSha256}`; editor runner SHA256 `{report.RunnerSha256}`.\n\nShipped rules SHA256 `{report.RulesSha256}`; Amber Crossing map SHA256 `{report.MapSha256}`.");
            text.AppendLine("\nSynthetic rows use a128x128m map and author N moving Aven Tenders, four other gatherers per side, one Threadkeeper and completed infrastructure, with 5000 of each resource/player and base population 1000. An11-cell solid buffer across z70..80 encloses the Serevin AI annex beyond attack range, preventing combat deaths from changing N. This is a noncombat scaling fixture with a deliberately unreachable opponent, not a complete match. Serevin AI and paid scripted Aven actions run under shipped rules/timers. It is not a normal start or a population/progression recommendation. Unrestricted shipped samples cover combat. The exact schedule and rejected script actions are in source/JSON.");
            text.AppendLine($"\nShipped rows use ordinary starts with both factions and both victory modes. Early sampling starts at0s; mid sampling at{report.MidStartSeconds}s after ordinary AI progression. Requested windows: stress{report.StressSeconds}s, shipped{report.ShippedSeconds}s, repetitions{report.Repetitions}. A result ends sampling; unavailable/truncated windows remain explicit. Full-match pacing evidence comes from OfflineMatchVerification, not this runner.");
            text.AppendLine("\n| Fixture / mode / P1 faction | N movers | Rep | Start / sampled ticks | World p50 / p95 / p99 / max ms | AI think p50 / p95 / max ms | Script batch total / max ms | Units initial / peak / final | Window available / match ended |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var item in report.Cases ?? Array.Empty<OfflinePerformanceResult>())
                text.AppendLine($"| {item.Fixture} / {item.Mode} / {item.PlayerOneFaction} | {item.RequestedMovers} | {item.Repetition} | {item.StartTick} / {item.MeasuredTicks} | {Format(item.WorldTicks.P50Milliseconds)} / {Format(item.WorldTicks.P95Milliseconds)} / {Format(item.WorldTicks.P99Milliseconds)} / {Format(item.WorldTicks.MaxMilliseconds)} | {Format(item.AiThinkingTicks.P50Milliseconds)} / {Format(item.AiThinkingTicks.P95Milliseconds)} / {Format(item.AiThinkingTicks.MaxMilliseconds)} | {Format(item.ScriptBatches.TotalMilliseconds)} / {Format(item.ScriptBatches.MaxMilliseconds)} | {item.InitialUnits} / {item.PeakUnits} / {item.FinalUnits} | {item.WindowAvailable} / {item.MatchFinished} |");
            text.AppendLine("\n| Fixture / N / mode / P1 / start / rep | Moving / gatherers peak | Active research / charters / affected units / boosted producers peak | AI accepted / commands | Script accepted / commands | Navigation queries / ms | Allocation bytes World / AI / script | GC0 / GC1 / GC2 | Heap before / peak / after bytes |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var item in report.Cases ?? Array.Empty<OfflinePerformanceResult>())
                text.AppendLine($"| {item.Fixture} / {item.RequestedMovers} / {item.Mode} / {item.PlayerOneFaction} / {item.StartTick} / {item.Repetition} | {item.PeakMovingUnits} / {item.PeakGatherers} | {item.PeakActiveResearch} / {item.PeakActiveCharters} / {item.PeakCharterAffectedUnits} / {item.PeakBoostedProducers} | {item.AiAccepted}/{item.AiCommands} | {item.ScriptAccepted}/{item.ScriptCommands} | {item.PathQueries + item.AttackQueries} / {Format(item.NavigationMilliseconds)} | {item.WorldAllocatedBytes} / {item.AiAllocatedBytes} / {item.ScriptAllocatedBytes} | {item.Gen0Collections} / {item.Gen1Collections} / {item.Gen2Collections} | {item.ManagedHeapBeforeBytes} / {item.ManagedHeapPeakBytes} / {item.ManagedHeapAfterBytes} |");
            if (report.Cases != null && report.Cases.Length > 0)
                text.AppendLine("\nAllocation capability: " + report.Cases[0].AllocationMeasurementMethod + " Probe bytes: " + report.Cases[0].AllocationProbeObservedBytes + ".");
            text.AppendLine("\n| Synthetic movers | Rep | Group moves | Move p50 / max ms | Move path queries / visited cells |\n| --- | --- | --- | --- | --- |");
            foreach (var item in report.Cases ?? Array.Empty<OfflinePerformanceResult>()) if (item.RequestedMovers > 0)
                text.AppendLine($"| {item.RequestedMovers} | {item.Repetition} | {item.MoveCommands.Samples} | {Format(item.MoveCommands.P50Milliseconds)} / {Format(item.MoveCommands.MaxMilliseconds)} | {item.MovePathQueries} / {item.MoveVisitedCells} |");
            text.AppendLine("\nNavigation query count excludes connectivity rebuilds; navigation time includes them. Shared-field hits and recovery queries are subsets of path work, not extra independent queries. Script latency brackets Submit calls, excluding command object creation. AI timing includes its command object creation and submissions plus fixed command-event capture; trace serialization is excluded.");
            text.AppendLine("\n" + report.FingerprintMethod + " Command traces include command tick, ordered payload and result, but exclude unmeasured natural warm-up commands. Start-state hashes detect a changed warm-up outcome. Compare before/after only with matching fixture parameters, shipped hashes, start/end hashes and trace hashes; timing differences alone do not demonstrate semantic equivalence.");
            text.AppendLine($"\nComplete: {report.Complete}. Synthetic fixture valid (all requested movers survive, scripted commands accept, full window runs): {report.SyntheticFixtureValid}. Matching repetitions: {report.RepetitionsMatch}; verified pairs: {report.VerifiedRepetitionPairs}. A single repetition verifies no pair. Runner wall seconds (all setup, warm-up and reporting included): {Format(report.RunnerWallSeconds)}. No performance budget or rendering/mobile pass is inferred from completing this CPU measurement.");
            File.WriteAllText(prefix + ".md", text.ToString());
        }
        private static string Argument(string name, string fallback)
        { var args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return fallback; }
        private static int Number(string value, int min, int max)
        { if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < min || number > max) throw new ArgumentException("Integer outside supported range: " + value); return number; }
        private static string Format(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string SourceHash()
        {
            var files = new List<string>(); files.AddRange(Directory.GetFiles("Assets/Game/Simulation", "*.cs", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles("Assets/Game/Diagnostics", "*.cs", SearchOption.AllDirectories)); files.Sort(StringComparer.Ordinal);
            using (var stream = new MemoryStream())
            {
                foreach (string path in files)
                { var bytes = Encoding.UTF8.GetBytes(path.Replace('\\', '/') + "\n" + File.ReadAllText(path) + "\n"); stream.Write(bytes, 0, bytes.Length); }
                return Hash(stream.ToArray());
            }
        }
    }
}
