using System;

namespace Emberfield.Simulation
{
    internal sealed class ProductionSystem
    {
        private readonly World world;
        internal ProductionSystem(World world) { this.world = world; }

        internal CommandResult Submit(TrainCommand command)
        {
            var result = ValidateBuilding(command.PlayerId, command.BuildingId, out var building, out var definition);
            if (!result.Accepted) return result;
            if (command.UnitDefinitionId == null || !world.unitDefinitions.TryGetValue(command.UnitDefinitionId, out var unit))
                return CommandResult.Reject(CommandRejection.UnknownDefinition, "Unit definition does not exist.");
            if (Array.IndexOf(definition.TrainableUnitIds ?? Array.Empty<string>(), unit.Id) < 0)
                return CommandResult.Reject(CommandRejection.CannotTrain, "This building cannot train that unit.");
            if (building.ActiveResearch != null)
                return CommandResult.Reject(CommandRejection.ResearchBusy, "Training must wait until this building's research finishes.");
            if (building.CharterRemainingTicks != 0)
                return CommandResult.Reject(CommandRejection.FactionActionBusy, "Training must wait until the charter change finishes.");
            var faction = world.ValidateFactionRequirement(command.PlayerId, unit.RequiredFactionId);
            if (!faction.Accepted) return faction;
            var recruitment = world.ValidateUnitRecruitment(command.PlayerId, unit.Id);
            if (!recruitment.Accepted) return recruitment;
            var requirements = world.ValidateRequirements(command.PlayerId, unit.RequiredEraId, unit.RequiredTechnologyIds);
            if (!requirements.Accepted) return requirements;
            if (building.Queue.Count >= world.Definition.MaxProductionQueue)
                return CommandResult.Reject(CommandRejection.QueueFull, "Production queue is full.");
            if (!world.HasEntityId) return CommandResult.Reject(CommandRejection.EntityLimit, "Entity ID space is exhausted.");
            var player = world.Player(command.PlayerId);
            if ((long)player.PopulationUsed + player.PopulationReserved + unit.PopulationCost > player.PopulationCapacity)
                return CommandResult.Reject(CommandRejection.PopulationLimit, "More completed housing is required.");
            if (!player.CanAfford(unit.Cost)) return CommandResult.Reject(CommandRejection.InsufficientResources, "Not enough resources to train.");
            player.TrySpend(unit.Cost);
            player.PopulationReserved += unit.PopulationCost;
            building.Queue.Add(new TrainingQueueEntry(unit));
            return CommandResult.Success();
        }

        internal CommandResult Submit(SetRallyCommand command)
        {
            var result = ValidateBuilding(command.PlayerId, command.BuildingId, out var building, out var definition);
            if (!result.Accepted) return result;
            if (definition.TrainableUnitIds == null || definition.TrainableUnitIds.Length == 0)
                return CommandResult.Reject(CommandRejection.CannotTrain, "This building does not produce units.");
            int radius = 0;
            foreach (var id in definition.TrainableUnitIds) radius = Math.Max(radius, world.unitDefinitions[id].RadiusMillimetres);
            // A dock rallies its ships on the water; everything else rallies on land.
            var navigation = world.NavigationFor(world.unitDefinitions[definition.TrainableUnitIds[0]]);
            if (!navigation.Contains(command.Destination)) return CommandResult.Reject(CommandRejection.InvalidTarget, "Rally point is outside the map.");
            if (!navigation.CanOccupy(command.Destination, radius)) return CommandResult.Reject(CommandRejection.DestinationBlocked, "Rally point is blocked.");
            if (!InteractionSearch.Approach(world, navigation, command.Destination, radius,
                GridFootprint.Building(building, world.cellSize), out _))
                return CommandResult.Reject(CommandRejection.NoPath, "Rally point is unreachable from this building.");
            building.HasRallyPoint = true; building.RallyPoint = command.Destination;
            return CommandResult.Success();
        }

        private CommandResult ValidateBuilding(int playerId, int id, out BuildingState building, out BuildingDefinition definition)
        {
            definition = null;
            if (!world.TryGetBuilding(id, out building)) return CommandResult.Reject(CommandRejection.UnknownBuilding, "Building does not exist.");
            if (building.OwnerId != playerId) return CommandResult.Reject(CommandRejection.NotOwner, "You do not own this building.");
            if (!building.IsComplete) return CommandResult.Reject(CommandRejection.BuildingIncomplete, "Construction must finish first.");
            if (!building.IsOperational) return CommandResult.Reject(CommandRejection.FactionActionBusy, "This building is relocating.");
            definition = world.buildingDefinitions[building.DefinitionId];
            return CommandResult.Success();
        }

        internal void Tick()
        {
            foreach (var building in world.buildings)
            {
                if (!building.IsOperational || building.Queue.Count == 0 || building.CharterRemainingTicks != 0) continue;
                var entry = building.Queue[0];
                if (entry.RemainingTicks > 0)
                {
                    int work = building.ProductionWorkPermille + entry.WorkRemainder;
                    entry.RemainingTicks = Math.Max(0, entry.RemainingTicks - work / 1000);
                    entry.WorkRemainder = work % 1000;
                }
                if (entry.RemainingTicks > 0 || !world.HasEntityId) continue;
                var definition = world.unitDefinitions[entry.UnitDefinitionId];
                if (!InteractionSearch.SpawnPoint(world, world.NavigationFor(definition), building, definition.RadiusMillimetres, out var position)) continue;
                var player = world.Player(building.OwnerId);
                if ((long)player.PopulationUsed + entry.PopulationCost > player.PopulationCapacity) continue;
                var unit = new UnitState(new UnitSpawnDefinition
                { Id = world.AllocateEntityId(), OwnerId = building.OwnerId, DefinitionId = definition.Id, Position = position }, definition);
                world.AddUnit(unit);
                player.PopulationReserved -= entry.PopulationCost;
                player.PopulationUsed += entry.PopulationCost;
                building.Queue.RemoveAt(0);
                if (building.HasRallyPoint)
                    world.movement.Submit(new System.Collections.Generic.List<UnitState>(1) { unit }, building.RallyPoint);
            }
        }
    }
}
