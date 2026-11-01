using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Life;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;
using V32 = Halka.Poc2D3D.EditorTools.WorldVer32Checks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver4.0「そうさくせいかつ！」: the checks of what ver4.0 adds (指示書 20) on the real game
    // (the Phase 6 harness: the RC scene's components, memory storage, a clock the checks move):
    //   memory     the outline copies shared and released after batching; chunks and rooms still free
    //              every mesh they made; a round of the nine regions leaves the meshes as they were;
    //   time       the game clock read from the world clock (06:00 / 18:00 / 00:00, the phase, the day);
    //   skills     the three abilities, the 20-point bands, no cap (10^500), the save of huge numbers;
    //   games      the three mini games' scores, a cancel that gives nothing, the window and the world;
    //   nico       ﾆｺﾁｭｰﾌﾞ: the first 24 hours, a post, money = views / 10, the next 24 hours (a new day
    //              alone never opens it), no double post, the save and a restart, huge numbers;
    //   npc        the chance by subscribers, the guarantee from 1,000,000, at most 3, where they stand,
    //              chunks, the save and a restart (never duplicated);
    //   companions Rin and Len from the user's models, following alone, together and with the crow,
    //              over region borders, そらをとぶ, waiting at the door, the save;
    //   hide       hide-and-seek: Rin, Len, both; the places; finding; the hint; stopping; the records;
    //   save       older saves (no life data) once only, export / import, a damaged block;
    //   existing   the crow, そらをとぶ's five places, the menu, AUTO;
    //   protect    what must not change.
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer40Checks.RunBatch
    public static class WorldVer40Checks
    {
        private const string Published = "5adf1c1";   // ver3.9 as published (2026-10-31): this version's base
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";
        private static long fakeNow;
        private static int sharedCellFrames;
        private static int blockedCellFrames;

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
            lines.Clear();
            var keys = new[] { LifeLogController.SaveKey, WorldTimeController.SaveKey };
            var saved = keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
            var randomState = UnityEngine.Random.state;
            var shadowDistance = QualitySettings.shadowDistance;   // the 3D view shortens it (PocViewSwitcher)
            UnityEngine.Random.InitState(4040);
            try
            {
                Run("memory", MemoryChecks);
                Run("time", TimeChecks);
                Run("skills", SkillChecks);
                Run("games", GameChecks);
                Run("nico", NicoChecks);
                Run("npc", NpcChecks);
                Run("companions", CompanionChecks);
                Run("hide", HideChecks);
                Run("garden", GardenChecks);
                Run("house", HouseChecks);
                Run("sleep", SleepChecks);
                Run("difficulty", DifficultyChecks);
                Run("look2", SecondLookChecks);
                Run("save", SaveChecks);
                Run("existing", ExistingChecks);
                Run("protect", ProtectChecks);
            }
            finally
            {
                C6.DisposeHarness();
                UnityEngine.Random.state = randomState;
                QualitySettings.shadowDistance = shadowDistance;
                foreach (var pair in saved)
                {
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                    else PlayerPrefs.SetString(pair.Key, pair.Value);
                }
                PlayerPrefs.Save();
            }
            foreach (var failure in failures) Debug.LogError("VER40 FAIL " + failure);
            Debug.Log("HARUKA ver4.0 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver4.0 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V40_LOGS", "ver40_checks.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllLines(report, lines.Concat(new[] { $"TOTAL {passes} passed, {failures.Count} failed" }));
            return failures.Count == 0;
        }

        private static void Run(string name, Action checks)
        {
            category = name;
            try { checks(); }
            catch (Exception exception) { Check("no exception in " + name, false, exception.GetType().Name + ": " + exception.Message + " " + exception.StackTrace?.Split('\n').FirstOrDefault()); Debug.LogException(exception); }
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            detail = detail ?? "";
            perCategory[category] = perCategory.TryGetValue(category, out var n) ? n + 1 : 1;
            var line = (condition ? "PASS" : "FAIL") + " [" + category + "] " + name + (detail.Length > 0 ? " | " + detail : "");
            lines.Add(line);
            if (condition) { passes++; Debug.Log("VER40 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness

        private static PocLife L(C6.Ctx c) => UnityEngine.Object.FindAnyObjectByType<PocLife>(FindObjectsInactive.Include);

        // The game with ver4.0 started; the world clock reads fakeNow (moved by the checks).
        private static C6.Ctx Begin(HarukaMemoryStorage storage, long? startMs = null)
        {
            var fresh = storage.Files.Count == 0;
            fakeNow = startMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var c = C6.Start(storage, null, null);
            c.Clock.ClockMilliseconds = () => fakeNow;
            if (fresh) c.Clock.SetAnchor(fakeNow, fakeNow); // a new game begins now (the check's clock)
            c.Save.Clock = () => DateTimeOffset.FromUnixTimeMilliseconds(fakeNow);
            var t = c.Gameplay.FastTravel;
            C6.Invoke(t, "Awake");
            t.CrowTime = () => C6.Clock;
            t.StartPlay();
            var life = L(c);
            life.Begin();
            Frame(c, 1f / 60f, Vector2Int.zero);
            return c;
        }

        private static void Frame(C6.Ctx c, float dt, Vector2Int raw)
        {
            fakeNow += (long)(dt * 1000f);
            c.Clock.RefreshAt(fakeNow);   // WorldTimeController.Update
            C6.Frame(c, dt, raw, false);
            var crow = c.Crow;
            if (crow != null && crow.isActiveAndEnabled && crow.gameObject.activeInHierarchy && !crow.TravelHeld)
            {
                crow.WatchAir(C6.Clock);
                crow.WatchTravelReturn(C6.Clock);
                crow.Tick(C6.Clock);
            }
            if (c.Gameplay.CrowChoices != null) c.Gameplay.CrowChoices.Watch(C6.Clock);
            c.Presenter.UpdateStreaming(false);
            c.Gameplay.FastTravel.Tick(C6.Clock);
            var life = L(c);
            life.Tick(dt, C6.Clock);
            if (c.Session.World == c.Session.OutdoorWorld)
            {
                var cells = new HashSet<Vector2Int> { c.Session.Cell };
                var shared = false;
                if (crow != null && crow.isActiveAndEnabled && crow.GroundOccupancyActive && !crow.IsMoving) shared |= !cells.Add(crow.Cell);
                foreach (var w in PocWalker.Active)
                    if (w != null && w.Present && !w.HiddenByGame && !w.IsMoving)
                    {
                        shared |= !cells.Add(w.Cell);
                        if (!c.Session.OutdoorWorld.CanEnter(w.Cell)) blockedCellFrames++;
                    }
                if (shared) sharedCellFrames++;
            }
        }

        private static void Settle(C6.Ctx c, float seconds)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
        }

        // Moves the world clock on without frames (a page left open, a closed browser).
        private static void Advance(C6.Ctx c, long realMs)
        {
            fakeNow += realMs;
            Frame(c, 1f / 60f, Vector2Int.zero);
        }

        private static bool Walk(C6.Ctx c, Vector2Int goal, float maxSeconds = 160f)
        {
            var frames = 0;
            var limit = Mathf.CeilToInt(maxSeconds * 60f);
            while (c.Session.Cell != goal && frames < limit)
            {
                var path = Route(c, c.Session.Cell, goal);
                if (path == null || path.Count == 0) { Frame(c, 1f / 60f, Vector2Int.zero); frames++; continue; }
                var next = path[0];
                var from = c.Session.Cell;
                var held = 0;
                while (c.Session.Cell == from && !c.Session.IsMoving && frames < limit && held++ < 180)
                {
                    Frame(c, 1f / 60f, V32.Raw(c, next - from));
                    frames++;
                }
                while (c.Session.IsMoving && frames < limit) { Frame(c, 1f / 60f, Vector2Int.zero); frames++; }
            }
            C6.Release(c);
            return c.Session.Cell == goal;
        }

        private static List<Vector2Int> Route(C6.Ctx c, Vector2Int from, Vector2Int goal)
        {
            bool Open(Vector2Int cell) => c.Session.World.CanEnter(cell) && !c.Session.IsTransitionCell(c.Session.World, cell) &&
                (cell == goal || c.Session.CanPlayerEnter(cell));
            var previous = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == goal) break;
                foreach (var d in Dirs)
                {
                    var next = cell + d;
                    if (previous.ContainsKey(next) || !Open(next)) continue;
                    previous[next] = cell;
                    queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(goal)) return null;
            var path = new List<Vector2Int>();
            for (var cell = goal; cell != from; cell = previous[cell]) path.Add(cell);
            path.Reverse();
            return path;
        }

        private static void Face(C6.Ctx c, Vector2Int toward) => c.Session.SetFacing(FacingDirectionExtensions.FromVector(toward));

        // As a player talks to someone who walks about: towards where it stands now, a step at a time (never
        // pushing into it long enough for it to step aside), and A as soon as she is next to it.
        private static bool ApproachAndTalk(C6.Ctx c, PocWalker who, float maxSeconds = 90f)
        {
            var frames = 0;
            var limit = Mathf.CeilToInt(maxSeconds * 60f);
            while (frames < limit)
            {
                while (c.Session.IsMoving && frames < limit) { Frame(c, 1f / 60f, Vector2Int.zero); frames++; }
                var here = c.Session.Cell;
                var toward = who.Cell - here;
                if (Mathf.Abs(toward.x) + Mathf.Abs(toward.y) == 1)
                {
                    Face(c, toward);
                    Frame(c, 1f / 60f, Vector2Int.zero);
                    C6.Release(c);
                    if (c.Session.InteractAhead()) return true;
                }
                var path = Route(c, here, who.Cell);
                if (path == null || path.Count < 2) { Frame(c, 1f / 60f, Vector2Int.zero); frames++; continue; }
                var step = path[0] - here;
                for (var held = 0; held < 15 && c.Session.Cell == here && !c.Session.IsMoving && frames < limit; held++)
                {
                    Frame(c, 1f / 60f, V32.Raw(c, step));
                    frames++;
                }
            }
            C6.Release(c);
            return false;
        }

        // A free walkable cell next to the target from which she can face it.
        private static bool StandNextTo(C6.Ctx c, Vector2Int target)
        {
            foreach (var d in Dirs)
            {
                var cell = target + d;
                if (!c.Session.World.CanEnter(cell) || c.Session.IsTransitionCell(c.Session.World, cell)) continue;
                if (cell != c.Session.Cell && !c.Session.CanPlayerEnter(cell)) continue;
                if (!Walk(c, cell, 60f)) continue;
                Face(c, target - cell);
                return true;
            }
            return false;
        }

        private static IGameMenuPage Page(C6.Ctx c) => c.Menu.Page;

        private static bool Press(C6.Ctx c, string label)
        {
            var page = Page(c);
            if (page == null) return false;
            var index = page.Buttons.ToList().IndexOf(label);
            if (index < 0) return false;
            c.Menu.PressPageButton(index, C6.Clock + 1f);
            Frame(c, 1f / 60f, Vector2Int.zero);
            return true;
        }

        private static int Meshes() => Resources.FindObjectsOfTypeAll<Mesh>().Length;

        // ---------------------------------------------------------------- memory

        private static void MemoryChecks()
        {
            var a = new GameObject("a");
            var b = new GameObject("b");
            var d = new GameObject("d");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/POC2D3D/Models/V36/flower_blue.fbx");
            if (source == null) source = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.TreeModelPath);
            var ma = UnityEngine.Object.Instantiate(source, a.transform);
            var mb = UnityEngine.Object.Instantiate(source, b.transform);
            var md = UnityEngine.Object.Instantiate(source, d.transform);
            var copies = new PocOutlineTag.OutlineCopies();
            var made0 = PocOutlineTag.CopiesMade;
            PocOutlineTag.Apply(a, 7, 1f, copies);
            PocOutlineTag.Apply(b, 7, 1f, copies);
            PocOutlineTag.Apply(d, 8, 1f, copies);
            var fa = ma.GetComponentInChildren<MeshFilter>().sharedMesh;
            var fb = mb.GetComponentInChildren<MeshFilter>().sharedMesh;
            var fd = md.GetComponentInChildren<MeshFilter>().sharedMesh;
            var original = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            Check("the same model, outline id and strength share one tagged copy (sharing within a build)", fa == fb && fa != original && fa.name.EndsWith(PocOutlineTag.CopySuffix),
                $"copies made {PocOutlineTag.CopiesMade - made0}");
            Check("a different outline id gets its own copy (the 2D lines between neighbours stay)", fd != fa && Mathf.RoundToInt(fd.uv2[0].x) == 8 && Mathf.RoundToInt(fa.uv2[0].x) == 7);
            Check("the model's own mesh is never touched (no UV2 written into the catalog's mesh)", original.uv2 == null || original.uv2.Length == 0 || Mathf.RoundToInt(original.uv2[0].x) == 0);
            // after batching (here: b's filter points elsewhere, as static batching does) only the used copies stay
            var keep = new List<Mesh>();
            mb.GetComponentInChildren<MeshFilter>().sharedMesh = original;
            ma.GetComponentInChildren<MeshFilter>().sharedMesh = original;
            var released = PocOutlineTag.ReleaseBatched(a.transform.parent == null ? WrapAll(a, b, d) : a.transform, copies, keep);
            Check("ReleaseBatched: copies no renderer uses any more are destroyed, the used ones are kept with the build", released == 1 && fa == null && keep.Contains(fd) && fd != null,
                $"released {released} kept {keep.Count}");
            PocOutlineTag.FreeAll(copies);
            foreach (var mesh in keep) UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var go in new[] { a, b, d }) if (go != null) UnityEngine.Object.DestroyImmediate(go.transform.root.gameObject);
            var source2 = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.Chunks.cs"));
            Check("a chunk: copies released after static batching, the rest still freed with the chunk (ver3.7's FreePayload kept)",
                source2.Contains("PocOutlineTag.ReleaseBatched(root, copies, payload.Meshes);") && source2.Contains("foreach (var mesh in payload.Meshes)") &&
                source2.Contains("PocOutlineTag.FreeAll(copies);"));
            var room = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.cs"));
            Check("a room: the same (copies released after batching; FreeMeshes at the next map change)",
                room.Contains("PocOutlineTag.ReleaseBatched(mapRoot, copies, roomMeshes);") && room.Contains("FreeMeshes(roomMeshes);"));

            // The game: a round of the nine regions and the house, twice; meshes back to the same number.
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            life.SetMode(life.Rin, CompanionMode.Follow);
            life.SetMode(life.Len, CompanionMode.Follow);
            Settle(c, 1f);
            var start = c.Session.Cell;
            var counts = new List<int>();
            for (var round = 0; round < 2; round++)
            {
                foreach (var goal in new[] { new Vector2Int(0, 16), new Vector2Int(25, 16), new Vector2Int(27, -2), new Vector2Int(22, -18),
                             new Vector2Int(2, -20), new Vector2Int(-24, -17), new Vector2Int(-27, 2), new Vector2Int(-25, 15) })
                    Walk(c, goal, 120f);
                Walk(c, start, 120f);
                Settle(c, 2f);
                counts.Add(Meshes());
            }
            Check("a round of the nine regions with Rin and Len following, twice: the meshes the same after each (nothing left behind)",
                counts.Count == 2 && counts[0] == counts[1] && c.Session.Cell == start, string.Join(" / ", counts));
            var walkerModels = PocWalker.Active.Count(w => w.ModelInstance != null);
            Check("only drawn characters have a model (far ones keep only their logical state)",
                PocWalker.Active.All(w => w.Drawn == (w.ModelInstance != null)), $"models {walkerModels} walkers {PocWalker.Active.Count}");
        }

        private static Transform WrapAll(params GameObject[] items)
        {
            var root = new GameObject("wrap").transform;
            foreach (var item in items) item.transform.SetParent(root, true);
            return root;
        }

        // ---------------------------------------------------------------- time

        private static void TimeChecks()
        {
            const long anchor = 1767225600000L;
            long At(long realMs) => anchor + realMs;
            Check("the anchor is 06:00 of day 1 (a new game)", LifeClock.MinuteOfDay(anchor, At(0)) == 360 && LifeClock.ShownDay(anchor, At(0), 0) == 1 &&
                LifeClock.Text(1, 360) == "1日目 06:00");
            Check("a game day is one morning/night cycle (20 real minutes); a game minute 0.8333 s",
                LifeClock.GameMinutes(WorldTimeCycle.CycleMilliseconds) == 1440 && LifeClock.GameMinutes(2500) == 3 && LifeClock.RealMilliseconds(1440) == 1200000);
            var agree = true;
            for (var ms = 0L; ms < WorldTimeCycle.CycleMilliseconds * 3; ms += 7919)
            {
                var minute = LifeClock.MinuteOfDay(anchor, At(ms));
                var night = LifeClock.IsNight(minute);
                agree &= night == (WorldTimeCycle.PhaseAt(anchor, At(ms)) == WorldPhase.Night);
            }
            Check("the time shown always agrees with the morning / night look (sampled over three days)", agree);
            Check("06:00 and 18:00: the phase changes exactly there", LifeClock.MinuteOfDay(anchor, At(600000)) == 1080 &&
                WorldTimeCycle.PhaseAt(anchor, At(600000)) == WorldPhase.Night && WorldTimeCycle.PhaseAt(anchor, At(599999)) == WorldPhase.Morning &&
                LifeClock.MinuteOfDay(anchor, At(1200000)) == 360 && WorldTimeCycle.PhaseAt(anchor, At(1200000)) == WorldPhase.Morning);
            Check("00:00: the day goes up (15 real minutes after the anchor)", LifeClock.ShownDay(anchor, At(899999), 0) == 1 &&
                LifeClock.ShownDay(anchor, At(900000), 0) == 2 && LifeClock.MinuteOfDay(anchor, At(900000)) == 0);
            Check("no cap on days (day 100,001)", LifeClock.ShownDay(anchor, At(1200000L * 100000L), 0) == 100001);
            Check("the day counted from ver4.0's first start (an older save keeps its phase and starts at day 1)",
                LifeClock.ShownDay(anchor, At(1200000L * 40 + 1000), LifeClock.AbsoluteDay(anchor, At(1200000L * 40))) == 1);
            Check("a clock set before the anchor: day 1, 06:00 (never a negative day or time)", LifeClock.ShownDay(anchor, anchor - 99999999, 0) == 1 &&
                LifeClock.MinuteOfDay(anchor, anchor - 99999999) == 360);
            Check("the wait text: 05:42; never negative; at most 99:59", LifeClock.DurationText(342) == "05:42" && LifeClock.DurationText(-5) == "00:00" && LifeClock.DurationText(999999) == "99:59");

            var c = Begin(new HarukaMemoryStorage(), anchor);
            var life = L(c);
            Check("in the game: a new game starts on 1日目 06:00, the world clock's own anchor", life.ClockText == "1日目 06:00" && life.AnchorMs == c.Clock.AnchorMilliseconds,
                life.ClockText + " anchor " + life.AnchorMs);
            Advance(c, 600000 + 30000);
            Check("in the game: 18:00 and after: the night icon's time and the world's night together", life.IsNightNow && c.Clock.CurrentPhase == WorldPhase.Night && life.MinuteOfDay >= 1080,
                life.ClockText);
            Advance(c, 300000);
            Check("in the game: past 00:00 it is 2日目", life.ShownDay == 2 && life.MinuteOfDay < 60, life.ClockText);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var text3D = life.ClockText;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("2D / 3D switching never changes the time or the day", life.ClockText == text3D, text3D + " / " + life.ClockText);
            var phone = PocLifeHud.BoxFor(new Rect(0, 0, 375, 667), 667, 196f);
            var small = PocLifeHud.BoxFor(new Rect(0, 0, 320, 568), 568, 196f);
            var wide = PocLifeHud.BoxFor(new Rect(0, 0, 1280, 720), 720, 196f);
            bool Clear(Rect r, float w) => !r.Overlaps(new Rect(12, 12, 52, 48)) && !r.Overlaps(new Rect(w - 12 - 64, 12, 64, 48));
            Check("the clock box: between the menu and view buttons on a wide screen and a 375 px phone, under them on a 320 px phone (never over a button)",
                wide.y < 40f && Clear(wide, 1280) && phone.y < 40f && Clear(phone, 375) && small.y >= 56f && Clear(small, 320), $"{wide} {phone} {small}");
        }

        // ---------------------------------------------------------------- skills

        private static void SkillChecks()
        {
            Check("the score bands: 0-19 +0, 20-39 +1, 40-59 +2, 60-79 +3, 80-99 +4, 100 +5",
                CreatorState.GainFor(0) == 0 && CreatorState.GainFor(19) == 0 && CreatorState.GainFor(20) == 1 && CreatorState.GainFor(39) == 1 &&
                CreatorState.GainFor(40) == 2 && CreatorState.GainFor(60) == 3 && CreatorState.GainFor(79) == 3 && CreatorState.GainFor(80) == 4 &&
                CreatorState.GainFor(99) == 4 && CreatorState.GainFor(100) == 5 && CreatorState.GainFor(150) == 5 && CreatorState.GainFor(-3) == 0);
            var s = new CreatorState();
            s.ApplyScore(CreatorSkill.Composition, 85);
            s.ApplyScore(CreatorSkill.Drawing, 45);
            s.ApplyScore(CreatorSkill.Drawing, 10);
            Check("three separate abilities, records per game (plays, best, last)", s.Skills[0] == 4 && s.Skills[1] == 2 && s.Skills[2] == 0 &&
                s.Plays[1] == 2 && s.Best[1] == 45 && s.Last[1] == 10 && s.Plays[2] == 0 && s.Best[2] == -1);
            var huge = BigInteger.Pow(10, 500);
            s.Skills[2] = huge;
            s.ApplyScore(CreatorSkill.Singing, 100);
            Check("no cap: an ability of 10^500 still grows (+5)", s.Skills[2] == huge + 5);
            Check("beyond a long (9.2e18) and a double (1e308) too", BigInteger.Pow(10, 400) + 1 > new BigInteger(double.MaxValue) && LifeNumbers.ToSave(huge).Length == 501);
            var block = new LifeSaveBlock();
            block.Creator.Skills[0] = huge;
            block.Creator.Skills[1] = BigInteger.Parse("123456789012345678901234567890");
            var data = new HarukaSaveData { GameVersion = "ver4.0", SavedAtUtc = HarukaSaveData.UtcStamp(DateTimeOffset.UtcNow), TimeAnchorUnixMs = 1, MapId = "first_field", Life = block };
            data.Seal();
            var status = HarukaSaveData.TryRead(data.ToJson(), out var read, out var problem);
            Check("huge abilities saved as digit strings and read back exactly", status == HarukaSaveReadStatus.Valid && read.Life != null &&
                read.Life.Creator.Skills[0] == huge && read.Life.Creator.Skills[1] == block.Creator.Skills[1], status + " " + problem);
            Check("digits only: a sign, a letter, a leading zero, 4001 digits are refused",
                !LifeNumbers.TryParse("-5", out _) && !LifeNumbers.TryParse("12a", out _) && !LifeNumbers.TryParse("007", out _) &&
                !LifeNumbers.TryParse(new string('9', 4001), out _) && LifeNumbers.TryParse("0", out var zero) && zero.IsZero);
            Check("grouped and short display", LifeNumbers.Grouped(1234567) == "1,234,567" && LifeNumbers.Short(BigInteger.Pow(10, 20)) == "1.00e20" &&
                LifeNumbers.Short(999999999999) == "999,999,999,999");
            Check("the curve helpers never give Infinity, NaN or a negative value", !double.IsInfinity(LifeNumbers.Log10OnePlus(huge)) &&
                LifeNumbers.Log10OnePlus(-5) == 0d && LifeNumbers.Pow10(-1).IsZero && LifeNumbers.Pow10(double.NaN).IsZero && LifeNumbers.Pow10(500.2).Sign > 0);
        }

        // ---------------------------------------------------------------- mini games

        private static void GameChecks()
        {
            var host = new GameObject("games");
            var games = host.AddComponent<PocMinigames>();
            Check("DTM: all within 0.08 s = 100, all within 0.16 s = 60, none = 0, half perfect = 50",
                games.ScoreDtmFor(Enumerable.Repeat(0.03f, 40).ToList(), 40) == 100 && games.ScoreDtmFor(Enumerable.Repeat(0.12f, 40).ToList(), 40) == 60 &&
                games.ScoreDtmFor(new List<float>(), 40) == 0 && games.ScoreDtmFor(Enumerable.Repeat(0.03f, 20).ToList(), 40) == 50);
            var scores = new List<int>();
            for (var shape = 0; shape < PocMinigames.Subjects.Length; shape++)
            {
                var exact = PocMinigames.Shape(shape);
                var shaky = exact.Select((p, i) => p + new Vector2(Mathf.Sin(i) * 0.04f, Mathf.Cos(i * 1.3f) * 0.04f)).ToList();
                var wrong = exact.Select(p => p * 0.35f + new Vector2(0.5f, 0.5f)).ToList();
                scores.Add(games.ScoreDrawingFor(shape, exact, false));
                scores.Add(games.ScoreDrawingFor(shape, shaky, false));
                scores.Add(games.ScoreDrawingFor(shape, wrong, false));
            }
            Check("drawing: the five models (star, heart, flower, circle, cat): traced = 100, a shaky hand ≥ 85, the wrong place ≤ 40",
                Enumerable.Range(0, 5).All(i => scores[i * 3] >= 99 && scores[i * 3 + 1] >= 85 && scores[i * 3 + 2] <= 40), string.Join(",", scores));
            var off = PocMinigames.Shape(3).Select(p => p * 1.16f).ToList();
            Check("drawing: a finger gets a wider tolerance than the mouse", games.ScoreDrawingFor(3, off, true) > games.ScoreDrawingFor(3, off, false),
                games.ScoreDrawingFor(3, off, true) + " > " + games.ScoreDrawingFor(3, off, false));
            Check("drawing: nothing drawn = 0", games.ScoreDrawingFor(0, new List<Vector2>(), false) == 0);
            // (ふつう's band is 0.7 steps since the second look; it was 1.25, when half a step off gave 60)
            Check("singing: on the melody = 100, a quarter step off ≥ 55, far off = 0", games.ScoreSingingFor(0f) == 100 && games.ScoreSingingFor(0.25f) >= 55 &&
                games.ScoreSingingFor(3f) == 0);
            UnityEngine.Object.DestroyImmediate(host);

            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            var g = life.Minigames;
            c.Session.LoadMapForCheck(SoundBoothMap(), DeskStand(c, PocLife.DtmDeskId), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var pressed = c.Session.InteractAhead();
            Check("the DTM desk (sound booth): A opens its window: its own text, さっきょくする, ニコチューブをひらく, やめる",
                pressed && Page(c) != null && Page(c).Title == "DTMデスク" && Page(c).Buttons.SequenceEqual(new[] { "さっきょくする", PocLife.NicoOpenLabel, PocLife.CloseLabel }),
                Page(c) != null ? string.Join("/", Page(c).Lines) : "no page");
            Press(c, "さっきょくする");
            Check("さっきょくする: the DTM game's window opens; her input and AUTO wait (activity); the menu button is hidden",
                g.IsOpen && g.Skill == CreatorSkill.Composition && c.Gameplay.ActivityOpen && c.Session.InputSuspended && c.Menu.HiddenWhile());
            var before = life.State.Creator.Skills[0];
            g.Cancel();
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("やめる before or during play: closed, nothing given, nothing recorded, input back", !g.IsOpen && !c.Gameplay.ActivityOpen &&
                life.State.Creator.Skills[0] == before && life.State.Creator.Plays[0] == 0 && life.GamesCancelled == 1);
            // A finished game (the score the window computed is handed to the life controller)
            var writes = c.Save.Writes;
            var gain = (int)typeof(PocLife).GetMethod("OnGameFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(life, new object[] { CreatorSkill.Composition, 87 });
            Check("a finished game: さっきょくりょく +4 for 87 points, the records, saved at once", gain == 4 && life.State.Creator.Skills[0] == before + 4 &&
                life.State.Creator.Best[0] == 87 && c.Save.Writes == writes + 1 && c.Save.LastWritten.Life.Creator.Skills[0] == before + 4);
            c.Session.LoadMapForCheck(SoundBoothMap(), DeskStand(c, PocLife.TabletId), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("the drawing tablet: おえかきする", c.Session.InteractAhead() && Page(c) != null && Page(c).Buttons.Contains("おえかきする"));
            Press(c, "おえかきする");
            Check("おえかきする opens the drawing game", g.IsOpen && g.Skill == CreatorSkill.Drawing);
            g.Cancel();
            Frame(c, 1f / 60f, Vector2Int.zero);
            var mic = c.Session.World.Objects.First(o => o.DefinitionId == PocLife.MicId);
            var stand = Dirs.Select(dd => mic.RootCell + dd).First(cell => c.Session.World.CanEnter(cell));
            c.Session.PlaceForCheck(stand, FacingDirectionExtensions.FromVector(mic.RootCell - stand));
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("the microphone (no action point of its own): A in front of it opens うたう", c.Session.InteractAhead() && Page(c) != null &&
                Page(c).Buttons.Contains("うたう"));
            Press(c, "うたう");
            Check("うたう opens the singing game", g.IsOpen && g.Skill == CreatorSkill.Singing);
            g.Cancel();
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("after the games: her place and facing as before (the window never moved her)", c.Session.Cell == stand && !c.Gameplay.ActivityOpen);
        }

        private static string SoundBoothMap() => "halka_soundbooth";

        private static Vector2Int DeskStand(C6.Ctx c, string definitionId)
        {
            var world = c.Session.FindWorld(SoundBoothMap());
            var desk = world.Objects.First(o => o.DefinitionId == definitionId);
            var point = desk.Definition.ActionPoints.First();
            return desk.RootCell + point.PlayerCellOffset;
        }

        // ---------------------------------------------------------------- ﾆｺﾁｭｰﾌﾞ

        private static void NicoChecks()
        {
            var storage = new HarukaMemoryStorage();
            var c = Begin(storage);
            var life = L(c);
            var ch = life.State.Channel;
            Check("a new channel: 0 subscribers, views, money, posts; the first post 24 game hours away", ch.Subscribers.IsZero && ch.TotalViews.IsZero &&
                ch.Money.IsZero && ch.Posts == 0 && ch.RemainingGameMinutes(life.NowMs) == 1440 && !ch.CanPost(life.NowMs));
            life.OpenNicoTube(C6.Clock);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("the page: とうろくしゃ, さいせい, とうこう, おかね, the wait 「つぎのとうこうまで 24:00」, 動画を投稿する", Page(c) != null &&
                Page(c).Title == PocLife.NicoName && Page(c).Lines.Count == 5 && Page(c).Lines[4] == "つぎのとうこうまで 24:00" &&
                Page(c).Buttons[0] == PocLife.PostLabel, Page(c) != null ? string.Join(" / ", Page(c).Lines) : "");
            var writes = c.Save.Writes;
            Press(c, PocLife.PostLabel);
            Check("too early: nothing posted, nothing changed (a message says how long)", ch.Posts == 0 && life.PostRefusals == 1 && c.Save.Writes == writes);
            c.Menu.SetOpen(false, C6.Clock + 2f);
            Advance(c, LifeClock.RealMilliseconds(1439));
            Check("23 hours 59 minutes later: still closed (00:01 left)", !ch.CanPost(life.NowMs) && ch.RemainingGameMinutes(life.NowMs) == 1, life.CooldownLine(life.NowMs));
            Advance(c, LifeClock.RealMilliseconds(1) + 10);
            Check("24 game hours (20 real minutes) after the basis: 「とうこうできる！」", ch.CanPost(life.NowMs) && life.CooldownLine(life.NowMs) == PocLife.PostReadyLine);
            life.State.Creator.Skills[0] = 30;
            life.State.Creator.Skills[1] = 20;
            life.State.Creator.Skills[2] = 25;
            var preview = ch.Preview(life.State.Creator);
            life.OpenNicoTube(C6.Clock + 3f);
            Frame(c, 1f / 60f, Vector2Int.zero);
            writes = c.Save.Writes;
            Press(c, PocLife.PostLabel);
            var result = life.LastPost;
            Check("a post: 「動画を投稿した！」 and this post's views, subscribers and money on the page", Page(c) != null && Page(c).Lines[0] == PocLife.PostedLine &&
                Page(c).Lines[1].Contains(LifeNumbers.Short(result.Views)) && Page(c).Buttons.SequenceEqual(new[] { "とじる" }));
            Check("every number and the time changed together: views, subscribers, money, posts 1, the post time", ch.Posts == 1 && ch.TotalViews == result.Views &&
                ch.Subscribers == result.NewSubscribers && ch.Money == result.Money && ch.LastPostUnixMs == life.NowMs - 16 || ch.LastPostUnixMs > 0 && ch.Posts == 1);
            Check("money = this post's views / 10, rounded down", result.Money == result.Views / 10 && result.Views > 0, $"views {result.Views} money {result.Money}");
            Check("the result is the one the abilities gave (the preview before the post is the same post)", preview.Views == result.Views &&
                preview.NewSubscribers == result.NewSubscribers);
            Check("saved at once, the post in the file", c.Save.Writes == writes + 1 && c.Save.LastWritten.Life.Channel.Posts == 1 &&
                c.Save.LastWritten.Life.Channel.Money == result.Money);
            Check("a second press at once: no second post (the 24 hours start again)", !life.TryPost(out _) && ch.Posts == 1 &&
                ch.RemainingGameMinutes(life.NowMs) >= 1439);
            // a new day alone does not open it: post near 23:50, check after 00:10
            Check("the wait after a post: 24 game hours, never just a new day", !ch.CanPost(ch.LastPostUnixMs + LifeClock.RealMilliseconds(1439)) &&
                ch.CanPost(ch.LastPostUnixMs + LifeClock.RealMilliseconds(1440)) &&
                !ch.CanPost(ch.LastPostUnixMs + LifeClock.RealMilliseconds(20)) /* 23:50 -> 00:10 is a new day, 20 minutes */);
            c.Menu.SetOpen(false, C6.Clock + 4f);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var left = ch.RemainingGameMinutes(life.NowMs);
            Check("2D / 3D switching never opens or lengthens the wait", left >= 1438 && left <= 1440, left.ToString());
            c.Save.Flush();
            Frame(c, 1f / 60f, Vector2Int.zero);
            var before = (ch.Posts, ch.Subscribers, ch.TotalViews, ch.Money, ch.LastPostUnixMs);
            var restartAt = fakeNow + LifeClock.RealMilliseconds(600); // closed for 10 game hours
            var c2 = Begin(storage, restartAt);
            var life2 = L(c2);
            var ch2 = life2.State.Channel;
            Check("a restart 10 game hours later: the same channel, the wait counted on (about 14 hours left)",
                ch2.Posts == before.Posts && ch2.Subscribers == before.Item2 && ch2.TotalViews == before.Item3 && ch2.Money == before.Item4 &&
                ch2.LastPostUnixMs == before.LastPostUnixMs && Math.Abs(ch2.RemainingGameMinutes(life2.NowMs) - 840) <= 2,
                $"left {ch2.RemainingGameMinutes(life2.NowMs)}");
            // the clock set back
            Check("the OS clock set back an hour: the wait is shown as at most 24:00, never negative", ch2.RemainingGameMinutes(ch2.LastPostUnixMs - 3600000) == 1440 &&
                ch2.RemainingGameMinutes(ch2.LastPostUnixMs + LifeClock.RealMilliseconds(5000)) == 0);
            // abilities matter (on average), luck a little
            NicoTubeChannel Probe(BigInteger skill) => new NicoTubeChannel { Seed = 99, BasisUnixMs = 1 };
            var low = new CreatorState(); var high = new CreatorState();
            for (var i = 0; i < 3; i++) { low.Skills[i] = 10; high.Skills[i] = 1000; }
            double Avg(CreatorState s) => Enumerable.Range(1, 200).Average(n => (double)Probe(0).Compute(s, n).Views);
            var lowAvg = Avg(low);
            var highAvg = Avg(high);
            Check("higher abilities: more views on average (not doubled by doubling: a curve)", highAvg > lowAvg * 10 && highAvg < lowAvg * 100,
                $"T 30: {lowAvg:0}  T 3000: {highAvg:0}");
            var lucks = Enumerable.Range(1, 500).Select(n => NicoTubeChannel.Luck(7, n)).ToList();
            Check("luck stays 0.80-1.25 and is the same for the same post (a reload never rolls again)", lucks.Min() >= 0.8 && lucks.Max() <= 1.25 &&
                NicoTubeChannel.Luck(7, 3) == NicoTubeChannel.Luck(7, 3));
            var unbalanced = new CreatorState(); unbalanced.Skills[0] = 3000;
            double AvgSubs(CreatorState s) => Enumerable.Range(1, 200).Average(n => (double)Probe(0).Compute(s, n).NewSubscribers);
            Check("all three grown evenly win more subscribers than one alone with the same total (the three are read)", AvgSubs(high) > AvgSubs(unbalanced) * 2,
                $"{AvgSubs(high):0} vs {AvgSubs(unbalanced):0}");
            var giant = new CreatorState();
            for (var i = 0; i < 3; i++) giant.Skills[i] = BigInteger.Pow(10, 3000);
            var giantChannel = new NicoTubeChannel { Seed = 5, BasisUnixMs = 1, Subscribers = BigInteger.Pow(10, 2000) };
            var g = giantChannel.Compute(giant, 1);
            Check("huge abilities and subscribers: a huge, positive result (no Infinity, NaN, exception)", g.Views.Sign > 0 && g.NewSubscribers.Sign > 0 &&
                g.Money == g.Views / 10 && LifeNumbers.ToSave(g.Views).Length > 2000);
            // growth towards 1,000,000 (the simulation in the spec, here as a check)
            var sim = new NicoTubeChannel { Seed = 12345, BasisUnixMs = 1 };
            var cs = new CreatorState();
            var posts = 0;
            for (; posts < 600 && sim.Subscribers < 1000000; posts++)
            {
                for (var i = 0; i < 3; i++) cs.Skills[i] += 10;   // 30 per day: a few games a day
                sim.LastPostUnixMs = 0; sim.BasisUnixMs = 1;
                sim.TryPost(cs, LifeClock.RealMilliseconds(1440) + 2, out _);
            }
            Check("1,000,000 subscribers is reachable: about 30 ability points a game day reach it within 300 posts (100 hours of play), and it keeps growing",
                sim.Subscribers >= 1000000 && posts <= 300, $"{posts} posts");
            Check("no genre, title, thumbnail or comments were added (the page has only the numbers and the post)",
                !File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/Life/PocLife.cs")).Contains("ジャンル"));
        }

        // ---------------------------------------------------------------- passers-by

        private static void NpcChecks()
        {
            Check("the chance: 0 → 0 %, 10,000 → 10 %, 100,000 → 31.6 %, 500,000 → 70.7 %, 1,000,000 → 100 %, beyond → 100 %",
                PocLife.NpcChance(0) == 0 && Math.Abs(PocLife.NpcChance(10000) - 0.1) < 0.001 && Math.Abs(PocLife.NpcChance(100000) - 0.3162) < 0.001 &&
                Math.Abs(PocLife.NpcChance(500000) - 0.7071) < 0.001 && PocLife.NpcChance(1000000) == 1 && PocLife.NpcChance(BigInteger.Pow(10, 900)) == 1);
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            Check("no subscribers: nobody about", life.NpcCount == 0);
            life.State.Channel.Subscribers = 100000;
            var hits = 0;
            for (var i = 0; i < 300; i++)
            {
                typeof(PocLife).GetMethod("ClearNpcs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(life, null);
                life.EvaluateNpcs("check");
                if (life.NpcCount > 0) hits++;
            }
            Check("100,000 subscribers: someone about in about 31.6 % of evaluations (300 rolls)", hits > 300 * 0.22 && hits < 300 * 0.42, $"{hits}/300");
            typeof(PocLife).GetMethod("ClearNpcs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(life, null);
            life.State.Channel.Subscribers = 1000000;
            var kinds = new Dictionary<string, int>();
            var max = 0;
            var spawnOk = true;
            var her = c.Session.Cell;
            for (var i = 0; i < 120; i++)
            {
                life.EvaluateNpcs("check");
                max = Mathf.Max(max, life.NpcCount);
                foreach (var w in life.NpcWalkers())
                {
                    kinds[w.Kind] = kinds.TryGetValue(w.Kind, out var k) ? k + 1 : 1;
                    spawnOk &= c.Session.OutdoorWorld.CanEnter(w.Cell) && life.Reachable.Contains(w.Cell) && !life.ActionCells.Contains(w.Cell) &&
                        !c.Session.IsTransitionCell(c.Session.OutdoorWorld, w.Cell);
                }
                if (life.NpcCount == 0) spawnOk = false;
                typeof(PocLife).GetMethod("ClearNpcs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(life, null);
            }
            Check("1,000,000 subscribers: someone about after every evaluation, never more than 3", spawnOk && max <= 3 && max >= 2, $"max {max}");
            var fans = kinds.Where(p => p.Key.StartsWith("fan")).Sum(p => p.Value);
            var all = kinds.Values.Sum();
            Check("the four kinds all appear; fans about 75 %", kinds.Count == 4 && fans > all * 0.62 && fans < all * 0.87,
                string.Join(", ", kinds.Select(p => p.Key + " " + p.Value)));
            life.EvaluateNpcs("check");
            Check("where they stand: walkable, reachable on foot, never a door or a stand cell, at least 12 cells from her", spawnOk &&
                life.NpcWalkers().All(w => Mathf.Abs(w.Cell.x - her.x) + Mathf.Abs(w.Cell.y - her.y) >= 12));
            // the guarantee: the last one leaves → another comes
            var first = life.NpcWalkers().ToList();
            foreach (var w in first) typeof(PocLife).GetField("npcs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Advance(c, 21L * 60L * 1000L);   // past everyone's stay
            Settle(c, 1f);
            Check("from 1,000,000: when they go (their time over, out of sight) another is about at once (never nobody)", life.NpcCount >= 1 &&
                life.NpcDepartures >= 1, $"departures {life.NpcDepartures} now {life.NpcCount} guarantee {life.NpcGuaranteeSpawns}");
            // logical state survives chunks: a far one has no model, its place kept
            var far = life.NpcWalkers().FirstOrDefault(w => !w.Drawn);
            Check("a far passer-by: no model (its chunk not drawn), its cell and kind kept", far == null || far.ModelInstance == null && far.Present,
                far != null ? far.Kind + " " + far.Cell : "all drawn");
            var near = life.NpcWalkers().First();
            var toNear = Route(c, c.Session.Cell, near.Cell);
            // up to the cell before it (walking into it would make it step aside)
            Walk(c, toNear != null && toNear.Count > 1 ? toNear[toNear.Count - 2] : c.Session.Cell, 120f);
            Settle(c, 0.5f);
            var target = life.NpcWalkers().OrderBy(w => Mathf.Abs(w.Cell.x - c.Session.Cell.x) + Mathf.Abs(w.Cell.y - c.Session.Cell.y)).First();
            Check("walking there draws it (its chunk drawn: the model built)", target.Drawn && target.ModelInstance != null, target.Kind + " " + target.Cell);
            var talked = ApproachAndTalk(c, target);
            Check("A in front of a fan or an anti: its line (ファン「いつもみてるよ！」 / アンチ「そんなに にんきなの？」)", talked && life.NpcTalks >= 1 &&
                (PocLife.FanLines.Contains(life.LastNpcLine) || PocLife.AntiLines.Contains(life.LastNpcLine)),
                $"{life.LastNpcLine} (her {c.Session.Cell}, {target.Kind} {target.Cell})");
            var count = life.NpcCount;
            var ids = life.NpcWalkers().Select(w => w.Id).OrderBy(x => x).ToList();
            c.Save.Flush();
            Frame(c, 1f / 60f, Vector2Int.zero);
            var c2 = Begin(c.Storage, fakeNow + 1000);
            var life2 = L(c2);
            Check("a restart: the same passers-by (the same ids, not one more)", life2.NpcCount == count &&
                life2.NpcWalkers().Select(w => w.Id).OrderBy(x => x).SequenceEqual(ids), $"{count} -> {life2.NpcCount}");
            Check("the same game day: no new roll at the start (closing the page never rolls again)", life2.NpcEvaluations == 0 ||
                life2.LastNpcEvaluation.StartsWith("start: guarantee"), life2.LastNpcEvaluation);
            Check("no character ever stood in a blocked cell", blockedCellFrames == 0, blockedCellFrames.ToString());
        }

        // ---------------------------------------------------------------- Rin and Len

        private static void CompanionChecks()
        {
            var root = PocSceneBuilder.RepositoryRoot;
            var rinManifest = File.ReadAllText(Path.Combine(root, "blender/exports/v40_rin_onehead_manifest.json"));
            var lenManifest = File.ReadAllText(Path.Combine(root, "blender/exports/v40_len_onehead_manifest.json"));
            Check("Rin and Len are the user's models (made from copies of rin ver2.0.blend / len ver2.0.blend; manifests)",
                rinManifest.Contains("\"who\": \"rin\"") && lenManifest.Contains("\"who\": \"len\"") && rinManifest.Contains("kami_matome") &&
                lenManifest.Contains("Len_hair") && rinManifest.Contains("atama"));
            var looks = PocSceneBuilderVer40.Looks();
            Check("their 3D models, materials and 2D sprites (4 directions, idle and two walk frames) are all there",
                looks.Length == 6 && looks.All(l => l.model != null && l.material != null && l.sprites.IsComplete));
            // Like the original file, Rin and Len are drawn from both sides (one of Len's eyes and some hair strands face inward)
            var tint = File.ReadAllText(Path.Combine(root, "unity-project/Assets/POC2D3D/Shaders/PocToonTextureTint.shader"));
            var mask = File.ReadAllText(Path.Combine(root, "unity-project/Assets/POC2D3D/Shaders/PocOutlineMask.shader"));
            var rinLenLooks = looks.Where(l => l.id == PocLife.RinId || l.id == PocLife.LenId).ToList();
            Check("Rin and Len drawn from both sides, as in Blender (no eye or hair strand lost to back-face culling), the outline mask too, nothing else changed",
                rinLenLooks.Count == 2 && rinLenLooks.All(l => l.material.shader.name == PocSceneBuilderVer40.TextureTintShader && l.material.GetTag("RenderType", false) == "OpaqueTwoSided") &&
                tint.Contains("Cull Off") && tint.Contains("VFACE") && mask.Contains("\"RenderType\" = \"OpaqueTwoSided\"") && mask.Contains("\"RenderType\" = \"Opaque\""));
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            var rin = life.Rin;
            var len = life.Len;
            Check("at the start: Rin and Len near the house, free, on free walkable cells", rin.Present && len.Present && life.ModeOf(rin) == CompanionMode.Free &&
                c.Session.OutdoorWorld.CanEnter(rin.Cell) && c.Session.OutdoorWorld.CanEnter(len.Cell) && rin.Cell != len.Cell, $"{rin.Cell} {len.Cell}");
            Settle(c, 20f);
            var home = c.Session.OutdoorIndex.RegionAt(rin.Cell);
            Check("free for 20 s: they potter about in the field near home (findable)", rin.StepsTaken + len.StepsTaken > 0 &&
                home != null && home.MapId == "first_field" && c.Session.OutdoorIndex.RegionAt(len.Cell)?.MapId == "first_field");
            Check("2D: they are drawn as sprites; 3D: as their models", rin.Drawn && rin.CurrentSprite != null && rin.ModelInstance != null);
            // talk to Rin: ついてきて
            Check("A in front of Rin: her window (ついてきて, かくれんぼ, やめる)", StandNextTo(c, rin.Cell) && c.Session.InteractAhead() && Page(c) != null &&
                Page(c).Title == PocLife.RinName && Page(c).Buttons.SequenceEqual(new[] { PocLife.ChoiceFollow, PocLife.ChoiceHide, PocLife.CloseLabel }));
            Press(c, PocLife.ChoiceFollow);
            Check("ついてきて: Rin follows, Len still free (each on her own)", life.ModeOf(rin) == CompanionMode.Follow && life.ModeOf(len) == CompanionMode.Free);
            sharedCellFrames = 0;
            Walk(c, new Vector2Int(0, 16), 120f);
            Settle(c, 3f);
            var d = Mathf.Abs(rin.Cell.x - c.Session.Cell.x) + Mathf.Abs(rin.Cell.y - c.Session.Cell.y);
            Check("Rin walked with her to the North (over the region border), close behind", d <= 3 && c.Session.OutdoorIndex.RegionAt(rin.Cell)?.MapId == "north_area_01",
                $"distance {d} at {rin.Cell}");
            Check("Len stayed home (free)", c.Session.OutdoorIndex.RegionAt(len.Cell)?.MapId == "first_field");
            life.SetMode(len, CompanionMode.Follow);
            // and the crow
            c.Crow.SetMode(CrowCompanionMode.FollowPlayer, C6.Clock);
            Walk(c, new Vector2Int(-25, 15), 160f);
            Settle(c, 4f);
            Walk(c, new Vector2Int(-27, 2), 120f);
            var lenSteps = len.StepsTaken;
            Settle(c, 4f);
            var lenPaths = (Dictionary<PocWalker, List<Vector2Int>>)typeof(PocLife).GetField("paths", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(life);
            var lenWhy = $" mode {life.ModeOf(len)} moving {len.IsMoving} steps {len.StepsTaken - lenSteps} path {lenPaths[len].Count} drawn {len.Drawn}";
            {
                var spotArgs = new object[] { len, Vector2Int.zero };
                var spotOk = (bool)typeof(PocLife).GetMethod("TryFollowSpot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(life, spotArgs);
                var probe = new List<Vector2Int>();
                var pathOk = life.TryPath(len, (Vector2Int)spotArgs[1], 24, probe);
                lenWhy += $" spot {spotOk} {(Vector2Int)spotArgs[1]} path {pathOk} {probe.Count} reachable {life.Reachable.Contains(len.Cell)} canEnterHere {life.CharacterCanEnter(len.Cell, len)}";
                foreach (var dd in Dirs) lenWhy += $" {len.Cell + dd}:{life.CharacterCanEnter(len.Cell + dd, len)}";
            }
            var dl = Mathf.Abs(len.Cell.x - c.Session.Cell.x) + Mathf.Abs(len.Cell.y - c.Session.Cell.y);
            var dr = Mathf.Abs(rin.Cell.x - c.Session.Cell.x) + Mathf.Abs(rin.Cell.y - c.Session.Cell.y);
            Check("Rin, Len and the crow all following: over North → North-West → West, all close", dr <= 4 && dl <= 4 && c.Crow.Following,
                $"rin {dr} len {dl} (len {len.Cell} her {c.Session.Cell} rin {rin.Cell})" + lenWhy);
            Check("nobody ever shared a cell with her, the crow or each other (standing)", sharedCellFrames == 0, sharedCellFrames + " frames");
            // そらをとぶ
            var travel = c.Gameplay.FastTravel;
            travel.SetFound(new List<string> { "landmark_east_flower_plaza" });
            var landed = 0;
            travel.Completed += ok => { if (ok) landed++; };
            var started = travel.TryBegin("landmark_east_flower_plaza", C6.Clock, FastTravelSource.Crow) == FastTravelStart.Started;
            for (var i = 0; i < 600 && travel.Busy; i++) Frame(c, 1f / 60f, Vector2Int.zero);
            Settle(c, 1f);
            var drt = Mathf.Abs(rin.Cell.x - c.Session.Cell.x) + Mathf.Abs(rin.Cell.y - c.Session.Cell.y);
            var dlt = Mathf.Abs(len.Cell.x - c.Session.Cell.x) + Mathf.Abs(len.Cell.y - c.Session.Cell.y);
            Check("そらをとぶ to the East plaza: Rin and Len land beside her (followers only)", started && landed == 1 && drt <= 3 && dlt <= 3 && life.CompanionLandings >= 2,
                $"rin {drt} len {dlt}");
            // indoors: wait beside the door, then follow again
            c.Session.LoadMapForCheck("halka_house", new Vector2Int(5, 2), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("indoors: followers wait beside the door (not drawn, not on the door's step)", life.Waiting(rin) && life.Waiting(len) && rin.HiddenByGame &&
                !rin.Drawn && rin.Cell != c.Session.OutdoorWorld.TryGetHouseDoorCell() + Vector2Int.down);
            c.Session.LoadMapForCheck("first_field", c.Session.OutdoorWorld.TryGetHouseDoorCell() + Vector2Int.down, FacingDirection.Down);
            Settle(c, 2f);
            Check("back outdoors: they show again and follow", !rin.HiddenByGame && rin.Drawn && life.ModeOf(rin) == CompanionMode.Follow);
            // Rin standing in her way below the door: she keeps walking into her, Rin steps aside
            var her0 = c.Session.Cell;
            var ahead = her0 + Vector2Int.down;
            var placed = life.CharacterCanEnter(ahead, rin);
            if (placed) rin.Place(ahead, FacingDirection.Up);
            var yields0 = life.WalkerYields;
            for (var i = 0; i < 90; i++) Frame(c, 1f / 60f, Vector2Int.down);
            Check("she keeps walking into Rin in her way: Rin steps aside (as the crow does), she gets through", placed &&
                life.WalkerYields > yields0 && c.Session.Cell != her0 && rin.Cell != c.Session.Cell, $"her {her0} -> {c.Session.Cell}, rin {rin.Cell}");
            // あそんでおいで from far: they go home
            Walk(c, new Vector2Int(22, -18), 160f);
            Settle(c, 3f);
            life.SetMode(rin, CompanionMode.Free);
            life.SetMode(len, CompanionMode.Free);
            Walk(c, new Vector2Int(2, -20), 160f);
            Settle(c, 30f);
            Check("あそんでおいで far from home: they make their way back to the field (never lost at the edge of the map)",
                c.Session.OutdoorIndex.RegionAt(rin.Cell)?.MapId == "first_field" && c.Session.OutdoorIndex.RegionAt(len.Cell)?.MapId == "first_field",
                $"{rin.Cell} {len.Cell}");
            // the save
            life.SetMode(rin, CompanionMode.Follow);
            c.Save.Flush();
            Frame(c, 1f / 60f, Vector2Int.zero);
            var c2 = Begin(c.Storage, fakeNow + 1000);
            var life2 = L(c2);
            Check("a restart: Rin following, Len free (each mode kept), nobody duplicated", life2.ModeOf(life2.Rin) == CompanionMode.Follow &&
                life2.ModeOf(life2.Len) == CompanionMode.Free && PocWalker.Active.Count(w => w.Kind == PocLife.RinId) == 1);
            Check("no character ever stood in a blocked cell", blockedCellFrames == 0);
        }

        // ---------------------------------------------------------------- hide-and-seek

        private static void HideChecks()
        {
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            var rin = life.Rin;
            var len = life.Len;
            foreach (var mode in new[] { HideSeekMode.Rin, HideSeekMode.Len, HideSeekMode.Both })
            {
                Walk(c, new Vector2Int(-2, 0), 60f);
                Settle(c, 1f);
                var started = life.StartHideSeek(mode);
                Check(mode + ": starts; she turns her back, her input waits while they hide", started && life.HideSeekCounting && c.Gameplay.ActivityOpen &&
                    c.Session.Facing == FacingDirection.Up);
                Settle(c, PocLife.HideCountMaxSeconds + 0.5f);
                var hiders = mode == HideSeekMode.Rin ? new[] { rin } : mode == HideSeekMode.Len ? new[] { len } : new[] { rin, len };
                var her = c.Session.Cell;
                Check(mode + ": 「もういいよ！」 — hidden on free walkable cells in the garden, at least 8 cells away, next to something, never a door or stand cell",
                    !life.HideSeekCounting && !c.Gameplay.ActivityOpen && hiders.All(h => c.Session.OutdoorWorld.CanEnter(h.Cell) &&
                        Mathf.Abs(h.Cell.x - her.x) + Mathf.Abs(h.Cell.y - her.y) >= PocLife.HideMinDistance && !life.ActionCells.Contains(h.Cell) &&
                        Dirs.Concat(new[] { new Vector2Int(1, -1), new Vector2Int(-1, -1) }).Any(dd => c.Session.OutdoorWorld.IsBlocked(h.Cell + dd)) &&
                        c.Session.OutdoorIndex.RegionAt(h.Cell) != null),
                    string.Join(" ", hiders.Select(h => h.Cell + " " + c.Session.OutdoorIndex.RegionAt(h.Cell)?.MapId)));
                var spots = hiders.Select(h => h.Cell).ToList();
                Settle(c, 5f);
                Check(mode + ": they stay hidden (no wandering during the game)", hiders.Select(h => h.Cell).SequenceEqual(spots));
                if (mode == HideSeekMode.Rin)
                {
                    Settle(c, PocLife.FirstHintSeconds);
                    Check("a hint after 40 s: 「リンは …のほうに いるみたい！」 (no penalty)", life.HideSeekHints >= 1 && life.LastHint.StartsWith("リンは ") &&
                        life.LastHint.EndsWith("いるみたい！"), life.LastHint);
                }
                foreach (var h in hiders)
                {
                    var ok = StandNextTo(c, h.Cell) && c.Session.InteractAhead();
                    Check(mode + ": A in front of " + (h == rin ? "Rin" : "Len") + ": found", ok);
                    if (mode == HideSeekMode.Both && h == hiders[0])
                    {
                        // after the second look: talking to the one found while the other still hides
                        var talks = life.HideSeekTalks;
                        var again = StandNextTo(c, h.Cell) && c.Session.InteractAhead();
                        Check("both: A at Rin again before Len is found: her own line (「レンは まだ かくれてるよ！ …」), never the choices (あそんでおいで …)",
                            again && c.Session.LastMessage == PocLife.FoundRinLine && life.HideSeekTalks == talks + 1 && Page(c)?.Title != "リン" && life.HideSeekActive, c.Session.LastMessage);
                    }
                }
                Check(mode + ": everyone found: the game ends, the time recorded, each back to its own mode", !life.HideSeekActive && life.LastClearSeconds >= 0f &&
                    life.ModeOf(rin) == CompanionMode.Free && life.ModeOf(len) == CompanionMode.Free);
            }
            // the whole garden: many games' places spread over the regions; a game can start anywhere outdoors
            var regions = new HashSet<string>();
            var far = 0;
            for (var game = 0; game < 16; game++)
            {
                Settle(c, 0.3f);
                if (!life.StartHideSeek(HideSeekMode.Both)) continue;
                foreach (var spot in life.HideSpotCells)
                {
                    regions.Add(c.Session.OutdoorIndex.RegionAt(spot)?.MapId ?? "?");
                    if (Mathf.Abs(spot.x - c.Session.Cell.x) + Mathf.Abs(spot.y - c.Session.Cell.y) > 18) far++;
                }
                life.StopHideSeek(false);
            }
            Check("the whole garden: 16 games' places in several regions (the field alone before), some farther than the old 18 cells",
                regions.Count >= 3 && far > 0, string.Join(", ", regions) + " | far " + far);
            Check("the hiders dash (a step in 0.05 s: four times her walk, the user's 「かなり速め」)", PocLife.HideDashStepSeconds <= 0.06f);
            var hereBefore = c.Session.Cell;
            c.Session.PlaceForCheck(new Vector2Int(0, 16), FacingDirection.Up);   // the North area
            Frame(c, 1f / 60f, Vector2Int.zero);
            var northOk = life.CanStartHideSeek(out var northWhy);
            c.Session.PlaceForCheck(hereBefore, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("a game can begin in another region too (the North area); indoors it says 「かくれんぼは にわで しよう！」",
                northOk && northWhy == null, northWhy);
            var h2 = life.State.HideSeek;
            Check("the records: Rin found 2, Len found 2, both cleared 1, the best times", h2.RinFound == 2 && h2.LenFound == 2 && h2.BothCleared == 1 &&
                h2.BestRinSeconds >= 0 && h2.BestLenSeconds >= 0 && h2.BestBothSeconds >= 0, life.LastHint);
            // stopping, and a save in the middle
            Walk(c, new Vector2Int(-2, 0), 60f);
            life.SetMode(len, CompanionMode.Follow);
            life.StartHideSeek(HideSeekMode.Both);
            Settle(c, PocLife.HideCountMaxSeconds + 0.5f);
            c.Save.Flush();
            Frame(c, 1f / 60f, Vector2Int.zero);
            var saved = c.Save.LastWritten.Life;
            Check("a save during the game keeps each one's mode from before it (Len following, Rin free)", saved.Len.Mode == CompanionMode.Follow &&
                saved.Rin.Mode == CompanionMode.Free);
            c.Hud.ClearMessage();   // (the menu does not open over a message, as always)
            c.Menu.SetOpen(true, C6.Clock + 5f);
            var link = c.Menu.LinkLabels.ToList().IndexOf(PocLife.HideSeekStopLabel);
            Check("the menu shows かくれんぼをやめる during the game", link >= 0 && c.Menu.IsLinkShown(link));
            var stoppedBefore = life.HideSeekStopped;
            c.Menu.ActivateLink(link, C6.Clock + 6f);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("かくれんぼをやめる: stopped, Len follows again, Rin free again, nobody left hidden", !life.HideSeekActive && life.ModeOf(len) == CompanionMode.Follow &&
                life.ModeOf(rin) == CompanionMode.Free && life.HideSeekStopped == stoppedBefore + 1);
            var c2 = Begin(c.Storage, fakeNow + 1000);
            var life2 = L(c2);
            Check("a restart after a save in the middle: no game going on, both visible and walking normally", !life2.HideSeekActive &&
                life2.Rin.Present && life2.Len.Present && !life2.Rin.HiddenByGame);
            c2.Session.LoadMapForCheck("halka_house", new Vector2Int(0, -3), FacingDirection.Up);
            Frame(c2, 1f / 60f, Vector2Int.zero);
            Check("indoors: no hide-and-seek (「かくれんぼは にわで しよう！」)", !life2.CanStartHideSeek(out var indoorWhy) && indoorWhy == "かくれんぼは にわで しよう！", indoorWhy);
        }

        // ---------------------------------------------------------------- save

        private static void SaveChecks()
        {
            // a ver3.9 file (no life data): the first ver4.0 start begins it, once
            var storage = new HarukaMemoryStorage();
            var c = C6.Start(storage);
            c.Save.Flush();
            var old = storage.Files.Values.FirstOrDefault(t => t.Contains("harukaworld-save"));
            Check("a ver3.9-like save (written without ver4.0's controller) has no life data", old != null && !old.Contains("life40"));
            var c2 = Begin(storage);
            var life = L(c2);
            Check("ver4.0's first start on it: Life Log, place and settings kept; ver4.0's data from its defaults (day 1, abilities 0)",
                life.StartSource.StartsWith("older save") && life.ShownDay == 1 && life.State.Creator.Skills.All(s => s.IsZero) && c2.LifeLog.TotalSteps == c.LifeLog.TotalSteps,
                life.StartSource);
            Settle(c2, 1.5f);
            Check("…and saved at once (the life block now in the file)", c2.Save.LastWritten != null && c2.Save.LastWritten.Life != null);
            life.State.Creator.Skills[1] = 77;
            life.Changed();
            Settle(c2, 1.5f);
            var c3 = Begin(storage, fakeNow + 5000);
            var life3 = L(c3);
            Check("the next start: from the saved block (no second migration: がりょく 77 kept)", life3.StartSource == "save" && life3.State.Creator.Skills[1] == 77);
            // export / import
            var text = c3.Save.ExportText();
            Check("export: the file holds the life block", text.Contains("\"life40\"") && text.Contains("\"nicotube\""));
            life3.State.Creator.Skills[1] = 1;
            life3.Changed();
            var check = c3.Save.CheckImport(text, out var imported);
            var applied = check == HarukaSaveSystem.ImportCheck.Ok && c3.Save.ApplyImport(imported, C6.Clock, out var why);
            Check("import: the abilities and channel come back exactly (がりょく 77)", applied && life3.State.Creator.Skills[1] == 77, check.ToString());
            // a damaged block
            var damaged = text.Replace("\"plays\": 0", "\"plays\": 1");
            Check("a damaged life block (a value changed, the checksum not): the file is Corrupt (the snapshot store falls back)",
                HarukaSaveData.TryRead(damaged, out _, out var problem) == HarukaSaveReadStatus.Corrupt && problem.Contains("life40"), problem);
            var futureBlock = imported.Life.Copy();
            futureBlock.Channel.LastPostUnixMs = fakeNow + 999999999;
            futureBlock.Channel.Posts = 1;
            futureBlock.Npcs.Add(new LifeSaveBlock.NpcRecord { Id = 99, Kind = "dragon", Region = "first_field", X = 0, Y = 0 });
            futureBlock.Npcs.Add(new LifeSaveBlock.NpcRecord { Id = 98, Kind = "fan_male", Region = "nowhere", X = 0, Y = 0 });
            var repairs = life3.Repairs;
            life3.Sanitize(futureBlock, fakeNow);
            Check("odd values repaired, never refused: a post time in the future becomes now (a day's wait at most), an unknown kind or region drops that NPC only",
                futureBlock.Channel.LastPostUnixMs == fakeNow && futureBlock.Npcs.Count == 0 && life3.Repairs >= repairs + 3);
            Check("Schema 1 kept (ver3.x read the file and ignore the extension)", HarukaSaveData.SchemaVersion == 1);
        }

        // ---------------------------------------------------------------- existing

        private static void ExistingChecks()
        {
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            Check("the nine regions and そらをとぶ's five places", c.Session.OutdoorIndex.Regions.Count == 9 && c.Gameplay.FastTravel.Registry.points.Count == 5);
            Check("the crow: still the first to answer A (the roster asks it first)", c.Session.TalkTarget == life && c.Session.GroundOccupant == life);
            var crow = c.Crow;
            var talked = crow.isActiveAndEnabled && StandNextTo(c, crow.Cell) && c.Session.InteractAhead() && crow.ChoiceOpen;
            Check("talking to the crow opens its choices as before", talked);
            c.Menu.SetOpen(false, C6.Clock + 1f);
            Frame(c, 1f / 60f, Vector2Int.zero);
            c.Menu.SetOpen(true, C6.Clock + 2f);
            Check("the menu: そうさく added (セーブデータ and やめる as before)", c.Menu.LinkLabels.Contains(PocLife.MenuLabel) &&
                c.Menu.LinkLabels.Contains(HarukaSaveSystem.MenuLabel) && c.Menu.CloseLabel == HarukaSaveSystem.CloseLabel);
            var link = c.Menu.LinkLabels.ToList().IndexOf(PocLife.MenuLabel);
            c.Menu.ActivateLink(link, C6.Clock + 3f);
            Check("そうさく: the abilities, the channel, money and the clock", Page(c) != null && Page(c).Title == "のうりょく" && Page(c).Lines.Count == 6 &&
                Page(c).Lines[0].StartsWith("さっきょくりょく") && Page(c).Lines[5] == life.ClockText);
            c.Menu.SetOpen(false, C6.Clock + 4f);
            Check("the house door and the sound booth still open (a room loads)", c.Session.FindWorld("halka_house") != null && c.Session.FindWorld("halka_soundbooth") != null);
            Check("the font has every character ver4.0 writes (no ? boxes)", new[] { "動画を投稿した！", PocLife.NicoName, PocLife.NicoOpenLabel, "3日目 14:35", "さっきょくりょく +4", "がりょく +3",
                "かしょうりょく +5", "つぎのとうこうまで 05:42", "リンは きたのほうに いるみたい！", "ファン「いつもみてるよ！」", "アンチ「そんなに にんきなの？」",
                "かくれんぼをやめる", "↑↓（W S）か ゆびで うえしたに", "+0～12", "むずかしさを えらんでね", "かんたん", "ふつう", "やばい",
                "（やばい）", PocLife.NotSleepyLine, PocLife.SleptLine + " 01:00", "かくれんぼは にわで しよう！", "リンは とおくの きたにしのほうに いるみたい！",
                PocLife.FoundRinLine, PocLife.FoundLenLine, "リン" + PocLife.HidingLine, "レン" + PocLife.HidingLine, "おえかき：いろいろ 5こ", "おえかき：さんかくとしかく" }
                .Concat(PocMinigames.Subjects)
                .All(GameBitmapFont.HasAllGlyphs));
            Check("ニコチューブ is written in full-width katakana (the font's half-width kana are squeezed and lose ﾌﾞ's voicing mark)",
                PocLife.NicoName == "ニコチューブ" && PocLife.NicoOpenLabel.StartsWith("ニコチューブ") &&
                !(PocLife.NicoName + PocLife.NicoOpenLabel).Any(ch => ch >= 'ｦ' && ch <= 'ﾟ'));
        }

        // ---------------------------------------------------------------- the garden (after the user's first look)

        private static WorldObjectDefinition Def(string id) =>
            AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/" + id + ".asset");

        private static readonly string[] FlowerIds = { "flower_basic", "flower_yellow", "flower_blue", "sunflower_cluster" };

        private static void GardenChecks()
        {
            Check("the flowers are passable (no blocking, no blocked cell); trees, stones and signs still block",
                FlowerIds.All(id => Def(id) != null && !Def(id).BlocksMovement && !Def(id).EffectiveBlockedOffsets().Any()) &&
                Def("tree_basic").BlocksMovement && Def("stone_basic").BlocksMovement && Def("sign_basic").BlocksMovement);
            var c = Begin(new HarukaMemoryStorage());
            var world = c.Session.OutdoorWorld;
            var flowers = world.Objects.Where(o => FlowerIds.Contains(o.DefinitionId)).Select(o => o.RootCell).ToList();
            Check("every flower in the garden can be walked on", flowers.Count >= 10 && flowers.All(world.CanEnter), flowers.Count + " flowers");
            var target = flowers.OrderBy(f => Mathf.Abs(f.x - c.Session.Cell.x) + Mathf.Abs(f.y - c.Session.Cell.y)).First();
            Check("she walks onto a flower", Walk(c, target, 60f), target.ToString());
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var fading = catalog.Bindings.Where(b => b.fadeInFront2D || b.fadeInFront3D).Select(b => b.definitionId).ToList();
            Check("only the trees are drawn see-through in front of her, in 2D and 3D (the sunflowers and the easel no longer)",
                fading.SequenceEqual(new[] { "tree_basic" }), string.Join(", ", fading));
        }

        // ---------------------------------------------------------------- the house and the booth

        private static Vector2 Centre(SharedObjectInstance o) =>
            new Vector2(o.RootCell.x + (o.Definition.Footprint.x - 1) * 0.5f, o.RootCell.y + (o.Definition.Footprint.y - 1) * 0.5f);

        // No vertex of the model inside the door's opening (she is 0.53 wide and 0.97 tall).
        private static int VerticesInOpening(string modelPath, string part, float halfWidth, float height, float z0, float z1, float depthSign)
        {
            if (depthSign < 0f) { var flipped = -z1; z1 = -z0; z0 = flipped; }   // Blender's North as Unity's -Z
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var inside = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part != null && filter.name != part) continue;
                var m = filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (Mathf.Abs(p.x) < halfWidth && p.y > 0.05f && p.y < height && p.z > z0 && p.z < z1) inside++;
                }
            }
            return inside;
        }

        private static void HouseChecks()
        {
            var c = Begin(new HarukaMemoryStorage());
            var field = c.Session.OutdoorWorld;
            Check("the house: 7 x 3 cells (was 5 x 2), its door cell where it was (-7, -4)", Def("house_main").Footprint == new Vector2Int(7, 3) &&
                field.TryGetHouseDoorCell() == new Vector2Int(-7, -4));
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.OfficialPlayerModelPath);
            var bounds = new Bounds();
            var first = true;
            foreach (var r in player.GetComponentsInChildren<MeshFilter>(true))
            {
                var b = GeometryUtility.CalculateBounds(r.sharedMesh.vertices, r.transform.localToWorldMatrix);
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }
            var house = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.Phase3Models + "house_main.fbx");
            var doorAnchor = house.GetComponentsInChildren<Transform>(true).First(tr => tr.name == "DoorAnchor");
            var depthSign = Mathf.Sign(doorAnchor.position.z);   // the door plane lies behind the front (Blender +Y)
            var door = house.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "HouseDoor");
            var doorSize = GeometryUtility.CalculateBounds(door.sharedMesh.vertices, UnityEngine.Matrix4x4.identity).size;
            Check("the house door is bigger than ﾊﾙｶﾁｬﾝ (door " + doorSize.x.ToString("0.00") + " x " + doorSize.y.ToString("0.00") + ", she " +
                  bounds.size.x.ToString("0.00") + " x " + bounds.size.y.ToString("0.00") + ")",
                doorSize.y > bounds.size.y + 0.08f && doorSize.x > bounds.size.x + 0.08f);
            Check("nothing of the house in the doorway up to her height (the alcove and the arch clear her)",
                VerticesInOpening(PocSceneBuilder.Phase3Models + "house_main.fbx", "house_main", 0.26f, bounds.size.y, -0.15f, 0.4f, depthSign) == 0 && Mathf.Abs(doorAnchor.position.z) > 0.3f);
            Check("nothing of the sound booth in its doorway up to her height (door 0.53 x 1.05, was 0.29 x 0.84)",
                VerticesInOpening("Assets/POC2D3D/Models/V31/soundbooth_exterior.fbx", null, 0.26f, bounds.size.y, -0.18f, 0.12f, depthSign) == 0);
            var room = c.Session.FindWorld("halka_house");
            var rug = room.Objects.First(o => o.DefinitionId == "living_rug");
            var table = room.Objects.First(o => o.DefinitionId == "low_table_living");
            Check("the rug is 4 x 3 and the low table stands in its middle", rug.Definition.Footprint == new Vector2Int(4, 3) && Centre(rug) == Centre(table),
                Centre(rug) + " " + Centre(table));
            var kitchen = room.Objects.First(o => o.DefinitionId == "kitchen_counter");
            var points = kitchen.Definition.ActionPoints;
            Check("the kitchen: along the South wall (the wall right below each of its cells), facing North, hob - sink - fridge West to East, each used from the North",
                kitchen.Definition.Footprint == new Vector2Int(3, 1) && kitchen.BlockedCells().All(cell => room.Map.SurfaceAt(cell + Vector2Int.down) != null &&
                    room.Map.SurfaceAt(cell + Vector2Int.down).BlocksMovement || cell + Vector2Int.down == new Vector2Int(0, -4)) &&
                points.All(p => p.PlayerCellOffset.y == 1 && p.PlayerFacing == WorldFacing.Down) &&
                points.Select(p => p.Id).SequenceEqual(new[] { "sink_center", "hob", "fridge" }) &&
                points.First(p => p.Id == "hob").PlayerCellOffset.x == 0 && points.First(p => p.Id == "fridge").PlayerCellOffset.x == 2);
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            Check("the kitchen's model is turned to face North (a quarter turn)", Mathf.Approximately(catalog.Find("kitchen_counter").yawDegrees, 90f));
            c.Session.LoadMapForCheck("halka_house", kitchen.RootCell + new Vector2Int(2, 1), FacingDirection.Down);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("A at the fridge (from the North): its words", c.Session.InteractAhead() && !string.IsNullOrEmpty(c.Session.LastMessage), c.Session.LastMessage);
            var booth = c.Session.FindWorld("halka_soundbooth");
            var floor = 0;
            for (var y = booth.Map.MinCell.y; y <= booth.Map.MaxCell.y; y++)
            for (var x = booth.Map.MinCell.x; x <= booth.Map.MaxCell.x; x++)
            {
                var s = booth.Map.SurfaceAt(new Vector2Int(x, y));
                if (s == null || !s.BlocksMovement) floor++;
            }
            Check("the sound booth's room: 9 x 6 cells of floor (was 13 x 8, about half), the door in the South wall",
                floor == 9 * 6 + 1 && booth.TryGetMarker("interior_exit", out var exit) && exit == new Vector2Int(0, -4), floor + " floor cells (with the door)");
            booth.TryGetMarker("interior_entry", out var entry);
            var reach = new HashSet<Vector2Int> { entry };
            var queue = new Queue<Vector2Int>(reach);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Dirs)
                    if (!reach.Contains(cell + d) && booth.CanEnter(cell + d)) { reach.Add(cell + d); queue.Enqueue(cell + d); }
            }
            var stands = booth.Objects.Where(o => o.Definition.ActionPoints.Count > 0).SelectMany(o => o.Definition.ActionPoints.Select(p => o.RootCell + p.PlayerCellOffset)).ToList();
            var mic = booth.Objects.First(o => o.DefinitionId == PocLife.MicId);
            Check("the booth: the DTM desk, the tablet, the mic and the guitar all inside, each reached on foot from the door",
                booth.Objects.Count == 4 && stands.All(reach.Contains) && Dirs.Any(d => reach.Contains(mic.RootCell + d)), string.Join(" ", stands));
        }

        // ---------------------------------------------------------------- sleeping

        private static void SleepChecks()
        {
            var storage = new HarukaMemoryStorage();
            var c = Begin(storage);
            var life = L(c);
            var room = c.Session.FindWorld("halka_house");
            var bed = room.Objects.First(o => o.DefinitionId == PocLife.BedId);
            var stand = bed.RootCell + bed.Definition.ActionPoints[0].PlayerCellOffset;
            c.Session.LoadMapForCheck("halka_house", stand, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var minute0 = life.AbsoluteMinuteNow;
            var asked = c.Session.InteractAhead();
            Check("a new game, just begun: A at the bed says 「ねむくないよ」 and nothing moves", asked && life.SleepRefusals == 1 &&
                c.Session.LastMessage == PocLife.NotSleepyLine && life.State.Sleeps == 0 && life.AbsoluteMinuteNow - minute0 <= 1);
            Advance(c, LifeClock.RealMilliseconds(11L * 60L));
            Check("11 game hours up: still not sleepy", c.Session.InteractAhead() && life.SleepRefusals == 2 && life.State.Sleeps == 0, life.AwakeMinutes + " min");
            Advance(c, LifeClock.RealMilliseconds(61L));
            var ch = life.State.Channel;
            ch.LastPostUnixMs = life.NowMs;   // a post just now: the wait is 24:00
            var waitBefore = ch.RemainingGameMinutes(life.NowMs);
            var anchorBefore = c.Clock.AnchorMilliseconds;
            var minuteBefore = life.AbsoluteMinuteNow;
            var writes = c.Save.Writes;
            var slept = c.Session.InteractAhead();
            Check("12 game hours up: she sleeps; the world moves on 6 game hours (World Time's anchor 5 real minutes back: clock, sky and day together)",
                slept && life.State.Sleeps == 1 && life.AbsoluteMinuteNow - minuteBefore >= 360 && life.AbsoluteMinuteNow - minuteBefore <= 361 &&
                anchorBefore - c.Clock.AnchorMilliseconds == LifeClock.RealMilliseconds(360) &&
                c.Clock.CurrentPhase == WorldTimeCycle.PhaseAt(c.Clock.AnchorMilliseconds, fakeNow) &&
                c.Session.LastMessage.StartsWith(PocLife.SleptLine), c.Session.LastMessage);
            Check("ニコチューブ's wait is 6 hours shorter too (it counts the same game time)", waitBefore - ch.RemainingGameMinutes(life.NowMs) == 360,
                waitBefore + " -> " + ch.RemainingGameMinutes(life.NowMs));
            Check("saved at once: the wake time and the count", c.Save.Writes == writes + 1 && c.Save.LastWritten.Life.Sleeps == 1 &&
                c.Save.LastWritten.Life.WakeMinute == life.State.WakeMinute && PocLife.SleepFadeSeconds > 0f);
            Check("just woken: not sleepy again (12 game hours from now)", c.Session.InteractAhead() && life.State.Sleeps == 1 && !life.Sleepy);
            // after the second look: from every side, facing it (before: only the bottom-left cell, its action point)
            var sides = new List<(Vector2Int cell, FacingDirection facing)>();
            foreach (var blocked in bed.BlockedCells())
                foreach (var d in Dirs)
                {
                    var cell = blocked + d;
                    if (bed.BlockedCells().Contains(cell) || !room.CanEnter(cell) || c.Session.IsTransitionCell(room, cell)) continue;
                    sides.Add((cell, FacingDirectionExtensions.FromVector(-d)));
                }
            var answered = 0;
            foreach (var (cell, facing) in sides)
            {
                c.Session.PlaceForCheck(cell, facing);
                Frame(c, 1f / 60f, Vector2Int.zero);
                c.Session.ShowMessage("");
                if (c.Session.InteractAhead() && c.Session.LastMessage == PocLife.NotSleepyLine) answered++;
            }
            var awayCell = sides.Count > 0 ? sides[0].cell + (sides[0].cell - (sides[0].cell + sides[0].facing.ToVector())) : stand;
            var awayAnswers = false;
            if (room.CanEnter(awayCell))
            {
                c.Session.PlaceForCheck(awayCell, sides[0].facing);
                Frame(c, 1f / 60f, Vector2Int.zero);
                c.Session.ShowMessage("");
                awayAnswers = c.Session.InteractAhead() && c.Session.LastMessage == PocLife.NotSleepyLine;
            }
            Check("the bed answers from every side she can stand on, facing it (not only its bottom-left cell); one cell further away, it does not",
                sides.Count >= 3 && answered == sides.Count && !awayAnswers, $"{answered} of {sides.Count} sides: " + string.Join(" ", sides.Select(x => x.cell + x.facing.ToString())));
            var anchor = c.Clock.AnchorMilliseconds;
            var c2 = Begin(storage, fakeNow + 1000);
            var life2 = L(c2);
            Check("a restart: the moved world clock, the wake time and the count all kept", c2.Clock.AnchorMilliseconds == anchor &&
                life2.State.WakeMinute == life.State.WakeMinute && life2.State.Sleeps == 1);
            // an earlier ver4.0 block (before sleeping existed): no "sleep" in it, its checksum still good
            var old = life2.State.Copy();
            old.WakeMinute = -1;
            old.Sleeps = 0;
            var json = old.ToJson("main");
            HarukaSaveJson.TryParse(json, out var parsed, out _);
            var read = LifeSaveBlock.TryRead(parsed, "main", out var back, out var problem);
            Check("an earlier ver4.0 save (no sleep data) still reads (its checksum unchanged)", read && !json.Contains("\"sleep\"") && !back.HasSleep, problem);
            back.StartedUnixMs = life2.NowMs - LifeClock.RealMilliseconds(8L * 60L);
            life2.Sanitize(back, life2.NowMs);
            Check("…and counts her day from the life's start (that life began 8 game hours ago: up 8 hours)", back.WakeMinute >= 0 &&
                life2.AbsoluteMinuteNow - back.WakeMinute >= 479 && life2.AbsoluteMinuteNow - back.WakeMinute <= 481,
                (life2.AbsoluteMinuteNow - back.WakeMinute).ToString());
        }

        // ---------------------------------------------------------------- the mini games' difficulties

        private static void DifficultyChecks()
        {
            int[] Gains(MinigameDifficulty d) => new[] { 0, 20, 40, 60, 80, 100 }.Select(s => CreatorState.GainFor(s, d)).ToArray();
            Check("the gains by score band: かんたん 0 0 1 1 2 3, ふつう 0 1 2 3 4 5 (as before), やばい 0 1 3 5 8 12",
                Gains(MinigameDifficulty.Easy).SequenceEqual(new[] { 0, 0, 1, 1, 2, 3 }) && Gains(MinigameDifficulty.Normal).SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }) &&
                Gains(MinigameDifficulty.Hard).SequenceEqual(new[] { 0, 1, 3, 5, 8, 12 }) && CreatorState.GainFor(87) == 4);
            var host = new GameObject("ver40 difficulty checks");
            var g = host.AddComponent<PocMinigames>();
            var notes = new Dictionary<MinigameDifficulty, int>();
            var melody = new Dictionary<MinigameDifficulty, int>();
            var travel = new Dictionary<MinigameDifficulty, float>();
            var window = new Dictionary<MinigameDifficulty, float>();
            var tolerance = new Dictionary<MinigameDifficulty, float>();
            var drawTime = new Dictionary<MinigameDifficulty, float>();
            var singScore = new Dictionary<MinigameDifficulty, int>();
            var singTolerance = new Dictionary<MinigameDifficulty, float>();
            var intro = "";
            foreach (MinigameDifficulty d in Enum.GetValues(typeof(MinigameDifficulty)))
            {
                g.Begin(CreatorSkill.Composition, null, null);
                if (d == MinigameDifficulty.Easy) intro = g.DevJson();
                g.StartForCheck(d);
                notes[d] = g.NoteCount;
                travel[d] = g.NoteTravel;
                window[d] = g.GoodWindowNow;
                g.Cancel();
                g.Begin(CreatorSkill.Drawing, null, null);
                g.StartForCheck(d);
                tolerance[d] = g.Tolerance;
                drawTime[d] = g.DrawSecondsNow;
                g.Cancel();
                g.Begin(CreatorSkill.Singing, null, null);
                g.StartForCheck(d);
                melody[d] = g.MelodyCount;
                singScore[d] = g.ScoreSingingFor(0.4f);
                singTolerance[d] = g.SingTolerance;
                g.Cancel();
            }
            var e = MinigameDifficulty.Easy;
            var n = MinigameDifficulty.Normal;
            var h = MinigameDifficulty.Hard;
            Check("the first screen: かんたん / ふつう / やばい and やめる", intro.Contains("\"easy\"") && intro.Contains("\"normal\"") && intro.Contains("\"hard\"") && intro.Contains("\"b\""));
            Check("DTM: かんたん fewer and slower notes with a wider window, やばい more, faster and tighter",
                notes[e] < notes[n] && notes[n] < notes[h] && travel[e] > travel[n] && travel[n] > travel[h] && window[e] > window[n] && window[n] > window[h],
                $"notes {notes[e]}/{notes[n]}/{notes[h]}");
            Check("おえかき: かんたん a wider line, やばい a narrower one; the time grows with the shapes (25 / 30 / 35 s for 1 / 2 / 5)",
                tolerance[e] > tolerance[n] && tolerance[n] > tolerance[h] && Mathf.Approximately(drawTime[e], 25f) &&
                Mathf.Approximately(drawTime[n], 30f) && Mathf.Approximately(drawTime[h], 35f));
            Check("うた: かんたん a slower melody that forgives more, やばい a faster one that forgives less",
                melody[e] < melody[n] && melody[n] < melody[h] && singScore[e] > singScore[n] && singScore[n] > singScore[h],
                $"melody {melody[e]}/{melody[n]}/{melody[h]} score {singScore[e]}/{singScore[n]}/{singScore[h]}");
            Check("ふつう: DTM as the first version (its windows and travel); おえかき's line as before (two shapes now, 30 s)",
                Mathf.Approximately(travel[n], PocMinigames.NoteTravelSeconds) && Mathf.Approximately(window[n], PocMinigames.GoodWindow) &&
                Mathf.Approximately(drawTime[n], PocMinigames.DrawSeconds) && Mathf.Approximately(tolerance[n], 0.1f));
            Check("うた after the second look: かんたん is the first やばい (band 0.85), ふつう 0.7, やばい 0.5 with notes changing about every 0.4-0.6 s",
                Mathf.Approximately(singTolerance[e], 0.85f) && Mathf.Approximately(singTolerance[n], 0.7f) && Mathf.Approximately(singTolerance[h], 0.5f) &&
                melody[h] >= 36, $"melody {melody[h]} notes in 24 s");
            UnityEngine.Object.DestroyImmediate(host);
            // in the game: the difficulty chosen is the one paid
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            var games = life.Minigames;
            var finish = typeof(PocLife).GetMethod("OnGameFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            games.Begin(CreatorSkill.Singing, null, null);
            games.StartForCheck(MinigameDifficulty.Hard);
            var hard = (int)finish.Invoke(life, new object[] { CreatorSkill.Singing, 100 });
            games.Cancel();
            games.Begin(CreatorSkill.Singing, null, null);
            games.StartForCheck(MinigameDifficulty.Easy);
            var easy = (int)finish.Invoke(life, new object[] { CreatorSkill.Singing, 100 });
            games.Cancel();
            Check("in the game: 100 points on やばい +12, on かんたん +3 (かしょうりょく 15)", hard == 12 && easy == 3 && life.State.Creator.Skills[2] == 15,
                hard + " / " + easy);
        }

        // ---------------------------------------------------------------- after the second look (2026-11-01)

        private static void SecondLookChecks()
        {
            // the WebGL quality level's shadow distance (High: 40 m); an earlier suite in the same editor
            // may have left the 3D view's 24 m behind (Run puts the editor's own value back at the end)
            QualitySettings.shadowDistance = 40f;
            var c = Begin(new HarukaMemoryStorage());
            var life = L(c);
            Check("the 3D camera stands farther back outdoors (4.1, was 3.4; the house is 1.55x), indoors as before (3.6)",
                Mathf.Approximately(c.Third.OutdoorDistance, 4.1f) && Mathf.Approximately(c.Third.IndoorDistance, 3.6f) &&
                Mathf.Approximately(PocThirdPersonCamera.StandardOutdoorDistance, 4.1f), c.Third.OutdoorDistance + " / " + c.Third.IndoorDistance);
            var viewBefore = c.Switcher.Mode;
            c.Menu.ActivateQuickItem(C6.Clock);
            var switched = c.Switcher.Mode != viewBefore;
            c.Menu.ActivateQuickItem(C6.Clock);
            Check("the menu has no 「ひょうじ」 row any more; the 2D / 3D button at the top right still switches the view",
                c.Menu.ExtraItems.Count == 0 && switched && c.Switcher.Mode == viewBefore, string.Join(", ", c.Menu.ExtraItems.Select(i => i.Label)));

            // shadows: soft and nearer in 3D (the jagged edges on the bigger house), hard and as far as before in 2D
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var far2D = QualitySettings.shadowDistance;
            var hard2D = c.Switcher.Sun != null ? c.Switcher.Sun.shadows : LightShadows.None;
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var near3D = QualitySettings.shadowDistance;
            var soft3D = c.Switcher.Sun != null ? c.Switcher.Sun.shadows : LightShadows.None;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var back2D = QualitySettings.shadowDistance;
            Check("3D shadows soft and drawn to 24 m (finer near her: no jagged edges on the house); 2D hard and as far as before",
                soft3D == LightShadows.Soft && near3D <= PocViewSwitcher.Shadow3DDistance && hard2D == LightShadows.Hard &&
                c.Switcher.Sun.shadows == LightShadows.Hard && Mathf.Approximately(back2D, far2D) && far2D > near3D,
                $"2D {hard2D} {far2D} m, 3D {soft3D} {near3D} m");
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);

            // footsteps: two soft sounds in turn, a little quieter, a slightly different pitch each
            var steps = c.Gameplay.Footsteps;
            var source = steps.GetComponent<AudioSource>();
            var clips = new List<AudioClip>();
            var pitches = new List<float>();
            for (var i = 0; i < 6; i++) { steps.PlayStep(); clips.Add(steps.LastClip); pitches.Add(source.pitch); }
            Check("footsteps: the two soft sounds in turn (footstep_soft_0 / _1), volume 0.35, pitch within 1 ± 0.06",
                clips.All(x => x != null && x.name.StartsWith("footstep_soft_")) && clips[0] != clips[1] && clips[0] == clips[2] &&
                Mathf.Approximately(source.volume, 0.35f) && pitches.All(x => x >= 0.94f && x <= 1.06f) && pitches.Distinct().Count() > 1,
                string.Join(" ", clips.Select(x => x != null ? x.name : "null")));
            float Loudest(AudioClip clip)
            {
                var data = new float[clip.samples * clip.channels];
                clip.GetData(data, 0);
                return data.Max(Mathf.Abs);
            }
            var old = AssetDatabase.LoadAssetAtPath<AudioClip>(PocSceneBuilder.FootstepPath);
            Check("…softer than the first footstep (the cute short 「ぷっ」 the user picked: peak 0.4 against 0.68; the old file kept for the 2D game)",
                old != null && clips[0] != null && Loudest(clips[0]) < 0.5f && Loudest(old) > 0.6f, $"{Loudest(clips[0]):0.00} / {(old != null ? Loudest(old) : 0f):0.00}");

            // 2D: a small dark oval at the feet of everyone drawn
            Walk(c, new Vector2Int(-2, 0), 60f);
            Settle(c, 1f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            c.Pixel.ComposeScreen(960, 540);
            var player = c.Pixel.LastSpriteRect;
            var shadows = c.Pixel.ShadowRects.ToList();
            var walkers = c.Pixel.WalkerRects.Values.ToList();
            var expected = 1 + walkers.Count + (c.Pixel.LastCrowSprite != null ? 1 : 0);
            bool Under(RectInt sprite, RectInt shadow) => Mathf.Abs(sprite.center.x - shadow.center.x) <= 2f &&
                shadow.y <= sprite.y + sprite.height * 0.12f && shadow.yMax >= sprite.y && shadow.width < sprite.width;
            Check("2D: a shadow at the feet of her, of Rin and Len and of the crow (an oval under each, as wide as the feet)",
                shadows.Count == expected && walkers.Count >= 2 && shadows.Count > 0 && Under(player, shadows[0]) &&
                walkers.All(w => shadows.Any(sh => Under(w, sh))),
                $"shadows {shadows.Count} expected {expected}; her {player} shadow {(shadows.Count > 0 ? shadows[0].ToString() : "-")}");
            // each frame's own feet: Len's back view stands 5 px above its sprite's bottom, ﾊﾙｶﾁｬﾝ 2 px
            var lenBack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Character/V40/len/back_idle_0.png");
            var harukaFront = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Character/front_idle/00.png");
            var padLen = c.Pixel.BottomPadding(lenBack);
            var padHaruka = c.Pixel.BottomPadding(harukaFront);
            Check("2D shadows sit a pixel under each frame's own lowest row (Len from behind no longer floats: 5 px above his sprite's bottom, ﾊﾙｶﾁｬﾝ 2)",
                padLen == 5 && padHaruka == 2, $"Len back {padLen}, ﾊﾙｶﾁｬﾝ front {padHaruka}");
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);

            // おえかき: one simple shape, two simple shapes, five different things
            var host = new GameObject("ver40 drawing checks");
            var g = host.AddComponent<PocMinigames>();
            var counts = new Dictionary<MinigameDifficulty, List<int>>();
            var hardSeen = new HashSet<int>();
            var simpleOnly = true;
            for (var round = 0; round < 12; round++)
                foreach (MinigameDifficulty d in Enum.GetValues(typeof(MinigameDifficulty)))
                {
                    g.Begin(CreatorSkill.Drawing, null, null);
                    g.StartForCheck(d);
                    if (!counts.ContainsKey(d)) counts[d] = new List<int>();
                    counts[d].Add(g.ShapesNow.Count);
                    if (d != MinigameDifficulty.Hard) simpleOnly &= g.ShapesNow.All(x => PocMinigames.SimpleShapes.Contains(x));
                    else
                    {
                        simpleOnly &= g.ShapesNow.Distinct().Count() == g.ShapesNow.Count;
                        foreach (var x in g.ShapesNow) hardSeen.Add(x);
                    }
                    g.Cancel();
                }
            Check("おえかき: かんたん always 1 simple shape, ふつう 2, やばい 5 different things (from all 10, not only the simple ones)",
                counts[MinigameDifficulty.Easy].All(x => x == 1) && counts[MinigameDifficulty.Normal].All(x => x == 2) &&
                counts[MinigameDifficulty.Hard].All(x => x == 5) && simpleOnly && hardSeen.Count >= 8 && hardSeen.Any(x => !PocMinigames.SimpleShapes.Contains(x)),
                "seen on やばい: " + string.Join(",", hardSeen.OrderBy(x => x)));
            var five = new[] { 0, 1, 8, 9, 5 };
            var lines = new List<List<Vector2>>();
            var layout = PocMinigames.Layout(five);
            for (var i = 0; i < five.Length; i++) lines.Add(layout.Skip(i * 161).Take(161).ToList());
            g.Begin(CreatorSkill.Drawing, null, null);
            g.StartForCheck(MinigameDifficulty.Hard);
            var all = g.ScoreDrawingFor(five, lines, false);
            var oneOfFive = g.ScoreDrawingFor(five, lines.Take(1).ToList(), false);
            g.Cancel();
            Check("…all five traced on やばい: a high score; only one of the five: half or less", all >= 95 && oneOfFive <= 50, $"{all} / {oneOfFive}");
            UnityEngine.Object.DestroyImmediate(host);

            // the worn world in the models themselves (the full-screen pass was taken out again); the game
            // sets the strength in PocTimeOfDay's LateUpdate (the harness's frame does not run it)
            Frame(c, 1f / 60f, Vector2Int.zero);
            PocWorn.Apply(0f);
            c.Time.Apply(UnityEngine.Time.unscaledTime);
            var sceneryRenderers = c.Presenter.Views.Where(v => v != null).SelectMany(v => v.GetComponentsInChildren<Renderer>(true)).ToList();
            var characterRenderers = c.Actor.GetComponentsInChildren<Renderer>(true)
                .Concat(c.Switcher.CrowView != null ? c.Switcher.CrowView.GetComponentsInChildren<Renderer>(true) : new Renderer[0]).ToList();
            Check("the worn world: scenery models worn (one global strength, 1), characters keep their colours (ﾊﾙｶﾁｬﾝ, the crow); no full-screen pass",
                Mathf.Approximately(PocWorn.Current, PocWorn.Strength) && Mathf.Approximately(Shader.GetGlobalFloat("_PocWorn"), 1f) &&
                sceneryRenderers.Count > 20 && sceneryRenderers.Count(r => !PocWorn.IsExempt(r)) > sceneryRenderers.Count / 2 &&
                characterRenderers.Count > 0 && characterRenderers.All(PocWorn.IsExempt) &&
                UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).All(cam => cam.GetComponents<MonoBehaviour>().All(m => m == null || m.GetType().Name != "PocMoodGrade")),
                $"scenery {sceneryRenderers.Count(r => !PocWorn.IsExempt(r))}/{sceneryRenderers.Count} worn, characters {characterRenderers.Count(PocWorn.IsExempt)}/{characterRenderers.Count} kept");

            // the house: windows on the front now (the Blender script's manifest for house_main.fbx: the
            // note and the triangles the two windows and sills added: 1418 before)
            var manifest = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "blender/exports/phase3_assets_manifest.json"));
            var entry = manifest.Substring(manifest.IndexOf("\"name\": \"house_main\"", StringComparison.Ordinal));
            entry = entry.Substring(0, Math.Min(entry.Length, entry.IndexOf("\"supplemented\"", StringComparison.Ordinal) + 600));
            var triangles = int.Parse(System.Text.RegularExpressions.Regex.Match(entry, "\"triangles\": (\\d+)").Groups[1].Value);
            Check("the house has windows on its front, one on each side of the door (house_main.fbx, built by build_phase3_buildings.py)",
                entry.Contains("ver4.0: windows on the front") && triangles >= 1418 + 150, triangles + " triangles");

            // the room: the kitchen on the South wall drawn low in 2D (she no longer sinks into it), full
            // height in 3D; no shadow on the South wall's face when her feet are behind it
            c.Session.LoadMapForCheck("halka_house", new Vector2Int(4, -2), FacingDirection.Down);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var kitchen = c.Presenter.Views.FirstOrDefault(v => v != null && v.DefinitionId == "kitchen_counter");
            var low2D = kitchen != null ? kitchen.transform.localScale.y : -1f;
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var full3D = kitchen != null ? kitchen.transform.localScale.y : -1f;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Session.PlaceForCheck(new Vector2Int(-3, -3), FacingDirection.Down);
            Frame(c, 1f / 60f, Vector2Int.zero);
            c.Pixel.ComposeScreen(960, 540);
            var shadowsBottomRow = c.Pixel.ShadowRects.Count;
            c.Session.PlaceForCheck(new Vector2Int(-3, -1), FacingDirection.Down);
            Frame(c, 1f / 60f, Vector2Int.zero);
            c.Pixel.ComposeScreen(960, 540);
            var shadowsInside = c.Pixel.ShadowRects.Count;
            Check("the room in 2D: the kitchen drawn low (x0.3, as the South wall's base), at full height in 3D; on the bottom row (feet behind the South wall) no shadow on the wall, further in her shadow as usual",
                kitchen != null && Mathf.Approximately(low2D, PocPixelView2D.Low2DScale) && Mathf.Approximately(full3D, 1f) &&
                shadowsBottomRow == 0 && shadowsInside >= 1, $"kitchen 2D {low2D} 3D {full3D}; shadows on the bottom row {shadowsBottomRow}, inside {shadowsInside}");
            Check("the 2D sprites' dots are drawn evenly only at in-between sizes (x1.5 at an odd scale); at whole sizes plain nearest",
                !c.Pixel.EvenDots(lenBack) == Mathf.Approximately(c.Pixel.SpriteDotSize(lenBack), Mathf.Round(c.Pixel.SpriteDotSize(lenBack))),
                $"dot size {c.Pixel.SpriteDotSize(lenBack)} at scale {c.Pixel.Scale}");
        }

        // ---------------------------------------------------------------- protect

        private static void ProtectChecks()
        {
            Check("WORLD EDITOR v1.0 unchanged (tools/HalkaWorldMapEditor)", V32.Git("diff --quiet " + Published + " -- tools/HalkaWorldMapEditor", out _));
            Check("the published ver3.9 (halkaworld/) unchanged", V32.Git("diff --quiet " + Published + " -- halkaworld", out _));
            Check("みねはうす (minehouse/) unchanged", V32.Git("diff --quiet " + Published + " -- minehouse", out _));
            // After the user's first look the user asked for: the house bigger, its room's rug, table and kitchen,
            // the booth half the size, the flowers passable, bigger doors. Only those files changed.
            var allowedMaps = new HashSet<string>(new[] { "first_field", "halka_house", "halka_soundbooth" }
                .SelectMany(m => new[] { "unity-project/Assets/Content/Maps/Authoring/" + m + ".hwmap.json", "unity-project/Assets/Content/Maps/" + m + ".asset" })
                .Concat(new[] { "unity-project/Assets/Content/Maps/Authoring/object_catalog.hwcatalog.json" })
                .Concat(new[] { "flower_basic", "flower_yellow", "flower_blue", "sunflower_cluster", "house_main", "kitchen_counter", "living_rug" }
                    .Select(d => "unity-project/Assets/Content/Maps/" + d + ".asset")));
            V32.Git("diff --name-only " + Published + " -- unity-project/Assets/Content/Maps unity-project/Assets/Content/FastTravel", out var changedMaps);
            var unexpectedMaps = changedMaps.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !allowedMaps.Contains(s)).ToList();
            Check("the Maps: only the user's changes (the house's place in the field, its room, the booth, the catalog's house, kitchen, rug and flowers); the other Maps, the registry and the そらをとぶ places unchanged",
                unexpectedMaps.Count == 0, string.Join(", ", unexpectedMaps));
            var allowedModels = new HashSet<string>(new[] { "Phase3/house_main.fbx", "Phase3/interior_doorway_base.fbx", "Phase3/interior_doorway_upper.fbx",
                    "V31/soundbooth_exterior.fbx", "V31/booth_doorway_base.fbx", "V31/booth_doorway_upper.fbx", "V32/living_rug.fbx" }
                .Select(m => "unity-project/Assets/POC2D3D/Models/" + m).Concat(new[] { "unity-project/Assets/POC2D3D/Data/POC_PresentationCatalog.asset" }));
            V32.Git("diff --name-only --diff-filter=MD " + Published + " -- unity-project/Assets/POC2D3D/Models unity-project/Assets/POC2D3D/Data", out var changedModels);
            var unexpectedModels = changedModels.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !allowedModels.Contains(s)).ToList();
            Check("the models: only the house, the doorways, the booth's front and the rug (the user's changes) and the presentation catalog; everything else as it was",
                unexpectedModels.Count == 0, string.Join(", ", unexpectedModels));
            // (after the second look the soft footsteps were added to Content/Audio, so the crow's voice is
            // compared on its own and the folder may only gain those two files)
            V32.Git("diff --name-only --diff-filter=MDR " + Published + " -- unity-project/Assets/Content/Audio", out var changedAudio);
            V32.Git("diff --name-only --diff-filter=A " + Published + " -- unity-project/Assets/Content/Audio", out var addedAudio);
            var addedOk = addedAudio.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0)
                .All(x => System.Text.RegularExpressions.Regex.IsMatch(x, @"Content/Audio/footstep_soft_[01]\.wav(\.meta)?$"));
            Check("the crow's sprites, model and voice unchanged (the crow's files; Content/Audio only gained the two soft footsteps)",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Models/Phase4/crow_basic.fbx unity-project/Assets/Content/Audio/crow_voice.mp3 unity-project/Assets/Content/Audio/crow_voice.mp3.meta", out _) &&
                V32.Git("diff --quiet " + Published + " -- " + string.Join(" ", Directory.GetFiles(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/Content/World"), "crow*")
                    .Select(f => "unity-project/Assets/Content/World/" + Path.GetFileName(f))), out _) &&
                changedAudio.Trim().Length == 0 && addedOk, (changedAudio + " " + addedAudio).Trim());
            Check("the font atlas: every earlier glyph in its place (only new glyphs appended)",
                File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/Resources/asobi_ui_map.txt")).StartsWith("ぁあぃいぅうぇえぉお"));
            Check("the old tests are all still there (none deleted)", V32.Git("diff --quiet --diff-filter=D " + Published + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _));
            Check("Save Schema still 1; the crow's own code unchanged except the one line that keeps it out of Rin's, Len's and the passers-by's cells",
                HarukaSaveData.SchemaVersion == 1 && V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/PocCrow.Companion.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Travel.cs unity-project/Assets/Scripts/World/CrowBehaviourCore.cs", out _));
            var originals = new[] { @"D:\バックアップ\3D\鏡音1頭身\rin ver2.0.blend", @"D:\バックアップ\3D\鏡音1頭身\len ver2.0.blend" };
            var hashes = File.Exists(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V40_BACKUP_20261031", "hashes_before.txt"))
                ? File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V40_BACKUP_20261031", "hashes_before.txt")) : "";
            var same = originals.All(p =>
            {
                if (!File.Exists(p)) return false;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var f = File.OpenRead(p))
                    return hashes.Contains(BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant());
            });
            Check("the user's original Rin / Len .blend files unchanged (the same SHA-256 as before the work)", same);
            Check("no development command in a release build (the life commands compile only with DEVELOPMENT_BUILD or the editor)",
                File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/Life/PocLife.cs")).Contains("#if DEVELOPMENT_BUILD || UNITY_EDITOR"));
        }
    }

    internal static class Ver40WorldExtensions
    {
        public static Vector2Int TryGetHouseDoorCell(this SharedWorldModel world) => world.TryGetHouseDoor(out var door) ? door : Vector2Int.zero;
    }
}
