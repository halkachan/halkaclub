using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど Phase 7: the release checks for ver3.0 RC3. The official 3D ﾊﾙｶﾁｬﾝ (adopted
    // by the user, not redesigned), the production scene and build settings, the save path from
    // fully damaged data back through an import, the ver3.0 public page prepared for the release,
    // and what must not change before publishing (ver2.9 page and build, other pages, maps, keys).
    // The game is started with the Phase 6 harness (the game's own start order, memory storage).
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase7ReleaseChecks.RunBatch
    public static class Phase7ReleaseChecks
    {
        // ver2.9 as published (origin/main before the ver3.0 work) and the Phase 6 final commit.
        public const string PublicBase = "13462feb";
        public const string Phase6Final = "6926976";
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static int passes;
        private static string category = "";

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
            var keys = new[] { LifeLogController.SaveKey, WorldTimeController.SaveKey };
            var saved = keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(7707);
            try
            {
                ModelChecks();
                GameChecks();
                SaveChecks();
                PageChecks();
                ReleaseChecks();
            }
            finally
            {
                C6.DisposeHarness();
                UnityEngine.Random.state = randomState;
                foreach (var pair in saved)
                {
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                    else PlayerPrefs.SetString(pair.Key, pair.Value);
                }
                PlayerPrefs.Save();
            }
            foreach (var failure in failures) Debug.LogError("PHASE7 FAIL " + failure);
            Debug.Log("HARUKA Phase 7 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA Phase 7 release checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Category(string name) => category = name;

        private static void Check(string name, bool condition, string detail = "")
        {
            perCategory[category] = perCategory.TryGetValue(category, out var n) ? n + 1 : 1;
            if (condition) { passes++; Debug.Log("PHASE7 PASS [" + category + "] " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- helpers

        private static string Root => PocSceneBuilder.RepositoryRoot;
        private static string Repo(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        private static string ReadRepo(string path) => File.Exists(Repo(path)) ? File.ReadAllText(Repo(path)).Replace("\r\n", "\n") : "";

        // SHA-256 like tools/poc2d3d/player_model_manifest.py: text files with LF line ends.
        private static string Sha256(string path)
        {
            var full = Repo(path);
            if (!File.Exists(full)) return "";
            var data = File.ReadAllBytes(full);
            if (new[] { ".py", ".meta", ".mat", ".shader" }.Any(path.EndsWith))
                data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data).Replace("\r\n", "\n"));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        // git in the repository (read only): exit code and output.
        private static (int code, string output) Git(string arguments)
        {
            foreach (var exe in new[] { "git", @"C:\Program Files\Git\cmd\git.exe" })
            {
                try
                {
                    var info = new ProcessStartInfo(exe, arguments)
                    {
                        WorkingDirectory = Root, UseShellExecute = false, RedirectStandardOutput = true,
                        RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8,
                    };
                    using var process = Process.Start(info);
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(60000);
                    return (process.ExitCode, output);
                }
                catch (Exception) { }
            }
            return (-1, "git not found");
        }

        private static string ChangedSince(string commit, string paths)
        {
            var (code, output) = Git("diff --name-only " + commit + " -- " + paths);
            return code == 0 ? output.Trim() : "git failed: " + output;
        }

        private static Dictionary<string, string> ManifestFiles(string manifest)
        {
            // "key": { "path": "...", "sha256": "..." } pairs of the files block (no JSON parser needed).
            var files = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(manifest, "\"path\": \"([^\"]+)\",\\s*\"sha256\": \"([0-9a-f]{64})\""))
                files[m.Groups[1].Value] = m.Groups[2].Value;
            return files;
        }

        // ---------------------------------------------------------------- the official 3D model

        private static void ModelChecks()
        {
            Category("model");
            var manifest = ReadRepo("blender/exports/haruka_player_official_manifest.json");
            var files = ManifestFiles(manifest);
            Check("official model manifest: status official, the Blender source, generator, FBX, import settings and material listed",
                manifest.Contains("\"status\": \"official\"") && files.Count == 7 &&
                files.ContainsKey("blender/sources/phase2_1/haruka_blocky_placeholder.blend") &&
                files.ContainsKey("unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_blocky_placeholder.fbx"));
            var changed = files.Where(f => Sha256(f.Key) != f.Value).Select(f => f.Key).ToList();
            Check("the model is not redesigned: Blender source, generator, FBX, import settings and material match the manifest (SHA-256)",
                files.Count == 7 && changed.Count == 0, string.Join(", ", changed));
            Check("the FBX and the Blender source are the Phase 6 files (no change since " + Phase6Final + ")",
                ChangedSince(Phase6Final, "blender/sources unity-project/Assets/POC2D3D/Models") == "");
            var built = ReadRepo("blender/exports/phase2_1_haruka_blocky_placeholder_manifest.json");
            Check("the generator's manifest names the model official (no TEMPORARY left); the script states it too",
                built.Contains("\"status\": \"OFFICIAL 3D player model") && !built.Contains("TEMPORARY") &&
                ReadRepo("blender/scripts/build_haruka_blocky.py").Contains("the OFFICIAL 3D ﾊﾙｶﾁｬﾝ"));
            Check("previews of the model exist (front, back, both sides)",
                new[] { "south_front", "east", "north_back", "west" }.All(v =>
                    File.Exists(Repo($"blender/exports/previews/phase2_1/haruka_blocky_placeholder_{v}.png"))));
            var sprites = Regex.Matches(manifest, "\"(Assets/Content/Character/[^\"]+\\.png)\": \"([0-9a-f]{64})\"").Cast<Match>().ToList();
            var spriteChanged = sprites.Where(m => Sha256("unity-project/" + m.Groups[1].Value) != m.Groups[2].Value).ToList();
            Check("2D: the 50 Player sprites match the recorded set (SHA-256; the user's redrawn set since 2026-10-20)", sprites.Count == 50 && spriteChanged.Count == 0,
                string.Join(", ", spriteChanged.Select(m => m.Groups[1].Value)));

            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.OfficialPlayerModelPath);
            Check("catalog: the player is the official model (not marked temporary), scale 1, no pivot offset, front yaw 180, the four limbs",
                catalog != null && fbx != null && catalog.Player.model == fbx && !catalog.Player.placeholder &&
                Mathf.Approximately(catalog.Player.uniformScale, 1f) && catalog.Player.pivotOffset == Vector3.zero &&
                Mathf.Approximately(catalog.Player.yawOffsetDegrees, 180f) && catalog.Player.armLeft == "ArmL" &&
                catalog.Player.legRight == "LegR" && Mathf.Approximately(catalog.Player.swingDegrees, 26f));
            Check("catalog: 2D keeps the original sprite set (complete), the model is never drawn in 2D",
                catalog != null && catalog.Player.sprites != null && catalog.Player.sprites.IsComplete);
            var toon = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ToonMaterialPath);
            var renderers = fbx != null ? fbx.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            Check("material: every part uses the shared vertex-color toon material (POC_ToonVertexColor)",
                renderers.Length == 5 && renderers.All(r => r.sharedMaterials.Length == 1 && r.sharedMaterials[0] == toon));
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in fbx != null ? fbx.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0])
            {
                var mesh = filter.sharedMesh;
                foreach (var corner in new[] { mesh.bounds.min, mesh.bounds.max })
                {
                    var world = filter.transform.localToWorldMatrix.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; } else bounds.Encapsulate(world);
                }
            }
            var triangles = fbx != null ? fbx.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3) : 0;
            Check("geometry: 1,030 triangles, feet at the root (y 0), about two cells tall (0.97), about two heads",
                triangles == 1030 && Mathf.Abs(bounds.min.y) < 0.005f && Mathf.Abs(bounds.max.y - 0.968f) < 0.01f, $"{triangles} tris, {bounds.min.y:0.000}..{bounds.max.y:0.000}");

            var c = C6.Start(new HarukaMemoryStorage());
            var s = c.Session;
            C6.Settle(c, 0.5f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            C6.ToOutdoor(c, new Vector2Int(1, -3));
            var facingOk = true;
            foreach (FacingDirection facing in Enum.GetValues(typeof(FacingDirection)))
            {
                s.PlaceForCheck(new Vector2Int(1, -3), facing);
                c.Actor.Apply(0f);
                var forward = c.Actor.ModelInstance.transform.rotation * Vector3.back; // Blender front = South = Unity -Z
                var dir = facing.ToVector();
                facingOk &= Vector2.Dot(new Vector2(forward.x, forward.z).normalized, new Vector2(dir.x, dir.y)) > 0.99f;
            }
            Check("3D: the model faces the Grid facing in all four directions (front, back, left, right)", facingOk);
            s.PlaceForCheck(new Vector2Int(1, -3), FacingDirection.Down);
            c.Actor.Apply(0f);
            Check("3D: drawn at the shared cell (pivot = cell centre on the ground), upright, scale 1, visible; the sprite hidden",
                (c.Actor.transform.position - GridSpace3D.ToWorld(s.Position)).sqrMagnitude < 1e-8f &&
                Vector3.Angle(c.Actor.transform.up, Vector3.up) < 0.01f && c.Actor.ModelVisible &&
                Mathf.Abs(c.Actor.ModelInstance.transform.lossyScale.y - 1f) < 1e-4f && c.Actor.ModelInstance.name == "haruka_blocky_placeholder");
            Check("3D idle: limbs at rest", c.Actor.HasLimbs && Mathf.Abs(c.Actor.CurrentSwingDegrees) < 0.01f);
            s.PlaceForCheck(new Vector2Int(1, -3), FacingDirection.Right);
            var startCell = s.Cell;
            s.Tick(0f, Vector2Int.right);
            s.Tick(0.045f, Vector2Int.zero);
            c.Actor.Apply(0f);
            var mid = c.Actor.CurrentSwingDegrees;
            for (var i = 0; i < 10; i++) s.Tick(0.02f, Vector2Int.zero);
            c.Actor.Apply(0f);
            Check("3D walk: limbs swing during the step and rest after it; one cell in 0.18 s",
                Mathf.Abs(mid) > 5f && Mathf.Abs(c.Actor.CurrentSwingDegrees) < 0.01f && s.Cell == startCell + Vector2Int.right, $"mid {mid:0.0}");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            C6.Settle(c, 0.1f);
            Check("2D: the original sprite is drawn, the 3D model is hidden", !c.Actor.ModelVisible && c.Actor.HasSprites && c.Actor.CurrentSprite != null);
            foreach (var mode in new[] { PocViewMode.ThirdPerson3D, PocViewMode.PixelView2D })
            {
                // The bench's left seat outdoors, the sofa indoors (Phase 5 layout).
                C6.ToOutdoor(c, new Vector2Int(-2, 1));
                c.Switcher.SetMode(mode);
                s.PlaceForCheck(new Vector2Int(-2, 1), FacingDirection.Up);
                s.InteractAhead();
                C6.Settle(c, 0.4f);
                var benchOk = s.IsSeated && s.SeatObject != null && s.SeatObject.DefinitionId == "bench_basic";
                var benchPose = mode == PocViewMode.ThirdPerson3D
                    ? Mathf.Approximately(c.Actor.CurrentSeatDisplay.LegDegrees, PocActorView.SitLegDegrees) && c.Actor.ModelVisible
                    : c.Actor.CurrentSprite == c.Actor.Sprites.benchSit && !c.Actor.ModelVisible;
                s.InteractAhead();
                C6.Settle(c, 0.3f);
                s.LoadMapForCheck(true, new Vector2Int(4, 1), FacingDirection.Up);
                C6.Settle(c, 0.2f);
                s.PlaceForCheck(new Vector2Int(4, 1), FacingDirection.Up);
                s.InteractAhead();
                C6.Settle(c, 0.4f);
                var sofaOk = s.IsSeated && s.SeatObject != null && s.SeatObject.DefinitionId == "sofa_basic";
                var sofaPose = mode == PocViewMode.ThirdPerson3D
                    ? Mathf.Approximately(c.Actor.CurrentSeatDisplay.LegDegrees, PocActorView.SitLegDegrees) &&
                      Mathf.Approximately(c.Actor.CurrentSeatDisplay.HipLift, PocActorView.SofaSitDisplay.HipLift)
                    : c.Actor.CurrentSprite == c.Actor.Sprites.benchSit;
                s.InteractAhead();
                C6.Settle(c, 0.3f);
                var name = mode == PocViewMode.ThirdPerson3D ? "3D" : "2D";
                Check($"{name} sit: bench (outdoors) and sofa (indoors) with the seated pose, standing up again",
                    benchOk && benchPose && sofaOk && sofaPose && !s.IsSeated, $"bench {benchOk}/{benchPose} sofa {sofaOk}/{sofaPose}");
            }
        }

        // ---------------------------------------------------------------- the game and the production scene

        private static void GameChecks()
        {
            Category("game");
            Check("production scene: the RC3 scene file exists (separate from the technical POC scene)",
                PocSceneBuilder.RcScenePath.EndsWith("HARUKA_WORLD_ver3.0_RC3.unity") && File.Exists(PocSceneBuilder.RcScenePath));
            EditorSceneManager.OpenScene(PocSceneBuilder.RcScenePath, OpenSceneMode.Single);
            var panel = UnityEngine.Object.FindAnyObjectByType<PocDebugPanel>(FindObjectsInactive.Include);
            var hud = UnityEngine.Object.FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
            var menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>(FindObjectsInactive.Include);
            var save = UnityEngine.Object.FindAnyObjectByType<HarukaSaveSystem>(FindObjectsInactive.Include);
            var switcher = UnityEngine.Object.FindAnyObjectByType<PocViewSwitcher>(FindObjectsInactive.Include);
            var actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include);
            Check("production scene: the debug panel is in release mode (hidden and off unless ?debug=1)",
                panel != null && panel.ReleaseCandidate);
            Check("version: the menu and the save files say ver3.0 (what players will see); RC3 only in the debug title and build info",
                PocDebugPanel.ReleaseCandidateVersionLabel == "ver3.0" && menu != null && menu.VersionText == "ver3.0" &&
                hud != null && !hud.VersionLabelShown && save != null &&
                (string)new SerializedObject(save).FindProperty("gameVersion").stringValue == "ver3.0" &&
                panel.Title == "はるかわーるど ver3.0 RC3 (debug)" && PocDebugPanel.ReleaseCandidateName.EndsWith("Release Candidate 3"));
            Check("title: はるかわーるど (the page title of the game frame)", PocDebugPanel.ReleaseCandidateTitle == "はるかわーるど");
            Check("production scene: starts in 2D (a first visit), the save system loads before the session",
                switcher != null && save != null &&
                save.GetType().GetCustomAttributes(typeof(DefaultExecutionOrder), false).Cast<DefaultExecutionOrder>().First().order <
                typeof(SharedWorldSession).GetCustomAttributes(typeof(DefaultExecutionOrder), false).Cast<DefaultExecutionOrder>().First().order);
            Check("production scene: the player uses the official model", actor != null);
            Check("build settings: no Development Build, no script debugging",
                !EditorUserBuildSettings.development && !EditorUserBuildSettings.allowDebugging);
            Check("WebGL settings: no Unity splash, the MinimalNoBrand template, hashed file names (cache), no full stack traces",
                !PlayerSettings.SplashScreen.show && PlayerSettings.WebGL.template == "PROJECT:MinimalNoBrand" &&
                PlayerSettings.WebGL.nameFilesAsHashes && PlayerSettings.WebGL.exceptionSupport != WebGLExceptionSupport.FullWithStacktrace);
            Check("WebGL settings: uncompressed files like ver2.9 (GitHub Pages sends no Content-Encoding for .br / .gz)",
                PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled);

            var c = C6.Start(new HarukaMemoryStorage());
            C6.Settle(c, 0.5f);
            var outdoor = c.Session.OutdoorWorld;
            var house = c.Session.InteriorWorld;
            Check("official maps: first_field outdoors and halka_house indoors", outdoor.MapId == "first_field" && house.MapId == "halka_house");
            var furniture = house.Objects.Select(o => o.DefinitionId).ToList();
            Check("furniture indoors (Phase 5 layout): bed, desk, recording mic, easel, shelf, sofa; the old centre cushion is not back",
                new[] { "bed_basic", "desk_basic", "recording_mic_basic", "art_easel_basic", "studio_shelf_basic", "sofa_basic" }.All(furniture.Contains) &&
                !furniture.Contains("cushion_basic"), string.Join(",", furniture));
            var outside = outdoor.Objects.Select(o => o.DefinitionId).ToList();
            Check("outdoors: the bench, the well, the sign, the trees and the house are there",
                new[] { "bench_basic", "well_basic", "sign_basic", "tree_basic", "house_main" }.All(outside.Contains));
            Check("crow: present outdoors in both views (2D sprite and 3D model share one state); its talk is カァ。",
                c.Crow != null && c.CrowView != null && PocCrow.TalkMessage == "カァ。");
            Check("AUTO: on by default, starts after 60 s without input", c.Auto.AutoEnabled && Mathf.Approximately(AutoModeController.IdleSeconds, 60f));
            Check("World Time: 10 minutes morning, 10 minutes night (real time, kept across starts by its key)",
                WorldTimeCycle.PhaseMilliseconds == 600000L && WorldTimeCycle.CycleMilliseconds == 1200000L);
            var steps = c.LifeLog.TotalSteps;
            C6.Step(c, C6.FreeDirection(c));
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            C6.Settle(c, 0.3f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            C6.Settle(c, 0.3f);
            Check("Life Log: one step counts once, a view switch adds none", c.LifeLog.TotalSteps == steps + 1);
        }

        // ---------------------------------------------------------------- save: fully damaged data back through an import

        private static string Export(C6.Ctx c) => c.Save.ExportText();

        private static void ExpireHudMessage(C6.Ctx c)
        {
            var field = typeof(GameHud).GetField("message", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((TimedMessage)field.GetValue(c.Hud)).Show(null, float.MinValue, 0f);
        }

        private static string ImportThroughMenu(C6.Ctx c, string text)
        {
            var clock = C6.Clock;
            c.Save.Menu.Open(clock);
            var reached = c.Save.Menu.IsOpen && c.Save.Menu.Buttons.Contains("インポート");
            c.Browser.NextImport = text;
            c.Menu.PressPageButton(1, clock);
            C6.Frame(c, 1f / 60f, Vector2Int.zero);
            var stage = (reached ? "reached>" : "") + c.Save.Menu.Current;
            if (c.Save.Menu.Current == HarukaSaveSystem.SaveMenu.Stage.ImportConfirm)
            {
                c.Menu.PressPageButton(0, C6.Clock);
                c.LifeLog.AdvanceClock(C6.Clock, false);
                C6.Frame(c, 1f / 60f, Vector2Int.zero);
                stage += ">" + c.Save.Menu.Current;
            }
            c.Menu.SetOpen(false, C6.Clock);
            return stage;
        }

        private static void SaveChecks()
        {
            Category("save");
            Check("keys and format unchanged: HALKA_WORLD_life_log_v1, HALKA_WORLD_time_cycle_anchor_v1, harukaworld-save schema 1, folder harukaworld_save",
                LifeLogController.SaveKey == "HALKA_WORLD_life_log_v1" && WorldTimeController.SaveKey == "HALKA_WORLD_time_cycle_anchor_v1" &&
                HarukaSaveData.Format == "harukaworld-save" && HarukaSaveData.SchemaVersion == 1 && HarukaSaveSystem.FolderName == "harukaworld_save");

            // A good save first: walked, in the house, 3D, AUTO off; exported.
            C6.SetLegacy("v1|30|40", "1750000000000");
            var good = new HarukaMemoryStorage();
            var c = C6.Start(good);
            C6.Settle(c, 1.2f);
            c.Session.LoadMapForCheck(true, new Vector2Int(2, 0), FacingDirection.Up);
            C6.Settle(c, 0.2f);
            C6.Step(c, Vector2Int.down, 0.2f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Auto.SetAutoEnabled(false, C6.Clock);
            C6.Settle(c, 1.5f);
            var exported = Export(c);
            var expect = c.Save.Capture();
            Check("export: a valid file of the current state", exported != null &&
                HarukaSaveData.TryRead(exported, out var exportedData, out _) == HarukaSaveReadStatus.Valid &&
                exportedData.MapId == "halka_house" && exportedData.TotalSteps == expect.TotalSteps);

            // Every snapshot damaged, no old key: the start notices, saves nothing.
            var lost = new HarukaMemoryStorage();
            foreach (var name in HarukaSnapshotStore.SlotNames) lost.Files[name] = "{ \"format\": \"harukaworld-save\", broken";
            lost.Files["head.json"] = "{ \"revision\": 9 }\n";
            var brokenCopy = lost.Files["snapshot_1.json"];
            C6.SetLegacy(null, "1750000000000");
            c = C6.Start(lost);
            C6.Settle(c, 1f);
            Check("all damaged, no old key: the start sees it, stops saving and tells the player (よみこめませんでした)",
                c.Save.SavingStopped && c.Save.LoadSource.StartsWith("unreadable") && C6.Shown(c) == HarukaSaveSystem.MessageUnreadable,
                c.Save.LoadSource + " / " + C6.Shown(c));
            // Play a little, then leave the page (the Life Log's own save and the page-hidden save).
            C6.Step(c, C6.FreeDirection(c));
            C6.Settle(c, 2f);
            c.LifeLog.Save();
            c.Save.OnPageHidden("");
            Check("all damaged: playing on writes nothing over them; the Life Log does not write the old key either (Phase 7 fix)",
                HarukaSnapshotStore.SlotNames.All(n => lost.Files[n] == brokenCopy) && c.Save.Writes == 0 &&
                C6.LegacyLife() == "" && c.LifeLog.PersistenceSuspended, $"old key '{C6.LegacyLife()}', writes {c.Save.Writes}");
            c = C6.Start(lost);
            C6.Settle(c, 1f);
            Check("all damaged, started again: still unreadable and not saving (no near-empty save is begun over the damaged files)",
                c.Save.SavingStopped && c.Save.LoadSource.StartsWith("unreadable") && c.Save.Store.Scan().Latest == null &&
                HarukaSnapshotStore.SlotNames.All(n => lost.Files[n] == brokenCopy), c.Save.LoadSource);
            // The notice (about 6 s; the menu never opens over a message, as before), then the save
            // page is reachable and the exported file brings the save back.
            var blocked = c.Hud.HasActiveMessage;
            c.Menu.SetOpen(true, C6.Clock);
            var openedOverNotice = c.Menu.IsOpen;
            var shows = 0;
            for (var i = 0; i < 4; i++)
            {
                // The HUD's 2 s timer reads real time, which stands still in a batch run: expire it.
                if (c.Hud.HasActiveMessage) shows++;
                ExpireHudMessage(c);
                C6.Settle(c, 0.2f);
            }
            Check("all damaged: the notice shows 3 times (about 6 s), the menu waits for it, then the menu can be opened",
                blocked && !openedOverNotice && shows == 3 && !c.Hud.HasActiveMessage && c.Save.PendingMessage == null, $"shows {shows}");
            var stage = ImportThroughMenu(c, exported);
            Check("all damaged: メニュー → セーブデータ → インポート is reachable; the file is confirmed and restored",
                stage == "reached>ImportConfirm>ImportDone", stage);
            var after = c.Save.Capture();
            var latest = c.Save.Store.Scan().Latest;
            Check("after the import: a valid snapshot of the file's place, view, AUTO and steps; saving goes on; the old key matches",
                !c.Save.SavingStopped && !c.LifeLog.PersistenceSuspended && latest != null && latest.MapId == "halka_house" &&
                latest.CellX == expect.CellX && latest.CellY == expect.CellY && latest.ViewMode == "3D" && !latest.AutoEnabled &&
                latest.TotalSteps == expect.TotalSteps && after.TotalSteps == expect.TotalSteps &&
                C6.LegacyLife().StartsWith("v1|" + expect.TotalSteps + "|"), $"{HarukaSaveSystem.Describe(after)} / {C6.LegacyLife()}");
            Check("the damaged files were copied aside before their slots were used (nothing deleted)",
                lost.Files.Where(f => f.Key.StartsWith("damaged_")).Any(f => f.Value == brokenCopy));
            c = C6.Start(lost);
            C6.Settle(c, 1f);
            Check("reload after the import: resumes in the house, 3D, AUTO off, the steps of the file",
                c.Save.LoadSource == "snapshot" && c.Session.World.MapId == "halka_house" && c.Session.Cell == new Vector2Int(expect.CellX, expect.CellY) &&
                c.Switcher.Mode == PocViewMode.ThirdPerson3D && !c.Auto.AutoEnabled && c.LifeLog.TotalSteps == expect.TotalSteps, c.Save.LoadSource);
            var writes = c.Save.Writes;
            C6.Step(c, C6.FreeDirection(c));
            C6.Settle(c, 3.5f);
            Check("and normal play saves again (a step is written)", c.Save.Writes > writes && c.Save.Store.Scan().Latest.TotalSteps == expect.TotalSteps + 1);

            // Other stops: a newer save format, another tab. The old key is left alone in both.
            var newer = new HarukaMemoryStorage();
            var future = expect.Copy();
            future.Schema = 2;
            future.Seal();
            newer.Files["snapshot_1.json"] = future.ToJson();
            C6.SetLegacy("v1|5|6", "1750000000000");
            c = C6.Start(newer);
            C6.Step(c, C6.FreeDirection(c));
            c.LifeLog.Save();
            Check("a newer save format: not saving, and the old key keeps its value (v1|5|6)",
                c.Save.SavingStopped && C6.LegacyLife() == "v1|5|6" && newer.Files["snapshot_1.json"] == future.ToJson(), C6.LegacyLife());
            C6.SetLegacy("v1|10|10", "1750000000000");
            var shared = new HarukaMemoryStorage();
            c = C6.Start(shared);
            C6.Settle(c, 1.2f);
            c.Browser.Marker = c.Save.KnownRevision + 5; // another tab saved later
            C6.Step(c, C6.FreeDirection(c));
            C6.Settle(c, 3.5f);
            var keyAfterStop = C6.LegacyLife();
            C6.Step(c, C6.FreeDirection(c));
            c.LifeLog.Save();
            Check("another tab saved later: this tab stops and its Life Log no longer writes the old key either",
                c.Save.SavingStopped && c.Save.StopReason.StartsWith("another tab") && C6.LegacyLife() == keyAfterStop, c.Save.StopReason);
            Check("rollback support: every save also writes the old keys (ver2.9 reads v1|steps|seconds)",
                C6.LegacyLife().StartsWith("v1|") && PlayerPrefs.HasKey(WorldTimeController.SaveKey));
        }

        // ---------------------------------------------------------------- the ver3.0 public page (prepared, not published)

        private static void PageChecks()
        {
            Category("page");
            var kit = "tools/poc2d3d/rc3/";
            var page = ReadRepo(kit + "release/halkaworld/index.html");
            var css = ReadRepo(kit + "release/halkaworld/game-page.css");
            var staging = ReadRepo(kit + "staging/halkaworld/index.html");
            var current = ReadRepo("halkaworld/index.html");
            var currentCss = ReadRepo("halkaworld/game-page.css");
            Check("release page: the same URL (canonical https://halkaclub.com/halkaworld/), the game at webgl/ with v=3.0 and the build id",
                page.Contains("<link rel=\"canonical\" href=\"https://halkaclub.com/halkaworld/\" />") &&
                page.Contains("<iframe src=\"webgl/?v=3.0&build=__BUILD__\" title=\"はるかわーるど\" allow=\"fullscreen\" loading=\"eager\"></iframe>"));
            Check("release page: the name はるかわーるど everywhere it is shown, no HALKA WORLD left (title, OGP, tape, header, heading)",
                page.Contains("<title>はるかわーるど | HALKA</title>") && page.Contains("content=\"はるかわーるど | HALKA\"") &&
                page.Contains("<h1 id=\"game-title\">はるかわーるど</h1>") && !page.Contains("HALKA WORLD"));
            Check("release page: version ver3.0 shown under the title", page.Contains("<p class=\"game-current-version\">ver3.0</p>"));
            Check("release page: 全画面にする is a page button under the game frame; full screen for the frame, page filling with もどす where it cannot",
                page.Contains("id=\"game-fullscreen-button\" hidden>全画面にする</button>") &&
                page.IndexOf("game-fullscreen-button", StringComparison.Ordinal) > page.IndexOf("<iframe", StringComparison.Ordinal) &&
                page.Contains("frame.requestFullscreen || frame.webkitRequestFullscreen") && page.Contains("if (!real) { fill(true, false); return; }") &&
                page.Contains("id=\"game-fill-back\">もどす</button>") && css.Contains("body.game-filled .unity-frame {"));
            Check("release page: the guide (PC, phone, 2D / 3D, full screen, save, backup) in plain words, and the notes",
                new[] { "<dt>パソコン</dt>", "<dt>スマホ</dt>", "<dt>2D と 3D</dt>", "<dt>全画面</dt>", "<dt>セーブ</dt>", "<dt>バックアップ</dt>",
                    "WASD・矢印キー", "十字キー", "縦向きでも横向きでも", "前回いた場所から", "エクスポート", "インポート", "プライベートブラウズ" }.All(page.Contains) &&
                page.Contains("クリックすると、調べたり座ったり") && !page.Contains("A キー")); // on a PC the A key walks left
            var oldEntries = Regex.Matches(current, "<h3>(ver[0-9.]+) <time datetime=\"([0-9-]+)\">").Cast<Match>().Select(m => m.Groups[1].Value + " " + m.Groups[2].Value).ToList();
            var newEntries = Regex.Matches(page, "<h3>(ver[0-9.]+) <time datetime=\"([0-9-]+|__RELEASE_DATE__)\">").Cast<Match>().Select(m => m.Groups[1].Value + " " + m.Groups[2].Value).ToList();
            Check("update history: every published entry kept in order (ver2.9 … ver1.0), each once; ver3.0 added once at the top",
                oldEntries.Count >= 20 && newEntries.Count == oldEntries.Count + 1 && newEntries[0] == "ver3.0 __RELEASE_DATE__" &&
                newEntries.Skip(1).SequenceEqual(oldEntries) && newEntries.Distinct().Count() == newEntries.Count, $"{oldEntries.Count} → {newEntries.Count}");
            Check("update history: ver3.0 sits in the existing \"ver2.1 ～ ver3.0\" group; no provisional date (left for the day of publishing)",
                page.IndexOf("ver3.0 <time", StringComparison.Ordinal) > page.IndexOf("<summary>ver2.1 ～ ver3.0</summary>", StringComparison.Ordinal) &&
                page.IndexOf("ver3.0 <time", StringComparison.Ordinal) < page.IndexOf("ver2.9 <time", StringComparison.Ordinal) &&
                Regex.Matches(page, "<summary>").Count == Regex.Matches(current, "<summary>").Count && page.Contains("__RELEASE_DATE__"));
            var oldTexts = Regex.Matches(current, "<article class=\"update-entry\">[\\s\\S]*?</article>").Cast<Match>().Select(m => m.Value).ToList();
            Check("update history: the older entries' text is unchanged", oldTexts.Count >= 20 && oldTexts.All(page.Contains));
            Check("release page: analytics kept, the site stylesheet unchanged (../style.css?v=1), game-page.css with a new version (cache)",
                page.Contains("static.cloudflareinsights.com/beacon.min.js") && page.Contains("<link rel=\"stylesheet\" href=\"../style.css?v=1\" />") &&
                page.Contains("<link rel=\"stylesheet\" href=\"game-page.css?v=3.0\" />") && !page.Contains("noindex"));
            Check("release CSS: the ver2.9 game-page.css kept as it is, the ver3.0 part added after it (no shared CSS touched)",
                currentCss.Length > 0 && css.StartsWith(currentCss.TrimEnd('\n')) && css.Contains(".game-guide-list"));
            Check("staging page: the release page, not indexed, no analytics, a staging note, the date shown as 公開日",
                staging.Contains("noindex") && !staging.Contains("cloudflareinsights") && staging.Contains("rc3-staging-note") &&
                staging.Contains("<iframe src=\"webgl/?v=3.0&build=__BUILD__\"") && staging.Contains("<time datetime=\"公開日\">公開日</time>"));
            Check("the page generator writes these pages from the public page (make_pages.py --release-date fills the date on the day)",
                ReadRepo(kit + "make_pages.py").Contains("--release-date"));
        }

        // ---------------------------------------------------------------- what must not change before publishing

        private static void ReleaseChecks()
        {
            Category("release");
            Check("the public ver2.9 page and WebGL build in this branch are unchanged (halkaworld/ as published)",
                ChangedSince(PublicBase, "halkaworld") == "" && ReadRepo("halkaworld/index.html").Contains("<p class=\"game-current-version\">ver2.9</p>"));
            Check("no other page of the site is changed in this branch (top page, みねはうす, shared CSS, sitemap, assets)",
                ChangedSince(PublicBase, "index.html style.css script.js sitemap.xml robots.txt CNAME favicon.png youtube-thumbnail.js " +
                    "assets club commission game harukaijiri request utamaze works") == "");
            // 2026-10-20: the Player sprites (Content/Character) were redrawn by the user on purpose;
            // they are pinned by the SHA-256 check in the model category instead.
            Check("maps, map JSON, catalogs and world sprites unchanged since Phase 6 (Content/Maps, World, Audio, Fonts)",
                ChangedSince(Phase6Final, "unity-project/Assets/Content/Maps unity-project/Assets/Content/World unity-project/Assets/Content/Audio unity-project/Assets/Content/Fonts") == "");
            var catalogBefore = Git("show " + Phase6Final + ":unity-project/Assets/POC2D3D/Data/POC_PresentationCatalog.asset").output.Replace("\r\n", "\n");
            var catalogNow = ReadRepo("unity-project/Assets/POC2D3D/Data/POC_PresentationCatalog.asset");
            var bindingsBefore = Regex.Matches(catalogBefore, "definitionId: (\\w+)").Cast<Match>().Select(m => m.Value).ToList();
            var bindingsNow = Regex.Matches(catalogNow, "definitionId: (\\w+)").Cast<Match>().Select(m => m.Value).ToList();
            Check("presentation catalog: the same bindings and models as Phase 6; only the player's placeholder flag changed",
                bindingsBefore.Count > 10 && bindingsBefore.SequenceEqual(bindingsNow) &&
                catalogBefore.Replace("    placeholder: 1\n    sprites:", "    placeholder: 0\n    sprites:") == catalogNow,
                $"{bindingsBefore.Count} bindings");
            Check("the public path is kept: the build goes to halkaworld/webgl (the URL /halkaworld/webgl/ and the save location stay)",
                PocBuilder.ReleaseCandidateGamePath == "halkaworld/webgl" && PocBuilder.ReleaseCandidateFolderName == "HARUKA_WORLD_ver3.0_RC3" &&
                !PocBuilder.ReleaseCandidatePath.StartsWith(Root, StringComparison.OrdinalIgnoreCase));
            var rc2Info = Path.Combine(Root, "..", "HARUKA_WORLD_ver3.0_RC2", "BUILD_INFO.txt");
            Check("RC1 and RC2 stay as they are (RC2 still holds its own build); RC3 has its own folder",
                Directory.Exists(Path.Combine(Root, "..", "HARUKA_WORLD_ver3.0_RC1")) && File.Exists(rc2Info) &&
                File.ReadAllText(rc2Info).Contains("Release Candidate 2") && File.ReadAllText(rc2Info).Contains("45c5cb99c7597df3dbadce5c933cb442") &&
                !PocBuilder.ReleaseCandidatePath.EndsWith("RC1") && !PocBuilder.ReleaseCandidatePath.EndsWith("RC2"));
            var rollback = ReadRepo("tools/poc2d3d/rc3/ROLLBACK_PROCEDURE_RC3.md");
            Check("rollback procedure: git revert (no reset, no force push), the save data kept (no IndexedDB clearing)",
                rollback.Contains("git revert") && rollback.Contains("force push") && rollback.Contains("IndexedDB") &&
                rollback.Contains("HALKA_WORLD_life_log_v1"));
            var publish = ReadRepo("tools/poc2d3d/rc3/PUBLISH_PROCEDURE_RC3.md");
            Check("publish procedure: the user's approval first, the latest origin/main, a merge, the date on the day, cache, checks after",
                publish.Contains("承認") && publish.Contains("origin/main") && publish.Contains("--release-date") &&
                publish.Contains("キャッシュ") && publish.Contains("merge"));
            // Found in the Phase 7 rehearsal: a page cached for up to 10 minutes loads the build it
            // names; with its files gone the game stops at "Unknown data format".
            Check("publish and rollback keep the other build's files while caches turn over (both sets side by side)",
                publish.Contains("消さずに残す") && publish.Contains("Unknown data format") &&
                rollback.Contains("ver3.0 の Build ファイル（4つ）は消さずに残す"));
        }
    }
}
