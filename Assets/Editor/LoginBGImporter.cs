using UnityEditor;
using UnityEngine;

// 自动将 LoginBG 贴图类型设为 Sprite，供登录场景背景使用
public class LoginBGImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("Textures/LoginBG")) return;
        var ti = (TextureImporter)assetImporter;
        if (ti.textureType == TextureImporterType.Sprite) return;
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.mipmapEnabled       = false;
        ti.filterMode          = FilterMode.Bilinear;
        ti.maxTextureSize       = 2048;
        Debug.Log("[LoginBGImporter] LoginBG 已自动设为 Sprite 类型");
    }
}
