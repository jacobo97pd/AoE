using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Editor
{
    /// <summary>
    /// Original authoring recipes for the two ArtStyleLab candidates. One mesh/submesh per LOD.
    /// This is a 16-bone articulated prototype, not a facial rig or production cloth simulation.
    /// Coordinates face +Z. Bone positions are WORLD rest positions with identity rotations.
    /// RGB is albedo, color alpha is ownership mask, UV1 is (metallic, smoothness).
    /// </summary>
    public static class ArtStylePrototypeGeometry
    {
        public sealed class Prototype
        {
            public Mesh Mesh;
            public string[] BoneNames;
            public Vector3[] BonePositions;
            public int[] BoneParents;
        }

        private const int Root = 0, Hips = 1, Spine = 2, Head = 3;
        private const int UpperArmL = 4, LowerArmL = 5, HandL = 6;
        private const int UpperArmR = 7, LowerArmR = 8, HandR = 9;
        private const int UpperLegL = 10, LowerLegL = 11, FootL = 12;
        private const int UpperLegR = 13, LowerLegR = 14, FootR = 15;

        private readonly struct Paint
        {
            public readonly Color Color;
            public readonly Vector2 Surface;
            public Paint(float r, float g, float b, float metallic = 0, float smoothness = .32f, float team = 0)
            { Color = new Color(r, g, b, team); Surface = new Vector2(metallic, smoothness); }
        }

        private static readonly Paint Linen = new Paint(.83f, .79f, .65f);
        private static readonly Paint Ivory = new Paint(.91f, .89f, .80f);
        private static readonly Paint Team = new Paint(.88f, .93f, 1f, 0, .32f, 1);
        private static readonly Paint Leather = new Paint(.26f, .12f, .055f, 0, .30f);
        private static readonly Paint LeatherLight = new Paint(.43f, .24f, .095f, 0, .35f);
        private static readonly Paint LeatherDark = new Paint(.095f, .061f, .038f, 0, .26f);
        private static readonly Paint Trousers = new Paint(.24f, .24f, .19f, 0, .24f);
        private static readonly Paint Steel = new Paint(.48f, .60f, .67f, .82f, .62f);
        private static readonly Paint SteelEdge = new Paint(.72f, .79f, .80f, .88f, .69f);
        private static readonly Paint SteelDark = new Paint(.18f, .25f, .29f, .65f, .42f);
        private static readonly Paint Gold = new Paint(.82f, .51f, .16f, .76f, .52f);
        private static readonly Paint Skin = new Paint(.68f, .39f, .22f, 0, .39f);
        private static readonly Paint SkinLight = new Paint(.79f, .49f, .30f, 0, .40f);
        private static readonly Paint SkinShade = new Paint(.48f, .235f, .135f, 0, .33f);
        private static readonly Paint Hair = new Paint(.105f, .061f, .035f, 0, .27f);
        private static readonly Paint HairLight = new Paint(.19f, .105f, .046f, 0, .29f);
        private static readonly Paint Eye = new Paint(.53f, .52f, .44f, 0, .35f);
        private static readonly Paint Iris = new Paint(.075f, .095f, .072f, 0, .45f);
        private static readonly Paint Straw = new Paint(.69f, .45f, .16f, 0, .30f);
        private static readonly Paint StrawLight = new Paint(.85f, .63f, .27f, 0, .33f);
        private static readonly Paint Wood = new Paint(.34f, .19f, .078f, 0, .31f);

        private readonly struct Ring
        {
            public readonly float Y, X, Z, OffsetX, OffsetZ;
            public Ring(float y, float x, float z, float offsetZ = 0, float offsetX = 0)
            { Y = y; X = x; Z = z; OffsetZ = offsetZ; OffsetX = offsetX; }
        }

        public static Prototype Build(bool warrior, int lod)
        {
            if (lod < 0 || lod > 2) throw new ArgumentOutOfRangeException(nameof(lod));
            var positions = RestPose(warrior);
            var builder = new Builder(lod);
            Body(builder, positions, warrior);
            Face(builder, warrior);
            if (warrior) Warrior(builder, positions); else Worker(builder, positions);
            var mesh = builder.Finish(warrior ? "Aven Kingdom Lanceman" : "Aven Kingdom Fieldworker", positions);
            return new Prototype
            {
                Mesh = mesh,
                BoneNames = new[] { "Root", "Hips", "Spine", "Head", "UpperArmL", "LowerArmL", "HandL", "UpperArmR", "LowerArmR", "HandR", "UpperLegL", "LowerLegL", "FootL", "UpperLegR", "LowerLegR", "FootR" },
                BonePositions = positions,
                BoneParents = new[] { -1, Root, Hips, Spine, Spine, UpperArmL, LowerArmL, Spine, UpperArmR, LowerArmR, Hips, UpperLegL, LowerLegL, Hips, UpperLegR, LowerLegR }
            };
        }

        private static Vector3[] RestPose(bool warrior)
        {
            return new[]
            {
                Vector3.zero, new Vector3(0, 1.02f, 0), new Vector3(0, 1.35f, 0), new Vector3(0, 1.74f, .025f),
                new Vector3(-.33f, 1.58f, 0), new Vector3(-.46f, 1.25f, .045f),
                warrior ? new Vector3(-.43f, 1.20f, .30f) : new Vector3(-.48f, 1.01f, .12f),
                new Vector3(.33f, 1.58f, 0), new Vector3(.47f, 1.26f, .035f), new Vector3(.535f, 1.20f, .205f),
                new Vector3(-.145f, .99f, 0), new Vector3(-.17f, .59f, .025f), new Vector3(-.18f, .13f, .03f),
                new Vector3(.145f, .99f, -.025f), new Vector3(.17f, .59f, -.015f), new Vector3(.18f, .13f, -.055f)
            };
        }

        private static void Body(Builder b, Vector3[] bones, bool warrior)
        {
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(.90f,.205f,.135f), new Ring(1.02f,.235f,.153f), new Ring(1.13f,.223f,.158f),
                new Ring(1.30f,.264f,.177f), new Ring(1.47f,.295f,.181f), new Ring(1.57f,.276f,.157f),
                new Ring(1.64f,.125f,.102f)
            }, 16, warrior ? SteelDark : Linen, Spine, .018f);
            b.BlendWaist();
            b.Profile(Vector3.zero, Quaternion.identity, new[] { new Ring(1.60f,.085f,.081f,.012f), new Ring(1.69f,.084f,.080f,.018f), new Ring(1.76f,.106f,.087f,.022f) }, 12, Skin, Head);

            for (int side = 0; side < 2; side++)
            {
                int upperLeg = side == 0 ? UpperLegL : UpperLegR;
                int lowerLeg = side == 0 ? LowerLegL : LowerLegR;
                int foot = side == 0 ? FootL : FootR;
                int upperArm = side == 0 ? UpperArmL : UpperArmR;
                int lowerArm = side == 0 ? LowerArmL : LowerArmR;
                int hand = side == 0 ? HandL : HandR;
                var hip = bones[upperLeg]; var knee = bones[lowerLeg]; var ankle = bones[foot];
                b.Limb(knee - Vector3.up * .025f, hip + Vector3.up * .065f,
                    new[] { new Vector2(.089f,.105f), new Vector2(.131f,.133f), new Vector2(.146f,.145f), new Vector2(.132f,.134f), new Vector2(.101f,.111f) },
                    12, Trousers, upperLeg, .052f);
                b.Limb(ankle + Vector3.up * .015f, knee + Vector3.up * .013f,
                    new[] { new Vector2(.074f,.083f), new Vector2(.082f,.085f), new Vector2(.097f,.103f), new Vector2(.107f,.10f), new Vector2(.099f,.089f) },
                    12, warrior ? LeatherDark : Leather, lowerLeg, .016f);

                // Heel, instep and toe are one continuous swept shoe profile, not a box foot.
                b.Profile(new Vector3(ankle.x, .103f, ankle.z - .11f), Quaternion.Euler(90,0,0), new[]
                {
                    new Ring(0,.066f,.060f), new Ring(.045f,.092f,.088f), new Ring(.125f,.108f,.101f),
                    new Ring(.235f,.112f,.085f), new Ring(.320f,.080f,.048f), new Ring(.362f,.026f,.020f)
                }, 12, warrior ? Steel : LeatherDark, foot);
                if (b.Lod < 2)
                {
                    b.Profile(new Vector3(ankle.x,.036f,ankle.z-.104f), Quaternion.Euler(90,0,0), new[]
                    { new Ring(0,.070f,.023f), new Ring(.055f,.102f,.029f), new Ring(.25f,.120f,.028f), new Ring(.344f,.066f,.023f) }, 10, LeatherDark, foot);
                    b.Limb(ankle + Vector3.up * .32f, ankle + Vector3.up * .375f,
                        new[] { new Vector2(.102f,.106f), new Vector2(.111f,.113f), new Vector2(.105f,.105f) }, 12,
                        warrior ? Gold : LeatherLight, lowerLeg);
                }

                var shoulder = bones[upperArm]; var elbow = bones[lowerArm]; var wrist = bones[hand];
                b.Limb(shoulder, elbow + Vector3.up * .025f,
                    new[] { new Vector2(.117f,.126f), new Vector2(.134f,.135f), new Vector2(.12f,.127f), new Vector2(.096f,.104f), new Vector2(.088f,.096f) },
                    12, warrior ? SteelDark : Linen, upperArm, warrior ? .01f : .045f);
                b.Limb(elbow, wrist,
                    new[] { new Vector2(.079f,.079f), new Vector2(.091f,.085f), new Vector2(.083f,.082f), new Vector2(.065f,.065f), new Vector2(.057f,.060f) },
                    12, warrior ? LeatherDark : Skin, lowerArm);
                if (!warrior)
                {
                    var sleeveEnd = Vector3.Lerp(shoulder, elbow, .85f);
                    b.Limb(sleeveEnd + (shoulder - elbow).normalized * .017f, sleeveEnd + (elbow - shoulder).normalized * .074f,
                        new[] { new Vector2(.102f,.110f), new Vector2(.119f,.117f), new Vector2(.117f,.118f), new Vector2(.10f,.102f) }, 12, Ivory, upperArm);
                }
                Hand(b, wrist, hand, side == 0 ? -1 : 1, warrior || side == 0, side == 1);
            }
        }

        private static void Hand(Builder b, Vector3 wrist, int bone, int side, bool glove, bool grips)
        {
            Paint paint = glove ? Leather : SkinLight;
            var centre = wrist + new Vector3(0, -.063f, .027f);
            b.Oval(centre, new Vector3(.074f,.103f,.061f), 10, paint, bone);
            var thumb = centre + new Vector3(-side * .066f,.015f,.044f);
            if (b.Lod < 2)
                b.Limb(thumb + Vector3.up * .034f, thumb + new Vector3(side*.009f,-.05f,.013f),
                    new[] { new Vector2(.027f,.026f), new Vector2(.033f,.030f), new Vector2(.020f,.021f) }, 8, paint, bone);
            if (b.Lod == 0)
            {
                for (int finger = 0; finger < 4; finger++)
                {
                    float x = (finger - 1.5f) * .030f;
                    var a = centre + new Vector3(x,-.039f,.03f);
                    var end = a + new Vector3(0,-.065f,grips ? -.013f : .016f);
                    b.Limb(a, end, new[] { new Vector2(.017f,.020f), new Vector2(.018f,.021f), new Vector2(.013f,.014f) }, 6, paint, bone);
                }
            }
        }

        private static void Face(Builder b, bool warrior)
        {
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(1.697f,.079f,.081f,.046f), new Ring(1.735f,.122f,.107f,.037f),
                new Ring(1.80f,.153f,.126f,.027f), new Ring(1.858f,.16f,.136f,.023f),
                new Ring(1.915f,.15f,.130f,.020f), new Ring(1.981f,.128f,.116f,.014f),
                new Ring(2.025f,.073f,.071f,.009f), new Ring(2.040f,.021f,.023f,.007f)
            }, 18, Skin, Head);
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(1.800f,.025f,.019f,.190f), new Ring(1.822f,.041f,.030f,.217f),
                new Ring(1.854f,.029f,.027f,.204f), new Ring(1.896f,.020f,.023f,.177f),
                new Ring(1.922f,.009f,.010f,.155f)
            }, 10, SkinLight, Head);
            for (int side = -1; side <= 1; side += 2)
            {
                if (b.Lod < 2)
                {
                    b.Oval(new Vector3(side*.161f,1.842f,.024f), new Vector3(.029f,.053f,.027f), 10, SkinLight, Head);
                    if (b.Lod == 0) b.Oval(new Vector3(side*.172f,1.843f,.044f), new Vector3(.012f,.032f,.008f), 8, SkinShade, Head);
                    var eye = new Vector3(side*.064f,1.884f,.150f);
                    var eyeRotation = Quaternion.Euler(0,side*22,0);
                    b.Plate(EllipseOutline(.020f,.008f,8), eye, eyeRotation, .0025f, .002f, .0008f, Eye, SkinShade, Head);
                    b.Plate(EllipseOutline(.006f,.006f,8), eye + eyeRotation*new Vector3(-side*.001f,0,.004f), eyeRotation, .001f, .001f, 0, Iris, Iris, Head);
                    b.Curve(new[] { new Vector3(side*.033f,1.905f,.162f), new Vector3(side*.07f,1.914f,.157f), new Vector3(side*.107f,1.905f,.133f) }, .012f, 6, Hair, Head);
                    if (b.Lod == 0)
                    {
                        b.Curve(new[] { new Vector3(side*.033f,1.893f,.163f), new Vector3(side*.065f,1.895f,.165f), new Vector3(side*.09f,1.889f,.153f) }, .004f, 5, SkinShade, Head);
                    }
                }
                if (b.Lod < 2)
                    b.Curve(new[] { new Vector3(side*.012f,1.806f,.209f), new Vector3(side*.043f,1.796f,.197f), new Vector3(side*.080f,1.782f,.168f) }, warrior ? .016f : .022f, 8, Hair, Head);
            }
            // A shaped chin and cheek mass leaves the upper face and nose exposed.
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(warrior ? 1.693f : 1.651f,.025f,.041f,.102f), new Ring(1.710f,.081f,.079f,.059f),
                new Ring(1.753f,.125f,.098f,.046f), new Ring(1.787f,.146f,.097f,.032f),
                new Ring(1.817f,.147f,.069f,.003f)
            }, 14, Hair, Head, .075f);
            if (b.Lod < 2)
            {
                b.Curve(new[] { new Vector3(-.041f,1.770f,.163f), new Vector3(0,1.766f,.168f), new Vector3(.041f,1.770f,.163f) }, .007f, 6, SkinShade, Head);
                if (!warrior)
                {
                    for (int lockIndex = -1; lockIndex <= 1; lockIndex++)
                        b.Curve(new[] { new Vector3(lockIndex*.043f,1.742f,.143f), new Vector3(lockIndex*.031f,1.69f,.139f), new Vector3(lockIndex*.018f,1.665f,.121f) }, .012f, 6, HairLight, Head);
                }
            }
            if (!warrior)
            {
                b.Profile(Vector3.zero, Quaternion.identity, new[] { new Ring(1.956f,.161f,.139f,.007f), new Ring(1.991f,.157f,.137f,.002f), new Ring(2.032f,.092f,.089f), new Ring(2.045f,.014f,.014f) }, 16, Hair, Head);
                if (b.Lod < 2)
                    for (int side = -1; side <= 1; side += 2)
                        b.Curve(new[] { new Vector3(side*.143f,1.973f,-.041f), new Vector3(side*.159f,1.891f,-.036f), new Vector3(side*.157f,1.810f,-.046f) }, .033f, 8, HairLight, Head);
            }
        }

        private static void Worker(Builder b, Vector3[] bones)
        {
            // Broad, slightly raised brim and a pinched, rounded crown.
            var hatPivotCorrection = new Vector3(-.1047f,.0027f,0);
            b.Profile(hatPivotCorrection, Quaternion.Euler(0,0,-3), new[]
            {
                new Ring(1.965f,.188f,.151f), new Ring(1.967f,.356f,.292f), new Ring(1.989f,.405f,.325f),
                new Ring(2.006f,.402f,.323f), new Ring(2.003f,.306f,.249f), new Ring(2.011f,.192f,.156f),
                new Ring(2.091f,.175f,.141f), new Ring(2.137f,.123f,.106f), new Ring(2.153f,.061f,.055f)
            }, 28, Straw, Head);
            // Same angular tessellation as the crown and a real radial clearance prevent saw-tooth intersections.
            b.Profile(hatPivotCorrection, Quaternion.Euler(0,0,-3), new[] { new Ring(2.016f,.201f,.165f), new Ring(2.056f,.192f,.157f) }, 28, Team, Head);

            ClothPanel(b, -.225f, -.019f, 1.075f, 1.573f, Team, Spine, false, .014f);
            ClothPanel(b, .019f, .225f, 1.075f, 1.573f, Team, Spine, false, .014f);
            ClothPanel(b, -.255f, -.019f, .746f, 1.092f, Team, Hips, false, .017f, Ivory);
            ClothPanel(b, .019f, .255f, .746f, 1.092f, Team, Hips, false, .017f, Ivory);
            ClothPanel(b, -.23f, .23f, .80f, 1.54f, Team, Spine, true, .015f);
            b.Ribbon(new[] { new Vector3(-.247f,1.557f,.128f), new Vector3(-.172f,1.432f,.172f), new Vector3(-.059f,1.263f,.197f), new Vector3(.185f,1.103f,.144f) }, .054f, .010f, Leather, Spine);
            b.Ribbon(new[] { new Vector3(.247f,1.557f,.131f), new Vector3(.172f,1.432f,.178f), new Vector3(.059f,1.263f,.206f), new Vector3(-.185f,1.103f,.146f) }, .046f, .009f, LeatherLight, Spine);
            Belt(b, false);

            // Rounded canvas pack with shaped flap, two leather straps, and a rolled blanket.
            b.Profile(new Vector3(0,0,-.244f), Quaternion.identity, new[]
            {
                new Ring(.99f,.141f,.095f), new Ring(1.045f,.214f,.139f), new Ring(1.31f,.224f,.155f),
                new Ring(1.43f,.193f,.141f), new Ring(1.476f,.12f,.095f)
            }, 14, LeatherLight, Spine, .018f);
            b.Plate(new[] { new Vector2(-.185f,.08f),new Vector2(-.178f,-.08f),new Vector2(0,-.116f),new Vector2(.178f,-.08f),new Vector2(.185f,.08f) },
                new Vector3(0,1.32f,-.396f), Quaternion.Euler(0,180,0), .019f, .016f, .018f, Leather, LeatherLight, Spine);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Ribbon(new[] { new Vector3(side*.137f,1.47f,-.339f), new Vector3(side*.16f,1.32f,-.418f), new Vector3(side*.145f,1.046f,-.369f) }, .031f, .012f, LeatherDark, Spine);
                b.Ribbon(new[] { new Vector3(side*.185f,1.52f,-.168f), new Vector3(side*.226f,1.578f,0), new Vector3(side*.193f,1.354f,.184f) }, .038f, .014f, Leather, Spine);
            }
            b.Limb(new Vector3(-.251f,1.469f,-.285f),new Vector3(.251f,1.469f,-.285f),
                new[] { new Vector2(.063f,.063f),new Vector2(.091f,.09f),new Vector2(.084f,.091f),new Vector2(.066f,.065f) }, 12, Linen, Spine);
            b.Ribbon(new[] { new Vector3(-.215f,1.192f,-.382f),new Vector3(0,1.179f,-.414f),new Vector3(.215f,1.192f,-.382f) }, .089f, .008f, Team, Spine);
            if (b.Lod < 2)
            {
                Pouch(b,new Vector3(-.28f,.924f,.094f),Quaternion.Euler(0,-25,0),Hips);
                Pouch(b,new Vector3(.26f,.925f,.076f),Quaternion.Euler(0,35,0),Hips);
                Horizon(b,new Vector3(-.109f,.867f,.210f),Quaternion.identity,.24f,Hips);
            }

            var shaftBottom = new Vector3(.541f,.12f,.233f);
            var shaftTop = new Vector3(.541f,1.776f,.233f);
            b.Limb(shaftBottom, shaftTop, new[] { new Vector2(.027f,.027f),new Vector2(.034f,.034f),new Vector2(.028f,.028f) }, 10, Wood, HandR);
            b.Curve(new[] { new Vector3(.395f,1.779f,.231f),new Vector3(.46f,1.75f,.237f),new Vector3(.541f,1.744f,.241f),new Vector3(.62f,1.75f,.237f),new Vector3(.687f,1.779f,.231f) }, .025f, 8, SteelDark, HandR);
            for (int prong = -1; prong <= 1; prong++)
            {
                float x = .541f + prong*.145f;
                b.Limb(new Vector3(x,1.766f,.23f),new Vector3(x+prong*.024f,2.111f,.189f),
                    new[] { new Vector2(.024f,.022f),new Vector2(.023f,.021f),new Vector2(.017f,.017f),new Vector2(.003f,.005f) }, 8, Steel, HandR);
            }
            b.Limb(new Vector3(.541f,1.64f,.233f),new Vector3(.541f,1.767f,.233f), new[] { new Vector2(.036f,.036f),new Vector2(.040f,.04f),new Vector2(.03f,.03f) }, 10, SteelDark, HandR);
        }

        private static void Warrior(Builder b, Vector3[] bones)
        {
            // Fitted cuirass with a broad breast ridge and narrowing waist.
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(1.04f,.241f,.168f,.004f), new Ring(1.11f,.25f,.179f,.001f), new Ring(1.27f,.282f,.211f,.006f),
                new Ring(1.43f,.307f,.219f,.007f), new Ring(1.553f,.274f,.188f), new Ring(1.602f,.163f,.133f)
            }, 18, Steel, Spine);
            b.Profile(Vector3.zero, Quaternion.identity, new[] { new Ring(1.592f,.133f,.117f),new Ring(1.629f,.13f,.114f),new Ring(1.65f,.093f,.095f) }, 14, Gold, Spine);
            ClothPanel(b,-.191f,-.009f,1.075f,1.513f,Team,Spine,false,.060f);
            ClothPanel(b,.009f,.191f,1.075f,1.513f,Ivory,Spine,false,.060f);
            ClothPanel(b,-.253f,-.012f,.684f,1.095f,Team,Hips,false,.032f,Gold);
            ClothPanel(b,.012f,.253f,.684f,1.095f,Ivory,Hips,false,.032f,Gold);
            ClothPanel(b,-.292f,.292f,.674f,1.568f,Team,Spine,true,.037f);
            for (int side = -1; side <= 1; side += 2)
            {
                int upper = side < 0 ? UpperArmL : UpperArmR;
                int lower = side < 0 ? LowerArmL : LowerArmR;
                int leg = side < 0 ? LowerLegL : LowerLegR;
                var shoulder = bones[upper];
                b.Profile(shoulder + new Vector3(side*.012f,-.152f,0), Quaternion.identity, new[]
                {
                    new Ring(0,.169f,.18f),new Ring(.043f,.192f,.194f),new Ring(.117f,.17f,.18f),
                    new Ring(.173f,.118f,.138f),new Ring(.200f,.033f,.047f)
                }, 16, Steel, upper);
                b.Profile(shoulder + new Vector3(side*.012f,-.152f,0),Quaternion.identity,
                    new[] { new Ring(.006f,.179f,.19f),new Ring(.026f,.192f,.199f) },16,Gold,upper);
                var elbow = bones[lower]; var wrist = bones[side < 0 ? HandL : HandR];
                b.Limb(Vector3.Lerp(elbow,wrist,.18f),Vector3.Lerp(elbow,wrist,.94f),
                    new[] { new Vector2(.100f,.100f),new Vector2(.113f,.106f),new Vector2(.092f,.091f),new Vector2(.076f,.076f) },14,Steel,lower);
                if (b.Lod < 2)
                {
                    b.Oval(elbow + new Vector3(0,-.005f,-.015f),new Vector3(.102f,.097f,.091f),12,SteelEdge,lower);
                    b.Limb(Vector3.Lerp(elbow,wrist,.83f),Vector3.Lerp(elbow,wrist,.96f),
                        new[] { new Vector2(.088f,.086f),new Vector2(.089f,.087f) },12,Gold,lower);
                    var knee = bones[leg];
                    b.Oval(knee + new Vector3(0,.009f,.083f),new Vector3(.112f,.079f,.061f),10,SteelEdge,leg);
                    var ankle=bones[side<0?FootL:FootR];
                    b.Profile(Vector3.zero,Quaternion.identity,new[]
                    {
                        new Ring(.145f,.078f,.094f,ankle.z,ankle.x),
                        new Ring(.237f,.087f,.110f,Mathf.Lerp(ankle.z,knee.z,.25f),Mathf.Lerp(ankle.x,knee.x,.25f)),
                        new Ring(.392f,.108f,.122f,Mathf.Lerp(ankle.z,knee.z,.61f),Mathf.Lerp(ankle.x,knee.x,.61f)),
                        new Ring(.515f,.107f,.113f,knee.z,knee.x),new Ring(.555f,.088f,.095f,knee.z,knee.x)
                    },12,Steel,leg,0,-1.22f,2.44f);
                    var hip = bones[side < 0 ? UpperLegL : UpperLegR];
                    b.Plate(new[] { new Vector2(-.107f,.086f),new Vector2(-.13f,-.088f),new Vector2(.111f,-.103f),new Vector2(.112f,.085f) },
                        new Vector3(side*.244f,.971f,.073f),Quaternion.Euler(0,side*47,side*5),.023f,.013f,.01f,Steel,Gold,Hips);
                }
            }
            Belt(b,true);
            Helmet(b);
            if (b.Lod < 2)
            {
                Horizon(b,new Vector3(-.095f,1.30f,.284f),Quaternion.identity,.28f,Spine);
                Pouch(b,new Vector3(.292f,1.006f,-.026f),Quaternion.Euler(0,72,0),Hips);
                // Belt sword is secondary decoration; the reedguard's tactical weapon remains its lance.
                var swordStart = new Vector3(.302f,.978f,-.045f); var swordEnd = new Vector3(.42f,.48f,-.10f);
                b.Limb(swordStart,swordEnd,new[] { new Vector2(.038f,.025f),new Vector2(.041f,.027f),new Vector2(.026f,.019f),new Vector2(.014f,.014f) },8,LeatherDark,Hips);
                b.Limb(swordStart,new Vector3(.268f,1.119f,-.034f),new[] { new Vector2(.022f,.024f),new Vector2(.021f,.021f) },8,Leather,Hips);
                b.Curve(new[] { new Vector3(.229f,1.007f,-.028f),new Vector3(.292f,1.007f,-.029f),new Vector3(.36f,1.024f,-.034f) },.013f,6,Gold,Hips);
            }

            var shieldCentre = new Vector3(-.454f,1.175f,.375f);
            var shieldRotation = Quaternion.Euler(3,-12,-6);
            var outline = new[] { new Vector2(-.219f,.384f),new Vector2(-.291f,.28f),new Vector2(-.26f,-.145f),new Vector2(0,-.487f),new Vector2(.26f,-.145f),new Vector2(.291f,.28f),new Vector2(.219f,.384f) };
            b.Plate(outline,shieldCentre,shieldRotation,.040f,.035f,.036f,Team,Gold,HandL);
            Horizon(b,shieldCentre + shieldRotation * new Vector3(0,.01f,.083f),shieldRotation,.77f,HandL);
            if (b.Lod == 0)
            {
                foreach (var point in outline)
                {
                    var rivet = shieldCentre + shieldRotation * new Vector3(point.x*.945f,point.y*.945f,.019f);
                    b.Oval(rivet,new Vector3(.012f,.012f,.009f),6,SteelEdge,HandL);
                }
            }
            var shaftBottom = new Vector3(.541f,.15f,.247f);
            var shaftTop = new Vector3(.541f,2.623f,.247f);
            b.Limb(shaftBottom,shaftTop,new[] { new Vector2(.027f,.027f),new Vector2(.036f,.036f),new Vector2(.030f,.030f) },12,Wood,HandR);
            b.Limb(new Vector3(.541f,2.511f,.247f),new Vector3(.541f,2.653f,.247f),new[] { new Vector2(.041f,.041f),new Vector2(.035f,.035f),new Vector2(.027f,.027f) },12,Gold,HandR);
            b.Plate(new[] { new Vector2(-.035f,-.115f),new Vector2(-.105f,-.01f),new Vector2(0,.260f),new Vector2(.105f,-.01f),new Vector2(.035f,-.115f) },
                new Vector3(.541f,2.664f,.247f),Quaternion.identity,.011f,.012f,.025f,SteelEdge,Steel,HandR);
            if (b.Lod < 2)
            {
                for (int wrap = 0; wrap < 4; wrap++)
                    b.Limb(new Vector3(.541f,1.013f+wrap*.051f,.247f),new Vector3(.541f,1.042f+wrap*.051f,.247f),new[] { new Vector2(.040f,.040f),new Vector2(.039f,.039f) },10,Leather,HandR);
            }
        }

        private static void Helmet(Builder b)
        {
            b.Profile(Vector3.zero,Quaternion.identity,new[]
            {
                new Ring(1.977f,.18f,.158f,.004f),new Ring(2.016f,.178f,.157f),new Ring(2.078f,.154f,.145f,-.004f),
                new Ring(2.147f,.10f,.106f,-.009f),new Ring(2.189f,.029f,.038f,-.013f)
            },20,Steel,Head);
            b.EllipseCurve(new Vector3(0,1.978f,.004f),.183f,.163f,.011f,24,Gold,Head);
            b.Profile(Vector3.zero,Quaternion.identity,new[] { new Ring(1.797f,.153f,.129f,-.003f),new Ring(1.873f,.175f,.15f,-.003f),new Ring(1.984f,.179f,.158f) },18,SteelDark,Head,0,.94f,Mathf.PI*2-1.88f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Plate(new[] { new Vector2(-.032f,.105f),new Vector2(-.049f,-.072f),new Vector2(.014f,-.095f),new Vector2(.045f,.094f) },
                    new Vector3(side*.147f,1.875f,.116f),Quaternion.Euler(0,side*39,side*-8),.013f,.01f,.01f,Steel,Gold,Head);
            }
            if (b.Lod < 2)
            {
                b.Ribbon(new[] { new Vector3(0,1.982f,.174f),new Vector3(0,2.083f,.129f),new Vector3(0,2.202f,-.013f),new Vector3(0,2.055f,-.149f) },.026f,.008f,SteelEdge,Head);
                b.Plate(new[] { new Vector2(-.022f,.018f),new Vector2(0,-.026f),new Vector2(.022f,.018f) },new Vector3(0,1.984f,.177f),Quaternion.identity,.008f,.002f,.004f,Gold,Gold,Head);
            }
        }

        private static void Belt(Builder b, bool warrior)
        {
            float depth=warrior?.228f:.210f;
            b.Profile(Vector3.zero,Quaternion.identity,new[] { new Ring(1.027f,.252f,depth-.013f),new Ring(1.037f,.263f,depth),new Ring(1.102f,.261f,depth),new Ring(1.109f,.25f,depth-.013f) },16,Leather,Hips);
            var buckle = new Vector3(.018f,1.068f,depth+.012f);
            b.Plate(new[] { new Vector2(-.053f,.042f),new Vector2(-.053f,-.042f),new Vector2(.053f,-.042f),new Vector2(.053f,.042f) },buckle,Quaternion.identity,.013f,.009f,.002f,LeatherDark,Gold,Hips);
            b.Ribbon(new[] { buckle+new Vector3(0,-.027f,.018f),buckle+new Vector3(0,.027f,.018f) },.009f,.003f,Gold,Hips);
            if (b.Lod == 0)
                for (int rivet = 0; rivet < 3; rivet++)
                    b.Oval(new Vector3(-.084f-rivet*.042f,1.069f,.184f-rivet*.013f),new Vector3(.005f,.005f,.006f),6,Gold,Hips);
        }

        private static void Pouch(Builder b, Vector3 centre, Quaternion rotation, int bone)
        {
            b.Profile(centre,rotation,new[] { new Ring(-.117f,.049f,.035f),new Ring(-.098f,.079f,.052f),new Ring(.082f,.084f,.052f),new Ring(.112f,.06f,.038f) },10,Leather,bone);
            b.Plate(new[] { new Vector2(-.074f,.052f),new Vector2(-.069f,-.035f),new Vector2(0,-.071f),new Vector2(.069f,-.035f),new Vector2(.074f,.052f) },
                centre+rotation*new Vector3(0,.03f,.053f),rotation,.008f,.008f,.005f,LeatherLight,LeatherDark,bone);
            b.Oval(centre+rotation*new Vector3(0,-.02f,.070f),new Vector3(.010f,.01f,.006f),6,Gold,bone);
        }

        // An original forked horizon mark: a stem and two rising paths above one broad horizon.
        private static void Horizon(Builder b, Vector3 centre, Quaternion rotation, float scale, int bone)
        {
            var branches = new[]
            {
                new[] { new Vector3(0,-.20f,0),new Vector3(0,.105f,0),new Vector3(-.14f,.235f,0) },
                new[] { new Vector3(0,.105f,0),new Vector3(.14f,.235f,0) },
                new[] { new Vector3(-.19f,-.045f,0),new Vector3(.19f,-.045f,0) }
            };
            foreach (var branch in branches)
            {
                var points = new Vector3[branch.Length];
                for (int i=0;i<points.Length;i++) points[i]=centre+rotation*(branch[i]*scale);
                b.Ribbon(points,.047f*scale,.006f,Gold,bone,rotation*Vector3.forward);
            }
        }

        private static void ClothPanel(Builder b, float left, float right, float low, float high, Paint paint, int bone, bool back, float clearance, Paint? trim=null)
        {
            Func<float,float,Vector3> sample=(u,v) =>
            {
                float y=Mathf.Lerp(low,high,v); float x=Mathf.Lerp(left,right,u);
                float waist=1-Mathf.Clamp01(Mathf.Abs(y-1.08f)/.48f);
                float spread=1-.17f*waist;
                float bodyWidth=y>1.08f?Mathf.Lerp(.238f,.307f,Mathf.Clamp01((y-1.08f)/.4f)):Mathf.Lerp(.282f,.238f,Mathf.Clamp01((y-.70f)/.38f));
                float bodyDepth=y>1.10f?Mathf.Lerp(.161f,.187f,Mathf.Clamp01((y-1.10f)/.33f)):Mathf.Lerp(.213f,.161f,Mathf.Clamp01((y-.75f)/.35f));
                float relativeX=x*spread/bodyWidth;
                float z=bodyDepth*Mathf.Sqrt(Mathf.Max(.10f,1-relativeX*relativeX))+clearance;
                z+=Mathf.Sin(u*Mathf.PI*3+.3f)*.012f*(1-v*.40f);
                return new Vector3(x*spread,y+(1-v)*.012f*Mathf.Cos(u*Mathf.PI*2),back?-z:z);
            };
            var front=back?Vector3.back:Vector3.forward;
            int columns=b.Lod==0?4:b.Lod==1?3:2;
            b.Sheet(sample,columns,b.Lod==0?5:b.Lod==1?3:2,paint,bone,front,true);
            if(trim.HasValue)
                b.Sheet((u,v)=>sample(u,v*.018f/(high-low))+front*.003f,columns,1,trim.Value,bone,front,false);
        }

        private static Vector2[] EllipseOutline(float width, float height, int sides)
        {
            var points=new Vector2[sides];
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;points[i]=new Vector2(Mathf.Cos(a)*width,Mathf.Sin(a)*height);}
            return points;
        }

        private sealed class Builder
        {
            public readonly int Lod;
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<Vector3> normals=new List<Vector3>();
            private readonly List<Color> colors=new List<Color>();
            private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<Vector2> surface=new List<Vector2>();
            private readonly List<BoneWeight> weights=new List<BoneWeight>();
            private readonly List<int> triangles=new List<int>();
            public Builder(int lod){Lod=lod;}
            // Called just after the base torso: preserve the pelvis seam during a bend,
            // without deforming rigid gear, the backpack or the weapon attachments.
            public void BlendWaist()
            {
                for(int i=0;i<vertices.Count;i++)
                {
                    float spine=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,1.30f,vertices[i].y));
                    weights[i]=spine>=.5f
                        ?new BoneWeight{boneIndex0=Spine,weight0=spine,boneIndex1=Hips,weight1=1-spine}
                        :new BoneWeight{boneIndex0=Hips,weight0=1-spine,boneIndex1=Spine,weight1=spine};
                }
            }
            private int Sides(int high){return Lod==0?Mathf.Max(5,Mathf.RoundToInt(high*.80f)):Lod==1?Mathf.Max(5,Mathf.RoundToInt(high*.46f)):Mathf.Max(4,high/4);}
            // Unity's Vector3.normalized discards vectors shorter than 1e-5. Finite-difference
            // surface derivatives legitimately produce smaller cross-products on facial detail.
            private static Vector3 UnitNormal(Vector3 value)
            {
                float squared=value.sqrMagnitude;
                return squared>1e-24f&&!float.IsNaN(squared)&&!float.IsInfinity(squared)?value/Mathf.Sqrt(squared):Vector3.zero;
            }
            private int Add(Vector3 position,Vector3 normal,Vector2 tex,Paint paint,int bone)
            {
                int index=vertices.Count;vertices.Add(position);normals.Add(UnitNormal(normal));colors.Add(paint.Color);
                uv.Add(tex);surface.Add(paint.Surface);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});return index;
            }
            private void Tri(int a,int b,int c)
            {
                if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-14f)return;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
            private void Flat(Vector3 a,Vector3 b,Vector3 c,Paint paint,int bone)
            {
                var normal=UnitNormal(Vector3.Cross(b-a,c-a));if(normal.sqrMagnitude<.01f)return;
                int first=Add(a,normal,Vector2.zero,paint,bone);Add(b,normal,Vector2.right,paint,bone);Add(c,normal,Vector2.up,paint,bone);Tri(first,first+1,first+2);
            }
            private Ring[] Reduce(Ring[] source)
            {
                if(Lod==0||source.Length<=3)return source;
                if(Lod==2)
                {
                    int widest=1;
                    for(int i=2;i<source.Length-1;i++)if(source[i].X*source[i].Z>source[widest].X*source[widest].Z)widest=i;
                    return new[]{source[0],source[widest],source[source.Length-1]};
                }
                if(source.Length<=5)return source;
                var result=new List<Ring>{source[0]};
                for(int i=1;i<source.Length-1;i+=2)result.Add(source[i]);
                result.Add(source[source.Length-1]);return result.ToArray();
            }
            private static Vector3 RingPoint(Ring ring,float angle,float fold)
            {
                float ripple=1+Mathf.Cos(angle*6)*fold;
                return new Vector3(ring.OffsetX+Mathf.Sin(angle)*ring.X*ripple,ring.Y,ring.OffsetZ+Mathf.Cos(angle)*ring.Z*ripple);
            }
            public void Profile(Vector3 origin,Quaternion rotation,Ring[] source,int sides,Paint paint,int bone,float fold=0,float start=0,float arc=Mathf.PI*2)
            {
                var rings=Reduce(source);int count=Sides(sides);int first=vertices.Count;
                for(int r=0;r<rings.Length;r++)
                {
                    var before=rings[Mathf.Max(0,r-1)];var after=rings[Mathf.Min(rings.Length-1,r+1)];
                    for(int j=0;j<=count;j++)
                    {
                        float angle=start+arc*j/count;var p=RingPoint(rings[r],angle,fold);
                        var tangent=RingPoint(rings[r],angle+.002f,fold)-RingPoint(rings[r],angle-.002f,fold);
                        var vertical=RingPoint(after,angle,fold)-RingPoint(before,angle,fold);
                        var n=UnitNormal(Vector3.Cross(tangent,vertical));
                        Add(origin+rotation*p,rotation*n,new Vector2((float)j/count,(float)r/(rings.Length-1)),paint,bone);
                    }
                }
                for(int r=0;r<rings.Length-1;r++)for(int j=0;j<count;j++)
                {int a=first+r*(count+1)+j;int c=a+count+1;Tri(a,a+1,c+1);Tri(a,c+1,c);}
                for(int j=0;j<count;j++)
                {
                    var bottom=rings[0];var top=rings[rings.Length-1];
                    var lowCentre=origin+rotation*new Vector3(bottom.OffsetX,bottom.Y,bottom.OffsetZ);
                    var highCentre=origin+rotation*new Vector3(top.OffsetX,top.Y,top.OffsetZ);
                    Flat(lowCentre,origin+rotation*RingPoint(bottom,start+arc*(j+1)/count,fold),origin+rotation*RingPoint(bottom,start+arc*j/count,fold),paint,bone);
                    Flat(highCentre,origin+rotation*RingPoint(top,start+arc*j/count,fold),origin+rotation*RingPoint(top,start+arc*(j+1)/count,fold),paint,bone);
                }
                if(arc<Mathf.PI*2-.001f)
                {
                    for(int r=0;r<rings.Length-1;r++)foreach(float angle in new[]{start,start+arc})
                    {
                        var a=origin+rotation*RingPoint(rings[r],angle,fold);var d=origin+rotation*RingPoint(rings[r+1],angle,fold);
                        var c=origin+rotation*new Vector3(rings[r+1].OffsetX,rings[r+1].Y,rings[r+1].OffsetZ);
                        var e=origin+rotation*new Vector3(rings[r].OffsetX,rings[r].Y,rings[r].OffsetZ);
                        if(angle==start){Flat(a,d,c,paint,bone);Flat(a,c,e,paint,bone);}else{Flat(a,c,d,paint,bone);Flat(a,e,c,paint,bone);}
                    }
                }
            }
            public void Limb(Vector3 from,Vector3 to,Vector2[] radii,int sides,Paint paint,int bone,float fold=0)
            {
                var axis=(to-from).normalized;var forward=Vector3.ProjectOnPlane(Vector3.forward,axis);
                if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.up,axis);
                var q=Quaternion.LookRotation(forward.normalized,axis);float length=(to-from).magnitude;
                var rings=new Ring[radii.Length];
                for(int i=0;i<rings.Length;i++)rings[i]=new Ring(length*i/(rings.Length-1),radii[i].x,radii[i].y);
                Profile(from,q,rings,sides,paint,bone,fold);
            }
            public void Oval(Vector3 centre,Vector3 radius,int sides,Paint paint,int bone)
            {
                float extent=Mathf.Max(radius.x,Mathf.Max(radius.y,radius.z));
                int count=extent<.025f?3:Lod==0?5:Lod==1?4:3;var rings=new Ring[count];
                for(int i=0;i<count;i++)
                {float t=Mathf.Lerp(-.975f,.975f,(float)i/(count-1));float w=Mathf.Sqrt(1-t*t);rings[i]=new Ring(t*radius.y,w*radius.x,w*radius.z);}
                Profile(centre,Quaternion.identity,rings,sides,paint,bone);
            }
            public void Curve(Vector3[] points,float radius,int sides,Paint paint,int bone)
            {
                if(points.Length<2)return;int count=Sides(sides);int first=vertices.Count;
                for(int i=0;i<points.Length;i++)
                {
                    var direction=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                    var axis=Vector3.Cross(direction,Mathf.Abs(direction.z)<.8f?Vector3.forward:Vector3.up).normalized;
                    var other=Vector3.Cross(direction,axis).normalized;
                    for(int j=0;j<=count;j++)
                    {float a=j*Mathf.PI*2/count;var normal=axis*Mathf.Cos(a)+other*Mathf.Sin(a);Add(points[i]+normal*radius,normal,new Vector2((float)j/count,(float)i/(points.Length-1)),paint,bone);}
                }
                for(int i=0;i<points.Length-1;i++)for(int j=0;j<count;j++)
                {int a=first+i*(count+1)+j;int c=a+count+1;Tri(a,a+1,c+1);Tri(a,c+1,c);}
                for(int j=0;j<count;j++)
                {
                    Flat(points[0],vertices[first+j+1],vertices[first+j],paint,bone);
                    int last=first+(points.Length-1)*(count+1);Flat(points[points.Length-1],vertices[last+j],vertices[last+j+1],paint,bone);
                }
            }
            public void EllipseCurve(Vector3 centre,float width,float depth,float radius,int samples,Paint paint,int bone)
            {
                int count=Sides(samples);var points=new Vector3[count+1];
                for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;points[i]=centre+new Vector3(Mathf.Sin(a)*width,0,Mathf.Cos(a)*depth);}
                Curve(points,radius,6,paint,bone);
            }
            public void Plate(Vector2[] outline,Vector3 centre,Quaternion rotation,float depth,float bevel,float bulge,Paint face,Paint edge,int bone)
            {
                float extent=0;foreach(var point in outline)extent=Mathf.Max(extent,Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.y)));
                float inset=1-Mathf.Clamp(bevel/Mathf.Max(.001f,extent),0,.45f);
                // Accept either winding. Front is always local +Z.
                float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var c=outline[(i+1)%outline.Length];area+=a.x*c.y-c.x*a.y;}
                var top=centre+rotation*new Vector3(0,0,depth+bulge);var back=centre-rotation*Vector3.forward*.012f;
                for(int i=0;i<outline.Length;i++)
                {
                    int j=(i+1)%outline.Length;var a=outline[area>0?i:j];var c=outline[area>0?j:i];
                    var ao=centre+rotation*new Vector3(a.x,a.y,0);var co=centre+rotation*new Vector3(c.x,c.y,0);
                    var ai=centre+rotation*new Vector3(a.x*inset,a.y*inset,depth);var ci=centre+rotation*new Vector3(c.x*inset,c.y*inset,depth);
                    Flat(ai,ci,top,face,bone);Flat(ao,co,ci,edge,bone);Flat(ao,ci,ai,edge,bone);Flat(co,ao,back,edge,bone);
                }
            }
            public void Ribbon(Vector3[] points,float width,float depth,Paint paint,int bone,Vector3? normalOverride=null)
            {
                var normal=normalOverride??Vector3.forward;
                for(int i=0;i<points.Length-1;i++)
                {
                    var direction=(points[i+1]-points[i]).normalized;var across=Vector3.Cross(direction,normal).normalized*width*.5f;
                    var a=points[i]-across;var c=points[i]+across;var d=points[i+1]+across;var e=points[i+1]-across;
                    var raised=normal*depth;
                    if(Vector3.Dot(Vector3.Cross(c-a,d-a),normal)<0){var swap=a;a=c;c=swap;swap=d;d=e;e=swap;}
                    Flat(a+raised,c+raised,d+raised,paint,bone);Flat(a+raised,d+raised,e+raised,paint,bone);
                    if(Lod<2)
                    {
                        Flat(a,e,e+raised,paint,bone);Flat(a,e+raised,a+raised,paint,bone);
                        Flat(c+raised,d+raised,d,paint,bone);Flat(c+raised,d,c,paint,bone);
                    }
                }
            }
            public void Sheet(Func<float,float,Vector3> sample,int columns,int rows,Paint paint,int bone,Vector3 forward,bool twoSided)
            {
                int first=vertices.Count;
                for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                {
                    float u=(float)x/columns,v=(float)y/rows;var p=sample(u,v);
                    var du=sample(Mathf.Min(1,u+.001f),v)-sample(Mathf.Max(0,u-.001f),v);
                    var dv=sample(u,Mathf.Min(1,v+.001f))-sample(u,Mathf.Max(0,v-.001f));
                    var n=UnitNormal(Vector3.Cross(du,dv));if(Vector3.Dot(n,forward)<0)n=-n;
                    Add(p,n,new Vector2(u,v),paint,bone);
                }
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {
                    int a=first+y*(columns+1)+x,c=a+columns+1;
                    bool reverse=Vector3.Dot(Vector3.Cross(vertices[a+1]-vertices[a],vertices[c+1]-vertices[a]),forward)<0;
                    if(reverse){Tri(a,c+1,a+1);Tri(a,c,c+1);}else{Tri(a,a+1,c+1);Tri(a,c+1,c);}
                }
                if(!twoSided)return;
                int backFirst=vertices.Count;int count=(columns+1)*(rows+1);
                for(int i=0;i<count;i++)Add(vertices[first+i]-normals[first+i]*.003f,-normals[first+i],uv[first+i],paint,bone);
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {
                    int a=backFirst+y*(columns+1)+x,c=a+columns+1;
                    bool reverse=Vector3.Dot(Vector3.Cross(vertices[a+1]-vertices[a],vertices[c+1]-vertices[a]),-forward)<0;
                    if(reverse){Tri(a,c+1,a+1);Tri(a,c,c+1);}else{Tri(a,a+1,c+1);Tri(a,c+1,c);}
                }
            }
            private void RepairNormalsAndCompact()
            {
                var used=new bool[vertices.Count];var weighted=new Vector3[vertices.Count];var largest=new Vector3[vertices.Count];
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int a=triangles[i],c=triangles[i+1],d=triangles[i+2];
                    var face=Vector3.Cross(vertices[c]-vertices[a],vertices[d]-vertices[a]);
                    foreach(int index in new[]{a,c,d})
                    {
                        used[index]=true;weighted[index]+=face;
                        if(face.sqrMagnitude>largest[index].sqrMagnitude)largest[index]=face;
                    }
                }
                var remap=new int[vertices.Count];int write=0;
                for(int read=0;read<vertices.Count;read++)
                {
                    if(!used[read]){remap[read]=-1;continue;}
                    var normal=normals[read];
                    if(float.IsNaN(normal.sqrMagnitude)||float.IsInfinity(normal.sqrMagnitude)||normal.sqrMagnitude<.25f)
                    {
                        normal=UnitNormal(weighted[read]);
                        if(normal.sqrMagnitude<.25f)normal=UnitNormal(largest[read]);
                        if(normal.sqrMagnitude<.25f)throw new InvalidOperationException("Referenced mesh vertex has no finite incident surface normal.");
                    }
                    remap[read]=write;vertices[write]=vertices[read];normals[write]=normal;colors[write]=colors[read];
                    uv[write]=uv[read];surface[write]=surface[read];weights[write]=weights[read];write++;
                }
                for(int i=0;i<triangles.Count;i++)triangles[i]=remap[triangles[i]];
                int removed=vertices.Count-write;
                vertices.RemoveRange(write,removed);normals.RemoveRange(write,removed);colors.RemoveRange(write,removed);
                uv.RemoveRange(write,removed);surface.RemoveRange(write,removed);weights.RemoveRange(write,removed);
            }
            public Mesh Finish(string name,Vector3[] restPositions)
            {
                RepairNormalsAndCompact();
                var mesh=new Mesh{name=name+" LOD"+Lod,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,surface);mesh.SetTriangles(triangles,0);
                mesh.boneWeights=weights.ToArray();var bindposes=new Matrix4x4[restPositions.Length];
                for(int i=0;i<bindposes.Length;i++)bindposes[i]=Matrix4x4.Translate(-restPositions[i]);
                mesh.bindposes=bindposes;mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
