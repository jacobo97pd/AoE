using System;

namespace Emberfield.Simulation
{
    /// <summary>Circle or axis-aligned footprint used for range; terrain does not occlude attacks.</summary>
    internal readonly struct TargetGeometry
    {
        internal readonly SimPoint Position;
        private readonly int radius;
        private readonly bool isBuilding;
        private readonly int left, right, bottom, top;

        internal TargetGeometry(UnitState unit)
        {
            Position = unit.Position; radius = unit.RadiusMillimetres; isBuilding = false;
            left = right = bottom = top = 0;
        }

        /// <summary>A point on the ground, such as the shore a ship lands its troops on.</summary>
        internal TargetGeometry(SimPoint point, int pointRadius)
        {
            Position = point; radius = pointRadius; isBuilding = false;
            left = right = bottom = top = 0;
        }

        internal TargetGeometry(BuildingState building, int cellSize)
        {
            Position = building.Position; radius = 0; isBuilding = true;
            left = building.Position.X - building.WidthCells * cellSize / 2;
            bottom = building.Position.Z - building.DepthCells * cellSize / 2;
            right = left + building.WidthCells * cellSize;
            top = bottom + building.DepthCells * cellSize;
        }

        private SimPoint ClosestPoint(SimPoint from) => isBuilding ?
            new SimPoint(Math.Max(left, Math.Min(from.X, right)), Math.Max(bottom, Math.Min(from.Z, top))) : Position;

        internal int EdgeDistance(SimPoint from, int fromRadius) =>
            Math.Max(0, SimPoint.DistanceCeiling(from, ClosestPoint(from)) - fromRadius - radius);

        internal bool InRange(SimPoint from, int fromRadius, int range)
        {
            var closest = ClosestPoint(from);
            long dx = (long)from.X - closest.X, dz = (long)from.Z - closest.Z;
            long reach = (long)fromRadius + radius + range;
            return dx * dx + dz * dz <= reach * reach;
        }

        // Every point InRange for this reach lies inside these bounds.
        internal void ReachBounds(int fromRadius, int range, out long left, out long bottom, out long right, out long top)
        {
            long reach = (long)fromRadius + radius + range;
            left = (isBuilding ? this.left : Position.X) - reach; right = (isBuilding ? this.right : Position.X) + reach;
            bottom = (isBuilding ? this.bottom : Position.Z) - reach; top = (isBuilding ? this.top : Position.Z) + reach;
        }

        internal SimPoint CandidateInCell(SimPoint center, int cellSize, int moverRadius)
        {
            int minX = center.X / cellSize * cellSize + moverRadius;
            int minZ = center.Z / cellSize * cellSize + moverRadius;
            int maxX = center.X / cellSize * cellSize + cellSize - moverRadius;
            int maxZ = center.Z / cellSize * cellSize + cellSize - moverRadius;
            return new SimPoint(Math.Max(minX, Math.Min(Position.X, maxX)), Math.Max(minZ, Math.Min(Position.Z, maxZ)));
        }
    }

    internal readonly struct CombatTarget
    {
        internal readonly UnitState Unit;
        internal readonly BuildingState Building;
        internal readonly TargetGeometry Geometry;
        internal int Id => Unit != null ? Unit.Id : Building.Id;
        internal int OwnerId => Unit != null ? Unit.OwnerId : Building.OwnerId;
        internal CombatTags Tags => Unit != null ? Unit.Tags : Building.Tags;
        internal int Armor => Unit != null ? Unit.Armor : Building.Armor;
        internal CombatTarget(UnitState unit) { Unit = unit; Building = null; Geometry = new TargetGeometry(unit); }
        internal CombatTarget(BuildingState building, int cellSize) { Unit = null; Building = building; Geometry = new TargetGeometry(building, cellSize); }
    }
}
