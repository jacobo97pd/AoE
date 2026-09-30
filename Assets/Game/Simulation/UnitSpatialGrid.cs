using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Reusable uniform-grid broad phase; old and committed swept bounds share the same index.</summary>
    internal sealed class UnitSpatialGrid
    {
        private readonly int width, height, cellSize;
        private readonly int[] heads;
        private int[] entryUnits = new int[256], entryNext = new int[256], seen = new int[64];
        private int count, stamp;

        internal UnitSpatialGrid(int width, int height, int cellSize)
        { this.width = width; this.height = height; this.cellSize = cellSize; heads = new int[width * height]; }

        internal void Reset(int unitCount)
        {
            Array.Clear(heads, 0, heads.Length); count = 0;
            if (seen.Length < unitCount) Array.Resize(ref seen, Math.Max(unitCount, seen.Length * 2));
        }

        internal void Add(int unit, SimPoint point, int radius) => AddSweep(unit, point, point, radius);
        internal void AddSweep(int unit, SimPoint from, SimPoint to, int radius)
        {
            int minX = Math.Max(0, (Math.Min(from.X, to.X) - radius) / cellSize);
            int maxX = Math.Min(width - 1, (Math.Max(from.X, to.X) + radius) / cellSize);
            int minZ = Math.Max(0, (Math.Min(from.Z, to.Z) - radius) / cellSize);
            int maxZ = Math.Min(height - 1, (Math.Max(from.Z, to.Z) + radius) / cellSize);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (count == entryUnits.Length)
                { Array.Resize(ref entryUnits, count * 2); Array.Resize(ref entryNext, count * 2); }
                int cell = z * width + x;
                entryUnits[count] = unit; entryNext[count] = heads[cell]; heads[cell] = ++count;
            }
        }

        internal void Query(SimPoint from, SimPoint to, int radius, List<int> result)
        {
            result.Clear();
            if (stamp == int.MaxValue) { Array.Clear(seen, 0, seen.Length); stamp = 0; }
            stamp++;
            int minX = Math.Max(0, (Math.Min(from.X, to.X) - radius) / cellSize);
            int maxX = Math.Min(width - 1, (Math.Max(from.X, to.X) + radius) / cellSize);
            int minZ = Math.Max(0, (Math.Min(from.Z, to.Z) - radius) / cellSize);
            int maxZ = Math.Min(height - 1, (Math.Max(from.Z, to.Z) + radius) / cellSize);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            for (int entry = heads[z * width + x] - 1; entry >= 0; entry = entryNext[entry] - 1)
            {
                int unit = entryUnits[entry];
                if (seen[unit] == stamp) continue;
                seen[unit] = stamp; result.Add(unit);
            }
        }
    }
}
