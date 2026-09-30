using System;
using System.Linq;
using Emberfield.Quality;
using NUnit.Framework;

namespace Emberfield.Tests.EditMode
{
    public class MobileTierTests
    {
        private static readonly MobileTier[] Tiers = { MobileTier.Low, MobileTier.Mid, MobileTier.High };

        // Memory as Unity reports it on the device: a 3 GB phone shows about 2,800 MB, 4 GB about 3,700, 6 GB about 5,600.
        [TestCase(2800, 8, 0, 8192, "Vulkan", MobileTier.Low, TestName = "A 3 GB phone is low")]
        [TestCase(3700, 8, 0, 8192, "OpenGLES3", MobileTier.Low, TestName = "A phone without Vulkan is low")]
        [TestCase(7600, 3, 0, 16384, "Vulkan", MobileTier.Low, TestName = "Three cores are low")]
        [TestCase(7600, 8, 0, 2048, "Vulkan", MobileTier.Low, TestName = "A 2048 texture limit is low")]
        [TestCase(3700, 8, 0, 8192, "Vulkan", MobileTier.Mid, TestName = "A 4 GB Vulkan phone is mid")]
        [TestCase(7600, 4, 0, 16384, "Vulkan", MobileTier.Mid, TestName = "Four cores stop at mid")]
        [TestCase(5600, 8, 512, 16384, "Vulkan", MobileTier.Mid, TestName = "Little graphics memory stops at mid")]
        [TestCase(5600, 8, 0, 4096, "Vulkan", MobileTier.Mid, TestName = "A 4096 texture limit stops at mid")]
        [TestCase(3800, 6, 1900, 16384, "Metal", MobileTier.Mid, TestName = "A 4 GB iPhone is mid")]
        [TestCase(5600, 8, 0, 16384, "Vulkan", MobileTier.High, TestName = "A 6 GB Vulkan phone is high")]
        [TestCase(5700, 6, 2800, 16384, "Metal", MobileTier.High, TestName = "A 6 GB iPhone is high")]
        [TestCase(11200, 8, 4096, 16384, "Vulkan", MobileTier.High, TestName = "A 12 GB phone is high")]
        public void DevicesLandInTheirTier(int memory, int cores, int graphicsMemory, int textureSize, string api, MobileTier expected)
        {
            Assert.That(MobileTiers.Choose(new DeviceProfile(memory, cores, graphicsMemory, textureSize, api)), Is.EqualTo(expected));
        }

        [Test]
        public void MoreOfAnythingNeverLowersTheTier()
        {
            int[] memories = { 0, 2000, 3300, 4000, 5300, 8000 }, cores = { 1, 4, 6, 8 }, graphics = { 0, 256, 1024, 4096 }, textures = { 2048, 4096, 8192, 16384 };
            string[] apis = { "OpenGLES3", "Vulkan" };
            foreach (int m in memories) foreach (int c in cores) foreach (int g in graphics) foreach (int t in textures) foreach (string a in apis)
            {
                var tier = MobileTiers.Choose(new DeviceProfile(m, c, g, t, a));
                Assert.That(MobileTiers.Choose(new DeviceProfile(m + 3000, c, g, t, a)), Is.GreaterThanOrEqualTo(tier));
                Assert.That(MobileTiers.Choose(new DeviceProfile(m, c + 2, g, t, a)), Is.GreaterThanOrEqualTo(tier));
                Assert.That(MobileTiers.Choose(new DeviceProfile(m, c, g * 4, t, a)), Is.GreaterThanOrEqualTo(tier));
                Assert.That(MobileTiers.Choose(new DeviceProfile(m, c, g, t * 2, a)), Is.GreaterThanOrEqualTo(tier));
                Assert.That(MobileTiers.Choose(new DeviceProfile(m, c, g, t, "Vulkan")), Is.GreaterThanOrEqualTo(tier));
            }
        }

        [TestCase("low", MobileTier.Low)]
        [TestCase(" MID ", MobileTier.Mid)]
        [TestCase("High", MobileTier.High)]
        public void TierNamesParse(string text, MobileTier expected)
        {
            Assert.That(MobileTiers.TryParse(text, out var tier), Is.True);
            Assert.That(tier, Is.EqualTo(expected));
        }

