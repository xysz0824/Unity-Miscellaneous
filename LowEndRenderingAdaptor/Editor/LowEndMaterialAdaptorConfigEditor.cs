using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LowEndRenderingAdaptorConfig))]
public class LowEndRenderingAdaptorConfigEditor : Editor
{
    Vector2 scrollPos = Vector2.zero;
    public static class Style
    {
        public static readonly GUIStyle BoldLabel = new GUIStyle("BoldLabel");
    }
    public override void OnInspectorGUI()
    {
        var config = target as LowEndRenderingAdaptorConfig;
        EditorGUILayout.LabelField("Material", Style.BoldLabel);
        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        var replaceShader = serializedObject.FindProperty(nameof(config.replaceShader));
        EditorGUILayout.PropertyField(replaceShader);
        var alphatestPropertyName = serializedObject.FindProperty(nameof(config.alphatestPropertyName));
        EditorGUILayout.PropertyField(alphatestPropertyName);
        var alphatestKeyword = serializedObject.FindProperty(nameof(config.alphatestKeyword));
        EditorGUILayout.PropertyField(alphatestKeyword);
        var alphatestShaders = serializedObject.FindProperty(nameof(config.alphatestShaders));
        EditorGUILayout.PropertyField(alphatestShaders);
        var ignoreShaderNames = serializedObject.FindProperty(nameof(config.ignoreShaderNames));
        EditorGUILayout.PropertyField(ignoreShaderNames);
        var ignoreMaterials = serializedObject.FindProperty(nameof(config.ignoreMaterials));
        EditorGUILayout.PropertyField(ignoreMaterials);
        var ignorePackTextures = serializedObject.FindProperty(nameof(config.ignorePackTextures));
        EditorGUILayout.PropertyField(ignorePackTextures);
        var excludeProperties = serializedObject.FindProperty(nameof(config.excludeProperties));
        EditorGUILayout.PropertyField(excludeProperties);
        EditorGUILayout.LabelField("Property Mapping Rule", Style.BoldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
        var propertyMappingRule = serializedObject.FindProperty(nameof(config.propertyMappingRule));
        propertyMappingRule.stringValue = EditorGUILayout.TextArea(propertyMappingRule.stringValue, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.LabelField("Keyword Mapping Rule", Style.BoldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
        var keywordMappingRule = serializedObject.FindProperty(nameof(config.keywordMappingRule));
        keywordMappingRule.stringValue = EditorGUILayout.TextArea(keywordMappingRule.stringValue, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }
    }
}
