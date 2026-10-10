using System;
using System.IO;
using Halka.Game.Core;
using Halka.Game.Player;
using Halka.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version252Checks
    {
        private const string AtlasPath = "Assets/Resources/asobi_ui_atlas.png";

        [MenuItem("HALKA/Validate ver2.5.2 scene")]
        public static void Run()
        {
            Version251Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            Check(GameVersion.Value == "2.5.2" || GameVersion.Value == "2.5.3" ||
                GameVersion.Value == "2.5.4", "game version");

            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            Check(importer != null && atlas != null &&
                importer.textureType == TextureImporterType.Default &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                !importer.GetPlatformTextureSettings("WebGL").overridden &&
                atlas.filterMode == FilterMode.Point && atlas.mipmapCount == 1,
                "bitmap font Point sampling without mipmaps or compression");
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Check(source.LoadImage(File.ReadAllBytes(AtlasPath), false) &&
                    source.width == 2048 && source.height == 640,
                    "bitmap atlas has its expected grid dimensions");
                var pixels = source.GetPixels32();
                var ink = 0;
                foreach (var pixel in pixels)
                {
                    Check(pixel.a == 0 || pixel.a == 255,
                        "glyph alpha contains no intermediate gray fringe");
                    if (pixel.a == 255) ink++;
                }
                Check(ink > 10000, "bitmap atlas retains legible ink");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }

            Check(GameBitmapFont.HasAllGlyphs(
                    "メニュー AUTO ON OFF せいかつきろく あるいたかず プレイじかん もどる " +
                    "ここは HALKA WORLD。 ベッド。 つくえ。 0123456789") &&
                !GameBitmapFont.HasAllGlyphs("𠮷"),
                "game UI glyphs and missing-glyph fallback route");
            Debug.Log("HALKA ver2.5.2 bitmap font and inherited regression checks passed.");
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version252Checks failed: " + description);
        }
    }
}
