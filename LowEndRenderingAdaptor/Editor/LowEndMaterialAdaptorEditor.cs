using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(LowEndMaterialAdaptor))]
public class LowEndMaterialAdaptorEditor : Editor
{
    static LowEndRenderingAdaptorConfig GetConfig()
    {
        var result = AssetDatabase.FindAssets("LowEndRenderingAdaptorConfig");
        foreach (var guid in result)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == typeof(LowEndRenderingAdaptorConfig))
            {
                return AssetDatabase.LoadAssetAtPath<LowEndRenderingAdaptorConfig>(path);
            }
        }
        return null;
    }
    static KeyValuePair<string, string> GetMappingPair(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            for (int i = 0; i < line.Length - 1; ++i)
            {
                if (line[i] == '-' && line[i + 1] == '>')
                {
                    return new KeyValuePair<string, string>(line.Substring(0, i).Trim(), line.Substring(i + 2, line.Length - i - 2).Trim());
                }
            }
        }
        return new KeyValuePair<string, string>(null, null);
    }
    static bool IsMapping(string key, string name)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            var mappingNames = key.Split('/');
            foreach (var mappingName in mappingNames)
            {
                if (mappingName.Trim() == name)
                {
                    return true;
                }
            }
        }
        return false;
    }
    public override void OnInspectorGUI()
    {
        var adaptor = target as LowEndMaterialAdaptor;
        var originMaterial = serializedObject.FindProperty(nameof(adaptor.originMaterial));
        EditorGUILayout.PropertyField(originMaterial);
        var lowEndMaterial = serializedObject.FindProperty(nameof(adaptor.lowEndMaterial));
        EditorGUILayout.PropertyField(lowEndMaterial);
        var renderer = adaptor.gameObject.GetComponent<Renderer>();
        var material = renderer.sharedMaterial;
        if (material == null || adaptor.lowEndMaterial == null)
        {
            EditorGUILayout.HelpBox("Material missing.", MessageType.Error);
            return;
        }
        if (material.shader == null || adaptor.lowEndMaterial.shader == null)
        {
            EditorGUILayout.HelpBox("Shader missing.", MessageType.Error);
            return;
        }
        else if (material != adaptor.lowEndMaterial && GUILayout.Button("Switch To Low-End"))
        {
            renderer.sharedMaterial = adaptor.lowEndMaterial;
        }
        else if (material == adaptor.lowEndMaterial && GUILayout.Button("Switch To Original"))
        {
            renderer.sharedMaterial = adaptor.originMaterial;
        }
        serializedObject.ApplyModifiedProperties();
    }
    static Texture GetMainTexture(string propertyMappingRule, Material material)
    {
        var propertyCount = material.shader.GetPropertyCount();
        var mainTexture = material.mainTexture;
        var lines = propertyMappingRule.Split('\n');
        foreach (var line in lines)
        {
            var pair = GetMappingPair(line);
            if (pair.Value == "[MainTexture]")
            {
                for (int i = 0; i < propertyCount; ++i)
                {
                    var propertyName = material.shader.GetPropertyName(i);
                    if (IsMapping(pair.Key, propertyName) && material.GetTexture(propertyName) != null)
                    {
                        mainTexture = material.GetTexture(propertyName);
                        break;
                    }
                }
            }
        }
        return mainTexture;
    }
    static void MapMaterialProperties(LowEndRenderingAdaptorConfig config, Material old, Material neo)
    {
        neo.renderQueue = old.renderQueue;
        neo.enableInstancing = old.enableInstancing;
        var propertyCount = old.shader.GetPropertyCount();
        for (int i = 0; i < propertyCount; ++i)
        {
            var propertyName = old.shader.GetPropertyName(i);
            if (propertyName == config.alphatestPropertyName) continue;
            if (neo.shader.FindPropertyIndex(propertyName) != -1)
            {
                var type = old.shader.GetPropertyType(i);
                switch (type)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        neo.SetColor(propertyName, old.GetColor(propertyName));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        neo.SetFloat(propertyName, old.GetFloat(propertyName));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        neo.SetTexture(propertyName, old.GetTexture(propertyName));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        neo.SetVector(propertyName, old.GetVector(propertyName));
                        break;
                }
            }
            if (neo.renderQueue == -1 && propertyName == "_Surface")
            {
                var surface = old.GetFloat("_Surface");
                neo.SetFloat("_Surface", surface);
                neo.renderQueue = surface == 0 ? 2000 : 3000;
            }
        }
        var lines = config.propertyMappingRule.Split('\n');
        foreach (var line in lines)
        {
            var pair = GetMappingPair(line);
            if (pair.Value == "[MainTexture]")
            {
                for (int i = 0; i < propertyCount; ++i)
                {
                    var propertyName = old.shader.GetPropertyName(i);
                    if (IsMapping(pair.Key, propertyName) && old.GetTexture(propertyName) != null)
                    {
                        neo.mainTexture = old.GetTexture(propertyName);
                        break;
                    }
                }
            }
            else if (pair.Value == "[MainColor]")
            {
                for (int i = 0; i < propertyCount; ++i)
                {
                    var propertyName = old.shader.GetPropertyName(i);
                    if (IsMapping(pair.Key, propertyName))
                    {
                        neo.color = old.GetColor(propertyName);
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < propertyCount; ++i)
                {
                    var propertyName = old.shader.GetPropertyName(i);
                    if (IsMapping(pair.Key, propertyName) && old.shader.FindPropertyIndex(pair.Value) == -1)
                    {
                        var type = old.shader.GetPropertyType(i);
                        switch (type)
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                neo.SetColor(pair.Value, old.GetColor(propertyName));
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                neo.SetFloat(pair.Value, old.GetFloat(propertyName));
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                neo.SetTexture(pair.Value, old.GetTexture(propertyName));
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                neo.SetVector(pair.Value, old.GetVector(propertyName));
                                break;
                        }
                        break;
                    }
                }
            }
        }
        var keywords = old.shaderKeywords;
        for (int i = 0; i < keywords.Length; ++i)
        {
            neo.EnableKeyword(keywords[i]);
        }
        lines = config.keywordMappingRule.Split('\n');
        foreach (var line in lines)
        {
            var pair = GetMappingPair(line);
            for (int i = 0; i < keywords.Length; ++i)
            {
                if (IsMapping(pair.Key, keywords[i]))
                {
                    neo.EnableKeyword(pair.Value);
                    break;
                }
            }
        }
        if (Array.IndexOf(config.alphatestShaders, old.shader) != -1)
        {
            neo.EnableKeyword(config.alphatestKeyword);
        }
        if (neo.IsKeywordEnabled(config.alphatestKeyword))
        {
            neo.SetFloat(config.alphatestPropertyName, 1);
        }
    }
    [MenuItem("Tools/Low-End Rendering Adaptor/Generate Material Adaptor", false, 11)]
    public static void GenerateAdaptorAndMaterial()
    {
        var config = GetConfig();
        if (config == null)
        {
            Debug.LogError("Can not find Low-End Rendering Adaptor Config.");
            return;
        }
        var gameObjects = Selection.gameObjects;
        foreach (var gameObject in gameObjects)
        {
            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
            GameObject prefabContents = null;
            Renderer[] children = null;
            if (isPrefabAsset)
            {
                prefabContents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(gameObject));
                children = prefabContents.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                children = gameObject.GetComponentsInChildren<Renderer>(true);
            }
            foreach (var child in children)
            {
                if (!(child is MeshRenderer || child is SkinnedMeshRenderer)) continue;
                if (child.sharedMaterial == null || child.sharedMaterial.shader == null) continue;
                if (Array.Exists(config.ignoreMaterials, (mat) => mat == child.sharedMaterial)) continue;
                if (Array.Exists(config.ignoreShaderNames, (name) => child.sharedMaterial.shader.name.ToLower().Contains(name))) continue;
                var adaptor = child.GetComponent<LowEndMaterialAdaptor>();
                if (adaptor == null) adaptor = child.gameObject.AddComponent<LowEndMaterialAdaptor>();
                if (adaptor.lowEndMaterial == null)
                {
                    var material = child.sharedMaterial;
                    adaptor.originMaterial = child.sharedMaterial;
                    var assetPath = AssetDatabase.GetAssetPath(material);
                    var name = Path.GetFileName(assetPath);
                    assetPath = assetPath.Substring(0, assetPath.Length - name.Length) + Path.GetFileNameWithoutExtension(assetPath) + "_low.mat";
                    if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null)
                    {
                        adaptor.lowEndMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    }
                    else
                    {
                        var lowEndMaterial = new Material(config.replaceShader);
                        MapMaterialProperties(config, material, lowEndMaterial);
                        AssetDatabase.CreateAsset(lowEndMaterial, assetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(assetPath);
                        adaptor.lowEndMaterial = lowEndMaterial;
                    }
                }
            }
            if (isPrefabAsset)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabContents, AssetDatabase.GetAssetPath(gameObject));
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }
        Debug.Log($"Low-End Material Generated.");
    }
    public static void PackMainTextures(int padding)
    {
        var config = GetConfig();
        if (config == null)
        {
            Debug.LogError("Can not find Low-End Rendering Adaptor Config.");
            return;
        }
        var gameObjects = Selection.gameObjects;
        var dict = new Dictionary<Texture2D, List<Material>>();
        foreach (var gameObject in gameObjects)
        {
            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
            var children = gameObject.GetComponentsInChildren<LowEndMaterialAdaptor>(true);
            foreach (var child in children)
            {
                if (child.lowEndMaterial == null) continue;
                var texture = child.lowEndMaterial.mainTexture as Texture2D;
                if (texture == null) texture = GetMainTexture(config.propertyMappingRule, child.originMaterial) as Texture2D;
                if (texture == null || texture.name.StartsWith("Packed")) continue;
                if (Array.Exists(config.ignorePackTextures, (tex) => tex == texture)) continue;
                if (!dict.ContainsKey(texture))
                {
                    dict[texture] = new List<Material>();
                }
                if (!dict[texture].Contains(child.lowEndMaterial))
                {
                    dict[texture].Add(child.lowEndMaterial);
                }
            }
        }
        if (dict.Keys.Count <= 1)
        {
            Debug.Log("No textures need to be packed.");
            return;
        }
        var originTextures = new List<Texture2D>();
        foreach (var originalTexture in dict.Keys)
        {
            originTextures.Add(originalTexture);
            Debug.Log($"Packing Texture : {AssetDatabase.GetAssetPath(originalTexture)}");
        }
        var textures = new List<Texture2D>();
        int totalPixels = 0;
        for (int i = 0; i < originTextures.Count; ++i)
        {
            var originTexture = originTextures[i];
            var texture = new Texture2D(originTexture.width, originTexture.height, originTexture.format, originTexture.mipmapCount > 1);
            totalPixels += originTexture.width * originTexture.height;
            texture.LoadRawTextureData(originTexture.GetRawTextureData());
            texture.Apply();
            textures.Add(texture);
        }
        int size = 4;
        while (totalPixels > size * size && size < 4096) size *= 2;
        var packed = new Texture2D(size, size);
        var rects = packed.PackTextures(textures.ToArray(), padding);
        var bytes = packed.EncodeToPNG();
        var path = EditorUtility.OpenFolderPanel("Save To Folder", "", "");
        if (string.IsNullOrEmpty(path)) return;
        var relativePath = "Assets" + path.Substring(Application.dataPath.Length);
        string guid = "00000000000000000000000000000000";
        var assetPath = path + "/PackedTexture_" + guid + ".png";
        File.WriteAllBytes(assetPath, bytes);
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(assetPath);
        assetPath = relativePath + "/PackedTexture_" + guid + ".png";
        guid = AssetDatabase.AssetPathToGUID(assetPath);
        AssetDatabase.RenameAsset(assetPath, "PackedTexture_" + guid);
        AssetDatabase.Refresh();
        assetPath = relativePath + "/PackedTexture_" + guid + ".png";
        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        packed = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        for (int i = 0; i < rects.Length; ++i)
        {
            foreach (var material in dict[originTextures[i]])
            {
                material.mainTexture = packed;
                material.mainTextureOffset = (rects[i].position * size + new Vector2(1.0f, 1.0f)) / size;
                material.mainTextureScale = (rects[i].size * size - new Vector2(2.0f, 2.0f)) / size;
            }
        }
        EditorGUIUtility.PingObject(packed);
        Debug.Log($"Main textures packed.");
    }
    [MenuItem("Tools/Low-End Rendering Adaptor/Pack Main Textures (No Padding)", false, 11)]
    public static void PackMainTexturesNoPadding()
    {
        PackMainTextures(0);
    }
    static string GetPrefabInstanceAssetPath(GameObject instance)
    {
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance);
        return AssetDatabase.GetAssetPath(prefabAsset);
    }
    static bool IsModelPrefab(string prefabPath)
    {
        var toLower = prefabPath.ToLower();
        return toLower.EndsWith("fbx") || toLower.EndsWith("obj");
    }
    [MenuItem("Tools/Low-End Rendering Adaptor/Apply Material Adaptor To Nearest Prefab", false, 11)]
    public static void ApplyToNearestPrefab()
    {
        //In case of wrong result, make sure material is original
        //SwitchToOriginal();
        var gameObjects = Selection.gameObjects;
        var applyDict = new Dictionary<GameObject, List<Renderer>>();
        var upperModified = new List<Renderer>();
        foreach (var gameObject in gameObjects)
        {
            var children = gameObject.GetComponentsInChildren<Renderer>(true);
            foreach (var child in children)
            {
                var selfIsRoot = PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject);
                var nearestRoot = selfIsRoot ? child.gameObject : PrefabUtility.GetNearestPrefabInstanceRoot(child);
                var nearestPrefabPath = selfIsRoot ? GetPrefabInstanceAssetPath(child.gameObject) : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child);
                while (!string.IsNullOrEmpty(nearestPrefabPath) && IsModelPrefab(nearestPrefabPath))
                {
                    if (nearestRoot.transform.parent == null)
                    {
                        nearestPrefabPath = null;
                        break;
                    }
                    var parentIsRoot = PrefabUtility.IsAnyPrefabInstanceRoot(nearestRoot.transform.parent.gameObject);
                    nearestRoot = parentIsRoot ? nearestRoot.transform.parent.gameObject : PrefabUtility.GetNearestPrefabInstanceRoot(nearestRoot.transform.parent);
                    nearestPrefabPath = parentIsRoot ? GetPrefabInstanceAssetPath(nearestRoot.transform.gameObject) : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nearestRoot.transform);
                }
                var adaptor = child.GetComponent<LowEndMaterialAdaptor>();
                bool isUpperModified = false;
                if (adaptor != null)
                {
                    var originRenderer = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, nearestPrefabPath);
                    while (!string.IsNullOrEmpty(nearestPrefabPath) && nearestRoot.transform.parent != null && (originRenderer == null || originRenderer.sharedMaterial != adaptor.originMaterial))
                    {
                        if (nearestRoot.transform.parent == null)
                        {
                            nearestPrefabPath = null;
                            break;
                        }
                        var parentIsRoot = PrefabUtility.IsAnyPrefabInstanceRoot(nearestRoot.transform.parent.gameObject);
                        nearestRoot = parentIsRoot ? nearestRoot.transform.parent.gameObject : PrefabUtility.GetNearestPrefabInstanceRoot(nearestRoot.transform.parent);
                        nearestPrefabPath = parentIsRoot ? GetPrefabInstanceAssetPath(nearestRoot.transform.gameObject) : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nearestRoot.transform);
                        originRenderer = PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, nearestPrefabPath);
                        isUpperModified = true;
                    }
                }
                if (string.IsNullOrEmpty(nearestPrefabPath)) continue;
                if (!applyDict.ContainsKey(nearestRoot))
                {
                    applyDict[nearestRoot] = new List<Renderer>();
                }
                applyDict[nearestRoot].Add(child);
                if (isUpperModified) upperModified.Add(child);
            }
        }
        var appliedPaths = new List<string>();
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var kv in applyDict)
            {
                var addedComponents = PrefabUtility.GetAddedComponents(kv.Key);
                var removedComponents = PrefabUtility.GetRemovedComponents(kv.Key);
                var prefabPath = GetPrefabInstanceAssetPath(kv.Key);
                foreach (var child in kv.Value)
                {
                    if (upperModified.Contains(child)) continue;
                    long localID = 0;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(PrefabUtility.GetCorrespondingObjectFromSourceAtPath(child, prefabPath).gameObject, out _, out localID);
                    var path = prefabPath + "_" + localID;
                    if (!appliedPaths.Contains(path))
                    {
                        for (int i = 0; i < removedComponents.Count; ++i)
                        {
                            var removedComponent = removedComponents[i];
                            if (removedComponent.containingInstanceGameObject.transform == child.transform && removedComponent.assetComponent.GetType() == typeof(LowEndMaterialAdaptor))
                            {
                                removedComponent.Apply(prefabPath, InteractionMode.AutomatedAction);
                            }
                        }
                        for (int i = 0; i < addedComponents.Count; ++i)
                        {
                            var addedComponent = addedComponents[i];
                            if (addedComponent.instanceComponent.transform == child.transform && addedComponent.instanceComponent.GetType() == typeof(LowEndMaterialAdaptor))
                            {
                                addedComponent.Apply(prefabPath, InteractionMode.AutomatedAction);
                            }
                        }
                        appliedPaths.Add(path);
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var kv in applyDict)
            {
                var addedComponents = PrefabUtility.GetAddedComponents(kv.Key);
                foreach (var addedComponent in addedComponents)
                {
                    if (addedComponent.instanceComponent.GetType() == typeof(LowEndMaterialAdaptor))
                    {
                        addedComponent.Revert(InteractionMode.AutomatedAction);
                    }
                }
                var removedComponents = PrefabUtility.GetRemovedComponents(kv.Key);
                foreach (var removedComponent in removedComponents)
                {
                    if (removedComponent.containingInstanceGameObject.GetType() == typeof(LowEndMaterialAdaptor))
                    {
                        removedComponent.Revert(InteractionMode.AutomatedAction);
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var kv in applyDict)
            {
                var prefabPath = GetPrefabInstanceAssetPath(kv.Key);
                foreach (var child in kv.Value)
                {
                    if (!upperModified.Contains(child)) continue;
                    var adaptor = child.GetComponent<LowEndMaterialAdaptor>();
                    if (adaptor == null) continue;
                    if (child.sharedMaterial != adaptor.originMaterial)
                    {
                        var serializedObject = new SerializedObject(adaptor);
                        var originMaterial = serializedObject.FindProperty(nameof(adaptor.originMaterial));
                        var lowEndMaterial = serializedObject.FindProperty(nameof(adaptor.lowEndMaterial));
                        originMaterial.objectReferenceValue = child.sharedMaterial;
                        var assetPath = AssetDatabase.GetAssetPath(child.sharedMaterial);
                        var name = Path.GetFileName(assetPath);
                        assetPath = assetPath.Substring(0, assetPath.Length - name.Length) + Path.GetFileNameWithoutExtension(assetPath) + "_low.mat";
                        lowEndMaterial.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                        serializedObject.ApplyModifiedProperties();
                        serializedObject.Update();
                        PrefabUtility.ApplyPropertyOverride(originMaterial, prefabPath, InteractionMode.AutomatedAction);
                        PrefabUtility.ApplyPropertyOverride(lowEndMaterial, prefabPath, InteractionMode.AutomatedAction);
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }
    [MenuItem("Tools/Low-End Rendering Adaptor/Switch Material To Low-End", false, 11)]
    public static void SwitchToLowEnd()
    {
        var gameObjects = Selection.gameObjects;
        foreach (var gameObject in gameObjects)
        {
            var adaptors = gameObject.GetComponentsInChildren<LowEndMaterialAdaptor>(true);
            foreach (var adaptor in adaptors)
            {
                if (!adaptor.IsValid()) continue;
                adaptor.Active();
            }
        }
    }
    [MenuItem("Tools/Low-End Rendering Adaptor/Switch Material To Original", false, 11)]
    public static void SwitchToOriginal()
    {
        var gameObjects = Selection.gameObjects;
        foreach (var gameObject in gameObjects)
        {
            var adaptors = gameObject.GetComponentsInChildren<LowEndMaterialAdaptor>(true);
            foreach (var adaptor in adaptors)
            {
                if (!adaptor.IsValid()) continue;
                adaptor.Disactive();
            }
        }
    }
    [MenuItem("Tools/Low-End Rendering Adaptor/Delete Material Adaptor", false, 11)]
    public static void DeleteAdaptorAndMaterial()
    {
        var gameObjects = Selection.gameObjects;
        foreach (var gameObject in gameObjects)
        {
            var adaptors = gameObject.GetComponentsInChildren<LowEndMaterialAdaptor>(true);
            foreach (var adaptor in adaptors)
            {
                var child = adaptor.GetComponent<Renderer>();
                if (child != null)
                {
                    child.sharedMaterial = adaptor.originMaterial;
                    if (adaptor.lowEndMaterial != null)
                    {
                        var assetPath = AssetDatabase.GetAssetPath(adaptor.lowEndMaterial);
                        AssetDatabase.DeleteAsset(assetPath);
                        adaptor.lowEndMaterial = null;
                    }
                }
                DestroyImmediate(adaptor);
            }
        }
        Debug.Log($"Low-End Material Deleted.");
    }
}
