using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    [Serializable]
    public struct SurfacePlacement
    {
        public Vector2Int Cell;
        public SurfaceDefinition Definition;
    }

    [Serializable]
    public struct WorldObjectPlacement
    {
        public string InstanceId;
        public Vector2Int RootCell;
        public WorldObjectDefinition Definition;
        public string SignText;
    }

    [Serializable]
    public struct LockedMapMarker
    {
        public string StableId;
        public Vector2Int Cell;
    }

    [Serializable]
    public struct EntitySpawnPlacement
    {
        public string InstanceId;
        public EntityDefinition Definition;
        public Vector2Int Cell;
        public string Facing;
    }

    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private int dataVersion = 3;
        [SerializeField] private string mapId;
        [SerializeField] private string displayName;
        [SerializeField] private string mapType = "outdoor";
        [SerializeField] private SurfaceDefinition baseSurface;
        [SerializeField] private string grassMode = "auto";
        [SerializeField] private Color backdropColor = new Color(0.063f, 0.063f, 0.078f);
        [SerializeField] private Vector2Int minCell = new Vector2Int(-10, -6);
        [SerializeField] private Vector2Int maxCell = new Vector2Int(10, 6);
        [SerializeField] private List<SurfacePlacement> surfaces = new List<SurfacePlacement>();
        [SerializeField] private List<WorldObjectPlacement> objects = new List<WorldObjectPlacement>();
        [SerializeField] private List<LockedMapMarker> markers = new List<LockedMapMarker>();
        [SerializeField] private List<EntitySpawnPlacement> entitySpawns = new List<EntitySpawnPlacement>();

        public int DataVersion => dataVersion;
        public string MapId => mapId;
        public string DisplayName => displayName;
        public string MapType => mapType;
        public SurfaceDefinition BaseSurface => baseSurface;
        public string GrassMode => grassMode;
        public Color BackdropColor => backdropColor;
        public Vector2Int MinCell => minCell;
        public Vector2Int MaxCell => maxCell;
        public IReadOnlyList<SurfacePlacement> Surfaces => surfaces;
        public IReadOnlyList<WorldObjectPlacement> Objects => objects;
        public IReadOnlyList<LockedMapMarker> Markers => markers;
        public IReadOnlyList<EntitySpawnPlacement> EntitySpawns => entitySpawns;
        public Vector2Int PlayerSpawnCell => TryGetEntitySpawn("player_main", out var spawn) ? spawn.Cell : default;
        public Vector2Int HouseDoorCell => TryGetHouseRoot(out var cell) ? cell : default;
        public Vector2Int OutsideEntryCell => HouseDoorCell + Vector2Int.down;
        public Vector2Int NorthRoadEnd => TryGetMarker("road_north", out var cell) ? cell : default;
        public Vector2Int EastRoadEnd => TryGetMarker("road_east", out var cell) ? cell : default;
        public Vector2Int SouthRoadEnd => TryGetMarker("road_south", out var cell) ? cell : default;
        public Vector2Int WestRoadEnd => TryGetMarker("road_west", out var cell) ? cell : default;

        public bool TryGetHouseRoot(out Vector2Int cell)
        {
            foreach (var placement in objects)
                if (placement.Definition != null && placement.Definition.Behavior == WorldObjectBehavior.HouseTransition)
                { cell = placement.RootCell; return true; }
            cell = default;
            return false;
        }

        public bool TryGetMarker(string stableId, out Vector2Int cell)
        {
            foreach (var marker in markers)
                if (marker.StableId == stableId)
                { cell = marker.Cell; return true; }
            cell = default;
            return false;
        }

        public bool Contains(Vector2Int cell) => cell.x >= minCell.x && cell.x <= maxCell.x &&
            cell.y >= minCell.y && cell.y <= maxCell.y;

        public SurfaceDefinition SurfaceAt(Vector2Int cell)
        {
            foreach (var placement in surfaces)
                if (placement.Cell == cell) return placement.Definition;
            return null;
        }

        public WorldObjectDefinition ObjectAt(Vector2Int cell)
        {
            foreach (var placement in objects)
                if (placement.RootCell == cell) return placement.Definition;
            return null;
        }

        public bool TryGetEntitySpawn(string definitionId, out EntitySpawnPlacement spawn)
        {
            foreach (var item in entitySpawns)
                if (item.Definition != null && item.Definition.StableId == definitionId)
                { spawn = item; return true; }
            spawn = default;
            return false;
        }

        public bool SetSurface(Vector2Int cell, SurfaceDefinition definition)
        {
            if (!Contains(cell)) return false;
            for (var i = 0; i < surfaces.Count; i++)
                if (surfaces[i].Cell == cell)
                {
                    if (surfaces[i].Definition == definition) return false;
                    if (definition == null) surfaces.RemoveAt(i);
                    else surfaces[i] = new SurfacePlacement { Cell = cell, Definition = definition };
                    return true;
                }
            if (definition == null) return false;
            surfaces.Add(new SurfacePlacement { Cell = cell, Definition = definition });
            return true;
        }

        public bool SetObject(Vector2Int cell, WorldObjectDefinition definition)
        {
            if (!Contains(cell)) return false;
            for (var i = 0; i < objects.Count; i++)
                if (objects[i].RootCell == cell)
                {
                    if (objects[i].Definition == definition) return false;
                    if (definition == null) objects.RemoveAt(i);
                    else objects[i] = new WorldObjectPlacement {
                        InstanceId = objects[i].InstanceId, RootCell = cell, Definition = definition };
                    return true;
                }
            if (definition == null) return false;
            objects.Add(new WorldObjectPlacement {
                InstanceId = "obj_" + Guid.NewGuid().ToString("N"), RootCell = cell, Definition = definition });
            return true;
        }

        // Called only by the Unity authoring importer. The JSON is the editable source.
        public void ReplaceFromAuthoring(string id, string title, string type,
            SurfaceDefinition baseDefinition, string grass, Color backdrop, Vector2Int minimum,
            Vector2Int maximum, List<SurfacePlacement> surfacePlacements,
            List<WorldObjectPlacement> objectPlacements, List<LockedMapMarker> mapMarkers,
            List<EntitySpawnPlacement> spawns = null)
        {
            dataVersion = 3;
            mapId = id;
            displayName = title;
            mapType = type;
            baseSurface = baseDefinition;
            grassMode = grass;
            backdropColor = backdrop;
            minCell = minimum;
            maxCell = maximum;
            surfaces = surfacePlacements;
            objects = objectPlacements;
            markers = mapMarkers;
            entitySpawns = spawns ?? new List<EntitySpawnPlacement>();
        }
    }
}
