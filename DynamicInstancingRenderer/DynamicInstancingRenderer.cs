using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

[DisallowMultipleComponent]
public class DynamicInstancingRenderer : MonoBehaviour
{
    static DynamicInstancingRenderer instance;
    public static DynamicInstancingRenderer Instance => instance;
    class Children
    {
        public DynamicInstancingChild[] array = new DynamicInstancingChild[1];
        public int[] sortedIndices = new int[1];
        public Matrix4x4[] transforms = new Matrix4x4[1];
        public Matrix4x4[] sortedTransforms = new Matrix4x4[1];
        public bool hasVisibleStateChanged;
        int count;
        public int Count => count;
        int visibleCount;
        public void Add(DynamicInstancingChild child)
        {
            if (array.Length <= count)
            {
                Array.Resize(ref array, array.Length * 2);
                Array.Resize(ref transforms, transforms.Length * 2);
                Array.Resize(ref sortedIndices, sortedIndices.Length * 2);
                Array.Resize(ref sortedTransforms, sortedTransforms.Length * 2);
            }
            transforms[count] = child.transform.localToWorldMatrix;
            child.childrenID = count++;
            array[child.childrenID] = child;
            hasVisibleStateChanged = true;
        }
        public void RemoveAt(int index)
        {
            array[count - 1].childrenID = index;
            array[index].childrenID = -1;
            array[index] = array[count - 1];
            array[count - 1] = null;
            transforms[index] = transforms[count - 1];
            count--;
            hasVisibleStateChanged = true;
        }
        public void UpdateTransform(int index)
        {
            transforms[index] = array[index].transform.localToWorldMatrix;
            hasVisibleStateChanged = true;
        }
        public void Draw(bool cull)
        {
            if (count <= 0) return;
            for (int index = 0; index < count;)
            {
                var renderCount = Mathf.Min(count - index, 1023);
                if (cull && hasVisibleStateChanged)
                {
                    visibleCount = 0;
                    for (int k = index; k < renderCount + index; ++k)
                    {
                        if (array[k].visible)
                        {
                            sortedTransforms[visibleCount].m00 = transforms[k].m00;
                            sortedTransforms[visibleCount].m01 = transforms[k].m01;
                            sortedTransforms[visibleCount].m02 = transforms[k].m02;
                            sortedTransforms[visibleCount].m03 = transforms[k].m03;
                            sortedTransforms[visibleCount].m10 = transforms[k].m10;
                            sortedTransforms[visibleCount].m11 = transforms[k].m11;
                            sortedTransforms[visibleCount].m12 = transforms[k].m12;
                            sortedTransforms[visibleCount].m13 = transforms[k].m13;
                            sortedTransforms[visibleCount].m20 = transforms[k].m20;
                            sortedTransforms[visibleCount].m21 = transforms[k].m21;
                            sortedTransforms[visibleCount].m22 = transforms[k].m22;
                            sortedTransforms[visibleCount].m23 = transforms[k].m23;
                            sortedTransforms[visibleCount].m30 = transforms[k].m30;
                            sortedTransforms[visibleCount].m31 = transforms[k].m31;
                            sortedTransforms[visibleCount].m32 = transforms[k].m32;
                            sortedTransforms[visibleCount].m33 = transforms[k].m33;
                            ++visibleCount;
                        }
                    }
                    hasVisibleStateChanged = false;
                }
                var mesh = array[0].Mesh;
                var material = array[0].Material;
                var shadowCastingMode = array[0].shadowCastingMode;
                var receiveShadows = array[0].receiveShadows;
                var layer = array[0].layer;
                Graphics.DrawMeshInstanced(mesh, 0, material, cull ? sortedTransforms : transforms, cull ? visibleCount : renderCount, null, shadowCastingMode, receiveShadows, layer);
                index += 1023;
            }
        }
    }
    Dictionary<long, Children> childDict = new Dictionary<long, Children>();
    BoundingSphere[] boundingSpheres = new BoundingSphere[1];
    DynamicInstancingChild[] boundingChildren = new DynamicInstancingChild[1];
    int boundingCount;
    CullingGroup cullingGroup;
    public bool enableCulling = true;
    public Camera cullingCamera;
    public bool syncTransform;
    int lastMipmapLevel;
    int fixedMipmapLevel;
    public int FixedMipmapLevel
    {
        set
        {
            fixedMipmapLevel = value;
            if (lastMipmapLevel != fixedMipmapLevel)
            {
                lastMipmapLevel = fixedMipmapLevel;
                UpdateMipmapLevel();
            }
        }
        get => fixedMipmapLevel;
    }
    static BoundingSphere GetBoundingSphere(Matrix4x4 mat, Bounds bounds)
    {
        var boundingSphere = new BoundingSphere();
        var worldMin = mat.MultiplyPoint(bounds.min);
        var worldMax = mat.MultiplyPoint(bounds.max);
        boundingSphere.position = (worldMin + worldMax) * 0.5f;
        boundingSphere.radius = (worldMax - worldMin).magnitude * 0.5f;
        return boundingSphere;
    }
    public void UpdateMipmapLevel(DynamicInstancingChild child)
    {
        int[] texIDs = child.Material.GetTexturePropertyNameIDs();
        for (int i = 0; i < texIDs.Length; ++i)
        {
            var texture = child.Material.GetTexture(texIDs[i]) as Texture2D;
            if (texture != null)
            {
                texture.requestedMipmapLevel = fixedMipmapLevel;
            }
        }
    }
    public void UpdateMipmapLevel()
    {
        var children = childDict.Values;
        foreach (var childList in children)
        {
            int childCount = childList.Count;
            if (childCount == 0) continue;
            UpdateMipmapLevel(childList.array[0]);
        }
    }
    public void Join(DynamicInstancingChild child)
    {
        if (child.Mesh == null || child.Material == null) return;
        if (!childDict.ContainsKey(child.ResourceID))
        {
            childDict[child.ResourceID] = new Children();
            UpdateMipmapLevel(child);
        }
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = OnVisibleStateChanged;
        }
        var boundingSphere = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        if (child.childrenID == -1)
        {
            childDict[child.ResourceID].Add(child);
            if (boundingSpheres.Length <= boundingCount)
            {
                Array.Resize(ref boundingSpheres, boundingSpheres.Length * 2);
            }
            if (boundingChildren.Length <= boundingCount)
            {
                Array.Resize(ref boundingChildren, boundingChildren.Length * 2);
            }
            child.boundingID = boundingCount;
            boundingSpheres[boundingCount] = boundingSphere;
            boundingChildren[boundingCount] = child;
            ++boundingCount;
            cullingGroup.SetBoundingSpheres(boundingSpheres);
            cullingGroup.SetBoundingSphereCount(boundingCount);
        }
        else
        {
            boundingSpheres[child.boundingID] = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        }
    }
    public void Quit(DynamicInstancingChild child)
    {
        if (!childDict.ContainsKey(child.ResourceID) || child.childrenID == -1) return;
        childDict[child.ResourceID].RemoveAt(child.childrenID);
        boundingSpheres[child.boundingID] = boundingSpheres[boundingCount - 1];
        boundingChildren[child.boundingID] = boundingChildren[boundingCount - 1];
        boundingChildren[boundingCount - 1].boundingID = child.boundingID;
        --boundingCount;
        if (cullingGroup != null) cullingGroup.SetBoundingSphereCount(boundingCount);
    }
    public void UpdateTransform(DynamicInstancingChild child)
    {
        if (child.childrenID == -1) return;
        boundingSpheres[child.boundingID] = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        childDict[child.ResourceID].UpdateTransform(child.childrenID);
    }
    void Awake()
    {
        if (instance == null) instance = this;
    }
    void Start()
    {
        if (childDict.Count == 0)
        {
            var children = GetComponentsInChildren<DynamicInstancingChild>();
            foreach (var child in children)
            {
                child.enabled = false;
                child.enabled = true;
            }
        }
    }
    void OnVisibleStateChanged(CullingGroupEvent e)
    {
        if (e.index >= boundingCount) return;
        childDict[boundingChildren[e.index].ResourceID].hasVisibleStateChanged = true;
        boundingChildren[e.index].visible = e.isVisible;
    }
    void LateUpdate()
    {
        if (cullingGroup != null)
        {
            if (cullingCamera == null) cullingCamera = Camera.main;
            cullingGroup.targetCamera = cullingCamera;
        }
        if (syncTransform)
        {
            Profiler.BeginSample("Sync Transform");
            for (int i = 0; i < boundingCount; ++i)
            {
                if (boundingChildren[i].syncTransform)
                {
                    UpdateTransform(boundingChildren[i]);
                }
            }
            Profiler.EndSample();
        }
        var children = childDict.Values;
        foreach (var childList in children)
        {
            childList.Draw(enableCulling);
        }
    }
    void OnDestroy()
    {
        if (cullingGroup != null) cullingGroup.Dispose();
        cullingGroup = null;
    }
}
