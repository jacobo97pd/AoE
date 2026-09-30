using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public class MobileHudLayoutTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [TestCase(1280, 720, 0, 0, 1280, 720)]
        [TestCase(2340, 1080, 0, 0, 2340, 1080)]
        [TestCase(2048, 1536, 0, 0, 2048, 1536)]
        [TestCase(1024, 768, 0, 0, 1024, 768)]
        [TestCase(2340, 1080, 132, 24, 2170, 1020)]
        [TestCase(2340, 1080, 38, 36, 2170, 1020)]
        public void DeviceAndRotatedNotchLayoutsKeepAllButtonsInsideSafeArea(int width, int height, int x, int y, int safeWidth, int safeHeight)
        {
            var expectedSafe = new Rect(x, y, safeWidth, safeHeight);
            var metrics = MobileHudLayout.Resolve(width, height, expectedSafe, 11, 7);
            var fixture = CreateFixture(11, 7);
            Apply(fixture, width, height, metrics);
            AssertGeometry(fixture, metrics);
            var safeBounds = BoundsIn(fixture.Safe, fixture.Root);
            Assert.That(safeBounds.xMin * metrics.Scale, Is.EqualTo(expectedSafe.xMin).Within(.1f));
            Assert.That(safeBounds.yMin * metrics.Scale, Is.EqualTo(expectedSafe.yMin).Within(.1f));
            Assert.That(safeBounds.xMax * metrics.Scale, Is.EqualTo(expectedSafe.xMax).Within(.1f));
            Assert.That(safeBounds.yMax * metrics.Scale, Is.EqualTo(expectedSafe.yMax).Within(.1f));
            Assert.That(metrics.Scale, Is.EqualTo(height / metrics.ReferenceResolution.y).Within(.0001f),
                "CanvasScaler's full-screen scale must agree with the safe-viewport layout scale.");
            Assert.That(metrics.CommandHeight, Is.LessThan(metrics.LogicalSize.y * .5f));
        }

        [Test]
        public void ExistingHierarchyReflowsAcrossAspectAndSafeAreaChangesIncludingDenseContextRows()
        {
            var fixture = CreateFixture(11, 12);
            var snapshots = new[] {
                new Vector2Int(1280, 720), new Vector2Int(2048, 1536),
                new Vector2Int(2340, 1080), new Vector2Int(1024, 768)
            };
            float smallestCell = float.MaxValue, largestCell = 0;
            foreach (var size in snapshots)
            {
                var safe = size.x == 2340 ? new Rect(132, 24, 2170, 1020) : new Rect(0, 0, size.x, size.y);
                var metrics = MobileHudLayout.Resolve(size.x, size.y, safe, 11, 12);
                Apply(fixture, size.x, size.y, metrics);
                AssertGeometry(fixture, metrics);
                Assert.That(metrics.ContextButtons.Rows, Is.GreaterThan(1), "Dense context actions must wrap instead of shrinking below their readable width.");
                smallestCell = Mathf.Min(smallestCell, fixture.Context.GetComponent<GridLayoutGroup>().cellSize.x);
                largestCell = Mathf.Max(largestCell, fixture.Context.GetComponent<GridLayoutGroup>().cellSize.x);
            }
            Assert.That(largestCell - smallestCell, Is.GreaterThan(1), "The same hierarchy must actually recompute its cell geometry.");
            Assert.AreEqual(11, fixture.Actions.childCount);
            Assert.AreEqual(12, fixture.Context.childCount);
        }

        [Test]
        public void MissingOrOutOfBoundsSafeAreasResolveToFiniteUsableGeometry()
        {
            var empty = MobileHudLayout.Resolve(1280, 720, Rect.zero, 11, 7);
            Assert.AreEqual(new Rect(0, 0, 1280, 720), empty.SafePixels);
            var oversized = MobileHudLayout.Resolve(1280, 720, new Rect(-40, -80, 1500, 1000), 11, 7);
            Assert.AreEqual(empty.SafePixels, oversized.SafePixels);
            Assert.That(oversized.Scale, Is.GreaterThan(0));
            Assert.AreEqual(empty.ActionButtons.CellSize, oversized.ActionButtons.CellSize);
        }

        [UnityTest]
        public IEnumerator ActualHudButtonsMeetTargetSizeAndSelectionBoxKeepsScreenCoordinatesAcrossCanvasModes()
        {
            root = new GameObject("Actual responsive HUD");
            root.SetActive(false);
            var match = root.AddComponent<MatchController>();
            match.enabled = false;
            root.SetActive(true);
            yield return null;
            match.Select(new[] { 1 }); match.Hud.Invalidate(); match.SyncPresentation(1);
            Canvas.ForceUpdateCanvases();
            var canvas = root.transform.Find("Tablet HUD").GetComponent<Canvas>();
            var safe = canvas.transform.Find("Safe area").GetComponent<RectTransform>();
            var actions = safe.Find("Commands/Actions").GetComponent<RectTransform>();
            var context = safe.Find("Commands/Context actions").GetComponent<RectTransform>();
            Assert.That(ActiveButtons(actions).Count, Is.GreaterThanOrEqualTo(8));
            AssertButtons(actions, MobileHudLayout.MinimumActionWidth);
            AssertButtons(context, MobileHudLayout.MinimumContextWidth);
            Assert.AreEqual(1, MobileHudLayout.Resolve(Screen.width, Screen.height, Screen.safeArea,
                ActiveButtons(actions).Count, ActiveButtons(context).Count).ActionButtons.Rows,
                "Every command of a real match must sit on one row.");
            var box = canvas.transform.Find("Selection rectangle").GetComponent<RectTransform>();
            var rectangle = new Rect(Screen.width * .21f, Screen.height * .4f, Screen.width * .22f, Screen.height * .17f);
            foreach (bool cameraCanvas in new[] { false, true })
            {
                if (cameraCanvas) match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
                // Inset the real safe-area RectTransform without a global Screen override or shipping test flag.
                safe.anchorMin = new Vector2(.07f, .04f); safe.anchorMax = new Vector2(.93f, .96f);
                Canvas.ForceUpdateCanvases();
                match.Hud.SetSelectionBox(rectangle);
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4]; box.GetWorldCorners(corners);
                var eventCamera = cameraCanvas ? match.Rig.Camera : null;
                Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
                Assert.That(Vector2.Distance(min, rectangle.min), Is.LessThan(.2f));
                Assert.That(Vector2.Distance(max, rectangle.max), Is.LessThan(.2f));
            }
            match.Hud.SetSelectionBox(null);
            Assert.IsFalse(box.gameObject.activeSelf);
        }

        private sealed class Fixture
        {
            public RectTransform Root, Safe, Commands, Actions, Context, Details, Feedback;
        }

        private Fixture CreateFixture(int actions, int context)
        {
            root = new GameObject("Responsive viewport", typeof(RectTransform));
            var fixture = new Fixture { Root = root.GetComponent<RectTransform>() };
            fixture.Root.pivot = Vector2.zero;
            fixture.Safe = RectChild("Safe area", fixture.Root);
            fixture.Commands = RectChild("Commands", fixture.Safe);
            fixture.Actions = RectChild("Actions", fixture.Commands);
            fixture.Context = RectChild("Context actions", fixture.Commands);
            fixture.Details = RectChild("Selection", fixture.Commands);
            fixture.Feedback = RectChild("Feedback", fixture.Commands);
            fixture.Actions.gameObject.AddComponent<GridLayoutGroup>();
            fixture.Context.gameObject.AddComponent<GridLayoutGroup>();
            for (int i = 0; i < actions; i++) RectChild("Action " + i, fixture.Actions).gameObject.AddComponent<Button>();
            for (int i = 0; i < context; i++) RectChild("Context " + i, fixture.Context).gameObject.AddComponent<Button>();
            return fixture;
        }

        private static void Apply(Fixture fixture, int width, int height, MobileHudLayout.Metrics metrics)
        {
            fixture.Root.sizeDelta = new Vector2(width, height) / metrics.Scale;
            MobileHudLayout.Apply(metrics, fixture.Safe, fixture.Commands,
                fixture.Actions, fixture.Actions.GetComponent<GridLayoutGroup>(),
                fixture.Context, fixture.Context.GetComponent<GridLayoutGroup>(), fixture.Details, fixture.Feedback);
            // These grids are separate layout roots: uGUI intentionally does not traverse
            // a plain RectTransform parent with no ILayoutController in this canvas-free fixture.
            LayoutRebuilder.ForceRebuildLayoutImmediate(fixture.Actions);
            LayoutRebuilder.ForceRebuildLayoutImmediate(fixture.Context);
        }

        private static void AssertGeometry(Fixture fixture, MobileHudLayout.Metrics metrics)
        {
            Assert.That(fixture.Safe.rect.width, Is.EqualTo(metrics.LogicalSize.x).Within(.1f));
            Assert.That(fixture.Safe.rect.height, Is.EqualTo(metrics.LogicalSize.y).Within(.1f));
            AssertInside(BoundsIn(fixture.Commands, fixture.Safe), fixture.Safe.rect);
            foreach (var row in new[] { fixture.Actions, fixture.Context, fixture.Details, fixture.Feedback })
                AssertInside(BoundsIn(row, fixture.Commands), fixture.Commands.rect);
            Assert.IsFalse(BoundsIn(fixture.Actions, fixture.Commands).Overlaps(BoundsIn(fixture.Context, fixture.Commands)));
            Assert.IsFalse(BoundsIn(fixture.Context, fixture.Commands).Overlaps(BoundsIn(fixture.Details, fixture.Commands)));
            AssertButtons(fixture.Actions, MobileHudLayout.MinimumActionWidth); AssertButtons(fixture.Context, MobileHudLayout.MinimumContextWidth);
        }

        private static void AssertButtons(RectTransform row, float minimumWidth)
        {
            var buttons = ActiveButtons(row);
            Assert.IsNotEmpty(buttons);
            foreach (var button in buttons)
            {
                var bounds = BoundsIn(button, row);
                Assert.That(bounds.width, Is.GreaterThanOrEqualTo(minimumWidth - .1f));
                Assert.That(bounds.height, Is.GreaterThanOrEqualTo(MobileHudLayout.MinimumTarget));
                AssertInside(bounds, row.rect);
            }
            for (int i = 0; i < buttons.Count; i++) for (int j = i + 1; j < buttons.Count; j++)
            {
                var a = BoundsIn(buttons[i], row); var b = BoundsIn(buttons[j], row);
                Assert.IsFalse(a.Overlaps(b), "Independent touch targets must never overlap.");
                if (Mathf.Abs(a.center.y - b.center.y) < .1f)
                    Assert.That(Mathf.Max(a.xMin, b.xMin) - Mathf.Min(a.xMax, b.xMax), Is.GreaterThanOrEqualTo(MobileHudLayout.Gap - .1f));
            }
        }

        private static List<RectTransform> ActiveButtons(RectTransform row)
        {
            var buttons = new List<RectTransform>();
            for (int i = 0; i < row.childCount; i++)
            {
                var child = row.GetChild(i);
                if (child.gameObject.activeSelf && child.GetComponent<Button>() != null) buttons.Add((RectTransform)child);
            }
            return buttons;
        }

        private static void AssertInside(Rect inner, Rect outer)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .1f));
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .1f));
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .1f));
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .1f));
        }

        private static Rect BoundsIn(RectTransform child, RectTransform ancestor)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var min = ancestor.InverseTransformPoint(corners[0]); var max = ancestor.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static RectTransform RectChild(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); return rect;
        }
    }
}
