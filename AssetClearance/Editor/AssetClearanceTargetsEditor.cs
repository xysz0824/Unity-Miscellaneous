using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

[CustomEditor(typeof(AssetClearanceTargets))]
public class AssetClearanceTargetsEditor : Editor
{
    SerializedProperty targetObjectsProperty;
    ReorderableList targetObjectsList;
    SerializedProperty overrideRulesProperty;
    ReorderableList overrideRulesList;
    Vector2 scrollPos;
    void OnEnable()
    {
        var targets = target as AssetClearanceTargets;
        targetObjectsProperty = serializedObject.FindProperty(nameof(targets.targetObjects));
        targetObjectsList = new ReorderableList(serializedObject, targetObjectsProperty, false, true, true, true);
        targetObjectsList.drawHeaderCallback = (rect) =>
        {
            EditorGUI.LabelField(rect, "Target Objects");
            AssetClearanceWindow.HandleObjectsDragAndDrop(targetObjectsProperty, rect);
        };
        targetObjectsList.elementHeight = EditorGUIUtility.singleLineHeight * 2;
        targetObjectsList.drawNoneElementCallback = (rect) => AssetClearanceWindow.HandleObjectsDragAndDrop(targetObjectsProperty, rect);
        targetObjectsList.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.height = EditorGUIUtility.singleLineHeight;
            var element = targetObjectsProperty.GetArrayElementAtIndex(index);
            var objProperty = element.FindPropertyRelative("obj");
            EditorGUI.PropertyField(rect, objProperty, GUIContent.none);
        };
#if UNITY_2021_1_OR_NEWER
            targetObjectsList.multiSelect = true;
#endif
        overrideRulesProperty = serializedObject.FindProperty(nameof(targets.overrideRules));
        overrideRulesList = new ReorderableList(serializedObject, overrideRulesProperty, false, true, true, true);
        overrideRulesList.drawHeaderCallback = (rect) =>
        {
            EditorGUI.LabelField(rect, "Override Rules");
            AssetClearanceWindow.HandleRulesDragAndDrop(overrideRulesProperty, rect);
        };
        overrideRulesList.drawNoneElementCallback = (rect) => AssetClearanceWindow.HandleRulesDragAndDrop(overrideRulesProperty, rect);
        overrideRulesList.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = overrideRulesProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };
#if UNITY_2021_1_OR_NEWER
            overrideRulesList.multiSelect = true;
#endif
    }
    public override void OnInspectorGUI()
    {
        var targets = target as AssetClearanceTargets;
        var autoSearchRulesProperty = serializedObject.FindProperty(nameof(targets.autoSearchRules));
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
        EditorGUILayout.PropertyField(autoSearchRulesProperty);
        if (!targets.autoSearchRules)
        {
            overrideRulesList.DoLayoutList();
        }
        else
        {
            var searchRulesInTargetRangeProperty = serializedObject.FindProperty(nameof(targets.searchRulesInTargetRange));
            EditorGUILayout.PropertyField(searchRulesInTargetRangeProperty, new GUIContent("Search Rules in Target Objects"));
        }
        targetObjectsList.DoLayoutList();
        EditorGUI.BeginDisabledGroup(targets.targetObjects.Count == 0 || (!targets.autoSearchRules && targets.overrideRules.Count == 0));
        if (GUILayout.Button("Clear"))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var reports = AssetClearance.Clear(targets.targetObjects, targets.autoSearchRules, targets.searchRulesInTargetRange, targets.overrideRules);
            AssetClearanceReportWindow.Open(null, reports);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndScrollView();
        serializedObject.ApplyModifiedProperties();
        serializedObject.UpdateIfRequiredOrScript();
    }
}
