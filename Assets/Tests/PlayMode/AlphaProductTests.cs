using System;
using System.IO;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    public sealed class AlphaProductTests
    {
        private string directory;
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "EmberfieldAlphaTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void SettingsRoundTripSurvivesANewStoreAndBoundsInvalidValues()
        {
            var store = new AlphaSettingsStore(directory);
            store.Value.SoundEnabled = false; store.Value.SoundVolume = 99; store.Value.MusicVolume = -2;
            store.Value.PanSpeed = float.NaN; store.Value.ZoomSpeed = -3; store.Value.DragTolerance = 8;
            store.Value.HighContrastText = true; store.Value.TutorialProgress = 17; store.Value.TutorialActive = true;
            Assert.IsTrue(store.Save());
            var reload = new AlphaSettingsStore(directory).Value;
            Assert.IsFalse(reload.SoundEnabled); Assert.AreEqual(1, reload.SoundVolume);
            // Silence is a legal choice for the score alone, so this one clamps to zero rather than to a floor.
            Assert.AreEqual(0, reload.MusicVolume);
            Assert.AreEqual(1, reload.PanSpeed); Assert.AreEqual(.5f, reload.ZoomSpeed); Assert.AreEqual(2, reload.DragTolerance);
            Assert.IsTrue(reload.HighContrastText); Assert.AreEqual(17, reload.TutorialProgress); Assert.IsTrue(reload.TutorialActive);
            Assert.IsFalse(reload.DiagnosticsConsent, "Reports must require their own explicit opt-in.");
        }

        [TestCase("broken JSON")]
        [TestCase("{\"Version\":999,\"DiagnosticsConsent\":true}")]
        [TestCase("{\"DiagnosticsConsent\":true}")]
        public void InvalidSettingsFallBackToUsableDefaultsWithoutEnablingReports(string contents)
        {
            File.WriteAllText(Path.Combine(directory, "settings.json"), contents);
            var value = new AlphaSettingsStore(directory).Value;
            Assert.AreEqual(1, value.Version); Assert.AreEqual(1, value.PanSpeed); Assert.IsTrue(value.SoundEnabled); Assert.IsFalse(value.DiagnosticsConsent);
        }

        [Test]
        public void PartialVersionedSettingsKeepDefaultsAndOversizedFilesAreIgnored()
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"Version\":1,\"HighContrastText\":true}");
            var value = new AlphaSettingsStore(directory).Value;
            Assert.IsTrue(value.HighContrastText); Assert.AreEqual(.8f, value.SoundVolume); Assert.AreEqual(1, value.PanSpeed);
            // A settings file written before the score had its own level must not read back as silent music.
            Assert.AreEqual(.7f, value.MusicVolume);
            File.WriteAllText(path, new string('x', AlphaSettingsStore.MaximumFileBytes + 1));
            Assert.IsFalse(new AlphaSettingsStore(directory).Value.DiagnosticsConsent);
        }

        [Test]
        public void ReportsRequireConsentRetainOnlyBoundedTypedRecordsAndRevokeDeletesExports()
        {
            using (var diagnostics = new AlphaDiagnostics(directory, false))
            {
                diagnostics.Record(AlphaDiagnosticKind.ExceptionObserved, 2, 4);
                Assert.IsEmpty(diagnostics.Records); Assert.IsFalse(diagnostics.Export()); Assert.IsEmpty(Directory.GetFiles(directory));
                diagnostics.SetConsent(true);
                for (int i = 0; i < 180; i++) diagnostics.Record(AlphaDiagnosticKind.TutorialStep, i, i);
                Assert.AreEqual(AlphaDiagnostics.Capacity, diagnostics.Records.Count);
                Assert.AreEqual(52, diagnostics.Records[0].Tick); Assert.AreEqual(179, diagnostics.Records[127].Tick);
                Assert.IsTrue(diagnostics.Export());
                var export = File.ReadAllText(diagnostics.ExportPath);
                var data = JsonUtility.FromJson<AlphaDiagnosticEnvelope>(export);
                Assert.AreEqual(128, data.Records.Length); Assert.Less(new FileInfo(diagnostics.ExportPath).Length, AlphaDiagnostics.MaximumFileBytes);
                StringAssert.DoesNotContain("password", export); StringAssert.DoesNotContain("device", export); StringAssert.DoesNotContain("stack", export);
                diagnostics.SetConsent(false);
                Assert.IsEmpty(diagnostics.Records); Assert.IsEmpty(Directory.GetFiles(directory));
            }
        }

        [Test]
        public void CleanExitDoesNotReportInterruptionButAnExistingSessionMarkerDoes()
        {
            using (var first = new AlphaDiagnostics(directory, true)) { Assert.IsFalse(Contains(first, AlphaDiagnosticKind.PreviousSessionInterrupted)); }
            using (var second = new AlphaDiagnostics(directory, true)) { Assert.IsFalse(Contains(second, AlphaDiagnosticKind.PreviousSessionInterrupted)); second.Clear(); }
            File.WriteAllText(Path.Combine(directory, "session-active.json"), "{\"Version\":1}");
            using (var third = new AlphaDiagnostics(directory, true)) { Assert.IsTrue(Contains(third, AlphaDiagnosticKind.PreviousSessionInterrupted)); }
            Assert.IsFalse(File.Exists(Path.Combine(directory, "session-active.json")));
        }

        [Test]
        public void DamagedReportsCannotInjectArbitraryFieldsOrUnboundedValuesIntoAnExport()
        {
            File.WriteAllText(Path.Combine(directory, "diagnostics.json"), "{\"Version\":1,\"Records\":[{\"Kind\":999,\"UtcSeconds\":1,\"Tick\":0,\"Value\":0},{\"Kind\":0,\"UtcSeconds\":1,\"Tick\":0,\"Value\":2147483647,\"password\":\"secret\"}]}");
            using (var diagnostics = new AlphaDiagnostics(directory, true))
            {
                Assert.AreEqual(1, diagnostics.Records.Count, "Only the new session event is retained.");
                diagnostics.Record((AlphaDiagnosticKind)999, -3, int.MaxValue);
                Assert.AreEqual(1, diagnostics.Records.Count);
                Assert.IsTrue(diagnostics.Export()); StringAssert.DoesNotContain("secret", File.ReadAllText(diagnostics.ExportPath));
            }
        }

        [Test]
        public void TutorialNeedsActualCameraSelectionAcceptedMoveAndPhysicalMovement()
        {
            var world = DefinitionLoader.CreateWorld(); var guide = new AlphaTutorial(world, 1);
            var initialResources = world.Players[0].Resources; int unitsBefore = world.Units.Count;
            guide.ObserveWorld(); guide.ObserveCamera(Vector3.zero, 8); guide.ObserveCamera(Vector3.zero, 8);
            Assert.AreEqual(AlphaTutorialStep.Camera, guide.Step);
            guide.ObserveCamera(new Vector3(2, 0, 0), 8); guide.ObserveSelection(new[] { 5 });
            Assert.AreEqual(AlphaTutorialStep.Selection, guide.Step, "The opponent's worker cannot satisfy selection.");
            guide.ObserveSelection(new[] { 1 }); Assert.AreEqual(AlphaTutorialStep.Movement, guide.Step);
            var invalid = new MoveCommand(1, new[] { 5 }, new SimPoint(18500, 17500));
            guide.ObserveCommand(invalid, world.Submit(invalid));
            var move = new MoveCommand(1, new[] { 1 }, new SimPoint(18500, 17500));
            var accepted = world.Submit(move); Assert.IsTrue(accepted.Accepted); guide.ObserveCommand(move, accepted);
            guide.ObserveWorld(); Assert.AreEqual(AlphaTutorialStep.Movement, guide.Step, "An accepted order alone does not prove movement.");
            Advance(world, guide, () => Has(guide, AlphaTutorialStep.Movement));
            Assert.AreEqual(initialResources, world.Players[0].Resources); Assert.AreEqual(unitsBefore, world.Units.Count);
        }

        [Test]
        public void TutorialGatesDeliveryConstructionTrainingAndEraOnCompletedWorldWork()
        {
            var world = DefinitionLoader.CreateWorld(); var guide = new AlphaTutorial(world, 1);
            world.TryGetPlayer(1, out var player);
            Assert.IsTrue(world.Submit(new GatherCommand(1, new[] { 1 }, 200)).Accepted);
            Assert.IsTrue(world.Submit(new GatherCommand(1, new[] { 2 }, 201)).Accepted);
            Advance(world, guide, () => world.Units[0].CarriedAmount > 0);
            Assert.IsFalse(Has(guide, AlphaTutorialStep.Delivery));
            Advance(world, guide, () => Has(guide, AlphaTutorialStep.Delivery));
            var build = world.Submit(new BuildCommand(1, new[] { 3 }, "shelter", new SimPoint(21000, 16000)));
            Assert.IsTrue(build.Accepted, build.Message); guide.ObserveWorld();
            Assert.IsFalse(Has(guide, AlphaTutorialStep.Building));
            Advance(world, guide, () => Has(guide, AlphaTutorialStep.Building));
            var train = world.Submit(new TrainCommand(1, 100, "tender")); Assert.IsTrue(train.Accepted, train.Message);
            Assert.IsFalse(Has(guide, AlphaTutorialStep.Training));
            Advance(world, guide, () => Has(guide, AlphaTutorialStep.Training));
            TechnologyDefinition era = null;
            foreach (var technology in world.Definition.Technologies) if (technology.Id == "advance_kingdom") era = technology;
            Assert.IsNotNull(era);
            Advance(world, guide, () => player.Resources.Food >= era.Cost.Food && player.Resources.Wood >= era.Cost.Wood);
            var research = world.Submit(new ResearchCommand(1, 100, era.Id)); Assert.IsTrue(research.Accepted, research.Message);
            Assert.IsFalse(Has(guide, AlphaTutorialStep.Era));
            Advance(world, guide, () => Has(guide, AlphaTutorialStep.Era));
            Assert.AreEqual(2, player.EraTier);
        }

        [Test]
        public void TutorialVictoryRequiresLocalWinEvenWhenResultArrivesWithoutAnotherTick()
        {
            var world = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            var guide = new AlphaTutorial(world, 1, AlphaTutorial.AllSteps & ~(1 << (int)AlphaTutorialStep.Victory));
            guide.ObserveWorld(); Assert.IsFalse(guide.IsComplete);
            Assert.IsTrue(world.Submit(new SurrenderCommand(2)).Accepted); guide.ObserveWorld(); Assert.IsTrue(guide.IsComplete);
            var loss = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest); var losingGuide = new AlphaTutorial(loss, 1);
            loss.Submit(new SurrenderCommand(1)); losingGuide.ObserveWorld(); Assert.IsFalse(Has(losingGuide, AlphaTutorialStep.Victory));
            var restored = new AlphaTutorial(DefinitionLoader.CreateWorld(), 1, guide.Progress); Assert.IsTrue(restored.IsComplete);
        }

        [Test]
        public void TutorialAttackRequiresAValidatedLocalOrderAgainstAnEnemy()
        {
            var world = DefinitionLoader.CreateWorld("Maps/combat_sandbox"); var guide = new AlphaTutorial(world, 1);
            UnitState local = null, enemy = null;
            foreach (var unit in world.Units) if (unit.AttackDamage > 0) { if (unit.OwnerId == 1 && local == null) local = unit; if (unit.OwnerId == 2 && enemy == null) enemy = unit; }
            Assert.IsNotNull(local); Assert.IsNotNull(enemy);
            var invalid = new AttackCommand(1, new[] { local.Id }, local.Id);
            var rejected = world.Submit(invalid); Assert.IsFalse(rejected.Accepted); guide.ObserveCommand(invalid, rejected);
            Assert.IsFalse(Has(guide, AlphaTutorialStep.Attack));
            var attack = new AttackCommand(1, new[] { local.Id }, enemy.Id); var accepted = world.Submit(attack);
            Assert.IsTrue(accepted.Accepted, accepted.Message); guide.ObserveCommand(attack, accepted);
            Assert.IsTrue(Has(guide, AlphaTutorialStep.Attack)); Assert.AreEqual(enemy.Id, local.AttackTargetId);
        }

        private static bool Has(AlphaTutorial guide, AlphaTutorialStep step) => (guide.Progress & (1 << (int)step)) != 0;
        private static bool Contains(AlphaDiagnostics diagnostics, AlphaDiagnosticKind kind) { foreach (var item in diagnostics.Records) if (item.Kind == kind) return true; return false; }
        private static void Advance(World world, AlphaTutorial guide, Func<bool> done)
        { for (int i = 0; i < 20000 && !done(); i++) { world.Tick(); guide.ObserveWorld(); } Assert.IsTrue(done(), "Real simulation did not reach the tutorial milestone."); }
    }
}
