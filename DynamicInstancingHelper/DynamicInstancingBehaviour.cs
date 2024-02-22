using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicInstancingBehaviour : StateMachineBehaviour
{
    List<DynamicInstancingChild> children = new List<DynamicInstancingChild>();
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        OnStateUpdate(animator, stateInfo, layerIndex);
    }
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (DynamicInstancingRenderer.Instance == null) return;
        if (animator.GetNextAnimatorStateInfo(layerIndex).fullPathHash == 0 && stateInfo.normalizedTime >= 1.0f) return;
        animator.GetComponentsInChildren(false, children);
        foreach (var child in children)
        {
            DynamicInstancingRenderer.Instance.UpdateTransform(child);
        }
        var proxies = PrefabProxyManager.Instance.Proxies;
        foreach (var proxy in proxies)
        {
            var instancingProxy = proxy as DynamicInstancingProxy;
            if (instancingProxy != null)
            {
                instancingProxy.UpdateInstancesTransform(animator.transform);
            }
        }
    }
}