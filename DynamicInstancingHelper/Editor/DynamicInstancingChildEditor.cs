using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CanEditMultipleObjects]
[CustomEditor(typeof(DynamicInstancingChild))]
public class DynamicInstancingChildEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var child = target as DynamicInstancingChild;
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(child.syncTransform)));
        var layerProperty = serializedObject.FindProperty(nameof(child.layer));
        layerProperty.intValue = EditorGUILayout.LayerField(layerProperty.displayName, layerProperty.intValue);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(child.shadowCastingMode)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(child.receiveShadows)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(child.enableInLowEnd)));
        if (DynamicInstancingRenderer.Instance != null)
        {
            var matrix = DynamicInstancingRenderer.Instance.GetCurrentMatrix(child);
            var position = new Vector3(matrix.m03, matrix.m13, matrix.m23);
            var boundingSphere = DynamicInstancingRenderer.Instance.GetCurrentBoundingSphere(child);
            EditorGUILayout.HelpBox($"Position : {position}\nBoundingSphere : {boundingSphere.position}", MessageType.Info);
        }
        serializedObject.ApplyModifiedProperties();
    }
    [MenuItem("GameObject/Dynamic Instancing Child/Sync Layer", false, 11)]
    public static void SyncLayer()
    {
        var gameObject = Selection.activeObject as GameObject;
        if (gameObject == null) return;
        var children = gameObject.GetComponentsInChildren<DynamicInstancingChild>();
        foreach (var child in children)
        {
            child.layer = child.gameObject.layer;
        }
    }
    [MenuItem("GameObject/Dynamic Instancing Child/Enable", false, 11)]
    public static void Enable()
    {
        var gameObject = Selection.activeObject as GameObject;
        if (gameObject == null) return;
        var children = gameObject.GetComponentsInChildren<DynamicInstancingChild>();
        foreach (var child in children)
        {
            child.enabled = true;
        }
    }
    [MenuItem("GameObject/Dynamic Instancing Child/Disable", false, 11)]
    public static void Disable()
    {
        var gameObject = Selection.activeObject as GameObject;
        if (gameObject == null) return;
        var children = gameObject.GetComponentsInChildren<DynamicInstancingChild>();
        foreach (var child in children)
        {
            child.enabled = false;
            var meshRenderer = child.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
        }
    }
}
