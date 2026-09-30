using System;
using System.Collections.Generic;
using Emberfield.Simulation;

namespace Emberfield.Diagnostics
{
    /// <summary>
    /// Scripted player for complete recorded review matches. It acts only through World.Submit
    /// with the same paid, validated commands as the HUD, and sees opponents and resources through
    /// its own fog: what is visible now, or where it was last seen. It never reads hidden state.
    /// </summary>
    public sealed class ScriptedCommander
    {
        public const int ThinkIntervalTicks = 10;
        private const int CommandsPerThink = 8;
        private const long Minute = World.TickRate * 60L;

        public sealed class TimelineEvent
        {
            public long Tick;
            public string Text;
        }

        private sealed class Sighting
        {
            public int Id;
            public string DefinitionId;
            public SimPoint Position;
            public CombatTags Tags;
            public bool IsBuilding, Visible;
        }

        private sealed class Node
        {
            public int Id;
            public ResourceKind Kind;
            public SimPoint Position;
            public int Amount;
            public bool Visible;
        }

        private enum Stance { Mustering, Rallying, Marching }

        private readonly World world;
        private readonly int me;
        private readonly string central;
        private readonly int cell;
        private readonly Dictionary<string, UnitDefinition> unitDefinitions = new Dictionary<string, UnitDefinition>();
        private readonly Dictionary<string, BuildingDefinition> buildingDefinitions = new Dictionary<string, BuildingDefinition>();
        private readonly Dictionary<string, TechnologyDefinition> technologies = new Dictionary<string, TechnologyDefinition>();
        private readonly Dictionary<int, Sighting> sightings = new Dictionary<int, Sighting>();
        private readonly Dictionary<int, Node> nodes = new Dictionary<int, Node>();
        private readonly HashSet<GridCell> terrain = new HashSet<GridCell>();
        private readonly HashSet<int> completed = new HashSet<int>();
        private readonly HashSet<int> alive = new HashSet<int>();
        private readonly HashSet<int> rallied = new HashSet<int>();
        private readonly HashSet<string> recruitedKinds = new HashSet<string>();
        private readonly HashSet<string> finishedResearch = new HashSet<string>();
        private readonly List<UnitState> workers = new List<UnitState>();
        private readonly List<UnitState> army = new List<UnitState>();
        private readonly List<BuildingState> owned = new List<BuildingState>();
        private SimPoint home, staging, enemyHome;
        private Stance stance = Stance.Mustering;
        private string plannedResearch;
        private int plannedSite, budget, waveSize = 18, waves, recruitCursor, lastEra = 1, enemyHearthsDestroyed, sheltersLogged;
        private long nextThink, stanceSince, nextRebalance, nextPlacement, nextMarchOrder;

        public int PlayerId => me;
        /// <summary>Army size that launches the first attack; later waves grow after a retreat.</summary>
        public int FirstWaveSize { get => waveSize; set => waveSize = Math.Max(4, value); }
        /// <summary>No attack leaves before this tick, however large the army.</summary>
        public long EarliestAttackTick { get; set; } = 5 * Minute;
        public int Waves => waves;
        public string Plan { get; private set; } = "Economía: los cuatro Tenders empiezan a recolectar.";
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public string LastRejection { get; private set; } = "";
        public int Losses { get; private set; }
        public int WorkerCount => workers.Count;
        public int ArmyCount => army.Count;
        public List<TimelineEvent> Timeline { get; } = new List<TimelineEvent>();
        /// <summary>Where a recording should look: the fight, the marching army, or home.</summary>
        public SimPoint Focus { get; private set; }
        public bool InCombat { get; private set; }

        public ScriptedCommander(World world, int playerId)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (world.Match == null || world.Vision == null) throw new ArgumentException("A scripted player needs a match with fog.");
            if (!world.TryGetPlayer(playerId, out _)) throw new ArgumentException("Player does not exist.");
            me = playerId;
            central = world.Map.OfflineMatch.CentralBuildingId;
            cell = world.Map.CellSizeMillimetres;
            foreach (var item in world.Definition.Units) unitDefinitions[item.Id] = item;
            foreach (var item in world.Definition.Buildings) buildingDefinitions[item.Id] = item;
            foreach (var item in world.Definition.Technologies ?? Array.Empty<TechnologyDefinition>()) technologies[item.Id] = item;
            foreach (var blocked in world.Map.BlockedCells ?? Array.Empty<GridCell>()) terrain.Add(blocked);
            // The starting units and buildings are the setup, not events of the match.
            Observe();
            Timeline.Clear();
            foreach (var unit in workers) recruitedKinds.Add(unit.DefinitionId);
            // The authored maps are 180-degree mirrors, so a player knows the rival's start.
            enemyHome = new SimPoint(world.Map.WidthCells * cell - home.X, world.Map.HeightCells * cell - home.Z);
            staging = Walkable(Toward(home, enemyHome, 10000));
            Focus = home;
        }

