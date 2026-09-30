using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.PlayMode
{
    public sealed class AlphaMatchMetricsTests
    {
        private static World LocalWorld() => DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
        private static void Tick(World world, AlphaMatchMetrics metrics, double cpuMilliseconds = .25)
        { metrics.BeginTick(world); world.Tick(); metrics.ObserveTick(world, cpuMilliseconds); }

        [Test]
        public void InitialStockAndOpponentsAreExcludedFromLocalMetrics()
        {
            var world = LocalWorld(); var metrics = new AlphaMatchMetrics(world);
            Assert.AreEqual(4, metrics.Workers); Assert.AreEqual(4, metrics.IdleWorkers); Assert.AreEqual(0, metrics.Army);
            Assert.AreEqual(1, metrics.Producers); Assert.AreEqual(0, metrics.BusyProducers);
            Assert.AreEqual(0, metrics.Delivered(ResourceKind.Food)); Assert.AreEqual(0, metrics.AcceptedOrders);
            Assert.IsTrue(metrics.HasExactIncome); Assert.IsFalse(metrics.HasLocalTickTiming);
            Assert.AreEqual(0, metrics.SessionSeconds);
        }

        [Test]
        public void PaidOrdersCountOnceWhileSpendingIsExcludedFromDeliveredIncome()
        {
            var world = LocalWorld(); var metrics = new AlphaMatchMetrics(world);
            var train = new TrainCommand(1, 100, "tender"); var accepted = world.Submit(train);
            Assert.IsTrue(accepted.Accepted); metrics.ObserveCommand(train, accepted);
            var denied = new TrainCommand(1, 101, "tender"); metrics.ObserveCommand(denied, world.Submit(denied));
            var opponent = new TrainCommand(2, 101, "tender"); metrics.ObserveCommand(opponent, world.Submit(opponent));
            Tick(world, metrics);
            Assert.AreEqual(1, metrics.AcceptedOrders); Assert.AreEqual(1, metrics.BusyProducers);
            Assert.AreEqual(1, metrics.ProductionUtilization); Assert.AreEqual(1, metrics.PopulationReserved);
            Assert.AreEqual(0, metrics.Delivered(ResourceKind.Food)); Assert.AreEqual(0, metrics.IncomePerMinute(ResourceKind.Food));
            Assert.Less(metrics.NetPerMinute(ResourceKind.Food), 0);
            Assert.IsTrue(metrics.HasLocalTickTiming); Assert.AreEqual(.25, metrics.TickP95Milliseconds);
        }

        [Test]
        public void IncomeRequiresActualCargoDeliveryAndRollingRatesExpire()
        {
            var world = LocalWorld(); var metrics = new AlphaMatchMetrics(world);
            world.TryGetPlayer(1, out var player); int initialFood = player.Resources.Food;
            var gather = new GatherCommand(1, new[] { 1 }, 200); var result = world.Submit(gather);
            Assert.IsTrue(result.Accepted); metrics.ObserveCommand(gather, result);
            bool sawUncreditedCargo = false;
            for (int i = 0; i < 4000 && player.Resources.Food == initialFood; i++)
            {
                Tick(world, metrics);
                world.TryGetUnit(1, out var worker);
                if (worker.CarriedAmount > 0 && player.Resources.Food == initialFood)
                { sawUncreditedCargo = true; Assert.AreEqual(0, metrics.Delivered(ResourceKind.Food)); }
            }
            Assert.IsTrue(sawUncreditedCargo); Assert.Greater(player.Resources.Food, initialFood);
            long delivery = player.Resources.Food - initialFood;
            Assert.AreEqual(delivery, metrics.Delivered(ResourceKind.Food)); Assert.Greater(metrics.IncomePerMinute(ResourceKind.Food), 0);
            var stop = new StopCommand(1, new[] { 1 }); metrics.ObserveCommand(stop, world.Submit(stop));
            for (int i = 0; i <= World.TickRate * 60; i++) Tick(world, metrics);
            Assert.AreEqual(delivery, metrics.Delivered(ResourceKind.Food)); Assert.AreEqual(0, metrics.IncomePerMinute(ResourceKind.Food));
            Assert.AreEqual(0, metrics.OrdersPerMinute); Assert.AreEqual(2, metrics.AcceptedOrders);
        }

        [Test]
        public void DuplicateObservationDoesNotDoubleCountTimeOrCpuAndMissingTicksDisableExactIncome()
        {
            var world = LocalWorld(); var metrics = new AlphaMatchMetrics(world);
            Tick(world, metrics, 2); metrics.ObserveTick(world, 200);
            Assert.AreEqual(1, metrics.TickSamples); Assert.AreEqual(2, metrics.TickP95Milliseconds);
            Assert.AreEqual(.05, metrics.SessionSeconds, .00001);
            world.Tick(); metrics.ObserveTick(world);
            Assert.IsFalse(metrics.HasExactIncome); Assert.AreEqual(0, metrics.IncomePerMinute(ResourceKind.Food));
        }

        [Test]
        public void FrameWindowIsBoundedAndIncludesVisibleStallsButExcludesInactiveFrames()
        {
            var metrics = new AlphaMatchMetrics(LocalWorld());
            for (int i = 0; i < 100; i++) metrics.ObserveFrame(.01, true);
            metrics.ObserveFrame(2, false); metrics.ObserveFrame(double.NaN, true); metrics.ObserveFrame(-1, true);
            Assert.AreEqual(100, metrics.FrameSamples); Assert.AreEqual(100, metrics.FramesPerSecond, .0001);
            Assert.AreEqual(10, metrics.FrameP95Milliseconds, .0001);
            for (int i = 0; i < 20; i++) metrics.ObserveFrame(.1, true);
            Assert.AreEqual(100, metrics.FrameP95Milliseconds, .0001);
            for (int i = 0; i < 300; i++) metrics.ObserveFrame(.02, true);
            Assert.AreEqual(300, metrics.FrameSamples); Assert.AreEqual(50, metrics.FramesPerSecond, .0001);
            Assert.AreEqual(20, metrics.FrameP95Milliseconds, .0001);
        }

        [Test]
        public void OnlineStockIsNetOnlyAndOnlyServerAcknowledgementsCountAsOrders()
        {
            var authority = LocalWorld(); var first = NetworkObservation.Export(authority, 2, 1);
            var replica = World.CreateNetworkReplica(authority.Definition, authority.Map, first);
            var metrics = new AlphaMatchMetrics(replica);
            Assert.IsFalse(metrics.HasExactIncome); Assert.AreEqual(4, metrics.Workers); Assert.AreEqual(1, metrics.Producers);
            var localOrder = new StopCommand(1, new[] { 11 });
            metrics.ObserveCommand(localOrder, NetworkCommandResult.Queued());
            Assert.AreEqual(0, metrics.AcceptedOrders);
            metrics.ObserveAcceptedOnlineOrder(); Assert.AreEqual(1, metrics.AcceptedOrders);
            Assert.IsTrue(authority.Submit(new TrainCommand(2, 101, "tender")).Accepted);
            authority.Tick(); var updated = NetworkObservation.Export(authority, 2, 2);
            replica.ApplyNetworkSnapshot(updated);
            metrics.ObserveTick(replica, 99);
            Assert.IsFalse(metrics.HasExactIncome); Assert.IsFalse(metrics.HasLocalTickTiming); Assert.AreEqual(0, metrics.TickSamples);
            Assert.AreEqual(0, metrics.Delivered(ResourceKind.Food)); Assert.Less(metrics.NetPerMinute(ResourceKind.Food), 0);
            Assert.AreEqual(1, metrics.BusyProducers); Assert.AreEqual(1, metrics.PopulationReserved);
        }

        [Test]
        public void ANewWorldResetsTheClientSessionInsteadOfCombiningMatches()
        {
            var first = LocalWorld(); var metrics = new AlphaMatchMetrics(first);
            var command = new StopCommand(1, new[] { 1 }); metrics.ObserveCommand(command, first.Submit(command));
            Tick(first, metrics); metrics.ObserveFrame(.02, true);
            var second = LocalWorld(); metrics.ObserveTick(second);
            Assert.AreEqual(0, metrics.AcceptedOrders); Assert.AreEqual(0, metrics.SessionSeconds);
            Assert.AreEqual(0, metrics.FrameSamples); Assert.AreEqual(0, metrics.TickSamples); Assert.IsTrue(metrics.HasExactIncome);
        }
    }
}
