using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class PrefabBakerLightmapMuter : MonoBehaviour
{
    static bool muting;
    public static bool Muting => muting;
    
    void OnEnable()
    {
        muting = true;
    }

    void OnDisable()
    {
        muting = false;
    }
}
