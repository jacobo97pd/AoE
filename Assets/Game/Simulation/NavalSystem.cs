using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>
    /// Ships' cargo. Land units walk to a friendly ship lying alongside a shore they can reach, climb aboard over a
    /// second and leave the world while carried; a ship sails within reach of a shore point and lands them on the
    /// clear ground there. A sunk ship drowns everything aboard (World.DestroyUnit).
    /// </summary>
    internal sealed class NavalSystem
    {
        /// <summary>Edge to edge, soldier to hull: how close a boarding soldier stands to climb aboard.</summary>
        internal const int BoardRangeMillimetres = 1500;
        /// <summary>Hull to the chosen shore point, edge to edge: where a ship stops to land its troops.</summary>
        internal const int UnloadRangeMillimetres = 2500;
        /// <summary>How far from the hull's centre a landed soldier may be set down.</summary>
        internal const int LandingReachMillimetres = 3500;
        internal const int BoardTicks = World.TickRate;
        private const int RetryTicks = 10;
        private const int LandingSearchCells = 4;
        private readonly World world;
        private readonly List<int> ids = new List<int>();
        private readonly List<SimPoint> landings = new List<SimPoint>();
        private int errands;

        internal NavalSystem(World world) { this.world = world; }

        internal CommandResult Submit(IGameCommand command)
        {
            if (command is EmbarkCommand embark) return Embark(embark);
            if (command is DisembarkCommand disembark) return Disembark(disembark);
            return Reject(CommandRejection.InvalidCommand, "Unknown naval order.");
        }

        private CommandResult Embark(EmbarkCommand command)
        {
            if (command.UnitIds.Count == 0) return Reject(CommandRejection.EmptySelection, "Select the troops to embark.");
            if (command.UnitIds.Count > 32) return Reject(CommandRejection.TooManyUnits, "A ship takes at most 32 troops.");
            if (!world.TryGetUnit(command.ShipId, out var ship)) return Reject(CommandRejection.UnknownUnit, "The ship no longer exists.");
            if (ship.OwnerId != command.PlayerId) return Reject(CommandRejection.NotOwner, "Troops board only your own ships.");
            if (ship.CargoCapacity == 0) return Reject(CommandRejection.InvalidCommand, "This ship carries no troops.");
            var selected = new List<UnitState>(command.UnitIds.Count);
            var seen = new HashSet<int>();
            foreach (int id in command.UnitIds)
            {
                if (!seen.Add(id)) return Reject(CommandRejection.DuplicateUnit, "Selection contains a duplicate unit.");
                if (!world.TryGetUnit(id, out var unit)) return Reject(CommandRejection.UnknownUnit, "A selected unit does not exist.");
                if (unit.OwnerId != command.PlayerId) return Reject(CommandRejection.NotOwner, "You do not own every selected unit.");
                if (unit.Domain != MovementDomain.Land) return Reject(CommandRejection.InvalidCommand, "Only land units board a ship.");
                if (unit.IsPackedOutpost || unit.ThreadkeeperRemainingTicks != 0 || unit.RelocationStage != RelocationStage.None)
                    return Reject(CommandRejection.FactionActionBusy, "Finish the current deployment first.");
                if (unit.WallId != 0 || unit.BoardingWallId != 0) return Reject(CommandRejection.FactionActionBusy, "Leave the wall before boarding a ship.");
                selected.Add(unit);
            }
            selected.Sort((a, b) => a.Id.CompareTo(b.Id));
            int reserved = ship.CargoCount;
            foreach (var unit in world.units) if (unit.EmbarkShipId == ship.Id && !seen.Contains(unit.Id)) reserved++;
            if (reserved + selected.Count > ship.CargoCapacity)
                return Reject(CommandRejection.CargoFull, "The ship has room for " + Math.Max(0, ship.CargoCapacity - reserved) + " more troops.");
            var hull = new TargetGeometry(ship);
            var routes = new RoutePlan[selected.Count];
            for (int i = 0; i < selected.Count; i++)
            {
                var unit = selected[i];
                if (hull.InRange(unit.Position, unit.RadiusMillimetres, BoardRangeMillimetres)) continue;
                if (!TryBoardRoute(unit, ship, false, out routes[i]))
                    return Reject(CommandRejection.NoShore, "Bring the ship alongside a shore the troops can reach.");
            }
            for (int i = 0; i < selected.Count; i++)
            {
                var unit = selected[i];
                MovementSystem.Stop(unit);
                unit.EmbarkShipId = ship.Id; unit.EmbarkRemainingTicks = 0;
                if (routes[i].Path != null) MovementSystem.SetRoute(unit, routes[i]);
                unit.NavalRouteDestination = unit.Destination;
                unit.NextNavalRetryTick = world.TickIndex + RetryTicks;
            }
            errands += selected.Count;
            return CommandResult.Success(ship.Id);
        }

        private CommandResult Disembark(DisembarkCommand command)
        {
            if (!world.TryGetUnit(command.ShipId, out var ship)) return Reject(CommandRejection.UnknownUnit, "The ship no longer exists.");
            if (ship.OwnerId != command.PlayerId) return Reject(CommandRejection.NotOwner, "You do not own this ship.");
            if (ship.CargoCapacity == 0) return Reject(CommandRejection.InvalidCommand, "This ship carries no troops.");
            if (ship.CargoCount == 0) return Reject(CommandRejection.InvalidCommand, "The ship carries no troops to land.");
            if (!world.navigation.Contains(command.Destination)) return Reject(CommandRejection.InvalidTarget, "Choose ground inside the map.");
            if (!world.navigation.IsWalkable(command.Destination)) return Reject(CommandRejection.DestinationBlocked, "Choose open shore ground to land on.");
            var shore = new TargetGeometry(command.Destination, 0);
            var route = default(RoutePlan);
            if (!shore.InRange(ship.Position, ship.RadiusMillimetres, UnloadRangeMillimetres) &&
                !world.waterNavigation.TryFindAttackRoute(ship.Position, ship.RadiusMillimetres, shore, UnloadRangeMillimetres, out route))
                return Reject(CommandRejection.NoShore, "The ship cannot reach water beside that ground.");
            CombatSystem.CancelAttack(ship, true);
            MovementSystem.Halt(ship);
            ship.IsUnloading = true; ship.UnloadPoint = command.Destination;
            if (route.Path != null) MovementSystem.SetRoute(ship, route);
            ship.NavalRouteDestination = ship.Destination;
            ship.NextNavalRetryTick = world.TickIndex + RetryTicks;
            errands++;
            return CommandResult.Success(ship.Id);
        }

        internal void Tick()
        {
            world.UpdatePassengerPositions();
            // An upper bound on the errands afoot, recounted below: a match that never used a ship never walks its units here.
            if (errands == 0) return;
            ids.Clear();
            foreach (var unit in world.units) if (unit.EmbarkShipId != 0 || unit.IsUnloading) ids.Add(unit.Id);
            errands = ids.Count;
            foreach (int id in ids)
            {
                if (!world.TryGetUnit(id, out var unit)) continue;
                if (unit.EmbarkShipId != 0) Board(unit);
                else Unload(unit);
            }
        }

        private void Board(UnitState unit)
        {
            if (!world.TryGetUnit(unit.EmbarkShipId, out var ship) || ship.OwnerId != unit.OwnerId || ship.CargoCount >= ship.CargoCapacity || Diverted(unit) ||
                unit.WallId != 0 || unit.BoardingWallId != 0 || unit.IsPackedOutpost || unit.ThreadkeeperRemainingTicks != 0 || unit.RelocationStage != RelocationStage.None)
            { CancelEmbark(unit); return; }
            var hull = new TargetGeometry(ship);
            if (hull.InRange(unit.Position, unit.RadiusMillimetres, BoardRangeMillimetres))
            {
                if (unit.Order == UnitOrder.Moving) MovementSystem.Halt(unit);
                unit.NavalRouteDestination = unit.Destination;
                if (unit.EmbarkRemainingTicks == 0) { unit.EmbarkRemainingTicks = BoardTicks; return; }
                if (--unit.EmbarkRemainingTicks > 0) return;
                world.Embark(unit, ship);
                return;
            }
            // The ship moved off: the climb starts again once it is alongside.
            unit.EmbarkRemainingTicks = 0;
            bool blocked = unit.Order == UnitOrder.Moving && unit.MovementBlockedTicks >= 20;
            if (unit.Order == UnitOrder.Moving && !blocked || world.TickIndex < unit.NextNavalRetryTick) return;
            unit.NextNavalRetryTick = world.TickIndex + RetryTicks;
            if (TryBoardRoute(unit, ship, blocked, out var route))
            { MovementSystem.SetRoute(unit, route); unit.NavalRouteDestination = unit.Destination; }
            else CancelEmbark(unit);
        }

        // Attack-range routing considers terrain, not stationary soldiers. A boarding destination occupied by
        // another soldier can leave a passenger circling forever just outside the hull's reach. Keep the ordinary
        // route when clear; otherwise pick a free, connected shore cell, with fixed distance and scan tie-breaks.
        private bool TryBoardRoute(UnitState unit, UnitState ship, bool alternate, out RoutePlan route)
        {
            var hull = new TargetGeometry(ship);
            if (!alternate && world.navigation.TryFindAttackRoute(unit.Position, unit.RadiusMillimetres, hull, BoardRangeMillimetres, out route))
            {
                if (ClearBoardingPoint(unit, route.Destination)) return true;
                RouteLists.Return(route.Path);
            }
            route = default;
            int size = world.cellSize, reach = BoardRangeMillimetres + ship.RadiusMillimetres + unit.RadiusMillimetres;
            int left = Math.Max(0, (ship.Position.X - reach) / size), right = Math.Min(world.mapWidth - 1, (ship.Position.X + reach) / size);
            int bottom = Math.Max(0, (ship.Position.Z - reach) / size), top = Math.Min(world.mapHeight - 1, (ship.Position.Z + reach) / size);
            bool found = false; long best = long.MaxValue; SimPoint destination = default;
            for (int z = bottom; z <= top; z++)
            for (int x = left; x <= right; x++)
            {
                var point = new SimPoint(x * size + size / 2, z * size + size / 2);
                if (alternate && point == unit.Destination || !hull.InRange(point, unit.RadiusMillimetres, BoardRangeMillimetres) ||
                    !world.navigation.CanOccupy(point, unit.RadiusMillimetres) || !world.navigation.CanReach(unit.Position, point) || !ClearBoardingPoint(unit, point)) continue;
                long score = DistanceSquared(unit.Position, point);
                if (found && score >= best) continue;
                found = true; best = score; destination = point;
            }
            if (!found || !world.navigation.TryFindPath(unit.Position, destination, out var path)) return false;
            route = new RoutePlan(destination, path, SimPoint.DistanceCeiling(unit.Position, destination));
            return true;
        }

        private bool ClearBoardingPoint(UnitState unit, SimPoint point)
        {
            foreach (var other in world.units)
                if (other.Id != unit.Id && other.WallId == 0 && MovementGeometry.DiscsOverlap(point, unit.RadiusMillimetres, other.Position, other.RadiusMillimetres)) return false;
            return true;
        }

        private void Unload(UnitState ship)
        {
            if (ship.CargoCount == 0 || Diverted(ship)) { CancelUnload(ship); return; }
            var shore = new TargetGeometry(ship.UnloadPoint, 0);
            if (shore.InRange(ship.Position, ship.RadiusMillimetres, UnloadRangeMillimetres))
            {
                if (ship.Order == UnitOrder.Moving) MovementSystem.Halt(ship);
                // Whoever finds no clear ground stays aboard; the order ends either way.
                Land(ship);
                CancelUnload(ship);
                return;
            }
            if (ship.Order == UnitOrder.Moving || world.TickIndex < ship.NextNavalRetryTick) return;
            ship.NextNavalRetryTick = world.TickIndex + RetryTicks;
            if (world.waterNavigation.TryFindAttackRoute(ship.Position, ship.RadiusMillimetres, shore, UnloadRangeMillimetres, out var route))
            { MovementSystem.SetRoute(ship, route); ship.NavalRouteDestination = ship.Destination; }
            else CancelUnload(ship);
        }

        // Another order took the unit elsewhere: a new route, a fight or a job.
        private static bool Diverted(UnitState unit) =>
            unit.AttackTargetId != 0 || unit.WorkerTask != WorkerTask.None || unit.Order == UnitOrder.Moving && unit.Destination != unit.NavalRouteDestination;

        private void Land(UnitState ship)
        {
            landings.Clear();
            for (int index = 0; index < ship.CargoCount;)
            {
                var passenger = ship.cargo[index];
                if (!FindLanding(ship, passenger, out var point)) { index++; continue; }
                landings.Add(point);
                world.Disembark(ship, index, point);
            }
        }

        /// <summary>
        /// The clear land cell nearest the chosen shore point within reach of the hull, connected on foot to that point,
        /// so nobody is set down on a spit of ground cut off from where they were sent. Scanned in a fixed order.
        /// </summary>
        private bool FindLanding(UnitState ship, UnitState passenger, out SimPoint landing)
        {
            int size = world.cellSize, cx = ship.Position.X / size, cz = ship.Position.Z / size;
            bool found = false; long best = long.MaxValue; landing = default;
            for (int dz = -LandingSearchCells; dz <= LandingSearchCells; dz++)
            for (int dx = -LandingSearchCells; dx <= LandingSearchCells; dx++)
            {
                int x = cx + dx, z = cz + dz;
                if (x < 0 || z < 0 || x >= world.mapWidth || z >= world.mapHeight) continue;
                var point = new SimPoint(x * size + size / 2, z * size + size / 2);
                if (SimPoint.DistanceCeiling(point, ship.Position) > LandingReachMillimetres) continue;
                if (!world.navigation.CanOccupy(point, passenger.RadiusMillimetres) || !world.navigation.CanReach(point, ship.UnloadPoint)) continue;
                long score = DistanceSquared(point, ship.UnloadPoint);
                if (found && score >= best) continue;
                if (!Clear(point, passenger.RadiusMillimetres)) continue;
                found = true; best = score; landing = point;
            }
            return found;
        }

        private bool Clear(SimPoint point, int radius)
        {
            foreach (var other in world.units)
                if (other.WallId == 0 && MovementGeometry.DiscsOverlap(point, radius, other.Position, other.RadiusMillimetres)) return false;
            foreach (var prior in landings) if (SimPoint.DistanceCeiling(point, prior) < radius * 2) return false;
            return true;
        }

        /// <summary>Ends a boarding walk or a landing voyage; any new order does this before it takes over.</summary>
        internal static void CancelOrders(UnitState unit)
        {
            if (unit.EmbarkShipId != 0) CancelEmbark(unit);
            if (unit.IsUnloading) CancelUnload(unit);
        }

        // Combat runs after boarding. Clear reservations immediately when a hull sinks so the same tick's
        // authoritative observation cannot contain references to a destroyed ship.
        internal void ShipDestroyed(int shipId)
        {
            foreach (var unit in world.units)
                if (unit.EmbarkShipId == shipId) { MovementSystem.Halt(unit); CancelEmbark(unit); }
        }

        private static void CancelEmbark(UnitState unit)
        {
            unit.EmbarkShipId = unit.EmbarkRemainingTicks = 0;
            unit.AutoAttackEnabled = true;
        }

        private static void CancelUnload(UnitState ship) { ship.IsUnloading = false; ship.UnloadPoint = default; }

        private static long DistanceSquared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }
        private static CommandResult Reject(CommandRejection reason, string message) => CommandResult.Reject(reason, message);
    }
}
