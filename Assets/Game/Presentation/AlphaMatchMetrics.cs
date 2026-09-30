using System;
using System.Collections.Generic;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    /// <summary>Local client-session observations. Never reads an opponent's private economy or uploads data.</summary>
    public sealed class AlphaMatchMetrics
    {
        private const int WindowTicks = World.TickRate * 60;
        private readonly int playerId;
        private readonly Bucket[] buckets = new Bucket[WindowTicks];
        private readonly HashSet<string> producerDefinitions = new HashSet<string>(StringComparer.Ordinal);
        private readonly SampleWindow frames = new SampleWindow(300);
        private readonly SampleWindow cpuTicks = new SampleWindow(200);
        // Whole-session smoothness diagnostics, independent of the rolling HUD windows above: bounded so a very
        // long match cannot grow this unboundedly, but never overwritten, so tail percentiles cover the session.
        private readonly SessionSamples sessionFrames = new SessionSamples(20000);
        private readonly SessionSamples sessionTicks = new SessionSamples(20000);
        private World world;
        private long firstTick, observedTick, windowTick, beforeTick = -1;
        private ResourceAmount lastStock, beforeStock;
        private Totals recentIncome, recentNet, delivered;
        private long recentOrders;
        private bool exactCoverage;

        public int Workers { get; private set; }
        public int IdleWorkers { get; private set; }
        public int Army { get; private set; }
        public int PopulationUsed { get; private set; }
        public int PopulationReserved { get; private set; }
        public int PopulationCapacity { get; private set; }
        public int BusyProducers { get; private set; }
        public int Producers { get; private set; }
        /// <summary>Fraction of operational producers currently training or researching, not a historical average.</summary>
        public double ProductionUtilization => Producers == 0 ? 0 : BusyProducers / (double)Producers;
        public long AcceptedOrders { get; private set; }
        public double SessionSeconds => Math.Max(0, observedTick - firstTick) / (double)World.TickRate;
        public double RateWindowSeconds => Math.Max(1, Math.Min(60, SessionSeconds));
        public double OrdersPerMinute => recentOrders * 60.0 / RateWindowSeconds;
        public bool HasExactIncome => !world.IsNetworkReplica && exactCoverage;
        public bool HasLocalTickTiming => !world.IsNetworkReplica && cpuTicks.Count > 0;
        public double FramesPerSecond => frames.Mean > 0 ? 1000.0 / frames.Mean : 0;
        public double FrameP95Milliseconds => frames.P95;
        public double TickP95Milliseconds => HasLocalTickTiming ? cpuTicks.P95 : 0;
        public int FrameSamples => frames.Count;
        public int TickSamples => cpuTicks.Count;
        /// <summary>Whole-session frame pacing (not the ~5 s rolling HUD window above). A hitch is a sampled
        /// frame more than twice the session median and over one 30 fps frame (33 ms).</summary>
        public int SessionFrameSamples => sessionFrames.Count;
        public bool SessionFrameSampleOverflow => sessionFrames.Overflow;
        public double SessionFrameP50Milliseconds => sessionFrames.P50;
        public double SessionFrameP95Milliseconds => sessionFrames.P95;
        public double SessionFrameP99Milliseconds => sessionFrames.P99;
        public double SessionFrameP999Milliseconds => sessionFrames.P999;
        public double SessionFrameMaxMilliseconds => sessionFrames.Max;
        public double SessionFrameStdDevMilliseconds => sessionFrames.StdDev;
        public double SessionFrameHitchThresholdMilliseconds => sessionFrames.HitchThreshold;
        public int SessionFrameHitchCount => sessionFrames.HitchCount;
        public int SessionTickSamples => sessionTicks.Count;
        public double SessionTickP99Milliseconds => sessionTicks.P99;
        public double SessionTickMaxMilliseconds => sessionTicks.Max;

        public AlphaMatchMetrics(World world, int localPlayerId = 1)
        {
            if (localPlayerId < 1) throw new ArgumentOutOfRangeException(nameof(localPlayerId));
            playerId = localPlayerId;
            Reset(world);
        }

        public long Delivered(ResourceKind kind) => delivered.Get(kind);
        public double IncomePerMinute(ResourceKind kind) => HasExactIncome ? recentIncome.Get(kind) * 60.0 / RateWindowSeconds : 0;
        /// <summary>Signed stock change including purchases. Online snapshots cannot identify gross delivered income.</summary>
        public double NetPerMinute(ResourceKind kind) => recentNet.Get(kind) * 60.0 / RateWindowSeconds;

        /// <summary>Call after command dispatch and immediately before the local World.Tick.</summary>
        public void BeginTick(World observedWorld)
        {
            EnsureWorld(observedWorld);
            if (world.IsNetworkReplica) return;
            beforeTick = world.TickIndex;
            world.TryGetPlayer(playerId, out var player);
            beforeStock = player.Resources;
        }

        /// <summary>Call after each local tick, or after applying a remote snapshot. Duplicate observations are harmless.</summary>
        public void ObserveTick(World observedWorld, double cpuTickMilliseconds = double.NaN)
        {
            EnsureWorld(observedWorld);
            long tick = world.TickIndex;
            if (tick == observedTick) return;
            AdvanceWindow(tick);
            world.TryGetPlayer(playerId, out var player);
            var stock = player.Resources;
            var net = Totals.Difference(stock, lastStock);
            ref var bucket = ref buckets[(int)(tick % WindowTicks)];
            bucket.Net.Add(net); recentNet.Add(net);
            if (!world.IsNetworkReplica)
            {
                // EconomySystem.ReturnArrived is the sole in-tick stock credit. Submit spends happen before BeginTick.
                if (beforeTick == observedTick && tick == beforeTick + 1)
                {
                    var income = Totals.PositiveDifference(stock, beforeStock);
                    bucket.Income.Add(income); recentIncome.Add(income); delivered.Add(income);
                }
                else exactCoverage = false;
                if (IsNonnegativeFinite(cpuTickMilliseconds)) { cpuTicks.Add(cpuTickMilliseconds); sessionTicks.Add(cpuTickMilliseconds); }
            }
            beforeTick = -1; lastStock = stock; observedTick = tick;
            ReadLocalState();
        }

        /// <summary>Only confirmed local commands count. Replicas use their asynchronous acknowledgement hook.</summary>
        public void ObserveCommand(IGameCommand command, CommandResult result)
        {
            if (world.IsNetworkReplica || command == null || command.PlayerId != playerId || !result.Accepted) return;
            AddAcceptedOrder(); ReadLocalState();
        }

        /// <summary>Call once for a successful server reply, never when the command is queued or its reply is uncertain.</summary>
        public void ObserveAcceptedOnlineOrder()
        {
            if (world.IsNetworkReplica) AddAcceptedOrder();
        }

        /// <summary>Records displayed gameplay-frame intervals; menu/inactive frames are excluded by the caller.</summary>
        public void ObserveFrame(double unscaledDeltaSeconds, bool gameplayActive)
        {
            if (!gameplayActive || unscaledDeltaSeconds <= 0 || !IsNonnegativeFinite(unscaledDeltaSeconds)) return;
            double milliseconds = unscaledDeltaSeconds * 1000.0;
            frames.Add(milliseconds); sessionFrames.Add(milliseconds);
        }

        private void AddAcceptedOrder()
        {
            AdvanceWindow(world.TickIndex);
            if (AcceptedOrders < long.MaxValue) AcceptedOrders++;
            recentOrders++;
            buckets[(int)(world.TickIndex % WindowTicks)].Orders++;
        }

        private void EnsureWorld(World observedWorld)
        {
            if (!ReferenceEquals(world, observedWorld) || observedWorld.TickIndex < observedTick) Reset(observedWorld);
        }

        private void Reset(World observedWorld)
        {
            if (observedWorld == null || !observedWorld.TryGetPlayer(playerId, out var player))
                throw new ArgumentException("Metrics require the local player of a world.", nameof(observedWorld));
            world = observedWorld; firstTick = observedTick = windowTick = world.TickIndex;
            lastStock = player.Resources; beforeTick = -1; exactCoverage = !world.IsNetworkReplica;
            AcceptedOrders = recentOrders = 0; recentIncome = recentNet = delivered = default;
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new Bucket { Tick = -1 };
            buckets[(int)(windowTick % WindowTicks)].Tick = windowTick;
            frames.Clear(); cpuTicks.Clear(); sessionFrames.Clear(); sessionTicks.Clear(); producerDefinitions.Clear();
            foreach (var building in world.Definition.Buildings)
                if (building.TrainableUnitIds != null && building.TrainableUnitIds.Length > 0) producerDefinitions.Add(building.Id);
            foreach (var technology in world.Definition.Technologies)
                if (!string.IsNullOrEmpty(technology.ResearchBuildingId)) producerDefinitions.Add(technology.ResearchBuildingId);
            ReadLocalState();
        }

        private void AdvanceWindow(long tick)
        {
            if (tick <= windowTick) return;
            if (tick - windowTick >= WindowTicks)
            {
                recentIncome = recentNet = default; recentOrders = 0;
                for (int i = 0; i < buckets.Length; i++) buckets[i] = new Bucket { Tick = -1 };
            }
            else
            {
                for (long next = windowTick + 1; next <= tick; next++)
                {
                    ref var expired = ref buckets[(int)(next % WindowTicks)];
                    recentIncome.Subtract(expired.Income); recentNet.Subtract(expired.Net); recentOrders -= expired.Orders;
                    expired = new Bucket { Tick = next };
                }
            }
            windowTick = tick;
            buckets[(int)(tick % WindowTicks)].Tick = tick;
        }

        private void ReadLocalState()
        {
            world.TryGetPlayer(playerId, out var player);
            PopulationUsed = player.PopulationUsed; PopulationReserved = player.PopulationReserved; PopulationCapacity = player.PopulationCapacity;
            Workers = IdleWorkers = Army = Producers = BusyProducers = 0;
            for (int i = 0; i < world.Units.Count; i++)
            {
                var unit = world.Units[i];
                if (unit.OwnerId != playerId) continue;
                if (unit.IsWorker)
                {
                    Workers++;
                    if (unit.WorkerTask == WorkerTask.None && unit.Order == UnitOrder.Idle && unit.AttackTargetId == 0) IdleWorkers++;
                }
                else if (!unit.IsPackedOutpost && (unit.AttackDamage > 0 || (unit.Tags & CombatTags.Siege) != 0)) Army++;
            }
            for (int i = 0; i < world.Buildings.Count; i++)
            {
                var building = world.Buildings[i];
                if (building.OwnerId != playerId || !building.IsOperational || !producerDefinitions.Contains(building.DefinitionId)) continue;
                Producers++;
                if (building.ProductionQueue.Count > 0 || building.ActiveResearch != null) BusyProducers++;
            }
        }

        private static bool IsNonnegativeFinite(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        private struct Bucket { public long Tick, Orders; public Totals Income, Net; }
        private struct Totals
        {
            private long food, wood, metal, stone;
            public long Get(ResourceKind kind)
            {
                switch (kind)
                {
                    case ResourceKind.Food: return food;
                    case ResourceKind.Wood: return wood;
                    case ResourceKind.Metal: return metal;
                    case ResourceKind.Stone: return stone;
                    default: throw new ArgumentOutOfRangeException(nameof(kind));
                }
            }
            public void Add(Totals other) { food += other.food; wood += other.wood; metal += other.metal; stone += other.stone; }
            public void Subtract(Totals other) { food -= other.food; wood -= other.wood; metal -= other.metal; stone -= other.stone; }
            public static Totals Difference(ResourceAmount after, ResourceAmount before) => new Totals
            { food = (long)after.Food - before.Food, wood = (long)after.Wood - before.Wood, metal = (long)after.Metal - before.Metal, stone = (long)after.Stone - before.Stone };
            public static Totals PositiveDifference(ResourceAmount after, ResourceAmount before)
            {
                var result = Difference(after, before);
                result.food = Math.Max(0, result.food); result.wood = Math.Max(0, result.wood);
                result.metal = Math.Max(0, result.metal); result.stone = Math.Max(0, result.stone); return result;
            }
        }

        private sealed class SampleWindow
        {
            private readonly double[] values, scratch;
            private int cursor;
            private double total, cachedP95;
            private bool dirty;
            public int Count { get; private set; }
            public double Mean => Count == 0 ? 0 : total / Count;
            public double P95
            {
                get
                {
                    if (Count == 0) return 0;
                    if (dirty)
                    {
                        Array.Copy(values, scratch, Count); Array.Sort(scratch, 0, Count);
                        cachedP95 = scratch[(int)Math.Ceiling(Count * .95) - 1]; dirty = false;
                    }
                    return cachedP95;
                }
            }
            public SampleWindow(int capacity) { values = new double[capacity]; scratch = new double[capacity]; }
            public void Add(double value)
            {
                if (Count == values.Length) total -= values[cursor]; else Count++;
                values[cursor] = value; total += value; cursor = (cursor + 1) % values.Length; dirty = true;
            }
            public void Clear() { Count = cursor = 0; total = cachedP95 = 0; dirty = false; Array.Clear(values, 0, values.Length); }
        }

        /// <summary>Bounded, never-overwritten sample set for whole-session smoothness reporting (offline probes,
        /// product smoke). Distinct from <see cref="SampleWindow"/>, which intentionally forgets old samples for a
        /// responsive live HUD reading.</summary>
        private sealed class SessionSamples
        {
            private readonly double[] values, scratch;
            private double sum, cachedP50, cachedP95, cachedP99, cachedP999, cachedMax, cachedStdDev, cachedHitchThreshold;
            private int cachedHitchCount;
            private bool dirty;
            public int Count { get; private set; }
            public bool Overflow { get; private set; }
            public double Mean => Count == 0 ? 0 : sum / Count;
            public double P50 { get { Recompute(); return cachedP50; } }
            public double P95 { get { Recompute(); return cachedP95; } }
            public double P99 { get { Recompute(); return cachedP99; } }
            public double P999 { get { Recompute(); return cachedP999; } }
            public double Max { get { Recompute(); return cachedMax; } }
            public double StdDev { get { Recompute(); return cachedStdDev; } }
            public double HitchThreshold { get { Recompute(); return cachedHitchThreshold; } }
            public int HitchCount { get { Recompute(); return cachedHitchCount; } }
            public SessionSamples(int capacity) { values = new double[capacity]; scratch = new double[capacity]; }
            public void Add(double value) { if (Count == values.Length) { Overflow = true; return; } values[Count++] = value; sum += value; dirty = true; }
            public void Clear() { Count = 0; sum = 0; Overflow = false; dirty = true; }
            private void Recompute()
            {
                if (!dirty) return;
                dirty = false;
                if (Count == 0) { cachedP50 = cachedP95 = cachedP99 = cachedP999 = cachedMax = cachedStdDev = cachedHitchThreshold = 0; cachedHitchCount = 0; return; }
                Array.Copy(values, scratch, Count); Array.Sort(scratch, 0, Count);
                cachedP50 = At(.5); cachedP95 = At(.95); cachedP99 = At(.99); cachedP999 = At(.999); cachedMax = scratch[Count - 1];
                double mean = Mean, variance = 0;
                for (int i = 0; i < Count; i++) { double d = scratch[i] - mean; variance += d * d; }
                cachedStdDev = Math.Sqrt(variance / Count);
                // A hitch is a sampled frame more than twice the session median and over one 30 fps frame (33 ms).
                cachedHitchThreshold = Math.Max(2 * cachedP50, 33.0);
                int hitches = 0; for (int i = 0; i < Count; i++) if (scratch[i] > cachedHitchThreshold) hitches++;
                cachedHitchCount = hitches;
            }
            private double At(double fraction) => scratch[Math.Max(0, Math.Min(Count - 1, (int)Math.Ceiling(Count * fraction) - 1))];
        }
    }
}
