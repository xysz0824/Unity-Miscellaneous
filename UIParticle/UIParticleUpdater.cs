using System;
using System.Collections.Generic;
using Coffee.UIParticleExtensions;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Jobs;
using System.Linq;

namespace Coffee.UIExtensions
{
    internal static class UIParticleUpdater
    {
        static readonly List<UIParticle> s_ActiveParticles = new List<UIParticle>();
        public static ParticleSystem.Particle[] s_Particles = new ParticleSystem.Particle[2048];


        public static void Register(UIParticle particle)
        {
            if (!particle) return;
            s_ActiveParticles.Add(particle);
        }

        public static void Unregister(UIParticle particle)
        {
            if (!particle) return;
            s_ActiveParticles.Remove(particle);
            foreach (var ps in particle.particles)
            {
                var psInstanceID = ps.GetInstanceID();
                if (particleSystemNatives.ContainsKey(psInstanceID))
                {
                    particleSystemNatives[psInstanceID].Dispose();
                    particleSystemNatives.Remove(psInstanceID);
                }
            }
            cachedVertexTotal.Remove(particle);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            MeshHelper.Init();
            MeshPool.Init();
            CombineInstanceArrayPool.Init();
            if (particleSystemNatives != null)
            {
                var values = particleSystemNatives.Values;
                foreach (var val in values)
                {
                    val.Dispose();
                }
            }
            particleSystemNatives = new Dictionary<int, ParticleSystemNative>();
            cachedVertexTotal = new Dictionary<UIParticle, int>();
            bakedTrailMeshesMap = new Dictionary<int, Mesh>();
            bakedIndexMap = new Dictionary<int, KeyValuePair<int, int>>();

            Canvas.willRenderCanvases -= Refresh;
            Canvas.willRenderCanvases += Refresh;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= DestroyNativeContainer;
            UnityEditor.EditorApplication.playModeStateChanged += DestroyNativeContainer;
#endif
        }

#if UNITY_EDITOR
        private static void DestroyNativeContainer(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode || state == UnityEditor.PlayModeStateChange.ExitingEditMode)
                DestroyNativeContainer();
        }
        private static void DestroyNativeContainer()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= DestroyNativeContainer;
            s_ActiveParticles.Clear();
            if (particleSystemNatives != null)
            {
                var values = particleSystemNatives.Values;
                foreach (var val in values)
                {
                    val.Dispose();
                }
            }
            particleSystemNatives = new Dictionary<int, ParticleSystemNative>();
            if (jobHandles.IsCreated)
            {
                jobHandles.Dispose();
                jobHandles = default;
            }
            if (meshJob.vertices.IsCreated)
            {
                meshJob.vertices.Dispose();
                meshJob.vertices = default;
                meshJob.colors.Dispose();
                meshJob.colors = default;
                meshJob.texcoord0.Dispose();
                meshJob.texcoord0 = default;
                meshJob.texcoord1.Dispose();
                meshJob.texcoord1 = default;
            }
            if (meshJob.indices.IsCreated)
            {
                meshJob.indices.Dispose();
                meshJob.indices = default;
            }
            if (meshJob.particles.IsCreated)
            {
                meshJob.particles.Dispose();
                meshJob.particles = default;
            }
        }
