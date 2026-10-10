using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version254Checks
    {
        private const string CharacterFolder = "Assets/Content/Character/";

        [MenuItem("HALKA/Validate ver2.5.4 scene")]
        public static void Run()
        {
            Version253Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            Check(GameVersion.Value == "2.5.4", "game version");

            var folders = new[] { "front_idle", "back_idle", "left_idle", "right_idle",
                "walk_front", "walk_back", "walk_left", "walk_right" };
            var paths = folders.SelectMany(folder => Directory.GetFiles(CharacterFolder + folder, "*.png"))
                .Concat(new[] { CharacterFolder + "bench_sit.png" }).ToArray();
            Check(paths.Length == 25, "all authored Player frames are audited");
            foreach (var path in paths) CheckSpriteImport(path.Replace('\\', '/'));
            Check(AssetDatabase.FindAssets("t:SpriteAtlas").Length == 0,
                "Player sprites have no atlas packing or atlas overrides");

            var player = GameObject.Find("Player - HarukaChan");
            var artwork = player?.transform.Find("Player artwork");
            var renderer = artwork?.GetComponent<SpriteRenderer>();
            var visual = player?.GetComponent<CharacterVisual>();
            var mover = player?.GetComponent<PlayerMover>();
            var seat = player?.GetComponent<PlayerSeatController>();
            var benchSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CharacterFolder + "bench_sit.png");
            Check(renderer != null && visual != null && mover != null && seat != null &&
                  benchSprite != null, "Player has one renderer and the authored bench sprite");
            var serializedVisual = new SerializedObject(visual);
            Check(serializedVisual.FindProperty("benchSit").objectReferenceValue == benchSprite &&
                  serializedVisual.FindProperty("spriteRenderer").objectReferenceValue == renderer &&
                  renderer.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(CharacterFolder + "front_idle/00.png"),
                  "seating and standing use the same renderer and exact source assets");
            Check(artwork.localScale == Vector3.one && player.transform.localScale == Vector3.one &&
                  artwork.localRotation == Quaternion.identity &&
                  player.transform.localRotation == Quaternion.identity &&
                  renderer.color == Color.white && !renderer.flipX && !renderer.flipY &&
                  renderer.drawMode == SpriteDrawMode.Simple,
                  "Player artwork has no tint, flip, rotation, or unequal scale");
            Check(artwork.Find("Player grass foot mask")?.GetComponent<SpriteMask>() != null &&
                  renderer.maskInteraction == SpriteMaskInteraction.None,
                  "the original grass mask hierarchy does not cover the initial face");
            Check(typeof(CharacterVisual).GetProperty("HeadAlignmentWorldY") == null,
                  "authored animation bob remains unchanged");

            CheckSeatSpriteSwap(player, artwork, renderer, visual, mover, seat, benchSprite);
            var template = File.ReadAllText("Assets/WebGLTemplates/MinimalNoBrand/index.html");
            Check(template.Contains("function layoutCanvas()") &&
                  template.Contains("const canvasHeight = Math.floor(height * scale)") &&
                  template.Contains("window.addEventListener(\"resize\", resize)") &&
                  template.Contains("image-rendering: pixelated"),
                  "responsive one-to-one desktop canvas and existing Point display");
            Debug.Log("HALKA ver2.5.4 Player source sprite fidelity and inherited regression checks passed.");
        }

        private static void CheckSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Check(importer != null && sprite != null, "Player asset exists: " + path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Check(importer.textureType == TextureImporterType.Sprite &&
                  importer.spriteImportMode == SpriteImportMode.Single &&
                  Mathf.Approximately(importer.spritePixelsPerUnit, 64f) &&
                  settings.spriteMeshType == SpriteMeshType.FullRect &&
                  importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                  importer.textureCompression == TextureImporterCompression.Uncompressed &&
                  !importer.GetPlatformTextureSettings("WebGL").overridden &&
                  importer.maxTextureSize >= 64 && importer.sRGBTexture &&
                  importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp &&
                  sprite.rect.size == new Vector2(64f, 64f) &&
                  sprite.textureRect.size == new Vector2(64f, 64f) &&
                  sprite.texture.filterMode == FilterMode.Point &&
                  sprite.texture.mipmapCount == 1,
                  "unmodified Full Rect pixel import: " + path);
        }

        private static void CheckSeatSpriteSwap(GameObject player, Transform artwork,
            SpriteRenderer renderer, CharacterVisual visual, PlayerMover mover,
            PlayerSeatController seat, Sprite benchSprite)
        {
            var originalPosition = artwork.localPosition;
            var originalSprite = renderer.sprite;
            var originalFacing = mover.Facing;
            var originalMaterial = renderer.sharedMaterial;
            var update = typeof(CharacterVisual).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(update != null, "original CharacterVisual frame selector");
            var probes = new[] { new GameObject("seat_left fidelity probe"),
                new GameObject("seat_right fidelity probe") };
            try
            {
                mover.SetFacing(FacingDirection.Up);
                for (var i = 0; i < probes.Length; i++)
                {
                    probes[i].transform.position = new Vector3(i * 0.5f, 0.5f, 0f);
                    Check(seat.TrySit(probes[i].transform, PlayerSeatController.BenchPoseKey) &&
                          seat.IsSeated, "bench seat " + i + " activates");
                    update.Invoke(visual, null);
                    Check(renderer.sprite == benchSprite && renderer.sharedMaterial == originalMaterial &&
                          renderer.color == Color.white && artwork.localScale == Vector3.one &&
                          artwork.localRotation == Quaternion.identity &&
                          renderer.maskInteraction == SpriteMaskInteraction.None,
                          "bench seat " + i + " displays the authored sprite unchanged");
                    Check(seat.Stand() && !seat.IsSeated && !mover.MovementLocked &&
                          artwork.localPosition == originalPosition,
                          "bench seat " + i + " restores the original artwork position");
                    update.Invoke(visual, null);
                    Check(renderer.sprite != benchSprite, "standing sprite returns after seat " + i);
                }
            }
            finally
            {
                seat.Stand();
                mover.SetFacing(originalFacing);
                artwork.localPosition = originalPosition;
                renderer.sprite = originalSprite;
                foreach (var probe in probes) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version254Checks failed: " + description);
        }
    }
}
