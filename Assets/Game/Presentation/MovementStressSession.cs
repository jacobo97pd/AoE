using System;
using System.Collections.Generic;
using System.Diagnostics;
using Emberfield.Diagnostics;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Developer-scene orchestration. Measurements never participate in simulation decisions.
    public sealed class MovementStressSession
    {
        public static readonly int[] Counts = { 50, 100, 200, 300, 500 };
        public MovementScenario Scenario { get; }
        public MovementObserver Observer { get; private set; }
        public bool Running { get; private set; }
        public bool FollowCamera { get; set; }
        public double CommandMilliseconds { get; private set; }
        public double LastNavigationMilliseconds { get; private set; }
        public double SmoothedFrameMilliseconds { get; private set; }
        public int FrameSamples => frames.Count;
        public bool SamplingCurrentFrame { get; private set; }
        public string Status { get; private set; } = "Ready — choose a count/scenario, then Run orders.";
        private readonly List<double> frames = new List<double>(4096);
        private readonly List<double> ticks = new List<double>(4096);
        private readonly List<double> paths = new List<double>(4096);
        // What SyncPresentation (and the view's share of it) cost on each sampled frame: the army's per-frame CPU.
        private readonly List<double> presentations = new List<double>(4096);
        private readonly List<double> views = new List<double>(4096);
        private long navigationBefore;
        private double elapsedSeconds;
        private double sampledSeconds;
        private float nextCameraRefresh;
        private readonly Stopwatch watch = new Stopwatch();

        public MovementStressSession(MovementScenarioKind kind, int count)
        {
            Scenario = MovementScenarioFactory.Create(kind, count);
            Scenario.World.NavigationMetrics.Enabled = true;
            Observer = new MovementObserver(Scenario);
            Observer.Sample();
        }

        public void Start()
        {
            if (Scenario.OrdersIssued) return;
            frames.Clear(); ticks.Clear(); paths.Clear(); presentations.Clear(); views.Clear(); elapsedSeconds = sampledSeconds = 0;
            Scenario.World.NavigationMetrics.Reset();
            watch.Restart(); var results = Scenario.IssueOrders(); watch.Stop();
            CommandMilliseconds = watch.Elapsed.TotalMilliseconds;
            bool accepted = true;
            foreach (var result in results) accepted &= result.Accepted;
            Running = accepted;
            Status = accepted ? "Running — measurements use normal 20 Hz simulation." : "Order rejected: " + (results.Length > 0 ? results[0].Message : "No order.");
            Observer = new MovementObserver(Scenario);
            Observer.Sample();
        }

        public void BeforeTick()
        {
            navigationBefore = Scenario.World.NavigationMetrics.Snapshot.TotalElapsedStopwatchTicks;
            if (Running) Scenario.AdvanceScheduledEvents();
        }

        public void AfterTick(double milliseconds)
        {
            LastNavigationMilliseconds = (Scenario.World.NavigationMetrics.Snapshot.TotalElapsedStopwatchTicks - navigationBefore) * 1000.0 / NavigationMetrics.StopwatchFrequency;
            if (!Running) return;
            if (ticks.Count < 36000) { ticks.Add(milliseconds); paths.Add(LastNavigationMilliseconds); }
            if (Scenario.ElapsedTicks % 2 == 0)
            {
                Observer.Sample();
                if (Observer.Current.Arrived == Scenario.UnitIds.Count)
                { Running = false; Status = "All movement goals reached. Reset to repeat."; }
                else if (Scenario.ElapsedTicks >= 2400)
                { Running = false; Status = "120 second observation complete — inspect remaining units."; }
            }
        }

        public void OnFrame(float seconds)
        {
            SamplingCurrentFrame = false;
            double milliseconds = seconds * 1000.0;
            SmoothedFrameMilliseconds = SmoothedFrameMilliseconds == 0 ? milliseconds : SmoothedFrameMilliseconds * .9 + milliseconds * .1;
            if (!Running) return;
            elapsedSeconds += seconds;
            // Exclude scene warm-up and initial command submission from steady frame samples.
            if (elapsedSeconds >= 3 && frames.Count < 36000)
            { frames.Add(milliseconds); sampledSeconds += seconds; SamplingCurrentFrame = true; }
        }

        /// <summary>Called after the frame's SyncPresentation; kept for the same frames OnFrame sampled.</summary>
        public void ObservePresentation(double presentationMilliseconds, double viewMilliseconds)
        {
            if (!SamplingCurrentFrame) return;
            presentations.Add(presentationMilliseconds); views.Add(viewMilliseconds);
        }

        public void FrameArmy(RtsCamera rig, bool force = false)
        {
            if (!force && (!FollowCamera || Time.unscaledTime < nextCameraRefresh)) return;
            nextCameraRefresh = Time.unscaledTime + .25f;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (int id in Scenario.UnitIds)
                if (Scenario.World.TryGetUnit(id, out var unit))
                {
                    var point = DefinitionLoader.ToWorld(unit.Position);
                    minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                    minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
                }
            if (minX == float.MaxValue) return;
            bool wide = rig.Camera.aspect > 1.7f;
            float referenceHeight = wide ? 720 : 1080;
            float playableFraction = (referenceHeight - 328) / referenceHeight;
            float size = Mathf.Max(8, Mathf.Max((maxX - minX + 5) / (2 * rig.Camera.aspect), ((maxZ - minZ) * .9063f + 5) / (2 * playableFraction)));
            float verticalOffset = 148 / referenceHeight * size / .9063f;
            rig.SetHome(new Vector3((minX + maxX) * .5f, 0, (minZ + maxZ) * .5f - verticalOffset), size);
        }

        public MovementFrameSummary Summarize()
        {
            Observer.Sample();
            // A hitch is a sampled frame more than twice the sampled median and over one 30 fps frame (33 ms).
            double frameHitchThreshold = Math.Max(2 * Percentile(frames, .5), 33.0);
            return new MovementFrameSummary
            {
                Scenario = Scenario.Kind.ToString(), Units = Scenario.RequestedUnits,
                FrameSamples = frames.Count, TickSamples = ticks.Count, CommandMilliseconds = CommandMilliseconds,
                SampledFrameSeconds = sampledSeconds,
                FrameP50Milliseconds = Percentile(frames, .5), FrameP95Milliseconds = Percentile(frames, .95), FrameP99Milliseconds = Percentile(frames, .99),
                FrameP999Milliseconds = Percentile(frames, .999), FrameMaxMilliseconds = Percentile(frames, 1), FrameStdDevMilliseconds = StdDev(frames),
                FrameHitchThresholdMilliseconds = frameHitchThreshold, FrameHitchCount = CountAbove(frames, frameHitchThreshold),
                TickP50Milliseconds = Percentile(ticks, .5), TickP95Milliseconds = Percentile(ticks, .95), TickP99Milliseconds = Percentile(ticks, .99), TickMaxMilliseconds = Percentile(ticks, 1),
                NavigationP95Milliseconds = Percentile(paths, .95), NavigationMaxMilliseconds = Percentile(paths, 1),
                PresentationP50Milliseconds = Percentile(presentations, .5), PresentationP95Milliseconds = Percentile(presentations, .95),
                PresentationP99Milliseconds = Percentile(presentations, .99), ViewP50Milliseconds = Percentile(views, .5),
                ViewP95Milliseconds = Percentile(views, .95), ViewP99Milliseconds = Percentile(views, .99),
                Arrived = Observer.Current.Arrived, Stalled = Observer.Current.Stalled, OverlapPairs = Observer.Current.OverlapPairs,
                MaxOverlapPairs = Observer.Current.MaxOverlapPairs, InvalidPositions = Observer.Current.MaxInvalidPositions
            };
        }

        private static double Percentile(List<double> values, double fraction)
        {
            if (values.Count == 0) return 0;
            var sorted = values.ToArray(); Array.Sort(sorted);
            return sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(fraction * sorted.Length) - 1)];
        }

        private static double StdDev(List<double> values)
        {
            if (values.Count == 0) return 0;
            double mean = 0; for (int i = 0; i < values.Count; i++) mean += values[i]; mean /= values.Count;
            double variance = 0; for (int i = 0; i < values.Count; i++) { double d = values[i] - mean; variance += d * d; }
            return Math.Sqrt(variance / values.Count);
        }

        private static int CountAbove(List<double> values, double threshold)
        {
            int count = 0; for (int i = 0; i < values.Count; i++) if (values[i] > threshold) count++; return count;
        }
    }

    [Serializable]
    public sealed class MovementFrameSummary
    {
        public string Scenario;
        public int Units, FrameSamples, TickSamples, Arrived, Stalled, OverlapPairs, MaxOverlapPairs, InvalidPositions;
        public double CommandMilliseconds, FrameP50Milliseconds, FrameP95Milliseconds, FrameP99Milliseconds;
        public double FrameP999Milliseconds, FrameMaxMilliseconds, FrameStdDevMilliseconds, FrameHitchThresholdMilliseconds;
        public int FrameHitchCount;
        public double TickP50Milliseconds, TickP95Milliseconds, TickP99Milliseconds, TickMaxMilliseconds;
        public double NavigationP95Milliseconds, NavigationMaxMilliseconds;
        public double PresentationP50Milliseconds, PresentationP95Milliseconds, PresentationP99Milliseconds;
        public double ViewP50Milliseconds, ViewP95Milliseconds, ViewP99Milliseconds;
        public double SampledFrameSeconds;
    }
}
