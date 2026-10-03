using System;
using System.Collections.Generic;
using System.Linq;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public sealed class MapEditorWindow : EditorWindow
    {
        private enum ToolMode { Select, Paint, Erase }
        private enum EditLayer { Surface, Object }

        private const float PaletteWidth = 184f;
        private const float HeaderHeight = 85f;
        private const float DetailHeight = 185f;
        private const float MinCellPixels = 12f;
        private const float MaxCellPixels = 128f;
        private MapDefinition map;
        private SurfaceDefinition selectedSurface;
        private WorldObjectDefinition selectedDefinition;
        private List<SurfaceDefinition> surfacePalette = new List<SurfaceDefinition>();
        private List<WorldObjectDefinition> objectPalette = new List<WorldObjectDefinition>();
        private ToolMode mode = ToolMode.Select;
        private EditLayer layer = EditLayer.Surface;
        private Vector2 pan;
        private float cellPixels = 32f;
        private Vector2Int hoverCell;
        private bool hasHover;
        private Vector2Int? selectedRoot;
        private bool selectedLocked;
        private string selectedLockedLabel;
        private Vector2Int moveToCell;
        private bool showGrid = true;
        private bool showGrass = true;
        private bool showSurface = true;
        private bool showObjects = true;
        private bool showCollision;
        private bool showCoordinates;
        private bool spaceDown;
        private bool draggingDirt;
        private int dragUndoGroup;
        private Vector2Int lastPaintCell;
        private string message = "Ready";
        private List<string> validation = new List<string>();
        private Sprite grassSprite;
        private Sprite houseSprite;
        private Sprite crowSprite;

        [MenuItem("HALKA WORLD/Map Editor")]
        public static void Open() => GetWindow<MapEditorWindow>("HALKA WORLD MAP EDITOR");

        private void OnEnable()
        {
            if (map == null)
                map = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                    "Assets/Content/Maps/first_field.asset");
            grassSprite = LoadSprite("grass.png");
            houseSprite = LoadSprite("house_exterior.png");
            crowSprite = LoadSprite("crow_idle_right.png");
            RefreshPalette();
            Undo.undoRedoPerformed += AfterUndoRedo;
            ValidateMap();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= AfterUndoRedo;

        private void AfterUndoRedo()
        {
            ValidateMap();
            Repaint();
        }

        private static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/" + name);

        private void RefreshPalette()
        {
            surfacePalette = AssetDatabase.FindAssets("t:SurfaceDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SurfaceDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid))).Where(item => item != null)
                .OrderBy(item => item.DisplayName).ToList();
            objectPalette = AssetDatabase.FindAssets("t:WorldObjectDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid))).Where(item => item != null)
                .OrderBy(item => item.DisplayName).ToList();
            if (selectedSurface == null) selectedSurface = surfacePalette.FirstOrDefault();
            if (selectedDefinition == null) selectedDefinition = objectPalette.FirstOrDefault();
        }

        private void OnGUI()
        {
            minSize = new Vector2(640f, 500f);
            HandleShortcuts(Event.current);
            DrawHeader();
            var paletteRect = new Rect(0, HeaderHeight, PaletteWidth,
                position.height - HeaderHeight - DetailHeight);
            var mapRect = new Rect(PaletteWidth, HeaderHeight,
                position.width - PaletteWidth, position.height - HeaderHeight - DetailHeight);
            DrawPalette(paletteRect);
            if (map != null)
            {
                HandleMapInput(Event.current, mapRect);
                DrawMap(mapRect);
            }
            else EditorGUI.HelpBox(mapRect, "Select a MapDefinition asset.", MessageType.Warning);
            DrawDetails(new Rect(0, position.height - DetailHeight, position.width, DetailHeight));
        }

        private void DrawHeader()
        {
            GUILayout.BeginArea(new Rect(8, 4, position.width - 16, HeaderHeight - 4));
            GUILayout.Label("HALKA WORLD MAP EDITOR  v0.1", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var next = (MapDefinition)EditorGUILayout.ObjectField("Map", map,
                typeof(MapDefinition), false);
            if (EditorGUI.EndChangeCheck())
            {
                var switchMap = true;
                if (map != null && EditorUtility.IsDirty(map))
                {
                    var choice = EditorUtility.DisplayDialogComplex("Unsaved Map",
                        "Save changes before switching maps?", "Save", "Discard", "Cancel");
                    if (choice == 0) SaveMap();
                    else if (choice == 1)
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(map),
                            ImportAssetOptions.ForceUpdate);
                    else switchMap = false;
                    if (choice == 0 && EditorUtility.IsDirty(map)) switchMap = false;
                }
                if (switchMap)
                {
                    map = next;
                    selectedRoot = null;
                    ValidateMap();
                }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Map", GUILayout.Width(90))) SaveMap();
            if (GUILayout.Button("Validate Map", GUILayout.Width(100))) ValidateMap();
            if (GUILayout.Button("Refresh Palette", GUILayout.Width(110))) RefreshPalette();
            GUILayout.Label(map != null && EditorUtility.IsDirty(map) ? "DIRTY - unsaved" : "Saved");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawPalette(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16));
            GUILayout.Label("TOOLS  1 / 2 / 3", EditorStyles.boldLabel);
            mode = (ToolMode)GUILayout.Toolbar((int)mode, new[] { "Select", "Paint", "Erase" });
            GUILayout.Space(8);
            GUILayout.Label("LAYER", EditorStyles.boldLabel);
            layer = (EditLayer)GUILayout.Toolbar((int)layer, new[] { "Surface", "Object" });
            GUILayout.Space(8);
            GUILayout.Label("SURFACE", EditorStyles.boldLabel);
            foreach (var definition in surfacePalette)
                if (GUILayout.Toggle(selectedSurface == definition, definition.DisplayName,
                    "Button"))
                {
                    selectedSurface = definition;
                    layer = EditLayer.Surface;
                }
            GUILayout.Space(6);
            GUILayout.Label("OBJECT", EditorStyles.boldLabel);
            foreach (var definition in objectPalette)
                if (GUILayout.Toggle(selectedDefinition == definition, definition.DisplayName,
                    "Button"))
                {
                    selectedDefinition = definition;
                    layer = EditLayer.Object;
                }
            GUILayout.Space(10);
            showGrid = GUILayout.Toggle(showGrid, "Grid (G)");
            showGrass = GUILayout.Toggle(showGrass, "Grass preview");
            showSurface = GUILayout.Toggle(showSurface, "Surface");
            showObjects = GUILayout.Toggle(showObjects, "Objects");
            showCollision = GUILayout.Toggle(showCollision, "Collision");
            showCoordinates = GUILayout.Toggle(showCoordinates, "Coordinates");
            GUILayout.EndArea();
        }

        private Vector2 Origin(Rect localViewport) => localViewport.center + pan;

        private Rect CellRect(Vector2Int cell, Rect localViewport)
        {
            var origin = Origin(localViewport);
            return new Rect(origin.x + (cell.x - 0.5f) * cellPixels,
                origin.y - (cell.y + 0.5f) * cellPixels, cellPixels, cellPixels);
        }

        private Vector2Int CellAt(Vector2 point, Rect localViewport)
        {
            var origin = Origin(localViewport);
            return new Vector2Int(Mathf.FloorToInt((point.x - origin.x) / cellPixels + 0.5f),
                Mathf.FloorToInt((origin.y - point.y) / cellPixels + 0.5f));
        }

        private void DrawMap(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            var local = new Rect(0, 0, rect.width, rect.height);
            GUI.BeginGroup(rect);
            var first = CellAt(new Vector2(0, rect.height), local);
            var last = CellAt(new Vector2(rect.width, 0), local);
            var minX = Mathf.Max(map.MinCell.x, first.x - 1);
            var maxX = Mathf.Min(map.MaxCell.x, last.x + 1);
            var minY = Mathf.Max(map.MinCell.y, first.y - 1);
            var maxY = Mathf.Min(map.MaxCell.y, last.y + 1);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = CellRect(cell, local);
                EditorGUI.DrawRect(tile, new Color(0.76f, 0.86f, 0.64f));
                if (showSurface) DrawSprite(map.SurfaceAt(cell)?.Sprite, tile);
                if (showGrass && MapPlacementRules.HasGrass(map, cell))
                    DrawSprite(grassSprite, tile);
                if (showCollision && MapPlacementRules.BlocksMovement(map, cell))
                    EditorGUI.DrawRect(tile, new Color(0.92f, 0.18f, 0.20f, 0.32f));
                if (showGrid)
                {
                    EditorGUI.DrawRect(new Rect(tile.x, tile.y, tile.width, 1),
                        new Color(0.15f, 0.21f, 0.16f, 0.55f));
                    EditorGUI.DrawRect(new Rect(tile.x, tile.y, 1, tile.height),
                        new Color(0.15f, 0.21f, 0.16f, 0.55f));
                }
                if (showCoordinates && cellPixels >= 36f)
                    GUI.Label(tile, $"{x},{y}", EditorStyles.miniLabel);
            }
            if (showObjects)
            {
                foreach (var placement in map.Objects)
                    DrawRootedSprite(placement.Definition?.PreviewSprite,
                        placement.RootCell, local);
                DrawRootedSprite(houseSprite, map.HouseDoorCell, local);
                var houseRoot = CellRect(map.HouseDoorCell, local);
                GUI.Label(new Rect(houseRoot.x, houseRoot.y, houseRoot.width, 17),
                    "H LOCKED", EditorStyles.whiteMiniLabel);
                if (map.TryGetEntitySpawn("crow", out var crowCell))
                {
                    DrawRootedSprite(crowSprite, crowCell, local);
                    GUI.Label(CellRect(crowCell, local), "C", EditorStyles.whiteMiniLabel);
                }
                var playerCell = CellRect(map.PlayerSpawnCell, local);
                GUI.Label(playerCell, "P", EditorStyles.boldLabel);
            }
            if (selectedRoot.HasValue)
            {
                var outline = CellRect(selectedRoot.Value, local);
                DrawOutline(outline, Color.yellow, 2f);
                GUI.Label(new Rect(outline.x, outline.yMax - 18, outline.width, 18),
                    "R", EditorStyles.boldLabel);
            }
            if (hasHover && map.Contains(hoverCell))
                DrawOutline(CellRect(hoverCell, local), Color.white, 1f);
            GUI.EndGroup();
        }

        private static void DrawSprite(Sprite sprite, Rect destination)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            var source = sprite.textureRect;
            GUI.DrawTextureWithTexCoords(destination, texture,
                new Rect(source.x / texture.width, source.y / texture.height,
                    source.width / texture.width, source.height / texture.height), true);
        }

        private void DrawRootedSprite(Sprite sprite, Vector2Int root, Rect viewport)
        {
            if (sprite == null) return;
            var tile = CellRect(root, viewport);
            var width = sprite.rect.width / GridWorld2D.TilePixels * cellPixels;
            var height = sprite.rect.height / GridWorld2D.TilePixels * cellPixels;
            DrawSprite(sprite, new Rect(tile.center.x - width * 0.5f,
                tile.yMax - height, width, height));
        }

        private static void DrawOutline(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private void HandleMapInput(Event current, Rect rect)
        {
            var inside = rect.Contains(current.mousePosition);
            hasHover = inside;
            if (inside) hoverCell = CellAt(current.mousePosition - rect.position,
                new Rect(0, 0, rect.width, rect.height));
            if (inside && current.type == EventType.ScrollWheel)
            {
                var previous = cellPixels;
                cellPixels = Mathf.Clamp(cellPixels * (current.delta.y > 0 ? 0.9f : 1.1f),
                    MinCellPixels, MaxCellPixels);
                var point = current.mousePosition - rect.position;
                pan = point - new Vector2(rect.width, rect.height) * 0.5f -
                    (point - new Vector2(rect.width, rect.height) * 0.5f - pan) *
                    (cellPixels / previous);
                current.Use();
                Repaint();
                return;
            }
            if (inside && current.type == EventType.MouseDrag &&
                (current.button == 2 || current.button == 0 && spaceDown))
            {
                pan += current.delta;
                current.Use();
                Repaint();
                return;
            }
            if (current.type == EventType.MouseUp && current.button == 0 && draggingDirt)
            {
                Undo.CollapseUndoOperations(dragUndoGroup);
                draggingDirt = false;
                current.Use();
                return;
            }
            if (!inside || !map.Contains(hoverCell) || spaceDown) return;
            if (current.type == EventType.MouseDown && (current.button == 0 || current.button == 1))
            {
                if (current.button == 1 || mode == ToolMode.Erase) Erase(hoverCell);
                else if (mode == ToolMode.Select) Select(hoverCell);
                else Paint(hoverCell);
                if (mode == ToolMode.Paint && layer == EditLayer.Surface && current.button == 0)
                {
                    draggingDirt = true;
                    lastPaintCell = hoverCell;
                    dragUndoGroup = Undo.GetCurrentGroup();
                }
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseDrag && current.button == 0 && draggingDirt)
            {
                PaintLine(lastPaintCell, hoverCell);
                lastPaintCell = hoverCell;
                current.Use();
                Repaint();
            }
        }

        private void PaintLine(Vector2Int from, Vector2Int to)
        {
            var x = from.x;
            var y = from.y;
            while (x != to.x || y != to.y)
            {
                if (x != to.x) x += Math.Sign(to.x - x);
                else y += Math.Sign(to.y - y);
                Paint(new Vector2Int(x, y));
            }
        }

        private void Select(Vector2Int cell)
        {
            selectedLocked = false;
            selectedRoot = null;
            foreach (var placement in map.Objects.Reverse())
                if (CoversCell(placement, cell))
                {
                    selectedRoot = placement.RootCell;
                    moveToCell = placement.RootCell;
                    message = "Selected " + placement.Definition?.DisplayName;
                    return;
                }
            if (cell == map.HouseDoorCell || MapPlacementRules.IsHouseFootprint(map, cell))
            {
                selectedRoot = map.HouseDoorCell;
                selectedLocked = true;
                selectedLockedLabel = "House";
            }
            else if (cell == map.PlayerSpawnCell)
            {
                selectedRoot = cell;
                selectedLocked = true;
                selectedLockedLabel = "Player spawn";
            }
            else if (MapPlacementRules.IsEntitySpawn(map, cell))
            {
                selectedRoot = cell;
                selectedLocked = true;
                selectedLockedLabel = "Crow spawn";
            }
            else message = "Empty cell " + cell;
        }

        private static bool CoversCell(WorldObjectPlacement placement, Vector2Int cell)
        {
            var sprite = placement.Definition?.PreviewSprite;
            if (sprite == null) return placement.RootCell == cell;
            var width = Mathf.CeilToInt(sprite.rect.width / GridWorld2D.TilePixels);
            var height = Mathf.CeilToInt(sprite.rect.height / GridWorld2D.TilePixels);
            var left = placement.RootCell.x - width / 2;
            return cell.x >= left && cell.x < left + width &&
                cell.y >= placement.RootCell.y && cell.y < placement.RootCell.y + height;
        }

        private void Paint(Vector2Int cell)
        {
            if (layer == EditLayer.Surface)
            {
                if (selectedSurface == null || map.SurfaceAt(cell) == selectedSurface) return;
                Undo.RecordObject(map, "Paint map surface");
                map.SetSurface(cell, selectedSurface);
                EditorUtility.SetDirty(map);
            }
            else
            {
                if (!MapPlacementRules.CanPlaceObject(map, cell, selectedDefinition,
                    out var reason)) { message = "Cannot place Object: " + reason; return; }
                Undo.RecordObject(map, "Place world object");
                map.SetObject(cell, selectedDefinition);
                EditorUtility.SetDirty(map);
                selectedRoot = cell;
                moveToCell = cell;
            }
            message = "Changed " + cell + " - Save Map to persist";
            ValidateMap();
        }

        private void Erase(Vector2Int cell)
        {
            if (layer == EditLayer.Surface)
            {
                if (map.SurfaceAt(cell) == null) return;
                Undo.RecordObject(map, "Erase map surface");
                map.SetSurface(cell, null);
            }
            else
            {
                if (map.ObjectAt(cell) == null) return;
                Undo.RecordObject(map, "Erase world object");
                map.SetObject(cell, null);
                selectedRoot = null;
            }
            EditorUtility.SetDirty(map);
            message = "Erased " + cell + " - Save Map to persist";
            ValidateMap();
        }

        private void DeleteSelected()
        {
            if (!selectedRoot.HasValue || selectedLocked || map.ObjectAt(selectedRoot.Value) == null)
                return;
            Undo.RecordObject(map, "Delete selected world object");
            map.SetObject(selectedRoot.Value, null);
            selectedRoot = null;
            EditorUtility.SetDirty(map);
            message = "Object deleted - Save Map to persist";
            ValidateMap();
        }

        private void MoveSelected()
        {
            if (!selectedRoot.HasValue || selectedLocked) return;
            var definition = map.ObjectAt(selectedRoot.Value);
            if (definition == null || moveToCell == selectedRoot.Value) return;
            if (!MapPlacementRules.CanPlaceObject(map, moveToCell, definition,
                out var reason, selectedRoot.Value))
            { message = "Cannot move Object: " + reason; return; }
            Undo.RecordObject(map, "Move world object");
            map.SetObject(selectedRoot.Value, null);
            map.SetObject(moveToCell, definition);
            selectedRoot = moveToCell;
            EditorUtility.SetDirty(map);
            message = "Moved to " + moveToCell + " - Save Map to persist";
            ValidateMap();
        }

        private void DrawDetails(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 5, rect.width - 16, rect.height - 10));
            var selection = selectedRoot.HasValue
                ? selectedLocked ? selectedLockedLabel + " (LOCKED)" :
                    map?.ObjectAt(selectedRoot.Value)?.DisplayName ?? "None" : "None";
            GUILayout.Label($"{map?.MapId ?? "No map"} | Cell: " +
                (hasHover ? hoverCell.ToString() : "-") +
                $" | {mode}: {layer} | {selection} | " +
                (map != null && EditorUtility.IsDirty(map) ? "DIRTY" : "Saved"),
                EditorStyles.boldLabel);
            if (selectedRoot.HasValue)
            {
                var definition = selectedLocked ? null : map?.ObjectAt(selectedRoot.Value);
                GUILayout.Label($"Type: {selection}   Root Cell: {selectedRoot.Value}   " +
                    $"Definition: {(definition != null ? definition.StableId : "LOCKED")}   " +
                    $"Footprint: {(definition != null ? definition.Footprint.ToString() : "fixed")}");
                if (definition != null)
                {
                    GUILayout.BeginHorizontal();
                    moveToCell = EditorGUILayout.Vector2IntField("Move To Cell", moveToCell);
                    if (GUILayout.Button("Move", GUILayout.Width(65))) MoveSelected();
                    if (GUILayout.Button("Delete", GUILayout.Width(65))) DeleteSelected();
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Label(message, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(validation.Count == 0 ? "Validation: VALID" :
                "Validation: ERROR " + validation.Count, EditorStyles.boldLabel);
            foreach (var problem in validation.Take(3))
                GUILayout.Label(problem, EditorStyles.wordWrappedMiniLabel);
            GUILayout.EndArea();
        }

        private void HandleShortcuts(Event current)
        {
            if (current.type == EventType.KeyUp && current.keyCode == KeyCode.Space)
                spaceDown = false;
            if (current.type != EventType.KeyDown) return;
            if (current.keyCode == KeyCode.Space) { spaceDown = true; current.Use(); return; }
            if ((current.control || current.command) && current.keyCode == KeyCode.S)
            { SaveMap(); current.Use(); return; }
            if ((current.control || current.command) && current.keyCode == KeyCode.Z)
            { Undo.PerformUndo(); current.Use(); return; }
            if ((current.control || current.command) && current.keyCode == KeyCode.Y)
            { Undo.PerformRedo(); current.Use(); return; }
            if (current.keyCode == KeyCode.Alpha1) mode = ToolMode.Select;
            else if (current.keyCode == KeyCode.Alpha2) mode = ToolMode.Paint;
            else if (current.keyCode == KeyCode.Alpha3) mode = ToolMode.Erase;
            else if (current.keyCode == KeyCode.G) showGrid = !showGrid;
            else if (current.keyCode == KeyCode.Delete && selectedRoot.HasValue && !selectedLocked)
                DeleteSelected();
            else return;
            current.Use();
            Repaint();
        }

        private void SaveMap()
        {
            if (map == null) return;
            ValidateMap();
            if (validation.Count != 0)
            { message = "Fix validation errors before saving."; return; }
            AssetDatabase.SaveAssets();
            message = "Saved " + map.MapId;
            Repaint();
        }

        private void ValidateMap()
        {
            validation = MapPlacementRules.Validate(map);
            Repaint();
        }
    }
}
