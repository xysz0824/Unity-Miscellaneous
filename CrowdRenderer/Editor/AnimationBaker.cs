using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;

public class AnimationBaker : EditorWindow
{
    enum Status
    {
        Initial,
        Ready,
    }

    Status status;
    SkinnedMeshRenderer smr;
    List<SkinnedMeshRenderer> lodSMR = new List<SkinnedMeshRenderer>();
    int lodCount;
    bool lodFoldout;
    int frameRate = 30;
    List<AnimationClip> clips = new List<AnimationClip>();
    int clipCount;
    bool clipFoldout;
    [MenuItem("Tools/Animation Baker")]
    static void Open()
    {
        var window = EditorWindow.GetWindow<AnimationBaker>("Animation Baker", false);
        window.Show();
    }
    private void OnGUI()
    {
        switch (status)
        {
            case Status.Initial:
                EditorGUILayout.HelpBox("Drag a SkinnedMeshRenderer", MessageType.Info);
                break;
            case Status.Ready:
                EditorGUILayout.HelpBox("Generate baked mesh or animations", MessageType.Info);
                break;
        }
        EditorGUILayout.BeginVertical();
        smr = EditorGUILayout.ObjectField("SkinnedMeshRenderer", smr, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        if (smr != null)
        {
            status = Status.Ready;
            lodCount = EditorGUILayout.IntField("LOD Count", lodCount);
            if (lodCount > lodSMR.Count)
            {
                for (int i = lodSMR.Count; i < lodCount; ++i)
                    lodSMR.Add(null);
            }
            else if (lodCount < lodSMR.Count)
            {
                lodSMR.RemoveRange(lodCount, lodSMR.Count - lodCount);
            }
            lodFoldout = EditorGUILayout.Foldout(lodFoldout, "LODs");
            if (lodFoldout)
            {
                for (int i = 0; i < lodCount; ++i)
                {
                    lodSMR[i] = EditorGUILayout.ObjectField("[" + i + "]", lodSMR[i], typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
                }
            }
            if (GUILayout.Button("Generate Baked Mesh"))
            {
                var path = EditorUtility.SaveFilePanelInProject("Save Mesh", smr.sharedMesh.name, "mesh", "Please enter a file name to save the mesh to");
                if (GenerateBakedMeshes(path))
                {
                    string notification = "Successfully generated baked mesh";
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(notification));
                    Debug.Log(notification);
                }
            }
            frameRate = EditorGUILayout.IntField("Frame Rate", frameRate);
            clipCount = EditorGUILayout.IntField("Animation Clip Count", clipCount);
            if (clipCount > clips.Count)
            {
                for (int i = clips.Count; i < clipCount; ++i)
                    clips.Add(null);
            }
            else if (clipCount < clips.Count)
            {
                clips.RemoveRange(clipCount, clips.Count - clipCount);
            }
            clipFoldout = EditorGUILayout.Foldout(clipFoldout, "Clips");
            if (clipFoldout)
            {
                for (int i = 0; i < clipCount; ++i)
                {
                    clips[i] = EditorGUILayout.ObjectField("[" + i + "]", clips[i], typeof(AnimationClip), true) as AnimationClip;
                }
            }
            if (clipCount > 0 && GUILayout.Button("Generate Baked Animations"))
            {
                var path = EditorUtility.SaveFilePanelInProject("Save Animations", "Baked Animations", "asset", "Please enter a file name to save the animations to");
                if (GenerateBakedAnimations(path))
                {
                    string notification = "Successfully generated baked animations";
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(notification));
                    Debug.Log(notification);
                }
            }
        }
        EditorGUILayout.EndVertical();
    }
    bool GenerateBakedMeshes(string path)
    {
        GenerateBakedMesh(smr, path, null);
        var name = Path.GetFileName(path);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        path = path.Remove(path.Length - name.Length, name.Length);
        for (int i = 0;i < lodSMR.Count; ++i)
        {
            GenerateBakedMesh(lodSMR[i], path + nameWithoutExtension + "_" + (i + 1) + ".mesh", smr);
        }
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
        return true;
    }
    void AlignBoneOrder(SkinnedMeshRenderer smr, SkinnedMeshRenderer alignedSMR, ref BoneWeight[] boneWeights)
    {
        for (int i = 0;i < boneWeights.Length; ++i)
        {
            var bone0 = smr.bones[boneWeights[i].boneIndex0];
            boneWeights[i].boneIndex0 = Array.FindIndex(alignedSMR.bones, (item) => { return item.name == bone0.name; });
            var bone1 = smr.bones[boneWeights[i].boneIndex1];
            boneWeights[i].boneIndex1 = Array.FindIndex(alignedSMR.bones, (item) => { return item.name == bone1.name; });
            var bone2 = smr.bones[boneWeights[i].boneIndex2];
            boneWeights[i].boneIndex2 = Array.FindIndex(alignedSMR.bones, (item) => { return item.name == bone2.name; });
            var bone3 = smr.bones[boneWeights[i].boneIndex3];
            boneWeights[i].boneIndex3 = Array.FindIndex(alignedSMR.bones, (item) => { return item.name == bone3.name; });
        }
    }
    bool GenerateBakedMesh(SkinnedMeshRenderer smr, string path, SkinnedMeshRenderer alignedSMR)
    {
        var mesh = smr.sharedMesh;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Color[] colors = mesh.colors;
        Vector2[] uv = mesh.uv;

        var bakedMesh = new Mesh();
        bakedMesh.name = mesh.name;
        bakedMesh.vertices = mesh.vertices;
        bakedMesh.triangles = mesh.triangles;
        if (normals != null && normals.Length > 0)
        {
            bakedMesh.normals = normals;
        }
        if (tangents != null && tangents.Length > 0)
        {
            bakedMesh.tangents = tangents;
        }
        if (colors != null && colors.Length > 0)
        {
            bakedMesh.colors = colors;
        }
        if (uv != null && uv.Length > 0)
        {
            bakedMesh.uv = uv;
        }
        BoneWeight[] boneWeights = mesh.boneWeights;
        if (alignedSMR != null)
        {
            AlignBoneOrder(smr, alignedSMR, ref boneWeights);
        }
        List<Vector4> uv2 = new List<Vector4>(mesh.vertexCount);
        List<Vector4> uv3 = new List<Vector4>(mesh.vertexCount);
        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            uv2.Add(new Vector4(boneWeights[i].boneIndex0, boneWeights[i].weight0, boneWeights[i].boneIndex1, boneWeights[i].weight1));
            uv3.Add(new Vector4(boneWeights[i].boneIndex2, boneWeights[i].weight2, boneWeights[i].boneIndex3, boneWeights[i].weight3));
        }
        bakedMesh.SetUVs(1, uv2);
        bakedMesh.SetUVs(2, uv3);

