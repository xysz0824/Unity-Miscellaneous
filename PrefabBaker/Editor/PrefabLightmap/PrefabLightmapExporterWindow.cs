using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PrefabLightmapExporterWindow : EditorWindow
{
    static readonly string[] pages = new string[1] { "Export" };
    static readonly string[] maxAtlasSizes = { "64", "128", "256", "512", "1024", "2048" };
    Vector2 scrollPos;
    static bool objectsFoldout = true;
    static bool lightmapsFoldout = true;
    static bool exportSettingFoldout = true;
    static bool packLightmaps = false;
    static int maxAtlasSize = 1024;
    static bool clearExportedLightmaps = true;
    static string exportInfoTag = "";

    void OnSceneChanged(Scene a, Scene b)
    {
        Close();
    }

    [MenuItem("Tools/PrefabLightmapExporter/Open", false, 100)]
    public static void Open()
    {
        var window = EditorWindow.GetWindow<PrefabLightmapExporterWindow>(true, "Prefab Lightmap Exporter");
        window.position = new Rect(200, 200, 400, 800);
        window.minSize = new Vector2(400, 800);
        window.maxSize = new Vector2(800, 1200);
        window.Show();
        EditorSceneManager.activeSceneChangedInEditMode -= window.OnSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += window.OnSceneChanged;
    }

    void OnDestroy()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
    }

    void DrawExportPage()
    {
        var objects = new List<GameObject>();
        foreach (var obj in Selection.gameObjects)
        {
            var prefabType = PrefabUtility.GetPrefabAssetType(obj);
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(obj)) continue;
            if (prefabType != PrefabAssetType.Regular && prefabType != PrefabAssetType.Variant) continue;
            objects.Add(obj);
        }
        objectsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(objectsFoldout, "Objects");
        if (objectsFoldout)
        {
            if (objects.Count == 0)
            {
                EditorGUILayout.HelpBox("Select prefabs that need to be export in this scene", MessageType.Warning);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                foreach (var obj in objects)
                {
                    EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox(objects.Count + " prefabs selected", MessageType.Info);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        lightmapsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(lightmapsFoldout, "Lightmaps");
        if (lightmapsFoldout)
        {
            int count = 0;
            for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
            {
                var data = LightmapSettings.lightmaps[i];
                if (PrefabLightmapInfo.IsPrefabLightmap(data.lightmapColor)) continue;
                count++;
            }
            if (LightmapSettings.lightmaps != null && count > 0)
            {
                for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
                {
                    var data = LightmapSettings.lightmaps[i];
                    if (PrefabLightmapInfo.IsPrefabLightmap(data.lightmapColor)) continue;
                    EditorGUILayout.ObjectField("      " + data.lightmapColor.name, data.lightmapColor, typeof(Texture2D), false);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No lightmap in this scene", MessageType.Info);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUI.BeginDisabledGroup(Lightmapping.isRunning);
        exportSettingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(exportSettingFoldout, "Export Setting");
        if (exportSettingFoldout)
        {
            packLightmaps = EditorGUILayout.Toggle("Pack Lightmaps", packLightmaps);
            if (packLightmaps)
            {
                int selectedIndex = (int)Mathf.Log(maxAtlasSize, 2) - 6;
                selectedIndex = EditorGUILayout.Popup("      Max Size", selectedIndex, maxAtlasSizes);
                maxAtlasSize = (int)Mathf.Pow(2, selectedIndex + 6);
            }
            clearExportedLightmaps = EditorGUILayout.Toggle("Clear Exported Lightmaps", clearExportedLightmaps);
            exportInfoTag = EditorGUILayout.TextField("Info Tag", exportInfoTag).Trim();
            EditorGUI.BeginDisabledGroup(objects.Count == 0 || LightmapSettings.lightmaps == null || LightmapSettings.lightmaps.Length == 0);
            if (GUILayout.Button("Export to Prefabs"))
            {
                PrefabLightmapExporter.ExportToPrefabs(objects, exportInfoTag, packLightmaps, maxAtlasSize);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(objects.Count == 0);
            if (GUILayout.Button("Clear Export"))
            {
                PrefabLightmapExporter.ClearExport(objects, exportInfoTag, clearExportedLightmaps);
            }
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUI.EndDisabledGroup();
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
        EditorGUILayout.BeginVertical();
        DrawExportPage();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }
}