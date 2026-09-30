using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Emberfield.Localization;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// Drawing a wall as one run through the real input adapter and HUD: a drag lays a straight wall, clicks chain
    /// corners and closing on the start orders the enclosure, every stretch is coloured by the rules' own verdict and
    /// the label adds up what the green ones cost, Escape and the cancel paths order nothing and keep the builders
    /// selected, and on a touchscreen each lift fixes a corner and only Confirm wall orders. A gate turns to stand in a
    /// north-south wall. The greybox practice world is used with the wall and gate made cheap and era-free for the
    /// test, so its 80 wood and 40 stone pay for exactly ten stretches.
    /// </summary>
    public sealed class WallRunToolTests
    {
        private static readonly int[] Workers = { 1, 2, 3, 4 };
        private static readonly ResourceAmount StretchCost = new ResourceAmount(0, 1, 0, 4);
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
            root = new GameObject("Wall run integration");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.enabled = false;
            root.SetActive(true);
            adapter = typeof(MatchController).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(match) as RtsInput;
            Assert.IsNotNull(adapter);
            yield return null;
            // Cheap and era-free for the test: the practice stock then pays for exactly ten stretches.
            foreach (var definition in match.World.Definition.Buildings)
                if (definition.IsWall || definition.IsGate) { definition.RequiredEraId = null; definition.Cost = StretchCost; }
            match.Select(Workers);
            Frame(14.5f, 22, 9);
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
        public void ADragLaysAStraightWallInOneOrderNearestTheBuildersFirstAndTheyBuildItOneStretchAfterAnother()
        {
            var run = match.Economy.WallRun;
            match.Economy.BeginBuild("wall");
            Assert.IsTrue(run.Active, "Choosing the wall opens the run.");
            Assert.AreEqual(WallRunTool.Guide, match.Feedback);
            MouseAt(Cell(20, 19), false);
            Assert.AreEqual(1, run.Sites.Count, "Before the press one stretch follows the pointer.");
            Assert.IsFalse(run.IsDrawing);
            MouseAt(Cell(20, 19), true);
            MouseAt(Cell(16, 19), true);
            MouseAt(Cell(12, 19), true);
            Assert.AreEqual(3, run.Sites.Count, "Nine cells westward are three stretches.");
            AssertGhostsFollowTheRules(3, 0, 0);
            var expected = new ResourceAmount(0, 3, 0, 12);
            Assert.AreEqual(expected, run.Cost);
            StringAssert.StartsWith("3 stretches · " + EconomyControls.CostText(expected) + "\nStock: 80 wood 40 stone", run.Label);
            Assert.AreEqual(run.Label, ShownLabel(), "The HUD shows the run's label beside it.");
            int before = match.World.Buildings.Count;
            var stock = Stock();

            MouseAt(Cell(12, 19), false);
            Assert.IsFalse(run.Active, "Releasing the drag orders the wall and puts the tool away.");
            Assert.IsNull(match.Economy.PendingBuildingId);
            StringAssert.StartsWith("Wall ordered: 3 stretches", match.Feedback);
            CollectionAssert.AreEqual(Workers, match.Selection, "The builders stay selected.");
            var laid = NewBuildings(before);
            Assert.AreEqual(3, laid.Count);
            Assert.AreEqual(new ResourceAmount(stock.Food, stock.Wood - expected.Wood, stock.Metal, stock.Stone - expected.Stone), Stock(), "The order cost what the label said.");
            // The drag ran west, away from the builders at z 16 and x 10-17: the order starts at the near, western end.
            Assert.AreEqual(new SimPoint(13500, 19500), laid[0].Position, "Nearest the builders first.");
            Assert.AreEqual(new SimPoint(16500, 19500), laid[1].Position);
            Assert.AreEqual(new SimPoint(19500, 19500), laid[2].Position);
            foreach (int id in Workers) Assert.AreEqual(laid[0].Id, Unit(id).TargetBuildingId, "Every builder goes to the first stretch.");

            var finished = new List<int>();
            for (int tick = 0; tick < 4000 && finished.Count < 3; tick++)
            {
                match.World.Tick();
                foreach (var wall in laid) if (wall.IsComplete && !finished.Contains(wall.Id)) finished.Add(wall.Id);
                for (int i = 1; i < laid.Count; i++)
                    if (!laid[i - 1].IsComplete) Assert.AreEqual(0, laid[i].ConstructionProgress, "A stretch is begun only once the one before it stands.");
            }
            CollectionAssert.AreEqual(new[] { laid[0].Id, laid[1].Id, laid[2].Id }, finished, "The stretches go up one after another, nearest first.");
        }

        [Test]
        public void ClicksChainCornersAndClosingOnTheStartOrdersTheWholeEnclosure()
        {
            var run = match.Economy.WallRun;
            match.Economy.BeginBuild("wall");
            MouseClick(Cell(10, 19));
            Assert.AreEqual(1, run.CornerCount, "A click fixes the start.");
            Assert.IsTrue(run.Active);
            MouseAt(Cell(18, 19), false);
            Assert.AreEqual(3, run.Sites.Count, "The run follows the pointer from the start.");
            MouseClick(Cell(18, 19));
            MouseClick(Cell(18, 25));
            MouseClick(Cell(10, 25));
            Assert.AreEqual(4, run.CornerCount);
            MouseAt(Cell(10, 19), false);
            Assert.IsTrue(run.Closed, "Back beside the start the enclosure closes.");
            Assert.AreEqual(10, run.Sites.Count, "Three stretches east, two north, three west and two south.");
            StringAssert.Contains("\nEnclosure closed", run.Label);
            AssertGhostsFollowTheRules(10, 0, 0);
            int before = match.World.Buildings.Count;

            MouseClick(Cell(10, 19));
            Assert.IsFalse(run.Active, "Closing the enclosure orders it: " + match.Feedback);
            var laid = NewBuildings(before);
            Assert.AreEqual(10, laid.Count);
            int turned = 0; foreach (var wall in laid) if (wall.IsTurned) turned++;
            Assert.AreEqual(4, turned, "The north-south sides stand turned.");
            Assert.AreEqual(new ResourceAmount(120, 70, 0, 0), Stock(), "Ten stretches spent the whole stone.");
            // The west side runs back down x9: the third leg's three stretches end a cell past the start's column.
            foreach (var cell in new[] { new GridCell(10, 19), new GridCell(18, 19), new GridCell(18, 25), new GridCell(9, 25), new GridCell(14, 19), new GridCell(9, 22) })
                Assert.IsFalse(match.World.IsWalkable(Centre(cell)), "The ring covers " + cell.X + "," + cell.Z);
            Assert.IsTrue(match.World.IsWalkable(Centre(new GridCell(14, 22))), "The inside stays open ground.");
        }

        [Test]
        public void EveryStretchIsGreenOrangeOrRedByTheRulesAndTheLabelAddsUpOnlyTheGreenOnes()
        {
            // Four stretches' worth of stone, and a line across the metal seam at x21 and the rocks at x26-28.
            foreach (var definition in match.World.Definition.Buildings) if (definition.IsWall) definition.Cost = new ResourceAmount(0, 1, 0, 10);
            Frame(21.5f, 21, 11);
            var run = match.Economy.WallRun;
            match.Economy.BeginBuild("wall");
            MouseAt(Cell(11, 21), false); MouseAt(Cell(11, 21), true);
            MouseAt(Cell(21, 21), true); MouseAt(Cell(31, 21), true);
            Assert.AreEqual(7, run.Sites.Count, "Twenty-one cells are seven stretches.");
            var sites = ToArray(run.Sites);
            var verdicts = match.World.PreviewBuildRun(new BuildRunCommand(1, Workers, "wall", sites));
            int green = 0, orange = 0, red = 0;
            foreach (var verdict in verdicts.Sites)
                if (verdict.Accepted) green++; else if (verdict.Reason == CommandRejection.InsufficientResources) orange++; else red++;
            Assert.AreEqual(4, green, "The stock pays for four.");
            Assert.AreEqual(1, orange, "Beyond the stock the last stretch is marked as unaffordable.");
            Assert.AreEqual(2, red, "Over the seam and the rocks the stretches cannot stand.");
            AssertGhostsFollowTheRules(green, orange, red);
            Assert.AreEqual(CommandRejection.DestinationBlocked, verdicts.Sites[3].Reason, "The stretch over the seam at x21 is refused.");
            Assert.AreEqual(CommandRejection.DestinationBlocked, verdicts.Sites[5].Reason, "The stretch over the rocks at x26-28 is refused.");
            Assert.AreEqual(verdicts.Cost, run.Cost, "The label's total is the rules' own.");
            Assert.AreEqual(new ResourceAmount(0, 4, 0, 40), run.Cost);
            StringAssert.StartsWith("4 stretches · 4 wood 40 stone\nStock: 80 wood 40 stone · " + orange + " need more resources\n" + red + " blocked · ", run.Label);
            Assert.AreEqual(WallRunTool.Blocked, run.Tone);
            int before = match.World.Buildings.Count;

            MouseAt(Cell(31, 21), false);
            var laid = NewBuildings(before);
            Assert.AreEqual(4, laid.Count, "Only the green stretches are ordered.");
            var greens = new List<SimPoint>();
            for (int i = 0; i < verdicts.Sites.Count; i++) if (verdicts.Sites[i].Accepted) greens.Add(sites[i].Position);
            foreach (var wall in laid) CollectionAssert.Contains(greens, wall.Position);
        }

        [Test]
        public void EscapeRightClickAndCancelOrderNothingAndKeepTheBuildersSelected()
        {
            var run = match.Economy.WallRun;
            int before = match.World.Buildings.Count;
            var stock = Stock();
            match.Economy.BeginBuild("wall");
            MouseClick(Cell(10, 19)); MouseAt(Cell(18, 19), false);
            Assert.AreEqual(3, run.Sites.Count);
            KeyPress(Key.Escape);
            Assert.IsFalse(run.Active, "Escape puts the wall away.");
            CollectionAssert.AreEqual(Workers, match.Selection, "Escape while drawing keeps the builders selected.");

            match.Economy.BeginBuild("wall");
            MouseAt(Cell(12, 19), false);
            RightClick(Cell(12, 19));
            Assert.IsFalse(run.Active, "A right click with nothing drawn puts the wall away.");
            CollectionAssert.AreEqual(Workers, match.Selection);

            match.Economy.BeginBuild("wall");
            MouseClick(Cell(10, 19)); MouseAt(Cell(18, 19), false);
            Refresh();
            Press("Cancel wall");
            Assert.IsFalse(run.Active);
            Assert.AreEqual(before, match.World.Buildings.Count, "Nothing was ordered.");
            Assert.AreEqual(stock, Stock(), "Nothing was spent.");
            foreach (var ghost in run.Ghosts) Assert.IsFalse(ghost.gameObject.activeSelf, "No ghost is left behind.");
            CollectionAssert.AreEqual(Workers, match.Selection);
        }

        [Test]
        public void RightClickOrdersTheRunAsDrawnAndBackspaceTakesTheLastCornerBack()
        {
            var run = match.Economy.WallRun;
            match.Economy.BeginBuild("wall");
            MouseClick(Cell(10, 19)); MouseClick(Cell(16, 19));
            MouseAt(Cell(16, 24), false);
            Assert.AreEqual(2, run.CornerCount);
            int drawn = run.Sites.Count;
            KeyPress(Key.Backspace);
            Assert.AreEqual(1, run.CornerCount, "Backspace takes the last corner back.");
            Assert.Less(run.Sites.Count, drawn);
            MouseClick(Cell(16, 19));
            MouseAt(Cell(16, 24), false);
            Assert.AreEqual(drawn, run.Sites.Count);
            int before = match.World.Buildings.Count;
            RightClick(Cell(16, 24));
            Assert.IsFalse(run.Active);
            Assert.AreEqual(drawn, match.World.Buildings.Count - before, "The right click ordered what was drawn, up to the pointer.");
        }

        [Test]
        public void OneFingerDrawsEveryLiftFixesACornerAndOnlyConfirmWallOrders()
        {
            var run = match.Economy.WallRun;
            int before = match.World.Buildings.Count;
            match.Economy.BeginBuild("wall");
            Touch(1, TouchPhase.Began, Cell(10, 19)); Step();
            Touch(1, TouchPhase.Moved, Cell(14, 19)); Step();
            Touch(1, TouchPhase.Moved, Cell(18, 19)); Step();
            Assert.AreEqual(3, run.Sites.Count, "The finger drags the run out.");
            Touch(1, TouchPhase.Ended, Cell(18, 19)); Step(); Step();
            Assert.IsTrue(run.Active, "Lifting the finger orders nothing.");
            Assert.AreEqual(2, run.CornerCount, "The lift fixed the far end.");
            Assert.AreEqual(3, run.Sites.Count, "The fixed run stays drawn.");
            Touch(2, TouchPhase.Began, Cell(18, 21)); Step();
            Touch(2, TouchPhase.Moved, Cell(18, 25)); Step();
            Touch(2, TouchPhase.Ended, Cell(18, 25)); Step(); Step();
            Assert.AreEqual(3, run.CornerCount);
            Assert.AreEqual(5, run.Sites.Count, "The next leg runs north from the last corner.");
            Assert.AreEqual(before, match.World.Buildings.Count, "Drawing with a finger orders nothing.");

            Refresh();
            var confirm = FindButton("Confirm wall");
            Assert.IsTrue(confirm.interactable);
            Assert.IsTrue(FindButton("Undo corner").interactable);
            confirm.onClick.Invoke();
            Assert.IsFalse(run.Active);
            Assert.AreEqual(5, match.World.Buildings.Count - before, "Confirm wall ordered the five stretches.");
        }

        [Test]
        public void ASecondFingerLeavesTheRunAsItWasAndPans()
        {
            var run = match.Economy.WallRun;
            match.Economy.BeginBuild("wall");
            var camera = match.Rig.Camera.transform.position;
            Touch(1, TouchPhase.Began, Cell(10, 19)); Step();
            Assert.AreEqual(1, run.CornerCount);
            Touch(1, TouchPhase.Moved, Cell(12, 19));
            Touch(2, TouchPhase.Began, Cell(16, 23)); Step();
            Assert.AreEqual(0, run.CornerCount, "A second finger undoes the press it interrupted.");
            Touch(1, TouchPhase.Moved, Cell(11, 19));
            Touch(2, TouchPhase.Moved, Cell(15, 22)); Step();
            Assert.Greater(Vector3.Distance(camera, match.Rig.Camera.transform.position), .01f, "Two fingers still move the view.");
            Touch(1, TouchPhase.Ended, Cell(11, 19));
            Touch(2, TouchPhase.Ended, Cell(15, 22)); Step(); Step();
            Assert.IsTrue(run.Active);
            Assert.AreEqual(0, run.CornerCount);
        }

        [Test]
        public void AGateTurnsToStandInANorthSouthWallAndRTurnsItByHand()
        {
            Accept(match.World.Submit(new BuildCommand(1, Workers, "wall", new SimPoint(12500, 20500), true)));
            Accept(match.World.Submit(new BuildCommand(1, Workers, "wall", new SimPoint(12500, 25500), true)));
            match.Economy.BeginBuild("gate");
            Assert.IsFalse(match.Economy.WallRun.Active, "A gate is placed on its own.");
            match.Economy.PreviewAt(new SimPoint(12400, 23100));
            Assert.IsTrue(match.Economy.Turned, "Between two north-south walls the gate turns to carry them on.");
            Assert.AreEqual(new SimPoint(12500, 23000), match.Economy.PreviewPosition);
            Assert.IsTrue(match.Economy.PlacementResult.Accepted, match.Economy.PlacementResult.Message);
            Refresh();
            Assert.IsNotNull(FindButton("Rotate gate"), "Placing a gate offers to turn it.");
            KeyPress(Key.R);
            Assert.IsFalse(match.Economy.Turned, "R turns it by hand.");
            match.Economy.PreviewAt(new SimPoint(12400, 23100));
            Assert.IsFalse(match.Economy.Turned, "Turned by hand, it stays that way.");
            KeyPress(Key.R);
            Assert.IsTrue(match.Economy.Turned);
            var result = match.Economy.ConfirmBuild();
            Accept(result);
            Assert.IsTrue(match.World.TryGetBuilding(result.EntityId, out var gate));
            Assert.IsTrue(gate.IsTurned); Assert.AreEqual(1, gate.WidthCells); Assert.AreEqual(2, gate.DepthCells);

            match.Economy.BeginBuild("gate");
            match.Economy.PreviewAt(new SimPoint(20000, 21500));
            Assert.IsFalse(match.Economy.Turned, "In the open a gate stands as it always has, east-west.");
        }

        [Test]
        public void AStillPointerCostsTheRunNothing()
        {
            match.Economy.BeginBuild("wall");
            MouseClick(Cell(10, 19)); MouseAt(Cell(18, 22), false);
            var refresh = (Action)Delegate.CreateDelegate(typeof(Action), match.Hud, typeof(MatchHud).GetMethod("RefreshWallRun", BindingFlags.Instance | BindingFlags.NonPublic));
            refresh(); refresh();
            Assert.That(() => refresh(), UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not));
        }

        [Test]
        public void TheLabelReadsInSpanish()
        {
            var spanish = SpanishCatalog.Create();
            Assert.AreEqual("3 tramos · 3 madera 12 piedra", spanish.Translate("3 stretches · 3 wood 12 stone"));
            Assert.AreEqual("1 tramo · 1 madera 4 piedra", spanish.Translate("1 stretch · 1 wood 4 stone"));
            Assert.AreEqual("Existencias: 80 madera 40 piedra · faltan recursos para 2", spanish.Translate("Stock: 80 wood 40 stone · 2 need more resources"));
            Assert.AreEqual("Cerco cerrado · 2 bloqueados · Una unidad ocupa el espacio del edificio o su margen.",
                spanish.Translate("Enclosure closed · 2 blocked · A unit occupies the building footprint or its clearance."));
            Assert.AreEqual("Confirmar muralla", spanish.Translate("Confirm wall"));
            match.Economy.BeginBuild("wall");
            MouseClick(Cell(10, 19)); MouseAt(Cell(18, 19), false);
            string shown = spanish.Translate(match.Economy.WallRun.Label);
            foreach (var english in new[] { "stretch", "Stock", "wood", "stone" }) StringAssert.DoesNotContain(english, shown);
        }

        // Each ghost wears the colour of the rules' own verdict on its stretch, and there are this many of each.
        private void AssertGhostsFollowTheRules(int green, int orange, int red)
        {
            var run = match.Economy.WallRun;
            var verdicts = match.World.PreviewBuildRun(new BuildRunCommand(1, Workers, "wall", ToArray(run.Sites)));
            int greens = 0, oranges = 0, reds = 0;
            for (int i = 0; i < run.Sites.Count; i++)
            {
                var ghost = run.Ghosts[i];
                Assert.IsTrue(ghost.gameObject.activeSelf, "Stretch " + i + " is drawn.");
                Assert.AreEqual(DefinitionLoader.ToWorld(run.Sites[i].Position).x, ghost.transform.position.x, .001f);
                Assert.AreEqual(DefinitionLoader.ToWorld(run.Sites[i].Position).z, ghost.transform.position.z, .001f);
                Assert.AreEqual(run.Sites[i].Turned, ghost.transform.localScale.z > ghost.transform.localScale.x, "A north-south stretch is drawn north-south.");
                var verdict = verdicts.Sites[i];
                var colour = verdict.Accepted ? WallRunTool.Buildable : verdict.Reason == CommandRejection.InsufficientResources ? WallRunTool.Unaffordable : WallRunTool.Blocked;
                var shown = ghost.sharedMaterial.color;
                Assert.That(Mathf.Abs(shown.r - colour.r) + Mathf.Abs(shown.g - colour.g) + Mathf.Abs(shown.b - colour.b), Is.LessThan(.01f),
                    "Stretch " + i + " is drawn " + shown + ": " + verdict.Message);
                if (verdict.Accepted) greens++; else if (verdict.Reason == CommandRejection.InsufficientResources) oranges++; else reds++;
            }
            for (int i = run.Sites.Count; i < run.Ghosts.Count; i++) Assert.IsFalse(run.Ghosts[i].gameObject.activeSelf, "Spare ghosts are hidden.");
            Assert.AreEqual(green, greens, "green"); Assert.AreEqual(orange, oranges, "orange"); Assert.AreEqual(red, reds, "red");
            Assert.AreEqual(green, run.AcceptedCount); Assert.AreEqual(orange, run.UnaffordableCount); Assert.AreEqual(red, run.BlockedCount);
        }

        private static BuildSite[] ToArray(IReadOnlyList<BuildSite> sites)
        {
            var array = new BuildSite[sites.Count];
            for (int i = 0; i < array.Length; i++) array[i] = sites[i];
            return array;
        }

        // North up, with this ground point in the clear band between the command panel and the minimap.
        private void Frame(float x, float z, float size)
        {
            match.Rig.Rotate(-match.Rig.Camera.transform.eulerAngles.y);
            match.Rig.SetHome(new Vector3(x, 0, z), size);
            match.Rig.Pan(match.Rig.Camera.WorldToScreenPoint(new Vector3(x, 0, z)), new Vector2(Screen.width * .5f, Screen.height * .6f));
        }

        private List<BuildingState> NewBuildings(int before)
        {
            var laid = new List<BuildingState>();
            for (int i = before; i < match.World.Buildings.Count; i++) laid.Add(match.World.Buildings[i]);
            laid.Sort((a, b) => a.Id.CompareTo(b.Id));
            return laid;
        }

        private string ShownLabel()
        {
            match.SyncPresentation(1);
            var tag = root.GetComponentsInChildren<RectTransform>(true);
            foreach (var rect in tag) if (rect.name == "Wall run cost") { Assert.IsTrue(rect.gameObject.activeSelf, "The cost label is shown."); return rect.GetComponentInChildren<Text>().text; }
            Assert.Fail("No cost label.");
            return null;
        }

        private Button FindButton(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true)) if (button.name == name && button.gameObject.activeInHierarchy) return button;
            return null;
        }
        private void Press(string name)
        {
            var button = FindButton(name);
            Assert.IsNotNull(button, name);
            button.onClick.Invoke();
        }
        private void Refresh() { match.Hud.Invalidate(); match.SyncPresentation(1); }

        private ResourceAmount Stock() { Assert.IsTrue(match.World.TryGetPlayer(1, out var player)); return player.Resources; }
        private UnitState Unit(int id) { Assert.IsTrue(match.World.TryGetUnit(id, out var unit)); return unit; }
        private static void Accept(CommandResult result) => Assert.IsTrue(result.Accepted, result.Message);
        private static SimPoint Centre(GridCell cell) => new SimPoint(cell.X * 1000 + 500, cell.Z * 1000 + 500);

        // The middle of a map cell on the screen, checked to be open ground rather than the HUD.
        private Vector2 Cell(int x, int z)
        {
            Vector2 point = match.Rig.Camera.WorldToScreenPoint(new Vector3(x + .5f, 0, z + .5f));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.IsEmpty(hits, "Cell " + x + "," + z + " is under the HUD at " + point + " of " + Screen.width + "x" + Screen.height + ".");
            return point;
        }

        private void MouseAt(Vector2 point, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, pressed));
            Step();
        }
        private void MouseClick(Vector2 point) { MouseAt(point, false); MouseAt(point, true); MouseAt(point, false); }
        private void RightClick(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Right, true)); Step();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); Step();
        }
        private void KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); Step();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Step();
        }
        private void Touch(int id, TouchPhase phase, Vector2 point) =>
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = point });
        private void Step() { InputSystem.Update(); adapter.Update(); match.SyncPresentation(1); }
    }
}
