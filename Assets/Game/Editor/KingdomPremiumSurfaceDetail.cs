using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>
    /// Deterministic, tileable 512px micro-surface maps for the Kingdom characters.
    /// Call from KingdomPremiumBaker.MakeMaterial AFTER authored maps/tiling are assigned,
    /// BEFORE Save(material,...):
    /// KingdomPremiumSurfaceDetail.Apply(material, item.name, Root + "/Textures/SurfaceDetail");
    /// outputRoot is an AssetDatabase folder, not a temporary filesystem directory.
    /// Only missing map slots are filled. BaseColor/BaseMap, UV transforms, authored maps,
    /// alpha/culling settings, and the existing Smoothness scalar remain untouched.
    /// Normal PNGs are tangent-space RGB, imported as NormalMap (Unity handles GPU packing).
    /// Packed response maps are linear: R=metallic, A=smoothness multiplier, G/B unused.
    /// URP ignores its Metallic scalar when a map is present, so metal-class baselines
    /// match the character manifests: Steel .82, SteelDark .72, Gold .78. Dielectrics stay 0.
    /// Seven normal families and nine response classes are shared by all three characters;
    /// no map or material is generated per renderer. No AO or albedo noise is added.
    /// </summary>
    public static class KingdomPremiumSurfaceDetail
    {
        private const int Size = 512;
        private const string Revision = "v1";
        private const float Tau = Mathf.PI * 2;
        private enum Grain { Skin, Leather, Cloth, Metal, Straw, Wood, Fur }

        private readonly struct Profile
        {
            public readonly string ResponseKey;
            public readonly Grain Grain;
            public readonly float Metal, BumpScale, SmoothnessVariation;
            public Profile(string key, Grain grain, float metal, float bump, float variation)
            { ResponseKey = key; Grain = grain; Metal = metal; BumpScale = bump; SmoothnessVariation = variation; }
        }

        /// <summary>
        /// Adds missing microdetail to URP/Lit materials with recognized semantic names.
        /// Unknown names, environment-prefixed names, other shaders, and already-authored
        /// slots are left alone. Specular workflow/albedo-alpha smoothness receive no
        /// metallic map. The caller owns SaveAssets and visual approval under scene lighting.
        /// All generation uses RAM; only the small final PNG assets are written to outputRoot.
        /// </summary>
        public static void Apply(Material material, string semanticName, string outputRoot)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit") return;
            if (!TryProfile(semanticName, out var profile)) return;

            bool needNormal = material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") == null;
            bool metallicWorkflow = !material.HasProperty("_WorkflowMode") || material.GetFloat("_WorkflowMode") > .5f;
            bool albedoSmoothness = material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A")
                || (material.HasProperty("_SmoothnessTextureChannel") && material.GetFloat("_SmoothnessTextureChannel") > .5f);
            bool needResponse = metallicWorkflow && !albedoSmoothness && material.HasProperty("_MetallicGlossMap") && material.GetTexture("_MetallicGlossMap") == null;
            if (!needNormal && !needResponse) return;

            string folder = ValidateFolder(outputRoot);
            Directory.CreateDirectory(folder);
            if (needNormal)
            {
                string path = folder + "/kp_" + profile.Grain.ToString().ToLowerInvariant() + "_" + Revision + "_normal.png";
                material.SetTexture("_BumpMap", GetMap(path, profile, true));
                material.SetFloat("_BumpScale", profile.BumpScale);
                material.EnableKeyword("_NORMALMAP");
            }
            if (needResponse)
            {
                string path = folder + "/kp_" + profile.ResponseKey + "_" + Revision + "_metal-smooth.png";
                material.SetTexture("_MetallicGlossMap", GetMap(path, profile, false));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material);
        }

        private static bool TryProfile(string semantic, out Profile profile)
        {
            string key = (semantic ?? "").Trim().ToLowerInvariant();
            // Blender may append .001 to duplicate material names; do not strip arbitrary prefixes.
            int suffix = key.LastIndexOf('.');
            if (suffix >= 0 && int.TryParse(key.Substring(suffix + 1), out _)) key = key.Substring(0, suffix);
            switch (key)
            {
                case "skin": profile = new Profile("skin", Grain.Skin, 0, .18f, .09f); return true;
                case "skinlips": profile = new Profile("skin", Grain.Skin, 0, .09f, .09f); return true;
                case "leather": case "leatherlight": profile = new Profile("leather", Grain.Leather, 0, .28f, .16f); return true;
                case "bluecloth": case "ivorycloth": case "capelining": case "royalembroidery":
                    profile = new Profile("cloth", Grain.Cloth, 0, .32f, .17f); return true;
                case "steel": profile = new Profile("steel", Grain.Metal, .82f, .13f, .12f); return true;
                case "steeldark": profile = new Profile("steel-dark", Grain.Metal, .72f, .13f, .14f); return true;
                case "gold": profile = new Profile("gold", Grain.Metal, .78f, .09f, .10f); return true;
                case "straw": case "strawlight": profile = new Profile("straw", Grain.Straw, 0, .34f, .13f); return true;
                case "wood": profile = new Profile("wood", Grain.Wood, 0, .22f, .15f); return true;
                case "fur": case "furspot": profile = new Profile("fur", Grain.Fur, 0, .20f, .12f); return true;
                default: profile = default; return false;
            }
        }

        private static string ValidateFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An Assets/... output folder is required.", nameof(folder));
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("Microdetail output must be an Assets/... folder.", nameof(folder));
            string absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), folder));
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output folder escapes Assets.", nameof(folder));
            return folder;
        }

        private static Texture2D GetMap(string path, Profile profile, bool normal)
        {
            bool generated = !File.Exists(path);
            if (generated)
            {
                var pixels = normal ? MakeNormals(profile.Grain) : MakeResponse(profile);
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
                try
                {
                    texture.SetPixels32(pixels); texture.Apply(false, false);
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
            }
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (generated || importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) throw new InvalidOperationException("Cannot import generated surface map: " + path);

            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            const string tag = "KingdomPremiumSurfaceDetail/" + Revision;
            bool dirty = generated || importer.userData != tag || importer.textureType != type || importer.sRGBTexture
                || importer.maxTextureSize != Size || !importer.mipmapEnabled || importer.isReadable
                || importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 8
                || importer.textureCompression != TextureImporterCompression.CompressedHQ || importer.convertToNormalmap
                || importer.alphaIsTransparency || importer.alphaSource != TextureImporterAlphaSource.FromInput;
            if (dirty)
            {
                importer.textureType = type;
                importer.convertToNormalmap = false; // PNG already contains RGB tangent normals, not a height field.
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.maxTextureSize = Size;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.userData = tag;
                importer.SaveAndReimport();
            }
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (result == null) throw new InvalidOperationException("Generated surface texture is missing: " + path);
            return result;
        }

        private static Color32[] MakeNormals(Grain grain)
        {
            var heights = new float[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) heights[y * Size + x] = Height(grain, (x + .5f) / Size, (y + .5f) / Size);
            var pixels = new Color32[heights.Length];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                int left = (x + Size - 1) % Size, right = (x + 1) % Size;
                int down = (y + Size - 1) % Size, up = (y + 1) % Size;
                float dx = (heights[y * Size + right] - heights[y * Size + left]) * .5f;
                float dy = (heights[up * Size + x] - heights[down * Size + x]) * .5f;
                Vector3 n = new Vector3(-dx, -dy, 1).normalized;
                pixels[y * Size + x] = new Color(Byte(n.x * .5f + .5f), Byte(n.y * .5f + .5f), Byte(n.z * .5f + .5f), 1);
            }
            return pixels;
        }

        private static Color32[] MakeResponse(Profile profile)
        {
            var pixels = new Color32[Size * Size];
            uint seed = 141u + (uint)profile.Grain * 61u;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                float u = (x + .5f) / Size, v = (y + .5f) / Size;
                float broad = Noise(u, v, 12, seed), fine = Noise(u, v, 128, seed + 7);
                float rough = Mathf.Clamp01(.25f + .42f * broad + .33f * fine);
                if (profile.Grain == Grain.Cloth) rough = Mathf.Clamp01(rough + Height(Grain.Cloth, u, v) * .20f);
                if (profile.Grain == Grain.Metal) rough = Mathf.Clamp01(rough + Scratches(u, v) * .24f);
                float metallic = profile.Metal <= 0 ? 0 : Mathf.Clamp01(profile.Metal - .018f * fine);
                float smoothnessMultiplier = 1 - profile.SmoothnessVariation * rough;
                pixels[y * Size + x] = new Color(metallic, 0, 0, smoothnessMultiplier);
            }
            return pixels;
        }

        // Returns a quantized normalized channel before Color -> Color32 conversion.
        private static float Byte(float value) => Mathf.Round(Mathf.Clamp01(value) * 255) / 255;

        private static float Height(Grain grain, float u, float v)
        {
            switch (grain)
            {
                case Grain.Skin:
                {
                    float pore = CellDistance(u, v, 88, 17);
                    return -.20f * Mathf.Exp(-pore * pore * 38) + .035f * (Noise(u, v, 128, 19) - .5f);
                }
                case Grain.Leather:
                    return .32f * (1 - CellDistance(u, v, 72, 43)) + .055f * (Noise(u, v, 192, 47) - .5f);
                case Grain.Cloth:
                {
                    float warp = Mathf.Pow(.5f + .5f * Mathf.Cos(Tau * u * 128), 2);
                    float weft = Mathf.Pow(.5f + .5f * Mathf.Cos(Tau * v * 128), 2);
                    float crossing = .5f + .32f * Mathf.Sin(Tau * u * 64) * Mathf.Sin(Tau * v * 64);
                    return .65f * Mathf.Lerp(warp, weft, crossing) + .018f * Noise(u, v, 192, 67);
                }
                case Grain.Metal:
                    return -.095f * Scratches(u, v) + .018f * (Noise(u, v, 192, 89) - .5f);
                case Grain.Straw:
                    return .34f * Mathf.Cos(Tau * (u * 96 + .13f * Noise(u, v, 12, 107))) * (.75f + .25f * Mathf.Cos(Tau * v * 24))
                        + .08f * Mathf.Cos(Tau * v * 80);
                case Grain.Wood:
                    return .23f * Mathf.Sin(Tau * (u * 88 + .35f * Noise(u, v, 8, 131))) + .025f * Noise(u, v, 192, 137);
                case Grain.Fur:
                    return .24f * Mathf.Cos(Tau * (u * 128 + .20f * Noise(u, v, 16, 163))) * (.6f + .4f * Noise(u, v, 48, 167));
                default: return 0;
            }
        }

        private static float Scratches(float u, float v)
        {
            float phase = Mathf.Repeat(u * 64 + .11f * Mathf.Sin(Tau * v * 6), 1);
            float distance = Mathf.Min(phase, 1 - phase);
            float line = Mathf.Exp(-distance * distance * 160);
            float interruption = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Noise(u, v, 24, 83) - .38f) * 3));
            return line * interruption;
        }

        private static float CellDistance(float u, float v, int cells, uint seed)
        {
            float px = Mathf.Repeat(u, 1) * cells, py = Mathf.Repeat(v, 1) * cells;
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
            float best = 4;
            for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
            {
                int cx = ix + ox, cy = iy + oy;
                float dx = cx + .12f + .76f * Hash(Wrap(cx, cells), Wrap(cy, cells), seed) - px;
                float dy = cy + .12f + .76f * Hash(Wrap(cx, cells), Wrap(cy, cells), seed + 1) - py;
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
            return Mathf.Clamp01(Mathf.Sqrt(best));
        }

        private static float Noise(float u, float v, int cells, uint seed)
        {
            float px = Mathf.Repeat(u, 1) * cells, py = Mathf.Repeat(v, 1) * cells;
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
            float fx = px - ix, fy = py - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(Wrap(ix, cells), Wrap(iy, cells), seed), b = Hash(Wrap(ix + 1, cells), Wrap(iy, cells), seed);
            float c = Hash(Wrap(ix, cells), Wrap(iy + 1, cells), seed), d = Hash(Wrap(ix + 1, cells), Wrap(iy + 1, cells), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Wrap(int value, int cells) => (value % cells + cells) % cells;
        private static float Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint n = (uint)x * 374761393u + (uint)y * 668265263u + seed * 2246822519u;
                n = (n ^ (n >> 13)) * 1274126177u; n ^= n >> 16;
                return (n & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
