using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Embers and thin smoke over the lava of a volcanic land and out of its volcanoes, and glimmers drifting at the edge
    /// of the elven forest. A set is one mesh of quads that the Land Motes shader flies (rise, drift, wobble, fade and
    /// start again), so nothing runs on the CPU after the build and the count is fixed when the set is made. Every count
    /// here is a cap for a desktop and the high phone tier; mid draws half and low draws no embers or glimmers and half
    /// the smoke (<see cref="MobileQuality.AmbientParticleShare"/>). A land's sources are cut into patches about 32 m
    /// across, each its own set, so a patch appears when its ground is explored and the fog never shows floating lights.
    /// </summary>
    public static class LandMotes
    {
        public enum Kind { LavaEmbers, LavaSmoke, VolcanoEmbers, VolcanoSmoke, Glimmers }
        public const float PatchMetres = 32;
        private static readonly Dictionary<Kind, Material> materials = new Dictionary<Kind, Material>();

        /// <summary>How many motes of this kind a source group of this full count draws on the current tier.</summary>
        public static int Count(Kind kind, int full)
        {
            float share = MobileQuality.AmbientParticleShare;
            bool smoke = kind == Kind.LavaSmoke || kind == Kind.VolcanoSmoke;
            return Mathf.RoundToInt(full * (smoke ? Mathf.Max(.5f, share) : share));
        }

        /// <summary>
        /// One set of <paramref name="count"/> motes rising from the given sources (in the parent's space), or null when
        /// the tier draws none. The caller owns the returned mesh and hides the set with the ground it stands on.
        /// </summary>
        public static MeshRenderer Build(Kind kind, Transform parent, IReadOnlyList<Vector3> sources, int count, uint salt, List<Mesh> owned)
        {
            count = Count(kind, count);
            if (count <= 0 || sources == null || sources.Count == 0) return null;
            var material = Material(kind);
            if (material == null) return null;
            var vertices = new List<Vector3>(count * 4); var corners = new List<Vector2>(count * 4); var motes = new List<Vector4>(count * 4);
            var indices = new List<int>(count * 6);
            float jitter = kind == Kind.VolcanoEmbers || kind == Kind.VolcanoSmoke ? .5f : kind == Kind.Glimmers ? 1.2f : .45f;
            var low = Vector3.positiveInfinity; var high = Vector3.negativeInfinity;
            for (int i = 0; i < count; i++)
            {
                uint seed = Hash((uint)i * 747796405u + salt);
                var origin = sources[(int)(seed % (uint)sources.Count)];
                origin += new Vector3(((seed >> 8 & 255) / 255f - .5f) * 2 * jitter, 0, ((seed >> 16 & 255) / 255f - .5f) * 2 * jitter);
                uint more = Hash(seed ^ 0x9E3779B9u);
                var mote = new Vector4((more & 1023) / 1023f, (more >> 10 & 255) / 255f, (more >> 18 & 255) / 255f, .7f + (seed >> 24 & 255) / 255f * .6f);
                int first = vertices.Count;
                foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) })
                { vertices.Add(origin); corners.Add(corner); motes.Add(mote); }
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 2); indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
                low = Vector3.Min(low, origin); high = Vector3.Max(high, origin);
            }
            var mesh = new Mesh { name = "Land motes " + kind };
            mesh.SetVertices(vertices); mesh.SetUVs(0, corners); mesh.SetUVs(1, motes); mesh.SetTriangles(indices, 0);
            // The shader moves every vertex, so the bounds are what the motes can reach, not where the quads were built.
            float rise = material.GetFloat("_Rise") * 1.4f + 1, reach = Mathf.Abs(material.GetVector("_Drift").x) + Mathf.Abs(material.GetVector("_Drift").z) + 2.5f;
            var bounds = new Bounds(); bounds.SetMinMax(low - new Vector3(reach, 1, reach), high + new Vector3(reach, rise, reach));
            mesh.bounds = bounds;
            owned.Add(mesh);
            var holder = new GameObject("Land " + kind, typeof(MeshFilter), typeof(MeshRenderer));
            holder.transform.SetParent(parent, false);
            holder.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = holder.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        /// <summary>Sources cut into square patches about <see cref="PatchMetres"/> across, each with its sources' centre.</summary>
        public static List<(List<Vector3> Sources, Vector3 Centre)> Patches(IEnumerable<Vector3> sources)
        {
            var patches = new Dictionary<long, List<Vector3>>();
            foreach (var source in sources)
            {
                long key = (long)Mathf.FloorToInt(source.z / PatchMetres) << 32 | (uint)Mathf.FloorToInt(source.x / PatchMetres);
                if (!patches.TryGetValue(key, out var list)) patches[key] = list = new List<Vector3>();
                list.Add(source);
            }
            var result = new List<(List<Vector3>, Vector3)>();
            foreach (var list in patches.Values)
            {
                var centre = Vector3.zero; foreach (var source in list) centre += source; centre /= list.Count;
                // The source nearest the centre, so the patch shows with ground that really is in it.
                var nearest = list[0];
                foreach (var source in list) if ((source - centre).sqrMagnitude < (nearest - centre).sqrMagnitude) nearest = source;
                result.Add((list, nearest));
            }
            return result;
        }

        private static Material Material(Kind kind)
        {
            if (materials.TryGetValue(kind, out var existing) && existing != null) return existing;
            var shader = Resources.Load<Shader>("Shaders/LandMotes");
            if (shader == null) return null;
            var material = new Material(shader) { name = "Land motes " + kind };
            bool smoke = kind == Kind.LavaSmoke || kind == Kind.VolcanoSmoke;
            material.SetFloat("_Source", smoke ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat("_Destination", smoke ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.One);
            // Colours are linear and embers are above the bloom threshold (1.2), so they glow on the tiers with HDR.
            switch (kind)
            {
                case Kind.LavaEmbers:
                    Set(material, new Color(3.4f, 1.3f, .32f, 1), new Color(1.3f, .22f, .05f, 1), 3.2f, 2.8f, new Vector3(.55f, 0, .25f), .055f, 0, .55f);
                    break;
                case Kind.VolcanoEmbers:
                    Set(material, new Color(3.8f, 1.45f, .35f, 1), new Color(1.4f, .24f, .05f, 1), 3.8f, 5.5f, new Vector3(.9f, 0, .4f), .075f, 0, .55f);
                    break;
                case Kind.Glimmers:
                    Set(material, new Color(1.25f, 1.35f, .7f, .8f), new Color(.45f, .95f, .75f, .6f), 7f, .9f, new Vector3(.25f, 0, .1f), .05f, 0, .8f);
                    break;
                case Kind.LavaSmoke:
                    Set(material, new Color(.3f, .27f, .26f, .2f), new Color(.42f, .4f, .39f, 1), 8f, 3.2f, new Vector3(1.2f, 0, .5f), .8f, 1.8f, 1);
                    break;
                default:
                    // Lighter than the ash below it: dark smoke over dark ground and a dark sky is not seen at all.
                    Set(material, new Color(.36f, .33f, .32f, .42f), new Color(.52f, .5f, .49f, 1), 10f, 8.5f, new Vector3(2.6f, 0, 1.1f), 1.1f, 2.6f, 1);
                    break;
            }
            material.renderQueue = (int)RenderQueue.Transparent + (smoke ? 2 : 1);
            materials[kind] = material;
            return material;
        }

        private static void Set(Material material, Color colour, Color cool, float life, float rise, Vector3 drift, float size, float grow, float softness)
        {
            material.SetVector("_Color", colour); material.SetVector("_Cool", cool);
            material.SetFloat("_Life", life); material.SetFloat("_Rise", rise); material.SetVector("_Drift", drift);
            material.SetFloat("_Size", size); material.SetFloat("_Grow", grow); material.SetFloat("_Softness", softness);
        }

        private static uint Hash(uint v) { unchecked { v ^= v >> 16; v *= 0x7feb352d; v ^= v >> 15; v *= 0x846ca68b; return v ^ v >> 16; } }
    }
}
