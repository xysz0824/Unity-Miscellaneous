using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Experimental.Rendering;

public class PrefabBakerEditorWindow : EditorWindow
{
    const string GUID = "75c72e40-304b-4dc3-92d5-850b4dba6fa9";
    const string DEFAULT_SCENE = "BakeScene.unity";
    static string rootAssetPath;
    static string lastOpenedPath;
    static Scene nowOpened;

    public override void SaveChanges()
    {
        Lightmapping.ForceStop();
        Close();
    }

    void OnSceneChanged(Scene a, Scene b)
    {
        if (a.path != nowOpened.path || this == null) return;
        Close();
    }

    static void NewSetting()
    {
        var root = new GameObject(PrefabBakerConfig.ROOT_NAME);
        root.hideFlags = (HideFlags)11;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
    }

    static void DefaultSetting()
    {
        RenderSettings.sun = null;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflection = null;
        RenderSettings.fog = false;
        LightingSettings lightingSettings = null;
        if (!Lightmapping.TryGetLightingSettings(out lightingSettings))
        {
            lightingSettings = new LightingSettings();
            lightingSettings.indirectSampleCount = 32;
            lightingSettings.environmentSampleCount = 32;
            lightingSettings.lightmapMaxSize = 256;
            lightingSettings.ao = true;
            lightingSettings.aoExponentIndirect = 2f;
            Lightmapping.lightingSettings = lightingSettings;
        }
        lightingSettings.realtimeGI = false;
        lightingSettings.bakedGI = true;
        lightingSettings.mixedBakeMode = MixedLightingMode.Subtractive;
        lightingSettings.directionalityMode = LightmapsMode.NonDirectional;
    }

