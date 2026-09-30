using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Emberfield.Diagnostics;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    [Serializable]
    public sealed class MovementRunReport
    {
        public string Mode, Label, FixtureVersion, UtcTime, UnityVersion, Runtime, OperatingSystem, Processor;
        public string SourceSha256, ReferenceAlgorithmCommit = "9d61486";
        public string AllocationMeasurementMethod;
        public long AllocationProbeObservedBytes;
        public long StopwatchFrequency;
        public int ProcessorCount, SecondsPerCase, Repetitions, ObserverCadenceTicks = 1;
        public double RunnerWallSeconds;
        public bool RunComplete, RequiredGatesCovered, RequiredGatesPassed;
        public MovementBenchmarkResult[] Cases;
    }

    public static class MovementVerification
    {
        [MenuItem("Emberfield/Measure movement baseline")]
        public static void Run()
        {
            string mode = Argument("-movementMode", "Baseline");
            if (mode != "Baseline" && mode != "Final") throw new ArgumentException("movementMode must be Baseline or Final.");
            string label = Argument("-movementLabel", mode.ToLowerInvariant());
            if (!Regex.IsMatch(label, @"^[A-Za-z0-9_-]+$")) throw new ArgumentException("movementLabel must contain letters, digits, underscore or dash.");
            int seconds = ParseInteger(Argument("-movementSeconds", "120"), 1, 600);
            int repetitions = ParseInteger(Argument("-movementRepetitions", "1"), 1, 10);
            var counts = new List<int>();
            foreach (string value in Argument("-movementCounts", "50,100,200,300,500").Split(',')) counts.Add(ParseInteger(value, 1, 500));
            var kinds = new List<MovementScenarioKind>();
            foreach (string value in Argument("-movementScenarios", "OpenField,WideCorridor,NarrowChoke,CrossingGroups,DynamicObstacle,Unreachable").Split(','))
            {
                if (!Enum.TryParse(value, out MovementScenarioKind kind) || !Enum.IsDefined(typeof(MovementScenarioKind), kind))
                    throw new ArgumentException("Unknown movement scenario: " + value);
                kinds.Add(kind);
            }
            var report = new MovementRunReport
            {
                Mode = mode, Label = label, FixtureVersion = MovementScenarioFactory.FixtureVersion,
                UtcTime = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), UnityVersion = Application.unityVersion,
                Runtime = Environment.Version.ToString(), OperatingSystem = SystemInfo.operatingSystem,
                Processor = SystemInfo.processorType, ProcessorCount = SystemInfo.processorCount,
                StopwatchFrequency = Stopwatch.Frequency, SecondsPerCase = seconds, Repetitions = repetitions,
                SourceSha256 = SourceHash(), RequiredGatesPassed = true
            };
            report.AllocationMeasurementMethod = MovementBenchmark.AllocationMeasurementMethod;
            report.AllocationProbeObservedBytes = MovementBenchmark.AllocationProbeObservedBytes;
            report.RequiredGatesCovered = counts.Contains(100) && kinds.Contains(MovementScenarioKind.OpenField) &&
                kinds.Contains(MovementScenarioKind.WideCorridor) && kinds.Contains(MovementScenarioKind.CrossingGroups) &&
                kinds.Contains(MovementScenarioKind.Unreachable);
            report.RequiredGatesPassed = report.RequiredGatesCovered;
            var wall = Stopwatch.StartNew();
            MovementBenchmark.WarmUp();
            var cases = new List<MovementBenchmarkResult>();
            foreach (MovementScenarioKind kind in kinds)
            foreach (int count in counts)
            for (int repetition = 1; repetition <= repetitions; repetition++)
            {
                var result = MovementBenchmark.Run(kind, count, seconds, repetition);
                cases.Add(result); report.RequiredGatesPassed &= result.RequiredCasePassed;
                UnityEngine.Debug.Log($"MOVEMENT_CASE {kind} N={count} repetition={repetition} arrival={result.Observation.Arrived}/{count} overlapsMax={result.Observation.MaxOverlapPairs} acceptance={result.AcceptancePassed}");
            }
            report.Cases = cases.ToArray(); report.RunnerWallSeconds = wall.Elapsed.TotalSeconds; report.RunComplete = true;
            Directory.CreateDirectory("TestResults");
            string prefix = "TestResults/movement-" + label;
            File.WriteAllText(prefix + ".json", JsonUtility.ToJson(report, true));
            File.WriteAllText(prefix + ".md", Markdown(report));
            if (mode == "Final" && !report.RequiredGatesPassed)
                throw new InvalidOperationException("Movement acceptance gate failed. Inspect " + prefix + ".md and .json.");
            UnityEngine.Debug.Log($"EMBERFIELD_MOVEMENT_{mode.ToUpperInvariant()}_COMPLETE {cases.Count} cases; required acceptance={report.RequiredGatesPassed}");
        }

        private static string Markdown(MovementRunReport report)
        {
            var text = new StringBuilder("# Movement CPU measurement: " + report.Label + "\n\n");
            text.AppendLine("Captured UTC: " + report.UtcTime + ". Unity " + report.UnityVersion + "; " + report.OperatingSystem + "; " + report.Processor + ".");
            text.AppendLine("\nFixture: `" + report.FixtureVersion + "`; simulation+diagnostics source SHA256: `" + report.SourceSha256 + "`. Pre-optimization algorithm reference: `9d61486`; baseline adds opt-in timing instrumentation only.");
            text.AppendLine("\nEach case uses a fresh 128x96 m map, dense stable IDs, unarmed worker radius300 mm/speed3200 mm/s, and one or two simultaneous group orders. Crossing uses two owners. DynamicObstacle has N movers plus one builder and inserts a 3x9 m footprint at elapsed tick100. No shipped combat asset is changed.");
            text.AppendLine($"\n{report.SecondsPerCase} simulated seconds/case at {World.TickRate} Hz; {report.Repetitions} repetition(s); observer every tick. JIT warm-up uses separate worlds. Command latency includes the entire order batch and destination capture. Navigation totals include commands, scheduled construction and ticks, excluding world creation. Tick time/allocations bracket only World.Tick; observation and report allocations are excluded. Active ticks begin with at least one moving unit; full-window quantiles include settled idle ticks. Allocation counts are managed bytes on the current thread, not total heap/native/GPU usage. Timings are instrumented editor CPU measurements, not rendered FPS or mobile-device results.");
            text.AppendLine("\nAllocation capability: " + report.AllocationMeasurementMethod + " Probe observed bytes: " + report.AllocationProbeObservedBytes + ".");
            text.AppendLine("\nShared-field builds/cache hits and local recovery counts are reported separately below. Group route requests still increment path-query counts even when reusing a field; recovery queries are a subset of the path count. Their timing/visited cells stay in the path bucket. Do not add these counters as separate searches. These supplemental counters were introduced after the preserved baseline capture.");
            text.AppendLine("\nArrival requires Idle within100 mm of the assigned goal. A stalled mover has made less than50 mm progress for200 ticks. Overlap means more than1 mm circle penetration; a pair persisting20 ticks fails the normal criterion. The independent radius/static-cell/map-edge checks run every tick. Peak overlap remains visible even when transient. Unreachable must reject atomically and remain still; its stall count is expected.");
            text.AppendLine("\nFinal required gates: all sampled cases remain inside the map and outside static obstacles; Unreachable rejects/remains still; 100-unit OpenField, WideCorridor and CrossingGroups must all arrive by the deadline, with no final stalls/overlaps and no persistent overlap. Other sizes and constrained scenarios report the same acceptance outcome without silently treating congestion as a success. Baseline records failures without aborting.");
            text.AppendLine("\n| Scenario | Movers / total | Rep | Accepted orders | Command ms | Paths / ms | Active p50 / p95 / p99 / max ms | Tick allocation bytes | Arrived / seconds | Final stalls | Overlap peak / longest ticks / final | Invalid peak | Criterion | Required case |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var c in report.Cases)
            {
                var a = c.ActiveTicks; var o = c.Observation;
                text.AppendLine($"| {c.Scenario} | {c.Movers} / {c.TotalUnits} | {c.Repetition} | {c.AcceptedOrders}/{c.Orders} | {Number(c.CommandBatchMilliseconds)} | {c.PathQueries} / {Number(c.PathMilliseconds)} | {Number(a.P50Milliseconds)} / {Number(a.P95Milliseconds)} / {Number(a.P99Milliseconds)} / {Number(a.MaxMilliseconds)} | {c.TickAllocatedBytes} | {o.Arrived}/{c.Movers} / {(c.AllArrivedSeconds < 0 ? "deadline" : Number(c.AllArrivedSeconds))} | {o.Stalled} | {o.MaxOverlapPairs} / {o.LongestOverlapTicks} / {o.OverlapPairs} | {o.MaxInvalidPositions} | {c.AcceptancePassed} | {c.RequiredCasePassed} |");
            }
            text.AppendLine("\n| Scenario | Movers | Rep | Shared-field builds | Shared-field cache hits | Local recovery queries | Path visited cells |\n| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var c in report.Cases)
                text.AppendLine($"| {c.Scenario} | {c.Movers} | {c.Repetition} | {c.SharedFieldBuildCount} | {c.SharedFieldCacheHitCount} | {c.RecoveryQueryCount} | {c.PathVisitedCells} |");
            text.AppendLine("\nRun complete: " + report.RunComplete + ". Required gates covered: " + report.RequiredGatesCovered + ". Required acceptance passed: " + report.RequiredGatesPassed + ". Runner wall seconds (including observation/warm-up): " + Number(report.RunnerWallSeconds) + ".");
            text.AppendLine("\nThe adjacent JSON contains all-window quantiles, active sample counts, maximum tick/command/event allocation, complete navigation/connectivity counters and times, individual order rejection reasons, dynamic build outcome, maximum penetration, separate outside/static counts and gate reasons. Single-repetition results are a diagnostic sample, not a claim of stable cross-machine performance.");
            return text.ToString();
        }

        private static string Number(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
        private static string Argument(string name, string fallback)
        { var args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1]; return fallback; }
        private static int ParseInteger(string value, int min, int max)
        { if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < min || parsed > max) throw new ArgumentException("Integer outside supported range: " + value); return parsed; }
        private static string SourceHash()
        {
            var files = new List<string>();
            files.AddRange(Directory.GetFiles("Assets/Game/Simulation", "*.cs", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles("Assets/Game/Diagnostics", "*.cs", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            using (var hash = SHA256.Create())
            using (var stream = new MemoryStream())
            {
                foreach (string path in files)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(path.Replace('\\', '/') + "\n" + File.ReadAllText(path) + "\n");
                    stream.Write(bytes, 0, bytes.Length);
                }
                return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "");
            }
        }
    }
}
