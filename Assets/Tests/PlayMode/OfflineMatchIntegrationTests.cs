using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Emberfield.Tests.PlayMode
{
    public sealed class OfflineMatchIntegrationTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup() { if (root != null) Object.Destroy(root); yield return null; }
        private MatchController Create(VictoryMode mode = VictoryMode.Conquest)
        {
            root = new GameObject("Offline integration"); root.SetActive(false);
            var match = root.AddComponent<MatchController>(); match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = mode;
            root.SetActive(true); match.enabled = false; return match;
        }
        [Test]
        public void ShippedLegacyMapsDoNotOptIntoOfflineRulesWhenUnityDeserializesMissingFields()
        {
            foreach (var path in new[] { "Maps/amber_reach", "Maps/combat_sandbox", "Maps/faction_proving_ground" })
            {
                var world = DefinitionLoader.CreateWorld(path);
                Assert.IsNull(world.Match, path); Assert.IsNull(world.Vision, path);
            }
            Assert.IsNotNull(DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Conquest).Match);
        }
        [UnityTest]
        public IEnumerator ScoutingRevealsEnemyViewsAndLosingVisionHidesThemAgain()
        {
            var match = Create(); yield return null;
            Assert.IsNotNull(match.World.Match); Assert.IsNotNull(match.OfflineControls.Ai);
            Assert.IsNull(match.View.RootFor(101), "An unseen rival Hearth must have no live view.");
            Assert.IsFalse(match.World.Vision.IsEntityVisible(1, 101));
            // The scout's route is read from the map, so the case survives a rebaked battlefield.
            Assert.IsTrue(match.World.TryGetBuilding(101, out var rival));
            Assert.IsTrue(match.World.TryGetUnit(1, out var scout));
            var homeGround = scout.Position;
            var lookout = MoveNear(match.World, 1, 1, rival.Position);
            for (int tick = 0; tick < 3000 && !match.World.Vision.IsEntityVisible(1, 101); tick++) match.World.Tick();
            Assert.IsTrue(match.World.Vision.IsEntityVisible(1, 101), "A real moving scout reveals the rival base.");
            match.SyncPresentation(1);
            Assert.IsTrue(match.View.RootFor(101).gameObject.activeSelf);
            Assert.IsTrue(match.World.Submit(new MoveCommand(1, new[] { 1 }, homeGround)).Accepted);
            for (int tick = 0; tick < 3000 && match.World.Vision.IsEntityVisible(1, 101); tick++) match.World.Tick();
            match.SyncPresentation(1);
            Assert.IsFalse(match.View.RootFor(101).gameObject.activeSelf);
            Assert.IsTrue(match.World.Vision.IsExplored(1, lookout), "Ground the scout stood on stays explored.");
        }
        [UnityTest]
        public IEnumerator HiddenOutpostRoundTripUpdatesItsPositionWhenRevealedAgain()
        {
            var match = Create(); yield return null;
            Assert.IsTrue(match.World.TryGetBuilding(101, out var rival));
            Assert.IsTrue(match.World.TryGetUnit(1, out var scout));
            var homeGround = scout.Position;
            // The rival picks the first clear footprint near its own base rather than a coordinate that a
            // rebaked map might have filled with trees.
            var site = default(SimPoint); var build = default(CommandResult);
            foreach (var offset in new[] { new Vector2Int(5, -5), new Vector2Int(-5, 5), new Vector2Int(6, 2), new Vector2Int(-2, -6), new Vector2Int(7, 7), new Vector2Int(-7, -7) })
            {
                var candidate = Near(rival.Position, offset.x * 1000, offset.y * 1000);
                build = match.World.Submit(new BuildCommand(2, new[] { 14 }, "supply_outpost", candidate));
                if (build.Accepted) { site = candidate; break; }
            }
            Assert.IsTrue(build.Accepted, build.Message); match.World.TryGetBuilding(build.EntityId, out var outpost);
            for (int i = 0; i < 800 && !outpost.IsComplete; i++) match.World.Tick();
            Assert.IsTrue(outpost.IsComplete);
            Assert.IsTrue(match.World.Submit(new MoveCommand(1, new[] { 1 }, Near(site, -3500, -3500))).Accepted);
            for (int i = 0; i < 3000 && !match.World.Vision.IsEntityVisible(1, outpost.Id); i++) match.World.Tick();
            Assert.IsTrue(match.World.Vision.IsEntityVisible(1, outpost.Id)); match.SyncPresentation(1);
            var oldVisual = match.View.RootFor(outpost.Id); Assert.IsNotNull(oldVisual);
            Assert.IsTrue(match.World.Submit(new MoveCommand(1, new[] { 1 }, homeGround)).Accepted);
            for (int i = 0; i < 1500 && match.World.Vision.IsEntityVisible(1, outpost.Id); i++) match.World.Tick();
            Assert.IsFalse(match.World.Vision.IsEntityVisible(1, outpost.Id)); match.SyncPresentation(1);
            Assert.IsTrue(match.World.Submit(new PackOutpostCommand(2, outpost.Id)).Accepted);
            for (int i = 0; i < 100; i++) { match.World.Tick(); match.SyncPresentation(1); }
            Assert.IsTrue(match.World.TryGetUnit(outpost.Id, out var cart));
            Assert.IsTrue(match.World.Submit(new MoveCommand(2, new[] { cart.Id }, new SimPoint(72500, 49500))).Accepted);
            for (int i = 0; i < 800 && cart.Order != UnitOrder.Idle; i++) match.World.Tick();
            Assert.AreEqual(UnitOrder.Idle, cart.Order);
            var deploy = match.World.Submit(new DeployOutpostCommand(2, cart.Id, new SimPoint(73000, 51000)));
            Assert.IsTrue(deploy.Accepted, deploy.Message);
            for (int i = 0; i < 100; i++) { match.World.Tick(); match.SyncPresentation(1); }
            Assert.IsTrue(match.World.TryGetBuilding(outpost.Id, out var relocated));
            Assert.IsTrue(match.World.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(68500, 45500))).Accepted);
            for (int i = 0; i < 1500 && !match.World.Vision.IsEntityVisible(1, relocated.Id); i++) match.World.Tick();
            Assert.IsTrue(match.World.Vision.IsEntityVisible(1, relocated.Id)); match.SyncPresentation(1);
            Assert.AreSame(oldVisual, match.View.RootFor(relocated.Id));
            Assert.Less(Vector3.Distance(DefinitionLoader.ToWorld(relocated.Position), oldVisual.position), .001f);
        }
        [UnityTest]
        public IEnumerator OfflineMenuInterceptsOrdersAndSurrenderProducesFrozenResult()
        {
            var match = Create(); yield return null;
            match.Select(new[] { 1 }); match.World.TryGetUnit(1, out var worker);
            var before = worker.Position;
            match.TouchSelectionMode = true; match.Economy.BeginBuild("shelter");
            Assert.IsFalse(match.TouchSelectionMode, "Choosing a placement action must leave selection mode so the next terrain tap can preview it.");
            Assert.AreEqual("shelter", match.Economy.PendingBuildingId); match.Economy.CancelBuild();
            root.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            match.Hud.Refresh(); Canvas.ForceUpdateCanvases();
            Assert.IsTrue(match.OfflineControls.IsPaused); Assert.IsTrue(match.Hud.OfflinePanel.IsVisible);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(new Vector3(28.5f, 0, 24.5f)), false);
            Assert.AreEqual(before, worker.Position);
            Click(match, "Surrender..."); Assert.IsTrue(match.OfflineControls.ConfirmSurrender);
            match.Hud.Refresh(); Click(match, "Confirm surrender");
            Assert.IsTrue(match.World.Match.IsFinished); Assert.AreEqual(2, match.World.Match.WinnerId);
            long endedAt = match.World.TickIndex;
            match.World.Tick(); match.World.Tick();
            Assert.AreEqual(endedAt, match.World.TickIndex);
            Assert.IsFalse(match.World.Submit(new MoveCommand(1, new[] { 1 }, new SimPoint(28500, 24500))).Accepted);
            match.Hud.Refresh();
            Assert.IsTrue(match.Hud.OfflinePanel.Root.GetComponentInChildren<Text>().text.Contains("DEFEAT"));
        }
        [UnityTest]
        public IEnumerator DominionShowsPublicObjectiveClocksWithoutRevealingEnemyEntities()
        {
            var match = Create(VictoryMode.Dominion); yield return null;
            Assert.AreEqual(3, match.World.Match.Objectives.Count);
            Assert.IsNotNull(root.transform.Find("World presentation/Beacon crossing"));
            Assert.IsNull(match.View.RootFor(101));
            var status = root.transform.Find("Tablet HUD/Safe area/Match objective clock/Objectives").GetComponent<Text>();
            Assert.That(status.text, Does.Contain("BEACONS")); Assert.That(status.text, Does.Contain("8:00"));
            match.OfflineControls.Open(); match.OfflineControls.Close(); match.Hud.Refresh();
            Assert.IsFalse(match.OfflineControls.IsPaused); Assert.IsFalse(match.Hud.OfflinePanel.IsVisible);
        }
        [UnityTest]
        public IEnumerator RestartAfterResultCreatesFreshMatchWithSameFactionAndMode()
        {
            var match = Create(VictoryMode.Dominion); yield return null;
            for (int i = 0; i < 20; i++) match.World.Tick();
            match.World.Submit(new SurrenderCommand(1));
            match.Restart(); yield return null; yield return null;
            var restarted = Object.FindFirstObjectByType<MatchController>();
            Assert.IsNotNull(restarted); root = restarted.gameObject; restarted.enabled = false;
            Assert.AreEqual(VictoryMode.Dominion, restarted.World.Match.Mode);
            Assert.AreEqual("aven", restarted.Factions.LocalDefinition.Id);
            Assert.IsFalse(restarted.World.Match.IsFinished); Assert.Less(restarted.World.TickIndex, 20);
            Assert.AreEqual(4, CountWorkers(restarted.World, 1));
        }
        [UnityTest]
        public IEnumerator PracticeCommandRowHoldsOrdersOnlyAndItsMenuCarriesTheSessionControls()
        {
            root = new GameObject("Practice integration"); root.SetActive(false);
            var match = root.AddComponent<MatchController>(); root.SetActive(true); match.enabled = false;
            yield return null;
            var actions = root.transform.Find("Tablet HUD/Safe area/Commands/Actions");
            var labels = new System.Collections.Generic.List<string>();
            foreach (var text in actions.GetComponentsInChildren<Text>()) labels.Add(text.text);
            CollectionAssert.IsSubsetOf(new[] { "Workers", "Army", "Stop", "Clear", "Home", "Menu", "Settings" }, labels);
            foreach (string moved in new[] { "Play", "Online", "Battle", "Factions", "Restart" })
                CollectionAssert.DoesNotContain(labels, moved, "The command row carries orders only.");

            match.OfflineControls.Open(); match.Hud.Refresh(); Canvas.ForceUpdateCanvases();
            var panel = match.Hud.OfflinePanel;
            Assert.IsTrue(panel.IsVisible);
            var names = new System.Collections.Generic.List<string> { "Restart", "Battle", "Online", "Main menu" };
            if (match.Factions.Available) names.Add("Factions");
            var placed = new System.Collections.Generic.List<RectTransform>();
            foreach (string name in names)
            {
                var button = Find(panel, name);
                Assert.IsNotNull(button, name + " belongs to the match menu now.");
                Assert.IsTrue(button.gameObject.activeInHierarchy, name + " must be visible in the menu.");
                placed.Add((RectTransform)button.transform);
            }
            for (int i = 0; i < placed.Count; i++)
                for (int j = i + 1; j < placed.Count; j++)
                    Assert.IsFalse(Bounds(placed[i], panel.Root).Overlaps(Bounds(placed[j], panel.Root)),
                        "The menu shares its row out instead of stacking controls on top of each other.");
        }
        private static Button Find(OfflinePanel panel, string name)
        {
            foreach (var button in panel.Root.GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
            return null;
        }
        private static Rect Bounds(RectTransform child, RectTransform ancestor)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var min = ancestor.InverseTransformPoint(corners[0]); var max = ancestor.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static SimPoint Near(SimPoint point, int dx, int dz) => new SimPoint(point.X + dx, point.Z + dz);

        /// <summary>Walks a unit to the first clear approach beside an anchor, whatever the map baked there.</summary>
        private static SimPoint MoveNear(World world, int player, int unitId, SimPoint anchor)
        {
            foreach (var offset in new[] { new Vector2Int(-5, -5), new Vector2Int(5, -5), new Vector2Int(-5, 5), new Vector2Int(-6, -2), new Vector2Int(2, -6), new Vector2Int(-8, -8) })
            {
                var destination = Near(anchor, offset.x * 1000, offset.y * 1000);
                if (world.Submit(new MoveCommand(player, new[] { unitId }, destination)).Accepted) return destination;
            }
            Assert.Fail("No clear approach beside " + anchor.X + "," + anchor.Z);
            return default;
        }
        private static int CountWorkers(World world, int player)
        { int count = 0; foreach (var unit in world.Units) if (unit.OwnerId == player && unit.IsWorker) count++; return count; }
        private static void Click(MatchController match, string name)
        {
            foreach (var button in match.Hud.OfflinePanel.Root.GetComponentsInChildren<Button>())
                if (button.name == name) { button.onClick.Invoke(); return; }
            Assert.Fail("Visible button not found: " + name);
        }
    }
}
