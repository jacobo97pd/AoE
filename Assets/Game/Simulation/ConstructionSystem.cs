using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    internal sealed class ConstructionSystem
    {
        private readonly World world;
        internal ConstructionSystem(World world) { this.world = world; }

        internal CommandResult Validate(BuildCommand command) => Prepare(command, out _, out _, out _, out _);

        private CommandResult Prepare(BuildCommand command, out List<UnitState> workers, out BuildingDefinition definition,
            out GridFootprint footprint, out RoutePlan[] routes)
        {
            definition = null; footprint = default; routes = null;
            var result = world.SelectWorkers(command.PlayerId, command.WorkerIds, out workers);
            if (!result.Accepted) return result;
            if (command.BuildingDefinitionId == null || !world.buildingDefinitions.TryGetValue(command.BuildingDefinitionId, out definition))
                return CommandResult.Reject(CommandRejection.UnknownDefinition, "Building definition does not exist.");
            if (command.Turned && !BuildingFootprints.CanTurn(definition))
                return CommandResult.Reject(CommandRejection.InvalidPlacement, "Only walls and gates can be turned.");
            var faction = world.ValidateFactionRequirement(command.PlayerId, definition.RequiredFactionId);
            if (!faction.Accepted) return faction;
            var requirements = world.ValidateRequirements(command.PlayerId, definition.RequiredEraId, definition.RequiredTechnologyIds);
            if (!requirements.Accepted) return requirements;
            if (!world.HasEntityId) return CommandResult.Reject(CommandRejection.EntityLimit, "Entity ID space is exhausted.");
            var player = world.Player(command.PlayerId);
            if (!player.CanAfford(definition.Cost)) return CommandResult.Reject(CommandRejection.InsufficientResources, "Not enough resources to build.");
            long futureCapacity = (long)player.PopulationCapacity + definition.PopulationCapacity;
            foreach (var building in world.buildings)
                if (building.OwnerId == player.Id && !building.IsComplete) futureCapacity += world.buildingDefinitions[building.DefinitionId].PopulationCapacity;
            if (futureCapacity > int.MaxValue) return CommandResult.Reject(CommandRejection.PopulationLimit, "Population capacity would exceed supported bounds.");
            var placed = Footprint(definition, command.Position, command.Turned, out footprint);
            if (!placed.Accepted) return placed;
            if (definition.RequiresShore && !OnShore(footprint))
                return CommandResult.Reject(CommandRejection.NoShore, "A dock must stand on the shore, beside open water.");
            var previewNavigation = world.navigation.WithFootprint(footprint);
            // Ships ride on deep water, which is never land a building can take.
            foreach (var unit in world.units)
                if (unit.Domain == MovementDomain.Land && !previewNavigation.CanOccupy(unit.Position, unit.RadiusMillimetres))
                    return CommandResult.Reject(CommandRejection.UnitObstruction, "A unit occupies the building footprint or its clearance.");
            routes = new RoutePlan[workers.Count];
            for (int i = 0; i < workers.Count; i++)
                if (!InteractionSearch.Approach(world, previewNavigation, workers[i].Position, workers[i].RadiusMillimetres, footprint, out routes[i]))
                    return CommandResult.Reject(CommandRejection.NoPath, "Every builder must be able to reach the new foundation.");
            return CommandResult.Success();
        }

        internal CommandResult Submit(BuildCommand command)
        {
            var result = Prepare(command, out var workers, out var definition, out var footprint, out var routes);
            if (!result.Accepted) return result;
            // Everything that can reject has completed. Mutations below form a single command transaction.
            world.Player(command.PlayerId).TrySpend(definition.Cost);
            int id = world.AllocateEntityId();
            var building = new BuildingState(new BuildingSpawnDefinition
            { Id = id, OwnerId = command.PlayerId, DefinitionId = definition.Id, Position = command.Position, Turned = command.Turned }, definition)
            { BuildProgressTicks = 0, Health = 1 };
            world.AddBuilding(building);
            world.navigation.AddFootprint(footprint);
            world.movement.ReplanAll();
            Assign(workers, building, routes);
            return CommandResult.Success(id);
        }

        // Where a building of this definition centred on the position would stand: aligned to the grid, inside the
        // map and on cells nothing blocks. A turned footprint swaps its sides.
        private CommandResult Footprint(BuildingDefinition definition, SimPoint position, bool turned, out GridFootprint footprint)
        {
            footprint = default;
            int width = BuildingFootprints.WidthCells(definition, turned), depth = BuildingFootprints.DepthCells(definition, turned);
            long left = (long)position.X - (long)width * world.cellSize / 2;
            long bottom = (long)position.Z - (long)depth * world.cellSize / 2;
            if (left < 0 || bottom < 0 || left % world.cellSize != 0 || bottom % world.cellSize != 0 ||
                left / world.cellSize + width > world.mapWidth || bottom / world.cellSize + depth > world.mapHeight)
                return CommandResult.Reject(CommandRejection.InvalidPlacement, "Building footprint must align to the grid and fit inside the map.");
            footprint = new GridFootprint((int)(left / world.cellSize), (int)(bottom / world.cellSize), width, depth);
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                if (!world.navigation.IsWalkable(new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2)))
                    return CommandResult.Reject(CommandRejection.DestinationBlocked, "Building footprint overlaps terrain, a resource, or another building.");
            return CommandResult.Success();
        }

        /// <summary>
        /// Whether a cell beside the footprint's sides is deep water: where the dock launches its ships, the same cells
        /// the spawn search tries (InteractionSearch.SpawnPoint).
        /// </summary>
        internal bool OnShore(GridFootprint footprint)
        {
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                if (Sailable(x, footprint.Z - 1) || Sailable(x, footprint.Z + footprint.Depth)) return true;
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
                if (Sailable(footprint.X - 1, z) || Sailable(footprint.X + footprint.Width, z)) return true;
            return false;
        }

        private bool Sailable(int x, int z) => x >= 0 && z >= 0 && x < world.mapWidth && z < world.mapHeight &&
            world.waterNavigation.IsWalkable(new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2));

        // Wall runs: the run each worker is still laying, as the ids of its foundations in order. Only ever looked up by
        // worker, never enumerated, so no dictionary order reaches the rules; empty until a run is ordered.
        private readonly Dictionary<int, int[]> runByWorker = new Dictionary<int, int[]>();
        // Scratch for planning a run: the accepted sites' indices and footprints, in order.
        private readonly List<int> runSites = new List<int>();
        private readonly List<GridFootprint> runFootprints = new List<GridFootprint>();
        private readonly List<CommandResult> runVerdicts = new List<CommandResult>();

        internal void Preview(BuildRunCommand command, BuildRunPreview preview)
        {
            preview.Result = PlanRun(command, preview.verdicts, out _, out _, out var cost);
            preview.Cost = cost; preview.AcceptedCount = 0;
            foreach (var verdict in preview.verdicts) if (verdict.Accepted) preview.AcceptedCount++;
        }

        /// <summary>
        /// Judges every stretch of a run in order, as if the accepted ones were all laid at once. A stretch is accepted when
        /// it is visible, fits, overlaps no obstacle, unit or earlier accepted stretch, the stock still covers it after the
        /// earlier ones, and some selected worker can reach a cell beside it with every accepted stretch standing.
        /// </summary>
        private CommandResult PlanRun(BuildRunCommand command, List<CommandResult> verdicts, out List<UnitState> workers,
            out BuildingDefinition definition, out ResourceAmount cost)
        {
            verdicts.Clear(); runSites.Clear(); runFootprints.Clear(); cost = default;
            var result = PlanRunOrder(command, out workers, out definition);
            if (!result.Accepted)
            {
                for (int i = 0; i < command.Sites.Count; i++) verdicts.Add(result);
                return result;
            }
            var player = world.Player(command.PlayerId);
            var stock = player.Resources;
            long futureCapacity = player.PopulationCapacity;
            foreach (var building in world.buildings)
                if (building.OwnerId == player.Id && !building.IsComplete) futureCapacity += world.buildingDefinitions[building.DefinitionId].PopulationCapacity;
            for (int i = 0; i < command.Sites.Count; i++)
            {
                var verdict = PlanSite(command, i, definition, ref stock, ref futureCapacity, out var footprint);
                verdicts.Add(verdict);
                if (verdict.Accepted) { runSites.Add(i); runFootprints.Add(footprint); }
            }
            if (runSites.Count == 0) return verdicts[0];
            // Reach is judged with every accepted stretch standing, on the placement preview grid. Clearing cells only
            // ever joins areas, so dropping an unreachable stretch strands none already judged reachable.
            var scratch = world.navigation.WithFootprint(runFootprints[0]);
            for (int k = 1; k < runFootprints.Count; k++) Block(scratch, runFootprints[k]);
            scratch.BuildConnectivity();
            for (int k = 0; k < runFootprints.Count; k++)
            {
                if (ReachedByAny(scratch, workers, runFootprints[k])) continue;
                verdicts[runSites[k]] = CommandResult.Reject(CommandRejection.NoPath, "No builder can reach this stretch of the wall.");
                scratch.ClearFootprint(runFootprints[k]);
                runSites.RemoveAt(k); runFootprints.RemoveAt(k); k--;
            }
            if (runSites.Count == 0) return CommandResult.Reject(CommandRejection.NoPath, "No builder can reach this stretch of the wall.");
            var each = definition.Cost; int count = runSites.Count;
            cost = new ResourceAmount(each.Food * count, each.Wood * count, each.Metal * count, each.Stone * count);
            foreach (var worker in workers)
            {
                bool reached = false;
                foreach (var footprint in runFootprints) if (Reachable(scratch, worker, footprint)) { reached = true; break; }
                if (!reached) return CommandResult.Reject(CommandRejection.NoPath, "Every builder must be able to reach the wall.");
            }
            return CommandResult.Success();
        }

        private CommandResult PlanRunOrder(BuildRunCommand command, out List<UnitState> workers, out BuildingDefinition definition)
        {
            definition = null;
            var result = world.SelectWorkers(command.PlayerId, command.WorkerIds, out workers);
            if (!result.Accepted) return result;
            if (command.Sites.Count == 0 || command.Sites.Count > BuildRunCommand.MaximumSites)
                return CommandResult.Reject(CommandRejection.InvalidCommand, "A wall run holds between one and 64 stretches.");
            if (command.BuildingDefinitionId == null || !world.buildingDefinitions.TryGetValue(command.BuildingDefinitionId, out definition))
                return CommandResult.Reject(CommandRejection.UnknownDefinition, "Building definition does not exist.");
            if (!definition.IsWall) return CommandResult.Reject(CommandRejection.InvalidCommand, "Only walls are laid in runs.");
            var faction = world.ValidateFactionRequirement(command.PlayerId, definition.RequiredFactionId);
            if (!faction.Accepted) return faction;
            return world.ValidateRequirements(command.PlayerId, definition.RequiredEraId, definition.RequiredTechnologyIds);
        }

        private CommandResult PlanSite(BuildRunCommand command, int index, BuildingDefinition definition, ref ResourceAmount stock,
            ref long futureCapacity, out GridFootprint footprint)
        {
            var site = command.Sites[index];
            footprint = default;
            if (site.Turned && !BuildingFootprints.CanTurn(definition))
                return CommandResult.Reject(CommandRejection.InvalidPlacement, "Only walls and gates can be turned.");
            if (world.Vision != null)
            {
                var visible = world.Vision.ValidatePlacement(command.PlayerId, definition.Id, site.Position, site.Turned);
                if (!visible.Accepted) return visible;
            }
            var placed = Footprint(definition, site.Position, site.Turned, out footprint);
            if (!placed.Accepted) return placed;
            // Accepted stretches are not on any grid yet, and blocking a cell twice throws: every earlier one is compared.
            foreach (var other in runFootprints)
                if (footprint.X < other.X + other.Width && other.X < footprint.X + footprint.Width &&
                    footprint.Z < other.Z + other.Depth && other.Z < footprint.Z + footprint.Depth)
                    return CommandResult.Reject(CommandRejection.DestinationBlocked, "This stretch overlaps another stretch of the wall.");
            long left = (long)footprint.X * world.cellSize, right = (long)(footprint.X + footprint.Width) * world.cellSize;
            long bottom = (long)footprint.Z * world.cellSize, top = (long)(footprint.Z + footprint.Depth) * world.cellSize;
            foreach (var unit in world.units)
            {
                // Soldiers on a wall deck stand on that wall, not on the ground being built.
                if (unit.WallId != 0) continue;
                long dx = unit.Position.X - Math.Max(left, Math.Min(unit.Position.X, right));
                long dz = unit.Position.Z - Math.Max(bottom, Math.Min(unit.Position.Z, top));
                if (dx * dx + dz * dz < (long)unit.RadiusMillimetres * unit.RadiusMillimetres)
                    return CommandResult.Reject(CommandRejection.UnitObstruction, "A unit occupies the building footprint or its clearance.");
            }
            if (!stock.Covers(definition.Cost)) return CommandResult.Reject(CommandRejection.InsufficientResources, "Not enough resources for this stretch of the wall.");
            if (!world.HasEntityIds(runSites.Count + 1)) return CommandResult.Reject(CommandRejection.EntityLimit, "Entity ID space is exhausted.");
            if (futureCapacity + definition.PopulationCapacity > int.MaxValue)
                return CommandResult.Reject(CommandRejection.PopulationLimit, "Population capacity would exceed supported bounds.");
            stock = new ResourceAmount(stock.Food - definition.Cost.Food, stock.Wood - definition.Cost.Wood, stock.Metal - definition.Cost.Metal, stock.Stone - definition.Cost.Stone);
            futureCapacity += definition.PopulationCapacity;
            return CommandResult.Success();
        }

        internal CommandResult Submit(BuildRunCommand command)
        {
            var result = PlanRun(command, runVerdicts, out var workers, out var definition, out _);
            if (!result.Accepted) return result;
            // Everything that can reject has completed. Mutations below form a single command transaction.
            var player = world.Player(command.PlayerId);
            var ids = new int[runSites.Count];
            for (int k = 0; k < ids.Length; k++)
            {
                var site = command.Sites[runSites[k]];
                player.TrySpend(definition.Cost);
                ids[k] = world.AllocateEntityId();
                world.AddBuilding(new BuildingState(new BuildingSpawnDefinition
                { Id = ids[k], OwnerId = command.PlayerId, DefinitionId = definition.Id, Position = site.Position, Turned = site.Turned }, definition)
                { BuildProgressTicks = 0, Health = 1 });
                Block(world.navigation, runFootprints[k]);
            }
            world.navigation.BuildConnectivity();
            world.movement.ReplanAll();
            foreach (var unit in workers)
            {
                CombatSystem.CancelAttack(unit, true);
                MovementSystem.CancelWorkerTask(unit);
                runByWorker[unit.Id] = ids;
                ContinueRun(unit);
            }
            return CommandResult.Success(ids[0]);
        }

        /// <summary>
        /// Sends a worker laying a run on to the first unfinished foundation of it that it can reach. With none left it
        /// leaves the run and idles, rather than wait forever beside a foundation it cannot get to.
        /// </summary>
        internal void ContinueRun(UnitState unit)
        {
            if (runByWorker.Count == 0 || !runByWorker.TryGetValue(unit.Id, out var run)) return;
            foreach (int id in run)
            {
                if (!world.TryGetBuilding(id, out var next) || next.OwnerId != unit.OwnerId || next.IsComplete) continue;
                var footprint = GridFootprint.Building(next, world.cellSize);
                if (!Reachable(world.navigation, unit, footprint)) continue;
                unit.TargetBuildingId = next.Id; unit.WorkerTask = WorkerTask.MovingToConstruction;
                if (footprint.CanInteract(unit.Position, world.cellSize)) MovementSystem.Halt(unit);
                else if (InteractionSearch.Approach(world, world.navigation, unit.Position, unit.RadiusMillimetres, footprint, out var route)) MovementSystem.SetRoute(unit, route);
                // Other units stand on every free cell beside it for now: the usual retry looks again in a second.
                else { MovementSystem.Halt(unit); unit.RetryAtTick = world.TickIndex + World.TickRate; }
                return;
            }
            runByWorker.Remove(unit.Id);
        }

        private bool ReachedByAny(Navigation navigation, List<UnitState> workers, GridFootprint footprint)
        {
            foreach (var worker in workers) if (Reachable(navigation, worker, footprint)) return true;
            return false;
        }

        // Whether a free cell beside the footprint, where the unit fits, lies in the unit's connected area. Units standing
        // there are ignored: they move, and the approach steps around them.
        private bool Reachable(Navigation navigation, UnitState unit, GridFootprint footprint)
        {
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++)
                if (Beside(navigation, unit, x, footprint.Z - 1) || Beside(navigation, unit, x, footprint.Z + footprint.Depth)) return true;
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
                if (Beside(navigation, unit, footprint.X - 1, z) || Beside(navigation, unit, footprint.X + footprint.Width, z)) return true;
            return false;
        }

        private bool Beside(Navigation navigation, UnitState unit, int x, int z)
        {
            if (x < 0 || z < 0 || x >= world.mapWidth || z >= world.mapHeight) return false;
            var cell = new SimPoint(x * world.cellSize + world.cellSize / 2, z * world.cellSize + world.cellSize / 2);
            return navigation.CanOccupy(cell, unit.RadiusMillimetres) && navigation.CanReach(unit.Position, cell);
        }

        // Without rebuilding connectivity: the caller does that once after the last footprint.
        private static void Block(Navigation navigation, GridFootprint footprint)
        {
            for (int z = footprint.Z; z < footprint.Z + footprint.Depth; z++)
            for (int x = footprint.X; x < footprint.X + footprint.Width; x++) navigation.Block(x, z);
        }

        internal CommandResult Submit(ConstructCommand command)
        {
            var result = world.SelectWorkers(command.PlayerId, command.WorkerIds, out var workers);
            if (!result.Accepted) return result;
            if (!world.TryGetBuilding(command.BuildingId, out var building))
                return CommandResult.Reject(CommandRejection.UnknownBuilding, "Building does not exist.");
            if (building.OwnerId != command.PlayerId) return CommandResult.Reject(CommandRejection.NotOwner, "You do not own this building.");
            if (building.IsComplete) return CommandResult.Reject(CommandRejection.BuildingComplete, "Building is already complete.");
            var routes = new RoutePlan[workers.Count];
            for (int i = 0; i < workers.Count; i++)
                if (!InteractionSearch.Approach(world, world.navigation, workers[i].Position, workers[i].RadiusMillimetres,
                    GridFootprint.Building(building, world.cellSize), out routes[i]))
                    return CommandResult.Reject(CommandRejection.NoPath, "Every builder must be able to reach the foundation.");
            Assign(workers, building, routes);
            return CommandResult.Success(building.Id);
        }

        private void Assign(List<UnitState> workers, BuildingState building, RoutePlan[] routes)
        {
            for (int i = 0; i < workers.Count; i++)
            {
                var unit = workers[i];
                // A worker sent to one foundation leaves the wall run it was laying.
                if (runByWorker.Count != 0) runByWorker.Remove(unit.Id);
                if (unit.TargetBuildingId == building.Id && (unit.WorkerTask == WorkerTask.MovingToConstruction || unit.WorkerTask == WorkerTask.Constructing)) continue;
                CombatSystem.CancelAttack(unit, true);
                MovementSystem.CancelWorkerTask(unit);
                unit.TargetBuildingId = building.Id;
                unit.WorkerTask = WorkerTask.MovingToConstruction;
                MovementSystem.SetRoute(unit, routes[i]);
            }
        }

        internal void Tick()
        {
            foreach (var unit in world.units)
            {
                if ((unit.WorkerTask != WorkerTask.MovingToConstruction && unit.WorkerTask != WorkerTask.Constructing) ||
                    world.TickIndex < unit.RetryAtTick) continue;
                if (!world.TryGetBuilding(unit.TargetBuildingId, out var building) || building.IsComplete)
                { MovementSystem.CancelWorkerTask(unit); ContinueRun(unit); continue; }
                var footprint = GridFootprint.Building(building, world.cellSize);
                if (!footprint.CanInteract(unit.Position, world.cellSize))
                {
                    if (unit.Order == UnitOrder.Moving) continue;
                    if (InteractionSearch.Approach(world, world.navigation, unit.Position, unit.RadiusMillimetres, footprint, out var route))
                        MovementSystem.SetRoute(unit, route);
                    // A run's foundation cut off from its worker: go on to one it can reach rather than wait forever.
                    else if (runByWorker.Count != 0 && runByWorker.ContainsKey(unit.Id) && !Reachable(world.navigation, unit, footprint))
                    { MovementSystem.CancelWorkerTask(unit); ContinueRun(unit); }
                    else unit.RetryAtTick = world.TickIndex + World.TickRate;
                    continue;
                }
                if (unit.Order == UnitOrder.Moving) MovementSystem.Halt(unit);
                unit.WorkerTask = WorkerTask.Constructing;
                var definition = building.Definition;
                int previousHealthGrant = Math.Max(1, (int)((long)definition.MaxHealth * building.BuildProgressTicks / building.BuildRequiredTicks));
                building.BuildProgressTicks++;
                int nextHealthGrant = Math.Max(1, (int)((long)definition.MaxHealth * building.BuildProgressTicks / building.BuildRequiredTicks));
                building.Health = Math.Min(building.MaxHealth, building.Health + (nextHealthGrant - previousHealthGrant));
                if (building.IsComplete)
                {
                    world.Player(building.OwnerId).PopulationCapacity += definition.PopulationCapacity;
                    MovementSystem.CancelWorkerTask(unit);
                    ContinueRun(unit);
                }
            }
        }
    }
}
