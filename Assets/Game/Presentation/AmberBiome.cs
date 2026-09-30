using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    // Original flat terrain. Continuous vertex color never changes navigation height or occupancy.
    // On a map whose lands wear different cultures (MapLands) the painted ground carries every biome shown: each vertex
    // takes its share of each land, blurred over a few metres so a border reads as the ground changing, not as a seam.
    public sealed class AmberBiome : IDisposable
    {
        private readonly Mesh mesh;
        private readonly Material material;
        public Transform Root { get; }
        public AmberBiome(MapDefinition map, Transform parent, MapLands lands = null)
        {
            Root = new GameObject(map.BiomeId + " · original biome").transform; Root.SetParent(parent, false);
            float width = map.WidthCells * map.CellSizeMillimetres * .001f;
            float depth = map.HeightCells * map.CellSizeMillimetres * .001f;
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var weights = new List<Vector4>(); var triangles = new List<int>();
            bool coast = map.BiomeId == "caribbean", desert = map.BiomeId == "desert";
            bool authoredWater = (map.WaterCells?.Length ?? 0) > 0;
            var meadowShade = desert ? new Color(.58f, .43f, .25f) : coast ? new Color(.22f, .38f, .25f) : new Color(.18f, .32f, .19f);
            var meadowSun = desert ? new Color(.80f, .64f, .38f) : coast ? new Color(.43f, .51f, .29f) : new Color(.29f, .43f, .25f);
            var thickets = BlockedDistance(map);
            Color Tint(Vector3 position)
            {
                float noise = Mathf.PerlinNoise(position.x * .073f + 9.2f, position.z * .073f + 3.7f);
                Color tint = Color.Lerp(meadowShade, meadowSun, Mathf.SmoothStep(0, 1, noise));
                float route = AlphaEnvironment.PathDistance(map, position.x, position.z);
                float path = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - route / 3.2f));
                tint = Color.Lerp(tint, desert ? new Color(.72f, .55f, .32f) : coast ? new Color(.75f, .65f, .44f) : new Color(.50f, .44f, .32f), path * .94f);
                if (authoredWater)
                {
                    // Ground reads from the map's own water: sand on a beach, green at an oasis, mud on a
                    // river bank, so a generated shoreline dresses itself wherever it was drawn.
                    float shore = AlphaEnvironment.WaterDistanceCells(map, position.x, position.z);
                    var edge = desert ? new Color(.35f, .44f, .25f) : coast ? new Color(.82f, .74f, .51f) : new Color(.43f, .46f, .33f);
                    tint = Color.Lerp(tint, edge, Mathf.Clamp01(1 - shore / (coast ? 7f : 5f)) * (coast ? 1f : .78f));
                }
                else if (coast)
                {
                    float shore = Mathf.Min(position.x, width - position.x);
                    tint = Color.Lerp(tint, new Color(.80f, .72f, .49f), Mathf.Clamp01(1 - shore / 13));
                }
                else if (desert)
                {
                    float oasis = Mathf.Min(Vector2.Distance(new Vector2(position.x, position.z), new Vector2(42.5f,44.5f)), Vector2.Distance(new Vector2(position.x, position.z), new Vector2(width - 42.5f,depth - 44.5f)));
                    tint = Color.Lerp(tint, new Color(.35f, .44f, .25f), Mathf.Clamp01(1 - oasis / 8));
                }
                if (desert)
                {
                    float ridges = Mathf.Pow(Mathf.Sin(position.x * .24f + Mathf.Sin(position.z * .16f) * 2) * .5f + .5f, 3);
                    tint *= .96f + ridges * .09f;
                }
                if (!authoredWater && map.Id == "amber_crossing" && width >= 80)
                {
                    float bank = Mathf.Abs(position.x - AlphaEnvironment.StreamCenter(position.z, width, depth));
                    tint = Color.Lerp(tint, new Color(.43f, .46f, .33f), Mathf.Clamp01(1 - bank / 4.5f) * .65f);
                }
                // Alpha marks wet ground at the waterline, which the painted ground darkens.
                tint.a = authoredWater ? Mathf.Clamp01(1 - AlphaEnvironment.WaterDistanceCells(map, position.x, position.z) / 1.2f) : 0;
                return tint;
            }
            // How much of painted layers 1-3 this corner wants; layer 0 (grass, or sand in the desert) takes the rest.
            Vector4 Weights(Vector3 position)
            {
                float path = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - AlphaEnvironment.PathDistance(map, position.x, position.z) / 3.2f));
                float shore = authoredWater ? AlphaEnvironment.WaterDistanceCells(map, position.x, position.z) : 99;
                float near = Mathf.Clamp01(1 - (Corner(thickets, map, position) - .5f) / 2.2f);
                if (desert) return new Vector4(path, Mathf.Clamp01(1 - shore / 5), near * .7f, 0);
                if (coast) return new Vector4(Mathf.Clamp01(1 - shore / 7), Mathf.Clamp01(1 - shore / 1.6f), path, 0);
                return new Vector4(near * .9f, path, Mathf.Clamp01(1 - shore / 5) * .8f, 0);
            }
            // One vertex per metre, shared by the triangles around it, so paths and shores blend over a metre, not two.
            float cell = map.CellSizeMillimetres * .001f;
            int columns = map.WidthCells, rows = map.HeightCells;
            for (int z = 0; z <= rows; z++)
                for (int x = 0; x <= columns; x++)
                {
                    var position = new Vector3(Mathf.Min(x * cell, width), -.038f, Mathf.Min(z * cell, depth));
                    vertices.Add(position); colors.Add(Tint(position)); weights.Add(Weights(position));
                }
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < columns; x++)
                {
                    int corner = z * (columns + 1) + x, right = corner + 1, far = corner + columns + 1, farRight = far + 1;
                    triangles.Add(corner); triangles.Add(farRight); triangles.Add(right);
                    triangles.Add(corner); triangles.Add(far); triangles.Add(farRight);
                }
            mesh = new Mesh { name = map.BiomeId + "_surface", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetColors(colors); mesh.SetUVs(1, weights);
            var shown = lands != null && lands.HasLands && lands.Biomes.Count > 1 ? lands.Biomes : null;
            if (shown != null)
            {
                var shares = LandShares(map, lands, shown);
                // Ground beside lava glows instead of reading wet.
                for (int i = 0; i < shares.Length; i++) if (shares[i].w > 0) { var colour = colors[i]; colour.a = 0; colors[i] = colour; }
                mesh.SetColors(colors); mesh.SetUVs(2, shares);
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = shown != null ? PaintedLands(shown, colors) : PaintedGround(map.BiomeId, colors);
            if (material == null)
            {
                var shader = Resources.Load<Shader>("Shaders/AmberSurface");
                if (shader == null) throw new InvalidOperationException("Amber terrain shader is missing.");
                material = new Material(shader) { name = map.BiomeId + " terrain", enableInstancing = true };
            }
            var renderer = Root.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }

        [Serializable] private sealed class LayerFile { public Layer[] layers; }
        [Serializable] private sealed class Layer { public string name, mean; }

        // The biome's painted layers (tools/art/meshy_ground.py), or null to keep the flat procedural surface.
        private static Material PaintedGround(string biome, List<Color> terrain)
        {
            if (!MeshyUnitVisuals.Enabled) return null;
            biome = string.IsNullOrEmpty(biome) ? "forest" : biome;
            var shader = Resources.Load<Shader>("Shaders/MeshyGround");
            var layers = Resources.Load<Texture2DArray>("Art/Ground/" + biome + "_layers");
            var file = Resources.Load<TextAsset>("Art/Ground/" + biome + "_layers");
            if (shader == null || layers == null || file == null) return null;
            var means = JsonUtility.FromJson<LayerFile>(file.text)?.layers;
            if (means == null || means.Length < 4) return null;
            var luminance = new Vector4();
            for (int i = 0; i < 4; i++)
                if (ColorUtility.TryParseHtmlString(means[i].mean, out var colour)) { var linear = colour.linear; luminance[i] = linear.r * .2126f + linear.g * .7152f + linear.b * .0722f; }
            // Match the painted ground's average brightness to the flat ground's, so the lighting grade still holds.
            double sum = 0; foreach (var colour in terrain) sum += colour.r * .2126f + colour.g * .7152f + colour.b * .0722f;
            float terrainLuminance = terrain.Count > 0 ? (float)(sum / terrain.Count) : .3f;
            var material = new Material(shader) { name = biome + " painted terrain" };
            material.SetTexture("_Layers", layers);
            material.SetVector("_MeanLuminance", luminance);
            material.SetFloat("_Exposure", Mathf.Clamp(terrainLuminance / Mathf.Max(.02f, luminance.x), .6f, 3.5f));
            return material;
        }

        // How each culture's ground sits in the light: the elven forest lush, the highland a fresh open meadow, the volcanic
        // waste dark ash. Its red comes from the lava, the cracks and the land's grade: red ground as well, and its busy
        // cinder paths most of all, hid the orcs standing on it, so its paths are mostly smooth ash.
        private static (float Saturation, Vector4 Colour, float Path) LandLook(string biome) =>
            biome == MapLands.Elven ? (.76f, new Vector4(.93f, .99f, .94f, 1), 1) :
            biome == MapLands.Volcanic ? (.36f, new Vector4(.76f, .69f, .67f, 1), .3f) : (.8f, new Vector4(.97f, 1.06f, .99f, 1), 1);

        // Up to three biomes' painted layers in one material; a biome missing its layers keeps the whole map procedural.
        private static Material PaintedLands(IReadOnlyList<string> biomes, List<Color> terrain)
        {
            if (!MeshyUnitVisuals.Enabled) return null;
            var shader = Resources.Load<Shader>("Shaders/MeshyGround");
            if (shader == null) return null;
            double sum = 0; foreach (var colour in terrain) sum += colour.r * .2126f + colour.g * .7152f + colour.b * .0722f;
            float terrainLuminance = terrain.Count > 0 ? (float)(sum / terrain.Count) : .3f;
            var material = new Material(shader) { name = string.Join(" + ", biomes) + " painted lands" };
            material.EnableKeyword("_LANDS");
            string[] textures = { "_Layers", "_Layers1", "_Layers2" }, means = { "_MeanLuminance", "_MeanLuminance1", "_MeanLuminance2" }, colours = { "_LandColor0", "_LandColor1", "_LandColor2" };
            Vector4 exposure = Vector4.one, saturation = Vector4.one * .72f, path = Vector4.one;
            for (int slot = 0; slot < 3; slot++)
            {
                // An unused slot binds the first biome's layers again, so no texture is ever missing; no vertex asks for it.
                string biome = biomes[Mathf.Min(slot, biomes.Count - 1)];
                var layers = Resources.Load<Texture2DArray>("Art/Ground/" + biome + "_layers");
                var file = Resources.Load<TextAsset>("Art/Ground/" + biome + "_layers");
                var mean = file != null ? JsonUtility.FromJson<LayerFile>(file.text)?.layers : null;
                if (layers == null || mean == null || mean.Length < 4) { UnityEngine.Object.Destroy(material); return null; }
                var luminance = new Vector4();
                for (int i = 0; i < 4; i++)
                    if (ColorUtility.TryParseHtmlString(mean[i].mean, out var colour)) { var linear = colour.linear; luminance[i] = linear.r * .2126f + linear.g * .7152f + linear.b * .0722f; }
                material.SetTexture(textures[slot], layers);
                material.SetVector(means[slot], luminance);
                exposure[slot] = Mathf.Clamp(terrainLuminance / Mathf.Max(.02f, luminance.x), .6f, 3.5f);
                var look = LandLook(biome);
                saturation[slot] = look.Saturation; path[slot] = look.Path;
                material.SetVector(colours[slot], look.Colour);
            }
            material.SetVector("_LandExposure", exposure);
            material.SetVector("_LandSaturation", saturation);
            material.SetVector("_LandPath", path);
            material.SetFloat("_Exposure", exposure.x);
            return material;
        }

        /// <summary>
        /// Each grid corner's share of every biome shown (xyz, in the order of <see cref="MapLands.Biomes"/>) and how close
        /// it lies to lava (w: 1 on the bank, nothing two cells out). A land's cells are box-blurred twice, over about
        /// five metres each way, so the ground crosses from one land to the next over a few metres.
        /// </summary>
        public static Vector4[] LandShares(MapDefinition map, MapLands lands, IReadOnlyList<string> biomes)
        {
            int width = map.WidthCells, height = map.HeightCells, count = Mathf.Min(3, biomes.Count);
            var shares = new float[count][];
            for (int slot = 0; slot < count; slot++)
            {
                var cells = new float[width * height];
                for (int z = 0; z < height; z++) for (int x = 0; x < width; x++) cells[z * width + x] = lands.BiomeAt(x, z) == biomes[slot] ? 1 : 0;
                shares[slot] = Blur(Blur(cells, width, height, 3), width, height, 2);
            }
            var corners = new Vector4[(width + 1) * (height + 1)];
            for (int z = 0; z <= height; z++)
                for (int x = 0; x <= width; x++)
                {
                    var share = new Vector4(); float total = 0, glow = 0;
                    for (int dz = -1; dz <= 0; dz++) for (int dx = -1; dx <= 0; dx++)
                    {
                        int cx = Mathf.Clamp(x + dx, 0, width - 1), cz = Mathf.Clamp(z + dz, 0, height - 1);
                        for (int slot = 0; slot < count; slot++) share[slot] += shares[slot][cz * width + cx];
                        if (lands.BiomeAt(cx, cz) == MapLands.Volcanic)
                            glow = Mathf.Max(glow, Mathf.Clamp01(1 - (AlphaEnvironment.WaterDistanceCells(map, cx + .5f, cz + .5f) - .5f) / 1.5f));
                    }
                    for (int slot = 0; slot < count; slot++) total += share[slot];
                    for (int slot = 0; slot < count; slot++) share[slot] /= Mathf.Max(1e-4f, total);
                    share.w = glow;
                    corners[z * (width + 1) + x] = share;
                }
            return corners;
        }

        // A box blur of the given reach, along the rows and then the columns, clamped at the map's edges.
        private static float[] Blur(float[] cells, int width, int height, int reach)
        {
            var rows = new float[cells.Length]; var result = new float[cells.Length];
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int d = -reach; d <= reach; d++) sum += cells[z * width + Mathf.Clamp(x + d, 0, width - 1)];
                    rows[z * width + x] = sum / (reach * 2 + 1);
                }
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int d = -reach; d <= reach; d++) sum += rows[Mathf.Clamp(z + d, 0, height - 1) * width + x];
                    result[z * width + x] = sum / (reach * 2 + 1);
                }
            return result;
        }

        // Cells from the nearest blocked, dry cell (a thicket, rock or cliff), capped at 4.
        private static byte[] BlockedDistance(MapDefinition map)
        {
            int width = map.WidthCells, height = map.HeightCells;
            var distance = new byte[width * height];
            for (int i = 0; i < distance.Length; i++) distance[i] = 4;
            var queue = new Queue<int>();
            foreach (var c in map.BlockedCells ?? Array.Empty<GridCell>())
            {
                if (c.X < 0 || c.X >= width || c.Z < 0 || c.Z >= height || AlphaEnvironment.IsWaterCell(map, c.X, c.Z)) continue;
                int index = c.Z * width + c.X; if (distance[index] == 0) continue;
                distance[index] = 0; queue.Enqueue(index);
            }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); int x = current % width, z = current / width; byte next = (byte)(distance[current] + 1);
                if (next >= 4) continue;
                if (x > 0 && distance[current - 1] > next) { distance[current - 1] = next; queue.Enqueue(current - 1); }
                if (x < width - 1 && distance[current + 1] > next) { distance[current + 1] = next; queue.Enqueue(current + 1); }
                if (z > 0 && distance[current - width] > next) { distance[current - width] = next; queue.Enqueue(current - width); }
                if (z < height - 1 && distance[current + width] > next) { distance[current + width] = next; queue.Enqueue(current + width); }
            }
            return distance;
        }

        // A grid corner touches four cells; it takes the nearest, so a stand's edge is the same seen from any side.
        private static float Corner(byte[] distance, MapDefinition map, Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x), z = Mathf.RoundToInt(position.z), best = 4;
            for (int dz = -1; dz <= 0; dz++)
                for (int dx = -1; dx <= 0; dx++)
                {
                    int cx = x + dx, cz = z + dz;
                    if (cx < 0 || cz < 0 || cx >= map.WidthCells || cz >= map.HeightCells) continue;
                    best = Mathf.Min(best, distance[cz * map.WidthCells + cx]);
                }
            return best;
        }
        public void Dispose() { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(material); if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
