// Phase 1 fixture: move one tree with the real MAP EDITOR v0.6 core (MapSession.MoveObject)
// on a COPY of first_field.hwmap.json. The official Map JSON is only read.
using System.Security.Cryptography;
using HalkaWorldMapEditor.Core;

var project = ProjectPaths.FindNear(AppContext.BaseDirectory) ?? throw new Exception("Unity project not found");
var officialPath = Path.Combine(ProjectPaths.AuthoringFolder(project), "first_field.hwmap.json");
var fixtureFolder = Path.Combine(project, "POC2D3DFixtures");
var fixturePath = Path.Combine(fixtureFolder, "first_field_tree_moved.hwmap.json");
const string treeId = "obj_b8083b939c355574929e534054d4ad43";

string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var officialBefore = Hash(officialPath);
Directory.CreateDirectory(fixtureFolder);
File.Copy(officialPath, fixturePath, overwrite: true);

var catalog = MapFormat.LoadCatalog(ProjectPaths.CatalogPath(project));
var session = MapSession.Open(fixturePath, catalog);
var tree = session.Map.Objects.Single(item => item.InstanceId == treeId);
var from = tree.RootCell;
GridCell? moved = null;
foreach (var candidate in new[] { new GridCell(from.X + 1, from.Y), new GridCell(from.X - 1, from.Y),
             new GridCell(from.X, from.Y + 1), new GridCell(from.X, from.Y - 1) })
{
    if (!session.MoveObject(treeId, candidate, out var reason))
    {
        Console.WriteLine($"SKIP {candidate}: {reason}");
        continue;
    }
    moved = candidate;
    break;
}
if (moved == null) throw new Exception("No adjacent cell accepted the tree");
session.Save();

var reloaded = MapFormat.LoadMap(fixturePath);
var issues = MapRules.Validate(reloaded, catalog);
if (issues.Any(issue => !issue.IsWarning))
    throw new Exception("Fixture invalid: " + string.Join("; ", issues.Select(issue => issue.Message)));
if (Hash(officialPath) != officialBefore) throw new Exception("Official map changed");
Console.WriteLine($"FIXTURE {treeId} {from} -> {moved} saved to {fixturePath}");
Console.WriteLine($"OFFICIAL_SHA256 {officialBefore} unchanged");
