using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public enum SiegeEquipmentKind { None, Ram, Ladder, Tower }
    public sealed class BoardWallCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public int WallId { get; }
        public int SiegeUnitId { get; }
        public BoardWallCommand(int playerId, int[] unitIds, int wallId, int siegeUnitId = 0)
        { PlayerId = playerId; UnitIds = Array.AsReadOnly(unitIds == null ? Array.Empty<int>() : (int[])unitIds.Clone()); WallId = wallId; SiegeUnitId = siegeUnitId; }
    }
    public sealed class LeaveWallCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public SimPoint Destination { get; }
        public LeaveWallCommand(int playerId, int[] unitIds, SimPoint destination)
        { PlayerId = playerId; UnitIds = Array.AsReadOnly(unitIds == null ? Array.Empty<int>() : (int[])unitIds.Clone()); Destination = destination; }
    }
    public sealed class SetGateCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int BuildingId { get; }
        public bool Open { get; }
        public SetGateCommand(int playerId, int buildingId, bool open)
        { PlayerId = playerId; BuildingId = buildingId; Open = open; }
    }
}
