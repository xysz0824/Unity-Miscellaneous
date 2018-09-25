using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;
using System.Text;

public class ReferenceSearcher : Editor
{
    [PreferenceItem("Reference Searcher")]
    static void PrefenrecesGUI()
    {
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var searchPath = EditorPrefs.GetString("ReferenceSearchPath", "");
        searchPath = EditorGUILayout.TextField("Search Path", searchPath);
        EditorGUI.EndChangeCheck();
        EditorPrefs.SetString("ReferenceSearchPath", searchPath);
    }

    [MenuItem("Assets/Set As References Search Path")]
    static void SearchReferencesInThisPath()
    {
        Object[] selectedObject = Selection.GetFiltered(typeof(Object), SelectionMode.TopLevel);
        string path = AssetDatabase.GetAssetPath(selectedObject[0]);
        EditorPrefs.SetString("ReferenceSearchPath", path);
        Debug.Log("Successfully set references search path.");
    }

    [MenuItem("Assets/Find References")]
    static void FindReferences()
    {
        Object[] selectedObject = Selection.GetFiltered(typeof(Object), SelectionMode.TopLevel);
        string searchPath = EditorPrefs.GetString("ReferenceSearchPath", "");
        if (string.IsNullOrEmpty(searchPath))
        {
            Debug.LogError("No reference search path set.");
            return;
        }
        searchPath = Application.dataPath.Replace("Assets", "") + searchPath;
        DirectoryInfo folder = new DirectoryInfo(searchPath);
        FileInfo[] files = folder.GetFiles("*.*", SearchOption.AllDirectories);
        for (int k = 0; k < selectedObject.Length; ++k)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selectedObject[k]));
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < files.Length; ++i)
            {
                if (files[i].Extension != ".prefab" && files[i].Extension != ".asset" && files[i].Extension != ".mat")
                    continue;

                using (var reader = new StreamReader(files[i].FullName))
                {
                    string line = reader.ReadToEnd();
                    if (line.Contains(guid))
                        result.AppendLine(files[i].FullName);
                }
            }
            if (result.Length != 0)
            {
                Debug.Log("\"" + selectedObject[k].name + "\" Found References : \n" + result);
            }
            else
            {
                Debug.Log("\"" + selectedObject[k].name + "\" No reference found.");
            }
        }
    }
}