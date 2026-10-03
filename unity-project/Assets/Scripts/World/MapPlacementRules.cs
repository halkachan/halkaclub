using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    // Shared by the runtime loader and the Editor preview; grass is derived, not stored.
    public static class MapPlacementRules
    {
        public static bool IsHouseFootprint(MapDefinition map, Vector2Int cell) =>
            cell.x >= map.HouseDoorCell.x - 2 && cell.x <= map.HouseDoorCell.x + 2 &&
            cell.y >= map.HouseDoorCell.y && cell.y <= map.HouseDoorCell.y + 1 &&
            cell != map.HouseDoorCell;

        public static bool IsProtected(MapDefinition map, Vector2Int cell) =>
            IsHouseFootprint(map, cell) || cell == map.HouseDoorCell ||
            cell == map.OutsideEntryCell || cell == map.NorthRoadEnd ||
            cell == map.EastRoadEnd || cell == map.SouthRoadEnd ||
            cell == map.WestRoadEnd || cell == map.PlayerSpawnCell ||
            IsEntitySpawn(map, cell);

        public static bool IsEntitySpawn(MapDefinition map, Vector2Int cell)
        {
            foreach (var spawn in map.EntitySpawns)
                if (spawn.Cell == cell) return true;
            return false;
        }

        public static bool BlocksMovement(MapDefinition map, Vector2Int cell)
        {
            if (IsHouseFootprint(map, cell)) return true;
            foreach (var placement in map.Objects)
            {
                var definition = placement.Definition;
                if (definition == null || !definition.BlocksMovement) continue;
                for (var y = 0; y < definition.Footprint.y; y++)
                for (var x = 0; x < definition.Footprint.x; x++)
                    if (placement.RootCell + new Vector2Int(x, y) == cell) return true;
            }
            return false;
        }

        private static bool Covers(WorldObjectPlacement placement, Vector2Int cell)
        {
            var footprint = placement.Definition.Footprint;
            return cell.x >= placement.RootCell.x &&
                cell.x < placement.RootCell.x + footprint.x &&
                cell.y >= placement.RootCell.y &&
                cell.y < placement.RootCell.y + footprint.y;
        }

        public static bool HasGrass(MapDefinition map, Vector2Int cell)
        {
            if (!map.Contains(cell) || map.SurfaceAt(cell) != null ||
                BlocksMovement(map, cell)) return false;
            foreach (var placement in map.Objects)
                if (placement.Definition != null && placement.Definition.ExcludesGrass &&
                    placement.RootCell == cell) return false;
            return true;
        }

        public static bool CanPlaceObject(MapDefinition map, Vector2Int cell,
            WorldObjectDefinition definition, out string reason, Vector2Int? ignore = null)
        {
            reason = null;
            if (definition == null) { reason = "Choose an Object Definition."; return false; }
            if (definition.Footprint.x < 1 || definition.Footprint.y < 1)
            { reason = "Definition footprint must be positive."; return false; }
            if (!map.Contains(cell)) { reason = "Outside map bounds."; return false; }
            if (IsProtected(map, cell)) { reason = "Protected House, spawn or road-end cell."; return false; }
            foreach (var placement in map.Objects)
                if (placement.RootCell == cell && placement.RootCell != ignore)
                {
                    reason = "Cell occupied by " +
                        (placement.Definition == null ? "an invalid object" : placement.Definition.DisplayName) + ".";
                    return false;
                }
            for (var y = 0; y < definition.Footprint.y; y++)
            for (var x = 0; x < definition.Footprint.x; x++)
            {
                var occupied = cell + new Vector2Int(x, y);
                var otherObject = false;
                foreach (var placement in map.Objects)
                    if (placement.Definition != null && placement.RootCell != ignore &&
                        Covers(placement, occupied)) otherObject = true;
                if (!map.Contains(occupied) || IsProtected(map, occupied) || otherObject)
                {
                    reason = "Footprint overlaps a protected or occupied cell.";
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
            var occupiedCells = new HashSet<Vector2Int>();
            foreach (var placement in map.Objects)
            {
                if (placement.Definition == null) problems.Add("ERROR: Null Object Definition at " + placement.RootCell);
                if (!map.Contains(placement.RootCell)) problems.Add("ERROR: Object out of bounds at " + placement.RootCell);
                if (!objectCells.Add(placement.RootCell)) problems.Add("ERROR: Duplicate Object at " + placement.RootCell);
                if (placement.Definition == null) continue;
                var footprint = placement.Definition.Footprint;
                if (footprint.x < 1 || footprint.y < 1)
                { problems.Add("ERROR: Invalid footprint at " + placement.RootCell); continue; }
                for (var y = 0; y < footprint.y; y++)
                for (var x = 0; x < footprint.x; x++)
                {
                    var cell = placement.RootCell + new Vector2Int(x, y);
                    if (!map.Contains(cell)) problems.Add("ERROR: Object footprint out of bounds at " + cell);
                    if (IsProtected(map, cell)) problems.Add("ERROR: Object blocks protected cell " + cell);
                    if (!occupiedCells.Add(cell)) problems.Add("ERROR: Overlapping Object footprint at " + cell);
                }
            }
            if (!map.Contains(map.HouseDoorCell) || !map.Contains(map.OutsideEntryCell))
                problems.Add("ERROR: House entry out of bounds.");
            foreach (var cell in new[] { map.HouseDoorCell, map.OutsideEntryCell,
                map.NorthRoadEnd, map.EastRoadEnd, map.SouthRoadEnd, map.WestRoadEnd })
                if (BlocksMovement(map, cell)) problems.Add("ERROR: Protected entry or road end blocked at " + cell);
            return problems;
        }
    }
}
