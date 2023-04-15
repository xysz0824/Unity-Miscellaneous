using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class AssetClearanceMethod : Attribute
{
    private string group;
    public string Group => group;
    private string tip;
    public string Tip => tip;
    public AssetClearanceMethod(string group = "", string tip = "")
    {
        this.group = group;
        this.tip = tip;
    }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public abstract class ObjectValidation : Attribute
{
    public abstract bool DoValidate(UnityEngine.Object obj);
}

public class ExceptModel : ObjectValidation
{
    public override bool DoValidate(UnityEngine.Object obj)
    {
        return !PrefabUtility.IsPartOfModelPrefab(obj);
    }
}

public class EnsureModel : ObjectValidation
{
    public override bool DoValidate(UnityEngine.Object obj)
    {
        return PrefabUtility.IsPartOfModelPrefab(obj);
    }
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public abstract class ParameterValidation : Attribute
{
    public abstract string[] GetItems();
    public bool DoValidate(object value) => Array.Exists(GetItems(), (item) => item == value as string);
}

public class EnsureComponent : ParameterValidation
{
    static string[] itemsCache;
    public override string[] GetItems()
    {
        if (itemsCache != null) return itemsCache;
        var items = new List<string>();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var componentType = typeof(Component);
        var behaviourType = typeof(Behaviour);
        var monoBehaviourType = typeof(MonoBehaviour);
        var transformType = typeof(Transform);
        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic) continue;
            var types = assembly.GetExportedTypes();
            foreach (var type in types)
            {
                if (!type.IsAbstract && (type.IsSubclassOf(componentType) || type == componentType) && type != behaviourType && type != monoBehaviourType && type != transformType)
                {
                    items.Add(type.Name);
                }
            }
        }
        items.Sort();
        itemsCache = items.ToArray();
        return itemsCache;
    }
}

public class EnsureShader : ParameterValidation
{
    static string[] itemsCache;
    public override string[] GetItems()
    {
        if (itemsCache != null) return itemsCache;
        var items = new List<string>();
        var shaders = ShaderUtil.GetAllShaderInfo();
        foreach (var shader in shaders)
        {
            items.Add(shader.name);
        }
        items.Sort();
        itemsCache = items.ToArray();
        return itemsCache;
    }
}

public class EnsurePlatform : ParameterValidation
{
    static string[] itemsCache;
    public override string[] GetItems()
    {
        if (itemsCache != null) return itemsCache;
        var items = new List<string>();
        var moduleManager = System.Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");
        var isPlatformSupportLoaded = moduleManager.GetMethod("IsPlatformSupportLoaded", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var getTargetStringFromBuildTarget = moduleManager.GetMethod("GetTargetStringFromBuildTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        items.Add("Default");
        items.Add("Standalone");
        if ((bool)isPlatformSupportLoaded.Invoke(null, new object[] { (string)getTargetStringFromBuildTarget.Invoke(null, new object[] { BuildTarget.Android }) }))
        {
            items.Add("Android");
        }
        if ((bool)isPlatformSupportLoaded.Invoke(null, new object[] { (string)getTargetStringFromBuildTarget.Invoke(null, new object[] { BuildTarget.iOS }) }))
        {
            items.Add("iPhone");
        }
        itemsCache = items.ToArray();
        return itemsCache;
    }
}