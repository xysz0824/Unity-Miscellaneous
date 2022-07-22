using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StaticBatchHelper : MonoBehaviour
{
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
