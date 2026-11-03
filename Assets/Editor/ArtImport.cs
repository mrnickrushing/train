using UnityEditor;
using UnityEngine;

namespace Ironhorse.EditorTools
{
    /// <summary>
    /// Import rules for the packed CC0 texture atlases built by Tools/art/pack_textures.py: each one
    /// becomes a Texture2DArray (one layer per flipbook cell), compressed per platform.
    /// </summary>
    public sealed class ArtImport : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Textures/";

        const string Models = "Assets/Resources/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Models)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // ArtMaterials builds URP materials at runtime
            bool person = assetPath.StartsWith(Models + "People/");
            mi.importAnimation = person;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = person ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            if (person)
            {
                mi.animationWrapMode = WrapMode.Loop;
                mi.resampleCurves = true;
            }
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Models))
            {
                var t = (TextureImporter)assetImporter;
                bool normal = assetPath.EndsWith("_normal.jpg") || assetPath.EndsWith("_normal.png"), data = assetPath.EndsWith("_ms.png");
                t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                t.sRGBTexture = !normal && !data;
                t.mipmapEnabled = true;
                t.anisoLevel = 4;
                t.maxTextureSize = 2048;
                t.textureCompression = TextureImporterCompression.CompressedHQ;
                t.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "iPhone", overridden = true, maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
                });
                return;
            }
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            int cols, rows;
            switch (file)
            {
                case "TerrainAlbedo": cols = 3; rows = 2; ti.sRGBTexture = true; break;
                case "TerrainNormal": cols = 3; rows = 2; ti.sRGBTexture = false; break;
                case "SurfaceDetail": cols = 4; rows = 4; ti.sRGBTexture = false; break;
                default: return;
            }
            ti.textureType = TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2DArray;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.flipbookColumns = cols;
            settings.flipbookRows = rows;
            ti.SetTextureSettings(settings);
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone", overridden = true, maxTextureSize = 4096,
                format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50,
            });
        }
    }
}
