using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>
    /// Footprint orientation. Walls and gates may stand turned a quarter so a fortification can run north-south: the
    /// state swaps WidthCells and DepthCells and everything that reads the state's footprint (navigation, fog, range,
    /// climbing, gates) follows it. A square footprint turns into itself, so it never turns.
    /// </summary>
    public static class BuildingFootprints
    {
        public static bool CanTurn(BuildingDefinition definition) =>
            definition != null && (definition.IsWall || definition.IsGate) && definition.WidthCells != definition.DepthCells;
        public static int WidthCells(BuildingDefinition definition, bool turned) => turned ? definition.DepthCells : definition.WidthCells;
        public static int DepthCells(BuildingDefinition definition, bool turned) => turned ? definition.WidthCells : definition.DepthCells;

        /// <summary>
        /// Where deck position <paramref name="slot"/> of <paramref name="capacity"/> stands: spread evenly along the wall's
        /// long side, at its middle. The simulation boards soldiers here and the presentation draws them here.
        /// </summary>
        public static SimPoint DeckSlot(BuildingState wall, int capacity, int slot, int cellSize)
        {
            if (wall.DepthCells > wall.WidthCells)
            {
                int depth = wall.DepthCells * cellSize;
                return new SimPoint(wall.Position.X, wall.Position.Z - depth / 2 + (slot + 1) * depth / (capacity + 1));
            }
            int width = wall.WidthCells * cellSize;
            return new SimPoint(wall.Position.X - width / 2 + (slot + 1) * width / (capacity + 1), wall.Position.Z);
        }
    }

    /// <summary>One stretch of a wall run: the centre of its footprint and whether it stands north-south.</summary>
    [Serializable]
    public struct BuildSite : IEquatable<BuildSite>
    {
        public SimPoint Position;
        public bool Turned;
        public BuildSite(SimPoint position, bool turned) { Position = position; Turned = turned; }
        public bool Equals(BuildSite other) => Position == other.Position && Turned == other.Turned;
        public override bool Equals(object obj) => obj is BuildSite other && Equals(other);
        public override int GetHashCode() => Position.GetHashCode() * 2 + (Turned ? 1 : 0);
    }

    /// <summary>
    /// One order for a whole run of wall. Every stretch that can stand and that the player can still pay for is laid
    /// as a foundation and paid for at once; the others are skipped. The workers go to the first foundation together
    /// and, as each one is finished, walk on to the next unfinished one in list order.
    /// </summary>
    public sealed class BuildRunCommand : IGameCommand
    {
        public const int MaximumSites = 64;
        public int PlayerId { get; }
        public IReadOnlyList<int> WorkerIds { get; }
        public string BuildingDefinitionId { get; }
        public IReadOnlyList<BuildSite> Sites { get; }
        public BuildRunCommand(int playerId, int[] workerIds, string buildingDefinitionId, BuildSite[] sites)
        {
            PlayerId = playerId; WorkerIds = GatherCommand.Copy(workerIds); BuildingDefinitionId = buildingDefinitionId;
            Sites = Array.AsReadOnly(sites == null ? Array.Empty<BuildSite>() : (BuildSite[])sites.Clone());
        }
    }

    /// <summary>
    /// What a BuildRunCommand would do now, from World.PreviewBuildRun: the same planning the command runs, without
    /// changing anything. Each site's verdict is accepted (it would be laid), InsufficientResources (it could stand
    /// but the stock runs out before it) or the reason it cannot stand there.
    /// </summary>
    public sealed class BuildRunPreview
    {
        internal readonly List<CommandResult> verdicts = new List<CommandResult>();
        /// <summary>The command's answer: accepted when at least one stretch would be laid and every builder reaches the first.</summary>
        public CommandResult Result { get; internal set; }
        /// <summary>One verdict per site, in the command's order.</summary>
        public IReadOnlyList<CommandResult> Sites => verdicts;
        /// <summary>What the accepted stretches cost together.</summary>
        public ResourceAmount Cost { get; internal set; }
        public int AcceptedCount { get; internal set; }
    }

    /// <summary>
    /// Lays a dragged wall run out as whole stretches. Each leg runs straight east-west or north-south, along its longer
    /// axis, towards the next corner the player chose, rounded to whole stretches; a north-south leg is made of turned
    /// stretches. The first leg starts on the start cell; each later leg starts on the cell beside the previous leg's
    /// last cell in its own direction, so corners meet without a gap or an overlap and a rectangle whose opposite
    /// sides hold the same number of stretches closes exactly. Pure: the rules judge the stretches (World.PreviewBuildRun).
    /// </summary>
    public static class WallRunLayout
    {
        /// <summary>The cell holding a point, also for points left of or below the map.</summary>
        public static GridCell Cell(SimPoint point, int cellSize) =>
            new GridCell(FloorDivide(point.X, cellSize), FloorDivide(point.Z, cellSize));

        /// <summary>
        /// Replaces <paramref name="sites"/> with the stretches through <paramref name="corners"/> (the start cell, the
        /// corners fixed so far, then the cursor), at most BuildRunCommand.MaximumSites. Returns whether the run closes:
        /// three or more legs whose last cell lies beside the start cell.
        /// </summary>
        public static bool Layout(BuildingDefinition definition, int cellSize, IReadOnlyList<GridCell> corners, List<BuildSite> sites)
        {
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            sites.Clear();
            if (definition == null || corners == null || corners.Count == 0 || cellSize < 1) return false;
            int along = definition.WidthCells, across = definition.DepthCells;
            bool canTurn = BuildingFootprints.CanTurn(definition);
            var start = corners[0];
            int endX = start.X, endZ = start.Z, legs = 0;
            for (int i = corners.Count == 1 ? 0 : 1; i < corners.Count && sites.Count < BuildRunCommand.MaximumSites; i++)
            {
                var target = corners[i];
                // The first leg counts its start cell; a later leg begins one cell past the previous leg's end.
                int dx = target.X - endX, dz = target.Z - endZ;
                if (legs > 0 && dx == 0 && dz == 0) continue;
                bool alongX = !canTurn || Math.Abs(dx) >= Math.Abs(dz);
                int delta = alongX ? dx : dz;
                if (legs > 0 && delta == 0) continue;
                int step = delta < 0 ? -1 : 1;
                int startX = endX, startZ = endZ, span = Math.Abs(delta) + 1;
                if (legs > 0) { if (alongX) startX += step; else startZ += step; span--; }
                int stretches = Math.Max(1, (span + along / 2) / along);
                stretches = Math.Min(stretches, BuildRunCommand.MaximumSites - sites.Count);
                for (int k = 0; k < stretches; k++)
                {
                    // The south-west cell of stretch k, walking away from the leg's start cell.
                    int first = step > 0 ? k * along : -(k + 1) * along + 1;
                    if (alongX)
                        sites.Add(new BuildSite(new SimPoint((startX + first) * cellSize + along * cellSize / 2, startZ * cellSize + across * cellSize / 2), false));
                    else
                        sites.Add(new BuildSite(new SimPoint(startX * cellSize + across * cellSize / 2, (startZ + first) * cellSize + along * cellSize / 2), true));
                }
                int reach = step * (stretches * along - 1);
                if (alongX) { endX = startX + reach; endZ = startZ; } else { endX = startX; endZ = startZ + reach; }
                legs++;
            }
            return legs >= 3 && Math.Abs(endX - start.X) + Math.Abs(endZ - start.Z) == 1;
        }

        /// <summary>
        /// Reorders a run so the builders start at the stretch nearest <paramref name="from"/>: an open run is walked from
        /// its nearer end, a closed one from its nearest stretch onwards in the same direction.
        /// </summary>
        public static void NearestFirst(List<BuildSite> sites, SimPoint from, bool closed)
        {
            if (sites == null || sites.Count < 2) return;
            if (!closed)
            {
                if (DistanceSquared(sites[sites.Count - 1].Position, from) < DistanceSquared(sites[0].Position, from)) sites.Reverse();
                return;
            }
            int nearest = 0;
            for (int i = 1; i < sites.Count; i++)
                if (DistanceSquared(sites[i].Position, from) < DistanceSquared(sites[nearest].Position, from)) nearest = i;
            if (nearest == 0) return;
            var rotated = new BuildSite[sites.Count];
            for (int i = 0; i < rotated.Length; i++) rotated[i] = sites[(nearest + i) % sites.Count];
            sites.Clear(); sites.AddRange(rotated);
        }

        private static long DistanceSquared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }
        private static int FloorDivide(int value, int divisor) { int quotient = value / divisor; return value % divisor < 0 ? quotient - 1 : quotient; }
    }
}
