using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Logical wall decks, timed boarding, vulnerable assault equipment, public gates and collapse evacuation.</summary>
    internal sealed class SiegeSystem
    {
        private readonly World world;
        private readonly List<int> ids = new List<int>();
        internal SiegeSystem(World world) { this.world = world; }

        internal CommandResult Submit(IGameCommand command)
        {
            if (command is SetGateCommand gate) return SetGate(gate);
            if (command is BoardWallCommand board) return Board(board);
            if (command is LeaveWallCommand leave) return Leave(leave);
            return Reject(CommandRejection.InvalidCommand, "Unknown siege order.");
        }

        private CommandResult Select(int player, IReadOnlyList<int> unitIds, out List<UnitState> selected)
        {
            selected = new List<UnitState>();
            if (unitIds.Count == 0 || unitIds.Count > 32) return Reject(CommandRejection.EmptySelection, "Select between one and 32 infantry or ranged soldiers.");
            var seen = new HashSet<int>();
            foreach (int id in unitIds)
            {
                if (!seen.Add(id)) return Reject(CommandRejection.DuplicateUnit, "Duplicate selected unit.");
                if (!world.TryGetUnit(id, out var unit)) return Reject(CommandRejection.UnknownUnit, "Selected unit no longer exists.");
                if (unit.OwnerId != player) return Reject(CommandRejection.NotOwner, "You do not own every selected unit.");
                if (unit.Domain != MovementDomain.Land || unit.IsWorker || (unit.Tags & (CombatTags.Infantry | CombatTags.Ranged)) == 0 ||
                    (unit.Tags & (CombatTags.Creature | CombatTags.Cavalry | CombatTags.Siege)) != 0)
                    return Reject(CommandRejection.InvalidCommand, "Wall decks admit infantry and archers; mounted armies and creatures remain on the ground.");
                if (unit.ThreadkeeperRemainingTicks != 0 || unit.RelocationStage != RelocationStage.None)
                    return Reject(CommandRejection.FactionActionBusy, "Finish the current deployment first.");
                selected.Add(unit);
            }
            selected.Sort((a, b) => a.Id.CompareTo(b.Id));
            return CommandResult.Success();
        }

        private CommandResult Board(BoardWallCommand command)
        {
            var result = Select(command.PlayerId, command.UnitIds, out var selected); if (!result.Accepted) return result;
            if (!world.TryGetBuilding(command.WallId, out var wall)) return Reject(CommandRejection.UnknownBuilding, "Wall no longer exists.");
            var definition = world.buildingDefinitions[wall.DefinitionId];
            if (!definition.IsWall || !wall.IsOperational) return Reject(CommandRejection.InvalidCommand, "Board a completed wall segment.");
            if (world.Vision != null && !world.Vision.IsEntityVisible(command.PlayerId, wall.Id)) return Reject(CommandRejection.TargetNotVisible, "Scout this wall first.");
            int reserved = 0;
            foreach (var unit in world.units) if (unit.WallId == wall.Id || unit.BoardingWallId == wall.Id) reserved++;
            if (reserved + selected.Count > definition.WallCapacity) return Reject(CommandRejection.UnitObstruction, "The wall deck has insufficient free positions.");
            int climbTicks = 40;
            if (wall.OwnerId != command.PlayerId)
            {
                if (!EquipmentReady(command.PlayerId, command.SiegeUnitId, wall, out var equipment))
                    return Reject(CommandRejection.OutOfRange, "Bring your siege ladder or tower beside the enemy wall.");
                climbTicks = equipment == SiegeEquipmentKind.Tower ? 40 : 100;
            }
            var geometry = new TargetGeometry(wall, world.cellSize);
            foreach (var unit in selected)
            {
                if (unit.WallId != 0 || unit.BoardingWallId != 0) return Reject(CommandRejection.FactionActionBusy, "A selected unit is already on a wall or climbing.");
                if (!geometry.InRange(unit.Position, unit.RadiusMillimetres, 1800)) return Reject(CommandRejection.OutOfRange, "Move soldiers beside the wall before climbing.");
            }
            foreach (var unit in selected)
            {
                NavalSystem.CancelOrders(unit);
                MovementSystem.Stop(unit);
                unit.BoardingWallId = wall.Id;
                unit.BoardingSiegeUnitId = wall.OwnerId == command.PlayerId ? 0 : command.SiegeUnitId;
                unit.BoardingRemainingTicks = climbTicks;
            }
            return CommandResult.Success(wall.Id);
        }

        private bool EquipmentReady(int player, int id, BuildingState wall, out SiegeEquipmentKind equipment)
        {
            equipment = SiegeEquipmentKind.None;
            if (!world.TryGetUnit(id, out var unit) || unit.OwnerId != player || unit.Order != UnitOrder.Idle) return false;
            equipment = unit.Definition.SiegeEquipment;
            return (equipment == SiegeEquipmentKind.Ladder || equipment == SiegeEquipmentKind.Tower) &&
                new TargetGeometry(wall, world.cellSize).InRange(unit.Position, unit.RadiusMillimetres, 1300);
        }

        private CommandResult Leave(LeaveWallCommand command)
        {
            var result = Select(command.PlayerId, command.UnitIds, out var selected); if (!result.Accepted) return result;
            var positions = new List<SimPoint>();
            foreach (var unit in selected)
            {
                if (unit.WallId == 0 || !world.TryGetBuilding(unit.WallId, out var wall)) return Reject(CommandRejection.InvalidCommand, "Every selected unit must occupy a wall deck.");
                if (!new TargetGeometry(wall, world.cellSize).InRange(command.Destination, 0, 2200)) return Reject(CommandRejection.OutOfRange, "Choose ground immediately beside the wall.");
                if (!FindGround(command.Destination, unit, 2, positions, out var point)) return Reject(CommandRejection.UnitObstruction, "No clear ground is available beside that wall.");
                // Never let the search escape the requested wall side or jump a second fortification.
                if (!new TargetGeometry(wall, world.cellSize).InRange(point, 0, 2200) || SimPoint.DistanceCeiling(point, command.Destination) > 1800)
                    return Reject(CommandRejection.DestinationBlocked, "Choose a clear landing beside the wall.");
                if (!ClearDescent(unit.Position, point, wall)) return Reject(CommandRejection.DestinationBlocked, "Another obstacle blocks this descent.");
                positions.Add(point);
            }
            for (int i = 0; i < selected.Count; i++) Land(selected[i], positions[i]);
            return CommandResult.Success();
        }

        private CommandResult SetGate(SetGateCommand command)
        {
            if (!world.TryGetBuilding(command.BuildingId, out var building)) return Reject(CommandRejection.UnknownBuilding, "Gate no longer exists.");
            if (building.OwnerId != command.PlayerId) return Reject(CommandRejection.NotOwner, "You do not own this gate.");
            if (!world.buildingDefinitions[building.DefinitionId].IsGate || !building.IsOperational) return Reject(CommandRejection.InvalidCommand, "Choose a completed gate.");
            if (building.GateOpen == command.Open) return CommandResult.Success();
            if (!command.Open)
            {
                var geometry = new TargetGeometry(building, world.cellSize);
                foreach (var unit in world.units)
                    if (unit.WallId == 0 && geometry.InRange(unit.Position, unit.RadiusMillimetres, 0))
                        return Reject(CommandRejection.UnitObstruction, "Clear the gate before closing it.");
            }
            building.GateOpen = command.Open;
            var footprint = GridFootprint.Building(building, world.cellSize);
            if (command.Open) world.navigation.ClearFootprint(footprint); else world.navigation.AddFootprint(footprint);
            world.movement.ReplanAll();
            return CommandResult.Success();
        }

        internal void Tick()
        {
            foreach (var unit in world.units)
            {
                if (unit.BoardingWallId == 0) continue;
                if (!world.TryGetBuilding(unit.BoardingWallId, out var wall) ||
                    (unit.BoardingSiegeUnitId != 0 && !EquipmentReady(unit.OwnerId, unit.BoardingSiegeUnitId, wall, out _)))
                { CancelBoarding(unit); continue; }
                if (--unit.BoardingRemainingTicks > 0) continue;
                var definition = world.buildingDefinitions[wall.DefinitionId];
                int slot = 0;
                for (; slot < definition.WallCapacity; slot++)
                {
                    bool occupied = false; var point = DeckPosition(wall, definition, slot);
                    foreach (var other in world.units) if (other.WallId == wall.Id && other.Position == point) { occupied = true; break; }
                    if (!occupied) break;
                }
                if (slot == definition.WallCapacity) { CancelBoarding(unit); continue; }
                unit.WallId = wall.Id; unit.ElevationMillimetres = definition.WallHeightMillimetres;
                unit.Position = unit.PreviousPosition = unit.Destination = DeckPosition(wall, definition, slot);
                CancelBoarding(unit);
                unit.AutoAttackEnabled = true;
                world.factions.ApplyStats(unit);
            }
            if (world.TickIndex % World.TickRate == 0) Regenerate();
        }

        // Along the wall's long side, so a turned wall's deck runs north-south.
        private SimPoint DeckPosition(BuildingState wall, BuildingDefinition definition, int slot) =>
            BuildingFootprints.DeckSlot(wall, definition.WallCapacity, slot, world.cellSize);

        private void Regenerate()
        {
            foreach (var unit in world.units)
            {
                if (unit.Health == unit.MaxHealth || world.TickIndex - unit.LastDamageTick < 120 || unit.AttackTargetId != 0) continue;
                int healing = unit.Definition.RegenerationPerSecond;
                var faction = world.factions.DefinitionFor(unit.OwnerId);
                if (faction != null && faction.HomeHealingPerSecond != 0)
                    foreach (var building in world.buildings)
                        if (building.OwnerId == unit.OwnerId && building.IsOperational && (building.DefinitionId == "hearth" || building.DefinitionId == "keep") &&
                            new TargetGeometry(building, world.cellSize).InRange(unit.Position, unit.RadiusMillimetres, 5000))
                        { healing += faction.HomeHealingPerSecond; break; }
                unit.Health = (int)Math.Min(unit.MaxHealth, (long)unit.Health + healing);
            }
        }

        internal void WallDestroyed(BuildingState wall)
        {
            ids.Clear();
            foreach (var unit in world.units) if (unit.WallId == wall.Id || unit.BoardingWallId == wall.Id) ids.Add(unit.Id);
            foreach (int id in ids)
            {
                if (!world.TryGetUnit(id, out var unit)) continue;
                CancelBoarding(unit);
                if (unit.WallId == 0) continue;
                unit.Health = Math.Max(0, unit.Health - Math.Max(1, unit.MaxHealth * 2 / 5));
                unit.LastDamageTick = world.TickIndex;
                if (unit.Health == 0 || !FindGround(wall.Position, unit, 4, null, out var point)) world.DestroyUnit(unit);
                else Land(unit, point);
            }
        }

        private bool FindGround(SimPoint center, UnitState unit, int search, List<SimPoint> reserved, out SimPoint point)
        {
            int cx = center.X / world.cellSize, cz = center.Z / world.cellSize;
            for (int radius = 0; radius <= search; radius++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Math.Abs(x) != radius && Math.Abs(z) != radius) continue;
                point = new SimPoint((cx + x) * world.cellSize + world.cellSize / 2, (cz + z) * world.cellSize + world.cellSize / 2);
                if (!world.navigation.CanOccupy(point, unit.RadiusMillimetres)) continue;
                bool clear = true;
                foreach (var other in world.units)
                    if (other.Id != unit.Id && other.WallId == 0 && MovementGeometry.DiscsOverlap(point, unit.RadiusMillimetres, other.Position, other.RadiusMillimetres)) { clear = false; break; }
                if (clear && reserved != null) foreach (var prior in reserved) if (SimPoint.DistanceCeiling(point, prior) < unit.RadiusMillimetres * 2) { clear = false; break; }
                if (clear) return true;
            }
            point = default; return false;
        }

        private bool ClearDescent(SimPoint from, SimPoint to, BuildingState wall)
        {
            int steps = Math.Max(1, SimPoint.DistanceCeiling(from, to) / Math.Max(1, world.cellSize / 4));
            int left = wall.Position.X - wall.WidthCells * world.cellSize / 2, bottom = wall.Position.Z - wall.DepthCells * world.cellSize / 2;
            for (int step = 1; step <= steps; step++)
            {
                var point = new SimPoint(from.X + (int)(((long)to.X - from.X) * step / steps), from.Z + (int)(((long)to.Z - from.Z) * step / steps));
                if (point.X >= left && point.X < left + wall.WidthCells * world.cellSize && point.Z >= bottom && point.Z < bottom + wall.DepthCells * world.cellSize) continue;
                if (!world.navigation.IsWalkable(point)) return false;
            }
            return true;
        }

        private void Land(UnitState unit, SimPoint position)
        {
            MovementSystem.Stop(unit); unit.WallId = unit.ElevationMillimetres = 0; CancelBoarding(unit);
            unit.Position = unit.PreviousPosition = unit.Destination = position;
            world.factions.ApplyStats(unit);
        }
        internal static void CancelBoarding(UnitState unit) { unit.BoardingWallId = unit.BoardingRemainingTicks = unit.BoardingSiegeUnitId = 0; }
        private static CommandResult Reject(CommandRejection reason, string message) => CommandResult.Reject(reason, message);
    }
}