        private PlayerState Player { get { world.TryGetPlayer(me, out var player); return player; } }

        public void Tick()
        {
            if (world.Match.IsFinished || world.TickIndex < nextThink) return;
            nextThink = world.TickIndex + ThinkIntervalTicks;
            budget = CommandsPerThink;
            Observe();
            Army();
            Economy();
            Construction();
            Research();
            Production();
        }

        private void Log(string text) => Timeline.Add(new TimelineEvent { Tick = world.TickIndex, Text = text });
        private string Name(string id) => unitDefinitions.TryGetValue(id, out var unit) ? unit.DisplayName : buildingDefinitions.TryGetValue(id, out var building) ? building.DisplayName : id;

        private bool Send(IGameCommand command)
        {
            budget--;
            var result = world.Submit(command);
            if (result.Accepted) Accepted++;
            else { Rejected++; LastRejection = command.GetType().Name + ": " + result.Message; }
            return result.Accepted;
        }

        private void Observe()
        {
            workers.Clear(); army.Clear(); owned.Clear();
            foreach (var item in sightings.Values) item.Visible = false;
            foreach (var item in nodes.Values) item.Visible = false;
            var present = new HashSet<int>();
            foreach (var unit in world.Units)
            {
                if (unit.OwnerId == me)
                {
                    present.Add(unit.Id);
                    if (unit.IsWorker) workers.Add(unit); else if (unit.AttackDamage > 0) army.Add(unit);
                    if (world.TickIndex > 0 && recruitedKinds.Add(unit.DefinitionId)) Log("Entra en juego: " + Name(unit.DefinitionId));
                    continue;
                }
                if (world.Vision.IsEntityVisible(me, unit.Id)) See(unit.Id, unit.DefinitionId, unit.Position, unit.Tags, false);
            }
            foreach (int id in alive) if (!present.Contains(id)) Losses++;
            alive.Clear(); alive.UnionWith(present);
            foreach (var building in world.Buildings)
            {
                if (building.OwnerId == me)
                {
                    owned.Add(building);
                    if (building.DefinitionId == central && completed.Count == 0) home = building.Position;
                    if (building.IsComplete && completed.Add(building.Id) && world.TickIndex > 0 && (building.DefinitionId != "shelter" || sheltersLogged++ == 0))
                        Log(Name(building.DefinitionId) + " terminado");
                    continue;
                }
                if (!world.Vision.IsEntityVisible(me, building.Id)) continue;
                if (!sightings.ContainsKey(building.Id) && building.DefinitionId == central) Log("Hearth enemigo a la vista");
                See(building.Id, building.DefinitionId, building.Position, building.Tags, true);
            }
            foreach (var node in world.Resources)
            {
                if (!world.Vision.IsEntityVisible(me, node.Id)) continue;
                if (!nodes.TryGetValue(node.Id, out var known)) nodes.Add(node.Id, known = new Node { Id = node.Id });
                known.Kind = node.Kind; known.Position = node.Position; known.Amount = node.RemainingAmount; known.Visible = true;
            }
            // A remembered position that is visible and empty is forgotten; hidden units are never looked up.
            var forget = new List<int>();
            foreach (var item in sightings.Values) if (!item.Visible && world.Vision.IsVisible(me, item.Position)) forget.Add(item.Id);
            foreach (int id in forget)
            {
                if (sightings[id].IsBuilding && sightings[id].DefinitionId == central) { enemyHearthsDestroyed++; Log("Hearth enemigo destruido"); }
                sightings.Remove(id);
            }
            var player = Player;
            if (player.EraTier > lastEra) { lastEra = player.EraTier; Log("Nueva era: " + player.EraId); }
            foreach (var id in player.CompletedTechnologyIds)
                if (finishedResearch.Add(id) && technologies.TryGetValue(id, out var technology) && string.IsNullOrEmpty(technology.AdvancesToEraId)) Log("Investigado: " + technology.DisplayName);
        }

