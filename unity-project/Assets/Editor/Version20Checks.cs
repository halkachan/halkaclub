using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.CameraControl;
using Halka.Game.Core;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    // Game regression checks retained for later game versions.
    public static class Version20Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver2.0 scene")]
        public static void Run()
        {
            MapAuthoringImporter.SyncAll();
            ProjectBuilder.PrepareScene();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var field = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/first_field.asset");
            var room = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/halka_house.asset");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var maps = UnityEngine.Object.FindFirstObjectByType<MapWorldController2D>();
            var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            var follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow2D>();
            var autoMode = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            var crow = UnityEngine.Object.FindFirstObjectByType<CrowWander2D>();
            var grass = UnityEngine.Object.FindFirstObjectByType<GrassField2D>();
            var playerMask = UnityEngine.Object.FindFirstObjectByType<PlayerGrassOcclusion>();
            var footsteps = UnityEngine.Object.FindFirstObjectByType<PlayerFootstepAudio>();
            Check((GameVersion.Value == "2.0" || GameVersion.Value == "2.1" || GameVersion.Value == "2.2") &&
                GameVersion.Label == "ver" + GameVersion.Value, "supported game version");
            Check(field != null && room != null && maps != null && area != null && world != null,
                "both maps and runtime controller exist");
            Check(field.MapId == "first_field" && room.MapId == "halka_house" &&
                field.DataVersion == 4 && room.DataVersion == 4 &&
                field.MapType == "outdoor" && room.MapType == "interior" &&
                field.GrassMode == "auto" && room.GrassMode == "none" &&
                room.BaseSurface.StableId == "house_floor", "map identities, base and grass policy");
            Check(MapPlacementRules.Validate(field).Count == 0 &&
                MapPlacementRules.Validate(room).Count == 0, "both authoring maps validate");
            Check(field.TryGetHouseRoot(out var door) && door == new Vector2Int(-7, -4) &&
                field.OutsideEntryCell == new Vector2Int(-7, -5) &&
                field.PlayerSpawnCell == Vector2Int.zero, "house root derives the entry and spawn remains");
            var housePlacement = field.Objects.Single(item => item.Definition.Behavior == WorldObjectBehavior.HouseTransition);
            Check(housePlacement.Definition.PreviewSprite.rect.size == new Vector2(160, 128) &&
                housePlacement.Definition.EffectiveBlockedOffsets().Count() == 9 &&
                !housePlacement.Definition.EffectiveBlockedOffsets().Contains(Vector2Int.zero),
                "house visual and irregular nine-cell footprint");
            var insideEntry = Vector2Int.zero;
            var insideExit = Vector2Int.zero;
            Check(room.MinCell == new Vector2Int(-6, -4) && room.MaxCell == new Vector2Int(6, 4) &&
                room.Surfaces.Count == 50 && room.Surfaces.All(item => item.Definition.BlocksMovement) &&
                room.TryGetMarker("interior_entry", out insideEntry) && insideEntry == new Vector2Int(0, -3) &&
                room.TryGetMarker("interior_exit", out insideExit) && insideExit == new Vector2Int(0, -4) &&
                room.SurfaceAt(insideExit) == null, "interior data matches the former 13x9 room");
            var bed = room.Objects.Single(item => item.Definition.StableId == "bed_basic");
            Check(bed.RootCell == new Vector2Int(-5, 0) &&
                bed.Definition.PreviewSprite.rect.size == new Vector2(64, 96) &&
                bed.Definition.EffectiveBlockedOffsets().Count() == 6,
                "bed stays in its two-by-three footprint");
            Check(crow != null && crow.GetComponent<ExamineInteractable>() != null &&
                crow.GetComponent<CrowGrassOcclusion>() != null &&
                crow.GetComponent<InteractionAudio>() != null &&
                crow.GetComponent<GridObstacle>() == null,
                "crow movement, interaction and voice remain configured");
            Check(playerMask != null && footsteps != null && grass != null &&
                GridWorld2D.TilePixels == 32 &&
                Mathf.Approximately(GridWorld2D.TileWorldSize, 0.5f) &&
                player.transform.Find("Player artwork").localPosition == new Vector3(0f, 0.25f),
                "player placement, grass mask and footsteps remain configured");
            var exteriorLoader = UnityEngine.Object.FindObjectsByType<MapRuntimeLoader2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Single(item => item.Map == field);
            exteriorLoader.Build();
            Check(world.MinCell == field.MinCell && world.MaxCell == field.MaxCell &&
                world.CanEnter(door) && !world.CanEnter(door + Vector2Int.left) &&
                !grass.HasGrass(door + Vector2Int.left), "outdoor runtime uses the house footprint");
            Check(field.Objects.Where(item => item.Definition.Behavior == WorldObjectBehavior.Examine)
                .All(item => !world.CanEnter(item.RootCell)), "stone, flower and tree remain blocked");
            Check(autoMode.CanAutoEnter(door) == false && AutoModeController.IdleSeconds == 60f,
                "AUTO remains outdoors and avoids the house entrance");

            var interiorGrass = (GrassField2D)Field(area, "interiorGrass");
            Check(interiorGrass != null &&
                (GrassField2D)Field(area, "exteriorGrass") == grass,
                "both areas have separate grass fields");

            typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(area, null);
            player.TeleportTo(field.OutsideEntryCell);
            area.Enter();
            var interiorRoot = (GameObject[])Field(maps, "roots");
            Check(area.IsInside && maps.ActiveMapId == "halka_house" &&
                interiorRoot[1].activeSelf && !interiorRoot[0].activeSelf &&
                player.Cell == insideEntry && world.MinCell == room.MinCell &&
                world.MaxCell == room.MaxCell &&
                (GrassField2D)Field(playerMask, "grassField") == interiorGrass,
                "entry activates indoor Map ID, spawn and grass mask source");
            Check(!world.CanEnter(bed.RootCell) && !world.CanEnter(new Vector2Int(-6, 0)) &&
                world.CanEnter(insideExit), "bed, wall and passage collision follow MapData");
            Check(follow.transform.position == follow.TargetPosition,
                "entry camera snaps immediately");
            area.Exit();
            Check(!area.IsInside && maps.ActiveMapId == "first_field" &&
                interiorRoot[0].activeSelf && !interiorRoot[1].activeSelf &&
                player.Cell == field.OutsideEntryCell && world.MinCell == field.MinCell &&
                follow.transform.position == follow.TargetPosition &&
                (GrassField2D)Field(playerMask, "grassField") == grass,
                "exit returns to the house root-derived entry and snaps camera");
            Check(grass.HasGrass(new Vector2Int(0, 0)) ==
                MapPlacementRules.HasGrass(field, new Vector2Int(0, 0)),
                "outdoor grass is rebuilt after return");
            Check(maps.GetMap("first_field") == field && maps.GetMap("halka_house") == room,
                "map registry resolves stable IDs");
            Debug.Log("HALKA ver2.0 regression checks with Map Editor v0.6 passed.");
        }

        [MenuItem("HALKA/Validate v0.3 moved house fixture")]
        public static void RunMovedHouseFixture()
        {
            MapAuthoringImporter.SyncAll();
            ProjectBuilder.PrepareScene();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var official = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/first_field.asset");
            var shifted = UnityEngine.Object.Instantiate(official);
            var shiftedDoor = new Vector2Int(-6, -4);
            var objects = official.Objects.Select(item =>
                item.Definition.Behavior == WorldObjectBehavior.HouseTransition
                    ? new WorldObjectPlacement { InstanceId = item.InstanceId,
                        Definition = item.Definition, RootCell = shiftedDoor }
                    : item).ToList();
            shifted.ReplaceFromAuthoring(official.MapId, official.DisplayName, official.MapType,
                official.BaseSurface, official.GrassMode, official.BackdropColor,
                official.MinCell, official.MaxCell, official.Surfaces.ToList(), objects,
                official.Markers.ToList(), official.EntitySpawns.ToList());
            try
            {
                Check(MapPlacementRules.Validate(shifted).Count == 0,
                    "moved house fixture validates without editing the official map");
                var controller = UnityEngine.Object.FindFirstObjectByType<MapWorldController2D>();
                var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
                var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
                var grass = UnityEngine.Object.FindFirstObjectByType<GrassField2D>();
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
                var loader = UnityEngine.Object.FindObjectsByType<MapRuntimeLoader2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(item => item.Map == official);
                var registry = (MapDefinition[])Field(controller, "maps");
                registry[0] = shifted;
                loader.SetMap(shifted);
                loader.Build();
                var objectRoot = (Transform)Field(loader, "objectRoot");
                Check(objectRoot.Cast<Transform>().Any(root =>
                    root.position == world.CellToWorld(shiftedDoor) &&
                    root.GetComponentInChildren<SpriteRenderer>().sprite.rect.size == new Vector2(160, 128)),
                    "house artwork moves with the authored root");
                Check(shifted.HouseDoorCell == shiftedDoor &&
                    shifted.OutsideEntryCell == shiftedDoor + Vector2Int.down &&
                    world.CanEnter(shiftedDoor) &&
                    !world.CanEnter(shiftedDoor + Vector2Int.left) &&
                    !world.CanEnter(new Vector2Int(-4, -4)) &&
                    world.CanEnter(new Vector2Int(-9, -4)),
                    "door and irregular nine-cell collision move together");
                for (var y = shifted.MinCell.y; y <= shifted.MaxCell.y; y++)
                for (var x = shifted.MinCell.x; x <= shifted.MaxCell.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Check(grass.HasGrass(cell) == MapPlacementRules.HasGrass(shifted, cell),
                        "moved house grass is rebuilt from the fixture map");
                }
                typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(area, null);
                player.TeleportTo(shifted.OutsideEntryCell);
                area.Enter();
                Check(area.IsInside && controller.ActiveMapId == "halka_house",
                    "shifted house enters the interior");
                area.Exit();
                Check(!area.IsInside && controller.ActiveMapId == "first_field" &&
                    player.Cell == shifted.OutsideEntryCell,
                    "exit returns to the shifted house entrance");
                Debug.Log("HALKA v0.3 moved house runtime fixture passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shifted);
                EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            }
        }

        [MenuItem("HALKA/Build v0.3 moved house fixture")]
        public static void BuildMovedHouseFixture()
        {
            MapAuthoringImporter.SyncAll();
            const string scenePath = "Assets/Scenes/V03MovedHouseFixture.unity";
            const string mapPath = "Assets/Editor/V03MovedHouseFixture.asset";
            Check(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath) == null &&
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(mapPath) == null,
                "moved house temporary assets do not already exist");
            var official = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/first_field.asset");
            var shifted = UnityEngine.Object.Instantiate(official);
            var objects = official.Objects.Select(item =>
                item.Definition.Behavior == WorldObjectBehavior.HouseTransition
                    ? new WorldObjectPlacement { InstanceId = item.InstanceId,
                        Definition = item.Definition, RootCell = new Vector2Int(-6, -4) }
                    : item).ToList();
            shifted.ReplaceFromAuthoring(official.MapId, official.DisplayName, official.MapType,
                official.BaseSurface, official.GrassMode, official.BackdropColor,
                official.MinCell, official.MaxCell, official.Surfaces.ToList(), objects,
                official.Markers.ToList(), official.EntitySpawns.ToList());
            Check(MapPlacementRules.Validate(shifted).Count == 0, "moved house test map validates");
            try
            {
                AssetDatabase.CreateAsset(shifted, mapPath);
                Check(AssetDatabase.CopyAsset("Assets/Scenes/FirstDay.unity", scenePath),
                    "temporary scene copied");
                var scene = EditorSceneManager.OpenScene(scenePath);
                var controller = UnityEngine.Object.FindFirstObjectByType<MapWorldController2D>();
                var loader = UnityEngine.Object.FindObjectsByType<MapRuntimeLoader2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(item => item.Map == official);
                ((MapDefinition[])Field(controller, "maps"))[0] = shifted;
                loader.SetMap(shifted);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(loader);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                var output = Path.Combine(Path.GetTempPath(),
                    "HalkaV03HouseMoveWebGL-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { scenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                Check(report.summary.result == BuildResult.Succeeded,
                    "moved house WebGL test build succeeded");
                Debug.Log("HALKA v0.3 moved house WebGL fixture: " + output);
            }
            finally
            {
                EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
                AssetDatabase.DeleteAsset(scenePath);
                AssetDatabase.DeleteAsset(mapPath);
                AssetDatabase.SaveAssets();
            }
        }

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Version20Checks failed: " + name);
        }
    }
}
