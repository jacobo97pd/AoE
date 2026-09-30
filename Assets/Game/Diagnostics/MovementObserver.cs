using System;
using System.Collections.Generic;
using Emberfield.Simulation;

namespace Emberfield.Diagnostics
{
    [Serializable]
    public sealed class MovementObservation
    {
        public long ElapsedTicks;
        public int Arrived, Moving, Stalled, OverlapPairs, InvalidPositions, OutsideUnits, StaticInvalidUnits;
        public int MaxOverlapPairs, MaxInvalidPositions, MaxOutsideUnits, MaxStaticInvalidUnits, MaxStalledUnits, MaxPenetrationMillimetres;
        public long LongestOverlapTicks;
        public long AllArrivedAtTick = -1;
        public double ArrivalFraction;
    }

    /// <summary>Diagnostic checks run outside timed simulation work. Sample returns one reused mutable snapshot.</summary>
    public sealed class MovementObserver
    {
        public const int ArrivalToleranceMillimetres = 100;
        public const int ProgressToleranceMillimetres = 50;
        public const int StalledAfterTicks = 200;
        public const int PersistentOverlapTicks = 20;
        private readonly MovementScenario scenario;
        private readonly int[] heads, next, usedCells;
        private int usedCellCount;
        private readonly SimPoint[] progressAnchors;
        private readonly long[] progressTicks;
        private readonly Dictionary<long, PairSpan> overlaps = new Dictionary<long, PairSpan>();
        private readonly List<long> expiredPairs = new List<long>();
        private long previousSampleTick = -1;
        public MovementObservation Current { get; } = new MovementObservation();

        private struct PairSpan { internal long Started, LastSeen; }

        public MovementObserver(MovementScenario scenario)
        {
            this.scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            heads = new int[scenario.World.Map.WidthCells * scenario.World.Map.HeightCells];
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            next = new int[scenario.World.Units.Count]; usedCells = new int[next.Length];
            progressAnchors = new SimPoint[scenario.UnitIds.Count]; progressTicks = new long[progressAnchors.Length];
            for (int i = 0; i < progressAnchors.Length; i++)
                if (scenario.World.TryGetUnit(scenario.UnitIds[i], out var unit)) progressAnchors[i] = unit.Position;
        }

        public MovementObservation Sample()
        {
            long elapsed = scenario.ElapsedTicks;
            if (elapsed == previousSampleTick) return Current;
            var world = scenario.World;
            Current.ElapsedTicks = elapsed;
            Current.Arrived = Current.Moving = Current.Stalled = 0;
            Current.OverlapPairs = Current.InvalidPositions = Current.OutsideUnits = Current.StaticInvalidUnits = 0;
            for (int i = 0; i < scenario.UnitIds.Count; i++)
            {
                if (!world.TryGetUnit(scenario.UnitIds[i], out var unit)) continue;
                bool arrived = scenario.OrdersIssued && unit.Order == UnitOrder.Idle && scenario.ExpectedDestinations.TryGetValue(unit.Id, out var target) &&
                    DistanceSquared(unit.Position, target) <= (long)ArrivalToleranceMillimetres * ArrivalToleranceMillimetres;
                if (arrived) Current.Arrived++;
                if (unit.Order == UnitOrder.Moving) Current.Moving++;
                if (DistanceSquared(unit.Position, progressAnchors[i]) >= (long)ProgressToleranceMillimetres * ProgressToleranceMillimetres)
                { progressAnchors[i] = unit.Position; progressTicks[i] = elapsed; }
                if (scenario.OrdersIssued && !arrived && elapsed - progressTicks[i] >= StalledAfterTicks) Current.Stalled++;
            }
            Current.ArrivalFraction = scenario.UnitIds.Count == 0 ? 1 : Current.Arrived / (double)scenario.UnitIds.Count;
            if (scenario.OrdersIssued && Current.Arrived == scenario.UnitIds.Count && Current.AllArrivedAtTick < 0)
                Current.AllArrivedAtTick = elapsed;
            BuildSpatialGridAndCheckStatic();
            CountOverlaps(elapsed);
            Current.MaxOverlapPairs = Math.Max(Current.MaxOverlapPairs, Current.OverlapPairs);
            Current.MaxInvalidPositions = Math.Max(Current.MaxInvalidPositions, Current.InvalidPositions);
            Current.MaxOutsideUnits = Math.Max(Current.MaxOutsideUnits, Current.OutsideUnits);
            Current.MaxStaticInvalidUnits = Math.Max(Current.MaxStaticInvalidUnits, Current.StaticInvalidUnits);
            Current.MaxStalledUnits = Math.Max(Current.MaxStalledUnits, Current.Stalled);
            previousSampleTick = elapsed;
            return Current;
        }

