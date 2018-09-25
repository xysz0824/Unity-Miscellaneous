using UnityEngine;
using UnityEditor;
using System.Collections;

public class GUIDSearch : EditorWindow
{
    string guid;

    [MenuItem("Tools/GUID Search")]
    static void Open()
    {
        var controller = EditorWindow.GetWindow<GUIDSearch>("GUID Search", true);
        controller.maxSize = new Vector2(500, 50);
        var position = controller.position;
        position.center = new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height).center;
        controller.position = position;
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        guid = EditorGUILayout.TextField(guid);
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(guid))
        {
            EditorGUILayout.TextArea(AssetDatabase.GUIDToAssetPath(guid));
        }
        EditorGUILayout.EndVertical();
    }
}