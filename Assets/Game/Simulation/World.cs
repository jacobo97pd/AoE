using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Single-thread-owned rules state. Submit orders between fixed ticks.</summary>
    public sealed partial class World
    {
        public const int TickRate = 20;
        public const float TickSeconds = 1f / TickRate;
        private const int MaximumSelection = 1024;
        internal readonly List<UnitState> units = new List<UnitState>();
        internal readonly List<BuildingState> buildings = new List<BuildingState>();
        internal readonly List<ResourceNodeState> resources = new List<ResourceNodeState>();
        private readonly Dictionary<int, UnitState> unitsById = new Dictionary<int, UnitState>();
        private readonly Dictionary<int, BuildingState> buildingsById = new Dictionary<int, BuildingState>();
        private readonly Dictionary<int, ResourceNodeState> resourcesById = new Dictionary<int, ResourceNodeState>();
        internal readonly Navigation navigation;
        // The ships' grid: open on deep water (the map's water cells that are blocked to feet), closed everywhere else.
        // Nothing is ever built or grown on deep water, so it never changes during a match.
        internal readonly Navigation waterNavigation;
        internal readonly int mapWidth;
        internal readonly int mapHeight;
        internal readonly int cellSize;
        internal readonly Dictionary<string, UnitDefinition> unitDefinitions;
        internal readonly Dictionary<string, BuildingDefinition> buildingDefinitions;
        internal readonly Dictionary<string, ResourceDefinition> resourceDefinitions;
        internal readonly TechnologyCatalog technologyCatalog;
        internal readonly FactionCatalog factionCatalog;
        internal readonly FactionSystem factions;
        internal readonly MovementSystem movement;
        private readonly EconomySystem economy;
        private readonly ConstructionSystem construction;
        private readonly ProductionSystem production;
        private readonly CombatSystem combat;
        private readonly ResearchSystem research;
        private readonly SiegeSystem siege;
        private readonly NavalSystem naval;
        // Land units carried by a ship, out of the world until they land, by id.
        private readonly Dictionary<int, UnitState> passengers = new Dictionary<int, UnitState>();
        internal readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly Dictionary<int, PlayerState> playersById = new Dictionary<int, PlayerState>();
        // Selection scratch: a whole-army order checks and hands on its units without allocating.
        private readonly List<UnitState> selection = new List<UnitState>();
        private readonly HashSet<int> selectedIds = new HashSet<int>();
        private long nextEntityId = 1;
        public GameDefinition Definition { get; }
        public MapDefinition Map { get; }
        public long TickIndex { get; private set; }
        public IReadOnlyList<UnitState> Units { get; }
        public IReadOnlyList<BuildingState> Buildings { get; }
        public IReadOnlyList<ResourceNodeState> Resources { get; }
        public IReadOnlyList<PlayerState> Players { get; }
        public IReadOnlyList<ProjectileState> Projectiles { get; }
        public int DeathCount { get; private set; }
        public NavigationMetrics NavigationMetrics { get; } = new NavigationMetrics();
        /// <summary>The ships' own searches, kept apart so a match without ships reports exactly what it did before.</summary>
        public NavigationMetrics WaterNavigationMetrics { get; } = new NavigationMetrics();
        /// <summary>Whether any ship can sail this map.</summary>
        public bool HasDeepWater { get; }
        public OfflineMatchState Match { get; }
        public FogOfWarSystem Vision { get; }
        /// <summary>The map's lands for this match, resolved from its starts before a replica scrubs them. Scenery only.</summary>
        public MapLands Lands { get; }

        public World(GameDefinition definition, MapDefinition map)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (!definition.StartingResources.IsNonnegative || definition.BasePopulationCapacity < 0 ||
                definition.MaxProductionQueue < 1 || definition.MaxProductionQueue > 1000)
                throw new ArgumentException("Starting inventory, population capacity, or production queue limit is invalid.");
            if (map.WidthCells < 1 || map.HeightCells < 1 || (long)map.WidthCells * map.HeightCells > 262144 ||
                map.CellSizeMillimetres < 2 || map.CellSizeMillimetres > 1000000 ||
                (long)map.WidthCells * map.CellSizeMillimetres > 1000000 || (long)map.HeightCells * map.CellSizeMillimetres > 1000000)
                throw new ArgumentException("Map dimensions must fit 262144 cells and 1000000 millimetres per axis.");
            mapWidth = map.WidthCells; mapHeight = map.HeightCells; cellSize = map.CellSizeMillimetres;
            navigation = new Navigation(mapWidth, mapHeight, cellSize, NavigationMetrics);
            unitDefinitions = DefinitionValidation.Units(definition.Units, cellSize);
            buildingDefinitions = DefinitionValidation.Buildings(definition.Buildings, mapWidth, mapHeight);
            resourceDefinitions = DefinitionValidation.Resources(definition.Resources);
            technologyCatalog = new TechnologyCatalog(definition, unitDefinitions, buildingDefinitions);
            factionCatalog = new FactionCatalog(definition, map, unitDefinitions, buildingDefinitions, technologyCatalog);
            Lands = MapLands.Resolve(map);
            foreach (var building in buildingDefinitions.Values)
            {
                var trainable = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in building.TrainableUnitIds ?? Array.Empty<string>())
                {
                    if (id == null || !unitDefinitions.ContainsKey(id) || !trainable.Add(id))
                        throw new ArgumentException("Building training lists require existing unique unit definitions.");
                    // Ships are launched from a shore building onto the water beside it, and a shore building launches only ships.
                    if ((unitDefinitions[id].Domain == MovementDomain.Water) != building.RequiresShore)
                        throw new ArgumentException("Only a shore building trains ships, and it trains nothing else: " + building.Id);
                }
            }
            var entityIds = new HashSet<int>();
            foreach (var blocked in map.BlockedCells ?? Array.Empty<GridCell>()) navigation.Block(blocked.X, blocked.Z);
            waterNavigation = new Navigation(mapWidth, mapHeight, cellSize, WaterNavigationMetrics);
            var deep = new bool[mapWidth * mapHeight];
            foreach (var cell in map.WaterCells ?? Array.Empty<GridCell>())
                if (cell.X >= 0 && cell.Z >= 0 && cell.X < mapWidth && cell.Z < mapHeight &&
                    !navigation.IsWalkable(new SimPoint(cell.X * cellSize + cellSize / 2, cell.Z * cellSize + cellSize / 2))) deep[cell.Z * mapWidth + cell.X] = true;
            for (int z = 0; z < mapHeight; z++)
            for (int x = 0; x < mapWidth; x++)
                if (deep[z * mapWidth + x]) HasDeepWater = true; else waterNavigation.Block(x, z);
            waterNavigation.BuildConnectivity();
            foreach (var spawn in map.BuildingSpawns ?? Array.Empty<BuildingSpawnDefinition>())
            {
                if (spawn == null) throw new ArgumentException("Building spawn is null.");
                ValidateIdentity(spawn.Id, spawn.OwnerId, entityIds);
                if (spawn.DefinitionId == null || !buildingDefinitions.TryGetValue(spawn.DefinitionId, out var item))
                    throw new ArgumentException("Unknown building definition: " + spawn.DefinitionId);
                factionCatalog.ValidateSpawn(spawn.OwnerId, item.RequiredFactionId);
                if (spawn.Turned && !BuildingFootprints.CanTurn(item)) throw new ArgumentException("Only walls and gates can be turned: " + spawn.DefinitionId);
                int width = BuildingFootprints.WidthCells(item, spawn.Turned), depth = BuildingFootprints.DepthCells(item, spawn.Turned);
                int left = spawn.Position.X - width * cellSize / 2;
                int bottom = spawn.Position.Z - depth * cellSize / 2;
                if (left < 0 || bottom < 0 || left % cellSize != 0 || bottom % cellSize != 0)
                    throw new ArgumentException("Building position must center an aligned grid footprint.");
                for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++) navigation.Block(left / cellSize + x, bottom / cellSize + z);
                var state = new BuildingState(spawn, item);
                buildings.Add(state); buildingsById.Add(state.Id, state);
            }
            foreach (var spawn in map.ResourceSpawns ?? Array.Empty<ResourceSpawnDefinition>())
            {
                if (spawn == null) throw new ArgumentException("Resource spawn is null.");
                ValidateIdentity(spawn.Id, 1, entityIds);
                if (spawn.DefinitionId == null || !resourceDefinitions.TryGetValue(spawn.DefinitionId, out var item))
                    throw new ArgumentException("Unknown resource definition: " + spawn.DefinitionId);
                if (!navigation.Contains(spawn.Position)) throw new ArgumentException("Resource is outside the map.");
                navigation.Block(spawn.Position.X / cellSize, spawn.Position.Z / cellSize);
                var state = new ResourceNodeState(spawn, item);
                resources.Add(state); resourcesById.Add(state.Id, state);
            }
            navigation.BuildConnectivity();
            foreach (var spawn in map.UnitSpawns ?? Array.Empty<UnitSpawnDefinition>())
            {
                if (spawn == null) throw new ArgumentException("Unit spawn is null.");
                ValidateIdentity(spawn.Id, spawn.OwnerId, entityIds);
                if (spawn.DefinitionId == null || !unitDefinitions.TryGetValue(spawn.DefinitionId, out var item))
                    throw new ArgumentException("Unknown unit definition: " + spawn.DefinitionId);
                factionCatalog.ValidateSpawn(spawn.OwnerId, item.RequiredFactionId, item.Id);
                if (!NavigationFor(item).CanOccupy(spawn.Position, item.RadiusMillimetres))
                    throw new ArgumentException("A unit spawn overlaps an obstacle or map boundary.");
                var state = new UnitState(spawn, item);
                units.Add(state); unitsById.Add(state.Id, state);
            }
            ValidateUnitCollectionRestrictions(units, buildings);
            units.Sort((a, b) => a.Id.CompareTo(b.Id));
            buildings.Sort((a, b) => a.Id.CompareTo(b.Id));
            resources.Sort((a, b) => a.Id.CompareTo(b.Id));
            Units = units.AsReadOnly(); Buildings = buildings.AsReadOnly(); Resources = resources.AsReadOnly();
            foreach (int id in entityIds) nextEntityId = Math.Max(nextEntityId, (long)id + 1);
            foreach (var unit in units)
            {
                var player = GetOrCreatePlayer(unit.OwnerId);
                player.PopulationUsed = CheckedPopulation((long)player.PopulationUsed + unitDefinitions[unit.DefinitionId].PopulationCost);
            }
            foreach (var building in buildings)
            {
                var player = GetOrCreatePlayer(building.OwnerId);
                player.PopulationCapacity = CheckedPopulation((long)player.PopulationCapacity + buildingDefinitions[building.DefinitionId].PopulationCapacity);
            }
            foreach (int playerId in factionCatalog.Assignments.Keys) GetOrCreatePlayer(playerId);
            players.Sort((a, b) => a.Id.CompareTo(b.Id));
            Players = players.AsReadOnly();
            movement = new MovementSystem(this);
            economy = new EconomySystem(this);
            construction = new ConstructionSystem(this);
            production = new ProductionSystem(this);
            combat = new CombatSystem(this);
            research = new ResearchSystem(this);
            factions = new FactionSystem(this);
            siege = new SiegeSystem(this);
            naval = new NavalSystem(this);
            factions.RefreshBonuses();
            Projectiles = projectiles.AsReadOnly();
            if (map.OfflineMatch != null && map.OfflineMatch.Enabled)
            {
                Match = new OfflineMatchState(this, map.OfflineMatch);
                Vision = new FogOfWarSystem(this, map.OfflineMatch);
            }
        }

        public bool TryGetUnit(int id, out UnitState state) => unitsById.TryGetValue(id, out state);
        public bool TryGetBuilding(int id, out BuildingState state) => buildingsById.TryGetValue(id, out state);
        public bool TryGetResource(int id, out ResourceNodeState state) => resourcesById.TryGetValue(id, out state);
        public bool TryGetPlayer(int id, out PlayerState state) => playersById.TryGetValue(id, out state);
        public bool IsWalkable(SimPoint point) => navigation.IsWalkable(point);
        /// <summary>Whether a ship can sail here: deep water, as opposed to land or a ford.</summary>
        public bool IsSailable(SimPoint point) => waterNavigation.IsWalkable(point);
        /// <summary>A land unit carried aboard a ship, by id; carried units are not in Units.</summary>
        public bool TryGetPassenger(int id, out UnitState passenger) => passengers.TryGetValue(id, out passenger);
        internal Navigation NavigationFor(UnitState unit) => unit.Domain == MovementDomain.Water ? waterNavigation : navigation;
        internal Navigation NavigationFor(UnitDefinition unit) => unit.Domain == MovementDomain.Water ? waterNavigation : navigation;

        /// <summary>Everything a match ever did, in order. A saved game is this list and the start it began from.</summary>
        public MatchJournal Journal { get; } = new MatchJournal();

        public CommandResult Submit(IGameCommand command)
        {
            if (IsNetworkReplica) return NetworkCommandSink == null ? NetworkCommandResult.Unavailable("The network connection is unavailable.") : NetworkCommandSink(command);
            if (Match != null && Match.IsFinished) return FinishedResult();
            var result = SubmitCore(command);
            if (!result.Accepted) return result;
            Journal.Record(TickIndex, command);
            Vision?.Refresh();
            return result;
        }

        private CommandResult SubmitCore(IGameCommand command)
        {
            if (command == null) return CommandResult.Reject(CommandRejection.InvalidCommand, "No command was supplied.");
            if (command.PlayerId < 1) return CommandResult.Reject(CommandRejection.InvalidPlayer, "Player IDs must be positive.");
            if (command is SurrenderCommand) return Match == null ?
                CommandResult.Reject(CommandRejection.MatchNotRunning, "This world is a drill, not an offline match.") : Match.Surrender(command.PlayerId);
            if (command is GatherCommand gather)
            {
                if (Vision != null && !Vision.IsEntityVisible(command.PlayerId, gather.ResourceId))
                    return CommandResult.Reject(CommandRejection.TargetNotVisible, "Scout this resource before ordering workers to gather.");
                return economy.Submit(gather);
            }
            if (command is ReturnCargoCommand returnCargo) return economy.Submit(returnCargo);
            if (command is BuildCommand build)
            {
                var visible = ValidateVisiblePlacement(build.PlayerId, build.BuildingDefinitionId, build.Position, build.Turned);
                return visible.Accepted ? construction.Submit(build) : visible;
            }
            // Each stretch of a run answers for its own visibility.
            if (command is BuildRunCommand run) return construction.Submit(run);
            if (command is ConstructCommand construct) return construction.Submit(construct);
            if (command is TrainCommand train) return production.Submit(train);
            if (command is SetRallyCommand rally) return production.Submit(rally);
            if (command is AttackCommand attack) return combat.Submit(attack);
            if (command is ResearchCommand researchCommand) return research.Submit(researchCommand);
            if (command is BoardWallCommand || command is LeaveWallCommand || command is SetGateCommand) return siege.Submit(command);
            if (command is EmbarkCommand || command is DisembarkCommand) return naval.Submit(command);
            if (command is DeployOutpostCommand deployment)
            {
                var visible = ValidateDeploymentVision(deployment);
                if (!visible.Accepted) return visible;
            }
            if (command is SetCharterCommand || command is DeployThreadkeeperCommand || command is PackOutpostCommand ||
                command is DeployOutpostCommand || command is RepositionCommand) return factions.Submit(command);
            var move = command as MoveCommand;
            var stop = command as StopCommand;
            if (move == null && stop == null) return CommandResult.Reject(CommandRejection.InvalidCommand, "Unsupported command type.");
            var ids = move != null ? move.UnitIds : stop.UnitIds;
            if (ids.Count == 0) return CommandResult.Reject(CommandRejection.EmptySelection, "Select at least one unit.");
            if (ids.Count > MaximumSelection) return CommandResult.Reject(CommandRejection.TooManyUnits, "Selection exceeds the command limit.");
            // Movement reads the selection during this call and keeps none of it.
            var selected = selection; selected.Clear();
            var seen = selectedIds; seen.Clear();
            foreach (int id in ids)
            {
                if (!seen.Add(id)) return CommandResult.Reject(CommandRejection.DuplicateUnit, "Selection contains a duplicate unit.");
                if (!unitsById.TryGetValue(id, out var unit)) return CommandResult.Reject(CommandRejection.UnknownUnit, "A selected unit does not exist.");
                if (unit.OwnerId != command.PlayerId) return CommandResult.Reject(CommandRejection.NotOwner, "You do not own every selected unit.");
                if (unit.ThreadkeeperRemainingTicks > 0 || (move != null && unit.RelocationStage == RelocationStage.Deploying))
                    return CommandResult.Reject(CommandRejection.FactionActionBusy, "A selected unit is committed to deployment.");
                selected.Add(unit);
            }
            selected.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (stop != null)
            {
                foreach (var unit in selected) { FactionSystem.CancelDeployment(unit); SiegeSystem.CancelBoarding(unit); NavalSystem.CancelOrders(unit); MovementSystem.Stop(unit); }
                return CommandResult.Success();
            }
            bool mixed = false;
            for (int i = 1; i < selected.Count && !mixed; i++) mixed = selected[i].Domain != selected[0].Domain;
            if (!mixed) return Moved(selected, movement.Submit(selected, move.Destination, move.Formation));
            // Ships and troops ordered together each take their own grid: whichever can reach the point goes.
            var land = new List<UnitState>(selected.Count); var water = new List<UnitState>();
            foreach (var unit in selected) (unit.Domain == MovementDomain.Water ? water : land).Add(unit);
            var landResult = Moved(land, movement.Submit(land, move.Destination, move.Formation));
            var waterResult = Moved(water, movement.Submit(water, move.Destination, move.Formation));
            return landResult.Accepted || !waterResult.Accepted ? landResult : waterResult;
        }

        private static CommandResult Moved(List<UnitState> units, CommandResult result)
        {
            if (result.Accepted) foreach (var unit in units) NavalSystem.CancelOrders(unit);
            return result;
        }

        public CommandResult ValidatePlacement(int playerId, int[] workerIds, string buildingDefinitionId, SimPoint position, bool turned = false)
        {
            if (Match != null && Match.IsFinished) return FinishedResult();
            var visible = ValidateVisiblePlacement(playerId, buildingDefinitionId, position, turned);
            return visible.Accepted ? construction.Validate(new BuildCommand(playerId, workerIds, buildingDefinitionId, position, turned)) : visible;
        }

        /// <summary>
        /// What submitting this wall run would do now, stretch by stretch, without changing the match: the same
        /// planning the command runs. Pass the previous preview to reuse its storage.
        /// </summary>
        public BuildRunPreview PreviewBuildRun(BuildRunCommand command, BuildRunPreview reuse = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var preview = reuse ?? new BuildRunPreview();
            if (Match != null && Match.IsFinished)
            {
                preview.verdicts.Clear();
                for (int i = 0; i < command.Sites.Count; i++) preview.verdicts.Add(FinishedResult());
                preview.Result = FinishedResult(); preview.Cost = default; preview.AcceptedCount = 0;
                return preview;
            }
            construction.Preview(command, preview);
            return preview;
        }

        public CommandResult ValidateResearch(ResearchCommand command) => Match != null && Match.IsFinished ? FinishedResult() : research.Validate(command);
        public CommandResult ValidateFactionAction(IGameCommand command)
        {
            if (Match != null && Match.IsFinished) return FinishedResult();
            if (command is DeployOutpostCommand deployment)
            { var visible = ValidateDeploymentVision(deployment); if (!visible.Accepted) return visible; }
            return factions.Validate(command);
        }
        public CommandResult ValidateFactionRequirement(int playerId, string requiredFactionId) => factions.ValidateRequirement(playerId, requiredFactionId);
        public CommandResult ValidateRequirements(int playerId, string requiredEraId, string[] requiredTechnologyIds) =>
            research.ValidateRequirements(playerId, requiredEraId, requiredTechnologyIds);

        /// <summary>Movement, economy, construction, combat, faction actions, production, research; influence refreshes at phase boundaries.</summary>
        public void Tick()
        {
            if (IsNetworkReplica) return;
            if (Match != null && Match.IsFinished) return;
            Vision?.Refresh();
            factions.RefreshBonuses();
            movement.Tick();
            Vision?.Refresh();
            factions.RefreshBonuses();
            economy.Tick();
            construction.Tick();
            siege.Tick();
            naval.Tick();
            Vision?.Refresh();
            combat.Tick();
            factions.Tick();
            production.Tick();
            research.Tick();
            factions.RefreshBonuses();
            Vision?.Refresh();
            TickIndex++;
            Match?.Tick();
        }

        private CommandResult ValidateVisiblePlacement(int playerId, string definitionId, SimPoint position, bool turned) =>
            Vision == null ? CommandResult.Success() : Vision.ValidatePlacement(playerId, definitionId, position, turned);
        private CommandResult ValidateDeploymentVision(DeployOutpostCommand command)
        {
            if (Vision == null || !TryGetUnit(command.UnitId, out var unit) || !unit.IsPackedOutpost) return CommandResult.Success();
            return Vision.ValidatePlacement(command.PlayerId, unit.PackedBuildingDefinitionId, command.Position);
        }
        private static CommandResult FinishedResult() => CommandResult.Reject(CommandRejection.MatchFinished, "The match has ended.");

        internal CommandResult SelectWorkers(int playerId, IReadOnlyList<int> ids, out List<UnitState> selected)
        {
            selected = new List<UnitState>();
            if (playerId < 1 || !playersById.ContainsKey(playerId))
                return CommandResult.Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (ids.Count == 0) return CommandResult.Reject(CommandRejection.EmptySelection, "Select at least one worker.");
            if (ids.Count > MaximumSelection) return CommandResult.Reject(CommandRejection.TooManyUnits, "Selection exceeds the command limit.");
            var seen = selectedIds; seen.Clear();
            foreach (int id in ids)
            {
                if (!seen.Add(id)) return CommandResult.Reject(CommandRejection.DuplicateUnit, "Selection contains a duplicate worker.");
                if (!unitsById.TryGetValue(id, out var unit)) return CommandResult.Reject(CommandRejection.UnknownUnit, "A selected worker does not exist.");
                if (unit.OwnerId != playerId) return CommandResult.Reject(CommandRejection.NotOwner, "You do not own every selected worker.");
                if (!unit.IsWorker) return CommandResult.Reject(CommandRejection.NotWorker, "This order requires workers.");
                selected.Add(unit);
            }
            selected.Sort((a, b) => a.Id.CompareTo(b.Id));
            return CommandResult.Success();
        }

        internal PlayerState Player(int id) => playersById[id];
        internal void RefreshUnitResearchStats(UnitState unit) => research.ApplyStats(unit);
        internal bool HasEntityId => nextEntityId <= int.MaxValue;
        internal bool HasEntityIds(int count) => nextEntityId + count - 1 <= int.MaxValue;
        internal int AllocateEntityId()
        {
            if (!HasEntityId) throw new InvalidOperationException("Entity ID space exhausted.");
            return (int)nextEntityId++;
        }
        internal void AddBuilding(BuildingState building) { buildings.Add(building); buildingsById.Add(building.Id, building); }
        internal void AddUnit(UnitState unit) { units.Add(unit); unitsById.Add(unit.Id, unit); research.ApplyStats(unit); factions.ApplyStats(unit); }

        internal void ReplaceBuildingWithTransport(BuildingState building, UnitState transport)
        {
            buildingsById.Remove(building.Id); buildings.Remove(building);
            navigation.ClearFootprint(GridFootprint.Building(building, cellSize));
            AddUnit(transport);
            units.Sort((a, b) => a.Id.CompareTo(b.Id));
            InvalidateBuildingInteractions(building.Id);
            movement.ReplanAll();
        }

        internal void ReplaceTransportWithBuilding(UnitState transport, BuildingState building, GridFootprint footprint)
        {
            unitsById.Remove(transport.Id); units.Remove(transport);
            MovementSystem.Stop(transport);
            AddBuilding(building);
            buildings.Sort((a, b) => a.Id.CompareTo(b.Id));
            navigation.AddFootprint(footprint);
            movement.ReplanAll();
        }

        internal void DestroyUnit(UnitState unit)
        {
            if (!unitsById.Remove(unit.Id)) return;
            units.Remove(unit);
            if (unit.CargoCapacity > 0) naval.ShipDestroyed(unit.Id);
            Player(unit.OwnerId).PopulationUsed -= unit.PopulationCost;
            MovementSystem.Stop(unit);
            unit.CarriedAmount = 0;
            DeathCount++;
            if (unit.CargoCount == 0) return;
            // A sunk ship is out on the water, with no shore to swim to: everyone aboard drowns with it.
            foreach (var passenger in unit.cargo)
            {
                passengers.Remove(passenger.Id);
                Player(passenger.OwnerId).PopulationUsed -= passenger.PopulationCost;
                passenger.CarrierId = 0; passenger.CarriedAmount = 0; passenger.Health = 0;
                DeathCount++;
            }
            unit.cargo.Clear();
        }

        /// <summary>Takes a land unit out of the world and aboard the ship. It keeps its id, health, load and population.</summary>
        internal void Embark(UnitState unit, UnitState ship)
        {
            if (!unitsById.Remove(unit.Id)) return;
            units.Remove(unit);
            MovementSystem.Stop(unit);
            unit.EmbarkShipId = unit.EmbarkRemainingTicks = 0;
            unit.CarrierId = ship.Id;
            unit.Position = unit.PreviousPosition = unit.Destination = ship.Position;
            (ship.cargo ??= new List<UnitState>()).Add(unit);
            passengers.Add(unit.Id, unit);
        }

        // Cargo is absent from movement, combat and vision, but its authoritative position still follows the hull.
        internal void UpdatePassengerPositions()
        {
            foreach (var passenger in passengers.Values)
                if (unitsById.TryGetValue(passenger.CarrierId, out var ship))
                {
                    passenger.PreviousPosition = passenger.Position;
                    passenger.Position = passenger.Destination = ship.Position;
                }
        }

        /// <summary>Sets one passenger down on the shore and back into the world, in id order with the rest.</summary>
        internal void Disembark(UnitState ship, int index, SimPoint position)
        {
            var unit = ship.cargo[index];
            ship.cargo.RemoveAt(index);
            passengers.Remove(unit.Id);
            unit.CarrierId = 0;
            unit.Position = unit.PreviousPosition = unit.Destination = position;
            unit.AutoAttackEnabled = true;
            int at = units.Count;
            while (at > 0 && units[at - 1].Id > unit.Id) at--;
            units.Insert(at, unit); unitsById.Add(unit.Id, unit);
            research.ApplyStats(unit); factions.ApplyStats(unit);
        }

        internal void DestroyBuilding(BuildingState building)
        {
            if (!buildingsById.Remove(building.Id)) return;
            buildings.Remove(building);
            var player = Player(building.OwnerId);
            if (building.IsComplete) player.PopulationCapacity -= buildingDefinitions[building.DefinitionId].PopulationCapacity;
            foreach (var entry in building.Queue) player.PopulationReserved -= entry.PopulationCost;
            building.Queue.Clear();
            research.ClearPending(building);
            factions.BuildingDestroyed(building.Id);
            navigation.ClearFootprint(GridFootprint.Building(building, cellSize));
            siege.WallDestroyed(building);
            movement.ReplanAll();
            InvalidateBuildingInteractions(building.Id);
            DeathCount++;
        }

        internal void InvalidateBuildingInteractions(int buildingId)
        {
            foreach (var unit in units)
            {
                if (unit.TargetBuildingId != buildingId) continue;
                MovementSystem.Halt(unit);
                if (unit.WorkerTask == WorkerTask.ReturningResources)
                { unit.TargetBuildingId = 0; unit.RetryAtTick = 0; }
                // A worker laying a wall run walks on to the run's next foundation.
                else { MovementSystem.CancelWorkerTask(unit); construction.ContinueRun(unit); }
            }
        }

        private PlayerState GetOrCreatePlayer(int id)
        {
            if (playersById.TryGetValue(id, out var player)) return player;
            factionCatalog.Assignments.TryGetValue(id, out string factionId);
            player = new PlayerState(id, Definition.StartingResources, Definition.BasePopulationCapacity, factionId);
            player.EraId = technologyCatalog.InitialEraId;
            if (factionId != null && factionCatalog.Definitions.TryGetValue(factionId, out var faction)) player.Faction = faction;
            players.Add(player); playersById.Add(id, player);
            return player;
        }

        private static int CheckedPopulation(long value)
        {
            if (value > int.MaxValue) throw new ArgumentException("Initial population or capacity exceeds supported bounds.");
            return (int)value;
        }

        private static void ValidateIdentity(int id, int ownerId, HashSet<int> used)
        {
            if (id < 1 || !used.Add(id)) throw new ArgumentException("Entity IDs must be positive and unique across all entity types.");
            if (ownerId < 1) throw new ArgumentException("Owned entities must have a positive player ID.");
        }
    }
}
