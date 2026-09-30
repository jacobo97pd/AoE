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
    /// A short siege film, opted into with -emberfieldSiegeFilm &lt;folder&gt; [defender] [attacker] (ashen defends
    /// against verdant unless named). Built the same way as <see cref="ArmyFilm"/> and <see cref="LegendLandsFilm"/>:
    /// match.enabled stays false so the AI never ticks, every Canvas is disabled, the director drives World.Tick
    /// itself and writes two frames per tick (40 fps, since <see cref="World.TickRate"/> is 20) into
    /// frames/frame-NNNNNN.jpg plus frames.csv for tools/art/encode_film.py to caption.
    ///
    /// Unlike those two, the world this builds is a small, ALREADY COMPLETE orc palisade -- a front wall with a gate
    /// and a watchtower, a couple of buildings behind it -- and the assault on it: verdant siege ladders and
    /// boarding soldiers, ordered with the same BoardWallCommand recipe <see cref="SiegeShowcase"/> proved (a ladder
    /// must be idle within 1300 mm of the wall; a boarding soldier within 1800 mm climbs over 100 ticks, or 40 for a
    /// defender boarding its own wall).
    ///
    /// The fortress sits on legend_lands, in the volcanic waste that is baked at the ashen (player 2) hearth's
    /// position. CreateOfflineWorld always seats its chosen faction at player 1 -- the elven grove's hearth on this
    /// map -- so the attacker's faction is what gets passed there and the defender is placed at player 2 afterward,
    /// exactly as LegendLandsFilm does for this same pairing; that is what keeps the orc side standing on the ash
    /// rather than in the grove. A short, straight row of that ground, clear enough for four wall segments and a
    /// gate, is found the way LegendLandsFilm finds its own formations: by probing World.IsWalkable outward from the
    /// hearth until a clear stretch turns up (walls only run along X, so the row is a straight line at one Z). If
    /// none does, the film falls back to the row SiegeShowcaseScenario already proved open on amber_crossing.
    /// </summary>
    public static class SiegeFilm
    {
        public const string Flag = "-emberfieldSiegeFilm";

        // Authored identifiers, all well clear of anything a map spawns on its own.
        private const int WallA = 910, WallB = 911, GateId = 912, WallC = 913, WallD = 914, TowerId = 915, HearthId = 916, ShelterId = 917;
        private static readonly int[] DefenderIds = { 940, 941, 942 };
        private static readonly int[] LadderIds = { 950, 951 };
        private static readonly int[] AttackerIds = { 960, 961, 962, 963, 964, 965, 966 };
        public static bool IsRunning { get; private set; }

        private static string Argument(int offset)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + offset >= args.Length || args[index + offset].StartsWith("-", StringComparison.Ordinal)) return null;
            return args[index + offset];
        }

        // ================================================================================ site search

        // A margin the widest unit here (the siege ladder, 350 mm radius) can stand at without its edge crossing
        // into a blocked neighbour cell -- the same caution LegendLandsFilm's own IsClear takes.
        private static bool IsWalkableRow(World probe, int cx, int z, int inwardZ, int cellSize)
        {
            for (int i = -7; i <= 7; i++)
                if (!probe.IsWalkable(new SimPoint(cx + i * cellSize, z))) return false;
            int outwardZ = -inwardZ;
            // The staging ground the attackers start on and cross, well outside the row.
            foreach (int dx in new[] { -6, -4, -2, 0, 2, 4, 6 })
                if (!probe.IsWalkable(new SimPoint(cx + dx * cellSize, z + outwardZ * 13 * cellSize))) return false;
            // Behind the row: the watchtower and the two backdrop buildings, checked over every cell of the actual
            // footprint Build() will place -- not just its centre -- and at the same whole-cell-aligned spine Build()
            // uses (see the comment on Build's own cx), so a row this passes can never make Build() spawn a
            // building onto ground, or another building's footprint, that was never actually verified clear.
            int alignedCx = cx - cellSize / 2;
            int towerZ = z + inwardZ * 3 * cellSize + inwardZ * cellSize / 2;
            if (!IsClearFootprint(probe, alignedCx, towerZ, 2, 2, cellSize)) return false;
            int hearthZ = z + inwardZ * (4 * cellSize + cellSize / 2);
            if (!IsClearFootprint(probe, alignedCx - 6 * cellSize, hearthZ, 4, 4, cellSize)) return false;
            if (!IsClearFootprint(probe, alignedCx + 6 * cellSize, hearthZ, 4, 4, cellSize)) return false;
            return true;
        }

        /// <summary>Every cell of a WidthCells x DepthCells footprint centred at (centerX, centerZ) is walkable.</summary>
        private static bool IsClearFootprint(World probe, int centerX, int centerZ, int widthCells, int depthCells, int cellSize)
        {
            int left = centerX - widthCells * cellSize / 2, bottom = centerZ - depthCells * cellSize / 2;
            for (int dz = 0; dz < depthCells; dz++)
                for (int dx = 0; dx < widthCells; dx++)
                    if (!probe.IsWalkable(new SimPoint(left + dx * cellSize + cellSize / 2, bottom + dz * cellSize + cellSize / 2)))
                        return false;
            return true;
        }

        // IsWalkableRow only samples terrain: it says nothing about the map's own starting units (workers, an
        // opposing hearth's garrison), which never block navigation and so never fail a terrain probe, yet still
        // collide with whatever Build() spawns on top of them. The only check that actually speaks for the whole
        // candidate -- terrain, this film's own eight buildings, and every unit, all at once -- is World's own
        // constructor, so each candidate is proven by really building it, and only kept on success. Map.BuildingSpawns
        // and UnitSpawns are reset before every attempt, since a failed Build() has already appended its candidate
        // onto them and would otherwise leave that debris for the next candidate to build on top of.
        private static bool TryFindRow(World probe, MapDefinition map, int anchorX, int anchorZ, int inwardZ, int cellSize, out World world)
        {
            var originalBuildings = map.BuildingSpawns; var originalUnits = map.UnitSpawns;
            int baseX = Mathf.RoundToInt(anchorX / (float)cellSize) * cellSize + cellSize / 2;
            int baseZ = Mathf.RoundToInt(anchorZ / (float)cellSize) * cellSize + cellSize / 2;
            int walkable = 0, rejected = 0;
            // A wide net: the hearth/shelter alone need a fully clear 4x4 cells, and a biome with any real density
            // of rock decoration can leave the narrower band this used to search entirely without one, even though
            // open ground exists a little further out. 34 steps deep by 41 wide covers a generous stretch of the
            // land on either side of the anchor without costing much -- IsWalkableRow (cheap, terrain only) is
            // tried before the expensive full Build() on every candidate.
            for (int steps = 7; steps <= 40; steps++)
            {
                int z = baseZ - inwardZ * steps * cellSize;
                for (int xSteps = -20; xSteps <= 20; xSteps++)
                {
                    int cx = baseX + xSteps * cellSize;
                    if (!IsWalkableRow(probe, cx, z, inwardZ, cellSize)) continue;
                    walkable++;
                    map.BuildingSpawns = originalBuildings; map.UnitSpawns = originalUnits;
                    try { world = Build(probe, map, new SimPoint(cx, z), inwardZ); return true; }
                    catch (ArgumentException) { rejected++; /* collides with a unit or building the terrain probe cannot see; try the next */ }
                }
            }
            Debug.LogWarning("EMBERFIELD_SIEGE_FILM row search around " + anchorX + "/" + anchorZ + " found " + walkable +
                " terrain-clear candidates, " + rejected + " rejected on build (unit/building collision).");
            world = null;
            return false;
        }

        // ================================================================================ world

        public static bool TryCreateWorld(out World world)
        {
            world = null;
            if ((!Debug.isDebugBuild && !Application.isEditor) || Argument(1) == null) return false;
            string defender = Argument(2) ?? "ashen";
            string attacker = Argument(3) ?? "verdant";
            if (TryLegendLands(defender, attacker, out world)) return true;
            if (TryAmberCrossing(defender, attacker, out world)) return true;
            Debug.LogError("EMBERFIELD_SIEGE_FILM found no clear row for the fortress.");
            return false;
        }

        private static bool TryLegendLands(string defender, string attacker, out World world)
        {
            world = null;
            // The chosen faction always lands at player 1 (the elven grove's hearth here), so the attacker goes in
            // as that and the defender is swapped onto player 2 afterward -- the volcanic ground is baked at that
            // player's hearth regardless of which faction id sits there, exactly as LegendLandsFilm relies on.
            World baseline;
            try { baseline = DefinitionLoader.CreateOfflineWorld(attacker, VictoryMode.Conquest, "legend_lands"); }
            catch (ArgumentException) { return false; }
            var map = baseline.Map;
            foreach (var player in map.PlayerFactions) if (player.PlayerId == 2) player.FactionId = defender;
            SimPoint hearth = default; bool found = false;
            foreach (var building in map.BuildingSpawns) if (building.OwnerId == 2 && building.DefinitionId == "hearth") { hearth = building.Position; found = true; }
            if (!found) return false;
            float metres = map.CellSizeMillimetres * .001f;
            var centre = new SimPoint(Mathf.RoundToInt(map.WidthCells * metres * 500f), Mathf.RoundToInt(map.HeightCells * metres * 500f));
            int inwardZ = centre.Z < hearth.Z ? 1 : -1; // toward the hearth; the open field is the other way
            return TryFindRow(baseline, map, hearth.X, hearth.Z, inwardZ, map.CellSizeMillimetres, out world);
        }

        private static bool TryAmberCrossing(string defender, string attacker, out World world)
        {
            world = null;
            World baseline;
            try { baseline = DefinitionLoader.CreateOfflineWorld(attacker, VictoryMode.Conquest, "amber_crossing"); }
            catch (ArgumentException) { return false; }
            var map = baseline.Map;
            foreach (var player in map.PlayerFactions) if (player.PlayerId == 2) player.FactionId = defender;
            // SiegeShowcaseScenario proved z=30500, x=15000..38000 clear of terrain; the gate at x=26000 there
            // matches this film's own wall/gate spacing exactly, so it is tried first -- validated the same way as
            // every other candidate, by actually building it -- before falling back to the wider search.
            const int inwardZ = -1;
            if (IsWalkableRow(baseline, 26000, 30500, inwardZ, map.CellSizeMillimetres))
            {
                var originalBuildings = map.BuildingSpawns; var originalUnits = map.UnitSpawns;
                try { world = Build(baseline, map, new SimPoint(26000, 30500), inwardZ); return true; }
                catch (ArgumentException) { map.BuildingSpawns = originalBuildings; map.UnitSpawns = originalUnits; }
            }
            return TryFindRow(baseline, map, 26000, 30500, inwardZ, map.CellSizeMillimetres, out world);
        }

        private const int Attacker = 1, Defender = 2;

        // The direction (in simulation Z, which shares its sign with world-space Z under DefinitionLoader.ToWorld)
        // away from the fortress's rear, toward the field the elves cross. Read by the Director for its camera and
        // approach-mark math, since which way is "outside" depends on which site was found.
        internal static int OutwardZ { get; private set; } = 1;

        private static World Build(World baseline, MapDefinition map, SimPoint row, int inwardZ)
        {
            int cellSize = map.CellSizeMillimetres;
            int outwardZ = -inwardZ;
            OutwardZ = outwardZ;
            // row.X sits at the centre of TryFindRow's probed cell (a half-cell offset), which is exactly right for
            // a 3-wide wall segment's own centre but wrong for the 2-wide gate and the 2x2/4x4 buildings, which
            // World's spawn validation requires to centre on a whole-cell boundary. Shifting the spine back by half
            // a cell here -- and nowhere else -- makes the gate and tower/hearth/shelter land on that boundary while
            // the wall math below (which spaces every segment by half-widths around this spine) keeps landing the
            // odd-width walls on their own required half-cell centres, exactly as it did before the shift.
            int cx = row.X - cellSize / 2, z = row.Z;
            int wallSpan = 3 * cellSize, gateSpan = 2 * cellSize;

            var buildings = new List<BuildingSpawnDefinition>(map.BuildingSpawns)
            {
                Building(WallB, Defender, "wall", cx - gateSpan / 2 - wallSpan / 2, z),
                Building(WallA, Defender, "wall", cx - gateSpan / 2 - wallSpan * 3 / 2, z),
                Building(GateId, Defender, "gate", cx, z),
                Building(WallC, Defender, "wall", cx + gateSpan / 2 + wallSpan / 2, z),
                Building(WallD, Defender, "wall", cx + gateSpan / 2 + wallSpan * 3 / 2, z),
                // The watchtower is 2x2 (an even footprint on both axes), so unlike the walls and gate it needs a
                // whole-cell-aligned Z too; the plain 3-cell inward offset keeps z's own half-cell parity, so an
                // extra half-cell push in the same inward direction is added to land it on the boundary.
                Building(TowerId, Defender, "watchtower", cx, z + inwardZ * 3 * cellSize + inwardZ * cellSize / 2),
                Building(HearthId, Defender, "hearth", cx - 6 * cellSize, z + inwardZ * (4 * cellSize + cellSize / 2)),
                Building(ShelterId, Defender, "shelter", cx + 6 * cellSize, z + inwardZ * (4 * cellSize + cellSize / 2)),
            };
            map.BuildingSpawns = buildings.ToArray();

            int wallBX = cx - gateSpan / 2 - wallSpan / 2, wallCX = cx + gateSpan / 2 + wallSpan / 2;
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns)
            {
                // Defenders: two on the west-of-gate deck, one on the east-of-gate deck, already up before the
                // ladders ever arrive.
                Unit(DefenderIds[0], Defender, "reedguard", wallBX - 800, z + inwardZ * 1200),
                Unit(DefenderIds[1], Defender, "reedguard", wallBX + 800, z + inwardZ * 1200),
                Unit(DefenderIds[2], Defender, "reedguard", wallCX, z + inwardZ * 1200),
            };
            // Ladders stage roughly 13 m out, lined up with the deck they will lean on.
            int stage = z + outwardZ * 13 * cellSize;
            units.Add(Unit(LadderIds[0], Attacker, "siege_ladder", wallBX, stage));
            units.Add(Unit(LadderIds[1], Attacker, "siege_ladder", wallCX, stage));
            // Their crews: a loose group in front, staggered so no two stand on the same spot. Index 3 sits at
            // wallCX -- exactly L1's own X -- so the alternating Z stagger must clear a ladder's 350 mm radius
            // plus a stringwarden's 300 mm one (650 mm) as well as separating the soldiers from each other; 800
            // clears both with room to spare (a plain 400 left it 250 mm short of the ladder and always rejected
            // the whole site, on every candidate row on either map).
            int[] soldierX = { wallBX - 900, wallBX + 900, wallCX - 1300, wallCX, wallCX + 1300, cx - 4500, cx + 4500 };
            string[] roles = { "reedguard", "stringwarden", "reedguard", "stringwarden", "reedguard", "stringwarden", "reedguard" };
            for (int i = 0; i < AttackerIds.Length; i++)
                units.Add(Unit(AttackerIds[i], Attacker, roles[i], soldierX[i], stage + outwardZ * (i % 2 == 0 ? 800 : -800)));
            map.UnitSpawns = units.ToArray();

            // Every unit must see across the field for the move and board orders to be accepted.
            map.OfflineMatch.VisionUnitCells = 128;
            foreach (var definition in baseline.Definition.Units) definition.VisionCells = 0;
            return new World(baseline.Definition, map);
        }

        private static BuildingSpawnDefinition Building(int id, int owner, string definitionId, int x, int z)
            => new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definitionId, Position = new SimPoint(x, z) };
        private static UnitSpawnDefinition Unit(int id, int owner, string definitionId, int x, int z)
            => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definitionId, Position = new SimPoint(x, z) };

        // ================================================================================ director

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
            private StreamWriter index;
            private RenderTexture target;
            private Texture2D pixels;
            private int count;
            private string caption = "";
            private Vector3 focus;
            private float yaw, pitch = 44, zoom = 8, orbit;
            private Func<Vector3> subject;
            private float faceYaw;
            private int wallBId, wallCId;

            public Director(MatchController match, string folder) { this.match = match; this.folder = folder; world = match.World; }

            public IEnumerator Run()
            {
                yield return null;
                match.enabled = false;
                match.Shell?.Close(); match.OfflineControls.Close();
                yield return null;
                string frames = Path.Combine(folder, "frames");
                Directory.CreateDirectory(frames);
                index = new StreamWriter(Path.Combine(frames, "frames.csv"), false, new UTF8Encoding(false));
                index.WriteLine("index,tick,alpha,mode,caption");
                target = new RenderTexture(Mathf.Max(640, Screen.width), Mathf.Max(360, Screen.height), 24, RenderTextureFormat.ARGB32);
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) canvas.enabled = false;
                match.Rig.MinimumZoom = 2f; match.Rig.MaximumZoom = 40f;

                if (!world.TryGetBuilding(GateId, out var gate) || !world.TryGetBuilding(WallB, out var wallB) || !world.TryGetBuilding(WallC, out var wallC))
                { Debug.LogError("EMBERFIELD_SIEGE_FILM the fortress did not spawn."); IsRunning = false; SafeQuit.Request(1); yield break; }
                wallBId = WallB; wallCId = WallC;
                int outward = SiegeFilm.OutwardZ;
                var wallCentre = DefinitionLoader.ToWorld(gate.Position);
                var wallBWorld = DefinitionLoader.ToWorld(wallB.Position);
                var wallCWorld = DefinitionLoader.ToWorld(wallC.Position);
                // The sun is fixed at Euler(50, -25, 0) for every match; this angle was proven, on the same sun, to
                // light a wall's outer face when that face looks toward +Z (SiegeShowcase's ClimbYaw). Mirror it by
                // 180 degrees when this fortress's outer face looks the other way instead.
                faceYaw = outward > 0 ? 128f : 308f;

                // 1. The fortress, complete: a slow orbit outside the wall, gate, tower and the buildings behind it.
                // faceYaw - 40 looked almost along the wall's own run (edge-on) from far back; turning 15 degrees
                // past faceYaw itself, toward square-on, gives a frontal three-quarter view of the lit face, closer,
                // with the gate, the tower over it and the buildings behind all read from the front rather than end-on.
                foreach (int id in DefenderIds) Order(new BoardWallCommand(Defender, new[] { id }, WallForDefender(id)));
                Shot("La fortaleza orca", () => wallCentre, 8f, 50, faceYaw + 15, 6);
                yield return Roll(120);

                // 2. The elves advance with their ladders.
                for (int i = 0; i < LadderIds.Length; i++)
                {
                    int wallX = i == 0 ? wallB.Position.X : wallC.Position.X;
                    var approach = new SimPoint(wallX, gate.Position.Z + outward * 1100);
                    if (world.TryGetUnit(LadderIds[i], out _)) Order(new MoveCommand(Attacker, new[] { LadderIds[i] }, approach, MovementFormation.Loose));
                }
                for (int i = 0; i < AttackerIds.Length; i++)
                {
                    if (!world.TryGetUnit(AttackerIds[i], out _)) continue;
                    int laneWall = i < 2 ? 0 : 1;
                    int wallX = laneWall == 0 ? wallB.Position.X : wallC.Position.X;
                    int slot = i < 2 ? i : i - 2;
                    var mark = new SimPoint(wallX - 900 + slot * 900, gate.Position.Z + outward * 1300);
                    Order(new MoveCommand(Attacker, new[] { AttackerIds[i] }, mark, MovementFormation.Loose));
                }
                Shot("Los elfos avanzan con sus escaleras", AttackerMiddle, 9, 42, faceYaw + 30, 0);
                yield return Roll(120);

                // 3. Close as the ladders settle against the palisade.
                Shot("Las escaleras contra la empalizada", () => wallBWorld, 4.5f, faceYawPitch, faceYaw, 0);
                yield return WaitIdle(LadderIds, 60);
                // A ladder that is still walking is not equipment yet; stop it explicitly once it has arrived, the
                // same caution SiegeShowcase takes before sending anyone up.
                Order(new StopCommand(Attacker, LadderIds));
                yield return Roll(60);

                // 4. Close on the climbers, going up the rungs.
                var climbersB = new List<int> { AttackerIds[0], AttackerIds[1] };
                var climbersC = new List<int> { AttackerIds[2], AttackerIds[3], AttackerIds[4] };
                Shot("Suben por las escaleras", () => wallCWorld, 4f, faceYawPitch, faceYaw, 0);
                // Retries every soldier still on the ground each attempt (Board is a no-op once one is boarding),
                // instead of stopping as soon as any single one gets accepted: that left whichever of the five
                // hadn't quite arrived within range on the attempt something else succeeded on stranded on the
                // ground for the rest of the film, standing in for the ordinary impact flash to show unmoderated.
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    bool pending = false;
                    foreach (int id in climbersB) { Board(id, wallBId, LadderIds[0]); if (Grounded(id)) pending = true; }
                    foreach (int id in climbersC) { Board(id, wallCId, LadderIds[1]); if (Grounded(id)) pending = true; }
                    if (!pending || attempt == 3) break;
                    yield return Roll(20);
                }
                yield return Roll(140);

                // 5. The fight on the wall walk.
                foreach (int id in climbersB) if (world.TryGetUnit(id, out var u) && u.WallId != 0) Order(new AttackCommand(Attacker, new[] { id }, NearestOnWall(id)));
                foreach (int id in climbersC) if (world.TryGetUnit(id, out var u) && u.WallId != 0) Order(new AttackCommand(Attacker, new[] { id }, NearestOnWall(id)));
                foreach (int id in DefenderIds) if (world.TryGetUnit(id, out var u) && u.WallId != 0) Order(new AttackCommand(Defender, new[] { id }, NearestOnWall(id)));
                Shot("Combate en el adarve", () => (wallBWorld + wallCWorld) / 2f, 5.5f, 40, faceYaw - 20, 10);
                yield return Roll(100);

                // 6. The assault, wide.
                Shot("El asalto", () => wallCentre, 15, 52, faceYaw + 60, 4);
                yield return Roll(80);

                index.Flush(); index.Dispose();
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
                Debug.Log("EMBERFIELD_SIEGE_FILM frames=" + count + " ticks=" + world.TickIndex + " " + folder);
                IsRunning = false;
                SafeQuit.Request(0);
            }

            private const float faceYawPitch = 42;

            private int WallForDefender(int id) => id == DefenderIds[2] ? wallCId : wallBId;

            private bool Board(int id, int wallId, int ladderId)
            {
                if (!world.TryGetUnit(id, out var unit) || unit.WallId != 0 || unit.BoardingWallId != 0) return false;
                return Order(new BoardWallCommand(Attacker, new[] { id }, wallId, ladderId)).Accepted;
            }

            // True while a soldier is neither on the wall nor already boarding it: still worth another Board().
            private bool Grounded(int id) => world.TryGetUnit(id, out var unit) && unit.WallId == 0 && unit.BoardingWallId == 0;

            private int NearestOnWall(int fromId)
            {
                if (!world.TryGetUnit(fromId, out var from)) return 0;
                int best = 0; long nearest = long.MaxValue;
                foreach (var unit in world.Units)
                {
                    if (unit.WallId == 0 || unit.OwnerId == from.OwnerId) continue;
                    long dx = unit.Position.X - from.Position.X, dz = unit.Position.Z - from.Position.Z;
                    long distance = dx * dx + dz * dz;
                    if (distance < nearest) { nearest = distance; best = unit.Id; }
                }
                return best;
            }

            private Func<Vector3> AttackerMiddle => () =>
            {
                Vector3 sum = Vector3.zero; int n = 0;
                foreach (int id in AttackerIds) if (world.TryGetUnit(id, out var u)) { sum += DefinitionLoader.ToWorld(u.Position); n++; }
                foreach (int id in LadderIds) if (world.TryGetUnit(id, out var u)) { sum += DefinitionLoader.ToWorld(u.Position); n++; }
                return n > 0 ? sum / n : focus;
            };

            private CommandResult Order(IGameCommand command)
            {
                var result = world.Submit(command);
                if (!result.Accepted) Debug.LogWarning("EMBERFIELD_SIEGE_FILM order refused: " + result.Message);
                return result;
            }

            private void Shot(string name, Func<Vector3> follow, float size, float tilt, float heading, float degreesPerSecond)
            {
                caption = name;
                subject = follow; zoom = size; pitch = tilt; yaw = heading; orbit = degreesPerSecond;
                focus = follow(); Apply();
                Debug.Log("EMBERFIELD_SIEGE_FILM shot=" + name + " tick=" + world.TickIndex + " focus=" + focus.ToString("F1") + " camera=" + match.Rig.Camera.transform.position.ToString("F1"));
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

            private IEnumerator WaitIdle(int[] ids, int maxTicks)
            {
                for (int i = 0; i < maxTicks; i++)
                {
                    bool moving = false;
                    foreach (int id in ids) if (world.TryGetUnit(id, out var u) && u.Order == UnitOrder.Moving) moving = true;
                    if (!moving) yield break;
                    yield return Tick();
                }
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
                index.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},siege,\"{3}\"", count, world.TickIndex, alpha, caption.Replace("\"", "'")));
                count++;
            }
        }
    }
}
