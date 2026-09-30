using System;
using System.Diagnostics;
using Emberfield.Simulation;

namespace Emberfield.Diagnostics
{
    [Serializable]
    public sealed class TimingDistribution
    {
        public int Samples;
        public double P50Milliseconds, P95Milliseconds, P99Milliseconds, MaxMilliseconds, TotalMilliseconds;

        internal static TimingDistribution From(long[] values, int count)
        {
            var result = new TimingDistribution { Samples = count };
            if (count == 0) return result;
            long total = 0; for (int i = 0; i < count; i++) total += values[i];
            Array.Sort(values, 0, count);
            result.P50Milliseconds = MovementBenchmark.Milliseconds(values[(int)Math.Ceiling(count * .50) - 1]);
            result.P95Milliseconds = MovementBenchmark.Milliseconds(values[(int)Math.Ceiling(count * .95) - 1]);
            result.P99Milliseconds = MovementBenchmark.Milliseconds(values[(int)Math.Ceiling(count * .99) - 1]);
            result.MaxMilliseconds = MovementBenchmark.Milliseconds(values[count - 1]);
            result.TotalMilliseconds = MovementBenchmark.Milliseconds(total);
            return result;
        }
    }

    [Serializable]
    public sealed class MovementBenchmarkResult
    {
        public string Scenario, FixtureVersion;
        public int Movers, TotalUnits, Repetition, SimulatedTicks, Orders, AcceptedOrders;
        public string[] OrderResults;
        public double CommandBatchMilliseconds, ScheduledEventMilliseconds;
        public long CommandBatchAllocatedBytes, ScheduledEventAllocatedBytes, TickAllocatedBytes, MaxTickAllocatedBytes;
        public bool AllocationMeasurementAvailable;
        public string AllocationMeasurementMethod;
        public long AllocationProbeObservedBytes;
        public TimingDistribution AllTicks, ActiveTicks;
        public long PathQueries, AttackQueries, ConnectivityRebuilds, PathVisitedCells, AttackVisitedCells, ConnectivityVisitedCells;
        public long SharedFieldBuildCount, SharedFieldCacheHitCount, RecoveryQueryCount;
        public string NavigationCounterMethod = "Path queries include group-route requests and local recovery. Shared-field builds/hits count field lookups; recovery is a subset of path queries. All remain in the path timing/visited-cell bucket; do not sum these counters as independent searches.";
        public double PathMilliseconds, AttackMilliseconds, ConnectivityMilliseconds, MaxPathMilliseconds, MaxAttackMilliseconds, MaxConnectivityMilliseconds;
        public MovementObservation Observation;
        public string DynamicBuildResult;
        public bool NormalHundredUnitGate, AcceptancePassed, RequiredCasePassed, RejectedUnitsStayedStill;
        public string AcceptanceReason;
        public double AllArrivedSeconds = -1;
    }

    /// <summary>CPU-only diagnostic runner: no rendering, sleeping or authoritative timing decisions.</summary>
    public static class MovementBenchmark
    {
        public const int DefaultSeconds = 120;
        private static bool allocationProbeComplete, allocationCounterWorks;
        private static long allocationProbeBytes = -1;
        public static long AllocationProbeObservedBytes { get { CheckAllocationCounter(); return allocationProbeBytes; } }
        public static string AllocationMeasurementMethod
        {
            get
            {
                CheckAllocationCounter();
                return allocationCounterWorks
                    ? "GC.GetAllocatedBytesForCurrentThread; capability verified by a retained 4096-byte array increasing the counter by at least4096 bytes; measured windows exclude diagnostics."
                    : "Unavailable: GC.GetAllocatedBytesForCurrentThread did not report at least4096 bytes for a retained 4096-byte array (or threw NotSupported/NotImplemented). Allocation fields are -1, never a zero-allocation claim.";
            }
        }

        public static void WarmUp()
        {
            // Separate worlds warm JIT/observer paths. Measured worlds start fresh, without warmed navigation caches.
            foreach (MovementScenarioKind kind in Enum.GetValues(typeof(MovementScenarioKind)))
            {
                var scenario = MovementScenarioFactory.Create(kind, 12);
                scenario.World.NavigationMetrics.Enabled = true;
                var observer = new MovementObserver(scenario);
                scenario.IssueOrders(); observer.Sample();
                for (int i = 0; i < 120; i++) { scenario.AdvanceScheduledEvents(); scenario.World.Tick(); observer.Sample(); }
            }
            AllocatedBytes();
        }

