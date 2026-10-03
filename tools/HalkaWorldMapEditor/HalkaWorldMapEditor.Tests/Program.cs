using HalkaWorldMapEditor.Core;

var project = ProjectPaths.FindNear(AppContext.BaseDirectory) ?? throw new Exception("Unity project not found");
var mapPath = Path.Combine(ProjectPaths.AuthoringFolder(project), "first_field.hwmap.json");
var catalogPath = ProjectPaths.CatalogPath(project);
var source = File.ReadAllText(mapPath);
var map = MapFormat.LoadMap(mapPath);
var catalog = MapFormat.LoadCatalog(catalogPath);
var passes = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
    passes++;
}

Check("format and bounds", map.MapId == "first_field" && map.Bounds.MinX == -10 && map.Bounds.MaxX == 10 &&
    map.Bounds.MinY == -6 && map.Bounds.MaxY == 6);
Check("golden migration counts", map.Surfaces.Count == 52 && map.Objects.Count == 11 &&
    map.Objects.Count(o => o.DefinitionId == "stone_basic") == 4 &&
    map.Objects.Count(o => o.DefinitionId == "flower_basic") == 4 &&
    map.Objects.Count(o => o.DefinitionId == "tree_basic") == 3);
Check("golden marker coordinates", map.Markers.PlayerSpawn == new GridCell(0, 0) &&
    map.Markers.CrowSpawn == new GridCell(7, 2) && map.Markers.HouseDoor == new GridCell(-7, -4) &&
    map.Markers.OutsideEntry == new GridCell(-7, -5) &&
    map.Markers.RoadEnds.North == new GridCell(0, 6) &&
    map.Markers.RoadEnds.East == new GridCell(10, -1) &&
    map.Markers.RoadEnds.South == new GridCell(2, -6) &&
    map.Markers.RoadEnds.West == new GridCell(-10, 0));
Check("valid official map", MapRules.Validate(map, catalog).Count == 0);
var grass = 0;
for (var y = map.Bounds.MinY; y <= map.Bounds.MaxY; y++)
for (var x = map.Bounds.MinX; x <= map.Bounds.MaxX; x++)
    if (MapRules.HasGrass(map, catalog, new GridCell(x, y))) grass++;
Check("derived grass count", grass == 201);
Check("no grass on road", map.Surfaces.All(p => !MapRules.HasGrass(map, catalog, p.Cell)));
Check("IDs stable unique", map.Objects.All(o => !string.IsNullOrEmpty(o.InstanceId)) &&
    map.Objects.Select(o => o.InstanceId).Distinct().Count() == map.Objects.Count);
Check("stable ordered roundtrip", MapFormat.SerializeMap(MapFormat.ParseMap(source)) == MapFormat.SerializeMap(map));
Check("locked cells", MapRules.IsProtected(map, map.Markers.HouseDoor) &&
    MapRules.IsProtected(map, map.Markers.PlayerSpawn) &&
    MapRules.IsProtected(map, map.Markers.RoadEnds.North));
Check("reject protected and outside", !MapRules.CanPlace(map, catalog, "stone_basic", map.Markers.HouseDoor, out _) &&
    !MapRules.CanPlace(map, catalog, "stone_basic", new GridCell(100, 100), out _));
Check("sprite path safety", ProjectPaths.SpritePath(project, "Assets/Content/World/stone.png").EndsWith("stone.png") &&
    Throws(() => ProjectPaths.SpritePath(project, "../other.png")));
Check("malformed JSON", Throws(() => MapFormat.ParseMap("{not json}")));

var tempDir = Path.Combine(Path.GetTempPath(), "HalkaMapEditorTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    var tempMap = Path.Combine(tempDir, "first_field.hwmap.json");
    File.WriteAllText(tempMap, source);
    var edit = MapSession.Open(tempMap, catalog);
    var originalSurfaceCount = edit.Map.Surfaces.Count;
    var blank = new GridCell(-4, 4);
    Check("surface paint", edit.PaintSurface("dirt", blank) && edit.Map.Surfaces.Count == originalSurfaceCount + 1 && edit.IsDirty);
    edit.Undo();
    Check("surface undo", edit.Map.Surfaces.Count == originalSurfaceCount && !edit.IsDirty);
    edit.Redo();
    Check("surface redo", edit.Map.Surfaces.Count == originalSurfaceCount + 1);
    Check("surface erase", edit.EraseSurface(blank));
    edit.BeginStroke();
    edit.PaintSurface("dirt", new GridCell(-4, 4));
    edit.PaintSurface("dirt", new GridCell(-4, 5));
    edit.EndStroke();
    edit.Undo();
    Check("drag stroke one undo", !edit.Map.Surfaces.Any(s => s.Cell == new GridCell(-4, 4) || s.Cell == new GridCell(-4, 5)));
    var objectCell = new GridCell(-1, 5);
    Check("object add", edit.PlaceObject("stone_basic", objectCell, out _));
    var placed = edit.Map.Objects.Single(o => o.RootCell == objectCell);
    Check("object ID format", placed.InstanceId.StartsWith("obj_") && placed.InstanceId.Length > 30);
    Check("object overlap rejected", !edit.PlaceObject("flower_basic", objectCell, out _));
    Check("object move", edit.MoveObject(placed.InstanceId, new GridCell(-1, 4), out _) &&
        edit.Map.Objects.Single(o => o.InstanceId == placed.InstanceId).RootCell == new GridCell(-1, 4));
    Check("object copy new identity", edit.CopyObject(placed.InstanceId, objectCell, out _) &&
        edit.Map.Objects.Single(o => o.RootCell == objectCell).InstanceId != placed.InstanceId);
    Check("object delete", edit.DeleteObject(placed.InstanceId));
    edit.Undo();
    Check("object delete undo", edit.Map.Objects.Any(o => o.InstanceId == placed.InstanceId));
    edit.Save();
    Check("atomic save and backup", File.Exists(tempMap) && File.Exists(tempMap + ".bak") && !edit.IsDirty);
    Check("reloaded edited map valid", MapRules.Validate(MapFormat.LoadMap(tempMap), catalog).Count == 0);
    var invalid = MapFormat.Clone(map);
    invalid.Objects[1].InstanceId = invalid.Objects[0].InstanceId;
    Check("duplicate ID validation", MapRules.Validate(invalid, catalog).Any(i => i.Code == "duplicateInstanceId"));
    invalid = MapFormat.Clone(map);
    invalid.Surfaces.Add(new SurfacePlacement { DefinitionId = "dirt", Cell = invalid.Surfaces[0].Cell });
    Check("duplicate surface validation", MapRules.Validate(invalid, catalog).Any(i => i.Code == "duplicateSurface"));
}
finally { Directory.Delete(tempDir, true); }
Check("formal map never written", File.ReadAllText(mapPath) == source);
Console.WriteLine($"All {passes} checks passed.");

static bool Throws(Action action)
{
    try { action(); return false; }
    catch { return true; }
}
