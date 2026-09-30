using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Emberfield.Presentation
{
    // Explicit development-only functional walkthrough using shipped rules and physically gathered stock.
    internal sealed class TechnologyPlayerSmoke
    {
        private readonly MatchController match;
        private readonly string folder;
        private PlayerState player;
        private bool passed = true;
        private string failure = "";
        private int archiveId, hallId, oldSoldierId, newSoldierId;
        private readonly List<string> completed = new List<string>();
        private bool lockedArchive, earlyEmpire, existingUpgraded, newUpgraded, enemyUnchanged, spentExactly;
        private int frames, totalTicks, visibleButtonDispatches;

        private TechnologyPlayerSmoke(MatchController match, string folder) { this.match = match; this.folder = folder; }
        internal static IEnumerator Run(MatchController match, string folder) => new TechnologyPlayerSmoke(match, folder).Execute();

        private IEnumerator Execute()
        {
            Directory.CreateDirectory(folder); match.enabled = false;
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            match.World.TryGetPlayer(MatchController.LocalPlayer, out player);
            var starting = player.Resources;
            match.Select(new[] { 2 });
            var reject = match.World.Submit(new BuildCommand(1, new[] { 2 }, "archive", new SimPoint(17500, 8500)));
            lockedArchive = !reject.Accepted && reject.Reason == CommandRejection.PrerequisiteMissing && starting.Equals(player.Resources);
            reject = match.World.Submit(new ResearchCommand(1, 100, "advance_empire"));
            earlyEmpire = !reject.Accepted && reject.Reason == CommandRejection.PrerequisiteMissing && starting.Equals(player.Resources);
            for (int i = 1; i <= 4; i++) Check(match.World.Submit(new GatherCommand(1, new[] { i }, 199 + i)).Accepted, "Worker gather order rejected.");

            yield return Research("advance_kingdom");
            yield return Construct("archive", new SimPoint(17500, 8500), id => archiveId = id);
            yield return Construct("muster_hall", new SimPoint(23500, 15500), id => hallId = id);
            yield return Train(id => oldSoldierId = id);
            yield return Research("gather_1", true);
            yield return Research("weapons_1");
            yield return Research("armor_1");
            yield return Research("advance_dominion");
            yield return Research("gather_2");
            yield return Research("advance_empire");
            yield return Research("weapons_2");
            yield return Research("armor_2");
            yield return Train(id => newSoldierId = id);

            if (passed)
            {
                var definition = match.Economy.UnitDefinition("reedguard");
                match.World.TryGetUnit(oldSoldierId, out var oldUnit); match.World.TryGetUnit(newSoldierId, out var newUnit);
                existingUpgraded = oldUnit != null && oldUnit.AttackDamage == definition.Attack.Damage + 4 && oldUnit.Armor == definition.Armor + 2;
                newUpgraded = newUnit != null && newUnit.AttackDamage == oldUnit.AttackDamage && newUnit.Armor == oldUnit.Armor;
                match.World.TryGetUnit(5, out var enemy); match.World.TryGetPlayer(2, out var enemyPlayer);
                enemyUnchanged = enemy.GatherAmount == match.Economy.UnitDefinition("tender").GatherAmount && enemyPlayer.EraTier == 1 && enemyPlayer.CompletedTechnologyIds.Count == 0;
                match.World.TryGetUnit(1, out var worker);
                Check(worker.GatherAmount == 4 && player.EraTier == 4 && completed.Count == 9 && lockedArchive && earlyEmpire && existingUpgraded && newUpgraded && enemyUnchanged, "Final era, upgrade or isolation checks failed.");
            }
            match.Research.Close();
            if (newSoldierId != 0)
            {
                match.Select(new[] { newSoldierId });
                if (match.World.TryGetUnit(newSoldierId, out var selected)) match.Rig.Focus(DefinitionLoader.ToWorld(selected.Position));
            }
            match.SetFeedback(passed ? "Empire reached through gathering, construction and paid research. Existing and new troops share completed upgrades." : failure);
            match.SyncPresentation(1);
            yield return Capture("empire-units.png");
            match.Research.Open(); match.Hud.Invalidate(); match.Hud.Refresh();
            yield return Capture("research-empire-top.png");
            match.Hud.ResearchPanel.ScrollToBottom();
            yield return Capture("greybox.png");
            File.WriteAllText(Path.Combine(folder, "smoke.txt"),
                $"Passed: {passed}\nScenario: Technology\nUnity: {Application.unityVersion}\nBuildGuid: {Application.buildGUID}\nResolution: {Screen.width}x{Screen.height}\nGraphics: {SystemInfo.graphicsDeviceName}\n" +
                $"Map: {match.World.Map.Id}\nEra: {player.EraId}\nTier: {player.EraTier}\nCompletedResearch: {string.Join(",", completed)}\nResearchCount: {completed.Count}\n" +
                $"StartingStockUnmodified: True\nResourcesPhysicallyGathered: True\nVisibleRaycastButtonDispatches: {visibleButtonDispatches}\nLastPurchaseChargedExactly: {spentExactly}\nEarlyArchiveRejected: {lockedArchive}\nEarlyEmpireRejected: {earlyEmpire}\n" +
                $"ExistingTroopUpgraded: {existingUpgraded}\nNewTroopUpgraded: {newUpgraded}\nEnemyUnchanged: {enemyUnchanged}\nArchive: {archiveId}\nMusterHall: {hallId}\nPopulation: {player.PopulationUsed}+{player.PopulationReserved}/{player.PopulationCapacity}\n" +
                $"Tick: {match.World.TickIndex}\nAcceleratedTicks: {totalTicks}\nYieldedFrames: {frames}\nTechnologyTicksAccelerated: True\nFailure: {failure}\n");
            Debug.Log("EMBERFIELD_TECHNOLOGY_SMOKE " + passed);
            Application.Quit(passed ? 0 : 1);
        }

        private IEnumerator Research(string id, bool captureProgress = false)
        {
            if (!passed) yield break;
            match.ClearSelection();
            yield return WaitFor(() => match.Research.Validate(id).Accepted, "Cannot start " + id);
            if (!passed) yield break;
            var definition = match.Research.Technology(id);
            var before = player.Resources;
            match.Research.Open(); match.Hud.Invalidate(); match.Hud.Refresh(); Canvas.ForceUpdateCanvases();
            var button = match.Hud.ResearchPanel.ButtonFor(id);
            Check(button != null && button.interactable, "Research button unavailable: " + id);
            if (!passed) yield break;
            match.Hud.ResearchPanel.Reveal(id);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = button.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = point };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Research button is not visible/hittable after scrolling: " + id);
            if (!passed) yield break;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler); visibleButtonDispatches++;
            var after = player.Resources; var cost = definition.Cost;
            spentExactly = before.Food - after.Food == cost.Food && before.Wood - after.Wood == cost.Wood && before.Metal - after.Metal == cost.Metal && before.Stone - after.Stone == cost.Stone;
            Check(spentExactly && match.Research.ActiveSite(id) != null, "Research click did not start or charge exactly: " + id);
            var duplicate = match.Research.Start(id);
            Check(!duplicate.Accepted && after.Equals(player.Resources), "Duplicate research charged twice: " + id);
            if (captureProgress && passed)
            {
                Advance(definition.ResearchTicks / 2); match.SyncPresentation(1);
                yield return Capture("research-kingdom.png");
                match.Research.Close(); match.Select(new[] { archiveId });
                if (match.World.TryGetBuilding(archiveId, out var archive)) match.Rig.Focus(DefinitionLoader.ToWorld(archive.Position));
                match.SyncPresentation(1);
                yield return Capture("archive-research.png");
                match.Research.Open();
            }
            yield return WaitFor(() => player.HasTechnology(id), "Research did not finish: " + id);
            if (passed) completed.Add(id);
            match.Research.Close();
        }

        private IEnumerator Construct(string id, SimPoint position, Action<int> record)
        {
            if (!passed) yield break;
            var definition = match.Economy.BuildingDefinition(id);
            yield return WaitFor(() => Affordable(definition.Cost), "Cannot afford " + id);
            if (!passed) yield break;
            match.Select(new[] { 2 }); match.Economy.BeginBuild(id); match.Economy.PreviewAt(position);
            var result = match.Economy.ConfirmBuild(); Check(result.Accepted, result.Message);
            if (!passed) yield break;
            int buildingId = result.EntityId; record(buildingId);
            yield return WaitFor(() => match.World.TryGetBuilding(buildingId, out var building) && building.IsComplete, "Construction did not finish: " + id);
            Check(match.World.Submit(new GatherCommand(1, new[] { 2 }, 201)).Accepted, "Builder did not resume wood gathering.");
        }

        private IEnumerator Train(Action<int> record)
        {
            if (!passed) yield break;
            match.Research.Close();
            yield return WaitFor(() => Affordable(match.Economy.UnitDefinition("reedguard").Cost), "Cannot afford Reedguard.");
            if (!passed) yield break;
            var before = new HashSet<int>(); foreach (var unit in match.World.Units) before.Add(unit.Id);
            match.Select(new[] { hallId }); Check(match.Economy.Train("reedguard").Accepted, "Reedguard training rejected.");
            yield return WaitFor(() => { foreach (var unit in match.World.Units) if (!before.Contains(unit.Id)) return true; return false; }, "Reedguard did not spawn.");
            foreach (var unit in match.World.Units) if (!before.Contains(unit.Id)) { record(unit.Id); break; }
        }

        private IEnumerator WaitFor(Func<bool> condition, string message)
        {
            int budget = World.TickRate * 1200;
            while (passed && !condition() && budget > 0)
            {
                int count = 0;
                while (count++ < 100 && budget-- > 0 && !condition()) Advance(1);
                match.SyncPresentation(1); frames++; yield return null;
            }
            Check(condition(), message + " at tick " + match.World.TickIndex);
        }
        private bool Affordable(ResourceAmount cost)
        { var stock = player.Resources; return stock.Food >= cost.Food && stock.Wood >= cost.Wood && stock.Metal >= cost.Metal && stock.Stone >= cost.Stone; }
        private void Advance(int ticks) { for (int i = 0; i < ticks; i++) { match.AdvanceSimulationTick(); totalTicks++; } }
        private void Check(bool condition, string message) { if (!condition && passed) { passed = false; failure = message; } }
        private IEnumerator Capture(string name)
        {
            match.Hud.Invalidate(); match.SyncPresentation(1); match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            Check(PlayerSmoke.Capture(match, Path.Combine(folder, name)), "Capture has no pixels: " + name);
        }
    }
}
