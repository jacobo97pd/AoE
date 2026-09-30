using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Opt-in functional drill. Accelerated ticks and synthetic UI clicks are not performance/touch measurements.
    internal sealed class FactionPlayerSmoke
    {
        private static int chooserDispatches;
        private static string requestedFaction;
        private static bool chooserSwapVerified;
        private readonly MatchController match;
        private readonly string folder;
        private PlayerState player, opponent;
        private FactionDefinition faction;
        private bool passed = true, equalStarts, uniqueGate, mechanic, uniqueAction, uniqueResearch, healthPreserved;
        private string failure = "";
        private int uniqueId, outpostId, archiveId, ticks, frames, clicks;
        private FactionPlayerSmoke(MatchController match, string folder) { this.match = match; this.folder = folder; }
        internal static IEnumerator Run(MatchController match, string folder) => new FactionPlayerSmoke(match, folder).Execute();

        private IEnumerator Execute()
        {
            Directory.CreateDirectory(folder); match.enabled = false;
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            faction = match.Factions.LocalDefinition;
            Check(faction != null, "Faction launch selection missing.");
            if (passed && chooserDispatches < 2)
            {
                string target;
                if (chooserDispatches == 0)
                {
                    // This retained drill verifies the Aven/Serevin mechanics and their visible
                    // first historical card pair; expansion factions have their own walkthrough.
                    requestedFaction = faction.Id; target = requestedFaction == "aven" ? "serevin" : "aven";
                }
                else { chooserSwapVerified = faction.Id == (requestedFaction == "aven" ? "serevin" : "aven"); Check(chooserSwapVerified, "CLI faction overrode an explicit chooser selection."); target = requestedFaction; }
                match.Factions.Open(); yield return Capture("faction-chooser.png");
                chooserDispatches++;
                yield return Click("Choose " + target);
                if (passed) yield break; // The chooser loads a fresh match; its Start resumes this opt-in runner.
            }
            Check(faction.Id == requestedFaction && chooserSwapVerified, "Chooser did not return to the requested faction.");
            match.World.TryGetPlayer(1, out player); match.World.TryGetPlayer(2, out opponent);
            equalStarts = player.Resources.Equals(match.World.Definition.StartingResources) && player.Resources.Equals(opponent.Resources) && player.PopulationUsed == 4 && opponent.PopulationUsed == 4;
            Check(equalStarts, "Faction drill did not preserve equal authored starting budgets.");
            bool aven = faction?.Kind == FactionKind.AvenCompact;
            var before = player.Resources;
            var denied = match.World.Submit(new TrainCommand(1, 102, aven ? "ashrunner" : "threadkeeper"));
            uniqueGate = !denied.Accepted && before.Equals(player.Resources);
            Check(uniqueGate, "Enemy faction unit gate was not atomic.");
            for (int id = 1; id <= 4; id++) Check(match.World.Submit(new GatherCommand(1, new[] { id }, 199 + id)).Accepted, "Gather order rejected.");
            yield return Train(aven ? "threadkeeper" : "ashrunner", id => uniqueId = id);
            if (aven) yield return AvenDrill(); else yield return SerevinDrill();
            yield return Research("advance_kingdom");
            yield return Construct("archive", new SimPoint(17500, 26500), id => archiveId = id);
            string technology = aven ? faction.ReciprocalStoresTechnologyId : faction.PreparedEncampmentsTechnologyId;
            yield return Research(technology);
            uniqueResearch = player.HasTechnology(technology) && !opponent.HasTechnology(technology);
            Check(uniqueResearch, "Faction research did not complete for its owner alone.");
            if (passed && !aven)
            {
                match.Select(new[] { outpostId });
                yield return Click("Pack outpost");
                Check(match.World.TryGetBuilding(outpostId, out var packed) && packed.RelocationRemainingTicks == faction.PackTicks - faction.PreparationReductionTicks, "Prepared Encampments did not shorten packing.");
                yield return WaitFor(() => match.World.TryGetUnit(outpostId, out _), "Improved packing did not complete.");
                yield return DeployTransport();
            }
            match.Research.Open(); match.Hud.Invalidate(); match.Hud.Refresh(); match.Hud.ResearchPanel.Reveal(technology);
            yield return Capture("faction-research.png");
            match.Research.Close();
            match.Select(new[] { aven ? 104 : outpostId }); FocusSelection();
            match.SetFeedback(passed ? faction.DisplayName + ": paid faction choices, unique unit and unique technology verified through physical gathering." : failure);
            yield return Capture("greybox.png");
            File.WriteAllText(Path.Combine(folder, "smoke.txt"),
                $"Passed: {passed}\nScenario: Faction\nFaction: {faction?.Id}\nUnity: {Application.unityVersion}\nBuildGuid: {Application.buildGUID}\nResolution: {Screen.width}x{Screen.height}\nGraphics: {SystemInfo.graphicsDeviceName}\n" +
                $"Map: {match.World.Map.Id}\nEqualStartingBudgets: {equalStarts}\nResourcesPhysicallyGathered: True\nForeignUniqueRejectedAtomically: {uniqueGate}\nUniqueUnit: {uniqueId}\nFactionMechanicVerified: {mechanic}\nUniqueActionVerified: {uniqueAction}\nUniqueResearchCompleted: {uniqueResearch}\nRelocationHealthPreserved: {(aven ? "Not applicable" : healthPreserved.ToString())}\n" +
                $"ChooserButtonDispatches: {chooserDispatches}\nChooserOverridesCliFaction: {chooserSwapVerified}\nVisibleButtonDispatches: {clicks + chooserDispatches}\nPopulation: {player.PopulationUsed}+{player.PopulationReserved}/{player.PopulationCapacity}\nTick: {match.World.TickIndex}\nAcceleratedTicks: {ticks}\nYieldedFrames: {frames}\nTicksAccelerated: True\nFailure:{failure}\n");
            Debug.Log("EMBERFIELD_FACTION_SMOKE " + passed);
            Application.Quit(passed ? 0 : 1);
        }

        private IEnumerator AvenDrill()
        {
            if (!passed) yield break;
            yield return WaitFor(() => match.World.ValidateFactionAction(new SetCharterCommand(1, 104, StoreyardCharter.Logistics)).Accepted, "Cannot afford Logistics charter.");
            match.Select(new[] { 104 }); var before = player.Resources;
            yield return Click("Set Logistics charter");
            Check(Charged(before, faction.CharterCost), "Charter switch did not charge its exact cost.");
            var after = player.Resources;
            Check(!match.World.Submit(new SetCharterCommand(1, 104, StoreyardCharter.Muster)).Accepted && after.Equals(player.Resources), "Concurrent charter switch charged again.");
            Advance(faction.CharterTicks / 2); FocusSelection(); yield return Capture("charter-changing.png");
            yield return WaitFor(() => match.World.TryGetBuilding(104, out var yard) && yard.ActiveCharter == StoreyardCharter.Logistics, "Logistics charter did not finish.");
            Check(match.World.Submit(new MoveCommand(1, new[] { uniqueId }, new SimPoint(26500, 20500))).Accepted, "Threadkeeper move rejected.");
            yield return WaitFor(() => match.World.TryGetUnit(uniqueId, out var unit) && unit.Order == UnitOrder.Idle, "Threadkeeper failed to reach relay location.");
            match.Select(new[] { uniqueId }); yield return Click("Deploy relay");
            match.World.TryGetUnit(uniqueId, out var relay);
            uniqueAction = relay.ThreadkeeperRemainingTicks > 0 && relay.LinkedStoreyardId == 104 && !match.World.Submit(new MoveCommand(1, new[] { uniqueId }, new SimPoint(28500, 20500))).Accepted;
            Check(uniqueAction, "Threadkeeper relay did not immobilize and link correctly.");
            Advance(10); FocusSelection(); yield return Capture("unique-action.png");
            match.Select(new[] { 104 }); FocusSelection(); yield return Capture("faction-mechanic.png");
            yield return WaitFor(() => match.World.ValidateFactionAction(new SetCharterCommand(1, 104, StoreyardCharter.Muster)).Accepted, "Cannot switch to Muster.");
            before = player.Resources; yield return Click("Set Muster charter");
            Check(Charged(before, faction.CharterCost), "Second charter cost was not exact.");
            yield return WaitFor(() => match.World.TryGetBuilding(104, out var yard) && yard.ActiveCharter == StoreyardCharter.Muster, "Muster charter did not finish.");
            match.World.TryGetBuilding(102, out var hall);
            mechanic = hall.ProductionWorkPermille == 1000 + faction.MusterWorkBonusPermille;
            Check(mechanic, "Nearby Muster Hall did not receive nonstacking charter work bonus.");
            yield return Train("reedguard", _ => { });
        }

        private IEnumerator SerevinDrill()
        {
            if (!passed) yield break;
            match.Select(new[] { uniqueId });
            Check(match.World.Submit(new MoveCommand(1, new[] { uniqueId }, new SimPoint(27500, 17500))).Accepted, "Ashrunner route rejected.");
            yield return Click("Reposition");
            match.World.TryGetUnit(uniqueId, out var runner);
            uniqueAction = runner.RepositionRemainingTicks == faction.RepositionTicks && !match.World.Submit(new AttackCommand(1, new[] { uniqueId }, 11)).Accepted;
            Check(uniqueAction, "Reposition failed to suppress attacks.");
            Advance(20); FocusSelection(); yield return Capture("unique-action.png");
            yield return WaitFor(() => runner.RepositionRemainingTicks == 0, "Reposition did not expire.");
            Check(runner.RepositionCooldownTicks > 0, "Reposition cooldown did not start.");
            match.World.Submit(new StopCommand(1, new[] { uniqueId }));
            yield return Construct("supply_outpost", new SimPoint(26000, 27000), id => outpostId = id);
            // The opponent spends its unchanged starting resources on a defender to prove actual damage survives relocation.
            var ids = new HashSet<int>(); foreach (var unit in match.World.Units) ids.Add(unit.Id);
            Check(match.World.Submit(new TrainCommand(2, 103, "reedguard")).Accepted, "Opponent could not train the damage probe.");
            int enemy = 0;
            yield return WaitFor(() => { foreach (var unit in match.World.Units) if (unit.OwnerId == 2 && !ids.Contains(unit.Id)) { enemy = unit.Id; return true; } return false; }, "Opponent defender did not spawn.");
            if (!passed) yield break;
            Check(match.World.Submit(new AttackCommand(2, new[] { enemy }, outpostId)).Accepted, "Outpost damage order rejected.");
            yield return WaitFor(() => match.World.TryGetBuilding(outpostId, out var building) && building.Health < building.MaxHealth, "Outpost was not damaged.");
            match.World.Submit(new StopCommand(2, new[] { enemy }));
            match.World.TryGetBuilding(outpostId, out var original); int health = original.Health, population = player.PopulationUsed;
            match.Select(new[] { outpostId }); yield return Click("Pack outpost");
            Check(!original.IsOperational, "Packing outpost still provides its utility.");
            Advance(faction.PackTicks / 2); FocusSelection(); yield return Capture("outpost-packing.png");
            yield return WaitFor(() => match.World.TryGetUnit(outpostId, out _), "Packing did not create a transport.");
            if (!passed) yield break;
            match.World.TryGetUnit(outpostId, out var transport);
            Check(transport.Health == health && player.PopulationUsed == population, "Packing healed the outpost or changed population.");
            var start = transport.Position;
            Check(match.World.Submit(new MoveCommand(1, new[] { outpostId }, new SimPoint(32500, 28500))).Accepted, "Transport move rejected.");
            Advance(30); FocusSelection(); yield return Capture("faction-mechanic.png");
            yield return WaitFor(() => transport.Order == UnitOrder.Idle, "Transport did not arrive.");
            mechanic = transport.Position != start && transport.IsPackedOutpost;
            Check(mechanic, "Transport did not physically move.");
            yield return DeployTransport();
            healthPreserved = match.World.TryGetBuilding(outpostId, out var deployed) && deployed.Health == health && player.PopulationUsed == population;
            Check(healthPreserved, "Deployment did not preserve the damaged asset and population.");
        }

        private IEnumerator DeployTransport()
        {
            if (!passed) yield break;
            match.World.TryGetUnit(outpostId, out var unit);
            SimPoint position = default; bool found = false;
            int cell = match.World.Map.CellSizeMillimetres;
            // Packing leaves the cart at the old structure's grid intersection. Move it onto an open
            // approach-cell center before choosing an adjacent footprint, just as a terrain order does.
            var approach = new SimPoint(unit.Position.X / cell * cell + cell / 2, unit.Position.Z / cell * cell + cell / 2);
            Check(match.World.Submit(new MoveCommand(1, new[] { outpostId }, approach)).Accepted, "Transport could not approach a deployment cell.");
            yield return WaitFor(() => unit.Order == UnitOrder.Idle, "Transport did not reach deployment approach.");
            if (!passed) yield break;
            for (int z = -3; z <= 3 && !found; z++) for (int x = -3; x <= 3 && !found; x++)
            {
                var candidate = new SimPoint((unit.Position.X / cell + x) * cell, (unit.Position.Z / cell + z) * cell);
                if (match.World.ValidateFactionAction(new DeployOutpostCommand(1, outpostId, candidate)).Accepted) { position = candidate; found = true; }
            }
            Check(found, "No legal adjacent deployment footprint."); if (!passed) yield break;
            match.Select(new[] { outpostId });
            yield return Click("Deploy outpost\nBeside transport");
            match.Factions.PreviewAt(position); yield return Click("Confirm deploy");
            Check(unit.RelocationStage == RelocationStage.Deploying, "Deploy button did not start preparation.");
            yield return WaitFor(() => match.World.TryGetBuilding(outpostId, out var building) && building.IsOperational, "Outpost did not redeploy.");
            FocusSelection(); yield return Capture("outpost-deployed.png");
        }

        private IEnumerator Research(string id)
        {
            if (!passed) yield break;
            match.ClearSelection(); yield return WaitFor(() => match.Research.Validate(id).Accepted, "Cannot research " + id);
            if (!passed) yield break;
            match.Research.Open(); match.Hud.Invalidate(); match.Hud.Refresh(); match.Hud.ResearchPanel.Reveal(id);
            yield return Click("Research " + id);
            yield return WaitFor(() => player.HasTechnology(id), "Research did not complete: " + id);
            match.Research.Close();
        }
        private IEnumerator Train(string id, Action<int> record)
        {
            if (!passed) yield break;
            var definition = match.Economy.UnitDefinition(id);
            yield return WaitFor(() => Affordable(definition.Cost), "Cannot afford " + id);
            if (!passed) yield break;
            var old = new HashSet<int>(); foreach (var unit in match.World.Units) old.Add(unit.Id);
            match.Select(new[] { 102 });
            yield return Click("Train " + definition.DisplayName + "\n" + EconomyControls.CostText(definition.Cost));
            yield return WaitFor(() => { foreach (var unit in match.World.Units) if (!old.Contains(unit.Id) && unit.OwnerId == 1) return true; return false; }, "Training did not produce " + id);
            foreach (var unit in match.World.Units) if (!old.Contains(unit.Id) && unit.OwnerId == 1) { record(unit.Id); break; }
        }
        private IEnumerator Construct(string id, SimPoint position, Action<int> record)
        {
            if (!passed) yield break;
            yield return WaitFor(() => Affordable(match.Economy.BuildingDefinition(id).Cost), "Cannot afford " + id);
            if (!passed) yield break;
            match.Select(new[] { 2 }); match.Economy.BeginBuild(id); match.Economy.PreviewAt(position);
            var result = match.Economy.ConfirmBuild(); Check(result.Accepted, result.Message); if (!passed) yield break;
            record(result.EntityId);
            yield return WaitFor(() => match.World.TryGetBuilding(result.EntityId, out var building) && building.IsComplete, "Construction did not finish: " + id);
            Check(match.World.Submit(new GatherCommand(1, new[] { 2 }, 201)).Accepted, "Builder did not resume wood gathering.");
        }
        private IEnumerator Click(string name)
        {
            if (!passed) yield break;
            match.Hud.Invalidate(); match.SyncPresentation(1); yield return null; Canvas.ForceUpdateCanvases();
            Button button = null; foreach (var item in match.GetComponentsInChildren<Button>()) if (item.name == name) { button = item; break; }
            Check(button != null && button.interactable, "Button unavailable: " + name); if (!passed) yield break;
            var canvas = button.GetComponentInParent<Canvas>(); var rect = button.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Button is covered or outside the viewport: " + name);
            if (passed) { ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler); clicks++; }
        }
        private IEnumerator WaitFor(Func<bool> condition, string message)
        {
            if (!passed) yield break;
            int budget = World.TickRate * 1200;
            while (!condition() && budget > 0) { for (int i = 0; i < 100 && budget-- > 0 && !condition(); i++) Advance(1); match.SyncPresentation(1); frames++; yield return null; }
            Check(condition(), message + " at tick " + match.World.TickIndex);
        }
        private bool Affordable(ResourceAmount cost) { var stock = player.Resources; return stock.Food >= cost.Food && stock.Wood >= cost.Wood && stock.Metal >= cost.Metal && stock.Stone >= cost.Stone; }
        private bool Charged(ResourceAmount before, ResourceAmount cost) { var after = player.Resources; return before.Food - after.Food == cost.Food && before.Wood - after.Wood == cost.Wood && before.Metal - after.Metal == cost.Metal && before.Stone - after.Stone == cost.Stone; }
        private void Advance(int count) { for (int i = 0; i < count; i++) { match.AdvanceSimulationTick(); ticks++; } }
        private void Check(bool condition, string message) { if (!condition && passed) { passed = false; failure = message; } }
        private void FocusSelection()
        {
            if (match.Selection.Count != 1) return; int id = match.Selection[0];
            if (match.World.TryGetUnit(id, out var unit)) match.Rig.Focus(DefinitionLoader.ToWorld(unit.Position));
            else if (match.World.TryGetBuilding(id, out var building)) match.Rig.Focus(DefinitionLoader.ToWorld(building.Position));
        }
        private IEnumerator Capture(string name)
        {
            match.Hud.Invalidate(); match.SyncPresentation(1); match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            Check(PlayerSmoke.Capture(match, Path.Combine(folder, name)), "Capture contains no pixels: " + name);
        }
    }
}
