using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>
    /// A settlement that is lived in shows it from the roofline. Each hearth releases a few puffs that
    /// grow out of the chimney, lean with the wind and thin away to nothing. They use a translucent unlit
    /// material of their own: drawn with the world's opaque material they read as grey boulders parked
    /// on the roof, which is worse than no smoke at all.
    /// </summary>
    public sealed class HearthSmoke : MonoBehaviour
    {
        // Buildings with a fire in them. Storeyards, walls and workshops stay cold.
        public static bool Smokes(string definitionId)
            => definitionId == "hearth" || definitionId == "shelter" || definitionId == "muster_hall" || definitionId == "keep";

        private const int Puffs = 4;
        private const float Life = 6.4f, Rise = 3.6f;
        private static readonly Vector3 Drift = new Vector3(.75f, 0, .32f);
        private static readonly int FadeProperty = Shader.PropertyToID("_Fade");
        private static Mesh shared;
        private static Material material;
        private static MaterialPropertyBlock block;
        private Renderer[] renderers;
        private Transform[] puffs;
        private float[] phase, turn;
        private float size = 1;

        public static HearthSmoke Attach(Transform parent, Vector3 chimney, float scale)
        {
            var holder = new GameObject("Chimney smoke");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = chimney;
            var smoke = holder.AddComponent<HearthSmoke>();
            // The puff mesh is built at half-metre radius, so this is close to its width in metres.
            smoke.size = 1.15f * scale;
            smoke.Build();
            return smoke;
        }

        private void Build()
        {
            if (shared == null)
            {
                var builder = new AlphaMeshBuilder();
                builder.Dome(Vector3.zero, new Vector3(.5f, .42f, .5f), 6, 2, Color.white);
                builder.Dome(new Vector3(.2f, .14f, -.14f), new Vector3(.35f, .31f, .35f), 5, 2, Color.white);
                shared = builder.Mesh("Chimney puff");
            }
            if (material == null)
                material = new Material(Resources.Load<Shader>("Shaders/Smoke")) { name = "Hearth smoke", enableInstancing = true };
            block = block ?? new MaterialPropertyBlock();
            puffs = new Transform[Puffs]; renderers = new Renderer[Puffs]; phase = new float[Puffs]; turn = new float[Puffs];
            for (int index = 0; index < Puffs; index++)
            {
                var puff = new GameObject("Puff", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                puff.SetParent(transform, false);
                puff.GetComponent<MeshFilter>().sharedMesh = shared;
                var renderer = puff.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // Smoke neither casts nor takes a shadow: it would strobe across the roof below it.
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                puffs[index] = puff; renderers[index] = renderer;
                phase[index] = index / (float)Puffs;
                turn[index] = 34 + index * 29;
            }
        }

        private void LateUpdate()
        {
            if (puffs == null) return;
            for (int index = 0; index < puffs.Length; index++)
            {
                float age = Mathf.Repeat(Time.time / Life + phase[index], 1);
                var puff = puffs[index];
                puff.localPosition = Vector3.up * (Rise * age) + Drift * (age * age * 1.1f);
                // It leaves the chimney small and tight and spreads as it cools.
                puff.localScale = Vector3.one * (size * (.34f + age * 1.15f));
                puff.localRotation = Quaternion.Euler(0, turn[index] * age, turn[index] * .18f * age);
                // Thin at the mouth, fullest early, gone well before it reaches the treetops.
                block.Clear();
                block.SetFloat(FadeProperty, Mathf.Sin(age * Mathf.PI) * (1 - age * .55f));
                renderers[index].SetPropertyBlock(block);
            }
        }
    }
}
