using System;
using System.Collections;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Opt-in test of an externally modelled building. With -emberfieldImportedBarracks, the local player's Muster
    /// Hall is drawn with the Meshy barracks prefab instead of the procedural hall; every other building, including
    /// the rival's hall, stays procedural so the two can be judged side by side on the same battlefield.
    ///
    /// Without the flag nothing changes. The model is fitted to the building's own footprint and kept under the
    /// health bar at load time, so the result holds whatever scale or up-axis the source file happened to use.
    /// </summary>
    public static class ImportedBuildingVisuals
    {
        public const string Flag = "-emberfieldImportedBarracks";
        public const string ResourcePath = "ImportedBuildings/Barracks";
        private const float MaximumHeight = 3.4f;
        private static GameObject prefab;
        private static bool reported;

        public static bool Requested
            => (Debug.isDebugBuild || Application.isEditor) && Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

        public static Transform TryCreate(BuildingState building, Transform parent, float width, float depth)
        {
            if (building == null || building.DefinitionId != "muster_hall" || building.OwnerId != MatchController.LocalPlayer || !Requested) return null;
            if (prefab == null) prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                if (!reported) { reported = true; Debug.LogWarning("Imported barracks missing at Resources/" + ResourcePath + "; the procedural hall is kept."); }
                return null;
            }
            // A container keeps WorldView's 180-degree turn centred on the footprint whatever the model's own pivot.
            var root = new GameObject("Imported barracks").transform;
            root.SetParent(parent, false);
            var model = UnityEngine.Object.Instantiate(prefab, root, false).transform;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { UnityEngine.Object.Destroy(root.gameObject); return null; }
            var bounds = Measure(renderers);
            float fit = Mathf.Min(Mathf.Min(width, depth) * .96f / Mathf.Max(bounds.size.x, bounds.size.z), MaximumHeight / bounds.size.y);
            model.localScale *= fit;
            bounds = Measure(renderers);
            model.position += new Vector3(root.position.x - bounds.center.x, root.position.y - bounds.min.y, root.position.z - bounds.center.z);
            return root;
        }

        private static Bounds Measure(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>With a folder after the flag, frames both halls and writes one still of each.</summary>
        public static void TryStartCapture(MatchController match)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (!Requested || index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal)) return;
            Application.runInBackground = true;
            match.StartCoroutine(Capture(match, args[index + 1]));
        }

        private static IEnumerator Capture(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            BuildingState local = null, rival = null;
            foreach (var building in match.World.Buildings)
            {
                if (building.DefinitionId != "muster_hall") continue;
                if (building.OwnerId == MatchController.LocalPlayer) { if (local == null) local = building; }
                else if (rival == null) rival = building;
            }
            if (local == null) { Debug.LogError("EMBERFIELD_IMPORTED_BARRACKS no local Muster Hall; launch with -emberfieldFaction aven."); yield break; }
            match.ClearSelection();
            match.Rig.SetHome(DefinitionLoader.ToWorld(local.Position) + new Vector3(-2, 0, 2), 7);
            match.SyncPresentation(1);
            yield return new WaitForEndOfFrame();
            bool ok = PlayerSmoke.Capture(match, Path.Combine(folder, "barracks-imported.png"));
            if (rival != null)
            {
                match.Rig.Focus(DefinitionLoader.ToWorld(rival.Position));
                match.SyncPresentation(1);
                yield return new WaitForEndOfFrame();
                ok &= PlayerSmoke.Capture(match, Path.Combine(folder, "barracks-procedural.png"));
                match.Rig.Home();
            }
            Debug.Log("EMBERFIELD_IMPORTED_BARRACKS " + ok + " " + folder);
            if (Application.isBatchMode) Application.Quit(ok ? 0 : 1);
        }
    }
}
