using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    // Shared by the runtime loader and the Editor preview; grass is derived, not stored.
    public static class MapPlacementRules
    {
        public static bool IsHouseFootprint(MapDefinition map, Vector2Int cell)
        {
            foreach (var placement in map.Objects)
                if (placement.Definition != null &&
                    placement.Definition.Behavior == WorldObjectBehavior.HouseTransition)
                    foreach (var offset in placement.Definition.EffectiveBlockedOffsets())
                        if (placement.RootCell + offset == cell) return true;
            return false;
        }

        public static bool IsProtected(MapDefinition map, Vector2Int cell) =>
            (map.TryGetHouseRoot(out var door) && (cell == door || cell == door + Vector2Int.down)) ||
            IsEntitySpawn(map, cell);

        public static bool IsEntitySpawn(MapDefinition map, Vector2Int cell)
        {
            foreach (var spawn in map.Markers)
                if (spawn.Cell == cell) return true;
            return false;
        }

        public static bool BlocksMovement(MapDefinition map, Vector2Int cell)
        {
            if (map.SurfaceAt(cell) != null && map.SurfaceAt(cell).BlocksMovement) return true;
            foreach (var placement in map.Objects)
            {
                var definition = placement.Definition;
                if (definition == null || !definition.BlocksMovement) continue;
                foreach (var offset in definition.EffectiveBlockedOffsets())
                    if (placement.RootCell + offset == cell) return true;
            }
            return false;
        }

        private static bool Covers(WorldObjectPlacement placement, Vector2Int cell)
        {
            foreach (var offset in placement.Definition.EffectiveBlockedOffsets())
                if (placement.RootCell + offset == cell) return true;
            return false;
        }

        public static bool HasGrass(MapDefinition map, Vector2Int cell)
        {
            if (map.GrassMode != "auto" || !map.Contains(cell) || map.SurfaceAt(cell) != null ||
                BlocksMovement(map, cell)) return false;
            foreach (var placement in map.Objects)
                if (placement.Definition != null && placement.Definition.ExcludesGrass &&
                    Covers(placement, cell)) return false;
            return true;
        }

        public static bool CanPlaceObject(MapDefinition map, Vector2Int cell,
            WorldObjectDefinition definition, out string reason, Vector2Int? ignore = null)
        {
            reason = null;
            if (definition == null) { reason = "Choose an Object Definition."; return false; }
            if (definition.PreviewSprite == null)
            { reason = "Definition footprint must be positive."; return false; }
            if (!map.Contains(cell)) { reason = "Outside map bounds."; return false; }
            if (IsProtected(map, cell) && definition.Behavior != WorldObjectBehavior.HouseTransition)
            { reason = "Protected House, spawn or road-end cell."; return false; }
            foreach (var placement in map.Objects)
                if (placement.RootCell == cell && placement.RootCell != ignore)
                {
                    reason = "Cell occupied by " +
                        (placement.Definition == null ? "an invalid object" : placement.Definition.DisplayName) + ".";
                    return false;
                }
            foreach (var offset in definition.EffectiveBlockedOffsets())
            {
                var occupied = cell + offset;
                var otherObject = false;
                foreach (var placement in map.Objects)
                    if (placement.Definition != null && placement.RootCell != ignore &&
                        Covers(placement, occupied)) otherObject = true;
                if (!map.Contains(occupied) || (IsProtected(map, occupied) &&
                    definition.Behavior != WorldObjectBehavior.HouseTransition) || otherObject ||
                    (map.SurfaceAt(occupied) != null && map.SurfaceAt(occupied).BlocksMovement))
                {
                    reason = "Footprint overlaps a protected cell, Surface or Object.";
                    return false;
                }
            }
            return true;
        }

        public static List<string> Validate(MapDefinition map)
        {
            var problems = new List<string>();
            if (map == null) { problems.Add("ERROR: No Map Definition selected."); return problems; }
            if (string.IsNullOrWhiteSpace(map.MapId)) problems.Add("ERROR: Map ID is empty.");
            if (map.MinCell.x > map.MaxCell.x || map.MinCell.y > map.MaxCell.y)
                problems.Add("ERROR: Invalid bounds.");
            var surfaceCells = new HashSet<Vector2Int>();
            foreach (var surface in map.Surfaces)
            {
                if (surface.Definition == null) problems.Add("ERROR: Null Surface Definition at " + surface.Cell);
                if (!map.Contains(surface.Cell)) problems.Add("ERROR: Surface out of bounds at " + surface.Cell);
                if (!surfaceCells.Add(surface.Cell)) problems.Add("ERROR: Duplicate Surface at " + surface.Cell);
            }
            var objectCells = new HashSet<Vector2Int>();
            var instanceIds = new HashSet<string>();
            var occupiedCells = new HashSet<Vector2Int>();
            foreach (var placement in map.Objects)
            {
                if (string.IsNullOrWhiteSpace(placement.InstanceId) ||
                    !instanceIds.Add(placement.InstanceId))
                    problems.Add("ERROR: Empty or duplicate instanceId at " + placement.RootCell);
                if (placement.Definition == null) problems.Add("ERROR: Null Object Definition at " + placement.RootCell);
                if (!map.Contains(placement.RootCell)) problems.Add("ERROR: Object out of bounds at " + placement.RootCell);
                if (!objectCells.Add(placement.RootCell)) problems.Add("ERROR: Duplicate Object at " + placement.RootCell);
                if (placement.Definition == null) continue;
                var footprint = placement.Definition.Footprint;
                if (footprint.x < 1 || footprint.y < 1)
                { problems.Add("ERROR: Invalid footprint at " + placement.RootCell); continue; }
                foreach (var offset in placement.Definition.EffectiveBlockedOffsets())
                {
                    var cell = placement.RootCell + offset;
                    if (!map.Contains(cell)) problems.Add("ERROR: Object footprint out of bounds at " + cell);
                    if (map.Markers != null && IsEntitySpawn(map, cell))
                        problems.Add("ERROR: Object blocks protected cell " + cell);
                    if (surfaceCells.Contains(cell) && map.SurfaceAt(cell).BlocksMovement)
                        problems.Add("ERROR: Object overlaps blocking Surface at " + cell);
                    if (!occupiedCells.Add(cell)) problems.Add("ERROR: Overlapping Object footprint at " + cell);
                }
            }
            if (map.MapType == "outdoor")
            {
                if (!map.TryGetHouseRoot(out var door) || !map.Contains(door + Vector2Int.down) ||
                    BlocksMovement(map, door) || BlocksMovement(map, door + Vector2Int.down))
                    problems.Add("ERROR: House door or outside entry blocked.");
            }
            foreach (var marker in map.Markers)
                if (BlocksMovement(map, marker.Cell)) problems.Add("ERROR: Protected marker blocked at " + marker.Cell);
            return problems;
        }
    }
}
