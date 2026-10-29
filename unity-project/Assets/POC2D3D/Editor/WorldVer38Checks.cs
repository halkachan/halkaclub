using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using Halka.Poc2D3D.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;
using V32 = Halka.Poc2D3D.EditorTools.WorldVer32Checks;
using V33 = Halka.Poc2D3D.EditorTools.WorldVer33Checks;
using V36 = Halka.Poc2D3D.EditorTools.WorldVer36Checks;
using V37 = Halka.Poc2D3D.EditorTools.WorldVer37Checks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.8「南・赤エリア」: the checks of the South region south_area_01 (the registry, the road
    // over the region border y -6|-7 and the chunk border y -16|-17 with negative cells, the red rocks and
    // the hill: their Collision, the reddish road, the 2D camera that keeps every other place's framing,
    // the pinwheel and かざぐるまをまわす, the crow among the rocks, そらをとぶ to みなみのかざぐるま with five
    // places over two pages, the save), of the chunks streaming as she walks the four directions (the
    // official world, a shortened view reach so the South chunks are freed too, the ver3.2 256 x 256 test
    // world East and North) with no mesh left behind, and of what must not change (ver3.7's work
    // included). The game is started with the Phase 6 harness (memory storage only); every frame also
    // runs what Update does in play (the crow, the choices' watch, the chunk streamer, the water, the
    // forest, the pinwheel, the flight, the time of day).
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer38Checks.RunBatch
    public static class WorldVer38Checks
    {
        public const string SouthId = "south_area_01";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Home = "landmark_halka_home";
        private const string Plaza = "landmark_east_flower_plaza";
        private const string Pond = "landmark_north_blue_pond";
        private const string Clearing = "landmark_west_forest_clearing";
        private const string Pinwheel = "landmark_south_red_pinwheel";
        private const string Published = "43cc967";   // ver3.6 as published (2026-10-29), still the published version
        private const string Ver37 = "0c1b4a3";       // ver3.7 as finished (local, not published): this version's base
        private const string Message = "かざぐるまが くるくる まわった！";
        private static readonly Vector2Int SouthOffset = new Vector2Int(-10, -26);
        private static readonly Vector2Int FieldRoadEnd = new Vector2Int(2, -6);   // first_field's South road end
        private static readonly Vector2Int RoadIn = new Vector2Int(2, -7);         // local (12,19)
        private static readonly Vector2Int SouthEnd = new Vector2Int(3, -26);      // local (13,0), the road's South end
        private static readonly Vector2Int TrailMouth = new Vector2Int(1, -13);    // local (11,13), off the road
        private static readonly Vector2Int SignSpot = new Vector2Int(0, -13);      // local (10,13): reads the red sign
        private static readonly Vector2Int HillGate = new Vector2Int(-1, -16);     // local (9,10): the way up
        private static readonly Vector2Int HillTop = new Vector2Int(-1, -17);      // local (9,9): on the hill
        private static readonly Vector2Int PinwheelCell = new Vector2Int(-3, -18); // local (7,8)
        private static readonly Vector2Int TurnSpot = new Vector2Int(-3, -19);     // local (7,7): its Action Point
        private static readonly Vector2Int HillArrival = new Vector2Int(-3, -20);  // local (7,6)
        private static readonly Vector2Int HomeArrival = new Vector2Int(-7, -5);
        private static readonly Vector2Int PlazaArrival = new Vector2Int(27, -2);
        private static readonly Vector2Int PondArrival = new Vector2Int(-3, 15);
        private static readonly Vector2Int ClearingArrival = new Vector2Int(-27, 2);
        private static readonly Vector2Int StumpSpot = new Vector2Int(-27, 3);
        private static readonly Vector2Int WestEnd = new Vector2Int(-35, -1);
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";
        private static int maxCrows;
        private static int crowBlockedFrames;

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
            UnityEngine.Random.InitState(3838);
            try
            {
                Run("region", RegionChecks);
                Run("walk", WalkChecks);
                Run("landscape", LandscapeChecks);
                Run("pinwheel", PinwheelChecks);
                Run("crow", CrowChecks);
                Run("travel", TravelChecks);
                Run("save", SaveChecks);
                Run("chunks", ChunkChecks);
                Run("existing", ExistingChecks);
                Run("protect", ProtectChecks);
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
            foreach (var failure in failures) Debug.LogError("VER38 FAIL " + failure);
            Debug.Log("HARUKA ver3.8 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.8 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V38_LOGS", "ver38_checks.txt");
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
            if (condition) { passes++; Debug.Log("VER38 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness (as WorldVer37Checks)

        private static PocFastTravel T(C6.Ctx c) => c.Gameplay.FastTravel;

        private static C6.Ctx Begin(HarukaMemoryStorage storage, Action<SharedWorldSession> configure = null)
        {
            var c = C6.Start(storage, null, configure);
            var t = T(c);
            C6.Invoke(t, "Awake");
            t.CrowTime = () => C6.Clock;
            c.Presenter.WaterClock = () => C6.Clock;
            t.StartPlay();
            Frame(c, 1f / 60f, Vector2Int.zero);
            return c;
        }

        private static void Frame(C6.Ctx c, float dt, Vector2Int raw, bool auto = false)
        {
            C6.Frame(c, dt, raw, false);
            if (auto) c.Auto.Tick(C6.Clock, !c.Gameplay.AutoWorld.Suspended);
            var crow = c.Crow;
            if (crow != null && crow.isActiveAndEnabled && crow.gameObject.activeInHierarchy && !crow.TravelHeld)
            {
                crow.WatchAir(C6.Clock);
                crow.WatchTravelReturn(C6.Clock);
                crow.Tick(C6.Clock);
            }
            if (c.Gameplay.CrowChoices != null) c.Gameplay.CrowChoices.Watch(C6.Clock);
            if (c.CrowView != null && c.CrowView.isActiveAndEnabled) c.CrowView.Apply(dt, C6.Clock);
            c.Presenter.UpdateStreaming(false);
            c.Presenter.TickWater(C6.Clock);
            c.Time.Apply(C6.Clock);
            c.Presenter.TickForest(C6.Clock);
            c.Presenter.TickPinwheels(C6.Clock);
            T(c).Tick(C6.Clock);
            maxCrows = Mathf.Max(maxCrows, Crows());
            if (c.Session.World == c.Session.OutdoorWorld && crow != null && crow.isActiveAndEnabled && crow.GroundOccupancyActive &&
                !c.Session.OutdoorWorld.CanEnter(crow.Cell)) crowBlockedFrames++;
        }

        private static void Settle(C6.Ctx c, float seconds, bool auto = false)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, auto);
        }

        private static bool Fly(C6.Ctx c)
        {
            for (var i = 0; i < 360 && T(c).Busy; i++) Frame(c, 1f / 60f, Vector2Int.zero);
            return !T(c).Busy;
        }

        // Walks there holding the keys; onFrame sees every frame (the camera's steadiness).
        private static bool Walk(C6.Ctx c, Vector2Int goal, Action onFrame = null, float maxSeconds = 120f)
        {
            var frames = 0;
            var limit = Mathf.CeilToInt(maxSeconds * 60f);
            while (c.Session.Cell != goal && frames < limit)
            {
                var path = Route(c, c.Session.Cell, goal);
                if (path == null || path.Count == 0) { Frame(c, 1f / 60f, Vector2Int.zero); onFrame?.Invoke(); frames++; continue; }
                var next = path[0];
                var from = c.Session.Cell;
                var held = 0;
                while (c.Session.Cell == from && !c.Session.IsMoving && frames < limit && held++ < 180)
                {
                    Frame(c, 1f / 60f, V32.Raw(c, next - from));
                    onFrame?.Invoke();
                    frames++;
                }
                while (c.Session.IsMoving && frames < limit) { Frame(c, 1f / 60f, Vector2Int.zero); onFrame?.Invoke(); frames++; }
            }
            C6.Release(c);
            return c.Session.Cell == goal;
        }

        private static List<Vector2Int> Route(C6.Ctx c, Vector2Int from, Vector2Int goal)
        {
            bool Open(Vector2Int cell) => c.Session.World.CanEnter(cell) && !c.Session.IsTransitionCell(c.Session.World, cell);
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

        private static void SetCore(object target, string name, object value) =>
            typeof(CrowBehaviourCore).GetField(name, Hidden).SetValue(target, value);

        private static void SetCrow(object target, string name, object value) =>
            typeof(PocCrow).GetField(name, Hidden).SetValue(target, value);

        private static void HoldCrow(C6.Ctx c)
        {
            SetCore(c.Crow, "nextFlightAt", C6.Clock + 1000f);
            SetCore(c.Crow, "nextActionAt", C6.Clock + 1000f);
            SetCore(c.Crow, "interactionCooldownUntil", 0f);
            SetCrow(c.Crow, "nextSettleAt", C6.Clock + 1000f);
        }

        private static bool WaitStanding(C6.Ctx c, float seconds = 15f)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f)
            {
                SetCrow(c.Crow, "nextSettleAt", C6.Clock + 1000f);
                if (c.Crow.Activity == CrowActivity.GroundIdle && !c.Crow.IsMoving) return true;
                Frame(c, 1f / 60f, Vector2Int.zero);
            }
            return false;
        }

        private static bool TalkToCrow(C6.Ctx c)
        {
            WaitStanding(c);
            HoldCrow(c);
            foreach (var d in Dirs)
            {
                var stand = c.Crow.Cell - d;
                if (!V32.Walkable(c, stand)) continue;
                c.Session.PlaceForCheck(stand, FacingDirectionExtensions.FromVector(d));
                C6.Frame(c, 1f / 60f, Vector2Int.zero);
                c.Hud.ClearMessage();
                return c.Session.InteractAhead();
            }
            return false;
        }

        private static bool Press(C6.Ctx c, string label)
        {
            var page = c.Menu.Page;
            if (page == null) return false;
            for (var i = 0; i < page.Buttons.Count; i++)
                if (page.Buttons[i] == label) { c.Menu.PressPageButton(i, C6.Clock); return true; }
            return false;
        }

        private static string Buttons(C6.Ctx c) => c.Menu.Page != null ? string.Join("/", c.Menu.Page.Buttons) : "(no page)";

        private static List<Rect> ButtonRects(C6.Ctx c) =>
            Enumerable.Range(0, c.Menu.Page != null ? c.Menu.Page.Buttons.Count : 0).Select(i => c.Menu.TryGetPageButtonRect(i, out var r) ? r : default).ToList();

        private static bool Inside(List<Rect> rects, int count) => rects.Count == count &&
            rects.All(r => r.width > 0 && r.xMin >= 0 && r.yMin >= 0 && r.xMax <= 1280 && r.yMax <= 720) &&
            rects.Zip(rects.Skip(1), (a, b) => !a.Overlaps(b)).All(x => x);

        private static bool StartFollowing(C6.Ctx c)
        {
            HoldCrow(c);
            Settle(c, 0.2f);
            if (!TalkToCrow(c)) return false;
            Press(c, PocCrow.ChoiceFollow);
            Settle(c, 0.1f);
            return c.Crow.Following && c.Menu.Page == null;
        }

        private static string Where(C6.Ctx c) =>
            $"her {c.Session.Cell} {c.Session.CurrentRegionMapId ?? c.Session.World.MapId} {c.Session.Facing} crow {c.Crow.Cell} {c.Crow.CompanionState} mode {c.Crow.Mode}";

        private static int Crows() => UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private static Vector3 CameraFor(C6.Ctx c, Vector2Int cell)
        {
            c.Session.PlaceForCheck(cell, FacingDirection.Up);
            c.Pixel.SnapToTarget();
            C6.Frame(c, 1f / 60f, Vector2Int.zero);
            return c.Pixel.WorldCamera.transform.position;
        }

        private static bool OnScreen(Vector3 p) => p.x > 16f && p.x < 1264f && p.y > 16f && p.y < 704f;

        private static OutdoorRegion South(C6.Ctx c) => c.Session.OutdoorIndex.FindRegion(SouthId);

        private static bool InSouth(C6.Ctx c, Vector2Int cell) => South(c).ContainsWorld(cell);

        private static SharedObjectInstance PinwheelObject(C6.Ctx c) =>
            c.Session.OutdoorIndex.Objects.First(o => o.DefinitionId == PocWorldPresenter.PinwheelDefinitionId);

        // The hill's top (local x 4..10, y 5..9) and its rim (the ring round it), world cells.
        private static IEnumerable<Vector2Int> HillCells()
        {
            for (var y = -21; y <= -17; y++)
            for (var x = -6; x <= 0; x++)
                yield return new Vector2Int(x, y);
        }

        private static IEnumerable<Vector2Int> HillRim()
        {
            for (var x = -7; x <= 1; x++) { yield return new Vector2Int(x, -22); yield return new Vector2Int(x, -16); }
            for (var y = -21; y <= -17; y++) { yield return new Vector2Int(-7, y); yield return new Vector2Int(1, y); }
        }

        // The cells reached from origin through cells that pass (origin included).
        private static HashSet<Vector2Int> Reach(C6.Ctx c, Vector2Int origin, Func<Vector2Int, bool> pass)
        {
            var seen = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>(seen);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = cell + d;
                    if (seen.Contains(n) || !pass(n)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        // Runtime meshes no MeshFilter of the scene uses (inactive objects included): what a freed chunk or
        // room would leave behind. The count of all runtime meshes follows the chunks drawn; this must not grow.
        private static int OrphanMeshes()
        {
            var used = new HashSet<Mesh>(UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(f => f.sharedMesh).Where(m => m != null));
            return Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0 && !used.Contains(m));
        }

        // Meshes that exist only at run time (made by the game, not loaded from the project).
        private static int RuntimeMeshes() =>
            Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0);

        private static Dictionary<string, int> RuntimeMeshNames() =>
            Resources.FindObjectsOfTypeAll<Mesh>().Where(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0)
                .GroupBy(m => System.Text.RegularExpressions.Regex.Replace(m.name, @"[-0-9, ()]+$", "").Trim()).ToDictionary(g => g.Key, g => g.Count());

        private static string LeftBehind(Dictionary<string, int> before) =>
            string.Join(", ", RuntimeMeshNames().Where(p => p.Value > before.GetValueOrDefault(p.Key)).OrderByDescending(p => p.Value - before.GetValueOrDefault(p.Key))
                .Take(12).Select(p => p.Key + " +" + (p.Value - before.GetValueOrDefault(p.Key))));

        private static int AllTransforms() => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        // Every Transform under a drawn chunk's root.
        private static int ChunkTransforms() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Chunk (", StringComparison.Ordinal)).Sum(t => t.GetComponentsInChildren<Transform>(true).Length);

        // The road's colour where it is drawn on this cell: the ground mesh's vertices over it at road height.
        private static Color RoadColour(Vector2Int cell)
        {
            var centre = GridSpace3D.ToWorld(cell);
            var half = GridSpace3D.CellSize * 0.5f - 0.02f;
            var sum = Color.clear;
            var n = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("Ground ", StringComparison.Ordinal)) continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices;
                var col = mesh.colors;
                for (var i = 0; i < v.Length && i < col.Length; i++)
                {
                    var p = r.transform.TransformPoint(v[i]);
                    if (p.y < 0.003f || Mathf.Abs(p.x - centre.x) > half || Mathf.Abs(p.z - centre.z) > half) continue;
                    sum += col[i];
                    n++;
                }
            }
            return n > 0 ? sum / n : Color.clear;
        }

        // ---------------------------------------------------------------- 19.1 the region

        private static void RegionChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var definition = index.Definition;
            var south = South(c);
            Check("the registry: first_field, east_area_01, north_area_01, west_area_01 and south_area_01 (in that order)",
                index.Regions.Select(r => r.MapId).SequenceEqual(new[] { "first_field", V33.EastId, V36.NorthId, V37.WestId, SouthId }), string.Join(",", index.Regions.Select(r => r.MapId)));
            Check("south_area_01: direction south, World offset (-10,-26), 21 x 20 cells: World (-10,-26)..(10,-7)",
                south != null && south.Direction == "south" && south.Offset == SouthOffset &&
                south.Rect.Min == new Vector2Int(-10, -26) && south.Rect.Max == new Vector2Int(10, -7), south != null ? $"{south.Offset} {south.Rect.Min}..{south.Rect.Max}" : "missing");
            var theme = definition.ThemeOf("south");
            Check("its theme: みなみ, #c8564b / #953b33 (direction_themes.json as it was: North blue, East yellow, South red, West green)",
                theme != null && theme.name == "みなみ" && ColorUtility.ToHtmlStringRGB(theme.color) == "C8564B" && ColorUtility.ToHtmlStringRGB(theme.shade) == "953B33" &&
                ColorUtility.ToHtmlStringRGB(definition.ThemeOf("north").color) == "5B84B1" && ColorUtility.ToHtmlStringRGB(definition.ThemeOf("east").color) == "F2C443" &&
                ColorUtility.ToHtmlStringRGB(definition.ThemeOf("west").color) == "6E9F57");
            var problems = definition.Validate();
            Check("the world passes its checks: no region overlaps another", !OutdoorWorldDefinition.HasErrors(problems), string.Join("; ", problems));
            Check("first_field, the East, North and West areas did not move or change size",
                index.FindRegion("first_field").Rect.Min == new Vector2Int(-10, -6) && index.FindRegion("first_field").Rect.Max == new Vector2Int(10, 6) &&
                index.FindRegion(V33.EastId).Rect.Min == new Vector2Int(11, -6) && index.FindRegion(V33.EastId).Rect.Max == new Vector2Int(36, 6) &&
                index.FindRegion(V36.NorthId).Rect.Min == new Vector2Int(-10, 7) && index.FindRegion(V36.NorthId).Rect.Max == new Vector2Int(10, 26) &&
                index.FindRegion(V37.WestId).Rect.Min == new Vector2Int(-35, -6) && index.FindRegion(V37.WestId).Rect.Max == new Vector2Int(-11, 6));
            Check("the Map is the authored one (Map JSON v4 through the importer): 28 Surfaces, 80 Objects, the pinwheel's and the road's markers",
                south.Map.Surfaces.Count == 28 && south.Map.Objects.Count == 80 && south.Map.TryGetMarker(Pinwheel, out var marker) &&
                marker == new Vector2Int(7, 7) && south.Map.TryGetMarker("road_north", out var rn) && rn == new Vector2Int(12, 19) &&
                south.Map.TryGetMarker("road_south", out var rs) && rs == new Vector2Int(13, 0), $"{south.Map.Surfaces.Count} surfaces, {south.Map.Objects.Count} objects");
            var rules = MapPlacementRules.Validate(south.Map);
            Check("the game's own Map rules find nothing wrong in it (WORLD EDITOR's MapRules: 0 issues when authored)", !rules.Any(p => p.StartsWith("ERROR")), string.Join("; ", rules));
            Check("negative cells: local (12,19) is World (2,-7), the pinwheel's local (7,8) is World (-3,-18) and back",
                south.ToWorld(new Vector2Int(12, 19)) == RoadIn && south.ToWorld(new Vector2Int(7, 8)) == PinwheelCell &&
                south.ToLocal(PinwheelCell) == new Vector2Int(7, 8) && south.ToLocal(SouthEnd) == new Vector2Int(13, 0));
            // One road South: first_field's South road on over the border to the area's South edge.
            var road = new HashSet<Vector2Int> { new Vector2Int(2, -5) };
            var queue = new Queue<Vector2Int>(road);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = cell + d;
                    if (road.Contains(n) || !index.CanEnter(n) || index.SurfaceAt(n)?.StableId != "dirt" || n.y > -4 || n.x < -1 || n.x > 4) continue;
                    road.Add(n);
                    queue.Enqueue(n);
                }
            }
            Check("one continuous dirt road from first_field's South road (2,-5) over the border (2,-6)|(2,-7) to the area's South edge (3,-26)",
                road.Contains(FieldRoadEnd) && road.Contains(RoadIn) && road.Contains(new Vector2Int(2, -14)) && road.Contains(new Vector2Int(3, -14)) &&
                road.Contains(SouthEnd) && index.SurfaceAt(FieldRoadEnd)?.StableId == "dirt", $"{road.Count} road cells");
            Check("nothing blocks the border rows next to the road (y -6 and -7, x 1..3)",
                new[] { 1, 2, 3 }.All(x => index.CanEnter(new Vector2Int(x, -6)) && index.CanEnter(new Vector2Int(x, -7))));
            Check("outside every region nothing is walkable (South of the area, and beside it: West of it below the forest, East of it)",
                !index.CanEnter(new Vector2Int(0, -27)) && !index.CanEnter(new Vector2Int(-11, -7)) && !index.CanEnter(new Vector2Int(11, -7)) &&
                index.RegionAt(new Vector2Int(0, -27)) == null && index.RegionAt(new Vector2Int(-11, -10)) == null && index.RegionAt(new Vector2Int(11, -10)) == null);
            var reach = Reach(c, RoadIn, cell => index.CanEnter(cell) && InSouth(c, cell));
            var walkable = 0;
            for (var y = south.Rect.Min.y; y <= south.Rect.Max.y; y++)
            for (var x = south.Rect.Min.x; x <= south.Rect.Max.x; x++)
                if (index.CanEnter(new Vector2Int(x, y))) walkable++;
            Check("every walkable cell of the South area can be reached on foot (no closed-in corner): 373 cells", reach.Count == walkable && walkable == 373,
                $"{reach.Count} of {walkable}");
            Check("the Region border y -6|-7 lies inside one chunk row; the chunk border is y -16|-17 (floor division: -16 in chunk -1, -17 in chunk -2)",
                index.ChunkOfCell(FieldRoadEnd).Y == -1 && index.ChunkOfCell(RoadIn).Y == -1 && index.ChunkOfCell(new Vector2Int(0, -16)).Y == -1 &&
                index.ChunkOfCell(new Vector2Int(0, -17)).Y == -2 && index.ChunkOfCell(new Vector2Int(-1, -1)) == new ChunkCoord(-1, -1) &&
                index.ChunkOfCell(PinwheelCell) == new ChunkCoord(-1, -2) && index.ChunkOfCell(SouthEnd) == new ChunkCoord(0, -2));
            Check("the save menu names it by its direction: みなみ", c.Save.PlaceName(SouthId) == "みなみ", c.Save.PlaceName(SouthId));
            Check("North, East, South and West all join first_field now (four roads out of it, each into its own area)",
                index.RegionAt(new Vector2Int(0, 7))?.MapId == V36.NorthId && index.RegionAt(new Vector2Int(11, -1))?.MapId == V33.EastId &&
                index.RegionAt(RoadIn)?.MapId == SouthId && index.RegionAt(new Vector2Int(-11, 0))?.MapId == V37.WestId &&
                index.CanEnter(new Vector2Int(0, 7)) && index.CanEnter(new Vector2Int(11, -1)) && index.CanEnter(RoadIn) && index.CanEnter(new Vector2Int(-11, 0)));
        }

        // ---------------------------------------------------------------- 19.2 walking

        private static void WalkChecks()
        {
            C6.SetLegacy(null, null);
            // The 2D camera everywhere but the South area: the framing the world had without it (the ver3.7
            // world, the same game). first_field's South road end (2,-6) and its bottom rows included.
            var cells = new[] { Vector2Int.zero, FieldRoadEnd, new Vector2Int(0, -6), new Vector2Int(-9, -5), new Vector2Int(9, -6), new Vector2Int(-10, 5),
                new Vector2Int(5, 3), new Vector2Int(10, -1), new Vector2Int(20, 0), new Vector2Int(30, -3), new Vector2Int(0, 16), new Vector2Int(-8, 20),
                new Vector2Int(-16, 0), new Vector2Int(-25, 0), WestEnd, new Vector2Int(-27, 3), new Vector2Int(2, -3) };
            var storage = new HarukaMemoryStorage();
            storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(V32.Old("ver3.3", "first_field", Vector2Int.zero)));
            var o = C6.Start(storage, null, s =>
            {
                var primary = AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath);
                var full = PocSceneBuilder.LoadOutdoorWorld(primary);
                var withoutSouth = new OutdoorWorldDefinition
                {
                    worldId = full.worldId, chunkSize = full.chunkSize, themes = full.themes, formatVersion = full.formatVersion,
                    regions = full.regions.Where(r => r.mapId != SouthId).ToList()
                };
                s.UseOutdoorWorldForTest(withoutSouth, primary, false);
            });
            Check("the comparison world: first_field, the East, North and West areas (no South area)",
                o.Session.OutdoorIndex.Regions.Select(r => r.MapId).SequenceEqual(new[] { "first_field", V33.EastId, V36.NorthId, V37.WestId }));
            var before = cells.ToDictionary(cell => cell, cell => CameraFor(o, cell));
            var c = Begin(new HarukaMemoryStorage());
            var moved = cells.Where(cell => (CameraFor(c, cell) - before[cell]).magnitude > 0.005f).ToList();
            Check("2D camera in first_field (its South edge too), the North, East and West areas: exactly where it was without the South area (17 places)",
                moved.Count == 0, string.Join(" ", moved.Select(m => m + " " + CameraFor(c, m).ToString("0.00") + " was " + before[m].ToString("0.00"))));
            var startCam = CameraFor(c, Vector2Int.zero);
            Check("…and at the start (0,0) as in ver3.6 and ver3.7: (0.00, 22.98, -19.28)", (startCam - new Vector3(0f, 22.98f, -19.28f)).magnitude < 0.02f, startCam.ToString("0.00"));
            var edgeCam = CameraFor(c, FieldRoadEnd);
            var inCam = CameraFor(c, RoadIn);
            var hillCam = CameraFor(c, TurnSpot);
            var herHill = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(TurnSpot));
            var pinwheelOnScreen = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(PinwheelCell, PocWorldPresenter.PinwheelPinHeight));
            var endCam = CameraFor(c, SouthEnd);
            var herEnd = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(SouthEnd));
            Check("2D camera South of first_field: it follows her down ((2,-7) a little, the pinwheel (-3,-19) far; from there on it stands at the world's South edge)",
                inCam.z < edgeCam.z && edgeCam.z - inCam.z < 1f && hillCam.z < edgeCam.z - 2f && endCam.z <= hillCam.z + 0.001f,
                $"z at (2,-6) {edgeCam.z:0.00}, (2,-7) {inCam.z:0.00}, (-3,-19) {hillCam.z:0.00}, (3,-26) {endCam.z:0.00}");
            Check("…at the pinwheel she and the pinwheel are on screen; at the South end (3,-26) she is (the camera stops at the world's edge)",
                OnScreen(herHill) && OnScreen(pinwheelOnScreen) && OnScreen(herEnd), $"her {herHill:0}, pinwheel {pinwheelOnScreen:0}, her at the end {herEnd:0}");
            Check("2D camera width in the South area: as in first_field (its columns are first_field's)", Mathf.Abs(hillCam.x - CameraFor(c, new Vector2Int(-3, -3)).x) < 0.01f);
            // Walk South from the field over the region border.
            c.Session.PlaceForCheck(new Vector2Int(2, -5), FacingDirection.Down);
            c.Pixel.SnapToTarget();
            Settle(c, 0.3f);
            var steps = c.Session.CompletedSteps;
            var loads = c.Session.MapLoads;
            var changes = c.Session.RegionChanges;
            var lifeSteps = c.LifeLog.TotalSteps;
            var auto = c.Auto.AutoEnabled;
            C6.Step(c, Vector2Int.down, 0.4f);
            C6.Step(c, Vector2Int.down, 0.4f);
            Check("two steps South (2,-5) -> (2,-6) -> (2,-7): the South area, facing down, one region change, no map load (no fade, no scene change)",
                c.Session.Cell == RoadIn && c.Session.CurrentRegionMapId == SouthId && c.Session.Facing == FacingDirection.Down &&
                c.Session.RegionChanges == changes + 1 && c.Session.MapLoads == loads && c.Session.World == c.Session.OutdoorWorld, $"{c.Session.Cell} {c.Session.CurrentRegionMapId}");
            Check("each step counted once (session and Life Log); AUTO setting unchanged", c.Session.CompletedSteps == steps + 2 && c.LifeLog.TotalSteps == lifeSteps + 2 &&
                c.Auto.AutoEnabled == auto);
            var last = c.Pixel.WorldCamera.transform.position;
            var maxJump = 0f;
            var chunkChanges = new List<string>();
            var lastChunk = c.Session.OutdoorIndex.ChunkOfCell(c.Session.Cell);
            var notReady = 0;
            var stepsBefore = c.Session.CompletedSteps;
            Walk(c, SouthEnd, () =>
            {
                var p = c.Pixel.WorldCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
                var chunk = c.Session.OutdoorIndex.ChunkOfCell(c.Session.Cell);
                if (chunk.Y != lastChunk.Y) chunkChanges.Add(lastChunk.Y + ">" + chunk.Y);
                lastChunk = chunk;
                if (!c.Session.IsCellReady(c.Session.Cell)) notReady++;
            });
            Check("South along the road to its South end (3,-26): over the chunk border y -16|-17 once, still south_area_01, no map load, one step per cell",
                c.Session.Cell == SouthEnd && c.Session.CurrentRegionMapId == SouthId && c.Session.MapLoads == loads &&
                chunkChanges.SequenceEqual(new[] { "-1>-2" }) && c.Session.CompletedSteps - stepsBefore == 20, $"{c.Session.Cell} chunks {string.Join(" ", chunkChanges)} steps {c.Session.CompletedSteps - stepsBefore}");
            Check("the 2D camera moves smoothly all the way (no jump at the borders); the chunk under her always drawn",
                maxJump < 0.12f && notReady == 0, $"max {maxJump:0.000} unit/frame, frames not ready {notReady}");
            // 3D: the same place; back North.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.5f);
            var cam3 = c.Third.ViewCamera.transform.position;
            var her3 = GridSpace3D.ToWorld(c.Session.Cell);
            Check("3D at the South end: the same cell, the camera behind her, near", c.Session.Cell == SouthEnd &&
                new Vector2(cam3.x - her3.x, cam3.z - her3.z).magnitude < 6f, $"camera {cam3} her {her3}");
            last = c.Third.ViewCamera.transform.position;
            maxJump = 0f;
            Walk(c, new Vector2Int(2, -10), () =>
            {
                var p = c.Third.ViewCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
            });
            Check("3D North along the road over the chunk border to (2,-10): the camera follows without a jump", c.Session.Cell == new Vector2Int(2, -10) && maxJump < 0.2f, $"max {maxJump:0.000}");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.5f);
            last = c.Pixel.WorldCamera.transform.position;
            maxJump = 0f;
            var lifeBefore = c.LifeLog.TotalSteps;
            Walk(c, new Vector2Int(2, -3), () =>
            {
                var p = c.Pixel.WorldCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
            });
            Check("back North into first_field (2,-3): first_field again, no map load, the camera smooth, 7 steps for 7 cells", c.Session.CurrentRegionMapId == "first_field" &&
                c.Session.MapLoads == loads && maxJump < 0.12f && c.LifeLog.TotalSteps - lifeBefore == 7, $"{c.Session.Cell} max {maxJump:0.000} steps {c.LifeLog.TotalSteps - lifeBefore}");
            Settle(c, 1.5f);
            Check("and the 2D camera is back at first_field's framing", (c.Pixel.WorldCamera.transform.position - before[new Vector2Int(2, -3)]).magnitude < 0.05f,
                c.Pixel.WorldCamera.transform.position.ToString("0.00"));
        }

        // ---------------------------------------------------------------- 19.3 the landscape

        private static void LandscapeChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var objects = index.Objects.Where(o => o.RegionMapId == SouthId).ToList();
            var byKind = objects.GroupBy(o => o.DefinitionId).ToDictionary(g => g.Key, g => g.Count());
            Check("the South area: 22 red-brown rocks, 19 rock ledges, 11 small stones, 15 short grass, 7 reddish soil patches, 4 trees, 1 pinwheel, 1 red signpost",
                byKind.GetValueOrDefault("south_rock_red") == 22 && byKind.GetValueOrDefault("south_rock_ledge") == 19 && byKind.GetValueOrDefault("south_rock_small") == 11 &&
                byKind.GetValueOrDefault("south_grass_short") == 15 && byKind.GetValueOrDefault("south_ground_accent") == 7 && byKind.GetValueOrDefault("tree_basic") == 4 &&
                byKind.GetValueOrDefault("south_pinwheel_red") == 1 && byKind.GetValueOrDefault("south_signpost_red") == 1 && objects.Count == 80,
                string.Join(", ", byKind.Select(p => p.Key + " " + p.Value)));
            Check("Collision: rocks, ledges, trees, the pinwheel and the sign block their cell; small stones, short grass and the soil patches are walkable",
                objects.Where(o => o.DefinitionId is "south_rock_red" or "south_rock_ledge" or "tree_basic" or "south_pinwheel_red" or "south_signpost_red").All(o => !index.CanEnter(o.RootCell)) &&
                objects.Where(o => o.DefinitionId is "south_rock_small" or "south_grass_short" or "south_ground_accent").All(o => index.CanEnter(o.RootCell)));
            Check("no rock, tree or anything blocking on the road or the trail", objects.Where(o => o.Definition.BlocksMovement).All(o => index.SurfaceAt(o.RootCell)?.StableId != "dirt"));
            Check("the trees are few (4: an open sky), none South of the hill (the 3D camera looks over it from there)",
                objects.Count(o => o.DefinitionId == "tree_basic") == 4 && objects.Where(o => o.DefinitionId == "tree_basic").All(o => !(o.RootCell.x >= -7 && o.RootCell.x <= 1 && o.RootCell.y < -22)));
            var hill = HillCells().ToList();
            Check("the hill's top (風の丘): 35 cells (x -6..0, y -21..-17), open but for the pinwheel (34 walkable)",
                hill.Count == 35 && hill.Count(cell => index.CanEnter(cell)) == 34 && !index.CanEnter(PinwheelCell));
            var rim = HillRim().Distinct().ToList();
            Check("its edge: a ring of rock ledges and rocks, closed but for one way up (-1,-16) at the trail's end; its South face (y -22) all ledges and rocks",
                rim.Where(cell => cell != HillGate).All(cell => !index.CanEnter(cell)) && index.CanEnter(HillGate) &&
                Enumerable.Range(-6, 7).All(x => objects.Any(o => o.RootCell == new Vector2Int(x, -22) && o.DefinitionId == "south_rock_ledge")),
                string.Join(" ", rim.Where(cell => cell != HillGate && index.CanEnter(cell))));
            var withoutGate = Reach(c, RoadIn, cell => index.CanEnter(cell) && cell != HillGate && InSouth(c, cell));
            Check("the hill is reached by its trail only (without its gate no cell of the top can be reached)", !withoutGate.Contains(TurnSpot) && !withoutGate.Contains(HillTop));
            var trail = new[] { TrailMouth, new Vector2Int(0, -13), new Vector2Int(-1, -13), new Vector2Int(-1, -14), new Vector2Int(-1, -15), HillGate, HillTop };
            Check("the trail: dirt from the road (2,-13) West and up to the hilltop (-1,-17), past the red sign", trail.All(cell => index.SurfaceAt(cell)?.StableId == "dirt") &&
                objects.Any(o => o.DefinitionId == "south_signpost_red" && o.RootCell == new Vector2Int(0, -12)));
            Check("grass: none on the cells of rocks, ledges, the pinwheel, trees and the sign; the walkable decoration's cells as any walkable cell (the grass rule clears blocked cells only, as for ver3.7's ferns)",
                objects.Where(o => o.Definition.BlocksMovement).All(o => !index.HasGrass(o.RootCell)) &&
                objects.Where(o => !o.Definition.BlocksMovement).All(o => index.HasGrass(o.RootCell) == (index.SurfaceAt(o.RootCell)?.StableId != "dirt")) &&
                objects.Where(o => !o.Definition.BlocksMovement).All(o => !o.Definition.ExcludesGrass));
            // The red sign.
            c.Session.PlaceForCheck(SignSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("the red signpost, read from the trail: 「みなみの かぜのおか。 こみちの さきに かざぐるま。」", c.Session.InteractAhead() &&
                c.Session.LastMessage == "みなみの かぜのおか。 こみちの さきに かざぐるま。" && GameBitmapFont.HasAllGlyphs(c.Session.LastMessage), c.Session.LastMessage);
            // The reddish road.
            Settle(c, 0.3f);
            var field = RoadColour(new Vector2Int(2, -5));
            var deep = RoadColour(new Vector2Int(3, -20));
            var first = RoadColour(RoadIn);
            var west = RoadColour(new Vector2Int(-20, -1));
            Check("the road in the South area is reddish soil (redder than first_field's), a little at the border and fully from 4 rows in; the West road as first_field's",
                field.a > 0f && deep.a > 0f && deep.r / deep.g > field.r / field.g * 1.15f && first.r / first.g > field.r / field.g &&
                first.r / first.g < deep.r / deep.g && Mathf.Abs(west.r / west.g - field.r / field.g) < 0.01f,
                $"r/g field {field.r / field.g:0.00}, (2,-7) {first.r / first.g:0.00}, (3,-20) {deep.r / deep.g:0.00}, West {west.r / west.g:0.00}");
            Check("…the rule: only a region whose direction is south (first_field, East, North, West white)",
                PocEnvironmentBuilder.RoadTint(index, new Vector2Int(2, -3)) == Color.white && PocEnvironmentBuilder.RoadTint(index, new Vector2Int(20, -1)) == Color.white &&
                PocEnvironmentBuilder.RoadTint(index, new Vector2Int(0, 10)) == Color.white && PocEnvironmentBuilder.RoadTint(index, new Vector2Int(-20, -1)) == Color.white &&
                PocEnvironmentBuilder.RoadTint(index, new Vector2Int(3, -20)) == PocEnvironmentBuilder.SouthSoilTint);
            // The models: 2D and 3D draw the Blender models (no stand-in), outlined.
            var views = c.Presenter.Views.Where(v => objects.Any(o => o.InstanceId == v.InstanceId)).ToList();
            var models = new Dictionary<string, string>
            {
                ["south_pinwheel_red"] = "pinwheel_red", ["south_rock_red"] = "rock_red", ["south_rock_ledge"] = "rock_ledge", ["south_rock_small"] = "rock_small",
                ["south_grass_short"] = "grass_short", ["south_ground_accent"] = "ground_accent", ["south_signpost_red"] = "sign_red", ["tree_basic"] = "tree_basic"
            };
            Check("every Object of the area is drawn with its Blender model (V38; trees the field's), none a stand-in, none twice",
                views.Count == 80 && views.All(v => !v.IsFallback && !v.Placeholder && models.TryGetValue(v.DefinitionId, out var m) && v.ModelName == m) &&
                V32.DuplicateViews(c).Count == 0, $"{views.Count} views; " + string.Join(",", views.Where(v => v.IsFallback || v.Placeholder).Select(v => v.DefinitionId)));
            Check("each has its own outline copy (the 2D view's outline ids), the soil patches a soft outline, the rocks a full one",
                views.All(v => v.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh != null && f.sharedMesh.name.EndsWith(PocOutlineTag.CopySuffix, StringComparison.Ordinal))) &&
                AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath).Find("south_ground_accent").outlineStrength == 0.5f &&
                AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath).Find("south_rock_red").outlineStrength == 1f);
            Check("the theme's red is in the pinwheel and the sign (their models' vertex colours include #c8564b)",
                new[] { "pinwheel_red", "sign_red" }.All(name => AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.V38Models + name + ".fbx").GetComponentsInChildren<MeshFilter>(true)
                    .SelectMany(f => f.sharedMesh.colors32).Any(k => Mathf.Abs(k.r - 0xc8) <= 3 && Mathf.Abs(k.g - 0x56) <= 3 && Mathf.Abs(k.b - 0x4b) <= 3)));
            // 3D: at the pinwheel, nothing of it hidden for the camera.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Session.PlaceForCheck(TurnSpot, FacingDirection.Up);
            c.Third.SnapToTarget();
            Settle(c, 0.6f);
            var pinwheelView = views.First(v => v.DefinitionId == "south_pinwheel_red");
            Check("3D at the pinwheel: it is drawn whole (never hidden or faded for the camera), the hill's ledges in front of the camera too",
                !pinwheelView.CameraOccluder && pinwheelView.Renderers.All(r => r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) &&
                views.Where(v => v.DefinitionId == "south_rock_ledge").All(v => v.Renderers.All(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)));
            // Night and morning: the same world clock lights the South area.
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 4f);
            Check("at night the South area is in the night (the shared clock and lights; nothing of its own)", c.Time.NightBlend > 0.99f && c.Session.CurrentRegionMapId == SouthId);
            c.Time.Override = PocPhaseOverride.Morning;
            Settle(c, 4f);
            Check("in the morning it is day again", c.Time.NightBlend < 0.01f);
            c.Time.Override = PocPhaseOverride.Auto;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.3f);
            // AUTO in the South area: it walks, never stuck among the rocks, never out of the area, never finds the pinwheel.
            c.Session.PlaceForCheck(new Vector2Int(3, -18), FacingDirection.Down);
            c.Hud.ClearMessage();
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var regions = new HashSet<string>();
            var visited = new HashSet<Vector2Int>();
            var autoSteps = c.Gameplay.AutoWorld.AutoSteps;
            var spins = c.Presenter.PinwheelSpinsStarted;
            crowBlockedFrames = 0;
            for (var t = 0f; t < 200f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId); visited.Add(c.Session.Cell); }
            Check("AUTO in the South area for 200 s: it walks (many cells), never out of the South area, never into a rock",
                c.Gameplay.AutoWorld.AutoSteps - autoSteps > 20 && visited.Count > 10 && regions.SetEquals(new[] { SouthId }) && visited.All(v => index.CanEnter(v)),
                $"AUTO steps {c.Gameplay.AutoWorld.AutoSteps - autoSteps}, cells {visited.Count}, regions {string.Join(",", regions)}");
            Check("…AUTO never unlocks the pinwheel's そらをとぶ place nor turns the pinwheel by itself", !T(c).IsFound(Pinwheel) && c.Presenter.PinwheelSpinsStarted == spins,
                visited.Any(v => Mathf.Abs(v.x - TurnSpot.x) + Mathf.Abs(v.y - TurnSpot.y) <= 2) ? "AUTO came within 2" : "AUTO did not come near");
            c.Auto.SetAutoEnabled(false, C6.Clock);
        }

        // ---------------------------------------------------------------- 19.4 the pinwheel

        private static void PinwheelChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var pinwheel = PinwheelObject(c);
            var point = pinwheel.Definition.ActionPoints.FirstOrDefault();
            Check("the pinwheel at (-3,-18) (local (7,8)); its Action Point turn_pinwheel one cell South (-3,-19), facing up, on open ground",
                pinwheel.RootCell == PinwheelCell && point != null && point.Id == "turn_pinwheel" && pinwheel.RootCell + point.PlayerCellOffset == TurnSpot &&
                point.PlayerFacing.ToString() == "Up" && c.Session.OutdoorIndex.CanEnter(TurnSpot), point?.Id);
            var view = c.Presenter.FindView(pinwheel.InstanceId);
            var blades = c.Presenter.PinwheelBladesOf(pinwheel.InstanceId);
            var pole = view != null ? view.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.transform != blades) : null;
            Check("its model: the pole and the blades are separate (the blades a child, pinwheel_blades, with its own renderer), registered for turning",
                view != null && blades != null && blades.name == PocWorldPresenter.PinwheelBladesName && blades.GetComponent<MeshRenderer>() != null && pole != null &&
                c.Presenter.PinwheelBladesLoaded == 1, view != null ? string.Join(",", view.GetComponentsInChildren<Transform>(true).Select(t => t.name)) : "no view");
            var bladeMesh = blades.GetComponent<MeshFilter>().sharedMesh;
            Check("the blades turn about their own pin: the blades' mesh is centred on it, the pin 1.2 above the ground (over her head) just South of the pole",
                bladeMesh.bounds.center.magnitude < 0.04f && Mathf.Abs(blades.position.y - PocWorldPresenter.PinwheelPinHeight) < 0.03f &&
                blades.position.z < view.transform.position.z && Mathf.Abs(blades.position.x - view.transform.position.x) < 0.01f,
                $"centre {bladeMesh.bounds.center:0.000}, pin {blades.position - view.transform.position:0.000}");
            var rest = blades.rotation;
            var poleRotation = pole.transform.rotation;
            var bladePosition = blades.position;
            c.Session.PlaceForCheck(TurnSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Settle(c, 0.3f);
            var spins = c.Presenter.PinwheelSpinsStarted;
            var turned = c.Session.InteractAhead();
            Check("A at the pinwheel (かざぐるまをまわす): 「かざぐるまが くるくる まわった！」", turned && c.Session.LastMessage == Message && C6.Shown(c) == Message &&
                GameBitmapFont.HasAllGlyphs(Message), c.Session.LastMessage);
            Check("the blades start to turn (one spin)", c.Presenter.PinwheelSpinsStarted == spins + 1 && c.Presenter.PinwheelSpinning);
            Check("she can move at once (nothing waits for the pinwheel)", !c.Session.InputSuspended && !c.Menu.BlocksGameplayInput);
            var angles = new List<float>();
            for (var i = 0; i < 12; i++)
            {
                Settle(c, 0.2f);
                angles.Add(c.Presenter.PinwheelAngle(pinwheel.InstanceId));
            }
            var gains = angles.Zip(angles.Skip(1), (a, b) => b - a).ToList();
            Check("they spin fast at first (over 120 degrees in the first 0.2 s after the start-up) and then slower and slower",
                angles[0] > 100f && gains.Take(9).Zip(gains.Skip(1).Take(9), (a, b) => b <= a + 0.01f).All(x => x) && gains[0] > 4f * gains[8],
                string.Join(" ", angles.Select(a => a.ToString("0"))));
            Check("only the blades turn: about their own axis (they stay where they are), the pole never moves",
                Vector3.Distance(blades.position, bladePosition) < 0.001f && Quaternion.Angle(pole.transform.rotation, poleRotation) < 0.01f);
            Settle(c, 0.4f);
            var finalAngle = c.Presenter.PinwheelAngle(pinwheel.InstanceId);
            Check($"after about {PocWorldPresenter.PinwheelSeconds} s they stand still again, about two turns on ({PocWorldPresenter.PinwheelFullTurn:0} degrees), where they stopped (not back to the start)",
                !c.Presenter.PinwheelSpinning && Mathf.Abs(finalAngle - PocWorldPresenter.PinwheelFullTurn) < 0.5f &&
                Quaternion.Angle(blades.rotation, Quaternion.AngleAxis(finalAngle, -GridSpace3D.Direction(Vector2Int.down)) * rest) < 0.5f &&
                Quaternion.Angle(blades.rotation, rest) > 1f, $"{finalAngle:0.0} degrees");
            // Examined again while it turns: nothing new.
            c.Hud.ClearMessage();
            c.Session.InteractAhead();
            Settle(c, 0.5f);
            var retriggers = c.Presenter.PinwheelRetriggersIgnored;
            c.Hud.ClearMessage();
            var again = c.Session.InteractAhead();
            Check("examined again while the blades turn: the message again, but no second spin on top (ignored)",
                again && C6.Shown(c) == Message && c.Presenter.PinwheelSpinsStarted == spins + 2 && c.Presenter.PinwheelRetriggersIgnored == retriggers + 1);
            Settle(c, PocWorldPresenter.PinwheelSeconds);
            var before = AllTransforms();
            var meshes = RuntimeMeshes();
            for (var i = 0; i < 6; i++)
            {
                c.Hud.ClearMessage();
                c.Session.InteractAhead();
                Settle(c, PocWorldPresenter.PinwheelSeconds + 0.1f);
            }
            Check("six more spins, each after the last stopped: six more spins, nothing made for them (no GameObject, no mesh added)",
                c.Presenter.PinwheelSpinsStarted == spins + 8 && AllTransforms() == before && RuntimeMeshes() == meshes && !c.Presenter.PinwheelSpinning,
                $"objects {before} -> {AllTransforms()}, meshes {meshes} -> {RuntimeMeshes()}");
            C6.Step(c, Vector2Int.right, 0.4f);
            Check("and she walks on (East on the hill)", c.Session.Cell == TurnSpot + Vector2Int.right);
            c.Session.PlaceForCheck(TurnSpot + Vector2Int.right, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var s2 = c.Presenter.PinwheelSpinsStarted;
            Check("from beside the Action Point (another cell) A finds nothing at the pinwheel", !c.Session.InteractAhead() && c.Presenter.PinwheelSpinsStarted == s2);
            c.Session.PlaceForCheck(TurnSpot + Vector2Int.down, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("nor from one cell further back", !c.Session.InteractAhead() && c.Presenter.PinwheelSpinsStarted == s2);
            c.Session.PlaceForCheck(TurnSpot, FacingDirection.Left);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("nor on the spot facing away from it", !c.Session.InteractAhead() && c.Presenter.PinwheelSpinsStarted == s2);
            // A click (PC) on the pinwheel: the same rules (from its Action Point, facing it).
            c.Session.PlaceForCheck(TurnSpot, FacingDirection.Up);
            c.Pixel.SnapToTarget();
            Settle(c, 0.3f);
            c.Hud.ClearMessage();
            var screen = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(PinwheelCell, PocWorldPresenter.PinwheelPinHeight));
            var picked = c.Switcher.PickObject(screen);
            c.Input.Click(screen);
            Check("a click on its blades (PC, 2D): the pinwheel is picked and turns, the message shown", picked == pinwheel && c.Presenter.PinwheelSpinsStarted == s2 + 1 &&
                C6.Shown(c) == Message, picked != null ? picked.DefinitionId : "nothing picked at " + screen);
            Settle(c, PocWorldPresenter.PinwheelSeconds + 0.1f);
            // The phone's A button is the same InteractAhead (GameInput's A): checked above.
            // 3D and night.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            c.Hud.ClearMessage();
            Settle(c, 0.5f);
            var screen3 = c.Third.ViewCamera.WorldToScreenPoint(GridSpace3D.ToWorld(PinwheelCell, PocWorldPresenter.PinwheelPinHeight));
            Check("in 3D the same: A turns the same blades (one pinwheel, shared by both views), the message", c.Session.InteractAhead() &&
                c.Presenter.PinwheelSpinsStarted == s2 + 2 && c.Session.LastMessage == Message && c.Presenter.PinwheelBladesLoaded == 1 &&
                c.Presenter.PinwheelBladesOf(pinwheel.InstanceId) == blades);
            Settle(c, 1f);
            Check("…and a click on it in 3D picks it", c.Switcher.PickObject(screen3) == pinwheel, screen3.ToString("0"));
            Settle(c, 2f);
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 4f);
            c.Hud.ClearMessage();
            Check("at night the same (the blades lit as the night lights the hill)", c.Session.InteractAhead() && c.Presenter.PinwheelSpinsStarted == s2 + 3);
            c.Time.Override = PocPhaseOverride.Auto;
            Settle(c, 2.5f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.3f);
            var turnedBy = c.Presenter.PinwheelAngle(pinwheel.InstanceId) % 360f;
            Check("2D / 3D switches beside it: she stays on the Action Point, one pinwheel, its blades where they stopped",
                c.Session.Cell == TurnSpot && c.Presenter.PinwheelBladesLoaded == 1 &&
                Mathf.Abs(Mathf.Min(turnedBy, 360f - turnedBy) - Quaternion.Angle(rest, blades.rotation)) < 1f, $"{turnedBy:0} degrees");
            // The other examine spots keep their own effects.
            var leaves = c.Presenter.LeafBurstsStarted;
            var ripples = c.Presenter.RipplesStarted;
            var s3 = c.Presenter.PinwheelSpinsStarted;
            c.Session.PlaceForCheck(StumpSpot, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var stump = c.Session.InteractAhead();
            c.Session.PlaceForCheck(new Vector2Int(-4, 15), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var pond = c.Session.InteractAhead();
            Check("the West stump still sends its leaves and the North pond still ripples (and neither turns the pinwheel)",
                stump && pond && c.Presenter.LeafBurstsStarted == leaves + 1 && c.Presenter.RipplesStarted == ripples + 1 && c.Presenter.PinwheelSpinsStarted == s3);
        }

        // ---------------------------------------------------------------- 19.5 the crow

        private static void CrowChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var crow = c.Crow;
            maxCrows = 0;
            crowBlockedFrames = 0;
            Check("ついてきて", StartFollowing(c));
            Walk(c, new Vector2Int(2, -4));
            Settle(c, 2f);
            var regions = new HashSet<string>();
            var crowChunks = new HashSet<int>();
            Walk(c, new Vector2Int(3, -20), () =>
            {
                if (!crow.isActiveAndEnabled) return;
                var cell = GridSpace3D.ToCell(crow.transform.position);
                regions.Add(c.Session.OutdoorIndex.RegionAt(cell)?.MapId ?? "-");
                crowChunks.Add(c.Session.OutdoorIndex.ChunkOfCell(cell).Y);
            });
            Settle(c, 3f);
            for (var t = 0f; t < 12f && (crow.Airborne || crow.DistanceToPlayer > 3); t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            crowChunks.Add(c.Session.OutdoorIndex.ChunkOfCell(crow.Cell).Y);   // (where it came down after the walk)
            Check("the crow follows her South over the region border y -6|-7 and the chunk border y -16|-17 (one crow, still following, near her)",
                crow.Following && regions.Contains(SouthId) && crowChunks.Contains(-2) && crow.DistanceToPlayer <= 3 && Crows() == 1 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == SouthId, $"{Where(c)} regions {string.Join(",", regions)} chunk rows {string.Join(",", crowChunks)}");
            // Up the trail onto the hill.
            Walk(c, TurnSpot);
            C6.Step(c, Vector2Int.up, 0.3f);   // (into the pinwheel: refused, she faces it)
            Settle(c, 3f);
            Check("up the trail onto the hill: the crow with her, on open ground of the hilltop or by the trail", crow.Following && crow.DistanceToPlayer <= 3 &&
                c.Session.OutdoorWorld.CanEnter(crow.Cell) && InSouth(c, crow.Cell), Where(c));
            Check("the pinwheel can still be turned with the crow beside her (it never stands on her Action Point when she is on it)",
                crow.Cell != TurnSpot && c.Session.InteractAhead() && c.Session.LastMessage == Message);
            Settle(c, 20f);
            Check("waiting by the pinwheel: the crow keeps near her, never inside a rock, a ledge or the pinwheel",
                crow.Following && crow.DistanceToPlayer <= 4 && crowBlockedFrames == 0 && (crow.Airborne || c.Session.OutdoorWorld.CanEnter(crow.Cell)), Where(c));
            var landingBlocked = 0;
            var onHill = 0;
            var choose = typeof(PocCrow).GetMethod("TryChooseGroundLanding", Hidden);
            for (var i = 0; i < 60; i++)
            {
                var args = new object[] { default(Vector2Int) };
                if (!(bool)choose.Invoke(crow, args)) continue;
                var cell = (Vector2Int)args[0];
                if (!c.Session.OutdoorWorld.CanEnter(cell)) landingBlocked++;
                if (HillCells().Contains(cell)) onHill++;
            }
            Check("60 landing choices on the hill: all on walkable ground (never a rock, a ledge or the pinwheel's cell)", landingBlocked == 0, $"{onHill} on the hilltop");
            // A walk among the rocks: down off the hill, West below it, East over the rocks, back.
            Walk(c, new Vector2Int(-10, -24));
            Walk(c, new Vector2Int(8, -17));
            Walk(c, SouthEnd);
            Walk(c, new Vector2Int(2, -12));
            Settle(c, 3f);
            Check("a walk among the rocks (below the hill's South face, the rocks East of the road, the South end): it kept up on land or flew, never stood in a rock",
                crowBlockedFrames == 0 && crow.DistanceToPlayer <= 4 && crow.Following, $"{Where(c)} blocked frames {crowBlockedFrames}");
            Walk(c, new Vector2Int(2, -3));
            Settle(c, 4f);
            Check("back North in first_field: the crow with her", crow.Following && crow.DistanceToPlayer <= 3 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", Where(c));
            // あそんでおいで in the South area: free again, it flies back to its own trees.
            Walk(c, new Vector2Int(3, -18));
            Settle(c, 3f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            var home = false;
            for (var t = 0f; t < 40f && !home; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (!crow.Airborne && c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field" ||
                    crow.Activity == CrowActivity.Perched && c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(crow.transform.position))?.MapId == "first_field") home = true;
            }
            Check("あそんでおいで in the South area: free again, it flies back to first_field (its own trees)", !crow.Following && home, Where(c));
            Check("one crow the whole time, never inside a blocked cell", maxCrows == 1 && crowBlockedFrames == 0, $"{maxCrows} crows, {crowBlockedFrames} blocked frames");
            // Into the house from the South area: the crow waits outside, as before.
            Check("ついてきて again", StartFollowing(c));
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Walk(c, door + Vector2Int.down);
            V32.Step(c, Vector2Int.up, 0.4f);
            Settle(c, 1f);
            Check("into the house from the South: the crow does not come in (one crow, outside)", c.Session.World.MapId == "halka_house" && Crows() == 1 &&
                (!crow.isActiveAndEnabled || c.Session.OutdoorIndex.RegionAt(crow.Cell) != null), Where(c));
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Settle(c, 3f);
            for (var t = 0f; t < 12f && (crow.Airborne || crow.DistanceToPlayer > 4); t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("and out again: following, near her", c.Session.World == c.Session.OutdoorWorld && crow.Following && crow.DistanceToPlayer <= 4, Where(c));
        }

        // ---------------------------------------------------------------- 19.6 そらをとぶ

        private static void TravelChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var point = t.Registry.Find(Pinwheel);
            var south = South(c);
            Check("the registry has five places (home, East, North, West: the four of ver3.7 unchanged); the new one: landmark_south_red_pinwheel 「みなみのかざぐるま」",
                t.Registry.points.Select(p => p.id).SequenceEqual(new[] { Home, Plaza, Pond, Clearing, Pinwheel }) && point != null && point.name == "みなみのかざぐるま" &&
                point.mapId == SouthId && point.worldId == "outdoor_main" && !point.unlockedAtStart && point.order == 40 && GameBitmapFont.HasAllGlyphs(point.name) &&
                t.Registry.Find(Pond).arrivalCell == new Vector2Int(7, 8) && t.Registry.Find(Plaza).arrivalCell == new Vector2Int(16, 4) &&
                t.Registry.Find(Clearing).arrivalCell == new Vector2Int(8, 8), string.Join(",", t.Registry.points.Select(p => p.id)));
            Check("at the start: home found, the South place not (nor East, North or West)", t.IsFound(Home) && !t.IsFound(Pinwheel) && !t.IsFound(Plaza) && !t.IsFound(Pond) && !t.IsFound(Clearing));
            Check("its places: discovered within 2 of (-3,-19) (the pinwheel's spot, 6 cells from the road), arriving at (-3,-20) facing the pinwheel",
                south.ToWorld(point.discoverCell) == TurnSpot && point.discoverRadius == 2 && south.ToWorld(point.arrivalCell) == HillArrival &&
                point.ArrivalFacing == FacingDirection.Up && point.landingSearchRadius == 4 && c.Session.OutdoorIndex.CanEnter(HillArrival));
            var problems = t.Registry.Validate(c.Session.OutdoorIndex, cell => c.Session.IsTransitionCell(c.Session.OutdoorWorld, cell));
            Check("the five places pass the registry's checks", problems.Count == 0, string.Join("; ", problems));
            Check("not found yet: it cannot be flown to", t.TryBegin(Pinwheel, C6.Clock, FastTravelSource.Check) == FastTravelStart.NotDiscovered);
            Walk(c, SouthEnd);
            Walk(c, new Vector2Int(2, -8));
            Check("walking the whole South road there and back (past the hill, 6 cells from it): not found", !t.IsFound(Pinwheel), c.Session.Cell.ToString());
            Walk(c, TrailMouth);
            Walk(c, HillGate);
            Check("nor on the trail up to the hill's way up (-1,-16)", !t.IsFound(Pinwheel));
            var foundAt = new Vector2Int(int.MinValue, 0);
            var message = "";
            t.Discovered += p => { if (p.id == Pinwheel) { foundAt = c.Session.Cell; message = C6.Shown(c); } };
            Walk(c, TurnSpot);
            Check("onto the hill to the pinwheel: found on the first cell within 2, once, 「そらをとべるばしょを みつけた！」",
                t.IsFound(Pinwheel) && Mathf.Abs(foundAt.x - TurnSpot.x) + Mathf.Abs(foundAt.y - TurnSpot.y) == 2 && message == PocFastTravel.MessageFound && HillCells().Contains(foundAt),
                $"found at {foundAt}: {message}");
            Walk(c, TrailMouth);
            c.Hud.ClearMessage();
            Walk(c, TurnSpot);
            Check("walking back onto the hill: no second message", t.Discoveries == 1 && C6.Shown(c) != PocFastTravel.MessageFound);
            // The list of five: two pages.
            t.SetFound(new[] { Plaza, Pond, Clearing, Pinwheel });
            c.Hud.ClearMessage();
            c.Menu.SetOpen(true, C6.Clock);
            c.Menu.ActivateLink(c.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
            Check("the list, page 1: ハルカチャンの家 / ひがしのはなのひろば / きたのみずべ / にしのこもれび / つぎへ / やめる",
                Buttons(c) == "ハルカチャンの家/ひがしのはなのひろば/きたのみずべ/にしのこもれび/つぎへ/やめる" && t.Page.PageIndex == 0, Buttons(c));
            var rects = ButtonRects(c);
            Check("all six buttons of page 1 inside the window (1280 x 720), none overlapping", Inside(rects, 6), string.Join(" ", rects.Select(r => r.ToString("0"))));
            Press(c, PocFastTravel.NextLabel);
            Check("つぎへ: page 2: みなみのかざぐるま / まえへ / やめる", Buttons(c) == "みなみのかざぐるま/まえへ/やめる" && t.Page.PageIndex == 1 && c.Menu.Page == t.Page, Buttons(c));
            rects = ButtonRects(c);
            Check("all three buttons of page 2 inside the window, none overlapping", Inside(rects, 3), string.Join(" ", rects.Select(r => r.ToString("0"))));
            Press(c, PocFastTravel.PreviousLabel);
            Check("まえへ: back to page 1, as it was", Buttons(c) == "ハルカチャンの家/ひがしのはなのひろば/きたのみずべ/にしのこもれび/つぎへ/やめる" && t.Page.PageIndex == 0, Buttons(c));
            Press(c, PocFastTravel.NextLabel);
            Press(c, PocFastTravel.CancelLabel);
            Settle(c, 0.2f);
            Check("やめる on page 2 closes it, she stays (no flight)", c.Menu.Page == null && c.Session.Cell == TurnSpot && !t.Busy);
            c.Menu.SetOpen(false, C6.Clock);
            Check("following for the flights", StartFollowing(c) || c.Crow.Following);
            var anchor = c.Clock.AnchorMilliseconds;
            void Leg(string label, string id, Vector2Int arrival, string region, PocViewMode view, bool byMenu = false)
            {
                c.Switcher.SetMode(view);
                Settle(c, 0.3f);
                var steps = c.Session.CompletedSteps;
                var spins = c.Presenter.PinwheelSpinsStarted;
                FastTravelStart started;
                if (byMenu)
                {
                    // Through the menu as she would: the list, つぎへ when the place is on page 2, its button.
                    c.Menu.SetOpen(true, C6.Clock);
                    c.Menu.ActivateLink(c.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
                    var name = t.Registry.Find(id).name;
                    if (!c.Menu.Page.Buttons.Contains(name)) Press(c, PocFastTravel.NextLabel);
                    started = Press(c, name) && t.Busy ? FastTravelStart.Started : FastTravelStart.NotDiscovered;
                }
                else started = t.TryBegin(id, C6.Clock, FastTravelSource.Check);
                Fly(c);
                Settle(c, 0.5f);
                Check(label, started == FastTravelStart.Started && c.Session.Cell == arrival && c.Session.CurrentRegionMapId == region &&
                    t.LastArrivalVerified && c.Session.CompletedSteps == steps && c.Crow.Following && c.Crow.DistanceToPlayer <= 3 &&
                    c.Session.OutdoorWorld.CanEnter(c.Crow.Cell) && c.Crow.Cell != c.Session.Cell && c.Switcher.Mode == view &&
                    c.Presenter.PinwheelSpinsStarted == spins && c.Session.IsCellReady(c.Session.Cell) && Crows() == 1,
                    $"{started} {Where(c)} {t.LastArrivalProblem}");
            }
            c.Session.PlaceForCheck(HomeArrival + Vector2Int.right * 2, FacingDirection.Down);
            Settle(c, 1f);
            Leg("home -> South by the menu (page 2, 2D): (-3,-20) on the hill, facing the pinwheel, no step, the crow beside her on open ground, no spin by itself",
                Pinwheel, HillArrival, SouthId, PocViewMode.PixelView2D, true);
            Leg("South -> home by the menu (2D)", Home, HomeArrival, "first_field", PocViewMode.PixelView2D, true);
            Leg("home -> South (3D)", Pinwheel, HillArrival, SouthId, PocViewMode.ThirdPerson3D);
            Leg("South -> East (3D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.ThirdPerson3D);
            Leg("East -> South (2D)", Pinwheel, HillArrival, SouthId, PocViewMode.PixelView2D);
            Leg("South -> North (2D)", Pond, PondArrival, V36.NorthId, PocViewMode.PixelView2D);
            Leg("North -> South (3D)", Pinwheel, HillArrival, SouthId, PocViewMode.ThirdPerson3D);
            Leg("South -> West (3D)", Clearing, ClearingArrival, V37.WestId, PocViewMode.ThirdPerson3D);
            Leg("West -> South by the menu (2D)", Pinwheel, HillArrival, SouthId, PocViewMode.PixelView2D, true);
            Leg("South -> home (3D)", Home, HomeArrival, "first_field", PocViewMode.ThirdPerson3D);
            // The round of five: home, East, North, West, South, home (and back the other way).
            Leg("round: home -> East (2D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.PixelView2D, true);
            Leg("round: East -> North (2D)", Pond, PondArrival, V36.NorthId, PocViewMode.PixelView2D, true);
            Leg("round: North -> West (3D)", Clearing, ClearingArrival, V37.WestId, PocViewMode.ThirdPerson3D, true);
            Leg("round: West -> South (3D)", Pinwheel, HillArrival, SouthId, PocViewMode.ThirdPerson3D, true);
            Leg("round: South -> home (2D)", Home, HomeArrival, "first_field", PocViewMode.PixelView2D, true);
            Leg("round back: home -> South (2D)", Pinwheel, HillArrival, SouthId, PocViewMode.PixelView2D);
            Leg("round back: South -> West (2D)", Clearing, ClearingArrival, V37.WestId, PocViewMode.PixelView2D);
            Leg("round back: West -> North (3D)", Pond, PondArrival, V36.NorthId, PocViewMode.ThirdPerson3D);
            Leg("round back: North -> East (3D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.ThirdPerson3D);
            Leg("round back: East -> home (2D)", Home, HomeArrival, "first_field", PocViewMode.PixelView2D);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("the day/night clock untouched by the flights", c.Clock.AnchorMilliseconds == anchor);
            Check("a place whose arrival is the pinwheel lands on open ground beside it",
                t.TryFindLanding(new FastTravelPoint { id = "check_pinwheel", mapId = SouthId, arrivalCell = south.ToLocal(PinwheelCell), landingSearchRadius = 4 }, south, PinwheelCell, out var land) &&
                c.Session.OutdoorIndex.CanEnter(land) && land != PinwheelCell, land.ToString());
            Check("a place whose arrival is a ledge lands on open ground too", t.TryFindLanding(new FastTravelPoint { id = "check_ledge", mapId = SouthId,
                arrivalCell = south.ToLocal(new Vector2Int(-3, -22)), landingSearchRadius = 4 }, south, new Vector2Int(-3, -22), out var land2) &&
                c.Session.OutdoorIndex.CanEnter(land2), land2.ToString());
            // Fewer places found: the list as before (one page).
            var f = Begin(new HarukaMemoryStorage());
            T(f).SetFound(new[] { Plaza, Pond, Clearing });
            f.Menu.SetOpen(true, C6.Clock);
            f.Menu.ActivateLink(f.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
            Check("with four places found (South not yet): the ver3.7 list, one page, no つぎへ", Buttons(f) == "ハルカチャンの家/ひがしのはなのひろば/きたのみずべ/にしのこもれび/やめる", Buttons(f));
            f.Menu.SetOpen(false, C6.Clock);
            T(f).SetFound(new[] { Pinwheel });
            f.Menu.SetOpen(true, C6.Clock);
            f.Menu.ActivateLink(f.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
            Check("with home and the South only: one page, in order", Buttons(f) == "ハルカチャンの家/みなみのかざぐるま/やめる", Buttons(f));
            f.Menu.SetOpen(false, C6.Clock);
            // AUTO does not find it (a new game).
            var a = Begin(new HarukaMemoryStorage());
            a.Session.PlaceForCheck(HillTop, FacingDirection.Down);
            Frame(a, 1f / 60f, Vector2Int.zero);
            var s1 = a.Gameplay.AutoWorld.TryStep(Vector2Int.down);
            Settle(a, 0.4f);
            var s2 = a.Gameplay.AutoWorld.TryStep(Vector2Int.left);
            Settle(a, 0.4f);
            Check("AUTO steps on the hill to (-2,-18) (2 from the pinwheel's spot): nothing found", s1 && s2 && a.Session.Cell == new Vector2Int(-2, -18) &&
                !T(a).IsFound(Pinwheel), a.Session.Cell.ToString());
        }

        // ---------------------------------------------------------------- 19.7 the save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            // Saved by the pinwheel, following, all five found; closed and opened.
            {
                var storage = new HarukaMemoryStorage();
                var c = Begin(storage);
                T(c).SetFound(new[] { Plaza, Pond, Clearing });
                StartFollowing(c);
                Walk(c, TurnSpot);
                C6.Step(c, Vector2Int.up, 0.3f);   // (into the pinwheel: refused, she faces it)
                Settle(c, 2f);
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                Settle(c, 1.5f);
                var steps = c.LifeLog.TotalSteps;
                var play = c.LifeLog.TotalPlaySeconds;
                c.Save.OnPageHidden("");
                var latest = C6.Latest(c);
                Check("saved by the pinwheel: south_area_01 local (7,7), facing up, 3D, following, places east + north + west + home + south",
                    latest.MapId == SouthId && latest.CellX == 7 && latest.CellY == 7 && latest.Facing == "up" && latest.ViewMode == HarukaSaveData.View3D &&
                    latest.CrowMode == HarukaSaveData.CrowFollow && latest.FastTravel != null &&
                    latest.FastTravel.OrderBy(x => x).SequenceEqual(new[] { Plaza, Pond, Clearing, Home, Pinwheel }.OrderBy(x => x)), HarukaSaveSystem.Describe(latest));
                Check("the schema is still 1; steps and play time are there", latest.Schema == 1 && latest.TotalSteps == steps && latest.TotalSteps > 0 &&
                    Mathf.Abs((float)(latest.TotalPlaySeconds - play)) < 2f, latest.Schema + " " + latest.TotalSteps);
                var phase = c.Clock.CurrentPhase;
                var r = Begin(storage);
                Check("opened again: the same cell, facing, 3D, the crow following near her, all five places, the chunks drawn first",
                    r.Session.Cell == TurnSpot && r.Session.CurrentRegionMapId == SouthId && r.Session.Facing == FacingDirection.Up &&
                    r.Switcher.Mode == PocViewMode.ThirdPerson3D && r.Crow.Following && r.Crow.DistanceToPlayer <= 6 &&
                    r.Session.OutdoorWorld.CanEnter(r.Crow.Cell) && new[] { Plaza, Pond, Clearing, Pinwheel }.All(id => T(r).IsFound(id)) &&
                    r.Session.IsCellReady(TurnSpot) && r.Session.IsCellReady(PinwheelCell), Where(r));
                Check("…steps kept, the time of day as it was (no sudden change)", r.LifeLog.TotalSteps == steps && r.Clock.CurrentPhase == phase,
                    $"{r.LifeLog.TotalSteps} {r.Clock.CurrentPhase} was {phase}");
                Check("and A turns the pinwheel there at once", r.Session.InteractAhead() && r.Session.LastMessage == Message);
                Check("and she can fly home, East, North and West from there", T(r).TryBegin(Home, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == HomeArrival && T(r).TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == PlazaArrival && T(r).TryBegin(Pond, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == PondArrival && T(r).TryBegin(Clearing, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == ClearingArrival);
                var exported = C6.Sealed(latest);
                var t2 = Begin(new HarukaMemoryStorage());
                Settle(t2, 1.2f);
                var check = t2.Save.CheckImport(exported, out var data);
                var ok = t2.Save.ApplyImport(data, C6.Clock, out var problem);
                Settle(t2, 0.5f);
                Check("export / import of the South save: checked, applied by the pinwheel with its places and the companion",
                    check == HarukaSaveSystem.ImportCheck.Ok && ok && t2.Session.Cell == TurnSpot && T(t2).IsFound(Pinwheel) && T(t2).IsFound(Clearing) && t2.Crow.Following, problem);
                var unknown = latest.Copy();
                unknown.MapId = "south_area_99";
                Check("a save naming a South area that does not exist is refused (UnknownMap)", t2.Save.CheckImport(C6.Sealed(unknown), out _) == HarukaSaveSystem.ImportCheck.UnknownMap);
                var outside = latest.Copy();
                outside.CellY = 25;
                Check("a cell outside the South Map is refused (OutOfMap)", t2.Save.CheckImport(C6.Sealed(outside), out _) == HarukaSaveSystem.ImportCheck.OutOfMap);
                var tampered = C6.Sealed(latest).Replace("\"" + SouthId + "\"", "\"south_area_0X\"");
                Check("a changed file whose checksum no longer matches is refused", t2.Save.CheckImport(tampered, out _) != HarukaSaveSystem.ImportCheck.Ok);
                Check("the save's extensions: the crow companion and そらをとぶ side by side (neither overwrites the other)",
                    exported.Contains("crowCompanion") && exported.Contains("fastTravel") && exported.Contains(Pinwheel));
            }
            // A save on the pinwheel's cell (damaged by hand): resumed on the nearest walkable cell of the South area.
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", SouthId, new Vector2Int(7, 8));
                data.GameVersion = "ver3.8";
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("a save on the pinwheel's cell: she resumes on the nearest walkable cell of the South area",
                    g.Session.CurrentRegionMapId == SouthId && g.Session.OutdoorWorld.CanEnter(g.Session.Cell) && g.Save.LocationNote.Contains("nearest"),
                    $"{g.Session.Cell} {g.Save.LocationNote}");
            }
            // Old saves: every version; the South place not found (normal), the older places kept.
            var places = new (string version, string map, Vector2Int cell, string[] found)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0), null), ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1), null),
                ("ver3.2", "halka_house", new Vector2Int(-2, 0), null), ("ver3.3", V33.EastId, new Vector2Int(9, 6), null),
                ("ver3.4", V33.EastId, new Vector2Int(4, 6), null), ("ver3.5", "first_field", new Vector2Int(-7, -5), new[] { Plaza, Home }),
                ("ver3.6", V36.NorthId, new Vector2Int(6, 8), new[] { Plaza, Home, Pond }),
                ("ver3.7", V37.WestId, new Vector2Int(8, 9), new[] { Plaza, Home, Pond, Clearing }),
            };
            foreach (var (version, map, cell, found) in places)
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", map, cell);
                data.GameVersion = version;
                if (found != null) data.FastTravel = new List<string>(found);
                if (version == "ver3.6" || version == "ver3.7") data.CrowMode = HarukaSaveData.CrowFollow;
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Settle(g, 0.5f);
                Check($"a {version} save ({map}): resumed there, steps kept; South not found; " + (found != null ? "its places kept" : "the house"),
                    g.Session.TryGetSavePlace(out var m, out var l) && m == map && l == cell && !T(g).IsFound(Pinwheel) && g.LifeLog.TotalSteps == 4321 &&
                    (found == null ? T(g).FoundIds.SequenceEqual(new[] { Home }) : found.All(id => T(g).IsFound(id))) &&
                    (version != "ver3.6" && version != "ver3.7" || g.Crow.Following),
                    $"{(g.Session.TryGetSavePlace(out var mm, out var ll) ? mm + " " + ll : "no place")} steps {g.LifeLog.TotalSteps} found {string.Join(",", T(g).FoundIds)} crow {g.Crow.Mode}");
            }
            // A damaged newest snapshot: the older one (by the pinwheel) is loaded.
            {
                var storage = new HarukaMemoryStorage();
                var g = Begin(storage);
                Walk(g, TurnSpot);
                Settle(g, 2f);
                g.Save.Write("check", C6.Clock);
                var latest = C6.Latest(g);
                var newest = HarukaSnapshotStore.SlotNames.First(n => HarukaSaveData.TryRead(storage.Read(n), out var d, out _) == HarukaSaveReadStatus.Valid && d.Revision == latest.Revision);
                storage.Write(newest, storage.Read(newest).Replace("\"" + SouthId + "\"", "\"south_area_02\""));
                var r = Begin(storage);
                Check("the newest snapshot damaged: the backup snapshot is loaded, South found, by the pinwheel",
                    r.Save.LoadSource == "backup snapshot" && T(r).IsFound(Pinwheel) && r.Session.CurrentRegionMapId == SouthId, $"{r.Save.LoadSource} {Where(r)}");
            }
            // Rollback: what a build without the South area does with this save (its Map unknown to it).
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", "south_area_00", new Vector2Int(7, 7));
                data.TotalSteps = 4567;
                data.FastTravel = new List<string> { Plaza, Home, Pond, Clearing, "landmark_future_99" };
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("what a build without the area does with its save: the first map's start, steps kept, an unknown place id kept (and saved again)",
                    g.Session.TryGetSavePlace(out var m, out _) && m == "first_field" && g.LifeLog.TotalSteps == 4567 && g.Save.LocationNote.Contains("unknown map") &&
                    T(g).IsFound(Clearing) && T(g).IsFound("landmark_future_99") && T(g).FoundPoints().All(p => p.id != "landmark_future_99") &&
                    T(g).SavedIds().Contains("landmark_future_99"), g.Save.LocationNote);
            }
        }

        // ---------------------------------------------------------------- 19.8 chunk streaming

        private static void ChunkChecks()
        {
            C6.SetLegacy(null, null);
            // The official world: the four directions from the middle, twice.
            var c = Begin(new HarukaMemoryStorage());
            var streamer = c.Presenter.Streamer;
            c.Session.PlaceForCheck(Vector2Int.zero, FacingDirection.Down);
            Settle(c, 1f);
            var index = c.Session.OutdoorIndex;
            var all = index.Chunks.Count;
            Check($"the official world has {all} chunks (16 x 16; 20 in ver3.7, the South area adds its own): at the start every one is drawn, none failed",
                streamer.Stats.Loaded == all && all > 20 && streamer.Stats.Failed == 0 && streamer.Stats.FallbackChunks == 0 &&
                index.Chunks.Keys.Any(k => k.Y == -2), $"loaded {streamer.Stats.Loaded} of {all}");
            var objectsAll = c.Presenter.LoadedChunkObjects;
            var waterAll = c.Presenter.WaterCellsDrawn;
            var lightAll = c.Presenter.KomorebiCellsDrawn;
            var meshesAtStart = RuntimeMeshes();
            var namesAtStart = RuntimeMeshNames();
            var outsideAtStart = AllTransforms() - ChunkTransforms();
            var route = new[] { ("North", PondArrival), ("the middle", Vector2Int.zero), ("South", new Vector2Int(3, -24)), ("the middle", Vector2Int.zero),
                ("East", PlazaArrival), ("the middle", Vector2Int.zero), ("West", WestEnd), ("the middle", Vector2Int.zero) };
            var log = new List<string>();
            var meshesAfter = new List<int>();
            var unloadsNorthSouth = 0;
            for (var round = 1; round <= 2; round++)
            {
                foreach (var (name, goal) in route)
                {
                    var u = streamer.Stats.TotalUnloads;
                    var walked = Walk(c, goal, null, 240f);
                    Settle(c, 0.5f);
                    if (name == "North" || name == "South") unloadsNorthSouth += streamer.Stats.TotalUnloads - u;
                    log.Add($"{round}:{name} {(walked ? "" : "(not reached) ")}loaded {streamer.Stats.Loaded} loads {streamer.Stats.TotalLoads} unloads {streamer.Stats.TotalUnloads}");
                }
                Settle(c, 1f);
                meshesAfter.Add(RuntimeMeshes());
            }
            var reloaded = streamer.Records.Values.Where(r => r.Loads > 1).ToList();
            Check("two rounds middle - North - middle - South - middle - East - middle - West - middle, on foot: every leg reached",
                !log.Any(l => l.Contains("not reached")), string.Join("; ", log));
            Check("East <-> West frees the far end's chunks and draws them again (as in ver3.7); North <-> South frees nothing: the world is 53 rows tall, within the views' reach (40) plus the kept ring",
                streamer.Stats.TotalUnloads > 0 && reloaded.Count > 0 && unloadsNorthSouth == 0, $"unloads {streamer.Stats.TotalUnloads}, reloaded chunks {reloaded.Count}, North/South legs freed {unloadsNorthSouth}");
            Check("back in the middle: every chunk drawn again, the same Objects (none twice), the same water and light as at first",
                streamer.Stats.Loaded == all && c.Presenter.LoadedChunkObjects == objectsAll && V32.DuplicateViews(c).Count == 0 &&
                c.Presenter.WaterCellsDrawn == waterAll && c.Presenter.KomorebiCellsDrawn == lightAll && c.Presenter.PinwheelBladesLoaded == 1,
                $"{V32.ChunkSummary(c)} objects {c.Presenter.LoadedChunkObjects}/{objectsAll}");
            Check("runtime meshes: after each round as many as at first (nothing a freed chunk made is left behind; ver3.7's fix holds)",
                meshesAfter.All(m => m == meshesAtStart), $"{meshesAtStart} -> {string.Join(", ", meshesAfter)}; left: " + LeftBehind(namesAtStart));
            Check("GameObjects outside the chunks: as many as at first (nothing piles up)", AllTransforms() - ChunkTransforms() == outsideAtStart,
                $"{outsideAtStart} -> {AllTransforms() - ChunkTransforms()}");
            Check("no chunk was ever built in a hurry by the step gate (the prefetch was in time) and none failed",
                streamer.Stats.SyncLoads == 0 && streamer.Stats.TotalFailures == 0, V32.ChunkSummary(c));
            Check("summary (official world, two rounds): loads " + streamer.Stats.TotalLoads + ", unloads " + streamer.Stats.TotalUnloads + ", chunks drawn again " + reloaded.Count +
                ", average load " + streamer.Stats.AverageLoadMs.ToString("0.0") + " ms", streamer.Records.Count <= all + 4);

            // The South chunks freed too: a shorter view reach (checks only) frees the hill's chunk when she goes far North,
            // and draws it again (with the pinwheel's blades, where they stopped) when she comes back.
            Walk(c, TurnSpot);
            c.Session.PlaceForCheck(TurnSpot, FacingDirection.Up);
            Settle(c, 0.5f);
            var pinwheel = PinwheelObject(c);
            c.Session.InteractAhead();
            Settle(c, 1f);
            Check("a spin under way while she walks off", c.Presenter.PinwheelSpinning);
            var axis = -GridSpace3D.Direction(Vector2Int.down);
            var bladesBefore = c.Presenter.PinwheelBladesOf(pinwheel.InstanceId);
            var restBefore = Quaternion.Inverse(Quaternion.AngleAxis(c.Presenter.PinwheelAngle(pinwheel.InstanceId), axis)) * bladesBefore.rotation;
            var reach = c.Presenter.ReachCells3D;
            var half = c.Presenter.ViewHalfCells2D;
            c.Presenter.ReachCells3D = 12;
            c.Presenter.ViewHalfCells2D = new Vector2Int(12, 8);
            Settle(c, 1f);   // (the chunks beyond the shorter reach are freed here, before the measure)
            var hillChunk = index.ChunkOfCell(PinwheelCell);
            var hillLoads = streamer.Records[hillChunk].Loads;
            var meshesHere = RuntimeMeshes();
            var namesHere = RuntimeMeshNames();
            var outsideHere = AllTransforms() - ChunkTransforms();
            var orphansHere = OrphanMeshes();
            Walk(c, new Vector2Int(0, 20), null, 240f);
            Settle(c, 1f);
            Check("with a 12-cell reach, at the North pond's far side (0,20): the hill's chunk (y -32..-17) freed, its pinwheel's blades no longer registered",
                streamer.StateOf(hillChunk) == ChunkState.Unloaded && !c.Presenter.IsChunkDrawn(hillChunk) && c.Presenter.PinwheelBladesLoaded == 0 &&
                c.Presenter.PinwheelBladesOf(pinwheel.InstanceId) == null, V32.ChunkSummary(c));
            var stopped = c.Presenter.PinwheelAngle(pinwheel.InstanceId);
            Walk(c, TurnSpot, null, 240f);
            Settle(c, 1f);
            var blades = c.Presenter.PinwheelBladesOf(pinwheel.InstanceId);
            var view = c.Presenter.FindView(pinwheel.InstanceId);
            var restAfter = blades != null ? Quaternion.Inverse(Quaternion.AngleAxis(stopped, axis)) * blades.rotation : Quaternion.identity;
            Check("back at the pinwheel: its chunk drawn again (loaded once more), one set of blades registered, standing where the spin stopped",
                streamer.Records[hillChunk].Loads == hillLoads + 1 && blades != null && c.Presenter.PinwheelBladesLoaded == 1 && !c.Presenter.PinwheelSpinning &&
                Mathf.Abs(stopped - c.Presenter.PinwheelAngle(pinwheel.InstanceId)) < 0.01f && view != null && Quaternion.Angle(restBefore, restAfter) < 0.5f,
                $"loads {hillLoads} -> {streamer.Records[hillChunk].Loads}, angle {stopped:0}, rest moved {Quaternion.Angle(restBefore, restAfter):0.00}");
            c.Session.PlaceForCheck(TurnSpot, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("…and it turns again", c.Session.InteractAhead() && c.Presenter.PinwheelSpinning);
            Settle(c, PocWorldPresenter.PinwheelSeconds + 0.2f);
            Check("runtime meshes and GameObjects back where they were before the hill's chunk was freed and drawn again",
                RuntimeMeshes() == meshesHere && AllTransforms() - ChunkTransforms() == outsideHere && OrphanMeshes() == orphansHere,
                $"meshes {meshesHere} -> {RuntimeMeshes()}, unused {orphansHere} -> {OrphanMeshes()}; left: " + LeftBehind(namesHere));
            c.Presenter.ReachCells3D = reach;
            c.Presenter.ViewHalfCells2D = half;
            Settle(c, 1f);
            // Into the house and out again, three times (each map change rebuilds the presentation).
            c.Session.OutdoorWorld.TryGetHouseDoor(out var houseDoor);
            Walk(c, houseDoor + Vector2Int.down);
            Settle(c, 0.5f);
            var meshesOutside = RuntimeMeshes();
            var namesOutside = RuntimeMeshNames();
            for (var i = 0; i < 3; i++)
            {
                V32.Step(c, Vector2Int.up, 0.4f);
                Settle(c, 0.3f);
                V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
                V32.Step(c, Vector2Int.down, 0.4f);
                Settle(c, 0.5f);
            }
            Check("into the house and out again three times: as many runtime meshes as before (ver3.7's fix), the pinwheel registered once",
                c.Session.World == c.Session.OutdoorWorld && RuntimeMeshes() == meshesOutside && c.Presenter.PinwheelBladesLoaded == 1,
                $"{meshesOutside} -> {RuntimeMeshes()}; left: " + LeftBehind(namesOutside));
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var models = catalog.Bindings.SelectMany(b => new[] { b.model }.Concat(b.variants ?? new List<GameObject>())).Where(m => m != null).ToList();
            Check("the catalog's models (the new South ones too) untouched by the freeing (every model mesh still there, an asset)",
                models.Count > 25 && models.All(m => m.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh != null && AssetDatabase.Contains(f.sharedMesh))),
                models.Count + " models");

            // The ver3.2 256 x 256 test world, the same streamer as the game: a long walk East and back, then North and back.
            var w = C6.Start(new HarukaMemoryStorage(), null, s =>
            {
                var (world, primary) = V32.TestWorld(256);
                s.UseOutdoorWorldForTest(world, primary, false);
            });
            V32.Settle(w, 0.5f);
            var ws = w.Presenter.Streamer;
            var start = w.Session.Cell;
            var startChunk = w.Session.OutdoorIndex.ChunkOfCell(start);
            var startViews = w.Presenter.Views.Where(v => !v.IsWall && w.Session.OutdoorIndex.ChunkOfCell(v.RootCell) == startChunk).Select(v => v.InstanceId).OrderBy(x => x).ToList();
            var outsideW = AllTransforms() - ChunkTransforms();
            var meshesW = RuntimeMeshes();
            var orphansW = OrphanMeshes();
            var namesW = RuntimeMeshNames();
            var monoAtStart = GC.GetTotalMemory(true);
            var loadedW = w.Presenter.ReadyChunks().Count();
            foreach (var (dir, label) in new[] { (new Vector2Int(110, 0), "East"), (new Vector2Int(0, 110), "North") })
            {
                var maxLoaded = 0;
                var u0 = ws.Stats.TotalUnloads;
                var l0 = ws.Stats.TotalLoads;
                var goal = V32.NearestWalkable(w, start + dir);
                var walked = V32.WalkTo(w, goal, _ => maxLoaded = Mathf.Max(maxLoaded, ws.Stats.Loaded), 0.14f);
                V32.Settle(w, 1f);
                Check($"256 x 256 test world: she walks about 110 cells {label} ({start} -> {w.Session.Cell}): chunks loaded ahead, the start's freed by the streamer itself, at most the 9 x 9 keep area drawn",
                    walked && ws.Stats.TotalLoads > l0 + 4 && ws.Stats.TotalUnloads > u0 && ws.StateOf(startChunk) == ChunkState.Unloaded && maxLoaded <= 81,
                    $"{V32.ChunkSummary(w)} max {maxLoaded}");
                var back = V32.WalkTo(w, start, null, 0.14f);
                V32.Settle(w, 1.5f);
                var againViews = w.Presenter.Views.Where(v => !v.IsWall && w.Session.OutdoorIndex.ChunkOfCell(v.RootCell) == startChunk).Select(v => v.InstanceId).OrderBy(x => x).ToList();
                Check($"…back from the {label}: the start's chunk drawn again with exactly the same Objects, none twice",
                    back && ws.StateOf(startChunk) == ChunkState.Ready && againViews.SequenceEqual(startViews) && V32.DuplicateViews(w).Count == 0,
                    $"{startViews.Count} views, again {againViews.Count}");
            }
            var monoBack = GC.GetTotalMemory(true);
            var drawnBack = w.Presenter.ReadyChunks().Count();
            Check("256 x 256 after both walks: no runtime mesh left unused (a freed chunk's are destroyed with it), GameObjects outside the chunks as at the start, managed memory within 8 MB",
                OrphanMeshes() <= orphansW && AllTransforms() - ChunkTransforms() == outsideW && monoBack - monoAtStart < 8L * 1024 * 1024,
                $"unused runtime meshes {orphansW} -> {OrphanMeshes()}; all runtime meshes {meshesW} -> {RuntimeMeshes()} (they follow the chunks drawn: {loadedW} -> {drawnBack}); " +
                $"outside {outsideW} -> {AllTransforms() - ChunkTransforms()}, mono {monoAtStart / 1048576f:0.0} -> {monoBack / 1048576f:0.0} MB");
            w.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            V32.Settle(w, 0.5f);
            var loadsBeforeSwitch = ws.Stats.TotalLoads;
            for (var i = 0; i < 6; i++) { w.Switcher.Toggle(); V32.Frame(w, 1f / 60f, Vector2Int.zero); }
            Check("2D / 3D switches after the walks: nothing built or freed by a switch", ws.Stats.TotalLoads == loadsBeforeSwitch, V32.ChunkSummary(w));
        }

        // ---------------------------------------------------------------- what was there

        private static void ExistingChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var houseObjects = c.Session.InteriorWorld.Objects.Count();
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Walk(c, door + Vector2Int.down);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("the house: in through its door, its furniture all there (" + houseObjects + " Objects, its Map's)", c.Session.World.MapId == "halka_house" &&
                c.Session.World.Objects.Count() == houseObjects && houseObjects == c.Session.World.Map.Objects.Count, houseObjects.ToString());
            V32.WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.14f);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("the booth from the house", c.Session.World.MapId == "halka_soundbooth");
            V32.Step(c, Vector2Int.down, 0.4f);
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Check("and out again (the forest's light and the pinwheel rebuilt with the outdoor world)", c.Session.World == c.Session.OutdoorWorld &&
                c.Session.Cell == door + Vector2Int.down && c.Presenter.KomorebiCellsDrawn > 0 && c.Presenter.PinwheelBladesLoaded == 1);
            var north = c.Session.OutdoorIndex.FindRegion(V36.NorthId);
            Check("the North area as it was: 21 x 20 cells, its 22 water cells drawn, the pond's marker",
                north.Rect.Min == new Vector2Int(-10, 7) && north.Rect.Max == new Vector2Int(10, 26) && north.Map.TryGetMarker(Pond, out var pm) &&
                pm == new Vector2Int(6, 8) && c.Presenter.WaterCellsDrawn == 22);
            var west = c.Session.OutdoorIndex.FindRegion(V37.WestId);
            Check("the West area as it was: 25 x 13 cells, 83 Objects, its 8 komorebi markers, the clearing's marker",
                west.Rect.Min == new Vector2Int(-35, -6) && west.Rect.Max == new Vector2Int(-11, 6) && west.Map.Objects.Count == 83 &&
                west.Map.Markers.Count(m => m.StableId.StartsWith(PocWorldPresenter.KomorebiMarkerPrefix, StringComparison.Ordinal)) == 8 &&
                west.Map.TryGetMarker(Clearing, out var cm) && cm == new Vector2Int(8, 9));
            var east = c.Session.OutdoorIndex.FindRegion(V33.EastId);
            Check("the East area as it was: its 26 x 13 cells, the plaza's marker", east.Rect.Min == new Vector2Int(11, -6) && east.Rect.Max == new Vector2Int(36, 6) &&
                east.Map.TryGetMarker(Plaza, out var m) && m == new Vector2Int(16, 4));
            Walk(c, PlazaArrival);
            Check("the plaza is still found by walking there", T(c).IsFound(Plaza));
            Walk(c, new Vector2Int(-3, 15));
            Check("the pond is still found by walking there", T(c).IsFound(Pond));
            Walk(c, StumpSpot);
            Check("the clearing is still found by walking there", T(c).IsFound(Clearing));
            // AUTO in first_field next to the South road: never into the South area by itself.
            c.Session.PlaceForCheck(new Vector2Int(2, -5), FacingDirection.Down);
            c.Hud.ClearMessage();
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var regions = new HashSet<string>();
            for (var t = 0f; t < 150f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId ?? c.Session.World.MapId); }
            Check("AUTO in first_field next to the South road for 150 s: it walks, but never into the South area (nor into the house)", c.Gameplay.AutoWorld.AutoSteps > 0 &&
                !regions.Contains(SouthId) && !regions.Contains("halka_house"), string.Join(",", regions));
            c.Auto.SetAutoEnabled(false, C6.Clock);
            Check("the menu and its rows (そらをとぶ, セーブデータ) are there", c.Menu.LinkLabels.Contains(PocFastTravel.MenuLabel) &&
                c.Menu.LinkLabels.Contains(HarukaSaveSystem.MenuLabel));
            Check("the Life Log counts steps as before", c.LifeLog.TotalSteps > 0);
            Check("the game's version label: ver3.8", PocDebugPanel.ReleaseCandidateVersionLabel == "ver3.8");
        }

        // ---------------------------------------------------------------- what must not change

        private static void ProtectChecks()
        {
            Check("WORLD EDITOR v1.0 unchanged (tools/HalkaWorldMapEditor, since ver3.6's publication)", V32.Git("diff --quiet " + Published + " -- tools/HalkaWorldMapEditor", out _));
            Check("first_field, east_area_01, the house and the booth Maps (Authoring JSON and caches) unchanged", V36.OfficialMapsKept(Published));
            Check("north_area_01 and west_area_01 (Authoring JSON and caches) unchanged",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/Maps/Authoring/north_area_01.hwmap.json unity-project/Assets/Content/Maps/north_area_01.asset", out _) &&
                V32.Git("diff --quiet " + Ver37 + " -- unity-project/Assets/Content/Maps/Authoring/west_area_01.hwmap.json unity-project/Assets/Content/Maps/west_area_01.asset", out _));
            Check("the Maps folder: nothing changed or removed since ver3.7; the catalog and the world registry only added to", V36.MapsFolderKept(Ver37) && V36.MapsFolderKept(Published));
            Check("direction_themes.json unchanged; the world registry only added to (the four regions' lines as they were)", V36.RegistryKept(Ver37) && V36.RegistryKept(Published));
            Check("the そらをとぶ places: only added to (the four of ver3.7 unchanged)", V36.OnlyAdded(Ver37, "unity-project/Assets/Content/FastTravel/fast_travel_points.json"));
            Check("the published ver3.6 (halkaworld/) unchanged", V32.Git("diff --quiet " + Published + " -- halkaworld", out _));
            Check("the crow's sprites, model and voice, and the font atlas unchanged", V36.CrowKept(Published, true));
            Check("the save system and the crow's code unchanged (no Save Schema change)",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/Save unity-project/Assets/POC2D3D/Runtime/PocCrow.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Companion.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Travel.cs unity-project/Assets/Scripts/World/CrowBehaviourCore.cs", out _));
            Check("the chunk streamer unchanged (World/ChunkStreamer.cs): verified as it is, not rewritten",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/World/ChunkStreamer.cs", out _));
            Check("ver3.7's work kept: the forest and the water code unchanged, the mesh freeing of chunks and rooms still there",
                V32.Git("diff --quiet " + Ver37 + " -- unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.Forest.cs unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.Water.cs unity-project/Assets/POC2D3D/Runtime/PocOutlineTag.cs", out _) &&
                File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.Chunks.cs")).Contains("CollectMeshes(root, payload.Meshes, true);") &&
                File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.cs")).Contains("FreeMeshes(roomMeshes);"));
            Check("the old tests are all still there (none deleted since ver3.6 or ver3.7)",
                V32.Git("diff --quiet --diff-filter=D " + Published + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _) &&
                V32.Git("diff --quiet --diff-filter=D " + Ver37 + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _));
        }
    }
}
