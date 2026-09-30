using System;
using System.Collections.Generic;
using Emberfield.Simulation;

namespace Emberfield.Networking
{
    public enum NetworkCommandKind
    {
        Move = 1, Stop, Gather, ReturnCargo, Build, Construct, Train, Rally, Attack,
        Research, Charter, Threadkeeper, Pack, Deploy, Reposition, Surrender, BoardWall, LeaveWall, SetGate, BuildRun,
        Embark, Disembark
    }

    /// <summary>Wire request. Player identity deliberately comes only from the authenticated server session.</summary>
    [Serializable]
    public sealed class NetworkCommandEnvelope
    {
        public int Version;
        public long Sequence;
        public long IssuedTick;
        public string RequestId;
        public NetworkCommandKind Kind;
        public int[] UnitIds = Array.Empty<int>();
        public int EntityId;
        public int TargetId;
        public string DefinitionId;
        public int X;
        public int Z;
        public int Option;
        // BuildRun only: X, Z and 1 when turned (0 otherwise) for each stretch, in order.
        public int[] Sites = Array.Empty<int>();
    }

    public static class NetworkCommandCodec
    {
        public const int Version = 2;
        public const int MaximumCommandBytes = 16384;
        public const int MaximumSelection = 1024;
        public const int MaximumPastTicks = 1200;
        public const int MaximumFutureTicks = 40;

        public static NetworkCommandEnvelope Encode(IGameCommand command, long sequence, long issuedTick, string requestId)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var e = new NetworkCommandEnvelope { Version = Version, Sequence = sequence, IssuedTick = issuedTick, RequestId = requestId };
            if (command is MoveCommand move) { e.Kind = NetworkCommandKind.Move; e.UnitIds = Copy(move.UnitIds); Point(e, move.Destination); e.Option = (int)move.Formation; }
            else if (command is StopCommand stop) { e.Kind = NetworkCommandKind.Stop; e.UnitIds = Copy(stop.UnitIds); }
            else if (command is GatherCommand gather) { e.Kind = NetworkCommandKind.Gather; e.UnitIds = Copy(gather.WorkerIds); e.TargetId = gather.ResourceId; }
            else if (command is ReturnCargoCommand cargo) { e.Kind = NetworkCommandKind.ReturnCargo; e.UnitIds = Copy(cargo.WorkerIds); e.TargetId = cargo.DropOffBuildingId; }
            else if (command is BuildCommand build) { e.Kind = NetworkCommandKind.Build; e.UnitIds = Copy(build.WorkerIds); e.DefinitionId = build.BuildingDefinitionId; Point(e, build.Position); e.Option = build.Turned ? 1 : 0; }
            else if (command is BuildRunCommand run) { e.Kind = NetworkCommandKind.BuildRun; e.UnitIds = Copy(run.WorkerIds); e.DefinitionId = run.BuildingDefinitionId; e.Sites = Sites(run.Sites); }
            else if (command is ConstructCommand construct) { e.Kind = NetworkCommandKind.Construct; e.UnitIds = Copy(construct.WorkerIds); e.TargetId = construct.BuildingId; }
            else if (command is TrainCommand train) { e.Kind = NetworkCommandKind.Train; e.EntityId = train.BuildingId; e.DefinitionId = train.UnitDefinitionId; }
            else if (command is SetRallyCommand rally) { e.Kind = NetworkCommandKind.Rally; e.EntityId = rally.BuildingId; Point(e, rally.Destination); }
            else if (command is AttackCommand attack) { e.Kind = NetworkCommandKind.Attack; e.UnitIds = Copy(attack.UnitIds); e.TargetId = attack.TargetEntityId; }
            else if (command is ResearchCommand research) { e.Kind = NetworkCommandKind.Research; e.EntityId = research.BuildingId; e.DefinitionId = research.TechnologyId; }
            else if (command is SetCharterCommand charter) { e.Kind = NetworkCommandKind.Charter; e.EntityId = charter.BuildingId; e.Option = (int)charter.Charter; }
            else if (command is DeployThreadkeeperCommand threadkeeper) { e.Kind = NetworkCommandKind.Threadkeeper; e.EntityId = threadkeeper.UnitId; e.TargetId = threadkeeper.StoreyardId; }
            else if (command is PackOutpostCommand pack) { e.Kind = NetworkCommandKind.Pack; e.EntityId = pack.BuildingId; }
            else if (command is DeployOutpostCommand deploy) { e.Kind = NetworkCommandKind.Deploy; e.EntityId = deploy.UnitId; Point(e, deploy.Position); }
            else if (command is RepositionCommand reposition) { e.Kind = NetworkCommandKind.Reposition; e.UnitIds = Copy(reposition.UnitIds); }
            else if (command is BoardWallCommand board) { e.Kind = NetworkCommandKind.BoardWall; e.UnitIds = Copy(board.UnitIds); e.TargetId = board.WallId; e.Option = board.SiegeUnitId; }
            else if (command is LeaveWallCommand leaveWall) { e.Kind = NetworkCommandKind.LeaveWall; e.UnitIds = Copy(leaveWall.UnitIds); Point(e, leaveWall.Destination); }
            else if (command is SetGateCommand gate) { e.Kind = NetworkCommandKind.SetGate; e.EntityId = gate.BuildingId; e.Option = gate.Open ? 1 : 0; }
            else if (command is EmbarkCommand embark) { e.Kind = NetworkCommandKind.Embark; e.UnitIds = Copy(embark.UnitIds); e.TargetId = embark.ShipId; }
            else if (command is DisembarkCommand disembark) { e.Kind = NetworkCommandKind.Disembark; e.EntityId = disembark.ShipId; Point(e, disembark.Destination); }
            else if (command is SurrenderCommand) e.Kind = NetworkCommandKind.Surrender;
            else throw new ArgumentException("Unsupported command type.", nameof(command));
            if (!TryDecode(e, 1, issuedTick, out _, out string error)) throw new ArgumentException(error, nameof(command));
            return e;
        }

