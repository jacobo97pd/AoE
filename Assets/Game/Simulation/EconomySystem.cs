using System;

namespace Emberfield.Simulation
{
    /// <summary>Workers retain cargo across task changes; only an arrived drop-off transfers inventory.</summary>
    internal sealed class EconomySystem
    {
        private readonly World world;
        internal EconomySystem(World world) { this.world = world; }

        internal CommandResult Submit(ReturnCargoCommand command)
        {
            var result = world.SelectWorkers(command.PlayerId, command.WorkerIds, out var workers);
            if (!result.Accepted) return result;
            BuildingState explicitDropOff = null;
            if (command.DropOffBuildingId != 0)
            {
                if (!world.TryGetBuilding(command.DropOffBuildingId, out explicitDropOff))
                    return CommandResult.Reject(CommandRejection.UnknownBuilding, "Drop-off building does not exist.");
                if (explicitDropOff.OwnerId != command.PlayerId)
                    return CommandResult.Reject(CommandRejection.NotOwner, "You do not own this drop-off.");
                if (!explicitDropOff.IsOperational || !world.buildingDefinitions[explicitDropOff.DefinitionId].CanDropOff)
                    return CommandResult.Reject(CommandRejection.NoDropOff, "A completed resource drop-off is required.");
            }
            var routes = new RoutePlan[workers.Count];
            var dropOffIds = new int[workers.Count];
            bool anyCargo = false;
            for (int i = 0; i < workers.Count; i++)
            {
                var unit = workers[i];
                if (unit.CarriedAmount == 0) continue;
                anyCargo = true;
                if (explicitDropOff != null)
                {
                    if (!InteractionSearch.Approach(world, world.navigation, unit.Position, unit.RadiusMillimetres,
                        GridFootprint.Building(explicitDropOff, world.cellSize), out routes[i]))
                        return CommandResult.Reject(CommandRejection.NoPath, "Every carrying worker must be able to reach this drop-off.");
                    dropOffIds[i] = explicitDropOff.Id;
                }
                else
                {
                    if (!InteractionSearch.DropOff(world, unit, out var dropOff, out routes[i]))
                        return CommandResult.Reject(CommandRejection.NoDropOff, "Every carrying worker needs a reachable completed drop-off belonging to you.");
                    dropOffIds[i] = dropOff.Id;
                }
            }
            if (!anyCargo) return CommandResult.Reject(CommandRejection.InvalidCommand, "The selected workers have no cargo to deliver.");
            for (int i = 0; i < workers.Count; i++)
            {
                if (dropOffIds[i] == 0) continue;
                var unit = workers[i];
                CombatSystem.CancelAttack(unit, true);
                MovementSystem.CancelWorkerTask(unit);
                unit.TargetBuildingId = dropOffIds[i];
                unit.WorkerTask = WorkerTask.ReturningResources;
                MovementSystem.SetRoute(unit, routes[i]);
            }
            return CommandResult.Success();
        }

        internal CommandResult Submit(GatherCommand command)
        {
            var result = world.SelectWorkers(command.PlayerId, command.WorkerIds, out var workers);
            if (!result.Accepted) return result;
            if (!world.TryGetResource(command.ResourceId, out var resource))
                return CommandResult.Reject(CommandRejection.UnknownResource, "Resource node does not exist.");
            if (resource.RemainingAmount == 0)
                return CommandResult.Reject(CommandRejection.ResourceDepleted, "Resource node is depleted.");
            var routes = new RoutePlan[workers.Count];
            var dropOffIds = new int[workers.Count];
            for (int i = 0; i < workers.Count; i++)
            {
                var unit = workers[i];
                if (!InteractionSearch.Approach(world, world.navigation, unit.Position, unit.RadiusMillimetres,
                    GridFootprint.Resource(resource, world.cellSize), out var resourceRoute))
                    return CommandResult.Reject(CommandRejection.NoPath, "Every worker must be able to reach the resource.");
                if (!InteractionSearch.DropOff(world, unit, out var dropOff, out var returnRoute))
                    return CommandResult.Reject(CommandRejection.NoDropOff, "A reachable completed drop-off belonging to you is required.");
                bool returnFirst = unit.CarriedAmount > 0 && (unit.CarriedKind != resource.Kind ||
                    unit.CarriedAmount >= unit.CarryCapacity);
                routes[i] = returnFirst ? returnRoute : resourceRoute;
                RouteLists.Return(returnFirst ? resourceRoute.Path : returnRoute.Path);
                dropOffIds[i] = returnFirst ? dropOff.Id : 0;
            }
            for (int i = 0; i < workers.Count; i++)
            {
                var unit = workers[i];
                if (unit.TargetResourceId == resource.Id && (unit.WorkerTask == WorkerTask.MovingToResource ||
                    unit.WorkerTask == WorkerTask.Gathering || unit.WorkerTask == WorkerTask.ReturningResources)) continue;
                CombatSystem.CancelAttack(unit, true);
                MovementSystem.CancelWorkerTask(unit);
                unit.TargetResourceId = resource.Id;
                unit.TargetBuildingId = dropOffIds[i];
                unit.WorkerTask = dropOffIds[i] == 0 ? WorkerTask.MovingToResource : WorkerTask.ReturningResources;
                MovementSystem.SetRoute(unit, routes[i]);
            }
            return CommandResult.Success();
        }

