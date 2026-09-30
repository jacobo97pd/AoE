using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Emberfield.Simulation;

namespace Emberfield.Diagnostics
{
    [Serializable]
    public sealed class OfflinePerformanceResult
    {
        public string Fixture, FixtureVersion, Description, Mode, PlayerOneFaction;
        public int RequestedMovers, Repetition, RequestedWarmupTicks, RequestedSampleTicks, MeasuredTicks;
        public long StartTick, EndTick;
        public bool WindowAvailable, MatchFinished;
        public string MatchReason, StartStateSha256, EndStateSha256, CommandTraceSha256;
        public int WinnerId, InitialUnits, FinalUnits, PeakUnits, PeakMovingUnits, PeakProjectiles;
        public int SurvivingRequestedMovers;
        public int PeakActiveResearch, PeakActiveCharters, PeakCharterAffectedUnits, PeakBoostedProducers, PeakGatherers;
        public int AiThinks, AiCommands, AiAccepted, ScriptCommands, ScriptAccepted;
        public bool AllocationMeasurementAvailable;
        public string AllocationMeasurementMethod;
        public long AllocationProbeObservedBytes, WorldAllocatedBytes, AiAllocatedBytes, ScriptAllocatedBytes;
        public long ManagedHeapBeforeBytes, ManagedHeapAfterBytes, ManagedHeapPeakBytes;
        public int Gen0Collections, Gen1Collections, Gen2Collections;
        public TimingDistribution WorldTicks, MovingWorldTicks, AiTicks, AiThinkingTicks, ScriptBatches, MoveCommands;
        public long MovePathQueries, MoveVisitedCells;
        public long PathQueries, AttackQueries, ConnectivityRebuilds, PathVisitedCells, AttackVisitedCells, ConnectivityVisitedCells;
        public long SharedFieldBuilds, SharedFieldCacheHits, RecoveryQueries;
        public double NavigationMilliseconds, MaxNavigationQueryMilliseconds;
        public string[] ScriptResults;
        public bool RepetitionMatchesFirst;
    }

    /// <summary>Opt-in CPU diagnostics. Creates no presentation objects and changes no simulation algorithms.</summary>
    public static class OfflinePerformanceBenchmark
    {
        public const string FixtureVersion = "offline-cpu-v1";
        public const string FingerprintMethod = "SHA256 of ordered public unit/building/player/resource/projectile/objective state and both visible/explored masks. This is an observable-state comparison, not a savegame or a proof that private path/work remainders match.";
        public const string MeasurementMethod = "World and AI timing/allocation windows are disjoint. AI includes its legal command submissions; fixed script batches are separate. Navigation counters include all three windows. Observation, hashing, report construction and GC.GetTotalMemory(false) sampling are outside timed windows. GC collection/heap fields cover the whole sample loop, including diagnostics; heap deltas are not allocation totals. No forced collection, sleep, rendering, GPU or mobile-device measurement.";

