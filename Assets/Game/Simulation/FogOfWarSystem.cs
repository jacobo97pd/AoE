using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Per-player visible/explored cell masks. Vision ignores terrain occlusion in this slice.</summary>
    public sealed partial class FogOfWarSystem
    {
        private sealed class PlayerVision
        {
            internal bool[] Visible, Next;
            internal readonly bool[] Explored;
            internal PlayerVision(int count) { Visible = new bool[count]; Next = new bool[count]; Explored = new bool[count]; }
        }
        private struct SourceStamp
        {
            internal int Id, Owner, CellX, CellZ, Radius, FootprintX, FootprintZ, Width, Depth;
            internal bool Matches(SourceStamp other) => Id == other.Id && Owner == other.Owner && CellX == other.CellX && CellZ == other.CellZ &&
                Radius == other.Radius && FootprintX == other.FootprintX && FootprintZ == other.FootprintZ && Width == other.Width && Depth == other.Depth;
        }
        private readonly World world;
        private readonly int unitRadius, buildingRadius;
        private readonly Dictionary<int, PlayerVision> players = new Dictionary<int, PlayerVision>();
        private SourceStamp[] sources = Array.Empty<SourceStamp>();
        private int sourceCount = -1;
        private readonly int[][] spans = new int[129][];
        public int WidthCells => world.mapWidth;
        public int HeightCells => world.mapHeight;
        public long Revision { get; private set; }

        internal FogOfWarSystem(World world, OfflineMatchDefinition definition)
        {
            this.world = world;
            if (definition.VisionUnitCells < 1 || definition.VisionUnitCells > 128 || definition.VisionBuildingCells < 1 || definition.VisionBuildingCells > 128)
                throw new ArgumentException("Default unit/building vision must be between 1 and 128 cells.");
            unitRadius = definition.VisionUnitCells; buildingRadius = definition.VisionBuildingCells;
            foreach (var unit in world.unitDefinitions.Values)
                if (unit.VisionCells < 0 || unit.VisionCells > 128) throw new ArgumentException("Unit vision override must be 0 through 128 cells.");
            foreach (var building in world.buildingDefinitions.Values)
                if (building.VisionCells < 0 || building.VisionCells > 128) throw new ArgumentException("Building vision override must be 0 through 128 cells.");
            foreach (var player in world.Players) players.Add(player.Id, new PlayerVision(WidthCells * HeightCells));
            Refresh();
        }

        public bool IsVisible(int playerId, SimPoint position) =>
            world.navigation.Contains(position) && players.TryGetValue(playerId, out var player) && player.Visible[Index(position)];
        public bool IsExplored(int playerId, SimPoint position) =>
            world.navigation.Contains(position) && players.TryGetValue(playerId, out var player) && player.Explored[Index(position)];

        /// <summary>
        /// One player's sight of the whole map, row by row, for presentation to draw from in one pass: bit 1 (2) in sight,
        /// bit 0 (1) explored, the answers IsVisible and IsExplored give at each cell's centre. Read only; false, and
        /// nothing written, for a player the fog does not track or an array that is not one byte per cell.
        /// </summary>
        public bool CopyCells(int playerId, byte[] cells)
        {
            if (cells == null || cells.Length != WidthCells * HeightCells || !players.TryGetValue(playerId, out var player)) return false;
            bool[] visible = player.Visible, explored = player.Explored;
            for (int i = 0; i < cells.Length; i++) cells[i] = (byte)((visible[i] ? 2 : 0) | (explored[i] ? 1 : 0));
            return true;
        }

        public bool IsEntityVisible(int playerId, int entityId)
        {
            if (world.TryGetUnit(entityId, out var unit)) return IsUnitVisible(playerId, unit);
            if (world.TryGetBuilding(entityId, out var building)) return IsBuildingVisible(playerId, building);
            return players.ContainsKey(playerId) && world.TryGetResource(entityId, out var resource) && IsVisible(playerId, resource.Position);
        }

        /// <summary>IsEntityVisible for a unit in the world, without finding it by ID.</summary>
        internal bool IsUnitVisible(int playerId, UnitState unit)
        {
            if (!players.TryGetValue(playerId, out var vision)) return false;
            return unit.OwnerId == playerId || world.navigation.Contains(unit.Position) && vision.Visible[Index(unit.Position)];
        }

        /// <summary>IsEntityVisible for a building in the world, without finding it by ID.</summary>
        internal bool IsBuildingVisible(int playerId, BuildingState building)
        {
            if (!players.TryGetValue(playerId, out var vision)) return false;
            if (building.OwnerId == playerId) return true;
            var footprint = GridFootprint.Building(building, world.cellSize);
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                if (vision.Visible[z * WidthCells + x]) return true;
            return false;
        }

        internal CommandResult ValidatePlacement(int playerId, string definitionId, SimPoint position, bool turned = false)
        {
            if (definitionId == null || !world.buildingDefinitions.TryGetValue(definitionId, out var definition))
                return CommandResult.Reject(CommandRejection.UnknownDefinition, "Building definition does not exist.");
            if (turned && !BuildingFootprints.CanTurn(definition))
                return CommandResult.Reject(CommandRejection.InvalidPlacement, "Only walls and gates can be turned.");
            int width = BuildingFootprints.WidthCells(definition, turned), depth = BuildingFootprints.DepthCells(definition, turned);
            long left = (long)position.X - (long)width * world.cellSize / 2;
            long bottom = (long)position.Z - (long)depth * world.cellSize / 2;
            if (left < 0 || bottom < 0 || left % world.cellSize != 0 || bottom % world.cellSize != 0 ||
                left / world.cellSize + width > WidthCells || bottom / world.cellSize + depth > HeightCells)
                return CommandResult.Reject(CommandRejection.InvalidPlacement, "Building footprint must align to the grid and fit inside the map.");
            for (int z = (int)(bottom / world.cellSize); z < bottom / world.cellSize + depth; z++)
            for (int x = (int)(left / world.cellSize); x < left / world.cellSize + width; x++)
                if (!IsVisible(playerId, new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2)))
                    return CommandResult.Reject(CommandRejection.TargetNotVisible, "The complete placement footprint must currently be visible.");
            return CommandResult.Success();
        }

        internal void Refresh()
        {
            // Preserve every call boundary; skip only when the exact inputs to the cell masks are unchanged.
            if (!SourcesChanged()) return;
            foreach (var player in players.Values) Array.Clear(player.Next, 0, player.Next.Length);
            // Every cell revealed is marked explored as it is revealed.
            foreach (var unit in world.units)
            {
                int radius = unit.Definition.VisionCells;
                Reveal(players[unit.OwnerId], unit.Position, radius == 0 ? unitRadius : radius);
            }
            foreach (var building in world.buildings)
            {
                int radius = building.Definition.VisionCells;
                Reveal(players[building.OwnerId], building.Position, radius == 0 ? buildingRadius : radius);
                // A large owned footprint is always known, including any part outside its center's radius.
                var footprint = GridFootprint.Building(building, world.cellSize);
                var owner = players[building.OwnerId];
                for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
                for (int x = footprint.X; x < footprint.X + footprint.Width; x++) owner.Next[z * WidthCells + x] = owner.Explored[z * WidthCells + x] = true;
            }
            bool changed = false;
            foreach (var player in players.Values)
            {
                bool[] visible = player.Visible, next = player.Next;
                for (int i = 0; i < next.Length && !changed; i++) changed = visible[i] != next[i];
                player.Visible = next; player.Next = visible;
            }
            if (changed) Revision++;
        }

        private bool SourcesChanged()
        {
            int count = world.units.Count + world.buildings.Count;
            bool changed = count != sourceCount;
            if (sources.Length < count) Array.Resize(ref sources, Math.Max(count, Math.Max(16, sources.Length * 2)));
            int index = 0;
            foreach (var unit in world.units)
            {
                int radius = unit.Definition.VisionCells;
                var next = new SourceStamp { Id = unit.Id, Owner = unit.OwnerId, CellX = unit.Position.X / world.cellSize,
                    CellZ = unit.Position.Z / world.cellSize, Radius = radius == 0 ? unitRadius : radius };
                if (!next.Matches(sources[index])) { changed = true; sources[index] = next; }
                index++;
            }
            foreach (var building in world.buildings)
            {
                int radius = building.Definition.VisionCells;
                var footprint = GridFootprint.Building(building, world.cellSize);
                var next = new SourceStamp { Id = building.Id, Owner = building.OwnerId, CellX = building.Position.X / world.cellSize,
                    CellZ = building.Position.Z / world.cellSize, Radius = radius == 0 ? buildingRadius : radius,
                    FootprintX = footprint.X, FootprintZ = footprint.Z, Width = footprint.Width, Depth = footprint.Depth };
                if (!next.Matches(sources[index])) { changed = true; sources[index] = next; }
                index++;
            }
            sourceCount = count;
            return changed;
        }

        private int Index(SimPoint position) => position.Z / world.cellSize * WidthCells + position.X / world.cellSize;
        // Each row of the disc is one run of cells, those with dx * dx + dz * dz <= radius * radius.
        private void Reveal(PlayerVision player, SimPoint position, int radius)
        {
            int cx = position.X / world.cellSize, cz = position.Z / world.cellSize;
            int minZ = Math.Max(0, cz - radius), maxZ = Math.Min(HeightCells - 1, cz + radius);
            var half = Spans(radius); bool[] next = player.Next, explored = player.Explored;
            for (int z = minZ; z <= maxZ; z++)
            {
                int span = half[Math.Abs(z - cz)], row = z * WidthCells;
                for (int cell = row + Math.Max(0, cx - span), last = row + Math.Min(WidthCells - 1, cx + span); cell <= last; cell++) next[cell] = explored[cell] = true;
            }
        }

        private int[] Spans(int radius)
        {
            var half = spans[radius];
            if (half != null) return half;
            half = spans[radius] = new int[radius + 1];
            for (int dz = 0, dx = radius; dz <= radius; dz++)
            {
                while (dx * dx + dz * dz > radius * radius) dx--;
                half[dz] = dx;
            }
            return half;
        }
    }
}
