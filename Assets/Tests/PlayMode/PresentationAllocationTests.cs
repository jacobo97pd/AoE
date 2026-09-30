using System.Collections;
using Emberfield.Localization;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The parts of a presentation frame that run whatever happens on screen allocate nothing while nothing changes:
    /// the fog redraws from one read of the map, and a world label is translated when it is written instead of being
    /// read back every frame.
    /// </summary>
    public sealed class PresentationAllocationTests
    {
        private GameObject root;
        private WorldView view;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UiLocalization.Enabled = false; UiLocalization.SetLanguage(GameLanguage.Spanish);
            view?.Dispose(); view = null;
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFogRedrawsFromOneReadOfTheMapWithoutAllocating()
        {
            root = new GameObject("Fog allocation");
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            var world = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Dominion);
            view = new WorldView(world, root.transform, camera);
            UnitState scout = null;
            foreach (var unit in world.Units) if (unit.OwnerId == MatchController.LocalPlayer) { scout = unit; break; }
            Assert.IsNotNull(scout);
            // Towards the middle of the map, to the nearest open cell there.
            int width = world.Map.WidthCells, height = world.Map.HeightCells, size = world.Map.CellSizeMillimetres;
            var destination = new SimPoint(-1, -1);
            for (int ring = 0; ring < width && destination.X < 0; ring++)
                for (int dz = -ring; dz <= ring && destination.X < 0; dz++) for (int dx = -ring; dx <= ring && destination.X < 0; dx++)
                {
                    var point = new SimPoint((width / 2 + dx) * size + size / 2, (height / 2 + dz) * size + size / 2);
                    if (world.IsWalkable(point)) destination = point;
                }
            Assert.IsTrue(world.Submit(new MoveCommand(MatchController.LocalPlayer, new[] { scout.Id }, destination)).Accepted);
            view.Sync(1, new int[0]);
            long revision = world.Vision.Revision;
            for (int tick = 0; tick < 200 && world.Vision.Revision == revision; tick++) world.Tick();
            Assert.That(world.Vision.Revision, Is.Not.EqualTo(revision), "The scout's walk changes what the local player sees.");
            // The redraw itself, on a new revision: one read of the map into the fog's own arrays.
            Assert.That(() => view.Fog.Sync(), AllocatesNothing());
            var cells = view.Fog.Cells;
            for (int z = 0, index = 0; z < height; z++) for (int x = 0; x < width; x++, index++)
            {
                var point = new SimPoint(x * size + size / 2, z * size + size / 2);
                int expected = (world.Vision.IsVisible(1, point) ? 2 : 0) | (world.Vision.IsExplored(1, point) ? 1 : 0);
                if (cells[index] != expected) Assert.Fail("The fog's cell " + x + "," + z + " is not what the fog of war says.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AWorldLabelIsTranslatedWhenWrittenAndFollowsTheLanguage()
        {
            root = new GameObject("World label");
            var label = root.AddComponent<TextMesh>();
            UiLocalization.Enabled = true; UiLocalization.SetLanguage(GameLanguage.Spanish);
            const string source = "YOURS\nBeacon";
            UiLocalization.SetText(label, source);
            Assert.AreEqual("TUYO\nFaro", label.text);
            yield return null;
            Assert.AreEqual("TUYO\nFaro", label.text, "A frame later it still reads in Spanish.");
            UiLocalization.SetLanguage(GameLanguage.English);
            UiLocalization.ApplyNow();
            Assert.AreEqual(UiLocalization.Translate(source), label.text);
            Assert.AreNotEqual("TUYO\nFaro", label.text);
        }

        private static NUnit.Framework.Constraints.IResolveConstraint AllocatesNothing() => UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);
    }
}
