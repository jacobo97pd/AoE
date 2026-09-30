using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public sealed class GatherCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> WorkerIds { get; }
        public int ResourceId { get; }
        public GatherCommand(int playerId, int[] workerIds, int resourceId)
        { PlayerId = playerId; WorkerIds = Copy(workerIds); ResourceId = resourceId; }
        internal static IReadOnlyList<int> Copy(int[] ids) => Array.AsReadOnly(ids == null ? Array.Empty<int>() : (int[])ids.Clone());
    }

    public sealed class BuildCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> WorkerIds { get; }
        public string BuildingDefinitionId { get; }
        public SimPoint Position { get; }
        /// <summary>Place a wall or gate turned a quarter, running north-south (BuildingFootprints.CanTurn).</summary>
        public bool Turned { get; }
        public BuildCommand(int playerId, int[] workerIds, string buildingDefinitionId, SimPoint position, bool turned = false)
        { PlayerId = playerId; WorkerIds = GatherCommand.Copy(workerIds); BuildingDefinitionId = buildingDefinitionId; Position = position; Turned = turned; }
    }

    public sealed class ReturnCargoCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> WorkerIds { get; }
        public int DropOffBuildingId { get; }
        public ReturnCargoCommand(int playerId, int[] workerIds, int dropOffBuildingId = 0)
        { PlayerId = playerId; WorkerIds = GatherCommand.Copy(workerIds); DropOffBuildingId = dropOffBuildingId; }
    }

    public sealed class ConstructCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> WorkerIds { get; }
        public int BuildingId { get; }
        public ConstructCommand(int playerId, int[] workerIds, int buildingId)
        { PlayerId = playerId; WorkerIds = GatherCommand.Copy(workerIds); BuildingId = buildingId; }
    }

    public sealed class TrainCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int BuildingId { get; }
        public string UnitDefinitionId { get; }
        public TrainCommand(int playerId, int buildingId, string unitDefinitionId)
        { PlayerId = playerId; BuildingId = buildingId; UnitDefinitionId = unitDefinitionId; }
    }

    public sealed class SetRallyCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int BuildingId { get; }
        public SimPoint Destination { get; }
        public SetRallyCommand(int playerId, int buildingId, SimPoint destination)
        { PlayerId = playerId; BuildingId = buildingId; Destination = destination; }
    }
}