        private void See(int id, string definition, SimPoint position, CombatTags tags, bool building)
        {
            if (!sightings.TryGetValue(id, out var known)) sightings.Add(id, known = new Sighting { Id = id });
            known.DefinitionId = definition; known.Position = position; known.Tags = tags; known.IsBuilding = building; known.Visible = true;
        }

        // ---------------------------------------------------------------- army

        private void Army()
        {
            InCombat = false;
            if (army.Count == 0) { Focus = home; return; }
            var centre = Centroid(army);
            Sighting threat = null; long nearest = 18000L * 18000;
            foreach (var item in sightings.Values)
            {
                if (!item.Visible || item.IsBuilding) continue;
                long distance = DistanceSquared(item.Position, home);
                if (distance < nearest) { nearest = distance; threat = item; }
            }
            if (threat != null)
            {
                var defenders = new List<int>();
                foreach (var unit in army) if (unit.AttackTargetId != threat.Id && DistanceSquared(unit.Position, home) < 30000L * 30000) defenders.Add(unit.Id);
                if (defenders.Count > 0) Attack(defenders, threat.Id);
                if (defenders.Count > 0 || stance != Stance.Marching)
                {
                    InCombat = true; Focus = threat.Position; Plan = "Defendiendo la base de " + Name(threat.DefinitionId) + ".";
                    if (stance != Stance.Marching) return;
                }
            }
            if (stance == Stance.Mustering)
            {
                Focus = army.Count >= 4 ? centre : home;
                if (army.Count >= waveSize && world.TickIndex >= EarliestAttackTick)
                {
                    stance = Stance.Rallying; stanceSince = world.TickIndex;
                    Log("Ejército formado: " + army.Count + " unidades");
                }
                else Plan = "Formando el ejército: " + army.Count + " de " + waveSize + " unidades · " + workers.Count + " trabajadores.";
                return;
            }
            if (stance == Stance.Rallying)
            {
                Focus = staging;
                int ready = 0; var late = new List<int>();
                foreach (var unit in army)
                {
                    if (DistanceSquared(unit.Position, staging) < 8000L * 8000) ready++;
                    else if (unit.Order == UnitOrder.Idle) late.Add(unit.Id);
                }
                if (late.Count > 0 && budget > 0) Send(new MoveCommand(me, late.ToArray(), staging));
                Plan = "Reuniendo " + army.Count + " unidades para el ataque.";
                if (ready * 5 >= army.Count * 4 || world.TickIndex - stanceSince > 30 * World.TickRate)
                {
                    stance = Stance.Marching; waves++; nextMarchOrder = 0;
                    Log("Ataque " + waves + ": " + army.Count + " unidades salen hacia el Hearth enemigo");
                }
                return;
            }
            if (army.Count < Math.Max(4, waveSize / 4))
            {
                stance = Stance.Mustering; waveSize += 4;
                var ids = new List<int>(); foreach (var unit in army) ids.Add(unit.Id);
                Send(new MoveCommand(me, ids.ToArray(), staging));
                Log("Retirada: sobreviven " + army.Count + " unidades; la siguiente oleada será de " + waveSize);
                return;
            }
            var target = ChooseTarget(centre);
            if (target != null)
            {
                var attackers = new List<int>();
                foreach (var unit in army)
                {
                    if (unit.AttackTargetId == target.Id) continue;
                    bool busy = unit.AttackTargetId != 0 && sightings.TryGetValue(unit.AttackTargetId, out var current) && current.Visible && !current.IsBuilding &&
                        DistanceSquared(unit.Position, current.Position) < 6000L * 6000;
                    if (!busy && DistanceSquared(unit.Position, target.Position) < 40000L * 40000) attackers.Add(unit.Id);
                }
                if (attackers.Count > 0) Attack(attackers, target.Id);
                InCombat = true; Focus = Midpoint(centre, target.Position);
                Plan = target.IsBuilding && target.DefinitionId == central ? "Asalto al Hearth enemigo con " + army.Count + " unidades."
                    : target.IsBuilding ? "Arrasando " + Name(target.DefinitionId) + " enemigo." : "Combate: " + army.Count + " unidades contra " + Name(target.DefinitionId) + ".";
            }
            else
            {
                Focus = centre;
                SimPoint objective = enemyHome;
                foreach (var item in sightings.Values) if (item.IsBuilding && item.DefinitionId == central) { objective = item.Position; break; }
                if (world.TickIndex >= nextMarchOrder && budget > 0)
                {
                    nextMarchOrder = world.TickIndex + 80;
                    var movers = new List<int>();
                    foreach (var unit in army) if (unit.Order == UnitOrder.Idle && unit.AttackTargetId == 0 && DistanceSquared(unit.Position, objective) > 7000L * 7000) movers.Add(unit.Id);
                    if (movers.Count > 0)
                    {
                        var goal = DistanceSquared(centre, objective) < 9000L * 9000 ? Unexplored(objective) : Walkable(Toward(objective, centre, 6000));
                        Send(new MoveCommand(me, movers.ToArray(), goal));
                    }
                }
                Plan = "Ataque " + waves + ": " + army.Count + " unidades marchan hacia el Hearth enemigo.";
            }
            // Reinforcements waiting at the rally point join the attack in groups.
            var reinforcements = new List<int>();
            foreach (var unit in army) if (unit.Order == UnitOrder.Idle && unit.AttackTargetId == 0 && DistanceSquared(unit.Position, staging) < 9000L * 9000) reinforcements.Add(unit.Id);
            if (reinforcements.Count >= 4 && budget > 0 && DistanceSquared(centre, staging) > 20000L * 20000) Send(new MoveCommand(me, reinforcements.ToArray(), Walkable(centre)));
        }

