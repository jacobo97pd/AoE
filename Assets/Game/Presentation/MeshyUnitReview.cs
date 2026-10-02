using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// A debug fixture for judging Meshy units in the game itself: -emberfieldMeshyReview &lt;folder&gt; [faction] [map].
    /// Both armies line up face to face on a clear strip of amber_crossing, one row per player, each with its worker,
    /// spear, archer, rider and unique unit. The default faction, aven, meets a historical rival that shares its
    /// models, which is exactly the case owner colour has to settle. The simulation is held while a close, a medium and
    /// a play-distance still are taken of the formed rows, then let go; one more still follows the fight. Then it quits.
    /// On another map the rows, player 1's town and a wall-gate-wall stretch stand on the nearest clear ground around
    /// player 1's Hearth, and the walls get a still of their own. Ordinary play never touches it.
    ///
    /// -emberfieldReviewSkin &lt;store item&gt; (with the flag above) reviews a character skin instead: a mirror match on
    /// amber_crossing with just the skin's unit twice, side by side. Player 1 wears the skin for the stills (in memory,
    /// never saved), the rival's unit is the faction's own model, and three stills of the pair are taken.
    /// </summary>
    public static class MeshyUnitReview
    {
        public const string Flag = "-emberfieldMeshyReview";
        public const string SkinFlag = "-emberfieldReviewSkin";
        private static readonly string[] Roles = { "tender", "reedguard", "stringwarden", "strider" };
        private const int Row = 29500, RivalRow = 31500, FirstColumn = 19000, Spacing = 2000, FirstId = 600;
        // Player 1's town on amber_crossing, on sites the siege showcase proved clear; SceneryStills builds it too.
        internal static readonly (string, int, int)[] Town =
        {
            ("muster_hall", 21500, 23500), ("archive", 17500, 26500), ("beast_lodge", 18000, 22500), ("storeyard", 23000, 27000),
            ("shelter", 21000, 20000), ("shelter", 24000, 20000), ("watchtower", 27000, 20000), ("keep", 33000, 27000),
        };
        /// <summary>True from world creation until the stills of the formed rows are taken.</summary>
        public static bool HoldsSimulation { get; private set; }
        // Where the stills look, in world metres; set by CreateWorld. No walls are raised on amber_crossing.
        private static Vector3? armyCentre, townCentre, wallCentre;

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
            if (!Debug.isDebugBuild && !Application.isEditor) return false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return false;
            var skin = SkinUnderReview();
            if (skin != null) { world = CreateSkinWorld(skin); CosmeticLoadout.EquipTransientPreview(skin.id); }
            else world = CreateWorld(Argument(2) ?? "aven", Argument(3) ?? "amber_crossing");
            HoldsSimulation = Argument(1) != null;
            return true;
        }

        private static CosmeticCatalogItem SkinUnderReview()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, SkinFlag);
            var item = index >= 0 && index + 1 < args.Length ? CosmeticLoadout.Find(args[index + 1]) : null;
            return item != null && item.slot == CosmeticLoadout.CharacterSlot ? item : null;
        }

        /// <summary>
        /// The skin's faction against itself with one unit each, two metres apart on the strip the rows use: player 1's
        /// is the one that wears the skin, player 2's keeps the faction's default model.
        /// </summary>
        private static World CreateSkinWorld(CosmeticCatalogItem skin)
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(skin.factionId, VictoryMode.Conquest);
            armyCentre = townCentre = wallCentre = null;
            var map = baseline.Map;
            foreach (var seat in map.PlayerFactions) seat.FactionId = skin.factionId;
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            int x = FirstColumn + Spacing * 2;
            units.Add(new UnitSpawnDefinition { Id = FirstId, OwnerId = 1, DefinitionId = skin.targetId, Position = new SimPoint(x, Row) });
            units.Add(new UnitSpawnDefinition { Id = FirstId + 1, OwnerId = 2, DefinitionId = skin.targetId, Position = new SimPoint(x + Spacing, Row) });
            map.UnitSpawns = units.ToArray();
            armyCentre = new Vector3((x + Spacing / 2) * .001f, 0, Row * .001f);
            return new World(baseline.Definition, map);
        }

        /// <summary>The shipped offline match for <paramref name="faction"/>, plus both players' lines. Ids start at 600, player 1 first.</summary>
        public static World CreateWorld(string faction, string mapId = "amber_crossing")
        {
            var baseline = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, mapId);
            armyCentre = townCentre = wallCentre = null;
            // Another map has no sites proven clear by hand: the rows, the town and the walls take the nearest free ground.
            if (mapId != "amber_crossing") { Arrange(baseline); AddShoreScout(baseline.Map); return new World(baseline.Definition, baseline.Map); }
            AddShoreScout(baseline.Map);
            armyCentre = new Vector3((FirstColumn + Spacing * 2) * .001f, 0, (Row + RivalRow) * .0005f); townCentre = new Vector3(25, 0, 24);
            var map = baseline.Map;
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            int id = FirstId;
            foreach (var player in map.PlayerFactions)
            {
                string unique = null;
                foreach (var definition in baseline.Definition.Factions) if (definition.Id == player.FactionId) unique = definition.UniqueUnitId;
                var line = new List<string>(Roles);
                if (!string.IsNullOrEmpty(unique)) line.Add(unique);
                line.Add("siege_ladder");
                for (int i = 0; i < line.Count; i++)
                    units.Add(new UnitSpawnDefinition { Id = id++, OwnerId = player.PlayerId, DefinitionId = line[i],
                        Position = new SimPoint(FirstColumn + i * Spacing, player.PlayerId == 1 ? Row : RivalRow) });
            }
            map.UnitSpawns = units.ToArray();
            // Player 1's town, on sites the siege showcase already proved clear on this map.
            var buildings = new List<BuildingSpawnDefinition>(map.BuildingSpawns);
            foreach (var (definition, x, z) in Town)
                buildings.Add(new BuildingSpawnDefinition { Id = id++, OwnerId = 1, DefinitionId = definition, Position = new SimPoint(x, z) });
            map.BuildingSpawns = buildings.ToArray();
            return new World(baseline.Definition, map);
        }

        public const int ScoutId = 699;

        private static List<string> Line(World world, string factionId)
        {
            string unique = null;
            foreach (var definition in world.Definition.Factions) if (definition.Id == factionId) unique = definition.UniqueUnitId;
            var line = new List<string>(Roles);
            if (!string.IsNullOrEmpty(unique)) line.Add(unique);
            line.Add("siege_ladder");
            return line;
        }

        // Player 1's row (with the rival's two metres beyond), a wall-gate-wall stretch and the town, each on the
        // nearest ground around its Hearth that is walkable and clear of resources and of whatever was placed before it.
        private static void Arrange(World baseline)
        {
            var map = baseline.Map; int cell = map.CellSizeMillimetres;
            var taken = new HashSet<long>();
            long Key(int x, int z) => (long)z << 32 | (uint)x;
            foreach (var node in baseline.Resources)
                for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++) taken.Add(Key(node.Position.X / cell + dx, node.Position.Z / cell + dz));
            foreach (var unit in baseline.Units) taken.Add(Key(unit.Position.X / cell, unit.Position.Z / cell));
            bool Free(int left, int bottom, int width, int depth)
            {
                for (int x = left - 1; x <= left + width; x++)
                    for (int z = bottom - 1; z <= bottom + depth; z++)
                        if (x < 1 || z < 1 || x >= map.WidthCells - 1 || z >= map.HeightCells - 1 || taken.Contains(Key(x, z)) ||
                            !baseline.IsWalkable(new SimPoint(x * cell + cell / 2, z * cell + cell / 2))) return false;
                return true;
            }
            SimPoint home = default;
            foreach (var building in baseline.Buildings) if (building.OwnerId == 1 && building.DefinitionId == "hearth") home = building.Position;
            bool Place(int width, int depth, int nearest, out int left, out int bottom)
            {
                int cx = home.X / cell, cz = home.Z / cell;
                for (int ring = nearest; ring < 48; ring++)
                    for (int dz = -ring; dz <= ring; dz++)
                        for (int dx = -ring; dx <= ring; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                            left = cx + dx - width / 2; bottom = cz + dz - depth / 2;
                            if (!Free(left, bottom, width, depth)) continue;
                            for (int x = left; x < left + width; x++) for (int z = bottom; z < bottom + depth; z++) taken.Add(Key(x, z));
                            return true;
                        }
                left = bottom = 0; return false;
            }
            SimPoint Centre(int left, int bottom, int width, int depth) => new SimPoint(left * cell + width * cell / 2, bottom * cell + depth * cell / 2);

            var units = new List<UnitSpawnDefinition>(map.UnitSpawns);
            var buildings = new List<BuildingSpawnDefinition>(map.BuildingSpawns);
            int id = FirstId;
            var lines = new List<List<string>>();
            foreach (var player in map.PlayerFactions) lines.Add(Line(baseline, player.FactionId));
            int length = 0; foreach (var line in lines) length = Math.Max(length, line.Count);
            // Two metres between soldiers, as on amber_crossing; player 1's row, then its rival's two metres on.
            if (Place(length * 2, 3, 6, out int armyLeft, out int armyBottom))
            {
                for (int seat = 0; seat < lines.Count; seat++)
                    for (int i = 0; i < lines[seat].Count; i++)
                        units.Add(new UnitSpawnDefinition { Id = id++, OwnerId = map.PlayerFactions[seat].PlayerId, DefinitionId = lines[seat][i],
                            Position = new SimPoint((armyLeft + i * 2) * cell + cell / 2, (armyBottom + seat * 2) * cell + cell / 2) });
                armyCentre = DefinitionLoader.ToWorld(new SimPoint((armyLeft + length) * cell, (armyBottom + 1) * cell + cell / 2));
            }
            if (Place(8, 1, 8, out int wallLeft, out int wallBottom))
            {
                int z = wallBottom * cell + cell / 2;
                buildings.Add(new BuildingSpawnDefinition { Id = id++, OwnerId = 1, DefinitionId = "wall", Position = new SimPoint(wallLeft * cell + cell * 3 / 2, z) });
                buildings.Add(new BuildingSpawnDefinition { Id = id++, OwnerId = 1, DefinitionId = "gate", Position = new SimPoint((wallLeft + 4) * cell, z) });
                buildings.Add(new BuildingSpawnDefinition { Id = id++, OwnerId = 1, DefinitionId = "wall", Position = new SimPoint(wallLeft * cell + cell * 13 / 2, z) });
                wallCentre = DefinitionLoader.ToWorld(new SimPoint((wallLeft + 4) * cell, z));
            }
            var town = DefinitionLoader.ToWorld(home); int placed = 1;
            foreach (string definitionId in new[] { "keep", "beast_lodge", "muster_hall", "archive", "siege_workshop", "watchtower", "storeyard", "shelter", "shelter" })
            {
                BuildingDefinition definition = null;
                foreach (var candidate in baseline.Definition.Buildings) if (candidate.Id == definitionId) definition = candidate;
                if (definition == null || !Place(definition.WidthCells, definition.DepthCells, 4, out int left, out int bottom)) continue;
                var at = Centre(left, bottom, definition.WidthCells, definition.DepthCells);
                buildings.Add(new BuildingSpawnDefinition { Id = id++, OwnerId = 1, DefinitionId = definitionId, Position = at });
                town += DefinitionLoader.ToWorld(at); placed++;
            }
            townCentre = town / placed;
            map.UnitSpawns = units.ToArray(); map.BuildingSpawns = buildings.ToArray();
        }

        // A rider on the dry bank nearest player 1's hearth: its sight uncovers water for the shore still.
        private static void AddShoreScout(MapDefinition map)
        {
            if ((map.WaterCells?.Length ?? 0) == 0) return;
            var home = new SimPoint(0, 0);
            foreach (var building in map.BuildingSpawns) if (building.DefinitionId == "hearth" && building.OwnerId == 1) home = building.Position;
            var blocked = new HashSet<long>();
            foreach (var cell in map.BlockedCells) blocked.Add((long)cell.Z << 32 | (uint)cell.X);
            foreach (var cell in map.WaterCells) blocked.Add((long)cell.Z << 32 | (uint)cell.X);
            long best = long.MaxValue; SimPoint spot = default;
            foreach (var water in map.WaterCells)
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = water.X + dx, z = water.Z + dz;
                    if (x < 1 || z < 1 || x >= map.WidthCells - 1 || z >= map.HeightCells - 1 || blocked.Contains((long)z << 32 | (uint)x)) continue;
                    var point = new SimPoint(x * map.CellSizeMillimetres + map.CellSizeMillimetres / 2, z * map.CellSizeMillimetres + map.CellSizeMillimetres / 2);
                    long distance = (long)(point.X - home.X) * (point.X - home.X) + (long)(point.Z - home.Z) * (point.Z - home.Z);
                    if (distance < best) { best = distance; spot = point; }
                }
            if (best == long.MaxValue) return;
            var units = new List<UnitSpawnDefinition>(map.UnitSpawns) { new UnitSpawnDefinition { Id = ScoutId, OwnerId = 1, DefinitionId = "strider", Position = spot } };
            map.UnitSpawns = units.ToArray();
        }

        public static void TryStartCapture(MatchController match)
        {
            string folder = Argument(1);
            if (folder == null || !(Debug.isDebugBuild || Application.isEditor)) return;
            Application.runInBackground = true;
            match.StartCoroutine(Capture(match, folder));
        }

        private static IEnumerator Capture(MatchController match, string folder)
        {
            Directory.CreateDirectory(folder);
            yield return null;
            while (!Application.isBatchMode && !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            var centre = armyCentre ?? new Vector3((FirstColumn + Spacing * 2) * .001f, 0, (Row + RivalRow) * .0005f);
            if (armyCentre == null)
                foreach (var building in match.World.Buildings)
                    if (building.DefinitionId == "hearth" && building.OwnerId == 1) centre = DefinitionLoader.ToWorld(building.Position) + new Vector3(6, 0, 6);
            match.ClearSelection();
            bool ok = true;
            match.Rig.MinimumZoom = 2.2f;
            if (SkinUnderReview() is CosmeticCatalogItem skin)
            {
                foreach (var (name, zoom) in new[] { ("pair-close", 2.2f), ("pair-near", 3.4f), ("pair-play", 8f) })
                {
                    match.Rig.SetHome(centre, zoom);
                    match.SyncPresentation(1);
                    yield return new WaitForEndOfFrame();
                    ok &= PlayerSmoke.Capture(match, Path.Combine(folder, skin.id + "-" + name + ".png"));
                }
                Debug.Log("EMBERFIELD_MESHY_REVIEW " + ok + " " + folder);
                if (!Application.isEditor) Application.Quit(ok ? 0 : 1);
                yield break;
            }
            foreach (var (name, zoom) in new[] { ("close", 2.6f), ("medium", 5f), ("play", 8f) })
            {
                match.Rig.SetHome(centre, zoom);
                match.SyncPresentation(1);
                yield return new WaitForEndOfFrame();
                ok &= PlayerSmoke.Capture(match, Path.Combine(folder, "meshy-" + name + ".png"));
            }
            if (match.World.TryGetUnit(ScoutId, out var scout))
            {
                match.Rig.SetHome(DefinitionLoader.ToWorld(scout.Position), 5f);
                match.SyncPresentation(1);
                yield return new WaitForEndOfFrame();
                ok &= PlayerSmoke.Capture(match, Path.Combine(folder, "meshy-water.png"));
            }
            match.Rig.SetHome(townCentre ?? new Vector3(25, 0, 24), 9f);
            match.SyncPresentation(1);
            yield return new WaitForEndOfFrame();
            ok &= PlayerSmoke.Capture(match, Path.Combine(folder, "meshy-town.png"));
            if (wallCentre != null)
            {
                match.Rig.SetHome(wallCentre.Value, 4.2f);
                match.SyncPresentation(1);
                yield return new WaitForEndOfFrame();
                ok &= PlayerSmoke.Capture(match, Path.Combine(folder, "meshy-walls.png"));
            }
            HoldsSimulation = false;
            // The fight drifts from where the rows stood, so the camera follows the local spear into it.
            float until = Time.realtimeSinceStartup + 3.5f;
            while (Time.realtimeSinceStartup < until)
            {
                if (match.World.TryGetUnit(FirstId + 1, out var spear)) match.Rig.SetHome(DefinitionLoader.ToWorld(spear.Position), 3.4f);
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            ok &= PlayerSmoke.Capture(match, Path.Combine(folder, "meshy-fight.png"));
            Debug.Log("EMBERFIELD_MESHY_REVIEW " + ok + " " + folder);
            if (!Application.isEditor) Application.Quit(ok ? 0 : 1);
        }
    }
}
