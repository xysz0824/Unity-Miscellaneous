using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;

public partial class AssetClearanceFixMethods
{ 
    [AssetClearanceMethod("Material", "清理无用的贴图属性")]
    public static bool ClearUnusedTextureProperty(Material material)
    {
        if (material.shader == null) return false;
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
        return true;
    }
    [AssetClearanceMethod("Material", "清理无用的贴图引用")]
    public static bool ClearMaterialUnusedTexture(Material material)
    {
        if (material.shader == null) return false;
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
            var texProperty = property.Copy();
            texProperty.NextVisible(true);
            texProperty.NextVisible(true);
            texProperty = texProperty.FindPropertyRelative("m_Texture");
            if (!propertyNames.Contains(property.displayName) && texProperty.objectReferenceInstanceIDValue != 0)
            {
                texEnvs.DeleteArrayElementAtIndex(i);
                i--;
            }
        }
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        return true;
    }
}
