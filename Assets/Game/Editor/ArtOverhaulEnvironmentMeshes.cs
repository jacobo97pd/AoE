using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Editor
{
    internal sealed class ArtOverhaulEnvironmentMeshes
    {
        internal readonly struct Paint
        {
            internal readonly Color Color;
            internal readonly int Tile;
            internal readonly Vector2 Surface;
            internal Paint(Color color, int tile, float metal = 0, float smooth = .24f, float alpha = 0)
            { Color = new Color(color.r, color.g, color.b, alpha); Tile = tile; Surface = new Vector2(metal, smooth); }
        }
        private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> uv = new List<Vector2>(), surface = new List<Vector2>(), atlas = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        internal int VertexCount => vertices.Count;
        internal int Vertex(Vector3 p, Vector3 n, Vector2 tex, Paint paint)
        {
            int index = vertices.Count; vertices.Add(p); normals.Add(n.sqrMagnitude > .000001f ? n.normalized : Vector3.up);
            uv.Add(tex); colors.Add(paint.Color); surface.Add(paint.Surface); atlas.Add(new Vector2(paint.Tile, 0)); return index;
        }
        internal void Face(int a, int b, int c)
        {
            if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude < .00000001f) return;
            indices.Add(a); indices.Add(b); indices.Add(c);
        }
        internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Paint paint)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            var u=(b-a).normalized;var v=Vector3.Cross(n,u);const float density=.70f;
            Vector2 Tex(Vector3 p)=>new Vector2(Vector3.Dot(p-a,u),Vector3.Dot(p-a,v))*density;
            int i = Vertex(a, n, Tex(a), paint); Vertex(b, n, Tex(b), paint); Vertex(c, n, Tex(c), paint); Face(i, i + 1, i + 2);
        }
        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Paint paint, float repeat = 1)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            var u=(d-a).normalized;var v=(b-a-u*Vector3.Dot(b-a,u)).normalized;float density=.70f*repeat;
            Vector2 Tex(Vector3 p)=>new Vector2(Vector3.Dot(p-a,u),Vector3.Dot(p-a,v))*density;
            int i = Vertex(a, n, Tex(a), paint); Vertex(b, n, Tex(b), paint); Vertex(c, n, Tex(c), paint); Vertex(d, n, Tex(d), paint);
            Face(i, i + 1, i + 2); Face(i, i + 2, i + 3);
        }
        internal void Box(Vector3 center, Vector3 size, Paint paint, float bevel = .045f, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity; var half = size * .5f;
            float cut = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .45f);
            var rings = new Vector3[4, 8];
            for (int layer = 0; layer < 4; layer++)
            {
                float y = layer == 0 ? -half.y : layer == 1 ? -half.y + cut : layer == 2 ? half.y - cut : half.y;
                float shrink = layer == 0 || layer == 3 ? cut : 0;
                float x = half.x - shrink, z = half.z - shrink, corner = Mathf.Min(cut, Mathf.Min(x, z) * .5f);
                var points = new[] { new Vector2(-x + corner, -z), new Vector2(x - corner, -z), new Vector2(x, -z + corner), new Vector2(x, z - corner), new Vector2(x - corner, z), new Vector2(-x + corner, z), new Vector2(-x, z - corner), new Vector2(-x, -z + corner) };
                for (int k = 0; k < 8; k++) rings[layer, k] = center + q * new Vector3(points[k].x, y, points[k].y);
            }
            for (int layer = 0; layer < 3; layer++) for (int k = 0; k < 8; k++) Quad(rings[layer, k], rings[layer + 1, k], rings[layer + 1, (k + 1) % 8], rings[layer, (k + 1) % 8], paint);
            for (int k = 0; k < 8; k++)
            {
                Triangle(center - q * Vector3.up * half.y, rings[0, k], rings[0, (k + 1) % 8], paint);
                Triangle(center + q * Vector3.up * half.y, rings[3, (k + 1) % 8], rings[3, k], paint);
            }
        }
        internal void Beam(Vector3 a, Vector3 b, float width, float depth, Paint paint)
        { Box((a + b) * .5f, new Vector3(width, Vector3.Distance(a, b), depth), paint, Mathf.Min(width, depth) * .12f, Quaternion.FromToRotation(Vector3.up, b - a)); }
        internal void Frustum(Vector3 a, Vector3 b, float r0, float r1, Paint paint, int sides = 16)
        {
            var axis = (b - a).normalized; var q = Quaternion.FromToRotation(Vector3.up, axis); float length = Vector3.Distance(a, b);
            var bot = new int[sides+1]; var top = new int[sides+1];
            float circumference=Mathf.PI*(r0+r1)*.70f;
            for (int k = 0; k <= sides; k++)
            {
                float angle = k * Mathf.PI * 2 / sides; var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var n = q * new Vector3(radial.x, (r0 - r1) / Mathf.Max(.001f, length), radial.z).normalized;
                bot[k] = Vertex(a + q * radial * r0, n, new Vector2(k / (float)sides * circumference, 0), paint);
                top[k] = Vertex(b + q * radial * r1, n, new Vector2(k / (float)sides * circumference, length*.70f), paint);
            }
            for (int k = 0; k < sides; k++)
            {
                int next = k + 1; Face(bot[k], top[k], top[next]); Face(bot[k], top[next], bot[next]);
                Triangle(a, a + q * new Vector3(Mathf.Cos(k * Mathf.PI * 2 / sides), 0, Mathf.Sin(k * Mathf.PI * 2 / sides)) * r0, a + q * new Vector3(Mathf.Cos(next * Mathf.PI * 2 / sides), 0, Mathf.Sin(next * Mathf.PI * 2 / sides)) * r0, paint);
                Triangle(b, b + q * new Vector3(Mathf.Cos(next * Mathf.PI * 2 / sides), 0, Mathf.Sin(next * Mathf.PI * 2 / sides)) * r1, b + q * new Vector3(Mathf.Cos(k * Mathf.PI * 2 / sides), 0, Mathf.Sin(k * Mathf.PI * 2 / sides)) * r1, paint);
            }
        }
        internal void Ellipsoid(Vector3 center, Vector3 size, Paint paint, int sides = 16, int rings = 10, float irregularity = 0, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity; int start = vertices.Count;
            for (int y = 0; y <= rings; y++)
            {
                float latitude = -Mathf.PI * .5f + y * Mathf.PI / rings;
                for (int x = 0; x <= sides; x++)
                {
                    float a = x * Mathf.PI * 2 / sides;
                    var n = new Vector3(Mathf.Cos(latitude) * Mathf.Cos(a), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(a));
                    float wave = 1 + irregularity * (Mathf.Sin(a * 3 + latitude * 4) * .5f + Mathf.Cos(a * 5 - latitude * 3) * .5f);
                    var p = Vector3.Scale(n, size * .5f) * wave;
                    var normal = new Vector3(n.x / Mathf.Max(.001f, size.x), n.y / Mathf.Max(.001f, size.y), n.z / Mathf.Max(.001f, size.z)).normalized;
                    Vertex(center + q * p, q * normal, new Vector2(x / (float)sides * Mathf.PI*(size.x+size.z)*.5f*.70f, y / (float)rings * Mathf.PI*size.y*.5f*.70f), paint);
                }
            }
            for (int y = 0; y < rings; y++) for (int x = 0; x < sides; x++)
            { int a = start + y * (sides + 1) + x; Face(a, a + sides + 1, a + 1); Face(a + 1, a + sides + 1, a + sides + 2); }
        }
        internal void Arch(Vector3 origin, float width, float height, float depth, Paint paint, int segments = 14, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity; float radius = width * .5f, rise = height - radius, thickness = .22f;
            for (int side = -1; side <= 1; side += 2) Box(origin + q * new Vector3(side * (radius + thickness * .5f), rise * .5f, 0), new Vector3(thickness, rise, depth), paint, .035f, q);
            Vector3 Point(float angle, float r, float z) => origin + q * new Vector3(Mathf.Cos(angle) * r, rise + Mathf.Sin(angle) * r, z);
            for (int k = 0; k < segments; k++)
            {
                float a = k * Mathf.PI / segments, b = (k + 1) * Mathf.PI / segments;
                Quad(Point(b, radius, -depth * .5f), Point(b, radius + thickness, -depth * .5f), Point(a, radius + thickness, -depth * .5f), Point(a, radius, -depth * .5f), paint);
                Quad(Point(a, radius, depth * .5f), Point(a, radius + thickness, depth * .5f), Point(b, radius + thickness, depth * .5f), Point(b, radius, depth * .5f), paint);
                Quad(Point(b, radius + thickness, -depth * .5f), Point(b, radius + thickness, depth * .5f), Point(a, radius + thickness, depth * .5f), Point(a, radius + thickness, -depth * .5f), paint);
                Quad(Point(a, radius, -depth * .5f), Point(a, radius, depth * .5f), Point(b, radius, depth * .5f), Point(b, radius, -depth * .5f), paint);
            }
        }
        internal void Roof(Vector3 center, float width, float depth, float height, Paint paint, int rows = 7)
        {
            for (int side = -1; side <= 1; side += 2) for (int row = 0; row < rows; row++)
            {
                float a = row / (float)rows, b = (row + 1) / (float)rows;
                float x0 = side * width * .5f * a, x1 = side * width * .5f * b;
                float y0 = height * (1 - a) + Mathf.Sin(a * Mathf.PI) * .16f + .025f;
                float y1 = height * (1 - b) + Mathf.Sin(b * Mathf.PI) * .16f;
                int tiles = Mathf.Max(4, Mathf.RoundToInt(depth / .6f));
                for (int tile = 0; tile < tiles; tile++)
                {
                    float z0 = -depth * .5f + tile * depth / tiles, z1 = z0 + depth / tiles + .025f;
                    var p = new Paint(paint.Color * (1 - (tile + row * 3) % 4 * .022f), paint.Tile, paint.Surface.x, paint.Surface.y);
                    Vector3 P(float x, float y, float z) => center + new Vector3(x, y, z);
                    if (side > 0) Quad(P(x0, y0, z0), P(x0, y0, z1), P(x1, y1, z1), P(x1, y1, z0), p);
                    else Quad(P(x1, y1, z0), P(x1, y1, z1), P(x0, y0, z1), P(x0, y0, z0), p);
                }
            }
            Frustum(center + new Vector3(0, height + .025f, -depth * .52f), center + new Vector3(0, height + .025f, depth * .52f), .095f, .095f, paint, 10);
        }
        internal Mesh Finish(string name, bool smoothNormals = false)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, surface); mesh.SetUVs(2, atlas); mesh.SetTriangles(indices, 0);
            if (smoothNormals) mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
    }
}
