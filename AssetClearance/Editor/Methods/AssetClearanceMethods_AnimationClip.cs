using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public partial class AssetClearanceMethods
{
    public static int GetPrecision(float number)
    {
        if (float.IsInfinity(number)) return 0;
        var text = number.ToString();
        int precision = 0;
        for (int i = text.Length - 1; i >= 0; --i)
        {
            if (text[i] == '.') break;
            if (i == 0) return 0;
            precision++;
        }
        return precision;
    }
    [AssetClearanceMethod("AnimationClip", "检查动画是否已优化")]
    public static bool AnimationClipOptimized(AnimationClip clip)
    {
        var log = new StringBuilder(); 
        var curveBindings = AnimationUtility.GetCurveBindings(clip);
        bool emptyScale = false;
        bool highPrecision = false;
        foreach (var curveBinding in curveBindings)
        {
            string name = curveBinding.propertyName.ToLower();
            if (name.Contains("editor")) continue;
            var curve = AnimationUtility.GetEditorCurve(clip, curveBinding);
            if (curve == null) continue;
            if (!emptyScale && name.Contains("scale"))
            {
                var keys = curve.keys;
                emptyScale = true;
                var firstScaleValue = keys.Length == 0 ? 1 : keys[0].value;
                for (int i = 0; i < keys.Length - 1; ++i)
                {
                    if (keys[i].value != keys[i + 1].value)
                    {
                        emptyScale = false;
                        break;
                    }
                }
                if (emptyScale && firstScaleValue == 1) log.Append("存在空Scale曲线 ");
            }
            if (!highPrecision)
            {
                var keys = curve.keys;
                foreach (var key in keys)
                {
                    if (GetPrecision(key.value) > 3 || GetPrecision(key.inTangent) > 3 || GetPrecision(key.outTangent) > 3 || 
                        GetPrecision(key.inWeight) > 3 || GetPrecision(key.outWeight) > 3)
                    {
                        highPrecision = true;
                        log.Append("存在精度过高数值 ");
                        break;
                    }
                }
            }
        }
        if (emptyScale || highPrecision) AddLog(log.ToString());
        return !emptyScale && !highPrecision;
    }
}
