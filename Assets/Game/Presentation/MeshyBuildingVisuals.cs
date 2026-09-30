using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Presentation only: draws a building with its Meshy model when the catalogue has one for the building and its
    /// owner's faction. Fitted to the building's own footprint at load time, so the result holds whatever scale the
    /// source used, and recoloured for its owner like the units. -emberfieldProceduralUnits turns it off as well.
    ///
    /// Walls and gates are built at the game's exact size, one set per culture, and fill their footprint with no margin
    /// and one scale per axis. Segments stand side by side along X, so each takes its whole width and meets the next,
    /// and the full height keeps the wall walk at the simulation's 3 m, where ladders reach and boarders stand. A gate
    /// also carries its culture's leaf as the "Portcullis" WorldView lifts to open it.
    /// </summary>
    public static class MeshyBuildingVisuals
    {
        /// <summary>The catalogue's building name for a gate's leaf: a part of the gate, never a building of its own.</summary>
        public const string GateLeaf = "gate_leaf";

        /// <summary>
        /// Where a culture building's prefab leaves its near level for the far one (tools/art/meshy_lod.py, 40% of the
        /// triangles). Unity multiplies the share of the screen by the LOD bias, so a desktop (bias 2) keeps the near
        /// level at every zoom: the smallest house, a 1.9 m shelter, still fills 0.087 of the screen at the widest. The
        /// low tier (0.7) switches that shelter from the play zoom out and a 4.6 m keep near the widest zoom; mid (1)
        /// switches the shelter past 12 m of zoom and high (1.5) past 18 m. Walls, gates and leaves have one level.
        /// </summary>
        public const float FarLevelHeight = .08f;

        // farTriangles: the far level's, 0 where the model has one level.
        [Serializable] public sealed class Entry { public string id, style, building, prefab; public string[] factions; public int triangles, farTriangles; public float height; }
        [Serializable] public sealed class Document { public Entry[] entries; }

        private static Entry[] entries;
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        public static Entry[] Entries
        {
            get
            {
                if (entries == null)
                {
                    var json = Resources.Load<TextAsset>("MeshyBuildings/catalog");
                    entries = json ? JsonUtility.FromJson<Document>(json.text)?.entries ?? Array.Empty<Entry>() : Array.Empty<Entry>();
                }
                return entries;
            }
        }

        public static Entry Resolve(string building, FactionKind faction)
        {
            string id = MeshyUnitVisuals.FactionId(faction);
            if (id == null) return null;
            foreach (var entry in Entries)
                if (entry.building == building && entry.factions != null && Array.IndexOf(entry.factions, id) >= 0) return entry;
            return null;
        }

        /// <summary>
        /// The definitions with IsWall or IsGate in greybox.json. The view is handed a building's state, which does not
        /// carry its definition's flags, so they are named by id here, as WorldView and AlphaWorldArt name the gate.
        /// </summary>
        public static bool IsFortification(string definitionId) => definitionId == "wall" || definitionId == "gate";

        public static Transform TryCreate(BuildingState building, FactionKind faction, Transform parent, float width, float depth)
            => building == null ? null : TryCreate(building.DefinitionId, building.OwnerId, faction, parent, width, depth);

        /// <summary>The same by definition and owner, for PresentationPrewarm, which has no building state to hand.</summary>
        internal static Transform TryCreate(string definitionId, int ownerId, FactionKind faction, Transform parent, float width, float depth)
        {
            // Unowned structures have no faction, so no culture to be drawn in; they keep the shared art.
            if (!MeshyUnitVisuals.Enabled || ownerId <= 0) return null;
            var entry = Resolve(definitionId, faction);
            var prefab = Prefab(entry);
            if (!prefab) return null;
            // A container keeps WorldView's half turn centred on the footprint whatever the model's own pivot.
            var root = new GameObject("Meshy " + definitionId).transform;
            root.SetParent(parent, false);
            var model = Place(prefab, root, ownerId, out var renderers);
            if (model == null) { UnityEngine.Object.Destroy(root.gameObject); return null; }
            var size = Vector3.Max(Measure(renderers).size, Vector3.one * .01f);
            float height = entry.height > 0 ? entry.height : AlphaWorldArt.BuildingHeight(definitionId);
            Vector3 fit;
            if (IsFortification(definitionId))
            {
                // No margin along the wall, so neighbouring segments touch. The depth scales with the length: the model
                // keeps its own 0.9 m, and its faces stay where the ladders were measured against them.
                float along = width / size.x;
                fit = new Vector3(along, height / size.y, Mathf.Min(along, depth / size.z));
            }
            else fit = Vector3.one * Mathf.Min(width * .96f / size.x, depth * .96f / size.z, height / size.y);
            model.localScale = Vector3.Scale(model.localScale, fit);
            Ground(model, root, renderers);
            if (definitionId == "gate") AddLeaf(root, faction, ownerId, fit);
            return root;
        }

        /// <summary>
        /// The owner culture's leaf, under a "Portcullis" at the centre of the passage with its pivot on the ground, so
        /// WorldView opens this gate as it does the procedural one: it sets the Portcullis at 2.45 m and squashes it
        /// into the lintel over the 2.4 m passage. Built at its size in metres like the gate, the leaf takes the gate's
        /// own fit and so still fills the passage. The half turn and the construction's rise act on both alike.
        /// </summary>
        private static void AddLeaf(Transform root, FactionKind faction, int owner, Vector3 fit)
        {
            var prefab = Prefab(Resolve(GateLeaf, faction));
            if (!prefab) return;
            var model = Place(prefab, root, owner, out var renderers);
            if (model == null) return;
            var leaf = new GameObject("Portcullis").transform;
            leaf.SetParent(root, false);
            model.SetParent(leaf, false);
            model.localScale = Vector3.Scale(model.localScale, fit);
            Ground(model, leaf, renderers);
        }

        private static GameObject Prefab(Entry entry)
        {
            if (entry == null) return null;
            if (!prefabs.TryGetValue(entry.id, out var prefab) || !prefab)
            {
                prefab = Resources.Load<GameObject>(entry.prefab);
                if (!prefab) return null;
                prefabs[entry.id] = prefab;
            }
            return prefab;
        }

        // The model under a holder of its own, recoloured for its owner. The fit goes on the holder, so it scales along
        // the building's axes: the imported model itself is turned upright from Blender's Z-up.
        private static Transform Place(GameObject prefab, Transform parent, int owner, out Renderer[] renderers)
        {
            renderers = null;
            if (prefab.GetComponentInChildren<Renderer>() == null) return null;
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(prefab, holder, false);
            renderers = holder.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = MeshyUnitVisuals.Owned(slots[i], owner);
                renderer.sharedMaterials = slots;
            }
            // A far level decimated from the near one is never quite its size, so the fit and the grounding go by the near one.
            if (model.TryGetComponent<LODGroup>(out var group) && group.lodCount > 1) renderers = group.GetLODs()[0].renderers;
            return holder;
        }

        // Centred on the parent's origin in plan, and standing on it.
        private static void Ground(Transform model, Transform parent, Renderer[] renderers)
        {
            var bounds = Measure(renderers);
            model.position += new Vector3(parent.position.x - bounds.center.x, parent.position.y - bounds.min.y, parent.position.z - bounds.center.z);
        }

        private static Bounds Measure(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
