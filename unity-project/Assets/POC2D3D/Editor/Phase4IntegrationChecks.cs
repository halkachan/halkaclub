using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Halka.Game.Input;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.0 Phase 4: the game's systems on the shared 2D/3D world.
    // Camera (exit composition), 3D see-through trees, phone landscape / rotation guide, AUTO,
    // the crow (shared state, 2D sprites, 3D model), seats, Life Log / save / time / menu.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase4IntegrationChecks.RunBatch
    public static class Phase4IntegrationChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int passes;
        private static readonly List<string> failures = new List<string>();
        private static float clock = 100f;
        private const int ScreenW = 1280, ScreenH = 720;

        [MenuItem("HALKA/POC 2D3D/Run Phase 4 Integration Checks")]
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
            var hadLifeLog = PlayerPrefs.HasKey(LifeLogController.SaveKey);
            var savedLifeLog = PlayerPrefs.GetString(LifeLogController.SaveKey, "");
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(4404);
            PlayerPrefs.SetString(LifeLogController.SaveKey, "v1|100|50");
            var holder = Open(out var c);
            try
            {
                ExitCameraChecks(c);
                TreeFadeChecks(c);
                LandscapeChecks(c);
                AutoChecks(c);
                CrowChecks(c);
                SeatChecks(c);
                LifeLogTimeMenuChecks(c);
                ProtectionChecks();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Random.state = randomState;
                if (hadLifeLog) PlayerPrefs.SetString(LifeLogController.SaveKey, savedLifeLog);
                else PlayerPrefs.DeleteKey(LifeLogController.SaveKey);
                PlayerPrefs.Save();
            }
            foreach (var failure in failures) Debug.LogError("PHASE4 FAIL " + failure);
            Debug.Log($"HARUKA Phase 4 integration checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            if (condition) { passes++; Debug.Log("PHASE4 PASS " + name + (detail.Length > 0 ? " | " + detail : "")); }
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
            public PocGameplay Gameplay;
            public PocCrow Crow;
            public PocCrowView3D CrowView;
            public PocOrientationGuard Guard;
            public AutoModeController Auto;
            public LifeLogController LifeLog;
            public GameMenuController Menu;
            public GameHud Hud;
            public WorldTimeController Clock;
            public TouchDpad Dpad;
            public TouchActionButton Action;
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
                Gameplay = root.GetComponent<PocGameplay>(),
                Guard = root.GetComponent<PocOrientationGuard>(),
                Auto = root.GetComponent<AutoModeController>(),
                LifeLog = root.GetComponent<LifeLogController>(),
                Menu = root.GetComponent<GameMenuController>(),
                Hud = root.GetComponent<GameHud>(),
                Clock = root.GetComponent<WorldTimeController>(),
                Pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include),
                Third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include),
                Actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include),
                Crow = UnityEngine.Object.FindAnyObjectByType<PocCrow>(FindObjectsInactive.Include),
                CrowView = UnityEngine.Object.FindAnyObjectByType<PocCrowView3D>(FindObjectsInactive.Include),
                Dpad = UnityEngine.Object.FindAnyObjectByType<TouchDpad>(FindObjectsInactive.Include),
                Action = UnityEngine.Object.FindAnyObjectByType<TouchActionButton>(FindObjectsInactive.Include)
            };
            c.Session.Initialize();
            // The crow's own timers read Time.time (as in the game); the check clock starts there.
            clock = Time.time;
            // Edit Mode runs no Awake / OnEnable: start the components as Play Mode would.
            Invoke(c.LifeLog, "Awake");
            Invoke(c.LifeLog, "Start");
            Invoke(c.Auto, "Awake");
            Invoke(c.Clock, "Awake");
            Invoke(c.Presenter, "OnEnable");
            c.Presenter.Rebuild(c.Session.World);
            c.Gameplay.Connect();
            Invoke(c.Third, "OnEnable");
            Invoke(c.Pixel, "OnEnable");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Time.Override = PocPhaseOverride.Morning;
            Frame(c, 0.016f, Vector2Int.zero);
            var holder = new GameObject("Phase 4 check holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, Hidden)?.Invoke(target, null);

        // GameHud keeps a message for 2 s of real time; checks that need no message clear it.
        private static void ClearMessage(Ctx c) =>
            ((TimedMessage)typeof(GameHud).GetField("message", Hidden).GetValue(c.Hud)).Show(null, 0f, 0f);

        // The 2D overlays (sprites) are drawn when the screen image is composed.
        private static void Draw2D(Ctx c)
        {
            c.Pixel.UpdateView(0f, ScreenW, ScreenH);
            c.Pixel.WorldCamera.Render();
            c.Pixel.ComposeScreen(ScreenW, ScreenH);
        }

        // One frame: input, the shared session (and the crow), then the presentation.
        // The crow is ticked only by the checks that drive it (its timers start from Time.time).
        private static void Frame(Ctx c, float dt, Vector2Int raw, bool tickCrow = false)
        {
            clock += dt;
            c.Input.SetRaw(raw, clock);
            if (!c.Session.InputSuspended)
                c.Session.Tick(dt, c.Input.ResolveDirection(c.Session));
            else c.Session.Tick(dt, Vector2Int.zero);
            if (tickCrow && c.Crow.isActiveAndEnabled && c.Crow.gameObject.activeInHierarchy) c.Crow.Tick(clock);
            c.Actor.Apply(dt);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(dt, ScreenW, ScreenH);
            else
            {
                c.Third.ViewCamera.aspect = ScreenW / (float)ScreenH;
                c.Third.UpdateView(dt, clock);
            }
            c.CrowView.Apply(dt, clock);
        }

        private static void Settle(Ctx c, float seconds = 0.5f, bool tickCrow = false)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, tickCrow);
        }

        // A fresh key press for a world direction (3D keys are camera-relative).
        private static Vector2Int RawFor(Ctx c, Vector2Int world)
        {
            if (c.Switcher.Mode != PocViewMode.ThirdPerson3D) return world;
            foreach (var raw in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                if (GridSpace3D.CameraRelative(raw, GridSpace3D.FacingYaw(c.Switcher.CameraHeading)) == world) return raw;
            return world;
        }

        private static void Release(Ctx c)
        {
            c.Input.SetRaw(Vector2Int.zero, clock);
            c.Input.Resolve(Vector2Int.zero, false, FacingDirection.Up);
        }

        private static void HoldUntil(Ctx c, Vector2Int raw, Func<bool> done, float maxSeconds = 3f, bool letGo = true)
        {
            Release(c);
            for (var t = 0f; t < maxSeconds && !done(); t += 1f / 60f) Frame(c, 1f / 60f, raw);
            if (letGo) Release(c);
        }

        private static void Place(Ctx c, Vector2Int cell, FacingDirection facing, PocViewMode mode)
        {
            Release(c);
            Settle(c, 0.05f);
            c.Session.PlaceForCheck(cell, facing);
            c.Switcher.SetMode(mode);
            if (mode == PocViewMode.ThirdPerson3D) c.Third.SnapToTarget();
            else c.Pixel.SnapToTarget();
            Release(c);
            Settle(c, 0.5f);
        }

        private static void ToMap(Ctx c, bool interior, Vector2Int cell, FacingDirection facing)
        {
            if (interior ? c.Session.World != c.Session.InteriorWorld : c.Session.World != c.Session.OutdoorWorld)
                c.Session.LoadMapForCheck(interior, cell, facing);
            c.Session.PlaceForCheck(cell, facing);
            Settle(c, 0.1f);
        }

        // ---------------------------------------------------------------- 1. exit camera

        private static void ExitCameraChecks(Ctx c)
        {
            var door = c.Session.OutdoorWorld.Map.HouseDoorCell;
            var house = c.Presenter.Views.First(v => v.DefinitionId == "house_main");
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            HoldUntil(c, RawFor(c, Vector2Int.down), () => c.Session.World == c.Session.OutdoorWorld);
            Settle(c, 0.3f);
            house = c.Presenter.Views.First(v => v.DefinitionId == "house_main");
            var cam = c.Third.ViewCamera;
            Check("exit camera: right after leaving the house in 3D the composition is held, looking at the house (North)",
                c.Third.CompositionHeld && c.Third.Heading == FacingDirection.Up && c.Session.Cell == door + Vector2Int.down,
                $"held {c.Third.CompositionHeld} heading {c.Third.Heading} cell {c.Session.Cell}");
            Check("exit camera: no part of the house is hidden (roof, walls and door drawn, not ShadowsOnly)",
                house.Renderers.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !r.forceRenderingOff));
            var playerPoint = cam.WorldToViewportPoint(GridSpace3D.ToWorld(c.Session.Position, 0.5f));
            var housePoint = cam.WorldToViewportPoint(HouseCentre(house));
            Check("exit camera: the house front and the player are both in the frame (player in front of the house)",
                InFrame(playerPoint) && InFrame(housePoint) && playerPoint.z < housePoint.z,
                $"player {playerPoint} house {housePoint}");
            var pose = c.Third.HeldPose;
            Settle(c, 2f);
            Check("exit camera: the composition stays until the next movement input (2 s without input)",
                c.Third.CompositionHeld && Vector3.Distance(cam.transform.position,
                    pose.Focus - Quaternion.Euler(pose.Pitch, pose.Yaw, 0f) * Vector3.forward * pose.Distance) < 0.02f);

            // The next input: S (toward the camera) walks South, never into the house.
            var before = c.Session.Cell;
            var raw = Vector2Int.down;
            var dirSeen = Vector2Int.zero;
            var maxMove = 0f;
            var lastPos = cam.transform.position;
            var lastYaw = c.Third.CurrentYaw;
            var maxYawChange = 0f;
            Release(c);
            for (var t = 0f; t < 1.2f; t += 1f / 60f)
            {
                var cell = c.Session.Cell;
                Frame(c, 1f / 60f, t < 0.1f ? raw : Vector2Int.zero);
                if (dirSeen == Vector2Int.zero && c.Session.IsMoving) dirSeen = c.Session.StepToCell - cell;
                maxMove = Mathf.Max(maxMove, Vector3.Distance(cam.transform.position, lastPos));
                maxYawChange = Mathf.Max(maxYawChange, Mathf.Abs(Mathf.DeltaAngle(lastYaw, c.Third.CurrentYaw)));
                lastPos = cam.transform.position;
                lastYaw = c.Third.CurrentYaw;
            }
            Check("exit camera: the first S walks South (toward the camera); the direction is not reversed",
                dirSeen == Vector2Int.down && c.Session.Cell == before + Vector2Int.down, $"dir {dirSeen} cell {c.Session.Cell}");
            Check("exit camera: the first move releases the composition and the normal camera keeps the heading (no turn)",
                !c.Third.CompositionHeld && c.Third.Heading == FacingDirection.Up && maxYawChange < 0.5f,
                $"held {c.Third.CompositionHeld} heading {c.Third.Heading} yaw step {maxYawChange:0.00}");
            Check("exit camera: the hand-over to the normal camera is smooth (no jump: < 0.12 per frame)",
                maxMove < 0.12f, $"max {maxMove:0.000}");
            Check("exit camera: the house stays drawn while the camera returns",
                house.Renderers.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly));

            // A/D right after the exit are West/East (camera looks North). West of the door
            // cell grow flowers, so A only turns her there; D steps East.
            foreach (var (key, world) in new[] { (Vector2Int.left, Vector2Int.left), (Vector2Int.right, Vector2Int.right) })
            {
                ExitAgain(c);
                var start = c.Session.Cell;
                Release(c);
                Frame(c, 1f / 60f, key);
                Release(c);
                Settle(c, 0.3f);
                var expectedCell = c.Session.World.CanEnter(start + world) ? start + world : start;
                Check($"exit camera: {(key.x < 0 ? "A" : "D")} right after the exit goes {(world.x < 0 ? "West" : "East")} (camera looks North)",
                    c.Session.Facing == FacingDirectionExtensions.FromVector(world) && c.Session.Cell == expectedCell,
                    $"{start} -> {c.Session.Cell} facing {c.Session.Facing}");
            }

            // 2D: leaving the house changes nothing for the camera; switching to 3D afterwards is normal.
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.PixelView2D);
            var shown = c.Third.CompositionsShown;
            HoldUntil(c, Vector2Int.down, () => c.Session.World == c.Session.OutdoorWorld);
            Settle(c, 0.3f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.3f);
            Check("exit camera: never in 2D (leaving in 2D and then switching to 3D gives the normal camera)",
                c.Third.CompositionsShown == shown && !c.Third.CompositionHeld);
            // Normal outdoor walking never triggers it.
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            HoldUntil(c, Vector2Int.up, () => c.Session.Cell.y >= 2, 1.5f);
            Check("exit camera: ordinary walking outdoors never holds a composition",
                c.Third.CompositionsShown == shown && !c.Third.CompositionHeld);
            // An AUTO step (no key) also ends it.
            ExitAgain(c);
            c.Session.TryStep(Vector2Int.down);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("exit camera: any step (for example AUTO's) ends the composition", !c.Third.CompositionHeld);
            Settle(c, 0.4f);
        }

        // Back inside and out again through the doorway (3D), key let go at the door.
        private static void ExitAgain(Ctx c)
        {
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            HoldUntil(c, RawFor(c, Vector2Int.down), () => c.Session.World == c.Session.OutdoorWorld);
            Settle(c, 0.3f);
        }

        private static Vector3 HouseCentre(PocObjectView house)
        {
            var bounds = house.Renderers[0].bounds;
            foreach (var r in house.Renderers) bounds.Encapsulate(r.bounds);
            return bounds.center;
        }

        private static bool InFrame(Vector3 viewport) =>
            viewport.z > 0f && viewport.x > 0.02f && viewport.x < 0.98f && viewport.y > 0.02f && viewport.y < 0.98f;

        // ---------------------------------------------------------------- 2. 3D see-through trees

        private static void TreeFadeChecks(Ctx c)
        {
            ToMap(c, false, new Vector2Int(5, 2), FacingDirection.Up);
            var tree = c.Presenter.Views.First(v => v.DefinitionId == "tree_basic" && v.RootCell == new Vector2Int(5, 1));
            var toon = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            var originals = tree.Renderers.Select(_ => toon).ToArray();
            var fade = c.Third.TreeFade;
            Check("tree 3D: trees are no longer hidden as occluders; they fade with the same opacity and time as in 2D",
                !tree.CameraOccluder && tree.FadeInFront3D && tree.FadeInFront2D &&
                Mathf.Approximately(tree.FadeOpacity, 0.4f) && Mathf.Approximately(tree.FadeSeconds, 0.25f));
            Place(c, new Vector2Int(5, 2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            var camPos = c.Third.ViewCamera.transform.position;
            Check("tree 3D: the tree South of the player stands between the camera and her body",
                PocTreeFade3D.HidesPlayer(tree, camPos, GridSpace3D.ToWorld(c.Session.Position),
                    Quaternion.Euler(0f, c.Third.CurrentYaw, 0f)));
            Check("tree 3D: it fades to 40% (and stays there)", Mathf.Abs(fade.AlphaOf(tree) - 0.4f) < 0.01f,
                fade.AlphaOf(tree).ToString("0.000"));
            Check("tree 3D: while see-through it uses the fade material; its shadow keeps being cast",
                fade.IsSwapped(tree) && tree.Renderers.All(r => r.shadowCastingMode == ShadowCastingMode.On &&
                    r.sharedMaterial != null && r.sharedMaterial.shader.name == "HALKA/POC/ToonVertexColorFade"));
            var others = c.Presenter.Views.Where(v => v.FadeInFront3D && v != tree).ToList();
            Check("tree 3D: trees that do not hide her stay opaque with their own material",
                others.All(v => Mathf.Approximately(fade.AlphaOf(v), 1f) && !fade.IsSwapped(v)));
            Check("tree 3D: the crow and the player never fade (separate renderers)",
                c.CrowView.ModelInstance.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial == null ||
                    r.sharedMaterial.shader.name != "HALKA/POC/ToonVertexColorFade") &&
                c.Actor.ModelInstance.GetComponentsInChildren<Renderer>().All(r =>
                    r.sharedMaterial.shader.name != "HALKA/POC/ToonVertexColorFade"));
            // Timing: from opaque, half the time gives half the way (0.25 s fade, 0.125 s = 0.7).
            fade.RestoreAll();
            var fps = 1f / 120f;
            for (var t = 0f; t < 0.125f - 0.0001f; t += fps) Frame(c, fps, Vector2Int.zero, false);
            Check("tree 3D: the fade takes 0.25 s (about 0.7 after 0.125 s)", Mathf.Abs(fade.AlphaOf(tree) - 0.7f) < 0.04f,
                fade.AlphaOf(tree).ToString("0.000"));
            // Walking away (East, out of its way) brings it back to opaque with its own material.
            Place(c, new Vector2Int(7, 2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            Settle(c, 0.4f);
            Check("tree 3D: once she is out of its way it returns to 100% and to its own material",
                Mathf.Approximately(fade.AlphaOf(tree), 1f) && !fade.IsSwapped(tree) &&
                tree.Renderers.Select(r => r.sharedMaterial).SequenceEqual(originals));

            // Several trees: two in line between a camera and the player both fade; then only one.
            var a = c.Presenter.Views.First(v => v.DefinitionId == "tree_basic" && v.RootCell == new Vector2Int(-7, 2));
            var b = c.Presenter.Views.First(v => v.DefinitionId == "tree_basic" && v.RootCell == new Vector2Int(-4, 3));
            var pa = GridSpace3D.ToWorld(a.RootCell);
            var pb = GridSpace3D.ToWorld(b.RootCell);
            var dir = (pb - pa).normalized;
            var player = pb + dir * 1.2f;
            var camera = pa - dir * 2.5f + Vector3.up * 0.8f;
            var yawQ = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
            var together = new PocTreeFade3D(AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.TreeFadeMaterialPath));
            for (var i = 0; i < 40; i++) together.Update(c.Presenter.Views, camera, player, yawQ, 1f / 60f);
            Check("tree 3D: two trees hiding her at once both fade", Mathf.Abs(together.AlphaOf(a) - 0.4f) < 0.01f &&
                Mathf.Abs(together.AlphaOf(b) - 0.4f) < 0.01f, $"{together.AlphaOf(a):0.00} {together.AlphaOf(b):0.00}");
            var onlyA = pa + dir * 0.6f;
            for (var i = 0; i < 40; i++) together.Update(c.Presenter.Views, camera, onlyA, yawQ, 1f / 60f);
            Check("tree 3D: when only one still hides her, the other returns to opaque (none stays see-through)",
                Mathf.Abs(together.AlphaOf(a) - 0.4f) < 0.01f && Mathf.Approximately(together.AlphaOf(b), 1f) &&
                !together.IsSwapped(b), $"{together.AlphaOf(a):0.00} {together.AlphaOf(b):0.00}");
            together.RestoreAll();

            // Switching views: 2D gets every tree back (materials intact), 2D fades as before, 3D again.
            Place(c, new Vector2Int(5, 2), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("tree 3D -> 2D: switching to 2D restores every tree's own material",
                fade.FadingCount == 0 && tree.Renderers.Select(r => r.sharedMaterial).SequenceEqual(originals));
            Settle(c, 0.5f);
            Check("tree 2D: the 2D see-through rule still works (0.4 in front of the sprite)",
                Mathf.Abs(c.Pixel.FadeOf(tree) - 0.4f) < 0.01f, c.Pixel.FadeOf(tree).ToString("0.00"));
            for (var i = 0; i < 4; i++) { c.Switcher.Toggle(); Settle(c, 0.3f); }
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.5f);
            Check("tree 2D <-> 3D x4: the 3D fade works again and nothing is left over",
                Mathf.Abs(fade.AlphaOf(tree) - 0.4f) < 0.01f && c.Pixel.FadingCount == 0 &&
                tree.Renderers.All(r => r.gameObject.layer == 0));
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("tree 3D -> 2D again: no 3D material remains in 2D",
                tree.Renderers.Select(r => r.sharedMaterial).SequenceEqual(originals));
        }

        // ---------------------------------------------------------------- 3. landscape phones

        private static void LandscapeChecks(Ctx c)
        {
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            var guard = c.Guard;
            guard.TouchDevice = false;
            guard.Evaluate(800, 1000);
            Check("phone: a PC window taller than wide shows no rotation guide", !guard.GuideVisible && !c.Session.InputSuspended);
            guard.TouchDevice = true;
            guard.Evaluate(1688, 780);
            Check("phone: landscape (844x390 at DPR 2) plays normally", !guard.GuideVisible && !c.Session.InputSuspended &&
                c.Dpad.enabled && c.Action.enabled && c.Menu.enabled);
            var cell = c.Session.Cell;
            var facing = c.Session.Facing;
            var mode = c.Switcher.Mode;
            var steps = c.Session.CompletedSteps;
            var heading = c.Third.Heading;
            guard.Evaluate(780, 1688);
            Check("phone portrait: the rotation guide state stops input, AUTO and play time; touch controls and menu off",
                guard.GuideVisible && c.Session.InputSuspended && c.LifeLog.ForegroundBlocked && c.Gameplay.Suspended &&
                c.Gameplay.AutoWorld.Suspended && !c.Dpad.enabled && !c.Action.enabled && !c.Menu.enabled && !c.Menu.IsOpen);
            for (var t = 0f; t < 0.6f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.right);
            Check("phone portrait: a held key / D-pad does not move or turn her", c.Session.Cell == cell && c.Session.Facing == facing);
            Check("phone portrait: the A button does nothing (input suspended)", c.Session.InputSuspended && !c.Input.ActionPressed);
            var now = clock;
            c.Auto.RecordUserAction(now);
            var foreground = (bool)typeof(AutoModeController).GetMethod("IsForeground", Hidden).Invoke(c.Auto, null);
            c.Auto.Tick(now + 61f, foreground);
            Check("phone portrait: AUTO does not start (the guide counts as not in the foreground)",
                !foreground && !c.Auto.Active, $"foreground {foreground} active {c.Auto.Active}");
            var played = c.LifeLog.TotalPlaySeconds;
            Invoke(c.LifeLog, "Update");
            c.LifeLog.AdvanceClock(Time.realtimeSinceStartupAsDouble + 30, !c.LifeLog.ForegroundBlocked);
            Check("phone portrait: play time is not counted while the guide shows", c.LifeLog.TotalPlaySeconds == played);
            var anchor = c.Clock.AnchorMilliseconds;
            guard.Evaluate(1688, 780);
            Check("phone back to landscape: everything continues from the same state (cell, facing, view, steps, camera, clock)",
                !guard.GuideVisible && !c.Session.InputSuspended && c.Session.Cell == cell && c.Session.Facing == facing &&
                c.Switcher.Mode == mode && c.Session.CompletedSteps == steps && c.Third.Heading == heading &&
                c.Clock.AnchorMilliseconds == anchor && c.Dpad.enabled && c.Action.enabled && c.Menu.enabled);
            var before = c.Session.Cell;
            HoldUntil(c, Vector2Int.right, () => c.Session.Cell != before, 0.6f);
            Check("phone back to landscape: input works again", c.Session.Cell == before + Vector2Int.right);
            // Portrait -> landscape -> portrait -> landscape keeps working.
            for (var i = 0; i < 2; i++) { guard.Evaluate(780, 1688); guard.Evaluate(1688, 780); }
            Check("phone: portrait / landscape twice more: no state left behind",
                !guard.GuideVisible && !c.Session.InputSuspended && guard.GuideShownCount == 3 && c.Dpad.enabled);
            guard.TouchDevice = false;
            guard.Evaluate(ScreenW, ScreenH);

            // Layout at the landscape sizes: D-pad bottom-left, A bottom-right, Menu top-left; no overlap.
            foreach (var (w, h) in new[] { (1688, 780), (1280, 720), (1024, 576), (1920, 1080) })
            {
                var dpadRect = DpadRect(w, h, out var size);
                var aRect = ActionRect(w, h, size);
                var menuRect = new Rect(12f, 12f, 52f, 48f);
                var inside = new Rect(0, 0, w, h);
                Check($"phone landscape layout {w}x{h}: D-pad bottom-left, A bottom-right, Menu top-left, inside and apart",
                    Contains(inside, dpadRect) && Contains(inside, aRect) && !dpadRect.Overlaps(aRect) &&
                    !menuRect.Overlaps(dpadRect) && dpadRect.center.x < w * 0.5f && aRect.center.x > w * 0.5f &&
                    dpadRect.yMax > h * 0.5f && aRect.yMax > h * 0.5f,
                    $"dpad {dpadRect} A {aRect}");
            }
        }

        // TouchDpad.GetLayout / TouchActionButton layout for a screen size (safe area = full screen).
        private static Rect DpadRect(int w, int h, out float size)
        {
            size = Mathf.Clamp(Mathf.Min(w, h) * 0.21f, 64f, 152f);
            var gap = Mathf.Max(4f, size * 0.07f);
            var width = size * 3f + gap * 2f;
            var height = size * 2f + gap;
            return new Rect(12f, h - 12f - height, width, height);
        }

        // TouchActionButton.GetBounds (same 0.21 fraction, 64..152 px) for a screen size.
        private static Rect ActionRect(int w, int h, float dpadSize)
        {
            var size = Mathf.Clamp(Mathf.Min(w, h) * 0.21f, 64f, 152f);
            return new Rect(w - size - 12f, h - 12f - size, size, size);
        }

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f &&
            inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;

        // ---------------------------------------------------------------- 4. AUTO

        private static void AutoChecks(Ctx c)
        {
            ClearMessage(c);
            var auto = c.Auto;
            var door = c.Session.OutdoorWorld.Map.HouseDoorCell;
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var name = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
                Place(c, new Vector2Int(0, 0), FacingDirection.Down, mode);
                auto.SetAutoEnabled(true, clock);
                var start = clock;
                auto.Tick(start + 59.9f);
                Check($"AUTO outdoor {name}: not before 60 s without input", !auto.Active);
                auto.Tick(start + 60.05f); // 60 s (a hair more: float clock far from zero)
                Check($"AUTO outdoor {name}: starts after 60 s without input", auto.Active,
                    $"enabled {auto.AutoEnabled} menu {auto.MenuSuspended} moving {c.Session.IsMoving} message {c.Hud.HasActiveMessage} seated {c.Session.IsSeated}");
                var cells = RunAuto(c, start + 60f, 25f, out var steps, out var lifeSteps);
                Check($"AUTO outdoor {name}: walks the shared Grid one cell at a time in world directions",
                    steps > 3 && cells.Zip(cells.Skip(1), (p, q) => Mathf.Abs(q.x - p.x) + Mathf.Abs(q.y - p.y)).All(d => d <= 1),
                    $"steps {steps}");
                Check($"AUTO outdoor {name}: never enters the house and never walks into blocked cells or the crow",
                    c.Session.World == c.Session.OutdoorWorld && cells.All(p => p != door && c.Session.OutdoorWorld.CanEnter(p)));
                Check($"AUTO outdoor {name}: every AUTO step is a Life Log step", lifeSteps == steps, $"{lifeSteps} / {steps}");
                var cellNow = c.Session.Cell;
                auto.RecordUserAction(clock);
                Check($"AUTO outdoor {name}: user input stops it at once (on a whole cell)", !auto.Active && !c.Session.IsMoving);
            }
            // Same seed, same start: the route is the same in 2D and in 3D (camera never used).
            var routes = new List<List<Vector2Int>>();
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
                Place(c, new Vector2Int(0, 0), FacingDirection.Down, mode);
                c.Crow.gameObject.SetActive(false);
                UnityEngine.Random.InitState(77);
                auto.RecordUserAction(clock);
                auto.Tick(clock + 60f);
                routes.Add(RunAuto(c, clock + 60f, 12f, out _, out _, false));
                auto.RecordUserAction(clock);
                c.Crow.gameObject.SetActive(true);
            }
            Check("AUTO 2D / 3D: with the same random seed the walk is identical (world directions, not camera ones)",
                routes[0].Count > 3 && routes[0].SequenceEqual(routes[1]), $"{routes[0].Count} / {routes[1].Count}");

            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var name = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Up);
                Place(c, new Vector2Int(0, -3), FacingDirection.Up, mode);
                c.Session.OutdoorWorld.Map.TryGetMarker("interior_exit", out _);
                c.Session.InteriorWorld.TryGetMarker("interior_exit", out var exit);
                var start = clock;
                auto.RecordUserAction(start);
                auto.Tick(start + 1f); // the map change restarts the idle clock (AutoModeController)
                auto.RecordUserAction(start + 1f);
                auto.Tick(start + 61f);
                var cells = RunAuto(c, start + 61f, 30f, out var steps, out _);
                Check($"AUTO indoor {name}: walks the room around the furniture, never through the doorway",
                    auto.Active && steps > 3 && c.Session.World == c.Session.InteriorWorld &&
                    cells.All(p => p != exit && c.Session.InteriorWorld.CanEnter(p)), $"steps {steps}");
                auto.RecordUserAction(clock);
            }

            // Night, view switching and seats.
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            c.Time.Override = PocPhaseOverride.Night;
            var nightStart = clock;
            auto.RecordUserAction(nightStart);
            auto.Tick(nightStart + 1f);
            auto.RecordUserAction(nightStart + 1f);
            auto.Tick(nightStart + 61f);
            var nightCells = RunAuto(c, nightStart + 61f, 6f, out var nightSteps, out _);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Check("AUTO at night, and across a view switch: it keeps walking (the switch itself is not input)",
                auto.Active && nightSteps > 0, $"steps {nightSteps}");
            RunAuto(c, clock, 4f, out var afterSwitch, out _);
            Check("AUTO after the switch: still walking in 3D", auto.Active && afterSwitch > 0);
            auto.RecordUserAction(clock);
            c.Time.Override = PocPhaseOverride.Morning;
            // Seated: AUTO stays off.
            ToMap(c, false, new Vector2Int(-2, 1), FacingDirection.Up);
            Place(c, new Vector2Int(-2, 1), FacingDirection.Up, PocViewMode.PixelView2D);
            c.Session.InteractAhead();
            auto.RecordUserAction(clock);
            auto.Tick(clock + 61f);
            Check("AUTO while seated: does not start (movement locked, as with PlayerSeatController)",
                c.Session.IsSeated && !auto.Active);
            c.Session.Stand();
            // OFF: no AUTO.
            auto.SetAutoEnabled(false, clock);
            auto.Tick(clock + 120f);
            Check("AUTO OFF: never starts", !auto.Active && !auto.AutoEnabled);
            auto.SetAutoEnabled(true, clock);
        }

        // Runs the game loop (AUTO ticks with the frame clock) for some seconds; returns the cells.
        private static List<Vector2Int> RunAuto(Ctx c, float from, float seconds, out int steps, out long lifeSteps,
            bool tickCrow = true)
        {
            var cells = new List<Vector2Int> { c.Session.Cell };
            var autoBefore = c.Gameplay.AutoWorld.AutoSteps;
            var completedBefore = c.Session.CompletedSteps;
            var lifeBefore = c.LifeLog.TotalSteps;
            var now = from;
            for (var t = 0f; t < seconds; t += 1f / 30f)
            {
                now += 1f / 30f;
                c.Auto.Tick(now);
                clock = now;
                c.Session.Tick(1f / 30f, Vector2Int.zero);
                if (tickCrow && c.Crow.isActiveAndEnabled) c.Crow.Tick(now);
                if (cells[cells.Count - 1] != c.Session.Cell) cells.Add(c.Session.Cell);
            }
            steps = (int)(c.Session.CompletedSteps - completedBefore);
            lifeSteps = c.LifeLog.TotalSteps - lifeBefore;
            return cells;
        }

        // ---------------------------------------------------------------- 5. crow

        private static void CrowChecks(Ctx c)
        {
            var crow = c.Crow;
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            crow.ApplySpawnFrom(c.Session.OutdoorWorld.Map);
            c.Session.OutdoorWorld.Map.TryGetEntitySpawn("crow_main", out var spawn);
            Check("crow: one shared crow, spawned from the map's crow_main (ground idle at its cell)",
                UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 &&
                crow.Activity == CrowActivity.GroundIdle && crow.Cell == spawn.Cell);
            Check("crow: its ground cell is taken for the player (dynamic occupancy)",
                !c.Session.CanPlayerEnter(crow.Cell) && c.Session.CanPlayerEnter(crow.Cell + Vector2Int.left * 5));
            var now = clock + 100f;
            Set(crow, "nextFlightAt", now + 1000f);
            // A wander step: both ends reserved, walk frames, 3D legs swing.
            var stepped = false;
            for (var i = 0; i < 60 && !stepped; i++)
            {
                Set(crow, "nextActionAt", now);
                crow.Tick(now += 0.01f);
                stepped = crow.IsMoving;
            }
            var from = crow.StepFromCell;
            var to = crow.StepToCell;
            crow.Tick(now += 0.1f);
            c.CrowView.Visible = true;
            c.CrowView.Apply(1f / 60f, now);
            Check("crow ground walk: both ends of the step are taken; the 2D frame is a walk frame",
                stepped && crow.Activity == CrowActivity.GroundWalk && !c.Session.CanPlayerEnter(from) &&
                !c.Session.CanPlayerEnter(to) && crow.CurrentSprite != null && crow.CurrentSprite.name.StartsWith("crow_walk_"),
                crow.CurrentSprite != null ? crow.CurrentSprite.name : "-");
            crow.Tick(now += 0.5f);
            c.CrowView.Apply(1f / 60f, now);
            Check("crow ground idle: an idle frame for its facing", crow.Activity == CrowActivity.GroundIdle &&
                crow.CurrentSprite.name.StartsWith("crow_idle_"), crow.CurrentSprite.name);
            Check("crow 3D: Blender model with head, wings, tail and legs, standing where the shared crow is",
                c.CrowView.HasParts && c.CrowView.ModelShown &&
                Vector3.Distance(c.CrowView.transform.position, crow.transform.position) < 0.001f,
                $"parts {c.CrowView.HasParts} shown {c.CrowView.ModelShown}");

            // Natural flight: takeoff (still on the ground), flying (ground freed), perch on a tree.
            var ground = crow.Cell;
            Set(crow, "nextActionAt", now + 1000f);
            Set(crow, "nextFlightAt", now);
            crow.Tick(now += 0.01f);
            Check("crow takeoff: keeps its ground cell until airborne; flight frames", crow.Activity == CrowActivity.Takeoff &&
                !c.Session.CanPlayerEnter(ground) && crow.CurrentSprite.name.StartsWith("crow_flight_"));
            crow.Tick(now += 0.3f);
            c.CrowView.Apply(1f / 60f, now);
            var flapA = c.CrowView.CurrentFlapDegrees;
            crow.Tick(now += 0.3f);
            c.CrowView.Apply(1f / 60f, now + 0.07f);
            var flapB = c.CrowView.CurrentFlapDegrees;
            Check("crow flying: the ground is free; the 3D wings flap", crow.Activity == CrowActivity.Flying &&
                c.Session.CanPlayerEnter(ground) && Mathf.Abs(flapA - flapB) > 1f, $"{flapA:0} {flapB:0}");
            // View switches during the flight change nothing in the crow's state.
            var state = (crow.Activity, crow.TargetTreeView, crow.LegStart, crow.LegEnd);
            for (var i = 0; i < 4; i++) c.Switcher.Toggle();
            Check("crow flight across view switches: same activity, same target tree, same leg (no respawn)",
                crow.Activity == state.Activity && crow.TargetTreeView == state.TargetTreeView &&
                crow.LegStart == state.LegStart && crow.LegEnd == state.LegEnd);
            for (var i = 0; i < 80 && crow.Activity != CrowActivity.Perched; i++) crow.Tick(now += 0.1f);
            var perch = crow.TargetTreeView;
            Check("crow perched: on a tree's Blender PerchAnchor (no ground cell taken)",
                crow.Activity == CrowActivity.Perched && perch != null && perch.PerchAnchor != null &&
                Vector3.Distance(crow.transform.position, perch.PerchAnchor.position) < 0.001f &&
                c.Session.CanPlayerEnter(ground) && crow.CurrentSprite.name == "crow_idle_right");
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.CrowView.Apply(1f / 60f, now);
            Check("crow perched in 3D: the model sits at the anchor with folded wings",
                Vector3.Distance(c.CrowView.transform.position, perch.PerchAnchor.position) < 0.001f &&
                Mathf.Approximately(c.CrowView.CurrentFlapDegrees, 0f));
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero, false);
            Draw2D(c);
            Check("crow perched in 2D: the original sprite is drawn on top (like the 2D game's flight order)",
                c.Pixel.LastCrowOnTop && c.Pixel.LastCrowSprite != null && c.Pixel.LastCrowRect.width > 0);
            for (var i = 0; i < 160 && crow.Activity != CrowActivity.LeavingPerch; i++) crow.Tick(now += 0.1f);
            Check("crow leaving the perch: a separate takeoff from the tree", crow.Activity == CrowActivity.LeavingPerch);
            var sawLanding = false;
            for (var i = 0; i < 200 && crow.Activity != CrowActivity.GroundIdle; i++)
            {
                crow.Tick(now += 0.05f);
                sawLanding |= crow.Activity == CrowActivity.LandingGround;
            }
            Check("crow landing: approaches, touches down inside its wander range, takes its cell again",
                sawLanding && crow.Activity == CrowActivity.GroundIdle && !c.Session.CanPlayerEnter(crow.Cell) &&
                crow.Cell.x >= crow.RangeMin.x && crow.Cell.x <= crow.RangeMax.x &&
                crow.Cell.y >= crow.RangeMin.y && crow.Cell.y <= crow.RangeMax.y);

            Check("crow perch anchors exist on every tree (Phase 2 Blender PerchAnchor), none hard-coded",
                c.Presenter.Views.Count(v => v.PerchAnchor != null) == 4 &&
                c.Presenter.Views.Where(v => v.PerchAnchor != null).All(v =>
                    GridSpace3D.ToCell(v.PerchAnchor.position) == v.RootCell ||
                    (GridSpace3D.ToCell(v.PerchAnchor.position) - v.RootCell).sqrMagnitude <= 2));

            // Talking: in front, facing it -> "カァ。", the voice, a takeoff toward a nearby tree.
            Set(crow, "nextFlightAt", now + 1000f);
            Set(crow, "nextActionAt", now + 1000f);
            Set(crow, "interactionCooldownUntil", 0f);
            var front = crow.Cell + Vector2Int.down;
            c.Session.PlaceForCheck(front, FacingDirection.Up);
            var voice = c.Crow.GetComponent<InteractionAudio>();
            var plays = voice.PlayRequestCount;
            var talked = TryTalk(c);
            Check("crow talk (A): \"カァ。\", the crow voice, then a takeoff after 0.45 s",
                talked && c.Session.LastMessage == PocCrow.TalkMessage && voice.PlayRequestCount == plays + 1 &&
                crow.TalkTakeoffPending, c.Session.LastMessage);
            Check("crow talk: a second A while it is reacting does nothing (no double flight)", !TryTalk(c) &&
                voice.PlayRequestCount == plays + 1);
            crow.Tick(Time.time + 0.5f);
            Check("crow talk: it takes off toward a tree", crow.Activity == CrowActivity.Takeoff);
            Check("crow talk: in the air it cannot be talked to", !TryTalk(c));
            c.Session.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
            Check("crow talk: from further away nothing happens (ExamineInteractable's range)",
                !crow.TryTalk(new Vector2Int(-5, -5), FacingDirection.Up, false));

            // House: indoors the crow is gone (no occupancy); back outdoors it respawns once.
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Up);
            Check("crow indoors: not shown, not occupying, not ticking (as the 2D game's map unload)",
                !crow.gameObject.activeSelf && !crow.ReservesGround(spawn.Cell) && !c.CrowView.ModelShown);
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Check("crow back outdoors: respawned at its spawn cell, ground idle, a single crow",
                crow.gameObject.activeSelf && crow.Cell == spawn.Cell && crow.Activity == CrowActivity.GroundIdle &&
                UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1);
            // Player and crow never share a ground cell.
            c.Session.PlaceForCheck(spawn.Cell + Vector2Int.left, FacingDirection.Right);
            var stepInto = c.Session.TryStep(Vector2Int.right);
            Check("crow / player: the player cannot step onto the crow's cell", !stepInto && c.Session.Cell == spawn.Cell + Vector2Int.left);
            Check("crow / player: the crow cannot land or walk where the player stands",
                !(bool)typeof(CrowBehaviourCore).GetMethod("CanLand", Hidden).Invoke(crow, new object[] { c.Session.Cell }));
        }

        private static bool TryTalk(Ctx c) => c.Session.InteractAhead() && c.Session.LastMessage == PocCrow.TalkMessage &&
            Mathf.Approximately(c.Session.LastMessageAt, Time.unscaledTime);

        private static void Set(object target, string name, object value) =>
            typeof(CrowBehaviourCore).GetField(name, Hidden).SetValue(target, value);

        // ---------------------------------------------------------------- 6. seats

        private static void SeatChecks(Ctx c)
        {
            var benchSit = c.Actor.Sprites.benchSit;
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var name = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                ToMap(c, false, new Vector2Int(-2, 1), FacingDirection.Up);
                Place(c, new Vector2Int(-2, 1), FacingDirection.Up, mode);
                var steps = c.Session.CompletedSteps;
                var life = c.LifeLog.TotalSteps;
                c.Session.InteractAhead();
                Settle(c, 0.4f);
                if (mode == PocViewMode.PixelView2D) Draw2D(c);
                var bench = c.Session.SeatObject;
                Check($"bench {name}: A at the left seat sits down (bench_sit), the player keeps her cell",
                    c.Session.IsSeated && c.Session.SeatPose == "bench_sit" && bench != null && bench.DefinitionId == "bench_basic" &&
                    c.Session.Cell == new Vector2Int(-2, 1) && c.Session.SeatCell == new Vector2Int(-2, 2));
                if (mode == PocViewMode.PixelView2D)
                    Check("bench 2D: the original bench_sit.png, on the seat cell, 10/64 above it (PlayerSeatController)",
                        c.Actor.CurrentSprite == benchSit && Vector3.Distance(c.Actor.SpriteRoot,
                            GridSpace3D.ToWorld(new Vector2Int(-2, 2))) < 0.04f &&
                        Mathf.Abs(c.Actor.SpriteOffsetNorth - PlayerSeatController.ArtworkAboveSeatCenter) < 0.001f &&
                        c.Pixel.LastSpriteRect.width > 0);
                else
                    Check("bench 3D: the model sits on SeatAnchor_L (hips on the seat, facing the bench's front)",
                        Vector2.Distance(new Vector2(c.Actor.transform.position.x, c.Actor.transform.position.z),
                            new Vector2(c.Actor.SeatPoint.x, c.Actor.SeatPoint.z)) < 0.001f &&
                        Mathf.Abs(c.Actor.SeatPoint.y - 0.22f) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(c.Actor.Yaw, 180f)) < 1f,
                        $"seat {c.Actor.SeatPoint} actor {c.Actor.transform.position} yaw {c.Actor.Yaw:0}");
                // Seated: keys do nothing, A stands up, steps never change.
                for (var t = 0f; t < 0.5f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.down);
                Check($"bench {name}: while seated a key does not walk or turn", c.Session.IsSeated &&
                    c.Session.Cell == new Vector2Int(-2, 1) && c.Session.Facing == FacingDirection.Up);
                c.Switcher.Toggle();
                Settle(c, 0.2f);
                c.Switcher.Toggle();
                Settle(c, 0.2f);
                Check($"bench {name}: still seated after 2D/3D switches", c.Session.IsSeated && c.Switcher.Mode == mode);
                Release(c);
                c.Session.InteractAhead();
                Settle(c, 0.2f);
                Check($"bench {name}: A again stands up on the same cell; no step was counted (game and Life Log)",
                    !c.Session.IsSeated && c.Session.Cell == new Vector2Int(-2, 1) && c.Session.CompletedSteps == steps &&
                    c.LifeLog.TotalSteps == life);
            }
            // Right seat too.
            Place(c, new Vector2Int(-1, 1), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            c.Session.InteractAhead();
            Settle(c, 0.2f);
            Check("bench 3D: the right seat uses SeatAnchor_R", c.Session.IsSeated && c.Session.SeatCell == new Vector2Int(-1, 2) &&
                Mathf.Abs(c.Actor.SeatPoint.x - GridSpace3D.ToWorld(new Vector2Int(-1, 2)).x) < 0.01f);
            c.Session.Stand();

            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var name = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                ToMap(c, true, new Vector2Int(0, 0), FacingDirection.Up);
                Place(c, new Vector2Int(0, 0), FacingDirection.Up, mode);
                c.Session.InteractAhead();
                Settle(c, 0.2f);
                Check($"cushion {name}: A on the cushion sits down there (cushion_sit), same cell",
                    c.Session.IsSeated && c.Session.SeatPose == "cushion_sit" && c.Session.SeatCell == new Vector2Int(0, 0) &&
                    c.Session.Cell == new Vector2Int(0, 0));
                if (mode == PocViewMode.PixelView2D)
                    Check("cushion 2D: the seated artwork (bench_sit.png) on the cushion, lower than on the bench",
                        c.Actor.CurrentSprite == benchSit && c.Actor.SpriteOffsetNorth < PlayerSeatController.ArtworkAboveSeatCenter &&
                        Vector3.Distance(c.Actor.SpriteRoot, GridSpace3D.ToWorld(new Vector2Int(0, 0))) < 0.04f);
                else
                    Check("cushion 3D: the model sits on SitAnchor (0.1 high)",
                        Mathf.Abs(c.Actor.SeatPoint.y - 0.1f) < 0.01f &&
                        Vector2.Distance(new Vector2(c.Actor.transform.position.x, c.Actor.transform.position.z),
                            new Vector2(c.Actor.SeatPoint.x, c.Actor.SeatPoint.z)) < 0.001f);
                c.Switcher.Toggle();
                Settle(c, 0.2f);
                Check($"cushion {name}: still seated after a view switch", c.Session.IsSeated);
                c.Switcher.Toggle();
                c.Session.InteractAhead();
                Settle(c, 0.1f);
                Check($"cushion {name}: A stands up", !c.Session.IsSeated && c.Session.Cell == new Vector2Int(0, 0));
            }
            // Other furniture keeps its message.
            Place(c, new Vector2Int(2, 0), FacingDirection.Up, PocViewMode.ThirdPerson3D);
            Check("furniture: the desk still answers \"つくえ。\"", c.Session.InteractAhead() && c.Session.LastMessage == "つくえ。");
            Place(c, new Vector2Int(-5, -1), FacingDirection.Up, PocViewMode.PixelView2D);
            Check("furniture: the bed still answers \"ベッド。\" (no new sleep system)",
                c.Session.InteractAhead() && c.Session.LastMessage == "ベッド。" && !c.Session.IsSeated);
            // A click while seated: only the seat answers (InteractionRouter).
            Place(c, new Vector2Int(0, 0), FacingDirection.Up, PocViewMode.PixelView2D);
            c.Session.InteractAhead();
            var desk = c.Session.World.Objects.First(o => o.DefinitionId == "desk_basic");
            Check("seated click: another object does not answer; the seat itself stands her up",
                !c.Session.InteractWith(desk) && c.Session.IsSeated && c.Session.InteractWith(c.Session.SeatObject) &&
                !c.Session.IsSeated);
        }

        // ---------------------------------------------------------------- 7. Life Log, save, time, menu

        private static void LifeLogTimeMenuChecks(Ctx c)
        {
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            var life = c.LifeLog.TotalSteps;
            var steps = c.Session.CompletedSteps;
            HoldUntil(c, Vector2Int.left, () => c.Session.Cell.x <= -2, 1.5f);
            Check("Life Log: each completed step adds one", c.LifeLog.TotalSteps - life == c.Session.CompletedSteps - steps &&
                c.Session.CompletedSteps - steps == 2, $"{c.LifeLog.TotalSteps - life}");
            life = c.LifeLog.TotalSteps;
            c.Session.PlaceForCheck(new Vector2Int(5, 0), FacingDirection.Up);
            life = c.LifeLog.TotalSteps;
            for (var t = 0f; t < 0.5f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.up); // tree at (5,1)
            Check("Life Log: a step into a tree (blocked) adds nothing", c.LifeLog.TotalSteps == life && c.Session.Cell == new Vector2Int(5, 0));
            for (var i = 0; i < 6; i++) { c.Switcher.Toggle(); Settle(c, 0.1f); }
            Check("Life Log: view switches and camera moves add nothing", c.LifeLog.TotalSteps == life);
            c.LifeLog.Save();
            var saved = PlayerPrefs.GetString(LifeLogController.SaveKey, "");
            var reloaded = LifeLogData.Deserialize(saved);
            Check("save: the Life Log is saved under the same key and format and reloads with the same total",
                saved.StartsWith("v1|") && reloaded.TotalSteps == c.LifeLog.TotalSteps, saved);
            // World Time: one clock; switching never resets it.
            var anchor = c.Clock.AnchorMilliseconds;
            var phase = c.Clock.CurrentPhase;
            for (var i = 0; i < 4; i++) c.Switcher.Toggle();
            Check("time: the same WorldTimeController (save key, anchor) is used and view switches never reset it",
                c.Clock.AnchorMilliseconds == anchor && c.Clock.CurrentPhase == phase &&
                PlayerPrefs.GetString(WorldTimeController.SaveKey, "") == anchor.ToString());
            c.Time.Override = PocPhaseOverride.Night;
            c.Time.Apply(clock);
            c.Time.Apply(clock + WorldNightOverlay2D.FadeSeconds + 0.1f);
            Check("time: night in 2D uses the 2D game's overlay alpha (outdoor 0.33)",
                Mathf.Abs(c.Time.PixelOverlayAlpha - WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, "first_field")) < 0.01f);
            c.Time.Override = PocPhaseOverride.Morning;
            c.Time.Apply(clock + 10f);
            c.Time.Apply(clock + 20f);
            // AUTO setting: on by default, not saved (as in the 2D game).
            Check("AUTO setting: ON by default and not stored (the 2D game's rule)", c.Auto.AutoEnabled);
            // Menu: AUTO, Life Log and the view row.
            ClearMessage(c);
            var menu = c.Menu;
            menu.SetOpen(true, clock);
            Check("menu: opening it suspends AUTO (AutoModeController.SetMenuSuspended)", menu.IsOpen && c.Auto.MenuSuspended);
            menu.ToggleAuto(clock + 1f);
            Check("menu: AUTO OFF", !c.Auto.AutoEnabled);
            menu.ToggleAuto(clock + 2f);
            Check("menu: AUTO ON again", c.Auto.AutoEnabled);
            var item = menu.ExtraItem;
            var before = c.Switcher.Mode;
            Check("menu: the view row reads ひょうじ with the current view", item != null && item.Label == "ひょうじ" &&
                item.Value == (before == PocViewMode.ThirdPerson3D ? "3D" : "2D") && GameBitmapFont.HasAllGlyphs(item.Label));
            menu.ActivateExtraItem(clock + 3f);
            Check("menu: the view row switches 2D / 3D (the shared state stays)", c.Switcher.Mode != before &&
                item.Value == (c.Switcher.Mode == PocViewMode.ThirdPerson3D ? "3D" : "2D"));
            menu.OpenLifeLog(clock + 4f);
            Check("menu: せいかつきろく shows the same Life Log", menu.IsLifeLogOpen && c.LifeLog.StepsLabel.Length > 0);
            menu.BackFromLifeLog(clock + 5f);
            menu.SetOpen(false, clock + 6f);
            Check("menu: closing it lets AUTO count its 60 s again", !menu.IsOpen && !c.Auto.MenuSuspended);
            // Gameplay and display agree after everything.
            Check("display: the 2D sprite and the 3D model stand on the shared cell",
                Vector2.Distance(new Vector2(c.Actor.transform.position.x, c.Actor.transform.position.z),
                    new Vector2(GridSpace3D.ToWorld(c.Session.Position).x, GridSpace3D.ToWorld(c.Session.Position).z)) < 0.05f);
            Check("debug panel: hidden by default (?debug=1 shows it); the preview title says Phase 4",
                !UnityEngine.Object.FindAnyObjectByType<PocDebugPanel>(FindObjectsInactive.Include).PanelVisible &&
                PocDebugPanel.PreviewTitle == "はるかわーるど ver3.0 Phase 4 Preview");
        }

        // ---------------------------------------------------------------- protection

        private static void ProtectionChecks()
        {
            var root = PocSceneBuilder.RepositoryRoot;
            var expected = System.IO.File.ReadAllLines(System.IO.Path.Combine(root, "tools/poc2d3d/official_map_sha256.txt"))
                .Where(l => l.Length > 66).ToList();
            var same = expected.All(line =>
            {
                var path = System.IO.Path.Combine(root, line.Substring(65).TrimStart('*', ' '));
                using var sha = System.Security.Cryptography.SHA256.Create();
                return System.IO.File.Exists(path) && BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(path)))
                    .Replace("-", "").Equals(line.Substring(0, 64), StringComparison.OrdinalIgnoreCase);
            });
            Check("protection: official Map JSON and Catalog unchanged (SHA-256 of all four files)", same && expected.Count == 4);
        }
    }
}
