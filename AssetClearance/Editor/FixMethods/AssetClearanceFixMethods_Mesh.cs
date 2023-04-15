using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class AssetClearanceFixMethods
{
    [AssetClearanceMethod("Mesh", "设置网格是否开启Read/Write Enabled")]
    public static bool SetMeshReadWrite(Mesh mesh, bool enabled)
    {
        var so = new SerializedObject(mesh);
        so.FindProperty("m_IsReadable").boolValue = enabled;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        return true;
    }
}
