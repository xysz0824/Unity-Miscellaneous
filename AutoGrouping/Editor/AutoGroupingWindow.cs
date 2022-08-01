using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AutoGroupingWindow : EditorWindow
{
    static GameObject groupRoot;
    static float maxDistance = 2.5f;
    static Color groupColor = Color.white;
    static int vertexCount = 65536;
    static bool renderableOnly = true;
    static List<Transform> transforms;
    static List<Vector3?> positions;
    static List<Bounds?> bounds;
    static Dictionary<Transform, Transform> originalParents;
    static List<GameObject> groups;
    static Dictionary<int, Bounds> previewBounds;
    static Dictionary<int, Bounds> resultBounds;

    Thread taskThread;

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
    [MenuItem("GameObject/AutoGrouping", false)]
    static void Open()
    {
        groupRoot = Selection.activeGameObject;
        GetWindow<AutoGroupingWindow>("AutoGrouping").Show();
    }
    static bool CheckRenderable(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null) return false;
        var size = renderer.bounds.size.x * renderer.bounds.size.y * renderer.bounds.size.z;
        if (size == 0) return false;
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
    static int GetGroupRootID(int[] groupIDs, int id)
    {
        id = groupIDs[id];
        while (groupIDs[id] != id)
        {
            id = groupIDs[id];
        }
        return id;
    }
    static int[] GroupID(List<Vector3?> positions)
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
                if (dist <= maxDistance * 2)
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
    void DoGroupTask()
    {
        if (taskThread != null && taskThread.IsAlive)
        {
            taskThread.Abort();
            taskThread = null;
        }
        taskThread = new Thread(() =>
        {
            var groupIDs = GroupID(positions);
            previewBounds = CalculateGroupBounds(bounds, positions, groupIDs);
        });
        taskThread.Start();
    }
    void Awake()
    {
        transforms = GetTransforms(renderableOnly, groupRoot);
        positions = GetPositions(transforms);
        bounds = GetBounds(transforms);
        DoGroupTask();
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
        transforms = GetTransforms(renderableOnly, groupRoot);
        positions = GetPositions(transforms);
        bounds = GetBounds(transforms);
        var groupIDs = GroupID(positions);
        SplitGroupByVertexCount(transforms, ref groupIDs);
        previewBounds = null;
        resultBounds = CalculateGroupBounds(bounds, positions, groupIDs);
        groups = new List<GameObject>();
        var groupDict = new Dictionary<int, GameObject>();
        originalParents = new Dictionary<Transform, Transform>();
        for (int i = 0; i < transforms.Count; ++i)
        {
            if (!groupDict.ContainsKey(groupIDs[i]))
            {
                var group = new GameObject("Group_" + groupDict.Count);
                group.transform.SetParent(groupRoot == null ? null : groupRoot.transform, false);
                groupDict[groupIDs[i]] = group;
                groups.Add(group);
            }
            if (PrefabUtility.IsPartOfAnyPrefab(transforms[i]) && !PrefabUtility.IsAddedGameObjectOverride(transforms[i].gameObject))
            {
                var copy = Instantiate(transforms[i]);
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(transforms[i]);
                var prefabRoot = groupDict[groupIDs[i]].transform.Find(instanceRoot.name);
                if (!prefabRoot)
                {
                    prefabRoot = new GameObject(instanceRoot.name).transform;
                    prefabRoot.SetParent(groupDict[groupIDs[i]].transform, false);
                }
                copy.name = transforms[i].name;
                copy.transform.SetParent(prefabRoot, true);
                copy.transform.position = transforms[i].transform.position;
                copy.transform.rotation = transforms[i].transform.rotation;
            }
            else
            {
                originalParents[transforms[i]] = transforms[i].parent;
                transforms[i].SetParent(groupDict[groupIDs[i]].transform, true);
            }
        }
    }
    void Recover()
    {
        if (originalParents != null)
        {
            foreach (var originalParent in originalParents)
            {
                if (originalParent.Key == null) continue;
                originalParent.Key.SetParent(originalParent.Value);
            }
            originalParents = null;
        }
        foreach (var group in groups)
        {
            DestroyImmediate(group);
        }
        groups = null;
        transforms = GetTransforms(renderableOnly, groupRoot);
        positions = GetPositions(transforms);
        bounds = GetBounds(transforms);
        var groupIDs = GroupID(positions);
        resultBounds = null;
        previewBounds = CalculateGroupBounds(bounds, positions, groupIDs);
    }
    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        groupRoot = (GameObject)EditorGUILayout.ObjectField("Group Root", groupRoot, typeof(GameObject), true);
        renderableOnly = EditorGUILayout.Toggle("Renderable Only", renderableOnly);
        if (groups == null && EditorGUI.EndChangeCheck())
        {
            transforms = GetTransforms(renderableOnly, groupRoot);
            positions = GetPositions(transforms);
            bounds = GetBounds(transforms);
            DoGroupTask();
            SceneView.RepaintAll();
        }
        EditorGUI.BeginChangeCheck();
        maxDistance = Mathf.Max(0, EditorGUILayout.FloatField("Max Distance", maxDistance));
        groupColor = EditorGUILayout.ColorField("Group Color", groupColor);
        if (groups == null && EditorGUI.EndChangeCheck())
        {
            DoGroupTask();
            SceneView.RepaintAll();
        }
        vertexCount = Mathf.Max(256, EditorGUILayout.IntField("Vertex Count", vertexCount));
        EditorGUI.BeginDisabledGroup(transforms == null || transforms.Count == 0);
        if (groups == null && GUILayout.Button("Group"))
        {
            Group();
        }
        EditorGUI.EndDisabledGroup();
        if (groups != null && GUILayout.Button("Recover"))
        {
            Recover();
        }
    }
    void OnSceneGUI(SceneView sceneView)
    {
        if (previewBounds != null)
        {
            foreach (var groupBound in previewBounds.Values)
            {
                Handles.color = groupColor;
                Handles.DrawWireCube(groupBound.center, groupBound.size);
            }
        }
        else if (resultBounds != null)
        {
            foreach (var groupBound in resultBounds.Values)
            {
                Handles.color = groupColor;
                Handles.DrawWireCube(groupBound.center, groupBound.size);
            }
        }
    }
}
