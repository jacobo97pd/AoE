using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    public sealed class CameraRotationTests
    {
        private GameObject holder;
        private Camera camera;

        [TearDown]
        public void Cleanup() { if (holder != null) Object.DestroyImmediate(holder); }

        private RtsCamera Create(Vector2 mapSize, float aspect, bool allowMapEdgeFocus = false)
        {
            holder = new GameObject("Camera rotation test", typeof(Camera)); camera = holder.GetComponent<Camera>();
            camera.aspect = aspect;
            return new RtsCamera(camera, mapSize, allowMapEdgeFocus);
        }

        [TestCase(1.7777778f)]
        [TestCase(.75f)]
        public void NavalEdgeFocusKeepsOuterCoastShipsCentredThroughRotationAndZoom(float aspect)
        {
            var rig = Create(new Vector2(144, 112), aspect, true);
            foreach (var ship in new[] { new Vector3(.5f, 0, .5f), new Vector3(143.5f, 0, .5f),
                new Vector3(.5f, 0, 111.5f), new Vector3(143.5f, 0, 111.5f) })
            {
                rig.Focus(ship);
                for (int turn = 0; turn < 4; turn++)
                {
                    rig.Rotate(90); rig.Zoom(turn % 2 == 0 ? 1.2f : 1 / 1.2f);
                    var screen = camera.WorldToViewportPoint(ship);
                    Assert.That(screen.x, Is.EqualTo(.5f).Within(.0001f), "A ship at the map edge remains focusable.");
                    Assert.That(screen.y, Is.EqualTo(.5f).Within(.0001f), "The coastline cannot push a ship under the HUD.");
                }
            }
            rig.Focus(new Vector3(-50, 0, -50));
            Assert.IsTrue(rig.GroundPoint(new Vector2(camera.pixelWidth * .5f, camera.pixelHeight * .5f), out var focus));
            Assert.That(focus.x, Is.EqualTo(0).Within(.002f));
            Assert.That(focus.z, Is.EqualTo(0).Within(.002f), "Naval focus itself still stays inside the map.");
        }

        [TestCase(96, 72, 1.7777778f)]
        [TestCase(104, 80, 1.3333333f)]
        [TestCase(72, 96, .75f)]
        public void QuarterTurnsKeepGroundFootprintLegalAtEveryMapCorner(int width, int height, float aspect)
        {
            var mapSize = new Vector2(width, height); var rig = Create(mapSize, aspect);
            rig.SetHome(new Vector3(width * .5f, 0, height * .5f), 20);
            var plane = new Plane(Vector3.up, Vector3.zero);
            foreach (var corner in new[] { Vector3.zero, new Vector3(width, 0, 0), new Vector3(0, 0, height), new Vector3(width, 0, height) })
            {
                rig.Focus(corner);
                for (int turn = 0; turn < 4; turn++)
                {
                    float previousYaw = camera.transform.eulerAngles.y; rig.Rotate(90);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(previousYaw + 90, camera.transform.eulerAngles.y)), Is.LessThan(.001f));
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(55, camera.transform.eulerAngles.x)), Is.LessThan(.001f));
                    for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
                    {
                        var ray = camera.ViewportPointToRay(new Vector3(x, y)); Assert.IsTrue(plane.Raycast(ray, out float distance));
                        var ground = ray.GetPoint(distance);
                        Assert.That(ground.x, Is.InRange(-.002f, width + .002f));
                        Assert.That(ground.z, Is.InRange(-.002f, height + .002f));
                    }
                }
            }
        }

        [Test]
        public void FourQuarterTurnsRestoreCentralGroundProjectionAndZoom()
        {
            var rig = Create(new Vector2(144, 120), 1.7777778f); rig.SetHome(new Vector3(72, 0, 60), 10);
            var screen = new Vector2(camera.pixelWidth * .39f, camera.pixelHeight * .64f);
            Assert.IsTrue(rig.GroundPoint(screen, out var before));
            float zoom = camera.orthographicSize; var position = camera.transform.position;
            for (int turn = 0; turn < 4; turn++) rig.Rotate(90);
            Assert.IsTrue(rig.GroundPoint(screen, out var after));
            Assert.That(Vector3.Distance(before, after), Is.LessThan(.002f));
            Assert.That(Vector3.Distance(position, camera.transform.position), Is.LessThan(.002f));
            Assert.AreEqual(zoom, camera.orthographicSize);
        }

        [Test]
        public void GroundAnchorAndReversePanRemainAccurateAfterAnObliqueRotation()
        {
            var rig = Create(new Vector2(144, 120), 1.3333333f); rig.SetHome(new Vector3(72, 0, 60), 10); rig.Rotate(137);
            var from = new Vector2(camera.pixelWidth * .43f, camera.pixelHeight * .47f);
            var to = new Vector2(camera.pixelWidth * .57f, camera.pixelHeight * .58f);
            Assert.IsTrue(rig.GroundPoint(from, out var anchor)); var position = camera.transform.position;
            rig.Pan(from, to);
            Assert.IsTrue(rig.GroundPoint(to, out var movedAnchor));
            Assert.That(Vector3.Distance(anchor, movedAnchor), Is.LessThan(.002f));
            rig.Pan(to, from);
            Assert.IsTrue(rig.GroundPoint(from, out var restoredAnchor));
            Assert.That(Vector3.Distance(anchor, restoredAnchor), Is.LessThan(.002f));
            Assert.That(Vector3.Distance(position, camera.transform.position), Is.LessThan(.002f));
        }
    }
}