        public static bool TryDecode(NetworkCommandEnvelope e, int authenticatedPlayerId, long serverTick, out IGameCommand command, out string error)
        {
            command = null; error = null;
            if (e == null || e.Version != Version) return Reject("Unsupported command protocol version.", out error);
            if (authenticatedPlayerId < 1 || serverTick < 0) return Reject("Invalid authenticated match context.", out error);
            if (e.Sequence < 1 || !ValidToken(e.RequestId, 64)) return Reject("Invalid sequence or request ID.", out error);
            if (e.IssuedTick < 0 || e.IssuedTick > serverTick && e.IssuedTick - serverTick > MaximumFutureTicks ||
                serverTick > e.IssuedTick && serverTick - e.IssuedTick > MaximumPastTicks)
                return Reject("Command tick is outside the accepted window.", out error);
            if (!Enum.IsDefined(typeof(NetworkCommandKind), e.Kind)) return Reject("Unsupported command kind.", out error);
            var ids = e.UnitIds ?? Array.Empty<int>();
            if (ids.Length > MaximumSelection) return Reject("Selection exceeds the command limit.", out error);
            var seen = new HashSet<int>();
            foreach (int id in ids) if (id < 1 || !seen.Add(id)) return Reject("Selection IDs must be positive and unique.", out error);
            if (e.X < 0 || e.Z < 0 || e.X > 1000000 || e.Z > 1000000 || e.EntityId < 0 || e.TargetId < 0)
                return Reject("Invalid entity ID or position bounds.", out error);
            bool selected = e.Kind == NetworkCommandKind.Move || e.Kind == NetworkCommandKind.Stop || e.Kind == NetworkCommandKind.Gather ||
                e.Kind == NetworkCommandKind.ReturnCargo || e.Kind == NetworkCommandKind.Build || e.Kind == NetworkCommandKind.Construct ||
                e.Kind == NetworkCommandKind.Attack || e.Kind == NetworkCommandKind.Reposition || e.Kind == NetworkCommandKind.BoardWall || e.Kind == NetworkCommandKind.LeaveWall ||
                e.Kind == NetworkCommandKind.BuildRun || e.Kind == NetworkCommandKind.Embark;
            if (selected && ids.Length == 0 || !selected && ids.Length != 0) return Reject("Invalid selection for command kind.", out error);
            var sites = e.Sites ?? Array.Empty<int>();
            if (e.Kind == NetworkCommandKind.BuildRun ? sites.Length == 0 || sites.Length % 3 != 0 || sites.Length > BuildRunCommand.MaximumSites * 3 : sites.Length != 0)
                return Reject("Invalid wall run stretches.", out error);
            for (int i = 0; i < sites.Length; i += 3)
                if (sites[i] < 0 || sites[i + 1] < 0 || sites[i] > 1000000 || sites[i + 1] > 1000000 || sites[i + 2] != 0 && sites[i + 2] != 1)
                    return Reject("Invalid wall run stretches.", out error);
            bool definition = e.Kind == NetworkCommandKind.Build || e.Kind == NetworkCommandKind.Train || e.Kind == NetworkCommandKind.Research || e.Kind == NetworkCommandKind.BuildRun;
            if (definition ? !ValidToken(e.DefinitionId, 80) : !string.IsNullOrEmpty(e.DefinitionId)) return Reject("Invalid definition ID.", out error);
            bool entity = e.Kind == NetworkCommandKind.Train || e.Kind == NetworkCommandKind.Rally || e.Kind == NetworkCommandKind.Research ||
                e.Kind == NetworkCommandKind.Charter || e.Kind == NetworkCommandKind.Threadkeeper || e.Kind == NetworkCommandKind.Pack || e.Kind == NetworkCommandKind.Deploy || e.Kind == NetworkCommandKind.SetGate ||
                e.Kind == NetworkCommandKind.Disembark;
            if (entity ? e.EntityId < 1 : e.EntityId != 0) return Reject("Invalid acting entity ID.", out error);
            bool target = e.Kind == NetworkCommandKind.Gather || e.Kind == NetworkCommandKind.Construct || e.Kind == NetworkCommandKind.Attack || e.Kind == NetworkCommandKind.Threadkeeper || e.Kind == NetworkCommandKind.BoardWall ||
                e.Kind == NetworkCommandKind.Embark;
            if (target ? e.TargetId < 1 : e.Kind != NetworkCommandKind.ReturnCargo && e.TargetId != 0) return Reject("Invalid target ID.", out error);
            bool point = e.Kind == NetworkCommandKind.Move || e.Kind == NetworkCommandKind.Build || e.Kind == NetworkCommandKind.Rally || e.Kind == NetworkCommandKind.Deploy || e.Kind == NetworkCommandKind.LeaveWall ||
                e.Kind == NetworkCommandKind.Disembark;
            if (!point && (e.X != 0 || e.Z != 0)) return Reject("This command has no position.", out error);
            if (e.Kind == NetworkCommandKind.Move ? !Enum.IsDefined(typeof(MovementFormation), e.Option) :
                e.Kind == NetworkCommandKind.Charter ? !Enum.IsDefined(typeof(StoreyardCharter), e.Option) :
                e.Kind == NetworkCommandKind.BoardWall ? e.Option < 0 : e.Kind == NetworkCommandKind.SetGate || e.Kind == NetworkCommandKind.Build ? e.Option != 0 && e.Option != 1 : e.Option != 0)
                return Reject("Invalid command option.", out error);
            var position = new SimPoint(e.X, e.Z);
            switch (e.Kind)
            {
                case NetworkCommandKind.Move: command = new MoveCommand(authenticatedPlayerId, ids, position, (MovementFormation)e.Option); break;
                case NetworkCommandKind.Stop: command = new StopCommand(authenticatedPlayerId, ids); break;
                case NetworkCommandKind.Gather: command = new GatherCommand(authenticatedPlayerId, ids, e.TargetId); break;
                case NetworkCommandKind.ReturnCargo: command = new ReturnCargoCommand(authenticatedPlayerId, ids, e.TargetId); break;
                case NetworkCommandKind.Build: command = new BuildCommand(authenticatedPlayerId, ids, e.DefinitionId, position, e.Option == 1); break;
                case NetworkCommandKind.BuildRun:
                    var runSites = new BuildSite[sites.Length / 3];
                    for (int i = 0; i < runSites.Length; i++) runSites[i] = new BuildSite(new SimPoint(sites[i * 3], sites[i * 3 + 1]), sites[i * 3 + 2] == 1);
                    command = new BuildRunCommand(authenticatedPlayerId, ids, e.DefinitionId, runSites); break;
                case NetworkCommandKind.Construct: command = new ConstructCommand(authenticatedPlayerId, ids, e.TargetId); break;
                case NetworkCommandKind.Train: command = new TrainCommand(authenticatedPlayerId, e.EntityId, e.DefinitionId); break;
                case NetworkCommandKind.Rally: command = new SetRallyCommand(authenticatedPlayerId, e.EntityId, position); break;
                case NetworkCommandKind.Attack: command = new AttackCommand(authenticatedPlayerId, ids, e.TargetId); break;
                case NetworkCommandKind.Research: command = new ResearchCommand(authenticatedPlayerId, e.EntityId, e.DefinitionId); break;
                case NetworkCommandKind.Charter: command = new SetCharterCommand(authenticatedPlayerId, e.EntityId, (StoreyardCharter)e.Option); break;
                case NetworkCommandKind.Threadkeeper: command = new DeployThreadkeeperCommand(authenticatedPlayerId, e.EntityId, e.TargetId); break;
                case NetworkCommandKind.Pack: command = new PackOutpostCommand(authenticatedPlayerId, e.EntityId); break;
                case NetworkCommandKind.Deploy: command = new DeployOutpostCommand(authenticatedPlayerId, e.EntityId, position); break;
                case NetworkCommandKind.Reposition: command = new RepositionCommand(authenticatedPlayerId, ids); break;
                case NetworkCommandKind.BoardWall: command = new BoardWallCommand(authenticatedPlayerId, ids, e.TargetId, e.Option); break;
                case NetworkCommandKind.LeaveWall: command = new LeaveWallCommand(authenticatedPlayerId, ids, position); break;
                case NetworkCommandKind.SetGate: command = new SetGateCommand(authenticatedPlayerId, e.EntityId, e.Option == 1); break;
                case NetworkCommandKind.Embark: command = new EmbarkCommand(authenticatedPlayerId, ids, e.TargetId); break;
                case NetworkCommandKind.Disembark: command = new DisembarkCommand(authenticatedPlayerId, e.EntityId, position); break;
                case NetworkCommandKind.Surrender: command = new SurrenderCommand(authenticatedPlayerId); break;
            }
            return true;
        }

