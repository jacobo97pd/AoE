using System.Collections.Generic;
using Emberfield.Quality;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// On a phone tier, distant Meshy units draw a lighter level of their own mesh. The unit baker has the importer
    /// generate Unity Mesh LOD levels (about half the triangles each), but Unity's own selection never leaves the first
    /// level under the battlefield's orthographic camera, and a LOD group would need a second skinned renderer in every
    /// unit prefab. So the level is forced here from the zoom: the whole skinned mesh is still skinned and animated as
    /// before, only fewer of its triangles are drawn, shadow included. A desktop never forces a level.
    /// </summary>
    public static class UnitMeshDetail
    {
        private static readonly List<(SkinnedMeshRenderer Renderer, float Height)> units = new List<(SkinnedMeshRenderer, float)>();
        private static float zoom = -1;

        /// <summary>The number of unit renderers whose level follows the zoom.</summary>
        public static int Count { get { Prune(); return units.Count; } }

        /// <summary>Lets a Meshy unit's skinned meshes follow the zoom; <paramref name="height"/> is its standing height in metres.</summary>
        public static void Register(Transform model, float height)
        {
            if (MobileQuality.Active == null || model == null) return;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null || renderer.sharedMesh.lodCount < 2) continue;
                units.Add((renderer, height));
                if (zoom > 0) renderer.forceMeshLod = (short)Level(renderer, height, zoom);
            }
        }

        /// <summary>Called with every presentation sync; does nothing until the zoom changes.</summary>
        public static void Refresh(Camera camera)
        {
            if (camera == null || !camera.orthographic || MobileQuality.Active == null || Mathf.Approximately(camera.orthographicSize, zoom)) return;
            zoom = camera.orthographicSize;
            Prune();
            foreach (var (renderer, height) in units) renderer.forceMeshLod = (short)Level(renderer, height, zoom);
        }

        private static int Level(SkinnedMeshRenderer renderer, float height, float orthographicSize) =>
            MobileTiers.UnitMeshLevel(height / (2 * orthographicSize), MobileQuality.Active?.UnitDetailHeight ?? 0, renderer.sharedMesh.lodCount);

        private static void Prune() => units.RemoveAll(unit => unit.Renderer == null);
    }
}
