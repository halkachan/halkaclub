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
        private const string OfficialPath = "Assets/Content/Maps/first_field.asset";
        private const string FixtureAssetPath = "Assets/Content/Maps/standalone_contract_fixture.asset";

        [MenuItem("HALKA WORLD/Build v0.4 WebGL regression to Temp")]
        public static void BuildWebGLRegression()
        {
            ProjectBuilder.PrepareScene();
            var output = Path.Combine(Path.GetTempPath(), "HalkaMapEditorV04WebGLRegression");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/FirstDay.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            Check(report.summary.result == BuildResult.Succeeded,
                "WebGL regression build succeeded");
            Debug.Log("HALKA WORLD Map Editor v0.4 WebGL regression built to " + output);
        }

        [MenuItem("HALKA WORLD/Validate Standalone Map Contract v0.4")]
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
            var bed = interior.Objects.Single(item => item.Definition.StableId == "bed_basic").Definition;
            Check(bed.ActionPoints.Count == 1 && bed.ActionPoints[0].Id == "sleep_main" &&
                bed.ActionPoints[0].PlayerCellOffset == new Vector2Int(0, 1) &&
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
                Debug.Log("HALKA WORLD Standalone Map Contract v0.4: passed.");
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
                    Check(root.GetComponent<ExamineInteractable>() != null ==
                        (definitions[index].Behavior == WorldObjectBehavior.Examine),
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
    }
}
