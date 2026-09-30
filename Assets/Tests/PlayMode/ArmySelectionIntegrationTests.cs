using System;
using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Emberfield.Tests.PlayMode
{
    public class ArmySelectionIntegrationTests
    {
        private GameObject root;
        private MatchController match;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CategoryCountsFollowTheSelectedSubsetAndClicksNeverExpandIt()
        {
            Create(false);
            yield return null;
            match.Select(new[] { 10, 12, 13, 14 });
            Refresh();
            Assert.IsNotNull(FindButton("Infantry x1\n"));
            Assert.IsNotNull(FindButton("Ranged x2\n"));
            Assert.IsNotNull(FindButton("Cavalry x1\n"));

            // Selection changes do not explicitly invalidate the HUD. Its normal refresh must
            // detect different role counts even though both selections are mixed armies.
            match.Select(new[] { 10, 12, 14 });
            yield return new WaitForSecondsRealtime(.12f);
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
            Assert.IsNull(FindButton("Ranged x2\n"));
            var archers = FindButton("Ranged x1\n");
            Assert.IsNotNull(archers);
            CollectionAssert.Contains(match.View.VisibleUnits(match.Rig.Camera, 1, "stringwarden"), 13,
                "An unselected same-role unit must be visible so selecting the whole viewport would fail.");
            long tick = match.World.TickIndex;
            var positions = new Dictionary<int, SimPoint>();
            var destinations = new Dictionary<int, SimPoint>();
            var orders = new Dictionary<int, UnitOrder>();
            foreach (var unit in match.World.Units)
            {
                positions.Add(unit.Id, unit.Position);
                destinations.Add(unit.Id, unit.Destination);
                orders.Add(unit.Id, unit.Order);
            }
            yield return Click(archers);
            CollectionAssert.AreEqual(new[] { 12 }, match.Selection);
            Assert.AreEqual(tick, match.World.TickIndex);
            foreach (var unit in match.World.Units)
            {
                Assert.AreEqual(positions[unit.Id], unit.Position);
                Assert.AreEqual(destinations[unit.Id], unit.Destination);
                Assert.AreEqual(orders[unit.Id], unit.Order, "Filtering selection must not issue a world order.");
            }

            Refresh();
            Assert.IsNull(FindButton("Infantry x"));
            Assert.IsNull(FindButton("Cavalry x"));
            match.Select(new[] { 10, 13, 14 });
            yield return new WaitForSecondsRealtime(.12f);
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
            yield return Click(FindButton("Ranged x1\n"));
            CollectionAssert.AreEqual(new[] { 13 }, match.Selection,
                "Callbacks must filter the current IDs, never a previous selection with the same counts.");
        }

        [UnityTest]
        public IEnumerator AshrunnerCountsAsCavalryAndItsActionCoexistsWithRoleFiltering()
        {
            Create(true);
            yield return null;
            match.Select(new[] { 102 });
            Refresh();
            // The real equal-start budget covers these two authored recruits; no resource grants.
            yield return Click(FindButton("Train Ashrunner\n"));
            yield return Click(FindButton("Train Stringwarden\n"));
            for (int tick = 0; tick < 1000 && (FindUnit("ashrunner") == null || FindUnit("stringwarden") == null); tick++)
                match.World.Tick();
            var cavalry = FindUnit("ashrunner");
            var archer = FindUnit("stringwarden");
            Assert.IsNotNull(cavalry);
            Assert.IsNotNull(archer);
            match.Select(new[] { cavalry.Id, archer.Id });
            Refresh();
            Assert.IsNotNull(FindButton("Ranged x1\n"));
            Assert.IsNotNull(FindButton("Cavalry x1\n"), "Unique cavalry must share the military role category.");
            var mixedAction = FindButton("Reposition");
            Assert.IsTrue(mixedAction == null || !mixedAction.interactable,
                "Role filters must preserve the existing all-Ashrunner action requirement.");
            yield return Click(FindButton("Cavalry x1\n"));
            CollectionAssert.AreEqual(new[] { cavalry.Id }, match.Selection);
            Refresh();
            Assert.IsNotNull(FindButton("Cavalry x1\n"), "Faction actions cannot swallow the category controls.");
            Assert.IsNull(FindButton("Ranged x"));
            yield return Click(FindButton("Reposition"));
            Assert.AreEqual(match.Factions.LocalDefinition.RepositionTicks, cavalry.RepositionRemainingTicks);
            Assert.AreEqual(0, archer.RepositionRemainingTicks);
            CollectionAssert.AreEqual(new[] { cavalry.Id }, match.Selection);

            match.Select(new[] { 1, cavalry.Id });
            Refresh();
            Assert.IsNotNull(FindButton("Shelter\n"), "Worker-containing selections must retain their build controls.");
            Assert.IsNull(FindButton("Cavalry x"));
            Assert.IsNull(FindButton("Reposition"));
        }

        private void Create(bool serevin)
        {
            root = new GameObject("Army selection integration");
            root.SetActive(false);
            match = root.AddComponent<MatchController>();
            if (serevin) match.InitialFactionId = "serevin";
            else match.MapResourcePath = "Maps/combat_sandbox";
            match.enabled = false;
            root.SetActive(true);
        }

        private UnitState FindUnit(string definition)
        {
            foreach (var unit in match.World.Units)
                if (unit.OwnerId == MatchController.LocalPlayer && unit.DefinitionId == definition) return unit;
            return null;
        }

        private void Refresh()
        {
            match.Hud.Invalidate();
            match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
        }

        private Button FindButton(string prefix)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                var label = button.GetComponentInChildren<Text>();
                if (label != null && label.text.StartsWith(prefix, StringComparison.Ordinal)) return button;
            }
            return null;
        }

        private IEnumerator Click(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.interactable);
            // The disabled controller is refreshed synchronously, creating context controls in
            // this test frame. Rebuild the actual independent grid roots, then allow the normal
            // canvas frame to assign graphic depths before testing a real frontmost raycast.
            foreach (var grid in root.GetComponentsInChildren<GridLayoutGroup>())
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            var canvas = button.GetComponentInParent<Canvas>();
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            string diagnostic = "Expected " + Path(button.transform) + " at " + pointer.position
                + "; screen " + Screen.width + "x" + Screen.height + "; rect " + rect.rect
                + "; graphic depth " + button.targetGraphic.depth;
            Assert.IsNotEmpty(hits, diagnostic);
            var front = hits[0].gameObject.transform;
            Assert.IsTrue(front == button.transform || front.IsChildOf(button.transform),
                "The category or action must receive the frontmost UI raycast. " + diagnostic
                + "; actual front " + Path(front) + "; front depth " + hits[0].depth);
            Assert.IsTrue(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler));
        }

        private static string Path(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
            return path;
        }
    }
}
