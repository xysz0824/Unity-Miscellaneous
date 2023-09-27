using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LowEndRenderingAdaptorConfig", menuName = "Low-End Rendering Adaptor Config", order = 100)]
public class LowEndRenderingAdaptorConfig : ScriptableObject
{
    public Shader replaceShader;
    public string alphatestPropertyName;
    public string alphatestKeyword;
    public Shader[] alphatestShaders;
    public string[] ignoreShaderNames;
    public Material[] ignoreMaterials;
    public Texture2D[] ignorePackTextures;
    public string propertyMappingRule;
    public string keywordMappingRule;
    public string[] excludeProperties;
}