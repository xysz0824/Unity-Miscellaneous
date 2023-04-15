using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu(fileName = "New Asset Clearance Rules", menuName = "Asset Clearance Rules", order = 100)]
public class AssetClearanceRules : ScriptableObject
{
    [Serializable]
    public class SpecificObject
    {
        public UnityEngine.Object obj;
    }
    public enum LogicOperator
    {
        AND,
        OR
    }
    [Serializable]
    public class Method
    {
        public string name;
        public int[] intParamCounts;
        public int[] intParams;
        public int[] floatParamCounts;
        public float[] floatParams;
        public int[] boolParamCounts;
        public bool[] boolParams;
        public int[] stringParamCounts;
        public string[] stringParams;
        public int[] objectParamCounts;
        public UnityEngine.Object[] objectParams;
    }
    [Serializable]
    public class Condition
    {
        public int priority;
        public Method method;
        public bool negation;
        public LogicOperator logicOperator;
        [NonSerialized]
        private MethodInfo _methodInfo;
        public MethodInfo ValidateMethod(List<MethodInfo> methods)
        {
            if (_methodInfo == null || _methodInfo.Name != method.name)
            {
                _methodInfo = methods.Find((info) => info.Name == method.name);
            }
            return _methodInfo;
        }
    }
    public enum TargetScope
    {
        DeepInCurrentFolder,
        CurrentFolder,
        SpecificObjects
    }
    public enum LogType
    {
        None,
        Info,
        Warning,
        Error
    }
    [Serializable]
    public class Rule
    {
        public AssetClearanceRuleTemplate template;
        public bool enable;
        public string name;
        public List<Condition> conditions;
        public LogType trueLogType = LogType.None;
        public LogType falseLogType = LogType.Warning;
        public string helpURL;
        public Method fixMethod = new Method();
        public string fixNotice;
    }
    public bool enableRules = true;
    public bool useForSecondaryCheck;
    public TargetScope targetScope;
    public List<SpecificObject> specificObjects = new List<SpecificObject>();
    public string[] defaultIncludePaths = new string[0];
    public string[] defaultExcludePaths = new string[0];
    public bool enableSkipConditions = true;
    public string skipFilesFilter = "";
    public List<Condition> skipConditions = new List<Condition>();
    public List<SpecificObject> skipObjects = new List<SpecificObject>();
    public List<Rule> ruleList = new List<Rule>();
}
