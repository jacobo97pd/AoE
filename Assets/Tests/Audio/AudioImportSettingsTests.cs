using Emberfield.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Tests.Audio
{
    /// <summary>
    /// Import settings for the CC0 clip library (docs/art/audio.md, Assets/Game/Editor/AudioAssetImport.cs):
    /// short one-shots decompress as ADPCM, the long ambience beds stream Vorbis instead, and the whole
    /// library stays well inside a phone's memory budget.
    /// </summary>
    public sealed class AudioImportSettingsTests
    {
        private const string Root = AudioAssetImport.Root;
        // A phone can afford this many decompressed bytes resident for short one-shots without noticing;
        // ambience/music never count here because they stream instead of decompressing in full.
        private const long DecompressedBudgetBytes = 12L * 1024 * 1024;

        [Test]
        public void EveryClipHasTheSettingsItsCategoryNeeds()
        {
            int checkedFiles = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                Assert.IsNotNull(importer, path + " must have an AudioImporter.");
                var settings = importer.defaultSampleSettings;
                bool streamed = AudioAssetImport.IsStreamed(path);
                if (streamed)
                {
                    Assert.AreEqual(AudioClipLoadType.Streaming, settings.loadType, path + " (ambience/music) must stream.");
                    Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat, path + " must stay Vorbis.");
                }
                else
                {
                    Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, settings.loadType, path + " (a short one-shot) must decompress on load.");
                    Assert.AreEqual(AudioCompressionFormat.ADPCM, settings.compressionFormat, path + " must be ADPCM.");
                }
                Assert.IsFalse(importer.forceToMono, path + " must keep the recording as authored.");
                checkedFiles++;
            }
            Assert.AreEqual(93, checkedFiles, "The curated library is 93 clips (docs/art/audio.md); this catches an accidental add or drop.");
        }

        [Test]
        public void TheDecompressedShortClipsStayWellInsideAPhonesBudget()
        {
            long bytes = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AudioAssetImport.IsStreamed(path)) continue; // streamed clips never hold their full PCM at once
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(clip, path);
                bytes += (long)clip.samples * clip.channels * 2; // 16-bit PCM once decompressed on load
            }
            Assert.That(bytes, Is.LessThan(DecompressedBudgetBytes), "Decompressed short SFX: " + (bytes / 1024.0 / 1024.0).ToString("F2") + " MB.");
        }

        [Test]
        public void TheProvenanceFilesShipBesideTheClips()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "LICENSES.md"), "Licences must ship next to the clips.");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "manifest.json"), "The per-file manifest must ship next to the clips.");
        }
    }
}
