using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    [Serializable] public sealed class ReferenceCharacterEntry
    {
        public string id, name, family, role, suggestedFaction, realm, modelPath, sourceBlendPath, referenceImage, interpretationNotes;
        public string prefabResourcePath;
        public float heightMetres;
    }
    [Serializable] public sealed class ReferenceCharacterDocument { public ReferenceCharacterEntry[] entries; }

    /// <summary>Presentation assets only. Unit identity, costs, ownership and realm rules remain in World.</summary>
    public static class ReferenceCharacterVisuals
    {
        private static ReferenceCharacterEntry[] entries;
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        public static ReferenceCharacterEntry[] Entries
        {
            get
            {
                if (entries == null)
                {
                    var json = Resources.Load<TextAsset>("ReferenceCharacters/catalog");
                    entries = json ? JsonUtility.FromJson<ReferenceCharacterDocument>(json.text)?.entries ?? Array.Empty<ReferenceCharacterEntry>() : Array.Empty<ReferenceCharacterEntry>();
                }
                return entries;
            }
        }
        public static ReferenceCharacterEntry Find(string id) => Array.Find(Entries, e => e.id == id);
        public static void ReleasePrefabCache() => prefabs.Clear();
        public static GameObject Prefab(string id)
        {
            var entry = Find(id);
            if (entry == null) return null;
            if (!prefabs.TryGetValue(id, out var prefab) || !prefab)
            { prefab = Resources.Load<GameObject>(entry.prefabResourcePath); if (prefab) prefabs[id] = prefab; }
            return prefab;
        }
        public static string Resolve(string definition, FactionKind faction, string realm)
        {
            if (realm == ContentRealms.Historical)
            {
                // Shared kingdom art is provisional; it does not claim nationally specific uniforms.
                if (faction == FactionKind.AvenCompact || faction == FactionKind.SerevinMarch || faction == FactionKind.EnglishKingdom)
                    return definition == "tender" ? "figure_21" : definition == "reedguard" ? "figure_18" : null;
                if (faction == FactionKind.MirajSultanate)
                    return definition == "tender" ? "figure_22" : definition == "reedguard" ? "figure_19" : null;
            }
            if (realm == ContentRealms.Fantasy)
            {
                if (faction == FactionKind.SkeldClans)
                    return definition == "tender" ? "figure_23" : definition == "reedguard" ? "figure_20" : definition == "stringwarden" ? "figure_17" : definition == "frostguard" ? "figure_15" : null;
                if (definition == "ember_drake") return faction == FactionKind.SolarKingdom ? "figure_06" : faction == FactionKind.VerdantCovenant ? "figure_07" : "figure_08";
                if (faction == FactionKind.VerdantCovenant)
                    return definition == "reedguard" ? "figure_10" : definition == "stringwarden" ? "figure_09" : definition == "threadkeeper" ? "figure_11" : null;
                if (faction == FactionKind.DrakeforgedClans)
                    return definition == "tender" ? "figure_13" : definition == "reedguard" ? "figure_12" : definition == "frostguard" ? "figure_14" : null;
            }
            return null;
        }
        public static CorsairAnimationDriver TryCreate(World world, UnitState unit, FactionKind faction, Transform parent)
        {
            if (world == null || unit == null) return null;
            // Equipped appearances retain their authored models and material treatment.
            if (!CosmeticLoadout.Resolve(unit.DefinitionId, world.Map.RealmId, unit.OwnerId).IsDefault) return null;
            var id = Resolve(unit.DefinitionId, faction, world.Map.RealmId);
            var entry = Find(id);
            if (entry == null || entry.realm != world.Map.RealmId) return null;
            var prefab = Prefab(id);
            if (!prefab) return null;
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = entry.name;
            var driver = instance.GetComponent<CorsairAnimationDriver>();
            if (driver && driver.HasValidRig) return driver;
            instance.SetActive(false); UnityEngine.Object.Destroy(instance); return null;
        }
    }
}
