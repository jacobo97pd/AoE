using System.Collections;
using Emberfield.Presentation;
using Emberfield.Quality;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class MobileQualityTests
    {
        private GameObject root;
        private RenderPipelineAsset pipeline;
        private float lodBias, meshLodThreshold;
        private int maximumLod, frameRate, vSync;
        private SkinWeights skinWeights;
        private AnisotropicFiltering anisotropic;

        [SetUp]
        public void Remember()
        {
            pipeline = QualitySettings.renderPipeline; lodBias = QualitySettings.lodBias; meshLodThreshold = QualitySettings.meshLodThreshold;
            maximumLod = QualitySettings.maximumLODLevel; frameRate = Application.targetFrameRate; vSync = QualitySettings.vSyncCount;
            skinWeights = QualitySettings.skinWeights; anisotropic = QualitySettings.anisotropicFiltering;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (root != null) Object.Destroy(root);
            MobileQuality.ForcedMode = null;
            yield return null;
            // Back to a desktop, so the next fixture's views do not build a phone's scenery.
            MobileQuality.Apply(null, null, false);
            QualitySettings.renderPipeline = pipeline; QualitySettings.lodBias = lodBias; QualitySettings.meshLodThreshold = meshLodThreshold;
            QualitySettings.maximumLODLevel = maximumLod; Application.targetFrameRate = frameRate; QualitySettings.vSyncCount = vSync;
            QualitySettings.skinWeights = skinWeights; QualitySettings.anisotropicFiltering = anisotropic;
        }

        private MatchController StartMatch()
        {
            root = new GameObject("Mobile quality match");
            return root.AddComponent<MatchController>();
        }

        [UnityTest]
        public IEnumerator ForcedLowTierRendersThroughItsOwnCopyOfTheShippedAsset()
        {
            MobileQuality.ForcedMode = "low";
            var match = StartMatch();
            yield return null;
            var tier = MobileTierSettings.For(MobileTier.Low);
            Assert.That(MobileQuality.Mode, Is.EqualTo("low"));
            Assert.That(MobileQuality.Active, Is.SameAs(tier));
            var shipped = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            var active = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            Assert.That(active, Is.Not.Null.And.Not.SameAs(shipped), "a tier never edits the shipped asset");
            Assert.That(active.name, Is.EqualTo("Mobile low (runtime copy)"));
            Assert.That(active.renderScale, Is.EqualTo(MobileTiers.StartingRenderScale(tier, Screen.width, Screen.height)).Within(.001f));
            Assert.That(active.msaaSampleCount, Is.EqualTo(tier.MsaaSamples));
            Assert.That(active.supportsHDR, Is.EqualTo(tier.Hdr));
            Assert.That(active.mainLightShadowmapResolution, Is.EqualTo(tier.ShadowResolution));
            Assert.That(active.shadowDistance, Is.EqualTo(tier.ShadowDistance));
            Assert.That(active.shadowCascadeCount, Is.EqualTo(tier.ShadowCascades));
            Assert.That(active.maxAdditionalLightsCount, Is.EqualTo(tier.AdditionalLights));
            Assert.That(shipped.renderScale, Is.EqualTo(1f)); Assert.That(shipped.shadowCascadeCount, Is.EqualTo(2));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(tier.LodBias));
            Assert.That(QualitySettings.maximumLODLevel, Is.EqualTo(tier.MaximumLodLevel));
            Assert.That(QualitySettings.meshLodThreshold, Is.EqualTo(tier.MeshLodThreshold));
            Assert.That(QualitySettings.skinWeights, Is.EqualTo(SkinWeights.TwoBones));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            var sun = System.Array.Find(root.GetComponentsInChildren<Light>(), light => light.type == LightType.Directional);
            Assert.That(sun.shadows, Is.EqualTo(LightShadows.Hard), "the low tier's shadows are hard");
            var quality = root.GetComponent<MobileQuality>();
            Assert.That(quality, Is.Not.Null, "phones get dynamic resolution");
            Assert.That(quality.Governor.Maximum, Is.EqualTo(active.renderScale));
            Assert.That(quality.Governor.Minimum, Is.EqualTo(System.Math.Min(tier.MinimumRenderScale, active.renderScale)));
            Assert.That(match.World, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ForcedBaselineKeepsTheShippedAssetAndTheMobileLevel()
        {
            MobileQuality.ForcedMode = MobileQuality.BaselineMode;
            StartMatch();
            yield return null;
            Assert.That(MobileQuality.Mode, Is.EqualTo(MobileQuality.BaselineMode));
            Assert.That(MobileQuality.Active, Is.Null);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(GraphicsSettings.defaultRenderPipeline), "no desktop shadow upgrade");
            Assert.That(QualitySettings.lodBias, Is.EqualTo(MobileQuality.LevelLodBias));
            Assert.That(QualitySettings.skinWeights, Is.EqualTo(MobileQuality.LevelSkinWeights));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(MobileQuality.LevelAnisotropic));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            Assert.That(root.GetComponent<MobileQuality>(), Is.Null, "the baseline has no dynamic resolution");
        }

        [UnityTest]
        public IEnumerator DesktopIsLeftExactlyAsItWas()
        {
            float bias = QualitySettings.lodBias; var weights = QualitySettings.skinWeights; var filtering = QualitySettings.anisotropicFiltering;
            var match = StartMatch();
            yield return null;
            Assert.That(MobileQuality.Mode, Is.EqualTo(MobileQuality.DesktopMode));
            Assert.That(MobileQuality.Active, Is.Null);
            Assert.That(root.GetComponent<MobileQuality>(), Is.Null);
            var active = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            Assert.That(active, Is.Not.Null);
            Assert.That(active.name, Does.Not.StartWith("Mobile "));
            Assert.That(active.msaaSampleCount, Is.EqualTo(4), "the desktop shadow upgrade still applies");
            Assert.That(active.shadowCascadeCount, Is.EqualTo(3));
            Assert.That(active.renderScale, Is.EqualTo(1f));
            // A desktop paces by the saved choice (FramePacing): the display's own rate unless the player picked a cap.
            bool display = match.Alpha.Settings.Value.FrameRate == FramePacing.Display;
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(display ? 1 : 0));
            if (display) Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            Assert.That(QualitySettings.lodBias, Is.EqualTo(bias));
            Assert.That(QualitySettings.skinWeights, Is.EqualTo(weights));
            Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(filtering));
        }

