using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NexgenDragon
{
    public class DynamicInstancingBehaviour : StateMachineBehaviour
    {
        DynamicInstancingChild[] children;
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            children = animator.GetComponentsInChildren<DynamicInstancingChild>(true);
        }
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (DynamicInstancingRenderer.Instance == null) return;
            if (animator.GetNextAnimatorStateInfo(layerIndex).fullPathHash == 0 && stateInfo.normalizedTime >= 1.0f) return;
            for (int i = 0; i < children.Length; ++i)
            {
                if (children[i] != null)
                    DynamicInstancingRenderer.Instance.UpdateTransform(children[i]);
            }
        }
    }
}