        private void BuildSpatialGridAndCheckStatic()
        {
            for (int i = 0; i < usedCellCount; i++) heads[usedCells[i]] = -1;
            usedCellCount = 0;
            var world = scenario.World; int cellSize = world.Map.CellSizeMillimetres;
            int width = world.Map.WidthCells, height = world.Map.HeightCells;
            if (world.Units.Count > next.Length) throw new InvalidOperationException("Create a fresh observer after changing the diagnostic unit population.");
            for (int i = 0; i < world.Units.Count; i++)
            {
                var unit = world.Units[i]; var p = unit.Position; int radius = unit.RadiusMillimetres;
                bool outside = p.X < radius || p.Z < radius || p.X > width * cellSize - radius || p.Z > height * cellSize - radius;
                bool invalidStatic = !outside && IntersectsStatic(unit);
                if (outside) Current.OutsideUnits++;
                if (invalidStatic) Current.StaticInvalidUnits++;
                if (outside || invalidStatic) Current.InvalidPositions++;
                int x = p.X / cellSize, z = p.Z / cellSize;
                if (p.X < 0 || p.Z < 0 || x >= width || z >= height) { next[i] = -1; continue; }
                int key = z * width + x;
                if (heads[key] == -1) usedCells[usedCellCount++] = key;
                next[i] = heads[key]; heads[key] = i;
            }
        }

        private bool IntersectsStatic(UnitState unit)
        {
            var world = scenario.World; int cell = world.Map.CellSizeMillimetres; var p = unit.Position; int radius = unit.RadiusMillimetres;
            int minX = Math.Max(0, (p.X - radius) / cell), maxX = Math.Min(world.Map.WidthCells - 1, (p.X + radius) / cell);
            int minZ = Math.Max(0, (p.Z - radius) / cell), maxZ = Math.Min(world.Map.HeightCells - 1, (p.Z + radius) / cell);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (world.IsWalkable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2))) continue;
                long dx = p.X - Math.Max(x * cell, Math.Min(p.X, (x + 1) * cell));
                long dz = p.Z - Math.Max(z * cell, Math.Min(p.Z, (z + 1) * cell));
                if (dx * dx + dz * dz < (long)radius * radius) return true;
            }
            return false;
        }

        private void CountOverlaps(long elapsed)
        {
            var world = scenario.World; int width = world.Map.WidthCells, height = world.Map.HeightCells, cell = world.Map.CellSizeMillimetres;
            for (int i = 0; i < world.Units.Count; i++)
            {
                var unit = world.Units[i]; int x = unit.Position.X / cell, z = unit.Position.Z / cell;
                if (unit.Position.X < 0 || unit.Position.Z < 0 || x >= width || z >= height) continue;
                // Fixtures use radius300 < half a 1m cell; adjacent grid buckets cover every possible overlap.
                for (int nz = Math.Max(0, z - 1); nz <= Math.Min(height - 1, z + 1); nz++)
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                for (int j = heads[nz * width + nx]; j >= 0; j = next[j])
                {
                    if (j <= i) continue;
                    var other = world.Units[j]; int separation = unit.RadiusMillimetres + other.RadiusMillimetres;
                    if (DistanceSquared(unit.Position, other.Position) >= (long)(separation - 1) * (separation - 1)) continue;
                    Current.OverlapPairs++;
                    Current.MaxPenetrationMillimetres = Math.Max(Current.MaxPenetrationMillimetres, separation - (int)Math.Ceiling(Math.Sqrt(DistanceSquared(unit.Position, other.Position))));
                    int low = Math.Min(unit.Id, other.Id), high = Math.Max(unit.Id, other.Id);
                    long key = ((long)low << 32) | (uint)high;
                    if (!overlaps.TryGetValue(key, out var span) || span.LastSeen != previousSampleTick) span.Started = elapsed;
                    span.LastSeen = elapsed; overlaps[key] = span;
                    Current.LongestOverlapTicks = Math.Max(Current.LongestOverlapTicks, elapsed - span.Started + 1);
                }
            }
            expiredPairs.Clear();
            foreach (var pair in overlaps) if (pair.Value.LastSeen != elapsed) expiredPairs.Add(pair.Key);
            foreach (long key in expiredPairs) overlaps.Remove(key);
        }

        private static long DistanceSquared(SimPoint a, SimPoint b)
        { long dx = (long)a.X - b.X, dz = (long)a.Z - b.Z; return dx * dx + dz * dz; }
    }
}
