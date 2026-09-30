using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Shared original public scenery and resource silhouettes for the three playable biomes.</summary>
    internal static class AlphaBiomeGeometry
    {
        private static readonly Color Sand = new Color(.76f, .61f, .38f), WhiteStone = new Color(.81f, .79f, .62f);
        private static readonly Color Timber = new Color(.30f, .20f, .115f), Sail = new Color(.89f, .85f, .66f);
        private static readonly Color Leaf = new Color(.22f, .46f, .29f), Gold = new Color(.92f, .65f, .25f);

        internal static Mesh Resource(ResourceKind kind, string biome, int lod)
        {
            var b = new AlphaMeshBuilder(); bool detail = lod == 0;
            if (kind == ResourceKind.Wood) Palm(b, Vector3.zero, detail, biome == "desert");
            else
            {
                var trunk = new Vector3(0, 0, 0);
                b.Frustum(trunk, Vector3.up * .93f, new Vector2(.09f, .08f), new Vector2(.06f, .05f), detail ? 6 : 4, Timber);
                b.Rock(new Vector3(0, .90f, 0), new Vector3(1.17f, .93f, 1.08f), detail ? 8 : 5, Leaf);
                for (int i = 0; i < (detail ? 7 : 4); i++)
                {
                    float a = i * 2.39996f;
                    b.Rock(new Vector3(Mathf.Cos(a) * .42f, .71f + i % 3 * .15f, Mathf.Sin(a) * .39f), new Vector3(.15f, .17f, .14f), detail ? 6 : 4, biome == "caribbean" ? new Color(.94f, .56f, .16f) : new Color(.56f, .27f, .13f));
                }
                b.Box(new Vector3(.38f, .13f, .33f), new Vector3(.31f, .23f, .28f), Timber);
            }
            return b.Mesh("Alpha " + biome + " " + kind + " LOD" + lod);
        }
        private static void Palm(AlphaMeshBuilder b, Vector3 offset, bool detail, bool datePalm)
        {
            var basePoint = offset; var bend = offset + new Vector3(.12f, 1.35f, -.07f); var crown = offset + new Vector3(.29f, 2.85f, -.13f);
            b.Frustum(basePoint, bend, new Vector2(.16f, .15f), new Vector2(.13f, .12f), detail ? 8 : 5, Timber);
            b.Frustum(bend, crown, new Vector2(.13f, .12f), new Vector2(.08f, .08f), detail ? 8 : 5, Timber);
            for (int n = 0; n < (detail ? 8 : 5); n++)
            {
                float angle = n * Mathf.PI * 2 / (detail ? 8 : 5);
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var tangent = new Vector3(-direction.z, 0, direction.x);
                var elbow = crown + direction * .64f + Vector3.up * .25f;
                var tip = crown + direction * (datePalm ? 1.05f : 1.24f) - Vector3.up * .34f;
                b.Triangle(crown, elbow + tangent * .17f, tip, Leaf); b.Triangle(crown, tip, elbow - tangent * .17f, Leaf * 1.10f);
                b.Triangle(tip, elbow + tangent * .17f, crown, Leaf); b.Triangle(elbow - tangent * .17f, tip, crown, Leaf * 1.10f);
                if (detail) { b.Beam(crown, elbow, .025f, .025f, Leaf * 1.18f); b.Beam(elbow, tip, .018f, .018f, Leaf); }
            }
            for (int n = 0; n < (detail ? 4 : 2); n++) b.Rock(crown + new Vector3(n % 2 * .16f -.09f, -.17f, n / 2 * .16f -.09f), new Vector3(.15f, .20f, .16f), 5, datePalm ? new Color(.55f, .29f, .12f) : new Color(.43f, .32f, .17f));
            if (detail) for (int n = 0; n < 7; n++) b.Frustum(offset + new Vector3(n * .029f, .30f + n * .31f, -n * .013f), offset + new Vector3(n * .029f, .34f + n * .31f, -n * .013f), new Vector2(.15f - n * .009f, .145f - n * .009f), new Vector2(.15f - n * .009f, .145f - n * .009f), 7, Timber * 1.32f);
        }
        internal static Mesh Prop(string kind)
        {
            var b = new AlphaMeshBuilder();
            if (kind == "palm" || kind == "date_palm") Palm(b, Vector3.zero, true, kind == "date_palm");
            else if (kind == "ship") Ship(b);
            else if (kind == "harbor") Harbor(b);
            else if (kind == "ruins") Ruins(b);
            else if (kind == "pyramid") Pyramid(b);
            else if (kind == "caravan") Caravan(b);
            else if (kind == "windmill") Windmill(b);
            else if (kind == "village") Village(b);
            else if (kind == "windmill_sails") Sails(b);
            else if (kind == "bridge") Bridge(b);
            else if (kind == "stone_bridge") StoneBridge(b);
            else if (kind == "pier") Pier(b);
            else if (kind == "lighthouse") Lighthouse(b);
            else if (kind == "obelisk") Obelisk(b);
            else if (kind == "dune")
            {
                b.Rock(new Vector3(0, .34f, 0), new Vector3(4.1f, .88f, 2.9f), 7, Sand);
                b.Rock(new Vector3(-1.4f, .17f, .7f), new Vector3(3.3f, .46f, 2.6f), 6, Sand * 1.06f);
            }
            else if (kind == "desert_rock")
            {
                b.Rock(new Vector3(-.13f, .44f, .03f), new Vector3(.70f, .88f, .77f), 6, Sand);
                b.Rock(new Vector3(.23f, .25f, -.17f), new Vector3(.42f, .50f, .45f), 5, Sand * .80f);
            }
            else if (kind == "coastal_rock")
            {
                b.Rock(new Vector3(-.13f, .31f, .03f), new Vector3(.70f, .62f, .77f), 7, WhiteStone);
                b.Rock(new Vector3(.23f, .21f, -.17f), new Vector3(.42f, .42f, .45f), 5, WhiteStone * .81f);
            }
            else if (kind == "dry_grass")
            {
                for (int n = 0; n < 9; n++)
                {
                    float a = n * 2.39996f; var root = new Vector3(Mathf.Cos(a) * .17f, 0, Mathf.Sin(a) * .17f);
                    b.Triangle(root - Vector3.right * .028f, root + new Vector3(Mathf.Cos(a) * .13f, .20f + n % 3 * .06f, Mathf.Sin(a) * .13f), root + Vector3.right * .028f, new Color(.69f, .59f, .32f));
                }
            }
            return b.Mesh("Alpha biome scenery " + kind);
        }
        private static void Ship(AlphaMeshBuilder b)
        {
            b.Frustum(new Vector3(0, .18f, 0), new Vector3(0, .90f, 0), new Vector2(.68f, 2.3f), new Vector2(1.0f, 2.7f), 8, Timber);
            b.Box(new Vector3(0, .92f, -.10f), new Vector3(1.51f, .11f, 4.33f), new Color(.62f, .45f, .25f));
            b.Beam(new Vector3(0, .70f, 0), new Vector3(0, 5.38f, 0), .16f, .16f, Timber);
            b.Beam(new Vector3(-1.49f, 4.65f, 0), new Vector3(1.49f, 4.65f, 0), .12f, .12f, Timber);
            b.Quad(new Vector3(-1.35f, 4.62f, .03f), new Vector3(-1.19f, 2.65f, .24f), new Vector3(1.19f, 2.65f, .24f), new Vector3(1.35f, 4.62f, .03f), Sail);
            b.Quad(new Vector3(1.35f, 4.62f, -.03f), new Vector3(1.19f, 2.65f, .22f), new Vector3(-1.19f, 2.65f, .22f), new Vector3(-1.35f, 4.62f, -.03f), Sail);
            b.Box(new Vector3(0, 3.60f, .27f), new Vector3(.35f, .65f, .025f), new Color(.14f, .39f, .45f));
            b.Box(new Vector3(0, 1.26f, -1.65f), new Vector3(1.34f, .66f, .88f), Timber);
            b.Roof(new Vector3(0, 1.72f, -1.65f), new Vector3(1.51f, .34f, 1.0f), new Color(.22f, .38f, .42f));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * .83f, 1.04f, -1.82f), new Vector3(side * .88f, 1.05f, 1.73f), .12f, .14f, Timber);
                b.Beam(new Vector3(0, 5.18f, 0), new Vector3(side * .89f, 1.09f, 1.61f), .018f, .018f, Sail);
                b.Beam(new Vector3(0, 5.18f, 0), new Vector3(side * .87f, 1.11f, -1.80f), .018f, .018f, Sail);
            }
            b.Beam(new Vector3(0, 1.08f, 1.85f), new Vector3(0, 1.49f, 3.3f), .11f, .12f, Timber);
            b.Box(new Vector3(.35f, 5.22f, 0), new Vector3(.70f, .32f, .035f), new Color(.12f, .38f, .46f));
        }
        private static void Harbor(AlphaMeshBuilder b)
        {
            for (int n = 0; n < 18; n++) b.Box(new Vector3(0, .18f, -2.65f + n * .31f), new Vector3(1.92f, .14f, .27f), Timber * (n % 2 == 0 ? 1 : 1.08f));
            for (int side = -1; side <= 1; side += 2) for (int n = -1; n <= 1; n++)
            {
                b.Box(new Vector3(side * .90f, .12f, n * 2.42f), new Vector3(.19f, 1.12f, .19f), Timber);
                b.Frustum(new Vector3(side * .90f, .55f, n * 2.42f), new Vector3(side * .90f, .69f, n * 2.42f), new Vector2(.12f, .12f), new Vector2(.12f, .12f), 6, Sail);
            }
            b.Box(new Vector3(.32f, .53f, -1.76f), new Vector3(.65f, .60f, .54f), Timber * 1.18f);
            b.Box(new Vector3(-.40f, .39f, -1.95f), new Vector3(.46f, .33f, .64f), Gold);
        }
        private static void Ruins(AlphaMeshBuilder b)
        {
            b.Box(new Vector3(0, .09f, 0), new Vector3(2.95f, .18f, 2.45f), WhiteStone * .88f);
            for (int side = -1; side <= 1; side += 2)
            {
                float h = side < 0 ? 2.30f : 1.37f;
                b.Frustum(new Vector3(side * .90f, .18f, -.51f), new Vector3(side * .90f, h, -.51f), new Vector2(.24f, .24f), new Vector2(.20f, .20f), 8, WhiteStone);
                b.Box(new Vector3(side * .90f, h + .06f, -.51f), new Vector3(.61f, .17f, .58f), WhiteStone);
            }
            b.Box(new Vector3(-.49f, 2.51f, -.51f), new Vector3(1.38f, .25f, .52f), WhiteStone);
            b.Rock(new Vector3(.70f, .38f, .45f), new Vector3(.90f, .59f, .72f), 5, WhiteStone);
            b.Box(new Vector3(-.40f, .35f, .21f), new Vector3(.76f, .35f, .47f), WhiteStone);
        }
        private static void Pyramid(AlphaMeshBuilder b)
        {
            for (int i = 0; i < 6; i++)
            {
                float size = 5.6f - i * .74f;
                b.Box(new Vector3(0, .25f + i * .52f, 0), new Vector3(size, .50f, size), Sand * (1 + i * .025f));
            }
            b.Frustum(Vector3.up * 3.11f, Vector3.up * 4.44f, new Vector2(.84f, .84f), Vector2.zero, 4, Gold);
            b.Box(new Vector3(0, .77f, 2.805f), new Vector3(.72f, 1.35f, .025f), new Color(.18f, .14f, .10f));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Frustum(new Vector3(side * 2.31f, 0, 2.91f), new Vector3(side * 2.31f, 2.25f, 2.91f), new Vector2(.20f, .20f), new Vector2(.15f, .15f), 4, Sand);
                b.Frustum(new Vector3(side * 2.31f, 2.25f, 2.91f), new Vector3(side * 2.31f, 2.68f, 2.91f), new Vector2(.18f, .18f), Vector2.zero, 4, Gold);
            }
        }
        // A crossing is built along +Z and the map rotates it, so one mesh serves a river running either way.
        private static void Bridge(AlphaMeshBuilder b)
        {
            for (int n = 0; n < 23; n++)
                b.Box(new Vector3(0, .34f, -3.52f + n * .32f), new Vector3(2.68f, .10f, .27f), Timber * (n % 2 == 0 ? 1.15f : 1.34f));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * 1.16f, .24f, -3.55f), new Vector3(side * 1.16f, .24f, 3.55f), .17f, .18f, Timber);
                b.Beam(new Vector3(side * 1.44f, .97f, -3.35f), new Vector3(side * 1.44f, .97f, 3.35f), .10f, .10f, Timber * 1.12f);
                for (int post = -3; post <= 3; post++)
                    b.Beam(new Vector3(side * 1.44f, .34f, post * 1.06f), new Vector3(side * 1.44f, 1.02f, post * 1.06f), .12f, .12f, Timber);
                foreach (float z in new[] { -1.95f, 0f, 1.95f })
                {
                    b.Beam(new Vector3(side * 1.12f, -.40f, z), new Vector3(side * 1.12f, .27f, z), .21f, .21f, Timber * .84f);
                    b.Beam(new Vector3(side * 1.12f, .12f, z), new Vector3(side * .55f, .28f, z), .10f, .10f, Timber * .9f);
                }
            }
            foreach (float z in new[] { -3.72f, 3.72f })
            {
                b.Box(new Vector3(0, .17f, z), new Vector3(3.3f, .48f, .92f), WhiteStone * .88f);
                b.Rock(new Vector3(1.75f, .22f, z), new Vector3(.8f, .5f, .7f), 5, WhiteStone * .8f);
                b.Rock(new Vector3(-1.72f, .20f, z), new Vector3(.7f, .44f, .66f), 5, WhiteStone * .84f);
            }
        }
        private static void StoneBridge(AlphaMeshBuilder b)
        {
            var stone = WhiteStone * .93f;
            b.Box(new Vector3(0, .44f, 0), new Vector3(3.1f, .28f, 7.4f), stone);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(side * 1.4f, .76f, 0), new Vector3(.3f, .42f, 7.4f), stone * 1.07f);
                for (int post = -1; post <= 1; post++)
                    b.Box(new Vector3(side * 1.4f, 1.04f, post * 2.7f), new Vector3(.44f, .24f, .44f), stone * 1.14f);
            }
            foreach (float z in new[] { -1.85f, 1.85f })
            {
                b.Box(new Vector3(0, -.05f, z), new Vector3(2.9f, .82f, .92f), stone * .86f);
                b.Box(new Vector3(0, .34f, z * .52f), new Vector3(2.7f, .22f, 1.5f), stone * .9f, 0, Quaternion.Euler(z > 0 ? -17 : 17, 0, 0));
            }
            foreach (float z in new[] { -3.5f, 3.5f }) b.Box(new Vector3(0, .22f, z), new Vector3(3.5f, .54f, 1.2f), stone * .82f);
        }
        private static void Pier(AlphaMeshBuilder b)
        {
            for (int n = 0; n < 34; n++) b.Box(new Vector3(0, .26f, -5.2f + n * .31f), new Vector3(2.05f, .12f, .27f), Timber * (n % 2 == 0 ? 1.05f : 1.16f));
            b.Box(new Vector3(0, .26f, 5.4f), new Vector3(4.6f, .12f, 1.9f), Timber * 1.1f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = -5f; z <= 5.6f; z += 1.75f) b.Beam(new Vector3(side * .93f, -.45f, z), new Vector3(side * .93f, .22f, z), .19f, .19f, Timber * .85f);
                b.Beam(new Vector3(side * 2.1f, -.45f, 5.4f), new Vector3(side * 2.1f, .22f, 5.4f), .19f, .19f, Timber * .85f);
                b.Frustum(new Vector3(side * 2.1f, .22f, 5.4f), new Vector3(side * 2.1f, .78f, 5.4f), new Vector2(.14f, .14f), new Vector2(.12f, .12f), 6, Timber);
            }
            b.Box(new Vector3(.62f, .58f, 4.6f), new Vector3(.72f, .52f, .58f), Timber * 1.2f);
            b.Box(new Vector3(-.58f, .46f, 5.9f), new Vector3(.5f, .36f, .66f), Gold);
        }
        private static void Lighthouse(AlphaMeshBuilder b)
        {
            b.Frustum(Vector3.zero, Vector3.up * .55f, new Vector2(1.72f, 1.72f), new Vector2(1.5f, 1.5f), 8, WhiteStone * .85f);
            for (int band = 0; band < 5; band++)
                b.Frustum(Vector3.up * (.55f + band * 1.1f), Vector3.up * (.55f + (band + 1) * 1.1f),
                    new Vector2(1.32f - band * .17f, 1.32f - band * .17f), new Vector2(1.15f - band * .17f, 1.15f - band * .17f), 8,
                    band % 2 == 0 ? WhiteStone : new Color(.63f, .24f, .19f));
            b.Frustum(Vector3.up * 6.05f, Vector3.up * 6.35f, new Vector2(.72f, .72f), new Vector2(.86f, .86f), 8, WhiteStone * .78f);
            b.Frustum(Vector3.up * 6.35f, Vector3.up * 7.25f, new Vector2(.62f, .62f), new Vector2(.62f, .62f), 8, Gold);
            b.Frustum(Vector3.up * 7.25f, Vector3.up * 7.95f, new Vector2(.78f, .78f), Vector2.zero, 8, new Color(.24f, .33f, .35f));
            b.Box(new Vector3(0, 1.15f, 1.38f), new Vector3(.62f, 1.3f, .06f), Timber);
        }
        private static void Obelisk(AlphaMeshBuilder b)
        {
            b.Box(new Vector3(0, .22f, 0), new Vector3(2.4f, .44f, 2.4f), Sand * 1.02f);
            b.Box(new Vector3(0, .62f, 0), new Vector3(1.75f, .4f, 1.75f), Sand * .96f);
            b.Frustum(Vector3.up * .82f, Vector3.up * 5.3f, new Vector2(.62f, .62f), new Vector2(.4f, .4f), 4, Sand * 1.05f);
            b.Frustum(Vector3.up * 5.3f, Vector3.up * 6.05f, new Vector2(.4f, .4f), Vector2.zero, 4, Gold);
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(side * .64f, 2.9f, 0), new Vector3(.03f, 3.1f, .5f), new Color(.52f, .40f, .24f));
        }
        private static void Caravan(AlphaMeshBuilder b)
        {
            for (int n = 0; n < 2; n++)
            {
                var p = new Vector3(n * 1.9f - .9f, 0, n * .23f);
                b.Rock(p + new Vector3(0, 1.12f, 0), new Vector3(.56f, .66f, 1.15f), 6, Sand * .76f);
                b.Rock(p + new Vector3(0, 1.48f, -.11f), new Vector3(.46f, .47f, .42f), 5, Sand * .76f);
                b.Beam(p + new Vector3(0, 1.08f, .42f), p + new Vector3(0, 1.83f, .73f), .18f, .19f, Sand * .76f);
                b.Box(p + new Vector3(0, 1.86f, .85f), new Vector3(.25f, .26f, .41f), Sand * .76f);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) b.Beam(p + new Vector3(x * .18f, 1.02f, z * .38f), p + new Vector3(x * .21f, .09f, z * .42f), .10f, .10f, Sand * .76f);
                b.Box(p + new Vector3(0, 1.45f, -.11f), new Vector3(.88f, .20f, .77f), new Color(.23f, .41f, .44f));
            }
        }
        /// <summary>The mill without its sails: those are their own mesh so that they can turn.</summary>
        private static void Windmill(AlphaMeshBuilder b)
        {
            b.Frustum(Vector3.zero, Vector3.up * 3.20f, new Vector2(.89f, .84f), new Vector2(.52f, .51f), 8, WhiteStone);
            b.Frustum(Vector3.up * 3.18f, Vector3.up * 4.04f, new Vector2(.73f, .71f), Vector2.zero, 8, new Color(.23f, .31f, .32f));
            b.Frustum(new Vector3(0, 2.91f, -.70f), new Vector3(0, 2.91f, -.86f), new Vector2(.19f, .19f), new Vector2(.15f, .15f), 6, Timber);
            b.Box(new Vector3(0, .63f, -.834f), new Vector3(.40f, 1.17f, .035f), Timber);
        }

        /// <summary>Four sails around their own origin, so a spin about local forward is all they need.</summary>
        private static void Sails(AlphaMeshBuilder b)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * .5f + .30f;
                var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var across = new Vector3(-direction.y, direction.x, 0);
                b.Beam(Vector3.zero, direction * 1.91f, .095f, .09f, Timber);
                b.Quad(direction * .59f, direction * 1.79f, direction * 1.79f + across * .39f, direction * .59f + across * .39f, Sail);
                b.Quad(direction * .59f + across * .39f, direction * 1.79f + across * .39f, direction * 1.79f, direction * .59f, Sail);
            }
            b.Frustum(new Vector3(0, 0, -.07f), new Vector3(0, 0, .12f), new Vector2(.14f, .14f), new Vector2(.11f, .11f), 6, Timber);
        }
        private static void Village(AlphaMeshBuilder b)
        {
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(i % 2 * 1.95f -.9f, 0, i / 2 * 2.1f -.8f);
                b.Box(p + Vector3.up * .68f, new Vector3(1.43f, 1.35f, 1.52f), WhiteStone);
                b.Roof(p + Vector3.up * 1.61f, new Vector3(1.65f, .66f, 1.71f), new Color(.36f, .28f, .20f));
                b.Box(p + new Vector3(0, .53f, -.779f), new Vector3(.32f, .94f, .025f), Timber);
            }
            b.Box(new Vector3(-1.49f, 1.19f, 1.18f), new Vector3(.73f, 2.36f, .73f), WhiteStone);
            for (int side = -1; side <= 1; side += 2) b.Box(new Vector3(-1.49f + side * .24f, 2.51f, 1.18f), new Vector3(.20f, .31f, .73f), WhiteStone);
        }
    }
}
