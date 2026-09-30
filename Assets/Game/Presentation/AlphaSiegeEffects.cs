using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>Fixed-cost oil/fire feedback for observed defense discharges; visibility is checked each frame.</summary>
    internal sealed class AlphaSiegeEffects : IDisposable
    {
        private const int Capacity = 24;
        private readonly World world;
        private readonly Transform root;
        private readonly Transform[] sparks = new Transform[Capacity];
        private readonly Vector3[] sources = new Vector3[Capacity], targets = new Vector3[Capacity];
        private readonly float[] born = new float[Capacity];
        private readonly int[] owners = new int[Capacity];
        private int next;
        internal AlphaSiegeEffects(World world, Transform parent)
        {
            this.world = world; root = new GameObject("Visible siege discharges · pool 24").transform; root.SetParent(parent, false);
            for (int i = 0; i < Capacity; i++)
            {
                var item = new GameObject("Pooled falling oil " + i, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(root, false);
                item.GetComponent<MeshFilter>().sharedMesh = AlphaSiegeVisuals.Projectile(2);
                var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = ArtKit.Catalog.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                sparks[i] = item.transform; item.SetActive(false);
            }
        }
        internal void Oil(BuildingState building)
        {
            if (world.Vision != null && !world.Vision.IsEntityVisible(1, building.Id)) return;
            float width = building.WidthCells * world.Map.CellSizeMillimetres * .001f, depth = building.DepthCells * world.Map.CellSizeMillimetres * .001f;
            var centre = DefinitionLoader.ToWorld(building.Position);
            for (int side = 0; side < 4; side++) for (int burst = 0; burst < 2; burst++)
            {
                int slot = next++ % Capacity; float a = side * Mathf.PI * .5f;
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var tangent = new Vector3(-outward.z, 0, outward.x);
                float radius = side % 2 == 0 ? width * .5f : depth * .5f;
                sources[slot] = centre + outward * radius * .80f + tangent * (burst == 0 ? -.28f : .28f) + Vector3.up * 3.70f;
                targets[slot] = centre + outward * (radius + .35f) + tangent * (burst == 0 ? -.45f : .45f) + Vector3.up * .12f;
                born[slot] = Time.unscaledTime - burst * .07f; owners[slot] = building.Id;
                sparks[slot].position = sources[slot]; sparks[slot].gameObject.SetActive(true);
            }
        }
        internal void Sync()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (!sparks[i].gameObject.activeSelf) continue;
                float progress = (Time.unscaledTime - born[i]) / .85f;
                if (progress >= 1 || world.Vision != null && !world.Vision.IsEntityVisible(1, owners[i])) { sparks[i].gameObject.SetActive(false); continue; }
                sparks[i].position = Vector3.Lerp(sources[i], targets[i], progress * progress);
                sparks[i].localRotation = Quaternion.Euler(90, i * 29, 0);
                sparks[i].localScale = new Vector3(.65f, .65f, 1.4f) * Mathf.Lerp(.6f, 1.2f, progress);
            }
        }
        public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root.gameObject); }
    }
}
