using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>
    /// A debug fixture for judging the scenery's levels of detail: -emberfieldSceneryStills &lt;folder&gt; [map]. The
    /// shipped offline match on the map (amber_crossing unless one is named) with every unit seeing the whole map, so
    /// no fog hides the forest, and with no match loop. The camera centres on the thickest stand of blocked cells and
    /// takes a still at the widest zoom, the play zoom and the closest zoom, the HUD off. A last still at the closest
    /// zoom holds every LOD group, filler and node at its far level, so the far models can be judged where they are
    /// largest. On amber_crossing player 1 also gets the review's town (MeshyUnitReview), shot at the play zoom as drawn
    /// and held at its far level, units at their third Mesh LOD level. The widest shot is taken again without the grass
    /// tufts, to price them. Each still's render statistics go to stills.json, with the time the zoom's scenery swap
    /// took (SceneryDetail). With -emberfieldMobileTier the stills show what that phone tier draws. Then it quits.
    /// A map of another realm is shot with that realm's armies: -emberfieldSceneryStills &lt;folder&gt; legend_lands
    /// [faction] [opponent] (the elves against the orcs unless named), and a map with lands adds a tour of them after the
    /// widest, play and closest shots (see <see cref="LandShots"/>). Ordinary play never touches it.
    /// </summary>
    public static class SceneryStills
    {
        public const string Flag = "-emberfieldSceneryStills";
        public static bool IsRunning { get; private set; }

        [Serializable] private sealed class Shot { public string name; public float zoom; public long triangles, vertices, batches, drawCalls, setPassCalls, shadowCasters; public double sceneryMilliseconds; }
        [Serializable] private sealed class Report { public string map, tier; public int width, height; public float lodBias; public Vector3 focus; public List<Shot> shots = new List<Shot>(); }

        private static string Argument(int offset)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + offset >= args.Length || args[index + offset].StartsWith("-", StringComparison.Ordinal)) return null;
            return args[index + offset];
        }

        public static bool TryCreateWorld(out World world)
        {
            world = null;
            if ((!Debug.isDebugBuild && !Application.isEditor) || Argument(1) == null) return false;
            string mapId = Argument(2) ?? "amber_crossing";
            string faction = Argument(3) ?? (ContentRealms.IsMapAllowedInRealm(mapId, ContentRealms.Historical) ? "aven" : "verdant");
            var baseline = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, mapId);
            // Every starting worker is a tender whatever its faction, so naming the opponent only changes who it is.
            if (Argument(4) != null) foreach (var player in baseline.Map.PlayerFactions) if (player.PlayerId == 2) player.FactionId = Argument(4);
            if (baseline.Map.Id == "amber_crossing")
            {
                var buildings = new List<BuildingSpawnDefinition>(baseline.Map.BuildingSpawns);
                int id = 900;
                foreach (var (definition, x, z) in MeshyUnitReview.Town)
                    buildings.Add(new BuildingSpawnDefinition { Id = id++, OwnerId = 1, DefinitionId = definition, Position = new SimPoint(x, z) });
                baseline.Map.BuildingSpawns = buildings.ToArray();
            }
            // Every unit sees the default (widest) radius, which covers the whole map from the starting town.
            baseline.Map.OfflineMatch.VisionUnitCells = 128;
            foreach (var definition in baseline.Definition.Units) definition.VisionCells = 0;
            world = new World(baseline.Definition, baseline.Map);
            return true;
        }

        public static void TryStart(MatchController match)
        {
            string folder = Argument(1);
            if (folder == null || !(Debug.isDebugBuild || Application.isEditor)) return;
            IsRunning = true;
            Application.runInBackground = true;
            match.StartCoroutine(Capture(match, folder));
            // The match never runs its own loop: the stills are of the opening position, not of whatever the AI does next.
            match.enabled = false;
        }

        // The blocked cell with the most blocked cells within 8 m, away from the map's edge so the view stays on the map.
        private static Vector3 ThickestStand(World world)
        {
            var map = world.Map;
            var blocked = new HashSet<long>();
            foreach (var cell in map.BlockedCells)
                if (!AlphaEnvironment.IsWaterCell(map, cell.X, cell.Z)) blocked.Add((long)cell.Z << 32 | (uint)cell.X);
            float metres = map.CellSizeMillimetres * .001f;
            var best = new Vector3(map.WidthCells * metres / 2, 0, map.HeightCells * metres / 2); int most = -1;
            foreach (var cell in map.BlockedCells)
            {
                if (cell.X < 12 || cell.Z < 12 || cell.X >= map.WidthCells - 12 || cell.Z >= map.HeightCells - 12) continue;
                int count = 0;
                for (int dz = -8; dz <= 8; dz++) for (int dx = -8; dx <= 8; dx++)
                    if (dx * dx + dz * dz <= 64 && blocked.Contains((long)(cell.Z + dz) << 32 | (uint)(cell.X + dx))) count++;
                if (count > most) { most = count; best = new Vector3((cell.X + .5f) * metres, 0, (cell.Z + .5f) * metres); }
            }
            return best;
        }

        /// <summary>
        /// A tour of a map's lands: each starting land around its settlement and closer at its edge, its monument, the
        /// border where it meets the neutral ground, a ford in its water or lava, its peaks, then the neutral centre and
        /// peak, and the whole map. Named after the biome each shows, so a pairing's stills compare with another's.
        /// </summary>
        public static List<(string Name, Vector3 Focus, float Zoom)> LandShots(World world)
        {
            var shots = new List<(string, Vector3, float)>();
            var lands = world.Lands; var map = world.Map;
            if (!lands.HasLands) return shots;
            float metres = map.CellSizeMillimetres * .001f;
            var centre = new Vector3(map.WidthCells * metres / 2, 0, map.HeightCells * metres / 2);
            var water = new HashSet<long>();
            foreach (var cell in map.WaterCells ?? Array.Empty<GridCell>()) water.Add((long)cell.Z << 32 | (uint)cell.X);
            foreach (var cell in map.BlockedCells) water.Remove((long)cell.Z << 32 | (uint)cell.X);
            for (int zone = 1; zone <= MapLands.MaxZone; zone++)
            {
                int owner = lands.OwnerOf(zone);
                if (owner == 0) continue;
                string biome = lands.BiomeOf(zone), name = biome + "-" + zone;
                var hearth = centre;
                foreach (var building in world.Buildings) if (building.OwnerId == owner && building.DefinitionId == "hearth") hearth = DefinitionLoader.ToWorld(building.Position);
                var inward = (hearth - centre).normalized;
                shots.Add((name + "-play", hearth + inward * 4 + Vector3.Cross(Vector3.up, inward) * 6, 8));
                shots.Add((name + "-close", hearth + inward * 9 - Vector3.Cross(Vector3.up, inward) * 9, 5));
                // Walk from the settlement toward the centre until the ground stops being this land.
                var border = hearth;
                for (float step = 0; step < 90; step += .5f)
                {
                    var point = Vector3.MoveTowards(hearth, centre, step);
                    if (lands.ZoneAt(DefinitionLoader.ToSimulation(point)) != zone) { border = point; break; }
                }
                shots.Add((name + "-border", border, 9));
                var ford = Vector3.zero; int fords = 0;
                foreach (long key in water)
                {
                    int x = (int)(uint)key, z = (int)(key >> 32);
                    if (lands.ZoneAt(x, z) != zone) continue;
                    ford += new Vector3((x + .5f) * metres, 0, (z + .5f) * metres); fords++;
                }
                if (fords > 0) shots.Add((name + "-ford", ford / fords, 6));
                int peaks = 0;
                foreach (var landmark in map.Landmarks ?? Array.Empty<MapLandmarkDefinition>())
                {
                    if (lands.ZoneAt(landmark.Position) != zone) continue;
                    var at = DefinitionLoader.ToWorld(landmark.Position);
                    string kind = lands.LandmarkKind(landmark);
                    if (landmark.Kind == MapLands.MonumentSlot) shots.Add((name + "-" + kind, at, 6));
                    else if (landmark.Kind == MapLands.PeakSlot) shots.Add((name + "-" + kind + "-" + ++peaks, Vector3.MoveTowards(at, centre, 3), 10));
                }
            }
            shots.Add(("neutral-centre", centre, 11));
            foreach (var landmark in map.Landmarks ?? Array.Empty<MapLandmarkDefinition>())
                if (landmark.Kind == MapLands.PeakSlot && lands.ZoneAt(landmark.Position) == MapLands.Neutral)
                { shots.Add(("neutral-" + lands.LandmarkKind(landmark), Vector3.MoveTowards(DefinitionLoader.ToWorld(landmark.Position), centre, 3), 10)); break; }
            shots.Add(("lands-overview", centre, 62));
            return shots;
        }

        private static IEnumerator Capture(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            yield return null;
            match.Shell?.Close(); match.OfflineControls.Close();
            match.ClearSelection();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) canvas.enabled = false;
            // One tick lets every unit's sight uncover the map before the scenery reads what is explored.
            match.World.Tick();
            var camera = match.Rig.Camera;
            var target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
            camera.enabled = false;
            var request = new RenderPipeline.StandardRequest { destination = target };
            var recorders = new[] { "Triangles Count", "Vertices Count", "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Shadow Casters Count" };
            var counters = new ProfilerRecorder[recorders.Length];
            for (int i = 0; i < recorders.Length; i++) counters[i] = ProfilerRecorder.StartNew(ProfilerCategory.Render, recorders[i]);
            var report = new Report { map = match.World.Map.Id, tier = MobileQuality.Mode, width = Screen.width, height = Screen.height, lodBias = QualitySettings.lodBias, focus = ThickestStand(match.World) };
            bool ok = true;
            match.Rig.MinimumZoom = 5; match.Rig.MaximumZoom = 22;
            // The held shots come last: from them on every LOD group, filler and node draws its far level and every unit
            // its third level.
            bool town = match.World.Map.Id == "amber_crossing";
            var townFocus = new Vector3(25, 0, 24);
            var shots = new List<(string, Vector3, float, bool)> { ("far", report.focus, 22, false), ("far-without-grass", report.focus, 22, false),
                ("play", report.focus, 8, false), ("close", report.focus, 5, false) };
            if (town) shots.Add(("town", townFocus, 8, false));
            foreach (var shot in LandShots(match.World)) shots.Add((shot.Name, shot.Focus, shot.Zoom, false));
            shots.Add(("close-far-level", report.focus, 5, true));
            if (town) shots.Add(("town-far-level", townFocus, 8, true));
            var grass = new List<Renderer>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (renderer.name.StartsWith("Landscape ", StringComparison.Ordinal) && AlphaEnvironment.IsTuft(renderer.name.Substring(10))) grass.Add(renderer);
            foreach (var (name, focus, zoom, far) in shots)
            {
                // What the scattered tufts cost is the difference between the first two shots.
                foreach (var tuft in grass) tuft.enabled = name != "far-without-grass";
                if (far)
                {
                    foreach (var group in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        if (group.lodCount > 1) group.ForceLOD(1);
                    SceneryDetail.HoldLighterLevel();
                    foreach (var skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        if (skin.sharedMesh != null && skin.sharedMesh.lodCount > 1) skin.forceMeshLod = (short)Mathf.Min(2, skin.sharedMesh.lodCount - 1);
                }
                match.Rig.SetHome(focus, zoom);
                // The whole-map view is the one shot past the game's widest zoom, and a peak stands on the map's edge where
                // the rig would not centre it: both place the camera themselves. The whole map is also shot clear of the
                // haze a view so wide would reach.
                bool overview = name == "lands-overview", free = overview || name.Contains("-volcano-") || name.Contains("-mountain"), fog = RenderSettings.fog;
                if (free) { camera.orthographicSize = zoom; camera.transform.position = focus - camera.transform.forward * 55; }
                if (overview) RenderSettings.fog = false;
                // The swap on its own, timed: the sync that follows finds the zoom unchanged.
                var watch = System.Diagnostics.Stopwatch.StartNew();
                SceneryDetail.Refresh(camera);
                double scenery = watch.Elapsed.TotalMilliseconds;
                match.SyncPresentation(1);
                // The land's grade settles on the view as it would after a second of looking at it.
                foreach (var atmosphere in UnityEngine.Object.FindObjectsByType<LandAtmosphere>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) atmosphere.Settle();
                for (int frame = 0; frame < 3; frame++)
                {
                    // The match's own camera work would pull a placed camera back inside the map between frames.
                    if (free) { camera.orthographicSize = zoom; camera.transform.position = focus - camera.transform.forward * 55; }
                    RenderPipeline.SubmitRenderRequest(camera, request); yield return null;
                }
                report.shots.Add(new Shot { name = name, zoom = zoom, triangles = counters[0].LastValue, vertices = counters[1].LastValue, batches = counters[2].LastValue,
                    drawCalls = counters[3].LastValue, setPassCalls = counters[4].LastValue, shadowCasters = counters[5].LastValue, sceneryMilliseconds = scenery });
                ok &= PlayerSmoke.CaptureTarget(target, Path.Combine(folder, "scenery-" + name + ".png"));
                RenderSettings.fog = fog;
                if (free) match.Rig.SetHome(focus, 22);
            }
            foreach (var counter in counters) counter.Dispose();
            target.Release(); UnityEngine.Object.Destroy(target);
            File.WriteAllText(Path.Combine(folder, "stills.json"), JsonUtility.ToJson(report, true));
            Debug.Log("EMBERFIELD_SCENERY_STILLS " + ok + " " + folder);
            IsRunning = false;
            if (!Application.isEditor) Application.Quit(ok ? 0 : 1);
        }
    }
}
