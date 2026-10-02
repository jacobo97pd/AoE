using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>Original alpha geometry. One immutable LOD pair per public archetype/faction; instances share material and meshes.</summary>
    public static class AlphaWorldArt
    {
        private sealed class Pair { internal Mesh Near, Far; }
        private static readonly Dictionary<string, Pair> Meshes = new Dictionary<string, Pair>(StringComparer.Ordinal);
        private static readonly MaterialPropertyBlock Tint = new MaterialPropertyBlock();
        private static readonly int TeamColor = Shader.PropertyToID("_TeamColor");
        public static readonly string[] UnitIds = { "tender", "reedguard", "stringwarden", "strider", "threadkeeper", "ashrunner", "supply_cart", "sun_lion", "grove_guardian", "war_troll", "ember_drake", "dune_elephant", "frostguard", "siege_ram", "siege_ladder", "siege_tower", "camel_archer", "quilted_lancer" };
        public static readonly string[] BuildingIds = { "hearth", "shelter", "muster_hall", "storeyard", "archive", "supply_outpost", "wall", "gate", "watchtower", "keep", "beast_lodge", "siege_workshop" };
        public static int CachedMeshCount => Meshes.Count * 2;

        public static Transform Unit(string id, FactionKind faction, Transform parent, int owner)
        {
            if (Array.IndexOf(UnitIds, id) < 0) return null;
            var result = Create("unit/" + faction + "/" + id, parent, owner, Vector3.one,
                lod => AlphaWorldGeometry.Unit(id, faction, lod));
            if (result == null) return null;
            if (id == "ember_drake")
            {
                var left = Create("part/dragon-left-wing", result, owner, Vector3.one, lod => AlphaExpandedGeometry.DragonWing(true, lod));
                var right = Create("part/dragon-right-wing", result, owner, Vector3.one, lod => AlphaExpandedGeometry.DragonWing(false, lod));
                left.name = "Left wing"; right.name = "Right wing";
                left.localPosition = new Vector3(-.30f, 1.34f, -.15f); right.localPosition = new Vector3(.30f, 1.34f, -.15f);
            }
            ApplyOwnedCosmetic(result, id, faction, owner); return result;
        }
        public static Transform Building(string id, FactionKind faction, Transform parent, int owner, float width, float depth)
        {
            if (Array.IndexOf(BuildingIds, id) < 0) return null;
            var result = Create("building/" + faction + "/" + id, parent, owner, new Vector3(width, 1, depth),
                lod => AlphaWorldGeometry.Building(id, faction, lod));
            if (result == null) return null;
            if (id == "gate") { var gate = Create("part/gate-leaf", result, owner, Vector3.one, AlphaExpandedGeometry.GateLeaf); gate.name = "Portcullis"; }
            ApplyOwnedCosmetic(result, id, faction, owner); return result;
        }
        public static Transform Resource(ResourceKind kind, Transform parent, string biomeId = "forest")
        {
            if (kind != ResourceKind.Food && kind != ResourceKind.Wood && kind != ResourceKind.Stone && kind != ResourceKind.Metal) return null;
            return Create("resource/" + biomeId + "/" + kind, parent, 0, Vector3.one, lod => AlphaWorldGeometry.Resource(kind, lod, biomeId));
        }
        public static Transform Beacon(Transform parent) => Create("public/beacon", parent, 0, Vector3.one, AlphaWorldGeometry.Beacon);
        /// <summary>Characters are authored at life size, which left them nearly as tall as a shelter and taller than
        /// the trees: every unit visual is drawn at this fraction instead. Bars, rings, shot heights and the imported
        /// and reference characters all follow it, so one value keeps the whole world in proportion.</summary>
        public const float UnitScale = .6f;
        public static float UnitHeight(string id) => AuthoredUnitHeight(id) * UnitScale;
        public static float UnitSelectionRadius(string id) => AuthoredUnitSelectionRadius(id) * UnitScale;
        // Ships (WorldView.NewShipVisual) are fitted to their own length; these are their hull and sail, and a ring round the hull.
        public static bool IsShip(string id) => id == "war_galley" || id == "pirate_sloop" || id == "english_frigate" || id == "spanish_galleon";
        /// <summary>
        /// The culture a faction's soldiers and town are drawn in. Each navy fields its kingdom's army and builds its
        /// kingdom's town, in its owner's colours: the English navy the English kingdom's, the Spanish navy the Hispanos'.
        /// Every other faction is drawn as itself.
        /// </summary>
        public static FactionKind Culture(FactionKind faction) =>
            faction == FactionKind.EnglishNavy ? FactionKind.EnglishKingdom : faction == FactionKind.SpanishNavy ? FactionKind.SerevinMarch : faction;
        private static float AuthoredUnitHeight(string id) => IsShip(id) ? 4.0f : id == "crimson_corsair" ? 2.8f : id == "boarding_raider" || id == "gunpowder_corsair" || id == "treasure_seeker" ? 2.35f : id == "siege_tower" ? 4.9f : id == "dune_elephant" || id == "grove_guardian" || id == "siege_ladder" ? 3.8f : id == "frostguard" || id == "war_troll" ? 3.4f : id == "ember_drake" ? 3.0f : id == "camel_archer" ? 3.4f : id == "quilted_lancer" ? 3.3f : id == "strider" || id == "ashrunner" ? 3.1f : id == "reedguard" || id == "threadkeeper" ? 2.65f : id == "supply_cart" ? 1.65f : 2.2f;
        private static float AuthoredUnitSelectionRadius(string id) => IsShip(id) ? 2.6f : id == "dune_elephant" || id == "siege_tower" ? 1.15f : id == "ember_drake" || id == "siege_ram" ? 1.0f : id == "sun_lion" || id == "grove_guardian" || id == "war_troll" || id == "frostguard" || Mounted(id) ? .90f : .70f;
        /// <summary>The riders: the procedural figure sits them on a horse, and their selection ring is as wide as a creature's.</summary>
        public static bool Mounted(string id) => id == "strider" || id == "ashrunner" || id == "camel_archer" || id == "quilted_lancer";
        public static float BuildingHeight(string id) => id == "dock" ? 1.4f : id == "keep" ? 4.7f : id == "watchtower" ? 4.0f : id == "wall" || id == "gate" ? 3.7f : id == "hearth" ? 4.3f : id == "archive" ? 3.85f : id == "muster_hall" ? 3.7f : 3.3f;
        public static Color OwnerColor(int owner) => owner == 1 ? new Color(.12f, .56f, .56f) : owner == 2 ? new Color(.83f, .25f, .17f) : ArtKit.OwnerColor(owner);

        public static void ApplyCosmetic(Transform root, CosmeticVisualStyle style)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                Tint.Clear(); renderer.GetPropertyBlock(Tint);
                Tint.SetColor("_CosmeticPrimary", style.Primary); Tint.SetColor("_CosmeticAccent", style.Accent);
                // An equipped palette defines the surface hue; retaining most of the original
                // red albedo made blue skins visibly brown. Team cloth is excluded in the shader.
                Tint.SetFloat("_CosmeticBlend", style.IsDefault ? 0 : 1); Tint.SetFloat("_CosmeticEmission", style.IsDefault ? 0 : style.Emission);
                renderer.SetPropertyBlock(Tint);
            }
        }
        public static void ApplyOwnedCosmetic(Transform root, string definitionId, FactionKind faction, int owner)
        {
            if (owner != 1 && owner != 2) return;
            string factionId = MeshyUnitVisuals.FactionId(faction);
            if (factionId == null) return;
            ApplyCosmetic(root, CosmeticLoadout.Resolve(definitionId, ContentRealms.RealmForFaction(factionId), owner));
        }

        private static Transform Create(string key, Transform parent, int owner, Vector3 scale, Func<int, Mesh> factory)
        {
            if (ArtKit.Catalog == null || ArtKit.Catalog.Material == null) return null;
            if (!Meshes.TryGetValue(key, out var pair))
            {
                pair = new Pair { Near = factory(0), Far = factory(1) }; Meshes.Add(key, pair);
            }
            var root = new GameObject("Alpha " + key).transform; root.SetParent(parent, false); root.localScale = scale;
            var near = Render(root, pair.Near, owner, "LOD0"); var far = Render(root, pair.Far, owner, "LOD1");
            var lods = root.gameObject.AddComponent<LODGroup>();
            lods.SetLODs(new[] { new LOD(.06f, new Renderer[] { near }), new LOD(.008f, new Renderer[] { far }) });
            lods.fadeMode = LODFadeMode.None; lods.RecalculateBounds();
            return root;
        }
        private static MeshRenderer Render(Transform parent, Mesh mesh, int owner, string name)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = ArtKit.Catalog.Material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            Tint.Clear(); Tint.SetColor(TeamColor, OwnerColor(owner)); renderer.SetPropertyBlock(Tint);
            return renderer;
        }
    }
}
