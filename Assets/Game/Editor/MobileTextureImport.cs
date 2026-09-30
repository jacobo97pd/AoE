using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    /// <summary>
    /// Android import settings for the game's art: ASTC blocks at the sizes a phone screen can show. Units, buildings
    /// and the larger props drop to 512, small obstacles (the rocks) to 256; the painted ground keeps its 2048 atlas,
    /// four 1024 slices, because it fills the screen. Colour and mask maps use ASTC 6x6, normal maps the finer 5x5.
    /// Windows and every other platform keep their own settings, and UI textures are left alone so the HUD stays crisp.
    /// The Meshy bakers call <see cref="Configure"/> on each texture they import, so new art arrives with it;
    /// "Emberfield/Art/Apply Android texture settings" writes it into the .meta files of textures imported before.
    /// </summary>
    public static class MobileTextureImport
    {
        public const string Platform = "Android";
        public const int ArtSize = 512, SmallPropSize = 256, GroundSize = 2048;
        public const TextureImporterFormat ColourFormat = TextureImporterFormat.ASTC_6x6, NormalFormat = TextureImporterFormat.ASTC_5x5;
        private const string Models = "Assets/Models/", Ground = "Assets/Game/Resources/Art/Ground/";
        // Rocks: under 1.5 m, they never fill more than a few dozen pixels of a phone screen.
        private const float SmallPropHeight = 1.5f;

        [Serializable] private sealed class Roster { public Prop[] props; }
        [Serializable] private sealed class Prop { public string id, role; public float height; }
        private static HashSet<string> smallProps;

        /// <summary>
        /// The largest Android size for a texture of the game's art, or 0 when the texture is not one: reference and
        /// concept sheets sit beside the models but never ship.
        /// </summary>
        public static int SizeFor(string path)
        {
            path = (path ?? "").Replace('\\', '/');
            string file = Path.GetFileName(path);
            if (path.StartsWith(Ground, StringComparison.Ordinal)) return file.EndsWith("_layers.png", StringComparison.Ordinal) ? GroundSize : 0;
            if (!path.StartsWith(Models, StringComparison.Ordinal) || file == "reference.png" || file == "concept.png") return 0;
            var folders = path.Substring(Models.Length).Split('/');
            // Assets/Models/Props/<biome>/<id>/<id>_Map.png
            if (folders[0] == "Props" && folders.Length >= 4 && SmallProps().Contains(folders[2])) return SmallPropSize;
            return ArtSize;
        }

        /// <summary>Sets the Android override; the caller saves and reimports. Returns false when it was already there.</summary>
        public static bool Configure(TextureImporter importer, string path)
        {
            int size = SizeFor(path);
            if (importer == null || size == 0) return false;
            var format = importer.textureType == TextureImporterType.NormalMap ? NormalFormat : ColourFormat;
            var settings = importer.GetPlatformTextureSettings(Platform);
            if (settings.overridden && settings.maxTextureSize == size && settings.format == format) return false;
            settings.overridden = true; settings.maxTextureSize = size; settings.format = format;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        [MenuItem("Emberfield/Art/Apply Android texture settings")]
        public static void ApplyToExisting()
        {
            int changed = 0, art = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { Models.TrimEnd('/'), Ground.TrimEnd('/') }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || SizeFor(path) == 0) continue;
                    art++;
                    if (!Configure(importer, path)) continue;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log("EMBERFIELD_ANDROID_TEXTURES art=" + art + " changed=" + changed);
        }

        private static HashSet<string> SmallProps()
        {
            if (smallProps != null) return smallProps;
            smallProps = new HashSet<string>(StringComparer.Ordinal);
            string roster = Path.Combine(Application.dataPath, "../tools/art/meshy_props.json");
            if (!File.Exists(roster)) return smallProps;
            foreach (var prop in JsonUtility.FromJson<Roster>(File.ReadAllText(roster)).props ?? Array.Empty<Prop>())
                if (prop.role == "obstacle" && prop.height < SmallPropHeight) smallProps.Add(prop.id);
            return smallProps;
        }
    }
}
