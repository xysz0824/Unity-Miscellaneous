using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Burst;

public class CrowdRenderer : MonoBehaviour
{
    public struct InstanceDataArray
    {
        public int[] CullingBoxIndex;
        public Matrix4x4[] Matrices;
        public Matrix4x4[] SortedMatrices;
        public float[] UVIndices;
        public float[] SortedUVIndices;
        public int[] AnimationMatrixStartIndices;
        public int[] AnimationMatrixCountArray;
        public float[] AnimationMatrixIndices;
        public float[] SortedAnimationMatrixIndices;
        public InstanceDataArray(int size)
        {
            CullingBoxIndex = new int[size];
            Matrices = new Matrix4x4[size];
            SortedMatrices = new Matrix4x4[size >= 1023 ? 1023 : size];
            UVIndices = new float[size];
            SortedUVIndices = new float[size >= 1023 ? 1023 : size];
            AnimationMatrixStartIndices = new int[size];
            AnimationMatrixCountArray = new int[size];
            AnimationMatrixIndices = new float[size];
            SortedAnimationMatrixIndices = new float[size >= 1023 ? 1023 : size];
        }
    }
    static int uvIndexID;
    static int uvIndexArrayID;
    static int animationMatrixIndexArrayID;
    RoughCullJob roughCullJob;
    DetailCullJob detailCullJob;
    LODJob lodJob;
    InstanceDataArray data;
    BoxCollider[] cullingBoxColliders;
    Matrix4x4[] cullingBoxMatrices;
    Vector3[] cullingBoxSizes;
    Plane[] frustumPlanes;
    CommandBuffer commandBuffer;
    MaterialPropertyBlock propertyBlock;
    Texture2D bakedAnimationTexture;
    float animatedTime;
    int totalRenderCount;
    [Serializable]
    public class LODRenderInfo
    {
        [NonSerialized]
        public List<Renderer> Objects;
        public Mesh Mesh;
        public Mesh BakedMesh;
        public Material Material;
        public Vector2 LODDistance;
    }
    public Camera TargetCamera;
    public Vector3 BoundingCenter;
    public float BoundingRadius;
    public LODRenderInfo[] LODRenderInfos;
    public BakedAnimationSheet BakedAnimationSheet;
    public TextAsset BakedAnimations;
    public bool SyncTransform;
    private void Awake()
    {
        uvIndexID = Shader.PropertyToID("_UVIndex");
        uvIndexArrayID = Shader.PropertyToID("_UVIndexArray");
        animationMatrixIndexArrayID = Shader.PropertyToID("_AnimationMatrixIndexArray");
    }
    private void Start()
    {
        commandBuffer = new CommandBuffer();
        TargetCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);

        var cullingBoxes = GetComponentsInChildren<CullingBox>();
        cullingBoxColliders = new BoxCollider[cullingBoxes.Length];
        cullingBoxMatrices = new Matrix4x4[cullingBoxes.Length];
        cullingBoxSizes = new Vector3[cullingBoxes.Length];
        for (int i = 0; i < cullingBoxes.Length; ++i)
        {
            cullingBoxColliders[i] = cullingBoxes[i].GetComponent<BoxCollider>();
            cullingBoxColliders[i].transform.localPosition += cullingBoxColliders[i].center;
            cullingBoxMatrices[i] = cullingBoxColliders[i].transform.localToWorldMatrix;
            cullingBoxColliders[i].transform.localPosition -= cullingBoxColliders[i].center;
            cullingBoxSizes[i] = cullingBoxColliders[i].size;
        }

