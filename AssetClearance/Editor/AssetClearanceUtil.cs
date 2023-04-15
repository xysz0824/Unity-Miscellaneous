using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.IO;
using Method = AssetClearanceRules.Method;
using Report = AssetClearanceReports.Report;
using PingObject = AssetClearanceReports.PingObject;
using Status = AssetClearanceReports.Status;
using System.Text;
using UnityEngine.SceneManagement;
using System.Linq;
#if UNITY_2021_1_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

public class AssetClearanceUtil
{
    const string GUID = "7bfc268c-6863-4c9f-8838-d61da3144d26";
    public static readonly Type RuleType = typeof(AssetClearanceRules);
    public static readonly Type ReportType = typeof(AssetClearanceReports);
    public static readonly Type DefaultAssetType = typeof(DefaultAsset);
    public static readonly Type ObjectType = typeof(UnityEngine.Object);
    public static readonly Type ObjectArrayType = typeof(UnityEngine.Object[]);
    public static readonly Type ObjectListType = typeof(List<UnityEngine.Object>);
    public static readonly Type IEnumerableGenericType = typeof(IEnumerable<>);
    public static readonly Type ListGenericType = typeof(List<>);
    public static readonly Type IntType = typeof(int);
    public static readonly Type IntArrayType = typeof(int[]);
    public static readonly Type FloatType = typeof(float);
    public static readonly Type FloatArrayType = typeof(float[]);
    public static readonly Type BoolType = typeof(bool);
    public static readonly Type BoolArrayType = typeof(bool[]);
    public static readonly Type StringType = typeof(string);
    public static readonly Type StringArrayType = typeof(string[]);
    static readonly Type[] AllowedBasicTypes = {
        IntType,
        IntArrayType,
        FloatType,
        FloatArrayType,
        BoolType,
        BoolArrayType,
        StringType,
        StringArrayType,
    };
    public static readonly string PingObjectsName = "pingobjects";

