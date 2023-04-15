using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using TargetObject = AssetClearance.TargetObject;

[CreateAssetMenu(fileName = "New Asset Clearance Targets", menuName = "Asset Clearance Targets", order = 100)]
public class AssetClearanceTargets : ScriptableObject
{
    public List<AssetClearanceRules> overrideRules = new List<AssetClearanceRules>();
    public bool autoSearchRules = true;
    public bool searchRulesInTargetRange = true;
    public List<TargetObject> targetObjects = new List<TargetObject>();
}