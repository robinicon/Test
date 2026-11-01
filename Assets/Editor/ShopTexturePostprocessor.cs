#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

// Auto-configures the cartoon art pack on import: every PNG under
// Assets/Resources/ShopArt reads its sibling "<name>.png.json" (which carries the
// 9-slice border from the design tool) and applies it as the sprite border.
public class ShopTexturePostprocessor : AssetPostprocessor
{
    [System.Serializable]
    class BorderData { public float left, bottom, right, top; }
    [System.Serializable]
    class SliceData { public bool isNineSlice; public BorderData border; }

    void OnPreprocessTexture()
    {
        if (assetPath.Contains("/ShopArt/") == false) return;
        string jsonPath = assetPath + ".json";
        if (File.Exists(jsonPath) == false) return;

        var slice = JsonUtility.FromJson<SliceData>(File.ReadAllText(jsonPath));
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

        if (slice != null && slice.isNineSlice && slice.border != null)
        {
            importer.spriteBorder = new Vector4(
                slice.border.left, slice.border.bottom, slice.border.right, slice.border.top);
        }
    }
}
#endif
