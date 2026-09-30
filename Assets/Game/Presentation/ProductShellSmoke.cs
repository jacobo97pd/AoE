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
    // Explicit packaged acceptance driver. Normal launches never enter this code.
    public static class ProductShellSmoke
    {
        [Serializable] private sealed class Report
        {
            public bool Passed, MainMenuPauses, StoreHasNoTransactions, StartedChosenMatch, GameplayObserved;
            public string BuildGuid, UnityVersion, Failure = "";
            public bool IsolatedStorage;
            public string LocalStorageRoot;
            public int Width, Height, VisibleButtonClicks;
            public long FinalTick;
            public int FrameSamples, TargetFrameRate;
            public double FramesPerSecond, FrameP95Milliseconds, LocalTickP95Milliseconds;
            // Whole-session pacing (see AlphaMatchMetrics.SessionSamples), not just the ~5 s rolling window above.
            // Real frames are sampled for the first 3 s of the match and the final 8 s; the fast-forward between runs
            // with MatchController disabled and is not observed by these fields. The loading frame is never sampled.
            public bool SessionFrameSampleOverflow;
            public int SessionFrameSamples, SessionFrameHitchCount;
            public double SessionFrameP99Milliseconds, SessionFrameP999Milliseconds, SessionFrameMaxMilliseconds, SessionFrameStdDevMilliseconds, SessionFrameHitchThresholdMilliseconds;
            public double SessionTickP99Milliseconds, SessionTickMaxMilliseconds;
            // The slowest frames of the real-time window, each with what its Update did, so a hitch can be named. Frame
            // -1 is the frame the window opens in: MatchController's first sample of it is that frame's length.
            public List<FrameRecord> SlowestFrames = new List<FrameRecord>();
            // The same for the first three real seconds of the match, right after its loading frame.
            public List<FrameRecord> StartSlowestFrames = new List<FrameRecord>();
            // The frame that loaded the match as the clock first reported it, and from the Begin click until the start
            // window's last frame still loading or over 25 ms: how long the player waits for a steady match.
            public double LoadingFrameMilliseconds, ClickToSteadyMilliseconds;
            // Each fast-forward frame runs 20 ticks; what it costs beyond them is the view building and drawing whatever
            // the match has just produced, which is where a first-use stall of a new unit or building type lands.
            public double FastForwardOverheadP50Milliseconds, FastForwardOverheadMaxMilliseconds;
            public int FastForwardFramesOver33Milliseconds;
            // How the match paced its frames (FramePacing) and what its load-time prewarm read and cost.
            public int VSyncCount, PrewarmedModels;
            public double PrewarmMilliseconds;
            public List<FrameRecord> FastForwardSlowestFrames = new List<FrameRecord>();
        }
        [Serializable] private sealed class FrameRecord
        {
            public int Frame, Ticks, Created, GcCollections;
            // Still the load reaching the clock (MatchController.IsLoading): not sampled by the match's metrics.
            public bool Loading;
            public long Tick;
            // SimulationMilliseconds covers the frame's ticks with the AI's thinking; WorldTickMilliseconds is World.Tick alone, last tick.
            // PresentationMilliseconds is all of SyncPresentation; ViewMilliseconds the world view's share, the rest mostly the HUD.
            public double Milliseconds, SimulationMilliseconds, WorldTickMilliseconds, PresentationMilliseconds, ViewMilliseconds;
        }
        private const int SlowestKept = 8;
        private static int stage;
        private static float clickedBegin;
        private static Report report;
        private static string folder;
        public static void TryStart(MatchController match)
        {
            if (!Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-emberfieldProductSmoke");
            if (index < 0 || index + 1 >= args.Length) return;
            folder = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(folder);
            report = report ?? new Report { BuildGuid = Application.buildGUID, UnityVersion = Application.unityVersion, Width = Screen.width, Height = Screen.height };
            report.IsolatedStorage = NativeSmokeStorage.IsIsolated; report.LocalStorageRoot = NativeSmokeStorage.Root;
            if (!report.IsolatedStorage) { report.Failure = "Product smoke requires isolated local storage."; Finish(false); return; }
            Application.runInBackground = true;
            match.StartCoroutine(Guard(stage == 0 ? Frontend(match) : Gameplay(match)));
        }
        private static IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception error)
                { report.Failure = error.GetType().Name + ": " + error.Message; Finish(false); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
        }
        private static IEnumerator Frontend(MatchController match)
        {
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            match.Shell.Open(); long tick = match.World.TickIndex;
            yield return new WaitForSecondsRealtime(.2f);
            report.MainMenuPauses = match.World.TickIndex == tick; Require(report.MainMenuPauses, "Main menu advanced local rules.");
            yield return Capture(match, "main-menu.png");
            yield return Click(match, "Navigation Store"); yield return null;
            report.StoreHasNoTransactions = match.Shell.HasCatalog && !match.Shell.PurchasesAvailable && match.Shell.Page == "store";
            Require(report.StoreHasNoTransactions, "Store state mismatch."); yield return Capture(match, "store.png");
            yield return Click(match, "Navigation Settings"); yield return null; yield return Capture(match, "settings.png");
            yield return Click(match, "Navigation Home"); yield return null;
            yield return Click(match, "Play online"); yield return null; Require(match.Online.IsOpen && !match.Shell.IsOpen, "Online entry did not open.");
            yield return Capture(match, "multiplayer.png"); match.Online.Close(); yield return null;
            match.Shell.Open(); yield return Click(match, "Navigation Play"); yield return null;
            yield return Click(match, "Select serevin"); yield return null;
            yield return Click(match, "Select aven"); yield return null;
            yield return Click(match, "Choose Dominion"); yield return null;
            yield return Click(match, "Choose Conquest"); yield return null;
            yield return Click(match, "Choose difficulty"); yield return null; // Normal -> Hard
            yield return Capture(match, "skirmish-setup.png");
            stage = 1; clickedBegin = Time.realtimeSinceStartup; yield return Click(match, "Begin skirmish");
        }
        private static IEnumerator Gameplay(MatchController match)
        {
            // The loading frame itself, with WorldView's prewarm standing under the ground: nothing of it may show. Only on
            // request, since the capture's readback and PNG write would lengthen the load this smoke times.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-emberfieldCaptureLoadingFrame") >= 0) yield return Capture(match, "loading-frame.png");
            yield return null; match.OfflineControls.Close();
            report.LoadingFrameMilliseconds = Time.unscaledDeltaTime * 1000.0; report.ClickToSteadyMilliseconds = (Time.realtimeSinceStartup - clickedBegin) * 1000.0;
            // The first real seconds of the fresh match, played at the real pace before the fast-forward takes over.
            yield return Observe(match, 3, report.StartSlowestFrames, true);
            match.enabled = false;
            report.StartedChosenMatch = match.World.Match != null && match.World.Match.Mode == VictoryMode.Conquest && match.Factions.LocalDefinition.Id == "aven" && !match.Shell.IsOpen &&
                match.OfflineControls.Ai != null && match.OfflineControls.Ai.Difficulty == OfflineAiDifficulty.Hard;
            Require(report.StartedChosenMatch, "Selected faction/mode/difficulty were not started.");
            match.Select(new[] { match.World.Units[0].Id }); match.SyncPresentation(1);
            yield return Capture(match, "match-start.png");
            // The capture's own readback and PNG write must not be counted as the first fast-forward frame.
            yield return null;
            var local = new OfflineAi(match.World, 1);
            var watch = new System.Diagnostics.Stopwatch(); var overheads = new List<double>();
            for (int frame = 0; frame < 120; frame++)
            {
                int collections = GC.CollectionCount(0);
                watch.Restart();
                for (int step = 0; step < 20; step++) { local.Tick(); match.AdvanceSimulationTick(); }
                double ticks = watch.Elapsed.TotalMilliseconds;
                watch.Restart(); match.SyncPresentation(1); double view = watch.Elapsed.TotalMilliseconds;
                var record = new FrameRecord { Frame = frame, Tick = match.World.TickIndex, Ticks = 20, SimulationMilliseconds = ticks, PresentationMilliseconds = view, Created = match.View.CreatedLastSync };
                yield return null;
                // Resumed after the next Update: unscaledDeltaTime is the whole of the frame above, drawing included.
                record.Milliseconds = Time.unscaledDeltaTime * 1000.0 - ticks; record.GcCollections = GC.CollectionCount(0) - collections;
                overheads.Add(record.Milliseconds); Keep(report.FastForwardSlowestFrames, record);
                if (record.Milliseconds > 33) report.FastForwardFramesOver33Milliseconds++;
            }
            overheads.Sort(); report.FastForwardOverheadP50Milliseconds = overheads[overheads.Count / 2]; report.FastForwardOverheadMaxMilliseconds = overheads[overheads.Count - 1];
            match.Rig.Home(); match.ClearSelection(); match.SyncPresentation(1);
            report.GameplayObserved = match.World.TickIndex >= 2400 && match.Metrics.Workers >= 4;
            Require(report.GameplayObserved, "The local match did not develop.");
            match.enabled = true;
            // A diagnostic run can record where these real frames' allocations come from; its timings are then not comparable.
            AllocationCapture.Begin(Path.Combine(folder, "allocations.raw"), AllocationCapture.RequestedFrames(Environment.GetCommandLineArgs()));
            yield return Observe(match, 8, report.SlowestFrames);
            AllocationCapture.Stop();
            report.FrameSamples = match.Metrics.FrameSamples; report.TargetFrameRate = Application.targetFrameRate; report.VSyncCount = QualitySettings.vSyncCount;
            report.PrewarmedModels = match.View.PrewarmedModels; report.PrewarmMilliseconds = match.View.PrewarmMilliseconds;
            report.FramesPerSecond = match.Metrics.FramesPerSecond; report.FrameP95Milliseconds = match.Metrics.FrameP95Milliseconds;
            report.LocalTickP95Milliseconds = match.Metrics.TickP95Milliseconds;
            report.SessionFrameSamples = match.Metrics.SessionFrameSamples; report.SessionFrameSampleOverflow = match.Metrics.SessionFrameSampleOverflow;
            report.SessionFrameP99Milliseconds = match.Metrics.SessionFrameP99Milliseconds; report.SessionFrameP999Milliseconds = match.Metrics.SessionFrameP999Milliseconds;
            report.SessionFrameMaxMilliseconds = match.Metrics.SessionFrameMaxMilliseconds; report.SessionFrameStdDevMilliseconds = match.Metrics.SessionFrameStdDevMilliseconds;
            report.SessionFrameHitchThresholdMilliseconds = match.Metrics.SessionFrameHitchThresholdMilliseconds; report.SessionFrameHitchCount = match.Metrics.SessionFrameHitchCount;
            report.SessionTickP99Milliseconds = match.Metrics.SessionTickP99Milliseconds; report.SessionTickMaxMilliseconds = match.Metrics.SessionTickMaxMilliseconds;
            Require(report.FrameSamples >= 100 && report.FramesPerSecond > 0, "Gameplay timing window was not collected.");
            yield return Capture(match, "settlement.png");
            match.Research.Open(); match.Hud.Invalidate(); match.SyncPresentation(1); yield return Capture(match, "research.png"); match.Research.Close();
            match.Alpha.Open(); match.Hud.Invalidate(); match.SyncPresentation(1); yield return Capture(match, "in-game-settings.png"); match.Alpha.Close();
            report.FinalTick = match.World.TickIndex; Finish(true);
        }
        // Real frames at the real cap, each paired with what its own Update did: the coroutine resumes after Update, when
        // unscaledDeltaTime is still the length of the frame before, so a record is timed on the following resume.
        private static IEnumerator Observe(MatchController match, float seconds, List<FrameRecord> slowest, bool start = false)
        {
            float end = Time.realtimeSinceStartup + seconds; int frame = -1, collections = GC.CollectionCount(0);
            // Frame -1 is the one this starts in, whose Update has already run.
            var last = Record(match, frame++, 0);
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                AllocationCapture.Frame();
                last.Milliseconds = Time.unscaledDeltaTime * 1000.0; last.Loading = match.IsLoading; Keep(slowest, last);
                // From the click to the last frame still loading or over one refresh and a half: the wait for a steady match.
                if (start && (last.Milliseconds > 25 || last.Loading)) report.ClickToSteadyMilliseconds = (Time.realtimeSinceStartup - clickedBegin) * 1000.0;
                int now = GC.CollectionCount(0);
                last = Record(match, frame++, now - collections);
                collections = now;
            }
        }
        private static FrameRecord Record(MatchController match, int frame, int collections) => new FrameRecord
        {
            Frame = frame, Tick = match.World.TickIndex, Ticks = match.LastFrameTicks, SimulationMilliseconds = match.LastFrameSimulationMilliseconds, WorldTickMilliseconds = match.LastTickMilliseconds,
            PresentationMilliseconds = match.LastPresentationMilliseconds, ViewMilliseconds = match.LastViewMilliseconds, Created = match.View.CreatedLastSync, GcCollections = collections,
        };
        private static void Keep(List<FrameRecord> slowest, FrameRecord record)
        {
            int at = slowest.FindIndex(kept => kept.Milliseconds < record.Milliseconds);
            if (at < 0) at = slowest.Count;
            if (at < SlowestKept) slowest.Insert(at, record);
            if (slowest.Count > SlowestKept) slowest.RemoveAt(slowest.Count - 1);
        }
        private static IEnumerator Click(MatchController match, string name)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame(); Button target = null;
            foreach (var button in match.GetComponentsInChildren<Button>()) if (button.name == name && button.isActiveAndEnabled) { target = button; break; }
            Require(target != null && target.interactable, "Missing active control: " + name);
            var rect = (RectTransform)target.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == target,
                "Control covered or outside viewport: " + name + " at " + pointer.position + "; hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + "; canvas=" + target.GetComponentInParent<Canvas>().renderMode);
            report.VisibleButtonClicks++; ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static IEnumerator Capture(MatchController match, string name)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            // Hidden Windows players may have no readable swapchain. Render the same camera
            // and native canvases explicitly, then restore their normal overlay configuration.
            var canvases = match.GetComponentsInChildren<Canvas>();
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            var orders = new int[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode; cameras[i] = canvases[i].worldCamera; distances[i] = canvases[i].planeDistance; orders[i] = canvases[i].sortingOrder;
                if (modes[i] != RenderMode.ScreenSpaceOverlay) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = match.Rig.Camera; canvases[i].planeDistance = 1;
            }
            try
            {
                Canvas.ForceUpdateCanvases();
                Require(PlayerSmoke.Capture(match, Path.Combine(folder, name)), "Native camera/UI capture was empty.");
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; canvases[i].sortingOrder = orders[i]; }
                Canvas.ForceUpdateCanvases();
            }
        }
        private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
        private static void Finish(bool passed)
        {
            report.Passed = passed; File.WriteAllText(Path.Combine(folder, "product-smoke.json"), JsonUtility.ToJson(report, true));
            if (!passed) Debug.LogError("EMBERFIELD_PRODUCT_SMOKE " + report.Failure);
            SafeQuit.Request(passed ? 0 : 1);
        }
    }
}
