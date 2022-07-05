using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;

public class GeneralPlanarShadow : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public float ProjectivePlaneY = 0.03f;
        public int ShadowStencilRef = 128;
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
        public LayerMask LayerMask;
        public string[] LightModeTags;
    }

    public Settings settings = new Settings();
    GeneralPlanarShadowPass pass;

    public override void Create()
    {
        pass = new GeneralPlanarShadowPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}
