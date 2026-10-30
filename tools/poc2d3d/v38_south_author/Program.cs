// はるかわーるど ver3.8: authors south_area_01 (Map JSON v4) with WORLD EDITOR v1.0's own Core
// (MapDocument, MapRules.Validate, MapFormat.SaveAtomic: the same code the editor saves with), so the
// file is exactly what the editor writes and reads. The editor's sources and EXE are not changed.
// Map-local cells 0..20 x 0..19; the registry places them at world offset (-10,-26), right South of
// first_field (world x -10..10, y -6..6): local (12,19) = world (2,-7) continues first_field's South road (2,-6).
//   dotnet run --project tools/poc2d3d/v38_south_author/SouthAuthor.csproj -- unity-project/Assets/Content/Maps/Authoring [--write]
using HalkaWorldMapEditor.Core;

var folder = args.Length > 0 ? args[0] : throw new ArgumentException("Authoring folder");
var write = args.Contains("--write");
var catalog = MapFormat.LoadCatalog(Path.Combine(folder, "object_catalog.hwcatalog.json"));
const int MaxX = 20, MaxY = 19;
var map = new MapDocument
{
    MapId = "south_area_01",
    DisplayName = "南エリア",
    MapType = "outdoor",
    BaseSurfaceDefinitionId = "base_ground",
    GrassMode = "auto",
    BackdropColor = "#101014",
    Bounds = new MapBounds { MinX = 0, MaxX = MaxX, MinY = 0, MaxY = MaxY },
};

// The whole area, North (y 19, by first_field) at the top; one character per cell:
//   = road / trail (dirt)       P the red pinwheel (かざぐるまをまわす from (7,7), facing up)
//   L rock ledge (the hill's edge, blocks)     R red-brown rock (blocks)     T tree (the field's)
//   r small stones (walkable)   g short grass (walkable)   a reddish soil (walkable, no grass)
//   S the red signpost (read from the trail, (10,13), facing up)            . grass
// The road comes down from first_field at x 12 and runs on South at x 13 (a road end at the area's
// South edge, for a later region). At y 13 a trail turns West off it, past the sign, and climbs North
// of the hill to its only way up, (9,10)-(9,9). The hill (風の丘): its top x 4..10, y 5..9, open grass
// with patches of bare reddish soil and the pinwheel; its edge a ring of rock ledges and rocks, whose
// South face (y 4) is the step the cameras see. East of the road the red rocks lie scattered in the
// grass; few trees (an open sky), none South of the hill (the 3D camera looks over it from there), and
// none in the rows next to first_field (at first two stood at y 16: from first_field's South edge the 3D
// camera stood right behind them and they hid the field; they were moved beside the hill).
var rows = new[]
{
    "............=........", // 19
    "..g.........=.....g..", // 18
    "......r.....=..R.....", // 17
    "............=........", // 16
    ".......g....=.g......", // 15
    "....R.....S.=...r..R.", // 14
    "..g......====...g....", // 13
    ".r.......=..==..R....", // 12
    "....R....=...=.r...g.", // 11
    "...RLLRLL=LR.=...R...", // 10
    "...Lg.a..=.L.=..r..T.", // 9
    ".T.Ra..P.a.L.=.R..g..", // 8
    "...L.a....rR.=.....R.", // 7
    "...Lg...a..L.=..r....", // 6
    "...R.a...agL.=.R...g.", // 5
    "...RLLLLLLLR.=...r...", // 4
    "..r..g..r....=..R....", // 3
    ".T...R...R...=...g...", // 2
    "...g.....r...=....T..", // 1
    ".......R.....=...R...", // 0
};
if (rows.Length != MaxY + 1 || rows.Any(r => r.Length != MaxX + 1)) throw new InvalidOperationException("layout size");
var ids = new Dictionary<char, string>
{
    ['P'] = "south_pinwheel_red", ['L'] = "south_rock_ledge", ['R'] = "south_rock_red", ['T'] = "tree_basic",
    ['r'] = "south_rock_small", ['g'] = "south_grass_short", ['a'] = "south_ground_accent", ['S'] = "south_signpost_red",
};
int n = 0;
for (var row = 0; row < rows.Length; row++)
for (var x = 0; x <= MaxX; x++)
{
    var y = MaxY - row;
    var c = rows[row][x];
    if (c == '=') map.Surfaces.Add(new SurfacePlacement { DefinitionId = "dirt", Cell = new GridCell(x, y) });
    else if (ids.TryGetValue(c, out var id))
        map.Objects.Add(new ObjectPlacement { InstanceId = $"obj_{id}_v38_south_{n++:00}", DefinitionId = id, RootCell = new GridCell(x, y) });
    else if (c != '.') throw new InvalidOperationException("unknown cell " + c);
}
map.Markers.Add(new MapMarker { Id = "landmark_south_red_pinwheel", Cell = new GridCell(7, 7) });
map.Markers.Add(new MapMarker { Id = "road_north", Cell = new GridCell(12, 19) });
map.Markers.Add(new MapMarker { Id = "road_south", Cell = new GridCell(13, 0) });

