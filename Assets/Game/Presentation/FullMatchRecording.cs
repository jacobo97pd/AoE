using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Emberfield.Diagnostics;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Opt-in recorded full match (-emberfieldFullMatch folder). A scripted player drives the local
    /// seat with ordinary commands against the game's own AI, from the first tick to the result.
    /// Every tick is presented; frames are kept for a time-lapse of the whole match and, while the
    /// scripted army fights, for real-time battle footage. Options: -emberfieldMatchMap,
    /// -emberfieldMatchFaction and -emberfieldMatchMode.
    /// </summary>
    public static class FullMatchRecording
    {
        private const string Flag = "-emberfieldFullMatch";
        private static bool requested, running;
        private static string folder, mapId = "sapphire_coast", factionId = "aven";
        private static VictoryMode mode = VictoryMode.Conquest;
        private static OfflineAiDifficulty difficulty = OfflineAiDifficulty.Normal;

        [Serializable] private sealed class Report
        {
            public string buildGuid, map, faction, rival, mode, difficulty, reason, failure;
            public bool finished, passed;
            public int winner, frames, battleFrames, commanderAccepted, commanderRejected, commanderLosses, deaths;
            public int rivalGatherOrders, rivalBuildings, rivalUnitsQueued, rivalAttacks, rivalRejected;
            public long ticks;
            public double simulatedSeconds, wallSeconds;
            public List<string> timeline = new List<string>();
            public List<string> stills = new List<string>();
        }

        private static string Argument(string[] args, string name, string fallback)
        { int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }

        public static void TryStart(MatchController match)
        {
            if (running || !Debug.isDebugBuild) return;
            var args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, Flag);
            if (flag < 0 || flag + 1 >= args.Length) return;
            folder = Path.GetFullPath(args[flag + 1]);
            mapId = Argument(args, "-emberfieldMatchMap", mapId);
            factionId = Argument(args, "-emberfieldMatchFaction", factionId);
            mode = (VictoryMode)Enum.Parse(typeof(VictoryMode), Argument(args, "-emberfieldMatchMode", mode.ToString()), true);
            difficulty = (OfflineAiDifficulty)Enum.Parse(typeof(OfflineAiDifficulty), Argument(args, "-emberfieldMatchDifficulty", difficulty.ToString()), true);
            Application.runInBackground = true;
            var world = match.World;
            bool ready = world.Match != null && !world.IsNetworkReplica && world.Map.Id == mapId && world.Match.Mode == mode &&
                world.TickIndex == 0 && match.Factions.LocalDefinition != null && match.Factions.LocalDefinition.Id == factionId &&
                match.OfflineControls.Ai != null && match.OfflineControls.Ai.Difficulty == difficulty;
            if (!ready)
            {
                // Open the requested skirmish exactly as the Play menu does, then start on the new scene.
                if (requested) { Debug.LogError("EMBERFIELD_FULL_MATCH could not open " + mapId + "/" + factionId + "/" + mode + "/" + difficulty); Application.Quit(1); return; }
                requested = true; match.StartOfflineMatch(factionId, mode, mapId, difficulty); return;
            }
            running = true;
            match.StartCoroutine(Run(match));
        }

        private static bool Milestone(string text) =>
            text.StartsWith("Muster Hall terminado", StringComparison.Ordinal) || text.StartsWith("Nueva era", StringComparison.Ordinal) ||
            text.StartsWith("Ejército formado", StringComparison.Ordinal) || text.StartsWith("Ataque", StringComparison.Ordinal) ||
            text.StartsWith("Hearth enemigo", StringComparison.Ordinal) || text.StartsWith("Retirada", StringComparison.Ordinal) ||
            text.StartsWith("Primer Crimson", StringComparison.Ordinal) || text.StartsWith("Primer Corsario", StringComparison.Ordinal);

        private static string Clock(long tick) => (tick / (World.TickRate * 60)).ToString("00") + ":" + (tick / World.TickRate % 60).ToString("00");

        private static IEnumerator Run(MatchController match)
        {
            yield return null;
            match.enabled = false;
            match.Shell?.Close(); match.OfflineControls.Close();
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            Directory.CreateDirectory(folder);
            var frames = Path.Combine(folder, "frames"); Directory.CreateDirectory(frames);
            var world = match.World;
            var report = new Report { buildGuid = Application.buildGUID, map = mapId, faction = factionId, mode = mode.ToString(), difficulty = difficulty.ToString() };
            world.TryGetPlayer(2, out var rivalPlayer); report.rival = rivalPlayer?.FactionId;
            var commander = new ScriptedCommander(world, MatchController.LocalPlayer);
            var args = Environment.GetCommandLineArgs();
            if (int.TryParse(Argument(args, "-emberfieldMatchWave", ""), out int wave)) commander.FirstWaveSize = wave;
            if (int.TryParse(Argument(args, "-emberfieldMatchEarliest", ""), out int earliest)) commander.EarliestAttackTick = earliest * (long)World.TickRate;
            var recorder = new MatchRecorder(frames);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            match.Rig.MinimumZoom = 5;
            string plan = null; int shown = 0;
            long deadline = World.TickRate * 60L * 40;
            Exception failure = null;
            while (!world.Match.IsFinished && world.TickIndex < deadline && failure == null)
            {
                try
                {
                    commander.Tick();
                    match.AdvanceSimulationTick();
                    if (commander.Plan != plan) { plan = commander.Plan; match.SetFeedback(plan); }
                    bool battle = commander.InCombat;
                    var focus = DefinitionLoader.ToWorld(commander.Focus);
                    match.Rig.SetHome(focus, battle ? 7 : commander.ArmyCount >= 4 ? 9 : 10);
                    if (battle)
                    {
                        match.SyncPresentation(.5f); recorder.Render(match, world.TickIndex, .5f, "battle");
                        match.SyncPresentation(1); recorder.Render(match, world.TickIndex, 1, "battle");
                    }
                    else
                    {
                        match.SyncPresentation(1);
                        if (world.TickIndex % 4 == 0) recorder.Render(match, world.TickIndex, 1, "overview");
                    }
                    while (shown < commander.Timeline.Count)
                    {
                        var entry = commander.Timeline[shown++];
                        report.timeline.Add(Clock(entry.Tick) + " " + entry.Text);
                        if (!Milestone(entry.Text)) continue;
                        string still = "still-" + entry.Tick.ToString("D6") + ".png";
                        recorder.Still(match, Path.Combine(folder, still)); report.stills.Add(still + " " + Clock(entry.Tick) + " " + entry.Text);
                    }
                }
                catch (Exception error) { failure = error; }
                if (world.TickIndex % 5 == 0) yield return null;
            }
            report.finished = world.Match.IsFinished; report.winner = world.Match.WinnerId; report.reason = world.Match.Reason.ToString();
            report.ticks = world.TickIndex; report.simulatedSeconds = world.TickIndex / (double)World.TickRate;
            if (report.finished)
            {
                report.timeline.Add(Clock(world.TickIndex) + " Fin de la partida: " + (world.Match.WinnerId == MatchController.LocalPlayer ? "victoria" : "derrota") + " por " + world.Match.Reason);
                // The real result panel, held for a few seconds at the end of the footage.
                match.OfflineControls.Observe(); match.Hud.Invalidate(); match.SyncPresentation(1);
                yield return null; match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
                for (int i = 0; i < 90; i++) { recorder.Render(match, world.TickIndex, 1, "result"); if (i % 15 == 0) yield return null; }
                recorder.Still(match, Path.Combine(folder, "still-result.png")); report.stills.Add("still-result.png " + Clock(world.TickIndex) + " Resultado");
            }
            recorder.Close();
            var rival = match.OfflineControls.Ai?.Statistics;
            report.frames = recorder.Count; report.battleFrames = recorder.BattleFrames;
            report.commanderAccepted = commander.Accepted; report.commanderRejected = commander.Rejected; report.commanderLosses = commander.Losses;
            report.deaths = world.DeathCount; report.wallSeconds = watch.Elapsed.TotalSeconds;
            if (rival != null) { report.rivalGatherOrders = rival.GatherOrders; report.rivalBuildings = rival.BuildingsStarted; report.rivalUnitsQueued = rival.UnitsQueued; report.rivalAttacks = rival.AttackOrders; report.rivalRejected = rival.Rejected; }
            report.failure = failure?.ToString();
            report.passed = failure == null && report.finished;
            File.WriteAllText(Path.Combine(folder, "full-match.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("EMBERFIELD_FULL_MATCH " + report.passed + " winner=" + report.winner + " reason=" + report.reason + " seconds=" + report.simulatedSeconds.ToString(CultureInfo.InvariantCulture) + " frames=" + report.frames);
            Application.Quit(report.passed ? 0 : 1);
        }

        /// <summary>Offscreen renders of the real camera and HUD, with a camera that eases between framings.</summary>
        private sealed class MatchRecorder
        {
            private readonly string folder;
            private readonly StreamWriter manifest;
            private RenderTexture target;
            private Texture2D pixels;
            private bool placed;
            private Vector3 position;
            private Quaternion rotation;
            private float size;
            private double lastTime;
            public int Count { get; private set; }
            public int BattleFrames { get; private set; }

            public MatchRecorder(string folder)
            {
                this.folder = folder;
                manifest = new StreamWriter(Path.Combine(folder, "frames.csv"), false, new UTF8Encoding(false));
                manifest.WriteLine("index,tick,alpha,mode");
            }

            public void Render(MatchController match, long tick, float alpha, string mode)
            {
                double time = (tick + alpha) * (double)World.TickSeconds;
                float seconds = placed ? (float)Math.Max(0, time - lastTime) : 0; lastTime = time;
                var camera = match.Rig.Camera; var view = camera.transform;
                Vector3 goal = view.position; float goalSize = camera.orthographicSize;
                if (!placed || Quaternion.Angle(rotation, view.rotation) > .5f) { position = goal; size = goalSize; rotation = view.rotation; placed = true; }
                else
                {
                    float ease = 1 - Mathf.Exp(-seconds / .45f);
                    position = Vector3.Lerp(position, goal, ease); size = Mathf.Lerp(size, goalSize, ease);
                }
                view.position = position; camera.orthographicSize = size;
                Draw(match);
                view.position = goal; camera.orthographicSize = goalSize;
                File.WriteAllBytes(Path.Combine(folder, "frame-" + Count.ToString("D6") + ".jpg"), pixels.EncodeToJPG(88));
                manifest.WriteLine(Count + "," + tick + "," + alpha.ToString(CultureInfo.InvariantCulture) + "," + mode);
                Count++; if (mode == "battle") BattleFrames++;
            }

            public void Still(MatchController match, string path)
            {
                var camera = match.Rig.Camera; var view = camera.transform;
                Vector3 goal = view.position; float goalSize = camera.orthographicSize;
                if (placed) { view.position = position; camera.orthographicSize = size; }
                Draw(match);
                view.position = goal; camera.orthographicSize = goalSize;
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }

            private void Draw(MatchController match)
            {
                if (target == null)
                {
                    target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
                    pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                }
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(match.Rig.Camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                var previous = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                RenderTexture.active = previous;
            }

            public void Close() => manifest.Dispose();
        }
    }
}
