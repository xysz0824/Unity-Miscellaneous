using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditor.Animations;
using Object = UnityEngine.Object;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("GameObject", "物体Tag白名单")]
    public static bool TagWhiteList([ExceptModel] GameObject gameObject, string[] tags)
    {
        var result = Array.Exists(tags, (tag) => (gameObject.tag.Equals(tag)));
        if (!result)
        {
            AddLog("该物体Tag不在白名单中");
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查物体是否active")]
    public static bool IsActive([ExceptModel] GameObject gameObject)
    {
        AddLog(gameObject.activeSelf ? "该物体处于active状态" : "该物体处于inactive状态");
        return gameObject.activeSelf;
    }
    [AssetClearanceMethod("GameObject", "检查丢失脚本")]
    public static bool HaveMissingScript([ExceptModel] GameObject gameObject)
    {
        var hasMissingScript = false;
        var transforms = gameObject.GetComponentsInChildren<Transform>(true);
        foreach (var transform in transforms)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
            {
                AddLog("该物体存在丢失脚本", 0, transform.gameObject);
                hasMissingScript = true;
            }
        }
        return hasMissingScript;
    }
    [AssetClearanceMethod("GameObject", "检查丢失Prefab")]
    public static bool HaveMissingPrefab([ExceptModel] GameObject gameObject)
    {
        bool isError = false;
        var transforms = gameObject.GetComponentsInChildren<Transform>(true);
        foreach (var transform in transforms)
        {
            if (transform.name.Contains("Missing Prefab") ||
                PrefabUtility.IsPrefabAssetMissing(transform.gameObject) ||
                PrefabUtility.IsDisconnectedFromPrefabAsset(transform.gameObject))
            {
                AddLog("该物体存在丢失Prefab", 0, transform.gameObject);
                isError = true;
            }
        }
        return isError;
    }
    [AssetClearanceMethod("GameObject", "检查未激活物体")]
    public static bool HaveInactive([ExceptModel] GameObject gameObject, string[] includePaths, string[] excludePaths)
    {
        bool haveInactive = false;
        var transforms = gameObject.GetComponentsInChildrenAdvanced<Transform>(includePaths, excludePaths, true);
        foreach (var transform in transforms)
        {
            if (!transform.gameObject.activeSelf)
            {
                AddLog("该物体未激活", 0, transform.gameObject);
                haveInactive = true;
            }
        }
        return haveInactive;
    }
    [AssetClearanceMethod("GameObject", "检查空物体")]
    public static bool HaveEmpty([ExceptModel] GameObject gameObject, bool ignoreBones, string[] includePaths, string[] excludePaths)
    {
        bool haveEmpty = false;
        var transforms = gameObject.GetComponentsInChildrenAdvanced<Transform>(includePaths, excludePaths, true);
        foreach (var transform in transforms)
        {
            if (transform.childCount == 0 && transform.GetComponents<Component>().Length == 1)
            {
                if (ignoreBones && IsBone(gameObject, transform.gameObject))
                {
                    continue;
                }
                AddLog("该物体为空", 0, transform.gameObject);
                haveEmpty = true;
            }
        }
        return haveEmpty;
    }
    [AssetClearanceMethod("GameObject", "统计子物体数量不超过指定数量")]
    public static bool ChildrenCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        var transforms = gameObject.GetComponentsInChildrenAdvanced<Transform>(includePaths, excludePaths, true);
        bool result = transforms.Length <= maxCount;
        if (!result)
        {
            AddLog($"子物体数量{transforms.Length}超过指定数量{maxCount}", transforms.Length);
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查组件是否存在（包含子物体）")]
    public static bool IsComponentExistInChildren([ExceptModel] GameObject gameObject, [EnsureComponent] string componentName, string[] includePaths, string[] excludePaths)
    {
        bool hasComp = false;
        var transforms = gameObject.GetComponentsInChildrenAdvanced<Transform>(includePaths, excludePaths, true);
        foreach (var transform in transforms)
        {
            if (transform.GetComponent(componentName) != null)
            {
                AddLog($"存在组件{componentName}", 0, transform.gameObject);
                hasComp = true;
            }
        }
        return hasComp;
    }
    [AssetClearanceMethod("GameObject", "组件白名单")]
    public static bool ComponentWhiteList([ExceptModel] GameObject gameObject, [EnsureComponent] string[] componentNames, string[] includePaths, string[] excludePaths)
    {
        var components = gameObject.GetComponentsInChildrenAdvanced<Component>(includePaths, excludePaths, true);
        bool pass = true;
        foreach (var component in components)
        {
            if (component == null || component is Transform) continue;
            var componentName = component.GetType().Name;
            if (!Array.Exists(componentNames, (name) => componentName.Equals(name)))
            {
                pass = false;
                AddLog($"存在白名单之外的组件{componentName}", 0, component.gameObject);
            }
        }
        return pass;
    }
    private static char[] sSplitCharArr = new char[] { ',' };
    //高级复杂检查项，检查组件是否在白名单，是否满足有且仅有，是否有最大数量限制
    [AssetClearanceMethod("GameObject", "组件白名单,支持有且仅有个，数量上限控制")]
    public static bool IsComponentsTypeAndCountOK([ExceptModel] GameObject gameObject, [EnsureComponent] string[] componentNames, string[] countLimits, string[] includePaths, string[] excludePaths)
    {
        if (componentNames.Length != countLimits.Length) return false;
        var components = gameObject.GetComponentsInChildrenAdvanced<Component>(includePaths, excludePaths, true);
        bool isCountOK = true;
        for (int i = 0; i < componentNames.Length; i++)
        {
            var allowCompName = componentNames[i];
            var maxCountLimit = countLimits[i].Split(sSplitCharArr, StringSplitOptions.None);
            int minCount = Convert.ToInt32(maxCountLimit[0]);
            int maxCount = Convert.ToInt32(maxCountLimit[1]);
            var comps = components.Where(c => c.GetType().Name.Equals(allowCompName)).ToList();
            var count = comps.Count;
            if (count < minCount || count > maxCount)
            {
                isCountOK = false;
                var tip = $"数量非法的组件：{allowCompName}数量{count}不满足[{minCount},{maxCount}] :{GetRelativePathForTip(includePaths)}";
                int order = count;
                AddLog(tip, order);
                foreach (var comp in comps)
                {
                    AddLog($"{allowCompName}+1", order, comp.gameObject);
                }
            }
        }
        //白名单检查
        bool isTypeOK = true;
        foreach (var component in components)
        {
            if (component == null || component is Transform) continue;
            var componentName = component.GetType().Name;
            if (!Array.Exists(componentNames, (name) => (componentName.Equals(name))))
            {
                isTypeOK = false;
                AddLog($"存在白名单之外的组件{componentName}", 0,component.gameObject);
            }
        }
        return isTypeOK & isCountOK;
    }
    [AssetClearanceMethod("GameObject", "统计指定组件数量不超过指定数量")]
    public static bool ComponentCount([ExceptModel] GameObject gameObject, [EnsureComponent] string componentName, int maxCount, bool includeTransform, string[] includePaths, string[] excludePaths)
    {
        int componentCount = 0;
        var transforms = gameObject.GetComponentsInChildrenAdvanced<Transform>(includePaths, excludePaths, true);
        foreach (var transform in transforms)
        {
            if (componentName == "Component")
            {
                var components = transform.GetComponents<Component>();
                if (includeTransform) //统计时带上Transform
                {
                    componentCount += components.Length;
                }
                else //统计时忽略Transform
                {
                    componentCount += Mathf.Max(0, components.Length - 1);
                }
            }
            else if (transform.GetComponent(componentName) != null)
            {
                componentCount++;
            }
        }
        bool result = componentCount <= maxCount;
        if (!result)
        {
            AddLog($"指定组件{componentName}数量{componentCount}超过指定数量{maxCount}", componentCount);
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查是否存在Disabled组件的子物体")]
    public static bool DumpDisabledComponents([ExceptModel] GameObject gameObject, string[] includePaths, string[] excludePaths)
    {
        bool haveDisabled = false;
        var transforms = gameObject.GetComponentsInChildrenAdvanced<Transform>(includePaths, excludePaths, true);
        foreach (var transform in transforms)
        {
            var components = transform.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component is Transform) continue;
                var enabledProperty = component.GetType().GetProperty("enabled");
                if (enabledProperty != null && !(bool)enabledProperty.GetValue(component))
                {
                    var componentName = component.GetType().Name;
                    AddLog($"该物体含有Disabled组件{componentName}", 0, component.gameObject);
                    haveDisabled = true;
                }
            }
        }
        return haveDisabled;
    }
    [AssetClearanceMethod("GameObject", "检查Animator是否不为空")]
    public static bool AnimatorNotEmpty([ExceptModel] GameObject gameObject, string[] includePaths, string[] excludePaths)
    {
        bool noEmpty = true;
        var animators = gameObject.GetComponentsInChildrenAdvanced<Animator>(includePaths, excludePaths, true);
        foreach (var animator in animators)
        {
            if (animator.runtimeAnimatorController == null)
            {
                AddLog("该物体上的Animator组件的AnimatorController为空", 0, animator.gameObject);
                noEmpty = false;
            }
        }
        return noEmpty;
    }
    [AssetClearanceMethod("GameObject", "检查AudioSource是否不为空")]
    public static bool AudioSourceNotEmpty([ExceptModel] GameObject gameObject)
    {
        bool noEmpty = true;
        var audioSources = gameObject.GetComponentsInChildren<AudioSource>(true);
        foreach (var audioSource in audioSources)
        {
            if (audioSource.clip == null)
            {
                AddLog("该物体上的AudioSource组件的Clip为空", 0, audioSource.gameObject);
                noEmpty = false;
            }
        }
        return noEmpty;
    }
    [AssetClearanceMethod("GameObject", "检查Animator的Renderer是否不为空")]
    public static bool AnimatorRendererNotEmpty([ExceptModel] GameObject gameObject, bool meshRenderer, bool skinnedMeshRenderer, string[] includePaths, string[] excludePaths)
    {
        bool noEmpty = true;
        if (!meshRenderer && !skinnedMeshRenderer) return noEmpty;
        var animators = gameObject.GetComponentsInChildrenAdvanced<Animator>(includePaths, excludePaths, true);
        foreach (var animator in animators)
        {
            if (!((meshRenderer && animator.GetComponentInChildren<MeshRenderer>()) || (skinnedMeshRenderer && animator.GetComponentInChildren<SkinnedMeshRenderer>())))
            {
                AddLog("该物体含有Animator组件但其下面无Renderer", 0, animator.gameObject);
                noEmpty = false;
            }
        }
        return noEmpty;
    }
    [AssetClearanceMethod("GameObject", "检查Animator是否符合规范")]
    public static bool CheckAnimator([ExceptModel] GameObject gameObject, AnimatorCullingMode preferMode, string[] includePaths, string[] excludePaths)
    {
        bool noProblem = true;
        var problemsLog = new StringBuilder();
        var animators = gameObject.GetComponentsInChildrenAdvanced<Animator>(includePaths, excludePaths, true);
        foreach (var animator in animators)
        {
            if (animator.cullingMode != preferMode)
            {
                AddLog($"Animator的CullingMode建议为{preferMode}", 0, animator.gameObject);
                noProblem = false;
            }
        }
        return noProblem;
    }
    [AssetClearanceMethod("GameObject", "检查MeshFilter的Renderer是否符合规范")]
    public static bool CheckMeshFilterAndMeshRenderer([ExceptModel] GameObject gameObject, int materialLimit, bool allowCastShadow, string ignoreNodeNamePrefix, string[] includePaths, string[] excludePaths)
    {
        bool isError = false;
        var meshFilters = gameObject.GetComponentsInChildrenAdvanced<MeshFilter>(includePaths, excludePaths, true);
        foreach (var meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                isError = true;
                AddLog("MeshFilter缺少Mesh! ", 0, meshFilter.gameObject);
            }
            var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                isError = true;
                AddLog("没有MeshRenderer! ", 0, meshFilter.gameObject);
            }
            else if (HasLostMaterial(meshRenderer.sharedMaterials))
            {
                isError = true;
                AddLog("没材质! ", 0, meshFilter.gameObject);
            }
            else if (meshRenderer.sharedMaterials.Length > materialLimit)
            {
                isError = true;
                AddLog($"材质数量超过限制 matCount:{meshRenderer.sharedMaterials.Length} ", 0, meshFilter.gameObject);
            }
            else if (!allowCastShadow && meshRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                isError = true;
                AddLog($"不允许开启CastShadow", 0, meshFilter.gameObject);
            }
            if (meshRenderer != null && meshFilter.sharedMesh != null)
            {
                if (meshRenderer.sharedMaterials.Length != meshFilter.sharedMesh.subMeshCount)
                {
                    isError = true;
                    AddLog($"材质数量与子网格数量不一致 matCount:{meshRenderer.sharedMaterials.Length} subMeshCount:{meshFilter.sharedMesh.subMeshCount} ", 0, meshFilter.gameObject);
                }
            }
        }
        return !isError;
    }
    [AssetClearanceMethod("GameObject", "检查SkinnedMeshRenderer是否符合规范")]
    public static bool CheckSkinnedMeshRenderer([ExceptModel] GameObject gameObject, int materialLimit, bool allowCastShadow, int boneLimit, bool allowUpdateWhenOffScreen, string[] includePaths, string[] excludePaths)
    {
        bool isError = false;
        var meshRenderers = gameObject.GetComponentsInChildrenAdvanced<SkinnedMeshRenderer>(includePaths, excludePaths, true);
        foreach (var meshRenderer in meshRenderers)
        {
            if (meshRenderer.sharedMesh == null)
            {
                isError = true;
                AddLog("Mesh丢失 ", 0, meshRenderer.gameObject);
            }
            if (HasLostMaterial(meshRenderer.sharedMaterials))
            {
                isError = true;
                AddLog("没材质 ", 0, meshRenderer.gameObject);
            }
            else if (meshRenderer.sharedMaterials.Length > materialLimit)
            {
                isError = true;
                AddLog("材质数量" + meshRenderer.sharedMaterials.Length + "已超过限制 ", 0, meshRenderer.gameObject);
            }
            if (!allowUpdateWhenOffScreen && meshRenderer.updateWhenOffscreen)
            {
                isError = true;
                AddLog("开启了updateWhenOffscreen ", 0, meshRenderer.gameObject);
            }
            if (!allowCastShadow && meshRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
            {
                isError = true;
                AddLog("开启了CastShadow ", 0, meshRenderer.gameObject);
            }
            if (meshRenderer.bones.Length > boneLimit)
            {
                isError = true;
                AddLog("骨骼数量" + meshRenderer.bones.Length + "已超过限制 ", meshRenderer.bones.Length, meshRenderer.gameObject);
            }
        }
        return !isError;
    }
    [AssetClearanceMethod("GameObject", "检查ParticleSystemRenderer(丢材质球，材质球数量超标)是否符合规范")]
    public static bool CheckParticleSystemRenderer([ExceptModel] GameObject gameObject, int materialLimit, int meshTriCountLimit, string[] includePaths, string[] excludePaths)
    {
        bool isError = false;
        var particleSystems = gameObject.GetComponentsInChildrenAdvanced<ParticleSystem>(includePaths, excludePaths, true);
        foreach (var particleSystem in particleSystems)
        {
            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleSystem.trails.enabled)
            {
                if (renderer.trailMaterial == null)
                {
                    isError = true;
                    AddLog("Trail材质丢失 ", 0, particleSystem.gameObject);
                }
                else if (renderer.trailMaterial.renderQueue < 3000)
                {
                    isError = true;
                    AddLog($"Trail材质球renderQueue小于3000 count:{renderer.trailMaterial.renderQueue}", 0, particleSystem.gameObject);
                }
            }
            if (renderer.enabled && HasLostMaterial(renderer.sharedMaterials))
            {
                isError = true;
                AddLog("材质丢失 ", 0, particleSystem.gameObject);
            }
            else if (renderer.sharedMaterials.Length > materialLimit)
            {
                isError = true;
                AddLog($"材质数量超过限制 count:{renderer.sharedMaterials.Length} ", renderer.sharedMaterials.Length, particleSystem.gameObject);
            }
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh)
            {
                if (renderer.mesh != null)
                {
                    int triCount = renderer.mesh.triangles.Length / 3;
                    if (triCount > meshTriCountLimit)
                    {
                        isError = true;
                        AddLog($"网格面数超限制 count:{triCount} ", triCount, particleSystem.gameObject);
                    }
                }
            }
            if (renderer.sharedMaterials != null)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        if (mat.renderQueue < 3000)
                        {
                            isError = true;
                            AddLog($"材质球renderQueue小于3000 count:{mat.renderQueue} ", 0, particleSystem.gameObject);
                        }
                    }
                }
            }
        }
        return !isError;
    }
    [AssetClearanceMethod("GameObject", "检查ReflectionProbe是否符合规范")]
    public static bool CheckReflectionProbe([ExceptModel] GameObject gameObject, bool allowRealtime, int resolution, string[] includePaths, string[] excludePaths)
    {
        bool noProblem = true;
        var reflectionProbes = gameObject.GetComponentsInChildrenAdvanced<ReflectionProbe>(includePaths, excludePaths, true);
        foreach (var reflectionProbe in reflectionProbes)
        {
            if (!allowRealtime && reflectionProbe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
            {
                AddLog("该物体上的ReflectionProbe开启了实时反射", 0, reflectionProbe.gameObject);
                noProblem = false;
            }
            if (reflectionProbe.resolution > resolution)
            {
                AddLog($"该物体上的ReflectionProbe分辨率{reflectionProbe.resolution}大于设定值{resolution}", reflectionProbe.resolution, reflectionProbe.gameObject);
                noProblem = false;
            }
        }
        return noProblem;
    }
    [AssetClearanceMethod("GameObject", "检查Light是否符合规范")]
    public static bool CheckLight([ExceptModel] GameObject gameObject, bool checkRealtimeOnly, int maxTotalCount, bool allowShadow, int directionalLimit, int pointLimit, int spotLimit, int importanceLimit, 
        string[] includePaths, string[] excludePaths)
    {
        bool noProblem = true;
        var lights = gameObject.GetComponentsInChildrenAdvanced<Light>(includePaths, excludePaths, true);
        if (lights.Length > maxTotalCount)
        {
            AddLog($"灯光总量{lights.Length}超过指定数量{maxTotalCount}", lights.Length);
            noProblem = false;
        }
        int directionalCount = 0;
        int pointCount = 0;
        int spotCount = 0;
        int importanceCount = 0;
        foreach (var light in lights)
        {
            if (checkRealtimeOnly && light.lightmapBakeType == LightmapBakeType.Baked)
            {
                continue;
            }
            if (light.type == LightType.Directional)
            {
                directionalCount++;
            }
            else if (light.type == LightType.Point)
            {
                pointCount++;
            }
            else if (light.type == LightType.Spot)
            {
                spotCount++;
            }
            if (light.renderMode == LightRenderMode.ForcePixel)
            {
                importanceCount++;
            }
        }
        if (directionalCount > directionalLimit)
        {
            AddLog($"DirectionalLight数量为{directionalCount}大于限制值{directionalLimit}", directionalCount);
            noProblem = false;
        }
        if (pointCount > pointLimit)
        {
            AddLog($"PointLight数量为{pointCount}大于限制值{pointLimit}", pointCount);
            noProblem = false;
        }
        if (spotCount > spotLimit)
        {
            AddLog($"SpotLight数量为{spotCount}大于限制值{spotLimit}", spotCount);
            noProblem = false;
        }
        if (importanceCount > importanceLimit)
        {
            AddLog($"Important Light数量为{importanceCount}大于限制值{importanceLimit}", importanceCount);
            noProblem = false;
        }
        foreach (var light in lights)
        {
            if (checkRealtimeOnly && light.lightmapBakeType == LightmapBakeType.Baked)
            {
                continue;
            }
            if (light.type == LightType.Directional || 
                light.type == LightType.Point || 
                light.type == LightType.Spot || 
                light.renderMode == LightRenderMode.ForcePixel)
            {
                AddLog($"该物体上的Light为{light.type}，其RenderMode为{light.renderMode}", 0, light.gameObject);
            }
            if (!allowShadow && light.shadows != LightShadows.None)
            {
                AddLog("该物体上的Light开启了实时影子", 0, light.gameObject);
                noProblem = false;
            }
        }
        return noProblem;
    }
    [AssetClearanceMethod("GameObject", "检查AudioSource是否符合规范")]
    public static bool CheckAudioSource([ExceptModel] GameObject gameObject, int clipSecondsLimit, string[] includePaths, string[] excludePaths)
    {
        bool noProblem = true;
        var audioSources = GetDependencyAudioClips(gameObject, includePaths, excludePaths, true);
        foreach (var kv in audioSources)
        {
            var referencer = kv.Key;
            var audioList = kv.Value;
            foreach (var audio in audioList)
            {
                if (audio.length > clipSecondsLimit)
                {
                    AddLog($"音频片段{audio.name}长度{audio.length}s超过指定长度{clipSecondsLimit}s", (int)(audio.length * 100), referencer);
                    noProblem = false;
                }
                //放弃获取音频文件实际导入大小，因为找不到能用的相关接口，使用Profiler.GetRuntimeMemorySizeLong得不到正确结果
                //var memory = GetAudioClipImportedSize(audio);
                //if (memory > clipMemoryLimit * 1024 * 1024)
                //{
                //    AddLog($"AudioSource的音频片段{audio.name}在当前平台占用内存{FormatBytes(memory)}MB超过指定大小{clipMemoryLimit}MB", referencer);
                //    noProblem = false;
                //}
            }
        }
        return noProblem;
    }
    [AssetClearanceMethod("GameObject", "检查是否有重叠的渲染物体")]
    public static bool CheckOverlapping([ExceptModel] GameObject gameObject)
    {
        var transforms = gameObject.GetComponentsInChildren<Transform>(true);
        var selectTransforms = new List<Transform>();
        foreach (var transform in transforms)
        {
            Mesh mesh = null;
            var meshFilter = transform.GetComponent<MeshFilter>();
            var skinnedMeshRenderer = transform.GetComponent<SkinnedMeshRenderer>();
            if (meshFilter != null && meshFilter.sharedMesh != null) mesh = meshFilter.sharedMesh;
            else if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null) mesh = skinnedMeshRenderer.sharedMesh;
            if (mesh == null) continue;
            selectTransforms.Add(transform);
        }
        var matches = new List<KeyValuePair<Transform, Transform>>();
        for (int i = 0; i < selectTransforms.Count; ++i)
        {
            for (int k = i + 1; k < selectTransforms.Count; ++k)
            {
                var a = selectTransforms[i];
                var b = selectTransforms[k];
                if (matches.Exists((match) => (match.Key == a && match.Value == b) || (match.Key == b && match.Value == a)))
                {
                    continue;
                }
                var meshFilter = a.GetComponent<MeshFilter>();
                var skinnedMeshRenderer = a.GetComponent<SkinnedMeshRenderer>();
                var meshA = meshFilter != null ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
                meshFilter = b.GetComponent<MeshFilter>();
                skinnedMeshRenderer = b.GetComponent<SkinnedMeshRenderer>();
                var meshB = meshFilter != null ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
                if (meshA != meshB) continue;
                if (Vector3.Distance(a.position, b.position) < 0.001f && Vector3.Distance(a.localScale, b.localScale) < 0.001f &&
                    Vector3.Distance(a.eulerAngles, b.eulerAngles) < 0.001f)
                {
                    matches.Add(new KeyValuePair<Transform, Transform>(a, b));
                }
            }
        }
        bool haveOverlapping = false;
        foreach (var match in matches)
        {
            AddLog($"该物体与{match.Value.gameObject.name}存在重叠问题", 0, match.Key.gameObject);
            AddLog($"该物体与{match.Key.gameObject.name}存在重叠问题", 0, match.Value.gameObject);
            haveOverlapping = true;
        }
        return haveOverlapping;
    }
    [AssetClearanceMethod("GameObject", "预估Drawcall数量不超过指定数量（不统计粒子渲染器）")]
    public static bool EstimateDrawcalls([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        var count = 0;
        var renderers = gameObject.GetComponentsInChildrenAdvanced<Renderer>(includePaths, excludePaths);
        var drawcallDict = new Dictionary<GameObject, int>();
        foreach (var renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer) continue;
            var sharedMaterials = renderer.sharedMaterials;
            var counted = false;
            var subCount = 0;
            for (int i = 0; i < sharedMaterials.Length; ++i)
            {
                if (sharedMaterials[i] == null) continue;
                subCount++;
                counted = true;
            }
            count += subCount;
            if (counted)
            {
                if (!drawcallDict.ContainsKey(renderer.gameObject))
                {
                    drawcallDict[renderer.gameObject] = 0;
                }
                drawcallDict[renderer.gameObject] = subCount;
            }
        }
        bool result = count <= maxCount;
        if (!result)
        {
            int order = count;
            AddLog($"预估Drawcall数量{count}已超过指定数量{maxCount}", order);
            foreach (var kv in drawcallDict)
            {
                AddLog($"该物体计入{kv.Value}个Drawcall", order, kv.Key);
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下网格单个面数是否不超过指定数量")]
    public static bool GameObjectMeshSingleTriCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var meshes = GetDependencyMeshes(gameObject, includePaths, excludePaths, true, true);
        foreach (var kv in meshes)
        {
            var referencer = kv.Key;
            var mesh = kv.Value;
            var count = mesh.triangles.Length / 3;
            if (count > maxCount)
            {
                AddLog($"网格{mesh.name}面数{count}超过指定数量{maxCount}", count, referencer, mesh);
                ok = false;
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下所有网格总面数是否不超过指定数量")]
    public static bool GameObjectMeshTotalTriCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        int totalCount = 0;
        var meshes = GetDependencyMeshes(gameObject, includePaths, excludePaths, true, false);
        foreach (var kv in meshes)
        {
            var mesh = kv.Value;
            totalCount += mesh.triangles.Length / 3;
        }
        bool result = totalCount <= maxCount;
        if (!result)
        {
            int order = totalCount;
            AddLog($"网格总面数{totalCount}超过指定数量{maxCount}", totalCount);
            foreach (var kv in meshes)
            {
                var referencer = kv.Key;
                var mesh = kv.Value;
                AddLog($"引用了网格{mesh.name}面数{mesh.triangles.Length / 3}", totalCount, referencer, mesh);
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下网格单个占用内存是否不超过指定大小（MB）")]
    public static bool GameObjectMeshSingleMemory([ExceptModel] GameObject gameObject, float memoryLimit, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var meshes = GetDependencyMeshes(gameObject, includePaths, excludePaths, true, true);
        foreach (var kv in meshes)
        {
            var referencer = kv.Key;
            var mesh = kv.Value;
            var size = GetMeshMemory(mesh);
            if (size > memoryLimit * 1024 * 1024)
            {
                AddLog($"网格{mesh.name}占用内存{FormatBytes(size)}MB超过指定大小{memoryLimit}MB", (int)(size / 1024 / 1024), referencer, mesh);
                ok = false;
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下所有网格占用内存是否不超过指定大小（MB）")]
    public static bool GameObjectMeshTotalMemory([ExceptModel] GameObject gameObject, float memoryLimit, string[] includePaths, string[] excludePaths)
    {
        var meshes = GetDependencyMeshes(gameObject, includePaths, excludePaths, true, false);
        long runtimeMemory = 0;
        foreach (var kv in meshes)
        {
            var mesh = kv.Value;
            var size = GetMeshMemory(mesh);
            runtimeMemory += size;
        }
        bool result = runtimeMemory <= memoryLimit * 1024 * 1024;
        if (!result)
        {
            int order = (int)(runtimeMemory / 1024 / 1024);
            AddLog($"网格占用内存{FormatBytes(runtimeMemory)}MB超过指定大小{memoryLimit}MB", order);
            foreach (var kv in meshes)
            {
                var referencer = kv.Key;
                var mesh = kv.Value;
                AddLog($"引用了网格{mesh.name}占用内存{FormatBytes(GetMeshMemory(mesh))}MB", order, referencer, mesh);
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下模型网格是否不含特定信息")]
    public static bool GameObjectMeshInfo([ExceptModel] GameObject gameObject, bool uv2, bool uv3_8, bool color, bool normal, bool tangent, string[] includePaths, string[] excludePaths)
    {
        var meshes = GetDependencyMeshes(gameObject, includePaths, excludePaths, true, true);
        bool ok = true;
        foreach (var kv in meshes)
        {
            var referencer = kv.Key;
            var mesh = kv.Value;
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
                AddLog($"模型网格{mesh.name}含有{infoStringBuilder.ToString()}", 0, referencer, mesh);
                ok = false;
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下模型所有网格引用次数是否不超过指定数量")]
    public static bool GameObjectMeshReferenceCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var meshes = GetDependencyMeshes(gameObject, includePaths, excludePaths, true, false);
        var refDict = new Dictionary<Mesh, int>();
        foreach (var kv in meshes)
        {
            var referencer = kv.Key;
            var mesh = kv.Value;
            if (!refDict.ContainsKey(mesh))
            {
                refDict[mesh] = 0;
            }
            refDict[mesh]++;
        }
        foreach (var kv in refDict)
        {
            var mesh = kv.Key;
            var refCount = kv.Value;
            if (refCount > maxCount)
            {
                var referencer = meshes.Where(i => i.Value == mesh).Select(i => i.Key).First();
                AddLog($"网格{mesh.name}引用次数{refCount}超过指定数量{maxCount}", refCount, referencer, mesh);
                ok = false;
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下模型是否导入特定Shader的材质")]
    public static bool GameObjectModelMaterial([ExceptModel] GameObject gameObject, [EnsureShader] string[] materialShaders, string[] includePaths, string[] excludePaths)
    {
        var models = GetDependencyModels(gameObject, includePaths, excludePaths, true);
        bool imported = false;
        foreach (var kv in models)
        {
            var referencer = kv.Key;
            var model = kv.Value;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterials == null) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (Array.Exists(materialShaders, (shaderName) => material.shader.name == shaderName))
                    {
                        AddLog($"该模型导入特定Shader\"{material.shader.name}\"的材质", 0, referencer, model);
                        imported = true;
                    }
                }
            }
        }
        return imported;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下贴图导入格式是否符合要求")]
    public static bool GameObjectTexturePlatformFormat([ExceptModel] GameObject gameObject, [EnsurePlatform] string platform, TextureImporterFormat desiredFormat, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        foreach (var kv in textures)
        {
            var referencer = kv.Key;
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var assetPath = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(assetPath)) continue;
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                TextureImporterFormat format = default;
                if (!importer.GetPlatformTextureSettings(platform, out _, out format, out _, out _))
                {
                    format = importer.GetDefaultPlatformTextureSettings().format;
                }
                if (format != desiredFormat)
                {
                    AddLog($"该贴图导入格式{format}不符合要求格式{desiredFormat}", 0, referencer, texture);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下贴图导入压缩设置是否符合要求")]
    public static bool GameObjectTexturePlatformCompression([ExceptModel] GameObject gameObject, [EnsurePlatform] string platform, TextureImporterCompression compression, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        foreach (var kv in textures)
        {
            var referencer = kv.Key;
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var assetPath = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(assetPath)) continue;
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings == null)
                {
                    settings = importer.GetDefaultPlatformTextureSettings();
                }
                if (settings.textureCompression != compression)
                {
                    AddLog($"该贴图压缩设置{settings.textureCompression}不符合要求设置{compression}", 0, referencer, texture);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下贴图是否带有Alpha通道信息")]
    public static bool GameObjectDoesTextureHasAlpha([ExceptModel] GameObject gameObject, string[] includePaths, string[] excludePaths)
    {
        bool haveAlpha = false;
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        foreach (var kv in textures)
        {
            var referencer = kv.Key;
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                if (path == null) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.DoesSourceTextureHaveAlpha() && importer.alphaSource != TextureImporterAlphaSource.None)
                {
                    AddLog($"贴图带有Alpha通道信息", 0, referencer, texture);
                    haveAlpha = true;
                }
            }
        }
        return haveAlpha;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下单张贴图在当前平台占用内存是否不超过指定大小（MB）")]
    public static bool GameObjectTextureSingleMemory([ExceptModel] GameObject gameObject, float memoryLimit, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        foreach (var kv in textures)
        {
            var referencer = kv.Key;
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var memory = GetTextureStorageMemory(texture);
                if (memory > memoryLimit * 1024 * 1024)
                {
                    AddLog($"该贴图占用内存{FormatBytes(memory)}MB超过指定大小{memoryLimit}MB", (int)(memory / 1024 / 1024), referencer, texture);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下所有贴图在当前平台占用内存是否不超过指定大小（MB）")]
    public static bool GameObjectTextureTotalMemory([ExceptModel] GameObject gameObject, float memoryLimit, string[] includePaths, string[] excludePaths)
    {
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        long storageMemory = 0;
        foreach (var kv in textures)
        {
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var memory = GetTextureStorageMemory(texture);
                storageMemory += memory;
            }
        }
        bool result = storageMemory <= memoryLimit * 1024 * 1024;
        if (!result)
        {
            int order = (int)(storageMemory / 1024 / 1024);
            AddLog($"当前平台贴图占用内存{FormatBytes(storageMemory)}MB超过指定大小{memoryLimit}MB", order);
            foreach (var kv in textures)
            {
                var referencer = kv.Key;
                var textureList = kv.Value;
                foreach (var texture in textureList)
                {
                    AddLog($"引用了贴图{texture.name}占用内存{FormatBytes(GetTextureStorageMemory(texture))}MB", order, referencer, texture);
                }
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下所有贴图数量不超过指定数量")]
    public static bool GameObjectTextureCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        var count = textures.Sum(i => i.Value.Count);
        bool result = count <= maxCount;
        if (!result)
        {
            int order = count;
            AddLog($"贴图数量{count}超过指定数量{maxCount}", order);
            foreach (var kv in textures)
            {
                var referencer = kv.Key;
                var subCount = kv.Value.Count;
                AddLog($"引用了不同贴图{subCount}张", order, referencer);
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下单张贴图宽高尺寸不超过指定大小")]
    public static bool GameObjectTextureSize([ExceptModel] GameObject gameObject, [EnsurePlatform] string platform, int width, int height, string[] includePaths, string[] excludePaths)
    {
        bool ok = true;
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        foreach (var kv in textures)
        {
            var referencer = kv.Key;
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var platformSize = GetPlatformTextureSize(texture, platform);
                if (!(platformSize.x <= width && platformSize.y <= height))
                {
                    if (platformSize.x > width && platformSize.y > height)
                    {
                        AddLog($"贴图宽高{platformSize.x}x{platformSize.y}超过指定大小{width}x{height}", platformSize.x * platformSize.y, referencer, texture);
                    }
                    else if (platformSize.x > width)
                    {
                        AddLog($"贴图宽{platformSize.x}超过指定大小{width}", platformSize.x, referencer, texture);
                    }
                    else
                    {
                        AddLog($"贴图高{platformSize.y}超过指定大小{height}", platformSize.y, referencer, texture);
                    }
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下贴图面积当量（2的n次方）不超过指定数量")]
    public static bool GameObjectTextureAreaCount([ExceptModel] GameObject gameObject, [EnsurePlatform] string platform, int size, int maxCount, string[] includePaths, string[] excludePaths)
    {
        var textures = GetDependencyTextures(gameObject, includePaths, excludePaths, true);
        int area = 0;
        foreach (var kv in textures)
        {
            var textureList = kv.Value;
            foreach (var texture in textureList)
            {
                var platformSize = GetPlatformTextureSize(texture, platform);
                area += platformSize.x * platformSize.y;
            }
        }
        size = Mathf.Max(2, (int)Mathf.Pow(2, (int)Mathf.Log(size, 2)));
        float areaCount = area / (size * size);
        bool result = areaCount <= maxCount;
        if (!result)
        {
            AddLog($"贴图面积当量{areaCount}张{size}图，超过指定数量{maxCount}", (int)areaCount);
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下引用动画总占用内存大小不超过指定大小")]
    public static bool GameObjectClipsMemory([ExceptModel] GameObject gameObject, int memoryLimit, string[] includePaths, string[] excludePaths)
    {
        var clips = GetDependencyAnimationClips(gameObject, includePaths, excludePaths, true);
        long runtimeMemory = 0;
        foreach (var kv in clips)
        {
            var clipList = kv.Value;
            foreach (var clip in clipList)
            {
                runtimeMemory += GetAnimationClipMemory(clip);
            }
        }
        bool result = runtimeMemory <= memoryLimit * 1024 * 1024;
        if (!result)
        {
            int order = (int)(runtimeMemory / 1024 / 1024);
            AddLog($"引用动画总占用内存大小{FormatBytes(runtimeMemory)}MB超过指定大小{memoryLimit}MB", order);
            foreach (var kv in clips)
            {
                var referencer = kv.Key;
                var clipList = kv.Value;
                foreach (var clip in clipList)
                {
                    AddLog($"引用了动画{clip.name}占用内存{FormatBytes(GetAnimationClipMemory(clip))}MB", order, referencer, clip);
                }
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下引用AnimationClip总数量不超过指定数量")]
    public static bool GameObjectClipCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        var clips = GetDependencyAnimationClips(gameObject, includePaths, excludePaths, true);
        var count = clips.Sum(i => i.Value.Count);
        bool result = count <= maxCount;
        if (!result)
        {
            int order = count;
            AddLog($"引用动画总数量{count}超过指定大小{maxCount}");
            foreach (var kv in clips)
            {
                var referencer = kv.Key;
                var subCount = kv.Value.Count;
                AddLog($"引用了不同动画{subCount}个", order, referencer);
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下单个引用动画长度（s）不超过指定长度")]
    public static bool GameObjectSingleClipMaxTime([ExceptModel] GameObject gameObject, float maxTime, string[] includePaths, string[] excludePaths)
    {
        var clips = GetDependencyAnimationClips(gameObject, includePaths, excludePaths, true);
        bool ok = true;
        foreach (var kv in clips)
        {
            var referencer = kv.Key;
            var clipList = kv.Value;
            foreach (var clip in clipList)
            {
                if (clip.length > maxTime)
                {
                    AddLog($"引用了动画{clip.name}长度{clip.length.ToString("f2")}s超过指定长度{maxTime}s", (int)(clip.length * 100), referencer, clip);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下引用动画总长度（s）不超过指定长度")]
    public static bool GameObjectClipsTotalTime([ExceptModel] GameObject gameObject, float maxTime, string[] includePaths, string[] excludePaths)
    {
        var clips = GetDependencyAnimationClips(gameObject, includePaths, excludePaths, true);
        float totalTime = 0;
        foreach (var kv in clips)
        {
            var clipList = kv.Value;
            foreach (var clip in clipList)
            {
                totalTime += clip.length;
            }
        }
        bool result = totalTime <= maxTime;
        if (!result)
        {
            int order = (int)(totalTime * 100);
            AddLog($"引用动画总时间长度{totalTime.ToString("f2")}s超过指定长度{maxTime}s", order);
            foreach (var kv in clips)
            {
                var referencer = kv.Key;
                var clipList = kv.Value;
                foreach (var clip in clipList)
                {
                    AddLog($"引用了动画{clip.name}长度{clip.length.ToString("f2")}s", order, referencer, clip);
                }
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "统计GameObject下材质球数量不超过指定数量")]
    public static bool GameObjectMaterialCount([ExceptModel] GameObject gameObject, int maxCount, string[] includePaths, string[] excludePaths)
    {
        var materials = GetDependencyMaterials(gameObject, includePaths, excludePaths, true);
        var count = materials.Sum(i => i.Value.Count);
        bool result = count <= maxCount;
        if (!result)
        {
            int order = count;
            AddLog($"材质球数量{count}已超过指定数量{maxCount}", order);
            foreach (var kv in materials)
            {
                var referencer = kv.Key;
                var subCount = kv.Value.Count;
                AddLog($"引用了不同材质球{subCount}个", order, referencer);
            }
        }
        return result;
    }
    [AssetClearanceMethod("GameObject", "GameObject下材质Shader白名单")]
    public static bool GameObjectShaderWhiteList([ExceptModel] GameObject gameObject, [EnsureShader] string[] shaderNames, string[] includePaths, string[] excludePaths)
    {
        var materials = GetDependencyMaterials(gameObject, includePaths, excludePaths, true);
        bool ok = true;
        foreach (var kv in materials)
        {
            var referencer = kv.Key;
            var materialList = kv.Value;
            foreach (var material in materialList)
            {
                if (material.shader == null) continue;
                if (!Array.Exists(shaderNames, (name) => material.shader.name == name))
                {
                    AddLog($"该材质使用了白名单之外的Shader\"{material.shader.name}\"", 0, referencer, material);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下AlphaTest材质RenderQueue是否符合要求")]
    public static bool GameObjectMaterialAlphaTest([ExceptModel] GameObject gameObject, string alphaTestKeyword, int minRenderQueue, int maxRenderQueue, string[] includePaths, string[] excludePaths)
    {
        var materials = GetDependencyMaterials(gameObject, includePaths, excludePaths, true);
        bool ok = true;
        foreach (var kv in materials)
        {
            var referencer = kv.Key;
            var materialList = kv.Value;
            foreach (var material in materialList)
            {
                if (material.shader == null) continue;
                if (!material.IsKeywordEnabled(alphaTestKeyword)) continue;
                if (!(material.renderQueue >= minRenderQueue && material.renderQueue <= maxRenderQueue))
                {
                    AddLog($"该AlphaTest材质的RenderQueue={material.renderQueue}不符合要求范围[{minRenderQueue},{maxRenderQueue}]", 0, referencer, material);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "检查GameObject下Transparent材质RenderQueue是否符合要求")]
    public static bool GameObjectMaterialTransparent([ExceptModel] GameObject gameObject, int minRenderQueue, string[] includePaths, string[] excludePaths)
    {
        var materials = GetDependencyMaterials(gameObject, includePaths, excludePaths, true);
        bool ok = true;
        foreach (var kv in materials)
        {
            var referencer = kv.Key;
            var materialList = kv.Value;
            foreach (var material in materialList)
            {
                if (material.shader == null) continue;
                if (material.GetTag("RenderType", true, "") != "Transparent") continue;
                if (!(material.renderQueue >= minRenderQueue))
                {
                    AddLog($"该Transparent材质的RenderQueue={material.renderQueue}小于要求值{minRenderQueue}", 0, referencer, material);
                    ok = false;
                }
            }
        }
        return ok;
    }
    [AssetClearanceMethod("GameObject", "找出GameObject下材质中无用的贴图属性")]
    public static bool GameObjectUnusedTextureProperty([ExceptModel] GameObject gameObject, string[] includePaths, string[] excludePaths)
    {
        var materials = GetDependencyMaterials(gameObject, includePaths, excludePaths, true);
        bool haveUseless = false;
        foreach (var kv in materials)
        {
            var referencer = kv.Key;
            var materialList = kv.Value;
            foreach (var material in materialList)
            {
                if (material.shader == null) continue;
                var unusedpropertiesLog = new StringBuilder();
                var propertyCount = ShaderUtil.GetPropertyCount(material.shader);
                var propertyNames = new List<string>();
                for (int i = 0; i < propertyCount; ++i)
                {
                    var name = ShaderUtil.GetPropertyName(material.shader, i);
                    propertyNames.Add(name);
                }
                var serializedObject = new SerializedObject(material);
                var savedProperties = serializedObject.FindProperty("m_SavedProperties");
                var texEnvs = savedProperties.FindPropertyRelative("m_TexEnvs");
                for (int i = 0; i < texEnvs.arraySize; ++i)
                {
                    var property = texEnvs.GetArrayElementAtIndex(i);
                    if (!propertyNames.Contains(property.displayName))
                    {
                        AddLog($"该材质存在无用的贴图属性{property.displayName}", 0, referencer, material);
                        haveUseless = true;
                    }
                }
            }
        }
        return haveUseless;
    }
}
