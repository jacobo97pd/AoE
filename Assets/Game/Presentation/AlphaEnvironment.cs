using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    /// <summary>Deterministic cosmetic landscape from public map geometry. Decoration never enters entity picking or simulation occupancy.</summary>
    public sealed class AlphaEnvironment : IDisposable
    {
        // Shown: whether Sync last left it active (-1 before the first), so only a change reaches the engine.
        private sealed class Decoration { internal Transform Root; internal SimPoint Anchor; internal int Shown = -1; }
        private static readonly Dictionary<string, Mesh> Shared = new Dictionary<string, Mesh>();
        private readonly List<Decoration> decorations = new List<Decoration>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly World world;
        private Material water;
        // A peak's model is drawn a fifth larger than it was made, so it covers the footing WorldMapBaker blocks for it.
        public const float PeakScale = 1.2f;
        // WorldMapBaker pads a land monument 2 cells each way and lays a peak 6 by 3 cells each way along its map edge;
        // a peak's model covers its footing to about this far from its centre.
        private const int MonumentPad = 2, PeakLength = 6, PeakWidth = 3;
        private const float PeakCover = 5.6f;
        private long revision = -1;
        private readonly bool legacyStream;
        public Transform Root { get; }
        public int DecorationCount => decorations.Count;
        public bool HasStream { get; }

        public AlphaEnvironment(World world, Transform parent)
        {
            this.world = world;
            Root = new GameObject("Alpha landscape dressing · public geometry").transform; Root.SetParent(parent, false);
            float width = world.Map.WidthCells * world.Map.CellSizeMillimetres * .001f, depth = world.Map.HeightCells * world.Map.CellSizeMillimetres * .001f;
            bool authored = (world.Map.WaterCells?.Length ?? 0) > 0;
            legacyStream = !authored && world.Map.Id == "amber_crossing" && width >= 80 && depth >= 60;
            HasStream = authored || legacyStream;
            if (legacyStream) { BuildStream(width, depth); BuildFords(width, depth); }
            if (authored || world.Map.BiomeId == "caribbean" || world.Map.BiomeId == "desert") BuildBiomeWater(width, depth);
            BuildLandmarks(width, depth);
            var footprints = LandmarkFootprints(world);
            for (int z = 2; z < depth - 2; z += 3) for (int x = 2; x < width - 2; x += 3)
            {
                uint seed = Hash(x, z); float chance = (seed & 1023) / 1023f;
                if (chance > .65f || PathDistance(world.Map, x, z) < 2.8f || IsWaterCell(world.Map, x, z)) continue;
                float bank = legacyStream ? Mathf.Abs(x - StreamCenter(z, width, depth)) : authored ? WaterDistanceCells(world.Map, x, z) : 99;
                if (bank < 2.1f) continue;
                float px = x + ((seed >> 10 & 255) / 255f - .5f), pz = z + ((seed >> 18 & 255) / 255f - .5f);
                string kind = Tuft(world.Lands.BiomeAt(new SimPoint(Mathf.RoundToInt(px * 1000), Mathf.RoundToInt(pz * 1000))), bank, chance, world.Map.BiomeId);
                if (kind == null || footprints.Count > 0 && footprints.ContainsKey(CellIndex(world.Map, px, pz))) continue;
                AddTuft(kind, px, pz, seed, chance);
            }
            if (world.Lands.HasLands) DressLands(footprints);
            Sync();
        }

        // What grows between the stands: reeds on a bank, then the land's own tufts. The elven forest flowers, the highland
        // has heather in its grass, and the volcanic waste keeps sparse ash-grey tufts and nothing on a lava bank. A map
        // without lands only ever reaches the last line.
        private static string Tuft(string land, float bank, float chance, string mapBiome)
        {
            if (land == MapLands.Volcanic) return bank < 3.9f || chance > .24f ? null : "ash_tuft";
            if (bank < 3.9f) return "reeds";
            if (land == MapLands.Elven) return chance < .26f ? "flowers" : "grass";
            if (land == MapLands.Highland) return chance < .16f ? "heather" : "grass";
            return mapBiome == "desert" ? "dry_grass" : "grass";
        }

        private void AddTuft(string kind, float px, float pz, uint seed, float chance)
        {
            var prop = Create(kind, Root); prop.localPosition = new Vector3(px, 0, pz);
            prop.localRotation = Quaternion.Euler(0, seed % 360, 0); prop.localScale = Vector3.one * (.75f + chance * .65f);
            decorations.Add(new Decoration { Root = prop, Anchor = new SimPoint(Mathf.RoundToInt(px * 1000), Mathf.RoundToInt(pz * 1000)) });
        }

        private static int CellIndex(MapDefinition map, float x, float z)
        {
            float metres = map.CellSizeMillimetres * .001f;
            return Mathf.Clamp(Mathf.FloorToInt(z / metres), 0, map.HeightCells - 1) * map.WidthCells + Mathf.Clamp(Mathf.FloorToInt(x / metres), 0, map.WidthCells - 1);
        }

        /// <summary>
        /// The cells under a land's monument or peak (land slots only; other maps have none), each with the biome whose
        /// rocks fill it, or null where no filler may grow. WorldMapBaker blocks a monument's pad and a peak's footing, and
        /// the fillers drawn there used to put pines inside the stone circle: the model stands alone on its pad, and the
        /// ends of a peak's footing that its model leaves bare take the peak's own rocks, scree at the mountain's foot.
        /// </summary>
        public static Dictionary<int, string> LandmarkFootprints(World world)
        {
            var cells = new Dictionary<int, string>();
            var map = world.Map;
            float metres = map.CellSizeMillimetres * .001f;
            foreach (var landmark in map.Landmarks ?? Array.Empty<MapLandmarkDefinition>())
            {
                if (landmark == null) continue;
                bool peak = landmark.Kind == MapLands.PeakSlot;
                if (!peak && landmark.Kind != MapLands.MonumentSlot) continue;
                var centre = new Vector2(landmark.Position.X, landmark.Position.Z) * .001f;
                int cx = Mathf.FloorToInt(centre.x / metres), cz = Mathf.FloorToInt(centre.y / metres);
                bool alongX = landmark.RotationDegrees % 180 == 0;
                int reachX = peak ? (alongX ? PeakLength : PeakWidth) : MonumentPad, reachZ = peak ? (alongX ? PeakWidth : PeakLength) : MonumentPad;
                string kind = world.Lands.LandmarkKind(landmark);
                string rocks = MapLands.LandmarkBiome(kind, world.Lands.BiomeAt(landmark.Position));
                for (int dz = -reachZ; dz <= reachZ; dz++) for (int dx = -reachX; dx <= reachX; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= map.WidthCells || z >= map.HeightCells) continue;
                    bool covered = !peak || Vector2.Distance(new Vector2((x + .5f) * metres, (z + .5f) * metres), centre) <= PeakCover;
                    cells[z * map.WidthCells + x] = covered ? null : rocks;
                }
            }
            return cells;
        }

        // The finishing touches of a map with lands: the elven forest is denser in flowers than the lattice alone gives,
        // and glimmers drift at the edges of its stands.
        private void DressLands(Dictionary<int, string> footprints)
        {
            var map = world.Map;
            float metres = map.CellSizeMillimetres * .001f, width = map.WidthCells * metres, depth = map.HeightCells * metres;
            var blocked = new HashSet<int>(); foreach (var c in map.BlockedCells) blocked.Add(c.Z * map.WidthCells + c.X);
            var glimmers = new List<Vector3>();
            for (float z = 3.5f; z < depth - 3; z += 3) for (float x = 3.5f; x < width - 3; x += 3)
            {
                int cx = Mathf.FloorToInt(x / metres), cz = Mathf.FloorToInt(z / metres);
                if (world.Lands.BiomeAt(cx, cz) != MapLands.Elven) continue;
                uint seed = Hash(cx + 7919, cz + 104729); float chance = (seed & 1023) / 1023f;
                int index = cz * map.WidthCells + cx;
                if (blocked.Contains(index) || IsWaterCell(map, cx, cz) || footprints.ContainsKey(index) || PathDistance(map, x, z) < 2.8f) continue;
                bool edge = blocked.Contains(index - 1) || blocked.Contains(index + 1) || blocked.Contains(index - map.WidthCells) || blocked.Contains(index + map.WidthCells);
                if (edge || chance < .2f) glimmers.Add(new Vector3(x, .25f, z));
                if (chance > .42f || WaterDistanceCells(map, x, z) < 3.9f) continue;
                AddTuft("flowers", x + ((seed >> 10 & 255) / 255f - .5f), z + ((seed >> 18 & 255) / 255f - .5f), seed, chance);
            }
            foreach (var (sources, centre) in LandMotes.Patches(glimmers))
                AddMotes(LandMotes.Kind.Glimmers, sources, Mathf.Clamp(sources.Count / 2, 6, 40), centre);
        }

        private void AddMotes(LandMotes.Kind kind, List<Vector3> sources, int count, Vector3 anchor)
        {
            var motes = LandMotes.Build(kind, Root, sources, count, Hash(Mathf.RoundToInt(anchor.x), Mathf.RoundToInt(anchor.z)) ^ (uint)kind, ownedMeshes);
            if (motes == null) return;
            decorations.Add(new Decoration { Root = motes.transform, Anchor = new SimPoint(Mathf.RoundToInt(anchor.x * 1000), Mathf.RoundToInt(anchor.z * 1000)) });
        }
        public static float PathDistance(MapDefinition map, float x, float z)
        {
            float width = map.WidthCells * map.CellSizeMillimetres * .001f, depth = map.HeightCells * map.CellSizeMillimetres * .001f;
            if (map.Id == "amber_crossing" || map.OfflineMatch == null) return PathDistance(x, z, width, depth);
            var a = new Vector2(width * .18f, depth * .25f); var b = new Vector2(width * .82f, depth * .75f);
            foreach (var spawn in map.BuildingSpawns)
                if (spawn.DefinitionId == "hearth") { if (spawn.OwnerId == 1) a = new Vector2(spawn.Position.X, spawn.Position.Z) * .001f; else if (spawn.OwnerId == 2) b = new Vector2(spawn.Position.X, spawn.Position.Z) * .001f; }
            var p = new Vector2(x, z); float distance = Mathf.Min(Segment(p, a, b), Mathf.Min(Vector2.Distance(p, a) - 4.5f, Vector2.Distance(p, b) - 4.5f));
            foreach (var objective in map.OfflineMatch.Objectives)
            {
                var node = new Vector2(objective.Position.X, objective.Position.Z) * .001f;
                distance = Mathf.Min(distance, Mathf.Min(Segment(p, a, node), Segment(p, b, node)));
            }
            return distance;
        }
        private static MapDefinition waterMap;
        private static HashSet<int> waterLookup;
        private static byte[] waterDistance;
        private const byte FarFromWater = 12;
        // One flood fill per map gives both the water test and the distance to a shore, so banks, beaches
        // and oasis edges follow whatever the map authored instead of a formula per biome.
        private static void EnsureWaterLookup(MapDefinition map)
        {
            if (ReferenceEquals(map, waterMap)) return;
            waterMap = map;
            int width = map.WidthCells, height = map.HeightCells;
            waterLookup = new HashSet<int>();
            foreach (var cell in map.WaterCells ?? Array.Empty<GridCell>())
                if (cell.X >= 0 && cell.X < width && cell.Z >= 0 && cell.Z < height) waterLookup.Add(cell.Z * width + cell.X);
            waterDistance = new byte[width * height];
            for (int i = 0; i < waterDistance.Length; i++) waterDistance[i] = FarFromWater;
            var queue = new Queue<int>();
            foreach (int index in waterLookup) { waterDistance[index] = 0; queue.Enqueue(index); }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); int x = current % width, z = current / width;
                byte next = (byte)(waterDistance[current] + 1);
                if (next >= FarFromWater) continue;
                for (int side = 0; side < 4; side++)
                {
                    int nx = x + (side == 0 ? -1 : side == 1 ? 1 : 0), nz = z + (side == 2 ? -1 : side == 3 ? 1 : 0);
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                    int index = nz * width + nx;
                    if (waterDistance[index] <= next) continue;
                    waterDistance[index] = next; queue.Enqueue(index);
                }
            }
        }
        /// <summary>Cells to the nearest authored water, capped. Returns 99 for a map that authored none.</summary>
        public static float WaterDistanceCells(MapDefinition map, float x, float z)
        {
            if ((map.WaterCells?.Length ?? 0) == 0) return 99;
            EnsureWaterLookup(map);
            int cx = Mathf.Clamp(Mathf.FloorToInt(x), 0, map.WidthCells - 1), cz = Mathf.Clamp(Mathf.FloorToInt(z), 0, map.HeightCells - 1);
            return waterDistance[cz * map.WidthCells + cx];
        }
        public static bool IsWaterCell(MapDefinition map, int x, int z)
        {
            if ((map.WaterCells?.Length ?? 0) > 0)
            {
                EnsureWaterLookup(map);
                return waterLookup.Contains(z * map.WidthCells + x);
            }
            if (map.BiomeId == "caribbean")
            {
                if (z >= 0 && z <= 20 && x < 5 + Mathf.FloorToInt(3 * Mathf.Sin(Mathf.PI * z / 20))) return true;
                int mx = map.WidthCells - 1 - x, mz = map.HeightCells - 1 - z;
                return mz >= 0 && mz <= 20 && mx < 5 + Mathf.FloorToInt(3 * Mathf.Sin(Mathf.PI * mz / 20));
            }
            if (map.BiomeId == "desert")
            {
                int mx = map.WidthCells - 1 - x, mz = map.HeightCells - 1 - z;
                return x >= 40 && x < 45 && z >= 42 && z < 47 || mx >= 40 && mx < 45 && mz >= 42 && mz < 47;
            }
            return false;
        }
        private void BuildLandmarks(float width, float depth)
        {
            if ((world.Map.Landmarks?.Length ?? 0) > 0)
            {
                // A generated map ships its own scenery, so bridges, harbours and monuments stand where
                // the map put them rather than at coordinates written per biome down here.
                foreach (var landmark in world.Map.Landmarks)
                {
                    if (string.IsNullOrEmpty(landmark.Kind)) continue;
                    // A land slot becomes its land's own monument, drawn from the biome whose scenery has it.
                    string kind = world.Lands.LandmarkKind(landmark);
                    // Decorative quays have no navigation footprint and can cover a live hull or its
                    // boarding shore. Naval matches show the players' constructed docks and ships.
                    if (world.Map.RealmId == ContentRealms.Naval && (kind == "pier" || kind == "harbor" || kind == "ship")) continue;
                    var prop = Landmark(kind, new Vector3(landmark.Position.X * .001f, 0, landmark.Position.Z * .001f), landmark.RotationDegrees,
                        MapLands.LandmarkBiome(kind, world.Lands.BiomeAt(landmark.Position)));
                    if (landmark.ScalePermille > 0 && landmark.ScalePermille != 1000) prop.localScale = Vector3.one * (landmark.ScalePermille * .001f);
                    if (landmark.Kind == MapLands.PeakSlot) prop.localScale *= PeakScale;
                    // A volcano smokes and throws embers from its crater; both hide with it until its ground is explored.
                    if (kind == "volcano")
                    {
                        var crater = new[] { new Vector3(0, 4.7f, 0), new Vector3(.45f, 4.6f, .2f), new Vector3(-.35f, 4.6f, -.3f) };
                        LandMotes.Build(LandMotes.Kind.VolcanoSmoke, prop, crater, 12, Hash(landmark.Position.X, landmark.Position.Z), ownedMeshes);
                        LandMotes.Build(LandMotes.Kind.VolcanoEmbers, prop, crater, 44, Hash(landmark.Position.Z, landmark.Position.X), ownedMeshes);
                    }
                }
                return;
            }
            if (world.Map.BiomeId == "caribbean")
            {
                if (world.Map.RealmId != ContentRealms.Naval)
                {
                    Landmark("ship", new Vector3(2.3f, -.12f, 8.5f), 19);
                    Landmark("harbor", new Vector3(5.3f, 0, 12.6f), 0);
                    Landmark("ship", new Vector3(width - 2.3f, -.12f, depth - 8.5f), 199);
                    Landmark("harbor", new Vector3(width - 5.3f, 0, depth - 12.6f), 180);
                }
                Landmark("ruins", new Vector3(30, 0, 52), 18);
                Landmark("ruins", new Vector3(width - 30, 0, depth - 52), 198);
                foreach (var p in new[] { new Vector3(1.5f, 0, 17), new Vector3(3.2f, 0, 18), new Vector3(width - 1.5f, 0, depth - 17), new Vector3(width - 3.2f, 0, depth - 18) }) Landmark("palm", p, p.z * 11);
            }
            else if (world.Map.BiomeId == "desert")
            {
                Landmark("pyramid", new Vector3(28.5f, 0, 62.5f), 0);
                Landmark("pyramid", new Vector3(width - 28.5f, 0, depth - 62.5f), 180);
                foreach (var p in new[] { new Vector3(40.5f, 0, 42.5f), new Vector3(44.4f, 0, 46.4f), new Vector3(width - 40.5f, 0, depth - 42.5f), new Vector3(width - 44.4f, 0, depth - 46.4f) }) Landmark("date_palm", p, p.z * 11);
                Landmark("caravan", new Vector3(-3.5f, 0, 26), 115);
                Landmark("caravan", new Vector3(width + 3.5f, 0, depth - 26), 295);
                for (int z = 5; z < depth - 5; z += 12) { Landmark("dune", new Vector3(-4.5f, 0, z), z * 7); Landmark("dune", new Vector3(width + 4.5f, 0, depth - z), z * 7); }
            }
            else if (world.Map.Id == "amber_crossing")
            {
                Landmark("windmill", new Vector3(-3, 0, 21), 0);
                Landmark("windmill", new Vector3(width + 3, 0, depth - 21), 180);
                Landmark("village", new Vector3(-4.5f, 0, 12), 0);
                Landmark("village", new Vector3(width + 4.5f, 0, depth - 12), 180);
                for (int z = 3; z < depth - 3; z += 5) { Landmark("tree", new Vector3(-2.1f, 0, z), z * 17); Landmark("tree", new Vector3(width + 2.1f, 0, depth - z), z * 17); }
            }
        }
        private Transform Landmark(string kind, Vector3 position, float rotation, string biome = null)
        {
            // Landmarks the Meshy catalogue has come as a model; the rest, and the windmill with its turning sails, stay procedural.
            var prop = MeshyPropVisuals.TryLandmark(kind, biome ?? world.Map.BiomeId, Root) ?? Create(kind, Root);
            prop.name = "Landmark " + kind; prop.localPosition = position; prop.localRotation = Quaternion.Euler(0, rotation, 0);
            if (kind == "windmill")
            {
                // A mill whose sails never turn reads as a monument to a mill.
                var sails = Create("windmill_sails", prop);
                sails.localPosition = new Vector3(0, 2.91f, -.86f);
                sails.gameObject.AddComponent<TurningSails>();
            }
            float width = world.Map.WidthCells * world.Map.CellSizeMillimetres * .001f, depth = world.Map.HeightCells * world.Map.CellSizeMillimetres * .001f;
            decorations.Add(new Decoration { Root = prop, Anchor = new SimPoint(Mathf.RoundToInt(Mathf.Clamp(position.x, .5f, width -.5f) * 1000), Mathf.RoundToInt(Mathf.Clamp(position.z, .5f, depth -.5f) * 1000)) });
            return prop;
        }
        public static float StreamCenter(float z, float width, float depth) => width * .5f - Mathf.Sin(z / depth * Mathf.PI * 2) * 2.4f;
        public static float PathDistance(float x, float z, float width, float depth)
        {
            var p = new Vector2(x, z);
            float main = Segment(p, new Vector2(width * .1875f, depth * .25f), new Vector2(width * .8125f, depth * .75f));
            float firstCourt = Vector2.Distance(p, new Vector2(width * .1875f, depth * .25f)) - 4.5f;
            float secondCourt = Vector2.Distance(p, new Vector2(width * .8125f, depth * .75f)) - 4.5f;
            float branches = Mathf.Min(Segment(p, new Vector2(width * .3125f, depth * .6111f), new Vector2(width * .5f, depth * .5f)),
                Segment(p, new Vector2(width * .6875f, depth * .3889f), new Vector2(width * .5f, depth * .5f)));
            return Mathf.Min(main, Mathf.Min(branches, Mathf.Min(firstCourt, secondCourt)));
        }
        private static float Segment(Vector2 p, Vector2 a, Vector2 b) { var d = b - a; return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude)); }
        private static uint Hash(int x, int z) { unchecked { uint v = (uint)(x * 73856093 ^ z * 19349663 ^ 83492791); v ^= v >> 13; v *= 1274126177; return v ^ v >> 16; } }

        public void Sync()
        {
            long next = world.Vision?.Revision ?? 0; if (next == revision) return; revision = next;
            foreach (var item in decorations)
            {
                int shown = world.Vision == null || world.Vision.IsExplored(1, item.Anchor) ? 1 : 0;
                if (shown != item.Shown) { item.Shown = shown; item.Root.gameObject.SetActive(shown == 1); }
            }
        }
        public static Transform CreateObstacle(Transform parent, int x, int z, string biomeId = "forest", bool rocksOnly = false)
        {
            uint seed = Hash(x, z);
            // Impassable ground reads as what the biome is made of: a thicket in the woods, a palm on the
            // coast, sandstone in the basin, with outcrops between them so a stand never looks stamped.
            // The lands mix their own: the elven forest is mostly trees, the highland a third rock, and the
            // volcanic waste more rock than dead wood.
            string kind = rocksOnly ? "obstacle"
                : biomeId == "desert" ? "desert_rock"
                : biomeId == "caribbean" ? (seed % 3 == 0 ? "palm" : "coastal_rock")
                : biomeId == MapLands.Volcanic ? (seed % 20 < 11 ? "obstacle" : "thicket")
                : biomeId == MapLands.Highland ? (seed % 3 == 0 ? "obstacle" : "thicket")
                : biomeId == MapLands.Elven ? (seed % 6 == 0 ? "obstacle" : "thicket")
                : seed % 4 == 0 ? "obstacle" : "thicket";
            // A Meshy model of the same kind for this biome takes the cell when the catalogue has one.
            Transform rock;
            if (MeshyPropVisuals.TryObstacle(kind, biomeId, seed, out var mesh, out var far, out var material))
            {
                rock = Create(kind, parent, mesh, material);
                // On a phone tier the filler's one renderer swaps to the model's far mesh, with every other copy of
                // the model, when the zoom crosses the model's switch. A desktop never reaches it at a game zoom and
                // keeps the near mesh: a LOD group and a second renderer on each of Amber Crossing's 2,500 fillers
                // cost it 0.4 ms a frame for nothing, and on a phone the group's per-frame work bought nothing a zoom
                // check does not.
                if (far != null) SceneryDetail.RegisterFiller(rock.GetComponent<MeshFilter>(), mesh, far);
            }
            else rock = Create(kind, parent);
            // The low phone tier drops the fillers' shadows: on a forest map they were most of the shadow casters.
            if (!MobileQuality.SceneryShadows) foreach (var renderer in rock.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            // Free rotation, uneven scale and a nudge off centre: on a grid of impassable cells the eye
            // picks out repetition immediately, and a stamped forest is what gives a generated map away.
            rock.localRotation = Quaternion.Euler(0, (seed >> 3 & 1023) * .3516f, 0);
            float height = .74f + (seed & 255) / 255f * .62f, spread = .82f + (seed >> 8 & 127) / 127f * .34f;
            rock.localScale = new Vector3(spread, height, spread * (.92f + (seed >> 15 & 63) / 63f * .17f));
            rock.localPosition += new Vector3(((seed >> 21 & 63) / 63f - .5f) * .34f, 0, ((seed >> 26 & 31) / 31f - .5f) * .34f);
            return rock;
        }
        private static Transform Create(string kind, Transform parent)
        {
            if (!Shared.TryGetValue(kind, out var mesh)) { mesh = Geometry(kind); Shared.Add(kind, mesh); }
            return Create(kind, parent, mesh, ArtKit.Catalog.Material);
        }
        private static Transform Create(string kind, Transform parent, Mesh mesh, Material material)
        {
            var root = new GameObject("Landscape " + kind, typeof(MeshFilter), typeof(MeshRenderer)).transform; root.SetParent(parent, false);
            root.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = IsTuft(kind) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            // The landscape stands still. A whole map breathing at once read as restless rather than alive, and
            // it stole the eye from the things that matter. The only vegetation that moves now is a tree with
            // somebody's axe in it -- see WorldView, which drives the same _Sway on the resource being worked.
            return root;
        }
        /// <summary>The scattered tufts: no shadow, one shared instanced mesh per kind.</summary>
        public static bool IsTuft(string kind) =>
            kind == "grass" || kind == "reeds" || kind == "dry_grass" || kind == "flowers" || kind == "heather" || kind == "ash_tuft";
        private static Mesh Geometry(string kind)
        {
            if (kind != "tree" && kind != "bush" && kind != "obstacle" && kind != "thicket" && (!IsTuft(kind) || kind == "dry_grass")) return AlphaBiomeGeometry.Prop(kind);
            var b = new AlphaMeshBuilder(); var trunk = new Color(.28f, .21f, .13f);
            if (kind == "thicket")
            {
                foreach (var stand in new[] { new Vector3(-.22f, 0, .16f), new Vector3(.26f, 0, -.19f), new Vector3(.04f, 0, .34f) })
                {
                    float height = 1.55f + Mathf.Abs(stand.x) * 1.9f;
                    b.Frustum(stand, stand + new Vector3(.03f, height, .02f), new Vector2(.12f, .11f), new Vector2(.07f, .06f), 5, trunk);
                    b.Rock(stand + new Vector3(-.16f, height + .22f, .06f), new Vector3(.92f, .86f, .88f), 6, new Color(.22f, .37f, .22f));
                    b.Rock(stand + new Vector3(.17f, height + .48f, -.08f), new Vector3(.86f, .78f, .8f), 6, new Color(.33f, .45f, .24f));
                }
                b.Rock(new Vector3(-.3f, .19f, -.3f), new Vector3(.62f, .38f, .55f), 5, new Color(.29f, .41f, .22f));
                b.Rock(new Vector3(.33f, .15f, .3f), new Vector3(.48f, .3f, .44f), 5, new Color(.43f, .5f, .26f));
            }
            else if (kind == "obstacle")
            {
                b.Rock(new Vector3(-.13f, .40f, .03f), new Vector3(.68f, .80f, .77f), 7, new Color(.53f, .57f, .51f));
                b.Rock(new Vector3(.23f, .26f, -.17f), new Vector3(.42f, .52f, .45f), 5, new Color(.68f, .68f, .55f));
                b.Rock(new Vector3(-.11f, .54f, .05f), new Vector3(.48f, .09f, .48f), 5, new Color(.34f, .45f, .24f));
            }
            else if (kind == "tree")
            {
                b.Frustum(Vector3.zero, new Vector3(.04f, 2.5f, .02f), new Vector2(.20f, .17f), new Vector2(.10f, .09f), 7, trunk);
                for (int side = -1; side <= 1; side += 2) b.Beam(new Vector3(0, 1.4f, 0), new Vector3(side * .77f, 2.54f, side * .31f), .13f, .15f, trunk);
                b.Rock(new Vector3(-.46f, 2.56f, .14f), new Vector3(1.55f, 1.54f, 1.51f), 7, new Color(.24f, .39f, .23f));
                b.Rock(new Vector3(.46f, 2.90f, -.19f), new Vector3(1.64f, 1.48f, 1.42f), 7, new Color(.39f, .48f, .25f));
                b.Rock(new Vector3(.0f, 3.48f, .08f), new Vector3(1.31f, 1.10f, 1.25f), 6, new Color(.49f, .57f, .28f));
            }
            else if (kind == "bush")
            {
                b.Rock(new Vector3(-.16f, .20f, .04f), new Vector3(.65f, .40f, .57f), 6, new Color(.35f, .47f, .23f));
                b.Rock(new Vector3(.23f, .16f, -.1f), new Vector3(.51f, .32f, .46f), 5, new Color(.49f, .55f, .28f));
            }
            else
            {
                bool reeds = kind == "reeds", flowers = kind == "flowers", heather = kind == "heather", ash = kind == "ash_tuft";
                // The lands' tufts: an elven tuft is a lusher green with blossoms at its tips, heather is short with mauve
                // tips, and an ash tuft is a few dry grey-brown blades.
                var blossoms = new[] { new Color(.96f, .94f, .86f), new Color(.98f, .84f, .42f), new Color(.80f, .68f, .95f), new Color(.72f, .95f, .88f) };
                for (int n = 0; n < (reeds ? 7 : ash ? 6 : 9); n++)
                {
                    float angle = n * 2.39996f, radius = .12f + n % 3 * .08f;
                    var p = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    float h = reeds ? .42f + n % 3 * .16f : heather ? .12f + n % 3 * .04f : ash ? .11f + n % 3 * .05f : .16f + n % 4 * .055f;
                    var tip = p + new Vector3(Mathf.Cos(angle) * .11f, h, Mathf.Sin(angle) * .11f);
                    var color = flowers ? (n % 2 == 0 ? new Color(.30f, .55f, .30f) : new Color(.48f, .66f, .34f))
                        : heather ? (n % 3 == 0 ? new Color(.36f, .42f, .26f) : n % 2 == 0 ? new Color(.58f, .36f, .56f) : new Color(.68f, .46f, .64f))
                        : ash ? (n % 2 == 0 ? new Color(.36f, .33f, .30f) : new Color(.50f, .42f, .34f))
                        : n % 2 == 0 ? new Color(.42f, .52f, .25f) : new Color(.64f, .62f, .31f);
                    b.Triangle(p - Vector3.right * .032f, tip, p + Vector3.right * .032f, color);
                    b.Triangle(p - Vector3.forward * .032f, p + Vector3.forward * .032f, tip, color);
                    if (reeds) b.Frustum(tip - Vector3.up * .12f, tip + Vector3.up * .08f, new Vector2(.026f, .026f), new Vector2(.018f, .018f), 4, trunk);
                    if (flowers && n % 2 == 1)
                    {
                        // A flat four-petal blossom facing up, which is how the battlefield camera sees it.
                        float s = .055f; var bloom = blossoms[n / 2 % blossoms.Length]; var top = tip + Vector3.up * .01f;
                        b.Triangle(top - Vector3.right * s, top + Vector3.forward * s, top + Vector3.right * s, bloom);
                        b.Triangle(top + Vector3.right * s, top - Vector3.forward * s, top - Vector3.right * s, bloom);
                    }
                }
            }
            return b.Mesh("Alpha landscape " + kind);
        }
        private void BuildStream(float width, float depth)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var indices = new List<int>();
            var blocked = new HashSet<int>(); foreach (var c in world.Map.BlockedCells) blocked.Add(c.Z * world.Map.WidthCells + c.X);
            for (int z = 0; z <= Mathf.CeilToInt(depth); z++)
            {
                float pz = Mathf.Min(z, depth), cx = StreamCenter(pz, width, depth); bool deep = false;
                float cell = world.Map.CellSizeMillimetres * .001f;
                int row = Mathf.Clamp(Mathf.FloorToInt(pz / cell), 0, world.Map.HeightCells - 1);
                for (int x = Mathf.FloorToInt((cx - 1.5f) / cell); x <= Mathf.CeilToInt((cx + 1.5f) / cell); x++) deep |= blocked.Contains(row * world.Map.WidthCells + x);
                float ford = Mathf.Clamp01(PathDistance(cx, pz, width, depth) / 3);
                float half = Mathf.Lerp(1.15f, 1.85f, ford);
                vertices.Add(new Vector3(cx - half, -.024f, pz)); vertices.Add(new Vector3(cx + half, -.024f, pz));
                uv.Add(new Vector2(0, pz)); uv.Add(new Vector2(1, pz)); colors.Add(new Color(deep ? 1 : 0, 0, 0)); colors.Add(new Color(deep ? 1 : 0, 0, 0));
                if (z == 0) continue; int i = vertices.Count - 4;
                indices.Add(i); indices.Add(i + 2); indices.Add(i + 1); indices.Add(i + 1); indices.Add(i + 2); indices.Add(i + 3);
            }
            var mesh = new Mesh { name = "Amber shallow stream — legal fords, public blocked banks" }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            water = WaterMaterial("forest", "Amber living shallow water");
            var river = new GameObject("Shallow river and fords", typeof(MeshFilter), typeof(MeshRenderer)); river.transform.SetParent(Root, false);
            river.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = river.GetComponent<MeshRenderer>(); renderer.sharedMaterial = water; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }
        private void BuildBiomeWater(float width, float depth)
        {
            // An authored map floods exactly the cells it declares, which lets a river keep walkable
            // fords and bridges; older maps still flood the blocked cells their biome shape describes.
            var flooded = (world.Map.WaterCells?.Length ?? 0) > 0 ? world.Map.WaterCells : world.Map.BlockedCells;
            var pool = new HashSet<long>();
            foreach (var cell in flooded) if (IsWaterCell(world.Map, cell.X, cell.Z)) pool.Add((long)cell.Z << 32 | (uint)cell.X);
            if (!world.Lands.HasLands) { BuildWaterSheet(pool, world.Map.BiomeId, width, depth); return; }
            // Water belongs wholly to one land (WorldMapBaker keeps a pool and its banks inside it), so each land's water is
            // a sheet of its own: clear in the elven forest, cold in the highland, lava in a volcanic land.
            var lands = new Dictionary<string, HashSet<long>>();
            foreach (long key in pool)
            {
                string biome = world.Lands.BiomeAt((int)(uint)key, (int)(key >> 32));
                if (!lands.TryGetValue(biome, out var cells)) lands[biome] = cells = new HashSet<long>();
                cells.Add(key);
            }
            foreach (var land in lands) BuildWaterSheet(land.Value, land.Key, width, depth);
        }

        private void BuildWaterSheet(HashSet<long> pool, string biome, float width, float depth)
        {
            var b = new AlphaMeshBuilder();
            bool lava = biome == MapLands.Volcanic;
            bool Wet(int x, int z) => pool.Contains((long)z << 32 | (uint)x);
            // A shoreline drawn cell by cell steps like a staircase. Each grid corner is pulled toward the
            // water it touches, so an outer corner is cut off and the bank reads as a curve. Neighbouring
            // cells compute the same corner from the same four cells, which keeps the sheet watertight.
            Vector3 Corner(int gx, int gz)
            {
                int count = (Wet(gx - 1, gz - 1) ? 1 : 0) + (Wet(gx, gz - 1) ? 1 : 0) + (Wet(gx - 1, gz) ? 1 : 0) + (Wet(gx, gz) ? 1 : 0);
                float x = gx, z = gz;
                if (count == 1)
                {
                    float dx = (Wet(gx, gz - 1) || Wet(gx, gz) ? .34f : -.34f);
                    float dz = (Wet(gx - 1, gz) || Wet(gx, gz) ? .34f : -.34f);
                    x += dx; z += dz;
                }
                else if (count == 3)
                {
                    float dx = !Wet(gx, gz - 1) && !Wet(gx, gz) ? .14f : !Wet(gx - 1, gz - 1) && !Wet(gx - 1, gz) ? -.14f : 0;
                    float dz = !Wet(gx - 1, gz) && !Wet(gx, gz) ? .14f : !Wet(gx - 1, gz - 1) && !Wet(gx, gz - 1) ? -.14f : 0;
                    x -= dx; z -= dz;
                }
                return new Vector3(x, -.018f, z);
            }
            // Cells from the nearest bank, so the shader can shade shallows and foam where the water meets land.
            var shore = new Dictionary<long, int>();
            var frontier = new Queue<long>();
            foreach (long key in pool)
            {
                int x = (int)(uint)key, z = (int)(key >> 32);
                if (Wet(x - 1, z) && Wet(x + 1, z) && Wet(x, z - 1) && Wet(x, z + 1)) continue;
                shore[key] = 0; frontier.Enqueue(key);
            }
            while (frontier.Count > 0)
            {
                long key = frontier.Dequeue(); int x = (int)(uint)key, z = (int)(key >> 32); int next = shore[key] + 1;
                if (next > 4) continue;
                foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    long neighbour = (long)(z + step.Item2) << 32 | (uint)(x + step.Item1);
                    if (!pool.Contains(neighbour) || shore.ContainsKey(neighbour)) continue;
                    shore[neighbour] = next; frontier.Enqueue(neighbour);
                }
            }
            // Depth is sampled at the grid corners and interpolated across each cell, so the shallows fade
            // into open water instead of showing one flat tone per cell.
            Color Deepness(int gx, int gz)
            {
                float total = 0;
                foreach (var cell in new[] { (gx - 1, gz - 1), (gx, gz - 1), (gx - 1, gz), (gx, gz) })
                {
                    long key = (long)cell.Item2 << 32 | (uint)cell.Item1;
                    if (pool.Contains(key)) total += (shore.TryGetValue(key, out int distance) ? distance : 4) + 1;
                }
                return new Color(Mathf.Clamp01(total * .25f / 2.9f), lava ? Ford(gx, gz) : 0, 0);
            }
            // A ford is water the map leaves walkable. Lava crusts over it (vertex green, read by the lava shader); water
            // gets stepping stones.
            var walkable = new HashSet<long>();
            if (world.Lands.HasLands)
            {
                foreach (long key in pool) walkable.Add(key);
                foreach (var cell in world.Map.BlockedCells) walkable.Remove((long)cell.Z << 32 | (uint)cell.X);
            }
            float Ford(int gx, int gz)
            {
                int wet = 0, ford = 0;
                foreach (var cell in new[] { (gx - 1, gz - 1), (gx, gz - 1), (gx - 1, gz), (gx, gz) })
                {
                    long key = (long)cell.Item2 << 32 | (uint)cell.Item1;
                    if (!pool.Contains(key)) continue;
                    wet++; if (walkable.Contains(key)) ford++;
                }
                return wet == 0 ? 0 : ford / (float)wet;
            }
            foreach (long key in pool)
            {
                int x = (int)(uint)key, z = (int)(key >> 32);
                b.Quad(Corner(x, z), Corner(x, z + 1), Corner(x + 1, z + 1), Corner(x + 1, z),
                    Deepness(x, z), Deepness(x, z + 1), Deepness(x + 1, z + 1), Deepness(x + 1, z));
            }
            if (world.Lands.HasLands && walkable.Count > 0) BuildLandFord(walkable, biome, lava);
            if (lava) BuildLavaMotes(pool, shore);
            if (world.Map.BiomeId == "caribbean")
            {
                b.Quad(new Vector3(-18, -.018f, -10), new Vector3(-18, -.018f, depth + 10), new Vector3(0, -.018f, depth + 10), new Vector3(0, -.018f, -10), Color.red);
                b.Quad(new Vector3(width, -.018f, -10), new Vector3(width, -.018f, depth + 10), new Vector3(width + 18, -.018f, depth + 10), new Vector3(width + 18, -.018f, -10), Color.red);
            }
            var mesh = b.Mesh("Public " + biome + " navigation " + (lava ? "lava" : "water"));
            // Water uses world coordinates for UV; blocked cells define the exact shore, so visuals cannot invent crossings.
            var coordinates = new List<Vector2>(); foreach (var vertex in mesh.vertices) coordinates.Add(new Vector2(vertex.x * .31f, vertex.z)); mesh.SetUVs(0, coordinates);
            ownedMeshes.Add(mesh);
            var material = lava ? LavaMaterial() : null;
            if (material == null) material = WaterMaterial(biome, biome + " living water");
            if (water == null) water = material; else ownedMaterials.Add(material);
            var surface = new GameObject("Public " + biome + (lava ? " lava" : " water"), typeof(MeshFilter), typeof(MeshRenderer)); surface.transform.SetParent(Root, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = surface.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }

        // Stepping stones across a ford in water; a lava ford is crust drawn by the lava itself, with a few dark stones set
        // in it so that the crossing reads at a glance even where the crust is thin.
        private void BuildLandFord(HashSet<long> ford, string biome, bool lava)
        {
            var b = new AlphaMeshBuilder();
            var stone = lava ? new Color(.24f, .21f, .2f) : biome == MapLands.Elven ? new Color(.55f, .56f, .51f) : new Color(.47f, .48f, .46f);
            foreach (long key in ford)
            {
                int x = (int)(uint)key, z = (int)(key >> 32);
                uint seed = Hash(x * 3, z * 5);
                // One stone to most cells of a water ford, a path to step along; a scattering on lava, whose crust is the crossing.
                if ((seed & 3) == 0 || lava && (seed & 3) != 1) continue;
                float jitter = (seed >> 24 & 255) / 255f;
                var at = new Vector3(x + .3f + ((seed >> 8 & 255) / 255f) * .4f, -.004f, z + .3f + ((seed >> 16 & 255) / 255f) * .4f);
                b.Rock(at, new Vector3(.46f + jitter * .14f, .075f, .4f + jitter * .12f), 6, stone * (.88f + jitter * .22f));
            }
            var mesh = b.Mesh("Land ford stones"); ownedMeshes.Add(mesh);
            var holder = new GameObject("Public " + biome + " ford", typeof(MeshFilter), typeof(MeshRenderer)); holder.transform.SetParent(Root, false);
            holder.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = holder.GetComponent<MeshRenderer>(); renderer.sharedMaterial = ArtKit.Catalog.Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }

        // Embers over the open lava and a thin smoke over its wider reaches, one set per patch of the flow.
        private void BuildLavaMotes(HashSet<long> pool, Dictionary<long, int> shore)
        {
            float metres = world.Map.CellSizeMillimetres * .001f;
            var open = new List<Vector3>();
            foreach (long key in pool)
                if (!shore.TryGetValue(key, out int distance) || distance >= 1)
                    open.Add(new Vector3(((int)(uint)key + .5f) * metres, .05f, ((int)(key >> 32) + .5f) * metres));
            foreach (var (sources, centre) in LandMotes.Patches(open))
            {
                AddMotes(LandMotes.Kind.LavaEmbers, sources, Mathf.Clamp(sources.Count, 12, 60), centre);
                if (sources.Count >= 12) AddMotes(LandMotes.Kind.LavaSmoke, sources, Mathf.Clamp(sources.Count / 8, 3, 8), centre);
            }
        }

        private static Material LavaMaterial()
        {
            var shader = MeshyUnitVisuals.Enabled ? Resources.Load<Shader>("Shaders/MeshyLava") : null;
            var molten = shader != null ? Resources.Load<Texture2D>("Art/Water/lava_color") : null;
            var crust = shader != null ? Resources.Load<Texture2D>("Art/Water/lava_crust") : null;
            if (molten == null || crust == null) return null;
            var material = new Material(shader) { name = "volcanic living lava" };
            material.SetTexture("_LavaMap", molten); material.SetTexture("_CrustMap", crust);
            string[] palette = { "#2E2624", "#5C3B31", "#FF6A1E" };
            string[] slots = { "_CrustColor", "_Bank", "_Rim" };
            for (int i = 0; i < slots.Length; i++) if (ColorUtility.TryParseHtmlString(palette[i], out var colour)) material.SetColor(slots[i], colour);
            return material;
        }
        // The Meshy water sits with the textured art: ripples, sky, sun glint and foam lace, tinted per biome. The flat
        // Alpha water stays for -emberfieldProceduralUnits and for a build that lacks the maps.
        private static Material WaterMaterial(string biome, string name)
        {
            var shader = MeshyUnitVisuals.Enabled ? Resources.Load<Shader>("Shaders/MeshyWater") : null;
            var ripples = shader != null ? Resources.Load<Texture2D>("Art/Water/water_normal") : null;
            var foam = shader != null ? Resources.Load<Texture2D>("Art/Water/water_foam") : null;
            if (shader == null || ripples == null || foam == null) return new Material(Resources.Load<Shader>("Shaders/AlphaWater")) { name = name };
            var material = new Material(shader) { name = name };
            material.SetTexture("_NormalMap", ripples); material.SetTexture("_FoamMap", foam);
            string[] palette = biome == "caribbean" ? new[] { "#48C3C0", "#0E5E7E", "#F4FBF8", "#A9936A" }
                : biome == "desert" ? new[] { "#5E9A86", "#1E5552", "#EEF2E6", "#6E7F45" }
                // The elven forest's water runs clear and green over pale stone; the highland's is cold slate.
                : biome == MapLands.Elven ? new[] { "#5FB3A2", "#1B5E5C", "#F0F7F0", "#6F7F70" }
                : biome == MapLands.Highland ? new[] { "#6893A0", "#21445A", "#E8EEF2", "#5E5E50" }
                : new[] { "#5E8F84", "#1F4A50", "#E6EEE6", "#6E6A52" };
            string[] slots = { "_Shallow", "_Deep", "_Foam", "_WetBand" };
            for (int i = 0; i < slots.Length; i++) if (ColorUtility.TryParseHtmlString(palette[i], out var colour)) material.SetColor(slots[i], colour);
            return material;
        }
        private void BuildFords(float width, float depth)
        {
            // Flat stones remain below the opaque unknown-cell fog surface and never create navigation blockers.
            var b = new AlphaMeshBuilder();
            for (float z = 1; z < depth - 1; z += .45f)
            {
                float cx = StreamCenter(z, width, depth);
                for (int i = -3; i <= 3; i++)
                {
                    float x = cx + i * .44f;
                    if (PathDistance(x, z, width, depth) > 2.0f) continue;
                    float jitter = (Hash(Mathf.RoundToInt(x * 100), Mathf.RoundToInt(z * 100)) & 255) / 255f;
                    b.Rock(new Vector3(x + jitter * .08f, -.008f, z), new Vector3(.32f + jitter * .07f, .026f, .31f), 5, new Color(.63f + jitter * .08f, .61f + jitter * .08f, .48f + jitter * .07f));
                }
            }
            var mesh = b.Mesh("Amber ford stepping stones"); ownedMeshes.Add(mesh);
            var ford = new GameObject("Public shallow stone fords", typeof(MeshFilter), typeof(MeshRenderer)); ford.transform.SetParent(Root, false);
            ford.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = ford.GetComponent<MeshRenderer>(); renderer.sharedMaterial = ArtKit.Catalog.Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }
        public void Dispose()
        {
            foreach (var mesh in ownedMeshes) UnityEngine.Object.Destroy(mesh);
            foreach (var material in ownedMaterials) UnityEngine.Object.Destroy(material);
            if (water != null) UnityEngine.Object.Destroy(water);
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
