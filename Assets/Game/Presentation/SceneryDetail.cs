using System.Collections.Generic;
using Emberfield.Quality;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// On a phone tier, the trees, palms and rocks on blocked cells and the gatherable nodes draw a lighter level once
    /// the camera zooms out past their switch. The battlefield camera is orthographic, so a model's share of the screen
    /// depends on the zoom alone and every copy of one model switches at the same zoom. Each filler therefore keeps its
    /// one renderer, and when the zoom crosses a model's switch every copy of that model is handed the model's other
    /// shared mesh at once. Nothing is evaluated per frame (a LOD group on each of Amber Crossing's 2,500 fillers cost a
    /// desktop about 0.4 ms a frame), both meshes keep the one instanced material, so the copies still batch, and a
    /// renderer keeps its property block across the swap, so a tree being felled sways at either level. A model that has
    /// gone light comes back only once the zoom is back inside its switch by <see cref="MobileTiers.SceneryHysteresis"/>,
    /// so a pinch that rests on a switch never flickers. Nothing is looked at until the zoom changes, and nothing is
    /// allocated then. A desktop registers nothing: its fillers keep their one mesh and its nodes their prefab's group.
    /// </summary>
    public static class SceneryDetail
    {
        private sealed class Model
        {
            internal Mesh Near, Far;
            // The size the switch is measured at: a filler model's longest side (every copy switches at one zoom whatever
            // its own scale), and for a node the 3 m tree's, so that it switches with the trees around it.
            internal float Size;
            internal bool Lighter;
            internal readonly List<MeshFilter> Filters = new List<MeshFilter>();
        }

        private static readonly List<Model> models = new List<Model>();
        private static float zoom = -1;
        private static bool held;

        /// <summary>How many renderers follow the zoom.</summary>
        public static int Count
        {
            get { int count = 0; foreach (var model in models) count += model.Filters.Count; return count; }
        }

        /// <summary>Forgets every registration. A new battlefield view starts with none.</summary>
        public static void Reset() { models.Clear(); zoom = -1; held = false; }

        /// <summary>
        /// A filler on a blocked cell, on a phone tier: its one renderer draws <paramref name="near"/> or
        /// <paramref name="far"/>, both shared by every copy of the model, as the zoom crosses the model's switch.
        /// </summary>
        public static void RegisterFiller(MeshFilter filter, Mesh near, Mesh far)
        {
            if (MobileQuality.Active == null || filter == null || near == null || far == null) return;
            var size = near.bounds.size;
            Register(filter, near, far, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }

        /// <summary>
        /// A gatherable node as instantiated from its prefab, on a phone tier: the prefab's LOD group and far renderer give
        /// way to the near renderer alone, whose mesh follows the zoom at which the tier's trees switch. A node whose
        /// prefab is not a plain two-level group of sibling renderers on one material keeps its group.
        /// </summary>
        public static void RegisterNode(Transform model)
        {
            if (MobileQuality.Active == null || model == null || !model.TryGetComponent<LODGroup>(out var group)) return;
            var levels = group.GetLODs();
            if (levels.Length != 2 || levels[0].renderers.Length != 1 || levels[1].renderers.Length != 1) return;
            var nearRenderer = levels[0].renderers[0]; var farRenderer = levels[1].renderers[0];
            if (nearRenderer == null || farRenderer == null || nearRenderer.sharedMaterial != farRenderer.sharedMaterial) return;
            if (!nearRenderer.TryGetComponent<MeshFilter>(out var near) || !farRenderer.TryGetComponent<MeshFilter>(out var far) || near.sharedMesh == null || far.sharedMesh == null) return;
            Transform a = nearRenderer.transform, b = farRenderer.transform;
            if (b == model || b.childCount > 0 || a.parent != b.parent || a.localPosition != b.localPosition || a.localRotation != b.localRotation || a.localScale != b.localScale) return;
            var farMesh = far.sharedMesh;
            Object.Destroy(group);
            // Out of the node at once, so nothing that walks the node's renderers this frame (the felling sway) finds it.
            b.gameObject.SetActive(false); b.SetParent(null, false);
            Object.Destroy(b.gameObject);
            Register(near, near.sharedMesh, farMesh, MobileTiers.SceneryTreeHeight);
        }

        private static void Register(MeshFilter filter, Mesh near, Mesh far, float size)
        {
            Model model = null;
            foreach (var candidate in models)
                if (ReferenceEquals(candidate.Near, near) && ReferenceEquals(candidate.Far, far)) { model = candidate; break; }
            if (model == null)
            {
                model = new Model { Near = near, Far = far, Size = size };
                if (!held && zoom > 0) model.Lighter = MobileTiers.DrawsLighterLevel(false, zoom, SwitchZoom(model));
                models.Add(model);
            }
            model.Filters.Add(filter);
            filter.sharedMesh = model.Lighter ? far : near;
        }

        private static float SwitchZoom(Model model) => MobileTiers.SceneryDetailZoom(MobileQuality.SceneryDetailHeight, model.Size);

        /// <summary>Called with every presentation sync; returns at once unless the zoom has changed.</summary>
        public static void Refresh(Camera camera)
        {
            if (held || camera == null || !camera.orthographic) return;
            float size = camera.orthographicSize;
            if (size == zoom) return;
            zoom = size;
            foreach (var model in models) Show(model, MobileTiers.DrawsLighterLevel(model.Lighter, zoom, SwitchZoom(model)));
        }

        /// <summary>
        /// For stills: every registered model draws its lighter level, whatever the zoom, until <see cref="Release"/>.
        /// </summary>
        public static void HoldLighterLevel()
        {
            held = true;
            foreach (var model in models) Show(model, true);
        }

        /// <summary>Hands the levels back to the zoom at the next <see cref="Refresh"/>.</summary>
        public static void Release() { held = false; zoom = -1; }

        private static void Show(Model model, bool lighter)
        {
            if (model.Lighter == lighter) return;
            model.Lighter = lighter;
            var mesh = lighter ? model.Far : model.Near;
            var filters = model.Filters;
            for (int i = filters.Count - 1; i >= 0; i--)
            {
                // A filler destroyed with its view is dropped here, the only time the list is walked.
                if (filters[i] == null) { int last = filters.Count - 1; filters[i] = filters[last]; filters.RemoveAt(last); continue; }
                filters[i].sharedMesh = mesh;
            }
        }
    }
}
