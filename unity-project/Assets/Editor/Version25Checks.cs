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
    public static class Version25Checks
    {
        [MenuItem("HALKA/Validate ver2.5 scene")]
        public static void Run()
        {
            Version24Checks.Run();
            Check(GameVersion.Value == "2.5" || GameVersion.Value == "2.5.1", "game version");
            var field = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/first_field.asset");
            var room = AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/halka_house.asset");
            Check(field != null && room != null && field.DataVersion == 4 && room.DataVersion == 4,
                "formal Map v4 assets");
            Check(MapPlacementRules.Validate(field).Count == 0 && MapPlacementRules.Validate(room).Count == 0,
                "formal maps validate without overlaps");
            var placements = field.Objects.Concat(room.Objects).ToArray();
            Check(placements.All(item => !string.IsNullOrWhiteSpace(item.InstanceId)) &&
                placements.Select(item => item.InstanceId).Distinct().Count() == placements.Length,
                "unique object instance IDs");
            var bench = ExactlyOne(field, "bench_basic");
            var sign = ExactlyOne(field, "sign_basic");
            var cushion = ExactlyOne(room, "cushion_basic");
            var desk = ExactlyOne(room, "desk_basic");
            Check(!string.IsNullOrWhiteSpace(sign.SignText) &&
                sign.Definition.Footprint == Vector2Int.one &&
                sign.Definition.EffectiveBlockedOffsets().SequenceEqual(new[] { Vector2Int.zero }) &&
                sign.Definition.ActionPoints.Count == 1 &&
                sign.Definition.ActionPoints[0].Id == "read_front" &&
                sign.Definition.ActionPoints[0].ActionType == WorldActionType.Examine &&
                sign.Definition.ActionPoints[0].PlayerFacing == WorldFacing.Up,
                "one-cell sign and instance text");
            Check(!cushion.Definition.BlocksMovement &&
                !MapPlacementRules.BlocksMovement(room, cushion.RootCell) &&
                desk.Definition.BlocksMovement &&
                MapPlacementRules.BlocksMovement(room, desk.RootCell) &&
                MapPlacementRules.BlocksMovement(room, desk.RootCell + Vector2Int.right) &&
                desk.Definition.ActionPoints[0].InteractionText == "つくえ。",
                "cushion passage and desk examine metadata");
            var fieldReachable = Reachable(field, field.PlayerSpawnCell);
            var roomReachable = Reachable(room, Marker(room, "interior_entry"));
            Check(fieldReachable.Contains(field.OutsideEntryCell) &&
                fieldReachable.Contains(field.HouseDoorCell) &&
                fieldReachable.Contains(field.NorthRoadEnd) &&
                fieldReachable.Contains(field.EastRoadEnd) &&
                fieldReachable.Contains(field.SouthRoadEnd) &&
                fieldReachable.Contains(field.WestRoadEnd), "outdoor routes and house entry remain reachable");
            Check(roomReachable.Contains(Marker(room, "interior_exit")) &&
                roomReachable.Contains(cushion.RootCell), "interior exit and cushion reachable");
            foreach (var point in bench.Definition.ActionPoints)
                Check(fieldReachable.Contains(bench.RootCell + point.PlayerCellOffset) &&
                    !MapPlacementRules.BlocksMovement(field, bench.RootCell + point.PlayerCellOffset),
                    "both bench seats have reachable action cells");
            Check(fieldReachable.Contains(sign.RootCell + sign.Definition.ActionPoints[0].PlayerCellOffset) &&
                roomReachable.Contains(desk.RootCell + desk.Definition.ActionPoints[0].PlayerCellOffset),
                "sign and desk action cells reachable");
            Check(field.TryGetEntitySpawn("crow_main", out var crow) &&
                sign.RootCell.x < crow.Cell.x + crow.Definition.WanderMinimum.x &&
                bench.RootCell.x < crow.Cell.x + crow.Definition.WanderMinimum.x,
                "new outdoor objects outside crow patrol");
            Check(GameBitmapFont.HasAllGlyphs("ここは HALKA WORLD。") &&
                !GameBitmapFont.HasAllGlyphs("𠮷"), "sign primary glyphs and fallback route");
            CheckSignRuntime(sign.Definition);
            Debug.Log("HALKA ver2.5 formal placements, sign text and inherited regression checks passed.");
        }

        private static WorldObjectPlacement ExactlyOne(MapDefinition map, string id)
        {
            var matches = map.Objects.Where(item => item.Definition != null &&
                item.Definition.StableId == id).ToArray();
            Check(matches.Length == 1, "one formal " + id + " in " + map.MapId);
            return matches[0];
        }

        private static Vector2Int Marker(MapDefinition map, string id)
        {
            Check(map.TryGetMarker(id, out var cell), "marker " + id);
            return cell;
        }

        private static HashSet<Vector2Int> Reachable(MapDefinition map, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            if (!map.Contains(start) || MapPlacementRules.BlocksMovement(map, start)) return seen;
            seen.Add(start);
            pending.Enqueue(start);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (map.Contains(next) && !MapPlacementRules.BlocksMovement(map, next) && seen.Add(next))
                        pending.Enqueue(next);
                }
            }
            return seen;
        }

        private static void CheckSignRuntime(WorldObjectDefinition definition)
        {
            var fixture = ScriptableObject.CreateInstance<MapDefinition>();
            var root = new GameObject("ver2.5 sign fixture");
            root.transform.position = new Vector3(100f, 100f);
            try
            {
                fixture.ReplaceFromAuthoring("v25_sign_fixture", "Sign fixture", "outdoor",
                    null, "none", Color.black, new Vector2Int(-3, -3), new Vector2Int(3, 3),
                    new List<SurfacePlacement>(), new List<WorldObjectPlacement> {
                        new WorldObjectPlacement { InstanceId = "sign_a", RootCell = Vector2Int.zero,
                            Definition = definition, SignText = "ここは HALKA WORLD。" },
                        new WorldObjectPlacement { InstanceId = "sign_b", RootCell = new Vector2Int(2, 0),
                            Definition = definition, SignText = "このさきは もり。" }
                    }, new List<LockedMapMarker>());
                var world = root.AddComponent<GridWorld2D>();
                var player = root.AddComponent<PlayerMover>();
                var renderer = root.AddComponent<SpriteRenderer>();
                var hud = root.AddComponent<GameHud>();
                var surfaces = root.AddComponent<GroundSurfaceField2D>();
                var surfaceRoot = new GameObject("Surfaces").transform;
                surfaceRoot.SetParent(root.transform, false);
                var objectRoot = new GameObject("Objects").transform;
                objectRoot.SetParent(root.transform, false);
                var loader = root.AddComponent<MapRuntimeLoader2D>();
                var serialized = new SerializedObject(loader);
                serialized.FindProperty("map").objectReferenceValue = fixture;
                serialized.FindProperty("world").objectReferenceValue = world;
                serialized.FindProperty("surfaceField").objectReferenceValue = surfaces;
                serialized.FindProperty("surfaceRoot").objectReferenceValue = surfaceRoot;
                serialized.FindProperty("objectRoot").objectReferenceValue = objectRoot;
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.FindProperty("playerRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("hud").objectReferenceValue = hud;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var playerData = new SerializedObject(player);
                playerData.FindProperty("world").objectReferenceValue = world;
                playerData.ApplyModifiedPropertiesWithoutUndo();
                loader.Build();
                Check(objectRoot.childCount == 2, "two runtime sign instances");
                typeof(PlayerMover).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(player, null);
                for (var index = 0; index < 2; index++)
                {
                    var sign = objectRoot.GetChild(index).GetComponent<WorldObjectActionInteractable>();
                    var cell = new Vector2Int(index * 2, -1);
                    Check(sign != null && sign.CanInteractAt(cell, FacingDirection.Up) &&
                        !sign.CanInteractAt(new Vector2Int(index * 2 + 1, 0), FacingDirection.Left),
                        "sign only reads from authored front point");
                    player.TeleportTo(cell);
                    player.SetFacing(FacingDirection.Up);
                    sign.Interact();
                    var message = (TimedMessage)typeof(GameHud).GetField("message",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                    Check(message.TextAt(Time.unscaledTime) == (index == 0 ?
                        "ここは HALKA WORLD。" : "このさきは もり。"),
                        "each sign displays its own instance text");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Version25Checks failed: " + name);
        }
    }
}
