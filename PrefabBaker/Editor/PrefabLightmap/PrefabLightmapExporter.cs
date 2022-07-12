using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class PrefabLightmapExporter
{
    static Rect[] PackTextures(Texture2D atlas, List<Texture2D> textures)
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

    public static void ExportToPrefabs(List<GameObject> objects, string infoTag, bool packLightmaps = false, int maxAtlasSize = 0, Action<GameObject> func = null)
    {
        var folder = EditorUtility.SaveFolderPanel("Save Lightmaps To Folder", "", "");
        if (string.IsNullOrEmpty(folder)) return;
        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i] == null) continue;
            var prefabType = PrefabUtility.GetPrefabAssetType(objects[i]);
            if (prefabType != PrefabAssetType.Regular && prefabType != PrefabAssetType.Variant) continue;
            EditorUtility.DisplayProgressBar("Importing Prefabs", objects[i].name, i / (float)objects.Count * 100.0f);
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(objects[i]);
            var root = PrefabUtility.LoadPrefabContents(path);
            var infos = root.GetComponentsInChildren<PrefabLightmapInfo>();
            foreach (var info in infos)
            {
                if (info.infoTag.Trim() != infoTag) continue;
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
        if (packLightmaps)
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
                if (area >= maxAtlasSize * maxAtlasSize || i == LightmapSettings.lightmaps.Length - 1)
                {
                    area = Mathf.Min(area, maxAtlasSize * maxAtlasSize);
                    var size = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(Mathf.Sqrt(area), 2)));
                    var atlas = new Texture2D(size, size, GraphicsFormat.R16G16B16A16_SFloat, TextureCreationFlags.None);
                    var rects = PrefabLightmapExporter.PackTextures(atlas, lightmaps);
                    var tag = string.IsNullOrEmpty(infoTag) ? "" : "_" + infoTag;
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
            if (objects[i] == null) continue;
            var prefabType = PrefabUtility.GetPrefabAssetType(objects[i]);
            if (prefabType != PrefabAssetType.Regular && prefabType != PrefabAssetType.Variant) continue;
            var renderers = objects[i].GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.lightmapIndex < 0 || renderer.lightmapIndex >= LightmapSettings.lightmaps.Length) continue;
                var info = renderer.gameObject.AddComponent<PrefabLightmapInfo>();
                info.infoTag = infoTag;
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
                        var tag = string.IsNullOrEmpty(infoTag) ? "" : "_" + infoTag;
                        var newPath = folder.Substring(Application.dataPath.Length - 6) + "/Lightmap-" + i + tag + "_" + objects[i].name + Path.GetExtension(path);
                        AssetDatabase.CopyAsset(path, newPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(newPath);
                        texDict[lightmap.name] = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
                    }
                    info.lightmap = texDict[lightmap.name];
                }
            }
            var addedComponents = PrefabUtility.GetAddedComponents(objects[i]);
            foreach (var addedComponent in addedComponents)
            {
                if (addedComponent.instanceComponent is PrefabLightmapInfo)
                {
                    addedComponent.Apply(InteractionMode.AutomatedAction);
                }
            }
            var objectOverrides = PrefabUtility.GetObjectOverrides(objects[i]);
            foreach (var objectOverride in objectOverrides)
            {
                if (objectOverride.instanceObject is PrefabLightmapInfo)
                {
                    objectOverride.Apply(InteractionMode.AutomatedAction);
                }
            }
            func?.Invoke(objects[i]);
        }
        EditorUtility.ClearProgressBar();
    }

    public static void ClearExport(List<GameObject> objects, string infoTag, bool clearLightmaps, Action<GameObject> func = null)
    {
        for (int i = 0; i < objects.Count; ++i)
        {
            if (objects[i] == null) continue;
            var prefabType = PrefabUtility.GetPrefabAssetType(objects[i]);
            if (prefabType != PrefabAssetType.Regular && prefabType != PrefabAssetType.Variant) continue;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(objects[i]);
            var root = PrefabUtility.LoadPrefabContents(path);
            var infos = root.GetComponentsInChildren<PrefabLightmapInfo>();
            foreach (var info in infos)
            {
                if (info.infoTag.Trim() != infoTag) continue;
                if (clearLightmaps && info.lightmap != null)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(info.lightmap));
                }
                GameObject.DestroyImmediate(info);
            }
            func?.Invoke(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}