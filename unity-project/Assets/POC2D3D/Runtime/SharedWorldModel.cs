using System;
using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    public sealed class SharedObjectInstance
    {
        public string InstanceId;
        public string DefinitionId;
        public WorldObjectDefinition Definition;
        public Vector2Int RootCell;
        public string SignText;

        public IEnumerable<Vector2Int> BlockedCells()
        {
            foreach (var offset in Definition.EffectiveBlockedOffsets()) yield return RootCell + offset;
        }
    }

    // Presentation-free map state built from the existing MapDefinition cache (Map JSON v4).
    // Blocking comes from MapPlacementRules, the same data rule the 2D runtime and editor use,
    // instead of Physics colliders, so the 2D and 3D views cannot disagree about collision.
    public sealed class SharedWorldModel
    {
        private readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        private readonly List<SharedObjectInstance> objects = new List<SharedObjectInstance>();

        public MapDefinition Map { get; }
        public string MapId => Map.MapId;
        public bool IsInterior => Map.MapType == "interior";
        public Vector2Int MinCell => Map.MinCell;
        public Vector2Int MaxCell => Map.MaxCell;
        public IReadOnlyList<SharedObjectInstance> Objects => objects;
        public IReadOnlyCollection<Vector2Int> BlockedCells => blocked;

        public SharedWorldModel(MapDefinition map)
        {
            Map = map != null ? map : throw new ArgumentNullException(nameof(map));
            var errors = MapPlacementRules.Validate(map);
            if (errors.Count != 0)
                throw new InvalidOperationException("Invalid map " + map.MapId + ": " + string.Join("; ", errors));
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (MapPlacementRules.BlocksMovement(map, cell)) blocked.Add(cell);
            }
            foreach (var placement in map.Objects)
                objects.Add(new SharedObjectInstance
                {
                    InstanceId = placement.InstanceId,
                    DefinitionId = placement.Definition.StableId,
                    Definition = placement.Definition,
                    RootCell = placement.RootCell,
                    SignText = placement.SignText
                });
        }

        public bool Contains(Vector2Int cell) => Map.Contains(cell);
        public bool IsBlocked(Vector2Int cell) => blocked.Contains(cell);
        public bool CanEnter(Vector2Int cell) => Contains(cell) && !IsBlocked(cell);

        public SharedObjectInstance FindInstance(string instanceId)
        {
            foreach (var instance in objects)
                if (instance.InstanceId == instanceId) return instance;
            return null;
        }

        public bool TryGetHouseDoor(out Vector2Int cell) => Map.TryGetHouseRoot(out cell);
        public bool TryGetMarker(string id, out Vector2Int cell) => Map.TryGetMarker(id, out cell);

        // Same selection as MapRuntimeLoader2D.CreateObject: authored Action Points for
        // multi-cell, non-examine, sign and well objects; otherwise the plain Examine rule.
        public static bool UsesAuthoredActions(WorldObjectDefinition definition) =>
            definition.ActionPoints.Count > 0 &&
            (definition.Footprint.x > 1 || definition.Footprint.y > 1 ||
             definition.Behavior == WorldObjectBehavior.None ||
             definition.StableId == "sign_basic" || definition.StableId == "well_basic");

        public bool TryInteract(SharedObjectInstance target, Vector2Int playerCell,
            FacingDirection facing, bool playerMoving, out string message)
        {
            message = null;
            if (target == null || playerMoving) return false;
            var definition = target.Definition;
            if (UsesAuthoredActions(definition))
            {
                foreach (var point in definition.ActionPoints)
                {
                    if (playerCell != target.RootCell + point.PlayerCellOffset) continue;
                    if (facing != ToFacing(point.PlayerFacing)) continue;
                    message = point.ActionType == WorldActionType.Sleep ? "ベッド。" :
                        point.ActionType == WorldActionType.Sit ? "すわる。" :
                        !string.IsNullOrWhiteSpace(target.SignText) ? target.SignText :
                        !string.IsNullOrWhiteSpace(point.InteractionText) ? point.InteractionText :
                        definition.ExamineMessage;
                    return !string.IsNullOrEmpty(message);
                }
                return false;
            }
            if (definition.Behavior != WorldObjectBehavior.Examine) return false;
            var delta = target.RootCell - playerCell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1 || delta != facing.ToVector()) return false;
            message = definition.ExamineMessage;
            return !string.IsNullOrEmpty(message);
        }

        public SharedObjectInstance FindInteractionAhead(Vector2Int playerCell, FacingDirection facing,
            out string message)
        {
            message = null;
            foreach (var instance in objects)
                if (TryInteract(instance, playerCell, facing, false, out message)) return instance;
            return null;
        }

        private static FacingDirection ToFacing(WorldFacing facing) => facing switch
        {
            WorldFacing.Up => FacingDirection.Up,
            WorldFacing.Down => FacingDirection.Down,
            WorldFacing.Left => FacingDirection.Left,
            _ => FacingDirection.Right
        };
    }
}
