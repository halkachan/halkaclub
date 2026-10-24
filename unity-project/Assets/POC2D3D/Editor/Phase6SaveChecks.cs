using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Halka.Game.Input;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど Phase 6: the save system. Each "start" builds the game scene anew over the
    // same stored files (memory) and PlayerPrefs keys, in the game's own start order, so a
    // restart is a real restart. The player's Editor PlayerPrefs are put back afterwards.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase6SaveChecks.RunBatch
    public static class Phase6SaveChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const int ScreenW = 1280, ScreenH = 720;
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static int passes;
        private static string category = "";
        private static float clock;
        private static GameObject holder;
        // Phase 7: the release checks start the game with this harness too.
        internal static float Clock => clock;
        internal static void DisposeHarness()
        {
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
            holder = null;
        }

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
            UnityEngine.Random.InitState(6606);
            try
            {
                SchemaChecks();
                BackupChecks();
                MigrationChecks();
                AutoSaveChecks();
                ResumeChecks();
                ExportChecks();
                ImportChecks();
                MenuChecks();
                TabChecks();
                GameplayChecks();
                BrowserChecks();
                ProtectionChecks();
            }
            finally
            {
                if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Random.state = randomState;
                foreach (var pair in saved)
                {
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                    else PlayerPrefs.SetString(pair.Key, pair.Value);
                }
                PlayerPrefs.Save();
            }
            foreach (var failure in failures) Debug.LogError("PHASE6 FAIL " + failure);
            Debug.Log("HARUKA Phase 6 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA Phase 6 save checks: {passes} passed, {failures.Count} failed");
            return failures.Count == 0;
        }

        private static void Category(string name) => category = name;

        private static void Check(string name, bool condition, string detail = "")
        {
            perCategory[category] = perCategory.TryGetValue(category, out var n) ? n + 1 : 1;
            if (condition) { passes++; Debug.Log("PHASE6 PASS [" + category + "] " + name + (detail.Length > 0 ? " | " + detail : "")); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- data helpers

        internal static HarukaSaveData Sample(long revision = 0) => new HarukaSaveData
        {
            GameVersion = "ver3.0 RC2", SavedAtUtc = "2026-10-18T03:04:05Z", Revision = revision,
            TotalSteps = 1234, TotalPlaySeconds = 5678, TimeAnchorUnixMs = 1760000000000L, AutoEnabled = false,
            ViewMode = HarukaSaveData.View3D, MapId = "halka_house", CellX = 2, CellY = 1, Facing = "up",
        };

        internal static string Sealed(HarukaSaveData data)
        {
            var copy = data.Copy();
            copy.Seal();
            return copy.ToJson();
        }

        private static HarukaSaveReadStatus Status(string text) => HarukaSaveData.TryRead(text, out _, out _);

        // ---------------------------------------------------------------- schema

        private static void SchemaChecks()
        {
            Category("schema");
            var data = Sample(7);
            var text = Sealed(data);
            var status = HarukaSaveData.TryRead(text, out var back, out var problem);
            Check("schema 1: written and read back with every field (format, schema, version, time, revision, life log, anchor, settings, place, checksum)",
                status == HarukaSaveReadStatus.Valid && back.Schema == 1 && back.GameVersion == data.GameVersion &&
                back.SavedAtUtc == data.SavedAtUtc && back.Revision == 7 && back.TotalSteps == 1234 && back.TotalPlaySeconds == 5678 &&
                back.TimeAnchorUnixMs == data.TimeAnchorUnixMs && !back.AutoEnabled && back.ViewMode == "3D" &&
                back.MapId == "halka_house" && back.CellX == 2 && back.CellY == 1 && back.Facing == "up" && back.Checksum.StartsWith("crc32:"),
                problem ?? "");
            Check("schema 1: the text names its format and schema, and carries a checksum",
                text.Contains("\"format\": \"harukaworld-save\"") && text.Contains("\"schemaVersion\": 1") && text.Contains("\"checksum\": \"crc32:"));
            Check("checksum: an edited value (steps) is detected as damage",
                Status(text.Replace("\"totalSteps\": 1234", "\"totalSteps\": 99999")) == HarukaSaveReadStatus.Corrupt);
            Check("damage: a file cut off midway is not read",
                Status(text.Substring(0, text.Length / 2)) == HarukaSaveReadStatus.Corrupt);
            Check("damage: not JSON / an empty text",
                Status("this is not json") == HarukaSaveReadStatus.Corrupt && Status("") == HarukaSaveReadStatus.Missing);
            Check("fields: a missing field is rejected (player)",
                Status(text.Replace("\"player\"", "\"playr\"")) == HarukaSaveReadStatus.Corrupt);
            var negative = Sample(1); negative.TotalSteps = -5;
            var badView = Sample(1); badView.ViewMode = "4D";
            var badFacing = Sample(1); badFacing.Facing = "north";
            var badMap = Sample(1); badMap.MapId = "../etc";
            var badAnchor = Sample(1); badAnchor.TimeAnchorUnixMs = 0;
            Check("values: negative steps, an unknown view, facing or map ID, a zero anchor are rejected",
                Status(Sealed(negative)) == HarukaSaveReadStatus.Corrupt && Status(Sealed(badView)) == HarukaSaveReadStatus.Corrupt &&
                Status(Sealed(badFacing)) == HarukaSaveReadStatus.Corrupt && Status(Sealed(badMap)) == HarukaSaveReadStatus.Corrupt &&
                Status(Sealed(badAnchor)) == HarukaSaveReadStatus.Corrupt);
            Check("size: a text over 64 KB is rejected",
                Status(text.Replace("\"extensions\": {}", "\"extensions\": {\"pad\": \"" + new string('x', 70000) + "\"}")) == HarukaSaveReadStatus.Corrupt);
            Check("newer schema: a schemaVersion 2 file is reported (never read as 1)",
                Status(text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2")) == HarukaSaveReadStatus.NewerSchema);
            Check("forward fields: an unknown extra field of schema 1 is ignored (the values and checksum still hold)",
                Status(text.Replace("\"extensions\": {}", "\"extensions\": {\"items\": [1, 2, 3]}, \"futureField\": true")) == HarukaSaveReadStatus.Valid);
            Check("JSON reader: duplicate keys, deep nesting and trailing text are rejected; 64-bit integers stay exact",
                !HarukaSaveJson.TryParse("{\"a\":1,\"a\":2}", out _, out _) &&
                !HarukaSaveJson.TryParse(new string('[', 40) + new string(']', 40), out _, out _) &&
                !HarukaSaveJson.TryParse("{} x", out _, out _) &&
                HarukaSaveJson.TryParse("{\"n\": 9007199254740993}", out var big, out _) &&
                (long)((Dictionary<string, object>)big)["n"] == 9007199254740993L);
            Check("UTF-8: the text is plain UTF-8 without a byte order mark and round-trips Japanese",
                Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)) == text && !text.StartsWith("\uFEFF", StringComparison.Ordinal) &&
                HarukaSaveJson.TryParse("{\"s\": \"はるかわーるど\"}", out var jp, out _) && (string)((Dictionary<string, object>)jp)["s"] == "はるかわーるど");
        }

        // ---------------------------------------------------------------- backups

        private static void BackupChecks()
        {
            Category("backup");
            var memory = new HarukaMemoryStorage();
            var store = new HarukaSnapshotStore(memory);
            var first = store.Write(Sample());
            Check("first save: a good snapshot, head revision 1",
                first != null && first.Revision == 1 && store.Scan().Latest.Revision == 1 && store.Scan().HeadRevision == 1);
            var latestKept = true;
            for (var i = 2; i <= 6; i++)
            {
                var before = store.Scan().Latest;
                var beforeName = store.Scan().Slots.First(s => s.Data != null && s.Data.Revision == before.Revision).Name;
                var data = Sample(); data.TotalSteps = 1000 + i;
                store.Write(data);
                latestKept &= HarukaSaveData.TryRead(memory.Read(beforeName), out var check, out _) == HarukaSaveReadStatus.Valid &&
                    check.Revision == before.Revision && store.Scan().Latest.Revision == before.Revision + 1;
            }
            var revisions = store.Scan().Slots.Where(s => s.Status == HarukaSaveReadStatus.Valid).Select(s => s.Data.Revision).OrderBy(r => r).ToArray();
            Check("three generations: after 6 saves the snapshots are revisions 4, 5, 6", revisions.SequenceEqual(new long[] { 4, 5, 6 }),
                string.Join(",", revisions));
            Check("a new save never goes over the newest good snapshot", latestKept);
            var latestName = store.Scan().Slots.First(s => s.Data != null && s.Data.Revision == 6).Name;
            var original = memory.Files[latestName];
            memory.Files[latestName] = original.Substring(0, original.Length / 3);
            var scan = store.Scan();
            Check("newest damaged: the previous generation (5) loads, and the scan says it is a recovery",
                scan.Latest.Revision == 5 && scan.RecoveredFromBackup && scan.CorruptCount == 1);
            var damagedText = memory.Files[latestName];
            var afterDamage = store.Write(Sample());
            Check("a damaged snapshot is copied aside (damaged_1.json) before its slot is reused",
                afterDamage != null && afterDamage.Revision == 7 && memory.Read("damaged_1.json") == damagedText);
            memory.TruncateNextWriteTo = 40;
            var cut = store.Write(Sample());
            Check("a save cut off midway: not accepted, the newest good snapshot (7) is still loaded",
                cut == null && store.Scan().Latest.Revision == 7);
            memory.FailWrites = true;
            var failed = store.Write(Sample());
            memory.FailWrites = false;
            Check("a storage that refuses to write: nothing changes", failed == null && store.Scan().Latest.Revision == 7);
            var allBad = new HarukaMemoryStorage();
            foreach (var name in HarukaSnapshotStore.SlotNames) allBad.Files[name] = "{ broken";
            var badScan = new HarukaSnapshotStore(allBad).Scan();
            Check("every generation damaged: nothing loads (Latest null), all three counted as damaged",
                badScan.Latest == null && badScan.CorruptCount == 3);
            var newer = new HarukaMemoryStorage();
            var newerStore = new HarukaSnapshotStore(newer);
            newerStore.Write(Sample());
            newer.Files["snapshot_2.json"] = Sealed(Sample(9)).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2");
            var newerText = newer.Files["snapshot_2.json"];
            var refused = newerStore.Write(Sample());
            Check("a newer schema stored: no save is written and the newer file is untouched",
                refused == null && newer.Files["snapshot_2.json"] == newerText && newerStore.Scan().HasNewerSchema);
            var recovery = new HarukaSnapshotStore(new HarukaMemoryStorage());
            Check("import recovery copy: written, read back, separate from the three generations",
                recovery.WriteRecovery(Sample(3)) && recovery.ReadRecovery() != null && recovery.Scan().Latest == null);
        }

        // ---------------------------------------------------------------- boot / play harness

        internal sealed class Ctx
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
            public AutoModeController Auto;
            public LifeLogController LifeLog;
            public GameMenuController Menu;
            public GameHud Hud;
            public WorldTimeController Clock;
            public HarukaSaveSystem Save;
            public HarukaMemoryStorage Storage;
            public HarukaEditorSaveBrowser Browser;
        }

        internal static void SetLegacy(string life, string anchor)
        {
            if (life == null) PlayerPrefs.DeleteKey(LifeLogController.SaveKey); else PlayerPrefs.SetString(LifeLogController.SaveKey, life);
            if (anchor == null) PlayerPrefs.DeleteKey(WorldTimeController.SaveKey); else PlayerPrefs.SetString(WorldTimeController.SaveKey, anchor);
            PlayerPrefs.Save();
        }

        // The game's start, in its own order (WorldTime -100, LifeLog -50, save -45, AUTO -40,
        // session -30, gameplay -25, then OnEnable / Start), over the given storage.
        // ver3.2: configure (optional) runs on the built session before anything starts (the
        // ver3.2 checks put a test outdoor world there).
        internal static Ctx Start(HarukaMemoryStorage storage, HarukaEditorSaveBrowser browser = null,
            Action<SharedWorldSession> configure = null)
        {
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var screen = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ScreenMaterialPath);
            var root = PocSceneBuilder.BuildSceneContent(catalog, screen);
            var c = new Ctx
            {
                Session = root.GetComponent<SharedWorldSession>(),
                Presenter = root.GetComponent<PocWorldPresenter>(),
                Switcher = root.GetComponent<PocViewSwitcher>(),
                Time = root.GetComponent<PocTimeOfDay>(),
                Input = root.GetComponent<PocInput>(),
                Gameplay = root.GetComponent<PocGameplay>(),
                Auto = root.GetComponent<AutoModeController>(),
                LifeLog = root.GetComponent<LifeLogController>(),
                Menu = root.GetComponent<GameMenuController>(),
                Hud = root.GetComponent<GameHud>(),
                Clock = root.GetComponent<WorldTimeController>(),
                Save = root.GetComponentInChildren<HarukaSaveSystem>(true),
                Pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include),
                Third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include),
                Actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include),
                Crow = UnityEngine.Object.FindAnyObjectByType<PocCrow>(FindObjectsInactive.Include),
                CrowView = UnityEngine.Object.FindAnyObjectByType<PocCrowView3D>(FindObjectsInactive.Include),
                Storage = storage,
                Browser = browser ?? new HarukaEditorSaveBrowser(),
            };
            // The game's own clocks (AUTO's idle time, the Life Log's play time) read the same time.
            clock = UnityEngine.Time.unscaledTime;
            configure?.Invoke(c.Session);
            Invoke(c.Clock, "Awake");
            Invoke(c.LifeLog, "Awake");
            c.Save.UseStorage(storage, c.Browser);
            c.Save.Boot();
            Invoke(c.Auto, "Awake");
            c.Session.Initialize();
            c.Gameplay.Connect();
            Invoke(c.Presenter, "OnEnable");
            c.Presenter.Rebuild(c.Session.World);
            Invoke(c.Third, "OnEnable");
            Invoke(c.Pixel, "OnEnable");
            Invoke(c.LifeLog, "Start");
            Invoke(c.Switcher, "Start");
            if (c.Switcher.Mode == PocViewMode.ThirdPerson3D) c.Third.SnapToTarget(); else c.Pixel.SnapToTarget();
            c.Save.StartPlay(clock);
            c.LifeLog.AdvanceClock(clock, false);
            c.Time.Override = PocPhaseOverride.Morning;
            holder = new GameObject("Phase 6 check holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            Frame(c, 0.016f, Vector2Int.zero);
            return c;
        }

        internal static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, Hidden)?.Invoke(target, null);

        // One frame of play: input, the session, AUTO (optional), play time, the views, the save.
        internal static void Frame(Ctx c, float dt, Vector2Int raw, bool auto = false)
        {
            clock += dt;
            c.Input.SetRaw(raw, clock);
            c.Session.Tick(dt, c.Session.InputSuspended ? Vector2Int.zero : c.Input.ResolveDirection(c.Session));
            if (auto) c.Auto.Tick(clock, true);
            c.LifeLog.AdvanceClock(clock, true);
            c.Actor.Apply(dt);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(dt, ScreenW, ScreenH);
            else { c.Third.ViewCamera.aspect = ScreenW / (float)ScreenH; c.Third.UpdateView(dt, clock); }
            c.Save.Tick(clock);
        }

        internal static void Settle(Ctx c, float seconds, bool auto = false)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, auto);
        }

        internal static void Release(Ctx c)
        {
            c.Input.SetRaw(Vector2Int.zero, clock);
            c.Input.Resolve(Vector2Int.zero, false, FacingDirection.Up);
        }

        private static Vector2Int RawFor(Ctx c, Vector2Int world)
        {
            if (c.Switcher.Mode != PocViewMode.ThirdPerson3D) return world;
            foreach (var raw in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                if (GridSpace3D.CameraRelative(raw, GridSpace3D.FacingYaw(c.Switcher.CameraHeading)) == world) return raw;
            return world;
        }

        // One step in a world direction (a tap), then the given settle time.
        internal static void Step(Ctx c, Vector2Int world, float settle = 0.3f)
        {
            Release(c);
            var raw = RawFor(c, world);
            for (var i = 0; i < 4; i++) Frame(c, 1f / 60f, raw);
            Release(c);
            Settle(c, settle);
        }

        internal static HarukaSaveData Latest(Ctx c) => c.Save.Store.Scan().Latest;

        internal static Vector2Int FreeNeighbour(Ctx c, out Vector2Int direction)
        {
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var cell = c.Session.Cell + d;
                if (c.Session.World.CanEnter(cell) && !HarukaSaveSystem.IsTransitionCell(c.Session.World, cell) && c.Session.CanPlayerEnter(cell))
                { direction = d; return cell; }
            }
            direction = Vector2Int.zero;
            return c.Session.Cell;
        }

        // The message GameHud shows now (its own timer reads real time).
        internal static string Shown(Ctx c) =>
            ((TimedMessage)typeof(GameHud).GetField("message", Hidden).GetValue(c.Hud)).TextAt(UnityEngine.Time.unscaledTime);

        internal static string LegacyLife() => PlayerPrefs.GetString(LifeLogController.SaveKey, "");

        // ---------------------------------------------------------------- migration

        private static void MigrationChecks()
        {
            Category("migration");
            SetLegacy(null, null);
            var c = Start(new HarukaMemoryStorage());
            var spawnOk = c.Session.StartMap.TryGetEntitySpawn("player_main", out var spawn);
            Check("new player: the first map's Player Start, 2D, AUTO as the scene's default (ON), 0 steps",
                spawnOk && c.Session.World.MapId == "first_field" && c.Session.Cell == spawn.Cell && c.Switcher.Mode == PocViewMode.PixelView2D &&
                c.Auto.AutoEnabled == c.Auto.EnabledByDefault && c.Auto.AutoEnabled && c.LifeLog.TotalSteps == 0 && c.Save.LoadSource == "new",
                c.Save.LoadSource);
            Settle(c, 1.2f);
            Check("new player: the first snapshot is written within about a second", Latest(c) != null && c.Save.Writes == 1, c.Save.LastWriteReason);

            SetLegacy("v1|1234|5678", "1750000000000");
            c = Start(new HarukaMemoryStorage());
            Check("ver2.9 keys only: steps 1,234 and 5,678 s carried over, the anchor kept, start place, 2D",
                c.LifeLog.TotalSteps == 1234 && c.LifeLog.TotalPlaySeconds == 5678 && c.Clock.AnchorMilliseconds == 1750000000000L &&
                c.Session.World.MapId == "first_field" && c.Switcher.Mode == PocViewMode.PixelView2D && c.Save.LoadSource == "ver2.9 keys",
                c.Save.LoadSource);
            Settle(c, 1.2f);
            var migrated = Latest(c);
            Check("ver2.9 keys: a new-format snapshot is written with those values, and the old keys keep them",
                migrated != null && migrated.TotalSteps == 1234 && migrated.TimeAnchorUnixMs == 1750000000000L &&
                LegacyLife().StartsWith("v1|1234|") && PlayerPrefs.GetString(WorldTimeController.SaveKey) == "1750000000000");

            // Both: the snapshot is the authority.
            var both = new HarukaMemoryStorage();
            var snap = Sample(); snap.TotalSteps = 500; snap.TotalPlaySeconds = 100; snap.MapId = "first_field"; snap.CellX = -1; snap.CellY = 0;
            new HarukaSnapshotStore(both).Write(snap);
            SetLegacy("v1|400|90", "1750000000000");
            c = Start(both);
            Check("new and old both present: the snapshot is used (500 steps, its place and settings)",
                c.LifeLog.TotalSteps == 500 && c.Session.Cell == new Vector2Int(-1, 0) && c.Switcher.Mode == PocViewMode.ThirdPerson3D &&
                !c.Auto.AutoEnabled && c.Clock.AnchorMilliseconds == snap.TimeAnchorUnixMs, c.Save.LoadSource);
            // Old key further ahead: played in ver2.9 after a rollback.
            SetLegacy("v1|600|150", "1750000000000");
            c = Start(both);
            Check("old key further ahead (ver2.9 after a rollback): its 600 steps / 150 s are taken; steps never go back",
                c.LifeLog.TotalSteps == 600 && c.LifeLog.TotalPlaySeconds == 150 && c.Session.Cell == new Vector2Int(-1, 0));

            // New save damaged, old keys there.
            var damaged = new HarukaMemoryStorage();
            foreach (var name in HarukaSnapshotStore.SlotNames) damaged.Files[name] = "{\"format\": \"harukaworld-save\", \"schemaVersion\": 1, broken";
            var damagedCopy = damaged.Files["snapshot_1.json"];
            SetLegacy("v1|800|300", "1750000000000");
            c = Start(damaged);
            Settle(c, 1.5f);
            Check("every snapshot damaged, old keys there: their values are used (800 steps), saving goes on, the player is told",
                c.LifeLog.TotalSteps == 800 && !c.Save.SavingStopped && Latest(c) != null && Latest(c).TotalSteps == 800 &&
                c.Save.LoadSource.Contains("damaged") && Shown(c) == HarukaSaveSystem.MessageRecovered, Shown(c));
            Check("the damaged snapshots are copied aside, not deleted", damaged.Read("damaged_1.json") == damagedCopy);

            // Damaged with nothing to fall back on.
            var lost = new HarukaMemoryStorage();
            foreach (var name in HarukaSnapshotStore.SlotNames) lost.Files[name] = "{ broken";
            SetLegacy(null, "1750000000000");
            c = Start(lost);
            Step(c, FreeDirection(c));
            Settle(c, 4f);
            Check("every snapshot damaged and no Life Log key: no empty save is written over them (saving stopped, files unchanged)",
                c.Save.SavingStopped && HarukaSnapshotStore.SlotNames.All(n => lost.Files[n] == "{ broken") &&
                Shown(c) == HarukaSaveSystem.MessageUnreadable, c.Save.StopReason + " / " + Shown(c));

            // Migration cut off: the first snapshot write is interrupted.
            var cut = new HarukaMemoryStorage { TruncateNextWriteTo = 30 };
            SetLegacy("v1|222|33", "1750000000000");
            c = Start(cut); // the first snapshot write (at once on start) is cut off
            var cutOff = c.Save.FailedWrites == 1 && c.Save.Store.Scan().Latest == null;
            c = Start(cut);
            Check("migration cut off midway: the next start still has the old values (222 steps), never 0",
                cutOff && c.LifeLog.TotalSteps == 222 && c.LifeLog.TotalPlaySeconds == 33, $"cut off {cutOff}");
            Settle(c, 1.5f);
            Check("…and that start writes the migration again", Latest(c) != null && Latest(c).TotalSteps == 222);
        }

        internal static Vector2Int FreeDirection(Ctx c)
        {
            FreeNeighbour(c, out var d);
            return d;
        }

        // ---------------------------------------------------------------- automatic save

        private static void AutoSaveChecks()
        {
            Category("autosave");
            SetLegacy("v1|10|10", "1750000000000");
            var c = Start(new HarukaMemoryStorage());
            Settle(c, 1.5f);
            var writes = c.Save.Writes;
            Settle(c, 10f);
            Check("standing still: nothing is written for 10 s (no frame-by-frame saving)", c.Save.Writes == writes);
            var target = FreeNeighbour(c, out var dir);
            writes = c.Save.Writes;
            Step(c, dir, 0.3f);
            var late = Latest(c);
            Check("one step after standing still: written at once, with the new cell, facing and step count",
                c.Session.Cell == target && c.Save.Writes == writes + 1 && late.CellX == target.x && late.CellY == target.y &&
                late.Facing == HarukaSaveData.FacingName(c.Session.Facing) && late.TotalSteps == c.LifeLog.TotalSteps,
                $"cell {c.Session.Cell} target {target} writes +{c.Save.Writes - writes} saved ({late.CellX},{late.CellY}) {late.Facing} {late.TotalSteps}/{c.LifeLog.TotalSteps} {c.Save.LastWriteReason}");
            Settle(c, 3f);
            // Holding a key for several steps.
            writes = c.Save.Writes;
            Release(c);
            var holdRaw = RawFor(c, -dir);
            var held = 0f;
            for (; held < 2.4f; held += 1f / 60f) Frame(c, 1f / 60f, holdRaw);
            Release(c);
            Settle(c, 3.2f);
            Check("walking with a held key: few writes (at most one per 3 s), the final cell saved",
                c.Save.Writes - writes <= 2 && Latest(c).CellX == c.Session.Cell.x && Latest(c).CellY == c.Session.Cell.y,
                $"{c.Save.Writes - writes} writes");
            // A step into something solid.
            var blockedDir = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
                .FirstOrDefault(d => !c.Session.World.CanEnter(c.Session.Cell + d));
            writes = c.Save.Writes;
            var cellBefore = c.Session.Cell;
            if (blockedDir != Vector2Int.zero) Step(c, blockedDir, 3.5f);
            Check("a step into something solid: the cell stays, nothing new to save except the facing",
                blockedDir == Vector2Int.zero || c.Session.Cell == cellBefore && Latest(c).CellX == cellBefore.x && Latest(c).CellY == cellBefore.y,
                blockedDir.ToString());
            // The view and AUTO setting: saved within a second.
            c.Switcher.Toggle();
            Settle(c, 1.1f);
            Check("2D / 3D switched: saved within about a second", Latest(c).ViewMode == "3D");
            c.Auto.SetAutoEnabled(false, clock);
            Settle(c, 1.1f);
            Check("AUTO switched off: saved within about a second", !Latest(c).AutoEnabled);
            // Play time alone.
            writes = c.Save.Writes;
            Settle(c, 20f);
            var mid = c.Save.Writes;
            Settle(c, 6f);
            Check("play time alone: saved every 25 s (not before)", mid == writes && c.Save.Writes == writes + 1,
                $"{writes} -> {mid} -> {c.Save.Writes}");
            // Hidden page: at once.
            Step(c, FreeDirection(c), 0.2f);
            writes = c.Save.Writes;
            c.Save.OnPageHidden("");
            Check("page hidden / closing: saved at once, without waiting for the interval",
                c.Save.Writes == writes + 1 && Latest(c).CellX == c.Session.Cell.x && Latest(c).CellY == c.Session.Cell.y);
            // Mid-step: the logical cell.
            Settle(c, 1f);
            var from = c.Session.Cell;
            var stepDir = FreeDirection(c);
            Release(c);
            for (var i = 0; i < 3; i++) Frame(c, 1f / 60f, RawFor(c, stepDir));
            var moving = c.Session.IsMoving;
            c.Save.OnPageHidden("");
            var mid2 = Latest(c);
            Release(c);
            Settle(c, 0.5f);
            Check("saved during a step: the cell it started from (never a position between cells)",
                moving && mid2.CellX == from.x && mid2.CellY == from.y, $"moving {moving}");
            Check("each save also writes the ver2.9 keys (same steps and anchor)",
                LegacyLife().StartsWith("v1|" + Latest(c).TotalSteps.ToString(CultureInfo.InvariantCulture) + "|") &&
                PlayerPrefs.GetString(WorldTimeController.SaveKey) == Latest(c).TimeAnchorUnixMs.ToString(CultureInfo.InvariantCulture));
            // Into the house: map and cell together, in every snapshot written on the way.
            var storage = c.Storage;
            var door = c.Session.OutdoorWorld.Map.HouseDoorCell;
            c.Session.PlaceForCheck(door + Vector2Int.down, FacingDirection.Up);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 3.2f);
            var consistent = true;
            Release(c);
            for (var t = 0f; t < 3f && c.Session.World != c.Session.InteriorWorld; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.up);
                var s = Latest(c);
                var w = c.Session.FindWorld(s.MapId);
                consistent &= w != null && w.CanEnter(new Vector2Int(s.CellX, s.CellY));
            }
            Release(c);
            Settle(c, 1.2f);
            var inside = Latest(c);
            c.Session.InteriorWorld.TryGetMarker("interior_entry", out var entry);
            Check("into the house: saved within about a second as the house and its entry cell (never the house with an outdoor cell)",
                inside.MapId == "halka_house" && inside.CellX == entry.x && inside.CellY == entry.y && consistent);
            // AUTO walking.
            ToOutdoor(c, new Vector2Int(-1, 0));
            c.Auto.SetAutoEnabled(true, clock);
            Settle(c, 1.2f);
            var startSteps = c.LifeLog.TotalSteps;
            for (var t = 0f; t < 75f && c.LifeLog.TotalSteps < startSteps + 3; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, true);
            var autoSteps = c.LifeLog.TotalSteps;
            for (var t = 0f; t < 6f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, true);
            c.Auto.RecordUserAction(clock);
            Settle(c, 3.5f);
            Check("AUTO walking: its completed steps are saved like the player's (cell and steps)",
                autoSteps >= startSteps + 3 && Latest(c).TotalSteps == c.LifeLog.TotalSteps &&
                Latest(c).CellX == c.Session.Cell.x && Latest(c).CellY == c.Session.Cell.y);
        }

        internal static void ToOutdoor(Ctx c, Vector2Int cell)
        {
            if (c.Session.World != c.Session.OutdoorWorld) c.Session.LoadMapForCheck(false, cell, FacingDirection.Down);
            c.Session.PlaceForCheck(cell, FacingDirection.Down);
            Settle(c, 0.2f);
        }

        // ---------------------------------------------------------------- resume

        private static void ResumeChecks()
        {
            Category("resume");
            SetLegacy("v1|50|60", "1750000000000");
            var storage = new HarukaMemoryStorage();
            var c = Start(storage);
            Settle(c, 1.2f);
            ToOutdoor(c, new Vector2Int(-1, 0));
            Step(c, Vector2Int.left, 0.2f);
            c.Session.SetFacing(FacingDirection.Right);
            c.Save.OnPageHidden("");
            var outCell = c.Session.Cell;
            c = Start(storage);
            Check("outdoors: the same cell and facing after a restart", c.Session.World.MapId == "first_field" &&
                c.Session.Cell == outCell && c.Session.Facing == FacingDirection.Right, c.Session.Cell.ToString());
            // Indoors, 3D, AUTO off.
            c.Session.LoadMapForCheck(true, new Vector2Int(2, 0), FacingDirection.Up);
            Settle(c, 0.2f);
            Step(c, Vector2Int.down, 0.2f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Auto.SetAutoEnabled(false, clock);
            var steps = c.LifeLog.TotalSteps;
            var seconds = c.LifeLog.TotalPlaySeconds;
            c.Save.OnPageHidden("");
            var inCell = c.Session.Cell;
            c = Start(storage);
            Check("in the house: the same cell after a restart", c.Session.World.MapId == "halka_house" && c.Session.Cell == inCell,
                c.Session.Cell.ToString());
            Check("3D kept, AUTO off kept", c.Switcher.Mode == PocViewMode.ThirdPerson3D && !c.Auto.AutoEnabled);
            Check("Life Log kept (steps and play time), the morning / night anchor kept",
                c.LifeLog.TotalSteps == steps && c.LifeLog.TotalPlaySeconds >= seconds && c.Clock.AnchorMilliseconds == 1750000000000L);
            Check("after a resume: no map change, no message, not moving, AUTO not walking",
                c.Session.MapLoads == 1 && c.Session.LastMessage == null && !c.Session.IsMoving && !c.Auto.Active);
            c.Auto.SetAutoEnabled(true, clock);
            Settle(c, 1.2f);
            c = Start(storage);
            var walked = false;
            for (var t = 0f; t < 55f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); walked |= c.Auto.Active || c.Session.IsMoving; }
            Check("AUTO on after a resume: it does not walk off at once (the 60 s are counted from the start)", c.Auto.AutoEnabled && !walked);

            // Seated when the page closed.
            ToOutdoor(c, new Vector2Int(-2, 1));
            c.Session.SetFacing(FacingDirection.Up);
            Settle(c, 0.1f);
            c.Session.InteractAhead();
            Settle(c, 0.2f);
            var seated = c.Session.IsSeated;
            c.Save.OnPageHidden("");
            c = Start(storage);
            Check("seated on the bench when closed: resumes standing on the cell in front, no pose, not on the seat",
                seated && !c.Session.IsSeated && c.Session.Cell == new Vector2Int(-2, 1) && !c.Auto.Active, $"seated {seated}");

            // Bad places in a save.
            string Saved(string map, int x, int y)
            {
                var s = new HarukaMemoryStorage();
                var d = Sample(); d.MapId = map; d.CellX = x; d.CellY = y; d.Facing = "left"; d.ViewMode = "2D";
                new HarukaSnapshotStore(s).Write(d);
                var g = Start(s);
                return g.Session.World.MapId + "|" + g.Session.Cell.x + "," + g.Session.Cell.y + "|" +
                    g.Session.World.CanEnter(g.Session.Cell) + "|" + HarukaSaveSystem.IsTransitionCell(g.Session.World, g.Session.Cell) +
                    "|" + g.LifeLog.TotalSteps + "|" + g.Save.LocationNote;
            }
            var tree = Saved("first_field", 5, 1);
            Check("saved on a cell that is now blocked (a tree): the nearest free cell of the same map", tree.StartsWith("first_field|") &&
                tree.Contains("|True|False|") && tree.Contains("nearest"), tree);
            var outside = Saved("halka_house", 40, 40);
            Check("saved outside the map's bounds: a free cell inside the map", outside.StartsWith("halka_house|") && outside.Contains("|True|False|"), outside);
            var unknown = Saved("old_map", 1, 1);
            Check("an unknown (removed) map ID: the first map's start, the Life Log kept",
                unknown.StartsWith("first_field|") && unknown.Contains("|1234|") && unknown.Contains("unknown map"), unknown);
            var doorCell = c.Session.OutdoorWorld.Map.HouseDoorCell;
            var onDoor = Saved("first_field", doorCell.x, doorCell.y);
            Check("saved on the house door: resumes next to it, with no map change at start",
                onDoor.StartsWith("first_field|") && onDoor.Contains("|True|False|"), onDoor);
        }

        // ---------------------------------------------------------------- export

        private static void ExportChecks()
        {
            Category("export");
            SetLegacy("v1|70|80", "1750000000000");
            var storage = new HarukaMemoryStorage();
            var c = Start(storage);
            Settle(c, 1.2f);
            Step(c, FreeDirection(c), 0.2f);
            var before = c.Save.Capture();
            var revisionsBefore = c.Save.Store.Scan().Slots.Count(s => s.Status == HarukaSaveReadStatus.Valid);
            var text = c.Save.ExportText();
            var status = HarukaSaveData.TryRead(text, out var exported, out _);
            Check("export: a good schema-1 file with every field, the current state", status == HarukaSaveReadStatus.Valid &&
                exported.MapId == before.MapId && exported.CellX == before.CellX && exported.CellY == before.CellY &&
                exported.Facing == before.Facing && exported.TotalSteps == before.TotalSteps && exported.ViewMode == before.ViewMode &&
                exported.AutoEnabled == before.AutoEnabled && exported.TimeAnchorUnixMs == before.TimeAnchorUnixMs);
            Check("export: UTF-8 JSON text; no personal or browser data (only the save's fields)",
                text.StartsWith("{") && !text.Contains("C:\\") && !text.Contains("http") && !text.Contains("userAgent"));
            var after = c.Save.Capture();
            Check("export changes nothing in play (place, steps, settings) and loses no snapshot",
                after.MapId == before.MapId && after.CellX == before.CellX && after.CellY == before.CellY && after.TotalSteps == before.TotalSteps &&
                after.AutoEnabled == before.AutoEnabled && after.ViewMode == before.ViewMode &&
                c.Save.Store.Scan().Slots.Count(s => s.Status == HarukaSaveReadStatus.Valid) >= revisionsBefore);
            // Through the menu.
            c.Save.Menu.Open(clock);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("menu → セーブデータ: the page arms the エクスポート and インポート buttons for the browser",
                c.Browser.ArmedJson != null && c.Browser.ArmedExportRect.width > 0f && c.Browser.ArmedImportRect.width > 0f);
            c.Menu.PressPageButton(0, clock);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("エクスポート: the browser gets the file, the page says it was written",
                c.Browser.ExportCount == 1 && HarukaSaveData.TryRead(c.Browser.Exported, out _, out _) == HarukaSaveReadStatus.Valid &&
                c.Save.Menu.Current == HarukaSaveSystem.SaveMenu.Stage.ExportDone);
            c.Menu.PressPageButton(0, clock);
            c.Browser.FailNextExport = true;
            Frame(c, 1f / 60f, Vector2Int.zero);
            c.Menu.PressPageButton(0, clock);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("a failed download is never reported as done", c.Save.Menu.Current == HarukaSaveSystem.SaveMenu.Stage.ExportFailed);
            c.Menu.SetOpen(false, clock);
            Check("export file name: harukaworld_save_YYYYMMDD_HHMMSS.json (the page builds it from the export time)",
                HarukaWebGLSaveBrowser.FilePrefix == "harukaworld_save_" && Jslib().Contains("s.exportPrefix + now.getFullYear()") &&
                Jslib().Contains("two(now.getSeconds()) + \".json\""));
        }

        private static string Jslib() => File.ReadAllText(Path.Combine(Application.dataPath, "POC2D3D", "Plugins", "WebGL", "HarukaSave.jslib"));

        // ---------------------------------------------------------------- import

        private static void ImportChecks()
        {
            Category("import");
            SetLegacy("v1|90|100", "1750000000000");
            var storage = new HarukaMemoryStorage();
            var c = Start(storage);
            Settle(c, 1.2f);
            var file = Sample(); file.TotalSteps = 4321; file.TotalPlaySeconds = 3725; file.AutoEnabled = false;
            file.ViewMode = "3D"; file.MapId = "halka_house"; file.CellX = 2; file.CellY = 0; file.Facing = "up";
            file.TimeAnchorUnixMs = 1760000000000L;
            var fileText = Sealed(file);
            var before = c.Save.Capture();
            var latestBefore = Latest(c).Revision;

            string Import(string text, bool confirm = true)
            {
                c.Save.Menu.Open(clock);
                c.Browser.NextImport = text;
                c.Menu.PressPageButton(1, clock);
                Frame(c, 1f / 60f, Vector2Int.zero);
                var stage = c.Save.Menu.Current.ToString();
                if (c.Save.Menu.Current == HarukaSaveSystem.SaveMenu.Stage.ImportConfirm)
                {
                    c.Menu.PressPageButton(confirm ? 0 : 1, clock);
                    c.LifeLog.AdvanceClock(clock, false); // the Life Log's own sample clock restarts on an import
                    Frame(c, 1f / 60f, Vector2Int.zero);
                    stage += ">" + c.Save.Menu.Current;
                }
                var problem = c.Save.Menu.LastProblem;
                c.Menu.SetOpen(false, clock);
                return stage + "|" + problem;
            }

            bool Unchanged()
            {
                var now = c.Save.Capture();
                var last = Latest(c);
                return now.MapId == before.MapId && now.CellX == before.CellX && now.CellY == before.CellY &&
                    now.TotalSteps == before.TotalSteps && now.AutoEnabled == before.AutoEnabled && now.ViewMode == before.ViewMode &&
                    last.MapId == before.MapId && last.CellX == before.CellX && last.CellY == before.CellY && last.TotalSteps == before.TotalSteps;
            }

            // The confirmation page, then やめる.
            c.Save.Menu.Open(clock);
            c.Browser.NextImport = fileText;
            c.Menu.PressPageButton(1, clock);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var lines = string.Join(" / ", c.Save.Menu.Lines);
            Check("a good file: a confirmation page with the date, steps, play time, place and view; nothing changed yet",
                c.Save.Menu.Current == HarukaSaveSystem.SaveMenu.Stage.ImportConfirm && lines.Contains("4,321") && lines.Contains("1:02:05") &&
                lines.Contains("家") && lines.Contains("3D") && lines.Contains("おきかえますか") && Unchanged(), lines);
            Check("the confirmation buttons are ふくげんする / やめる", c.Save.Menu.Buttons.SequenceEqual(new[] { "ふくげんする", "やめる" }));
            c.Menu.PressPageButton(1, clock);
            c.Menu.SetOpen(false, clock);
            Check("やめる: nothing changes", Unchanged());
            Check("file chooser cancelled: back to the page, nothing changes", Import(null).StartsWith("Main") && Unchanged());
            Check("a broken file: refused, nothing changes", Import("{ \"format\": \"harukaworld-save\", ").StartsWith("ImportFailed") && Unchanged());
            Check("a newer schema: refused (あたらしい ばん), nothing changes",
                Import(fileText.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 3")).Contains("あたらしい") && Unchanged());
            var unknownMap = file.Copy(); unknownMap.MapId = "secret_room";
            Check("an unknown map: refused", Import(Sealed(unknownMap)).StartsWith("ImportFailed") && Unchanged());
            var outside = file.Copy(); outside.CellX = 99;
            Check("a cell outside the map: refused", Import(Sealed(outside)).StartsWith("ImportFailed") && Unchanged());
            var negative = file.Copy(); negative.TotalPlaySeconds = -3;
            Check("a negative number: refused", Import(Sealed(negative)).StartsWith("ImportFailed") && Unchanged());
            Check("an edited value (checksum differs): refused",
                Import(fileText.Replace("\"totalSteps\": 4321", "\"totalSteps\": 999999")).StartsWith("ImportFailed") && Unchanged());
            Check("an empty file: refused (からっぽ)", Import("").Contains("からっぽ") && Unchanged());
            Check("a file over 64 KB: refused", Import(fileText + new string(' ', 70000)).Contains("おおきすぎ") && Unchanged());
            c.Browser.ImportTooLarge = true;
            Check("the page refuses a large file before reading it: refused", Import("x").StartsWith("ImportFailed") && Unchanged());
            c.Browser.ImportTooLarge = false;
            storage.FailWrites = true;
            var failed = Import(fileText);
            storage.FailWrites = false;
            Check("writing fails during an import: the world and the save stay as they were", failed.Contains("ImportFailed") && Unchanged(), failed);

            // Restore.
            var result = Import(fileText);
            Check("ふくげんする: done", result.StartsWith("ImportConfirm>ImportDone"), result);
            Check("after the import: the house, cell (2,0), facing up, 3D, AUTO off",
                c.Session.World.MapId == "halka_house" && c.Session.Cell == new Vector2Int(2, 0) && c.Session.Facing == FacingDirection.Up &&
                c.Switcher.Mode == PocViewMode.ThirdPerson3D && !c.Auto.AutoEnabled);
            Check("after the import: steps 4,321, play time 3,725 s, the anchor from the file",
                c.LifeLog.TotalSteps == 4321 && c.LifeLog.TotalPlaySeconds == 3725 && c.Clock.AnchorMilliseconds == 1760000000000L);
            var recovery = c.Save.Store.ReadRecovery();
            Check("before the import, a recovery copy of the previous state was kept",
                recovery != null && recovery.TotalSteps == before.TotalSteps && recovery.MapId == before.MapId);
            Check("the import is the newest snapshot; the old ones are kept as earlier generations; the ver2.9 keys follow",
                Latest(c).TotalSteps == 4321 && Latest(c).Revision > latestBefore &&
                c.Save.Store.Scan().Slots.Any(s => s.Data != null && s.Data.Revision == latestBefore) && LegacyLife().StartsWith("v1|4321|3725"));
            c = Start(storage);
            Check("after the import and a restart: the imported place and data continue",
                c.Session.World.MapId == "halka_house" && c.Session.Cell == new Vector2Int(2, 0) && c.LifeLog.TotalSteps == 4321 &&
                c.Switcher.Mode == PocViewMode.ThirdPerson3D);
            // A blocked cell in an otherwise good file: moved to a safe cell.
            var blocked = file.Copy(); blocked.MapId = "first_field"; blocked.CellX = 5; blocked.CellY = 1;
            var blockedResult = Import(Sealed(blocked));
            Check("a file whose cell is now blocked: imported, the player put on the nearest free cell",
                blockedResult.StartsWith("ImportConfirm>ImportDone") && c.Session.World.CanEnter(c.Session.Cell) && c.Session.Cell != new Vector2Int(5, 1),
                blockedResult + " " + c.Session.Cell);
        }

        // ---------------------------------------------------------------- menu

        private static void MenuChecks()
        {
            Category("menu");
            SetLegacy("v1|1|1", "1750000000000");
            var c = Start(new HarukaMemoryStorage());
            var menu = c.Menu;
            Check("menu rows: AUTO, ひょうじ, せいかつきろく, セーブデータ, やめる (the version at the bottom); no ぜんがめん",
                menu.ExtraItems.Count == 1 && menu.ExtraItems[0].Label == "ひょうじ" && menu.LinkLabels.SequenceEqual(new[] { "セーブデータ" }) &&
                menu.CloseLabel == "やめる" && menu.ExtraItems.All(i => i.Label != "ぜんがめん") && !menu.LinkLabels.Contains("ぜんがめん"));
            menu.SetOpen(true, clock);
            Check("the menu suspends AUTO while open (also on the save pages)", c.Auto.MenuSuspended);
            menu.ActivateLink(0, clock);
            Check("セーブデータ opens its page: エクスポート / インポート / もどる", menu.IsPageOpen &&
                c.Save.Menu.Buttons.SequenceEqual(new[] { "エクスポート", "インポート", "もどる" }));
            menu.PressPageButton(2, clock);
            Check("もどる returns to the main rows", menu.IsOpen && !menu.IsPageOpen);
            menu.SetOpen(false, clock);
            var texts = new List<string> { "セーブデータ", "やめる", "エクスポート", "インポート", "もどる", "ふくげんする",
                HarukaSaveSystem.MessageRecovered, HarukaSaveSystem.MessageUnreadable, HarukaSaveSystem.MessageNewer, HarukaSaveSystem.MessageOtherTab,
                "じどうで セーブしています。", "いまは セーブしていません。", "ファイルに かきだして", "べつの ばしょに ほぞんできます。",
                "かきだしています…", "ファイルを かきだしました。", "ダウンロードを たしかめてください。", "かきだせませんでした。",
                "ファイルを えらんでください。", "ほぞんしたひ 2026/10/18 12:34", "あるいたかず 1,234", "プレイじかん 1:02:03", "ばしょ 家  ひょうじ 3D",
                "ばしょ そと", "いまの セーブデータを", "おきかえますか？", "いまの セーブデータは そのままです。", "ふくげんできませんでした。",
                "ファイルを よめませんでした。", "ファイルが おおきすぎます。", "あたらしい ばんの ファイルです。", "ファイルが からっぽです。", "この ファイルは よみこめません。" };
            var missing = texts.Where(t => !GameBitmapFont.HasAllGlyphs(t)).ToArray();
            Check("every save text is drawn by the game's own bitmap font (no missing glyph)", missing.Length == 0, string.Join(" / ", missing));
            menu.SetOpen(true, clock);
            menu.ActivateLink(0, clock);
            var pageOpen = menu.IsPageOpen;
            menu.SetOpen(false, clock);
            Check("やめる / closing the menu also closes the page", pageOpen && !menu.IsPageOpen && !menu.IsOpen);
        }

        // ---------------------------------------------------------------- tabs

        private static void TabChecks()
        {
            Category("tabs");
            SetLegacy("v1|5|5", "1750000000000");
            var storage = new HarukaMemoryStorage();
            var browser = new HarukaEditorSaveBrowser();
            var c = Start(storage, browser);
            Settle(c, 1.2f);
            Check("each save leaves its revision for other tabs (localStorage marker)", browser.Marker == Latest(c).Revision);
            browser.Marker = Latest(c).Revision + 5;
            var written = c.Save.Writes;
            var latest = Latest(c).Revision;
            Step(c, FreeDirection(c), 3.5f);
            Check("another tab saved later: this tab stops saving (nothing written over it) and says so",
                c.Save.SavingStopped && c.Save.Writes == written && Latest(c).Revision == latest && c.Save.StopReason.StartsWith("another tab"));
            // A marker left ahead by this browser's own earlier session (closed before its storage synced).
            var storage2 = new HarukaMemoryStorage();
            new HarukaSnapshotStore(storage2).Write(Sample());
            var browser2 = new HarukaEditorSaveBrowser { Marker = 9 };
            c = Start(storage2, browser2);
            Step(c, FreeDirection(c), 3.5f);
            Check("a marker ahead at start (an earlier session's unsynced save): no false stop, the next revision goes past it",
                !c.Save.SavingStopped && Latest(c).Revision > 9);
        }

        // ---------------------------------------------------------------- gameplay with the save

        private static void GameplayChecks()
        {
            Category("gameplay");
            SetLegacy("v1|0|0", "1750000000000");
            var c = Start(new HarukaMemoryStorage());
            Settle(c, 1.2f);
            ToOutdoor(c, new Vector2Int(-1, 0));
            var cell = c.Session.Cell;
            c.Switcher.Toggle();
            Settle(c, 0.3f);
            c.Switcher.Toggle();
            Settle(c, 0.3f);
            Check("2D / 3D switching keeps the cell", c.Session.Cell == cell);
            var steps = c.LifeLog.TotalSteps;
            Step(c, FreeDirection(c), 0.3f);
            Check("Life Log still counts completed steps", c.LifeLog.TotalSteps == steps + 1);
            ToOutdoor(c, new Vector2Int(-2, 1));
            c.Session.SetFacing(FacingDirection.Up);
            c.Session.InteractAhead();
            Settle(c, 0.2f);
            var sat = c.Session.IsSeated;
            c.Session.InteractAhead();
            Settle(c, 0.2f);
            Check("the bench: sit and stand as before", sat && !c.Session.IsSeated);
            Check("the crow is there outdoors", c.Crow != null && c.Crow.gameObject.activeInHierarchy);
            var door = c.Session.OutdoorWorld.Map.HouseDoorCell;
            c.Session.PlaceForCheck(door + Vector2Int.down, FacingDirection.Up);
            Release(c);
            for (var t = 0f; t < 3f && c.Session.World != c.Session.InteriorWorld; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.up);
            Release(c);
            Settle(c, 0.3f);
            Check("the house: entering works as before", c.Session.World == c.Session.InteriorWorld);
            Check("morning / night follows the anchor", c.Clock.CurrentPhase == WorldTimeCycle.PhaseAt(c.Clock.AnchorMilliseconds, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }

        // ---------------------------------------------------------------- browser side (static)

        private static void BrowserChecks()
        {
            Category("browser");
            var js = Jslib();
            Check("download: a Blob of UTF-8 JSON, the file name set, the URL released afterwards",
                js.Contains("new Blob([s.exportJson], { type: \"application/json;charset=utf-8\" })") && js.Contains("link.download = name") &&
                js.Contains("URL.revokeObjectURL(url)"));
            Check("download and file chooser run inside the person's tap (pointerup on the armed button)",
                js.Contains("document.addEventListener(\"pointerup\"") && js.Contains("exportNow()") && js.Contains("importNow()"));
            Check("import reads only the picked file, as text, with a size limit checked before reading; nothing is run",
                js.Contains("file.size > s.maxBytes") && js.Contains("readAsText(file, \"utf-8\")") && !js.Contains("eval(") && !js.Contains("new Function"));
            Check("a cancelled chooser is noticed (cancel event / no file)", js.Contains("\"cancel\"") && js.Contains("if (!file) { s.importState = 2"));
            Check("page hidden and closing are passed on (visibilitychange, pagehide)",
                js.Contains("visibilitychange") && js.Contains("pagehide") && js.Contains("SendMessage(\"HarukaSaveSystem\", \"OnPageHidden\""));
            Check("the multi-tab marker is kept per page path", js.Contains("\"harukaworld-save-revision:\" + location.pathname"));
            var staging = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d", "rc2", "staging", "halkaworld", "index.html"));
            Check("RC2 staging page: the game at webgl/ in the frame, the full screen button outside the game, the page-filling fallback",
                staging.Contains("src=\"webgl/?v=3.0-rc2&build=__BUILD__\"") && staging.Contains("全画面にする") &&
                staging.Contains("game-fill-back") && staging.Contains("allow=\"fullscreen\""));
        }

        // ---------------------------------------------------------------- protection (static)

        private static void ProtectionChecks()
        {
            Category("protection");
            Check("the ver2.9 keys keep their names", LifeLogController.SaveKey == "HALKA_WORLD_life_log_v1" &&
                WorldTimeController.SaveKey == "HALKA_WORLD_time_cycle_anchor_v1");
            var code = string.Join("\n", Directory.GetFiles(Path.Combine(Application.dataPath, "POC2D3D", "Runtime", "Save"), "*.cs")
                .Select(File.ReadAllText));
            Check("the save code never deletes saves or keys (no File.Delete, DeleteKey, DeleteAll)",
                !code.Contains("File.Delete") && !code.Contains("DeleteKey") && !code.Contains("DeleteAll") && !code.Contains("Directory.Delete"));
            Check("the game's page path stays /halkaworld/webgl/ (same storage as ver2.9)", PocBuilder.ReleaseCandidateGamePath == "halkaworld/webgl");
            var page = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "halkaworld", "index.html"));
            Check("the public page in the repository is still ver2.9 (not changed)", page.Contains("ver2.9") && !page.Contains("RC2"));
        }
    }
}
