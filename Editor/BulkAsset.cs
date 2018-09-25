using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class BulkAsset : AssetPostprocessor
{
    static string[] paths = new string[] { "icons" };
    static string notification;
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        BulkAssetManifest manifest = null;
        UFEx.IconsManifest iconsManifest = null;
        bool manifestLoad = false;
        bool idGot = false;
        bool changeIconsMainfest = false;
        for (int i = 0;i < importedAssets.Length;++i)
        {
            if (FilterPath(importedAssets[i]))
            {
                if (!manifestLoad)
                {
                    manifest = LoadManifest();
                    iconsManifest = LoadIconsManifest();
                    manifestLoad = true;
                }
                idGot = RenameOrReplace(manifest, iconsManifest, importedAssets[i]);
                changeIconsMainfest = true;
            }
        }
        for (int i = 0;i < movedAssets.Length;++i)
        {
            if (FilterPath(movedAssets[i]))
            {
                if (!manifestLoad)
                {
                    manifest = LoadManifest();
                    iconsManifest = LoadIconsManifest();
                    manifestLoad = true;
                }
                idGot = RenameOrReplace(manifest, iconsManifest, movedAssets[i]);
                changeIconsMainfest = true;
            }
        }

        for (int i = 0; i < deletedAssets.Length; ++i)
        {
            if (FilterPath(deletedAssets[i]))
            {
                if (!manifestLoad)
                {
                    manifest = LoadManifest();
                    iconsManifest = LoadIconsManifest();
                    manifestLoad = true;
                }
                DeleteIconsManifest(iconsManifest, deletedAssets[i]);
                changeIconsMainfest = true;
            }
        }
        if (!manifestLoad && (deletedAssets.Length > 0 || movedAssets.Length > 0))
        {
            LoadManifest();
        }
        if (!string.IsNullOrEmpty(notification))
        {
            EditorWindow.focusedWindow.ShowNotification(new GUIContent(notification));
            notification = string.Empty;
        }
        if (idGot)
        {
            EditorUtility.SetDirty(manifest);
        }
        if (iconsManifest != null && changeIconsMainfest)
            EditorUtility.SetDirty(iconsManifest);
    }
    static BulkAssetManifest LoadManifest()
    {
        Object[] loadList = AssetDatabase.LoadAllAssetsAtPath("Assets/" + PathHelper.GameAssets + "/bulkasset.asset");
        if (loadList.Length != 0 && loadList[0] != null)
        {
            bool repeat = false;
            for (int i = 0;i < paths.Length;++i)
            {
                DirectoryInfo folder = new DirectoryInfo(Application.dataPath + "/" + PathHelper.GameAssets + "/" + paths[i]);
                FileInfo[] files = folder.GetFiles("*.*", SearchOption.AllDirectories);
                if (files.Length > 2)
                {
                    HashSet<uint> derepeats = new HashSet<uint>();
                    for (int k = 0;k < files.Length;++k)
                    {
                        if (files[k].Name.EndsWith(".meta")) continue;
                        string[] split = Path.GetFileNameWithoutExtension(files[k].Name).Split('_');
                        if (split.Length > 1)
                        {
                            uint fileID = uint.Parse(split[split.Length - 1]);
                            if (!derepeats.Add(fileID))
                            {
                                repeat = true;
                            }
                        }
                    }
                }
            }
            if (repeat)
            {
                notification = "检测到重复编号，请删除后重提，注意及时提交bulkasset修改";
            }
            return loadList[0] as BulkAssetManifest;
        }
        else
        {
            bool repeat = false;
            var manifest = new BulkAssetManifest();
            uint id = 0;
            for (int i = 0;i < paths.Length;++i)
            {
                DirectoryInfo folder = new DirectoryInfo(Application.dataPath + "/" + PathHelper.GameAssets + "/" + paths[i]);
                FileInfo[] files = folder.GetFiles("*.*", SearchOption.AllDirectories);
                if (files.Length > 2)
                {
                    HashSet<uint> derepeats = new HashSet<uint>();
                    for (int k = 0;k < files.Length;++k)
                    {
                        if (files[k].Name.EndsWith(".meta")) continue;
                        string[] split = Path.GetFileNameWithoutExtension(files[k].Name).Split('_');
                        uint fileID = uint.Parse(split[split.Length - 1]);
                        if (!derepeats.Add(fileID))
                        {
                            repeat = true;
                        }
                        id = id > fileID ? id : fileID;
                    }
                }
            }
            if (repeat)
            {
                notification = "检测到重复编号，请删除后重提，注意及时提交bulkasset修改";
            }
            manifest.IncreasedID = id + 1;
            AssetDatabase.CreateAsset(manifest, "Assets/" + PathHelper.GameAssets + "/bulkasset.asset");
            return manifest;
        }
    }

    static UFEx.IconsManifest LoadIconsManifest()
    {
        UFEx.IconsManifest iconsManifest =
            UnityEditor.AssetDatabase.LoadAssetAtPath<UFEx.IconsManifest>(
                "Assets/" + PathHelper.GameAssets + "/iconsmanifest.asset");
        if (iconsManifest == null)
        {
            iconsManifest = ScriptableObject.CreateInstance<UFEx.IconsManifest>();
            AssetDatabase.CreateAsset(iconsManifest, "Assets/" + PathHelper.GameAssets + "/iconsmanifest.asset");
        }

        return iconsManifest;
    }

    static bool FilterPath(string url)
    {
        for (int i = 0;i < paths.Length;++i)
        {
            if (url.Contains(".") && url.Contains("Assets/" + PathHelper.GameAssets + "/" + paths[i] + "/"))
            {
                return true;
            }
        }
        return false;
    }
    static bool RenameOrReplace(BulkAssetManifest manifest, UFEx.IconsManifest iconManifest, string url)
    {
        string name = Path.GetFileNameWithoutExtension(url);
        string[] split = name.Split('_');
        if (split.Length > 1)
        {
            uint fileID = 0;
            if (uint.TryParse(split[split.Length - 1], out fileID))
            {
                AddIconsManifest(iconManifest, (int)fileID,
                     Path.GetDirectoryName(url).Replace('\\', '/') + "/" + name+".png");
                return false;
            }
        }
        string directory = Path.GetDirectoryName(url);
        DirectoryInfo folder = new DirectoryInfo(Application.dataPath.Replace("Assets", "") + directory);
        FileInfo[] files = folder.GetFiles("*.*", SearchOption.TopDirectoryOnly);
        for (int i = 0;i < files.Length;++i)
        {
            if (files[i].Name.EndsWith(".meta")) continue;
            split = Path.GetFileNameWithoutExtension(files[i].Name).Split('_');
            if (split.Length <= 1)
            {
                continue;
            }
            string pre = split[0];
            for (int k = 1;k < split.Length - 1;++k)
            {
                pre += split[k];
            }
            if (pre == name)
            {
                AssetDatabase.DeleteAsset(directory + "/" + files[i].Name);
                AssetDatabase.RenameAsset(url, name + "_" + split[split.Length - 1]);
                return false;
            }
        }
        if (string.IsNullOrEmpty(notification))
        {
            notification = "bulkasset已被修改，请确认bulkasset为最新，然后及时提交";
        }
        int iconId = (int)manifest.GetID();
        AssetDatabase.RenameAsset(url, name + "_" + iconId.ToString());
        AddIconsManifest(iconManifest, (int)iconId,
            Path.GetDirectoryName(url).Replace('\\','/') + "/" + name + "_" + iconId.ToString()+".png");
        return true;
    }
    public static bool ResetAssetBundleName(AssetImporter importer, string assetPath)
    {
        string fileName = Path.GetFileName(assetPath);
        string[] split = fileName.Split('_');
        bool isAsset = assetPath.Contains(".");
        for (int i = 0;i < paths.Length;++i)
        {
            if (isAsset)
            {
                if (assetPath.Contains("Assets/" + PathHelper.GameAssets + "/" + paths[i] + "/"))
                {
                    string assetBundleName =  split[split.Length - 1];
                    importer.assetBundleName = assetBundleName;
                    if ((TextureImporter) importer) ((TextureImporter) importer).npotScale = TextureImporterNPOTScale.ToNearest;
                    return true;
                }
            }
        }
        return false;
    }

    static void AddIconsManifest(UFEx.IconsManifest manifest, int id, string url)
    {
        manifest.records[id] = url;
    }

    static void DeleteIconsManifest(UFEx.IconsManifest manifest, string url)
    {
        foreach(var pair in manifest.records)
        {
            if(pair.Value == url)
            {
                manifest.records.Remove(pair.Key);
                break;
            }
        }
    }
}
