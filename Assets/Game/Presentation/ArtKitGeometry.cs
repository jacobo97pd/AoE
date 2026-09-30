using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfield.Presentation
{
    // Original Emberfield forms, authored as editable geometry recipes. No imported source meshes.
    public static class ArtKitGeometry
    {
        private static readonly Color Plaster = new Color(.80f, .76f, .60f);
        private static readonly Color Linen = new Color(.92f, .90f, .78f);
        private static readonly Color Timber = new Color(.32f, .23f, .15f);
        private static readonly Color Dark = new Color(.13f, .18f, .19f);
        private static readonly Color Skin = new Color(.68f, .45f, .29f);
        private static readonly Color Iron = new Color(.47f, .55f, .55f);
        private static readonly Color Amber = new Color(.87f, .57f, .20f);

        public static Mesh Unit(string id, int lod)
        {
            var b = new Builder(); bool detailed = lod == 0; int sides = detailed ? 6 : 4;
            if (id == "strider") Mounted(b, detailed, sides);
            else if (id == "tender" || id == "reedguard" || id == "stringwarden")
            {
                bool worker = id == "tender", archer = id == "stringwarden";
                b.Beam(new Vector3(-.15f, .13f, .06f), new Vector3(-.13f, .67f, 0), .16f, .19f, Dark);
                b.Beam(new Vector3(.16f, .13f, -.03f), new Vector3(.13f, .67f, 0), .16f, .19f, Dark);
                b.Box(new Vector3(-.15f, .09f, .09f), new Vector3(.20f, .16f, .31f), Timber);
                b.Box(new Vector3(.16f, .09f, .01f), new Vector3(.20f, .16f, .31f), Timber);
                b.Frustum(new Vector3(0, .61f, 0), new Vector3(0, 1.17f, 0), new Vector2(.24f, .18f), new Vector2(.31f, .22f), sides, worker ? Plaster : Linen, worker ? 0 : 1);
                b.Frustum(new Vector3(0, 1.22f, .02f), new Vector3(0, 1.56f, .04f), new Vector2(.17f, .16f), new Vector2(.16f, .14f), sides, Skin);
                b.Box(new Vector3(0, .81f, .205f), new Vector3(.28f, .36f, .035f), Linen, 1);
                if (detailed)
                {
                    b.Box(new Vector3(0, .71f, 0), new Vector3(.50f, .08f, .39f), Timber);
                    b.Box(new Vector3(0, .72f, .22f), new Vector3(.085f, .09f, .04f), Amber);
                    b.Frustum(new Vector3(0, 1.50f, .035f), new Vector3(0, 1.65f, .025f), new Vector2(.19f, .17f), new Vector2(.12f, .10f), sides, worker ? Timber : Iron);
                }
                else b.Box(new Vector3(0, 1.56f, .035f), new Vector3(.35f, .15f, .30f), worker ? Timber : Iron);
                if (worker)
                {
                    b.Beam(new Vector3(-.29f, 1.10f, 0), new Vector3(-.34f, .70f, .13f), .15f, .17f, Plaster);
                    b.Beam(new Vector3(.29f, 1.10f, .02f), new Vector3(.44f, .79f, .18f), .15f, .17f, Plaster);
                    b.Box(new Vector3(0, .94f, -.29f), new Vector3(.49f, .52f, .27f), Timber);
                    // The fixed tactical camera commonly sees a Tender's back: wrap the pack
                    // as well as the front tab so ownership survives movement and LOD changes.
                    b.Box(new Vector3(0, .99f, -.30f), new Vector3(.51f, .18f, .30f), Linen, 1);
                    b.Box(new Vector3(0, .95f, -.44f), new Vector3(.08f, .55f, .04f), Linen, 1);
                    b.Beam(new Vector3(.44f, .46f, .18f), new Vector3(.50f, 1.26f, .20f), .065f, .065f, Timber);
                    b.Box(new Vector3(.52f, 1.26f, .20f), new Vector3(.29f, .09f, .16f), Iron);
                    if (detailed) b.Box(new Vector3(-.33f, .68f, -.10f), new Vector3(.22f, .28f, .19f), Plaster);
                }
                else if (!archer)
                {
                    b.Beam(new Vector3(.29f, 1.13f, 0), new Vector3(.40f, .88f, .12f), .17f, .18f, Linen, 1);
                    b.Beam(new Vector3(-.29f, 1.13f, 0), new Vector3(-.37f, .83f, .17f), .17f, .18f, Linen, 1);
                    b.Beam(new Vector3(.43f, .11f, .15f), new Vector3(.43f, 1.94f, .15f), .055f, .055f, Timber);
                    b.Frustum(new Vector3(.43f, 1.88f, .15f), new Vector3(.43f, 2.14f, .15f), new Vector2(.10f, .035f), Vector2.zero, 4, Iron);
                    b.Box(new Vector3(-.40f, .90f, .23f), new Vector3(.30f, .51f, .09f), Timber);
                    b.Box(new Vector3(-.40f, .90f, .285f), new Vector3(.23f, .41f, .04f), Linen, 1);
                    if (detailed) foreach (int direction in new[] { -1, 1 }) b.Box(new Vector3(direction * .26f, 1.15f, .02f), new Vector3(.22f, .13f, .39f), Iron);
                }
                else
                {
                    b.Beam(new Vector3(-.28f, 1.10f, 0), new Vector3(-.10f, 1.00f, .38f), .15f, .17f, Linen, 1);
                    b.Beam(new Vector3(.28f, 1.10f, 0), new Vector3(.35f, 1.12f, .39f), .15f, .17f, Linen, 1);
                    var left = new Vector3(-.54f, 1.02f, .30f); var middle = new Vector3(0, 1.10f, .65f); var right = new Vector3(.54f, 1.18f, .30f);
                    b.Beam(left, middle, .07f, .07f, Timber); b.Beam(middle, right, .07f, .07f, Timber);
                    b.Beam(left, right, .018f, .018f, Linen);
                    b.Box(new Vector3(.11f, 1.01f, -.27f), new Vector3(.22f, .51f, .21f), Timber);
                    if (detailed) for (int arrow = 0; arrow < 3; arrow++)
                    {
                        float x = .045f + arrow * .065f;
                        b.Beam(new Vector3(x, 1.12f, -.27f), new Vector3(x, 1.55f, -.32f), .025f, .025f, Timber);
                        b.Box(new Vector3(x, 1.48f, -.31f), new Vector3(.07f, .12f, .02f), Linen);
                    }
                }
            }
            else throw new ArgumentException("Unsupported unit recipe: " + id, nameof(id));
            return b.Mesh("Aven " + id + " LOD" + lod);
        }

        private static void Mounted(Builder b, bool detailed, int sides)
        {
            var coat = new Color(.46f, .33f, .21f);
            b.Frustum(new Vector3(0, .73f, -.02f), new Vector3(0, 1.13f, -.02f), new Vector2(.32f, .59f), new Vector2(.29f, .55f), sides, coat);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                b.Beam(new Vector3(x * .24f, .76f, z * .41f), new Vector3(x * .27f, .15f, z * .47f), .13f, .17f, coat);
                b.Box(new Vector3(x * .27f, .09f, z * .47f + .035f), new Vector3(.18f, .16f, .23f), Dark);
            }
            b.Frustum(new Vector3(0, .94f, .39f), new Vector3(0, 1.52f, .61f), new Vector2(.24f, .24f), new Vector2(.16f, .18f), sides, coat);
            b.Box(new Vector3(0, 1.51f, .77f), new Vector3(.33f, .25f, .46f), coat, 0, Quaternion.Euler(-12, 0, 0));
            b.Beam(new Vector3(0, .98f, -.56f), new Vector3(0, .45f, -.80f), .13f, .15f, Dark);
            b.Box(new Vector3(0, 1.12f, -.12f), new Vector3(.69f, .12f, .68f), Linen, 1);
            b.Frustum(new Vector3(0, 1.18f, -.15f), new Vector3(0, 1.74f, -.10f), new Vector2(.22f, .17f), new Vector2(.28f, .20f), sides, Linen, 1);
            b.Frustum(new Vector3(0, 1.80f, -.08f), new Vector3(0, 2.10f, -.06f), new Vector2(.16f, .15f), new Vector2(.15f, .13f), sides, Skin);
            b.Box(new Vector3(0, 2.08f, -.065f), new Vector3(.34f, .17f, .31f), Iron);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * .16f, 1.27f, -.13f), new Vector3(side * .38f, .76f, -.07f), .16f, .20f, Dark);
                b.Beam(new Vector3(side * .25f, 1.67f, -.06f), new Vector3(side * .39f, 1.40f, .22f), .15f, .18f, Linen, 1);
            }
            b.Beam(new Vector3(.44f, 1.37f, -.05f), new Vector3(.49f, 1.70f, 1.11f), .06f, .06f, Timber);
            b.Box(new Vector3(.49f, 1.69f, 1.08f), new Vector3(.09f, .09f, .20f), Iron, 0, Quaternion.Euler(-16, 0, 0));
            if (detailed)
            {
                for (int side = -1; side <= 1; side += 2)
                    b.Frustum(new Vector3(side * .105f, 1.62f, .65f), new Vector3(side * .12f, 1.85f, .62f), new Vector2(.065f, .045f), Vector2.zero, 3, coat);
                b.Beam(new Vector3(0, 1.65f, .47f), new Vector3(0, 1.09f, .28f), .09f, .08f, Dark);
                b.Box(new Vector3(0, 1.48f, .74f), new Vector3(.36f, .055f, .40f), Timber);
                b.Box(new Vector3(0, 1.38f, -.145f), new Vector3(.47f, .065f, .38f), Timber);
            }
        }

        // Normalized X/Z footprints let presentation apply the authored simulation dimensions exactly.
        public static Mesh Building(string id, int lod)
        {
            var b = new Builder(); bool detailed = lod == 0;
            b.Box(new Vector3(0, .075f, 0), new Vector3(.96f, .15f, .96f), Plaster);
            if (id == "hearth")
            {
                b.Box(new Vector3(0, .78f, -.07f), new Vector3(.68f, 1.26f, .69f), Plaster);
                b.Roof(new Vector3(0, 1.77f, -.07f), new Vector3(.84f, .75f, .85f), Timber);
                b.Roof(new Vector3(0, 1.785f, -.07f), new Vector3(.84f, .75f, .21f), Linen, 1);
                b.Box(new Vector3(0, 1.34f, .285f), new Vector3(.71f, .16f, .055f), Linen, 1);
                b.Box(new Vector3(0, .54f, .294f), new Vector3(.21f, .78f, .035f), Dark);
                b.Roof(new Vector3(-.29f, 1.10f, .19f), new Vector3(.34f, .32f, .43f), Timber);
                b.Roof(new Vector3(.29f, 1.10f, .19f), new Vector3(.34f, .32f, .43f), Timber);
                for (int side = -1; side <= 1; side += 2)
                    b.Box(new Vector3(side * .34f, .64f, .35f), new Vector3(.055f, 1.04f, .055f), Timber);
                if (detailed)
                {
                    b.Box(new Vector3(0, 2.17f, -.07f), new Vector3(.10f, .07f, .79f), Plaster);
                    for (int step = 0; step < 3; step++) b.Box(new Vector3(0, .12f + step * .065f, .43f - step * .04f), new Vector3(.27f, .09f, .11f), Plaster);
                    for (int side = -1; side <= 1; side += 2)
                        b.Box(new Vector3(side * .245f, .86f, .297f), new Vector3(.105f, .32f, .035f), Dark);
                }
            }
            else if (id == "shelter")
            {
                b.Box(new Vector3(0, .63f, -.03f), new Vector3(.70f, .96f, .68f), Plaster);
                b.Roof(new Vector3(0, 1.37f, -.03f), new Vector3(.91f, .58f, .87f), Timber);
                b.Roof(new Vector3(0, 1.385f, -.03f), new Vector3(.91f, .58f, .22f), Linen, 1);
                b.Box(new Vector3(.10f, .50f, .325f), new Vector3(.22f, .69f, .035f), Dark);
                b.Box(new Vector3(-.225f, .88f, .337f), new Vector3(.16f, .35f, .03f), Linen, 1);
                if (detailed)
                {
                    b.Box(new Vector3(-.23f, .43f, .38f), new Vector3(.23f, .20f, .15f), Timber);
                    b.Box(new Vector3(.235f, 1.46f, -.19f), new Vector3(.12f, .55f, .12f), Plaster);
                    b.Box(new Vector3(0, .12f, .42f), new Vector3(.42f, .10f, .15f), Plaster);
                }
            }
            else if (id == "muster_hall")
            {
                b.Box(new Vector3(0, .71f, -.20f), new Vector3(.83f, 1.13f, .43f), Plaster);
                b.Roof(new Vector3(0, 1.54f, -.20f), new Vector3(.94f, .57f, .57f), Timber);
                b.Roof(new Vector3(0, 1.555f, -.20f), new Vector3(.94f, .57f, .18f), Linen, 1);
                b.Box(new Vector3(0, .68f, .025f), new Vector3(.40f, .99f, .04f), Dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(new Vector3(side * .37f, .81f, .34f), new Vector3(.065f, 1.33f, .065f), Timber);
                    b.Box(new Vector3(side * .30f, 1.14f, .35f), new Vector3(.12f, .49f, .025f), Linen, 1);
                }
                b.Box(new Vector3(0, 1.47f, .34f), new Vector3(.83f, .10f, .10f), Timber);
                if (detailed)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        b.Box(new Vector3(side * .32f, .53f, .10f), new Vector3(.035f, .74f, .035f), Timber);
                        b.Box(new Vector3(side * .32f, .79f, .12f), new Vector3(.12f, .20f, .035f), Amber);
                        b.Beam(new Vector3(side * .37f, 1.08f, .34f), new Vector3(side * .20f, 1.43f, .34f), .035f, .04f, Timber);
                    }
                    b.Box(new Vector3(0, 1.29f, .038f), new Vector3(.77f, .09f, .04f), Linen, 1);
                }
            }
            else throw new ArgumentException("Unsupported building recipe: " + id, nameof(id));
            return b.Mesh("Aven " + id + " LOD" + lod);
        }

        public static Mesh Resource(Emberfield.Simulation.ResourceKind kind, int lod)
        {
            var b = new Builder(); bool detailed = lod == 0; int sides = detailed ? 6 : 4;
            if (kind == Emberfield.Simulation.ResourceKind.Wood)
            {
                b.Frustum(Vector3.zero, new Vector3(.035f, 1.66f, .02f), new Vector2(.17f, .14f), new Vector2(.10f, .09f), sides, Timber);
                b.Frustum(new Vector3(0, 1.07f, 0), new Vector3(-.08f, 1.89f, .02f), new Vector2(.64f, .53f), new Vector2(.32f, .27f), sides, new Color(.37f, .49f, .28f));
                b.Frustum(new Vector3(-.05f, 1.60f, .01f), new Vector3(.04f, 2.43f, -.02f), new Vector2(.48f, .40f), new Vector2(.12f, .10f), sides, new Color(.52f, .58f, .31f));
                if (detailed) b.Beam(new Vector3(.025f, .79f, 0), new Vector3(.47f, 1.50f, .18f), .10f, .12f, Timber);
            }
            else if (kind == Emberfield.Simulation.ResourceKind.Food)
            {
                b.Box(new Vector3(0, .055f, 0), new Vector3(1.17f, .11f, 1.10f), new Color(.30f, .27f, .15f));
                for (int row = -1; row <= 1; row++) for (int col = -1; col <= 1; col++)
                {
                    if (!detailed && row == 0 && col != 0) continue;
                    var bottom = new Vector3(col * .34f, .11f, row * .32f);
                    b.Frustum(bottom, bottom + new Vector3(.04f, .48f, -.02f), new Vector2(.16f, .14f), new Vector2(.08f, .06f), sides, new Color(.59f, .57f, .24f));
                    if (detailed) b.Box(bottom + new Vector3(.03f, .45f, 0), new Vector3(.13f, .15f, .13f), Amber);
                }
            }
            else if (kind == Emberfield.Simulation.ResourceKind.Metal || kind == Emberfield.Simulation.ResourceKind.Stone)
            {
                bool metal = kind == Emberfield.Simulation.ResourceKind.Metal;
                var color = metal ? new Color(.32f, .39f, .40f) : new Color(.62f, .62f, .53f);
                b.Rock(new Vector3(-.22f, .35f, -.08f), new Vector3(.70f, .70f, .72f), sides, color);
                b.Rock(new Vector3(.28f, .24f, .19f), new Vector3(.54f, .48f, .64f), sides, color * .88f);
                if (detailed) b.Rock(new Vector3(.20f, .20f, -.34f), new Vector3(.37f, .40f, .42f), sides, color * 1.10f);
                if (metal) for (int i = 0; i < (detailed ? 3 : 1); i++)
                    b.Frustum(new Vector3(-.35f + i * .16f, .45f, -.07f), new Vector3(-.31f + i * .17f, .89f - i * .07f, -.05f), new Vector2(.085f, .07f), new Vector2(.025f, .025f), 4, Amber);
            }
            else throw new ArgumentException("Unsupported resource recipe.", nameof(kind));
            return b.Mesh("Amber Reach " + kind + " LOD" + lod);
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float mask)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < .000000001f) return;
                normal.Normalize(); color.a = mask;
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                for (int i = 0; i < 3; i++) { normals.Add(normal); colors.Add(color); triangles.Add(first + i); }
            }
            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float mask)
            { Triangle(a, b, c, color, mask); Triangle(a, c, d, color, mask); }
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
            public void Rock(Vector3 centre, Vector3 size, int sides, Color color)
            {
                var mid = centre + new Vector3(.035f, .02f, -.015f);
                Frustum(centre - Vector3.up * size.y * .5f, mid, new Vector2(size.x * .35f, size.z * .34f), new Vector2(size.x * .50f, size.z * .49f), sides, color);
                Frustum(mid, centre + new Vector3(-.05f, size.y * .5f, .035f), new Vector2(size.x * .50f, size.z * .49f), new Vector2(size.x * .18f, size.z * .17f), sides, color);
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
}
