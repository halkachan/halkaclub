using System;
using System.Collections.Generic;
using System.Linq;
using Halka.Game.Core;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version27Checks
    {
        [MenuItem("HALKA/Validate ver2.7 scene")]
        public static void Run()
        {
            MapAuthoringImporter.SyncAll();
            Version26Checks.Run();
            Check(GameVersion.Value == "2.7", "game version");

            var field = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/first_field.asset");
            var house = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/halka_house.asset");
            Check(field != null && house != null && field.DataVersion == 4 && house.DataVersion == 4,
                "both authored maps remain v4");
            Check(field.AreaTransitions.Count == 0 && MapPlacementRules.Validate(field).Count == 0 &&
                MapPlacementRules.Validate(house).Count == 0, "no new transition or invalid placement");

            var all = field.Objects.Concat(house.Objects).ToArray();
            Check(all.All(item => item.Definition != null && !string.IsNullOrWhiteSpace(item.InstanceId)) &&
                all.Select(item => item.InstanceId).Distinct(StringComparer.Ordinal).Count() == all.Length,
                "object definitions and instance IDs");
            var newLandscaping = field.Objects.Where(item =>
                item.InstanceId.Contains("_v27_")).ToArray();
            Check(newLandscaping.Count(item => item.InstanceId.StartsWith("obj_tree_north_v27_", StringComparison.Ordinal) &&
                item.Definition.StableId == "tree_basic") >= 1 &&
                newLandscaping.Count(item => item.InstanceId.StartsWith("obj_flower_east_v27_", StringComparison.Ordinal) &&
                item.Definition.StableId == "flower_basic") >= 3 &&
                newLandscaping.Count(item => item.InstanceId.StartsWith("obj_stone_south_v27_", StringComparison.Ordinal) &&
                item.Definition.StableId == "stone_basic") >= 2 &&
                newLandscaping.Count(item => item.InstanceId.StartsWith("obj_flower_west_v27_", StringComparison.Ordinal) &&
                item.Definition.StableId == "flower_basic") >= 1,
                "four authored landscape themes use the existing definitions");

            var road = new HashSet<Vector2Int>(field.Surfaces.Where(item =>
                item.Definition != null && item.Definition.StableId == "dirt").Select(item => item.Cell));
            var accessible = Reachable(field, field.PlayerSpawnCell);
            var hasPlayer = field.TryGetEntitySpawn("player_main", out var player);
            var hasCrow = field.TryGetEntitySpawn("crow_main", out var crow);
            Check(hasPlayer && hasCrow &&
                player.Cell == field.PlayerSpawnCell &&
                !MapPlacementRules.BlocksMovement(field, player.Cell) &&
                !MapPlacementRules.BlocksMovement(field, crow.Cell), "player and crow spawns remain clear");
            foreach (var placement in newLandscaping)
            {
                Check(field.Contains(placement.RootCell) && !road.Contains(placement.RootCell) &&
                    placement.RootCell != player.Cell && placement.RootCell != crow.Cell,
                    "landscape roots avoid roads and spawns");
                var patrolMin = crow.Cell + crow.Definition.WanderMinimum;
                var patrolMax = crow.Cell + crow.Definition.WanderMaximum;
                Check(placement.RootCell.x < patrolMin.x || placement.RootCell.x > patrolMax.x ||
                    placement.RootCell.y < patrolMin.y || placement.RootCell.y > patrolMax.y,
                    "new obstacles avoid the crow patrol rectangle");
                foreach (var offset in placement.Definition.EffectiveBlockedOffsets())
                    Check(field.Contains(placement.RootCell + offset), "footprint stays inside the map");
                foreach (var point in placement.Definition.ActionPoints)
                    Check(accessible.Contains(placement.RootCell + point.PlayerCellOffset),
                        "new object's action cell is reachable");
            }

            Check(field.TryGetMarker("road_north", out var north) && north.y == field.MaxCell.y &&
                field.TryGetMarker("road_east", out var east) && east.x == field.MaxCell.x &&
                field.TryGetMarker("road_south", out var south) && south.y == field.MinCell.y &&
                field.TryGetMarker("road_west", out var west) && west.x == field.MinCell.x &&
                new[] { north, east, south, west }.Distinct().Count() == 4 &&
                accessible.Contains(north) && accessible.Contains(east) &&
                accessible.Contains(south) && accessible.Contains(west),
                "all four edge markers remain distinct and reachable");
            Check(accessible.Contains(field.OutsideEntryCell) && accessible.Contains(field.HouseDoorCell),
                "house entry remains reachable");
            foreach (var id in new[] { "bench_basic", "sign_basic", "well_basic" })
            {
                var placement = field.Objects.Single(item => item.Definition.StableId == id);
                foreach (var point in placement.Definition.ActionPoints)
                    Check(accessible.Contains(placement.RootCell + point.PlayerCellOffset),
                        id + " action point remains reachable");
            }
            foreach (var asset in new[] { "tree", "flower", "stone" })
            {
                var path = "Assets/Content/World/" + asset + ".png";
                var texture = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(texture != null && texture.filterMode == FilterMode.Point &&
                    !texture.mipmapEnabled &&
                    texture.textureCompression == TextureImporterCompression.Uncompressed &&
                    Mathf.Approximately(texture.spritePixelsPerUnit, 64f),
                    asset + " uses the existing pixel art import settings");
            }
            Debug.Log("HALKA ver2.7 landscape, routes and inherited regression checks passed.");
        }

        private static HashSet<Vector2Int> Reachable(MapDefinition map, Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var pending = new Queue<Vector2Int>();
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

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version27Checks failed: " + description);
        }
    }
}
