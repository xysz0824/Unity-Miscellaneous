using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AutoGrouping))]
public class AutoGroupingEditor : Editor
{
    bool groupsFoldout;
    bool originalHierarchiesFoldout;
    Dictionary<int, bool> groupFoldouts = new Dictionary<int, bool>();
    public override void OnInspectorGUI()
    {
        var autoGrouping = target as AutoGrouping;
        groupsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(groupsFoldout, "Groups");
        if (groupsFoldout)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < autoGrouping.groups.Count; ++i)
            {
                if (!groupFoldouts.ContainsKey(i)) groupFoldouts[i] = false;
                groupFoldouts[i] = EditorGUILayout.Foldout(groupFoldouts[i], autoGrouping.groups[i].name);
                if (groupFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField("Root", autoGrouping.groups[i].root, typeof(GameObject), true);
                    EditorGUI.indentLevel++;
                    for (int k = 0; k < autoGrouping.groups[i].children.Count; ++k)
                    {
                        EditorGUILayout.ObjectField("Child [" + k + "]", autoGrouping.groups[i].children[k], typeof(GameObject), true);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel-= 2;
                }
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        originalHierarchiesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(originalHierarchiesFoldout, "Original Hierarchies");
        if (originalHierarchiesFoldout)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            for (int i = 0; i < autoGrouping.originalHierarchies.Count; ++i)
            {
                EditorGUILayout.ObjectField("Transform", autoGrouping.originalHierarchies[i].transform, typeof(Transform), true);
                EditorGUILayout.ObjectField("Original Parent", autoGrouping.originalHierarchies[i].originalParent, typeof(Transform), true);
                EditorGUILayout.Separator();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        if (GUILayout.Button("Rebuild Group"))
        {
            autoGrouping.RebuildGroup();
        }
        if (GUILayout.Button("Recover"))
        {
            autoGrouping.Recover();
        }
    }
}
