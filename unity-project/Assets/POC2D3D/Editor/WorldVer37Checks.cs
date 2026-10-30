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

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.7「西・緑エリア」: the checks of the West region west_area_01 (the registry, the road
    // over the region border x -10|-11 and the chunk borders x -16|-17 and -32|-33, the forest: its
    // Collision, the sunlight through the leaves by day and night, the clearing and its stump with
    // きりかぶにふれる, the crow among the trees, そらをとぶ to にしのこもれび with four places, the save), of the
    // chunks streaming as she walks (the official world and the ver3.2 256 x 256 test world: loaded,
    // freed and drawn again on the way back), and of what must not change. The game is started with the
    // Phase 6 harness (memory storage only); every frame also runs what Update does in play (the crow,
    // the choices' watch, the chunk streamer, the water, the forest, the flight, the time of day).
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer37Checks.RunBatch
    public static class WorldVer37Checks
    {
        public const string WestId = "west_area_01";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Home = "landmark_halka_home";
        private const string Plaza = "landmark_east_flower_plaza";
        private const string Pond = "landmark_north_blue_pond";
        private const string Clearing = "landmark_west_forest_clearing";
        private const string Published = "43cc967";   // ver3.6 as published (2026-10-29)
        private static readonly Vector2Int WestOffset = new Vector2Int(-35, -6);
        private static readonly Vector2Int TouchSpot = new Vector2Int(-27, 3);       // local (8,9): the stump's Action Point
        private static readonly Vector2Int Stump = new Vector2Int(-27, 4);           // local (8,10)
        private static readonly Vector2Int ClearingArrival = new Vector2Int(-27, 2); // local (8,8)
        private static readonly Vector2Int TrailMouth = new Vector2Int(-24, 0);      // local (11,6), off the road
        private static readonly Vector2Int WestEnd = new Vector2Int(-35, -1);        // local (0,5), the road's West end
        private static readonly Vector2Int HomeArrival = new Vector2Int(-7, -5);
        private static readonly Vector2Int PlazaArrival = new Vector2Int(27, -2);
        private static readonly Vector2Int PondArrival = new Vector2Int(-3, 15);
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
            UnityEngine.Random.InitState(3737);
            try
            {
                Run("region", RegionChecks);
                Run("walk", WalkChecks);
                Run("forest", ForestChecks);
                Run("stump", StumpChecks);
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
            foreach (var failure in failures) Debug.LogError("VER37 FAIL " + failure);
            Debug.Log("HARUKA ver3.7 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.7 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V37_LOGS", "ver37_checks.txt");
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
            if (condition) { passes++; Debug.Log("VER37 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness (as WorldVer36Checks)

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

        private static OutdoorRegion West(C6.Ctx c) => c.Session.OutdoorIndex.FindRegion(WestId);

        private static bool InWest(C6.Ctx c, Vector2Int cell) => West(c).ContainsWorld(cell);

        // The komorebi meshes drawn now: (renderer, quad centres).
        private static List<(Renderer renderer, List<Vector3> quads)> KomorebiMeshes()
        {
            var list = new List<(Renderer, List<Vector3>)>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("Komorebi", StringComparison.Ordinal)) continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices;
                var quads = new List<Vector3>();
                for (var i = 0; i + 3 < v.Length; i += 4) quads.Add(r.transform.TransformPoint((v[i] + v[i + 1] + v[i + 2] + v[i + 3]) * 0.25f));
                list.Add((r, quads));
            }
            return list;
        }

        // Meshes that exist only at run time (made by the game, not loaded from the project).
        private static int RuntimeMeshes() =>
            Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0);

        private static Dictionary<string, int> RuntimeMeshNames() =>
            Resources.FindObjectsOfTypeAll<Mesh>().Where(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0)
                .GroupBy(m => System.Text.RegularExpressions.Regex.Replace(m.name, @"[-0-9, ()]+$", "").Trim()).ToDictionary(g => g.Key, g => g.Count());

        // The kinds of runtime mesh there are more of now than before (name: how many more).
        private static string LeftBehind(Dictionary<string, int> before) =>
            string.Join(", ", RuntimeMeshNames().Where(p => p.Value > before.GetValueOrDefault(p.Key)).OrderByDescending(p => p.Value - before.GetValueOrDefault(p.Key))
                .Take(12).Select(p => p.Key + " +" + (p.Value - before.GetValueOrDefault(p.Key))));

        // Every Transform under a drawn chunk's root.
        private static int ChunkTransforms() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Chunk (", StringComparison.Ordinal)).Sum(t => t.GetComponentsInChildren<Transform>(true).Length);

        private static List<Vector2Int> KomorebiMarkers(C6.Ctx c) =>
            West(c).Map.Markers.Where(m => m.StableId.StartsWith(PocWorldPresenter.KomorebiMarkerPrefix, StringComparison.Ordinal))
                .Select(m => West(c).ToWorld(m.Cell)).ToList();

        // ---------------------------------------------------------------- 17.1 the region

        private static void RegionChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var definition = index.Definition;
            var west = West(c);
            // ver3.8: more regions may follow (the South area is the fifth); these four come first, in this order.
            Check("the registry: first_field, east_area_01, north_area_01 and west_area_01 (in that order)",
                index.Regions.Select(r => r.MapId).Take(4).SequenceEqual(new[] { "first_field", V33.EastId, V36.NorthId, WestId }), string.Join(",", index.Regions.Select(r => r.MapId)));
            Check("west_area_01: direction west, World offset (-35,-6), 25 x 13 cells: World (-35,-6)..(-11,6)",
                west != null && west.Direction == "west" && west.Offset == WestOffset &&
                west.Rect.Min == new Vector2Int(-35, -6) && west.Rect.Max == new Vector2Int(-11, 6), west != null ? $"{west.Offset} {west.Rect.Min}..{west.Rect.Max}" : "missing");
            var theme = definition.ThemeOf("west");
            Check("its theme: にし, #6e9f57 / #4b7a3b (direction_themes.json, unchanged)", theme != null && theme.name == "にし" &&
                ColorUtility.ToHtmlStringRGB(theme.color) == "6E9F57" && ColorUtility.ToHtmlStringRGB(theme.shade) == "4B7A3B");
            var problems = definition.Validate();
            Check("the world passes its checks: no region overlaps another", !OutdoorWorldDefinition.HasErrors(problems), string.Join("; ", problems));
            Check("first_field, east_area_01 and north_area_01 did not move or change size", index.FindRegion("first_field").Rect.Min == new Vector2Int(-10, -6) &&
                index.FindRegion("first_field").Rect.Max == new Vector2Int(10, 6) && index.FindRegion(V33.EastId).Offset == new Vector2Int(11, -6) &&
                index.FindRegion(V33.EastId).Rect.Max == new Vector2Int(36, 6) && index.FindRegion(V36.NorthId).Offset == new Vector2Int(-10, 7) &&
                index.FindRegion(V36.NorthId).Rect.Max == new Vector2Int(10, 26));
            Check("the Map is the authored one (Map JSON v4 through the importer): 29 Surfaces, 83 Objects, the clearing's and the road's markers",
                west.Map.Surfaces.Count == 29 && west.Map.Objects.Count == 83 && west.Map.TryGetMarker(Clearing, out var marker) &&
                marker == new Vector2Int(8, 9) && west.Map.TryGetMarker("road_east", out var re) && re == new Vector2Int(24, 6) &&
                west.Map.TryGetMarker("road_west", out var rw) && rw == new Vector2Int(0, 5), $"{west.Map.Surfaces.Count} surfaces, {west.Map.Objects.Count} objects");
            var rules = MapPlacementRules.Validate(west.Map);
            Check("the game's own Map rules find nothing wrong in it (WORLD EDITOR's MapRules: 0 issues when authored)", !rules.Any(p => p.StartsWith("ERROR")), string.Join("; ", rules));
            // One road West: first_field's West road (y 0, x -10..-3) on through the forest to its West edge.
            var road = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            road.Add(new Vector2Int(-3, 0));
            queue.Enqueue(new Vector2Int(-3, 0));
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = cell + d;
                    if (road.Contains(n) || !index.CanEnter(n) || index.SurfaceAt(n)?.StableId != "dirt" || n.y > 0 && n.x < -10 && !InWest(c, n)) continue;
                    road.Add(n);
                    queue.Enqueue(n);
                }
            }
            Check("one continuous dirt road from first_field's West road (-3,0) over the border (-10,0)|(-11,0) to the forest's West edge (-35,-1)",
                road.Contains(new Vector2Int(-10, 0)) && road.Contains(new Vector2Int(-11, 0)) && road.Contains(new Vector2Int(-19, 0)) &&
                road.Contains(new Vector2Int(-19, -1)) && road.Contains(WestEnd), $"{road.Count} road cells");
            Check("nothing blocks the border columns next to the road (x -10 and -11, y -1..1)",
                new[] { -1, 0, 1 }.All(y => index.CanEnter(new Vector2Int(-10, y)) && index.CanEnter(new Vector2Int(-11, y))));
            Check("outside every region nothing is walkable (West of the forest, North and South of it)",
                !index.CanEnter(new Vector2Int(-36, 0)) && !index.CanEnter(new Vector2Int(-20, 7)) && !index.CanEnter(new Vector2Int(-20, -7)) &&
                index.RegionAt(new Vector2Int(-36, 0)) == null && index.RegionAt(new Vector2Int(-20, 7)) == null);
            var reach = new HashSet<Vector2Int> { new Vector2Int(-11, 0) };
            queue = new Queue<Vector2Int>(reach);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = cell + d;
                    if (!west.ContainsWorld(n) || reach.Contains(n) || !index.CanEnter(n)) continue;
                    reach.Add(n);
                    queue.Enqueue(n);
                }
            }
            var walkable = 0;
            for (var y = west.Rect.Min.y; y <= west.Rect.Max.y; y++)
            for (var x = west.Rect.Min.x; x <= west.Rect.Max.x; x++)
                if (index.CanEnter(new Vector2Int(x, y))) walkable++;
            Check("every walkable cell of the forest can be reached on foot (no closed-in corner): 271 cells", reach.Count == walkable && walkable == 271,
                $"{reach.Count} of {walkable}");
            Check("the Region border x -10|-11 and the chunk borders x -16|-17 and -32|-33 are different columns (chunk size 16)",
                index.ChunkOfCell(new Vector2Int(-10, 0)) == index.ChunkOfCell(new Vector2Int(-11, 0)) &&
                index.ChunkOfCell(new Vector2Int(-16, 0)) != index.ChunkOfCell(new Vector2Int(-17, 0)) &&
                index.ChunkOfCell(new Vector2Int(-32, 0)) != index.ChunkOfCell(new Vector2Int(-33, 0)));
            Check("the save menu names it by its direction: にし", c.Save.PlaceName(WestId) == "にし", c.Save.PlaceName(WestId));
        }

        // ---------------------------------------------------------------- 17.2 walking

        private static void WalkChecks()
        {
            C6.SetLegacy(null, null);
            // The 2D camera where first_field, the North and the East areas are: the framing the world had
            // without the West area (the ver3.6 world, the same game).
            var cells = new[] { Vector2Int.zero, new Vector2Int(-8, 0), new Vector2Int(-10, 0), new Vector2Int(-9, -5), new Vector2Int(-10, 5),
                new Vector2Int(5, 3), new Vector2Int(10, -1), new Vector2Int(20, 0), new Vector2Int(30, -3), new Vector2Int(0, 16), new Vector2Int(-8, 20) };
            // (a save to start from: with no save the game would read the old 2D game's data for its start)
            var storage = new HarukaMemoryStorage();
            storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(V32.Old("ver3.3", "first_field", Vector2Int.zero)));
            // The world as the scene builder reads it from the registry, less the West area (loaded in the new
            // scene: opening it unloads assets nothing references yet).
            var o = C6.Start(storage, null, s =>
            {
                var primary = AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath);
                var full = PocSceneBuilder.LoadOutdoorWorld(primary);
                var withoutWest = new OutdoorWorldDefinition
                {
                    worldId = full.worldId, chunkSize = full.chunkSize, themes = full.themes, formatVersion = full.formatVersion,
                    regions = full.regions.Where(r => r.mapId != WestId).ToList()
                };
                s.UseOutdoorWorldForTest(withoutWest, primary, false);
            });
            // ver3.8: the comparison world has the South area too (after these three); still no West area.
            Check("the comparison world: first_field, east_area_01, north_area_01 (no West area)",
                o.Session.OutdoorIndex.Regions.Select(r => r.MapId).Take(3).SequenceEqual(new[] { "first_field", V33.EastId, V36.NorthId }) &&
                o.Session.OutdoorIndex.Regions.All(r => r.MapId != WestId));
            var before = cells.ToDictionary(cell => cell, cell => CameraFor(o, cell));
            var c = Begin(new HarukaMemoryStorage());
            var moved = cells.Where(cell => (CameraFor(c, cell) - before[cell]).magnitude > 0.005f).ToList();
            Check("2D camera in first_field, the North and the East areas: exactly where it was without the West area (11 places, the West edge (-10,0) too)",
                moved.Count == 0, string.Join(" ", moved.Select(m => m + " " + CameraFor(c, m).ToString("0.00") + " was " + before[m].ToString("0.00"))));
            var startCam = CameraFor(c, Vector2Int.zero);
            Check("…and at the start (0,0) as in ver3.6: (0.00, 22.98, -19.28)", (startCam - new Vector3(0f, 22.98f, -19.28f)).magnitude < 0.02f, startCam.ToString("0.00"));
            var edgeCam = CameraFor(c, WestEnd);
            var herEdge = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(WestEnd));
            Check("2D camera at the forest's West edge (-35,-1): she is on screen (the camera stops at the world's edge)",
                herEdge.x > 16f && herEdge.x < 1264f && herEdge.y > 16f && herEdge.y < 704f, $"her on screen at {herEdge.x:0},{herEdge.y:0}, camera {edgeCam:0.00}");
            var deepCam = CameraFor(c, new Vector2Int(-25, 0));
            var herDeep = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(new Vector2Int(-25, 0)));
            var midCam = CameraFor(c, new Vector2Int(-16, 0));
            Check("2D camera in the forest: it has followed her West of first_field ((-16,0): over the forest; (-25,0): as far as the world's edge lets it), she on screen",
                midCam.x < before[new Vector2Int(-10, 0)].x - 0.5f && deepCam.x <= midCam.x && herDeep.x > 16f && herDeep.x < 1264f,
                $"camera x at (-10,0) {before[new Vector2Int(-10, 0)].x:0.00}, (-16,0) {midCam.x:0.00}, (-25,0) {deepCam.x:0.00}; her on screen at x {herDeep.x:0}");
            Check("2D camera height in the forest: the same as in first_field (its rows are first_field's)", Mathf.Abs(deepCam.z - startCam.z) < 0.01f);
            // Walk West from the field over the region border.
            c.Session.PlaceForCheck(new Vector2Int(-9, 0), FacingDirection.Left);
            c.Pixel.SnapToTarget();
            Settle(c, 0.3f);
            var steps = c.Session.CompletedSteps;
            var loads = c.Session.MapLoads;
            var changes = c.Session.RegionChanges;
            var lifeSteps = c.LifeLog.TotalSteps;
            var auto = c.Auto.AutoEnabled;
            C6.Step(c, Vector2Int.left, 0.4f);
            C6.Step(c, Vector2Int.left, 0.4f);
            Check("two steps West (-9,0) -> (-10,0) -> (-11,0): the West area, facing left, one region change, no map load",
                c.Session.Cell == new Vector2Int(-11, 0) && c.Session.CurrentRegionMapId == WestId && c.Session.Facing == FacingDirection.Left &&
                c.Session.RegionChanges == changes + 1 && c.Session.MapLoads == loads && c.Session.World == c.Session.OutdoorWorld, $"{c.Session.Cell} {c.Session.CurrentRegionMapId}");
            Check("each step counted once (session and Life Log); AUTO setting unchanged", c.Session.CompletedSteps == steps + 2 && c.LifeLog.TotalSteps == lifeSteps + 2 &&
                c.Auto.AutoEnabled == auto);
            var last = c.Pixel.WorldCamera.transform.position;
            var maxJump = 0f;
            var chunkChanges = new List<string>();
            var lastChunk = c.Session.OutdoorIndex.ChunkOfCell(c.Session.Cell);
            var notReady = 0;
            Walk(c, WestEnd, () =>
            {
                var p = c.Pixel.WorldCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
                var chunk = c.Session.OutdoorIndex.ChunkOfCell(c.Session.Cell);
                if (chunk.X != lastChunk.X) chunkChanges.Add(lastChunk.X + ">" + chunk.X);
                lastChunk = chunk;
                if (!c.Session.IsCellReady(c.Session.Cell)) notReady++;
            });
            Check("West along the forest road to its West end (-35,-1): over the chunk borders x -16|-17 and -32|-33, still west_area_01, no map load",
                c.Session.Cell == WestEnd && c.Session.CurrentRegionMapId == WestId && c.Session.MapLoads == loads &&
                chunkChanges.SequenceEqual(new[] { "-1>-2", "-2>-3" }), $"{c.Session.Cell} chunks {string.Join(" ", chunkChanges)}");
            Check("the 2D camera moves smoothly all the way (no jump at the borders); the chunk under her always drawn",
                maxJump < 0.12f && notReady == 0, $"max {maxJump:0.000} unit/frame, frames not ready {notReady}");
            // 3D: the same place; back East.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.5f);
            var cam3 = c.Third.ViewCamera.transform.position;
            var her3 = GridSpace3D.ToWorld(c.Session.Cell);
            Check("3D in the forest: the same cell, the camera behind her, near", c.Session.Cell == WestEnd &&
                new Vector2(cam3.x - her3.x, cam3.z - her3.z).magnitude < 6f, $"camera {cam3} her {her3}");
            last = c.Third.ViewCamera.transform.position;
            maxJump = 0f;
            Walk(c, new Vector2Int(-12, 0), () =>
            {
                var p = c.Third.ViewCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
            });
            Check("3D East along the road to (-12,0): the camera follows without a jump", c.Session.Cell == new Vector2Int(-12, 0) && maxJump < 0.2f, $"max {maxJump:0.000}");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.5f);
            last = c.Pixel.WorldCamera.transform.position;
            maxJump = 0f;
            Walk(c, new Vector2Int(-2, 0), () =>
            {
                var p = c.Pixel.WorldCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
            });
            Check("back East into first_field (-2,0): first_field again, no map load, the camera smooth", c.Session.CurrentRegionMapId == "first_field" &&
                c.Session.MapLoads == loads && maxJump < 0.12f, $"{c.Session.Cell} max {maxJump:0.000}");
            Settle(c, 0.5f);
            Check("and the 2D camera is back at first_field's framing", (c.Pixel.WorldCamera.transform.position - before[Vector2Int.zero]).magnitude < 0.6f,
                c.Pixel.WorldCamera.transform.position.ToString("0.00"));
        }

        // ---------------------------------------------------------------- 17.3 the forest

        private static void ForestChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var west = West(c);
            var objects = index.Objects.Where(o => o.RegionMapId == WestId).ToList();
            var byKind = objects.GroupBy(o => o.DefinitionId).ToDictionary(g => g.Key, g => g.Count());
            Check("the forest: 21 trees, 24 bushes, 21 ferns, 8 fallen leaves, 1 stump, 1 green signpost, 4 stones, 3 white flowers",
                byKind.GetValueOrDefault("tree_basic") == 21 && byKind.GetValueOrDefault("forest_bush") == 24 && byKind.GetValueOrDefault("forest_fern") == 21 &&
                byKind.GetValueOrDefault("fallen_leaves") == 8 && byKind.GetValueOrDefault("tree_stump_hollow") == 1 &&
                byKind.GetValueOrDefault("signpost_green") == 1 && byKind.GetValueOrDefault("stone_basic") == 4 && byKind.GetValueOrDefault("flower_basic") == 3,
                string.Join(", ", byKind.Select(p => p.Key + " " + p.Value)));
            Check("Collision: trees, bushes, the stump, the sign and the stones block their cell; ferns and fallen leaves are walkable",
                objects.Where(o => o.DefinitionId is "tree_basic" or "forest_bush" or "tree_stump_hollow" or "signpost_green" or "stone_basic").All(o => !index.CanEnter(o.RootCell)) &&
                objects.Where(o => o.DefinitionId is "forest_fern" or "fallen_leaves").All(o => index.CanEnter(o.RootCell)));
            Check("no tree, bush or stump on the road or the trail", objects.Where(o => o.Definition.BlocksMovement).All(o => index.SurfaceAt(o.RootCell)?.StableId != "dirt"));
            var clearing = new List<Vector2Int>();
            foreach (var (y, x0, x1) in new[] { (2, -28, -25), (3, -29, -25), (4, -29, -25), (5, -28, -26) })
                for (var x = x0; x <= x1; x++) clearing.Add(new Vector2Int(x, y));
            Check("the clearing (こもれびのひろば): 17 cells of open ground round the stump (only the stump blocks)",
                clearing.Count == 17 && clearing.Count(cell => !index.CanEnter(cell)) == 1 && !index.CanEnter(Stump) &&
                clearing.All(cell => cell == Stump || index.CanEnter(cell)));
            var path = V32.ReachableCells(c.Session.OutdoorWorld, new Vector2Int(-11, 0), cell => index.CanEnter(cell));
            Check("the clearing is reached on foot from the road (by the trail from (-24,0))", path.Contains(TouchSpot) && path.Contains(ClearingArrival) &&
                index.SurfaceAt(TrailMouth)?.StableId == "dirt" && index.SurfaceAt(new Vector2Int(-25, 1))?.StableId == "dirt");
            // Tree fade in 3D: a tree right South of her (between her and the camera) fades.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Session.PlaceForCheck(new Vector2Int(-24, -3), FacingDirection.Up);
            c.Third.SnapToTarget();
            Settle(c, 0.6f);
            var tree = c.Presenter.Views.FirstOrDefault(v => v.DefinitionId == "tree_basic" && v.RootCell == new Vector2Int(-24, -4));
            Check("Tree Fade (3D): standing at (-24,-3), the forest tree South of her (between her and the camera) fades",
                tree != null && c.Third.TreeFade.FadingCount > 0 && c.Third.TreeFade.AlphaOf(tree) < 0.99f, tree != null ? c.Third.TreeFade.AlphaOf(tree).ToString("0.00") : "no tree");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            // The light through the leaves.
            c.Session.PlaceForCheck(new Vector2Int(-24, -1), FacingDirection.Left);
            Settle(c, 0.5f);
            var markers = KomorebiMarkers(c);
            Check("the forest has its 8 komorebi Markers (on the road, at the trail and in the clearing; protected cells, all walkable)",
                markers.Count == 8 && markers.All(m => index.CanEnter(m)), string.Join(" ", markers));
            var meshes = KomorebiMeshes();
            var quads = meshes.SelectMany(m => m.quads).ToList();
            Check("its light is drawn (by day): in the forest's chunks, round each Marker", c.Presenter.KomorebiCellsDrawn > 30 && c.Presenter.KomorebiChunks >= 2 &&
                markers.All(m => quads.Any(q => GridSpace3D.ToCell(q) == m)), $"{c.Presenter.KomorebiCellsDrawn} cells in {c.Presenter.KomorebiChunks} chunks, {quads.Count} spots");
            Check("every spot of light lies on walkable ground of the forest within 2 cells of a Marker (never on a tree, bush or the stump), just above the road",
                quads.All(q => { var cell = GridSpace3D.ToCell(q); return index.CanEnter(cell) && InWest(c, cell) && markers.Any(m => Mathf.Max(Mathf.Abs(m.x - cell.x), Mathf.Abs(m.y - cell.y)) <= 2) &&
                    Mathf.Abs(q.y - PocWorldPresenter.KomorebiHeight) < 0.001f; }));
            Check("nowhere else: the other regions have no komorebi Markers, so no light is drawn there",
                index.Regions.Where(r => r.MapId != WestId).All(r => !r.Map.Markers.Any(m => m.StableId.StartsWith(PocWorldPresenter.KomorebiMarkerPrefix, StringComparison.Ordinal))));
            Check("one shared material (the ground's vertex-colour material): the 2D view draws the same meshes as the 3D view, no shadows",
                meshes.All(m => m.renderer.sharedMaterial == AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath).VertexColorMaterial &&
                    m.renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
            var main = meshes.Where(m => !m.renderer.name.Contains("flicker")).ToList();
            var flickerA = meshes.Where(m => m.renderer.name.Contains("flicker A")).Select(m => m.renderer).ToList();
            var flickerB = meshes.Where(m => m.renderer.name.Contains("flicker B")).Select(m => m.renderer).ToList();
            var toggles = 0;
            var alwaysOne = true;
            var previous = flickerA.Count > 0 && flickerA[0].enabled;
            for (var t = 0f; t < 3f; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (flickerA.Count > 0 && flickerA[0].enabled != previous) { toggles++; previous = flickerA[0].enabled; }
                if (flickerA.Count > 0 && flickerB.Count > 0 && flickerA[0].enabled == flickerB[0].enabled) alwaysOne = false;
            }
            Check("by day the light stays on and two small sets take turns (the leaves moving), about every 1.1 s",
                main.All(m => m.renderer.enabled) && flickerA.Count > 0 && flickerB.Count > 0 && alwaysOne && toggles >= 2 && toggles <= 3, $"{toggles} switches in 3 s");
            // Night: the light goes; morning: it comes back.
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 4f);
            Check("at night the light is gone (all its meshes off)", c.Time.NightBlend > 0.99f && !c.Presenter.KomorebiVisible &&
                KomorebiMeshes().All(m => !m.renderer.enabled), $"blend {c.Time.NightBlend:0.00}");
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.5f);
            Check("…in 3D too (the same meshes)", KomorebiMeshes().All(m => !m.renderer.enabled));
            c.Time.Override = PocPhaseOverride.Morning;
            Settle(c, 4f);
            Check("in the morning it comes back", c.Presenter.KomorebiVisible && KomorebiMeshes().Where(m => !m.renderer.name.Contains("flicker")).All(m => m.renderer.enabled));
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 0.3f);
            // AUTO in the forest: it walks, never stuck among the trees, never out of the West area, never finds the clearing.
            c.Session.PlaceForCheck(new Vector2Int(-20, -1), FacingDirection.Left);
            c.Hud.ClearMessage();
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var regions = new HashSet<string>();
            var visited = new HashSet<Vector2Int>();
            var autoSteps = c.Gameplay.AutoWorld.AutoSteps;
            for (var t = 0f; t < 200f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId); visited.Add(c.Session.Cell); }
            Check("AUTO in the forest for 200 s: it walks (many cells), never out of the West area, never stuck",
                c.Gameplay.AutoWorld.AutoSteps - autoSteps > 20 && visited.Count > 10 && regions.SetEquals(new[] { WestId }),
                $"AUTO steps {c.Gameplay.AutoWorld.AutoSteps - autoSteps}, cells {visited.Count}, regions {string.Join(",", regions)}");
            Check("…and AUTO never unlocks the clearing's そらをとぶ place, even when it walks there", !T(c).IsFound(Clearing),
                visited.Any(v => Mathf.Abs(v.x - TouchSpot.x) + Mathf.Abs(v.y - TouchSpot.y) <= 2) ? "AUTO came within 2" : "AUTO did not come near");
            c.Auto.SetAutoEnabled(false, C6.Clock);
        }

        // ---------------------------------------------------------------- 17.4 the stump

        private static void StumpChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var stump = c.Session.OutdoorIndex.Objects.First(o => o.DefinitionId == PocWorldPresenter.StumpDefinitionId);
            var point = stump.Definition.ActionPoints.FirstOrDefault();
            Check("the stump at (-27,4) (local (8,10)); its Action Point touch_stump one cell South (-27,3), facing up, on open ground",
                stump.RootCell == Stump && point != null && point.Id == "touch_stump" && stump.RootCell + point.PlayerCellOffset == TouchSpot &&
                point.PlayerFacing.ToString() == "Up" && c.Session.OutdoorIndex.CanEnter(TouchSpot), point?.Id);
            c.Session.PlaceForCheck(TouchSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Settle(c, 0.3f);
            var bursts = c.Presenter.LeafBurstsStarted;
            var touched = c.Session.InteractAhead();
            Check("A at the stump (きりかぶにふれる): 「はっぱが くるくる まいあがった。」", touched && c.Session.LastMessage == "はっぱが くるくる まいあがった。" &&
                C6.Shown(c) == "はっぱが くるくる まいあがった。" && GameBitmapFont.HasAllGlyphs("はっぱが くるくる まいあがった。"), c.Session.LastMessage);
            Check("leaves fly up from the stump (one burst)", c.Presenter.LeafBurstsStarted == bursts + 1 && c.Presenter.LeavesActive);
            Check("she can move at once (nothing waits for the leaves)", !c.Session.InputSuspended && !c.Menu.BlocksGameplayInput);
            Settle(c, 0.6f);
            var leaves = GameObject.Find("Leaves (きりかぶにふれる)");
            var leafBounds = leaves != null ? leaves.GetComponent<Renderer>().bounds : default;
            Check("mid-way the leaves are up in the air above the stump (higher than half a cell over the cut) and spread round it",
                c.Presenter.LeafHighest > 0.45f && c.Presenter.LeafWidest > 0.2f && leaves != null && leafBounds.max.y > 0.7f &&
                Vector2.Distance(new Vector2(leafBounds.center.x, leafBounds.center.z), new Vector2(GridSpace3D.ToWorld(Stump).x, GridSpace3D.ToWorld(Stump).z)) < 0.3f,
                $"highest {c.Presenter.LeafHighest:0.00} widest {c.Presenter.LeafWidest:0.00} top {leafBounds.max.y:0.00}");
            // Touched again while they fly: nothing new (no second burst, no second object).
            var retriggers = c.Presenter.LeafRetriggersIgnored;
            c.Hud.ClearMessage();
            var again = c.Session.InteractAhead();
            Check("touched again while the leaves fly: the message again, but no second burst (ignored), one leaves object",
                again && c.Presenter.LeafBurstsStarted == bursts + 1 && c.Presenter.LeafRetriggersIgnored == retriggers + 1 &&
                UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(f => f.name.StartsWith("Leaves", StringComparison.Ordinal)) == 1);
            Settle(c, PocWorldPresenter.LeafSeconds);
            Check("after about 1.8 s the leaves have fallen and are gone (the object hidden, its mesh emptied)", !c.Presenter.LeavesActive &&
                (leaves == null || !leaves.activeSelf));
            var frames = 0;
            var before = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            for (var i = 0; i < 6; i++)
            {
                c.Hud.ClearMessage();
                c.Session.InteractAhead();
                Settle(c, PocWorldPresenter.LeafSeconds + 0.1f);
                frames++;
            }
            var after = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Check("six more touches, each after the last fell: six more bursts, the same object reused (no GameObject added)",
                c.Presenter.LeafBurstsStarted == bursts + 7 && after == before && !c.Presenter.LeavesActive, $"objects {before} -> {after}");
            C6.Step(c, Vector2Int.right, 0.4f);
            Check("and she walks on (East in the clearing)", c.Session.Cell == TouchSpot + Vector2Int.right);
            c.Session.PlaceForCheck(TouchSpot + Vector2Int.right, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var b2 = c.Presenter.LeafBurstsStarted;
            Check("from beside the Action Point (another cell) A finds nothing at the stump", !c.Session.InteractAhead() && c.Presenter.LeafBurstsStarted == b2);
            c.Session.PlaceForCheck(TouchSpot + Vector2Int.down, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("nor from one cell further back", !c.Session.InteractAhead() && c.Presenter.LeafBurstsStarted == b2);
            c.Session.PlaceForCheck(TouchSpot, FacingDirection.Left);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("nor on the spot facing away from it", !c.Session.InteractAhead() && c.Presenter.LeafBurstsStarted == b2);
            // 3D, night: the same.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Session.PlaceForCheck(TouchSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Settle(c, 0.5f);
            Check("in 3D the same: the message and a burst of leaves", c.Session.InteractAhead() && c.Presenter.LeafBurstsStarted == b2 + 1 &&
                c.Session.LastMessage == "はっぱが くるくる まいあがった。");
            Settle(c, 2f);
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 4f);
            c.Hud.ClearMessage();
            Check("at night the same (the leaves are lit as the night lights the forest)", c.Session.InteractAhead() && c.Presenter.LeafBurstsStarted == b2 + 2);
            c.Time.Override = PocPhaseOverride.Morning;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 2.5f);
            // A ripple at the pond still works (the leaves are their own).
            var ripples = c.Presenter.RipplesStarted;
            c.Session.PlaceForCheck(new Vector2Int(-4, 15), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("the North pond's みずをのぞく still ripples (and starts no leaves)", c.Session.InteractAhead() && c.Presenter.RipplesStarted == ripples + 1 &&
                c.Presenter.LeafBurstsStarted == b2 + 2);
        }

        // ---------------------------------------------------------------- 17.5 the crow

        private static void CrowChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var crow = c.Crow;
            maxCrows = 0;
            crowBlockedFrames = 0;
            Check("ついてきて", StartFollowing(c));
            Walk(c, new Vector2Int(-9, 0));
            Settle(c, 2f);
            var regions = new HashSet<string>();
            Walk(c, new Vector2Int(-20, -1), () => { if (crow.isActiveAndEnabled) regions.Add(c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(crow.transform.position))?.MapId ?? "-"); });
            Settle(c, 3f);
            Check("the crow follows her West over the region border and the chunk border x -16|-17 (one crow, still following, near her)",
                crow.Following && regions.Contains(WestId) && crow.DistanceToPlayer <= 3 && Crows() == 1 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == WestId, $"{Where(c)} regions {string.Join(",", regions)}");
            // Among the trees: up the trail into the clearing.
            Walk(c, TouchSpot);
            C6.Step(c, Vector2Int.up, 0.3f);   // (into the stump: refused, she faces it)
            Settle(c, 3f);
            Check("into the clearing by the trail: the crow with her, on open ground", crow.Following && crow.DistanceToPlayer <= 3 &&
                c.Session.OutdoorWorld.CanEnter(crow.Cell) && InWest(c, crow.Cell), Where(c));
            Check("the stump can still be touched with the crow beside her (it never stands on her Action Point when she is on it)",
                crow.Cell != TouchSpot && c.Session.InteractAhead());
            var perchedWest = false;
            for (var t = 0f; t < 90f && !perchedWest; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (crow.Activity == CrowActivity.Perched && InWest(c, GridSpace3D.ToCell(crow.transform.position)))
                    perchedWest = true;
            }
            Check("standing in the clearing, the following crow rests on a forest tree near her", perchedWest, crow.Activity.ToString());
            for (var t = 0f; t < 15f && crow.Airborne; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            // A walk among the trees: West end, the far side of the forest, back.
            Walk(c, WestEnd);
            Walk(c, new Vector2Int(-33, 2));
            Walk(c, new Vector2Int(-14, -3));
            Settle(c, 3f);
            Check("a walk among the trees (to the West end, North side, the South-East corner): it kept up on land or flew, never stood in a tree or bush",
                crowBlockedFrames == 0 && crow.DistanceToPlayer <= 4 && crow.Following, $"{Where(c)} blocked frames {crowBlockedFrames}");
            var landingBlocked = 0;
            var choose = typeof(PocCrow).GetMethod("TryChooseGroundLanding", Hidden);
            c.Session.PlaceForCheck(TouchSpot, FacingDirection.Up);
            Settle(c, 0.2f);
            for (var i = 0; i < 60; i++)
            {
                var args = new object[] { default(Vector2Int) };
                if ((bool)choose.Invoke(crow, args) && !c.Session.OutdoorWorld.CanEnter((Vector2Int)args[0])) landingBlocked++;
            }
            Check("60 landing choices in the clearing: all on walkable ground (never a tree, bush or the stump)", landingBlocked == 0);
            Walk(c, new Vector2Int(-5, 0));
            Settle(c, 4f);
            Check("back East in first_field: the crow with her", crow.Following && crow.DistanceToPlayer <= 3 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", Where(c));
            // あそんでおいで in the forest: free again, it flies back to its own trees.
            Walk(c, new Vector2Int(-22, -1));
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
            Check("あそんでおいで in the forest: free again, it flies back to first_field (its own trees)", !crow.Following && home, Where(c));
            Check("one crow the whole time, never inside a blocked cell", maxCrows == 1 && crowBlockedFrames == 0, $"{maxCrows} crows, {crowBlockedFrames} blocked frames");
        }

        // ---------------------------------------------------------------- 17.6 そらをとぶ

        private static void TravelChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var point = t.Registry.Find(Clearing);
            var west = West(c);
            Check("the registry has four places (home, East, North, West: the three of ver3.6 unchanged); the new one: landmark_west_forest_clearing 「にしのこもれび」",
                // ver3.8: more places may follow (the South pinwheel is the fifth); these four come first.
                t.Registry.points.Select(p => p.id).Take(4).SequenceEqual(new[] { Home, Plaza, Pond, Clearing }) && point != null && point.name == "にしのこもれび" &&
                point.mapId == WestId && !point.unlockedAtStart && point.order == 30 && GameBitmapFont.HasAllGlyphs(point.name) &&
                t.Registry.Find(Pond).arrivalCell == new Vector2Int(7, 8) && t.Registry.Find(Plaza).arrivalCell == new Vector2Int(16, 4),
                string.Join(",", t.Registry.points.Select(p => p.id)));
            Check("at the start: home found, the West place not (nor East or North)", t.IsFound(Home) && !t.IsFound(Clearing) && !t.IsFound(Plaza) && !t.IsFound(Pond));
            Check("its places: discovered within 2 of (-27,3) (the clearing's marker), arriving at (-27,2) facing the stump",
                west.ToWorld(point.discoverCell) == TouchSpot && point.discoverRadius == 2 && west.ToWorld(point.arrivalCell) == ClearingArrival &&
                point.ArrivalFacing == FacingDirection.Up && c.Session.OutdoorIndex.CanEnter(ClearingArrival));
            var problems = t.Registry.Validate(c.Session.OutdoorIndex, cell => c.Session.IsTransitionCell(c.Session.OutdoorWorld, cell));
            Check("the four places pass the registry's checks", problems.Count == 0, string.Join("; ", problems));
            Check("not found yet: it cannot be flown to", t.TryBegin(Clearing, C6.Clock, FastTravelSource.Check) == FastTravelStart.NotDiscovered);
            Walk(c, WestEnd);
            Walk(c, new Vector2Int(-12, 0));
            Check("walking the whole forest road there and back (past the clearing, 4 cells from it): not found", !t.IsFound(Clearing), c.Session.Cell.ToString());
            Walk(c, TrailMouth);
            Check("nor at the trail's mouth (-24,0)", !t.IsFound(Clearing));
            var foundAt = new Vector2Int(int.MinValue, 0);
            var message = "";
            t.Discovered += p => { if (p.id == Clearing) { foundAt = c.Session.Cell; message = C6.Shown(c); } };
            Walk(c, TouchSpot);
            Check("up the trail into the clearing: found on the first cell within 2, once, 「そらをとべるばしょを みつけた！」",
                t.IsFound(Clearing) && Mathf.Abs(foundAt.x - TouchSpot.x) + Mathf.Abs(foundAt.y - TouchSpot.y) == 2 && message == PocFastTravel.MessageFound,
                $"found at {foundAt}: {message}");
            Walk(c, TrailMouth);
            c.Hud.ClearMessage();
            Walk(c, TouchSpot);
            Check("walking back into it: no second message", t.Discoveries == 1 && C6.Shown(c) != PocFastTravel.MessageFound);
            // The list of four and the flights.
            t.SetFound(new[] { Plaza, Pond, Clearing });
            c.Hud.ClearMessage();
            c.Menu.SetOpen(true, C6.Clock);
            c.Menu.ActivateLink(c.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
            Check("the list: ハルカチャンの家 / ひがしのはなのひろば / きたのみずべ / にしのこもれび / やめる (one page of four, no つぎへ)",
                Buttons(c) == "ハルカチャンの家/ひがしのはなのひろば/きたのみずべ/にしのこもれび/やめる", Buttons(c));
            var rects = Enumerable.Range(0, c.Menu.Page != null ? c.Menu.Page.Buttons.Count : 0)
                .Select(i => c.Menu.TryGetPageButtonRect(i, out var r) ? r : default).ToList();
            Check("all five buttons are inside the window (1280 x 720) and do not overlap", rects.Count == 5 &&
                rects.All(r => r.width > 0 && r.xMin >= 0 && r.yMin >= 0 && r.xMax <= 1280 && r.yMax <= 720) &&
                rects.Zip(rects.Skip(1), (a, b) => !a.Overlaps(b)).All(x => x), string.Join(" ", rects.Select(r => r.ToString("0"))));
            Press(c, PocFastTravel.CancelLabel);
            Settle(c, 0.2f);
            Check("やめる closes it, she stays", c.Menu.Page == null && c.Session.Cell == TouchSpot);
            c.Menu.SetOpen(false, C6.Clock);
            Check("following for the flights", StartFollowing(c) || c.Crow.Following);
            var anchor = c.Clock.AnchorMilliseconds;
            void Leg(string label, string id, Vector2Int arrival, string region, PocViewMode view)
            {
                c.Switcher.SetMode(view);
                Settle(c, 0.3f);
                var steps = c.Session.CompletedSteps;
                var leaves = c.Presenter.LeafBurstsStarted;
                var started = t.TryBegin(id, C6.Clock, FastTravelSource.Check);
                Fly(c);
                Settle(c, 0.5f);
                Check(label, started == FastTravelStart.Started && c.Session.Cell == arrival && c.Session.CurrentRegionMapId == region &&
                    t.LastArrivalVerified && c.Session.CompletedSteps == steps && c.Crow.Following && c.Crow.DistanceToPlayer <= 3 &&
                    c.Session.OutdoorWorld.CanEnter(c.Crow.Cell) && c.Switcher.Mode == view && c.Presenter.LeafBurstsStarted == leaves &&
                    c.Session.IsCellReady(c.Session.Cell), $"{started} {Where(c)} {t.LastArrivalProblem}");
            }
            c.Session.PlaceForCheck(HomeArrival + Vector2Int.right * 2, FacingDirection.Down);
            Settle(c, 1f);
            Leg("home -> West (2D): (-27,2) in the clearing, facing the stump, no step, the crow beside her on open ground, no leaves by themselves",
                Clearing, ClearingArrival, WestId, PocViewMode.PixelView2D);
            Leg("West -> home (2D)", Home, HomeArrival, "first_field", PocViewMode.PixelView2D);
            Leg("home -> West (3D)", Clearing, ClearingArrival, WestId, PocViewMode.ThirdPerson3D);
            Leg("West -> East (3D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.ThirdPerson3D);
            Leg("East -> West (2D)", Clearing, ClearingArrival, WestId, PocViewMode.PixelView2D);
            Leg("West -> North (2D)", Pond, PondArrival, V36.NorthId, PocViewMode.PixelView2D);
            Leg("North -> West (3D)", Clearing, ClearingArrival, WestId, PocViewMode.ThirdPerson3D);
            Leg("West -> North (3D)", Pond, PondArrival, V36.NorthId, PocViewMode.ThirdPerson3D);
            Leg("North -> East (2D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.PixelView2D);
            Leg("East -> home (2D)", Home, HomeArrival, "first_field", PocViewMode.PixelView2D);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("the day/night clock untouched by the flights", c.Clock.AnchorMilliseconds == anchor);
            Check("a place whose arrival is the stump lands on open ground beside it",
                t.TryFindLanding(new FastTravelPoint { id = "check_stump", mapId = WestId, arrivalCell = west.ToLocal(Stump), landingSearchRadius = 4 }, west, Stump, out var land) &&
                c.Session.OutdoorIndex.CanEnter(land) && land != Stump, land.ToString());
            Check("the arrival does not touch the stump by itself (no leaves until A)", !c.Presenter.LeavesActive);
            // AUTO does not find it (a new game).
            var a = Begin(new HarukaMemoryStorage());
            a.Session.PlaceForCheck(new Vector2Int(-25, 1), FacingDirection.Up);
            Frame(a, 1f / 60f, Vector2Int.zero);
            var s1 = a.Gameplay.AutoWorld.TryStep(Vector2Int.up);
            Settle(a, 0.4f);
            var s2 = a.Gameplay.AutoWorld.TryStep(Vector2Int.left);
            Settle(a, 0.4f);
            Check("AUTO steps into the clearing to (-26,2) (2 from the stump's spot): nothing found", s1 && s2 && a.Session.Cell == new Vector2Int(-26, 2) &&
                !T(a).IsFound(Clearing), a.Session.Cell.ToString());
        }

        // ---------------------------------------------------------------- 17.7 the save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            // Saved in the clearing, following, all four found; closed and opened.
            {
                var storage = new HarukaMemoryStorage();
                var c = Begin(storage);
                T(c).SetFound(new[] { Plaza, Pond });
                StartFollowing(c);
                Walk(c, TouchSpot);
                C6.Step(c, Vector2Int.up, 0.3f);   // (into the stump: refused, she faces it)
                Settle(c, 2f);
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                Settle(c, 1.5f);
                c.Save.OnPageHidden("");
                var latest = C6.Latest(c);
                Check("saved in the clearing: west_area_01 local (8,9), facing up, 3D, following, places east + north + home + west",
                    latest.MapId == WestId && latest.CellX == 8 && latest.CellY == 9 && latest.Facing == "up" && latest.ViewMode == HarukaSaveData.View3D &&
                    latest.CrowMode == HarukaSaveData.CrowFollow && latest.FastTravel != null &&
                    latest.FastTravel.OrderBy(x => x).SequenceEqual(new[] { Plaza, Pond, Home, Clearing }.OrderBy(x => x)), HarukaSaveSystem.Describe(latest));
                Check("the schema is still 1; steps and play time are there", latest.Schema == 1 && latest.TotalSteps > 0, latest.Schema.ToString());
                var r = Begin(storage);
                Check("opened again: the same cell, facing, 3D, the crow following near her, all four places, the chunks drawn first",
                    r.Session.Cell == TouchSpot && r.Session.CurrentRegionMapId == WestId && r.Session.Facing == FacingDirection.Up &&
                    r.Switcher.Mode == PocViewMode.ThirdPerson3D && r.Crow.Following && r.Crow.DistanceToPlayer <= 6 &&
                    r.Session.OutdoorWorld.CanEnter(r.Crow.Cell) && T(r).IsFound(Clearing) && T(r).IsFound(Pond) && T(r).IsFound(Plaza) &&
                    r.Session.IsCellReady(TouchSpot) && r.Session.IsCellReady(Stump), Where(r));
                Check("and she can fly home, East and North from there", T(r).TryBegin(Home, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == HomeArrival && T(r).TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == PlazaArrival && T(r).TryBegin(Pond, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == PondArrival);
                var exported = C6.Sealed(latest);
                var t2 = Begin(new HarukaMemoryStorage());
                Settle(t2, 1.2f);
                var check = t2.Save.CheckImport(exported, out var data);
                var ok = t2.Save.ApplyImport(data, C6.Clock, out var problem);
                Settle(t2, 0.5f);
                Check("export / import of the West save: checked, applied in the clearing with its places and the companion",
                    check == HarukaSaveSystem.ImportCheck.Ok && ok && t2.Session.Cell == TouchSpot && T(t2).IsFound(Clearing) && t2.Crow.Following, problem);
                var unknown = latest.Copy();
                unknown.MapId = "west_area_99";
                Check("a save naming a West area that does not exist is refused (UnknownMap)", t2.Save.CheckImport(C6.Sealed(unknown), out _) == HarukaSaveSystem.ImportCheck.UnknownMap);
                var outside = latest.Copy();
                outside.CellX = 30;
                Check("a cell outside the West Map is refused (OutOfMap)", t2.Save.CheckImport(C6.Sealed(outside), out _) == HarukaSaveSystem.ImportCheck.OutOfMap);
                var tampered = C6.Sealed(latest).Replace("\"" + WestId + "\"", "\"west_area_0X\"");
                Check("a changed file whose checksum no longer matches is refused", t2.Save.CheckImport(tampered, out _) != HarukaSaveSystem.ImportCheck.Ok);
            }
            // A save on the stump's cell (damaged by hand): resumed on the nearest walkable cell of the West area.
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", WestId, new Vector2Int(8, 10));
                data.GameVersion = "ver3.7";
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("a save on the stump's cell: she resumes on the nearest walkable cell of the West area",
                    g.Session.CurrentRegionMapId == WestId && g.Session.OutdoorWorld.CanEnter(g.Session.Cell) && g.Save.LocationNote.Contains("nearest"),
                    $"{g.Session.Cell} {g.Save.LocationNote}");
            }
            // Old saves: every version; the West place not found (normal), the older places kept.
            var places = new (string version, string map, Vector2Int cell, string[] found)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0), null), ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1), null),
                ("ver3.2", "halka_house", new Vector2Int(-2, 0), null), ("ver3.3", V33.EastId, new Vector2Int(9, 6), null),
                ("ver3.4", V33.EastId, new Vector2Int(4, 6), null), ("ver3.5", "first_field", new Vector2Int(-7, -5), new[] { Plaza, Home }),
                ("ver3.6", V36.NorthId, new Vector2Int(6, 8), new[] { Plaza, Home, Pond }),
            };
            foreach (var (version, map, cell, found) in places)
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", map, cell);
                data.GameVersion = version;
                if (found != null) data.FastTravel = new List<string>(found);
                if (version == "ver3.6") data.CrowMode = HarukaSaveData.CrowFollow;
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Settle(g, 0.5f);
                Check($"a {version} save ({map}): resumed there; West not found; " + (found != null ? "its places kept" : "the house"),
                    g.Session.TryGetSavePlace(out var m, out var l) && m == map && l == cell && !T(g).IsFound(Clearing) &&
                    (found == null ? T(g).FoundIds.SequenceEqual(new[] { Home }) : found.All(id => T(g).IsFound(id))) &&
                    (version != "ver3.6" || g.Crow.Following), string.Join(",", T(g).FoundIds));
            }
            // A damaged newest snapshot: the older one (in the clearing) is loaded.
            {
                var storage = new HarukaMemoryStorage();
                var g = Begin(storage);
                Walk(g, TouchSpot);
                Settle(g, 2f);
                g.Save.Write("check", C6.Clock);
                var latest = C6.Latest(g);
                var newest = HarukaSnapshotStore.SlotNames.First(n => HarukaSaveData.TryRead(storage.Read(n), out var d, out _) == HarukaSaveReadStatus.Valid && d.Revision == latest.Revision);
                storage.Write(newest, storage.Read(newest).Replace("\"" + WestId + "\"", "\"west_area_02\""));
                var r = Begin(storage);
                Check("the newest snapshot damaged: the backup snapshot is loaded, West found, in the clearing",
                    r.Save.LoadSource == "backup snapshot" && T(r).IsFound(Clearing) && r.Session.CurrentRegionMapId == WestId, $"{r.Save.LoadSource} {Where(r)}");
            }
            // Rollback: what a build without the West area does with this save.
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", "west_area_00", new Vector2Int(8, 9));
                data.TotalSteps = 4567;
                data.FastTravel = new List<string> { Plaza, Home, Pond, "landmark_south_99" };
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("what an older build does with a West save (its Map unknown to it): the first map's start, steps kept, an unknown place id kept",
                    g.Session.TryGetSavePlace(out var m, out _) && m == "first_field" && g.LifeLog.TotalSteps == 4567 && g.Save.LocationNote.Contains("unknown map") &&
                    T(g).IsFound(Pond) && T(g).IsFound("landmark_south_99") && T(g).FoundPoints().All(p => p.id != "landmark_south_99") &&
                    T(g).SavedIds().Contains("landmark_south_99"),
                    g.Save.LocationNote);
            }
        }

        // ---------------------------------------------------------------- 17.8 chunk streaming

        private static void ChunkChecks()
        {
            C6.SetLegacy(null, null);
            // The official world: walking from one end to the other frees what is far behind and draws it
            // again on the way back, with the same Objects, the light, the water.
            var c = Begin(new HarukaMemoryStorage());
            var streamer = c.Presenter.Streamer;
            c.Session.PlaceForCheck(Vector2Int.zero, FacingDirection.Left);
            Settle(c, 1f);
            var all = c.Session.OutdoorIndex.Chunks.Count;
            var atStart = streamer.Stats.Loaded;
            Check($"the official world has {all} chunks (16 x 16): at the start every one is drawn (the views reach 40 cells), none failed",
                atStart == all && streamer.Stats.Failed == 0 && streamer.Stats.FallbackChunks == 0, $"loaded {atStart} of {all}");
            var objectsAll = c.Presenter.LoadedChunkObjects;
            var meshesAtStart = RuntimeMeshes();
            var namesAtStart = RuntimeMeshNames();
            var waterAll = c.Presenter.WaterCellsDrawn;
            var lightAll = c.Presenter.KomorebiCellsDrawn;
            var unloads = streamer.Stats.TotalUnloads;
            Walk(c, WestEnd);
            Settle(c, 1f);
            var eastChunk = c.Session.OutdoorIndex.ChunkOfCell(new Vector2Int(35, 0));
            Check("at the forest's West end (-35,-1): the East area's far chunk (x 32..47) has been freed by the streamer as she walked (not by a check)",
                streamer.Stats.TotalUnloads > unloads && streamer.StateOf(eastChunk) == ChunkState.Unloaded && !c.Presenter.IsChunkDrawn(eastChunk),
                V32.ChunkSummary(c));
            var duringWest = streamer.Stats.Loaded;
            Walk(c, PlazaArrival);
            Settle(c, 1f);
            var westChunk = c.Session.OutdoorIndex.ChunkOfCell(WestEnd);
            Check("walking to the East plaza (27,-2): it was drawn again on the way, and the forest's far chunk (x -48..-33) freed",
                streamer.StateOf(eastChunk) == ChunkState.Ready && streamer.Records[eastChunk].Loads >= 2 && streamer.StateOf(westChunk) == ChunkState.Unloaded,
                V32.ChunkSummary(c));
            Check("…its light freed with it (fewer light cells drawn now)", c.Presenter.KomorebiCellsDrawn < lightAll, $"{c.Presenter.KomorebiCellsDrawn} < {lightAll}");
            Walk(c, new Vector2Int(-20, -1));
            Settle(c, 1f);
            Walk(c, Vector2Int.zero);
            Settle(c, 1.5f);
            Check("back to the start: every chunk drawn again, the same Objects (none twice), the same water and light as at first",
                streamer.Stats.Loaded == all && c.Presenter.LoadedChunkObjects == objectsAll && V32.DuplicateViews(c).Count == 0 &&
                c.Presenter.WaterCellsDrawn == waterAll && c.Presenter.KomorebiCellsDrawn == lightAll && streamer.Records[westChunk].Loads >= 2,
                $"{V32.ChunkSummary(c)} objects {c.Presenter.LoadedChunkObjects}/{objectsAll} water {c.Presenter.WaterCellsDrawn} light {c.Presenter.KomorebiCellsDrawn}");
            Check("no chunk was ever built in a hurry by the step gate (the prefetch was in time) and none failed",
                streamer.Stats.SyncLoads == 0 && streamer.Stats.TotalFailures == 0, V32.ChunkSummary(c));
            Check("the chunks drawn never exceeded the world's (no growth without bound)", streamer.Records.Count <= all + 4, streamer.Records.Count.ToString());

            // Meshes made at run time (not assets): the same state before and after the round trip must hold the
            // same number (a freed chunk's meshes are destroyed with it, including each Object's outline copy).
            Check("runtime meshes: back at the start with the same chunks drawn, as many as at first (nothing a freed chunk made is left behind)",
                RuntimeMeshes() == meshesAtStart, $"{meshesAtStart} -> {RuntimeMeshes()}; left: " + LeftBehind(namesAtStart));
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
            Check("into the house and out again three times: as many runtime meshes as before (the rooms' and the outdoor world's are freed when the map changes)",
                c.Session.World == c.Session.OutdoorWorld && RuntimeMeshes() == meshesOutside, $"{meshesOutside} -> {RuntimeMeshes()}; left: " + LeftBehind(namesOutside));
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var models = catalog.Bindings.SelectMany(b => new[] { b.model }.Concat(b.variants ?? new List<GameObject>())).Where(m => m != null).ToList();
            Check("the catalog's models are untouched by the freeing (every model mesh still there, an asset)",
                models.Count > 20 && models.All(m => m.GetComponentsInChildren<MeshFilter>(true).All(f => f.sharedMesh != null && AssetDatabase.Contains(f.sharedMesh))),
                models.Count + " models");

            // The ver3.2 256 x 256 test world, the same streamer as the game: a long walk East and back.
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
            var startBlocked = new List<Vector2Int>();
            var rect = WorldGrid.ChunkRect(startChunk, 16);
            for (var y = rect.Min.y; y <= rect.Max.y; y++)
            for (var x = rect.Min.x; x <= rect.Max.x; x++)
                if (!w.Session.OutdoorWorld.CanEnter(new Vector2Int(x, y))) startBlocked.Add(new Vector2Int(x, y));
            var loadedAtStart = ws.Stats.Loaded;
            var objectsAtStart = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var outsideAtStart = objectsAtStart - ChunkTransforms();
            var monoAtStart = GC.GetTotalMemory(true);
            var crowCell = w.Crow.Cell;
            var crowMode = w.Crow.Mode;
            var maxLoaded = 0;
            var maxReady = 0;
            var loadedOnTheWay = new List<int>();
            var u0 = ws.Stats.TotalUnloads;
            var l0 = ws.Stats.TotalLoads;
            var goal = V32.NearestWalkable(w, start + new Vector2Int(110, 0));
            var walked = V32.WalkTo(w, goal, _ =>
            {
                maxLoaded = Mathf.Max(maxLoaded, ws.Stats.Loaded);
                maxReady = Mathf.Max(maxReady, w.Presenter.ReadyChunks().Count());
                loadedOnTheWay.Add(ws.Stats.Loaded);
            }, 0.14f);
            V32.Settle(w, 1f);
            Check($"256 x 256 test world: she walks 110 cells East ({start} -> {w.Session.Cell}); new chunks were loaded ahead as she went",
                walked && ws.Stats.TotalLoads > l0 + 4, V32.ChunkSummary(w));
            Check("…and the chunks of the start, far behind, were freed by the streamer itself (automatic unloads)",
                ws.Stats.TotalUnloads > u0 && ws.StateOf(startChunk) == ChunkState.Unloaded && !w.Presenter.IsChunkDrawn(startChunk), V32.ChunkSummary(w));
            // The view reaches 40 cells each way (+ 8 ahead while walking): at most 7 x 7 chunks required, and
            // with the one-chunk hysteresis ring at most 9 x 9 kept. And it does not creep up along the way.
            var third = Mathf.Max(1, loadedOnTheWay.Count / 3);
            var firstThird = loadedOnTheWay.Take(third).DefaultIfEmpty(0).Max();
            var lastThird = loadedOnTheWay.Skip(loadedOnTheWay.Count - third).DefaultIfEmpty(0).Max();
            Check("the number of drawn chunks stayed bounded on the way (at most the 9 x 9 keep area) and did not grow along the walk",
                maxLoaded <= 81 && maxReady == maxLoaded && lastThird <= firstThird + 6,
                $"start {loadedAtStart}, max {maxLoaded}, first third max {firstThird}, last third max {lastThird}");
            var midObjects = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var back = V32.WalkTo(w, start, null, 0.14f);
            V32.Settle(w, 1.5f);
            var againViews = w.Presenter.Views.Where(v => !v.IsWall && w.Session.OutdoorIndex.ChunkOfCell(v.RootCell) == startChunk).Select(v => v.InstanceId).OrderBy(x => x).ToList();
            var againBlocked = new List<Vector2Int>();
            for (var y = rect.Min.y; y <= rect.Max.y; y++)
            for (var x = rect.Min.x; x <= rect.Max.x; x++)
                if (!w.Session.OutdoorWorld.CanEnter(new Vector2Int(x, y))) againBlocked.Add(new Vector2Int(x, y));
            Check("back at the start: its chunk drawn again (loaded a second time) with exactly the same Objects, none twice",
                back && ws.StateOf(startChunk) == ChunkState.Ready && ws.Records[startChunk].Loads >= 2 && againViews.SequenceEqual(startViews) &&
                V32.DuplicateViews(w).Count == 0, $"{startViews.Count} views, again {againViews.Count}");
            Check("…its Collision the same (the logical world is never streamed)", againBlocked.SequenceEqual(startBlocked), $"{startBlocked.Count} blocked cells");
            var objectsBack = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var monoBack = GC.GetTotalMemory(true);
            var roots = V32.ChunkRoots(w);
            var ready = w.Presenter.ReadyChunks().ToList();
            Check("nothing of a freed chunk is left: one chunk root per drawn chunk, every drawn Object in a drawn chunk",
                roots == ready.Count && w.Presenter.Views.Where(v => !v.IsWall).All(v => w.Presenter.IsChunkDrawn(w.Session.OutdoorIndex.ChunkOfCell(v.RootCell)) ||
                    w.Session.OutdoorIndex.Chunks.Values.Any(k => k.OwnedObjects.Any(o => o.InstanceId == v.InstanceId) && w.Presenter.IsChunkDrawn(k.Coord))),
                $"chunk roots {roots}, drawn chunks {ready.Count}");
            Check("the GameObjects outside the drawn chunks are the same as at the start (nothing piles up); those in them follow the drawn chunks",
                objectsBack - ChunkTransforms() == outsideAtStart,
                $"all {objectsAtStart} -> far East {midObjects} -> back {objectsBack}; outside the chunks {outsideAtStart} -> {objectsBack - ChunkTransforms()}; " +
                $"in the chunks {objectsAtStart - outsideAtStart} for {loadedAtStart} chunks -> {ChunkTransforms()} for {ready.Count}");
            Check("managed memory does not grow with the walk (within 8 MB after a collection)", monoBack - monoAtStart < 8L * 1024 * 1024,
                $"{monoAtStart / 1048576f:0.0} MB -> {monoBack / 1048576f:0.0} MB");
            Check("the crow's logical state did not change with the chunks (its cell and mode kept)", w.Crow.Mode == crowMode &&
                (w.Crow.Cell == crowCell || w.Session.OutdoorWorld.CanEnter(w.Crow.Cell)), $"{crowCell} -> {w.Crow.Cell}");
            w.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            V32.Settle(w, 0.5f);
            var loadsBeforeSwitch = ws.Stats.TotalLoads;
            for (var i = 0; i < 6; i++) { w.Switcher.Toggle(); V32.Frame(w, 1f / 60f, Vector2Int.zero); }
            Check("2D / 3D switches after the walk: nothing built or freed by a switch", ws.Stats.TotalLoads == loadsBeforeSwitch, V32.ChunkSummary(w));
            Check("summary: loads " + ws.Stats.TotalLoads + ", unloads " + ws.Stats.TotalUnloads + ", chunks loaded more than once " +
                ws.Records.Values.Count(r => r.Loads > 1) + ", average load " + ws.Stats.AverageLoadMs.ToString("0.0") + " ms",
                ws.Stats.TotalUnloads > 0 && ws.Records.Values.Count(r => r.Loads > 1) > 0);
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
            Check("and out again (the forest's light and leaves rebuilt with the outdoor world)", c.Session.World == c.Session.OutdoorWorld &&
                c.Session.Cell == door + Vector2Int.down && c.Presenter.KomorebiCellsDrawn > 0);
            var north = c.Session.OutdoorIndex.FindRegion(V36.NorthId);
            Check("the North area as it was: 21 x 20 cells, its 22 water cells drawn, the pond's marker",
                north.Rect.Min == new Vector2Int(-10, 7) && north.Rect.Max == new Vector2Int(10, 26) && north.Map.TryGetMarker(Pond, out var pm) &&
                pm == new Vector2Int(6, 8) && c.Presenter.WaterCellsDrawn == 22);
            var east = c.Session.OutdoorIndex.FindRegion(V33.EastId);
            Check("the East area as it was: its 26 x 13 cells, the plaza's marker", east.Rect.Min == new Vector2Int(11, -6) && east.Rect.Max == new Vector2Int(36, 6) &&
                east.Map.TryGetMarker(Plaza, out var m) && m == new Vector2Int(16, 4));
            Walk(c, PlazaArrival);
            Check("the plaza is still found by walking there", T(c).IsFound(Plaza));
            Walk(c, new Vector2Int(-3, 15));
            Check("the pond is still found by walking there", T(c).IsFound(Pond));
            // AUTO in first_field next to the West road: never into the forest by itself.
            c.Session.PlaceForCheck(new Vector2Int(-8, 0), FacingDirection.Left);
            c.Hud.ClearMessage();
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var regions = new HashSet<string>();
            for (var t = 0f; t < 150f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId ?? c.Session.World.MapId); }
            Check("AUTO in first_field next to the West road for 150 s: it walks, but never into the West area", c.Gameplay.AutoWorld.AutoSteps > 0 &&
                !regions.Contains(WestId), string.Join(",", regions));
            c.Auto.SetAutoEnabled(false, C6.Clock);
            Check("the menu and its rows (そらをとぶ, セーブデータ) are there", c.Menu.LinkLabels.Contains(PocFastTravel.MenuLabel) &&
                c.Menu.LinkLabels.Contains(HarukaSaveSystem.MenuLabel));
            Check("the Life Log counts steps as before", c.LifeLog.TotalSteps > 0);
        }

        // ---------------------------------------------------------------- what must not change

        private static void ProtectChecks()
        {
            Check("WORLD EDITOR v1.0 unchanged since ver3.6 (tools/HalkaWorldMapEditor)", V32.Git("diff --quiet " + Published + " -- tools/HalkaWorldMapEditor", out _));
            Check("first_field, east_area_01, the house and the booth Maps (Authoring JSON and caches) unchanged", V36.OfficialMapsKept(Published));
            Check("north_area_01 (Authoring JSON and cache) unchanged", V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/Maps/Authoring/north_area_01.hwmap.json unity-project/Assets/Content/Maps/north_area_01.asset", out _));
            Check("the Maps folder: nothing changed or removed; the catalog and the world registry only added to", V36.MapsFolderKept(Published));
            Check("direction_themes.json unchanged; the world registry only added to (the three regions' lines as they were)", V36.RegistryKept(Published));
            Check("the そらをとぶ places: only added to (the three of ver3.6 unchanged)", V36.OnlyAdded(Published, "unity-project/Assets/Content/FastTravel/fast_travel_points.json"));
            Check("the published ver3.6 (halkaworld/) unchanged", V32.Git("diff --quiet " + Published + " -- halkaworld", out _));
            Check("the crow's sprites, model and voice, and the font atlas unchanged", V36.CrowKept(Published, true));
            Check("the save system and the crow's code unchanged (no Save Schema change)",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/Save unity-project/Assets/POC2D3D/Runtime/PocCrow.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Companion.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Travel.cs unity-project/Assets/Scripts/World/CrowBehaviourCore.cs", out _));
            Check("the chunk streamer unchanged (World/ChunkStreamer.cs): verified as it is, not rewritten",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/World/ChunkStreamer.cs", out _));
            Check("the old tests are all still there (none deleted)", V32.Git("diff --quiet --diff-filter=D " + Published + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _));
        }
    }
}
