using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string DirtPath = "Assets/Content/World/dirt.png";
        private const string HouseExteriorPath = "Assets/Content/World/house_exterior.png";
        private const string HouseFloorPath = "Assets/Content/World/house_floor.png";
        private const string HouseWallPath = "Assets/Content/World/house_wall.png";
        private const string HouseBedPath = "Assets/Content/World/house_bed.png";
        private const string CrowMaskPath = "Assets/Content/World/crow_grass_mask.png";
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
        internal static readonly Vector2Int[] FlowerCells =
            { FlowerCell, new Vector2Int(-5, 2), new Vector2Int(2, 3), new Vector2Int(6, -1) };
        internal static readonly Vector2Int[] StoneCells =
            { new Vector2Int(1, 1), new Vector2Int(-2, 4),
                new Vector2Int(3, -4), new Vector2Int(9, 3) };
        internal static readonly Vector2Int[] TreeCells =
            { TreeRootCell, new Vector2Int(-7, 2), new Vector2Int(8, -3) };
        internal static readonly Vector2Int[] RoadCells =
        {
            new Vector2Int(-8, -5), new Vector2Int(-7, -5), new Vector2Int(-6, -5),
            new Vector2Int(-5, -5), new Vector2Int(-4, -5), new Vector2Int(-3, -5),
            new Vector2Int(-3, -4), new Vector2Int(-2, -4), new Vector2Int(-2, -3),
            new Vector2Int(-1, -3), new Vector2Int(-1, -2), new Vector2Int(0, -2),
            new Vector2Int(0, -1)
        };
        internal static readonly Vector2Int[] DirtCells =
            (from y in Enumerable.Range(-4, 4)
                from x in Enumerable.Range(-9, 5)
                select new Vector2Int(x, y))
            .Concat(RoadCells).Distinct().ToArray();
        private static readonly Vector2 StoneOpaquePixels = new Vector2(28f, 20f);

        [MenuItem("HALKA/Prepare ver2.0 scene")]
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
            ConfigureSpriteImport(DirtPath, SpritePixelsPerUnit);
            var dirtSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DirtPath);
            if (dirtSprite == null || dirtSprite.rect.size != new Vector2(32f, 32f))
                throw new InvalidOperationException("Dirt must be an unchanged 32x32 sprite");
            var houseSprite = LoadWorldSprite(HouseExteriorPath, new Vector2(160f, 128f));
            var floorSprite = LoadWorldSprite(HouseFloorPath, new Vector2(32f, 32f));
            var wallSprite = LoadWorldSprite(HouseWallPath, new Vector2(32f, 32f));
            var bedSprite = LoadWorldSprite(HouseBedPath, new Vector2(64f, 96f));
            var crowIdleDown = LoadWorldSprite(CrowSpritePath("idle_down"), new Vector2(32f, 32f));
            var crowIdleUp = LoadWorldSprite(CrowSpritePath("idle_up"), new Vector2(32f, 32f));
            var crowIdleLeft = LoadWorldSprite(CrowSpritePath("idle_left"), new Vector2(32f, 32f));
            var crowIdleRight = LoadWorldSprite(CrowSpritePath("idle_right"), new Vector2(32f, 32f));
            var crowWalkDown = LoadCrowWalk("down");
            var crowWalkUp = LoadCrowWalk("up");
            var crowWalkLeft = LoadCrowWalk("left");
            var crowWalkRight = LoadCrowWalk("right");
            ConfigureSpriteImport(GrassPath, SpritePixelsPerUnit);
            var grassSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrassPath);
            if (grassSprite == null) throw new InvalidOperationException("Grass sprite import failed");
            var rustleFrames = LoadFramesFromFolder(GrassRustlePath);
            if (rustleFrames.Length == 0) throw new InvalidOperationException("Grass rustle frames are missing");
            var maskSprite = CreatePlayerGrassMask();
            var crowMaskSprite = CreateCrowGrassMask();
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

            var surfaceGroup = new GameObject("Ground surface overrides");
            var surfaceField = surfaceGroup.AddComponent<GroundSurfaceField2D>();
            foreach (var cell in DirtCells)
            {
                surfaceField.AddSurface(cell, dirtSprite);
                var tile = new GameObject($"Ground tile {cell.x},{cell.y}");
                tile.transform.SetParent(surfaceGroup.transform, false);
                tile.transform.position = world.CellToWorld(cell);
                var tileRenderer = tile.AddComponent<SpriteRenderer>();
                tileRenderer.sprite = dirtSprite;
                tileRenderer.sortingOrder = -9;
            }

            var gridSprite = CreateGridOverlay(world);
            var gridObject = new GameObject("FirstDay - 32px cell boundaries");
            var gridRenderer = gridObject.AddComponent<SpriteRenderer>();
            gridRenderer.sprite = gridSprite;
            gridRenderer.sortingOrder = -8;

            var addedObjects = new List<GameObject>();
            var addedExamines = new List<(ExamineInteractable Examine, string Message)>();
            var stone = new GameObject("Stone - first world object");
            stone.transform.position = world.CellToWorld(new Vector2Int(1, 1));
            var stoneRenderer = stone.AddComponent<SpriteRenderer>();
            stoneRenderer.sprite = stoneSprite;
            stoneRenderer.sortingOrder = 2;
            var stoneCollider = stone.AddComponent<BoxCollider2D>();
            stoneCollider.size = StoneOpaquePixels / SpritePixelsPerUnit;
            stone.AddComponent<GridObstacle>();
            var examine = stone.AddComponent<ExamineInteractable>();
            for (var i = 1; i < StoneCells.Length; i++)
            {
                var extra = CreateSmallObject(world, StoneCells[i], stoneSprite,
                    StoneOpaquePixels / SpritePixelsPerUnit, 2, $"Stone {i + 1}");
                addedObjects.Add(extra);
                addedExamines.Add((extra.GetComponent<ExamineInteractable>(), "いし。"));
            }

            var flower = new GameObject("Flower - first bloom");
            flower.transform.position = world.CellToWorld(FlowerCell);
            var flowerRenderer = flower.AddComponent<SpriteRenderer>();
            flowerRenderer.sprite = flowerSprite;
            flowerRenderer.sortingOrder = stoneRenderer.sortingOrder;
            var flowerCollider = flower.AddComponent<BoxCollider2D>();
            flowerCollider.size = Vector2.one * GridWorld2D.TileWorldSize;
            flower.AddComponent<GridObstacle>();
            var flowerExamine = flower.AddComponent<ExamineInteractable>();
            for (var i = 1; i < FlowerCells.Length; i++)
            {
                var extra = CreateSmallObject(world, FlowerCells[i], flowerSprite,
                    Vector2.one * GridWorld2D.TileWorldSize, 2, $"Flower {i + 1}");
                addedObjects.Add(extra);
                addedExamines.Add((extra.GetComponent<ExamineInteractable>(), "はな。"));
            }

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
            for (var i = 1; i < TreeCells.Length; i++)
            {
                var extra = CreateTreeObject(world, TreeCells[i], treeSprite,
                    mover, playerRenderer, $"Tree {i + 1}");
                addedObjects.Add(extra);
                addedExamines.Add((extra.GetComponent<ExamineInteractable>(), "き。"));
            }

            var house = new GameObject("House - HarukaChan home");
            house.transform.position = world.CellToWorld(HouseArea2D.HouseDoorCell);
            for (var y = -4; y <= -3; y++)
            for (var x = -9; x <= -5; x++)
            {
                var cell = new Vector2Int(x, y);
                if (cell == HouseArea2D.HouseDoorCell) continue;
                var footprint = new GameObject($"House footprint {x},{y}");
                footprint.transform.SetParent(house.transform, false);
                footprint.transform.position = world.CellToWorld(cell);
                footprint.AddComponent<BoxCollider2D>().size =
                    Vector2.one * GridWorld2D.TileWorldSize;
                footprint.AddComponent<GridObstacle>();
            }
            var houseArtwork = new GameObject("House exterior artwork");
            houseArtwork.transform.SetParent(house.transform, false);
            houseArtwork.transform.localPosition = RootedSpriteLayout2D.OffsetFromBottomCenter(houseSprite);
            var houseRenderer = houseArtwork.AddComponent<SpriteRenderer>();
            houseRenderer.sprite = houseSprite;
            houseRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            var houseDepth = house.AddComponent<RootedWorldObjectDepth2D>();
            SetReference(houseDepth, "world", world);
            SetReference(houseDepth, "player", mover);
            SetReference(houseDepth, "playerRenderer", playerRenderer);
            SetReference(houseDepth, "objectRenderer", houseRenderer);

            var occupancyObject = new GameObject("Dynamic grid occupancy");
            var occupancy = occupancyObject.AddComponent<DynamicGridOccupancy2D>();
            SetReference(occupancy, "player", mover);
            SetReference(mover, "occupancy", occupancy);
            var crow = new GameObject("Crow - first neighbor");
            crow.transform.position = world.CellToWorld(CrowWander2D.InitialCell);
            var crowRenderer = crow.AddComponent<SpriteRenderer>();
            crowRenderer.sprite = crowIdleRight;
            crowRenderer.sortingOrder = 3;
            var crowCollider = crow.AddComponent<BoxCollider2D>();
            crowCollider.size = Vector2.one * GridWorld2D.TileWorldSize;
            var crowExamine = crow.AddComponent<ExamineInteractable>();
            var crowWander = crow.AddComponent<CrowWander2D>();
            SetReference(crowWander, "world", world);
            SetReference(crowWander, "occupancy", occupancy);
            SetReference(crowWander, "artwork", crowRenderer);
            SetReference(crowWander, "clickCollider", crowCollider);
            SetReference(crowWander, "idleDown", crowIdleDown);
            SetReference(crowWander, "idleUp", crowIdleUp);
            SetReference(crowWander, "idleLeft", crowIdleLeft);
            SetReference(crowWander, "idleRight", crowIdleRight);
            SetSprites(crowWander, "walkDown", crowWalkDown);
            SetSprites(crowWander, "walkUp", crowWalkUp);
            SetSprites(crowWander, "walkLeft", crowWalkLeft);
            SetSprites(crowWander, "walkRight", crowWalkRight);
            SetReference(occupancy, "crow", crowWander);
            var crowMaskObject = new GameObject("Crow grass foot mask");
            crowMaskObject.transform.SetParent(crow.transform, false);
            var crowSpriteMask = crowMaskObject.AddComponent<SpriteMask>();
            crowSpriteMask.sprite = crowMaskSprite;

            var grassGroup = new GameObject("Grass decorations");
            var grassCount = 0;
            for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
            for (var x = world.MinCell.x; x <= world.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!world.CanEnter(cell) || surfaceField.HasGroundOverride(cell)) continue;
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
            var crowOcclusion = crow.AddComponent<CrowGrassOcclusion>();
            SetReference(crowOcclusion, "crow", crowWander);
            SetReference(crowOcclusion, "grassField", grassField);
            SetReference(crowOcclusion, "artwork", crowRenderer);
            SetReference(crowOcclusion, "spriteMask", crowSpriteMask);

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
            SetReference(crowExamine, "player", mover);
            SetReference(crowExamine, "world", world);
            SetReference(crowExamine, "hud", hud);
            SetString(crowExamine, "message", "カァ。");
            foreach (var pair in addedExamines)
            {
                SetReference(pair.Examine, "player", mover);
                SetReference(pair.Examine, "world", world);
                SetReference(pair.Examine, "hud", hud);
                SetString(pair.Examine, "message", pair.Message);
            }
            SetSprites(visual, "frontIdle", frames);
            SetSprites(visual, "backIdle", LoadFrames("back_idle"));
            SetSprites(visual, "leftIdle", LoadFrames("left_idle"));
            SetSprites(visual, "rightIdle", LoadFrames("right_idle"));
            SetSprites(visual, "frontWalk", LoadFrames("walk_front"));
            SetSprites(visual, "backWalk", LoadFrames("walk_back"));
            SetSprites(visual, "leftWalk", LoadFrames("walk_left"));
            SetSprites(visual, "rightWalk", LoadFrames("walk_right"));

            var exteriorRoot = new GameObject("Exterior - first field");
            foreach (var objectInField in new[] { ground, surfaceGroup, gridObject,
                stone, flower, tree, house, crow, grassGroup })
                objectInField.transform.SetParent(exteriorRoot.transform, true);
            foreach (var extra in addedObjects)
                extra.transform.SetParent(exteriorRoot.transform, true);
            var interiorRoot = CreateHouseInterior(world, pixel, floorSprite, wallSprite,
                bedSprite);
            interiorRoot.SetActive(false);

            var areaObject = new GameObject("House area switch");
            var houseArea = areaObject.AddComponent<HouseArea2D>();
            SetReference(houseArea, "world", world);
            SetReference(houseArea, "player", mover);
            SetReference(houseArea, "grassOcclusion", grassOcclusion);
            SetReference(houseArea, "exteriorRoot", exteriorRoot);
            SetReference(houseArea, "interiorRoot", interiorRoot);
            SetReference(houseArea, "worldCamera", camera);
            SetReference(houseArea, "cameraFollow", follow);

            var autoObject = new GameObject("UI - auto living toggle");
            var autoMode = autoObject.AddComponent<AutoModeController>();
            SetReference(autoMode, "input", input);
            SetReference(autoMode, "player", mover);
            SetReference(autoMode, "world", world);
            SetReference(autoMode, "house", houseArea);
            SetReference(autoMode, "occupancy", occupancy);
            SetReference(input, "autoMode", autoMode);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"HALKA ver2.0 scene prepared with {surfaceField.Count} ground overrides and {grassCount} grass cells.");
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
            var wasmFiles = Directory.GetFiles(Path.Combine(output, "Build"), "*.wasm");
            var dataFiles = Directory.GetFiles(Path.Combine(output, "Build"), "*.data");
            if (wasmFiles.Length != 1 || dataFiles.Length != 1)
                throw new InvalidOperationException("Expected one WebGL wasm and data file for cache refresh");
            var buildId = Path.GetFileNameWithoutExtension(wasmFiles[0]) + "-" +
                Path.GetFileNameWithoutExtension(dataFiles[0]);
            var gamePagePath = Path.GetFullPath(Path.Combine(output, "..", "index.html"));
            var page = File.ReadAllText(gamePagePath);
            const string framePattern = "(<iframe\\s+src=\"webgl/)(?:\\?[^\"]*)?(\")";
            if (!Regex.IsMatch(page, framePattern))
                throw new InvalidOperationException("HALKA WORLD iframe was not found for cache refresh");
            File.WriteAllText(gamePagePath, Regex.Replace(page, framePattern,
                "$1?v=" + GameVersion.Value + "&build=" + buildId + "$2"));
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

        private static Sprite CreateCrowGrassMask()
        {
            const int side = 32;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            for (var x = 0; x < side; x++)
                pixels[y * side + x] = y < CrowGrassOcclusion.HiddenPixels
                    ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (!File.Exists(CrowMaskPath) || !File.ReadAllBytes(CrowMaskPath).SequenceEqual(png))
            {
                File.WriteAllBytes(CrowMaskPath, png);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            ConfigureSpriteImport(CrowMaskPath, SpritePixelsPerUnit);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CrowMaskPath);
            if (sprite == null) throw new InvalidOperationException("Crow grass mask import failed");
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

        private static GameObject CreateSmallObject(GridWorld2D world, Vector2Int cell,
            Sprite sprite, Vector2 colliderSize, int sortingOrder, string name)
        {
            var result = new GameObject(name);
            result.transform.position = world.CellToWorld(cell);
            var renderer = result.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            result.AddComponent<BoxCollider2D>().size = colliderSize;
            result.AddComponent<GridObstacle>();
            result.AddComponent<ExamineInteractable>();
            return result;
        }

        private static GameObject CreateTreeObject(GridWorld2D world, Vector2Int cell,
            Sprite sprite, PlayerMover player, SpriteRenderer playerRenderer, string name)
        {
            var root = new GameObject(name);
            root.transform.position = world.CellToWorld(cell);
            root.AddComponent<ExamineInteractable>();
            var obstacle = new GameObject("Tree root obstacle");
            obstacle.transform.SetParent(root.transform, false);
            obstacle.AddComponent<BoxCollider2D>().size =
                Vector2.one * GridWorld2D.TileWorldSize;
            obstacle.AddComponent<GridObstacle>();
            var artwork = new GameObject("Tree artwork");
            artwork.transform.SetParent(root.transform, false);
            artwork.transform.localPosition = RootedSpriteLayout2D.OffsetFromBottomCenter(sprite);
            var renderer = artwork.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = playerRenderer.sortingOrder - 1;
            artwork.AddComponent<BoxCollider2D>().size = sprite.bounds.size;
            var depth = root.AddComponent<RootedWorldObjectDepth2D>();
            SetReference(depth, "world", world);
            SetReference(depth, "player", player);
            SetReference(depth, "playerRenderer", playerRenderer);
            SetReference(depth, "objectRenderer", renderer);
            return root;
        }

        private static GameObject CreateHouseInterior(GridWorld2D world, Sprite pixel,
            Sprite floor, Sprite wall, Sprite bedSprite)
        {
            var root = new GameObject("House interior - one room");
            var backdrop = new GameObject("Interior dark backdrop");
            backdrop.transform.SetParent(root.transform, false);
            backdrop.transform.localScale = new Vector3(20f, 14f, 1f);
            var backdropRenderer = backdrop.AddComponent<SpriteRenderer>();
            backdropRenderer.sprite = pixel;
            backdropRenderer.color = new Color(0.063f, 0.063f, 0.078f);
            backdropRenderer.sortingOrder = -20;
            for (var y = HouseArea2D.InsideMinCell.y; y <= HouseArea2D.InsideMaxCell.y; y++)
            for (var x = HouseArea2D.InsideMinCell.x; x <= HouseArea2D.InsideMaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = new GameObject($"Room floor {x},{y}");
                tile.transform.SetParent(root.transform, false);
                tile.transform.position = world.CellToWorld(cell);
                var floorRenderer = tile.AddComponent<SpriteRenderer>();
                floorRenderer.sprite = floor;
                floorRenderer.sortingOrder = -9;
                var boundary = x == HouseArea2D.InsideMinCell.x ||
                    x == HouseArea2D.InsideMaxCell.x || y == HouseArea2D.InsideMinCell.y ||
                    y >= 3;
                if (cell == HouseArea2D.InsideExitCell) continue;
                if (!boundary) continue;
                var wallTile = new GameObject($"Room wall {x},{y}");
                wallTile.transform.SetParent(root.transform, false);
                wallTile.transform.position = world.CellToWorld(cell);
                var wallRenderer = wallTile.AddComponent<SpriteRenderer>();
                wallRenderer.sprite = wall;
                wallRenderer.sortingOrder = -7;
                wallTile.AddComponent<BoxCollider2D>().size =
                    Vector2.one * GridWorld2D.TileWorldSize;
                wallTile.AddComponent<GridObstacle>();
            }

            var passage = new GameObject("House exit passage");
            passage.transform.SetParent(root.transform, false);
            passage.transform.position = world.CellToWorld(HouseArea2D.InsideExitCell);

            var bed = new GameObject("Bed - simple one");
            bed.transform.SetParent(root.transform, false);
            bed.transform.position = world.CellToWorld(new Vector2(-4.5f, 1f));
            var bedRenderer = bed.AddComponent<SpriteRenderer>();
            bedRenderer.sprite = bedSprite;
            bedRenderer.sortingOrder = 3;
            for (var y = 0; y <= 2; y++)
            for (var x = -5; x <= -4; x++)
            {
                var cell = new Vector2Int(x, y);
                var obstacle = new GameObject($"Bed footprint {x},{y}");
                obstacle.transform.SetParent(bed.transform, false);
                obstacle.transform.position = world.CellToWorld(cell);
                obstacle.AddComponent<BoxCollider2D>().size =
                    Vector2.one * GridWorld2D.TileWorldSize;
                obstacle.AddComponent<GridObstacle>();
            }
            return root;
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

        private static Sprite LoadWorldSprite(string path, Vector2 expectedSize)
        {
            ConfigureSpriteImport(path, SpritePixelsPerUnit);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.rect.size != expectedSize)
                throw new InvalidOperationException($"World sprite must be {expectedSize}: {path}");
            return sprite;
        }

        private static string CrowSpritePath(string suffix) =>
            $"Assets/Content/World/crow_{suffix}.png";

        private static Sprite[] LoadCrowWalk(string direction) => new[]
        {
            LoadWorldSprite(CrowSpritePath($"walk_{direction}_0"), new Vector2(32f, 32f)),
            LoadWorldSprite(CrowSpritePath($"walk_{direction}_1"), new Vector2(32f, 32f))
        };

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

        private static void SetBool(UnityEngine.Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
