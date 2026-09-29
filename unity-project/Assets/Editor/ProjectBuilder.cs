using System;
using System.IO;
using Halka.Game.CameraControl;
using Halka.Game.Core;
using Halka.Game.Input;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halka.Game.Editor
{
    public static class ProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/FirstDay.unity";
        private const string CharacterPath = "Assets/Content/Character/front_idle";
        private const string PixelPath = "Assets/Content/World/pixel.png";
        private const string StonePath = "Assets/Content/World/stone.png";
        private static readonly Vector2 StoneOpaquePixels = new Vector2(28f, 20f);

        [MenuItem("HALKA/Prepare ver1.2 scene")]
        public static void PrepareScene()
        {
            ConfigureProject();
            var frames = LoadFrames("front_idle");
            if (frames.Length == 0) throw new InvalidOperationException("front_idle needs at least one PNG frame");
            ConfigureSpriteImport(PixelPath, 1f);
            var pixel = AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
            if (pixel == null) throw new InvalidOperationException("World pixel import failed");
            ConfigureSpriteImport(StonePath, GridWorld2D.TilePixels);
            var stoneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StonePath);
            if (stoneSprite == null) throw new InvalidOperationException("Stone sprite import failed");

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldObject = new GameObject("World - first day grid");
            var world = worldObject.AddComponent<GridWorld2D>();

            var player = new GameObject("Player - HarukaChan");
            player.transform.position = Vector3.zero;
            var artwork = new GameObject("Player artwork");
            artwork.transform.SetParent(player.transform, false);
            var playerRenderer = artwork.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = frames[0];
            playerRenderer.sortingOrder = 10;
            var mover = player.AddComponent<PlayerMover>();
            var visual = player.AddComponent<CharacterVisual>();
            var visualAnchor = player.AddComponent<PlayerVisualAnchor2D>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.9f, 0.93f, 0.82f);
            var follow = cameraObject.AddComponent<CameraFollow2D>();
            SetReference(follow, "target", player.transform);
            SetReference(follow, "cameraComponent", camera);

            var ground = new GameObject("FirstDay - small ground");
            var groundRenderer = ground.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = pixel;
            groundRenderer.color = new Color(0.85f, 0.91f, 0.72f);
            groundRenderer.sortingOrder = -10;
            ground.transform.localScale = new Vector3(12f, 8f, 1f);

            var stone = new GameObject("Stone - first world object");
            stone.transform.position = world.CellToWorld(new Vector2Int(1, 1));
            var stoneRenderer = stone.AddComponent<SpriteRenderer>();
            stoneRenderer.sprite = stoneSprite;
            stoneRenderer.sortingOrder = 2;
            var stoneCollider = stone.AddComponent<BoxCollider2D>();
            stoneCollider.size = StoneOpaquePixels / GridWorld2D.TilePixels;
            stone.AddComponent<GridObstacle>();
            var examine = stone.AddComponent<ExamineInteractable>();

            var hudObject = new GameObject("UI - minimal HUD");
            hudObject.AddComponent<GameHud>();
            var dpadObject = new GameObject("UI - touch D-pad");
            var dpad = dpadObject.AddComponent<TouchDpad>();
            var hud = hudObject.GetComponent<GameHud>();
            SetReference(hud, "dpad", dpad);
            SetReference(hud, "messageFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/Content/Fonts/HalkaMessageSubset.ttf"));
            var interactionObject = new GameObject("Interaction Router");
            var interaction = interactionObject.AddComponent<InteractionRouter>();
            SetReference(interaction, "worldCamera", camera);
            var inputObject = new GameObject("Input - keyboard pointer");
            var input = inputObject.AddComponent<GameInput>();
            SetReference(input, "interaction", interaction);
            SetReference(input, "dpad", dpad);
            SetReference(mover, "input", input);
            SetReference(mover, "world", world);
            SetReference(visualAnchor, "mover", mover);
            SetReference(visualAnchor, "world", world);
            SetReference(visualAnchor, "artwork", artwork.transform);
            SetReference(visual, "mover", mover);
            SetReference(visual, "spriteRenderer", playerRenderer);
            SetReference(examine, "player", mover);
            SetReference(examine, "world", world);
            SetReference(examine, "hud", hud);
            SetString(examine, "message", "いし。");
            SetSprites(visual, "frontIdle", frames);
            SetSprites(visual, "backIdle", LoadFrames("back_idle"));
            SetSprites(visual, "leftIdle", LoadFrames("left_idle"));
            SetSprites(visual, "rightIdle", LoadFrames("right_idle"));
            SetFloat(visual, "frameSeconds", CharacterVisual.OriginalIdleFrameSeconds);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("HALKA ver1.2 scene prepared.");
        }

        [MenuItem("HALKA/Build WebGL for HP")]
        public static void BuildWeb()
        {
            ConfigureProject();
            if (!File.Exists(ScenePath)) PrepareScene();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "halkaworld", "webgl"));
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            Debug.Log($"HALKA WebGL build succeeded: {output}");
        }

        [MenuItem("HALKA/Refresh character sprites")]
        public static void RefreshCharacterSprites()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var visual = UnityEngine.Object.FindFirstObjectByType<CharacterVisual>();
            if (visual == null) throw new InvalidOperationException("CharacterVisual not found in FirstDay scene");
            SetSprites(visual, "frontIdle", LoadFrames("front_idle"));
            SetSprites(visual, "backIdle", LoadFrames("back_idle"));
            SetSprites(visual, "leftIdle", LoadFrames("left_idle"));
            SetSprites(visual, "rightIdle", LoadFrames("right_idle"));
            SetFloat(visual, "frameSeconds", CharacterVisual.OriginalIdleFrameSeconds);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "HALKA";
            PlayerSettings.productName = "Game";
            PlayerSettings.bundleVersion = GameVersion.Value;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 600;
            PlayerSettings.WebGL.template = "PROJECT:MinimalNoBrand";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
        }

        private static void ConfigureSpriteImport(string path, float pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Missing texture: {path}");
            if (importer.textureType == TextureImporterType.Sprite &&
                Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.npotScale == TextureImporterNPOTScale.None) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static Sprite[] LoadFrames(string direction)
        {
            var folder = $"Assets/Content/Character/{direction}";
            if (!AssetDatabase.IsValidFolder(folder)) return Array.Empty<Sprite>();
            var paths = Directory.GetFiles(folder, "*.png");
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var sprites = new Sprite[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i].Replace('\\', '/');
                ConfigureSpriteImport(path, GridWorld2D.TilePixels);
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[i] == null) throw new InvalidOperationException($"Sprite import failed: {path}");
            }
            return sprites;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSprites(UnityEngine.Object target, string field, Sprite[] sprites)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(field);
            array.arraySize = sprites.Length;
            for (var i = 0; i < sprites.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
