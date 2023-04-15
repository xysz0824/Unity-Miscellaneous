using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

[RequireComponent(typeof(MeshRenderer))]
public class StaticBatchComponent : MonoBehaviour
{
    static readonly MethodInfo setInfoMethod = typeof(Renderer).GetMethod("SetStaticBatchInfo", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly PropertyInfo rootTransformProperty = typeof(Renderer).GetProperty("staticBatchRootTransform", BindingFlags.Instance | BindingFlags.NonPublic);
    public Transform root;
    public Mesh combinedMesh;
    public int firstSubMesh;
    public int subMeshCount;
    public Action onDestroy;
    
    public void ActiveBatch()
    {
        if (combinedMesh == null) return;
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) return;
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshFilter.mesh != combinedMesh)
        {
            meshFilter.mesh = combinedMesh;
            rootTransformProperty.SetValue(meshRenderer, root);
            setInfoMethod.Invoke(meshRenderer, new object[] { firstSubMesh, subMeshCount });
            meshFilter.mesh = combinedMesh;
        }
    }
    void OnEnable()
    {
        ActiveBatch();
    }
    void OnDestroy()
    {
        onDestroy?.Invoke();
    }
}
