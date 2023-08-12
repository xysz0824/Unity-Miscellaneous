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
    DynamicInstancingCollection collection;
    public bool syncTransform;
    public int layer;
    public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
    public bool receiveShadows = true;
    public bool enableInLowEnd;
    [NonSerialized]
    public int childrenID = -1;
    [NonSerialized]
    public int boundingID = -1;
    [NonSerialized]
    public bool visible = true;
    void OnEnable()
    {
        if (!SystemInfo.supportsInstancing || !DynamicInstancingRenderer.Instance) return;
        if (Shader.globalMaximumLOD <= DynamicInstancingRenderer.Instance.lodThreshold && !enableInLowEnd) return;
        var meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter?.sharedMesh;
        var meshRenderer = GetComponent<MeshRenderer>();
        material = meshRenderer?.sharedMaterial;
        if (mesh == null || material == null)
        {
            Debug.LogError($"Instancing object \"{name}\" has null resource.");
            return;
        }
        resourceID = GetResourceID();
        if (!material.enableInstancing)
        {
            Debug.LogWarning($"Material \"{material.name}\" doesn't enable instancing.");
            return;
        }
        DynamicInstancingRenderer.Instance.Join(this);
        meshRenderer.enabled = false;
        collection = GetComponentInParent<DynamicInstancingCollection>();
        if (collection != null) collection.AddChild(this);
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
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
            if (collection != null) collection.RemoveChild(this);
        }
    }
    void OnTransformChildrenChanged()
    {
        if (DynamicInstancingRenderer.Instance != null)
        {
            if (collection != null) collection.RemoveChild(this);
            collection = GetComponentInParent<DynamicInstancingCollection>();
            if (collection != null) collection.AddChild(this);
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
