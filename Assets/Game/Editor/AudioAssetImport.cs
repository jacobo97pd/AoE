using System;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    /// <summary>
    /// Import settings for the CC0 clip library under Assets/Game/Resources/Audio (see docs/art/audio.md).
    /// Short one-shots (UI, unit voice/footsteps/selection, combat and economy stingers) decompress into memory
    /// as ADPCM: cheap to decode, small enough that keeping the whole clip in memory costs nothing on a phone.
    /// The long ambience beds and any future music track stream compressed Vorbis instead, so an 80 s field
    /// recording never holds tens of megabytes of PCM at once. "Emberfield/Audio/Apply clip import settings"
    /// writes this into the .meta files of clips imported before this menu item existed.
    /// </summary>
    public static class AudioAssetImport
    {
        public const string Root = "Assets/Game/Resources/Audio/";
        private static readonly string[] StreamedCategories = { "Ambience", "Music" };
        private const float StreamedQuality = .7f;

        public static bool IsStreamed(string path)
        {
            path = (path ?? string.Empty).Replace('\\', '/');
            if (!path.StartsWith(Root, StringComparison.Ordinal)) return false;
            string remainder = path.Substring(Root.Length);
            int slash = remainder.IndexOf('/');
            string category = slash < 0 ? remainder : remainder.Substring(0, slash);
            foreach (string streamed in StreamedCategories) if (string.Equals(category, streamed, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Applies the wanted settings; returns whether anything actually changed (the caller reimports only then).</summary>
        public static bool Configure(AudioImporter importer, string path)
        {
            if (importer == null || !(path ?? string.Empty).Replace('\\', '/').StartsWith(Root, StringComparison.Ordinal)) return false;
            bool streamed = IsStreamed(path);
            var current = importer.defaultSampleSettings;
            var wantedLoad = streamed ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            var wantedFormat = streamed ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            bool wantedPreload = !streamed, wantedBackground = streamed;
            bool changed = current.loadType != wantedLoad || current.compressionFormat != wantedFormat
                || (streamed && !Mathf.Approximately(current.quality, StreamedQuality))
                || current.preloadAudioData != wantedPreload || importer.loadInBackground != wantedBackground || importer.forceToMono;
            if (!changed) return false;
            current.loadType = wantedLoad; current.compressionFormat = wantedFormat; current.preloadAudioData = wantedPreload;
            if (streamed) current.quality = StreamedQuality;
            importer.defaultSampleSettings = current;
            importer.loadInBackground = wantedBackground;
            importer.forceToMono = false;
            return true;
        }

        [MenuItem("Emberfield/Audio/Apply clip import settings")]
        public static void ApplyToExisting()
        {
            int total = 0, changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root.TrimEnd('/') }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                    total++;
                    if (!Configure(importer, path)) continue;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log("EMBERFIELD_AUDIO_IMPORT total=" + total + " changed=" + changed);
        }
    }
}