var first = MapFormat.LoadMap(Path.Combine(folder, "first_field.hwmap.json"));
var east = MapFormat.LoadMap(Path.Combine(folder, "east_area_01.hwmap.json"));
var north = MapFormat.LoadMap(Path.Combine(folder, "north_area_01.hwmap.json"));
var west = MapFormat.LoadMap(Path.Combine(folder, "west_area_01.hwmap.json"));
var issues = MapRules.Validate(map, catalog, new[] { first, east, north, west, map });
foreach (var issue in issues) Console.WriteLine((issue.IsWarning ? "WARNING " : "ERROR ") + issue.Code + " " + issue.Message + " " + issue.Cell);
Console.WriteLine($"south_area_01: {map.Surfaces.Count} surfaces, {map.Objects.Count} objects, {map.Markers.Count} markers, issues {issues.Count}");
foreach (var group in map.Objects.GroupBy(o => o.DefinitionId).OrderBy(g => g.Key)) Console.WriteLine($"  {group.Key} x{group.Count()}");

// Every walkable cell must be reachable from the road's North end (no shut-in corner); the hilltop
// only by its trail (the distance walked to the pinwheel, from the road's North end, is printed).
bool Blocked(int x, int y) => map.Objects.Any(o => o.RootCell == new GridCell(x, y) &&
    catalog.Objects.First(d => d.DefinitionId == o.DefinitionId).BlocksMovement);
var steps = new Dictionary<(int, int), int> { [(12, MaxY)] = 0 };
var todo = new Queue<(int, int)>(steps.Keys);
while (todo.Count > 0)
{
    var (cx, cy) = todo.Dequeue();
    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
    {
        var (nx, ny) = (cx + dx, cy + dy);
        if (nx < 0 || nx > MaxX || ny < 0 || ny > MaxY || Blocked(nx, ny) || steps.ContainsKey((nx, ny))) continue;
        steps[(nx, ny)] = steps[(cx, cy)] + 1;
        todo.Enqueue((nx, ny));
    }
}
var walkable = 0;
var shut = new List<(int, int)>();
for (var y = 0; y <= MaxY; y++)
for (var x = 0; x <= MaxX; x++)
    if (!Blocked(x, y)) { walkable++; if (!steps.ContainsKey((x, y))) shut.Add((x, y)); }
Console.WriteLine($"walkable {walkable}, reachable from the road {steps.Count}, shut in: {string.Join(" ", shut)}");
Console.WriteLine($"steps from the road's North end to the pinwheel's Action Point (7,7): {steps.GetValueOrDefault((7, 7), -1)}");
// The pinwheel's place is found by walking (discover radius 2 round (7,7)): no road cell within 3.
var road = map.Surfaces.Select(s => (s.Cell.X, s.Cell.Y)).Where(c => !(c.X <= 11 && c.Y <= 13 && c.X >= 9)).ToList();
Console.WriteLine("nearest road cell to (7,7): " + road.Min(c => Math.Abs(c.X - 7) + Math.Abs(c.Y - 7)));
for (var y = MaxY; y >= 0; y--) Console.WriteLine($"{y,3} {rows[MaxY - y]}   world y={y - 26}");
Console.WriteLine("    " + string.Concat(Enumerable.Range(0, MaxX + 1).Select(x => (x % 10).ToString())) + "   (world x = local - 10)");
if (issues.Any(i => !i.IsWarning) || shut.Count > 0) return 1;
if (write)
{
    MapFormat.SaveAtomic(map, Path.Combine(folder, "south_area_01.hwmap.json"));
    var again = MapFormat.LoadMap(Path.Combine(folder, "south_area_01.hwmap.json"));
    Console.WriteLine("written and read back by WORLD EDITOR Core: " + again.MapId + " " + again.Objects.Count + " objects, re-validated issues " +
        MapRules.Validate(again, catalog, new[] { first, east, north, west, again }).Count);
}
return 0;
