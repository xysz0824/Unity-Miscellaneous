using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using TargetObject = AssetClearance.TargetObject;

public class AssetClearanceWindow : EditorWindow
{
    SerializedObject serializedObject;
    [SerializeField]
    List<TargetObject> targetObjects = new List<TargetObject>();
    SerializedProperty targetObjectsProperty;
    ReorderableList targetObjectsList;
    bool autoSearchRules = true;
    bool searchRulesInTargetRange = true;
    [SerializeField]
    List<AssetClearanceRules> overrideRules = new List<AssetClearanceRules>();
    SerializedProperty overrideRulesProperty;
    ReorderableList overrideRulesList;
    Vector2 scrollPos;

    [MenuItem("Window/AssetClearance/Open")]
    static void Open()
    {
        EditorWindow.GetWindow<AssetClearanceWindow>(false, "Asset Clearance");
    }
    [MenuItem("Window/AssetClearance/Global Check")]
    static void GlobalCheck()
    {
        var name = "AssetReport_" + DateTime.Now.Year;
        name += "_" + DateTime.Now.Month.ToString().PadLeft(2, '0');
        name += "_" + DateTime.Now.Day.ToString().PadLeft(2, '0');
        name += "_" + DateTime.Now.Hour.ToString().PadLeft(2, '0');
        name += "_" + DateTime.Now.Minute.ToString().PadLeft(2, '0');
        var path = EditorUtility.SaveFilePanelInProject("Save As...", name, "asset", "");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var targetObjects = new List<TargetObject> { new TargetObject { obj = AssetDatabase.LoadMainAssetAtPath("Assets") } };
            var reports = AssetClearance.Clear(targetObjects, true, true, null);
            AssetClearanceUtil.SaveReportsToAsset(path, reports, "");
            AssetDatabase.Refresh();
            var reportsAsset = AssetDatabase.LoadAssetAtPath<AssetClearanceReports>(path);
            AssetClearanceReportWindow.Open(reportsAsset, null);
        }
    }
    [MenuItem("Assets/AssetClearance", validate = true)]
    static bool OpenFromProjectValidation()
    {
        var objects = Selection.assetGUIDs;
        if (objects.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(objects[0]);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null) return true;
        }
        return false;
    }
    [MenuItem("Assets/AssetClearance", validate = false)]
    static void OpenFromProject()
    {
        var window = EditorWindow.GetWindow<AssetClearanceWindow>(false, "Asset Clearance");
        window.targetObjectsProperty.ClearArray();
        var objects = Selection.assetGUIDs;
        foreach (var obj in objects)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(obj));
            window.targetObjectsProperty.arraySize++;
            var property = window.targetObjectsProperty.GetArrayElementAtIndex(window.targetObjectsProperty.arraySize - 1);
            property.FindPropertyRelative("obj").objectReferenceValue = asset;
        }
    }
    public static void HandleObjectsDragAndDrop(SerializedProperty objectsProperty, Rect rect)
    {
        var dragDropControlID = GUIUtility.GetControlID("ReorderableListDragAndDrop".GetHashCode(), FocusType.Passive, rect);
        switch (Event.current.GetTypeForControl(dragDropControlID))
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (GUI.enabled && rect.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                    DragAndDrop.activeControlID = dragDropControlID;
                    if (Event.current.type == EventType.DragPerform)
                    {
                        var objectReferences = DragAndDrop.objectReferences;
                        for (int i = 0; i < objectReferences.Length; ++i)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(objectReferences[i]);
                            if (string.IsNullOrEmpty(assetPath)) continue;
                            objectsProperty.arraySize++;
                            var element = objectsProperty.GetArrayElementAtIndex(objectsProperty.arraySize - 1);
                            element.FindPropertyRelative("obj").objectReferenceValue = objectReferences[i];
                        }
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.activeControlID = 0;
                    }
                }
                break;
            case EventType.DragExited:
                if (GUI.enabled)
                {
                    HandleUtility.Repaint();
                }
                break;
        }
    }
    public static void HandleRulesDragAndDrop(SerializedProperty rulesProperty, Rect rect)
    {
        var dragDropControlID = GUIUtility.GetControlID("ReorderableListDragAndDrop".GetHashCode(), FocusType.Passive, rect);
        switch (Event.current.GetTypeForControl(dragDropControlID))
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (GUI.enabled && rect.Contains(Event.current.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                    DragAndDrop.activeControlID = dragDropControlID;
                    if (Event.current.type == EventType.DragPerform)
                    {
                        var objectReferences = DragAndDrop.objectReferences;
                        for (int i = 0; i < objectReferences.Length; ++i)
                        {
                            var assetPath = AssetDatabase.GetAssetPath(objectReferences[i]);
                            if (string.IsNullOrEmpty(assetPath)) continue;
                            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                            if (type != typeof(AssetClearanceRules)) continue;
                            rulesProperty.arraySize++;
                            var element = rulesProperty.GetArrayElementAtIndex(rulesProperty.arraySize - 1);
                            element.objectReferenceValue = objectReferences[i];
                        }
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.activeControlID = 0;
                    }
                }
                break;
            case EventType.DragExited:
                if (GUI.enabled)
                {
                    HandleUtility.Repaint();
                }
                break;
        }
    }
    void OnEnable()
    {
        if (serializedObject == null)
        {
            serializedObject = new SerializedObject(this);
            targetObjectsProperty = serializedObject.FindProperty(nameof(targetObjects));
            targetObjectsList = new ReorderableList(serializedObject, targetObjectsProperty, false, true, true, true);
            targetObjectsList.drawHeaderCallback = (rect) =>
            {
                EditorGUI.LabelField(rect, "Target Objects");
                HandleObjectsDragAndDrop(targetObjectsProperty, rect);
            };
            targetObjectsList.elementHeight = EditorGUIUtility.singleLineHeight * 2;
            targetObjectsList.drawNoneElementCallback = (rect) => HandleObjectsDragAndDrop(targetObjectsProperty, rect);
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
            overrideRulesProperty = serializedObject.FindProperty(nameof(overrideRules));
            overrideRulesList = new ReorderableList(serializedObject, overrideRulesProperty, false, true, true, true);
            overrideRulesList.drawHeaderCallback = (rect) =>
            {
                EditorGUI.LabelField(rect, "Override Rules");
                HandleRulesDragAndDrop(overrideRulesProperty, rect);
            };
            overrideRulesList.drawNoneElementCallback = (rect) => HandleRulesDragAndDrop(overrideRulesProperty, rect);
            overrideRulesList.drawElementCallback = (rect, index, active, focused) =>
            {
                var element = overrideRulesProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, element, GUIContent.none);
            };
#if UNITY_2021_1_OR_NEWER
            overrideRulesList.multiSelect = true;
#endif
        }
    }
    void Clear()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var reports = AssetClearance.Clear(targetObjects, autoSearchRules, searchRulesInTargetRange, overrideRules);
        AssetClearanceReportWindow.Open(null, reports);
    }
    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Auto Search Rules");
        autoSearchRules = EditorGUILayout.Toggle(autoSearchRules);
        EditorGUILayout.EndHorizontal();
        if (!autoSearchRules)
        {
            overrideRulesList.DoLayoutList();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search Rules in Target Objects");
            searchRulesInTargetRange = EditorGUILayout.Toggle(searchRulesInTargetRange);
            EditorGUILayout.EndHorizontal();
        }
        targetObjectsList.DoLayoutList();
        EditorGUI.BeginDisabledGroup(targetObjects.Count == 0 || (!autoSearchRules && overrideRules.Count == 0));
        if (GUILayout.Button("Start"))
        {
            Clear();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndScrollView();
        serializedObject.ApplyModifiedProperties();
        serializedObject.UpdateIfRequiredOrScript();
    }
}
