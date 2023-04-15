using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicInstancingMipmap : MonoBehaviour
{
    public float mipmapBasePlaneY;
    public float minHeight;
    public int minMipmapLevel;
    public float maxHeight;
    public int maxMipmapLevel;
    void Update()
    {
        if (DynamicInstancingRenderer.Instance == null || DynamicInstancingRenderer.Instance.cullingCamera == null) return;
        var cameraTrans = DynamicInstancingRenderer.Instance.cullingCamera.transform;
        var distance = Mathf.Abs(cameraTrans.position.y - mipmapBasePlaneY);
        var t = Mathf.InverseLerp(minHeight, maxHeight, distance);
        DynamicInstancingRenderer.Instance.FixedMipmapLevel = (int)Mathf.Lerp(minMipmapLevel, maxMipmapLevel, t);
    }
}