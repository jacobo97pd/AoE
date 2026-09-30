using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Land units walk to one of their own ships and climb aboard once it lies alongside a shore they can reach.</summary>
    public sealed class EmbarkCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public int ShipId { get; }
        public EmbarkCommand(int playerId, int[] unitIds, int shipId)
        { PlayerId = playerId; UnitIds = Array.AsReadOnly(unitIds == null ? Array.Empty<int>() : (int[])unitIds.Clone()); ShipId = shipId; }
    }

    /// <summary>A ship sails within reach of a shore point and lands everything it carries on clear ground there.</summary>
    public sealed class DisembarkCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int ShipId { get; }
        public SimPoint Destination { get; }
        public DisembarkCommand(int playerId, int shipId, SimPoint destination) { PlayerId = playerId; ShipId = shipId; Destination = destination; }
    }
}
