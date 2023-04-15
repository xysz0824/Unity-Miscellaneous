using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;

public partial class AssetClearanceFixMethods
{ 
    [AssetClearanceMethod("Texture", "修改贴图平台格式设置")]
    public static bool SetTexturePlatformSettings(Texture texture, [EnsurePlatform] string platform, 
        int maxSize, TextureResizeAlgorithm resizeAlgorithm, TextureImporterFormat format, TextureImporterCompression compression)
    {
        var assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath)) return false;
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;
        var settings = importer.GetPlatformTextureSettings(platform);
        if (settings == null)
        {
            settings = importer.GetDefaultPlatformTextureSettings();
        }
        settings.maxTextureSize = maxSize;
        settings.resizeAlgorithm = resizeAlgorithm;
        settings.format = format;
        settings.textureCompression = compression;
        if (importer.GetPlatformTextureSettings(platform) != null)
        {
            importer.SetPlatformTextureSettings(settings);
        }
        importer.SaveAndReimport();
        return true;
    }
    static TextureImporterFormat[] alphaFreeFormats =
    {
        TextureImporterFormat.RGB16,
        TextureImporterFormat.RGB24,
        TextureImporterFormat.ETC2_RGB4,
        TextureImporterFormat.ETC2_RGB4,
        TextureImporterFormat.ETC2_RGB4,
        TextureImporterFormat.PVRTC_RGB2,
        TextureImporterFormat.PVRTC_RGB4,
        TextureImporterFormat.RGB24,
        TextureImporterFormat.RGB48,
        TextureImporterFormat.RGB9E5,
        TextureImporterFormat.RGB9E5
    };
    [AssetClearanceMethod("Texture", "优化不需要Alpha的贴图格式")]
    public static bool OptimizeTextureFormatNotNeedAlpha(Texture texture, [EnsurePlatform] string platform)
    {
        var path = AssetDatabase.GetAssetPath(texture);
        if (path == null) return false;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;
        bool hasAlpha = importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None;
        if (!hasAlpha)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (settings == null)
            {
                settings = importer.GetDefaultPlatformTextureSettings();
            }
            for (int i = 0; i < AssetClearanceMethods.alphaIncludedFormats.Length; ++i)
            {
                if (settings.format == AssetClearanceMethods.alphaIncludedFormats[i])
                {
                    settings.format = alphaFreeFormats[i];
                    importer.SetPlatformTextureSettings(settings); 
                    importer.SaveAndReimport();
                    return true;
                }
            }
        }
        return false;
    }
    [AssetClearanceMethod("Texture", "设置贴图是否生成Mipmaps")]
    public static bool SetTextureGenerateMipmaps(Texture texture, bool generateMipmaps)
    {
        var assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath)) return false;
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;
        importer.mipmapEnabled = generateMipmaps;
        importer.SaveAndReimport();
        return true;
    }
}
