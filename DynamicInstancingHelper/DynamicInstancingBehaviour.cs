using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicInstancingBehaviour : StateMachineBehaviour
{
    DynamicInstancingCollection collection;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        collection = animator.GetComponent<DynamicInstancingCollection>();
        if (collection == null)
        {
            collection = animator.gameObject.AddComponent<DynamicInstancingCollection>();
            var children = animator.GetComponentsInChildren<DynamicInstancingChild>(true);
            foreach (var child in children)
            {
                DynamicInstancingRenderer.Instance.UpdateTransform(child);
            }
        }
    }
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (DynamicInstancingRenderer.Instance == null) return;
        if (animator.GetNextAnimatorStateInfo(layerIndex).fullPathHash == 0 && stateInfo.normalizedTime >= 1.0f) return;
        foreach (var child in collection.Children)
        {
            DynamicInstancingRenderer.Instance.UpdateTransform(child);
        }
    }
}