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
    /// A debug film of one faction's whole army against its rival, opted into with
    /// -emberfieldArmyFilm &lt;folder&gt; [faction]. Two of each unit (worker, spear, archer, rider, unique) line up for each
    /// side on the clear ground north of the player's hearth on amber_crossing: the rows the siege showcase proved open.
    /// The film drives the simulation itself, with no AI and no HUD. It writes two frames per tick (40 fps) and puts
    /// each frame's shot name in frames.csv, which tools/art/encode_film.py turns into captions.
    ///
    /// Shots: the formation up close; the army on the move (infantry at a walk or run, cavalry at a gallop); a closer
    /// look at just the mounted riders galloping, so each faction's own mount reads clearly; the advance at middle
    /// distance; the battle at play distance; the melee up close from both sides; the outcome from far.
    /// </summary>
    public static class ArmyFilm
    {
        public const string Flag = "-emberfieldArmyFilm";
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

        public static bool TryCreateWorld(out World world)
        {
            world = null;
            if ((!Debug.isDebugBuild && !Application.isEditor) || Argument(1) == null) return false;
            var baseline = DefinitionLoader.CreateOfflineWorld(Argument(2) ?? "aven", VictoryMode.Conquest, "amber_crossing");
            var map = baseline.Map;
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            foreach (var player in map.PlayerFactions)
            {
                string unique = null;
                foreach (var definition in baseline.Definition.Factions) if (definition.Id == player.FactionId) unique = definition.UniqueUnitId;
                var line = new List<string>();
                foreach (var role in Roles) { line.Add(role); line.Add(role); }
                if (!string.IsNullOrEmpty(unique)) { line.Add(unique); line.Add(unique); }
                bool own = player.PlayerId == 1;
                for (int i = 0; i < line.Count; i++)
                    units.Add(new UnitSpawnDefinition { Id = (own ? FirstOwn : FirstRival) + i, OwnerId = player.PlayerId, DefinitionId = line[i],
                        Position = new SimPoint((own ? 15500 : 17000) + i * 1300, own ? 29500 : 40500) });
            }
            map.UnitSpawns = units.ToArray();
            // The two armies must see each other from the first frame: without it the rival stands in the fog, its
            // units are hidden and the attack orders are refused. Every unit sees the default (widest) radius here.
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
            // The match never runs its own loop: a single tick of the offline AI sends the rival army home, out of shot.
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
            private string caption = "", faction = "";
            private Vector3 focus;
            private float yaw, pitch = 40, zoom = 3, orbit;
            private Func<Vector3> subject;

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
                if (world.TryGetPlayer(1, out _) && match.Factions?.LocalDefinition != null) faction = match.Factions.LocalDefinition.DisplayName;
                string frames = Path.Combine(folder, "frames");
                Directory.CreateDirectory(frames);
                index = new StreamWriter(Path.Combine(frames, "frames.csv"), false, new UTF8Encoding(false));
                index.WriteLine("index,tick,alpha,mode,caption");
                target = new RenderTexture(Mathf.Max(640, Screen.width), Mathf.Max(360, Screen.height), 24, RenderTextureFormat.ARGB32);
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                // The film shows the world alone: every interface canvas (HUD, minimap, voice panel) is switched off.
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) canvas.enabled = false;
                match.Rig.MinimumZoom = 1.5f; match.Rig.MaximumZoom = 40f;

                // 1. The formation up close: a slow pan along our line, then along theirs.
                var ownLine = Line(own); var rivalLine = Line(rival);
                Shot("Cerca · la formación", () => Vector3.Lerp(ownLine.a, ownLine.b, Mathf.Clamp01(pan)), 2.6f, 34, 200, 0);
                for (pan = 0; pan <= 1; pan += 1f / 90) yield return Tick();
                Shot("Cerca · el rival", () => Vector3.Lerp(rivalLine.a, rivalLine.b, Mathf.Clamp01(pan)), 2.6f, 34, 20, 0);
                for (pan = 0; pan <= 1; pan += 1f / 70) yield return Tick();

                // 2. On the move: the whole line marches east; the camera rides along beside it.
                Order(new MoveCommand(1, own.ToArray(), new SimPoint(30000, 29500)));
                Shot("Cerca · en marcha", Middle(own), 3.4f, 36, 165, 0);
                yield return Roll(80);

                // 2b. A closer look at just the mounted riders, still at a gallop, so each faction's own mount
                // (skeleton and animation, not just the generic army shape) reads clearly for a beat.
                var ownRiders = Riders(own);
                if (ownRiders.Count > 0)
                {
                    Shot("Cerca · los jinetes al galope", Middle(ownRiders), 2f, 32, 210, 14);
                    yield return Roll(40);
                }

                // 3. The advance at middle distance: back toward the rival.
                Order(new MoveCommand(1, own.ToArray(), new SimPoint(22000, 35500)));
                Shot("Media distancia · el ejército avanza", Middle(own), 6f, 46, 150, 5);
                yield return Roll(70);

                // 4. The battle at play distance: each fighter attacks its opposite number; the workers, who carry no
                // weapon, stay out of it.
                var ownFighters = Fighters(own); var rivalFighters = Fighters(rival);
                for (int i = 0; i < Mathf.Min(ownFighters.Count, rivalFighters.Count); i++)
                {
                    Order(new AttackCommand(1, new[] { ownFighters[i] }, rivalFighters[i]));
                    Order(new AttackCommand(2, new[] { rivalFighters[i] }, ownFighters[i]));
                }
                Shot("Distancia de juego · la batalla", Middle(null), 9.5f, 55, 35, 0);
                yield return Roll(90);

                // 5. The melee up close, from our side and then from theirs.
                Shot("Cerca · en pleno combate", () => Position(Living(ownFighters)), 2.8f, 35, 210, 12);
                yield return Roll(80);
                Shot("Cerca · el rival combate", () => Position(Living(rivalFighters)), 2.8f, 35, 30, 12);
                yield return Roll(60);

                // 6. The outcome from far.
                Shot("Lejos · el desenlace", Middle(null), 13f, 58, 30, 3);
                yield return Roll(70);

                index.Flush(); index.Dispose();
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
                Debug.Log("EMBERFIELD_ARMY_FILM frames=" + count + " ticks=" + world.TickIndex + " " + folder);
                IsRunning = false;
                SafeQuit.Request(0);
            }

            private float pan;

            private (Vector3 a, Vector3 b) Line(List<int> ids)
            {
                if (ids.Count == 0) return (focus, focus);
                return (Position(ids[0]), Position(ids[ids.Count - 1]));
            }

            private List<int> Fighters(List<int> ids)
            {
                var fighters = new List<int>();
                foreach (int id in ids) if (world.TryGetUnit(id, out var unit) && unit.AttackDamage > 0) fighters.Add(id);
                return fighters;
            }

            // The mounted role alone (each faction's own skeleton and animation set), for a shot that shows the
            // mount off rather than the whole mixed-arms line.
            private List<int> Riders(List<int> ids)
            {
                var riders = new List<int>();
                foreach (int id in ids) if (world.TryGetUnit(id, out var unit) && unit.DefinitionId == "strider") riders.Add(id);
                return riders;
            }

            private int Living(List<int> ids)
            {
                foreach (int id in ids) if (world.TryGetUnit(id, out _)) return id;
                return ids.Count > 0 ? ids[0] : 0;
            }

            private void Order(IGameCommand command)
            {
                var result = world.Submit(command);
                if (!result.Accepted) Debug.LogWarning("EMBERFIELD_ARMY_FILM order refused: " + result.Message);
            }

            private Vector3 Position(int id) => world.TryGetUnit(id, out var unit) ? DefinitionLoader.ToWorld(unit.Position) : focus;

            // The middle of the living film units: ours, or both armies with null. The map's own units (some carry ids
            // above the film's) never count.
            private Func<Vector3> Middle(List<int> ids) => () =>
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (var unit in world.Units)
                    if (ids != null ? ids.Contains(unit.Id) : own.Contains(unit.Id) || rival.Contains(unit.Id)) { sum += DefinitionLoader.ToWorld(unit.Position); n++; }
                return n > 0 ? sum / n : focus;
            };

            private void Shot(string name, Func<Vector3> follow, float size, float tilt, float heading, float degreesPerSecond)
            {
                caption = (faction.Length > 0 ? faction + " · " : "") + name;
                subject = follow; zoom = size; pitch = tilt; yaw = heading; orbit = degreesPerSecond;
                focus = follow(); Apply();
                var where = new StringBuilder();
                foreach (var unit in world.Units)
                    if (own.Contains(unit.Id) || rival.Contains(unit.Id)) where.Append(' ').Append(unit.Id).Append(':').Append(unit.DefinitionId).Append('@').Append(unit.Position.X).Append(',').Append(unit.Position.Z);
                Debug.Log("EMBERFIELD_ARMY_FILM shot=" + name + " tick=" + world.TickIndex + " focus=" + focus.ToString("F1") + " camera=" + match.Rig.Camera.transform.position.ToString("F1") + where);
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
                index.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},battle,\"{3}\"", count, world.TickIndex, alpha, caption.Replace("\"", "'")));
                count++;
            }
        }
    }
}
