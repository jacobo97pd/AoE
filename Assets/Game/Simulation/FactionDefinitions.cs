using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public enum FactionKind { AvenCompact = 0, SerevinMarch = 1, MirajSultanate = 2, SkeldClans = 3,
        SolarKingdom = 4, VerdantCovenant = 5, AshenDominion = 6, DrakeforgedClans = 7,
        EnglishKingdom = 8, PirateBrotherhood = 9,
        // The naval realm's two navies. 12 stays free for the Skeleton Fleet, locked until its crews exist.
        EnglishNavy = 10, SpanishNavy = 11,
        DesertSultanate = 13, SahelConfederation = 14 }
    public enum StoreyardCharter { None = 0, Logistics = 1, Muster = 2 }
    public enum RelocationStage { None = 0, Packing = 1, Packed = 2, Deploying = 3 }

    [Serializable]
    public sealed class PlayerFactionDefinition
    {
        public int PlayerId;
        public string FactionId;
    }

    [Serializable]
    public sealed class FactionDefinition
    {
        public string Id;
        public string DisplayName;
        public string RealmId = "historical";
        public string Description;
        public string UniqueUnitId;
        public string UniqueTechnologyId;
        public CombatTags ArmorBonusTags;
        public int ArmorBonus;
        public ResourceKind BonusGatherResource;
        public int ResourceGatherBonus;
        public int HomeHealingPerSecond;
        public int MeleeLifeSteal;
        public FactionKind Kind;
        public int WorkerCarryBonus;
        public int CavalrySpeedBonusPermille;
        public ResourceAmount CharterCost;
        public int CharterTicks = 100;
        public int CharterRadiusMillimetres = 6000;
        public int LogisticsCarryBonus = 4;
        public int MusterWorkBonusPermille = 200;
        public string ReciprocalStoresTechnologyId;
        public int ReciprocalRadiusBonusMillimetres = 2000;
        public int ThreadkeeperDeployTicks = 300;
        public int ThreadkeeperCooldownTicks = 400;
        public int RelayRadiusMillimetres = 4000;
        public int PackTicks = 100;
        public int DeployTicks = 100;
        public string PreparedEncampmentsTechnologyId;
        public int PreparationReductionTicks = 40;
        public int RepositionTicks = 60;
        public int RepositionCooldownTicks = 300;
        public int RepositionSpeedBonusPermille = 500;
    }

    public sealed class SetCharterCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int BuildingId { get; }
        public StoreyardCharter Charter { get; }
        public SetCharterCommand(int playerId, int buildingId, StoreyardCharter charter)
        { PlayerId = playerId; BuildingId = buildingId; Charter = charter; }
    }

    public sealed class DeployThreadkeeperCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public int StoreyardId { get; }
        public DeployThreadkeeperCommand(int playerId, int unitId, int storeyardId)
        { PlayerId = playerId; UnitId = unitId; StoreyardId = storeyardId; }
    }

    public sealed class PackOutpostCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int BuildingId { get; }
        public PackOutpostCommand(int playerId, int buildingId)
        { PlayerId = playerId; BuildingId = buildingId; }
    }

    public sealed class DeployOutpostCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int UnitId { get; }
        public SimPoint Position { get; }
        public DeployOutpostCommand(int playerId, int unitId, SimPoint position)
        { PlayerId = playerId; UnitId = unitId; Position = position; }
    }

    public sealed class RepositionCommand : IGameCommand
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public RepositionCommand(int playerId, int[] unitIds)
        { PlayerId = playerId; UnitIds = Array.AsReadOnly(unitIds == null ? Array.Empty<int>() : (int[])unitIds.Clone()); }
    }
}
