using System;
using System.Collections;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using Emberfield.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class VoiceControlsIntegrationTests
    {
        private GameObject root;
        private MatchController match;
        private ScriptedVoiceRecognizer ear;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpokenSpanishRunsTheSettlementThroughTheHudHandlers()
        {
            CreateOfflineMatch(VoiceLanguage.Spanish);
            yield return null;
            Assert.IsTrue(ear.IsListening, "Always-on voice listens as soon as it is configured.");
            CollectionAssert.Contains(ear.Phrases, "entrena dos trabajadores");
            CollectionAssert.Contains(ear.Phrases, "cámara a la base");

            Say("trabajadores ociosos");
            var workers = match.Economy.SelectedWorkers();
            Assert.Greater(workers.Length, 0, match.Feedback);
            Assert.AreEqual(match.Selection.Count, workers.Length);

            Say("recolecta madera");
            foreach (int id in workers)
            {
                Assert.IsTrue(match.World.TryGetUnit(id, out var worker));
                Assert.IsTrue(match.World.TryGetResource(worker.TargetResourceId, out var node), "Every idle worker goes to a visible source. " + match.Feedback);
                Assert.AreEqual(ResourceKind.Wood, node.Kind);
            }

            int queued = WorkerQueue();
            Say("entrena dos trabajadores");
            Assert.AreEqual(queued + 2, WorkerQueue(), match.Feedback);
            StringAssert.Contains("2 trabajadores en cola", match.Feedback);

            Say("entrena un lancero");
            StringAssert.Contains("cuartel", match.Feedback, "Without a finished Muster Hall the player hears which building is missing.");

            match.Voice.PointerOverride = ShelterSpot();
            Say("construye una casa");
            Assert.AreEqual("shelter", match.Economy.PendingBuildingId);
            Assert.IsTrue(match.Economy.HasPreview && match.Economy.PlacementResult.Accepted, match.Feedback);
            Say("confirma");
            Assert.IsNull(match.Economy.PendingBuildingId, match.Feedback);
            Assert.IsTrue(match.World.Buildings.Any(b => b.OwnerId == MatchController.LocalPlayer && b.DefinitionId == "shelter" && !b.IsComplete), match.Feedback);

            Say("construye una casa");
            Assert.AreEqual("shelter", match.Economy.PendingBuildingId);
            Say("cancela");
            Assert.IsNull(match.Economy.PendingBuildingId);

            Say("ayuda");
            Refresh();
            Assert.IsTrue(match.Hud.VoicePanel.IsVisible);
            Assert.IsTrue(match.Hud.VoicePanel.HelpVisible);
            Say("cierra la ayuda");
            Refresh();
            Assert.IsFalse(match.Hud.VoicePanel.HelpVisible);
            StringAssert.Contains("Ayuda oculta", match.Feedback);
        }

        [UnityTest]
        public IEnumerator MenusOnlyAcceptMenuCommands()
        {
            CreateOfflineMatch(VoiceLanguage.Spanish);
            yield return null;
            Say("pausa");
            Assert.IsTrue(match.OfflineControls.IsPaused);
            StringAssert.Contains("Partida en pausa", match.Feedback, "Opening the menu clears the selection; the feedback must still say the game is paused.");
            int queued = WorkerQueue();
            Say("entrena un trabajador");
            Assert.AreEqual(queued, WorkerQueue(), "Orders wait while the pause menu is open.");
            StringAssert.Contains("reanuda", match.Feedback);
            Say("reanuda");
            Assert.IsFalse(match.OfflineControls.IsPaused);
            Say("entrena un trabajador");
            Assert.AreEqual(queued + 1, WorkerQueue(), match.Feedback);
        }

        [UnityTest]
        public IEnumerator PushToTalkListensOnlyWhenAskedAndUnderstandsEnglish()
        {
            CreateOfflineMatch(VoiceLanguage.English, VoiceListenMode.PushToTalk);
            yield return null;
            Assert.IsFalse(ear.IsListening, "Push-to-talk does not listen until asked.");
            Assert.IsFalse(ear.Say("select workers"));
            match.Voice.ListenOnce();
            Assert.IsTrue(ear.IsListening);
            Say("select workers");
            Assert.IsFalse(ear.IsListening, "One tap listens for one command.");
            Assert.Greater(match.Economy.SelectedWorkers().Length, 0);
            int queued = WorkerQueue();
            match.Voice.ListenOnce();
            Say("train a worker");
            Assert.AreEqual(queued + 1, WorkerQueue(), match.Feedback);
            StringAssert.Contains("1 worker queued", match.Feedback);
            Assert.AreEqual(new VoiceIntent(VoiceAction.Train, "tender", 3), match.Voice.Hear("Train 3 workers, please!"), "Typed text is read like speech.");
            Assert.IsTrue(match.Voice.Hear("dance the conga").IsNone);
            StringAssert.Contains("did not understand", match.Feedback);
        }

        [Test]
        public void EveryShippedFactionHasSpokenNamesThatRoundTrip()
        {
            var definition = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest).Definition;
            foreach (var language in new[] { VoiceLanguage.Spanish, VoiceLanguage.English })
                foreach (var faction in definition.Factions)
                {
                    var vocabulary = VoiceVocabulary.Build(definition, faction.Id, faction.RealmId, language);
                    foreach (var term in vocabulary.Units.Concat(vocabulary.Buildings))
                        Assert.IsTrue(term.Nouns.Any(noun => noun.Offered), language + " has no spoken name for " + term.Id + " (" + faction.Id + ").");
                    var parser = new VoiceCommandParser(vocabulary);
                    var phrases = vocabulary.Phrases();
                    Assert.Less(phrases.Count, 1000, "Keep the keyword list small enough for quick, accurate recognition.");
                    foreach (var phrase in phrases)
                        Assert.AreEqual(phrase.Intent, parser.Parse(phrase.Text), language + " · " + faction.Id + " · " + phrase.Text);
                }
        }

        [Test]
        public void WindowsSpeechAcceptsBothPhraseLists()
        {
            if (!WindowsVoiceRecognizer.IsSupported) Assert.Ignore("Windows speech recognition is not available on this machine.");
            var world = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest);
            world.TryGetPlayer(MatchController.LocalPlayer, out var player);
            foreach (var language in new[] { VoiceLanguage.Spanish, VoiceLanguage.English })
            {
                var texts = VoiceVocabulary.Build(world.Definition, player.FactionId, world.Map.RealmId, language).Phrases().Select(phrase => phrase.Text).ToArray();
                using (var recognizer = new WindowsVoiceRecognizer())
                {
                    recognizer.Load(texts); // Prepares the grammar only; the microphone opens on Start.
                    Assert.IsTrue(recognizer.IsAvailable, language + ": " + recognizer.Problem);
                    Assert.IsFalse(recognizer.IsListening);
                }
            }
        }

        private void CreateOfflineMatch(VoiceLanguage language, VoiceListenMode mode = VoiceListenMode.AlwaysOn)
        {
            root = new GameObject("Voice integration");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Conquest;
            match.enabled = false;
            root.SetActive(true);
            match.Shell?.Close(); match.OfflineControls.Close();
            ear = new ScriptedVoiceRecognizer();
            match.Voice.UseRecognizer(ear);
            match.Voice.SetFocused(true);
            match.Voice.Configure(mode, language);
            match.Voice.PointerOverride = new Vector2(Screen.width * .5f, Screen.height * .5f);
            Refresh();
        }

        private void Say(string phrase)
        {
            Assert.IsTrue(ear.Say(phrase), "The recognizer was not listening for «" + phrase + "».");
            match.Voice.Update();
            Assert.IsFalse(match.Voice.LastIntent.IsNone, "«" + phrase + "» was not understood.");
        }

        private int WorkerQueue() => match.World.Buildings.Where(building => building.OwnerId == MatchController.LocalPlayer)
            .Sum(building => building.ProductionQueue.Count(entry => entry.UnitDefinitionId == "tender"));

        private Vector2 ShelterSpot()
        {
            var hearth = match.World.Buildings.First(building => building.OwnerId == MatchController.LocalPlayer && building.DefinitionId == "hearth");
            int cell = match.World.Map.CellSizeMillimetres;
            match.Economy.BeginBuild("shelter");
            try
            {
                for (int ring = 3; ring < 16; ring++)
                    for (int step = 0; step < 16; step++)
                    {
                        double angle = step * Math.PI / 8;
                        match.Economy.PreviewAt(new SimPoint(hearth.Position.X + (int)(Math.Cos(angle) * ring * cell), hearth.Position.Z + (int)(Math.Sin(angle) * ring * cell)));
                        if (match.Economy.HasPreview && match.Economy.PlacementResult.Accepted)
                            return match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(match.Economy.PreviewPosition));
                    }
            }
            finally { match.Economy.CancelBuild(); }
            Assert.Fail("No valid Shelter location near the Hearth.");
            return default;
        }

        private void Refresh()
        {
            match.Hud.Invalidate();
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
        }
    }
}
