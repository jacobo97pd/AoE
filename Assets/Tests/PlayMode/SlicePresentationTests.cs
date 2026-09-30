using System.Collections;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Emberfield.Tests
{
    public sealed class SlicePresentationTests
    {
        [UnityTest]
        public IEnumerator ArtMotionAndFeedbackNeverAdvanceGameplayAndOffscreenEventsStaySilent()
        {
            var root = new GameObject("Slice integration"); root.SetActive(false);
            var match = root.AddComponent<MatchController>(); match.InitialOfflineFactionId = "aven";
            root.SetActive(true); match.enabled = false; yield return null;
            bool initialMute = SliceFeedback.Muted;
            try
            {
                Assert.IsNotNull(match.View.RootFor(1).GetComponentInChildren<LODGroup>(), "The actual match must bind the baked Aven worker.");
                // Walk towards the middle of the map rather than to a fixed coordinate: the battlefields were
                // rebaked larger, and the old destination had become close enough to reach inside ten ticks,
                // which left the worker standing still and the walk cycle below with nothing to assert.
                var map = match.World.Map;
                var across = new SimPoint(map.WidthCells * map.CellSizeMillimetres / 2, map.HeightCells * map.CellSizeMillimetres / 2);
                var order = match.World.Submit(new MoveCommand(1, new[] { 1 }, across));
                Assert.IsTrue(order.Accepted, order.Message);
                for (int i = 0; i < 10; i++) match.World.Tick();
                match.World.TryGetUnit(1, out var worker); match.World.TryGetPlayer(1, out var player);
                Assert.AreEqual(UnitOrder.Moving, worker.Order, "The walk cycle is only asserted for a worker that is actually walking.");
                var position = worker.Position; int health = worker.Health; var stock = player.Resources; long tick = match.World.TickIndex;
                for (int i = 0; i < 25; i++) match.SyncPresentation(i / 25f);
                Assert.AreEqual(position, worker.Position); Assert.AreEqual(health, worker.Health);
                Assert.AreEqual(stock, player.Resources); Assert.AreEqual(tick, match.World.TickIndex);
                var rendered = match.View.RootFor(1);
                // A worker drawn by a rigged character (the Meshy kingdom worker) walks with its clip, and its model
                // offset is zeroed on purpose; only the procedural figure bobs its model while walking.
                var character = rendered.GetComponentInChildren<CorsairAnimationDriver>();
                if (character != null)
                    Assert.That(character.CurrentState, Is.EqualTo("Walk").Or.EqualTo("Run"), "The walking worker's character must play its gait.");
                else
                {
                    var model = rendered.Find("Model"); Assert.That(model.localPosition.y, Is.GreaterThan(0).And.LessThan(.06f));
                }
                var feedback = match.View.Feedback; int particles = feedback.EmittedParticles, sounds = feedback.PlayedSounds;
                feedback.Emit(FeedbackCue.Impact, new Vector3(-10000, 0, -10000));
                Assert.AreEqual(particles, feedback.EmittedParticles); Assert.AreEqual(sounds, feedback.PlayedSounds);
                bool initiallyMuted = SliceFeedback.Muted;
                if (!initiallyMuted) feedback.ToggleMute();
                feedback.Emit(FeedbackCue.Impact, match.View.RootFor(1).position);
                Assert.AreEqual(sounds, feedback.PlayedSounds, "Muting must suppress new voice playback.");
                Assert.Greater(feedback.EmittedParticles, particles, "Muting retains visual feedback.");
                if (!initiallyMuted) feedback.ToggleMute();
                Assert.LessOrEqual(feedback.ActiveParticles, SliceFeedback.ParticleCapacity);
            }
            finally { if (SliceFeedback.Muted != initialMute) match.View.Feedback.ToggleMute(); Object.Destroy(root); }
            yield return null;
        }
    }
}
