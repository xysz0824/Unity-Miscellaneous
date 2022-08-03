using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class AutoGrouping : MonoBehaviour
{
    [Serializable]
    public class Group
    {
        public int id;
        public string name;
        public GameObject root;
        public List<Transform> children;
        public List<GameObject> addedInstances;
    }
    public List<Group> groups = new List<Group>();
    [Serializable]
    public class OriginalHierarchy
    {
        public Transform transform;
        public Transform originalParent;
    }
    public List<OriginalHierarchy> originalHierarchies = new List<OriginalHierarchy>();
#if UNITY_EDITOR
    public static bool CheckIfHierarchyImmutable(GameObject gameObject)
    {
        return PrefabUtility.IsPartOfAnyPrefab(gameObject) &&
                PrefabUtility.GetNearestPrefabInstanceRoot(gameObject) &&
                !PrefabUtility.IsAddedGameObjectOverride(gameObject) &&
                !PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject);
    }
    public void Recover()
    {
        for (int i = 0; i < transform.childCount; ++i)
        {
            var child = transform.GetChild(i);
            var autoGrouping = child.GetComponent<AutoGrouping>();
            if (autoGrouping)
            {
                autoGrouping.Recover();
            }
        }
        foreach (var originalHierarchy in originalHierarchies)
        {
            if (originalHierarchy.transform == null || originalHierarchy.originalParent == null) continue;
            originalHierarchy.transform.SetParent(originalHierarchy.originalParent, true);
        }
        foreach (var group in groups)
        {
            if (group.root)
            {
                DestroyImmediate(group.root);
            }
            group.root = null;
            if (group.addedInstances != null)
            {
                foreach (var added in group.addedInstances)
                {
                    DestroyImmediate(added);
                }
                group.addedInstances.Clear();
            }
        }
        EditorUtility.SetDirty(gameObject);
    }
    public void RebuildGroup()
    {
        Recover();
        foreach (var group in groups)
        {
            if (group.root) continue;
            group.root = new GameObject(group.name);
            group.root.transform.SetParent(transform, false);
            group.addedInstances = new List<GameObject>();
            foreach (var child in group.children)
            {
                if (CheckIfHierarchyImmutable(child.gameObject))
                {
                    var instance = Instantiate(child.gameObject);
                    var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(child);
                    var prefabRoot = group.root.transform.Find(instanceRoot.name);
                    if (!prefabRoot)
                    {
                        prefabRoot = new GameObject(instanceRoot.name).transform;
                        prefabRoot.SetParent(group.root.transform, false);
                    }
                    instance.name = child.name;
                    instance.transform.SetParent(prefabRoot, true);
                    instance.transform.position = child.position;
                    instance.transform.rotation = child.rotation;
                    group.addedInstances.Add(instance);
                }
                else
                {
                    child.transform.SetParent(group.root.transform, true);
                }
            }
        }
        foreach (var group in groups)
        {
            group.root.transform.SetSiblingIndex(group.id);
        }
        EditorUtility.SetDirty(gameObject);
    }
#endif
}