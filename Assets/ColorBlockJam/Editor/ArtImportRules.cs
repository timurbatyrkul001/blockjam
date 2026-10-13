using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.EditorTools
{
    // Assets/Art altına giren her PNG otomatik olarak Sprite olur; butonlar ve panel 9-slice
    // kenarlarıyla gelir (köşeler sabit, orta kısım uzar). Elle Inspector ayarı gerekmez.
    class ArtImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/") || assetPath.Contains("/Food/")) return;   // 3D model dokuları sprite olmaz

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            // sol, alt, sağ, üst (piksel). Kenney "Double" boyutlu görseller.
            if (assetPath.EndsWith("/button_green.png")) importer.spriteBorder = new Vector4(32, 40, 32, 32);
            else if (assetPath.EndsWith("/panel.png")) importer.spriteBorder = new Vector4(40, 48, 40, 40);
        }
    }
}
