using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>Faction actions, nonstacking local influence, and reversible identity-preserving outpost transformations.</summary>
    internal sealed class FactionSystem
    {
        private readonly World world;
        private readonly List<Influence> influence = new List<Influence>();
        private readonly List<int> unitIds = new List<int>();
        private readonly List<int> buildingIds = new List<int>();
        private readonly struct Influence
        {
            internal readonly int Owner, YardId, Radius;
            internal readonly SimPoint Position;
            internal readonly StoreyardCharter Charter;
            internal Influence(int owner, int yard, SimPoint position, int radius, StoreyardCharter charter)
            { Owner = owner; YardId = yard; Position = position; Radius = radius; Charter = charter; }
        }
        internal FactionSystem(World world) { this.world = world; }

        internal FactionDefinition DefinitionFor(int playerId)
        {
            if (!world.TryGetPlayer(playerId, out var player) || player.FactionId == null) return null;
            return player.Faction ?? world.factionCatalog.Definitions[player.FactionId];
        }

        internal CommandResult ValidateRequirement(int playerId, string requiredFactionId)
        {
            if (!world.TryGetPlayer(playerId, out var player)) return Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (FactionCatalog.Eligible(requiredFactionId, player.FactionId)) return CommandResult.Success();
            string label = requiredFactionId;
            if (requiredFactionId != null && world.factionCatalog.Definitions.TryGetValue(requiredFactionId, out var faction) && !string.IsNullOrWhiteSpace(faction.DisplayName)) label = faction.DisplayName;
            return Reject(CommandRejection.WrongFaction, "Requires faction: " + label + ".");
        }

        internal CommandResult Validate(IGameCommand command)
        {
            if (command == null) return Reject(CommandRejection.InvalidCommand, "No faction action was supplied.");
            if (!world.TryGetPlayer(command.PlayerId, out _)) return Reject(CommandRejection.InvalidPlayer, "Player does not exist.");
            if (command is SetCharterCommand charter) return ValidateCharter(charter);
            if (command is DeployThreadkeeperCommand relay) return ValidateRelay(relay);
            if (command is PackOutpostCommand pack) return ValidatePack(pack);
            if (command is DeployOutpostCommand deploy) return ValidateDeploy(deploy, out _);
            if (command is RepositionCommand reposition) return SelectReposition(reposition, out _);
            return Reject(CommandRejection.InvalidCommand, "Unsupported faction action.");
        }

        internal CommandResult Submit(IGameCommand command)
        {
            var result = Validate(command); if (!result.Accepted) return result;
            var faction = DefinitionFor(command.PlayerId);
            int entityId = 0;
            if (command is SetCharterCommand charter)
            {
                world.TryGetBuilding(charter.BuildingId, out var building);
                world.Player(command.PlayerId).TrySpend(faction.CharterCost);
                building.ActiveCharter = StoreyardCharter.None;
                building.PendingCharter = charter.Charter;
                building.CharterRemainingTicks = faction.CharterTicks;
                entityId = building.Id;
            }
            else if (command is DeployThreadkeeperCommand relay)
            {
                world.TryGetUnit(relay.UnitId, out var unit);
                MovementSystem.Stop(unit);
                unit.LinkedStoreyardId = relay.StoreyardId;
                unit.ThreadkeeperRemainingTicks = faction.ThreadkeeperDeployTicks;
                entityId = unit.Id;
            }
            else if (command is PackOutpostCommand pack)
            {
                world.TryGetBuilding(pack.BuildingId, out var building);
                building.RelocationStage = RelocationStage.Packing;
                building.RelocationRemainingTicks = building.RelocationTotalTicks = PreparationTicks(faction, world.Player(command.PlayerId), faction.PackTicks);
                world.InvalidateBuildingInteractions(building.Id);
                entityId = building.Id;
            }
            else if (command is DeployOutpostCommand deploy)
            {
                world.TryGetUnit(deploy.UnitId, out var unit);
                MovementSystem.Stop(unit);
                unit.RelocationStage = RelocationStage.Deploying;
                unit.RelocationRemainingTicks = unit.RelocationTotalTicks = PreparationTicks(faction, world.Player(command.PlayerId), faction.DeployTicks);
                unit.DeploymentPosition = deploy.Position;
                entityId = unit.Id;
            }
            else if (command is RepositionCommand reposition)
            {
                foreach (int id in reposition.UnitIds)
                {
                    world.TryGetUnit(id, out var unit);
                    CombatSystem.CancelAttack(unit, true);
                    unit.RepositionRemainingTicks = faction.RepositionTicks;
                }
            }
            RefreshBonuses();
            return CommandResult.Success(entityId);
        }

        private CommandResult RequireKind(int playerId, FactionKind kind, out FactionDefinition definition)
        {
            definition = DefinitionFor(playerId);
            return definition != null && definition.Kind == kind ? CommandResult.Success() : Reject(CommandRejection.WrongFaction, "This action belongs to the other faction.");
        }

        private CommandResult OwnedBuilding(int owner, int id, out BuildingState building)
        {
            if (!world.TryGetBuilding(id, out building)) return Reject(CommandRejection.UnknownBuilding, "Building does not exist.");
            if (building.OwnerId != owner) return Reject(CommandRejection.NotOwner, "You do not own this building.");
            if (!building.IsComplete) return Reject(CommandRejection.BuildingIncomplete, "Construction must finish first.");
            if (!building.IsOperational) return Reject(CommandRejection.FactionActionBusy, "This outpost is relocating.");
            return CommandResult.Success();
        }

        private CommandResult OwnedUnit(int owner, int id, out UnitState unit)
        {
            if (!world.TryGetUnit(id, out unit)) return Reject(CommandRejection.UnknownUnit, "Unit does not exist.");
            return unit.OwnerId == owner ? CommandResult.Success() : Reject(CommandRejection.NotOwner, "You do not own this unit.");
        }

        private CommandResult ValidateCharter(SetCharterCommand command)
        {
            var result = RequireKind(command.PlayerId, FactionKind.AvenCompact, out var faction); if (!result.Accepted) return result;
            result = OwnedBuilding(command.PlayerId, command.BuildingId, out var building); if (!result.Accepted) return result;
            if (command.Charter != StoreyardCharter.Logistics && command.Charter != StoreyardCharter.Muster) return Reject(CommandRejection.InvalidCommand, "Choose Logistics or Muster.");
            if (!world.buildingDefinitions[building.DefinitionId].CanCharter) return Reject(CommandRejection.CannotUseFactionAction, "This building does not support charters.");
            if (building.CharterRemainingTicks != 0 || building.ActiveResearch != null || building.Queue.Count != 0)
                return Reject(CommandRejection.FactionActionBusy, "Charter changes require an idle producer.");
            if (building.ActiveCharter == command.Charter) return Reject(CommandRejection.InvalidCommand, "This charter is already active.");
            if (!world.Player(command.PlayerId).CanAfford(faction.CharterCost)) return Reject(CommandRejection.InsufficientResources, "Not enough resources to change charter.");
            return CommandResult.Success();
        }

        private CommandResult ValidateRelay(DeployThreadkeeperCommand command)
        {
            var result = RequireKind(command.PlayerId, FactionKind.AvenCompact, out var faction); if (!result.Accepted) return result;
            result = OwnedUnit(command.PlayerId, command.UnitId, out var unit); if (!result.Accepted) return result;
            if (!world.unitDefinitions[unit.DefinitionId].IsThreadkeeper) return Reject(CommandRejection.CannotUseFactionAction, "Only a Threadkeeper can establish a relay.");
            if (unit.ThreadkeeperRemainingTicks != 0) return Reject(CommandRejection.FactionActionBusy, "This Threadkeeper is already deployed.");
            if (unit.ThreadkeeperCooldownTicks != 0) return Reject(CommandRejection.AbilityCooldown, "The relay is recovering.");
            result = OwnedBuilding(command.PlayerId, command.StoreyardId, out var yard); if (!result.Accepted) return result;
            if (!ActiveYard(yard)) return Reject(CommandRejection.CannotUseFactionAction, "An active chartered Storeyard is required.");
            if (!Within(unit.Position, yard.Position, YardRadius(faction, world.Player(command.PlayerId)))) return Reject(CommandRejection.OutOfRange, "Move within this Storeyard's charter radius.");
            return CommandResult.Success();
        }

        private CommandResult ValidatePack(PackOutpostCommand command)
        {
            var result = RequireKind(command.PlayerId, FactionKind.SerevinMarch, out _); if (!result.Accepted) return result;
            result = OwnedBuilding(command.PlayerId, command.BuildingId, out var building); if (!result.Accepted) return result;
            if (string.IsNullOrEmpty(world.buildingDefinitions[building.DefinitionId].TransportUnitId)) return Reject(CommandRejection.CannotUseFactionAction, "This building cannot relocate.");
            if (building.ActiveResearch != null || building.Queue.Count != 0 || building.CharterRemainingTicks != 0)
                return Reject(CommandRejection.FactionActionBusy, "Finish research, training, or charter work before packing.");
            return CommandResult.Success();
        }

        private CommandResult ValidateDeploy(DeployOutpostCommand command, out GridFootprint footprint)
        {
            footprint = default;
            var result = RequireKind(command.PlayerId, FactionKind.SerevinMarch, out _); if (!result.Accepted) return result;
            result = OwnedUnit(command.PlayerId, command.UnitId, out var unit); if (!result.Accepted) return result;
            if (!unit.IsPackedOutpost) return Reject(CommandRejection.CannotUseFactionAction, "Only a packed outpost can deploy.");
            if (unit.RelocationStage != RelocationStage.Packed || unit.Order != UnitOrder.Idle) return Reject(CommandRejection.FactionActionBusy, "Stop the transport before deploying.");
            return ValidateDeploymentFootprint(unit, command.Position, out footprint);
        }

        private CommandResult ValidateDeploymentFootprint(UnitState unit, SimPoint position, out GridFootprint footprint)
        {
            footprint = default;
            var definition = world.buildingDefinitions[unit.PackedBuilding.DefinitionId];
            long left = (long)position.X - (long)definition.WidthCells * world.cellSize / 2;
            long bottom = (long)position.Z - (long)definition.DepthCells * world.cellSize / 2;
            if (left < 0 || bottom < 0 || left % world.cellSize != 0 || bottom % world.cellSize != 0 ||
                left / world.cellSize + definition.WidthCells > world.mapWidth || bottom / world.cellSize + definition.DepthCells > world.mapHeight)
                return Reject(CommandRejection.InvalidPlacement, "Deployment must align to the grid and fit inside the map.");
            footprint = new GridFootprint((int)(left / world.cellSize), (int)(bottom / world.cellSize), definition.WidthCells, definition.DepthCells);
            if (!footprint.CanInteract(unit.Position, world.cellSize)) return Reject(CommandRejection.OutOfRange, "Move the transport adjacent to the planned footprint.");
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                if (!world.navigation.IsWalkable(new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2)))
                    return Reject(CommandRejection.DestinationBlocked, "Deployment overlaps terrain, resources, or another building.");
            foreach (var other in world.units)
            {
                long dx = other.Position.X - Math.Max(left, Math.Min(other.Position.X, left + (long)definition.WidthCells * world.cellSize));
                long dz = other.Position.Z - Math.Max(bottom, Math.Min(other.Position.Z, bottom + (long)definition.DepthCells * world.cellSize));
                if (dx * dx + dz * dz < (long)other.RadiusMillimetres * other.RadiusMillimetres)
                    return Reject(CommandRejection.UnitObstruction, "A unit occupies the deployment footprint or clearance.");
            }
            return CommandResult.Success();
        }

        private CommandResult SelectReposition(RepositionCommand command, out List<UnitState> selected)
        {
            selected = new List<UnitState>();
            var result = RequireKind(command.PlayerId, FactionKind.SerevinMarch, out _); if (!result.Accepted) return result;
            if (command.UnitIds.Count == 0) return Reject(CommandRejection.EmptySelection, "Select at least one Ashrunner.");
            if (command.UnitIds.Count > 1024) return Reject(CommandRejection.TooManyUnits, "Selection exceeds the command limit.");
            var seen = new HashSet<int>();
            foreach (int id in command.UnitIds)
            {
                if (!seen.Add(id)) return Reject(CommandRejection.DuplicateUnit, "Selection contains a duplicate unit.");
                result = OwnedUnit(command.PlayerId, id, out var unit); if (!result.Accepted) return result;
                if (!world.unitDefinitions[unit.DefinitionId].CanReposition) return Reject(CommandRejection.CannotUseFactionAction, "Every selected unit must support reposition.");
                if (unit.RepositionRemainingTicks != 0) return Reject(CommandRejection.FactionActionBusy, "Reposition is already active.");
                if (unit.RepositionCooldownTicks != 0) return Reject(CommandRejection.AbilityCooldown, "Reposition is recovering.");
                selected.Add(unit);
            }
            return CommandResult.Success();
        }

        internal static bool MovementLocked(UnitState unit) => unit.ThreadkeeperRemainingTicks > 0 || unit.RelocationStage == RelocationStage.Deploying || unit.WallId != 0 || unit.BoardingWallId != 0;
        internal static bool AttackSuppressed(UnitState unit) => unit.RepositionRemainingTicks > 0 || unit.ThreadkeeperRemainingTicks > 0 || unit.IsPackedOutpost || unit.BoardingWallId != 0;
        internal static void CancelDeployment(UnitState unit)
        {
            if (unit.RelocationStage != RelocationStage.Deploying) return;
            unit.RelocationStage = RelocationStage.Packed;
            unit.RelocationRemainingTicks = 0;
            unit.RelocationTotalTicks = 0;
            unit.DeploymentPosition = default;
        }

        internal void Tick()
        {
            if (world.factionCatalog.Assignments.Count == 0) return;
            unitIds.Clear(); buildingIds.Clear();
            foreach (var unit in world.units) unitIds.Add(unit.Id);
            foreach (var building in world.buildings) buildingIds.Add(building.Id);
            foreach (int id in unitIds)
            {
                if (!world.TryGetUnit(id, out var unit)) continue;
                var faction = DefinitionFor(unit.OwnerId); if (faction == null) continue;
                if (unit.ThreadkeeperRemainingTicks > 0)
                {
                    unit.ThreadkeeperRemainingTicks--;
                    if (unit.ThreadkeeperRemainingTicks == 0 || !world.TryGetBuilding(unit.LinkedStoreyardId, out _)) EndRelay(unit, faction);
                }
                else if (unit.ThreadkeeperCooldownTicks > 0 && unit.ThreadkeeperCooldownStartedTick < world.TickIndex) unit.ThreadkeeperCooldownTicks--;
                if (unit.RepositionRemainingTicks > 0)
                {
                    unit.RepositionRemainingTicks--;
                    if (unit.RepositionRemainingTicks == 0)
                    unit.RepositionCooldownTicks = faction.RepositionCooldownTicks;
                }
                else if (unit.RepositionCooldownTicks > 0) unit.RepositionCooldownTicks--;
                if (unit.RelocationStage != RelocationStage.Deploying) continue;
                if (unit.RelocationRemainingTicks > 0) unit.RelocationRemainingTicks--;
                if (unit.RelocationRemainingTicks == 0 && ValidateDeploymentFootprint(unit, unit.DeploymentPosition, out var footprint).Accepted)
                    CompleteDeployment(unit, footprint);
            }
            foreach (int id in buildingIds)
            {
                if (!world.TryGetBuilding(id, out var building)) continue;
                if (building.CharterRemainingTicks > 0)
                {
                    building.CharterRemainingTicks--;
                    if (building.CharterRemainingTicks == 0)
                    { building.ActiveCharter = building.PendingCharter; building.PendingCharter = StoreyardCharter.None; }
                }
                if (building.RelocationStage != RelocationStage.Packing) continue;
                if (building.RelocationRemainingTicks > 0) building.RelocationRemainingTicks--;
                if (building.RelocationRemainingTicks == 0) CompletePacking(building);
            }
            RefreshBonuses();
        }

        private void CompletePacking(BuildingState building)
        {
            var definition = world.unitDefinitions[world.buildingDefinitions[building.DefinitionId].TransportUnitId];
            var transport = new UnitState(new UnitSpawnDefinition
            { Id = building.Id, OwnerId = building.OwnerId, DefinitionId = definition.Id, Position = building.Position }, definition)
            { Health = building.Health, PackedBuilding = building, RelocationStage = RelocationStage.Packed, AutoAttackEnabled = false };
            world.ReplaceBuildingWithTransport(building, transport);
        }

        private void CompleteDeployment(UnitState unit, GridFootprint footprint)
        {
            var original = unit.PackedBuilding;
            var definition = world.buildingDefinitions[original.DefinitionId];
            var building = new BuildingState(new BuildingSpawnDefinition
            { Id = unit.Id, OwnerId = unit.OwnerId, DefinitionId = original.DefinitionId, Position = unit.DeploymentPosition }, definition)
            { Health = unit.Health, HasRallyPoint = original.HasRallyPoint, RallyPoint = original.RallyPoint };
            world.ReplaceTransportWithBuilding(unit, building, footprint);
        }

        private static int PreparationTicks(FactionDefinition faction, PlayerState player, int basis) =>
            basis - (player.HasTechnology(faction.PreparedEncampmentsTechnologyId) ? faction.PreparationReductionTicks : 0);
        private static int YardRadius(FactionDefinition faction, PlayerState player) =>
            faction.CharterRadiusMillimetres + (player.HasTechnology(faction.ReciprocalStoresTechnologyId) ? faction.ReciprocalRadiusBonusMillimetres : 0);
        private static bool Within(SimPoint a, SimPoint b, int radius)
        { long dx = (long)a.X - b.X, dz = (long)a.Z - b.Z; return dx * dx + dz * dz <= (long)radius * radius; }
        private bool ActiveYard(BuildingState yard) => yard.IsOperational && yard.CharterRemainingTicks == 0 &&
            yard.ActiveCharter != StoreyardCharter.None && yard.Definition.CanCharter;

        internal void RefreshBonuses()
        {
            if (world.factionCatalog.Assignments.Count == 0) return;
            influence.Clear();
            foreach (var yard in world.buildings)
            {
                var faction = DefinitionFor(yard.OwnerId);
                if (faction == null || faction.Kind != FactionKind.AvenCompact || !ActiveYard(yard)) continue;
                influence.Add(new Influence(yard.OwnerId, yard.Id, yard.Position, YardRadius(faction, world.Player(yard.OwnerId)), yard.ActiveCharter));
            }
            foreach (var unit in world.units)
            {
                if (unit.ThreadkeeperRemainingTicks == 0 || !world.TryGetBuilding(unit.LinkedStoreyardId, out var yard) || !ActiveYard(yard)) continue;
                var faction = DefinitionFor(unit.OwnerId);
                if (faction == null || yard.OwnerId != unit.OwnerId || !Within(unit.Position, yard.Position, YardRadius(faction, world.Player(unit.OwnerId)))) continue;
                influence.Add(new Influence(unit.OwnerId, yard.Id, unit.Position, faction.RelayRadiusMillimetres, yard.ActiveCharter));
            }
            foreach (var unit in world.units) ApplyStats(unit);
            foreach (var building in world.buildings)
            {
                building.CharterSourceId = FindInfluence(building.OwnerId, building.Position, StoreyardCharter.Muster);
                var faction = DefinitionFor(building.OwnerId);
                building.ProductionWorkPermille = 1000 + (building.CharterSourceId == 0 || faction == null ? 0 : faction.MusterWorkBonusPermille);
            }
        }

        internal void ApplyStats(UnitState unit)
        {
            world.RefreshUnitResearchStats(unit);
            var definition = unit.Definition;
            var faction = DefinitionFor(unit.OwnerId);
            unit.CarryCapacity = definition.CarryCapacity;
            unit.MoveSpeedMillimetresPerSecond = definition.MoveSpeedMillimetresPerSecond;
            unit.CharterSourceId = 0;
            if (faction == null) return;
            if (unit.IsWorker)
            {
                unit.CarryCapacity += faction.WorkerCarryBonus;
                unit.CharterSourceId = FindInfluence(unit.OwnerId, unit.Position, StoreyardCharter.Logistics);
                if (unit.CharterSourceId != 0) unit.CarryCapacity += faction.LogisticsCarryBonus;
            }
            int speedBonus = (unit.Tags & CombatTags.Cavalry) == 0 ? 0 : faction.CavalrySpeedBonusPermille;
            if (unit.RepositionRemainingTicks > 0) speedBonus += faction.RepositionSpeedBonusPermille;
            unit.MoveSpeedMillimetresPerSecond = (int)((long)definition.MoveSpeedMillimetresPerSecond * (1000 + speedBonus) / 1000);
            if (unit.IsPackedOutpost || definition.IsThreadkeeper) unit.AttackDamage = 0;
        }

        private int FindInfluence(int owner, SimPoint position, StoreyardCharter charter)
        {
            int found = 0;
            foreach (var source in influence)
                if (source.Owner == owner && source.Charter == charter && (found == 0 || source.YardId < found) && Within(position, source.Position, source.Radius)) found = source.YardId;
            return found;
        }

        internal void BuildingDestroyed(int id)
        {
            foreach (var unit in world.units)
                if (unit.LinkedStoreyardId == id) EndRelay(unit, DefinitionFor(unit.OwnerId));
        }
        private void EndRelay(UnitState unit, FactionDefinition faction)
        {
            unit.ThreadkeeperRemainingTicks = 0;
            unit.LinkedStoreyardId = 0;
            if (faction != null)
            { unit.ThreadkeeperCooldownTicks = faction.ThreadkeeperCooldownTicks; unit.ThreadkeeperCooldownStartedTick = world.TickIndex; }
        }
        private static CommandResult Reject(CommandRejection reason, string message) => CommandResult.Reject(reason, message);
    }
}
