using System;
using System.IO;
using Halka.Game.Core;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version251Checks
    {
        private const string CharacterFolder = "Assets/Content/Character/";
        private const string WorldFolder = "Assets/Content/World/";

        [MenuItem("HALKA/Validate ver2.5.1 scene")]
        public static void Run()
        {
            Version25Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            Check(GameVersion.Value == "2.5.1", "game version");

            string[] characterSprites = {
                "front_idle/00.png", "front_idle/01.png",
                "back_idle/00.png", "left_idle/00.png", "right_idle/00.png",
                "walk_front/00.png", "walk_back/00.png", "walk_left/00.png",
                "walk_right/00.png", "bench_sit.png"
            };
            foreach (var name in characterSprites)
                CheckPixelSprite(CharacterFolder + name);
            string[] worldSprites = {
                "grass.png", "dirt.png", "stone.png", "flower.png", "tree.png",
                "house_exterior.png", "house_bed.png", "bench.png", "sign.png",
                "cushion.png", "desk.png", "well.png", "crow_idle_down.png"
            };
            foreach (var name in worldSprites)
                CheckPixelSprite(WorldFolder + name);
            Check(AssetDatabase.FindAssets("t:SpriteAtlas").Length == 0,
                "no atlas overrides pixel art import settings");

            var standing = LoadPng(CharacterFolder + "front_idle/00.png");
            var seated = LoadPng(CharacterFolder + "bench_sit.png");
            Check(standing.width == 64 && standing.height == 64 &&
                seated.width == 64 && seated.height == 64, "standing and seated dimensions");
            Check(CountFaceAccent(standing) >= 2 && CountFaceAccent(seated) >= 2,
                "red-pink eye accents retained in both poses");
            UnityEngine.Object.DestroyImmediate(standing);
            UnityEngine.Object.DestroyImmediate(seated);

            var camera = Camera.main;
            Check(camera != null && camera.orthographic &&
                Mathf.Approximately(camera.orthographicSize, 4.5f) &&
                camera.targetTexture == null, "original camera framing and direct display");
            Check(Mathf.Approximately(GridWorld2D.TileWorldSize, 32f / 64f) &&
                Mathf.Approximately(PlayerSeatController.ArtworkAboveSeatCenter, 10f / 64f) &&
                Mathf.Approximately(camera.orthographicSize * 2f * 64f, 576f),
                "cell, seat offset and 576-pixel outdoor projection");

            var template = File.ReadAllText("Assets/WebGLTemplates/MinimalNoBrand/index.html");
            Check(template.Contains("image-rendering: pixelated") &&
                template.Contains("new ResizeObserver(resize)") &&
                template.Contains("devicePixelRatio: pixelRatio()") &&
                template.Contains("bounds.width < 800 || cssHeight > bounds.width"),
                "responsive pixel canvas source template");
            Debug.Log("HALKA ver2.5.1 pixel art and inherited regression checks passed.");
        }

        private static void CheckPixelSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Check(importer != null && sprite != null &&
                importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                Mathf.Approximately(importer.spritePixelsPerUnit, 64f) &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                sprite.texture.filterMode == FilterMode.Point &&
                sprite.texture.mipmapCount == 1 &&
                !importer.GetPlatformTextureSettings("WebGL").overridden,
                "pixel sprite import: " + path);
        }

        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Check(texture.LoadImage(File.ReadAllBytes(path), false), "PNG loads: " + path);
            return texture;
        }

        private static int CountFaceAccent(Texture2D texture)
        {
            var count = 0;
            // Both approved poses place the eyes inside this broad upper-face region.
            for (var y = 24; y < 40; y++)
                for (var x = 16; x < 46; x++)
                {
                    var color = texture.GetPixel(x, y);
                    if (color.a > 0.99f && color.r > 0.7f &&
                        color.g < 0.25f && color.b > 0.2f && color.b < 0.7f)
                        count++;
                }
            return count;
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version251Checks failed: " + description);
        }
    }
}
