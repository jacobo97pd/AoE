using System;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    public static class AlphaAssetImport
    {
        private const string KeyArt = "Assets/Game/Resources/Interface/EmberfieldKeyArt.png";
        public static void Prepare()
        {
            var image = AssetImporter.GetAtPath(KeyArt) as TextureImporter;
            if (image == null) throw new InvalidOperationException("The frontend key art has no texture importer.");
            image.textureType = TextureImporterType.Default;
            image.textureShape = TextureImporterShape.Texture2D;
            image.sRGBTexture = true; image.mipmapEnabled = false;
            image.npotScale = TextureImporterNPOTScale.None;
            image.alphaSource = TextureImporterAlphaSource.None;
            image.maxTextureSize = 2048; image.textureCompression = TextureImporterCompression.Compressed;
            image.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KeyArt);
            if (texture == null) throw new InvalidOperationException("The frontend illustration did not import as a 2D texture.");
            foreach (string name in new[] { "Lato-Regular", "Lato-Bold", "Marcellus-Regular" })
            {
                string path = "Assets/Game/Resources/Fonts/" + name + ".ttf";
                var font = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
                if (font == null) throw new InvalidOperationException("Missing font importer: " + name);
                font.fontTextureCase = FontTextureCase.Dynamic; font.includeFontData = true; font.fontSize = 18;
                font.SaveAndReimport();
                if (AssetDatabase.LoadAssetAtPath<Font>(path) == null) throw new InvalidOperationException("Font did not import: " + name);
            }
        }
    }
}
