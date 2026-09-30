using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Emberfield.Tests.PlayMode
{
    public sealed class SiegeInputTests
    {
        private GameObject root;
        private MatchController match;
        private RtsInput adapter;
        private Touchscreen screen;
        private Mouse mouse, previousMouse;
        private InputSettings.UpdateMode previousUpdate;
        private InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode previousEditor;
#endif

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousUpdate = InputSystem.settings.updateMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            previousMouse = Mouse.current;
            mouse = InputSystem.AddDevice<Mouse>();
            screen = InputSystem.AddDevice<Touchscreen>();
            root = new GameObject("Siege input regression"); root.SetActive(false);
            match = root.AddComponent<MatchController>(); match.InitialFactionId = "serevin"; match.enabled = false;
            root.SetActive(true);
            adapter = new RtsInput(match);
            yield return null;
            Canvas.ForceUpdateCanvases();
            InputSystem.QueueStateEvent(mouse, new MouseState()); mouse.MakeCurrent(); Step();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            adapter?.Dispose();
            if (root != null) Object.Destroy(root);
            if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            InputSystem.settings.updateMode = previousUpdate;
            InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
#endif
            yield return null;
        }

        [Test]
        public void SelectOnDoesNotDivertWallTargetTouchesIntoASelectionChange()
        {
            match.Select(new[] { 2 }); match.TouchSelectionMode = true;
            match.ChooseWall();
            var first = match.View.RootFor(1);
            Assert.IsNotNull(first);
            Vector2 target = match.Rig.Camera.WorldToScreenPoint(first.position + Vector3.up * .6f);
            Tap(1, target);
            Assert.IsFalse(match.TouchSelectionMode);
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection, "A target tap must not select the worker under the finger.");
            Assert.IsTrue(match.ChoosingWall);
            StringAssert.Contains("Tap a completed wall segment", match.Feedback, "The real touch adapter must reach siege target validation.");

            match.TouchSelectionMode = true;
            match.ChooseDescent();
            Tap(2, target);
            Assert.IsFalse(match.TouchSelectionMode);
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection);
            Assert.IsTrue(match.ChoosingDescent);
            StringAssert.Contains("Select between one and 32", match.Feedback,
                "The tap must submit LeaveWallCommand and receive its ordinary selection rejection; workers are intentionally ineligible.");
        }

        [Test]
        public void WallTargetingCancelsBuildAndRallyAndNewSelectionCancelsSiegeTargeting()
        {
            match.Select(new[] { 1 }); match.Economy.BeginBuild("shelter");
            Assert.AreEqual("shelter", match.Economy.PendingBuildingId);
            match.TouchSelectionMode = true; match.ChooseWall();
            Assert.IsNull(match.Economy.PendingBuildingId); Assert.IsFalse(match.Economy.IsChoosingRally);
            Assert.IsTrue(match.ChoosingWall); Assert.IsFalse(match.ChoosingDescent); Assert.IsFalse(match.TouchSelectionMode);
            match.Select(new[] { 100 });
            Assert.IsFalse(match.ChoosingWall); Assert.IsFalse(match.ChoosingDescent);
            match.Economy.SelectRally(); Assert.IsTrue(match.Economy.IsChoosingRally);
            match.TouchSelectionMode = true; match.ChooseDescent();
            Assert.IsFalse(match.Economy.IsChoosingRally); Assert.IsNull(match.Economy.PendingBuildingId);
            Assert.IsTrue(match.ChoosingDescent); Assert.IsFalse(match.ChoosingWall); Assert.IsFalse(match.TouchSelectionMode);
            match.Select(new[] { 2 });
            Assert.IsFalse(match.ChoosingWall); Assert.IsFalse(match.ChoosingDescent);
        }

        [Test]
        public void ChoosingEitherWallModeCancelsAnActualOutpostDeploymentPreview()
        {
            var result = match.World.Submit(new BuildCommand(1, new[] { 1 }, "supply_outpost", new SimPoint(14000, 24000)));
            Assert.IsTrue(result.Accepted, result.Message);
            int id = result.EntityId;
            for (int tick = 0; tick < 600; tick++) match.World.Tick();
            Assert.IsTrue(match.World.TryGetBuilding(id, out var building)); Assert.IsTrue(building.IsComplete);
            result = match.World.Submit(new PackOutpostCommand(1, id)); Assert.IsTrue(result.Accepted, result.Message);
            for (int tick = 0; tick < 100; tick++) match.World.Tick();
            Assert.IsTrue(match.World.TryGetUnit(id, out var cart)); Assert.IsTrue(cart.IsPackedOutpost);
            match.Select(new[] { id }); match.Factions.BeginDeploy();
            Assert.AreEqual(id, match.Factions.PendingDeployUnitId);
            match.ChooseWall(); Assert.AreEqual(0, match.Factions.PendingDeployUnitId); Assert.IsFalse(match.Factions.HasPreview);
            match.Select(new[] { id }); match.Factions.BeginDeploy();
            Assert.AreEqual(id, match.Factions.PendingDeployUnitId);
            match.ChooseDescent(); Assert.AreEqual(0, match.Factions.PendingDeployUnitId); Assert.IsFalse(match.Factions.HasPreview);
            Assert.IsTrue(cart.IsPackedOutpost, "Canceling a preview cannot spend resources or deploy the cart.");
        }

        private void Tap(int id, Vector2 point)
        {
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = TouchPhase.Began, position = point }); Step();
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = TouchPhase.Ended, position = point }); Step();
            Step();
        }
        private void Step() { InputSystem.Update(); adapter.Update(); }
    }
}
