using System;
using System.Collections.Generic;
using UnityEngine;

public class InstanceProxy<T> : IInstanceProxy
{
    protected IPrefabProxy prefabProxy;
    public Transform parent;
    public Vector3 localPosition;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 localScale = Vector3.one;
    public bool activeSelf = true;
    public InstanceProxy(IPrefabProxy prefabProxy)
    {
        this.prefabProxy = prefabProxy;
    }
    protected static Vector3 GetWorldScale(Transform trans)
    {
        var worldScale = trans.localScale;
        var currentTrans = trans;
        while (currentTrans.parent != null)
        {
            worldScale = Vector3.Scale(worldScale, currentTrans.parent.localScale);
            currentTrans = currentTrans.parent;
        }
        return worldScale;
    }
    public static bool IsChildOrSelf(Transform trans, Transform test)
    {
        if (trans == null) return false;
        else if (trans == test) return true;
        var currentTrans = trans;
        while (currentTrans.parent != null)
        {
            if (currentTrans.parent == test) return true;
            currentTrans = currentTrans.parent;
        }
        return false;
    }
    public Vector3 GetLocalPosition() => localPosition;
    public Quaternion GetLocalRotation() => localRotation;
    public Vector3 GetLocalScale() => localScale;
    public Vector3 GetPosition() => parent != null ? parent.TransformPoint(localPosition) : localPosition;
    public Quaternion GetRotation() => parent != null ? localRotation * parent.rotation : localRotation;
    public Vector3 GetScale() => parent != null ? Vector3.Scale(localScale, GetWorldScale(parent)) : localScale;
    public bool IsActiveSelf() => activeSelf;
    public void SetActive(bool active)
    {
        if (activeSelf == active) return;
        if (PrefabProxyManager.Instance.IsStashed(this)) return;
        activeSelf = active;
        SetActualActive(active);
    }
    protected virtual void SetActualActive(bool active) {}
    public virtual void SetActive(Transform trans, bool active)
    {
        if (PrefabProxyManager.Instance.IsStashed(this)) return;
        SetActualActive(trans, active);
    }
    protected virtual void SetActualActive(Transform trans, bool active) {}
    public void SetLocalPosition(Vector3 pos)
    {
        localPosition = pos;
        var worldPos = parent != null ? parent.TransformPoint(pos) : pos;
        worldPos += (prefabProxy as PrefabProxyBase<T>).PositionInverse;
        SetActualPosition(worldPos);
    }
    protected virtual void SetActualPosition(Vector3 worldPos) {}
    public void SetLocalRotation(Quaternion rot)
    {
        localRotation = rot;
        var worldRot = parent != null ? parent.rotation * rot : rot;
        worldRot *= (prefabProxy as PrefabProxyBase<T>).RotationInverse;
        SetActualRotation(worldRot);
    }
    protected virtual void SetActualRotation(Quaternion worldRot) {}
    public void SetLocalScale(Vector3 s)
    {
        localScale = s;
        var worldS = parent != null ? Vector3.Scale(s, GetWorldScale(parent)) : s;
        worldS = Vector3.Scale(worldS, (prefabProxy as PrefabProxyBase<T>).ScaleInverse);
        SetActualScale(worldS);
    }
    protected virtual void SetActualScale(Vector3 worldS) {}
    public void SetLocalTRS(Vector3 pos, Quaternion rot, Vector3 s)
    {
        var proxyBase = prefabProxy as PrefabProxyBase<T>;
        localPosition = pos;
        var worldPos = parent != null ? parent.TransformPoint(pos) : pos;
        worldPos += proxyBase.PositionInverse;
        localRotation = rot;
        var worldRot = parent != null ? parent.rotation * rot : rot;
        worldRot *= proxyBase.RotationInverse;
        localScale = s;
        var worldS = parent != null ? Vector3.Scale(s, GetWorldScale(parent)) : s;
        worldS = Vector3.Scale(worldS, proxyBase.ScaleInverse);
        SetActualTRS(worldPos, worldRot, worldS);
    }
    protected virtual void SetActualTRS(Vector3 worldPos, Quaternion worldRot, Vector3 worldS) {}
    public void SetPosition(Vector3 pos)
    {
        localPosition = parent != null ? parent.InverseTransformPoint(pos) : pos;
        pos += (prefabProxy as PrefabProxyBase<T>).PositionInverse;
        SetActualPosition(pos);
    }
    public void SetRotation(Quaternion rot)
    {
        localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * rot : rot;
        rot *= (prefabProxy as PrefabProxyBase<T>).RotationInverse;
        SetActualRotation(rot);
    }
    public void SetScale(Vector3 s)
    {
        var worldScale = parent != null ? GetWorldScale(parent) : Vector3.one;
        localScale = parent != null ? new Vector3(s.x / worldScale.x, s.y / worldScale.y, s.z / worldScale.z) : s;
        s = Vector3.Scale(s, (prefabProxy as PrefabProxyBase<T>).ScaleInverse);
        SetActualScale(s);
    }
    public void SetTRS(Vector3 pos, Quaternion rot, Vector3 s)
    {
        var proxyBase = prefabProxy as PrefabProxyBase<T>;
        localPosition = parent != null ? parent.InverseTransformPoint(pos) : pos;
        pos += proxyBase.PositionInverse;
        localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * rot : rot;
        rot *= proxyBase.RotationInverse;
        var worldScale = parent != null ? GetWorldScale(parent) : Vector3.one;
        localScale = parent != null ? new Vector3(s.x / worldScale.x, s.y / worldScale.y, s.z / worldScale.z) : s;
        s = Vector3.Scale(s, proxyBase.ScaleInverse);
        SetActualTRS(pos, rot, s);
    }
    public void UpdateTransform()
    {
        var proxyBase = prefabProxy as PrefabProxyBase<T>;
        var worldPos = parent != null ? parent.TransformPoint(localPosition) : localPosition;
        worldPos += proxyBase.PositionInverse;
        var worldRot = parent != null ? parent.rotation * localRotation : localRotation;
        worldRot *= proxyBase.RotationInverse;
        var worldS = parent != null ? Vector3.Scale(localScale, GetWorldScale(parent)) : localScale;
        worldS = Vector3.Scale(worldS, proxyBase.ScaleInverse);
        SetActualTRS(worldPos, worldRot, worldS);
    }
}
[DisallowMultipleComponent]
public abstract class PrefabProxyBase<T> : MonoBehaviour, IPrefabProxy 
{
    protected T[] children;
    protected IInstanceComponentProxy[] instanceComponentProxies;
    Vector3 positionInverse;
    public Vector3 PositionInverse => positionInverse;
    Quaternion rotationInverse;
    public Quaternion RotationInverse => rotationInverse;
    Vector3 scaleInverse;
    public Vector3 ScaleInverse => scaleInverse;
    Dictionary<Transform, List<InstanceProxy<T>>> instancedDict = new Dictionary<Transform, List<InstanceProxy<T>>>();
    List<InstanceProxy<T>> rootInstanced = new List<InstanceProxy<T>>();
    object destroyManager;
    Action<object, GameObject> onPrefabDestroy;
    public IInstanceProxy Instantiate()
    {
        if (instancedDict.Count == 0 && rootInstanced.Count == 0)
        {
            children = GetComponentsInChildren<T>();
            instanceComponentProxies = GetComponentsInChildren<IInstanceComponentProxy>();
            positionInverse = -transform.localPosition;
            rotationInverse = Quaternion.Inverse(transform.localRotation);
            scaleInverse = new Vector3(1.0f /transform.localScale.x, 1.0f / transform.localScale.y, 1.0f / transform.localScale.z);
            PrefabProxyManager.Instance.AddProxy(this);
        }
        var instanceProxy = ActualInstantiate() as InstanceProxy<T>;
        if (PrefabProxyManager.Instance.stachNewInstance)
        {
            instanceProxy.activeSelf = false;
            PrefabProxyManager.Instance.Stash(instanceProxy);
        }
        if (instanceProxy.parent != null)
        {
            if (!instancedDict.ContainsKey(instanceProxy.parent))
            {
                instancedDict[instanceProxy.parent] = new List<InstanceProxy<T>>();
            }
            instancedDict[instanceProxy.parent].Add(instanceProxy);
        }
        else
        {
            rootInstanced.Add(instanceProxy);
        }
        foreach (var proxy in instanceComponentProxies)
        {
            proxy.OnEnableProxy(instanceProxy);
        }
        return instanceProxy;
    }
    protected virtual IInstanceProxy ActualInstantiate()  { throw new NotImplementedException(); }
    public void Destroy(IInstanceProxy instance)
    {
        var instanceProxy = instance as InstanceProxy<T>;
        if (instanceProxy != null)
        {
            ActualDestroy(instanceProxy);
            var parent = instanceProxy.parent;
            if (parent != null && instancedDict.ContainsKey(parent))
            {
                instancedDict[parent].Remove(instanceProxy);
                if (instancedDict[parent].Count == 0)
                {
                    instancedDict.Remove(parent);
                }
            }
            else
            {
                rootInstanced.Remove(instanceProxy);
            }
            foreach (var proxy in instanceComponentProxies)
            {
                proxy.OnDisableProxy(instanceProxy);
            }
        }
        if (instancedDict.Count == 0 && rootInstanced.Count == 0)
        {
            onPrefabDestroy?.Invoke(destroyManager, gameObject);
            onPrefabDestroy = null;
            destroyManager = null;
            PrefabProxyManager.Instance.RemoveProxy(this);
        }
    }
    protected virtual void ActualDestroy(IInstanceProxy instanceProxy) {}
    public void SetOnPrefabDestroy(object manager, Action<object, GameObject> onPrefabDestroy)
    {
        destroyManager = manager;
        this.onPrefabDestroy = onPrefabDestroy;
    }
    public void SetInstanceParent(IInstanceProxy instance, Transform trans)
    {
        var proxy = instance as InstanceProxy<T>;
        if (proxy == null) return;
        var old = proxy.parent;
        if (old != null && instancedDict.ContainsKey(old))
        {
            instancedDict[old].Remove(proxy);
        }
        else
        {
            rootInstanced.Remove(proxy);
        }
        if (trans != null)
        {
            if (!instancedDict.ContainsKey(trans))
            {
                instancedDict[trans] = new List<InstanceProxy<T>>();
            }
            instancedDict[trans].Add(proxy);
        }
        else
        {
            rootInstanced.Add(proxy);
        }
        proxy.parent = trans;
    }
    public Transform GetInstanceParent(IInstanceProxy instance)
    {
        var proxy = instance as InstanceProxy<T>;
        if (proxy == null) return null;
        return proxy.parent;
    }
    public void UpdateInstancesComponent()
    {
        var proxyLists = instancedDict.Values;
        foreach (var list in proxyLists)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                var proxy = list[i];
                if (!proxy.activeSelf) continue;
                foreach (var component in instanceComponentProxies)
                {
                    component.UpdateProxy(list[i]);
                }
            }
        }
        foreach (var proxy in rootInstanced)
        {
            if (!proxy.activeSelf) continue;
            foreach (var component in instanceComponentProxies)
            {
                component.UpdateProxy(proxy);
            }
        }
    }
    public void UpdateInstancesTransform(Transform root)
    {
        foreach (var kv in instancedDict)
        {
            if (InstanceProxy<T>.IsChildOrSelf(kv.Key, root))
            {
                var proxyList = kv.Value;
                for (int i = 0; i < proxyList.Count; ++i)
                {
                    proxyList[i].UpdateTransform();
                }
            }
        }
    }
    public void SetInstancesActive(Transform root, bool active)
    {
        foreach (var kv in instancedDict)
        {
            if (InstanceProxy<T>.IsChildOrSelf(kv.Key, root))
            {
                var proxyList = kv.Value;
                for (int i = 0; i < proxyList.Count; ++i)
                {
                    proxyList[i].SetActive(active);
                }
            }
        }
    }
}