using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEditor;
using System.Reflection;
using UnityEditor.Animations;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("AnimatorController", "统计引用动画数量不超过指定数量")]
    public static bool AnimatorControllerClipCount(AnimatorController controller, int maxCount)
    {
        bool result = controller.animationClips.Length <= maxCount;
        if (!result)
        {
            AddLog($"引用动画数量{controller.animationClips.Length}超过指定数量{maxCount}", controller.animationClips.Length);
        }
        return result;
    }
    [AssetClearanceMethod("AnimatorController", "统计动画总长度（秒）不超过指定长度")]
    public static bool AnimatorControllerClipTotalLen(AnimatorController controller, int maxLength)
    {
        var clips = controller.animationClips;
        float totalLength = 0;
        for (int i = 0; i < clips.Length; ++i)
        {
            totalLength += clips[i].length;
        }
        bool result = totalLength <= maxLength;
        if (!result)
        {
            int order = (int)(totalLength * 100);
            AddLog($"动画总长度{totalLength.ToString("f2")}s超过指定长度{maxLength}s", order);
            foreach (var clip in clips)
            {
                AddLog($"引用动画长度{clip.length.ToString("f2")}s", order, null, clip);
            }
        }
        return result;
    }
    [AssetClearanceMethod("AnimatorController", "检查引用动画单个长度（秒）不超过指定长度")]
    public static bool AnimatorControllerClipSingleLen(AnimatorController controller, float maxLength)
    {
        bool ok = true;
        var clips = controller.animationClips;
        for (int i = 0; i < clips.Length; ++i)
        {
            if (clips[i].length > maxLength)
            {
                AddLog($"引用动画长度{clips[i].length.ToString("f2")}s超过指定长度{maxLength}s", (int)(clips[i].length * 100), null, clips[i]);
                ok = false;
            }
        }
        return ok;
    }
}
