// はるかわーるど ver3.9: authors the four diagonal transition regions (Map JSON v4) with WORLD EDITOR v1.0's own
// Core (MapDocument, MapRules.Validate, MapFormat.SaveAtomic: the same code the editor saves with). The editor's
// sources and EXE are not changed.
//
// The regions fill the four empty corners of the world's rectangle between the North, East, South and West areas:
//   northwest_transition_01  25 x 20 at (-35,7)    the West forest  -> the North waterside
//   northeast_transition_01  26 x 20 at (11,7)     the East flowers -> the North waterside
//   southwest_transition_01  25 x 20 at (-35,-26)  the West forest  -> the South rocks
//   southeast_transition_01  26 x 20 at (11,-26)   the East flowers -> the South rocks
// None of them is a sight of its own (no landmark, no Interaction, no place to fly to; no sunflowers, the East plaza's own): each is a stretch of
// country where its two neighbours' plants mix. A cell's mix is t = (dA + 0.5) / (dA + dB + 1), dA and dB its
// distances from the borders with neighbour A and neighbour B (t 0 beside A, 1 beside B), with a little stable
// noise so the two kinds interleave; the plants' kinds and densities are lerped by t. Everything is decided by
// a stable hash of (Map ID, cell, salt): the same file on every run, the same world on every start.
// Two grass trails cross each region from one neighbour to the other (no dirt: the neighbours have no road
// there to continue); no blocking Object stands on them, few beside them. Then every walkable cell must be
// reachable inside its own region from the trails (Objects that shut a pocket are taken out), and the whole
// nine-region world is searched from first_field's player start (breadth first, over the real collision of
// every Map at its World offset): no walkable cell of a new region may be shut in, and each of the eight new
// borders must have walkable cells side by side.
//   dotnet run --project tools/poc2d3d/v39_diagonal_author/DiagonalAuthor.csproj -- unity-project/Assets/Content/Maps/Authoring [--write]
using HalkaWorldMapEditor.Core;

var folder = args.Length > 0 ? args[0] : throw new ArgumentException("Authoring folder");
var write = args.Contains("--write");
var catalog = MapFormat.LoadCatalog(Path.Combine(folder, "object_catalog.hwcatalog.json"));

// The five regions of ver3.8, as outdoor_main.hwworld.json places them (they are read, never changed).
var existing = new List<(MapDocument map, int ox, int oy)>
{
    (MapFormat.LoadMap(Path.Combine(folder, "first_field.hwmap.json")), 0, 0),
    (MapFormat.LoadMap(Path.Combine(folder, "east_area_01.hwmap.json")), 11, -6),
    (MapFormat.LoadMap(Path.Combine(folder, "north_area_01.hwmap.json")), -10, 7),
    (MapFormat.LoadMap(Path.Combine(folder, "west_area_01.hwmap.json")), -35, -6),
    (MapFormat.LoadMap(Path.Combine(folder, "south_area_01.hwmap.json")), -10, -26),
};

