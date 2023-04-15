using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;
using Object = System.Object;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("Model", "检查模型网格是否不含特定信息")]
    public static bool ModelMeshInfo([EnsureModel] GameObject model, bool uv2, bool uv3_8, bool color, bool normal, bool tangent)
    {
        var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
        var meshs = new HashSet<Mesh>();
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            meshs.Add(meshFilter.sharedMesh);
        }
        var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            meshs.Add(renderer.sharedMesh);
        }
        bool hasMeshInfo = false;
        foreach (var mesh in meshs)
        {
            var infoStringBuilder = new StringBuilder();
            if (uv2 && mesh.uv2 != null && mesh.uv2.Length > 0) infoStringBuilder.Append("UV2 ");
            if (uv3_8 && mesh.uv3 != null && mesh.uv3.Length > 0) infoStringBuilder.Append("UV3 ");
            if (uv3_8 && mesh.uv4 != null && mesh.uv4.Length > 0) infoStringBuilder.Append("UV4 ");
            if (uv3_8 && mesh.uv5 != null && mesh.uv5.Length > 0) infoStringBuilder.Append("UV5 ");
            if (uv3_8 && mesh.uv6 != null && mesh.uv6.Length > 0) infoStringBuilder.Append("UV6 ");
            if (uv3_8 && mesh.uv7 != null && mesh.uv7.Length > 0) infoStringBuilder.Append("UV7 ");
            if (uv3_8 && mesh.uv8 != null && mesh.uv8.Length > 0) infoStringBuilder.Append("UV8 ");
            if (color && mesh.colors != null && mesh.colors.Length > 0) infoStringBuilder.Append("Color ");
            if (normal && mesh.normals != null && mesh.normals.Length > 0) infoStringBuilder.Append("Normal ");
            if (tangent && mesh.tangents != null && mesh.tangents.Length > 0) infoStringBuilder.Append("Tangent ");
            if (infoStringBuilder.Length > 0)
            {
                AddLog($"模型网格含有{infoStringBuilder.ToString()}", 0, null, mesh);
                hasMeshInfo = true;
            }
        }
        return !hasMeshInfo;
    }
    [AssetClearanceMethod("Model", "检查模型网格是否不含冗余信息")]
    public static bool ModelUselessMeshInfo([EnsureModel] GameObject model, bool uv2, bool uv3_8, bool color)
    {
        var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
        var meshs = new HashSet<Mesh>();
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            meshs.Add(meshFilter.sharedMesh);
        }
        var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            meshs.Add(renderer.sharedMesh);
        }
        bool hasMeshInfo = false;
        foreach (var mesh in meshs)
        {
            var infoStringBuilder = new StringBuilder();
            if (uv2 && mesh.uv2 != null && mesh.uv2.Length > 0 && IsUselessMeshInfo(mesh.uv2)) infoStringBuilder.Append("UV2 ");
            if (uv3_8 && mesh.uv3 != null && mesh.uv3.Length > 0 && IsUselessMeshInfo(mesh.uv3)) infoStringBuilder.Append("UV3");
            if (uv3_8 && mesh.uv4 != null && mesh.uv4.Length > 0 && IsUselessMeshInfo(mesh.uv4)) infoStringBuilder.Append("UV4");
            if (uv3_8 && mesh.uv5 != null && mesh.uv5.Length > 0 && IsUselessMeshInfo(mesh.uv5)) infoStringBuilder.Append("UV5");
            if (uv3_8 && mesh.uv6 != null && mesh.uv6.Length > 0 && IsUselessMeshInfo(mesh.uv6)) infoStringBuilder.Append("UV6");
            if (uv3_8 && mesh.uv7 != null && mesh.uv7.Length > 0 && IsUselessMeshInfo(mesh.uv7)) infoStringBuilder.Append("UV7");
            if (uv3_8 && mesh.uv8 != null && mesh.uv8.Length > 0 && IsUselessMeshInfo(mesh.uv8)) infoStringBuilder.Append("UV8");
            if (color && mesh.colors != null && mesh.colors.Length > 0 && IsUselessMeshInfo(mesh.colors)) infoStringBuilder.Append("Color");
            if (infoStringBuilder.Length > 0)
            {
                AddLog($"模型网格含有冗余信息{infoStringBuilder.ToString()}", 0, null, mesh);
                hasMeshInfo = true;
            }
        }
        return !hasMeshInfo;
    }
    [AssetClearanceMethod("Model", "检查模型网格导入设置是否符合规范")]
    public static bool ModelMeshImportSetting([EnsureModel] GameObject model, bool readWrite, ModelImporterMeshCompression compression)
    {
        var log = new StringBuilder();
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(model)) as ModelImporter;
        bool noProblem = true;
        if (readWrite && importer.isReadable)
        {
            log.Append("开启了Read/Write Enabled ");
            noProblem = false;
        }
        if (importer.meshCompression < compression)
        {
            log.Append($"压缩设置{importer.meshCompression}低于指定设置{compression} ");
            noProblem = false;
        }
        if (!noProblem) AddLog(log.ToString());
        return noProblem;
    }
    [AssetClearanceMethod("Model", "检查模型是否导入特定Shader材质")]
    public static bool ModelMaterialShader([EnsureModel] GameObject model, [EnsureShader] string[] materialShaders)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;
                if (Array.Exists(materialShaders, (shaderName) => material.shader.name == shaderName))
                {
                    AddLog($"该模型导入特定Shader\"{material.shader.name}\"的材质");
                    return true;
                }
            }
        }
        return false;
    }
    [AssetClearanceMethod("Model", "检查模型内单个网格面数是否不超过指定数量")]
    public static bool ModelSingleTriCount([EnsureModel] GameObject model, int maxTriangleCount)
    {
        bool ok = true;
        var oversizemeshesLog = new StringBuilder();
        var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
        var meshs = new HashSet<Mesh>();
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            meshs.Add(meshFilter.sharedMesh);
        }
        var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            meshs.Add(renderer.sharedMesh);
        }
        foreach (var mesh in meshs)
        {
            var triCount = mesh.triangles.Length / 3;
            if (triCount > maxTriangleCount)
            {
                oversizemeshesLog.Append(mesh.name).Append($" 面数:{triCount}").Append(" ");
                AddLog($"网格面数{triCount}超过指定数量{maxTriangleCount}", triCount, null, mesh);
                ok = false;
            }
        }
        return ok;
    }
    [AssetClearanceMethod("Model", "检查模型所有网格面数总数是否不超过指定数量")]
    public static bool ModelTotalTriCount([EnsureModel] GameObject model, int triangleCount)
    {
        int totalCount = 0;
        var meshes = new HashSet<Mesh>();
        var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            meshes.Add(meshFilter.sharedMesh);
        }
        var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            meshes.Add(renderer.sharedMesh);
        }
        foreach (var mesh in meshes)
        {
            totalCount += mesh.triangles.Length / 3;
        }
        bool result = totalCount <= triangleCount;
        if (!result)
        {
            AddLog($"模型网格总面数{totalCount}超过指定数量{triangleCount}", totalCount);
        }
        return result;
    }
    [AssetClearanceMethod("Model", "检查单个模型所有网格占用内存是否不超过指定大小（MB）")]
    public static bool ModelTotalMemory([EnsureModel] GameObject model, float memoryLimit)
    {
        var meshes = new HashSet<Mesh>();
        var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            meshes.Add(meshFilter.sharedMesh);
        }
        var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            meshes.Add(renderer.sharedMesh);
        }
        long runtimeMemory = 0;
        foreach (var mesh in meshes)
        {
            runtimeMemory += GetMeshMemory(mesh);
        }
        bool result = runtimeMemory <= memoryLimit * 1024 * 1024;
        if (!result)
        {
            int order = (int)(runtimeMemory / 1024 / 1024);
            AddLog($"模型所有网格占用内存{FormatBytes(runtimeMemory)}MB超过指定大小{memoryLimit}MB", order);
            foreach (var mesh in meshes)
            {
                AddLog($"网格占用内存{FormatBytes(GetMeshMemory(mesh))}MB", order, null, mesh);
            }
        }
        return result;
    }
    [AssetClearanceMethod("Model", "检查模型所有网格引用次数是否不超过指定数量")]
    public static bool ModelMeshReferenceCount([EnsureModel] GameObject model, int referenceCount)
    {
        bool ok = true;
        var referenceDict = new Dictionary<Mesh, int>();
        var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            if (!referenceDict.ContainsKey(meshFilter.sharedMesh))
            {
                referenceDict[meshFilter.sharedMesh] = 0;
            }
            referenceDict[meshFilter.sharedMesh]++;
        }
        var skinnedMeshRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            if (!referenceDict.ContainsKey(renderer.sharedMesh))
            {
                referenceDict[renderer.sharedMesh] = 0;
            }
            referenceDict[renderer.sharedMesh]++;
        }
        foreach (var kv in referenceDict)
        {
            if (kv.Value > referenceCount)
            {
                AddLog($"网格引用次数{kv.Value}超过指定数量{referenceCount}", kv.Value, null, kv.Key);
                ok = false;
            }
        }
        return ok;
    }
}
