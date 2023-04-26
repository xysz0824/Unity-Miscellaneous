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
        serializedObject.ApplyModifiedProperties();
    }
    [MenuItem("GameObject/Dynamic Instancing Child/Sync Layer", false, 11)]
    public static void SyncLayer()
    {
        var gameObjects = Selection.gameObjects;
        int count = 0;
        foreach (var gameObject in gameObjects)
        {
            var children = gameObject.GetComponentsInChildren<DynamicInstancingChild>(true);
            foreach (var child in children)
            {
                child.layer = child.gameObject.layer;
                count++;
            }
        }
        Debug.Log($"Sync Finished.");
    }
}
