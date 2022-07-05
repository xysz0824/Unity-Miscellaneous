using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class GeneralPlanarShadowLight : MonoBehaviour
{
    static HashSet<GeneralPlanarShadowLight> activeLights = new HashSet<GeneralPlanarShadowLight>();
    public static HashSet<GeneralPlanarShadowLight> ActiveLights => activeLights;

    public enum LightType
    {
        Spot = 0,
        Directional = 1,
        Point = 2
    }
    public LightType type = LightType.Directional;
    public float range = 10;
    public float innerSpotAngle = 20;
    public float outerSpotAngle = 30;
    public Color color = new Color(255 / 255f, 244 / 255f, 214 / 255f);
    public float intensity = 1;
    public float shadowStrength = 1;

    public static void Foreach(Action<GeneralPlanarShadowLight> func)
    {
        foreach (var activeLight in activeLights)
        {
            func(activeLight);
        }
    }

    public static void Foreach(LightType type, Action<GeneralPlanarShadowLight> func)
    {
        foreach (var activeLight in activeLights)
        {
            if (activeLight.type == type) func(activeLight);
        }
    }

    void OnEnable()
    {
        var light = GetComponent<Light>();
        if (light != null && (int)light.type <= 2)
        {
            type = (LightType)((int)light.type);
            range = light.range;
            innerSpotAngle = light.innerSpotAngle;
            outerSpotAngle = light.spotAngle;
            color = light.color;
            intensity = light.intensity;
        }
        activeLights.Add(this);
    }

#if UNITY_EDITOR
    void Update()
    {
        OnEnable();
    }
#endif

    void OnDisable()
    {
        activeLights.Remove(this);
    }

}
