using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Presentation only: draws a unit with its Meshy model when the catalogue has one for the unit and its owner's
    /// faction. Identity, costs, timings and damage stay in World; the model only watches.
    ///
    /// The model's painted cloth takes the owner's colour through its own material copy, one per model and owner,
    /// so two players of the same faction never field identical armies. Equipped cosmetics keep their own models.
    /// -emberfieldProceduralUnits turns the whole catalogue off, to compare against the art it replaces.
    ///
    /// A character skin is an entry with <c>cosmetic</c> set: the store sells it, and the faction's default model never
    /// resolves to it. It draws in the default's place only for the owner who wears it (CosmeticLoadout), and only for
    /// the unit and faction it was made for. Colour, rings, animation states and phone detail work as for any model.
    /// </summary>
    public static class MeshyUnitVisuals
    {
        public const string DisableFlag = "-emberfieldProceduralUnits";
        [Serializable] public sealed class Entry { public string id, name, culture, unit, prefab; public string[] factions; public int triangles; public bool cosmetic; }
        [Serializable] public sealed class Document { public Entry[] entries; }

        private static Entry[] entries;
        private static bool? disabled;
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<(Material, int), Material> owned = new Dictionary<(Material, int), Material>();
        private static readonly int TeamColor = Shader.PropertyToID("_TeamColor");

        public static Entry[] Entries
        {
            get
            {
                if (entries == null)
                {
                    var json = Resources.Load<TextAsset>("MeshyUnits/catalog");
                    entries = json ? JsonUtility.FromJson<Document>(json.text)?.entries ?? Array.Empty<Entry>() : Array.Empty<Entry>();
                }
                return entries;
            }
        }

        public static bool Enabled
        {
            get
            {
                if (disabled == null) disabled = Array.IndexOf(Environment.GetCommandLineArgs(), DisableFlag) >= 0;
                return !disabled.Value;
            }
        }

        public static string FactionId(FactionKind kind) => kind switch
        {
            FactionKind.AvenCompact => "aven", FactionKind.SerevinMarch => "serevin", FactionKind.EnglishKingdom => "english",
            FactionKind.MirajSultanate => "miraj", FactionKind.SkeldClans => "skeld", FactionKind.SolarKingdom => "solar",
            FactionKind.VerdantCovenant => "verdant", FactionKind.AshenDominion => "ashen", FactionKind.DrakeforgedClans => "drakeforged",
            FactionKind.PirateBrotherhood => "pirates", FactionKind.DesertSultanate => "sultanate", FactionKind.SahelConfederation => "sahel",
            _ => null,
        };

        /// <summary>The faction's default model for a unit. Character skins are never the default.</summary>
        public static Entry Resolve(string unit, FactionKind faction)
        {
            string id = FactionId(faction);
            if (id == null) return null;
            foreach (var entry in Entries)
                if (!entry.cosmetic && entry.unit == unit && entry.factions != null && Array.IndexOf(entry.factions, id) >= 0) return entry;
            return null;
        }

        /// <summary>A character skin by its model id, if it was made for this unit and faction.</summary>
        public static Entry ResolveSkin(string modelId, string unit, string factionId)
        {
            if (string.IsNullOrEmpty(modelId) || factionId == null) return null;
            foreach (var entry in Entries)
                if (entry.cosmetic && entry.id == modelId && entry.unit == unit && entry.factions != null && Array.IndexOf(entry.factions, factionId) >= 0) return entry;
            return null;
        }

        /// <summary>The skin <paramref name="ownerId"/> wears on this faction's unit, or null for the default model.</summary>
        public static Entry Skin(string unit, FactionKind faction, string realmId, int ownerId)
        {
            string id = FactionId(faction);
            var item = id == null ? null : CosmeticLoadout.ResolveCharacter(unit, id, realmId, ownerId);
            return item == null ? null : ResolveSkin(item.modelId, unit, id);
        }

        /// <summary>The model the owner's unit is drawn with: its skin where it wears one, else the faction's default.</summary>
        public static Entry ResolveFor(string unit, FactionKind faction, string realmId, int ownerId) => Skin(unit, faction, realmId, ownerId) ?? Resolve(unit, faction);

        public static CorsairAnimationDriver TryCreate(World world, UnitState unit, FactionKind faction, Transform parent) => TryCreate(world, unit, faction, parent, out _);

        /// <param name="skinId">The character skin that was drawn, or null for a default model.</param>
        public static CorsairAnimationDriver TryCreate(World world, UnitState unit, FactionKind faction, Transform parent, out string skinId)
        {
            skinId = null;
            if (!Enabled || world == null || unit == null) return null;
            var skin = Skin(unit.DefinitionId, faction, world.Map.RealmId, unit.OwnerId);
            // A colour style (CosmeticLoadout.Resolve) paints the procedural model; a character skin is a model of its own.
            if (skin == null && !CosmeticLoadout.Resolve(unit.DefinitionId, world.Map.RealmId, unit.OwnerId).IsDefault) return null;
            var driver = Create(skin ?? Resolve(unit.DefinitionId, faction), unit.OwnerId, parent);
            if (driver && skin != null) skinId = skin.id;
            // On a phone tier the unit draws a lighter level of its mesh when zoomed out.
            if (driver) UnitMeshDetail.Register(driver.transform, AlphaWorldArt.UnitHeight(unit.DefinitionId));
            return driver;
        }

        /// <summary>
        /// The model of <paramref name="entry"/> recoloured for <paramref name="owner"/>, or null. TryCreate's own path,
        /// also taken by PresentationPrewarm to read a model from disk while the match loads instead of when it is first
        /// fielded.
        /// </summary>
        internal static CorsairAnimationDriver Create(Entry entry, int owner, Transform parent)
        {
            if (entry == null) return null;
            if (!prefabs.TryGetValue(entry.id, out var prefab) || !prefab)
            {
                prefab = Resources.Load<GameObject>(entry.prefab);
                if (!prefab) return null;
                prefabs[entry.id] = prefab;
            }
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = entry.name;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = Owned(slots[i], owner);
                renderer.sharedMaterials = slots;
            }
            var driver = instance.GetComponent<CorsairAnimationDriver>();
            if (driver && driver.HasValidRig) return driver;
            instance.SetActive(false); UnityEngine.Object.Destroy(instance); return null;
        }

        /// <summary>The model's material recoloured for one owner, shared by every model of that owner.</summary>
        internal static Material Owned(Material shared, int owner)
        {
            if (shared == null || !shared.HasProperty(TeamColor)) return shared;
            if (!owned.TryGetValue((shared, owner), out var material) || !material)
            {
                material = new Material(shared) { name = shared.name + " (player " + owner + ")" };
                material.SetColor(TeamColor, AlphaWorldArt.OwnerColor(owner));
                owned[(shared, owner)] = material;
            }
            return material;
        }
    }
}
