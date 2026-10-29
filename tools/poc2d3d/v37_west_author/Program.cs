// はるかわーるど ver3.7: authors west_area_01 (Map JSON v4) with WORLD EDITOR v1.0's own Core
// (MapDocument, MapRules.Validate, MapFormat.SaveAtomic: the same code the editor saves with), so the
// file is exactly what the editor writes and reads. The editor's sources and EXE are not changed.
// Map-local cells 0..24 x 0..12; the registry places them at world offset (-35,-6), right West of
// first_field (world x -10..10, y -6..6): local (24,6) = world (-11,0) continues first_field's West road (-10,0).
//   dotnet run --project tools/poc2d3d/v37_west_author/WestAuthor.csproj -- unity-project/Assets/Content/Maps/Authoring [--write]
using HalkaWorldMapEditor.Core;

var folder = args.Length > 0 ? args[0] : throw new ArgumentException("Authoring folder");
var write = args.Contains("--write");
var catalog = MapFormat.LoadCatalog(Path.Combine(folder, "object_catalog.hwcatalog.json"));
var map = new MapDocument
{
    MapId = "west_area_01",
    DisplayName = "西エリア",
    MapType = "outdoor",
    BaseSurfaceDefinitionId = "base_ground",
    GrassMode = "auto",
    BackdropColor = "#101014",
    Bounds = new MapBounds { MinX = 0, MaxX = 24, MinY = 0, MaxY = 12 },
};
var surfaces = new Dictionary<GridCell, string>();
void Surface(string id, int x, int y) => surfaces[new GridCell(x, y)] = id;
int n = 0;
void Put(string id, int x, int y) =>
    map.Objects.Add(new ObjectPlacement { InstanceId = $"obj_{id}_v37_west_{n++:00}", DefinitionId = id, RootCell = new GridCell(x, y) });

// The forest road (こもれびの こみち): first_field's West road (world y = 0) comes in at local (24,6),
// bends one row South at x 16 and runs on to the area's West edge (a road end for a later region).
for (var x = 16; x <= 24; x++) Surface("dirt", x, 6);
for (var x = 0; x <= 16; x++) Surface("dirt", x, 5);
// A narrow trail off the road, North into the trees, to the secret clearing (こもれびのひろば).
foreach (var (x, y) in new[] { (11, 6), (11, 7), (10, 7) }) Surface("dirt", x, y);
foreach (var (cell, id) in surfaces) map.Surfaces.Add(new SurfacePlacement { DefinitionId = id, Cell = cell });

// The clearing: open grass, about 5 x 4 cells and not square (rows y 8..11), ringed by trees. The big
// stump stands in it; its Action Point is (8,9), facing up (きりかぶにふれる).
var clearing = new HashSet<(int, int)>();
foreach (var (y, x0, x1) in new[] { (8, 7, 10), (9, 6, 10), (10, 6, 10), (11, 7, 9) })
    for (var x = x0; x <= x1; x++) clearing.Add((x, y));
Put("tree_stump_hollow", 8, 10);
Put("fallen_leaves", 7, 11);
Put("fallen_leaves", 9, 10);
map.Markers.Add(new MapMarker { Id = "landmark_west_forest_clearing", Cell = new GridCell(8, 9) });
map.Markers.Add(new MapMarker { Id = "road_east", Cell = new GridCell(24, 6) });
map.Markers.Add(new MapMarker { Id = "road_west", Cell = new GridCell(0, 5) });
// Where the sunlight comes through the leaves (木漏れ日): the game draws a few dapples of light round
// each komorebi_ Marker (on the road, at the trail's mouth and in the clearing; none by night).
var light = new[] { (22, 6), (17, 6), (13, 5), (6, 5), (1, 5), (11, 7), (7, 10), (9, 8) };
for (var i = 0; i < light.Length; i++)
    map.Markers.Add(new MapMarker { Id = $"komorebi_{i + 1:00}", Cell = new GridCell(light[i].Item1, light[i].Item2) });
// At the entrance: the green signpost (read from (22,6) on the road, facing up), North of the road.
Put("signpost_green", 22, 7);

// The forest. A tree's picture is 3 x 4 cells (its root and three rows above), so roots stand in rows
// 1..9 and columns 1..23; the rows above them (10..12) are the trees' crowns, with bushes and ferns
// below. Trees flank the clearing and stand thick North of the road; South of the road they are fewer
// and keep two rows' distance from it (the 3D camera stands South of her: trees there fade, but
// clear rows keep the road readable; three trees were moved one row South after the first local look). No tree in the column the camera looks up into the clearing from
// (x 6..10, y 2..8), on the road, the trail, or by first_field (x 23, 24 near the road).
var trees = new[]
{
    // beside the clearing and North of the road
    (5, 9), (11, 9), (3, 9), (1, 9), (2, 7), (13, 8), (15, 9), (17, 8), (19, 9), (21, 8), (14, 7),
    // South of the road
    (1, 2), (4, 2), (6, 1), (11, 2), (13, 1), (15, 2), (17, 1), (19, 2), (21, 1), (22, 3),
};
foreach (var (x, y) in trees) Put("tree_basic", x, y);
// Low bushes at the forest's edges and under the crowns (they block); ferns and fallen leaves
// (walkable) on the forest floor.
foreach (var (x, y) in new[] { (3, 4), (9, 4), (19, 4), (16, 7), (4, 7), (20, 7), (0, 3), (14, 2), (12, 11), (14, 12), (16, 11),
    (18, 12), (20, 11), (22, 12), (24, 11), (0, 11), (2, 12), (4, 11), (6, 12), (10, 12), (2, 0), (12, 0), (20, 0), (23, 9) })
    Put("forest_bush", x, y);
