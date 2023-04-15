using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PingObject = AssetClearanceReports.PingObject;
using Status = AssetClearanceReports.Status;
using Priority = AssetClearanceReports.Priority;
using Report = AssetClearanceReports.Report;
using LogType = AssetClearanceRules.LogType;
using UnityEditor.Callbacks;

public class AssetClearanceDatabase : ScriptableObject
{
    [Serializable]
    public class Record
    {
        public AssetClearanceRules rules;
        public string ruleName = "";
        public string assetPath = "";
        public PingObject pingObject;
        public string log = "";
        public LogType logType;
        public Status status;
        public Priority priority;
        public string note = "";
        public int group;
        public bool KeyEquals(Report report)
        {
            if (report == null) return false;
            return report.rules == rules &&
                report.ruleName == ruleName &&
                report.assetPath == assetPath &&
                (report.pingObject == pingObject || (report.pingObject != null && report.pingObject.ContentEquals(pingObject)) || (pingObject != null && pingObject.ContentEquals(report.pingObject))) &&
                report.log == log;
        }
    }
    public List<Record> records = new List<Record>();
    [OnOpenAsset(1)]
    public static bool OpenDatabaseWindow(int instanceID, int line)
    {
        if (AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(instanceID)) != typeof(AssetClearanceDatabase))
        {
            return false;
        }
        var database = AssetDatabase.LoadAssetAtPath<AssetClearanceDatabase>(AssetDatabase.GetAssetPath(instanceID));
        AssetClearanceReportWindow.Open(database);
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
    public bool Sync(Report report)
    {
        foreach (var record in records)
        {
            if (record.KeyEquals(report))
            {
                report.status = record.status;
                report.priority = record.priority;
                report.note = record.note;
                return true;
            }
        }
        report.status = Status.Confirm;
        report.priority = Priority.High;
        report.note = "";
        return false;
    }
    public void Insert(Report report)
    {
        foreach (var record in records)
        {
            if (record.KeyEquals(report))
            {
                record.status = report.status;
                record.priority = report.priority;
                record.note = report.note;
                return;
            }
        }
        var newRecord = new Record();
        newRecord.rules = report.rules;
        newRecord.group = report.group;
        newRecord.ruleName = report.ruleName;
        newRecord.assetPath = report.assetPath;
        newRecord.pingObject = report.pingObject?.Clone();
        newRecord.log = report.log;
        newRecord.logType = report.logType;
        newRecord.status = report.status;
        newRecord.priority = report.priority;
        newRecord.note = report.note;
        records.Add(newRecord);
    }
    public void Remove(Report report)
    {
        for (int i = 0; i < records.Count; ++i)
        {
            if (records[i].KeyEquals(report))
            {
                records.RemoveAt(i);
                i--;
            }
        }
    }
}
