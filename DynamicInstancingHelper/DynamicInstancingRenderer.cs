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
using System.Threading;

public class InstanceInfo
{
    Mesh mesh;
    public Mesh Mesh => mesh;
    Material material;
    public Material Material => material;
    public ShadowCastingMode shadowCastingMode;
    public bool receiveShadows;
    public int layer;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
    public int childrenID = -1;
    public int boundingID = -1;
    public bool visible = true;
    private long resourceID;
    public long ResourceID => resourceID;
    public InstanceInfo(Mesh mesh, Material material, ShadowCastingMode shadowCastingMode, bool receiveShadows, int layer)
    {
        this.mesh = mesh;
        this.material = material;
        this.shadowCastingMode = shadowCastingMode;
        this.receiveShadows = receiveShadows;
        this.layer = layer;
        resourceID = GetResourceID(mesh, material, shadowCastingMode, receiveShadows, layer);
    }
    public static long GetResourceID(Mesh mesh, Material material, ShadowCastingMode shadowCastingMode, bool receiveShadows, int layer)
    {
        return mesh.GetHashCode() * 17 + material.GetHashCode() * 13 + layer * 11 + (int)shadowCastingMode * 19 + (receiveShadows ? 41 : 0);
    }
}

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
        public DynamicInstancingChild[] array;
        public Matrix4x4[] transforms;
        public Matrix4x4[][] sortedTransforms;
        public bool hasVisibleStateChanged;
        int count;
        public int Count => count;
        int visibleCount;
        public Children(int batchCount)
        {
            array = new DynamicInstancingChild[batchCount];
            transforms = new Matrix4x4[batchCount];
            sortedTransforms = new Matrix4x4[1][];
            sortedTransforms[0] = new Matrix4x4[batchCount];
        }
        public void Add(DynamicInstancingChild child, int batchCount)
        {
            if (array.Length <= count)
            {
                Array.Resize(ref array, array.Length * 2);
                Array.Resize(ref transforms, transforms.Length * 2);
                var originSliceLength = sortedTransforms.Length;
                int sortedBatchs = Mathf.CeilToInt(transforms.Length / (float)batchCount);
                if (sortedTransforms.Length <= sortedBatchs)
                {
                    Array.Resize(ref sortedTransforms, sortedBatchs);
                    for (int i = originSliceLength; i < sortedTransforms.Length; ++i)
                    {
                        sortedTransforms[i] = new Matrix4x4[batchCount];
                    }
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
            Array.Resize(ref sortedTransforms, Mathf.CeilToInt(transforms.Length / (float)batchCount));
            for (int i = 0; i < sortedTransforms.Length; ++i)
            {
                sortedTransforms[i] = new Matrix4x4[batchCount];
            }
            hasVisibleStateChanged = true;
        }

        public void Draw(bool cull, float visibleProbability)
        {
            if (count <= 0) return;
            if (cull && hasVisibleStateChanged)
            {
                visibleCount = 0;
                int sliceIndex = 0;
                int sortedIndex = 0;
                for (int i = 0; i < count; ++i)
                {
                    var p = visibleProbability >= 1f ? 0f : Mathf.Sin((transforms[i].m03 * 17 + transforms[i].m13 * 42 + transforms[i].m23 * 61) * 100000f) * 0.5f + 0.5f;
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
            if (mesh == null) return;
            var material = array[0].Material;
            if (material == null || material.shader == null || !material.shader.isSupported) return;
            var shadowCastingMode = array[0].shadowCastingMode;
            var receiveShadows = array[0].receiveShadows;
            var layer = array[0].layer;
            if (cull)
            {
                int sortedIndex = 0;
                for (int index = 0; index < visibleCount;)
                {
                    var renderCount = Mathf.Min(sortedTransforms[sortedIndex].Length, visibleCount - index);
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
    public class InstanceGroup
    {
        public InstanceInfo[] array;
        public Matrix4x4[] transforms;
        public Matrix4x4[][] sortedTransforms;
        public bool hasVisibleStateChanged;
        int count;
        public int Count => count;
        int visibleCount;
        public InstanceGroup(int batchCount)
        {
            array = new InstanceInfo[batchCount];
            transforms = new Matrix4x4[batchCount];
            sortedTransforms = new Matrix4x4[1][];
            sortedTransforms[0] = new Matrix4x4[batchCount];
        }
        public void Add(InstanceInfo info, int batchCount)
        {
            if (array.Length <= count)
            {
                Array.Resize(ref array, array.Length * 2);
                Array.Resize(ref transforms, transforms.Length * 2);
                var originSliceLength = sortedTransforms.Length;
                int sortedBatchs = Mathf.CeilToInt(transforms.Length / (float)batchCount);
                if (sortedTransforms.Length <= sortedBatchs)
                {
                    Array.Resize(ref sortedTransforms, sortedBatchs);
                    for (int i = originSliceLength; i < sortedTransforms.Length; ++i)
                    {
                        sortedTransforms[i] = new Matrix4x4[batchCount];
                    }
                }
            }
            transforms[count] = Matrix4x4.TRS(info.position, info.rotation, info.scale);
            info.childrenID = count++;
            array[info.childrenID] = info;
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
            transforms[index] = Matrix4x4.TRS(array[index].position, array[index].rotation, array[index].scale);
            hasVisibleStateChanged = true;
        }
        public void UpdateSortedTransform(int batchCount)
        {
            Array.Resize(ref sortedTransforms, Mathf.CeilToInt(transforms.Length / (float)batchCount));
            for (int i = 0; i < sortedTransforms.Length; ++i)
            {
                sortedTransforms[i] = new Matrix4x4[batchCount];
            }
            hasVisibleStateChanged = true;
        }

        public void Draw(bool cull, float visibleProbability)
        {
            if (count <= 0) return;
            if (cull && hasVisibleStateChanged)
            {
                visibleCount = 0;
                int sliceIndex = 0;
                int sortedIndex = 0;
                for (int i = 0; i < count; ++i)
                {
                    var p = visibleProbability >= 1f ? 0f : Mathf.Sin((transforms[i].m03 * 17 + transforms[i].m13 * 42 + transforms[i].m23 * 61) * 100000f) * 0.5f + 0.5f;
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
            if (mesh == null) return;
            var material = array[0].Material;
            if (material == null || material.shader == null || !material.shader.isSupported) return;
            var shadowCastingMode = array[0].shadowCastingMode;
            var receiveShadows = array[0].receiveShadows;
            var layer = array[0].layer;
            if (cull)
            {
                int sortedIndex = 0;
                for (int index = 0; index < visibleCount;)
                {
                    var renderCount = Mathf.Min(sortedTransforms[sortedIndex].Length, visibleCount - index);
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
    Dictionary<long, InstanceGroup> instanceDict = new Dictionary<long, InstanceGroup>();
    Dictionary<long, Children> childDict = new Dictionary<long, Children>();
    BoundingSphere[] boundingInfoSpheres = new BoundingSphere[4000];
    InstanceInfo[] boundingInfos = new InstanceInfo[4000];
    int boundingInfoCount;
    BoundingSphere[] boundingChildSpheres = new BoundingSphere[4000];
    DynamicInstancingChild[] boundingChildren = new DynamicInstancingChild[4000];
    int boundingChildCount;
    Plane[] cullingPlanes = new Plane[6];
    CullJob infoCullJob;
    CullJob childCullJob;
    int[] infoVisibleResults = new int[4000];
    int[] childVisibleResults = new int[4000];
    public bool enableCulling = true;
    public Camera cullingCamera;
    public int syncDelay = 5;
    public int lodThreshold = 100;
    [Range(0, 1)]
    public float lodProbability = 1f;
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
    public void UpdateMipmapLevel(InstanceInfo info)
    {
        int[] texIDs = info.Material.GetTexturePropertyNameIDs();
        for (int i = 0; i < texIDs.Length; ++i)
        {
            var texture = info.Material.GetTexture(texIDs[i]) as Texture2D;
            if (texture != null)
            {
                texture.requestedMipmapLevel = fixedMipmapLevel;
            }
        }
    }
    public void UpdateMipmapLevel()
    {
        var instances = instanceDict.Values;
        foreach (var instanceList in instances)
        {
            int instanceCount = instanceList.Count;
            if (instanceCount == 0) continue;
            UpdateMipmapLevel(instanceList.array[0]);
        }
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
        if (!childDict.ContainsKey(child.ResourceID))
        {
            childDict[child.ResourceID] = new Children(batchCount);
            UpdateMipmapLevel(child);
        }
        if (child.childrenID == -1)
        {
            childDict[child.ResourceID].Add(child, batchCount);
            if (boundingChildSpheres.Length <= boundingChildCount)
            {
                Array.Resize(ref boundingChildSpheres, boundingChildSpheres.Length * 2);
                childCullJob.boundingSpheres.Dispose();
                childCullJob.boundingSpheres = new NativeArray<BoundingSphere>(boundingChildSpheres.Length, Allocator.Persistent);
                childCullJob.visibleResults.Dispose();
                childCullJob.visibleResults = new NativeArray<int>(boundingChildSpheres.Length, Allocator.Persistent);
                Array.Resize(ref childVisibleResults, boundingChildSpheres.Length);
            }
            if (boundingChildren.Length <= boundingChildCount)
            {
                Array.Resize(ref boundingChildren, boundingChildren.Length * 2);
            }
            child.boundingID = boundingChildCount;
            boundingChildSpheres[boundingChildCount] = GetBoundingSphere(childDict[child.ResourceID].transforms[child.childrenID], child.Mesh.bounds);
            boundingChildren[boundingChildCount] = child;
            ++boundingChildCount;
        }
        else
        {
            boundingChildSpheres[child.boundingID] = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        }
    }
    public void Join(InstanceInfo info)
    {
        if (!instanceDict.ContainsKey(info.ResourceID))
        {
            instanceDict[info.ResourceID] = new InstanceGroup(batchCount);
            UpdateMipmapLevel(info);
        }
        if (info.childrenID == -1)
        {
            instanceDict[info.ResourceID].Add(info, batchCount);
            if (boundingInfoSpheres.Length <= boundingInfoCount)
            {
                Array.Resize(ref boundingInfoSpheres, boundingInfoSpheres.Length * 2);
                infoCullJob.boundingSpheres.Dispose();
                infoCullJob.boundingSpheres = new NativeArray<BoundingSphere>(boundingInfoSpheres.Length, Allocator.Persistent);
                infoCullJob.visibleResults.Dispose();
                infoCullJob.visibleResults = new NativeArray<int>(boundingInfoSpheres.Length, Allocator.Persistent);
                Array.Resize(ref infoVisibleResults, boundingInfoSpheres.Length);
            }
            if (boundingInfos.Length <= boundingInfoCount)
            {
                Array.Resize(ref boundingInfos, boundingInfos.Length * 2);
            }
            info.boundingID = boundingInfoCount;
            boundingInfoSpheres[boundingInfoCount] = GetBoundingSphere(instanceDict[info.ResourceID].transforms[info.childrenID], info.Mesh.bounds);
            boundingInfos[boundingInfoCount] = info;
            ++boundingInfoCount;
        }
        else
        {
            var matrix = Matrix4x4.TRS(info.position, info.rotation, info.scale);
            boundingInfoSpheres[info.boundingID] = GetBoundingSphere(matrix, info.Mesh.bounds);
        }   
    }
    public void Quit(DynamicInstancingChild child)
    {
        if (child.childrenID == -1) return;
        childDict[child.ResourceID].RemoveAt(child.childrenID);
        boundingChildSpheres[child.boundingID] = boundingChildSpheres[boundingChildCount - 1];
        boundingChildren[child.boundingID] = boundingChildren[boundingChildCount - 1];
        boundingChildren[child.boundingID].boundingID = child.boundingID;
        --boundingChildCount;
    }
    public void Quit(InstanceInfo info)
    {
        if (info.childrenID == -1) return;
        instanceDict[info.ResourceID].RemoveAt(info.childrenID);
        boundingInfoSpheres[info.boundingID] = boundingInfoSpheres[boundingInfoCount - 1];
        boundingInfos[info.boundingID] = boundingInfos[boundingInfoCount - 1];
        boundingInfos[info.boundingID].boundingID = info.boundingID;
        --boundingInfoCount;
    }
    public void UpdateTransform(DynamicInstancingChild child)
    {
        if (child.childrenID == -1 || child.Mesh == null) return;
        boundingChildSpheres[child.boundingID] = GetBoundingSphere(child.transform.localToWorldMatrix, child.Mesh.bounds);
        childDict[child.ResourceID].UpdateTransform(child.childrenID);
    }
    public void UpdateTransform(InstanceInfo info)
    {
        if (info.childrenID == -1 || info.Mesh == null) return;
        var matrix = Matrix4x4.TRS(info.position, info.rotation, info.scale);
        boundingInfoSpheres[info.boundingID] = GetBoundingSphere(matrix, info.Mesh.bounds);
        instanceDict[info.ResourceID].UpdateTransform(info.childrenID);
    }
    public Matrix4x4 GetCurrentMatrix(DynamicInstancingChild child)
    {
        if (child.childrenID == -1) return new Matrix4x4();
        return childDict[child.ResourceID].transforms[child.childrenID];
    }
    public BoundingSphere GetCurrentBoundingSphere(DynamicInstancingChild child)
    {
        if (!childDict.ContainsKey(child.ResourceID) || child.childrenID == -1) return new BoundingSphere();
        return boundingChildSpheres[child.boundingID];
    }
    public void CullSync()
    {
        if (cullingCamera == null) return;
        if (cullingCamera.TryGetCullingParameters(out var cullingParameters))
        {
            for (int i = 0; i < cullingPlanes.Length; ++i)
            {
                cullingPlanes[i] = cullingParameters.GetCullingPlane(i);
            }
            infoCullJob.cullingPlanes.CopyFrom(cullingPlanes);
            infoCullJob.boundingSpheres.CopyFrom(boundingInfoSpheres);
            var handle = infoCullJob.Schedule(boundingInfoCount, 64);
            handle.Complete();
            infoCullJob.visibleResults.CopyTo(infoVisibleResults);
            for (int i = 0; i < boundingInfoCount; ++i)
            {
                var visible = infoVisibleResults[i] == 1;
                if (boundingInfos[i].visible != visible)
                {
                    instanceDict[boundingInfos[i].ResourceID].hasVisibleStateChanged = true;
                    boundingInfos[i].visible = visible;
                }
            }
            childCullJob.cullingPlanes.CopyFrom(cullingPlanes);
            childCullJob.boundingSpheres.CopyFrom(boundingChildSpheres);
            handle = childCullJob.Schedule(boundingChildCount, 64);
            handle.Complete();
            childCullJob.visibleResults.CopyTo(childVisibleResults);
            for (int i = 0; i < boundingChildCount; ++i)
            {
                var visible = childVisibleResults[i] == 1;
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
        infoCullJob = new CullJob();
        infoCullJob.cullingPlanes = new NativeArray<Plane>(6, Allocator.Persistent);
        infoCullJob.boundingSpheres = new NativeArray<BoundingSphere>(boundingInfoSpheres.Length, Allocator.Persistent);
        infoCullJob.visibleResults = new NativeArray<int>(infoVisibleResults.Length, Allocator.Persistent);
        childCullJob = new CullJob();
        childCullJob.cullingPlanes = new NativeArray<Plane>(6, Allocator.Persistent);
        childCullJob.boundingSpheres = new NativeArray<BoundingSphere>(boundingChildSpheres.Length, Allocator.Persistent);
        childCullJob.visibleResults = new NativeArray<int>(childVisibleResults.Length, Allocator.Persistent);
    }
    void LateUpdate()
    {
        var infos = instanceDict.Values;
        var children = childDict.Values;
        if (cullingCamera == null) cullingCamera = Camera.main;
        if (lastProbability != lodProbability)
        {
            lastProbability = lodProbability;
            foreach (var infoList in infos)
            {
                infoList.hasVisibleStateChanged = true;
            }
            foreach (var childList in children)
            {
                childList.hasVisibleStateChanged = true;
            }
        }
        if (lastBatchCount != batchCount)
        {
            lastBatchCount = batchCount;
            foreach (var infoList in infos)
            {
                infoList.UpdateSortedTransform(batchCount);
            }
            foreach (var childList in children)
            {
                childList.UpdateSortedTransform(batchCount);
            }
        }
        if (enableCulling) CullSync();
        Profiler.BeginSample("Sync Transform");
        for (int i = 0; i < boundingChildCount; ++i)
        {
            if (boundingChildren[i].syncDelay > 0 || boundingChildren[i].syncTransform)
            {
                boundingChildren[i].syncDelay = Mathf.Max(0, boundingChildren[i].syncDelay - 1);
                UpdateTransform(boundingChildren[i]);
            }
        }
        Profiler.EndSample();
        var probability = Shader.globalMaximumLOD <= lodThreshold ? lodProbability : 1f;
        foreach (var infoList in infos)
        {
            infoList.Draw(enableCulling, probability);
        }
        foreach (var childList in children)
        {
            childList.Draw(enableCulling, probability);
        }
    }
    void OnDestroy()
    {
        if (infoCullJob.cullingPlanes.IsCreated) infoCullJob.cullingPlanes.Dispose();
        if (infoCullJob.boundingSpheres.IsCreated) infoCullJob.boundingSpheres.Dispose();
        if (infoCullJob.visibleResults.IsCreated) infoCullJob.visibleResults.Dispose();
        if (childCullJob.cullingPlanes.IsCreated) childCullJob.cullingPlanes.Dispose();
        if (childCullJob.boundingSpheres.IsCreated) childCullJob.boundingSpheres.Dispose();
        if (childCullJob.visibleResults.IsCreated) childCullJob.visibleResults.Dispose();
    }
}
