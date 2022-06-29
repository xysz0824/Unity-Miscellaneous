using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[HideInInspector]
public class PrefabBakerConfig : MonoBehaviour
{
    public const string ROOT_NAME = "PrefabBakerSceneRoot";
    
    public float unitScale = 1;
    public int column = 10;
    public float space = 5;
    public bool generateContactQuad = true;
    public Material contactQuadMaterial;
    public float contactQuadScale = 1;
    public float contactQuadOffset = 0.01f;
    public bool packLightmaps = false;
    public int maxAtlasSize = 1024;
    public bool clearGeneratedQuad = false;
    public bool clearExportedLightmaps = true;
    public string exportInfoTag = "";
}
