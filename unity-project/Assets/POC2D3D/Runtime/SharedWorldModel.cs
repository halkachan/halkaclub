using System;
using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Game.World;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    public sealed class SharedObjectInstance
    {
        public string InstanceId;
        public string DefinitionId;
        public WorldObjectDefinition Definition;
        public Vector2Int RootCell;   // world cell outdoors (ver3.2), Map cell indoors
        public string SignText;
        // ver3.2: which Map the Object was authored in, its cell there, and its place in that Map's
        // list (the 2D outline's object id and the first-answers rule keep the ver3.1 order).
        public string RegionMapId;
        public Vector2Int LocalRootCell;
        public int OrderInMap;

        public IEnumerable<Vector2Int> BlockedCells()
        {
            foreach (var offset in Definition.EffectiveBlockedOffsets()) yield return RootCell + offset;
        }
    }

    public enum SharedActionKind { None, Message, Sit }

    // Presentation-free map state built from the existing MapDefinition cache (Map JSON v4).
    // Blocking comes from MapPlacementRules, the same data rule the 2D runtime and editor use,
    // instead of Physics colliders, so the 2D and 3D views cannot disagree about collision.
    // ver3.2: the outdoor world is one SharedWorldModel over an OutdoorWorldIndex (world cells,
    // every region); interiors keep one Map and Map cells. MapId of the outdoor world is its primary
    // region's id (first_field), which is what saves, AUTO and the night overlay have always used.
    public sealed class SharedWorldModel
    {
        private readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        private readonly List<SharedObjectInstance> objects = new List<SharedObjectInstance>();

        public MapDefinition Map { get; }
        public OutdoorWorldIndex OutdoorIndex { get; }
        public bool IsOutdoorWorld => OutdoorIndex != null;
        public string MapId => Map.MapId;
        public string WorldId => OutdoorIndex != null ? OutdoorIndex.WorldId : Map.MapId;
        public bool IsInterior => Map.MapType == "interior";
        public Vector2Int MinCell => OutdoorIndex != null ? OutdoorIndex.Bounds.Min : Map.MinCell;
        public Vector2Int MaxCell => OutdoorIndex != null ? OutdoorIndex.Bounds.Max : Map.MaxCell;
        public IReadOnlyList<SharedObjectInstance> Objects => OutdoorIndex != null ? OutdoorIndex.Objects : objects;
        // Interiors list their blocked cells; the outdoor world collects its regions' blocked cells
        // only when asked (checks, the debug grid); gameplay asks IsBlocked per cell.
        public IReadOnlyCollection<Vector2Int> BlockedCells
        {
            get
            {
                if (OutdoorIndex != null && !outdoorBlockedCollected)
                {
                    foreach (var region in OutdoorIndex.Regions)
                        foreach (var cell in region.BlockedWorldCells()) blocked.Add(cell);
                    outdoorBlockedCollected = true;
                }
                return blocked;
            }
        }
        private bool outdoorBlockedCollected;

        public SharedWorldModel(OutdoorWorldIndex index)
        {
            OutdoorIndex = index ?? throw new ArgumentNullException(nameof(index));
            Map = index.PrimaryRegion.Map;
            foreach (var region in index.Regions)
            {
                var errors = MapPlacementRules.Validate(region.Map);
                if (errors.Count != 0)
                    throw new InvalidOperationException("Invalid map " + region.MapId + ": " + string.Join("; ", errors));
            }
        }

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
            for (var i = 0; i < map.Objects.Count; i++)
            {
                var placement = map.Objects[i];
                objects.Add(new SharedObjectInstance
                {
                    InstanceId = placement.InstanceId,
                    DefinitionId = placement.Definition.StableId,
                    Definition = placement.Definition,
                    RootCell = placement.RootCell,
                    SignText = placement.SignText,
                    RegionMapId = map.MapId,
                    LocalRootCell = placement.RootCell,
                    OrderInMap = i
                });
            }
        }

        public bool Contains(Vector2Int cell) => OutdoorIndex != null ? OutdoorIndex.Contains(cell) : Map.Contains(cell);
        public bool IsBlocked(Vector2Int cell) => OutdoorIndex != null ? OutdoorIndex.IsBlocked(cell) : blocked.Contains(cell);
        public bool CanEnter(Vector2Int cell) => Contains(cell) && !IsBlocked(cell);
        // The 2D game's grass rule (MapPlacementRules.HasGrass), world cells outdoors.
        public bool HasGrass(Vector2Int cell) => OutdoorIndex != null ? OutdoorIndex.HasGrass(cell) : MapPlacementRules.HasGrass(Map, cell);

        public SharedObjectInstance FindInstance(string instanceId)
        {
            if (OutdoorIndex != null) return OutdoorIndex.FindInstance(instanceId);
            foreach (var instance in objects)
                if (instance.InstanceId == instanceId) return instance;
            return null;
        }

        public bool TryGetHouseDoor(out Vector2Int cell) =>
            OutdoorIndex != null ? OutdoorIndex.TryGetHouseDoor(out cell) : Map.TryGetHouseRoot(out cell);
        public bool TryGetMarker(string id, out Vector2Int cell) =>
            OutdoorIndex != null ? OutdoorIndex.TryGetMarker(id, out cell) : Map.TryGetMarker(id, out cell);

        // Same selection as MapRuntimeLoader2D.CreateObject: authored Action Points for
        // multi-cell, non-examine, sign and well objects; otherwise the plain Examine rule.
        public static bool UsesAuthoredActions(WorldObjectDefinition definition) =>
            definition.ActionPoints.Count > 0 &&
            (definition.Footprint.x > 1 || definition.Footprint.y > 1 ||
             definition.Behavior == WorldObjectBehavior.None ||
             definition.StableId == "sign_basic" || definition.StableId == "well_basic");

        // What using an object does from this cell and facing (WorldObjectActionInteractable's
        // rules): a message (examine, sign text, the bed's "ベッド。") or a seat pose (Phase 4:
        // the 2D game's bench_sit, and the cushion's cushion_sit). Range and facing are unchanged.
        public SharedActionKind ResolveInteraction(SharedObjectInstance target, Vector2Int playerCell,
            FacingDirection facing, bool playerMoving, out string message, out string poseKey)
        {
            message = null;
            poseKey = null;
            if (target == null || playerMoving) return SharedActionKind.None;
            var definition = target.Definition;
            if (UsesAuthoredActions(definition))
            {
                foreach (var point in definition.ActionPoints)
                {
                    if (playerCell != target.RootCell + point.PlayerCellOffset) continue;
                    if (facing != ToFacing(point.PlayerFacing)) continue;
                    if (point.ActionType == WorldActionType.Sit)
                    {
                        poseKey = point.PoseKey;
                        return string.IsNullOrEmpty(poseKey) ? SharedActionKind.None : SharedActionKind.Sit;
                    }
                    message = point.ActionType == WorldActionType.Sleep ? "ベッド。" :
                        !string.IsNullOrWhiteSpace(target.SignText) ? target.SignText :
                        !string.IsNullOrWhiteSpace(point.InteractionText) ? point.InteractionText :
                        definition.ExamineMessage;
                    return string.IsNullOrEmpty(message) ? SharedActionKind.None : SharedActionKind.Message;
                }
                return SharedActionKind.None;
            }
            if (definition.Behavior != WorldObjectBehavior.Examine) return SharedActionKind.None;
            var delta = target.RootCell - playerCell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1 || delta != facing.ToVector()) return SharedActionKind.None;
            message = definition.ExamineMessage;
            return string.IsNullOrEmpty(message) ? SharedActionKind.None : SharedActionKind.Message;
        }

        // True when the object answers from here (a message or a seat); message is the text, if any.
        public bool TryInteract(SharedObjectInstance target, Vector2Int playerCell,
            FacingDirection facing, bool playerMoving, out string message) =>
            ResolveInteraction(target, playerCell, facing, playerMoving, out message, out _) != SharedActionKind.None;

        public SharedObjectInstance FindInteractionAhead(Vector2Int playerCell, FacingDirection facing,
            out string message)
        {
            message = null;
            if (OutdoorIndex == null)
            {
                foreach (var instance in objects)
                    if (TryInteract(instance, playerCell, facing, false, out message)) return instance;
                return null;
            }
            // ver3.2: only the Objects of the chunks around her are asked; when two answer, the one
            // first in its Map's list wins, as when the whole list was walked.
            SharedObjectInstance best = null;
            string bestMessage = null;
            foreach (var instance in OutdoorIndex.ObjectsNear(playerCell))
            {
                if (best != null && Earlier(best, instance)) continue;
                if (TryInteract(instance, playerCell, facing, false, out var text)) { best = instance; bestMessage = text; }
            }
            message = bestMessage;
            return best;
        }

        private bool Earlier(SharedObjectInstance a, SharedObjectInstance b)
        {
            var ra = OutdoorIndex.FindRegion(a.RegionMapId)?.Order ?? 0;
            var rb = OutdoorIndex.FindRegion(b.RegionMapId)?.Order ?? 0;
            return ra != rb ? ra < rb : a.OrderInMap < b.OrderInMap;
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
