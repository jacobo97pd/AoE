using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Deterministic four-neighbour grid navigation. Scratch storage is reused between orders.</summary>
    internal sealed partial class Navigation
    {
        private readonly int width;
        private readonly int height;
        private readonly int cellSize;
        private readonly bool[] blocked;
        private readonly int[] queue;
        private readonly int[] predecessor;
        private readonly int[] visited;
        private readonly int[] components;
        private readonly NavigationMetrics metrics;
        private int searchStamp;
        private readonly int[] localQueue = new int[625], localParents = new int[625], localVisited = new int[625];
        private int localStamp;
        private readonly List<FlowField> flowFields = new List<FlowField>(4);

        private sealed class FlowField
        {
            internal readonly int Goal;
            internal readonly int[] Distances;
            internal FlowField(int goal, int[] distances) { Goal = goal; Distances = distances; }
        }

        internal Navigation(int width, int height, int cellSize, NavigationMetrics metrics)
        {
            this.width = width; this.height = height; this.cellSize = cellSize;
            this.metrics = metrics;
            int count = width * height;
            blocked = new bool[count]; queue = new int[count]; predecessor = new int[count];
            visited = new int[count]; components = new int[count];
        }

        internal int CellIndex(SimPoint point) => point.Z / cellSize * width + point.X / cellSize;
        internal bool Contains(SimPoint point) => point.X >= 0 && point.Z >= 0 && point.X < width * cellSize && point.Z < height * cellSize;
        internal bool IsWalkable(SimPoint point) => Contains(point) && !blocked[CellIndex(point)];

        internal bool CanTraverse(SimPoint from, SimPoint to, int radius)
        {
            if (!CanOccupy(to, radius)) return false;
            int minX = Math.Max(0, (Math.Min(from.X, to.X) - radius) / cellSize);
            int maxX = Math.Min(width - 1, (Math.Max(from.X, to.X) + radius) / cellSize);
            int minZ = Math.Max(0, (Math.Min(from.Z, to.Z) - radius) / cellSize);
            int maxZ = Math.Min(height - 1, (Math.Max(from.Z, to.Z) + radius) / cellSize);
            bool band = (maxX - minX + 1) * (maxZ - minZ + 1) > 16;
            for (int z = minZ; z <= maxZ; z++)
            {
                int left = minX, right = maxX;
                if (band) SweptRow(from, to, radius, z, ref left, ref right);
                for (int x = left; x <= right; x++)
                    if (blocked[z * width + x] && MovementGeometry.SweptDiscRectangle(from, to, radius,
                        x * cellSize, z * cellSize, (x + 1) * cellSize, (z + 1) * cellSize)) return false;
            }
            return true;
        }

        internal bool CanOccupy(SimPoint point, int radius)
        {
            if (!IsWalkable(point) || point.X < radius || point.Z < radius ||
                point.X > width * cellSize - radius || point.Z > height * cellSize - radius) return false;
            int minX = Math.Max(0, (point.X - radius) / cellSize);
            int maxX = Math.Min(width - 1, (point.X + radius) / cellSize);
            int minZ = Math.Max(0, (point.Z - radius) / cellSize);
            int maxZ = Math.Min(height - 1, (point.Z + radius) / cellSize);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!blocked[z * width + x]) continue;
                int nearestX = Math.Max(x * cellSize, Math.Min(point.X, (x + 1) * cellSize));
                int nearestZ = Math.Max(z * cellSize, Math.Min(point.Z, (z + 1) * cellSize));
                long dx = point.X - nearestX;
                long dz = point.Z - nearestZ;
                if (dx * dx + dz * dz < (long)radius * radius) return false;
            }
            return true;
        }

        internal void Block(int x, int z)
        {
            if (x < 0 || z < 0 || x >= width || z >= height)
                throw new ArgumentException("An obstacle footprint is outside the map.");
            int index = z * width + x;
            if (blocked[index]) throw new ArgumentException("Obstacle footprints overlap.");
            blocked[index] = true;
        }

        internal void BuildConnectivity()
        {
            long started = metrics.Begin();
            int visitedCells = 0;
            try { BuildConnectivityCore(ref visitedCells); }
            finally { metrics.End(NavigationQueryKind.Connectivity, started, visitedCells); }
        }

        private void BuildConnectivityCore(ref int visitedCells)
        {
            ClearFlowFields(); ForgetPathTrees();
            Array.Clear(components, 0, components.Length);
            int component = 0;
            for (int start = 0; start < blocked.Length; start++)
            {
                if (blocked[start] || components[start] != 0) continue;
                component++;
                int read = 0, write = 0;
                queue[write++] = start;
                components[start] = component;
                while (read < write)
                {
                    int current = queue[read++];
                    int z = current / width, x = current - z * width, cell;
                    // Connect written out, in the same order: Mono does not inline it.
                    if (x + 1 < width) { cell = current + 1; if (!blocked[cell] && components[cell] == 0) { components[cell] = component; queue[write++] = cell; } }
                    if (z + 1 < height) { cell = current + width; if (!blocked[cell] && components[cell] == 0) { components[cell] = component; queue[write++] = cell; } }
                    if (x > 0) { cell = current - 1; if (!blocked[cell] && components[cell] == 0) { components[cell] = component; queue[write++] = cell; } }
                    if (z > 0) { cell = current - width; if (!blocked[cell] && components[cell] == 0) { components[cell] = component; queue[write++] = cell; } }
                }
                visitedCells += read;
            }
        }

        internal Navigation WithFootprint(GridFootprint footprint)
        {
            var result = PreviewGrid();
            Array.Copy(blocked, result.blocked, blocked.Length);
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++) result.Block(x, z);
            result.BuildConnectivity();
            return result;
        }

        internal void AddFootprint(GridFootprint footprint)
        {
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++) Block(x, z);
            BuildConnectivity();
        }

        internal void ClearResourceCell(SimPoint position)
        {
            blocked[CellIndex(position)] = false;
            BuildConnectivity();
        }

        internal void ClearFootprint(GridFootprint footprint)
        {
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++) blocked[z * width + x] = false;
            BuildConnectivity();
        }

        internal bool TryFindAttackRoute(SimPoint start, int moverRadius, TargetGeometry target, int range, out RoutePlan route)
        {
            long started = metrics.Begin();
            int visitedCells = 0;
            try { return TryFindAttackRouteCore(start, moverRadius, target, range, out route, ref visitedCells); }
            finally { metrics.End(NavigationQueryKind.Attack, started, visitedCells); }
        }

        private bool TryFindAttackRouteCore(SimPoint start, int moverRadius, TargetGeometry target, int range,
            out RoutePlan route, ref int visitedCells)
        {
            route = default;
            if (!CanOccupy(start, moverRadius)) return false;
            if (target.InRange(start, moverRadius, range))
            {
                var here = RouteLists.Rent(); here.Add(start);
                route = new RoutePlan(start, here, 0);
                return true;
            }
            if (searchStamp == int.MaxValue) { Array.Clear(visited, 0, visited.Length); searchStamp = 0; }
            searchStamp++;
            int first = CellIndex(start), read = 0, write = 0, last = -1, stamp = searchStamp;
            var destination = default(SimPoint);
            queue[write++] = first; visited[first] = searchStamp; predecessor[first] = -1;
            // Both points a cell offers (its centre and the in-cell point nearest the target) lie inside
            // the cell, so a cell wholly outside the target's reach cannot hold a firing position and
            // skips the range tests. The cells visited, their order and the cell found are unchanged.
            target.ReachBounds(moverRadius, range, out long reachLeft, out long reachBottom, out long reachRight, out long reachTop);
            bool bounded = 2L * moverRadius <= cellSize;
            while (read < write)
            {
                int current = queue[read++];
                int z = current / width, x = current - z * width;
                long cellLeft = (long)x * cellSize, cellBottom = (long)z * cellSize;
                if (!bounded || (cellLeft + cellSize >= reachLeft && cellLeft <= reachRight && cellBottom + cellSize >= reachBottom && cellBottom <= reachTop))
                {
                    var center = new SimPoint(x * cellSize + cellSize / 2, z * cellSize + cellSize / 2);
                    var candidate = target.InRange(center, moverRadius, range) ? center : target.CandidateInCell(center, cellSize, moverRadius);
                    if (target.InRange(candidate, moverRadius, range) && CanOccupy(candidate, moverRadius))
                    { last = current; destination = candidate; break; }
                }
                // Visit written out, in the same fixed order: Mono does not inline it.
                int cell;
                if (x + 1 < width) { cell = current + 1; if (!blocked[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
                if (z + 1 < height) { cell = current + width; if (!blocked[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
                if (x > 0) { cell = current - 1; if (!blocked[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
                if (z > 0) { cell = current - width; if (!blocked[cell] && visited[cell] != stamp) { visited[cell] = stamp; predecessor[cell] = current; queue[write++] = cell; } }
            }
            visitedCells = read;
            if (last < 0) return false;
            var path = RouteLists.Rent();
            for (int cell = last; cell != first; cell = predecessor[cell])
                path.Add(new SimPoint(cell % width * cellSize + cellSize / 2, cell / width * cellSize + cellSize / 2));
            path.Reverse();
            if (path.Count == 0 || path[path.Count - 1] != destination) path.Add(destination);
            route = new RoutePlan(destination, path, 0);
            return true;
        }

        internal bool CanReach(SimPoint start, SimPoint destination) =>
            IsWalkable(start) && IsWalkable(destination) && components[CellIndex(start)] == components[CellIndex(destination)];

        internal bool TryFindGroupPath(SimPoint start, SimPoint destination, SimPoint anchor, int nearRadius, out List<SimPoint> path)
        {
            long started = metrics.Begin();
            int visitedCells = 0;
            try { return FindGroupPathCore(start, destination, anchor, nearRadius, out path, ref visitedCells); }
            finally { metrics.End(NavigationQueryKind.Path, started, visitedCells); }
        }

        private bool FindGroupPathCore(SimPoint start, SimPoint destination, SimPoint anchor, int nearRadius,
            out List<SimPoint> path, ref int visitedCells)
        {
            path = null;
            if (!CanReach(start, destination)) return false;
            if (!CanReach(start, anchor)) return TryFindPathCore(start, destination, out path, ref visitedCells);
            int goal = CellIndex(anchor);
            FlowField field = null;
            for (int i = 0; i < flowFields.Count; i++)
                if (flowFields[i].Goal == goal)
                { field = flowFields[i]; flowFields.RemoveAt(i); flowFields.Insert(0, field); break; }
            metrics.SharedField(field != null);
            if (field == null)
            {
                field = NewFlowField(goal);
                for (int i = 0; i < field.Distances.Length; i++) field.Distances[i] = -1;
                int read = 0, write = 0;
                queue[write++] = goal; field.Distances[goal] = 0;
                var distances = field.Distances;
                while (read < write)
                {
                    int current = queue[read++], z = current / width, x = current - z * width, near, next = distances[current] + 1;
                    // FlowVisit written out, in the same order: Mono does not inline it.
                    if (x + 1 < width) { near = current + 1; if (!blocked[near] && distances[near] < 0) { distances[near] = next; queue[write++] = near; } }
                    if (z + 1 < height) { near = current + width; if (!blocked[near] && distances[near] < 0) { distances[near] = next; queue[write++] = near; } }
                    if (x > 0) { near = current - 1; if (!blocked[near] && distances[near] < 0) { distances[near] = next; queue[write++] = near; } }
                    if (z > 0) { near = current - width; if (!blocked[near] && distances[near] < 0) { distances[near] = next; queue[write++] = near; } }
                }
                visitedCells += read;
                if (flowFields.Count == 4) { spareFields.Add(flowFields[3].Distances); flowFields.RemoveAt(3); }
                flowFields.Insert(0, field);
            }
            path = RouteLists.Rent();
            int cell = CellIndex(start);
            SimPoint from = start;
            int cellZ = cell / width, cellX = cell - cellZ * width, goalZ = goal / width, goalX = goal - goalZ * width;
            TravelOrder(start, destination, out int sx, out int sz);
            while (Math.Abs(cellX - goalX) + Math.Abs(cellZ - goalZ) > nearRadius)
            {
                int best = -1; long bestDeviation = long.MaxValue;
                int x = cellX, z = cellZ;
                if (sx > 0 ? x + 1 < width : x > 0) ConsiderFlow(field, cell, cell + sx, start, destination, ref best, ref bestDeviation);
                if (sz > 0 ? z + 1 < height : z > 0) ConsiderFlow(field, cell, cell + sz * width, start, destination, ref best, ref bestDeviation);
                if (sx > 0 ? x > 0 : x + 1 < width) ConsiderFlow(field, cell, cell - sx, start, destination, ref best, ref bestDeviation);
                if (sz > 0 ? z > 0 : z + 1 < height) ConsiderFlow(field, cell, cell - sz * width, start, destination, ref best, ref bestDeviation);
                if (best < 0) { RouteLists.Return(path); path = null; return false; }
                cell = best; cellZ = cell / width; cellX = cell - cellZ * width;
                from = new SimPoint(cellX * cellSize + cellSize / 2, cellZ * cellSize + cellSize / 2);
                path.Add(from);
            }
            int tailVisited = 0;
            bool found = TryFindPathCore(from, destination, out var tail, ref tailVisited);
            visitedCells += tailVisited;
            if (!found) { RouteLists.Return(path); path = null; return false; }
            path.AddRange(tail);
            RouteLists.Return(tail);
            return true;
        }

        private void ConsiderFlow(FlowField field, int current, int candidate, SimPoint start, SimPoint destination,
            ref int best, ref long bestDeviation)
        {
            var distances = field.Distances;
            if (distances[candidate] != distances[current] - 1) return;
            int row = candidate / width;
            long x = (candidate - row * width) * cellSize + cellSize / 2 - start.X;
            long z = row * cellSize + cellSize / 2 - start.Z;
            long deviation = Math.Abs(x * ((long)destination.Z - start.Z) - z * ((long)destination.X - start.X));
            if (deviation >= bestDeviation) return;
            best = candidate; bestDeviation = deviation;
        }

        internal bool TryFindLocalDetour(SimPoint start, SimPoint destination, int radius,
            Func<SimPoint, SimPoint, bool> clearStep, out RoutePlan route)
        {
            long started = metrics.Begin();
            int visitedCells = 0;
            metrics.RecoveryQuery();
            try { return LocalDetourCore(start, destination, radius, clearStep, out route, ref visitedCells); }
            finally { metrics.End(NavigationQueryKind.Path, started, visitedCells); }
        }

        private bool LocalDetourCore(SimPoint start, SimPoint destination, int radius,
            Func<SimPoint, SimPoint, bool> clearStep, out RoutePlan route, ref int visitedCells)
        {
            route = default;
            if (localStamp == int.MaxValue) { Array.Clear(localVisited, 0, localVisited.Length); localStamp = 0; }
            localStamp++;
            int step = Math.Max(1, cellSize / 2);
            int originX = start.X / step - 12, originZ = start.Z / step - 12;
            const int first = 12 * 25 + 12;
            int read = 0, write = 0, best = first;
            long bestDistance = Squared(start, destination);
            localQueue[write++] = first; localVisited[first] = localStamp; localParents[first] = -1;
            TravelOrder(start, destination, out int sx, out int sz);
            while (read < write)
            {
                int current = localQueue[read++], x = current % 25, z = current / 25;
                var from = current == first ? start : new SimPoint((originX + x) * step, (originZ + z) * step);
                long distance = Squared(from, destination);
                if (distance < bestDistance) { bestDistance = distance; best = current; }
                if (distance <= (long)step * step * 2 && CanTraverse(from, destination, radius) && clearStep(from, destination))
                { best = current; break; }
                if (sx > 0 ? x < 24 : x > 0) DetourVisit(current + sx, current, from, radius, originX, originZ, step, clearStep, ref write);
                if (sz > 0 ? z < 24 : z > 0) DetourVisit(current + sz * 25, current, from, radius, originX, originZ, step, clearStep, ref write);
                if (sx > 0 ? x > 0 : x < 24) DetourVisit(current - sx, current, from, radius, originX, originZ, step, clearStep, ref write);
                if (sz > 0 ? z > 0 : z < 24) DetourVisit(current - sz * 25, current, from, radius, originX, originZ, step, clearStep, ref write);
            }
            visitedCells = read;
            if (best == first) return false;
            var path = RouteLists.Rent();
            for (int current = best; current != first; current = localParents[current])
                path.Add(new SimPoint((originX + current % 25) * step, (originZ + current / 25) * step));
            path.Reverse();
            var end = path[path.Count - 1];
            if (Squared(end, destination) <= (long)step * step * 2 && CanTraverse(end, destination, radius) && clearStep(end, destination))
            { path.Add(destination); end = destination; }
            route = new RoutePlan(end, path, 0);
            return true;
        }

        private void DetourVisit(int cell, int previous, SimPoint from, int radius, int originX, int originZ, int step,
            Func<SimPoint, SimPoint, bool> clearStep, ref int write)
        {
            if (localVisited[cell] == localStamp) return;
            var to = new SimPoint((originX + cell % 25) * step, (originZ + cell / 25) * step);
            if (!CanTraverse(from, to, radius) || !clearStep(from, to)) return;
            localVisited[cell] = localStamp; localParents[cell] = previous; localQueue[write++] = cell;
        }

        private static long Squared(SimPoint a, SimPoint b)
        { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }

        internal bool TryFindPath(SimPoint start, SimPoint destination, out List<SimPoint> path)
        {
            long started = metrics.Begin();
            int visitedCells = 0;
            try { return TryFindPathCore(start, destination, out path, ref visitedCells); }
            finally { metrics.End(NavigationQueryKind.Path, started, visitedCells); }
        }

        private bool TryFindPathCore(SimPoint start, SimPoint destination, out List<SimPoint> path, ref int visitedCells)
        {
            path = null;
            if (!CanReach(start, destination)) return false;
            int first = CellIndex(start), last = CellIndex(destination);
            if (first == last)
            {
                path = RouteLists.Rent(); path.Add(destination);
                return true;
            }
            TravelOrder(start, destination, out int sx, out int sz);
            if (Staircase(first, last, sx, sz, ref visitedCells)) { path = StaircasePath(destination); return true; }
            var tree = PathTreeFrom(first, sx, sz);
            if (!GrowPathTree(tree, last, sx, sz, ref visitedCells)) return false;
            path = TreePath(tree, last, destination);
            return true;
        }

        /// <summary>The length TryFindPath's route would measure from start, without building the route.</summary>
        internal bool TryFindPathLength(SimPoint start, SimPoint destination, out int length)
        {
            long started = metrics.Begin();
            int visitedCells = 0;
            length = 0;
            try
            {
                if (!CanReach(start, destination)) return false;
                int first = CellIndex(start), last = CellIndex(destination);
                if (first == last) { length = SimPoint.DistanceCeiling(start, destination); return true; }
                TravelOrder(start, destination, out int sx, out int sz);
                if (Staircase(first, last, sx, sz, ref visitedCells)) { length = StaircaseLength(start, destination); return true; }
                var tree = PathTreeFrom(first, sx, sz);
                if (!GrowPathTree(tree, last, sx, sz, ref visitedCells)) return false;
                length = TreePathLength(tree, last, start, destination);
                return true;
            }
            finally { metrics.End(NavigationQueryKind.Path, started, visitedCells); }
        }

        // Neighbours are expanded along the direction of travel (towards the destination first, then
        // away from it) instead of in fixed compass order. Among equally short routes a search and its
        // 180-degree rotation then pick rotated cells, so mirrored starts on a mirrored map move alike.
        // An axis-aligned trip breaks the tie towards the map centre.
        private void TravelOrder(SimPoint start, SimPoint destination, out int sx, out int sz)
        {
            long dx = (long)destination.X - start.X, dz = (long)destination.Z - start.Z;
            if (dx == 0) dx = (long)width * cellSize / 2 - start.X;
            if (dz == 0) dz = (long)height * cellSize / 2 - start.Z;
            sx = dx >= 0 ? 1 : -1; sz = dz >= 0 ? 1 : -1;
        }
    }
}