        private Sighting ChooseTarget(SimPoint centre)
        {
            Sighting best = null; long bestScore = long.MaxValue;
            foreach (var item in sightings.Values)
            {
                if (!item.Visible) continue;
                long distance = DistanceSquared(centre, item.Position), score;
                if (!item.IsBuilding && (item.Tags & CombatTags.Worker) == 0) { if (distance > 16000L * 16000) continue; score = distance - 4000000000L; }
                else if (item.IsBuilding && item.DefinitionId == central) score = distance - 2000000000L;
                else if (!item.IsBuilding) { if (distance > 10000L * 10000) continue; score = distance - 1000000000L; }
                else { if (distance > 14000L * 14000) continue; score = distance; }
                if (score < bestScore) { best = item; bestScore = score; }
            }
            return best;
        }

        private void Attack(List<int> ids, int target)
        {
            if (budget <= 0 || ids.Count == 0) return;
            if (Send(new AttackCommand(me, ids.ToArray(), target))) return;
            // A single blocked attacker rejects a group order; retry the nearest ones alone.
            for (int i = 0; i < ids.Count && budget > 0 && i < 4; i++) Send(new AttackCommand(me, new[] { ids[i] }, target));
        }

        // ---------------------------------------------------------------- economy

        private static bool Harvesting(UnitState worker) =>
            worker.WorkerTask == WorkerTask.MovingToResource || worker.WorkerTask == WorkerTask.Gathering || worker.WorkerTask == WorkerTask.ReturningResources;
        private static bool Building(UnitState worker) =>
            worker.WorkerTask == WorkerTask.MovingToConstruction || worker.WorkerTask == WorkerTask.Constructing;

        private double[] Shares()
        {
            var stock = Player.Resources;
            double food, wood, metal, stone;
            if (workers.Count < 7) { food = .6; wood = .4; metal = 0; stone = 0; }
            else { food = .5; wood = .25; metal = .20; stone = stock.Stone < 100 ? .05 : 0; }
            if (stock.Food < 150) food += .1;
            if (stock.Wood > 450) { wood -= .12; food += .06; metal += .06; }
            if (stock.Food > 700) { food -= .12; metal += .06; wood += .06; }
            if (stock.Metal > 400) { metal -= .12; food += .12; }
            double total = Math.Max(.01, food + wood + metal + stone);
            return new[] { food / total, wood / total, metal / total, stone / total };
        }

