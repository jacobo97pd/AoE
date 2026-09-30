using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public enum MovementFormation { Loose, Line, Box }

    /// <summary>Reserves legal group destinations independently of route finding and movement.</summary>
    internal sealed class FormationPlanner
    {
        private readonly World world;
        private readonly UnitSpatialGrid bodies, goals;
        private readonly List<int> neighbours = new List<int>(64);
        // Scratch refilled by every plan, so a whole-army order allocates only its destinations.
        private readonly HashSet<int> selectedIds = new HashSet<int>();
        private readonly List<SimPoint> slots = new List<SimPoint>();
        private readonly List<int> order = new List<int>();
        internal FormationPlanner(World world)
        {
            this.world = world;
            bodies = new UnitSpatialGrid(world.mapWidth, world.mapHeight, world.cellSize);
            goals = new UnitSpatialGrid(world.mapWidth, world.mapHeight, world.cellSize);
        }

        private Navigation navigation;

        /// <summary>Plans on the grid the selection moves on, land or deep water.</summary>
        internal CommandResult Plan(Navigation grid, List<UnitState> selected, SimPoint target, MovementFormation formation,
            out SimPoint[] destinations, out int near)
        {
            navigation = grid;
            destinations = new SimPoint[selected.Count]; near = 2;
            selectedIds.Clear();
            long sumX = 0, sumZ = 0; int maxRadius = 0;
            foreach (var unit in selected)
            {
                if (!navigation.CanReach(unit.Position, target))
                    return CommandResult.Reject(CommandRejection.NoPath, "Every selected unit must be able to reach the destination.");
                selectedIds.Add(unit.Id); sumX += unit.Position.X; sumZ += unit.Position.Z;
                maxRadius = Math.Max(maxRadius, unit.RadiusMillimetres);
            }
            var center = new SimPoint((int)(sumX / selected.Count), (int)(sumZ / selected.Count));
            int spacing = Math.Max(world.cellSize, maxRadius * 4 + world.cellSize / 2);
            // Only enlarge a loose cohort when its current nearest pair needs more arrival clearance.
            int currentSpacing = spacing;
            bodies.Reset(world.units.Count);
            for (int i = 0; i < world.units.Count; i++)
            {
                var unit = world.units[i]; if (!selectedIds.Contains(unit.Id)) continue;
                bodies.Query(unit.Position, unit.Position, spacing, neighbours);
                foreach (int j in neighbours) currentSpacing = Math.Min(currentSpacing, SimPoint.DistanceCeiling(unit.Position, world.units[j].Position));
                bodies.Add(i, unit.Position, 0);
            }
            bodies.Reset(world.units.Count); goals.Reset(world.units.Count);
            for (int i = 0; i < world.units.Count; i++)
            {
                var unit = world.units[i]; if (selectedIds.Contains(unit.Id) || unit.WallId != 0) continue;
                if (unit.Order == UnitOrder.Idle) bodies.Add(i, unit.Position, unit.RadiusMillimetres);
                if (unit.Order == UnitOrder.Moving && unit.MoveGroupId != 0) goals.Add(i, unit.Destination, unit.RadiusMillimetres);
            }
            long dx = (long)target.X - center.X, dz = (long)target.Z - center.Z;
            if (dx == 0 && dz == 0) dx = 1;
            slots.Clear();
            if (formation == MovementFormation.Loose)
            {
                foreach (var unit in selected)
                {
                    if (!Point((long)target.X + ((long)unit.Position.X - center.X) * spacing / currentSpacing,
                        (long)target.Z + ((long)unit.Position.Z - center.Z) * spacing / currentSpacing, out var point)) break;
                    slots.Add(point);
                }
            }
            else
            {
                int columns = formation == MovementFormation.Line ? selected.Count : 1;
                if (formation == MovementFormation.Box) while (columns * columns < selected.Count) columns++;
                int rows = (selected.Count + columns - 1) / columns;
                bool alongX = Math.Abs(dx) >= Math.Abs(dz);
                int forward = alongX ? Math.Sign(dx) : Math.Sign(dz);
                for (int i = 0; i < selected.Count; i++)
                {
                    long lateral = (2L * (i % columns) - columns + 1) * spacing / 2;
                    long depth = (2L * (i / columns) - rows + 1) * spacing / 2;
                    if (!Point(alongX ? target.X + depth * forward : target.X - lateral * forward,
                        alongX ? target.Z + lateral * forward : target.Z + depth * forward, out var point)) break;
                    slots.Add(point);
                }
            }
            bool preferred = slots.Count == selected.Count;
            foreach (var point in slots)
                if (!Free(point, maxRadius, selected[0].Position)) { preferred = false; break; }
            if (!preferred)
            {
                slots.Clear();
                int maxRing = Math.Max(world.mapWidth, world.mapHeight);
                for (int ring = 0; ring < maxRing && slots.Count < selected.Count; ring++)
                for (int pass = 0; pass < (selected.Count == 1 ? 2 : 1) && slots.Count < selected.Count; pass++)
                for (int z = -ring; z <= ring && slots.Count < selected.Count; z++)
                for (int x = -ring; x <= ring && slots.Count < selected.Count; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) != ring) continue;
                    if (selected.Count == 1 && ((x != 0 && z != 0) != (pass == 1))) continue;
                    if (!Point((long)target.X + (long)x * spacing, (long)target.Z + (long)z * spacing, out var point)) continue;
                    if (Free(point, maxRadius, selected[0].Position)) slots.Add(point);
                }
                if (slots.Count != selected.Count)
                    return CommandResult.Reject(CommandRejection.DestinationBlocked, "The selection needs more free destination space.");
            }
            if (formation == MovementFormation.Loose && preferred)
            { for (int i = 0; i < selected.Count; i++) destinations[i] = slots[i]; }
            else
            {
                order.Clear(); for (int i = 0; i < selected.Count; i++) order.Add(i);
                order.Sort((a, b) => Compare(selected[a].Position, selected[b].Position, dx, dz, selected[a].Id, selected[b].Id));
                slots.Sort((a, b) => Compare(a, b, dx, dz, 0, 0));
                for (int i = 0; i < order.Count; i++) destinations[order[i]] = slots[i];
            }
            foreach (var point in destinations)
                near = Math.Max(near, (Math.Abs(point.X - target.X) + Math.Abs(point.Z - target.Z)) / world.cellSize + 2);
            return CommandResult.Success();
        }

        private bool Point(long x, long z, out SimPoint point)
        {
            point = default;
            if (x < 0 || z < 0 || x >= (long)world.mapWidth * world.cellSize || z >= (long)world.mapHeight * world.cellSize) return false;
            point = new SimPoint((int)x, (int)z); return true;
        }

        private static int Compare(SimPoint a, SimPoint b, long dx, long dz, int idA, int idB)
        {
            int result = (a.Z * dx - a.X * dz).CompareTo(b.Z * dx - b.X * dz);
            if (result != 0) return result;
            result = (a.X * dx + a.Z * dz).CompareTo(b.X * dx + b.Z * dz);
            return result != 0 ? result : idA.CompareTo(idB);
        }

        private bool Free(SimPoint point, int radius, SimPoint from)
        {
            if (!navigation.CanOccupy(point, radius) || !navigation.CanReach(from, point)) return false;
            bodies.Query(point, point, radius, neighbours);
            foreach (int i in neighbours)
                if (MovementGeometry.DiscsOverlap(point, radius, world.units[i].Position, world.units[i].RadiusMillimetres)) return false;
            goals.Query(point, point, radius, neighbours);
            foreach (int i in neighbours)
                if (MovementGeometry.DiscsOverlap(point, radius, world.units[i].Destination, world.units[i].RadiusMillimetres)) return false;
            return true;
        }
    }
}
