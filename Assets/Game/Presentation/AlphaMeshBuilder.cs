using System.Collections.Generic;
using UnityEngine;

namespace Emberfield.Presentation
{
    internal sealed class AlphaMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float mask = 0)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < .000000001f) return;
            normal.Normalize(); color.a = mask;
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) { normals.Add(normal); colors.Add(color); triangles.Add(first + i); }
        }
        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float mask = 0)
        { Triangle(a, b, c, color, mask); Triangle(a, c, d, color, mask); }
        // Per-corner colour, for surfaces that carry a field across the sheet rather than a flat tone:
        // a shore gradient shaded per cell reads as a quilt, and the eye catches every seam.
        internal void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float mask = 0)
        {
            var normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < .000000001f) return;
            normal.Normalize();
            ca.a = cb.a = cc.a = mask;
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            for (int i = 0; i < 3; i++) { normals.Add(normal); triangles.Add(first + i); }
        }
        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd, float mask = 0)
        { Triangle(a, b, c, ca, cb, cc, mask); Triangle(a, c, d, ca, cc, cd, mask); }
        public void Box(Vector3 centre, Vector3 size, Color color, float mask = 0, Quaternion? rotation = null)
        {
            var q = rotation ?? Quaternion.identity; var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = centre + q * Vector3.Scale(size * .5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Quad(v[0], v[4], v[6], v[2], color, mask); Quad(v[1], v[3], v[7], v[5], color, mask);
            Quad(v[0], v[1], v[5], v[4], color, mask); Quad(v[2], v[6], v[7], v[3], color, mask);
            Quad(v[0], v[2], v[3], v[1], color, mask); Quad(v[4], v[5], v[7], v[6], color, mask);
        }
        public void Beam(Vector3 from, Vector3 to, float width, float depth, Color color, float mask = 0)
        { Box((from + to) * .5f, new Vector3(width, (to - from).magnitude, depth), color, mask, Quaternion.FromToRotation(Vector3.up, to - from)); }
        public void Frustum(Vector3 bottom, Vector3 top, Vector2 bottomRadius, Vector2 topRadius, int sides, Color color, float mask = 0)
        {
            for (int side = 0; side < sides; side++)
            {
                float a = (side + .5f) * Mathf.PI * 2 / sides, next = (side + 1.5f) * Mathf.PI * 2 / sides;
                var lowA = bottom + new Vector3(Mathf.Cos(a) * bottomRadius.x, 0, Mathf.Sin(a) * bottomRadius.y);
                var lowB = bottom + new Vector3(Mathf.Cos(next) * bottomRadius.x, 0, Mathf.Sin(next) * bottomRadius.y);
                var highA = top + new Vector3(Mathf.Cos(a) * topRadius.x, 0, Mathf.Sin(a) * topRadius.y);
                var highB = top + new Vector3(Mathf.Cos(next) * topRadius.x, 0, Mathf.Sin(next) * topRadius.y);
                Quad(lowA, highA, highB, lowB, color, mask);
                Triangle(bottom, lowA, lowB, color, mask); Triangle(top, highB, highA, color, mask);
            }
        }
        public void Rock(Vector3 centre, Vector3 size, int sides, Color color, float mask = 0)
        {
            var mid = centre + new Vector3(.035f, .02f, -.015f);
            Frustum(centre - Vector3.up * size.y * .5f, mid, new Vector2(size.x * .35f, size.z * .34f), new Vector2(size.x * .50f, size.z * .49f), sides, color, mask);
            Frustum(mid, centre + new Vector3(-.05f, size.y * .5f, .035f), new Vector2(size.x * .50f, size.z * .49f), new Vector2(size.x * .18f, size.z * .17f), sides, color, mask);
        }
        public void Tube(Vector3 from, Vector3 to, float radius, int sides, Color color, float mask = 0)
        {
            var direction = (to - from).normalized;
            var u = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right).normalized * radius;
            var v = Vector3.Cross(direction, u).normalized * radius;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, c = (i + 1) * Mathf.PI * 2 / sides;
                var one = u * Mathf.Cos(a) + v * Mathf.Sin(a); var two = u * Mathf.Cos(c) + v * Mathf.Sin(c);
                Quad(from + one, from + two, to + two, to + one, color, mask);
                Triangle(from, from + two, from + one, color, mask); Triangle(to, to + one, to + two, color, mask);
            }
        }
        public void Dome(Vector3 centre, Vector3 radius, int sides, int rings, Color color, float mask = 0)
        {
            for (int ring = 0; ring < rings; ring++)
            {
                float a = ring * Mathf.PI * .5f / rings, next = (ring + 1) * Mathf.PI * .5f / rings;
                Frustum(centre + Vector3.up * Mathf.Sin(a) * radius.y, centre + Vector3.up * Mathf.Sin(next) * radius.y,
                    new Vector2(radius.x, radius.z) * Mathf.Cos(a), new Vector2(radius.x, radius.z) * Mathf.Cos(next), sides, color, mask);
            }
        }
        public void Roof(Vector3 centre, Vector3 size, Color color, float mask = 0)
        {
            float x = size.x * .5f, y = size.y * .5f, z = size.z * .5f;
            var a = centre + new Vector3(-x, -y, -z); var b = centre + new Vector3(x, -y, -z);
            var c = centre + new Vector3(-x, -y, z); var d = centre + new Vector3(x, -y, z);
            var back = centre + new Vector3(0, y, -z); var front = centre + new Vector3(0, y, z);
            Quad(a, c, front, back, color, mask); Quad(b, back, front, d, color, mask);
            Triangle(a, back, b, color, mask); Triangle(c, d, front, color, mask); Quad(a, b, d, c, color, mask);
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
