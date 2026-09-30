using System;

namespace Emberfield.Simulation
{
    [Serializable]
    public sealed class EraDefinition
    {
        public string Id;
        public string DisplayName;
        public int Tier;
    }

    public enum TechnologyEffectKind { AttackDamage = 0, Armor = 1, GatherAmount = 2 }

    [Serializable]
    public sealed class TechnologyEffect
    {
        public TechnologyEffectKind Kind;
        public CombatTags TargetTags;
        public string TargetUnitId;
        public int Amount;
    }

    [Serializable]
    public sealed class TechnologyDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string ResearchBuildingId;
        public string RequiredFactionId;
        public string RequiredEraId;
        public string[] RequiredTechnologyIds = Array.Empty<string>();
        public string[] RequiredBuildingIds = Array.Empty<string>();
        public string AdvancesToEraId;
        public ResourceAmount Cost;
        public int ResearchTicks = 100;
        public TechnologyEffect[] Effects = Array.Empty<TechnologyEffect>();
    }

    public sealed class ResearchCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int BuildingId { get; }
        public string TechnologyId { get; }
        public ResearchCommand(int playerId, int buildingId, string technologyId)
        { PlayerId = playerId; BuildingId = buildingId; TechnologyId = technologyId; }
    }

    public sealed class ResearchState
    {
        public string TechnologyId { get; }
        public int RemainingTicks { get; internal set; }
        public int TotalTicks { get; }
        internal ResearchState(TechnologyDefinition definition)
        { TechnologyId = definition.Id; RemainingTicks = TotalTicks = definition.ResearchTicks; }
    }
}
