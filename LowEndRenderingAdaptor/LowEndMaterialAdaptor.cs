using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class LowEndMaterialAdaptor : LowEndRenderingAdaptor
{
    public Material originMaterial;
    public Material lowEndMaterial;
    public override bool IsValid()
    {
        return originMaterial != null && lowEndMaterial != null;
    }
    public override void Active()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = lowEndMaterial;
        }
    }
    public override void Disactive()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = originMaterial;
        }
    }
}