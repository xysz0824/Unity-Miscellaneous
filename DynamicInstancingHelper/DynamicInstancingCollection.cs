using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DynamicInstancingCollection : MonoBehaviour
{
    List<DynamicInstancingChild> children = new List<DynamicInstancingChild>();
    public List<DynamicInstancingChild> Children => children;
    public void AddChild(DynamicInstancingChild child)
    {
        children.Add(child);
    }
    public void RemoveChild(DynamicInstancingChild child)
    {
        children.Remove(child);
    }
}