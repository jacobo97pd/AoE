using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public sealed class ObservedEnemy
    {
        public int Id { get; internal set; }
        public int OwnerId { get; internal set; }
        public string DefinitionId { get; internal set; }
        public SimPoint Position { get; internal set; }
        public CombatTags Tags { get; internal set; }
        public int Health { get; internal set; }
        public bool IsBuilding { get; internal set; }
        public long LastSeenTick { get; internal set; }
        public bool Visible { get; internal set; }
    }

    public sealed class ObservedResource
    {
        public int Id { get; internal set; }
        public SimPoint Position { get; internal set; }
        public ResourceKind Kind { get; internal set; }
        public int LastObservedAmount { get; internal set; }
        public bool Visible { get; internal set; }
        public long LastSeenTick { get; internal set; }
    }

    // The only opponent/resource reader used by OfflineAi. Hidden records retain their last observation.
    // Ownership/ID are inspected only to filter candidates; no hidden position, stock or queue is read.
    public sealed class OfflineAiObservation
    {
        private readonly List<UnitState> units = new List<UnitState>();
        private readonly List<BuildingState> buildings = new List<BuildingState>();
        private readonly List<ObservedEnemy> enemies = new List<ObservedEnemy>();
        private readonly List<ObservedResource> resources = new List<ObservedResource>();
        private readonly Dictionary<int, ObservedEnemy> enemyById = new Dictionary<int, ObservedEnemy>();
        private readonly Dictionary<int, ObservedResource> resourceById = new Dictionary<int, ObservedResource>();
        public IReadOnlyList<UnitState> OwnedUnits => units;
        public IReadOnlyList<BuildingState> OwnedBuildings => buildings;
        public IReadOnlyList<ObservedEnemy> KnownEnemies => enemies;
        public IReadOnlyList<ObservedResource> KnownResources => resources;

        internal void Refresh(World world, int player)
        {
            units.Clear(); buildings.Clear();
            foreach (var item in enemies) item.Visible = false;
            foreach (var item in resources) item.Visible = false;
            foreach (var unit in world.Units)
            {
                if (unit.OwnerId == player) { units.Add(unit); continue; }
                if (!world.Vision.IsEntityVisible(player, unit.Id)) continue;
                Observe(unit.Id, unit.OwnerId, unit.DefinitionId, unit.Position, unit.Tags, unit.Health, false, world.TickIndex);
            }
            foreach (var building in world.Buildings)
            {
                if (building.OwnerId == player) { buildings.Add(building); continue; }
                if (!world.Vision.IsEntityVisible(player, building.Id)) continue;
                Observe(building.Id, building.OwnerId, building.DefinitionId, building.Position, building.Tags, building.Health, true, world.TickIndex);
            }
            foreach (var node in world.Resources)
            {
                if (!world.Vision.IsEntityVisible(player, node.Id)) continue;
                if (!resourceById.TryGetValue(node.Id, out var known))
                {
                    known = new ObservedResource { Id = node.Id };
                    resourceById.Add(node.Id, known); resources.Add(known);
                }
                known.Position = node.Position; known.Kind = node.Kind; known.LastObservedAmount = node.RemainingAmount;
                known.Visible = true; known.LastSeenTick = world.TickIndex;
            }
            // An empty currently visible last-known position disproves that location, without looking up a hidden unit.
            for (int index = enemies.Count - 1; index >= 0; index--)
                if (!enemies[index].Visible && world.Vision.IsVisible(player, enemies[index].Position))
                { enemyById.Remove(enemies[index].Id); enemies.RemoveAt(index); }
            enemies.Sort((a, b) => a.Id.CompareTo(b.Id));
            resources.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        private void Observe(int id, int owner, string definition, SimPoint position, CombatTags tags, int health, bool building, long tick)
        {
            if (!enemyById.TryGetValue(id, out var known))
            {
                known = new ObservedEnemy { Id = id };
                enemyById.Add(id, known); enemies.Add(known);
            }
            known.OwnerId = owner; known.DefinitionId = definition; known.Position = position; known.Tags = tags;
            known.Health = health; known.IsBuilding = building; known.LastSeenTick = tick; known.Visible = true;
        }
    }
}
