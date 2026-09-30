using UnityEngine;

namespace Emberfield.Presentation
{
    internal static class AlphaSiegeVisuals
    {
        private static readonly Mesh[] Projectiles = new Mesh[4];
        internal static Mesh Projectile(int kind)
        {
            if (Projectiles[kind] != null) return Projectiles[kind];
            var b = new AlphaMeshBuilder();
            if (kind == 3)
            {
                b.Rock(Vector3.zero, Vector3.one * .065f, 10, new Color(.36f, .34f, .29f));
                b.Beam(new Vector3(0, 0, -.24f), Vector3.zero, .018f, .018f, new Color(1, .73f, .30f));
            }
            else if (kind == 1) b.Rock(Vector3.zero, Vector3.one * .29f, 6, new Color(.56f, .54f, .47f));
            else if (kind == 2)
            {
                b.Rock(Vector3.zero, new Vector3(.28f, .28f, .45f), 6, new Color(1, .52f, .13f));
                b.Rock(new Vector3(0, 0, -.24f), new Vector3(.14f, .14f, .38f), 5, new Color(.86f, .22f, .055f));
            }
            else
            {
                b.Beam(new Vector3(0, 0, -.38f), new Vector3(0, 0, .36f), .026f, .026f, new Color(.53f, .34f, .17f));
                b.Triangle(new Vector3(-.095f, 0, .30f), new Vector3(0, 0, .49f), new Vector3(.095f, 0, .30f), new Color(.73f, .77f, .73f));
                b.Triangle(new Vector3(.095f, 0, .30f), new Vector3(0, 0, .49f), new Vector3(-.095f, 0, .30f), new Color(.73f, .77f, .73f));
                b.Box(new Vector3(0, 0, -.28f), new Vector3(.14f, .035f, .13f), new Color(.82f, .77f, .61f));
            }
            return Projectiles[kind] = b.Mesh("Alpha shared projectile " + kind);
        }
    }
}
