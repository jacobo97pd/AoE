using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Editable geometric art recipes. Geometry is cosmetic and has no colliders or rule-state access.</summary>
    public static class AlphaWorldGeometry
    {
        private static readonly Color Stone = new Color(.66f, .66f, .55f), Plaster = new Color(.85f, .80f, .63f);
        private static readonly Color Cloth = new Color(.95f, .91f, .77f), Timber = new Color(.27f, .18f, .115f);
        private static readonly Color Roof = new Color(.21f, .30f, .31f), Leather = new Color(.37f, .23f, .13f);
        private static readonly Color Dark = new Color(.075f, .115f, .12f), Gold = new Color(.95f, .65f, .25f);
        private static readonly Color Steel = new Color(.58f, .68f, .67f), Skin = new Color(.71f, .49f, .32f);

        public static Mesh Unit(string id, FactionKind faction, int lod)
        {
            if (AlphaExpandedGeometry.SpecialUnit(id)) return AlphaExpandedGeometry.Unit(id, faction, lod);
            var b = new AlphaMeshBuilder(); bool detail = lod == 0, march = faction == FactionKind.SerevinMarch;
            int sides = detail ? 8 : 5;
            if (id == "supply_cart") { Cart(b, detail); return b.Mesh("Alpha " + faction + " " + id + " LOD" + lod); }
            bool mounted = AlphaWorldArt.Mounted(id);
            bool worker = id == "tender", archer = id == "stringwarden" || id == "camel_archer", support = id == "threadkeeper";
            float lift = mounted ? .57f : 0;
            if (mounted) Horse(b, detail, sides, march);
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * .14f, .68f + lift, -.04f);
                var foot = new Vector3(side * (mounted ? .38f : .16f), (mounted ? .57f : .12f), side * .035f);
                b.Beam(hip, foot, .18f, .22f, Dark);
                b.Box(foot, new Vector3(.22f, .19f, .34f), Leather);
                b.Beam(new Vector3(side * .3f, 1.12f + lift, 0), new Vector3(side * .39f, .82f + lift, .19f), .18f, .20f, march ? Leather : Cloth, march ? 0 : 1);
                b.Frustum(new Vector3(side * .39f, .8f + lift, .19f), new Vector3(side * .39f, .94f + lift, .19f), new Vector2(.09f, .095f), new Vector2(.08f, .08f), 5, Skin);
            }
            b.Frustum(new Vector3(0, .54f + lift, -.01f), new Vector3(0, 1.20f + lift, 0), new Vector2(.28f, .21f), new Vector2(.31f, .23f), sides, Cloth, 1);
            b.Frustum(new Vector3(0, 1.22f + lift, .02f), new Vector3(0, 1.60f + lift, .045f), new Vector2(.17f, .16f), new Vector2(.16f, .145f), sides, Skin);
            b.Box(new Vector3(0, .77f + lift, 0), new Vector3(.58f, .095f, .45f), Leather);
            b.Box(new Vector3(0, .78f + lift, .246f), new Vector3(.105f, .12f, .055f), Gold);
            if (!worker)
            {
                // Broad shoulder mantle and full rear cloak make faction and ownership readable from overhead.
                b.Frustum(new Vector3(0, 1.03f + lift, -.045f), new Vector3(0, 1.24f + lift, -.015f), new Vector2(.35f, .26f), new Vector2(.27f, .20f), sides, march ? Leather : Steel);
                b.Box(new Vector3(0, .91f + lift, -.267f), new Vector3(march ? .54f : .45f, .63f, .055f), Cloth, 1, Quaternion.Euler(march ? -12 : -5, 0, march ? 9 : 0));
                if (detail) b.Box(new Vector3(0, .70f + lift, -.30f), new Vector3(.44f, .07f, .04f), Gold);
            }
            if (worker || archer || support)
            {
                b.Frustum(new Vector3(0, 1.52f + lift, .035f), new Vector3(0, 1.71f + lift, .015f), new Vector2(worker ? .24f : .19f, .19f), new Vector2(.12f, .12f), sides, worker ? Leather : Cloth, worker ? 0 : 1);
                if (detail && worker) b.Frustum(new Vector3(0, 1.54f, .035f), new Vector3(0, 1.585f, .035f), new Vector2(.29f, .25f), new Vector2(.29f, .25f), 8, Leather);
            }
            else
            {
                b.Frustum(new Vector3(0, 1.49f + lift, .035f), new Vector3(0, 1.78f + lift, .015f), new Vector2(.20f, .19f), new Vector2(march ? .06f : .14f, .09f), sides, Steel);
                if (detail) { b.Box(new Vector3(0, 1.55f + lift, .198f), new Vector3(.29f, .085f, .04f), Dark); b.Box(new Vector3(0, 1.65f + lift, .20f), new Vector3(.045f, .24f, .06f), Gold); }
                if (mounted) b.Box(new Vector3(0, 1.81f + lift, -.02f), new Vector3(.055f, .13f, .36f), Cloth, 1);
            }
            if (worker || support)
            {
                b.Box(new Vector3(0, 1.00f, -.36f), new Vector3(support ? .68f : .48f, support ? .79f : .55f, .36f), Timber);
                b.Box(new Vector3(0, 1.12f, -.55f), new Vector3(support ? .7f : .5f, .18f, .04f), Cloth, 1);
                if (detail) for (int side = -1; side <= 1; side += 2) b.Beam(new Vector3(side * .22f, .72f, -.55f), new Vector3(side * .22f, 1.38f, -.55f), .055f, .055f, Gold);
                b.Beam(new Vector3(.47f, .36f, .20f), new Vector3(.49f, support ? 2.20f : 1.33f, .20f), .065f, .065f, Timber);
                if (support)
                {
                    b.Box(new Vector3(.60f, 1.91f, .20f), new Vector3(.29f, .50f, .035f), Cloth, 1);
                    b.Frustum(new Vector3(.49f, 2.14f, .20f), new Vector3(.49f, 2.35f, .20f), new Vector2(.11f, .075f), Vector2.zero, 4, Gold);
                }
                else { b.Box(new Vector3(.51f, 1.33f, .20f), new Vector3(.32f, .10f, .18f), Steel); }
            }
            else if (archer)
            {
                // A mounted archer (the camel archer) draws from the saddle: the bow and quiver ride up with the rider.
                var bowA = new Vector3(-.59f, .97f + lift, .30f); var bowB = new Vector3(0, 1.13f + lift, .69f); var bowC = new Vector3(.59f, 1.30f + lift, .31f);
                b.Beam(bowA, bowB, .065f, .065f, Timber); b.Beam(bowB, bowC, .065f, .065f, Timber); b.Beam(bowA, bowC, .016f, .016f, Cloth);
                b.Box(new Vector3(.23f, 1.14f + lift, -.39f), new Vector3(.23f, .60f, .18f), Leather);
                if (detail) for (int n = 0; n < 4; n++) { b.Beam(new Vector3(.14f + n * .055f, 1.24f + lift, -.4f), new Vector3(.14f + n * .055f, 1.68f + lift, -.44f), .022f, .022f, Timber); b.Box(new Vector3(.14f + n * .055f, 1.62f + lift, -.44f), new Vector3(.045f, .10f, .03f), Cloth); }
            }
            else
            {
                var basePoint = new Vector3(.48f, mounted ? 1.15f : .10f, .20f);
                var tip = new Vector3(.48f, mounted ? 2.25f : 2.37f, mounted ? 1.15f : .20f);
                b.Beam(basePoint, tip, .055f, .055f, Timber);
                b.Frustum(tip - Vector3.up * .10f, tip + Vector3.up * .20f, new Vector2(.105f, .045f), Vector2.zero, 4, Steel);
                Shield(b, new Vector3(-.43f, .99f + lift, .31f), march);
                if (detail) { b.Box(new Vector3(-.43f, .99f + lift, .401f), new Vector3(.06f, .59f, .025f), Gold); b.Box(new Vector3(-.43f, .99f + lift, .402f), new Vector3(.40f, .065f, .025f), Gold); }
            }
            AlphaExpandedGeometry.AddUnitIdentity(b, faction, lift, detail, worker);
            return b.Mesh("Alpha " + faction + " " + id + " LOD" + lod);
        }

        private static void Shield(AlphaMeshBuilder b, Vector3 centre, bool march)
        {
            var profile = march ? new[] { new Vector2(-.26f, .30f), new Vector2(.26f, .30f), new Vector2(.23f, -.13f), new Vector2(0, -.43f), new Vector2(-.23f, -.13f) }
                : new[] { new Vector2(-.26f, .31f), new Vector2(.26f, .31f), new Vector2(.29f, -.22f), new Vector2(.16f, -.36f), new Vector2(-.16f, -.36f), new Vector2(-.29f, -.22f) };
            for (int i = 0; i < profile.Length; i++)
            {
                Vector3 a = new Vector3(profile[i].x, profile[i].y, 0), next = new Vector3(profile[(i + 1) % profile.Length].x, profile[(i + 1) % profile.Length].y, 0);
                b.Triangle(centre + Vector3.forward * .055f, centre + next + Vector3.forward * .055f, centre + a + Vector3.forward * .055f, Cloth, 1);
                b.Triangle(centre - Vector3.forward * .055f, centre + a - Vector3.forward * .055f, centre + next - Vector3.forward * .055f, Timber);
                b.Quad(centre + a - Vector3.forward * .055f, centre + a + Vector3.forward * .055f, centre + next + Vector3.forward * .055f, centre + next - Vector3.forward * .055f, Gold);
            }
        }

        public static Mesh Resource(ResourceKind kind, int lod, string biomeId = "forest")
        {
            if (biomeId != "forest" && (kind == ResourceKind.Wood || kind == ResourceKind.Food)) return AlphaBiomeGeometry.Resource(kind, biomeId, lod);
            var b = new AlphaMeshBuilder(); bool detail = lod == 0; int sides = detail ? 7 : 5;
            if (kind == ResourceKind.Wood)
            {
                b.Frustum(Vector3.zero, new Vector3(.04f, 2.34f, .02f), new Vector2(.22f, .19f), new Vector2(.10f, .09f), sides, Timber);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(new Vector3(0, 1.15f, 0), new Vector3(side * .65f, 2.36f, side * .23f), .12f, .14f, Timber);
                    if (detail) b.Beam(new Vector3(0, .35f, 0), new Vector3(side * .40f, .12f, side * .19f), .14f, .17f, Timber);
                }
                b.Rock(new Vector3(-.40f, 2.28f, .13f), new Vector3(1.40f, 1.43f, 1.40f), sides, new Color(.24f, .40f, .24f));
                b.Rock(new Vector3(.39f, 2.65f, -.18f), new Vector3(1.47f, 1.40f, 1.34f), sides, new Color(.40f, .50f, .26f));
                b.Rock(new Vector3(.0f, 3.17f, .07f), new Vector3(1.18f, 1.02f, 1.12f), sides, new Color(.52f, .58f, .29f));
                if (detail) b.Rock(new Vector3(.62f, 2.20f, .26f), new Vector3(.67f, .72f, .73f), 5, new Color(.35f, .46f, .24f));
            }
            else if (kind == ResourceKind.Food)
            {
                b.Box(new Vector3(0, .025f, 0), new Vector3(1.36f, .05f, 1.24f), new Color(.35f, .29f, .15f));
                for (int row = -1; row <= 1; row++) for (int col = -1; col <= 1; col++)
                {
                    var p = new Vector3(col * .37f, .05f, row * .35f);
                    b.Frustum(p, p + new Vector3(.02f, .70f + (row + col + 2) % 3 * .08f, 0), new Vector2(.015f, .015f), new Vector2(.012f, .012f), 4, Gold);
                    for (int leaf = 0; leaf < (detail ? 3 : 1); leaf++)
                    {
                        float y = .23f + leaf * .15f;
                        b.Triangle(p + new Vector3(-.02f, y, 0), p + new Vector3(-.19f, y + .16f, .02f), p + new Vector3(.03f, y + .07f, 0), new Color(.60f, .61f, .27f));
                        b.Triangle(p + new Vector3(.02f, y + .05f, 0), p + new Vector3(.21f, y + .19f, -.02f), p + new Vector3(-.03f, y + .12f, 0), new Color(.60f, .61f, .27f));
                    }
                    b.Frustum(p + new Vector3(.02f, .61f, 0), p + new Vector3(.02f, .87f, 0), new Vector2(.075f, .05f), new Vector2(.02f, .02f), sides, Gold);
                }
                if (detail) for (int side = -1; side <= 1; side += 2) b.Box(new Vector3(side * .68f, .07f, 0), new Vector3(.045f, .10f, 1.25f), Timber);
            }
            else
            {
                // A seam has to be told apart from the scenery outcrops at a glance, from a tilted camera and
                // at any zoom: dark host rock for metal, bright ore standing proud of it; cut blocks for stone.
                bool metal = kind == ResourceKind.Metal;
                Color stone = metal ? new Color(.20f, .25f, .27f) : Stone;
                b.Rock(new Vector3(-.23f, .48f, -.06f), new Vector3(.94f, .96f, 1.02f), sides, stone);
                b.Rock(new Vector3(.35f, .32f, .22f), new Vector3(.78f, .64f, .77f), sides, stone * .85f);
                if (detail) { b.Rock(new Vector3(.30f, .20f, -.42f), new Vector3(.45f, .40f, .45f), 5, stone * 1.1f); b.Rock(new Vector3(-.42f, .09f, .49f), new Vector3(.30f, .18f, .32f), 5, stone); }
                if (metal)
                {
                    var ore = new Color(1.0f, .74f, .26f);
                    for (int i = 0; i < (detail ? 6 : 3); i++)
                    {
                        float a = i * 2.39996f, radius = .30f + i % 3 * .12f;
                        var root = new Vector3(Mathf.Cos(a) * radius, .30f + i % 2 * .22f, Mathf.Sin(a) * radius * .9f);
                        b.Rock(root + Vector3.up * .34f, new Vector3(.34f, .40f, .33f), 5, ore * (i % 2 == 0 ? 1f : .86f));
                        b.Frustum(root, root + new Vector3(.02f, .52f, -.02f), new Vector2(.11f, .09f), new Vector2(.05f, .04f), 4, ore * .78f);
                    }
                    b.Rock(new Vector3(-.06f, 1.02f, -.04f), new Vector3(.52f, .46f, .5f), 6, ore);
                }
                else
                {
                    for (int i = 0; i < (detail ? 4 : 2); i++)
                    {
                        float a = i * 1.87f;
                        b.Box(new Vector3(Mathf.Cos(a) * .46f, .13f + i % 2 * .22f, Mathf.Sin(a) * .44f), new Vector3(.42f, .26f, .38f),
                            Stone * (i % 2 == 0 ? 1.08f : .92f), 0, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                    }
                    if (detail) b.Rock(new Vector3(-.25f, .81f, -.02f), new Vector3(.54f, .055f, .54f), 5, new Color(.43f, .51f, .30f));
                }
            }
            return b.Mesh("Alpha " + kind + " LOD" + lod);
        }

        private static void Horse(AlphaMeshBuilder b, bool detail, int sides, bool march)
        {
            var coat = march ? new Color(.32f, .29f, .26f) : new Color(.51f, .32f, .18f);
            b.Frustum(new Vector3(0, .58f, -.08f), new Vector3(0, 1.03f, -.08f), new Vector2(.35f, .67f), new Vector2(.31f, .60f), sides, coat);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                b.Beam(new Vector3(x * .24f, .69f, z * .46f), new Vector3(x * .27f, .11f, z * .49f), .12f, .17f, coat);
                b.Box(new Vector3(x * .27f, .08f, z * .49f), new Vector3(.18f, .16f, .24f), Dark);
            }
            b.Frustum(new Vector3(0, .87f, .42f), new Vector3(0, 1.53f, .65f), new Vector2(.25f, .27f), new Vector2(.17f, .19f), sides, coat);
            b.Box(new Vector3(0, 1.51f, .84f), new Vector3(.33f, .28f, .47f), coat, 0, Quaternion.Euler(-13, 0, 0));
            b.Box(new Vector3(0, 1.05f, -.11f), new Vector3(.77f, .11f, .80f), Cloth, 1);
            b.Beam(new Vector3(0, .97f, -.68f), new Vector3(0, .43f, -.91f), .13f, .15f, Dark);
            if (detail)
            {
                b.Beam(new Vector3(0, 1.58f, .52f), new Vector3(0, 1.05f, .26f), .12f, .095f, Dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Frustum(new Vector3(side * .11f, 1.62f, .68f), new Vector3(side * .14f, 1.88f, .65f), new Vector2(.075f, .045f), Vector2.zero, 4, coat);
                    b.Box(new Vector3(side * .33f, .94f, -.10f), new Vector3(.045f, .37f, .67f), Cloth, 1);
                    b.Beam(new Vector3(side * .16f, 1.48f, .77f), new Vector3(side * .22f, 1.27f, .14f), .035f, .035f, Leather);
                }
                b.Box(new Vector3(0, 1.48f, .85f), new Vector3(.35f, .06f, .49f), Leather);
            }
        }

        public static Mesh Beacon(int lod)
        {
            var b = new AlphaMeshBuilder(); int sides = lod == 0 ? 10 : 6;
            b.Frustum(Vector3.zero, Vector3.up * .14f, new Vector2(1.05f, 1.05f), new Vector2(1.05f, 1.05f), sides, Stone);
            b.Frustum(Vector3.up * .14f, Vector3.up * .32f, new Vector2(.79f, .79f), new Vector2(.72f, .72f), sides, Stone * .84f);
            b.Box(new Vector3(0, 1.12f, 0), new Vector3(.44f, 1.65f, .44f), Stone);
            b.Box(new Vector3(0, 1.91f, 0), new Vector3(.62f, .14f, .62f), Gold);
            b.Frustum(Vector3.up * 2.02f, Vector3.up * 2.66f, new Vector2(.30f, .30f), Vector2.zero, 4, Gold);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(side * .28f, 1.2f, 0), new Vector3(.075f, 1.48f, .12f), Gold);
                b.Beam(new Vector3(side * .43f, .26f, .36f), new Vector3(side * .31f, .86f, .21f), .12f, .13f, Stone);
                if (lod == 0) b.Frustum(new Vector3(side * .68f, .18f, -.38f), new Vector3(side * .68f, .63f, -.38f), new Vector2(.12f, .12f), new Vector2(.085f, .085f), 6, Gold);
            }
            return b.Mesh("Alpha public beacon LOD" + lod);
        }

        private static void Cart(AlphaMeshBuilder b, bool detail)
        {
            b.Box(new Vector3(0, .39f, 0), new Vector3(1.04f, .19f, 1.56f), Timber);
            b.Box(new Vector3(0, .76f, -.1f), new Vector3(.96f, .64f, 1.13f), Leather);
            b.Roof(new Vector3(0, 1.33f, -.1f), new Vector3(1.05f, .51f, 1.33f), Cloth, 1);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * .44f, .25f, -.75f), new Vector3(side * .44f, .31f, 1.01f), .14f, .16f, Timber);
                b.Box(new Vector3(side * .51f, .8f, -.1f), new Vector3(.055f, .80f, 1.24f), Cloth, 1);
                if (detail) for (int z = -1; z <= 1; z += 2) b.Box(new Vector3(side * .53f, .58f, z * .53f), new Vector3(.13f, .90f, .07f), Gold);
            }
            if (detail) { b.Box(new Vector3(0, 1.14f, -.1f), new Vector3(1.08f, .06f, 1.35f), Timber); b.Box(new Vector3(0, .65f, .60f), new Vector3(.54f, .49f, .3f), Stone); }
        }

        public static Mesh Building(string id, FactionKind faction, int lod)
        {
            if (AlphaExpandedGeometry.SpecialBuilding(id)) return AlphaExpandedGeometry.Building(id, faction, lod);
            var b = new AlphaMeshBuilder(); bool detail = lod == 0, march = faction == FactionKind.SerevinMarch;
            Color wall = AlphaExpandedGeometry.WallColor(faction, march ? new Color(.57f, .45f, .30f) : Plaster);
            Color roof = AlphaExpandedGeometry.RoofColor(faction, march ? new Color(.39f, .26f, .16f) : Roof);
            b.Box(new Vector3(0, .11f, 0), new Vector3(.96f, .22f, .96f), Stone);
            if (id == "hearth")
            {
                House(b, new Vector3(-.04f, 0, -.04f), .73f, .69f, 1.73f, wall, roof, detail, march);
                House(b, new Vector3(.28f, 0, .15f), .29f, .48f, 1.14f, wall, roof, detail, march);
                var tower = new Vector3(-.23f, 0, -.19f);
                b.Box(tower + new Vector3(0, 2.61f, 0), new Vector3(.28f, 1.64f, .26f), wall);
                b.Roof(tower + new Vector3(0, 3.73f, 0), new Vector3(.36f, .70f, .34f), roof);
                b.Box(tower + new Vector3(0, 2.77f, .137f), new Vector3(.16f, .63f, .028f), Dark);
                b.Box(tower + new Vector3(0, 2.56f, .158f), new Vector3(.105f, .26f, .025f), Gold);
                Banner(b, new Vector3(.29f, 1.12f, .407f), detail);
                if (detail) { b.Box(tower + new Vector3(0, 3.42f, 0), new Vector3(.30f, .10f, .29f), Gold); Stairs(b, .02f, .43f, 4); }
            }
            else if (id == "archive")
            {
                House(b, new Vector3(-.05f, 0, .035f), .70f, .70f, 2.15f, wall, roof, detail, march);
                b.Frustum(new Vector3(.24f, .22f, -.25f), new Vector3(.24f, 2.87f, -.25f), new Vector2(.19f, .19f), new Vector2(.17f, .17f), detail ? 8 : 5, wall);
                b.Frustum(new Vector3(.24f, 2.85f, -.25f), new Vector3(.24f, 3.59f, -.25f), new Vector2(.23f, .22f), Vector2.zero, detail ? 8 : 5, roof);
                b.Box(new Vector3(-.05f, 1.90f, .398f), new Vector3(.12f, .54f, .025f), Cloth, 1);
                if (detail) { Window(b, -.23f, 1.34f, .397f, .14f, .61f); Window(b, .13f, 1.34f, .397f, .14f, .61f); Stairs(b, -.05f, .44f, 4); }
            }
            else if (id == "storeyard")
            {
                House(b, new Vector3(-.19f, 0, -.20f), .48f, .46f, 1.43f, wall, roof, detail, march);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) b.Box(new Vector3(.13f + x * .27f, 1.04f, .13f + z * .24f), new Vector3(.06f, 1.77f, .06f), Timber);
                b.Roof(new Vector3(.13f, 2.02f, .13f), new Vector3(.62f, .42f, .56f), Cloth, 1);
                for (int i = 0; i < (detail ? 5 : 2); i++) Crate(b, new Vector3(-.17f + i % 3 * .19f, .38f + i / 3 * .32f, .23f - i / 3 * .17f), .16f, detail);
                if (detail) b.Frustum(new Vector3(.35f, .22f, -.25f), new Vector3(.35f, .86f, -.25f), new Vector2(.09f, .09f), new Vector2(.075f, .075f), 8, Timber);
            }
            else if (id == "muster_hall")
            {
                House(b, new Vector3(0, 0, -.19f), .86f, .48f, 1.99f, wall, roof, detail, march);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(new Vector3(side * .36f, 1.14f, .32f), new Vector3(.08f, 1.9f, .10f), Stone);
                    b.Frustum(new Vector3(side * .36f, 2.04f, .32f), new Vector3(side * .36f, 2.44f, .32f), new Vector2(.07f, .07f), Vector2.zero, 4, Gold);
                    Banner(b, new Vector3(side * .29f, 1.15f, .34f), detail);
                    if (detail) for (int i = 0; i < 3; i++) { b.Beam(new Vector3(side * .29f, .22f, .02f + i * .08f), new Vector3(side * .25f, 1.46f, .02f + i * .08f), .022f, .027f, Timber); }
                }
                b.Box(new Vector3(0, 2.02f, .32f), new Vector3(.80f, .17f, .12f), Timber);
                b.Box(new Vector3(0, 2.035f, .39f), new Vector3(.42f, .12f, .025f), Gold);
            }
            else if (id == "supply_outpost")
            {
                b.Box(new Vector3(0, .45f, 0), new Vector3(.79f, .45f, .78f), Timber);
                b.Roof(new Vector3(0, 1.49f, -.045f), new Vector3(.88f, 1.67f, .85f), Cloth, 1);
                b.Box(new Vector3(0, .90f, .393f), new Vector3(.29f, 1.02f, .034f), Dark);
                b.Beam(new Vector3(-.37f, .18f, .32f), new Vector3(-.37f, 2.47f, .32f), .045f, .045f, Timber);
                Banner(b, new Vector3(-.29f, 1.48f, .32f), detail);
                if (detail) for (int side = -1; side <= 1; side += 2) { Crate(b, new Vector3(side * .31f, .41f, .40f), .17f, true); b.Box(new Vector3(side * .36f, .13f, 0), new Vector3(.08f, .12f, .98f), Timber); }
            }
            else
            {
                House(b, new Vector3(0, 0, -.015f), .75f, .72f, 1.33f, wall, roof, detail, march);
                b.Box(new Vector3(.22f, 2.0f, -.20f), new Vector3(.13f, .92f, .12f), Stone);
                if (detail) { b.Box(new Vector3(.22f, 2.43f, -.20f), new Vector3(.17f, .09f, .16f), Stone); Crate(b, new Vector3(-.28f, .38f, .36f), .20f, true); Stairs(b, .02f, .43f, 2); }
            }
            if (march)
            {
                for (int side = -1; side <= 1; side += 2) b.Beam(new Vector3(side * .37f, .23f, -.37f), new Vector3(side * .31f, 1.60f, -.34f), .065f, .08f, Timber);
                if (detail) b.Box(new Vector3(0, .27f, -.43f), new Vector3(.92f, .13f, .07f), Timber);
            }
            AlphaExpandedGeometry.AddBuildingIdentity(b, id, faction, detail);
            return b.Mesh("Alpha " + faction + " " + id + " LOD" + lod);
        }

        private static void House(AlphaMeshBuilder b, Vector3 offset, float width, float depth, float height, Color wall, Color roof, bool detail, bool march)
        {
            b.Box(offset + new Vector3(0, .22f + height * .5f, 0), new Vector3(width, height, depth), wall);
            b.Roof(offset + new Vector3(0, .26f + height + .36f, 0), new Vector3(width + .10f, march ? .95f : .74f, depth + .10f), roof);
            b.Roof(offset + new Vector3(0, .275f + height + .36f, 0), new Vector3(width + .11f, march ? .95f : .74f, depth * .19f), Cloth, 1);
            b.Box(offset + new Vector3(0, .73f, depth * .5f + .013f), new Vector3(width * .27f, 1.03f, .027f), Dark);
            if (!detail) return;
            b.Box(offset + new Vector3(0, height + .20f, 0), new Vector3(width + .025f, .12f, depth + .025f), Timber);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(offset + new Vector3(side * width * .44f, .22f + height * .5f, depth * .507f), new Vector3(.035f, height, .027f), Timber);
                Window(b, offset.x + side * width * .29f, offset.y + height * .57f + .33f, offset.z + depth * .5f + .019f, width * .12f, height * .28f);
                b.Box(offset + new Vector3(side * width * .47f, .38f, 0), new Vector3(.047f, .24f, depth), Stone);
            }
            for (int row = 0; row < 3; row++)
            {
                float roofX = width * (.13f + row * .13f);
                float roofY = height + .97f - row * .22f;
                for (int side = -1; side <= 1; side += 2) b.Box(offset + new Vector3(side * roofX, roofY, 0), new Vector3(.025f, .055f, depth + .11f), roof * 1.15f);
            }
            b.Box(offset + new Vector3(0, height + 1.02f, 0), new Vector3(.055f, .075f, depth + .11f), march ? Timber : Gold);
        }
        private static void Window(AlphaMeshBuilder b, float x, float y, float z, float width, float height)
        {
            b.Box(new Vector3(x, y, z), new Vector3(width, height, .025f), Dark);
            b.Box(new Vector3(x, y, z + .018f), new Vector3(width * .11f, height, .015f), Gold);
            b.Box(new Vector3(x, y, z + .018f), new Vector3(width, height * .12f, .015f), Timber);
            b.Box(new Vector3(x, y - height * .53f, z), new Vector3(width * 1.22f, .07f, .07f), Stone);
        }
        private static void Banner(AlphaMeshBuilder b, Vector3 point, bool detail)
        {
            b.Box(point + Vector3.up * .15f, new Vector3(.14f, .75f, .035f), Cloth, 1);
            if (detail) { b.Box(point + Vector3.up * .53f, new Vector3(.19f, .055f, .06f), Gold); b.Box(point + Vector3.up * .17f + Vector3.forward * .025f, new Vector3(.045f, .18f, .015f), Gold); }
        }
        private static void Crate(AlphaMeshBuilder b, Vector3 point, float width, bool detail)
        {
            b.Box(point, new Vector3(width, .33f, width), Timber);
            if (detail) for (int side = -1; side <= 1; side += 2) b.Box(point + new Vector3(side * width * .32f, 0, 0), new Vector3(.025f, .35f, width + .015f), Leather * 1.4f);
        }
        private static void Stairs(AlphaMeshBuilder b, float x, float z, int steps)
        { for (int i = 0; i < steps; i++) b.Box(new Vector3(x, .055f + i * .055f, z - i * .028f), new Vector3(.25f, .095f, .07f), Stone); }
    }
}
