using System.Collections;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System.IO;

public class AssetClearanceStringTreeView : TreeView
{
	string[] strings;
	public AssetClearanceStringTreeView(TreeViewState treeViewState, string[] strings)
		: base(treeViewState)
	{
		this.strings = strings;
		Reload();
	}
	protected override TreeViewItem BuildRoot()
	{
		var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
		foreach (var item in strings)
		{
			root.AddChild(new TreeViewItem { id = 0, displayName = item, children = new List<TreeViewItem>() });
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
	protected override void RowGUI(RowGUIArgs args)
	{
		var item = args.item;
		var cellRect = args.rowRect;
		Rect toggleRect = cellRect;
		toggleRect.x += GetContentIndent(item);
		toggleRect.width = 19;
		toggleRect.height = 19;
		extraSpaceBeforeIconAndLabel = !item.hasChildren ? 16 : 0;
		base.RowGUI(args);
	}
}