        private void Economy()
        {
            if (workers.Count == 0) return;
            var gatherers = new int[4];
            var perNode = new Dictionary<int, int>();
            foreach (var worker in workers)
                if (Harvesting(worker) && nodes.TryGetValue(worker.TargetResourceId, out var node))
                { gatherers[(int)node.Kind]++; perNode[node.Id] = (perNode.TryGetValue(node.Id, out int n) ? n : 0) + 1; }
            var shares = Shares();
            foreach (var worker in workers)
            {
                if (budget <= 0) return;
                if (worker.WorkerTask != WorkerTask.None || worker.Order != UnitOrder.Idle) continue;
                if (worker.CarriedAmount > 0 && Send(new ReturnCargoCommand(me, new[] { worker.Id }))) continue;
                int kind = Neediest(gatherers, shares);
                var node = Nearest(worker.Position, kind, perNode, true) ?? Nearest(worker.Position, -1, perNode, true);
                if (node != null)
                {
                    if (Send(new GatherCommand(me, new[] { worker.Id }, node.Id)))
                    { gatherers[(int)node.Kind]++; perNode[node.Id] = (perNode.TryGetValue(node.Id, out int n) ? n : 0) + 1; }
                    continue;
                }
                // Walk beside a remembered deposit, or explore near home to find one.
                var remembered = Nearest(worker.Position, kind, perNode, false) ?? Nearest(worker.Position, -1, perNode, false);
                Send(new MoveCommand(me, new[] { worker.Id }, remembered != null ? Walkable(Toward(remembered.Position, worker.Position, cell * 2)) : Unexplored(home)));
            }
            if (world.TickIndex < nextRebalance || budget <= 0 || workers.Count < 7) return;
            nextRebalance = world.TickIndex + 100;
            int over = -1, under = -1; double most = 0, least = 1.5;
            for (int k = 0; k < 4; k++)
            {
                double gap = gatherers[k] - shares[k] * workers.Count;
                // A resource no longer wanted releases its gatherers one by one.
                if (gap > (shares[k] <= 0 ? .5 : 1.5) && gap > most) { most = gap; over = k; }
                if (-gap > least && Nearest(home, k, perNode, true) != null) { least = -gap; under = k; }
            }
            if (over < 0 || under < 0) return;
            foreach (var worker in workers)
                if (Harvesting(worker) && worker.CarriedAmount == 0 && nodes.TryGetValue(worker.TargetResourceId, out var node) && (int)node.Kind == over)
                {
                    var target = Nearest(worker.Position, under, perNode, true);
                    if (target != null) Send(new GatherCommand(me, new[] { worker.Id }, target.Id));
                    return;
                }
        }

        private static int Neediest(int[] gatherers, double[] shares)
        {
            int best = 0; double score = double.MinValue;
            int total = 0; foreach (int n in gatherers) total += n;
            for (int k = 0; k < 4; k++)
            {
                if (shares[k] <= 0) continue;
                double gap = shares[k] * (total + 1) - gatherers[k];
                if (gap > score) { score = gap; best = k; }
            }
            return best;
        }

        private Node Nearest(SimPoint from, int kind, Dictionary<int, int> perNode, bool visible)
        {
            Node best = null; long score = long.MaxValue;
            foreach (var node in nodes.Values)
            {
                if (node.Amount <= 0 || node.Visible != visible || kind >= 0 && (int)node.Kind != kind) continue;
                perNode.TryGetValue(node.Id, out int crowd);
                long candidate = DistanceSquared(from, node.Position) + crowd * 16000000L + DropOffDistance(node.Position) / 2;
                if (candidate < score) { score = candidate; best = node; }
            }
            return best;
        }

        private long DropOffDistance(SimPoint point)
        {
            long nearest = long.MaxValue;
            foreach (var building in owned)
                if (building.IsOperational && buildingDefinitions[building.DefinitionId].CanDropOff) nearest = Math.Min(nearest, DistanceSquared(point, building.Position));
            return nearest == long.MaxValue ? 0 : nearest;
        }

        // ---------------------------------------------------------------- construction

        private int Count(string id) { int n = 0; foreach (var building in owned) if (building.DefinitionId == id) n++; return n; }

