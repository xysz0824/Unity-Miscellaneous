using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class LowEndMaterialAdaptor : LowEndRenderingAdaptor
{
    Renderer thisRenderer;
    public Material originMaterial;
    public Material lowEndMaterial;
    public override bool IsValid()
    {
        return originMaterial != null && lowEndMaterial != null;
    }
    public override void Active()
    {
        if (thisRenderer == null && this != null) thisRenderer = GetComponent<Renderer>();
        if (thisRenderer != null && thisRenderer.sharedMaterial != lowEndMaterial)
        {
            thisRenderer.sharedMaterial = lowEndMaterial;
        }
    }
    public override void Disactive()
    {
        if (thisRenderer != null && thisRenderer.sharedMaterial != originMaterial)
        {
            thisRenderer.sharedMaterial = originMaterial;
        }
    }
}