// はるかわーるど ver3.6: authors north_area_01 (Map JSON v4) with WORLD EDITOR v1.0's own Core
// (MapDocument, MapRules.Validate, MapFormat.SaveAtomic: the same code the editor saves with), so the
// file is exactly what the editor writes and reads. The editor's sources and EXE are not changed.
// Map-local cells 0..20 x 0..19; the registry places them at world offset (-10,7), right North of
// first_field (world x -10..10, y -6..6): local (10,0) = world (0,7) continues first_field's North road (0,6).
//   dotnet run --project tools/poc2d3d/v36_north_author/NorthAuthor.csproj -- unity-project/Assets/Content/Maps/Authoring [--write]
using HalkaWorldMapEditor.Core;

var folder = args.Length > 0 ? args[0] : throw new ArgumentException("Authoring folder");
var write = args.Contains("--write");
var catalog = MapFormat.LoadCatalog(Path.Combine(folder, "object_catalog.hwcatalog.json"));
var map = new MapDocument
{
    MapId = "north_area_01",
    DisplayName = "北エリア",
    MapType = "outdoor",
    BaseSurfaceDefinitionId = "base_ground",
    GrassMode = "auto",
    BackdropColor = "#101014",
    Bounds = new MapBounds { MinX = 0, MaxX = 20, MinY = 0, MaxY = 19 },
};
var surfaces = new Dictionary<GridCell, string>();
void Surface(string id, int x, int y) => surfaces[new GridCell(x, y)] = id;
int n = 0;
void Put(string id, int x, int y) =>
    map.Objects.Add(new ObjectPlacement { InstanceId = $"obj_{id}_v36_north_{n++:00}", DefinitionId = id, RootCell = new GridCell(x, y) });

// The road: first_field's North road (world x = 0) goes on North (local x = 10) to the area's North
// edge (a road end for a later region).
for (var y = 0; y <= 19; y++) Surface("dirt", 10, y);
// The pond (あおいみずべ): about 5 x 4 cells of water, not square, West of the road and a little
// away from it (the road is never blocked by it). Water blocks movement; its bank is walkable.
var water = new (int y, int x0, int x1)[] { (13, 4, 6), (12, 3, 7), (11, 3, 8), (10, 4, 8), (9, 5, 7) };
foreach (var (y, x0, x1) in water) for (var x = x0; x <= x1; x++) Surface("water", x, y);
// A side path from the road to a small viewing place on the pond's South bank.
foreach (var (x, y) in new[] { (9, 8), (8, 8), (7, 8), (6, 8), (5, 8), (6, 7), (7, 7) }) Surface("dirt", x, y);
foreach (var (cell, id) in surfaces) map.Surfaces.Add(new SurfacePlacement { DefinitionId = id, Cell = cell });

// At the pond: lily pads where one looks into the water from the bank (its Action Point is (6,8),
// facing up), reeds at the water's edge, stones round the bank.
Put("water_lily", 6, 9);
Put("pond_reeds", 3, 11);
Put("pond_reeds", 8, 10);
Put("pond_reeds", 4, 13);
foreach (var (x, y) in new[] { (2, 10), (9, 12), (8, 14), (3, 8) }) Put("stone_basic", x, y);
map.Markers.Add(new MapMarker { Id = "landmark_north_blue_pond", Cell = new GridCell(6, 8) });
map.Markers.Add(new MapMarker { Id = "road_south", Cell = new GridCell(10, 0) });
map.Markers.Add(new MapMarker { Id = "road_north", Cell = new GridCell(10, 19) });
// At the entrance: the blue signpost (read from (11,0), facing up), East of the road.
Put("signpost_blue", 11, 1);
// A grove round the pond (shade, perches for the crow) and trees over the rest of the area; none on
// the road, the side path or the border row next to first_field.
// (2,8): a tree West of the viewing place (its shade, the crow's nearest perch there). No tree in the
// column behind the viewing place (x 4..8, y 2..7): the 3D camera stands there looking at the pond.
foreach (var (x, y) in new[] { (2, 8), (1, 12), (2, 15), (5, 16), (1, 7), (12, 11), (14, 14), (17, 9), (18, 3), (14, 4), (16, 16), (12, 16), (8, 16), (19, 13), (1, 3), (3, 2), (19, 7) })
    Put("tree_basic", x, y);
// Blue flowers near the water and along the road; a few white ones.
foreach (var (x, y) in new[] { (2, 13), (4, 8), (9, 10), (9, 6), (11, 5), (12, 8), (13, 2), (15, 11), (16, 6), (11, 15), (13, 16), (4, 15), (7, 15), (18, 16), (17, 1), (3, 5), (8, 2) })
    Put("flower_blue", x, y);
foreach (var (x, y) in new[] { (15, 7), (5, 5), (18, 11) }) Put("flower_basic", x, y);

var first = MapFormat.LoadMap(Path.Combine(folder, "first_field.hwmap.json"));
var east = MapFormat.LoadMap(Path.Combine(folder, "east_area_01.hwmap.json"));
var issues = MapRules.Validate(map, catalog, new[] { first, east, map });
foreach (var issue in issues) Console.WriteLine((issue.IsWarning ? "WARNING " : "ERROR ") + issue.Code + " " + issue.Message + " " + issue.Cell);
Console.WriteLine($"north_area_01: {map.Surfaces.Count} surfaces, {map.Objects.Count} objects, {map.Markers.Count} markers, issues {issues.Count}");
// What the area looks like (local cells, North up; ~ water, = road/path, T tree, o stone, b blue flower,
// w white flower, L lily (examine), r reeds, S sign).
var glyph = new Dictionary<string, char> { ["tree_basic"] = 'T', ["stone_basic"] = 'o', ["flower_basic"] = 'w', ["flower_blue"] = 'b',
    ["water_lily"] = 'L', ["pond_reeds"] = 'r', ["signpost_blue"] = 'S' };
for (var y = 19; y >= 0; y--)
{
    var line = $"{y,3} ";
    for (var x = 0; x <= 20; x++)
    {
        var obj = map.Objects.FirstOrDefault(o => o.RootCell == new GridCell(x, y));
        line += obj != null ? glyph[obj.DefinitionId] : surfaces.TryGetValue(new GridCell(x, y), out var s) ? (s == "water" ? '~' : '=') : '.';
    }
    Console.WriteLine(line);
}
if (issues.Any(i => !i.IsWarning)) return 1;
if (write)
{
    MapFormat.SaveAtomic(map, Path.Combine(folder, "north_area_01.hwmap.json"));
    var again = MapFormat.LoadMap(Path.Combine(folder, "north_area_01.hwmap.json"));
    Console.WriteLine("written and read back by WORLD EDITOR Core: " + again.MapId + " " + again.Objects.Count + " objects, re-validated issues " +
        MapRules.Validate(again, catalog, new[] { first, east, again }).Count);
}
return 0;