        /// <summary>Do not let guessed actor IDs distinguish a hidden enemy from a nonexistent entity through rule rejection text.</summary>
        public static bool TryAuthorizeObservation(World world, IGameCommand command, out string error)
        {
            error = null;
            if (world == null || command == null || world.IsNetworkReplica || world.Match == null || world.Vision == null || !world.Match.IsParticipant(command.PlayerId))
                return Reject("Invalid authoritative match context.", out error);
            int player = command.PlayerId;
            IReadOnlyList<int> actors = null;
            if (command is MoveCommand move) actors = move.UnitIds;
            else if (command is StopCommand stop) actors = stop.UnitIds;
            else if (command is AttackCommand attack) actors = attack.UnitIds;
            else if (command is RepositionCommand reposition) actors = reposition.UnitIds;
            else if (command is BoardWallCommand board) actors = board.UnitIds;
            else if (command is LeaveWallCommand leaveWall) actors = leaveWall.UnitIds;
            else if (command is EmbarkCommand embarking) actors = embarking.UnitIds;
            else if (command is GatherCommand gather) actors = gather.WorkerIds;
            else if (command is ReturnCargoCommand cargo) actors = cargo.WorkerIds;
            else if (command is BuildCommand build) actors = build.WorkerIds;
            else if (command is BuildRunCommand run) actors = run.WorkerIds;
            else if (command is ConstructCommand construct) actors = construct.WorkerIds;
            if (actors != null)
                foreach (int id in actors) if (!OwnUnit(world, player, id)) return Reject("An owned command entity is unavailable.", out error);
            int building = 0;
            if (command is TrainCommand train) building = train.BuildingId;
            else if (command is SetRallyCommand rally) building = rally.BuildingId;
            else if (command is ResearchCommand research) building = research.BuildingId;
            else if (command is SetCharterCommand charter) building = charter.BuildingId;
            else if (command is PackOutpostCommand pack) building = pack.BuildingId;
            else if (command is SetGateCommand gate) building = gate.BuildingId;
            else if (command is ConstructCommand constructing) building = constructing.BuildingId;
            else if (command is ReturnCargoCommand returning) building = returning.DropOffBuildingId;
            else if (command is DeployThreadkeeperCommand relay) building = relay.StoreyardId;
            if (building != 0 && !OwnBuilding(world, player, building)) return Reject("An owned command entity is unavailable.", out error);
            int unit = command is DeployThreadkeeperCommand threadkeeper ? threadkeeper.UnitId : command is DeployOutpostCommand deploy ? deploy.UnitId : command is BoardWallCommand boarding ? boarding.SiegeUnitId :
                command is EmbarkCommand embark ? embark.ShipId : command is DisembarkCommand disembark ? disembark.ShipId : 0;
            if (unit != 0 && !OwnUnit(world, player, unit)) return Reject("An owned command entity is unavailable.", out error);
            int target = command is AttackCommand attacking ? attacking.TargetEntityId : command is GatherCommand gathering ? gathering.ResourceId : command is BoardWallCommand wall ? wall.WallId : 0;
            if (target != 0 && !world.Vision.IsEntityVisible(player, target)) return Reject("The command target is not currently visible.", out error);
            return true;
        }