    public static string GetRootAssetPath()
    {
        var guid = AssetDatabase.FindAssets(GUID);
        if (guid.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid[0]);
            path = path.Remove(path.Length - GUID.Length, GUID.Length);
            path = path.Substring(7);
            return path;
        }
        return null;
    }
    public static string GetIconPath(string fileName)
    {
        return "Assets/" + GetRootAssetPath() + "/Icons/" + fileName;
    }
    public static TreeViewItem BuildGroup(TreeViewItem root, Dictionary<string, TreeViewItem> dict, string group)
    {
        if (string.IsNullOrEmpty(group)) return root;
        TreeViewItem parent = BuildGroup(root, dict, Path.GetDirectoryName(group));
        var name = Path.GetFileName(group);
        if (!dict.ContainsKey(name))
        {
            dict[name] = new TreeViewItem { id = 0, displayName = name, children = new List<TreeViewItem>() };
            parent.AddChild(dict[name]);
        }
        return dict[name];
    }
    public static void SetIndexToTreeItems(TreeViewItem root, ref int index)
    {
        root.id = index++;
        foreach (var child in root.children)
        {
            SetIndexToTreeItems(child, ref index);
        }
    }
    public static TreeViewItem GetTreeViewLeafRecursively(TreeViewItem parent, int id)
    {
        if (!parent.hasChildren)
        {
            return parent.id == id ? parent : null;
        }
        foreach (var child in parent.children)
        {
            var item = GetTreeViewLeafRecursively(child, id);
            if (item != null) return item;
        }
        return null;
    }
    public static bool IsObjectTypeValid(Type parameterType)
    {
        return parameterType == ObjectType || parameterType.IsSubclassOf(ObjectType);
    }
    public static bool GetGameObjectRelativePath(GameObject root, GameObject gameObject, ref string relativePath)
    {
        if (root == gameObject) return true;
        for (int i = 0; i < root.transform.childCount; ++i)
        {
            var child = root.transform.GetChild(i);
            if (GetGameObjectRelativePath(child.gameObject, gameObject, ref relativePath))
            {
                relativePath = child.name + "/" + relativePath;
                return true;
            }
        }
        return false;
    }
    public static List<UnityEngine.Object> SelectGameObjectsInPrefabStage(GameObject assetRoot, GameObject stageRoot, List<UnityEngine.Object> assetObjs)
    {
        var stageObjs = new List<UnityEngine.Object>();
        if (assetRoot == null || stageRoot == null) return stageObjs;
        if (assetObjs.Contains(assetRoot)) stageObjs.Add(stageRoot);
        for (int i = 0; i < assetRoot.transform.childCount; ++i)
        {
            var assetChild = assetRoot.transform.GetChild(i);
            var stageChild = stageRoot.transform.GetChild(i);
            stageObjs.AddRange(SelectGameObjectsInPrefabStage(assetChild?.gameObject, stageChild?.gameObject, assetObjs));
        }
        return stageObjs;
    }
    public static List<MethodInfo> GetMethods()
    {
        var classType = typeof(AssetClearanceMethods);
        var methods = new List<MethodInfo>(classType.GetMethods());
        for (int i = 0; i < methods.Count; ++i)
        {
            var parameters = methods[i].GetParameters();
            var needRemove = !methods[i].IsStatic || !methods[i].IsPublic || parameters.Length == 0 || methods[i].GetCustomAttribute<AssetClearanceMethod>() == null || 
                methods[i].ReturnParameter.ParameterType != typeof(bool);
            if (!needRemove)
            {
                needRemove = !IsObjectTypeValid(parameters[0].ParameterType);
            }
            if (!needRemove && parameters.Length > 1)
            {
                for (int k = 1; k < parameters.Length; ++k)
                {
                    if (parameters[k].ParameterType.IsEnum || (parameters[k].ParameterType.IsArray && parameters[k].ParameterType.GetElementType().IsEnum))
                    {
                        continue;
                    }
                    if (!Array.Exists(AllowedBasicTypes, (type) => parameters[k].ParameterType == type) &&
                        !(parameters[k].ParameterType.IsSubclassOf(ObjectType) || (parameters[k].ParameterType.IsArray && parameters[k].ParameterType.GetElementType().IsSubclassOf(ObjectType))))
                    {
                        needRemove = true;
                        break;
                    }
                }
            }
            if (needRemove)
            {
                methods.RemoveAt(i);
                i--;
            }
        }
        return methods;
    }
    public static List<MethodInfo> GetFixMethods()
    {
        var classType = typeof(AssetClearanceFixMethods);
        var methods = new List<MethodInfo>(classType.GetMethods());
        for (int i = 0; i < methods.Count; ++i)
        {
            var parameters = methods[i].GetParameters();
            var needRemove = !methods[i].IsStatic || !methods[i].IsPublic || parameters.Length == 0 || methods[i].GetCustomAttribute<AssetClearanceMethod>() == null ||
                methods[i].ReturnParameter.ParameterType != typeof(bool);
            if (!needRemove)
            {
                needRemove = !IsObjectTypeValid(parameters[0].ParameterType);
            }
            if (!needRemove && parameters.Length > 1)
            {
                int pingObjectsParameterCount = 0;
                for (int k = 1; k < parameters.Length; ++k)
                {
                    if (parameters[k].Name.ToLower() == PingObjectsName && parameters[k].ParameterType.FullName.StartsWith(ObjectListType.FullName))
                    {
                        if (parameters[k].IsOut)
                        {
                            needRemove = true;
                            break;
                        }
                        pingObjectsParameterCount++;
                        if (pingObjectsParameterCount > 1)
                        {
                            needRemove = true;
                            break;
                        }
                        continue;
                    }
                    if (parameters[k].ParameterType.IsEnum || (parameters[k].ParameterType.IsArray && parameters[k].ParameterType.GetElementType().IsEnum))
                    {
                        continue;
                    }
                    if (!Array.Exists(AllowedBasicTypes, (type) => parameters[k].ParameterType == type) &&
                        !(parameters[k].ParameterType.IsSubclassOf(ObjectType) || (parameters[k].ParameterType.IsArray && parameters[k].ParameterType.GetElementType().IsSubclassOf(ObjectType))))
                    {
                        needRemove = true;
                        break;
                    }
                }
            }
            if (needRemove)
            {
                methods.RemoveAt(i);
                i--;
            }
        }
        return methods;
    }
    public static string GetDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (!Char.IsUpper(name[0])) name = Char.ToUpperInvariant(name[0]) + name.Substring(1);
        for (int i = 1; i < name.Length; ++i)
        {
            if (Char.IsUpper(name[i]) && name[i - 1] != ' ')
            {
                name = name.Insert(i, " ");
                i++;
            }
        }
        return name;
    }
    public static string GetPingObjectDisplayName(string assetPath, PingObject pingObject)
    {
        if (pingObject == null) return "";
        if (pingObject.assetPath != assetPath) return pingObject.assetPath;
        if (pingObject.subAsset) return pingObject.Reference.name;
        return pingObject.referencerPath != null ? pingObject.referencerPath : "";
    }
    public static bool NeedUnloadAsset(Type assetType, string assetPath)
    {
        return !AssetDatabase.IsMainAssetAtPathLoaded(assetPath) && assetType != typeof(GameObject);
    }
    public static object GetSerializedPropertyValue(SerializedProperty serializedProperty)
    {
        switch (serializedProperty.propertyType)
        {
            case SerializedPropertyType.Integer:
                return serializedProperty.intValue;
            case SerializedPropertyType.Boolean:
                return serializedProperty.boolValue;
            case SerializedPropertyType.Float:
                return serializedProperty.floatValue;
            case SerializedPropertyType.String:
                return serializedProperty.stringValue;
            case SerializedPropertyType.Color:
                return serializedProperty.colorValue;
            case SerializedPropertyType.ObjectReference:
                return serializedProperty.objectReferenceValue;
            case SerializedPropertyType.LayerMask:
                return serializedProperty.intValue;
            case SerializedPropertyType.Enum:
                return serializedProperty.enumValueIndex;
            case SerializedPropertyType.Vector2:
                return serializedProperty.vector2Value;
            case SerializedPropertyType.Vector3:
                return serializedProperty.vector3Value;
            case SerializedPropertyType.Vector4:
                return serializedProperty.vector4Value;
            case SerializedPropertyType.Rect:
                return serializedProperty.rectValue;
            case SerializedPropertyType.ArraySize:
                return serializedProperty.intValue;
            case SerializedPropertyType.Character:
                return serializedProperty.intValue;
            case SerializedPropertyType.AnimationCurve:
                return serializedProperty.animationCurveValue;
            case SerializedPropertyType.Bounds:
                return serializedProperty.boundsValue;
            case SerializedPropertyType.ExposedReference:
                return serializedProperty.exposedReferenceValue;
            case SerializedPropertyType.Vector2Int:
                return serializedProperty.vector2IntValue;
            case SerializedPropertyType.Vector3Int:
                return serializedProperty.vector3IntValue;
            case SerializedPropertyType.RectInt:
                return serializedProperty.rectIntValue;
            case SerializedPropertyType.BoundsInt:
                return serializedProperty.boundsIntValue;
        }
        return null;
    }
    public static bool SameValueSerializedProperty(SerializedProperty a, SerializedProperty b)
    {
        switch (a.propertyType)
        {
            case SerializedPropertyType.Integer:
                return a.intValue.Equals(b.intValue);
            case SerializedPropertyType.Boolean:
                return a.boolValue.Equals(b.boolValue);
            case SerializedPropertyType.Float:
                return a.floatValue.Equals(b.floatValue);
            case SerializedPropertyType.String:
                return a.stringValue.Equals(b.stringValue);
            case SerializedPropertyType.Color:
                return a.colorValue.Equals(b.colorValue);
            case SerializedPropertyType.ObjectReference:
                return a.objectReferenceValue.Equals(b.objectReferenceValue);
            case SerializedPropertyType.LayerMask:
                return a.intValue.Equals(b.intValue);
            case SerializedPropertyType.Enum:
                return a.enumValueIndex.Equals(b.enumValueIndex);
            case SerializedPropertyType.Vector2:
                return a.vector2Value.Equals(b.vector2Value);
            case SerializedPropertyType.Vector3:
                return a.vector3Value.Equals(b.vector3Value);
            case SerializedPropertyType.Vector4:
                return a.vector4Value.Equals(b.vector4Value);
            case SerializedPropertyType.Rect:
                return a.rectValue.Equals(b.rectValue);
            case SerializedPropertyType.ArraySize:
                return a.intValue.Equals(b.intValue);
            case SerializedPropertyType.Character:
                return a.intValue.Equals(b.intValue);
            case SerializedPropertyType.AnimationCurve:
                return a.animationCurveValue.Equals(b.animationCurveValue);
            case SerializedPropertyType.Bounds:
                return a.boundsValue.Equals(b.boundsValue);
            case SerializedPropertyType.ExposedReference:
                return a.exposedReferenceValue.Equals(b.exposedReferenceValue);
            case SerializedPropertyType.Vector2Int:
                return a.vector2IntValue.Equals(b.vector2IntValue);
            case SerializedPropertyType.Vector3Int:
                return a.vector3IntValue.Equals(b.vector3IntValue);
            case SerializedPropertyType.RectInt:
                return a.rectIntValue.Equals(b.rectIntValue);
            case SerializedPropertyType.BoundsInt:
                return a.boundsIntValue.Equals(b.boundsIntValue);
        }
        return false;
    }
    public static void ResetSerializedPropertyValue(SerializedProperty serializedProperty)
    {
        switch (serializedProperty.propertyType)
        {
            case SerializedPropertyType.Integer:
                serializedProperty.intValue = default;
                break;
            case SerializedPropertyType.Boolean:
                serializedProperty.boolValue = default;
                break;
            case SerializedPropertyType.Float:
                serializedProperty.floatValue = default;
                break;
            case SerializedPropertyType.String:
                serializedProperty.stringValue = "";
                break;
            case SerializedPropertyType.Color:
                serializedProperty.colorValue = default;
                break;
            case SerializedPropertyType.ObjectReference:
                serializedProperty.objectReferenceValue = default;
                break;
            case SerializedPropertyType.LayerMask:
                serializedProperty.intValue = default;
                break;
            case SerializedPropertyType.Enum:
                serializedProperty.enumValueIndex = default;
                break;
            case SerializedPropertyType.Vector2:
                serializedProperty.vector2Value = default;
                break;
            case SerializedPropertyType.Vector3:
                serializedProperty.vector3Value = default;
                break;
            case SerializedPropertyType.Vector4:
                serializedProperty.vector4Value = default;
                break;
            case SerializedPropertyType.Rect:
                serializedProperty.rectValue = default;
                break;
            case SerializedPropertyType.ArraySize:
                serializedProperty.intValue = default;
                break;
            case SerializedPropertyType.Character:
                serializedProperty.intValue = default;
                break;
            case SerializedPropertyType.AnimationCurve:
                serializedProperty.animationCurveValue = default;
                break;
            case SerializedPropertyType.Bounds:
                serializedProperty.boundsValue = default;
                break;
            case SerializedPropertyType.ExposedReference:
                serializedProperty.exposedReferenceValue = default;
                break;
            case SerializedPropertyType.Vector2Int:
                serializedProperty.vector2IntValue = default;
                break;
            case SerializedPropertyType.Vector3Int:
                serializedProperty.vector3IntValue = default;
                break;
            case SerializedPropertyType.RectInt:
                serializedProperty.rectIntValue = default;
                break;
            case SerializedPropertyType.BoundsInt:
                serializedProperty.boundsIntValue = default;
                break;
        }
    }
    public static void CopySerializedPropertyValue(SerializedProperty source, SerializedProperty dest)
    {
        switch (source.propertyType)
        {
            case SerializedPropertyType.Integer:
                dest.intValue = source.intValue;
                break;
            case SerializedPropertyType.Boolean:
                dest.boolValue = source.boolValue;
                break;
            case SerializedPropertyType.Float:
                dest.floatValue = source.floatValue;
                break;
            case SerializedPropertyType.String:
                dest.stringValue = source.stringValue;
                break;
            case SerializedPropertyType.Color:
                dest.colorValue = source.colorValue;
                break;
            case SerializedPropertyType.ObjectReference:
                dest.objectReferenceValue = source.objectReferenceValue;
                break;
            case SerializedPropertyType.LayerMask:
                dest.intValue = source.intValue;
                break;
            case SerializedPropertyType.Enum:
                dest.enumValueIndex = source.enumValueIndex;
                break;
            case SerializedPropertyType.Vector2:
                dest.vector2Value = source.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                dest.vector3Value = source.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                dest.vector4Value = source.vector4Value;
                break;
            case SerializedPropertyType.Rect:
                dest.rectValue = source.rectValue;
                break;
            case SerializedPropertyType.ArraySize:
                dest.intValue = source.intValue;
                break;
            case SerializedPropertyType.Character:
                dest.intValue = source.intValue;
                break;
            case SerializedPropertyType.AnimationCurve:
                dest.animationCurveValue = source.animationCurveValue;
                break;
            case SerializedPropertyType.Bounds:
                dest.boundsValue = source.boundsValue;
                break;
            case SerializedPropertyType.ExposedReference:
                dest.exposedReferenceValue = source.exposedReferenceValue;
                break;
            case SerializedPropertyType.Vector2Int:
                dest.vector2IntValue = source.vector2IntValue;
                break;
            case SerializedPropertyType.Vector3Int:
                dest.vector3IntValue = source.vector3IntValue;
                break;
            case SerializedPropertyType.RectInt:
                dest.rectIntValue = source.rectIntValue;
                break;
            case SerializedPropertyType.BoundsInt:
                dest.boundsIntValue = source.boundsIntValue;
                break;
        }
    }
    public static void CopySerializedProperty(SerializedProperty source, SerializedProperty dest)
    {
        if (source.type != dest.type) return;
        var sourceIt = source.Copy();
        var sourceEnd = source.GetEndProperty();
        var destIt = dest.Copy();
        sourceIt.NextVisible(true);
        destIt.NextVisible(true);
        if (sourceIt.propertyPath == sourceEnd.propertyPath)
        {
            CopySerializedPropertyValue(source, dest);
        }
        while (sourceIt.propertyPath != sourceEnd.propertyPath)
        {
            CopySerializedPropertyValue(sourceIt, destIt);
            sourceIt.NextVisible(true);
            destIt.NextVisible(true);
        }
        dest.serializedObject.ApplyModifiedProperties();
        dest.serializedObject.Update();
    }
    public static void ResetSerializedProperty(SerializedProperty serializedProperty)
    {
        var iterator = serializedProperty.Copy();
        var endProperty = serializedProperty.GetEndProperty();
        iterator.NextVisible(true);
        while (iterator.propertyPath != endProperty.propertyPath)
        {
            if (iterator.isArray) iterator.ClearArray();
            else ResetSerializedPropertyValue(iterator);
            iterator.NextVisible(true);
        }
    }
    public static bool EqualSerializedProperty(SerializedProperty a, SerializedProperty b)
    {
        if (a.type != b.type) return false;
        if (a.isArray)
        {
            if (a.arraySize != b.arraySize) return false;
            for (int i = 0; i < a.arraySize; ++i)
            {
                var elementA = a.GetArrayElementAtIndex(i);
                var elementB = b.GetArrayElementAtIndex(i);
                if (!EqualSerializedProperty(elementA, elementB))
                {
                    Debug.Log(elementA.propertyPath);
                    return false;
                }
            }
            return true;
        }
        else if (a.propertyType == SerializedPropertyType.Generic)
        {
            var aIt = a.Copy();
            var bIt = b.Copy();
            var aEnd = a.GetEndProperty();
            aIt.NextVisible(true);
            bIt.NextVisible(true);
            while (aIt.propertyPath != aEnd.propertyPath)
            {
                if (!EqualSerializedProperty(aIt, bIt))
                {
                    Debug.Log(aIt.propertyPath);
                    return false;
                }
                aIt.NextVisible(false);
                bIt.NextVisible(false);
            }
            return true;
        }
        else
        {
            return SameValueSerializedProperty(a, b);
        }
    }
    static Dictionary<Type, SerializedProperty> paramCountsPropertyDict = new Dictionary<Type, SerializedProperty>();
    static Dictionary<Type, SerializedProperty> arrayParamCountsPropertyDict = new Dictionary<Type, SerializedProperty>();
    public static Dictionary<Type, SerializedProperty> GetParamCountsPropertyDict(SerializedProperty property, bool arrayTypeOnly = false)
    {
        var intParamCountsProperty = property.FindPropertyRelative("intParamCounts");
        var floatParamCountsProperty = property.FindPropertyRelative("floatParamCounts");
        var boolParamCountsProperty = property.FindPropertyRelative("boolParamCounts");
        var stringParamCountsProperty = property.FindPropertyRelative("stringParamCounts");
        var objectParamCountsProeprty = property.FindPropertyRelative("objectParamCounts");
        if (arrayTypeOnly)
        {
            arrayParamCountsPropertyDict[IntArrayType] = intParamCountsProperty;
            arrayParamCountsPropertyDict[FloatArrayType] = floatParamCountsProperty;
            arrayParamCountsPropertyDict[BoolArrayType] = boolParamCountsProperty;
            arrayParamCountsPropertyDict[StringArrayType] = stringParamCountsProperty;
            arrayParamCountsPropertyDict[ObjectArrayType] = objectParamCountsProeprty;
            return arrayParamCountsPropertyDict;
        }
        else
        {
            paramCountsPropertyDict[IntType] = paramCountsPropertyDict[IntArrayType] = intParamCountsProperty;
            paramCountsPropertyDict[FloatType] = paramCountsPropertyDict[FloatArrayType] = floatParamCountsProperty;
            paramCountsPropertyDict[BoolType] = paramCountsPropertyDict[BoolArrayType] = boolParamCountsProperty;
            paramCountsPropertyDict[StringType] = paramCountsPropertyDict[StringArrayType] = stringParamCountsProperty;
            paramCountsPropertyDict[ObjectType] = paramCountsPropertyDict[ObjectArrayType] = objectParamCountsProeprty;
            return paramCountsPropertyDict;
        }
    }
    static Dictionary<Type, SerializedProperty> parametersPropertyDict = new Dictionary<Type, SerializedProperty>();
    public static Dictionary<Type, SerializedProperty> GetParametersPropertyDict(SerializedProperty property)
    {
        var intParamsProperty = property.FindPropertyRelative("intParams");
        var floatParamsProperty = property.FindPropertyRelative("floatParams");
        var boolParamsProperty = property.FindPropertyRelative("boolParams");
        var stringParamsProperty = property.FindPropertyRelative("stringParams");
        var objectParamsProeprty = property.FindPropertyRelative("objectParams");
        parametersPropertyDict[IntType] = parametersPropertyDict[IntArrayType] = intParamsProperty;
        parametersPropertyDict[FloatType] = parametersPropertyDict[FloatArrayType] = floatParamsProperty;
        parametersPropertyDict[BoolType] = parametersPropertyDict[BoolArrayType] = boolParamsProperty;
        parametersPropertyDict[StringType] = parametersPropertyDict[StringArrayType] = stringParamsProperty;
        parametersPropertyDict[ObjectType] = parametersPropertyDict[ObjectArrayType] = objectParamsProeprty;
        return parametersPropertyDict;
    }
    public class Index
    {
        public int value;
    }
    static Dictionary<Type, Index> paramIndexDict = new Dictionary<Type, Index>();
    public static Dictionary<Type, Index> GetParamCountsIndexDict()
    {
        paramIndexDict[IntType] = paramIndexDict[IntArrayType] = new Index();
        paramIndexDict[FloatType] = paramIndexDict[FloatArrayType] = new Index();
        paramIndexDict[BoolType] = paramIndexDict[BoolArrayType] = new Index();
        paramIndexDict[StringType] = paramIndexDict[StringArrayType] = new Index();
        paramIndexDict[ObjectType] = paramIndexDict[ObjectArrayType] = new Index();
        return paramIndexDict;
    }
    static Dictionary<Type, Index> paramCountDict = new Dictionary<Type, Index>();
    public static Dictionary<Type, Index> GetParamCountict()
    {
        paramCountDict[IntType] = paramCountDict[IntArrayType] = new Index();
        paramCountDict[FloatType] = paramCountDict[FloatArrayType] = new Index();
        paramCountDict[BoolType] = paramCountDict[BoolArrayType] = new Index();
        paramCountDict[StringType] = paramCountDict[StringArrayType] = new Index();
        paramCountDict[ObjectType] = paramCountDict[ObjectArrayType] = new Index();
        return paramCountDict;
    }
    static Dictionary<Type, Array> paramCountsDict = new Dictionary<Type, Array>();
    public static Dictionary<Type, Array> GetParamCountsDict(Method method)
    {
        paramCountsDict[IntType] = paramCountsDict[IntArrayType] = method.intParamCounts;
        paramCountsDict[FloatType] = paramCountsDict[FloatArrayType] = method.floatParamCounts;
        paramCountsDict[BoolType] = paramCountsDict[BoolArrayType] = method.boolParamCounts;
        paramCountsDict[StringType] = paramCountsDict[StringArrayType] = method.stringParamCounts;
        paramCountsDict[ObjectType] = paramCountsDict[ObjectArrayType] = method.objectParamCounts;
        return paramCountsDict;
    }
    static Dictionary<Type, Array> parametersDict = new Dictionary<Type, Array>();
    public static Dictionary<Type, Array> GetParametersDict(Method method)
    {
        parametersDict[IntType] = parametersDict[IntArrayType] = method.intParams;
        parametersDict[FloatType] = parametersDict[FloatArrayType] = method.floatParams;
        parametersDict[BoolType] = parametersDict[BoolArrayType] = method.boolParams;
        parametersDict[StringType] = parametersDict[StringArrayType] = method.stringParams;
        parametersDict[ObjectType] = parametersDict[ObjectArrayType] = method.objectParams;
        return parametersDict;
    }
    public static List<AssetClearanceRules> AutoSearchRules()
    {
        var rulesList = new List<AssetClearanceRules>();
        var files = AssetDatabase.FindAssets("t:AssetClearanceRules");
        foreach (var guid in files)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var rules = AssetDatabase.LoadAssetAtPath<AssetClearanceRules>(path);
            rulesList.Add(rules);
        }
        return rulesList;
    }
    public static AssetClearanceReports SaveReportsToAsset(string path, List<Report> reports, string comment)
    {
        var reportsAsset = ScriptableObject.CreateInstance<AssetClearanceReports>();
        reportsAsset.reports.AddRange(reports);
        reportsAsset.comment = comment;
        AssetDatabase.CreateAsset(reportsAsset, path);
        AssetDatabase.SaveAssets();
        return reportsAsset;
    }
    public static void SaveReportsToCSV(string path, List<Report> reports)
    {
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        var builder = new StringBuilder();
        builder.Append("Group,Status,Rules,Rule,AssetPath,PingObject,Log,Priority,Note\n");
        foreach (var report in reports)
        {
            if (string.IsNullOrEmpty(report.log)) continue;
            builder.Append(report.group).Append(",");
            builder.Append(report.status).Append(",");
            builder.Append(AssetDatabase.GetAssetPath(report.rules)).Append(",");
            builder.Append(report.ruleName).Append(",");
            builder.Append(report.assetPath).Append(",");
            builder.Append(GetPingObjectDisplayName(report.assetPath, report.pingObject)).Append(",");
            builder.Append(report.log).Append(",");
            builder.Append(report.status == Status.Fixing ? report.priority.ToString() : "").Append(",");
            builder.Append(report.status == Status.Fixing || report.status == Status.Ignore ? report.note : "");
            builder.Append("\n");
        }
        writer.Write(builder.ToString());
    }
}
