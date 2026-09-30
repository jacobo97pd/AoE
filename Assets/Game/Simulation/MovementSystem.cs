using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Integer, swept-disc movement with shared static routes and bounded local recovery.</summary>
    internal sealed class MovementSystem
    {
        private readonly World world;
        private List<UnitState> units => world.units;
        private readonly UnitSpatialGrid bodies;
        private readonly FormationPlanner formations;
        private readonly List<int> neighbours = new List<int>(64);
        private readonly List<int> recoveryNeighbours = new List<int>(64);
        private readonly Func<SimPoint, SimPoint, bool> recoveryClear;
        private int recoveryUnit, recoveries, nextGroup;
        private static readonly int[] Cos = { 866, 500, 0, -500, -866, -1000 };
        private static readonly int[] Sin = { 500, 866, 1000, 866, 500, 0 };

        internal MovementSystem(World world)
        {
            this.world = world;
            bodies = new UnitSpatialGrid(world.mapWidth, world.mapHeight, world.cellSize);
            formations = new FormationPlanner(world);
            recoveryClear = ClearRecovery;
            bodies.Reset(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i]; bodies.Query(unit.Position, unit.Position, unit.RadiusMillimetres, neighbours);
                foreach (int j in neighbours)
                    if (MovementGeometry.DiscsOverlap(unit.Position, unit.RadiusMillimetres, units[j].Position, units[j].RadiusMillimetres))
                        throw new ArgumentException("Initial unit bodies overlap.");
                bodies.Add(i, unit.Position, unit.RadiusMillimetres);
            }
        }

        internal CommandResult Submit(List<UnitState> selected, SimPoint target, MovementFormation formation = MovementFormation.Loose)
        {
            if (formation < MovementFormation.Loose || formation > MovementFormation.Box)
                return CommandResult.Reject(CommandRejection.InvalidCommand, "Movement formation is invalid.");
            // One grid for the whole selection: the world hands ships and troops in separately.
            var navigation = world.NavigationFor(selected[0]);
            if (!navigation.Contains(target)) return CommandResult.Reject(CommandRejection.InvalidTarget, "Destination is outside the map.");
            if (!navigation.IsWalkable(target))
                return CommandResult.Reject(CommandRejection.DestinationBlocked, selected[0].Domain == MovementDomain.Water
                    ? "Ships sail only on open water." : "Destination is occupied by terrain, a building, or resources.");
            foreach (var unit in selected)
                if (FactionSystem.MovementLocked(unit))
                    return CommandResult.Reject(CommandRejection.FactionActionBusy, "A selected unit is committed to deployment.");
            foreach (var unit in selected)
                if (!navigation.CanOccupy(target, unit.RadiusMillimetres))
                    return CommandResult.Reject(CommandRejection.DestinationBlocked, "Destination does not leave enough static clearance.");
            bool repeated = selected[0].MoveGroupId != 0 && selected[0].MoveGroupSize == selected.Count;
            foreach (var unit in selected)
                repeated &= unit.MoveGroupId == selected[0].MoveGroupId && unit.RequestedMoveTarget == target && unit.Formation == formation && unit.AttackTargetId == 0 && unit.WorkerTask == WorkerTask.None;
            if (repeated) return CommandResult.Success();
            var planResult = formations.Plan(navigation, selected, target, formation, out var destinations, out int near);
            if (!planResult.Accepted) return planResult;
            var paths = new List<SimPoint>[selected.Count];
            for (int i = 0; i < selected.Count; i++)
            {
                bool found = selected.Count >= 4
                    ? navigation.TryFindGroupPath(selected[i].Position, destinations[i], target, near, out paths[i])
                    : navigation.TryFindPath(selected[i].Position, destinations[i], out paths[i]);
                if (!found) return CommandResult.Reject(CommandRejection.NoPath, "Destination cannot be reached.");
            }
            nextGroup = nextGroup == int.MaxValue ? 1 : nextGroup + 1;
            for (int i = 0; i < selected.Count; i++)
            {
                var unit = selected[i]; CombatSystem.CancelAttack(unit, true); CancelWorkerTask(unit);
                SetRoute(unit, new RoutePlan(destinations[i], paths[i], 0));
                unit.MoveGroupId = nextGroup; unit.MoveGroupSize = selected.Count; unit.RequestedMoveTarget = target;
                unit.Formation = formation;
                unit.FlowAnchor = target; unit.FlowNearRadius = near;
            }
            return CommandResult.Success();
        }

        internal void Tick()
        {
            bodies.Reset(units.Count); recoveries = 0;
            for (int i = 0; i < units.Count; i++)
            { units[i].PreviousPosition = units[i].Position; if (units[i].WallId == 0) bodies.Add(i, units[i].Position, units[i].RadiusMillimetres); }
            for (int offset = 0; offset < units.Count; offset++)
            {
                int i = (offset + (int)(world.TickIndex % units.Count)) % units.Count;
                var unit = units[i];
                if (unit.Order != UnitOrder.Moving) continue;
                Advance(i, unit);
                bodies.AddSweep(i, unit.PreviousPosition, unit.Position, unit.RadiusMillimetres);
            }
        }

        // The grid of the unit Advance is moving, resolved once for all its clearance checks.
        private Navigation grid;

        private void Advance(int index, UnitState unit)
        {
            grid = world.NavigationFor(unit);
            if (unit.Position == unit.Destination && !unit.TemporaryRoute) { Halt(unit); return; }
            if (unit.Route == null || unit.RouteIndex >= unit.Route.Count)
            {
                if (world.TickIndex < unit.NextMovementRepathTick) return;
                unit.NextMovementRepathTick = world.TickIndex + 20;
                if (!Replan(unit)) { unit.MovementBlockedTicks++; return; }
            }
            long speed = (long)unit.MovementSpeed + unit.SpeedRemainder;
            int budget = (int)(speed / World.TickRate); unit.SpeedRemainder = (int)(speed % World.TickRate);
            if (budget == 0) return;
            // Keep a clear straight segment until it is reached or local steering changes its origin.
            // New segment selection uses bounded logarithmic lookahead, never a full scan of the remaining route.
            if (unit.SegmentLength == 0) SmoothSegment(index, unit);
            SimPoint waypoint = unit.Route[unit.RouteIndex];
            if (unit.SegmentLength == 0) unit.SegmentLength = SimPoint.DistanceCeiling(unit.SegmentStart, waypoint);
            int progress = Math.Min(unit.SegmentLength, unit.SegmentProgress + budget);
            SimPoint desired = unit.SegmentLength == 0 ? waypoint : new SimPoint(
                unit.SegmentStart.X + (int)(((long)waypoint.X - unit.SegmentStart.X) * progress / unit.SegmentLength),
                unit.SegmentStart.Z + (int)(((long)waypoint.Z - unit.SegmentStart.Z) * progress / unit.SegmentLength));
            bool rounded = SimPoint.DistanceCeiling(unit.Position, desired) > budget + 1;
            if (rounded) desired = MovementGeometry.Step(unit.Position, desired, budget);
            if (Clear(index, unit.Position, desired, out int blocker))
            {
                unit.Position = desired; unit.SegmentProgress = progress; unit.MovementBlockedTicks = 0;
                if (rounded) ResetSegment(unit);
                if (unit.Position == waypoint)
                {
                    unit.RouteIndex++; ResetSegment(unit);
                    if (unit.RouteIndex == unit.Route.Count)
                    { if (unit.Position == unit.Destination) Halt(unit); else { RouteLists.Return(unit.Route); unit.Route = null; unit.NextMovementRepathTick = 0; } }
                }
                return;
            }
            unit.MovementBlockedTicks++;
            if (unit.MovementBlockedTicks >= 8 && world.TickIndex >= unit.NextMovementRepathTick && recoveries < 4)
            {
                recoveries++; recoveryUnit = index; unit.NextMovementRepathTick = world.TickIndex + 20;
                if (grid.TryFindLocalDetour(unit.Position, unit.Destination, unit.RadiusMillimetres, recoveryClear, out var route))
                { RouteLists.Return(unit.Route); unit.Route = route.Path; unit.RouteIndex = 0; unit.TemporaryRoute = true; ResetSegment(unit); return; }
            }
            long dx = (long)waypoint.X - unit.Position.X, dz = (long)waypoint.Z - unit.Position.Z;
            int length = Math.Max(1, SimPoint.DistanceCeiling(unit.Position, waypoint));
            int hx = (int)(dx * 1000 / length), hz = (int)(dz * 1000 / length);
            bool yielding = unit.YieldUntilTick > world.TickIndex;
            if (yielding) { hx = unit.YieldHeading.X; hz = unit.YieldHeading.Z; }
            SimPoint best = unit.Position; long bestScore = long.MinValue;
            for (int angle = 0; angle < Cos.Length; angle++)
            for (int side = 1; side >= -1; side -= 2)
            {
                if (angle >= 3 && !yielding && unit.MovementBlockedTicks < 12) continue;
                int vx = (hx * Cos[angle] + hz * Sin[angle] * side) / 1000;
                int vz = (hz * Cos[angle] - hx * Sin[angle] * side) / 1000;
                var candidate = MovementGeometry.Step(unit.Position, new SimPoint(unit.Position.X + vx * 10, unit.Position.Z + vz * 10), budget);
                if (candidate == unit.Position || !Clear(index, unit.Position, candidate, out _)) continue;
                long score = ((long)candidate.X - unit.Position.X) * hx + ((long)candidate.Z - unit.Position.Z) * hz;
                score += side > 0 ? budget * 150L : 0;
                if (score > bestScore) { best = candidate; bestScore = score; }
            }
            if (best != unit.Position)
            { unit.Position = best; ResetSegment(unit); return; }
            // Preserve a persistent winner through a head-on jam; the other mover backs into staging space.
            if (!yielding && blocker >= 0 && units[blocker].Order == UnitOrder.Moving && unit.Id > units[blocker].Id && unit.MovementBlockedTicks >= 4)
            {
                unit.YieldToId = units[blocker].Id; unit.YieldUntilTick = world.TickIndex + 30;
                unit.YieldHeading = new SimPoint(-hx, -hz);
            }
        }

        private void SmoothSegment(int index, UnitState unit)
        {
            int first = unit.RouteIndex, last = unit.Route.Count - 1;
            if (first == last) return;
            recoveryUnit = index;
            int farthest = first;
            if (Visible(unit, last)) farthest = last;
            else
            {
                int upper = last, checks = 1;
                for (int stride = 1; first + stride < last && checks < 12; stride *= 2)
                {
                    int candidate = first + stride; checks++;
                    if (Visible(unit, candidate)) farthest = candidate;
                    else { upper = candidate; break; }
                }
                while (upper - farthest > 1 && checks++ < 12)
                {
                    int candidate = farthest + (upper - farthest) / 2;
                    if (Visible(unit, candidate)) farthest = candidate;
                    else upper = candidate;
                }
            }
            if (farthest != first) { unit.RouteIndex = farthest; ResetSegment(unit); }
        }

        private bool Visible(UnitState unit, int routeIndex) =>
            grid.CanTraverse(unit.Position, unit.Route[routeIndex], unit.RadiusMillimetres) &&
            (!unit.TemporaryRoute || ClearRecovery(unit.Position, unit.Route[routeIndex]));

        // Bodies of both grids share one index: a hull and a soldier are kept apart by the shore itself, each at least
        // its own radius from the other's ground.
        private bool Clear(int index, SimPoint from, SimPoint to, out int blocker)
        {
            blocker = -1; var unit = units[index];
            if (!grid.CanTraverse(from, to, unit.RadiusMillimetres)) return false;
            bodies.Query(from, to, unit.RadiusMillimetres, neighbours);
            foreach (int other in neighbours)
            {
                if (other == index) continue;
                var body = units[other];
                if (MovementGeometry.SweptDiscsOverlap(from, to, unit.RadiusMillimetres, body.PreviousPosition, body.Position, body.RadiusMillimetres))
                { blocker = other; return false; }
            }
            return true;
        }

        private bool ClearRecovery(SimPoint from, SimPoint to)
        {
            var unit = units[recoveryUnit]; bodies.Query(from, to, unit.RadiusMillimetres, recoveryNeighbours);
            foreach (int other in recoveryNeighbours)
                if (other != recoveryUnit && MovementGeometry.SweptDiscsOverlap(from, to, unit.RadiusMillimetres,
                    units[other].Position, units[other].Position, units[other].RadiusMillimetres)) return false;
            return true;
        }

        private bool Replan(UnitState unit)
        {
            var navigation = world.NavigationFor(unit);
            if (!navigation.CanOccupy(unit.Destination, unit.RadiusMillimetres)) return false;
            List<SimPoint> path;
            bool found = unit.MoveGroupSize >= 4
                ? navigation.TryFindGroupPath(unit.Position, unit.Destination, unit.FlowAnchor, unit.FlowNearRadius, out path)
                : navigation.TryFindPath(unit.Position, unit.Destination, out path);
            if (!found) return false;
            RouteLists.Return(unit.Route); unit.Route = path; unit.RouteIndex = 0; unit.TemporaryRoute = false; ResetSegment(unit); return true;
        }

        private static void ResetSegment(UnitState unit)
        { unit.SegmentStart = unit.Position; unit.SegmentLength = 0; unit.SegmentProgress = 0; }

        internal static void Halt(UnitState unit)
        {
            unit.Order = UnitOrder.Idle; unit.Destination = unit.Position; RouteLists.Return(unit.Route); unit.Route = null; unit.RouteIndex = 0;
            ResetSegment(unit); unit.SpeedRemainder = 0; unit.MovementBlockedTicks = 0; unit.TemporaryRoute = false;
            unit.YieldUntilTick = 0; unit.YieldToId = 0;
        }

        internal static void CancelWorkerTask(UnitState unit)
        {
            unit.WorkerTask = WorkerTask.None; unit.TargetResourceId = 0; unit.TargetBuildingId = 0;
            unit.GatherElapsed = 0; unit.RetryAtTick = 0; unit.MoveGroupId = 0; unit.MoveGroupSize = 0;
        }

        internal static void Stop(UnitState unit) { CombatSystem.CancelAttack(unit, false); Halt(unit); CancelWorkerTask(unit); }

        internal static void SetRoute(UnitState unit, RoutePlan plan)
        {
            // The route a unit drops is recycled; nothing else holds it.
            if (unit.Route != plan.Path) RouteLists.Return(unit.Route);
            unit.Destination = plan.Destination; unit.Route = plan.Path; unit.RouteIndex = 0;
            ResetSegment(unit); unit.Order = UnitOrder.Moving; unit.TemporaryRoute = false; unit.MovementBlockedTicks = 0;
            unit.MoveGroupId = 0; unit.MoveGroupSize = 0; unit.YieldUntilTick = 0; unit.YieldToId = 0;
        }

        internal void ReplanAll()
        {
            foreach (var unit in units)
            {
                // Only the land grid ever changes; deep water is fixed for the match.
                if (unit.Order != UnitOrder.Moving || unit.Domain == MovementDomain.Water) continue;
                if (!Replan(unit)) { RouteLists.Return(unit.Route); unit.Route = null; unit.NextMovementRepathTick = world.TickIndex + 20; }
            }
        }
    }
}