        internal void Tick()
        {
            foreach (var unit in world.units)
            {
                if (unit.WorkerTask == WorkerTask.None || unit.WorkerTask == WorkerTask.MovingToConstruction ||
                    unit.WorkerTask == WorkerTask.Constructing || world.TickIndex < unit.RetryAtTick) continue;
                if (unit.Order == UnitOrder.Moving)
                {
                    bool adjacent = unit.WorkerTask == WorkerTask.ReturningResources
                        ? world.TryGetBuilding(unit.TargetBuildingId, out var destinationBuilding) && GridFootprint.Building(destinationBuilding, world.cellSize).CanInteract(unit.Position, world.cellSize)
                        : world.TryGetResource(unit.TargetResourceId, out var destinationResource) && GridFootprint.Resource(destinationResource, world.cellSize).CanInteract(unit.Position, world.cellSize);
                    // A recruit or another idle worker can occupy an interaction cell after this
                    // route was chosen. Local movement recovery keeps the original destination,
                    // so reselect a free resource/drop-off approach after one blocked second.
                    // Halt/SetRoute reset the counter; failed searches use the existing retry delay.
                    if (!adjacent && unit.MovementBlockedTicks < World.TickRate) continue;
                    MovementSystem.Halt(unit);
                }
                if (unit.WorkerTask == WorkerTask.ReturningResources) { ReturnArrived(unit); continue; }
                if (!world.TryGetResource(unit.TargetResourceId, out var resource)) { FinishOrReturn(unit); continue; }
                if (resource.RemainingAmount == 0) { FinishOrReturn(unit); continue; }
                if (!GridFootprint.Resource(resource, world.cellSize).CanInteract(unit.Position, world.cellSize))
                { SendToResource(unit, resource); continue; }
                if (unit.WorkerTask == WorkerTask.MovingToResource)
                { unit.WorkerTask = WorkerTask.Gathering; unit.GatherElapsed = 0; continue; }
                Harvest(unit, resource);
            }
        }

        private void Harvest(UnitState unit, ResourceNodeState resource)
        {
            var definition = unit.Definition;
            if (unit.CarriedAmount > 0 && unit.CarriedKind != resource.Kind) { SendToDropOff(unit); return; }
            if (unit.CarriedAmount >= unit.CarryCapacity) { SendToDropOff(unit); return; }
            unit.GatherElapsed++;
            if (unit.GatherElapsed < definition.GatherIntervalTicks) return;
            unit.GatherElapsed = 0;
            var faction = world.factions.DefinitionFor(unit.OwnerId);
            int bonus = faction != null && faction.BonusGatherResource == resource.Kind ? faction.ResourceGatherBonus : 0;
            int amount = Math.Min(unit.GatherAmount + bonus, Math.Min(unit.CarryCapacity - unit.CarriedAmount, resource.RemainingAmount));
            resource.RemainingAmount -= amount;
            if (resource.RemainingAmount == 0) world.navigation.ClearResourceCell(resource.Position);
            unit.CarriedKind = resource.Kind;
            unit.CarriedAmount += amount;
            if (unit.CarriedAmount == unit.CarryCapacity || resource.RemainingAmount == 0) SendToDropOff(unit);
        }

        private void FinishOrReturn(UnitState unit)
        {
            if (unit.CarriedAmount > 0) SendToDropOff(unit);
            else MovementSystem.CancelWorkerTask(unit);
        }

        private void ReturnArrived(UnitState unit)
        {
            if (unit.CarriedAmount == 0) { ContinueGathering(unit); return; }
            if (!world.TryGetBuilding(unit.TargetBuildingId, out var dropOff) || dropOff.OwnerId != unit.OwnerId || !dropOff.IsOperational ||
                !world.buildingDefinitions[dropOff.DefinitionId].CanDropOff ||
                !GridFootprint.Building(dropOff, world.cellSize).CanInteract(unit.Position, world.cellSize))
            { SendToDropOff(unit); return; }
            int credited = world.Player(unit.OwnerId).Deposit(unit.CarriedKind, unit.CarriedAmount);
            unit.CarriedAmount -= credited;
            if (unit.CarriedAmount == 0) ContinueGathering(unit);
            else unit.RetryAtTick = world.TickIndex + World.TickRate;
        }

        private void ContinueGathering(UnitState unit)
        {
            unit.TargetBuildingId = 0;
            if (world.TryGetResource(unit.TargetResourceId, out var resource) && resource.RemainingAmount > 0) SendToResource(unit, resource);
            else MovementSystem.CancelWorkerTask(unit);
        }

        private void SendToResource(UnitState unit, ResourceNodeState resource)
        {
            unit.WorkerTask = WorkerTask.MovingToResource;
            unit.GatherElapsed = 0;
            if (InteractionSearch.Approach(world, world.navigation, unit.Position, unit.RadiusMillimetres,
                GridFootprint.Resource(resource, world.cellSize), out var route)) MovementSystem.SetRoute(unit, route);
            else { MovementSystem.Halt(unit); unit.RetryAtTick = world.TickIndex + World.TickRate; }
        }

        private void SendToDropOff(UnitState unit)
        {
            unit.WorkerTask = WorkerTask.ReturningResources;
            unit.GatherElapsed = 0;
            if (InteractionSearch.DropOff(world, unit, out var dropOff, out var route))
            {
                unit.TargetBuildingId = dropOff.Id;
                MovementSystem.SetRoute(unit, route);
            }
            else { unit.TargetBuildingId = 0; MovementSystem.Halt(unit); unit.RetryAtTick = world.TickIndex + World.TickRate; }
        }
    }
}