        private sealed class PendingCommand
        {
            public IGameCommand Command;
            public CommandResult Result;
        }
        private sealed class Trace : IDisposable
        {
            private readonly MemoryStream stream = new MemoryStream();
            private readonly BinaryWriter writer;
            private readonly PendingCommand[] pending = new PendingCommand[16];
            private int count;
            internal Trace() { writer = new BinaryWriter(stream, Encoding.UTF8, true); for (int i = 0; i < pending.Length; i++) pending[i] = new PendingCommand(); }
            // Fixed storage keeps trace serialization and its allocations outside AI's measured window.
            internal void Capture(IGameCommand command, CommandResult result)
            {
                if (count == pending.Length) throw new InvalidOperationException("AI exceeded the diagnostic command capture bound.");
                pending[count].Command = command; pending[count++].Result = result;
            }
            internal void Flush(long tick)
            {
                for (int i = 0; i < count; i++) { Add(tick, pending[i].Command, pending[i].Result); pending[i].Command = null; }
                count = 0;
            }
            internal void Add(long tick, IGameCommand command, CommandResult result)
            {
                writer.Write(tick); writer.Write(command.GetType().Name); writer.Write(command.PlayerId);
                if (command is MoveCommand move) { Ids(writer, move.UnitIds); Point(writer, move.Destination); writer.Write((int)move.Formation); }
                else if (command is GatherCommand gather) { Ids(writer, gather.WorkerIds); writer.Write(gather.ResourceId); }
                // A turned build adds its flag, so traces of unturned builds stay as they were recorded.
                else if (command is BuildCommand build) { Ids(writer, build.WorkerIds); writer.Write(build.BuildingDefinitionId); Point(writer, build.Position); if (build.Turned) writer.Write(true); }
                else if (command is BuildRunCommand run)
                { Ids(writer, run.WorkerIds); writer.Write(run.BuildingDefinitionId); writer.Write(run.Sites.Count); foreach (var site in run.Sites) { Point(writer, site.Position); writer.Write(site.Turned); } }
                else if (command is ConstructCommand construct) { Ids(writer, construct.WorkerIds); writer.Write(construct.BuildingId); }
                else if (command is ReturnCargoCommand cargo) { Ids(writer, cargo.WorkerIds); writer.Write(cargo.DropOffBuildingId); }
                else if (command is TrainCommand train) { writer.Write(train.BuildingId); writer.Write(train.UnitDefinitionId); }
                else if (command is ResearchCommand research) { writer.Write(research.BuildingId); writer.Write(research.TechnologyId); }
                else if (command is AttackCommand attack) { Ids(writer, attack.UnitIds); writer.Write(attack.TargetEntityId); }
                else if (command is SetCharterCommand charter) { writer.Write(charter.BuildingId); writer.Write((int)charter.Charter); }
                else if (command is SetRallyCommand rally) { writer.Write(rally.BuildingId); Point(writer, rally.Destination); }
                else if (command is DeployThreadkeeperCommand relay) { writer.Write(relay.UnitId); writer.Write(relay.StoreyardId); }
                else if (command is StopCommand stop) Ids(writer, stop.UnitIds);
                else if (command is RepositionCommand reposition) Ids(writer, reposition.UnitIds);
                else if (command is PackOutpostCommand pack) writer.Write(pack.BuildingId);
                else if (command is DeployOutpostCommand deploy) { writer.Write(deploy.UnitId); Point(writer, deploy.Position); }
                else if (!(command is SurrenderCommand)) throw new InvalidOperationException("Add new command payload to the diagnostic trace: " + command.GetType().Name);
                writer.Write((int)result.Reason); writer.Write(result.EntityId);
            }
            internal string Finish() { writer.Flush(); return Hash(stream.ToArray()); }
            public void Dispose() { writer.Dispose(); stream.Dispose(); }
        }

