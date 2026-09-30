using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    [Serializable]
    public struct ResourceAmount : IEquatable<ResourceAmount>
    {
        public int Food;
        public int Wood;
        public int Metal;
        public int Stone;
        public ResourceAmount(int food, int wood, int metal, int stone)
        { Food = food; Wood = wood; Metal = metal; Stone = stone; }
        public int Get(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Food: return Food;
                case ResourceKind.Wood: return Wood;
                case ResourceKind.Metal: return Metal;
                case ResourceKind.Stone: return Stone;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        internal bool IsNonnegative => Food >= 0 && Wood >= 0 && Metal >= 0 && Stone >= 0;
        internal bool Covers(ResourceAmount cost) => cost.IsNonnegative && Food >= cost.Food && Wood >= cost.Wood && Metal >= cost.Metal && Stone >= cost.Stone;
        internal void Add(ResourceKind kind, int amount)
        {
            switch (kind)
            {
                case ResourceKind.Food: Food = checked(Food + amount); break;
                case ResourceKind.Wood: Wood = checked(Wood + amount); break;
                case ResourceKind.Metal: Metal = checked(Metal + amount); break;
                case ResourceKind.Stone: Stone = checked(Stone + amount); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public bool Equals(ResourceAmount other) => Food == other.Food && Wood == other.Wood && Metal == other.Metal && Stone == other.Stone;
        public override bool Equals(object obj) => obj is ResourceAmount other && Equals(other);
        public override int GetHashCode() { unchecked { return (((Food * 397) ^ Wood) * 397 ^ Metal) * 397 ^ Stone; } }
    }

    public sealed class PlayerState
    {
        private ResourceAmount resources;
        public int Id { get; }
        public string FactionId { get; }
        public ResourceAmount Resources => resources;
        public int PopulationUsed { get; internal set; }
        public int PopulationReserved { get; internal set; }
        public int PopulationCapacity { get; internal set; }
        public string EraId { get; internal set; }
        public int EraTier { get; internal set; } = 1;
        public IReadOnlyList<string> CompletedTechnologyIds { get; }
        private readonly List<string> completedTechnologies = new List<string>();
        private readonly HashSet<string> completedTechnologySet = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> PendingTechnologyIds = new HashSet<string>(StringComparer.Ordinal);
        internal bool PendingEraAdvance;
        /// <summary>The faction FactionId names, resolved when the world creates the player.</summary>
        internal FactionDefinition Faction;
        internal PlayerState(int id, ResourceAmount initial, int capacity, string factionId = null)
        {
            Id = id; FactionId = factionId; resources = initial; PopulationCapacity = capacity;
            CompletedTechnologyIds = completedTechnologies.AsReadOnly();
        }
        public bool HasTechnology(string id) => id != null && completedTechnologySet.Contains(id);
        internal void CompleteTechnology(string id)
        {
            if (!completedTechnologySet.Add(id)) return;
            completedTechnologies.Add(id);
            completedTechnologies.Sort(StringComparer.Ordinal);
        }
        internal bool CanAfford(ResourceAmount cost) => resources.Covers(cost);
        internal bool TrySpend(ResourceAmount cost)
        {
            if (!CanAfford(cost)) return false;
            resources.Food -= cost.Food; resources.Wood -= cost.Wood;
            resources.Metal -= cost.Metal; resources.Stone -= cost.Stone;
            return true;
        }
        internal int Deposit(ResourceKind kind, int amount)
        {
            int credited = Math.Min(amount, int.MaxValue - resources.Get(kind));
            resources.Add(kind, credited);
            return credited;
        }
    }
}
