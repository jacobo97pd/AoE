using System;
using System.Collections;
using System.Reflection;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public sealed class ThreeRealmSelectorTests
    {
        private GameObject actor;
        [UnityTearDown] public IEnumerator Cleanup() { if (actor != null) Object.Destroy(actor); yield return null; }
        private MatchController CreateMatch()
        {
            actor = new GameObject("Three realm selector acceptance"); actor.SetActive(false);
            var match = actor.AddComponent<MatchController>(); match.InitialOfflineFactionId = "aven";
            actor.SetActive(true); match.Shell.Open(); return match;
        }
        private static Button Find(Transform parent, string name)
        {
            foreach (var button in parent.GetComponentsInChildren<Button>()) if (button.name == name) return button;
            Assert.Fail("Missing visible control: " + name); return null;
        }
        private static string Selection(ProductShell shell, string field) =>
            (string)typeof(ProductShell).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell);

        [UnityTest]
        public IEnumerator FrontendOffersThreeRealmsAndKeepsPlannedFleetsLocked()
        {
            var match = CreateMatch(); yield return null; match.Shell.Navigate("skirmish");
            string before = JsonUtility.ToJson(match.World.Definition); long tick = match.World.TickIndex;
            foreach (string realm in ContentRealms.All)
            {
                Find(match.Shell.Root, "Choose realm " + realm).onClick.Invoke();
                foreach (string id in ContentRealms.FactionsForRealm(realm))
                {
                    var choose = Find(match.Shell.Root, "Select " + id); Assert.IsTrue(choose.interactable);
                    choose.onClick.Invoke(); Assert.AreEqual(id, Selection(match.Shell, "faction"));
                    Assert.AreEqual(realm, Selection(match.Shell, "realm"));
                }
                yield return null;
            }
            Assert.AreEqual("pirates", Selection(match.Shell, "faction"));
            Assert.AreEqual("sapphire_coast", Selection(match.Shell, "mapId"));
            Assert.IsFalse(Find(match.Shell.Root, "Choose map amber_crossing").interactable);
            Assert.IsFalse(Find(match.Shell.Root, "Choose map sunscar_basin").interactable);
            foreach (string id in FrontierCodex.PlannedNavalFactions)
            {
                var locked = Find(match.Shell.Root, "Select " + id); Assert.IsFalse(locked.interactable);
                locked.onClick.Invoke(); Assert.AreEqual("pirates", Selection(match.Shell, "faction"));
                StringAssert.Contains("faltan", FrontierCodex.Description(id));
            }
            Assert.AreEqual(tick, match.World.TickIndex); Assert.AreEqual(before, JsonUtility.ToJson(match.World.Definition));
        }

        [UnityTest]
        public IEnumerator DrillSelectorPaginatesFiveHistoricalFactionsAndFourNavalCards()
        {
            var match = CreateMatch(); yield return null; match.Shell.Close(); match.Factions.Open(); match.Hud.Refresh();
            var panel = match.Hud.FactionPanel.Root;
            Find(panel, "Faction realm historical").onClick.Invoke();
            Find(panel, "Next faction pair ›").onClick.Invoke();
            Assert.IsTrue(Find(panel, "Choose english").interactable); Assert.IsTrue(Find(panel, "Choose sultanate").interactable);
            Find(panel, "Next faction pair ›").onClick.Invoke();
            Assert.IsTrue(Find(panel, "Choose sahel").interactable);
            Find(panel, "Next faction pair ›").onClick.Invoke();
            Assert.IsTrue(Find(panel, "Choose aven").interactable); Assert.IsTrue(Find(panel, "Choose serevin").interactable);
            Find(panel, "Faction realm naval").onClick.Invoke();
            Assert.IsTrue(Find(panel, "Choose pirates").interactable); Assert.IsFalse(Find(panel, "Choose english_navy").interactable);
            Find(panel, "Next faction pair ›").onClick.Invoke();
            Assert.IsFalse(Find(panel, "Choose spanish_navy").interactable); Assert.IsFalse(Find(panel, "Choose skeleton_fleet").interactable);
            Find(panel, "Next faction pair ›").onClick.Invoke(); Assert.IsTrue(Find(panel, "Choose pirates").interactable);
        }

        [UnityTest]
        public IEnumerator SecondaryOfflineSelectorUsesCoastalPirateChoicesAndRejectsUnavailableIds()
        {
            var match = CreateMatch(); yield return null;
            var controls = match.OfflineControls;
            controls.ChooseRealm(); Assert.AreEqual("fantasy", controls.ChosenRealm);
            controls.ChooseRealm(); Assert.AreEqual("naval", controls.ChosenRealm);
            Assert.AreEqual("pirates", controls.ChosenFaction); Assert.AreEqual("sapphire_coast", controls.ChosenMap);
            for (int i = 0; i < 4; i++) { controls.NextFaction(); controls.ChooseMap(); }
            Assert.AreEqual("pirates", controls.ChosenFaction); Assert.AreEqual("sapphire_coast", controls.ChosenMap);
            foreach (string id in new[] { "miraj", "solar", "english_navy", "spanish_navy", "skeleton_fleet" })
            { controls.ChooseFaction(id); Assert.AreEqual("pirates", controls.ChosenFaction); }
            controls.ChooseRealm(); Assert.AreEqual("historical", controls.ChosenRealm);
            Assert.AreEqual("aven", controls.ChosenFaction);
        }

        [TestCase("aven", "Franceses")]
        [TestCase("serevin", "Hispanos")]
        [TestCase("english", "Ingleses")]
        [TestCase("sultanate", "Sultanato")]
        [TestCase("sahel", "Confederación del Sahel")]
        [TestCase("ashen", "Orcos")]
        [TestCase("drakeforged", "Enanos")]
        [TestCase("skeld", "Hombres de las montañas")]
        [TestCase("verdant", "Elfos")]
        [TestCase("pirates", "Piratas")]
        public void VisibleNamesAgreeWithTheAuthoritativeFactionDefinitions(string id, string expected)
        {
            var definition = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var faction = Array.Find(definition.Factions, item => item.Id == id);
            Assert.IsNotNull(faction); Assert.AreEqual(expected, faction.DisplayName); Assert.AreEqual(expected, FrontierCodex.Name(id));
        }

        [UnityTest]
        public IEnumerator TheDesertFactionsSuggestSunscarBasinUntilThePlayerPicksAnotherBattlefield()
        {
            var match = CreateMatch(); yield return null; match.Shell.Navigate("skirmish");
            Find(match.Shell.Root, "Choose realm historical").onClick.Invoke();
            Assert.AreEqual("amber_crossing", Selection(match.Shell, "mapId"));
            Find(match.Shell.Root, "Select sultanate").onClick.Invoke();
            Assert.AreEqual("sultanate", Selection(match.Shell, "faction")); Assert.AreEqual("sunscar_basin", Selection(match.Shell, "mapId"));
            Find(match.Shell.Root, "Select sahel").onClick.Invoke(); Assert.AreEqual("sunscar_basin", Selection(match.Shell, "mapId"));
            Find(match.Shell.Root, "Select english").onClick.Invoke(); Assert.AreEqual("amber_crossing", Selection(match.Shell, "mapId"));
            Find(match.Shell.Root, "Choose map sapphire_coast").onClick.Invoke();
            Find(match.Shell.Root, "Select sahel").onClick.Invoke();
            Assert.AreEqual("sahel", Selection(match.Shell, "faction")); Assert.AreEqual("sapphire_coast", Selection(match.Shell, "mapId"), "A picked battlefield stays.");

            var controls = match.OfflineControls;
            controls.ChooseFaction("english"); Assert.AreEqual("amber_crossing", controls.ChosenMap);
            controls.NextFaction(); Assert.AreEqual("sultanate", controls.ChosenFaction); Assert.AreEqual("sunscar_basin", controls.ChosenMap);
            controls.NextFaction(); Assert.AreEqual("sahel", controls.ChosenFaction); Assert.AreEqual("sunscar_basin", controls.ChosenMap);
            controls.NextFaction(); Assert.AreEqual("aven", controls.ChosenFaction); Assert.AreEqual("amber_crossing", controls.ChosenMap);
            controls.ChooseMap(); string picked = controls.ChosenMap; Assert.AreNotEqual("amber_crossing", picked);
            controls.ChooseFaction("sultanate"); Assert.AreEqual(picked, controls.ChosenMap, "A picked battlefield stays.");
        }

        [Test]
        public void DirectStartMethodsRejectUnavailableFactionsBeforeChangingSceneOrPendingChoices()
        {
            actor = new GameObject("Rejected direct faction starts"); actor.SetActive(false);
            var match = actor.AddComponent<MatchController>();
            typeof(MatchController).GetField("<World>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(match, DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest));
            string[] names = { "nextFactionId", "nextOfflineFaction", "nextOfflineMap", "nextOfflineMode", "nextOfflineDifficulty" };
            var fields = Array.ConvertAll(names, name => typeof(MatchController).GetField(name, BindingFlags.Static | BindingFlags.NonPublic));
            var before = Array.ConvertAll(fields, field => field.GetValue(null));
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            foreach (string id in new[] { "miraj", "solar", "english_navy", "spanish_navy", "skeleton_fleet", "unknown", null })
            {
                Assert.Throws<ArgumentException>(() => match.StartOfflineMatch(id, VictoryMode.Conquest), id);
                Assert.Throws<ArgumentException>(() => match.StartFactionDrill(id), id);
                for (int i = 0; i < fields.Length; i++) Assert.AreEqual(before[i], fields[i].GetValue(null), names[i]);
                Assert.AreEqual(scene, UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle);
            }
        }
    }
}
