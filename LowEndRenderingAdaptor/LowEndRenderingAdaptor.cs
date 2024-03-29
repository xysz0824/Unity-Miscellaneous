using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LowEndRenderingAdaptor : MonoBehaviour
{
    private bool joined;
    [NonSerialized]
    public int index = -1;
    void OnEnable()
    {
        if (LowEndRenderingManager.Instance != null)
        {
            joined = LowEndRenderingManager.Instance.Join(this);
        }
    }
    void OnDisable()
    {
        if (LowEndRenderingManager.Instance != null && joined)
        {
            LowEndRenderingManager.Instance.Quit(this);
            joined = false;
        }
    }
    public virtual bool IsValid()
    {
        return true;
    }
    public virtual void Active()
    {
    }
    public virtual void Disactive()
    {
    }
}