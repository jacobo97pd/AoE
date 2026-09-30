using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Presentation only: the Meshy scenery, from Resources/MeshyProps/catalog.json (tools/art/meshy_props.json).
    /// Obstacle fillers hand back shared meshes (a near and a far level, which a phone tier swaps with the zoom through
    /// SceneryDetail) and a material so thousands of copies GPU-instance; resource nodes, landmarks and siege engines are
    /// instantiated prefabs. The finish baked every model's size into its vertices in metres (authored units for siege),
    /// so the game's own per-instance scale and the felling sway, which reads object-space height, keep their meaning.
    /// -emberfieldProceduralUnits turns the scenery off with the units.
    /// </summary>
    public static class MeshyPropVisuals
    {
        // factions: the siege models a culture draws for itself (the mountain, dwarf, elf and orc ladders); empty on
        // scenery and on engines every faction shares.
        // farTriangles: the far level's, 0 where the model has one level.
        [Serializable] public sealed class Entry { public string id, biome, role, kind, prefab; public int triangles, farTriangles; public float height; public string[] factions; }
        [Serializable] public sealed class Document { public Entry[] entries; }

        private static Entry[] entries;
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, List<(Mesh Mesh, Mesh Far, Material Material)>> obstacles = new Dictionary<string, List<(Mesh, Mesh, Material)>>();

        public static Entry[] Entries
        {
            get
            {
                if (entries == null)
                {
                    var json = Resources.Load<TextAsset>("MeshyProps/catalog");
                    entries = json ? JsonUtility.FromJson<Document>(json.text)?.entries ?? Array.Empty<Entry>() : Array.Empty<Entry>();
                }
                return entries;
            }
        }

        // Maps with no biome (the sandboxes) are dressed as forest, which is what the procedural art does too.
        private static string Biome(string biome) => string.IsNullOrEmpty(biome) ? "forest" : biome;

        private static GameObject Prefab(Entry entry)
        {
            if (!prefabs.TryGetValue(entry.id, out var prefab) || !prefab)
            {
                prefab = Resources.Load<GameObject>(entry.prefab);
                if (prefab) prefabs[entry.id] = prefab;
            }
            return prefab;
        }

        private static Entry Find(string role, string kind, string biome)
        {
            if (!MeshyUnitVisuals.Enabled) return null;
            foreach (var entry in Entries)
                if (entry.role == role && entry.kind == kind && (entry.biome == biome || entry.biome == "all")) return entry;
            return null;
        }

        /// <summary>One of the biome's Meshy variants for a blocked-cell filler of this procedural kind, picked by seed.</summary>
        public static bool TryObstacle(string kind, string biome, uint seed, out Mesh mesh, out Material material) => TryObstacle(kind, biome, seed, out mesh, out _, out material);

        /// <summary>The same variant with its far level (the prefab's "LOD1" child), or a null far mesh where it has none.</summary>
        public static bool TryObstacle(string kind, string biome, uint seed, out Mesh mesh, out Mesh far, out Material material)
        {
            mesh = null; far = null; material = null;
            if (!MeshyUnitVisuals.Enabled) return false;
            string key = Biome(biome) + "/" + kind;
            if (!obstacles.TryGetValue(key, out var pool))
            {
                pool = new List<(Mesh, Mesh, Material)>();
                foreach (var entry in Entries)
                {
                    if (entry.role != "obstacle" || entry.kind != kind || entry.biome != Biome(biome)) continue;
                    var prefab = Prefab(entry);
                    if (prefab == null || !prefab.TryGetComponent<MeshFilter>(out var filter) || !prefab.TryGetComponent<MeshRenderer>(out var renderer) || !filter.sharedMesh) continue;
                    var farLevel = prefab.transform.Find("LOD1");
                    pool.Add((filter.sharedMesh, farLevel != null && farLevel.TryGetComponent<MeshFilter>(out var farFilter) ? farFilter.sharedMesh : null, renderer.sharedMaterial));
                }
                obstacles[key] = pool;
            }
            if (pool.Count == 0) return false;
            var pick = pool[(int)((seed >> 5) % (uint)pool.Count)];
            mesh = pick.Mesh; far = pick.Far; material = pick.Material;
            return true;
        }

        /// <summary>A gatherable node for this resource in this biome, turned by its id so neighbours differ.</summary>
        public static Transform TryResource(ResourceKind kind, string biome, Transform parent, int id)
        {
            var entry = Find("resource", kind.ToString(), Biome(biome));
            var prefab = entry != null ? Prefab(entry) : null;
            if (prefab == null) return null;
            var model = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            model.name = "Meshy " + entry.id;
            model.localRotation = Quaternion.Euler(0, (id * 97) % 360, 0);
            // A phone tier draws the node's far level from the zoom at which it does the trees around it, through the
            // node's one renderer; a desktop keeps the prefab's group.
            SceneryDetail.RegisterNode(model);
            return model;
        }

        /// <summary>A map landmark of this kind. The caller keeps positioning, rotating and scaling the returned root.</summary>
        public static Transform TryLandmark(string kind, string biome, Transform parent)
        {
            var entry = Find("landmark", kind, Biome(biome));
            var prefab = entry != null ? Prefab(entry) : null;
            // A decorative palm is a filler palm: the fuller, lighter one is the wood node, and a monument must not read as harvestable.
            if (prefab == null && kind == "palm" && TryObstacle("palm", biome, (uint)Mathf.Abs(parent.childCount * 7919), out var mesh, out var material))
            {
                var palm = new GameObject("Meshy landmark palm", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                palm.SetParent(parent, false);
                palm.GetComponent<MeshFilter>().sharedMesh = mesh;
                palm.GetComponent<MeshRenderer>().sharedMaterial = material;
                return palm;
            }
            if (prefab == null) return null;
            var model = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            model.name = "Meshy " + entry.id;
            return model;
        }

        /// <summary>
        /// The siege model this faction fields for this unit: its culture's own where the roster lists the faction on
        /// one, else a model drawn for everyone (no factions listed), else the kingdom's. A faction with no model of
        /// its own (the Miraj, the Solar kingdom) therefore keeps a Meshy engine instead of dropping to the procedural one.
        /// </summary>
        public static Entry ResolveSiege(string unitId, FactionKind faction)
        {
            if (!MeshyUnitVisuals.Enabled) return null;
            string id = MeshyUnitVisuals.FactionId(faction);
            Entry shared = null, kingdom = null;
            foreach (var entry in Entries)
            {
                if (entry.role != "siege" || entry.kind != unitId) continue;
                bool listed = entry.factions != null && entry.factions.Length > 0;
                if (listed && id != null && Array.IndexOf(entry.factions, id) >= 0) return entry;
                if (!listed) shared ??= entry;
                else if (Array.IndexOf(entry.factions, "aven") >= 0) kingdom ??= entry;
            }
            return shared ?? kingdom;
        }

        /// <summary>
        /// The hull this faction sails for this ship: its culture's own where the roster lists the faction on one (the
        /// English frigate, the Hispanos' galleon, the orcs' bone ship, the pirates' sloop), else the shared merchant ship
        /// the coast has always moored.
        /// </summary>
        public static Entry ResolveShip(string unitId, FactionKind faction)
        {
            if (!MeshyUnitVisuals.Enabled) return null;
            string id = MeshyUnitVisuals.FactionId(faction);
            Entry shared = null;
            foreach (var entry in Entries)
            {
                if (entry.role != "ship" || entry.kind != unitId) continue;
                bool listed = entry.factions != null && entry.factions.Length > 0;
                if (listed && id != null && Array.IndexOf(entry.factions, id) >= 0) return entry;
                if (!listed) shared ??= entry;
            }
            return shared ?? Find("landmark", "ship", "caribbean");
        }

        /// <summary>A ship in its faction's style, recoloured for its owner where the model carries a pennant to take it.</summary>
        public static Transform TryShip(string unitId, int owner, FactionKind faction, Transform parent)
        {
            var entry = ResolveShip(unitId, faction);
            var prefab = entry != null ? Prefab(entry) : null;
            if (prefab == null) return null;
            var model = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            model.name = "Meshy " + entry.id;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = MeshyUnitVisuals.Owned(slots[i], owner);
                renderer.sharedMaterials = slots;
            }
            return model;
        }

        /// <summary>The coast's harbour quay, standing in for a dock. The caller fits it to the footprint.</summary>
        public static Transform TryDock(Transform parent)
        {
            var entry = Find("landmark", "harbor", "caribbean");
            var prefab = entry != null ? Prefab(entry) : null;
            if (prefab == null) return null;
            var model = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            model.name = "Meshy " + entry.id;
            return model;
        }

        /// <summary>The kingdom's siege engine, for callers that do not know the owner's faction.</summary>
        public static Transform TrySiege(string unitId, int owner, Transform parent) => TrySiege(unitId, owner, FactionKind.AvenCompact, parent);

        /// <summary>A siege engine in its faction's style, recoloured for its owner like the units. Sized in authored units under the unit scale.</summary>
        public static Transform TrySiege(string unitId, int owner, FactionKind faction, Transform parent)
        {
            var entry = ResolveSiege(unitId, faction);
            var prefab = entry != null ? Prefab(entry) : null;
            if (prefab == null) return null;
            var model = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            model.name = "Meshy " + entry.id;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = MeshyUnitVisuals.Owned(slots[i], owner);
                renderer.sharedMaterials = slots;
            }
            return model;
        }
    }
}