static double Lerp(double a, double b, double t) => a + (b - a) * t;
var specs = new[]
{
    new Spec("northwest_transition_01", "北西エリア", "nw", -35, 7, 25, 20, "west", "north",
        (x, y) => y - 7, (x, y) => -11 - x,
        new Mix[]
        {
            new("tree_basic", t => Lerp(0.11, 0.025, t)), new("forest_bush", t => Lerp(0.06, 0.008, t)),
            new("forest_fern", t => Lerp(0.05, 0.01, t)), new("fallen_leaves", t => Lerp(0.03, 0.0, t)),
            new("flower_blue", t => Lerp(0.006, 0.05, t)), new("pond_reeds", t => Lerp(0.0, 0.035, t)),
            new("stone_basic", t => Lerp(0.006, 0.014, t)),
        },
        new[]
        {
            new[] { (-20, 7), (-20, 10), (-15, 10), (-15, 13), (-11, 13) },
            new[] { (-30, 7), (-30, 15), (-25, 15), (-25, 21), (-17, 21), (-17, 24), (-11, 24) },
        },
        t => t < 0.55 ? "fallen_leaves" : null, 0.22),
    new Spec("northeast_transition_01", "北東エリア", "ne", 11, 7, 26, 20, "east", "north",
        (x, y) => y - 7, (x, y) => x - 11,
        new Mix[]
        {
            new("flower_yellow", t => Lerp(0.085, 0.012, t)),
            new("flower_blue", t => Lerp(0.01, 0.07, t)), new("pond_reeds", t => Lerp(0.0, 0.035, t)),
            new("tree_basic", t => 0.03), new("stone_basic", t => 0.008), new("flower_basic", t => 0.006),
        },
        new[]
        {
            new[] { (24, 7), (24, 10), (18, 10), (18, 13), (11, 13) },
            new[] { (32, 7), (32, 16), (26, 16), (26, 22), (11, 22) },
        },
        t => null, 0.0),
    new Spec("southwest_transition_01", "南西エリア", "sw", -35, -26, 25, 20, "west", "south",
        (x, y) => -7 - y, (x, y) => -11 - x,
        new Mix[]
        {
            new("tree_basic", t => Lerp(0.105, 0.012, t)), new("forest_bush", t => Lerp(0.06, 0.0, t)),
            new("forest_fern", t => Lerp(0.045, 0.0, t)), new("fallen_leaves", t => Lerp(0.025, 0.0, t)),
            new("south_rock_red", t => Lerp(0.004, 0.045, t)), new("south_rock_small", t => Lerp(0.004, 0.03, t)),
            new("south_grass_short", t => Lerp(0.0, 0.04, t)), new("stone_basic", t => Lerp(0.008, 0.0, t)),
        },
        new[]
        {
            new[] { (-22, -7), (-22, -10), (-16, -10), (-16, -13), (-11, -13) },
            new[] { (-31, -7), (-31, -17), (-24, -17), (-24, -22), (-11, -22) },
        },
        t => t < 0.45 ? "fallen_leaves" : t > 0.6 ? "south_ground_accent" : null, 0.2),
    new Spec("southeast_transition_01", "南東エリア", "se", 11, -26, 26, 20, "east", "south",
        (x, y) => -7 - y, (x, y) => x - 11,
        new Mix[]
        {
            new("flower_yellow", t => Lerp(0.075, 0.016, t)),
            new("tree_basic", t => 0.016), new("south_rock_red", t => Lerp(0.004, 0.04, t)),
            new("south_rock_small", t => Lerp(0.0, 0.03, t)), new("south_grass_short", t => Lerp(0.008, 0.03, t)),
            new("south_ground_accent", t => Lerp(0.0, 0.018, t)), new("stone_basic", t => Lerp(0.008, 0.0, t)),
        },
        new[]
        {
            new[] { (24, -7), (24, -10), (17, -10), (17, -14), (11, -14) },
            new[] { (31, -7), (31, -18), (22, -18), (22, -21), (11, -21) },
        },
        t => t > 0.55 ? "south_ground_accent" : null, 0.2),
};

