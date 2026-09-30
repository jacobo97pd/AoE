using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// A tour of the Tierras de Leyenda for one pairing, opted into with
    /// -emberfieldLegendLandsFilm &lt;folder&gt; [faction] [opponent] (verdant against ashen unless named). A sibling of
    /// <see cref="ArmyFilm"/> built the same way: it drives the simulation itself with no AI and no HUD, writes two
    /// frames per tick (40 fps) and puts each frame's shot name in frames.csv for tools/art/encode_film.py to caption.
    /// Where ArmyFilm shows one army against its rival on open ground, this film shows the map itself: an aerial pass
    /// over the three lands, a close look at each (the elven grove and its moonwell, the highland heart and its
    /// massif, the volcanic river of lava with its ford, tower and volcano), both armies marching out, their clash in
    /// the neutral centre and a push into the volcanic land as its grade fades in. The land shots reuse
    /// <see cref="SceneryStills.LandShots"/>, so they stay in step with whatever the map baker places.
    /// </summary>
    public static class LegendLandsFilm
    {
        public const string Flag = "-emberfieldLegendLandsFilm";
        private static readonly string[] Roles = { "tender", "reedguard", "stringwarden", "strider" };
        private const int FirstOwn = 800, FirstRival = 850;
        public static bool IsRunning { get; private set; }

        private static string Argument(int offset)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + offset >= args.Length || args[index + offset].StartsWith("-", StringComparison.Ordinal)) return null;
            return args[index + offset];
        }

        // Walks an outward ring search, cell by cell, from the desired point until World's own navigation (queried
        // on the already-built baseline) says the ground is clear all the way to a cell's edges, so the formation
        // never has to be hand-tuned per map. IsWalkable alone only answers for the exact point tested: a point can
        // sit in an open cell yet still be close enough to a blocked neighbour that a unit's own radius reaches it
        // (this cost a whole recording attempt the first time), so every candidate is checked at its cell's centre
        // and a ring of points near its edges, matching the widest unit radius this film ever spawns.
        private const int MaxUnitRadiusMillimetres = 500;

        private static bool IsClear(World probe, SimPoint centre, int margin)
        {
            if (!probe.IsWalkable(centre)) return false;
            foreach (var offset in new[] { (margin, 0), (-margin, 0), (0, margin), (0, -margin), (margin, margin), (margin, -margin), (-margin, margin), (-margin, -margin) })
                if (!probe.IsWalkable(new SimPoint(centre.X + offset.Item1, centre.Z + offset.Item2))) return false;
            return true;
        }

        private static SimPoint FindOpenSpot(World probe, SimPoint desired, int cellSize)
        {
            int cx = Mathf.FloorToInt(desired.X / (float)cellSize), cz = Mathf.FloorToInt(desired.Z / (float)cellSize);
            for (int radius = 0; radius <= 24; radius++)
                for (int dz = -radius; dz <= radius; dz++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius) continue;
                        var centre = new SimPoint((cx + dx) * cellSize + cellSize / 2, (cz + dz) * cellSize + cellSize / 2);
                        if (IsClear(probe, centre, MaxUnitRadiusMillimetres)) return centre;
                    }
            return desired;
        }

        public static bool TryCreateWorld(out World world)
        {
            world = null;
            if ((!Debug.isDebugBuild && !Application.isEditor) || Argument(1) == null) return false;
            string faction = Argument(2) ?? "verdant";
            string opponent = Argument(3) ?? "ashen";
            var baseline = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, "legend_lands");
            var map = baseline.Map;
            foreach (var player in map.PlayerFactions) if (player.PlayerId == 2) player.FactionId = opponent;
            var hearths = new Dictionary<int, SimPoint>();
            foreach (var building in map.BuildingSpawns) if (building.DefinitionId == "hearth") hearths[building.OwnerId] = building.Position;
            float metres = map.CellSizeMillimetres * .001f;
            var centre = new SimPoint(Mathf.RoundToInt(map.WidthCells * metres * 500f), Mathf.RoundToInt(map.HeightCells * metres * 500f));
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            foreach (var player in map.PlayerFactions)
            {
                if (!hearths.TryGetValue(player.PlayerId, out var hearth)) continue;
                string unique = null;
                foreach (var definition in baseline.Definition.Factions) if (definition.Id == player.FactionId) unique = definition.UniqueUnitId;
                var line = new List<string>();
                foreach (var role in Roles) { line.Add(role); line.Add(role); }
                if (!string.IsNullOrEmpty(unique)) { line.Add(unique); line.Add(unique); }
                bool own = player.PlayerId == 1;
                // A line formation a few metres in front of the hearth, spread across the direction toward the
                // centre, ready to march inward: this map's hearths do not sit on one axis the way amber_crossing's do.
                float dx = centre.X - hearth.X, dz = centre.Z - hearth.Z, length = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dz * dz));
                float ix = dx / length, iz = dz / length, px = -iz, pz = ix;
                const float ahead = 9000f, spacing = 1600f;
                float start = -(line.Count - 1) * spacing * .5f;
                for (int i = 0; i < line.Count; i++)
                {
                    float offset = start + i * spacing;
                    var desired = new SimPoint(hearth.X + Mathf.RoundToInt(ix * ahead + px * offset), hearth.Z + Mathf.RoundToInt(iz * ahead + pz * offset));
                    // The desired line can fall on an obstacle, a building footprint or a resource node near the
                    // hearth: baseline is already a built World for this exact map (its own starting spawns only),
                    // so its navigation answers whether a point is walkable without the film re-deriving blocking.
                    var position = FindOpenSpot(baseline, desired, map.CellSizeMillimetres);
                    units.Add(new UnitSpawnDefinition { Id = (own ? FirstOwn : FirstRival) + i, OwnerId = player.PlayerId, DefinitionId = line[i], Position = position });
                }
            }
            map.UnitSpawns = units.ToArray();
            // The two armies must see each other and the whole tour from the first frame: every unit sees the
            // default (widest) radius here, exactly as ArmyFilm does for its own showcase.
            map.OfflineMatch.VisionUnitCells = 128;
            foreach (var definition in baseline.Definition.Units) definition.VisionCells = 0;
            world = new World(baseline.Definition, map);
            return true;
        }

        public static void TryStart(MatchController match)
        {
            string folder = Argument(1);
            if (folder == null || !(Debug.isDebugBuild || Application.isEditor)) return;
            IsRunning = true;
            Application.runInBackground = true;
            match.StartCoroutine(new Director(match, folder).Run());
            match.enabled = false;
        }

        private sealed class Director
        {
            private readonly MatchController match;
            private readonly World world;
            private readonly string folder;
            private readonly List<int> own = new List<int>(), rival = new List<int>();
            private StreamWriter index;
            private RenderTexture target;
            private Texture2D pixels;
            private int count;
            private string caption = "";
            private Vector3 focus;
            private float yaw, pitch = 40, zoom = 3, orbit;
            private Func<Vector3> subject;
            private float pan;

            public Director(MatchController match, string folder) { this.match = match; this.folder = folder; world = match.World; }

            public IEnumerator Run()
            {
                yield return null;
                match.enabled = false;
                match.Shell?.Close(); match.OfflineControls.Close();
                yield return null;
                foreach (var unit in world.Units)
                    if (unit.Id >= FirstRival && unit.Id < FirstRival + 50) rival.Add(unit.Id); else if (unit.Id >= FirstOwn && unit.Id < FirstRival) own.Add(unit.Id);
                own.Sort(); rival.Sort();
                string frames = Path.Combine(folder, "frames");
                Directory.CreateDirectory(frames);
                index = new StreamWriter(Path.Combine(frames, "frames.csv"), false, new UTF8Encoding(false));
                index.WriteLine("index,tick,alpha,mode,caption");
                target = new RenderTexture(Mathf.Max(640, Screen.width), Mathf.Max(360, Screen.height), 24, RenderTextureFormat.ARGB32);
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) canvas.enabled = false;
                // Apply() writes camera.orthographicSize directly and never goes through the rig's own clamp, but the
                // maximum is raised anyway in case anything else in the rig reads it while the aerial pass is wide.
                match.Rig.MinimumZoom = 1.5f; match.Rig.MaximumZoom = 70f;

                var lands = SceneryStills.LandShots(world);
                (Vector3 Focus, float Zoom) Named(Func<string, bool> matches, Vector3 fallbackFocus, float fallbackZoom)
                {
                    foreach (var shot in lands) if (matches(shot.Name)) return (shot.Focus, shot.Zoom);
                    return (fallbackFocus, fallbackZoom);
                }
                var elvenHearth = HearthOf(1); var volcanicHearth = HearthOf(2);
                var centre = Named(name => name == "neutral-centre", Vector3.Lerp(elvenHearth, volcanicHearth, .5f), 11);
                var elvenGrove = Named(name => name.StartsWith("elven-", StringComparison.Ordinal) && name.EndsWith("-play", StringComparison.Ordinal), elvenHearth, 8);
                var elvenMoonwell = Named(name => name.StartsWith("elven-", StringComparison.Ordinal) && name.Contains("moonwell"), elvenGrove.Focus, 6);
                var highlandMassif = Named(name => name.StartsWith("neutral-", StringComparison.Ordinal) && name != "neutral-centre", centre.Focus, 10);
                var volcanicFord = Named(name => name.StartsWith("volcanic-", StringComparison.Ordinal) && name.EndsWith("-ford", StringComparison.Ordinal), volcanicHearth, 6);
                var volcanicTower = Named(name => name.StartsWith("volcanic-", StringComparison.Ordinal) && name.Contains("spiked_tower"), volcanicHearth, 6);
                var volcanicPeak = Named(name => name.StartsWith("volcanic-", StringComparison.Ordinal) && name.Contains("volcano-"), volcanicHearth, 10);

                // 1. A wide aerial pass over the whole map: elven, a hold over the neutral centre, then volcanic, each
                // third of the pass belonging to one land so all three read as one continuous piece of ground with
                // roughly equal screen time, instead of the straight elven-to-volcanic lerp that used to leave the
                // last third entirely volcanic. Each leg eases out into the next, so the camera settles rather than
                // snaps through the centre. A pass this wide would otherwise haze into the map's fog well before the
                // far side, exactly as SceneryStills turns fog off for its own widest shot.
                bool fog = RenderSettings.fog;
                RenderSettings.fog = false;
                Vector3 Aerial(float t)
                {
                    t = Mathf.Clamp01(t);
                    if (t < 1f / 3f) return Vector3.Lerp(elvenHearth, centre.Focus, Ease(t * 3f));
                    if (t < 2f / 3f) return centre.Focus;
                    return Vector3.Lerp(centre.Focus, volcanicHearth, Ease((t - 2f / 3f) * 3f));
                }
                Shot("Vista aérea · las Tierras de Leyenda", () => Aerial(pan), 40, 60, 340, 4);
                for (pan = 0; pan <= 1; pan += 1f / 190) yield return Tick();
                RenderSettings.fog = fog;

                // 2. The elven forest close: the grove, then the moonwell.
                Shot("Bosque élfico · la arboleda", () => elvenGrove.Focus, elvenGrove.Zoom, 34, 25, 8);
                yield return Roll(90);
                Shot("Bosque élfico · el pozo de luna", () => elvenMoonwell.Focus, elvenMoonwell.Zoom, 32, 200, 6);
                yield return Roll(80);

                // 3. The highland: the open neutral heart, then its massif.
                Shot("Llanuras · el corazón neutral", () => centre.Focus, 12, 46, 55, 6);
                yield return Roll(80);
                Shot("Llanuras · el macizo", () => highlandMassif.Focus, highlandMassif.Zoom, 36, 150, 8);
                yield return Roll(70);

                // 4. The volcanic waste: the lava river at its ford, the spiked tower, the volcano throwing embers.
                Shot("Tierra volcánica · el río de lava", () => volcanicFord.Focus, volcanicFord.Zoom, 38, 95, 8);
                yield return Roll(80);
                Shot("Tierra volcánica · la torre", () => volcanicTower.Focus, volcanicTower.Zoom, 32, 250, 6);
                yield return Roll(70);
                Shot("Tierra volcánica · el volcán", () => volcanicPeak.Focus, volcanicPeak.Zoom, 30, 15, 10);
                yield return Roll(90);

                // 5. Both armies march out of their lands toward the neutral centre.
                var centreSim = DefinitionLoader.ToSimulation(centre.Focus);
                Order(new MoveCommand(1, Alive(own).ToArray(), centreSim));
                Order(new MoveCommand(2, Alive(rival).ToArray(), centreSim));
                Shot("Los ejércitos avanzan", Middle(null), 15, 48, 40, 4);
                yield return Roll(150);

                // 6. The clash in the neutral centre: each fighter attacks its opposite number; workers stay clear.
                var ownFighters = Fighters(own); var rivalFighters = Fighters(rival);
                for (int i = 0; i < Mathf.Min(ownFighters.Count, rivalFighters.Count); i++)
                {
                    Order(new AttackCommand(1, new[] { ownFighters[i] }, rivalFighters[i]));
                    Order(new AttackCommand(2, new[] { rivalFighters[i] }, ownFighters[i]));
                }
                Shot("El choque en el centro neutral", Middle(null), 9, 55, 30, 6);
                yield return Roll(160);

                // 7. A push into the volcanic land: the survivors advance and the camera follows, crossing the
                // border the same way LandAtmosphere does, so its grade fades in as the ground itself changes.
                Order(new MoveCommand(1, Alive(own).ToArray(), DefinitionLoader.ToSimulation(volcanicHearth)));
                Shot("El avance hacia la tierra volcánica", () => Vector3.Lerp(centre.Focus, volcanicHearth, Mathf.Clamp01(pan)), 16, 46, 320, 3);
                for (pan = 0; pan <= 1; pan += 1f / 190) yield return Tick();

                index.Flush(); index.Dispose();
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
                Debug.Log("EMBERFIELD_LEGEND_LANDS_FILM frames=" + count + " ticks=" + world.TickIndex + " " + folder);
                IsRunning = false;
                SafeQuit.Request(0);
            }

            // Smoothstep: zero velocity at both ends of a leg, so the camera settles into each waypoint (elven
            // hearth, the neutral centre reached from either side, volcanic hearth) instead of snapping through it.
            private static float Ease(float x) => x * x * (3f - 2f * x);

            private Vector3 HearthOf(int ownerId)
            {
                foreach (var building in world.Buildings) if (building.OwnerId == ownerId && building.DefinitionId == "hearth") return DefinitionLoader.ToWorld(building.Position);
                return Vector3.zero;
            }

            private List<int> Alive(List<int> ids)
            {
                var alive = new List<int>();
                foreach (int id in ids) if (world.TryGetUnit(id, out _)) alive.Add(id);
                return alive;
            }

            private List<int> Fighters(List<int> ids)
            {
                var fighters = new List<int>();
                foreach (int id in ids) if (world.TryGetUnit(id, out var unit) && unit.AttackDamage > 0) fighters.Add(id);
                return fighters;
            }

            private void Order(IGameCommand command)
            {
                var result = world.Submit(command);
                if (!result.Accepted) Debug.LogWarning("EMBERFIELD_LEGEND_LANDS_FILM order refused: " + result.Message);
            }

            // The middle of the living film units: ours, or both armies with null. The map's own units (some carry
            // ids above the film's) never count.
            private Func<Vector3> Middle(List<int> ids) => () =>
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (var unit in world.Units)
                    if (ids != null ? ids.Contains(unit.Id) : own.Contains(unit.Id) || rival.Contains(unit.Id)) { sum += DefinitionLoader.ToWorld(unit.Position); n++; }
                return n > 0 ? sum / n : focus;
            };

            private void Shot(string name, Func<Vector3> follow, float size, float tilt, float heading, float degreesPerSecond)
            {
                caption = name;
                subject = follow; zoom = size; pitch = tilt; yaw = heading; orbit = degreesPerSecond;
                focus = follow(); Apply();
                Debug.Log("EMBERFIELD_LEGEND_LANDS_FILM shot=" + name + " tick=" + world.TickIndex + " focus=" + focus.ToString("F1") + " camera=" + match.Rig.Camera.transform.position.ToString("F1"));
            }

            private void Advance(float seconds)
            {
                yaw += orbit * seconds;
                focus = Vector3.Lerp(focus, subject(), 1 - Mathf.Exp(-3f * seconds));
                Apply();
            }

            private void Apply()
            {
                var camera = match.Rig.Camera;
                var rotation = Quaternion.Euler(pitch, yaw, 0);
                camera.transform.rotation = rotation;
                camera.orthographicSize = zoom;
                camera.transform.position = focus + Vector3.up * .8f + rotation * new Vector3(0, 0, -zoom * 3f - 10f);
            }

            private IEnumerator Tick()
            {
                world.Tick();
                match.SyncPresentation(.5f); Advance(World.TickSeconds * .5f); Write(.5f);
                match.SyncPresentation(1f); Advance(World.TickSeconds * .5f); Write(1f);
                if (world.TickIndex % 5 == 0) yield return null;
            }

            private IEnumerator Roll(int ticks)
            {
                for (int i = 0; i < ticks; i++) yield return Tick();
            }

            private void Write(float alpha)
            {
                var camera = match.Rig.Camera;
                var previous = camera.targetTexture;
                camera.targetTexture = target; camera.Render();
                var active = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false);
                RenderTexture.active = active; camera.targetTexture = previous;
                File.WriteAllBytes(Path.Combine(folder, "frames", "frame-" + count.ToString("D6", CultureInfo.InvariantCulture) + ".jpg"), pixels.EncodeToJPG(90));
                index.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},legend,\"{3}\"", count, world.TickIndex, alpha, caption.Replace("\"", "'")));
                count++;
            }
        }
    }
}
