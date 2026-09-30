using System;
using Emberfield.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    // The game ships no recordings: every sound is synthesised. These cases hold that synthesis to the
    // properties a player would notice if they broke — audible, distinct, in range, and seamless when looped.
    public sealed class FrontierSoundTests
    {
        [Test]
        public void EverySoundIsAudibleInRangeAndIdenticalOnEveryRun()
        {
            foreach (FeedbackCue cue in Enum.GetValues(typeof(FeedbackCue)))
            {
                var first = FrontierSounds.Cue(cue);
                var second = FrontierSounds.Cue(cue);
                try
                {
                    Assert.IsNotNull(first, cue.ToString());
                    Assert.AreEqual(1, first.channels); Assert.AreEqual(SoundForge.Rate, first.frequency);
                    Assert.That(first.length, Is.InRange(.05f, 3f), cue + " lasts a plausible time.");
                    var a = Samples(first); var b = Samples(second);
                    Assert.That(Peak(a), Is.InRange(.2f, 1f), cue + " is audible without clipping.");
                    // A floor against silence and against a degenerate one-sample click, not a loudness
                    // target: the order cue is deliberately the quietest thing in the game, since it answers
                    // every command given.
                    Assert.That(Energy(a), Is.GreaterThan(.0015f), cue + " carries real signal, not a click.");
                    CollectionAssert.AreEqual(a, b, cue + " must synthesise identically on every run.");
                }
                finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
            }
        }

        [Test]
        public void TheCuesOfTheEconomyAndOfCombatDoNotSoundAlike()
        {
            var chop = FrontierSounds.Cue(FeedbackCue.Chop);
            var mine = FrontierSounds.Cue(FeedbackCue.Mine);
            var impact = FrontierSounds.Cue(FeedbackCue.Impact);
            try
            {
                // Brightness separates them: wood is dull, stone rings, a blow lands low and heavy.
                float wood = Brightness(Samples(chop)), stone = Brightness(Samples(mine)), blow = Brightness(Samples(impact));
                Assert.That(stone, Is.GreaterThan(wood * 1.15f), "A pick must read brighter than an axe.");
                Assert.That(blow, Is.LessThan(stone), "A blow landing must read heavier than a pick.");
            }
            finally { Object.DestroyImmediate(chop); Object.DestroyImmediate(mine); Object.DestroyImmediate(impact); }
        }

        [Test]
        public void ALoopedBedHasNoSeamToHear()
        {
            var source = SoundForge.Buffer(4);
            uint seed = SoundForge.Seed(5);
            SoundForge.Noise(source, 0, 4, 1, 900, ref seed);
            var looped = SoundForge.Loop(source, .7f);
            Assert.That(looped.Length, Is.LessThan(source.Length), "The crossfade is taken out of the length.");
            float seam = Mathf.Abs(looped[0] - looped[looped.Length - 1]);
            float ordinary = 0;
            for (int i = 1; i < looped.Length; i++) ordinary += Mathf.Abs(looped[i] - looped[i - 1]);
            ordinary /= looped.Length - 1;
            Assert.That(seam, Is.LessThan(Mathf.Max(.002f, ordinary * 8)), "The wrap must not step louder than the material itself.");
        }

        [Test]
        public void SilenceIsRespectedWhenSoundIsTurnedOff()
        {
            var holder = new GameObject("Muted feedback", typeof(Camera));
            try
            {
                var feedback = new SliceFeedback(holder.transform, holder.GetComponent<Camera>());
                bool wasMuted = SliceFeedback.Muted;
                if (!SliceFeedback.Muted) feedback.ToggleMute();
                int before = feedback.PlayedSounds;
                feedback.Emit(FeedbackCue.Chop, holder.transform.position);
                Assert.AreEqual(before, feedback.PlayedSounds, "Muted feedback must not start a voice.");
                if (SliceFeedback.Muted != wasMuted) feedback.ToggleMute();
                feedback.Dispose();
            }
            finally { Object.DestroyImmediate(holder); }
        }

        private static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            return data;
        }
        private static float Peak(float[] data)
        {
            float peak = 0; foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample)); return peak;
        }
        private static float Energy(float[] data)
        {
            double total = 0; foreach (float sample in data) total += sample * sample; return (float)(total / data.Length);
        }
        /// <summary>Mean absolute change between samples: a cheap, honest stand-in for how bright a sound is.</summary>
        private static float Brightness(float[] data)
        {
            double total = 0;
            for (int i = 1; i < data.Length; i++) total += Mathf.Abs(data[i] - data[i - 1]);
            return (float)(total / Mathf.Max(1, data.Length - 1));
        }
    }
}
