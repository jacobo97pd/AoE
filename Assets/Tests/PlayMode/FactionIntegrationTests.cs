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
    public class FactionIntegrationTests
    {
        private GameObject root;
        private MatchController match;
        private PlayerState player;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothFactionAssignmentsHaveEqualStartsAndExposeOnlyTheirOwnRecruitAndResearch()
        {
            foreach (string factionId in new[] { "aven", "serevin" })
            {
                // Exercise the authored loader separately as well as the controller's startup adapter.
                var loaded = DefinitionLoader.CreateFactionWorld(factionId);
                AssertEqualStart(loaded, factionId);
                CreateMatch(factionId);
                yield return null;
                Refresh();
                AssertEqualStart(match.World, factionId);
                Assert.IsTrue(match.IsFactionDrill);
                string ownRole = factionId == "aven" ? "threadkeeper" : "ashrunner";
                string ownName = factionId == "aven" ? "Threadkeeper" : "Ashrunner";
                string enemyName = factionId == "aven" ? "Ashrunner" : "Threadkeeper";
                string ownTech = factionId == "aven" ? "aven_stores" : "serevin_camps";
                string enemyTech = factionId == "aven" ? "serevin_camps" : "aven_stores";

                match.Select(new[] { 102 });
                Refresh();
                var recruit = FindButton("Train " + ownName);
                Assert.IsNotNull(recruit);
                Assert.IsNull(FindButton("Train " + enemyName));
                var before = player.Resources;
                var definition = match.Economy.UnitDefinition(ownRole);
                Click(recruit);
                Assert.IsTrue(match.World.TryGetBuilding(102, out var hall));
                Assert.AreEqual(1, hall.ProductionQueue.Count);
                Assert.AreEqual(ownRole, hall.ProductionQueue[0].UnitDefinitionId);
                Assert.AreEqual(Subtract(before, definition.Cost), player.Resources);
                StringAssert.Contains(ownName + " queued", match.Feedback);
                AdvanceUntil(() => FindUnit(1, ownRole) != null, "The actual faction recruit button must produce its unit.");
                var unit = FindUnit(1, ownRole);
                Assert.IsNotNull(match.View.RootFor(unit.Id));
                Assert.AreEqual(5, player.PopulationUsed);

                if (factionId == "serevin")
                {
                    match.Select(new[] { unit.Id });
                    Refresh();
                    Click(FindButton("Reposition"));
                    Assert.AreEqual(match.Factions.LocalDefinition.RepositionTicks, unit.RepositionRemainingTicks);
                    Assert.AreEqual(RepositionSpeed(definition), unit.MoveSpeedMillimetresPerSecond);
                    StringAssert.Contains("cannot attack", match.Feedback);
                }

                match.Research.Open();
                Refresh();
                Assert.IsNotNull(match.Hud.ResearchPanel.ButtonFor(ownTech));
                Assert.IsNull(match.Hud.ResearchPanel.ButtonFor(enemyTech));
                Assert.IsFalse(match.Hud.ResearchPanel.ButtonFor(ownTech).interactable);
                Assert.AreEqual(CommandRejection.UnknownBuilding, match.Research.Validate(ownTech).Reason);
                StringAssert.StartsWith("Build", match.Hud.ResearchPanel.StatusFor(ownTech));
                StringAssert.Contains("Archive", match.Hud.ResearchPanel.StatusFor(ownTech));
                var technology = match.Research.Technology(ownTech);
                Assert.AreEqual(CommandRejection.PrerequisiteMissing,
                    match.World.ValidateRequirements(1, technology.RequiredEraId, technology.RequiredTechnologyIds).Reason);
                StringAssert.Contains("Kingdom", match.Hud.ResearchPanel.ButtonFor(ownTech).transform.parent.Find("Requirements").GetComponent<Text>().text);
                Object.Destroy(root);
                root = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FactionChooserInterceptsPointersAndBlocksWorldCommandsUntilReturnIsClicked()
        {
            CreateMatch("aven");
            yield return null;
            match.Select(new[] { 1 });
            Assert.IsTrue(match.World.TryGetUnit(1, out var first));
            Assert.IsTrue(match.World.TryGetUnit(2, out var second));
            var before = first.Position;
            // The command bar carries orders only; the faction chooser is opened from the match menu.
            match.OfflineControls.Open();
            Refresh();
            Click(FindButton("Factions"));
            Refresh();
            // Opening that menu clears the selection, so the chooser's own guarantee is what this measures.
            match.Select(new[] { first.Id });
            yield return null; // Let the real GraphicRaycaster register the newly visible modal.
            Refresh();
            var panel = match.Hud.FactionPanel;
            Assert.IsTrue(panel.IsVisible);
            var returnButton = panel.Root.Find("Return to map").GetComponent<Button>();
            AssertFrontmost(returnButton);
            var panelPointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(panelPointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(panel.Root) || hits[0].gameObject.transform == panel.Root);

            match.Tap(EntityPoint(second.Position), false);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(new Vector3(25.5f, 0, 18.5f)), false);
            match.World.Tick();
            CollectionAssert.AreEqual(new[] { first.Id }, match.Selection);
            Assert.AreEqual(before, first.Position);
            Assert.AreEqual(UnitOrder.Idle, first.Order);
            Click(returnButton);
            Refresh();
            Assert.IsFalse(match.Factions.IsOpen);
            Assert.IsFalse(panel.IsVisible);
            match.Tap(EntityPoint(second.Position), false);
            CollectionAssert.AreEqual(new[] { second.Id }, match.Selection);
        }

        // Serevin's cavalry passive plus Reposition, computed from the shipped data as FactionSystem does.
        private int RepositionSpeed(UnitDefinition definition)
        {
            var serevin = match.Factions.LocalDefinition;
            return (int)((long)definition.MoveSpeedMillimetresPerSecond * (1000 + serevin.CavalrySpeedBonusPermille + serevin.RepositionSpeedBonusPermille) / 1000);
        }

        [UnityTest]
        public IEnumerator PaidLogisticsButtonCompletesExactlyAndThreadkeeperButtonExtendsVisibleCoverage()
        {
            CreateMatch("aven");
            yield return null;
            Gather(1, 202);
            var faction = match.Factions.LocalDefinition;
            AdvanceUntil(() => player.Resources.Metal >= faction.CharterCost.Metal,
                "A Tender must physically deliver the charter's Metal cost.");
            match.Select(new[] { 1 });
            match.StopSelected();
            Assert.IsTrue(match.World.TryGetUnit(4, out var nearbyWorker));
            // Carry values follow the shipped data: the worker's base capacity plus Aven's passive,
            // and the Logistics charter on top.
            var workerDefinition = System.Array.Find(match.World.Definition.Units, unit => unit.Id == nearbyWorker.DefinitionId);
            int carried = workerDefinition.CarryCapacity + faction.WorkerCarryBonus, chartered = carried + faction.LogisticsCarryBonus;
            Assert.AreEqual(carried, nearbyWorker.CarryCapacity);
            Assert.IsTrue(match.World.TryGetBuilding(104, out var yard));
            match.Select(new[] { yard.Id });
            Refresh();
            var before = player.Resources;
            var charter = FindButton("Logistics charter");
            Click(charter);
            Assert.AreEqual(Subtract(before, faction.CharterCost), player.Resources);
            Assert.AreEqual(faction.CharterTicks, yard.CharterRemainingTicks);
            Assert.AreEqual(StoreyardCharter.None, yard.ActiveCharter);
            StringAssert.Contains("Logistics charter change started", match.Feedback);
            Click(charter); // A second event before HUD refresh must not charge the busy yard again.
            Assert.AreEqual(Subtract(before, faction.CharterCost), player.Resources);
            for (int tick = 0; tick < faction.CharterTicks - 1; tick++) match.World.Tick();
            Assert.AreEqual(1, yard.CharterRemainingTicks);
            Assert.AreEqual(carried, nearbyWorker.CarryCapacity);
            match.World.Tick();
            Refresh();
            Assert.AreEqual(StoreyardCharter.Logistics, yard.ActiveCharter);
            Assert.AreEqual(chartered, nearbyWorker.CarryCapacity);
            Assert.AreEqual(yard.Id, nearbyWorker.CharterSourceId);
            Assert.IsTrue(match.View.RootFor(yard.Id).Find("Selected supply reach").gameObject.activeSelf);

            match.Select(new[] { 102 });
            Refresh();
            Click(FindButton("Train Threadkeeper"));
            AdvanceUntil(() => FindUnit(1, "threadkeeper") != null, "The paid support recruit must finish training.");
            var relay = FindUnit(1, "threadkeeper");
            Assert.IsTrue(match.World.TryGetUnit(1, out var outsideWorker));
            Move(relay, new SimPoint(26500, 21500));
            Move(outsideWorker, new SimPoint(29500, 19500));
            AdvanceUntil(() => relay.Order == UnitOrder.Idle && outsideWorker.Order == UnitOrder.Idle,
                "The support and test worker must reach clear positions on the network edge.");
            Assert.AreEqual(carried, outsideWorker.CarryCapacity, "This worker is outside the original yard radius.");
            match.Select(new[] { relay.Id });
            Refresh();
            var position = relay.Position;
            Click(FindButton("Deploy relay"));
            Refresh();
            Assert.AreEqual(faction.ThreadkeeperDeployTicks, relay.ThreadkeeperRemainingTicks);
            Assert.AreEqual(yard.Id, relay.LinkedStoreyardId);
            Assert.AreEqual(chartered, outsideWorker.CarryCapacity, "The deployed relay must supply the worker beyond direct yard coverage.");
            AssertAlphaModel(match.View.RootFor(relay.Id), FactionKind.AvenCompact, "threadkeeper");
            Assert.IsNotNull(match.View.RootFor(relay.Id).Find("Model/Active faction signal").GetComponent<Renderer>());
            Assert.IsTrue(match.View.RootFor(relay.Id).Find("Research bar").gameObject.activeSelf);
            Assert.That(match.View.RootFor(relay.Id).Find("Research bar/Research fill").localScale.x, Is.EqualTo(1).Within(.001f));
            var ring = match.View.RootFor(relay.Id).Find("Selected supply reach");
            Assert.IsTrue(ring.gameObject.activeSelf);
            Assert.That(ring.localScale.x, Is.EqualTo(faction.RelayRadiusMillimetres * .001f).Within(.001f));
            StringAssert.Contains("relay deployed", match.Feedback);
            Assert.IsFalse(match.World.Submit(new MoveCommand(1, new[] { relay.Id }, new SimPoint(27500, 18500))).Accepted);
            for (int tick = 0; tick < faction.ThreadkeeperDeployTicks; tick++) match.World.Tick();
            Refresh();
            Assert.AreEqual(position, relay.Position);
            Assert.AreEqual(0, relay.ThreadkeeperRemainingTicks);
            Assert.AreEqual(faction.ThreadkeeperCooldownTicks, relay.ThreadkeeperCooldownTicks);
            Assert.AreEqual(carried, outsideWorker.CarryCapacity);
            Assert.IsFalse(ring.gameObject.activeSelf);
            CollectionAssert.AreEqual(new[] { relay.Id }, match.Selection);
        }

        [UnityTest]
        public IEnumerator PaidOutpostPackMoveAndDeployButtonsPreserveIdentityDamageStockAndPopulation()
        {
            CreateMatch("serevin");
            yield return null;
            var initial = player.Resources;
            for (int worker = 1; worker <= 4; worker++) Gather(worker, 199 + worker);
            AdvanceUntil(() => player.Resources.Food > initial.Food && player.Resources.Wood > initial.Wood &&
                player.Resources.Metal > initial.Metal && player.Resources.Stone > initial.Stone,
                "All four resource workers must deliver normal cargo before the relocation purchase.");
            match.Select(new[] { 1, 2, 3, 4 });
            match.StopSelected();
            var before = player.Resources;
            var definition = match.Economy.BuildingDefinition("supply_outpost");
            match.Economy.BeginBuild(definition.Id);
            match.Economy.PreviewAt(new SimPoint(26000, 30000));
            Assert.IsTrue(match.Economy.PlacementResult.Accepted, match.Economy.PlacementResult.Message);
            var placed = match.Economy.ConfirmBuild();
            Assert.IsTrue(placed.Accepted, placed.Message);
            var paid = Subtract(before, definition.Cost);
            Assert.AreEqual(paid, player.Resources);
            Assert.IsTrue(match.World.TryGetBuilding(placed.EntityId, out var site));
            AdvanceUntil(() => site.IsComplete, "The paid outpost foundation must be constructed by the selected workers.");

            // Damage comes from a real opponent recruit and attack, never a health mutation hook.
            var trainEnemy = match.World.Submit(new TrainCommand(2, 103, "reedguard"));
            Assert.IsTrue(trainEnemy.Accepted, trainEnemy.Message);
            AdvanceUntil(() => FindUnit(2, "reedguard") != null, "Create an ordinary attacker to verify damaged-form persistence.");
            var attacker = FindUnit(2, "reedguard");
            var attack = match.World.Submit(new AttackCommand(2, new[] { attacker.Id }, site.Id));
            Assert.IsTrue(attack.Accepted, attack.Message);
            AdvanceUntil(() => site.Health < site.MaxHealth, "A real hit must damage the outpost before it transforms.");
            Assert.IsTrue(match.World.Submit(new StopCommand(2, new[] { attacker.Id })).Accepted);
            int health = site.Health;
            int population = player.PopulationUsed;
            int capacity = player.PopulationCapacity;
            match.Select(new[] { site.Id });
            Refresh();
            var originalRoot = match.View.RootFor(site.Id);
            Click(FindButton("Pack outpost"));
            Assert.AreEqual(RelocationStage.Packing, site.RelocationStage);
            Assert.IsFalse(site.IsOperational);
            StringAssert.Contains("Drop-off is offline", match.Feedback);
            var faction = match.Factions.LocalDefinition;
            for (int tick = 0; tick < faction.PackTicks - 1; tick++) match.World.Tick();
            Assert.IsTrue(match.World.TryGetBuilding(site.Id, out _));
            match.World.Tick();
            Refresh();
            Assert.IsFalse(match.World.TryGetBuilding(site.Id, out _));
            Assert.IsTrue(match.World.TryGetUnit(site.Id, out var cart));
            Assert.IsTrue(cart.IsPackedOutpost);
            Assert.AreEqual("supply_cart", cart.DefinitionId);
            Assert.AreEqual(health, cart.Health);
            var cartRoot = match.View.RootFor(cart.Id);
            Assert.AreNotSame(originalRoot, cartRoot);
            Assert.IsFalse(originalRoot.gameObject.activeSelf);
            AssertAlphaModel(cartRoot, FactionKind.SerevinMarch, "supply_cart");
            CollectionAssert.AreEqual(new[] { site.Id }, match.Selection);

            Move(cart, new SimPoint(31500, 29500));
            AdvanceUntil(() => cart.Order == UnitOrder.Idle && cart.Position == cart.Destination,
                "The vulnerable transport must physically travel to the deployment area.");
            Assert.AreNotEqual(site.Position, cart.Position);
            Refresh();
            Click(FindButton("Deploy outpost"));
            Assert.AreEqual(cart.Id, match.Factions.PendingDeployUnitId);
            match.Factions.PreviewAt(new SimPoint(33000, 30000));
            Assert.IsTrue(match.Factions.HasPreview);
            Assert.IsTrue(match.Factions.PlacementResult.Accepted, match.Factions.PlacementResult.Message);
            var destination = match.Factions.PreviewPosition;
            Assert.AreEqual(paid, player.Resources, "Preview and relocation cannot buy a second asset.");
            Refresh();
            Click(FindButton("Confirm deploy"));
            Assert.AreEqual(0, match.Factions.PendingDeployUnitId);
            Assert.AreEqual(RelocationStage.Deploying, cart.RelocationStage);
            StringAssert.Contains("Deploying", match.Feedback);
            for (int tick = 0; tick < faction.DeployTicks - 1; tick++) match.World.Tick();
            Assert.IsTrue(match.World.TryGetUnit(cart.Id, out _));
            match.World.Tick();
            Refresh();
            Assert.IsFalse(match.World.TryGetUnit(cart.Id, out _));
            Assert.IsTrue(match.World.TryGetBuilding(cart.Id, out var deployed));
            Assert.AreEqual("supply_outpost", deployed.DefinitionId);
            Assert.AreEqual(destination, deployed.Position);
            Assert.IsTrue(deployed.IsOperational);
            Assert.AreEqual(health, deployed.Health);
            Assert.AreEqual(paid, player.Resources);
            Assert.AreEqual(population, player.PopulationUsed);
            Assert.AreEqual(capacity, player.PopulationCapacity);
            Assert.AreEqual(0, player.PopulationReserved);
            var deployedRoot = match.View.RootFor(deployed.Id);
            Assert.AreNotSame(cartRoot, deployedRoot);
            Assert.IsFalse(cartRoot.gameObject.activeSelf);
            AssertAlphaModel(deployedRoot, FactionKind.SerevinMarch, "supply_outpost");
            Assert.That(deployedRoot.Find("Health bar/Fill").localScale.x,
                Is.EqualTo(health / (float)deployed.MaxHealth).Within(.001f));
            CollectionAssert.AreEqual(new[] { deployed.Id }, match.Selection);
            Assert.IsNotNull(FindButton("Pack outpost"));
            Assert.AreEqual(match.World.Units.Count + match.World.Buildings.Count + match.World.Resources.Count, match.View.Count);
        }

        [UnityTest]
        public IEnumerator GroupedAshrunnerButtonActivatesBothUnitsAndRejectsMixedSelectionAtomically()
        {
            CreateMatch("serevin");
            yield return null;
            Gather(1, 200);
            var recruit = match.Economy.UnitDefinition("ashrunner");
            AdvanceUntil(() => player.Resources.Food >= recruit.Cost.Food * 2,
                "A Tender must physically deliver enough Food for two Ashrunners.");
            match.Select(new[] { 1 });
            match.StopSelected();
            match.Select(new[] { 102 });
            Refresh();
            var train = FindButton("Train Ashrunner");
            Click(train);
            Click(train);
            AdvanceUntil(() => CountUnits(1, "ashrunner") == 2, "Both paid cavalry recruits must finish their queue.");
            var cavalry = new List<UnitState>();
            foreach (var unit in match.World.Units)
                if (unit.OwnerId == 1 && unit.DefinitionId == "ashrunner") cavalry.Add(unit);

            match.Select(new[] { cavalry[0].Id, 1 });
            Refresh();
            var mixedButton = FindButton("Reposition");
            Assert.IsTrue(mixedButton == null || !mixedButton.interactable,
                "A mixed cavalry/worker group must not expose an enabled Reposition action.");
            var stock = player.Resources;
            var mixed = match.World.Submit(new RepositionCommand(1, new[] { cavalry[0].Id, 1 }));
            Assert.IsFalse(mixed.Accepted);
            Assert.AreEqual(CommandRejection.CannotUseFactionAction, mixed.Reason);
            Assert.AreEqual(stock, player.Resources);
            Assert.AreEqual(0, cavalry[0].RepositionRemainingTicks);
            Assert.AreEqual(0, cavalry[1].RepositionRemainingTicks);

            var ids = new[] { cavalry[0].Id, cavalry[1].Id };
            match.Select(ids);
            Refresh();
            var action = FindButton("Reposition");
            Assert.IsNotNull(action, "Selecting two Ashrunners must retain their shared ability button.");
            Click(action);
            foreach (var unit in cavalry)
            {
                Assert.AreEqual(match.Factions.LocalDefinition.RepositionTicks, unit.RepositionRemainingTicks);
                Assert.AreEqual(RepositionSpeed(match.Economy.UnitDefinition(unit.DefinitionId)), unit.MoveSpeedMillimetresPerSecond);
                Assert.AreEqual(0, unit.AttackTargetId);
            }
            foreach (var unit in match.World.Units)
                if (unit.OwnerId != 1 || unit.DefinitionId != "ashrunner")
                {
                    Assert.AreEqual(0, unit.RepositionRemainingTicks, "The group action must not affect workers or enemies.");
                    Assert.AreEqual(match.Economy.UnitDefinition(unit.DefinitionId).MoveSpeedMillimetresPerSecond,
                        unit.MoveSpeedMillimetresPerSecond);
                }
            Assert.AreEqual(stock, player.Resources);
            CollectionAssert.AreEqual(ids, match.Selection);
            Refresh();
            Assert.IsFalse(FindButton("Reposition").interactable, "An active group cannot start the same burst again.");
        }

        [UnityTest]
        public IEnumerator TransportDeathDuringDeploymentPreviewClearsGhostAndRestoresWorldTaps()
        {
            CreateMatch("serevin");
            yield return null;
            match.Select(new[] { 1 });
            match.Economy.BeginBuild("supply_outpost");
            match.Economy.PreviewAt(new SimPoint(26000, 30000));
            Assert.IsTrue(match.Economy.PlacementResult.Accepted, match.Economy.PlacementResult.Message);
            var placed = match.Economy.ConfirmBuild();
            Assert.IsTrue(placed.Accepted, placed.Message);
            Assert.IsTrue(match.World.TryGetBuilding(placed.EntityId, out var site));
            AdvanceUntil(() => site.IsComplete, "The ordinary paid construction order must finish the outpost.");
            match.Select(new[] { site.Id });
            Refresh();
            Click(FindButton("Pack outpost"));
            AdvanceUntil(() => match.World.TryGetUnit(site.Id, out _), "The outpost must finish packing before relocation.");
            Assert.IsTrue(match.World.TryGetUnit(site.Id, out var cart));
            Move(cart, new SimPoint(31500, 29500));
            AdvanceUntil(() => cart.Order == UnitOrder.Idle && cart.Position == cart.Destination,
                "The transport must physically reach a clear deployment approach.");
            Refresh();
            Click(FindButton("Deploy outpost"));
            match.Factions.PreviewAt(new SimPoint(33000, 30000));
            Refresh();
            Assert.AreEqual(cart.Id, match.Factions.PendingDeployUnitId);
            Assert.IsTrue(match.Factions.HasPreview);
            Assert.IsTrue(match.Factions.PlacementResult.Accepted, match.Factions.PlacementResult.Message);
            var preview = root.transform.Find("Outpost deployment preview");
            Assert.IsNotNull(preview);
            Assert.IsTrue(preview.gameObject.activeSelf);
            Assert.IsNotNull(FindButton("Confirm deploy"));

            var training = match.World.Submit(new TrainCommand(2, 103, "reedguard"));
            Assert.IsTrue(training.Accepted, training.Message);
            AdvanceUntil(() => FindUnit(2, "reedguard") != null, "An ordinary opposing recruit supplies real attack damage.");
            var attacker = FindUnit(2, "reedguard");
            var attack = match.World.Submit(new AttackCommand(2, new[] { attacker.Id }, cart.Id));
            Assert.IsTrue(attack.Accepted, attack.Message);
            AdvanceUntil(() => !match.World.TryGetUnit(cart.Id, out _),
                "The exposed transport must be killed by combat while its placement preview is open.");
            Assert.IsTrue(match.World.Submit(new StopCommand(2, new[] { attacker.Id })).Accepted);
            Refresh();
            Assert.AreEqual(0, match.Factions.PendingDeployUnitId);
            Assert.IsFalse(match.Factions.HasPreview);
            Assert.IsFalse(preview.gameObject.activeSelf);
            Assert.IsNull(FindButton("Confirm deploy"));
            Assert.IsNull(FindButton("Cancel deploy"));
            Assert.IsNull(match.View.RootFor(cart.Id));
            Assert.IsEmpty(match.Selection);

            Assert.IsTrue(match.World.TryGetUnit(2, out var worker));
            match.Tap(EntityPoint(worker.Position), false);
            CollectionAssert.AreEqual(new[] { worker.Id }, match.Selection,
                "The first normal world tap after death must select a worker, not update a stale preview.");
            var destination = new SimPoint(14500, 23500);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(destination)), false);
            Assert.AreEqual(UnitOrder.Moving, worker.Order);
            AdvanceUntil(() => worker.Order == UnitOrder.Idle && worker.Position == destination,
                "Ordinary movement must remain usable after the dead preview is cleared.");
        }

        private int CountUnits(int owner, string definition)
        {
            int count = 0;
            foreach (var unit in match.World.Units) if (unit.OwnerId == owner && unit.DefinitionId == definition) count++;
            return count;
        }

        private static void AssertAlphaModel(Transform entity, FactionKind faction, string definitionId)
        {
            // Equipment is now merged into shared LOD meshes, rather than separately named primitive children.
            var model = entity.Find("Model");
            Assert.IsNotNull(model);
            // Where the Meshy catalogue has this unit for this faction, its rigged character draws it instead.
            if (MeshyUnitVisuals.Resolve(definitionId, faction) != null)
            {
                var character = model.GetComponentInChildren<CorsairAnimationDriver>();
                Assert.IsTrue(character != null && character.HasValidRig, definitionId + " must have its Meshy character after training or transformation.");
                return;
            }
            // A siege engine or cart with a Meshy model is drawn by it for every faction, in the culture's own style
            // where the roster has one (the ladders).
            if (MeshyPropVisuals.ResolveSiege(definitionId, faction) is MeshyPropVisuals.Entry siege)
            {
                Assert.IsNotNull(model.Find("Meshy " + siege.id), definitionId + " must have its Meshy model after training or transformation.");
                return;
            }
            // Where the Meshy catalogue has this building for this faction, its model draws it instead.
            if (MeshyBuildingVisuals.Resolve(definitionId, faction) != null)
            {
                var meshy = model.Find("Meshy " + definitionId);
                Assert.IsNotNull(meshy, definitionId + " must have its Meshy model after training or transformation.");
                Assert.IsNotEmpty(meshy.GetComponentsInChildren<MeshRenderer>(), definitionId + " Meshy model renders");
                return;
            }
            var lodGroup = model.GetComponentInChildren<LODGroup>();
            Assert.IsNotNull(lodGroup, definitionId + " must have a rendered alpha model after training or transformation.");
            var lods = lodGroup.GetLODs();
            Assert.AreEqual(2, lods.Length);
            for (int index = 0; index < lods.Length; index++)
            {
                Assert.AreEqual(1, lods[index].renderers.Length);
                var filter = lods[index].renderers[0].GetComponent<MeshFilter>();
                Assert.IsNotNull(filter); Assert.IsNotNull(filter.sharedMesh);
                Assert.AreEqual("Alpha " + faction + " " + definitionId + " LOD" + index, filter.sharedMesh.name);
                Assert.Greater(filter.sharedMesh.vertexCount, 0);
            }
        }

        private void CreateMatch(string factionId)
        {
            root = new GameObject("Faction integration " + factionId);
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.InitialFactionId = factionId;
            match.enabled = false;
            root.SetActive(true);
            Assert.IsTrue(match.World.TryGetPlayer(1, out player));
            Refresh();
        }

        private static void AssertEqualStart(World world, string localFaction)
        {
            Assert.AreEqual("faction_proving_ground", world.Map.Id);
            Assert.AreEqual(8, world.Units.Count);
            Assert.AreEqual(6, world.Buildings.Count);
            Assert.IsTrue(world.TryGetPlayer(1, out var local));
            Assert.IsTrue(world.TryGetPlayer(2, out var opponent));
            Assert.AreEqual(localFaction, local.FactionId);
            Assert.AreEqual(localFaction == "aven" ? "serevin" : "aven", opponent.FactionId);
            foreach (var owner in new[] { local, opponent })
            {
                Assert.AreEqual(new ResourceAmount(120, 80, 0, 40), owner.Resources);
                Assert.AreEqual("settlement", owner.EraId);
                Assert.AreEqual(4, owner.PopulationUsed);
                Assert.AreEqual(0, owner.PopulationReserved);
                Assert.AreEqual(6, owner.PopulationCapacity);
                int workers = 0;
                var buildings = new List<string>();
                foreach (var unit in world.Units)
                    if (unit.OwnerId == owner.Id) { Assert.AreEqual("tender", unit.DefinitionId); workers++; }
                foreach (var building in world.Buildings)
                    if (building.OwnerId == owner.Id) { Assert.IsTrue(building.IsOperational); buildings.Add(building.DefinitionId); }
                Assert.AreEqual(4, workers);
                CollectionAssert.AreEquivalent(new[] { "hearth", "storeyard", "muster_hall" }, buildings);
            }
        }

        private void Gather(int workerId, int resourceId)
        {
            Assert.IsTrue(match.World.TryGetResource(resourceId, out var resource));
            match.Select(new[] { workerId });
            Refresh();
            match.Tap(EntityPoint(resource.Position), false);
            Assert.IsTrue(match.World.TryGetUnit(workerId, out var worker));
            Assert.AreEqual(resourceId, worker.TargetResourceId, "A resource tap must route through the real gather adapter.");
        }

        private void Move(UnitState unit, SimPoint destination)
        {
            var result = match.World.Submit(new MoveCommand(unit.OwnerId, new[] { unit.Id }, destination));
            Assert.IsTrue(result.Accepted, result.Message);
        }

        private UnitState FindUnit(int owner, string definition)
        {
            foreach (var unit in match.World.Units) if (unit.OwnerId == owner && unit.DefinitionId == definition) return unit;
            return null;
        }

        private void AdvanceUntil(Func<bool> condition, string message)
        {
            for (int tick = 0; tick < 20000 && !condition(); tick++) match.World.Tick();
            Refresh();
            Assert.IsTrue(condition(), message + " Tick " + match.World.TickIndex + ".");
        }

        private void Refresh()
        {
            match.Hud.Invalidate();
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
        }

        private Button FindButton(string labelPrefix)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                var label = button.GetComponentInChildren<Text>();
                if (label != null && label.text.StartsWith(labelPrefix, StringComparison.Ordinal)) return button;
            }
            return null;
        }

        private Vector2 EntityPoint(SimPoint position)
            => match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(position) + Vector3.up * .6f);

        private static Vector2 ButtonPoint(Button button)
        {
            var rect = button.GetComponent<RectTransform>();
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static void AssertFrontmost(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = ButtonPoint(button) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            var front = hits[0].gameObject.transform;
            Assert.IsTrue(front == button.transform || front.IsChildOf(button.transform), "The visible control must receive the frontmost real UI raycast.");
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
            var pointer = new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left, position = ButtonPoint(button) };
            Assert.IsTrue(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler));
        }

        private static ResourceAmount Subtract(ResourceAmount balance, ResourceAmount cost)
            => new ResourceAmount(balance.Food - cost.Food, balance.Wood - cost.Wood, balance.Metal - cost.Metal, balance.Stone - cost.Stone);
    }
}
