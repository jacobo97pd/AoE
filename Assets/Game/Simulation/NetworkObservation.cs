using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    /// <summary>A recipient-specific observation, not a savegame. No hidden enemy entities or private economy are exported.</summary>
    [Serializable]
    public sealed class NetworkSnapshot
    {
        public const int CurrentVersion = 2;
        public int Version;
        public long Sequence;
        public long Tick;
        public string MapId;
        public string RealmId = "historical";
        public string BiomeId = "forest";
        public int ServerPlayerId;
        public int WidthCells;
        public int HeightCells;
        public int CellSizeMillimetres;
        public NetworkPlayer LocalPlayer;
        public string OpponentFactionId;
        public NetworkUnit[] Units = Array.Empty<NetworkUnit>();
        public NetworkBuilding[] Buildings = Array.Empty<NetworkBuilding>();
        public NetworkResource[] Resources = Array.Empty<NetworkResource>();
        public NetworkProjectile[] Projectiles = Array.Empty<NetworkProjectile>();
        public NetworkMatch Match;
        // Four cells per byte: bit pair 0=unknown, 1=explored, 3=currently visible. Base64 avoids JSON bool-array bloat.
        public string Vision;
    }

    [Serializable]
    public sealed class NetworkPlayer
    {
        public string FactionId;
        public ResourceAmount Resources;
        public int PopulationUsed, PopulationReserved, PopulationCapacity;
        public string EraId;
        public int EraTier;
        public string[] CompletedTechnologies = Array.Empty<string>();
        public string[] PendingTechnologies = Array.Empty<string>();
        public bool PendingEraAdvance;
    }

    [Serializable]
    public sealed class NetworkUnit
    {
        public int Id, OwnerId;
        public string DefinitionId;
        public SimPoint Position, PreviousPosition, Destination;
        public UnitOrder Order;
        public int Health, Armor, AttackDamage, GatherAmount, CarryCapacity, MoveSpeedMillimetresPerSecond;
        public int CharterSourceId, ThreadkeeperRemainingTicks, ThreadkeeperCooldownTicks, LinkedStoreyardId;
        public int RepositionRemainingTicks, RepositionCooldownTicks;
        public RelocationStage RelocationStage;
        public int RelocationRemainingTicks, RelocationTotalTicks;
        public SimPoint DeploymentPosition;
        public string PackedBuildingDefinitionId;
        public NetworkBuilding PackedBuilding;
        public int WallId, ElevationMillimetres, BoardingWallId, BoardingRemainingTicks;
        public int AttackTargetId, AttackCooldownTicks;
        public bool AutoAttackEnabled;
        public ResourceKind CarriedKind;
        public int CarriedAmount;
        public WorkerTask WorkerTask;
        public int TargetResourceId, TargetBuildingId, MovementBlockedTicks;
        public MovementFormation Formation;
        // Naval: the ship a soldier walks to board, and the voyage of an unloading ship (own units only). An own ship's
        // manifest names who is aboard; an enemy hull's cargo is not observable.
        public int EmbarkShipId, EmbarkRemainingTicks;
        public bool IsUnloading;
        public SimPoint UnloadPoint;
        public int[] CargoIds = Array.Empty<int>();
        public string[] CargoDefinitionIds = Array.Empty<string>();
        public int[] CargoHealths = Array.Empty<int>();
    }

    [Serializable]
    public sealed class NetworkBuilding
    {
        public int Id, OwnerId;
        public string DefinitionId;
        public SimPoint Position;
        public bool Turned;
        public int Health, BuildProgressTicks;
        public bool GateOpen;
        public int AttackCooldownTicks, OilCooldownTicks;
        public StoreyardCharter ActiveCharter, PendingCharter;
        public int CharterRemainingTicks, CharterSourceId, ProductionWorkPermille;
        public RelocationStage RelocationStage;
        public int RelocationRemainingTicks, RelocationTotalTicks;
        public bool HasRallyPoint;
        public SimPoint RallyPoint;
        public NetworkTraining[] ProductionQueue = Array.Empty<NetworkTraining>();
        public string ResearchId;
        public int ResearchRemainingTicks;
    }

    [Serializable] public sealed class NetworkTraining { public string DefinitionId; public int RemainingTicks; }
    [Serializable] public sealed class NetworkResource { public int Id; public string DefinitionId; public SimPoint Position; public int RemainingAmount; }
    [Serializable] public sealed class NetworkProjectile
    {
        public int Id, OwnerId, SourceUnitId, TargetEntityId, RemainingLifetimeTicks;
        public SimPoint Position, PreviousPosition;
    }
    [Serializable] public sealed class NetworkObjective
    {
        public string Id;
        public int OwnerId, CapturingPlayerId, CaptureProgressTicks;
        public bool IsContested;
        public long HoldTicks;
    }
    [Serializable] public sealed class NetworkMatch
    {
        public VictoryMode Mode;
        public bool IsFinished;
        public int WinnerId;
        public MatchEndReason Reason;
        public long ElapsedTicks, LocalHoldTicks, OpponentHoldTicks;
        public NetworkObjective[] Objectives = Array.Empty<NetworkObjective>();
    }

    public static class NetworkObservation
    {
        public static NetworkSnapshot Export(World world, int serverPlayerId, long sequence)
        {
            if (world == null || world.IsNetworkReplica || world.Match == null || world.Vision == null || !world.Match.IsParticipant(serverPlayerId) || sequence < 1)
                throw new ArgumentException("Export requires a participant of an authoritative match and a positive sequence.");
            world.TryGetPlayer(serverPlayerId, out var local);
            int opponentId = world.Match.PlayerIds[0] == serverPlayerId ? world.Match.PlayerIds[1] : world.Match.PlayerIds[0];
            world.TryGetPlayer(opponentId, out var opponent);
            var result = new NetworkSnapshot
            {
                Version = NetworkSnapshot.CurrentVersion, Sequence = sequence, Tick = world.TickIndex,
                RealmId = world.Map.RealmId, BiomeId = world.Map.BiomeId,
                ServerPlayerId = serverPlayerId, MapId = world.Map.Id, WidthCells = world.Map.WidthCells,
                HeightCells = world.Map.HeightCells, CellSizeMillimetres = world.Map.CellSizeMillimetres,
                OpponentFactionId = opponent.FactionId,
                LocalPlayer = new NetworkPlayer { FactionId = local.FactionId, Resources = local.Resources, PopulationUsed = local.PopulationUsed,
                    PopulationReserved = local.PopulationReserved, PopulationCapacity = local.PopulationCapacity, EraId = local.EraId,
                    EraTier = local.EraTier, CompletedTechnologies = Copy(local.CompletedTechnologyIds),
                    PendingTechnologies = Sorted(local.PendingTechnologyIds), PendingEraAdvance = local.PendingEraAdvance }
            };
            var visible = new HashSet<int>();
            foreach (var u in world.Units) if (world.Vision.IsEntityVisible(serverPlayerId, u.Id)) visible.Add(u.Id);
            foreach (var b in world.Buildings) if (world.Vision.IsEntityVisible(serverPlayerId, b.Id)) visible.Add(b.Id);
            foreach (var r in world.Resources) if (world.Vision.IsEntityVisible(serverPlayerId, r.Id)) visible.Add(r.Id);
            var units = new List<NetworkUnit>();
            foreach (var u in world.Units)
            {
                if (!visible.Contains(u.Id)) continue;
                bool own = u.OwnerId == serverPlayerId;
                var definition = world.unitDefinitions[u.DefinitionId];
                units.Add(new NetworkUnit
                {
                    Id = u.Id, OwnerId = Normalize(u.OwnerId, serverPlayerId), DefinitionId = u.DefinitionId,
                    Position = u.Position, WallId = Known(u.WallId, visible), ElevationMillimetres = u.ElevationMillimetres,
                    BoardingWallId = own ? Known(u.BoardingWallId, visible) : 0, BoardingRemainingTicks = own ? u.BoardingRemainingTicks : 0,
                    PreviousPosition = own || world.Vision.IsVisible(serverPlayerId, u.PreviousPosition) ? u.PreviousPosition : u.Position,
                    Destination = own ? u.Destination : u.Position, Order = u.Order, Health = u.Health,
                    Armor = own ? u.Armor : definition.Armor, AttackDamage = own ? u.AttackDamage : definition.Attack.Damage,
                    GatherAmount = own ? u.GatherAmount : definition.GatherAmount,
                    CarryCapacity = own ? u.CarryCapacity : definition.CarryCapacity,
                    MoveSpeedMillimetresPerSecond = own ? u.MoveSpeedMillimetresPerSecond : definition.MoveSpeedMillimetresPerSecond,
                    CharterSourceId = own ? Known(u.CharterSourceId, visible) : 0,
                    ThreadkeeperRemainingTicks = u.ThreadkeeperRemainingTicks,
                    ThreadkeeperCooldownTicks = own ? u.ThreadkeeperCooldownTicks : 0, LinkedStoreyardId = own ? Known(u.LinkedStoreyardId, visible) : 0,
                    RepositionRemainingTicks = u.RepositionRemainingTicks, RepositionCooldownTicks = own ? u.RepositionCooldownTicks : 0,
                    RelocationStage = u.RelocationStage, RelocationRemainingTicks = u.RelocationRemainingTicks,
                    RelocationTotalTicks = u.RelocationTotalTicks, DeploymentPosition = own ? u.DeploymentPosition : u.Position,
                    PackedBuildingDefinitionId = u.PackedBuildingDefinitionId,
                    PackedBuilding = own && u.PackedBuilding != null ? Building(u.PackedBuilding, serverPlayerId, visible) : null,
                    AttackTargetId = own ? Known(u.AttackTargetId, visible) : 0, AttackCooldownTicks = u.AttackCooldownTicks,
                    AutoAttackEnabled = own && u.AutoAttackEnabled, CarriedKind = u.CarriedKind,
                    CarriedAmount = own ? u.CarriedAmount : u.CarriedAmount > 0 ? 1 : 0,
                    WorkerTask = own ? u.WorkerTask : u.WorkerTask == WorkerTask.Gathering ? WorkerTask.Gathering :
                        u.WorkerTask == WorkerTask.Constructing ? WorkerTask.Constructing : WorkerTask.None,
                    TargetResourceId = own ? Known(u.TargetResourceId, visible) : 0, TargetBuildingId = own ? Known(u.TargetBuildingId, visible) : 0,
                    MovementBlockedTicks = own ? u.MovementBlockedTicks : 0, Formation = own ? u.Formation : MovementFormation.Loose,
                    EmbarkShipId = own ? Known(u.EmbarkShipId, visible) : 0, EmbarkRemainingTicks = own && u.EmbarkShipId != 0 ? u.EmbarkRemainingTicks : 0,
                    IsUnloading = own && u.IsUnloading, UnloadPoint = own && u.IsUnloading ? u.UnloadPoint : default,
                    CargoIds = own ? CargoIds(u) : Array.Empty<int>(), CargoDefinitionIds = own ? CargoDefinitions(u) : Array.Empty<string>(),
                    CargoHealths = own ? CargoHealths(u) : Array.Empty<int>()
                });
            }
            var buildings = new List<NetworkBuilding>();
            foreach (var b in world.Buildings) if (visible.Contains(b.Id)) buildings.Add(Building(b, serverPlayerId, visible));
            var resources = new List<NetworkResource>();
            foreach (var r in world.Resources) if (visible.Contains(r.Id)) resources.Add(new NetworkResource
                { Id = r.Id, DefinitionId = r.DefinitionId, Position = r.Position, RemainingAmount = r.RemainingAmount });
            var projectiles = new List<NetworkProjectile>();
            foreach (var p in world.Projectiles) if (world.Vision.IsVisible(serverPlayerId, p.Position)) projectiles.Add(new NetworkProjectile
                { Id = p.Id, OwnerId = Normalize(p.OwnerId, serverPlayerId), SourceUnitId = Known(p.SourceUnitId, visible),
                    TargetEntityId = Known(p.TargetEntityId, visible), Position = p.Position,
                    PreviousPosition = world.Vision.IsVisible(serverPlayerId, p.PreviousPosition) ? p.PreviousPosition : p.Position,
                    RemainingLifetimeTicks = p.RemainingLifetimeTicks });
            result.Units = units.ToArray(); result.Buildings = buildings.ToArray(); result.Resources = resources.ToArray(); result.Projectiles = projectiles.ToArray();
            var match = world.Match;
            result.Match = new NetworkMatch { Mode = match.Mode, IsFinished = match.IsFinished, WinnerId = Normalize(match.WinnerId, serverPlayerId),
                Reason = match.Reason, ElapsedTicks = match.ElapsedTicks, LocalHoldTicks = match.GetHoldTicks(serverPlayerId),
                OpponentHoldTicks = match.GetHoldTicks(opponentId), Objectives = new NetworkObjective[match.Objectives.Count] };
            for (int i = 0; i < result.Match.Objectives.Length; i++)
            {
                var o = match.Objectives[i]; result.Match.Objectives[i] = new NetworkObjective { Id = o.Id, OwnerId = Normalize(o.OwnerId, serverPlayerId),
                    CapturingPlayerId = Normalize(o.CapturingPlayerId, serverPlayerId), CaptureProgressTicks = o.CaptureProgressTicks,
                    IsContested = o.IsContested, HoldTicks = o.HoldTicks };
            }
            var mask = new byte[(result.WidthCells * result.HeightCells + 3) / 4];
            for (int z = 0; z < result.HeightCells; z++) for (int x = 0; x < result.WidthCells; x++)
            {
                var p = new SimPoint(x * result.CellSizeMillimetres + result.CellSizeMillimetres / 2, z * result.CellSizeMillimetres + result.CellSizeMillimetres / 2);
                int bits = world.Vision.IsVisible(serverPlayerId, p) ? 3 : world.Vision.IsExplored(serverPlayerId, p) ? 1 : 0;
                int index = z * result.WidthCells + x; mask[index / 4] |= (byte)(bits << (index % 4 * 2));
            }
            result.Vision = Convert.ToBase64String(mask);
            return result;
        }

        private static NetworkBuilding Building(BuildingState b, int player, HashSet<int> visible)
        {
            bool own = b.OwnerId == player;
            var result = new NetworkBuilding { Id = b.Id, OwnerId = Normalize(b.OwnerId, player), DefinitionId = b.DefinitionId, Position = b.Position, Turned = b.IsTurned,
                Health = b.Health, BuildProgressTicks = b.BuildProgressTicks, GateOpen = b.GateOpen, AttackCooldownTicks = b.AttackCooldownTicks, OilCooldownTicks = b.OilCooldownTicks, ActiveCharter = b.ActiveCharter,
                PendingCharter = own ? b.PendingCharter : StoreyardCharter.None, CharterRemainingTicks = own ? b.CharterRemainingTicks : 0,
                CharterSourceId = own ? Known(b.CharterSourceId, visible) : 0, ProductionWorkPermille = own ? b.ProductionWorkPermille : 1000,
                RelocationStage = b.RelocationStage, RelocationRemainingTicks = b.RelocationRemainingTicks,
                RelocationTotalTicks = b.RelocationTotalTicks, HasRallyPoint = own && b.HasRallyPoint,
                RallyPoint = own ? b.RallyPoint : b.Position, ResearchId = own ? b.ActiveResearch?.TechnologyId : null,
                ResearchRemainingTicks = own ? b.ActiveResearch?.RemainingTicks ?? 0 : 0 };
            if (own)
            {
                result.ProductionQueue = new NetworkTraining[b.ProductionQueue.Count];
                for (int i = 0; i < result.ProductionQueue.Length; i++) result.ProductionQueue[i] = new NetworkTraining
                    { DefinitionId = b.ProductionQueue[i].UnitDefinitionId, RemainingTicks = b.ProductionQueue[i].RemainingTicks };
            }
            return result;
        }
        private static int[] CargoIds(UnitState ship)
        {
            if (ship.CargoCount == 0) return Array.Empty<int>();
            var result = new int[ship.CargoCount]; for (int i = 0; i < result.Length; i++) result[i] = ship.Cargo[i].Id;
            return result;
        }
        private static string[] CargoDefinitions(UnitState ship)
        {
            if (ship.CargoCount == 0) return Array.Empty<string>();
            var result = new string[ship.CargoCount]; for (int i = 0; i < result.Length; i++) result[i] = ship.Cargo[i].DefinitionId;
            return result;
        }
        private static int[] CargoHealths(UnitState ship)
        {
            if (ship.CargoCount == 0) return Array.Empty<int>();
            var result = new int[ship.CargoCount]; for (int i = 0; i < result.Length; i++) result[i] = ship.Cargo[i].Health;
            return result;
        }
        private static int Normalize(int owner, int player) => owner == 0 ? 0 : owner == player ? 1 : 2;
        private static int Known(int id, HashSet<int> visible) => visible.Contains(id) ? id : 0;
        private static string[] Copy(IReadOnlyList<string> source) { var result = new string[source.Count]; for (int i = 0; i < result.Length; i++) result[i] = source[i]; return result; }
        private static string[] Sorted(HashSet<string> source) { var result = new string[source.Count]; source.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result; }
    }
}
