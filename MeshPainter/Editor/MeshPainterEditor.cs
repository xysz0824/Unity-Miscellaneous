using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class MeshPainterEditor : EditorWindow
{
    Transform currentSelect;
    MeshCollider tempMeshCollider;

    int selBrush = 0;
    int oldSelBrush;
    float brushSize = 1;
    float brushStrength = 0.5f;

    Texture[] TexBrush;
    Texture[] TexChannel;

    Vector2 scrollPos;

    int selTexture = 0;
    int brushSizeInTex;
    int oldBrushSizeInPourcent;
    Color targetColor;
    Texture2D targetTexture;
    Color[] brushColor;

    static string LocationGUID = "8924c82b-25d8-4ed0-9e8c-f739f12228e1";
    static string ScriptFolder;
    static string EditorFolder;
    static string BrushFolder;

    bool disabled;
    enum PaintMode
    {
        Mask,
        Red,
        Green,
        Blue,
        Alpha
    }
    PaintMode paintMode;
    bool paintable;
    enum UVSet
    {
        UV1,
        UV2
    }
    UVSet uvSet;

    bool eraser;

    void OnDestroy()
    {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
        if (tempMeshCollider != null)
        {
            DestroyImmediate(tempMeshCollider);
        }
    }

    public static string GetRootAssetPath()
    {
        var guid = AssetDatabase.FindAssets(LocationGUID);
        if (guid.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid[0]);
            path = path.Remove(path.Length - LocationGUID.Length, LocationGUID.Length);
            return path;
        }
        return null;
    }

    [MenuItem("Window/Mesh Painter")]
    static void Initialize()
    {
        var rootAssetPath = GetRootAssetPath();
        if (rootAssetPath != null)
        {
            ScriptFolder = rootAssetPath;
            EditorFolder = ScriptFolder + "Editor/";
            BrushFolder = ScriptFolder + "Editor/Brushes/";
        }
        else
        {
            Debug.LogError("MeshPainter found the location GUID has gone, please check files are complete");
            return;
        }
        MeshPainterEditor window = (MeshPainterEditor)EditorWindow.GetWindowWithRect(typeof(MeshPainterEditor), new Rect(0, 0, 372, 700), false, "Mesh Painter");
        window.Show();
        SceneView.onSceneGUIDelegate += window.OnSceneGUI;
    }

    void OnInspectorUpdate()
    {
        Repaint();
    }

    static Action<string, MessageType> CustomHelpBox = (str, msg) =>
    {
        var backupFontSize = EditorStyles.helpBox.fontSize;
        EditorStyles.helpBox.fontSize = 14;
        EditorGUILayout.HelpBox(str, msg);
        EditorStyles.helpBox.fontSize = backupFontSize;
    };

    GUIStyle TitleLabel;
    GUIStyle BrushGridCell;
    GUIStyle ChannelGridCell;
    GUIStyle RightAlignmentLabel;
    GUIStyle MiddleAlignmentLabel;

    void OnGUI()
    {
        paintable = false;
        if (TitleLabel == null)
        {
            TitleLabel = new GUIStyle();
            TitleLabel.fontStyle = FontStyle.Bold;
            TitleLabel.margin = new RectOffset(10, 10, 3, 3);
        }
        if (BrushGridCell == null)
        {
            BrushGridCell = new GUIStyle();
            BrushGridCell.fixedWidth = 44;
            BrushGridCell.fixedHeight = 44;
            BrushGridCell.padding = new RectOffset(5, 5, 5, 5);
            BrushGridCell.onNormal.background = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorFolder + "Image/active.png");
        }
        if (ChannelGridCell == null)
        {
            ChannelGridCell = new GUIStyle();
            ChannelGridCell.fixedWidth = 90;
            ChannelGridCell.fixedHeight = 90;
            ChannelGridCell.padding = new RectOffset(5, 5, 5, 5);
            ChannelGridCell.onNormal.background = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorFolder + "Image/active.png");
        }
        if (RightAlignmentLabel == null)
        {
            RightAlignmentLabel = new GUIStyle();
            RightAlignmentLabel.alignment = TextAnchor.MiddleRight;
            RightAlignmentLabel.margin = new RectOffset(5, 5, 0, 0);
        }
        if (MiddleAlignmentLabel == null)
        {
            MiddleAlignmentLabel = new GUIStyle();
            MiddleAlignmentLabel.alignment = TextAnchor.MiddleCenter;
            MiddleAlignmentLabel.margin = new RectOffset(5, 5, 0, 0);
        }

        currentSelect = Selection.activeTransform;
        if (currentSelect == null)
        {
            if (tempMeshCollider != null)
            {
                DestroyImmediate(tempMeshCollider);
            }
            CustomHelpBox("No GameObject was selected", MessageType.Warning);
            return;
        }
        var renderer = currentSelect.GetComponent<Renderer>();
        if (renderer == null)
        {
            CustomHelpBox("The GameObject you selected has no renderer", MessageType.Warning);
            return;
        }
        if (tempMeshCollider != null && tempMeshCollider.gameObject != currentSelect.gameObject)
        {
            DestroyImmediate(tempMeshCollider);
        }
        var colliders = currentSelect.GetComponents<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = collider is MeshCollider;
        }
        var meshCollider = currentSelect.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            tempMeshCollider = currentSelect.gameObject.AddComponent<MeshCollider>();
            tempMeshCollider.hideFlags = HideFlags.HideAndDontSave;
        }


        EditorGUILayout.BeginVertical();
        if ((!disabled && GUILayout.Button("Lock")) || (disabled && GUILayout.Button("Unlock")))
        {
            disabled = !disabled;
        }

        EditorGUI.BeginDisabledGroup(disabled);
        GUILayout.Label("Brush", TitleLabel);
        BrushBlock();
        EditorGUILayout.Separator();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Mode", TitleLabel);
        paintMode = (PaintMode)EditorGUILayout.EnumPopup(paintMode);
        EditorGUILayout.EndHorizontal();
        switch (paintMode)
        {
            case PaintMode.Mask:
                MaskModeBlock(renderer);
                break;
            default:
                ChannelModeBlock(renderer);
                break;
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    void BrushBlock()
    {
        var BrushList = new List<Texture>();
        var searchPath = Application.dataPath.Replace("Assets", "") + BrushFolder;
        DirectoryInfo folder = new DirectoryInfo(searchPath);
        FileInfo[] files = folder.GetFiles("*.*", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            if (file.Extension != ".png")
                continue;

            BrushList.Add(AssetDatabase.LoadAssetAtPath<Texture>(BrushFolder + file.Name));
        }
        TexBrush = BrushList.ToArray();

        GUILayout.BeginVertical("box");
        scrollPos = GUILayout.BeginScrollView(scrollPos, false, false, GUILayout.Height(132));
        selBrush = GUILayout.SelectionGrid(selBrush, TexBrush, 8, BrushGridCell);
        GUILayout.EndScrollView();
        GUILayout.Label("\"" + TexBrush[selBrush].name + "\" (" + TexBrush[selBrush].width + "x" + TexBrush[selBrush].height + ")", RightAlignmentLabel);
        GUILayout.EndVertical();
    }

    void CommonSettingBlock()
    {
        if (selBrush != oldSelBrush || brushSizeInTex != oldBrushSizeInPourcent || brushColor == null)
        {
            var brush = TexBrush[selBrush] as Texture2D;
            brushColor = new Color[brushSizeInTex * brushSizeInTex];
            for (int i = 0; i < brushSizeInTex; i++)
            {
                for (int j = 0; j < brushSizeInTex; j++)
                {
                    brushColor[j * brushSizeInTex + i] = brush.GetPixelBilinear(((float)i) / brushSizeInTex, ((float)j) / brushSizeInTex);
                }
            }
            oldSelBrush = selBrush;
            oldBrushSizeInPourcent = brushSizeInTex;
        }
        EditorGUILayout.Separator();
        uvSet = (UVSet)EditorGUILayout.EnumPopup("UV Set", uvSet);
        EditorGUILayout.Separator();
        brushSize = EditorGUILayout.FloatField("Brush Size", brushSize);
        EditorGUILayout.Separator();
        brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.05f, 1);
        EditorGUILayout.Separator();
        if (paintMode != PaintMode.Mask)
        {
            eraser = EditorGUILayout.Toggle("Eraser", eraser);
        }
        EditorGUILayout.Separator();        
    }

    void ChannelModeBlock(Renderer renderer)
    {
        var material = renderer.sharedMaterial;
        var propertyCount = ShaderUtil.GetPropertyCount(material.shader);
        var propertyName = new List<string>();
        for (int i = 0; i < propertyCount; ++i)
        {
            if (ShaderUtil.GetPropertyType(material.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                propertyName.Add(ShaderUtil.GetPropertyName(material.shader, i));
            }
        }
        GUILayout.BeginVertical("box");
        if (propertyName.Count == 0)
        {
            EditorGUILayout.HelpBox("The Material of the GameObject you selected doesn't have texture property", MessageType.Error);
        }
        else
        {
            selTexture = Mathf.Clamp(selTexture, 0, propertyName.Count);
            selTexture = EditorGUILayout.Popup("Property", selTexture, propertyName.ToArray());
            targetTexture = material.GetTexture(propertyName[selTexture]) as Texture2D;
            if (!targetTexture)
            {
                EditorGUILayout.HelpBox("The target texture of material is null or not 2D type", MessageType.Warning);
                if (GUILayout.Button("Generate"))
                {
                    GenerateTexture(material, propertyName[selTexture], new Color(0, 0, 0, 1));
                }
            }
            else
            {
                EditorGUILayout.HelpBox("To ensure that the texture you selected is paintable, check : \n1. Read/Write Enabled\n2. UnCompressed\n3. PNG/EXR type", MessageType.Info);
                paintable = true;
                CommonSettingBlock();
            }
        }
        GUILayout.EndVertical();
    }

    void MaskModeBlock(Renderer renderer)
    {
        EditorGUILayout.HelpBox("To ensure that the texture you selected is paintable, check : \n1. Read/Write Enabled\n2. UnCompressed\n3. PNG/EXR type", MessageType.Info);        
        var ChannelList = new List<Texture>();
        var material = renderer.sharedMaterial;
        for (int i = 0; i < 4; ++i)
        {
            var propertyName = "_Splat" + i;
            if (material.HasProperty(propertyName))
                ChannelList.Add((Texture)AssetPreview.GetAssetPreview(material.GetTexture("_Splat" + i)));
            else
                ChannelList.Add(null);
        }
        TexChannel = ChannelList.ToArray();
        var isSupportMat = material.HasProperty("_Control");
        GUILayout.BeginVertical("box");
        GUILayout.BeginHorizontal();
        GUILayout.Label("R", MiddleAlignmentLabel);
        GUILayout.Label("G", MiddleAlignmentLabel);
        GUILayout.Label("B", MiddleAlignmentLabel);
        GUILayout.Label("A", MiddleAlignmentLabel);
        GUILayout.EndHorizontal();
        selTexture = Mathf.Clamp(selTexture, 0, 3);
        selTexture = GUILayout.SelectionGrid(selTexture, TexChannel, 4, ChannelGridCell);
        switch (selTexture)
        {
            case 0:
                targetColor = new Color(1, 0, 0, 0);
                break;
            case 1:
                targetColor = new Color(0, 1, 0, 0);
                break;
            case 2:
                targetColor = new Color(0, 0, 1, 0);
                break;
            case 3:
                targetColor = new Color(0, 0, 0, 1);
                break;
        }
        if (!isSupportMat)
        {
            EditorGUILayout.HelpBox("The Material of the GameObject you selected is not supported", MessageType.Error);
        }
        else
        {
            targetTexture = material.GetTexture("_Control") as Texture2D;
            if (!targetTexture)
            {
                EditorGUILayout.HelpBox("The mask texture of material is null", MessageType.Warning);
                if (GUILayout.Button("Generate"))
                {
                    GenerateTexture(material, "_Control", new Color(1, 0, 0, 0));
                }
            }
            else
            {
                paintable = true;
                CommonSettingBlock();
            }
        }
        GUILayout.EndVertical();
    }

    void GenerateTexture(Material material, string propertyName, Color defaultColor)
    {
        var path = EditorUtility.SaveFilePanel("Save", Application.dataPath, "Untitled.png", "png");
        if (!string.IsNullOrEmpty(path))
        {
            var mask = new Texture2D(512, 512, TextureFormat.RGBA32, true);
            Color[] color = new Color[512 * 512];
            for (var t = 0; t < color.Length; t++)
            {
                color[t] = defaultColor;
            }
            mask.SetPixels(color);
            var data = mask.EncodeToPNG();
            File.WriteAllBytes(path, data);
            path = "Assets" + path.Remove(0, Application.dataPath.Length);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = TextureImporter.GetAtPath(path) as TextureImporter;
            var setting = new TextureImporterSettings();
            setting.textureType = TextureImporterType.Default;
            setting.sRGBTexture = true;
            setting.readable = true;
            setting.mipmapEnabled = true;
            setting.filterMode = FilterMode.Trilinear;
            setting.aniso = 4;
            setting.alphaSource = TextureImporterAlphaSource.FromInput;
            setting.alphaIsTransparency = false;
            setting.wrapMode = TextureWrapMode.Clamp;
            setting.textureShape = TextureImporterShape.Texture2D;
            importer.SetTextureSettings(setting);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            material.SetTexture(propertyName, (Texture)AssetDatabase.LoadAssetAtPath<Texture>(path));
            AssetDatabase.SaveAssets();
        }
    }

    float GetWorldSpaceBrushRadius(int trangleindex, int width, int height, int brushSize)
    {
        if (trangleindex < 0)
        {
            return 0;
        }
        var mesh = currentSelect.GetComponent<MeshFilter>().sharedMesh;
        var index = trangleindex * 3;
        var vertex1 = currentSelect.localToWorldMatrix * mesh.vertices[mesh.triangles[index]];
        var uv1 = mesh.uv[mesh.triangles[index]];
        var vertex2 = currentSelect.localToWorldMatrix * mesh.vertices[mesh.triangles[index + 1]];
        var uv2 = mesh.uv[mesh.triangles[index + 1]];
        var worldSpaceDistance = Vector3.Distance(vertex1, vertex2);
        var uvSpaceDistance = new Vector2((uv1.x - uv2.x) * width, (uv1.y - uv2.y) * height).magnitude;
        return worldSpaceDistance / uvSpaceDistance * brushSize;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!disabled && paintable)
        {
            var e = Event.current;
            var raycastHit = new RaycastHit();
            HandleUtility.AddDefaultControl(0);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.KeypadPlus)
            {
                brushSize += 0.1f;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.KeypadMinus)
            {
                brushSize -= 0.1f;
            }
            brushSizeInTex = (int)Mathf.Round((brushSize * targetTexture.width) / 100);
            if (Physics.Raycast(ray, out raycastHit, Mathf.Infinity))
            {
                var radius = GetWorldSpaceBrushRadius(raycastHit.triangleIndex, targetTexture.width, targetTexture.height, brushSizeInTex);
                Handles.SphereHandleCap(1, raycastHit.point, Quaternion.identity, radius * 0.8f, EventType.Repaint);
                if (raycastHit.transform != currentSelect)
                    return;

                if (e.type == EventType.MouseDrag && e.shift == false && e.alt == false && e.button == 0)
                {
                    Vector2 pixelUV = uvSet == UVSet.UV1 ? raycastHit.textureCoord : raycastHit.textureCoord2;
                    int PuX = Mathf.FloorToInt(pixelUV.x * targetTexture.width);
                    int PuY = Mathf.FloorToInt(pixelUV.y * targetTexture.height);
                    int x = Mathf.Clamp(PuX - brushSizeInTex / 2, 0, targetTexture.width - 1);
                    int y = Mathf.Clamp(PuY - brushSizeInTex / 2, 0, targetTexture.height - 1);
                    int width = Mathf.Clamp((PuX + brushSizeInTex / 2), 0, targetTexture.width) - x;
                    int height = Mathf.Clamp((PuY + brushSizeInTex / 2), 0, targetTexture.height) - y;
                    Color[] area = targetTexture.GetPixels(x, y, width, height, 0);
                    for (int i = 0; i < height; i++)
                    {
                        for (int j = 0; j < width; j++)
                        {
                            int index = (i * width) + j;
                            int brushIndex = Mathf.Clamp((y + i) - (PuY - brushSizeInTex / 2), 0, brushSizeInTex - 1) * brushSizeInTex + Mathf.Clamp((x + j) - (PuX - brushSizeInTex / 2), 0, brushSizeInTex - 1);
                            if (paintMode != PaintMode.Mask)
                            {
                                targetColor = area[index];
                            }
                            switch (paintMode)
                            {
                                case PaintMode.Red:
                                    targetColor.r = eraser ? 0 : 1;
                                    break;
                                case PaintMode.Green:
                                    targetColor.g = eraser ? 0 : 1;
                                    break;
                                case PaintMode.Blue:
                                    targetColor.b = eraser ? 0 : 1;
                                    break;
                                case PaintMode.Alpha:
                                    targetColor.a = eraser ? 0 : 1;
                                    break;
                            }
                            float strength = brushColor[brushIndex].a * brushStrength;
                            area[index] = Color.Lerp(area[index], targetColor, strength);
                        }
                    }
                    targetTexture.SetPixels(x, y, width, height, area, 0);
                    targetTexture.Apply();
                    e.Use();
                    //UndoObj = new Texture2D[1];
                    //UndoObj[0] = MeshPainterEditor.T4MMaskTex;
                    //Undo.RecordObjects(UndoObj, "T4MMask"); //Unity don't work correctly with this for now
                }
                else if (e.type == EventType.MouseUp && e.shift == false && e.alt == false && e.button == 0)
                {
                    var path = AssetDatabase.GetAssetPath(targetTexture);
                    byte[] bytes = null;
                    var ext = Path.GetExtension(path);
                    if (ext == ".png")
                    {
                        bytes = targetTexture.EncodeToPNG();
                        File.WriteAllBytes(path, bytes);
                    }
                    else if (ext == ".exr")
                    {
                        bytes = targetTexture.EncodeToEXR();
                        File.WriteAllBytes(path, bytes);
                    }
                }
            }
        }
    }
}