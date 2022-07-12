using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class PrefabLightmapInfo : MonoBehaviour
{
    static Dictionary<string, Action> enableTriggers = new Dictionary<string, Action>();
    static Dictionary<string, Action> disableTriggers = new Dictionary<string, Action>();
    static LightmapData[] lightmaps = new LightmapData[0];
    static LightmapData[] mergedLightmaps = new LightmapData[0];
    static Dictionary<int, int> refs = new Dictionary<int, int>();

    public string infoTag = "";
    public Texture2D lightmap;
    public Vector4 scaleOffset;

    bool mutingOnEnable;

    public static void Switch(string infoTag)
    {
        if (enableTriggers.ContainsKey(infoTag))
        {
            foreach (var disableTrigger in disableTriggers)
            {
                if (disableTrigger.Key == infoTag) continue;
                disableTrigger.Value();
            }
            enableTriggers[infoTag]();
        }
    }

    void Enable()
    {
        enabled = true;
    }

    void Disable()
    {
        enabled = false;
    }

    void Awake()
    {
        if (!enableTriggers.ContainsKey(infoTag)) enableTriggers.Add(infoTag, Enable);
        else enableTriggers[infoTag] += Enable;
        if (!disableTriggers.ContainsKey(infoTag)) disableTriggers.Add(infoTag, Disable);
        else disableTriggers[infoTag] += Disable;
    }

    void OnDestroy()
    {
        if (enableTriggers.ContainsKey(infoTag)) enableTriggers[infoTag] -= Enable;
        if (disableTriggers.ContainsKey(infoTag)) disableTriggers[infoTag] -= Disable;
    }

    void MergeToLightmapSettings(LightmapData deleted)
    {
        int length = LightmapSettings.lightmaps == null ? 0 : LightmapSettings.lightmaps.Length;
        List<int> existLightmapIndices = new List<int>();
        if (deleted != null)
        {
            var lightmapIndex = System.Array.FindIndex(LightmapSettings.lightmaps, (data) =>
                data != null && data.lightmapColor == deleted.lightmapColor);
            if (lightmapIndex >= 0)
            {
                length--;
            }
        }
        for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
        {
            var lightmapIndex = System.Array.FindIndex(lightmaps, (data) =>
                LightmapSettings.lightmaps[i] != null && data.lightmapColor == LightmapSettings.lightmaps[i].lightmapColor);
            if (lightmapIndex >= 0)
            {
                existLightmapIndices.Add(lightmapIndex);
            }
        }
        mergedLightmaps = new LightmapData[length + lightmaps.Length - existLightmapIndices.Count];
        int index = 0;
        for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
        {
            if (deleted != null && LightmapSettings.lightmaps[i].lightmapColor == deleted.lightmapColor) continue;
            mergedLightmaps[index] = LightmapSettings.lightmaps[i];
            index++;
        }
        index = 0;
        for (int i = 0; i < lightmaps.Length; ++i)
        {
            if (existLightmapIndices.Contains(i)) continue;
            mergedLightmaps[length + index] = lightmaps[i];
            index++;
        }
        LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
        LightmapSettings.lightmaps = mergedLightmaps;
    }

    static void OnActiveSceneChanged(Scene a, Scene B)
    {
        LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
    }

    void OnEnable()
    {
        mutingOnEnable = PrefabLightmapMuter.Muting;
        if (mutingOnEnable) return;
#if UNITY_EDITOR
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
#endif
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        var lightmapIndex = System.Array.FindIndex(lightmaps, (data) => data.lightmapColor == lightmap);
        if (lightmapIndex < 0)
        {
            System.Array.Resize(ref lightmaps, lightmaps.Length + 1);
            lightmapIndex = lightmaps.Length - 1;
            lightmaps[lightmapIndex] = new LightmapData();
            lightmaps[lightmapIndex].lightmapColor = lightmap;
            refs[lightmapIndex] = 0;
            MergeToLightmapSettings(null);
        }
        refs[lightmapIndex]++;
        var thisRenderer = GetComponent<Renderer>();
        if (thisRenderer != null)
        {
            lightmapIndex = System.Array.FindIndex(LightmapSettings.lightmaps, (data) => data.lightmapColor == lightmap);
            thisRenderer.lightmapIndex = lightmapIndex;
            thisRenderer.lightmapScaleOffset = scaleOffset;
        }
    }

    void OnDisable()
    {
        if (mutingOnEnable) return;
        var lightmapIndex = System.Array.FindIndex(lightmaps, (data) => data.lightmapColor == lightmap);
        if (lightmapIndex >= 0)
        {
            refs[lightmapIndex]--;
            if (refs[lightmapIndex] == 0)
            {
                refs.Remove(lightmapIndex);
                var temp = lightmaps[lightmapIndex];
                lightmaps[lightmapIndex] = lightmaps[lightmaps.Length - 1];
                lightmaps[lightmaps.Length - 1] = temp;
                System.Array.Resize(ref lightmaps, lightmaps.Length - 1);
                MergeToLightmapSettings(temp);
            }
        }
    }
}
