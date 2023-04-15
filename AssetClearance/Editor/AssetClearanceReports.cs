using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Method = AssetClearanceRules.Method;
using LogType = AssetClearanceRules.LogType;
using UnityEditor.Callbacks;

public class AssetClearanceReports : ScriptableObject
{
    [Serializable]
    public class PingObject
    {
        public string assetPath = "";
        public bool subAsset;
        public long subAssetID;
        public string referencerPath = "";
        public long referencerID;
        [NonSerialized]
        UnityEngine.Object _reference;
        public UnityEngine.Object Reference
        {
            get
            {
                if (_reference != null) return _reference;
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (assetType != null)
                {
                    if (!subAsset)
                    {
                        _reference = AssetDatabase.LoadMainAssetAtPath(assetPath);
                        if (assetType == typeof(GameObject) && referencerPath != null)
                        {
                            _reference = (_reference as GameObject).transform.Find(referencerPath).gameObject;
                            long subID = 0;
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_reference, out _, out subID);
                            if (subID != referencerID)
                            {
                                var parent = (_reference as GameObject).transform.parent;
                                for (int i = 0; i < parent.childCount; ++i)
                                {
                                    var child = parent.GetChild(i);
                                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(child.gameObject, out _, out subID);
                                    if (subID == referencerID)
                                    {
                                        _reference = child.gameObject;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                        foreach (var subAsset in subAssets)
                        {
                            long subID = 0;
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(subAsset, out _, out subID);
                            if (subID == subAssetID)
                            {
                                _reference = subAsset;
                            }
                        }
                    }
                }
                return _reference;
            }
        }
        private PingObject() { }
        public PingObject(UnityEngine.Object root, GameObject referencer, UnityEngine.Object obj = null)
        {
            if (root is GameObject && !PrefabUtility.IsPartOfModelPrefab(root))
            {
                AssetClearanceUtil.GetGameObjectRelativePath(root as GameObject, referencer, ref referencerPath);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(referencer, out _, out referencerID);
            }
            else if (root is SceneAsset)
            {
                //TODO
            }
            if (obj == null) obj = referencer;
            _reference = obj;
            this.assetPath = AssetDatabase.GetAssetPath(obj);
            this.subAsset = AssetDatabase.IsSubAsset(obj);
            if (this.subAsset)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out _, out subAssetID);
            }
        }
        public bool ContentEquals(PingObject pingObject)
        {
            if (pingObject == null) return NullEquals(pingObject);
            return assetPath == pingObject.assetPath &&
                subAsset == pingObject.subAsset &&
                subAssetID == pingObject.subAssetID &&
                referencerPath == pingObject.referencerPath &&
                referencerID == pingObject.referencerID;
        }
        public bool NullEquals(PingObject pingObject)
        {
            if (pingObject != null) return ContentEquals(pingObject);
            return string.IsNullOrEmpty(assetPath) &&
                subAsset == false &&
                subAssetID == 0 &&
                string.IsNullOrEmpty(referencerPath) &&
                referencerID == 0;
        }
        public PingObject Clone()
        {
            var newPingObject = new PingObject();
            newPingObject.assetPath = assetPath;
            newPingObject.subAsset = subAsset;
            newPingObject.subAssetID = subAssetID;
            newPingObject.referencerPath = referencerPath;
            newPingObject.referencerID = referencerID;
            return newPingObject;
        }
    }
    public enum Priority
    {
        High,
        Middle,
        Low,
    }
    public enum Status
    {
        Confirm,
        Fixing,
        Ignore
    }
    [Serializable]
    public class Report
    {
        public AssetClearanceRules rules;
        public string ruleName = "";
        public string assetPath = "";
        public PingObject pingObject;
        public string log = "";
        public LogType logType;
        public int logOrder;
        public Method fixMethod;
        public string fixNotice = "";
        public string helpURL = "";
        [NonSerialized]
        public Status status;
        public bool fixResult;
        [NonSerialized]
        public Priority priority;
        [NonSerialized]
        public string note = "";
        public int group;
        [NonSerialized]
        UnityEngine.Object _asset;
        public UnityEngine.Object Asset
        {
            get
            {
                if (_asset != null) return _asset;
                if (string.IsNullOrEmpty(assetPath)) return null;
                _asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                return _asset;
            }
        }
    }
    public List<Report> reports = new List<Report>();
    public string comment = "";
    [OnOpenAsset(1)]
    public static bool OpenReportWindow(int instanceID, int line)
    {
        if (AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(instanceID)) != typeof(AssetClearanceReports))
        {
            return false;
        }
        var reports = AssetDatabase.LoadAssetAtPath<AssetClearanceReports>(AssetDatabase.GetAssetPath(instanceID));
        AssetClearanceReportWindow.Open(reports, null);
        bool windowIsOpen = EditorWindow.HasOpenInstances<AssetClearanceReportWindow>();
        if (!windowIsOpen)
        {
            EditorWindow.CreateWindow<AssetClearanceReportWindow>();
        }
        else
        {
            EditorWindow.FocusWindowIfItsOpen<AssetClearanceReportWindow>();
        }
        return false;
    }
}

[CustomEditor(typeof(AssetClearanceReports))]
public class AssetClearanceReportsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        //Keep it empty
    }
}