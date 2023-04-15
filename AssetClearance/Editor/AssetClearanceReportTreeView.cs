using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using Report = AssetClearanceReports.Report;
using Status = AssetClearanceReports.Status;
using PingObject = AssetClearanceReports.PingObject;
using LogType = AssetClearanceRules.LogType;
using Priority = AssetClearanceReports.Priority;
using UnityEditor.SceneManagement;
#if UNITY_2021_1_OR_NEWER
#else
using UnityEditor.Experimental.SceneManagement;
#endif

public class AssetClearanceReportTreeView : TreeView
{
    readonly Texture2D HelpIcon = EditorGUIUtility.FindTexture("_Help");
    readonly Dictionary<LogType, Texture2D> LogTypeIconDict = new Dictionary<LogType, Texture2D>
    {
        { LogType.Info, EditorGUIUtility.FindTexture("console.infoicon.sml") },
        { LogType.Warning, EditorGUIUtility.FindTexture("console.warnicon.sml") },
        { LogType.Error, EditorGUIUtility.FindTexture("console.erroricon.sml") },
    };
    readonly Dictionary<Status, Texture2D> StatusIconDict = new Dictionary<Status, Texture2D>
    {
        { Status.Confirm, EditorGUIUtility.FindTexture("TestNormal") },
        { Status.Fixing, EditorGUIUtility.FindTexture("TestStopwatch") },
        { Status.Ignore, EditorGUIUtility.FindTexture("TestIgnored") },
    };
    readonly Texture2D FixedIcon = EditorGUIUtility.FindTexture("TestPassed");
    public class Item : TreeViewItem
    {
        public Report report;
    }
    public enum SearchMode
    {
        Asset,
        Rule,
        PingObject,
        Log
    }

    AssetClearanceReports reportsAsset;
    List<Report> reports;
    Dictionary<string, TreeViewItem> rootDict = new Dictionary<string, TreeViewItem>();
    Dictionary<string, TreeViewItem> tempAssetsRootDict = new Dictionary<string, TreeViewItem>();
    AssetClearanceDatabase database;
    public string selectRulePath;
    public SearchMode searchMode;
    public bool showTemp;