        [TestCase("ultra")]
        [TestCase("baseline")]
        [TestCase("")]
        [TestCase((string)null)]
        public void OtherNamesDoNotParse(string text) => Assert.That(MobileTiers.TryParse(text, out _), Is.False);

        [Test]
        public void EachTierIsInTheTableUnderItsOwnName()
        {
            foreach (var tier in Tiers) Assert.That(MobileTierSettings.For(tier).Tier, Is.EqualTo(tier));
            Assert.Throws<ArgumentOutOfRangeException>(() => MobileTierSettings.For((MobileTier)3));
        }

        [Test]
        public void EveryTierAsksForSomethingAPhoneCanDo()
        {
            foreach (var settings in Tiers.Select(MobileTierSettings.For))
            {
                string name = settings.Tier.ToString();
                Assert.That(settings.RenderScale, Is.InRange(.5f, 1f), name);
                Assert.That(settings.MinimumRenderScale, Is.InRange(.6f, settings.RenderScale), name + ": dynamic resolution stops at 0.6");
                Assert.That(settings.RenderHeight, Is.InRange(540, 1440), name);
                Assert.That(new[] { 1, 2, 4 }, Does.Contain(settings.MsaaSamples), name);
                Assert.That(new[] { 512, 1024, 2048 }, Does.Contain(settings.ShadowResolution), name + ": a 4096 shadow map is 32 MB");
                Assert.That(settings.ShadowCascades, Is.InRange(1, 4), name);
                Assert.That(settings.ShadowDistance, Is.GreaterThanOrEqualTo(MobileTierSettings.DeepestView), name + ": shadows must reach the far edge of the widest view");
                Assert.That(settings.AdditionalLights, Is.InRange(0, 8), name);
                Assert.That(settings.LodBias, Is.InRange(.5f, 2f), name);
                Assert.That(settings.MaximumLodLevel, Is.EqualTo(0), name + ": skipping LOD0 would drop art with a single level");
                Assert.That(settings.MeshLodThreshold, Is.GreaterThan(0), name);
                Assert.That(new[] { 1, 2, 4 }, Does.Contain(settings.SkinWeights), name);
                Assert.That(settings.SceneryDetailHeight, Is.GreaterThan(MobileTiers.DesktopSceneryDetailHeight).And.LessThanOrEqualTo(.5f), name);
                Assert.That(settings.UnitDetailHeight, Is.GreaterThan(0).And.LessThanOrEqualTo(.5f), name);
            }
        }

        [Test]
        public void HigherTiersNeverAskForLess()
        {
            for (int i = 1; i < Tiers.Length; i++)
            {
                var lower = MobileTierSettings.For(Tiers[i - 1]); var higher = MobileTierSettings.For(Tiers[i]);
                Assert.That(higher.RenderScale, Is.GreaterThanOrEqualTo(lower.RenderScale));
                Assert.That(higher.RenderHeight, Is.GreaterThanOrEqualTo(lower.RenderHeight));
                Assert.That(higher.MsaaSamples, Is.GreaterThanOrEqualTo(lower.MsaaSamples));
                Assert.That(higher.ShadowResolution, Is.GreaterThanOrEqualTo(lower.ShadowResolution));
                Assert.That(higher.ShadowDistance, Is.GreaterThanOrEqualTo(lower.ShadowDistance));
                Assert.That(higher.SoftShadows || !lower.SoftShadows, Is.True);
                Assert.That(higher.AdditionalLights, Is.GreaterThanOrEqualTo(lower.AdditionalLights));
                Assert.That(higher.LodBias, Is.GreaterThanOrEqualTo(lower.LodBias));
                Assert.That(higher.MeshLodThreshold, Is.LessThanOrEqualTo(lower.MeshLodThreshold));
                Assert.That(higher.SkinWeights, Is.GreaterThanOrEqualTo(lower.SkinWeights));
                Assert.That(higher.TargetFrameRate, Is.GreaterThanOrEqualTo(lower.TargetFrameRate));
                Assert.That(higher.SceneryDetailHeight, Is.LessThanOrEqualTo(lower.SceneryDetailHeight), "a higher tier keeps detailed scenery at least as far out");
                Assert.That(higher.UnitDetailHeight, Is.LessThan(lower.UnitDetailHeight));
                Assert.That(higher.SceneryShadows || !lower.SceneryShadows, Is.True);
            }
        }

