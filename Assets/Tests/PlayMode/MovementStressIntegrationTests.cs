using System.Collections;
using Emberfield.Diagnostics;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class MovementStressIntegrationTests
    {
        private GameObject root;
        private MatchController match;
        private int originalTargetFrameRate;

        [SetUp]
        public void SaveGlobalSettings() => originalTargetFrameRate = Application.targetFrameRate;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            Application.targetFrameRate = originalTargetFrameRate;
            yield return null;
        }

        [UnityTest]
        public IEnumerator StressSetupUsesRequestedScenarioCountAndCreatesEveryView()
        {
            yield return Create(MovementScenarioKind.WideCorridor, 50);
            Assert.IsNotNull(match.Stress);
            Assert.AreSame(match.World, match.Stress.Scenario.World);
            Assert.AreEqual(MovementScenarioKind.WideCorridor, match.Stress.Scenario.Kind);
            Assert.AreEqual(50, match.Stress.Scenario.RequestedUnits);
            Assert.AreEqual(50, match.World.Units.Count);
            Assert.AreEqual(50, match.View.Count);
            Assert.IsFalse(match.Stress.Running);
            Assert.IsFalse(match.Stress.Scenario.OrdersIssued);
            Assert.IsTrue(match.World.NavigationMetrics.Enabled);
            foreach (var unit in match.World.Units)
            {
                Assert.AreEqual(UnitOrder.Idle, unit.Order);
                Assert.IsNotNull(match.View.RootFor(unit.Id));
            }
        }

        [UnityTest]
        public IEnumerator RunOrdersAndTickWrapperReachObservedIdleGoalsWithMeasurements()
        {
            yield return Create(MovementScenarioKind.OpenField, 8);
            match.StartStress();
            Assert.IsTrue(match.Stress.Running);
            Assert.IsTrue(match.Stress.Scenario.OrdersIssued);
            Assert.AreEqual(8, match.Stress.Scenario.ExpectedDestinations.Count);
            var first = match.World.Units[0];
            var start = first.Position;
            // Synthetic known frame durations check summary wiring; they are not performance measurements.
            for (int frame = 0; frame < 240; frame++) match.Stress.OnFrame(1f / 60);
            for (int tick = 0; tick < World.TickRate * 120 && match.Stress.Running; tick++)
                match.AdvanceSimulationTick();
            match.SyncPresentation(1);
            Assert.IsFalse(match.Stress.Running);
            Assert.AreNotEqual(start, first.Position);
            Assert.AreEqual(8, match.Stress.Observer.Current.Arrived);
            Assert.AreEqual(0, match.Stress.Observer.Current.Stalled);
            Assert.AreEqual(0, match.Stress.Observer.Current.MaxOverlapPairs);
            Assert.AreEqual(0, match.Stress.Observer.Current.MaxInvalidPositions);
            foreach (var goal in match.Stress.Scenario.ExpectedDestinations)
            {
                Assert.IsTrue(match.World.TryGetUnit(goal.Key, out var unit));
                Assert.AreEqual(goal.Value, unit.Position, "The scene must finish at the assigned goal.");
                Assert.AreEqual(UnitOrder.Idle, unit.Order, "The observer must not finish while a unit is still moving.");
                var view = match.View.RootFor(unit.Id);
                Assert.That(Vector3.Distance(view.position, DefinitionLoader.ToWorld(unit.Position)), Is.LessThan(.001f));
            }
            var summary = match.Stress.Summarize();
            Assert.That(summary.FrameSamples, Is.GreaterThan(0));
            Assert.That(summary.TickSamples, Is.GreaterThan(0));
            Assert.That(summary.FrameP50Milliseconds, Is.EqualTo(1000.0 / 60).Within(.001));
            Assert.That(summary.TickMaxMilliseconds, Is.GreaterThanOrEqualTo(summary.TickP95Milliseconds));
            Assert.That(summary.CommandMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.NavigationMaxMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(match.World.NavigationMetrics.Snapshot.TotalQueryCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TickWrapperAppliesScheduledFoundationAndSynchronizesItsView()
        {
            yield return Create(MovementScenarioKind.DynamicObstacle, 8);
            match.StartStress();
            Assert.IsTrue(match.Stress.Running);
            Assert.IsFalse(match.Stress.Scenario.DynamicBuildResult.HasValue);
            for (int tick = 0; tick < 101; tick++) match.AdvanceSimulationTick();
            Assert.IsTrue(match.Stress.Scenario.DynamicBuildResult.HasValue, "AdvanceSimulationTick must execute scheduled scenario events.");
            var result = match.Stress.Scenario.DynamicBuildResult.Value;
            Assert.IsTrue(result.Accepted, result.Message);
            Assert.IsTrue(match.World.TryGetBuilding(result.EntityId, out var barrier));
            Assert.IsFalse(match.World.IsWalkable(barrier.Position));
            match.SyncPresentation(1);
            Assert.IsNotNull(match.View.RootFor(barrier.Id));
            Assert.AreEqual(match.World.Units.Count + match.World.Buildings.Count, match.View.Count);
            Assert.That(match.Stress.Observer.Current.ElapsedTicks, Is.GreaterThanOrEqualTo(100));
            Assert.AreEqual(0, match.Stress.Observer.Current.MaxInvalidPositions);
        }

        private IEnumerator Create(MovementScenarioKind scenario, int count)
        {
            root = new GameObject("Movement stress integration match");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.PerformanceStress = true;
            match.PerformanceUnitCount = count;
            match.PerformanceScenario = scenario;
            match.enabled = false;
            root.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            match.SyncPresentation(1);
        }
    }
}