#if UNITY_EDITOR
        [Test]
        public void ForcedTiersStartFromTheMobileQualityLevel()
        {
            // A desktop player has no Mobile level, so MobileQuality repeats its values; they must stay in step. The
            // editor hides levels the active platform excludes, so the level is read from the settings file itself.
            string text = System.IO.File.ReadAllText("ProjectSettings/QualitySettings.asset").Replace("\r", "");
            var level = System.Text.RegularExpressions.Regex.Match(text, @"\n    name: Mobile\n(?:    .*\n)*");
            Assert.That(level.Success, Is.True, "the project has a Mobile quality level");
            string Value(string field) => System.Text.RegularExpressions.Regex.Match(level.Value, @"\n    " + field + @": (\S+)").Groups[1].Value;
            Assert.That(float.Parse(Value("lodBias"), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(MobileQuality.LevelLodBias));
            Assert.That(float.Parse(Value("meshLodThreshold"), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(MobileQuality.LevelMeshLodThreshold));
            Assert.That(int.Parse(Value("maximumLODLevel")), Is.EqualTo(MobileQuality.LevelMaximumLod));
            Assert.That(int.Parse(Value("skinWeights")), Is.EqualTo((int)MobileQuality.LevelSkinWeights));
            Assert.That(int.Parse(Value("anisotropicTextures")), Is.EqualTo((int)MobileQuality.LevelAnisotropic));
            Assert.That(int.Parse(Value("vSyncCount")), Is.EqualTo(0));
        }
#endif
    }
}
