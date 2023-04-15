using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;

[CustomEditor(typeof(StaticBatchHelper))]
public class StaticBatchHelperEditor : Editor
{
    bool combinedMeshGUIDsFoldout = true;
    bool combinedRendererInfosFoldout = false;
    public override void OnInspectorGUI()
    {
        var helper = target as StaticBatchHelper;
        var gameObject = helper.gameObject;
        var selectObjectsProperty = serializedObject.FindProperty("selectObjects");
        var combinedMeshGUIDsProperty = serializedObject.FindProperty("combinedMeshGUIDs");
        var combinedRendererInfosProperty = serializedObject.FindProperty("combinedRendererInfos");
        var appliedPrefabGUIDProperty = serializedObject.FindProperty("appliedPrefabGUID");
        var batchRateProperty = serializedObject.FindProperty("batchRate");
        EditorGUILayout.PropertyField(selectObjectsProperty);
        if (combinedMeshGUIDsProperty.arraySize == 0)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorGUILayout.HelpBox("You are in prefab mode, where batching is not valid", MessageType.Error);
                return;
            }
            if (!helper.gameObject.activeSelf)
            {
                EditorGUILayout.HelpBox("You should active this gameObject", MessageType.Error);
                return;
            }
            if (GUILayout.Button("Batch"))
            {
                var path = EditorUtility.OpenFolderPanel("Save To Folder", "", "");
                if (string.IsNullOrEmpty(path)) return;
                var inactives = helper.DisactiveUnselectedObjects();
                path = "Assets" + path.Substring(Application.dataPath.Length);
                var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
                var originalMeshes = new Dictionary<MeshFilter, Mesh>();
                foreach (var meshFilter in meshFilters)
                {
                    originalMeshes[meshFilter] = meshFilter.sharedMesh;
                }
                StaticBatchingUtility.Combine(gameObject);
                var combinedMeshes = new List<Mesh>();
                var rendererInfos = new List<StaticBatchHelper.CombinedRendererInfo>();
                var combinedFilters = new List<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.sharedMesh == null) continue;
                    if (meshFilter.sharedMesh.name.Contains("Combined Mesh ("))
                        combinedFilters.Add(meshFilter);
                }
                foreach (var meshFilter in combinedFilters)
                {
                    var combinedMesh = meshFilter.sharedMesh;
                    if (!combinedMeshes.Contains(combinedMesh))
                    {
                        string guid = "00000000000000000000000000000000";
                        var assetPath = path + "/CombinedMesh_" + guid + ".mesh";
                        AssetDatabase.CreateAsset(combinedMesh, assetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(assetPath);
                        guid = AssetDatabase.AssetPathToGUID(assetPath);
                        AssetDatabase.RenameAsset(assetPath, "CombinedMesh_" + guid);
                        combinedMeshes.Add(combinedMesh);
                        combinedMeshGUIDsProperty.arraySize++;
                        combinedMeshGUIDsProperty.GetArrayElementAtIndex(combinedMeshGUIDsProperty.arraySize - 1).stringValue = guid;
                    }
                    var rendererInfo = new StaticBatchHelper.CombinedRendererInfo();
                    rendererInfo.meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                    rendererInfo.combinedMeshID = combinedMeshes.IndexOf(combinedMesh);
                    var batchInfo = new StaticBatchHelper.StaticBatchInfo();
                    var serializedRenderer = new SerializedObject(rendererInfo.meshRenderer);
                    var firstSubMeshProperty = serializedRenderer.FindProperty("m_StaticBatchInfo.firstSubMesh");
                    var subMeshCountProperty = serializedRenderer.FindProperty("m_StaticBatchInfo.subMeshCount");
                    batchInfo.firstSubMesh = firstSubMeshProperty.intValue;
                    batchInfo.subMeshCount = subMeshCountProperty.intValue;
                    firstSubMeshProperty.intValue = 0;
                    subMeshCountProperty.intValue = 0;
                    meshFilter.sharedMesh = originalMeshes[meshFilter];
                    rendererInfo.staticBatchInfo = batchInfo;
                    rendererInfos.Add(rendererInfo);
                }
                foreach (var rendererInfo in rendererInfos)
                {
                    combinedRendererInfosProperty.arraySize++;
                    var infoProperty = combinedRendererInfosProperty.GetArrayElementAtIndex(combinedRendererInfosProperty.arraySize - 1);
                    infoProperty.FindPropertyRelative("meshRenderer").objectReferenceValue = rendererInfo.meshRenderer;
                    infoProperty.FindPropertyRelative("combinedMeshID").intValue = rendererInfo.combinedMeshID;
                    infoProperty.FindPropertyRelative("staticBatchInfo.firstSubMesh").intValue = rendererInfo.staticBatchInfo.firstSubMesh;
                    infoProperty.FindPropertyRelative("staticBatchInfo.subMeshCount").intValue = rendererInfo.staticBatchInfo.subMeshCount;
                }
                helper.ReactiveUnselectedObjects(inactives);
                batchRateProperty.floatValue = (float)(combinedFilters.Count - combinedMeshes.Count + 1) / combinedFilters.Count;
                serializedObject.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
        else
        {
            combinedMeshGUIDsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(combinedMeshGUIDsFoldout, "Combined Mesh IDs");
            if (combinedMeshGUIDsFoldout)
            {
                int index = 0;
                EditorGUILayout.BeginVertical(new GUIStyle("RL Background"));
                foreach (var id in helper.combinedMeshGUIDs)
                {
                    EditorGUILayout.LabelField("[" + index + "]", id);
                    index++;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            combinedRendererInfosFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(combinedRendererInfosFoldout, "Combined Renderer Infos");
            if (combinedRendererInfosFoldout)
            {
                EditorGUI.BeginDisabledGroup(true);
                foreach (var info in helper.combinedRendererInfos)
                {
                    EditorGUILayout.BeginVertical(new GUIStyle("RL Background"));
                    EditorGUILayout.ObjectField("Mesh Renderer", info.meshRenderer, typeof(MeshRenderer), false);
                    EditorGUILayout.IntField("Combined Mesh ID", info.combinedMeshID);
                    EditorGUILayout.IntField("First Sub Mesh", info.staticBatchInfo.firstSubMesh);
                    EditorGUILayout.IntField("Sub Mesh Count", info.staticBatchInfo.subMeshCount);
                    EditorGUILayout.EndVertical();
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUI.enabled = false;
            EditorGUILayout.PropertyField(batchRateProperty);
            GUI.enabled = true;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorGUILayout.HelpBox("You are in prefab mode, where applying is not valid", MessageType.Error);
            }
            else if (string.IsNullOrEmpty(helper.appliedPrefabGUID) && GUILayout.Button("Apply as Prefab"))
            {
                var path = EditorUtility.SaveFilePanelInProject("Save", gameObject.name + "_Batch", "prefab", "");
                if (string.IsNullOrEmpty(path)) return;
                Selection.activeGameObject = gameObject;
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                var duplicate = Selection.activeGameObject;
                var components = duplicate.GetComponentsInChildren<StaticBatchComponent>(true);
                for (int i = 0; i < components.Length; ++i)
                {
                    DestroyImmediate(components[i]);
                }
                var duplicatedHelper = duplicate.GetComponent<StaticBatchHelper>();
                duplicatedHelper.ApplyInEditor();
                DestroyImmediate(duplicatedHelper);
                PrefabUtility.SaveAsPrefabAsset(duplicate, path);
                DestroyImmediate(duplicate);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);
                helper.appliedPrefabGUID = AssetDatabase.AssetPathToGUID(path);
                serializedObject.ApplyModifiedProperties();
                var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)) as GameObject;
                var siblingIndex = helper.gameObject.transform.GetSiblingIndex();
                instance.transform.SetParent(helper.gameObject.transform.parent, false);
                instance.transform.SetSiblingIndex(siblingIndex);
                helper.gameObject.SetActive(false);
                Selection.activeGameObject = instance;
            }
            else if (!string.IsNullOrEmpty(helper.appliedPrefabGUID))
            {
                EditorGUILayout.BeginVertical(new GUIStyle("RL Background"));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("Applied Prefab GUID", helper.appliedPrefabGUID);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
                if (GUILayout.Button("Instantiate"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(helper.appliedPrefabGUID);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)) as GameObject;
                        instance.transform.SetParent(helper.gameObject.transform.parent, false);
                        var siblingIndex = helper.gameObject.transform.GetSiblingIndex();
                        instance.transform.SetSiblingIndex(siblingIndex);
                        helper.gameObject.SetActive(false);
                    }
                }
            }
            if (GUILayout.Button("Apply as Component"))
            {
                var combinedMesh = new List<Mesh>();
                for (int i = 0; i < combinedMeshGUIDsProperty.arraySize; ++i)
                {
                    var guid = combinedMeshGUIDsProperty.GetArrayElementAtIndex(i).stringValue;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    combinedMesh.Add(AssetDatabase.LoadAssetAtPath<Mesh>(path));
                }
                for (int i = 0; i < combinedRendererInfosProperty.arraySize; ++i)
                {
                    var info = combinedRendererInfosProperty.GetArrayElementAtIndex(i);
                    var meshRenderer = info.FindPropertyRelative("meshRenderer").objectReferenceValue as MeshRenderer;
                    var combinedMeshID = info.FindPropertyRelative("combinedMeshID").intValue;
                    var firstSubMesh = info.FindPropertyRelative("staticBatchInfo.firstSubMesh").intValue;
                    var subMeshCount = info.FindPropertyRelative("staticBatchInfo.subMeshCount").intValue;
                    var component = meshRenderer.gameObject.GetComponent<StaticBatchComponent>();
                    if (component == null)
                    {
                        component = meshRenderer.gameObject.AddComponent<StaticBatchComponent>();
                    }
                    component.root = helper.transform;
                    component.combinedMesh = combinedMesh[combinedMeshID];
                    component.firstSubMesh = firstSubMesh;
                    component.subMeshCount = subMeshCount;
                }
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            if (GUILayout.Button("Clear"))
            {
                foreach (var guid in helper.combinedMeshGUIDs)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(path);
                }
                if (!string.IsNullOrEmpty(helper.appliedPrefabGUID))
                {
                    var path = AssetDatabase.GUIDToAssetPath(helper.appliedPrefabGUID);
                    AssetDatabase.DeleteAsset(path);
                }
                var parent = helper.gameObject.transform.parent;
                if (parent != null)
                {
                    for (int i = 0; i < parent.childCount; ++i)
                    {
                        var child = parent.GetChild(i);
                        if (PrefabUtility.IsPrefabAssetMissing(child.gameObject))
                        {
                            DestroyImmediate(child.gameObject);
                            i--;
                        }
                    }
                }
                else
                {
                    var scene = SceneManager.GetActiveScene();
                    var rootObjects = scene.GetRootGameObjects();
                    for (int i = 0; i < rootObjects.Length; ++i)
                    {
                        if (PrefabUtility.IsPrefabAssetMissing(rootObjects[i]))
                        {
                            DestroyImmediate(rootObjects[i]);
                        }
                    }
                }
                var components = helper.GetComponentsInChildren<StaticBatchComponent>(true);
                for (int i = 0; i < components.Length; ++i)
                {
                    DestroyImmediate(components[i]);
                }
                appliedPrefabGUIDProperty.stringValue = "";
                combinedMeshGUIDsProperty.ClearArray();
                combinedRendererInfosProperty.ClearArray();
                serializedObject.ApplyModifiedProperties();
                helper.gameObject.SetActive(true);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }
        serializedObject.UpdateIfRequiredOrScript();
    }
}