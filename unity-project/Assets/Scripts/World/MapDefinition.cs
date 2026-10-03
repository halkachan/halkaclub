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
        public Vector2Int RootCell;
        public WorldObjectDefinition Definition;
    }

    [Serializable]
    public struct LockedMapMarker
    {
        public string StableId;
        public Vector2Int Cell;
    }

    [CreateAssetMenu(menuName = "HALKA WORLD/Map Definition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private int dataVersion = 1;
        [SerializeField] private string mapId;
        [SerializeField] private string displayName;
        [SerializeField] private Vector2Int minCell = new Vector2Int(-10, -6);
        [SerializeField] private Vector2Int maxCell = new Vector2Int(10, 6);
        [SerializeField] private List<SurfacePlacement> surfaces = new List<SurfacePlacement>();
        [SerializeField] private List<WorldObjectPlacement> objects = new List<WorldObjectPlacement>();
        [SerializeField] private Vector2Int playerSpawnCell;
        [SerializeField] private List<LockedMapMarker> entitySpawns = new List<LockedMapMarker>();
        [SerializeField] private Vector2Int houseDoorCell = new Vector2Int(-7, -4);
        [SerializeField] private Vector2Int outsideEntryCell = new Vector2Int(-7, -5);
        [SerializeField] private Vector2Int northRoadEnd = new Vector2Int(0, 6);
        [SerializeField] private Vector2Int eastRoadEnd = new Vector2Int(10, -1);
        [SerializeField] private Vector2Int southRoadEnd = new Vector2Int(2, -6);
        [SerializeField] private Vector2Int westRoadEnd = new Vector2Int(-10, 0);

        public int DataVersion => dataVersion;
        public string MapId => mapId;
        public string DisplayName => displayName;
        public Vector2Int MinCell => minCell;
        public Vector2Int MaxCell => maxCell;
        public IReadOnlyList<SurfacePlacement> Surfaces => surfaces;
        public IReadOnlyList<WorldObjectPlacement> Objects => objects;
        public Vector2Int PlayerSpawnCell => playerSpawnCell;
        public IReadOnlyList<LockedMapMarker> EntitySpawns => entitySpawns;
        public Vector2Int HouseDoorCell => houseDoorCell;
        public Vector2Int OutsideEntryCell => outsideEntryCell;
        public Vector2Int NorthRoadEnd => northRoadEnd;
        public Vector2Int EastRoadEnd => eastRoadEnd;
        public Vector2Int SouthRoadEnd => southRoadEnd;
        public Vector2Int WestRoadEnd => westRoadEnd;

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

        public bool TryGetEntitySpawn(string stableId, out Vector2Int cell)
        {
            foreach (var marker in entitySpawns)
                if (marker.StableId == stableId)
                {
                    cell = marker.Cell;
                    return true;
                }
            cell = default;
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
                    else objects[i] = new WorldObjectPlacement { RootCell = cell, Definition = definition };
                    return true;
                }
            if (definition == null) return false;
            objects.Add(new WorldObjectPlacement { RootCell = cell, Definition = definition });
            return true;
        }
    }
}
