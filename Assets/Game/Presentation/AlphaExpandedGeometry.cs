using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Original realm silhouettes. All dimensions below are presentation only.</summary>
    internal static class AlphaExpandedGeometry
    {
        private static readonly Color Bone = new Color(.88f, .84f, .65f), Gold = new Color(.92f, .64f, .23f);
        private static readonly Color Iron = new Color(.22f, .29f, .31f), Wood = new Color(.29f, .18f, .105f);
        private static readonly Color Cloth = new Color(.92f, .89f, .76f), Dark = new Color(.07f, .10f, .105f);

        internal static Color WallColor(FactionKind faction, Color fallback)
        {
            switch ((int)faction)
            {
                case 2: return new Color(.79f, .62f, .38f);
                case 3: return new Color(.39f, .35f, .28f);
                case 4: return new Color(.91f, .89f, .76f);
                case 5: return new Color(.43f, .44f, .25f);
                case 6: return new Color(.23f, .25f, .28f);
                case 7: return new Color(.38f, .36f, .35f);
                default: return fallback;
            }
        }
        internal static Color RoofColor(FactionKind faction, Color fallback)
        {
            switch ((int)faction)
            {
                case 2: return new Color(.20f, .44f, .47f);
                case 3: return new Color(.24f, .29f, .34f);
                case 4: return new Color(.70f, .53f, .20f);
                case 5: return new Color(.19f, .39f, .27f);
                case 6: return new Color(.35f, .14f, .14f);
                case 7: return new Color(.42f, .30f, .22f);
                default: return fallback;
            }
        }
        internal static bool SpecialUnit(string id) => id == "sun_lion" || id == "grove_guardian" || id == "war_troll" || id == "ember_drake" || id == "dune_elephant" || id == "frostguard" || id.StartsWith("siege_", System.StringComparison.Ordinal);
        internal static bool SpecialBuilding(string id) => id == "wall" || id == "gate" || id == "watchtower" || id == "keep" || id == "beast_lodge" || id == "siege_workshop";

        internal static void AddUnitIdentity(AlphaMeshBuilder b, FactionKind faction, float lift, bool detail, bool worker)
        {
            int identity = (int)faction; if (identity < 2) return;
            float y = 1.73f + lift;
            if (identity == 2)
            {
                b.Frustum(new Vector3(0, y - .13f, .02f), new Vector3(0, y + .12f, .02f), new Vector2(.25f, .22f), new Vector2(.16f, .14f), detail ? 8 : 5, Cloth);
                b.Box(new Vector3(0, y - .11f, .205f), new Vector3(.33f, .095f, .04f), Cloth, 1);
                b.Box(new Vector3(.21f, y - .29f, -.10f), new Vector3(.10f, .54f, .08f), Cloth, 1);
                if (!worker) b.Frustum(new Vector3(0, y + .09f, 0), new Vector3(0, y + .25f, 0), new Vector2(.055f, .055f), Vector2.zero, 5, Gold, 2);
            }
            else if (identity == 3)
            {
                b.Rock(new Vector3(0, 1.20f + lift, -.06f), new Vector3(.82f, .30f, .51f), 6, new Color(.64f, .63f, .54f));
                if (!worker) for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(new Vector3(side * .16f, y -.03f, 0), new Vector3(side * .33f, y + .13f, -.03f), .10f, .09f, Bone);
                    b.Beam(new Vector3(side * .33f, y + .13f, -.03f), new Vector3(side * .26f, y + .30f, -.01f), .06f, .06f, Bone);
                }
            }
            else if (identity == 4)
            {
                b.Box(new Vector3(0, y + .09f, -.035f), new Vector3(.05f, .29f, .29f), Gold, 2);
                if (!worker) for (int side = -1; side <= 1; side += 2)
                    b.Frustum(new Vector3(side * .31f, 1.16f + lift, 0), new Vector3(side * .42f, 1.58f + lift, -.04f), new Vector2(.11f, .13f), Vector2.zero, 4, Bone);
            }
            else if (identity == 5)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(new Vector3(side * .17f, y -.04f, -.02f), new Vector3(side * .27f, y + .30f, -.04f), .06f, .065f, Wood);
                    if (detail) b.Beam(new Vector3(side * .24f, y + .19f, -.04f), new Vector3(side * .43f, y + .27f, -.05f), .045f, .04f, Wood);
                    b.Triangle(new Vector3(side * .14f, 1.20f + lift, -.20f), new Vector3(side * .53f, 1.26f + lift, -.18f), new Vector3(side * .29f, .91f + lift, -.30f), new Color(.28f, .45f, .24f));
                }
            }
            else if (identity == 6)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Frustum(new Vector3(side * .17f, y - .04f, -.01f), new Vector3(side * .28f, y + .35f, -.12f), new Vector2(.095f, .09f), Vector2.zero, 4, Iron);
                    if (!worker) b.Frustum(new Vector3(side * .35f, 1.18f + lift, -.04f), new Vector3(side * .54f, 1.55f + lift, -.10f), new Vector2(.13f, .14f), Vector2.zero, 4, Iron);
                }
            }
            else if (identity == (int)FactionKind.DrakeforgedClans)
            {
                b.Box(new Vector3(0, y -.025f, .035f), new Vector3(.38f, .14f, .38f), Iron);
                for (int side = -1; side <= 1; side += 2)
                    b.Frustum(new Vector3(side * .13f, y, 0), new Vector3(side * .21f, y + .24f, -.14f), new Vector2(.07f, .08f), Vector2.zero, 4, Gold, 2);
                b.Box(new Vector3(0, 1.15f + lift, .235f), new Vector3(.30f, .28f, .05f), Gold, 2);
            }
        }

        internal static void AddBuildingIdentity(AlphaMeshBuilder b, string id, FactionKind faction, bool detail)
        {
            int identity = (int)faction; if (identity < 2) return;
            float top = id == "hearth" ? 3.48f : id == "archive" ? 2.95f : id == "muster_hall" ? 2.86f : 2.20f;
            var wall = WallColor(faction, Bone); var roof = RoofColor(faction, Iron);
            if (identity == 2)
            {
                b.Dome(new Vector3(0, top - .25f, -.045f), new Vector3(.27f, .72f, .27f), detail ? 12 : 6, detail ? 4 : 2, roof);
                b.Frustum(new Vector3(0, top + .40f, -.045f), new Vector3(0, top + .72f, -.045f), new Vector2(.028f, .028f), Vector2.zero, 4, Gold, 2);
                if (detail) for (int side = -1; side <= 1; side += 2)
                    b.Box(new Vector3(side * .35f, .99f, .39f), new Vector3(.065f, 1.47f, .045f), Gold, 2);
            }
            else if (identity == 3)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(new Vector3(0, top - .04f, side * .32f), new Vector3(side * .14f, top + .28f, side * .43f), .055f, .08f, Wood);
                    b.Rock(new Vector3(side * .14f, top + .27f, side * .43f), new Vector3(.10f, .18f, .11f), 5, Bone);
                }
            }
            else if (identity == 4)
            {
                b.Tube(new Vector3(0, top + .13f, .025f), new Vector3(0, top + .13f, .09f), .18f, detail ? 12 : 6, Gold, 2);
                b.Beam(new Vector3(0, top -.05f, .05f), new Vector3(0, top + .48f, .05f), .045f, .045f, Gold, 2);
                if (detail) for (int side = -1; side <= 1; side += 2)
                    b.Frustum(new Vector3(side * .34f, .24f, -.30f), new Vector3(side * .34f, top + .10f, -.30f), new Vector2(.06f, .06f), new Vector2(.04f, .04f), 6, wall);
            }
            else if (identity == 5)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Beam(new Vector3(side * .44f, .20f, -.32f), new Vector3(side * .26f, top + .07f, -.23f), .09f, .10f, Wood);
                    b.Beam(new Vector3(side * .26f, top + .07f, -.23f), new Vector3(side * .40f, top + .53f, -.24f), .05f, .06f, Wood);
                    b.Rock(new Vector3(side * .22f, top + .14f, -.19f), new Vector3(.36f, .30f, .49f), detail ? 7 : 4, roof);
                }
            }
            else if (identity == 6)
            {
                for (int side = -1; side <= 1; side += 2)
                    b.Frustum(new Vector3(side * .34f, top - .36f, -.28f), new Vector3(side * .44f, top + .55f, -.37f), new Vector2(.075f, .07f), Vector2.zero, 4, Iron);
                b.Box(new Vector3(0, 1.06f, .417f), new Vector3(.10f, .68f, .025f), new Color(.84f, .24f, .11f), 2);
            }
            else if (identity == (int)FactionKind.DrakeforgedClans)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(new Vector3(side * .30f, top - .22f, -.29f), new Vector3(.12f, .91f, .14f), Iron);
                    b.Box(new Vector3(side * .30f, top + .25f, -.29f), new Vector3(.17f, .08f, .19f), Gold, 2);
                }
                if (detail) for (int row = 0; row < 3; row++)
                    b.Box(new Vector3(0, .57f + row * .22f, .408f), new Vector3(.30f, .07f, .045f), Iron);
            }
        }

        internal static Mesh Unit(string id, FactionKind faction, int lod)
        {
            var b = new AlphaMeshBuilder(); bool detail = lod == 0; int sides = detail ? 8 : 5;
            if (id.StartsWith("siege_", System.StringComparison.Ordinal)) Siege(b, id, detail);
            else if (id == "dune_elephant") Elephant(b, detail, sides);
            else if (id == "ember_drake") Dragon(b, detail, sides);
            else if (id == "sun_lion") Lion(b, detail, sides);
            else if (id == "grove_guardian") Guardian(b, detail, sides);
            else Giant(b, id == "frostguard", detail, sides);
            return b.Mesh("Alpha " + faction + " " + id + " LOD" + lod);
        }
        private static void Elephant(AlphaMeshBuilder b, bool detail, int sides)
        {
            var hide = new Color(.48f, .47f, .42f);
            b.Rock(new Vector3(0, 1.55f, -.12f), new Vector3(1.64f, 1.74f, 2.65f), sides, hide);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                b.Frustum(new Vector3(x * .55f, .10f, z * .72f), new Vector3(x * .54f, 1.47f, z * .67f), new Vector2(.23f, .24f), new Vector2(.29f, .28f), sides, hide);
                if (detail) b.Box(new Vector3(x * .55f, .16f, z * .72f + .19f), new Vector3(.36f, .11f, .07f), Bone);
            }
            b.Rock(new Vector3(0, 1.95f, 1.01f), new Vector3(1.11f, 1.30f, .99f), sides, hide);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Rock(new Vector3(side * .59f, 1.80f, .80f), new Vector3(.62f, 1.08f, .25f), sides, hide * .9f);
                b.Beam(new Vector3(side * .32f, 1.45f, 1.25f), new Vector3(side * .36f, 1.17f, 1.78f), .16f, .16f, Bone);
                b.Beam(new Vector3(side * .36f, 1.17f, 1.78f), new Vector3(side * .30f, 1.45f, 2.02f), .095f, .08f, Bone);
                b.Box(new Vector3(side * .69f, 1.48f, -.15f), new Vector3(.08f, .89f, 1.48f), Cloth, 1);
            }
            b.Beam(new Vector3(0, 1.95f, 1.43f), new Vector3(0, .66f, 1.62f), .32f, .30f, hide);
            b.Beam(new Vector3(0, .66f, 1.62f), new Vector3(0, .40f, 1.95f), .21f, .21f, hide);
            b.Box(new Vector3(0, 2.48f, -.13f), new Vector3(1.15f, .20f, 1.34f), Wood);
            for (int side = -1; side <= 1; side += 2) b.Box(new Vector3(side * .55f, 2.81f, -.13f), new Vector3(.09f, .57f, 1.34f), Cloth, 1);
            b.Box(new Vector3(0, 2.81f, -.75f), new Vector3(1.16f, .57f, .09f), Gold, 2);
            b.Beam(new Vector3(0, 2.64f, -.55f), new Vector3(0, 3.68f, -.55f), .07f, .07f, Gold, 2);
            b.Box(new Vector3(.19f, 3.36f, -.55f), new Vector3(.38f, .43f, .04f), Cloth, 1);
            b.Beam(new Vector3(0, 1.97f, -1.34f), new Vector3(.13f, .81f, -1.49f), .07f, .08f, hide);
        }
        private static void Dragon(AlphaMeshBuilder b, bool detail, int sides)
        {
            var scales = new Color(.40f, .16f, .12f); var belly = new Color(.69f, .41f, .22f);
            b.Rock(new Vector3(0, 1.00f, -.17f), new Vector3(1.02f, 1.11f, 1.80f), sides, scales);
            b.Rock(new Vector3(0, .80f, .22f), new Vector3(.68f, .66f, 1.15f), sides, belly);
            b.Beam(new Vector3(0, 1.14f, .45f), new Vector3(0, 1.67f, .87f), .46f, .47f, scales);
            b.Rock(new Vector3(0, 1.76f, 1.05f), new Vector3(.60f, .53f, .89f), sides, scales);
            b.Box(new Vector3(0, 1.58f, 1.37f), new Vector3(.41f, .13f, .57f), belly);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Frustum(new Vector3(side * .18f, 1.93f, .85f), new Vector3(side * .35f, 2.42f, .55f), new Vector2(.085f, .09f), Vector2.zero, 5, Bone);
                b.Box(new Vector3(side * .29f, 1.81f, 1.17f), new Vector3(.035f, .07f, .16f), Gold, 2);
                for (int z = -1; z <= 1; z += 2)
                {
                    b.Beam(new Vector3(side * .35f, .92f, z * .43f), new Vector3(side * .59f, .14f, z * .60f), .18f, .21f, scales);
                    b.Box(new Vector3(side * .59f, .12f, z * .60f + .08f), new Vector3(.28f, .17f, .40f), Bone);
                }
            }
            var tail = new[] { new Vector3(0, .92f, -.95f), new Vector3(.16f, .54f, -1.61f), new Vector3(.51f, .32f, -2.19f), new Vector3(.74f, .56f, -2.51f) };
            for (int i = 0; i < 3; i++) b.Beam(tail[i], tail[i + 1], .29f - i * .08f, .30f - i * .08f, scales);
            if (detail) for (int i = 0; i < 6; i++) b.Frustum(new Vector3(0, 1.41f - i * .07f, .35f - i * .25f), new Vector3(0, 1.70f - i * .07f, .28f - i * .25f), new Vector2(.07f, .10f), Vector2.zero, 4, Gold, 2);
            b.Box(new Vector3(0, 1.57f, -.11f), new Vector3(.75f, .09f, .67f), Cloth, 1);
        }
        internal static Mesh DragonWing(bool left, int lod)
        {
            var b = new AlphaMeshBuilder(); float side = left ? -1 : 1;
            var root = new Vector3(0, 0, 0); var elbow = new Vector3(side * 1.14f, .75f, -.09f); var tip = new Vector3(side * 2.22f, .93f, -.63f);
            var rear = new Vector3(side * .92f, -.10f, -1.27f); var middle = new Vector3(side * 1.56f, .25f, -1.08f);
            var membrane = new Color(.71f, .29f, .17f); var bone = new Color(.36f, .18f, .13f);
            b.Triangle(root, elbow, rear, membrane); b.Triangle(rear, elbow, root, membrane);
            b.Triangle(elbow, tip, middle, membrane); b.Triangle(middle, tip, elbow, membrane);
            b.Triangle(elbow, middle, rear, membrane); b.Triangle(rear, middle, elbow, membrane);
            b.Beam(root, elbow, .12f, .13f, bone); b.Beam(elbow, tip, .085f, .075f, bone);
            if (lod == 0) { b.Beam(elbow, rear, .055f, .055f, Gold, 2); b.Beam(elbow, middle, .045f, .045f, bone); }
            b.Triangle(root + new Vector3(side * .1f, .02f, 0), elbow * .72f, rear * .5f, Cloth, 1);
            return b.Mesh("Alpha drake " + (left ? "left" : "right") + " wing LOD" + lod);
        }
        private static void Lion(AlphaMeshBuilder b, bool detail, int sides)
        {
            var hide = new Color(.76f, .58f, .29f);
            b.Rock(new Vector3(0, .90f, -.16f), new Vector3(.98f, .84f, 1.75f), sides, hide);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                b.Beam(new Vector3(x * .30f, .85f, z * .54f), new Vector3(x * .33f, .15f, z * .61f), .19f, .23f, hide);
                b.Box(new Vector3(x * .33f, .11f, z * .61f + .10f), new Vector3(.28f, .20f, .39f), Bone);
            }
            b.Rock(new Vector3(0, 1.10f, .62f), new Vector3(1.13f, 1.31f, .94f), sides, Gold);
            b.Rock(new Vector3(0, 1.26f, .98f), new Vector3(.66f, .63f, .59f), sides, hide);
            b.Box(new Vector3(0, 1.07f, 1.24f), new Vector3(.38f, .27f, .34f), Bone);
            b.Box(new Vector3(0, 1.19f, 1.43f), new Vector3(.21f, .10f, .04f), Dark);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Frustum(new Vector3(side * .24f, 1.49f, .86f), new Vector3(side * .31f, 1.80f, .78f), new Vector2(.10f, .10f), Vector2.zero, 5, Gold, 2);
                b.Box(new Vector3(side * .41f, 1.00f, -.12f), new Vector3(.06f, .43f, .71f), Cloth, 1);
            }
            b.Beam(new Vector3(0, .94f, -1.03f), new Vector3(.21f, .56f, -1.66f), .085f, .085f, hide);
            b.Rock(new Vector3(.21f, .58f, -1.69f), new Vector3(.26f, .29f, .29f), 5, Gold);
            if (detail) for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7;
                b.Frustum(new Vector3(Mathf.Cos(a) * .47f, 1.10f + Mathf.Sin(a) * .50f, .44f), new Vector3(Mathf.Cos(a) * .63f, 1.10f + Mathf.Sin(a) * .64f, .21f), new Vector2(.11f, .11f), Vector2.zero, 4, Gold, 2);
            }
        }
        private static void Guardian(AlphaMeshBuilder b, bool detail, int sides)
        {
            var bark = new Color(.38f, .30f, .20f); var leaf = new Color(.26f, .46f, .28f);
            b.Frustum(new Vector3(0, .85f, 0), new Vector3(0, 2.33f, 0), new Vector2(.32f, .29f), new Vector2(.55f, .36f), sides, bark);
            b.Rock(new Vector3(0, 2.57f, .06f), new Vector3(.68f, .81f, .56f), sides, bark);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * .25f, 1.13f, 0), new Vector3(side * .39f, .17f, .04f), .29f, .32f, bark);
                b.Beam(new Vector3(side * .42f, 2.11f, 0), new Vector3(side * .89f, 1.05f, .22f), .24f, .27f, bark);
                b.Rock(new Vector3(side * .56f, 2.25f, -.12f), new Vector3(.93f, .56f, .76f), sides, leaf);
                b.Beam(new Vector3(side * .21f, 2.83f, -.02f), new Vector3(side * .55f, 3.50f, -.17f), .08f, .09f, bark);
                if (detail)
                {
                    b.Beam(new Vector3(side * .39f, 3.17f, -.10f), new Vector3(side * .82f, 3.26f, -.16f), .055f, .065f, bark);
                    for (int n = 0; n < 3; n++) b.Beam(new Vector3(side * .40f, .15f, .07f), new Vector3(side * (.28f + n * .13f), .08f, .43f), .09f, .12f, bark);
                }
            }
            b.Box(new Vector3(0, 1.83f, .37f), new Vector3(.39f, .45f, .055f), Cloth, 1);
            b.Box(new Vector3(0, 2.61f, .335f), new Vector3(.34f, .065f, .04f), new Color(.75f, .86f, .36f), 2);
        }
        private static void Giant(AlphaMeshBuilder b, bool frost, bool detail, int sides)
        {
            var hide = frost ? new Color(.67f, .74f, .77f) : new Color(.39f, .43f, .34f);
            var armor = frost ? new Color(.30f, .40f, .47f) : Iron;
            b.Frustum(new Vector3(0, .94f, 0), new Vector3(0, 2.33f, 0), new Vector2(.35f, .30f), new Vector2(.65f, .42f), sides, hide);
            b.Rock(new Vector3(0, 2.56f, .13f), new Vector3(.66f, .76f, .61f), sides, hide);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * .26f, 1.16f, 0), new Vector3(side * .36f, .18f, .07f), .28f, .34f, hide);
                b.Box(new Vector3(side * .36f, .13f, .17f), new Vector3(.36f, .26f, .50f), armor);
                b.Beam(new Vector3(side * .56f, 2.12f, -.01f), new Vector3(side * .83f, 1.09f, .30f), .31f, .34f, hide);
                b.Rock(new Vector3(side * .63f, 2.23f, 0), new Vector3(.67f, .41f, .72f), sides, armor);
                if (frost) b.Frustum(new Vector3(side * .18f, 2.81f, .05f), new Vector3(side * .37f, 3.30f, -.13f), new Vector2(.10f, .10f), Vector2.zero, 5, Bone);
                else b.Frustum(new Vector3(side * .13f, 2.36f, .42f), new Vector3(side * .15f, 2.66f, .48f), new Vector2(.065f, .055f), Vector2.zero, 4, Bone);
            }
            b.Box(new Vector3(0, 1.34f, .27f), new Vector3(.76f, .65f, .13f), Cloth, 1);
            b.Beam(new Vector3(.89f, .21f, .39f), new Vector3(.87f, 2.53f, .39f), .105f, .105f, Wood);
            b.Rock(new Vector3(.87f, 2.50f, .39f), new Vector3(frost ? .66f : .49f, frost ? .34f : .72f, .48f), sides, armor);
            if (detail) for (int n = 0; n < 3; n++) b.Box(new Vector3(.87f, 2.29f + n * .16f, .65f), new Vector3(.34f, .06f, .05f), Gold, 2);
        }
        private static void Wheels(AlphaMeshBuilder b, float halfWidth, float halfLength, bool detail)
        {
            for (int side = -1; side <= 1; side += 2) for (int z = -1; z <= 1; z += 2)
            {
                var centre = new Vector3(side * halfWidth, .30f, z * halfLength);
                b.Tube(centre - Vector3.right * .09f, centre + Vector3.right * .09f, .29f, detail ? 12 : 6, Wood);
                if (detail) { b.Tube(centre + Vector3.right * side * .095f, centre + Vector3.right * side * .11f, .10f, 8, Iron); }
            }
        }
        private static void Siege(AlphaMeshBuilder b, string id, bool detail)
        {
            if (id == "siege_ladder")
            {
                for (int side = -1; side <= 1; side += 2) b.Beam(new Vector3(side * .38f, .08f, -.69f), new Vector3(side * .38f, 3.58f, .62f), .11f, .12f, Wood);
                for (int i = 0; i < (detail ? 12 : 8); i++)
                {
                    float t = (i + 1f) / (detail ? 13 : 9);
                    b.Beam(new Vector3(-.37f, .08f + t * 3.5f, -.69f + t * 1.31f), new Vector3(.37f, .08f + t * 3.5f, -.69f + t * 1.31f), .075f, .075f, Wood);
                }
                b.Box(new Vector3(0, 1.16f, -.29f), new Vector3(.76f, .27f, .05f), Cloth, 1);
                b.Box(new Vector3(0, .22f, -.69f), new Vector3(.91f, .16f, .36f), Iron);
            }
            else if (id == "siege_ram")
            {
                Wheels(b, .72f, .73f, detail);
                b.Box(new Vector3(0, .43f, 0), new Vector3(1.35f, .20f, 1.98f), Wood);
                for (int side = -1; side <= 1; side += 2) for (int z = -1; z <= 1; z += 2)
                    b.Box(new Vector3(side * .54f, 1.18f, z * .74f), new Vector3(.13f, 1.44f, .14f), Wood);
                b.Roof(new Vector3(0, 1.98f, 0), new Vector3(1.51f, .84f, 2.10f), Cloth, 1);
                b.Tube(new Vector3(0, .97f, -1.11f), new Vector3(0, .97f, 1.51f), .25f, detail ? 10 : 6, Wood);
                b.Box(new Vector3(0, .97f, 1.52f), new Vector3(.58f, .56f, .33f), Iron);
                if (detail) for (int z = -1; z <= 1; z += 2) b.Beam(new Vector3(0, 1.91f, z * .53f), new Vector3(0, 1.04f, z * .53f), .045f, .045f, Iron);
            }
            else
            {
                Wheels(b, .88f, .75f, detail);
                b.Box(new Vector3(0, .43f, 0), new Vector3(1.77f, .19f, 1.95f), Wood);
                for (int side = -1; side <= 1; side += 2) for (int z = -1; z <= 1; z += 2)
                    b.Beam(new Vector3(side * .72f, .43f, z * .76f), new Vector3(side * .56f, 4.08f, z * .58f), .17f, .18f, Wood);
                for (int level = 0; level < 3; level++)
                {
                    float y = 1.20f + level * 1.11f;
                    b.Box(new Vector3(0, y, 0), new Vector3(1.42f, .13f, 1.61f), Wood);
                    b.Box(new Vector3(0, y + .34f, .78f), new Vector3(1.41f, .57f, .12f), Cloth, 1);
                    if (detail) for (int side = -1; side <= 1; side += 2) b.Beam(new Vector3(side * .66f, y, -.73f), new Vector3(side * .61f, y + .91f, .69f), .08f, .095f, Wood);
                }
                b.Roof(new Vector3(0, 4.13f, 0), new Vector3(1.62f, .75f, 1.67f), Iron);
                b.Box(new Vector3(0, 3.07f, 1.24f), new Vector3(1.06f, .12f, 1.25f), Wood);
                b.Box(new Vector3(0, 4.55f, 0), new Vector3(.10f, .49f, .10f), Gold, 2);
            }
        }

        internal static Mesh Building(string id, FactionKind faction, int lod)
        {
            var b = new AlphaMeshBuilder(); bool detail = lod == 0; var wall = WallColor(faction, new Color(.66f, .65f, .54f)); var roof = RoofColor(faction, Iron);
            b.Box(new Vector3(0, .12f, 0), new Vector3(.98f, .24f, .98f), wall * .84f);
            if (id == "wall")
            {
                b.Box(new Vector3(0, 1.54f, 0), new Vector3(.94f, 2.88f, .86f), wall);
                Battlement(b, new Vector3(0, 3.10f, 0), .97f, .94f, wall, detail);
                b.Box(new Vector3(0, 1.71f, -.439f), new Vector3(.17f, .82f, .025f), Cloth, 1);
                if (detail) for (int row = 0; row < 5; row++) b.Box(new Vector3(0, .43f + row * .48f, -.436f), new Vector3(.94f, .032f, .023f), wall * .75f);
            }
            else if (id == "gate")
            {
                for (int side = -1; side <= 1; side += 2) b.Box(new Vector3(side * .385f, 1.62f, 0), new Vector3(.22f, 3.0f, .94f), wall);
                b.Box(new Vector3(0, 2.80f, 0), new Vector3(.97f, .53f, .94f), wall);
                Battlement(b, new Vector3(0, 3.14f, 0), .97f, .94f, wall, detail);
                b.Box(new Vector3(0, 2.85f, -.478f), new Vector3(.18f, .41f, .024f), Cloth, 1);
            }
            else if (id == "watchtower" || id == "keep")
            {
                bool keep = id == "keep"; float h = keep ? 4.22f : 3.57f;
                b.Box(new Vector3(0, h * .5f, 0), new Vector3(keep ? .71f : .60f, h, keep ? .70f : .60f), wall);
                b.Box(new Vector3(0, h - .20f, 0), new Vector3(.86f, .43f, .86f), wall);
                Battlement(b, new Vector3(0, h + .14f, 0), .89f, .89f, wall, detail);
                b.Box(new Vector3(0, h - 1.25f, -.363f), new Vector3(.19f, .95f, .035f), Cloth, 1);
                if (keep) for (int side = -1; side <= 1; side += 2)
                {
                    b.Frustum(new Vector3(side * .33f, .20f, -.33f), new Vector3(side * .33f, 3.57f, -.33f), new Vector2(.14f, .14f), new Vector2(.12f, .12f), detail ? 8 : 5, wall);
                    b.Frustum(new Vector3(side * .33f, 3.57f, -.33f), new Vector3(side * .33f, 4.25f, -.33f), new Vector2(.16f, .16f), Vector2.zero, detail ? 8 : 5, roof);
                }
                if (detail) for (int side = -1; side <= 1; side += 2)
                    b.Box(new Vector3(side * .19f, h - .97f, -.371f), new Vector3(.045f, .42f, .04f), Dark);
            }
            else if (id == "beast_lodge")
            {
                b.Box(new Vector3(0, 1.07f, -.19f), new Vector3(.85f, 1.80f, .55f), wall);
                b.Roof(new Vector3(0, 2.19f, -.18f), new Vector3(.95f, .81f, .63f), roof);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(new Vector3(side * .40f, .75f, .19f), new Vector3(.08f, 1.30f, .56f), Wood);
                    b.Frustum(new Vector3(side * .32f, 1.99f, -.19f), new Vector3(side * .20f, 2.87f, -.19f), new Vector2(.065f, .065f), Vector2.zero, 5, Bone);
                }
                b.Box(new Vector3(0, 1.21f, .39f), new Vector3(.83f, .16f, .055f), Cloth, 1);
                if (detail) for (int n = -3; n <= 3; n++) b.Box(new Vector3(n * .12f, .62f, .39f), new Vector3(.029f, 1.06f, .045f), Wood);
            }
            else
            {
                b.Box(new Vector3(-.13f, .96f, -.16f), new Vector3(.57f, 1.65f, .63f), wall);
                b.Roof(new Vector3(-.13f, 2.01f, -.16f), new Vector3(.69f, .66f, .66f), roof);
                b.Box(new Vector3(.24f, 1.44f, -.28f), new Vector3(.20f, 2.60f, .21f), Iron);
                b.Box(new Vector3(.24f, 2.78f, -.28f), new Vector3(.26f, .14f, .26f), Gold, 2);
                b.Box(new Vector3(.14f, .58f, .23f), new Vector3(.47f, .15f, .33f), Wood);
                b.Box(new Vector3(0, 1.44f, .167f), new Vector3(.20f, .59f, .025f), Cloth, 1);
                if (detail) for (int side = -1; side <= 1; side += 2)
                    b.Tube(new Vector3(side * .28f - .04f, .35f, .27f), new Vector3(side * .28f + .04f, .35f, .27f), .22f, 8, Wood);
            }
            if (id != "wall" && id != "gate") AddBuildingIdentity(b, id == "keep" ? "hearth" : id, faction, detail);
            return b.Mesh("Alpha " + faction + " " + id + " LOD" + lod);
        }
        private static void Battlement(AlphaMeshBuilder b, Vector3 p, float width, float depth, Color stone, bool detail)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(p + new Vector3(0, .04f, side * depth * .44f), new Vector3(width, .18f, .11f), stone);
                for (int i = -1; i <= 1; i++) b.Box(p + new Vector3(i * width * .34f, .26f, side * depth * .44f), new Vector3(width * .19f, .30f, .12f), stone);
                if (detail) b.Box(p + new Vector3(side * width * .44f, .12f, 0), new Vector3(.11f, .26f, depth * .72f), stone);
            }
        }
        internal static Mesh GateLeaf(int lod)
        {
            var b = new AlphaMeshBuilder();
            b.Box(new Vector3(0, 1.42f, 0), new Vector3(.56f, 2.46f, .12f), Wood);
            for (int i = 0; i < (lod == 0 ? 5 : 3); i++) b.Box(new Vector3(-.25f + i * (lod == 0 ? .125f : .25f), 1.42f, -.085f), new Vector3(.025f, 2.49f, .045f), Iron);
            for (int i = 0; i < 3; i++) b.Box(new Vector3(0, .62f + i * .80f, -.096f), new Vector3(.58f, .08f, .05f), Iron);
            return b.Mesh("Alpha functional portcullis LOD" + lod);
        }
    }
}