foreach (var (x, y) in new[] { (5, 6), (8, 6), (15, 6), (19, 5), (7, 4), (12, 4), (22, 4), (6, 11), (11, 8), (1, 7), (17, 10),
    (13, 11), (19, 11), (21, 10), (23, 11), (15, 12), (1, 10), (3, 11), (10, 11), (8, 0), (17, 3) })
    Put("forest_fern", x, y);
foreach (var (x, y) in new[] { (20, 5), (4, 6), (13, 6), (2, 4), (18, 10), (16, 0) }) Put("fallen_leaves", x, y);
foreach (var (x, y) in new[] { (18, 9), (16, 2), (5, 1), (9, 1) }) Put("stone_basic", x, y);
foreach (var (x, y) in new[] { (12, 10), (22, 10), (10, 2) }) Put("flower_basic", x, y);

foreach (var o in map.Objects)
    if (clearing.Contains((o.RootCell.X, o.RootCell.Y)) && o.DefinitionId is not ("tree_stump_hollow" or "fallen_leaves"))
        throw new InvalidOperationException("clearing not open at " + o.RootCell);

var first = MapFormat.LoadMap(Path.Combine(folder, "first_field.hwmap.json"));
var east = MapFormat.LoadMap(Path.Combine(folder, "east_area_01.hwmap.json"));
var north = MapFormat.LoadMap(Path.Combine(folder, "north_area_01.hwmap.json"));
var issues = MapRules.Validate(map, catalog, new[] { first, east, north, map });
foreach (var issue in issues) Console.WriteLine((issue.IsWarning ? "WARNING " : "ERROR ") + issue.Code + " " + issue.Message + " " + issue.Cell);
Console.WriteLine($"west_area_01: {map.Surfaces.Count} surfaces, {map.Objects.Count} objects, {map.Markers.Count} markers, issues {issues.Count}");

// Every walkable cell must be reachable from the road's East end (no shut-in corner).
bool Blocked(int x, int y) => map.Objects.Any(o => o.RootCell == new GridCell(x, y) &&
    catalog.Objects.First(d => d.DefinitionId == o.DefinitionId).BlocksMovement);
var seen = new HashSet<(int, int)> { (24, 6) };
var todo = new Queue<(int, int)>(seen);
while (todo.Count > 0)
{
    var (cx, cy) = todo.Dequeue();
    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
    {
        var (nx, ny) = (cx + dx, cy + dy);
        if (nx < 0 || nx > 24 || ny < 0 || ny > 12 || Blocked(nx, ny) || !seen.Add((nx, ny))) continue;
        todo.Enqueue((nx, ny));
    }
}
var walkable = 0;
var shut = new List<(int, int)>();
for (var y = 0; y <= 12; y++)
for (var x = 0; x <= 24; x++)
    if (!Blocked(x, y)) { walkable++; if (!seen.Contains((x, y))) shut.Add((x, y)); }
Console.WriteLine($"walkable {walkable}, reachable from the road {seen.Count}, shut in: {string.Join(" ", shut)}");

// What the area looks like (local cells, North up; = road/trail, T tree, # bush, f fern, l leaves,
// o stone, w white flower, K stump (touch), S sign, * a komorebi Marker, c the clearing's open grass).
var glyph = new Dictionary<string, char> { ["tree_basic"] = 'T', ["stone_basic"] = 'o', ["flower_basic"] = 'w', ["forest_bush"] = '#',
    ["forest_fern"] = 'f', ["fallen_leaves"] = 'l', ["tree_stump_hollow"] = 'K', ["signpost_green"] = 'S' };
for (var y = 12; y >= 0; y--)
{
    var line = $"{y,3} ";
    for (var x = 0; x <= 24; x++)
    {
        var obj = map.Objects.FirstOrDefault(o => o.RootCell == new GridCell(x, y));
        var marker = map.Markers.FirstOrDefault(m => m.Cell == new GridCell(x, y));
        line += obj != null ? glyph[obj.DefinitionId] : marker != null && marker.Id.StartsWith("komorebi_") ? '*' :
            surfaces.ContainsKey(new GridCell(x, y)) ? '=' : clearing.Contains((x, y)) ? 'c' : '.';
    }
    Console.WriteLine(line + $"   world y={y - 6}");
}
Console.WriteLine("    " + string.Concat(Enumerable.Range(0, 25).Select(x => (x % 10).ToString())) + "   (world x = local - 35)");
if (issues.Any(i => !i.IsWarning) || shut.Count > 0) return 1;
if (write)
{
    MapFormat.SaveAtomic(map, Path.Combine(folder, "west_area_01.hwmap.json"));
    var again = MapFormat.LoadMap(Path.Combine(folder, "west_area_01.hwmap.json"));
    Console.WriteLine("written and read back by WORLD EDITOR Core: " + again.MapId + " " + again.Objects.Count + " objects, re-validated issues " +
        MapRules.Validate(again, catalog, new[] { first, east, north, again }).Count);
}
return 0;