        public static OfflinePerformanceResult RunStress(GameDefinition shippedRules, int movers, int sampleTicks = 2400, int repetition = 1)
        {
            if (shippedRules == null) throw new ArgumentNullException(nameof(shippedRules));
            if (movers < 1 || movers > 500) throw new ArgumentOutOfRangeException(nameof(movers));
            var rules = new GameDefinition
            {
                Units = shippedRules.Units, Buildings = shippedRules.Buildings, Resources = shippedRules.Resources,
                Eras = shippedRules.Eras, Technologies = shippedRules.Technologies, Factions = shippedRules.Factions,
                StartingResources = new ResourceAmount(5000, 5000, 5000, 5000), BasePopulationCapacity = 1000,
                MaxProductionQueue = shippedRules.MaxProductionQueue
            };
            var map = StressMap(rules, movers);
            var world = new World(rules, map);
            var scripts = new Dictionary<long, List<IGameCommand>>();
            var ids = new int[movers]; for (int i = 0; i < movers; i++) ids[i] = i + 1;
            for (int tick = 0; tick < sampleTicks; tick += 800)
                Schedule(scripts, tick, new MoveCommand(1, ids, new SimPoint(tick % 1600 == 0 ? 104500 : 20500, 48500)));
            Schedule(scripts, 0, new SetCharterCommand(1, 1002, StoreyardCharter.Logistics));
            Schedule(scripts, 0, new SetCharterCommand(1, 1003, StoreyardCharter.Muster));
            Schedule(scripts, 0, new ResearchCommand(1, 1001, "advance_kingdom"));
            Schedule(scripts, 0, new SetRallyCommand(1, 1004, new SimPoint(39500, 30500)));
            for (int i = 0; i < 4; i++) Schedule(scripts, 0, new GatherCommand(1, new[] { movers + i + 1 }, 2001 + i));
            for (int tick = 0; tick < sampleTicks; tick += 200) Schedule(scripts, tick, new TrainCommand(1, 1004, "reedguard"));
            Schedule(scripts, 100, new DeployThreadkeeperCommand(1, movers + 9, 1002));
            Schedule(scripts, 600, new ResearchCommand(1, 1005, "gather_1"));
            Schedule(scripts, 600, new ResearchCommand(1, 1006, "weapons_1"));
            Schedule(scripts, 1000, new ResearchCommand(1, 1005, "aven_stores"));
            Schedule(scripts, 1000, new ResearchCommand(1, 1006, "armor_1"));
            Schedule(scripts, 1600, new SetCharterCommand(1, 1002, StoreyardCharter.Muster));
            Schedule(scripts, 1600, new SetCharterCommand(1, 1003, StoreyardCharter.Logistics));
            return Run(world, new[] { new OfflineAi(world, 2) }, scripts, "SyntheticMovers", movers, 0, sampleTicks, repetition,
                "Synthetic 128x128m noncombat scaling fixture; an 11-cell solid buffer across z70..80 isolates the Serevin AI annex beyond attack range, keeping N Aven Tender movers alive during alternating shuttle commands every 40s. Four additional gatherers per side, one Aven Threadkeeper, authored completed bases, 5000 of each resource/player, base population 1000. Serevin AI remains active; player1 receives fixed paid charter/research/train orders. Shipped rules, costs, timers, fog and faction effects are unchanged. This deliberately unreachable opponent is not a complete-match or combat fixture; unrestricted shipped samples cover those systems.");
        }

        public static OfflinePerformanceResult RunShipped(World freshWorld, int warmupTicks, int sampleTicks, int repetition = 1)
        {
            if (freshWorld == null || freshWorld.Match == null || freshWorld.Vision == null || freshWorld.TickIndex != 0)
                throw new ArgumentException("Provide a fresh opt-in shipped offline world.");
            var ai = new[] { new OfflineAi(freshWorld, freshWorld.Match.PlayerIds[0]), new OfflineAi(freshWorld, freshWorld.Match.PlayerIds[1]) };
            return Run(freshWorld, ai, null, warmupTicks == 0 ? "ShippedEarly" : "ShippedMid", 0, warmupTicks, sampleTicks, repetition,
                "Unmodified caller-provided shipped world and starting stock; both ordinary AI controllers advance naturally before and during the sampled window. This measures a CPU sample, not full-match pacing or completion. An ended world yields an unavailable or truncated window, never repeated frozen ticks.");
        }

