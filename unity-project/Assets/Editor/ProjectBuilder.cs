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
        private const string MapPath = "Assets/Content/Maps/first_field.asset";
        private const string HouseMapPath = "Assets/Content/Maps/halka_house.asset";
        private const string HouseExteriorPath = "Assets/Content/World/house_exterior.png";
        private const string HouseFloorPath = "Assets/Content/World/house_floor.png";
        private const string HouseWallPath = "Assets/Content/World/house_wall.png";
        private const string HouseBedPath = "Assets/Content/World/house_bed.png";
        private const string CrowMaskPath = "Assets/Content/World/crow_grass_mask.png";
        private const string GrassPath = "Assets/Content/World/grass.png";
        private const string GrassRustlePath = "Assets/Content/World/grass_rustle";
        private const string FootstepPath = "Assets/Content/Audio/footstep_one_step.wav";
        private const string CrowVoicePath = "Assets/Content/Audio/crow_voice.mp3";
        private const string MessageFontPath = "Assets/Content/Fonts/k8x12L.ttf";
        private const string GameFontAtlasPath = "Assets/Resources/asobi_ui_atlas.png";
        private const string PlayerGrassMaskPath = "Assets/Content/World/player_grass_mask.png";
        private const string BenchSitPath = "Assets/Content/Character/bench_sit.png";
        private const string GrassPrefabPath = "Assets/Content/World/GrassDecoration.prefab";
        private const string GridPath = "Assets/Content/World/grid.png";
        private const float SpritePixelsPerUnit = 64f;
        internal const float ArtworkFootOffset = GridWorld2D.TileWorldSize / 2f;

        [MenuItem("HALKA/Prepare ver2.5.4 scene")]
        public static void PrepareScene()
        {
            ConfigureProject();
            ConfigureGameFontAtlas();
            MapAuthoringImporter.SyncAll();
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
            var houseMap = AssetDatabase.LoadAssetAtPath<MapDefinition>(HouseMapPath);
            if (map == null) throw new InvalidOperationException("first_field MapDefinition is missing");
            if (houseMap == null) throw new InvalidOperationException("halka_house MapDefinition is missing");
            var authoredMaps = Directory.GetFiles(MapAuthoringImporter.AuthoringFolder, "*.hwmap.json")
                .Select(path => AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/" +
                    Path.GetFileName(path).Replace(".hwmap.json", ".asset")))
                .Where(item => item != null).ToArray();
            var mapProblems = MapPlacementRules.Validate(map);
            if (mapProblems.Count != 0)
                throw new InvalidOperationException("Invalid MapDefinition: " + string.Join("; ", mapProblems));
            var houseProblems = MapPlacementRules.Validate(houseMap);
            if (houseProblems.Count != 0)
                throw new InvalidOperationException("Invalid halka_house: " + string.Join("; ", houseProblems));
            var startMap = authoredMaps.Single(item => item.TryGetEntitySpawn("player_main", out _));
            var playerStart = EntityRuntimeFactory2D.Required(startMap, "player_main");
            var crowMap = authoredMaps.First(item => item.TryGetEntitySpawn("crow_main", out _));
            var crowSpawn = EntityRuntimeFactory2D.Required(crowMap, "crow_main");
            var crowWanderMinimum = crowSpawn.Definition.WanderMinimum;
            var crowWanderMaximum = crowSpawn.Definition.WanderMaximum;
            var frames = LoadFrames("front_idle");
            if (frames.Length == 0) throw new InvalidOperationException("front_idle needs at least one PNG frame");
            var benchSit = LoadWorldSprite(BenchSitPath, new Vector2(64f, 64f));
            ConfigureSpriteImport(PixelPath, 1f);
            var pixel = AssetDatabase.LoadAssetAtPath<Sprite>(PixelPath);
            if (pixel == null) throw new InvalidOperationException("World pixel import failed");
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
            ConfigureAudioImport(FootstepPath);
            ConfigureAudioImport(CrowVoicePath);
            var footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepPath);
            if (footstepClip == null) throw new InvalidOperationException("Single-step audio import failed");
            var crowVoiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CrowVoicePath);
            if (crowVoiceClip == null) throw new InvalidOperationException("Crow voice import failed");
            var grassPrefab = CreateGrassPrefab(grassSprite);

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldObject = new GameObject("World - first day grid");
            var world = worldObject.AddComponent<GridWorld2D>();
            world.SetBounds(startMap.MinCell, startMap.MaxCell);

            var player = new GameObject("Player - HarukaChan");
            player.transform.position = world.CellToWorld(playerStart.Cell);
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
            SetString(mover, "initialFacing", playerStart.Facing);
            var visual = player.AddComponent<CharacterVisual>();
            var seat = player.AddComponent<PlayerSeatController>();
            SetReference(seat, "mover", mover);
            SetReference(seat, "world", world);
            SetReference(seat, "artwork", artwork.transform);
            SetReference(visual, "seat", seat);
            SetReference(visual, "benchSit", benchSit);
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

            var gridSprite = CreateGridOverlay(world);
            var gridObject = new GameObject("FirstDay - 32px cell boundaries");
            var gridRenderer = gridObject.AddComponent<SpriteRenderer>();
            gridRenderer.sprite = gridSprite;
            gridRenderer.sortingOrder = -8;

            var objectGroup = new GameObject("Map world objects");

            var occupancyObject = new GameObject("Dynamic grid occupancy");
            var occupancy = occupancyObject.AddComponent<DynamicGridOccupancy2D>();
            SetReference(occupancy, "player", mover);
            SetReference(mover, "occupancy", occupancy);
            var crow = new GameObject("Crow - first neighbor");
            crow.transform.position = world.CellToWorld(crowSpawn.Cell);
            var crowRenderer = crow.AddComponent<SpriteRenderer>();
            crowRenderer.sprite = crowIdleRight;
            crowRenderer.sortingOrder = 3;
            var crowCollider = crow.AddComponent<BoxCollider2D>();
            crowCollider.size = Vector2.one * GridWorld2D.TileWorldSize;
            var crowExamine = crow.AddComponent<ExamineInteractable>();
            var crowWander = crow.AddComponent<CrowWander2D>();
            SetReference(crowWander, "world", world);
            SetVector2Int(crowWander, "spawnCell", crowSpawn.Cell);
            SetVector2Int(crowWander, "wanderMinimumOffset", crowWanderMinimum);
            SetVector2Int(crowWander, "wanderMaximumOffset", crowWanderMaximum);
            SetString(crowWander, "initialFacing", crowSpawn.Facing);
            var crowAudioSource = crow.AddComponent<AudioSource>();
            crowAudioSource.playOnAwake = false;
            crowAudioSource.spatialBlend = 0f;
            crowAudioSource.volume = 0.55f;
            var crowVoice = crow.AddComponent<InteractionAudio>();
            SetReference(crowVoice, "audioSource", crowAudioSource);
            SetReference(crowVoice, "clip", crowVoiceClip);
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
            var grassField = grassGroup.AddComponent<GrassField2D>();
            SetReference(grassField, "world", world);
            SetReference(grassField, "player", mover);
            SetReference(grassField, "idleSprite", grassSprite);
            SetSprites(grassField, "rustleFrames", rustleFrames);
            SetReference(crowWander, "grassField", grassField);
            var grassOcclusion = player.AddComponent<PlayerGrassOcclusion>();
            SetReference(grassOcclusion, "mover", mover);
            SetReference(grassOcclusion, "seat", seat);
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
            SetReference(actionButton, "fallbackFont", messageFont);
            var interactionObject = new GameObject("Interaction Router");
            var interaction = interactionObject.AddComponent<InteractionRouter>();
            SetReference(interaction, "worldCamera", camera);
            SetReference(interaction, "player", mover);
            SetReference(interaction, "world", world);
            SetReference(interaction, "seat", seat);
            var inputObject = new GameObject("Input - keyboard pointer");
            var input = inputObject.AddComponent<GameInput>();
            SetReference(input, "interaction", interaction);
            SetReference(input, "seat", seat);
            SetReference(input, "dpad", dpad);
            SetReference(input, "actionButton", actionButton);
            SetReference(mover, "input", input);
            SetReference(mover, "world", world);
            SetReference(visual, "mover", mover);
            SetReference(visual, "spriteRenderer", playerRenderer);
            SetReference(crowExamine, "player", mover);
            SetReference(crowExamine, "world", world);
            SetReference(crowExamine, "hud", hud);
            SetString(crowExamine, "message", "カァ。");
            SetReference(crowExamine, "availability", crowWander);
            SetReference(crowExamine, "interactionAudio", crowVoice);
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
                objectGroup, crow, grassGroup })
                objectInField.transform.SetParent(exteriorRoot.transform, true);
            var loaderObject = new GameObject("Map runtime loader");
            loaderObject.transform.SetParent(exteriorRoot.transform, false);
            var loader = loaderObject.AddComponent<MapRuntimeLoader2D>();
            loader.SetMap(AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath));
            if (loader.Map == null)
                throw new InvalidOperationException("Could not serialize first_field on runtime loader");
            SetReference(loader, "world", world);
            SetReference(loader, "surfaceField", surfaceField);
            SetReference(loader, "grassField", grassField);
            SetReference(loader, "surfaceRoot", surfaceGroup.transform);
            SetReference(loader, "objectRoot", objectGroup.transform);
            SetReference(loader, "entityRoot", exteriorRoot.transform);
            SetReference(loader, "grassPrefab", grassPrefab);
            SetReference(loader, "player", mover);
            SetReference(loader, "playerRenderer", playerRenderer);
            SetReference(loader, "hud", hud);
            SetReference(loader, "crow", crowWander);
            var interiorRoot = new GameObject("Interior - halka_house");
            interiorRoot.SetActive(false);
            var backdrop = new GameObject("Interior dark backdrop");
            backdrop.transform.SetParent(interiorRoot.transform, false);
            backdrop.transform.localScale = new Vector3(20f, 14f, 1f);
            var backdropRenderer = backdrop.AddComponent<SpriteRenderer>();
            backdropRenderer.sprite = pixel;
            backdropRenderer.color = houseMap.BackdropColor;
            backdropRenderer.sortingOrder = -20;
            var interiorSurfaces = new GameObject("Interior surfaces");
            interiorSurfaces.transform.SetParent(interiorRoot.transform, false);
            var interiorSurfaceField = interiorSurfaces.AddComponent<GroundSurfaceField2D>();
            var interiorGrassGroup = new GameObject("Interior grass decorations");
            interiorGrassGroup.transform.SetParent(interiorRoot.transform, false);
            var interiorGrassField = interiorGrassGroup.AddComponent<GrassField2D>();
            SetReference(interiorGrassField, "world", world);
            SetReference(interiorGrassField, "player", mover);
            SetReference(interiorGrassField, "idleSprite", grassSprite);
            SetSprites(interiorGrassField, "rustleFrames", rustleFrames);
            var interiorObjects = new GameObject("Interior objects");
            interiorObjects.transform.SetParent(interiorRoot.transform, false);
            var interiorLoaderObject = new GameObject("Interior map loader");
            interiorLoaderObject.transform.SetParent(interiorRoot.transform, false);
            var interiorLoader = interiorLoaderObject.AddComponent<MapRuntimeLoader2D>();
            interiorLoader.SetMap(houseMap);
            SetReference(interiorLoader, "world", world);
            SetReference(interiorLoader, "surfaceField", interiorSurfaceField);
            SetReference(interiorLoader, "grassField", interiorGrassField);
            SetReference(interiorLoader, "grassPrefab", grassPrefab);
            SetReference(interiorLoader, "surfaceRoot", interiorSurfaces.transform);
            SetReference(interiorLoader, "objectRoot", interiorObjects.transform);
            SetReference(interiorLoader, "entityRoot", interiorRoot.transform);
            SetReference(interiorLoader, "player", mover);
            SetReference(interiorLoader, "playerRenderer", playerRenderer);
            SetReference(interiorLoader, "hud", hud);
            SetReference(interiorLoader, "crow", crowWander);

            var registeredMaps = new List<MapDefinition> { map, houseMap };
            var registeredLoaders = new List<MapRuntimeLoader2D> { loader, interiorLoader };
            var registeredRoots = new List<GameObject> { exteriorRoot, interiorRoot };
            var registeredGrass = new List<GrassField2D> { grassField, interiorGrassField };
            foreach (var extraMap in authoredMaps.Where(item => item != map && item != houseMap))
            {
                var extraRoot = new GameObject("Area - " + extraMap.MapId);
                extraRoot.SetActive(false);
                var extraGround = new GameObject("Area ground");
                extraGround.transform.SetParent(extraRoot.transform, false);
                extraGround.transform.localScale = new Vector3(
                    (extraMap.MaxCell.x - extraMap.MinCell.x + 1) * GridWorld2D.TileWorldSize,
                    (extraMap.MaxCell.y - extraMap.MinCell.y + 1) * GridWorld2D.TileWorldSize, 1f);
                extraGround.transform.position = new Vector3(
                    (extraMap.MinCell.x + extraMap.MaxCell.x) * GridWorld2D.TileWorldSize * 0.5f,
                    (extraMap.MinCell.y + extraMap.MaxCell.y) * GridWorld2D.TileWorldSize * 0.5f, 0f);
                var extraGroundRenderer = extraGround.AddComponent<SpriteRenderer>();
                extraGroundRenderer.sprite = pixel;
                extraGroundRenderer.color = extraMap.BackdropColor;
                extraGroundRenderer.sortingOrder = -10;
                var extraSurfaces = new GameObject("Area surfaces");
                extraSurfaces.transform.SetParent(extraRoot.transform, false);
                var extraSurfaceField = extraSurfaces.AddComponent<GroundSurfaceField2D>();
                var extraObjects = new GameObject("Area objects");
                extraObjects.transform.SetParent(extraRoot.transform, false);
                var extraGrass = new GameObject("Area grass");
                extraGrass.transform.SetParent(extraRoot.transform, false);
                var extraGrassField = extraGrass.AddComponent<GrassField2D>();
                SetReference(extraGrassField, "world", world);
                SetReference(extraGrassField, "player", mover);
                SetReference(extraGrassField, "idleSprite", grassSprite);
                SetSprites(extraGrassField, "rustleFrames", rustleFrames);
                var extraLoaderObject = new GameObject("Area map loader");
                extraLoaderObject.transform.SetParent(extraRoot.transform, false);
                var extraLoader = extraLoaderObject.AddComponent<MapRuntimeLoader2D>();
                extraLoader.SetMap(extraMap);
                SetReference(extraLoader, "world", world);
                SetReference(extraLoader, "surfaceField", extraSurfaceField);
                SetReference(extraLoader, "grassField", extraGrassField);
                SetReference(extraLoader, "grassPrefab", grassPrefab);
                SetReference(extraLoader, "surfaceRoot", extraSurfaces.transform);
                SetReference(extraLoader, "objectRoot", extraObjects.transform);
                SetReference(extraLoader, "entityRoot", extraRoot.transform);
                SetReference(extraLoader, "player", mover);
                SetReference(extraLoader, "playerRenderer", playerRenderer);
                SetReference(extraLoader, "hud", hud);
                SetReference(extraLoader, "crow", crowWander);
                registeredMaps.Add(extraMap);
                registeredLoaders.Add(extraLoader);
                registeredRoots.Add(extraRoot);
                registeredGrass.Add(extraGrassField);
            }

            var mapControllerObject = new GameObject("Map runtime controller");
            var mapController = mapControllerObject.AddComponent<MapWorldController2D>();
            SetReferences(mapController, "maps", registeredMaps.ToArray());
            SetReferences(mapController, "loaders", registeredLoaders.ToArray());
            SetReferences(mapController, "roots", registeredRoots.ToArray());
            SetReferences(mapController, "grassFields", registeredGrass.ToArray());
            SetReference(mapController, "player", mover);
            SetReference(mapController, "grassOcclusion", grassOcclusion);
            SetReference(mapController, "cameraFollow", follow);
            SetReference(mapController, "worldCamera", camera);
            SetReference(mover, "mapController", mapController);

            var areaObject = new GameObject("House area switch");
            var houseArea = areaObject.AddComponent<HouseArea2D>();
            SetReference(houseArea, "world", world);
            SetReference(houseArea, "player", mover);
            SetReference(houseArea, "grassOcclusion", grassOcclusion);
            SetReference(houseArea, "exteriorGrass", grassField);
            SetReference(houseArea, "interiorGrass", interiorGrassField);
            SetReference(houseArea, "maps", mapController);
            SetReference(houseArea, "worldCamera", camera);
            SetReference(houseArea, "cameraFollow", follow);
            SetReference(mapController, "houseArea", houseArea);

            var autoObject = new GameObject("AUTO living controller");
            var autoMode = autoObject.AddComponent<AutoModeController>();
            SetReference(autoMode, "input", input);
            SetReference(autoMode, "player", mover);
            SetReference(autoMode, "world", world);
            SetReference(autoMode, "house", houseArea);
            SetReference(autoMode, "occupancy", occupancy);
            SetReference(autoMode, "crow", crowWander);
            var lifeLogObject = new GameObject("Life log controller");
            var lifeLog = lifeLogObject.AddComponent<LifeLogController>();
            SetReference(lifeLog, "player", mover);
            var menuObject = new GameObject("UI - game menu");
            var menu = menuObject.AddComponent<GameMenuController>();
            SetReference(menu, "autoMode", autoMode);
            SetReference(menu, "lifeLog", lifeLog);
            SetReference(menu, "hud", hud);
            SetReference(menu, "font", messageFont);
            SetReference(input, "menu", menu);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"HALKA ver{GameVersion.Value} scene prepared from {map.MapId}: " +
                $"{map.Surfaces.Count} ground overrides and {map.Objects.Count} objects.");
        }

        [MenuItem("HALKA/Build WebGL for HP")]
        public static void BuildWeb()
        {
            ConfigureProject();
            // The Scene is generated from Map JSON. Rebuild it on every release build so
            // edits made in the standalone editor cannot be shadowed by an older Scene.
            PrepareScene();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "halkaworld", "webgl"));
            Directory.CreateDirectory(output);
            var buildFolder = Path.Combine(output, "Build");
            if (Directory.Exists(buildFolder))
                foreach (var file in Directory.GetFiles(buildFolder))
                    if (file.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".data", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".loader.js", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".framework.js", StringComparison.OrdinalIgnoreCase))
                        File.Delete(file);
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
            SetReference(visual, "benchSit", LoadWorldSprite(BenchSitPath, new Vector2(64f, 64f)));
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
            var playerSprite = path.StartsWith("Assets/Content/Character/", StringComparison.OrdinalIgnoreCase);
            if (importer.textureType == TextureImporterType.Sprite &&
                Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                (!playerSprite || settings.spriteMeshType == SpriteMeshType.FullRect) &&
                settings.spriteAlignment == (int)SpriteAlignment.Center &&
                settings.spritePivot == new Vector2(0.5f, 0.5f)) return;
            if (playerSprite) settings.spriteMeshType = SpriteMeshType.FullRect;
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

        private static void ConfigureAudioImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Audio asset is missing: " + path);
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

        private static void SetReferences(UnityEngine.Object target, string field, params UnityEngine.Object[] values)
        {
            var member = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (member == null || !member.FieldType.IsArray)
                throw new InvalidOperationException("Reference array not found: " + field);
            var elementType = member.FieldType.GetElementType();
            var array = Array.CreateInstance(elementType, values.Length);
            for (var i = 0; i < values.Length; i++)
                array.SetValue(values[i], i);
            member.SetValue(target, array);
            EditorUtility.SetDirty(target);
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

        private static void ConfigureGameFontAtlas()
        {
            var importer = AssetImporter.GetAtPath(GameFontAtlasPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Rasterized game UI glyph atlas is missing");
            if (importer.textureType == TextureImporterType.Default &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.npotScale == TextureImporterNPOTScale.None) return;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static void SetVector2Int(UnityEngine.Object target, string field, Vector2Int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector2IntValue = value;
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
