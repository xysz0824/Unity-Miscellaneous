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
        EditorGUI.BeginChangeCheck();
        var layerProperty = serializedObject.FindProperty(nameof(child.layer));
        layerProperty.intValue = EditorGUILayout.LayerField(layerProperty.displayName, layerProperty.intValue);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(child.shadowCastingMode)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(child.receiveShadows)));
        if (EditorGUI.EndChangeCheck())
        {
            child.enabled = false;
            child.enabled = true;
        }
        serializedObject.ApplyModifiedProperties();
    }
}
