using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LowEndRenderingManager : MonoBehaviour
{
    static LowEndRenderingManager instance;
    public static LowEndRenderingManager Instance => instance;
    HashSet<LowEndRenderingAdaptor> adaptors = new HashSet<LowEndRenderingAdaptor>();
    int lastLod;
    public int lodThreshold = 100;
    void Awake()
    {
        if (instance == null) instance = this;
    }
    void Update()
    {
        if (Shader.globalMaximumLOD != lastLod)
        {
            lastLod = Shader.globalMaximumLOD;
            if (lastLod <= lodThreshold)
            {
                foreach (var adaptor in adaptors)
                {
                    adaptor.Active();
                }
            }
            else
            {
                foreach (var adaptor in adaptors)
                {
                    adaptor.Disactive();
                }
            }
        }
    }
    public void Join(LowEndRenderingAdaptor adaptor)
    {
        if (!adaptor.IsValid()) return;
        adaptors.Add(adaptor);
        if (enabled && Shader.globalMaximumLOD <= lodThreshold) adaptor.Active();
    }
    public void Quit(LowEndRenderingAdaptor adaptor)
    {
        adaptors.Remove(adaptor);
        adaptor.Disactive();
    }
}