var defs = catalog.Objects.ToDictionary(d => d.DefinitionId);
var made = new List<(MapDocument map, int ox, int oy, Spec spec, HashSet<GridCell> trail)>();
var failed = false;
foreach (var spec in specs)
{
    var map = new MapDocument
    {
        MapId = spec.MapId,
        DisplayName = spec.DisplayName,
        MapType = "outdoor",
        BaseSurfaceDefinitionId = "base_ground",
        GrassMode = "auto",
        BackdropColor = "#101014",
        Bounds = new MapBounds { MinX = 0, MaxX = spec.Width - 1, MinY = 0, MaxY = spec.Height - 1 },
    };
    // The trails (world waypoints joined by straight runs, x first) in Map-local cells.
    var trail = new HashSet<GridCell>();
    var trailIndex = 0;
    foreach (var path in spec.Trails)
    {
        for (var i = 0; i + 1 < path.Length; i++)
        {
            var (x0, y0) = path[i];
            var (x1, y1) = path[i + 1];
            for (var x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) trail.Add(new GridCell(x - spec.OffX, y0 - spec.OffY));
            for (var y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) trail.Add(new GridCell(x1 - spec.OffX, y - spec.OffY));
        }
        var letter = (char)('a' + trailIndex++);
        // The trail's two ends, on the borders with the neighbours (protected cells: nothing stands on them).
        map.Markers.Add(new MapMarker { Id = $"trail_{letter}_{spec.NeighbourA}", Cell = new GridCell(path[0].Item1 - spec.OffX, path[0].Item2 - spec.OffY) });
        map.Markers.Add(new MapMarker { Id = $"trail_{letter}_{spec.NeighbourB}", Cell = new GridCell(path[^1].Item1 - spec.OffX, path[^1].Item2 - spec.OffY) });
    }
    bool NearTrail(GridCell c) => trail.Contains(c with { X = c.X + 1 }) || trail.Contains(c with { X = c.X - 1 }) ||
        trail.Contains(c with { Y = c.Y + 1 }) || trail.Contains(c with { Y = c.Y - 1 });
    var n = 0;
    for (var y = 0; y < spec.Height; y++)
    for (var x = 0; x < spec.Width; x++)
    {
        var cell = new GridCell(x, y);
        var (wx, wy) = (x + spec.OffX, y + spec.OffY);
        double dA = spec.DistanceA(wx, wy), dB = spec.DistanceB(wx, wy);
        var t = Math.Clamp((dA + 0.5) / (dA + dB + 1.0) + (Hash.Unit(spec.MapId, x, y, "mix") - 0.5) * 0.24, 0.0, 1.0);
        string? id = null;
        if (trail.Contains(cell))
        {
            var decor = spec.TrailDecor(t);
            if (decor != null && Hash.Unit(spec.MapId, x, y, "trail") < spec.TrailDecorChance) id = decor;
        }
        else
        {
            var u = Hash.Unit(spec.MapId, x, y, "pick");
            // Groves and patches rather than an even sprinkle: a slow stable noise (one value every 4 cells,
            // blended between) scales the density from 0.3 to 1.7 times.
            var clump = 0.3 + 1.4 * Hash.Smooth(spec.MapId, wx / 4.0, wy / 4.0);
            var sum = 0.0;
            foreach (var mix in spec.Mix)
            {
                sum += Math.Max(0.0, mix.Chance(t)) * clump;
                if (u < sum) { id = mix.DefinitionId; break; }
            }
            // Few blocking things right beside a trail, so it reads as a way through.
            if (id != null && defs[id].BlocksMovement && NearTrail(cell) && Hash.Unit(spec.MapId, x, y, "side") < 0.6) id = null;
        }
        if (id == null || map.Markers.Any(m => m.Cell == cell)) continue;
        map.Objects.Add(new ObjectPlacement { InstanceId = $"obj_{id}_v39_{spec.Tag}_{n++:000}", DefinitionId = id, RootCell = cell });
    }
    // Objects the editor's own rules refuse (a tree whose picture would hang over the Map's edge, ...) are left out.
    for (var pass = 0; pass < 20; pass++)
    {
        // (and a flower's examine spot is kept free: what stands on it goes)
        var refused = MapRules.Validate(map, catalog).Where(i => (!i.IsWarning || i.Code == "actionBlocked") && i.Cell is not null).ToList();
        if (refused.Count == 0) break;
        var bad = refused.Select(i => i.Cell!.Value).ToHashSet();
        // an examine spot off the Map names the spot, not the Object: the Object beside it goes
        var spots = refused.Where(i => i.Code == "actionBounds").Select(i => i.Cell!.Value).ToList();
        map.Objects.RemoveAll(o => bad.Contains(o.RootCell) || (defs[o.DefinitionId].ActionPoints.Count > 0 &&
            spots.Any(s => Math.Abs(s.X - o.RootCell.X) + Math.Abs(s.Y - o.RootCell.Y) <= 1)));
    }
    // No pocket: every walkable cell reachable inside the region from its trails (an Object that shuts one is taken out).
    for (var pass = 0; pass < 200; pass++)
    {
        var reach = Flood(map, catalog, trail);
        var shut = AllCells(map).Where(c => !MapRules.BlocksMovement(map, catalog, c) && !reach.Contains(c)).ToList();
        if (shut.Count == 0) break;
        var c0 = shut[0];
        var opener = map.Objects.Where(o => defs[o.DefinitionId].BlocksMovement &&
                Math.Abs(o.RootCell.X - c0.X) + Math.Abs(o.RootCell.Y - c0.Y) == 1)
            .OrderBy(o => Neighbours(o.RootCell).Any(reach.Contains) ? 0 : 1).First();
        map.Objects.Remove(opener);
    }
    made.Add((map, spec.OffX, spec.OffY, spec, trail));
}

