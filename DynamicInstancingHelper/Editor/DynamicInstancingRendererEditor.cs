using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Windows.Speech;

#if ASSET_CLEARANCE
public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("GameObject", "检查是否缺DynamicInstancing组件")]
    public static bool CheckDynamicInstancing([ExceptModel] GameObject gameObject, string[] prefixes)
    {
        bool needAdd = false;
        foreach (var prefix in prefixes)
        {
            if (gameObject.name.StartsWith(prefix))
            {
                var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
                foreach (var meshRenderer in meshRenderers)
                {
                    var dic = meshRenderer.GetComponent<DynamicInstancingChild>();
                    if (dic == null)
                    {
                        AddLog("缺DynamicInstancing组件", 0, meshRenderer.gameObject);
                        needAdd = true;
                    }
                }
            }
        }
        return needAdd;
    }
}
public partial class AssetClearanceFixMethods
{
    [AssetClearanceMethod("GameObject", "加上DynamicInstancing组件")]
    public static bool AddDynamicInstancing([ExceptModel] GameObject gameObject, 
        bool syncTransform, int layer, ShadowCastingMode shadowCastingMode, bool receiveShadows, bool enableInLowEnd, List<UnityEngine.Object> pingObjects)
    {
        foreach (var pingObject in pingObjects)
        {
            var component = pingObject as GameObject;
            var dic = component.GetComponent<DynamicInstancingChild>();
            if (dic == null) dic = component.gameObject.AddComponent<DynamicInstancingChild>();
            dic.syncTransform = syncTransform;
            dic.layer = layer;
            dic.shadowCastingMode = shadowCastingMode;
            dic.receiveShadows = receiveShadows;
            dic.enableInLowEnd = enableInLowEnd;
        }
        return true;
    }
}
#endif

[CanEditMultipleObjects]
[CustomEditor(typeof(DynamicInstancingRenderer))]
public class DynamicInstancingRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var renderer = target as DynamicInstancingRenderer;
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.enableCulling)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.cullingCamera)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.syncDelay)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.lodThreshold)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.lodProbability)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.batchCount)));
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button("Add Instancing To Children"))
        {
            var children = renderer.GetComponentsInChildren<MeshRenderer>();
            foreach (var child in children)
            {
                if (child.GetComponent<MeshFilter>() == null) continue;
                if (child.sharedMaterial == null || !child.sharedMaterial.enableInstancing) continue;
                var instancingChild = child.GetComponent<DynamicInstancingChild>();
                if (instancingChild == null)
                {
                    child.gameObject.AddComponent<DynamicInstancingChild>();
                }
            }
            EditorSceneManager.MarkAllScenesDirty();
        }
        if (GUILayout.Button("Clear Instancing Children"))
        {
            var children = renderer.GetComponentsInChildren<MeshRenderer>();
            foreach (var child in children)
            {
                if (child.GetComponent<MeshFilter>() == null) continue;
                var instancingChild = child.GetComponent<DynamicInstancingChild>();
                if (instancingChild != null)
                {
                    DestroyImmediate(instancingChild);
                }
            }
            EditorSceneManager.MarkAllScenesDirty();
        }
    }
}
