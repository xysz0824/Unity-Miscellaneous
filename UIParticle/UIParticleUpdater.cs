using System;
using System.Collections.Generic;
using Coffee.UIParticleExtensions;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace Coffee.UIExtensions
{
    internal static class UIParticleUpdater
    {
        static readonly List<UIParticle> s_ActiveParticles = new List<UIParticle>();
        static ParticleSystem.Particle[] s_Particles = new ParticleSystem.Particle[2048];


        public static void Register(UIParticle particle)
        {
            if (!particle) return;
            s_ActiveParticles.Add(particle);
        }

        public static void Unregister(UIParticle particle)
        {
            if (!particle) return;
            s_ActiveParticles.Remove(particle);
#if !UNITY_EDITOR
            foreach (var ps in particle.particles)
            {
                var psInstanceID = ps.GetInstanceID();
                if (particleSystemNatives.ContainsKey(psInstanceID))
                {
                    particleSystemNatives[psInstanceID].Dispose();
                    particleSystemNatives.Remove(psInstanceID);
                }
            }
#endif
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
        }

        private static void Refresh()
        {
            Profiler.BeginSample("[UIParticle] Refresh");
            for (var i = 0; i < s_ActiveParticles.Count; i++)
            {
                try
                {
                    Refresh(s_ActiveParticles[i]);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            Profiler.EndSample();
        }

        private static void Refresh(UIParticle particle)
        {
            if (!particle || !particle.canvas || !particle.canvasRenderer) return;

            // #102: Do not bake particle system to mesh when the alpha is zero.
            if (Mathf.Approximately(particle.canvasRenderer.GetInheritedAlpha(), 0))
            {
                particle.bakedMesh.Clear();
                return;
            }

            Profiler.BeginSample("[UIParticle] Modify scale");
            ModifyScale(particle);
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Bake mesh");
            if (particle.boostByJobSystem) BakeMeshPerformant(particle);
            else BakeMesh(particle);
            Profiler.EndSample();

            if (!particle.boostByJobSystem && QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                Profiler.BeginSample("[UIParticle] Modify color space to linear");
                particle.bakedMesh.ModifyColorSpaceToLinear();
                Profiler.EndSample();
            }

            Profiler.BeginSample("[UIParticle] Set mesh to CanvasRenderer");
            particle.canvasRenderer.SetMesh(particle.bakedMesh);
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Update Animatable Material Properties");
            particle.UpdateMaterialProperties();
            Profiler.EndSample();
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

        private static Matrix4x4 GetScaledMatrix(ParticleSystem particle)
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

        private static void BakeMesh(UIParticle particle)
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
                        r.BakeMesh(m, camera, true);
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
                        try
                        {
                            r.BakeTrailsMesh(m, camera, true);
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
        }

        private static bool CanBakeMesh(ParticleSystemRenderer renderer)
        {
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
                    var uvsSlice = meshJob.uvs.GetSubArray(vertexBase, vertexCount);
                    data.GetUVs(0, uvsSlice);
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

        private static void BakeMeshPerformant(UIParticle particle)
        {
            // Get camera for baking mesh.
            Profiler.BeginSample("[UIParticle] Bake Mesh > Prepare Matrix");
            var camera = BakingCamera.GetCamera(particle.canvas);
            var root = particle.transform;
            var rootMatrix = Matrix4x4.Rotate(root.rotation).inverse
                             * Matrix4x4.Scale(root.lossyScale).inverse;
            var scale = particle.ignoreCanvasScaler
                ? Vector3.Scale(particle.canvas.rootCanvas.transform.localScale, particle.scale3D)
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

            Profiler.BeginSample("[UIParticle] Bake Mesh > Prepare Container");
            var particles = particle.particles;
            var particleRenderers = particle.particleRenderers;
            if (jobHandles.Length < particles.Count * 4 && jobHandles.IsCreated)
            {
                jobHandles.Dispose();
                jobHandles = default;
            }
            if (!jobHandles.IsCreated)
            {
                jobHandles = new NativeArray<JobHandle>(particles.Count * 4, Allocator.Persistent);
            }
            int particleTotal = 0;
            int vertexTotal = 0;
            int indexTotal = 0;
            int trailTotal = 0;
            for (int i = 0; i < particles.Count; ++i)
            {
                var ps = particles[i];
                var r = particleRenderers[i];
                if (!CanBakeMesh(r)) continue;
                var psInstanceID = ps.GetInstanceID();
                //In editor, we will update the native data per time, otherwise only at the first time
#if UNITY_EDITOR
                var psNative = new ParticleSystemNative();
                psNative.CopyFrom(ps, r);
                particleSystemNatives[psInstanceID] = psNative;
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
                    meshJob.uvs.Dispose();
                    meshJob.uvs = default;
                }
                if (!meshJob.vertices.IsCreated)
                {
                    int length = Mathf.Max(meshJob.vertices.Length * 2, vertexTotal);
                    meshJob.vertices = new NativeArray<Vector3>(length, Allocator.Persistent);
                    meshJob.colors = new NativeArray<Color>(length, Allocator.Persistent);
                    meshJob.uvs = new NativeArray<Vector2>(length, Allocator.Persistent);
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
            if (!cachedVertexTotal.ContainsKey(particle) || cachedVertexTotal[particle] > vertexTotal)
            {
                //Avoid an error when vertex total decreased the vertices will less than indices
                particle.bakedMesh.Clear();
            }
            cachedVertexTotal[particle] = vertexTotal;
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Bake Mesh > Particle Job");
            int particleIndex = 0;
            for (int i = 0; i < particles.Count; ++i)
            {
                var ps = particles[i];
                var r = particleRenderers[i];
                if (!CanBakeMesh(r) || !CanBakeMeshPerformant(r)) continue;
                var slice = meshJob.particles.GetSubArray(particleIndex, ps.particleCount);
                ps.GetParticles(slice, ps.particleCount, 0);
                particleIndex += ps.particleCount;
            }
            vertexTotal = 0;
            indexTotal = 0;
            particleIndex = 0;
            meshJob.colorSpace = QualitySettings.activeColorSpace;
            meshJob.scaleMatrix = scaleMatrix;
            transformJob.colorSpace = QualitySettings.activeColorSpace;
            transformJob.vertices = meshJob.vertices;
            transformJob.colors = meshJob.colors;
            indexJob.indices = meshJob.indices;
            var tempMesh = MeshHelper.GetTemporaryMesh();
            for (int i = 0; i < particles.Count; ++i)
            {
                var ps = particles[i];
                var r = particleRenderers[i];
                if (!CanBakeMesh(r)) continue;
                // Extra world simulation.
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World && 0 < diff.sqrMagnitude)
                {
                    if (s_Particles.Length < ps.particleCount)
                    {
                        var size = Mathf.NextPowerOfTwo(ps.particleCount);
                        s_Particles = new ParticleSystem.Particle[size];
                    }
                    ps.GetParticles(s_Particles);
                    for (var j = 0; j < ps.particleCount; j++)
                    {
                        var p = s_Particles[j];
                        p.position += diff;
                        s_Particles[j] = p;
                    }
                    ps.SetParticles(s_Particles, ps.particleCount);
                }
                if (CanBakeMeshPerformant(r))
                {
                    var psInstanceID = ps.GetInstanceID();
                    var transform = ps.transform;
                    if (transform != root)
                    {
                        MeshJob.GetParticleMatrix(rootMatrix, root.localToWorldMatrix, root.position, transform.position, transform.rotation, transform.lossyScale, ps.main.scalingMode, ps.main.simulationSpace, out meshJob.matrix);
                    }
                    else
                    {
                        MeshJob.GetScaledMatrix(transform.rotation, transform.lossyScale, transform.worldToLocalMatrix, ps.main.customSimulationSpace != null, ps.main.simulationSpace,
                            ps.main.customSimulationSpace != null ? ps.main.customSimulationSpace.position : Vector3.zero, out meshJob.matrix);
                    }
                    MeshJob.GetAlignMatrix(r.alignment, transform.rotation, camera.transform.rotation, out meshJob.alignMatrix);
                    int vertexCount = ps.particleCount * 4;
                    int indexCount = ps.particleCount * 6;
                    meshJob.particleSystemNative = particleSystemNatives[psInstanceID];
                    meshJob.particleIndex = particleIndex;
                    meshJob.vertexBase = vertexTotal;
                    meshJob.indexOffset = indexTotal;
                    jobHandles[i * 4] = meshJob.Schedule(ps.particleCount, 64);
                    particleIndex += ps.particleCount;
                    vertexTotal += vertexCount;
                    indexTotal += indexCount;
                }
                if (!CanBakeMeshPerformant(r) || (ps.trails.enabled && r.trailMaterial != null))
                {
                    var matrix = rootMatrix;
                    if (ps.transform != root)
                    {
                        if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                        {
                            var relativePos = root.InverseTransformPoint(ps.transform.position);
                            matrix = Matrix4x4.Translate(relativePos) * matrix;
                        }
                        else
                        {
                            matrix = matrix * Matrix4x4.Translate(-root.position);
                        }
                    }
                    else
                    {
                        matrix = GetScaledMatrix(ps);
                    }
                    matrix = scaleMatrix * matrix;
                    transformJob.matrix = matrix;
                    if (!CanBakeMeshPerformant(r))
                    {
                        r.BakeMesh(tempMesh, camera, true);
                        CopyFromMesh(tempMesh, ref vertexTotal, ref indexTotal, out var vertexCount, out var indexCount);
                        bakedIndexMap[i * 2] = new KeyValuePair<int, int>(vertexTotal - vertexCount, indexCount);
                        transformJob.vertexBase = bakedIndexMap[i * 2].Key;
                        jobHandles[i * 4] = transformJob.Schedule(vertexCount, 256);
                        indexJob.indexOffset = indexTotal - indexCount;
                        indexJob.vertexBase = bakedIndexMap[i * 2].Key;
                        jobHandles[i * 4 + 1] = indexJob.Schedule(indexCount, 512);
                    }
                    if (ps.trails.enabled && r.trailMaterial != null)
                    {
                        CopyFromMesh(bakedTrailMeshesMap[i], ref vertexTotal, ref indexTotal, out var vertexCount, out var indexCount);
                        bakedIndexMap[i * 2 + 1] = new KeyValuePair<int, int>(vertexTotal - vertexCount, indexCount);
                        transformJob.vertexBase = bakedIndexMap[i * 2 + 1].Key;
                        jobHandles[i * 4 + 2] = transformJob.Schedule(vertexCount, 256);
                        indexJob.indexOffset = indexTotal - indexCount;
                        indexJob.vertexBase = bakedIndexMap[i * 2 + 1].Key;
                        jobHandles[i * 4 + 3] = indexJob.Schedule(indexCount, 512);
                        MeshHelper.DiscardTemporaryMesh(bakedTrailMeshesMap[i]);
                    }
                }
            }
            JobHandle.CompleteAll(jobHandles);
            MeshHelper.DiscardTemporaryMesh(tempMesh);
#if UNITY_EDITOR
            foreach (var psNative in particleSystemNatives.Values)
            {
                psNative.Dispose();
            }
            particleSystemNatives.Clear();
#endif
            Profiler.EndSample();

            Profiler.BeginSample("[UIParticle] Bake Mesh > Copy To Mesh");
            if (particle.bakedMesh.subMeshCount != particles.Count + trailTotal)
            {
                particle.bakedMesh.subMeshCount = particles.Count + trailTotal;
            }
            particle.bakedMesh.SetVertices(meshJob.vertices, 0, vertexTotal);
            particle.bakedMesh.SetColors(meshJob.colors, 0, vertexTotal);
            particle.bakedMesh.SetUVs(0, meshJob.uvs, 0, vertexTotal);
            indexTotal = 0;
            int subMeshIndex = 0;
            for (int i = 0; i < particles.Count; ++i)
            {
                var ps = particles[i];
                var r = particleRenderers[i];
                if (!CanBakeMesh(r)) continue;
                int indexCount = CanBakeMeshPerformant(r) ? ps.particleCount * 6 : bakedIndexMap[i * 2].Value;
                particle.bakedMesh.SetIndices(meshJob.indices, indexTotal, indexCount, MeshTopology.Triangles, subMeshIndex++, true, 0);
                particle.activeMeshIndices |= (long)1 << (i * 2);
                indexTotal += indexCount;
                if (ps.trails.enabled && r.trailMaterial != null)
                {
                    indexCount = bakedIndexMap[i * 2 + 1].Value;
                    particle.bakedMesh.SetIndices(meshJob.indices, indexTotal, indexCount, MeshTopology.Triangles, subMeshIndex++, true, 0);
                    particle.activeMeshIndices |= (long)1 << (i * 2 + 1);
                    indexTotal += indexCount;
                }
            }
            Profiler.EndSample();
        }
    }
}