        [Test]
        public void OnlyTheLowTierDropsSceneryShadows()
        {
            Assert.That(MobileTierSettings.For(MobileTier.Low).SceneryShadows, Is.False, "the blocked-cell fillers were most of the low tier's shadow casters");
            foreach (var tier in new[] { MobileTier.Mid, MobileTier.High }) Assert.That(MobileTierSettings.For(tier).SceneryShadows, Is.True, tier.ToString());
        }

        [Test]
        public void TheLowTierDrawsNoEmbersAndTheHighTierDrawsThemAll()
        {
            // The lands' embers, glimmers and smoke (LandMotes): none of the first two on low, half on mid, all on high.
            Assert.That(MobileTierSettings.For(MobileTier.Low).AmbientParticleShare, Is.EqualTo(0));
            Assert.That(MobileTierSettings.For(MobileTier.Mid).AmbientParticleShare, Is.EqualTo(.5f));
            Assert.That(MobileTierSettings.For(MobileTier.High).AmbientParticleShare, Is.EqualTo(1));
        }

        // The orthographic battlefield camera zooms between half-heights of 5 and 22 m, so a model fills its size over
        // twice that of the screen. The fillers run from a 0.82 m rock (a 1 m rock at its smallest scale) to a 4.1 m tree.
        private const float ClosestZoom = 5, PlayZoom = 8, WidestZoom = 22, SmallestFiller = .82f;

