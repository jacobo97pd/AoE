using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Emberfield.Tests.PlayMode
{
    // Exercise the actual device adapter: direct MatchController.Tap calls cannot
    // detect an input gesture incorrectly becoming a world order.
    public class InputAdapterTests
    {
        private GameObject root;
        private MatchController match;
        private RtsInput adapter;
        private Mouse mouse, previousMouse;
        private Touchscreen touchscreen;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
#endif

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
#if UNITY_EDITOR
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            // A hidden batch editor has no focused Game View. Without these fixture-only
            // settings, public Update() chooses Editor state rather than player frame edges.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            previousMouse = Mouse.current;
            mouse = InputSystem.AddDevice<Mouse>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            root = new GameObject("Input adapter test match");
            match = root.AddComponent<MatchController>();
            match.enabled = false; // Drive input and simulation explicitly, once per test step.
            adapter = new RtsInput(match);
            yield return null;
            Canvas.ForceUpdateCanvases();
            InputSystem.QueueStateEvent(mouse, new MouseState());
            mouse.MakeCurrent();
            Step();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            adapter?.Dispose();
            if (root != null) Object.Destroy(root);
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseClickSelectsAndUiPreventsWorldOrder()
        {
            var worker = match.World.Units[0];
            MouseClick(WorkerPoint());
            CollectionAssert.Contains(match.Selection, worker.Id);

            Vector2 destination = match.Rig.Camera.WorldToScreenPoint(new Vector3(11.5f, 0, 19.5f));
            var overlay = new GameObject("Blocking test panel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            overlay.transform.SetParent(root.transform, false);
            overlay.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(overlay.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            yield return null; // Register the new UI graphic with the rendered canvas.
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = destination }, hits);
            Assert.IsTrue(hits.Exists(hit => hit.gameObject == panel), "The blocking panel must participate in real UI raycasts.");

            MouseClick(destination);
            match.World.Tick();
            Assert.AreEqual(UnitOrder.Idle, worker.Order, "A UI-origin click must not reach world movement.");

            overlay.SetActive(false);
            MouseClick(destination);
            match.World.Tick();
            Assert.AreEqual(UnitOrder.Moving, worker.Order, "The same destination must accept an unobstructed click.");
        }

        [Test]
        public void TouchTapSelectsButDraggingPansWithoutMovingUnit()
        {
            var worker = match.World.Units[0];
            var point = WorkerPoint();
            Touch(1, TouchPhase.Began, point); Step();
            Touch(1, TouchPhase.Ended, point); Step();
            Step(); // Retire EnhancedTouch's ended contact before a fresh gesture.
            CollectionAssert.Contains(match.Selection, worker.Id);

            var cameraBefore = match.Rig.Camera.transform.position;
            var dragStart = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var dragEnd = dragStart + new Vector2(60, 0);
            Touch(2, TouchPhase.Began, dragStart); Step();
            Touch(2, TouchPhase.Moved, dragEnd); Step();
            Touch(2, TouchPhase.Ended, dragEnd); Step();
            match.World.Tick();
            Assert.That(Vector3.Distance(cameraBefore, match.Rig.Camera.transform.position), Is.GreaterThan(.1f));
            Assert.AreEqual(UnitOrder.Idle, worker.Order, "A completed pan must not become a move tap.");
        }

        [Test]
        public void PinchCancelsTapAndRemainingFingerCannotIssueOrder()
        {
            var worker = match.World.Units[0];
            match.Select(new[] { worker.Id });
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var left = center - new Vector2(45, 0);
            var right = center + new Vector2(45, 0);
            float zoomBefore = match.Rig.Camera.orthographicSize;
            Touch(1, TouchPhase.Began, left); Step();
            Touch(2, TouchPhase.Began, right); Step();
            Touch(1, TouchPhase.Moved, left - new Vector2(30, 0));
            Touch(2, TouchPhase.Moved, right + new Vector2(30, 0)); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.LessThan(zoomBefore));

            Touch(2, TouchPhase.Ended, right + new Vector2(30, 0)); Step();
            Step();
            Touch(1, TouchPhase.Moved, center); Step();
            Touch(1, TouchPhase.Ended, center); Step();
            Step();
            match.World.Tick();
            CollectionAssert.AreEqual(new[] { worker.Id }, match.Selection);
            Assert.AreEqual(UnitOrder.Idle, worker.Order, "A surviving pinch finger must lift without issuing an order.");
        }

        [Test]
        public void CancelWithNoContactsDoesNotBlockNextFreshPinch()
        {
            adapter.Cancel();
            Step(); // Application-focus cancellation can occur while no fingers are held.
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            float zoomBefore = match.Rig.Camera.orthographicSize;
            Touch(1, TouchPhase.Began, center - new Vector2(40, 0));
            Touch(2, TouchPhase.Began, center + new Vector2(40, 0)); Step();
            Touch(1, TouchPhase.Moved, center - new Vector2(70, 0));
            Touch(2, TouchPhase.Moved, center + new Vector2(70, 0)); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.LessThan(zoomBefore), "A new pinch after all contacts are up must remain usable.");
        }

        [Test]
        public void ThirdContactBlocksPinchUntilEveryFingerLifts()
        {
            var worker = match.World.Units[0];
            match.Select(new[] { worker.Id });
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var left = center - new Vector2(45, 0);
            var right = center + new Vector2(45, 0);
            Touch(1, TouchPhase.Began, left);
            Touch(2, TouchPhase.Began, right); Step();
            Touch(3, TouchPhase.Began, center + new Vector2(0, 80)); Step();
            float zoomBefore = match.Rig.Camera.orthographicSize;

            // Retiring an original pinch finger changes the first pair in activeTouches.
            Touch(1, TouchPhase.Ended, left); Step(); Step();
            Touch(2, TouchPhase.Moved, right + new Vector2(45, 0));
            Touch(3, TouchPhase.Moved, center - new Vector2(45, 0)); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.EqualTo(zoomBefore).Within(.001f));
            Touch(2, TouchPhase.Ended, right + new Vector2(45, 0));
            Touch(3, TouchPhase.Ended, center - new Vector2(45, 0)); Step(); Step();
            match.World.Tick();
            Assert.AreEqual(UnitOrder.Idle, worker.Order);
            CollectionAssert.AreEqual(new[] { worker.Id }, match.Selection);

            Touch(4, TouchPhase.Began, left);
            Touch(5, TouchPhase.Began, right); Step();
            Touch(4, TouchPhase.Moved, left - new Vector2(30, 0));
            Touch(5, TouchPhase.Moved, right + new Vector2(30, 0)); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.LessThan(zoomBefore), "All lifted contacts must allow a new pinch.");
        }

        private Vector2 WorkerPoint()
        {
            var worker = match.World.Units[0];
            return match.Rig.Camera.WorldToScreenPoint(match.View.RootFor(worker.Id).position + Vector3.up * .6f);
        }

        private void MouseClick(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.AreSame(mouse, Mouse.current, "The queued test mouse must own the adapter input.");
            Assert.IsTrue(mouse.leftButton.wasPressedThisFrame, $"The queued press must have a fresh frame edge. value={mouse.leftButton.ReadValue()} pressed={mouse.leftButton.isPressed} updated={mouse.wasUpdatedThisFrame} update={InputState.currentUpdateType}");
            adapter.Update();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            Assert.AreSame(mouse, Mouse.current, "The release must still belong to the test mouse.");
            Assert.IsTrue(mouse.leftButton.wasReleasedThisFrame, "The queued release must have a fresh frame edge.");
            adapter.Update();
        }

        private void Touch(int id, TouchPhase phase, Vector2 point)
            => InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = point });

        private void Step()
        {
            InputSystem.Update();
            adapter.Update();
        }
    }
}
