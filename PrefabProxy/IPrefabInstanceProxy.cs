using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NexgenDragon
{
    public interface IInstanceComponentProxy
    {
        void OnEnableProxy(IInstanceProxy proxy);
        void OnDisableProxy(IInstanceProxy proxy);
        void UpdateProxy(IInstanceProxy proxy);
    }
    public interface IInstanceProxy
    {
        void SetPosition(Vector3 pos);
        Vector3 GetPosition();
        void SetRotation(Quaternion rot);
        Quaternion GetRotation();
        void SetScale(Vector3 scale);
        Vector3 GetScale();
        void SetTRS(Vector3 pos, Quaternion rot, Vector3 s);
        void SetLocalPosition(Vector3 pos);
        Vector3 GetLocalPosition();
        void SetLocalRotation(Quaternion rot);
        Quaternion GetLocalRotation();
        void SetLocalScale(Vector3 s);
        Vector3 GetLocalScale();
        void SetLocalTRS(Vector3 pos, Quaternion rot, Vector3 s);
        void SetActive(bool active);
        void SetActive(Transform trans, bool active);
        bool IsActiveSelf();
    }
    public interface IPrefabProxy
    {
        IInstanceProxy Instantiate();
        void Destroy(IInstanceProxy instance);
        void SetOnPrefabDestroy(object manager, Action<object, GameObject> onPrefabDestroy);
        void UpdateInstancesComponent();
        void UpdateInstancesTransform(Transform root);
        void SetInstanceParent(IInstanceProxy instance, Transform parent);
        Transform GetInstanceParent(IInstanceProxy instance);
        void SetInstancesActive(Transform root, bool active);
    }
}
