using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Profiling;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("Folder", "统计文件夹下AnimatorController引用动画总占用内存大小不超过指定大小")]
    public static bool FolderAnimatorControllerClipsMemory(DefaultAsset folder, int memoryLimit)
    {
        var guids = AssetDatabase.FindAssets("t:AnimatorController", new string[] { AssetDatabase.GetAssetPath(folder) });
        var clips = new HashSet<AnimationClip>();
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            foreach (var clip in controller.animationClips)
            {
                clips.Add(clip);
            }
        }
        long runtimeMemory = 0;
        foreach (var clip in clips)
        {
            runtimeMemory += GetAnimationClipMemory(clip);
        }
        bool result = runtimeMemory <= memoryLimit * 1024 * 1024;
        if (!result)
        {
            AddLog($"该文件夹下AnimatorController引用动画总占用内存大小{FormatBytes(runtimeMemory)}MB超过指定大小{memoryLimit}MB", (int)(runtimeMemory / 1024 / 1024));
        }
        return result;
    }
    [AssetClearanceMethod("Folder", "统计文件夹下材质球数量不超过指定数量")]
    public static bool FolderMaterialCount(DefaultAsset folder, int maxCount)
    {
        var guids = AssetDatabase.FindAssets("t:Material", new string[] { AssetDatabase.GetAssetPath(folder) });
        bool result = guids.Length <= maxCount;
        if (!result)
        {
            AddLog($"材质球数量{guids.Length}已超过指定数量{maxCount}", guids.Length);
        }
        return result;
    }
    [AssetClearanceMethod("Folder", "统计文件夹下模型所有网格面数总数是否不超过指定数量")]
    public static bool FolderModelTotalTriCount(DefaultAsset folder, int maxCount)
    {
        int totalCount = 0;
        var meshs = new HashSet<Mesh>();
        var guids = AssetDatabase.FindAssets("t:Model", new string[] { AssetDatabase.GetAssetPath(folder) });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null) continue;
            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
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
        }
        foreach (var mesh in meshs)
        {
            totalCount += mesh.triangles.Length / 3;
        }
        bool result = totalCount <= maxCount;
        if (!result)
        {
            AddLog($"所有网格面数总数{totalCount}超过指定数量{maxCount}", totalCount);
        }
        return result;
    }
    [AssetClearanceMethod("Folder", "统计文件夹下模型所有网格占用内存是否不超过指定大小（MB）")]
    public static bool FolderModelMeshesMemory(DefaultAsset folder, float memoryLimit)
    {
        var meshs = new HashSet<Mesh>();
        var guids = AssetDatabase.FindAssets("t:Model", new string[] { AssetDatabase.GetAssetPath(folder) });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null) continue;
            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
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
        }
        long runtimeMemory = 0;
        foreach (var mesh in meshs)
        {
            runtimeMemory += GetMeshMemory(mesh);
        }
        bool result = runtimeMemory <= memoryLimit * 1024 * 1024; 
        if (!result)
        {
            AddLog($"所有网格占用内存{FormatBytes(runtimeMemory)}MB超过指定大小{memoryLimit}MB", (int)(runtimeMemory / 1024 / 1024));
        }
        return result;
    }
    [AssetClearanceMethod("Folder", "统计文件夹下当前平台的总贴图占用内存是否不超过指定大小（MB）")]
    public static bool FolderTexturesMemory(DefaultAsset folder, float memoryLimit)
    {
        long storageMemory = 0;
        var guids = AssetDatabase.FindAssets("t:Texture", new string[] { AssetDatabase.GetAssetPath(folder) });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            var memory = GetTextureStorageMemory(texture);
            storageMemory += memory;
        }
        bool result = storageMemory <= memoryLimit * 1024 * 1024;
        if (!result)
        {
            AddLog($"当前平台所有贴图占用内存大小{FormatBytes(storageMemory)}MB超过指定大小{memoryLimit}MB", (int)(storageMemory / 1024 / 1024));
        }
        return result;
    }
    [AssetClearanceMethod("Folder", "统计文件夹下贴图数量不超过指定数量")]
    public static bool FolderTextureCount(DefaultAsset folder, int maxCount)
    {
        var guids = AssetDatabase.FindAssets("t:Texture", new string[] { AssetDatabase.GetAssetPath(folder) });
        bool result = guids.Length <= maxCount;
        if (!result)
        {
            AddLog($"贴图数量{guids.Length}超过指定数量{maxCount}", guids.Length);
        }
        return result;
    }
    [AssetClearanceMethod("Folder", "统计文件夹下贴图面积当量（2的n次方）不超过指定数量")]
    public static bool FolderTextureAreaCount(DefaultAsset folder, [EnsurePlatform] string platform, int size, int maxCount)
    {
        int area = 0;
        var guids = AssetDatabase.FindAssets("t:Texture", new string[] { AssetDatabase.GetAssetPath(folder) });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            var platformSize = GetPlatformTextureSize(texture, platform);
            area += platformSize.x * platformSize.y;
        }
        size = Mathf.Max(2, (int)Mathf.Pow(2, (int)Mathf.Log(size, 2)));
        float areaCount = area / (size * size);
        bool result = areaCount <= maxCount;
        if (!result)
        {
            AddLog($"贴图面积当量{areaCount}张{size}图，超过指定数量{maxCount}张", (int)areaCount);
        }
        return result;
    }
}
