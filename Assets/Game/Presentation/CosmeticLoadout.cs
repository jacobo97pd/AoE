using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emberfield.Presentation
{
    [Serializable] public sealed class CosmeticCatalogItem
    {
        public string id, displayName, description, slot, targetId, realmId, styleId, currency;
        // Character skins only: the faction whose targetId unit wears the skin, and the MeshyUnitVisuals model that
        // replaces the unit's default one. The other slots leave both empty.
        public string factionId, modelId;
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
        /// <summary>The slot that swaps one faction's unit model for a skin's own.</summary>
        public const string CharacterSlot = "character";
        /// <summary>
        /// What an equipped item occupies, as the server's wardrobe reports it: the target for most slots, and the faction
        /// with the unit for a character skin, so the dwarves' and the clans' reedguard skins are worn together.
        /// </summary>
        public static string EquipmentTarget(CosmeticCatalogItem item) => item.slot == CharacterSlot ? item.factionId + ":" + item.targetId : item.targetId;
        private static string Key(CosmeticCatalogItem item) => item.realmId + ":" + item.slot + ":" + EquipmentTarget(item);
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
        /// <summary>Wears an item as a local preview without saving it, for review fixtures: the player's own previews are untouched.</summary>
        public static bool EquipTransientPreview(string id)
        {
            LoadPreviews(); var item = Find(id); if (item == null) return false;
            previews[Key(item)] = id; Revision++; return true;
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
                if (item != null && item.slot == entry.slot && EquipmentTarget(item) == entry.targetId) next[Key(item)] = item.id;
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
                // A character skin swaps the model instead of painting it: ResolveCharacter answers for it.
                if (item == null || item.slot == CharacterSlot || item.realmId != "shared" && item.realmId != realmId) continue;
                int score = item.targetId == definitionId ? 100 : item.targetId == "*" && item.slot == "architecture" && IsBuilding(definitionId) ? 10 : 0;
                if (score == 0) continue;
                if (item.realmId == realmId) score++;
                if (score > priority || score == priority && string.CompareOrdinal(item.id,fallback?.id) < 0) { fallback = item; priority = score; }
            }
            return fallback == null ? default : Style(fallback.styleId);
        }
        /// <summary>
        /// The character skin this owner's units of <paramref name="definitionId"/> wear while fielded by
        /// <paramref name="factionId"/> in a <paramref name="realmId"/> match, or null. Equipment is read as Resolve reads it:
        /// local previews offline, the account's wardrobe for owner 1 and the opponent's for owner 2 online.
        /// </summary>
        public static CosmeticCatalogItem ResolveCharacter(string definitionId, string factionId, string realmId, int ownerId)
        {
            if (string.IsNullOrEmpty(factionId) || ownerId != 1 && (!IsOnlineSession || ownerId != 2)) return null;
            LoadPreviews(); var equipped = IsOnlineSession ? (ownerId == 1 ? online : opponent) : previews;
            CosmeticCatalogItem found = null;
            foreach (string id in equipped.Values)
            {
                var item = Find(id);
                if (item == null || item.slot != CharacterSlot || item.realmId != realmId || item.factionId != factionId || item.targetId != definitionId) continue;
                if (found == null || string.CompareOrdinal(item.id, found.id) < 0) found = item;
            }
            return found;
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
                // Character skins paint nothing: these name the accent beside them in the store.
                case "forge_copper": return new CosmeticVisualStyle(id, new Color(.30f,.30f,.34f), new Color(.82f,.42f,.17f));
                case "forest_leaf": return new CosmeticVisualStyle(id, new Color(.27f,.42f,.22f), new Color(.52f,.76f,.36f));
                case "wanderer_grey": return new CosmeticVisualStyle(id, new Color(.38f,.42f,.36f), new Color(.45f,.60f,.82f));
                default: return default;
            }
        }
    }
}
