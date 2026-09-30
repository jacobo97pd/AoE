using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public enum UnitOrder { Idle, Moving }
    public enum WorkerTask { None, MovingToResource, Gathering, ReturningResources, MovingToConstruction, Constructing }

    public sealed class UnitState
    {
        public int Id { get; }
        public int OwnerId { get; }
        public string DefinitionId { get; }
        public SimPoint Position { get; internal set; }
        public SimPoint PreviousPosition { get; internal set; }
        public SimPoint Destination { get; internal set; }
        public UnitOrder Order { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; }
        public CombatTags Tags { get; }
        public int Armor { get; internal set; }
        public int AttackDamage { get; internal set; }
        public int GatherAmount { get; internal set; }
        public int CarryCapacity { get; internal set; }
        public int MoveSpeedMillimetresPerSecond { get; internal set; }
        public int CharterSourceId { get; internal set; }
        public int ThreadkeeperRemainingTicks { get; internal set; }
        public int ThreadkeeperCooldownTicks { get; internal set; }
        internal long ThreadkeeperCooldownStartedTick = -1;
        public int LinkedStoreyardId { get; internal set; }
        public int RepositionRemainingTicks { get; internal set; }
        public int RepositionCooldownTicks { get; internal set; }
        public int WallId { get; internal set; }
        public int ElevationMillimetres { get; internal set; }
        public int BoardingWallId { get; internal set; }
        public int BoardingRemainingTicks { get; internal set; }
        internal int BoardingSiegeUnitId;
        internal long LastDamageTick = -100000;
        public RelocationStage RelocationStage { get; internal set; }
        public int RelocationRemainingTicks { get; internal set; }
        public int RelocationTotalTicks { get; internal set; }
        public bool IsPackedOutpost => PackedBuilding != null;
        public string PackedBuildingDefinitionId => PackedBuilding?.DefinitionId;
        public SimPoint DeploymentPosition { get; internal set; }
        internal BuildingState PackedBuilding;
        public int PopulationCost { get; }
        public int AttackTargetId { get; internal set; }
        public int AttackCooldownTicks { get; internal set; }
        public bool AutoAttackEnabled { get; internal set; } = true;
        public int RadiusMillimetres { get; }
        public bool IsWorker { get; }
        public ResourceKind CarriedKind { get; internal set; }
        public int CarriedAmount { get; internal set; }
        public WorkerTask WorkerTask { get; internal set; }
        public int TargetResourceId { get; internal set; }
        public int TargetBuildingId { get; internal set; }
        internal int GatherElapsed;
        internal long RetryAtTick;
        internal bool IsAttackChasing;
        internal bool AttackIsExplicit;
        internal long NextAcquireTick;
        internal long NextChaseTick;
        internal int MovementSpeed => MoveSpeedMillimetresPerSecond;
        internal int SpeedRemainder;
        internal List<SimPoint> Route;
        internal int RouteIndex;
        internal SimPoint SegmentStart;
        internal int SegmentLength;
        internal int SegmentProgress;
        public int MovementBlockedTicks { get; internal set; }
        public MovementFormation Formation { get; internal set; }
        /// <summary>The grid this unit moves on: land, or deep water for a ship.</summary>
        public MovementDomain Domain { get; }
        /// <summary>How many land units this ship carries at most; zero for everything else.</summary>
        public int CargoCapacity { get; }
        /// <summary>The land units aboard this ship, in the order they boarded. They are out of the world until landed.</summary>
        public IReadOnlyList<UnitState> Cargo => cargo ?? (IReadOnlyList<UnitState>)System.Array.Empty<UnitState>();
        public int CargoCount => cargo?.Count ?? 0;
        internal List<UnitState> cargo;
        /// <summary>The ship this unit is aboard, while it is out of the world as cargo.</summary>
        public int CarrierId { get; internal set; }
        /// <summary>The ship this land unit is walking to and climbing onto; the climb counts down once it is alongside.</summary>
        public int EmbarkShipId { get; internal set; }
        public int EmbarkRemainingTicks { get; internal set; }
        /// <summary>A ship sailing to land its cargo at UnloadPoint, on the shore.</summary>
        public bool IsUnloading { get; internal set; }
        public SimPoint UnloadPoint { get; internal set; }
        // Where the naval errand last sent this unit: another order that moved it elsewhere ends the errand.
        internal SimPoint NavalRouteDestination;
        internal long NextNavalRetryTick;
        internal int MoveGroupId;
        internal int MoveGroupSize;
        internal SimPoint RequestedMoveTarget;
        internal SimPoint FlowAnchor;
        internal int FlowNearRadius;
        internal bool TemporaryRoute;
        internal int YieldToId;
        internal long YieldUntilTick;
        internal SimPoint YieldHeading;
        internal long NextMovementRepathTick;
        /// <summary>The rules entry DefinitionId names, resolved once instead of by name every tick.</summary>
        internal readonly UnitDefinition Definition;

        internal UnitState(UnitSpawnDefinition spawn, UnitDefinition definition)
        {
            Id = spawn.Id; OwnerId = spawn.OwnerId; DefinitionId = spawn.DefinitionId; Definition = definition;
            Position = PreviousPosition = Destination = spawn.Position;
            Health = MaxHealth = definition.MaxHealth;
            Tags = definition.Tags; Armor = definition.Armor; PopulationCost = definition.PopulationCost;
            AttackDamage = definition.Attack.Damage; GatherAmount = definition.GatherAmount;
            MoveSpeedMillimetresPerSecond = definition.MoveSpeedMillimetresPerSecond;
            CarryCapacity = definition.CarryCapacity;
            RadiusMillimetres = definition.RadiusMillimetres;
            IsWorker = definition.IsWorker;
            Domain = definition.Domain; CargoCapacity = definition.CargoCapacity;
        }
    }

    public sealed class BuildingState
    {
        public int Id { get; }
        public int OwnerId { get; }
        public string DefinitionId { get; }
        public SimPoint Position { get; }
        public int Health { get; internal set; }
        public int MaxHealth { get; }
        public CombatTags Tags { get; }
        public int Armor { get; }
        public int WidthCells { get; }
        public bool GateOpen { get; internal set; }
        public int AttackCooldownTicks { get; internal set; }
        public int OilCooldownTicks { get; internal set; }
        public int DepthCells { get; }
        /// <summary>A wall or gate standing north-south: its footprint is the definition's turned a quarter.</summary>
        public bool IsTurned => WidthCells != Definition.WidthCells;
        public bool IsComplete => BuildProgressTicks >= BuildRequiredTicks;
        public bool IsOperational => IsComplete && RelocationStage == RelocationStage.None;
        public StoreyardCharter ActiveCharter { get; internal set; }
        public StoreyardCharter PendingCharter { get; internal set; }
        public int CharterRemainingTicks { get; internal set; }
        public int CharterSourceId { get; internal set; }
        public int ProductionWorkPermille { get; internal set; } = 1000;
        public RelocationStage RelocationStage { get; internal set; }
        public int RelocationRemainingTicks { get; internal set; }
        public int RelocationTotalTicks { get; internal set; }
        public int BuildProgressTicks { get; internal set; }
        public int BuildRequiredTicks { get; }
        public float ConstructionProgress => (float)BuildProgressTicks / BuildRequiredTicks;
        public IReadOnlyList<TrainingQueueEntry> ProductionQueue { get; }
        public bool HasRallyPoint { get; internal set; }
        public SimPoint RallyPoint { get; internal set; }
        public ResearchState ActiveResearch { get; internal set; }
        internal readonly List<TrainingQueueEntry> Queue = new List<TrainingQueueEntry>();
        /// <summary>The rules entry DefinitionId names, resolved once instead of by name every tick.</summary>
        internal readonly BuildingDefinition Definition;
        internal BuildingState(BuildingSpawnDefinition spawn, BuildingDefinition definition)
        {
            Id = spawn.Id; OwnerId = spawn.OwnerId; DefinitionId = spawn.DefinitionId; Definition = definition;
            Position = spawn.Position; Health = MaxHealth = definition.MaxHealth;
            Tags = definition.Tags; Armor = definition.Armor;
            WidthCells = BuildingFootprints.WidthCells(definition, spawn.Turned); DepthCells = BuildingFootprints.DepthCells(definition, spawn.Turned);
            BuildRequiredTicks = BuildProgressTicks = definition.BuildTicks;
            ProductionQueue = Queue.AsReadOnly();
        }
    }

    public sealed class TrainingQueueEntry
    {
        public string UnitDefinitionId { get; }
        public int RemainingTicks { get; internal set; }
        public int TotalTicks { get; }
        public int PopulationCost { get; }
        internal int WorkRemainder;
        internal TrainingQueueEntry(UnitDefinition definition)
        {
            UnitDefinitionId = definition.Id; RemainingTicks = TotalTicks = definition.TrainTicks;
            PopulationCost = definition.PopulationCost;
        }
    }

    public sealed class ResourceNodeState
    {
        public int Id { get; }
        public string DefinitionId { get; }
        public SimPoint Position { get; }
        public ResourceKind Kind { get; }
        public int RemainingAmount { get; internal set; }
        internal ResourceNodeState(ResourceSpawnDefinition spawn, ResourceDefinition definition)
        {
            Id = spawn.Id; DefinitionId = spawn.DefinitionId; Position = spawn.Position;
            Kind = definition.Kind; RemainingAmount = definition.InitialAmount;
        }
    }
}
