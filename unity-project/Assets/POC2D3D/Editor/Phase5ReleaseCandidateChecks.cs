using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
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
    // はるかわーるど ver3.0 Phase 5: the Release Candidate. Startup (RC scene and page), footsteps,
    // the 2D grass and feet, seats, the crow, the camera (exit with a held key), phone page,
    // save / time, protected files. Phase 1-4 suites keep covering what they covered.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase5ReleaseCandidateChecks.RunBatch
    public static class Phase5ReleaseCandidateChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int passes;
        private static readonly List<string> failures = new List<string>();
        private static readonly Dictionary<string, int> perCategory = new Dictionary<string, int>();
        private static string category = "";
        private static float clock = 100f;
        private const int ScreenW = 1280, ScreenH = 720;

        [MenuItem("HALKA/POC 2D3D/Run Phase 5 Release Candidate Checks")]
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
            perCategory.Clear();
            var hadLifeLog = PlayerPrefs.HasKey(LifeLogController.SaveKey);
            var savedLifeLog = PlayerPrefs.GetString(LifeLogController.SaveKey, "");
            var hadTime = PlayerPrefs.HasKey(WorldTimeController.SaveKey);
            var savedTime = PlayerPrefs.GetString(WorldTimeController.SaveKey, "");
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(5505);
            try
            {
                StartupChecks();
                SaveTimeChecks();
                PlayerPrefs.SetString(LifeLogController.SaveKey, "v1|100|50");
                var holder = Open(out var c);
                try
                {
                    FootstepChecks(c);
                    Grass2DChecks(c);
                    SeatChecks(c);
                    RoomChecks(c);
                    Outline3DChecks(c);
                    LineBoilChecks(c);
                    MenuChecks(c);
                    CrowChecks(c);
                    CameraChecks(c);
                    MobileChecks(c);
                    SessionSaveChecks(c);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(holder);
                }
                ProtectionChecks();
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (hadLifeLog) PlayerPrefs.SetString(LifeLogController.SaveKey, savedLifeLog);
                else PlayerPrefs.DeleteKey(LifeLogController.SaveKey);
                if (hadTime) PlayerPrefs.SetString(WorldTimeController.SaveKey, savedTime);
                else PlayerPrefs.DeleteKey(WorldTimeController.SaveKey);
                PlayerPrefs.Save();
            }
            foreach (var failure in failures) Debug.LogError("PHASE5 FAIL " + failure);
            Debug.Log("HARUKA Phase 5 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA Phase 5 release candidate checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Category(string name) => category = name;

        private static void Check(string name, bool condition, string detail = "")
        {
            perCategory[category] = perCategory.TryGetValue(category, out var n) ? n + 1 : 1;
            if (condition) { passes++; Debug.Log("PHASE5 PASS [" + category + "] " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- startup (RC scene, page)

        private static void StartupChecks()
        {
            Category("startup");
            Check("RC scene: a separate scene file from the technical POC scene",
                PocSceneBuilder.RcScenePath != PocSceneBuilder.ScenePath && File.Exists(PocSceneBuilder.RcScenePath) &&
                File.Exists(PocSceneBuilder.ScenePath));
            EditorSceneManager.OpenScene(PocSceneBuilder.RcScenePath, OpenSceneMode.Single);
            var panel = UnityEngine.Object.FindAnyObjectByType<PocDebugPanel>(FindObjectsInactive.Include);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            var switcher = UnityEngine.Object.FindAnyObjectByType<PocViewSwitcher>(FindObjectsInactive.Include);
            var gameplay = UnityEngine.Object.FindAnyObjectByType<PocGameplay>(FindObjectsInactive.Include);
            var pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include);
            var actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include);
            var third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include);
            Check("RC scene: the debug panel is in release mode", panel != null && panel.ReleaseCandidate);
            var rcBoil = UnityEngine.Object.FindAnyObjectByType<PocLineBoil>(FindObjectsInactive.Include);
            Check("RC scene: the 3D outlines twitch a little (line boil \"lines\", 6 steps a second), switchable in ?debug=1",
                rcBoil != null && rcBoil.enabled && rcBoil.Mode == PocLineBoilMode.Lines &&
                Mathf.Approximately(rcBoil.StepsPerSecond, 6f) && panel != null && panel.LineBoil == rcBoil);
            Check("RC scene: starts in 2D (the game's first view)",
                switcher != null && (PocViewMode)typeof(PocViewSwitcher).GetField("initialMode", Hidden).GetValue(switcher) ==
                PocViewMode.PixelView2D);
            var rcMenu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>(FindObjectsInactive.Include);
            var rcGuard = UnityEngine.Object.FindAnyObjectByType<PocOrientationGuard>(FindObjectsInactive.Include);
            // Phase 6: the release candidate is RC2; the version shown follows its label.
            Check("RC scene: the version (" + PocDebugPanel.ReleaseCandidateVersionLabel + ") is in the menu, not at the top right; the 2D game's own label is unchanged",
                hud != null && !hud.VersionLabelShown && hud.VersionLabel == PocDebugPanel.ReleaseCandidateVersionLabel && rcMenu != null &&
                rcMenu.VersionText == PocDebugPanel.ReleaseCandidateVersionLabel && GameVersion.Label == "ver3.0 Preview");
            Check("RC scene: phones are played upright too (no landscape-only rule, no pause when upright)",
                rcGuard != null && !rcGuard.LandscapeOnly);
            // Awake reads ?debug=1 from the page URL; without it (Edit Mode: no URL) the panel is off.
            panel.enabled = true;
            typeof(PocDebugPanel).GetMethod("Awake", Hidden).Invoke(panel, null);
            Check("RC scene: without ?debug=1 the panel is hidden and disabled (no OnGUI, no profiler recorders)",
                !panel.PanelVisible && !panel.enabled);
            Check("RC scene: the name is fixed as はるかわーるど (page title exactly はるかわーるど); IDs keep HALKA",
                PocDebugPanel.ReleaseCandidateTitle == "はるかわーるど" &&
                PocDebugPanel.ReleaseCandidateName.StartsWith("はるかわーるど") &&
                panel.Title.StartsWith("はるかわーるど " + PocDebugPanel.ReleaseCandidateVersionLabel) && LifeLogController.SaveKey.StartsWith("HALKA_WORLD"));
            var footsteps = gameplay != null ? gameplay.Footsteps : null;
            var source = footsteps != null ? footsteps.GetComponent<AudioSource>() : null;
            var clip = footsteps != null ? (AudioClip)typeof(PlayerFootstepAudio).GetField("footstep", Hidden).GetValue(footsteps) : null;
            Check("RC scene: footsteps are the 2D game's PlayerFootstepAudio with footstep_one_step.wav (volume 0.45, 2D, pitch 1)",
                footsteps != null && source != null && clip != null &&
                AssetDatabase.GetAssetPath(clip) == PocSceneBuilder.FootstepPath && Mathf.Approximately(source.volume, 0.45f) &&
                source.spatialBlend == 0f && Mathf.Approximately(source.pitch, 1f) && !source.playOnAwake &&
                (bool)typeof(PlayerFootstepAudio).GetField("externallyDriven", Hidden).GetValue(footsteps));
            Check("RC scene: the 2D grass is the Phase 4 look by default; the 2D game's sprites stay an option",
                pixel != null && !pixel.OriginalGrass2D && pixel.GrassReady);
            Check("RC scene: seated artwork is bench_sit.png (bench and sofa); no provisional cushion artwork",
                actor != null && actor.Sprites != null && actor.Sprites.SeatSprite("bench_sit") == actor.Sprites.benchSit &&
                AssetDatabase.GetAssetPath(actor.Sprites.benchSit) == PocSceneBuilder.BenchSitPath &&
                !AssetDatabase.IsValidFolder("Assets/POC2D3D/Provisional"));
            Check("RC scene: the 3D camera defaults are kept (North-fixed, 3.4 outdoors, 3.6 indoors)",
                third != null && third.Follow == PocCameraFollow.NorthFixed &&
                Mathf.Approximately(third.OutdoorDistance, 3.4f) && Mathf.Approximately(third.IndoorDistance, 3.6f));
            Check("RC scene: the menu has the ひょうじ (2D / 3D) row; no new on-screen buttons",
                gameplay != null && gameplay.Menu != null &&
                UnityEngine.Object.FindObjectsByType<TouchActionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<TouchDpad>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1);
            EditorSceneManager.OpenScene(PocSceneBuilder.ScenePath, OpenSceneMode.Single);
            var pocPanel = UnityEngine.Object.FindAnyObjectByType<PocDebugPanel>(FindObjectsInactive.Include);
            Check("POC scene: stays the technical verification scene (not in release mode, Phase 4 landscape rule)",
                pocPanel != null && !pocPanel.ReleaseCandidate &&
                UnityEngine.Object.FindAnyObjectByType<PocOrientationGuard>(FindObjectsInactive.Include).LandscapeOnly);

            // The RC page: the template page plus the phone layer, with the RC title.
            var template = File.ReadAllText(Path.Combine(Application.dataPath, "WebGLTemplates", "MinimalNoBrand", "index.html"));
            var page = PocBuilder.PreviewPage(template, PocDebugPanel.ReleaseCandidateTitle, false);
            Check("RC page: title はるかわーるど, any orientation (no rotation guide), safe area, follows full screen (its own or its frame's)",
                page.Contains("<title>はるかわーるど</title>") && !page.Contains("スマホを横向きにしてください") &&
                !page.Contains("updateRotateGuide") && page.Contains("safe-area-probe") && page.Contains("fullscreenchange") &&
                page.Contains("window.innerWidth >= screen.width - 1"));
            Check("RC folder: the game sits at halkaworld/webgl like on halkaclub.com, outside the repository",
                PocBuilder.ReleaseCandidateGamePath == "halkaworld/webgl" &&
                !PocBuilder.ReleaseCandidatePath.StartsWith(PocSceneBuilder.RepositoryRoot, StringComparison.OrdinalIgnoreCase));
            var kit = Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d", "rc1");
            var staging = File.Exists(Path.Combine(kit, "staging", "halkaworld", "index.html"))
                ? File.ReadAllText(Path.Combine(kit, "staging", "halkaworld", "index.html")) : "";
            Check("staging page: the public page's layout with the RC1 game in webgl/, no analytics, not indexed",
                staging.Contains("src=\"webgl/?v=3.0-rc1&build=__BUILD__\"") && staging.Contains("allow=\"fullscreen\"") &&
                !staging.Contains("cloudflareinsights") && staging.Contains("noindex") && staging.Contains("unity-frame"));
            Check("staging page: the name is はるかわーるど everywhere it is shown (no HALKA WORLD left)",
                staging.Contains("<title>はるかわーるど | HALKA</title>") && staging.Contains("<h1 id=\"game-title\">はるかわーるど</h1>") &&
                !staging.Contains("HALKA WORLD") && !File.Exists(Path.Combine(kit, "staging", "halkaworld", "index-mobilefix.html")));
            Check("staging page: 全画面にする is a button on the page under the game frame (not in the game); full screen for the frame, or where a browser cannot (iPhone Safari) the frame fills the page with a もどす bar outside the game and the back gesture",
                staging.Contains("id=\"game-fullscreen-button\" hidden>全画面にする</button>") &&
                staging.IndexOf("game-fullscreen-button", StringComparison.Ordinal) > staging.IndexOf("<iframe", StringComparison.Ordinal) &&
                staging.Contains("frame.requestFullscreen || frame.webkitRequestFullscreen") && staging.Contains("document.fullscreenEnabled") &&
                staging.Contains("if (!real) { fill(true, false); return; }") && staging.Contains("id=\"game-fill-back\">もどす</button>") &&
                staging.Contains("body.game-filled .unity-frame { position: fixed;") && staging.Contains("halkaGameFilled") &&
                !File.Exists(Path.Combine(Application.dataPath, "POC2D3D", "Plugins", "WebGL", "HalkaFullscreen.jslib")));
        }

        // ---------------------------------------------------------------- save keys and old data

        private static void SaveTimeChecks()
        {
            Category("save/time");
            Check("save keys: unchanged (HALKA_WORLD_life_log_v1, HALKA_WORLD_time_cycle_anchor_v1)",
                LifeLogController.SaveKey == "HALKA_WORLD_life_log_v1" &&
                WorldTimeController.SaveKey == "HALKA_WORLD_time_cycle_anchor_v1");
            // ver2.x data (the same format since ver2.2) reads back unchanged.
            foreach (var old in new[] { "v1|0|0", "v1|12345|6789", "v1|9876543|123456" })
            {
                var data = LifeLogData.Deserialize(old);
                Check($"old Life Log \"{old}\": read as is and written back identically", data.Serialize() == old);
            }
            Check("old Life Log: an unknown format is not overwritten by reading (starts from zero in memory)",
                LifeLogData.Deserialize("v2|1|2").TotalSteps == 0 && LifeLogData.Deserialize("").TotalSteps == 0);
        }

        // ---------------------------------------------------------------- footsteps

        private static int Plays(Ctx c) => c.Gameplay.Footsteps.PlayCount;

        private static void FootstepChecks(Ctx c)
        {
            Category("footsteps");
            var order = new List<string>();
            Action<Vector2Int> started = cell => order.Add("start " + cell + " plays " + Plays(c));
            Action<Vector2Int> completed = cell => order.Add("done " + cell);
            c.Session.StepStarted += started;
            c.Session.StepCompleted += completed;
            try
            {
                foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                {
                    var name = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                    ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
                    Place(c, new Vector2Int(0, 0), FacingDirection.Left, mode);
                    var plays = Plays(c);
                    var startedSteps = c.Session.StartedSteps;
                    var done = c.Session.CompletedSteps;
                    order.Clear();
                    // The first frame of the step: the sound is there before the step completes.
                    Release(c);
                    Frame(c, 1f / 60f, RawFor(c, Vector2Int.left));
                    Check($"footsteps {name}: the sound starts with the step (StepStarted), before it completes",
                        c.Session.IsMoving && Plays(c) == plays + 1 && c.Session.CompletedSteps == done,
                        $"moving {c.Session.IsMoving} plays {Plays(c) - plays}");
                    HoldUntil(c, RawFor(c, Vector2Int.left), () => c.Session.Cell.x <= -2, 2f);
                    Settle(c, 0.3f);
                    // A held key starts the next step in the frame the last one completes.
                    var walked = c.Session.StartedSteps - startedSteps;
                    Check($"footsteps {name}: one footstep per step (sounds = steps started = steps completed)",
                        walked >= 2 && Plays(c) - plays == walked && c.Session.CompletedSteps - done == walked,
                        $"plays {Plays(c) - plays} started {walked} completed {c.Session.CompletedSteps - done}");
                    Check($"footsteps {name}: StepStarted and StepCompleted are separate events (start, done, start, done, ...)",
                        order.Count == walked * 2 && order.Select((e, i) => e.StartsWith(i % 2 == 0 ? "start" : "done")).All(x => x),
                        string.Join(" / ", order));
                    // Blocked: a tree north of (5,0).
                    Place(c, new Vector2Int(5, 0), FacingDirection.Up, mode);
                    plays = Plays(c);
                    HoldUntil(c, RawFor(c, Vector2Int.up), () => false, 0.6f);
                    Check($"footsteps {name}: a blocked step (tree) makes no sound", Plays(c) == plays && c.Session.Cell == new Vector2Int(5, 0));
                    // View switches and the camera.
                    plays = Plays(c);
                    for (var i = 0; i < 6; i++) { c.Switcher.Toggle(); Settle(c, 0.1f); }
                    c.Switcher.SetMode(mode);
                    Check($"footsteps {name}: view switches and camera moves make no sound", Plays(c) == plays);
                }
                // Seats: sitting, keys while seated, standing.
                ToMap(c, false, new Vector2Int(-2, 1), FacingDirection.Up);
                Place(c, new Vector2Int(-2, 1), FacingDirection.Up, PocViewMode.PixelView2D);
                var before = Plays(c);
                c.Session.InteractAhead();
                Settle(c, 0.2f);
                for (var t = 0f; t < 0.5f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.down);
                Release(c);
                c.Session.InteractAhead();
                Settle(c, 0.2f);
                Check("footsteps: sitting on the bench, keys while seated and standing up make no sound",
                    Plays(c) == before && !c.Session.IsSeated);
                // Map change: the step onto the door sounds once; the change itself adds nothing.
                var door = c.Session.OutdoorWorld.Map.HouseDoorCell;
                ToMap(c, false, door + Vector2Int.down, FacingDirection.Up);
                Place(c, door + Vector2Int.down, FacingDirection.Up, PocViewMode.PixelView2D);
                before = Plays(c);
                var startedBefore = c.Session.StartedSteps;
                HoldUntil(c, Vector2Int.up, () => c.Session.World == c.Session.InteriorWorld, 2f);
                Settle(c, 0.5f);
                Check("footsteps: entering the house: one sound for the step onto the door, none for the map change",
                    c.Session.World == c.Session.InteriorWorld && Plays(c) - before == c.Session.StartedSteps - startedBefore &&
                    Plays(c) - before == 1, $"plays {Plays(c) - before}");
                // AUTO: every AUTO step that starts sounds once.
                foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                {
                    var name = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                    ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
                    Place(c, new Vector2Int(0, 0), FacingDirection.Down, mode);
                    c.Auto.SetAutoEnabled(true, clock);
                    c.Auto.RecordUserAction(clock);
                    c.Auto.Tick(clock + 60.05f);
                    before = Plays(c);
                    startedBefore = c.Session.StartedSteps;
                    RunAuto(c, clock + 60f, 12f, out var steps, out _, false);
                    Check($"footsteps AUTO {name}: one sound per AUTO step (same component, same clip)",
                        c.Auto.Active && steps > 3 && Plays(c) - before == c.Session.StartedSteps - startedBefore,
                        $"plays {Plays(c) - before} started {c.Session.StartedSteps - startedBefore} completed {steps}");
                    c.Auto.RecordUserAction(clock);
                }
                // The crow's hops are not footsteps.
                ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
                Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
                before = Plays(c);
                var now = clock + 300f;
                for (var i = 0; i < 300; i++) c.Crow.Tick(now += 0.05f);
                Check("footsteps: the crow walking or flying never plays the player's footstep", Plays(c) == before);
            }
            finally
            {
                c.Session.StepStarted -= started;
                c.Session.StepCompleted -= completed;
            }
        }

        // ---------------------------------------------------------------- 2D grass and feet

        private static List<Vector2Int> ExpectedGrass(MapDefinition map)
        {
            var cells = new List<Vector2Int>();
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
                if (MapPlacementRules.HasGrass(map, new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
            return cells;
        }

        private static void Grass2DChecks(Ctx c)
        {
            Category("2D presentation");
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            // Feedback after RC1: the default 2D grass is Phase 4's (3D tufts, feet drawn).
            Draw2D(c);
            var defaultTufts = c.Presenter.TuftObject != null ? c.Presenter.TuftObject.GetComponent<Renderer>() : null;
            Check("2D grass default: the Phase 4 look (3D tufts drawn, no grass sprites, feet not hidden)",
                !c.Pixel.OriginalGrass2D && defaultTufts != null && defaultTufts.enabled && c.Pixel.LastGrassDrawn == 0 &&
                c.Pixel.PlayerCropPixels() == 0 && c.Pixel.CrowCropPixels() == 0);
            // The 2D game's grass stays as an option (?debug=1 "2D grass"); its rules are checked below.
            c.Pixel.OriginalGrass2D = true;
            c.Pixel.ApplyTuftVisibility(true);
            var map = c.Session.OutdoorWorld.Map;
            var expected = ExpectedGrass(map);
            Check("2D grass option: one grass sprite on every cell where the 2D game grows grass (MapPlacementRules.HasGrass)",
                c.Pixel.GrassReady && expected.Count > 50 && c.Pixel.GrassCount == expected.Count &&
                expected.All(c.Pixel.HasGrass), $"{c.Pixel.GrassCount} / {expected.Count}");
            var onDirt = 0;
            var blocked = 0;
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var surface = map.SurfaceAt(cell);
                if (c.Pixel.HasGrass(cell) && surface != null && !surface.GrowsGrass) onDirt++;
                if (c.Pixel.HasGrass(cell) && MapPlacementRules.BlocksMovement(map, cell)) blocked++;
            }
            Check("2D grass: none on dirt or other non-grass surfaces, none under blocking objects", onDirt == 0 && blocked == 0,
                $"dirt {onDirt} blocked {blocked}");
            Draw2D(c);
            Check("2D grass: drawn as ground (one batch under every 3D object), from the Map JSON only", c.Pixel.LastGrassDrawn == expected.Count,
                $"drawn {c.Pixel.LastGrassDrawn}");
            var tufts = c.Presenter.TuftObject != null ? c.Presenter.TuftObject.GetComponent<Renderer>() : null;
            Check("2D grass: the 3D grass tufts are left out of the 2D view (presentation only)", tufts != null && !tufts.enabled);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.1f);
            Check("3D: the 3D grass tufts are drawn in 3D", tufts.enabled);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.1f);
            Check("2D grass: no grass collision (the player walks through grass cells as before)",
                expected.Where(cell => c.Session.World.CanEnter(cell)).Count() == expected.Count);
            // Deterministic: the same cells every time the map is shown.
            var first = expected.Where(c.Pixel.HasGrass).ToList();
            ToMap(c, true, new Vector2Int(0, 0), FacingDirection.Down);
            Settle(c, 0.1f);
            var interiorExpected = ExpectedGrass(c.Session.InteriorWorld.Map);
            Check("2D grass indoors: as the 2D game (house floor grows none)", c.Pixel.GrassCount == interiorExpected.Count,
                $"{c.Pixel.GrassCount} / {interiorExpected.Count}");
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Settle(c, 0.1f);
            Check("2D grass: deterministic (the same cells after a map change)",
                c.Pixel.GrassCount == first.Count && first.All(c.Pixel.HasGrass));

            // Feet: the 2D game's PlayerGrassOcclusion (10 of 64 px) on grass, nothing on dirt.
            var grass = FindCell(c, cell => c.Pixel.HasGrass(cell) && c.Pixel.HasGrass(cell + Vector2Int.right) &&
                                             c.Session.World.CanEnter(cell + Vector2Int.right));
            var dirt = FindCell(c, cell => !c.Pixel.HasGrass(cell) && c.Pixel.HasGrass(cell + Vector2Int.left) &&
                                            c.Session.World.CanEnter(cell + Vector2Int.left));
            Place(c, grass, FacingDirection.Down, PocViewMode.PixelView2D);
            Draw2D(c);
            Check("2D feet: standing on grass, the bottom 10 px of the 64 px sprite are hidden (PlayerGrassOcclusion)",
                c.Pixel.LastPlayerCropPixels == 10 && PlayerGrassOcclusion.GrassPlayerOcclusionPixels == 10, $"{c.Pixel.LastPlayerCropPixels}");
            Place(c, dirt, FacingDirection.Down, PocViewMode.PixelView2D);
            Draw2D(c);
            Check("2D feet: on dirt the feet are drawn", c.Pixel.LastPlayerCropPixels == 0);
            // A step from dirt onto grass: the mask follows the destination once half done.
            Place(c, dirt, FacingDirection.Left, PocViewMode.PixelView2D);
            Release(c);
            var early = -1;
            var late = -1;
            for (var i = 0; i < 30 && (early < 0 || late < 0); i++)
            {
                Frame(c, 1f / 60f, Vector2Int.left);
                if (!c.Session.IsMoving) continue;
                if (c.Session.StepProgressNormalized < 0.4f && early < 0) early = c.Pixel.PlayerCropPixels();
                if (c.Session.StepProgressNormalized >= 0.5f && late < 0) late = c.Pixel.PlayerCropPixels();
            }
            Release(c);
            Settle(c, 0.3f);
            Check("2D feet: walking onto grass the feet hide from the middle of the step (the 2D game's visual cell)",
                early == 0 && late == 10, $"early {early} late {late}");
            Check("2D feet: only the feet; the 64 px sprite keeps 54 px (face and body never cut)",
                64 - c.Pixel.PlayerCropPixels() >= 54);
            // Seated: nothing hidden. 3D: no sprite crop at all.
            ToMap(c, false, new Vector2Int(-2, 1), FacingDirection.Up);
            Place(c, new Vector2Int(-2, 1), FacingDirection.Up, PocViewMode.PixelView2D);
            c.Session.InteractAhead();
            Draw2D(c);
            Check("2D feet: seated on the bench nothing is hidden", c.Session.IsSeated && c.Pixel.LastPlayerCropPixels == 0);
            c.Session.Stand();
            // The crow: 4 of 32 px on grass (CrowGrassOcclusion), none in flight or perched.
            ToMap(c, false, grass, FacingDirection.Down);
            Place(c, grass, FacingDirection.Down, PocViewMode.PixelView2D);
            PlaceCrow(c, grass + Vector2Int.right);
            Draw2D(c);
            Check("2D crow: on grass its bottom 4 px are hidden (CrowGrassOcclusion)",
                c.Pixel.LastCrowCropPixels == 4 && CrowGrassOcclusion.HiddenPixels == 4, $"{c.Pixel.LastCrowCropPixels}");
            var crowDirt = FindCell(c, cell => !c.Pixel.HasGrass(cell) && c.Session.World.CanEnter(cell) && cell != c.Session.Cell);
            PlaceCrow(c, crowDirt);
            Draw2D(c);
            Check("2D crow: on dirt nothing is hidden", c.Pixel.LastCrowCropPixels == 0);
            // Rustle: a step into grass rustles its destination (StepStarted), as the 2D game's GrassField2D.
            Place(c, dirt, FacingDirection.Left, PocViewMode.PixelView2D);
            Release(c);
            var rustling = 0;
            for (var i = 0; i < 6; i++)
            {
                Frame(c, 1f / 60f, Vector2Int.left);
                rustling = Mathf.Max(rustling, c.Pixel.RustlingCount);
            }
            Release(c);
            Settle(c, 1f);
            Check("2D grass: a step onto grass plays the rustle frames, then the grass is idle again",
                rustling > 0 && c.Pixel.RustlingCount == 0, $"max {rustling}");
            Check("2D quality: 16 px per cell, 1 px outline #252B39 (kept)",
                c.Pixel.PixelsPerCell == 16 && c.Pixel.OutlinePixels == 1 &&
                ColorUtility.ToHtmlStringRGB(c.Pixel.OutlineColor) == "252B39");
            var grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PocSceneBuilder.GrassPath);
            Check("2D quality: the grass sprite stays point-sampled (import unchanged)", grassTexture != null && grassTexture.filterMode == FilterMode.Point);
            // The Phase 4 tufts can still be compared (POC option).
            c.Pixel.OriginalGrass2D = false;
            c.Pixel.ApplyTuftVisibility(true);
            tufts = c.Presenter.TuftObject.GetComponent<Renderer>(); // rebuilt with each map
            Draw2D(c);
            Check("2D grass: back to the default (3D tufts) after the option; no foot mask then",
                tufts.enabled && c.Pixel.LastGrassDrawn == 0 && c.Pixel.PlayerCropPixels() == 0);
        }

        private static Vector2Int FindCell(Ctx c, Func<Vector2Int, bool> ok)
        {
            var best = new Vector2Int(int.MaxValue, 0);
            var bestDistance = int.MaxValue;
            for (var y = -6; y <= 6; y++)
            for (var x = -6; x <= 6; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!c.Session.World.CanEnter(cell) || !c.Session.CanPlayerEnter(cell) || !ok(cell)) continue;
                var d = Mathf.Abs(x) + Mathf.Abs(y);
                if (d < bestDistance) { bestDistance = d; best = cell; }
            }
            if (bestDistance == int.MaxValue) throw new InvalidOperationException("No cell for the check");
            return best;
        }

        private static void PlaceCrow(Ctx c, Vector2Int cell)
        {
            var crow = c.Crow;
            crow.gameObject.SetActive(true);
            var now = clock + 500f;
            Set(crow, "nextFlightAt", now + 1000f);
            Set(crow, "nextActionAt", now + 1000f);
            Set(crow, "activity", CrowActivity.GroundIdle);
            typeof(CrowBehaviourCore).GetField("motion", Hidden).SetValue(crow, new GridStepMotion(cell));
            crow.transform.position = GridSpace3D.ToWorld(cell);
            crow.Tick(now);
        }

        private static void Set(object target, string name, object value) =>
            typeof(CrowBehaviourCore).GetField(name, Hidden).SetValue(target, value);

        // ---------------------------------------------------------------- seats

        // The seat surface (highest vertex below 0.35 m around the anchor) of the seat's model.
        private static float SeatSurface(Ctx c)
        {
            var view = c.Presenter.FindView(c.Session.SeatObject.InstanceId);
            var top = float.MinValue;
            foreach (var filter in view.GetComponentsInChildren<MeshFilter>())
            foreach (var v in filter.sharedMesh.vertices)
            {
                var w = filter.transform.TransformPoint(v);
                if (Mathf.Abs(w.x - c.Actor.SeatPoint.x) < 0.2f && Mathf.Abs(w.z - c.Actor.SeatPoint.z) < 0.08f && w.y < 0.35f)
                    top = Mathf.Max(top, w.y);
            }
            return top;
        }

        private static Bounds PartBounds(Ctx c, string name)
        {
            var renderer = c.Actor.GetComponentsInChildren<Renderer>().First(r => r.name == name);
            return renderer.bounds;
        }

        private static void SeatChecks(Ctx c)
        {
            Category("seats");
            foreach (var left in new[] { true, false })
            {
                var cell = new Vector2Int(left ? -2 : -1, 1);
                ToMap(c, false, cell, FacingDirection.Up);
                Place(c, cell, FacingDirection.Up, PocViewMode.ThirdPerson3D);
                var steps = c.Session.CompletedSteps;
                c.Session.InteractAhead();
                Settle(c, 0.3f);
                var anchor = left ? "SeatAnchor_L" : "SeatAnchor_R";
                var view = c.Presenter.FindView(c.Session.SeatObject.InstanceId);
                var anchorPoint = view.GetComponentsInChildren<Transform>(true).First(t => t.name == anchor).position;
                Check($"bench {(left ? "left" : "right")} 3D: the model sits at {anchor} (anchor and grid unchanged)",
                    c.Session.IsSeated && c.Session.SeatPose == "bench_sit" && Vector3.Distance(c.Actor.SeatPoint, anchorPoint) < 0.001f &&
                    c.Session.Cell == cell && c.Session.SeatCell == cell + Vector2Int.up);
                var surface = SeatSurface(c);
                var legs = PartBounds(c, "LegL");
                Check($"bench {(left ? "left" : "right")} 3D: the thighs rest on the seat (not inside it, not floating)",
                    legs.min.y >= surface - 0.005f && legs.min.y <= surface + 0.03f, $"seat {surface:0.000} thighs {legs.min.y:0.000}");
                var bench = view.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var hips = new Vector2(c.Actor.transform.position.x, c.Actor.transform.position.z);
                Check($"bench {(left ? "left" : "right")} 3D: hips over the seat, between its ends and in front of the backrest",
                    hips.x > bench.min.x + 0.05f && hips.x < bench.max.x - 0.05f && hips.y > bench.min.z && hips.y < bench.max.z);
                var bodyBounds = PartBounds(c, "haruka_blocky_placeholder");
                var inset = left ? bodyBounds.min.x - bench.min.x : bench.max.x - bodyBounds.max.x;
                Check($"bench {(left ? "left" : "right")} 3D: not flush with the bench end (drawn 0.08 toward the middle; anchor unchanged)",
                    inset >= 0.09f && Mathf.Abs(c.Actor.SeatDisplayShift.magnitude - 0.08f) < 0.001f &&
                    Vector3.Distance(c.Actor.SeatPoint, anchorPoint) < 0.001f, $"inset {inset:0.000}");
                Check($"bench {(left ? "left" : "right")}: seat display metadata (hip lift 0.03, legs 90°) for a future model",
                    Mathf.Approximately(c.Actor.CurrentSeatDisplay.HipLift, PocActorView.BenchSitDisplay.HipLift) &&
                    Mathf.Approximately(c.Actor.CurrentSeatDisplay.LegDegrees, 90f));
                c.Switcher.Toggle();
                Settle(c, 0.2f);
                Draw2D(c);
                Check($"bench {(left ? "left" : "right")} 2D: bench_sit.png, still seated after the switch",
                    c.Session.IsSeated && c.Actor.CurrentSprite == c.Actor.Sprites.benchSit);
                c.Switcher.Toggle();
                c.Session.InteractAhead();
                Settle(c, 0.2f);
                Check($"bench {(left ? "left" : "right")}: A stands up on the same cell, no step counted",
                    !c.Session.IsSeated && c.Session.Cell == cell && c.Session.CompletedSteps == steps);
            }
            // Feedback after RC1: the cushion moved onto a new sofa (sofa_basic, 2 x 1 at (4,2),
            // seats from (4,1) and (5,1) facing North, the bench_sit pose).
            ToMap(c, true, new Vector2Int(0, 0), FacingDirection.Up);
            var room = c.Session.InteriorWorld.Map;
            Check("room: the cushion is no longer in the middle; a sofa stands at (4,2)-(5,2) (cushion_basic stays in the catalog)",
                room.Objects.All(o => o.Definition.StableId != "cushion_basic") &&
                room.Objects.Count(o => o.Definition.StableId == "sofa_basic") == 1 &&
                room.Objects.Single(o => o.Definition.StableId == "sofa_basic").RootCell == new Vector2Int(4, 2) &&
                MapPlacementRules.BlocksMovement(room, new Vector2Int(4, 2)) && MapPlacementRules.BlocksMovement(room, new Vector2Int(5, 2)) &&
                AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/cushion_basic.asset") != null);
            foreach (var left in new[] { true, false })
            {
                var cell = new Vector2Int(left ? 4 : 5, 1);
                ToMap(c, true, cell, FacingDirection.Up);
                Place(c, cell, FacingDirection.Up, PocViewMode.ThirdPerson3D);
                var steps = c.Session.CompletedSteps;
                c.Session.InteractAhead();
                Settle(c, 0.3f);
                var name = left ? "left" : "right";
                var view = c.Presenter.FindView(c.Session.SeatObject != null ? c.Session.SeatObject.InstanceId : "");
                var anchorName = left ? "SeatAnchor_L" : "SeatAnchor_R";
                var anchorPoint = view != null ? view.GetComponentsInChildren<Transform>(true).First(t => t.name == anchorName).position : Vector3.zero;
                Check($"sofa {name} 3D: A sits on the sofa (bench_sit pose) at {anchorName}, the player keeps her cell",
                    c.Session.IsSeated && c.Session.SeatPose == "bench_sit" && c.Session.SeatObject.DefinitionId == "sofa_basic" &&
                    c.Session.Cell == cell && c.Session.SeatCell == cell + Vector2Int.up && Vector3.Distance(c.Actor.SeatPoint, anchorPoint) < 0.001f);
                var legs = PartBounds(c, "LegL");
                Check($"sofa {name} 3D: thighs on the seat cushion (surface 0.22), sofa display metadata",
                    legs.min.y >= 0.215f && legs.min.y <= 0.25f &&
                    Mathf.Approximately(c.Actor.CurrentSeatDisplay.HipLift, PocActorView.SofaSitDisplay.HipLift), $"thighs {legs.min.y:0.000}");
                c.Switcher.Toggle();
                Settle(c, 0.2f);
                Draw2D(c);
                Check($"sofa {name} 2D: bench_sit.png, still seated after the switch", c.Session.IsSeated &&
                    c.Actor.CurrentSprite == c.Actor.Sprites.benchSit);
                c.Switcher.Toggle();
                c.Session.InteractAhead();
                Settle(c, 0.2f);
                Check($"sofa {name}: A stands up on the same cell, no step counted",
                    !c.Session.IsSeated && c.Session.Cell == cell && c.Session.CompletedSteps == steps);
            }
            var sofaSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/sofa.png");
            Check("sofa: a 64 x 48 sprite for the 2D game and the map editor (point-sampled, 64 px per unit)",
                sofaSprite != null && sofaSprite.texture.width == 64 && sofaSprite.texture.height == 48 &&
                sofaSprite.texture.filterMode == FilterMode.Point && Mathf.Approximately(sofaSprite.pixelsPerUnit, 64f));
        }

        // ---------------------------------------------------------------- the room: mic, easel

        private static void RoomChecks(Ctx c)
        {
            Category("room");
            var room = c.Session.InteriorWorld.Map;
            Check("mic: only the stand's cell (4,-1) blocks; the cell North of it (4,0) is free",
                MapPlacementRules.BlocksMovement(room, new Vector2Int(4, -1)) && !MapPlacementRules.BlocksMovement(room, new Vector2Int(4, 0)));
            foreach (var (from, key) in new[] { (new Vector2Int(3, 0), Vector2Int.right), (new Vector2Int(4, 1), Vector2Int.down),
                                                 (new Vector2Int(5, 0), Vector2Int.left) })
            {
                ToMap(c, true, from, FacingDirection.Up);
                Place(c, from, FacingDirection.Up, PocViewMode.PixelView2D);
                Release(c);
                Frame(c, 1f / 60f, key); // one press: one step
                Release(c);
                Settle(c, 0.4f);
                Check($"mic: from {from} the player walks onto (4,0) above the mic", c.Session.Cell == new Vector2Int(4, 0),
                    $"cell {c.Session.Cell}");
            }
            var easel = c.Presenter.Views.First(v => v.DefinitionId == "art_easel_basic");
            var mic = c.Presenter.Views.First(v => v.DefinitionId == "recording_mic_basic");
            Check("easel and mic: never hidden as camera occluders; the easel fades in front of her, the mic stand does not",
                !easel.CameraOccluder && easel.FadeInFront3D && !mic.CameraOccluder && !mic.FadeInFront3D);
            // Second feedback: standing on (4,0) above the mic in 3D, the mic stays fully drawn.
            ToMap(c, true, new Vector2Int(4, 0), FacingDirection.Down);
            Place(c, new Vector2Int(4, 0), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            Settle(c, 0.6f);
            Check("mic: standing above it in 3D, it is not made see-through",
                c.Third.TreeFade.AlphaOf(mic) > 0.99f && !c.Third.TreeFade.IsSwapped(mic) &&
                mic.Renderers.All(r => r.enabled && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly));
            // Second feedback: in 2D the feet on the bottom row stay behind the cut-down South wall.
            foreach (var (cell, expectCrop) in new[] { (new Vector2Int(2, -3), true), (new Vector2Int(-3, -3), true),
                                                       (new Vector2Int(0, -3), false), (new Vector2Int(2, -2), false) })
            {
                ToMap(c, true, cell, FacingDirection.Down);
                Place(c, cell, FacingDirection.Down, PocViewMode.PixelView2D);
                Draw2D(c);
                var cropped = c.Pixel.LastWallCropPixels;
                var bottom = c.Pixel.LastSpriteRect.y + cropped * c.Pixel.LastSpritePixelSize;
                Check($"2D room {cell}: " + (expectCrop ? "the feet stop at the South wall's top edge (rows behind the wall left out)"
                        : "nothing hidden (doorway / not the bottom row)"),
                    expectCrop ? cropped > 0 && cropped <= 20 && c.Pixel.LastPlayerCropPixels == cropped : cropped == 0,
                    $"crop {cropped} px, sprite bottom {c.Pixel.LastSpriteRect.y} -> {bottom:0}");
            }
            // Walk West behind the easel ((-3,0)..(-2,0) are North of it) in 3D: it stays drawn.
            ToMap(c, true, new Vector2Int(-1, 0), FacingDirection.Left);
            Place(c, new Vector2Int(-1, 0), FacingDirection.Left, PocViewMode.ThirdPerson3D);
            var hidden = false;
            var inFront = 0;
            var fadedInFront = 0;
            Release(c);
            foreach (var (start, dir) in new[] { (new Vector2Int(-1, 0), Vector2Int.left), (new Vector2Int(-1, 1), Vector2Int.left) })
            {
                Place(c, start, FacingDirection.Left, PocViewMode.ThirdPerson3D);
                Release(c);
                for (var t = 0f; t < 1.6f; t += 1f / 60f)
                {
                    Frame(c, 1f / 60f, dir);
                    hidden |= easel.Renderers.Any(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || r.forceRenderingOff || !r.enabled);
                    if (PocTreeFade3D.HidesPlayer(easel, c.Third.ViewCamera.transform.position, GridSpace3D.ToWorld(c.Session.Position),
                            Quaternion.Euler(0f, c.Third.CurrentYaw, 0f)))
                    {
                        inFront++;
                        if (c.Third.TreeFade.AlphaOf(easel) < 0.99f) fadedInFront++;
                    }
                }
                Release(c);
                Settle(c, 0.3f);
            }
            Check("easel: walking behind it in 3D it is never hidden; where it stands in front of her it fades",
                !hidden && (inFront == 0 || fadedInFront > 0), $"hidden {hidden} in front {inFront} faded {fadedInFront}");
        }

        // ---------------------------------------------------------------- 3D outline

        private static int CountOutline(Ctx c, bool outlineOn)
        {
            var outline = c.Third.Outline;
            var camera = c.Third.ViewCamera;
            var src = new RenderTexture(ScreenW, ScreenH, 24, RenderTextureFormat.ARGB32);
            var dst = new RenderTexture(ScreenW, ScreenH, 0, RenderTextureFormat.ARGB32);
            camera.aspect = ScreenW / (float)ScreenH;
            camera.targetTexture = src;
            camera.Render();
            camera.targetTexture = null;
            if (outlineOn) outline.Render(src, dst);
            else Graphics.Blit(src, dst);
            var previous = RenderTexture.active;
            RenderTexture.active = dst;
            var read = new Texture2D(ScreenW, ScreenH, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, ScreenW, ScreenH), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            var line = (Color32)outline.OutlineColor;
            var count = read.GetPixels32().Count(p => Mathf.Abs(p.r - line.r) <= 2 && Mathf.Abs(p.g - line.g) <= 2 && Mathf.Abs(p.b - line.b) <= 2);
            UnityEngine.Object.DestroyImmediate(read);
            src.Release();
            dst.Release();
            return count;
        }

        private static void Outline3DChecks(Ctx c)
        {
            Category("3D outline");
            var outline = c.Third.Outline;
            Check("3D outline: on the 3D camera, configured with the 2D view's mask shader, #252B39, ON by default",
                outline != null && outline.Ready && outline.Enabled && ColorUtility.ToHtmlStringRGB(outline.OutlineColor) == "252B39");
            Check("3D outline: as thick as the 2D outline after its upscale (2 px at 576-720 px high, at most 3 px)",
                PocOutline3D.RadiusFor(576) == PocPixelView2D.ChooseScale(576, 16) && PocOutline3D.RadiusFor(576) == 2 &&
                PocOutline3D.RadiusFor(720) == 2 && PocOutline3D.RadiusFor(1152) == 3 && PocOutline3D.RadiusFor(1688) == 3);
            var playerTagged = c.Actor.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.uv2.Length > 0 &&
                Mathf.RoundToInt(f.sharedMesh.uv2[0].x) == PocOutlineTag.PlayerId);
            var crowTagged = c.CrowView.ModelInstance.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.uv2.Length > 0 &&
                Mathf.RoundToInt(f.sharedMesh.uv2[0].x) == PocOutlineTag.CrowId);
            Check("3D outline: the 3D models of ﾊﾙｶﾁｬﾝ and the crow carry their own IDs", playerTagged && crowTagged);
            foreach (var interior in new[] { false, true })
            {
                var cell = interior ? new Vector2Int(0, -1) : new Vector2Int(-1, 0);
                ToMap(c, interior, cell, FacingDirection.Up);
                Place(c, cell, FacingDirection.Up, PocViewMode.ThirdPerson3D);
                Settle(c, 0.5f);
                var off = CountOutline(c, false);
                var on = CoutlineCountOn(c);
                Check($"3D outline {(interior ? "indoors" : "outdoors")}: lines drawn around the objects and ﾊﾙｶﾁｬﾝ (outline-colored pixels {off} -> {on})",
                    on > off + 1500, $"{off} -> {on}");
            }
            outline.Enabled = false;
            Check("3D outline: can be switched off (?debug=1 \"3D outline\")", !outline.Enabled);
            outline.Enabled = true;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
        }

        private static int CoutlineCountOn(Ctx c) => CountOutline(c, true);

        // ---------------------------------------------------------------- line boil (after RC1)

        private static void LineBoilChecks(Ctx c)
        {
            Category("line boil");
            try
            {
                Check("line boil: a new random field 6 times a second (one field per step, slower after feedback); OFF = still",
                    PocLineBoil.StandardStepsPerSecond == 6f && PocLineBoil.StepAt(1.00f, 6f) == 6 &&
                    PocLineBoil.StepAt(1.16f, 6f) == 6 && PocLineBoil.StepAt(1.17f, 6f) == 7 && PocLineBoil.AmountFor(PocLineBoilMode.Off) == 0f &&
                    PocLineBoil.AmountFor(PocLineBoilMode.Lines) == 1f);
                // A line color found nowhere else in the picture (the room's walls are near #252B39),
                // so every line pixel is seen; restored below.
                var line = new Color32(255, 0, 255, 255);
                c.Pixel.OutlineColor = line;
                c.Third.Outline.OutlineColor = line;
                foreach (var interior in new[] { false, true })
                {
                    var cell = interior ? new Vector2Int(2, 0) : new Vector2Int(-1, 0);
                    ToMap(c, interior, cell, FacingDirection.Up);
                    foreach (var view in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                    {
                        Place(c, cell, FacingDirection.Up, view);
                        var is2D = view == PocViewMode.PixelView2D;
                        var name = (is2D ? "2D " : "3D ") + (interior ? "indoors" : "outdoors");
                        // 2D: one texel; 3D: 3/4 of the line width (2 px at 720 px high), rounded.
                        var reach = is2D ? 1 : Mathf.RoundToInt(PocOutline3D.RadiusFor(ScreenH) * 0.75f);
                        int width;
                        Color32[] Shot(PocLineBoilMode mode, int step, bool lines, out int w)
                        {
                            PocLineBoil.Apply(mode, step);
                            return is2D ? Picture2D(c, lines, out w) : Picture3D(c, lines, out w);
                        }
                        var none = Shot(PocLineBoilMode.Off, 0, false, out width);
                        var still = Shot(PocLineBoilMode.Off, 0, true, out _);
                        if (is2D)
                        {
                            // Feedback: no boil in 2D. Every mode and step gives the still 2D picture.
                            var same = new[] { PocLineBoilMode.Lines, PocLineBoilMode.LinesStrong, PocLineBoilMode.LinesAndPicture }
                                .SelectMany(m => new[] { 1, 2, 3 }.Select(k => Shot(m, k, true, out _)))
                                .All(px => Enumerable.Range(0, still.Length).All(i => SamePixel(still[i], px[i])));
                            Check($"line boil {name}: none in 2D - every mode and step gives exactly the still lines", same);
                            continue;
                        }
                        var s1 = Shot(PocLineBoilMode.Lines, 1, true, out _);
                        var s2 = Shot(PocLineBoilMode.Lines, 2, true, out _);
                        var again = Shot(PocLineBoilMode.Off, 0, true, out _);
                        var picture = Shot(PocLineBoilMode.LinesAndPicture, 1, true, out _);
                        var a = CompareBoil(none, still, s1, width, reach, line);
                        var b = CompareBoil(none, still, s2, width, reach, line);
                        var p = CompareBoil(none, still, picture, width, reach, line);
                        var steps = Enumerable.Range(0, s1.Length).Count(i => !SamePixel(s1[i], s2[i]));
                        Check($"line boil {name}: the lines twitch (different at each step)",
                            a.Changed > 0 && b.Changed > 0 && steps > 0,
                            $"still line px {a.StillInk}, changed px step1 {a.Changed} step2 {b.Changed}, step1 vs step2 {steps}");
                        int stray = 0, gaps = 0, most = 0;
                        for (var step = 1; step <= 8; step++)
                        {
                            var e = step == 1 ? a : step == 2 ? b : CompareBoil(none, still, Shot(PocLineBoilMode.Lines, step, true, out _), width, reach, line);
                            stray += e.Stray;
                            gaps += e.Gaps;
                            most = Mathf.Max(most, e.BoiledInk);
                        }
                        Check($"line boil {name}: only a little - within {reach} px of the still line, never leaving a gap (8 steps)",
                            stray == 0 && gaps == 0, $"stray {stray}, gaps {gaps}, line px still {a.StillInk} / boiled at most {most}");
                        Check($"line boil {name}: only the lines move (the fills stay); \"lines + picture\" (?debug=1) moves the fills too",
                            a.NotLine == 0 && b.NotLine == 0 && p.NotLine > 0, $"fill px moved {a.NotLine}/{b.NotLine}, picture mode {p.NotLine}");
                        Check($"line boil {name}: OFF gives exactly the still lines",
                            Enumerable.Range(0, still.Length).All(i => SamePixel(still[i], again[i])));
                    }
                }
            }
            finally
            {
                PocLineBoil.Apply(PocLineBoilMode.Off, 0);
                c.Pixel.OutlineColor = new Color32(0x25, 0x2b, 0x39, 0xff);
                c.Third.Outline.OutlineColor = new Color32(0x25, 0x2b, 0x39, 0xff);
                // One more pass of each, so the shared materials hold the real color again.
                Picture2D(c, true, out _);
                Picture3D(c, true, out _);
                c.Switcher.SetMode(PocViewMode.PixelView2D);
            }
        }

        private struct BoilStats
        {
            public int StillInk, BoiledInk, Changed, Stray, Gaps, NotLine;
        }

        // none: the picture without lines; still: with the still lines; boiled: one step of the boil.
        // Stray: a boiled line pixel further than 'reach' from every still line pixel; Gaps: a still
        // line pixel with no boiled line pixel within 'reach'; NotLine: a pixel that changed to a
        // color that is not on the way from the picture to the line color (a moved fill).
        private static BoilStats CompareBoil(Color32[] none, Color32[] still, Color32[] boiled, int width, int reach, Color32 line)
        {
            var height = none.Length / width;
            var stillInk = new bool[none.Length];
            var boiledInk = new bool[none.Length];
            var stats = new BoilStats();
            for (var i = 0; i < none.Length; i++)
            {
                stillInk[i] = !SamePixel(still[i], none[i]);
                boiledInk[i] = !SamePixel(boiled[i], none[i]);
                if (stillInk[i]) stats.StillInk++;
                if (boiledInk[i]) stats.BoiledInk++;
                if (!SamePixel(still[i], boiled[i])) stats.Changed++;
                if (boiledInk[i] && !Between(boiled[i], none[i], line)) stats.NotLine++;
            }
            bool Near(bool[] ink, int x, int y)
            {
                for (var dy = -reach; dy <= reach; dy++)
                for (var dx = -reach; dx <= reach; dx++)
                {
                    int qx = x + dx, qy = y + dy;
                    if (qx >= 0 && qy >= 0 && qx < width && qy < height && ink[qy * width + qx]) return true;
                }
                return false;
            }
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                if (boiledInk[i] && !stillInk[i] && !Near(stillInk, x, y)) stats.Stray++;
                if (stillInk[i] && !boiledInk[i] && !Near(boiledInk, x, y)) stats.Gaps++;
            }
            return stats;
        }

        private static bool SamePixel(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) <= 1 && Mathf.Abs(a.g - b.g) <= 1 && Mathf.Abs(a.b - b.b) <= 1;

        private static bool Between(Color32 p, Color32 from, Color32 to) =>
            p.r >= Mathf.Min(from.r, to.r) - 2 && p.r <= Mathf.Max(from.r, to.r) + 2 &&
            p.g >= Mathf.Min(from.g, to.g) - 2 && p.g <= Mathf.Max(from.g, to.g) + 2 &&
            p.b >= Mathf.Min(from.b, to.b) - 2 && p.b <= Mathf.Max(from.b, to.b) + 2;

        // The 2D low-resolution picture after the outline pass (before the upscale and the sprite).
        private static Color32[] Picture2D(Ctx c, bool lines, out int width)
        {
            var pixels = c.Pixel.OutlinePixels;
            if (!lines) c.Pixel.OutlinePixels = 0;
            c.Pixel.UpdateView(0f, ScreenW, ScreenH);
            c.Pixel.WorldCamera.Render();
            var result = ReadAll(c.Pixel.ComposeOutline());
            c.Pixel.OutlinePixels = pixels;
            width = c.Pixel.Target.width;
            return result;
        }

        private static Color32[] Picture3D(Ctx c, bool lines, out int width)
        {
            var camera = c.Third.ViewCamera;
            var src = new RenderTexture(ScreenW, ScreenH, 24, RenderTextureFormat.ARGB32);
            var dst = new RenderTexture(ScreenW, ScreenH, 0, RenderTextureFormat.ARGB32);
            camera.aspect = ScreenW / (float)ScreenH;
            camera.targetTexture = src;
            camera.Render();
            camera.targetTexture = null;
            if (lines) c.Third.Outline.Render(src, dst);
            else Graphics.Blit(src, dst);
            var result = ReadAll(dst);
            src.Release();
            dst.Release();
            UnityEngine.Object.DestroyImmediate(src);
            UnityEngine.Object.DestroyImmediate(dst);
            width = ScreenW;
            return result;
        }

        private static Color32[] ReadAll(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            var pixels = read.GetPixels32();
            UnityEngine.Object.DestroyImmediate(read);
            return pixels;
        }

        // ---------------------------------------------------------------- the menu

        private static void MenuChecks(Ctx c)
        {
            Category("menu");
            var menu = c.Menu;
            Check("menu: one extra row, ひょうじ (full screen moved to a button on the page); the font has every glyph",
                menu.ExtraItems.Count == 1 && menu.ExtraItems[0].Label == "ひょうじ" &&
                menu.ExtraItems.All(item => item.Label != "ぜんがめん") && GameBitmapFont.HasAllGlyphs(PocDebugPanel.ReleaseCandidateVersionLabel));
            ClearMessage(c);
            menu.SetOpen(true, clock);
            var before = c.Switcher.Mode;
            menu.ActivateExtraItem(0, clock + 1f);
            Check("menu: ひょうじ switches 2D / 3D", c.Switcher.Mode != before);
            menu.ActivateExtraItem(0, clock + 1.5f);
            menu.SetOpen(false, clock + 2f);
            Check("menu: the ひょうじ row still switches the view", menu.ExtraItem.Label == "ひょうじ");
        }

        // ---------------------------------------------------------------- crow

        private static void CrowChecks(Ctx c)
        {
            Category("crow");
            var crow = c.Crow;
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            crow.ApplySpawnFrom(c.Session.OutdoorWorld.Map);
            var now = clock + 700f;
            Set(crow, "nextFlightAt", now);
            Set(crow, "nextActionAt", now + 1000f);
            crow.Tick(now);
            var states = new List<string>();
            for (var i = 0; i < 160 && crow.Activity != CrowActivity.Perched; i++)
            {
                crow.Tick(now += 0.05f);
                if (i % 2 == 0 && states.Count < 8)
                {
                    var before = $"{crow.Activity} {crow.transform.position:F3}";
                    c.Switcher.Toggle();
                    c.CrowView.Apply(1f / 60f, now);
                    var after = $"{crow.Activity} {crow.transform.position:F3}";
                    states.Add(before == after ? "same" : before + " -> " + after);
                }
            }
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("crow: one shared state; 8 view switches during the flight change nothing",
                states.Count >= 8 && states.All(s => s == "same"), string.Join(", ", states.Take(8)));
            Check("crow: flies to a tree and perches (the 2D game's AI, unchanged)", crow.Activity == CrowActivity.Perched);
            Set(crow, "perchedUntil", now + 1000f);
            Draw2D(c);
            Check("crow 2D perched: drawn at the tree's perch anchor, on top, feet not masked (no 2D-only state)",
                c.Pixel.LastCrowOnTop && c.Pixel.LastCrowCropPixels == 0 &&
                Vector3.Distance(crow.transform.position, crow.TargetTreeView.PerchAnchor.position) < 0.01f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            for (var i = 0; i < 20; i++) c.CrowView.Apply(1f / 60f, now += 1f / 60f);
            Check("crow 3D perched: the model stands on the same anchor", c.CrowView.ModelShown &&
                Vector3.Distance(c.CrowView.transform.position, crow.transform.position) < 0.001f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            // Talk: one message, one voice.
            PlaceCrow(c, new Vector2Int(1, 0));
            Place(c, new Vector2Int(0, 0), FacingDirection.Right, PocViewMode.PixelView2D);
            var voice = (InteractionAudio)typeof(PocCrow).GetField("voice", Hidden).GetValue(crow);
            var voices = voice.PlayRequestCount;
            var talks = crow.TalkCount;
            ClearMessage(c);
            c.Session.InteractAhead();
            Check("crow voice: one talk plays the voice once (no double play from the 2D/3D views)",
                crow.TalkCount == talks + 1 && voice.PlayRequestCount == voices + 1 &&
                UnityEngine.Object.FindObjectsByType<InteractionAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Count(a => a == voice) == 1);
            Check("crow 3D: legs tuck in flight (Phase 4 feedback kept)",
                PocCrowView3D.LegTuckDegrees < -60f && PocCrowView3D.LegTuckScale < 1f);
        }

        // ---------------------------------------------------------------- camera

        private static void CameraChecks(Ctx c)
        {
            Category("camera");
            Check("3D camera: North-fixed by default, 3.4 outdoors / 3.6 indoors",
                c.Third.Follow == PocCameraFollow.NorthFixed && Mathf.Approximately(c.Third.OutdoorDistance, 3.4f) &&
                Mathf.Approximately(c.Third.IndoorDistance, 3.6f));
            var door = c.Session.OutdoorWorld.Map.HouseDoorCell;
            // Walk out holding S: the house is shown, then the still-held S walks on South.
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            var raw = RawFor(c, Vector2Int.down);
            HoldUntil(c, raw, () => c.Session.World == c.Session.OutdoorWorld, 3f, false);
            var exitCell = c.Session.Cell;
            var held = 0f;
            var firstStep = Vector2Int.zero;
            var maxMove = 0f;
            var last = c.Third.ViewCamera.transform.position;
            var sawComposition = c.Third.CompositionHeld;
            var cells = new List<Vector2Int> { exitCell };
            for (var t = 0f; t < 2f; t += 1f / 60f)
            {
                var cell = c.Session.Cell;
                Frame(c, 1f / 60f, raw);
                sawComposition |= c.Third.CompositionHeld;
                if (firstStep == Vector2Int.zero && c.Session.IsMoving) { firstStep = c.Session.StepToCell - cell; held = t; }
                maxMove = Mathf.Max(maxMove, Vector3.Distance(c.Third.ViewCamera.transform.position, last));
                last = c.Third.ViewCamera.transform.position;
                if (cells[cells.Count - 1] != c.Session.Cell) cells.Add(c.Session.Cell);
            }
            Release(c);
            Settle(c, 0.3f);
            Check("exit with a held key: the house composition is shown first",
                sawComposition && exitCell == door + Vector2Int.down);
            Check("exit with a held key: after about 0.6 s the held S walks on South (never reversed, never back into the house)",
                firstStep == Vector2Int.down && held >= PocThirdPersonCamera.StandardExitHeldKeyResumeSeconds - 0.05f &&
                held <= PocThirdPersonCamera.StandardExitHeldKeyResumeSeconds + 0.1f &&
                c.Session.World == c.Session.OutdoorWorld && cells.All(p => p != door),
                $"first {firstStep} after {held:0.00}s cells {string.Join(" ", cells)}");
            Check("exit with a held key: the camera returns smoothly (< 0.12 per frame) and keeps looking North",
                maxMove < 0.12f && !c.Third.CompositionHeld && c.Third.Heading == FacingDirection.Up, $"max {maxMove:0.000}");
            // The Phase 4 rule is kept as an option: wait for the release.
            c.Third.ExitHeldKeyResumeSeconds = 0f;
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            raw = RawFor(c, Vector2Int.down);
            HoldUntil(c, raw, () => c.Session.World == c.Session.OutdoorWorld, 3f, false);
            var cellAfter = c.Session.Cell;
            for (var t = 0f; t < 1.5f; t += 1f / 60f) Frame(c, 1f / 60f, raw);
            Check("exit option (Phase 4): with the resume off, a held key waits for its release",
                c.Session.Cell == cellAfter && c.Third.CompositionHeld);
            Release(c);
            Settle(c, 0.2f);
            c.Third.ExitHeldKeyResumeSeconds = PocThirdPersonCamera.StandardExitHeldKeyResumeSeconds;
            // Another follow mode (POC option) keeps the Phase 4 rule: keys may mean something else.
            c.Third.Follow = PocCameraFollow.TurnBehindWhenIdle;
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.ThirdPerson3D);
            raw = RawFor(c, Vector2Int.down);
            HoldUntil(c, raw, () => c.Session.World == c.Session.OutdoorWorld, 3f, false);
            cellAfter = c.Session.Cell;
            for (var t = 0f; t < 1.5f; t += 1f / 60f) Frame(c, 1f / 60f, raw);
            Check("exit (turn-behind camera option): a held key still waits for its release (no reversal possible)",
                c.Session.Cell == cellAfter);
            Release(c);
            Settle(c, 0.2f);
            c.Third.Follow = PocCameraFollow.NorthFixed;
            // Exit in 2D: no composition, the held key keeps walking as in the 2D game.
            ToMap(c, true, new Vector2Int(0, -3), FacingDirection.Down);
            Place(c, new Vector2Int(0, -3), FacingDirection.Down, PocViewMode.PixelView2D);
            HoldUntil(c, Vector2Int.down, () => c.Session.World == c.Session.OutdoorWorld, 3f, false);
            cellAfter = c.Session.Cell;
            for (var t = 0f; t < 0.6f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.down);
            Check("exit in 2D: unchanged (no composition; the held key keeps walking)",
                !c.Third.CompositionHeld && c.Session.Cell.y < cellAfter.y);
            Release(c);
            Settle(c, 0.2f);
            // Tree fade in 3D (Phase 4) is still on.
            Check("3D see-through trees: still on", c.Third.TreeFade != null && c.Third.TreeFade.Enabled);
        }

        // ---------------------------------------------------------------- phone

        private static void MobileChecks(Ctx c)
        {
            Category("mobile");
            Check("phone: the orientation guard exists; upright play is allowed when it is off (the RC)", c.Guard != null);
            var landscapeOnly = c.Guard.LandscapeOnly;
            c.Guard.LandscapeOnly = false;
            c.Guard.TouchDevice = true;
            c.Guard.Evaluate(390, 844);
            Check("phone upright (RC rule): no guide, the game is not paused", !c.Guard.GuideVisible && !c.Gameplay.Suspended);
            c.Guard.Evaluate(844, 390);
            c.Guard.LandscapeOnly = landscapeOnly;
            c.Guard.TouchDevice = false;
            c.Guard.Evaluate(1280, 720);
            var publicCss = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "halkaworld", "game-page.css"));
            Check("public game-page.css: not changed (an upright phone keeps the public page's frame)",
                publicCss.Contains("clamp(420px, 54vw, 640px)") && publicCss.Contains("min(68svh, 620px)"));
        }

        // ---------------------------------------------------------------- save / time in the session

        private static void SessionSaveChecks(Ctx c)
        {
            Category("save/time");
            ToMap(c, false, new Vector2Int(0, 0), FacingDirection.Down);
            Place(c, new Vector2Int(0, 0), FacingDirection.Down, PocViewMode.PixelView2D);
            var life = c.LifeLog.TotalSteps;
            var done = c.Session.CompletedSteps;
            HoldUntil(c, Vector2Int.left, () => c.Session.Cell.x <= -1, 1.5f);
            Settle(c, 0.2f);
            c.LifeLog.Save();
            var saved = PlayerPrefs.GetString(LifeLogController.SaveKey, "");
            Check("Life Log: steps add up and save under the same key and format", saved.StartsWith("v1|") &&
                c.Session.CompletedSteps - done >= 1 && LifeLogData.Deserialize(saved).TotalSteps == life + (c.Session.CompletedSteps - done),
                $"{saved} steps {c.Session.CompletedSteps - done}");
            Check("World Time: the anchor is kept under its key (never reset by the RC)",
                PlayerPrefs.GetString(WorldTimeController.SaveKey, "") == c.Clock.AnchorMilliseconds.ToString());
        }

        // ---------------------------------------------------------------- protection

        private static void ProtectionChecks()
        {
            Category("protection");
            var root = PocSceneBuilder.RepositoryRoot;
            foreach (var list in new[] { "official_map_sha256.txt", "player_sprite_sha256.txt", "phase5_protected_sha256.txt" })
            {
                var lines = File.ReadAllLines(Path.Combine(root, "tools/poc2d3d", list)).Where(l => l.Length > 66).ToList();
                var bad = lines.Where(line =>
                {
                    var relative = line.Substring(65).TrimStart('*', ' ');
                    var path = Path.Combine(root, relative);
                    if (!File.Exists(path)) path = Path.Combine(root, "unity-project", relative);
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    return !File.Exists(path) || !BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                        .Replace("-", "").Equals(line.Substring(0, 64), StringComparison.OrdinalIgnoreCase);
                }).ToList();
                Check($"protection: {list} — every file unchanged (SHA-256)", lines.Count > 0 && bad.Count == 0,
                    $"{lines.Count} files, changed: {string.Join(", ", bad)}");
            }
            var publicPage = File.ReadAllText(Path.Combine(root, "halkaworld", "index.html"));
            Check("protection: the public game page still embeds ver2.9 (not replaced)", publicPage.Contains("webgl/?v=2.9&build="));
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
            var holder = new GameObject("Phase 5 check holder");
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
    }
}