// Validation with the whole world (instance IDs across Maps, as WORLD EDITOR checks them).
var worldDocs = existing.Select(e => e.map).Concat(made.Select(m => m.map)).ToList();
foreach (var (map, ox, oy, spec, trail) in made)
{
    var issues = MapRules.Validate(map, catalog, worldDocs);
    foreach (var issue in issues) Console.WriteLine((issue.IsWarning ? "WARNING " : "ERROR ") + issue.Code + " " + issue.Message + " " + issue.Cell);
    if (issues.Any(i => !i.IsWarning)) failed = true;
    Console.WriteLine($"{map.MapId}: {map.Surfaces.Count} surfaces, {map.Objects.Count} objects " +
        $"({map.Objects.Count(o => defs[o.DefinitionId].BlocksMovement)} blocking), {map.Markers.Count} markers, issues {issues.Count}");
    foreach (var group in map.Objects.GroupBy(o => o.DefinitionId).OrderBy(g => g.Key)) Console.Write($"  {group.Key} x{group.Count()}");
    Console.WriteLine();
}
var ids = new HashSet<string>(StringComparer.Ordinal);
foreach (var o in worldDocs.SelectMany(m => m.Objects))
    if (!ids.Add(o.InstanceId)) { Console.WriteLine("ERROR duplicate instance id across the world: " + o.InstanceId); failed = true; }

// The whole world, in world cells: owner, walkable.
var all = existing.Select(e => (e.map, e.ox, e.oy)).Concat(made.Select(m => (m.map, m.ox, m.oy))).ToList();
var owner = new Dictionary<(int, int), string>();
var walk = new Dictionary<(int, int), bool>();
foreach (var (map, ox, oy) in all)
    foreach (var c in AllCells(map))
    {
        var w = (c.X + ox, c.Y + oy);
        if (owner.TryGetValue(w, out var other)) { Console.WriteLine($"ERROR overlap {w} {other} {map.MapId}"); failed = true; }
        owner[w] = map.MapId;
        walk[w] = !MapRules.BlocksMovement(map, catalog, c);
    }
var ff = existing[0].map;
var start = ff.EntitySpawns.First(s => s.DefinitionId == "player_main").Cell;
var seen = new HashSet<(int, int)> { (start.X, start.Y) };
var queue = new Queue<(int, int)>(seen);
while (queue.Count > 0)
{
    var (cx, cy) = queue.Dequeue();
    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
    {
        var nxt = (cx + dx, cy + dy);
        if (walk.TryGetValue(nxt, out var ok) && ok && seen.Add(nxt)) queue.Enqueue(nxt);
    }
}
Console.WriteLine($"world: {owner.Count} cells in {all.Count} regions, walkable {walk.Count(p => p.Value)}, reachable from first_field's start {seen.Count}");
foreach (var (map, _, _) in all)
{
    var cells = walk.Where(p => p.Value && owner[p.Key] == map.MapId).Select(p => p.Key).ToList();
    var shut = cells.Where(c => !seen.Contains(c)).ToList();
    var isNew = made.Any(m => m.map == map);
    Console.WriteLine($"  {map.MapId,-25} walkable {cells.Count,4}  shut in {shut.Count}{(shut.Count > 0 ? ": " + string.Join(" ", shut.Take(12)) : "")}{(isNew ? "" : " (ver3.8, unchanged)")}");
    if (isNew && shut.Count > 0) failed = true;
}
// The eight new borders: walkable cells side by side across each, both reachable.
var borders = new (string a, string b)[]
{
    ("north_area_01", "northwest_transition_01"), ("west_area_01", "northwest_transition_01"),
    ("north_area_01", "northeast_transition_01"), ("east_area_01", "northeast_transition_01"),
    ("west_area_01", "southwest_transition_01"), ("south_area_01", "southwest_transition_01"),
    ("east_area_01", "southeast_transition_01"), ("south_area_01", "southeast_transition_01"),
};
foreach (var (a, b) in borders)
{
    var pairs = new List<string>();
    foreach (var p in walk.Where(p => p.Value && owner[p.Key] == a))
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var q = (p.Key.Item1 + dx, p.Key.Item2 + dy);
            if (owner.TryGetValue(q, out var o) && o == b && walk[q] && seen.Contains(p.Key) && seen.Contains(q)) pairs.Add($"{p.Key}|{q}");
        }
    Console.WriteLine($"border {a} <-> {b}: {pairs.Count} crossings");
    if (pairs.Count == 0) failed = true;
}
foreach (var (map, ox, oy, spec, trail) in made)
{
    Console.WriteLine($"--- {map.MapId} (world x {ox}..{ox + spec.Width - 1}, y {oy}..{oy + spec.Height - 1}); ':' trail");
    for (var y = spec.Height - 1; y >= 0; y--)
    {
        var line = new System.Text.StringBuilder();
        for (var x = 0; x < spec.Width; x++)
        {
            var c = new GridCell(x, y);
            var o = map.Objects.FirstOrDefault(p => p.RootCell == c);
            line.Append(o != null ? Glyph(o.DefinitionId) : trail.Contains(c) ? ':' : '.');
        }
        Console.WriteLine($"{y + oy,4} {line}");
    }
}
if (failed) { Console.WriteLine("NOT WRITTEN (errors above)"); return 1; }
if (write)
    foreach (var (map, _, _, _, _) in made)
    {
        var path = Path.Combine(folder, map.MapId + ".hwmap.json");
        MapFormat.SaveAtomic(map, path);
        var again = MapFormat.LoadMap(path);
        Console.WriteLine("written and read back by WORLD EDITOR Core: " + again.MapId + " " + again.Objects.Count + " objects, re-validated issues " +
            MapRules.Validate(again, catalog, worldDocs).Count(i => !i.IsWarning));
    }
