using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// What the map sounds like when nothing is happening: a wind bed with the life of its own biome over it,
    /// and a quiet score that comes and goes rather than looping in your ear. Everything is synthesised here,
    /// and every clip is cached for the process, so a second match costs nothing to score.
    /// </summary>
    public sealed class FrontierAmbience : MonoBehaviour
    {
        private const float ScoreVolume = .17f, BedVolume = .17f;
        // How fast a land's bed closes on its share of the view: about 90% of the way in a second, the same feel
        // as the grade that follows the camera over the same lands (LandAtmosphere.Easing).
        private const float BedEasing = 2.3f;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        private AudioSource bed, score, chime;
        private Dictionary<string, AudioSource> beds;
        private MatchController match;
        private MapLands lands;
        private Camera view;
        private AudioClip[] phrases;
        private float nextPhrase, nextChime;
        private int played;
        private bool silent, settled;

        public void Initialize(MatchController controller)
        {
            match = controller;
            lands = match.World.Lands; view = match.Rig?.Camera;
            string biome = match.World.Map.BiomeId ?? "forest";
            string realm = match.World.Map.RealmId ?? ContentRealms.Historical;
            // A map that shows more than one land in view (the Tierras de Leyenda) gets one bed per land, crossfaded
            // by how much of the view around the camera each land covers; every other map keeps its single bed.
            if (lands != null && lands.HasLands && lands.Biomes.Count > 1 && view != null)
            {
                beds = new Dictionary<string, AudioSource>();
                bool elven = false;
                foreach (string land in lands.Biomes)
                {
                    var source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.volume = 0;
                    source.clip = Cached("bed/" + land, () => RealBed(land) ?? Bed(land));
                    source.Play();
                    beds[land] = source;
                    elven |= land == MapLands.Elven;
                }
                RefreshBeds(0); // starts on whatever land the camera already looks at, not silent and fading in
                SetUpChime(elven);
            }
            else
            {
                bed = gameObject.AddComponent<AudioSource>();
                bed.playOnAwake = false; bed.loop = true; bed.spatialBlend = 0; bed.volume = BedVolume;
                bed.clip = Cached("bed/" + biome, () => RealBed(biome) ?? Bed(biome));
                bed.Play();
                SetUpChime(biome == MapLands.Elven);
            }
            score = gameObject.AddComponent<AudioSource>();
            score.playOnAwake = false; score.loop = false; score.spatialBlend = 0; score.volume = ScoreVolume;
            phrases = new[] { Cached("score/" + realm + "/0", () => Score(realm, 0)), Cached("score/" + realm + "/1", () => Score(realm, 1)) };
            // The first phrase waits a little: a match opens on its own sounds, not on music.
            nextPhrase = Time.unscaledTime + 14;
            Refresh();
        }

        // The elven wood's real accent (docs/art/audio.md), played sparsely over its bed, the same spirit as the
        // synthesised Chimes() below. Only wired where the recording exists; silence otherwise costs nothing.
        private void SetUpChime(bool elvenPresent)
        {
            if (!elvenPresent) return;
            var clip = Resources.Load<AudioClip>("Audio/Ambience/Elven/ambience_elven_chimes_accent");
            if (clip == null) return;
            chime = gameObject.AddComponent<AudioSource>();
            chime.playOnAwake = false; chime.loop = false; chime.spatialBlend = 0; chime.clip = clip;
            nextChime = Time.unscaledTime + UnityEngine.Random.Range(6f, 11f);
        }

        private void Update()
        {
            RefreshBeds(Time.unscaledDeltaTime);
            Refresh();
            if (chime != null && !silent && !chime.isPlaying && Time.unscaledTime >= nextChime)
            {
                chime.volume = beds != null && beds.TryGetValue(MapLands.Elven, out var elvenBed) ? elvenBed.volume : BedVolume;
                chime.Play();
                nextChime = Time.unscaledTime + chime.clip.length + UnityEngine.Random.Range(6f, 11f);
            }
            if (silent || score.isPlaying || Time.unscaledTime < nextPhrase) return;
            score.clip = phrases[played++ % phrases.Length];
            score.Play();
            // A rest between phrases is what keeps a score from wearing a hole in the listener.
            nextPhrase = Time.unscaledTime + score.clip.length + 48 + played % 3 * 11;
        }

        /// <summary>Crossfades every land's bed toward how much of the view around the camera its land covers.</summary>
        private void RefreshBeds(float seconds)
        {
            if (beds == null) return;
            var focus = LandAtmosphere.Focus(view);
            float radius = view.orthographic ? view.orthographicSize * .7f : 10;
            float ease = settled ? 1 - Mathf.Exp(-seconds * BedEasing) : 1;
            foreach (var pair in beds)
                pair.Value.volume = Mathf.Lerp(pair.Value.volume, LandAtmosphere.Share(lands, focus, radius, pair.Key) * BedVolume, ease);
            settled = true;
        }

        private void Refresh()
        {
            var settings = match?.Alpha?.Settings.Value;
            silent = match == null || settings != null && !settings.SoundEnabled;
            if (bed != null) bed.mute = silent;
            if (beds != null) foreach (var source in beds.Values) source.mute = silent;
            if (chime != null) chime.mute = silent;
            if (score == null) return;
            // The wind belongs to the map and stays with the sound setting; only the score answers to its own level.
            score.volume = ScoreVolume * (settings?.MusicVolume ?? 1);
            score.mute = silent || score.volume <= 0;
        }

        private void OnDestroy()
        {
            // Clips live in the shared cache and outlive the match that first asked for them.
            if (bed != null) bed.Stop();
            if (beds != null) foreach (var source in beds.Values) source.Stop();
            if (chime != null) chime.Stop();
            if (score != null) score.Stop();
        }

        private static AudioClip Cached(string key, System.Func<AudioClip> build)
        {
            if (Cache.TryGetValue(key, out var clip) && clip != null) return clip;
            clip = build();
            Cache[key] = clip;
            return clip;
        }

        // ------------------------------------------------------------------ ambience

        // The real recordings staged for forest/highland/elven (docs/art/audio.md, cached the same as the synth
        // beds below). Coast, desert and volcanic have no CC0 source yet, so Bed() below still carries them.
        private static AudioClip RealBed(string biome)
        {
            string path = biome == MapLands.Elven ? "Audio/Ambience/Elven/ambience_elven_wind_bed"
                : biome == MapLands.Highland ? "Audio/Ambience/Highland/ambience_highland_bed"
                : biome == "forest" ? "Audio/Ambience/Forest/ambience_forest_bed" : null;
            return path == null ? null : Resources.Load<AudioClip>(path);
        }

        private static AudioClip Bed(string biome)
        {
            const float seconds = 24, crossfade = 2.5f;
            var data = SoundForge.Buffer(seconds + crossfade);
            uint seed = SoundForge.Seed(biome == "desert" ? 4211 : biome == "caribbean" ? 907 :
                biome == MapLands.Volcanic ? 3319 : biome == MapLands.Elven ? 2477 : 1637);
            if (biome == MapLands.Volcanic) { Rumble(data, ref seed); Crackle(data, ref seed); }
            else
            {
                Wind(data, biome, ref seed);
                if (biome == "caribbean") { Surf(data, ref seed); Calls(data, ref seed, 1150, 1.7f, 7, .055f); }
                else if (biome == "desert") Chirr(data, ref seed);
                // The elven forest: a lighter gust than the open highland's, plus its own birdsong and chimes.
                else if (biome == MapLands.Elven) { Calls(data, ref seed, 2600, 2.4f, 11, .045f); Chimes(data, ref seed); }
                else Calls(data, ref seed, 2100, 3.1f, 9, .05f);
            }
            var looped = SoundForge.Loop(data, crossfade);
            SoundForge.Normalize(looped, biome == "desert" ? .34f : biome == MapLands.Elven ? .3f : biome == MapLands.Volcanic ? .42f : .38f);
            return SoundForge.Clip("Original " + biome + " ambience", looped);
        }

        /// <summary>Gusts: noise shaped by two slow swells that never line up, so the bed never repeats audibly.</summary>
        private static void Wind(float[] data, string biome, ref uint seed)
        {
            float cutoff = biome == "desert" ? 1300 : biome == "caribbean" ? 800 : biome == MapLands.Elven ? 900 : 620;
            // The elven forest's air moves lighter than the open highland's: a softer gust, brighter in the same breath.
            float gain = biome == MapLands.Elven ? .72f : 1f;
            SoundForge.Noise(data, 0, data.Length / (float)SoundForge.Rate, gain, cutoff, ref seed);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SoundForge.Rate;
                float gust = .46f + .32f * Mathf.Sin(t * .41f) + .22f * Mathf.Sin(t * .93f + 1.7f);
                data[i] *= Mathf.Max(.12f, gust);
            }
            if (biome != "desert") return;
            SoundForge.HighPass(data, 180);
        }

        /// <summary>A slow, heavy swell of low-passed noise: the volcanic waste's rumble, felt more than heard.</summary>
        private static void Rumble(float[] data, ref uint seed)
        {
            SoundForge.Noise(data, 0, data.Length / (float)SoundForge.Rate, 1.15f, 130, ref seed);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SoundForge.Rate;
                float swell = .55f + .3f * Mathf.Sin(t * .19f) + .2f * Mathf.Sin(t * .53f + 2.4f);
                data[i] *= Mathf.Max(.22f, swell);
            }
        }

        /// <summary>Sparks off cooling rock: short, noisy pops that never fall on a beat.</summary>
        private static void Crackle(float[] data, ref uint seed)
        {
            float length = data.Length / (float)SoundForge.Rate;
            for (float t = .8f; t < length - 1; t += 2.6f)
            {
                float at = t + Mathf.Abs(SoundForge.Next(ref seed)) * 1.8f;
                if (at > length - .3f) continue;
                var pop = SoundForge.Buffer(.1f);
                SoundForge.Noise(pop, 0, .1f, 1f, 2800, ref seed);
                SoundForge.Fade(pop, .002f, .09f);
                SoundForge.Soften(pop, 2f);
                for (int i = 0; i < pop.Length; i++) pop[i] *= .5f;
                Mix(data, pop, at);
            }
        }

        /// <summary>A few soft, high partials, as if wind stirred chimes hung deep in the canopy.</summary>
        private static void Chimes(float[] data, ref uint seed)
        {
            float length = data.Length / (float)SoundForge.Rate;
            for (float t = 1.4f; t < length - 1.5f; t += 3.7f)
            {
                float at = t + Mathf.Abs(SoundForge.Next(ref seed));
                if (at > length - 1) continue;
                float hz = 1700 + Mathf.Abs(SoundForge.Next(ref seed)) * 1100;
                SoundForge.Bell(data, at, hz, .045f, .85f);
            }
        }

        private static void Surf(float[] data, ref uint seed)
        {
            float length = data.Length / (float)SoundForge.Rate;
            for (float t = 0; t < length - 6; t += 6.3f)
            {
                var swell = SoundForge.Buffer(5.4f);
                uint local = seed;
                SoundForge.Noise(swell, 0, 5.4f, 1f, 950, ref local);
                seed = local;
                for (int i = 0; i < swell.Length; i++)
                {
                    float age = i / (float)swell.Length;
                    swell[i] *= Mathf.Pow(Mathf.Sin(age * Mathf.PI), 1.8f) * .8f;
                }
                Mix(data, swell, t);
            }
        }

        /// <summary>Bird or gull calls: a couple of quick swept blips, spaced so they never fall in a pattern.</summary>
        private static void Calls(float[] data, ref uint seed, float hz, float spread, int count, float gain)
        {
            float length = data.Length / (float)SoundForge.Rate;
            for (int call = 0; call < count; call++)
            {
                float at = (call + .35f + Mathf.Abs(SoundForge.Next(ref seed)) * .55f) * (length / count);
                if (at > length - 1.2f) continue;
                int notes = 2 + (call % 2);
                for (int note = 0; note < notes; note++)
                {
                    float pitch = hz * (1 + note * .16f + SoundForge.Next(ref seed) * .07f);
                    var blip = SoundForge.Buffer(.16f);
                    SoundForge.Partial(blip, 0, pitch, gain, .045f, -pitch * spread * .2f);
                    SoundForge.Partial(blip, 0, pitch * 2.01f, gain * .3f, .03f, -pitch * spread * .3f);
                    SoundForge.Fade(blip, .004f, .06f);
                    Mix(data, blip, at + note * .11f);
                }
            }
        }

        /// <summary>The dry stridulation of a basin at noon: a high tone chopped by its own tremolo.</summary>
        private static void Chirr(float[] data, ref uint seed)
        {
            float length = data.Length / (float)SoundForge.Rate;
            for (float t = 1.5f; t < length - 3; t += 5.7f)
            {
                var buzz = SoundForge.Buffer(2.2f);
                for (int i = 0; i < buzz.Length; i++)
                {
                    float age = i / (float)SoundForge.Rate;
                    float tremolo = .5f + .5f * Mathf.Sin(age * 2 * Mathf.PI * 42);
                    buzz[i] = Mathf.Sin(age * 2 * Mathf.PI * 4200) * tremolo * .028f;
                }
                SoundForge.Fade(buzz, .35f, .6f);
                Mix(data, buzz, t + Mathf.Abs(SoundForge.Next(ref seed)));
            }
        }

        // ------------------------------------------------------------------ score

        private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        private static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };

        /// <summary>
        /// A phrase of four bars: a breathing drone underneath, a plucked figure walking the chord, and a
        /// few long notes on top. Each realm gets its own mode and root, so the three worlds do not sound
        /// like the same game with a different palette.
        /// </summary>
        private static AudioClip Score(string realm, int variant)
        {
            bool naval = realm == ContentRealms.Naval, fantasy = realm == ContentRealms.Fantasy;
            int[] scale = naval ? Mixolydian : fantasy ? Aeolian : Dorian;
            float root = naval ? 196f : fantasy ? 110f : 146.83f;
            int[] progression = variant == 0 ? new[] { 0, 5, 3, 4 } : new[] { 0, 3, 6, 4 };
            const float bar = 3.75f, crossfade = 1.6f;
            var data = SoundForge.Buffer(bar * 4 + 2.6f);
            uint seed = SoundForge.Seed(realm.Length * 131 + variant * 17 + 3);
            for (int b = 0; b < 4; b++)
            {
                float at = b * bar;
                int degree = progression[b];
                float chord = root * Semitone(scale, degree);
                // Drone: the root an octave down with its fifth, swelling into the bar.
                SoundForge.Partial(data, at, chord * .5f, .30f, bar * .85f);
                SoundForge.Partial(data, at, chord * .75f, .16f, bar * .8f);
                // Plucked figure: root, third, fifth, octave and back, on eighth notes.
                int[] figure = { 0, 2, 4, 7, 4, 2 };
                for (int note = 0; note < figure.Length; note++)
                {
                    float hz = root * Semitone(scale, degree + figure[note]);
                    float gain = note == 0 ? .34f : .22f - note * .015f;
                    SoundForge.Pluck(data, at + note * (bar / figure.Length), hz * 2, gain, .994f, ref seed);
                }
                // A long note above, one per bar, with a soft attack so it sings rather than strikes.
                float melody = root * Semitone(scale, degree + (b % 2 == 0 ? 7 : 9)) * 2;
                var voice = SoundForge.Buffer(bar * .95f);
                SoundForge.Partial(voice, 0, melody, .16f, bar * .5f);
                SoundForge.Partial(voice, 0, melody * 2, .05f, bar * .35f);
                SoundForge.Fade(voice, .55f, bar * .5f);
                Mix(data, voice, at + bar * .18f);
            }
            SoundForge.LowPass(data, naval ? 5200 : 4200);
            SoundForge.Reverb(data, .75f, .58f);
            var looped = SoundForge.Loop(data, crossfade);
            SoundForge.Fade(looped, .9f, 1.4f);
            SoundForge.Normalize(looped, .5f);
            return SoundForge.Clip("Original " + realm + " score " + variant, looped);
        }

        private static float Semitone(int[] scale, int degree)
        {
            int octave = Mathf.FloorToInt(degree / (float)scale.Length);
            int index = degree - octave * scale.Length;
            return Mathf.Pow(2, (scale[index] + octave * 12) / 12f);
        }

        private static void Mix(float[] data, float[] source, float atSeconds)
        {
            int start = Mathf.RoundToInt(atSeconds * SoundForge.Rate);
            for (int i = 0; i < source.Length; i++)
            {
                int index = start + i;
                if (index < 0 || index >= data.Length) continue;
                data[index] += source[i];
            }
        }
    }
}
