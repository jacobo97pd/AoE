using System;
using System.Collections;
using System.IO;
using Emberfield.Diagnostics;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Explicit development-player diagnostics. Uses real-time simulation and serialized offscreen rendering.
    public static class MovementPlayerProbe
    {
        public static bool TryStart(MatchController match, string[] args)
        {
            string folder = Argument(args, "-emberfieldMovementProbe");
            if (folder == null) return false;
            int count = int.TryParse(Argument(args, "-stressCount"), out int parsed) ? parsed : 100;
            var kind = Enum.TryParse(Argument(args, "-stressScenario"), out MovementScenarioKind scenario) ? scenario : MovementScenarioKind.OpenField;
            float seconds = float.TryParse(Argument(args, "-stressSeconds"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float duration) ? Mathf.Clamp(duration, 10, 90) : 20;
            Application.runInBackground = true;
            if (match.Stress == null || match.Stress.Scenario.RequestedUnits != count || match.Stress.Scenario.Kind != kind)
                match.ReloadStress(count, kind);
            else match.StartCoroutine(Run(match, folder, seconds));
            return true;
        }

        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static IEnumerator Run(MatchController match, string folder, float seconds)
        {
            Directory.CreateDirectory(folder);
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            var renderTarget = new RenderTexture(Screen.width, Screen.height, 24);
            renderTarget.Create();
            match.Rig.Camera.targetTexture = renderTarget;
            match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            // Hidden Windows players can skip automatic rendering entirely. Explicitly submit each
            // camera frame and complete a one-pixel readback, preventing CPU-only loop FPS claims.
            match.Rig.Camera.enabled = false;
            var completionPixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var renderRequest = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = renderTarget };
            void RenderFrame()
            {
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(match.Rig.Camera, renderRequest);
                var previous = RenderTexture.active;
                RenderTexture.active = renderTarget;
                completionPixel.ReadPixels(new Rect(renderTarget.width / 2, renderTarget.height / 2, 1, 1), 0, 0, false);
                RenderTexture.active = previous;
            }
            var preparation = System.Diagnostics.Stopwatch.StartNew();
            while (preparation.Elapsed.TotalSeconds < 2) { RenderFrame(); yield return null; }
            match.Stress.FollowCamera = true;
            match.StartStress();
            long startTick = match.World.TickIndex;
            int renderedCameraFrames = 0;
            int sampledRenderedCameraFrames = 0, lastRenderedFrame = -1;
            void OnCameraRendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
            {
                if (camera != match.Rig.Camera || lastRenderedFrame == Time.frameCount) return;
                lastRenderedFrame = Time.frameCount; renderedCameraFrames++;
                if (match.Stress.SamplingCurrentFrame) sampledRenderedCameraFrames++;
            }
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnCameraRendered;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // A diagnostic run can record where the sampled frames' allocations come from; its timings are then not comparable.
            int captureFrames = AllocationCapture.RequestedFrames(Environment.GetCommandLineArgs());
            // Session excludes its first three seconds from frame samples.
            while (watch.Elapsed.TotalSeconds < seconds + 3 && match.Stress.Running)
            {
                if (captureFrames > 0 && match.Stress.SamplingCurrentFrame) { AllocationCapture.Begin(Path.Combine(folder, "allocations.raw"), captureFrames); captureFrames = 0; }
                RenderFrame(); yield return null;
                AllocationCapture.Frame();
            }
            watch.Stop();
            AllocationCapture.Stop();
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            var summary = match.Stress.Summarize();
            double simulationSeconds = (match.World.TickIndex - startTick) / (double)Emberfield.Simulation.World.TickRate;
            double simulationRate = simulationSeconds / watch.Elapsed.TotalSeconds;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var report = new MovementPlayerReport
            {
                Utc = DateTime.UtcNow.ToString("O"), Unity = Application.unityVersion, BuildGuid = Application.buildGUID,
                OperatingSystem = SystemInfo.operatingSystem, Cpu = SystemInfo.processorType,
                LogicalProcessors = SystemInfo.processorCount, MemoryMegabytes = SystemInfo.systemMemorySize,
                Gpu = SystemInfo.graphicsDeviceName, GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsMemoryMegabytes = SystemInfo.graphicsMemorySize,
                Width = Screen.width, Height = Screen.height, Quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                RenderPipeline = pipeline == null ? "Unknown" : pipeline.name,
                RenderScale = pipeline == null ? -1 : pipeline.renderScale,
                MsaaSamples = pipeline == null ? -1 : pipeline.msaaSampleCount,
                VSyncCount = QualitySettings.vSyncCount, TargetFrameRate = Application.targetFrameRate,
                DevelopmentBuild = Debug.isDebugBuild, BatchPlayer = Application.isBatchMode,
                FixtureVersion = MovementScenarioFactory.FixtureVersion,
                WarmupSeconds = 3, RequestedSampleSeconds = seconds, ActualRunSeconds = watch.Elapsed.TotalSeconds,
                CompletedBeforeRequestedWindow = !match.Stress.Running && watch.Elapsed.TotalSeconds < seconds + 3,
                SimulationTicks = match.World.TickIndex - startTick, EntityViews = match.View.Count,
                RenderedCameraFrames = renderedCameraFrames,
                SampledRenderedCameraFrames = sampledRenderedCameraFrames,
                SimulationSeconds = simulationSeconds, SimulationToWallRatio = simulationRate,
                Sample = summary,
                // The coroutine can end after Update and before this final frame renders: permit that one lifecycle offset.
                Passed = !Application.isBatchMode && summary.FrameSamples >= 120 && sampledRenderedCameraFrames >= summary.FrameSamples - 1 &&
                    simulationRate >= .95 && simulationRate <= 1.05 &&
                    summary.TickSamples >= 200 && summary.MaxOverlapPairs == 0 && summary.InvalidPositions == 0
            };
            // Full screenshot readback and PNG encoding happen after timing; per-frame one-pixel readback is measured.
            match.Stress.FollowCamera = false;
            match.Hud.Refresh();
            match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
            yield return new WaitForEndOfFrame();
            report.CaptureHasPixels = PlayerSmoke.CaptureTarget(renderTarget, Path.Combine(folder, "stress.png"));
            match.Rig.Camera.targetTexture = null;
            UnityEngine.Object.Destroy(completionPixel);
            renderTarget.Release(); UnityEngine.Object.Destroy(renderTarget);
            report.Passed &= report.CaptureHasPixels;
            File.WriteAllText(Path.Combine(folder, "movement-player.json"), JsonUtility.ToJson(report, true));
            Debug.Log("EMBERFIELD_MOVEMENT_PLAYER " + report.Passed);
            Application.Quit(report.Passed ? 0 : 1);
        }
    }

    [Serializable]
    public sealed class MovementPlayerReport
    {
        public bool Passed, DevelopmentBuild, BatchPlayer, CaptureHasPixels, CompletedBeforeRequestedWindow;
        public string Utc, Unity, BuildGuid, OperatingSystem, Cpu, Gpu, GraphicsApi, Quality, FixtureVersion, RenderPipeline;
        public string Method = "Hidden non-batch D3D development player; one explicit URP camera/HUD render per Unity loop to a screen-sized texture, followed by synchronous one-pixel GPU readback. Frame times INCLUDE render-request, Canvas update and GPU synchronization overhead: conservative serialized offscreen throughput, not ordinary display FPS. Unique endCameraRendering frames match the sample window (one final lifecycle offset allowed). Uncapped 20 Hz simulation must stay within 5% of wall time. Frame deltas exclude 3 s warmup; tick/navigation include warmup. All mover bounds framed; observer every 2 ticks. Full PNG capture excluded. No display-present latency, isolated GPU timing or physical mobile measurement.";
        public int LogicalProcessors, MemoryMegabytes, GraphicsMemoryMegabytes, Width, Height, VSyncCount, TargetFrameRate, EntityViews, RenderedCameraFrames, SampledRenderedCameraFrames, MsaaSamples;
        public float RenderScale;
        public long SimulationTicks;
        public double WarmupSeconds, RequestedSampleSeconds, ActualRunSeconds, SimulationSeconds, SimulationToWallRatio;
        public MovementFrameSummary Sample;
    }
}
