using System;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed class FogView : IDisposable
    {
        private readonly World world;
        private readonly Texture2D texture;
        private static readonly Color32 Visible = new Color32(0, 0, 0, 0), Explored = new Color32(10, 20, 26, 165), Unknown = new Color32(8, 15, 20, 255);
        private readonly Color32[] pixels;
        private readonly byte[] cells;
        private readonly Material material;
        private readonly Mesh mesh;
        private long revision = -1;
        public Texture2D Texture => texture;
        public FogView(World world, Transform parent)
        {
            this.world = world;
            int width = world.Map.WidthCells, height = world.Map.HeightCells;
            // Bilinear, not point: the fog covers one texel per map cell, and sampling it sharply turned the
            // border of what you have seen into black shards. Interpolating fades it across a single cell.
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { name = "Local explored and visible terrain", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[width * height];
            cells = new byte[width * height];
            // Public world labels render after fog, and both precede camera-space UI.
            material = new Material(Resources.Load<Shader>("Shaders/FogOfWar")) { mainTexture = texture, renderQueue = 2998 };
            float w = width * world.Map.CellSizeMillimetres * .001f, h = height * world.Map.CellSizeMillimetres * .001f;
            mesh = new Mesh { name = "Fog terrain plane" };
            mesh.vertices = new[] { new Vector3(0, .02f, 0), new Vector3(w, .02f, 0), new Vector3(0, .02f, h), new Vector3(w, .02f, h) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 }; mesh.RecalculateBounds();
            var plane = new GameObject("Fog of war", typeof(MeshFilter), typeof(MeshRenderer)); plane.transform.SetParent(parent, false);
            plane.GetComponent<MeshFilter>().sharedMesh = mesh; plane.GetComponent<MeshRenderer>().sharedMaterial = material;
            Sync();
        }
        /// <summary>
        /// The local player's sight of every cell as the fog last drew it, row by row (FogOfWarSystem.CopyCells: 2 in
        /// sight, 1 explored). Brought up to date first, so the minimap reads this pass instead of asking cell by cell.
        /// </summary>
        public byte[] Cells { get { Sync(); return cells; } }

        public void Sync()
        {
            if (revision == world.Vision.Revision) return;
            revision = world.Vision.Revision;
            // One read of the whole map; the per-point queries cost a lookup each, twice for most cells.
            if (!world.Vision.CopyCells(1, cells)) Array.Clear(cells, 0, cells.Length);
            for (int index = 0; index < cells.Length; index++)
                pixels[index] = (cells[index] & 2) != 0 ? Visible : (cells[index] & 1) != 0 ? Explored : Unknown;
            texture.SetPixels32(pixels); texture.Apply(false, false);
        }
        public void Dispose() { UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(material); UnityEngine.Object.Destroy(mesh); }
    }
}
