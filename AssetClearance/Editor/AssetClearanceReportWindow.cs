using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using Report = AssetClearanceReports.Report;
using Status = AssetClearanceReports.Status;
using LogType = AssetClearanceRules.LogType;
using System;
using System.IO;
using System.Linq;
using SearchMode = AssetClearanceReportTreeView.SearchMode;

public class AssetClearanceReportWindow : EditorWindow
{
    AssetClearanceReports reportsAsset;
    List<Report> reports;
    AssetClearanceReportTreeView reportTreeView;
    string[] rulesOptions;
    string comment = "";
    int selectedRulesIndex;
    bool databaseMode;
    public static void Open(AssetClearanceReports reportsAsset, List<Report> reports)
    {
        if (reportsAsset == null && reports == null) return;
        if (reportsAsset != null) reports = reportsAsset.reports;
        var window = EditorWindow.GetWindow<AssetClearanceReportWindow>(false, "Asset Clearance Report");
        window.reportsAsset = reportsAsset;
        window.reports = reports;
        if (reportsAsset != null) window.comment = reportsAsset.comment;
        var databasePath = "Assets/" + AssetClearanceUtil.GetRootAssetPath() + "/AssetClearanceDatabase.asset";
        var database = AssetDatabase.LoadAssetAtPath<AssetClearanceDatabase>(databasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<AssetClearanceDatabase>();
            AssetDatabase.CreateAsset(database, databasePath);
            AssetDatabase.SaveAssets();
        }
        window.reportTreeView = new AssetClearanceReportTreeView(new TreeViewState(), reportsAsset, reports, database);
        window.minSize = new Vector2(1280, 720);
        window.Show();
        window.rulesOptions = new string[1] { "Total" };
        var hashSet = new HashSet<string>();
        foreach (var report in reports)
        {
            var rulesPath = AssetDatabase.GetAssetPath(report.rules);
            if (!string.IsNullOrEmpty(rulesPath) && !hashSet.Contains(rulesPath))
            {
                hashSet.Add(rulesPath);
                Array.Resize(ref window.rulesOptions, window.rulesOptions.Length + 1);
                window.rulesOptions[window.rulesOptions.Length - 1] = rulesPath;
            }
        }
    }
    public static void Open(AssetClearanceDatabase database)
    {
        if (database == null) return;
        var window = EditorWindow.GetWindow<AssetClearanceReportWindow>(false, "Asset Clearance Report");
        window.databaseMode = true;
        var reports = new List<Report>();
        foreach (var record in database.records)
        {
            var report = new Report();
            report.rules = record.rules;
            report.group = record.group;
            report.ruleName = record.ruleName;
            report.assetPath = record.assetPath;
            report.pingObject = record.pingObject?.Clone();
            report.log = record.log;
            report.logType = record.logType;
            reports.Add(report);
        }
        window.reports = reports;
        window.reportTreeView = new AssetClearanceReportTreeView(new TreeViewState(), null, reports, database);
        window.minSize = new Vector2(1280, 720);
        window.Show();
        window.rulesOptions = new string[1] { "Total" };
    }
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        EditorWindow.GetWindow<AssetClearanceReportWindow>(false, "Asset Clearance Report").Close();
    }
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
    private void OnGUI()
    {
        if (reportTreeView == null)
        {
            Close();
            return;
        }
        var rows = reportTreeView.GetRows();
        var selection = reportTreeView.GetSelection();
        EditorGUI.BeginChangeCheck();
        selectedRulesIndex = EditorGUI.Popup(new Rect(0, 0, position.width, 20), "Select Rules", selectedRulesIndex, rulesOptions);
        if (EditorGUI.EndChangeCheck())
        {
            reportTreeView.selectRulePath = rulesOptions[selectedRulesIndex];
            reportTreeView.Reload();
        }
        EditorGUI.BeginChangeCheck();
        reportTreeView.searchMode = (SearchMode)EditorGUI.EnumPopup(new Rect(50, 20, 80, 20), reportTreeView.searchMode);
        if (EditorGUI.EndChangeCheck())
        {
            reportTreeView.searchString = "";
        }
        reportTreeView.searchString = EditorGUI.TextField(new Rect(0, 20, position.width, 20), "Search", reportTreeView.searchString);
        var toolBarRect = new Rect(0, 40, 100, 24);
        EditorGUI.BeginDisabledGroup(rows.Count == 0);
        if (GUI.Button(Deflate(toolBarRect, 2, 2), "Select All"))
        {
            reportTreeView.SelectAllRows();
        }
        toolBarRect.x += toolBarRect.width;
        if (GUI.Button(Deflate(toolBarRect, 2, 2), "Deselect All"))
        {
            reportTreeView.SetSelection(new List<int>());
            Selection.objects = new UnityEngine.Object[0];
        }
        toolBarRect.x += toolBarRect.width;
        bool canFixSelected = selection.Count != 0;
        if (canFixSelected)
        {
            canFixSelected = false;
            for (int i = 0; i < rows.Count; ++i)
            {
                if (!selection.Contains(rows[i].id)) continue;
                var item = rows[i] as AssetClearanceReportTreeView.Item;
                if (item.report.status == Status.Confirm && item.report.logType == LogType.Error && !string.IsNullOrEmpty(item.report.fixMethod.name))
                {
                    canFixSelected = true;
                    break;
                }
            }
        }
        EditorGUI.BeginDisabledGroup(!canFixSelected);
        if (!databaseMode && GUI.Button(Deflate(toolBarRect, 2, 2), "Fix Selected"))
        {
            for (int i = 0; i < rows.Count; ++i)
            {
                if (!selection.Contains(rows[i].id)) continue;
                var item = rows[i] as AssetClearanceReportTreeView.Item;
                if (!item.report.fixResult && item.report.status == Status.Confirm && item.report.logType == LogType.Error && !string.IsNullOrEmpty(item.report.fixMethod.name))
                {
                    var reportGroup = item.depth == 0 ? reports.FindAll(i => i.status == Status.Confirm && i.group == item.report.group) : new List<Report> { item.report };
                    var fixResult = AssetClearance.Fix(reportGroup);
                    foreach (var report in reportGroup)
                    {
                        report.fixResult = fixResult;
                        if (report.Asset)
                        {
                            EditorUtility.SetDirty(report.Asset);
                        }
                    }
                }
            }
            if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUI.EndDisabledGroup();
        toolBarRect.x += toolBarRect.width;
        toolBarRect.width *= 2;
        EditorGUI.BeginChangeCheck();
        reportTreeView.showTemp = GUI.Toggle(Deflate(toolBarRect, 2, 2), reportTreeView.showTemp, "Show Temp Asset");
        if (EditorGUI.EndChangeCheck())
        {
            reportTreeView.Reload();
        }
        toolBarRect.width /= 2;
        toolBarRect.x = position.width - toolBarRect.width;
        if (!databaseMode && GUI.Button(Deflate(toolBarRect, 2, 2), "Save As..."))
        {
            var name = "AssetReport_" + DateTime.Now.Year;
            name += "_" + DateTime.Now.Month.ToString().PadLeft(2, '0');
            name += "_" + DateTime.Now.Day.ToString().PadLeft(2, '0');
            name += "_" + DateTime.Now.Hour.ToString().PadLeft(2, '0');
            name += "_" + DateTime.Now.Minute.ToString().PadLeft(2, '0');
            var menu = new GenericMenu();
            if (!reportsAsset)
            {
                menu.AddItem(new GUIContent("Reports Asset"), false, () =>
                {
                    var path = EditorUtility.SaveFilePanelInProject("Save As...", name, "asset", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        reportsAsset = AssetClearanceUtil.SaveReportsToAsset(path, reports, comment);
                        reports = reportsAsset.reports;
                        reportTreeView.UpdateReportsSource(reportsAsset);
                        AssetDatabase.Refresh();
                    }
                });
            }
            menu.AddItem(new GUIContent("CSV File"), false, () =>
            {
                var path = EditorUtility.SaveFilePanel("Save As...", Directory.GetCurrentDirectory(), name, "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetClearanceUtil.SaveReportsToCSV(path, reports);
                    AssetDatabase.Refresh();
                }
            });
            menu.ShowAsContext();
        }
        var y = 40 + toolBarRect.height;
        reportTreeView.OnGUI(new Rect(0, y, position.width, position.height - 170 - y));
        if (!databaseMode)
        {
            y = position.height - 170;
            GUI.BeginGroup(new Rect(0, y, position.width, 170));
            EditorGUI.LabelField(new Rect(0, 0, position.width, 20), "Comment");
            if (!reportsAsset) comment = EditorGUI.TextArea(new Rect(0, 20, position.width, 130), comment);
            else
            {
                EditorGUI.BeginChangeCheck();
                reportsAsset.comment = EditorGUI.TextArea(new Rect(0, 20, position.width, 130), reportsAsset.comment);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(reportsAsset);
                }
            }
            GUI.EndGroup();
        }
        EditorGUI.LabelField(new Rect(0, position.height - 20, position.width, 20), $"{rows.Count} Total, {selection.Count} Selected");
    }
}
