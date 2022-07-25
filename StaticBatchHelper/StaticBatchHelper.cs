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
        public Renderer renderer;
        public int combinedMeshID;
        public StaticBatchInfo staticBatchInfo;
    }
    public List<string> combinedMeshGUIDs = new List<string>();
    public List<CombinedRendererInfo> combinedRendererInfos = new List<CombinedRendererInfo>();
    public string appliedPrefabGUID = "";
#if UNITY_EDITOR
    public void BatchDirectly(string destPath)
    {
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
                var assetPath = destPath + "/Combined Mesh_" + guid + ".mesh";
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);
                guid = AssetDatabase.AssetPathToGUID(assetPath);
                AssetDatabase.RenameAsset(assetPath, "Combined Mesh_" + guid);
                combinedMeshes.Add(mesh);
            }
        }
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
            combinedMesh.Add(AssetDatabase.LoadAssetAtPath<Mesh>(path));
        }
        foreach (var info in combinedRendererInfos)
        {
            var meshFilter = info.renderer.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = combinedMesh[info.combinedMeshID];
            var serializedRenderer = new SerializedObject(info.renderer);
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
