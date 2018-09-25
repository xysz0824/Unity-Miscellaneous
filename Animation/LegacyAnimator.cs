using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LegacyAnimator : MonoBehaviour
{
    static int updateOrderGenerator;
    Transform rootBone;
    Vector3 lastRootPos;
    Vector3 rootMotion;
    Vector3 transitionRootPos;
    int lastNormalizedTime;
    bool rootMotionApplied;
    new Animation animation;
    LegacyAnimatorState lastState;
    LegacyAnimatorState currentState;
    LegacyAnimatorState nextState;
    float transitionOffset;
    float transitionSpeed;
    float nextStateNormalizedTime;
    LegacyAnimatorController controllerInstance;
    Renderer[] renderers;
    int updateOrder;
    bool lod;
    public LegacyAnimatorController Controller;
    public bool ApplyRootMotion = true;
    public Vector3 DefaultRootMotion;
    public int LowFPSDistance = 10;
    public int LODDistance = 20;
    void Start()
    {
        if (Controller == null)
        {
            Debug.LogError("LegacyAnimator in \"" + name + "\" doesn't have an LegacyAnimatiorController");
            return;
        }
        updateOrder = updateOrderGenerator++;

        controllerInstance = ScriptableObject.Instantiate<LegacyAnimatorController>(Controller);

        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }
        var behaviours = animator.GetBehaviours<LegacyStateMachineBehaviour>();
        foreach (var behaviour in behaviours)
        {
            var name = behaviour.name.Split('(')[0];
            var index = 0;
            var canParse = int.TryParse(name, out index);
            if (!canParse)
            {
                Debug.LogError("The LegacyStateMachineBehaviour \"" + behaviour.name +"\" in \"" + this.name + "\"'s LegacyAnimatorController is not valid, " + 
                "please check if the LegacyAnimator is sync to the latest");
                continue;
            }
            else if (controllerInstance.StateMachine.States.Length <= index)
            {
                Debug.LogError("The LegacyStateMachineBehaviour \"" + behaviour.name +"\" in \"" + this.name + "\"'s LegacyAnimatorController is not valid, " + 
                "please check if the LegacyAnimator is sync to the latest");
                continue;
            }
            for (int i = 0; i < controllerInstance.StateMachine.States[index].Behaviours.Length; ++i)
            {
                if (controllerInstance.StateMachine.States[index].Behaviours[i] == null)
                {
                    controllerInstance.StateMachine.States[index].Behaviours[i] = behaviour;
                    break;
                }

            }
        }

        animation = GetComponent<Animation>();
        if (animation != null)
        {
            Debug.LogWarning("LegacyAnimator in \"" + name + "\" already has an Animation component, may cause somne problems.");
        }
        if (animation == null)
        {
            animation = gameObject.AddComponent<Animation>();
        }
        animation.hideFlags = HideFlags.HideAndDontSave;
        animation.playAutomatically = false;
        foreach (var state in controllerInstance.StateMachine.States)
        {
            if (state.Motion == null)
            {
                Debug.LogError("LegacyAnimatorState \"" + state.Name + "\"in \""+ name + "\" has null AnimationClip");
                continue;
            }
#if UNITY_EDITOR
            state.Motion.legacy = true;
#else
            if (!state.Motion.legacy)
            {
                Debug.LogError("AnimationClip \"" + state.Motion.name + "\" in \"" + name + "\" is not legacy, please check if it is an unique file");
            }
#endif
            animation.AddClip(state.Motion, state.Name);
        }

        var skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer != null)
        {
            rootBone = FindRootBone(skinnedMeshRenderer.rootBone);
            if (rootBone != null)
            {
                lastRootPos = rootBone.localPosition;
            }
        }
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        if (controllerInstance == null)
            return;

        foreach (var state in controllerInstance.StateMachine.States)
        {
            if (state.Motion != null)
            {
                state.Motion.legacy = false;
            }
        }