        private void Construction()
        {
            foreach (var site in owned)
            {
                if (site.IsComplete || budget <= 0) continue;
                int builders = 0;
                foreach (var candidate in workers) if (candidate.TargetBuildingId == site.Id && Building(candidate)) builders++;
                if (builders >= (site.DefinitionId == "shelter" || site.DefinitionId == "storeyard" ? 1 : 2)) continue;
                var builder = FreeWorker(site.Position);
                if (builder != null) Send(new ConstructCommand(me, new[] { builder.Id }, site.Id));
            }
            if (world.TickIndex < nextPlacement || budget <= 0) return;
            var player = Player;
            int free = player.PopulationCapacity - player.PopulationUsed - player.PopulationReserved;
            int halls = Count("muster_hall");
            bool shelterRising = false, siteOpen = false;
            foreach (var site in owned) if (!site.IsComplete) { siteOpen = true; if (site.DefinitionId == "shelter") shelterRising = true; }
            string wanted = null; SimPoint anchor = home;
            if (free <= 3 + halls * 2 && player.PopulationCapacity < 150 && !shelterRising) wanted = "shelter";
            else if (siteOpen) return;
            else if (halls == 0 && workers.Count >= 6) { wanted = "muster_hall"; anchor = Toward(home, enemyHome, 7000); }
            else if (Count("storeyard") == 0 && workers.Count >= 9) { wanted = "storeyard"; anchor = FarDeposit(); }
            else if (halls == 1 && workers.Count >= 12) { wanted = "muster_hall"; anchor = Toward(home, enemyHome, 7000); }
            else if (halls == 2 && workers.Count >= 16 && player.Resources.Wood >= 250 && world.TickIndex >= 6 * Minute) { wanted = "muster_hall"; anchor = Toward(home, enemyHome, 7000); }
            if (wanted == null) return;
            var definition = buildingDefinitions[wanted];
            if (!Affordable(definition.Cost)) return;
            var worker = FreeWorker(anchor);
            if (worker == null) return;
            nextPlacement = world.TickIndex + 40;
            if (Place(wanted, anchor, worker, out var point) && Send(new BuildCommand(me, new[] { worker.Id }, wanted, point)))
            { if (wanted != "shelter") Log(Name(wanted) + " en construcción"); }
            else nextPlacement = world.TickIndex + 100;
        }

        private SimPoint FarDeposit()
        {
            // The busiest non-food deposit that sits far from every drop-off.
            SimPoint best = home; int crowd = 0;
            foreach (var node in nodes.Values)
            {
                if (node.Kind == ResourceKind.Food || node.Amount <= 0 || DropOffDistance(node.Position) < 10000L * 10000) continue;
                int n = 0; foreach (var worker in workers) if (worker.TargetResourceId == node.Id) n++;
                if (n > crowd) { crowd = n; best = node.Position; }
            }
            return best;
        }

        private UnitState FreeWorker(SimPoint near)
        {
            UnitState best = null; long score = long.MaxValue;
            foreach (var worker in workers)
            {
                if (Building(worker)) continue;
                long candidate = DistanceSquared(worker.Position, near) + (worker.CarriedAmount > 0 ? 25000000L : 0);
                if (candidate < score) { score = candidate; best = worker; }
            }
            return best;
        }

        private bool Place(string id, SimPoint anchor, UnitState builder, out SimPoint point)
        {
            var definition = buildingDefinitions[id];
            int w = definition.WidthCells, d = definition.DepthCells;
            for (int ring = id == "storeyard" ? 2 : 3; ring <= 14; ring++)
            for (int step = 0; step < 16; step++)
            {
                double angle = step * Math.PI / 8 + ring * .37;
                int cx = anchor.X / cell + (int)Math.Round(Math.Cos(angle) * ring), cz = anchor.Z / cell + (int)Math.Round(Math.Sin(angle) * ring);
                int left = cx - w / 2, bottom = cz - d / 2;
                if (left < 2 || bottom < 2 || left + w > world.Map.WidthCells - 2 || bottom + d > world.Map.HeightCells - 2 || !Clear(left, bottom, w, d)) continue;
                var centre = new SimPoint(left * cell + w * cell / 2, bottom * cell + d * cell / 2);
                if (!world.ValidatePlacement(me, new[] { builder.Id }, id, centre).Accepted) continue;
                point = centre; return true;
            }
            point = default; return false;
        }

        private bool Clear(int left, int bottom, int width, int depth)
        {
            // Keep a walkable lane around every building and away from deposits.
            foreach (var building in owned)
            {
                int bl = (building.Position.X - building.WidthCells * cell / 2) / cell, bb = (building.Position.Z - building.DepthCells * cell / 2) / cell;
                if (left - 1 < bl + building.WidthCells && bl < left + width + 1 && bottom - 1 < bb + building.DepthCells && bb < bottom + depth + 1) return false;
            }
            foreach (var node in nodes.Values)
            {
                int nx = node.Position.X / cell, nz = node.Position.Z / cell;
                if (nx >= left - 2 && nx <= left + width + 1 && nz >= bottom - 2 && nz <= bottom + depth + 1) return false;
            }
            for (int z = bottom - 1; z <= bottom + depth; z++)
            for (int x = left - 1; x <= left + width; x++)
                if (terrain.Contains(new GridCell(x, z))) return false;
            return true;
        }

