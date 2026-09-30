using System;

namespace Emberfield.Simulation
{
    public enum VictoryMode { Conquest = 0, Dominion = 1 }
    public enum MatchEndReason { None = 0, Conquest = 1, Dominion = 2, Surrender = 3, SimultaneousElimination = 4, SimultaneousDominion = 5 }

    [Serializable]
    public sealed class OfflineMatchDefinition
    {
        // Explicit opt-in survives serializers that construct omitted nested objects.
        public bool Enabled;
        public VictoryMode Mode;
        public int[] PlayerIds = new[] { 1, 2 };
        public int VisionUnitCells = 9;
        public int VisionBuildingCells = 12;
        public string CentralBuildingId = "hearth";
        public DominionObjectiveDefinition[] Objectives = Array.Empty<DominionObjectiveDefinition>();
        public int ObjectivesRequired = 2;
        public int CaptureTicks = 300;
        public int ContinuousHoldTicks = 9600;
    }

    [Serializable]
    public sealed class DominionObjectiveDefinition
    {
        public string Id;
        public string DisplayName;
        public SimPoint Position;
        public int RadiusMillimetres = 3000;
    }

    public sealed class SurrenderCommand : IGameCommand
    {
        public int PlayerId { get; }
        public SurrenderCommand(int playerId) { PlayerId = playerId; }
    }
}