#endif
    }

    void Update()
    {
        var deltaTime = Time.deltaTime;

        if (animation == null)
            return;

        if (currentState == null)
        {
            SwitchState(controllerInstance.StateMachine.DefaultStateIndex);
            lastState = currentState;
        }

        var distanceToCamera = Vector3.Distance(Camera.main.transform.position, transform.position);
        if (distanceToCamera > LowFPSDistance)
        {
            ++updateOrder;
            if (updateOrder % 2 == 0)
            {
                updateOrder = 0;
                return;
            }
            deltaTime *= 2;
        }
        var visible = false;
        for (int i = 0; i < renderers.Length; ++i)
        {
            visible |= renderers[i].isVisible;
        }
        if (distanceToCamera > LODDistance || !visible)
        {
            if (!lod)
            {
                lod = true;
            }
        }
        else
        {
            if (lod)
            {
                lod = false;
            }
        }

        var currentAnimation = animation[currentState.Name];
        MakeTransition(deltaTime);
        currentAnimation.enabled = true;
        currentAnimation.time += deltaTime;
        if (nextState != null)
        {
            var nextAnimation = animation[nextState.Name];
            nextStateNormalizedTime += deltaTime;
            nextAnimation.time = nextStateNormalizedTime + transitionOffset;
            nextAnimation.enabled = true;

            var speed = transitionSpeed * deltaTime;
            currentAnimation.weight -= speed;
            nextAnimation.weight += speed;
            if (nextAnimation.weight >= 1.0f)
            {
                OnStateExit(currentState);
                nextStateNormalizedTime = 0;
                currentAnimation.enabled = false;
                currentState = nextState;
                currentAnimation = animation[currentState.Name];
                nextState = null;
            }
        }
        animation.Sample();
        OnStateUpdate(currentState, deltaTime);
        currentAnimation.enabled = false;
        if (nextState != null)
        {
            //OnStateUpdate(nextState, deltaTime);
            var nextAnimation = animation[nextState.Name];
            nextAnimation.enabled = false;
        }
        if (ApplyRootMotion)
        {
            ApplyBuiltinRootMotion();
            CalculateRootMotion(deltaTime);
        }
    }

    void SwitchState(int stateIndex)
    {
        currentState = controllerInstance.StateMachine.States[stateIndex];
        var currentAnimation = animation[currentState.Name];
        currentAnimation.weight = 1;
        currentAnimation.normalizedSpeed = currentState.Speed;
        currentAnimation.time = 0;
        OnStateEnter(currentState);
    }

    Transform FindRootBone(Transform bone)
    {
        if (bone == null)
            return null;
        
        if (bone.parent == transform)
            return bone;

        return FindRootBone(bone.parent);
    }

    void MakeTransition(float deltaTime)
    {
        var currentAnimation = animation[currentState.Name];
        foreach (var transition in currentState.Trainsitions)
        {
            if (transition.HasExitTime && transition.ExitTime > currentAnimation.normalizedTime)
                continue;

            if (CheckCondition(transition.Conditions))
            {
                if (nextState != controllerInstance.StateMachine.States[transition.DestinationStateIndex])
                {
                    nextState = controllerInstance.StateMachine.States[transition.DestinationStateIndex];
                    var nextAnimation = animation[nextState.Name];
                    nextAnimation.weight = 0;
                    nextAnimation.normalizedSpeed = nextState.Speed;
                    nextAnimation.time = 0;
                    OnStateEnter(nextState);
                    transitionOffset = transition.Offset;
                    if (Mathf.Approximately(transition.Duration, 0f))
                    {
                        transitionSpeed = 1.0f / deltaTime;
                    }
                    else
                    {
                        transitionSpeed = 1.0f / transition.Duration;
                    }
                }
                break;
            }
        }
    }

    bool CheckCondition(LegacyAnimatorCondition[] conditions)
    {
        bool result = true;
        int paramNotFoundCondition = 0;
        foreach (var condition in conditions)
        {
            LegacyAnimatorControllerParameter target = null;
            foreach (var parameter in controllerInstance.Parameters)
            {
                if (parameter.Name == condition.Parameter)
                {
                    target = parameter;
                    break;
                }
            }
            if (target == null)
            {
                ++paramNotFoundCondition;
                continue;
            }

            if (target.Type == AnimatorControllerParameterType.Bool ||
                target.Type == AnimatorControllerParameterType.Trigger)
            {
                switch (condition.Mode)
                {
                    case LegacyAnimatorConditionMode.If:
                        result &= target.DefaultBool;
                        if (target.Type == AnimatorControllerParameterType.Trigger && target.DefaultBool)
                        {
                            target.DefaultBool = false;
                        }
                        break;
                    case LegacyAnimatorConditionMode.IfNot:
                        result &= !target.DefaultBool;
                        break;
                    default:
                        result &= false;
                        break;
                }
            }
            else if (target.Type == AnimatorControllerParameterType.Float ||
                target.Type == AnimatorControllerParameterType.Int)
            {
                var value = target.DefaultFloat;
                if (target.Type == AnimatorControllerParameterType.Int)
                    value = target.DefaultInt;

                switch (condition.Mode)
                {
                    case LegacyAnimatorConditionMode.Greater:
                        result &= value > condition.Threshold;
                        break;
                    case LegacyAnimatorConditionMode.Less:
                        result &= value < condition.Threshold;
                        break;
                    case LegacyAnimatorConditionMode.Equals:
                        result &= value == condition.Threshold;
                        break;
                    case LegacyAnimatorConditionMode.NotEqual:
                        result &= value != condition.Threshold;
                        break;
                    default:
                        result &= false;
                        break;
                }
            }
        }
        if (conditions.Length > 0 && paramNotFoundCondition == conditions.Length)
            return false;

        return result;
    }

    void OnStateEnter(LegacyAnimatorState state)
    {
        if (state == null)
            return;

        foreach (var behaviour in state.Behaviours)
        {
            if (behaviour != null)
                behaviour.OnLegacyStateEnter(this);
        }
    }

    void OnStateUpdate(LegacyAnimatorState state, float deltaTime)
    {
        if (state == null)
            return;

        foreach (var behaviour in state.Behaviours)
        {
            if (behaviour != null)
                behaviour.OnLegacyStateUpdate(this, deltaTime);
        }
    }

    void OnStateExit(LegacyAnimatorState state)
    {
        if (state == null)
            return;

        foreach (var behaviour in state.Behaviours)
        {
            if (behaviour != null)
                behaviour.OnLegacyStateExit(this);
        }
    }

    void CalculateRootMotion(float deltaTime)
    {
        if (rootBone == null)
            return;

        var currentAnimation = animation[currentState.Name];
        if (lastState != currentState || (currentAnimation.wrapMode == WrapMode.Loop && (int)currentAnimation.normalizedTime > lastNormalizedTime))
        {
            rootMotion = Vector3.zero;
            lastRootPos = rootBone.localPosition;
            lastState = currentState;
        }
        else
        {
            Vector3 rootPos = rootBone.localPosition;
            rootMotion = rootPos - lastRootPos;
            rootMotion.y = 0;
            lastRootPos = rootPos;
        }
        if (currentAnimation.weight >= 1f)
        {
            transitionRootPos = rootBone.localPosition;
            transitionRootPos.y = 0;
        }
        else
        {
            rootMotion += transitionRootPos * transitionSpeed * deltaTime;
        }
        rootBone.localPosition = new Vector3(0, rootBone.localPosition.y, 0);
        lastNormalizedTime = (int)currentAnimation.normalizedTime;
        rootMotionApplied = false;
    }

    public void ApplyBuiltinRootMotion()
    {
        if (!rootMotionApplied)
        {
            rootMotionApplied = true;
            transform.position += transform.TransformDirection(rootMotion);
        }
    }

    public bool IsInTransition()
    {
        return nextState != null;
    }

    public void SetTrigger(string name)
    {
        if (controllerInstance == null)
            return;

        foreach (var parameter in controllerInstance.Parameters)
        {
            if (parameter.Name == name)
            {
                parameter.DefaultBool = true;
                break;
            }
        }
    }

    public void SetInteger(string name, int value)
    {
        if (controllerInstance == null)
            return;

        foreach (var parameter in controllerInstance.Parameters)
        {
            if (parameter.Name == name)
            {
                parameter.DefaultInt = value;
                break;
            }
        }
    }

    public void SetBool(string name, bool value)
    {
        if (controllerInstance == null)
            return;

        foreach (var parameter in controllerInstance.Parameters)
        {
            if (parameter.Name == name)
            {
                parameter.DefaultBool = value;
                break;
            }
        }
    }

    public void SetFloat(string name, float value)
    {
        if (controllerInstance == null)
            return;

        foreach (var parameter in controllerInstance.Parameters)
        {
            if (parameter.Name == name)
            {
                parameter.DefaultFloat = value;
                break;
            }
        }
    }
}