        foreach (var info in LODRenderInfos)
        {
            info.Objects = new List<Renderer>();
        }
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            Mesh mesh = null;
            if (renderer is MeshRenderer)
            {
                mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                mesh = (renderer as SkinnedMeshRenderer).sharedMesh;
            }
            LODRenderInfo info = Array.Find(LODRenderInfos, (item) => { return item.Mesh == mesh; });
            if (info != null && renderer.gameObject.activeInHierarchy)
            {
                info.Objects.Add(renderer);
                renderer.enabled = false;
            }
        }
        int maxRenderCount = int.MinValue;
        LODRenderInfo renderInfo = LODRenderInfos[0];
        if (maxRenderCount < renderInfo.Objects.Count)
        {
            maxRenderCount = renderInfo.Objects.Count;
        }
        data = new InstanceDataArray(renderInfo.Objects.Count);
        for (int i = 0; i < renderInfo.Objects.Count; ++i)
        {
            var cullingBox = renderInfo.Objects[i].transform.parent.GetComponent<CullingBox>();
            if (cullingBox == null)
            {
                cullingBox = renderInfo.Objects[i].transform.parent.parent.GetComponent<CullingBox>();
            }
            if (cullingBox != null)
            {
                data.CullingBoxIndex[i] = Array.IndexOf(cullingBoxes, cullingBox) + 1;
            }
            if (renderInfo.Objects[i] is SkinnedMeshRenderer)
            {
                renderInfo.Objects[i].transform.localPosition = Vector3.zero;
                renderInfo.Objects[i].transform.localRotation = Quaternion.identity;
                renderInfo.Objects[i].transform.localScale = Vector3.one;
            }
            data.Matrices[i] = renderInfo.Objects[i].localToWorldMatrix;
            var material = renderInfo.Objects[i].sharedMaterial;
            data.UVIndices[i] = material.GetFloat(uvIndexID);
            if (BakedAnimationSheet != null && BakedAnimations != null)
            {
                var animation = renderInfo.Objects[i].transform.parent.GetComponent<Animation>();
                animation.enabled = false;
                var bakedClip = Array.Find(BakedAnimationSheet.Clips, (item) => { return item.Clip == animation.clip; });
                if (bakedClip != null)
                {
                    data.AnimationMatrixStartIndices[i] = bakedClip.MatrixStartIndex;
                    data.AnimationMatrixCountArray[i] = bakedClip.MatrixCount;
                }
                else
                {
                    data.AnimationMatrixCountArray[i] = 1;
                }
            }
        }
        roughCullJob = new RoughCullJob();
        roughCullJob.FrustumPlanes = new NativeArray<Plane>(6, Allocator.Persistent);
        roughCullJob.CullingBoxMatrices = new NativeArray<Matrix4x4>(cullingBoxColliders.Length, Allocator.Persistent);
        roughCullJob.CullingBoxSizes = new NativeArray<Vector3>(cullingBoxColliders.Length, Allocator.Persistent);
        roughCullJob.RoughCullResult = new NativeArray<int>(cullingBoxColliders.Length, Allocator.Persistent);
        frustumPlanes = new Plane[6];
        detailCullJob = new DetailCullJob();
        detailCullJob.Matrices = new NativeArray<Matrix4x4>(maxRenderCount, Allocator.Persistent);
        detailCullJob.CullingBoxIndex = new NativeArray<int>(maxRenderCount, Allocator.Persistent);
        detailCullJob.CullResult = new NativeArray<int>(maxRenderCount, Allocator.Persistent);
        lodJob = new LODJob();

        propertyBlock = new MaterialPropertyBlock();
        if (BakedAnimationSheet != null && BakedAnimations != null)
        {
            bakedAnimationTexture = new Texture2D(BakedAnimationSheet.TextureWidth, BakedAnimationSheet.TextureHeight, TextureFormat.RGBAHalf, false, true);
            bakedAnimationTexture.filterMode = FilterMode.Point;
            bakedAnimationTexture.LoadRawTextureData(BakedAnimations.bytes);
            bakedAnimationTexture.Apply(false, true);
            propertyBlock.SetFloat("_EnableSkinning", 1);
            propertyBlock.SetTexture("_BakedAnimationTexture", bakedAnimationTexture);
            propertyBlock.SetVector("_BakedAnimationTexture_SizeInfo", new Vector4(BakedAnimationSheet.TextureWidth, BakedAnimationSheet.TextureHeight,
                BakedAnimationSheet.BoneCount, 3));
        }

        var position = new Vector3[] { Vector3.zero };
        var lightProbes = new SphericalHarmonicsL2[1];
        var occlusionProbes = new Vector4[1];
        LightProbes.CalculateInterpolatedLightAndOcclusionProbes(position, lightProbes, occlusionProbes);
        propertyBlock.CopySHCoefficientArraysFrom(lightProbes);
        propertyBlock.CopyProbeOcclusionArrayFrom(occlusionProbes);
        var specCube = ReflectionProbe.defaultTexture;
        propertyBlock.SetTexture("unity_SpecCube0", specCube);
    }
    private void OnDestroy()
    {
        if (roughCullJob.FrustumPlanes != null)
        {
            roughCullJob.FrustumPlanes.Dispose();
        }
        if (roughCullJob.CullingBoxMatrices != null)
        {
            roughCullJob.CullingBoxMatrices.Dispose();
        }
        if (roughCullJob.CullingBoxSizes != null)
        {
            roughCullJob.CullingBoxSizes.Dispose();
        }
        if (roughCullJob.RoughCullResult != null)
        {
            roughCullJob.RoughCullResult.Dispose();
        }
        if (detailCullJob.Matrices != null)
        {
            detailCullJob.Matrices.Dispose();
        }
        if (detailCullJob.CullingBoxIndex != null)
        {
            detailCullJob.CullingBoxIndex.Dispose();
        }
        if (detailCullJob.CullResult != null)
        {
            detailCullJob.CullResult.Dispose();
        }
    }
    private void Update()
    {
        int frame = (int)(animatedTime * BakedAnimationSheet.FrameRate);
        commandBuffer.Clear();

        if (SyncTransform)
        {
            for (int i = 0; i < cullingBoxColliders.Length; ++i)
            {
                cullingBoxMatrices[i] = cullingBoxColliders[i].transform.localToWorldMatrix;
                cullingBoxSizes[i] = cullingBoxColliders[i].size;
            }
        }

        Matrix4x4 worldToProjectionMatrix = TargetCamera.projectionMatrix * TargetCamera.worldToCameraMatrix;

        GeometryUtility.CalculateFrustumPlanes(worldToProjectionMatrix, frustumPlanes);
        roughCullJob.FrustumPlanes.CopyFrom(frustumPlanes);
        roughCullJob.CullingBoxMatrices.CopyFrom(cullingBoxMatrices);
        roughCullJob.CullingBoxSizes.CopyFrom(cullingBoxSizes);
        roughCullJob.Execute(0);
        var roughCullJobHandle = roughCullJob.Schedule(cullingBoxMatrices.Length, 8);
        roughCullJobHandle.Complete();

        detailCullJob.CameraMatrix = worldToProjectionMatrix;
        detailCullJob.CameraUp = TargetCamera.transform.up;
        detailCullJob.BoundingCenter = BoundingCenter;
        detailCullJob.BoundingRadius = BoundingRadius;
        detailCullJob.Matrices.CopyFrom(data.Matrices);
        detailCullJob.CullingBoxIndex.CopyFrom(data.CullingBoxIndex);
        detailCullJob.RoughCullResult = roughCullJob.RoughCullResult;
        var cullJobHandle = detailCullJob.Schedule(data.Matrices.Length, 16);
        cullJobHandle.Complete();

        bool synced = false;
        totalRenderCount = 0;
        for (int i = 0; i < LODRenderInfos.Length; ++i)
        {
            LODRenderInfo info = LODRenderInfos[i];

            lodJob.CameraPos = TargetCamera.transform.position;
            lodJob.LODIndex = i + 1;
            lodJob.LODDistance = info.LODDistance;
            lodJob.Matrices = detailCullJob.Matrices;
            lodJob.CullResult = detailCullJob.CullResult;
            var lodJobHandle = lodJob.Schedule(data.Matrices.Length, 32);
            lodJobHandle.Complete();

            var renderCount = 0;
            for (int k = 0; k < data.Matrices.Length; ++k)
            {
                if (renderCount >= 1023)
                {
                    break;
                }
                if (lodJob.CullResult[k] == lodJob.LODIndex)
                {
                    if (SyncTransform && !synced)
                    {
                        data.Matrices[k] = info.Objects[k].localToWorldMatrix;
                    }
                    data.SortedMatrices[renderCount] = data.Matrices[k];
                    data.SortedUVIndices[renderCount] = data.UVIndices[k];
                    data.SortedAnimationMatrixIndices[renderCount] = data.AnimationMatrixStartIndices[k];
                    data.SortedAnimationMatrixIndices[renderCount] += (frame * BakedAnimationSheet.BoneCount) % data.AnimationMatrixCountArray[k];
                    renderCount++;
                }
            }
            totalRenderCount += renderCount;
            synced = true;
            if (data.SortedUVIndices.Length > 0)
            {
                propertyBlock.SetFloatArray(uvIndexArrayID, data.SortedUVIndices);
            }
            if (data.SortedAnimationMatrixIndices.Length > 0)
            {
                propertyBlock.SetFloatArray(animationMatrixIndexArrayID, data.SortedAnimationMatrixIndices);
            }
            var mesh = info.BakedMesh != null ? info.BakedMesh : info.Mesh;
            commandBuffer.DrawMeshInstanced(mesh, 0, info.Material, 0, data.SortedMatrices, renderCount, propertyBlock);
        }
        animatedTime += Time.deltaTime;
    }
    [BurstCompile(CompileSynchronously = true)]
    struct RoughCullJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Plane> FrustumPlanes;
        [ReadOnly]
        public NativeArray<Matrix4x4> CullingBoxMatrices;
        [ReadOnly]
        public NativeArray<Vector3> CullingBoxSizes;
        public NativeArray<int> RoughCullResult;
        public void Execute(int index)
        {
            Vector3 halfSize = CullingBoxSizes[index] * 0.5f;
            int outsideCount = 0;
            var testPoint = new Vector3(0, 0, 0);
            for (int i = 0;i < 8; ++i)
            {
                switch (i)
                {
                    case 0:
                        testPoint = new Vector3(-halfSize.x, halfSize.y, halfSize.z);
                        break;
                    case 1:
                        testPoint = new Vector3(halfSize.x, -halfSize.y, halfSize.z);
                        break;
                    case 2:
                        testPoint = new Vector3(halfSize.x, halfSize.y, -halfSize.z);
                        break;
                    case 3:
                        testPoint = new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
                        break;
                    case 4:
                        testPoint = new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
                        break;
                    case 5:
                        testPoint = new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
                        break;
                    case 6:
                        testPoint = new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
                        break;
                    case 7:
                        testPoint = new Vector3(halfSize.x, halfSize.y, halfSize.z);
                        break;
                }
                Vector4 homoCoord = CullingBoxMatrices[index] * new Vector4(testPoint.x, testPoint.y, testPoint.z, 1);
                testPoint = homoCoord;
                for (int k = 0; k < 6; ++k)
                {
                    if (FrustumPlanes[k].GetDistanceToPoint(testPoint) < 0)
                    {
                        outsideCount++;
                        break;
                    }
                }
            }
            for (int i = 0; i < 6; ++i)
            {
                float side = 0;
                for (int k = 0; k < 8; ++k)
                {
                    switch (k)
                    {
                        case 0:
                            testPoint = new Vector3(-halfSize.x, halfSize.y, halfSize.z);
                            break;
                        case 1:
                            testPoint = new Vector3(halfSize.x, -halfSize.y, halfSize.z);
                            break;
                        case 2:
                            testPoint = new Vector3(halfSize.x, halfSize.y, -halfSize.z);
                            break;
                        case 3:
                            testPoint = new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
                            break;
                        case 4:
                            testPoint = new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
                            break;
                        case 5:
                            testPoint = new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
                            break;
                        case 6:
                            testPoint = new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
                            break;
                        case 7:
                            testPoint = new Vector3(halfSize.x, halfSize.y, halfSize.z);
                            break;
                    }
                    Vector4 homoCoord = CullingBoxMatrices[index] * new Vector4(testPoint.x, testPoint.y, testPoint.z, 1);
                    testPoint = homoCoord;
                    if (k == 0)
                    {
                        side = FrustumPlanes[i].GetDistanceToPoint(testPoint);
                    }
                    else if (FrustumPlanes[i].GetDistanceToPoint(testPoint) * side < 0)
                    {
                        RoughCullResult[index] = 0;
                        return;
                    }
                }
            }
            RoughCullResult[index] = outsideCount == 8 ? 1 : 0;
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    struct DetailCullJob : IJobParallelFor
    {
        [ReadOnly]
        public Matrix4x4 CameraMatrix;
        [ReadOnly]
        public Vector3 CameraUp;
        [ReadOnly]
        public NativeArray<Matrix4x4> Matrices;
        [ReadOnly]
        public Vector3 BoundingCenter;
        [ReadOnly]
        public float BoundingRadius;
        [ReadOnly]
        public NativeArray<int> CullingBoxIndex;
        [ReadOnly]
        public NativeArray<int> RoughCullResult;
        public NativeArray<int> CullResult;
        public void Execute(int index)
        {
            int cullingBoxIndex = CullingBoxIndex[index] - 1;
            if ((cullingBoxIndex >= 0 && cullingBoxIndex < RoughCullResult.Length) && RoughCullResult[cullingBoxIndex] > 0)
            {
                CullResult[index] = int.MaxValue;
                return;
            }
            var matrix = Matrices[index];
            Vector3 position = new Vector3(matrix.m03, matrix.m13, matrix.m23) + BoundingCenter;
            Vector4 testCenter = CameraMatrix * new Vector4(position.x, position.y, position.z, 1);
            testCenter /= testCenter.w;
            if (testCenter.x < -1 || testCenter.x > 1 || testCenter.y < -1 || testCenter.y > 1)
            {
                CullResult[index] = int.MaxValue;
                return;
            }
            Vector4 testUp = (position + CameraUp * BoundingRadius);
            testUp.w = 1;
            testUp = CameraMatrix * testUp;
            testUp /= testUp.w;
            float viewportRaidus = (testUp - testCenter).y;
            Vector3 testPoint = testCenter + new Vector4(viewportRaidus, viewportRaidus, 0, 0);
            if (testPoint.x < -1 || testPoint.x > 1 || testPoint.y < -1 || testPoint.y > 1)
            {
                CullResult[index] = int.MaxValue;
                return;
            }
            testPoint = testCenter + new Vector4(-viewportRaidus, viewportRaidus, 0, 0);
            if (testPoint.x < -1 || testPoint.x > 1 || testPoint.y < -1 || testPoint.y > 1)
            {
                CullResult[index] = int.MaxValue;
                return;
            }
            testPoint = testCenter + new Vector4(viewportRaidus, -viewportRaidus, 0, 0);
            if (testPoint.x < -1 || testPoint.x > 1 || testPoint.y < -1 || testPoint.y > 1)
            {
                CullResult[index] = int.MaxValue;
                return;
            }
            testPoint = testCenter + new Vector4(-viewportRaidus, -viewportRaidus, 0, 0);
            if (testPoint.x < -1 || testPoint.x > 1 || testPoint.y < -1 || testPoint.y > 1)
            {
                CullResult[index] = int.MaxValue;
                return;
            }
            CullResult[index] = 0;
        }
    }
    [BurstCompile(CompileSynchronously = true)]
    struct LODJob : IJobParallelFor
    {
        [ReadOnly]
        public Vector3 CameraPos;
        [ReadOnly]
        public NativeArray<Matrix4x4> Matrices;
        [ReadOnly]
        public int LODIndex;
        [ReadOnly]
        public Vector2 LODDistance;
        public NativeArray<int> CullResult;
        public void Execute(int index)
        {
            if (CullResult[index] > 0)
            {
                return;
            }
            var matrix = Matrices[index];
            Vector3 position = new Vector3(matrix.m03, matrix.m13, matrix.m23);
            float distance = (CameraPos - position).sqrMagnitude;
            if (distance >= LODDistance.x * LODDistance.x &&
                distance <= LODDistance.y * LODDistance.y)
            {
                CullResult[index] = LODIndex;
                return;
            }
        }
    }
}
