using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version26Checks
    {
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver2.6 scene")]
        public static void Run()
        {
            MapAuthoringImporter.SyncAll();
            Version254Checks.Run();
            Check(GameVersion.Value == "2.6", "game version");
            var field = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/first_field.asset");
            var room = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/halka_house.asset");
            var definition = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/well_basic.asset");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/well.png");
            var importer = AssetImporter.GetAtPath("Assets/Content/World/well.png") as TextureImporter;
            Check(field != null && room != null && field.DataVersion == 4 && room.DataVersion == 4,
                "Map v4 remains in use");
            Check(definition != null && sprite != null && importer != null &&
                definition.PreviewSprite == sprite && sprite.rect.size == new Vector2(32, 32) &&
                definition.Footprint == Vector2Int.one && definition.BlocksMovement &&
                definition.EffectiveBlockedOffsets().SequenceEqual(new[] { Vector2Int.zero }) &&
                definition.Behavior == WorldObjectBehavior.Examine &&
                definition.ExamineMessage == "いど。" &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                Mathf.Approximately(importer.spritePixelsPerUnit, 64f),
                "well uses its existing pixel art and one blocked cell");
            Check(definition.ActionPoints.Count == 1 &&
                definition.ActionPoints[0].Id == "examine_front" &&
                definition.ActionPoints[0].PlayerCellOffset == Vector2Int.down &&
                definition.ActionPoints[0].PlayerFacing == WorldFacing.Up &&
                definition.ActionPoints[0].ActionType == WorldActionType.Examine &&
                definition.ActionPoints[0].InteractionText == "いど。",
                "authored front examine point");

            var wells = field.Objects.Where(item => item.Definition == definition).ToArray();
            Check(wells.Length == 1 && wells[0].RootCell == new Vector2Int(-2, -2) &&
                wells[0].InstanceId == "obj_well_basic_v26_first_field" &&
                room.Objects.All(item => item.Definition != definition),
                "exactly one authored outdoor well");
            Check(MapPlacementRules.Validate(field).Count == 0 &&
                MapPlacementRules.Validate(room).Count == 0 &&
                field.Objects.Concat(room.Objects).Select(item => item.InstanceId).Distinct().Count() ==
                field.Objects.Count + room.Objects.Count, "formal maps have no overlap or duplicate IDs");
            var rootCell = wells[0].RootCell;
            var actionCell = rootCell + definition.ActionPoints[0].PlayerCellOffset;
            var reachable = Reachable(field, field.PlayerSpawnCell);
            Check(MapPlacementRules.BlocksMovement(field, rootCell) &&
                !MapPlacementRules.BlocksMovement(field, actionCell) &&
                reachable.Contains(actionCell) && reachable.Contains(field.OutsideEntryCell) &&
                reachable.Contains(field.NorthRoadEnd) && reachable.Contains(field.EastRoadEnd) &&
                reachable.Contains(field.SouthRoadEnd) && reachable.Contains(field.WestRoadEnd),
                "well front and all existing routes remain accessible");
            Check(field.TryGetEntitySpawn("crow_main", out var crow) &&
                rootCell.x < crow.Cell.x + crow.Definition.WanderMinimum.x &&
                field.TryGetEntitySpawn("player_main", out var playerSpawn) &&
                playerSpawn.Cell != rootCell && playerSpawn.Cell != actionCell,
                "well avoids both actor spawns and crow patrol");
            CheckRuntime(definition, rootCell, actionCell);
            Debug.Log("HALKA ver2.6 well placement, interaction, collision and inherited checks passed.");
        }

        private static HashSet<Vector2Int> Reachable(MapDefinition map, Vector2Int start)
        {
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (map.Contains(next) && !MapPlacementRules.BlocksMovement(map, next) &&
                        visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }

        private static void CheckRuntime(WorldObjectDefinition definition, Vector2Int rootCell,
            Vector2Int actionCell)
        {
            var fixture = new GameObject("ver2.6 well runtime fixture");
            try
            {
                var world = fixture.AddComponent<GridWorld2D>();
                var player = fixture.AddComponent<PlayerMover>();
                var hud = fixture.AddComponent<GameHud>();
                var objectRoot = new GameObject("Objects").transform;
                objectRoot.SetParent(fixture.transform, false);
                var loader = fixture.AddComponent<MapRuntimeLoader2D>();
                var playerData = new SerializedObject(player);
                playerData.FindProperty("world").objectReferenceValue = world;
                playerData.ApplyModifiedPropertiesWithoutUndo();
                typeof(PlayerMover).GetMethod("Awake", Hidden).Invoke(player, null);
                var loaderData = new SerializedObject(loader);
                loaderData.FindProperty("world").objectReferenceValue = world;
                loaderData.FindProperty("objectRoot").objectReferenceValue = objectRoot;
                loaderData.FindProperty("player").objectReferenceValue = player;
                loaderData.FindProperty("hud").objectReferenceValue = hud;
                loaderData.ApplyModifiedPropertiesWithoutUndo();
                var placement = new WorldObjectPlacement { InstanceId = "well_runtime_fixture",
                    Definition = definition, RootCell = rootCell };
                typeof(MapRuntimeLoader2D).GetMethod("CreateObject", Hidden)
                    .Invoke(loader, new object[] { placement, 1 });
                var well = objectRoot.GetChild(0).GetComponent<WorldObjectActionInteractable>();
                var art = objectRoot.GetChild(0).GetComponentInChildren<SpriteRenderer>();
                Check(well != null && art != null && art.sprite == definition.PreviewSprite &&
                    well.CanInteractAt(actionCell, FacingDirection.Up) &&
                    !well.CanInteractAt(actionCell, FacingDirection.Left),
                    "runtime imports exact sprite and authored action point");
                player.TeleportTo(actionCell);
                player.SetFacing(FacingDirection.Up);
                var steps = 0;
                player.StepCompleted += _ => steps++;
                Check(!world.CanEnter(rootCell) && !player.TryStep(Vector2Int.up) &&
                    player.Cell == actionCell && steps == 0,
                    "blocked well cell cannot complete a step or increment Life Log");
                var route = new List<Vector2Int>();
                Check(!GridPathfinder2D.TryFind(world, actionCell, rootCell, route) &&
                    GridPathfinder2D.TryFind(world, actionCell, rootCell + Vector2Int.right, route) &&
                    !route.Contains(rootCell), "AUTO pathfinding goes around the well");
                well.Interact();
                var message = (TimedMessage)typeof(GameHud).GetField("message", Hidden).GetValue(hud);
                Check(message.TextAt(Time.unscaledTime) == "いど。" && steps == 0 &&
                    player.Cell == actionCell,
                    "well examine uses the existing message without a step");
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version26Checks failed: " + description);
        }
    }
}
