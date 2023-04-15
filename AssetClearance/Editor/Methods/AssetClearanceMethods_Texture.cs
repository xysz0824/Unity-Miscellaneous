using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;
using System.Reflection;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("Texture", "检查贴图导入格式是否符合要求")]
    public static bool PlatformTextureFormat(Texture texture, [EnsurePlatform] string platform, TextureImporterFormat desiredFormat)
    {
        var assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath)) return true;
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return true;
        TextureImporterFormat format = default;
        if (!importer.GetPlatformTextureSettings(platform, out _, out format, out _, out _))
        {
            format = importer.GetDefaultPlatformTextureSettings().format;
        }
        bool result = format == desiredFormat;
        if (!result)
        {
            AddLog($"贴图格式{format}不符合要求格式{desiredFormat}");
        }
        return result;
    }
    [AssetClearanceMethod("Texture", "检查贴图导入压缩设置是否符合要求")]
    public static bool PlatformTextureCompression(Texture texture, [EnsurePlatform] string platform, TextureImporterCompression compression)
    {
        var assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath)) return true;
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return true;
        var settings = importer.GetPlatformTextureSettings(platform);
        if (settings == null)
        {
            settings = importer.GetDefaultPlatformTextureSettings();
        }
        bool result = settings.textureCompression == compression;
        if (!result)
        {
            AddLog($"压缩设置{settings.textureCompression}不符合要求设置{compression}");
        }
        return result;
    }
    [AssetClearanceMethod("Texture", "检查当前平台贴图占用内存是否不超过指定大小（MB）")]
    public static bool MaxTextureMemory(Texture texture, float memory)
    {
        var storageMemory = GetTextureStorageMemory(texture);
        bool result = storageMemory <= memory * 1024 * 1024;
        if (!result)
        {
            AddLog($"贴图占用内存{FormatBytes(storageMemory)}MB超过指定大小{memory}", (int)(storageMemory / 1024 / 1024));
        }
        return result;
    }
    [AssetClearanceMethod("Texture", "检查贴图宽高尺寸不超过指定大小")]
    public static bool MaxTextureSize(Texture texture, [EnsurePlatform] string platform, int width, int height)
    {
        var size = GetPlatformTextureSize(texture, platform);
        bool result = size.x <= width && size.y <= height;
        if (!result)
        {
            if (size.x > width && size.y > height)
            {
                AddLog($"贴图宽高{size.x}x{size.y}超过指定大小{width}x{height}", size.x * size.y);
            }
            else if (size.x > width)
            {
                AddLog($"贴图宽{size.x}超过指定大小{width}", size.x);
            }
            else
            {
                AddLog($"贴图高{size.y}超过指定大小{height}", size.y);
            }
        }
        return result;
    }
    [AssetClearanceMethod("Texture", "检查贴图是否带有Alpha通道信息")]
    public static bool DoesTextureHasAlpha(Texture texture)
    {
        var path = AssetDatabase.GetAssetPath(texture);
        if (path == null) return false;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;
        bool result = importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None;
        if (result) AddLog("贴图带有Alpha通道信息");
        else AddLog("贴图不带Alpha通道信息");
        return result;
    }
    public static TextureImporterFormat[] alphaIncludedFormats =
    {
        TextureImporterFormat.ARGB16,
        TextureImporterFormat.ARGB32,
        TextureImporterFormat.ETC2_RGB4_PUNCHTHROUGH_ALPHA,
        TextureImporterFormat.ETC2_RGBA8,
        TextureImporterFormat.ETC2_RGBA8Crunched,
        TextureImporterFormat.PVRTC_RGBA2,
        TextureImporterFormat.PVRTC_RGBA4,
        TextureImporterFormat.RGBA32,
        TextureImporterFormat.RGBA64,
        TextureImporterFormat.RGBAFloat,
        TextureImporterFormat.RGBAHalf
    };
    [AssetClearanceMethod("Texture", "检查贴图格式是否不需要Alpha")]
    public static bool TextureFormatNotNeedAlpha(Texture texture, [EnsurePlatform] string platform)
    {
        var path = AssetDatabase.GetAssetPath(texture);
        if (path == null) return false;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;
        bool hasAlpha = importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None;
        if (!hasAlpha)
        {
            TextureImporterFormat format = default;
            if (!importer.GetPlatformTextureSettings(platform, out _, out format, out _, out _))
            {
                format = importer.GetDefaultPlatformTextureSettings().format;
            }
            foreach (var alphaIncludedFormat in alphaIncludedFormats)
            {
                if (format == alphaIncludedFormat)
                {
                    AddLog("该贴图不需要带有Alpha通道的格式");
                    return true;
                }
            }
        }
        return false;
    }
    [AssetClearanceMethod("Texture", "检查Sprite贴图是否开启Mipmaps")]
    public static bool TextureSpriteMipmapsEnabled(Texture texture)
    {
        var path = AssetDatabase.GetAssetPath(texture);
        if (path == null) return false;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;
        if (importer.textureType == TextureImporterType.Sprite && importer.mipmapEnabled)
        {
            AddLog("该Sprite贴图开启了MIpmaps");
            return true;
        }
        return false;
    }
}
