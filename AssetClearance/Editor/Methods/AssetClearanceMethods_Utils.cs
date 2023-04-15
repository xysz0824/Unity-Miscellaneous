using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Profiling;

public partial class AssetClearanceMethods
{
    public static bool IsUselessMeshInfo<T>(T[] info)
    {
        if (info.Length <= 1) return false;
        T a = info[0];
        for (int i = 1; i < info.Length; ++i)
        {
            if (!a.Equals(info[i])) return false;
            a = info[i];
        }
        return true;
    }
    static bool IsEmptyStringArray(string[] array)
    {
        if (array == null || array.Length == 0) return true;
        for (int i = 0; i < array.Length; ++i)
        {
            if (!string.IsNullOrWhiteSpace(array[i]))
            {
                return false;
            }
        }
        return true;
    }
    public static T[] GetComponentsInChildrenAdvanced<T>(this GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive = false) where T : Component
    {
        if (IsEmptyStringArray(includePaths)) includePaths = rules.defaultIncludePaths;
        if (IsEmptyStringArray(excludePaths)) excludePaths = rules.defaultExcludePaths;
        var components = gameObject.GetComponentsInChildren<T>(includeInactive);
        List<Transform> includePathRoots = null;
        List<Transform> excludePathRoots = null;
        List<Transform> includePathChildrens = new List<Transform>();
        List<Transform> excludePathChildrens = new List<Transform>();
        foreach (var includePath in includePaths)
        {
            if (string.IsNullOrWhiteSpace(includePath)) continue;
            if (includePathRoots == null)
            {
                includePathRoots = new List<Transform>();
            }
            var includePathRoot = gameObject.transform.Find(includePath);
            if (includePathRoot)
            {
                includePathRoots.Add(includePathRoot);
            }
        }
        foreach (var excludePath in excludePaths)
        {
            if (string.IsNullOrWhiteSpace(excludePath)) continue;
            if (excludePathRoots == null)
            {
                excludePathRoots = new List<Transform>();
            }
            var excludePathRoot = gameObject.transform.Find(excludePath);
            if (excludePathRoot)
            {
                excludePathRoots.Add(excludePathRoot);
            }
        }
        if (includePathRoots != null)
        {
            foreach (var includePathRoot in includePathRoots)
            {
                var childrens = includePathRoot.GetComponentsInChildren<Transform>(true);
                includePathChildrens.AddRange(childrens);
            }
        }
        if (excludePathRoots != null)
        {
            foreach (var excludePathRoot in excludePathRoots)
            {
                var childrens = excludePathRoot.GetComponentsInChildren<Transform>(true);
                excludePathChildrens.AddRange(childrens);
            }
        }
        if (components.Length > 0)
        {
            var removeIndex = components.Length;
            for (int i = 0; i < removeIndex; ++i)
            {
                var component = components[i] as Component;
                if (component == null ||
                    (includePathRoots != null && !includePathChildrens.Contains(component.transform)) ||
                    (excludePathRoots != null && excludePathChildrens.Contains(component.transform)))
                {
                    removeIndex--;
                    var temp = components[i];
                    components[i] = components[removeIndex];
                    components[removeIndex] = temp;
                    i--;
                }
            }
            Array.Resize(ref components, removeIndex);
        }
        return components;
    }
    private static Regex sBoneNameRE = new Regex("bone[(0-9)+]|^Bip00|^Bone|^HH_|^B_|root");//过滤骨骼名字，bone99,Bip001 Head等
    private static bool IsParentBoneRoot(GameObject root, GameObject gameObject)
    {
        if (gameObject == root) return false;
        if (gameObject.name.ToLower() == "root") return true;
        return IsParentBoneRoot(root, gameObject.transform.parent.gameObject);
    }
    private static bool IsBone(GameObject root, GameObject gameObject)
    {
        var name = gameObject.name;
        return sBoneNameRE.IsMatch(name) || IsParentBoneRoot(root, gameObject);
    }
    private static string GetRelativePathForTip(string[] includePaths)
    {
        if (IsEmptyStringArray(includePaths)) includePaths = rules.defaultIncludePaths;
        if (includePaths.Length > 0)
        {
            return includePaths[0];
        }
        return "";
    }
    private static bool HasLostMaterial(Material[] matArr)
    {
        if (matArr == null)
        {
            return true;
        }

        if (matArr.Length == 0)
        {
            return true;
        }

        foreach (var mat in matArr)
        {
            if (mat == null)
            {
                return true;
            }
        }

        return false;
    }
    // 输入byte，返回MB ,小数点后保留两位
    static string FormatBytes(long byteCount)
    {
        return (byteCount / 1024f / 1024f).ToString("f2");
    }
    static HashSet<UnityEngine.Object> GetObjectReferences(GameObject gameObject, string[] includePaths, string[] excludePaths)
    {
        var components = gameObject.GetComponentsInChildrenAdvanced<Component>(includePaths, excludePaths, true);
        var objectReferences = new HashSet<UnityEngine.Object>();
        foreach (var child in components)
        {
            if (child is Transform) continue;
            var so = new SerializedObject(child);
            var prop = so.GetIterator();
            while (prop.NextVisible(true))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null)
                {
                    objectReferences.Add(prop.objectReferenceValue);
                }
            }
        }
        return objectReferences;
    }
    static List<T> GetReferencingAssets<T>(GameObject gameObject, string[] includePaths, string[] excludePaths) where T : UnityEngine.Object
    {
        var assetPath = AssetDatabase.GetAssetPath(gameObject);
        var objectReferences = GetObjectReferences(gameObject, includePaths, excludePaths);
        var assets = new List<T>();
        foreach (var objectReference in objectReferences)
        {
            var type = objectReference.GetType();
            var path = AssetDatabase.GetAssetPath(objectReference);
            if (path == null || path == assetPath) continue;
            if (type == typeof(T) || type.IsSubclassOf(typeof(T)))
            {
                assets.Add(objectReference as T);
            }
        }
        return assets;
    }
    static Dictionary<GameObject, Mesh> GetDependencyMeshes(GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive, bool noRepeat)
    {
        var meshHashSet = new HashSet<Mesh>();
        var meshes = new Dictionary<GameObject, Mesh>();
        var meshFilters = gameObject.GetComponentsInChildrenAdvanced<MeshFilter>(includePaths, excludePaths, includeInactive);
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            if (noRepeat && meshHashSet.Contains(meshFilter.sharedMesh)) continue;
            meshes[meshFilter.gameObject] = meshFilter.sharedMesh;
            meshHashSet.Add(meshFilter.sharedMesh);
        }
        var skinnedMeshRenderers = gameObject.GetComponentsInChildrenAdvanced<SkinnedMeshRenderer>(includePaths, excludePaths, includeInactive);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            if (noRepeat && meshHashSet.Contains(renderer.sharedMesh)) continue;
            meshes[renderer.gameObject] = renderer.sharedMesh;
            meshHashSet.Add(renderer.sharedMesh);
        }
        return meshes;
    }
    static long GetMeshMemory(Mesh mesh)
    {
        return Profiler.GetRuntimeMemorySizeLong(mesh) / 3;
    }
    static Dictionary<GameObject, GameObject> GetDependencyModels(GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive)
    {
        var modelHashSet = new HashSet<GameObject>();
        var models = new Dictionary<GameObject, GameObject>();
        var meshFilters = gameObject.GetComponentsInChildrenAdvanced<MeshFilter>(includePaths, excludePaths, includeInactive);
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            var assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (type == typeof(GameObject))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (modelHashSet.Contains(model)) continue;
                models[meshFilter.gameObject] = model;
                modelHashSet.Add(model);
            }
        }
        var skinnedMeshRenderers = gameObject.GetComponentsInChildrenAdvanced<SkinnedMeshRenderer>(includePaths, excludePaths, includeInactive);
        foreach (var renderer in skinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;
            var assetPath = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (type == typeof(GameObject))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (modelHashSet.Contains(model)) continue;
                models[renderer.gameObject] = model;
                modelHashSet.Add(model);
            }
        }
        return models;
    }
    static Dictionary<GameObject, List<Texture>> GetDependencyTextures(GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive)
    {
        var textureHashSet = new HashSet<Texture>();
        var textures = new Dictionary<GameObject, List<Texture>>();
        var renderers = gameObject.GetComponentsInChildrenAdvanced<Renderer>(includePaths, excludePaths, includeInactive);
        foreach (var renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0) continue;
            foreach (var sharedMaterial in renderer.sharedMaterials)
            {
                if (sharedMaterial == null) continue;
                var properties = sharedMaterial.GetTexturePropertyNameIDs();
                foreach (var id in properties)
                {
                    var texture = sharedMaterial.GetTexture(id);
                    if (texture == null) continue;
                    if (textureHashSet.Contains(texture)) continue;
                    if (!textures.ContainsKey(renderer.gameObject))
                    {
                        textures[renderer.gameObject] = new List<Texture>();
                    }
                    textures[renderer.gameObject].Add(texture);
                    textureHashSet.Add(texture);
                }
            }
        }
        return textures;
    }
    static long GetTextureStorageMemory(Texture texture)
    {
        var type = typeof(EditorGUI).Assembly.GetType("UnityEditor.TextureUtil");
        MethodInfo methodInfo = type.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
        return (long)methodInfo.Invoke(null, new object[] { texture });
    }
    static Vector2Int GetPlatformTextureSize(Texture texture, string platform)
    {
        var assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath)) return new Vector2Int(texture.width, texture.height);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return new Vector2Int(texture.width, texture.height);
        int platformMaxTextureSize = 0;
        if (!importer.GetPlatformTextureSettings(platform, out platformMaxTextureSize, out _, out _, out _))
        {
            platformMaxTextureSize = importer.GetDefaultPlatformTextureSettings().maxTextureSize;
        }
        var args = new object[2] { 0, 0 };
        var mi = typeof(TextureImporter).GetMethod("GetWidthAndHeight", BindingFlags.NonPublic | BindingFlags.Instance);
        mi.Invoke(importer, args);
        int originWidth = (int)args[0];
        int originHeight = (int)args[1];
        int originMaxSize = Mathf.Max(originWidth, originHeight);
        int settingsMaxSize = Mathf.Min(platformMaxTextureSize, originMaxSize);
        return new Vector2Int(originWidth, originHeight) * settingsMaxSize / originMaxSize;
    }
    static long GetAnimationClipMemory(AnimationClip clip)
    {
        return Profiler.GetRuntimeMemorySizeLong(clip) / 2;
    }
    static Dictionary<GameObject, List<AnimationClip>> GetDependencyAnimationClips(GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive)
    {
        var clipHashSet = new HashSet<AnimationClip>();
        var clips = new Dictionary<GameObject, List<AnimationClip>>();
        var animators = gameObject.GetComponentsInChildrenAdvanced<Animator>(includePaths, excludePaths, includeInactive);
        foreach (var animator in animators)
        {
            var ac = animator.runtimeAnimatorController;
            if (ac != null)
            {
                foreach (var clip in ac.animationClips)
                {
                    if (clip == null) continue;
                    if (clipHashSet.Contains(clip)) continue;
                    if (!clips.ContainsKey(animator.gameObject))
                    {
                        clips[animator.gameObject] = new List<AnimationClip>();
                    }
                    clips[animator.gameObject].Add(clip);
                    clipHashSet.Add(clip);
                }
            }
        }
        return clips;
    }
    static Dictionary<GameObject, List<Material>> GetDependencyMaterials(GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive)
    {
        var materialHashSet = new HashSet<Material>();
        var materials = new Dictionary<GameObject, List<Material>>();
        var renderers = gameObject.GetComponentsInChildrenAdvanced<Renderer>(includePaths, excludePaths, includeInactive);
        foreach (var renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0) continue;
            foreach (var sharedMaterial in renderer.sharedMaterials)
            {
                if (sharedMaterial == null) continue;
                if (materialHashSet.Contains(sharedMaterial)) continue;
                if (!materials.ContainsKey(renderer.gameObject))
                {
                    materials[renderer.gameObject] = new List<Material>();
                }
                materials[renderer.gameObject].Add(sharedMaterial);
                materialHashSet.Add(sharedMaterial);
            }
        }
        return materials;
    }
    static Dictionary<GameObject, List<AudioClip>> GetDependencyAudioClips(GameObject gameObject, string[] includePaths, string[] excludePaths, bool includeInactive)
    {
        var audioHashSet = new HashSet<AudioClip>();
        var audios = new Dictionary<GameObject, List<AudioClip>>();
        var audioSources = gameObject.GetComponentsInChildrenAdvanced<AudioSource>(includePaths, excludePaths, includeInactive);
        foreach (var audioSource in audioSources)
        {
            if (audioSource.clip != null)
            {
                if (audioHashSet.Contains(audioSource.clip)) continue;
                if (!audios.ContainsKey(audioSource.gameObject))
                {
                    audios[audioSource.gameObject] = new List<AudioClip>();
                }
                audios[audioSource.gameObject].Add(audioSource.clip);
                audioHashSet.Add(audioSource.clip);
            }
        }
        return audios;
    }
}