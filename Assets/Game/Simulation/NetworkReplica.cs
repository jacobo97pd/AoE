using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public static class NetworkCommandResult
    {
        /// <summary>Transport queued the request; this does not imply server acceptance or mutate any gameplay state.</summary>
        public static CommandResult Queued() => CommandResult.Success();
        public static CommandResult Unavailable(string message) => CommandResult.Reject(CommandRejection.InvalidCommand, message);
    }

    public sealed partial class World
    {
        public bool IsNetworkReplica { get; private set; }
        public long NetworkSnapshotSequence { get; private set; }
        public int NetworkServerPlayerId { get; private set; }
        public Func<IGameCommand, CommandResult> NetworkCommandSink { get; set; }

        /// <summary>Bootstrap from the installed public map, scrub its starts before returning, then display only the received observation.</summary>
        public static World CreateNetworkReplica(GameDefinition definition, MapDefinition publicMap, NetworkSnapshot snapshot)
        {
            if (snapshot == null || snapshot.LocalPlayer == null || snapshot.Match == null || publicMap == null ||
                !ContentRealms.IsValidRealm(snapshot.RealmId) || snapshot.BiomeId != publicMap.BiomeId ||
                publicMap.OfflineMatch == null || !publicMap.OfflineMatch.Enabled || snapshot.ServerPlayerId < 1)
                throw new ArgumentException("A replica requires an observation and the installed match map.");
            var original = publicMap.OfflineMatch;
            if (original.PlayerIds == null || original.PlayerIds.Length != 2 ||
                snapshot.ServerPlayerId != original.PlayerIds[0] && snapshot.ServerPlayerId != original.PlayerIds[1])
                throw new ArgumentException("Snapshot seat does not belong to the installed map.");
            var map = new MapDefinition { Id = publicMap.Id, RealmId = snapshot.RealmId, BiomeId = snapshot.BiomeId, WidthCells = publicMap.WidthCells, HeightCells = publicMap.HeightCells,
                CellSizeMillimetres = publicMap.CellSizeMillimetres, BlockedCells = (GridCell[])publicMap.BlockedCells.Clone(),
                // Public scenery: the replica dresses its water, landmarks and lands exactly as the installed map does offline.
                WaterCells = (GridCell[])(publicMap.WaterCells ?? Array.Empty<GridCell>()).Clone(),
                Landmarks = (MapLandmarkDefinition[])(publicMap.Landmarks ?? Array.Empty<MapLandmarkDefinition>()).Clone(), ZoneRuns = publicMap.ZoneRuns,
                ResourceSpawns = (ResourceSpawnDefinition[])publicMap.ResourceSpawns.Clone(),
                UnitSpawns = new UnitSpawnDefinition[publicMap.UnitSpawns.Length], BuildingSpawns = new BuildingSpawnDefinition[publicMap.BuildingSpawns.Length],
                PlayerFactions = string.IsNullOrEmpty(snapshot.LocalPlayer.FactionId) && string.IsNullOrEmpty(snapshot.OpponentFactionId) ? Array.Empty<PlayerFactionDefinition>() :
                    new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = snapshot.LocalPlayer.FactionId }, new PlayerFactionDefinition { PlayerId = 2, FactionId = snapshot.OpponentFactionId } },
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, Mode = snapshot.Match.Mode, PlayerIds = new[] { 1, 2 },
                    VisionUnitCells = original.VisionUnitCells, VisionBuildingCells = original.VisionBuildingCells, CentralBuildingId = original.CentralBuildingId,
                    Objectives = original.Objectives, ObjectivesRequired = original.ObjectivesRequired, CaptureTicks = original.CaptureTicks,
                    ContinuousHoldTicks = original.ContinuousHoldTicks } };
            for (int i = 0; i < map.UnitSpawns.Length; i++)
            {
                var s = publicMap.UnitSpawns[i]; map.UnitSpawns[i] = new UnitSpawnDefinition { Id = s.Id,
                    OwnerId = s.OwnerId == snapshot.ServerPlayerId ? 1 : 2, DefinitionId = s.DefinitionId, Position = s.Position };
            }
            for (int i = 0; i < map.BuildingSpawns.Length; i++)
            {
                var s = publicMap.BuildingSpawns[i]; map.BuildingSpawns[i] = new BuildingSpawnDefinition { Id = s.Id,
                    OwnerId = s.OwnerId == snapshot.ServerPlayerId ? 1 : 2, DefinitionId = s.DefinitionId, Position = s.Position, Turned = s.Turned };
            }
            ContentRealms.PrepareStartingUnits(map);
            var result = new World(definition, map) { IsNetworkReplica = true, NetworkServerPlayerId = snapshot.ServerPlayerId };
            result.ApplyNetworkSnapshot(snapshot);
            // Starts are public authored data, but keeping them on the replica would invite accidental entity reads outside its observation.
            map.UnitSpawns = Array.Empty<UnitSpawnDefinition>(); map.BuildingSpawns = Array.Empty<BuildingSpawnDefinition>();
            map.ResourceSpawns = Array.Empty<ResourceSpawnDefinition>();
            return result;
        }

        /// <summary>Atomically validates and replaces the visible replica. Never recomputes vision, economy, combat, research, or victory.</summary>
        public void ApplyNetworkSnapshot(NetworkSnapshot snapshot)
        {
            if (!IsNetworkReplica) throw new InvalidOperationException("Authoritative worlds cannot receive presentation snapshots.");
            if (snapshot == null || snapshot.Version != NetworkSnapshot.CurrentVersion || snapshot.Sequence <= NetworkSnapshotSequence || snapshot.Tick < TickIndex ||
                snapshot.MapId != Map.Id || snapshot.RealmId != Map.RealmId || snapshot.BiomeId != Map.BiomeId || snapshot.ServerPlayerId != NetworkServerPlayerId || snapshot.WidthCells != mapWidth || snapshot.HeightCells != mapHeight ||
                snapshot.CellSizeMillimetres != cellSize || snapshot.Match == null || snapshot.Match.Mode != Match.Mode || snapshot.LocalPlayer == null)
                throw new ArgumentException("Invalid, stale, or incompatible snapshot header.");
            CheckArray(snapshot.Units, 8192); CheckArray(snapshot.Buildings, 8192); CheckArray(snapshot.Resources, 8192); CheckArray(snapshot.Projectiles, 8192);
            if ((long)snapshot.Units.Length + snapshot.Buildings.Length + snapshot.Resources.Length + snapshot.Projectiles.Length > 16384)
                throw new ArgumentException("Snapshot entity limit exceeded.");
            int maskLength = (mapWidth * mapHeight + 3) / 4;
            if (snapshot.Vision == null || snapshot.Vision.Length != (maskLength + 2) / 3 * 4) throw new ArgumentException("Invalid vision payload length.");
            byte[] mask;
            try { mask = Convert.FromBase64String(snapshot.Vision); } catch (FormatException) { throw new ArgumentException("Invalid vision payload encoding."); }
            if (mask.Length != maskLength) throw new ArgumentException("Invalid decoded vision length.");
            for (int i = 0; i < mapWidth * mapHeight; i++) if ((mask[i / 4] >> (i % 4 * 2) & 3) == 2) throw new ArgumentException("Visible cells must also be explored.");
            ValidateReplicaMatch(snapshot.Match);
            var local = snapshot.LocalPlayer;
            if (!SameOptionalToken(local.FactionId, Player(1).FactionId) || !SameOptionalToken(snapshot.OpponentFactionId, Player(2).FactionId) || !local.Resources.IsNonnegative ||
                local.PopulationUsed < 0 || local.PopulationReserved < 0 || local.PopulationCapacity < 0 || local.EraTier < 1 || local.EraTier > 4)
                throw new ArgumentException("Invalid local player state.");
            if (technologyCatalog.Eras.Count != 0 && (local.EraId == null || !technologyCatalog.Eras.TryGetValue(local.EraId, out var era) || era.Tier != local.EraTier))
                throw new ArgumentException("Invalid player era.");
            CheckArray(local.CompletedTechnologies, technologyCatalog.Technologies.Count); CheckArray(local.PendingTechnologies, technologyCatalog.Technologies.Count);
            var newLocal = new PlayerState(1, local.Resources, local.PopulationCapacity, OptionalToken(local.FactionId))
            { PopulationUsed = local.PopulationUsed, PopulationReserved = local.PopulationReserved, EraId = local.EraId, EraTier = local.EraTier, PendingEraAdvance = local.PendingEraAdvance };
            var techIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in local.CompletedTechnologies)
            {
                if (id == null || !technologyCatalog.Technologies.ContainsKey(id) || !techIds.Add(id)) throw new ArgumentException("Invalid completed technology.");
                newLocal.CompleteTechnology(id);
            }
            foreach (string id in local.PendingTechnologies)
            {
                if (id == null || !technologyCatalog.Technologies.ContainsKey(id) || !techIds.Add(id)) throw new ArgumentException("Invalid pending technology.");
                newLocal.PendingTechnologyIds.Add(id);
            }
            var nextUnits = new List<UnitState>(snapshot.Units.Length);
            var nextBuildings = new List<BuildingState>(snapshot.Buildings.Length);
            var nextResources = new List<ResourceNodeState>(snapshot.Resources.Length);
            var nextProjectiles = new List<ProjectileState>(snapshot.Projectiles.Length);
            var ids = new HashSet<int>();
            foreach (var u in snapshot.Units)
            {
                if (u == null || u.DefinitionId == null || !unitDefinitions.TryGetValue(u.DefinitionId, out var d)) throw new ArgumentException("Unknown snapshot unit.");
                ValidateReplicaId(u.Id, u.OwnerId, ids); ValidateReplicaPosition(u.Position); ValidateReplicaPosition(u.PreviousPosition);
                if (u.WallId < 0 || u.ElevationMillimetres < 0 || u.ElevationMillimetres > 20000 || u.BoardingWallId < 0 || u.BoardingRemainingTicks < 0 ||
                    u.Health < 1 || u.Health > d.MaxHealth || u.Armor < 0 || u.AttackDamage < 0 || u.GatherAmount < 0 || u.CarryCapacity < 0 ||
                    u.MoveSpeedMillimetresPerSecond < 0 || u.CarriedAmount < 0 ||
                    u.ThreadkeeperRemainingTicks < 0 || u.ThreadkeeperCooldownTicks < 0 || u.RepositionRemainingTicks < 0 || u.RepositionCooldownTicks < 0 ||
                    u.AttackCooldownTicks < 0 || u.MovementBlockedTicks < 0 || u.RelocationRemainingTicks < 0 || u.RelocationTotalTicks < 0 ||
                    u.RelocationRemainingTicks > u.RelocationTotalTicks || u.RelocationStage == RelocationStage.Deploying && u.RelocationTotalTicks < 1 ||
                    !Enum.IsDefined(typeof(UnitOrder), u.Order) || !Enum.IsDefined(typeof(WorkerTask), u.WorkerTask) ||
                    !Enum.IsDefined(typeof(ResourceKind), u.CarriedKind) || !Enum.IsDefined(typeof(RelocationStage), u.RelocationStage) ||
                    !Enum.IsDefined(typeof(MovementFormation), u.Formation) || u.EmbarkShipId < 0 || u.EmbarkRemainingTicks < 0 ||
                    u.EmbarkRemainingTicks > NavalSystem.BoardTicks || u.IsUnloading && d.CargoCapacity == 0) throw new ArgumentException("Invalid snapshot unit state.");
                var cargoIds = u.CargoIds ?? Array.Empty<int>(); var cargoDefinitions = u.CargoDefinitionIds ?? Array.Empty<string>();
                var cargoHealths = u.CargoHealths ?? Array.Empty<int>();
                if (cargoIds.Length != cargoDefinitions.Length || cargoIds.Length != cargoHealths.Length || cargoIds.Length > d.CargoCapacity || cargoIds.Length > 0 && u.OwnerId != 1)
                    throw new ArgumentException("Invalid snapshot ship manifest.");
                if (u.EmbarkShipId != 0 && (u.OwnerId != 1 || d.Domain != MovementDomain.Land || u.WallId != 0 || u.BoardingWallId != 0) ||
                    u.EmbarkShipId == 0 && u.EmbarkRemainingTicks != 0 ||
                    u.IsUnloading && (u.OwnerId != 1 || cargoIds.Length == 0) || !u.IsUnloading && u.UnloadPoint != default ||
                    d.Domain == MovementDomain.Water && (!waterNavigation.CanOccupy(u.Position, d.RadiusMillimetres) || u.WallId != 0 || u.BoardingWallId != 0))
                    throw new ArgumentException("Invalid snapshot naval order or movement domain.");
                var unit = new UnitState(new UnitSpawnDefinition { Id = u.Id, OwnerId = u.OwnerId, DefinitionId = u.DefinitionId, Position = u.Position }, d)
                {
                    PreviousPosition = NetworkSnapshotSequence > 0 && unitsById.TryGetValue(u.Id, out var previous) ? previous.Position : u.Position,
                    WallId = u.WallId, ElevationMillimetres = u.ElevationMillimetres, BoardingWallId = u.BoardingWallId, BoardingRemainingTicks = u.BoardingRemainingTicks,
                    Destination = u.Destination, Order = u.Order, Health = u.Health, Armor = u.Armor, AttackDamage = u.AttackDamage,
                    GatherAmount = u.GatherAmount, CarryCapacity = u.CarryCapacity, MoveSpeedMillimetresPerSecond = u.MoveSpeedMillimetresPerSecond,
                    CharterSourceId = u.CharterSourceId, ThreadkeeperRemainingTicks = u.ThreadkeeperRemainingTicks, ThreadkeeperCooldownTicks = u.ThreadkeeperCooldownTicks,
                    LinkedStoreyardId = u.LinkedStoreyardId, RepositionRemainingTicks = u.RepositionRemainingTicks, RepositionCooldownTicks = u.RepositionCooldownTicks,
                    RelocationStage = u.RelocationStage, RelocationRemainingTicks = u.RelocationRemainingTicks, RelocationTotalTicks = u.RelocationTotalTicks,
                    DeploymentPosition = u.DeploymentPosition, AttackTargetId = u.AttackTargetId, AttackCooldownTicks = u.AttackCooldownTicks,
                    AutoAttackEnabled = u.AutoAttackEnabled, CarriedKind = u.CarriedKind, CarriedAmount = u.CarriedAmount, WorkerTask = u.WorkerTask,
                    TargetResourceId = u.TargetResourceId, TargetBuildingId = u.TargetBuildingId, MovementBlockedTicks = u.MovementBlockedTicks, Formation = u.Formation,
                    EmbarkShipId = u.EmbarkShipId, EmbarkRemainingTicks = u.EmbarkRemainingTicks, IsUnloading = u.IsUnloading, UnloadPoint = u.UnloadPoint
                };
                if (u.IsUnloading) ValidateReplicaPosition(u.UnloadPoint);
                // Own passengers remain off-map; their manifest preserves identity and health across reconnection.
                for (int i = 0; i < cargoIds.Length; i++)
                {
                    if (cargoDefinitions[i] == null || !unitDefinitions.TryGetValue(cargoDefinitions[i], out var passenger) || passenger.Domain != MovementDomain.Land ||
                        cargoHealths[i] < 1 || cargoHealths[i] > passenger.MaxHealth)
                        throw new ArgumentException("Invalid snapshot passenger.");
                    ValidateReplicaId(cargoIds[i], 1, ids);
                    (unit.cargo ??= new List<UnitState>()).Add(new UnitState(new UnitSpawnDefinition { Id = cargoIds[i], OwnerId = 1, DefinitionId = passenger.Id, Position = u.Position }, passenger) { CarrierId = u.Id, Health = cargoHealths[i] });
                }
                // Unity's inline JSON serializer can materialize a null nested object as its empty default value.
                // The explicit public definition ID identifies a transport; an empty nested shell never does.
                if (u.PackedBuilding != null && (u.PackedBuilding.Id != 0 || !string.IsNullOrEmpty(u.PackedBuilding.DefinitionId)))
                {
                    if (u.OwnerId != 1 || u.PackedBuilding.OwnerId != 1 || u.PackedBuilding.Id != u.Id ||
                        u.PackedBuilding.DefinitionId != u.PackedBuildingDefinitionId) throw new ArgumentException("Invalid packed outpost ownership.");
                    unit.PackedBuilding = ReplicaBuilding(u.PackedBuilding);
                    if (buildingDefinitions[unit.PackedBuilding.DefinitionId].TransportUnitId != u.DefinitionId) throw new ArgumentException("Invalid packed outpost definition.");
                }
                else if (!string.IsNullOrEmpty(u.PackedBuildingDefinitionId))
                {
                    if (u.OwnerId != 2 || !buildingDefinitions.TryGetValue(u.PackedBuildingDefinitionId, out var packedDefinition) || packedDefinition.TransportUnitId != u.DefinitionId)
                        throw new ArgumentException("Invalid visible transport definition.");
                    // The visible model needs its public type, not the previous hidden pack position or private outpost queue/rally state.
                    unit.PackedBuilding = new BuildingState(new BuildingSpawnDefinition { Id = u.Id, OwnerId = u.OwnerId,
                        DefinitionId = u.PackedBuildingDefinitionId, Position = u.Position }, packedDefinition) { Health = u.Health };
                }
                nextUnits.Add(unit);
            }
            foreach (var b in snapshot.Buildings)
            {
                if (b == null) throw new ArgumentException("Null snapshot building.");
                ValidateReplicaId(b.Id, b.OwnerId, ids); nextBuildings.Add(ReplicaBuilding(b));
            }
            var observedShips = new Dictionary<int, UnitState>();
            var reservations = new Dictionary<int, int>();
            foreach (var unit in nextUnits) if (unit.CargoCapacity > 0) observedShips.Add(unit.Id, unit);
            foreach (var unit in nextUnits)
                if (unit.EmbarkShipId != 0)
                {
                    if (!observedShips.TryGetValue(unit.EmbarkShipId, out var ship) || ship.OwnerId != unit.OwnerId)
                        throw new ArgumentException("Invalid snapshot boarding ship reference.");
                    reservations.TryGetValue(ship.Id, out int count);
                    reservations[ship.Id] = ++count;
                    if (count + ship.CargoCount > ship.CargoCapacity) throw new ArgumentException("Snapshot boarding reservations exceed capacity.");
                }
            ValidateUnitCollectionRestrictions(nextUnits, nextBuildings);
            var wallHeights = new Dictionary<int, int>();
            foreach (var building in nextBuildings)
            { var definition = buildingDefinitions[building.DefinitionId]; if (definition.IsWall) wallHeights.Add(building.Id, definition.WallHeightMillimetres); }
            foreach (var unit in nextUnits)
            {
                if (unit.WallId != 0 && (!wallHeights.TryGetValue(unit.WallId, out int elevation) || unit.ElevationMillimetres != elevation) ||
                    unit.BoardingWallId != 0 && !wallHeights.ContainsKey(unit.BoardingWallId)) throw new ArgumentException("Invalid observed wall reference or elevation.");
            }
            foreach (var r in snapshot.Resources)
            {
                if (r == null || r.DefinitionId == null || !resourceDefinitions.TryGetValue(r.DefinitionId, out var d) || r.RemainingAmount < 0 || r.RemainingAmount > d.InitialAmount)
                    throw new ArgumentException("Invalid snapshot resource.");
                ValidateReplicaId(r.Id, 1, ids); ValidateReplicaPosition(r.Position);
                nextResources.Add(new ResourceNodeState(new ResourceSpawnDefinition { Id = r.Id, DefinitionId = r.DefinitionId, Position = r.Position }, d) { RemainingAmount = r.RemainingAmount });
            }
            foreach (var p in snapshot.Projectiles)
            {
                if (p == null || p.RemainingLifetimeTicks < 1 || p.SourceUnitId < 0 || p.TargetEntityId < 0) throw new ArgumentException("Invalid snapshot projectile.");
                ValidateReplicaId(p.Id, p.OwnerId, ids); ValidateReplicaPosition(p.Position); ValidateReplicaPosition(p.PreviousPosition);
                // A source may be hidden or already dead. The placeholder contains only the disclosed source ID and owner, never another unit's state.
                var source = new UnitState(new UnitSpawnDefinition { Id = p.SourceUnitId, OwnerId = p.OwnerId, DefinitionId = Definition.Units[0].Id, Position = p.Position }, Definition.Units[0]);
                nextProjectiles.Add(new ProjectileState(p.Id, source, p.TargetEntityId, 0, 0, p.RemainingLifetimeTicks)
                    { Position = p.Position, PreviousPosition = p.PreviousPosition });
            }
            if (ids.Count > 16384) throw new ArgumentException("Snapshot entity limit, including cargo, exceeded.");
            // Everything below uses already-validated data; the old observation remains intact if validation above fails.
            navigation.ReplaceReplicaOccupancy(Map.BlockedCells, nextBuildings, nextResources);
            units.Clear(); unitsById.Clear(); buildings.Clear(); buildingsById.Clear(); resources.Clear(); resourcesById.Clear(); projectiles.Clear(); passengers.Clear();
            nextUnits.Sort((a, b) => a.Id.CompareTo(b.Id)); nextBuildings.Sort((a, b) => a.Id.CompareTo(b.Id)); nextResources.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var u in nextUnits) { units.Add(u); unitsById.Add(u.Id, u); foreach (var passenger in u.Cargo) passengers.Add(passenger.Id, passenger); }
            foreach (var b in nextBuildings) { buildings.Add(b); buildingsById.Add(b.Id, b); }
            foreach (var r in nextResources) { resources.Add(r); resourcesById.Add(r.Id, r); }
            projectiles.AddRange(nextProjectiles);
            players.Clear(); playersById.Clear(); players.Add(newLocal); playersById.Add(1, newLocal);
            var opponent = new PlayerState(2, default, 0, OptionalToken(snapshot.OpponentFactionId)); players.Add(opponent); playersById.Add(2, opponent);
            TickIndex = snapshot.Tick; NetworkSnapshotSequence = snapshot.Sequence;
            // Global deaths are intentionally absent: a hidden enemy death is not observable.
            DeathCount = 0; Vision.ApplyReplicaVision(mask); Match.ApplyReplicaMatch(snapshot.Match);
        }

        private BuildingState ReplicaBuilding(NetworkBuilding b)
        {
            if (b.DefinitionId == null || !buildingDefinitions.TryGetValue(b.DefinitionId, out var d) || b.Health < 1 || b.Health > d.MaxHealth ||
                b.GateOpen && !d.IsGate || b.Turned && !BuildingFootprints.CanTurn(d) || b.AttackCooldownTicks < 0 || b.OilCooldownTicks < 0 || b.BuildProgressTicks < 0 || b.BuildProgressTicks > d.BuildTicks || b.ProductionWorkPermille < 1 ||
                b.CharterRemainingTicks < 0 || b.RelocationRemainingTicks < 0 || b.RelocationTotalTicks < 0 ||
                b.RelocationRemainingTicks > b.RelocationTotalTicks || b.RelocationStage == RelocationStage.Packing && b.RelocationTotalTicks < 1 ||
                !Enum.IsDefined(typeof(StoreyardCharter), b.ActiveCharter) || !Enum.IsDefined(typeof(StoreyardCharter), b.PendingCharter) ||
                !Enum.IsDefined(typeof(RelocationStage), b.RelocationStage)) throw new ArgumentException("Invalid snapshot building state.");
            ValidateReplicaPosition(b.Position); CheckArray(b.ProductionQueue, Definition.MaxProductionQueue);
            var building = new BuildingState(new BuildingSpawnDefinition { Id = b.Id, OwnerId = b.OwnerId, DefinitionId = b.DefinitionId, Position = b.Position, Turned = b.Turned }, d)
                { Health = b.Health, BuildProgressTicks = b.BuildProgressTicks, GateOpen = b.GateOpen, AttackCooldownTicks = b.AttackCooldownTicks, OilCooldownTicks = b.OilCooldownTicks, ActiveCharter = b.ActiveCharter, PendingCharter = b.PendingCharter,
                    CharterRemainingTicks = b.CharterRemainingTicks, CharterSourceId = b.CharterSourceId, ProductionWorkPermille = b.ProductionWorkPermille,
                    RelocationStage = b.RelocationStage, RelocationRemainingTicks = b.RelocationRemainingTicks, RelocationTotalTicks = b.RelocationTotalTicks,
                    HasRallyPoint = b.HasRallyPoint, RallyPoint = b.RallyPoint };
            foreach (var q in b.ProductionQueue)
            {
                if (q == null || q.DefinitionId == null || !unitDefinitions.TryGetValue(q.DefinitionId, out var unit) || q.RemainingTicks < 0 || q.RemainingTicks > unit.TrainTicks)
                    throw new ArgumentException("Invalid snapshot training queue.");
                building.Queue.Add(new TrainingQueueEntry(unit) { RemainingTicks = q.RemainingTicks });
            }
            if (!string.IsNullOrEmpty(b.ResearchId))
            {
                if (!technologyCatalog.Technologies.TryGetValue(b.ResearchId, out var researchDefinition) || b.ResearchRemainingTicks < 0 || b.ResearchRemainingTicks > researchDefinition.ResearchTicks)
                    throw new ArgumentException("Invalid snapshot research.");
                building.ActiveResearch = new ResearchState(researchDefinition) { RemainingTicks = b.ResearchRemainingTicks };
            }
            return building;
        }

        private void ValidateReplicaMatch(NetworkMatch match)
        {
            CheckArray(match.Objectives, 3);
            if (match.Objectives.Length != Match.Objectives.Count || match.ElapsedTicks < 0 || match.LocalHoldTicks < 0 || match.OpponentHoldTicks < 0 ||
                match.WinnerId < 0 || match.WinnerId > 2 || !Enum.IsDefined(typeof(MatchEndReason), match.Reason) ||
                match.IsFinished != (match.Reason != MatchEndReason.None) || !match.IsFinished && match.WinnerId != 0)
                throw new ArgumentException("Invalid snapshot match state.");
            for (int i = 0; i < match.Objectives.Length; i++)
            {
                var o = match.Objectives[i]; var known = Match.Objectives[i];
                if (o == null || o.Id != known.Id || o.OwnerId < 0 || o.OwnerId > 2 || o.CapturingPlayerId < 0 || o.CapturingPlayerId > 2 ||
                    o.CaptureProgressTicks < 0 || o.CaptureProgressTicks > known.CaptureRequiredTicks || o.HoldTicks < 0)
                    throw new ArgumentException("Invalid public objective state.");
            }
        }
        private void ValidateReplicaPosition(SimPoint point) { if (!navigation.Contains(point)) throw new ArgumentException("Snapshot position outside map."); }
        private static void ValidateReplicaId(int id, int owner, HashSet<int> ids)
        { if (id < 1 || owner < 1 || owner > 2 || !ids.Add(id)) throw new ArgumentException("Invalid or duplicate snapshot entity ID."); }
        private static void CheckArray<T>(T[] array, int maximum)
        { if (array == null || array.Length > maximum) throw new ArgumentException("Invalid snapshot collection size."); }
        private static string OptionalToken(string token) => string.IsNullOrEmpty(token) ? null : token;
        private static bool SameOptionalToken(string first, string second) => string.Equals(OptionalToken(first), OptionalToken(second), StringComparison.Ordinal);
    }

    public sealed partial class FogOfWarSystem
    {
        internal void ApplyReplicaVision(byte[] mask)
        {
            bool changed = false;
            foreach (var entry in players)
            {
                var player = entry.Value;
                for (int i = 0; i < player.Visible.Length; i++)
                {
                    int bits = entry.Key == 1 ? mask[i / 4] >> (i % 4 * 2) & 3 : 0;
                    bool visible = bits == 3, explored = (bits & 1) != 0;
                    changed |= player.Visible[i] != visible || player.Explored[i] != explored;
                    player.Visible[i] = visible; player.Explored[i] = explored;
                }
            }
            if (changed) Revision++;
        }
    }

    public sealed partial class OfflineMatchState
    {
        internal void ApplyReplicaMatch(NetworkMatch match)
        {
            IsFinished = match.IsFinished; WinnerId = match.WinnerId; Reason = match.Reason; ElapsedTicks = match.ElapsedTicks;
            firstHold = match.LocalHoldTicks; secondHold = match.OpponentHoldTicks;
            for (int i = 0; i < objectives.Count; i++)
            {
                var source = match.Objectives[i]; var target = objectives[i];
                target.OwnerId = source.OwnerId; target.CapturingPlayerId = source.CapturingPlayerId; target.CaptureProgressTicks = source.CaptureProgressTicks;
                target.IsContested = source.IsContested; target.HoldTicks = source.HoldTicks;
            }
        }
    }

    internal sealed partial class Navigation
    {
        internal void ReplaceReplicaOccupancy(GridCell[] staticCells, List<BuildingState> buildings, List<ResourceNodeState> resources)
        {
            var next = new bool[blocked.Length];
            foreach (var cell in staticCells) next[cell.Z * width + cell.X] = true;
            foreach (var building in buildings)
            {
                long left = (long)building.Position.X - building.WidthCells * (long)cellSize / 2;
                long bottom = (long)building.Position.Z - building.DepthCells * (long)cellSize / 2;
                if (left < 0 || bottom < 0 || left % cellSize != 0 || bottom % cellSize != 0 ||
                    left / cellSize + building.WidthCells > width || bottom / cellSize + building.DepthCells > height)
                    throw new ArgumentException("Invalid observed building footprint.");
                if (building.GateOpen) continue;
                for (int z = (int)(bottom / cellSize); z < bottom / cellSize + building.DepthCells; z++)
                for (int x = (int)(left / cellSize); x < left / cellSize + building.WidthCells; x++)
                { int index = z * width + x; if (next[index]) throw new ArgumentException("Observed obstacle footprints overlap."); next[index] = true; }
            }
            foreach (var resource in resources)
            {
                if (resource.RemainingAmount == 0) continue;
                int index = CellIndex(resource.Position);
                if (next[index]) throw new ArgumentException("Observed resource overlaps an obstacle."); next[index] = true;
            }
            bool changed = false; for (int i = 0; i < next.Length && !changed; i++) changed = next[i] != blocked[i];
            if (changed) { Array.Copy(next, blocked, next.Length); BuildConnectivity(); }
        }
    }
}
