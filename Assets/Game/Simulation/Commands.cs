using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public interface IGameCommand { int PlayerId { get; } }

    public sealed class MoveCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public SimPoint Destination { get; }
        public MovementFormation Formation { get; }
        public MoveCommand(int playerId, int[] unitIds, SimPoint destination, MovementFormation formation = MovementFormation.Loose)
        {
            PlayerId = playerId;
            UnitIds = Array.AsReadOnly(unitIds == null ? Array.Empty<int>() : (int[])unitIds.Clone());
            Destination = destination;
            Formation = formation;
        }
    }

    public sealed class StopCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public StopCommand(int playerId, int[] unitIds)
        {
            PlayerId = playerId;
            UnitIds = Array.AsReadOnly(unitIds == null ? Array.Empty<int>() : (int[])unitIds.Clone());
        }
    }

    public enum CommandRejection
    {
        None, InvalidCommand, InvalidPlayer, EmptySelection, TooManyUnits,
        UnknownUnit, NotOwner, DuplicateUnit, InvalidTarget, DestinationBlocked, NoPath,
        NotWorker, UnknownResource, ResourceDepleted, NoDropOff, UnknownBuilding,
        UnknownDefinition, InsufficientResources, InvalidPlacement, UnitObstruction,
        BuildingIncomplete, BuildingComplete, CannotTrain, PopulationLimit, QueueFull, EntityLimit,
        Unarmed, FriendlyTarget, UnknownTarget,
        CannotResearch, ResearchBusy, ResearchAlreadyCompleted, ResearchInProgress, PrerequisiteMissing,
        WrongFaction, CannotUseFactionAction, FactionActionBusy, AbilityCooldown, OutOfRange,
        MatchFinished, MatchNotRunning, TargetNotVisible,
        CargoFull, NoShore
    }

    public readonly struct CommandResult
    {
        public bool Accepted { get; }
        public CommandRejection Reason { get; }
        public string Message { get; }
        public int EntityId { get; }
        private CommandResult(bool accepted, CommandRejection reason, string message, int entityId = 0)
        { Accepted = accepted; Reason = reason; Message = message; EntityId = entityId; }
        internal static CommandResult Success(int entityId = 0) => new CommandResult(true, CommandRejection.None, "Order accepted.", entityId);
        internal static CommandResult Reject(CommandRejection reason, string message) => new CommandResult(false, reason, message);
    }
}
