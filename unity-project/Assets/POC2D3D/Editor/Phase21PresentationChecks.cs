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
    // はるかわーるど ver3.0 Phase 2.1: 3D camera-relative input, 2D pixel outline and the
    // TEMPORARY ﾊﾙｶﾁｬﾝ model.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase21PresentationChecks.RunBatch
    public static class Phase21PresentationChecks
    {
        private static int passes;
        private static readonly List<string> failures = new List<string>();
        private static float clock;

        [MenuItem("HALKA/POC 2D3D/Run Phase 2.1 Presentation Checks")]
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
            QuantizationChecks();
            PlaceholderAssetChecks();
            var holder = Open(out var c);
            try
            {
                InputChecks(c);
                InteractionFacingChecks(c);
                AutoAndTwoDChecks(c);
                SwitchChecks(c);
                OutlineChecks(c);
                PlaceholderSceneChecks(c);
                SpriteChecks(c);
                FadeChecks(c);
                SwapChecks(c);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
            foreach (var failure in failures) Debug.LogError("PHASE21 FAIL " + failure);
            Debug.Log($"HARUKA Phase 2.1 presentation checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            if (condition) { passes++; Debug.Log("PHASE21 PASS " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add(name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- pure mapping

        private static void QuantizationChecks()
        {
            var dirs = new[] { FacingDirection.Up, FacingDirection.Right, FacingDirection.Down, FacingDirection.Left };
            var table = true;
            foreach (var heading in dirs)
            {
                var yaw = GridSpace3D.FacingYaw(heading);
                var forward = heading.ToVector();
                var right = new Vector2Int(forward.y, -forward.x);
                table &= GridSpace3D.CameraRelative(Vector2Int.up, yaw) == forward &&
                         GridSpace3D.CameraRelative(Vector2Int.down, yaw) == -forward &&
                         GridSpace3D.CameraRelative(Vector2Int.right, yaw) == right &&
                         GridSpace3D.CameraRelative(Vector2Int.left, yaw) == -right;
            }
            Check("3D keys: W/S/A/D map to forward/back/left/right for all four camera headings", table);
            Check("3D keys: camera yaw is quantized to the nearest cardinal (44°→N, 46°→E, 314°→W, 181°→S)",
                GridSpace3D.HeadingFromYaw(44f) == FacingDirection.Up && GridSpace3D.HeadingFromYaw(46f) == FacingDirection.Right &&
                GridSpace3D.HeadingFromYaw(314f) == FacingDirection.Left && GridSpace3D.HeadingFromYaw(181f) == FacingDirection.Down &&
                GridSpace3D.HeadingFromYaw(-46f) == FacingDirection.Left);
        }

        // ---------------------------------------------------------------- placeholder asset

        private static void PlaceholderAssetChecks()
        {
            var root = PocSceneBuilder.RepositoryRoot;
            Check("placeholder (blocky): Blender source, script, manifest and previews exist",
                File.Exists(Path.Combine(root, "blender/sources/phase2_1/haruka_blocky_placeholder.blend")) &&
                File.Exists(Path.Combine(root, "blender/scripts/build_haruka_blocky.py")) &&
                File.Exists(Path.Combine(root, "blender/exports/phase2_1_haruka_blocky_placeholder_manifest.json")) &&
                new[] { "south_front", "east", "north_back", "west" }.All(v =>
                    File.Exists(Path.Combine(root, $"blender/exports/previews/phase2_1/haruka_blocky_placeholder_{v}.png"))));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.PlaceholderPlayerPath);
            Check("placeholder: FBX imported and named as temporary (blocky)", model != null &&
                model.name.Contains("placeholder") && model.name.Contains("blocky"));
            if (model == null) return;
            // Minecraft-like: every face of every part is axis-aligned (boxes only).
            var axisAligned = model.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh.normals.All(n =>
                Mathf.Abs(n.x) > 0.999f || Mathf.Abs(n.y) > 0.999f || Mathf.Abs(n.z) > 0.999f));
            Check("placeholder: blocky like a Minecraft character (only axis-aligned box faces)", axisAligned);
            if (model == null) return;
            var parts = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            Check("placeholder: limb nodes for the simple walk swing (ArmL/ArmR/LegL/LegR)",
                new[] { "ArmL", "ArmR", "LegL", "LegR" }.All(parts.Contains), string.Join(",", parts));
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var toon = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            Check("placeholder: every part uses the one shared vertex-color material",
                renderers.Length == 5 && renderers.All(r => r.sharedMaterials.Length == 1 && r.sharedMaterial == toon));
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Check("placeholder: feet on the ground at the root, about two cells tall, scale 1",
                Mathf.Abs(bounds.min.y) < 0.01f && bounds.max.y > 0.9f && bounds.max.y < 1.0f &&
                (model.transform.lossyScale - Vector3.one).sqrMagnitude < 1e-6f, bounds.ToString("F3"));
            var body = model.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == model.name || f.transform == model.transform).sharedMesh;
            var eyes = Centroid(body, new Color32(0xfe, 0x1b, 0x6d, 255), 0.5f); // pink under the eyes: face only
            var clip = Centroid(body, new Color32(0xff, 0xff, 0xff, 255), 0.7f);
            Check("placeholder: face (pink under the eyes) on the South side, the white clip on the character's right (-X)",
                eyes.HasValue && eyes.Value.z < -0.15f && clip.HasValue && clip.Value.x < -0.09f,
                $"eyes {eyes?.ToString("F3")}, clip {clip?.ToString("F3")}");
        }

        private static Vector3? Centroid(Mesh mesh, Color32 color, float minHeight)
        {
            var colors = mesh.colors32;
            var vertices = mesh.vertices;
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < colors.Length; i++)
                if (Mathf.Abs(colors[i].r - color.r) <= 2 && Mathf.Abs(colors[i].g - color.g) <= 2 &&
                    Mathf.Abs(colors[i].b - color.b) <= 2 && vertices[i].y > minHeight) { sum += vertices[i]; count++; }
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
            var holder = new GameObject("Phase 2.1 check holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        // One simulated frame in the runtime order: input -> session -> actor -> camera.
        private static void Frame(Ctx c, float dt, Vector2Int raw)
        {
            clock += dt;
            c.Input.SetRaw(raw, clock);
            c.Session.Tick(dt, c.Input.ResolveDirection(c.Session));
            c.Actor.Apply(dt);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(dt, 1024, 576);
            else c.Third.UpdateView(dt, clock);
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
            return cells;
        }

        private static List<Vector2Int> Release(Ctx c, float seconds)
        {
            var cells = new List<Vector2Int>();
            for (var t = 0f; t < seconds; t += 1f / 60f)
            {
                var before = c.Session.Cell;
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (c.Session.Cell != before) cells.Add(c.Session.Cell);
            }
            return cells;
        }

        // A short tap: press, then release long enough for the step to finish.
        private static List<Vector2Int> Tap(Ctx c, Vector2Int raw, float press, float rest)
        {
            var cells = Hold(c, raw, press);
            cells.AddRange(Release(c, rest));
            return cells;
        }

        private static void Enter3D(Ctx c, Vector2Int cell, FacingDirection facing)
        {
            Release(c, 0.1f);
            c.Session.PlaceForCheck(cell, facing);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            Frame(c, 1f / 60f, Vector2Int.zero);
            Release(c, 0.6f);
        }

        private static Vector2Int OpenArea(SharedWorldModel world, int radius)
        {
            for (var y = world.MinCell.y + radius; y <= world.MaxCell.y - radius; y++)
            for (var x = world.MinCell.x + radius; x <= world.MaxCell.x - radius; x++)
            {
                var ok = true;
                for (var dy = -radius; dy <= radius && ok; dy++)
                for (var dx = -radius; dx <= radius && ok; dx++)
                    ok &= world.CanEnter(new Vector2Int(x + dx, y + dy)) &&
                          !(world.TryGetHouseDoor(out var door) && door == new Vector2Int(x + dx, y + dy));
                if (ok) return new Vector2Int(x, y);
            }
            throw new InvalidOperationException("No open area of radius " + radius);
        }

        // ---------------------------------------------------------------- 3D input

        private static void InputChecks(Ctx c)
        {
            // Phase 4 made the 3D camera north-fixed by default; these checks keep testing the turn-behind mode (still selectable).
            c.Third.Follow = PocCameraFollow.TurnBehindWhenIdle;
            var s = c.Session;
            var center = OpenArea(s.World, 2);
            Check("3D keys: camera-relative is the default scheme in 3D", c.Input.Scheme == Poc3DControlScheme.CameraRelative);

            Enter3D(c, center + Vector2Int.up * 2, FacingDirection.Up);
            var start = s.Cell;
            var back = Hold(c, Vector2Int.down, 0.75f);
            Check("3D S held: walks straight back toward the camera, never turns around",
                back.Count >= 3 && back.Select((cell, i) => cell == start + Vector2Int.down * (i + 1)).All(b => b) &&
                c.Third.Heading == FacingDirection.Up && s.Facing == FacingDirection.Down,
                $"{back.Count} steps, heading {c.Third.Heading}");
            Release(c, 0.8f);
            Check("3D S released: the camera does not swing 180° (heading stays North)", c.Third.Heading == FacingDirection.Up);

            Enter3D(c, center + Vector2Int.up * 2, FacingDirection.Up);
            start = s.Cell;
            var repeated = new List<Vector2Int>();
            for (var i = 0; i < 3; i++) repeated.AddRange(Tap(c, Vector2Int.down, 0.1f, 0.6f));
            Check("3D S pressed three times: three steps South, no back-and-forth",
                repeated.SequenceEqual(new[] { start + Vector2Int.down, start + Vector2Int.down * 2, start + Vector2Int.down * 3 }),
                string.Join(">", repeated));

            Enter3D(c, center + Vector2Int.right * 2, FacingDirection.Up);
            start = s.Cell;
            var left = Hold(c, Vector2Int.left, 0.75f);
            var headingWhileHeld = c.Third.Heading;
            Check("3D A held: straight line West (no circling), camera heading unchanged while held",
                left.Count >= 3 && left.Select((cell, i) => cell == start + Vector2Int.left * (i + 1)).All(b => b) &&
                headingWhileHeld == FacingDirection.Up, $"{left.Count} steps, heading {headingWhileHeld}");
            left.AddRange(Release(c, 0.2f));
            var beforeDelay = c.Third.Heading;
            left.AddRange(Release(c, 0.4f));
            Check("3D A released: after the idle delay the camera swings behind the player (heading West)",
                beforeDelay == FacingDirection.Up && c.Third.Heading == FacingDirection.Left, $"{beforeDelay} -> {c.Third.Heading}");
            var yawMid = c.Third.CurrentYaw;
            var from = s.Cell;
            var forward = Hold(c, Vector2Int.up, 0.25f);
            Check("3D W while the camera is still turning: goes toward the new heading (predictable)",
                Mathf.Abs(Mathf.DeltaAngle(yawMid, 270f)) > 1f && forward.Count >= 1 && forward[0] == from + Vector2Int.left,
                $"yaw {yawMid:0}, from {from}, first {forward.FirstOrDefault()}");

            Enter3D(c, center + Vector2Int.left * 2, FacingDirection.Up);
            start = s.Cell;
            var right = Hold(c, Vector2Int.right, 0.75f);
            Check("3D D held: straight line East, camera heading unchanged while held",
                right.Count >= 3 && right.Select((cell, i) => cell == start + Vector2Int.right * (i + 1)).All(b => b) &&
                c.Third.Heading == FacingDirection.Up);

            Enter3D(c, center, FacingDirection.Up);
            start = s.Cell;
            var alternate = new List<Vector2Int>();
            alternate.AddRange(Tap(c, Vector2Int.left, 0.1f, 0.15f));
            alternate.AddRange(Tap(c, Vector2Int.right, 0.1f, 0.15f));
            alternate.AddRange(Tap(c, Vector2Int.left, 0.1f, 0.15f));
            Check("3D A/D alternated quickly: West, East, West around the same cell (no drift, no flip)",
                alternate.SequenceEqual(new[] { start + Vector2Int.left, start, start + Vector2Int.left }), string.Join(">", alternate));

            Enter3D(c, center, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.up);
            var target = s.StepToCell;
            Frame(c, 1f / 60f, Vector2Int.left);
            Frame(c, 1f / 60f, Vector2Int.left);
            Check("3D key changed mid-step: the step in progress keeps its target cell",
                s.IsMoving && s.StepToCell == target && target == center + Vector2Int.up);
            Release(c, 0.4f);
            c.Third.Follow = PocCameraFollow.NorthFixed; // the Phase 4 default
        }

        // ---------------------------------------------------------------- facing / interaction

        private static void InteractionFacingChecks(Ctx c)
        {
            var s = c.Session;
            var flower = s.World.Objects.First(o => o.DefinitionId == "flower_basic" && s.World.CanEnter(o.RootCell + Vector2Int.up));
            Enter3D(c, flower.RootCell + Vector2Int.up, FacingDirection.Up);
            Hold(c, Vector2Int.down, 0.1f);
            Release(c, 0.1f);
            Check("3D S into a flower behind: the player turns to face it (South) and A examines it",
                s.Cell == flower.RootCell + Vector2Int.up && s.Facing == FacingDirection.Down &&
                s.InteractAhead() && s.LastMessage == "はな。", s.LastMessage);
            var stone = s.World.Objects.First(o => o.DefinitionId == "stone_basic" && s.World.CanEnter(o.RootCell + Vector2Int.right));
            Enter3D(c, stone.RootCell + Vector2Int.right, FacingDirection.Up);
            Hold(c, Vector2Int.left, 0.1f);
            Release(c, 0.1f);
            Check("3D A toward a stone on the left: facing West, A examines the stone (not another object)",
                s.Facing == FacingDirection.Left && s.InteractAhead() && s.LastMessage == "いし。", s.LastMessage);
            var tree = s.World.Objects.First(o => o.DefinitionId == "tree_basic" && s.World.CanEnter(o.RootCell + Vector2Int.down));
            Enter3D(c, tree.RootCell + Vector2Int.down, FacingDirection.Up);
            Hold(c, Vector2Int.up, 0.1f);
            Release(c, 0.1f);
            Check("3D W into a tree ahead: facing North, A examines the tree", s.Facing == FacingDirection.Up &&
                s.InteractAhead() && s.LastMessage == "き。", s.LastMessage);
        }

        // ---------------------------------------------------------------- AUTO and 2D

        private static void AutoAndTwoDChecks(Ctx c)
        {
            var s = c.Session;
            var center = OpenArea(s.World, 2);
            Enter3D(c, center, FacingDirection.Right);
            var route = new List<Vector2Int>();
            var goal = center + new Vector2Int(-2, 2);
            GridPathfinder2D.TryFind(null, s.Cell, goal, route, s.World.CanEnter);
            var walked = new List<Vector2Int>();
            foreach (var cell in route)
            {
                s.TryStep(cell - s.Cell);
                for (var i = 0; i < 12; i++) Frame(c, 0.02f, Vector2Int.zero);
                walked.Add(s.Cell);
            }
            Check("AUTO path: world Grid directions, unaffected by the 3D camera heading (East)",
                walked.SequenceEqual(route) && s.Cell == goal, string.Join(">", walked));

            Release(c, 0.1f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Release(c, 0.1f);
            s.PlaceForCheck(center, FacingDirection.Right);
            var south = Hold(c, Vector2Int.down, 0.2f);
            Release(c, 0.2f);
            s.PlaceForCheck(center, FacingDirection.Right);
            var west = Hold(c, Vector2Int.left, 0.2f);
            Release(c, 0.2f);
            Check("2D keys: unchanged world-fixed (S = South, A = West) whatever the 3D camera heading is",
                south.FirstOrDefault() == center + Vector2Int.down && west.FirstOrDefault() == center + Vector2Int.left,
                "3D heading " + c.Third.Heading);
        }

        // ---------------------------------------------------------------- switching

        private static void SwitchChecks(Ctx c)
        {
            var s = c.Session;
            var center = OpenArea(s.World, 2);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Release(c, 0.1f);
            s.PlaceForCheck(center + Vector2Int.down * 2, FacingDirection.Left);
            Hold(c, Vector2Int.up, 0.25f); // still holding W: a step is in progress
            var stepping = s.StepToCell;
            var steps = s.CompletedSteps;
            var mapId = s.World.MapId;
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            var held = Hold(c, Vector2Int.up, 0.5f);
            Check("switch with W still held: only the step in progress finishes, no new step until release",
                c.Input.WaitingForRelease && s.Cell == stepping && !s.IsMoving && s.CompletedSteps <= steps + 1 &&
                s.World.MapId == mapId, $"held {string.Join(">", held)}");
            Release(c, 0.05f);
            var facingAtEntry = s.Facing;
            var after = Tap(c, Vector2Int.up, 0.1f, 0.3f);
            Check("switch: after releasing, W works again (camera-relative: forward = facing at entry)",
                after.Count == 1 && after[0] == stepping + facingAtEntry.ToVector(), $"{facingAtEntry} {after.FirstOrDefault()}");
            var ok = true;
            for (var i = 0; i < 20; i++)
            {
                var st = (s.Cell, s.Facing, s.CompletedSteps, s.MapLoads);
                c.Switcher.Toggle();
                Frame(c, 1f / 60f, Vector2Int.zero);
                ok &= st == (s.Cell, s.Facing, s.CompletedSteps, s.MapLoads);
            }
            Check("switch: 20 toggles keep cell, facing, steps and map", ok);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Release(c, 0.1f);
        }

        // ---------------------------------------------------------------- 2D outline

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

        private static (int changed, bool onlyNearObjects) Outline(Ctx c, int ppc, int px)
        {
            c.Pixel.PixelsPerCell = ppc;
            c.Pixel.OutlinePixels = px;
            c.Session.PlaceForCheck(new Vector2Int(5, -1), FacingDirection.Down);
            c.Pixel.SnapToTarget();
            Frame(c, 0f, Vector2Int.zero);
            c.Pixel.WorldCamera.Render();
            var raw = ReadPixels(c.Pixel.Target);
            var outlined = ReadPixels(c.Pixel.ComposeOutline());
            var mask = px > 0 ? ReadPixels(c.Pixel.OutlineMask) : null;
            var w = c.Pixel.Target.width;
            var h = c.Pixel.Target.height;
            var changed = 0;
            var near = true;
            for (var i = 0; i < raw.Length; i++)
            {
                if (raw[i].Equals(outlined[i])) continue;
                changed++;
                if (mask == null) continue;
                int x = i % w, y = i / w;
                var found = false;
                for (var dy = -2; dy <= 2 && !found; dy++)
                for (var dx = -2; dx <= 2 && !found; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    found = mask[ny * w + nx].r > 0 && mask[ny * w + nx].g > 0;
                }
                near &= found;
            }
            return (changed, near);
        }

        private static void OutlineChecks(Ctx c)
        {
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("2D outline: mask shader and composite material wired, 1 px by default, colour #252B39",
                c.Pixel.OutlineReady && c.Pixel.OutlinePixels == 1 &&
                (Color32)c.Pixel.OutlineColor is var color && color.r == 0x25 && color.g == 0x2b && color.b == 0x39);
            foreach (var ppc in new[] { 16, 32 })
            {
                var off = Outline(c, ppc, 0);
                var one = Outline(c, ppc, 1);
                var two = Outline(c, ppc, 2);
                var cellsTall = c.Pixel.Target.height / (float)ppc;
                Check($"2D outline {ppc}px/cell: off changes nothing, 1 px adds lines, 2 px adds more",
                    off.changed == 0 && one.changed > 50 && two.changed > one.changed, $"{off.changed}/{one.changed}/{two.changed}");
                Check($"2D outline {ppc}px/cell: lines only next to outlined objects (never on bare ground or road)",
                    one.onlyNearObjects && two.onlyNearObjects);
                Check($"2D outline {ppc}px/cell: same visible area (18 cells tall)", Mathf.Abs(cellsTall - 18f) < 0.6f,
                    cellsTall.ToString("F2"));
            }
            c.Pixel.PixelsPerCell = 16;
            c.Pixel.OutlinePixels = 1;
            Check("2D outline: separate low-resolution textures, same size as the view (UI is drawn later by IMGUI)",
                c.Pixel.OutlineMask.width == c.Pixel.Target.width && c.Pixel.OutlineMask.height == c.Pixel.Target.height &&
                c.Pixel.ComposeOutline() != c.Pixel.Target);
            var aligned = true;
            for (var i = 0; i < 30; i++)
            {
                Frame(c, 0.03f, Vector2Int.right);
                var p = c.Pixel.WorldCamera.transform.position + c.Pixel.WorldCamera.transform.forward * 30f;
                aligned &= Mathf.Abs(p.x / c.Pixel.PixelWorldSize - Mathf.Round(p.x / c.Pixel.PixelWorldSize)) < 1e-3f &&
                           Mathf.Abs(p.z / c.Pixel.PixelWorldNorth - Mathf.Round(p.z / c.Pixel.PixelWorldNorth)) < 1e-3f;
            }
            Release(c, 0.3f);
            Check("2D outline: pixel-grid camera snapping still holds while walking", aligned);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Check("3D: no outline pass (pixel camera and presenter off, 3D camera on)",
                !c.Pixel.enabled && !c.Pixel.WorldCamera.enabled && c.Third.ViewCamera.enabled);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Time.Override = PocPhaseOverride.Night;
            c.Time.Apply(clock);
            c.Time.Apply(clock + WorldNightOverlay2D.FadeSeconds + 0.1f);
            var night = (Color)PocTimeOfDay.NightColor;
            var white = Color.Lerp(Color.white, night, c.Time.PixelOverlayAlpha);
            var ink = Color.Lerp(c.Pixel.OutlineColor, night, c.Time.PixelOverlayAlpha);
            Check("night 2D: white petals stay bright and the outline stays a dark blue-grey, not black",
                Mathf.Abs(c.Time.PixelOverlayAlpha - WorldNightOverlay2D.OutdoorNightAlpha) < 0.001f &&
                white.grayscale > 0.6f && ink.maxColorComponent > 0.12f, $"white {white}, ink {ink}");
            c.Time.Override = PocPhaseOverride.Auto;
        }

        // ---------------------------------------------------------------- placeholder in the scene

        private static void PlaceholderSceneChecks(Ctx c)
        {
            var s = c.Session;
            Check("placeholder: the catalog marks the player model as temporary",
                c.Catalog.Player.placeholder && c.Actor.ModelInstance != null && c.Actor.ModelInstance.name.Contains("placeholder"));
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            var facingOk = true;
            foreach (FacingDirection facing in Enum.GetValues(typeof(FacingDirection)))
            {
                s.PlaceForCheck(new Vector2Int(1, -3), facing);
                c.Actor.Apply(0f);
                var body = c.Actor.ModelInstance.GetComponent<MeshFilter>().sharedMesh;
                var eyes = Centroid(body, new Color32(0xfe, 0x1b, 0x6d, 255), 0.5f);
                var world = c.Actor.ModelInstance.transform.TransformPoint(eyes ?? Vector3.zero) - c.Actor.transform.position;
                var dir = facing.ToVector();
                facingOk &= eyes.HasValue && Vector2.Dot(new Vector2(world.x, world.z).normalized, new Vector2(dir.x, dir.y)) > 0.9f;
            }
            Check("placeholder 3D: the face looks along the Grid facing in all four directions", facingOk);
            s.PlaceForCheck(new Vector2Int(1, -3), FacingDirection.Down);
            c.Actor.Apply(0f);
            Check("placeholder 3D: drawn exactly at the shared cell, upright, normal scale, visible",
                (c.Actor.transform.position - GridSpace3D.ToWorld(s.Position)).sqrMagnitude < 1e-8f &&
                Vector3.Angle(c.Actor.transform.up, Vector3.up) < 0.01f &&
                Mathf.Abs(c.Actor.ModelInstance.transform.localScale.x - 1f) < 1e-4f && c.Actor.ModelVisible &&
                c.Actor.ModelInstance.GetComponentsInChildren<Renderer>().All(r => r.enabled));
            s.PlaceForCheck(new Vector2Int(1, -3), FacingDirection.Right);
            var start = s.Cell;
            s.Tick(0f, Vector2Int.right);
            s.Tick(0.045f, Vector2Int.zero);
            c.Actor.Apply(0f);
            var midSwing = c.Actor.CurrentSwingDegrees;
            var midCell = s.Cell;
            for (var i = 0; i < 10; i++) s.Tick(0.02f, Vector2Int.zero);
            c.Actor.Apply(0f);
            Check("placeholder walk: limbs swing mid-step and rest when stopped; the logical step is unchanged (0.18 s, one cell)",
                c.Actor.HasLimbs && Mathf.Abs(midSwing) > 5f && Mathf.Abs(c.Actor.CurrentSwingDegrees) < 0.01f &&
                midCell == start && s.Cell == start + Vector2Int.right, $"mid swing {midSwing:0.0}");
            var hashes = File.ReadAllLines(Path.Combine(PocSceneBuilder.RepositoryRoot, "tools/poc2d3d/player_sprite_sha256.txt"));
            var same = hashes.Where(line => line.Length > 66).All(line =>
            {
                var expected = line.Substring(0, 64);
                var path = Path.Combine(Application.dataPath, "..", line.Substring(65).TrimStart('*', ' '));
                using var sha = SHA256.Create();
                return File.Exists(path) && BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "")
                    .Equals(expected, StringComparison.OrdinalIgnoreCase);
            });
            Check("original Player sprites unchanged (all 50 PNG SHA-256 match the Phase 2.1 start)", same && hashes.Length == 50);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
        }

        // ---------------------------------------------------------------- 2D original sprite

        private const int ScreenW = 1024, ScreenH = 576;

        // GUIDs of each CharacterVisual sprite array in FirstDay.unity, read from the YAML so
        // the scene is never opened or saved.
        private static Dictionary<string, List<string>> FirstDaySpriteGuids()
        {
            var lines = File.ReadAllLines(Path.Combine(Application.dataPath, "Scenes/FirstDay.unity"));
            var result = new Dictionary<string, List<string>>();
            var start = Array.FindIndex(lines, l => l.Contains("Halka.Game.Player.CharacterVisual"));
            string field = null;
            for (var i = start + 1; i < lines.Length && !lines[i].StartsWith("---"); i++)
            {
                var line = lines[i];
                if (line.StartsWith("  ") && !line.StartsWith("  -") && line.TrimEnd().EndsWith(":"))
                {
                    field = line.Trim().TrimEnd(':');
                    result[field] = new List<string>();
                }
                else if (field != null && line.StartsWith("  - ") && line.Contains("guid: "))
                    result[field].Add(line.Substring(line.IndexOf("guid: ", StringComparison.Ordinal) + 6, 32));
            }
            return result;
        }

        private static Color32[] SpritePixels(Sprite sprite)
        {
            var rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sprite.texture, rt);
            var pixels = ReadPixels(rt);
            RenderTexture.ReleaseTemporary(rt);
            return pixels;
        }

        // Every opaque sprite pixel on screen is either the original color, or covered by
        // something nearer than the sprite's card in the mask (or that thing's outline).
        private static (int matched, int covered, int wrong) CompareSprite(Ctx c)
        {
            c.Pixel.WorldCamera.Render();
            var screen = ReadPixels(c.Pixel.ComposeScreen(ScreenW, ScreenH));
            var mask = ReadPixels(c.Pixel.OutlineMask);
            var sprite = c.Actor.CurrentSprite;
            var source = SpritePixels(sprite);
            var rect = c.Pixel.LastSpriteRect;
            var size = c.Pixel.LastSpritePixelSize;
            int mw = c.Pixel.OutlineMask.width, mh = c.Pixel.OutlineMask.height, scale = c.Pixel.Scale;
            var tw = sprite.texture.width;
            var tr = sprite.textureRect;
            int matched = 0, covered = 0, wrong = 0;
            float Depth(int x, int y)
            {
                if (x < 0 || y < 0 || x >= mw || y >= mh) return 1f;
                var m = mask[y * mw + x];
                if (m.r == 0 || m.r == PocOutlineTag.DecorationId) return 2f; // ground layer
                return (m.b + m.a / 255f) / 255f;
            }
            bool Strong(int x, int y) => x >= 0 && y >= 0 && x < mw && y < mh && mask[y * mw + x].g > 5;
            for (var y = 0; y < rect.height; y++)
            for (var x = 0; x < rect.width; x++)
            {
                int sx = (int)(x / size), sy = (int)(y / size);
                // Phase 5: on a grass cell the 2D game's grass hides the bottom sprite rows (the
                // feet); those rows are left out here and checked in Phase5ReleaseCandidateChecks.
                if (sy < c.Pixel.LastPlayerCropPixels) continue;
                var original = source[((int)tr.y + sy) * tw + (int)tr.x + sx];
                if (original.a < 128) continue;
                int px = rect.x + x, py = rect.y + y;
                if (px < 0 || py < 0 || px >= ScreenW || py >= ScreenH) continue;
                var shown = screen[py * ScreenW + px];
                var card = Mathf.Lerp(c.Pixel.LastSpriteDepthBottom, c.Pixel.LastSpriteDepthTop, (y + 0.5f) / rect.height);
                int lx = (px + c.Pixel.Offset.x) / scale, ly = (py + c.Pixel.Offset.y) / scale;
                if (shown.r == original.r && shown.g == original.g && shown.b == original.b) { matched++; continue; }
                var near = Depth(lx, ly) < card + 0.0005f;
                for (var d = -2; d <= 2 && !near; d++)
                    near = (Strong(lx + d, ly) && Depth(lx + d, ly) < card) || (Strong(lx, ly + d) && Depth(lx, ly + d) < card);
                if (near) covered++;
                else wrong++;
            }
            return (matched, covered, wrong);
        }

        private static void Place2D(Ctx c, Vector2Int cell, FacingDirection facing)
        {
            Release(c, 0.1f);
            c.Session.PlaceForCheck(cell, facing);
            c.Pixel.SnapToTarget();
            Frame(c, 0f, Vector2Int.zero);
        }

        private static void SpriteChecks(Ctx c)
        {
            var s = c.Session;
            var sprites = c.Catalog.Player.sprites;
            var scene = FirstDaySpriteGuids();
            string Guid(Sprite sprite) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite));
            bool Same(string field, Sprite[] frames) => scene.TryGetValue(field, out var guids) &&
                frames.Select(Guid).SequenceEqual(guids);
            Check("2D sprite: the catalog references the original sprites, same files and order as FirstDay's CharacterVisual",
                sprites.IsComplete && Same("frontIdle", sprites.frontIdle) && Same("backIdle", sprites.backIdle) &&
                Same("leftIdle", sprites.leftIdle) && Same("rightIdle", sprites.rightIdle) &&
                Same("frontWalk", sprites.frontWalk) && Same("backWalk", sprites.backWalk) &&
                Same("leftWalk", sprites.leftWalk) && Same("rightWalk", sprites.rightWalk) &&
                Mathf.Approximately(sprites.offsetY, 0.25f),
                string.Join(",", scene.Keys));

            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Pixel.PixelsPerCell = 16;
            c.Pixel.OutlinePixels = 1;
            var open = OpenArea(s.World, 2);
            Place2D(c, open, FacingDirection.Down);
            var hidden = !c.Actor.ModelVisible && c.Actor.ModelInstance.GetComponentsInChildren<Renderer>().All(r => !r.enabled);
            c.Pixel.WorldCamera.Render();
            var mask = ReadPixels(c.Pixel.OutlineMask);
            var playerInMask = mask.Any(m => m.r == PocOutlineTag.PlayerId);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 0f, Vector2Int.zero);
            var shown = c.Actor.ModelVisible && c.Actor.ModelInstance.GetComponentsInChildren<Renderer>().All(r => r.enabled);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 0f, Vector2Int.zero);
            Check("2D sprite: the 3D model is not drawn in 2D (nor in the outline mask) and is drawn again in 3D",
                hidden && !playerInMask && shown);

            // Animation: CharacterVisual's sets, 0.4 s idle frames, 0.15 s walk frames, restart on change.
            Place2D(c, open, FacingDirection.Up);
            s.PlaceForCheck(open, FacingDirection.Down);
            c.Actor.Apply(0f);
            var anim = c.Actor.CurrentSprite == sprites.frontIdle[0];
            c.Actor.Apply(0.39f);
            anim &= c.Actor.CurrentSprite == sprites.frontIdle[0];
            c.Actor.Apply(0.02f);
            anim &= c.Actor.CurrentSprite == sprites.frontIdle[1];
            s.Tick(0f, Vector2Int.right);
            c.Actor.Apply(0f);
            anim &= s.IsMoving && c.Actor.CurrentSprite == sprites.rightWalk[0];
            c.Actor.Apply(0.15f);
            anim &= c.Actor.CurrentSprite == sprites.rightWalk[1];
            c.Actor.Apply(0.31f);
            anim &= c.Actor.CurrentSprite == sprites.rightWalk[3];
            for (var i = 0; i < 20; i++) s.Tick(0.02f, Vector2Int.zero);
            c.Actor.Apply(0f);
            anim &= !s.IsMoving && c.Actor.CurrentSprite == sprites.rightIdle[0];
            foreach (var (facing, frames) in new[] { (FacingDirection.Up, sprites.backIdle),
                (FacingDirection.Left, sprites.leftIdle), (FacingDirection.Down, sprites.frontIdle) })
            {
                s.PlaceForCheck(open, facing);
                c.Actor.Apply(0f);
                anim &= c.Actor.CurrentSprite == frames[0];
            }
            Check("2D sprite: frames and timing equal CharacterVisual (idle 0.4 s, walk 0.15 s, 4 directions, restart on change)", anim);

            // Density and placement for PC 16 / 32 px per cell and a phone-sized screen.
            var density = true;
            var detail = "";
            foreach (var (ppc, w, h) in new[] { (16, ScreenW, ScreenH), (32, ScreenW, ScreenH), (16, 780, 1688) })
            {
                c.Pixel.PixelsPerCell = ppc;
                Place2D(c, open, FacingDirection.Down);
                c.Pixel.UpdateView(0f, w, h);
                c.Pixel.WorldCamera.Render();
                if (w == ScreenW) CompareSprite(c);
                else c.Pixel.ComposeScreen(w, h);
                var rect = c.Pixel.LastSpriteRect;
                var expected = c.Pixel.Scale * ppc / 32f;
                var cell = GridSpace3D.ToWorld(s.Position);
                var south = c.Pixel.WorldCamera.WorldToViewportPoint(cell + Vector3.back * GridSpace3D.CellSize * 0.5f);
                var sx = south.x * c.Pixel.Target.width * c.Pixel.Scale - c.Pixel.Offset.x;
                var sy = south.y * c.Pixel.Target.height * c.Pixel.Scale - c.Pixel.Offset.y;
                density &= Mathf.Abs(c.Pixel.LastSpritePixelSize - expected) < 1e-4f &&
                           rect.width == Mathf.RoundToInt(64 * expected) && rect.height == rect.width &&
                           Mathf.Abs(rect.y - sy) <= c.Pixel.Scale && Mathf.Abs(rect.x + rect.width * 0.5f - sx) <= c.Pixel.Scale;
                detail += $"{ppc}px {w}x{h}: x{c.Pixel.LastSpritePixelSize:0.##} {rect}; ";
            }
            c.Pixel.PixelsPerCell = 16;
            Frame(c, 0f, Vector2Int.zero);
            Check("2D sprite: the 2D game's density (64 px = 1 unit) at every Pixel View density; bottom on the cell's South edge, centred",
                density, detail);

            // No tree or house within 3 cells sideways and 6 cells South (tall crowns further South
            // reach up to the player's feet on screen and correctly cover them), and nothing at
            // all in the cells just South.
            bool Near(Vector2Int cell, Vector2Int d, int sideways, int south) =>
                Mathf.Abs(d.x - cell.x) <= sideways && d.y <= cell.y && d.y >= cell.y - south;
            var clear = Enumerable.Range(s.World.MinCell.y, s.World.MaxCell.y - s.World.MinCell.y + 1)
                .SelectMany(y => Enumerable.Range(s.World.MinCell.x, s.World.MaxCell.x - s.World.MinCell.x + 1)
                    .Select(x => new Vector2Int(x, y)))
                .First(cell => s.World.CanEnter(cell) && !s.World.Objects.Any(o =>
                    (o.DefinitionId != "flower_basic" && o.DefinitionId != "stone_basic" && Near(cell, o.RootCell, 3, 6)) ||
                    Near(cell, o.RootCell, 1, 2)));
            Place2D(c, clear, FacingDirection.Down);
            var plain = CompareSprite(c);
            Check("2D sprite: on open ground every opaque pixel is the original sprite color (nothing resampled, recolored or covered)",
                plain.wrong == 0 && plain.matched > 1000 && plain.covered == 0,
                $"cell {clear}: matched {plain.matched}, covered {plain.covered}, wrong {plain.wrong}");

            // Draw order against a tree: covered from the South, in front of it from the North.
            var southTree = s.World.Objects.First(o => o.DefinitionId == "tree_basic" && s.World.CanEnter(o.RootCell + Vector2Int.up));
            var northTree = s.World.Objects.First(o => o.DefinitionId == "tree_basic" && s.World.CanEnter(o.RootCell + Vector2Int.down));
            c.Pixel.FadeEnabled = false; // the plain draw order; see-through trees are checked below
            foreach (var px in new[] { 1, 0 })
            {
                c.Pixel.OutlinePixels = px;
                Place2D(c, southTree.RootCell + Vector2Int.up, FacingDirection.Down);
                var behind = CompareSprite(c);
                Place2D(c, northTree.RootCell + Vector2Int.down, FacingDirection.Up);
                var front = CompareSprite(c);
                Check($"2D sprite (outline {px}, see-through off): a tree South of the player covers it; a tree North of it stays behind",
                    behind.covered > 100 && behind.wrong == 0 && front.wrong == 0 && front.covered * 20 <= front.matched,
                    $"South tree {behind.matched}/{behind.covered}/{behind.wrong}, North tree {front.matched}/{front.covered}/{front.wrong}");
            }
            c.Pixel.OutlinePixels = 1;
            c.Pixel.FadeEnabled = true;

            // Pixel grid: while walking with the camera following, the sprite stays on whole
            // low-resolution pixels (no shimmer against the world).
            Place2D(c, open + Vector2Int.left * 2, FacingDirection.Right);
            var grid = true;
            var moved = 0;
            var last = new RectInt();
            for (var i = 0; i < 40; i++)
            {
                Frame(c, 1f / 60f, i < 30 ? Vector2Int.right : Vector2Int.zero);
                c.Pixel.ComposeScreen(ScreenW, ScreenH);
                var rect = c.Pixel.LastSpriteRect;
                grid &= (rect.x + c.Pixel.Offset.x) % c.Pixel.Scale == 0 && (rect.y + c.Pixel.Offset.y) % c.Pixel.Scale == 0;
                if (i > 0 && rect.position != last.position) moved++;
                last = rect;
            }
            Check("2D sprite: on whole low-resolution pixels every frame while walking", grid && moved > 0, $"moved {moved}");
        }

        // ---------------------------------------------------------------- 2D see-through trees

        // Every opaque sprite pixel is the original color, or the original blended with the
        // see-through layer at that layer's opacity, or a faded tree's outline, or covered by a
        // nearer (not faded) object in the mask.
        private static (int plain, int blended, int wrong) CompareFaded(Ctx c)
        {
            c.Pixel.WorldCamera.Render();
            var screen = ReadPixels(c.Pixel.ComposeScreen(ScreenW, ScreenH));
            var layer = c.Pixel.FadeLayer != null && c.Pixel.FadingCount > 0 ? ReadPixels(c.Pixel.FadeLayer) : null;
            var mask = ReadPixels(c.Pixel.OutlineMask);
            var sprite = c.Actor.CurrentSprite;
            var source = SpritePixels(sprite);
            var rect = c.Pixel.LastSpriteRect;
            int w = c.Pixel.Target.width, h = c.Pixel.Target.height, scale = c.Pixel.Scale, tw = sprite.texture.width;
            var tr = sprite.textureRect;
            int plain = 0, blended = 0, wrong = 0;
            float LayerAlpha(int x, int y) => layer == null || x < 0 || y < 0 || x >= w || y >= h ? 0f : layer[y * w + x].a / 255f;
            for (var y = 0; y < rect.height; y++)
            for (var x = 0; x < rect.width; x++)
            {
                var original = source[((int)tr.y + y) * tw + (int)tr.x + x];
                if (original.a < 128) continue;
                int px = rect.x + x, py = rect.y + y;
                if (px < 0 || py < 0 || px >= ScreenW || py >= ScreenH) continue;
                var shown = (Color)screen[py * ScreenW + px];
                int lx = (px + c.Pixel.Offset.x) / scale, ly = (py + c.Pixel.Offset.y) / scale;
                var a = LayerAlpha(lx, ly);
                var expected = a > 0f ? Color.Lerp(original, (Color)layer[ly * w + lx], a) : (Color)original;
                if (Mathf.Abs(shown.r - expected.r) < 0.016f && Mathf.Abs(shown.g - expected.g) < 0.016f &&
                    Mathf.Abs(shown.b - expected.b) < 0.016f)
                {
                    if (a > 0f) blended++;
                    else plain++;
                    continue;
                }
                var edge = false;
                for (var d = -2; d <= 2 && !edge; d++) edge = LayerAlpha(lx + d, ly) > 0f || LayerAlpha(lx, ly + d) > 0f;
                var m = mask[ly * w + lx];
                var nearer = m.r != 0 && m.r != PocOutlineTag.DecorationId && (m.b + m.a / 255f) / 255f <
                    Mathf.Lerp(c.Pixel.LastSpriteDepthBottom, c.Pixel.LastSpriteDepthTop, (y + 0.5f) / rect.height);
                if (!edge && !nearer) wrong++;
            }
            return (plain, blended, wrong);
        }

        private static void FadeChecks(Ctx c)
        {
            var s = c.Session;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Pixel.PixelsPerCell = 16;
            c.Pixel.OutlinePixels = 1;
            Check("see-through: on by default, fades trees to 0.4 over 0.25 s; only trees are marked in the catalog",
                c.Pixel.FadeEnabled && c.Pixel.FadeReady && Mathf.Approximately(c.Pixel.FadedAlpha, 0.4f) &&
                Mathf.Approximately(c.Pixel.FadeSeconds, 0.25f) && c.Catalog.Find("tree_basic").fadeInFront2D &&
                !c.Catalog.Find("flower_basic").fadeInFront2D && !c.Catalog.Find("stone_basic").fadeInFront2D);

            var tree = s.World.Objects.First(o => o.DefinitionId == "tree_basic" && s.World.CanEnter(o.RootCell + Vector2Int.up));
            var view = c.Presenter.Views.First(v => v.InstanceId == tree.InstanceId);
            Place2D(c, tree.RootCell + Vector2Int.up, FacingDirection.Down);
            var start = c.Pixel.FadeOf(view);
            Frame(c, 0.1f, Vector2Int.zero);
            var mid = c.Pixel.FadeOf(view);
            for (var i = 0; i < 6; i++) Frame(c, 0.05f, Vector2Int.zero);
            var faded = c.Pixel.FadeOf(view);
            Check("see-through: the tree in front of the player fades smoothly (1 -> 0.76 after 0.1 s -> 0.4)",
                Mathf.Approximately(start, 1f) && Mathf.Abs(mid - 0.76f) < 0.01f && Mathf.Abs(faded - 0.4f) < 1e-4f,
                $"{start:F2} -> {mid:F2} -> {faded:F2}");
            Check("see-through: the faded tree keeps its ground shadow, leaves the world image and the mask",
                view.Renderers.All(r => r.gameObject.layer == PocPixelView2D.FadeLayerIndex &&
                    r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
            var result = CompareFaded(c);
            Check("see-through: the sprite shows through (screen = sprite blended with the tree at 0.4), nothing else changed",
                result.blended > 300 && result.wrong == 0, $"plain {result.plain}, blended {result.blended}, wrong {result.wrong}");
            c.Pixel.FadeEnabled = false;
            Frame(c, 0f, Vector2Int.zero);
            var covered = CompareSprite(c);
            c.Pixel.FadeEnabled = true;
            Frame(c, 0f, Vector2Int.zero);
            Check("see-through: the sprite is visible where see-through off hides it",
                result.plain + result.blended > 3 * covered.matched, $"{result.plain + result.blended} vs {covered.matched}");

            // Walk away: the tree comes back and is restored exactly.
            var open = OpenArea(s.World, 2);
            Place2D(c, open, FacingDirection.Down);
            for (var i = 0; i < 8; i++) Frame(c, 0.05f, Vector2Int.zero);
            Check("see-through: when the player leaves, the tree returns to opaque, its normal layer and shadow",
                Mathf.Approximately(c.Pixel.FadeOf(view), 1f) &&
                view.Renderers.All(r => r.gameObject.layer == 0 && !r.forceRenderingOff &&
                    r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On),
                $"fading {c.Pixel.FadingCount}, alpha {c.Pixel.FadeOf(view):F2}, " +
                string.Join(",", view.Renderers.Select(r => $"{r.name}:{r.gameObject.layer}/{r.shadowCastingMode}/{r.forceRenderingOff}")));

            // A tree North of the player is behind the sprite: never faded.
            var north = s.World.Objects.First(o => o.DefinitionId == "tree_basic" && s.World.CanEnter(o.RootCell + Vector2Int.down));
            var northView = c.Presenter.Views.First(v => v.InstanceId == north.InstanceId);
            Place2D(c, north.RootCell + Vector2Int.down, FacingDirection.Up);
            for (var i = 0; i < 8; i++) Frame(c, 0.05f, Vector2Int.zero);
            Check("see-through: a tree North of the player (already behind the sprite) does not fade",
                Mathf.Approximately(c.Pixel.FadeOf(northView), 1f));

            // Switching to 3D restores every tree.
            Place2D(c, tree.RootCell + Vector2Int.up, FacingDirection.Down);
            for (var i = 0; i < 8; i++) Frame(c, 0.05f, Vector2Int.zero);
            var wasFading = c.Pixel.FadingCount > 0;
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("see-through: 2D only; switching to 3D restores every tree",
                wasFading && c.Pixel.FadingCount == 0 &&
                c.Presenter.Views.All(v => v.Renderers.All(r => r == null || r.gameObject.layer == 0)),
                $"was {wasFading}, fading {c.Pixel.FadingCount}, enabled {c.Pixel.enabled}, off-layer " +
                string.Join(",", c.Presenter.Views.SelectMany(v => v.Renderers).Where(r => r != null && r.gameObject.layer != 0).Select(r => r.name + ":" + r.gameObject.layer)));
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Release(c, 0.1f);
        }

        // ---------------------------------------------------------------- model swap

        private static void SwapChecks(Ctx c)
        {
            var s = c.Session;
            var original = c.Catalog.Player;
            var state = (s.Cell, s.Facing, s.CompletedSteps, s.World.MapId);
            try
            {
                c.Catalog.ConfigurePlayer(new PocActorPresentation
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.PlayerModelPath),
                    placeholder = true, armLeft = "", armRight = "", legLeft = "", legRight = "",
                    sprites = original.sprites
                });
                c.Actor.BuildModel();
                Frame(c, 1f / 60f, Vector2Int.zero);
                Check("model swap: another 3D model is bound from the catalog alone; 2D sprites and Grid state untouched",
                    c.Actor.ModelInstance.name == "poc_player" && !c.Actor.HasLimbs && c.Actor.HasSprites &&
                    state == (s.Cell, s.Facing, s.CompletedSteps, s.World.MapId));
            }
            finally
            {
                c.Catalog.ConfigurePlayer(original);
                c.Actor.BuildModel();
            }
        }
    }
}
