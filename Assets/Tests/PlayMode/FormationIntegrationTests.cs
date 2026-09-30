using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class FormationIntegrationTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectedFormationFlowsThroughTerrainTapIntoUnitOrders()
        {
            root = new GameObject("Formation integration match");
            root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            match.enabled = false;
            root.SetActive(true);
            yield return null;
            match.SyncPresentation(1);
            Assert.AreEqual(MovementFormation.Loose, match.Formation);
            match.Select(new[] { 1, 2 });
            match.CycleFormation();
            Assert.AreEqual(MovementFormation.Line, match.Formation);
            var terrain = new SimPoint(18500, 19500);
            Vector2 screenPoint = match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(terrain));
            match.Tap(screenPoint, false);
            Assert.IsTrue(match.World.TryGetUnit(1, out var first));
            Assert.IsTrue(match.World.TryGetUnit(2, out var second));
            Assert.AreEqual(MovementFormation.Line, first.Formation);
            Assert.AreEqual(MovementFormation.Line, second.Formation);
            Assert.AreEqual(first.Destination.X, second.Destination.X, "Eastward Line order must form a lateral row.");
            Assert.AreNotEqual(first.Destination.Z, second.Destination.Z);
            var firstGoal = first.Destination; var secondGoal = second.Destination;
            for (int tick = 0; tick < World.TickRate * 120 && (first.Order != UnitOrder.Idle || second.Order != UnitOrder.Idle); tick++)
                match.AdvanceSimulationTick();
            match.SyncPresentation(1);
            Assert.AreEqual(firstGoal, first.Position); Assert.AreEqual(secondGoal, second.Position);
            Assert.AreEqual(UnitOrder.Idle, first.Order); Assert.AreEqual(UnitOrder.Idle, second.Order);
            CollectionAssert.AreEqual(new[] { 1, 2 }, match.Selection);
            match.CycleFormation(); Assert.AreEqual(MovementFormation.Box, match.Formation);
            match.CycleFormation(); Assert.AreEqual(MovementFormation.Loose, match.Formation);
        }
    }
}
