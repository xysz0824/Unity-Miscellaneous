using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;

public partial class AssetClearanceFixMethods
{
    [AssetClearanceMethod("Model", "替换模型导入材质")]
    public static bool RemapModelEmbeddedMaterial([EnsureModel] GameObject model, Material material)
    {
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
        return true;
    }
    [AssetClearanceMethod("Model", "修改模型网格导入设置")]
    public static bool SetModelImportSettings([EnsureModel] GameObject model, bool readwrite, ModelImporterMeshCompression compression)
    {
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(model)) as ModelImporter;
        importer.isReadable = readwrite;
        importer.meshCompression = importer.meshCompression < compression ? compression : importer.meshCompression;
        importer.SaveAndReimport();
        return true;
    }
}
