using System;
using System.IO;
using System.Linq;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.Interaction;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class MapAuthoringChecks
    {
        private const string OfficialPath = "Assets/Content/Maps/first_field.asset";
        private const string FixtureAssetPath = "Assets/Content/Maps/standalone_contract_fixture.asset";

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
                         ("bench", 64, 32), ("sign", 32, 64), ("well", 32, 32) })
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
                         ("sign_north", "sign", 1, WorldObjectBehavior.Examine),
                         ("sign_east", "sign", 1, WorldObjectBehavior.Examine),
                         ("sign_south", "sign", 1, WorldObjectBehavior.Examine),
                         ("sign_west", "sign", 1, WorldObjectBehavior.Examine),
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
            var sign = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_north.asset");
            Check(sign.ActionPoints[0].InteractionText == "きた" &&
                sign.ActionPoints[0].PlayerFacing == WorldFacing.Up &&
                sign.ExamineMessage == "きた", "sign examine text is imported");
            ValidateRuntimeSpriteFixtures();
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
            var ids = new[] { "cushion_basic", "desk_basic", "bench_basic", "sign_north", "well_basic" };
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
                        InstanceId = "fixture_" + id, RootCell = cells[index], Definition = definitions[index]
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
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testRoot);
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }
    }
}
