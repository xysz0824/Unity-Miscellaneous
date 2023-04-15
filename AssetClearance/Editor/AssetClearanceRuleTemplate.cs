using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Rule = AssetClearanceRules.Rule;

[CreateAssetMenu(fileName = "New Asset Clearance Rule Template", menuName = "Asset Clearance Rule Template", order = 100)]
public class AssetClearanceRuleTemplate : ScriptableObject
{
    public Rule rule;
    public Rule unappliedSave;
}