        // ---------------------------------------------------------------- research and production

        private bool Affordable(ResourceAmount cost)
        {
            var stock = Player.Resources; var reserve = default(ResourceAmount);
            if (plannedResearch != null) reserve = technologies[plannedResearch].Cost;
            return (long)stock.Food >= (long)cost.Food + reserve.Food && (long)stock.Wood >= (long)cost.Wood + reserve.Wood &&
                (long)stock.Metal >= (long)cost.Metal + reserve.Metal && (long)stock.Stone >= (long)cost.Stone + reserve.Stone;
        }

        private void Research()
        {
            var player = Player;
            plannedResearch = null; plannedSite = 0;
            string wanted = null;
            if (player.EraTier == 1 && workers.Count >= 12 && Count("muster_hall") >= 1 && army.Count >= 4) wanted = "advance_kingdom";
            else if (player.EraTier >= 2 && Count("muster_hall") >= 2 && army.Count >= 6)
                foreach (string id in new[] { "weapons_1", "armor_1" }) if (!player.HasTechnology(id) && Pending(id) == null) { wanted = id; break; }
            if (wanted == null || !technologies.TryGetValue(wanted, out var technology) || Pending(wanted) != null) return;
            if (!string.IsNullOrEmpty(technology.AdvancesToEraId) && HasPendingEra()) return;
            BuildingState site = null;
            foreach (var building in owned)
                if (building.IsOperational && building.DefinitionId == technology.ResearchBuildingId && building.ActiveResearch == null) { site = building; break; }
            if (site == null) return;
            plannedResearch = wanted; plannedSite = site.Id;
            if (site.ProductionQueue.Count > 0 || budget <= 0) return;
            var command = new ResearchCommand(me, site.Id, wanted);
            if (!world.ValidateResearch(command).Accepted) return;
            if (Send(command)) { Log("Investigando " + technology.DisplayName); plannedResearch = null; plannedSite = 0; }
        }

        private BuildingState Pending(string technology)
        {
            foreach (var building in owned) if (building.ActiveResearch?.TechnologyId == technology) return building;
            return null;
        }
        private bool HasPendingEra()
        {
            foreach (var building in owned)
                if (building.ActiveResearch != null && technologies.TryGetValue(building.ActiveResearch.TechnologyId, out var item) && !string.IsNullOrEmpty(item.AdvancesToEraId)) return true;
            return false;
        }

        private void Production()
        {
            var player = Player;
            int queuedWorkers = 0, seekers = 0; bool captain = false;
            foreach (var worker in workers) if (worker.DefinitionId == "treasure_seeker") seekers++;
            foreach (var unit in army) if (unit.DefinitionId == "crimson_corsair") captain = true;
            foreach (var building in owned)
                foreach (var entry in building.ProductionQueue)
                {
                    if (unitDefinitions[entry.UnitDefinitionId].IsWorker) queuedWorkers++;
                    if (entry.UnitDefinitionId == "treasure_seeker") seekers++;
                    if (entry.UnitDefinitionId == "crimson_corsair") captain = true;
                }
            int workerTarget = Count("muster_hall") >= 2 ? 20 : 16;
            foreach (var building in owned)
            {
                if (budget <= 0) return;
                if (!building.IsOperational || building.ActiveResearch != null || building.ProductionQueue.Count >= 2 || building.Id == plannedSite) continue;
                var definition = buildingDefinitions[building.DefinitionId];
                if (definition.TrainableUnitIds == null || definition.TrainableUnitIds.Length == 0) continue;
                string choice = null;
                if (building.DefinitionId == central)
                {
                    if (workers.Count + queuedWorkers < workerTarget) choice = seekers == 0 && workers.Count >= 8 && Eligible("treasure_seeker") ? "treasure_seeker" : "tender";
                    else if (!captain && Eligible("crimson_corsair")) choice = "crimson_corsair";
                }
                else
                {
                    if (!rallied.Contains(building.Id) && Send(new SetRallyCommand(me, building.Id, staging))) rallied.Add(building.Id);
                    choice = Soldier(definition);
                }
                if (choice == null) continue;
                var unit = unitDefinitions[choice];
                if (player.PopulationUsed + player.PopulationReserved + unit.PopulationCost > player.PopulationCapacity || !Affordable(unit.Cost)) continue;
                if (Send(new TrainCommand(me, building.Id, choice)))
                {
                    if (unit.IsWorker) queuedWorkers++; else recruitCursor++;
                    if (choice == "treasure_seeker") seekers++;
                    if (choice == "crimson_corsair") { captain = true; Log("Reclutando al Corsario Carmesí"); }
                }
            }
        }

