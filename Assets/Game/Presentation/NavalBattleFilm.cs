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
    /// A debug film of a sea battle on the naval Sapphire Coast, opted into with -emberfieldNavalFilm &lt;folder&gt; [faction]
    /// (the English navy against its rival, the Spanish navy, unless named). Three warships a side face each other on the
    /// widest open water nearest the middle of the coast, and the first player has one more hull with a landing party on a
    /// beach further along. A sibling of <see cref="ArmyFilm"/>, built the same way: it drives the simulation itself with no
    /// AI and no HUD, writes a frame per tick (20 fps) and names each frame's shot in frames.csv for
    /// tools/art/encode_film.py. Shots: the two fleets; the landing party boarding; the broadsides at play distance and
    /// close from each side; the transport sailing along the coast and landing its troops; the outcome.
    /// </summary>
    public static class NavalBattleFilm
    {
        public const string Flag = "-emberfieldNavalFilm";
        private static readonly string[] Party = { "reedguard", "reedguard", "reedguard", "stringwarden", "stringwarden" };
        private static int firstOwn, firstRival, transportId, firstParty;
        private static SimPoint landing;
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
            var baseline = DefinitionLoader.CreateOfflineWorld(Argument(2) ?? "english_navy", VictoryMode.Conquest, "sapphire_coast");
            var map = baseline.Map; int cell = map.CellSizeMillimetres;
            string HullOf(int player)
            {
                foreach (var unit in baseline.Definition.Units)
                    if (unit.Domain == MovementDomain.Water && baseline.ValidateUnitRecruitment(player, unit.Id).Accepted) return unit.Id;
                throw new InvalidOperationException("Player " + player + " has no hull to sail.");
            }
            bool Sea(int x, int z) => x >= 0 && z >= 0 && x < map.WidthCells && z < map.HeightCells && baseline.IsSailable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2));
            bool Land(int x, int z) => x >= 0 && z >= 0 && x < map.WidthCells && z < map.HeightCells && baseline.IsWalkable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2));
            SimPoint Centre(int x, int z) => new SimPoint(x * cell + cell / 2, z * cell + cell / 2);
            long Squared(int x, int z, int tx, int tz) => (long)(x - tx) * (x - tx) + (long)(z - tz) * (z - tz);

            // The battle water: open sea twenty-five cells along and five across, nearest the middle of the map.
            int midX = map.WidthCells / 2, midZ = map.HeightCells / 2, cx = -1, cz = -1; long best = long.MaxValue;
            for (int z = 2; z < map.HeightCells - 2; z++)
            for (int x = 12; x < map.WidthCells - 12; x++)
            {
                if (!Sea(x, z) || Squared(x, z, midX, midZ) >= best) continue;
                bool open = true;
                for (int dz = -2; dz <= 2 && open; dz++) for (int dx = -12; dx <= 12 && open; dx++) open = Sea(x + dx, z + dz);
                if (open) { best = Squared(x, z, midX, midZ); cx = x; cz = z; }
            }
            if (cx < 0) throw new InvalidOperationException("The naval film found no open water.");
            // Beaches: dry ground a cell from deep water and two clear cells deep, nearest the requested point.
            (int X, int Z) Beach(int tx, int tz)
            {
                (int, int) found = (-1, -1); long nearest = long.MaxValue;
                for (int z = 1; z < map.HeightCells - 1; z++)
                for (int x = 1; x < map.WidthCells - 1; x++)
                {
                    if (Squared(x, z, tx, tz) >= nearest || !Land(x, z)) continue;
                    bool clear = true;
                    for (int dz = -1; dz <= 1 && clear; dz++) for (int dx = -1; dx <= 1 && clear; dx++) clear = Land(x + dx, z + dz) || Sea(x + dx, z + dz);
                    bool shore = Sea(x - 2, z) || Sea(x + 2, z) || Sea(x, z - 2) || Sea(x, z + 2);
                    if (clear && shore) { nearest = Squared(x, z, tx, tz); found = (x, z); }
                }
                return found;
            }
            (int X, int Z) Water(int tx, int tz)
            {
                (int, int) found = (-1, -1); long nearest = long.MaxValue;
                for (int z = 1; z < map.HeightCells - 1; z++)
                for (int x = 1; x < map.WidthCells - 1; x++)
                    if (Squared(x, z, tx, tz) < nearest && Sea(x, z) && Sea(x - 1, z) && Sea(x + 1, z) && Sea(x, z - 1) && Sea(x, z + 1)) { nearest = Squared(x, z, tx, tz); found = (x, z); }
                return found;
            }

            int next = 1;
            foreach (var spawn in map.UnitSpawns) next = Math.Max(next, spawn.Id + 1);
            foreach (var spawn in map.BuildingSpawns) next = Math.Max(next, spawn.Id + 1);
            foreach (var spawn in map.ResourceSpawns) next = Math.Max(next, spawn.Id + 1);
            next += 100;
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            string ownHull = HullOf(1), rivalHull = HullOf(2);
            // Two lines sixteen metres apart, out of each other's reach until ordered in.
            int[,] own = { { -8, -1 }, { -8, 1 }, { -11, 0 } }, rival = { { 8, -1 }, { 8, 1 }, { 11, 0 } };
            firstOwn = next;
            for (int i = 0; i < 3; i++) units.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 1, DefinitionId = ownHull, Position = Centre(cx + own[i, 0], cz + own[i, 1]) });
            firstRival = next;
            for (int i = 0; i < 3; i++) units.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 2, DefinitionId = rivalHull, Position = Centre(cx + rival[i, 0], cz + rival[i, 1]) });
            // The landing party waits on a beach twenty-four metres behind its fleet, its hull in the water beside it, and is
            // landed on the beach twenty-four metres beyond the rival's.
            int side = cz < midZ ? 1 : -1; // The land lies away from the open sea's edge.
            var start = Beach(cx - 24, cz + side * 4);
            var water = Water(start.X, start.Z);
            var goal = Beach(cx + 24, cz + side * 4);
            if (start.X < 0 || water.X < 0 || goal.X < 0) throw new InvalidOperationException("The naval film found no beach for its landing.");
            transportId = next;
            units.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 1, DefinitionId = ownHull, Position = Centre(water.X, water.Z) });
            firstParty = next;
            int placed = 0;
            for (int ring = 0; ring <= 4 && placed < Party.Length; ring++)
                for (int dz = -ring; dz <= ring && placed < Party.Length; dz++)
                    for (int dx = -ring; dx <= ring && placed < Party.Length; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring || !Land(start.X + dx, start.Z + dz)) continue;
                        units.Add(new UnitSpawnDefinition { Id = next++, OwnerId = 1, DefinitionId = Party[placed++], Position = Centre(start.X + dx, start.Z + dz) });
                    }
            landing = Centre(goal.X, goal.Z);
            map.UnitSpawns = units.ToArray();
            // Both fleets see each other from the first frame, as in ArmyFilm.
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
            private readonly List<int> own = new List<int>(), rival = new List<int>(), party = new List<int>();
            private StreamWriter index;
            private RenderTexture target;
            private Texture2D pixels;
            private int count;
            private string caption = "", title = "";
            private Vector3 focus;
            private float yaw, pitch = 45, zoom = 8, orbit;
            private Func<Vector3> subject;

            public Director(MatchController match, string folder) { this.match = match; this.folder = folder; world = match.World; }

            public IEnumerator Run()
            {
                yield return null;
                match.enabled = false;
                match.Shell?.Close(); match.OfflineControls.Close();
                yield return null;
                for (int i = 0; i < 3; i++) { own.Add(firstOwn + i); rival.Add(firstRival + i); }
                for (int id = firstParty; id < firstParty + Party.Length; id++) if (world.TryGetUnit(id, out _)) party.Add(id);
                title = FrontierCodex.Name(world.Map.PlayerFactions[0].FactionId) + " contra " + FrontierCodex.Name(world.Map.PlayerFactions[1].FactionId);
                string frames = Path.Combine(folder, "frames");
                Directory.CreateDirectory(frames);
                index = new StreamWriter(Path.Combine(frames, "frames.csv"), false, new UTF8Encoding(false));
                index.WriteLine("index,tick,alpha,mode,caption");
                target = new RenderTexture(Mathf.Max(640, Screen.width), Mathf.Max(360, Screen.height), 24, RenderTextureFormat.ARGB32);
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) canvas.enabled = false;
                match.Rig.MinimumZoom = 1.5f; match.Rig.MaximumZoom = 40f;
                var fleets = new List<int>(own); fleets.AddRange(rival);

                // 1. The two fleets, out of reach of each other.
                Shot("Las dos flotas", Middle(fleets), 9f, 50, 0, 4);
                yield return Roll(60);
                // 2. The landing party walks aboard its hull.
                Order(new EmbarkCommand(1, party.ToArray(), transportId));
                Shot("El desembarco · las tropas embarcan", () => Position(transportId), 4f, 40, 200, 0);
                for (int i = 0; i < 500 && Cargo() < party.Count; i++) yield return Tick();
                // 3. Broadsides: each hull on its nearest enemy, again whenever its target sinks.
                Engage();
                Shot("Andanadas · a distancia de juego", Middle(fleets), 8f, 50, 20, 3);
                yield return Roll(160);
                Shot("Cerca · " + Hull(own), () => Position(Living(own)), 4.5f, 38, 210, 8);
                yield return Roll(120);
                Shot("Cerca · " + Hull(rival), () => Position(Living(rival)), 4.5f, 38, 30, 8);
                yield return Roll(120);
                // 4. The transport sails along the coast and lands its troops beyond the rival's line.
                if (Cargo() > 0) Order(new DisembarkCommand(1, transportId, landing));
                Shot("El desembarco · rumbo a la orilla", () => Position(transportId), 6f, 45, 180, 0);
                for (int i = 0; i < 1200 && Cargo() > 0 && world.TryGetUnit(transportId, out _); i++) yield return Tick();
                Shot("El desembarco · en tierra", Middle(party), 3.5f, 36, 200, 10);
                yield return Roll(80);
                // 5. The outcome from far.
                Shot("Lejos · el desenlace", Middle(fleets), 11f, 55, 20, 2);
                yield return Roll(160);

                index.Flush(); index.Dispose();
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
                int ownAfloat = 0, rivalAfloat = 0, ashore = 0;
                foreach (int id in own) if (world.TryGetUnit(id, out _)) ownAfloat++;
                foreach (int id in rival) if (world.TryGetUnit(id, out _)) rivalAfloat++;
                foreach (int id in party) if (world.TryGetUnit(id, out var unit) && unit.Domain == MovementDomain.Land && world.IsWalkable(unit.Position)) ashore++;
                Debug.Log("EMBERFIELD_NAVAL_FILM frames=" + count + " ticks=" + world.TickIndex + " ownAfloat=" + ownAfloat + " rivalAfloat=" + rivalAfloat +
                    " landed=" + ashore + "/" + party.Count + " " + folder);
                IsRunning = false;
                SafeQuit.Request(0);
            }

            private int Cargo() => world.TryGetUnit(transportId, out var ship) ? ship.CargoCount : 0;
            private string Hull(List<int> ids) => world.TryGetUnit(Living(ids), out var unit) ? unit.DefinitionId == "english_frigate" ? "las fragatas" :
                unit.DefinitionId == "spanish_galleon" ? "los galeones" : unit.DefinitionId == "pirate_sloop" ? "las balandras" : unit.DefinitionId : "";

            // Every hull without a living target takes the nearest enemy hull.
            private void Engage()
            {
                foreach (var (ids, owner, enemies) in new[] { (own, 1, rival), (rival, 2, own) })
                    foreach (int id in ids)
                    {
                        if (!world.TryGetUnit(id, out var ship) || ship.AttackTargetId != 0 && world.TryGetUnit(ship.AttackTargetId, out _)) continue;
                        int nearest = 0; long distance = long.MaxValue;
                        foreach (int enemy in enemies)
                        {
                            if (!world.TryGetUnit(enemy, out var other)) continue;
                            long dx = other.Position.X - ship.Position.X, dz = other.Position.Z - ship.Position.Z;
                            if (dx * dx + dz * dz < distance) { distance = dx * dx + dz * dz; nearest = enemy; }
                        }
                        if (nearest != 0) Order(new AttackCommand(owner, new[] { id }, nearest));
                    }
            }

            private int Living(List<int> ids)
            {
                foreach (int id in ids) if (world.TryGetUnit(id, out _)) return id;
                return ids.Count > 0 ? ids[0] : 0;
            }

            private void Order(IGameCommand command)
            {
                var result = world.Submit(command);
                if (!result.Accepted) Debug.LogWarning("EMBERFIELD_NAVAL_FILM order refused: " + result.Message);
            }

            private Vector3 Position(int id) => world.TryGetUnit(id, out var unit) ? DefinitionLoader.ToWorld(unit.Position) : focus;

            private Func<Vector3> Middle(List<int> ids) => () =>
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (int id in ids) if (world.TryGetUnit(id, out var unit)) { sum += DefinitionLoader.ToWorld(unit.Position); n++; }
                return n > 0 ? sum / n : focus;
            };

            private void Shot(string name, Func<Vector3> follow, float size, float tilt, float heading, float degreesPerSecond)
            {
                caption = title + " · " + name;
                subject = follow; zoom = size; pitch = tilt; yaw = heading; orbit = degreesPerSecond;
                focus = follow(); Apply();
                Debug.Log("EMBERFIELD_NAVAL_FILM shot=" + name + " tick=" + world.TickIndex + " focus=" + focus.ToString("F1"));
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
                if (world.TickIndex % World.TickRate == 0) Engage();
                match.SyncPresentation(1f); Advance(World.TickSeconds); Write();
                if (world.TickIndex % 5 == 0) yield return null;
            }

            private IEnumerator Roll(int ticks)
            {
                for (int i = 0; i < ticks; i++) yield return Tick();
            }

            private void Write()
            {
                var camera = match.Rig.Camera;
                var previous = camera.targetTexture;
                camera.targetTexture = target; camera.Render();
                var active = RenderTexture.active; RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false);
                RenderTexture.active = active; camera.targetTexture = previous;
                File.WriteAllBytes(Path.Combine(folder, "frames", "frame-" + count.ToString("D6", CultureInfo.InvariantCulture) + ".jpg"), pixels.EncodeToJPG(88));
                index.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},battle,\"{3}\"", count, world.TickIndex, 1f, caption.Replace("\"", "'")));
                count++;
            }
        }
    }
}