        private static OfflinePerformanceResult Run(World world, OfflineAi[] ai, Dictionary<long, List<IGameCommand>> scripts,
            string fixture, int movers, int warmupTicks, int sampleTicks, int repetition, string description)
        {
            if (warmupTicks < 0 || warmupTicks > 72000 || sampleTicks < 1 || sampleTicks > 12000 || repetition < 1)
                throw new ArgumentOutOfRangeException(nameof(sampleTicks));
            bool allocations = MovementBenchmark.AllocationProbeObservedBytes >= 4096;
            for (int tick = 0; tick < warmupTicks && !world.Match.IsFinished; tick++) { Think(world, ai); world.Tick(); }
            var result = new OfflinePerformanceResult
            {
                Fixture = fixture, FixtureVersion = FixtureVersion, Description = description, Mode = world.Match.Mode.ToString(),
                RequestedMovers = movers, Repetition = repetition, RequestedWarmupTicks = warmupTicks, RequestedSampleTicks = sampleTicks,
                StartTick = world.TickIndex, WindowAvailable = !world.Match.IsFinished && world.TickIndex == warmupTicks,
                InitialUnits = world.Units.Count, StartStateSha256 = Fingerprint(world),
                AllocationMeasurementAvailable = allocations, AllocationMeasurementMethod = MovementBenchmark.AllocationMeasurementMethod,
                AllocationProbeObservedBytes = MovementBenchmark.AllocationProbeObservedBytes
            };
            world.TryGetPlayer(world.Match.PlayerIds[0], out var player); result.PlayerOneFaction = player.FactionId;
            var worldTimes = new long[sampleTicks]; var movingTimes = new long[sampleTicks]; int movingCount = 0;
            var aiTimes = new long[sampleTicks]; var thinkTimes = new long[sampleTicks]; int thinkCount = 0;
            var scriptTimes = new long[sampleTicks]; int scriptCount = 0;
            var moveTimes = new long[sampleTicks]; int moveCount = 0;
            var scriptResults = new List<string>();
            int beforeThinks = Sum(ai, 0), beforeCommands = Sum(ai, 1), beforeAccepted = Sum(ai, 2);
            world.NavigationMetrics.Enabled = true; world.NavigationMetrics.Reset();
            using (var trace = new Trace())
            {
                foreach (var controller in ai) controller.CommandIssued += trace.Capture;
                result.ManagedHeapBeforeBytes = result.ManagedHeapPeakBytes = GC.GetTotalMemory(false);
                int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
                for (int tick = 0; tick < sampleTicks && !world.Match.IsFinished; tick++)
                {
                    long allocationStart, started;
                    if (scripts != null && scripts.TryGetValue(world.TickIndex, out var commands))
                    {
                        var responses = new CommandResult[commands.Count]; // Exclude fixture storage from command measurement.
                        allocationStart = Allocated(allocations); started = Stopwatch.GetTimestamp();
                        for (int i = 0; i < commands.Count; i++)
                        {
                            if (commands[i] is MoveCommand)
                            {
                                var prior = world.NavigationMetrics.Snapshot;
                                long moveStart = Stopwatch.GetTimestamp(); responses[i] = world.Submit(commands[i]);
                                moveTimes[moveCount++] = Stopwatch.GetTimestamp() - moveStart;
                                var next = world.NavigationMetrics.Snapshot;
                                result.MovePathQueries += next.PathQueryCount - prior.PathQueryCount;
                                result.MoveVisitedCells += next.PathVisitedCells - prior.PathVisitedCells;
                            }
                            else responses[i] = world.Submit(commands[i]);
                        }
                        scriptTimes[scriptCount++] = Stopwatch.GetTimestamp() - started;
                        result.ScriptAllocatedBytes += Difference(allocations, allocationStart);
                        for (int i = 0; i < commands.Count; i++)
                        {
                            result.ScriptCommands++; if (responses[i].Accepted) result.ScriptAccepted++;
                            scriptResults.Add(world.TickIndex + ":" + commands[i].GetType().Name + ":" + responses[i].Reason);
                            trace.Add(world.TickIndex, commands[i], responses[i]);
                        }
                    }
                    int priorThinks = Sum(ai, 0);
                    allocationStart = Allocated(allocations); started = Stopwatch.GetTimestamp();
                    Think(world, ai);
                    long elapsed = Stopwatch.GetTimestamp() - started;
                    result.AiAllocatedBytes += Difference(allocations, allocationStart);
                    aiTimes[tick] = elapsed; if (Sum(ai, 0) != priorThinks) thinkTimes[thinkCount++] = elapsed;
                    trace.Flush(world.TickIndex);
                    bool moving = Observe(world, result) > 0;
                    allocationStart = Allocated(allocations); started = Stopwatch.GetTimestamp();
                    world.Tick();
                    elapsed = Stopwatch.GetTimestamp() - started;
                    result.WorldAllocatedBytes += Difference(allocations, allocationStart);
                    worldTimes[tick] = elapsed; if (moving) movingTimes[movingCount++] = elapsed;
                    result.MeasuredTicks++;
                    if (tick % 20 == 0) result.ManagedHeapPeakBytes = Math.Max(result.ManagedHeapPeakBytes, GC.GetTotalMemory(false));
                }
                result.ManagedHeapAfterBytes = GC.GetTotalMemory(false);
                result.ManagedHeapPeakBytes = Math.Max(result.ManagedHeapPeakBytes, result.ManagedHeapAfterBytes);
                result.Gen0Collections = GC.CollectionCount(0) - gen0; result.Gen1Collections = GC.CollectionCount(1) - gen1; result.Gen2Collections = GC.CollectionCount(2) - gen2;
                foreach (var controller in ai) controller.CommandIssued -= trace.Capture;
                result.CommandTraceSha256 = trace.Finish();
            }
            result.WorldTicks = TimingDistribution.From(worldTimes, result.MeasuredTicks);
            result.MovingWorldTicks = TimingDistribution.From(movingTimes, movingCount);
            result.AiTicks = TimingDistribution.From(aiTimes, result.MeasuredTicks);
            result.AiThinkingTicks = TimingDistribution.From(thinkTimes, thinkCount);
            result.ScriptBatches = TimingDistribution.From(scriptTimes, scriptCount);
            result.MoveCommands = TimingDistribution.From(moveTimes, moveCount);
            result.AiThinks = Sum(ai, 0) - beforeThinks; result.AiCommands = Sum(ai, 1) - beforeCommands; result.AiAccepted = Sum(ai, 2) - beforeAccepted;
            result.ScriptResults = scriptResults.ToArray(); result.EndTick = world.TickIndex; result.FinalUnits = world.Units.Count;
            for (int id = 1; id <= movers; id++) if (world.TryGetUnit(id, out _)) result.SurvivingRequestedMovers++;
            result.MatchFinished = world.Match.IsFinished; result.WinnerId = world.Match.WinnerId; result.MatchReason = world.Match.Reason.ToString();
            result.EndStateSha256 = Fingerprint(world);
            var metrics = world.NavigationMetrics.Snapshot;
            result.PathQueries = metrics.PathQueryCount; result.AttackQueries = metrics.AttackQueryCount; result.ConnectivityRebuilds = metrics.ConnectivityRebuildCount;
            result.PathVisitedCells = metrics.PathVisitedCells; result.AttackVisitedCells = metrics.AttackVisitedCells; result.ConnectivityVisitedCells = metrics.ConnectivityVisitedCells;
            result.SharedFieldBuilds = metrics.SharedFieldBuildCount; result.SharedFieldCacheHits = metrics.SharedFieldCacheHitCount; result.RecoveryQueries = metrics.RecoveryQueryCount;
            result.NavigationMilliseconds = MovementBenchmark.Milliseconds(metrics.TotalElapsedStopwatchTicks);
            result.MaxNavigationQueryMilliseconds = MovementBenchmark.Milliseconds(Math.Max(metrics.MaxQueryStopwatchTicks, metrics.MaxConnectivityStopwatchTicks));
            if (!allocations) result.WorldAllocatedBytes = result.AiAllocatedBytes = result.ScriptAllocatedBytes = -1;
            return result;
        }

