using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public class GreyboxIntegrationTests
    {
        private GameObject root;
        [UnityTearDown] public IEnumerator Cleanup() { if (root != null) Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator TapSelectAndTerrainTapMoveThroughPresentation()
        {
            root = new GameObject("Integration match");
            var match = root.AddComponent<MatchController>();
            yield return null;
            var worker = match.World.Units[0];
            var point = match.Rig.Camera.WorldToScreenPoint(match.View.RootFor(worker.Id).position + Vector3.up * .6f);
            match.Tap(point, false);
            CollectionAssert.Contains(match.Selection, worker.Id);
            var start = worker.Position;
            var target = new Vector3(11.5f, 0, 19.5f);
            match.Tap(match.Rig.Camera.WorldToScreenPoint(target), false);
            yield return new WaitForSeconds(.3f);
            Assert.AreNotEqual(start, worker.Position, "An accepted terrain tap must move simulation state.");
            Assert.That(Vector3.Distance(match.View.RootFor(worker.Id).position, DefinitionLoader.ToWorld(worker.Position)), Is.LessThan(.3f));
            Assert.That(match.View.Count, Is.EqualTo(match.World.Units.Count + match.World.Buildings.Count + match.World.Resources.Count));
        }

        [UnityTest]
        public IEnumerator EnemyCannotBeSelectedAndStopDoesNotMove()
        {
            root = new GameObject("Integration match");
            var match = root.AddComponent<MatchController>();
            yield return null;
            match.Select(new[] { 5 });
            Assert.That(match.Selection, Is.Empty);
            match.Select(new[] { 1 });
            match.Issue(new MoveCommand(1, new[] { 1 }, new SimPoint(11500, 19500)), "Move");
            yield return new WaitForSeconds(.1f);
            match.StopSelected();
            match.World.TryGetUnit(1, out var worker);
            var position = worker.Position;
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(position, worker.Position);
        }

        [Test]
        public void PanDoesNotTurnIntoLongPressSelection()
        {
            var gesture = new GestureTracker(); gesture.Begin(Vector2.zero, 0);
            Assert.AreEqual(GestureKind.Pan, gesture.Move(new Vector2(25, 0), .1f));
            Assert.AreEqual(GestureKind.Pan, gesture.End(new Vector2(70, 0), 1));
        }

        [Test]
        public void HoldThenDragSelectsAndCancelNeverTaps()
        {
            var gesture = new GestureTracker(); gesture.Begin(Vector2.zero, 0);
            Assert.AreEqual(GestureKind.BoxSelection, gesture.Move(new Vector2(30, 0), .5f));
            gesture.Cancel();
            Assert.AreEqual(GestureKind.None, gesture.End(new Vector2(30, 0), .6f));
        }

        [UnityTest]
        public IEnumerator CameraGroundCornersStayOnMapAtZoomExtremes()
        {
            root = new GameObject("Camera test"); var camera = root.AddComponent<Camera>();
            var rig = new RtsCamera(camera, new Vector2(64, 48));
            foreach (float aspect in new[] { 4f / 3, 20f / 9, 1f })
            {
                camera.aspect = aspect;
                rig.Zoom(100); rig.Focus(new Vector3(-100, 0, 1000));
                var plane = new Plane(Vector3.up, Vector3.zero);
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(x, y));
                    Assert.IsTrue(plane.Raycast(ray, out float distance));
                    var p = ray.GetPoint(distance);
                    Assert.That(p.x, Is.InRange(-.02f, 64.02f)); Assert.That(p.z, Is.InRange(-.02f, 48.02f));
                }
            }
            yield return null;
        }
    }
}
