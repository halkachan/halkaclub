using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Player;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using Halka.Poc2D3D.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.2「オープンワールド基盤アップデート」: the checks of the outdoor World
    // coordinates and the chunk streaming (spec 19). The game is started with the Phase 6 harness
    // (memory storage only); the test worlds are generated in memory (PocTestWorldGenerator) and are
    // never written into first_field or the release scene. Every check prints PASS or FAIL.
    //   Prepare: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer32Checks.PrepareBatch
    //   Checks:  -executeMethod Halka.Poc2D3D.EditorTools.WorldVer32Checks.RunBatch
    public static class WorldVer32Checks
    {
        public const string FixtureDir = "Assets/POC2D3D/Fixtures/ChunkTest";
        public const string FixturePath = FixtureDir + "/PocTestWorldFixture.asset";
        public const int Seed = 3232;
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static readonly Dictionary<(int, int), (OutdoorWorldDefinition world, MapDefinition primary)> worlds =
            new Dictionary<(int, int), (OutdoorWorldDefinition, MapDefinition)>();
        private static int passes;
        private static string category = "";

        public static void PrepareBatch()
        {
            try
            {
                Halka.Game.Editor.MapAuthoringImporter.SyncAll();
                LoadOrCreateFixture(true);
                PocSceneBuilder.Build();
                PocSceneBuilder.Build(PocSceneBuilder.RcScenePath, true);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
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
            lines.Clear();
            worlds.Clear();
            var keys = new[] { LifeLogController.SaveKey, WorldTimeController.SaveKey };
            var saved = keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(3232);
            try
            {
                Run("coordinates", CoordinateChecks);
                Run("registry", RegistryChecks);
                Run("test-worlds", TestWorldChecks);
                Run("lifecycle", LifecycleChecks);
                Run("official-field", OfficialFieldChecks);
                Run("movement", MovementChecks);
                Run("large-objects", LargeObjectChecks);
                Run("crow", CrowChecks);
                Run("auto", AutoChecks);
                Run("view-2d-3d", ViewChecks);
                Run("failures", FailureChecks);
                Run("interiors", InteriorChecks);
                Run("save", SaveChecks);
                Run("time-lifelog", TimeChecks);
                Run("protection", ProtectionChecks);
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
                foreach (var pair in worlds.Values)
                    foreach (var link in pair.world.regions)
                        if (link.map != null) UnityEngine.Object.DestroyImmediate(link.map);
                worlds.Clear();
            }
            foreach (var failure in failures) Debug.LogError("VER32 FAIL " + failure);
            Debug.Log("HARUKA ver3.2 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.2 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V32_LOGS", "ver32_checks.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllLines(report, lines.Concat(new[] { $"TOTAL {passes} passed, {failures.Count} failed" }));
            return failures.Count == 0;
        }

        private static void Run(string name, Action checks)
        {
            category = name;
            try { checks(); }
            catch (Exception exception) { Check("no exception in " + name, false, exception.GetType().Name + ": " + exception.Message); Debug.LogException(exception); }
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            detail = detail ?? "";
            perCategory[category] = perCategory.TryGetValue(category, out var n) ? n + 1 : 1;
            var line = (condition ? "PASS" : "FAIL") + " [" + category + "] " + name + (detail.Length > 0 ? " | " + detail : "");
            lines.Add(line);
            if (condition) { passes++; Debug.Log("VER32 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- fixtures and the harness

        private static T Content<T>(string id) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>("Assets/Content/Maps/" + id + ".asset");

        // The official definitions a test world is made of (the asset is test-only: only the chunk
        // test scene references it).
        public static PocTestWorldFixture LoadOrCreateFixture(bool saveAsset)
        {
            var fixture = AssetDatabase.LoadAssetAtPath<PocTestWorldFixture>(FixturePath);
            if (fixture != null && fixture.IsComplete) return fixture;
            var created = fixture == null;
            if (created) fixture = ScriptableObject.CreateInstance<PocTestWorldFixture>();
            fixture.grass = Content<SurfaceDefinition>("base_ground");
            fixture.dirt = Content<SurfaceDefinition>("dirt");
            fixture.tree = Content<WorldObjectDefinition>("tree_basic");
            fixture.stone = Content<WorldObjectDefinition>("stone_basic");
            fixture.flower = Content<WorldObjectDefinition>("flower_basic");
            fixture.bench = Content<WorldObjectDefinition>("bench_basic");
            fixture.well = Content<WorldObjectDefinition>("well_basic");
            fixture.house = Content<WorldObjectDefinition>("house_main");
            fixture.player = Content<EntityDefinition>("player_main");
            fixture.crow = Content<EntityDefinition>("crow_main");
            if (!fixture.IsComplete) throw new InvalidOperationException("official definitions for the test world are missing");
            if (saveAsset)
            {
                Directory.CreateDirectory(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project", FixtureDir));
                AssetDatabase.Refresh();
                if (created) AssetDatabase.CreateAsset(fixture, FixturePath);
                else EditorUtility.SetDirty(fixture);
                AssetDatabase.SaveAssets();
            }
            return fixture;
        }

        private static (OutdoorWorldDefinition world, MapDefinition primary) TestWorld(int size, int chunkSize = 16)
        {
            if (worlds.TryGetValue((size, chunkSize), out var cached)) return cached;
            var world = PocTestWorldGenerator.Build(LoadOrCreateFixture(false), size, chunkSize, Seed, out var primary);
            worlds[(size, chunkSize)] = (world, primary);
            return (world, primary);
        }

        private static C6.Ctx StartWorld(int size, int chunkSize = 16, HarukaMemoryStorage storage = null)
        {
            var (world, primary) = TestWorld(size, chunkSize);
            return C6.Start(storage ?? new HarukaMemoryStorage(), null, s => s.UseOutdoorWorldForTest(world, primary, false));
        }

        // One frame of play as the game runs it, plus what Update does in play mode (the crow, the
        // chunk streamer), which the Editor harness has to call itself.
        private static void Frame(C6.Ctx c, float dt, Vector2Int raw, bool auto = false)
        {
            C6.Frame(c, dt, raw, auto);
            if (c.Crow != null && c.Crow.isActiveAndEnabled && c.Crow.gameObject.activeInHierarchy) c.Crow.Tick(C6.Clock);
            c.Presenter.UpdateStreaming(false);
        }

        private static void Settle(C6.Ctx c, float seconds, bool auto = false)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, auto);
        }

        private static Vector2Int Raw(C6.Ctx c, Vector2Int world)
        {
            if (c.Switcher.Mode != PocViewMode.ThirdPerson3D) return world;
            foreach (var raw in Dirs)
                if (GridSpace3D.CameraRelative(raw, GridSpace3D.FacingYaw(c.Switcher.CameraHeading)) == world) return raw;
            return world;
        }

        private static void Step(C6.Ctx c, Vector2Int world, float settle = 0.25f)
        {
            C6.Release(c);
            var raw = Raw(c, world);
            for (var i = 0; i < 4; i++) Frame(c, 1f / 60f, raw);
            C6.Release(c);
            Settle(c, settle);
        }

        private static bool Walkable(C6.Ctx c, Vector2Int cell) =>
            c.Session.World.CanEnter(cell) && !c.Session.IsTransitionCell(c.Session.World, cell) && c.Session.CanPlayerEnter(cell);

        private static List<Vector2Int> Route(C6.Ctx c, Vector2Int from, Vector2Int goal)
        {
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
                    if (previous.ContainsKey(next) || !Walkable(c, next)) continue;
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

        // Walks there step by step (each step a real input tap); onStep runs after every step.
        private static bool WalkTo(C6.Ctx c, Vector2Int goal, Action<Vector2Int> onStep = null, float settle = 0.22f)
        {
            for (var attempt = 0; attempt < 4 && c.Session.Cell != goal; attempt++)
            {
                var path = Route(c, c.Session.Cell, goal);
                if (path == null) return false;
                foreach (var next in path)
                {
                    var from = c.Session.Cell;
                    Step(c, next - from, settle);
                    onStep?.Invoke(from);
                    if (c.Session.Cell != next) break;
                }
            }
            return c.Session.Cell == goal;
        }

        private static Vector2Int NearestWalkable(C6.Ctx c, Vector2Int target)
        {
            for (var r = 0; r < 12; r++)
                for (var y = -r; y <= r; y++)
                for (var x = -r; x <= r; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != r) continue;
                    var cell = target + new Vector2Int(x, y);
                    if (Walkable(c, cell)) return cell;
                }
            return target;
        }

        private static PlayerFootstepAudio Footsteps() =>
            UnityEngine.Object.FindAnyObjectByType<PlayerFootstepAudio>(FindObjectsInactive.Include);

        private static int CountGameObjects(C6.Ctx c) => c.Presenter.GetComponentsInChildren<Transform>(true).Length;

        private static int ChunkRoots(C6.Ctx c)
        {
            var count = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Chunk (", StringComparison.Ordinal)) count++;
            return count;
        }

        private static List<string> DuplicateViews(C6.Ctx c) =>
            c.Presenter.Views.Where(v => v != null && !v.IsWall).GroupBy(v => v.InstanceId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        private static string ChunkSummary(C6.Ctx c)
        {
            var s = c.Presenter.Streamer?.Stats;
            return s == null ? "no streamer" : $"ready {s.Loaded} queued {s.Queued} loads {s.TotalLoads} unloads {s.TotalUnloads} sync {s.SyncLoads}";
        }

        private static bool GitClean(string relative)
        {
            var info = new ProcessStartInfo("git", "status --porcelain -- \"" + relative + "\"")
            {
                WorkingDirectory = PocSceneBuilder.RepositoryRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 && output.Trim().Length == 0;
            }
        }

        // ---------------------------------------------------------------- 19.1 coordinates

        private static void CoordinateChecks()
        {
            foreach (var (value, chunk) in new[] { (-1, -1), (-16, -1), (-17, -2), (0, 0), (15, 0), (16, 1), (-32, -2), (-33, -3), (31, 1), (32, 2) })
                Check($"world {value} -> chunk {chunk} (16 cells, floor division)",
                    WorldGrid.FloorDiv(value, 16) == chunk && WorldGrid.ChunkOf(new Vector2Int(value, value), 16) == new ChunkCoord(chunk, chunk),
                    WorldGrid.FloorDiv(value, 16).ToString());
            Check("first_field corners: (-10,-6) and (-1,-1) in chunk (-1,-1); (0,0) and (10,6) in chunk (0,0)",
                WorldGrid.ChunkOf(new Vector2Int(-10, -6), 16) == new ChunkCoord(-1, -1) && WorldGrid.ChunkOf(new Vector2Int(-1, -1), 16) == new ChunkCoord(-1, -1) &&
                WorldGrid.ChunkOf(Vector2Int.zero, 16) == new ChunkCoord(0, 0) && WorldGrid.ChunkOf(new Vector2Int(10, 6), 16) == new ChunkCoord(0, 0));
            foreach (var size in new[] { 8, 16, 32 })
            {
                var bad = 0;
                for (var v = -300; v <= 300; v++)
                {
                    var cell = new Vector2Int(v, -v);
                    var chunk = WorldGrid.ChunkOf(cell, size);
                    var local = WorldGrid.LocalInChunk(cell, size);
                    if (local.x < 0 || local.x >= size || local.y < 0 || local.y >= size) bad++;
                    if (WorldGrid.ChunkToWorld(chunk, local, size) != cell) bad++;
                    if (!WorldGrid.ChunkRect(chunk, size).Contains(cell)) bad++;
                }
                Check($"chunk {size}: for -300..300 the local cell is always 0..{size - 1} and chunk + local -> world gives the cell back", bad == 0, "bad " + bad);
            }
            Check("just before / after a border: 15 and 16 are in chunks 0 and 1, locals 15 and 0; -16 and -17 in -1 and -2, locals 0 and 15",
                WorldGrid.LocalInChunk(new Vector2Int(15, 0), 16).x == 15 && WorldGrid.LocalInChunk(new Vector2Int(16, 0), 16).x == 0 &&
                WorldGrid.LocalInChunk(new Vector2Int(-16, 0), 16).x == 0 && WorldGrid.LocalInChunk(new Vector2Int(-17, 0), 16).x == 15);
            var big = new Vector2Int(WorldGrid.MaxAbsCell - 1, -(WorldGrid.MaxAbsCell - 1));
            var bigChunk = WorldGrid.ChunkOf(big, 16);
            Check("large values (±(2^24-1)): valid, chunk round trip exact", WorldGrid.IsValidCell(big) &&
                WorldGrid.ChunkToWorld(bigChunk, WorldGrid.LocalInChunk(big, 16), 16) == big, bigChunk.ToString());
            Check("invalid cells: beyond ±2^24 and int.MinValue are rejected",
                !WorldGrid.IsValidCell(new Vector2Int(WorldGrid.MaxAbsCell + 1, 0)) && !WorldGrid.IsValidCell(new Vector2Int(0, int.MinValue)));
            Check("overflow is an error, never a wrapped cell (CheckedAdd int.MaxValue + 1)", Throws<OverflowException>(() =>
                WorldGrid.CheckedAdd(new Vector2Int(int.MaxValue, 0), Vector2Int.right)));
            Check("a chunk-local cell outside 0..size-1 is refused", Throws<ArgumentOutOfRangeException>(() =>
                WorldGrid.ChunkToWorld(new ChunkCoord(0, 0), new Vector2Int(16, 0), 16)));
            Check("a chunk size outside 4..256 is refused", Throws<ArgumentOutOfRangeException>(() => WorldGrid.RequireChunkSize(3)) &&
                Throws<ArgumentOutOfRangeException>(() => WorldGrid.RequireChunkSize(300)));
            var precise = true;
            foreach (var x in new[] { 0, 1, -1, 1000, -1000, 32767, -32768 })
            {
                var cell = new Vector2Int(x, -x);
                precise &= GridSpace3D.ToCell(GridSpace3D.ToWorld(cell)) == cell;
                var quarter = cell.x * GridSpace3D.CellSize + 0.125f;
                precise &= Math.Abs(quarter - (cell.x * 0.5 + 0.125)) < 1e-9;
            }
            Check("float positions: within ±32768 cells a cell and its 1/4-cell steps are exact (no drift)", precise);
            var far = (float)(8388608.0 + 0.125);
            Check("beyond the render-safe range float loses the step fraction (2^23 units + 0.125): why Validate warns there",
                Math.Abs(far - 8388608.125) > 1e-4, far.ToString("R"));

            // Map-local <-> world (first_field offset 0, a test region with an offset).
            var c = C6.Start(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var field = index.FindRegion("first_field");
            var same = true;
            for (var y = field.Map.MinCell.y; y <= field.Map.MaxCell.y; y++)
            for (var x = field.Map.MinCell.x; x <= field.Map.MaxCell.x; x++)
                same &= field.ToWorld(new Vector2Int(x, y)) == new Vector2Int(x, y);
            Check("first_field: world ID outdoor_main, offset (0,0), every world cell equals its first_field cell",
                index.WorldId == "outdoor_main" && field.Offset == Vector2Int.zero && same && field.Rect.Equals(new CellRect(new Vector2Int(-10, -6), new Vector2Int(10, 6))));
            Check("TryToSessionCell(first_field, (-1,0)) -> outdoor world (-1,0); interiors keep their Map cells",
                c.Session.TryToSessionCell("first_field", new Vector2Int(-1, 0), out var w1, out var s1) && w1 == c.Session.OutdoorWorld && s1 == new Vector2Int(-1, 0) &&
                c.Session.TryToSessionCell("halka_house", new Vector2Int(2, 1), out var w2, out var s2) && w2 == c.Session.InteriorWorld && s2 == new Vector2Int(2, 1) &&
                c.Session.TryToSessionCell("halka_soundbooth", new Vector2Int(3, -1), out var w3, out var s3) && w3.MapId == "halka_soundbooth" && s3 == new Vector2Int(3, -1));
            Check("interiors are not part of the outdoor world (no region, own model)",
                index.FindRegion("halka_house") == null && index.FindRegion("halka_soundbooth") == null && c.Session.InteriorWorld.OutdoorIndex == null);

            var (world, _) = TestWorld(128);
            var testIndex = new OutdoorWorldIndex(world);
            var nw = testIndex.FindRegion("chunktest_128_nw");
            var roundTrip = true;
            for (var y = nw.Rect.Min.y; y <= nw.Rect.Max.y; y++)
            for (var x = nw.Rect.Min.x; x <= nw.Rect.Max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                roundTrip &= nw.ToWorld(nw.ToLocal(cell)) == cell && testIndex.RegionAt(cell) == nw;
            }
            Check("a region with offset (-64,0): local (0,0) is world (-64,0); world -> local -> world exact for all 4,096 cells",
                nw.Offset == new Vector2Int(-64, 0) && nw.ToWorld(Vector2Int.zero) == new Vector2Int(-64, 0) && nw.Map.MinCell == Vector2Int.zero && roundTrip);
        }

        private static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
            catch (Exception) { return false; }
        }

        // ---------------------------------------------------------------- 11.1 registry

        private static void RegistryChecks()
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(PocSceneBuilder.OutdoorWorldRegistryPath);
            var problems = new List<string>();
            var official = file != null ? OutdoorWorldRegistryJson.Parse(file.text, id => Content<MapDefinition>(id), problems) : null;
            Check("outdoor_main.hwworld.json: format halka-world-outdoor v1, world outdoor_main, chunk 16, one region first_field at (0,0), no problem",
                official != null && problems.Count == 0 && official.worldId == "outdoor_main" && official.chunkSize == 16 && official.regions.Count == 1 &&
                official.regions[0].mapId == "first_field" && official.regions[0].offset == Vector2Int.zero, string.Join("; ", problems));
            Func<string, MapDefinition> resolve = id => Content<MapDefinition>(id);
            string Json(string format, int version, string regions, int chunk = 16) =>
                "{\"format\":\"" + format + "\",\"formatVersion\":" + version + ",\"worldId\":\"outdoor_main\",\"chunkSize\":" + chunk + ",\"regions\":[" + regions + "]}";
            const string field = "{\"mapId\":\"first_field\",\"offset\":{\"x\":0,\"y\":0}}";
            var cases = new (string label, string json, string expect)[]
            {
                ("another format", Json("something-else", 1, field), "format"),
                ("a newer format version", Json("halka-world-outdoor", 2, field), "version"),
                ("an unknown Map", Json("halka-world-outdoor", 1, "{\"mapId\":\"no_such_map\",\"offset\":{\"x\":0,\"y\":0}}"), "unknown Map"),
                ("an interior Map (halka_house)", Json("halka-world-outdoor", 1, field + ",{\"mapId\":\"halka_house\",\"offset\":{\"x\":100,\"y\":0}}"), "not an outdoor Map"),
                ("the same Map twice", Json("halka-world-outdoor", 1, field + ",{\"mapId\":\"first_field\",\"offset\":{\"x\":40,\"y\":0}}"), "placed twice"),
                ("chunk size 3", Json("halka-world-outdoor", 1, field, 3), "chunk size"),
                ("an offset beyond ±2^24", Json("halka-world-outdoor", 1, "{\"mapId\":\"first_field\",\"offset\":{\"x\":2147483000,\"y\":0}}"), "ERROR"),
                ("no region", Json("halka-world-outdoor", 1, ""), "no region"),
                ("not JSON", "not json at all", "ERROR"),
            };
            foreach (var (label, json, expect) in cases)
            {
                var p = new List<string>();
                var result = OutdoorWorldRegistryJson.Parse(json, resolve, p);
                Check("registry with " + label + " is refused (nothing loaded)", result == null && p.Any(x => x.Contains(expect)), string.Join("; ", p));
            }
            // Overlap: two outdoor Maps on the same cells.
            var (world, _) = TestWorld(64);
            var overlapping = new OutdoorWorldDefinition { worldId = "overlap_test" };
            overlapping.regions.Add(new OutdoorRegionLink { mapId = "first_field", map = Content<MapDefinition>("first_field"), offset = Vector2Int.zero });
            overlapping.regions.Add(new OutdoorRegionLink { mapId = world.regions[0].mapId, map = world.regions[0].map, offset = new Vector2Int(5, 5) });
            Check("two regions on the same cells are refused (overlap)", overlapping.Validate().Any(p => p.Contains("overlaps")),
                string.Join("; ", overlapping.Validate()));
            var apart = new OutdoorWorldDefinition { worldId = "apart_test" };
            apart.regions.Add(new OutdoorRegionLink { mapId = "first_field", map = Content<MapDefinition>("first_field"), offset = Vector2Int.zero });
            apart.regions.Add(new OutdoorRegionLink { mapId = world.regions[0].mapId, map = world.regions[0].map, offset = new Vector2Int(0, 60) });
            Check("a second region placed next to first_field (no overlap) is accepted (the ver3.3 path)", !OutdoorWorldDefinition.HasErrors(apart.Validate()),
                string.Join("; ", apart.Validate()));
            var far = new OutdoorWorldDefinition { worldId = "far_test" };
            far.regions.Add(new OutdoorRegionLink { mapId = "first_field", map = Content<MapDefinition>("first_field"), offset = new Vector2Int(40000, 0) });
            Check("a region beyond ±32768 cells is a WARNING (float precision), not silently accepted",
                far.Validate().Any(p => p.StartsWith("WARNING") && p.Contains("float")) && !OutdoorWorldDefinition.HasErrors(far.Validate()));
            Check("the registry never writes the Authoring Maps (first_field.json unchanged in Git)",
                GitClean("unity-project/Assets/Content/Maps/Authoring") && GitClean("unity-project/Assets/Content/Maps/first_field.asset"));
        }

        // ---------------------------------------------------------------- test worlds (spec 10)

        private static void TestWorldChecks()
        {
            foreach (var size in PocTestWorldGenerator.Sizes)
            {
                var (world, primary) = TestWorld(size);
                OutdoorWorldIndex index = null;
                string error = null;
                try { index = new OutdoorWorldIndex(world); new SharedWorldModel(index); }
                catch (Exception exception) { error = exception.Message; }
                Check($"{size}x{size}: valid world and Maps (registry rules + MapPlacementRules)", index != null && error == null, error);
                if (index == null) continue;
                var half = size / 2;
                Check($"{size}x{size}: covers ({-half},{-half})..({half - 1},{half - 1}) (negative coordinates), {(size <= 64 ? 1 : 4)} region(s)",
                    index.Bounds.Equals(new CellRect(new Vector2Int(-half, -half), new Vector2Int(half - 1, half - 1))) &&
                    index.Regions.Count == (size <= 64 ? 1 : 4));
                var cells = size * size;
                var trees = index.Objects.Count(o => o.DefinitionId == "tree_basic");
                var stones = index.Objects.Count(o => o.DefinitionId == "stone_basic");
                var flowers = index.Objects.Count(o => o.DefinitionId == "flower_basic");
                var road = 0;
                var grass = 0;
                for (var y = -half; y < half; y++)
                for (var x = -half; x < half; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var surface = index.SurfaceAt(cell);
                    if (surface != null && surface.StableId == "dirt") road++;
                    if (index.HasGrass(cell)) grass++;
                }
                Check($"{size}x{size}: realistic density (trees ~4%, stones ~2%, flowers ~3%, roads, grass on most cells)",
                    trees > cells * 0.025 && stones > cells * 0.01 && flowers > cells * 0.018 && road > size && grass > cells / 2,
                    $"trees {trees} stones {stones} flowers {flowers} road {road} grass {grass} of {cells}");
                var chunks = index.Chunks.Count;
                Check($"{size}x{size}: chunk table covers the world and its dressing margin", chunks >= (size / 16) * (size / 16), "chunks " + chunks);
                if (size >= 128)
                    Check($"{size}x{size}: three regions have a non-zero offset (Map cells differ from world cells)",
                        index.Regions.Count(r => r.Offset != Vector2Int.zero) == 3 && index.Regions.All(r => r.Offset == Vector2Int.zero || r.Map.MinCell == Vector2Int.zero));
                var again = PocTestWorldGenerator.Build(LoadOrCreateFixture(false), size, 16, Seed, out var againPrimary);
                Check($"{size}x{size}: the same seed builds the same world", again.regions.Sum(r => r.map.Objects.Count) == world.regions.Sum(r => r.map.Objects.Count) &&
                    again.regions.Zip(world.regions, (a, b) => a.offset == b.offset && a.map.Objects.Select(o => o.InstanceId + o.RootCell).SequenceEqual(b.map.Objects.Select(o => o.InstanceId + o.RootCell))).All(x => x));
                foreach (var link in again.regions) UnityEngine.Object.DestroyImmediate(link.map);
            }
            Check("test worlds are not official content: no Map asset written, first_field untouched",
                !AssetDatabase.FindAssets("chunktest_ t:MapDefinition").Any() && GitClean("unity-project/Assets/Content"));
        }

        // ---------------------------------------------------------------- 19.2 chunk lifecycle (the streamer alone)

        private sealed class FakeBuilder : IChunkBuilder
        {
            public ChunkStreamer Streamer;
            public readonly Dictionary<ChunkCoord, int> Builds = new Dictionary<ChunkCoord, int>();
            public readonly Dictionary<ChunkCoord, int> Releases = new Dictionary<ChunkCoord, int>();
            public readonly HashSet<ChunkCoord> Failing = new HashSet<ChunkCoord>();
            public readonly List<ChunkState> StateWhileBuilding = new List<ChunkState>();
            public int Fallbacks;
            public object Build(ChunkCoord coord)
            {
                if (Streamer != null) StateWhileBuilding.Add(Streamer.StateOf(coord));
                if (Failing.Contains(coord)) throw new InvalidOperationException("injected failure " + coord);
                Builds[coord] = Builds.TryGetValue(coord, out var n) ? n + 1 : 1;
                return "payload " + coord;
            }
            public object BuildFallback(ChunkCoord coord) { Fallbacks++; return "fallback " + coord; }
            public void Release(ChunkCoord coord, object payload) => Releases[coord] = Releases.TryGetValue(coord, out var n) ? n + 1 : 1;
            public int TotalBuilds => Builds.Values.Sum();
        }

        private static CellRect View(Vector2Int focus, int half) => new CellRect(focus - Vector2Int.one * half, focus + Vector2Int.one * half);

        private static void LifecycleChecks()
        {
            var (world, _) = TestWorld(256);
            var index = new OutdoorWorldIndex(world);
            var fake = new FakeBuilder();
            var streamer = new ChunkStreamer(index, fake);
            fake.Streamer = streamer;
            var home = new ChunkCoord(0, 0);
            Check("before anything: every chunk Unloaded", index.Chunks.Keys.All(k => streamer.StateOf(k) == ChunkState.Unloaded));
            streamer.SetFocus(new Vector2Int(8, 8), View(new Vector2Int(8, 8), 10), Vector2Int.zero);
            Check("needed -> Queued (not built yet): the 3 x 3 chunks under a 21-cell view", streamer.StateOf(home) == ChunkState.Queued && fake.TotalBuilds == 0 && streamer.Required.Count == 9,
                "required " + streamer.Required.Count);
            streamer.Update(1000f, 0);
            Check("Queued -> Loading while it is built -> Ready", fake.StateWhileBuilding.Count > 0 && fake.StateWhileBuilding.All(s => s == ChunkState.Loading) &&
                streamer.StateOf(home) == ChunkState.Ready && streamer.Stats.Loaded == 9);
            var builds = fake.TotalBuilds;
            streamer.SetFocus(new Vector2Int(8, 8), View(new Vector2Int(8, 8), 10), Vector2Int.zero);
            streamer.SetFocus(new Vector2Int(8, 8), View(new Vector2Int(8, 8), 10), Vector2Int.zero);
            streamer.Update(1000f, 0);
            streamer.EnsureReady(home, 0);
            Check("no double load: asking again (SetFocus twice, EnsureReady on a Ready chunk) builds nothing", fake.TotalBuilds == builds);

            // Walk East: the view leaves chunk (-1,*) -> release candidate (kept by hysteresis) -> released.
            var west = new ChunkCoord(-1, 0);
            streamer.SetFocus(new Vector2Int(26, 8), View(new Vector2Int(26, 8), 10), Vector2Int.right);
            streamer.Update(1000f, 0);
            Check("Ready -> release candidate: no longer required but kept inside the hysteresis band",
                streamer.StateOf(west) == ChunkState.Ready && streamer.IsReleaseCandidate(west) && streamer.IsKept(west));
            streamer.SetFocus(new Vector2Int(60, 8), View(new Vector2Int(60, 8), 10), Vector2Int.right);
            streamer.Update(1000f, 0);
            Check("release candidate -> Unloaded once out of the band: its payload is released (once)",
                streamer.StateOf(west) == ChunkState.Unloaded && fake.Releases.TryGetValue(west, out var r) && r == 1);
            Check("walking direction prefetch: one more chunk ahead (East) is required while moving",
                streamer.LastRequiredCells.Max.x >= 60 + 10 + streamer.PrefetchCells, streamer.LastRequiredCells.ToString());
            streamer.SetFocus(new Vector2Int(8, 8), View(new Vector2Int(8, 8), 10), Vector2Int.left);
            streamer.Update(1000f, 0);
            Check("reload: coming back builds the released chunk again (Ready, built twice in all)",
                streamer.StateOf(home) == ChunkState.Ready && fake.Builds[home] == 2 && streamer.Records[home].Loads == 2);

            // Oscillation at a border: no rebuild.
            var before = fake.TotalBuilds;
            var unloads = streamer.Stats.TotalUnloads;
            for (var i = 0; i < 40; i++)
            {
                var x = i % 2 == 0 ? 15 : 16;
                streamer.SetFocus(new Vector2Int(x, 8), View(new Vector2Int(x, 8), 10), i % 2 == 0 ? Vector2Int.left : Vector2Int.right);
                streamer.Update(1000f, 0);
            }
            Check("back and forth over a chunk border 40 times: nothing is built or released again after the first crossing",
                fake.TotalBuilds - before <= 2 && streamer.Stats.TotalUnloads == unloads, $"builds +{fake.TotalBuilds - before} unloads +{streamer.Stats.TotalUnloads - unloads}");

            // Cancel: needed then not needed before the update.
            var cancels = streamer.Stats.TotalCancels;
            builds = fake.TotalBuilds;
            streamer.SetFocus(new Vector2Int(-100, -100), View(new Vector2Int(-100, -100), 10), Vector2Int.zero);
            streamer.SetFocus(new Vector2Int(8, 8), View(new Vector2Int(8, 8), 10), Vector2Int.zero);
            streamer.Update(1000f, 0);
            Check("cancel: chunks queued for a place left before their turn are never built",
                streamer.Stats.TotalCancels > cancels && fake.TotalBuilds == builds && streamer.StateOf(new ChunkCoord(-7, -7)) == ChunkState.Unloaded,
                $"cancels +{streamer.Stats.TotalCancels - cancels}");

            // Many at once (a jump): the budget builds at least one per update; LoadAllRequiredNow builds all.
            streamer.SetFocus(new Vector2Int(-90, 90), View(new Vector2Int(-90, 90), 40), Vector2Int.zero);
            var need = streamer.Required.Count;
            streamer.Update(0f, 0);
            var afterOne = streamer.Stats.Loaded;
            var updates = 1;
            while (streamer.Stats.Queued > 0 && updates < 200) { streamer.Update(0f, 0); updates++; }
            Check($"many chunks needed at once ({need}): built over several frames (budget 0 ms still builds one per frame), all Ready",
                need > 20 && streamer.Required.All(k => streamer.StateOf(k) == ChunkState.Ready) && updates >= need / 2, $"updates {updates} first {afterOne}");
            Check("nearest first: the chunk under the focus is built before far ones", streamer.Records[new ChunkCoord(-6, 5)].Loads >= 1);

            // Failures: retry, then fallback; the step gate never waits.
            var bad = new ChunkCoord(5, 5);
            fake.Failing.Add(bad);
            streamer.SetFocus(new Vector2Int(88, 88), View(new Vector2Int(88, 88), 6), Vector2Int.zero);
            streamer.Update(1000f, 10);
            Check("a failing build -> Failed (logged, retried later), the others still load", streamer.StateOf(bad) == ChunkState.Failed &&
                streamer.Stats.TotalFailures >= 1);
            streamer.Update(1000f, 10.5);
            Check("no retry before its delay", streamer.Records[bad].Failures == 1);
            fake.Failing.Remove(bad);
            streamer.Update(1000f, 11.1);
            Check("error recovery: the retry succeeds -> Ready (real chunk, not fallback)", streamer.StateOf(bad) == ChunkState.Ready && !streamer.Records[bad].IsFallback);
            var worse = new ChunkCoord(-5, -5);
            fake.Failing.Add(worse);
            streamer.SetFocus(new Vector2Int(-72, -72), View(new Vector2Int(-72, -72), 6), Vector2Int.zero);
            for (var t = 20.0; t < 40.0; t += 0.5) streamer.Update(1000f, t);
            Check($"a chunk that keeps failing becomes the plain fallback after {ChunkStreamer.MaxRetries} retries (never a hole)",
                streamer.StateOf(worse) == ChunkState.Ready && streamer.Records[worse].IsFallback && fake.Fallbacks >= 1);
            var gate = new ChunkCoord(-9, 2);
            fake.Failing.Add(gate);
            var ready = streamer.EnsureReady(gate, 50);
            Check("the step gate (EnsureReady) on a failing chunk: fallback at once, she can walk on", ready && streamer.Records[gate].IsFallback);
            Check("a cell outside every region has no chunk to wait for (IsCellReady true, nothing built)",
                streamer.IsCellReady(new Vector2Int(5000, 5000)) && streamer.StateOf(WorldGrid.ChunkOf(new Vector2Int(5000, 5000), 16)) == ChunkState.Unloaded);
            streamer.ReleaseAll();
            Check("ReleaseAll frees every built chunk", fake.Releases.Values.Sum() == fake.TotalBuilds + fake.Fallbacks && streamer.Stats.Loaded == 0,
                $"released {fake.Releases.Values.Sum()} built {fake.TotalBuilds} fallback {fake.Fallbacks}");

            // Wide Objects bring their owner chunk.
            var house = index.Objects.First(o => o.DefinitionId == "house_main");
            var extent = OutdoorWorldIndex.PresentationExtent(house);
            var ownChunk = index.ChunkOfCell(house.RootCell);
            var reach = new List<ChunkCoord>();
            for (var y = extent.Min.y; y <= extent.Max.y; y++)
            for (var x = extent.Min.x; x <= extent.Max.x; x++)
            {
                var k = index.ChunkOfCell(new Vector2Int(x, y));
                if (k != ownChunk && !reach.Contains(k)) reach.Add(k);
            }
            Check("the house's picture reaches other chunks, which list its owner chunk", reach.Count > 0 && reach.All(k => index.ChunkAt(k).ReachingOwners.Contains(ownChunk)),
                "owner " + ownChunk + " reaches " + string.Join(" ", reach));
            var probe = new ChunkStreamer(index, new FakeBuilder());
            var other = reach[0];
            var otherRect = WorldGrid.ChunkRect(other, 16);
            var edgeCell = Vector2Int.Max(extent.Min, otherRect.Min);   // a cell of the picture inside the other chunk
            probe.SetFocus(edgeCell, new CellRect(edgeCell, edgeCell), Vector2Int.zero);
            Check("a view touching only the reached chunk still requires the owner chunk (the house is drawn, not cut)",
                probe.Required.Contains(other) && probe.Required.Contains(ownChunk));
        }

        // ---------------------------------------------------------------- official field: unchanged

        private static void OfficialFieldChecks()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            var presenter = c.Presenter;
            var map = Content<MapDefinition>("first_field");
            Check("first_field is drawn in chunk mode (outdoor_main, chunk 16)", presenter.ChunkMode && presenter.ChunkIndex.WorldId == "outdoor_main" &&
                presenter.ChunkIndex.ChunkSize == 16, ChunkSummary(c));
            Check("the whole field and its dressing margin are drawn at the start (4 chunks: (-2..0) x (-1..0) as needed)",
                presenter.ChunkIndex.Chunks.Keys.All(presenter.IsChunkDrawn), ChunkSummary(c));
            // The ver3.1 builder on the same Map: the same ground, roads and tufts.
            var holder = new GameObject("ver3.1 reference build");
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
                var legacy = PocEnvironmentBuilder.Build(new SharedWorldModel(map), catalog.Environment, catalog.VertexColorMaterial, holder.transform, out _, out _);
                var now = presenter.Environment;
                Check("ground identical to ver3.1: grass tiles, road pieces, tufts, triangles",
                    now.GrassTiles == legacy.GrassTiles && now.RoadFull == legacy.RoadFull && now.RoadEdge == legacy.RoadEdge &&
                    now.RoadCorner == legacy.RoadCorner && now.Tufts == legacy.Tufts && now.GroundTriangles == legacy.GroundTriangles &&
                    now.TuftTriangles == legacy.TuftTriangles && now.RoadCells == legacy.RoadCells && now.RoadCellsOutside == legacy.RoadCellsOutside,
                    $"grass {now.GrassTiles}/{legacy.GrassTiles} tufts {now.Tufts}/{legacy.Tufts} tri {now.GroundTriangles}/{legacy.GroundTriangles}");
                var a = new HashSet<Vector3>(now.TuftPositions.Select(p => Round(p)));
                var b = new HashSet<Vector3>(legacy.TuftPositions.Select(p => Round(p)));
                Check("every tuft at the same place as in ver3.1 (no seam shift at chunk borders)", a.SetEquals(b), $"{a.Count} / {b.Count}");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
            var objects = presenter.Views.Where(v => !v.IsWall).ToList();
            Check("every first_field Object drawn once at its cell (no Object moved, none missing, none twice)",
                objects.Count == map.Objects.Count && map.Objects.All(o => objects.Count(v => v.InstanceId == o.InstanceId && v.RootCell == o.RootCell) == 1),
                $"{objects.Count} views / {map.Objects.Count} placements");
            var mismatch = 0;
            for (var y = map.MinCell.y - 9; y <= map.MaxCell.y + 9; y++)
            for (var x = map.MinCell.x - 9; x <= map.MaxCell.x + 9; x++)
            {
                var cell = new Vector2Int(x, y);
                var inside = map.Contains(cell);
                var oldBlocked = !inside || MapPlacementRules.BlocksMovement(map, cell);
                var oldGrass = inside && MapPlacementRules.HasGrass(map, cell);
                if (c.Session.OutdoorWorld.IsBlocked(cell) != oldBlocked || c.Session.OutdoorWorld.HasGrass(cell) != oldGrass) mismatch++;
            }
            Check("collision and grass: every cell (and 9 cells around) answers as ver3.1's rules (outside the field: blocked, no grass)", mismatch == 0, "mismatches " + mismatch);
            var house = c.Session.OutdoorWorld.TryGetHouseDoor(out var door) ? door : default;
            Check("the house door and the Player Start are where ver3.1 had them", house == map.HouseDoorCell && map.PlayerSpawnCell == c.Session.StartMap.PlayerSpawnCell);
            var outlines = objects.All(v => v.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null && f.sharedMesh.uv2 != null && f.sharedMesh.uv2.Length > 0));
            Check("Outline: every drawn Object carries its outline id (uv2), as in ver3.1", outlines);
            Check("Outline ids follow the Map's Object order (1..249), as ver3.1 numbered them",
                objects.All(v => (int)v.GetComponentInChildren<MeshFilter>().sharedMesh.uv2[0].x == map.Objects.ToList().FindIndex(o => o.InstanceId == v.InstanceId) % 249 + 1));
        }

        private static Vector3 Round(Vector3 p) => new Vector3(Mathf.Round(p.x * 1000f), Mathf.Round(p.y * 1000f), Mathf.Round(p.z * 1000f));

        // ---------------------------------------------------------------- 19.3 movement and collision

        private static void MovementChecks()
        {
            var c = StartWorld(64);
            var footsteps = Footsteps();
            Check("64x64: the game starts at the test Player Start, drawn in chunks", c.Session.World.IsOutdoorWorld && c.Session.Cell == PocTestWorldGenerator.PlayerStart &&
                c.Presenter.ChunkMode && c.Presenter.IsChunkDrawn(new ChunkCoord(0, 0)), ChunkSummary(c));
            // Four directions.
            var start = c.Session.Cell;
            var ok4 = true;
            foreach (var d in Dirs)
            {
                var from = c.Session.Cell;
                if (!Walkable(c, from + d)) continue;
                Step(c, d);
                ok4 &= c.Session.Cell == from + d;
                Step(c, -d);
                ok4 &= c.Session.Cell == from;
            }
            Check("four-direction movement (one cell per tap, back again)", ok4 && c.Session.Cell == start);

            // Cross chunk borders East (x 15 -> 16) and West (x 0 -> -1), counting everything.
            var row = Enumerable.Range(-6, 20).Select(dy => new Vector2Int(0, 2 + dy)).FirstOrDefault(p =>
                Enumerable.Range(-3, 24).All(x => Walkable(c, new Vector2Int(x, p.y))));
            Check("a free row for the border walk exists", row != default, row.ToString());
            Check("walked to the row start", WalkTo(c, new Vector2Int(-3, row.y)));
            var steps = c.Session.CompletedSteps;
            var life = c.LifeLog.TotalSteps;
            var plays = footsteps != null ? footsteps.PlayCount : -1;
            var started = 0;
            var completed = 0;
            Action<Vector2Int> onStart = _ => started++;
            Action<Vector2Int> onDone = _ => completed++;
            c.Session.StepStarted += onStart;
            c.Session.StepCompleted += onDone;
            var crossings = new List<string>();
            var facingKept = true;
            for (var x = -3; x < 20; x++)
            {
                var beforeChunk = WorldGrid.ChunkOf(c.Session.Cell, 16);
                Step(c, Vector2Int.right, 0.2f);
                facingKept &= c.Session.Facing == FacingDirection.Right;
                var afterChunk = WorldGrid.ChunkOf(c.Session.Cell, 16);
                if (afterChunk != beforeChunk) crossings.Add(beforeChunk + "->" + afterChunk + " at " + c.Session.Cell);
            }
            c.Session.StepStarted -= onStart;
            c.Session.StepCompleted -= onDone;
            Check("crossing chunk borders (-1|0 at x 0, 0|1 at x 16): she ends 23 cells East, never pulled back",
                c.Session.Cell == new Vector2Int(20, row.y) && crossings.Count == 2, string.Join(", ", crossings));
            Check("23 steps over 2 borders: exactly 23 steps counted (session and Life Log), 23 started, 23 completed",
                c.Session.CompletedSteps - steps == 23 && c.LifeLog.TotalSteps - life == 23 && started == 23 && completed == 23,
                $"session +{c.Session.CompletedSteps - steps} life +{c.LifeLog.TotalSteps - life} started {started} completed {completed}");
            Check("one footstep per step (23), none doubled at a border", footsteps != null && footsteps.PlayCount - plays == 23,
                footsteps != null ? "+" + (footsteps.PlayCount - plays) : "no footstep audio");
            Check("facing stays Right through the borders", facingKept);
            Check("every chunk under her was drawn before she arrived (prefetch: the step gate never had to build one)",
                c.Presenter.Streamer.Stats.SyncLoads == 0, ChunkSummary(c));

            // Mid-step: the chunk changes while she is between two cells.
            WalkTo(c, new Vector2Int(14, row.y));
            Step(c, Vector2Int.right, 0.2f);
            C6.Release(c);
            var raw = Raw(c, Vector2Int.right);
            for (var i = 0; i < 4; i++) Frame(c, 1f / 60f, raw);
            C6.Release(c);
            var midway = c.Session.IsMoving && c.Session.StepFromCell.x == 15 && c.Session.StepToCell.x == 16;
            var midCell = c.Session.Cell;
            Settle(c, 0.3f);
            Check("half-way across the border (15 -> 16) the step finishes on 16, never back on 15",
                midway && c.Session.Cell == new Vector2Int(16, row.y) && !c.Session.IsMoving, $"midway {midway} cell then {midCell} now {c.Session.Cell}");

            // Grass rustle: only the destination of a started step.
            var rustles = new List<Vector2Int>();
            c.Session.StepStarted += rustles.Add;
            Step(c, Vector2Int.left, 0.2f);
            Step(c, Vector2Int.right, 0.2f);
            c.Session.StepStarted -= rustles.Add;
            Check("grass rustles once per started step, at its destination (no rustle from a chunk change)",
                rustles.Count == 2 && rustles[0] == new Vector2Int(15, row.y) && rustles[1] == new Vector2Int(16, row.y));

            // Blocked cells at a border and the world's edge.
            var index = c.Session.OutdoorIndex;
            var borderTree = index.Objects.FirstOrDefault(o => o.DefinitionId == "tree_basic" && (o.RootCell.x == 16 || o.RootCell.x == 0) &&
                Walkable(c, o.RootCell + Vector2Int.left));
            if (borderTree != null)
            {
                WalkTo(c, borderTree.RootCell + Vector2Int.left);
                var at = c.Session.Cell;
                Step(c, Vector2Int.right, 0.2f);
                Check("a tree just across a chunk border blocks the step (world collision, no chunk-local hole)",
                    c.Session.Cell == at && c.Session.World.IsBlocked(borderTree.RootCell), borderTree.InstanceId);
            }
            else Check("a tree just across a chunk border blocks the step", false, "no tree on x=0/16 in the test world");
            var edge = new Vector2Int(31, row.y);
            var edgeReached = WalkTo(c, NearestWalkable(c, edge));
            var beforeEdge = c.Session.Cell;
            var edgeSteps = c.Session.CompletedSteps;
            for (var i = 0; i < 5; i++) Step(c, Vector2Int.right, 0.15f);
            Check("undefined world beyond the edge (x 32+) is not walkable: no grass made up, no step counted",
                edgeReached && c.Session.Cell.x <= 31 && !c.Session.World.CanEnter(new Vector2Int(32, row.y)) && !c.Session.World.HasGrass(new Vector2Int(32, row.y)) &&
                c.Session.CompletedSteps - edgeSteps == Math.Max(0, 31 - beforeEdge.x), $"{beforeEdge} -> {c.Session.Cell}");

            // Collision / grass of every cell of the 128 world equal the Map rules of its region.
            var (world, _) = TestWorld(128);
            var big = new OutdoorWorldIndex(world);
            var wrong = 0;
            foreach (var region in big.Regions)
                for (var y = region.Map.MinCell.y; y <= region.Map.MaxCell.y; y++)
                for (var x = region.Map.MinCell.x; x <= region.Map.MaxCell.x; x++)
                {
                    var local = new Vector2Int(x, y);
                    var w = region.ToWorld(local);
                    if (big.IsBlocked(w) != MapPlacementRules.BlocksMovement(region.Map, local) || big.HasGrass(w) != MapPlacementRules.HasGrass(region.Map, local)) wrong++;
                }
            Check("128x128 (4 regions, 3 with offsets): every world cell's collision and grass equal its Map's rules at its Map cell", wrong == 0, "wrong " + wrong);

            // The crow's cells are not walkable for her (Dynamic Occupancy).
            var crowCell = c.Crow.Cell;
            Check("player and crow occupancy: the crow's ground cell is reserved (CanPlayerEnter false while it stands there)",
                !c.Crow.GroundOccupancyActive || !c.Session.CanPlayerEnter(crowCell), c.Crow.Activity + " " + crowCell);
        }

        // ---------------------------------------------------------------- 9.2 large Objects across chunks

        private static void LargeObjectChecks()
        {
            var c = StartWorld(64);
            var index = c.Session.OutdoorIndex;
            var house = index.Objects.First(o => o.DefinitionId == "house_main");
            var blocked = house.BlockedCells().ToList();
            var chunks = blocked.Select(b => WorldGrid.ChunkOf(b, 16)).Distinct().ToList();
            Check("the test house (root (0,6), 5 x 2) lies on two chunks ((-1,0) and (0,0))", chunks.Count == 2, string.Join(" ", chunks));
            Check("all its blocked cells block on both sides of the border", blocked.All(c.Session.World.IsBlocked) && !c.Session.World.IsBlocked(house.RootCell));
            Check("drawn once (one view, no double build when both chunks are drawn)", c.Presenter.Views.Count(v => v.InstanceId == house.InstanceId) == 1 &&
                chunks.All(c.Presenter.IsChunkDrawn));
            Check("no Object is drawn twice anywhere", DuplicateViews(c).Count == 0, string.Join(" ", DuplicateViews(c)));
            // Benches across x 15|16: blocked cells on both sides, its seats answer.
            var bench = index.Objects.FirstOrDefault(o => o.DefinitionId == "bench_basic");
            if (bench != null)
            {
                var cells = bench.BlockedCells().ToList();
                Check("a bench across chunk border x 15|16 blocks both cells", cells.Count == 2 && cells.Select(b => WorldGrid.ChunkOf(b, 16).X).Distinct().Count() == 2 &&
                    cells.All(c.Session.World.IsBlocked), string.Join(" ", cells));
                var answered = 0;
                var points = bench.Definition.ActionPoints;
                foreach (var point in points)
                {
                    var cell = bench.RootCell + point.PlayerCellOffset;
                    foreach (FacingDirection facing in Enum.GetValues(typeof(FacingDirection)))
                        if (c.Session.World.FindInteractionAhead(cell, facing, out _) == bench) { answered++; break; }
                }
                Check("each bench Action Point answers at its world cell (no shift at the border)", points.Count > 0 && answered == points.Count,
                    $"{answered}/{points.Count}");
                var view = c.Presenter.FindView(bench.InstanceId);
                Check("the bench is drawn at its root cell", view != null && Vector3.Distance(view.transform.position, GridSpace3D.ToWorld(bench.RootCell)) < 1e-4f);
            }
            else Check("a bench across a chunk border exists", false);

            // Walk far away (the house's chunks released) and back: drawn again, once, same place.
            var houseView = c.Presenter.FindView(house.InstanceId);
            var where = houseView.transform.position;
            Check("walked far South-West (house chunks out of range)", WalkTo(c, NearestWalkable(c, new Vector2Int(-28, -28)), null, 0.16f));
            Settle(c, 0.5f);
            var released = chunks.All(k => !c.Presenter.IsChunkDrawn(k)) || c.Presenter.ReachCells3D >= 40;
            var gone = c.Presenter.FindView(house.InstanceId) == null;
            Check("far away: the chunks around her stay drawn; chunks out of reach are released (their GameObjects destroyed)",
                c.Presenter.Streamer.Stats.TotalUnloads > 0 && ChunkRoots(c) == c.Presenter.Streamer.Stats.Loaded, ChunkSummary(c) + " roots " + ChunkRoots(c));
            Check("walked back to the house", WalkTo(c, house.RootCell + Vector2Int.down * 2, null, 0.16f));
            Settle(c, 0.3f);
            var back = c.Presenter.FindView(house.InstanceId);
            Check("back at the house: drawn again, once, at the same place", back != null && Vector3.Distance(back.transform.position, where) < 1e-4f &&
                c.Presenter.Views.Count(v => v.InstanceId == house.InstanceId) == 1, $"was gone {gone} released {released}");
        }

        // ---------------------------------------------------------------- 12 crow

        private static HashSet<Vector2Int> CrowCells(C6.Ctx c, CellRect area)
        {
            var set = new HashSet<Vector2Int>();
            for (var y = area.Min.y; y <= area.Max.y; y++)
            for (var x = area.Min.x; x <= area.Max.x; x++)
                if (c.Crow.ReservesGround(new Vector2Int(x, y))) set.Add(new Vector2Int(x, y));
            return set;
        }

        private static void CrowChecks()
        {
            var c = StartWorld(128);
            var crows = UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check("one crow in the session, not inside any chunk (its state is the session's, not a chunk's)", crows.Length == 1 &&
                !crows[0].GetComponentsInParent<Transform>(true).Any(t => t.name.StartsWith("Chunk (", StringComparison.Ordinal)), "crows " + crows.Length);
            Check("the crow starts at the test Crow Start", c.Crow.Cell == PocTestWorldGenerator.CrowStart, c.Crow.Cell.ToString());
            // Perches: the trees of its Map, also those of chunks not drawn.
            var trees = c.Session.OutdoorWorld.Objects.Where(o => o.DefinitionId == "tree_basic" && o.RegionMapId == "chunktest_128_ne").ToList();
            var undrawn = trees.Where(t => !c.Presenter.IsChunkDrawn(WorldGrid.ChunkOf(t.RootCell, 16))).ToList();
            var computedOk = undrawn.All(t => c.Presenter.TryGetPerchPosition(t, out _));
            // A drawn tree: computed point = drawn PerchAnchor.
            var maxError = 0f;
            foreach (var tree in trees.Where(t => c.Presenter.FindView(t.InstanceId) != null).Take(40))
            {
                var view = c.Presenter.FindView(tree.InstanceId);
                var drawn = view.PerchAnchor.position;
                view.gameObject.SetActive(false);
                c.Presenter.TryGetPerchPosition(tree, out var computed);
                view.gameObject.SetActive(true);
                maxError = Mathf.Max(maxError, Vector3.Distance(drawn, computed));
            }
            Check("perch points of trees in undrawn chunks come from the logical Object (same point as the drawn PerchAnchor)",
                undrawn.Count > 0 && computedOk && maxError < 1e-3f, $"undrawn {undrawn.Count} max error {maxError:0.00000}");
            // Run the crow for a long while, walking her far away and back; the crow keeps its life.
            var activities = new HashSet<CrowActivity>();
            var spawnResets = 0;
            var lastCell = c.Crow.Cell;
            Action<Vector2Int> watch = _ =>
            {
                activities.Add(c.Crow.Activity);
                if (c.Crow.Cell == PocTestWorldGenerator.CrowStart && Vector2Int.Distance(lastCell, PocTestWorldGenerator.CrowStart) > 2.5f && !c.Crow.IsMoving)
                    spawnResets++;
                lastCell = c.Crow.Cell;
            };
            for (var t = 0f; t < 60f; t += 0.05f) { Frame(c, 0.05f, Vector2Int.zero); watch(default); }
            var unloadsBefore = c.Presenter.Streamer.Stats.TotalUnloads;
            WalkTo(c, NearestWalkable(c, new Vector2Int(-50, -50)), watch, 0.14f);
            for (var t = 0f; t < 20f; t += 0.05f) { Frame(c, 0.05f, Vector2Int.zero); watch(default); }
            WalkTo(c, NearestWalkable(c, new Vector2Int(4, 4)), watch, 0.14f);
            for (var t = 0f; t < 40f; t += 0.05f) { Frame(c, 0.05f, Vector2Int.zero); watch(default); }
            var count = UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Check("chunks were released and rebuilt meanwhile", c.Presenter.Streamer.Stats.TotalUnloads > unloadsBefore, ChunkSummary(c));
            Check("still one crow (never duplicated by a chunk load)", count == 1);
            Check("the crow kept living (walked / flew / perched) and never jumped back to its spawn", activities.Count >= 2 && spawnResets == 0,
                string.Join(",", activities) + " resets " + spawnResets);
            Check("the crow stays in its own region (its ver3.1 range)", c.Session.OutdoorIndex.FindRegion("chunktest_128_ne").ContainsWorld(c.Crow.Cell), c.Crow.Cell.ToString());
            var area = new CellRect(c.Crow.Cell - Vector2Int.one * 3, c.Crow.Cell + Vector2Int.one * 3);
            var reserved = CrowCells(c, area);
            Check("its occupied cells are right (only where it stands or walks to, while on the ground)",
                c.Crow.GroundOccupancyActive ? reserved.Contains(c.Crow.Cell) && reserved.Count <= 2 : reserved.Count == 0,
                c.Crow.Activity + " " + string.Join(" ", reserved));
            var target = c.Crow.TargetTreeView;
            Check("a perch target, when it has one, is a real tree", target == null || target.DefinitionId == "tree_basic");
        }

        // ---------------------------------------------------------------- 13 AUTO

        private static void AutoChecks()
        {
            var c = StartWorld(256);
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var loads = c.Session.MapLoads;
            var visited = new HashSet<Vector2Int>();
            var notReady = 0;
            var longest = 0.0;
            for (var t = 0f; t < 600f; t += 0.05f)
            {
                var watch = Stopwatch.StartNew();
                Frame(c, 0.05f, Vector2Int.zero, true);
                longest = Math.Max(longest, watch.Elapsed.TotalMilliseconds);
                visited.Add(c.Session.Cell);
                if (!c.Presenter.Streamer.IsCellReady(c.Session.Cell)) notReady++;
            }
            Check("AUTO in the 256x256 world for 10 minutes: walks (many cells), never changes the map", visited.Count > 20 && c.Session.MapLoads == loads,
                "cells " + visited.Count);
            Check("AUTO only walks where the chunk is drawn (never into an undrawn chunk)", notReady == 0, "not ready " + notReady);
            var maxDistance = visited.Max(v => Mathf.Max(Mathf.Abs(v.x - PocTestWorldGenerator.PlayerStart.x), Mathf.Abs(v.y - PocTestWorldGenerator.PlayerStart.y)));
            Check("AUTO's route search stays inside the drawn area (no search over the whole world)", maxDistance <= 16 * 4 + 40, "max distance " + maxDistance);
            Check("AUTO never steps onto the house door (no map change by itself)", !visited.Contains(c.Session.OutdoorIndex.Objects.First(o => o.DefinitionId == "house_main").RootCell));
            Check("an AUTO frame stays cheap in the big world (longest frame incl. streaming)", longest < 250, $"{longest:0.0} ms");
            var undrawn = c.Session.OutdoorIndex.Chunks.Keys.First(k => !c.Presenter.IsChunkDrawn(k) && c.Session.OutdoorIndex.ChunkAt(k).RegionCells > 0);
            var cell = WorldGrid.ChunkRect(undrawn, 16).Min;
            Check("AUTO refuses a cell of an undrawn chunk (CanAutoEnter false)", !c.Auto.CanAutoEnter(cell) || !c.Session.World.CanEnter(cell));
        }

        // ---------------------------------------------------------------- 19.4 2D / 3D

        private static void ViewChecks()
        {
            // The 256 world: far from its edges the 2D camera is never held by the world's bounds
            // (ver3.1's clamp, kept: near the edge it stops, as on first_field).
            var c = StartWorld(256);
            var grass = (List<Vector2Int>)typeof(PocPixelView2D).GetField("grassCells", Hidden).GetValue(c.Pixel);
            Settle(c, 0.2f);
            c.Pixel.HasGrass(Vector2Int.zero);
            var drawnGrass = c.Presenter.ReadyChunks().Sum(k =>
            {
                var r = WorldGrid.ChunkRect(k, 16);
                var n = 0;
                for (var y = r.Min.y; y <= r.Max.y; y++)
                for (var x = r.Min.x; x <= r.Max.x; x++) if (c.Session.OutdoorWorld.HasGrass(new Vector2Int(x, y))) n++;
                return n;
            });
            Check("2D grass: every grass cell of every drawn chunk is listed, North rows first", grass.Count == drawnGrass &&
                grass.Zip(grass.Skip(1), (a, b) => a.y > b.y || a.y == b.y && a.x < b.x).All(x => x), $"{grass.Count} / {drawnGrass}");
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                c.Switcher.SetMode(mode);
                Settle(c, 0.3f);
                var tag = mode == PocViewMode.PixelView2D ? "2D: " : "3D: ";
                var from = c.Session.Cell;
                var goal = NearestWalkable(c, from + new Vector2Int(mode == PocViewMode.PixelView2D ? 20 : -20, 0));
                var loadsBefore = c.Presenter.Streamer.Stats.TotalLoads;
                Check(tag + "walks over chunk borders", WalkTo(c, goal, null, 0.18f), from + " -> " + goal + " at " + c.Session.Cell);
                Check(tag + "chunks loaded on the way", c.Presenter.Streamer.Stats.TotalLoads > loadsBefore || c.Presenter.ReadyChunks().Count() > 0, ChunkSummary(c));
                Settle(c, 1.5f);
                var camera = mode == PocViewMode.PixelView2D ? c.Pixel.WorldCamera : c.Third.ViewCamera;
                var ray = new Ray(camera.transform.position, camera.transform.forward);
                var aim = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);   // where it looks on the ground
                var player = GridSpace3D.ToWorld(c.Session.Cell);
                var flat = new Vector2(aim.x - player.x, aim.z - player.z).magnitude;
                Check(tag + "camera follows her across the borders (it looks at her)", flat < 2.5f, $"aim {aim} player {player}");
                Check(tag + "she is drawn at her World cell", Vector3.Distance(new Vector3(c.Actor.transform.position.x, 0f, c.Actor.transform.position.z),
                    new Vector3(player.x, 0f, player.z)) < 0.05f, c.Actor.transform.position + " vs " + player);
            }

            // Switch on a border cell, many times: nothing of the world changes, no chunk is rebuilt.
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            WalkTo(c, NearestWalkable(c, new Vector2Int(16, 2)));
            WalkTo(c, NearestWalkable(c, new Vector2Int(15, c.Session.Cell.y)));
            Settle(c, 0.5f);
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var before = (c.Session.Cell, c.Session.Facing, c.Session.CompletedSteps, c.LifeLog.TotalSteps, c.Clock.AnchorMilliseconds, c.Auto.AutoEnabled,
                WorldGrid.ChunkOf(c.Session.Cell, 16), c.Crow.Cell);
            var version = c.Presenter.ChunkVersion;
            var loads = c.Presenter.Streamer.Stats.TotalLoads;
            for (var i = 0; i < 12; i++) { c.Switcher.Toggle(); Frame(c, 1f / 60f, Vector2Int.zero); }
            var after = (c.Session.Cell, c.Session.Facing, c.Session.CompletedSteps, c.LifeLog.TotalSteps, c.Clock.AnchorMilliseconds, c.Auto.AutoEnabled,
                WorldGrid.ChunkOf(c.Session.Cell, 16), c.Crow.Cell);
            Check("12 view switches at a chunk border: cell, facing, steps, time, AUTO, chunk, crow unchanged", before.Equals(after), before + " -> " + after);
            Check("…and the drawn chunks are shared: no chunk built or released by a switch", c.Presenter.ChunkVersion == version &&
                c.Presenter.Streamer.Stats.TotalLoads == loads);
            c.Auto.SetAutoEnabled(false, C6.Clock);

            // Switch while chunks are still queued (a jump far away, in the 256 world).
            c = StartWorld(256);
            c.Session.PlaceForCheck(NearestWalkable(c, new Vector2Int(-100, -100)), FacingDirection.Down);
            c.Presenter.ChunkBudgetMs = 0f;
            Frame(c, 1f / 60f, Vector2Int.zero);
            var queued = c.Presenter.Streamer.Stats.Queued;
            c.Switcher.Toggle();
            for (var i = 0; i < 120 && c.Presenter.Streamer.Stats.Queued > 0; i++) Frame(c, 1f / 60f, Vector2Int.zero);
            c.Presenter.ChunkBudgetMs = 4f;
            var cellNow = c.Session.Cell;
            Step(c, Dirs.First(d => Walkable(c, cellNow + d)));
            Check("switching while chunks are queued: the queue still drains and she walks on", queued > 0 && c.Presenter.Streamer.Stats.Queued == 0 &&
                c.Session.Cell != cellNow, $"queued {queued}");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.2f);

            // Tree fade / occlusion and outline settings are those of ver3.1 for every drawn tree.
            var trees = c.Presenter.Views.Where(v => v.DefinitionId == "tree_basic").ToList();
            var binding = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath).Find("tree_basic");
            Check("tree fade: drawn trees keep the catalog's 2D / 3D fade and occluder settings", trees.Count > 0 &&
                trees.All(t => t.FadeInFront2D == binding.fadeInFront2D && t.FadeInFront3D == binding.fadeInFront3D && t.CameraOccluder == binding.cameraOccluder),
                "trees " + trees.Count);
            Check("Outline: every drawn Object and the tufts carry an outline id",
                c.Presenter.Views.Where(v => !v.IsWall).All(v => v.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.uv2 != null && f.sharedMesh.uv2.Length > 0)));
            Check("shadows: trees cast shadows as in ver3.1", trees.All(t => t.GetComponentsInChildren<Renderer>().Any(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)));
            Check("one shared ground material for every chunk (the time of day lights them all alike)",
                c.Presenter.ReadyChunks().Count() > 1 && UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(r => r.name.StartsWith("Ground", StringComparison.Ordinal)).Select(r => r.sharedMaterial).Distinct().Count() == 1);
            c.Presenter.ChunkBordersVisible = true;
            var borders = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("Chunk border", StringComparison.Ordinal));
            c.Presenter.ChunkBordersVisible = false;
            var hidden = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("Chunk border", StringComparison.Ordinal));
            Check("debug chunk borders: shown only when switched on (one per drawn chunk), hidden by default", borders == c.Presenter.ReadyChunks().Count() && hidden == 0,
                $"shown {borders} hidden {hidden}");
        }

        // ---------------------------------------------------------------- 7.6 failures

        private static void FailureChecks()
        {
            var c = StartWorld(256);
            var target = new ChunkCoord(-5, 0);   // x -80..-65: far outside the first view
            c.Presenter.FailChunkBuildForTest = k => k == target;
            var walkedIn = WalkTo(c, NearestWalkable(c, new Vector2Int(-70, 4)), null, 0.12f);
            Check("a chunk whose build fails: she still walks into it (fallback ground, the logical world decides)", walkedIn &&
                c.Presenter.Streamer.StateOf(target) == ChunkState.Ready && c.Presenter.Streamer.Records[target].IsFallback, ChunkSummary(c));
            Check("…the failure was counted, nothing else broke", c.Presenter.Streamer.Stats.TotalFailures >= 1 && DuplicateViews(c).Count == 0);
            var blocked = c.Session.OutdoorIndex.Objects.FirstOrDefault(o => WorldGrid.ChunkOf(o.RootCell, 16) == target && o.DefinitionId == "tree_basic");
            Check("…collision in the fallback chunk is unchanged (its trees still block)", blocked != null && c.Session.World.IsBlocked(blocked.RootCell));
            c.Presenter.FailChunkBuildForTest = null;
            c.Presenter.ChunkBudgetMs = 4f;
            WalkTo(c, NearestWalkable(c, new Vector2Int(10, 4)), null, 0.12f);
            Settle(c, 0.3f);
            WalkTo(c, NearestWalkable(c, new Vector2Int(-70, 4)), null, 0.12f);
            Settle(c, 0.3f);
            Check("after the cause is gone, the chunk is built properly when it comes back", c.Presenter.Streamer.StateOf(target) == ChunkState.Ready &&
                !c.Presenter.Streamer.Records[target].IsFallback, ChunkSummary(c));

            // The step gate when the prefetch is late: a jump next to an undrawn chunk.
            c.Presenter.ChunkBudgetMs = 0f;
            var sync = c.Presenter.Streamer.Stats.SyncLoads;
            c.Session.PlaceForCheck(NearestWalkable(c, new Vector2Int(100, -100)), FacingDirection.Left);
            var cell = c.Session.Cell;
            var dir = Dirs.First(d => Walkable(c, cell + d));
            Step(c, dir, 0.25f);
            Check("prefetch late (budget 0, a jump): the step goes on, its chunk is built at once (sync), she is never stuck",
                c.Session.Cell == cell + dir && c.Presenter.Streamer.IsCellReady(c.Session.Cell), $"sync +{c.Presenter.Streamer.Stats.SyncLoads - sync}");
            c.Presenter.ChunkBudgetMs = 4f;

            // Entering the house while chunks are queued, saving meanwhile.
            var storage = new HarukaMemoryStorage();
            var g = StartWorld(64, 16, storage);
            Settle(g, 1.2f);
            g.Presenter.ChunkBudgetMs = 0f;
            var house = g.Session.OutdoorIndex.Objects.First(o => o.DefinitionId == "house_main");
            WalkTo(g, house.RootCell + Vector2Int.down, null, 0.16f);
            g.Session.PlaceForCheck(house.RootCell + Vector2Int.down, FacingDirection.Up);
            g.Presenter.UpdateStreaming(false);
            Step(g, Vector2Int.up, 0.4f);
            Check("entering the house while chunks wait: the house loads, nothing outdoors is left drawn", g.Session.World.MapId == "halka_house" &&
                !g.Presenter.ChunkMode && ChunkRoots(g) == 0);
            g.Save.OnPageHidden("");
            var latest = C6.Latest(g);
            Check("…the save written then is a normal Schema 1 save in the house", latest != null && latest.MapId == "halka_house" && latest.Schema == 1);
        }

        // ---------------------------------------------------------------- 19.5 interiors

        private static void InteriorChecks()
        {
            foreach (var size in new[] { 0, 64 })
            {
                var c = size == 0 ? C6.Start(new HarukaMemoryStorage()) : StartWorld(64);
                var tag = size == 0 ? "first_field: " : "test world 64: ";
                c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
                foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                {
                    c.Switcher.SetMode(mode);
                    var t = tag + (mode == PocViewMode.PixelView2D ? "2D " : "3D ");
                    if (c.Session.World != c.Session.OutdoorWorld) { c.Session.LoadMapForCheck(false, door + Vector2Int.down, FacingDirection.Up); Settle(c, 0.3f); }
                    WalkTo(c, door + Vector2Int.down, null, 0.16f);
                    var steps = c.Session.CompletedSteps;
                    Step(c, Vector2Int.up, 0.4f);
                    Check(t + "outdoors -> halka_house (door at world " + door + ")", c.Session.World.MapId == "halka_house" && c.Session.Cell == new Vector2Int(0, -3) &&
                        c.Session.CompletedSteps == steps + 1, c.Session.World.MapId + " " + c.Session.Cell);
                    Check(t + "indoors: no chunk drawn, interior pieces only", !c.Presenter.ChunkMode && ChunkRoots(c) == 0 && c.Presenter.Interior != null);
                    WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.16f);
                    Step(c, Vector2Int.up, 0.4f);
                    Check(t + "halka_house -> halka_soundbooth", c.Session.World.MapId == "halka_soundbooth" && c.Session.Cell == WorldVer31Checks.BoothEntry,
                        c.Session.World.MapId + " " + c.Session.Cell);
                    Step(c, Vector2Int.down, 0.4f);
                    Check(t + "halka_soundbooth -> halka_house", c.Session.World.MapId == "halka_house" && c.Session.Cell == WorldVer31Checks.BoothApproach);
                    WalkTo(c, new Vector2Int(0, -3), null, 0.16f);
                    Step(c, Vector2Int.down, 0.4f);
                    Check(t + "halka_house -> outdoors, in front of the door (world cell)", c.Session.World == c.Session.OutdoorWorld && c.Session.Cell == door + Vector2Int.down,
                        c.Session.World.MapId + " " + c.Session.Cell);
                    Check(t + "outdoors again: drawn in chunks around her", c.Presenter.ChunkMode && c.Presenter.Streamer.IsCellReady(c.Session.Cell) &&
                        c.Presenter.IsChunkDrawn(WorldGrid.ChunkOf(c.Session.Cell, c.Session.OutdoorIndex.ChunkSize)), ChunkSummary(c));
                    var camera = mode == PocViewMode.PixelView2D ? c.Pixel.WorldCamera : c.Third.ViewCamera;
                    Frame(c, 1f / 60f, Vector2Int.zero);
                    var first = camera.transform.position;
                    Settle(c, 1.5f);
                    Check(t + "camera snapped on leaving (no slide)", Vector3.Distance(first, camera.transform.position) < 0.06f, first + " / " + camera.transform.position);
                }
                // Mixed route: 2D in, 3D in the booth, 2D out.
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                WalkTo(c, door + Vector2Int.down, null, 0.16f);
                Step(c, Vector2Int.up, 0.4f);
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.16f);
                Step(c, Vector2Int.up, 0.4f);
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                Step(c, Vector2Int.down, 0.4f);
                WalkTo(c, new Vector2Int(0, -3), null, 0.16f);
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                Step(c, Vector2Int.down, 0.4f);
                Check(tag + "mixed 2D / 3D route out and back", c.Session.World == c.Session.OutdoorWorld && c.Session.Cell == door + Vector2Int.down);
                // AUTO never takes a door.
                c.Auto.SetAutoEnabled(true, C6.Clock);
                var loads = c.Session.MapLoads;
                for (var t = 0f; t < 180f; t += 0.05f) Frame(c, 0.05f, Vector2Int.zero, true);
                Check(tag + "AUTO for 3 minutes next to the door: never enters by itself", c.Session.MapLoads == loads && c.Session.World == c.Session.OutdoorWorld);
                c.Auto.SetAutoEnabled(false, C6.Clock);
            }
        }

        // ---------------------------------------------------------------- 19.6 save

        private static HarukaSaveData Old(string version, string map, Vector2Int cell, string view = HarukaSaveData.View2D, bool auto = false) => new HarukaSaveData
        {
            GameVersion = version, SavedAtUtc = "2026-10-21T03:04:05Z", Revision = 0, TotalSteps = 4321, TotalPlaySeconds = 765,
            TimeAnchorUnixMs = 1760000000000L, AutoEnabled = auto, ViewMode = view, MapId = map, CellX = cell.x, CellY = cell.y, Facing = "left",
        };

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            Check("Save Schema stays 1", HarukaSaveData.SchemaVersion == 1);
            var places = new (string version, string map, Vector2Int cell)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0)), ("ver3.0", "halka_house", new Vector2Int(0, 0)),
                ("ver3.1", "first_field", new Vector2Int(9, -5)), ("ver3.1", "halka_house", new Vector2Int(1, 0)),
                ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1)),
            };
            foreach (var (version, map, cell) in places)
            foreach (var (view, auto) in new[] { (HarukaSaveData.View2D, true), (HarukaSaveData.View3D, false) })
            {
                var storage = new HarukaMemoryStorage();
                new HarukaSnapshotStore(storage).Write(Old(version, map, cell, view, auto));
                var c = C6.Start(storage);
                Check($"{version} save ({map} {cell}, {view}, AUTO {(auto ? "ON" : "OFF")}): same place, view, AUTO, steps, time",
                    c.Session.World.MapId == map && c.Session.Cell == cell && c.Session.Facing == FacingDirection.Left &&
                    (c.Switcher.Mode == PocViewMode.ThirdPerson3D) == (view == HarukaSaveData.View3D) && c.Auto.AutoEnabled == auto &&
                    c.LifeLog.TotalSteps == 4321 && c.Clock.AnchorMilliseconds == 1760000000000L && c.Session.CompletedSteps == 0,
                    $"{c.Session.World.MapId} {c.Session.Cell} {c.Switcher.Mode} auto {c.Auto.AutoEnabled}");
            }
            // Next to the door and on an invalid cell.
            var door = Content<MapDefinition>("first_field").HouseDoorCell;
            var s1 = new HarukaMemoryStorage();
            new HarukaSnapshotStore(s1).Write(Old("ver3.1", "first_field", door));
            var r1 = C6.Start(s1);
            Check("saved on the house door: resumes on a free cell next to it, outdoors (never on a door)", r1.Session.World.MapId == "first_field" &&
                r1.Session.Cell != door && r1.Session.World.CanEnter(r1.Session.Cell) && !r1.Session.IsTransitionCell(r1.Session.World, r1.Session.Cell), r1.Session.Cell.ToString());
            var s2 = new HarukaMemoryStorage();
            new HarukaSnapshotStore(s2).Write(Old("ver3.1", "first_field", new Vector2Int(40, 40)));
            var r2 = C6.Start(s2);
            Check("a cell outside first_field (undefined world): safe return inside the field, steps kept",
                r2.Session.World.MapId == "first_field" && Content<MapDefinition>("first_field").Contains(r2.Session.Cell) && r2.Session.World.CanEnter(r2.Session.Cell) &&
                r2.LifeLog.TotalSteps == 4321, r2.Session.Cell.ToString());

            // A save written by ver3.2 outdoors: first_field + local cell (= world cell), nothing about chunks.
            var storage2 = new HarukaMemoryStorage();
            var g = C6.Start(storage2);
            Settle(g, 1.2f);
            C6.ToOutdoor(g, new Vector2Int(-1, 0));
            g.Save.OnPageHidden("");
            var latest = C6.Latest(g);
            var text = C6.Sealed(latest);
            Check("ver3.2 outdoor save: Map ID first_field and its local cell, Schema 1, no World ID or chunk stored",
                latest.MapId == "first_field" && latest.CellX == -1 && latest.CellY == 0 && latest.Schema == 1 &&
                text.IndexOf("chunk", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("outdoor_main", StringComparison.Ordinal) < 0);
            var resumed = C6.Start(storage2);
            Check("…restart resumes there", resumed.Session.World.MapId == "first_field" && resumed.Session.Cell == new Vector2Int(-1, 0));

            // Saved on a chunk border cell of a test world region with an offset: Map ID + Map cell.
            var storage3 = new HarukaMemoryStorage();
            var t = StartWorld(128, 16, storage3);
            Settle(t, 1.2f);
            var target = NearestWalkable(t, new Vector2Int(-16, 20));
            Check("test world 128: walked to a chunk border cell in the North-West region", WalkTo(t, target, null, 0.14f), t.Session.Cell.ToString());
            var steps = t.LifeLog.TotalSteps;
            t.Save.OnPageHidden("");
            var saved = C6.Latest(t);
            var nw = t.Session.OutdoorIndex.FindRegion("chunktest_128_nw");
            Check("saved there: the region's Map ID and its Map cell (world - offset), Schema 1",
                saved.MapId == "chunktest_128_nw" && new Vector2Int(saved.CellX, saved.CellY) == nw.ToLocal(t.Session.Cell) && saved.Schema == 1,
                $"{saved.MapId} ({saved.CellX},{saved.CellY}) world {t.Session.Cell}");
            var t2 = StartWorld(128, 16, storage3);
            Check("…restart resumes on the same world cell, steps not added again", t2.Session.Cell == target && t2.LifeLog.TotalSteps == steps,
                $"{t2.Session.Cell} steps {t2.LifeLog.TotalSteps}/{steps}");
            var exported = t2.Save.ExportText();
            var fresh = StartWorld(128, 16);
            var check = fresh.Save.CheckImport(exported, out var imported);
            var applied = check == HarukaSaveSystem.ImportCheck.Ok && fresh.Save.ApplyImport(imported, C6.Clock, out _);
            Check("Export -> Import (region save): accepted, same world cell", applied && fresh.Session.Cell == target, check.ToString());

            // ver3.0 export file and snapshot recovery, in the chunked game.
            var fixture = Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d", "v31_fixtures", "ver30_export_house_3d_auto.json");
            var game = C6.Start(new HarukaMemoryStorage());
            var old = game.Save.CheckImport(File.Exists(fixture) ? File.ReadAllText(fixture) : null, out var oldData);
            Check("a file exported by ver3.0: accepted and applied (house (1,-2), 3D, AUTO ON, 20 steps)", old == HarukaSaveSystem.ImportCheck.Ok &&
                game.Save.ApplyImport(oldData, C6.Clock, out _) && game.Session.World.MapId == "halka_house" && game.Session.Cell == new Vector2Int(1, -2) &&
                game.LifeLog.TotalSteps == 20, old.ToString());
            var damaged = new HarukaMemoryStorage();
            var store = new HarukaSnapshotStore(damaged);
            var older = Old("ver3.1", "first_field", new Vector2Int(9, -5)); older.TotalSteps = 100;
            store.Write(older);
            var newer = Old("ver3.2", "first_field", new Vector2Int(-9, 5)); newer.TotalSteps = 101;
            store.Write(newer);
            var newest = store.Scan().Slots.First(slot => slot.Data != null && slot.Data.TotalSteps == 101).Name;
            damaged.Files[newest] = damaged.Files[newest].Substring(0, damaged.Files[newest].Length / 2);
            var r = C6.Start(damaged);
            Check("newest snapshot damaged: the previous one loads outdoors (first_field (9,-5), 100 steps)",
                r.Session.World.MapId == "first_field" && r.Session.Cell == new Vector2Int(9, -5) && r.LifeLog.TotalSteps == 100);
        }

        // ---------------------------------------------------------------- 16 time of day, Life Log

        private static void TimeChecks()
        {
            C6.SetLegacy(null, null);
            var c = StartWorld(256);
            var anchor = c.Clock.AnchorMilliseconds;
            var seconds = c.LifeLog.TotalPlaySeconds;
            var lifeSteps = c.LifeLog.TotalSteps;
            for (var i = 0; i < 6; i++)
            {
                c.Session.PlaceForCheck(NearestWalkable(c, new Vector2Int(i % 2 == 0 ? -100 : 100, i % 3 == 0 ? -90 : 90)), FacingDirection.Down);
                Settle(c, 0.5f);
            }
            Check("chunk loads and releases do not move the World Time anchor", c.Clock.AnchorMilliseconds == anchor && c.Presenter.Streamer.Stats.TotalUnloads > 0);
            Check("chunk loads add no step (only a completed step counts)", c.LifeLog.TotalSteps == lifeSteps && c.Session.CompletedSteps == 0,
                $"steps {lifeSteps} -> {c.LifeLog.TotalSteps}");
            Check("play time keeps running", c.LifeLog.TotalPlaySeconds >= seconds);
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 0.3f);
            c.Session.PlaceForCheck(NearestWalkable(c, new Vector2Int(-100, 100)), FacingDirection.Down);
            Settle(c, 0.3f);
            var materials = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Ground", StringComparison.Ordinal)).Select(r => r.sharedMaterial).Distinct().ToList();
            Check("a chunk loaded at night uses the same lit materials as the others (no per-chunk time)", materials.Count == 1);
        }

        // ---------------------------------------------------------------- protection

        private static void ProtectionChecks()
        {
            Check("first_field, halka_house, halka_soundbooth assets and their Authoring JSON unchanged (Git)",
                GitClean("unity-project/Assets/Content/Maps/first_field.asset") && GitClean("unity-project/Assets/Content/Maps/halka_house.asset") &&
                GitClean("unity-project/Assets/Content/Maps/halka_soundbooth.asset") && GitClean("unity-project/Assets/Content/Maps/Authoring"));
            Check("WORLD EDITOR v1.0 sources unchanged (Git)", GitClean("tools/HalkaWorldMapEditor") && GitClean("unity-project/Assets/Editor/MapAuthoringImporter.cs"));
            var scene = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project", PocSceneBuilder.RcScenePath));
            Check("the release scene references no test world (no fixture, no chunktest Map)",
                scene.IndexOf("chunktest", StringComparison.Ordinal) < 0 &&
                scene.IndexOf(AssetDatabase.AssetPathToGUID(FixturePath) is string g && g.Length > 0 ? g : "no-fixture-guid", StringComparison.Ordinal) < 0);
            Check("the release scene's outdoor world is the registry's (outdoor_main, first_field at (0,0))",
                scene.Contains("worldId: outdoor_main") && scene.Contains("mapId: first_field") && scene.Contains("offset: {x: 0, y: 0}"));
        }
    }
}
