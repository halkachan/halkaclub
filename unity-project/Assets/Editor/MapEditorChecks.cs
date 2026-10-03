using System;
using System.Linq;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class MapEditorChecks
    {
        private const string MapPath = "Assets/Content/Maps/first_field.asset";
        private const string TempPath = "Assets/Editor/__map_editor_test_copy.asset";

        [MenuItem("HALKA WORLD/Check v0.1 migration baseline")]
        public static void RunMigrationBaseline()
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
            Check(map != null, "migration MapDefinition exists");
            Check(map.MinCell == new Vector2Int(-10, -6) &&
                map.MaxCell == new Vector2Int(10, 6), "migration keeps 21x13 bounds");
            Check(map.Surfaces.Count == 52 && map.Objects.Count == 11 &&
                map.Objects.Count(item => item.Definition.StableId == "stone_basic") == 4 &&
                map.Objects.Count(item => item.Definition.StableId == "flower_basic") == 4 &&
                map.Objects.Count(item => item.Definition.StableId == "tree_basic") == 3,
                "one-time migration baseline: 52 dirt, 4 stone, 4 flower, 3 tree");
            Check(CellsFor(map, "stone_basic").SetEquals(new[] {
                    new Vector2Int(1, 1), new Vector2Int(-2, 4),
                    new Vector2Int(3, -4), new Vector2Int(9, 3) }) &&
                CellsFor(map, "flower_basic").SetEquals(new[] {
                    new Vector2Int(-3, 1), new Vector2Int(-5, 2),
                    new Vector2Int(2, 3), new Vector2Int(6, -1) }) &&
                CellsFor(map, "tree_basic").SetEquals(new[] {
                    new Vector2Int(5, 1), new Vector2Int(-7, 2),
                    new Vector2Int(8, -3) }),
                "one-time migration baseline: every world object retains its root cell");
            var baselineGrass = 0;
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
                if (MapPlacementRules.HasGrass(map, new Vector2Int(x, y))) baselineGrass++;
            Check(baselineGrass == 201, "one-time migration baseline: 201 grass cells");
            Check(map.HouseDoorCell == new Vector2Int(-7, -4) &&
                map.OutsideEntryCell == new Vector2Int(-7, -5) &&
                map.NorthRoadEnd == new Vector2Int(0, 6) &&
                map.EastRoadEnd == new Vector2Int(10, -1) &&
                map.SouthRoadEnd == new Vector2Int(2, -6) &&
                map.WestRoadEnd == new Vector2Int(-10, 0),
                "locked house entry and all four road ends match migration baseline");
            Debug.Log("HALKA WORLD v0.1 one-time migration baseline passed.");
        }

        [MenuItem("HALKA WORLD/Validate Map Editor v0.1")]
        public static void Run()
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
            Check(map != null && map.MapId == "first_field" && map.DataVersion == 1,
                "first_field MapDefinition exists");
            Check(MapPlacementRules.Validate(map).Count == 0, "migration map is valid");
            var stone = map.Objects.First(item => item.Definition.StableId == "stone_basic").Definition;
            var dirt = map.Surfaces[0].Definition;
            Check(!MapPlacementRules.CanPlaceObject(map, map.HouseDoorCell, stone, out _) &&
                !MapPlacementRules.CanPlaceObject(map, map.NorthRoadEnd, stone, out _),
                "house entry and road end reject object placement");

            // A temporary copy is the only edited asset; first_field remains untouched.
            if (AssetDatabase.LoadAssetAtPath<MapDefinition>(TempPath) != null)
                throw new InvalidOperationException("Old temporary test map exists: " + TempPath);
            var copy = ScriptableObject.CreateInstance<MapDefinition>();
            EditorUtility.CopySerialized(map, copy);
            AssetDatabase.CreateAsset(copy, TempPath);
            try
            {
                var cell = new Vector2Int(2, 5);
                Check(copy.ObjectAt(cell) == null && copy.SurfaceAt(cell) == null &&
                    MapPlacementRules.HasGrass(copy, cell), "test cell begins as grass");
                Check(MapPlacementRules.CanPlaceObject(copy, cell, stone, out _),
                    "test Stone placement is allowed");
                Undo.IncrementCurrentGroup();
                Undo.RecordObject(copy, "Test Stone placement");
                copy.SetObject(cell, stone);
                Undo.FlushUndoRecordObjects();
                Check(copy.ObjectAt(cell) == stone, "Stone placed");
                Undo.PerformUndo();
                Check(copy.ObjectAt(cell) == null, "Stone placement Undo");
                Undo.PerformRedo();
                Check(copy.ObjectAt(cell) == stone, "Stone placement Redo");
                var movedCell = new Vector2Int(3, 5);
                Check(MapPlacementRules.CanPlaceObject(copy, movedCell, stone, out _, cell),
                    "test Stone move is allowed");
                Undo.RecordObject(copy, "Test Stone move");
                copy.SetObject(cell, null);
                copy.SetObject(movedCell, stone);
                Undo.FlushUndoRecordObjects();
                Check(copy.ObjectAt(cell) == null && copy.ObjectAt(movedCell) == stone,
                    "Stone moved to a new root cell");
                Undo.PerformUndo();
                Check(copy.ObjectAt(cell) == stone && copy.ObjectAt(movedCell) == null,
                    "Stone move Undo");
                Undo.PerformRedo();
                Check(copy.ObjectAt(cell) == null && copy.ObjectAt(movedCell) == stone,
                    "Stone move Redo");
                Undo.RecordObject(copy, "Test Stone erase");
                copy.SetObject(movedCell, null);
                Undo.FlushUndoRecordObjects();
                Check(copy.ObjectAt(movedCell) == null, "Stone erased");
                Undo.PerformUndo();
                Check(copy.ObjectAt(movedCell) == stone, "Stone erase Undo");
                Undo.PerformRedo();
                Check(copy.ObjectAt(movedCell) == null, "Stone erase Redo");
                Undo.RecordObject(copy, "Test Dirt paint");
                copy.SetSurface(cell, dirt);
                Undo.FlushUndoRecordObjects();
                Check(copy.SurfaceAt(cell) == dirt && !MapPlacementRules.HasGrass(copy, cell),
                    "Dirt paint removes grass preview");
                Undo.RecordObject(copy, "Test Dirt erase");
                copy.SetSurface(cell, null);
                Undo.FlushUndoRecordObjects();
                Check(copy.SurfaceAt(cell) == null && MapPlacementRules.HasGrass(copy, cell),
                    "Dirt erase restores grass preview");
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(TempPath, ImportAssetOptions.ForceSynchronousImport);
                var reloaded = AssetDatabase.LoadAssetAtPath<MapDefinition>(TempPath);
                Check(reloaded != null && reloaded.ObjectAt(cell) == null &&
                    reloaded.SurfaceAt(cell) == null &&
                    reloaded.Surfaces.Count == map.Surfaces.Count,
                    "temporary map survives Save and Reload");
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempPath);
            }
            Debug.Log("HALKA WORLD MAP EDITOR v0.1 checks passed; first_field was not edited.");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Map Editor check failed: " + label);
        }

        private static System.Collections.Generic.HashSet<Vector2Int> CellsFor(
            MapDefinition map, string id) => new System.Collections.Generic.HashSet<Vector2Int>(
            map.Objects.Where(item => item.Definition.StableId == id)
                .Select(item => item.RootCell));
    }
}
