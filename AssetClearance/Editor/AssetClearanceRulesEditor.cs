using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.IMGUI.Controls;
using System.Text.RegularExpressions;
using TargetScope = AssetClearanceRules.TargetScope;

[CustomEditor(typeof(AssetClearanceRules))]
public class AssetClearanceRulesEditor : Editor
{
    public static class Style
    {
        public static readonly GUIStyle BoldLabel = new GUIStyle("BoldLabel");
        public static readonly GUIStyle Box = new GUIStyle("RL Background");
        public static readonly GUIStyle Window = new GUIStyle("TabWindowBackground");
        private static GUIStyle methodField;
        public static GUIStyle MethodField
        {
            get
            {
                if (methodField == null)
                {
                    methodField = new GUIStyle("textfield");
                    methodField.padding.left = 16;
                }
                return methodField;
            }
        }
        private static Texture2D methodIcon;
        public static Texture2D MethodIcon
        {
            get
            {
                if (methodIcon == null)
                {
                    methodIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetClearanceUtil.GetIconPath("method.png"));
                }
                return methodIcon;
            }
        }
        public static readonly Color InvalidColor = new Color(2.0f, 0.5f, 0.5f);
        public static readonly int IntelliSenseDisplayNum = 8;
        private static GUIStyle methodTipTitle;
        public static GUIStyle MethodTipTitle
        {
            get
            {
                if (methodTipTitle == null)
                {
                    methodTipTitle = new GUIStyle("BoldLabel");
                    methodTipTitle.fontSize = 16;
                    methodTipTitle.normal.textColor = new Color(0.86f, 0.84f, 0.47f);
                }
                return methodTipTitle;
            }
        }
        private static GUIStyle methodTip;
        public static GUIStyle MethodTip
        {
            get
            {
                if (methodTip == null)
                {
                    methodTip = new GUIStyle("Label");
                    methodTip.wordWrap = true;
                    methodTip.richText = true;
                    methodTip.alignment = TextAnchor.UpperLeft;
                }
                return methodTip;
            }
        }
        private static GUIStyle templateTip;
        public static GUIStyle TemplateTip
        {
            get
            {
                if (templateTip == null)
                {
                    templateTip = new GUIStyle("Label");
                    templateTip.normal.textColor = Color.gray;
                }
                return templateTip;
            }
        }
    }

    static Dictionary<Type, AssetClearanceStringTreeView> stringTreeViewDict;
    static Dictionary<string, bool> parameterFoldoutDict = new Dictionary<string, bool>();
    static Dictionary<int, bool> ruleFoldoutDict = new Dictionary<int, bool>();
    static SerializedObject copyingElementSerializedObject;
    static string copyingElementPath;
    static string copyingElementParentPath;
    static bool cutingElement;

    SerializedProperty enableRulesProperty;
    SerializedProperty useforSecondaryCheckProperty;
    SerializedProperty targetScopeProperty;
    SerializedProperty specificObjectsProperty;
    ReorderableList specificObjectList;
    bool commonParametersFoldout = true;
    SerializedProperty defaultIncludePathsProperty;
    bool defaultIncludePathsFoldout;
    SerializedProperty defaultExcludePathsProperty;
    bool defaultExcludePathsFoldout;
    SerializedProperty enableSkipConditionsProperty;
    SerializedProperty skipFilesFilter;
    SerializedProperty skipObjectsProperty;
    ReorderableList skipObjectsList;
    SerializedProperty skipConditionsProperty;
    bool skipConditionsFoldout;
    ReorderableList skipConditionList;
    SerializedProperty intelliSenseProperty;
    Rect intelliSenseTextRect;
    TreeView intelliSenseTreeView;
    Action onConfirmIntelliSense;
    SerializedProperty ruleListProperty;
    ReorderableList ruleList;
    List<ReorderableList> ruleConditionLists;
    List<MethodInfo> objectMethods;
    List<MethodInfo> objectFixMethods;
    Dictionary<string, MethodInfo> methodValidationCache;
    Dictionary<string, bool> parameterValidationCache;
    bool keepParameterFoldout;
    MethodInfo clearCacheMethod;
    PropertyInfo currentGUIView;
    string needRecalculatePropertyPath = "";
    AssetClearanceMethodTreeView objectMethodTreeView;
    AssetClearanceMethodTreeView objectFixMethodTreeView;
    Color backgroundColor;
    SerializedProperty selectedElement;
    SerializedProperty selectedElementParent;

    public static Rect Inflate(Rect rect, float horizontal, float vertical)
    {
        rect.x -= horizontal;
        rect.width += horizontal * 2;
        rect.y -= vertical;
        rect.height += vertical * 2;
        return rect;
    }
    public static Rect Deflate(Rect rect, float horizontal, float vertical)
    {
        return Inflate(rect, -horizontal, -vertical);
    }
    public static Rect Indent(Rect rect, float length)
    {
        rect.x += length;
        rect.width -= length;
        return rect;
    }
    public static Rect Space(Rect rect, float height)
    {
        rect.y += height;
        return rect;
    }
    public static float GetPropertyBorderHeight(SerializedProperty property)
    {
        return EditorGUI.GetPropertyHeight(property) + 4;
    }
    static readonly Regex ruleRegex = new Regex("ruleList.Array.data\\[[0-9]+?\\](.*)");
    void RevertPropertyMenu(Rect rect, string propertyName, SerializedProperty property, AssetClearanceRuleTemplate template)
    {
        if (template == null) return;
        var containMouse = rect.Contains(Event.current.mousePosition);
        if (containMouse && Event.current.clickCount == 1 && Event.current.button == 1)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"Revert \"{propertyName}\""), false, () =>
            {
                var path = ruleRegex.Match(property.propertyPath).Groups[1];
                var templateSerializedObject = new SerializedObject(template);
                var templateProperty = templateSerializedObject.FindProperty("rule" + path);
                AssetClearanceUtil.CopySerializedProperty(templateProperty, property);
                RebuildRuleListAndCache();
            });
            menu.ShowAsContext();
        }
    }
    public static void CustomPropertyField(Rect rect, string label, int labelWidth, SerializedProperty property, Type type)
    {
        var labelRect = rect;
        labelRect.width = labelWidth;
        EditorGUI.LabelField(labelRect, label);
        rect = Indent(rect, labelWidth);
        GUI.SetNextControlName(property.propertyPath);
        if (type != null && type.IsSubclassOf(typeof(UnityEngine.Object)))
        {
            EditorGUI.ObjectField(rect, property, type, GUIContent.none);
        }
        else
        {
            EditorGUI.PropertyField(rect, property, GUIContent.none);
        }
    }
    public static void CustomPropertyField(Rect rect, int labelWidth, SerializedProperty property, Type type)
    {
        CustomPropertyField(rect, property.displayName, labelWidth, property, type);
    }
    public void CustomPropertyField(Rect rect, int labelWidth, SerializedProperty property, Type type, AssetClearanceRuleTemplate template)
    {
        RevertPropertyMenu(rect, property.displayName, property, template);
        CustomPropertyField(rect, labelWidth, property, type);
    }
    public static void EnumField(Type enumType, Rect rect, string label, int labelWidth, SerializedProperty property)
    {
        var labelRect = rect;
        labelRect.width = labelWidth;
        EditorGUI.LabelField(labelRect, label);
        rect = Indent(rect, labelWidth);
        var enumValue = Enum.ToObject(enumType, property.intValue) as Enum;
        if (enumValue == null) enumValue = Enum.ToObject(enumType, 0) as Enum;
        GUI.SetNextControlName(property.propertyPath);
        if (enumType.GetCustomAttribute<FlagsAttribute>() != null)
        {
            property.intValue = Convert.ToInt32(EditorGUI.EnumFlagsField(rect, enumValue));
        }
        else
        {
            property.intValue = Convert.ToInt32(EditorGUI.EnumPopup(rect, enumValue));
        }
    }
    public static Rect MethodField(Rect rect, int labelWidth, SerializedProperty methodNameProperty, SerializedProperty methodProperty, SerializedProperty negationProperty = null)
    {
        var labelRect = rect;
        labelRect.width = labelWidth;
        EditorGUI.LabelField(labelRect, methodProperty.displayName);
        if (negationProperty != null)
        {
            var negationRect = Indent(rect, labelWidth - 20);
            negationRect.width = 14;
            negationProperty.boolValue = EditorGUI.Toggle(negationRect, GUIContent.none, negationProperty.boolValue, Style.MethodField);
            if (negationProperty.boolValue)
            {
                GUI.Label(Indent(negationRect, 3), "!");
            }
        }
        rect = Indent(rect, labelWidth);
        GUI.SetNextControlName(methodNameProperty.propertyPath);
        methodNameProperty.stringValue = EditorGUI.TextField(rect, methodNameProperty.stringValue, Style.MethodField);
        var iconRect = rect;
        iconRect.width = 19;
        iconRect.height = 19;
        GUI.DrawTexture(Deflate(iconRect, 2, 2), Style.MethodIcon);
        return rect;
    }
    static bool DrawArray(SerializedProperty arrayProperty, bool foldout)
    {
        arrayProperty.arraySize = Mathf.Max(1, arrayProperty.arraySize);
        foldout = EditorGUILayout.Foldout(foldout, arrayProperty.displayName + " [" + arrayProperty.arraySize + "]");
        if (foldout && arrayProperty.arraySize > 0)
        {
            for (int i = 0; i < arrayProperty.arraySize; ++i)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(arrayProperty.GetArrayElementAtIndex(i), new GUIContent(" [" + i + "]"));
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    arrayProperty.DeleteArrayElementAtIndex(i--);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                arrayProperty.arraySize++;
                AssetClearanceUtil.ResetSerializedPropertyValue(arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize - 1));
            }
            EditorGUILayout.EndHorizontal();
        }
        return foldout;
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
        return height * GetPropertyBorderHeight(methodNameProperty);
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
    void UpdateIntellisense()
    {
        intelliSenseTreeView.searchString = intelliSenseProperty.stringValue;
        intelliSenseTreeView.state.selectedIDs.Clear();
    }
    void BlockMouseEvent(Rect textRect)
    {
        var rect = Space(textRect, textRect.height);
        rect.height *= Style.IntelliSenseDisplayNum;
        if (rect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp))
        {
            Event.current.Use();
        }
    }
    void DrawIntellisense()
    {
        if (intelliSenseProperty == null) return;
        var focusRect = intelliSenseTextRect;
        focusRect.height *= (Style.IntelliSenseDisplayNum + 1);
        var clickRect = Space(intelliSenseTextRect, intelliSenseTextRect.height);
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
            var boxRect = Space(intelliSenseTextRect, intelliSenseTextRect.height);
            boxRect.height *= Style.IntelliSenseDisplayNum;
            GUI.Box(boxRect, GUIContent.none, Style.Box);
            GUI.Box(Deflate(boxRect, 1, 1), GUIContent.none, Style.Window);
            GUI.SetNextControlName("intellisense");
            intelliSenseTreeView.OnGUI(Deflate(boxRect, 1, 1));
        }
    }
    void DrawMethodTip()
    {
        if (intelliSenseProperty == null || !(intelliSenseTreeView is AssetClearanceMethodTreeView)) return;
        var treeView = intelliSenseTreeView as AssetClearanceMethodTreeView;
        var rect = Space(intelliSenseTextRect, intelliSenseTextRect.height);
        rect.height *= Style.IntelliSenseDisplayNum;
        if (treeView.LastSelectedItem != null && !treeView.LastSelectedItem.hasChildren)
        {
            var item = treeView.LastSelectedItem as AssetClearanceMethodTreeView.Item;
            rect.x -= 274;
            rect.width = 275;
            var titleHeight = 26;
            var attribute = item.method.GetCustomAttribute<AssetClearanceMethod>();
            var content = new GUIContent(attribute.Tip);
            var contentSize = Style.MethodTip.CalcHeight(content, rect.width) + Style.MethodTip.lineHeight +  2;
            rect.height = titleHeight + contentSize;
            rect.y += treeView.LastSelectedLine * treeView.RowHeight;
            GUI.Box(rect, GUIContent.none, Style.Box);
            GUI.Box(Deflate(rect, 1, 1), GUIContent.none, Style.Window);
            var labelRect = rect;
            labelRect.height = titleHeight;
            GUI.Label(Deflate(labelRect, 2, 2), item.displayName, Style.MethodTipTitle);
            labelRect = Space(labelRect, labelRect.height);
            labelRect.height = rect.height - titleHeight;
            GUI.Label(Deflate(labelRect, 2, 0), content, Style.MethodTip);
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
                    AssetClearanceUtil.ResetSerializedPropertyValue(paramCountsProperty[parameterType].GetArrayElementAtIndex(paramCountsProperty[parameterType].arraySize - 1));
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
        needRecalculatePropertyPath = intelliSenseProperty.propertyPath;
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
    Rect DrawMethodParameters(Rect propertyRect, int labelWidth, Rect inputFieldRect, SerializedProperty methodNameProperty, SerializedProperty methodProperty)
    {
        var propertyHeight = GetPropertyBorderHeight(methodNameProperty);
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
                propertyRect = Space(propertyRect, propertyHeight);
                var labelRect = propertyRect;
                labelRect.width = labelWidth;
                if (!parameterType.IsArray)
                {
                    EditorGUI.LabelField(Deflate(labelRect, 4, 2), "- " + displayName);
                    var parameterProperty = parametersProperty[parameterType].GetArrayElementAtIndex(paramCount[parameterType].value++);
                    GUI.SetNextControlName(parameterProperty.propertyPath);
                    if (validationType != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        BeginParameterValidation(parameterProperty);
                        inputFieldRect = Deflate(Indent(propertyRect, labelWidth), 4, 2);
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
                        EnumField(parameters[i].ParameterType, Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty);
                    }
                    else
                    {
                        CustomPropertyField(Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty, parameters[i].ParameterType);
                    }
                }
                else
                {
                    var countProperty = paramCountsProperty[parameterType].GetArrayElementAtIndex(paramCountsIndex[parameterType].value);
                    parameterFoldoutDict[countProperty.propertyPath] = EditorGUI.Foldout(Deflate(labelRect, 4, 2), parameterFoldoutDict[countProperty.propertyPath], GUIContent.none);
                    EditorGUI.LabelField(Deflate(labelRect, 4, 2), displayName + " [" + countProperty.intValue + "]");
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
                                inputFieldRect = Deflate(Indent(propertyRect, labelWidth), 4, 2);
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
                            else if(parameters[i].ParameterType.GetElementType().IsEnum)
                            {
                                EnumField(parameters[i].ParameterType.GetElementType(), Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty);
                            }
                            else
                            {
                                CustomPropertyField(Deflate(propertyRect, 4, 2), "", labelWidth, parameterProperty, parameters[i].ParameterType.GetElementType());
                            }
                            propertyRect.width += 22;
                            if (GUI.Button(Deflate(Indent(propertyRect, propertyRect.width - 30), 4, 2), "-"))
                            {
                                if (countProperty.intValue > 1)
                                {
                                    countProperty.intValue--;
                                    parametersProperty[parameterType].DeleteArrayElementAtIndex(paramCount[parameterType].value + k);
                                    k--;
                                }
                                methodValidationCache.Remove(methodNameProperty.propertyPath);
                            }
                            propertyRect = Space(propertyRect, propertyHeight);
                        }
                        if (GUI.Button(Deflate(Indent(propertyRect, propertyRect.width - 30), 4, 2), "+"))
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
    void DrawCondition(Rect rect, int index, SerializedProperty listProperty, SerializedProperty conditionProperty, AssetClearanceRuleTemplate template)
    {
        GUI.Box(rect, GUIContent.none, Style.Box);
        rect = Deflate(rect, 16, 0);
        var methodProperty = conditionProperty.FindPropertyRelative("method");
        var methodNameProperty = methodProperty.FindPropertyRelative("name");
        var negationProperty = conditionProperty.FindPropertyRelative("negation");
        var propertyHeight = GetPropertyBorderHeight(methodNameProperty);
        if (listProperty.arraySize > 1 && currentGUIView.GetValue(null) != null)
        {
            var priorityProperty = conditionProperty.FindPropertyRelative("priority");
            var priorityRect = rect;
            priorityRect.width = 50;
            priorityRect.height = propertyHeight;
            EditorGUI.LabelField(Deflate(priorityRect, 4, 2), priorityProperty.displayName);
            priorityRect = Space(priorityRect, propertyHeight);
            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName(priorityProperty.propertyPath);
            priorityProperty.intValue = EditorGUI.DelayedIntField(Deflate(priorityRect, 4, 2), priorityProperty.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                SortConditionsByPriority(listProperty);
                Repaint();
                return;
            }
        }
        int labelWidth = 160;
        var propertyRect = rect;
        propertyRect = Indent(propertyRect, 70);
        propertyRect.height = propertyHeight;
        EditorGUI.BeginChangeCheck();
        BeginMethodValidation(methodNameProperty);
        var inputFieldRect = MethodField(Deflate(propertyRect, 4, 2), labelWidth, methodNameProperty, methodProperty, negationProperty);
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
        propertyRect = Space(propertyRect, propertyHeight);
        if (index < listProperty.arraySize - 1)
        {
            var logicOperatorProperty = conditionProperty.FindPropertyRelative("logicOperator");
            CustomPropertyField(Deflate(propertyRect, 4, 2), labelWidth, logicOperatorProperty, null);
        }
    }
    float ElementHeightOfSkipConditionList(int index)
    {
        var element = skipConditionsProperty.GetArrayElementAtIndex(index);
        return HeightOfCondition(element);
    }
    void DrawElementOfSkipConditionList(Rect rect, int index, bool active, bool focused)
    {
        var element = skipConditionsProperty.GetArrayElementAtIndex(index);
        var templateProperty = element.FindPropertyRelative("template");
        DrawCondition(rect, index, skipConditionsProperty, element, templateProperty?.objectReferenceValue as AssetClearanceRuleTemplate);
    }
    void RebuildRuleListAndCache()
    {
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        ruleList = new ReorderableList(serializedObject, ruleListProperty, false, true, true, true);
        ruleList.draggable = true;
        ruleList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Rules", Style.BoldLabel);
        ruleList.elementHeightCallback = ElementHeightOfRuleList;
        ruleList.onAddCallback = OnAddElementOfRuleList;
        ruleList.onRemoveCallback = OnRemoveElementOfRuleList;
        ruleList.drawElementCallback = DrawElementOfRuleList;
        ruleList.onSelectCallback = OnSelectElement;
        ruleList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
        {
            var oldFoldout = ruleFoldoutDict[oldIndex];
            ruleFoldoutDict[oldIndex] = ruleFoldoutDict[newIndex];
            ruleFoldoutDict[newIndex] = oldFoldout;
            methodValidationCache.Clear();
            needRecalculatePropertyPath = "ruleList.Array.data";
        };
#if UNITY_2021_1_OR_NEWER
        ruleList.multiSelect = true;
#endif
        for (int i = 0; i < ruleListProperty.arraySize; ++i)
        {
            if (!ruleFoldoutDict.ContainsKey(i)) ruleFoldoutDict[i] = false;
        }
        ruleConditionLists = new List<ReorderableList>();
        for (int i = 0; i < ruleListProperty.arraySize; ++i)
        {
            var ruleProperty = ruleListProperty.GetArrayElementAtIndex(i);
            var templateProperty = ruleProperty.FindPropertyRelative("template");
            var conditionsProperty = ruleProperty.FindPropertyRelative("conditions");
            var reorderableList = new ReorderableList(serializedObject, conditionsProperty, false, true, true, true);
            reorderableList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Conditions", Style.BoldLabel);
            reorderableList.elementHeightCallback = (index) =>
            {
                var conditionProperty = conditionsProperty.GetArrayElementAtIndex(index);
                return HeightOfCondition(conditionProperty);
            };
            reorderableList.onAddCallback = OnAddElementOfConditionList;
            reorderableList.onRemoveCallback = OnRemoveElementOfConditionList;
            reorderableList.drawElementCallback = (rect, index, active, focused) =>
            {
                var conditionProperty = conditionsProperty.GetArrayElementAtIndex(index);
                DrawCondition(rect, index, conditionsProperty, conditionProperty, templateProperty.objectReferenceValue as AssetClearanceRuleTemplate);
            };
            reorderableList.onSelectCallback = OnSelectElement;
#if UNITY_2021_1_OR_NEWER
            reorderableList.multiSelect = true;
#endif
            ruleConditionLists.Add(reorderableList);
        }
        methodValidationCache = new Dictionary<string, MethodInfo>();
        parameterValidationCache = new Dictionary<string, bool>();
    }
    float ElementHeightOfRuleList(int index)
    {
        var element = ruleListProperty.GetArrayElementAtIndex(index);
        var fixMethodProperty = element.FindPropertyRelative("fixMethod");
        var fixMethodNameProperty = fixMethodProperty.FindPropertyRelative("name");
        var height = GetPropertyBorderHeight(element.FindPropertyRelative("name"));
        if (ruleFoldoutDict[index])
        {
            height += height * (6 + LineCountOfParameters(fixMethodNameProperty, fixMethodProperty));
            height += ruleConditionLists[index].GetHeight() + 2;
            height += 6;
        }
        return height;
    }
    void AddBlankRule(ReorderableList list)
    {
        list.serializedProperty.arraySize++;
        var index = list.serializedProperty.arraySize - 1;
        var element = list.serializedProperty.GetArrayElementAtIndex(index);
        selectedElement = element;
        selectedElementParent = list.serializedProperty;
        AssetClearanceUtil.ResetSerializedProperty(element);
        var enableProperty = element.FindPropertyRelative("enable");
        var nameProperty = element.FindPropertyRelative("name");
        var conditionsProperty = element.FindPropertyRelative("conditions");
        enableProperty.boolValue = true;
        nameProperty.stringValue = "Rule " + (index + 1);
        serializedObject.ApplyModifiedProperties();
        ruleFoldoutDict[index] = true;
        var reorderableList = new ReorderableList(serializedObject, conditionsProperty, false, true, true, true);
        reorderableList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Conditions", Style.BoldLabel);
        reorderableList.elementHeightCallback = (index) =>
        {
            var element = conditionsProperty.GetArrayElementAtIndex(index);
            return HeightOfCondition(element);
        };
        reorderableList.onAddCallback = OnAddElementOfConditionList;
        reorderableList.onRemoveCallback = OnRemoveElementOfConditionList;
        reorderableList.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = conditionsProperty.GetArrayElementAtIndex(index);
            var templateProperty = element.FindPropertyRelative("template");
            DrawCondition(rect, index, conditionsProperty, element, templateProperty?.objectReferenceValue as AssetClearanceRuleTemplate);
        };
        reorderableList.onSelectCallback = OnSelectElement;
#if UNITY_2021_1_OR_NEWER
        reorderableList.multiSelect = true;
#endif
        ruleConditionLists.Add(reorderableList);
        list.index = list.serializedProperty.arraySize - 1;
    }
    void AddRuleTemplate(ReorderableList list, AssetClearanceRuleTemplate template)
    {
        list.serializedProperty.arraySize++;
        var index = list.serializedProperty.arraySize - 1;
        var element = list.serializedProperty.GetArrayElementAtIndex(index);
        selectedElement = element;
        selectedElementParent = list.serializedProperty;
        var templateSerializedObject = new SerializedObject(template);
        var templateRuleProperty = templateSerializedObject.FindProperty("rule");
        AssetClearanceUtil.CopySerializedProperty(templateRuleProperty, element);
        var templateProperty = element.FindPropertyRelative("template");
        templateProperty.objectReferenceValue = template;
        var enableProperty = element.FindPropertyRelative("enable");
        enableProperty.boolValue = true;
        ruleFoldoutDict[index] = true;
        list.index = list.serializedProperty.arraySize - 1;
        RebuildRuleListAndCache();
    }
    void OnAddElementOfRuleList(ReorderableList list)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Blank Rule"), false, () => AddBlankRule(list));
        var templates = AssetDatabase.FindAssets("t:AssetClearanceRuleTemplate");
        foreach (var guid in templates)
        {
            var template = AssetDatabase.LoadAssetAtPath<AssetClearanceRuleTemplate>(AssetDatabase.GUIDToAssetPath(guid));
            if (!string.IsNullOrEmpty(template.rule.name))
            {
                menu.AddItem(new GUIContent("Templates/" + template.rule.name), false, () => AddRuleTemplate(list, template));
            }
        }
        menu.ShowAsContext();
    }
    void OnRemoveElementOfRuleList(ReorderableList list)
    {
        ReorderableList.defaultBehaviours.DoRemoveButton(ruleList);
        var index = ruleList.index;
        RebuildRuleListAndCache();
        ruleList.index = index;
        if (ruleList.serializedProperty.arraySize > 0)
        {
            var element = ruleList.serializedProperty.GetArrayElementAtIndex(ruleList.index);
            selectedElement = element;
            selectedElementParent = ruleList.serializedProperty;
        }
        else
        {
            selectedElement = null;
            selectedElementParent = null;
        }
    }
    void OnConfirmFixMethodIntellSense()
    {
        methodValidationCache.Remove(intelliSenseProperty.propertyPath);
        needRecalculatePropertyPath = intelliSenseProperty.propertyPath;
    }
    void RevertProperties(SerializedProperty element, AssetClearanceRuleTemplate template)
    {
        var templateProperty = element.FindPropertyRelative("template");
        var enableProperty = element.FindPropertyRelative("enable");
        var lastEnable = enableProperty.boolValue;
        var templateSerializedObject = new SerializedObject(template);
        var templateRuleProperty = templateSerializedObject.FindProperty("rule");
        AssetClearanceUtil.CopySerializedProperty(templateRuleProperty, element);
        templateProperty.objectReferenceValue = template;
        enableProperty.boolValue = lastEnable;
        RebuildRuleListAndCache();
    }
    void DrawElementOfRuleList(Rect rect, int index, bool active, bool focused)
    {
        GUI.Box(rect, GUIContent.none, Style.Box);
        var element = ruleListProperty.GetArrayElementAtIndex(index);
        var templateProperty = element.FindPropertyRelative("template");
        var template = templateProperty.objectReferenceValue as AssetClearanceRuleTemplate;
        var enableProperty = element.FindPropertyRelative("enable");
        var nameProperty = element.FindPropertyRelative("name");
        var trueLogTypeProperty = element.FindPropertyRelative("trueLogType");
        var falseLogTypeProperty = element.FindPropertyRelative("falseLogType");
        var helpURLProperty = element.FindPropertyRelative("helpURL");
        var fixMethodProperty = element.FindPropertyRelative("fixMethod");
        var fixMethodNameProperty = fixMethodProperty.FindPropertyRelative("name");
        var fixNoticeProperty = element.FindPropertyRelative("fixNotice");
        var conditionsProperty = ruleConditionLists[index].serializedProperty;
        for (int i = 0; i < conditionsProperty.arraySize; ++i)
        {
            var conditionProperty = conditionsProperty.GetArrayElementAtIndex(i);
            var methodProperty = conditionProperty.FindPropertyRelative("method");
            var methodNameProperty = conditionProperty.FindPropertyRelative("method.name");
            if (!methodValidationCache.ContainsKey(methodNameProperty.propertyPath))
            {
                var methods = objectMethods;
                methodValidationCache[methodNameProperty.propertyPath] = ValidateMethod(methods, methodNameProperty, methodProperty);
                needRecalculatePropertyPath = methodNameProperty.propertyPath;
            }
        }
        if (!methodValidationCache.ContainsKey(fixMethodNameProperty.propertyPath))
        {
            var fixMethods = objectFixMethods;
            methodValidationCache[fixMethodNameProperty.propertyPath] = ValidateMethod(fixMethods, fixMethodNameProperty, fixMethodProperty);
            needRecalculatePropertyPath = fixMethodNameProperty.propertyPath;
        }
        var containsMouse = rect.Contains(Event.current.mousePosition);
        if (template != null && containsMouse && Event.current.clickCount == 1 && Event.current.button == 1)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Select Template"), false, () =>
            {
                EditorGUIUtility.PingObject(template);
                Selection.objects = new UnityEngine.Object[1] { template };
            });
            menu.AddItem(new GUIContent("Revert Properties"), false, () => RevertProperties(element, template));
            menu.ShowAsContext();
        }
        var propertyRect = Deflate(rect, 16, 0);
        propertyRect.height = GetPropertyBorderHeight(nameProperty);
        CustomPropertyField(Indent(propertyRect, propertyRect.width - 74), enableProperty.boolValue ? "Enabled" : "Disabled", 55, enableProperty, null);
        if (templateProperty.objectReferenceValue != null)
        {
            EditorGUI.LabelField(Indent(propertyRect, propertyRect.width - 200), "T E M P L A T E", Style.TemplateTip);
        }
        EditorGUI.BeginDisabledGroup(!enableProperty.boolValue);
        ruleFoldoutDict[index] = EditorGUI.Foldout(propertyRect, ruleFoldoutDict[index], ruleFoldoutDict[index] ? "" : nameProperty.stringValue);
        if (!ruleFoldoutDict[index])
        {
            EditorGUI.EndDisabledGroup();
            return;
        }
        var labelWidth = 230;
        propertyRect = Space(propertyRect, propertyRect.height);
        CustomPropertyField(Deflate(propertyRect, 4, 2), labelWidth, nameProperty, null, template);
        propertyRect = Space(propertyRect, propertyRect.height);
        propertyRect.height = ruleConditionLists[index].GetHeight() + 2;
        RevertPropertyMenu(propertyRect, conditionsProperty.displayName, conditionsProperty, template);
        ruleConditionLists[index].DoList(Space(Deflate(propertyRect, 4, 0), 2));
        propertyRect = Space(propertyRect, propertyRect.height);
        propertyRect.height = GetPropertyBorderHeight(nameProperty);
        CustomPropertyField(Deflate(propertyRect, 4, 2), labelWidth, trueLogTypeProperty, null, template);
        propertyRect = Space(propertyRect, propertyRect.height);
        CustomPropertyField(Deflate(propertyRect, 4, 2), labelWidth, falseLogTypeProperty, null, template);
        propertyRect = Space(propertyRect, propertyRect.height);
        CustomPropertyField(Deflate(propertyRect, 4, 2), labelWidth, helpURLProperty, null, template);
        propertyRect = Space(propertyRect, propertyRect.height);
        EditorGUI.BeginChangeCheck();
        BeginMethodValidation(fixMethodNameProperty);
        var inputFieldRect = MethodField(Deflate(propertyRect, 4, 2), labelWidth, fixMethodNameProperty, fixMethodProperty);
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
        propertyRect = Space(propertyRect, propertyRect.height);
        var fixMethodRect = propertyRect;
        fixMethodRect.y = inputFieldRect.y;
        fixMethodRect.height = propertyRect.y - inputFieldRect.y;
        RevertPropertyMenu(fixMethodRect, fixMethodProperty.displayName, fixMethodProperty, template);
        CustomPropertyField(Deflate(propertyRect, 4, 2), labelWidth, fixNoticeProperty, null);
        EditorGUI.EndDisabledGroup();
    }
    void OnSelectElement(ReorderableList list)
    {
        selectedElement = list.serializedProperty.GetArrayElementAtIndex(list.index);
        selectedElementParent = list.serializedProperty;
    }
    void HandleObjectsDragAndDrop(SerializedProperty property, Rect rect)
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
                            property.arraySize++;
                            var element = property.GetArrayElementAtIndex(property.arraySize - 1);
                            var obj = element.FindPropertyRelative("obj");
                            obj.objectReferenceValue = objectReferences[i];
                        }
                        DragAndDrop.AcceptDrag();
                        DragAndDrop.objectReferences = null;
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
    void DrawElementOfSpecificObjectList(SerializedProperty property, Rect rect, int index, bool active, bool focused)
    {
        var element = property.GetArrayElementAtIndex(index);
        var objProperty = element.FindPropertyRelative("obj");
        rect = Deflate(rect, 20, 0);
        rect.height = EditorGUIUtility.singleLineHeight;
        objProperty.objectReferenceValue = EditorGUI.ObjectField(rect, GUIContent.none, objProperty.objectReferenceValue, AssetClearanceUtil.ObjectType, false);
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
        var rules = target as AssetClearanceRules;
        enableRulesProperty = serializedObject.FindProperty(nameof(rules.enableRules));
        useforSecondaryCheckProperty = serializedObject.FindProperty(nameof(rules.useForSecondaryCheck));
        targetScopeProperty = serializedObject.FindProperty(nameof(rules.targetScope));
        specificObjectsProperty = serializedObject.FindProperty(nameof(rules.specificObjects));
        specificObjectList = new ReorderableList(serializedObject, specificObjectsProperty, false, true, true, true);
        specificObjectList.drawHeaderCallback = (rect) =>
        {
            EditorGUI.LabelField(rect, "Specific Objects", Style.BoldLabel);
            HandleObjectsDragAndDrop(specificObjectsProperty, rect);
        };
        specificObjectList.elementHeight = EditorGUIUtility.singleLineHeight + 2;
        specificObjectList.drawElementCallback = (rect, index, active, focused) => DrawElementOfSpecificObjectList(specificObjectsProperty, rect, index, active, focused);
        specificObjectList.drawNoneElementCallback = (rect) => HandleObjectsDragAndDrop(specificObjectsProperty, rect);
#if UNITY_2021_1_OR_NEWER
        specificObjectList.multiSelect = true;
#endif
        defaultIncludePathsProperty = serializedObject.FindProperty(nameof(rules.defaultIncludePaths));
        defaultExcludePathsProperty = serializedObject.FindProperty(nameof(rules.defaultExcludePaths));
        enableSkipConditionsProperty = serializedObject.FindProperty(nameof(rules.enableSkipConditions));
        skipFilesFilter = serializedObject.FindProperty(nameof(rules.skipFilesFilter));
        skipObjectsProperty = serializedObject.FindProperty(nameof(rules.skipObjects));
        skipObjectsList = new ReorderableList(serializedObject, skipObjectsProperty, false, true, true, true);
        skipObjectsList.drawHeaderCallback = (rect) =>
        {
            EditorGUI.LabelField(rect, "Skip Objects", Style.BoldLabel);
            HandleObjectsDragAndDrop(skipObjectsProperty, rect);
        };
        skipObjectsList.elementHeight = EditorGUIUtility.singleLineHeight + 2;
        skipObjectsList.drawElementCallback = (rect, index, active, focused) => DrawElementOfSpecificObjectList(skipObjectsProperty, rect, index, active, focused);
        skipObjectsList.drawNoneElementCallback = (rect) => HandleObjectsDragAndDrop(skipObjectsProperty, rect);
        skipConditionsProperty = serializedObject.FindProperty(nameof(rules.skipConditions));
        skipConditionList = new ReorderableList(serializedObject, skipConditionsProperty, false, true, true, true);
        skipConditionList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Skip Conditions", Style.BoldLabel);
        skipConditionList.elementHeightCallback = ElementHeightOfSkipConditionList;
        skipConditionList.onAddCallback = OnAddElementOfConditionList;
        skipConditionList.onRemoveCallback = OnRemoveElementOfConditionList;
        skipConditionList.drawElementCallback = DrawElementOfSkipConditionList;
        skipConditionList.onSelectCallback = OnSelectElement;
#if UNITY_2021_1_OR_NEWER
        skipConditionList.multiSelect = true;
#endif
        ruleListProperty = serializedObject.FindProperty(nameof(rules.ruleList));
        RebuildRuleListAndCache();
        Undo.undoRedoPerformed += RebuildRuleListAndCache;
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
        Undo.undoRedoPerformed -= RebuildRuleListAndCache;
    }
    void OnDestroy()
    {
        Undo.undoRedoPerformed -= RebuildRuleListAndCache;
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
                    RebuildRuleListAndCache();
                }
            }
        }
    }
    public override bool RequiresConstantRepaint()
    {
        return true;
    }
    public override void OnInspectorGUI()
    {
        CopyAndPaste();
        EventType originalType = Event.current.type;
        if (intelliSenseProperty != null)
        {
            BlockMouseEvent(intelliSenseTextRect);
        }
        var minHeight = intelliSenseTextRect.y + intelliSenseTextRect.height * (Style.IntelliSenseDisplayNum + 1);
        EditorGUILayout.BeginVertical(GUILayout.MinHeight(minHeight));
        EditorGUILayout.PropertyField(enableRulesProperty);
        EditorGUI.BeginDisabledGroup(!enableRulesProperty.boolValue);
        EditorGUILayout.PropertyField(useforSecondaryCheckProperty);
        EditorGUILayout.PropertyField(targetScopeProperty);
        specificObjectList.DoLayoutList();
        commonParametersFoldout = EditorGUILayout.Foldout(commonParametersFoldout, "Common Parameters");
        if (commonParametersFoldout)
        {
            EditorGUI.indentLevel++;
            defaultIncludePathsFoldout = DrawArray(defaultIncludePathsProperty, defaultIncludePathsFoldout);
            defaultExcludePathsFoldout = DrawArray(defaultExcludePathsProperty, defaultExcludePathsFoldout);
            EditorGUI.indentLevel--;
        }
        skipConditionsFoldout = EditorGUILayout.Foldout(skipConditionsFoldout, "Skip Conditions");
        if (skipConditionsFoldout)
        {
            EditorGUILayout.PropertyField(enableSkipConditionsProperty);
            EditorGUI.BeginDisabledGroup(!enableSkipConditionsProperty.boolValue);
            EditorGUILayout.PropertyField(skipFilesFilter);
            skipObjectsList.DoLayoutList();
            for (int i = 0; i < skipConditionsProperty.arraySize; ++i)
            {
                var conditionProperty = skipConditionsProperty.GetArrayElementAtIndex(i);
                var methodProperty = conditionProperty.FindPropertyRelative("method");
                var methodNameProperty = conditionProperty.FindPropertyRelative("method.name");
                if (!methodValidationCache.ContainsKey(methodNameProperty.propertyPath))
                {
                    methodValidationCache[methodNameProperty.propertyPath] = ValidateMethod(objectMethods, methodNameProperty, methodProperty);
                    needRecalculatePropertyPath = methodNameProperty.propertyPath;
                }
            }
            skipConditionList.DoLayoutList();
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Collapse All", GUILayout.Width(100)))
        {
            var newDict = new Dictionary<int, bool>();
            foreach (var key in ruleFoldoutDict.Keys)
            {
                newDict[key] = false;
            }
            ruleFoldoutDict = newDict;
            methodValidationCache.Clear();
            needRecalculatePropertyPath = "ruleList.Array.data";
        }
        if (GUILayout.Button("Expand All", GUILayout.Width(100)))
        {
            var newDict = new Dictionary<int, bool>();
            foreach (var key in ruleFoldoutDict.Keys)
            {
                newDict[key] = true;
            }
            ruleFoldoutDict = newDict;
            methodValidationCache.Clear();
            needRecalculatePropertyPath = "ruleList.Array.data";
        }
        EditorGUILayout.EndHorizontal();
        ruleList.DoLayoutList();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
        if (intelliSenseProperty != null)
        {
            Event.current.type = originalType;
        }
        DrawIntellisense();
        DrawMethodTip();
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        if (originalType == EventType.Layout && needRecalculatePropertyPath.StartsWith("skipConditions"))
        {
            clearCacheMethod.Invoke(skipConditionList, new object[] { });
            needRecalculatePropertyPath = "";
        }
        else if (originalType == EventType.Layout && needRecalculatePropertyPath.StartsWith("ruleList.Array.data"))
        {
            clearCacheMethod.Invoke(ruleList, new object[] { });
            foreach (var reorderableList in ruleConditionLists)
            {
                clearCacheMethod.Invoke(reorderableList, new object[] { });
            }
            needRecalculatePropertyPath = "";
        }
    }
}
