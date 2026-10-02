using System;
using System.IO;
using System.Linq;
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
        private const string PixelPath = "Assets/Content/World/pixel.png";
        private const string StonePath = "Assets/Content/World/stone.png";
        private const string FlowerPath = "Assets/Content/World/flower.png";
        private const string TreePath = "Assets/Content/World/tree.png";
        private const string GrassPath = "Assets/Content/World/grass.png";
        private const string GrassRustlePath = "Assets/Content/World/grass_rustle";
        private const string FootstepPath = "Assets/Content/Audio/footstep_one_step.wav";
        private const string MessageFontPath = "Assets/Content/Fonts/k8x12L.ttf";
        private const string PlayerGrassMaskPath = "Assets/Content/World/player_grass_mask.png";
        private const string GrassPrefabPath = "Assets/Content/World/GrassDecoration.prefab";
        private const string GridPath = "Assets/Content/World/grid.png";
        private const float SpritePixelsPerUnit = 64f;
        internal const float ArtworkFootOffset = GridWorld2D.TileWorldSize / 2f;
        internal static readonly Vector2Int FlowerCell = new Vector2Int(-3, 1);
        internal static readonly Vector2Int TreeRootCell = new Vector2Int(5, 1);
        private static readonly Vector2 StoneOpaquePixels = new Vector2(28f, 20f);

        [MenuItem("HALKA/Prepare ver1.8 scene")]
        public static void PrepareScene()
        {
            ConfigureProject();
            var frames = LoadFrames("front_idle");
            if (frames.Length == 0) throw new InvalidOperationException("front_idle needs at least one PNG frame");
            ConfigureSpriteImport(PixelPath, 1f);
            var pixel = AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
            if (pixel == null) throw new InvalidOperationException("World pixel import failed");
            ConfigureSpriteImport(StonePath, SpritePixelsPerUnit);
            var stoneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StonePath);
            if (stoneSprite == null) throw new InvalidOperationException("Stone sprite import failed");
            ConfigureSpriteImport(FlowerPath, SpritePixelsPerUnit);
            var flowerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlowerPath);
            if (flowerSprite == null || flowerSprite.rect.size != new Vector2(32f, 32f))
                throw new InvalidOperationException("Flower must be an unchanged 32x32 sprite");
            ConfigureSpriteImport(TreePath, SpritePixelsPerUnit);
            var treeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TreePath);
            if (treeSprite == null || treeSprite.rect.size != new Vector2(96f, 128f))
                throw new InvalidOperationException("Tree must be an unchanged 96x128 sprite");
            ConfigureSpriteImport(GrassPath, SpritePixelsPerUnit);
            var grassSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrassPath);
            if (grassSprite == null) throw new InvalidOperationException("Grass sprite import failed");
            var rustleFrames = LoadFramesFromFolder(GrassRustlePath);
            if (rustleFrames.Length == 0) throw new InvalidOperationException("Grass rustle frames are missing");
            var maskSprite = CreatePlayerGrassMask();
            ConfigureFootstepImport();
            var footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepPath);
            if (footstepClip == null) throw new InvalidOperationException("Single-step audio import failed");
            var grassPrefab = CreateGrassPrefab(grassSprite);

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldObject = new GameObject("World - first day grid");
            var world = worldObject.AddComponent<GridWorld2D>();

            var player = new GameObject("Player - HarukaChan");
            player.transform.position = Vector3.zero;
            var artwork = new GameObject("Player artwork");
            artwork.transform.SetParent(player.transform, false);
            artwork.transform.localPosition = Vector3.up * ArtworkFootOffset;
            var playerRenderer = artwork.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = frames[0];
            playerRenderer.sortingOrder = 10;
            var maskObject = new GameObject("Player grass foot mask");
            maskObject.transform.SetParent(artwork.transform, false);
            var spriteMask = maskObject.AddComponent<SpriteMask>();
            spriteMask.sprite = maskSprite;
            var mover = player.AddComponent<PlayerMover>();
            var visual = player.AddComponent<CharacterVisual>();
            var audioSource = player.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.45f;
            var footstepAudio = player.AddComponent<PlayerFootstepAudio>();
            SetReference(footstepAudio, "mover", mover);
            SetReference(footstepAudio, "source", audioSource);
            SetReference(footstepAudio, "footstep", footstepClip);

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

            var gridSprite = CreateGridOverlay(world);
            var gridObject = new GameObject("FirstDay - 32px cell boundaries");
            var gridRenderer = gridObject.AddComponent<SpriteRenderer>();
            gridRenderer.sprite = gridSprite;
            gridRenderer.sortingOrder = -9;

            var stone = new GameObject("Stone - first world object");
            stone.transform.position = world.CellToWorld(new Vector2Int(1, 1));
            var stoneRenderer = stone.AddComponent<SpriteRenderer>();
            stoneRenderer.sprite = stoneSprite;
            stoneRenderer.sortingOrder = 2;
            var stoneCollider = stone.AddComponent<BoxCollider2D>();
            stoneCollider.size = StoneOpaquePixels / SpritePixelsPerUnit;
            stone.AddComponent<GridObstacle>();
            var examine = stone.AddComponent<ExamineInteractable>();

            var flower = new GameObject("Flower - first bloom");
            flower.transform.position = world.CellToWorld(FlowerCell);
            var flowerRenderer = flower.AddComponent<SpriteRenderer>();
            flowerRenderer.sprite = flowerSprite;
            flowerRenderer.sortingOrder = stoneRenderer.sortingOrder;
            var flowerCollider = flower.AddComponent<BoxCollider2D>();
            flowerCollider.size = Vector2.one * GridWorld2D.TileWorldSize;
            flower.AddComponent<GridObstacle>();
            var flowerExamine = flower.AddComponent<ExamineInteractable>();

            var tree = new GameObject("Tree - first tree");
            tree.transform.position = world.CellToWorld(TreeRootCell);
            var treeExamine = tree.AddComponent<ExamineInteractable>();
            var treeRoot = new GameObject("Tree root obstacle");
            treeRoot.transform.SetParent(tree.transform, false);
            var treeRootCollider = treeRoot.AddComponent<BoxCollider2D>();
            treeRootCollider.size = Vector2.one * GridWorld2D.TileWorldSize;
            treeRoot.AddComponent<GridObstacle>();
            var treeArtwork = new GameObject("Tree artwork");
            treeArtwork.transform.SetParent(tree.transform, false);
            treeArtwork.transform.localPosition = RootedSpriteLayout2D.OffsetFromBottomCenter(treeSprite);
            var treeRenderer = treeArtwork.AddComponent<SpriteRenderer>();
            treeRenderer.sprite = treeSprite;
            treeRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            var treeClickCollider = treeArtwork.AddComponent<BoxCollider2D>();
            treeClickCollider.size = treeSprite.bounds.size;
            var treeDepth = tree.AddComponent<RootedWorldObjectDepth2D>();
            SetReference(treeDepth, "world", world);
            SetReference(treeDepth, "player", mover);
            SetReference(treeDepth, "playerRenderer", playerRenderer);
            SetReference(treeDepth, "objectRenderer", treeRenderer);

            var grassGroup = new GameObject("Grass decorations");
            var grassCount = 0;
            for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
            for (var x = world.MinCell.x; x <= world.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!world.CanEnter(cell)) continue;
                var grass = (GameObject)PrefabUtility.InstantiatePrefab(grassPrefab);
                grass.transform.SetParent(grassGroup.transform);
                grass.transform.position = world.CellToWorld(cell);
                grassCount++;
            }
            var grassField = grassGroup.AddComponent<GrassField2D>();
            SetReference(grassField, "world", world);
            SetReference(grassField, "player", mover);
            SetReference(grassField, "idleSprite", grassSprite);
            SetSprites(grassField, "rustleFrames", rustleFrames);
            var grassOcclusion = player.AddComponent<PlayerGrassOcclusion>();
            SetReference(grassOcclusion, "mover", mover);
            SetReference(grassOcclusion, "grassField", grassField);
            SetReference(grassOcclusion, "playerRenderer", playerRenderer);
            SetReference(grassOcclusion, "spriteMask", spriteMask);

            var hudObject = new GameObject("UI - minimal HUD");
            hudObject.AddComponent<GameHud>();
            var dpadObject = new GameObject("UI - touch D-pad");
            var dpad = dpadObject.AddComponent<TouchDpad>();
            var actionObject = new GameObject("UI - touch action");
            var actionButton = actionObject.AddComponent<TouchActionButton>();
            SetReference(actionButton, "dpad", dpad);
            var hud = hudObject.GetComponent<GameHud>();
            SetReference(hud, "dpad", dpad);
            SetReference(hud, "actionButton", actionButton);
            var fontImporter = AssetImporter.GetAtPath(MessageFontPath) as TrueTypeFontImporter;
            if (fontImporter == null) throw new InvalidOperationException("k8x12L font import failed");
            if (fontImporter.fontRenderingMode != FontRenderingMode.HintedRaster ||
                fontImporter.fontSize != 12)
            {
                fontImporter.fontRenderingMode = FontRenderingMode.HintedRaster;
                fontImporter.fontSize = 12;
                fontImporter.SaveAndReimport();
            }
            var messageFont = AssetDatabase.LoadAssetAtPath<Font>(MessageFontPath);
            if (messageFont == null) throw new InvalidOperationException("k8x12L font is missing");
            SetReference(hud, "messageFont", messageFont);
            var interactionObject = new GameObject("Interaction Router");
            var interaction = interactionObject.AddComponent<InteractionRouter>();
            SetReference(interaction, "worldCamera", camera);
            SetReference(interaction, "player", mover);
            SetReference(interaction, "world", world);
            var inputObject = new GameObject("Input - keyboard pointer");
            var input = inputObject.AddComponent<GameInput>();
            SetReference(input, "interaction", interaction);
            SetReference(input, "dpad", dpad);
            SetReference(input, "actionButton", actionButton);
            SetReference(mover, "input", input);
            SetReference(mover, "world", world);
            SetReference(visual, "mover", mover);
            SetReference(visual, "spriteRenderer", playerRenderer);
            SetReference(examine, "player", mover);
            SetReference(examine, "world", world);
            SetReference(examine, "hud", hud);
            SetString(examine, "message", "いし。");
            SetReference(flowerExamine, "player", mover);
            SetReference(flowerExamine, "world", world);
            SetReference(flowerExamine, "hud", hud);
            SetString(flowerExamine, "message", "はな。");
            SetReference(treeExamine, "player", mover);
            SetReference(treeExamine, "world", world);
            SetReference(treeExamine, "hud", hud);
            SetString(treeExamine, "message", "き。");
            SetSprites(visual, "frontIdle", frames);
            SetSprites(visual, "backIdle", LoadFrames("back_idle"));
            SetSprites(visual, "leftIdle", LoadFrames("left_idle"));
            SetSprites(visual, "rightIdle", LoadFrames("right_idle"));
            SetSprites(visual, "frontWalk", LoadFrames("walk_front"));
            SetSprites(visual, "backWalk", LoadFrames("walk_back"));
            SetSprites(visual, "leftWalk", LoadFrames("walk_left"));
            SetSprites(visual, "rightWalk", LoadFrames("walk_right"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"HALKA ver1.8 scene prepared with {grassCount} grass cells.");
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
            SetSprites(visual, "frontWalk", LoadFrames("walk_front"));
            SetSprites(visual, "backWalk", LoadFrames("walk_back"));
            SetSprites(visual, "leftWalk", LoadFrames("walk_left"));
            SetSprites(visual, "rightWalk", LoadFrames("walk_right"));
            EditorSceneManager.SaveScene(scene);
        }

        private static Sprite CreateGridOverlay(GridWorld2D world)
        {
            var width = (world.MaxCell.x - world.MinCell.x + 1) * GridWorld2D.TilePixels;
            var height = (world.MaxCell.y - world.MinCell.y + 1) * GridWorld2D.TilePixels;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var line = new Color32(56, 83, 49, 30);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = x % GridWorld2D.TilePixels == 0 ||
                    y % GridWorld2D.TilePixels == 0 ? line : new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(GridPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(GridPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImport(GridPath, SpritePixelsPerUnit);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GridPath);
            if (sprite == null) throw new InvalidOperationException("Grid overlay import failed");
            return sprite;
        }

        private static Sprite CreatePlayerGrassMask()
        {
            const int spritePixels = 64;
            var hiddenPixels = PlayerGrassOcclusion.GrassPlayerOcclusionPixels;
            if (hiddenPixels <= 0 || hiddenPixels >= spritePixels)
                throw new InvalidOperationException("Player grass mask height is invalid");
            var texture = new Texture2D(spritePixels, spritePixels, TextureFormat.RGBA32, false);
            var pixels = new Color32[spritePixels * spritePixels];
            for (var y = 0; y < spritePixels; y++)
            for (var x = 0; x < spritePixels; x++)
                pixels[y * spritePixels + x] = y < hiddenPixels
                    ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (!File.Exists(PlayerGrassMaskPath) || !File.ReadAllBytes(PlayerGrassMaskPath).SequenceEqual(png))
            {
                File.WriteAllBytes(PlayerGrassMaskPath, png);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            ConfigureSpriteImport(PlayerGrassMaskPath, SpritePixelsPerUnit);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerGrassMaskPath);
            if (sprite == null) throw new InvalidOperationException("Player grass mask import failed");
            return sprite;
        }

        private static GameObject CreateGrassPrefab(Sprite sprite)
        {
            var root = new GameObject("GrassDecoration");
            try
            {
                var artwork = new GameObject("Grass artwork");
                artwork.transform.SetParent(root.transform, false);
                artwork.transform.localPosition = Vector3.zero;
                var renderer = artwork.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 0;
                return PrefabUtility.SaveAsPrefabAsset(root, GrassPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "HALKA";
            PlayerSettings.productName = "HALKA WORLD";
            PlayerSettings.bundleVersion = GameVersion.Value;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 600;
            PlayerSettings.WebGL.template = "PROJECT:MinimalNoBrand";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.stripEngineCode = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
        }

        private static void ConfigureSpriteImport(string path, float pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException($"Missing texture: {path}");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (importer.textureType == TextureImporterType.Sprite &&
                Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                settings.spriteAlignment == (int)SpriteAlignment.Center &&
                settings.spritePivot == new Vector2(0.5f, 0.5f)) return;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static void ConfigureFootstepImport()
        {
            var importer = AssetImporter.GetAtPath(FootstepPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Single-step audio is missing");
            var settings = importer.defaultSampleSettings;
            var importerObject = new SerializedObject(importer);
            var normalize = importerObject.FindProperty("m_Normalize");
            var normalized = normalize != null && normalize.boolValue;
            if (settings.compressionFormat == AudioCompressionFormat.PCM &&
                settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate &&
                !importer.forceToMono && !normalized) return;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            if (normalize == null) throw new InvalidOperationException("Audio normalization setting is missing");
            normalize.boolValue = false;
            importerObject.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }

        private static Sprite[] LoadFrames(string direction)
        {
            return LoadFramesFromFolder($"Assets/Content/Character/{direction}");
        }

        private static Sprite[] LoadFramesFromFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Array.Empty<Sprite>();
            var paths = Directory.GetFiles(folder, "*.png");
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            var sprites = new Sprite[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i].Replace('\\', '/');
                ConfigureSpriteImport(path, SpritePixelsPerUnit);
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

        private static void SetString(UnityEngine.Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
