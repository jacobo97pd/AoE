using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfield.Presentation
{
    [Serializable] public sealed class CosmeticCatalogItem
    {
        public string id, displayName, description, slot, targetId, realmId, styleId, currency;
        public int priceMinor;
    }
    [Serializable] public sealed class CosmeticEquippedItem { public string slot, targetId, itemId; }
    [Serializable] public sealed class CosmeticCatalogDocument { public CosmeticCatalogItem[] items; }
    public readonly struct CosmeticVisualStyle
    {
        public readonly string Id;
        public readonly Color Primary, Accent;
        public readonly float Emission;
        public bool IsDefault => string.IsNullOrEmpty(Id);
        public CosmeticVisualStyle(string id, Color primary, Color accent, float emission = 0)
        { Id = id; Primary = primary; Accent = accent; Emission = emission; }
    }

    // Presentation only. Local previews are not paid entitlements and are never used in an online match.
    public static class CosmeticLoadout
    {
        private const string PreviewKey = "Emberfield.Alpha03.CosmeticPreviews";
        private static CosmeticCatalogItem[] catalog;
        private static readonly Dictionary<string, string> previews = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> online = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> opponent = new Dictionary<string, string>(StringComparer.Ordinal);
        private static bool loaded, onlineSession;
        private static string loadedRoot;
        public static int Revision { get; private set; }
        public static bool IsOnlineSession { get => onlineSession; set { if (onlineSession != value) { onlineSession = value; Revision++; } } }
        public static IReadOnlyList<CosmeticCatalogItem> Catalog
        {
            get
            {
                if (catalog == null)
                {
                    var asset = Resources.Load<TextAsset>("Cosmetics/catalog");
                    catalog = asset == null ? Array.Empty<CosmeticCatalogItem>() : JsonUtility.FromJson<CosmeticCatalogDocument>(asset.text)?.items ?? Array.Empty<CosmeticCatalogItem>();
                }
                return catalog;
            }
        }
        public static CosmeticCatalogItem Find(string id)
        { foreach (var item in Catalog) if (item.id == id) return item; return null; }
        private static string Key(CosmeticCatalogItem item) => item.realmId + ":" + item.slot + ":" + item.targetId;
        private static void LoadPreviews()
        {
            string root = NativeSmokeStorage.Root;
            if (loaded && loadedRoot == root) return;
            loaded = true; loadedRoot = root; previews.Clear();
            foreach (string id in NativeSmokeStorage.GetPreference(PreviewKey).Split('|'))
            { var item = Find(id); if (item != null) previews[Key(item)] = id; }
        }
        public static bool IsPreviewEquipped(string id)
        { LoadPreviews(); var item = Find(id); return item != null && previews.TryGetValue(Key(item), out var selected) && selected == id; }
        public static bool EquipPreview(string id)
        {
            LoadPreviews(); var item = Find(id); if (item == null) return false;
            previews[Key(item)] = id; SavePreviews(); return true;
        }
        public static void ClearPreviews() { LoadPreviews(); previews.Clear(); SavePreviews(); }
        private static void SavePreviews()
        {
            var values = new List<string>(previews.Values); values.Sort(StringComparer.Ordinal);
            NativeSmokeStorage.SetPreference(PreviewKey, string.Join("|", values)); Revision++;
        }
        public static void SetOnlineEquipment(CosmeticEquippedItem[] equipped) => SetEquipment(online,equipped);
        public static void SetOpponentEquipment(CosmeticEquippedItem[] equipped) => SetEquipment(opponent,equipped);
        private static void SetEquipment(Dictionary<string,string> target, CosmeticEquippedItem[] equipped)
        {
            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in equipped ?? Array.Empty<CosmeticEquippedItem>())
            {
                var item = Find(entry?.itemId);
                if (item != null && item.slot == entry.slot && item.targetId == entry.targetId) next[Key(item)] = item.id;
            }
            bool changed = next.Count != target.Count;
            foreach (var entry in next) if (!target.TryGetValue(entry.Key, out string old) || old != entry.Value) changed = true;
            if (!changed) return;
            target.Clear(); foreach (var pair in next) target.Add(pair.Key, pair.Value); Revision++;
        }
        public static CosmeticVisualStyle Resolve(string definitionId, string realmId) => Resolve(definitionId,realmId,1);
        public static CosmeticVisualStyle Resolve(string definitionId, string realmId,int ownerId)
        {
            if (ownerId != 1 && (!IsOnlineSession || ownerId != 2)) return default;
            LoadPreviews(); var equipped = IsOnlineSession ? (ownerId == 1 ? online : opponent) : previews;
            CosmeticCatalogItem fallback = null; int priority = 0;
            foreach (string id in equipped.Values)
            {
                var item = Find(id);
                if (item == null || item.realmId != "shared" && item.realmId != realmId) continue;
                int score = item.targetId == definitionId ? 100 : item.targetId == "*" && item.slot == "architecture" && IsBuilding(definitionId) ? 10 : 0;
                if (score == 0) continue;
                if (item.realmId == realmId) score++;
                if (score > priority || score == priority && string.CompareOrdinal(item.id,fallback?.id) < 0) { fallback = item; priority = score; }
            }
            return fallback == null ? default : Style(fallback.styleId);
        }
        public static bool IsBuilding(string id) => id == "hearth" || id == "shelter" || id == "muster_hall" || id == "storeyard" || id == "archive" || id == "supply_outpost" || id == "wall" || id == "gate" || id == "watchtower" || id == "keep" || id == "beast_lodge" || id == "siege_workshop";
        public static CosmeticVisualStyle Style(string id)
        {
            switch (id)
            {
                case "bronze_tide": return new CosmeticVisualStyle(id, new Color(.19f,.61f,.62f), new Color(.91f,.65f,.32f));
                case "imperial_sand": return new CosmeticVisualStyle(id, new Color(.87f,.72f,.47f), new Color(.32f,.66f,.71f));
                case "sapphire_frost": return new CosmeticVisualStyle(id, new Color(.25f,.47f,.82f), new Color(.76f,.94f,1), .12f);
                case "ember_crown": return new CosmeticVisualStyle(id, new Color(.48f,.16f,.09f), new Color(1,.56f,.12f), .15f);
                case "storm_scale": return new CosmeticVisualStyle(id, new Color(.39f,.35f,.61f), new Color(.68f,.87f,1), .13f);
                case "void_scale": return new CosmeticVisualStyle(id, new Color(.21f,.14f,.32f), new Color(.84f,.47f,.95f), .1f);
                case "luminous_ward": return new CosmeticVisualStyle(id, new Color(.9f,.87f,.73f), new Color(1,.77f,.35f), .08f);
                case "verdant_bloom": return new CosmeticVisualStyle(id, new Color(.27f,.55f,.31f), new Color(.98f,.66f,.76f), .05f);
                default: return default;
            }
        }
    }
}
