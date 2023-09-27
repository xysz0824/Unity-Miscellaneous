using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LowEndPipelineSwitcher : MonoBehaviour
{
    static FieldInfo m_RendererIndex = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
    [Serializable]
    public class Pair
    {
        public int originRendererDataIndex = -1;
        public int lowEndRendererDataIndex = -1;
    }
    public List<Pair> pairs = new List<Pair>();
    Camera[] allCameras = new Camera[10];
    Camera lastCamera;
    int lastLod;
    bool lowEnd;

    void Switch(Camera camera)
    {
        if (camera == null || camera.gameObject == null) return;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData != null)
        {
            var currentIndex = (int)m_RendererIndex.GetValue(cameraData);
            foreach (var pair in pairs)
            {
                if (lowEnd && currentIndex == pair.originRendererDataIndex)
                {
                    if (UniversalRenderPipeline.asset.ValidateRendererData(pair.lowEndRendererDataIndex))
                    {
                        cameraData.SetRenderer(pair.lowEndRendererDataIndex);
                    }
                    else
                    {
                        Debug.LogError("The low-end renderer data is not in list.");
                    }
                }
                else if (!lowEnd && currentIndex == pair.lowEndRendererDataIndex)
                {
                    if (UniversalRenderPipeline.asset.ValidateRendererData(pair.originRendererDataIndex))
                    {
                        cameraData.SetRenderer(pair.originRendererDataIndex);
                    }
                    else
                    {
                        Debug.LogError("The origin renderer data is not in list.");
                    }
                }
            }
        }
    }
    void Update()
    {
        if (Shader.globalMaximumLOD != lastLod)
        {
            lastLod = Shader.globalMaximumLOD;
            lowEnd = lastLod <= 100;
            Switch(lastCamera);
            return;
        }
        int count = Camera.GetAllCameras(allCameras);
        if (count <= 1) return;
        for (int i = 1; i < count; ++i)
        {
            if (allCameras[i].orthographic) continue;
            if (lastCamera != allCameras[i])
            {
                lastCamera = allCameras[i];
                Switch(lastCamera);
            }
            break;
        }
    }
}