    static MultiColumnHeader GetMultiColumnHeader()
    {
        var columns = new[]
        {
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Status"),
                width = 48,
                minWidth = 48,
                maxWidth = 48,
                autoResize = false,
                allowToggleVisibility = false,
                sortedAscending = true,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Rule (双击跳转至规则）"),
                width = 200,
                minWidth = 200,
                maxWidth = int.MaxValue,
                autoResize = true,
                allowToggleVisibility = false,
                sortedAscending = true,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Asset"),
                width = 500,
                minWidth = 500,
                maxWidth = int.MaxValue,
                autoResize = true,
                allowToggleVisibility = false,
                sortedAscending = true,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Ping Object (双击跳转至目标，再单击选中资源)"),
                width = 500,
                minWidth = 500,
                maxWidth = int.MaxValue,
                autoResize = true,
                allowToggleVisibility = true,
                sortedAscending = true,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Log"),
                width = 300,
                minWidth = 300,
                maxWidth = int.MaxValue,
                autoResize = true,
                allowToggleVisibility = false,
                sortedAscending = true,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Fix"),
                width = 58,
                minWidth = 58,
                maxWidth = 58,
                autoResize = true,
                allowToggleVisibility = false,
                canSort = false,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Help"),
                width = 48,
                minWidth = 48,
                maxWidth = 48,
                autoResize = true,
                allowToggleVisibility = false,
                canSort = false
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Priority"),
                width = 68,
                minWidth = 68,
                maxWidth = 68,
                autoResize = true,
                allowToggleVisibility = false,
                sortedAscending = true,
            },
            new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Note"),
                width = 300,
                minWidth = 100,
                maxWidth = int.MaxValue,
                autoResize = true,
                allowToggleVisibility = false,
                canSort = false
            }
        };
        var state = new MultiColumnHeaderState(columns);
        return new MultiColumnHeader(state);
    }
    public AssetClearanceReportTreeView(TreeViewState treeViewState, AssetClearanceReports reportsAsset, List<Report> reports, AssetClearanceDatabase database)
        : base(treeViewState, GetMultiColumnHeader())
    {
        this.reportsAsset = reportsAsset;
        this.reports = reportsAsset ? reportsAsset.reports : reports;
        this.database = database;
        columnIndexForTreeFoldouts = 2;
        rowHeight = 20; 
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        multiColumnHeader.sortingChanged += OnSortingChanged;
        Reload();
    }
    public void UpdateReportsSource(AssetClearanceReports asset)
    {
        reportsAsset = asset;
        reports = asset.reports;
    }
    protected override TreeViewItem BuildRoot()
    {
        if (!rootDict.ContainsKey("Total"))
        {
            rootDict["Total"] = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
            tempAssetsRootDict["Total"] = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
            var query = Order(reports, c => c.group, true);
            float count = query.Count();
            var index = 1;
            Item subRoot = null;
            foreach (var report in query)
            {
                var rulesPath = AssetDatabase.GetAssetPath(report.rules);
                if (!rootDict.ContainsKey(rulesPath))
                {
                    rootDict[rulesPath] = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
                }
                if (!tempAssetsRootDict.ContainsKey(rulesPath))
                {
                    tempAssetsRootDict[rulesPath] = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
                }
                if (report == query.First() || report.group != subRoot.report.group)
                {
                    subRoot = new Item { id = index++, report = report, children = new List<TreeViewItem>() };
                    if (!string.IsNullOrEmpty(subRoot.report.log))
                    {
                        database.Sync(subRoot.report);
                        if (report.assetPath.Contains("_Temp."))
                        {
                            tempAssetsRootDict["Total"].AddChild(subRoot);
                            tempAssetsRootDict[rulesPath].AddChild(subRoot);
                        }
                        else
                        {
                            rootDict["Total"].AddChild(subRoot);
                            rootDict[rulesPath].AddChild(subRoot);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(report.log))
                {
                    database.Sync(report);
                    subRoot.AddChild(new Item { id = index++, report = report, children = new List<TreeViewItem>() });
                }
                if (index % 100 == 0) EditorUtility.DisplayProgressBar("Loading", $"{index}/{count}", index / count);
            }
            EditorUtility.ClearProgressBar();
        }
        var dict = !showTemp ? rootDict : tempAssetsRootDict;
        var key = string.IsNullOrEmpty(selectRulePath) ? "Total" : selectRulePath;
        SetupDepthsFromParentsAndChildren(dict[key]);
        return dict[key];
    }
    protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
    {
        var rows = base.BuildRows(root);
        SortIfNeed(rows);
        return rows;
    }
    void OnSortingChanged(MultiColumnHeader multiColumnHeader)
    {
        SortIfNeed(GetRows());
    }

    public static IOrderedEnumerable<T> Order<T, TKey>(IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
    {
        return ascending ? source.OrderBy(selector) : source.OrderByDescending(selector);
    }

    public static IOrderedEnumerable<T> ThenBy<T, TKey>(IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
    {
        return ascending ? source.ThenBy(selector) : source.ThenByDescending(selector);
    }
    void TreeToList(IList<TreeViewItem> source, IList<TreeViewItem> result)
    {
        if (source == null)
            throw new NullReferenceException("source");
        if (result == null)
            throw new NullReferenceException("result");

        result.Clear();

        Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
        for (int i = source.Count - 1; i >= 0; i--)
            stack.Push(source[i]);

        while (stack.Count > 0)
        {
            TreeViewItem current = stack.Pop();
            result.Add(current);

            if (current.hasChildren && IsExpanded(current.id))
            {
                for (int i = current.children.Count - 1; i >= 0; i--)
                {
                    stack.Push(current.children[i]);
                }
            }
        }
    }
    static int GetStringOrder(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;
        int order = 0;
        for (int i = 0; i < str.Length; ++i)
        {
            order += str[i];
        }
        return order;
    }
    void SortIfNeed(IList<TreeViewItem> rows)
    {
        if (rows.Count <= 1) return;
        if (multiColumnHeader.sortedColumnIndex == -1) return;
        var sortedColumns = multiColumnHeader.state.sortedColumns;
        if (sortedColumns.Length == 0) return;
        var myTypes = rows.Cast<Item>();
        var orderedQuery = InitialOrder(myTypes, sortedColumns);
        for (int i = 1; i < sortedColumns.Length; i++)
        {
            bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);
            switch (sortedColumns[i])
            {
                case 0: //Status
                    orderedQuery = ThenBy(orderedQuery, l => (int)l.report.status + (l.report.fixResult ? 1 : 0), ascending);
                    break;
                case 1: //Rule
                    orderedQuery = ThenBy(orderedQuery, l => l.report.ruleName, ascending);
                    break;
                case 2: //Asset
                    orderedQuery = ThenBy(orderedQuery, l => l.report.assetPath, ascending);
                    break;
                case 3: //PingObject
                    orderedQuery = ThenBy(orderedQuery, l => AssetClearanceUtil.GetPingObjectDisplayName(l.report.assetPath, l.report.pingObject), ascending);
                    break;
                case 4: //Log
                    orderedQuery = ThenBy(orderedQuery, l => l.report.logOrder == 0 ? GetStringOrder(l.report.log) : l.report.logOrder, ascending);
                    break;
                case 7: //Priority
                    orderedQuery = ThenBy(orderedQuery, l => l.report.priority, ascending);
                    break;
            }
        }
        var orderedItem = orderedQuery.Cast<TreeViewItem>().ToList();
        TreeToList(orderedItem, rows);
        Repaint();
    }
    IOrderedEnumerable<Item> InitialOrder(IEnumerable<Item> myTypes, int[] history)
    {
        bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
        switch (history[0])
        {
            case 0: //Status
                return Order(myTypes, l => (int)l.report.status + (l.report.fixResult ? 1 : 0), ascending);
            case 1: //Rule
                return Order(myTypes, l => l.report.ruleName, ascending);
            case 2: //Asset
                return Order(myTypes, l => l.report.assetPath, ascending);
            case 3: //PingObject
                return Order(myTypes, l => AssetClearanceUtil.GetPingObjectDisplayName(l.report.assetPath, l.report.pingObject), ascending);
            case 4: //Log
                return Order(myTypes, l => l.report.logOrder == 0 ? GetStringOrder(l.report.log) : l.report.logOrder, ascending);
            case 7: //Priority
                return Order(myTypes, l => l.report.priority, ascending);
            default:
                Assert.IsTrue(false, "Unhandled enum");
                break;
        }
        return Order(myTypes, l => l.report.status, ascending);
    }
    void PingAsset(UnityEngine.Object asset)
    {
        EditorGUIUtility.PingObject(asset);
        Selection.objects = new UnityEngine.Object[1] { asset };
    }
    void PingObject(UnityEngine.Object rootAsset,string rootAssetPath, PingObject pingObject)
    {
        if (rootAsset == null || pingObject == null) return;
        if (rootAsset is GameObject && !PrefabUtility.IsPartOfModelPrefab(rootAsset))
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != rootAssetPath) return;
            var obj = (rootAsset as GameObject).transform.Find(pingObject.referencerPath);
            if (obj != null)
            {
                long subID = 0;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj.gameObject, out _, out subID);
                if (subID != pingObject.referencerID)
                {
                    var parent = obj.gameObject.transform.parent;
                    for (int i = 0; i < parent.childCount; ++i)
                    {
                        var child = parent.GetChild(i);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(child.gameObject, out _, out subID);
                        if (subID == pingObject.referencerID)
                        {
                            obj = child;
                            break;
                        }
                    }
                }
                var objects = AssetClearanceUtil.SelectGameObjectsInPrefabStage((rootAsset as GameObject), stage.prefabContentsRoot, new List<UnityEngine.Object> { obj.gameObject }).ToArray();
                Selection.objects = objects;
                EditorGUIUtility.PingObject(objects[0]);
            }
        }
        else if (rootAsset is SceneAsset)
        {
            if (EditorSceneManager.GetActiveScene().path != rootAssetPath) return;
            //TODO
        }
    }
    void OpenAsset(UnityEngine.Object asset)
    {
        if (asset is SceneAsset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        else
        {
            AssetDatabase.OpenAsset(asset);
        }
    }
    protected override void SelectionChanged(IList<int> selectedIds)
    {
        base.SelectionChanged(selectedIds);
        var rows = FindRows(selectedIds);
        Selection.objects = rows.Select(i => (i as Item).report.Asset).ToArray();
        for (int i = 0; i < rows.Count; ++i)
        {
            var report = (rows[i] as Item).report;
            if (i == rows.Count - 1)
            {
                EditorGUIUtility.PingObject(report.Asset);
                PingObject(report.Asset, report.assetPath, report.pingObject);
            }
        }
    }
    protected override void RowGUI(RowGUIArgs args)
    {
        var item = args.item as Item;
        for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
        {
            var column = args.GetColumn(i);
            var cellRect = args.GetCellRect(i);
            var containsMouse = cellRect.Contains(Event.current.mousePosition);
            if (containsMouse && Event.current.clickCount == 1 && Event.current.button == 1)
            {
                var menu = new GenericMenu();
                if (item.report.fixResult)
                {
                    menu.AddDisabledItem(new GUIContent("Set Fixing"));
                    menu.AddDisabledItem(new GUIContent("Set Fixed"));
                    menu.AddDisabledItem(new GUIContent("Set Ignore"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Set Fixing"), false, () =>
                    {
                        var rows = FindRows(GetSelection());
                        if (rows.Count == 0) rows = new List<TreeViewItem> { item };
                        foreach (Item row in rows)
                        {
                            if (row.report.fixResult) continue;
                            if (row.depth == 0)
                            {
                                foreach (var report in reports)
                                {
                                    if (report.group == row.report.group)
                                    {
                                        report.status = Status.Fixing;
                                        database.Insert(report);
                                    }
                                }
                            }
                            row.report.status = Status.Fixing;
                            database.Insert(row.report);
                        }
                        if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                        EditorUtility.SetDirty(database);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    });
                    menu.AddItem(new GUIContent("Set Fixed"), false, () =>
                    {
                        var rows = FindRows(GetSelection());
                        if (rows.Count == 0) rows = new List<TreeViewItem> { item };
                        foreach (Item row in rows)
                        {
                            if (row.report.fixResult) continue;
                            if (row.depth == 0)
                            {
                                foreach (var report in reports)
                                {
                                    if (report.group == row.report.group)
                                    {
                                        report.fixResult = true;
                                        database.Remove(report);
                                    }
                                }
                            }
                            row.report.fixResult = true;
                            database.Remove(row.report);
                        }
                        if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    });
                    menu.AddItem(new GUIContent("Set Ignore"), false, () =>
                    {
                        var rows = FindRows(GetSelection());
                        if (rows.Count == 0) rows = new List<TreeViewItem> { item };
                        foreach (Item row in rows)
                        {
                            if (row.report.fixResult) continue;
                            if (row.depth == 0)
                            {
                                foreach (var report in reports)
                                {
                                    if (report.group == row.report.group)
                                    {
                                        report.status = Status.Ignore;
                                        database.Insert(report);
                                    }
                                }
                            }
                            row.report.status = Status.Ignore;
                            database.Insert(row.report);
                        }
                        if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                        EditorUtility.SetDirty(database);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    });
                }
                menu.AddItem(new GUIContent("Reset"), false, () =>
                {
                    var rows = FindRows(GetSelection());
                    if (rows.Count == 0) rows = new List<TreeViewItem> { item };
                    foreach (Item row in rows)
                    {
                        if (row.depth == 0)
                        {
                            foreach (var report in reports)
                            {
                                if (report.group == row.report.group)
                                {
                                    report.fixResult = false;
                                    report.status = Status.Confirm;
                                    report.priority = Priority.High;
                                    database.Remove(report);
                                }
                            }
                        }
                        row.report.fixResult = false;
                        row.report.status = Status.Confirm;
                        row.report.priority = Priority.High;
                        database.Remove(row.report);
                    }
                    if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                });
                menu.ShowAsContext();
            }
            switch (column)
            {
                case 0: //Status
                    cellRect.x += 2;
                    cellRect.y += 2;
                    cellRect.width = cellRect.height = 16;
                    GUI.DrawTexture(cellRect, item.report.fixResult ? FixedIcon : StatusIconDict[item.report.status]);
                    break;
                case 1: //Rule
                    if (item.report.status == Status.Ignore) GUI.enabled = false;
                    EditorGUI.LabelField(cellRect, item.report.ruleName);
                    if (item.report.status == Status.Ignore) GUI.enabled = true;
                    if (Event.current.type == EventType.MouseDown && containsMouse && Event.current.isMouse)
                    {
                        if (Event.current.clickCount > 1)
                        {
                            OpenAsset(item.report.rules);
                        }
                    }
                    containsMouse = cellRect.Contains(Event.current.mousePosition);
                    if (containsMouse && Event.current.clickCount == 1 && Event.current.button == 1)
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Show This Only"), false, () =>
                        {
                            searchMode = SearchMode.Rule;
                            searchString = item.report.ruleName;
                        });
                        menu.ShowAsContext();
                    }
                    break;
                case 2: //Asset
                    cellRect = AssetClearanceRulesEditor.Indent(cellRect, GetContentIndent(item));
                    containsMouse = cellRect.Contains(Event.current.mousePosition);
                    if (Event.current.type == EventType.MouseDown && containsMouse && Event.current.isMouse && Event.current.clickCount == 1)
                    {
                        PingAsset(item.report.Asset);
                    }
                    var icon = AssetDatabase.GetCachedIcon(item.report.assetPath);
                    if (item.report.status == Status.Ignore) GUI.enabled = false;
                    EditorGUI.LabelField(cellRect, new GUIContent { image = icon, text = item.report.assetPath });
                    if (item.report.status == Status.Ignore) GUI.enabled = true;
                    break;
                case 3: //Ping Object
                    var name = AssetClearanceUtil.GetPingObjectDisplayName(item.report.assetPath, item.report.pingObject);
                    if (Event.current.type == EventType.MouseDown && containsMouse && Event.current.isMouse)
                    {
                        if (Event.current.clickCount == 1)
                        {
                            if (AssetDatabase.GetMainAssetTypeAtPath(name) == null && (item.report.pingObject == null || !item.report.pingObject.subAsset)) PingAsset(item.report.Asset);
                            else PingAsset(item.report.pingObject.Reference);
                        }
                        else
                        {
                            OpenAsset(item.report.Asset);
                            PingObject(item.report.Asset, item.report.assetPath, item.report.pingObject);
                        }
                    }
                    icon = EditorGUIUtility.ObjectContent(item.report.pingObject?.Reference, null).image;
                    if (item.report.status == Status.Ignore) GUI.enabled = false;
                    EditorGUI.LabelField(cellRect, new GUIContent { image = icon, text = name });
                    if (item.report.status == Status.Ignore) GUI.enabled = true;
                    break;
                case 4: //Log
                    if (item.report.status == Status.Ignore) GUI.enabled = false;
                    EditorGUI.LabelField(cellRect, new GUIContent { image = LogTypeIconDict[item.report.logType], text = item.report.log });
                    if (item.report.status == Status.Ignore) GUI.enabled = true;
                    break;
                case 5: //Fix
                    if (!item.report.fixResult && item.report.status == Status.Confirm && item.report.logType == LogType.Error)
                    {
                        var hasFixMethod = !string.IsNullOrEmpty(item.report.fixMethod.name);
                        var hasFixNotice = !string.IsNullOrEmpty(item.report.fixNotice);
                        if (hasFixMethod && GUI.Button(cellRect, "Fix"))
                        {
                            if (hasFixNotice)
                            {
                                EditorUtility.DisplayDialog("Fix Notice", item.report.fixNotice, "OK");
                            }
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
                            if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                        else if (!hasFixMethod && hasFixNotice && GUI.Button(cellRect, "Notice"))
                        {
                            EditorUtility.DisplayDialog("Fix Notice", item.report.fixNotice, "OK");
                        }
                    }
                    else if (item.report.status == Status.Fixing) EditorGUI.LabelField(cellRect, "修复中");
                    break;
                case 6: //Help
                    if (item.report.logType == LogType.Error && !string.IsNullOrEmpty(item.report.helpURL) && GUI.Button(cellRect, new GUIContent { image = HelpIcon }))
                    {
                        Application.OpenURL(item.report.helpURL);
                    }
                    break;
                case 7: //Priority
                    if (item.report.status == Status.Fixing)
                    {
                        EditorGUI.BeginChangeCheck();
                        item.report.priority = (AssetClearanceReports.Priority)EditorGUI.EnumPopup(cellRect, item.report.priority);
                        if (EditorGUI.EndChangeCheck())
                        {
                            var rows = FindRows(GetSelection());
                            if (rows.Count == 0) rows = new List<TreeViewItem> { item };
                            foreach (Item row in rows)
                            {
                                if (row.depth == 0)
                                {
                                    foreach (var report in reports)
                                    {
                                        if (report.group == row.report.group)
                                        {
                                            report.priority = item.report.priority;
                                            database.Insert(report);
                                        }
                                    }
                                }
                                row.report.priority = item.report.priority;
                                database.Insert(row.report);
                            }
                            if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                            EditorUtility.SetDirty(database);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                    }
                    break;
                case 8: //Note
                    if (item.report.status == Status.Fixing || item.report.status == Status.Ignore)
                    {
                        EditorGUI.BeginChangeCheck();
                        item.report.note = EditorGUI.DelayedTextField(cellRect, item.report.note);
                        if (EditorGUI.EndChangeCheck())
                        {
                            database.Insert(item.report);
                            if (reportsAsset) EditorUtility.SetDirty(reportsAsset);
                            EditorUtility.SetDirty(database);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                    }
                    break;
            }
        }
    }

    protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
    {
        switch (searchMode)
        {
            case SearchMode.Rule:
                return (item as Item).report.ruleName.ToLower().Contains(search.ToLower());
            case SearchMode.PingObject:
                var sItem = (item as Item);
                var display = AssetClearanceUtil.GetPingObjectDisplayName(sItem.report.assetPath, sItem.report.pingObject);
                return display.ToLower().Contains(search.ToLower());
            case SearchMode.Log:
                return (item as Item).report.log.ToLower().Contains(search.ToLower());
            case SearchMode.Asset:
            default:
                return (item as Item).report.assetPath.ToLower().Contains(search.ToLower());
        }
    }
}