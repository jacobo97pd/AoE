using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The landscape stands still. The one piece of vegetation allowed to move is a tree somebody is currently
    /// cutting down, which is what makes the motion mean something instead of being ambient restlessness.
    /// </summary>
    public sealed class FellingMotionTests
    {
        private GameObject root;
        private MatchController match;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Felling motion"); root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Conquest;
            root.SetActive(true); match.enabled = false;
            yield return null;
            match.SyncPresentation(1);
        }
        [UnityTearDown] public IEnumerator TearDown() { if (root != null) Object.Destroy(root); yield return null; }

        private static float SwayOf(Transform visual)
        {
            if (visual == null) return -1;
            var block = new MaterialPropertyBlock();
            float strongest = 0;
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                block.Clear();
                renderer.GetPropertyBlock(block);
                strongest = Mathf.Max(strongest, block.GetFloat("_Sway"));
            }
            return strongest;
        }

        [UnityTest]
        public IEnumerator NothingInTheLandscapeSwaysOnItsOwn()
        {
            yield return null;
            var landscape = match.View.RootFor(0);
            Assert.IsNull(landscape, "Scenery is not an entity; this only guards against the lookup changing meaning.");
            // Every scenery prop the environment builds must be still: the sway property is never written now.
            var environment = GameObject.Find("World presentation");
            Assert.IsNotNull(environment);
            var block = new MaterialPropertyBlock();
            int checkedProps = 0;
            foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.name.StartsWith("Landscape")) continue;
                checkedProps++;
                block.Clear(); renderer.GetPropertyBlock(block);
                Assert.AreEqual(0, block.GetFloat("_Sway"), .0001f, renderer.name + " must stand still.");
            }
            Assert.Greater(checkedProps, 0, "The battlefield must actually carry scenery for this to mean anything.");
        }

        [Test]
        public void OnlyTheTreeBeingCutMoves()
        {
            int tree = 0;
            foreach (var resource in match.World.Resources)
                if (resource.Kind == ResourceKind.Wood) { tree = resource.Id; break; }
            Assert.AreNotEqual(0, tree, "The battlefield must ship a wood source.");

            int worker = 0;
            foreach (var unit in match.World.Units)
                if (unit.OwnerId == MatchController.LocalPlayer && unit.IsWorker) { worker = unit.Id; break; }
            Assert.AreNotEqual(0, worker);

            match.SyncPresentation(1);
            Assert.AreEqual(0, SwayOf(match.View.RootFor(tree)), .0001f, "An untouched tree stands still.");

            var order = match.World.Submit(new GatherCommand(MatchController.LocalPlayer, new[] { worker }, tree));
            Assert.IsTrue(order.Accepted, order.Message);
            bool cutting = false;
            for (int tick = 0; tick < World.TickRate * 120 && !cutting; tick++)
            {
                match.World.Tick();
                match.World.TryGetUnit(worker, out var state);
                cutting = state != null && state.WorkerTask == WorkerTask.Gathering && state.TargetResourceId == tree;
            }
            Assert.IsTrue(cutting, "The worker must actually reach the tree and start cutting.");
            match.SyncPresentation(1);
            Assert.Greater(SwayOf(match.View.RootFor(tree)), 0, "A tree with an axe in it moves.");

            Assert.IsTrue(match.World.Submit(new StopCommand(MatchController.LocalPlayer, new[] { worker })).Accepted);
            match.World.Tick();
            match.SyncPresentation(1);
            Assert.AreEqual(0, SwayOf(match.View.RootFor(tree)), .0001f, "It stops the moment the cutting does.");
        }
    }
}
