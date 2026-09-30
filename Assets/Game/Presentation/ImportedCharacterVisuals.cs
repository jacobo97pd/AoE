using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>Pirate presentation assets. Registering an asset does not add or enable a gameplay unit.</summary>
    public static class ImportedCharacterVisuals
    {
        public const string PrefabResourcePath = "ImportedUnits/CrimsonCorsair";
        public const string GameplayDefinitionId = "crimson_corsair";
        public const string BoardingRaiderId = "boarding_raider";
        public const string GunpowderCorsairId = "gunpowder_corsair";
        public const string TreasureSeekerId = "treasure_seeker";

        /// <summary>Shared model identity for review/UI; combat roles and recruitment belong to simulation definitions.</summary>
        public sealed class Descriptor
        {
            public string DefinitionId { get; }
            public string PrefabResourcePath { get; }
            public string DisplayName { get; }
            internal Descriptor(string definitionId, string prefabResourcePath, string displayName)
            { DefinitionId = definitionId; PrefabResourcePath = prefabResourcePath; DisplayName = displayName; }
        }

        public static IReadOnlyList<Descriptor> Descriptors { get; } = Array.AsReadOnly(new[]
        {
            new Descriptor(GameplayDefinitionId, PrefabResourcePath, "Corsario Carmesí"),
            new Descriptor(BoardingRaiderId, "ImportedUnits/BoardingRaider", "Saqueador de abordaje"),
            new Descriptor(GunpowderCorsairId, "ImportedUnits/GunpowderCorsair", "Corsario de pólvora"),
            new Descriptor(TreasureSeekerId, "ImportedUnits/TreasureSeeker", "Buscadora de tesoros")
        });

        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private static readonly HashSet<string> reportedMissing = new HashSet<string>(StringComparer.Ordinal);

        public static bool TryGetDescriptor(string definitionId, out Descriptor descriptor)
        {
            foreach (var item in Descriptors)
                if (item.DefinitionId == definitionId) { descriptor = item; return true; }
            descriptor = null;
            return false;
        }

        public static bool CanUse(World world, UnitState unit) => world != null && unit != null &&
            world.Map.RealmId == ContentRealms.Naval && world.TryGetPlayer(unit.OwnerId, out var owner) &&
            owner.FactionId == "pirates" && TryGetDescriptor(unit.DefinitionId, out _);

        public static CorsairAnimationDriver TryCreate(World world, UnitState unit, Transform parent)
        {
            if (!CanUse(world, unit)) return null;
            TryGetDescriptor(unit.DefinitionId, out var descriptor);
            if (!prefabs.TryGetValue(descriptor.DefinitionId, out var prefab) || prefab == null)
            {
                prefab = Resources.Load<GameObject>(descriptor.PrefabResourcePath);
                if (prefab != null) prefabs[descriptor.DefinitionId] = prefab;
            }
            if (prefab == null)
            {
                if (reportedMissing.Add(descriptor.DefinitionId))
                    Debug.LogWarning(descriptor.DisplayName + " prefab is unavailable; using the fallback unit visual.");
                return null;
            }
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = descriptor.DisplayName;
            var driver = instance.GetComponent<CorsairAnimationDriver>();
            if (driver == null) driver = instance.AddComponent<CorsairAnimationDriver>();
            if (descriptor.DefinitionId == GunpowderCorsairId) driver.AttackBlendSeconds = 0;
            if (!driver.HasValidRig)
            {
                if (reportedMissing.Add(descriptor.DefinitionId))
                    Debug.LogWarning(descriptor.DisplayName + " has no usable animated rig; using the fallback unit visual.");
                instance.SetActive(false);
                UnityEngine.Object.Destroy(instance);
                return null;
            }
            return driver;
        }
    }
}
