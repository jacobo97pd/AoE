using System;
using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class TechnologyIntegrationTests
    {
        private GameObject root;
        private MatchController match;
        private PlayerState player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Technology integration match");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.enabled = false;
            root.SetActive(true);
            yield return null;
            Assert.IsTrue(match.World.TryGetPlayer(MatchController.LocalPlayer, out player));
            Assert.AreEqual("settlement", player.EraId);
            RefreshPresentation();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PanelExplainsLocksInterceptsRaycastsAndBlocksWorldTapsUntilClosed()
        {
            match.Select(new[] { 1 });
            Assert.IsTrue(match.World.TryGetUnit(1, out var first));
            Assert.IsTrue(match.World.TryGetUnit(2, out var second));
            var start = first.Position;
            match.Research.Open();
            RefreshPresentation();
            yield return null; // Register the newly visible graphics with the real canvas raycaster.
            RefreshPresentation();
            var panel = match.Hud.ResearchPanel;
            Assert.IsTrue(panel.IsVisible);
            var kingdom = match.Research.Technology("advance_kingdom");
            var kingdomButton = panel.ButtonFor(kingdom.Id);
            var viewport = panel.Root.Find("Research viewport").GetComponent<RectTransform>();
            var content = viewport.Find("Technology cards").GetComponent<RectTransform>();
            Assert.That(content.rect.width, Is.EqualTo(viewport.rect.width).Within(.1f),
                "Scrollable cards must fit the viewport horizontally instead of clipping their first column.");
            Assert.IsFalse(kingdomButton.interactable);
            Assert.AreEqual(CommandRejection.InsufficientResources, match.Research.Validate(kingdom.Id).Reason);
            Assert.AreEqual(match.Research.Validate(kingdom.Id).Message, panel.StatusFor(kingdom.Id));
            StringAssert.Contains(EconomyControls.CostText(kingdom.Cost), CardText(kingdom.Id, "Cost and time"));
            Assert.IsFalse(panel.ButtonFor("advance_dominion").interactable);
            StringAssert.Contains("Kingdom", CardText("advance_dominion", "Requirements"));
            StringAssert.Contains("Archive", CardText("advance_dominion", "Requirements"));
            StringAssert.Contains("Work Rotation", CardText("advance_empire", "Requirements"));

            var pointer = new PointerEventData(EventSystem.current) { position = EntityPoint(second.Position) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "The open research panel must participate in actual UI raycasts.");
            var front = hits[0].gameObject.transform;
            Assert.IsTrue(front == panel.Root || front.IsChildOf(panel.Root), "The research panel must be the frontmost raycast target.");
            match.Tap(pointer.position, false);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(new Vector3(18.5f, 0, 19.5f)), false);
            match.World.Tick();
            CollectionAssert.AreEqual(new[] { first.Id }, match.Selection);
            Assert.AreEqual(start, first.Position);
            Assert.AreEqual(UnitOrder.Idle, first.Order);

            var close = panel.Root.Find("Close research").GetComponent<Button>();
            Click(close);
            RefreshPresentation();
            Assert.IsFalse(match.Research.IsOpen);
            Assert.IsFalse(panel.IsVisible);
            match.Tap(EntityPoint(second.Position), false);
            CollectionAssert.AreEqual(new[] { second.Id }, match.Selection);
        }

        [UnityTest]
        public IEnumerator ActualResearchButtonChargesOnceAndCompletesOnTheConfiguredTick()
        {
            AssignGatherer(1, 200);
            AssignGatherer(2, 201);
            var technology = match.Research.Technology("advance_kingdom");
            WaitForCost(technology.Cost);
            match.Select(new[] { 1, 2 });
            match.StopSelected(); // Keep stock fixed while checking exact research payment and timing.
            var before = player.Resources;
            var archive = match.Economy.BuildingDefinition("archive");
            Assert.IsFalse(match.World.ValidateRequirements(1, archive.RequiredEraId, archive.RequiredTechnologyIds).Accepted);
            match.Research.Open();
            RefreshPresentation();
            yield return null;
            RefreshPresentation();
            var panel = match.Hud.ResearchPanel;
            var button = panel.ButtonFor(technology.Id);
            Assert.IsTrue(button.interactable);
            var producer = match.Research.ProducerFor(technology);
            Assert.IsNotNull(producer);
            Click(button);
            Assert.IsNotNull(producer.ActiveResearch);
            Assert.AreEqual(technology.Id, producer.ActiveResearch.TechnologyId);
            Assert.AreEqual(technology.ResearchTicks, producer.ActiveResearch.RemainingTicks);
            var paid = new ResourceAmount(before.Food - technology.Cost.Food, before.Wood - technology.Cost.Wood,
                before.Metal - technology.Cost.Metal, before.Stone - technology.Cost.Stone);
            Assert.AreEqual(paid, player.Resources);
            // The same still-enabled widget receives a second click before the next HUD refresh.
            Click(button);
            Assert.AreEqual(paid, player.Resources, "A duplicate UI event must not charge a second research cost.");
            Assert.AreEqual(CommandRejection.ResearchInProgress, match.Research.Validate(technology.Id).Reason);
            Assert.AreEqual(technology.ResearchTicks, producer.ActiveResearch.RemainingTicks);
            RefreshPresentation();
            Assert.IsFalse(button.interactable);
            StringAssert.Contains("Researching", panel.StatusFor(technology.Id));

            for (int tick = 0; tick < technology.ResearchTicks - 1; tick++) match.World.Tick();
            Assert.AreEqual("settlement", player.EraId);
            Assert.IsFalse(player.HasTechnology(technology.Id));
            Assert.AreEqual(1, producer.ActiveResearch.RemainingTicks);
            match.World.Tick();
            RefreshPresentation();
            Assert.AreEqual("kingdom", player.EraId);
            Assert.AreEqual(2, player.EraTier);
            Assert.IsTrue(player.HasTechnology(technology.Id));
            Assert.IsNull(producer.ActiveResearch);
            Assert.AreEqual(paid, player.Resources);
            Assert.IsTrue(match.World.ValidateRequirements(1, archive.RequiredEraId, archive.RequiredTechnologyIds).Accepted);
            Assert.AreEqual("Completed", panel.StatusFor(technology.Id));
            StringAssert.Contains("Kingdom", panel.Root.Find("Research heading").GetComponent<Text>().text);
        }

        [Test]
        public void GatherConstructionAndResearchAdaptersReachAllFourErasUsingShippedCosts()
        {
            var initial = player.Resources;
            var earlyEmpire = match.Research.Start("advance_empire");
            Assert.IsFalse(earlyEmpire.Accepted);
            Assert.AreEqual(CommandRejection.PrerequisiteMissing, earlyEmpire.Reason);
            Assert.AreEqual(initial, player.Resources);
            match.Select(new[] { 1 });
            match.Economy.BeginBuild("archive");
            Assert.IsNull(match.Economy.PendingBuildingId, "The placement adapter must refuse Archive construction in Settlement.");

            for (int worker = 1; worker <= 4; worker++) AssignGatherer(worker, 199 + worker);
            ResearchAndComplete("advance_kingdom");
            Assert.AreEqual("kingdom", player.EraId);
            var withoutArchive = match.Research.Start("advance_dominion");
            Assert.IsFalse(withoutArchive.Accepted);
            Assert.AreEqual(CommandRejection.PrerequisiteMissing, withoutArchive.Reason);
            StringAssert.Contains("archive", withoutArchive.Message.ToLowerInvariant());

            WaitForCost(match.Economy.BuildingDefinition("archive").Cost);
            match.Research.Close();
            match.Select(new[] { 1 });
            match.Economy.BeginBuild("archive");
            Assert.AreEqual("archive", match.Economy.PendingBuildingId);
            match.Economy.PreviewAt(new SimPoint(16500, 21500));
            Assert.IsTrue(match.Economy.PlacementResult.Accepted, match.Economy.PlacementResult.Message);
            var placed = match.Economy.ConfirmBuild();
            Assert.IsTrue(placed.Accepted, placed.Message);
            Assert.IsTrue(match.World.TryGetBuilding(placed.EntityId, out var archive));
            Assert.IsFalse(archive.IsComplete);
            var incompleteArchive = match.Research.Start("advance_dominion");
            Assert.IsFalse(incompleteArchive.Accepted);
            Assert.AreEqual(CommandRejection.PrerequisiteMissing, incompleteArchive.Reason);
            AdvanceUntil(() => archive.IsComplete, "The paid Archive foundation must be completed by its assigned Tender.");
            Assert.IsNotNull(match.View.RootFor(archive.Id));
            Assert.AreSame(archive, match.Research.ProducerFor(match.Research.Technology("gather_1")));
            AssignGatherer(1, 200);

            ResearchAndComplete("advance_dominion");
            Assert.AreEqual("dominion", player.EraId);
            var earlyRotation = match.Research.Start("gather_2");
            Assert.IsFalse(earlyRotation.Accepted);
            Assert.AreEqual(CommandRejection.PrerequisiteMissing, earlyRotation.Reason);
            ResearchAndComplete("gather_1");
            ResearchAndComplete("gather_2");
            ResearchAndComplete("advance_empire");
            Assert.AreEqual("empire", player.EraId);
            Assert.AreEqual(4, player.EraTier);
            foreach (string id in new[] { "advance_kingdom", "advance_dominion", "gather_1", "gather_2", "advance_empire" })
            {
                Assert.IsTrue(player.HasTechnology(id));
                Assert.AreEqual("Completed", match.Hud.ResearchPanel.StatusFor(id));
            }
            Assert.AreEqual(5, player.CompletedTechnologyIds.Count);
            Assert.IsTrue(archive.IsComplete);
            StringAssert.Contains("Empire", match.Hud.ResearchPanel.Root.Find("Research heading").GetComponent<Text>().text);
        }

        private void AssignGatherer(int workerId, int resourceId)
        {
            match.Research.Close();
            RefreshPresentation();
            Assert.IsTrue(match.World.TryGetResource(resourceId, out var source));
            match.Select(new[] { workerId });
            match.Tap(EntityPoint(source.Position), false);
            Assert.IsTrue(match.World.TryGetUnit(workerId, out var worker));
            Assert.AreEqual(source.Id, worker.TargetResourceId, "The resource tap must issue an actual gather order.");
        }

        private void ResearchAndComplete(string id)
        {
            var technology = match.Research.Technology(id);
            WaitForCost(technology.Cost);
            match.Research.Open();
            var result = match.Research.Start(id);
            Assert.IsTrue(result.Accepted, id + ": " + result.Message);
            for (int tick = 0; tick < technology.ResearchTicks; tick++) match.World.Tick();
            RefreshPresentation();
            Assert.IsTrue(player.HasTechnology(id), id + " must finish after its configured research work.");
            StringAssert.Contains(technology.DisplayName + " complete", match.Research.Notice,
                "Completion feedback must name the new research even when completed IDs are sorted differently.");
        }

        private void WaitForCost(ResourceAmount cost) => AdvanceUntil(() =>
            player.Resources.Food >= cost.Food && player.Resources.Wood >= cost.Wood &&
            player.Resources.Metal >= cost.Metal && player.Resources.Stone >= cost.Stone,
            "Workers must physically gather and deliver the next configured purchase cost.");

        private void AdvanceUntil(Func<bool> condition, string message)
        {
            const int maximumTicks = 20000;
            for (int tick = 0; tick < maximumTicks && !condition(); tick++) match.World.Tick();
            RefreshPresentation();
            Assert.IsTrue(condition(), message + " Tick " + match.World.TickIndex + ".");
        }

        private void RefreshPresentation()
        {
            match.Hud.Invalidate();
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
        }

        private string CardText(string id, string name)
            => match.Hud.ResearchPanel.ButtonFor(id).transform.parent.Find(name).GetComponent<Text>().text;

        private Vector2 EntityPoint(SimPoint position)
            => match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(position) + Vector3.up * .6f);

        private static void Click(Button button)
        {
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            var rect = button.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            Assert.IsTrue(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler));
        }
    }
}
