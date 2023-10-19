using System.Collections.Generic;
using System;
using UnityEngine;

public class LowEndRenderingManager : MonoBehaviour
{
    static LowEndRenderingManager instance;
    public static LowEndRenderingManager Instance => instance;
    LowEndRenderingAdaptor[] adaptors = new LowEndRenderingAdaptor[5000];
    int count = 0;
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
                for (int i = 0; i < count; ++i)
                {
                    if (adaptors[i] != null) 
                        adaptors[i].Active();
                }
            }
            else
            {
                for (int i = 0; i < count; ++i)
                {
                    if (adaptors[i] != null)
                        adaptors[i].Disactive();
                }
            }
        }
    }
    public bool Join(LowEndRenderingAdaptor adaptor)
    {
        if (!adaptor.IsValid()) return false;
        adaptor.index = count;
        adaptors[count++] = adaptor;
        if (count >= adaptors.Length)
        {
            Array.Resize(ref adaptors, adaptors.Length * 2);
        }
        if (enabled)
        {
            if (Shader.globalMaximumLOD <= lodThreshold)
            {
                adaptor.Active();
            }
            else
            {
                adaptor.Disactive();
            }
        }
        return true;
    }
    public void Quit(LowEndRenderingAdaptor adaptor)
    {
        adaptors[adaptor.index] = adaptors[count - 1];
        adaptors[adaptor.index].index = adaptor.index;
        adaptor.index = -1;
        adaptors[count - 1] = null;
        count--;
    }
}
