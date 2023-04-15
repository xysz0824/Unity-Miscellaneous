using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;
using Object = UnityEngine.Object;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("Material", "材质Shader白名单")]
    public static bool MaterialShaderWhiteList(Material material, [EnsureShader] string[] shaderNames)
    {
        if (material.shader == null) return true;
        bool result = Array.Exists(shaderNames, (name) => material.shader.name == name);
        if (!result)
        {
            AddLog($"使用了白名单之外的Shader", 0, null, material.shader);
        }
        return result;
    }
    [AssetClearanceMethod("Material", "检查AlphaTest材质RenderQueue是否符合要求")]
    public static bool CheckMaterialAlphaTest(Material material, string alphaTestKeyword, int minRenderQueue, int maxRenderQueue)
    {
        if (!material.IsKeywordEnabled(alphaTestKeyword)) return true;
        bool result = material.renderQueue >= minRenderQueue && material.renderQueue <= maxRenderQueue;
        if (!result)
        {
            AddLog($"该AlphaTest材质的RenderQueue={material.renderQueue}不符合要求范围[{minRenderQueue},{maxRenderQueue}]");
        }
        return result;
    }
    [AssetClearanceMethod("Material", "检查Transparent材质RenderQueue是否符合要求")]
    public static bool CheckMaterialTransparent(Material material, int minRenderQueue)
    {
        if (material.shader == null) return true;
        if (material.GetTag("RenderType", true, "") != "Transparent") return true;
        bool result = material.renderQueue >= minRenderQueue;
        if (!result)
        {
            AddLog($"该Transparent材质的RenderQueue={material.renderQueue}小于要求{minRenderQueue}");
        }
        return result;
    }
    [AssetClearanceMethod("Material", "找出无用的贴图属性")]
    public static bool UnusedTextureProperty(Material material)
    {
        if (material.shader == null) return false;
        bool haveUseless = false;
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
        var log = new StringBuilder();
        for (int i = 0; i < texEnvs.arraySize; ++i)
        {
            var property = texEnvs.GetArrayElementAtIndex(i);
            if (!propertyNames.Contains(property.displayName))
            {
                log.Append($"{property.displayName} ");
                haveUseless = true;
            }
        }
        if (haveUseless)
        {
            AddLog($"存在无用的贴图属性{log}");
        }
        return haveUseless;
    }
    [AssetClearanceMethod("Material", "找出无用的贴图引用")]
    public static bool MaterialUnusedTexture(Material material)
    {
        if (material.shader == null) return false;
        bool haveUseless = false;
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
        var log = new StringBuilder();
        for (int i = 0; i < texEnvs.arraySize; ++i)
        {
            var property = texEnvs.GetArrayElementAtIndex(i);
            var texProperty = property.Copy();
            texProperty.NextVisible(true);
            texProperty.NextVisible(true);
            texProperty = texProperty.FindPropertyRelative("m_Texture");
            if (!propertyNames.Contains(property.displayName) && texProperty.objectReferenceInstanceIDValue != 0)
            {
                log.Append($"{property.displayName} ");
                haveUseless = true;
            }
        }
        if (haveUseless)
        {
            AddLog($"存在无用的贴图引用{log}");
        }
        return haveUseless;
    }
}
