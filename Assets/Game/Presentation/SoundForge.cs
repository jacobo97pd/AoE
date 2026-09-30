using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Original sound, synthesised from scratch. The project ships no recordings, so every axe stroke, bell
    /// and breath of wind here is built out of noise, decaying partials and plucked strings. Generation is
    /// deterministic: the same seed yields the same waveform on every machine and every run.
    /// </summary>
    public static class SoundForge
    {
        public const int Rate = 22050;

        public static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * Rate))];

        public static uint Seed(int value) => (uint)(value * 747796405 + 2891336453) | 1;

        public static float Next(ref uint seed)
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            return (seed & 0xFFFF) / 32767.5f - 1;
        }

        /// <summary>Noise through a one-pole low pass: the raw material for wind, surf, rustle and gravel.</summary>
        public static void Noise(float[] data, float start, float seconds, float gain, float cutoffHz, ref uint seed)
        {
            int from = Index(start), to = Mathf.Min(data.Length, from + Mathf.RoundToInt(seconds * Rate));
            float coefficient = Coefficient(cutoffHz), state = 0;
            for (int i = from; i < to; i++)
            {
                state += (Next(ref seed) - state) * coefficient;
                data[i] += state * gain;
            }
        }

        /// <summary>A sine that decays away, the body of every knock, thud, chime and horn here.</summary>
        public static void Partial(float[] data, float start, float hz, float gain, float decaySeconds, float bendPerSecond = 0)
        {
            int from = Index(start);
            float phase = 0;
            for (int i = from; i < data.Length; i++)
            {
                float age = (i - from) / (float)Rate;
                float amplitude = gain * Mathf.Exp(-age / Mathf.Max(.001f, decaySeconds));
                if (amplitude < .0002f) return;
                phase += 2 * Mathf.PI * Mathf.Max(20, hz + bendPerSecond * age) / Rate;
                data[i] += Mathf.Sin(phase) * amplitude;
            }
        }

        /// <summary>
        /// Karplus-Strong: a burst of noise chased around a delay line the length of one period, damped a
        /// little on every lap. It is the cheapest honest plucked string there is, and it carries the music.
        /// </summary>
        public static void Pluck(float[] data, float start, float hz, float gain, float damping, ref uint seed)
        {
            int from = Index(start), period = Mathf.Max(2, Mathf.RoundToInt(Rate / Mathf.Max(20, hz)));
            var line = new float[period];
            for (int i = 0; i < period; i++) line[i] = Next(ref seed);
            float amplitude = gain;
            for (int i = from, cursor = 0; i < data.Length && amplitude > .0002f; i++, cursor++)
            {
                int index = cursor % period, next = (cursor + 1) % period;
                float value = line[index];
                line[index] = (value + line[next]) * .5f * damping;
                data[i] += value * amplitude;
                amplitude *= .99994f;
            }
        }

        /// <summary>Inharmonic partials: what separates a bell or a struck rock from a plain tone.</summary>
        public static void Bell(float[] data, float start, float hz, float gain, float decaySeconds)
        {
            float[] ratios = { 1f, 2.76f, 5.40f, 8.93f, 13.34f };
            float[] weights = { 1f, .58f, .34f, .18f, .09f };
            for (int i = 0; i < ratios.Length; i++)
                Partial(data, start, hz * ratios[i], gain * weights[i], decaySeconds / (1 + i * .8f));
        }

        public static void LowPass(float[] data, float cutoffHz)
        {
            float coefficient = Coefficient(cutoffHz), state = 0;
            for (int i = 0; i < data.Length; i++) { state += (data[i] - state) * coefficient; data[i] = state; }
        }

        public static void HighPass(float[] data, float cutoffHz)
        {
            float coefficient = Coefficient(cutoffHz), state = 0;
            for (int i = 0; i < data.Length; i++) { state += (data[i] - state) * coefficient; data[i] -= state; }
        }

        /// <summary>Comb delays for a room around the sound; a dry chime in the open reads as a phone alert.</summary>
        public static void Reverb(float[] data, float amount, float decay = .55f)
        {
            int[] taps = { 1231, 1597, 2069, 2837 };
            foreach (int tap in taps)
            {
                if (tap >= data.Length) continue;
                for (int i = tap; i < data.Length; i++) data[i] += data[i - tap] * decay * amount * .25f;
            }
        }

        public static void Fade(float[] data, float attackSeconds, float releaseSeconds)
        {
            int attack = Mathf.Max(1, Index(attackSeconds)), release = Mathf.Max(1, Index(releaseSeconds));
            for (int i = 0; i < data.Length; i++)
            {
                float gain = Mathf.Min(1, i / (float)attack);
                int remaining = data.Length - 1 - i;
                if (remaining < release) gain *= remaining / (float)release;
                data[i] *= gain;
            }
        }

        /// <summary>
        /// Folds the tail back over the head and returns the shortened buffer, so a bed can loop for an hour
        /// with no seam to hear. Write the source a crossfade longer than the loop you want.
        /// </summary>
        public static float[] Loop(float[] data, float crossfadeSeconds)
        {
            int fade = Mathf.Clamp(Index(crossfadeSeconds), 1, data.Length / 3);
            int length = data.Length - fade;
            var result = new float[length];
            System.Array.Copy(data, result, length);
            for (int i = 0; i < fade; i++)
            {
                float blend = i / (float)fade;
                result[i] = data[i] * blend + data[length + i] * (1 - blend);
            }
            return result;
        }

        public static void Normalize(float[] data, float peak)
        {
            float loudest = 0;
            foreach (float sample in data) loudest = Mathf.Max(loudest, Mathf.Abs(sample));
            if (loudest < .0001f) return;
            float scale = peak / loudest;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * scale, -1, 1);
        }

        /// <summary>Soft saturation: keeps a loud transient from clipping into a click.</summary>
        public static void Soften(float[] data, float drive = 1.3f)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Tanh(data[i] * drive) / Mathf.Max(.001f, (float)System.Math.Tanh(drive));
        }

        public static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static int Index(float seconds) => Mathf.Max(0, Mathf.RoundToInt(seconds * Rate));
        private static float Coefficient(float cutoffHz) => Mathf.Clamp01(1 - Mathf.Exp(-2 * Mathf.PI * Mathf.Max(1, cutoffHz) / Rate));
    }
}
