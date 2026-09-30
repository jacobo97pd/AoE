using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emberfield.Simulation;
using Emberfield.Voice;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Explicit packaged acceptance driver: -emberfieldVoiceSmoke <folder> -emberfieldOffline Conquest.
    // Normal launches never enter this code. It never opens the microphone: Windows compiles both
    // phrase lists, then scripted phrases travel the same path as recognized speech.
    public static class VoiceSmoke
    {
        [Serializable] private sealed class Report
        {
            public bool Passed, WindowsSpeechSupported, SpanishListAccepted, EnglishListAccepted;
            public int SpanishPhrases, EnglishPhrases, CommandsCarriedOut;
            public string SpanishProblem = "", EnglishProblem = "", Failure = "", BuildGuid, UnityVersion;
            public List<string> Heard = new List<string>();
        }
        private static Report report;
        private static string folder;

        public static void TryStart(MatchController match)
        {
            if (!Debug.isDebugBuild || match.Voice == null) return;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-emberfieldVoiceSmoke");
            if (index < 0 || index + 1 >= args.Length) return;
            folder = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(folder);
            report = new Report { BuildGuid = Application.buildGUID, UnityVersion = Application.unityVersion };
            Application.runInBackground = true;
            match.StartCoroutine(Guard(Run(match)));
        }

        private static IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception error) { report.Failure = error.GetType().Name + ": " + error.Message; Finish(false); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
        }

        private static IEnumerator Run(MatchController match)
        {
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return null;
            match.Shell?.Close(); match.OfflineControls.Close();
            yield return null;
            Require(match.World.Match != null, "Launch the voice smoke with -emberfieldOffline so a match is running.");
            report.WindowsSpeechSupported = WindowsVoiceRecognizer.IsSupported;
            report.SpanishPhrases = Compile(match, VoiceLanguage.Spanish, out report.SpanishListAccepted, out report.SpanishProblem);
            report.EnglishPhrases = Compile(match, VoiceLanguage.English, out report.EnglishListAccepted, out report.EnglishProblem);
            Require(!report.WindowsSpeechSupported || report.SpanishListAccepted && report.EnglishListAccepted,
                "Windows rejected a phrase list. " + report.SpanishProblem + " " + report.EnglishProblem);

            var ear = new ScriptedVoiceRecognizer();
            match.Voice.UseRecognizer(ear);
            match.Voice.Configure(VoiceListenMode.AlwaysOn, VoiceLanguage.Spanish);
            yield return null;
            yield return Capture(match, "voice-chip.png");
            yield return Say(match, ear, "trabajadores ociosos", () => match.Economy.SelectedWorkers().Length > 0);
            var workers = match.Economy.SelectedWorkers();
            yield return Say(match, ear, "recolecta madera", () => workers.All(id =>
                match.World.TryGetUnit(id, out var worker) && match.World.TryGetResource(worker.TargetResourceId, out var node) && node.Kind == ResourceKind.Wood));
            int queued = WorkerQueue(match);
            yield return Say(match, ear, "entrena dos trabajadores", () => WorkerQueue(match) == queued + 2);
            yield return Capture(match, "voice-orders.png");
            Require(ShelterSpot(match, out var spot), "No valid Shelter location near the Hearth.");
            match.Voice.PointerOverride = spot;
            yield return Say(match, ear, "construye una casa", () => match.Economy.PendingBuildingId == "shelter" && match.Economy.HasPreview && match.Economy.PlacementResult.Accepted);
            yield return Capture(match, "voice-build-preview.png");
            yield return Say(match, ear, "confirma", () => match.World.Buildings.Any(building =>
                building.OwnerId == MatchController.LocalPlayer && building.DefinitionId == "shelter" && !building.IsComplete));
            yield return Say(match, ear, "ayuda", () => match.Hud.VoicePanel.HelpVisible);
            yield return Capture(match, "voice-help.png");
            yield return Say(match, ear, "cierra la ayuda", () => !match.Voice.HelpVisible);
            yield return Say(match, ear, "pausa", () => match.OfflineControls.IsPaused);
            yield return Say(match, ear, "reanuda", () => !match.OfflineControls.IsPaused);
            match.Voice.Configure(VoiceListenMode.AlwaysOn, VoiceLanguage.English);
            yield return Say(match, ear, "select workers", () => match.Economy.SelectedWorkers().Length > 0);
            yield return Say(match, ear, "camera home", () => true);
            yield return Capture(match, "voice-english.png");
            Finish(true);
        }

        private static int Compile(MatchController match, VoiceLanguage language, out bool accepted, out string problem)
        {
            match.Voice.Configure(VoiceListenMode.Off, language);
            var texts = match.Voice.Phrases.Select(phrase => phrase.Text).ToArray();
            accepted = false; problem = "";
            if (WindowsVoiceRecognizer.IsSupported)
                using (var windows = new WindowsVoiceRecognizer()) { windows.Load(texts); accepted = windows.IsAvailable; problem = windows.Problem ?? ""; }
            return texts.Length;
        }

        private static IEnumerator Say(MatchController match, ScriptedVoiceRecognizer ear, string phrase, Func<bool> effect)
        {
            Require(ear.Say(phrase), "The recognizer was not listening for «" + phrase + "».");
            yield return null; // MatchController.Update hands the phrase to the voice layer.
            report.Heard.Add(phrase + " → " + match.Voice.LastIntent + " · " + match.Feedback);
            Require(!match.Voice.LastIntent.IsNone, "Not understood: «" + phrase + "».");
            Require(effect(), "«" + phrase + "» had no effect: " + match.Feedback);
            report.CommandsCarriedOut++;
        }

        private static int WorkerQueue(MatchController match) => match.World.Buildings
            .Where(building => building.OwnerId == MatchController.LocalPlayer)
            .Sum(building => building.ProductionQueue.Count(entry => match.Economy.UnitDefinition(entry.UnitDefinitionId)?.IsWorker == true));

        private static bool ShelterSpot(MatchController match, out Vector2 screen)
        {
            screen = default;
            var hearth = match.World.Buildings.FirstOrDefault(building => building.OwnerId == MatchController.LocalPlayer && building.DefinitionId == "hearth");
            if (hearth == null) return false;
            int cell = match.World.Map.CellSizeMillimetres;
            match.Economy.BeginBuild("shelter");
            try
            {
                for (int ring = 3; ring < 16; ring++)
                    for (int step = 0; step < 16; step++)
                    {
                        double angle = step * Math.PI / 8;
                        match.Economy.PreviewAt(new SimPoint(hearth.Position.X + (int)(Math.Cos(angle) * ring * cell), hearth.Position.Z + (int)(Math.Sin(angle) * ring * cell)));
                        if (!match.Economy.HasPreview || !match.Economy.PlacementResult.Accepted) continue;
                        screen = match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(match.Economy.PreviewPosition));
                        return true;
                    }
                return false;
            }
            finally { match.Economy.CancelBuild(); }
        }

        private static IEnumerator Capture(MatchController match, string name)
        {
            match.Hud.Invalidate(); match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            // Hidden Windows players may have no readable swapchain: render the camera and canvases explicitly.
            var canvases = match.GetComponentsInChildren<Canvas>();
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode; cameras[i] = canvases[i].worldCamera; distances[i] = canvases[i].planeDistance;
                if (modes[i] != RenderMode.ScreenSpaceOverlay) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = match.Rig.Camera; canvases[i].planeDistance = 1;
            }
            try { Canvas.ForceUpdateCanvases(); Require(PlayerSmoke.Capture(match, Path.Combine(folder, name)), "Native camera/UI capture was empty: " + name); }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }

        private static void Finish(bool passed)
        {
            report.Passed = passed;
            File.WriteAllText(Path.Combine(folder, "voice-smoke.json"), JsonUtility.ToJson(report, true));
            if (!passed) Debug.LogError("EMBERFIELD_VOICE_SMOKE " + report.Failure);
            SafeQuit.Request(passed ? 0 : 1);
        }
    }
}