        public static MovementBenchmarkResult Run(MovementScenarioKind kind, int count, int seconds = DefaultSeconds, int repetition = 1)
        {
            if (seconds < 1 || seconds > 600) throw new ArgumentOutOfRangeException(nameof(seconds));
            var scenario = MovementScenarioFactory.Create(kind, count);
            var world = scenario.World;
            var observer = new MovementObserver(scenario);
            int ticks = seconds * World.TickRate;
            var tickTimes = new long[ticks]; var activeTimes = new long[ticks]; int activeCount = 0;
            var initialPositions = new SimPoint[count];
            for (int i = 0; i < count; i++) { world.TryGetUnit(scenario.UnitIds[i], out var unit); initialPositions[i] = unit.Position; }
            var result = new MovementBenchmarkResult
            {
                Scenario = kind.ToString(), FixtureVersion = MovementScenarioFactory.FixtureVersion,
                Movers = count, TotalUnits = world.Units.Count, Repetition = repetition, SimulatedTicks = ticks,
                Orders = scenario.Orders.Count, AllocationMeasurementAvailable = AllocatedBytes() >= 0,
                AllocationMeasurementMethod = AllocationMeasurementMethod, AllocationProbeObservedBytes = AllocationProbeObservedBytes,
                NormalHundredUnitGate = count == 100 && (kind == MovementScenarioKind.OpenField || kind == MovementScenarioKind.WideCorridor || kind == MovementScenarioKind.CrossingGroups)
            };
            world.NavigationMetrics.Enabled = true; world.NavigationMetrics.Reset();
            long allocationStart = AllocatedBytes(), started = Stopwatch.GetTimestamp();
            var commands = scenario.IssueOrders();
            result.CommandBatchMilliseconds = Milliseconds(Stopwatch.GetTimestamp() - started);
            result.CommandBatchAllocatedBytes = AllocationDifference(allocationStart);
            result.OrderResults = new string[commands.Length];
            for (int i = 0; i < commands.Length; i++)
            { if (commands[i].Accepted) result.AcceptedOrders++; result.OrderResults[i] = commands[i].Reason.ToString(); }
            observer.Sample();
            for (int tick = 0; tick < ticks; tick++)
            {
                // Scheduled build command cost is separate from World.Tick; its navigation queries remain included.
                allocationStart = AllocatedBytes(); started = Stopwatch.GetTimestamp();
                var scheduled = scenario.AdvanceScheduledEvents();
                long eventTime = Stopwatch.GetTimestamp() - started;
                long eventBytes = AllocationDifference(allocationStart);
                if (scheduled.HasValue)
                { result.ScheduledEventMilliseconds += Milliseconds(eventTime); result.ScheduledEventAllocatedBytes += Math.Max(0, eventBytes); }
                bool active = observer.Current.Moving > 0;
                allocationStart = AllocatedBytes(); started = Stopwatch.GetTimestamp();
                world.Tick();
                long elapsed = Stopwatch.GetTimestamp() - started;
                long bytes = AllocationDifference(allocationStart);
                tickTimes[tick] = elapsed;
                if (active) activeTimes[activeCount++] = elapsed;
                if (bytes >= 0) { result.TickAllocatedBytes += bytes; result.MaxTickAllocatedBytes = Math.Max(result.MaxTickAllocatedBytes, bytes); }
                observer.Sample(); // Geometry/progress checks are excluded from measured time and allocation windows.
            }
            result.AllTicks = TimingDistribution.From(tickTimes, ticks);
            result.ActiveTicks = TimingDistribution.From(activeTimes, activeCount);
            result.Observation = observer.Current;
            var metrics = world.NavigationMetrics.Snapshot;
            result.PathQueries = metrics.PathQueryCount; result.AttackQueries = metrics.AttackQueryCount;
            result.SharedFieldBuildCount = metrics.SharedFieldBuildCount;
            result.SharedFieldCacheHitCount = metrics.SharedFieldCacheHitCount;
            result.RecoveryQueryCount = metrics.RecoveryQueryCount;
            result.ConnectivityRebuilds = metrics.ConnectivityRebuildCount;
            result.PathVisitedCells = metrics.PathVisitedCells; result.AttackVisitedCells = metrics.AttackVisitedCells;
            result.ConnectivityVisitedCells = metrics.ConnectivityVisitedCells;
            result.PathMilliseconds = Milliseconds(metrics.PathElapsedStopwatchTicks);
            result.AttackMilliseconds = Milliseconds(metrics.AttackElapsedStopwatchTicks);
            result.ConnectivityMilliseconds = Milliseconds(metrics.ConnectivityElapsedStopwatchTicks);
            result.MaxPathMilliseconds = Milliseconds(metrics.MaxPathQueryStopwatchTicks);
            result.MaxAttackMilliseconds = Milliseconds(metrics.MaxAttackQueryStopwatchTicks);
            result.MaxConnectivityMilliseconds = Milliseconds(metrics.MaxConnectivityStopwatchTicks);
            result.DynamicBuildResult = scenario.DynamicBuildResult.HasValue ? scenario.DynamicBuildResult.Value.Reason.ToString() : "Not scheduled";
            result.RejectedUnitsStayedStill = true;
            for (int i = 0; i < count; i++)
                if (!world.TryGetUnit(scenario.UnitIds[i], out var unit) || unit.Position != initialPositions[i]) result.RejectedUnitsStayedStill = false;
            var observation = result.Observation;
            if (observation.AllArrivedAtTick >= 0) result.AllArrivedSeconds = observation.AllArrivedAtTick / (double)World.TickRate;
            bool staticValid = observation.MaxInvalidPositions == 0;
            if (kind == MovementScenarioKind.Unreachable)
            {
                result.AcceptancePassed = result.AcceptedOrders == 0 && result.RejectedUnitsStayedStill && staticValid;
                result.AcceptanceReason = "Unreachable orders must reject atomically, remain still and retain valid positions; stalls are expected.";
            }
            else
            {
                result.AcceptancePassed = result.AcceptedOrders == result.Orders && observation.Arrived == count &&
                    observation.Stalled == 0 && observation.OverlapPairs == 0 && observation.LongestOverlapTicks < MovementObserver.PersistentOverlapTicks && staticValid &&
                    (kind != MovementScenarioKind.DynamicObstacle || (scenario.DynamicBuildResult.HasValue && scenario.DynamicBuildResult.Value.Accepted));
                result.AcceptanceReason = "All orders accepted; all movers arrive; no final stalls/overlaps, no pair overlaps for 20 ticks, no static/map violations; dynamic build accepted when applicable.";
            }
            // Narrow/capacity-constrained scenarios and counts other than 100 expose outcomes, not an undisclosed universal arrival gate.
            result.RequiredCasePassed = staticValid && (!result.NormalHundredUnitGate || result.AcceptancePassed) &&
                (kind != MovementScenarioKind.Unreachable || result.AcceptancePassed);
            if (!result.AllocationMeasurementAvailable)
                result.CommandBatchAllocatedBytes = result.ScheduledEventAllocatedBytes = result.TickAllocatedBytes = result.MaxTickAllocatedBytes = -1;
            return result;
        }

        public static double Milliseconds(long stopwatchTicks) => stopwatchTicks * 1000d / Stopwatch.Frequency;
        private static long AllocatedBytes()
        { CheckAllocationCounter(); return allocationCounterWorks ? RawAllocatedBytes() : -1; }
        private static long RawAllocatedBytes()
        {
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (NotSupportedException) { return -1; }
            catch (NotImplementedException) { return -1; }
        }
        private static void CheckAllocationCounter()
        {
            if (allocationProbeComplete) return;
            long before = RawAllocatedBytes();
            var allocation = new byte[4096]; allocation[4095] = 1;
            long after = RawAllocatedBytes();
            GC.KeepAlive(allocation);
            allocationProbeBytes = before < 0 || after < before ? -1 : after - before;
            allocationCounterWorks = allocationProbeBytes >= 4096;
            allocationProbeComplete = true;
        }
        private static long AllocationDifference(long before) => before < 0 ? -1 : AllocatedBytes() - before;
    }
}
