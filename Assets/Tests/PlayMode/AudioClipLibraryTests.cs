using System.Reflection;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The CC0 clip library added on top of the game's synthesis (docs/art/audio.md): the cues the cue map
    /// promises a real recording for actually resolve to one, the ones it deliberately leaves synthesised stay
    /// that way, the voice pack never speaks its modern "objective achieved" line, and none of it costs a
    /// per-frame allocation once its clips are cached.
    /// </summary>
    public sealed class AudioClipLibraryTests
    {
        private static AudioSource[] Voices(SliceFeedback feedback) =>
            (AudioSource[])typeof(SliceFeedback).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback);

        [Test]
        public void CuesTheMapPromisesARealClipForResolveToOne()
        {
            foreach (var cue in new[] { FeedbackCue.Chop, FeedbackCue.Mine, FeedbackCue.Complete, FeedbackCue.Impact, FeedbackCue.Objective })
                Assert.IsNotNull(FrontierClips.Real(cue), cue + " is catalogued in docs/art/audio.md as having a real clip.");
        }

        [Test]
        public void CuesTheMapKeepsSynthesisedHaveNoRealClip()
        {
            // Order's real layer is the voiced acknowledgement (a second, separate voice), never a swap-in for
            // the pluck itself, so FrontierClips.Real must stay null for it too.
            foreach (var cue in new[] { FeedbackCue.Order, FeedbackCue.Gather, FeedbackCue.Defeat })
                Assert.IsNull(FrontierClips.Real(cue), cue + " is documented to stay purely synthesised.");
        }

        [Test]
        public void TheOrderVoiceNeverSpeaksTheModernObjectiveLine()
        {
            var forbidden = new[]
            {
                Resources.Load<AudioClip>("Audio/Units/Acknowledgement/unit_ack_m_objective_achieved"),
                Resources.Load<AudioClip>("Audio/Units/Acknowledgement/unit_ack_f_objective_achieved"),
            };
            Assert.IsNotNull(forbidden[0], "The clip must actually exist to be a meaningful exclusion check.");
            bool sawAClip = false;
            for (int i = 0; i < 200; i++)
            {
                var line = FrontierClips.OrderVoice();
                if (line == null) continue;
                sawAClip = true;
                Assert.That(forbidden, Has.No.Member(line), "The pack's modern-sounding line must never be picked.");
            }
            Assert.IsTrue(sawAClip, "The voice pack must actually be loaded for this check to mean anything.");
        }

        [Test]
        public void ImpactAndMineVaryWhichRealClipTheyPick()
        {
            // Not a statistical claim, just a floor against a copy-paste bug that always returns index 0.
            var seenMine = new System.Collections.Generic.HashSet<string>();
            var seenImpact = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                seenMine.Add(FrontierClips.Real(FeedbackCue.Mine).name);
                seenImpact.Add(FrontierClips.Real(FeedbackCue.Impact).name);
            }
            Assert.That(seenMine.Count, Is.GreaterThan(1), "200 draws from 5 variants must show more than one.");
            Assert.That(seenImpact.Count, Is.GreaterThan(1), "200 draws from 9 variants must show more than one.");
        }

        [Test]
        public void SliceFeedbackSwapsTheRealClipInAndJittersItsPitch()
        {
            var holder = new GameObject("Feedback clip swap", typeof(Camera));
            try
            {
                var camera = holder.GetComponent<Camera>();
                holder.transform.position = new Vector3(0, 0, -10); holder.transform.LookAt(Vector3.zero);
                var feedback = new SliceFeedback(holder.transform, camera);
                bool wasMuted = SliceFeedback.Muted; if (wasMuted) feedback.ToggleMute();
                try
                {
                    feedback.Emit(FeedbackCue.Chop, Vector3.zero);
                    var voice = Voices(feedback)[0];
                    Assert.IsNotNull(voice.clip);
                    Assert.AreEqual("economy_chop_01", voice.clip.name, "Chop must play the cataloged real clip, not the synth.");
                    Assert.That(voice.pitch, Is.InRange(.95f, 1.05f));
                }
                finally { if (SliceFeedback.Muted != wasMuted) feedback.ToggleMute(); feedback.Dispose(); }
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void SliceFeedbackKeepsSynthesisForACueWithNoRealClip()
        {
            var holder = new GameObject("Feedback synth kept", typeof(Camera));
            try
            {
                var camera = holder.GetComponent<Camera>();
                holder.transform.position = new Vector3(0, 0, -10); holder.transform.LookAt(Vector3.zero);
                var feedback = new SliceFeedback(holder.transform, camera);
                bool wasMuted = SliceFeedback.Muted; if (wasMuted) feedback.ToggleMute();
                try
                {
                    feedback.Emit(FeedbackCue.Defeat, Vector3.zero);
                    var voice = Voices(feedback)[0];
                    Assert.IsNotNull(voice.clip);
                    StringAssert.StartsWith("Original", voice.clip.name, "Defeat has no real clip, so it must keep FrontierSounds' synthesis.");
                    Assert.AreEqual(1f, voice.pitch, "A purely synthesised cue must not be pitch-shifted.");
                }
                finally { if (SliceFeedback.Muted != wasMuted) feedback.ToggleMute(); feedback.Dispose(); }
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void MutingSuppressesTheRealClipLayerToo()
        {
            var holder = new GameObject("Feedback mute", typeof(Camera));
            try
            {
                var camera = holder.GetComponent<Camera>();
                holder.transform.position = new Vector3(0, 0, -10); holder.transform.LookAt(Vector3.zero);
                var feedback = new SliceFeedback(holder.transform, camera);
                bool wasMuted = SliceFeedback.Muted; if (!SliceFeedback.Muted) feedback.ToggleMute();
                try
                {
                    int before = feedback.PlayedSounds;
                    feedback.Emit(FeedbackCue.Mine, Vector3.zero);
                    Assert.AreEqual(before, feedback.PlayedSounds, "Muted feedback must not start a voice, real clip or synth.");
                }
                finally { if (SliceFeedback.Muted != wasMuted) feedback.ToggleMute(); feedback.Dispose(); }
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void UiSoundRespectsTheSharedMuteAndVariesItsClip()
        {
            var host = new GameObject("Ui sound mute host");
            var cameraHost = new GameObject("Ui sound camera", typeof(Camera));
            bool wasMuted = SliceFeedback.Muted;
            var feedback = new SliceFeedback(host.transform, cameraHost.GetComponent<Camera>());
            try
            {
                if (!SliceFeedback.Muted) feedback.ToggleMute();
                int before = UiSound.PlayedSounds;
                UiSound.Play(UiCue.Click);
                Assert.AreEqual(before, UiSound.PlayedSounds, "UiSound must honour the same mute switch as every other sound.");
                if (SliceFeedback.Muted != wasMuted) feedback.ToggleMute();
                UiSound.Play(UiCue.Click);
                Assert.That(UiSound.PlayedSounds, Is.GreaterThan(before), "Unmuted clicks must actually start a voice.");
            }
            finally { if (SliceFeedback.Muted != wasMuted) feedback.ToggleMute(); feedback.Dispose(); Object.DestroyImmediate(host); Object.DestroyImmediate(cameraHost); }
        }

        [Test]
        public void NeitherFrontierClipsNorUiSoundAllocateOnceTheirClipsAreCached()
        {
            // Warm every cache first: the first call to each is where Resources.Load and the backing arrays happen.
            FrontierClips.Real(FeedbackCue.Mine); FrontierClips.Real(FeedbackCue.Impact); FrontierClips.OrderVoice();
            UiSound.Play(UiCue.Click); UiSound.Play(UiCue.Confirm);
            Assert.That(() => { FrontierClips.Real(FeedbackCue.Mine); FrontierClips.Real(FeedbackCue.Impact); FrontierClips.OrderVoice(); }, AllocatesNothing());
            Assert.That(() => UiSound.Play(UiCue.Click), AllocatesNothing());
        }

        private static NUnit.Framework.Constraints.IResolveConstraint AllocatesNothing() => UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);
    }
}
