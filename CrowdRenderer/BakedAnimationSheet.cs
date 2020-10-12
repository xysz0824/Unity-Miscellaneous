using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Baked Animation Sheet", menuName = "Baked Animation Sheet")]
public class BakedAnimationSheet : ScriptableObject
{
    public struct Matrix
    {
        public Color m1;
        public Color m2;
        public Color m3;
    }
    [Serializable]
    public class BakedClip
    {
        public AnimationClip Clip;
        public int MatrixStartIndex;
        public int MatrixCount;
        [NonSerialized]
        public Matrix[] Matrices;
    }
    public BakedClip[] Clips;
    public int FrameRate;
    public int BoneCount;
    public int TextureWidth;
    public int TextureHeight;
}
