using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Emberfield.Presentation
{
    public sealed class RtsInput : IDisposable
    {
        // Reference pixels of border that scroll the map, and how far a held key or edge travels per
        // second, measured in view heights so it reads the same at every zoom.
        private const float EdgeBand = 14, PanViewHeightsPerSecond = .75f;
        private readonly MatchController match;
        private readonly GestureTracker gesture = new GestureTracker();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData uiPointer;
        private EventSystem uiPointerSystem;
        private bool touchBlocked, multiTouch, mousePan, mouseBlocked;
        private bool touchGesture, selectionGesture, lastTapWasTouch, disposed;
        // A press drawing a wall (WallRunTool): where it began, and whether it has since moved past the drag threshold.
        private bool wallPress, wallDragged;
        private Vector2 previousMouse, previousCentre, lastTapPoint, wallPressPoint;
        private float pinchDistance, lastTapTime = -10, wallThreshold;
        private int fingerId = -1, pinchFirst = -1, pinchSecond = -1;
        private int screenWidth, screenHeight;
        private Rect safeArea;

        public RtsInput(MatchController match)
        {
            this.match = match;
            screenWidth = Screen.width; screenHeight = Screen.height;
            safeArea = Screen.safeArea;
            EnhancedTouchSupport.Enable();
        }

        public void Update()
        {
            if (match.Shell?.IsOpen == true) { Cancel(); return; }
            if (screenWidth != Screen.width || screenHeight != Screen.height || safeArea != Screen.safeArea)
            {
                Cancel(); screenWidth = Screen.width; screenHeight = Screen.height;
                safeArea = Screen.safeArea;
            }
            if (match.Research.IsOpen || match.Factions.IsOpen || match.OfflineControls?.BlocksWorldInput == true || match.Online?.BlocksWorldInput == true || match.Alpha?.IsOpen == true)
            {
                Cancel();
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                { match.Research.Close(); match.Factions.Close(); match.Alpha?.Close(); match.Online?.Close(); }
                return;
            }
            float now = Time.unscaledTime;
            KeyboardPan();
            // R turns a gate waiting to be placed a quarter, to stand in a north-south wall.
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) match.Economy.RotatePending();
            match.Groups.Update();
            var touches = EnhancedTouch.activeTouches;
            if (touches.Count > 0)
            {
                mousePan = false;
                if (!touchGesture && gesture.Active) ClearGesture();
                UpdateTouches(now);
                return;
            }
            if (fingerId != -1 || multiTouch || touchBlocked || touchGesture)
            {
                ClearGesture();
                fingerId = pinchFirst = pinchSecond = -1;
                multiTouch = touchBlocked = touchGesture = false;
                pinchDistance = 0;
            }
            UpdateMouse(now);
        }

        private void UpdateTouches(float now)
        {
            var touches = EnhancedTouch.activeTouches;
            if (touchBlocked) return; // UI, cancel and interrupted pinch sessions must completely lift.
            if (touches.Count > 2) { BlockTouches(); return; }
            if (touches.Count == 2)
            {
                var a = touches[0]; var b = touches[1];
                ClearGesture(); lastTapTime = -10;
                if (Ended(a.phase) || Ended(b.phase)) { BlockTouches(); return; }
                if (OverUi(a.screenPosition) || OverUi(b.screenPosition)) { BlockTouches(); return; }
                float distance = Vector2.Distance(a.screenPosition, b.screenPosition);
                var centre = (a.screenPosition + b.screenPosition) * .5f;
                if (!multiTouch)
                {
                    multiTouch = true; pinchFirst = a.touchId; pinchSecond = b.touchId;
                }
                else
                {
                    bool samePair = (a.touchId == pinchFirst && b.touchId == pinchSecond) ||
                        (a.touchId == pinchSecond && b.touchId == pinchFirst);
                    if (!samePair) { BlockTouches(); return; }
                    float scale = pinchDistance > 1 && distance > 1 ? pinchDistance / distance : 1;
                    match.Rig.PanAndZoom(previousCentre, centre, scale);
                }
                pinchDistance = distance; previousCentre = centre;
                return;
            }
            if (multiTouch) { BlockTouches(); return; }
            var touch = touches[0];
            if (touch.phase == TouchPhase.Canceled) { BlockTouches(); return; }
            // With a wall chosen one finger draws it rather than panning or selecting; two still pan and zoom.
            if (match.Economy.WallRun.Active) { DrawWall(touch); return; }
            if (touch.phase == TouchPhase.Began)
            {
                if (fingerId != -1 && fingerId != touch.touchId) { BlockTouches(); return; }
                fingerId = touch.touchId;
                if (OverUi(touch.screenPosition)) { BlockTouches(); return; }
                touchGesture = true;
                selectionGesture = match.TouchSelectionMode;
                gesture.DragThreshold = MobileInputSettings.ScreenPixels(MobileInputSettings.DragThreshold * (match.Alpha?.Settings.Value.DragTolerance ?? 1), screenHeight);
                gesture.Begin(touch.screenPosition, now, selectionGesture);
            }
            if (fingerId != touch.touchId || !gesture.Active) return;
            if (OverUi(touch.screenPosition)) { BlockTouches(); return; }
            if (touch.phase == TouchPhase.Ended) Finish(touch.screenPosition, now);
            else Drag(touch.screenPosition, now);
        }

        private void UpdateMouse(float now)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouseBlocked)
            {
                if (!AnyMouseButton(mouse)) mouseBlocked = false;
                return;
            }
            var point = mouse.position.ReadValue();
            if (mouse.middleButton.wasPressedThisFrame)
            {
                ClearGesture(); mousePan = !OverUi(point); previousMouse = point;
            }
            if (mouse.middleButton.isPressed && mousePan)
            {
                if (OverUi(point)) { mousePan = false; mouseBlocked = true; }
                else { match.Rig.Pan(previousMouse, point); previousMouse = point; }
            }
            if (mouse.middleButton.wasReleasedThisFrame) mousePan = false;
            if (match.Economy.WallRun.Active) { DrawWall(mouse, point); return; }
            if (mouse.leftButton.wasPressedThisFrame && !mouse.middleButton.isPressed)
            {
                ClearGesture();
                if (!OverUi(point))
                {
                    touchGesture = selectionGesture = false;
                    gesture.DragThreshold = MobileInputSettings.DragThreshold * (match.Alpha?.Settings.Value.DragTolerance ?? 1);
                    // Dragging the left button always draws the selection box, the way the classics do it.
                    // The camera travels with the middle button, the keyboard or the screen edge instead.
                    gesture.Begin(point, now, true);
                }
            }
            if (gesture.Active && OverUi(point)) ClearGesture();
            if (mouse.leftButton.isPressed) Drag(point, now);
            if (mouse.leftButton.wasReleasedThisFrame) Finish(point, now);
            if (!gesture.Active && !mousePan && mouse.rightButton.wasPressedThisFrame && !OverUi(point)) match.Tap(point, false);
            float scroll = mouse.scroll.ReadValue().y;
            if (!OverUi(point) && Mathf.Abs(scroll) > .1f) match.Rig.ZoomSmoothly(Mathf.Exp(-scroll * .001f));
            if (!gesture.Active && !mousePan && !AnyMouseButton(mouse)) EdgePan(point);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); match.ClearSelection(); }
        }

        // With a wall chosen the left button draws it (WallRunTool): a drag lays a straight wall, clicks fix corners.
        // The right button, Enter or a double click orders what is drawn, Backspace takes the last corner back, and a
        // right click with nothing drawn puts the wall away. Escape puts it away too but, unlike Escape anywhere else,
        // keeps the builders selected. The camera still moves: middle button, keys, wheel and the screen edge.
        private void DrawWall(Mouse mouse, Vector2 point)
        {
            var tool = match.Economy.WallRun;
            var keyboard = Keyboard.current;
            if (gesture.Active) ClearGesture();
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { ClearGesture(); match.Economy.CancelBuild(); return; }
            bool overUi = OverUi(point);
            if (!overUi) PointWall(point);
            if (mouse.leftButton.wasPressedThisFrame && !mouse.middleButton.isPressed && !overUi)
                PressWall(point, MobileInputSettings.DragThreshold * (match.Alpha?.Settings.Value.DragTolerance ?? 1));
            if (wallPress && (point - wallPressPoint).sqrMagnitude >= wallThreshold * wallThreshold) wallDragged = true;
            if (wallPress && mouse.leftButton.wasReleasedThisFrame) { wallPress = false; tool.Release(wallDragged, false); }
            else if (!wallPress && mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                if (tool.IsDrawing) match.Economy.ConfirmBuild(); else match.Economy.CancelBuild();
            }
            else if (!wallPress && keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) { if (tool.IsDrawing) match.Economy.ConfirmBuild(); }
                else if (keyboard.backspaceKey.wasPressedThisFrame) tool.UndoCorner();
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (!overUi && Mathf.Abs(scroll) > .1f) match.Rig.ZoomSmoothly(Mathf.Exp(-scroll * .001f));
            // A long wall runs off the screen: the edge keeps scrolling while the button is held.
            if (!mousePan && !mouse.middleButton.isPressed) EdgePan(point);
        }

        // One finger draws the wall: where it lands starts the run or its next leg, and where it lifts fixes a corner.
        // Only the Confirm wall button orders it, so a slip of the finger never spends anything.
        private void DrawWall(EnhancedTouch touch)
        {
            var point = touch.screenPosition;
            if (touch.phase == TouchPhase.Began)
            {
                if (fingerId != -1 && fingerId != touch.touchId) { BlockTouches(); return; }
                fingerId = touch.touchId;
                if (OverUi(point)) { BlockTouches(); return; }
                touchGesture = true;
                PressWall(point, MobileInputSettings.ScreenPixels(MobileInputSettings.DragThreshold * (match.Alpha?.Settings.Value.DragTolerance ?? 1), screenHeight));
                return;
            }
            if (fingerId != touch.touchId || !wallPress) return;
            if (OverUi(point)) { BlockTouches(); return; }
            PointWall(point);
            if ((point - wallPressPoint).sqrMagnitude >= wallThreshold * wallThreshold) wallDragged = true;
            if (touch.phase != TouchPhase.Ended) return;
            var tool = match.Economy.WallRun;
            wallPress = false; tool.Release(wallDragged, true); tool.Lift();
        }

        private void PressWall(Vector2 point, float threshold)
        {
            wallPress = true; wallDragged = false; wallPressPoint = point; wallThreshold = threshold;
            PointWall(point);
            match.Economy.WallRun.Press();
        }

        private void PointWall(Vector2 point)
        {
            if (match.Rig.GroundPoint(point, out var ground)) match.Economy.WallRun.Point(DefinitionLoader.ToSimulation(ground));
        }

        private void Drag(Vector2 point, float now)
        {
            if (!gesture.Active) return;
            var last = gesture.Last;
            var kind = gesture.Move(point, now);
            if (kind == GestureKind.Pan) match.Rig.Pan(last, point);
            else if (kind == GestureKind.BoxSelection) match.Hud.SetSelectionBox(Rectangle(gesture.Start, point));
        }

        private void Finish(Vector2 point, float now)
        {
            if (!gesture.Active) return;
            var start = gesture.Start;
            var kind = gesture.End(point, now);
            match.Hud.SetSelectionBox(null);
            // Holding shift keeps the group you already have and works on top of it.
            bool additive = !touchGesture && Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (kind == GestureKind.Tap && !OverUi(point))
            {
                float radius = touchGesture ? MobileInputSettings.ScreenPixels(MobileInputSettings.DoubleTapRadius, screenHeight) : MobileInputSettings.DoubleTapRadius;
                bool twice = touchGesture == lastTapWasTouch && now - lastTapTime < MobileInputSettings.DoubleTapSeconds && Vector2.Distance(point, lastTapPoint) < radius;
                if (additive) ToggleAt(point);
                else if (selectionGesture) SelectAt(point, twice);
                else match.Tap(point, twice);
                lastTapTime = now; lastTapPoint = point; lastTapWasTouch = touchGesture;
            }
            else if (kind == GestureKind.BoxSelection)
            {
                match.Select(match.View.VisibleUnits(match.Rig.Camera, MatchController.LocalPlayer, null, Rectangle(start, point)), additive);
                lastTapTime = -10;
            }
            else lastTapTime = -10;
        }

        private void ToggleAt(Vector2 point)
        {
            int id = match.View.Pick(match.Rig.Camera, point, Mathf.Max(18, Screen.height / 38f));
            if (id == 0) return;
            bool owned = match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer;
            owned |= match.World.TryGetBuilding(id, out var building) && building.OwnerId == MatchController.LocalPlayer;
            if (owned) match.ToggleSelected(id);
        }

        private void KeyboardPan()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var direction = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction.x -= 1;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction.x += 1;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) direction.y += 1;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) direction.y -= 1;
            PanBy(direction);
        }

        // The pointer resting against a border scrolls the map, and the command bar is exempt so that
        // reaching for a button never moves the view.
        private void EdgePan(Vector2 point)
        {
            if (point.x < 0 || point.y < 0 || point.x > screenWidth || point.y > screenHeight) return;
            float band = MobileInputSettings.ScreenPixels(EdgeBand, screenHeight);
            var direction = Vector2.zero;
            if (point.x <= band) direction.x -= 1; else if (point.x >= screenWidth - band) direction.x += 1;
            if (point.y <= band) direction.y -= 1; else if (point.y >= screenHeight - band) direction.y += 1;
            if (direction != Vector2.zero && !OverUi(point)) PanBy(direction);
        }

        private void PanBy(Vector2 direction)
        {
            if (direction == Vector2.zero) return;
            match.Rig.PanBy(direction.normalized, PanViewHeightsPerSecond * Time.unscaledDeltaTime);
        }

        private void SelectAt(Vector2 point, bool twice)
        {
            int id = match.View.Pick(match.Rig.Camera, point, Mathf.Max(18, Screen.height / 38f));
            if (match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer)
                match.Select(twice ? match.View.VisibleUnits(match.Rig.Camera, MatchController.LocalPlayer, unit.DefinitionId) : new List<int> { id });
            else if (match.World.TryGetBuilding(id, out var building) && building.OwnerId == MatchController.LocalPlayer)
                match.Select(new[] { id });
            else match.Select(Array.Empty<int>());
        }

        private bool OverUi(Vector2 point)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            // One pointer for the raycasts, asked every frame: only its position is read.
            if (uiPointer == null || uiPointerSystem != EventSystem.current) { uiPointerSystem = EventSystem.current; uiPointer = new PointerEventData(uiPointerSystem); }
            uiPointer.position = point;
            EventSystem.current.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }

        private void ClearGesture()
        {
            gesture.Cancel(); match.Hud.SetSelectionBox(null);
            // A wall press cut short by a second finger, a menu or lost focus leaves the run as it was.
            if (wallPress) { wallPress = false; match.Economy.WallRun.Abort(); }
        }
        private void BlockTouches() { ClearGesture(); touchBlocked = true; lastTapTime = -10; }
        private static bool Ended(TouchPhase phase) => phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
        private static bool AnyMouseButton(Mouse mouse) => mouse.leftButton.isPressed || mouse.middleButton.isPressed || mouse.rightButton.isPressed;
        private static Rect Rectangle(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        // Called by the controller on focus loss, pause, modal opening or a touch-mode change.
        public void Cancel()
        {
            ClearGesture(); lastTapTime = -10; mousePan = false;
            touchBlocked = fingerId != -1 || multiTouch || EnhancedTouch.activeTouches.Count > 0;
            if (Mouse.current != null) mouseBlocked = AnyMouseButton(Mouse.current);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; EnhancedTouchSupport.Disable();
        }
    }
}
