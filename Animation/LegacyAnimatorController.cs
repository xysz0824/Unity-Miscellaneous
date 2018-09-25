using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LegacyAnimatorConditionMode
{
    If = 1,
    IfNot = 2,
    Greater = 3,
    Less = 4,
    Equals = 6,
    NotEqual = 7
}

[System.Serializable]
public class LegacyAnimatorCondition
{
    public LegacyAnimatorConditionMode Mode;
    public string Parameter;
    public float Threshold;
}

[System.Serializable]
public class LegacyAnimatorStateTransition
{
    public bool HasExitTime;
    public float ExitTime;
    public float Duration;
    public float Offset;
    public int DestinationStateIndex;
    public LegacyAnimatorCondition[] Conditions;
}

[System.Serializable]
public class LegacyAnimatorState
{
    public string Name;
    public AnimationClip Motion;
    public float Speed;
    public LegacyAnimatorStateTransition[] Trainsitions;
    public LegacyStateMachineBehaviour[] Behaviours;
}

[System.Serializable]
public class LegacyAnimatorStateMachine
{
    public LegacyAnimatorState[] States;
    public int DefaultStateIndex;
}

[System.Serializable]
public class LegacyAnimatorControllerParameter
{
    public string Name;
    public AnimatorControllerParameterType Type;
    public bool DefaultBool;
    public float DefaultFloat;
    public int DefaultInt;
}

[System.Serializable]
public class LegacyAnimatorController : ScriptableObject 
{
	public LegacyAnimatorControllerParameter[] Parameters;
    public LegacyAnimatorStateMachine StateMachine;
}
