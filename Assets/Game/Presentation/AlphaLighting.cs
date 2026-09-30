using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    public sealed class AlphaLighting : MonoBehaviour
    {
        private VolumeProfile profile;
        /// <param name="lands">The match's lands (World.Lands). A map with lands is lit for the high, open air of its
        /// neutral highland, and the land in view lays its own grade over that (LandAtmosphere).</param>
        public static void Configure(Camera camera, Light sun, Transform parent, MapDefinition map = null, MapLands lands = null)
        {
            bool coast = map?.BiomeId == "caribbean", desert = map?.BiomeId == "desert", fantasy = map?.RealmId == ContentRealms.Fantasy;
            bool highland = lands != null && lands.HasLands;
            sun.color = desert ? new Color(1, .84f, .60f) : coast ? new Color(1, .94f, .81f) : highland ? new Color(1, .91f, .76f) : new Color(1, .88f, .69f);
            sun.intensity = desert ? 1.63f : coast ? 1.48f : highland ? 1.6f : 1.55f;
            sun.transform.rotation = Quaternion.Euler(desert ? 51 : coast ? 48 : highland ? 47 : 43, desert ? -52 : coast ? -24 : highland ? -32 : -36, 0);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .75f;
            sun.shadowBias = .055f; sun.shadowNormalBias = .24f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = desert ? new Color(.55f, .60f, .64f) : coast ? new Color(.38f, .64f, .74f) : highland ? new Color(.47f, .63f, .72f) : new Color(.45f, .61f, .68f);
            RenderSettings.ambientEquatorColor = desert ? new Color(.48f, .41f, .30f) : coast ? new Color(.28f, .45f, .43f) : highland ? new Color(.32f, .41f, .42f) : new Color(.29f, .39f, .38f);
            RenderSettings.ambientGroundColor = desert ? new Color(.28f, .23f, .15f) : new Color(.17f, .2f, .16f);
            RenderSettings.ambientIntensity = 1;
            // One horizon colour per biome drives both the far haze and what lies past the map edge, so the
            // battlefield dissolves into its own sky instead of ending at a flat wall of colour.
            // The highland's air is thin and clear: a paler, bluer horizon that the haze reaches later.
            var horizon = desert ? new Color(.72f, .60f, .44f) : coast ? new Color(.46f, .66f, .72f) : highland ? new Color(.55f, .65f, .70f) : new Color(.49f, .58f, .58f);
            RenderSettings.fog = true;
            // Linear, not exponential: this camera is orthographic and sits 55 m out, so only a narrow band
            // of depth is ever on screen. Starting the haze just in front of that band leaves the near half
            // of the picture clean and still separates the far side of the battlefield.
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogStartDistance = desert ? 38 : highland ? 46 : 42;
            RenderSettings.fogEndDistance = desert ? 104 : coast ? 112 : highland ? 128 : 118;
            camera.backgroundColor = Color.Lerp(horizon, desert ? new Color(.52f, .43f, .31f) : coast ? new Color(.13f, .40f, .48f) : new Color(.16f, .27f, .29f), .45f);
            camera.allowHDR = true; camera.allowMSAA = true;
            ConfigureDesktopShadows();
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var holder = new GameObject("Frontier atmosphere"); holder.transform.SetParent(parent, false);
            var owner = holder.AddComponent<AlphaLighting>();
            owner.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var volume = holder.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = owner.profile;
            var color = owner.profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(desert ? .06f : .20f); color.contrast.Override(fantasy ? 12 : 9); color.saturation.Override(coast ? 10 : 8);
            var tone = owner.profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = owner.profile.Add<Bloom>(true); bloom.intensity.Override(fantasy ? .19f : .13f); bloom.threshold.Override(1.2f); bloom.scatter.Override(.55f);
            var vignette = owner.profile.Add<Vignette>(true); vignette.intensity.Override(.14f); vignette.smoothness.Override(.55f);
            // Warm light and cool shade is what reads as daylight; each biome leans its own way.
            var split = owner.profile.Add<ShadowsMidtonesHighlights>(true);
            split.shadows.Override(desert ? new Vector4(.92f, .96f, 1.12f, 0) : coast ? new Vector4(.88f, .98f, 1.16f, 0) : new Vector4(.90f, .97f, 1.12f, 0));
            split.midtones.Override(new Vector4(1f, 1f, 1f, 0));
            split.highlights.Override(desert ? new Vector4(1.10f, 1.02f, .90f, 0) : coast ? new Vector4(1.05f, 1.03f, .96f, 0) : new Vector4(1.06f, 1.02f, .94f, 0));
            color.colorFilter.Override(desert ? new Color(1f, .97f, .90f) : coast ? new Color(.96f, 1f, 1.01f) : new Color(.98f, 1f, .97f));
            LandAtmosphere.Attach(holder, camera, lands);
        }

        private static UniversalRenderPipelineAsset desktopPipeline;

        /// <summary>
        /// The shipped pipeline asset is the phone's: a 2048 shadow map in two cascades over 80 m, and 2x MSAA.
        /// A desktop can afford more, so it runs on a copy made here with shadows reaching 110 m in three
        /// cascades and 4x MSAA. The shipped asset is never edited; phones get their own copies from MobileQuality.
        /// </summary>
        private static void ConfigureDesktopShadows()
        {
            if (Application.isMobilePlatform || MobileQuality.Requested || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (desktopPipeline == null)
            {
                if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset shipped)) return;
                if (shipped.mainLightShadowmapResolution >= 2048 && shipped.shadowDistance >= 110) return;
                desktopPipeline = Instantiate(shipped);
                desktopPipeline.name = "Desktop shadows (runtime copy)";
                desktopPipeline.shadowDistance = 110;
                desktopPipeline.mainLightShadowmapResolution = 2048;
                desktopPipeline.shadowCascadeCount = 3;
                desktopPipeline.msaaSampleCount = 4;
            }
            if (!ReferenceEquals(QualitySettings.renderPipeline, desktopPipeline)) QualitySettings.renderPipeline = desktopPipeline;
        }
        private void OnDestroy()
        {
            if (profile == null) return;
            foreach (var component in profile.components) if (component != null) Destroy(component);
            Destroy(profile);
        }
    }
}
