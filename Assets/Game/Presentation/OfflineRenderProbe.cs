using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Emberfield.Simulation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Emberfield.Presentation
{
    // Opt-in development measurement. Never participates in ordinary player input or AI decisions.
    public static class OfflineRenderProbe
    {
        public const string FixtureVersion = "amber-crossing-aven-dominion-natural-ai-6000-v1";
        public static bool IsRunning { get; private set; }

        public static bool TryStart(MatchController match, string[] args)
        {
            string folder = Argument(args, "-emberfieldOfflineProfile");
            if (folder == null || !Debug.isDebugBuild) return false;
            IsRunning = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            match.enabled = false;
            match.StartCoroutine(Run(match, folder));
            return true;
        }

        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        private static IEnumerator Run(MatchController match, string folder)
        {
            const int prewarmTicks = 6000, warmupTicks = 60, sampleTicks = 400;
            const double warmupSeconds = 3, sampleSeconds = 20;
            Directory.CreateDirectory(folder);
            var world = match.World;
            if (Application.isBatchMode || world.Match == null || world.Map.Id != "amber_crossing" ||
                world.Match.Mode != VictoryMode.Dominion || world.TickIndex != 0 ||
                !world.TryGetPlayer(1, out var local) || local.FactionId != "aven")
            {
                Debug.LogError("Offline profile requires a fresh non-batch Aven Dominion match on Amber Crossing.");
                IsRunning = false; Application.Quit(1); yield break;
            }
            while (!SplashScreen.isFinished) yield return null;
            match.OfflineControls.Close();
            // A forced phone tier is measured at its starting render scale: dynamic resolution would chase this
            // desktop's frame times and make runs incomparable.
            var mobile = match.GetComponent<MobileQuality>();
            if (mobile != null) mobile.enabled = false;
            var first = new OfflineAi(world, 1);
            var second = match.OfflineControls.Ai;
            void AiTick()
            {
                if (world.TickIndex % 20 < 10) { first.Tick(); second.Tick(); }
                else { second.Tick(); first.Tick(); }
            }
            void ObservedTick()
            {
                match.Metrics?.BeginTick(world);
                long started = Stopwatch.GetTimestamp(); world.Tick();
                match.Metrics?.ObserveTick(world, Milliseconds(started));
            }
            while (world.TickIndex < prewarmTicks && !world.Match.IsFinished)
            {
                for (int i = 0; i < 200 && world.TickIndex < prewarmTicks && !world.Match.IsFinished; i++)
                { AiTick(); ObservedTick(); }
                yield return null;
            }
            if (world.TickIndex != prewarmTicks || world.Match.IsFinished)
            {
                Debug.LogError("Natural offline fixture ended before its fixed checkpoint.");
                IsRunning = false; Application.Quit(1); yield break;
            }
            match.ClearSelection();
            match.Rig.SetHome(new Vector3(48, 0, 36), 22);
            // Every unit/building type reached by tick 6000 gets its first Resources.Load/Instantiate/shader-variant
            // compile here, all at once and outside every timed window below: a proxy for a cold first-use hitch.
            long initialSyncStarted = Stopwatch.GetTimestamp();
            match.SyncPresentation(1);
            double initialPresentationSyncMilliseconds = Milliseconds(initialSyncStarted);
            var checkpoint = Snapshot(match);
            string checkpointHash = StateHash(world);
            var renderTarget = new RenderTexture(Screen.width, Screen.height, 24);
            renderTarget.Create();
            var camera = match.Rig.Camera;
            camera.targetTexture = renderTarget;
            camera.enabled = false;
            match.Hud.PrepareOffscreenCapture(camera);
            var completionPixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var request = new RenderPipeline.StandardRequest { destination = renderTarget };
            int sampledCameraFrames = 0, lastCameraFrame = -1;
            bool recordingRender = false;
            void OnRendered(ScriptableRenderContext context, Camera rendered)
            {
                if (rendered != camera || !recordingRender || lastCameraFrame == Time.frameCount) return;
                lastCameraFrame = Time.frameCount; sampledCameraFrames++;
            }
            RenderPipelineManager.endCameraRendering += OnRendered;
            void RenderFrame()
            {
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, request);
                var previous = RenderTexture.active;
                RenderTexture.active = renderTarget;
                completionPixel.ReadPixels(new Rect(renderTarget.width / 2, renderTarget.height / 2, 1, 1), 0, 0, false);
                RenderTexture.active = previous;
            }

            // Allocate diagnostic buffers and verify counters before either timed window.
            var frames = new Samples(65536); var aiTimes = new Samples(sampleTicks);
            var tickTimes = new Samples(sampleTicks); var viewTimes = new Samples(65536);
            var renderTimes = new Samples(65536); var gpuTimes = new Samples(65536);
            var cpuMainTimes = new Samples(65536); var cpuRenderTimes = new Samples(65536);
            // Per-frame component breakdown, kept only to name which marker dominated the worst sampled frames.
            var frameBreakdowns = new List<OfflineFrameBreakdown>(4096);
            RenderFrame(); yield return null;
            var counters = CreateCounters(out var availableProfilerCounters);
            var allocation = counters[3];
            // Flush setup allocations before the explicit capability allocation.
            for (int i = 0; i < 3; i++) { RenderFrame(); yield return null; }
            var knownAllocation = new byte[8192]; knownAllocation[0] = 1;
            long allocationProbePeak = -1;
            for (int i = 0; i < 3; i++)
            {
                RenderFrame(); yield return null;
                allocationProbePeak = Math.Max(allocationProbePeak, allocation.Read());
            }
            GC.KeepAlive(knownAllocation);
            bool allocationVerified = allocationProbePeak >= knownAllocation.Length;
            if (!allocationVerified) allocation.UnavailableReason = "Known 8192-byte allocation was not observed; allocation values unavailable.";

            var watch = Stopwatch.StartNew();
            long warmupEndTick = prewarmTicks + warmupTicks;
            while ((watch.Elapsed.TotalSeconds < warmupSeconds || world.TickIndex < warmupEndTick) && watch.Elapsed.TotalSeconds < 15 && !world.Match.IsFinished)
            {
                long due = prewarmTicks + Math.Min(warmupTicks, (long)(watch.Elapsed.TotalSeconds * World.TickRate));
                for (int steps = 0; world.TickIndex < due && steps < 5; steps++) { AiTick(); ObservedTick(); }
                match.SyncPresentation(1); RenderFrame(); FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }
            double actualWarmup = watch.Elapsed.TotalSeconds;
            var initial = Snapshot(match);
            string initialHash = StateHash(world);
            long startTick = world.TickIndex;
            int startDeaths = world.DeathCount, projectilePeak = world.Projectiles.Count;
            int startThinks = first.Statistics.Thinks + second.Statistics.Thinks;
            var gcBefore = Collections();
            long heapBefore = GC.GetTotalMemory(false);
            foreach (var counter in counters) counter.ClearSamples();
            var timingBuffer = new FrameTiming[1];
            ulong lastGpuStamp = 0;
            if (FrameTimingManager.GetLatestTimings(1, timingBuffer) > 0) lastGpuStamp = timingBuffer[0].frameStartTimestamp;
            bool frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            world.NavigationMetrics.Enabled = true; world.NavigationMetrics.Reset();
            // A diagnostic run can record where the window's allocations come from; its timings are then not comparable.
            AllocationCapture.Begin(Path.Combine(folder, "allocations.raw"), AllocationCapture.RequestedFrames(Environment.GetCommandLineArgs()));
            watch.Restart();
            while ((watch.Elapsed.TotalSeconds < sampleSeconds || world.TickIndex < startTick + sampleTicks) &&
                watch.Elapsed.TotalSeconds < sampleSeconds + 2 && !world.Match.IsFinished)
            {
                long frameStarted = Stopwatch.GetTimestamp();
                long due = startTick + Math.Min(sampleTicks, (long)(watch.Elapsed.TotalSeconds * World.TickRate));
                double frameTickMilliseconds = 0, frameAiMilliseconds = 0;
                for (int steps = 0; world.TickIndex < due && steps < 5; steps++)
                {
                    long started = Stopwatch.GetTimestamp(); AiTick(); double aiMilliseconds = Milliseconds(started); aiTimes.Add(aiMilliseconds); frameAiMilliseconds += aiMilliseconds;
                    match.Metrics?.BeginTick(world);
                    started = Stopwatch.GetTimestamp(); world.Tick(); double tickMilliseconds = Milliseconds(started); tickTimes.Add(tickMilliseconds); frameTickMilliseconds += tickMilliseconds;
                    match.Metrics?.ObserveTick(world,tickMilliseconds);
                    projectilePeak = Math.Max(projectilePeak, world.Projectiles.Count);
                }
                double remainder = watch.Elapsed.TotalSeconds * World.TickRate - (world.TickIndex - startTick);
                long viewStarted = Stopwatch.GetTimestamp();
                match.SyncPresentation((float)Math.Max(0, Math.Min(1, remainder)));
                double viewMilliseconds = Milliseconds(viewStarted); viewTimes.Add(viewMilliseconds);
                recordingRender = true;
                long renderStarted = Stopwatch.GetTimestamp(); RenderFrame(); double renderMilliseconds = Milliseconds(renderStarted); renderTimes.Add(renderMilliseconds);
                recordingRender = false;
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                AllocationCapture.Frame();
                // Recorder values belong to the completed Unity frame, after the explicit render.
                foreach (var counter in counters) counter.Sample();
                if (frameTimingEnabled && FrameTimingManager.GetLatestTimings(1, timingBuffer) > 0)
                {
                    var timing = timingBuffer[0];
                    if (timing.frameStartTimestamp > lastGpuStamp)
                    {
                        lastGpuStamp = timing.frameStartTimestamp;
                        if (frames.Count >= 8 && timing.gpuFrameTime > 0 && timing.gpuFrameTime < 60000 &&
                            !double.IsNaN(timing.gpuFrameTime) && !double.IsInfinity(timing.gpuFrameTime))
                            gpuTimes.Add(timing.gpuFrameTime);
                        if (frames.Count >= 8 && timing.cpuMainThreadFrameTime > 0 && timing.cpuMainThreadFrameTime < 60000)
                            cpuMainTimes.Add(timing.cpuMainThreadFrameTime);
                        if (frames.Count >= 8 && timing.cpuRenderThreadFrameTime > 0 && timing.cpuRenderThreadFrameTime < 60000)
                            cpuRenderTimes.Add(timing.cpuRenderThreadFrameTime);
                    }
                }
                double totalFrameMilliseconds = Milliseconds(frameStarted);
                frames.Add(totalFrameMilliseconds);
                frameBreakdowns.Add(new OfflineFrameBreakdown { Tick = world.TickIndex, TotalMilliseconds = totalFrameMilliseconds,
                    WorldTickMilliseconds = frameTickMilliseconds, AiMilliseconds = frameAiMilliseconds,
                    PresentationMilliseconds = viewMilliseconds, RenderMilliseconds = renderMilliseconds });
            }
            watch.Stop();
            AllocationCapture.Stop();
            double actualSeconds = watch.Elapsed.TotalSeconds;
            long heapAfter = GC.GetTotalMemory(false);
            var gcAfter = Collections();
            var navigation = world.NavigationMetrics.Snapshot;
            world.NavigationMetrics.Enabled = false;
            RenderPipelineManager.endCameraRendering -= OnRendered;
            foreach (var counter in counters) counter.Stop();
            var final = Snapshot(match);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            long simulationTicks = world.TickIndex - startTick;
            double ratio = simulationTicks / (double)World.TickRate / actualSeconds;
            var frameSummary = frames.Summary();
            // A hitch is a sampled frame more than twice the sampled median and over one 30 fps frame (33 ms).
            double hitchThreshold = Math.Max(2 * frameSummary.P50, 33.0);
            int hitchCount = frames.CountAbove(hitchThreshold);
            frameBreakdowns.Sort((a, b) => b.TotalMilliseconds.CompareTo(a.TotalMilliseconds));
            var worstFrames = new OfflineFrameBreakdown[Math.Min(10, frameBreakdowns.Count)];
            for (int i = 0; i < worstFrames.Length; i++)
            {
                var entry = frameBreakdowns[i];
                entry.UnaccountedMilliseconds = entry.TotalMilliseconds - (entry.WorldTickMilliseconds + entry.AiMilliseconds + entry.PresentationMilliseconds + entry.RenderMilliseconds);
                entry.DominantMarker = DominantMarker(entry);
                worstFrames[i] = entry;
            }
            var report = new OfflineRenderReport
            {
                Utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), Fixture = FixtureVersion,
                Unity = Application.unityVersion, BuildGuid = Application.buildGUID, RulesSha256 = ResourceHash("Definitions/greybox"),
                MapSha256 = ResourceHash("Maps/amber_crossing"), CheckpointSha256 = checkpointHash,
                SampleStartSha256 = initialHash, SampleEndSha256 = StateHash(world),
                OperatingSystem = SystemInfo.operatingSystem, Cpu = SystemInfo.processorType, LogicalProcessors = SystemInfo.processorCount,
                MemoryMegabytes = SystemInfo.systemMemorySize, Gpu = SystemInfo.graphicsDeviceName,
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(), GraphicsMemoryMegabytes = SystemInfo.graphicsMemorySize,
                Width = Screen.width, Height = Screen.height, Quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                RenderPipeline = pipeline == null ? "Unknown" : pipeline.name, RenderScale = pipeline == null ? -1 : pipeline.renderScale,
                MsaaSamples = pipeline == null ? -1 : pipeline.msaaSampleCount, CameraPosition = camera.transform.position,
                CameraRotation = camera.transform.eulerAngles, OrthographicSize = camera.orthographicSize,
                DevelopmentBuild = Debug.isDebugBuild, BatchPlayer = Application.isBatchMode,
                VSyncCount = QualitySettings.vSyncCount, TargetFrameRate = Application.targetFrameRate,
                PrewarmTicks = prewarmTicks, WarmupTicks = warmupTicks, WarmupSeconds = warmupSeconds, ActualWarmupSeconds = actualWarmup,
                RequestedSampleSeconds = sampleSeconds, SampleSeconds = actualSeconds, SampleStartTick = startTick,
                SampleEndTick = world.TickIndex, SimulationTicks = simulationTicks, SimulationToWallRatio = ratio,
                FrameSamples = frames.Count, RenderedCameraFrames = sampledCameraFrames,
                AiThinks = first.Statistics.Thinks + second.Statistics.Thinks - startThinks,
                DeathsDuringSample = world.DeathCount - startDeaths, ProjectilePeak = projectilePeak,
                Checkpoint = checkpoint, Initial = initial, Final = final, FrameMilliseconds = frameSummary,
                HitchCount = hitchCount, HitchThresholdMilliseconds = hitchThreshold, WorstFrames = worstFrames,
                InitialPresentationSyncMilliseconds = initialPresentationSyncMilliseconds,
                AiPairPerTickMilliseconds = aiTimes.Summary(), WorldTickMilliseconds = tickTimes.Summary(),
                PresentationMilliseconds = viewTimes.Summary(), RenderAndReadbackMilliseconds = renderTimes.Summary(),
                GpuFeatureEnabled = frameTimingEnabled, GpuTimingAvailable = gpuTimes.Count > 0,
                GpuFrameMilliseconds = gpuTimes.Summary(), AllocationCapabilityVerified = allocationVerified,
                AllocationProbePeakBytes = allocationProbePeak, ManagedHeapBeforeBytes = heapBefore, ManagedHeapAfterBytes = heapAfter,
                GcCollections = new[] { gcAfter[0] - gcBefore[0], gcAfter[1] - gcBefore[1], gcAfter[2] - gcBefore[2] },
                Navigation = new OfflineNavigationReport(navigation), Counters = new OfflineCounterReport[counters.Length],
                AvailableProfilerCounters = availableProfilerCounters,
                SampleBufferOverflow = frames.Overflow || viewTimes.Overflow || renderTimes.Overflow || aiTimes.Overflow || tickTimes.Overflow || gpuTimes.Overflow ||
                    cpuMainTimes.Overflow || cpuRenderTimes.Overflow
            };
            for (int i = 0; i < counters.Length; i++) { report.Counters[i] = counters[i].Report(); counters[i].Dispose(); }
            report.CpuMainThreadFrameMilliseconds = cpuMainTimes.Summary(); report.CpuRenderThreadFrameMilliseconds = cpuRenderTimes.Summary();
            report.MobileTier = MobileQuality.Mode; report.DynamicResolutionFrozen = mobile != null;
            if (pipeline != null)
            {
                report.Hdr = pipeline.supportsHDR; report.ShadowResolution = pipeline.mainLightShadowmapResolution;
                report.ShadowDistance = pipeline.shadowDistance; report.ShadowCascades = pipeline.shadowCascadeCount;
                report.AdditionalLights = pipeline.maxAdditionalLightsCount;
                var sun = Array.Find(match.GetComponentsInChildren<Light>(), light => light.type == LightType.Directional);
                report.SoftShadows = pipeline.supportsSoftShadows && sun != null && sun.shadows == LightShadows.Soft;
            }
            report.LodBias = QualitySettings.lodBias; report.MaximumLodLevel = QualitySettings.maximumLODLevel;
            report.MeshLodThreshold = QualitySettings.meshLodThreshold; report.SkinWeights = QualitySettings.skinWeights.ToString();
            report.AnisotropicFiltering = QualitySettings.anisotropicFiltering.ToString();
            report.GpuTimingSource = report.GpuTimingAvailable ? "FrameTimingManager.gpuFrameTime (milliseconds)" : "Unavailable";
            // An exact named, unit-checked engine counter may exist when FrameTimingManager has no samples.
            // Preserve its provenance; neither source isolates this camera from the rest of the Unity frame.
            var gpuCounter = report.Counters[5];
            if (!report.GpuTimingAvailable && gpuCounter.Available)
            {
                var source = gpuCounter.Values;
                report.GpuTimingAvailable = true;
                report.GpuTimingSource = "ProfilerRecorder: GPU Frame Time (TimeNanoseconds converted to milliseconds)";
                report.GpuFrameMilliseconds = new OfflineDistribution
                {
                    Samples = source.Samples, Mean = source.Mean / 1000000, P50 = source.P50 / 1000000,
                    P95 = source.P95 / 1000000, P99 = source.P99 / 1000000, Maximum = source.Maximum / 1000000
                };
            }
            report.Passed = report.DevelopmentBuild && !report.BatchPlayer && world.Map.Id == "amber_crossing" &&
                report.GraphicsApi == "Direct3D11" && pipeline != null && report.VSyncCount == 0 && report.TargetFrameRate == -1 &&
                startTick == prewarmTicks + warmupTicks && simulationTicks == sampleTicks && !world.Match.IsFinished &&
                ratio >= .95 && ratio <= 1.05 && frames.Count >= 120 && sampledCameraFrames == frames.Count &&
                !report.SampleBufferOverflow && initial.ActiveMilitaryViews >= 8;
            // Read existing completed target only; full readback, PNG encoding and report serialization are outside all samples.
            report.CaptureHasPixels = PlayerSmoke.CaptureTarget(renderTarget, Path.Combine(folder, "profiler-art.png"));
            report.Passed &= report.CaptureHasPixels;
            // The same moment up close and without the HUD, also outside all timing, so each quality tier's picture
            // can be laid next to the desktop one: the camera zooms in on the thickest group of soldiers it sees.
            var canvases = match.GetComponentsInChildren<Canvas>();
            foreach (var canvas in canvases) canvas.enabled = false;
            match.Rig.SetHome(CloseFocus(match), 7);
            match.SyncPresentation(1); RenderFrame();
            report.CloseCaptureHasPixels = PlayerSmoke.CaptureTarget(renderTarget, Path.Combine(folder, "profiler-close.png"));
            foreach (var canvas in canvases) canvas.enabled = true;
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(completionPixel); renderTarget.Release(); UnityEngine.Object.Destroy(renderTarget);
            File.WriteAllText(Path.Combine(folder, "offline-render.json"), JsonUtility.ToJson(report, true));
            Debug.Log("EMBERFIELD_OFFLINE_RENDER " + report.Passed);
            IsRunning = false; Application.Quit(report.Passed ? 0 : 1);
        }

        private static double Milliseconds(long started) => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        private static int[] Collections() => new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        private static string DominantMarker(OfflineFrameBreakdown entry)
        {
            string name = "Unaccounted/engine"; double max = entry.UnaccountedMilliseconds;
            if (entry.WorldTickMilliseconds > max) { max = entry.WorldTickMilliseconds; name = "WorldTick"; }
            if (entry.AiMilliseconds > max) { max = entry.AiMilliseconds; name = "Ai"; }
            if (entry.PresentationMilliseconds > max) { max = entry.PresentationMilliseconds; name = "Presentation"; }
            if (entry.RenderMilliseconds > max) { name = "RenderAndReadback"; }
            return name;
        }
        private static string ResourceHash(string path) => Hash(Encoding.UTF8.GetBytes(Resources.Load<TextAsset>(path).text));
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string StateHash(World world)
        {
            var text = new StringBuilder(16384); text.Append(world.TickIndex).Append('|').Append(world.DeathCount);
            foreach (var p in world.Players)
            {
                text.Append("|P").Append(p.Id).Append(':').Append(p.FactionId).Append(':').Append(p.EraTier).Append(':').Append(p.PopulationUsed).Append(':').Append(p.PopulationReserved);
                for (int k = 0; k < 4; k++) text.Append(':').Append(p.Resources.Get((ResourceKind)k));
                foreach (string technology in p.CompletedTechnologyIds) text.Append(':').Append(technology);
            }
            foreach (var u in world.Units) text.Append("|U").Append(u.Id).Append(':').Append(u.OwnerId).Append(':').Append(u.DefinitionId).Append(':').Append(u.Position.X).Append(':').Append(u.Position.Z).Append(':').Append(u.Health).Append(':').Append(u.AttackTargetId).Append(':').Append(u.AttackCooldownTicks).Append(':').Append(u.CarriedAmount).Append(':').Append((int)u.CarriedKind).Append(':').Append((int)u.WorkerTask).Append(':').Append(u.Destination.X).Append(':').Append(u.Destination.Z);
            foreach (var b in world.Buildings) text.Append("|B").Append(b.Id).Append(':').Append(b.OwnerId).Append(':').Append(b.DefinitionId).Append(':').Append(b.Position.X).Append(':').Append(b.Position.Z).Append(':').Append(b.Health).Append(':').Append(b.BuildProgressTicks).Append(':').Append(b.ProductionQueue.Count).Append(':').Append(b.ActiveResearch?.TechnologyId).Append(':').Append(b.ActiveResearch?.RemainingTicks);
            foreach (var r in world.Resources) text.Append("|R").Append(r.Id).Append(':').Append(r.RemainingAmount);
            foreach (var o in world.Match.Objectives) text.Append("|O").Append(o.Id).Append(':').Append(o.OwnerId).Append(':').Append(o.CaptureProgressTicks).Append(':').Append(o.HoldTicks);
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        private static OfflineWorkloadSnapshot Snapshot(MatchController match)
        {
            var result = new OfflineWorkloadSnapshot { Tick = match.World.TickIndex, Units = match.World.Units.Count, Buildings = match.World.Buildings.Count, Projectiles = match.World.Projectiles.Count, Deaths = match.World.DeathCount, AllocatedEntityViews = match.View.Count };
            foreach (var unit in match.World.Units)
            {
                if (!unit.IsWorker && unit.AttackDamage > 0) result.MilitaryUnits++;
                var root = match.View.RootFor(unit.Id);
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                result.ActiveUnitViews++;
                if (!unit.IsWorker && unit.AttackDamage > 0) result.ActiveMilitaryViews++;
                var viewport = match.Rig.Camera.WorldToViewportPoint(root.position);
                if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) result.ActiveUnitCentersInViewport++;
            }
            return result;
        }

        private static Vector3 CloseFocus(MatchController match)
        {
            var soldiers = new List<Vector3>();
            foreach (var unit in match.World.Units)
            {
                if (unit.IsWorker || unit.AttackDamage <= 0) continue;
                var root = match.View.RootFor(unit.Id);
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                var viewport = match.Rig.Camera.WorldToViewportPoint(root.position);
                if (viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                soldiers.Add(root.position);
            }
            var focus = new Vector3(48, 0, 36); int best = -1;
            foreach (var soldier in soldiers)
            {
                int near = 0;
                foreach (var other in soldiers) if ((other - soldier).sqrMagnitude < 36) near++;
                if (near > best) { best = near; focus = new Vector3(soldier.x, 0, soldier.z); }
            }
            return focus;
        }

        private static Counter[] CreateCounters(out string[] inventory)
        {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                names.Add(description.Name + " [" + description.UnitType + "]");
            }
            inventory = new string[names.Count]; names.CopyTo(inventory);
            return new[] { new Counter(handles, "Draw Calls Count", "Count", true), new Counter(handles, "SetPass Calls Count", "Count", true),
                new Counter(handles, "Triangles Count", "Count", true), new Counter(handles, "GC Allocated In Frame", "Bytes", false),
                new Counter(handles, "Main Thread", "TimeNanoseconds", true), new Counter(handles, "GPU Frame Time", "TimeNanoseconds", true),
                // Render statistics that carry over to a phone better than a desktop's milliseconds do.
                new Counter(handles, "Batches Count", "Count", true), new Counter(handles, "Vertices Count", "Count", true),
                new Counter(handles, "Shadow Casters Count", "Count", true), new Counter(handles, "Visible Skinned Meshes Count", "Count", true),
                new Counter(handles, "Texture Memory", "Bytes", true), new Counter(handles, "Mesh Memory", "Bytes", true),
                new Counter(handles, "Render Textures Bytes", "Bytes", true),
                new Counter(handles, "CPU Main Thread Frame Time", "TimeNanoseconds", true), new Counter(handles, "CPU Render Thread Frame Time", "TimeNanoseconds", true) };
        }
        private sealed class Counter : IDisposable
        {
            private ProfilerRecorder recorder;
            private readonly string name;
            private readonly bool requirePositive;
            private readonly Samples values = new Samples(65536);
            public string UnavailableReason;
            private string unit = "Unavailable";
            public Counter(List<ProfilerRecorderHandle> handles, string name, string expectedUnit, bool requirePositive)
            {
                this.name = name; this.requirePositive = requirePositive;
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name != name) continue;
                    unit = description.UnitType.ToString();
                    if (unit != expectedUnit) { UnavailableReason = "Unexpected unit " + unit; return; }
                    recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame);
                    if (!recorder.Valid) UnavailableReason = "Recorder is invalid.";
                    return;
                }
                UnavailableReason = "Counter is not available on this player/platform.";
            }
            public long Read() => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1;
            public void Sample() { if (UnavailableReason != null) return; long value = Read(); if (value >= 0) values.Add(value); }
            public void ClearSamples() => values.Clear();
            public void Stop() { if (recorder.Valid) recorder.Stop(); }
            public OfflineCounterReport Report()
            {
                var summary = values.Summary();
                bool available = UnavailableReason == null && values.Count > 0 && (!requirePositive || summary.Maximum > 0) && !values.Overflow;
                return new OfflineCounterReport { Name = name, Unit = unit, Available = available, Reason = available ? "Available completed-frame recorder samples." : UnavailableReason ?? "No valid positive samples or sample capacity exceeded.", Values = available ? summary : new OfflineDistribution(), Total = available ? values.Sum : -1 };
            }
            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }
        private sealed class Samples
        {
            private readonly double[] values;
            public int Count { get; private set; }
            public bool Overflow { get; private set; }
            public double Sum { get; private set; }
            public Samples(int capacity) { values = new double[capacity]; }
            public void Clear() { Count = 0; Sum = 0; Overflow = false; }
            public void Add(double value) { if (Count == values.Length) { Overflow = true; return; } values[Count++] = value; Sum += value; }
            public OfflineDistribution Summary()
            {
                if (Count == 0) return new OfflineDistribution();
                Array.Sort(values, 0, Count);
                double mean = Sum / Count, variance = 0;
                for (int i = 0; i < Count; i++) { double d = values[i] - mean; variance += d * d; }
                return new OfflineDistribution { Samples = Count, Mean = mean, P50 = At(.5), P95 = At(.95), P99 = At(.99), P999 = At(.999),
                    Maximum = values[Count - 1], StdDev = Math.Sqrt(variance / Count) };
            }
            /// <summary>Count of samples above a threshold, e.g. a hitch cutoff. Independent of sort order.</summary>
            public int CountAbove(double threshold) { int count = 0; for (int i = 0; i < Count; i++) if (values[i] > threshold) count++; return count; }
            private double At(double fraction) => values[Math.Max(0, Math.Min(Count - 1, (int)Math.Ceiling(Count * fraction) - 1))];
        }
    }

    [Serializable] public sealed class OfflineDistribution { public int Samples; public double Mean = -1, P50 = -1, P95 = -1, P99 = -1, P999 = -1, Maximum = -1, StdDev = -1; }
    /// <summary>One sampled frame's cost split across the presentation loop's own measured components; the
    /// remainder is engine/GC/other overhead the probe does not time explicitly.</summary>
    [Serializable] public sealed class OfflineFrameBreakdown
    {
        public long Tick;
        public double TotalMilliseconds, WorldTickMilliseconds, AiMilliseconds, PresentationMilliseconds, RenderMilliseconds, UnaccountedMilliseconds;
        public string DominantMarker;
    }
    [Serializable] public sealed class OfflineCounterReport { public string Name, Unit, Reason; public bool Available; public double Total = -1; public OfflineDistribution Values; }
    [Serializable] public sealed class OfflineWorkloadSnapshot { public long Tick; public int Units, Buildings, Projectiles, Deaths, MilitaryUnits, AllocatedEntityViews, ActiveUnitViews, ActiveMilitaryViews, ActiveUnitCentersInViewport; }
    [Serializable] public sealed class OfflineNavigationReport
    {
        public long Paths, Attacks, ConnectivityRebuilds, VisitedCells, SharedFieldBuilds, SharedFieldCacheHits, RecoveryQueries;
        public double PathMilliseconds, AttackMilliseconds, ConnectivityMilliseconds, MaximumQueryMilliseconds;
        public OfflineNavigationReport(NavigationMetricsSnapshot s)
        {
            Paths = s.PathQueryCount; Attacks = s.AttackQueryCount; ConnectivityRebuilds = s.ConnectivityRebuildCount; VisitedCells = s.VisitedCells;
            SharedFieldBuilds = s.SharedFieldBuildCount; SharedFieldCacheHits = s.SharedFieldCacheHitCount; RecoveryQueries = s.RecoveryQueryCount;
            double scale = 1000.0 / NavigationMetrics.StopwatchFrequency;
            PathMilliseconds = s.PathElapsedStopwatchTicks * scale; AttackMilliseconds = s.AttackElapsedStopwatchTicks * scale;
            ConnectivityMilliseconds = s.ConnectivityElapsedStopwatchTicks * scale; MaximumQueryMilliseconds = s.MaxQueryStopwatchTicks * scale;
        }
    }
    [Serializable] public sealed class OfflineRenderReport
    {
        public string Method = "Fixed shipped Aven/Dominion AI-vs-AI checkpoint after 6000 ordinary ticks, 60 ticks/3 s rendered warmup, then exactly400 ticks at20Hz over20 s. Hidden non-batch D3D11 development player; explicit URP camera and Canvas render to persistent screen-sized RT plus synchronous one-pixel ReadPixels every Unity loop. Frame times INCLUDE render request, Canvas work, GPU synchronization, engine loop, profiler collection and presentation; conservative serialized offscreen throughput, NOT ordinary display FPS. AI pair and World.Tick timed separately per tick; SyncPresentation includes view/fog/HUD/camera. Navigation is route-search/connectivity work only. Platform GPU telemetry is delayed frame-level data, deduplicated by timestamp with first8 sampled frames ignored, or the exact named GPU Frame Time recorder when available with nanosecond units. GpuTimingSource identifies the source; no camera-isolated GPU claim. Recorder counters use completed frames; GC allocation requires observed8192-byte capability probe. Unavailable values=-1. Startup, state hashing, full PNG capture and report serialization excluded. A hitch is a sampled frame over both twice FrameMilliseconds.P50 and 33 ms; WorstFrames names the 10 costliest sampled frames and, for each, which of WorldTick/Ai/Presentation/RenderAndReadback/Unaccounted-engine-overhead was largest. InitialPresentationSyncMilliseconds times the one untimed SyncPresentation right after the prewarm, a proxy for a cold first-use hitch across every unit/building type reached by then, not any single in-match first production. No physical mobile or display-present latency evidence. MobileTier other than desktop means -emberfieldMobileTier made this desktop player render with a phone tier's settings (dynamic resolution frozen at the tier's starting scale); the desktop CPU and GPU still do the work, so compare its render statistics, not its milliseconds, with a phone.";
        public string Utc, Fixture, Unity, BuildGuid, RulesSha256, MapSha256, CheckpointSha256, SampleStartSha256, SampleEndSha256;
        public string OperatingSystem, Cpu, Gpu, GraphicsApi, Quality, RenderPipeline, GpuTimingSource;
        public string MobileTier, SkinWeights, AnisotropicFiltering;
        public string[] AvailableProfilerCounters;
        public bool Passed, CaptureHasPixels, CloseCaptureHasPixels, DevelopmentBuild, BatchPlayer, GpuFeatureEnabled, GpuTimingAvailable, AllocationCapabilityVerified, SampleBufferOverflow;
        public bool Hdr, SoftShadows, DynamicResolutionFrozen;
        public int LogicalProcessors, MemoryMegabytes, GraphicsMemoryMegabytes, Width, Height, MsaaSamples, VSyncCount, TargetFrameRate;
        public int ShadowResolution, ShadowCascades, AdditionalLights, MaximumLodLevel;
        public int PrewarmTicks, WarmupTicks, FrameSamples, RenderedCameraFrames, AiThinks, DeathsDuringSample, ProjectilePeak;
        public long SampleStartTick, SampleEndTick, SimulationTicks, ManagedHeapBeforeBytes, ManagedHeapAfterBytes, AllocationProbePeakBytes;
        public float RenderScale, OrthographicSize, ShadowDistance, LodBias, MeshLodThreshold;
        public Vector3 CameraPosition, CameraRotation;
        public double WarmupSeconds, ActualWarmupSeconds, RequestedSampleSeconds, SampleSeconds, SimulationToWallRatio;
        public int[] GcCollections;
        public OfflineWorkloadSnapshot Checkpoint, Initial, Final;
        public OfflineDistribution FrameMilliseconds, AiPairPerTickMilliseconds, WorldTickMilliseconds, PresentationMilliseconds, RenderAndReadbackMilliseconds, GpuFrameMilliseconds;
        public OfflineDistribution CpuMainThreadFrameMilliseconds, CpuRenderThreadFrameMilliseconds;
        /// <summary>A hitch is a sampled frame over both twice FrameMilliseconds.P50 and 33 ms (one 30 fps frame).</summary>
        public int HitchCount;
        public double HitchThresholdMilliseconds;
        /// <summary>The 10 costliest sampled frames, each split across the presentation loop's own timed components.</summary>
        public OfflineFrameBreakdown[] WorstFrames;
        /// <summary>The one, untimed SyncPresentation call right after the 6000-tick prewarm: every unit/building
        /// type reached by then pays its first Resources.Load/Instantiate/shader-variant compile here at once. A
        /// proxy for a cold first-use hitch, not a claim about any single in-match first production.</summary>
        public double InitialPresentationSyncMilliseconds;
        public OfflineCounterReport[] Counters;
        public OfflineNavigationReport Navigation;
    }
}
