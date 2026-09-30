using System;
using System.Collections.Generic;
using System.IO;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    /// <summary>
    /// Bakes the shipped maps. Terrain, water and scenery are generated here and written into the map
    /// definition, so the running game dresses a map from its own data instead of from a shape hard-coded
    /// per biome. Everything is drawn once and mirrored through the map centre, which is what keeps the two
    /// starts identical; the bake refuses to write a map whose spawns, resources or objectives are not all
    /// reachable on foot from the first start.
    /// </summary>
    public static class WorldMapBaker
    {
        private const string MapDirectory = "Assets/Game/Resources/Maps";

        public static void Bake()
        {
            Write(Forest(), true);
            Write(Coast(false), true);
            Write(Coast(true), true, true, "sapphire_coast_naval");
            Write(Desert(), true);
            Write(Legend(), true);
            // The sandbox is a one-sided teaching ground, so it answers for reachability but not symmetry.
            Write(Practice(), false);
            AssetDatabase.Refresh();
        }

        /// <summary>Bakes the fantasy realm's map alone, leaving the other maps' files untouched.</summary>
        public static void BakeLegend()
        {
            Write(Legend(), true);
            AssetDatabase.Refresh();
        }

        /// <summary>Bakes only the naval coast variant; the existing land coast and other maps remain untouched.</summary>
        public static void BakeCoast()
        {
            Write(Coast(true), true, true, "sapphire_coast_naval");
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- forest

        private static MapDefinition Forest()
        {
            var canvas = new Canvas(144, 112, "amber_crossing", "forest", "aven", "serevin");
            // A river down the middle. Only the northern half is drawn: its mirror completes the meander,
            // and because the bend returns to the centre at the middle row the two halves join cleanly.
            for (int z = 0; z < canvas.Height / 2; z++)
            {
                float bend = Mathf.Sin((z - 55.5f) * .052f) * 9f + Mathf.Sin((z - 55.5f) * .146f) * 2.4f;
                int centre = Mathf.RoundToInt(71.5f + bend);
                bool crossing = z >= 18 && z <= 21 || z >= 54;
                // The bank wanders a cell either way so the shoreline is not a ruled zigzag. A crossing keeps
                // the channel narrow, because the bridge that spans it is a fixed length.
                int half = crossing ? 3 : Mathf.RoundToInt(3.1f + Mathf.Sin(z * .77f) * .85f + Mathf.Sin(z * 1.93f) * .45f);
                for (int x = centre - half; x <= centre + half; x++)
                {
                    canvas.Flood(x, z);
                    if (!crossing) canvas.Block(x, z);
                }
                if (z == 19) canvas.Landmark("bridge", centre + .5f, z + .5f, 90);
            }
            Settle(canvas, 26, 24);
            Harvest(canvas, 26, 24, "forest");
            canvas.Objective("crossing", "The Crossing", canvas.Width * .5f, canvas.Height * .5f);
            canvas.ObjectivePair("western_terrace", "Western Terrace", 44.5f, 74.5f, "eastern_terrace", "Eastern Terrace");
            canvas.Lane(26, 28, 72, 56, 3);
            canvas.Lane(44, 74, 72, 56, 2);
            // Woodland and outcrops, once the lanes and the starts are on the canvas to be kept clear.
            canvas.Scatter(5, 5, 139, 51, 4, .24f, 2, 8f);
            canvas.Landmark("village", 9.5f, 15.5f, 25);
            canvas.Landmark("windmill", 16.5f, 9.5f, 0);
            canvas.Landmark("ruins", 52.5f, 33.5f, 20);
            foreach (var spot in new[] { new Vector2(38, 8), new Vector2(18, 46), new Vector2(96, 12) })
                canvas.Landmark("village", spot.x + .5f, spot.y + .5f, spot.x * 7);
            return canvas.Build();
        }

        // ---------------------------------------------------------------- coast

        private static MapDefinition Coast(bool naval)
        {
            var canvas = new Canvas(144, 112, "sapphire_coast", "caribbean", "aven", "serevin");
            // Keep the historical coastline and land paths. The naval variant alone adds a southern strait;
            // its mirror rings the map, letting ships sail between the two otherwise disconnected coasts.
            for (int z = 0; z < canvas.Height; z++)
            {
                int shore = 7 + Mathf.RoundToInt(Mathf.Sin(z * .085f) * 3f + Mathf.Sin(z * .21f) * 1.4f);
                for (int x = 0; x < shore; x++) { canvas.Flood(x, z); canvas.Block(x, z); }
            }
            if (naval) for (int x = 0; x < canvas.Width; x++)
            {
                int reach = 4 + Mathf.RoundToInt(Mathf.Sin(x * .07f) * 1.2f + Mathf.Sin(x * .23f) * .5f);
                for (int z = 0; z < reach; z++) { canvas.Flood(x, z); canvas.Block(x, z); }
            }
            // A bay biting into the land, with a rocky islet the sea keeps for itself.
            for (int z = 30; z <= 46; z++)
            {
                int reach = 26 - Mathf.Abs(z - 38) * 2;
                for (int x = 6; x < reach; x++) { canvas.Flood(x, z); canvas.Block(x, z); }
            }
            // Naval hulls need the lagoon linked to the bay. Preserve the original five-cell island on the land map.
            for (int z = 36; z <= 40; z++) for (int x = 15; x <= (naval ? 18 : 19); x++) { canvas.Dry(x, z); canvas.Block(x, z); }
            Settle(canvas, 32, 22);
            Harvest(canvas, 32, 22, "caribbean");
            canvas.Objective("tidewater", "Tidewater Reach", canvas.Width * .5f, canvas.Height * .5f);
            canvas.ObjectivePair("north_cay", "North Cay", 48.5f, 72.5f, "south_cay", "South Cay");
            canvas.Lane(32, 26, 72, 56, 3);
            canvas.Lane(48, 72, 72, 56, 2);
            canvas.Scatter(14, 5, 130, 51, 4, .22f, 2, 8f);
            canvas.Landmark("ruins", 17.5f, 38.5f, 15);
            canvas.Landmark("palm", 15.5f, 36.5f, 40);
            canvas.Landmark("palm", naval ? 18.5f : 19.5f, 40.5f, 210);
            canvas.Landmark("pier", 28.5f, 38.5f, 90);
            canvas.Landmark("harbor", 12.5f, 20.5f, 0);
            if (!naval)
            {
                canvas.Landmark("ship", 4.5f, 18.5f, 15);
                canvas.Landmark("ship", 3.5f, 68.5f, 195);
            }
            canvas.Landmark("lighthouse", 11.5f, 56.5f, 0);
            for (int z = 8; z < canvas.Height / 2; z += 9) canvas.Landmark("palm", 9.5f + z % 3, z + .5f, z * 13);
            return canvas.Build();
        }

        // ---------------------------------------------------------------- desert

        private static MapDefinition Desert()
        {
            var canvas = new Canvas(144, 112, "sunscar_basin", "desert", "aven", "serevin");
            // A wadi out of the northern dunes feeding an oasis, mirrored into a second arm and second pool.
            for (int z = 0; z <= 40; z++)
            {
                float drift = Mathf.Sin(z * .058f) * 9f;
                int centre = Mathf.RoundToInt(58f + drift);
                bool crossing = z >= 24 && z <= 27;
                int half = crossing ? 2 : Mathf.RoundToInt(2.4f + Mathf.Sin(z * .83f) * .7f);
                for (int x = centre - half; x <= centre + half; x++)
                {
                    canvas.Flood(x, z);
                    if (!crossing) canvas.Block(x, z);
                }
                if (z == 25) canvas.Landmark("stone_bridge", centre + .5f, z + .5f, 90);
            }
            for (int z = 38; z <= 50; z++) for (int x = 50; x <= 68; x++)
            {
                if ((x - 59f) * (x - 59f) / 72f + (z - 44f) * (z - 44f) / 30f > 1) continue;
                canvas.Flood(x, z); canvas.Block(x, z);
            }
            Settle(canvas, 26, 20);
            Harvest(canvas, 26, 20, "desert");
            canvas.Objective("sunscar", "Sunscar Wells", canvas.Width * .5f, canvas.Height * .5f);
            canvas.ObjectivePair("shaded_ridge", "Shaded Ridge", 46.5f, 74.5f, "burning_ridge", "Burning Ridge");
            canvas.Lane(26, 24, 72, 56, 3);
            canvas.Lane(46, 74, 72, 56, 2);
            canvas.Scatter(5, 5, 139, 51, 4, .2f, 2, 8f);
            foreach (var palm in new[] { new Vector2(49, 41), new Vector2(68, 46), new Vector2(53, 36), new Vector2(64, 51) })
                canvas.Landmark("date_palm", palm.x + .5f, palm.y + .5f, palm.x * 9);
            // Monuments stand inside the playfield on their own footings, not off the edge as backdrop.
            canvas.Pad(36, 52, 4);
            canvas.Landmark("pyramid", 36.5f, 52.5f, 0);
            canvas.Pad(88, 20, 1);
            canvas.Landmark("obelisk", 88.5f, 20.5f, 0);
            canvas.Landmark("caravan", 12.5f, 44.5f, 115);
            canvas.Landmark("ruins", 92.5f, 40.5f, 22);
            for (int z = 6; z < canvas.Height / 2; z += 14) canvas.Landmark("dune", 4.5f, z + .5f, z * 7);
            return canvas.Build();
        }

        // ---------------------------------------------------------------- lands of legend

        private static MapDefinition Legend()
        {
            // The fantasy realm's map. Each start sits in a land of its own culture (MapLands decides the look at
            // match start: the elves' forest, the orcs' volcanic waste or the mountain peoples' highland), with neutral
            // highland between. Lands are scenery only: terrain, water and resources mirror as on every other map.
            var canvas = new Canvas(144, 112, "legend_lands", MapLands.Highland, "verdant", "ashen") { Realm = ContentRealms.Fantasy };
            Lands(canvas, 26, 24, 17f, 6f, 6f);
            // A peak on the map edge in each land and one where the neutral band meets the edge; the mirror adds the
            // other land's pair and the northern neutral peak. Each land's peak feeds a stream, lava in a volcanic land,
            // that ends in a pool inside the land; the neutral peak keeps a mountain tarn.
            canvas.Peak(61.5f, 1.5f, true, 0, 1);
            canvas.Peak(142.5f, 40.5f, false, 90, 2);
            canvas.Peak(109.5f, 1.5f, true, 0, MapLands.Neutral);
            canvas.Stream(new[] { new Vector2(61, 5), new Vector2(63, 14), new Vector2(62, 24), new Vector2(61, 32) }, 1.25f, 1.1f, 1.7f, 1);
            canvas.Pool(61.5f, 34.5f, 2.8f, 2.3f, .4f, 1);
            canvas.Stream(new[] { new Vector2(138, 40), new Vector2(132, 37), new Vector2(127, 32) }, 1.2f, 1f, 2.9f, 2);
            canvas.Pool(125.5f, 29.5f, 3.2f, 2.6f, 1.3f, 2);
            canvas.Pool(104.5f, 9.5f, 2.6f, 2.1f, 2.2f, MapLands.Neutral);
            Settle(canvas, 26, 24);
            Harvest(canvas, 26, 24, MapLands.Highland);
            canvas.Objective("highland_crown", "Highland Crown", canvas.Width * .5f, canvas.Height * .5f);
            canvas.ObjectivePair("western_cairn", "Western Cairn", 44.5f, 74.5f, "eastern_cairn", "Eastern Cairn");
            // Stands of trees and rock gather in the lands; the neutral band keeps open ground for battle.
            canvas.Scatter(5, 5, 139, 51, 4, .22f, 2, 8f, (x, z) => canvas.ZoneAt(x, z) == MapLands.Neutral ? .12f : .30f);
            // A monument in each land and one each side of the crown in the neutral band.
            canvas.Monument(12.5f, 44.5f, 2, 1);
            canvas.Monument(82.5f, 45.5f, 2, MapLands.Neutral);
            // Roads last, so no stand closes them: the main road to the crown, the cairn road, and the back road that
            // fords the land's stream on its way to the far cairn.
            canvas.Lane(26, 28, 72, 56, 3);
            canvas.Lane(44, 74, 72, 56, 2);
            canvas.Lane(32, 21, 99, 37, 2);
            return canvas.Build();
        }

        // The two starting lands and the neutral band between them: a cell belongs to a land when it lies well to that
        // start's side of the line halfway between the starts, the edge bent by noise. The noise is g(p) - g(mirror p),
        // which changes sign under the mirror, so the drawn half and its reflection meet without a seam.
        private static void Lands(Canvas canvas, int hearthX, int hearthZ, float halfBand, float bend, float rough)
        {
            float centreX = canvas.Width * .5f, centreZ = canvas.Height * .5f;
            var axis = new Vector2(centreX - hearthX, centreZ - hearthZ).normalized;
            float Edge(float x, float z)
            {
                float along = (x - centreX) * -axis.y + (z - centreZ) * axis.x;
                return bend * Mathf.Sin(along * .045f + .9f) + rough * Noise(x, z, 13, 17) + rough * .4f * Noise(x, z, 5, 71);
            }
            for (int z = 0; z < canvas.Height / 2; z++) for (int x = 0; x < canvas.Width; x++)
            {
                float px = x + .5f, pz = z + .5f;
                float signed = (px - centreX) * axis.x + (pz - centreZ) * axis.y + Edge(px, pz) - Edge(canvas.Width - px, canvas.Height - pz);
                canvas.Zone(x, z, signed < -halfBand ? 1 : signed > halfBand ? 2 : MapLands.Neutral);
            }
        }

        // Smooth value noise in [-1, 1] on a lattice of the given spacing.
        private static float Noise(float x, float z, float spacing, int salt)
        {
            float gx = x / spacing, gz = z / spacing;
            int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float fx = Mathf.SmoothStep(0, 1, gx - ix), fz = Mathf.SmoothStep(0, 1, gz - iz);
            float Corner(int a, int b) => (Canvas.Hash(a + salt, b - salt) & 1023) / 511.5f - 1;
            return Mathf.Lerp(Mathf.Lerp(Corner(ix, iz), Corner(ix + 1, iz), fx), Mathf.Lerp(Corner(ix, iz + 1), Corner(ix + 1, iz + 1), fx), fz);
        }

        // ---------------------------------------------------------------- practice

        private static MapDefinition Practice()
        {
            // The sandbox keeps every authored start exactly where it is: tests and the tutorial address
            // these positions. Only dressing is added, and only east of the worked corridor.
            var canvas = new Canvas(64, 48, "amber_reach", "forest", null, null) { Mirror = false };
            for (int z = 0; z < canvas.Height; z++)
            {
                int centre = 38 + Mathf.RoundToInt(Mathf.Sin(z * .12f) * 2.5f);
                bool crossing = z >= 22 && z <= 25;
                for (int x = centre - 2; x <= centre + 2; x++)
                {
                    canvas.Flood(x, z);
                    if (!crossing) canvas.Block(x, z);
                }
                if (z == 23) canvas.Landmark("bridge", centre + .5f, z + .5f, 90);
            }
            canvas.Units(new[]
            {
                Unit(1, 1, 10500, 16500), Unit(2, 1, 12500, 16500), Unit(3, 1, 14500, 16500), Unit(4, 1, 16500, 16500),
                Unit(5, 2, 50500, 35500)
            });
            canvas.Buildings(new[] { Building(100, 1, 12000, 12000), Building(101, 2, 52000, 38000) });
            canvas.Sources(new[]
            {
                Source(200, "food_source", 6500, 17500), Source(201, "wood_source", 20500, 12500),
                Source(202, "metal_source", 21500, 21500), Source(203, "stone_source", 8500, 23500),
                Source(204, "food_source", 57500, 30500), Source(205, "wood_source", 43500, 35500),
                Source(206, "metal_source", 42500, 26500), Source(207, "stone_source", 55500, 24500),
                Source(208, "wood_source", 24500, 8500), Source(209, "wood_source", 27500, 10500),
                Source(210, "food_source", 15500, 29500), Source(211, "stone_source", 33500, 43500),
                Source(212, "wood_source", 47500, 15500), Source(213, "food_source", 58500, 20500)
            });
            // The worked corridor of the sandbox stays open: the tutorial and the tests walk it.
            canvas.Lane(10, 16, 38, 23, 3);
            canvas.Lane(38, 23, 52, 38, 3);
            canvas.Scatter(3, 3, 61, 45, 4, .22f, 2, 6f);
            canvas.Landmark("village", 6.5f, 6.5f, 20);
            canvas.Landmark("windmill", 57.5f, 8.5f, 0);
            canvas.Landmark("ruins", 24.5f, 40.5f, 35);
            return canvas.Build();
        }

        // ---------------------------------------------------------------- shared layout

        private static void Settle(Canvas canvas, int hearthX, int hearthZ)
        {
            canvas.Clearing(hearthX, hearthZ, 7);
            canvas.Buildings(new[] { Building(100, 1, hearthX * 1000, hearthZ * 1000), canvas.MirrorBuilding(101, 2, hearthX * 1000, hearthZ * 1000) });
            var units = new List<UnitSpawnDefinition>();
            for (int index = 0; index < 4; index++)
            {
                int x = (hearthX - 2 + index * 2) * 1000 + 500, z = (hearthZ + 4) * 1000 + 500;
                units.Add(Unit(1 + index, 1, x, z));
                units.Add(canvas.MirrorUnit(11 + index, 2, x, z));
            }
            canvas.Units(units.ToArray());
        }

        // Food beside the start, wood in the woods behind it, and the ores out where they are contested.
        private static void Harvest(Canvas canvas, int hearthX, int hearthZ, string biome)
        {
            int id = 200;
            foreach (var spot in new[]
            {
                new Vector2(hearthX - 7, hearthZ + 1), new Vector2(hearthX - 6, hearthZ + 4), new Vector2(hearthX - 3, hearthZ + 8),
                new Vector2(hearthX + 4, hearthZ - 5), new Vector2(hearthX + 9, hearthZ + 2), new Vector2(hearthX + 2, hearthZ + 11)
            }) canvas.SourcePair(ref id, "food_source", (int)spot.x, (int)spot.y);
            foreach (var spot in new[]
            {
                new Vector2(hearthX + 1, hearthZ - 8), new Vector2(hearthX + 5, hearthZ - 9), new Vector2(hearthX - 9, hearthZ - 4),
                new Vector2(hearthX - 11, hearthZ + 9), new Vector2(hearthX + 12, hearthZ + 9), new Vector2(hearthX + 16, hearthZ - 2),
                new Vector2(hearthX + 19, hearthZ + 13), new Vector2(hearthX - 4, hearthZ + 17)
            }) canvas.SourcePair(ref id, "wood_source", (int)spot.x, (int)spot.y);
            // Ore sits in three rings: a seam at home so a settlement is never stranded, one a short walk
            // out, and the rest in the contested middle. Everything downstream of the first age costs metal.
            foreach (var spot in new[] { new Vector2(hearthX + 11, hearthZ - 6), new Vector2(hearthX - 9, hearthZ + 14), new Vector2(hearthX + 24, hearthZ + 8), new Vector2(hearthX + 30, hearthZ + 20) })
                canvas.SourcePair(ref id, "stone_source", (int)spot.x, (int)spot.y);
            foreach (var spot in new[] { new Vector2(hearthX + 13, hearthZ + 6), new Vector2(hearthX - 6, hearthZ - 10), new Vector2(hearthX + 22, hearthZ + 18), new Vector2(hearthX + 30, hearthZ + 12), new Vector2(hearthX + 8, hearthZ + 27) })
                canvas.SourcePair(ref id, "metal_source", (int)spot.x, (int)spot.y);
            // The basin's extra field sits on the palm shore of the oasis, never in the pool itself.
            if (biome == "desert") canvas.SourcePair(ref id, "food_source", hearthX + 24, hearthZ + 24);
        }

        private static UnitSpawnDefinition Unit(int id, int owner, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = "tender", Position = new SimPoint(x, z) };
        private static BuildingSpawnDefinition Building(int id, int owner, int x, int z) =>
            new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = "hearth", Position = new SimPoint(x, z) };
        private static ResourceSpawnDefinition Source(int id, string definition, int x, int z) =>
            new ResourceSpawnDefinition { Id = id, DefinitionId = definition, Position = new SimPoint(x, z) };

        private static void Write(MapDefinition map, bool symmetric, bool oneSea = false, string resourceId = null)
        {
            Verify(map, symmetric);
            VerifySea(map, symmetric, oneSea);
            Directory.CreateDirectory(MapDirectory);
            File.WriteAllText(Path.Combine(MapDirectory, (resourceId ?? map.Id) + ".json"), JsonUtility.ToJson(map, true));
            Debug.Log($"Baked {resourceId ?? map.Id}: {map.WidthCells}x{map.HeightCells} cells, {map.BlockedCells.Length} blocked, " +
                $"{map.WaterCells.Length} water, {map.Landmarks.Length} landmarks, {map.ResourceSpawns.Length} resources.");
        }

        /// <summary>A baked map must be symmetric, walkable end to end, and free of spawns standing in terrain.</summary>
        private static void Verify(MapDefinition map, bool symmetric)
        {
            int width = map.WidthCells, height = map.HeightCells;
            var blocked = new HashSet<int>();
            foreach (var cell in map.BlockedCells) blocked.Add(cell.Z * width + cell.X);
            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            int start = map.UnitSpawns[0].Position.Z / 1000 * width + map.UnitSpawns[0].Position.X / 1000;
            reached.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); int x = current % width, z = current / width;
                for (int side = 0; side < 4; side++)
                {
                    int nx = x + (side == 0 ? -1 : side == 1 ? 1 : 0), nz = z + (side == 2 ? -1 : side == 3 ? 1 : 0);
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                    int index = nz * width + nx;
                    if (blocked.Contains(index) || !reached.Add(index)) continue;
                    queue.Enqueue(index);
                }
            }
            void Standing(SimPoint position, string what)
            {
                int index = position.Z / 1000 * width + position.X / 1000;
                if (blocked.Contains(index)) throw new InvalidOperationException(map.Id + ": " + what + " stands in terrain.");
                if (!reached.Contains(index)) throw new InvalidOperationException(map.Id + ": " + what + " is cut off from the first start.");
            }
            foreach (var unit in map.UnitSpawns) Standing(unit.Position, "unit " + unit.Id);
            foreach (var source in map.ResourceSpawns) Standing(source.Position, "resource " + source.Id);
            foreach (var building in map.BuildingSpawns)
            {
                Standing(building.Position, "building " + building.Id);
                for (int dz = -2; dz <= 1; dz++) for (int dx = -2; dx <= 1; dx++)
                    if (blocked.Contains((building.Position.Z / 1000 + dz) * width + building.Position.X / 1000 + dx))
                        throw new InvalidOperationException(map.Id + ": building " + building.Id + " has terrain under its footprint.");
            }
            if (map.OfflineMatch != null)
                foreach (var objective in map.OfflineMatch.Objectives) Standing(objective.Position, "objective " + objective.Id);
            if (!string.IsNullOrEmpty(map.ZoneRuns)) VerifyLands(map, symmetric);
            if (!symmetric) return;
            foreach (var cell in map.BlockedCells)
                if (!blocked.Contains((height - 1 - cell.Z) * width + width - 1 - cell.X))
                    throw new InvalidOperationException(map.Id + ": terrain is not symmetric at " + cell.X + "," + cell.Z);
        }

        /// <summary>
        /// Deep water, the water blocked to feet that ships sail, is mirrored like the land; on a naval map it is also one
        /// sea, so no harbour opens onto a lake that cannot reach the other side.
        /// </summary>
        private static void VerifySea(MapDefinition map, bool symmetric, bool oneSea)
        {
            int width = map.WidthCells, height = map.HeightCells;
            var blocked = new HashSet<int>();
            foreach (var cell in map.BlockedCells) blocked.Add(cell.Z * width + cell.X);
            var deep = new HashSet<int>();
            foreach (var cell in map.WaterCells) if (blocked.Contains(cell.Z * width + cell.X)) deep.Add(cell.Z * width + cell.X);
            if (symmetric)
                foreach (int index in deep)
                    if (!deep.Contains(width * height - 1 - index))
                        throw new InvalidOperationException(map.Id + ": the sea is not symmetric at " + index % width + "," + index / width);
            if (!oneSea) return;
            if (deep.Count == 0) throw new InvalidOperationException(map.Id + ": a naval map needs a sea.");
            int first = int.MaxValue; foreach (int index in deep) first = Math.Min(first, index);
            var reached = new HashSet<int> { first };
            var queue = new Queue<int>(); queue.Enqueue(first);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(), x = current % width, z = current / width;
                for (int side = 0; side < 4; side++)
                {
                    int nx = x + (side == 0 ? -1 : side == 1 ? 1 : 0), nz = z + (side == 2 ? -1 : side == 3 ? 1 : 0);
                    int next = nz * width + nx;
                    if (nx >= 0 && nx < width && nz >= 0 && nz < height && deep.Contains(next) && reached.Add(next)) queue.Enqueue(next);
                }
            }
            if (reached.Count != deep.Count)
                throw new InvalidOperationException(map.Id + ": the sea is split; " + (deep.Count - reached.Count) + " cells of deep water cannot be sailed to.");
        }

        /// <summary>
        /// Each start stands in a land of its own, the lands are each other's reflection, and no pool or stream runs
        /// over a land's edge, where half of it would be drawn as lava and half as water.
        /// </summary>
        private static void VerifyLands(MapDefinition map, bool symmetric)
        {
            int width = map.WidthCells, height = map.HeightCells;
            var zones = MapLands.DecodeRuns(map.ZoneRuns, width, height);
            var claimed = new HashSet<int>();
            foreach (var building in map.BuildingSpawns)
            {
                int zone = zones[building.Position.Z / 1000 * width + building.Position.X / 1000];
                if (zone == MapLands.Neutral || !claimed.Add(zone))
                    throw new InvalidOperationException(map.Id + ": building " + building.Id + " does not start in a land of its own.");
            }
            if (symmetric)
                for (int index = 0; index < zones.Length; index++)
                {
                    int zone = zones[index], mirror = zones[zones.Length - 1 - index];
                    if (zone == MapLands.Neutral ? mirror != MapLands.Neutral : mirror != 3 - zone)
                        throw new InvalidOperationException(map.Id + ": lands are not mirrored at " + index % width + "," + index / width);
                }
            var water = new HashSet<int>();
            foreach (var cell in map.WaterCells) water.Add(cell.Z * width + cell.X);
            var seen = new HashSet<int>();
            foreach (int first in water)
            {
                if (!seen.Add(first)) continue;
                var queue = new Queue<int>(); queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue(), x = current % width, z = current / width;
                    if (zones[current] != zones[first])
                        throw new InvalidOperationException(map.Id + ": water at " + x + "," + z + " crosses the edge of a land.");
                    for (int side = 0; side < 4; side++)
                    {
                        int nx = x + (side == 0 ? -1 : side == 1 ? 1 : 0), nz = z + (side == 2 ? -1 : side == 3 ? 1 : 0);
                        int next = nz * width + nx;
                        if (nx >= 0 && nx < width && nz >= 0 && nz < height && water.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- canvas

        private sealed class Canvas
        {
            private readonly HashSet<int> blocked = new HashSet<int>(), water = new HashSet<int>();
            private readonly List<MapLandmarkDefinition> landmarks = new List<MapLandmarkDefinition>();
            private readonly List<ResourceSpawnDefinition> sources = new List<ResourceSpawnDefinition>();
            private readonly List<DominionObjectiveDefinition> objectives = new List<DominionObjectiveDefinition>();
            private UnitSpawnDefinition[] units = Array.Empty<UnitSpawnDefinition>();
            private BuildingSpawnDefinition[] buildings = Array.Empty<BuildingSpawnDefinition>();
            private readonly string id, biome, firstFaction, secondFaction;
            private readonly byte[] zones;
            private bool hasLands;
            internal readonly int Width, Height;
            internal bool Mirror = true;
            internal string Realm = ContentRealms.Historical;

            internal Canvas(int width, int height, string id, string biome, string firstFaction, string secondFaction)
            { Width = width; Height = height; this.id = id; this.biome = biome; this.firstFaction = firstFaction; this.secondFaction = secondFaction; zones = new byte[width * height]; }

            private bool Inside(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;
            private int Index(int x, int z) => z * Width + x;
            internal void Block(int x, int z) { Mark(blocked, x, z); }
            internal void Flood(int x, int z) { Mark(water, x, z); }
            internal void Dry(int x, int z) { Erase(water, x, z); }
            internal void Open(int x, int z) { Erase(blocked, x, z); }
            private void Mark(HashSet<int> set, int x, int z)
            {
                if (Inside(x, z)) set.Add(Index(x, z));
                if (Mirror && Inside(Width - 1 - x, Height - 1 - z)) set.Add(Index(Width - 1 - x, Height - 1 - z));
            }
            private void Erase(HashSet<int> set, int x, int z)
            {
                if (Inside(x, z)) set.Remove(Index(x, z));
                if (Mirror && Inside(Width - 1 - x, Height - 1 - z)) set.Remove(Index(Width - 1 - x, Height - 1 - z));
            }

            /// <summary>Puts a cell in a land; its reflection goes to the other land (the neutral zone stays neutral).</summary>
            internal void Zone(int x, int z, int land)
            {
                hasLands = true;
                if (Inside(x, z)) zones[Index(x, z)] = (byte)land;
                if (Mirror && Inside(Width - 1 - x, Height - 1 - z)) zones[Index(Width - 1 - x, Height - 1 - z)] = (byte)(land == MapLands.Neutral ? land : 3 - land);
            }
            internal int ZoneAt(int x, int z) => Inside(x, z) ? zones[Index(x, z)] : MapLands.Neutral;

            /// <summary>Water along a winding line, blocked except where a lane later opens a ford across it.</summary>
            internal void Stream(Vector2[] points, float half, float wobble, float salt, int land)
            {
                var drawn = new List<int>();
                float walked = 0;
                for (int i = 0; i + 1 < points.Length; i++)
                {
                    Vector2 from = points[i], to = points[i + 1];
                    float length = Vector2.Distance(from, to);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length * 2));
                    var normal = new Vector2(from.y - to.y, to.x - from.x) / length;
                    for (int step = 0; step <= steps; step++)
                    {
                        float t = step / (float)steps, along = walked + length * t;
                        float sway = Mathf.Sin(along * .21f + salt) * wobble + Mathf.Sin(along * .53f + salt * 2) * wobble * .35f;
                        float radius = half + Mathf.Sin(along * .77f + salt) * .45f + Mathf.Sin(along * 1.9f) * .25f;
                        var centre = Vector2.Lerp(from, to, t) + normal * sway;
                        int reach = Mathf.CeilToInt(radius) + 1;
                        for (int dz = -reach; dz <= reach; dz++) for (int dx = -reach; dx <= reach; dx++)
                        {
                            int x = Mathf.FloorToInt(centre.x) + dx, z = Mathf.FloorToInt(centre.y) + dz;
                            if ((new Vector2(x + .5f, z + .5f) - centre).sqrMagnitude <= radius * radius) Water(x, z, drawn);
                        }
                    }
                    walked += length;
                }
                Claim(drawn, land);
            }

            /// <summary>A pool with a lobed shore, blocked water all through.</summary>
            internal void Pool(float centreX, float centreZ, float radiusX, float radiusZ, float salt, int land)
            {
                var drawn = new List<int>();
                for (int z = Mathf.FloorToInt(centreZ - radiusZ) - 2; z <= Mathf.CeilToInt(centreZ + radiusZ) + 2; z++)
                for (int x = Mathf.FloorToInt(centreX - radiusX) - 2; x <= Mathf.CeilToInt(centreX + radiusX) + 2; x++)
                {
                    float u = (x + .5f - centreX) / radiusX, v = (z + .5f - centreZ) / radiusZ, angle = Mathf.Atan2(z + .5f - centreZ, x + .5f - centreX);
                    float shore = 1 + .12f * Mathf.Sin(angle * 3 + salt) + .07f * Mathf.Sin(angle * 5 + salt * 2);
                    if (u * u + v * v <= shore * shore) Water(x, z, drawn);
                }
                Claim(drawn, land);
            }
            private void Water(int x, int z, List<int> drawn)
            {
                Flood(x, z); Block(x, z);
                if (Inside(x, z)) drawn.Add(Index(x, z));
            }

            // Water belongs wholly to one land, banks included, so no pool is half lava and half water. Only the drawn
            // half is claimed; the mirror gives the reflection to the other land.
            private void Claim(List<int> drawn, int land, int margin = 3)
            {
                foreach (int index in drawn)
                {
                    int x = index % Width, z = index / Width;
                    for (int dz = -margin; dz <= margin; dz++) for (int dx = -margin; dx <= margin; dx++)
                        if (dx * dx + dz * dz <= margin * margin + 1 && Inside(x + dx, z + dz) && z + dz < Height / 2) Zone(x + dx, z + dz, land);
                }
            }

            /// <summary>A peak on the map edge: blocked ground under a landmark lying along the edge, kept in its land.</summary>
            internal void Peak(float x, float z, bool alongX, float rotation, int land)
            {
                int reachX = alongX ? 6 : 3, reachZ = alongX ? 3 : 6;
                var footing = new List<int>();
                for (int dz = -reachZ; dz <= reachZ; dz++) for (int dx = -reachX; dx <= reachX; dx++)
                {
                    Block((int)x + dx, (int)z + dz);
                    if (Inside((int)x + dx, (int)z + dz)) footing.Add(Index((int)x + dx, (int)z + dz));
                }
                Claim(footing, land);
                Landmark(MapLands.PeakSlot, x, z, rotation);
            }

            /// <summary>A land's monument on its own blocked footing, kept in the land it is meant to stand for.</summary>
            internal void Monument(float x, float z, int radius, int land)
            {
                Pad((int)x, (int)z, radius);
                Claim(new List<int> { Index((int)x, (int)z) }, land, radius + 3);
                Landmark(MapLands.MonumentSlot, x, z, 0);
            }

            /// <summary>Terrain under a monument: blocked ground the map keeps clear of anything else.</summary>
            internal void Pad(int x, int z, int radius)
            {
                for (int dz = -radius; dz <= radius; dz++) for (int dx = -radius; dx <= radius; dx++) Block(x + dx, z + dz);
            }
            internal void Clearing(int x, int z, int radius, bool dry = true)
            {
                for (int dz = -radius; dz <= radius; dz++) for (int dx = -radius; dx <= radius; dx++)
                { Open(x + dx, z + dz); if (dry) Dry(x + dx, z + dz); }
            }

            /// <summary>Opens a walking lane without draining it, so it can run over a ford or a bridge.</summary>
            internal void Lane(float fromX, float fromZ, float toX, float toZ, int radius)
            {
                float length = Vector2.Distance(new Vector2(fromX, fromZ), new Vector2(toX, toZ));
                int steps = Mathf.CeilToInt(length * 2);
                for (int step = 0; step <= steps; step++)
                {
                    float t = steps == 0 ? 0 : step / (float)steps;
                    Clearing(Mathf.RoundToInt(Mathf.Lerp(fromX, toX, t)), Mathf.RoundToInt(Mathf.Lerp(fromZ, toZ, t)), radius, false);
                }
            }

            /// <summary>Clumps of terrain on a deterministic lattice, kept off the lanes and off the water.</summary>
            internal void Scatter(int fromX, int fromZ, int toX, int toZ, int step, float chance, int radius, float clearance, Func<int, int, float> density = null)
            {
                for (int z = fromZ; z < toZ; z += step) for (int x = fromX; x < toX; x += step)
                {
                    uint seed = Hash(x, z);
                    if ((seed & 1023) / 1023f > (density != null ? density(x, z) : chance)) continue;
                    if (NearWater(x, z, 3) || NearStart(x, z, clearance)) continue;
                    int size = 1 + (int)(seed >> 11 & 1) * (radius - 1);
                    for (int dz = -size; dz <= size; dz++) for (int dx = -size; dx <= size; dx++)
                    {
                        if (dx * dx + dz * dz > size * size + 1) continue;
                        // Every cell of the clump is tested, not only where it was seeded: a stand that
                        // spreads must not close over a resource, a start or a lane.
                        if (NearWater(x + dx, z + dz, 2) || NearStart(x + dx, z + dz, clearance)) continue;
                        Block(x + dx, z + dz);
                    }
                }
            }
            private bool NearWater(int x, int z, int radius)
            {
                for (int dz = -radius; dz <= radius; dz++) for (int dx = -radius; dx <= radius; dx++)
                    if (Inside(x + dx, z + dz) && water.Contains(Index(x + dx, z + dz))) return true;
                return false;
            }
            private bool NearStart(int x, int z, float clearance)
            {
                foreach (var building in buildings)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(building.Position.X, building.Position.Z) * .001f) < clearance) return true;
                foreach (var source in sources)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(source.Position.X, source.Position.Z) * .001f) < 2.5f) return true;
                foreach (var objective in objectives)
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(objective.Position.X, objective.Position.Z) * .001f) < 6f) return true;
                return false;
            }

            internal void Landmark(string kind, float x, float z, float rotation, int scalePermille = 1000)
            {
                landmarks.Add(New(kind, x, z, rotation, scalePermille));
                if (Mirror) landmarks.Add(New(kind, Width - x, Height - z, rotation + 180, scalePermille));
            }
            private static MapLandmarkDefinition New(string kind, float x, float z, float rotation, int scale) =>
                new MapLandmarkDefinition { Kind = kind, Position = new SimPoint(Mathf.RoundToInt(x * 1000), Mathf.RoundToInt(z * 1000)), RotationDegrees = Mathf.RoundToInt(Mathf.Repeat(rotation, 360)), ScalePermille = scale };

            internal void SourcePair(ref int id, string definition, int x, int z)
            {
                int px = x * 1000 + 500, pz = z * 1000 + 500;
                Clearing(x, z, 1);
                sources.Add(Source(id, definition, px, pz));
                sources.Add(Source(id + 50, definition, Width * 1000 - px, Height * 1000 - pz));
                id++;
            }
            internal void Objective(string objectiveId, string name, float x, float z)
            {
                Clearing(Mathf.FloorToInt(x), Mathf.FloorToInt(z), 3, false);
                objectives.Add(new DominionObjectiveDefinition { Id = objectiveId, DisplayName = name, Position = new SimPoint(Mathf.RoundToInt(x * 1000), Mathf.RoundToInt(z * 1000)), RadiusMillimetres = 4000 });
            }
            internal void ObjectivePair(string firstId, string firstName, float x, float z, string secondId, string secondName)
            {
                Objective(firstId, firstName, x, z);
                Objective(secondId, secondName, Width - x, Height - z);
            }
            internal void Units(UnitSpawnDefinition[] value) { units = value; foreach (var unit in value) Clearing(unit.Position.X / 1000, unit.Position.Z / 1000, 1); }
            internal void Buildings(BuildingSpawnDefinition[] value) { buildings = value; foreach (var item in value) Clearing(item.Position.X / 1000, item.Position.Z / 1000, 3); }
            internal void Sources(ResourceSpawnDefinition[] value) { sources.AddRange(value); foreach (var item in value) Clearing(item.Position.X / 1000, item.Position.Z / 1000, 1); }
            internal UnitSpawnDefinition MirrorUnit(int id, int owner, int x, int z) => Unit(id, owner, Width * 1000 - x, Height * 1000 - z);
            internal BuildingSpawnDefinition MirrorBuilding(int id, int owner, int x, int z) => Building(id, owner, Width * 1000 - x, Height * 1000 - z);

            internal MapDefinition Build()
            {
                var map = new MapDefinition
                {
                    Id = id, RealmId = Realm, BiomeId = biome,
                    WidthCells = Width, HeightCells = Height, CellSizeMillimetres = 1000,
                    BlockedCells = Cells(blocked), WaterCells = Cells(water), ZoneRuns = hasLands ? MapLands.EncodeRuns(zones) : "",
                    Landmarks = landmarks.ToArray(), UnitSpawns = units, BuildingSpawns = buildings,
                    ResourceSpawns = sources.ToArray()
                };
                if (firstFaction != null)
                {
                    map.PlayerFactions = new[]
                    {
                        new PlayerFactionDefinition { PlayerId = 1, FactionId = firstFaction },
                        new PlayerFactionDefinition { PlayerId = 2, FactionId = secondFaction }
                    };
                    map.OfflineMatch = new OfflineMatchDefinition
                    {
                        Enabled = true, Mode = VictoryMode.Conquest, PlayerIds = new[] { 1, 2 },
                        VisionUnitCells = 9, VisionBuildingCells = 12, CentralBuildingId = "hearth",
                        Objectives = objectives.ToArray(), ObjectivesRequired = 2, CaptureTicks = 300, ContinuousHoldTicks = 9600
                    };
                }
                return map;
            }
            private GridCell[] Cells(HashSet<int> set)
            {
                var list = new List<GridCell>(set.Count);
                foreach (int index in set) list.Add(new GridCell(index % Width, index / Width));
                list.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));
                return list.ToArray();
            }
            internal static uint Hash(int x, int z)
            { unchecked { uint v = (uint)(x * 73856093 ^ z * 19349663 ^ 83492791); v ^= v >> 13; v *= 1274126177; return v ^ v >> 16; } }
        }
    }
}
