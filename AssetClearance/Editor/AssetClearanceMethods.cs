using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Priority = AssetClearanceReports.Priority;

public static partial class AssetClearanceMethods
{
    public class Log
    {
        public string content;
        public int order;
    }
    public static AssetClearanceRules rules;
    static List<Log> logList = new List<Log>();
    public static int LogCount => logList.Count;
    public static Log GetLog(int i) => i < LogCount ? logList[i] : null;
    public static void ClearLogs() => logList.Clear();
    static List<GameObject> pingReferencerList = new List<GameObject>();
    static List<UnityEngine.Object> pingObjectList = new List<UnityEngine.Object>();
    public static int PingObjectListCount => pingObjectList.Count;
    public static GameObject GetPingReferencer(int i) => i < PingObjectListCount ? pingReferencerList[i] : null;
    public static UnityEngine.Object GetPingObject(int i) => i < PingObjectListCount ? pingObjectList[i] : null;
    public static void ClearPingObjects()
    {
        pingReferencerList.Clear();
        pingObjectList.Clear();
    }
    public static void AddLog(string log, int order = 0, GameObject pingReferencer = null, UnityEngine.Object pingObject = null)
    {
        logList.Add(new Log { content = log, order = order });
        pingReferencerList.Add(pingReferencer);
        pingObjectList.Add(pingObject);
    }
    public enum NameCompareMode
    {
        Match,
        Contain,
        StartWith,
        EndWith
    }
    [AssetClearanceMethod("Object", "±È½ÏÃû×Ö")]
    public static bool CompareName(UnityEngine.Object obj, string name, NameCompareMode mode, bool toLower)
    {
        name = toLower ? name.ToLower() : name;
        var testName = toLower ? obj.name.ToLower() : obj.name;
        switch (mode)
        {
            case NameCompareMode.Contain:
                return testName.Contains(name);
            case NameCompareMode.StartWith:
                return testName.StartsWith(name);
            case NameCompareMode.EndWith:
                return testName.EndsWith(name);
            default:
                return testName.Equals(name);
        }
    }
}