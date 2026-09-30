using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Emberfield.Localization;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class LocalizationIntegrationTests
    {
        private static readonly Regex EnglishWords = new Regex(
            @"\b(the|and|your|you|with|select|tap|choose|build|train|match|settings|resources|gather|return|enemy|units|press|close|sign|server|player|faction|realm|research|workers|army)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private GameObject root;
        private MatchController match;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UiLocalization.Enabled = false; UiLocalization.SetLanguage(GameLanguage.Spanish);
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMatchHudReadsInSpanishAndSwitchesToEnglish()
        {
            CreateOfflineMatch();
            yield return null;
            Show();
            CollectionAssert.IsSubsetOf(new[] { "Aldeanos", "Ejército", "Detener", "Limpiar", "Base", "Girar vista", "Menú", "Ajustes" }, Labels());
            CollectionAssert.DoesNotContain(Labels(), "Workers");
            match.Select(new[] { match.World.Units.First(unit => unit.OwnerId == MatchController.LocalPlayer && unit.IsWorker).Id });
            Show();
            Assert.IsTrue(Labels().Any(text => text.Contains("de salud")), "Selection details are translated: " + string.Join(" | ", Labels()));

            UiLocalization.SetLanguage(GameLanguage.English);
            Show();
            CollectionAssert.IsSubsetOf(new[] { "Workers", "Army", "Stop", "Menu", "Settings" }, Labels());
            Assert.IsTrue(Labels().Any(text => text.Contains("health")), "Switching back shows the source text again.");
        }

        [UnityTest]
        public IEnumerator MenusAndSettingsReadInSpanish()
        {
            CreateOfflineMatch();
            yield return null;
            var untranslated = new List<string>();
            match.Shell.Open();
            Show();
            CollectionAssert.IsSubsetOf(new[] { "INICIO", "JUGAR", "TIENDA", "AJUSTES", "IDIOMA: ES", "REANUDAR ESCARAMUZA    ›", "MULTIJUGADOR", "APRENDER A JUGAR" }, Labels());
            foreach (string page in new[] { "home", "skirmish", "store", "settings" })
            {
                match.Shell.Navigate(page);
                Show();
                untranslated.AddRange(Labels().Where(IsEnglish));
            }
            CollectionAssert.IsSubsetOf(new[] { "Hazlo a tu manera", "GUÍA E INFORMES LOCALES" }, Labels());
            match.Shell.Close();

            match.Alpha.Open(); Show();
            CollectionAssert.Contains(Labels(), "AJUSTES Y GUÍA");
            untranslated.AddRange(Labels().Where(IsEnglish));
            match.Alpha.Close();

            match.OfflineControls.Open(); Show();
            CollectionAssert.Contains(Labels(), "PARTIDA EN PAUSA");
            untranslated.AddRange(Labels().Where(IsEnglish));
            match.OfflineControls.Close();

            match.Research.Open(); Show();
            untranslated.AddRange(Labels().Where(IsEnglish));
            match.Research.Close();

            // Coverage report for new screens and phrases: written for review rather than failing the run.
            string report = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestResults", "untranslated-text.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllLines(report, untranslated.Distinct());
            Debug.Log("Untranslated text lines: " + untranslated.Distinct().Count() + " (" + report + ")");
        }

        private static bool IsEnglish(string text) => EnglishWords.IsMatch(text);

        private void CreateOfflineMatch()
        {
            root = new GameObject("Localization integration");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Conquest;
            match.enabled = false;
            root.SetActive(true);
            match.Shell?.Close(); match.OfflineControls.Close();
            // The saved setting applies when the match starts; these cases choose their own language.
            UiLocalization.Enabled = true; UiLocalization.SetLanguage(GameLanguage.Spanish);
        }

        private void Show()
        {
            match.Hud.Invalidate();
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
            UiLocalization.ApplyNow();
        }

        private List<string> Labels() => root.GetComponentsInChildren<Text>().Select(text => text.text).ToList();
    }
}
