// Fixtures made with the real MAP EDITOR v0.6 core (MapSession.MoveObject) on COPIES of the
// official Map JSON. The official files are only read; their SHA-256 is checked.
//   first_field_tree_moved.hwmap.json        : Phase 1, one tree moved one cell
//   first_field_nature_moved.hwmap.json      : Phase 2, one tree, one flower and one stone moved
//   first_field_buildings_moved.hwmap.json   : Phase 3, the house, the bench, a sign and the well moved
//   halka_house_furniture_moved.hwmap.json   : Phase 3, every piece of furniture moved
using System.Security.Cryptography;
using HalkaWorldMapEditor.Core;

var project = ProjectPaths.FindNear(AppContext.BaseDirectory) ?? throw new Exception("Unity project not found");
var authoring = ProjectPaths.AuthoringFolder(project);
var fixtureFolder = Path.Combine(project, "POC2D3DFixtures");
const string treeId = "obj_b8083b939c355574929e534054d4ad43";
const string flowerId = "obj_3420672f46af5b9ba3d44af98a19925a";
const string stoneId = "obj_08973c34e708520f81aa843efd4e00c3";

string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var officials = new[] { "first_field", "halka_house" }.ToDictionary(id => id,
    id => Path.Combine(authoring, id + ".hwmap.json"));
var before = officials.ToDictionary(pair => pair.Key, pair => Hash(pair.Value));
Directory.CreateDirectory(fixtureFolder);
var catalog = MapFormat.LoadCatalog(ProjectPaths.CatalogPath(project));

// Tries the four neighbours, then two cells away: MAP EDITOR's own rules decide what is allowed.
var offsets = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2) };

void Make(string source, string fileName, params string[] instanceIds)
{
    var fixturePath = Path.Combine(fixtureFolder, fileName);
    File.Copy(officials[source], fixturePath, overwrite: true);
    var session = MapSession.Open(fixturePath, catalog);
    foreach (var instanceId in instanceIds)
    {
        var placement = session.Map.Objects.Single(item => item.InstanceId == instanceId);
        var from = placement.RootCell;
        GridCell? moved = null;
        foreach (var (dx, dy) in offsets)
        {
            var candidate = new GridCell(from.X + dx, from.Y + dy);
            if (!session.MoveObject(instanceId, candidate, out var reason))
            {
                Console.WriteLine($"SKIP {instanceId} {candidate}: {reason}");
                continue;
            }
            moved = candidate;
            break;
        }
        if (moved == null) throw new Exception("No nearby cell accepted " + instanceId);
        Console.WriteLine($"FIXTURE {fileName} {placement.DefinitionId} {instanceId} {from} -> {moved}");
    }
    session.Save();
    var issues = MapRules.Validate(MapFormat.LoadMap(fixturePath), catalog);
    if (issues.Any(issue => !issue.IsWarning))
        throw new Exception("Fixture invalid: " + string.Join("; ", issues.Select(issue => issue.Message)));
}

string[] Ids(string source, params string[] definitionIds)
{
    var map = MapFormat.LoadMap(officials[source]);
    return definitionIds.Select(definition => map.Objects.First(item => item.DefinitionId == definition).InstanceId)
        .ToArray();
}

Make("first_field", "first_field_tree_moved.hwmap.json", treeId);
Make("first_field", "first_field_nature_moved.hwmap.json", treeId, flowerId, stoneId);
Make("first_field", "first_field_buildings_moved.hwmap.json",
    Ids("first_field", "house_main", "bench_basic", "sign_basic", "well_basic"));
Make("halka_house", "halka_house_furniture_moved.hwmap.json",
    Ids("halka_house", "bed_basic", "desk_basic", "cushion_basic", "recording_mic_basic", "art_easel_basic",
        "studio_shelf_basic"));
foreach (var (id, path) in officials)
{
    if (Hash(path) != before[id]) throw new Exception("Official map changed: " + id);
    Console.WriteLine($"OFFICIAL_SHA256 {id} {before[id]} unchanged");
}
