using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(LightProbeGroup))]
public class LightProbePlanter : MonoBehaviour
{
    [Header("Distribution")]
    public float space = 0.5f;
    public float layer = 1;
    [Range(0, 1)]
    public float height = 1;
}
