using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System;
using UnityEditorInternal;
using System.Reflection;
using RulesEditor = AssetClearanceRulesEditor;
using Style = AssetClearanceRulesEditor.Style;
using System.Text.RegularExpressions;

[CustomEditor(typeof(AssetClearanceRuleTemplate))]
public class AssetClearanceRuleTemplateEditor : Editor
{
    static Dictionary<Type, AssetClearanceStringTreeView> stringTreeViewDict;
    static Dictionary<string, bool> parameterFoldoutDict = new Dictionary<string, bool>();
    static SerializedObject copyingElementSerializedObject;
    static string copyingElementPath;
    static string copyingElementParentPath;
    static bool cutingElement;

    SerializedProperty intelliSenseProperty;
    Rect intelliSenseTextRect;
    TreeView intelliSenseTreeView;
    Action onConfirmIntelliSense;
    SerializedProperty unappliedSaveProperty;
    ReorderableList ruleConditionList;
    List<MethodInfo> objectMethods;
    List<MethodInfo> objectFixMethods;
    Dictionary<string, MethodInfo> methodValidationCache;
    Dictionary<string, bool> parameterValidationCache;
    bool keepParameterFoldout;
    MethodInfo clearCacheMethod;
    PropertyInfo currentGUIView;
    bool needRecalculateProperty = false;
    AssetClearanceMethodTreeView objectMethodTreeView;
    AssetClearanceMethodTreeView objectFixMethodTreeView;
    Color backgroundColor;
    SerializedProperty selectedElement;
    SerializedProperty selectedElementParent;
    void BlockMouseEvent(Rect textRect)
    {
        var rect = RulesEditor.Space(textRect, textRect.height);
        rect.height *= Style.IntelliSenseDisplayNum;
        if (rect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp))
        {
            Event.current.Use();
        }
    }
    int LineCountOfParameters(SerializedProperty methodNameProperty, SerializedProperty methodProperty)
    {
        int lineCount = 0;
        if (methodValidationCache.ContainsKey(methodNameProperty.propertyPath))
        {
            var paramCountsProperty = AssetClearanceUtil.GetParamCountsPropertyDict(methodProperty, true);
            var methodInfo = methodValidationCache[methodNameProperty.propertyPath];
            if (methodInfo == null) return lineCount;
            var parameters = methodInfo.GetParameters();
            var paramCountsIndex = AssetClearanceUtil.GetParamCountsIndexDict();
            for (int i = 1; i < parameters.Length; ++i)
            {
                if (parameters[i].Name.ToLower() == AssetClearanceUtil.PingObjectsName) continue;
                var parameterType = parameters[i].ParameterType;
                if (parameters[i].ParameterType.IsEnum) parameterType = AssetClearanceUtil.IntType;
                if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsEnum) parameterType = AssetClearanceUtil.IntArrayType;
                if (parameters[i].ParameterType.IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectType;
                if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectArrayType;
                if (paramCountsProperty.ContainsKey(parameterType))
                {
                    SerializedProperty countProperty = paramCountsProperty[parameterType].GetArrayElementAtIndex(paramCountsIndex[parameterType].value++);
                    lineCount += !parameterFoldoutDict[countProperty.propertyPath] ? 0 : countProperty.intValue;
                }
                lineCount += 1;
            }
        }
        return lineCount;
    }
    float HeightOfCondition(SerializedProperty property)
    {
        var methodProperty = property.FindPropertyRelative("method");
        var methodNameProperty = methodProperty.FindPropertyRelative("name");
        var height = 2 + LineCountOfParameters(methodNameProperty, methodProperty);
        return height * RulesEditor.GetPropertyBorderHeight(methodNameProperty);
    }
    void SortConditionsByPriority(SerializedProperty conditionsProperty)
    {
        int swapTime;
        do
        {
            swapTime = 0;
            for (int i = 0; i < conditionsProperty.arraySize - 1; ++i)
            {
                var conditionI = conditionsProperty.GetArrayElementAtIndex(i);
                var conditionIplus1 = conditionsProperty.GetArrayElementAtIndex(i + 1);
                if (conditionI.FindPropertyRelative("priority").intValue < conditionIplus1.FindPropertyRelative("priority").intValue)
                {
                    swapTime++;
                    conditionsProperty.MoveArrayElement(i, i + 1);
                }
            }
        }
        while (swapTime != 0);
        for (int i = 0; i < conditionsProperty.arraySize; ++i)
        {
            var condition = conditionsProperty.GetArrayElementAtIndex(i);
            methodValidationCache.Remove(condition.FindPropertyRelative("method.name").propertyPath);
        }
    }
    void OnAddElementOfConditionList(ReorderableList list)
    {
        list.serializedProperty.arraySize++;
        var element = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
        selectedElement = element;
        selectedElementParent = list.serializedProperty;
        AssetClearanceUtil.ResetSerializedProperty(element);
        serializedObject.ApplyModifiedProperties();
        SortConditionsByPriority(list.serializedProperty);
        list.index = list.serializedProperty.arraySize - 1;
    }
    void OnRemoveElementOfConditionList(ReorderableList list)
    {
        ReorderableList.defaultBehaviours.DoRemoveButton(list);
        if (list.serializedProperty.arraySize > 0)
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(list.index);
            selectedElement = element;
            selectedElementParent = list.serializedProperty;
        }
        else
        {
            selectedElement = null;
            selectedElementParent = null;
        }
        for (int i = 0; i < list.serializedProperty.arraySize; ++i)
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(i);
            methodValidationCache.Remove(element.FindPropertyRelative("method.name").propertyPath);
        }
    }
    void OnSelectElement(ReorderableList list)
    {
        selectedElement = list.serializedProperty.GetArrayElementAtIndex(list.index);
        selectedElementParent = list.serializedProperty;
    }
    void BeginMethodValidation(SerializedProperty methodNameProperty)
    {
        backgroundColor = GUI.backgroundColor;
        if (!string.IsNullOrEmpty(methodNameProperty.stringValue) && methodValidationCache.ContainsKey(methodNameProperty.propertyPath) && methodValidationCache[methodNameProperty.propertyPath] == null)
        {
            GUI.backgroundColor = Style.InvalidColor;
        }
    }
    void EndMethodValidation()
    {
        GUI.backgroundColor = backgroundColor;
    }
    void BeginParameterValidation(SerializedProperty property)
    {
        backgroundColor = GUI.backgroundColor;
        if (parameterValidationCache.ContainsKey(property.propertyPath) && !parameterValidationCache[property.propertyPath])
        {
            GUI.backgroundColor = Style.InvalidColor;
        }
    }
    void EndParameterValidaton()
    {
        GUI.backgroundColor = backgroundColor;
    }
    void OnConfirmMethodIntelliSense()
    {
        methodValidationCache.Remove(intelliSenseProperty.propertyPath);
        var prefix = intelliSenseProperty.propertyPath.Substring(0, intelliSenseProperty.propertyPath.Length - 6);
        var removeKeys = new List<string>();
        foreach (var propertyPath in parameterFoldoutDict.Keys)
        {
            if (propertyPath.StartsWith(prefix)) removeKeys.Add(propertyPath);
        }
        foreach (var key in removeKeys)
        {
            parameterFoldoutDict.Remove(key);
        }
        keepParameterFoldout = true;
    }
    void OnConfirmParameterIntelliSense()
    {
        parameterValidationCache.Remove(intelliSenseProperty.propertyPath);
    }
    void OnConfirmFixMethodIntellSense()
    {
        methodValidationCache.Remove(intelliSenseProperty.propertyPath);
    }
    void UpdateIntellisense()
    {
        intelliSenseTreeView.searchString = intelliSenseProperty.stringValue;
        intelliSenseTreeView.state.selectedIDs.Clear();
    }
    void DrawIntellisense()
    {
        if (intelliSenseProperty == null) return;
        var focusRect = intelliSenseTextRect;
        focusRect.height *= (Style.IntelliSenseDisplayNum + 1);
        var clickRect = RulesEditor.Space(intelliSenseTextRect, intelliSenseTextRect.height);
        clickRect.height *= Style.IntelliSenseDisplayNum;
        clickRect.width -= 20;
        if (!focusRect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp))
        {
            if (onConfirmIntelliSense != null)
            {
                onConfirmIntelliSense();
            }
            intelliSenseProperty = null;
            Event.current.Use();
            GUI.FocusControl(null);
        }
        else if ((clickRect.Contains(Event.current.mousePosition) && Event.current.isMouse && Event.current.clickCount >= 2) ||
            (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)))
        {
            if (onConfirmIntelliSense != null)
            {
                onConfirmIntelliSense();
            }
            var selection = intelliSenseTreeView.GetSelection();
            if (selection.Count > 0)
            {
                var rows = intelliSenseTreeView.GetRows();
                foreach (var row in rows)
                {
                    var item = AssetClearanceUtil.GetTreeViewLeafRecursively(row, selection[0]);
                    if (item != null)
                    {
                        intelliSenseProperty.stringValue = item.displayName;
                        break;
                    }
                }
            }
            intelliSenseProperty = null;
            Event.current.Use();
            GUI.FocusControl("intellisense");
            intelliSenseTreeView.state.selectedIDs.Clear();
        }
        else if (!intelliSenseTreeView.HasFocus() && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.DownArrow || Event.current.keyCode == KeyCode.UpArrow))
        {
            Event.current.Use();
            GUI.FocusControl("intellisense");
            var items = intelliSenseTreeView.GetRows();
            if (items.Count > 0)
            {
                intelliSenseTreeView.SetSelection(new int[] { items[0].id }, TreeViewSelectionOptions.RevealAndFrame | TreeViewSelectionOptions.FireSelectionChanged);
                intelliSenseTreeView.SetFocusAndEnsureSelectedItem();
            }
        }
        else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            intelliSenseProperty = null;
            Event.current.Use();
            GUI.FocusControl(null);
        }
        if (intelliSenseProperty != null)
        {
            var boxRect = RulesEditor.Space(intelliSenseTextRect, intelliSenseTextRect.height);
            boxRect.height *= Style.IntelliSenseDisplayNum;
            GUI.Box(boxRect, GUIContent.none, Style.Box);
            GUI.Box(RulesEditor.Deflate(boxRect, 1, 1), GUIContent.none, Style.Window);
            GUI.SetNextControlName("intellisense");
            intelliSenseTreeView.OnGUI(RulesEditor.Deflate(boxRect, 1, 1));
        }
    }
    void DrawMethodTip()
    {
        if (intelliSenseProperty == null || !(intelliSenseTreeView is AssetClearanceMethodTreeView)) return;
        var treeView = intelliSenseTreeView as AssetClearanceMethodTreeView;
        var rect = RulesEditor.Space(intelliSenseTextRect, intelliSenseTextRect.height);
        rect.height *= Style.IntelliSenseDisplayNum;
        if (treeView.LastSelectedItem != null && !treeView.LastSelectedItem.hasChildren)
        {
            var item = treeView.LastSelectedItem as AssetClearanceMethodTreeView.Item;
            rect.x -= 274;
            rect.width = 275;
            var titleHeight = 26;
            var attribute = item.method.GetCustomAttribute<AssetClearanceMethod>();
            var content = new GUIContent(attribute.Tip);
            var contentSize = Style.MethodTip.CalcHeight(content, rect.width) + Style.MethodTip.lineHeight + 2;
            rect.height = titleHeight + contentSize;
            rect.y += treeView.LastSelectedLine * treeView.RowHeight;
            GUI.Box(rect, GUIContent.none, Style.Box);
            GUI.Box(RulesEditor.Deflate(rect, 1, 1), GUIContent.none, Style.Window);
            var labelRect = rect;
            labelRect.height = titleHeight;
            GUI.Label(RulesEditor.Deflate(labelRect, 2, 2), item.displayName, Style.MethodTipTitle);
            labelRect = RulesEditor.Space(labelRect, labelRect.height);
            labelRect.height = rect.height - titleHeight;
            GUI.Label(RulesEditor.Deflate(labelRect, 2, 0), content, Style.MethodTip);
        }
    }
    bool ValidateParameter(ParameterInfo parameterInfo, SerializedProperty parameterProperty)
    {
        var attribute = parameterInfo.GetCustomAttribute<ParameterValidation>();
        if (attribute == null) return true;
        return attribute.DoValidate(AssetClearanceUtil.GetSerializedPropertyValue(parameterProperty));
    }
    MethodInfo ValidateMethod(List<MethodInfo> methods, SerializedProperty methodNameProperty, SerializedProperty methodProperty)
    {
        if (string.IsNullOrEmpty(methodNameProperty.stringValue)) return null;
        var methodInfo = methods.Find((method) => method.Name == methodNameProperty.stringValue);
        if (methodInfo != null)
        {
            var parameters = methodInfo.GetParameters();
            var paramCountsProperty = AssetClearanceUtil.GetParamCountsPropertyDict(methodProperty);
            var parametersProperty = AssetClearanceUtil.GetParametersPropertyDict(methodProperty);
            var paramCountsIndex = AssetClearanceUtil.GetParamCountsIndexDict();
            var paramCount = AssetClearanceUtil.GetParamCountict();
            for (int i = 1; i < parameters.Length; ++i)
            {
                if (parameters[i].Name.ToLower() == AssetClearanceUtil.PingObjectsName) continue;
                var parameterType = parameters[i].ParameterType;
                if (parameters[i].ParameterType.IsEnum) parameterType = AssetClearanceUtil.IntType;
                if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsEnum) parameterType = AssetClearanceUtil.IntArrayType;
                if (parameters[i].ParameterType.IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectType;
                if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectArrayType;
                if (paramCountsProperty[parameterType].arraySize <= paramCountsIndex[parameterType].value)
                {
                    paramCountsProperty[parameterType].arraySize++;
                }
                var countProperty = paramCountsProperty[parameterType].GetArrayElementAtIndex(paramCountsIndex[parameterType].value++);
                countProperty.intValue = Mathf.Max(1, countProperty.intValue);
                paramCount[parameterType].value += countProperty.intValue;
                if (!parameterFoldoutDict.ContainsKey(countProperty.propertyPath))
                {
                    parameterFoldoutDict[countProperty.propertyPath] = keepParameterFoldout;
                }
                if (parametersProperty[parameterType].arraySize < paramCount[parameterType].value)
                {
                    parametersProperty[parameterType].arraySize++;
                    AssetClearanceUtil.ResetSerializedPropertyValue(parametersProperty[parameterType].GetArrayElementAtIndex(parametersProperty[parameterType].arraySize - 1));
                    parametersProperty[parameterType].arraySize += countProperty.intValue - 1;
                }
                for (int k = paramCount[parameterType].value - countProperty.intValue; k < countProperty.intValue; ++k)
                {
                    var property = parametersProperty[parameterType].GetArrayElementAtIndex(k);
                    parameterValidationCache.Remove(property.propertyPath);
                }
            }
        }
        if (keepParameterFoldout)
        {
            keepParameterFoldout = false;
        }
        return methodInfo;
    }
    Rect DrawMethodParameters(Rect propertyRect, int labelWidth, Rect inputFieldRect, SerializedProperty methodNameProperty, SerializedProperty methodProperty)
    {
        var propertyHeight = RulesEditor.GetPropertyBorderHeight(methodNameProperty);
        if (methodValidationCache.ContainsKey(methodNameProperty.propertyPath) && methodValidationCache[methodNameProperty.propertyPath] != null)
        {
            var parameters = methodValidationCache[methodNameProperty.propertyPath].GetParameters();
            var paramCountsProperty = AssetClearanceUtil.GetParamCountsPropertyDict(methodProperty);
            var parametersProperty = AssetClearanceUtil.GetParametersPropertyDict(methodProperty);
            var paramCountsIndex = AssetClearanceUtil.GetParamCountsIndexDict();
            var paramCount = AssetClearanceUtil.GetParamCountict();
            for (int i = 1; i < parameters.Length; ++i)
            {
                if (parameters[i].Name.ToLower() == AssetClearanceUtil.PingObjectsName) continue;
                var parameterType = parameters[i].ParameterType;
                if (parameters[i].ParameterType.IsEnum) parameterType = AssetClearanceUtil.IntType;
                if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsEnum) parameterType = AssetClearanceUtil.IntArrayType;
                if (parameters[i].ParameterType.IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectType;
                if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectArrayType;
                var validation = parameters[i].GetCustomAttribute<ParameterValidation>();
                var validationType = validation == null ? null : validation.GetType();
                var displayName = AssetClearanceUtil.GetDisplayName(parameters[i].Name);
                propertyRect = RulesEditor.Space(propertyRect, propertyHeight);
                var labelRect = propertyRect;
                labelRect.width = labelWidth;
                if (!parameterType.IsArray)
                {
                    EditorGUI.LabelField(RulesEditor.Deflate(labelRect, 4, 2), "- " + displayName);
                    var parameterProperty = parametersProperty[parameterType].GetArrayElementAtIndex(paramCount[parameterType].value++);
                    GUI.SetNextControlName(parameterProperty.propertyPath);
                    if (validationType != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        BeginParameterValidation(parameterProperty);
                        inputFieldRect = RulesEditor.Deflate(RulesEditor.Indent(propertyRect, labelWidth), 4, 2);
                        parameterProperty.stringValue = EditorGUI.TextField(inputFieldRect, parameterProperty.stringValue);
                        EndParameterValidaton();
                        if ((EditorGUI.EndChangeCheck() || GUI.GetNameOfFocusedControl() == parameterProperty.propertyPath) && stringTreeViewDict.ContainsKey(validationType))
                        {
                            intelliSenseTreeView = stringTreeViewDict[validationType];
                            intelliSenseProperty = parameterProperty;
                            if (Event.current.type != EventType.Layout) intelliSenseTextRect = inputFieldRect;
                            onConfirmIntelliSense = OnConfirmParameterIntelliSense;
                            if (EditorGUI.EndChangeCheck()) UpdateIntellisense();
                        }
                        if (EditorGUI.EndChangeCheck())
                        {
                            parameterValidationCache.Remove(parameterProperty.propertyPath);
                        }
                        if (!parameterValidationCache.ContainsKey(parameterProperty.propertyPath))
                        {
                            parameterValidationCache[parameterProperty.propertyPath] = ValidateParameter(parameters[i], parameterProperty);
                        }
                    }
                    else if (parameters[i].ParameterType.IsEnum)
                    {
                        RulesEditor.EnumField(parameters[i].ParameterType, RulesEditor.Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty);
                    }
                    else
                    {
                        RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty, parameters[i].ParameterType);
                    }
                }
                else
                {
                    var countProperty = paramCountsProperty[parameterType].GetArrayElementAtIndex(paramCountsIndex[parameterType].value);
                    labelRect.x += 12;
                    parameterFoldoutDict[countProperty.propertyPath] = EditorGUI.Foldout(RulesEditor.Deflate(labelRect, 4, 2), parameterFoldoutDict[countProperty.propertyPath], GUIContent.none);
                    EditorGUI.LabelField(RulesEditor.Deflate(labelRect, 4, 2), displayName + " [" + countProperty.intValue + "]");
                    if (parameterFoldoutDict[countProperty.propertyPath])
                    {
                        for (int k = 0; k < countProperty.intValue; ++k)
                        {
                            var parameterProperty = parametersProperty[parameterType].GetArrayElementAtIndex(paramCount[parameterType].value + k);
                            propertyRect.width -= 22;
                            GUI.SetNextControlName(parameterProperty.propertyPath);
                            if (validationType != null)
                            {
                                EditorGUI.BeginChangeCheck();
                                BeginParameterValidation(parameterProperty);
                                inputFieldRect = RulesEditor.Deflate(RulesEditor.Indent(propertyRect, labelWidth), 4, 2);
                                parameterProperty.stringValue = EditorGUI.TextField(inputFieldRect, parameterProperty.stringValue);
                                EndParameterValidaton();
                                if ((EditorGUI.EndChangeCheck() || GUI.GetNameOfFocusedControl() == parameterProperty.propertyPath) && stringTreeViewDict.ContainsKey(validationType))
                                {
                                    intelliSenseTreeView = stringTreeViewDict[validationType];
                                    intelliSenseProperty = parameterProperty;
                                    if (Event.current.type != EventType.Layout) intelliSenseTextRect = inputFieldRect;
                                    onConfirmIntelliSense = OnConfirmParameterIntelliSense;
                                    if (EditorGUI.EndChangeCheck()) UpdateIntellisense();
                                }
                                if (EditorGUI.EndChangeCheck())
                                {
                                    parameterValidationCache.Remove(parameterProperty.propertyPath);
                                }
                                if (!parameterValidationCache.ContainsKey(parameterProperty.propertyPath))
                                {
                                    parameterValidationCache[parameterProperty.propertyPath] = ValidateParameter(parameters[i], parameterProperty);
                                }
                            }
                            else if (parameters[i].ParameterType.GetElementType().IsEnum)
                            {
                                RulesEditor.EnumField(parameters[i].ParameterType.GetElementType(), RulesEditor.Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty);
                            }
                            else
                            {
                                RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty, parameters[i].ParameterType.GetElementType());
                            }
                            propertyRect.width += 22;
                            if (GUI.Button(RulesEditor.Deflate(RulesEditor.Indent(propertyRect, propertyRect.width - 30), 4, 2), "-"))
                            {
                                if (countProperty.intValue > 1)
                                {
                                    countProperty.intValue--;
                                    parametersProperty[parameterType].DeleteArrayElementAtIndex(paramCount[parameterType].value + k);
                                    k--;
                                }
                                methodValidationCache.Remove(methodNameProperty.propertyPath);
                            }
                            propertyRect = RulesEditor.Space(propertyRect, propertyHeight);
                        }
                        if (GUI.Button(RulesEditor.Deflate(RulesEditor.Indent(propertyRect, propertyRect.width - 30), 4, 2), "+"))
                        {
                            countProperty.intValue++;
                            parametersProperty[parameterType].arraySize++;
                            var parametersIndex = 0;
                            for (int k = 0; k < paramCountsIndex[parameterType].value; ++k)
                            {
                                parametersIndex += paramCountsProperty[parameterType].GetArrayElementAtIndex(k).intValue;
                            }
                            for (int k = parametersProperty[parameterType].arraySize - 2; k >= parametersIndex + countProperty.intValue - 1; --k)
                            {
                                parametersProperty[parameterType].MoveArrayElement(k, k + 1);
                            }
                            AssetClearanceUtil.ResetSerializedPropertyValue(parametersProperty[parameterType].GetArrayElementAtIndex(parametersIndex + countProperty.intValue - 1));
                            methodValidationCache.Remove(methodNameProperty.propertyPath);
                        }
                    }
                    paramCount[parameterType].value += countProperty.intValue;
                }
                paramCountsIndex[parameterType].value++;
            }
        }
        return propertyRect;
    }
    void DrawCondition(Rect rect, int index, SerializedProperty listProperty, SerializedProperty conditionProperty)
    {
        GUI.Box(rect, GUIContent.none, Style.Box);
        rect = RulesEditor.Deflate(rect, 16, 0);
        var methodProperty = conditionProperty.FindPropertyRelative("method");
        var methodNameProperty = methodProperty.FindPropertyRelative("name");
        var negationProperty = conditionProperty.FindPropertyRelative("negation");
        var propertyHeight = RulesEditor.GetPropertyBorderHeight(methodNameProperty);
        if (listProperty.arraySize > 1 && currentGUIView.GetValue(null) != null)
        {
            var priorityRect = rect;
            priorityRect.width = 50;
            priorityRect.height = propertyHeight;
            EditorGUI.LabelField(RulesEditor.Deflate(priorityRect, 4, 2), "Priority");
            priorityRect = RulesEditor.Space(priorityRect, propertyHeight);
            EditorGUI.BeginChangeCheck();
            var priorityProperty = conditionProperty.FindPropertyRelative("priority");
            GUI.SetNextControlName(priorityProperty.propertyPath);
            priorityProperty.intValue = EditorGUI.DelayedIntField(RulesEditor.Deflate(priorityRect, 4, 2), priorityProperty.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                SortConditionsByPriority(listProperty);
                Repaint();
                return;
            }
        }
        int labelWidth = 160;
        var propertyRect = rect;
        propertyRect = RulesEditor.Indent(propertyRect, 70);
        propertyRect.height = propertyHeight;
        EditorGUI.BeginChangeCheck();
        BeginMethodValidation(methodNameProperty);
        var inputFieldRect = RulesEditor.MethodField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, methodNameProperty, methodProperty, negationProperty);
        EndMethodValidation();
        if (EditorGUI.EndChangeCheck() || GUI.GetNameOfFocusedControl() == methodNameProperty.propertyPath)
        {
            intelliSenseTreeView = objectMethodTreeView;
            if (intelliSenseProperty == null || intelliSenseProperty.propertyPath != methodNameProperty.propertyPath)
            {
                intelliSenseTreeView.searchString = methodNameProperty.stringValue;
                if (!string.IsNullOrWhiteSpace(intelliSenseTreeView.searchString))
                {
                    var rows = intelliSenseTreeView.GetRows();
                    if (rows.Count > 0)
                    {
                        intelliSenseTreeView.SetSelection(new List<int> { rows[0].id });
                    }
                }
            }
            intelliSenseProperty = methodNameProperty;
            if (Event.current.type != EventType.Layout) intelliSenseTextRect = inputFieldRect;
            onConfirmIntelliSense = OnConfirmMethodIntelliSense;
            if (EditorGUI.EndChangeCheck()) UpdateIntellisense();
        }
        propertyRect = DrawMethodParameters(propertyRect, labelWidth, inputFieldRect, methodNameProperty, methodProperty);
        propertyRect = RulesEditor.Space(propertyRect, propertyHeight);
        if (index < listProperty.arraySize - 1)
        {
            var logicOperatorProperty = conditionProperty.FindPropertyRelative("logicOperator");
            RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, logicOperatorProperty, null);
        }
    }
    void RebuildRuleAndCache()
    {
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        var conditionsProperty = unappliedSaveProperty.FindPropertyRelative("conditions");
        ruleConditionList = new ReorderableList(serializedObject, conditionsProperty, false, true, true, true);
        ruleConditionList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Conditions", Style.BoldLabel);
        ruleConditionList.elementHeightCallback = (index) =>
        {
            var conditionProperty = conditionsProperty.GetArrayElementAtIndex(index);
            return HeightOfCondition(conditionProperty);
        };
        ruleConditionList.onAddCallback = OnAddElementOfConditionList;
        ruleConditionList.onRemoveCallback = OnRemoveElementOfConditionList;
        ruleConditionList.drawElementCallback = (rect, index, active, focused) =>
        {
            var conditionProperty = conditionsProperty.GetArrayElementAtIndex(index);
            DrawCondition(rect, index, conditionsProperty, conditionProperty);
        };
        ruleConditionList.onSelectCallback = OnSelectElement;
#if UNITY_2021_1_OR_NEWER
        ruleConditionList.multiSelect = true;
#endif
        methodValidationCache = new Dictionary<string, MethodInfo>();
        parameterValidationCache = new Dictionary<string, bool>();
    }
    void OnEnable()
    {
        if (stringTreeViewDict == null)
        {
            stringTreeViewDict = new Dictionary<Type, AssetClearanceStringTreeView>();
            var assembly = Assembly.GetExecutingAssembly();
            var types = assembly.GetExportedTypes();
            foreach (var type in types)
            {
                if (!type.IsSubclassOf(typeof(ParameterValidation))) continue;
                stringTreeViewDict[type] = new AssetClearanceStringTreeView(new TreeViewState(), (assembly.CreateInstance(type.FullName) as ParameterValidation).GetItems());
            }
        }
        var template = target as AssetClearanceRuleTemplate;
        unappliedSaveProperty = serializedObject.FindProperty(nameof(template.unappliedSave));
        RebuildRuleAndCache();
        Undo.undoRedoPerformed += RebuildRuleAndCache;
        clearCacheMethod = typeof(ReorderableList).GetMethod("ClearCache", BindingFlags.Instance | BindingFlags.NonPublic);
        if (clearCacheMethod == null)
        {
            clearCacheMethod = typeof(ReorderableList).GetMethod("InvalidateCache", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        currentGUIView = typeof(EditorGUI).Assembly.GetType("UnityEditor.GUIView").GetProperty("current");
        objectMethods = AssetClearanceUtil.GetMethods();
        objectFixMethods = AssetClearanceUtil.GetFixMethods();
        objectMethodTreeView = new AssetClearanceMethodTreeView(new TreeViewState(), objectMethods);
        objectFixMethodTreeView = new AssetClearanceMethodTreeView(new TreeViewState(), objectFixMethods);
    }
    void OnDisable()
    {
        Undo.undoRedoPerformed -= RebuildRuleAndCache;
    }
    void OnDestroy()
    {
        Undo.undoRedoPerformed -= RebuildRuleAndCache;
    }
    static readonly Regex indexRegex = new Regex("\\[([0-9]+?)\\]");
    void CopyAndPaste()
    {
        if (Event.current.type == EventType.KeyUp && Event.current.control && GUI.GetNameOfFocusedControl() == "")
        {
            if (selectedElement != null && (Event.current.keyCode == KeyCode.C || Event.current.keyCode == KeyCode.X))
            {
                cutingElement = Event.current.keyCode == KeyCode.X;
                copyingElementSerializedObject = new SerializedObject(target);
                copyingElementPath = selectedElement.propertyPath;
                copyingElementParentPath = selectedElementParent.propertyPath;
            }
            else if (Event.current.keyCode == KeyCode.V && copyingElementSerializedObject != null)
            {
                SerializedProperty targetElementParent = null;
                var copyingElement = copyingElementSerializedObject.FindProperty(copyingElementPath);
                var copyingElementParent = copyingElementSerializedObject.FindProperty(copyingElementParentPath);
                if (selectedElement != null)
                {
                    if (selectedElementParent.arrayElementType == copyingElementParent.arrayElementType)
                    {
                        targetElementParent = selectedElementParent;
                    }
                    else
                    {
                        var it = selectedElement.Copy();
                        var end = it.GetEndProperty();
                        it.NextVisible(true);
                        while (it.propertyPath != end.propertyPath)
                        {
                            if (it.isArray && it.arrayElementType == copyingElementParent.arrayElementType)
                            {
                                targetElementParent = it;
                                break;
                            }
                            it.NextVisible(false);
                        }
                    }
                }
                if (targetElementParent == null)
                {
                    targetElementParent = serializedObject.FindProperty(copyingElementParentPath);
                }
                if (targetElementParent != null && copyingElement != null)
                {
                    targetElementParent.arraySize++;
                    AssetClearanceUtil.CopySerializedProperty(copyingElement, targetElementParent.GetArrayElementAtIndex(targetElementParent.arraySize - 1));
                    if (cutingElement)
                    {
                        if (copyingElementSerializedObject.targetObject == serializedObject.targetObject)
                        {
                            copyingElementSerializedObject = serializedObject;
                            copyingElementParent = copyingElementSerializedObject.FindProperty(copyingElementParentPath);
                        }
                        var index = indexRegex.Match(copyingElementPath.Substring(copyingElementParentPath.Length)).Groups[1].Value;
                        copyingElementParent.DeleteArrayElementAtIndex(int.Parse(index));
                        if (copyingElementSerializedObject.targetObject != serializedObject.targetObject)
                        {
                            copyingElementSerializedObject.ApplyModifiedProperties();
                            copyingElementSerializedObject.Update();
                        }
                    }
                    RebuildRuleAndCache();
                }
            }
        }
    }
    public override bool RequiresConstantRepaint()
    {
        return true;
    }
    static readonly Regex ruleRegex = new Regex("ruleList.Array.data\\[[0-9]+?\\](.*)");
    void ApplyToAllRules()
    {
        var rulesList = AssetClearanceUtil.AutoSearchRules();
        int count = 0;
        foreach (var rules in rulesList)
        {
            count++;
            if (count % 5 == 0) EditorUtility.DisplayProgressBar("Applying", AssetDatabase.GetAssetPath(rules), (float)count / rulesList.Count);
            var serializedObject = new SerializedObject(rules);
            var rulesListProperty = serializedObject.FindProperty("ruleList");
            for (int i = 0; i < rulesListProperty.arraySize; ++i)
            {
                var ruleProperty = rulesListProperty.GetArrayElementAtIndex(i);
                var templateProperty = ruleProperty.FindPropertyRelative("template");
                if (templateProperty.objectReferenceValue == target)
                {
                    var ruleIt = ruleProperty.Copy();
                    var thisRuleIt = this.serializedObject.FindProperty("rule").Copy();
                    var ruleEnd = ruleProperty.GetEndProperty();
                    ruleIt.NextVisible(true);
                    thisRuleIt.NextVisible(true);
                    while (ruleIt.propertyPath != ruleEnd.propertyPath)
                    {
                        if (AssetClearanceUtil.EqualSerializedProperty(ruleIt, thisRuleIt))
                        {
                            var path = ruleRegex.Match(ruleIt.propertyPath).Groups[1];
                            var newValueProperty = this.serializedObject.FindProperty("unappliedSave" + path);
                            AssetClearanceUtil.CopySerializedProperty(newValueProperty, ruleIt);
                        }
                        ruleIt.NextVisible(false);
                        thisRuleIt.NextVisible(false);
                    }
                }
            }
        }
        AssetClearanceUtil.CopySerializedProperty(serializedObject.FindProperty("unappliedSave"), serializedObject.FindProperty("rule"));
        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
    }
    public override void OnInspectorGUI()
    {
        CopyAndPaste();
        EventType originalType = Event.current.type;
        if (intelliSenseProperty != null)
        {
            BlockMouseEvent(intelliSenseTextRect);
        }
        if (originalType == EventType.Layout && needRecalculateProperty)
        {
            clearCacheMethod.Invoke(ruleConditionList, new object[] { });
            ruleConditionList.DoLayoutList();
            needRecalculateProperty = false;
        }
        var minHeight = intelliSenseTextRect.y + intelliSenseTextRect.height * (Style.IntelliSenseDisplayNum + 1);
        EditorGUILayout.BeginVertical(GUILayout.MinHeight(minHeight));
        var nameProperty = unappliedSaveProperty.FindPropertyRelative("name");
        var trueLogTypeProperty = unappliedSaveProperty.FindPropertyRelative("trueLogType");
        var falseLogTypeProperty = unappliedSaveProperty.FindPropertyRelative("falseLogType");
        var helpURLProperty = unappliedSaveProperty.FindPropertyRelative("helpURL");
        var fixNoticeProperty = unappliedSaveProperty.FindPropertyRelative("fixNotice");
        var conditionsProperty = ruleConditionList.serializedProperty;
        var fixMethodProperty = unappliedSaveProperty.FindPropertyRelative("fixMethod");
        var fixMethodNameProperty = fixMethodProperty.FindPropertyRelative("name");
        for (int i = 0; i < conditionsProperty.arraySize; ++i)
        {
            var conditionProperty = conditionsProperty.GetArrayElementAtIndex(i);
            var methodProperty = conditionProperty.FindPropertyRelative("method");
            var methodNameProperty = conditionProperty.FindPropertyRelative("method.name");
            if (!methodValidationCache.ContainsKey(methodNameProperty.propertyPath))
            {
                var methods = objectMethods;
                methodValidationCache[methodNameProperty.propertyPath] = ValidateMethod(methods, methodNameProperty, methodProperty);
                needRecalculateProperty = true;
            }
        }
        if (!methodValidationCache.ContainsKey(fixMethodNameProperty.propertyPath))
        {
            var fixMethods = objectFixMethods;
            methodValidationCache[fixMethodNameProperty.propertyPath] = ValidateMethod(fixMethods, fixMethodNameProperty, fixMethodProperty);
            needRecalculateProperty = true;
        }
        var height = RulesEditor.GetPropertyBorderHeight(unappliedSaveProperty.FindPropertyRelative("name"));
        height += height * (7 + LineCountOfParameters(fixMethodNameProperty, fixMethodProperty));
        height += ruleConditionList.GetHeight() + 2;
        height += 6;
        EditorGUILayout.Space(height);
        var rect = GUILayoutUtility.GetLastRect();
        GUI.Box(rect, GUIContent.none, Style.Box);
        var propertyRect = RulesEditor.Deflate(rect, 16, 0);
        propertyRect.width -= 20;
        propertyRect.height = RulesEditor.GetPropertyBorderHeight(nameProperty);
        var labelWidth = 230;
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, nameProperty, null);
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        propertyRect.height = ruleConditionList.GetHeight() + 2;
        ruleConditionList.DoList(RulesEditor.Space(RulesEditor.Deflate(propertyRect, 4, 0), 2));
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        propertyRect.height = RulesEditor.GetPropertyBorderHeight(nameProperty);
        RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, trueLogTypeProperty, null);
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, falseLogTypeProperty, null);
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, helpURLProperty, null);
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        EditorGUI.BeginChangeCheck();
        BeginMethodValidation(fixMethodNameProperty);
        var inputFieldRect = RulesEditor.MethodField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, fixMethodNameProperty, fixMethodProperty);
        EndMethodValidation();
        if (EditorGUI.EndChangeCheck() || GUI.GetNameOfFocusedControl() == fixMethodNameProperty.propertyPath)
        {
            intelliSenseTreeView = objectFixMethodTreeView;
            if (intelliSenseProperty == null || intelliSenseProperty.propertyPath != fixMethodNameProperty.propertyPath)
            {
                intelliSenseTreeView.searchString = fixMethodNameProperty.stringValue;
                if (!string.IsNullOrWhiteSpace(intelliSenseTreeView.searchString))
                {
                    var rows = intelliSenseTreeView.GetRows();
                    if (rows.Count > 0)
                    {
                        intelliSenseTreeView.SetSelection(new List<int> { rows[0].id });
                    }
                }
            }
            intelliSenseProperty = fixMethodNameProperty;
            if (Event.current.type != EventType.Layout) intelliSenseTextRect = inputFieldRect;
            onConfirmIntelliSense = OnConfirmFixMethodIntellSense;
            if (EditorGUI.EndChangeCheck())
            {
                UpdateIntellisense();
            }
        }
        propertyRect = DrawMethodParameters(propertyRect, labelWidth, inputFieldRect, fixMethodNameProperty, fixMethodProperty);
        propertyRect = RulesEditor.Space(propertyRect, propertyRect.height);
        RulesEditor.CustomPropertyField(RulesEditor.Deflate(propertyRect, 4, 2), labelWidth, fixNoticeProperty, null);
        if (GUILayout.Button("Apply To All Rules"))
        {
            ApplyToAllRules();
        }
        EditorGUILayout.EndVertical();
        if (intelliSenseProperty != null)
        {
            Event.current.type = originalType;
        }
        DrawIntellisense();
        DrawMethodTip();
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
    }
}
