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
    // Batch regression for the Phase 1 POC. Runs in Edit Mode by calling the same public
    // methods the POC components run every frame. It never saves scenes or official data.
    public static class PocChecks
    {
        private static int passes;
        private static readonly List<string> failures = new List<string>();

        [MenuItem("HALKA/POC 2D3D/Run POC Checks")]
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
            ImportChecks();
            MapDataChecks();
            var root = OpenTemporaryScene(out var context);
            try
            {
                PresentationChecks(context);
                MovementChecks(context);
                InteractionChecks(context);
                SwitchChecks(context);
                PickingChecks(context);
                HouseTransitionChecks(context);
                PixelViewChecks(context);
                AutoLinkChecks(context);
                TimeChecks(context);
                FixtureChecks(context);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            foreach (var failure in failures) Debug.LogError("POC FAIL " + failure);
            Debug.Log($"HALKA POC checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            if (condition) { passes++; Debug.Log("POC PASS " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add(name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- Blender import

        private static void ImportChecks()
        {
            var toon = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            var manifestPath = Path.Combine(PocSceneBuilder.RepositoryRoot, "blender", "exports", "poc_assets_manifest.json");
            var manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "";
            foreach (var (path, name) in new[] { (PocSceneBuilder.PlayerModelPath, "player"),
                         (PocSceneBuilder.TreeModelPath, "tree"), (PocSceneBuilder.HouseModelPath, "house") })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check($"import {name}: model exists", model != null, path);
                if (model == null) continue;
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                Check($"import {name}: one mesh", filters.Length == 1, filters.Length.ToString());
                var filter = filters[0];
                var t = filter.transform;
                Check($"import {name}: identity transform (axis baked)",
                    Quaternion.Angle(t.rotation, Quaternion.identity) < 0.01f && (t.lossyScale - Vector3.one).sqrMagnitude < 1e-8f &&
                    t.position.sqrMagnitude < 1e-8f, $"rot {t.rotation.eulerAngles} scale {t.lossyScale}");
                var mesh = filter.sharedMesh;
                var bounds = mesh.bounds;
                Check($"import {name}: pivot on ground", Mathf.Abs(bounds.min.y) < 0.002f, bounds.ToString("F3"));
                var triangles = mesh.triangles.Length / 3;
                Check($"import {name}: triangle count matches Blender manifest",
                    manifest.Contains($"\"triangles\": {triangles},"), triangles.ToString());
                Check($"import {name}: vertex colors", mesh.colors32.Length == mesh.vertexCount);
                Check($"import {name}: normals", mesh.normals.Length == mesh.vertexCount);
                Check($"import {name}: winding agrees with normals", WindingAgreement(mesh) > 0.99f,
                    WindingAgreement(mesh).ToString("P1"));
                var renderer = filter.GetComponent<MeshRenderer>();
                Check($"import {name}: shared POC material", renderer.sharedMaterials.Length == 1 &&
                    renderer.sharedMaterial == toon);
                if (name == "player")
                {
                    Check("import player: height 0.9-0.97 (two cells)", bounds.max.y > 0.9f && bounds.max.y < 0.97f,
                        bounds.max.y.ToString("F3"));
                    var eyes = Centroid(mesh, new Color32(0x2c, 0x56, 0xc8, 255));
                    Check("import player: eyes face South (-Z), not mirrored", eyes.HasValue && eyes.Value.z < -0.18f,
                        eyes?.ToString("F3") ?? "none");
                    var mark = Centroid(mesh, new Color32(0xf2, 0xc9, 0x4c, 255));
                    Check("import player: right-shoulder mark on West (-X) when facing South", mark.HasValue && mark.Value.x < -0.1f,
                        mark?.ToString("F3") ?? "none");
                    var pack = Centroid(mesh, new Color32(0xd9, 0x8a, 0x3a, 255));
                    Check("import player: back pack on North (+Z)", pack.HasValue && pack.Value.z > 0.07f,
                        pack?.ToString("F3") ?? "none");
                }
                if (name == "tree")
                {
                    Check("import tree: height about 1.6", bounds.max.y > 1.5f && bounds.max.y < 1.7f, bounds.max.y.ToString("F3"));
                    var trunk = Centroid(mesh, new Color32(0x7d, 0x54, 0x34, 255));
                    Check("import tree: trunk on root cell center", trunk.HasValue &&
                        new Vector2(trunk.Value.x, trunk.Value.z).magnitude < 0.05f, trunk?.ToString("F3") ?? "none");
                }
                if (name == "house")
                {
                    Check("import house: width 2.72 (5 cells + eaves)", Mathf.Abs(bounds.size.x - 2.72f) < 0.01f,
                        bounds.size.ToString("F3"));
                    var door = Centroid(mesh, new Color32(0x34, 0x34, 0x3a, 255));
                    Check("import house: door recessed in root cell, facing South", door.HasValue &&
                        door.Value.z > 0.2f && door.Value.z < 0.27f && Mathf.Abs(door.Value.x) < 0.01f, door?.ToString("F3") ?? "none");
                    var ornament = Centroid(mesh, new Color32(0xe3, 0xb3, 0x3a, 255));
                    Check("import house: gable front on South side", ornament.HasValue && ornament.Value.z < 0f,
                        ornament?.ToString("F3") ?? "none");
                }
            }
        }

        private static float WindingAgreement(Mesh mesh)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            var tris = mesh.triangles;
            var agree = 0;
            for (var i = 0; i < tris.Length; i += 3)
            {
                // Unity front faces are clockwise; the geometric normal is (b-a)x(c-a).
                var geometric = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                if (Vector3.Dot(geometric, n[tris[i]]) > 0f) agree++;
            }
            return agree / (tris.Length / 3f);
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

        // ---------------------------------------------------------------- Map data

        private static void MapDataChecks()
        {
            var catalog = PocMapJson.LoadCatalog(Path.Combine(Application.dataPath, "Content/Maps/Authoring/object_catalog.hwcatalog.json"));
            foreach (var (assetPath, jsonName) in new[] { (PocSceneBuilder.OutdoorMapPath, "first_field"),
                         (PocSceneBuilder.InteriorMapPath, "halka_house") })
            {
                var json = PocMapJson.Load(Path.Combine(Application.dataPath, $"Content/Maps/Authoring/{jsonName}.hwmap.json"));
                Check($"map {jsonName}: Map JSON v4", json.formatVersion == 4, json.formatVersion.ToString());
                var world = new SharedWorldModel(AssetDatabase.LoadAssetAtPath<MapDefinition>(assetPath));
                var same = json.objects.Count == world.Objects.Count && json.objects.All(o =>
                {
                    var instance = world.FindInstance(o.instanceId);
                    return instance != null && instance.DefinitionId == o.definitionId && instance.RootCell == o.rootCell.Cell;
                });
                Check($"map {jsonName}: every JSON Object Instance has the same ID, Definition and Root Cell", same,
                    $"{json.objects.Count} objects");
                var fromJson = PocMapJson.BlockedFromJson(json, catalog);
                Check($"map {jsonName}: blocked cells from JSON+Catalog == shared Grid", fromJson.SetEquals(world.BlockedCells),
                    $"{fromJson.Count} blocked cells");
                var rules = true;
                for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
                for (var x = world.MinCell.x; x <= world.MaxCell.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    rules &= world.IsBlocked(cell) == MapPlacementRules.BlocksMovement(world.Map, cell);
                }
                Check($"map {jsonName}: shared Grid == MapPlacementRules (2D runtime rule)", rules);
            }
        }

        // ---------------------------------------------------------------- Scene helpers

        private sealed class Context
        {
            public SharedWorldSession Session;
            public PocWorldPresenter Presenter;
            public PocViewSwitcher Switcher;
            public PocPixelView2D Pixel;
            public PocThirdPersonCamera Third;
            public PocActorView Actor;
            public PocTimeOfDay Time;
            public PocInput Input;
        }

        private static GameObject OpenTemporaryScene(out Context context)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var screen = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ScreenMaterialPath);
            var root = PocSceneBuilder.BuildSceneContent(catalog, screen);
            context = new Context
            {
                Session = root.GetComponent<SharedWorldSession>(),
                Presenter = root.GetComponent<PocWorldPresenter>(),
                Switcher = root.GetComponent<PocViewSwitcher>(),
                Time = root.GetComponent<PocTimeOfDay>(),
                Input = root.GetComponent<PocInput>(),
                Pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include),
                Third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include),
                Actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include)
            };
            context.Session.Initialize();
            context.Presenter.Rebuild(context.Session.World);
            context.Switcher.SetMode(PocViewMode.PixelView2D);
            Render(context, 1024, 576);
            // Everything the checks create lives in this scene; return the roots to destroy.
            var holder = new GameObject("POC check holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        private static void Render(Context c, int width, int height, float dt = 0.016f)
        {
            c.Actor.Apply(dt);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(dt, width, height);
            else c.Third.UpdateView(dt);
            c.Actor.Apply(dt);
        }

        private static void Walk(Context c, Vector2Int direction, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                c.Session.Tick(0f, direction);
                for (var t = 0; t < 12; t++) c.Session.Tick(0.02f, Vector2Int.zero);
            }
        }

        private static bool WalkPath(Context c, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            if (!GridPathfinder2D.TryFind(null, c.Session.Cell, goal, path, c.Session.World.CanEnter)) return false;
            var startMap = c.Session.World;
            foreach (var cell in path)
            {
                if (c.Session.World != startMap) break;
                Walk(c, cell - c.Session.Cell, 1);
            }
            return true;
        }

        // ---------------------------------------------------------------- Presentation

        private static void PresentationChecks(Context c)
        {
            var world = c.Session.World;
            Check("presentation: one view per Object Instance",
                world.Objects.All(o => c.Presenter.FindView(o.InstanceId) != null), world.Objects.Count.ToString());
            var positions = world.Objects.All(o =>
                (c.Presenter.FindView(o.InstanceId).transform.position - GridSpace3D.ToWorld(o.RootCell)).sqrMagnitude < 1e-8f);
            Check("presentation: every view root sits on GridSpace3D.ToWorld(Root Cell)", positions);
            var trees = world.Objects.Where(o => o.DefinitionId == "tree_basic").ToList();
            Check("presentation: trees use the Blender model", trees.Count > 0 &&
                trees.All(o => !c.Presenter.FindView(o.InstanceId).IsFallback), trees.Count + " trees");
            var houseInstance = world.Objects.Single(o => o.DefinitionId == "house_main");
            var house = c.Presenter.FindView(houseInstance.InstanceId);
            var b = house.Renderers[0].bounds;
            var root = GridSpace3D.ToWorld(houseInstance.RootCell);
            Check("presentation: Blender house stays inside its 5x2 footprint (+eaves)",
                !house.IsFallback && b.min.x >= root.x - 1.25f - 0.12f && b.max.x <= root.x + 1.25f + 0.12f &&
                b.min.z >= root.z - 0.25f - 0.12f && b.max.z <= root.z + 0.75f + 0.12f, b.ToString("F2"));
            var fallbackOk = true;
            foreach (var o in world.Objects.Where(o => c.Presenter.FindView(o.InstanceId).IsFallback))
            {
                var cells = o.BlockedCells().DefaultIfEmpty(o.RootCell).ToList();
                var fb = c.Presenter.FindView(o.InstanceId).Renderers[0].bounds;
                var minCell = GridSpace3D.ToWorld(new Vector2(cells.Min(p => p.x), cells.Min(p => p.y)));
                var maxCell = GridSpace3D.ToWorld(new Vector2(cells.Max(p => p.x), cells.Max(p => p.y)));
                fallbackOk &= fb.min.x > minCell.x - 0.25f && fb.max.x < maxCell.x + 0.25f &&
                              fb.min.z > minCell.z - 0.25f && fb.max.z < maxCell.z + 0.25f;
            }
            Check("presentation: generated boxes cover only their blocked cells", fallbackOk);
        }

        // ---------------------------------------------------------------- Movement

        private static void MovementChecks(Context c)
        {
            var s = c.Session;
            var start = s.Cell;
            Check("movement: player starts at the authored Player Start", start == new Vector2Int(0, 0) && s.Facing == FacingDirection.Down,
                start + " " + s.Facing);
            s.Tick(0f, Vector2Int.right);
            s.Tick(0.09f, Vector2Int.zero);
            Check("movement: half a step interpolates without changing the logical cell",
                s.IsMoving && s.Cell == start && Mathf.Abs(s.Position.x - (start.x + 0.5f)) < 0.01f, s.Position.ToString("F2"));
            s.Tick(0.09f, Vector2Int.zero);
            Check("movement: one step = one adjacent cell in 0.18 s", !s.IsMoving && s.Cell == start + Vector2Int.right &&
                s.Facing == FacingDirection.Right);
            var before = s.CompletedSteps;
            s.Tick(0f, new Vector2Int(1, 1));
            Check("movement: no diagonal step", !s.IsMoving && s.CompletedSteps == before);
            var held = s.CompletedSteps;
            s.PlaceForCheck(new Vector2Int(0, -1), FacingDirection.Down);
            for (var t = 0; t < 50; t++) s.Tick(0.02f, Vector2Int.left); // 1.0 s held
            Check("movement: holding a key keeps stepping (5 steps per second)", s.CompletedSteps - held == 5,
                (s.CompletedSteps - held).ToString());
            var tree = s.World.Objects.First(o => o.DefinitionId == "tree_basic");
            s.PlaceForCheck(tree.RootCell + Vector2Int.left, FacingDirection.Down);
            s.Tick(0.02f, Vector2Int.right);
            Check("movement: blocked tree cell refuses the step but turns the player",
                !s.IsMoving && s.Cell == tree.RootCell + Vector2Int.left && s.Facing == FacingDirection.Right);
            s.PlaceForCheck(new Vector2Int(s.World.MaxCell.x, 3), FacingDirection.Down);
            s.Tick(0.02f, Vector2Int.right);
            Check("movement: map bounds block like the 2D Grid", !s.IsMoving);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- Interaction

        private static void InteractionChecks(Context c)
        {
            var s = c.Session;
            var tree = s.World.Objects.First(o => o.DefinitionId == "tree_basic");
            s.PlaceForCheck(tree.RootCell + Vector2Int.down, FacingDirection.Up);
            Check("interaction: A ahead of a tree shows its message", s.InteractAhead() && s.LastMessage == "き。", s.LastMessage);
            s.PlaceForCheck(tree.RootCell + Vector2Int.left, FacingDirection.Right);
            Check("interaction: tree also answers from the side (ExamineInteractable rule)", s.InteractAhead());
            s.PlaceForCheck(tree.RootCell + Vector2Int.down, FacingDirection.Left);
            Check("interaction: facing away does nothing", !s.InteractAhead());
            var east = s.World.FindInstance("obj_sign_east_v30_preview");
            s.PlaceForCheck(east.RootCell + Vector2Int.down, FacingDirection.Up);
            Check("interaction: sign uses the Instance signText", s.InteractAhead() && s.LastMessage == "こっちは東。", s.LastMessage);
            s.PlaceForCheck(east.RootCell + Vector2Int.left, FacingDirection.Right);
            Check("interaction: sign keeps its authored front Action Point", !s.InteractAhead());
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- 2D/3D switching

        private static void SwitchChecks(Context c)
        {
            var s = c.Session;
            var ok = true;
            var directions = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            var actorPositionOk = true;
            for (var i = 0; i < 40; i++)
            {
                Walk(c, directions[i % 4], 1);
                var cell = s.Cell;
                var facing = s.Facing;
                var steps = s.CompletedSteps;
                var loads = s.MapLoads;
                var map = s.World;
                c.Switcher.Toggle();
                Render(c, 1024, 576);
                ok &= s.Cell == cell && s.Facing == facing && s.CompletedSteps == steps && s.MapLoads == loads && s.World == map;
                var expected = GridSpace3D.ToWorld(s.Position);
                var tolerance = c.Switcher.Mode == PocViewMode.PixelView2D ? c.Pixel.PixelWorldNorth : 1e-4f;
                actorPositionOk &= Vector3.Distance(c.Actor.transform.position, expected) <= tolerance;
            }
            Check("switch: 40 toggles keep cell, facing, steps, map and loads", ok, "switches " + c.Switcher.SwitchCount);
            Check("switch: player drawn at the shared cell in both views (2D within one pixel)", actorPositionOk);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            s.PlaceForCheck(new Vector2Int(3, -2), FacingDirection.Up);
            c.Third.SnapToTarget();
            Render(c, 1024, 576);
            var cam = c.Third.ViewCamera.transform.position;
            var player = GridSpace3D.ToWorld(s.Cell);
            Check("3D camera: behind the player (South of a North-facing player)", cam.z < player.z - 1f && Mathf.Abs(cam.x - player.x) < 0.01f,
                cam.ToString("F2"));
            s.PlaceForCheck(new Vector2Int(3, -2), FacingDirection.Right);
            for (var i = 0; i < 120; i++) c.Third.UpdateView(1f / 60f);
            cam = c.Third.ViewCamera.transform.position;
            Check("3D camera: turns behind an East-facing player within 2 s", cam.x < player.x - 1f && Mathf.Abs(cam.z - player.z) < 0.05f,
                cam.ToString("F2"));
            // Smooth follow: the camera never moves more than one step distance per frame.
            var lane = FindClearLane(s.World, 6);
            s.PlaceForCheck(lane, FacingDirection.Right);
            c.Third.SnapToTarget();
            c.Third.UpdateView(1f / 60f);
            var last = c.Third.ViewCamera.transform.position;
            var maxJump = 0f;
            var laneSteps = s.CompletedSteps;
            for (var i = 0; i < 6; i++)
            {
                s.Tick(0f, Vector2Int.right);
                for (var f = 0; f < 11; f++)
                {
                    s.Tick(1f / 60f, Vector2Int.right);
                    c.Third.UpdateView(1f / 60f);
                    maxJump = Mathf.Max(maxJump, Vector3.Distance(last, c.Third.ViewCamera.transform.position));
                    last = c.Third.ViewCamera.transform.position;
                }
            }
            Check("3D camera: smooth follow while walking 6 cells (max move per 60 fps frame < 0.06)",
                s.CompletedSteps - laneSteps >= 5 && maxJump > 0.01f && maxJump < 0.06f,
                $"steps {s.CompletedSteps - laneSteps}, max {maxJump:F3}");
            c.Input.Scheme = Poc3DControlScheme.CameraRelative;
            var first = c.Input.Resolve(Vector2Int.up, true, 90f);
            var stillHeld = c.Input.Resolve(Vector2Int.up, true, 180f);
            c.Input.Resolve(Vector2Int.zero, true, 180f);
            var afterRelease = c.Input.Resolve(Vector2Int.up, true, 180f);
            c.Input.Resolve(Vector2Int.zero, true, 0f);
            c.Input.Scheme = Poc3DControlScheme.WorldFixed;
            Check("3D keys B: camera-relative W follows the yaw latched at press time",
                first == Vector2Int.right && stillHeld == Vector2Int.right && afterRelease == Vector2Int.down,
                $"{first} {stillHeld} {afterRelease}");
            Check("3D keys A: world-fixed W is always North", c.Input.Resolve(Vector2Int.up, true, 180f) == Vector2Int.up);
            c.Input.Resolve(Vector2Int.zero, true, 0f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        private static Vector2Int FindClearLane(SharedWorldModel world, int length)
        {
            for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
            for (var x = world.MinCell.x; x <= world.MaxCell.x - length; x++)
            {
                var clear = true;
                for (var i = 0; i <= length; i++) clear &= world.CanEnter(new Vector2Int(x + i, y));
                if (clear && !(world.TryGetHouseDoor(out var door) && door.y == y)) return new Vector2Int(x, y);
            }
            throw new InvalidOperationException("No clear lane");
        }

        // ---------------------------------------------------------------- Picking

        private static void PickingChecks(Context c)
        {
            var s = c.Session;
            var tree = s.World.Objects.First(o => o.DefinitionId == "tree_basic");
            var crown = GridSpace3D.ToWorld(tree.RootCell, 1.0f);
            s.PlaceForCheck(tree.RootCell + new Vector2Int(0, -3), FacingDirection.Up);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Render(c, 1024, 576);
            var rtPixel = c.Pixel.WorldCamera.WorldToScreenPoint(crown);
            var screen = new Vector2(rtPixel.x * c.Pixel.Scale - c.Pixel.Offset.x, rtPixel.y * c.Pixel.Scale - c.Pixel.Offset.y);
            var picked2D = c.Switcher.PickObject(screen);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            Render(c, 1024, 576);
            var picked3D = c.Switcher.PickObject(c.Third.ViewCamera.WorldToScreenPoint(crown));
            Check("picking: clicking the same tree in 2D and 3D resolves the same Object Instance",
                picked2D != null && picked3D != null && picked2D.InstanceId == tree.InstanceId && picked3D.InstanceId == tree.InstanceId,
                $"{picked2D?.InstanceId} / {picked3D?.InstanceId}");
            s.PlaceForCheck(tree.RootCell + Vector2Int.down, FacingDirection.Up);
            Check("picking: click interaction uses the shared range rule", s.InteractWith(picked3D));
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- House

        private static void HouseTransitionChecks(Context c)
        {
            var s = c.Session;
            s.World.TryGetHouseDoor(out var door);
            var stepsBefore = s.CompletedSteps;
            var loadsBefore = s.MapLoads;
            Check("house: path from start to the door alcove", WalkPath(c, door));
            Check("house: stepping into the door loads halka_house at interior_entry facing Up",
                s.World.MapId == "halka_house" && s.World.TryGetMarker("interior_entry", out var entry) &&
                s.Cell == entry && s.Facing == FacingDirection.Up && s.MapLoads == loadsBefore + 1, s.World.MapId + " " + s.Cell);
            c.Presenter.Rebuild(s.World);
            Check("house: interior presentation rebuilt from shared data", c.Presenter.BuiltMapId == "halka_house" &&
                s.World.Objects.All(o => c.Presenter.FindView(o.InstanceId) != null));
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            s.PlaceForCheck(new Vector2Int(2, -3), FacingDirection.Up);
            c.Third.SnapToTarget();
            c.Third.HideOccluders = false;
            Render(c, 1024, 576);
            var visibleCount = CountBlocking(c);
            c.Third.HideOccluders = true;
            Render(c, 1024, 576);
            Check("house 3D: south wall would cover the player; hiding occluders clears the view",
                visibleCount > 0 && c.Third.HiddenOccluders > 0 && CountBlocking(c, true) == 0,
                $"blocking {visibleCount}, hidden {c.Third.HiddenOccluders}");
            c.Third.IndoorMode = PocIndoorCamera.CloserOffsetAndClamp;
            c.Third.SnapToTarget();
            Render(c, 1024, 576);
            var camera = c.Third.ViewCamera.transform.position;
            var room = s.World;
            Check("house 3D: clamp mode keeps the camera above the room",
                camera.z >= (room.MinCell.y + 0.5f) * GridSpace3D.CellSize - 0.01f, camera.ToString("F2"));
            c.Third.IndoorMode = PocIndoorCamera.CloserOffset;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("house: switching to 2D shows every wall hidden for the 3D camera again",
                c.Presenter.Views.All(v => v.Renderers.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)));
            s.World.TryGetMarker("interior_entry", out var back);
            s.PlaceForCheck(back, FacingDirection.Down);
            s.World.TryGetMarker("interior_exit", out var exit);
            Walk(c, exit - s.Cell, 1);
            Check("house: interior exit returns outside below the door facing Down",
                s.World.MapId == "first_field" && s.Cell == door + Vector2Int.down && s.Facing == FacingDirection.Down,
                s.World.MapId + " " + s.Cell);
            c.Presenter.Rebuild(s.World);
            Check("house: map changes counted no extra steps", s.CompletedSteps - stepsBefore > 0);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        private static int CountBlocking(Context c, bool onlyVisible = false)
        {
            var from = c.Third.ViewCamera.transform.position;
            var to = GridSpace3D.ToWorld(c.Session.Position) + Vector3.up * 0.5f;
            var dir = to - from;
            var ray = new Ray(from, dir.normalized);
            var count = 0;
            foreach (var view in c.Presenter.Views)
            {
                if (!view.CameraOccluder) continue;
                if (onlyVisible && view.Renderers.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)) continue;
                if (view.TryRaycast(ray, out var d) && d < dir.magnitude - 0.15f) count++;
            }
            return count;
        }

        // ---------------------------------------------------------------- Pixel view

        private static void PixelViewChecks(Context c)
        {
            Check("pixel view: desktop 1024x576 at 16 px/cell is a 512x288 texture at x2",
                PocPixelView2D.ChooseScale(576, 16) == 2);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            foreach (var (w, h) in new[] { (1024, 576), (1280, 720), (780, 1688), (1170, 2532) })
            foreach (var ppc in new[] { 16, 24, 32 })
            {
                c.Pixel.PixelsPerCell = ppc;
                Render(c, w, h);
                var rt = c.Pixel.Target;
                var cellsTall = rt.height / (float)ppc;
                Check($"pixel view: {w}x{h} @{ppc}px/cell integer cover (x{c.Pixel.Scale}, {rt.width}x{rt.height}, {cellsTall:0.0} cells tall)",
                    rt.width * c.Pixel.Scale >= w && rt.height * c.Pixel.Scale >= h &&
                    rt.width * c.Pixel.Scale - w < 2 * c.Pixel.Scale && rt.height * c.Pixel.Scale - h < 2 * c.Pixel.Scale &&
                    cellsTall > 11.9f && cellsTall < 24.1f);
            }
            c.Pixel.PixelsPerCell = 16;
            var aligned = true;
            for (var i = 0; i < 30; i++)
            {
                c.Session.Tick(0.03f, Vector2Int.right);
                Render(c, 1024, 576, 0.03f);
                var p = c.Pixel.WorldCamera.transform.position + c.Pixel.WorldCamera.transform.forward * 30f;
                aligned &= Mathf.Abs(p.x / c.Pixel.PixelWorldSize - Mathf.Round(p.x / c.Pixel.PixelWorldSize)) < 1e-3f &&
                           Mathf.Abs(p.z / c.Pixel.PixelWorldNorth - Mathf.Round(p.z / c.Pixel.PixelWorldNorth)) < 1e-3f;
            }
            Check("pixel view: camera moves in whole low-resolution pixels while following", aligned);
            c.Session.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- AUTO link

        // Not the AutoModeController itself: it checks that the planners AUTO already uses
        // (GridPathfinder2D, IndoorAutoRoutePlanner2D) run unchanged on the shared Grid and
        // drive the one shared player through TryStep, identically in either view.
        private static void AutoLinkChecks(Context c)
        {
            var s = c.Session;
            var results = new List<string>();
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                c.Switcher.SetMode(mode);
                s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
                var route = new List<Vector2Int>();
                var found = GridPathfinder2D.TryFind(null, s.Cell, new Vector2Int(5, 0), route, s.World.CanEnter);
                var trace = new List<Vector2Int>();
                foreach (var cell in route)
                {
                    if (!s.TryStep(cell - s.Cell)) break;
                    for (var i = 0; i < 12; i++) s.Tick(0.02f, Vector2Int.zero);
                    Render(c, 1024, 576);
                    trace.Add(s.Cell);
                }
                results.Add((found ? "ok" : "no route") + ":" + string.Join(">", trace));
            }
            Check("AUTO link: the existing GridPathfinder2D route walks the shared player the same in 2D and 3D",
                results[0] == results[1] && results[0].StartsWith("ok:") && results[0].EndsWith("(5, 0)"), results[0]);
            var room = AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.InteriorMapPath);
            var indoor = new SharedWorldModel(room);
            room.TryGetMarker("interior_entry", out var entry);
            var destinations = IndoorAutoRoutePlanner2D.CollectDestinations(room, entry, null, indoor.CanEnter);
            room.TryGetMarker("interior_exit", out var exit);
            Check("AUTO link: the existing indoor destination planner accepts the shared Grid (no exit, no furniture)",
                destinations.Count > 0 && !destinations.Contains(exit) && destinations.All(indoor.CanEnter),
                destinations.Count + " destinations");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            s.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
        }

        // ---------------------------------------------------------------- Time

        private static void TimeChecks(Context c)
        {
            var t = c.Time;
            t.Override = PocPhaseOverride.Morning;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            t.Apply(0f);
            t.Override = PocPhaseOverride.Night;
            t.Apply(1f);
            t.Apply(1f + WorldNightOverlay2D.FadeSeconds * 0.5f);
            var mid = t.PixelOverlayAlpha;
            t.Apply(1f + WorldNightOverlay2D.FadeSeconds + 0.01f);
            Check("time 2D: night uses the shipped overlay alpha (outdoor 0.33) with the 2.5 s fade",
                Mathf.Abs(t.PixelOverlayAlpha - WorldNightOverlay2D.OutdoorNightAlpha) < 0.001f && mid > 0.1f && mid < 0.25f,
                $"mid {mid:F3}, end {t.PixelOverlayAlpha:F3}");
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            t.Apply(10f);
            Check("time 3D: night dims light instead of drawing the overlay", t.PixelOverlayAlpha == 0f &&
                RenderSettings.ambientLight.b > RenderSettings.ambientLight.r);
            t.Override = PocPhaseOverride.Auto;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
        }

        // ---------------------------------------------------------------- MAP EDITOR fixture

        private static void FixtureChecks(Context c)
        {
            var fixturePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "POC2D3DFixtures",
                "first_field_tree_moved.hwmap.json"));
            Check("fixture: MAP EDITOR Core output exists (tools/Poc2D3DFixture)", File.Exists(fixturePath), fixturePath);
            if (!File.Exists(fixturePath)) return;
            const string treeId = "obj_b8083b939c355574929e534054d4ad43";
            var official = new SharedWorldModel(AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath));
            var fixtureMap = PocMapJson.ToDefinition(PocMapJson.Load(fixturePath));
            try
            {
                c.Session.UseOutdoorMapForTest(fixtureMap);
                c.Presenter.Rebuild(c.Session.World);
                var before = official.FindInstance(treeId).RootCell;
                var after = c.Session.World.FindInstance(treeId).RootCell;
                Check("fixture: tree moved one cell by MAP EDITOR", (after - before).sqrMagnitude == 1, $"{before} -> {after}");
                var removed = official.BlockedCells.Except(c.Session.World.BlockedCells).ToList();
                var added = c.Session.World.BlockedCells.Except(official.BlockedCells).ToList();
                Check("fixture: only the tree's blocked cell moved", removed.SequenceEqual(new[] { before }) &&
                    added.SequenceEqual(new[] { after }));
                var view = c.Presenter.FindView(treeId);
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                Render(c, 1024, 576);
                var at2D = view.transform.position;
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                Render(c, 1024, 576);
                var at3D = view.transform.position;
                Check("fixture: moved tree appears at the new cell in 2D and 3D (same GameObject)",
                    at2D == at3D && (at3D - GridSpace3D.ToWorld(after)).sqrMagnitude < 1e-8f, at3D.ToString("F2"));
                c.Session.PlaceForCheck(after + Vector2Int.left, FacingDirection.Right);
                c.Session.Tick(0.02f, Vector2Int.right);
                Check("fixture: collision follows the moved tree", !c.Session.IsMoving);
                c.Session.PlaceForCheck(before + Vector2Int.left, FacingDirection.Right);
                c.Session.Tick(0.02f, Vector2Int.right);
                Check("fixture: the old tree cell is walkable", c.Session.IsMoving);
            }
            finally
            {
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                UnityEngine.Object.DestroyImmediate(fixtureMap);
            }
        }
    }
}
