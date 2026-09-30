using System.Collections;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Emberfield.Tests.PlayMode
{
    public sealed class ReferenceCharacterNavigationTests
    {
        private Scene previous, fixture;

        [TestCase("queued")]
        [TestCase("lobby")]
        [TestCase("starting")]
        [TestCase("active")]
        [TestCase("unrecognized-future-state")]
        public void GalleryCannotSuspendAnOnlineSession(string status)
        { Assert.That(ReferenceCharacterReview.CanOpen(new OnlineState { status = status }), Is.False); }

        [Test]
        public void PendingOnlineOperationCannotRaceWithGallerySuspension()
        {
            Assert.That(ReferenceCharacterReview.CanOpen(null, true), Is.False);
            Assert.That(ReferenceCharacterReview.CanOpen(new OnlineState { status = "idle" }, true), Is.False);
            Assert.That(ReferenceCharacterReview.CanOpen(null), Is.True);
            Assert.That(ReferenceCharacterReview.CanOpen(new OnlineState { status = "idle" }), Is.True);
        }

        [UnityTest]
        public IEnumerator VisitingCollectionPreservesTheSamePausedOfflineWorldAndHomeScreen()
        {
            previous = SceneManager.GetActiveScene(); fixture = SceneManager.CreateScene("Reference navigation fixture"); SceneManager.SetActiveScene(fixture);
            var root = new GameObject("Retained offline match"); root.SetActive(false);
            var match = root.AddComponent<MatchController>(); match.InitialOfflineFactionId = "aven"; root.SetActive(true);
            yield return null; match.Shell.Open();
            var gamePipeline = QualitySettings.renderPipeline;
            var world = match.World; long tick = world.TickIndex;
            Assert.That(world.IsNetworkReplica, Is.False);
            ReferenceCharacterReview.OpenFromGame();
            float deadline = Time.realtimeSinceStartup + 15;
            while (root.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(root, Is.Not.Null); Assert.That(root.activeSelf, Is.False, "Suspend the root; never destroy the ongoing match.");
            Assert.That(match.World, Is.SameAs(world)); Assert.That(world.TickIndex, Is.EqualTo(tick));
            var scene = SceneManager.GetSceneByName(ReferenceCharacterReview.SceneName);
            Assert.That(scene.isLoaded, Is.True);
            ReferenceCharacterReview viewer = null;
            foreach (var item in scene.GetRootGameObjects()) if (item.TryGetComponent<ReferenceCharacterReview>(out var found)) viewer = found;
            Assert.That(viewer, Is.Not.Null);
            Assert.That(viewer.Pipeline, Is.Not.Null);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(viewer.Pipeline));
            viewer.ReturnToGame();
            deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetSceneByName(ReferenceCharacterReview.SceneName).isLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(root.activeSelf, Is.True); Assert.That(match.World, Is.SameAs(world)); Assert.That(world.TickIndex, Is.EqualTo(tick));
            Assert.That(match.Shell.IsOpen, Is.True); Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(fixture));
            Assert.That(SceneManager.GetSceneByName(ReferenceCharacterReview.SceneName).isLoaded, Is.False);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(gamePipeline), "Leaving the gallery restores the game's renderer.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            var collection = SceneManager.GetSceneByName(ReferenceCharacterReview.SceneName);
            if (collection.isLoaded) yield return SceneManager.UnloadSceneAsync(collection);
            if (fixture.IsValid() && fixture.isLoaded) yield return SceneManager.UnloadSceneAsync(fixture);
        }
    }
}