        private static bool OwnUnit(World world, int player, int id) => world.TryGetUnit(id, out var unit) && unit.OwnerId == player;
        private static bool OwnBuilding(World world, int player, int id) => world.TryGetBuilding(id, out var building) && building.OwnerId == player;
        private static bool ValidToken(string value, int maximum)
        {
            if (string.IsNullOrEmpty(value) || value.Length > maximum) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            return true;
        }
        private static bool Reject(string reason, out string error) { error = reason; return false; }
        private static void Point(NetworkCommandEnvelope envelope, SimPoint point) { envelope.X = point.X; envelope.Z = point.Z; }
        private static int[] Copy(IReadOnlyList<int> source) { var result = new int[source.Count]; for (int i = 0; i < result.Length; i++) result[i] = source[i]; return result; }
        private static int[] Sites(IReadOnlyList<BuildSite> sites)
        {
            var result = new int[sites.Count * 3];
            for (int i = 0; i < sites.Count; i++) { result[i * 3] = sites[i].Position.X; result[i * 3 + 1] = sites[i].Position.Z; result[i * 3 + 2] = sites[i].Turned ? 1 : 0; }
            return result;
        }
    }

    /// <summary>Retain one cursor per authenticated match seat across reconnections. Strictly ordered, at most once.</summary>
    public sealed class NetworkCommandCursor
    {
        public long LastSequence { get; private set; }
        private readonly HashSet<string> recentRequests = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> requestOrder = new Queue<string>();
        public bool TryAccept(NetworkCommandEnvelope envelope, int authenticatedPlayerId, long serverTick, out IGameCommand command, out string error)
        {
            if (!NetworkCommandCodec.TryDecode(envelope, authenticatedPlayerId, serverTick, out command, out error)) return false;
            if (LastSequence == long.MaxValue || envelope.Sequence != LastSequence + 1 || recentRequests.Contains(envelope.RequestId))
            { command = null; error = "Repeated or out-of-order command."; return false; }
            LastSequence = envelope.Sequence;
            recentRequests.Add(envelope.RequestId); requestOrder.Enqueue(envelope.RequestId);
            if (requestOrder.Count > 2048) recentRequests.Remove(requestOrder.Dequeue());
            return true;
        }
    }
}
