using System;
using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class EconomyIntegrationTests
    {
        private GameObject root;
        private MatchController match;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Economy integration match");
            match = root.AddComponent<MatchController>();
            match.enabled = false;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [Test]
        public void WorkerResourceTapGathersAndCreditsInventoryOnlyAfterReturn()
        {
            var worker = match.World.Units[0];
            var resource = match.World.Resources[0];
            Assert.IsTrue(match.World.TryGetPlayer(MatchController.LocalPlayer, out var player));
            int inventoryBefore = player.Resources.Get(resource.Kind);
            int stockBefore = resource.RemainingAmount;

            match.Tap(ScreenPoint(worker.Position), false);
            CollectionAssert.Contains(match.Selection, worker.Id);
            match.Tap(ScreenPoint(resource.Position), false);
            AdvanceUntil(() => worker.CarriedAmount > 0, "The resource tap must start actual worker harvesting.");
            Assert.That(resource.RemainingAmount, Is.LessThan(stockBefore));
            Assert.AreEqual(inventoryBefore, player.Resources.Get(resource.Kind), "Carried stock must not be spendable before drop-off.");

            AdvanceUntil(() => player.Resources.Get(resource.Kind) > inventoryBefore, "The worker must return to a completed owned drop-off.");
            Assert.AreEqual(0, worker.CarriedAmount, "A completed return must deposit the carried stock.");
            Assert.That(Vector3.Distance(match.View.RootFor(worker.Id).position, DefinitionLoader.ToWorld(worker.Position)), Is.LessThan(.001f));
        }

        [Test]
        public void StoppedCarrierTappingOwnedDropOffDeliversThenRemainsIdle()
        {
            var worker = match.World.Units[0];
            var resource = match.World.Resources[0];
            var hearth = match.World.Buildings[0];
            Assert.IsTrue(match.World.TryGetPlayer(MatchController.LocalPlayer, out var player));
            int inventoryBefore = player.Resources.Get(resource.Kind);
            match.Tap(ScreenPoint(worker.Position), false);
            match.Tap(ScreenPoint(resource.Position), false);
            AdvanceUntil(() => worker.CarriedAmount > 0, "The worker must collect physical cargo before it can be stopped.");

            match.StopSelected();
            int cargo = worker.CarriedAmount;
            var stoppedPosition = worker.Position;
            for (int tick = 0; tick < World.TickRate; tick++) match.World.Tick();
            Assert.AreEqual(stoppedPosition, worker.Position);
            Assert.AreEqual(cargo, worker.CarriedAmount);
            Assert.AreEqual(WorkerTask.None, worker.WorkerTask);
            Assert.AreEqual(inventoryBefore, player.Resources.Get(resource.Kind));

            match.Tap(ScreenPoint(hearth.Position), false);
            CollectionAssert.AreEqual(new[] { worker.Id }, match.Selection, "A drop-off tap with cargo must preserve the worker selection.");
            Assert.AreEqual(WorkerTask.ReturningResources, worker.WorkerTask);
            Assert.AreEqual(inventoryBefore, player.Resources.Get(resource.Kind), "The tap must order travel rather than instantly credit cargo.");
            AdvanceUntil(() => worker.CarriedAmount == 0 && worker.WorkerTask == WorkerTask.None, "The stopped carrier must physically deliver its cargo and finish idle.");
            Assert.AreNotEqual(stoppedPosition, worker.Position);
            Assert.AreEqual(inventoryBefore + cargo, player.Resources.Get(resource.Kind));
            Assert.AreEqual(UnitOrder.Idle, worker.Order);

            var deliveredPosition = worker.Position;
            for (int tick = 0; tick < World.TickRate; tick++) match.World.Tick();
            Assert.AreEqual(deliveredPosition, worker.Position, "Explicit delivery must not silently resume the old harvesting assignment.");
            Assert.AreEqual(inventoryBefore + cargo, player.Resources.Get(resource.Kind));
            CollectionAssert.AreEqual(new[] { worker.Id }, match.Selection);
        }

        [Test]
        public void PlacementConstructionAndTrainingProduceASelectableSoldierView()
        {
            var worker = match.World.Units[0];
            match.Select(new[] { worker.Id });
            Assert.IsTrue(match.World.TryGetPlayer(MatchController.LocalPlayer, out var player));
            int unitsBefore = match.World.Units.Count;
            int populationBefore = player.PopulationUsed;
            int requiredWood = 0;
            foreach (var definition in match.World.Definition.Buildings)
                if (definition.Id == "muster_hall") requiredWood += definition.Cost.Wood;
            foreach (var definition in match.World.Definition.Units)
                if (definition.Id == "reedguard") requiredWood += definition.Cost.Wood;
            ResourceNodeState wood = null;
            foreach (var resource in match.World.Resources)
                if (resource.Kind == ResourceKind.Wood) { wood = resource; break; }
            Assert.IsNotNull(wood);
            Assert.That(player.Resources.Wood, Is.LessThan(requiredWood), "The authored opening should require gathering before the full purchase sequence.");
            match.Tap(ScreenPoint(wood.Position), false);
            AdvanceUntil(() => player.Resources.Wood >= requiredWood, "Gather and deposit enough wood for the Muster Hall and Reedguard.");

            match.Economy.BeginBuild("muster_hall");
            match.Economy.PreviewAt(new SimPoint(16500, 21500));
            Assert.IsTrue(match.Economy.HasPreview);
            Assert.IsTrue(match.Economy.PlacementResult.Accepted, match.Economy.PlacementResult.Message);
            var placed = match.Economy.ConfirmBuild();
            Assert.IsTrue(placed.Accepted, placed.Message);
            Assert.IsTrue(match.World.TryGetBuilding(placed.EntityId, out var building));
            Assert.IsFalse(building.IsComplete, "Placement must create a construction site before granting production.");
            Assert.IsNull(match.Economy.PendingBuildingId);
            match.View.Sync(1, match.Selection);
            Assert.IsNotNull(match.View.RootFor(building.Id), "The construction site must have a presentation object.");

            AdvanceUntil(() => building.IsComplete, "The assigned worker must complete the placed Muster Hall.");
            match.ClearSelection();
            match.Tap(ScreenPoint(building.Position), false);
            CollectionAssert.Contains(match.Selection, building.Id);
            var trained = match.Economy.Train("reedguard");
            Assert.IsTrue(trained.Accepted, trained.Message);
            Assert.That(building.ProductionQueue.Count, Is.EqualTo(1));
            Assert.That(player.PopulationReserved, Is.GreaterThan(0));

            AdvanceUntil(() => match.World.Units.Count > unitsBefore, "The selected producer must finish its training queue.");
            UnitState soldier = null;
            foreach (var unit in match.World.Units)
                if (unit.OwnerId == MatchController.LocalPlayer && unit.DefinitionId == "reedguard") soldier = unit;
            Assert.IsNotNull(soldier);
            Assert.That(player.PopulationUsed, Is.GreaterThan(populationBefore));
            Assert.AreEqual(0, player.PopulationReserved);
            Assert.IsEmpty(building.ProductionQueue);
            Assert.IsNotNull(match.View.RootFor(soldier.Id), "A trained simulation unit must receive a visible entity view.");
            match.Tap(ScreenPoint(soldier.Position), false);
            CollectionAssert.Contains(match.Selection, soldier.Id);
        }

        [Test]
        public void InvalidPlacementAndCancelDoNotSpendOrCreateBuildings()
        {
            var worker = match.World.Units[0];
            match.Select(new[] { worker.Id });
            Assert.IsTrue(match.World.TryGetPlayer(MatchController.LocalPlayer, out var player));
            var resourcesBefore = player.Resources;
            int buildingsBefore = match.World.Buildings.Count;

            match.Economy.BeginBuild("shelter");
            match.Economy.PreviewAt(new SimPoint(16000, 22000));
            Assert.IsTrue(match.Economy.PlacementResult.Accepted, "The same affordable building must first validate on clear terrain.");
            match.Economy.PreviewAt(match.World.Buildings[0].Position);
            Assert.IsFalse(match.Economy.PlacementResult.Accepted, "The existing Hearth must invalidate an overlapping footprint.");
            var rejected = match.Economy.ConfirmBuild();
            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual(buildingsBefore, match.World.Buildings.Count);
            Assert.AreEqual(resourcesBefore, player.Resources);

            match.Economy.CancelBuild();
            Assert.IsNull(match.Economy.PendingBuildingId);
            Assert.IsFalse(match.Economy.HasPreview);
            match.World.Tick();
            Assert.AreEqual(buildingsBefore, match.World.Buildings.Count);
            Assert.AreEqual(resourcesBefore, player.Resources);
        }

        private Vector2 ScreenPoint(SimPoint position)
            => match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(position) + Vector3.up * .6f);

        private void AdvanceUntil(Func<bool> condition, string failure)
        {
            // Exercise the shipped balance data without making a test wait minutes in real time.
            const int maximumTicks = World.TickRate * 300;
            for (int tick = 0; tick < maximumTicks && !condition(); tick++) match.World.Tick();
            match.View.Sync(1, match.Selection);
            Assert.IsTrue(condition(), failure);
        }
    }
}
