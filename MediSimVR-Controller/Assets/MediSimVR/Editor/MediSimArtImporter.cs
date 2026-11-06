using UnityEditor;
using UnityEngine;

namespace MediSimVR.EditorTools
{
    // Imports every PNG under Assets/MediSimVR/Art as a UI Sprite (alpha, no mipmaps).
    // Shapes/rounded_* get a 64 px 9-slice border (corner radius is then set per Image).
    // App icon PNGs stay normal textures (Player Settings icons need Default type).
    public class MediSimArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/MediSimVR/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            if (assetPath.Contains("/Branding/app_icon"))
            {
                ti.textureType = TextureImporterType.Default;
                return;
            }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = assetPath.Contains("/Icons/") ? 128 : 1024;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 100f;
            if (assetPath.Contains("/Shapes/rounded")) ti.spriteBorder = new Vector4(64f, 64f, 64f, 64f);
            if (assetPath.Contains("/Shapes/shadow")) ti.spriteBorder = new Vector4(100f, 100f, 100f, 100f);
        }
    }
}
