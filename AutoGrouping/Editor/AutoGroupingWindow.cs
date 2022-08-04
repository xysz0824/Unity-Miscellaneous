using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AutoGroupingWindow : EditorWindow
{
    public enum Strategy
    {
        ByPosition,
        ByPrefix
    }
    static Strategy strategy;
    static GameObject groupRoot;
    static bool renderableOnly = true;
    static float maxDistance = 2.5f;
    static Color groupColor = Color.white;
    static int vertexCount = 65536;
    static float similarity = 0.6f;

    List<Transform> transforms;
    List<Vector3?> positions;
    List<Bounds?> bounds;
    List<string> names;
    Dictionary<int, Bounds> groupBounds;
    Dictionary<int, string> groupNames;

    Thread taskThread;
    bool needRepaint;
    Vector2 scrollPos;

    [MenuItem("GameObject/Delete Parent", false, 0)]
    static void DeleteParent()
    {
        var gameObjects = Selection.gameObjects;
        foreach (var gameObject in gameObjects)
        {
            for (int i = 0; i < gameObject.transform.childCount; ++i)
            {
                var child = gameObject.transform.GetChild(i);
                Undo.SetTransformParent(child.transform, gameObject.transform.parent, "Set Transform Parent");
                child.transform.SetParent(gameObject.transform.parent, true);
                i--;
            }
            Undo.DestroyObjectImmediate(gameObject);
        }
    }
    [MenuItem("GameObject/Auto Grouping", false)]
    static void Open()
    {
        groupRoot = Selection.activeGameObject;
        var window = GetWindow<AutoGroupingWindow>("AutoGrouping");
        window.transforms = GetTransforms(renderableOnly, groupRoot);
        window.positions = GetPositions(window.transforms);
        window.bounds = GetBounds(window.transforms);
        window.names = GetNames(window.transforms);
        window.DoGroupTask();
        window.Show();
    }
    static bool CheckRenderable(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null) return false;
        if (renderer.bounds.size.x == 0 && renderer.bounds.size.y == 0 && renderer.bounds.size.z == 0) return false;
        return true;
    }
    static void GetTransforms(bool renderableOnly, GameObject root, ref List<Transform> transforms)
    {
        var validTransforms = root.GetComponentsInChildren<Transform>();
        foreach (var transform in validTransforms)
        {
            if (!renderableOnly || CheckRenderable(transform.GetComponent<Renderer>()))
                transforms.Add(transform);
        }
    }
    static List<Transform> GetTransforms(bool renderableOnly, GameObject root)
    {
        var transforms = new List<Transform>();
        if (root != null) GetTransforms(renderableOnly, root, ref transforms);
        return transforms;
    }
    static List<Bounds?> GetBounds(List<Transform> transforms)
    {
        var bounds = new List<Bounds?>();
        foreach (var transform in transforms)
        {
            var renderer = transform == null ? null : transform.GetComponent<Renderer>();
            bounds.Add(renderer == null ? null : new Bounds?(renderer.bounds));
        }
        return bounds;
    }
    static List<Vector3?> GetPositions(List<Transform> transforms)
    {
        var positions = new List<Vector3?>();
        foreach (var transform in transforms)
        {
            positions.Add(transform == null ? null : new Vector3?(transform.position));
        }
        return positions;
    }
    static List<string> GetNames(List<Transform> transforms)
    {
        var names = new List<string>();
        foreach (var transform in transforms)
        {
            names.Add(transform == null ? null : transform.name.Trim().ToLowerInvariant());
        }
        return names;
    }
    static int GetGroupRootID(int[] groupIDs, int id)
    {
        id = groupIDs[id];
        while (groupIDs[id] != id)
        {
            id = groupIDs[id];
        }
        return id;
    }
    static int[] GroupIDByPosition(List<Vector3?> positions)
    {
        var groupIDs = new int[positions.Count];
        for (int i = 0; i < positions.Count; ++i)
        {
            if (positions[i] == null) continue;
            groupIDs[i] = i;
        }
        for (int i = 0; i < positions.Count; ++i)
        {
            if (positions[i] == null) continue;
            for (int k = 0; k < positions.Count; ++k)
            {
                if (positions[k] == null || positions[i] == positions[k]) continue;
                var dist = Vector3.Distance(positions[i].Value, positions[k].Value);
                if (dist <= maxDistance * 2 && groupIDs[groupIDs[i]] != k)
                {
                    groupIDs[k] = groupIDs[i];
                }
            }
        }
        for (int i = 0; i < groupIDs.Length; ++i)
        {
            groupIDs[i] = GetGroupRootID(groupIDs, i);
        }
        return groupIDs;
    }
    static void SplitGroupByVertexCount(List<Transform> transforms, ref int[] groupIDs, int groupID, ref int maxGroupID)
    {
        var sorted = new List<int>();
        for (int i = 0; i < groupIDs.Length; ++i)
        {
            if (groupIDs[i] == groupID)
            {
                sorted.Add(i);
            }
        }
        sorted.Sort((a, b) =>
        {
            var meshFilterA = transforms[a] == null ? null : transforms[a].GetComponent<MeshFilter>();
            var meshA = meshFilterA == null ? null : meshFilterA.sharedMesh;
            var meshFilterB = transforms[b] == null ? null : transforms[b].GetComponent<MeshFilter>();
            var meshB = meshFilterB == null ? null : meshFilterB.sharedMesh;
            var valueA = meshA == null ? 0 : meshA.vertexCount;
            var valueB = meshB == null ? 0 : meshB.vertexCount;
            return valueB - valueA;
        }
        );
        var newGroupIDs = new List<int>();
        for (int i = 0; i < sorted.Count; ++i)
        {
            if (transforms[sorted[i]] == null || newGroupIDs.Contains(groupIDs[sorted[i]])) continue;
            newGroupIDs.Add(++maxGroupID);
            groupIDs[sorted[i]] = newGroupIDs[newGroupIDs.Count - 1];
            var meshFilter = transforms[sorted[i]].GetComponent<MeshFilter>();
            var mesh = meshFilter == null ? null : meshFilter.sharedMesh;
            var vertexCount = mesh == null ? 0 : mesh.vertexCount;
            while (vertexCount < AutoGroupingWindow.vertexCount)
            {
                var nearestDist = float.MaxValue;
                var nearestID = 0;
                var nearestVertexCount = 0;
                for (int k = i + 1; k < sorted.Count; ++k)
                {
                    if (transforms[sorted[k]] == null || newGroupIDs.Contains(groupIDs[sorted[k]])) continue;
                    var dist = Vector3.Distance(transforms[sorted[i]].position, transforms[sorted[k]].position);
                    if (nearestDist >= dist)
                    {
                        meshFilter = transforms[sorted[k]].GetComponent<MeshFilter>();
                        mesh = meshFilter == null ? null : meshFilter.sharedMesh;
                        nearestVertexCount = mesh == null ? 0 : mesh.vertexCount;
                        if (vertexCount + nearestVertexCount > AutoGroupingWindow.vertexCount) continue;
                        nearestDist = dist;
                        nearestID = sorted[k];
                    }
                }
                if (nearestDist < float.MaxValue)
                {
                    vertexCount += nearestVertexCount;
                    groupIDs[nearestID] = newGroupIDs[newGroupIDs.Count - 1];
                }
                if (nearestDist == float.MaxValue || vertexCount >= AutoGroupingWindow.vertexCount)
                {
                    break;
                }
            }
        }
    }
    static void SplitGroupByVertexCount(List<Transform> transforms, ref int[] groupIDs)
    {
        var groupVertices = new Dictionary<int, int>();
        var maxGroupID = 0;
        for (int i = 0; i < transforms.Count; ++i)
        {
            var meshFilter = transforms[i] == null ? null : transforms[i].GetComponent<MeshFilter>();
            var mesh = meshFilter == null ? null : meshFilter.sharedMesh;
            if (!groupVertices.ContainsKey(groupIDs[i]))
            {
                groupVertices[groupIDs[i]] = mesh == null ? 0 : mesh.vertexCount;
            }
            else
            {
                groupVertices[groupIDs[i]] += mesh == null ? 0 : mesh.vertexCount;
            }
            if (groupIDs[i] >= maxGroupID)
            {
                maxGroupID = groupIDs[i];
            }
        }
        foreach (var groupVertex in groupVertices)
        {
            if (groupVertex.Value > vertexCount)
            {
                SplitGroupByVertexCount(transforms, ref groupIDs, groupVertex.Key, ref maxGroupID);
            }
        }
    }
    static Dictionary<int, Bounds> CalculateGroupBounds(List<Bounds?> bounds, List<Vector3?> positions, int[] groupIDs)
    {
        var groupBounds = new Dictionary<int, Bounds>();
        for (int i = 0; i < positions.Count; ++i)
        {
            if (positions[i] == null) continue;
            if (!groupBounds.ContainsKey(groupIDs[i]))
            {
                groupBounds[groupIDs[i]] = bounds[i] != null ? bounds[i].Value : new Bounds(positions[i].Value, new Vector3());
            }
            else if (bounds[i] != null)
            {
                var groupBound = groupBounds[groupIDs[i]];
                groupBound.Encapsulate(bounds[i].Value);
                groupBounds[groupIDs[i]] = groupBound;
            }
        }
        return groupBounds;
    }
    static void SortGroupByPosition(Dictionary<int, Bounds> bounds, ref int[] groupIDs)
    {
        var sortedGroupIDs = new List<int>(bounds.Keys);
        var enumerator = bounds.GetEnumerator();
        enumerator.MoveNext();
        var entireBounds = enumerator.Current.Value;
        while (enumerator.MoveNext())
        {
            entireBounds.Encapsulate(enumerator.Current.Value);
        }
        sortedGroupIDs.Sort((a, b) =>
        {
            var size = new Vector3(1, entireBounds.size.y, entireBounds.size.z);
            var valueA = Vector3.Dot(bounds[a].min - entireBounds.min, size);
            var valueB = Vector3.Dot(bounds[b].min - entireBounds.min, size);
            return (int)(valueA - valueB);
        });
        var changedGroupIds = new List<int>(groupIDs.Length);
        for (int i = 0; i < sortedGroupIDs.Count; ++i)
        {
            for (int k = 0; k < groupIDs.Length; ++k)
            {
                if (!changedGroupIds.Contains(k) && groupIDs[k] == sortedGroupIDs[i])
                {
                    groupIDs[k] = i;
                    changedGroupIds.Add(k);
                }
            }
        }
    }
    static int MaxMatchingPrefixLength(string a, string b)
    {
        int minLength = Mathf.Min(a.Length, b.Length);
        int count;
        for (count = 0; count < minLength; ++count)
        {
            if (a[count] != b[count]) break;
        }
        return count;
    }
    static string MaxMatchingPrefix(string a, string b)
    {
        int minLength = Mathf.Min(a.Length, b.Length);
        var stringBuilder = new StringBuilder(minLength);
        for (int i = 0; i < minLength; ++i)
        {
            if (a[i] != b[i]) break;
            stringBuilder.Append(a[i]);
        }
        return stringBuilder.ToString();
    }
    static int[] GroupIDByName(List<string> names)
    {
        var groupIDs = new int[names.Count];
        for (int i = 0; i < names.Count; ++i)
        {
            if (names[i] == null) continue;
            groupIDs[i] = i;
        }
        for (int i = 0; i < names.Count; ++i)
        {
            if (names[i] == null) continue;
            for (int k = 0; k < names.Count; ++k)
            {
                if (names[k] == null || names[i] == names[k]) continue;
                var prefix = MaxMatchingPrefixLength(names[i], names[k]);
                var testSimilarity = (float)prefix / Mathf.Min(names[i].Length, names[k].Length);
                if (testSimilarity >= similarity)
                {
                    groupIDs[k] = groupIDs[i];
                }
            }
        }
        for (int i = 0; i < groupIDs.Length; ++i)
        {
            groupIDs[i] = GetGroupRootID(groupIDs, i);
        }
        return groupIDs;
    }
    static Dictionary<int, string> GenerateGroupNames(List<string> names, int[] groupIDs)
    {
        var groupNames = new Dictionary<int, string>();
        for (int i = 0; i < names.Count; ++i)
        {
            if (names[i] == null) continue;
            if (!groupNames.ContainsKey(groupIDs[i]))
            {
                groupNames[groupIDs[i]] = names[i];
            }
            else
            {
                var prefix = MaxMatchingPrefix(groupNames[groupIDs[i]], names[i]);
                if (prefix.Length <= groupNames[groupIDs[i]].Length) groupNames[groupIDs[i]] = prefix;
            }
        }
        return groupNames;
    }
    void DoGroupTask()
    {
        if (taskThread != null && taskThread.IsAlive)
        {
            taskThread.Abort();
            taskThread = null;
        }
        taskThread = new Thread(() =>
        {
            switch (strategy)
            {
                case Strategy.ByPosition:
                    var groupIDs = GroupIDByPosition(positions);
                    groupBounds = CalculateGroupBounds(bounds, positions, groupIDs);
                    break;
                case Strategy.ByPrefix:
                    groupIDs = GroupIDByName(names);
                    groupNames = GenerateGroupNames(names, groupIDs);
                    break;
            }
            needRepaint = true;
        });
        taskThread.Start();
    }
    void Update()
    {
        if (needRepaint)
        {
            SceneView.RepaintAll();
            Repaint();
            needRepaint = false;
        }
    }
    void Awake()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }
    void OnDestroy()
    {
        if (taskThread != null && taskThread.IsAlive)
        {
            taskThread.Abort();
            taskThread = null;
        }
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }
    void Group()
    {
        var autoGrouping = groupRoot.GetComponent<AutoGrouping>();
        if (!autoGrouping) autoGrouping = groupRoot.AddComponent<AutoGrouping>();
        else autoGrouping.Recover();
        transforms = GetTransforms(renderableOnly, groupRoot);
        positions = GetPositions(transforms);
        bounds = GetBounds(transforms);
        names = GetNames(transforms);
        int[] groupIDs = null;
        switch (strategy)
        {
            case Strategy.ByPosition:
                groupIDs = GroupIDByPosition(positions);
                SplitGroupByVertexCount(transforms, ref groupIDs);
                groupBounds = CalculateGroupBounds(bounds, positions, groupIDs);
                SortGroupByPosition(groupBounds, ref groupIDs);
                break;
            case Strategy.ByPrefix:
                groupIDs = GroupIDByName(names);
                groupNames = GenerateGroupNames(names, groupIDs);
                break;
        }
        var groupDict = new Dictionary<int, AutoGrouping.Group>();
        var groups = new List<AutoGrouping.Group>();
        var originalHierarchies = new List<AutoGrouping.OriginalHierarchy>();
        for (int i = 0; i < transforms.Count; ++i)
        {
            if (!groupDict.ContainsKey(groupIDs[i]))
            {
                var group = new AutoGrouping.Group();
                group.id = groupIDs[i];
                switch (strategy)
                {
                    case Strategy.ByPosition:
                        group.name = "Group_" + group.id;
                        break;
                    case Strategy.ByPrefix:
                        group.name = groupNames[group.id];
                        break;
                }
                group.children = new List<Transform>();
                groupDict[groupIDs[i]] = group;
                groups.Add(group);
            }
            groupDict[groupIDs[i]].children.Add(transforms[i]);
            if (!AutoGrouping.IsHierarchyImmutable(transforms[i].gameObject))
            {
                var originalHierarchy = new AutoGrouping.OriginalHierarchy();
                originalHierarchy.transform = transforms[i];
                originalHierarchy.originalParent = transforms[i].parent;
                originalHierarchies.Add(originalHierarchy);
            }
        }
        for (int i = 0; i < groups.Count; ++i)
        {
            if (groups[i].children.Count <= 1)
            {
                groups.RemoveAt(i);
                i--;
            }
        }
        groups.Sort((a, b) => a.id - b.id);
        for (int i = 0; i < groups.Count; ++i)
        {
            groups[i].id = i;
        }
        autoGrouping.groups = groups;
        autoGrouping.originalHierarchies = originalHierarchies;
        autoGrouping.RebuildGroup();
        SceneView.RepaintAll();
    }
    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
        EditorGUI.BeginChangeCheck();
        groupRoot = (GameObject)EditorGUILayout.ObjectField("Group Root", groupRoot, typeof(GameObject), true);
        renderableOnly = EditorGUILayout.Toggle("Renderable Only", renderableOnly);
        strategy = (Strategy)EditorGUILayout.EnumPopup("Strategy", strategy);
        if (EditorGUI.EndChangeCheck())
        {
            transforms = GetTransforms(renderableOnly, groupRoot);
            positions = GetPositions(transforms);
            bounds = GetBounds(transforms);
            names = GetNames(transforms);
            DoGroupTask();
        }
        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        switch (strategy)
        {
            case Strategy.ByPosition:
                maxDistance = Mathf.Max(0, EditorGUILayout.FloatField("- Max Distance", maxDistance));
                groupColor = EditorGUILayout.ColorField("- Group Color", groupColor);
                vertexCount = Mathf.Max(256, EditorGUILayout.IntField("- Vertex Count", vertexCount));
                break;
            case Strategy.ByPrefix:
                similarity = Mathf.Clamp(EditorGUILayout.FloatField("- Similarity", similarity), 0f, 1);
                break;
        }
        if (EditorGUI.EndChangeCheck())
        {
            DoGroupTask();
        }
        EditorGUI.indentLevel--;
        EditorGUI.BeginDisabledGroup(transforms == null || transforms.Count == 0);
        if (GUILayout.Button("Group"))
        {
            Group();
        }
        EditorGUI.EndDisabledGroup();
        if (strategy == Strategy.ByPrefix && groupNames != null)
        {
            string groupNameList = "\n";
            foreach (var groupName in groupNames.Values)
            {
                groupNameList += groupName + "\n";
            }
            EditorGUILayout.HelpBox("Named Groups : " + groupNameList, MessageType.Info);
        }
        EditorGUILayout.EndScrollView();
    }
    static Vector3[] rectVertices = new Vector3[4];
    void OnSceneGUI(SceneView sceneView)
    {
        if (strategy == Strategy.ByPosition && groupBounds != null)
        {
            foreach (var groupBound in groupBounds.Values)
            {
                var faceColor = groupColor * 0.35f;
                var outlineColor = groupColor;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                rectVertices[0] = new Vector3(groupBound.min.x, groupBound.min.y, groupBound.min.z);
                rectVertices[1] = new Vector3(groupBound.min.x, groupBound.min.y, groupBound.max.z);
                rectVertices[2] = new Vector3(groupBound.max.x, groupBound.min.y, groupBound.max.z);
                rectVertices[3] = new Vector3(groupBound.max.x, groupBound.min.y, groupBound.min.z);
                Handles.DrawSolidRectangleWithOutline(rectVertices, faceColor, outlineColor);
                rectVertices[0] = new Vector3(groupBound.min.x, groupBound.max.y, groupBound.min.z);
                rectVertices[1] = new Vector3(groupBound.min.x, groupBound.max.y, groupBound.max.z);
                rectVertices[2] = new Vector3(groupBound.max.x, groupBound.max.y, groupBound.max.z);
                rectVertices[3] = new Vector3(groupBound.max.x, groupBound.max.y, groupBound.min.z);
                Handles.DrawSolidRectangleWithOutline(rectVertices, faceColor, outlineColor);
                rectVertices[0] = new Vector3(groupBound.min.x, groupBound.min.y, groupBound.min.z);
                rectVertices[1] = new Vector3(groupBound.min.x, groupBound.min.y, groupBound.max.z);
                rectVertices[2] = new Vector3(groupBound.min.x, groupBound.max.y, groupBound.max.z);
                rectVertices[3] = new Vector3(groupBound.min.x, groupBound.max.y, groupBound.min.z);
                Handles.DrawSolidRectangleWithOutline(rectVertices, faceColor, outlineColor);
                rectVertices[0] = new Vector3(groupBound.max.x, groupBound.min.y, groupBound.min.z);
                rectVertices[1] = new Vector3(groupBound.max.x, groupBound.min.y, groupBound.max.z);
                rectVertices[2] = new Vector3(groupBound.max.x, groupBound.max.y, groupBound.max.z);
                rectVertices[3] = new Vector3(groupBound.max.x, groupBound.max.y, groupBound.min.z);
                Handles.DrawSolidRectangleWithOutline(rectVertices, faceColor, outlineColor);
                rectVertices[0] = new Vector3(groupBound.min.x, groupBound.min.y, groupBound.min.z);
                rectVertices[1] = new Vector3(groupBound.min.x, groupBound.max.y, groupBound.min.z);
                rectVertices[2] = new Vector3(groupBound.max.x, groupBound.max.y, groupBound.min.z);
                rectVertices[3] = new Vector3(groupBound.max.x, groupBound.min.y, groupBound.min.z);
                Handles.DrawSolidRectangleWithOutline(rectVertices, faceColor, outlineColor);
                rectVertices[0] = new Vector3(groupBound.min.x, groupBound.min.y, groupBound.max.z);
                rectVertices[1] = new Vector3(groupBound.min.x, groupBound.max.y, groupBound.max.z);
                rectVertices[2] = new Vector3(groupBound.max.x, groupBound.max.y, groupBound.max.z);
                rectVertices[3] = new Vector3(groupBound.max.x, groupBound.min.y, groupBound.max.z);
                Handles.DrawSolidRectangleWithOutline(rectVertices, faceColor, outlineColor);
            }
        }
    }
}
