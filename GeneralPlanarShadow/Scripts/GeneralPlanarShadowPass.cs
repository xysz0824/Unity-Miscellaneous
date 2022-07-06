using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GeneralPlanarShadowPass : ScriptableRenderPass
{
    static readonly int s_PlanarShadowPlaneY = Shader.PropertyToID("_PlanarShadowPlaneY");
    static readonly int s_PlanarShadowLightDir = Shader.PropertyToID("_PlanarShadowLightDir");
    static readonly int s_PlanarShadowLightPos = Shader.PropertyToID("_PlanarShadowLightPos");
    static readonly int s_PlanarShadowLightColor = Shader.PropertyToID("_PlanarShadowLightColor");
    static readonly int s_PlanarShadowLightParam = Shader.PropertyToID("_PlanarShadowLightParam");
    static readonly int s_StencilRef = Shader.PropertyToID("_StencilRef");

    ProfilingSampler m_ProfilingSampler;
    GeneralPlanarShadow.Settings setings;
    List<ShaderTagId> shaderTagIdList = new List<ShaderTagId>();
    Material shadowMat;
    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;


    public GeneralPlanarShadowPass(GeneralPlanarShadow.Settings setings)
    {
        base.profilingSampler = new ProfilingSampler(nameof(GeneralPlanarShadowPass));
        m_ProfilingSampler = new ProfilingSampler("GeneralPlanarShadow");

        this.setings = setings;
        if (setings.LightModeTags != null && setings.LightModeTags.Length > 0)
        {
            foreach (var passName in setings.LightModeTags)
                shaderTagIdList.Add(new ShaderTagId(passName));
        }
        else
        {
            shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
            shaderTagIdList.Add(new ShaderTagId("LightweightForward"));
        }
        if (shadowMat != null) Object.Destroy(shadowMat);
        shadowMat = new Material(Shader.Find("Mobile Friend/GeneralPlanarShadow"));
        filteringSettings = new FilteringSettings(RenderQueueRange.all, setings.LayerMask);
        renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (GeneralPlanarShadowLight.ActiveLights.Count == 0) return;
        DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, SortingCriteria.CommonTransparent);
        drawingSettings.overrideMaterial = shadowMat;
        drawingSettings.overrideMaterialPassIndex = 0;
        shadowMat.SetInt(s_StencilRef, setings.ShadowStencilRef);
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, m_ProfilingSampler))
        {
            bool hasLight = false;
            cmd.EnableShaderKeyword("DIRECITONAL_PLANAR_SHADOW");
            cmd.SetGlobalFloat(s_PlanarShadowPlaneY, setings.ProjectivePlaneY);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            foreach (var light in GeneralPlanarShadowLight.ActiveLights)
            {
                if (light.type != GeneralPlanarShadowLight.LightType.Directional)
                {
                    continue;
                }
                hasLight = true;
                var dir = light.transform.forward;
                cmd.SetGlobalVector(s_PlanarShadowLightDir, new Vector4(dir.x, dir.y, dir.z));
                var color = light.color * light.intensity * light.shadowStrength;
                cmd.SetGlobalColor(s_PlanarShadowLightColor, color);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
            }
            cmd.DisableShaderKeyword("DIRECITONAL_PLANAR_SHADOW");
            cmd.EnableShaderKeyword("POINT_PLANAR_SHADOW");
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            foreach (var light in GeneralPlanarShadowLight.ActiveLights)
            {
                if (light.type != GeneralPlanarShadowLight.LightType.Point)
                {
                    continue;
                }
                hasLight = true;
                cmd.Blit(null, BuiltinRenderTextureType.CurrentActive, shadowMat, 1);
                var pos = light.transform.position;
                cmd.SetGlobalVector(s_PlanarShadowLightPos, new Vector4(pos.x, pos.y, pos.z));
                var color = light.color * Mathf.Sqrt(light.intensity) * light.shadowStrength;
                cmd.SetGlobalColor(s_PlanarShadowLightColor, color);
                var param = new Vector4(light.range, light.innerSpotAngle, light.outerSpotAngle, 0);
                cmd.SetGlobalVector(s_PlanarShadowLightParam, param);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
            }
            cmd.DisableShaderKeyword("POINT_PLANAR_SHADOW");
            cmd.EnableShaderKeyword("SPOT_PLANAR_SHADOW");
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            foreach (var light in GeneralPlanarShadowLight.ActiveLights)
            {
                if (light.type != GeneralPlanarShadowLight.LightType.Spot)
                {
                    continue;
                }
                hasLight = true;
                cmd.Blit(null, BuiltinRenderTextureType.CurrentActive, shadowMat, 1);
                var dir = light.transform.forward;
                cmd.SetGlobalVector(s_PlanarShadowLightDir, new Vector4(dir.x, dir.y, dir.z));
                var pos = light.transform.position;
                cmd.SetGlobalVector(s_PlanarShadowLightPos, new Vector4(pos.x, pos.y, pos.z));
                var color = light.color * Mathf.Sqrt(light.intensity) * light.shadowStrength;
                cmd.SetGlobalColor(s_PlanarShadowLightColor, color);
                var param = new Vector4(light.range, Mathf.Cos(light.innerSpotAngle * Mathf.Deg2Rad * 0.5f), Mathf.Cos(light.outerSpotAngle * Mathf.Deg2Rad * 0.5f), 0);
                cmd.SetGlobalVector(s_PlanarShadowLightParam, param);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
            }
            cmd.DisableShaderKeyword("SPOT_PLANAR_SHADOW");
            if (hasLight)
            {
                cmd.Blit(null, BuiltinRenderTextureType.CurrentActive, shadowMat, 1);
                hasLight = false;
            }
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}
