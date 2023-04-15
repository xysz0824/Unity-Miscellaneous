using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AssetClearanceMethods
{
    [AssetClearanceMethod("Mesh", "检查网格是否开启Read/Write Enabled")]
    public static bool MeshReadWriteEnabled(Mesh mesh)
    {
        if (mesh.isReadable) AddLog("开启了Read/Write Enabled");
        return mesh.isReadable;
    }
}
