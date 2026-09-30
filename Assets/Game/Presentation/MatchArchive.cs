using System;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Saved games. A file holds the start a match was dealt — map, faction, victory condition, opponent —
    /// and every order given since, so loading means dealing that start again and replaying the orders. The
    /// rules are deterministic, so what comes back is the same battlefield, not an approximation of it.
    /// </summary>
    public static class MatchArchive
    {
        // 3: transport orders must survive replay. Older builds refuse these saves rather than silently
        // dropping embark/disembark. Versions 1 and 2 remain readable (2 added turned builds and wall runs).
        public const int Version = 3;
        public const string SlotName = "match";

        [Serializable]
        public sealed class Order
        {
            public long Tick;
            public string Kind;
            public int Player;
            public int[] Units = Array.Empty<int>();
            public int Entity;
            public string Text;
            public int X, Z;
            public int Flag;
            // Wall runs: X, Z and 1 when turned for each stretch, in order.
            public int[] Sites = Array.Empty<int>();
        }

        [Serializable]
        public sealed class Save
        {
            public int Version;
            public string MapId, FactionId, SavedAtUtc;
            public int Mode, Difficulty;
            public long Tick;
            public Order[] Orders = Array.Empty<Order>();
        }

        public static string Directory => Path.Combine(NativeSmokeStorage.Root, "saves");
        public static string PathFor(string slot) => Path.Combine(Directory, slot + ".json");
        public static bool Exists(string slot = SlotName) => File.Exists(PathFor(slot));

        public static Save Capture(World world, string mapId, string factionId, OfflineAiDifficulty difficulty)
        {
            var save = new Save
            {
                Version = Version, MapId = mapId, FactionId = factionId,
                Mode = (int)(world.Match?.Mode ?? VictoryMode.Conquest), Difficulty = (int)difficulty,
                Tick = world.TickIndex, SavedAtUtc = DateTime.UtcNow.ToString("o"),
                Orders = Encode(world.Journal)
            };
            return save;
        }

        public static void Write(Save save, string slot = SlotName)
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(save, true));
        }

        public static Save Read(string slot = SlotName)
        {
            if (!Exists(slot)) return null;
            var save = JsonUtility.FromJson<Save>(File.ReadAllText(PathFor(slot)));
            return save != null && save.Version >= 1 && save.Version <= Version && !string.IsNullOrEmpty(save.MapId) ? save : null;
        }

        public static void Delete(string slot = SlotName) { if (Exists(slot)) File.Delete(PathFor(slot)); }

        public static Order[] Encode(MatchJournal journal)
        {
            var orders = new List<Order>(journal.Entries.Count);
            foreach (var entry in journal.Entries)
            {
                var order = Encode(entry.Command);
                if (order == null) continue;
                order.Tick = entry.Tick;
                orders.Add(order);
            }
            return orders.ToArray();
        }

        private static Order Encode(IGameCommand command)
        {
            switch (command)
            {
                case MoveCommand move: return new Order { Kind = "move", Player = move.PlayerId, Units = Ids(move.UnitIds), X = move.Destination.X, Z = move.Destination.Z, Flag = (int)move.Formation };
                case StopCommand stop: return new Order { Kind = "stop", Player = stop.PlayerId, Units = Ids(stop.UnitIds) };
                case AttackCommand attack: return new Order { Kind = "attack", Player = attack.PlayerId, Units = Ids(attack.UnitIds), Entity = attack.TargetEntityId };
                case GatherCommand gather: return new Order { Kind = "gather", Player = gather.PlayerId, Units = Ids(gather.WorkerIds), Entity = gather.ResourceId };
                case ReturnCargoCommand cargo: return new Order { Kind = "cargo", Player = cargo.PlayerId, Units = Ids(cargo.WorkerIds), Entity = cargo.DropOffBuildingId };
                case BuildCommand build: return new Order { Kind = "build", Player = build.PlayerId, Units = Ids(build.WorkerIds), Text = build.BuildingDefinitionId, X = build.Position.X, Z = build.Position.Z, Flag = build.Turned ? 1 : 0 };
                case BuildRunCommand run: return new Order { Kind = "wallrun", Player = run.PlayerId, Units = Ids(run.WorkerIds), Text = run.BuildingDefinitionId, Sites = Sites(run.Sites) };
                case ConstructCommand construct: return new Order { Kind = "construct", Player = construct.PlayerId, Units = Ids(construct.WorkerIds), Entity = construct.BuildingId };
                case TrainCommand train: return new Order { Kind = "train", Player = train.PlayerId, Entity = train.BuildingId, Text = train.UnitDefinitionId };
                case SetRallyCommand rally: return new Order { Kind = "rally", Player = rally.PlayerId, Entity = rally.BuildingId, X = rally.Destination.X, Z = rally.Destination.Z };
                case ResearchCommand research: return new Order { Kind = "research", Player = research.PlayerId, Entity = research.BuildingId, Text = research.TechnologyId };
                case SurrenderCommand surrender: return new Order { Kind = "surrender", Player = surrender.PlayerId };
                case BoardWallCommand board: return new Order { Kind = "board", Player = board.PlayerId, Units = Ids(board.UnitIds), Entity = board.WallId, Flag = board.SiegeUnitId };
                case LeaveWallCommand leave: return new Order { Kind = "leave", Player = leave.PlayerId, Units = Ids(leave.UnitIds), X = leave.Destination.X, Z = leave.Destination.Z };
                case EmbarkCommand embark: return new Order { Kind = "embark", Player = embark.PlayerId, Units = Ids(embark.UnitIds), Entity = embark.ShipId };
                case DisembarkCommand land: return new Order { Kind = "disembark", Player = land.PlayerId, Entity = land.ShipId, X = land.Destination.X, Z = land.Destination.Z };
                case SetGateCommand gate: return new Order { Kind = "gate", Player = gate.PlayerId, Entity = gate.BuildingId, Flag = gate.Open ? 1 : 0 };
                case SetCharterCommand charter: return new Order { Kind = "charter", Player = charter.PlayerId, Entity = charter.BuildingId, Flag = (int)charter.Charter };
                case DeployThreadkeeperCommand relay: return new Order { Kind = "relay", Player = relay.PlayerId, Entity = relay.UnitId, Flag = relay.StoreyardId };
                case PackOutpostCommand pack: return new Order { Kind = "pack", Player = pack.PlayerId, Entity = pack.BuildingId };
                case DeployOutpostCommand deploy: return new Order { Kind = "deploy", Player = deploy.PlayerId, Entity = deploy.UnitId, X = deploy.Position.X, Z = deploy.Position.Z };
                case RepositionCommand reposition: return new Order { Kind = "reposition", Player = reposition.PlayerId, Units = Ids(reposition.UnitIds) };
                default: return null;
            }
        }

        public static IGameCommand Decode(Order order)
        {
            var point = new SimPoint(order.X, order.Z);
            var units = order.Units ?? Array.Empty<int>();
            switch (order.Kind)
            {
                case "move": return new MoveCommand(order.Player, units, point, (MovementFormation)order.Flag);
                case "stop": return new StopCommand(order.Player, units);
                case "attack": return new AttackCommand(order.Player, units, order.Entity);
                case "gather": return new GatherCommand(order.Player, units, order.Entity);
                case "cargo": return new ReturnCargoCommand(order.Player, units, order.Entity);
                case "build": return new BuildCommand(order.Player, units, order.Text, point, order.Flag == 1);
                case "wallrun": return new BuildRunCommand(order.Player, units, order.Text, Sites(order.Sites));
                case "construct": return new ConstructCommand(order.Player, units, order.Entity);
                case "train": return new TrainCommand(order.Player, order.Entity, order.Text);
                case "rally": return new SetRallyCommand(order.Player, order.Entity, point);
                case "research": return new ResearchCommand(order.Player, order.Entity, order.Text);
                case "surrender": return new SurrenderCommand(order.Player);
                case "board": return new BoardWallCommand(order.Player, units, order.Entity, order.Flag);
                case "leave": return new LeaveWallCommand(order.Player, units, point);
                case "embark": return new EmbarkCommand(order.Player, units, order.Entity);
                case "disembark": return new DisembarkCommand(order.Player, order.Entity, point);
                case "gate": return new SetGateCommand(order.Player, order.Entity, order.Flag != 0);
                case "charter": return new SetCharterCommand(order.Player, order.Entity, (StoreyardCharter)order.Flag);
                case "relay": return new DeployThreadkeeperCommand(order.Player, order.Entity, order.Flag);
                case "pack": return new PackOutpostCommand(order.Player, order.Entity);
                case "deploy": return new DeployOutpostCommand(order.Player, order.Entity, point);
                case "reposition": return new RepositionCommand(order.Player, units);
                default: return null;
            }
        }

        /// <summary>
        /// Replays a stretch of the saved orders into a world that was dealt the same start. It returns when
        /// it reaches the tick budget, so a caller can spread a long match over several frames and keep the
        /// window answering. Feed it the cursor it hands back.
        /// </summary>
        public static int Replay(World world, Save save, int cursor, int tickBudget, out bool finished)
        {
            int spent = 0;
            while (cursor < save.Orders.Length)
            {
                var order = save.Orders[cursor];
                while (world.TickIndex < order.Tick && spent < tickBudget) { world.Tick(); spent++; }
                if (world.TickIndex < order.Tick) { finished = false; return cursor; }
                var command = Decode(order);
                if (command != null) world.Submit(command);
                cursor++;
            }
            while (world.TickIndex < save.Tick && spent < tickBudget) { world.Tick(); spent++; }
            finished = world.TickIndex >= save.Tick;
            return cursor;
        }

        private static int[] Ids(IReadOnlyList<int> source)
        {
            var ids = new int[source.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = source[i];
            return ids;
        }

        private static int[] Sites(IReadOnlyList<BuildSite> sites)
        {
            var values = new int[sites.Count * 3];
            for (int i = 0; i < sites.Count; i++) { values[i * 3] = sites[i].Position.X; values[i * 3 + 1] = sites[i].Position.Z; values[i * 3 + 2] = sites[i].Turned ? 1 : 0; }
            return values;
        }

        private static BuildSite[] Sites(int[] values)
        {
            var sites = new BuildSite[(values?.Length ?? 0) / 3];
            for (int i = 0; i < sites.Length; i++) sites[i] = new BuildSite(new SimPoint(values[i * 3], values[i * 3 + 1]), values[i * 3 + 2] == 1);
            return sites;
        }
    }
}
