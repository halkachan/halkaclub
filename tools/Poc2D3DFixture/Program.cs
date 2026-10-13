// Fixtures made with the real MAP EDITOR v0.6 core (MapSession.MoveObject) on COPIES of
// first_field.hwmap.json. The official Map JSON is only read; its SHA-256 is checked.
//   first_field_tree_moved.hwmap.json   : Phase 1, one tree moved one cell
//   first_field_nature_moved.hwmap.json : Phase 2, one tree, one flower and one stone moved
using System.Security.Cryptography;
using HalkaWorldMapEditor.Core;

var project = ProjectPaths.FindNear(AppContext.BaseDirectory) ?? throw new Exception("Unity project not found");
var officialPath = Path.Combine(ProjectPaths.AuthoringFolder(project), "first_field.hwmap.json");
var fixtureFolder = Path.Combine(project, "POC2D3DFixtures");
const string treeId = "obj_b8083b939c355574929e534054d4ad43";
const string flowerId = "obj_3420672f46af5b9ba3d44af98a19925a";
const string stoneId = "obj_08973c34e708520f81aa843efd4e00c3";

string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var officialBefore = Hash(officialPath);
Directory.CreateDirectory(fixtureFolder);
var catalog = MapFormat.LoadCatalog(ProjectPaths.CatalogPath(project));

void Make(string fileName, params string[] instanceIds)
{
    var fixturePath = Path.Combine(fixtureFolder, fileName);
    File.Copy(officialPath, fixturePath, overwrite: true);
    var session = MapSession.Open(fixturePath, catalog);
    foreach (var instanceId in instanceIds)
    {
        var placement = session.Map.Objects.Single(item => item.InstanceId == instanceId);
        var from = placement.RootCell;
        GridCell? moved = null;
        foreach (var candidate in new[] { new GridCell(from.X + 1, from.Y), new GridCell(from.X - 1, from.Y),
                     new GridCell(from.X, from.Y + 1), new GridCell(from.X, from.Y - 1) })
        {
            if (!session.MoveObject(instanceId, candidate, out var reason))
            {
                Console.WriteLine($"SKIP {instanceId} {candidate}: {reason}");
                continue;
            }
            moved = candidate;
            break;
        }
        if (moved == null) throw new Exception("No adjacent cell accepted " + instanceId);
        Console.WriteLine($"FIXTURE {fileName} {placement.DefinitionId} {instanceId} {from} -> {moved}");
    }
    session.Save();
    var issues = MapRules.Validate(MapFormat.LoadMap(fixturePath), catalog);
    if (issues.Any(issue => !issue.IsWarning))
        throw new Exception("Fixture invalid: " + string.Join("; ", issues.Select(issue => issue.Message)));
}

Make("first_field_tree_moved.hwmap.json", treeId);
Make("first_field_nature_moved.hwmap.json", treeId, flowerId, stoneId);
if (Hash(officialPath) != officialBefore) throw new Exception("Official map changed");
Console.WriteLine($"OFFICIAL_SHA256 {officialBefore} unchanged");