        private static void Think(World world, OfflineAi[] ai)
        { if (ai.Length == 2 && world.TickIndex % 20 >= 10) { ai[1].Tick(); ai[0].Tick(); } else foreach (var item in ai) item.Tick(); }
        private static int Sum(OfflineAi[] ai, int field)
        { int total = 0; foreach (var item in ai) total += field == 0 ? item.Statistics.Thinks : field == 1 ? item.Statistics.Commands : item.Statistics.Accepted; return total; }
        private static long Allocated(bool available) => available ? GC.GetAllocatedBytesForCurrentThread() : -1;
        private static long Difference(bool available, long before) => available ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
        private static void Schedule(Dictionary<long, List<IGameCommand>> schedule, long tick, IGameCommand command)
        { if (!schedule.TryGetValue(tick, out var list)) schedule.Add(tick, list = new List<IGameCommand>()); list.Add(command); }
        private static int Observe(World world, OfflinePerformanceResult result)
        {
            int moving = 0, gatherers = 0, affected = 0, research = 0, charters = 0, boosted = 0;
            foreach (var unit in world.Units)
            { if (unit.Order == UnitOrder.Moving) moving++; if (unit.WorkerTask != WorkerTask.None) gatherers++; if (unit.CharterSourceId != 0) affected++; }
            foreach (var building in world.Buildings)
            { if (building.ActiveResearch != null) research++; if (building.ActiveCharter != StoreyardCharter.None && building.CharterRemainingTicks == 0) charters++; if (building.ProductionWorkPermille > 1000 && building.ProductionQueue.Count > 0) boosted++; }
            result.PeakUnits = Math.Max(result.PeakUnits, world.Units.Count); result.PeakMovingUnits = Math.Max(result.PeakMovingUnits, moving);
            result.PeakGatherers = Math.Max(result.PeakGatherers, gatherers); result.PeakCharterAffectedUnits = Math.Max(result.PeakCharterAffectedUnits, affected);
            result.PeakActiveResearch = Math.Max(result.PeakActiveResearch, research); result.PeakActiveCharters = Math.Max(result.PeakActiveCharters, charters);
            result.PeakBoostedProducers = Math.Max(result.PeakBoostedProducers, boosted); result.PeakProjectiles = Math.Max(result.PeakProjectiles, world.Projectiles.Count);
            return moving;
        }

