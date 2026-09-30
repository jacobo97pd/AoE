using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// The voice of the battlefield. Each cue is built from its own materials — wood knocks and leaf rustle
    /// for an axe, inharmonic partials for a bell, a low thud with a metal edge for a blow landing — so the
    /// ear can tell an economy running smoothly from a fight starting without looking away from the map.
    /// </summary>
    public static class FrontierSounds
    {
        public static AudioClip Cue(FeedbackCue cue)
        {
            switch (cue)
            {
                case FeedbackCue.Order: return Clip("Order acknowledged", Order());
                case FeedbackCue.Gather: return Clip("Harvest", Harvest());
                case FeedbackCue.Chop: return Clip("Axe on wood", Chop());
                case FeedbackCue.Mine: return Clip("Pick on stone", Mine());
                case FeedbackCue.Impact: return Clip("Blow landed", Impact());
                case FeedbackCue.Complete: return Clip("Construction finished", Complete());
                case FeedbackCue.Defeat: return Clip("Loss", Defeat());
                default: return Clip("Objective", Objective());
            }
        }

        // A baton tap on wood: two short plucks a fifth apart. This one plays on every order given, so it
        // has to sit under the music rather than announce itself.
        private static float[] Order()
        {
            var data = SoundForge.Buffer(.30f);
            uint seed = SoundForge.Seed(11);
            SoundForge.Pluck(data, 0, 880, .34f, .962f, ref seed);
            SoundForge.Pluck(data, .045f, 1320, .22f, .955f, ref seed);
            SoundForge.LowPass(data, 4200);
            SoundForge.Fade(data, .002f, .10f);
            SoundForge.Normalize(data, .42f);
            return data;
        }

        // Grain poured into a basket: a soft rustle with a little body under it.
        private static float[] Harvest()
        {
            var data = SoundForge.Buffer(.34f);
            uint seed = SoundForge.Seed(23);
            SoundForge.Noise(data, 0, .26f, .34f, 2600, ref seed);
            SoundForge.HighPass(data, 700);
            SoundForge.Partial(data, .01f, 196, .18f, .09f);
            SoundForge.Fade(data, .004f, .22f);
            SoundForge.Normalize(data, .38f);
            return data;
        }

        // Axe into a trunk: a hard transient, the resonant knock of the wood, and leaves shaking after it.
        private static float[] Chop()
        {
            var data = SoundForge.Buffer(.42f);
            uint seed = SoundForge.Seed(37);
            SoundForge.Noise(data, 0, .012f, .95f, 9000, ref seed);
            SoundForge.Partial(data, .002f, 196, .55f, .075f, -240);
            SoundForge.Partial(data, .002f, 432, .32f, .055f, -600);
            SoundForge.Partial(data, .002f, 118, .40f, .11f, -90);
            SoundForge.Noise(data, .05f, .30f, .085f, 3400, ref seed);
            SoundForge.Soften(data, 1.6f);
            SoundForge.Fade(data, .001f, .18f);
            SoundForge.Normalize(data, .62f);
            return data;
        }

        // Pick on rock: the same shape as the axe, but the body rings inharmonically and gravel follows.
        private static float[] Mine()
        {
            var data = SoundForge.Buffer(.46f);
            uint seed = SoundForge.Seed(53);
            SoundForge.Noise(data, 0, .010f, .9f, 12000, ref seed);
            SoundForge.Bell(data, .002f, 620, .34f, .16f);
            SoundForge.Partial(data, .002f, 140, .34f, .07f, -160);
            SoundForge.Noise(data, .04f, .34f, .10f, 5200, ref seed);
            SoundForge.HighPass(data, 240);
            SoundForge.Soften(data, 1.5f);
            SoundForge.Fade(data, .001f, .2f);
            SoundForge.Normalize(data, .58f);
            return data;
        }

        // A blow landing: weight first, then the scrape of metal on shield.
        private static float[] Impact()
        {
            var data = SoundForge.Buffer(.40f);
            uint seed = SoundForge.Seed(71);
            SoundForge.Partial(data, 0, 132, .85f, .085f, -320);
            SoundForge.Partial(data, 0, 88, .55f, .13f, -120);
            SoundForge.Noise(data, 0, .05f, .45f, 5200, ref seed);
            SoundForge.Bell(data, .006f, 1240, .10f, .09f);
            SoundForge.Soften(data, 1.8f);
            SoundForge.Fade(data, .001f, .16f);
            SoundForge.Normalize(data, .70f);
            return data;
        }

        // Timber settling into place, then a warm third to say it is done and standing.
        private static float[] Complete()
        {
            var data = SoundForge.Buffer(1.25f);
            uint seed = SoundForge.Seed(97);
            SoundForge.Partial(data, 0, 165, .45f, .09f, -140);
            SoundForge.Partial(data, .07f, 220, .38f, .08f, -180);
            SoundForge.Noise(data, 0, .16f, .16f, 3000, ref seed);
            SoundForge.Pluck(data, .16f, 523.25f, .40f, .992f, ref seed);
            SoundForge.Pluck(data, .21f, 659.25f, .34f, .991f, ref seed);
            SoundForge.Pluck(data, .27f, 783.99f, .28f, .990f, ref seed);
            SoundForge.Reverb(data, .55f);
            SoundForge.Fade(data, .002f, .35f);
            SoundForge.Normalize(data, .52f);
            return data;
        }

        // Something of yours is gone: a short fall of a fifth on a muted horn. It fires often in a battle,
        // so it stays low and dark rather than dramatic.
        private static float[] Defeat()
        {
            var data = SoundForge.Buffer(.85f);
            uint seed = SoundForge.Seed(131);
            SoundForge.Partial(data, 0, 174.6f, .55f, .34f, -52);
            SoundForge.Partial(data, 0, 261.6f, .26f, .26f, -78);
            SoundForge.Partial(data, .01f, 87.3f, .40f, .40f, -20);
            SoundForge.Noise(data, 0, .06f, .12f, 1400, ref seed);
            SoundForge.LowPass(data, 2600);
            SoundForge.Reverb(data, .35f);
            SoundForge.Fade(data, .008f, .34f);
            SoundForge.Normalize(data, .46f);
            return data;
        }

        // A beacon changing hands rings across the valley: struck bell, long tail, room around it.
        private static float[] Objective()
        {
            var data = SoundForge.Buffer(1.9f);
            uint seed = SoundForge.Seed(179);
            SoundForge.Noise(data, 0, .008f, .5f, 9000, ref seed);
            SoundForge.Bell(data, 0, 392, .62f, .95f);
            SoundForge.Bell(data, .33f, 587.3f, .32f, .75f);
            SoundForge.Reverb(data, .85f, .62f);
            SoundForge.Fade(data, .001f, .55f);
            SoundForge.Normalize(data, .55f);
            return data;
        }

        private static AudioClip Clip(string name, float[] data) => SoundForge.Clip("Original " + name, data);
    }
}
