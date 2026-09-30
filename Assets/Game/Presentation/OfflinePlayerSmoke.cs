using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Opt-in accelerated distribution check. The local driver uses the same ordinary-command AI as its rival.
    internal sealed class OfflinePlayerSmoke
    {
        private static bool restarting;
        private static string completedReport;
        private readonly MatchController match;
        private readonly string folder;
        private bool passed = true;
        private string failure = "";
        private int clicks, frames;
        private OfflinePlayerSmoke(MatchController match, string folder) { this.match = match; this.folder = folder; }
        public static IEnumerator Run(MatchController match, string folder) => new OfflinePlayerSmoke(match, folder).Run();
        private IEnumerator Run()
        {
            Directory.CreateDirectory(folder); match.enabled = false; yield return null;
            if (restarting)
            {
                bool fresh = match.World.Match != null && !match.World.Match.IsFinished && match.World.TickIndex < 10 && match.World.Units.Count == 8;
                File.WriteAllText(Path.Combine(folder, "smoke.txt"), "Passed: " + fresh + "\n" + completedReport + "RestartFresh: " + fresh + "\n");
                restarting = false; Application.Quit(fresh ? 0 : 1); yield break;
            }
            Check(match.World.Match != null, "Offline world not loaded.");
            if (!passed) { FinishFailure(); yield break; }
            var local = new OfflineAi(match.World, 1);
            bool initialHidden = !match.World.Vision.IsEntityVisible(1, 101) && match.View.RootFor(101) == null;
            Check(initialHidden, "Starting vision leaked the enemy Hearth.");
            yield return Click("Menu");
            Check(match.OfflineControls.IsPaused && match.OfflineControls.BlocksWorldInput, "Pause menu did not block play.");
            yield return Capture("match-menu.png");
            yield return Click("Resume match");
            match.Select(new[] { 1 }); yield return Capture("match-start.png"); match.ClearSelection();
            bool settlementCaptured = false, battleCaptured = false, beaconCaptured = false;
            int peakUnits = 0;
            while (passed && !match.World.Match.IsFinished && match.World.TickIndex < World.TickRate * 2400L)
            {
                for (int tick = 0; tick < 100 && !match.World.Match.IsFinished; tick++)
                { local.Tick(); match.AdvanceSimulationTick(); peakUnits = Mathf.Max(peakUnits, match.World.Units.Count); }
                match.SyncPresentation(1); frames++; yield return null;
                if (!settlementCaptured && local.Statistics.BuildingsStarted >= 2 && local.ArmyCount >= 2)
                { settlementCaptured = true; match.Rig.Home(); yield return Capture("match-economy.png"); }
                if (!battleCaptured && match.World.DeathCount > 0)
                {
                    foreach (var enemy in local.Observation.KnownEnemies)
                        if (enemy.Visible) { match.Rig.Focus(DefinitionLoader.ToWorld(enemy.Position)); battleCaptured = true; break; }
                    if (battleCaptured) yield return Capture("match-battle.png");
                }
                if (!beaconCaptured && match.World.Match.Mode == VictoryMode.Dominion)
                    foreach (var point in match.World.Match.Objectives)
                        if (point.OwnerId == 1) { beaconCaptured = true; match.Rig.Focus(DefinitionLoader.ToWorld(point.Position)); yield return Capture("match-objective.png"); break; }
            }
            var state = match.World.Match;
            Check(state.IsFinished, "Natural match exceeded the 40-minute scenario deadline.");
            Check(local.Statistics.GatherOrders > 0 && local.Statistics.BuildingsStarted > 0 && local.Statistics.UnitsQueued > 0, "Local driver did not complete a real economy/production loop.");
            Check(match.OfflineControls.Ai.Statistics.GatherOrders > 0 && match.OfflineControls.Ai.Statistics.BuildingsStarted > 0, "Opponent did not use ordinary economy commands.");
            long finalTick = match.World.TickIndex; match.World.Tick();
            Check(match.World.TickIndex == finalTick && !match.World.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(20000, 24000))).Accepted, "Finished simulation accepted further play.");
            match.OfflineControls.Observe(); yield return Capture("greybox.png");
            completedReport = "Scenario: Offline\nUnity: " + Application.unityVersion + "\nBuildGuid: " + Application.buildGUID
                + "\nResolution: " + Screen.width + "x" + Screen.height + "\nFaction: " + match.Factions.LocalDefinition.Id + "\nMode: " + state.Mode
                + "\nWinner: " + state.WinnerId + "\nReason: " + state.Reason + "\nTick: " + finalTick + "\nSimulatedSeconds: " + (finalTick / (double)World.TickRate).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "\nInitialEnemyHidden: " + initialHidden + "\nLocalGatherOrders: " + local.Statistics.GatherOrders + "\nLocalBuildings: " + local.Statistics.BuildingsStarted
                + "\nLocalQueuedUnits: " + local.Statistics.UnitsQueued + "\nRivalGatherOrders: " + match.OfflineControls.Ai.Statistics.GatherOrders
                + "\nPeakUnits: " + peakUnits + "\nYieldedFrames: " + frames + "\nVisibleButtonDispatchesBeforeRestart: " + clicks
                + "\nNaturalResult: " + state.IsFinished + "\nFinishedStateFrozen: " + (match.World.TickIndex == finalTick)
                + "\nScriptedLocalAi: True\nTicksAccelerated: True\nFailure: " + failure + "\n";
            if (!passed) { FinishFailure(); yield break; }
            restarting = true;
            yield return Click("Play again");
            if (!passed) { restarting = false; FinishFailure(); }
        }
        private IEnumerator Click(string name)
        {
            if (!passed) yield break;
            match.Hud.Invalidate(); match.Hud.Refresh(); yield return null; Canvas.ForceUpdateCanvases();
            Button button = null; foreach (var item in match.GetComponentsInChildren<Button>()) if (item.name == name) { button = item; break; }
            Check(button != null && button.interactable, "Unavailable button: " + name); if (!passed) yield break;
            var canvas = button.GetComponentInParent<Canvas>(); var rect = button.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Button covered: " + name);
            if (passed) { clicks++; ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler); }
        }
        private IEnumerator Capture(string name)
        {
            match.Hud.Invalidate(); match.SyncPresentation(1); match.Hud.PrepareOffscreenCapture(match.Rig.Camera); Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame(); Check(PlayerSmoke.Capture(match, Path.Combine(folder, name)), "Empty screenshot.");
        }
        private void Check(bool value, string message) { if (!value && passed) { passed = false; failure = message; } }
        private void FinishFailure() { File.WriteAllText(Path.Combine(folder, "smoke.txt"), "Passed: False\n" + completedReport + "Failure: " + failure + "\n"); Debug.LogError("EMBERFIELD_OFFLINE_SMOKE " + failure); Application.Quit(1); }
    }
}
