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
    static int vertexCount = 65535;
    static bool renderableOnly = true;
    static List<Transform> transforms;
    static List<Vector3?> vector3s;
    static List<Bounds?> bounds;
    static Dictionary<Transform, Transform> originalParents;
    static List<GameObject> groups;
    static Dictionary<int, Bounds> groupBounds;

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
    static List<Vector3?> GetVector3s(List<Transform> transforms)
    {
        var vector3s = new List<Vector3?>();
        foreach (var transform in transforms)
        {
            vector3s.Add(transform == null ? null : new Vector3?(transform.position));
        }
        return vector3s;
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
        var indexA = 0;
        foreach (var a in positions)
        {
            if (a == null)
            {
                indexA++;
                continue;
            }
            groupIDs[indexA] = indexA;
            indexA++;
        }
        indexA = 0;
        foreach (var a in positions)
        {
            if (a == null)
            {
                indexA++;
                continue;
            }
            var indexB = 0;
            foreach (var b in positions)
            {
                if (b == null || a == b)
                {
                    indexB++;
                    continue;
                }
                var dist = Vector3.Distance(a.Value, b.Value);
                if (dist <= maxDistance * 2)
                {
                    groupIDs[indexB] = groupIDs[indexA];
                }
                indexB++;
            }
            indexA++;
        }
        for (int i = 0; i < groupIDs.Length; ++i)
        {
            groupIDs[i] = GetGroupRootID(groupIDs, i);
        }
        return groupIDs;
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
            var groupIDs = GroupID(vector3s);
            groupBounds = CalculateGroupBounds(bounds, vector3s, groupIDs);
        });
        taskThread.Start();
    }
    void Awake()
    {
        transforms = GetTransforms(renderableOnly, groupRoot);
        vector3s = GetVector3s(transforms);
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
    }
    void Group()
    {
        transforms = GetTransforms(renderableOnly, groupRoot);
        vector3s = GetVector3s(transforms);
        bounds = GetBounds(transforms);
        var groupIDs = GroupID(vector3s);
        groupBounds = CalculateGroupBounds(bounds, vector3s, groupIDs);
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
        vector3s = GetVector3s(transforms);
        bounds = GetBounds(transforms);
        var groupIDs = GroupID(vector3s);
        groupBounds = CalculateGroupBounds(bounds, vector3s, groupIDs);
    }
    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        groupRoot = (GameObject)EditorGUILayout.ObjectField("Group Root", groupRoot, typeof(GameObject), true);
        renderableOnly = EditorGUILayout.Toggle("Renderable Only", renderableOnly);
        if (groups == null && EditorGUI.EndChangeCheck())
        {
            transforms = GetTransforms(renderableOnly, groupRoot);
            vector3s = GetVector3s(transforms);
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
        vertexCount = Mathf.Max(0, EditorGUILayout.IntField("Vertex Count", vertexCount));
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
        if (groupBounds != null)
        {
            foreach (var groupBound in groupBounds.Values)
            {
                Handles.color = groupColor;
                Handles.DrawWireCube(groupBound.center, groupBound.size);
            }
        }
    }
}
