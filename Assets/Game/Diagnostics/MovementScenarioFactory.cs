using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Emberfield.Simulation;

namespace Emberfield.Diagnostics
{
    public enum MovementScenarioKind { OpenField, WideCorridor, NarrowChoke, CrossingGroups, DynamicObstacle, Unreachable }

    public sealed class MovementScenario
    {
        private readonly Dictionary<int, SimPoint> goals = new Dictionary<int, SimPoint>();
        private readonly int builderId;
        private CommandResult[] commandResults;
        public World World { get; }
        public MovementScenarioKind Kind { get; }
        public int RequestedUnits { get; }
        public IReadOnlyList<MoveCommand> Orders { get; }
        public IReadOnlyList<int> UnitIds { get; }
        public IReadOnlyDictionary<int, SimPoint> ExpectedDestinations { get; }
        public bool OrdersIssued { get; private set; }
        public long StartTick { get; private set; } = -1;
        public long ElapsedTicks => OrdersIssued ? World.TickIndex - StartTick : 0;
        public CommandResult? DynamicBuildResult { get; private set; }

        internal MovementScenario(World world, MovementScenarioKind kind, int count, int[] ids, MoveCommand[] orders, int builder)
        {
            World = world; Kind = kind; RequestedUnits = count; builderId = builder;
            Orders = Array.AsReadOnly(orders); UnitIds = Array.AsReadOnly(ids);
            ExpectedDestinations = new ReadOnlyDictionary<int, SimPoint>(goals);
        }

        public CommandResult[] IssueOrders()
        {
            if (OrdersIssued) return (CommandResult[])commandResults.Clone();
            OrdersIssued = true; StartTick = World.TickIndex;
            commandResults = new CommandResult[Orders.Count];
            for (int orderIndex = 0; orderIndex < Orders.Count; orderIndex++)
            {
                var order = Orders[orderIndex];
                commandResults[orderIndex] = World.Submit(order);
                foreach (int id in order.UnitIds)
                {
                    World.TryGetUnit(id, out var unit);
                    goals[id] = commandResults[orderIndex].Accepted ? unit.Destination : order.Destination;
                }
            }
            return (CommandResult[])commandResults.Clone();
        }

        public CommandResult? AdvanceScheduledEvents()
        {
            if (!OrdersIssued || Kind != MovementScenarioKind.DynamicObstacle || DynamicBuildResult.HasValue || ElapsedTicks < 100)
                return null;
            DynamicBuildResult = World.Submit(new BuildCommand(1, new[] { builderId }, "diagnostic_barrier", new SimPoint(64500, 48500)));
            return DynamicBuildResult;
        }
    }

    /// <summary>Versioned, deterministic movement-only fixtures. No shipped combat/balance assets are mutated.</summary>
    public static class MovementScenarioFactory
    {
        public const string FixtureVersion = "movement-v1";
        public const int MapWidth = 128;
        public const int MapHeight = 96;
        public const int CellSize = 1000;
        public static readonly int[] StandardCounts = { 50, 100, 200, 300, 500 };

        public static MovementScenario Create(MovementScenarioKind kind, int unitCount)
        {
            if (!Enum.IsDefined(typeof(MovementScenarioKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (unitCount < 1 || unitCount > 500) throw new ArgumentOutOfRangeException(nameof(unitCount), "Movement fixtures support 1–500 movers.");
            if (kind == MovementScenarioKind.CrossingGroups && unitCount < 2) throw new ArgumentOutOfRangeException(nameof(unitCount));
            var definitions = new GameDefinition
            {
                BasePopulationCapacity = 1000,
                Units = new[] { new UnitDefinition
                {
                    Id = "tender", DisplayName = "Diagnostic Tender", IsWorker = true, MaxHealth = 60,
                    MoveSpeedMillimetresPerSecond = 3200, RadiusMillimetres = 300, Tags = CombatTags.Worker,
                    Attack = new AttackDefinition { Damage = 0 }, PopulationCost = 1
                } },
                Buildings = new[] { new BuildingDefinition
                {
                    Id = "diagnostic_barrier", DisplayName = "Diagnostic Barrier", WidthCells = 3, DepthCells = 9,
                    MaxHealth = 1000, BuildTicks = 1, Cost = default, TrainableUnitIds = Array.Empty<string>()
                } }
            };
            var spawns = new List<UnitSpawnDefinition>(unitCount + 1);
            var ids = new int[unitCount];
            var orders = new List<MoveCommand>(2);
            if (kind == MovementScenarioKind.CrossingGroups)
            {
                int first = unitCount / 2, second = unitCount - first;
                AddGroup(spawns, ids, 0, first, 1, 20, 48);
                AddGroup(spawns, ids, first, second, 2, 104, 48);
                orders.Add(new MoveCommand(1, Slice(ids, 0, first), new SimPoint(104500, 48500)));
                orders.Add(new MoveCommand(2, Slice(ids, first, second), new SimPoint(20500, 48500)));
            }
            else
            {
                AddGroup(spawns, ids, 0, unitCount, 1, 20, 48);
                orders.Add(new MoveCommand(1, ids, new SimPoint(104500, 48500)));
            }
            int builder = 0;
            if (kind == MovementScenarioKind.DynamicObstacle)
            {
                builder = unitCount + 1;
                spawns.Add(new UnitSpawnDefinition { Id = builder, OwnerId = 1, DefinitionId = "tender", Position = new SimPoint(64500, 60500) });
            }
            var blocked = new List<GridCell>();
            if (kind == MovementScenarioKind.WideCorridor)
                for (int x = 36; x <= 92; x++) { blocked.Add(new GridCell(x, 41)); blocked.Add(new GridCell(x, 55)); }
            if (kind == MovementScenarioKind.NarrowChoke || kind == MovementScenarioKind.Unreachable)
                for (int z = 0; z < MapHeight; z++)
                    if (kind == MovementScenarioKind.Unreachable || z != 48) blocked.Add(new GridCell(64, z));
            var map = new MapDefinition
            {
                Id = FixtureVersion + "-" + kind + "-" + unitCount, WidthCells = MapWidth, HeightCells = MapHeight,
                CellSizeMillimetres = CellSize, UnitSpawns = spawns.ToArray(), BlockedCells = blocked.ToArray()
            };
            return new MovementScenario(new World(definitions, map), kind, unitCount, ids, orders.ToArray(), builder);
        }

        private static void AddGroup(List<UnitSpawnDefinition> spawns, int[] ids, int offset, int count, int owner, int centerX, int centerZ)
        {
            int columns = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (count + columns - 1) / columns;
            int left = centerX - columns / 2, bottom = centerZ - rows / 2;
            for (int i = 0; i < count; i++)
            {
                int id = offset + i + 1; ids[offset + i] = id;
                spawns.Add(new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = "tender",
                    Position = new SimPoint((left + i % columns) * CellSize + CellSize / 2, (bottom + i / columns) * CellSize + CellSize / 2) });
            }
        }

        private static int[] Slice(int[] source, int start, int count)
        { var result = new int[count]; Array.Copy(source, start, result, 0, count); return result; }
    }
}
