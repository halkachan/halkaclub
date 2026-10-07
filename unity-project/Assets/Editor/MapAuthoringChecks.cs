using System;
using System.IO;
using System.Linq;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.Interaction;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class MapAuthoringChecks
    {
        private const System.Reflection.BindingFlags Hidden =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private const string OfficialPath = "Assets/Content/Maps/first_field.asset";
        private const string FixtureAssetPath = "Assets/Content/Maps/standalone_contract_fixture.asset";

        [MenuItem("HALKA WORLD/Build v0.6 WebGL regression to Temp")]
        public static void BuildWebGLRegression()
        {
            ProjectBuilder.PrepareScene();
            var output = Path.Combine(Path.GetTempPath(), "HalkaMapEditorV06WebGLRegression");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/FirstDay.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            Check(report.summary.result == BuildResult.Succeeded,
                "WebGL regression build succeeded");
            Debug.Log("HALKA WORLD Map Editor v0.6 WebGL regression built to " + output);
        }

        [MenuItem("HALKA WORLD/Validate Standalone Map Contract v0.6")]
        public static void Run()
        {
            MapAuthoringImporter.SyncAll();
            var official = AssetDatabase.LoadAssetAtPath<MapDefinition>(OfficialPath);
            var interior = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/halka_house.asset");
            Check(official != null, "official generated cache exists");
            Check(interior != null && interior.MapId == "halka_house" &&
                interior.MapType == "interior" && interior.GrassMode == "none" &&
                interior.BaseSurface.StableId == "house_floor", "indoor map is imported");
            Check(official.DataVersion == 4 && interior.DataVersion == 4 &&
                official.EntitySpawns.Count == 2 && interior.EntitySpawns.Count == 0 &&
                official.AreaTransitions.Count == 0 && interior.AreaTransitions.Count == 0 &&
                official.Markers.All(item => item.StableId != "player_start" && item.StableId != "crow_spawn"),
                "v3 Entity Spawns replace actor markers");
            var playerSpawn = EntityRuntimeFactory2D.Required(official, "player_main");
            var crowSpawn = EntityRuntimeFactory2D.Required(official, "crow_main");
            Check(playerSpawn.Cell == Vector2Int.zero && playerSpawn.Facing == "down" &&
                crowSpawn.Cell == new Vector2Int(7, 2) && crowSpawn.Facing == "right" &&
                crowSpawn.Definition.WanderMinimum == new Vector2Int(-3, -2) &&
                crowSpawn.Definition.WanderMaximum == new Vector2Int(1, 2),
                "Player and Crow authoring positions and relative wander import");
            var movedSpawns = new System.Collections.Generic.List<EntitySpawnPlacement>(official.EntitySpawns);
            for (var i = 0; i < movedSpawns.Count; i++)
            {
                var item = movedSpawns[i];
                item.Cell = item.Definition.StableId == "crow_main" ? new Vector2Int(0, 3) : new Vector2Int(0, -3);
                movedSpawns[i] = item;
            }
            var movedFixture = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                movedFixture.ReplaceFromAuthoring("moved_spawn_fixture", "Moved spawn fixture", official.MapType,
                    official.BaseSurface, official.GrassMode, official.BackdropColor, official.MinCell, official.MaxCell,
                    new System.Collections.Generic.List<SurfacePlacement>(official.Surfaces),
                    new System.Collections.Generic.List<WorldObjectPlacement>(official.Objects),
                    new System.Collections.Generic.List<LockedMapMarker>(official.Markers), movedSpawns);
                var movedCrow = EntityRuntimeFactory2D.Required(movedFixture, "crow_main");
                Check(EntityRuntimeFactory2D.Required(movedFixture, "player_main").Cell == new Vector2Int(0, -3) &&
                    movedCrow.Cell == new Vector2Int(0, 3) &&
                    movedCrow.Cell + movedCrow.Definition.WanderMinimum == new Vector2Int(-3, 1) &&
                    movedCrow.Cell + movedCrow.Definition.WanderMaximum == new Vector2Int(1, 5) &&
                    MapPlacementRules.Validate(movedFixture).Count == 0,
                    "moved Player and Crow fixture follows authored cells");
            }
            finally { UnityEngine.Object.DestroyImmediate(movedFixture); }
            var bed = interior.Objects.Single(item => item.Definition.StableId == "bed_basic").Definition;
            Check(bed.ActionPoints.Count == 1 && bed.ActionPoints[0].Id == "sleep_main" &&
                bed.ActionPoints[0].PlayerCellOffset == new Vector2Int(0, -1) &&
                bed.ActionPoints[0].PlayerFacing == WorldFacing.Up &&
                bed.ActionPoints[0].ActionType == WorldActionType.Sleep &&
                bed.ActionPoints[0].PoseKey == "bed_sleep" &&
                bed.Behavior == WorldObjectBehavior.None, "bed sleep metadata is imported without execution");
            foreach (var idAndText in new[] { ("stone_basic", "いし。"),
                         ("flower_basic", "はな。"), ("tree_basic", "き。") })
            {
                var definition = official.Objects.First(item => item.Definition.StableId == idAndText.Item1).Definition;
                Check(definition.ActionPoints.Count == 1 &&
                    definition.ActionPoints[0].ActionType == WorldActionType.Examine &&
                    definition.ActionPoints[0].InteractionText == idAndText.Item2 &&
                    definition.ExamineMessage == idAndText.Item2 &&
                    definition.Behavior == WorldObjectBehavior.Examine,
                    idAndText.Item1 + " examine metadata matches runtime text");
            }
            foreach (var sprite in new[] { ("cushion", 32, 32), ("desk", 64, 32),
                         ("bench", 64, 32), ("sign", 32, 32), ("well", 32, 32) })
            {
                var path = "Assets/Content/World/" + sprite.Item1 + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(texture != null && texture.width == sprite.Item2 && texture.height == sprite.Item3 &&
                    importer != null && importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.spritePixelsPerUnit == 64 && importer.filterMode == FilterMode.Point &&
                    !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.npotScale == TextureImporterNPOTScale.None,
                    sprite.Item1 + " sprite dimensions and import settings");
            }
            foreach (var entry in new[] { ("cushion_basic", "cushion", 0, WorldObjectBehavior.None),
                         ("desk_basic", "desk", 2, WorldObjectBehavior.Examine),
                         ("bench_basic", "bench", 2, WorldObjectBehavior.None),
                         ("sign_basic", "sign", 1, WorldObjectBehavior.Examine),
                         ("well_basic", "well", 1, WorldObjectBehavior.Examine) })
            {
                var definition = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                    "Assets/Content/Maps/" + entry.Item1 + ".asset");
                Check(definition != null && definition.PreviewSprite != null &&
                    AssetDatabase.GetAssetPath(definition.PreviewSprite) ==
                    "Assets/Content/World/" + entry.Item2 + ".png" &&
                    definition.EffectiveBlockedOffsets().Count() == entry.Item3 &&
                    definition.Behavior == entry.Item4 && definition.ActionPoints.Count > 0,
                    entry.Item1 + " Unity Definition and Action Point");
            }
            var cushion = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/cushion_basic.asset");
            var bench = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/bench_basic.asset");
            Check(!cushion.BlocksMovement && cushion.ActionPoints[0].ActionType == WorldActionType.Sit &&
                bench.ActionPoints.Count == 2 && bench.ActionPoints.All(point => point.ActionType == WorldActionType.Sit),
                "sit Action Points remain metadata only");
            var sign = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_basic.asset");
            Check(sign.ActionPoints[0].InteractionText == "かんばん。" &&
                sign.ActionPoints[0].PlayerFacing == WorldFacing.Up &&
                sign.ExamineMessage == "かんばん。" && sign.PreviewSprite.rect.size == new Vector2(32, 32),
                "one-cell sign definition is imported");
            Check(AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_north.asset") == null &&
                AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_east.asset") == null &&
                AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_south.asset") == null &&
                AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_west.asset") == null,
                "obsolete directional sign assets are absent");
            var baseGround = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>("Assets/Content/Maps/base_ground.asset");
            Check(baseGround != null && baseGround.GrowsGrass, "grass ground Surface is available on both maps");
            ValidateRuntimeSpriteFixtures();
            ValidateCrossMapRuntimeFixtures();
            ValidateIndoorCrowRuntimeFixture(interior, crowSpawn, playerSpawn);
            ValidateAreaTransitionRuntimeFixture();
            var before = JsonUtility.ToJson(official);
            var authoringPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "Content/Maps/Authoring/first_field.hwmap.json"));
            var source = File.ReadAllText(authoringPath);
            var fixture = source.Replace("\"mapId\": \"first_field\"",
                "\"mapId\": \"standalone_contract_fixture\"");
            Check(fixture != source, "fixture mapId replaced");
            var temporaryFolder = Path.Combine(Application.dataPath, "../Temp/StandaloneMapContract");
            Directory.CreateDirectory(temporaryFolder);
            var fixturePath = Path.Combine(temporaryFolder, "standalone_contract_fixture.hwmap.json");
            var invalidPath = Path.Combine(temporaryFolder, "first_field.hwmap.json");
            Check(AssetDatabase.LoadAssetAtPath<MapDefinition>(FixtureAssetPath) == null,
                "old fixture cache is absent");
            try
            {
                File.WriteAllText(fixturePath, fixture);
                var imported = MapAuthoringImporter.SyncMapFromFile(fixturePath);
                Check(imported != null && imported.MapId == "standalone_contract_fixture" &&
                    imported.MinCell == new Vector2Int(-10, -6) &&
                    imported.MaxCell == new Vector2Int(10, 6), "fixture bounds and mapId");
                Check(imported.Surfaces.Count == official.Surfaces.Count &&
                    imported.Objects.Count == official.Objects.Count,
                    "fixture placement counts");
                Check(imported.Objects.All(item => !string.IsNullOrWhiteSpace(item.InstanceId)) &&
                    imported.Objects.Select(item => item.InstanceId).Distinct().Count() == official.Objects.Count,
                    "fixture instance IDs are preserved and unique");
                Check(imported.PlayerSpawnCell == new Vector2Int(0, 0) &&
                    imported.HouseDoorCell == new Vector2Int(-7, -4) &&
                    imported.NorthRoadEnd == new Vector2Int(0, 6), "fixture markers");
                Check(MapPlacementRules.Validate(imported).Count == 0, "fixture validates");
                Check(interior.MinCell == new Vector2Int(-6, -4) &&
                    interior.MaxCell == new Vector2Int(6, 4) &&
                    interior.Surfaces.Count == 50 && interior.Objects.Count == 1 &&
                    interior.TryGetMarker("interior_entry", out var entry) && entry == new Vector2Int(0, -3) &&
                    interior.TryGetMarker("interior_exit", out var exit) && exit == new Vector2Int(0, -4) &&
                    MapPlacementRules.Validate(interior).Count == 0,
                    "indoor bounds, walls, bed and passage match the former room");
                File.WriteAllText(invalidPath, "{ broken json");
                var rejected = false;
                try { MapAuthoringImporter.SyncMapFromFile(invalidPath); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "invalid JSON rejected");
                Check(JsonUtility.ToJson(official) == before,
                    "invalid JSON leaves official generated cache unchanged");
                Debug.Log("HALKA WORLD Standalone Map Contract v0.6: passed.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(FixtureAssetPath);
                if (File.Exists(fixturePath)) File.Delete(fixturePath);
                if (File.Exists(invalidPath)) File.Delete(invalidPath);
                if (Directory.Exists(temporaryFolder)) Directory.Delete(temporaryFolder);
                AssetDatabase.SaveAssets();
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Standalone Map Contract failed: " + name);
        }

        private static void ValidateAreaTransitionRuntimeFixture()
        {
            var a = ScriptableObject.CreateInstance<MapDefinition>();
            var b = ScriptableObject.CreateInstance<MapDefinition>();
            var root = new GameObject("v0.6 area transition fixture");
            var areaA = new GameObject("Area A");
            var areaB = new GameObject("Area B");
            areaA.transform.SetParent(root.transform, false);
            areaB.transform.SetParent(root.transform, false);
            try
            {
                var boundsMin = Vector2Int.zero;
                var boundsMax = new Vector2Int(2, 2);
                var playerDef = AssetDatabase.LoadAssetAtPath<EntityDefinition>(
                    "Assets/Content/Maps/player_main.asset");
                a.ReplaceFromAuthoring("fixture_a", "Fixture A", "outdoor", null, "none", Color.black,
                    boundsMin, boundsMax, new System.Collections.Generic.List<SurfacePlacement>(),
                    new System.Collections.Generic.List<WorldObjectPlacement>(),
                    new System.Collections.Generic.List<LockedMapMarker>(),
                    new System.Collections.Generic.List<EntitySpawnPlacement> {
                        new EntitySpawnPlacement { InstanceId = "fixture_player", Definition = playerDef,
                            Cell = new Vector2Int(1, 2), Facing = "up" }
                    }, new System.Collections.Generic.List<AreaTransitionPlacement> {
                        new AreaTransitionPlacement { InstanceId = "exit_a", TransitionId = "north_exit",
                            SourceCell = new Vector2Int(1, 2), ExitDirection = "up", DestinationMapId = "fixture_b",
                            DestinationCell = Vector2Int.zero, ArrivalFacing = "left" }
                    });
                b.ReplaceFromAuthoring("fixture_b", "Fixture B", "outdoor", null, "none", Color.black,
                    boundsMin, boundsMax, new System.Collections.Generic.List<SurfacePlacement>(),
                    new System.Collections.Generic.List<WorldObjectPlacement>(),
                    new System.Collections.Generic.List<LockedMapMarker>(), null,
                    new System.Collections.Generic.List<AreaTransitionPlacement> {
                        new AreaTransitionPlacement { InstanceId = "exit_b", TransitionId = "south_exit",
                            SourceCell = Vector2Int.zero, ExitDirection = "down", DestinationMapId = "fixture_a",
                            DestinationCell = new Vector2Int(1, 2), ArrivalFacing = "up" }
                    });
                Check(MapPlacementRules.Validate(a).Count == 0 && MapPlacementRules.Validate(b).Count == 0,
                    "fixture A/B edges and destinations validate");
                var grid = root.AddComponent<GridWorld2D>();
                var playerObject = new GameObject("Player");
                playerObject.transform.SetParent(root.transform, false);
                var player = playerObject.AddComponent<PlayerMover>();
                typeof(PlayerMover).GetField("world", Hidden).SetValue(player, grid);
                var renderer = playerObject.AddComponent<SpriteRenderer>();
                var hud = root.AddComponent<GameHud>();
                var loaders = new MapRuntimeLoader2D[2];
                var areas = new[] { areaA, areaB };
                var definitions = new[] { a, b };
                for (var i = 0; i < 2; i++)
                {
                    var surfaces = new GameObject("Surfaces");
                    surfaces.transform.SetParent(areas[i].transform, false);
                    var objects = new GameObject("Objects");
                    objects.transform.SetParent(areas[i].transform, false);
                    var loader = areas[i].AddComponent<MapRuntimeLoader2D>();
                    loader.SetMap(definitions[i]);
                    typeof(MapRuntimeLoader2D).GetField("world", Hidden).SetValue(loader, grid);
                    typeof(MapRuntimeLoader2D).GetField("surfaceField", Hidden).SetValue(loader,
                        surfaces.AddComponent<GroundSurfaceField2D>());
                    typeof(MapRuntimeLoader2D).GetField("surfaceRoot", Hidden).SetValue(loader, surfaces.transform);
                    typeof(MapRuntimeLoader2D).GetField("objectRoot", Hidden).SetValue(loader, objects.transform);
                    typeof(MapRuntimeLoader2D).GetField("entityRoot", Hidden).SetValue(loader, areas[i].transform);
                    typeof(MapRuntimeLoader2D).GetField("player", Hidden).SetValue(loader, player);
                    typeof(MapRuntimeLoader2D).GetField("playerRenderer", Hidden).SetValue(loader, renderer);
                    typeof(MapRuntimeLoader2D).GetField("hud", Hidden).SetValue(loader, hud);
                    loaders[i] = loader;
                }
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                var follow = cameraObject.AddComponent<Halka.Game.CameraControl.CameraFollow2D>();
                typeof(Halka.Game.CameraControl.CameraFollow2D).GetField("target", Hidden).SetValue(follow, playerObject.transform);
                typeof(Halka.Game.CameraControl.CameraFollow2D).GetField("cameraComponent", Hidden).SetValue(follow, camera);
                var controller = root.AddComponent<MapWorldController2D>();
                typeof(MapWorldController2D).GetField("maps", Hidden).SetValue(controller, definitions);
                typeof(MapWorldController2D).GetField("loaders", Hidden).SetValue(controller, loaders);
                typeof(MapWorldController2D).GetField("roots", Hidden).SetValue(controller, areas);
                typeof(MapWorldController2D).GetField("player", Hidden).SetValue(controller, player);
                typeof(MapWorldController2D).GetField("cameraFollow", Hidden).SetValue(controller, follow);
                typeof(PlayerMover).GetField("mapController", Hidden).SetValue(player, controller);
                typeof(MapWorldController2D).GetMethod("Awake", Hidden).Invoke(controller, null);
                loaders[0].Build();
                typeof(PlayerMover).GetMethod("Awake", Hidden).Invoke(player, null);
                var steps = 0;
                player.StepStarted += _ => steps++;
                player.ApplyDirection(Vector2Int.up);
                Check(controller.ActiveMapId == "fixture_b" && player.Cell == Vector2Int.zero &&
                    player.Facing == FacingDirection.Left && areas[1].activeSelf && !areas[0].activeSelf &&
                    follow.transform.position == follow.TargetPosition && steps == 0,
                    "manual A to B transition cuts camera without movement Step");
                Check(controller.ActiveMapId == "fixture_b" && player.Cell == Vector2Int.zero,
                    "arrival does not automatically bounce");
                Check(!player.TryStep(Vector2Int.down) && controller.ActiveMapId == "fixture_b",
                    "AUTO step cannot trigger an area exit");
                typeof(PlayerMover).GetMethod("Tick", Hidden).Invoke(player,
                    new object[] { 0.01f, Vector2Int.zero });
                player.ApplyDirection(Vector2Int.down);
                Check(controller.ActiveMapId == "fixture_a" && player.Cell == new Vector2Int(1, 2) &&
                    player.Facing == FacingDirection.Up && areas[0].activeSelf && !areas[1].activeSelf &&
                    follow.transform.position == follow.TargetPosition && steps == 0,
                    "manual B to A separately authored return transition");
                Check(!controller.TryExit(new Vector2Int(1, 2), Vector2Int.left) &&
                    controller.ActiveMapId == "fixture_a", "unconfigured direction cannot transition");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        private static void ValidateRuntimeSpriteFixtures()
        {
            var ids = new[] { "cushion_basic", "desk_basic", "bench_basic", "sign_basic", "well_basic" };
            var cells = new[] { new Vector2Int(-4, 0), new Vector2Int(-2, 0),
                new Vector2Int(1, 0), new Vector2Int(4, -1), new Vector2Int(4, 2) };
            var definitions = ids.Select(id => AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/" + id + ".asset")).ToArray();
            var fixture = ScriptableObject.CreateInstance<MapDefinition>();
            var testRoot = new GameObject("v0.4 object runtime fixture");
            try
            {
                fixture.ReplaceFromAuthoring("v04_object_sprite_fixture", "Object sprite fixture", "interior",
                    null, "none", Color.black, new Vector2Int(-6, -4), new Vector2Int(6, 4),
                    new System.Collections.Generic.List<SurfacePlacement>(),
                    ids.Select((id, index) => new WorldObjectPlacement {
                        InstanceId = "fixture_" + id, RootCell = cells[index], Definition = definitions[index],
                        SignText = id == "sign_basic" ? "きた" : null
                    }).ToList(), new System.Collections.Generic.List<LockedMapMarker>());
                Check(MapPlacementRules.Validate(fixture).Count == 0, "five object runtime fixture validates");
                var grid = testRoot.AddComponent<GridWorld2D>();
                var surfaces = testRoot.AddComponent<GroundSurfaceField2D>();
                var player = testRoot.AddComponent<PlayerMover>();
                var renderer = testRoot.AddComponent<SpriteRenderer>();
                var hud = testRoot.AddComponent<GameHud>();
                var surfaceRoot = new GameObject("Surfaces").transform;
                var objectRoot = new GameObject("Objects").transform;
                surfaceRoot.SetParent(testRoot.transform, false);
                objectRoot.SetParent(testRoot.transform, false);
                var loader = testRoot.AddComponent<MapRuntimeLoader2D>();
                var serialized = new SerializedObject(loader);
                serialized.FindProperty("map").objectReferenceValue = fixture;
                serialized.FindProperty("world").objectReferenceValue = grid;
                serialized.FindProperty("surfaceField").objectReferenceValue = surfaces;
                serialized.FindProperty("surfaceRoot").objectReferenceValue = surfaceRoot;
                serialized.FindProperty("objectRoot").objectReferenceValue = objectRoot;
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.FindProperty("playerRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("hud").objectReferenceValue = hud;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                loader.Build();
                Check(loader.IsBuilt && objectRoot.childCount == ids.Length,
                    "runtime loader creates all five object sprites");
                for (var index = 0; index < ids.Length; index++)
                {
                    var root = objectRoot.GetChild(index);
                    var artwork = root.GetComponentInChildren<SpriteRenderer>();
                    Check(artwork != null && artwork.sprite == definitions[index].PreviewSprite &&
                        root.position == grid.CellToWorld(cells[index]) &&
                        root.GetComponentsInChildren<GridObstacle>().Length ==
                        definitions[index].EffectiveBlockedOffsets().Count(),
                        ids[index] + " runtime sprite, root position and blocked cells");
                    Check((root.GetComponent<ExamineInteractable>() != null ||
                           root.GetComponent<WorldObjectActionInteractable>() != null) ==
                        (definitions[index].ActionPoints.Count > 0),
                        ids[index] + " runtime examine component");
                    if (ids[index] == "sign_basic")
                        Check(new SerializedObject(root.GetComponent<ExamineInteractable>())
                            .FindProperty("message").stringValue == "きた",
                            "runtime sign uses placement text");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testRoot);
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        private static void ValidateCrossMapRuntimeFixtures()
        {
            var baseGround = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(
                "Assets/Content/Maps/base_ground.asset");
            var floor = AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(
                "Assets/Content/Maps/house_floor.asset");
            var tree = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/tree_basic.asset");
            var sign = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/sign_basic.asset");
            var bed = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/bed_basic.asset");
            var house = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/house_main.asset");
            var grassSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Content/World/grass.png");
            var grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Content/World/GrassDecoration.prefab");
            var fixture = ScriptableObject.CreateInstance<MapDefinition>();
            var outdoorFixture = ScriptableObject.CreateInstance<MapDefinition>();
            var root = new GameObject("v0.4 cross-map runtime fixture");
            try
            {
                fixture.ReplaceFromAuthoring("cross_map_fixture", "Cross-map fixture", "interior",
                    floor, "none", Color.black, new Vector2Int(-6, -4), new Vector2Int(6, 4),
                    new System.Collections.Generic.List<SurfacePlacement> {
                        new SurfacePlacement { Cell = Vector2Int.zero, Definition = baseGround }
                    },
                    new System.Collections.Generic.List<WorldObjectPlacement> {
                        new WorldObjectPlacement { InstanceId = "indoor_tree", RootCell = new Vector2Int(2, 0), Definition = tree },
                        new WorldObjectPlacement { InstanceId = "indoor_sign", RootCell = new Vector2Int(-2, 0),
                            Definition = sign, SignText = "もり" }
                    }, new System.Collections.Generic.List<LockedMapMarker>());
                Check(MapPlacementRules.Validate(fixture).Count == 0 &&
                    MapPlacementRules.HasGrass(fixture, Vector2Int.zero),
                    "interior accepts tree, sign and explicit grass ground");
                var grid = root.AddComponent<GridWorld2D>();
                var player = root.AddComponent<PlayerMover>();
                var playerRenderer = root.AddComponent<SpriteRenderer>();
                var hud = root.AddComponent<GameHud>();
                var surfaces = root.AddComponent<GroundSurfaceField2D>();
                var surfaceRoot = new GameObject("Surfaces").transform;
                var objectRoot = new GameObject("Objects").transform;
                var grassRoot = new GameObject("Grass");
                surfaceRoot.SetParent(root.transform, false);
                objectRoot.SetParent(root.transform, false);
                grassRoot.transform.SetParent(root.transform, false);
                var grass = grassRoot.AddComponent<GrassField2D>();
                var grassData = new SerializedObject(grass);
                grassData.FindProperty("world").objectReferenceValue = grid;
                grassData.FindProperty("player").objectReferenceValue = player;
                grassData.FindProperty("idleSprite").objectReferenceValue = grassSprite;
                var frames = grassData.FindProperty("rustleFrames");
                frames.arraySize = 5;
                for (var i = 0; i < 5; i++)
                    frames.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>(
                            "Assets/Content/World/grass_rustle/0" + i + ".png");
                grassData.ApplyModifiedPropertiesWithoutUndo();
                var loader = root.AddComponent<MapRuntimeLoader2D>();
                var data = new SerializedObject(loader);
                data.FindProperty("map").objectReferenceValue = fixture;
                data.FindProperty("world").objectReferenceValue = grid;
                data.FindProperty("surfaceField").objectReferenceValue = surfaces;
                data.FindProperty("grassField").objectReferenceValue = grass;
                data.FindProperty("surfaceRoot").objectReferenceValue = surfaceRoot;
                data.FindProperty("objectRoot").objectReferenceValue = objectRoot;
                data.FindProperty("grassPrefab").objectReferenceValue = grassPrefab;
                data.FindProperty("player").objectReferenceValue = player;
                data.FindProperty("playerRenderer").objectReferenceValue = playerRenderer;
                data.FindProperty("hud").objectReferenceValue = hud;
                data.ApplyModifiedPropertiesWithoutUndo();
                loader.Build();
                Check(loader.IsBuilt && grass.HasGrass(Vector2Int.zero) &&
                    surfaces.GetSurface(Vector2Int.zero) == baseGround.Sprite,
                    "interior runtime creates grass on explicit ground");
                var treeRoot = objectRoot.GetChild(0);
                Check(treeRoot.GetComponent<RootedWorldObjectDepth2D>() != null &&
                    treeRoot.GetComponentsInChildren<GridObstacle>().Length == 1,
                    "indoor tree keeps depth and collision");
                var signRoot = objectRoot.GetChild(1);
                Check(new SerializedObject(signRoot.GetComponent<ExamineInteractable>())
                    .FindProperty("message").stringValue == "もり" &&
                    signRoot.GetComponentsInChildren<GridObstacle>().Length == 1,
                    "indoor sign uses its own text and one blocked cell");

                outdoorFixture.ReplaceFromAuthoring("outdoor_bed_fixture", "Outdoor bed fixture", "outdoor",
                    baseGround, "auto", Color.white, new Vector2Int(-6, -4), new Vector2Int(6, 4),
                    new System.Collections.Generic.List<SurfacePlacement>(),
                    new System.Collections.Generic.List<WorldObjectPlacement> {
                        new WorldObjectPlacement { InstanceId = "outdoor_house", RootCell = new Vector2Int(-4, -2),
                            Definition = house },
                        new WorldObjectPlacement { InstanceId = "outdoor_bed", RootCell = new Vector2Int(2, 0),
                            Definition = bed }
                    }, new System.Collections.Generic.List<LockedMapMarker>());
                loader.Unload();
                loader.SetMap(outdoorFixture);
                loader.Build();
                Check(loader.IsBuilt && objectRoot.childCount == 2 &&
                    objectRoot.GetChild(1).GetComponentsInChildren<GridObstacle>().Length == 6 &&
                    !grid.CanEnter(new Vector2Int(2, 0)) && grass.HasGrass(Vector2Int.zero),
                    "outdoor runtime creates bed collision and keeps nearby grass");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Object.DestroyImmediate(outdoorFixture);
            }
        }

        private static void ValidateIndoorCrowRuntimeFixture(MapDefinition interior,
            EntitySpawnPlacement outdoorCrow, EntitySpawnPlacement outdoorPlayer)
        {
            var fixture = ScriptableObject.CreateInstance<MapDefinition>();
            var playerFixture = ScriptableObject.CreateInstance<MapDefinition>();
            var root = new GameObject("Indoor crow spawn fixture");
            try
            {
                var indoorCrow = outdoorCrow;
                indoorCrow.Cell = new Vector2Int(3, 0);
                indoorCrow.Facing = "up";
                fixture.ReplaceFromAuthoring("indoor_crow_fixture", "Indoor crow fixture", interior.MapType,
                    interior.BaseSurface, interior.GrassMode, interior.BackdropColor,
                    interior.MinCell, interior.MaxCell,
                    new System.Collections.Generic.List<SurfacePlacement>(interior.Surfaces),
                    new System.Collections.Generic.List<WorldObjectPlacement>(interior.Objects),
                    new System.Collections.Generic.List<LockedMapMarker>(interior.Markers),
                    new System.Collections.Generic.List<EntitySpawnPlacement> { indoorCrow });
                Check(MapPlacementRules.Validate(fixture).Count == 0,
                    "indoor Crow Spawn is valid without a Player duplicate");
                var grid = root.AddComponent<GridWorld2D>();
                var surfaceField = root.AddComponent<GroundSurfaceField2D>();
                var grassField = root.AddComponent<GrassField2D>();
                var playerObject = new GameObject("Player");
                playerObject.transform.SetParent(root.transform, false);
                var player = playerObject.AddComponent<PlayerMover>();
                var renderer = playerObject.AddComponent<SpriteRenderer>();
                var hud = root.AddComponent<GameHud>();
                var surfaceRoot = new GameObject("Surfaces").transform;
                var objectRoot = new GameObject("Objects").transform;
                surfaceRoot.SetParent(root.transform, false);
                objectRoot.SetParent(root.transform, false);
                var crowObject = new GameObject("Crow");
                crowObject.transform.SetParent(root.transform, false);
                var crow = crowObject.AddComponent<CrowWander2D>();
                var loader = root.AddComponent<MapRuntimeLoader2D>();
                var serialized = new SerializedObject(loader);
                serialized.FindProperty("map").objectReferenceValue = fixture;
                serialized.FindProperty("world").objectReferenceValue = grid;
                serialized.FindProperty("surfaceField").objectReferenceValue = surfaceField;
                serialized.FindProperty("grassField").objectReferenceValue = grassField;
                serialized.FindProperty("surfaceRoot").objectReferenceValue = surfaceRoot;
                serialized.FindProperty("objectRoot").objectReferenceValue = objectRoot;
                serialized.FindProperty("entityRoot").objectReferenceValue = root.transform;
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.FindProperty("playerRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("hud").objectReferenceValue = hud;
                serialized.FindProperty("crow").objectReferenceValue = crow;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                loader.Build();
                Check(loader.IsBuilt && crow.transform.position == grid.CellToWorld(indoorCrow.Cell) &&
                    crow.RangeMin == new Vector2Int(0, -2) &&
                    crow.RangeMax == new Vector2Int(4, 2) &&
                    crow.Facing == FacingDirection.Up,
                    "indoor runtime applies authored Crow Spawn and relative wander range");
                var indoorPlayer = outdoorPlayer;
                indoorPlayer.Cell = new Vector2Int(1, 0);
                indoorPlayer.Facing = "left";
                playerFixture.ReplaceFromAuthoring("indoor_player_fixture", "Indoor Player fixture", interior.MapType,
                    interior.BaseSurface, interior.GrassMode, interior.BackdropColor,
                    interior.MinCell, interior.MaxCell,
                    new System.Collections.Generic.List<SurfacePlacement>(interior.Surfaces),
                    new System.Collections.Generic.List<WorldObjectPlacement>(interior.Objects),
                    new System.Collections.Generic.List<LockedMapMarker>(interior.Markers),
                    new System.Collections.Generic.List<EntitySpawnPlacement> { indoorPlayer });
                Check(MapPlacementRules.Validate(playerFixture).Count == 0,
                    "indoor Player Start placement validates");
                Check(EntityRuntimeFactory2D.FindPlayerStartMap(new[] { interior, playerFixture }) == 1,
                    "indoor Player Start selects the indoor map");
                Check(EntityRuntimeFactory2D.TryApplyPlayerStart(playerFixture, grid, player),
                    "indoor Player Start is applied to the persistent actor");
                Check(player.transform.position == grid.CellToWorld(indoorPlayer.Cell) &&
                    player.Facing == FacingDirection.Left,
                    "indoor Player Start sets position and facing");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Object.DestroyImmediate(playerFixture);
            }
        }
    }
}