        [TestCase(.12f, .7f, .084f)]
        [TestCase(.012f, 2, .024f)]
        [TestCase(.001f, 1, .008f, TestName = "The switch never reaches the cull")]
        public void TheSceneryThresholdCarriesTheLodBias(float share, float lodBias, float expected)
        {
            Assert.That(MobileTiers.SceneryLodThreshold(share, lodBias), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void ADesktopDrawsDetailedSceneryAtEveryZoom()
        {
            // The PC quality level's LOD bias is 2. Unity multiplies a group's share of the screen by it before comparing.
            float share = SmallestFiller / (2 * WidestZoom);
            Assert.That(share * 2, Is.GreaterThanOrEqualTo(MobileTiers.SceneryLodThreshold(MobileTiers.DesktopSceneryDetailHeight, 2)));
        }

        [Test]
        public void NoTierCullsSceneryAtAZoomTheGameAllows()
        {
            float share = SmallestFiller / (2 * WidestZoom);
            foreach (var settings in Tiers.Select(MobileTierSettings.For))
                Assert.That(share * settings.LodBias, Is.GreaterThan(MobileTiers.SceneryCullHeight), settings.Tier.ToString());
            Assert.That(share * MobileQualityLevelBias, Is.GreaterThan(MobileTiers.SceneryCullHeight), "the phone as it was");
        }

        // The Mobile quality level's LOD bias (MobileQuality.LevelLodBias), which the baseline keeps.
        private const float MobileQualityLevelBias = 1;

        [TestCase(.12f, 3, 12.5f)]
        [TestCase(.23f, 3, 6.52f)]
        [TestCase(.08f, 3.3f, 20.63f)]
        [TestCase(0, 3, float.PositiveInfinity, TestName = "A share of nothing never switches")]
        public void AModelSwitchesAtTheZoomWhereItFillsTheShare(float share, float size, float zoom)
        {
            Assert.That(MobileTiers.SceneryDetailZoom(share, size), Is.EqualTo(zoom).Within(.01f));
            if (share > 0) Assert.That(size / (2 * MobileTiers.SceneryDetailZoom(share, size)), Is.EqualTo(share).Within(1e-5f), "it fills the share there");
        }

        [Test]
        public void EveryTierKeepsDetailedTreesWellPastThePlayZoomAndLightensThemAtTheWidest()
        {
            // A 3 m broadleaf fills 3 / 16 of the screen at the play zoom, 3 / 10 zoomed right in and 3 / 44 at the widest,
            // a 3.3 m pine 3.3 / 16 at the play zoom. Fillers are measured at their model's size, not each copy's scale.
            var low = MobileTierSettings.For(MobileTier.Low); var mid = MobileTierSettings.For(MobileTier.Mid); var high = MobileTierSettings.For(MobileTier.High);
            foreach (var settings in new[] { low, mid, high })
            {
                float broadleaf = MobileTiers.SceneryDetailZoom(settings.SceneryDetailHeight, 3);
                // The play zoom is where most of a match is watched; at 0.23 low drew every tree as a rounded shell there.
                Assert.That(broadleaf / MobileTiers.SceneryHysteresis, Is.GreaterThan(PlayZoom * 1.3f), settings.Tier + ": detailed trees well past the play zoom, even coming back in");
                Assert.That(MobileTiers.SceneryDetailZoom(settings.SceneryDetailHeight, 3.3f), Is.LessThan(WidestZoom), settings.Tier + ": even the pine is light at the widest");
            }
            Assert.That(low.SceneryDetailHeight, Is.EqualTo(mid.SceneryDetailHeight), "low draws mid's trees and saves on their shadows instead");
            Assert.That(MobileTiers.SceneryDetailZoom(high.SceneryDetailHeight, 3), Is.GreaterThanOrEqualTo(18f), "high keeps the detailed tree to an 18 m zoom");
        }

        [Test]
        public void AGatherableNodeSwitchesAtTheZoomTheTreesDo()
        {
            foreach (var settings in Tiers.Select(MobileTierSettings.For))
            {
                float treeZoom = MobileTiers.SceneryDetailZoom(settings.SceneryDetailHeight, MobileTiers.SceneryTreeHeight);
                Assert.That(MobileTiers.NodeDetailZoom(settings.SceneryDetailHeight), Is.EqualTo(treeZoom).Within(1e-4f), settings.Tier.ToString());
                // A 1 m berry bush keeps its berries wherever the trees keep their detail, the play zoom included.
                Assert.That(MobileTiers.NodeDetailZoom(settings.SceneryDetailHeight), Is.GreaterThan(PlayZoom), settings.Tier.ToString());
            }
            // Measured at its own size, a 1 m berry bush would be light at every zoom on mid, the closest included.
            Assert.That(MobileTiers.SceneryDetailZoom(MobileTierSettings.For(MobileTier.Mid).SceneryDetailHeight, 1), Is.LessThan(ClosestZoom));
        }

        [TestCase(false, 12.4f, 12.5f, false, TestName = "Detailed inside the switch")]
        [TestCase(false, 12.6f, 12.5f, true, TestName = "Light past it")]
        [TestCase(true, 12.4f, 12.5f, true, TestName = "Light stays light just inside it")]
        [TestCase(true, 11.5f, 12.5f, true, TestName = "Light stays light within the hysteresis")]
        [TestCase(true, 11.3f, 12.5f, false, TestName = "Detailed again past the hysteresis")]
        [TestCase(false, 12.5f, 12.5f, false, TestName = "Detailed on the switch itself")]
        public void ALightModelComesBackOnlyWellInsideItsSwitch(bool lighter, float zoom, float switchZoom, bool expected)
        {
            Assert.That(MobileTiers.DrawsLighterLevel(lighter, zoom, switchZoom), Is.EqualTo(expected));
        }

        [Test]
        public void ZoomingOutAndBackAcrossASwitchFlipsTheLevelOnceEachWay()
        {
            // A pinch that rests on the switch and wobbles a few percent either side changes nothing after the first step.
            bool lighter = false; int flips = 0;
            foreach (float zoom in new[] { 12f, 12.6f, 12.3f, 12.7f, 12.2f, 12.6f, 11f, 11.8f, 12.4f, 11.9f })
            {
                bool next = MobileTiers.DrawsLighterLevel(lighter, zoom, 12.5f);
                if (next != lighter) flips++;
                lighter = next;
            }
            Assert.That(flips, Is.EqualTo(2));
            Assert.That(lighter, Is.False);
        }

        [TestCase(.2f, .2f, 4, 0)]
        [TestCase(.19f, .2f, 4, 1)]
        [TestCase(.1f, .2f, 4, 1)]
        [TestCase(.075f, .2f, 4, 2)]
        [TestCase(.03f, .2f, 4, 3)]
        [TestCase(.001f, .2f, 4, 3, TestName = "Never past the mesh's last level")]
        [TestCase(.03f, .2f, 2, 1)]
        [TestCase(.03f, .2f, 1, 0, TestName = "A mesh with one level draws it")]
        [TestCase(.03f, 0, 4, 0, TestName = "Without a tier the whole mesh")]
        public void UnitsDrawOneLighterLevelForEachHalvingOfTheirShare(float share, float detailHeight, int levels, int expected)
        {
            Assert.That(MobileTiers.UnitMeshLevel(share, detailHeight, levels), Is.EqualTo(expected));
        }

        [Test]
        public void AFootSoldierKeepsItsWholeMeshOnlyWhereTheTierCanAffordIt()
        {
            // A 1.32 m foot soldier (2.2 authored metres at the unit scale of 0.6), with the four levels the unit baker makes.
            float Share(float zoom) => 1.32f / (2 * zoom);
            var low = MobileTierSettings.For(MobileTier.Low); var mid = MobileTierSettings.For(MobileTier.Mid); var high = MobileTierSettings.For(MobileTier.High);
            Assert.That(MobileTiers.UnitMeshLevel(Share(PlayZoom), low.UnitDetailHeight, 4), Is.EqualTo(2));
            Assert.That(MobileTiers.UnitMeshLevel(Share(WidestZoom), low.UnitDetailHeight, 4), Is.EqualTo(3));
            Assert.That(MobileTiers.UnitMeshLevel(Share(PlayZoom), mid.UnitDetailHeight, 4), Is.EqualTo(1));
            Assert.That(MobileTiers.UnitMeshLevel(Share(PlayZoom), high.UnitDetailHeight, 4), Is.EqualTo(0));
            Assert.That(MobileTiers.UnitMeshLevel(Share(WidestZoom), high.UnitDetailHeight, 4), Is.EqualTo(1));
        }

        [Test]
        public void LowRunsAtThirtyAndTheOthersAtSixty()
        {
            Assert.That(MobileTierSettings.For(MobileTier.Low).TargetFrameRate, Is.EqualTo(30));
            Assert.That(MobileTierSettings.For(MobileTier.Mid).TargetFrameRate, Is.EqualTo(60));
            Assert.That(MobileTierSettings.For(MobileTier.High).TargetFrameRate, Is.EqualTo(60));
        }

        [TestCase(MobileTier.Low, 1280, 720, .8f)]
        [TestCase(MobileTier.Low, 1920, 1080, .6667f)]
        [TestCase(MobileTier.Low, 1080, 1920, .6667f)]
        [TestCase(MobileTier.Low, 1600, 720, .8f)]
        [TestCase(MobileTier.Low, 3200, 1440, .5f)]
        [TestCase(MobileTier.Mid, 1920, 1080, .8333f)]
        [TestCase(MobileTier.Mid, 3200, 1440, .625f)]
        [TestCase(MobileTier.High, 1920, 1080, 1f)]
        [TestCase(MobileTier.High, 2400, 1080, 1f)]
        [TestCase(MobileTier.High, 3200, 1440, .75f)]
        public void RenderScaleKeepsTheThreeDImageWithinTheTiersHeight(MobileTier tier, int width, int height, float expected)
        {
            Assert.That(MobileTiers.StartingRenderScale(MobileTierSettings.For(tier), width, height), Is.EqualTo(expected).Within(.001f));
        }

        [Test]
        public void SustainedOverloadStepsDownToTheFloorAndNoFurther()
        {
            var governor = new DynamicResolution(.8f, .6f, 30);
            int changes = 0;
            for (int frame = 0; frame < 60 * 30; frame++)
                if (governor.Observe(45, 45, 1 / 22.0)) changes++;
            Assert.That(governor.Scale, Is.EqualTo(.6f).Within(.001f));
            Assert.That(changes, Is.EqualTo(2), "0.8 to 0.7 to 0.6, then it holds");
        }

        [Test]
        public void OneHitchIsNotOverload()
        {
            var governor = new DynamicResolution(1, .6f, 60);
            for (int frame = 0; frame < 120; frame++) governor.Observe(9, 16.7, 1 / 60.0);
            Assert.That(governor.Observe(500, 500, .5), Is.False);
            for (int frame = 0; frame < 120; frame++) Assert.That(governor.Observe(9, 16.7, 1 / 60.0), Is.False);
            Assert.That(governor.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void OverloadTakesTwoSecondsToAct()
        {
            var governor = new DynamicResolution(1, .6f, 60);
            double seconds = 0;
            while (!governor.Observe(20, 20, 1 / 50.0)) { seconds += 1 / 50.0; Assert.That(seconds, Is.LessThan(5)); }
            Assert.That(seconds, Is.InRange(1.9, 2.5));
            Assert.That(governor.Scale, Is.EqualTo(.9f).Within(.001f));
        }

        [Test]
        public void RoomToSpareStepsBackUpAfterSixSeconds()
        {
            var governor = new DynamicResolution(1, .6f, 60);
            for (int frame = 0; frame < 5 * 50; frame++) governor.Observe(20, 20, 1 / 50.0);
            float lowered = governor.Scale;
            Assert.That(lowered, Is.LessThan(1f));
            double seconds = 0;
            while (!governor.Observe(6, 16.7, 1 / 60.0)) { seconds += 1 / 60.0; Assert.That(seconds, Is.LessThan(10)); }
            Assert.That(seconds, Is.InRange(6.0, 7.5));
            Assert.That(governor.Scale, Is.EqualTo(lowered + DynamicResolution.Step).Within(.001f));
        }

        [Test]
        public void WorkBetweenTheLinesHoldsTheScale()
        {
            var governor = new DynamicResolution(.9f, .6f, 60);
            for (int frame = 0; frame < 60 * 60; frame++)
                Assert.That(governor.Observe(16.7 * .8, 16.7, 1 / 60.0), Is.False, "80% of the budget is neither over it nor room to grow");
            Assert.That(governor.Scale, Is.EqualTo(.9f));
        }

        [Test]
        public void WithoutWorkTimesTheScaleOnlyGoesDown()
        {
            var governor = new DynamicResolution(1, .6f, 30);
            for (int frame = 0; frame < 90; frame++) governor.Observe(-1, 50, 1 / 20.0);
            float lowered = governor.Scale;
            Assert.That(lowered, Is.LessThan(1f), "missed frames show on the wall clock");
            // Holding the cap says nothing about spare time: the frame may have finished early and waited.
            for (int frame = 0; frame < 120 * 30; frame++) Assert.That(governor.Observe(-1, 33.3, 1 / 30.0), Is.False);
            Assert.That(governor.Scale, Is.EqualTo(lowered));
        }

        [Test]
        public void AStepUpTakenBackDoublesTheNextWait()
        {
            var governor = new DynamicResolution(1, .6f, 60);
            for (int frame = 0; frame < 3 * 50; frame++) governor.Observe(20, 20, 1 / 50.0);
            Assert.That(governor.Scale, Is.EqualTo(.9f).Within(.001f));
            double seconds = 0;
            while (!governor.Observe(6, 16.7, 1 / 60.0)) seconds += 1 / 60.0;
            Assert.That(seconds, Is.LessThan(7.5));
            // Full size is too much again: it steps down within the relapse window...
            while (!governor.Observe(20, 20, 1 / 50.0)) { }
            Assert.That(governor.Scale, Is.EqualTo(.9f).Within(.001f));
            // ...so the next step up waits twice as long.
            seconds = 0;
            while (!governor.Observe(6, 16.7, 1 / 60.0)) seconds += 1 / 60.0;
            Assert.That(seconds, Is.InRange(12.0, 13.5));
        }

        [Test]
        public void TheGovernorRefusesNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicResolution(1, .6f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicResolution(0, .6f, 30));
            var governor = new DynamicResolution(1, .6f, 30);
            Assert.That(governor.Observe(100, double.NaN, 1), Is.False);
            Assert.That(governor.Observe(100, 100, 0), Is.False);
            Assert.That(governor.Scale, Is.EqualTo(1f));
        }
    }
}
