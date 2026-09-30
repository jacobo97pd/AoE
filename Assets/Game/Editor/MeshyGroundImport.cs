using System.IO;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    /// <summary>
    /// Import settings for the painted ground and the water maps. Each biome's &lt;biome&gt;_layers.png is a 2x2 atlas
    /// (tools/art/meshy_ground.py) that becomes a four-slice Texture2DArray for the Meshy Ground shader; the water
    /// ripple and foam maps tile in world space, so both wrap.
    /// </summary>
    public static class MeshyGroundImport
    {
        private const string Ground = "Assets/Game/Resources/Art/Ground";
        private const string Water = "Assets/Game/Resources/Art/Water";

        [MenuItem("Emberfield/Art/Configure painted ground and water")]
        public static int Configure()
        {
            int count = 0;
            if (Directory.Exists(Ground))
                foreach (var file in Directory.GetFiles(Ground, "*_layers.png"))
                {
                    string path = file.Replace('\\', '/');
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Default;
                    importer.textureShape = TextureImporterShape.Texture2DArray;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.flipbookRows = 2; settings.flipbookColumns = 2;
                    importer.SetTextureSettings(settings);
                    importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
                    importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.anisoLevel = 4;
                    importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    MobileTextureImport.Configure(importer, path);
                    importer.SaveAndReimport();
                    count++;
                }
            foreach (var (name, normal) in new[] { ("water_normal", true), ("water_foam", false) })
            {
                string path = Water + "/" + name + ".png";
                if (!File.Exists(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.SingleChannel;
                importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
                importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                count++;
            }
            // Lava (tools/art/make_lava_maps.py): a colour map whose alpha is heat, and a crust map of plain data (distance
            // to a crack, a value per plate, grain) that must reach the shader unconverted. Both scroll, so both wrap.
            foreach (var (name, colour) in new[] { ("lava_color", true), ("lava_crust", false) })
            {
                string path = Water + "/" + name + ".png";
                if (!File.Exists(path)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = colour;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
                importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                count++;
            }
            Debug.Log("EMBERFIELD_MESHY_GROUND_OK textures=" + count);
            return count;
        }
    }
}
