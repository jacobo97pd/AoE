using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public sealed class OfflineAiStatistics
    {
        public int Thinks { get; internal set; }
        public int Commands { get; internal set; }
        public int Accepted { get; internal set; }
        public int Rejected { get; internal set; }
        public int GatherOrders { get; internal set; }
        public int BuildingsStarted { get; internal set; }
        public int UnitsQueued { get; internal set; }
        public int ResearchStarted { get; internal set; }
        public int AttackOrders { get; internal set; }
        public int ScoutOrders { get; internal set; }
        public int RetreatOrders { get; internal set; }
        public ResourceAmount Spent { get; internal set; }
        /// <summary>Why the rejected orders were rejected. A count here is wasted thinking, and naming the
        /// reason is the difference between fixing the AI and guessing at it.</summary>
        public Dictionary<CommandRejection, int> Rejections { get; } = new Dictionary<CommandRejection, int>();
    }

    public enum OfflineAiDifficulty { Easy = 0, Normal = 1, Hard = 2 }

    // One set of limits per difficulty. Every faction uses the same decision rules; only its
    // units, costs and bonuses differ.
    internal sealed class OfflineAiTuning
    {
        internal int ThinkInterval, CommandsPerThink, WorkerTarget, ArmyCap, PopulationGoal, MusterHalls, HallArmy;
        // Naval realm: the ships kept afloat, and the fleet that sails to raid the enemy's shore.
        internal int FleetTarget = 3;
        // Research starts at ResearchArmy soldiers; each started technology then needs ResearchArmyStep more.
        internal int ResearchArmy, ResearchArmyStep;
        internal int AssaultEra, AssaultTechnologies, AssaultPopulation, AssaultWorkers, RegroupBelow, DefenceRadius;
        internal long EarliestAssaultTick;
        // Only the producer that hosts planned research pauses; the others keep training.
        internal bool KeepProducersTraining;
        // Targets are scored from the army's centre, and soldiers already fighting a nearby visible
        // enemy keep their own target instead of being pulled across the battle.
        internal bool FocusedEngagement;

        internal static OfflineAiTuning For(OfflineAiDifficulty difficulty)
        {
            switch (difficulty)
            {
                // The original pacing: develops to the last Era and eight technologies before any assault.
                case OfflineAiDifficulty.Easy: return new OfflineAiTuning
                {
                    ThinkInterval = 10, CommandsPerThink = 4, WorkerTarget = 12, ArmyCap = 30, PopulationGoal = 60, MusterHalls = 2, HallArmy = 8,
                    ResearchArmy = 4, AssaultEra = 4, AssaultTechnologies = 8, AssaultPopulation = 26, AssaultWorkers = 10, RegroupBelow = 4, DefenceRadius = 18000,
                    FleetTarget = 2
                };
                // A larger economy, quicker reactions and a wider guard. Research waits for the army
                // to grow, and a larger army attacks from minute six once it has advanced an Era.
                case OfflineAiDifficulty.Hard: return new OfflineAiTuning
                {
                    ThinkInterval = 6, CommandsPerThink = 6, WorkerTarget = 20, ArmyCap = 40, PopulationGoal = 80, MusterHalls = 3, HallArmy = 6,
                    ResearchArmy = 4, ResearchArmyStep = 3, AssaultEra = 2, AssaultTechnologies = 2, AssaultPopulation = 24, AssaultWorkers = 14,
                    RegroupBelow = 8, DefenceRadius = 28000, EarliestAssaultTick = World.TickRate * 360L, KeepProducersTraining = true, FocusedEngagement = true,
                    FleetTarget = 4
                };
                // Attacks with a formed army once it has advanced an Era, from minute seven.
                default: return new OfflineAiTuning
                {
                    ThinkInterval = 10, CommandsPerThink = 4, WorkerTarget = 16, ArmyCap = 30, PopulationGoal = 70, MusterHalls = 2, HallArmy = 6,
                    ResearchArmy = 4, AssaultEra = 2, AssaultTechnologies = 2, AssaultPopulation = 18, AssaultWorkers = 12, RegroupBelow = 6,
                    DefenceRadius = 24000, EarliestAssaultTick = World.TickRate * 420L, KeepProducersTraining = true, FocusedEngagement = true
                };
            }
        }
    }

    // Deterministic ordinary-command opponent. No extra resources, hidden targets or direct state mutation.
    public sealed class OfflineAi
    {
        // Normal difficulty cadence; Easy thinks less often and Hard more often.
        public const int ThinkIntervalTicks = 10;
        public const int MaximumCommandsPerThink = 4;
        private readonly OfflineAiTuning tuning;
        private readonly World world;
        private readonly PlayerState player;
        private readonly Dictionary<string, UnitDefinition> unitDefinitions = new Dictionary<string, UnitDefinition>();
        private readonly Dictionary<string, BuildingDefinition> buildingDefinitions = new Dictionary<string, BuildingDefinition>();
        private readonly Dictionary<int, int> workerDestinations = new Dictionary<int, int>();
        private readonly Dictionary<int, long> retreatUntil = new Dictionary<int, long>();
        private readonly Dictionary<int, SimPoint> strategicGoals = new Dictionary<int, SimPoint>();
        private readonly List<UnitState> workers = new List<UnitState>();
        private readonly List<UnitState> army = new List<UnitState>();
        private readonly List<UnitState> fleet = new List<UnitState>();
        // Naval realm only (the naval realm's map has a sea): docks, ships, raids and landings. Every other match keeps
        // exactly the land plan below.
        private readonly bool naval;
        private List<SimPoint> dockSites;
        private int dockSiteCursor;
        private SimPoint? raidPoint, harbourPoint;
        private int landingShipId;
        private long landingStarted, nextDockScout;
        private readonly HashSet<GridCell> publicTerrain = new HashSet<GridCell>();
        private readonly HashSet<SimPoint> failedScoutGoals = new HashSet<SimPoint>();
        private readonly Dictionary<int, int> objectiveAssignments = new Dictionary<int, int>();
        private TechnologyDefinition plannedResearch;
        private bool assaultCommitted;
        private readonly int[] gatherers = new int[4];
        private long nextThink, nextBuild, nextResearch, nextRebalance;
        private int ordersThisThink, recruitmentIndex, placementCursor;
        private SimPoint home;
        private bool ExpandedArmy => player.FactionId != null && player.FactionId != "aven" && player.FactionId != "serevin";
        // A Beast Lodge only for a faction with a creature to raise there. The Frostguard's clans, the desert riders, the
        // English, the pirates and the navies have none, so they never pay for an empty lodge.
        private bool LodgeTrainsForUs()
        {
            if (!buildingDefinitions.TryGetValue("beast_lodge", out var lodge)) return false;
            foreach (string id in lodge.TrainableUnitIds ?? Array.Empty<string>()) if (world.ValidateUnitRecruitment(PlayerId, id).Accepted) return true;
            return false;
        }
        // Placement, regroup and scouting searches run in a frame mirrored through the map centre for
        // a seat that starts beyond it, so mirrored starts produce exact mirror images, rounding and
        // tie-breaks included, instead of favouring one side of the map.
        private bool FlipX => home.X * 2L > (long)world.Map.WidthCells * world.Map.CellSizeMillimetres;
        private bool FlipZ => home.Z * 2L > (long)world.Map.HeightCells * world.Map.CellSizeMillimetres;
        private SimPoint Frame(SimPoint point) => new SimPoint(FlipX ? world.Map.WidthCells * world.Map.CellSizeMillimetres - point.X : point.X,
            FlipZ ? world.Map.HeightCells * world.Map.CellSizeMillimetres - point.Z : point.Z);
        private bool ObservedFortifications
        {
            get
            {
                foreach (var enemy in Observation.KnownEnemies)
                    if (enemy.IsBuilding && buildingDefinitions.TryGetValue(enemy.DefinitionId, out var definition) && (definition.IsWall || definition.IsGate || definition.Id == "keep")) return true;
                return false;
            }
        }
        public int PlayerId => player.Id;
        public OfflineAiDifficulty Difficulty { get; }
        public OfflineAiObservation Observation { get; } = new OfflineAiObservation();
        public OfflineAiStatistics Statistics { get; } = new OfflineAiStatistics();
        public string Status { get; private set; } = "Preparing";
        public int WorkerCount => workers.Count;
        public int ArmyCount => army.Count;
        public event Action<IGameCommand, CommandResult> CommandIssued;

        public OfflineAi(World world, int playerId, OfflineAiDifficulty difficulty = OfflineAiDifficulty.Normal)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (world.Match == null || world.Vision == null) throw new ArgumentException("Offline AI requires an opt-in match with vision.");
            if (!world.TryGetPlayer(playerId, out player)) throw new ArgumentException("AI player does not exist.");
            if (!Enum.IsDefined(typeof(OfflineAiDifficulty), difficulty)) throw new ArgumentException("Unknown AI difficulty.", nameof(difficulty));
            Difficulty = difficulty; tuning = OfflineAiTuning.For(difficulty);
            foreach (var item in world.Definition.Units) unitDefinitions.Add(item.Id, item);
            foreach (var item in world.Definition.Buildings) buildingDefinitions.Add(item.Id, item);
            foreach (var cell in world.Map.BlockedCells ?? Array.Empty<GridCell>()) publicTerrain.Add(cell);
            Observation.Refresh(world, playerId);
            if (Observation.OwnedBuildings.Count > 0) home = Observation.OwnedBuildings[0].Position;
            else if (Observation.OwnedUnits.Count > 0) home = Observation.OwnedUnits[0].Position;
            naval = world.Map.RealmId == ContentRealms.Naval && world.HasDeepWater;
        }

        public void Tick()
        {
            if (world.Match.IsFinished || world.TickIndex < nextThink) return;
            nextThink = world.TickIndex + tuning.ThinkInterval;
            Statistics.Thinks++; ordersThisThink = 0;
            Observation.Refresh(world, PlayerId); workers.Clear(); army.Clear(); fleet.Clear();
            foreach (var unit in Observation.OwnedUnits)
            {
                // Ships sail as their own fleet; soldiers walking aboard one belong to the landing, not the army.
                if (unit.Domain == MovementDomain.Water) fleet.Add(unit);
                else if (unit.IsWorker) workers.Add(unit);
                else if (unit.EmbarkShipId != 0) continue;
                else if (unit.AttackDamage > 0) army.Add(unit);
            }
            foreach (var building in Observation.OwnedBuildings)
                if (building.DefinitionId == world.Map.OfflineMatch.CentralBuildingId) { home = building.Position; break; }
            plannedResearch = PlanResearch();
            DirectArmy();
            if (naval) DirectFleet();
            AssignWorkers();
            ResumeConstruction();
            DevelopBase();
            Research();
            Recruit();
            Charter();
        }

        private bool Budget => ordersThisThink < tuning.CommandsPerThink;
        private bool Affordable(ResourceAmount cost) => player.Resources.Food >= cost.Food && player.Resources.Wood >= cost.Wood && player.Resources.Metal >= cost.Metal && player.Resources.Stone >= cost.Stone;
        // Recruitment's spending rule: a small army buys troops even while saving for research.
        private bool Payable(UnitDefinition unit) => army.Count < 6 && !unit.IsWorker ? Affordable(unit.Cost) : CanSpend(unit.Cost);
        private bool CanSpend(ResourceAmount cost)
        {
            var reserve = plannedResearch?.Cost ?? default;
            return (long)player.Resources.Food >= (long)cost.Food + reserve.Food && (long)player.Resources.Wood >= (long)cost.Wood + reserve.Wood &&
                (long)player.Resources.Metal >= (long)cost.Metal + reserve.Metal && (long)player.Resources.Stone >= (long)cost.Stone + reserve.Stone;
        }
        private CommandResult Send(IGameCommand command)
        {
            ordersThisThink++; Statistics.Commands++;
            var before = player.Resources;
            var result = world.Submit(command);
            if (result.Accepted)
            {
                Statistics.Accepted++;
                var cost = Statistics.Spent; var after = player.Resources;
                cost.Food += before.Food - after.Food; cost.Wood += before.Wood - after.Wood;
                cost.Metal += before.Metal - after.Metal; cost.Stone += before.Stone - after.Stone; Statistics.Spent = cost;
                if (command is GatherCommand) Statistics.GatherOrders++;
                else if (command is BuildCommand) Statistics.BuildingsStarted++;
                else if (command is TrainCommand) Statistics.UnitsQueued++;
                else if (command is ResearchCommand) Statistics.ResearchStarted++;
                else if (command is AttackCommand) Statistics.AttackOrders++;
            }
            else
            {
                Statistics.Rejected++;
                Statistics.Rejections.TryGetValue(result.Reason, out int seen);
                Statistics.Rejections[result.Reason] = seen + 1;
            }
            CommandIssued?.Invoke(command, result);
            return result;
        }

        private void AssignWorkers()
        {
            Array.Clear(gatherers, 0, gatherers.Length);
            foreach (var worker in workers)
                foreach (var resource in Observation.KnownResources)
                    if (worker.TargetResourceId == resource.Id && worker.WorkerTask != WorkerTask.None) { gatherers[(int)resource.Kind]++; break; }
            foreach (var worker in workers)
            {
                if (!Budget) return;
                if (worker.WorkerTask != WorkerTask.None) continue;
                if (worker.CarriedAmount > 0)
                {
                    if (Send(new ReturnCargoCommand(PlayerId, new[] { worker.Id })).Accepted) return;
                    continue;
                }
                ObservedResource chosen = null;
                if (workerDestinations.TryGetValue(worker.Id, out int prior))
                    foreach (var item in Observation.KnownResources) if (item.Id == prior && item.LastObservedAmount > 0) { chosen = item; break; }
                if (chosen == null) chosen = ChooseResource(worker);
                if (chosen == null) { Scout(worker); return; }
                workerDestinations[worker.Id] = chosen.Id;
                if (chosen.Visible)
                {
                    if (Send(new GatherCommand(PlayerId, new[] { worker.Id }, chosen.Id)).Accepted) { workerDestinations.Remove(worker.Id); return; }
                }
                else if (worker.Order == UnitOrder.Idle)
                {
                    var target = AdjacentResourcePoint(chosen.Position, worker.Position);
                    if (Send(new MoveCommand(PlayerId, new[] { worker.Id }, target)).Accepted) { Statistics.ScoutOrders++; return; }
                }
            }
            if (world.TickIndex < nextRebalance || !Budget || workers.Count < 6) return;
            nextRebalance = world.TickIndex + 200;
            foreach (var worker in workers)
            {
                if (worker.CarriedAmount > 0 || worker.WorkerTask == WorkerTask.Constructing || worker.WorkerTask == WorkerTask.MovingToConstruction) continue;
                var chosen = ChooseResource(worker);
                if (chosen == null || !chosen.Visible || worker.TargetResourceId == chosen.Id) continue;
                ResourceKind current = chosen.Kind;
                foreach (var node in Observation.KnownResources) if (node.Id == worker.TargetResourceId) { current = node.Kind; break; }
                if (chosen.Kind != current && gatherers[(int)current] > DesiredGatherers(current))
                { Send(new GatherCommand(PlayerId, new[] { worker.Id }, chosen.Id)); return; }
            }
        }

        private int DesiredGatherers(ResourceKind kind)
        {
            int reserve = plannedResearch?.Cost.Get(kind) ?? 0;
            if (kind == ResourceKind.Food) return Math.Max(2, workers.Count * (player.Resources.Food < reserve + 200 ? 6 : 5) / 10);
            if (kind == ResourceKind.Wood) return Math.Max(2, workers.Count * (player.Resources.Wood < reserve + 200 ? 3 : 2) / 10);
            if (kind == ResourceKind.Metal) return workers.Count >= 6 && player.Resources.Metal < reserve + 150 ? 1 : 0;
            return workers.Count >= 8 && player.Resources.Stone < reserve + 100 ? 1 : 0;
        }

        private ObservedResource ChooseResource(UnitState worker)
        {
            ObservedResource best = null; long bestScore = long.MinValue;
            foreach (var node in Observation.KnownResources)
            {
                if (node.LastObservedAmount <= 0) continue;
                int desired = DesiredGatherers(node.Kind);
                if (desired == 0) continue;
                long priority = (desired - gatherers[(int)node.Kind]) * 1000000000L;
                if (player.Resources.Get(node.Kind) < 40) priority += 200000000;
                priority -= DistanceSquared(worker.Position, node.Position);
                if (priority > bestScore) { best = node; bestScore = priority; }
            }
            return best;
        }

        private void ResumeConstruction()
        {
            if (!Budget) return;
            foreach (var site in Observation.OwnedBuildings)
            {
                if (site.IsComplete) continue;
                foreach (var worker in workers) if (worker.TargetBuildingId == site.Id && (worker.WorkerTask == WorkerTask.Constructing || worker.WorkerTask == WorkerTask.MovingToConstruction)) return;
                var builder = BuilderNear(site.Position);
                if (builder != null) Send(new ConstructCommand(PlayerId, new[] { builder.Id }, site.Id));
                return;
            }
        }

        private int BuildingCount(string id)
        {
            int count = 0; foreach (var item in Observation.OwnedBuildings) if (item.DefinitionId == id) count++;
            return count;
        }
        private void DevelopBase()
        {
            if (!Budget || workers.Count == 0 || world.TickIndex < nextBuild) return;
            foreach (var item in Observation.OwnedBuildings) if (!item.IsComplete) return;
            string wanted = null; SimPoint anchor = home;
            if (player.PopulationCapacity - player.PopulationUsed - player.PopulationReserved < (ExpandedArmy ? 5 : 3) && player.PopulationCapacity < tuning.PopulationGoal) wanted = "shelter";
            else if (BuildingCount("muster_hall") == 0 && workers.Count >= 5) wanted = "muster_hall";
            else if (naval && BuildingCount("dock") == 0 && workers.Count >= 6) wanted = "dock";
            else if (player.EraTier >= 2 && BuildingCount("archive") == 0 && army.Count >= 4) wanted = "archive";
            else if (ExpandedArmy && player.EraTier >= 2 && BuildingCount("beast_lodge") == 0 && army.Count >= 5 && LodgeTrainsForUs()) wanted = "beast_lodge";
            else if ((ExpandedArmy || ObservedFortifications) && player.EraTier >= 2 && BuildingCount("siege_workshop") == 0 && army.Count >= 10) wanted = "siege_workshop";
            else if (ExpandedArmy && player.EraTier >= 2 && BuildingCount("watchtower") == 0 && army.Count >= 8 && player.Resources.Stone >= 160) wanted = "watchtower";
            // Further halls follow the army; the first one comes from the worker threshold above.
            else if (BuildingCount("muster_hall") > 0 && BuildingCount("muster_hall") < tuning.MusterHalls && army.Count >= tuning.HallArmy * BuildingCount("muster_hall") && player.Resources.Wood >= 160) wanted = "muster_hall";
            else if (BuildingCount("storeyard") < 3 && player.Resources.Wood >= 80)
            {
                long furthest = 64000000;
                foreach (var worker in workers)
                {
                    if (worker.WorkerTask != WorkerTask.Gathering) continue;
                    long nearest = long.MaxValue;
                    foreach (var site in Observation.OwnedBuildings)
                        if (site.IsOperational && buildingDefinitions[site.DefinitionId].CanDropOff) nearest = Math.Min(nearest, DistanceSquared(worker.Position, site.Position));
                    if (nearest > furthest) { furthest = nearest; anchor = worker.Position; wanted = "storeyard"; }
                }
                if (wanted == null && BuildingCount("storeyard") == 0 && player.Resources.Metal >= 20)
                    foreach (var site in Observation.OwnedBuildings) if (site.DefinitionId == "muster_hall") { anchor = site.Position; wanted = "storeyard"; break; }
            }
            if (wanted == null || !buildingDefinitions.TryGetValue(wanted, out var definition) || !CanSpend(definition.Cost)) return;
            if (!world.ValidateFactionRequirement(PlayerId, definition.RequiredFactionId).Accepted || !world.ValidateRequirements(PlayerId, definition.RequiredEraId, definition.RequiredTechnologyIds).Accepted) return;
            if (definition.RequiresShore) { nextBuild = world.TickIndex + 60; PlaceOnShore(definition); return; }
            var builder = BuilderNear(anchor); if (builder == null) return;
            nextBuild = world.TickIndex + 60;
            int cell = world.Map.CellSizeMillimetres;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int slot = placementCursor++ % 40;
                int ring = 4 + slot / 8 * 2;
                int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 }, dz = { 0, 1, 1, 1, 0, -1, -1, -1 };
                var local = Frame(anchor);
                var point = Frame(new SimPoint((local.X / cell + dx[slot % 8] * ring - definition.WidthCells / 2) * cell + definition.WidthCells * cell / 2,
                    (local.Z / cell + dz[slot % 8] * ring - definition.DepthCells / 2) * cell + definition.DepthCells * cell / 2));
                if (!VisibleFootprint(point, definition)) continue;
                var command = new BuildCommand(PlayerId, new[] { builder.Id }, wanted, point);
                if (!world.ValidatePlacement(PlayerId, new[] { builder.Id }, wanted, point).Accepted) continue;
                if (Send(command).Accepted) Status = "Building " + wanted;
                return;
            }
        }

        private bool VisibleFootprint(SimPoint center, BuildingDefinition definition)
        {
            int cell = world.Map.CellSizeMillimetres;
            int left = center.X - definition.WidthCells * cell / 2, bottom = center.Z - definition.DepthCells * cell / 2;
            for (int z = 0; z < definition.DepthCells; z++) for (int x = 0; x < definition.WidthCells; x++)
                if (!world.Vision.IsVisible(PlayerId, new SimPoint(left + x * cell + cell / 2, bottom + z * cell + cell / 2))) return false;
            return true;
        }
        private UnitState BuilderNear(SimPoint point)
        {
            UnitState best = null; long distance = long.MaxValue;
            foreach (var worker in workers)
            {
                if (worker.WorkerTask == WorkerTask.Constructing || worker.WorkerTask == WorkerTask.MovingToConstruction) continue;
                long candidate = DistanceSquared(worker.Position, point);
                if (candidate < distance) { distance = candidate; best = worker; }
            }
            return best;
        }

        private TechnologyDefinition PlanResearch()
        {
            int started = player.CompletedTechnologyIds.Count;
            foreach (var site in Observation.OwnedBuildings) if (site.ActiveResearch != null) started++;
            if (army.Count < tuning.ResearchArmy + tuning.ResearchArmyStep * started) return null;
            string[] priority = { "advance_kingdom", "gather_1", "weapons_1", "armor_1", "advance_dominion", "gather_2", "advance_empire", "weapons_2", "armor_2" };
            TechnologyDefinition best = null; int bestScore = int.MaxValue;
            foreach (var technology in world.Definition.Technologies ?? Array.Empty<TechnologyDefinition>())
            {
                if (player.HasTechnology(technology.Id) || !world.ValidateFactionRequirement(PlayerId, technology.RequiredFactionId).Accepted ||
                    !world.ValidateRequirements(PlayerId, technology.RequiredEraId, technology.RequiredTechnologyIds).Accepted) continue;
                bool eligible = true;
                foreach (string required in technology.RequiredBuildingIds ?? Array.Empty<string>())
                {
                    bool found = false; foreach (var building in Observation.OwnedBuildings) if (building.DefinitionId == required && building.IsOperational) { found = true; break; }
                    if (!found) eligible = false;
                }
                if (!string.IsNullOrEmpty(technology.AdvancesToEraId))
                    foreach (var era in world.Definition.Eras) if (era.Id == technology.AdvancesToEraId && era.Tier != player.EraTier + 1) eligible = false;
                if (!eligible) continue;
                foreach (var site in Observation.OwnedBuildings) if (site.ActiveResearch?.TechnologyId == technology.Id) eligible = false;
                if (!eligible) continue;
                foreach (var site in Observation.OwnedBuildings)
                {
                    if (!site.IsOperational || site.DefinitionId != technology.ResearchBuildingId || site.ActiveResearch != null) continue;
                    int score = Array.IndexOf(priority, technology.Id); if (score < 0) score = ExpandedArmy && technology.RequiredFactionId == player.FactionId ? 6 : 100;
                    if (score < bestScore) { best = technology; bestScore = score; }
                    break;
                }
            }
            return best;
        }

        private void Research()
        {
            if (!Budget || plannedResearch == null || world.TickIndex < nextResearch) return;
            nextResearch = world.TickIndex + 40;
            foreach (var site in Observation.OwnedBuildings)
            {
                if (site.DefinitionId != plannedResearch.ResearchBuildingId) continue;
                var command = new ResearchCommand(PlayerId, site.Id, plannedResearch.Id);
                if (!world.ValidateResearch(command).Accepted) continue;
                if (Send(command).Accepted) { Status = "Researching " + plannedResearch.Id; plannedResearch = null; }
                return;
            }
        }

        private void Recruit()
        {
            if (!Budget || player.PopulationUsed + player.PopulationReserved >= player.PopulationCapacity) return;
            int queuedWorkers = 0;
            foreach (var site in Observation.OwnedBuildings) foreach (var entry in site.ProductionQueue) if (unitDefinitions[entry.UnitDefinitionId].IsWorker) queuedWorkers++;
            var reservedCreature = CreatureRecruitmentReserve();
            BuildingState researchSite = null;
            if (plannedResearch != null)
                foreach (var site in Observation.OwnedBuildings)
                    if (site.DefinitionId == plannedResearch.ResearchBuildingId && site.IsOperational && site.ActiveResearch == null) { researchSite = site; break; }
            foreach (var site in Observation.OwnedBuildings)
            {
                if (!Budget) return;
                if (!site.IsOperational || site.ActiveResearch != null || site.CharterRemainingTicks > 0 || site.ProductionQueue.Count >= 2) continue;
                // The planned research's host lets its queue drain; with KeepProducersTraining the
                // other producers of that kind keep training instead of waiting with it.
                if (plannedResearch != null && site.DefinitionId == plannedResearch.ResearchBuildingId && (!tuning.KeepProducersTraining || site == researchSite)) continue;
                if (buildingDefinitions[site.DefinitionId].RequiresShore)
                {
                    var ship = RecruitShip(site);
                    if (ship != null && Payable(ship) && player.PopulationUsed + player.PopulationReserved + ship.PopulationCost <= player.PopulationCapacity)
                        Send(new TrainCommand(PlayerId, site.Id, ship.Id));
                    continue;
                }
                UnitDefinition chosen = null;
                if (workers.Count + queuedWorkers < tuning.WorkerTarget) chosen = RecruitWorker(site);
                if (chosen == null && army.Count < tuning.ArmyCap) chosen = RecruitSoldier(site);
                if (chosen == null || !Payable(chosen) || player.PopulationUsed + player.PopulationReserved + chosen.PopulationCost > player.PopulationCapacity) continue;
                if (reservedCreature != null && !chosen.IsWorker && chosen.Id != reservedCreature.Id)
                {
                    var reserve = reservedCreature.Cost;
                    if ((long)player.Resources.Food - chosen.Cost.Food < reserve.Food || (long)player.Resources.Wood - chosen.Cost.Wood < reserve.Wood ||
                        (long)player.Resources.Metal - chosen.Cost.Metal < reserve.Metal || (long)player.Resources.Stone - chosen.Cost.Stone < reserve.Stone ||
                        player.PopulationCapacity - player.PopulationUsed - player.PopulationReserved - chosen.PopulationCost < reservedCreature.PopulationCost) continue;
                }
                if (!world.ValidateUnitRecruitment(PlayerId, chosen.Id).Accepted || !world.ValidateRequirements(PlayerId, chosen.RequiredEraId, chosen.RequiredTechnologyIds).Accepted) continue;
                if (Send(new TrainCommand(PlayerId, site.Id, chosen.Id)).Accepted)
                { if (chosen.IsWorker) queuedWorkers++; else recruitmentIndex++; }
            }
        }

        private UnitDefinition CreatureRecruitmentReserve()
        {
            if (!ExpandedArmy || !world.factionCatalog.Definitions.TryGetValue(player.FactionId, out var faction) ||
                !unitDefinitions.TryGetValue(faction.UniqueUnitId ?? "", out var unique) || (unique.Tags & CombatTags.Creature) == 0 ||
                !world.ValidateRequirements(PlayerId, unique.RequiredEraId, unique.RequiredTechnologyIds).Accepted) return null;
            bool producer = false; int count = 0;
            foreach (var unit in army) if (unit.DefinitionId == unique.Id) count++;
            foreach (var building in Observation.OwnedBuildings)
            {
                if (building.IsOperational && Array.IndexOf(buildingDefinitions[building.DefinitionId].TrainableUnitIds, unique.Id) >= 0) producer = true;
                foreach (var queued in building.ProductionQueue) if (queued.UnitDefinitionId == unique.Id) count++;
            }
            return producer && count < Math.Max(1, Math.Min(3, army.Count / 10)) ? unique : null;
        }

        // The cheapest worker, plus one longer-sighted worker (such as a treasure seeker) once the
        // economy has six workers.
        private UnitDefinition RecruitWorker(BuildingState site)
        {
            var available = new List<UnitDefinition>();
            foreach (var id in buildingDefinitions[site.DefinitionId].TrainableUnitIds)
            {
                var definition = unitDefinitions[id];
                if (definition.IsWorker && world.ValidateUnitRecruitment(PlayerId, id).Accepted &&
                    world.ValidateRequirements(PlayerId, definition.RequiredEraId, definition.RequiredTechnologyIds).Accepted) available.Add(definition);
            }
            UnitDefinition basic = null, scout = null;
            foreach (var definition in available) if (basic == null || Price(definition) < Price(basic)) basic = definition;
            foreach (var definition in available) if (Vision(definition) > Vision(scout ?? basic)) scout = definition;
            if (scout == null || workers.Count < 6 || !Payable(scout)) return basic;
            foreach (var unit in workers) if (unit.DefinitionId == scout.Id) return basic;
            foreach (var building in Observation.OwnedBuildings) foreach (var entry in building.ProductionQueue) if (entry.UnitDefinitionId == scout.Id) return basic;
            return scout;
        }
        private int Vision(UnitDefinition unit) => unit.VisionCells > 0 ? unit.VisionCells : world.Map.OfflineMatch.VisionUnitCells;
        private static int Price(UnitDefinition unit) => unit.Cost.Food + unit.Cost.Wood + unit.Cost.Metal + unit.Cost.Stone;

        private UnitDefinition RecruitSoldier(BuildingState site)
        {
            var candidates = new List<UnitDefinition>();
            foreach (var id in buildingDefinitions[site.DefinitionId].TrainableUnitIds)
            {
                var candidate = unitDefinitions[id];
                // Only what can be paid now: a hall waiting for metal it is not gathering would stand idle
                // while food and wood pile up.
                if (candidate.Attack.Damage == 0 || candidate.IsWorker || candidate.Domain != MovementDomain.Land || !Payable(candidate) || !world.ValidateUnitRecruitment(PlayerId, candidate.Id).Accepted ||
                    !world.ValidateRequirements(PlayerId, candidate.RequiredEraId, candidate.RequiredTechnologyIds).Accepted) continue;
                if (candidate.SiegeEquipment == SiegeEquipmentKind.Ram)
                {
                    int rams = 0; foreach (var unit in army) if (unit.DefinitionId == candidate.Id) rams++;
                    foreach (var producer in Observation.OwnedBuildings) foreach (var queued in producer.ProductionQueue) if (queued.UnitDefinitionId == candidate.Id) rams++;
                    if (rams >= 2) continue;
                }
                candidates.Add(candidate);
            }
            if (candidates.Count == 0) return null;
            // Rotate through every available soldier, so realm companies and newer units take the
            // field, while the enemy troops seen still favour their ordinary counters.
            string fallback = candidates[recruitmentIndex % candidates.Count].Id;
            UnitDefinition best = null; int bestScore = int.MinValue;
            foreach (var candidate in candidates)
            {
                int score = Counter(candidate) + (candidate.Id == fallback ? 20 : 0);
                // The faction's own soldier is favoured until it makes up a third of the army, so it
                // appears in every army without replacing the shared roles and their counters.
                if (candidate.RequiredFactionId == player.FactionId && !string.IsNullOrEmpty(player.FactionId) && Share(candidate.Id) * 3 < army.Count + 1) score += 120;
                score -= Price(candidate);
                if (score > bestScore) { best = candidate; bestScore = score; }
            }
            return best;
        }

        // Soldiers of one kind in the army or queued at any producer.
        private int Share(string definitionId)
        {
            int count = 0;
            foreach (var unit in army) if (unit.DefinitionId == definitionId) count++;
            foreach (var building in Observation.OwnedBuildings) foreach (var entry in building.ProductionQueue) if (entry.UnitDefinitionId == definitionId) count++;
            return count;
        }

        // The average damage multiplier (permille) this unit would apply to the enemy troops in view.
        private int Counter(UnitDefinition candidate)
        {
            long total = 0; int seen = 0;
            foreach (var enemy in Observation.KnownEnemies)
            {
                if (!enemy.Visible || enemy.IsBuilding || (enemy.Tags & CombatTags.Worker) != 0) continue;
                int multiplier = 1000;
                foreach (var bonus in candidate.Attack.Bonuses ?? Array.Empty<DamageBonus>())
                    if ((bonus.TargetTags & enemy.Tags) != 0) multiplier = Math.Max(multiplier, bonus.MultiplierPermille);
                total += multiplier; seen++;
            }
            return seen == 0 ? 1000 : (int)(total / seen);
        }

        private void Charter()
        {
            if (!Budget || army.Count < 4) return;
            foreach (var site in Observation.OwnedBuildings)
            {
                var command = new SetCharterCommand(PlayerId, site.Id, StoreyardCharter.Muster);
                if (world.ValidateFactionAction(command).Accepted) { Send(command); return; }
            }
        }

        private void DirectArmy()
        {
            if (army.Count == 0) return;
            // Finite food can leave both armies below the normal buildup threshold. Commit the
            // surviving army after fifteen minutes instead of alternating guard and retreat forever.
            bool lastArmy = world.Match.Mode == VictoryMode.Conquest && world.TickIndex >= 18000 && player.Resources.Food < 100;
            // A focused defence fights to the end at home: pulling wounded soldiers back a few
            // metres only takes their damage out of the fight around the Hearth.
            bool defending = false;
            if (tuning.FocusedEngagement)
            {
                long guard = (long)tuning.DefenceRadius * tuning.DefenceRadius;
                // Ships off the coast are the fleet's to answer: the army cannot wade out to them.
                foreach (var enemy in Observation.KnownEnemies)
                    if (enemy.Visible && !enemy.IsBuilding && (enemy.Tags & (CombatTags.Worker | CombatTags.Naval)) == 0 && DistanceSquared(enemy.Position, home) < guard) { defending = true; break; }
            }
            foreach (var unit in army)
            {
                if (lastArmy || defending) break;
                if (!Budget) break;
                // Home has to mean the ground a retreat actually reaches. NearKnownPoint hands back a ring
                // four to eight cells out, so a tighter circle than that left the wounded walking back and
                // forth between the two definitions for the rest of the match.
                long reached = 12L * world.Map.CellSizeMillimetres;
                if (unit.Health * 4 >= unit.MaxHealth || DistanceSquared(unit.Position, home) < reached * reached) continue;
                if (retreatUntil.TryGetValue(unit.Id, out long until) && until > world.TickIndex) continue;
                if (unitDefinitions[unit.DefinitionId].CanReposition && world.ValidateFactionAction(new RepositionCommand(PlayerId, new[] { unit.Id })).Accepted)
                    Send(new RepositionCommand(PlayerId, new[] { unit.Id }));
                if (!Budget) break;
                // The cooldown is set either way. A retreat point can fail to leave a large unit enough static
                // clearance, and a refusal that does not throttle is a refusal repeated every think forever.
                bool retreated = Send(new MoveCommand(PlayerId, new[] { unit.Id }, NearKnownPoint(home, unit.Position))).Accepted;
                retreatUntil[unit.Id] = world.TickIndex + 400;
                if (retreated) { Statistics.RetreatOrders++; break; }
            }
            if (!Budget) return;
            var ready = new List<UnitState>();
            foreach (var unit in army) if (lastArmy || defending || !retreatUntil.TryGetValue(unit.Id, out long until) || until <= world.TickIndex) ready.Add(unit);
            if (ready.Count == 0) return;
            if (world.Match.Mode == VictoryMode.Dominion && world.Match.Objectives.Count > 0)
            { DirectDominion(ready); return; }
            // The assault waits for an army of a given size, an economy and development set by the
            // difficulty; population measures the army, so creature and infantry armies compare fairly.
            bool developed = world.Definition.Eras.Length == 0 || player.EraTier >= tuning.AssaultEra && player.CompletedTechnologyIds.Count >= tuning.AssaultTechnologies;
            int militaryPopulation = 0; foreach (var unit in ready) militaryPopulation += unit.PopulationCost;
            if (militaryPopulation >= tuning.AssaultPopulation && workers.Count >= tuning.AssaultWorkers && developed && world.TickIndex >= tuning.EarliestAssaultTick) assaultCommitted = true;
            if (lastArmy) assaultCommitted = true;
            else if (ready.Count < tuning.RegroupBelow) assaultCommitted = false;
            ObservedEnemy target = null; long nearest = long.MaxValue;
            long defence = (long)tuning.DefenceRadius * tuning.DefenceRadius;
            var reference = tuning.FocusedEngagement ? Centroid(ready) : ready[0].Position;
            foreach (var enemy in Observation.KnownEnemies)
            {
                if (!enemy.Visible || (enemy.Tags & CombatTags.Naval) != 0) continue;
                bool threat = !enemy.IsBuilding && DistanceSquared(enemy.Position, home) < defence;
                if (!assaultCommitted && !threat) continue;
                // A focused defence meets the raider closest to home first.
                long score = DistanceSquared(threat && tuning.FocusedEngagement ? home : reference, enemy.Position);
                if (threat) score -= 2000000000;
                else if (enemy.IsBuilding && enemy.DefinitionId == world.Map.OfflineMatch.CentralBuildingId) score -= 150000000;
                if (score < nearest) { nearest = score; target = enemy; }
            }
            if (target != null)
            {
                var ids = new List<int>();
                foreach (var unit in ready)
                    if (unit.AttackTargetId != target.Id && unit.RepositionRemainingTicks == 0 && !(tuning.FocusedEngagement && Engaged(unit))) ids.Add(unit.Id);
                if (ids.Count > 0 && Send(new AttackCommand(PlayerId, ids.ToArray(), target.Id)).Accepted) Status = "Engaging observed enemy";
                return;
            }
            if (assaultCommitted)
                foreach (var known in Observation.KnownEnemies)
                    if (known.IsBuilding) { MoveSquad(ready, NearKnownPoint(known.Position, ready[0].Position), "Investigating last-seen building"); return; }
            Scout(ready[0]);
            if (assaultCommitted && strategicGoals.TryGetValue(ready[0].Id, out var goal)) MoveSquad(ready, goal, "Coordinated assault scouting");
            else if (ready.Count > 1)
            {
                var guard = new List<UnitState>(ready); guard.RemoveAt(0);
                MoveSquad(guard, NearKnownPoint(home, new SimPoint(world.Map.WidthCells * world.Map.CellSizeMillimetres / 2, world.Map.HeightCells * world.Map.CellSizeMillimetres / 2)), "Developing economy and defending approaches");
            }
        }

        private void DirectDominion(List<UnitState> ready)
        {
            var objectives = new List<DominionObjectiveState>(world.Match.Objectives);
            objectives.Sort((a, b) => { int distance = DistanceSquared(a.Position, home).CompareTo(DistanceSquared(b.Position, home)); return distance != 0 ? distance : string.CompareOrdinal(a.Id, b.Id); });
            var groups = new[] { new List<UnitState>(), new List<UnitState>() };
            foreach (var unit in ready)
                if (objectiveAssignments.TryGetValue(unit.Id, out int assigned) && assigned < 2) groups[assigned].Add(unit);
            foreach (var unit in ready)
            {
                if (objectiveAssignments.ContainsKey(unit.Id)) continue;
                int destination = groups[0].Count < 3 ? 0 : 1;
                objectiveAssignments[unit.Id] = destination; groups[destination].Add(unit);
            }
            for (int index = 0; index < groups.Length && index < objectives.Count && Budget; index++)
            {
                var squad = groups[index]; if (squad.Count == 0) continue;
                var objective = objectives[index]; ObservedEnemy target = null; long nearest = long.MaxValue;
                foreach (var enemy in Observation.KnownEnemies)
                {
                    if (!enemy.Visible || enemy.IsBuilding || (enemy.Tags & (CombatTags.Worker | CombatTags.Naval)) != 0) continue;
                    long distance = DistanceSquared(enemy.Position, objective.Position);
                    if (distance > (long)(objective.RadiusMillimetres + 2500) * (objective.RadiusMillimetres + 2500)) continue;
                    if (distance < nearest) { target = enemy; nearest = distance; }
                }
                var attack = new List<int>(); var moving = new List<UnitState>();
                foreach (var unit in squad)
                {
                    if (target != null && DistanceSquared(unit.Position, objective.Position) < (long)(objective.RadiusMillimetres + 4000) * (objective.RadiusMillimetres + 4000))
                    { if (unit.AttackTargetId != target.Id && unit.RepositionRemainingTicks == 0 && !(tuning.FocusedEngagement && Engaged(unit))) attack.Add(unit.Id); }
                    else moving.Add(unit);
                }
                if (attack.Count > 0 && Budget) Send(new AttackCommand(PlayerId, attack.ToArray(), target.Id));
                // A retreating enemy outside the beacon's leash is no longer a reason to abandon capture/hold.
                // Standing anywhere inside the ring holds the beacon, so arriving there is arriving.
                if (Budget) MoveSquad(moving, objective.Position, "Capturing or defending public beacon", objective.RadiusMillimetres);
            }
        }

        /// <summary>
        /// Orders a squad to a point, skipping whoever is already there. <paramref name="arrival"/> is how
        /// close counts as arrived: a formation seats its members metres apart, so a tolerance smaller than
        /// the formation itself declares an arrived squad late and re-seats it — cancelling its attacks —
        /// every time the controller thinks.
        /// </summary>
        private void MoveSquad(List<UnitState> units, SimPoint goal, string status, int arrival = 1500)
        {
            long reached = (long)arrival * arrival;
            var ids = new List<int>();
            foreach (var unit in units)
            {
                if (DistanceSquared(unit.Position, goal) < reached && unit.Order == UnitOrder.Idle) continue;
                if (strategicGoals.TryGetValue(unit.Id, out var prior) && prior == goal && unit.Order == UnitOrder.Moving) continue;
                ids.Add(unit.Id);
            }
            if (ids.Count == 0 || !Budget) return;
            var result = Send(new MoveCommand(PlayerId, ids.ToArray(), goal));
            if (result.Accepted) { foreach (int id in ids) strategicGoals[id] = goal; Status = status; }
        }

        private void Scout(UnitState unit)
        {
            if (!Budget || unit.Order == UnitOrder.Moving) return;
            int cell = world.Map.CellSizeMillimetres, stride = 8;
            SimPoint? best = null; long score = long.MaxValue;
            var opposite = new SimPoint(world.Map.WidthCells * cell - home.X, world.Map.HeightCells * cell - home.Z);
            for (int z = 4; z < world.Map.HeightCells; z += stride)
            for (int x = 4; x < world.Map.WidthCells; x += stride)
            {
                var point = Frame(new SimPoint(x * cell + cell / 2, z * cell + cell / 2));
                if (publicTerrain.Contains(new GridCell(point.X / cell, point.Z / cell)) || failedScoutGoals.Contains(point) || world.Vision.IsExplored(PlayerId, point)) continue;
                long distance = DistanceSquared(unit.Position, point) + DistanceSquared(point, opposite) / 3;
                if (distance < score) { score = distance; best = point; }
            }
            if (!best.HasValue) return;
            if (Send(new MoveCommand(PlayerId, new[] { unit.Id }, best.Value)).Accepted)
            { strategicGoals[unit.Id] = best.Value; Statistics.ScoutOrders++; Status = "Scouting unexplored terrain"; }
            else failedScoutGoals.Add(best.Value);
        }

        private SimPoint NearKnownPoint(SimPoint point, SimPoint from)
        {
            int cell = world.Map.CellSizeMillimetres;
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 }, dz = { 0, 1, 1, 1, 0, -1, -1, -1 };
            SimPoint best = from; long nearest = long.MaxValue; var local = Frame(point);
            for (int ring = 4; ring <= 8; ring += 2) for (int direction = 0; direction < 8; direction++)
            {
                var candidate = Frame(new SimPoint((local.X / cell + dx[direction] * ring) * cell + cell / 2,
                    (local.Z / cell + dz[direction] * ring) * cell + cell / 2));
                if (candidate.X < cell || candidate.Z < cell || candidate.X >= (world.Map.WidthCells - 1) * cell || candidate.Z >= (world.Map.HeightCells - 1) * cell ||
                    publicTerrain.Contains(new GridCell(candidate.X / cell, candidate.Z / cell))) continue;
                bool blocked = false;
                foreach (var site in Observation.OwnedBuildings)
                    if (Math.Abs((long)candidate.X - site.Position.X) < (site.WidthCells * (long)cell / 2 + cell / 2) && Math.Abs((long)candidate.Z - site.Position.Z) < (site.DepthCells * (long)cell / 2 + cell / 2)) { blocked = true; break; }
                foreach (var node in Observation.KnownResources) if (node.LastObservedAmount > 0 && DistanceSquared(candidate, node.Position) < cell * (long)cell) { blocked = true; break; }
                if (blocked) continue;
                long distance = DistanceSquared(candidate, from);
                if (distance < nearest) { nearest = distance; best = candidate; }
            }
            return best;
        }

        private SimPoint AdjacentResourcePoint(SimPoint resource, SimPoint from)
        {
            int cell = world.Map.CellSizeMillimetres;
            int dx = from.X - resource.X, dz = from.Z - resource.Z;
            if (Math.Abs(dx) >= Math.Abs(dz)) return new SimPoint(resource.X + (dx >= 0 ? cell : -cell), resource.Z);
            return new SimPoint(resource.X, resource.Z + (dz >= 0 ? cell : -cell));
        }
        // A soldier already trading blows with a visible enemy soldier within six metres.
        private bool Engaged(UnitState unit)
        {
            if (unit.AttackTargetId == 0) return false;
            foreach (var enemy in Observation.KnownEnemies)
                if (enemy.Id == unit.AttackTargetId) return enemy.Visible && !enemy.IsBuilding && DistanceSquared(unit.Position, enemy.Position) < 36000000;
            return false;
        }
        // ------------------------------------------------------------------ naval realm

        /// <summary>
        /// A dock on the nearest visible stretch of home shore. Candidate sites come from the public map (open ground with
        /// deep water beside it), nearest the Hearth first in the mirrored frame; with none in sight, someone walks to look.
        /// </summary>
        private void PlaceOnShore(BuildingDefinition definition)
        {
            dockSites ??= ShoreSites(definition);
            int tried = 0;
            // Obstructed near sites are not the only shore. Continue the deterministic scan next think,
            // otherwise six occupied footprints could prevent a dock forever despite open coast nearby.
            for (int scanned = 0; scanned < dockSites.Count && tried < 6 && Budget; scanned++)
            {
                var site = dockSites[dockSiteCursor];
                dockSiteCursor = (dockSiteCursor + 1) % dockSites.Count;
                if (!VisibleFootprint(site, definition)) continue;
                var builder = BuilderNear(site); if (builder == null) return;
                tried++;
                if (!world.ValidatePlacement(PlayerId, new[] { builder.Id }, definition.Id, site).Accepted) continue;
                if (Send(new BuildCommand(PlayerId, new[] { builder.Id }, definition.Id, site)).Accepted) Status = "Building " + definition.Id;
                return;
            }
            if (tried > 0 || dockSites.Count == 0 || world.TickIndex < nextDockScout || !Budget) return;
            nextDockScout = world.TickIndex + 400;
            UnitState scout = null;
            foreach (var unit in army) if (unit.Order == UnitOrder.Idle) { scout = unit; break; }
            scout ??= BuilderNear(dockSites[0]);
            if (scout != null && Send(new MoveCommand(PlayerId, new[] { scout.Id }, dockSites[0])).Accepted) { Statistics.ScoutOrders++; Status = "Scouting the shore"; }
        }

        private List<SimPoint> ShoreSites(BuildingDefinition definition)
        {
            int cell = world.Map.CellSizeMillimetres, width = definition.WidthCells, depth = definition.DepthCells;
            var found = new List<(long Score, SimPoint Local, SimPoint Point)>();
            var local = Frame(home);
            var blocked = PublicBlocked();
            for (int z = 0; z + depth <= world.Map.HeightCells; z++)
            for (int x = 0; x + width <= world.Map.WidthCells; x++)
            {
                bool open = true;
                for (int dz = 0; dz < depth && open; dz++) for (int dx = 0; dx < width && open; dx++) open = !blocked[(z + dz) * world.Map.WidthCells + x + dx];
                if (!open) continue;
                bool shore = false;
                for (int i = 0; i < width && !shore; i++) shore = Sailable(x + i, z - 1) || Sailable(x + i, z + depth);
                for (int i = 0; i < depth && !shore; i++) shore = Sailable(x - 1, z + i) || Sailable(x + width, z + i);
                if (!shore) continue;
                var point = new SimPoint(x * cell + width * cell / 2, z * cell + depth * cell / 2);
                var framed = Frame(point);
                found.Add((DistanceSquared(framed, local), framed, point));
            }
            found.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Local.Z != b.Local.Z ? a.Local.Z.CompareTo(b.Local.Z) : a.Local.X.CompareTo(b.Local.X));
            var sites = new List<SimPoint>();
            long reach = 45L * cell;
            foreach (var site in found) if (sites.Count < 48 && site.Score <= reach * reach) sites.Add(site.Point);
            return sites;
        }

        // The public terrain as a grid, for the whole-map shore scans: one lookup a cell instead of a hashed one.
        private bool[] publicBlocked;
        private bool[] PublicBlocked()
        {
            if (publicBlocked != null) return publicBlocked;
            publicBlocked = new bool[world.Map.WidthCells * world.Map.HeightCells];
            foreach (var cell in publicTerrain)
                if (cell.X >= 0 && cell.Z >= 0 && cell.X < world.Map.WidthCells && cell.Z < world.Map.HeightCells) publicBlocked[cell.Z * world.Map.WidthCells + cell.X] = true;
            return publicBlocked;
        }

        private bool Sailable(int x, int z)
        {
            int cell = world.Map.CellSizeMillimetres;
            return x >= 0 && z >= 0 && x < world.Map.WidthCells && z < world.Map.HeightCells && world.IsSailable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2));
        }

        // Hulls afloat or on the slips, up to the fleet and one transport more for landings, and one warship more for each
        // enemy hull seen in the last three minutes, up to the fleet again: ships are answered with ships.
        private UnitDefinition RecruitShip(BuildingState site)
        {
            int afloat = fleet.Count;
            foreach (var building in Observation.OwnedBuildings)
                foreach (var entry in building.ProductionQueue) if (unitDefinitions[entry.UnitDefinitionId].Domain == MovementDomain.Water) afloat++;
            int enemyHulls = 0;
            foreach (var enemy in Observation.KnownEnemies)
                if ((enemy.Tags & CombatTags.Naval) != 0 && world.TickIndex - enemy.LastSeenTick <= World.TickRate * 180L) enemyHulls++;
            if (afloat >= tuning.FleetTarget + 1 + Math.Min(enemyHulls, tuning.FleetTarget) || workers.Count < 6) return null;
            UnitDefinition best = null;
            foreach (var id in buildingDefinitions[site.DefinitionId].TrainableUnitIds)
            {
                var candidate = unitDefinitions[id];
                if (candidate.Domain != MovementDomain.Water || !world.ValidateUnitRecruitment(PlayerId, id).Accepted ||
                    !world.ValidateRequirements(PlayerId, candidate.RequiredEraId, candidate.RequiredTechnologyIds).Accepted) continue;
                if (best == null || Price(candidate) < Price(best)) best = candidate;
            }
            return best;
        }

        /// <summary>
        /// The fleet: hulls turn on any enemy ship in view; while a transport sails a landing party to the enemy's shore
        /// the warships escort it and cover the beach; once the fleet is assembled (or the army attacks) it sails to the
        /// enemy's shore and shells whatever stands within reach of the water there. One transport ferries a landing
        /// party to the shore nearest the enemy once the army is committed.
        /// </summary>
        private void DirectFleet()
        {
            if (fleet.Count == 0 || !Budget) return;
            Landing();
            if (!Budget) return;
            var free = new List<UnitState>();
            foreach (var ship in fleet) if (ship.Id != landingShipId && !ship.IsUnloading && ship.CargoCount == 0) free.Add(ship);
            if (free.Count == 0) return;
            bool raiding = fleet.Count >= tuning.FleetTarget || assaultCommitted;
            var centre = Centroid(free);
            ObservedEnemy target = null; long nearest = long.MaxValue;
            foreach (var enemy in Observation.KnownEnemies)
            {
                if (!enemy.Visible || failedNavalTargets.Contains(enemy.Id)) continue;
                bool hull = (enemy.Tags & CombatTags.Naval) != 0;
                if (!hull && (!raiding || !Coastal(enemy.Position))) continue;
                // An escort fights for the beach it covers, not for shore elsewhere.
                if (!hull && escortPoint.HasValue && DistanceSquared(enemy.Position, escortPoint.Value) > EscortReach * EscortReach) continue;
                long score = DistanceSquared(centre, enemy.Position) - (hull ? 4000000000L : 0);
                if (score < nearest) { nearest = score; target = enemy; }
            }
            if (target != null)
            {
                var ids = new List<int>();
                foreach (var ship in free) if (ship.AttackTargetId != target.Id) ids.Add(ship.Id);
                if (ids.Count == 0) return;
                if (Send(new AttackCommand(PlayerId, ids.ToArray(), target.Id)).Accepted) Status = "Fleet engaging";
                else failedNavalTargets.Add(target.Id);
                return;
            }
            if (escortPoint.HasValue) { MoveSquad(free, escortPoint.Value, "Fleet escorting the landing", 4000); return; }
            if (!raiding) return;
            raidPoint ??= NearestWater(new SimPoint(world.Map.WidthCells * world.Map.CellSizeMillimetres - home.X, world.Map.HeightCells * world.Map.CellSizeMillimetres - home.Z));
            if (raidPoint.HasValue) MoveSquad(free, raidPoint.Value, "Fleet raiding the enemy shore", 4000);
        }
        private readonly HashSet<int> failedNavalTargets = new HashSet<int>();
        // The water off the beach a loaded transport is sailing to, while it sails there: the escort's goal.
        private SimPoint? escortPoint;
        private const long EscortReach = 10000;

        private void Landing()
        {
            if (world.TickIndex < nextLandingOrder) return;
            UnitState ship = null;
            foreach (var hull in fleet) if (hull.Id == landingShipId) ship = hull;
            if (ship == null)
            {
                landingShipId = 0; escortPoint = null;
                if (!assaultCommitted) return;
                long nearest = long.MaxValue;
                foreach (var hull in fleet)
                    if (hull.CargoCapacity > 0 && hull.CargoCount == 0 && !hull.IsUnloading && DistanceSquared(hull.Position, home) < nearest)
                    { nearest = DistanceSquared(hull.Position, home); ship = hull; }
                if (ship == null) return;
                landingShipId = ship.Id; landingStarted = world.TickIndex;
            }
            if (ship.IsUnloading) return;
            // The landing is over (landed, refused or called off): the escort is released.
            escortPoint = null;
            int boarding = 0;
            foreach (var unit in Observation.OwnedUnits) if (unit.EmbarkShipId == ship.Id) boarding++;
            // A full hold, up to ten: a galleon lands a larger party than a sloop or a frigate.
            int wanted = Math.Min(ship.CargoCapacity, 10);
            long waited = world.TickIndex - landingStarted;
            if (ship.CargoCount > 0 && (ship.CargoCount >= wanted || boarding == 0 && waited > 600))
            {
                var landing = LandingPoint();
                if (!landing.HasValue) { landingShipId = 0; return; }
                if (Send(new DisembarkCommand(PlayerId, ship.Id, landing.Value)).Accepted)
                { Status = "Landing troops on the enemy shore"; escortPoint = NearestWater(landing.Value); }
                // Ground found taken (a building, a resource): the next shore along is tried at the next order.
                else { failedLandings.Add(landing.Value); nextLandingOrder = world.TickIndex + 20; }
                return;
            }
            if (ship.CargoCount + boarding > 0 && (ship.CargoCount + boarding >= wanted || waited > 600)) return;
            if (waited > 1200) { landingShipId = 0; return; }
            // The transport comes to the dock's water to take on the soldiers nearest it, unless they are already fighting.
            if (!harbourPoint.HasValue)
            {
                SimPoint quay = home;
                foreach (var building in Observation.OwnedBuildings) if (buildingDefinitions[building.DefinitionId].RequiresShore) { quay = building.Position; break; }
                harbourPoint = NearestWater(quay);
            }
            if (!harbourPoint.HasValue) return;
            if (DistanceSquared(ship.Position, harbourPoint.Value) > 16000000L)
            {
                MoveSquad(new List<UnitState> { ship }, harbourPoint.Value, "Transport returning to take on troops", 1000);
                nextLandingOrder = world.TickIndex + 40;
                return;
            }
            if (ship.Order != UnitOrder.Idle) return;
            var party = new List<UnitState>();
            foreach (var unit in army)
                if (!Engaged(unit) && unit.WallId == 0 && unit.RelocationStage == RelocationStage.None && !unit.IsPackedOutpost && unit.ThreadkeeperRemainingTicks == 0) party.Add(unit);
            party.Sort((a, b) => { int order = DistanceSquared(a.Position, ship.Position).CompareTo(DistanceSquared(b.Position, ship.Position)); return order != 0 ? order : a.Id.CompareTo(b.Id); });
            var ids = new List<int>();
            foreach (var unit in party) if (ids.Count < wanted - ship.CargoCount - boarding) ids.Add(unit.Id);
            if (ids.Count == 0) return;
            if (Send(new EmbarkCommand(PlayerId, ids.ToArray(), ship.Id)).Accepted) Status = "Embarking a landing party";
            // Not alongside after all: bring it right in to the quay and try again shortly.
            else { MoveSquad(new List<UnitState> { ship }, harbourPoint.Value, "Transport returning to take on troops", 500); nextLandingOrder = world.TickIndex + 100; }
        }

        /// <summary>
        /// The open shore nearest the enemy's side of the map, from the public map, skipping ground seen taken by an enemy
        /// building or a resource and landings already refused; mirrored between the seats like the other searches.
        /// </summary>
        private SimPoint? LandingPoint()
        {
            if (landingCandidates == null)
            {
                int cell = world.Map.CellSizeMillimetres;
                var target = Frame(new SimPoint(world.Map.WidthCells * cell - home.X, world.Map.HeightCells * cell - home.Z));
                var found = new List<(long Score, SimPoint Local, SimPoint Point)>();
                var blocked = PublicBlocked();
                for (int z = 0; z < world.Map.HeightCells; z++)
                for (int x = 0; x < world.Map.WidthCells; x++)
                {
                    var local = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                    var actual = Frame(local);
                    int ax = actual.X / cell, az = actual.Z / cell;
                    if (blocked[az * world.Map.WidthCells + ax] || Sailable(ax, az) ||
                        !(Sailable(ax - 1, az) || Sailable(ax + 1, az) || Sailable(ax, az - 1) || Sailable(ax, az + 1))) continue;
                    found.Add((DistanceSquared(local, target), local, actual));
                }
                found.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Local.Z != b.Local.Z ? a.Local.Z.CompareTo(b.Local.Z) : a.Local.X.CompareTo(b.Local.X));
                landingCandidates = new List<SimPoint>();
                foreach (var candidate in found) if (landingCandidates.Count < 64) landingCandidates.Add(candidate.Point);
            }
            foreach (var candidate in landingCandidates)
            {
                if (failedLandings.Contains(candidate)) continue;
                bool taken = false;
                foreach (var enemy in Observation.KnownEnemies)
                    if (enemy.IsBuilding && DistanceSquared(enemy.Position, candidate) < 9000000L) { taken = true; break; }
                foreach (var node in Observation.KnownResources)
                    if (!taken && node.LastObservedAmount > 0 && DistanceSquared(node.Position, candidate) < 2250000L) taken = true;
                if (!taken) return candidate;
            }
            return null;
        }
        private List<SimPoint> landingCandidates;
        private readonly HashSet<SimPoint> failedLandings = new HashSet<SimPoint>();
        private long nextLandingOrder;

        // Whether a ship's guns can reach this point from deep water: open sea within five cells.
        private bool Coastal(SimPoint point)
        {
            int cell = world.Map.CellSizeMillimetres, cx = point.X / cell, cz = point.Z / cell;
            for (int dz = -5; dz <= 5; dz++) for (int dx = -5; dx <= 5; dx++) if (dx * dx + dz * dz <= 26 && Sailable(cx + dx, cz + dz)) return true;
            return false;
        }

        // The deep-water cell nearest a point, from the public map and scanned in the mirrored frame so the two seats choose
        // mirror images.
        private SimPoint? NearestWater(SimPoint point)
        {
            int cell = world.Map.CellSizeMillimetres;
            SimPoint? best = null; long nearest = long.MaxValue; var target = Frame(point);
            for (int z = 0; z < world.Map.HeightCells; z++)
            for (int x = 0; x < world.Map.WidthCells; x++)
            {
                var local = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                var actual = Frame(local);
                if (!Sailable(actual.X / cell, actual.Z / cell)) continue;
                long distance = DistanceSquared(local, target);
                if (distance < nearest) { nearest = distance; best = actual; }
            }
            return best;
        }

        private static SimPoint Centroid(List<UnitState> units)
        {
            long x = 0, z = 0; foreach (var unit in units) { x += unit.Position.X; z += unit.Position.Z; }
            return new SimPoint((int)(x / units.Count), (int)(z / units.Count));
        }
        private static long DistanceSquared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }
    }
}
