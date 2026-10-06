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
        public const string EntityCatalogPath = AuthoringFolder + "/entity_catalog.hwentitycatalog.json";
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
                var playerCount = 0;
                foreach (var file in files)
                {
                    var source = JsonUtility.FromJson<MapDto>(File.ReadAllText(file));
                    if (source == null || source.entitySpawns == null)
                        throw new InvalidDataException("Map is missing v3 Entity Spawns: " + file);
                    playerCount += source.entitySpawns.Count(item => item != null && item.definitionId == "player_main");
                }
                if (playerCount != 1)
                    throw new InvalidDataException("World needs exactly one player_main Entity Spawn; found " + playerCount);
                var catalog = ParseCatalog();
                EnsureCatalogDefinitions(catalog);
                var entities = ParseEntityCatalog();
                EnsureEntityDefinitions(entities);
                foreach (var file in files)
                    SyncMapFromFile(file, catalog, entities);
                AssetDatabase.SaveAssets();
            }
            finally { syncing = false; }
        }

        public static MapDefinition SyncMapFromFile(string fullPath)
        {
            var catalog = ParseCatalog();
            EnsureCatalogDefinitions(catalog);
            var entities = ParseEntityCatalog();
            EnsureEntityDefinitions(entities);
            var result = SyncMapFromFile(fullPath, catalog, entities);
            AssetDatabase.SaveAssets();
            return result;
        }

        private static MapDefinition SyncMapFromFile(string fullPath, CatalogDto catalog, EntityCatalogDto entityCatalog)
        {
            var sourceName = Path.GetFileName(fullPath);
            var expectedId = sourceName.Substring(0, sourceName.Length - MapSuffix.Length);
            MapDto source;
            try { source = JsonUtility.FromJson<MapDto>(File.ReadAllText(fullPath)); }
            catch (Exception error)
            { throw new InvalidDataException("Map " + expectedId + " JSON parse error: " + error.Message, error); }
            if (source == null || source.format != "halka-world-map" || source.formatVersion != 3 ||
                source.mapId != expectedId || !Regex.IsMatch(source.mapId ?? "", "^[a-z0-9_-]+$") ||
                source.bounds == null || source.markers == null || source.surfaces == null || source.objects == null ||
                source.entitySpawns == null ||
                (source.mapType != "outdoor" && source.mapType != "interior") ||
                (source.grassMode != "auto" && source.grassMode != "none"))
                throw new InvalidDataException("Map " + expectedId + " JSON parse error: missing or invalid format, mapId, bounds, placements or markers.");
            var baseSurface = FindSurface(source.baseSurfaceDefinitionId);
            if (baseSurface == null || !ColorUtility.TryParseHtmlString(source.backdropColor, out var backdrop))
                throw new InvalidDataException("Map " + expectedId + ": invalid base surface or backdrop.");
            var markers = new List<LockedMapMarker>();
            foreach (var marker in source.markers)
            {
                if (marker == null || string.IsNullOrWhiteSpace(marker.id) || marker.cell == null ||
                    markers.Any(item => item.StableId == marker.id))
                    throw new InvalidDataException("Map " + expectedId + ": invalid or duplicate marker.");
                markers.Add(new LockedMapMarker { StableId = marker.id, Cell = marker.cell.ToVector() });
            }

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
                var definitionId = item.definitionId;
                var signText = item.signText;
                switch (definitionId)
                {
                    case "sign_north": definitionId = "sign_basic"; signText = signText ?? "きた"; break;
                    case "sign_east": definitionId = "sign_basic"; signText = signText ?? "ひがし"; break;
                    case "sign_south": definitionId = "sign_basic"; signText = signText ?? "みなみ"; break;
                    case "sign_west": definitionId = "sign_basic"; signText = signText ?? "にし"; break;
                }
                var definition = FindObject(definitionId);
                if (definition == null)
                    throw new InvalidDataException("Map " + expectedId + ": unknown Object definitionId " + definitionId);
                objects.Add(new WorldObjectPlacement {
                    InstanceId = item.instanceId, RootCell = item.rootCell.ToVector(),
                    Definition = definition, SignText = signText });
            }
            var spawns = new List<EntitySpawnPlacement>();
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source.entitySpawns)
            {
                if (item == null || item.cell == null || string.IsNullOrWhiteSpace(item.instanceId) ||
                    !entityIds.Add(item.instanceId) ||
                    (item.facing != "up" && item.facing != "down" && item.facing != "left" && item.facing != "right"))
                    throw new InvalidDataException("Map " + expectedId + ": invalid Entity Spawn.");
                var definition = FindEntity(item.definitionId);
                if (definition == null || !entityCatalog.entities.Any(entry => entry.definitionId == item.definitionId))
                    throw new InvalidDataException("Map " + expectedId + ": unknown Entity " + item.definitionId);
                spawns.Add(new EntitySpawnPlacement { InstanceId = item.instanceId,
                    Definition = definition, Cell = item.cell.ToVector(), Facing = item.facing });
            }
            ValidateCatalog(catalog, surfaces, objects);
            var minimum = new Vector2Int(source.bounds.minX, source.bounds.minY);
            var maximum = new Vector2Int(source.bounds.maxX, source.bounds.maxY);
            var temporary = ScriptableObject.CreateInstance<MapDefinition>();
            try
            {
                temporary.ReplaceFromAuthoring(source.mapId, source.displayName, source.mapType,
                    baseSurface, source.grassMode, backdrop, minimum, maximum,
                    surfaces, objects, markers, spawns);
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
                    asset.ReplaceFromAuthoring(source.mapId, source.displayName, source.mapType,
                        baseSurface, source.grassMode, backdrop, minimum, maximum,
                        surfaces, objects, markers, spawns);
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
                    definition.Sprite.rect.height != match.visualHeightPixels ||
                    definition.BlocksMovement != match.blocksMovement ||
                    definition.GrowsGrass != match.growsGrass)
                    throw new InvalidDataException("Catalog mismatch for Surface " + match.definitionId);
            }
            foreach (var match in catalog.objects)
            {
                var definition = FindObject(match.definitionId);
                if (definition == null || match.footprint == null || match.blockedCellOffsets == null ||
                    match.actionPoints == null ||
                    (definition.PreviewSprite == null ? "" : AssetDatabase.GetAssetPath(definition.PreviewSprite)) !=
                        (match.previewSpritePath ?? "") ||
                    (definition.PreviewSprite != null && (definition.PreviewSprite.rect.width != match.visualWidthPixels ||
                    definition.PreviewSprite.rect.height != match.visualHeightPixels)) ||
                    (definition.PreviewSprite == null && !string.IsNullOrEmpty(match.previewSpritePath)) ||
                    definition.Footprint.x != match.footprint.width ||
                    definition.Footprint.y != match.footprint.height ||
                    definition.RootAnchor != match.rootAnchor ||
                    !definition.EffectiveBlockedOffsets().SequenceEqual(
                        match.blockedCellOffsets.Select(item => item.ToVector())) ||
                    definition.ActionPoints.Count != match.actionPoints.Length ||
                    !definition.ActionPoints.Select((point, index) =>
                        point.Id == match.actionPoints[index].id &&
                        point.PlayerCellOffset == match.actionPoints[index].playerCellOffset.ToVector() &&
                        point.PlayerFacing == ParseFacing(match.actionPoints[index].playerFacing) &&
                        point.ActionType == ParseActionType(match.actionPoints[index].actionType) &&
                        point.PoseKey == (match.actionPoints[index].poseKey ?? "") &&
                        point.InteractionText == (match.actionPoints[index].interactionText ?? "")).All(equal => equal) ||
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
                catalog.formatVersion != 3 || catalog.surfaces == null || catalog.objects == null)
                throw new InvalidDataException("Map catalog format is missing or unsupported.");
            return catalog;
        }

        private static void EnsureCatalogDefinitions(CatalogDto catalog)
        {
            foreach (var entry in catalog.surfaces)
            {
                var asset = FindSurface(entry.definitionId);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<SurfaceDefinition>();
                    AssetDatabase.CreateAsset(asset, "Assets/Content/Maps/" + entry.definitionId + ".asset");
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.previewSpritePath);
                if (sprite == null) throw new InvalidDataException("Surface sprite missing: " + entry.previewSpritePath);
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("stableId").stringValue = entry.definitionId;
                serialized.FindProperty("displayName").stringValue = entry.displayName;
                serialized.FindProperty("sprite").objectReferenceValue = sprite;
                serialized.FindProperty("blocksMovement").boolValue = entry.blocksMovement;
                serialized.FindProperty("growsGrass").boolValue = entry.growsGrass;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var entry in catalog.objects)
            {
                var asset = FindObject(entry.definitionId);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<WorldObjectDefinition>();
                    AssetDatabase.CreateAsset(asset, "Assets/Content/Maps/" + entry.definitionId + ".asset");
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.previewSpritePath);
                if (sprite == null && !string.IsNullOrWhiteSpace(entry.previewSpritePath))
                    throw new InvalidDataException("Object sprite missing: " + entry.previewSpritePath);
                if (entry.actionPoints == null || entry.blockedCellOffsets == null || entry.footprint == null)
                    throw new InvalidDataException("Incomplete Object metadata: " + entry.definitionId);
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("stableId").stringValue = entry.definitionId;
                serialized.FindProperty("displayName").stringValue = entry.displayName;
                serialized.FindProperty("previewSprite").objectReferenceValue = sprite;
                serialized.FindProperty("footprint").vector2IntValue =
                    new Vector2Int(entry.footprint.width, entry.footprint.height);
                serialized.FindProperty("rootAnchor").stringValue = entry.rootAnchor;
                serialized.FindProperty("blocksMovement").boolValue = entry.blocksMovement;
                serialized.FindProperty("excludesGrass").boolValue = entry.excludeGrass;
                var offsets = serialized.FindProperty("blockedCellOffsets");
                offsets.arraySize = entry.blockedCellOffsets.Length;
                for (var index = 0; index < entry.blockedCellOffsets.Length; index++)
                    offsets.GetArrayElementAtIndex(index).vector2IntValue = entry.blockedCellOffsets[index].ToVector();
                var points = serialized.FindProperty("actionPoints");
                points.arraySize = entry.actionPoints.Length;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < entry.actionPoints.Length; index++)
                {
                    var point = entry.actionPoints[index];
                    if (point == null || point.playerCellOffset == null ||
                        string.IsNullOrWhiteSpace(point.id) || !ids.Add(point.id))
                        throw new InvalidDataException("Invalid Action Point ID: " + entry.definitionId);
                    var target = points.GetArrayElementAtIndex(index);
                    target.FindPropertyRelative("id").stringValue = point.id;
                    target.FindPropertyRelative("playerCellOffset").vector2IntValue = point.playerCellOffset.ToVector();
                    target.FindPropertyRelative("playerFacing").enumValueIndex = (int)ParseFacing(point.playerFacing);
                    target.FindPropertyRelative("actionType").enumValueIndex = (int)ParseActionType(point.actionType);
                    target.FindPropertyRelative("poseKey").stringValue = point.poseKey ?? "";
                    target.FindPropertyRelative("interactionText").stringValue = point.interactionText ?? "";
                }
                var examine = entry.actionPoints.FirstOrDefault(point => point.actionType == "examine");
                serialized.FindProperty("examineMessage").stringValue = examine?.interactionText ?? "";
                serialized.FindProperty("behavior").enumValueIndex = entry.definitionId == "house_main"
                    ? (int)WorldObjectBehavior.HouseTransition
                    : examine != null && sprite != null ? (int)WorldObjectBehavior.Examine : (int)WorldObjectBehavior.None;
                if (entry.definitionId == "house_main" || entry.definitionId == "bed_basic")
                {
                    serialized.FindProperty("rootedArtwork").boolValue = entry.definitionId == "house_main";
                    serialized.FindProperty("sortingOrder").intValue = entry.definitionId == "bed_basic" ? 3 : 2;
                    if (sprite != null) serialized.FindProperty("clickColliderSize").vector2Value = sprite.bounds.size;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        private static SurfaceDefinition FindSurface(string id) =>
            AssetDatabase.FindAssets("t:SurfaceDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(item => item != null && item.StableId == id);

        private static WorldFacing ParseFacing(string value)
        {
            switch (value)
            {
                case "up": return WorldFacing.Up;
                case "down": return WorldFacing.Down;
                case "left": return WorldFacing.Left;
                case "right": return WorldFacing.Right;
                default: throw new InvalidDataException("Invalid Action Point facing: " + value);
            }
        }

        private static EntityCatalogDto ParseEntityCatalog()
        {
            EntityCatalogDto catalog;
            try { catalog = JsonUtility.FromJson<EntityCatalogDto>(File.ReadAllText(ToFullPath(EntityCatalogPath))); }
            catch (Exception error)
            { throw new InvalidDataException("Entity catalog JSON parse error: " + error.Message, error); }
            if (catalog == null || catalog.format != "halka-world-entity-catalog" ||
                catalog.formatVersion != 1 || catalog.entities == null || catalog.entities.Length == 0 ||
                catalog.entities.Any(item => item == null || string.IsNullOrWhiteSpace(item.definitionId)) ||
                catalog.entities.Select(item => item.definitionId).Distinct().Count() != catalog.entities.Length)
                throw new InvalidDataException("Entity catalog is incomplete or has duplicate IDs.");
            return catalog;
        }

        private static void EnsureEntityDefinitions(EntityCatalogDto catalog)
        {
            foreach (var entry in catalog.entities)
            {
                var asset = FindEntity(entry.definitionId);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<EntityDefinition>();
                    AssetDatabase.CreateAsset(asset, "Assets/Content/Maps/" + entry.definitionId + ".asset");
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.previewSpritePath);
                if (sprite == null || entry.visualWidthCells < 1 || entry.visualHeightCells < 1 ||
                    entry.maxInstances < 1 || entry.wanderRegion == null && entry.runtimeBehavior == "crow-wander")
                    throw new InvalidDataException("Invalid Entity definition: " + entry.definitionId);
                var serialized = new SerializedObject(asset);
                serialized.FindProperty("stableId").stringValue = entry.definitionId;
                serialized.FindProperty("displayName").stringValue = entry.displayName;
                serialized.FindProperty("previewSprite").objectReferenceValue = sprite;
                serialized.FindProperty("visualCells").vector2IntValue =
                    new Vector2Int(entry.visualWidthCells, entry.visualHeightCells);
                serialized.FindProperty("defaultFacing").stringValue = entry.defaultFacing;
                serialized.FindProperty("runtimeBehavior").stringValue = entry.runtimeBehavior;
                serialized.FindProperty("spawnMode").stringValue = entry.spawnMode;
                serialized.FindProperty("blocksMovement").boolValue = entry.blocksMovement;
                serialized.FindProperty("maxInstances").intValue = entry.maxInstances;
                if (entry.wanderRegion != null)
                {
                    serialized.FindProperty("wanderMinimum").vector2IntValue =
                        new Vector2Int(entry.wanderRegion.minX, entry.wanderRegion.minY);
                    serialized.FindProperty("wanderMaximum").vector2IntValue =
                        new Vector2Int(entry.wanderRegion.maxX, entry.wanderRegion.maxY);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static WorldActionType ParseActionType(string value)
        {
            switch (value)
            {
                case "none": return WorldActionType.None;
                case "examine": return WorldActionType.Examine;
                case "sit": return WorldActionType.Sit;
                case "sleep": return WorldActionType.Sleep;
                default: throw new InvalidDataException("Invalid Action Point type: " + value);
            }
        }

        private static WorldObjectDefinition FindObject(string id) =>
            AssetDatabase.FindAssets("t:WorldObjectDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(item => item != null && item.StableId == id);

        private static EntityDefinition FindEntity(string id) =>
            AssetDatabase.FindAssets("t:EntityDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<EntityDefinition>(
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
        { public string instanceId, definitionId, signText; public CellDto rootCell; }
        [Serializable] private sealed class MarkerDto { public string id; public CellDto cell; }
        [Serializable] private sealed class EntitySpawnDto
        { public string instanceId, definitionId, facing; public CellDto cell; }
        [Serializable] private sealed class MapDto
        {
            public string format, mapId, displayName;
            public string mapType, baseSurfaceDefinitionId, grassMode, backdropColor;
            public int formatVersion;
            public BoundsDto bounds;
            public SurfaceDto[] surfaces;
            public ObjectDto[] objects;
            public MarkerDto[] markers;
            public EntitySpawnDto[] entitySpawns;
        }
        [Serializable] private sealed class WanderRegionDto { public int minX, maxX, minY, maxY; }
        [Serializable] private sealed class EntityDto
        {
            public string definitionId, displayName, previewSpritePath, defaultFacing, runtimeBehavior, spawnMode;
            public int visualWidthCells, visualHeightCells, maxInstances;
            public bool blocksMovement;
            public WanderRegionDto wanderRegion;
        }
        [Serializable] private sealed class EntityCatalogDto
        { public string format; public int formatVersion; public EntityDto[] entities; }
        [Serializable] private sealed class CatalogSurfaceDto
        {
            public string definitionId, displayName, previewSpritePath;
            public int visualWidthPixels, visualHeightPixels;
            public bool blocksMovement;
            public bool growsGrass;
        }
        [Serializable] private sealed class CatalogObjectDto
        {
            public string definitionId, displayName, previewSpritePath, rootAnchor;
            public int visualWidthPixels, visualHeightPixels;
            public SizeDto footprint;
            public CellDto[] blockedCellOffsets;
            public ActionPointDto[] actionPoints;
            public bool blocksMovement, excludeGrass;
        }
        [Serializable] private sealed class ActionPointDto
        {
            public string id, playerFacing, actionType, poseKey, interactionText;
            public CellDto playerCellOffset;
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
                 path.EndsWith(".hwcatalog.json", StringComparison.Ordinal) ||
                 path.EndsWith(".hwentitycatalog.json", StringComparison.Ordinal)))) return;
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