    [MenuItem("Tools/PrefabBaker/Create Bake Scene", false, 101)]
    public static void CreateBakeScene()
    {
        var path = EditorUtility.SaveFilePanelInProject("Save Bake Scene To", "New Bake Scene", "unity", "");
        if (string.IsNullOrEmpty(path)) return;
        var name = Path.GetFileNameWithoutExtension(path);
        var folder = path.Substring(path.Length - Path.GetFileName(path).Length);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = name;
        new DirectoryInfo(folder).Create();
        NewSetting();
        DefaultSetting();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, path);
    }

    [MenuItem("Tools/PrefabBaker/Open", false, 100)]
    public static void Open()
    {
        rootAssetPath = GetRootAssetPath();
        if (rootAssetPath == null)
        {
            Debug.LogError("Can't find root asset path of Prefab Baker");
            return;
        }
        var openedScenes = new Scene[EditorSceneManager.sceneCount];
        for (int i = 0; i < openedScenes.Length; ++i)
        {
            openedScenes[i] = EditorSceneManager.GetSceneAt(i);
        }
        EditorSceneManager.SaveModifiedScenesIfUserWantsTo(openedScenes);
        Scene current = EditorSceneManager.GetActiveScene();
        lastOpenedPath = current.path;
        nowOpened = new Scene();
        var rootGameObjects = current.GetRootGameObjects();
        foreach (var rootGameObject in rootGameObjects)
        {
            if (rootGameObject.name == PrefabBakerConfig.ROOT_NAME && rootGameObject.hideFlags == (HideFlags)11)
            {
                nowOpened = current;
                break;
            }
        }
        string folder = rootAssetPath + "Scene";
        string folderPath = Application.dataPath + "/" + folder;
        string fullPath = folderPath + "/" + DEFAULT_SCENE;
        string relativePath = "Assets/" + folder + "/" + DEFAULT_SCENE;
        if (string.IsNullOrEmpty(nowOpened.path))
        {
            if (File.Exists(fullPath))
            {
                nowOpened = EditorSceneManager.OpenScene(relativePath, OpenSceneMode.Single);
            }
            else
            {
                nowOpened = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                nowOpened.name = "BakeScene";
                new DirectoryInfo(folderPath).Create();
                NewSetting();
            }
        }
        DefaultSetting();
        EditorSceneManager.MarkSceneDirty(nowOpened);
        if (string.IsNullOrEmpty(nowOpened.path))
        {
            EditorSceneManager.SaveScene(nowOpened, relativePath);
        }
        else
        {
            EditorSceneManager.SaveScene(nowOpened);
        }
        var window = EditorWindow.GetWindow<PrefabBakerEditorWindow>(true, "Prefab Baker");
        window.position = new Rect(200, 200, 400, 800);
        window.minSize = new Vector2(400, 800);
        window.maxSize = new Vector2(800, 1200);
        window.Show();
        EditorSceneManager.activeSceneChangedInEditMode -= window.OnSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += window.OnSceneChanged;
    }

    public static string GetRootAssetPath()
    {
        var guid = AssetDatabase.FindAssets(GUID);
        if (guid.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid[0]);
            path = path.Remove(path.Length - GUID.Length, GUID.Length);
            path = path.Substring(7);
            return path;
        }
        return null;
    }

    public enum MissingFlag
    {
        None,
        LightmapUV,
        Renderer,
        Mesh,
        Material
    }

    const string MISSING_FLAG_HEADER = " (Missing";
    static readonly string[] pages = new string[3] { "Objects", "Baking", "Export" };
    const string CONTACT_QUAD_NAME = "PrefabBakerContactQuad";
    const string CONTACT_QUAD_MATERIAL = "ContactQuad.mat";

    static int selectedPage;
    GameObject root;
    PrefabBakerConfig config;
    Vector2 scrollPos;
    ReorderableList objectList;
    SerializedObject serializedObject;
    [SerializeField]
    List<GameObject> objects;

    Bounds GetObjectBounds(GameObject obj)
    {
        Vector3 originPosition = obj.transform.position;
        obj.transform.position = new Vector3(0, 0, 0);
        var bounds = new Bounds();
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.gameObject.name != CONTACT_QUAD_NAME)
                bounds.Encapsulate(renderer.bounds);
        }
        obj.transform.position = originPosition;
        return bounds;
    }

    Vector3 GetObjectBaseCenter(Bounds objBounds)
    {
        return new Vector3(objBounds.center.x, objBounds.min.y - config.contactQuadOffset, objBounds.center.z);
    }

    Vector3 GetObjectBaseCenter(GameObject obj)
    {
        return GetObjectBaseCenter(GetObjectBounds(obj));
    }

    Vector3 GetObjectContactScale(Bounds objBounds)
    {
        float xScale = (objBounds.max - objBounds.min).x + Lightmapping.lightingSettings.aoMaxDistance;
        float zScale = (objBounds.max - objBounds.min).z + Lightmapping.lightingSettings.aoMaxDistance;
        return new Vector3(xScale, zScale, 1) * config.contactQuadScale;
    }

    Vector3 CalculatePositionByIndex(GameObject obj, int index)
    {
        return new Vector3((index / config.column), 0, (index % config.column)) * config.space * config.unitScale - GetObjectBaseCenter(obj);
    }

    void GenerateContactQuad(GameObject obj)
    {
        var objBounds = GetObjectBounds(obj);
        var quad = obj.transform.Find(CONTACT_QUAD_NAME);
        if (config.generateContactQuad && quad == null)
        {
            quad = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            quad.name = CONTACT_QUAD_NAME;
            GameObject.DestroyImmediate(quad.gameObject.GetComponent<Collider>());
            quad.SetParent(obj.transform, false);
        }
        else if (!config.generateContactQuad && quad != null && !PrefabUtility.IsPartOfAnyPrefab(quad))
        {
            GameObject.DestroyImmediate(quad.gameObject);
        }
        if (quad != null)
        {
            quad.gameObject.SetActive(config.generateContactQuad);
            quad.rotation = Quaternion.Euler(90, 0, 0);
            var objScale = obj.transform.localScale;
            quad.localScale = Vector3.Scale(GetObjectContactScale(objBounds), new Vector3(1.0f / objScale.x, 1.0f / objScale.y, 1.0f / objScale.z));
            quad.localPosition = GetObjectBaseCenter(objBounds);
            var renderer = quad.gameObject.GetComponent<Renderer>();
            renderer.sharedMaterial = config.contactQuadMaterial;
            renderer.gameObject.isStatic = true;
            var rendererSerialized = new SerializedObject(renderer);
            var lightmapParametersProperty = rendererSerialized.FindProperty("m_LightmapParameters");
            var lightmapParameters = new LightmapParameters();
            lightmapParameters.limitLightmapCount = true;
            lightmapParameters.maxLightmapCount = 1;
            lightmapParameters.bakedLightmapTag = obj.GetInstanceID();
            lightmapParameters.backFaceTolerance = 0;
            lightmapParametersProperty.objectReferenceValue = lightmapParameters;
            rendererSerialized.ApplyModifiedProperties();
        }
    }

    void ValidateAndRegularizeObject(GameObject obj)
    {
        var flag = MissingFlag.None;
        var missingHeaderIndex = obj.name.LastIndexOf(MISSING_FLAG_HEADER);
        if (missingHeaderIndex >= 0)
        {
            obj.name = obj.name.Remove(missingHeaderIndex);
        }
        GenerateContactQuad(obj);
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            flag = MissingFlag.Renderer;
        }
        foreach (var renderer in renderers)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                flag = MissingFlag.Mesh;
                break;
            }
            if (meshFilter.gameObject.name != CONTACT_QUAD_NAME && !meshFilter.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1))
            {
                flag = MissingFlag.LightmapUV;
                break;
            }
            var material = renderer.sharedMaterial;
            if (material == null)
            {
                flag = MissingFlag.Material;
                break;
            }
            renderer.gameObject.isStatic = true;
            var rendererSerialized = new SerializedObject(renderer);
            var lightmapParametersProperty = rendererSerialized.FindProperty("m_LightmapParameters");
            var lightmapParameters = new LightmapParameters();
            lightmapParameters.limitLightmapCount = true;
            lightmapParameters.maxLightmapCount = 1;
            lightmapParameters.bakedLightmapTag = obj.GetInstanceID();
            lightmapParameters.backFaceTolerance = 0;
            lightmapParametersProperty.objectReferenceValue = lightmapParameters;
            rendererSerialized.ApplyModifiedProperties();
        }
        if (flag != MissingFlag.None)
        {
            obj.name += MISSING_FLAG_HEADER + flag + ")";
        }
    }

    void Awake()
    {
        var rootGameObjects = nowOpened.GetRootGameObjects();
        foreach (var rootGameObject in rootGameObjects)
        {
            if (rootGameObject.name == PrefabBakerConfig.ROOT_NAME && rootGameObject.hideFlags == (HideFlags)11)
            {
                root = rootGameObject;
                var muter = root.GetComponent<PrefabBakerLightmapMuter>();
                if (muter == null)
                {
                    muter = root.AddComponent<PrefabBakerLightmapMuter>();
                }
                muter.enabled = true;
                config = root.GetComponent<PrefabBakerConfig>();
                if (config == null)
                {
                    config = root.AddComponent<PrefabBakerConfig>();
                    var materialPath = "Assets/" + rootAssetPath + "Material/" + CONTACT_QUAD_MATERIAL;
                    config.contactQuadMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                }
                objects = new List<GameObject>(root.transform.childCount);
                for (int i = 0; i < objects.Capacity; ++i)
                {
                    objects.Add(root.transform.GetChild(i).gameObject);
                    ValidateAndRegularizeObject(objects[i]);
                    objects[i].transform.position = CalculatePositionByIndex(objects[i], i);
                }
                break;
            }
        }
        serializedObject = new SerializedObject(this);
        objectList = new ReorderableList(serializedObject, serializedObject.FindProperty(nameof(objects)), false, true, true, true);
#if UNITY_2021_1_OR_NEWER
        objectList.multiSelect = true;
#endif
        objectList.drawHeaderCallback = DrawHeaderOfObjectList;
        objectList.drawElementCallback = DrawElementOfObjectList;
        objectList.onAddCallback = OnAddElementOfObjectList;
        objectList.onRemoveCallback = OnRemoveElementOfObjectList;
        objectList.onSelectCallback = OnSelectElementOfObjectList;
    }

    void DrawHeaderOfObjectList(Rect rect)
    {
        EditorGUI.LabelField(rect, "Put your prefabs into the list  ↴");
    }

    void DrawElementOfObjectList(Rect rect, int index, bool active, bool focused)
    {
        var element = objectList.serializedProperty.GetArrayElementAtIndex(index);
        var oldObject = element.objectReferenceValue;
        if (oldObject != null && oldObject.name.Contains(MISSING_FLAG_HEADER) && GUI.Button(new Rect(rect.x + 78, rect.y, 70, 23), "Refresh"))
        {
            ValidateAndRegularizeObject((GameObject)oldObject);
        }
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(rect, element, new GUIContent(index.ToString()), false);
        if (EditorGUI.EndChangeCheck() && element.objectReferenceValue != oldObject)
        {
            if (oldObject != null)
            {
                GameObject.DestroyImmediate(oldObject);
            }
            if (element.objectReferenceValue != null)
            {
                var prefabType = PrefabUtility.GetPrefabAssetType(element.objectReferenceValue);
                if (prefabType != PrefabAssetType.Regular && prefabType != PrefabAssetType.Variant)
                {
                    ShowNotification(new GUIContent("Please select a prefab"), 1.5f);
                    element.objectReferenceValue = null;
                }
                else
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(element.objectReferenceValue);
                    ValidateAndRegularizeObject(instance);
                    instance.hideFlags = (HideFlags)8;
                    instance.transform.parent = root.transform;
                    instance.transform.position = CalculatePositionByIndex(instance, index);
                    element.objectReferenceValue = instance;
                    Selection.objects = new Object[] { instance };
                    SceneView.FrameLastActiveSceneView();
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    void OnAddElementOfObjectList(ReorderableList list)
    {
        list.serializedProperty.arraySize++;
        var element = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
        element.objectReferenceValue = null;
        serializedObject.ApplyModifiedProperties();
#if UNITY_2021_1_OR_NEWER
        list.ClearSelection();
#else
        list.index = -1;
#endif
    }

    void OnRemoveElementOfObjectList(ReorderableList list)
    {
#if UNITY_2021_1_OR_NEWER
        for (int i = 0; i < list.selectedIndices.Count; ++i)
        {
            int srcIndex = list.selectedIndices[i];
            int dstIndex = list.serializedProperty.arraySize - i - 1;
            list.serializedProperty.MoveArrayElement(srcIndex, dstIndex);
        }
        if (list.serializedProperty.arraySize > 0)
        {
            if (list.selectedIndices.Count == 0)
            {
                var element = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
                if (element.objectReferenceValue != null)
                {
                    GameObject.DestroyImmediate(element.objectReferenceValue);
                }
            }
            else
            {
                for (int i = 0; i < list.selectedIndices.Count; ++i)
                {
                    var element = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - i - 1);
                    if (element.objectReferenceValue != null)
                    {
                        GameObject.DestroyImmediate(element.objectReferenceValue);
                    }
                }
            }
            list.serializedProperty.arraySize -= Mathf.Max(1, list.selectedIndices.Count);
        }
        serializedObject.ApplyModifiedProperties();
        list.ClearSelection();
#else
        var selectedIndex = list.index;
        if (selectedIndex >= 0)
        {
            int srcIndex = selectedIndex;
            int dstIndex = list.serializedProperty.arraySize - 1;
            list.serializedProperty.MoveArrayElement(srcIndex, dstIndex);
        }
        if (list.serializedProperty.arraySize > 0)
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
            if (element.objectReferenceValue != null)
            {
                GameObject.DestroyImmediate(element.objectReferenceValue);
            }
            list.serializedProperty.arraySize--;
        }
        serializedObject.ApplyModifiedProperties();
        list.index = list.serializedProperty.arraySize > 0 ? selectedIndex : -1;
#endif
        for (int i = 0; i < list.serializedProperty.arraySize; ++i)
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue != null)
            {
                var gameObject = (GameObject)element.objectReferenceValue;
                gameObject.transform.position = CalculatePositionByIndex(gameObject, i);
            }
        }
    }

    void OnSelectElementOfObjectList(ReorderableList list)
    {
#if UNITY_2021_1_OR_NEWER
        var objects = new List<Object>(list.selectedIndices.Count);
        for (int i = 0; i < list.selectedIndices.Count; ++i)
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(list.selectedIndices[i]);
            if (element.objectReferenceValue != null)
            {
                objects.Add(element.objectReferenceValue);
            }
        }
        Selection.objects = objects.ToArray();
