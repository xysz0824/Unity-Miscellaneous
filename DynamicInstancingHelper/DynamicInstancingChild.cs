using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class DynamicInstancingChild : MonoBehaviour
{
    Mesh mesh;
    public Mesh Mesh => mesh;
    Material material;
    public Material Material => material;
    long resourceID;
    public long ResourceID => resourceID;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    public bool syncTransform;
    public int layer;
    public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
    public bool receiveShadows = true;
    public bool enableInLowEnd = true;
    [NonSerialized]
    public int childrenID = -1;
    [NonSerialized]
    public int boundingID = -1;
    [NonSerialized]
    public bool visible = true;
    [NonSerialized]
    public int syncDelay = 0;
    public bool Init()
    {
        if (!SystemInfo.supportsInstancing || !DynamicInstancingRenderer.Instance) return false;
        if (!enableInLowEnd && Shader.globalMaximumLOD <= DynamicInstancingRenderer.Instance.lodThreshold) return false;;
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter?.sharedMesh;
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        material = meshRenderer?.sharedMaterial;
        if (mesh == null || material == null)
        {
            Debug.LogError($"Instancing object \"{name}\" has null resource.");
            return false;
        }
        if (!material.enableInstancing)
        {
            Debug.LogWarning($"Material \"{material.name}\" doesn't enable instancing.");
            return false;
        }
        resourceID = GetResourceID();
        return true;
    }
    void OnEnable()
    {
        if (!Init()) return;
        DynamicInstancingRenderer.Instance.Join(this);
        syncDelay = DynamicInstancingRenderer.Instance.syncDelay;
        meshRenderer.enabled = false;
    }
    void Start()
    {
        if (DynamicInstancingRenderer.Instance != null)
        {
            DynamicInstancingRenderer.Instance.UpdateTransform(this);
        }
    }
    void OnDisable()
    {
        if (DynamicInstancingRenderer.Instance != null)
        {
            DynamicInstancingRenderer.Instance.Quit(this);
        }
    }
    void OnDestroy()
    {
        OnDisable();
    }
    long GetResourceID()
    {
        if (mesh == null)
        {
            Debug.LogError(string.Format("{0} mesh is null", gameObject.name));
            return 0;
        }

        if (material == null)
        {
            Debug.LogError(string.Format("{0} material is null", gameObject.name));
            return 0;
        }
        
        return mesh.GetHashCode() * 17 + material.GetHashCode() * 13 + layer * 11 + (int)shadowCastingMode * 19 + (receiveShadows ? 41 : 0);
    }
}
