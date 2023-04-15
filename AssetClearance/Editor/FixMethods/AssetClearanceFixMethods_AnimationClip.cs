using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public partial class AssetClearanceFixMethods
{
    [AssetClearanceMethod("AnimationClip", "优化动画片段")]
    public static bool OptimizeAnimationClip(AnimationClip clip, bool scale, bool precision)
    {
        bool optimized = false;
        var curveBindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var curveBinding in curveBindings)
        {
            string name = curveBinding.propertyName.ToLower();
            if (name.Contains("editor"))
            {
                AnimationUtility.SetEditorCurve(clip, curveBinding, null);
                optimized = true;
                continue;
            }
            var curve = AnimationUtility.GetEditorCurve(clip, curveBinding);
            if (curve == null) continue;
            if (scale && name.Contains("scale"))
            {
                var keys = curve.keys;
                var emptyScale = true;
                var firstScaleValue = keys.Length == 0 ? 1 : keys[0].value;
                for (int i = 0; i < keys.Length - 1; ++i)
                {
                    if (keys[i].value != keys[i + 1].value)
                    {
                        emptyScale = false;
                        break;
                    }
                }
                if (emptyScale && firstScaleValue == 1)
                {
                    AnimationUtility.SetEditorCurve(clip, curveBinding, null);
                    optimized = true;
                    continue;
                }
            }
            if (precision && curve != null)
            {
                var keys = curve.keys;
                for (int i = 0;i < keys.Length; ++i)
                {
                    if (AssetClearanceMethods.GetPrecision(keys[i].value) > 3 || AssetClearanceMethods.GetPrecision(keys[i].inTangent) > 3 || AssetClearanceMethods.GetPrecision(keys[i].outTangent) > 3 ||
                        AssetClearanceMethods.GetPrecision(keys[i].inWeight) > 3 || AssetClearanceMethods.GetPrecision(keys[i].outWeight) > 3)
                    {
                        keys[i].value = float.Parse(keys[i].value.ToString("f3"));
                        keys[i].inTangent = float.Parse(keys[i].inTangent.ToString("f3"));
                        keys[i].outTangent = float.Parse(keys[i].outTangent.ToString("f3"));
                        keys[i].inWeight = float.Parse(keys[i].inWeight.ToString("f3"));
                        keys[i].outWeight = float.Parse(keys[i].outWeight.ToString("f3"));
                        optimized = true;
                    }
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clip, curveBinding, curve);
            }
        }
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return optimized;
    }
}