#endif
        private static void SortParticles()
        {
            for (int i = 1; i < s_ActiveParticles.Count; i++)
            {
                var key = s_ActiveParticles[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    if (key.meshSharingID < s_ActiveParticles[j].meshSharingID)
                    {
                        s_ActiveParticles[j + 1] = s_ActiveParticles[j];
                        s_ActiveParticles[j] = key;
                    }
                    else break;
                }
            }
        }
        private static void Refresh()
        {
            Profiler.BeginSample("[UIParticle] Refresh");
            SortParticles();
            for (var i = 0; i < s_ActiveParticles.Count; i++)
            {
                try
                {
                    Refresh(s_ActiveParticles[i], i == 0 ? null : s_ActiveParticles[i - 1]);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            Profiler.EndSample();
        }

        static int CompareMeshSharingID(UIParticle a)
        {
            return a.meshSharingID;
        }

        static bool HasDisabledCanvas(Transform trans)
        {
#if !UNITY_EDITOR   //For avoiding runtime GC, we don't check this in editor
            if (trans == null) return false;
            var canvas = trans.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled) return true;
            return HasDisabledCanvas(trans.parent);
#else
            return false;
#endif
        }

        private static void Refresh(UIParticle particle, UIParticle last)
        {
            particle.refreshed = false;
            if (!particle || !particle.canvas || !particle.canvasRenderer || HasDisabledCanvas(particle.transform)) return;

            // #102: Do not bake particle system to mesh when the alpha is zero.
            if (Mathf.Approximately(particle.canvasRenderer.GetInheritedAlpha(), 0))
            {
                particle.bakedMesh.Clear();
                return;
            }

            Profiler.BeginSample("[UIParticle] Modify scale");
            ModifyScale(particle);
            Profiler.EndSample();

#if UNITY_EDITOR
            try
            {
#endif
            Profiler.BeginSample("[UIParticle] Bake mesh");
            if (particle.boostByJobSystem) particle.refreshed = BakeMeshPerformant(particle, last);
            else particle.refreshed = BakeMesh(particle);
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Set mesh to CanvasRenderer");
            particle.canvasRenderer.SetMesh(particle.bakedMesh);
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Update Animatable Material Properties");
            particle.UpdateMaterialProperties();
            Profiler.EndSample();
#if UNITY_EDITOR
            }
            catch (Exception e)
            {
                Debug.LogError(particle.gameObject.name);
                throw e;
            }
#endif
        }

        private static void ModifyScale(UIParticle particle)
        {
            if (!particle.ignoreCanvasScaler || !particle.canvas) return;

            // Ignore Canvas scaling.
            var s = particle.canvas.rootCanvas.transform.localScale;
            var modifiedScale = new Vector3(
                Mathf.Approximately(s.x, 0) ? 1 : 1 / s.x,
                Mathf.Approximately(s.y, 0) ? 1 : 1 / s.y,
                Mathf.Approximately(s.z, 0) ? 1 : 1 / s.z);

            // Scale is already modified.
            var transform = particle.transform;
            if (Mathf.Approximately((transform.localScale - modifiedScale).sqrMagnitude, 0)) return;

            transform.localScale = modifiedScale;
        }

        public static Matrix4x4 GetScaledMatrix(ParticleSystem particle)
        {
            var transform = particle.transform;
            var main = particle.main;
            var space = main.simulationSpace;
            if (space == ParticleSystemSimulationSpace.Custom && !main.customSimulationSpace)
                space = ParticleSystemSimulationSpace.Local;

            switch (space)
            {
                case ParticleSystemSimulationSpace.Local:
                    return Matrix4x4.Rotate(transform.rotation).inverse
                           * Matrix4x4.Scale(transform.lossyScale).inverse;
                case ParticleSystemSimulationSpace.World:
                    return transform.worldToLocalMatrix;
                case ParticleSystemSimulationSpace.Custom:
                    // #78: Support custom simulation space.
                    return transform.worldToLocalMatrix
                           * Matrix4x4.Translate(main.customSimulationSpace.position);
                default:
                    return Matrix4x4.identity;
            }
        }

        private static bool BakeMesh(UIParticle particle)
        {
            // Clear mesh before bake.
            Profiler.BeginSample("[UIParticle] Bake Mesh > Clear Mesh");
            MeshHelper.Clear();
            particle.bakedMesh.Clear(false);
            Profiler.EndSample();

            // Get camera for baking mesh.
            Profiler.BeginSample("[UIParticle] Bake Mesh > Prepare Matrix");
            var camera = BakingCamera.GetCamera(particle.canvas);
            var root = particle.transform;
            var rootMatrix = Matrix4x4.Rotate(root.rotation).inverse
                             * Matrix4x4.Scale(root.lossyScale).inverse;
            var scale = particle.ignoreCanvasScaler
                ? Vector3.Scale( particle.canvas.rootCanvas.transform.localScale, particle.scale3D)
                : particle.scale3D;
            var scaleMatrix = Matrix4x4.Scale(scale);
            // Cache position
            var position = particle.transform.position;
            var diff = position - particle.cachedPosition;
            diff.x *= 1f - 1f / Mathf.Max(0.001f, scale.x);
            diff.y *= 1f - 1f / Mathf.Max(0.001f, scale.y);
            diff.z *= 1f - 1f / Mathf.Max(0.001f, scale.z);

            particle.cachedPosition = position;
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Bake Mesh > BakeMesh");
            bool baked = false;
            for (var i = 0; i < particle.particles.Count; i++)
            {
                // No particle to render.
                var currentPs = particle.particles[i];
                if (!currentPs || !currentPs.IsAlive() || currentPs.particleCount == 0) continue;
                var matrix = rootMatrix;
                if (currentPs.transform != root)
                {
                    if (currentPs.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    {
                        var relativePos = root.InverseTransformPoint(currentPs.transform.position);
                        matrix = Matrix4x4.Translate(relativePos) * matrix;
                    }
                    else
                    {
                        matrix = matrix * Matrix4x4.Translate(-root.position);
                    }
                }
                else
                {
                    matrix = GetScaledMatrix(currentPs);
                }

                matrix = scaleMatrix * matrix;

                // Extra world simulation.
                if (currentPs.main.simulationSpace == ParticleSystemSimulationSpace.World && 0 < diff.sqrMagnitude)
                {
                    var count = currentPs.particleCount;
                    if (s_Particles.Length < count)
                    {
                        var size = Mathf.NextPowerOfTwo(count);
                        s_Particles = new ParticleSystem.Particle[size];
                    }

                    currentPs.GetParticles(s_Particles);
                    for (var j = 0; j < count; j++)
                    {
                        var p = s_Particles[j];
                        p.position += diff;
                        s_Particles[j] = p;
                    }

                    currentPs.SetParticles(s_Particles, count);
                }

                // Bake main particles.
                var r = currentPs.GetComponent<ParticleSystemRenderer>();
                if (CanBakeMesh(r))
                {
                    var hash = currentPs.GetMaterialHash(false);
                    if (hash != 0)
                    {
                        var m = MeshHelper.GetTemporaryMesh();
                        m.Clear(false);
                        r.BakeMesh(m, camera, true);
                        baked = true;
                        MeshHelper.Push(i * 2, hash, m, matrix);
                    }

                }

                // Bake trails particles.
                if (currentPs.trails.enabled)
                {
                    var hash = currentPs.GetMaterialHash(true);
                    if (hash != 0)
                    {
                        var m = MeshHelper.GetTemporaryMesh();
                        m.Clear(false);
                        try
                        {
                            r.BakeTrailsMesh(m, camera, true);
                            baked = true;
                            MeshHelper.Push(i * 2 + 1, hash, m, matrix);
                        }
                        catch
                        {
                            MeshHelper.DiscardTemporaryMesh(m);
                        }
                    }
                }
            }
            // Set active indices.
            particle.activeMeshIndices = MeshHelper.activeMeshIndices;
            Profiler.EndSample();

            // Combine
            Profiler.BeginSample("[UIParticle] Bake Mesh > CombineMesh");
            MeshHelper.CombineMesh(particle.bakedMesh);
            MeshHelper.Clear();
            Profiler.EndSample();

            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                Profiler.BeginSample("[UIParticle] Modify color space to linear");
                particle.bakedMesh.ModifyColorSpaceToLinear();
                Profiler.EndSample();
            }
            return baked;
        }

        private static bool CanBakeMesh(ParticleSystemRenderer renderer)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy) return false;
            // #69: Editor crashes when mesh is set to null when `ParticleSystem.RenderMode = Mesh`
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh == null) return false;

            // #61: When `ParticleSystem.RenderMode = None`, an error occurs
            if (renderer.renderMode == ParticleSystemRenderMode.None) return false;

            return true;
        }

        private static bool CanBakeMeshPerformant(ParticleSystemRenderer renderer)
        {
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh) return false;
            return true;
        }

        static Dictionary<int, ParticleSystemNative> particleSystemNatives;
        static Dictionary<UIParticle, int> cachedVertexTotal;
        static MeshJob meshJob;
        static Dictionary<int, Mesh> bakedTrailMeshesMap;
        static TransformJob transformJob;
        static ColorSpaceJob colorSpaceJob;
        static IndexJob indexJob;
        static Dictionary<int, KeyValuePair<int, int>> bakedIndexMap;
        static NativeArray<JobHandle> jobHandles;

        private static void CopyFromMesh(Mesh mesh, ref int vertexBase, ref int indexOffset, out int vertexCount, out int indexCount)
        {
            using (var meshArray = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = meshArray[0];
                vertexCount = data.vertexCount;
                if (vertexCount > 0)
                {
                    var verticesSlice = meshJob.vertices.GetSubArray(vertexBase, vertexCount);
                    data.GetVertices(verticesSlice);
                    var colorsSlice = meshJob.colors.GetSubArray(vertexBase, vertexCount);
                    data.GetColors(colorsSlice);
                    var uvsSlice = meshJob.texcoord0.GetSubArray(vertexBase, vertexCount);
                    data.GetUVs(0, uvsSlice);
                    if (mesh.uv2 != null && mesh.uv2.Length == vertexCount)
                    {
                        uvsSlice = meshJob.texcoord1.GetSubArray(vertexBase, vertexCount);
                        data.GetUVs(1, uvsSlice);
                    }
                }
                indexCount = (int)mesh.GetIndexCount(0);
                if (indexCount > 0)
                {
                    var indicesSlice = meshJob.indices.GetSubArray(indexOffset, indexCount);
                    data.GetIndices(indicesSlice, 0);
                }
                vertexBase += vertexCount;
                indexOffset += indexCount;
            }
        }

        private static bool BakeMeshPerformant(UIParticle particle, UIParticle last)
        {
            var particles = particle.particles;
            var particleRenderers = particle.particleRenderers;
            int activeParticles = 0;
            for (int i = 0; i < particles.Count; ++i)
            {
                var ps = particles[i];
                if (!ps || !ps.gameObject.activeInHierarchy || ps.particleCount == 0)
                {
                    continue;
                }
                activeParticles++;
            }
            if (activeParticles == 0) return false;
#if !UNITY_EDITOR
            if (!particle.boostByJobSystem || particle.syncTransform)
#endif
            {
                particle.UpdateMatrix();
            }
            // Get camera for baking mesh.
            Profiler.BeginSample("[UIParticle] Bake Mesh > Prepare Matrix");
            var camera = BakingCamera.GetCamera(particle.canvas);
            var particleScale = particle.ignoreCanvasScaler
                ? Vector3.Scale(particle.canvas.rootCanvas.transform.localScale, particle.scale3D)
                : particle.scale3D;
            var scaleMatrix = Matrix4x4.Scale(particleScale);
            Profiler.EndSample();

            int vertexTotal = 0;
            int indexTotal = 0;
            int trailTotal = 0;
            bool meshSharing = last != null && last.boostByJobSystem && last.refreshed && particle.meshSharingID > 0 && last.meshSharingID == particle.meshSharingID;
            if (!meshSharing)
            {
                Profiler.BeginSample("[UIParticle] Bake Mesh > Prepare Container");
                if (jobHandles.Length < particles.Count * 6 && jobHandles.IsCreated)
                {
                    jobHandles.Dispose();
                    jobHandles = default;
                }
                if (!jobHandles.IsCreated)
                {
                    jobHandles = new NativeArray<JobHandle>(particles.Count * 6, Allocator.Persistent);
                }
                int particleTotal = 0;
                for (int i = 0; i < particles.Count; ++i)
                {
                    var ps = particles[i];
                    var r = particleRenderers[i];
                    if (!CanBakeMesh(r)) continue;
                    var psInstanceID = ps.GetInstanceID();
                    //In editor, we will update the native data per time, otherwise only at the first time
#if UNITY_EDITOR
                    if (!particleSystemNatives.ContainsKey(psInstanceID) || !Application.isPlaying)
                    {
                        var psNative = particleSystemNatives.ContainsKey(psInstanceID) ? particleSystemNatives[psInstanceID] : new ParticleSystemNative();
                        psNative.CopyFrom(ps, r);
                        particleSystemNatives[psInstanceID] = psNative;
                    }
#else
                if (!particleSystemNatives.ContainsKey(psInstanceID))
                {
                    var psNative = new ParticleSystemNative();
                    psNative.CopyFrom(ps, r);
                    particleSystemNatives.Add(psInstanceID, psNative);
                }
#endif
                    if (ps.trails.enabled && r.trailMaterial != null)
                    {
                        var m = MeshHelper.GetTemporaryMesh();
                        r.BakeTrailsMesh(m, camera, true);
                        bakedTrailMeshesMap[i] = m;
                        vertexTotal += m.vertexCount;
                        indexTotal += (int)m.GetIndexCount(0);
                        trailTotal++;
                    }
                    particleTotal += ps.particleCount;
                    int vertexCount = r.renderMode == ParticleSystemRenderMode.Mesh ? r.mesh.vertexCount : 4;
                    vertexTotal += ps.particleCount * vertexCount;
                    if (meshJob.vertices.Length < vertexTotal && meshJob.vertices.IsCreated)
                    {
                        meshJob.vertices.Dispose();
                        meshJob.vertices = default;
                        meshJob.colors.Dispose();
                        meshJob.colors = default;
                        meshJob.texcoord0.Dispose();
                        meshJob.texcoord0 = default;
                        meshJob.texcoord1.Dispose();
                        meshJob.texcoord1 = default;
                    }
                    if (!meshJob.vertices.IsCreated)
                    {
                        int length = Mathf.Max(meshJob.vertices.Length * 2, vertexTotal);
                        meshJob.vertices = new NativeArray<Vector3>(length, Allocator.Persistent);
                        meshJob.colors = new NativeArray<Color>(length, Allocator.Persistent);
                        meshJob.texcoord0 = new NativeArray<Vector4>(length, Allocator.Persistent);
                        meshJob.texcoord1 = new NativeArray<Vector4>(length, Allocator.Persistent);
                    }
                    int indexCount = r.renderMode == ParticleSystemRenderMode.Mesh ? (int)r.mesh.GetIndexCount(0) : 6;
                    indexTotal += ps.particleCount * indexCount;
                    if (meshJob.indices.Length < indexTotal && meshJob.indices.IsCreated)
                    {
                        meshJob.indices.Dispose();
                        meshJob.indices = default;
                    }
                    if (!meshJob.indices.IsCreated)
                    {
                        meshJob.indices = new NativeArray<int>(Mathf.Max(meshJob.indices.Length * 2, indexTotal), Allocator.Persistent);
                    }
                }
                if (meshJob.particles.Length < particleTotal && meshJob.particles.IsCreated)
                {
                    meshJob.particles.Dispose();
                    meshJob.particles = default;
                }
                if (!meshJob.particles.IsCreated)
                {
                    meshJob.particles = new NativeArray<ParticleSystem.Particle>(Mathf.NextPowerOfTwo(particleTotal), Allocator.Persistent);
                    s_Particles = new ParticleSystem.Particle[meshJob.particles.Length];
                }
                Profiler.EndSample();

                Profiler.BeginSample("[UIParticle] Bake Mesh > Particle Job");
                particleTotal = 0;
                for (int i = 0; i < particles.Count; ++i)
                {
                    var ps = particles[i];
                    var r = particleRenderers[i];
                    if (!CanBakeMeshPerformant(r) || !CanBakeMesh(r)) continue;
                    var slice = meshJob.particles.GetSubArray(particleTotal, ps.particleCount);
                    ps.GetParticles(slice);
                    particleTotal += ps.particleCount;
                }
                vertexTotal = 0;
                indexTotal = 0;
                particleTotal = 0;
                meshJob.scaleMatrix = scaleMatrix;
                transformJob.vertices = meshJob.vertices;
                colorSpaceJob.colors = meshJob.colors;
                indexJob.indices = meshJob.indices;
                var tempMesh = MeshHelper.GetTemporaryMesh();
                for (int i = 0; i < particles.Count; ++i)
                {
                    var ps = particles[i];
                    var r = particleRenderers[i];
                    if (!CanBakeMesh(r)) continue;
                    bool canBakeMeshPerformant = CanBakeMeshPerformant(r);
                    if (canBakeMeshPerformant)
                    {
                        var psInstanceID = ps.GetInstanceID();
                        int vertexCount = ps.particleCount * 4;
                        int indexCount = ps.particleCount * 6;
                        bakedIndexMap[i * 2] = new KeyValuePair<int, int>(vertexCount, indexCount);
                        meshJob.particleSystemNative = particleSystemNatives[psInstanceID];
                        meshJob.particleIndex = particleTotal;
                        meshJob.matrix = particle.Matrices[i];
                        meshJob.alignMatrix = particle.AlignMatrices[i];
                        meshJob.vertexBase = vertexTotal;
                        meshJob.indexOffset = indexTotal;
                        jobHandles[i * 6] = meshJob.Schedule(ps.particleCount, 64);
                        particleTotal += ps.particleCount;
                        vertexTotal += vertexCount;
                        indexTotal += indexCount;
                    }
                    bool bakeTrail = ps.trails.enabled && r.trailMaterial != null;
                    if (!canBakeMeshPerformant || bakeTrail)
                    {
                        transformJob.matrix = scaleMatrix * particle.Matrices[i];
                        if (!canBakeMeshPerformant)
                        {
                            r.BakeMesh(tempMesh, camera, true);
                            CopyFromMesh(tempMesh, ref vertexTotal, ref indexTotal, out var vertexCount, out var indexCount);
                            bakedIndexMap[i * 2] = new KeyValuePair<int, int>(vertexCount, indexCount);
                            transformJob.vertexBase = vertexTotal - vertexCount;
                            jobHandles[i * 6] = transformJob.Schedule(vertexCount, 512);
                            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                            {
                                colorSpaceJob.vertexBase = transformJob.vertexBase;
                                jobHandles[i * 6 + 4] = colorSpaceJob.Schedule(vertexCount, 512);
                            }
                            indexJob.indexOffset = indexTotal - indexCount;
                            indexJob.vertexBase = vertexTotal - vertexCount;
                            jobHandles[i * 6 + 1] = indexJob.Schedule(indexCount, 512);
                        }
                        if (bakeTrail)
                        {
                            CopyFromMesh(bakedTrailMeshesMap[i], ref vertexTotal, ref indexTotal, out var vertexCount, out var indexCount);
                            bakedIndexMap[i * 2 + 1] = new KeyValuePair<int, int>(vertexCount, indexCount);
                            transformJob.vertexBase = vertexTotal - vertexCount;
                            jobHandles[i * 6 + 2] = transformJob.Schedule(vertexCount, 512);
                            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                            {
                                colorSpaceJob.vertexBase = transformJob.vertexBase;
                                jobHandles[i * 6 + 5] = colorSpaceJob.Schedule(vertexCount, 512);
                            }
                            indexJob.indexOffset = indexTotal - indexCount;
                            indexJob.vertexBase = vertexTotal - vertexCount;
                            jobHandles[i * 6 + 3] = indexJob.Schedule(indexCount, 512);
                            MeshHelper.DiscardTemporaryMesh(bakedTrailMeshesMap[i]);
                        }
                    }
                }
                JobHandle.CompleteAll(jobHandles);
                MeshHelper.DiscardTemporaryMesh(tempMesh);
                Profiler.EndSample();
            }

            Profiler.BeginSample("[UIParticle] Bake Mesh > Copy To Mesh");
            long activeMeshIndices = 0;
            bool baked = false;
            if (!meshSharing)
            {
                if (!cachedVertexTotal.ContainsKey(particle) || cachedVertexTotal[particle] > vertexTotal)
                {
                    //Avoid an error when vertex total decreased the vertices will less than indices
                    particle.bakedMesh.Clear();
                }
                cachedVertexTotal[particle] = vertexTotal;
                if (particle.bakedMesh.subMeshCount != particles.Count + trailTotal)
                {
                    particle.bakedMesh.subMeshCount = particles.Count + trailTotal;
                }
                particle.bakedMesh.SetVertices(meshJob.vertices, 0, vertexTotal);
                particle.bakedMesh.SetColors(meshJob.colors, 0, vertexTotal);
                particle.bakedMesh.SetUVs(0, meshJob.texcoord0, 0, vertexTotal);
                particle.bakedMesh.SetUVs(1, meshJob.texcoord1, 0, vertexTotal);
                indexTotal = 0;
                int subMeshIndex = 0;
                for (int i = 0; i < particles.Count; ++i)
                {
                    var ps = particles[i];
                    var r = particleRenderers[i];
                    if (!CanBakeMesh(r)) continue;
                    int indexCount = bakedIndexMap[i * 2].Value;
                    particle.bakedMesh.SetIndices(meshJob.indices, indexTotal, indexCount, MeshTopology.Triangles, subMeshIndex++, true, 0);
                    activeMeshIndices |= (long)1 << (i * 2);
                    indexTotal += indexCount;
                    baked = true;
                    if (ps.trails.enabled && r.trailMaterial != null)
                    {
                        indexCount = bakedIndexMap[i * 2 + 1].Value;
                        particle.bakedMesh.SetIndices(meshJob.indices, indexTotal, indexCount, MeshTopology.Triangles, subMeshIndex++, true, 0);
                        activeMeshIndices |= (long)1 << (i * 2 + 1);
                        indexTotal += indexCount;
                    }
                }
            }
            else
            {
                if (particles.Count != last.particles.Count)
                {
                    return false;
                }
                for (int i = 0; i < particles.Count; ++i)
                {
                    var ps = particles[i];
                    var r = particleRenderers[i];
                    if (!CanBakeMesh(r)) continue;
                    vertexTotal += bakedIndexMap[i * 2].Key;
                    baked = true;
                    bool bakeTrail = ps.trails.enabled && r.trailMaterial != null;
                    if (bakeTrail)
                    {
                        vertexTotal += bakedIndexMap[i * 2 + 1].Key;
                    }
                }
                if (!cachedVertexTotal.ContainsKey(particle) || cachedVertexTotal[particle] > vertexTotal)
                {
                    //Avoid an error when vertex total decreased the vertices will less than indices
                    particle.bakedMesh.Clear();
                }
                cachedVertexTotal[particle] = vertexTotal;
                particle.bakedMesh.subMeshCount = last.bakedMesh.subMeshCount;
                particle.bakedMesh.SetVertices(meshJob.vertices, 0, vertexTotal);
                particle.bakedMesh.SetColors(meshJob.colors, 0, vertexTotal);
                particle.bakedMesh.SetUVs(0, meshJob.texcoord0, 0, vertexTotal);
                particle.bakedMesh.SetUVs(1, meshJob.texcoord1, 0, vertexTotal);
                int subMeshIndex = 0;
                for (int i = 0; i < particles.Count; ++i)
                {
                    var ps = particles[i];
                    var r = particleRenderers[i];
                    if (!CanBakeMesh(r)) continue;
                    int indexCount = bakedIndexMap[i * 2].Value;
                    particle.bakedMesh.SetIndices(meshJob.indices, indexTotal, indexCount, MeshTopology.Triangles, subMeshIndex++, true, 0);
                    activeMeshIndices |= (long)1 << (i * 2);
                    indexTotal += indexCount;
                    if (ps.trails.enabled && r.trailMaterial != null)
                    {
                        indexCount = bakedIndexMap[i * 2 + 1].Value;
                        particle.bakedMesh.SetIndices(meshJob.indices, indexTotal, indexCount, MeshTopology.Triangles, subMeshIndex++, true, 0);
                        activeMeshIndices |= (long)1 << (i * 2 + 1);
                        indexTotal += indexCount;
                    }
                }
            }
            particle.activeMeshIndices = activeMeshIndices;
            Profiler.EndSample();
            return baked;
        }
    }
}