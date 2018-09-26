using System;
using System.Collections;
using UnityEngine;
using UnityEditor;

public class MeshPainterBrushProcessor : AssetPostprocessor 
{
	public void OnPostprocessTexture(Texture tex)
	{
		var texture = assetImporter as TextureImporter;
		var rootAssetPath = MeshPainterEditor.GetRootAssetPath();
		if (rootAssetPath == null)
			return;

		if (texture.assetPath.StartsWith(rootAssetPath + "Editor/Brushes/") && !texture.isReadable)
		{
			texture.isReadable = true;
			texture.SaveAndReimport();
		}
	}
}