        private static MapDefinition StressMap(GameDefinition rules, int count)
        {
            var units = new List<UnitSpawnDefinition>(); int columns = (int)Math.Ceiling(Math.Sqrt(count)); int rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++) units.Add(new UnitSpawnDefinition { Id = i + 1, OwnerId = 1, DefinitionId = "tender", Position = new SimPoint((20 - columns / 2 + i % columns) * 1000 + 500, (48 - rows / 2 + i / columns) * 1000 + 500) });
            for (int owner = 1; owner <= 2; owner++) for (int i = 0; i < 4; i++)
                units.Add(new UnitSpawnDefinition { Id = count + (owner - 1) * 4 + i + 1, OwnerId = owner, DefinitionId = "tender", Position = new SimPoint((owner == 1 ? 22 : 100) * 1000 + i * 1000 + 500, (owner == 1 ? 21 : 95) * 1000 + 500) });
            units.Add(new UnitSpawnDefinition { Id = count + 9, OwnerId = 1, DefinitionId = "threadkeeper", Position = new SimPoint(26500, 16500) });
            var buildings = new List<BuildingSpawnDefinition>();
            AddBuilding(rules, buildings, 1001, 1, "hearth", 16, 16); AddBuilding(rules, buildings, 1002, 1, "storeyard", 24, 20);
            AddBuilding(rules, buildings, 1003, 1, "storeyard", 34, 16); AddBuilding(rules, buildings, 1004, 1, "muster_hall", 39, 16);
            AddBuilding(rules, buildings, 1005, 1, "archive", 45, 16); AddBuilding(rules, buildings, 1006, 1, "muster_hall", 39, 22);
            AddBuilding(rules, buildings, 1101, 2, "hearth", 104, 100); AddBuilding(rules, buildings, 1102, 2, "storeyard", 96, 100);
            AddBuilding(rules, buildings, 1103, 2, "muster_hall", 90, 100); AddBuilding(rules, buildings, 1104, 2, "archive", 84, 100);
            var resources = new List<ResourceSpawnDefinition>();
            var blocked = new GridCell[128 * 11];
            for (int z = 70; z <= 80; z++) for (int x = 0; x < 128; x++) blocked[(z - 70) * 128 + x] = new GridCell(x, z);
            string[] types = { "food_source", "wood_source", "metal_source", "stone_source" };
            for (int owner = 1; owner <= 2; owner++) for (int i = 0; i < 4; i++)
                resources.Add(new ResourceSpawnDefinition { Id = 2001 + (owner - 1) * 4 + i, DefinitionId = types[i], Position = new SimPoint((owner == 1 ? 21 : 99) * 1000 + i * 2000 + 500, (owner == 1 ? 24 : 91) * 1000 + 500) });
            return new MapDefinition
            {
                Id = FixtureVersion, WidthCells = 128, HeightCells = 128, CellSizeMillimetres = 1000,
                UnitSpawns = units.ToArray(), BuildingSpawns = buildings.ToArray(), ResourceSpawns = resources.ToArray(), BlockedCells = blocked,
                PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "aven" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "serevin" } },
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, Mode = VictoryMode.Conquest }
            };
        }
        private static void AddBuilding(GameDefinition rules, List<BuildingSpawnDefinition> list, int id, int owner, string type, int x, int z)
        {
            foreach (var definition in rules.Buildings) if (definition.Id == type)
            { list.Add(new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = type, Position = new SimPoint(x * 1000 + definition.WidthCells % 2 * 500, z * 1000 + definition.DepthCells % 2 * 500) }); return; }
            throw new ArgumentException("Shipped diagnostic fixture requires building " + type);
        }

        public static string Fingerprint(World world)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(FixtureVersion); writer.Write(world.TickIndex); writer.Write(world.DeathCount);
                writer.Write(world.Match.IsFinished); writer.Write(world.Match.WinnerId); writer.Write((int)world.Match.Reason);
                foreach (var player in world.Players)
                {
                    writer.Write(player.Id); Text(writer, player.FactionId); Text(writer, player.EraId); writer.Write(player.EraTier);
                    Amount(writer, player.Resources); writer.Write(player.PopulationUsed); writer.Write(player.PopulationReserved); writer.Write(player.PopulationCapacity);
                    writer.Write(player.CompletedTechnologyIds.Count); foreach (var id in player.CompletedTechnologyIds) Text(writer, id);
                    writer.Write(world.Match.GetHoldTicks(player.Id));
                }
                writer.Write(world.Units.Count);
                foreach (var unit in world.Units)
                {
                    writer.Write(unit.Id); writer.Write(unit.OwnerId); Text(writer, unit.DefinitionId); Point(writer, unit.Position); Point(writer, unit.PreviousPosition); Point(writer, unit.Destination);
                    writer.Write(unit.Health); writer.Write(unit.Armor); writer.Write(unit.AttackDamage); writer.Write(unit.AttackTargetId); writer.Write(unit.AttackCooldownTicks); writer.Write(unit.AutoAttackEnabled);
                    writer.Write((int)unit.Order); writer.Write((int)unit.WorkerTask); writer.Write((int)unit.CarriedKind); writer.Write(unit.CarriedAmount); writer.Write(unit.CarryCapacity); writer.Write(unit.GatherAmount);
                    writer.Write(unit.TargetResourceId); writer.Write(unit.TargetBuildingId); writer.Write(unit.MoveSpeedMillimetresPerSecond); writer.Write((int)unit.Formation); writer.Write(unit.MovementBlockedTicks);
                    writer.Write(unit.CharterSourceId); writer.Write(unit.ThreadkeeperRemainingTicks); writer.Write(unit.ThreadkeeperCooldownTicks); writer.Write(unit.LinkedStoreyardId);
                    writer.Write(unit.RepositionRemainingTicks); writer.Write(unit.RepositionCooldownTicks); writer.Write((int)unit.RelocationStage); writer.Write(unit.RelocationRemainingTicks); writer.Write(unit.RelocationTotalTicks);
                    Text(writer, unit.PackedBuildingDefinitionId); Point(writer, unit.DeploymentPosition);
                }
                writer.Write(world.Buildings.Count);
                foreach (var building in world.Buildings)
                {
                    writer.Write(building.Id); writer.Write(building.OwnerId); Text(writer, building.DefinitionId); Point(writer, building.Position); writer.Write(building.Health);
                    writer.Write(building.BuildProgressTicks); writer.Write((int)building.ActiveCharter); writer.Write((int)building.PendingCharter); writer.Write(building.CharterRemainingTicks);
                    writer.Write(building.CharterSourceId); writer.Write(building.ProductionWorkPermille); writer.Write((int)building.RelocationStage); writer.Write(building.RelocationRemainingTicks); writer.Write(building.RelocationTotalTicks);
                    writer.Write(building.HasRallyPoint); Point(writer, building.RallyPoint); writer.Write(building.ProductionQueue.Count);
                    foreach (var entry in building.ProductionQueue) { Text(writer, entry.UnitDefinitionId); writer.Write(entry.RemainingTicks); writer.Write(entry.TotalTicks); }
                    Text(writer, building.ActiveResearch?.TechnologyId); writer.Write(building.ActiveResearch?.RemainingTicks ?? -1);
                }
                writer.Write(world.Resources.Count); foreach (var resource in world.Resources) { writer.Write(resource.Id); writer.Write(resource.RemainingAmount); }
                writer.Write(world.Projectiles.Count); foreach (var projectile in world.Projectiles)
                { writer.Write(projectile.Id); writer.Write(projectile.OwnerId); writer.Write(projectile.SourceUnitId); writer.Write(projectile.TargetEntityId); Point(writer, projectile.Position); Point(writer, projectile.PreviousPosition); writer.Write(projectile.RemainingLifetimeTicks); }
                foreach (var objective in world.Match.Objectives)
                { Text(writer, objective.Id); writer.Write(objective.OwnerId); writer.Write(objective.CapturingPlayerId); writer.Write(objective.CaptureProgressTicks); writer.Write(objective.IsContested); writer.Write(objective.HoldTicks); }
                foreach (int owner in world.Match.PlayerIds) for (int z = 0; z < world.Map.HeightCells; z++) for (int x = 0; x < world.Map.WidthCells; x++)
                { var point = new SimPoint(x * world.Map.CellSizeMillimetres + world.Map.CellSizeMillimetres / 2, z * world.Map.CellSizeMillimetres + world.Map.CellSizeMillimetres / 2); writer.Write(world.Vision.IsVisible(owner, point)); writer.Write(world.Vision.IsExplored(owner, point)); }
                writer.Flush(); return Hash(stream.ToArray());
            }
        }
        private static void Ids(BinaryWriter writer, IReadOnlyList<int> ids) { writer.Write(ids.Count); foreach (int id in ids) writer.Write(id); }
        private static void Point(BinaryWriter writer, SimPoint point) { writer.Write(point.X); writer.Write(point.Z); }
        private static void Amount(BinaryWriter writer, ResourceAmount amount) { writer.Write(amount.Food); writer.Write(amount.Wood); writer.Write(amount.Metal); writer.Write(amount.Stone); }
        private static void Text(BinaryWriter writer, string value) { writer.Write(value ?? ""); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