return 0;

static IEnumerable<GridCell> AllCells(MapDocument map)
{
    for (var y = map.Bounds.MinY; y <= map.Bounds.MaxY; y++)
    for (var x = map.Bounds.MinX; x <= map.Bounds.MaxX; x++)
        yield return new GridCell(x, y);
}

static IEnumerable<GridCell> Neighbours(GridCell c) =>
    new[] { c with { X = c.X + 1 }, c with { X = c.X - 1 }, c with { Y = c.Y + 1 }, c with { Y = c.Y - 1 } };

static HashSet<GridCell> Flood(MapDocument map, CatalogDocument catalog, IEnumerable<GridCell> from)
{
    var seen = new HashSet<GridCell>(from.Where(c => map.Bounds.Contains(c) && !MapRules.BlocksMovement(map, catalog, c)));
    var queue = new Queue<GridCell>(seen);
    while (queue.Count > 0)
        foreach (var n in Neighbours(queue.Dequeue()))
            if (map.Bounds.Contains(n) && !MapRules.BlocksMovement(map, catalog, n) && seen.Add(n)) queue.Enqueue(n);
    return seen;
}

static char Glyph(string id) => id switch
{
    "tree_basic" => 'T', "forest_bush" => '#', "forest_fern" => 'f', "fallen_leaves" => 'l', "flower_blue" => 'b',
    "pond_reeds" => 'v', "stone_basic" => 'o', "flower_yellow" => 'y', "sunflower_cluster" => 'Y', "flower_basic" => '*',
    "south_rock_red" => 'R', "south_rock_small" => 'r', "south_grass_short" => 'g', "south_ground_accent" => 'a', _ => '?'
};

sealed record Mix(string DefinitionId, Func<double, double> Chance);

sealed record Spec(string MapId, string DisplayName, string Tag, int OffX, int OffY, int Width, int Height,
    string NeighbourA, string NeighbourB, Func<int, int, double> DistanceA, Func<int, int, double> DistanceB,
    Mix[] Mix, (int, int)[][] Trails, Func<double, string?> TrailDecor, double TrailDecorChance);

// A stable hash (FNV-1a, 32 bit) of the Map, the cell and a salt: the same on every run and machine.
static class Hash
{
    public static double Unit(string mapId, int x, int y, string salt)
    {
        var h = 2166136261u;
        foreach (var ch in $"{mapId}|{x}|{y}|{salt}") { h ^= ch; h *= 16777619u; }
        h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
        return (h & 0xFFFFFF) / (double)0x1000000;
    }

    // Value noise: a stable value at each whole (gx, gy), smoothly blended between (0..1).
    public static double Smooth(string mapId, double gx, double gy)
    {
        int x0 = (int)Math.Floor(gx), y0 = (int)Math.Floor(gy);
        double fx = gx - x0, fy = gy - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        double V(int x, int y) => Unit(mapId, x, y, "clump");
        return (V(x0, y0) * (1 - fx) + V(x0 + 1, y0) * fx) * (1 - fy) + (V(x0, y0 + 1) * (1 - fx) + V(x0 + 1, y0 + 1) * fx) * fy;
    }
}
