using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class MinimapIntegrationTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup() { if (root != null) Object.Destroy(root); yield return null; }

        private MatchController Create(bool offline)
        {
            root = new GameObject("Minimap integration"); root.SetActive(false);
            var match = root.AddComponent<MatchController>(); match.enabled = false;
            if (offline) { match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Dominion; }
            root.SetActive(true); return match;
        }

        [UnityTest]
        public IEnumerator PracticeShowsAMinimapThatPaintsFullyExploredWithoutAVisionSystem()
        {
            var practice = Create(false); yield return null;
            var sandboxMinimap = practice.Hud.Minimap;
            Assert.IsNotNull(sandboxMinimap, "The practice sandbox now gets a minimap too.");
            Assert.IsNull(practice.World.Vision, "Practice has no fog system to drive it.");
            Assert.IsNull(practice.World.Match, "Practice has no match, so no Dominion beacons either.");
            yield return Repaint(practice);
            Assert.IsFalse(AnyUnexplored(sandboxMinimap.Texture.GetPixels32()),
                "With no Vision, nothing on the practice map should read as unexplored.");
            // A left click still only looks, exactly as it does in a real match.
            var uv = new Vector2(.4f, .6f);
            var pointer = new PointerEventData(EventSystem.current) {
                pointerId = 95, button = PointerEventData.InputButton.Left, position = ScreenPoint(sandboxMinimap, uv)
            };
            var sandboxHits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, sandboxHits);
            Assert.IsNotEmpty(sandboxHits); pointer.pointerPressRaycast = sandboxHits[0];
            Assert.IsTrue(ExecuteEvents.Execute(sandboxMinimap.Image.gameObject, pointer, ExecuteEvents.pointerDownHandler));
            AssertCameraAt(practice, sandboxMinimap.PointAtUv(uv));
            Object.Destroy(root); yield return null;
        }

        [UnityTest]
        public IEnumerator OfflineMinimapInterceptsPointersAndChangesCameraWithoutOrdersAndModalInputCannotResume()
        {
            var match = Create(true); yield return null;
            var minimap = match.Hud.Minimap; Assert.IsNotNull(minimap);
            match.Select(new[] { 1 }); match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases();
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            var position = worker.Position; var destination = worker.Destination; var order = worker.Order;
            long tick = match.World.TickIndex;
            var camera = match.Rig.Camera.transform.position;
            var first = new Vector2(.5f, .5f);
            var pointer = new PointerEventData(EventSystem.current) {
                pointerId = 91, button = PointerEventData.InputButton.Left, position = ScreenPoint(minimap, first)
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits); Assert.AreSame(minimap.Image.gameObject, hits[0].gameObject);
            pointer.pointerPressRaycast = hits[0];
            Assert.IsTrue(ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerDownHandler));
            AssertCameraAt(match, minimap.PointAtUv(first));
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.GreaterThan(5));
            var second = new Vector2(.65f, .58f); pointer.position = ScreenPoint(minimap, second);
            Assert.IsTrue(ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.dragHandler));
            AssertCameraAt(match, minimap.PointAtUv(second));
            CollectionAssert.AreEqual(new[] { 1 }, match.Selection);
            Assert.AreEqual(position, worker.Position); Assert.AreEqual(destination, worker.Destination);
            Assert.AreEqual(order, worker.Order); Assert.AreEqual(tick, match.World.TickIndex);

            // Menu cancellation consumes the held minimap drag, just as it consumes a held world gesture.
            match.OfflineControls.Open(); match.Hud.Invalidate(); match.SyncPresentation(1);
            camera = match.Rig.Camera.transform.position;
            match.OfflineControls.Close(); match.Hud.Invalidate(); match.SyncPresentation(1);
            pointer.position = ScreenPoint(minimap, new Vector2(.35f, .4f));
            ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.dragHandler);
            Assert.That(Vector3.Distance(camera, match.Rig.Camera.transform.position), Is.LessThan(.001f));
            ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            pointer.pointerId = 92;
            ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            AssertCameraAt(match, minimap.PointAtUv(new Vector2(.35f, .4f)));
            ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            var parent = (RectTransform)minimap.Root.parent;
            var corners = new Vector3[4]; minimap.Root.GetWorldCorners(corners);
            Assert.IsTrue(parent.rect.Contains(parent.InverseTransformPoint(corners[0])));
            Assert.IsTrue(parent.rect.Contains(parent.InverseTransformPoint(corners[2])));
        }

        [UnityTest]
        public IEnumerator MinimapRightClickOrdersTheSelectionToTheGroundBelowTheCursor()
        {
            var match = Create(true); yield return null;
            var minimap = match.Hud.Minimap; Assert.IsNotNull(minimap);
            match.Select(new[] { 1 }); match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases();
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            var destination = MoveNear(match.World, 1, 1, worker.Position);
            long tick = match.World.TickIndex;
            var pointer = new PointerEventData(EventSystem.current) {
                pointerId = 81, button = PointerEventData.InputButton.Right, position = ScreenPoint(minimap, minimap.MapUv(destination))
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits); Assert.AreSame(minimap.Image.gameObject, hits[0].gameObject);
            pointer.pointerPressRaycast = hits[0];
            Assert.IsTrue(ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerClickHandler));
            Assert.AreEqual("Moving in loose formation.", match.Feedback, "The minimap order reuses the ground order's own feedback.");
            Assert.That(Vector3.Distance(DefinitionLoader.ToWorld(worker.Destination), DefinitionLoader.ToWorld(destination)), Is.LessThan(.02f));
            Assert.AreEqual(UnitOrder.Moving, worker.Order);
            Assert.AreEqual(tick, match.World.TickIndex, "Issuing a minimap order must not itself advance the simulation.");
        }

        [UnityTest]
        public IEnumerator MinimapRightClickDoesNothingWithoutASelectionOrWhileAMenuBlocksInput()
        {
            var match = Create(true); yield return null;
            var minimap = match.Hud.Minimap; Assert.IsNotNull(minimap);
            Assert.IsTrue(match.World.TryGetUnit(1, out var worker));
            var destination = worker.Destination; var order = worker.Order;
            var pointer = new PointerEventData(EventSystem.current) {
                pointerId = 82, button = PointerEventData.InputButton.Right, position = ScreenPoint(minimap, new Vector2(.4f, .45f))
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits); Assert.AreSame(minimap.Image.gameObject, hits[0].gameObject); pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(destination, worker.Destination, "Nothing was selected, so nothing may move.");
            Assert.AreEqual(order, worker.Order);

            // Selected now, but a menu is open: it blocks a minimap order the same way it blocks a ground order.
            match.Select(new[] { 1 }); match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases();
            match.OfflineControls.Open(); match.Hud.Invalidate(); match.SyncPresentation(1);
            ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(destination, worker.Destination, "A blocked minimap must not order the selection either.");
            Assert.AreEqual(order, worker.Order);
            match.OfflineControls.Close();
        }

        [UnityTest]
        public IEnumerator MinimapKeepsPublicBeaconsButHiddenEnemyMovementDoesNotChangePixelsAndScoutingRevealsThenHidesMarkers()
        {
            var match = Create(true); yield return null;
            var minimap = match.Hud.Minimap; var texture = minimap.Texture;
            yield return Repaint(match);
            var before = texture.GetPixels32(); Assert.AreEqual(0, EnemyPixels(before));
            Assert.AreEqual(3, match.World.Match.Objectives.Count);
            foreach (var objective in match.World.Match.Objectives)
            {
                var uv = minimap.MapUv(objective.Position);
                int x = Mathf.FloorToInt(uv.x * texture.width), y = Mathf.FloorToInt(uv.y * texture.height);
                Assert.AreEqual(new Color32(244, 196, 90, 255), (Color32)texture.GetPixel(x + 3, y),
                    "The public beacon outline must be present even before its terrain is explored.");
            }
            // Positions come from the map so this keeps working whatever size the battlefield is baked at.
            Assert.IsTrue(match.World.TryGetBuilding(101, out var rival));
            Assert.IsTrue(match.World.TryGetUnit(1, out var home));
            var homeGround = home.Position;
            Assert.IsFalse(match.World.Vision.IsEntityVisible(1, 11));
            Assert.IsTrue(match.World.TryGetUnit(11, out var enemy));
            var enemyStart = enemy.Position;
            MoveNear(match.World, 2, 11, rival.Position);
            for (int tick = 0; tick < 100; tick++) match.World.Tick();
            Assert.AreNotEqual(enemy.Position, enemyStart);
            Assert.IsFalse(match.World.Vision.IsEntityVisible(1, enemy.Id));
            yield return Repaint(match);
            Assert.AreSame(texture, minimap.Texture, "Refresh must reuse its texture.");
            CollectionAssert.AreEqual(before, texture.GetPixels32(), "A hidden rival moving cannot alter the local map.");

            MoveNear(match.World, 1, 1, rival.Position);
            Assert.IsTrue(match.World.TryGetUnit(1, out var scout));
            for (int tick = 0; tick < 3000 && scout.Order != UnitOrder.Idle; tick++) match.World.Tick();
            Assert.AreEqual(UnitOrder.Idle, scout.Order);
            Assert.IsTrue(match.World.Vision.IsEntityVisible(1, 101));
            yield return Repaint(match);
            Assert.That(EnemyPixels(texture.GetPixels32()), Is.GreaterThan(0), "A real scout must reveal enemy markers.");
            Assert.IsTrue(match.World.Submit(new MoveCommand(1, new[] { 1 }, homeGround)).Accepted);
            for (int tick = 0; tick < 3000 && AnyEnemyVisible(match.World); tick++) match.World.Tick();
            Assert.IsFalse(AnyEnemyVisible(match.World));
            Assert.IsTrue(match.World.TryGetBuilding(101, out var hearth));
            Assert.IsTrue(match.World.Vision.IsExplored(1, hearth.Position));
            yield return Repaint(match);
            Assert.AreEqual(0, EnemyPixels(texture.GetPixels32()), "Explored terrain must not retain a live enemy-position marker.");
        }

        private static IEnumerator Repaint(MatchController match)
        {
            // Real unscaled time observes the production five-Hz paint cap; no test-only refresh bypass.
            yield return new WaitForSecondsRealtime(.22f);
            match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases();
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

        // Matches MinimapView's own Unknown fog colour; a paint without a Vision system must never use it.
        private static bool AnyUnexplored(Color32[] pixels)
        {
            foreach (var pixel in pixels) if (pixel.r == 10 && pixel.g == 19 && pixel.b == 24 && pixel.a == 255) return true;
            return false;
        }
        private static int EnemyPixels(Color32[] pixels)
        {
            int count = 0;
            foreach (var pixel in pixels) if (pixel.r > pixel.g * 2 && pixel.r > pixel.b * 2) count++;
            return count;
        }
        private static bool AnyEnemyVisible(World world)
        {
            foreach (var unit in world.Units) if (unit.OwnerId == 2 && world.Vision.IsEntityVisible(1, unit.Id)) return true;
            foreach (var building in world.Buildings) if (building.OwnerId == 2 && world.Vision.IsEntityVisible(1, building.Id)) return true;
            return false;
        }
        private static Vector2 ScreenPoint(MinimapView minimap, Vector2 uv)
        {
            var rect = minimap.Image.rectTransform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.min + Vector2.Scale(uv, rect.rect.size)));
        }
        private static void AssertCameraAt(MatchController match, SimPoint expected)
        {
            Assert.IsTrue(match.Rig.GroundPoint(new Vector2(Screen.width / 2f, Screen.height / 2f), out var actual));
            Assert.That(Vector3.Distance(DefinitionLoader.ToWorld(expected), actual), Is.LessThan(.02f));
        }
    }
}
