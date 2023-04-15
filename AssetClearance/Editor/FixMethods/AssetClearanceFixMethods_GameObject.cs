using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public partial class AssetClearanceFixMethods
{
    [AssetClearanceMethod("GameObject", "清理丢失脚本")]
    public static bool ClearMissingScript([ExceptModel] GameObject gameObject, List<UnityEngine.Object> pingObjects)
    {
        bool haveFixed = false;
        foreach (var pingObject in pingObjects)
        {
            var go = pingObject as GameObject;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            haveFixed = true;
        }
        return haveFixed;
    }
    [AssetClearanceMethod("GameObject", "移开重叠的渲染物体")]
    public static bool OffsetOverlapping([ExceptModel] GameObject gameObject, int offset, List<UnityEngine.Object> pingObjects)
    {
        bool haveFixed = false;
        for (int i = 0; i < pingObjects.Count; ++i)
        {
            for (int k = 0; k < pingObjects.Count; ++k)
            {
                if (i == k) continue;
                var a = (pingObjects[i] as GameObject).transform;
                var b = (pingObjects[k] as GameObject).transform;
                if (Vector3.Distance(a.position, b.position) < 0.001f && Vector3.Distance(a.localScale, b.localScale) < 0.001f &&
                    Vector3.Distance(a.eulerAngles, b.eulerAngles) < 0.001f)
                {
                    b.position += Vector3.up * offset;
                    haveFixed = true;
                }
            }
        }
        return haveFixed;
    }
    [AssetClearanceMethod("GameObject", "清理GameObject下引用材质中无用的贴图属性")]
    public static bool ClearGameObjectUnusedTextureProperty([ExceptModel] GameObject gameObject, List<UnityEngine.Object> pingObjects)
    {
        bool haveFixed = false;
        foreach (var pingObject in pingObjects)
        {
            var material = pingObject as Material;
            if (material == null || material.shader == null) continue;
            var propertyCount = ShaderUtil.GetPropertyCount(material.shader);
            var propertyNames = new List<string>();
            for (int i = 0; i < propertyCount; ++i)
            {
                var name = ShaderUtil.GetPropertyName(material.shader, i);
                propertyNames.Add(name);
            }
            var serializedObject = new SerializedObject(material);
            var savedProperties = serializedObject.FindProperty("m_SavedProperties");
            var texEnvs = savedProperties.FindPropertyRelative("m_TexEnvs");
            for (int i = 0; i < texEnvs.arraySize; ++i)
            {
                var property = texEnvs.GetArrayElementAtIndex(i);
                if (!propertyNames.Contains(property.displayName))
                {
                    texEnvs.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            haveFixed = true;
        }
        return haveFixed;
    }
    [AssetClearanceMethod("GameObject", "替换GameObject下模型导入材质")]
    public static bool GameObjectRemapModelEmbeddedMaterial([ExceptModel] GameObject gameObject, Material material, List<UnityEngine.Object> pingObjects)
    {
        bool haveFixed = false;
        foreach (var pingObject in pingObjects)
        {
            var model = pingObject as GameObject;
            if (model == null || !PrefabUtility.IsPartOfModelPrefab(model)) continue;
            var assetPath = AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(assetPath)) return false;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;
            var serializedObject = new SerializedObject(importer);
            var embeddedMaterials = serializedObject.FindProperty("m_Materials");
            for (int i = 0; i < embeddedMaterials.arraySize; ++i)
            {
                var embeddedMaterial = embeddedMaterials.GetArrayElementAtIndex(i);
                var name = embeddedMaterial.FindPropertyRelative("name").stringValue;
                var identifier = new AssetImporter.SourceAssetIdentifier { name = name, type = typeof(Material) };
                importer.RemoveRemap(identifier);
                if (material != null)
                {
                    importer.AddRemap(identifier, material);
                }
            }
            importer.SaveAndReimport();
            haveFixed = true;
        }
        return haveFixed;
    }
    [AssetClearanceMethod("GameObject", "修改GameObject下贴图平台导入设置")]
    public static bool GameObjectSetTexturePlatformSettings([ExceptModel] GameObject gameObject, [EnsurePlatform] string platform,
        int maxSize, TextureResizeAlgorithm resizeAlgorithm, TextureImporterFormat format, TextureImporterCompression compression, List<UnityEngine.Object> pingObjects)
    {
        bool haveFixed = false;
        foreach (var pingObject in pingObjects)
        {
            var texture = pingObject as Texture;
            if (texture == null) continue;
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
            haveFixed = true;
        }
        return haveFixed;
    }
}
