using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class PrefabLightmapMuter : MonoBehaviour
{
    static bool muting;
    public static bool Muting 
    {
        get => muting;
        set => muting = value;
    }
    
    void OnEnable()
    {
        muting = true;
    }

    void OnDisable()
    {
        muting = false;
    }
}
