using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Emberfield.Tests.PlayMode
{
    // Queued Touchscreen states exercise EnhancedTouch and the actual controller-owned adapter.
    // Focus callbacks are simulated explicitly; these are not physical-device usability measurements.
    public class MobileInputTests
    {
        private GameObject root;
        private MatchController match;
        private RtsInput adapter;
        private Touchscreen touchscreen;
        private Mouse mouse, previousMouse;
        private Keyboard keyboard;
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
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            previousMouse = Mouse.current;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            root = new GameObject("Mobile input integration");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.enabled = false;
            root.SetActive(true);
            // Use the real private adapter so SendMessage exercises controller focus-cancellation wiring.
            var field = typeof(MatchController).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            adapter = field.GetValue(match) as RtsInput;
            Assert.IsNotNull(adapter);
            yield return null;
            match.Hud.Invalidate(); match.SyncPresentation(1);
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
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
#endif
            yield return null;
        }

        [Test]
        public void ScreenScaledJitterRemainsATapAndAFreshContextTapIssuesMovement()
        {
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            var point = EntityPoint(worker.Id);
            var camera = match.Rig.Camera.transform.position;
            var jitter = new Vector2(6 * Screen.height / 720f, 0);
            Touch(1, TouchPhase.Began, point); Step();
            Touch(1, TouchPhase.Moved, point + jitter); Step();
            Touch(1, TouchPhase.Ended, point + jitter); Step(); Step();
            CollectionAssert.AreEqual(new[] { worker.Id }, match.Selection);
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            Assert.AreEqual(UnitOrder.Idle, worker.Order);

            var destination = new SimPoint(11500, 19500);
            Tap(2, match.Rig.Camera.WorldToScreenPoint(DefinitionLoader.ToWorld(destination)));
            Assert.AreEqual(UnitOrder.Moving, worker.Order);
            Assert.AreEqual(destination, worker.Destination);
            match.World.Tick();
            Assert.AreNotEqual(worker.PreviousPosition, worker.Position);
        }

        [Test]
        public void SelectModeAllowsTapsAndImmediateBoxSelectionWithoutPanningOrWorldOrders()
        {
            match.TouchSelectionMode = true;
            Tap(1, EntityPoint(1));
            CollectionAssert.AreEqual(new[] { 1 }, match.Selection, "A stationary Select-mode contact must still select an entity.");
            Tap(2, match.Rig.Camera.WorldToScreenPoint(new Vector3(11.5f, 0, 19.5f)));
            Assert.IsEmpty(match.Selection, "Select-mode empty-ground taps must not issue movement.");
            var camera = match.Rig.Camera.transform.position;
            var rectangle = WorkerRectangle();
            Touch(3, TouchPhase.Began, rectangle.min); Step();
            Touch(3, TouchPhase.Moved, rectangle.max); Step();
            Assert.IsTrue(SelectionBox().gameObject.activeSelf);
            Touch(3, TouchPhase.Ended, rectangle.max); Step(); Step();
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, match.Selection);
            Assert.IsFalse(SelectionBox().gameObject.activeSelf);
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            foreach (var worker in match.World.Units)
                if (worker.OwnerId == 1) Assert.AreEqual(UnitOrder.Idle, worker.Order);
        }

        [Test]
        public void TwoFingerTranslationAndPinchAnchorTheGroundAndConsumeLiftedOrReplacementContacts()
        {
            match.Select(new[] { 1 });
            var centre = new Vector2(Screen.width * .55f, Screen.height * .6f);
            float scale = Screen.height / 720f;
            var left = centre - new Vector2(60 * scale, 0);
            var right = centre + new Vector2(60 * scale, 0);
            Assert.IsTrue(match.Rig.GroundPoint(centre, out var anchor));
            float zoom = match.Rig.Camera.orthographicSize;
            var camera = match.Rig.Camera.transform.position;
            Touch(1, TouchPhase.Began, left);
            Touch(2, TouchPhase.Began, right); Step();
            var translation = new Vector2(40, 25) * scale;
            var movedLeft = left + translation - new Vector2(20 * scale, 0);
            var movedRight = right + translation + new Vector2(20 * scale, 0);
            Touch(1, TouchPhase.Moved, movedLeft);
            Touch(2, TouchPhase.Moved, movedRight); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.LessThan(zoom));
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.GreaterThan(.1f));
            Assert.IsTrue(match.Rig.GroundPoint(centre + translation, out var retained));
            Assert.That(Vector3.Distance(anchor, retained), Is.LessThan(.02f), "The pinch centre must retain its ground anchor while translating.");

            Touch(2, TouchPhase.Ended, movedRight); Step(); Step();
            float heldZoom = match.Rig.Camera.orthographicSize;
            var heldCamera = match.Rig.Camera.transform.position;
            Touch(3, TouchPhase.Began, right); Step();
            Touch(1, TouchPhase.Moved, left - new Vector2(70 * scale, 0));
            Touch(3, TouchPhase.Moved, right + new Vector2(70 * scale, 0)); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.EqualTo(heldZoom).Within(.001f));
            Assert.That(Vector3.Distance(heldCamera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            Touch(1, TouchPhase.Ended, left); Touch(3, TouchPhase.Ended, right); Step(); Step();
            CollectionAssert.AreEqual(new[] { 1 }, match.Selection);
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            Assert.AreEqual(UnitOrder.Idle, worker.Order);

            Touch(4, TouchPhase.Began, left); Touch(5, TouchPhase.Began, right); Step();
            Touch(4, TouchPhase.Moved, centre - new Vector2(45 * scale, 0));
            Touch(5, TouchPhase.Moved, centre + new Vector2(45 * scale, 0)); Step();
            Assert.That(match.Rig.Camera.orthographicSize, Is.GreaterThan(heldZoom), "All lifted contacts must permit a fresh zoom gesture.");
        }

        [Test]
        public void UiOriginCannotBecomeAPanPinchOrOrderAfterEnteringTheWorld()
        {
            match.Select(new[] { 1 });
            match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases();
            var resourceCard = root.transform.Find("Tablet HUD/Safe area/Top bar/Resource ledger/FOOD") as RectTransform;
            Assert.IsNotNull(resourceCard, "Use the rendered resource card as the UI-origin fixture.");
            var uiPoint = RectTransformUtility.WorldToScreenPoint(null, resourceCard.TransformPoint(resourceCard.rect.center));
            Assert.IsTrue(HitsUi(uiPoint));
            var centre = new Vector2(Screen.width * .5f, Screen.height * .6f);
            Assert.IsFalse(HitsUi(centre), "The held contact must travel from real UI into the unobstructed playfield.");
            var camera = match.Rig.Camera.transform.position;
            float zoom = match.Rig.Camera.orthographicSize;
            Touch(1, TouchPhase.Began, uiPoint); Step();
            Touch(1, TouchPhase.Moved, centre - new Vector2(60, 0)); Step();
            Touch(2, TouchPhase.Began, centre + new Vector2(60, 0)); Step();
            Touch(1, TouchPhase.Moved, centre - new Vector2(110, 0));
            Touch(2, TouchPhase.Moved, centre + new Vector2(110, 0)); Step();
            Touch(1, TouchPhase.Ended, centre); Step(); Step();
            Touch(2, TouchPhase.Moved, centre); Step();
            Touch(2, TouchPhase.Ended, centre); Step(); Step();
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            Assert.That(match.Rig.Camera.orthographicSize, Is.EqualTo(zoom).Within(.001f));
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            Assert.AreEqual(UnitOrder.Idle, worker.Order);
            Tap(3, EntityPoint(2));
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection);
        }

        [Test]
        public void CanceledTouchHidesAnUncommittedSelectionBoxAndFreshTouchRecovers()
        {
            match.TouchSelectionMode = true;
            match.Select(new[] { 1 });
            var rectangle = WorkerRectangle();
            Touch(1, TouchPhase.Began, rectangle.min); Step();
            Touch(1, TouchPhase.Moved, rectangle.max); Step();
            Assert.IsTrue(SelectionBox().gameObject.activeSelf);
            Touch(1, TouchPhase.Canceled, rectangle.max); Step(); Step();
            Assert.IsFalse(SelectionBox().gameObject.activeSelf);
            CollectionAssert.AreEqual(new[] { 1 }, match.Selection, "Cancellation must not commit the box selection.");
            Tap(2, EntityPoint(2));
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection);
        }

        [TestCase("OnApplicationFocus", false)]
        [TestCase("OnApplicationPause", true)]
        public void ControllerLifecycleCallbackConsumesHeldContactsAndAllowsAFreshGesture(string callback, bool suspendedValue)
        {
            match.Select(new[] { 1 });
            var point = new Vector2(Screen.width * .5f, Screen.height * .6f);
            var camera = match.Rig.Camera.transform.position;
            Touch(1, TouchPhase.Began, point); Step();
            root.SendMessage(callback, suspendedValue, SendMessageOptions.RequireReceiver);
            root.SendMessage(callback, !suspendedValue, SendMessageOptions.RequireReceiver);
            Touch(1, TouchPhase.Moved, point + new Vector2(60, 0)); Step();
            Touch(1, TouchPhase.Ended, point + new Vector2(60, 0)); Step(); Step();
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            Assert.AreEqual(UnitOrder.Idle, worker.Order);
            Tap(2, EntityPoint(2));
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection);
        }

        [Test]
        public void ModalInterruptionCannotResumeAHeldWorldGestureAfterThePanelCloses()
        {
            match.Select(new[] { 1 });
            var point = new Vector2(Screen.width * .5f, Screen.height * .6f);
            var camera = match.Rig.Camera.transform.position;
            Touch(1, TouchPhase.Began, point); Step();
            match.Research.Open();
            Step();
            match.Research.Close();
            match.Hud.Invalidate(); match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
            Touch(1, TouchPhase.Moved, point + new Vector2(60, 0)); Step();
            Touch(1, TouchPhase.Ended, point + new Vector2(60, 0)); Step(); Step();
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            Assert.AreEqual(UnitOrder.Idle, worker.Order);
            CollectionAssert.AreEqual(new[] { 1 }, match.Selection);
            Tap(2, EntityPoint(2));
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection);
        }

        [Test]
        public void OfflineMenuBlocksHeldInputAndClosingItRequiresAFreshContact()
        {
            match.Select(new[] { 1 });
            var point = new Vector2(Screen.width * .5f, Screen.height * .6f);
            var camera = match.Rig.Camera.transform.position;
            Touch(1, TouchPhase.Began, point); Step();
            match.OfflineControls.Open();
            Assert.IsTrue(match.OfflineControls.BlocksWorldInput);
            Step();
            Touch(1, TouchPhase.Moved, point + new Vector2(60, 0)); Step();
            match.OfflineControls.Close();
            Assert.IsFalse(match.OfflineControls.BlocksWorldInput);
            match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases();
            Touch(1, TouchPhase.Ended, point + new Vector2(60, 0)); Step(); Step();
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            foreach (var unit in match.World.Units) Assert.AreEqual(UnitOrder.Idle, unit.Order);
            Assert.IsEmpty(match.Selection, "Opening the menu clears selection and the held touch cannot restore it.");
            Tap(2, EntityPoint(2));
            CollectionAssert.AreEqual(new[] { 2 }, match.Selection);
        }

        [Test]
        public void MouseDragSelectsAGroupWithoutPanningAndShiftClickAddsOrRemovesOne()
        {
            var camera = match.Rig.Camera.transform.position;
            var rectangle = WorkerRectangle();
            MouseDrag(rectangle.min, rectangle.max);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, match.Selection, "A left drag selects everyone inside the box.");
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f),
                "That drag draws the selection box instead of panning the view.");
            foreach (var unit in match.World.Units) if (unit.OwnerId == 1) Assert.AreEqual(UnitOrder.Idle, unit.Order);

            Shift(true);
            MouseClick(EntityPoint(4));
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, match.Selection, "Shift-clicking a selected unit drops it.");
            MouseClick(EntityPoint(4));
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, match.Selection, "Shift-clicking it again brings it back.");
            Shift(false);
        }

        [UnityTest]
        public IEnumerator ArrowKeysPanTheViewNowThatDraggingSelects()
        {
            var before = match.Rig.Camera.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            for (int frame = 0; frame < 6; frame++) { Step(); yield return null; }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Step(); yield return null;
            var moved = match.Rig.Camera.transform.position;
            Assert.That(Vector3.Distance(before, moved), Is.GreaterThan(.05f), "A held arrow key travels across the map.");
            Step(); yield return null;
            Assert.That(Vector3.Distance(moved, match.Rig.Camera.transform.position), Is.LessThan(.001f),
                "Releasing the key stops the view.");
        }

        private void MouseAt(Vector2 point, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, pressed));
            Step();
        }
        private void MouseDrag(Vector2 from, Vector2 to)
        {
            MouseAt(from, false); MouseAt(from, true);
            MouseAt(Vector2.Lerp(from, to, .5f), true); MouseAt(to, true);
            MouseAt(to, false); Step();
        }
        private void MouseClick(Vector2 point)
        {
            MouseAt(point, false); MouseAt(point, true); MouseAt(point, false); Step();
        }
        private void Shift(bool pressed)
        {
            InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(Key.LeftShift) : new KeyboardState());
            Step();
        }

        private Rect WorkerRectangle()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (int id in new[] { 1, 2, 3, 4 })
            {
                Vector2 point = match.Rig.Camera.WorldToScreenPoint(match.View.RootFor(id).position);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var margin = Vector2.one * (18 * Screen.height / 720f);
            min -= margin; max += margin;
            Assert.IsFalse(HitsUi(min)); Assert.IsFalse(HitsUi(max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Transform SelectionBox() => root.transform.Find("Tablet HUD/Selection rectangle");
        private Vector2 EntityPoint(int id)
            => match.Rig.Camera.WorldToScreenPoint(match.View.RootFor(id).position + Vector3.up * .6f);

        private static bool HitsUi(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            return hits.Count > 0;
        }

        private void Tap(int id, Vector2 point)
        {
            Touch(id, TouchPhase.Began, point); Step();
            Touch(id, TouchPhase.Ended, point); Step(); Step();
        }

        private void Touch(int id, TouchPhase phase, Vector2 point)
            => InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = point });
        private void Step() { InputSystem.Update(); adapter.Update(); }
    }
}
