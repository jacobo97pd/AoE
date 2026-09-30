using System;
using System.Collections.Generic;

namespace Emberfield.Simulation
{
    public sealed class DominionObjectiveState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public SimPoint Position { get; }
        public int RadiusMillimetres { get; }
        public int OwnerId { get; internal set; }
        public int CapturingPlayerId { get; internal set; }
        public int CaptureProgressTicks { get; internal set; }
        public int CaptureRequiredTicks { get; }
        public bool IsContested { get; internal set; }
        public long HoldTicks { get; internal set; }
        internal bool Holding;
        internal DominionObjectiveState(DominionObjectiveDefinition definition, int captureTicks)
        {
            Id = definition.Id; DisplayName = definition.DisplayName; Position = definition.Position;
            RadiusMillimetres = definition.RadiusMillimetres; CaptureRequiredTicks = captureTicks;
        }
    }

    /// <summary>Opt-in two-player result state. Objective information is public; unrelated enemy state is not.</summary>
    public sealed partial class OfflineMatchState
    {
        private readonly World world;
        private readonly string centralBuildingId;
        private readonly int firstPlayer, secondPlayer;
        private long firstHold, secondHold;
        private readonly List<DominionObjectiveState> objectives = new List<DominionObjectiveState>();
        public VictoryMode Mode { get; }
        public IReadOnlyList<int> PlayerIds { get; }
        public IReadOnlyList<DominionObjectiveState> Objectives { get; }
        public int ObjectivesRequired { get; }
        public int ContinuousHoldTicks { get; }
        public bool IsFinished { get; private set; }
        public int WinnerId { get; private set; }
        public MatchEndReason Reason { get; private set; }
        public long ElapsedTicks { get; private set; }

        internal OfflineMatchState(World world, OfflineMatchDefinition definition)
        {
            this.world = world;
            if (!Enum.IsDefined(typeof(VictoryMode), definition.Mode) || definition.PlayerIds == null || definition.PlayerIds.Length != 2 ||
                definition.PlayerIds[0] < 1 || definition.PlayerIds[1] < 1 || definition.PlayerIds[0] == definition.PlayerIds[1])
                throw new ArgumentException("An offline match requires a supported mode and two distinct positive players.");
            firstPlayer = definition.PlayerIds[0]; secondPlayer = definition.PlayerIds[1];
            if (world.Players.Count != 2 || !world.TryGetPlayer(firstPlayer, out _) || !world.TryGetPlayer(secondPlayer, out _))
                throw new ArgumentException("Every owned entity in an offline match must belong to its two participants.");
            if (string.IsNullOrWhiteSpace(definition.CentralBuildingId) || !world.buildingDefinitions.TryGetValue(definition.CentralBuildingId, out var central) ||
                !string.IsNullOrEmpty(central.TransportUnitId))
                throw new ArgumentException("The match requires a known stationary central building definition.");
            centralBuildingId = definition.CentralBuildingId;
            if (!HasCentral(firstPlayer) || !HasCentral(secondPlayer)) throw new ArgumentException("Each participant must start with a central building.");
            Mode = definition.Mode;
            PlayerIds = Array.AsReadOnly(new[] { firstPlayer, secondPlayer });
            ObjectivesRequired = definition.ObjectivesRequired;
            ContinuousHoldTicks = definition.ContinuousHoldTicks;
            if (definition.CaptureTicks < 1 || definition.CaptureTicks > 36000 || ContinuousHoldTicks < 1 || ContinuousHoldTicks > 144000)
                throw new ArgumentException("Capture and continuous hold timers are outside supported bounds.");
            var entries = definition.Objectives ?? Array.Empty<DominionObjectiveDefinition>();
            if (Mode == VictoryMode.Dominion && (entries.Length != 3 || ObjectivesRequired < 1 || ObjectivesRequired > entries.Length))
                throw new ArgumentException("Dominion requires three objectives and a reachable control threshold.");
            if (entries.Length > 3) throw new ArgumentException("An offline match supports at most three objectives.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id) || !world.navigation.IsWalkable(entry.Position) ||
                    entry.RadiusMillimetres < 1 || entry.RadiusMillimetres > 1000000)
                    throw new ArgumentException("Objectives require unique IDs, positive radii, and walkable positions inside the map.");
                objectives.Add(new DominionObjectiveState(entry, definition.CaptureTicks));
            }
            Objectives = objectives.AsReadOnly();
        }

        public long GetHoldTicks(int playerId) => playerId == firstPlayer ? firstHold : playerId == secondPlayer ? secondHold : 0;
        public bool IsParticipant(int playerId) => playerId == firstPlayer || playerId == secondPlayer;

        internal CommandResult Surrender(int playerId)
        {
            if (IsFinished) return CommandResult.Reject(CommandRejection.MatchFinished, "The match has ended.");
            if (!IsParticipant(playerId)) return CommandResult.Reject(CommandRejection.InvalidPlayer, "Only a participant may surrender.");
            Finish(playerId == firstPlayer ? secondPlayer : firstPlayer, MatchEndReason.Surrender);
            return CommandResult.Success();
        }

        internal void Tick()
        {
            if (IsFinished) return;
            ElapsedTicks = world.TickIndex;
            bool firstAlive = HasCentral(firstPlayer), secondAlive = HasCentral(secondPlayer);
            if (!firstAlive || !secondAlive)
            {
                Finish(firstAlive ? firstPlayer : secondAlive ? secondPlayer : 0,
                    firstAlive || secondAlive ? MatchEndReason.Conquest : MatchEndReason.SimultaneousElimination);
                return;
            }
            if (Mode != VictoryMode.Dominion) return;
            int firstControlled = 0, secondControlled = 0;
            foreach (var objective in objectives)
            {
                UpdateObjective(objective);
                if (!objective.Holding) continue;
                if (objective.OwnerId == firstPlayer) firstControlled++;
                else if (objective.OwnerId == secondPlayer) secondControlled++;
            }
            firstHold = firstControlled >= ObjectivesRequired ? firstHold + 1 : 0;
            secondHold = secondControlled >= ObjectivesRequired ? secondHold + 1 : 0;
            bool firstWins = firstHold >= ContinuousHoldTicks, secondWins = secondHold >= ContinuousHoldTicks;
            if (firstWins || secondWins) Finish(firstWins && secondWins ? 0 : firstWins ? firstPlayer : secondPlayer,
                firstWins && secondWins ? MatchEndReason.SimultaneousDominion : MatchEndReason.Dominion);
        }

        private void UpdateObjective(DominionObjectiveState objective)
        {
            bool first = false, second = false;
            foreach (var unit in world.units)
            {
                // Every armed army counts, creatures included; workers, transports and siege
                // equipment do not hold ground.
                if (unit.Domain != MovementDomain.Land || unit.IsWorker || unit.IsPackedOutpost || unit.AttackDamage <= 0 ||
                    (unit.Tags & (CombatTags.Infantry | CombatTags.Cavalry | CombatTags.Ranged | CombatTags.Creature)) == 0) continue;
                long dx = (long)unit.Position.X - objective.Position.X, dz = (long)unit.Position.Z - objective.Position.Z;
                if (dx * dx + dz * dz > (long)objective.RadiusMillimetres * objective.RadiusMillimetres) continue;
                if (unit.OwnerId == firstPlayer) first = true;
                else if (unit.OwnerId == secondPlayer) second = true;
            }
            objective.IsContested = first && second;
            int presence = first == second ? 0 : first ? firstPlayer : secondPlayer;
            bool captured = false;
            if (presence != 0 && presence != objective.OwnerId)
            {
                if (objective.CapturingPlayerId != presence) objective.CaptureProgressTicks = 0;
                objective.CapturingPlayerId = presence;
                objective.CaptureProgressTicks++;
                if (objective.CaptureProgressTicks == objective.CaptureRequiredTicks)
                {
                    objective.OwnerId = presence;
                    objective.CapturingPlayerId = 0; objective.CaptureProgressTicks = 0;
                    captured = true;
                }
            }
            else { objective.CapturingPlayerId = 0; objective.CaptureProgressTicks = 0; }
            objective.Holding = objective.OwnerId != 0 && !objective.IsContested && !captured &&
                (presence == 0 || presence == objective.OwnerId);
            objective.HoldTicks = objective.Holding ? objective.HoldTicks + 1 : 0;
        }

        // Only a completed central building keeps a participant alive. A foundation placed
        // while losing cannot hide in the fog and stall a decided match.
        private bool HasCentral(int owner)
        {
            foreach (var building in world.buildings)
                if (building.OwnerId == owner && building.DefinitionId == centralBuildingId && building.IsComplete) return true;
            return false;
        }

        private void Finish(int winner, MatchEndReason reason)
        { IsFinished = true; WinnerId = winner; Reason = reason; ElapsedTicks = world.TickIndex; }
    }
}
