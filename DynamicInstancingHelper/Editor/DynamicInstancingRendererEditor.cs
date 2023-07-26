using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CanEditMultipleObjects]
[CustomEditor(typeof(DynamicInstancingRenderer))]
public class DynamicInstancingRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var renderer = target as DynamicInstancingRenderer;
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.enableCulling)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.cullingCamera)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(renderer.syncTransform)));
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
