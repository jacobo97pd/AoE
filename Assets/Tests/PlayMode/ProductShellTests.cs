using System.Collections;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Emberfield.Tests.PlayMode
{
    public sealed class ProductShellTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup() { if (root != null) Object.Destroy(root); yield return null; }
        private MatchController CreateMatch()
        {
            root = new GameObject("Product shell acceptance"); root.SetActive(false);
            var match = root.AddComponent<MatchController>(); match.InitialOfflineFactionId = "aven";
            root.SetActive(true); match.OfflineControls.Close(); return match;
        }
        [UnityTest]
        public IEnumerator MainMenuPausesLocalWorldAndReturningResumesSameMatch()
        {
            var match = CreateMatch(); yield return null;
            match.Shell.Open(); var world = match.World; long tick = world.TickIndex;
            match.Tap(new Vector2(Screen.width * .5f, Screen.height * .5f), false);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.AreEqual(tick, world.TickIndex); Assert.IsEmpty(match.Selection);
            var button = Find(match, "Play skirmish"); Assert.That(button.GetComponentInChildren<Text>().text, Does.Contain("RESUME"));
            button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.18f);
            Assert.IsFalse(match.Shell.IsOpen); Assert.AreSame(world, match.World); Assert.Greater(world.TickIndex, tick);
        }
        [UnityTest]
        public IEnumerator StoreSettingsAndBackPreserveMatchAndResources()
        {
            var match = CreateMatch(); yield return null; match.Shell.Open();
            var world = match.World; long tick = world.TickIndex; world.TryGetPlayer(1, out var player);
            var stock = player.Resources;
            Find(match, "Navigation Store").onClick.Invoke(); yield return null;
            Assert.AreEqual("store", match.Shell.Page); Assert.IsTrue(match.Shell.HasCatalog); Assert.IsFalse(match.Shell.PurchasesAvailable);
            Find(match, "Navigation Settings").onClick.Invoke(); yield return null;
            Assert.AreEqual("settings", match.Shell.Page);
            Find(match, "Navigation Home").onClick.Invoke(); yield return null;
            Assert.AreEqual("home", match.Shell.Page); Assert.AreSame(world, match.World);
            Assert.AreEqual(tick, world.TickIndex); Assert.AreEqual(stock.Food, player.Resources.Food); Assert.AreEqual(stock.Wood, player.Resources.Wood);
            Assert.IsNotNull(Resources.Load<Texture2D>("Interface/EmberfieldKeyArt"));
            Assert.IsNotNull(Resources.Load<Font>("Fonts/Marcellus-Regular"));
        }
        [UnityTest]
        public IEnumerator MultiplayerPanelOpenedFromFrontendDoesNotAdvanceOfflineMatch()
        {
            var match = CreateMatch(); yield return null; match.Shell.Open();
            long tick = match.World.TickIndex;
            Find(match, "Play online").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.18f);
            Assert.IsTrue(match.Online.IsOpen); Assert.IsFalse(match.Shell.IsOpen);
            Assert.IsTrue(match.Shell.BlocksLocalSimulation); Assert.AreEqual(tick, match.World.TickIndex);
            match.Online.Close(); yield return null;
            Assert.IsTrue(match.Shell.IsOpen); Assert.AreEqual(tick, match.World.TickIndex);
        }
        private static Button Find(MatchController match, string name)
        {
            foreach (var button in match.GetComponentsInChildren<Button>()) if (button.name == name && button.gameObject.activeInHierarchy) return button;
            Assert.Fail("Missing product control: " + name); return null;
        }
    }
}
