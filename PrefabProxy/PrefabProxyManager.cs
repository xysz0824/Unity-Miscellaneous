using System;
using System.Collections.Generic;
using NexgenDragon;
using UnityEngine;


public class PrefabProxyManager : MonoSingleton<PrefabProxyManager>
{
    HashSet<IPrefabProxy> proxies = new HashSet<IPrefabProxy>();
    public HashSet<IPrefabProxy> Proxies => proxies;
    public bool stachNewInstance;
    HashSet<IInstanceProxy> stashed = new HashSet<IInstanceProxy>();
    public void AddProxy(IPrefabProxy proxy) => proxies.Add(proxy);
    public void RemoveProxy(IPrefabProxy proxy) => proxies.Remove(proxy);
    public void Update()
    {
        foreach (var proxy in proxies)
        {
            proxy.UpdateInstancesComponent();
        }
    }
    public void Stash(IInstanceProxy instance) => stashed.Add(instance);
    public bool IsStashed(IInstanceProxy instance) => stashed.Contains(instance);
    public void DropStashed(bool active)
    {
        if (active)
        {
            foreach (var instance in stashed)
            {
                instance.SetActive(true);
            }
        }
        stashed.Clear();
    }
}
