using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.IO;

public class AssetClearanceMethodTreeView : TreeView
{
	public class Item : TreeViewItem
    {
		public MethodInfo method;
    }
	List<MethodInfo> methods;
	TreeViewItem lastSelectedItem;
	public TreeViewItem LastSelectedItem => lastSelectedItem;
	int lineCount;
	public int LineCount => lineCount;
	int lastSelectedLine;
	public int LastSelectedLine => lastSelectedLine;
	public float RowHeight => rowHeight;

	public AssetClearanceMethodTreeView(TreeViewState treeViewState, List<MethodInfo> methods)
		: base(treeViewState)
	{
		this.methods = methods;
		Reload();
	}
	protected override TreeViewItem BuildRoot()
	{
		var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
		var groupDict = new Dictionary<string, TreeViewItem>();
		foreach (var method in methods)
        {
			var group = method.GetCustomAttribute<AssetClearanceMethod>().Group;
			TreeViewItem parent = AssetClearanceUtil.BuildGroup(root, groupDict, group);
			parent.AddChild(new Item { id = 0, displayName = method.Name, method = method, children = new List<TreeViewItem>() });
        }
		var id = 0;
		AssetClearanceUtil.SetIndexToTreeItems(root, ref id);
		SetupDepthsFromParentsAndChildren(root);
		return root;
	}
    protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
    {
		return !item.hasChildren && item.displayName.ToLowerInvariant().Contains(search.ToLowerInvariant());
    }
    protected override void BeforeRowsGUI()
    {
        base.BeforeRowsGUI();
		lastSelectedItem = null;
		lineCount = 0;
		lastSelectedLine = 0;
	}
    protected override void RowGUI(RowGUIArgs args)
    {
		var item = args.item;
		if (args.selected)
        {
			lastSelectedItem = item;
			lastSelectedLine = lineCount;
        }
        var cellRect = args.rowRect;
		Rect toggleRect = cellRect;
		toggleRect.x += GetContentIndent(item);
		toggleRect.width = 19;
		toggleRect.height = 19;
		extraSpaceBeforeIconAndLabel = !item.hasChildren ? 16 : 0;
		if (!item.hasChildren)
		{
			GUI.DrawTexture(AssetClearanceRulesEditor.Deflate(toggleRect, 2, 2), AssetClearanceRulesEditor.Style.MethodIcon);
		}
		base.RowGUI(args);
		lineCount++;
    }
}
