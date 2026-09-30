using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    internal readonly struct GridFootprint
    {
        internal readonly int X, Z, Width, Depth;
        internal GridFootprint(int x, int z, int width, int depth) { X = x; Z = z; Width = width; Depth = depth; }
        internal static GridFootprint Building(BuildingState building, int cellSize) =>
            new GridFootprint((building.Position.X - building.WidthCells * cellSize / 2) / cellSize,
                (building.Position.Z - building.DepthCells * cellSize / 2) / cellSize, building.WidthCells, building.DepthCells);
        internal static GridFootprint Resource(ResourceNodeState resource, int cellSize) =>
            new GridFootprint(resource.Position.X / cellSize, resource.Position.Z / cellSize, 1, 1);
        internal bool IsAdjacent(SimPoint position, int cellSize)
        {
            int x = position.X / cellSize, z = position.Z / cellSize;
            return ((x == X - 1 || x == X + Width) && z >= Z && z < Z + Depth) ||
                   ((z == Z - 1 || z == Z + Depth) && x >= X && x < X + Width);
        }

        internal bool CanInteract(SimPoint position, int cellSize)
        {
            if (!IsAdjacent(position, cellSize)) return false;
            long dx = position.X - Math.Max(X * cellSize, Math.Min(position.X, (X + Width) * cellSize));
            long dz = position.Z - Math.Max(Z * cellSize, Math.Min(position.Z, (Z + Depth) * cellSize));
            return dx * dx + dz * dz <= (long)(cellSize / 2) * (cellSize / 2);
        }
    }

    internal readonly struct RoutePlan
    {
        internal readonly SimPoint Destination;
        internal readonly List<SimPoint> Path;
        internal readonly int Length;
        internal RoutePlan(SimPoint destination, List<SimPoint> path, int length) { Destination = destination; Path = path; Length = length; }
    }

    internal static class InteractionSearch
    {
        internal static bool Approach(World world, Navigation navigation, SimPoint from, int radius, GridFootprint footprint, out RoutePlan route)
        {
            bool found = Measure(world, navigation, from, radius, footprint, out route);
            if (found) Build(navigation, from, ref route);
            return found;
        }

        // The nearest free cell beside the footprint, compared by route length alone; the route itself is not built.
        private static bool Measure(World world, Navigation navigation, SimPoint from, int radius, GridFootprint footprint, out RoutePlan route)
        {
            bool found = false;
            route = default;
            var bodies = NearbyBodies(world, from, radius, footprint);
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
            {
                Consider(world, navigation, bodies, from, radius, x, footprint.Z - 1, ref found, ref route);
                Consider(world, navigation, bodies, from, radius, x, footprint.Z + footprint.Depth, ref found, ref route);
            }
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            {
                Consider(world, navigation, bodies, from, radius, footprint.X - 1, z, ref found, ref route);
                Consider(world, navigation, bodies, from, radius, footprint.X + footprint.Width, z, ref found, ref route);
            }
            return found;
        }

        // The idle bodies, other than the unit's own, that could overlap a cell beside the footprint:
        // the units are scanned once per footprint instead of once per cell. A body left out is at
        // least its radius plus the mover's from every such cell's centre, so it overlaps none of them.
        [ThreadStatic] private static List<UnitState> nearbyBodies;

        private static List<UnitState> NearbyBodies(World world, SimPoint from, int radius, GridFootprint footprint)
        {
            var bodies = nearbyBodies ?? (nearbyBodies = new List<UnitState>(32));
            bodies.Clear();
            long size = world.cellSize, half = world.cellSize / 2;
            long left = (footprint.X - 1) * size + half, right = (footprint.X + footprint.Width) * size + half;
            long bottom = (footprint.Z - 1) * size + half, top = (footprint.Z + footprint.Depth) * size + half;
            foreach (var body in world.units)
            {
                if (body.Position == from || body.Order != UnitOrder.Idle) continue;
                long reach = (long)radius + body.RadiusMillimetres;
                if (body.Position.X <= left - reach || body.Position.X >= right + reach ||
                    body.Position.Z <= bottom - reach || body.Position.Z >= top + reach) continue;
                bodies.Add(body);
            }
            return bodies;
        }

        // Only the chosen cell's route is built; the search that measured it already reached it.
        private static void Build(Navigation navigation, SimPoint from, ref RoutePlan route)
        {
            if (navigation.TryFindPath(from, route.Destination, out var path)) route = new RoutePlan(route.Destination, path, route.Length);
        }

        private static void Consider(World world, Navigation navigation, List<UnitState> bodies, SimPoint from, int radius, int x, int z, ref bool found, ref RoutePlan best)
        {
            if (x < 0 || z < 0 || x >= world.mapWidth || z >= world.mapHeight) return;
            var target = new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2);
            foreach (var body in bodies)
                if (body.Position != from && body.Order == UnitOrder.Idle &&
                    MovementGeometry.DiscsOverlap(target, radius, body.Position, body.RadiusMillimetres)) return;
            if (!navigation.CanOccupy(target, radius) || !navigation.TryFindPathLength(from, target, out int length)) return;
            // Equal routes prefer the cell nearer the unit, then the map centre, rather than the scan
            // order, so mirrored positions on a mirrored map choose mirrored cells.
            if (found && (length > best.Length || length == best.Length && !Nearer(world, target, best.Destination, from))) return;
            found = true;
            best = new RoutePlan(target, null, length);
        }

        private static bool Nearer(World world, SimPoint candidate, SimPoint current, SimPoint from)
        {
            long a = DistanceSquared(candidate, from), b = DistanceSquared(current, from);
            if (a != b) return a < b;
            var centre = Centre(world);
            return DistanceSquared(candidate, centre) < DistanceSquared(current, centre);
        }
        private static SimPoint Centre(World world) => new SimPoint(world.mapWidth * world.cellSize / 2, world.mapHeight * world.cellSize / 2);
        private static long DistanceSquared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }

        internal static bool DropOff(World world, UnitState unit, out BuildingState building, out RoutePlan route)
        {
            building = null; route = default;
            foreach (var candidate in world.buildings)
            {
                if (candidate.OwnerId != unit.OwnerId || !candidate.IsOperational || !world.buildingDefinitions[candidate.DefinitionId].CanDropOff) continue;
                if (!Measure(world, world.navigation, unit.Position, unit.RadiusMillimetres, GridFootprint.Building(candidate, world.cellSize), out var plan)) continue;
                if (building != null && plan.Length >= route.Length) continue;
                building = candidate; route = plan;
            }
            if (building != null) Build(world.navigation, unit.Position, ref route);
            return building != null;
        }

        // A recruit leaves by the free edge cell nearest the rally point, or the map centre without
        // one, so it steps out the way it will walk and mirrored bases send recruits out alike.
        // A ship is launched the same way, onto the water beside its dock: the grid is the recruit's own.
        internal static bool SpawnPoint(World world, Navigation navigation, BuildingState building, int radius, out SimPoint point)
        {
            var footprint = GridFootprint.Building(building, world.cellSize);
            var exit = building.HasRallyPoint ? building.RallyPoint : Centre(world);
            bool found = false; long nearest = long.MaxValue; SimPoint chosen = default;
            void Consider(int x, int z)
            {
                if (!SpawnCandidate(world, navigation, radius, x, z, out var candidate)) return;
                long distance = DistanceSquared(candidate, exit);
                if (found && distance >= nearest) return;
                found = true; nearest = distance; chosen = candidate;
            }
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++) { Consider(x, footprint.Z - 1); Consider(x, footprint.Z + footprint.Depth); }
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++) { Consider(footprint.X - 1, z); Consider(footprint.X + footprint.Width, z); }
            point = chosen;
            return found;
        }

        private static bool SpawnCandidate(World world, Navigation navigation, int radius, int x, int z, out SimPoint point)
        {
            point = new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2);
            if (x < 0 || z < 0 || x >= world.mapWidth || z >= world.mapHeight || !navigation.CanOccupy(point, radius)) return false;
            foreach (var unit in world.units)
            {
                if (MovementGeometry.SweptDiscsOverlap(point, point, radius, unit.PreviousPosition, unit.Position, unit.RadiusMillimetres)) return false;
            }
            return true;
        }
    }
}
