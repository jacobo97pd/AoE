using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    // Instances share baked geometry and a material. Only the small team tint is per renderer.
    public static class ArtKit
    {
        public const string CatalogPath = "Art/EmberfieldKit";
        private static readonly int TeamColor = Shader.PropertyToID("_TeamColor");
        private static readonly MaterialPropertyBlock Tint = new MaterialPropertyBlock();
        private static ArtKitCatalog catalog;
        public static ArtKitCatalog Catalog => catalog != null ? catalog : catalog = Resources.Load<ArtKitCatalog>(CatalogPath);

        public static Transform CreateUnit(string definitionId, Transform parent, int ownerId)
            => Create(Find(Catalog != null ? Catalog.Units : null, definitionId), parent, ownerId, "Aven unit ", Vector3.one);

        public static Transform CreateBuilding(string definitionId, Transform parent, int ownerId, float width, float depth)
            => Create(Find(Catalog != null ? Catalog.Buildings : null, definitionId), parent, ownerId, "Aven building ", new Vector3(width, 1, depth));

        public static Transform CreateResource(ResourceKind kind, Transform parent)
            => Create(Find(Catalog != null ? Catalog.Resources : null, kind.ToString()), parent, 0, "Amber resource ", Vector3.one);

        private static ArtKitEntry Find(ArtKitEntry[] entries, string id)
        {
            if (entries != null) foreach (var entry in entries) if (entry != null && entry.Id == id) return entry;
            return null;
        }

        private static Transform Create(ArtKitEntry entry, Transform parent, int owner, string prefix, Vector3 scale)
        {
            if (entry == null || entry.Lod0 == null || entry.Lod1 == null || Catalog.Material == null) return null;
            var root = new GameObject(prefix + entry.Id).transform; root.SetParent(parent, false); root.localScale = scale;
            var near = Renderer(root, "LOD0", entry.Lod0, owner);
            var far = Renderer(root, "LOD1", entry.Lod1, owner);
            var group = root.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(.08f, new Renderer[] { near }), new LOD(.012f, new Renderer[] { far }) });
            group.fadeMode = LODFadeMode.None; group.RecalculateBounds();
            return root;
        }

        private static MeshRenderer Renderer(Transform parent, string name, Mesh mesh, int owner)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = Catalog.Material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            Tint.Clear(); Tint.SetColor(TeamColor, OwnerColor(owner)); renderer.SetPropertyBlock(Tint);
            return renderer;
        }

        public static Color OwnerColor(int owner)
        {
            switch (owner)
            {
                case 1: return new Color(.19f, .58f, .83f, 1);
                case 2: return new Color(.86f, .31f, .24f, 1);
                case 3: return new Color(.36f, .70f, .38f, 1);
                case 4: return new Color(.94f, .76f, .25f, 1);
                default: return Color.white;
            }
        }
    }
}
