using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Burst;

[DisallowMultipleComponent]
public class DynamicInstancingRenderer : MonoBehaviour
{
    static DynamicInstancingRenderer instance;
    public static DynamicInstancingRenderer Instance => instance;
    [BurstCompile]
    struct CullJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Plane> cullingPlanes;
        [ReadOnly]
        public NativeArray<BoundingSphere> boundingSpheres;
        [WriteOnly]
        public NativeArray<int> visibleResults;
        public void Execute(int i)
        {
            var position = boundingSpheres[i].position;
            var radius = boundingSpheres[i].radius;
            visibleResults[i] = 1;
            for (int k = 0; k < cullingPlanes.Length; ++k)
            {
                var distance = Vector3.Dot(position, cullingPlanes[k].normal) + cullingPlanes[k].distance;
                if (distance < -radius)
                {
                    visibleResults[i] = 0;
                    break;
                }
            }
        }
    }
    class Children
    {
        public DynamicInstancingChild[] array = new DynamicInstancingChild[1];
        public Matrix4x4[] transforms = new Matrix4x4[1];
        public Matrix4x4[][] sortedTransforms = new Matrix4x4[1][];
        public bool hasVisibleStateChanged;
        int count;
        public int Count => count;
        int visibleCount;
        public Children(int batchCount)
        {
            sortedTransforms[0] = new Matrix4x4[batchCount];
        }
        public void Add(DynamicInstancingChild child, int batchCount)
        {
            if (array.Length <= count)
            {
                Array.Resize(ref array, array.Length * 2);
                Array.Resize(ref transforms, transforms.Length * 2);
                var originSliceLength = sortedTransforms.Length;
                Array.Resize(ref sortedTransforms, transforms.Length / batchCount + 1);
                for (int i = originSliceLength; i < sortedTransforms.Length; ++i)
                {
                    sortedTransforms[i] = new Matrix4x4[batchCount];
                }
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
        public void UpdateSortedTransform(int batchCount)
        {
            Array.Resize(ref sortedTransforms, transforms.Length / batchCount + 1);
            for (int i = 0; i < sortedTransforms.Length; ++i)
            {
                sortedTransforms[i] = new Matrix4x4[batchCount];
            }
            hasVisibleStateChanged = true;
        }
        public void Draw(bool cull, float visibleProbability, int batchCount)
        {
            if (count <= 0) return;
            if (cull && hasVisibleStateChanged)
            {
                visibleCount = 0;
                int sliceIndex = 0;
                int sortedIndex = 0;
                for (int i = 0; i < count; ++i)
                {
                    var p = visibleProbability >= 1 ? 0 : Mathf.Sin((transforms[i].m03 * 17 + transforms[i].m13 * 42 + transforms[i].m23 * 61) * 100000f) * 0.5f + 0.5f;
                    if (array[i].visible && p < visibleProbability)
                    {
                        sortedTransforms[sliceIndex][sortedIndex].m00 = transforms[i].m00;
                        sortedTransforms[sliceIndex][sortedIndex].m01 = transforms[i].m01;
                        sortedTransforms[sliceIndex][sortedIndex].m02 = transforms[i].m02;
                        sortedTransforms[sliceIndex][sortedIndex].m03 = transforms[i].m03;
                        sortedTransforms[sliceIndex][sortedIndex].m10 = transforms[i].m10;
                        sortedTransforms[sliceIndex][sortedIndex].m11 = transforms[i].m11;
                        sortedTransforms[sliceIndex][sortedIndex].m12 = transforms[i].m12;
                        sortedTransforms[sliceIndex][sortedIndex].m13 = transforms[i].m13;
                        sortedTransforms[sliceIndex][sortedIndex].m20 = transforms[i].m20;
                        sortedTransforms[sliceIndex][sortedIndex].m21 = transforms[i].m21;
                        sortedTransforms[sliceIndex][sortedIndex].m22 = transforms[i].m22;
                        sortedTransforms[sliceIndex][sortedIndex].m23 = transforms[i].m23;
                        sortedTransforms[sliceIndex][sortedIndex].m30 = transforms[i].m30;
                        sortedTransforms[sliceIndex][sortedIndex].m31 = transforms[i].m31;
                        sortedTransforms[sliceIndex][sortedIndex].m32 = transforms[i].m32;
                        sortedTransforms[sliceIndex][sortedIndex].m33 = transforms[i].m33;
                        sortedIndex++;
                        if (sortedIndex >= sortedTransforms[sliceIndex].Length)
                        {
                            sortedIndex = 0;
                            sliceIndex++;
                        }
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
            if (cull)
            {
                int sortedIndex = 0;
                for (int index = 0; index < visibleCount;)
                {
                    var renderCount = Mathf.Min(sortedTransforms[sortedIndex].Length, visibleCount);
                    Graphics.DrawMeshInstanced(mesh, 0, material, sortedTransforms[sortedIndex], renderCount, null, shadowCastingMode, receiveShadows, layer);
                    sortedIndex++;
                    index += renderCount;
                }
            }
            else
            {
                Graphics.DrawMeshInstanced(mesh, 0, material, transforms, count, null, shadowCastingMode, receiveShadows, layer);
            }
        }
    }
    Dictionary<long, Children> childDict = new Dictionary<long, Children>();
    BoundingSphere[] boundingSpheres = new BoundingSphere[1];
    DynamicInstancingChild[] boundingChildren = new DynamicInstancingChild[1];
    int boundingCount;
    CullJob cullJob;
    int[] visibleResults = new int[1];
    public bool enableCulling = true;
    public Camera cullingCamera;
    public bool syncTransform;
    public int lodThreshold = 100;
    [Range(0, 1)]
    public float visibleProbability = 1f;
    float lastProbability = 1f;
    public int batchCount = 63;
    int lastBatchCount = 63;
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
            childDict[child.ResourceID] = new Children(batchCount);
            UpdateMipmapLevel(child);
        }
        var boundingSphere = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        if (child.childrenID == -1)
        {
            childDict[child.ResourceID].Add(child, batchCount);
            if (boundingSpheres.Length <= boundingCount)
            {
                Array.Resize(ref boundingSpheres, boundingSpheres.Length * 2);
                cullJob.boundingSpheres.Dispose();
                cullJob.boundingSpheres = new NativeArray<BoundingSphere>(boundingSpheres.Length, Allocator.Persistent);
                cullJob.visibleResults.Dispose();
                cullJob.visibleResults = new NativeArray<int>(boundingSpheres.Length, Allocator.Persistent);
                Array.Resize(ref visibleResults, boundingSpheres.Length);
            }
            if (boundingChildren.Length <= boundingCount)
            {
                Array.Resize(ref boundingChildren, boundingChildren.Length * 2);
            }
            child.boundingID = boundingCount;
            boundingSpheres[boundingCount] = boundingSphere;
            boundingChildren[boundingCount] = child;
            ++boundingCount;
        }
        else
        {
            boundingSpheres[child.boundingID] = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        }
    }
    public void Quit(DynamicInstancingChild child)
    {
        if (child.childrenID == -1) return;
        childDict[child.ResourceID].RemoveAt(child.childrenID);
        boundingSpheres[child.boundingID] = boundingSpheres[boundingCount - 1];
        boundingChildren[child.boundingID] = boundingChildren[boundingCount - 1];
        boundingChildren[child.boundingID].boundingID = child.boundingID;
        --boundingCount;
    }
    public void UpdateTransform(DynamicInstancingChild child)
    {
        if (child.childrenID == -1) return;
        boundingSpheres[child.boundingID] = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        childDict[child.ResourceID].UpdateTransform(child.childrenID);
    }
    public Matrix4x4 GetCurrentMatrix(DynamicInstancingChild child)
    {
        if (child.childrenID == -1) return new Matrix4x4();
        return childDict[child.ResourceID].transforms[child.childrenID];
    }
    public BoundingSphere GetCurrentBoundingSphere(DynamicInstancingChild child)
    {
        if (!childDict.ContainsKey(child.ResourceID) || child.childrenID == -1) return new BoundingSphere();
        return boundingSpheres[child.boundingID];
    }
    public void CullSync()
    {
        if (cullingCamera == null) return;
        if (cullingCamera.TryGetCullingParameters(out var cullingParameters))
        {
            var planes = new Plane[6];
            for (int i = 0; i < 6; ++i)
            {
                planes[i] = cullingParameters.GetCullingPlane(i);
            }
            cullJob.cullingPlanes.CopyFrom(planes);
            cullJob.boundingSpheres.CopyFrom(boundingSpheres);
            var handle = cullJob.Schedule(boundingCount, 64);
            handle.Complete();
            cullJob.visibleResults.CopyTo(visibleResults);
            for (int i = 0; i < boundingCount; ++i)
            {
                var visible = visibleResults[i] == 1;
                if (boundingChildren[i].visible != visible)
                {
                    childDict[boundingChildren[i].ResourceID].hasVisibleStateChanged = true;
                    boundingChildren[i].visible = visible;
                }
            }
        }
    }
    void Awake()
    {
        if (instance == null) instance = this;
        cullJob = new CullJob();
        cullJob.cullingPlanes = new NativeArray<Plane>(6, Allocator.Persistent);
        cullJob.boundingSpheres = new NativeArray<BoundingSphere>(1, Allocator.Persistent);
        cullJob.visibleResults = new NativeArray<int>(1, Allocator.Persistent);
    }
    void LateUpdate()
    {
        var children = childDict.Values;
        if (cullingCamera == null) cullingCamera = Camera.main;
        if (lastProbability != visibleProbability)
        {
            lastProbability = visibleProbability;
            foreach (var childList in children)
            {
                childList.hasVisibleStateChanged = true;
            }
        }
        if (lastBatchCount != batchCount)
        {
            lastBatchCount = batchCount;
            foreach (var childList in children)
            {
                childList.UpdateSortedTransform(batchCount);
            }
        }
        if (enableCulling) CullSync();
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
        foreach (var childList in children)
        {
            childList.Draw(enableCulling, visibleProbability, batchCount);
        }
    }
    void OnDestroy()
    {
        if (cullJob.cullingPlanes.IsCreated) cullJob.cullingPlanes.Dispose();
        if (cullJob.boundingSpheres.IsCreated) cullJob.boundingSpheres.Dispose();
        if (cullJob.visibleResults.IsCreated) cullJob.visibleResults.Dispose();
    }
}
