using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.0 Phase 3: the Blender house, outdoor fixtures, interior and furniture.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase3BuildingFurnitureChecks.RunBatch
    public static class Phase3BuildingFurnitureChecks
    {
        private static int passes;
        private static readonly List<string> failures = new List<string>();
        private static float clock;
        private const int ScreenW = 1280, ScreenH = 720;

        // Phase 4 replaced the bench's and cushion's placeholder "すわる。" message with the real
        // seat (the 2D game's PlayerSeatController rule), checked below as a seat, not a message.
        private static readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["desk_basic"] = "つくえ。", ["well_basic"] = "いど。", ["bed_basic"] = "ベッド。"
        };
        private static readonly Dictionary<string, string> SeatPoses = new Dictionary<string, string>
        {
            ["bench_basic"] = "bench_sit", ["cushion_basic"] = "cushion_sit"
        };

        [MenuItem("HALKA/POC 2D3D/Run Phase 3 Building and Furniture Checks")]
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
                PlacementChecks(c);
                CatalogChecks(c);
                OutdoorChecks(c);
                EntranceChecks(c);
                InteriorChecks(c);
                InteriorCameraChecks(c);
                TwoDChecks(c);
                InteractionChecks(c);
                ClickChecks(c);
                SwitchSequenceChecks(c);
                NightChecks(c);
                FixtureChecks(c);
                ProtectionChecks();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
            foreach (var failure in failures) Debug.LogError("PHASE3 FAIL " + failure);
            Debug.Log($"HARUKA Phase 3 building and furniture checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            if (condition) { passes++; Debug.Log("PHASE3 PASS " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add(name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness

        private sealed class Ctx
        {
            public SharedWorldSession Session;
            public PocWorldPresenter Presenter;
            public PocViewSwitcher Switcher;
            public PocPixelView2D Pixel;
            public PocThirdPersonCamera Third;
            public PocActorView Actor;
            public PocTimeOfDay Time;
            public PocInput Input;
            public PocPresentationCatalog Catalog;
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
                Input = root.GetComponent<PocInput>(),
                Catalog = catalog,
                Pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include),
                Third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include),
                Actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include)
            };
            c.Session.Initialize();
            c.Presenter.Rebuild(c.Session.World);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 0.016f, Vector2Int.zero);
            var holder = new GameObject("Phase 3 check holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        private static void Frame(Ctx c, float dt, Vector2Int raw)
        {
            clock += dt;
            c.Input.SetRaw(raw, clock);
            c.Session.Tick(dt, c.Input.ResolveDirection(c.Session));
            if (c.Presenter.BuiltMapId != c.Session.World.MapId)
            {
                // What SharedWorldSession.MapChanged does at runtime (events are off in Edit Mode).
                c.Presenter.Rebuild(c.Session.World);
                c.Third.SnapToTarget();
                c.Pixel.SnapToTarget();
            }
            c.Actor.Apply(dt);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(dt, ScreenW, ScreenH);
            else
            {
                c.Third.ViewCamera.aspect = ScreenW / (float)ScreenH;
                c.Third.UpdateView(dt, clock);
            }
        }

        private static void Settle(Ctx c, float seconds = 0.6f)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
        }

        private static List<Vector2Int> Hold(Ctx c, Vector2Int raw, float seconds)
        {
            var cells = new List<Vector2Int>();
            for (var t = 0f; t < seconds; t += 1f / 60f)
            {
                var before = c.Session.Cell;
                Frame(c, 1f / 60f, raw);
                if (c.Session.Cell != before) cells.Add(c.Session.Cell);
            }
            Settle(c, 0.3f);
            return cells;
        }

        // Holds a key until the condition is met (then releases), like a player who lets go as
        // soon as the door opens; at most maxSeconds.
        private static void HoldUntil(Ctx c, Vector2Int raw, Func<bool> done, float maxSeconds = 3f)
        {
            for (var t = 0f; t < maxSeconds && !done(); t += 1f / 60f) Frame(c, 1f / 60f, raw);
            Settle(c, 0.3f);
        }

        private static void Place(Ctx c, Vector2Int cell, FacingDirection facing, PocViewMode mode)
        {
            Settle(c, 0.05f);
            c.Session.PlaceForCheck(cell, facing);
            c.Switcher.SetMode(mode);
            if (mode == PocViewMode.ThirdPerson3D) c.Third.SnapToTarget();
            else c.Pixel.SnapToTarget();
            Settle(c, 0.6f);
        }

        private static void Enter(Ctx c, bool interior, Vector2Int cell, FacingDirection facing)
        {
            c.Session.LoadMapForCheck(interior, cell, facing);
            c.Presenter.Rebuild(c.Session.World);
            c.Third.SnapToTarget();
            c.Pixel.SnapToTarget();
            Settle(c, 0.3f);
        }

        private static bool WalkTo(Ctx c, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            if (!GridPathfinder2D.TryFind(null, c.Session.Cell, goal, path, c.Session.World.CanEnter)) return false;
            var start = c.Session.World;
            foreach (var cell in path)
            {
                if (c.Session.World != start) break;
                var direction = cell - c.Session.Cell;
                if (direction == Vector2Int.zero) continue;
                for (var guard = 0; guard < 60 && c.Session.Cell != cell && c.Session.World == start; guard++)
                    Frame(c, 1f / 60f, Vector2Int.RoundToInt((Vector2)direction));
                Settle(c, 0.1f);
            }
            return true;
        }

        private static Color32[] ReadPixels(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var pixels = texture.GetPixels32();
            UnityEngine.Object.DestroyImmediate(texture);
            return pixels;
        }

        private static PocObjectView ViewOf(Ctx c, string definitionId) =>
            c.Presenter.Views.FirstOrDefault(v => v != null && v.DefinitionId == definitionId && !v.IsWall);

        private static Bounds BoundsOf(PocObjectView view)
        {
            var bounds = view.Renderers[0].bounds;
            foreach (var item in view.Renderers) bounds.Encapsulate(item.bounds);
            return bounds;
        }

        // ---------------------------------------------------------------- Blender assets

        private static void AssetChecks()
        {
            var root = PocSceneBuilder.RepositoryRoot;
            var manifestPath = Path.Combine(root, "blender/exports/phase3_assets_manifest.json");
            Check("assets: Blender script, preview script and manifest exist",
                File.Exists(Path.Combine(root, "blender/scripts/build_phase3_buildings.py")) &&
                File.Exists(Path.Combine(root, "blender/scripts/render_phase3_previews.py")) && File.Exists(manifestPath));
            var manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "";
            var missing = new List<string>();
            var tris = new List<string>();
            var scaleOk = true;
            var normalsOk = true;
            var colorsOk = true;
            var materialOk = true;
            var toon = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            foreach (var name in PocSceneBuilder.Phase3ModelNames)
            {
                var path = PocSceneBuilder.Phase3Models + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null || !manifest.Contains($"\"name\": \"{name}\"")) { missing.Add(name); continue; }
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                scaleOk &= Mathf.Approximately(importer.globalScale, 1f) && (model.transform.lossyScale - Vector3.one).sqrMagnitude < 1e-8f;
                var count = 0;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    count += mesh.triangles.Length / 3;
                    normalsOk &= NormalsConsistent(mesh);
                    var colors = mesh.colors32;
                    colorsOk &= colors.Length == mesh.vertexCount && colors.All(col => col.a == 255) &&
                                colors.Any(col => col.r != 255 || col.g != 255 || col.b != 255);
                    var renderer = filter.GetComponent<MeshRenderer>();
                    materialOk &= renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == toon;
                }
                var expected = ManifestTriangles(manifest, name);
                if (expected != count) missing.Add($"{name}: Unity {count} tris, manifest {expected}");
                tris.Add($"{name} {count}");
            }
            Check("assets: every Phase 3 model is imported and matches its manifest entry (triangle counts)",
                missing.Count == 0, missing.Count > 0 ? string.Join(", ", missing) : string.Join(", ", tris));
            foreach (var family in new[] { "house_main", "bench_basic", "sign_basic", "well_basic", "interior", "bed_basic",
                         "desk_basic", "cushion_basic", "recording_mic_basic", "art_easel_basic", "studio_shelf_basic" })
                if (!File.Exists(Path.Combine(root, $"blender/sources/phase3/{family}.blend"))) missing.Add(family + ".blend");
            var previews = PocSceneBuilder.Phase3ModelNames.All(n => new[] { "south_front", "east", "north_back", "west", "top" }
                .All(v => File.Exists(Path.Combine(root, $"blender/exports/previews/phase3/{n}_{v}.png"))));
            Check("assets: a .blend source per family and front/side/back/top previews per model", missing.Count == 0 && previews,
                string.Join(", ", missing));
            Check("assets: imported at scale 1, no rescale", scaleOk);
            Check("assets: normals agree with the triangle winding (outward faces)", normalsOk);
            Check("assets: vertex colors on every vertex (opaque, not blank)", colorsOk);
            Check("assets: one shared vertex-color material per part", materialOk);
            var house = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.Phase3Models + "house_main.fbx");
            var names = house.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            Check("assets: the house has separate roof, walls and door parts plus door and entrance anchors",
                new[] { "house_main", "HouseRoof", "HouseDoor", "DoorAnchor", "EntranceAnchor" }.All(names.Contains),
                string.Join(",", names));
            var bench = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.Phase3Models + "bench_basic.fbx");
            var seats = bench.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("SeatAnchor")).ToList();
            Check("assets: bench seat anchors over both cells at seat height (for a future sit pose)",
                seats.Count == 2 && seats.All(s => Mathf.Abs(s.localPosition.y - 0.22f) < 0.01f) &&
                seats.Select(s => Mathf.Round(s.localPosition.x * 2f)).OrderBy(x => x).SequenceEqual(new[] { 0f, 1f }),
                string.Join(" ", seats.Select(s => s.name + s.localPosition.ToString("F2"))));
        }

        private static int ManifestTriangles(string manifest, string name)
        {
            var at = manifest.IndexOf($"\"name\": \"{name}\"", StringComparison.Ordinal);
            if (at < 0) return -1;
            var key = manifest.IndexOf("\"triangles\": ", at, StringComparison.Ordinal);
            var end = manifest.IndexOf(',', key);
            return int.Parse(manifest.Substring(key + 13, end - key - 13).Trim());
        }

        private static bool NormalsConsistent(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            if (n.Length != v.Length) return false;
            for (var i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (face.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) <= 0f) return false;
                if (Mathf.Abs(n[t[i]].magnitude - 1f) > 0.01f) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- placement

        private static void PlacementChecks(Ctx c)
        {
            foreach (var interior in new[] { false, true })
            {
                if (interior) Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
                var world = c.Session.World;
                var ok = true;
                var detail = new List<string>();
                foreach (var o in world.Objects)
                {
                    var view = c.Presenter.FindView(o.InstanceId);
                    var good = view != null && view.DefinitionId == o.DefinitionId && view.RootCell == o.RootCell &&
                               (view.transform.position - GridSpace3D.ToWorld(o.RootCell)).sqrMagnitude < 1e-8f;
                    ok &= good;
                    if (!good) detail.Add(o.InstanceId);
                }
                Check($"placement ({world.MapId}): every Instance ID / Definition ID / Root Cell matches Map JSON",
                    ok && world.Objects.Count > 0, string.Join(",", detail));
                // The drawn model stays inside its Definition's footprint (+ a small overhang).
                var inside = true;
                foreach (var o in world.Objects.Where(o => PocSceneBuilder.Phase3Definitions.Contains(o.DefinitionId)))
                {
                    var b = BoundsOf(c.Presenter.FindView(o.InstanceId));
                    var cells = FootprintCells(o).ToList();
                    var min = GridSpace3D.ToWorld(new Vector2(cells.Min(p => p.x), cells.Min(p => p.y))) - new Vector3(0.25f, 0f, 0.25f);
                    var max = GridSpace3D.ToWorld(new Vector2(cells.Max(p => p.x), cells.Max(p => p.y))) + new Vector3(0.25f, 0f, 0.25f);
                    const float overhang = 0.15f;
                    var good = b.min.x >= min.x - overhang && b.max.x <= max.x + overhang && b.min.z >= min.z - overhang &&
                               b.max.z <= max.z + overhang && Mathf.Abs(b.min.y) < 0.01f;
                    inside &= good;
                    if (!good) detail.Add($"{o.DefinitionId} {b.min:F2}..{b.max:F2} vs {min:F2}..{max:F2}");
                }
                Check($"placement ({world.MapId}): models sit on the ground inside their footprint (eaves <= 0.15)", inside,
                    string.Join("; ", detail));
                // Blocked cells come from Map JSON + Catalog only; Action Point cells stay walkable and clear.
                var blockedOk = world.Objects.All(o => o.BlockedCells().All(world.IsBlocked));
                var actionOk = true;
                foreach (var o in world.Objects.Where(o => PocSceneBuilder.Phase3Definitions.Contains(o.DefinitionId)))
                foreach (var point in o.Definition.ActionPoints)
                {
                    var cell = o.RootCell + point.PlayerCellOffset;
                    var walkable = world.CanEnter(cell);
                    var clear = point.PlayerCellOffset == Vector2Int.zero ||
                                !BoundsIntrude(BoundsOf(c.Presenter.FindView(o.InstanceId)), cell, 0.12f);
                    actionOk &= walkable && clear;
                    if (!(walkable && clear)) detail.Add($"{o.DefinitionId} {point.Id} {cell}");
                }
                Check($"placement ({world.MapId}): blocked footprints and Action Point cells unchanged (action cells walkable, not covered)",
                    blockedOk && actionOk, string.Join("; ", detail));
            }
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
        }

        private static IEnumerable<Vector2Int> FootprintCells(SharedObjectInstance o)
        {
            var blocked = o.BlockedCells().ToList();
            if (o.DefinitionId == "house_main") blocked.Add(o.RootCell); // the alcove is part of the drawing
            return blocked.Count > 0 ? blocked : new List<Vector2Int> { o.RootCell };
        }

        private static bool BoundsIntrude(Bounds b, Vector2Int cell, float margin)
        {
            var centre = GridSpace3D.ToWorld(cell);
            var half = 0.25f - margin;
            return b.max.x > centre.x - half && b.min.x < centre.x + half && b.max.z > centre.z - half && b.min.z < centre.z + half;
        }

        // ---------------------------------------------------------------- catalog

        private static void CatalogChecks(Ctx c)
        {
            var definitions = new HashSet<string>();
            foreach (var path in new[] { PocSceneBuilder.OutdoorMapPath, PocSceneBuilder.InteriorMapPath })
                foreach (var placement in AssetDatabase.LoadAssetAtPath<MapDefinition>(path).Objects)
                    definitions.Add(placement.Definition.StableId);
            var missing = PocBindingAudit.Missing(c.Catalog, definitions).ToList();
            Check("catalog: every Object Definition used in the official maps has a real (non-placeholder) Blender binding",
                missing.Count == 0, missing.Count > 0 ? string.Join(",", missing) : string.Join(",", definitions.OrderBy(d => d)));
            var fallback = false;
            foreach (var interior in new[] { false, true })
            {
                if (interior) Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
                fallback |= c.Presenter.Views.Any(v => v != null && (v.IsFallback || (!v.IsWall && v.Placeholder)));
            }
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Check("catalog: no generated box or placeholder is drawn in either official map", !fallback);
            Check("catalog: a definition without a binding is reported (audit for future objects)",
                PocBindingAudit.Missing(c.Catalog, new[] { "tree_basic", "phase3_audit_probe" }).SequenceEqual(new[] { "phase3_audit_probe" }));
            var report = PocBindingAudit.Report(c.Catalog, definitions);
            var logs = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE3_LOGS"));
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "phase3_binding_audit.md"), report);
            Check("catalog: the binding audit list was written", report.Contains("house_main"), logs);
        }

        // ---------------------------------------------------------------- outdoor 3D

        private static void OutdoorChecks(Ctx c)
        {
            var house = ViewOf(c, "house_main");
            var door = c.Session.World.Objects.Single(o => o.DefinitionId == "house_main").RootCell;
            Place(c, door + new Vector2Int(0, -2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            Check("house 3D: shown in full while the player approaches the door",
                house.Renderers.All(r => r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
            Place(c, door, FacingDirection.Up, PocViewMode.ThirdPerson3D);
            Check("house 3D: standing in the entrance alcove hides nothing (the doorway is open)",
                c.Third.HiddenParts == 0 && house.Renderers.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
            Place(c, door + new Vector2Int(0, 2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var hiddenRoof = house.Renderers.Where(PocThirdPersonCamera.IsRoof)
                .All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
            var hiddenBody = house.Renderers.Where(r => !PocThirdPersonCamera.IsRoof(r))
                .All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
            Check("house 3D: behind the house the blocking parts hide (walls with the door, then the roof: nothing floats)",
                hiddenBody && hiddenRoof && c.Third.HiddenParts == house.Renderers.Length);
            Place(c, door + new Vector2Int(0, -2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            Check("house 3D: walking back in front brings every part back",
                house.Renderers.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On) && c.Third.HiddenParts == 0);
            var anchor = house.GetComponentsInChildren<Transform>().First(t => t.name == "DoorAnchor").position;
            var doorCell = GridSpace3D.ToWorld(door);
            Check("house: the door stands on the door cell's North edge, behind the walkable alcove",
                Mathf.Abs(anchor.x - doorCell.x) < 0.01f && anchor.z > doorCell.z + 0.18f && anchor.z <= doorCell.z + 0.25f &&
                !c.Session.World.IsBlocked(door), anchor.ToString("F2"));
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
        }

        // ---------------------------------------------------------------- entrance and exit

        private static void EntranceChecks(Ctx c)
        {
            var s = c.Session;
            s.World.TryGetHouseDoor(out var door);
            Place(c, door + new Vector2Int(0, -2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var steps = s.CompletedSteps;
            HoldUntil(c, Vector2Int.up, () => s.World.MapId == "halka_house");
            Check("enter 3D: walking up into the alcove loads halka_house at interior_entry facing Up",
                s.World.MapId == "halka_house" && s.World.TryGetMarker("interior_entry", out var entry) && s.Cell == entry &&
                s.Facing == FacingDirection.Up, s.World.MapId + " " + s.Cell);
            Check("enter: the interior is rebuilt from Blender pieces (floor, walls, doorway)",
                c.Presenter.BuiltMapId == "halka_house" && c.Presenter.Interior != null && c.Presenter.Interior.HasDoorway);
            s.World.TryGetMarker("interior_exit", out var exit);
            Check("enter: the doorway is drawn on the interior_exit cell, the room side keeps walls",
                c.Presenter.Interior.DoorwayCell == exit && c.Presenter.Interior.DoorwaySide == "south");
            HoldUntil(c, Vector2Int.down, () => s.World.MapId == "first_field"); // camera-relative: back = South
            Check("exit 3D: walking into the doorway returns outside below the door facing Down",
                s.World.MapId == "first_field" && s.Cell == door + Vector2Int.down && s.Facing == FacingDirection.Down,
                s.World.MapId + " " + s.Cell);
            Check("enter/exit: map changes add no extra steps", s.CompletedSteps - steps == 3, (s.CompletedSteps - steps).ToString());
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
        }

        // ---------------------------------------------------------------- interior

        private static void InteriorChecks(Ctx c)
        {
            Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
            var world = c.Session.World;
            var stats = c.Presenter.Interior;
            var walkable = new HashSet<Vector2Int>();
            var walls = new HashSet<Vector2Int>();
            for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
            for (var x = world.MinCell.x; x <= world.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var surface = world.Map.SurfaceAt(cell);
                if (surface != null && surface.BlocksMovement) walls.Add(cell);
                else walkable.Add(cell);
            }
            Check("interior: a floor tile on every non-wall cell (also under furniture), none on walls",
                stats.FloorCells.SetEquals(walkable), $"{stats.FloorCells.Count} floor / {walkable.Count} cells");
            Check("interior: every wall Surface cell has a wall piece; nothing else does",
                stats.WallCellSet.SetEquals(walls), $"{stats.WallCellSet.Count} / {walls.Count}");
            var bounds = c.Presenter.Views.Where(v => v.IsWall).Select(BoundsOf).ToList();
            var minW = GridSpace3D.ToWorld(world.MinCell) - new Vector3(0.26f, 0f, 0.26f);
            var maxW = GridSpace3D.ToWorld(world.MaxCell) + new Vector3(0.26f, 0f, 0.26f);
            Check("interior: walls stay inside the Map Bounds",
                bounds.All(b => b.min.x >= minW.x && b.max.x <= maxW.x && b.min.z >= minW.z && b.max.z <= maxW.z));
            var furniture = world.Objects.Select(o => BoundsOf(c.Presenter.FindView(o.InstanceId))).ToList();
            Check("interior: furniture stands on the floor (no floating, nothing under the floor)",
                furniture.All(b => Mathf.Abs(b.min.y) < 0.01f));
            var blocked = world.Objects.SelectMany(o => o.BlockedCells()).ToList();
            Check("interior: collision unchanged (walls + furniture footprints, the cushion stays walkable)",
                blocked.All(world.IsBlocked) && walls.All(world.IsBlocked) &&
                world.CanEnter(world.Objects.Single(o => o.DefinitionId == "cushion_basic").RootCell));
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- interior camera

        private static void InteriorCameraChecks(Ctx c)
        {
            Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
            var results = new List<string>();
            var allClear = true;
            var cutRight = true;
            foreach (var (cell, facing, side, opposite) in new[]
                     {
                         (new Vector2Int(0, -2), FacingDirection.Up, "south", "north"),
                         (new Vector2Int(0, 1), FacingDirection.Down, "north", "south"),
                         (new Vector2Int(-3, 0), FacingDirection.Left, "east", "west"),
                         (new Vector2Int(3, -1), FacingDirection.Right, "west", "east")
                     })
            {
                Place(c, cell, facing, PocViewMode.ThirdPerson3D);
                var camera = c.Third.ViewCamera.transform.position;
                var clear = PlayerVisible(c, camera);
                var wallSide = c.Presenter.Views.Where(v => v.IsWall).ToDictionary(v => v.InstanceId);
                var cutOk = wallSide["wall_" + side].CutAway && !wallSide["wall_" + opposite].CutAway;
                allClear &= clear;
                cutRight &= cutOk;
                results.Add($"{facing}: clear {clear}, cut {string.Join("/", wallSide.Values.Where(v => v.CutAway).Select(v => v.InstanceId))}");
            }
            Check("interior camera: in all four directions nothing visible stands between the camera and the player", allClear,
                string.Join("; ", results));
            Check("interior camera: the wall side facing the camera is cut down, the far side stays", cutRight,
                string.Join("; ", results));
            Place(c, new Vector2Int(0, -3), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var cameraPos = c.Third.ViewCamera.transform.position;
            var furnitureSeen = c.Session.World.Objects.All(o => !RayBlockedByWalls(c, cameraPos, BoundsOf(c.Presenter.FindView(o.InstanceId)).center));
            var furnitureShown = c.Session.World.Objects.All(o => c.Presenter.FindView(o.InstanceId).Renderers
                .All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
            Check("interior camera: from the entry every piece of furniture is in sight (no wall in front, none hidden)",
                furnitureSeen && furnitureShown);
            // The camera never jumps: one step moves it smoothly (no more than a step's length per frame).
            var before = c.Third.ViewCamera.transform.position;
            var maxJump = 0f;
            for (var i = 0; i < 30; i++)
            {
                Frame(c, 1f / 60f, Vector2Int.right);
                var now = c.Third.ViewCamera.transform.position;
                maxJump = Mathf.Max(maxJump, Vector3.Distance(before, now));
                before = now;
            }
            Settle(c, 0.6f);
            Check("interior camera: following a walk the camera moves smoothly (< 0.1 per frame)", maxJump < 0.1f, maxJump.ToString("F3"));
            // Phase 2.1 controls still apply indoors: holding S walks back without turning the camera.
            Place(c, new Vector2Int(0, 1), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var heading = c.Third.Heading;
            var cells = new List<Vector2Int>();
            HoldUntil(c, Vector2Int.down, () =>
            {
                if (cells.Count == 0 || cells.Last() != c.Session.Cell) cells.Add(c.Session.Cell);
                return cells.Count > 3;
            });
            cells.RemoveAt(0);
            Check("interior controls: holding S walks straight back (South), the camera keeps its heading",
                cells.Count >= 3 && cells.All(p => p.x == 0) && cells.Last().y < 1 && c.Third.Heading == heading,
                string.Join(" ", cells));
            Place(c, new Vector2Int(3, -3), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var sideways = Hold(c, Vector2Int.left, 0.75f);
            Check("interior controls: holding A walks straight left (no circling)",
                sideways.Count >= 2 && sideways.All(p => p.y == -3), string.Join(" ", sideways));
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
        }

        // Chest ray from the camera: no visible (not cut, not ShadowsOnly) renderer in the way.
        private static bool PlayerVisible(Ctx c, Vector3 camera)
        {
            var target = GridSpace3D.ToWorld(c.Session.Position) + Vector3.up * PocThirdPersonCamera.ChestHeight;
            var dir = target - camera;
            var ray = new Ray(camera, dir.normalized);
            foreach (var view in c.Presenter.Views)
            foreach (var r in view.Renderers)
            {
                if (r == null || r.forceRenderingOff || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (view.IsWall ? r.bounds.IntersectRay(ray, out var d) && d < dir.magnitude - 0.2f
                        : view.RaycastPart(r, ray, out d) && d < dir.magnitude - 0.2f && view.CameraOccluder) return false;
            }
            return true;
        }

        private static bool RayBlockedByWalls(Ctx c, Vector3 from, Vector3 to)
        {
            var dir = to - from;
            var ray = new Ray(from, dir.normalized);
            return c.Presenter.Views.Where(v => v.IsWall && !v.CutAway).Any(v =>
                v.TryRaycast(ray, out var d) && d < dir.magnitude - 0.3f && !InsideRoomBand(v, to));
        }

        // A far wall behind a piece of furniture is not "in front" of it.
        private static bool InsideRoomBand(PocObjectView wall, Vector3 point) => wall.Renderers[0].bounds.Contains(point);

        // ---------------------------------------------------------------- 2D

        private static void TwoDChecks(Ctx c)
        {
            c.Pixel.PixelsPerCell = 16;
            c.Pixel.OutlinePixels = 1;
            // Outline: the house and each fixture/furniture get an outer line, not a mesh of inner lines.
            var outlined = new List<string>();
            var ok = true;
            foreach (var interior in new[] { false, true })
            {
                if (interior) Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
                Place(c, interior ? new Vector2Int(0, -3) : new Vector2Int(-3, -1), FacingDirection.Up, PocViewMode.PixelView2D);
                c.Pixel.WorldCamera.Render();
                var raw = ReadPixels(c.Pixel.Target);
                var lines = ReadPixels(c.Pixel.ComposeOutline());
                var w = c.Pixel.Target.width;
                foreach (var o in c.Session.World.Objects.Where(o => PocSceneBuilder.Phase3Definitions.Contains(o.DefinitionId)))
                {
                    var rect = LowResRect(c, BoundsOf(c.Presenter.FindView(o.InstanceId)));
                    if (rect.width <= 0) continue;
                    var ink = 0;
                    for (var y = rect.yMin; y < rect.yMax; y++)
                    for (var x = rect.xMin; x < rect.xMax; x++)
                        if (x >= 0 && y >= 0 && x < w && y < c.Pixel.Target.height && !raw[y * w + x].Equals(lines[y * w + x])) ink++;
                    var perimeter = 2 * (rect.width + rect.height);
                    var good = ink >= perimeter / 3 && ink <= perimeter * 3;
                    ok &= good;
                    outlined.Add($"{o.DefinitionId} {ink}/{perimeter}");
                }
            }
            Check("2D outline: the house, fixtures and furniture get a clear outer line, not lines all over (ink ~ perimeter)", ok,
                string.Join(", ", outlined.Distinct()));
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);

            // Draw order of the original sprite: hidden behind the house and the easel, in front of others.
            var door = c.Session.World.Objects.Single(o => o.DefinitionId == "house_main").RootCell;
            var behindHouse = SpriteCovered(c, door + new Vector2Int(-1, 2));
            var frontOfHouse = SpriteCovered(c, door + new Vector2Int(-1, -1));
            Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
            var behindEasel = SpriteCovered(c, new Vector2Int(-3, 0));
            var frontOfEasel = SpriteCovered(c, new Vector2Int(-3, -3));
            var frontOfDesk = SpriteCovered(c, new Vector2Int(2, 0));
            var onCushion = SpriteCovered(c, new Vector2Int(0, 0));
            Check("2D order: the original sprite is covered behind the house and the easel, and drawn in front of them otherwise",
                behindHouse > 200 && frontOfHouse < 20 && behindEasel > 60 && frontOfEasel < 20 && frontOfDesk < 20 && onCushion < 20,
                $"house behind {behindHouse} / front {frontOfHouse}, easel behind {behindEasel} / front {frontOfEasel}, desk {frontOfDesk}, cushion {onCushion}");
            Check("2D: the original sprite is drawn in the room (not the 3D model)",
                c.Pixel.LastSpriteRect.width > 0 && !c.Actor.ModelVisible && c.Actor.HasSprites);
            Check("2D: the South wall side is cut down to its base, the others stand (flat dark rows like the 2D room)",
                c.Presenter.Views.Where(v => v.IsWall).All(v => v.CutAway == (v.InstanceId == "wall_south")) && c.Pixel.CutWalls == 1);
            Check("2D: images stay separate low-resolution textures; the UI (IMGUI) is drawn after them",
                c.Pixel.ComposeOutline() != c.Pixel.Target && c.Pixel.OutlineMask.width == c.Pixel.Target.width);
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);

            // The see-through tree still works next to the new buildings.
            var tree = c.Session.World.Objects.First(o => o.DefinitionId == "tree_basic" && c.Session.World.CanEnter(o.RootCell + Vector2Int.up));
            var view = c.Presenter.FindView(tree.InstanceId);
            Place(c, tree.RootCell + Vector2Int.up, FacingDirection.Down, PocViewMode.PixelView2D);
            Check("2D: a tree in front of the player still fades to 0.4 (Phase 2.1)", Mathf.Abs(c.Pixel.FadeOf(view) - 0.4f) < 1e-3f,
                c.Pixel.FadeOf(view).ToString("F2"));
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            Check("2D: the house never fades (only trees are see-through)", !ViewOf(c, "house_main").FadeInFront2D);
        }

        private static RectInt LowResRect(Ctx c, Bounds b)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var v = c.Pixel.WorldCamera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, v.x * c.Pixel.Target.width);
                maxX = Mathf.Max(maxX, v.x * c.Pixel.Target.width);
                minY = Mathf.Min(minY, v.y * c.Pixel.Target.height);
                maxY = Mathf.Max(maxY, v.y * c.Pixel.Target.height);
            }
            return new RectInt(Mathf.FloorToInt(minX) - 2, Mathf.FloorToInt(minY) - 2, Mathf.CeilToInt(maxX - minX) + 4,
                Mathf.CeilToInt(maxY - minY) + 4);
        }

        // Screen pixels of the sprite that a nearer object covers (through the mask's depth).
        private static int SpriteCovered(Ctx c, Vector2Int cell)
        {
            Place(c, cell, FacingDirection.Down, PocViewMode.PixelView2D);
            c.Pixel.FadeEnabled = false;
            Frame(c, 0f, Vector2Int.zero);
            c.Pixel.WorldCamera.Render();
            c.Pixel.ComposeScreen(ScreenW, ScreenH);
            var mask = ReadPixels(c.Pixel.OutlineMask);
            var rect = c.Pixel.LastSpriteRect;
            int w = c.Pixel.OutlineMask.width, h = c.Pixel.OutlineMask.height, scale = c.Pixel.Scale, covered = 0;
            for (var y = 0; y < rect.height; y++)
            for (var x = 0; x < rect.width; x++)
            {
                int lx = (rect.x + x + c.Pixel.Offset.x) / scale, ly = (rect.y + y + c.Pixel.Offset.y) / scale;
                if (lx < 0 || ly < 0 || lx >= w || ly >= h) continue;
                var m = mask[ly * w + lx];
                if (m.r == 0 || m.r == PocOutlineTag.DecorationId) continue;
                var depth = (m.b + m.a / 255f) / 255f;
                var card = Mathf.Lerp(c.Pixel.LastSpriteDepthBottom, c.Pixel.LastSpriteDepthTop, (y + 0.5f) / rect.height);
                if (depth < card - 0.0005f) covered++;
            }
            c.Pixel.FadeEnabled = true;
            return covered;
        }

        // ---------------------------------------------------------------- interaction

        private static void InteractionChecks(Ctx c)
        {
            foreach (var interior in new[] { false, true })
            {
                if (interior) Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
                var world = c.Session.World;
                foreach (var o in world.Objects.Where(o => PocSceneBuilder.Phase3Definitions.Contains(o.DefinitionId)))
                {
                    var points = o.Definition.ActionPoints;
                    if (points.Count == 0)
                    {
                        // No Action Point: whatever the shared rule says is the same in 2D and 3D.
                        var answers = new List<string>();
                        foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                        {
                            Place(c, o.RootCell + Vector2Int.down, FacingDirection.Up, mode);
                            answers.Add(c.Session.InteractAhead() ? c.Session.LastMessage : "(none)");
                        }
                        Check($"interaction ({o.DefinitionId}): no Action Point; 2D and 3D answer the same ({answers[0]})",
                            answers[0] == answers[1], string.Join(" / ", answers));
                        continue;
                    }
                    foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                    {
                        var point = points[0];
                        var cell = o.RootCell + point.PlayerCellOffset;
                        var facing = point.PlayerFacing == WorldFacing.Up ? FacingDirection.Up :
                            point.PlayerFacing == WorldFacing.Down ? FacingDirection.Down :
                            point.PlayerFacing == WorldFacing.Left ? FacingDirection.Left : FacingDirection.Right;
                        Place(c, cell, facing, mode);
                        var messageBefore = c.Session.LastMessageAt;
                        var a = c.Session.InteractAhead();
                        if (SeatPoses.TryGetValue(o.DefinitionId, out var pose))
                        {
                            Check($"interaction ({o.DefinitionId}, {mode}): the A button seats the player ({pose}) at its Action Point",
                                a && c.Session.IsSeated && c.Session.SeatObject == o && c.Session.SeatPose == pose &&
                                c.Session.Cell == cell && Mathf.Approximately(c.Session.LastMessageAt, messageBefore),
                                $"seated {c.Session.IsSeated} {c.Session.SeatPose}");
                            c.Session.Stand();
                            continue;
                        }
                        var expected = o.DefinitionId == "sign_basic" ? o.SignText : Messages[o.DefinitionId];
                        Check($"interaction ({o.DefinitionId}, {mode}): the A button gives \"{expected}\" at its Action Point",
                            a && c.Session.LastMessage == expected, c.Session.LastMessage ?? "-");
                    }
                }
            }
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
        }

        private static IEnumerable<(Vector2Int cell, FacingDirection facing)> Around(SharedObjectInstance o)
        {
            foreach (var cell in FootprintCells(o))
                foreach (var (d, f) in new[] { (Vector2Int.down, FacingDirection.Up), (Vector2Int.up, FacingDirection.Down),
                             (Vector2Int.left, FacingDirection.Right), (Vector2Int.right, FacingDirection.Left) })
                    yield return (cell + d, f);
        }

        // ---------------------------------------------------------------- clicks

        private static void ClickChecks(Ctx c)
        {
            var world = c.Session.World;
            // A flower with open ground behind it, so a click through the player hits nothing else.
            var flower = world.Objects.First(o => o.DefinitionId == "flower_basic" && world.CanEnter(o.RootCell + Vector2Int.down) &&
                !world.Objects.Any(x => x != o && x.BlockedCells().DefaultIfEmpty(x.RootCell).Any(p =>
                    Mathf.Abs(p.x - o.RootCell.x) <= 1 && p.y > o.RootCell.y && p.y <= o.RootCell.y + 4)));
            Place(c, flower.RootCell + Vector2Int.down, FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var body = c.Third.ViewCamera.WorldToScreenPoint(GridSpace3D.ToWorld(c.Session.Position, 0.5f));
            var picked = c.Switcher.PickObject(body);
            Check("click 3D: a click on her body over the hidden flower examines the flower",
                picked != null && picked.InstanceId == flower.InstanceId &&
                c.Session.InteractWith(picked) && c.Session.LastMessage == "はな。",
                $"picked {(picked != null ? picked.DefinitionId + " " + picked.RootCell : "none")}, via player {c.Switcher.LastPickUsedPlayer}, flower {flower.RootCell}, msg {c.Session.LastMessage}");
            var head = c.Third.ViewCamera.WorldToScreenPoint(GridSpace3D.ToWorld(c.Session.Position, 0.85f));
            picked = c.Switcher.PickObject(head);
            Check("click 3D: a click on ﾊﾙｶﾁｬﾝ that hits no object examines what she faces (the A button's target)",
                picked != null && picked.InstanceId == flower.InstanceId && c.Switcher.LastPickUsedPlayer &&
                c.Session.InteractWith(picked) && c.Session.LastMessage == "はな。",
                $"picked {(picked != null ? picked.DefinitionId + " " + picked.RootCell : "none")}, via player {c.Switcher.LastPickUsedPlayer}, flower {flower.RootCell}");
            Place(c, flower.RootCell + Vector2Int.down + Vector2Int.left, FacingDirection.Up, PocViewMode.ThirdPerson3D);
            picked = c.Switcher.PickObject(c.Third.ViewCamera.WorldToScreenPoint(GridSpace3D.ToWorld(c.Session.Position, 0.85f)));
            Check("click 3D: with nothing ahead, clicking ﾊﾙｶﾁｬﾝ does nothing (no other object is chosen)",
                picked == null || !c.Session.InteractWith(picked));
            Place(c, flower.RootCell + Vector2Int.down * 3, FacingDirection.Up, PocViewMode.ThirdPerson3D);
            picked = c.Switcher.PickObject(c.Third.ViewCamera.WorldToScreenPoint(GridSpace3D.ToWorld(flower.RootCell, 0.1f)));
            Check("click 3D: a far flower is never examined (distance rule unchanged)",
                picked == null || !c.Session.InteractWith(picked));
            // Indoors a wall in sight stops the click: nothing behind it is chosen.
            Enter(c, true, new Vector2Int(0, -3), FacingDirection.Up);
            Place(c, new Vector2Int(0, -1), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var desk = c.Session.World.Objects.Single(o => o.DefinitionId == "desk_basic");
            var deskPoint = c.Third.ViewCamera.WorldToScreenPoint(BoundsOf(c.Presenter.FindView(desk.InstanceId)).center);
            var deskPick = c.Switcher.PickObject(deskPoint);
            Check("click 3D indoors: furniture in sight is picked", deskPick != null && deskPick.InstanceId == desk.InstanceId);
            var north = c.Presenter.Views.Single(v => v.InstanceId == "wall_north");
            var wallPoint = c.Third.ViewCamera.WorldToScreenPoint(BoundsOf(north).center + Vector3.up * 0.3f);
            var wallPick = c.Switcher.PickObject(wallPoint);
            Check("click 3D indoors: a click on a standing wall picks nothing behind it", wallPick == null);
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- 2D / 3D switching, in and out

        private static void SwitchSequenceChecks(Ctx c)
        {
            var s = c.Session;
            s.World.TryGetHouseDoor(out var door);
            var log = new List<string>();
            var ok = true;
            void Expect(string label, bool condition)
            {
                ok &= condition;
                log.Add(label + (condition ? " ok" : " NG") + $" [{s.World.MapId} {s.Cell} steps {s.CompletedSteps}]");
            }
            Place(c, door + new Vector2Int(0, -2), FacingDirection.Up, PocViewMode.PixelView2D);
            Expect("outdoor 2D", c.Pixel.CutWalls == 0 && HouseShown(c));
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            Settle(c, 0.4f);
            Expect("outdoor 3D", HouseShown(c) && PlayerVisible(c, c.Third.ViewCamera.transform.position));
            var state = (s.CompletedSteps, s.Facing);
            HoldUntil(c, Vector2Int.up, () => s.World.MapId == "halka_house");
            Expect("enter house", s.World.MapId == "halka_house");
            Expect("indoor 3D", PlayerVisible(c, c.Third.ViewCamera.transform.position) &&
                c.Presenter.Views.Where(v => v.IsWall).Count(v => v.CutAway) >= 1);
            var cell = s.Cell;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.4f);
            Expect("indoor 2D", s.Cell == cell && c.Presenter.Views.Where(v => v.IsWall).All(v => v.CutAway == (v.InstanceId == "wall_south")));
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            Settle(c, 0.4f);
            Expect("indoor 3D again", s.Cell == cell && PlayerVisible(c, c.Third.ViewCamera.transform.position));
            HoldUntil(c, Vector2Int.down, () => s.World.MapId == "first_field");
            Expect("leave house", s.World.MapId == "first_field" && s.Cell == door + Vector2Int.down);
            Settle(c, 0.4f);
            // Right after the exit the camera is behind her, i.e. North, beyond the house: the house
            // is an occluder then (parts hidden, shadow kept) and she stays in sight. One step
            // sideways turns the camera and the whole house is back: nothing stays hidden.
            Expect("outdoor 3D after exit", PlayerVisible(c, c.Third.ViewCamera.transform.position) &&
                c.Presenter.Views.All(v => !v.CutAway));
            HoldUntil(c, Vector2Int.left, () => s.Cell != door + Vector2Int.down);
            Settle(c, 1.2f);
            Expect("outdoor 3D after a step aside", HouseShown(c) && PlayerVisible(c, c.Third.ViewCamera.transform.position));
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.4f);
            Expect("outdoor 2D after", HouseShown(c) && c.Pixel.CutWalls == 0 && c.Pixel.LastSpriteRect.width >= 0);
            Check("switching outdoor 2D > 3D > enter > indoor 3D > 2D > 3D > exit > 3D > 2D: no part stays hidden, the player stays in sight",
                ok, string.Join(", ", log));
            var aside = Mathf.Abs(s.Cell.x - door.x); // the held key may take one more step aside
            Check("switching: only real steps are counted (2 to the door, 1 out, the steps aside); switches and doors add none",
                s.CompletedSteps == state.CompletedSteps + 3 + aside, $"{state.CompletedSteps} -> {s.CompletedSteps}, aside {aside}");
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
        }

        private static bool HouseShown(Ctx c)
        {
            var house = ViewOf(c, "house_main");
            return house != null && house.Renderers.All(r => r.enabled && !r.forceRenderingOff &&
                r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
        }

        // ---------------------------------------------------------------- night

        private static void NightChecks(Ctx c)
        {
            c.Time.Override = PocPhaseOverride.Night;
            c.Time.Apply(clock);
            c.Time.Apply(clock + WorldNightOverlay2D.FadeSeconds + 0.1f);
            var outdoorAlpha = c.Time.PixelOverlayAlpha;
            Enter(c, true, new Vector2Int(0, -1), FacingDirection.Up);
            c.Time.Apply(clock + 10f);
            var indoorAlpha = c.Time.PixelOverlayAlpha;
            Check("night 2D: the room is darkened less than outdoors (same overlay values as the 2D game)",
                Mathf.Abs(outdoorAlpha - WorldNightOverlay2D.OutdoorNightAlpha) < 1e-3f &&
                Mathf.Abs(indoorAlpha - WorldNightOverlay2D.IndoorNightAlpha) < 1e-3f, $"{outdoorAlpha:F2} / {indoorAlpha:F2}");
            Place(c, new Vector2Int(0, -1), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            c.Time.Apply(clock + 20f);
            var indoorAmbient = RenderSettings.ambientLight;
            var background = c.Third.ViewCamera.backgroundColor;
            Enter(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            c.Time.Apply(clock + 30f);
            var outdoorAmbient = RenderSettings.ambientLight;
            Check("night 3D: the room keeps more light than outdoors and shows the map backdrop, not the sky",
                indoorAmbient.maxColorComponent > outdoorAmbient.maxColorComponent &&
                (Vector4)background == (Vector4)AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.InteriorMapPath).BackdropColor,
                $"{indoorAmbient} vs {outdoorAmbient}, bg {background}");
            c.Time.Override = PocPhaseOverride.Auto;
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
        }

        // ---------------------------------------------------------------- MAP EDITOR fixtures

        private static void FixtureChecks(Ctx c)
        {
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "POC2D3DFixtures"));
            var outdoorPath = Path.Combine(folder, "first_field_buildings_moved.hwmap.json");
            var indoorPath = Path.Combine(folder, "halka_house_furniture_moved.hwmap.json");
            Check("fixtures: MAP EDITOR v0.6 core moved the house, bench, sign, well and all furniture on copies",
                File.Exists(outdoorPath) && File.Exists(indoorPath));
            if (!File.Exists(outdoorPath) || !File.Exists(indoorPath)) return;
            var officialOutdoor = new SharedWorldModel(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath));
            var officialIndoor = new SharedWorldModel(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.InteriorMapPath));
            var outdoor = PocMapJson.ToDefinition(PocMapJson.Load(outdoorPath));
            var indoor = PocMapJson.ToDefinition(PocMapJson.Load(indoorPath));
            try
            {
                c.Session.UseOutdoorMapForTest(outdoor);
                c.Session.UseInteriorMapForTest(indoor);
                c.Presenter.Rebuild(c.Session.World);
                MovedChecks(c, officialOutdoor, "outdoor");
                // The door moved with the house: the entrance follows it.
                c.Session.World.TryGetHouseDoor(out var door);
                Place(c, door + new Vector2Int(0, -1), FacingDirection.Up, PocViewMode.ThirdPerson3D);
                HoldUntil(c, Vector2Int.up, () => c.Session.World.MapId == "halka_house");
                Check("fixture outdoor: the moved house's door still leads inside", c.Session.World.MapId == "halka_house",
                    door.ToString());
                MovedChecks(c, officialIndoor, "indoor");
                var desk = c.Session.World.Objects.Single(o => o.DefinitionId == "desk_basic");
                Place(c, desk.RootCell + Vector2Int.down, FacingDirection.Up, PocViewMode.PixelView2D);
                Check("fixture indoor: the moved desk answers \"つくえ。\" at its new Action Point",
                    c.Session.InteractAhead() && c.Session.LastMessage == "つくえ。");
                Check("fixture indoor: the floor still covers every non-wall cell after the move",
                    c.Presenter.Interior.FloorCells.Count == c.Presenter.Interior.FloorTiles + (c.Presenter.Interior.HasDoorway ? 1 : 0));
            }
            finally
            {
                c.Session.UseInteriorMapForTest(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.InteriorMapPath));
                c.Session.UseOutdoorMapForTest(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath));
                c.Presenter.Rebuild(c.Session.World);
                UnityEngine.Object.DestroyImmediate(outdoor);
                UnityEngine.Object.DestroyImmediate(indoor);
            }
        }

        private static void MovedChecks(Ctx c, SharedWorldModel official, string label)
        {
            var world = c.Session.World;
            var moved = new List<string>();
            var ok = true;
            foreach (var o in world.Objects.Where(o => PocSceneBuilder.Phase3Definitions.Contains(o.DefinitionId)))
            {
                var before = official.FindInstance(o.InstanceId);
                if (before == null || before.RootCell == o.RootCell) continue;
                var view = c.Presenter.FindView(o.InstanceId);
                var good = view != null && (view.transform.position - GridSpace3D.ToWorld(o.RootCell)).sqrMagnitude < 1e-8f &&
                           o.BlockedCells().All(world.IsBlocked) &&
                           before.BlockedCells().Where(p => !o.BlockedCells().Contains(p) &&
                               !world.Objects.Any(x => x != o && x.BlockedCells().Contains(p)) &&
                               (world.Map.SurfaceAt(p) == null || !world.Map.SurfaceAt(p).BlocksMovement)).All(world.CanEnter);
                ok &= good;
                moved.Add($"{o.DefinitionId} {before.RootCell}->{o.RootCell}{(good ? "" : " NG")}");
            }
            Check($"fixture {label}: moved objects are drawn at their new cells in 2D/3D, collision followed", ok && moved.Count > 0,
                string.Join(", ", moved));
            if (label == "outdoor") return;
        }

        // ---------------------------------------------------------------- protection

        private static void ProtectionChecks()
        {
            var root = PocSceneBuilder.RepositoryRoot;
            var expected = File.ReadAllLines(Path.Combine(root, "tools/poc2d3d/official_map_sha256.txt"))
                .Where(l => l.Length > 66).ToList();
            var same = expected.All(line =>
            {
                var path = Path.Combine(root, line.Substring(65).TrimStart('*', ' '));
                using var sha = SHA256.Create();
                return File.Exists(path) && BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "")
                    .Equals(line.Substring(0, 64), StringComparison.OrdinalIgnoreCase);
            });
            Check("protection: official Map JSON and Catalog unchanged (SHA-256 of all four files)", same && expected.Count == 4);
        }
    }
}