        AssetDatabase.CreateAsset(bakedMesh, path);
        return true;
    }
    bool GenerateBakedAnimations(string path)
    {
        var sheet = CreateInstance<BakedAnimationSheet>();
        sheet.Clips = new BakedAnimationSheet.BakedClip[clipCount];
        for (int i = 0;i < sheet.Clips.Length; ++i)
        {
            sheet.Clips[i] = new BakedAnimationSheet.BakedClip();
        }
        sheet.FrameRate = frameRate;
        sheet.BoneCount = smr.bones.Length;
        var smrParent = smr.transform.parent;
        var smrPosition = smrParent.position;
        smrParent.position = Vector3.zero;
        var smrRotation = smrParent.rotation;
        smrParent.rotation = Quaternion.identity;
        var smrScale = smrParent.localScale;
        smrParent.localScale = Vector3.one;

        try
        {
            int matrixStartIndex = 0;
            for (int i = 0; i < sheet.Clips.Length; ++i)
            {
                var clip = clips[i];
                var bakedClip = sheet.Clips[i];
                bakedClip.Clip = clip;
                bakedClip.MatrixStartIndex = matrixStartIndex;
                int frameCount = (int)(clip.length * frameRate);
                bakedClip.MatrixCount = frameCount * smr.bones.Length;
                bakedClip.Matrices = new BakedAnimationSheet.Matrix[bakedClip.MatrixCount];
                for (int k = 0; k < frameCount; ++k)
                {
                    clip.SampleAnimation(smr.transform.parent.gameObject, k / (float)frameRate);
                    for (int j = 0;j < smr.bones.Length; ++j)
                    {
                        var matrix = smr.bones[j].localToWorldMatrix * smr.sharedMesh.bindposes[j];
                        int index = k * smr.bones.Length + j;
                        bakedClip.Matrices[index].m1 = new Color(matrix.m00, matrix.m01, matrix.m02, matrix.m03);
                        bakedClip.Matrices[index].m2 = new Color(matrix.m10, matrix.m11, matrix.m12, matrix.m13);
                        bakedClip.Matrices[index].m3 = new Color(matrix.m20, matrix.m21, matrix.m22, matrix.m23);
                    }
                }
                matrixStartIndex += bakedClip.MatrixCount;
            }
            int pixelCount = matrixStartIndex * 3;
            sheet.TextureWidth = Mathf.ClosestPowerOfTwo((int)Mathf.Sqrt(pixelCount));
            sheet.TextureHeight = sheet.TextureWidth;
            if (sheet.TextureWidth * sheet.TextureHeight < pixelCount)
            {
                sheet.TextureWidth *= 2;
            }
            if (sheet.TextureWidth * sheet.TextureHeight < pixelCount)
            {
                sheet.TextureHeight *= 2;
            }
            Texture2D texture = new Texture2D(sheet.TextureWidth, sheet.TextureHeight, TextureFormat.RGBAHalf, false, true);
            Color[] pixels = texture.GetPixels();
            int chipIndex = 0;
            for (int i = 0; i < sheet.Clips.Length; ++i)
            {
                var bakedClip = sheet.Clips[i];
                for (int k = 0; k < bakedClip.MatrixCount; ++k)
                {
                    pixels[chipIndex * 3] = bakedClip.Matrices[k].m1;
                    pixels[chipIndex * 3 + 1] = bakedClip.Matrices[k].m2;
                    pixels[chipIndex * 3 + 2] = bakedClip.Matrices[k].m3;
                    chipIndex++;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            string rawDataPath = Application.dataPath.Replace("Assets", "") + path.Replace(".asset", "") + ".bytes";
            using (FileStream stream = new FileStream(rawDataPath, FileMode.Create))
            {
                byte[] bytes = texture.GetRawTextureData();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
                stream.Close();
            }

            AssetDatabase.CreateAsset(sheet, path);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }
        catch (Exception e)
        {
            smrParent.position = smrPosition;
            smrParent.rotation = smrRotation;
            smrParent.localScale = smrScale;
            Debug.LogError(e);
            return false;
        }
        smrParent.position = smrPosition;
        smrParent.rotation = smrRotation;
        smrParent.localScale = smrScale;
        return true;
    }
}
