using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // Reads Map JSON v4 for checks. It builds an in-memory MapDefinition for MAP EDITOR
    // fixtures without writing any asset, so the official Map cache is never replaced.
    public static class PocMapJson
    {
        [Serializable] public sealed class CellDto { public int x, y; public Vector2Int Cell => new Vector2Int(x, y); }
        [Serializable] public sealed class BoundsDto { public int minX, maxX, minY, maxY; }
        [Serializable] public sealed class SurfaceDto { public string definitionId; public CellDto cell; }
        [Serializable] public sealed class ObjectDto { public string instanceId, definitionId, signText; public CellDto rootCell; }
        [Serializable] public sealed class MarkerDto { public string id; public CellDto cell; }
        [Serializable] public sealed class EntityDto { public string instanceId, definitionId, facing; public CellDto cell; }
        [Serializable]
        public sealed class TransitionDto
        {
            public string instanceId, transitionId, exitDirection, destinationMapId, arrivalFacing;
            public CellDto sourceCell, destinationCell;
        }

        [Serializable]
        public sealed class MapDto
        {
            public string format, mapId, displayName, mapType, baseSurfaceDefinitionId, grassMode, backdropColor;
            public int formatVersion;
            public BoundsDto bounds;
            public List<SurfaceDto> surfaces = new List<SurfaceDto>();
            public List<ObjectDto> objects = new List<ObjectDto>();
            public List<MarkerDto> markers = new List<MarkerDto>();
            public List<EntityDto> entitySpawns = new List<EntityDto>();
            public List<TransitionDto> areaTransitions = new List<TransitionDto>();
        }

        [Serializable] public sealed class OffsetDto { public int x, y; }
        [Serializable]
        public sealed class CatalogObjectDto
        {
            public string definitionId, category;
            public bool blocksMovement = true;
            public List<OffsetDto> blockedCellOffsets = new List<OffsetDto>();
        }
        [Serializable] public sealed class CatalogSurfaceDto { public string definitionId; public bool blocksMovement; }
        [Serializable]
        public sealed class CatalogDto
        {
            public List<CatalogSurfaceDto> surfaces = new List<CatalogSurfaceDto>();
            public List<CatalogObjectDto> objects = new List<CatalogObjectDto>();
        }

        public static MapDto Load(string path) => JsonUtility.FromJson<MapDto>(File.ReadAllText(path));
        public static CatalogDto LoadCatalog(string path) => JsonUtility.FromJson<CatalogDto>(File.ReadAllText(path));

        // Blocked cells computed only from JSON (Map + Catalog), independent of MapDefinition.
        public static HashSet<Vector2Int> BlockedFromJson(MapDto map, CatalogDto catalog)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var surface in map.surfaces)
                if (catalog.surfaces.Single(s => s.definitionId == surface.definitionId).blocksMovement)
                    result.Add(surface.cell.Cell);
            foreach (var item in map.objects)
            {
                var definition = catalog.objects.Single(o => o.definitionId == item.definitionId);
                if (!definition.blocksMovement) continue;
                foreach (var offset in definition.blockedCellOffsets)
                    result.Add(item.rootCell.Cell + new Vector2Int(offset.x, offset.y));
            }
            return result;
        }

        public static MapDefinition ToDefinition(MapDto source)
        {
            var objects = Find<WorldObjectDefinition>().ToDictionary(item => item.StableId);
            var surfaces = Find<SurfaceDefinition>().ToDictionary(item => item.StableId);
            var entities = Find<EntityDefinition>().ToDictionary(item => item.StableId);
            ColorUtility.TryParseHtmlString(source.backdropColor, out var backdrop);
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.ReplaceFromAuthoring(source.mapId, source.displayName, source.mapType,
                surfaces[source.baseSurfaceDefinitionId], source.grassMode, backdrop,
                new Vector2Int(source.bounds.minX, source.bounds.minY), new Vector2Int(source.bounds.maxX, source.bounds.maxY),
                source.surfaces.Select(s => new SurfacePlacement { Cell = s.cell.Cell, Definition = surfaces[s.definitionId] }).ToList(),
                source.objects.Select(o => new WorldObjectPlacement { InstanceId = o.instanceId, RootCell = o.rootCell.Cell,
                    Definition = objects[o.definitionId], SignText = o.signText }).ToList(),
                source.markers.Select(m => new LockedMapMarker { StableId = m.id, Cell = m.cell.Cell }).ToList(),
                source.entitySpawns.Select(e => new EntitySpawnPlacement { InstanceId = e.instanceId,
                    Definition = entities[e.definitionId], Cell = e.cell.Cell, Facing = e.facing }).ToList(),
                source.areaTransitions.Select(t => new AreaTransitionPlacement { InstanceId = t.instanceId,
                    TransitionId = t.transitionId, SourceCell = t.sourceCell.Cell, ExitDirection = t.exitDirection,
                    DestinationMapId = t.destinationMapId, DestinationCell = t.destinationCell.Cell,
                    ArrivalFacing = t.arrivalFacing }).ToList());
            map.name = source.mapId + " (in-memory fixture)";
            map.hideFlags = HideFlags.DontSave;
            return map;
        }

        private static IEnumerable<T> Find<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null);
    }
}