        private bool Eligible(string id) =>
            unitDefinitions.TryGetValue(id, out var unit) && world.ValidateUnitRecruitment(me, id).Accepted &&
            world.ValidateRequirements(me, unit.RequiredEraId, unit.RequiredTechnologyIds).Accepted;

        private string Soldier(BuildingDefinition producer)
        {
            int cavalry = 0, ranged = 0, infantry = 0;
            foreach (var item in sightings.Values)
            {
                if (item.IsBuilding || (item.Tags & CombatTags.Worker) != 0) continue;
                if ((item.Tags & CombatTags.Cavalry) != 0) cavalry++;
                else if ((item.Tags & CombatTags.Ranged) != 0) ranged++;
                else if ((item.Tags & CombatTags.Infantry) != 0) infantry++;
            }
            // A pirate-led line, bent toward the ordinary counters of what has been seen.
            string[] order = cavalry > ranged && cavalry > infantry ? new[] { "reedguard", "gunpowder_corsair", "reedguard", "boarding_raider" }
                : infantry > ranged && infantry > cavalry ? new[] { "stringwarden", "boarding_raider", "stringwarden", "gunpowder_corsair" }
                : ranged > 0 && ranged >= infantry ? new[] { "strider", "boarding_raider", "strider", "reedguard" }
                : new[] { "boarding_raider", "stringwarden", "reedguard", "gunpowder_corsair", "boarding_raider", "stringwarden" };
            for (int i = 0; i < order.Length; i++)
            {
                string id = order[(recruitCursor + i) % order.Length];
                if (Array.IndexOf(producer.TrainableUnitIds, id) < 0 || !Eligible(id)) continue;
                if (Affordable(unitDefinitions[id].Cost)) return id;
            }
            return null;
        }

        // ---------------------------------------------------------------- geometry

        private SimPoint Unexplored(SimPoint around)
        {
            SimPoint? best = null; long score = long.MaxValue;
            for (int z = 3; z < world.Map.HeightCells - 3; z += 5)
            for (int x = 3; x < world.Map.WidthCells - 3; x += 5)
            {
                var point = new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
                if (terrain.Contains(new GridCell(x, z)) || world.Vision.IsExplored(me, point) || !world.IsWalkable(point)) continue;
                long distance = DistanceSquared(point, around);
                if (distance < score) { score = distance; best = point; }
            }
            return best ?? Walkable(around);
        }

        private SimPoint Walkable(SimPoint point)
        {
            int cx = Math.Max(1, Math.Min(world.Map.WidthCells - 2, point.X / cell)), cz = Math.Max(1, Math.Min(world.Map.HeightCells - 2, point.Z / cell));
            for (int radius = 0; radius < 12; radius++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Math.Abs(x) != radius && Math.Abs(z) != radius) continue;
                var candidate = new SimPoint((cx + x) * cell + cell / 2, (cz + z) * cell + cell / 2);
                if (world.IsWalkable(candidate)) return candidate;
            }
            return point;
        }

        private static SimPoint Toward(SimPoint from, SimPoint to, int distance)
        {
            long dx = (long)to.X - from.X, dz = (long)to.Z - from.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 1) return from;
            return new SimPoint(from.X + (int)(dx * distance / length), from.Z + (int)(dz * distance / length));
        }

        private static SimPoint Centroid(List<UnitState> units)
        {
            long x = 0, z = 0; foreach (var unit in units) { x += unit.Position.X; z += unit.Position.Z; }
            return new SimPoint((int)(x / units.Count), (int)(z / units.Count));
        }
        private static SimPoint Midpoint(SimPoint a, SimPoint b) => new SimPoint((int)(((long)a.X + b.X) / 2), (int)(((long)a.Z + b.Z) / 2));
        private static long DistanceSquared(SimPoint a, SimPoint b) { long x = (long)a.X - b.X, z = (long)a.Z - b.Z; return x * x + z * z; }
    }
}
