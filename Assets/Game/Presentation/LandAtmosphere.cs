using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Emberfield.Presentation
{
    /// <summary>
    /// The grade and haze of the land the camera is looking at, on a map whose lands wear different cultures (MapLands).
    /// AlphaLighting's grade stays underneath; over it a light golden grade for the elven forest and a dark red one for the
    /// volcanic waste, each a global Volume whose weight is that land's share of the ground around the centre of the view.
    /// The weight eases toward the share over about a second, so a pan across a border fades the grade rather than
    /// cutting it, and the fog and the sky past the map's edge follow the same weights. A map without such lands, and
    /// every map of the other realms, never builds one.
    /// </summary>
    public sealed class LandAtmosphere : MonoBehaviour
    {
        // How fast a weight closes on its share: about 90% of the way in a second.
        private const float Easing = 2.3f;
        private static readonly Color ElvenHaze = new Color(.70f, .72f, .56f), VolcanicHaze = new Color(.40f, .28f, .25f);
        private Camera view;
        private MapLands lands;
        private Volume elven, volcanic;
        private VolumeProfile elvenProfile, volcanicProfile;
        private Color fog, background;
        private float fogStart, fogEnd;
        private bool settled;

        /// <summary>The current weight of the land's grade, 0 to 1; 0 for a land this map does not show.</summary>
        public float Weight(string biome) => biome == MapLands.Elven ? elven != null ? elven.weight : 0 : biome == MapLands.Volcanic && volcanic != null ? volcanic.weight : 0;

        /// <summary>Builds the land grades for a map whose lands show an elven or a volcanic land; null otherwise.</summary>
        public static LandAtmosphere Attach(GameObject holder, Camera camera, MapLands lands)
        {
            if (lands == null || !lands.HasLands) return null;
            bool hasElven = false, hasVolcanic = false;
            foreach (string biome in lands.Biomes) { hasElven |= biome == MapLands.Elven; hasVolcanic |= biome == MapLands.Volcanic; }
            if (!hasElven && !hasVolcanic) return null;
            var atmosphere = holder.AddComponent<LandAtmosphere>();
            atmosphere.view = camera; atmosphere.lands = lands;
            atmosphere.fog = RenderSettings.fogColor; atmosphere.fogStart = RenderSettings.fogStartDistance; atmosphere.fogEnd = RenderSettings.fogEndDistance;
            atmosphere.background = camera.backgroundColor;
            if (hasElven) atmosphere.elven = atmosphere.Grade(holder, "Elven forest grade", 11, out atmosphere.elvenProfile, profile =>
            {
                // Light and golden in the highlights only, so the greens stay deep instead of turning lime; a softer bloom.
                var colour = profile.Add<ColorAdjustments>(true);
                colour.postExposure.Override(.12f); colour.saturation.Override(4); colour.contrast.Override(8); colour.colorFilter.Override(new Color(1f, 1f, .96f));
                var split = profile.Add<ShadowsMidtonesHighlights>(true);
                split.shadows.Override(new Vector4(.94f, 1.01f, 1.05f, 0)); split.highlights.Override(new Vector4(1.08f, 1.04f, .92f, 0));
                var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.26f); bloom.threshold.Override(1.1f); bloom.scatter.Override(.6f);
            });
            if (hasVolcanic) atmosphere.volcanic = atmosphere.Grade(holder, "Volcanic waste grade", 12, out atmosphere.volcanicProfile, profile =>
            {
                // Dark and red, but not murky: the contrast rises as the exposure falls, so a unit still stands off the ash.
                var colour = profile.Add<ColorAdjustments>(true);
                colour.postExposure.Override(-.04f); colour.saturation.Override(-4); colour.contrast.Override(10); colour.colorFilter.Override(new Color(1f, .9f, .86f));
                var split = profile.Add<ShadowsMidtonesHighlights>(true);
                split.shadows.Override(new Vector4(1.06f, .97f, .95f, 0)); split.highlights.Override(new Vector4(1.05f, .97f, .92f, 0));
                var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.3f); bloom.threshold.Override(1.15f); bloom.scatter.Override(.62f);
                var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.2f); vignette.smoothness.Override(.5f); vignette.color.Override(new Color(.1f, .02f, .01f));
            });
            atmosphere.Settle();
            return atmosphere;
        }

        private Volume Grade(GameObject holder, string name, int priority, out VolumeProfile profile, System.Action<VolumeProfile> fill)
        {
            var child = new GameObject(name); child.transform.SetParent(holder.transform, false);
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = name;
            fill(profile);
            var volume = child.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = priority; volume.sharedProfile = profile; volume.weight = 0;
            return volume;
        }

        /// <summary>The share of this biome in the ground around a point, sampled at the point and on two rings about it.</summary>
        public static float Share(MapLands lands, Vector3 focus, float radius, string biome)
        {
            int hits = lands.BiomeAt(DefinitionLoader.ToSimulation(focus)) == biome ? 1 : 0, samples = 1;
            for (int ring = 1; ring <= 2; ring++)
                for (int i = 0; i < 6; i++)
                {
                    float angle = (i + ring * .5f) * Mathf.PI / 3;
                    var point = focus + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius * ring * .5f;
                    samples++; if (lands.BiomeAt(DefinitionLoader.ToSimulation(point)) == biome) hits++;
                }
            return hits / (float)samples;
        }

        /// <summary>The ground point at the centre of the view.</summary>
        public static Vector3 Focus(Camera camera)
        {
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            return new Plane(Vector3.up, 0).Raycast(ray, out float distance) ? ray.GetPoint(distance) : new Vector3(camera.transform.position.x, 0, camera.transform.position.z);
        }

        /// <summary>Eases the weights toward the land in view by this many seconds.</summary>
        public void Step(float seconds)
        {
            if (view == null || lands == null) return;
            var focus = Focus(view);
            float radius = view.orthographic ? view.orthographicSize * .7f : 10;
            float ease = settled ? 1 - Mathf.Exp(-seconds * Easing) : 1;
            if (elven != null) elven.weight = Mathf.Lerp(elven.weight, Share(lands, focus, radius, MapLands.Elven), ease);
            if (volcanic != null) volcanic.weight = Mathf.Lerp(volcanic.weight, Share(lands, focus, radius, MapLands.Volcanic), ease);
            float golden = elven != null ? elven.weight : 0, ash = volcanic != null ? volcanic.weight : 0;
            // The volcanic haze is thick and close; the forest only warms the air a little.
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(fog, ElvenHaze, golden * .4f), VolcanicHaze, ash * .7f);
            RenderSettings.fogStartDistance = Mathf.Lerp(fogStart, fogStart - 8, ash);
            RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, fogEnd - 22, ash);
            view.backgroundColor = Color.Lerp(Color.Lerp(background, ElvenHaze * .8f, golden * .3f), VolcanicHaze * .7f, ash * .7f);
        }

        /// <summary>Jumps the weights to the land in view at once, as a still or a fresh match wants.</summary>
        public void Settle() { settled = false; Step(0); settled = true; }

        private void LateUpdate() => Step(Time.unscaledDeltaTime);

        private void OnDestroy()
        {
            foreach (var profile in new[] { elvenProfile, volcanicProfile })
            {
                if (profile == null) continue;
                foreach (var component in profile.components) if (component != null) Destroy(component);
                Destroy(profile);
            }
        }
    }
}
