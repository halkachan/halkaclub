using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.0 Phase 2: ground, road, grass and nature objects.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase2EnvironmentChecks.RunBatch
    public static class Phase2EnvironmentChecks
    {
        private static int passes;
        private static readonly List<string> failures = new List<string>();
        private static readonly Dictionary<string, string> Messages = new Dictionary<string, string>
            { ["tree_basic"] = "き。", ["flower_basic"] = "はな。", ["stone_basic"] = "いし。" };

        [MenuItem("HALKA/POC 2D3D/Run Phase 2 Environment Checks")]
        public static void RunFromMenu() => Run();

        public static void RunBatch()
        {
            var ok = false;
            try { ok = Run(); }
            catch (Exception exception) { Debug.LogException(exception); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            passes = 0;
            failures.Clear();
            AssetChecks();
            var holder = Open(out var c);
            try
            {
                GroundChecks(c);
                GrassChecks(c);
                InstanceChecks(c);
                InteractionAndPickingChecks(c);
                SwitchChecks(c);
                FixtureChecks(c);
                TimeChecks(c);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
            foreach (var failure in failures) Debug.LogError("PHASE2 FAIL " + failure);
            Debug.Log($"HARUKA Phase 2 environment checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            if (condition) { passes++; Debug.Log("PHASE2 PASS " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add(name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- assets and import

        private static void AssetChecks()
        {
            var root = PocSceneBuilder.RepositoryRoot;
            var manifestPath = Path.Combine(root, "blender", "exports", "phase2_assets_manifest.json");
            var manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "";
            Check("assets: Phase 2 manifest exists", manifest.Length > 0, manifestPath);
            foreach (var family in new[] { "ground_grass", "ground_dirt", "grass_tuft", "tree_basic", "flower_basic", "stone_basic" })
                Check($"assets: Blender source {family}.blend", File.Exists(Path.Combine(root, "blender", "sources", "phase2", family + ".blend")));
            Check("assets: Blender build and preview scripts kept",
                File.Exists(Path.Combine(root, "blender", "scripts", "build_phase2_environment.py")) &&
                File.Exists(Path.Combine(root, "blender", "scripts", "render_phase2_previews.py")));
            var toon = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            foreach (var name in PocSceneBuilder.Phase2ModelNames)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.Phase2Models + name + ".fbx");
                Check($"import {name}: FBX imported", model != null);
                if (model == null) continue;
                var filter = model.GetComponentInChildren<MeshFilter>();
                var mesh = filter.sharedMesh;
                var t = filter.transform;
                var renderer = filter.GetComponent<MeshRenderer>();
                Check($"import {name}: scale 1, no rotation, origin at root", (t.lossyScale - Vector3.one).sqrMagnitude < 1e-8f &&
                    Quaternion.Angle(t.rotation, Quaternion.identity) < 0.01f && t.position.sqrMagnitude < 1e-8f);
                Check($"import {name}: one material, the shared vertex-color toon", renderer.sharedMaterials.Length == 1 &&
                    renderer.sharedMaterial == toon);
                Check($"import {name}: vertex colors and normals", mesh.colors32.Length == mesh.vertexCount &&
                    mesh.normals.Length == mesh.vertexCount);
                Check($"import {name}: triangle count matches the Blender manifest",
                    manifest.Contains($"\"name\": \"{name}\",") && ManifestTriangles(manifest, name) == mesh.triangles.Length / 3,
                    (mesh.triangles.Length / 3).ToString());
                var b = mesh.bounds;
                if (name.StartsWith("ground_grass"))
                    Check($"import {name}: exactly one cell at ground height", Mathf.Abs(b.size.x - 0.5f) < 1e-4f &&
                        Mathf.Abs(b.size.z - 0.5f) < 1e-4f && Mathf.Abs(b.max.y) < 1e-5f && Vector3.Distance(b.center, Vector3.zero) < 1e-4f,
                        b.ToString("F3"));
                else if (name.StartsWith("ground_dirt"))
                    Check($"import {name}: inside the NE quarter, 4 mm above grass", b.min.x > -1e-4f && b.min.z > -1e-4f &&
                        b.max.x < 0.2501f && b.max.z < 0.2501f && Mathf.Abs(b.min.y - 0.004f) < 1e-4f && Mathf.Abs(b.max.y - 0.004f) < 1e-4f,
                        b.ToString("F3"));
                else if (name.StartsWith("grass_tuft"))
                    Check($"import {name}: small tuft inside its cell", b.max.y < 0.25f && b.min.y > -1e-4f &&
                        Mathf.Abs(b.min.x) < 0.2f && Mathf.Abs(b.max.x) < 0.2f && Mathf.Abs(b.min.z) < 0.2f && Mathf.Abs(b.max.z) < 0.2f,
                        b.ToString("F3"));
                else if (name == "tree_basic")
                {
                    Check("import tree_basic: about three cells wide, 1.6-1.9 tall, root flare sunk under 4 cm",
                        b.size.x <= 1.5f && b.size.x >= 1.2f && b.max.y > 1.6f && b.max.y < 1.9f && b.min.y > -0.04f && b.min.y <= 0.001f,
                        b.ToString("F2"));
                    var trunk = Centroid(mesh, new Color32(0xa8, 0x73, 0x5a, 255));
                    Check("import tree_basic: trunk on the root cell centre", trunk.HasValue &&
                        new Vector2(trunk.Value.x, trunk.Value.z).magnitude < 0.06f, trunk?.ToString("F3") ?? "none");
                    var anchor = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "PerchAnchor");
                    Check("import tree_basic: Blender PerchAnchor exported (future Crow perch)", anchor != null &&
                        Vector3.Distance(anchor.position, new Vector3(-0.17f, 1.7f, 0f)) < 0.01f, anchor?.position.ToString("F3") ?? "missing");
                }
                else if (name == "flower_basic")
                {
                    Check("import flower_basic: fits its 1x1 cell, under half a cell tall",
                        b.min.x > -0.25f && b.max.x < 0.25f && b.min.z > -0.25f && b.max.z < 0.25f && b.max.y < 0.45f, b.ToString("F3"));
                    var facing = AverageNormal(mesh, new Color32(0xf6, 0xbc, 0x03, 255));
                    Check("import flower_basic: flower face tilted up and toward the South (both cameras)",
                        facing.y > 0.5f && facing.z < -0.2f, facing.ToString("F2"));
                }
                else if (name.StartsWith("stone_basic"))
                    Check($"import {name}: fits its 1x1 cell, low and on the ground", b.min.x > -0.25f && b.max.x < 0.25f &&
                        b.min.z > -0.25f && b.max.z < 0.25f && b.max.y < 0.35f && Mathf.Abs(b.min.y) < 1e-3f, b.ToString("F3"));
            }
        }

        private static int ManifestTriangles(string manifest, string name)
        {
            var at = manifest.IndexOf($"\"name\": \"{name}\",", StringComparison.Ordinal);
            var key = manifest.IndexOf("\"triangles\": ", at, StringComparison.Ordinal) + 13;
            var end = manifest.IndexOf(',', key);
            return int.Parse(manifest.Substring(key, end - key));
        }

        // Average normal of the triangles of one color (face normals from positions).
        private static Vector3 AverageNormal(Mesh mesh, Color32 color)
        {
            var colors = mesh.colors32;
            var v = mesh.vertices;
            var t = mesh.triangles;
            var sum = Vector3.zero;
            for (var i = 0; i < t.Length; i += 3)
            {
                var c = colors[t[i]];
                if (Mathf.Abs(c.r - color.r) > 2 || Mathf.Abs(c.g - color.g) > 2 || Mathf.Abs(c.b - color.b) > 2) continue;
                sum += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            }
            return sum.normalized;
        }

        private static Vector3? Centroid(Mesh mesh, Color32 color)
        {
            var colors = mesh.colors32;
            var vertices = mesh.vertices;
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < colors.Length; i++)
                if (Mathf.Abs(colors[i].r - color.r) <= 2 && Mathf.Abs(colors[i].g - color.g) <= 2 &&
                    Mathf.Abs(colors[i].b - color.b) <= 2) { sum += vertices[i]; count++; }
            return count > 0 ? sum / count : (Vector3?)null;
        }

        // ---------------------------------------------------------------- scene

        private sealed class Ctx
        {
            public SharedWorldSession Session;
            public PocWorldPresenter Presenter;
            public PocViewSwitcher Switcher;
            public PocPixelView2D Pixel;
            public PocThirdPersonCamera Third;
            public PocActorView Actor;
            public PocTimeOfDay Time;
            public PocMapJson.MapDto Json;
        }

        private static GameObject Open(out Ctx c)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var screen = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ScreenMaterialPath);
            var root = PocSceneBuilder.BuildSceneContent(catalog, screen);
            c = new Ctx
            {
                Session = root.GetComponent<SharedWorldSession>(),
                Presenter = root.GetComponent<PocWorldPresenter>(),
                Switcher = root.GetComponent<PocViewSwitcher>(),
                Time = root.GetComponent<PocTimeOfDay>(),
                Pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include),
                Third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include),
                Actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include),
                Json = PocMapJson.Load(Path.Combine(Application.dataPath, "Content/Maps/Authoring/first_field.hwmap.json"))
            };
            c.Session.Initialize();
            c.Presenter.Rebuild(c.Session.World);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Render(c);
            var holder = new GameObject("Phase 2 check holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        private static void Render(Ctx c, int width = 1024, int height = 576)
        {
            c.Actor.Apply(0.016f);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(0.016f, width, height);
            else c.Third.UpdateView(0.016f);
            c.Actor.Apply(0.016f);
        }

        // ---------------------------------------------------------------- ground and road

        private static void GroundChecks(Ctx c)
        {
            var map = c.Session.World.Map;
            var env = c.Presenter.Environment;
            Check("ground: first_field dressed with the Blender environment", env != null && c.Presenter.GroundObject != null);
            if (env == null) return;
            var margin = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath).Environment.outsideMargin;
            var expectedTiles = (map.MaxCell.x - map.MinCell.x + 1 + 2 * margin) * (map.MaxCell.y - map.MinCell.y + 1 + 2 * margin);
            Check("ground: one Blender grass tile per cell from Map Bounds (+outside margin)", env.GrassTiles == expectedTiles,
                $"{env.GrassTiles} tiles, bounds {map.MinCell}..{map.MaxCell}, margin {margin}");
            var jsonDirt = new HashSet<Vector2Int>(c.Json.surfaces.Where(s => s.definitionId == "dirt").Select(s => s.cell.Cell));
            var insideRoad = new HashSet<Vector2Int>(env.RoadCellSet.Where(map.Contains));
            Check("road: every Dirt Surface cell of Map JSON is a road cell, and no other cell inside the map",
                insideRoad.SetEquals(jsonDirt), $"{jsonDirt.Count} dirt cells");
            Check("road: each road cell is dressed by four quarter pieces",
                env.RoadFull + env.RoadEdge + env.RoadCorner == env.RoadCellSet.Count * 4,
                $"full {env.RoadFull}, edge {env.RoadEdge}, corner {env.RoadCorner}");
            var markers = new[] { "road_north", "road_east", "road_south", "road_west" };
            var continued = markers.All(id => map.TryGetMarker(id, out var cell) && env.RoadCellSet.Contains(cell) &&
                env.RoadCellSet.Contains(cell + Outward(map, cell)));
            Check("road: the four road ends continue off the map (markers unchanged)", continued &&
                env.RoadCellsOutside == markers.Length * margin, $"{env.RoadCellsOutside} cells outside");
            var mesh = c.Presenter.GroundObject.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var heightsOk = vertices.All(v => Mathf.Abs(v.y) < 1e-4f || Mathf.Abs(v.y - 0.004f) < 1e-4f);
            Check("ground: every ground vertex is at y 0 (grass) or 0.004 (road): same height as the player's feet", heightsOk);
            var triangles = mesh.triangles;
            var roadVerticesInRoadCells = true;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                if (Mathf.Abs(a.y - 0.004f) > 1e-4f) continue;
                var centroid = (a + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                roadVerticesInRoadCells &= env.RoadCellSet.Contains(GridSpace3D.ToCell(centroid));
            }
            Check("road: road geometry stays inside road cells (walkable area unchanged)", roadVerticesInRoadCells);
            Check("ground: no collider on the ground or road mesh", c.Presenter.GroundObject.GetComponent<Collider>() == null);
            Check("ground: Map Bounds and Grid cells are unchanged by the dressing",
                c.Session.World.MinCell == new Vector2Int(c.Json.bounds.minX, c.Json.bounds.minY) &&
                c.Session.World.MaxCell == new Vector2Int(c.Json.bounds.maxX, c.Json.bounds.maxY));
        }

        private static Vector2Int Outward(MapDefinition map, Vector2Int cell) =>
            cell.y == map.MaxCell.y ? Vector2Int.up : cell.y == map.MinCell.y ? Vector2Int.down :
            cell.x == map.MaxCell.x ? Vector2Int.right : Vector2Int.left;

        // ---------------------------------------------------------------- grass

        private static void GrassChecks(Ctx c)
        {
            var env = c.Presenter.Environment;
            var map = c.Session.World.Map;
            if (env == null) return;
            var cells = env.TuftPositions.Select(GridSpace3D.ToCell).ToList();
            Check("grass: tufts exist and stay on grass cells only (never on road)", env.Tufts > 50 &&
                cells.All(cell => !env.RoadCellSet.Contains(cell)), $"{env.Tufts} tufts, {env.RoadsideTufts} along roads");
            Check("grass: tufts only where the 2D rule grows grass (MapPlacementRules.HasGrass)",
                cells.All(cell => MapPlacementRules.HasGrass(map, cell)));
            // ver3.7: walkable decoration (the West forest's ferns and fallen leaves: no blocked cell) grows
            // grass under it by the game's own rule (MapPlacementRules.HasGrass, the same in 2D); every
            // Object that blocks or keeps grass away still has none.
            // ver3.9: as the rule has it (MapPlacementRules.Covers: an Object keeps grass away from its blocked cells), a walkable
            // Object marked excludeGrass (the North's waterside reeds, pond_reeds) has grass under it like the ferns; the more
            // reeds of the transition regions showed it. Blocking Objects still have none.
            Check("grass: no tuft on a blocked cell or an Object root", cells.All(cell => !c.Session.World.IsBlocked(cell) &&
                c.Session.World.Objects.Where(o => o.Definition == null || o.Definition.BlocksMovement).All(o => o.RootCell != cell)));
            Check("grass: tufts have no collider and are not pickable Objects",
                c.Presenter.TuftObject.GetComponent<Collider>() == null && c.Presenter.TuftObject.GetComponent<PocObjectView>() == null);
            var density = env.Tufts / (float)Enumerable.Range(map.MinCell.x, map.MaxCell.x - map.MinCell.x + 1)
                .SelectMany(x => Enumerable.Range(map.MinCell.y, map.MaxCell.y - map.MinCell.y + 1), (x, y) => new Vector2Int(x, y))
                .Count(cell => MapPlacementRules.HasGrass(map, cell));
            Check("grass: not overcrowded (about one tuft per grass cell)", density > 0.6f && density < 1.6f, density.ToString("F2"));
            var first = env.TuftPositions.ToList();
            var firstVertices = c.Presenter.GroundObject.GetComponent<MeshFilter>().sharedMesh.vertices;
            c.Presenter.Rebuild(c.Session.World);
            var again = c.Presenter.Environment;
            Check("grass: rebuilding the same Map gives the same tufts and ground (deterministic)",
                first.SequenceEqual(again.TuftPositions) &&
                firstVertices.SequenceEqual(c.Presenter.GroundObject.GetComponent<MeshFilter>().sharedMesh.vertices));
        }

        // ---------------------------------------------------------------- instances

        private static void InstanceChecks(Ctx c)
        {
            var world = c.Session.World;
            foreach (var id in new[] { "tree_basic", "flower_basic", "stone_basic" })
            {
                var json = c.Json.objects.Where(o => o.definitionId == id).ToList();
                var ok = json.All(o =>
                {
                    var view = c.Presenter.FindView(o.instanceId);
                    return view != null && !view.IsFallback && !view.Placeholder && view.RootCell == o.rootCell.Cell &&
                        (view.transform.position - GridSpace3D.ToWorld(o.rootCell.Cell)).sqrMagnitude < 1e-8f &&
                        view.ModelName != null && view.ModelName.StartsWith(id) &&
                        world.IsBlocked(o.rootCell.Cell);
                });
                Check($"instances: every {id} of Map JSON has its Blender model at its Root Cell, blocked root ({json.Count})",
                    ok && json.Count == world.Objects.Count(o => o.DefinitionId == id));
            }
            var trees = world.Objects.Where(o => o.DefinitionId == "tree_basic").ToList();
            var crownFree = trees.All(t => !world.IsBlocked(t.RootCell + Vector2Int.up) || world.Objects.Any(o => o.RootCell == t.RootCell + Vector2Int.up));
            Check("collision: the wide tree crown does not block its neighbours (root cell only)",
                trees.All(t => t.Definition.EffectiveBlockedOffsets().SequenceEqual(new[] { Vector2Int.zero })) && crownFree);
            Check("trees: every tree has a PerchAnchor for the future 3D Crow perch",
                trees.All(t => c.Presenter.FindView(t.InstanceId).PerchAnchor != null &&
                    c.Presenter.FindView(t.InstanceId).PerchAnchor.position.y > 1.4f));
            var stoneViews = world.Objects.Where(o => o.DefinitionId == "stone_basic").Select(o => c.Presenter.FindView(o.InstanceId)).ToList();
            var variants = stoneViews.Select(v => v.ModelName).Distinct().Count();
            var before = stoneViews.ToDictionary(v => v.InstanceId, v => v.ModelName);
            c.Presenter.Rebuild(world);
            var stable = before.All(pair => c.Presenter.FindView(pair.Key).ModelName == pair.Value);
            Check("stones: variants chosen from the Instance ID, the same after a rebuild", variants >= 2 && stable,
                string.Join(", ", before.Values.GroupBy(v => v).Select(g => g.Key + "x" + g.Count())));
            // Phase 3 replaced the Phase 1 placeholder house with the Blender model; this check now
            // confirms that state (it used to confirm the house was still the placeholder).
            Check("house: the Phase 3 Blender model replaced the Phase 1 placeholder",
                world.Objects.Where(o => o.DefinitionId == "house_main").All(o =>
                    !c.Presenter.FindView(o.InstanceId).Placeholder && !c.Presenter.FindView(o.InstanceId).IsFallback &&
                    c.Presenter.FindView(o.InstanceId).ModelName == "house_main"));
        }

        // ---------------------------------------------------------------- interaction

        private static void InteractionAndPickingChecks(Ctx c)
        {
            var s = c.Session;
            foreach (var id in Messages.Keys)
            {
                var all = s.World.Objects.Where(o => o.DefinitionId == id).ToList();
                var answered = 0;
                var unreachable = 0;
                foreach (var o in all)
                {
                    var front = o.RootCell + Vector2Int.down;
                    if (!s.World.CanEnter(front)) { unreachable++; continue; }
                    s.PlaceForCheck(front, FacingDirection.Up);
                    if (s.InteractAhead() && s.LastMessage == Messages[id]) answered++;
                }
                Check($"interaction: every {id} with a walkable front answers \"{Messages[id]}\"",
                    answered == all.Count - unreachable && answered > 0, $"{answered} answered, {unreachable} fronts blocked");
            }
            foreach (var (id, height) in new[] { ("flower_basic", 0.25f), ("stone_basic", 0.12f), ("tree_basic", 1.0f) })
            {
                var target = s.World.Objects.First(o => o.DefinitionId == id && s.World.CanEnter(o.RootCell + Vector2Int.down * 2));
                var point = GridSpace3D.ToWorld(target.RootCell, height);
                s.PlaceForCheck(target.RootCell + Vector2Int.down * 2, FacingDirection.Up);
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                Render(c);
                var rt = c.Pixel.WorldCamera.WorldToScreenPoint(point);
                var picked2D = c.Switcher.PickObject(new Vector2(rt.x * c.Pixel.Scale - c.Pixel.Offset.x, rt.y * c.Pixel.Scale - c.Pixel.Offset.y));
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                c.Third.SnapToTarget();
                Render(c);
                var picked3D = c.Switcher.PickObject(c.Third.ViewCamera.WorldToScreenPoint(point));
                Check($"picking: clicking the same {id} in 2D and 3D selects the same Object Instance",
                    picked2D?.InstanceId == target.InstanceId && picked3D?.InstanceId == target.InstanceId,
                    $"{picked2D?.InstanceId} / {picked3D?.InstanceId}");
            }
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- switching

        private static void SwitchChecks(Ctx c)
        {
            var s = c.Session;
            var ok = true;
            var dirs = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            var groundBefore = c.Presenter.GroundObject;
            for (var i = 0; i < 20; i++)
            {
                s.Tick(0f, dirs[i % 4]);
                for (var f = 0; f < 12; f++) s.Tick(0.02f, Vector2Int.zero);
                var state = (s.Cell, s.Facing, s.CompletedSteps, s.MapLoads);
                c.Switcher.Toggle();
                Render(c);
                ok &= state == (s.Cell, s.Facing, s.CompletedSteps, s.MapLoads);
            }
            Check("switch: 20 toggles keep the gameplay state and rebuild nothing",
                ok && c.Presenter.GroundObject == groundBefore);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- MAP EDITOR fixture

        private static void FixtureChecks(Ctx c)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "POC2D3DFixtures", "first_field_nature_moved.hwmap.json"));
            Check("fixture: MAP EDITOR core moved a tree, a flower and a stone on a copy", File.Exists(path), path);
            if (!File.Exists(path)) return;
            var official = new SharedWorldModel(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath));
            var fixture = PocMapJson.ToDefinition(PocMapJson.Load(path));
            try
            {
                c.Session.UseOutdoorMapForTest(fixture);
                c.Presenter.Rebuild(c.Session.World);
                foreach (var id in new[] { "obj_b8083b939c355574929e534054d4ad43", "obj_3420672f46af5b9ba3d44af98a19925a",
                             "obj_08973c34e708520f81aa843efd4e00c3" })
                {
                    var from = official.FindInstance(id).RootCell;
                    var to = c.Session.World.FindInstance(id).RootCell;
                    var view = c.Presenter.FindView(id);
                    c.Switcher.SetMode(PocViewMode.PixelView2D);
                    Render(c);
                    var at2D = view.transform.position;
                    c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                    Render(c);
                    var moved = (to - from).sqrMagnitude == 1 && at2D == view.transform.position &&
                        (at2D - GridSpace3D.ToWorld(to)).sqrMagnitude < 1e-8f;
                    var collision = c.Session.World.IsBlocked(to) && !c.Session.World.IsBlocked(from);
                    var grass = !c.Presenter.Environment.TuftPositions.Select(GridSpace3D.ToCell).Contains(to);
                    Check($"fixture: {official.FindInstance(id).DefinitionId} moved {from}->{to} in 2D and 3D, collision followed, no grass under it",
                        moved && collision && grass);
                }
            }
            finally
            {
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                UnityEngine.Object.DestroyImmediate(fixture);
                c.Session.UseOutdoorMapForTest(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath));
                c.Presenter.Rebuild(c.Session.World);
            }
        }

        // ---------------------------------------------------------------- time

        private static void TimeChecks(Ctx c)
        {
            var t = c.Time;
            t.Override = PocPhaseOverride.Morning;
            t.Apply(0f);
            t.Override = PocPhaseOverride.Night;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            t.Apply(1f);
            t.Apply(1f + WorldNightOverlay2D.FadeSeconds + 0.1f);
            Check("night 2D: same overlay as the shipped game (outdoor alpha 0.33)",
                Mathf.Abs(t.PixelOverlayAlpha - WorldNightOverlay2D.OutdoorNightAlpha) < 0.001f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            t.Apply(10f);
            var ambient = RenderSettings.ambientLight;
            Check("night 3D: scenery stays visible (ambient and sun not black)",
                ambient.maxColorComponent > 0.25f && t.PixelOverlayAlpha == 0f, ambient.ToString());
            t.Override = PocPhaseOverride.Auto;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
        }
    }
}
