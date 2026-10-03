using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    // JSON is the only authoring source; the ScriptableObject is a generated runtime cache.
    public static class MapAuthoringImporter
    {
        public const string AuthoringFolder = "Assets/Content/Maps/Authoring";
        public const string CatalogPath = AuthoringFolder + "/object_catalog.hwcatalog.json";
        private const string MapSuffix = ".hwmap.json";
        private static bool syncing;

        [MenuItem("HALKA WORLD/Sync Authoring JSON to Map Cache")]
        public static void SyncAll()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                var folder = ToFullPath(AuthoringFolder);
                if (!Directory.Exists(folder))
                    throw new InvalidDataException("Authoring map folder is missing: " + folder);
                var files = Directory.GetFiles(folder, "*" + MapSuffix, SearchOption.TopDirectoryOnly);
                if (files.Length == 0) throw new InvalidDataException("No *.hwmap.json found in " + folder);
                Array.Sort(files, StringComparer.Ordinal);
                var catalog = ParseCatalog();
                foreach (var file in files)
                    SyncMapFromFile(file, catalog);
                AssetDatabase.SaveAssets();
            }
            finally { syncing = false; }
        }

        public static MapDefinition SyncMapFromFile(string fullPath)
        {
            var result = SyncMapFromFile(fullPath, ParseCatalog());
            AssetDatabase.SaveAssets();
            return result;
        }

        private static MapDefinition SyncMapFromFile(string fullPath, CatalogDto catalog)
        {
            var sourceName = Path.GetFileName(fullPath);
            var expectedId = sourceName.Substring(0, sourceName.Length - MapSuffix.Length);
            MapDto source;
            try { source = JsonUtility.FromJson<MapDto>(File.ReadAllText(fullPath)); }
            catch (Exception error)
            { throw new InvalidDataException("Map " + expectedId + " JSON parse error: " + error.Message, error); }
            if (source == null || source.format != "halka-world-map" || source.formatVersion != 1 ||
                source.mapId != expectedId || !Regex.IsMatch(source.mapId ?? "", "^[a-z0-9_-]+$") ||
                source.bounds == null || source.markers == null || source.markers.roadEnds == null ||
                source.markers.houseFootprint == null || source.surfaces == null || source.objects == null)
                throw new InvalidDataException("Map " + expectedId + " JSON parse error: missing or invalid format, mapId, bounds, placements or markers.");
            var markers = source.markers;
            if (markers.playerSpawn == null || markers.crowSpawn == null || markers.houseDoor == null ||
                markers.outsideEntry == null || markers.roadEnds.north == null ||
                markers.roadEnds.east == null || markers.roadEnds.south == null ||
                markers.roadEnds.west == null)
                throw new InvalidDataException("Map " + expectedId + ": required marker cell is missing.");
            if (source.markers.houseFootprint.width != 5 || source.markers.houseFootprint.height != 2)
                throw new InvalidDataException("Map " + expectedId + ": current House footprint must remain 5x2.");

            var surfaces = new List<SurfacePlacement>();
            foreach (var item in source.surfaces)
            {
                if (item == null || item.cell == null)
                    throw new InvalidDataException("Map " + expectedId + ": null Surface placement.");
                var definition = FindSurface(item.definitionId);
                if (definition == null)
                    throw new InvalidDataException("Map " + expectedId + ": unknown Surface definitionId " + item.definitionId);
                surfaces.Add(new SurfacePlacement { Cell = item.cell.ToVector(), Definition = definition });
            }
            var objects = new List<WorldObjectPlacement>();
            foreach (var item in source.objects)
            {
                if (item == null || item.rootCell == null)
                    throw new InvalidDataException("Map " + expectedId + ": null Object placement.");
                var definition = FindObject(item.definitionId);
                if (definition == null)
                    throw new InvalidDataException("Map " + expectedId + ": unknown Object definitionId " + item.definitionId);
                objects.Add(new WorldObjectPlacement {
                    InstanceId = item.instanceId, RootCell = item.rootCell.ToVector(), Definition = definition });
            }
            ValidateCatalog(catalog, surfaces, objects);
            var minimum = new Vector2Int(source.bounds.minX, source.bounds.minY);
            var maximum = new Vector2Int(source.bounds.maxX, source.bounds.maxY);
            var spawns = new List<LockedMapMarker> {
                new LockedMapMarker { StableId = "crow", Cell = markers.crowSpawn.ToVector() }
            };
            var temporary = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                temporary.ReplaceFromAuthoring(source.mapId, source.displayName, minimum, maximum,
                    surfaces, objects, markers.playerSpawn.ToVector(), spawns,
                    markers.houseDoor.ToVector(), markers.outsideEntry.ToVector(),
                    markers.roadEnds.north.ToVector(), markers.roadEnds.east.ToVector(),
                    markers.roadEnds.south.ToVector(), markers.roadEnds.west.ToVector());
                var errors = MapPlacementRules.Validate(temporary);
                if (errors.Count != 0)
                    throw new InvalidDataException("Map " + expectedId + " validation error: " + string.Join("; ", errors));

                var assetPath = "Assets/Content/Maps/" + source.mapId + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<MapDefinition>(assetPath);
                if (asset == null)
                {
                    AssetDatabase.CreateAsset(temporary, assetPath);
                    return temporary;
                }
                if (JsonUtility.ToJson(asset) != JsonUtility.ToJson(temporary))
                {
                    asset.ReplaceFromAuthoring(source.mapId, source.displayName, minimum, maximum,
                        surfaces, objects, markers.playerSpawn.ToVector(), spawns,
                        markers.houseDoor.ToVector(), markers.outsideEntry.ToVector(),
                        markers.roadEnds.north.ToVector(), markers.roadEnds.east.ToVector(),
                        markers.roadEnds.south.ToVector(), markers.roadEnds.west.ToVector());
                    EditorUtility.SetDirty(asset);
                }
                return asset;
            }
            finally
            {
                if (!AssetDatabase.Contains(temporary)) UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static void ValidateCatalog(CatalogDto catalog,
            List<SurfacePlacement> surfaces, List<WorldObjectPlacement> objects)
        {
            if (catalog.surfaces.Any(item => item == null || string.IsNullOrWhiteSpace(item.definitionId)) ||
                catalog.objects.Any(item => item == null || string.IsNullOrWhiteSpace(item.definitionId)) ||
                catalog.surfaces.Select(item => item.definitionId).Distinct().Count() != catalog.surfaces.Length ||
                catalog.objects.Select(item => item.definitionId).Distinct().Count() != catalog.objects.Length)
                throw new InvalidDataException("Catalog contains an empty or duplicate definitionId.");
            foreach (var match in catalog.surfaces)
            {
                var definition = FindSurface(match.definitionId);
                if (definition == null || definition.Sprite == null ||
                    AssetDatabase.GetAssetPath(definition.Sprite) != match.previewSpritePath ||
                    definition.Sprite.rect.width != match.visualWidthPixels ||
                    definition.Sprite.rect.height != match.visualHeightPixels)
                    throw new InvalidDataException("Catalog mismatch for Surface " + match.definitionId);
            }
            foreach (var match in catalog.objects)
            {
                var definition = FindObject(match.definitionId);
                if (definition == null || definition.PreviewSprite == null || match.footprint == null ||
                    AssetDatabase.GetAssetPath(definition.PreviewSprite) != match.previewSpritePath ||
                    definition.PreviewSprite.rect.width != match.visualWidthPixels ||
                    definition.PreviewSprite.rect.height != match.visualHeightPixels ||
                    definition.Footprint.x != match.footprint.width ||
                    definition.Footprint.y != match.footprint.height ||
                    definition.BlocksMovement != match.blocksMovement ||
                    definition.ExcludesGrass != match.excludeGrass)
                    throw new InvalidDataException("Catalog mismatch for Object " + match.definitionId);
            }
            if (surfaces.Any(item => !catalog.surfaces.Any(entry => entry.definitionId == item.Definition.StableId)) ||
                objects.Any(item => !catalog.objects.Any(entry => entry.definitionId == item.Definition.StableId)))
                throw new InvalidDataException("Map placement references a Definition absent from the catalog.");
        }

        private static CatalogDto ParseCatalog()
        {
            CatalogDto catalog;
            try { catalog = JsonUtility.FromJson<CatalogDto>(File.ReadAllText(ToFullPath(CatalogPath))); }
            catch (Exception error)
            { throw new InvalidDataException("Map catalog JSON parse error: " + error.Message, error); }
            if (catalog == null || catalog.format != "halka-world-catalog" ||
                catalog.formatVersion != 1 || catalog.surfaces == null || catalog.objects == null)
                throw new InvalidDataException("Map catalog format is missing or unsupported.");
            return catalog;
        }

        private static SurfaceDefinition FindSurface(string id) =>
            AssetDatabase.FindAssets("t:SurfaceDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(item => item != null && item.StableId == id);

        private static WorldObjectDefinition FindObject(string id) =>
            AssetDatabase.FindAssets("t:WorldObjectDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(item => item != null && item.StableId == id);

        private static string ToFullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        [Serializable] private sealed class CellDto
        {
            public int x, y;
            public Vector2Int ToVector() => new Vector2Int(x, y);
        }
        [Serializable] private sealed class SizeDto { public int width, height; }
        [Serializable] private sealed class BoundsDto { public int minX, maxX, minY, maxY; }
        [Serializable] private sealed class SurfaceDto { public string definitionId; public CellDto cell; }
        [Serializable] private sealed class ObjectDto
        { public string instanceId, definitionId; public CellDto rootCell; }
        [Serializable] private sealed class RoadEndsDto
        { public CellDto north, east, south, west; }
        [Serializable] private sealed class MarkersDto
        {
            public CellDto playerSpawn, crowSpawn, houseDoor, outsideEntry;
            public SizeDto houseFootprint;
            public RoadEndsDto roadEnds;
        }
        [Serializable] private sealed class MapDto
        {
            public string format, mapId, displayName;
            public int formatVersion;
            public BoundsDto bounds;
            public SurfaceDto[] surfaces;
            public ObjectDto[] objects;
            public MarkersDto markers;
        }
        [Serializable] private sealed class CatalogSurfaceDto
        { public string definitionId, previewSpritePath; public int visualWidthPixels, visualHeightPixels; }
        [Serializable] private sealed class CatalogObjectDto
        {
            public string definitionId, previewSpritePath;
            public int visualWidthPixels, visualHeightPixels;
            public SizeDto footprint;
            public bool blocksMovement, excludeGrass;
        }
        [Serializable] private sealed class CatalogDto
        {
            public string format;
            public int formatVersion;
            public CatalogSurfaceDto[] surfaces;
            public CatalogObjectDto[] objects;
        }
    }

    public sealed class MapAuthoringPostprocessor : AssetPostprocessor
    {
        private static bool pending;
        private static void OnPostprocessAllAssets(string[] importedAssets,
            string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (pending || !importedAssets.Any(path =>
                path.StartsWith(MapAuthoringImporter.AuthoringFolder + "/", StringComparison.Ordinal) &&
                (path.EndsWith(".hwmap.json", StringComparison.Ordinal) ||
                 path.EndsWith(".hwcatalog.json", StringComparison.Ordinal)))) return;
            pending = true;
            EditorApplication.delayCall += () =>
            {
                pending = false;
                try { MapAuthoringImporter.SyncAll(); }
                catch (Exception error) { Debug.LogError(error); }
            };
        }
    }
}
