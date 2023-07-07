using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using Rule = AssetClearanceRules.Rule;
using Condition = AssetClearanceRules.Condition;
using Method = AssetClearanceRules.Method;
using Log = AssetClearanceMethods.Log;
using LogType = AssetClearanceRules.LogType;
using TargetScope = AssetClearanceRules.TargetScope;
using LogicOperator = AssetClearanceRules.LogicOperator;
using Report = AssetClearanceReports.Report;
using PingObject = AssetClearanceReports.PingObject;

[InitializeOnLoad]
public class AssetClearanceDefineSymbols
{
    static readonly string[] Symbols = new string[] { "ASSET_CLEARANCE" };
    static AssetClearanceDefineSymbols()
    {
        var definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        var allDefines = definesString.Split(';').ToList();
        if (!allDefines.Where((define) => Array.Exists(Symbols, (d) => d == define)).Any())
        {
            allDefines.AddRange(Symbols.Except(allDefines));
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", allDefines.ToArray()));
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}

public class AssetClearance
{
    static int reportGroupIndex;
    public class Wildcard : Regex
    {
        public Wildcard(string pattern) : base(ToRegex(pattern), RegexOptions.IgnoreCase) { }
        public Wildcard(string pattern, RegexOptions options) : base(ToRegex(pattern), options) { }
        public static string ToRegex(string pattern)
        {
            var splits = pattern.Split(',');
            if (splits.Length == 0) return "";
            var result = "";
            for (int i = 0; i < splits.Length; ++i)
            {
                result += Escape(splits[i]).Replace("\\*", ".*").Replace("\\?", ".");
                if (i < splits.Length - 1) result += "|";
            }
            return result;
        }
    }
    static readonly Regex logDataRegex = new Regex("\\{(.+?)\\}");
    [Serializable]
    public class TargetObject
    {
        public UnityEngine.Object obj;
    }
    public enum Status
    {
        False = 0,
        True = 1,
        Ignored = 3
    }
    public class ClearObjectRequest
    {
        public string assetPath;
    }
    static readonly List<MethodInfo> objectMethods = AssetClearanceUtil.GetMethods();
    static readonly List<MethodInfo> objectFixMethods = AssetClearanceUtil.GetFixMethods();
    static bool DoMethod(object rawObj, Method method, MethodInfo methodInfo, ParameterInfo[] parameters, List<PingObject> pingObjects)
    {
        var paramValues = new object[parameters.Length];
        paramValues[0] = rawObj;
        var paramCountsDict = AssetClearanceUtil.GetParamCountsDict(method);
        var paramsDict = AssetClearanceUtil.GetParametersDict(method);
        var paramCountsIndex = AssetClearanceUtil.GetParamCountsIndexDict();
        var paramCount = AssetClearanceUtil.GetParamCountict();
        for (int i = 1; i < paramValues.Length; ++i)
        {
            if (parameters[i].IsOut) continue;
            if (parameters[i].Name.ToLower() == AssetClearanceUtil.PingObjectsName)
            {
                paramValues[i] = pingObjects.Where(i => i != null && i.Reference != null).Select(i => i.Reference).ToList();
                continue;
            }
            var parameterType = parameters[i].ParameterType;
            if (parameters[i].ParameterType.IsEnum) parameterType = AssetClearanceUtil.IntType;
            if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsEnum) parameterType = AssetClearanceUtil.IntArrayType;
            if (parameters[i].ParameterType.IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectType;
            if (parameters[i].ParameterType.IsArray && parameters[i].ParameterType.GetElementType().IsSubclassOf(AssetClearanceUtil.ObjectType)) parameterType = AssetClearanceUtil.ObjectArrayType;
            var count = (int)paramCountsDict[parameterType].GetValue(paramCountsIndex[parameterType].value++);
            if (!parameterType.IsArray)
            {
                var value = paramsDict[parameterType].GetValue(paramCount[parameterType].value);
                if (value.ToString() == "null")
                {
                    value = null;
                }
                paramValues[i] = value;
            }
            else
            {
                var valueArray = Array.CreateInstance(parameters[i].ParameterType.GetElementType(), count);
                for (int k = 0; k < count; ++k)
                {
                    var value = paramsDict[parameterType].GetValue(paramCount[parameterType].value + k);
                    valueArray.SetValue(value, k);
                }
                paramValues[i] = valueArray;
            }
            paramCount[parameterType].value += count;
        }
        return (bool)methodInfo.Invoke(null, paramValues);
    }
    static void TransformPingObject(UnityEngine.Object root, GameObject referencer, UnityEngine.Object obj, out PingObject pingObject)
    {
        pingObject = null;
        if (referencer == null && obj == null) return;
        pingObject = new PingObject(root, referencer, obj);
    }
    static Status CheckCondition(AssetClearanceRules rules, bool outputReports, string assetPath, object rawObj, Condition condition)
    {
        var type = rawObj.GetType();
        var methodInfo = condition.ValidateMethod(objectMethods);
        if (methodInfo == null) return Status.Ignored;
        var parameters = methodInfo.GetParameters();
        var objectValidation = parameters[0].GetCustomAttribute<ObjectValidation>();
        if (!type.IsGenericType)
        {
            var parameterType = parameters[0].ParameterType;
            if (type != parameterType && !type.IsSubclassOf(parameterType))
            {
                return Status.Ignored;
            }
            if (objectValidation != null && !objectValidation.DoValidate(rawObj as UnityEngine.Object))
            {
                return Status.Ignored;
            }
        }
        else
        {
            return Status.Ignored;
        }
        AssetClearanceMethods.ClearLogs();
        AssetClearanceMethods.ClearPingObjects();
        AssetClearanceMethods.rules = rules;
        var retval = DoMethod(rawObj, condition.method, methodInfo, parameters, null);
        AssetClearanceMethods.rules = null;
        if (condition.negation) retval = !retval;
        return retval ? Status.True : Status.False;
    }
    static Status CheckConditions(bool outputReports, AssetClearanceRules rules, Rule rule, string assetPath, UnityEngine.Object obj, List<Condition> conditions, int reportGroup, out List<Report> reports)
    {
        reports = new List<Report>();
        if (conditions.Count == 0) return Status.Ignored;
        var blockCounts = new List<int>();
        blockCounts.Add(1);
        for (int i = 1; i < conditions.Count; ++i)
        {
            if (conditions[i].priority != conditions[i - 1].priority)
            {
                blockCounts.Add(0);
            }
            blockCounts[blockCounts.Count - 1]++;
        }
        var blockResults = new bool[blockCounts.Count];
        var blockOperators = new LogicOperator[blockCounts.Count];
        var ignoredCount = 0;
        int conditionIndex = 0;
        var trueLogs = new List<Log>();
        var truePingObjects = new List<PingObject>();
        var falseLogs = new List<Log>();
        var falsePingObjects = new List<PingObject>();
        for (int i = 0; i < blockCounts.Count; ++i)
        {
            PingObject pingObject;
            var blockResult = CheckCondition(rules, outputReports, assetPath, obj, conditions[conditionIndex]);
            if (blockResult == Status.Ignored)
            {
                ignoredCount++;
                blockResult = Status.True;
            }
            else
            {
                var resultBool = blockResult == Status.True;
                if (resultBool)
                {
                    for (int k = 0; k < AssetClearanceMethods.LogCount; ++k)
                    {
                        trueLogs.Add(AssetClearanceMethods.GetLog(k));
                        TransformPingObject(obj, AssetClearanceMethods.GetPingReferencer(k), AssetClearanceMethods.GetPingObject(k), out pingObject);
                        truePingObjects.Add(pingObject);
                    }
                }
                else
                {
                    for (int k = 0; k < AssetClearanceMethods.LogCount; ++k)
                    {
                        falseLogs.Add(AssetClearanceMethods.GetLog(k));
                        TransformPingObject(obj, AssetClearanceMethods.GetPingReferencer(k), AssetClearanceMethods.GetPingObject(k), out pingObject);
                        falsePingObjects.Add(pingObject);
                    }
                }
            }
            for (int k = 1; k < blockCounts[i]; ++k)
            {
                var nextResult = CheckCondition(rules, outputReports, assetPath, obj, conditions[conditionIndex + k]);
                if (nextResult == Status.Ignored)
                {
                    continue;
                }
                var nextBool = nextResult == Status.True;
                if (nextBool)
                {
                    for (int j = 0; j < AssetClearanceMethods.LogCount; ++j)
                    {
                        trueLogs.Add(AssetClearanceMethods.GetLog(j));
                        TransformPingObject(obj, AssetClearanceMethods.GetPingReferencer(j), AssetClearanceMethods.GetPingObject(j), out pingObject);
                        truePingObjects.Add(pingObject);
                    }
                }
                else
                {
                    for (int j = 0; j < AssetClearanceMethods.LogCount; ++j)
                    {
                        falseLogs.Add(AssetClearanceMethods.GetLog(j));
                        TransformPingObject(obj, AssetClearanceMethods.GetPingReferencer(j), AssetClearanceMethods.GetPingObject(j), out pingObject);
                        falsePingObjects.Add(pingObject);
                    }
                }
                switch (conditions[conditionIndex + k - 1].logicOperator)
                {
                    case LogicOperator.AND:
                        blockResult &= nextResult;
                        break;
                    case LogicOperator.OR:
                        blockResult |= nextResult;
                        break;
                }
            }
            blockResults[i] = blockResult == Status.False ? false : true;
            conditionIndex += blockCounts[i];
            blockOperators[i] = conditions[conditionIndex - 1].logicOperator;
        }
        var result = blockResults[0];
        for (int i = 0; i < blockOperators.Length - 1;++i)
        {
            switch (blockOperators[i])
            {
                case LogicOperator.AND:
                    result &= blockResults[i + 1];
                    break;
                case LogicOperator.OR:
                    result |= blockResults[i + 1];
                    break;
            }
        }
        var finalStatus = ignoredCount == blockResults.Length ? Status.Ignored : (result ? Status.True : Status.False);
        if (finalStatus == Status.True && outputReports)
        {
            for (int i = 0; i < trueLogs.Count; ++i)
            {
                var log = trueLogs[i];
                var pingObject = truePingObjects[i];
                var report = GenerateReport(rules, rule, finalStatus, log, pingObject, assetPath, reportGroup);
                if (report.log != null) reports.Add(report);
            }
        }
        else if (finalStatus == Status.False && outputReports)
        {
            for (int i = 0; i < falseLogs.Count; ++i)
            {
                var log = falseLogs[i];
                var pingObject = falsePingObjects[i];
                var report = GenerateReport(rules, rule, finalStatus, log, pingObject, assetPath, reportGroup);
                if (report.log != null) reports.Add(report);
            }
        }
        return finalStatus;
    }
    static Status CheckConditions(AssetClearanceRules rules, Rule rule, string assetPath, UnityEngine.Object obj, List<Condition> conditions, int reportGroup, out List<Report> reports)
    {
        return CheckConditions(true, rules, rule, assetPath, obj, conditions, reportGroup, out reports);
    }
    static Status CheckConditions(UnityEngine.Object obj, List<Condition> conditions)
    {
        return CheckConditions(false, null, null, null, obj, conditions, 0, out _);
    }
    static bool WildCardFilter(AssetClearanceRules rules, Wildcard wildcard, string path)
    {
        return rules.enableSkipConditions && wildcard != null && !string.IsNullOrWhiteSpace(rules.skipFilesFilter) && wildcard.IsMatch(path);
    }
    static bool ObjectsFilter(AssetClearanceRules rules, string path)
    {
        if (!rules.enableSkipConditions || rules.skipObjects == null) return false;
        var directory = Path.GetDirectoryName(path);
        foreach (var obj in rules.skipObjects)
        {
            if (obj.obj == null) continue;
            var objPath = AssetDatabase.GetAssetPath(obj.obj);
            if (obj.obj is DefaultAsset && directory.StartsWith(Path.GetDirectoryName(objPath + "/")))
            {
                return true;
            }
            if (objPath == path) return true;
        }
        return false;
    }
    static bool CheckSkipConditions(AssetClearanceRules rules, string assetPath, Type assetType)
    {
        if (rules.enableSkipConditions)
        {
            var needUnload = AssetClearanceUtil.NeedUnloadAsset(assetType, assetPath);
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var result = CheckConditions(asset, rules.skipConditions);
            if (needUnload && asset)
            {
                Resources.UnloadAsset(asset);
            }
            return result == Status.True;
        }
        return false;
    }
    static Report GenerateReport(AssetClearanceRules rules, Rule rule, Status status, Log log, PingObject pingObject, string assetPath, int group)
    {
        var report = new Report();
        report.rules = rules;
        report.ruleName = rule.name;
        report.assetPath = assetPath;
        report.pingObject = pingObject;
        report.logType = status == Status.True ? rule.trueLogType : (status == Status.False ? rule.falseLogType : LogType.None);
        if (report.logType != LogType.None)
        {
            report.log = log.content;
            report.logOrder = log.order;
            if (report.logType == LogType.Error)
            {
                report.fixMethod = rule.fixMethod;
                report.fixNotice = rule.fixNotice;
                report.helpURL = rule.helpURL;
            }
            report.group = group;
        }
        return report;
    }
    static List<Report> ClearObject(string assetPath, AssetClearanceRules rules)
    {
        var reports = new List<Report>();
        var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (assetType == null) return reports;
        var selectConditions = new Dictionary<Rule, List<Condition>>();
        foreach (var rule in rules.ruleList)
        {
            if (!rule.enable) continue;
            foreach (var condition in rule.conditions)
            {
                var methodInfo = condition.ValidateMethod(objectMethods);
                if (methodInfo == null) continue;
                var parameters = methodInfo.GetParameters();
                var parameterType = parameters[0].ParameterType;
                if (parameterType != assetType && !assetType.IsSubclassOf(parameterType)) continue;
                if (!selectConditions.ContainsKey(rule))
                {
                    selectConditions[rule] = new List<Condition>();
                }
                selectConditions[rule].Add(condition);
            }
        }
        if (selectConditions.Count == 0) return reports;
        if (CheckSkipConditions(rules, assetPath, assetType)) return reports;
        var needUnload = AssetClearanceUtil.NeedUnloadAsset(assetType, assetPath);
        var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        foreach (var kv in selectConditions)
        {
            var rule = kv.Key;
            var conditions = kv.Value;
            List<Report> newReports = null;
            CheckConditions(rules, rule, assetPath, asset, conditions, reportGroupIndex++, out newReports);
            reports.AddRange(newReports);
        }
        if (needUnload)
        {
            Resources.UnloadAsset(asset);
        }
        return reports;
    }
    static bool IsInTargetScope(string path, AssetClearanceRules rules)
    {
        var directory = Path.GetDirectoryName(path);
        if (rules.targetScope == TargetScope.DeepInCurrentFolder)
        {
            if (directory.StartsWith(Path.GetDirectoryName(AssetDatabase.GetAssetPath(rules))))
            {
                return true;
            }
        }
        else if (rules.targetScope == TargetScope.CurrentFolder)
        {
            if (directory == Path.GetDirectoryName(AssetDatabase.GetAssetPath(rules)))
            {
                return true;
            }
        }
        foreach (var specificObject in rules.specificObjects)
        {
            var specificPath = AssetDatabase.GetAssetPath(specificObject.obj);
            if (specificObject.obj is DefaultAsset && directory.StartsWith(Path.GetDirectoryName(specificPath + "/")))
            {
                return true;
            }
            if (path == specificPath)
            {
                return true;
            }
        }
        return false;
    }
    static Regex fileIgnore = new Regex(".meta$|.cs$|.lua$|.txt$|.xml$|.json$|\\.svn|\\.git");
    static bool IsFileIgnorable(string path)
    {
        return fileIgnore.IsMatch(path);
    }
    static bool IsTypeIgnorable(Type type)
    {
        return type == null ||
            type == AssetClearanceUtil.DefaultAssetType ||
            type == AssetClearanceUtil.RuleType ||
            type == AssetClearanceUtil.ReportType;
    }
    static List<Report> ClearPath(string path, List<AssetClearanceRules> rulesList, List<AssetClearanceRules> overrideRules)
    {;
        bool isFolder = AssetDatabase.GetMainAssetTypeAtPath(path) == AssetClearanceUtil.DefaultAssetType;
        var files = isFolder ? new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories) : new FileInfo[1] { new FileInfo(path) };
        var reports = new List<Report>();
        var assetPaths = new HashSet<string>();
        int fileCount = 0;
        foreach (var file in files)
        {
            fileCount++;
            if (IsFileIgnorable(file.FullName)) continue;
            var assetPath = file.FullName.Replace("\\", "/").Substring(Application.dataPath.Length - 6);
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (IsTypeIgnorable(assetType)) continue;
            if (fileCount % 5 == 0) EditorUtility.DisplayProgressBar("Searching", assetPath, fileCount / (float)files.Length);
            assetPaths.Add(assetPath);
        }
        EditorUtility.ClearProgressBar();
        var clearObjectRequestsTotal = new List<ClearObjectRequest>();
        foreach (var rules in rulesList)
        {
            if (!rules.enableRules || rules.useForSecondaryCheck) continue;
            var clearObjectRequests = new List<ClearObjectRequest>();
            if (isFolder)
            {
                if (rules.targetScope == TargetScope.CurrentFolder || rules.targetScope == TargetScope.DeepInCurrentFolder)
                {
                    var folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(rules));
                    if (folderPath.StartsWith(Path.GetDirectoryName(path + "/")))
                    {
                        clearObjectRequests.Add(new ClearObjectRequest { assetPath = folderPath });
                    }
                }
                foreach (var specificObject in rules.specificObjects)
                {
                    if (specificObject.obj is DefaultAsset)
                    {
                        var folderPath = AssetDatabase.GetAssetPath(specificObject.obj) + "/";
                        if (folderPath.StartsWith(path + "/"))
                        {
                            clearObjectRequests.Add(new ClearObjectRequest { assetPath = folderPath.Substring(0, folderPath.Length - 1) });
                        }
                    }
                }
            }
            var wildCard = new Wildcard(rules.skipFilesFilter);
            foreach (var assetPath in assetPaths)
            {
                if ((overrideRules == null || (overrideRules != null && !overrideRules.Contains(rules))) && !IsInTargetScope(assetPath, rules)) continue;
                if (WildCardFilter(rules, wildCard, assetPath) || ObjectsFilter(rules, assetPath)) continue;
                clearObjectRequests.Add(new ClearObjectRequest { assetPath = assetPath });
            }
            int assetCount = 0;
            foreach (var clearObjectRequest in clearObjectRequests)
            {
                var assetPath = clearObjectRequest.assetPath;
                assetCount++;
                if (assetCount % 5 == 0) EditorUtility.DisplayProgressBar("Clearing", assetPath, assetCount / (float)assetPaths.Count);
                reports.AddRange(ClearObject(assetPath, rules));
                clearObjectRequestsTotal.Add(clearObjectRequest);
            }
        }
        EditorUtility.ClearProgressBar();
        foreach (var rules in rulesList)
        {
            if (!rules.enableRules || !rules.useForSecondaryCheck) continue;
            int assetCount = 0;
            var wildCard = new Wildcard(rules.skipFilesFilter);
            foreach (var request in clearObjectRequestsTotal)
            {
                var assetPath = request.assetPath;
                assetCount++;
                if ((overrideRules == null || (overrideRules != null && !overrideRules.Contains(rules))) && !IsInTargetScope(assetPath, rules)) continue;
                if (WildCardFilter(rules, wildCard, assetPath) || ObjectsFilter(rules, assetPath)) continue;
                if (assetCount % 5 == 0) EditorUtility.DisplayProgressBar("Clearing", assetPath, assetCount / (float)clearObjectRequestsTotal.Count);
                reports.AddRange(ClearObject(assetPath, rules));
            }
        }
        EditorUtility.ClearProgressBar();
        return reports;
    }
    static List<Report> Clear(TargetObject targetObject, List<AssetClearanceRules> rulesList, List<AssetClearanceRules> customRules)
    {
        var reports = new List<Report>();
        if (targetObject == null || targetObject.obj == null) return reports;
        var assetPath = AssetDatabase.GetAssetPath(targetObject.obj);
        reports.AddRange(ClearPath(assetPath, rulesList, customRules));
        return reports;
    }
    public static List<Report> Clear(List<TargetObject> targetObjects, bool autoSearchRules, bool searchRulesInTargetRange, List<AssetClearanceRules> customRules = null)
    {
        reportGroupIndex = 0;
        var rulesList = autoSearchRules ? AssetClearanceUtil.AutoSearchRules() : new List<AssetClearanceRules>();
        if (customRules != null) rulesList.AddRange(customRules);
        var reports = new List<Report>();
        foreach (var targetObject in targetObjects)
        {
            var targetPath = AssetDatabase.GetAssetPath(targetObject.obj) + "/";
            var targetRulesList = new List<AssetClearanceRules>(rulesList);
            if (autoSearchRules && searchRulesInTargetRange)
            {
                for (int i = 0; i < targetRulesList.Count; ++i)
                {
                    var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(targetRulesList[i]));
                    if (!directory.StartsWith(Path.GetDirectoryName(targetPath)))
                    {
                        targetRulesList.RemoveAt(i--);
                    }
                }
            }
            reports.AddRange(Clear(targetObject, targetRulesList, customRules));
        }
        return reports;
    }
    public static void ClearAndSaveReports()
    {
        var args = Environment.GetCommandLineArgs();
        string reportsPath = null;
        List<TargetObject> targetObjects = new List<TargetObject>();
        AssetClearanceTargets targets = null;
        for (int i = 0; i < args.Length - 1; ++i)
        {
            if (args[i] == "-reportsPath")
            {
                var name = "/AssetReport_" + DateTime.Now.Year;
                name += "_" + DateTime.Now.Month.ToString().PadLeft(2, '0');
                name += "_" + DateTime.Now.Day.ToString().PadLeft(2, '0');
                name += "_" + DateTime.Now.Hour.ToString().PadLeft(2, '0');
                name += "_" + DateTime.Now.Minute.ToString().PadLeft(2, '0');
                reportsPath = args[i + 1] + name + ".asset";
            }
            if (args[i] == "-clearPath")
            {
                var targetObject = new TargetObject { obj = AssetDatabase.LoadMainAssetAtPath(args[i + 1]) };
                targetObjects.Add(targetObject);
            }
            if (args[i] == "-targetsPath")
            {
                targets = AssetDatabase.LoadAssetAtPath<AssetClearanceTargets>(args[i + 1]);
                if (targets == null)
                {
                    Debug.LogError("The targets path is invalid");
                    return;
                }
            }
        }
        if (reportsPath != null)
        {
            if (targets == null)
            {
                if (targetObjects.Count == 0)
                {
                    targetObjects.Add(new TargetObject { obj = AssetDatabase.LoadMainAssetAtPath("Assets") });
                }
                AssetClearanceUtil.SaveReportsToAsset(reportsPath, Clear(targetObjects, true, true, null), "");
            }
            else
            {
                AssetClearanceUtil.SaveReportsToAsset(reportsPath, Clear(targets.targetObjects, targets.autoSearchRules, targets.searchRulesInTargetRange, targets.overrideRules), "");
            }
            Debug.Log($"Reports created at \"{reportsPath}\"");
        }
        else
        {
            Debug.Log("Undefined reports path");
        }
    }
    public static bool Fix(List<Report> reportGroup)
    {
        if (reportGroup == null || reportGroup.Count == 0) return false;
        var fixMethod = reportGroup[0].fixMethod;
        var methodInfo = objectFixMethods.Find((method) => fixMethod.name == method.Name);
        if (methodInfo == null)
        {
            Debug.LogError("The fix method \"" + fixMethod + "\" is invalid");
            return false;
        }
        var assetType = AssetDatabase.GetMainAssetTypeAtPath(reportGroup[0].assetPath);
        if (assetType == null)
        {
            Debug.LogError("The asset path" + reportGroup[0].assetPath + "is invalid");
            return false;
        }
        var parameters = methodInfo.GetParameters();
        if (parameters[0].ParameterType != assetType && !assetType.IsSubclassOf(parameters[0].ParameterType))
        {
            Debug.LogError("The fix method doesn't  match with " + assetType.Name);
            return false;
        }
        var needUnload = AssetClearanceUtil.NeedUnloadAsset(assetType, reportGroup[0].assetPath);
        var asset = AssetDatabase.LoadMainAssetAtPath(reportGroup[0].assetPath);
        var objectValidation = parameters[0].GetCustomAttribute<ObjectValidation>();
        if (objectValidation != null && !objectValidation.DoValidate(asset))
        {
            Debug.LogError("The fix method doesn't  match with " + assetType.Name);
            return false;
        }
        var pingObjects = reportGroup.Select(i => i.pingObject).ToList();
        bool result = DoMethod(asset, fixMethod, methodInfo, parameters, pingObjects);
        if (needUnload)
        {
            Resources.UnloadAsset(asset);
        }
        return result;
    }
}