#else
        if (list.index >= 0)
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(list.index);
            if (element.objectReferenceValue != null)
            {
                Selection.objects = new Object[] { element.objectReferenceValue };
            }
        }
        else
        {
            Selection.objects = new Object[0];
        }
#endif
        SceneView.FrameLastActiveSceneView();
    }

    void DrawObjectPage()
    {
        EditorGUI.BeginDisabledGroup(Lightmapping.isRunning);
        EditorGUI.BeginChangeCheck();
        config.unitScale = Mathf.Max(0.001f, EditorGUILayout.FloatField("Unit Scale", config.unitScale));
        config.column = EditorGUILayout.IntSlider("Column", config.column, 1, 20);
        config.space = EditorGUILayout.Slider("Space", config.space, 1, 10);
        if (EditorGUI.EndChangeCheck())
        {
            for (int i = 0; i < objects.Count; ++i)
            {
                if (objects[i] == null) continue;
                objects[i].transform.position = CalculatePositionByIndex(objects[i], i);
            }
        }
        objectList.DoLayoutList();
        if (GUILayout.Button("Import from Folder"))
        {
            var folder = EditorUtility.OpenFolderPanel("Select Folder", "", "");
            if (!string.IsNullOrEmpty(folder))
            {
                var prefabPaths = new DirectoryInfo(folder).GetFiles("*.prefab", SearchOption.AllDirectories);
                int progress = 0;
                foreach (var path in prefabPaths)
                {
                    EditorUtility.DisplayProgressBar("Importing Prefabs", path.FullName, progress / (float)prefabPaths.Length * 100.0f);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.FullName.Substring(Application.dataPath.Length - 6));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    ValidateAndRegularizeObject(instance);
                    instance.hideFlags = (HideFlags)8;
                    instance.transform.parent = root.transform;
                    objectList.serializedProperty.arraySize++;
                    var element = objectList.serializedProperty.GetArrayElementAtIndex(objectList.serializedProperty.arraySize - 1);
                    element.objectReferenceValue = instance;
                    instance.transform.position = CalculatePositionByIndex(instance, objectList.serializedProperty.arraySize - 1);
                }
                serializedObject.ApplyModifiedProperties();
                EditorUtility.ClearProgressBar();
            }
        }
        EditorGUI.BeginDisabledGroup(objects.Count == 0);
        if (GUILayout.Button("View All"))
        {
            var objects = new List<Object>(objectList.serializedProperty.arraySize);
            for (int i = 0; i < objectList.serializedProperty.arraySize; ++i)
            {
                var element = objectList.serializedProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != null)
                {
                    objects.Add(element.objectReferenceValue);
                }
            }
            Selection.objects = objects.ToArray();
            SceneView.FrameLastActiveSceneView();

        }
        if (GUILayout.Button("Remove All"))
        {
            objectList.index = -1;
            for (int i = 0; i < objectList.serializedProperty.arraySize; ++i)
            {
                var element = objectList.serializedProperty.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != null)
                {
                    GameObject.DestroyImmediate(element.objectReferenceValue);
                }
            }
            objectList.serializedProperty.ClearArray();
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUI.EndDisabledGroup();
    }

    bool geometryFoldout = true;
    bool environmentFoldout = true;
    bool qualityFoldout = true;
    bool featureFoldout = true;
    static readonly string[] environmentSource = { "Skybox", "Gradient", "Color" };
    static readonly string[] lightmapMaxSizes = { "32", "64", "128", "256", "512", "1024" };

    void DrawBakingPage()
    {
        EditorGUI.BeginDisabledGroup(Lightmapping.isRunning);
        EditorGUI.BeginChangeCheck();
        geometryFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(geometryFoldout, "Geometry");
        if (geometryFoldout)
        {
            config.generateContactQuad = EditorGUILayout.Toggle("Generate Contact Quad", config.generateContactQuad);
            if (config.generateContactQuad)
            {
                config.contactQuadMaterial = (Material)EditorGUILayout.ObjectField("      Material", config.contactQuadMaterial, typeof(Material), false);
                config.contactQuadScale = Mathf.Max(1f, EditorGUILayout.FloatField("      Scale", config.contactQuadScale));
                config.contactQuadOffset = Mathf.Max(0.001f, EditorGUILayout.FloatField("      Offset", config.contactQuadOffset));
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        if (EditorGUI.EndChangeCheck())
        {
            for (int i = 0; i < objects.Count; ++i)
            {
                if (objects[i] == null) continue;
                objects[i].transform.position = CalculatePositionByIndex(objects[i], i);
                GenerateContactQuad(objects[i]);
            }
        }
        EditorGUILayout.Separator();
        environmentFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(environmentFoldout, "Environment");
        if (environmentFoldout)
        {
            int selectedIndex = 0;
            if (RenderSettings.ambientMode == AmbientMode.Skybox) selectedIndex = 0;
            else if (RenderSettings.ambientMode == AmbientMode.Trilight) selectedIndex = 1;
            else if (RenderSettings.ambientMode == AmbientMode.Flat) selectedIndex = 2;
            selectedIndex = EditorGUILayout.Popup("Source", selectedIndex, environmentSource);
            switch (selectedIndex)
            {
                case 0:
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.skybox = (Material)EditorGUILayout.ObjectField("      Skybox", RenderSettings.skybox, typeof(Material), false);
                    RenderSettings.ambientLight = EditorGUILayout.ColorField(new GUIContent("      Color"), RenderSettings.ambientLight, true, false, true);
                    break;
                case 1:
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = EditorGUILayout.ColorField(new GUIContent("      Sky Color"), RenderSettings.ambientSkyColor, true, false, true);
                    RenderSettings.ambientEquatorColor = EditorGUILayout.ColorField(new GUIContent("      Equator Color"), RenderSettings.ambientEquatorColor, true, false, true);
                    RenderSettings.ambientGroundColor = EditorGUILayout.ColorField(new GUIContent("      Ground Color"), RenderSettings.ambientGroundColor, true, false, true);
                    break;
                case 2:
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = EditorGUILayout.ColorField(new GUIContent("      Color"), RenderSettings.ambientLight, true, false, true);
                    break;
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        var lightingSettings = Lightmapping.lightingSettings;
        EditorGUILayout.Separator();
        qualityFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(qualityFoldout, "Quality");
        if (qualityFoldout)
        {
            lightingSettings.directSampleCount = Mathf.Max(1, EditorGUILayout.IntField("Direct Samples", lightingSettings.directSampleCount));
            lightingSettings.indirectSampleCount = Mathf.Max(8, EditorGUILayout.IntField("Indirect Samples", lightingSettings.indirectSampleCount));
            lightingSettings.environmentSampleCount = Mathf.Max(8, EditorGUILayout.IntField("Environment Samples", lightingSettings.environmentSampleCount));
            lightingSettings.lightmapResolution = Mathf.Max(1, EditorGUILayout.FloatField("Lightmap Resolution", lightingSettings.lightmapResolution));
            lightingSettings.lightmapPadding = Mathf.Clamp(EditorGUILayout.IntField("Lightmap Padding", lightingSettings.lightmapPadding), 2, 100);
            int selectedIndex = (int)Mathf.Log(lightingSettings.lightmapMaxSize, 2) - 5;
            selectedIndex = EditorGUILayout.Popup("Max Lightmap Size", selectedIndex, lightmapMaxSizes);
            lightingSettings.lightmapMaxSize = (int)Mathf.Pow(2, selectedIndex + 5);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Separator();
        featureFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(featureFoldout, "Feature");
        EditorGUI.BeginChangeCheck();
        if (featureFoldout)
        {
            lightingSettings.ao = EditorGUILayout.Toggle("Ambient Occlusion", lightingSettings.ao);
            if (lightingSettings.ao)
            {
                lightingSettings.aoMaxDistance = Mathf.Max(0, EditorGUILayout.FloatField("      Max Distance", lightingSettings.aoMaxDistance));
                lightingSettings.aoExponentIndirect = EditorGUILayout.Slider("      Intensity", lightingSettings.aoExponentIndirect, 0, 10);
            }
            lightingSettings.albedoBoost = EditorGUILayout.Slider("Albedo Boost", lightingSettings.albedoBoost, 1, 10);
            lightingSettings.indirectScale = EditorGUILayout.Slider("Indirect Intensity", lightingSettings.indirectScale, 0, 5);
        }
        if (EditorGUI.EndChangeCheck())
        {
            for (int i = 0; i < objects.Count; ++i)
            {
                if (objects[i] == null) continue;
                GenerateContactQuad(objects[i]);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Separator();
        EditorGUI.EndDisabledGroup();
        if (!Lightmapping.isRunning && GUILayout.Button("Bake") && objects.Count > 0)
        {
            bool haveMissingStatus = false;
            for (int i = 0; i < objects.Count; ++i)
            {
                if (objects[i] != null && objects[i].name.Contains(MISSING_FLAG_HEADER))
                {
                    haveMissingStatus = true;
                }
            }
            if (!haveMissingStatus ||
                (haveMissingStatus && EditorUtility.DisplayDialog("Notice", "Some prefabs have missing status. Would you like to continue baking?", "Bake", "Cancel")))
            {
                Lightmapping.ClearDiskCache();
                EditorSceneManager.MarkSceneDirty(nowOpened);
                EditorSceneManager.SaveScene(nowOpened);
                Lightmapping.BakeAsync();
                saveChangesMessage = "The prefabs are baking. Would you like to save and close?";
            }
        }
        else if (Lightmapping.isRunning && GUILayout.Button("Cancel"))
        {
            Lightmapping.Cancel();
        }
    }

    static readonly string[] maxAtlasSizes = { "64", "128", "256", "512", "1024", "2048" };
    bool lightmapsFoldout = true;
    bool exportSettingFoldout = true;

    Rect[] PackTextures(Texture2D atlas, List<Texture2D> textures)
    {
        var offset = new Vector2();
        var rects = new Rect[textures.Count];
        var textureSort = textures.ToArray();
        System.Array.Sort(textureSort, (a, b) =>
        {
            int areaA = a.width * a.height;
            int areaB = b.width * b.height;
            return areaB - areaA;
        });
        var points = new List<Vector2>();
        for (int i = 0; i < textureSort.Length; ++i)
        {
            var width = textureSort[i].width;
            var height = textureSort[i].height;
            if (points.Count > 0)
            {
                var minCost = float.MaxValue;
                var minCostPoint = new Vector2();
                foreach (var point in points)
                {
                    var xLeft = point.x + width - atlas.width;
                    var yLeft = point.y + height - atlas.height;
                    if (xLeft > 0 || yLeft > 0) continue;
                    if (minCost >= xLeft + yLeft)
                    {
                        minCost = xLeft + yLeft;
                        minCostPoint = point;
                    }
                }
                offset = minCostPoint;
                for (int k = 0; k < points.Count; ++k)
                {
                    if (points[k] == offset)
                    {
                        points.RemoveAt(k);
                        k--;
                    }
                }
            }
            var pixels = textureSort[i].GetPixels();
            atlas.SetPixels((int)offset.x, (int)offset.y, width, height, pixels);
            int index = textures.IndexOf(textureSort[i]);
            rects[index] = new Rect(offset.x / atlas.width, offset.y / atlas.height, width / (float)atlas.width, height / (float)atlas.height);
            points.Add(new Vector2(offset.x, offset.y + height));
            points.Add(new Vector2(offset.x + width, offset.y));
        }
        return rects;
    }

    void ExportToPrefabs()
    {
        var folder = EditorUtility.SaveFolderPanel("Save Lightmaps To Folder", "", "");
        if (string.IsNullOrEmpty(folder)) return;
        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i] == null || objects[i].name.Contains(MISSING_FLAG_HEADER)) continue;
            EditorUtility.DisplayProgressBar("Importing Prefabs", objects[i].name, i / (float)objects.Count * 100.0f);
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(objects[i]);
            var root = PrefabUtility.LoadPrefabContents(path);
            var infos = root.GetComponentsInChildren<PrefabBakerLightmapInfo>();
            foreach (var info in infos)
            {
                if (info.infoTag.Trim() != config.exportInfoTag) continue;
                if (info.lightmap != null)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(info.lightmap));
                }
                GameObject.DestroyImmediate(info);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var atlasDict = new Dictionary<string, Texture2D>();
        var atlasRectDict = new Dictionary<string, Rect>();
        if (config.packLightmaps)
        {
            List<Texture2D> lightmaps = new List<Texture2D>();
            int area = 0;
            int id = 0;
            for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
            {
                var lightmap = LightmapSettings.lightmaps[i].lightmapColor;
                var path = AssetDatabase.GetAssetPath(lightmap);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                importer.isReadable = true;
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                area += lightmap.height * lightmap.width;
                lightmaps.Add(lightmap);
                if (area >= config.maxAtlasSize * config.maxAtlasSize || i == LightmapSettings.lightmaps.Length - 1)
                {
                    area = Mathf.Min(area, config.maxAtlasSize * config.maxAtlasSize);
                    var size = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(Mathf.Sqrt(area), 2)));
                    var atlas = new Texture2D(size, size, GraphicsFormat.R16G16B16A16_SFloat, TextureCreationFlags.None);
                    var rects = PackTextures(atlas, lightmaps);
                    var tag = string.IsNullOrEmpty(config.exportInfoTag) ? "" : "_" + config.exportInfoTag;
                    var newName = "/LightmapAtlas-" + id + tag + ".exr";
                    var newPath = folder.Substring(Application.dataPath.Length - 6) + newName;
                    var bytes = atlas.EncodeToEXR();
                    var fullPath = folder + newName;
                    File.WriteAllBytes(fullPath, bytes);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(newPath);
                    importer = AssetImporter.GetAtPath(newPath) as TextureImporter;
                    importer.textureType = TextureImporterType.Lightmap;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                    atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
                    for (int k = 0; k < lightmaps.Count; ++k)
                    {
                        atlasDict[lightmaps[k].name] = atlas;
                        atlasRectDict[lightmaps[k].name] = rects[k];
                    }
                    id++;
                    area = 0;
                    lightmaps.Clear();
                }
            }
            for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
            {
                var lightmap = LightmapSettings.lightmaps[i].lightmapColor;
                var path = AssetDatabase.GetAssetPath(lightmap);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                importer.isReadable = false;
                importer.textureType = TextureImporterType.Lightmap;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }
        var texDict = new Dictionary<string, Texture2D>();
        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i] == null || objects[i].name.Contains(MISSING_FLAG_HEADER)) continue;
            var renderers = objects[i].GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.lightmapIndex < 0 || renderer.lightmapIndex >= LightmapSettings.lightmaps.Length) continue;
                var info = renderer.gameObject.AddComponent<PrefabBakerLightmapInfo>();
                info.infoTag = config.exportInfoTag;
                if (!string.IsNullOrEmpty(info.infoTag)) info.enabled = false;
                var lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapColor;
                if (atlasDict.ContainsKey(lightmap.name))
                {
                    info.lightmap = atlasDict[lightmap.name];
                    var scaleOffset = new Vector4(atlasRectDict[lightmap.name].width, atlasRectDict[lightmap.name].height,
                        atlasRectDict[lightmap.name].x, atlasRectDict[lightmap.name].y);
                    scaleOffset.x *= renderer.lightmapScaleOffset.x;
                    scaleOffset.y *= renderer.lightmapScaleOffset.y;
                    scaleOffset.z += renderer.lightmapScaleOffset.z * atlasRectDict[lightmap.name].width;
                    scaleOffset.w += renderer.lightmapScaleOffset.w * atlasRectDict[lightmap.name].height;
                    info.scaleOffset = scaleOffset;
                }
                else
                {
                    info.scaleOffset = renderer.lightmapScaleOffset;
                    if (!texDict.ContainsKey(lightmap.name))
                    {
                        var path = AssetDatabase.GetAssetPath(lightmap);
                        var tag = string.IsNullOrEmpty(config.exportInfoTag) ? "" : "_" + config.exportInfoTag;
                        var newPath = folder.Substring(Application.dataPath.Length - 6) + "/Lightmap-" + i + tag + "_" + objects[i].name + Path.GetExtension(path);
                        AssetDatabase.CopyAsset(path, newPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(newPath);
                        texDict[lightmap.name] = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
                    }
                    info.lightmap = texDict[lightmap.name];
                }
            }
            var addedGameObjects = PrefabUtility.GetAddedGameObjects(objects[i]);
            foreach (var addedGameObject in addedGameObjects)
            {
                if (addedGameObject.instanceGameObject.name == CONTACT_QUAD_NAME)
                {
                    addedGameObject.Apply(InteractionMode.AutomatedAction);
                }
            }
            var addedComponents = PrefabUtility.GetAddedComponents(objects[i]);
            foreach (var addedComponent in addedComponents)
            {
                if (addedComponent.instanceComponent is PrefabBakerLightmapInfo)
                {
                    addedComponent.Apply(InteractionMode.AutomatedAction);
                }
            }
            var quad = objects[i].transform.Find(CONTACT_QUAD_NAME);
            var so = new SerializedObject(quad.gameObject);
            so.FindProperty("m_StaticEditorFlags").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            var objectOverrides = PrefabUtility.GetObjectOverrides(objects[i]);
            foreach (var objectOverride in objectOverrides)
            {
                if (objectOverride.instanceObject.name == CONTACT_QUAD_NAME ||
                    objectOverride.instanceObject is PrefabBakerLightmapInfo)
                {
                    objectOverride.Apply(InteractionMode.AutomatedAction);
                }
            }
            quad.gameObject.isStatic = true;
        }
        EditorUtility.ClearProgressBar();
    }

    void ClearExport()
    {
        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i] == null) continue;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(objects[i]);
            var root = PrefabUtility.LoadPrefabContents(path);
            var quad = root.transform.Find(CONTACT_QUAD_NAME);
            if (config.clearGeneratedQuad && quad != null)
            {
                GameObject.DestroyImmediate(quad.gameObject);
            }
            var infos = root.GetComponentsInChildren<PrefabBakerLightmapInfo>();
            foreach (var info in infos)
            {
                if (info.infoTag.Trim() != config.exportInfoTag) continue;
                if (config.clearExportedLightmaps && info.lightmap != null)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(info.lightmap));
                }
                GameObject.DestroyImmediate(info);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            ValidateAndRegularizeObject(objects[i]);
        }
    }

    void DrawExportPage()
    {
        lightmapsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(lightmapsFoldout, "Lightmaps");
        if (lightmapsFoldout)
        {
            EditorGUILayout.LabelField("      Count", LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length.ToString() : "0");
            if (LightmapSettings.lightmaps != null)
            {
                for (int i = 0; i < LightmapSettings.lightmaps.Length; ++i)
                {
                    var data = LightmapSettings.lightmaps[i];
                    EditorGUILayout.ObjectField("      " + data.lightmapColor.name, data.lightmapColor, typeof(Texture2D), false);
                }
                if (LightmapSettings.lightmaps.Length > 0 && GUILayout.Button("Clear Lightmaps"))
                {
                    Lightmapping.Clear();
                    Lightmapping.ClearDiskCache();
                    Lightmapping.ClearLightingDataAsset();
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUI.BeginDisabledGroup(Lightmapping.isRunning);
        exportSettingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(exportSettingFoldout, "Export Setting");
        if (exportSettingFoldout)
        {
            config.packLightmaps = EditorGUILayout.Toggle("Pack Lightmaps", config.packLightmaps);
            if (config.packLightmaps)
            {
                int selectedIndex = (int)Mathf.Log(config.maxAtlasSize, 2) - 6;
                selectedIndex = EditorGUILayout.Popup("      Max Size", selectedIndex, maxAtlasSizes);
                config.maxAtlasSize = (int)Mathf.Pow(2, selectedIndex + 6);
            }
            config.clearGeneratedQuad = EditorGUILayout.Toggle("Clear Generated Quad", config.clearGeneratedQuad);
            config.clearExportedLightmaps = EditorGUILayout.Toggle("Clear Exported Lightmaps", config.clearExportedLightmaps);
            config.exportInfoTag = EditorGUILayout.TextField("Info Tag", config.exportInfoTag).Trim();
            EditorGUI.BeginDisabledGroup(objects.Count == 0 || LightmapSettings.lightmaps == null || LightmapSettings.lightmaps.Length == 0);
            if (GUILayout.Button("Export to Prefabs"))
            {
                ExportToPrefabs();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(objects.Count == 0);
            if (GUILayout.Button("Clear Export"))
            {
                ClearExport();
            }
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUI.EndDisabledGroup();
    }

    void OnGUI()
    {
        hasUnsavedChanges = Lightmapping.isRunning;
        EditorGUI.BeginChangeCheck();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.Separator();
        selectedPage = GUILayout.Toolbar(selectedPage, pages);
        switch (selectedPage)
        {
            case 0:
                DrawObjectPage();
                break;
            case 1:
                DrawBakingPage();
                break;
            case 2:
                DrawExportPage();
                break;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
        if (EditorGUI.EndChangeCheck())
        {
            EditorSceneManager.MarkSceneDirty(nowOpened);
        }
    }

    void OnDestroy()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
        EditorSceneManager.MarkSceneDirty(nowOpened);
        EditorSceneManager.SaveScene(nowOpened);
        if (!string.IsNullOrEmpty(lastOpenedPath))
        {
            EditorSceneManager.OpenScene(lastOpenedPath, OpenSceneMode.Single);
        }
    }
}
