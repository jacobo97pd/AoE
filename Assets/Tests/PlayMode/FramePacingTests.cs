using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    public class FramePacingTests
    {
        private int frameRate, vSync;

        [SetUp]
        public void Remember() { frameRate = Application.targetFrameRate; vSync = QualitySettings.vSyncCount; MobileQuality.ForcedMode = null; }

        [TearDown]
        public void Restore() { MobileQuality.ForcedMode = null; Application.targetFrameRate = frameRate; QualitySettings.vSyncCount = vSync; }

        [Test]
        public void ADesktopWaitsForTheDisplayUnlessTheChoiceCapsIt()
        {
            FramePacing.Begin(false);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1)); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            FramePacing.Choose(FramePacing.Cap60);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            FramePacing.Choose(FramePacing.Cap30);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            FramePacing.Choose(FramePacing.Unlimited);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            FramePacing.Choose(FramePacing.Display);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1)); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
        }

        [Test]
        public void RepeatingTheChoiceInForceLeavesAProbesOwnPacingAlone()
        {
            FramePacing.Begin(false); FramePacing.Choose(FramePacing.Display);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            FramePacing.Choose(FramePacing.Display);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0), "a settings save must not undo a probe's uncapped run");
        }

        [Test]
        public void StressRunsUncappedAndPhonesKeepTheirTierCap()
        {
            QualitySettings.vSyncCount = 1;
            FramePacing.Begin(true);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            MobileQuality.ForcedMode = "low";
            FramePacing.Begin(false);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            FramePacing.Choose(FramePacing.Display);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0), "a phone never takes the desktop choice");
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));
        }
    }
}
