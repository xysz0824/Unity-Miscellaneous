using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StaticBatchHelper : MonoBehaviour
{
    [NonSerialized]
    Dictionary<MeshFilter, Mesh> originalMeshes;
    [Serializable]
    public struct StaticBatchInfo
    {
        public int firstSubMesh;
        public int subMeshCount;
    }
    [Serializable]
    public class CombinedRendererInfo
    {
        public MeshRenderer meshRenderer;
        public int combinedMeshID;
        public StaticBatchInfo staticBatchInfo;
    }
    public List<GameObject> selectObjects = new List<GameObject>();
    public List<string> combinedMeshGUIDs = new List<string>();
    public List<CombinedRendererInfo> combinedRendererInfos = new List<CombinedRendererInfo>();
    public string appliedPrefabGUID = "";
    [SerializeField]
    float batchRate;
#if UNITY_EDITOR
    static bool IsSelectedParent(GameObject gameObject, GameObject parent)
    {
        if (gameObject == parent) return true;
        if (gameObject == null) return false;
        return IsSelectedParent(gameObject.transform.parent?.gameObject, parent);
    }
    static bool IsSelected(GameObject gameObject, GameObject selectObject)
    {
        if (gameObject == selectObject) return true;
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            var child = gameObject.transform.GetChild(i).gameObject;
            if (IsSelected(child, selectObject))
            {
                return true;
            }
        }
        return false;
    }
    public List<GameObject> DisactiveUnselectedObjects()
    {
        var inactives = new List<GameObject>();
        var ignores = GetComponentsInChildren<StaticBatchIgnore>();
        foreach (var ignore in ignores)
        {
            if (ignore.gameObject.activeSelf)
            {
                ignore.gameObject.SetActive(false);
                inactives.Add(ignore.gameObject);
            }
        }
        if (selectObjects.Count == 0)
        {
            return inactives;
        }
        var transforms = GetComponentsInChildren<Transform>();
        foreach (var transform in transforms)
        {
            if (transform == this.transform) continue;
            bool selected = false;
            foreach (var selectObject in selectObjects)
            {
                if (IsSelected(transform.gameObject, selectObject) || IsSelectedParent(transform.gameObject, selectObject))
                {
                    selected = true;
                    break;
                }
            }
            if (!selected && transform.gameObject.activeSelf)
            {
                transform.gameObject.SetActive(false);
                inactives.Add(transform.gameObject);
            }
        }
        return inactives;
    }
    public void ReactiveUnselectedObjects(List<GameObject> inactives)
    {
        foreach (var inactive in inactives)
        {
            inactive.SetActive(true);
        }
    }
    public void BatchDirectly(string destPath)
    {
        var inactives = DisactiveUnselectedObjects();
        var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
        originalMeshes = new Dictionary<MeshFilter, Mesh>();
        foreach (var meshFilter in meshFilters)
        {
            originalMeshes[meshFilter] = meshFilter.sharedMesh;
        }
        StaticBatchingUtility.Combine(gameObject);
        var combinedMeshes = new List<Mesh>();
        foreach (var meshFilter in meshFilters)
        {
            var mesh = meshFilter.sharedMesh;
            if (mesh.name.Contains("Combined Mesh (") && !combinedMeshes.Contains(mesh))
            {
                string guid = "00000000000000000000000000000000";
                var assetPath = destPath + "/CombinedMesh_" + guid + ".mesh";
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                AssetDatabase.RenameAsset(assetPath, "CombinedMesh_" + guid);
                combinedMeshes.Add(mesh);
            }
        }
        ReactiveUnselectedObjects(inactives);
    }
    public void ClearBatch()
    {
        if (originalMeshes == null) return;
        foreach (var kv in originalMeshes)
        {
            var meshFilter = kv.Key;
            var mesh = kv.Value;
            if (meshFilter.sharedMesh != mesh)
            {
                if (meshFilter.sharedMesh != null)
                {
                    var path = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                    AssetDatabase.DeleteAsset(path);
                }
                meshFilter.sharedMesh = mesh;
                var renderer = meshFilter.GetComponent<MeshRenderer>();
                var serializedRenderer = new SerializedObject(renderer);
                var staticBatchRootProperty = serializedRenderer.FindProperty("m_StaticBatchRoot");
                staticBatchRootProperty.objectReferenceValue = null;
                var firstSubMeshProperty = serializedRenderer.FindProperty("m_StaticBatchInfo.firstSubMesh");
                var subMeshCountProperty = serializedRenderer.FindProperty("m_StaticBatchInfo.subMeshCount");
                firstSubMeshProperty.intValue = 0;
                subMeshCountProperty.intValue = 0;
                serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
                serializedRenderer.UpdateIfRequiredOrScript();
            }
        }
    }
    public void ApplyInEditor()
    {
        var combinedMesh = new List<Mesh>();
        foreach (var guid in combinedMeshGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            combinedMesh.Add(string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Mesh>(path));
        }
        foreach (var info in combinedRendererInfos)
        {
            if (info == null || info.meshRenderer == null) continue;
            var meshFilter = info.meshRenderer.GetComponent<MeshFilter>();
            if (info.combinedMeshID < 0 || info.combinedMeshID >= combinedMesh.Count || combinedMesh[info.combinedMeshID] == null) continue;
            meshFilter.sharedMesh = combinedMesh[info.combinedMeshID];
            var serializedRenderer = new SerializedObject(info.meshRenderer);
            var staticBatchRootProperty = serializedRenderer.FindProperty("m_StaticBatchRoot");
            staticBatchRootProperty.objectReferenceValue = transform;
            var firstSubMeshProperty = serializedRenderer.FindProperty("m_StaticBatchInfo.firstSubMesh");
            var subMeshCountProperty = serializedRenderer.FindProperty("m_StaticBatchInfo.subMeshCount");
            firstSubMeshProperty.intValue = info.staticBatchInfo.firstSubMesh;
            subMeshCountProperty.intValue = info.staticBatchInfo.subMeshCount;
            serializedRenderer.ApplyModifiedProperties();
            serializedRenderer.UpdateIfRequiredOrScript();
        }
    }
    void Start()
    {
        ApplyInEditor();
    }
#endif
}
