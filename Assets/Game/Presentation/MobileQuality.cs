using System;
using Emberfield.Quality;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Phone quality. On a phone every match picks a tier (low, mid or high) from what the device reports and renders
    /// through a runtime copy of the shipped pipeline asset set for that tier, with dynamic resolution on top. The
    /// shipped asset and QualitySettings.asset are never edited, and a desktop is left exactly as it was.
    /// Development players and the editor take -emberfieldMobileTier low|mid|high|auto to behave as a phone of that
    /// tier on a desktop, and -emberfieldMobileTier baseline to behave as a phone did before the tiers: the Mobile
    /// quality level with the shipped pipeline asset untouched. Either way the desktop shadow upgrade stays off.
    /// </summary>
    public sealed class MobileQuality : MonoBehaviour
    {
        public const string Flag = "-emberfieldMobileTier";
        public const string BaselineMode = "baseline", DesktopMode = "desktop";

        // The Mobile quality level in ProjectSettings/QualitySettings.asset. A phone starts on that level; a desktop
        // player does not even contain it (it excludes Standalone), so a forced tier sets its values itself.
        public const float LevelLodBias = 1, LevelMeshLodThreshold = 1;
        public const int LevelMaximumLod = 0;
        public const SkinWeights LevelSkinWeights = SkinWeights.TwoBones;
        public const AnisotropicFiltering LevelAnisotropic = AnisotropicFiltering.Enable;

        /// <summary>Stands in for the command line in tests; null leaves the command line in charge.</summary>
        public static string ForcedMode;
        /// <summary>The tier the current match runs, or null on a desktop and in baseline mode.</summary>
        public static MobileTierSettings Active { get; private set; }
        /// <summary>What the current match runs as: desktop, baseline, low, mid or high.</summary>
        public static string Mode { get; private set; } = DesktopMode;
        /// <summary>True when matches behave as a phone: on a phone, or forced from the command line.</summary>
        public static bool Requested => Application.isMobilePlatform || RequestedMode() != null;
        /// <summary>The share of the screen below which scenery draws its lighter level: the tier's, else the desktop's.</summary>
        public static float SceneryDetailHeight => Active?.SceneryDetailHeight ?? MobileTiers.DesktopSceneryDetailHeight;
        /// <summary>Whether the trees, palms and rocks on blocked cells cast shadows: everywhere but the low tier.</summary>
        public static bool SceneryShadows => Active?.SceneryShadows ?? true;
        /// <summary>The share of the lands' embers, glimmers and smoke drawn: the tier's, else all of them.</summary>
        public static float AmbientParticleShare => Active?.AmbientParticleShare ?? 1;

        private static readonly UniversalRenderPipelineAsset[] copies = new UniversalRenderPipelineAsset[3];
        private static bool listening;
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private DynamicResolution governor;
        private UniversalRenderPipelineAsset pipeline;
        private float started;

        public DynamicResolution Governor => governor;

        /// <summary>Runs at match start, after AlphaLighting has lit the scene. Leaves a desktop untouched.</summary>
        public static void Apply(Light sun, GameObject owner, bool uncappedFrameRate)
        {
            string mode = RequestedMode();
            Active = null; Mode = DesktopMode;
            if (mode == null && !Application.isMobilePlatform) return;
            QualitySettings.lodBias = LevelLodBias; QualitySettings.maximumLODLevel = LevelMaximumLod;
            QualitySettings.meshLodThreshold = LevelMeshLodThreshold; QualitySettings.skinWeights = LevelSkinWeights;
            QualitySettings.anisotropicFiltering = LevelAnisotropic;
            if (!listening) { Application.lowMemory += ReleaseMemory; listening = true; }
            var shipped = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (mode == BaselineMode)
            {
                Mode = BaselineMode;
                if (shipped != null && !ReferenceEquals(QualitySettings.renderPipeline, shipped)) QualitySettings.renderPipeline = shipped;
                return;
            }
            if (mode == null || !MobileTiers.TryParse(mode, out var tier))
            {
                if (mode != null && mode != "auto") Debug.LogWarning("EMBERFIELD_MOBILE_TIER unknown " + mode + "; choosing from the device.");
                tier = MobileTiers.Choose(new DeviceProfile(SystemInfo.systemMemorySize, SystemInfo.processorCount,
                    SystemInfo.graphicsMemorySize, SystemInfo.maxTextureSize, SystemInfo.graphicsDeviceType.ToString()));
            }
            var settings = MobileTierSettings.For(tier);
            Active = settings; Mode = tier.ToString().ToLowerInvariant();
            QualitySettings.lodBias = settings.LodBias; QualitySettings.maximumLODLevel = settings.MaximumLodLevel;
            QualitySettings.meshLodThreshold = settings.MeshLodThreshold;
            QualitySettings.skinWeights = settings.SkinWeights >= 4 ? SkinWeights.FourBones : settings.SkinWeights == 2 ? SkinWeights.TwoBones : SkinWeights.OneBone;
            // Phones always wait for the display; the frame rate cap is what paces them.
            QualitySettings.vSyncCount = 0;
            if (!uncappedFrameRate) Application.targetFrameRate = settings.TargetFrameRate;
            // The pipeline's soft shadow switch is internal, so hard shadows are asked of the sun itself.
            if (!settings.SoftShadows && sun != null && sun.shadows == LightShadows.Soft) sun.shadows = LightShadows.Hard;
            if (shipped == null) return;
            var copy = copies[(int)tier];
            if (copy == null)
            {
                copy = Instantiate(shipped);
                copy.name = "Mobile " + Mode + " (runtime copy)";
                copy.msaaSampleCount = settings.MsaaSamples; copy.supportsHDR = settings.Hdr;
                copy.mainLightShadowmapResolution = settings.ShadowResolution; copy.shadowDistance = settings.ShadowDistance;
                copy.shadowCascadeCount = settings.ShadowCascades; copy.maxAdditionalLightsCount = settings.AdditionalLights;
                copies[(int)tier] = copy;
            }
            float scale = MobileTiers.StartingRenderScale(settings, Screen.width, Screen.height);
            copy.renderScale = scale;
            if (!ReferenceEquals(QualitySettings.renderPipeline, copy)) QualitySettings.renderPipeline = copy;
            var quality = owner.AddComponent<MobileQuality>();
            quality.pipeline = copy; quality.started = Time.unscaledTime;
            quality.governor = new DynamicResolution(scale, settings.MinimumRenderScale, settings.TargetFrameRate);
            Debug.Log(FormattableString.Invariant($"EMBERFIELD_MOBILE_TIER {Mode} scale={scale:0.00} memory={SystemInfo.systemMemorySize} cores={SystemInfo.processorCount} gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType}"));
        }

        private static string RequestedMode()
        {
            if (ForcedMode != null) return ForcedMode;
            if (!Debug.isDebugBuild && !Application.isEditor) return null;
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, Flag);
            return option >= 0 && option + 1 < args.Length ? args[option + 1].Trim().ToLowerInvariant() : null;
        }

        // The system is about to close apps for memory: give back what no longer has a user.
        private static void ReleaseMemory()
        {
            Debug.Log("EMBERFIELD_LOW_MEMORY unloading unused assets");
            Resources.UnloadUnusedAssets();
        }

        private void Update()
        {
            // Loading and the first frames of a match are not evidence about the device.
            if (governor == null || Time.unscaledTime - started < 3) return;
            FrameTimingManager.CaptureFrameTimings();
            double work = -1;
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                var frame = timing[0];
                work = Math.Max(Math.Max(frame.cpuMainThreadFrameTime - frame.cpuMainThreadPresentWaitTime, frame.cpuRenderThreadFrameTime), frame.gpuFrameTime);
            }
            double wall = Time.unscaledDeltaTime * 1000.0;
            if (!governor.Observe(work, wall, Time.unscaledDeltaTime)) return;
            pipeline.renderScale = governor.Scale;
            Debug.Log(FormattableString.Invariant($"EMBERFIELD_RENDER_SCALE {governor.Scale:0.00} work={governor.SmoothedWorkMilliseconds:0.0}ms frame={governor.SmoothedWallMilliseconds:0.0}ms"));
        }
    }
}
