using System;
using System.Diagnostics;

namespace Emberfield.Simulation
{
    /// <summary>Opt-in diagnostics. Timings never influence navigation or authoritative state.</summary>
    public sealed class NavigationMetrics
    {
        public bool Enabled { get; set; }
        public static long StopwatchFrequency => Stopwatch.Frequency;
        private readonly long[] counts = new long[3];
        private readonly long[] elapsed = new long[3];
        private readonly long[] visited = new long[3];
        private readonly long[] maxima = new long[3];
        private long sharedFieldBuilds, sharedFieldHits, recoveryQueries;

        public NavigationMetricsSnapshot Snapshot => new NavigationMetricsSnapshot(
            counts[0], counts[1], counts[2], elapsed[0], elapsed[1], elapsed[2],
            visited[0], visited[1], visited[2], maxima[0], maxima[1], maxima[2], sharedFieldBuilds, sharedFieldHits, recoveryQueries);

        public void Reset()
        {
            Array.Clear(counts, 0, counts.Length); Array.Clear(elapsed, 0, elapsed.Length);
            Array.Clear(visited, 0, visited.Length); Array.Clear(maxima, 0, maxima.Length);
            sharedFieldBuilds = sharedFieldHits = recoveryQueries = 0;
        }

        internal void SharedField(bool hit) { if (!Enabled) return; if (hit) sharedFieldHits++; else sharedFieldBuilds++; }
        internal void RecoveryQuery() { if (Enabled) recoveryQueries++; }

        internal long Begin() => Enabled ? Stopwatch.GetTimestamp() : -1;
        internal void End(NavigationQueryKind kind, long started, int visitedCells)
        {
            if (started < 0) return;
            long duration = Stopwatch.GetTimestamp() - started;
            int index = (int)kind;
            counts[index]++; elapsed[index] += duration; visited[index] += visitedCells;
            maxima[index] = Math.Max(maxima[index], duration);
        }
    }

    internal enum NavigationQueryKind { Path, Attack, Connectivity }

    /// <summary>Visited cells count cells removed from a BFS frontier; early exits can visit zero.</summary>
    public readonly struct NavigationMetricsSnapshot
    {
        public readonly long PathQueryCount, AttackQueryCount, ConnectivityRebuildCount;
        public readonly long PathElapsedStopwatchTicks, AttackElapsedStopwatchTicks, ConnectivityElapsedStopwatchTicks;
        public readonly long PathVisitedCells, AttackVisitedCells, ConnectivityVisitedCells;
        public readonly long MaxPathQueryStopwatchTicks, MaxAttackQueryStopwatchTicks, MaxConnectivityStopwatchTicks;
        public readonly long SharedFieldBuildCount, SharedFieldCacheHitCount, RecoveryQueryCount;
        public long TotalQueryCount => PathQueryCount + AttackQueryCount;
        public long TotalElapsedStopwatchTicks => PathElapsedStopwatchTicks + AttackElapsedStopwatchTicks + ConnectivityElapsedStopwatchTicks;
        public long VisitedCells => PathVisitedCells + AttackVisitedCells + ConnectivityVisitedCells;
        public long MaxQueryStopwatchTicks => Math.Max(MaxPathQueryStopwatchTicks, MaxAttackQueryStopwatchTicks);

        internal NavigationMetricsSnapshot(long paths, long attacks, long connectivity, long pathElapsed,
            long attackElapsed, long connectivityElapsed, long pathVisited, long attackVisited, long connectivityVisited,
            long maxPath, long maxAttack, long maxConnectivity, long sharedBuilds, long sharedHits, long recovery)
        {
            PathQueryCount = paths; AttackQueryCount = attacks; ConnectivityRebuildCount = connectivity;
            PathElapsedStopwatchTicks = pathElapsed; AttackElapsedStopwatchTicks = attackElapsed;
            ConnectivityElapsedStopwatchTicks = connectivityElapsed;
            PathVisitedCells = pathVisited; AttackVisitedCells = attackVisited; ConnectivityVisitedCells = connectivityVisited;
            MaxPathQueryStopwatchTicks = maxPath; MaxAttackQueryStopwatchTicks = maxAttack; MaxConnectivityStopwatchTicks = maxConnectivity;
            SharedFieldBuildCount = sharedBuilds; SharedFieldCacheHitCount = sharedHits; RecoveryQueryCount = recovery;
        }
    }
}
