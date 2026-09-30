using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Search state kept between queries, so a repeated search is resumed and its storage reused, never redone.</summary>
    internal sealed partial class Navigation
    {
        // A breadth-first search from one cell grows the same tree whatever it is looking for, so a
        // search that stopped at one destination resumes for the next instead of starting again. A
        // cell keeps the predecessor it was first reached from, so every path is the one a fresh
        // search returns. One tree per travel order (TravelOrder's signs); a new start cell restarts
        // its tree and any change to the blocked grid forgets them all. Workers weighing every free
        // cell around a resource or drop-off then search once instead of once per cell.
        private sealed class PathTree
        {
            internal int First = -1, Read, Write, Stamp;
            internal readonly int[] Queue, Visited, Predecessor;
            internal PathTree(int count) { Queue = new int[count]; Visited = new int[count]; Predecessor = new int[count]; }
        }
        private readonly PathTree[] pathTrees = new PathTree[4];
        private readonly List<int[]> spareFields = new List<int[]>(4);
        private Navigation preview;
        // The last staircase found: its cells from the start, and how many steps each has tried.
        private int[] stairs;
        private byte[] stairSteps;
        private int stairCount;

        private static int TreeSlot(int sx, int sz) => (sx > 0 ? 0 : 1) + (sz > 0 ? 0 : 2);

        private PathTree PathTreeFrom(int first, int sx, int sz)
        {
            int slot = TreeSlot(sx, sz);
            var tree = pathTrees[slot] ?? (pathTrees[slot] = new PathTree(blocked.Length));
            if (tree.First == first) return tree;
            if (tree.Stamp == int.MaxValue) { Array.Clear(tree.Visited, 0, tree.Visited.Length); tree.Stamp = 0; }
            tree.Stamp++; tree.First = first; tree.Read = 0; tree.Write = 1;
            tree.Queue[0] = first; tree.Visited[first] = tree.Stamp; tree.Predecessor[first] = -1;
            return tree;
        }

        // Grows the tree until it reaches the cell, expanding neighbours towards the destination first.
        // The four visits are written out: Mono does not inline a call with this many arguments.
        private bool GrowPathTree(PathTree tree, int last, int sx, int sz, ref int visitedCells)
        {
            int[] queue = tree.Queue, visited = tree.Visited, predecessor = tree.Predecessor;
            bool[] walls = blocked;
            int stamp = tree.Stamp, read = tree.Read, write = tree.Write, row = sz * width;
            while (read < write && visited[last] != stamp)
            {
                int current = queue[read++];
                int z = current / width, x = current - z * width, cell;
                if (sx > 0 ? x + 1 < width : x > 0)
                { cell = current + sx; if (!walls[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
                if (sz > 0 ? z + 1 < height : z > 0)
                { cell = current + row; if (!walls[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
                if (sx > 0 ? x > 0 : x + 1 < width)
                { cell = current - sx; if (!walls[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
                if (sz > 0 ? z > 0 : z + 1 < height)
                { cell = current - row; if (!walls[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
            }
            visitedCells = read - tree.Read;
            tree.Read = read; tree.Write = write;
            return visited[last] == stamp;
        }

        private List<SimPoint> TreePath(PathTree tree, int last, SimPoint destination)
        {
            var path = RouteLists.Rent();
            for (int cell = last; cell != tree.First; cell = tree.Predecessor[cell]) path.Add(Centre(cell));
            path.Reverse();
            if (path[path.Count - 1] != destination) path.Add(destination);
            return path;
        }

        // TreePath's route summed as a mover walks it: start to the first cell centre, a cell per step,
        // then on to the destination if it is not the last centre.
        private int TreePathLength(PathTree tree, int last, SimPoint start, SimPoint destination)
        {
            int steps = 0, cell = last;
            while (tree.Predecessor[cell] != tree.First) { cell = tree.Predecessor[cell]; steps++; }
            return RouteLength(start, cell, steps, last, destination);
        }

        private int RouteLength(SimPoint start, int second, int steps, int last, SimPoint destination)
        {
            var end = Centre(last);
            int length = SimPoint.DistanceCeiling(start, Centre(second)) + steps * cellSize;
            return end == destination ? length : length + SimPoint.DistanceCeiling(end, destination);
        }

        private SimPoint Centre(int cell) => new SimPoint(cell % width * cellSize + cellSize / 2, cell / width * cellSize + cellSize / 2);

        // The search expands a cell's neighbours in a fixed order, so the first route to reach a cell,
        // the one it keeps, is the shortest route whose steps, read from the start, come earliest in
        // that order. When a shortest route only ever steps towards the destination (a staircase
        // inside the two cells' box), every shortest route does, and the search's is the staircase
        // that takes the earlier-expanded of its two steps wherever that can still arrive. A
        // depth-first walk trying that step first finds it without flooding the diamond around the
        // start; a cell that cannot arrive is marked and never tried again. Without a staircase, or
        // when the start's tree already reaches the cell, the search answers as before.
        private bool Staircase(int first, int last, int sx, int sz, ref int visitedCells)
        {
            var tree = pathTrees[TreeSlot(sx, sz)];
            if (tree != null && tree.First == first && tree.Visited[last] == tree.Stamp) return false;
            if (stairs == null) { stairs = new int[width + height]; stairSteps = new byte[width + height]; }
            int firstZ = first / width, firstX = first - firstZ * width, lastZ = last / width, lastX = last - lastZ * width;
            int alongX = lastX >= firstX ? 1 : -1, alongZ = lastZ >= firstZ ? 1 : -1;
            int remainingX = Math.Abs(lastX - firstX), remainingZ = Math.Abs(lastZ - firstZ);
            // The search expands +sx, +sz, -sx, -sz in that order.
            bool xFirst = (alongX == sx ? 0 : 2) < (alongZ == sz ? 1 : 3);
            int stepX = alongX, stepZ = alongZ * width;
            // Dead cells take a fresh stamp of the attack search's visited marks; each search takes its own.
            if (searchStamp == int.MaxValue) { Array.Clear(visited, 0, visited.Length); searchStamp = 0; }
            int stamp = ++searchStamp, count = 1, pushed = 1;
            stairs[0] = first; stairSteps[0] = 0;
            while (count > 0)
            {
                if (remainingX == 0 && remainingZ == 0) { stairCount = count; visitedCells += pushed; return true; }
                int top = count - 1, tried = stairSteps[top];
                if (tried < 2)
                {
                    stairSteps[top] = (byte)(tried + 1);
                    bool x = (tried == 0) == xFirst;
                    if (x ? remainingX == 0 : remainingZ == 0) continue;
                    int next = stairs[top] + (x ? stepX : stepZ);
                    if (blocked[next] || visited[next] == stamp) continue;
                    if (x) remainingX--; else remainingZ--;
                    stairs[count] = next; stairSteps[count] = 0; count++; pushed++;
                    continue;
                }
                // Neither step from here arrives: forget the cell and undo the step that reached it.
                visited[stairs[top]] = stamp; count--;
                if (count > 0) { if ((stairSteps[count - 1] == 1) == xFirst) remainingX++; else remainingZ++; }
            }
            visitedCells += pushed;
            return false;
        }

        private List<SimPoint> StaircasePath(SimPoint destination)
        {
            var path = RouteLists.Rent();
            for (int i = 1; i < stairCount; i++) path.Add(Centre(stairs[i]));
            if (path[path.Count - 1] != destination) path.Add(destination);
            return path;
        }

        private int StaircaseLength(SimPoint start, SimPoint destination) =>
            RouteLength(start, stairs[1], stairCount - 2, stairs[stairCount - 1], destination);

        private void ForgetPathTrees()
        {
            foreach (var tree in pathTrees) if (tree != null) tree.First = -1;
        }

        // Flow fields are whole-map arrays; evicted or invalidated ones are refilled instead of reallocated.
        private FlowField NewFlowField(int goal)
        {
            int[] distances;
            if (spareFields.Count == 0) distances = new int[blocked.Length];
            else { distances = spareFields[spareFields.Count - 1]; spareFields.RemoveAt(spareFields.Count - 1); }
            return new FlowField(goal, distances);
        }

        private void ClearFlowFields()
        {
            foreach (var field in flowFields) spareFields.Add(field.Distances);
            flowFields.Clear();
        }

        // A long diagonal sweep crosses a small part of its bounding box. The disc can touch a cell of
        // row z only where the segment passes within a radius of that row, so the row is narrowed to
        // the segment's x extent over that band, widened by the radius and a cell of margin for the
        // integer rounding. Every cell the swept disc can touch is still tested; the answer is unchanged.
        private void SweptRow(SimPoint from, SimPoint to, int radius, int z, ref int left, ref int right)
        {
            long low = Math.Max((long)z * cellSize - radius, Math.Min(from.Z, to.Z));
            long high = Math.Min((long)(z + 1) * cellSize + radius, Math.Max(from.Z, to.Z));
            if (low > high) { left = 0; right = -1; return; }
            long a = from.X, b = to.X;
            if (to.Z != from.Z)
            {
                long dx = (long)to.X - from.X, dz = (long)to.Z - from.Z;
                a = from.X + dx * (low - from.Z) / dz; b = from.X + dx * (high - from.Z) / dz;
            }
            left = (int)Math.Max(left, (Math.Min(a, b) - radius) / cellSize - 1);
            right = (int)Math.Min(right, (Math.Max(a, b) + radius) / cellSize + 1);
        }

        // Placement checks run often and never overlap, so one preview grid per world is refilled for each.
        private Navigation PreviewGrid() => preview ?? (preview = new Navigation(width, height, cellSize, metrics));
    }
}
