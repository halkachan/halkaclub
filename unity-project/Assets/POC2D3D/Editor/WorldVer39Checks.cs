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
using V38 = Halka.Poc2D3D.EditorTools.WorldVer38Checks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.9「斜め4方向の中間エリア追加・9Regionワールド化」: the checks of the four transition regions
    // between the North, East, South and West areas (the registry: nine regions in the same world rectangle; the
    // eight new borders crossed on foot both ways; every walkable cell of the nine reached on the real collision;
    // the round walks that never pass first_field; the 2D camera that keeps every ver3.8 place's framing; the crow
    // over the new borders; そらをとぶ still five places, flown to from the new regions; the save in each new region),
    // of the chunks streaming round the eight directions with no mesh left behind, and of what must not change.
    // The game is started with the Phase 6 harness (memory storage only); every frame runs what Update does in play.
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer39Checks.RunBatch
    public static class WorldVer39Checks
    {
        public const string NorthWestId = "northwest_transition_01";
        public const string NorthEastId = "northeast_transition_01";
        public const string SouthWestId = "southwest_transition_01";
        public const string SouthEastId = "southeast_transition_01";
        public static readonly string[] NewIds = { NorthWestId, NorthEastId, SouthWestId, SouthEastId };
        private static readonly string[] OldIds = { "first_field", V33.EastId, V36.NorthId, V37.WestId, V38.SouthId };
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Home = "landmark_halka_home";
        private const string Plaza = "landmark_east_flower_plaza";
        private const string Pond = "landmark_north_blue_pond";
        private const string Clearing = "landmark_west_forest_clearing";
        private const string Pinwheel = "landmark_south_red_pinwheel";
        private const string Published = "812ea2f";   // ver3.8 (with ver3.7) as published (2026-10-30): this version's base
        private static readonly Vector2Int HomeArrival = new Vector2Int(-7, -5);
        private static readonly Vector2Int PlazaArrival = new Vector2Int(27, -2);
        private static readonly Vector2Int PondArrival = new Vector2Int(-3, 15);
        private static readonly Vector2Int ClearingArrival = new Vector2Int(-27, 2);
        private static readonly Vector2Int HillArrival = new Vector2Int(-3, -20);
        private static readonly Vector2Int SouthRoad = new Vector2Int(2, -20);
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // A new region: its World rectangle, its direction and place name, and the two trail ends on each border.
        private sealed class Spec
        {
            public string Id, Direction, Name, Display, NeighbourA, NeighbourB, MarkerA, MarkerB;
            public Vector2Int Offset, Min, Max, OutA, OutB;   // OutA / OutB: from the trail end over the border
            public Vector2Int Middle;                         // a walkable cell on its first trail, well inside
        }

        private static readonly Spec[] Specs =
        {
            new Spec { Id = NorthWestId, Direction = "northwest", Name = "きたにし", Display = "北西エリア", Offset = new Vector2Int(-35, 7),
                Min = new Vector2Int(-35, 7), Max = new Vector2Int(-11, 26), NeighbourA = V37.WestId, NeighbourB = V36.NorthId,
                MarkerA = "trail_a_west", MarkerB = "trail_a_north", OutA = Vector2Int.down, OutB = Vector2Int.right, Middle = new Vector2Int(-15, 11) },
            new Spec { Id = NorthEastId, Direction = "northeast", Name = "きたひがし", Display = "北東エリア", Offset = new Vector2Int(11, 7),
                Min = new Vector2Int(11, 7), Max = new Vector2Int(36, 26), NeighbourA = V33.EastId, NeighbourB = V36.NorthId,
                MarkerA = "trail_a_east", MarkerB = "trail_a_north", OutA = Vector2Int.down, OutB = Vector2Int.left, Middle = new Vector2Int(18, 11) },
            new Spec { Id = SouthWestId, Direction = "southwest", Name = "みなみにし", Display = "南西エリア", Offset = new Vector2Int(-35, -26),
                Min = new Vector2Int(-35, -26), Max = new Vector2Int(-11, -7), NeighbourA = V37.WestId, NeighbourB = V38.SouthId,
                MarkerA = "trail_a_west", MarkerB = "trail_a_south", OutA = Vector2Int.up, OutB = Vector2Int.right, Middle = new Vector2Int(-16, -11) },
            new Spec { Id = SouthEastId, Direction = "southeast", Name = "みなみひがし", Display = "南東エリア", Offset = new Vector2Int(11, -26),
                Min = new Vector2Int(11, -26), Max = new Vector2Int(36, -7), NeighbourA = V33.EastId, NeighbourB = V38.SouthId,
                MarkerA = "trail_a_east", MarkerB = "trail_a_south", OutA = Vector2Int.up, OutB = Vector2Int.left, Middle = new Vector2Int(17, -12) },
        };

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
            UnityEngine.Random.InitState(3939);
            try
            {
                Run("world", WorldChecks);
                Run("connect", ConnectChecks);
                Run("reach", ReachChecks);
                Run("camera", CameraChecks);
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
            foreach (var failure in failures) Debug.LogError("VER39 FAIL " + failure);
            Debug.Log("HARUKA ver3.9 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.9 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V39_LOGS", "ver39_checks.txt");
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
            if (condition) { passes++; Debug.Log("VER39 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness (as WorldVer38Checks)

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

        // Walks there holding the keys (a shortest way over the real collision); onFrame sees every frame.
        private static bool Walk(C6.Ctx c, Vector2Int goal, Action onFrame = null, float maxSeconds = 160f)
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

        // One step held on the keys (as a player would), the camera watched every frame; the largest camera move per frame.
        private static float StepWatched(C6.Ctx c, Vector2Int direction, bool threeD)
        {
            Camera Cam() => threeD ? c.Third.ViewCamera : c.Pixel.WorldCamera;
            var last = Cam().transform.position;
            var max = 0f;
            var from = c.Session.Cell;
            for (var i = 0; i < 120 && c.Session.Cell == from && !c.Session.IsMoving; i++)
            {
                Frame(c, 1f / 60f, V32.Raw(c, direction));
                max = Mathf.Max(max, (Cam().transform.position - last).magnitude);
                last = Cam().transform.position;
            }
            for (var i = 0; i < 120 && c.Session.IsMoving; i++)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                max = Mathf.Max(max, (Cam().transform.position - last).magnitude);
                last = Cam().transform.position;
            }
            C6.Release(c);
            for (var i = 0; i < 20; i++)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                max = Mathf.Max(max, (Cam().transform.position - last).magnitude);
                last = Cam().transform.position;
            }
            return max;
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

        private static bool OnScreen(Vector3 p) => p.x > 16f && p.x < 1264f && p.y > 16f && p.y < 704f;

        private static Vector2Int TrailEnd(C6.Ctx c, Spec spec, string marker)
        {
            var region = c.Session.OutdoorIndex.FindRegion(spec.Id);
            return region.Map.TryGetMarker(marker, out var local) ? region.ToWorld(local) : new Vector2Int(int.MinValue, 0);
        }

        // The ver3.8 world: the five regions alone (what the camera, the dressing and an older build had).
        private static OutdoorWorldDefinition Ver38World(out MapDefinition primary)
        {
            primary = AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath);
            var full = PocSceneBuilder.LoadOutdoorWorld(primary);
            return new OutdoorWorldDefinition
            {
                worldId = full.worldId, chunkSize = full.chunkSize, themes = full.themes, formatVersion = full.formatVersion,
                regions = full.regions.Where(r => !NewIds.Contains(r.mapId)).ToList()
            };
        }

        // Regions passed in order (repeats of the same one in a row counted once).
        private static List<string> Passed(List<string> seen) =>
            seen.Where((r, i) => i == 0 || seen[i - 1] != r).ToList();

        private static HashSet<Vector2Int> Reach(Vector2Int origin, Func<Vector2Int, bool> pass)
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

        private static int RuntimeMeshes() =>
            Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0);

        private static int OrphanMeshes()
        {
            var used = new HashSet<Mesh>(UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(f => f.sharedMesh).Where(m => m != null));
            return Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0 && !used.Contains(m));
        }

        private static Dictionary<string, int> RuntimeMeshNames() =>
            Resources.FindObjectsOfTypeAll<Mesh>().Where(m => m != null && !AssetDatabase.Contains(m) && (m.hideFlags & HideFlags.HideAndDontSave) == 0)
                .GroupBy(m => System.Text.RegularExpressions.Regex.Replace(m.name, @"[-0-9, ()]+$", "").Trim()).ToDictionary(g => g.Key, g => g.Count());

        private static string LeftBehind(Dictionary<string, int> before) =>
            string.Join(", ", RuntimeMeshNames().Where(p => p.Value > before.GetValueOrDefault(p.Key)).OrderByDescending(p => p.Value - before.GetValueOrDefault(p.Key))
                .Take(12).Select(p => p.Key + " +" + (p.Value - before.GetValueOrDefault(p.Key))));

        private static int AllTransforms() => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private static int ChunkTransforms() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Chunk (", StringComparison.Ordinal)).Sum(t => t.GetComponentsInChildren<Transform>(true).Length);

        // ---------------------------------------------------------------- 20.1 the world

        private static void WorldChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var definition = index.Definition;
            Check("the registry: nine regions, the five of ver3.8 first (in their order), then the four transition regions",
                index.Regions.Select(r => r.MapId).SequenceEqual(OldIds.Concat(NewIds)), string.Join(",", index.Regions.Select(r => r.MapId)));
            var old = new (string id, Vector2Int min, Vector2Int max)[]
            {
                ("first_field", new Vector2Int(-10, -6), new Vector2Int(10, 6)), (V33.EastId, new Vector2Int(11, -6), new Vector2Int(36, 6)),
                (V36.NorthId, new Vector2Int(-10, 7), new Vector2Int(10, 26)), (V37.WestId, new Vector2Int(-35, -6), new Vector2Int(-11, 6)),
                (V38.SouthId, new Vector2Int(-10, -26), new Vector2Int(10, -7)),
            };
            Check("the five regions of ver3.8 where they were, the same size and direction",
                old.All(o => index.FindRegion(o.id).Rect.Min == o.min && index.FindRegion(o.id).Rect.Max == o.max) &&
                index.FindRegion("first_field").Direction == "center" && index.FindRegion(V33.EastId).Direction == "east" && index.FindRegion(V36.NorthId).Direction == "north" &&
                index.FindRegion(V37.WestId).Direction == "west" && index.FindRegion(V38.SouthId).Direction == "south");
            foreach (var s in Specs)
            {
                var r = index.FindRegion(s.Id);
                Check($"{s.Id}: direction {s.Direction}, World offset {s.Offset}, {s.Max.x - s.Min.x + 1} x {s.Max.y - s.Min.y + 1} cells: World {s.Min}..{s.Max}",
                    r != null && r.Direction == s.Direction && r.Offset == s.Offset && r.Rect.Min == s.Min && r.Rect.Max == s.Max &&
                    r.Map.MapType == "outdoor" && r.Map.DisplayName == s.Display, r != null ? $"{r.Offset} {r.Rect.Min}..{r.Rect.Max} {r.Map.DisplayName}" : "missing");
                var theme = definition.ThemeOf(s.Direction);
                Check($"…its place name in the save menu: {s.Name} (its own theme, not one of its neighbours' names); the font has every letter",
                    theme != null && c.Save.PlaceName(s.Id) == s.Name && GameBitmapFont.HasAllGlyphs(s.Name) &&
                    c.Save.PlaceName(s.Id) != c.Save.PlaceName(s.NeighbourA) && c.Save.PlaceName(s.Id) != c.Save.PlaceName(s.NeighbourB), c.Save.PlaceName(s.Id));
                var rules = MapPlacementRules.Validate(r.Map);
                Check($"…the game's own Map rules find nothing wrong in it (authored with WORLD EDITOR's Core: MapRules 0 issues)", !rules.Any(p => p.StartsWith("ERROR")), string.Join("; ", rules));
                Check($"…no landmark (no sunflower: the East plaza's own), no Interaction, no sign, no house or water of its own: plants, stones and rocks of the existing catalog only, no Surface",
                    r.Map.Surfaces.Count == 0 && r.Map.Objects.All(o => o.Definition != null && o.Definition.ActionPoints.All(p => p.ActionType == WorldActionType.Examine) &&
                        new[] { "tree_basic", "forest_bush", "forest_fern", "fallen_leaves", "flower_blue", "flower_yellow", "flower_basic",
                            "pond_reeds", "stone_basic", "south_rock_red", "south_rock_small", "south_grass_short", "south_ground_accent" }.Contains(o.Definition.StableId)) &&
                    r.Map.Markers.All(m => m.StableId.StartsWith("trail_", StringComparison.Ordinal)),
                    string.Join(",", r.Map.Objects.Select(o => o.Definition.StableId).Distinct()));
            }
            Check("the four diagonal themes: each the average of its two neighbours' colours; the five old themes as they were",
                ColorUtility.ToHtmlStringRGB(definition.ThemeOf("northwest").color) == "649184" && ColorUtility.ToHtmlStringRGB(definition.ThemeOf("northeast").color) == "A6A47A" &&
                ColorUtility.ToHtmlStringRGB(definition.ThemeOf("southwest").color) == "9B7A51" && ColorUtility.ToHtmlStringRGB(definition.ThemeOf("southeast").color) == "DD8D47" &&
                ColorUtility.ToHtmlStringRGB(definition.ThemeOf("north").color) == "5B84B1" && ColorUtility.ToHtmlStringRGB(definition.ThemeOf("east").color) == "F2C443" &&
                ColorUtility.ToHtmlStringRGB(definition.ThemeOf("south").color) == "C8564B" && ColorUtility.ToHtmlStringRGB(definition.ThemeOf("west").color) == "6E9F57" &&
                definition.ThemeOf("center").name == "まんなか" && definition.ThemeOf("north").name == "きた" && definition.ThemeOf("south").name == "みなみ");
            var problems = definition.Validate();
            Check("the world passes its checks: no region overlaps another", !OutdoorWorldDefinition.HasErrors(problems), string.Join("; ", problems));
            var covered = new HashSet<Vector2Int>();
            var twice = 0;
            foreach (var r in index.Regions)
                for (var y = r.Rect.Min.y; y <= r.Rect.Max.y; y++)
                for (var x = r.Rect.Min.x; x <= r.Rect.Max.x; x++)
                    if (!covered.Add(new Vector2Int(x, y))) twice++;
            Check("the nine regions tile the world's rectangle (-35,-26)..(36,26) exactly: 72 x 53 = 3816 cells, none twice, no gap (the empty corners are filled)",
                index.Bounds.Min == new Vector2Int(-35, -26) && index.Bounds.Max == new Vector2Int(36, 26) && covered.Count == 72 * 53 && twice == 0, $"{covered.Count} cells, {twice} twice");
            Check("the world's rectangle as in ver3.8 (it does not grow): outside it nothing is walkable",
                !index.CanEnter(new Vector2Int(-36, 10)) && !index.CanEnter(new Vector2Int(37, 10)) && !index.CanEnter(new Vector2Int(-20, 27)) && !index.CanEnter(new Vector2Int(20, -27)) &&
                index.RegionAt(new Vector2Int(-36, 10)) == null && index.RegionAt(new Vector2Int(20, -27)) == null);
            Check("negative cells: Map-local and World both ways (North-West local (0,0) = (-35,7), South-West local (24,19) = (-11,-7), South-East local (25,0) = (36,-26))",
                index.FindRegion(NorthWestId).ToWorld(Vector2Int.zero) == new Vector2Int(-35, 7) && index.FindRegion(SouthWestId).ToWorld(new Vector2Int(24, 19)) == new Vector2Int(-11, -7) &&
                index.FindRegion(SouthEastId).ToWorld(new Vector2Int(25, 0)) == new Vector2Int(36, -26) && index.FindRegion(SouthWestId).ToLocal(new Vector2Int(-35, -26)) == Vector2Int.zero &&
                index.RegionAt(new Vector2Int(-11, 7))?.MapId == NorthWestId && index.RegionAt(new Vector2Int(-10, 7))?.MapId == V36.NorthId &&
                index.RegionAt(new Vector2Int(-11, 6))?.MapId == V37.WestId && index.RegionAt(new Vector2Int(11, -7))?.MapId == SouthEastId &&
                index.RegionAt(new Vector2Int(10, -7))?.MapId == V38.SouthId && index.RegionAt(new Vector2Int(11, -6))?.MapId == V33.EastId);
            Check("chunks (floor division): (-33,8) in chunk (-3,0), (-32,8) in (-2,0); (-17,-17) in (-2,-2), (-16,-16) in (-1,-1); (31,15) in (1,0), (32,16) in (2,1)",
                index.ChunkOfCell(new Vector2Int(-33, 8)) == new ChunkCoord(-3, 0) && index.ChunkOfCell(new Vector2Int(-32, 8)) == new ChunkCoord(-2, 0) &&
                index.ChunkOfCell(new Vector2Int(-17, -17)) == new ChunkCoord(-2, -2) && index.ChunkOfCell(new Vector2Int(-16, -16)) == new ChunkCoord(-1, -1) &&
                index.ChunkOfCell(new Vector2Int(31, 15)) == new ChunkCoord(1, 0) && index.ChunkOfCell(new Vector2Int(32, 16)) == new ChunkCoord(2, 1));
            Check("the chunk table: 36 chunks as in ver3.8 (the rectangle is the same), every one dressed; the corner chunks now hold region cells",
                index.Chunks.Count == 36 && index.Chunks.Values.All(k => k.HasDecoration) &&
                index.ChunkAt(new ChunkCoord(-3, 1)).RegionCells > 0 && index.ChunkAt(new ChunkCoord(2, 1)).RegionCells > 0 &&
                index.ChunkAt(new ChunkCoord(-3, -2)).RegionCells > 0 && index.ChunkAt(new ChunkCoord(2, -2)).RegionCells > 0, $"{index.Chunks.Count} chunks");
            var objects = index.Objects.Where(o => NewIds.Contains(o.RegionMapId)).ToList();
            Check("Objects of the four: " + objects.Count + " (" + string.Join(", ", NewIds.Select(id => id.Split('_')[0] + " " + objects.Count(o => o.RegionMapId == id))) +
                "), their instance ids unique in the world", objects.Count > 150 && objects.Count < 320 && index.Objects.Select(o => o.InstanceId).Distinct().Count() == index.Objects.Count);
            Check("no そらをとぶ place in a transition region (still the five of ver3.8)", T(c).Registry.points.Count == 5 && T(c).Registry.points.All(p => !NewIds.Contains(p.mapId)));
        }

        // ---------------------------------------------------------------- 20.2 the eight new borders, both ways

        private static void ConnectChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var crossings = 0;
            foreach (var s in Specs)
            foreach (var (marker, outward, neighbour) in new[] { (s.MarkerA, s.OutA, s.NeighbourA), (s.MarkerB, s.OutB, s.NeighbourB) })
            {
                var inside = TrailEnd(c, s, marker);
                var outside = inside + outward;
                for (var way = 0; way < 2; way++)
                {
                    var threeD = (crossings & 1) == 1;
                    c.Switcher.SetMode(threeD ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D);
                    var from = way == 0 ? outside : inside;
                    var to = way == 0 ? inside : outside;
                    var dir = to - from;
                    // Two cells back, so she walks over the border on her own steps.
                    var back = from - dir;
                    if (!index.CanEnter(back)) back = from;
                    c.Session.PlaceForCheck(back, FacingDirectionExtensions.FromVector(dir));
                    if (threeD) c.Third.SnapToTarget(); else c.Pixel.SnapToTarget();
                    Settle(c, 0.5f);
                    if (back != from) StepWatched(c, dir, threeD);
                    var steps = c.Session.CompletedSteps;
                    var life = c.LifeLog.TotalSteps;
                    var loads = c.Session.MapLoads;
                    var changes = c.Session.RegionChanges;
                    var auto = c.Auto.AutoEnabled;
                    var jump = StepWatched(c, dir, threeD);
                    var expect = way == 0 ? s.Id : neighbour;
                    Check($"{(way == 0 ? neighbour + " -> " + s.Id : s.Id + " -> " + neighbour)} ({(threeD ? "3D" : "2D")}): {from} -> {to}, one step over the border, " +
                        "facing kept, one region change, no map load, the camera without a jump",
                        c.Session.Cell == to && c.Session.CurrentRegionMapId == expect && c.Session.Facing == FacingDirectionExtensions.FromVector(dir) &&
                        c.Session.CompletedSteps == steps + 1 && c.LifeLog.TotalSteps == life + 1 && c.Session.MapLoads == loads && c.Session.RegionChanges == changes + 1 &&
                        c.Auto.AutoEnabled == auto && c.Session.World == c.Session.OutdoorWorld && jump < (threeD ? 0.2f : 0.12f) && Crows() == 1,
                        $"{c.Session.Cell} {c.Session.CurrentRegionMapId} steps +{c.Session.CompletedSteps - steps} changes +{c.Session.RegionChanges - changes} jump {jump:0.000}");
                    crossings++;
                }
            }
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("16 crossings walked (8 borders, both ways)", crossings == 16);
            // The trails through each region: from one neighbour's border to the other's, never leaving the region.
            foreach (var s in Specs)
            foreach (var letter in new[] { "a", "b" })
            {
                var a = TrailEnd(c, s, $"trail_{letter}_{s.NeighbourA.Split('_')[0]}");
                var b = TrailEnd(c, s, $"trail_{letter}_{s.NeighbourB.Split('_')[0]}");
                var region = index.FindRegion(s.Id);
                var path = Reach(a, cell => index.CanEnter(cell) && region.ContainsWorld(cell));
                Check($"{s.Id}: its trail {letter} joins the {s.NeighbourA} border {a} to the {s.NeighbourB} border {b} inside the region, nothing blocking on it",
                    region.ContainsWorld(a) && region.ContainsWorld(b) && path.Contains(b) && index.CanEnter(a) && index.CanEnter(b), $"{a} -> {b}");
            }
            // Walk one trail of each on foot.
            foreach (var s in Specs)
            {
                var a = TrailEnd(c, s, s.MarkerA);
                var b = TrailEnd(c, s, s.MarkerB);
                c.Session.PlaceForCheck(a + s.OutA, FacingDirectionExtensions.FromVector(-s.OutA));
                c.Pixel.SnapToTarget();
                Settle(c, 0.3f);
                var regions = new List<string>();
                var walked = Walk(c, b + s.OutB, () => regions.Add(c.Session.CurrentRegionMapId));
                Check($"on foot from {s.NeighbourA} through {s.Id} to {s.NeighbourB}: {string.Join(" > ", Passed(regions))}",
                    walked && Passed(regions).SequenceEqual(new[] { s.NeighbourA, s.Id, s.NeighbourB }), $"{c.Session.Cell}");
            }
        }

        // ---------------------------------------------------------------- 20.3 every cell reached; the round walks

        private static void ReachChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var start = c.Session.Cell;
            var reach = Reach(start, cell => index.CanEnter(cell));
            var walkable = new Dictionary<string, int>();
            var shut = new List<Vector2Int>();
            foreach (var r in index.Regions)
                for (var y = r.Rect.Min.y; y <= r.Rect.Max.y; y++)
                for (var x = r.Rect.Min.x; x <= r.Rect.Max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!index.CanEnter(cell)) continue;
                    walkable[r.MapId] = walkable.GetValueOrDefault(r.MapId) + 1;
                    if (!reach.Contains(cell)) shut.Add(cell);
                }
            Check($"every walkable cell of the nine regions is reached on foot from the start {start} (breadth first over the game's collision): {reach.Count} cells, none shut in",
                shut.Count == 0 && reach.Count == walkable.Values.Sum(), string.Join(", ", walkable.Select(p => p.Key + " " + p.Value)) + (shut.Count > 0 ? " shut " + string.Join(" ", shut.Take(8)) : ""));
            Check("the five ver3.8 regions keep their walkable cells (first_field 235, East 290, North 356, West 271, South 373)",
                walkable["first_field"] == 235 && walkable[V33.EastId] == 290 && walkable[V36.NorthId] == 356 && walkable[V37.WestId] == 271 && walkable[V38.SouthId] == 373,
                string.Join(", ", OldIds.Select(id => id + " " + walkable.GetValueOrDefault(id))));
            foreach (var s in Specs)
            {
                var region = index.FindRegion(s.Id);
                var own = Reach(TrailEnd(c, s, s.MarkerA), cell => index.CanEnter(cell) && region.ContainsWorld(cell));
                Check($"{s.Id}: every one of its {walkable[s.Id]} walkable cells reached without leaving it (no pocket); blocking Objects block, walkable ones do not",
                    own.Count == walkable[s.Id] && walkable[s.Id] > 400 &&
                    index.Objects.Where(o => o.RegionMapId == s.Id).All(o => index.CanEnter(o.RootCell) == !o.Definition.BlocksMovement),
                    $"{own.Count} of {walkable[s.Id]}");
                var crossingsA = 0;
                var crossingsB = 0;
                for (var y = s.Min.y; y <= s.Max.y; y++)
                for (var x = s.Min.x; x <= s.Max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!index.CanEnter(cell)) continue;
                    foreach (var d in Dirs)
                    {
                        var n = index.RegionAt(cell + d)?.MapId;
                        if (n == null || !index.CanEnter(cell + d)) continue;
                        if (n == s.NeighbourA) crossingsA++;
                        if (n == s.NeighbourB) crossingsB++;
                    }
                }
                Check($"…its borders are open in many places, not one gap: {crossingsA} crossings to {s.NeighbourA}, {crossingsB} to {s.NeighbourB}",
                    crossingsA >= 8 && crossingsB >= 8);
                Check($"…it touches first_field only at a corner (no shared edge): the transition regions are reached from the four areas",
                    !Enumerable.Range(s.Min.x, s.Max.x - s.Min.x + 1).Any(x => index.RegionAt(new Vector2Int(x, s.Min.y - 1))?.MapId == "first_field" ||
                        index.RegionAt(new Vector2Int(x, s.Max.y + 1))?.MapId == "first_field"));
            }
            // The four round walks of the spec (21.1-21.4), on foot, through the transition region (never back by first_field).
            var routes = new (string label, Vector2Int start, Vector2Int via, Vector2Int to, string[] expect)[]
            {
                ("North -> North-West -> West -> the field", PondArrival, new Vector2Int(-25, 15), ClearingArrival, new[] { V36.NorthId, NorthWestId, V37.WestId, "first_field" }),
                ("North -> North-East -> East -> the field", PondArrival, new Vector2Int(22, 16), PlazaArrival, new[] { V36.NorthId, NorthEastId, V33.EastId, "first_field" }),
                ("South -> South-West -> West -> the field", SouthRoad, new Vector2Int(-24, -17), ClearingArrival, new[] { V38.SouthId, SouthWestId, V37.WestId, "first_field" }),
                ("South -> South-East -> East -> the field", SouthRoad, new Vector2Int(22, -18), PlazaArrival, new[] { V38.SouthId, SouthEastId, V33.EastId, "first_field" }),
            };
            foreach (var (label, from, via, to, expect) in routes)
            {
                c.Session.PlaceForCheck(from, FacingDirection.Down);
                c.Pixel.SnapToTarget();
                Settle(c, 0.5f);
                var regions = new List<string> { c.Session.CurrentRegionMapId };
                var loads = c.Session.MapLoads;
                var steps = c.Session.CompletedSteps;
                var cells = 0;
                var lastCell = c.Session.Cell;
                void Track()
                {
                    regions.Add(c.Session.CurrentRegionMapId);
                    if (c.Session.Cell != lastCell) { cells++; lastCell = c.Session.Cell; }
                }
                var ok = Walk(c, via, Track) && Walk(c, to, Track) && Walk(c, new Vector2Int(-2, 0), Track);
                Check($"{label}: {string.Join(" > ", Passed(regions))}; no map load, one step per cell ({cells})",
                    ok && Passed(regions).SequenceEqual(expect) && c.Session.MapLoads == loads && c.Session.CompletedSteps - steps == cells, c.Session.Cell.ToString());
            }
            // All nine on one walk (21.5): the field, then round the eight directions and back.
            c.Session.PlaceForCheck(new Vector2Int(-2, 0), FacingDirection.Up);
            Settle(c, 0.3f);
            var tour = new[] { new Vector2Int(0, 16), new Vector2Int(22, 16), new Vector2Int(27, -2), new Vector2Int(22, -18), SouthRoad,
                new Vector2Int(-24, -17), new Vector2Int(-27, 2), new Vector2Int(-25, 15), new Vector2Int(-5, 22), new Vector2Int(-2, 0) };
            var visited = new HashSet<string>();
            var all = tour.All(goal => Walk(c, goal, () => visited.Add(c.Session.CurrentRegionMapId)));
            Check("one walk through all nine regions (field, N, NE, E, SE, S, SW, W, NW, N, field): every leg reached, no map load",
                all && visited.SetEquals(OldIds.Concat(NewIds)) && c.Session.World == c.Session.OutdoorWorld, string.Join(",", visited));
        }

        // ---------------------------------------------------------------- 20.4 the cameras and the ground

        private static void CameraChecks()
        {
            C6.SetLegacy(null, null);
            // The 2D camera at the places of ver3.8: exactly where it was in the ver3.8 world (the five regions alone).
            var cells = new[] { Vector2Int.zero, new Vector2Int(2, -6), new Vector2Int(-9, -5), new Vector2Int(9, 5), new Vector2Int(-10, 6), new Vector2Int(10, -6),
                new Vector2Int(20, 0), new Vector2Int(30, -3), new Vector2Int(36, 6), new Vector2Int(0, 16), new Vector2Int(-10, 26), new Vector2Int(10, 20),
                new Vector2Int(-16, 0), new Vector2Int(-35, -1), new Vector2Int(-27, 3), new Vector2Int(3, -26), new Vector2Int(-10, -20), new Vector2Int(10, -10), HillArrival };
            var storage = new HarukaMemoryStorage();
            var o = C6.Start(storage, null, s => { var w = Ver38World(out var p); s.UseOutdoorWorldForTest(w, p, false); });
            Check("the comparison world: the five regions of ver3.8", o.Session.OutdoorIndex.Regions.Select(r => r.MapId).SequenceEqual(OldIds));
            var before = cells.ToDictionary(cell => cell, cell => CameraFor(o, cell));
            o.Presenter.ReachCells3D = 1000;
            Settle(o, 1f);
            var tilesBefore = o.Presenter.Environment.GrassTiles;
            var loadedBefore = o.Presenter.Streamer.Stats.Loaded;
            var c = Begin(new HarukaMemoryStorage());
            var moved = cells.Where(cell => (CameraFor(c, cell) - before[cell]).magnitude > 0.005f).ToList();
            Check($"2D camera at {cells.Length} places of the five ver3.8 regions (the field, its edges and corners, East, North, West, South): exactly where it was",
                moved.Count == 0, string.Join(" ", moved.Select(m => m + " " + CameraFor(c, m).ToString("0.00") + " was " + before[m].ToString("0.00"))));
            var startCam = CameraFor(c, Vector2Int.zero);
            Check("…and at the start (0,0) as since ver3.6: (0.00, 22.98, -19.28)", (startCam - new Vector3(0f, 22.98f, -19.28f)).magnitude < 0.02f, startCam.ToString("0.00"));
            // The corners and the middles of the new regions: she is on screen, in 2D and in 3D.
            var corners = new[] { new Vector2Int(-35, 26), new Vector2Int(36, 26), new Vector2Int(-35, -26), new Vector2Int(36, -26),
                new Vector2Int(-23, 16), new Vector2Int(23, 16), new Vector2Int(-23, -16), new Vector2Int(23, -16), new Vector2Int(-11, 7), new Vector2Int(11, -7) };
            var off2 = new List<string>();
            foreach (var cell in corners)
            {
                var stand = V32.NearestWalkable(c, cell);
                CameraFor(c, stand);
                var p = c.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(stand));
                if (!OnScreen(p)) off2.Add(stand + " " + p.ToString("0"));
            }
            Check("2D at the world's four corners and the middles of the four new regions: she is on screen (the camera stops at the world's edge)",
                off2.Count == 0, string.Join("; ", off2));
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            var off3 = new List<string>();
            foreach (var cell in corners)
            {
                var stand = V32.NearestWalkable(c, cell);
                c.Session.PlaceForCheck(stand, FacingDirection.Up);
                c.Third.SnapToTarget();
                Settle(c, 0.3f);
                var cam = c.Third.ViewCamera.transform.position;
                var her = GridSpace3D.ToWorld(stand);
                var p = c.Third.ViewCamera.WorldToScreenPoint(her + Vector3.up * 0.5f);
                if (new Vector2(cam.x - her.x, cam.z - her.z).magnitude > 6f || p.z <= 0f || !OnScreen(p)) off3.Add(stand + " " + p.ToString("0"));
            }
            Check("3D at the same places: the camera behind her, near, she on screen", off3.Count == 0, string.Join("; ", off3));
            // Tree Fade in the North-West forest: a tree right South of her (between her and the camera) fades.
            var index = c.Session.OutdoorIndex;
            var tree = index.Objects.Where(x => x.RegionMapId == NorthWestId && x.DefinitionId == "tree_basic" && index.CanEnter(x.RootCell + Vector2Int.up))
                .OrderBy(x => x.RootCell.y).FirstOrDefault();
            if (tree != null)
            {
                c.Session.PlaceForCheck(tree.RootCell + Vector2Int.up, FacingDirection.Up);
                c.Third.SnapToTarget();
                Settle(c, 0.6f);
                var view = c.Presenter.FindView(tree.InstanceId);
                Check($"Tree Fade (3D) in the North-West: standing North of the tree at {tree.RootCell}, it fades (she is not hidden)",
                    view != null && c.Third.TreeFade.FadingCount > 0 && c.Third.TreeFade.AlphaOf(view) < 0.99f, view != null ? c.Third.TreeFade.AlphaOf(view).ToString("0.00") : "no view");
            }
            else Check("a North-West tree to stand behind", false);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            // The ground: drawn once per cell; the former corners are region ground now, not the outside tint.
            c.Presenter.ReachCells3D = 1000;
            c.Session.PlaceForCheck(Vector2Int.zero, FacingDirection.Down);
            Settle(c, 1f);
            var dressed = 0;
            foreach (var chunk in c.Presenter.ReadyChunks())
            {
                var rect = WorldGrid.ChunkRect(chunk, index.ChunkSize);
                for (var y = rect.Min.y; y <= rect.Max.y; y++)
                for (var x = rect.Min.x; x <= rect.Max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (index.RegionAt(cell) != null || index.DecorationRegionAt(cell) != null) dressed++;
                }
            }
            var tiles = c.Presenter.Environment.GrassTiles;
            Check("ground: one grass tile per dressed cell of the drawn chunks (none twice where the background grass met a region), as many as in ver3.8 (the corners were dressed already)",
                tiles == dressed && tiles == tilesBefore && c.Presenter.Streamer.Stats.Loaded == loadedBefore, $"tiles {tiles}, cells {dressed}, ver3.8 tiles {tilesBefore}");
            var tufts = c.Presenter.Environment.TuftPositions;
            Check("grass tufts only on region cells that grow grass (none on a blocking Object, none outside the regions)",
                tufts.Count > 0 && tufts.All(p => index.HasGrass(GridSpace3D.ToCell(p))), $"{tufts.Count} tufts");
            var dressedRect = index.Bounds.Expanded(index.DecorationMargin);
            var undressed = 0;
            for (var y = dressedRect.Min.y; y <= dressedRect.Max.y; y++)
            for (var x = dressedRect.Min.x; x <= dressedRect.Max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var chunk = index.ChunkAt(WorldGrid.ChunkOf(cell, index.ChunkSize));
                if (index.RegionAt(cell) == null && index.DecorationRegionAt(cell) == null || chunk == null || !chunk.HasDecoration) undressed++;
            }
            Check("no black gap: every cell of the world's rectangle and its 8-cell margin has ground (ver3.8's fix kept)", undressed == 0, undressed.ToString());
            Check("the former corners are region cells now (walkable where nothing stands), not background: (-20,20) (20,20) (-20,-20) (20,-20)",
                new[] { new Vector2Int(-20, 20), new Vector2Int(20, 20), new Vector2Int(-20, -20), new Vector2Int(20, -20) }.All(cell => NewIds.Contains(index.RegionAt(cell)?.MapId)) &&
                index.DecorationRegionAt(new Vector2Int(-40, 30)) != null && index.RegionAt(new Vector2Int(-40, 30)) == null && !index.CanEnter(new Vector2Int(-40, 30)));
            // 2D / 3D switches in each new region: the same cell, facing, region, crow, time of day; nothing rebuilt.
            foreach (var s in Specs)
            {
                c.Session.PlaceForCheck(s.Middle, FacingDirection.Left);
                c.Pixel.SnapToTarget();
                Settle(c, 0.5f);
                var loads = c.Presenter.Streamer.Stats.TotalLoads;
                var phase = c.Clock.CurrentPhase;
                for (var i = 0; i < 4; i++) { c.Switcher.Toggle(); Settle(c, 0.2f); }
                Check($"{s.Id}: 2D / 3D switched four times at {s.Middle}: the same cell, facing, region and time of day, nothing loaded",
                    c.Session.Cell == s.Middle && c.Session.Facing == FacingDirection.Left && c.Session.CurrentRegionMapId == s.Id && c.Clock.CurrentPhase == phase &&
                    c.Presenter.Streamer.Stats.TotalLoads == loads && c.Switcher.Mode == PocViewMode.PixelView2D, $"{c.Session.Cell} {c.Session.CurrentRegionMapId}");
            }
        }

        // ---------------------------------------------------------------- 20.5 the crow

        private static void CrowChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var crow = c.Crow;
            maxCrows = 0;
            crowBlockedFrames = 0;
            Check("ついてきて", StartFollowing(c));
            var legs = new (string label, Vector2Int[] goals, string[] through)[]
            {
                ("North -> North-West -> West", new[] { new Vector2Int(0, 12), new Vector2Int(-25, 15), new Vector2Int(-25, 2) }, new[] { V36.NorthId, NorthWestId, V37.WestId }),
                ("West -> South-West -> South", new[] { new Vector2Int(-24, -17), new Vector2Int(-5, -24) }, new[] { SouthWestId, V38.SouthId }),
                ("South -> South-East -> East", new[] { new Vector2Int(22, -18), new Vector2Int(25, -3) }, new[] { SouthEastId, V33.EastId }),
                ("East -> North-East -> North", new[] { new Vector2Int(22, 16), new Vector2Int(2, 20) }, new[] { NorthEastId, V36.NorthId }),
            };
            foreach (var (label, goals, through) in legs)
            {
                var crowRegions = new HashSet<string>();
                var maxDistance = 0;
                var ok = goals.All(g => Walk(c, V32.NearestWalkable(c, g), () =>
                {
                    if (!crow.isActiveAndEnabled) return;
                    crowRegions.Add(c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(crow.transform.position))?.MapId ?? "-");
                    if (!crow.Airborne) maxDistance = Mathf.Max(maxDistance, crow.DistanceToPlayer);
                }));
                Settle(c, 3f);
                for (var t = 0f; t < 12f && (crow.Airborne || crow.DistanceToPlayer > 3); t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
                crowRegions.Add(c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId ?? "-");   // (where it came down after the walk)
                Check($"{label}, the crow following: it went with her through {string.Join(", ", through)} and is near her at the end (one crow, never in a tree, a rock or a bush)",
                    ok && crow.Following && through.All(crowRegions.Contains) && crow.DistanceToPlayer <= 3 && Crows() == 1 && crowBlockedFrames == 0,
                    $"{Where(c)} crow regions {string.Join(",", crowRegions)} blocked {crowBlockedFrames}");
            }
            // Among the North-West trees and the South-West rocks: landings on open ground only.
            foreach (var (id, spot) in new[] { (NorthWestId, new Vector2Int(-28, 12)), (SouthWestId, new Vector2Int(-20, -20)) })
            {
                Walk(c, V32.NearestWalkable(c, spot));
                Settle(c, 3f);
                var blocked = 0;
                var inside = 0;
                var choose = typeof(PocCrow).GetMethod("TryChooseGroundLanding", Hidden);
                for (var i = 0; i < 60; i++)
                {
                    var args = new object[] { default(Vector2Int) };
                    if (!(bool)choose.Invoke(crow, args)) continue;
                    var cell = (Vector2Int)args[0];
                    if (!c.Session.OutdoorWorld.CanEnter(cell)) blocked++;
                    if (c.Session.OutdoorIndex.RegionAt(cell)?.MapId == id) inside++;
                }
                Check($"60 landing choices in {id}: all on walkable ground (never a tree, a bush, a rock or a flower)", blocked == 0 && inside > 0, $"{inside} inside the region");
                Settle(c, 15f);
                Check($"…waiting there 15 s: the crow stays near her, never inside a blocked cell", crow.Following && crow.DistanceToPlayer <= 4 && crowBlockedFrames == 0, Where(c));
            }
            // 2D / 3D in a new region: the crow's state kept.
            var mode = crow.Mode;
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 1f);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 1f);
            Check("2D / 3D switches in the South-West: the crow still following, the same one", crow.Following && crow.Mode == mode && Crows() == 1);
            // あそんでおいで in the North-East: free again, it flies back to first_field (its own trees), as before.
            Walk(c, new Vector2Int(22, 16));
            Settle(c, 3f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            var home = false;
            for (var t = 0f; t < 45f && !home; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (!crow.Airborne && c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field" ||
                    crow.Activity == CrowActivity.Perched && c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(crow.transform.position))?.MapId == "first_field") home = true;
            }
            Check("あそんでおいで in the North-East: free again, it flies back to first_field (its home is not moved)", !crow.Following && home, Where(c));
            Check("one crow the whole time, never inside a blocked cell", maxCrows == 1 && crowBlockedFrames == 0, $"{maxCrows} crows, {crowBlockedFrames} blocked frames");
            // Into the house with the crow following from a transition region: it waits outside.
            Check("ついてきて again", StartFollowing(c));
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Walk(c, door + Vector2Int.down);
            V32.Step(c, Vector2Int.up, 0.4f);
            Settle(c, 1f);
            Check("into the house: the crow does not come in (one crow, outside)", c.Session.World.MapId == "halka_house" && Crows() == 1 &&
                (!crow.isActiveAndEnabled || c.Session.OutdoorIndex.RegionAt(crow.Cell) != null), Where(c));
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Settle(c, 3f);
            for (var t = 0f; t < 12f && (crow.Airborne || crow.DistanceToPlayer > 4); t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("and out again: following, near her", c.Session.World == c.Session.OutdoorWorld && crow.Following && crow.DistanceToPlayer <= 4, Where(c));
        }

        // ---------------------------------------------------------------- 20.6 そらをとぶ

        private static void TravelChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            Check("the registry: the same five places as ver3.8 (home, East, North, West, South), none in a transition region",
                t.Registry.points.Select(p => p.id).SequenceEqual(new[] { Home, Plaza, Pond, Clearing, Pinwheel }) && t.Registry.points.All(p => !NewIds.Contains(p.mapId)));
            var problems = t.Registry.Validate(c.Session.OutdoorIndex, cell => c.Session.IsTransitionCell(c.Session.OutdoorWorld, cell));
            Check("the five places pass the registry's checks in the nine-region world", problems.Count == 0, string.Join("; ", problems));
            // Walking all four transition regions (their trails end to end) finds nothing.
            foreach (var s in Specs)
            {
                Walk(c, TrailEnd(c, s, s.MarkerA));
                Walk(c, TrailEnd(c, s, s.MarkerB));
                Walk(c, TrailEnd(c, s, "trail_b_" + s.NeighbourA.Split('_')[0]));
                Walk(c, TrailEnd(c, s, "trail_b_" + s.NeighbourB.Split('_')[0]));
            }
            Check("walking all four transition regions (both trails of each, end to end, and the ways between): no place found (only home), no message",
                t.FoundIds.SequenceEqual(new[] { Home }) && t.Discoveries == 0, string.Join(",", t.FoundIds));
            // From each transition region to each of the five places, following, 2D and 3D, by the menu (two pages).
            t.SetFound(new[] { Plaza, Pond, Clearing, Pinwheel });
            Check("following for the flights", StartFollowing(c) || c.Crow.Following);
            var targets = new (string id, Vector2Int arrival, string region)[]
            {
                (Home, HomeArrival, "first_field"), (Plaza, PlazaArrival, V33.EastId), (Pond, PondArrival, V36.NorthId),
                (Clearing, ClearingArrival, V37.WestId), (Pinwheel, HillArrival, V38.SouthId),
            };
            var n = 0;
            foreach (var s in Specs)
            foreach (var (id, arrival, region) in targets)
            {
                var view = (n++ & 1) == 0 ? PocViewMode.PixelView2D : PocViewMode.ThirdPerson3D;
                c.Switcher.SetMode(view);
                c.Session.PlaceForCheck(s.Middle, FacingDirection.Down);
                Settle(c, 2f);
                for (var w = 0f; w < 10f && (c.Crow.Airborne || c.Crow.DistanceToPlayer > 3); w += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
                var steps = c.Session.CompletedSteps;
                c.Menu.SetOpen(true, C6.Clock);
                c.Menu.ActivateLink(c.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
                var pages = Buttons(c);
                var name = t.Registry.Find(id).name;
                if (!c.Menu.Page.Buttons.Contains(name)) Press(c, PocFastTravel.NextLabel);
                var started = Press(c, name) && t.Busy;
                Fly(c);
                Settle(c, 0.5f);
                Check($"{s.Id} -> {name} ({(view == PocViewMode.PixelView2D ? "2D" : "3D")}, by the menu): {arrival} in {region}, no step, the crow following beside her, the view kept",
                    started && c.Session.Cell == arrival && c.Session.CurrentRegionMapId == region && t.LastArrivalVerified && c.Session.CompletedSteps == steps &&
                    c.Crow.Following && c.Crow.DistanceToPlayer <= 3 && c.Session.OutdoorWorld.CanEnter(c.Crow.Cell) && c.Switcher.Mode == view && Crows() == 1 &&
                    pages == "ハルカチャンの家/ひがしのはなのひろば/きたのみずべ/にしのこもれび/つぎへ/やめる",
                    $"{Where(c)} {t.LastArrivalProblem} list {pages}");
            }
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("the found places kept through the flights (the five, nothing new)", t.FoundIds.OrderBy(x => x).SequenceEqual(new[] { Home, Plaza, Pond, Clearing, Pinwheel }.OrderBy(x => x)));
            // AUTO in a transition region finds nothing and never leaves it.
            var a = Begin(new HarukaMemoryStorage());
            a.Session.PlaceForCheck(Specs[0].Middle, FacingDirection.Down);
            a.Auto.SetAutoEnabled(true, C6.Clock);
            var regions = new HashSet<string>();
            for (var s = 0f; s < 120f; s += 1f / 60f) { Frame(a, 1f / 60f, Vector2Int.zero, true); regions.Add(a.Session.CurrentRegionMapId); }
            a.Auto.SetAutoEnabled(false, C6.Clock);
            Check("AUTO in the North-West for 120 s: it walks, stays in the North-West, finds no place", a.Gameplay.AutoWorld.AutoSteps > 10 &&
                regions.SetEquals(new[] { NorthWestId }) && T(a).FoundIds.SequenceEqual(new[] { Home }), $"{a.Gameplay.AutoWorld.AutoSteps} steps, {string.Join(",", regions)}");
        }

        // ---------------------------------------------------------------- 20.7 the save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            var n = 0;
            foreach (var s in Specs)
            {
                var storage = new HarukaMemoryStorage();
                var c = Begin(storage);
                T(c).SetFound(new[] { Plaza, Pond });
                StartFollowing(c);
                Walk(c, s.Middle);
                C6.Step(c, Vector2Int.left, 0.3f);
                var cell = c.Session.Cell;
                var facing = c.Session.Facing;
                var threeD = (n++ & 1) == 1;
                c.Switcher.SetMode(threeD ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D);
                Settle(c, 2f);
                var steps = c.LifeLog.TotalSteps;
                var phase = c.Clock.CurrentPhase;
                c.Save.OnPageHidden("");
                var latest = C6.Latest(c);
                var local = c.Session.OutdoorIndex.FindRegion(s.Id).ToLocal(cell);
                Check($"saved in {s.Id}: its Map ID and Map-local cell {local}, facing, {(threeD ? "3D" : "2D")}, following, the places found (schema 1)",
                    latest.Schema == 1 && latest.MapId == s.Id && latest.CellX == local.x && latest.CellY == local.y && latest.Facing == facing.ToString().ToLowerInvariant() &&
                    latest.ViewMode == (threeD ? HarukaSaveData.View3D : HarukaSaveData.View2D) && latest.CrowMode == HarukaSaveData.CrowFollow &&
                    latest.FastTravel.OrderBy(x => x).SequenceEqual(new[] { Home, Plaza, Pond }.OrderBy(x => x)) && latest.TotalSteps == steps, HarukaSaveSystem.Describe(latest));
                var r = Begin(storage);
                Check($"…closed and opened: the same cell and facing in {s.Id}, the view, the crow following near her, the places, the steps, the time of day; the chunk drawn first",
                    r.Session.Cell == cell && r.Session.CurrentRegionMapId == s.Id && r.Session.Facing == facing &&
                    r.Switcher.Mode == (threeD ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D) && r.Crow.Following && r.Crow.DistanceToPlayer <= 6 &&
                    r.Session.OutdoorWorld.CanEnter(r.Crow.Cell) && T(r).IsFound(Plaza) && T(r).IsFound(Pond) && !T(r).IsFound(Pinwheel) &&
                    r.LifeLog.TotalSteps == steps && r.Clock.CurrentPhase == phase && r.Session.IsCellReady(cell), Where(r));
                Check($"…the save menu names it {s.Name}", r.Save.PlaceName(latest.MapId) == s.Name, r.Save.PlaceName(latest.MapId));
                var exported = C6.Sealed(latest);
                var t2 = Begin(new HarukaMemoryStorage());
                Settle(t2, 1.2f);
                var check = t2.Save.CheckImport(exported, out var data);
                var ok = t2.Save.ApplyImport(data, C6.Clock, out var problem);
                Settle(t2, 0.5f);
                Check($"…export / import: checked (checksum), applied in {s.Id} at the same cell with the places and the companion",
                    check == HarukaSaveSystem.ImportCheck.Ok && ok && t2.Session.Cell == cell && t2.Session.CurrentRegionMapId == s.Id && T(t2).IsFound(Pond) && t2.Crow.Following, problem);
                var outside = latest.Copy();
                outside.CellY = 20;
                Check($"…a cell outside its Map is refused (OutOfMap); a changed file is refused", t2.Save.CheckImport(C6.Sealed(outside), out _) == HarukaSaveSystem.ImportCheck.OutOfMap &&
                    t2.Save.CheckImport(exported.Replace("\"" + s.Id + "\"", "\"" + s.Id.Replace("01", "0X") + "\""), out _) != HarukaSaveSystem.ImportCheck.Ok);
            }
            // A save on a blocking Object's cell of a transition region: the nearest walkable cell of that region.
            {
                var probe = Begin(new HarukaMemoryStorage());
                var tree = probe.Session.OutdoorIndex.Objects.First(o => o.RegionMapId == SouthWestId && o.Definition.BlocksMovement);
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", SouthWestId, tree.LocalRootCell);
                data.GameVersion = "ver3.9";
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("a save on a tree's cell in the South-West: she resumes on the nearest walkable cell of the South-West",
                    g.Session.CurrentRegionMapId == SouthWestId && g.Session.OutdoorWorld.CanEnter(g.Session.Cell) && g.Save.LocationNote.Contains("nearest"),
                    $"{g.Session.Cell} {g.Save.LocationNote}");
            }
            // Old saves: every version up to ver3.8; their places and the crow kept.
            var places = new (string version, string map, Vector2Int cell, string[] found)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0), null), ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1), null),
                ("ver3.2", "halka_house", new Vector2Int(-2, 0), null), ("ver3.3", V33.EastId, new Vector2Int(9, 6), null),
                ("ver3.4", V33.EastId, new Vector2Int(4, 6), null), ("ver3.5", "first_field", new Vector2Int(-7, -5), new[] { Plaza, Home }),
                ("ver3.6", V36.NorthId, new Vector2Int(6, 8), new[] { Plaza, Home, Pond }),
                ("ver3.7", V37.WestId, new Vector2Int(8, 9), new[] { Plaza, Home, Pond, Clearing }),
                ("ver3.8", V38.SouthId, new Vector2Int(7, 6), new[] { Plaza, Home, Pond, Clearing, Pinwheel }),
            };
            foreach (var (version, map, cell, found) in places)
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", map, cell);
                data.GameVersion = version;
                if (found != null) data.FastTravel = new List<string>(found);
                if (version == "ver3.6" || version == "ver3.7" || version == "ver3.8") data.CrowMode = HarukaSaveData.CrowFollow;
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Settle(g, 0.5f);
                Check($"a {version} save ({map} {cell}): resumed there, steps kept, " + (found != null ? "its places kept" : "home only"),
                    g.Session.TryGetSavePlace(out var m, out var l) && m == map && l == cell && g.LifeLog.TotalSteps == 4321 &&
                    (found == null ? T(g).FoundIds.SequenceEqual(new[] { Home }) : found.All(id => T(g).IsFound(id)) && T(g).FoundIds.Count() == found.Length) &&
                    (found == null || version == "ver3.5" || g.Crow.Following),
                    $"{(g.Session.TryGetSavePlace(out var mm, out var ll) ? mm + " " + ll : "no place")} steps {g.LifeLog.TotalSteps} found {string.Join(",", T(g).FoundIds)}");
            }
            // A damaged newest snapshot: the older one (in the South-East) is loaded.
            {
                var storage = new HarukaMemoryStorage();
                var g = Begin(storage);
                Walk(g, Specs[3].Middle);
                Settle(g, 2f);
                g.Save.Write("check", C6.Clock);
                var latest = C6.Latest(g);
                var newest = HarukaSnapshotStore.SlotNames.First(x => HarukaSaveData.TryRead(storage.Read(x), out var d, out _) == HarukaSaveReadStatus.Valid && d.Revision == latest.Revision);
                storage.Write(newest, storage.Read(newest).Replace("\"" + SouthEastId + "\"", "\"southeast_transition_02\""));
                var r = Begin(storage);
                Check("the newest snapshot damaged: the backup snapshot is loaded, in the South-East", r.Save.LoadSource == "backup snapshot" &&
                    r.Session.CurrentRegionMapId == SouthEastId, $"{r.Save.LoadSource} {Where(r)}");
            }
            // Rollback: the ver3.8 world (the five regions alone, as an older build has it) opening a save made in a transition region.
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", NorthWestId, new Vector2Int(20, 6));
                data.GameVersion = "ver3.9";
                data.TotalSteps = 4567;
                data.FastTravel = new List<string> { Plaza, Home, Pond, Clearing, Pinwheel };
                data.CrowMode = HarukaSaveData.CrowFollow;
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage, s => { var w = Ver38World(out var p); s.UseOutdoorWorldForTest(w, p, false); });
                Check("rollback (a world without the transition regions opens a save made in one): the first map's start, steps and all five places kept, the crow following",
                    g.Session.TryGetSavePlace(out var m, out _) && m == "first_field" && g.LifeLog.TotalSteps == 4567 && g.Save.LocationNote.Contains("unknown map") &&
                    new[] { Plaza, Pond, Clearing, Pinwheel }.All(id => T(g).IsFound(id)) && g.Crow.Following, g.Save.LocationNote);
                g.Save.OnPageHidden("");
                var resaved = C6.Latest(g);
                Check("…saved again there: the position is the start's (the transition region's cell is lost; nothing else)", resaved.MapId == "first_field" &&
                    resaved.TotalSteps >= 4567 && resaved.FastTravel.Count == 5, HarukaSaveSystem.Describe(resaved));
            }
        }

        // ---------------------------------------------------------------- 20.8 chunk streaming

        private static void ChunkChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var streamer = c.Presenter.Streamer;
            c.Session.PlaceForCheck(new Vector2Int(-2, 0), FacingDirection.Down);
            Settle(c, 1f);
            var index = c.Session.OutdoorIndex;
            var all = index.Chunks.Count;
            Check($"at the start in the middle: all {all} chunks drawn, none failed", streamer.Stats.Loaded == all && streamer.Stats.Failed == 0 && streamer.Stats.FallbackChunks == 0,
                $"loaded {streamer.Stats.Loaded} of {all}");
            var objectsAll = c.Presenter.LoadedChunkObjects;
            var meshesAtStart = RuntimeMeshes();
            var namesAtStart = RuntimeMeshNames();
            var outsideAtStart = AllTransforms() - ChunkTransforms();
            // Round the eight directions from the middle and back to it (the corners by the new regions), twice.
            var route = new[] { ("N", new Vector2Int(0, 16)), ("NE", new Vector2Int(30, 22)), ("E", PlazaArrival), ("SE", new Vector2Int(30, -22)),
                ("S", new Vector2Int(3, -24)), ("SW", new Vector2Int(-30, -22)), ("W", new Vector2Int(-33, -1)), ("NW", new Vector2Int(-30, 22)), ("middle", new Vector2Int(-2, 0)) };
            var log = new List<string>();
            var meshesAfter = new List<int>();
            var objectsAfter = new List<int>();
            for (var round = 1; round <= 2; round++)
            {
                foreach (var (name, goal) in route)
                {
                    var walked = Walk(c, V32.NearestWalkable(c, goal), null, 240f);
                    Settle(c, 0.5f);
                    log.Add($"{round}:{name} {(walked ? "" : "(not reached) ")}{c.Session.CurrentRegionMapId} loaded {streamer.Stats.Loaded} loads {streamer.Stats.TotalLoads} unloads {streamer.Stats.TotalUnloads}");
                }
                Settle(c, 1f);
                meshesAfter.Add(RuntimeMeshes());
                objectsAfter.Add(c.Presenter.LoadedChunkObjects);
            }
            var reloaded = streamer.Records.Values.Where(r => r.Loads > 1).ToList();
            Check("two rounds from the middle round the eight directions (N, NE, E, SE, S, SW, W, NW) and back, on foot: every leg reached",
                !log.Any(l => l.Contains("not reached")), string.Join("; ", log));
            Check("the far corners free the chunks of the opposite side and draw them again when she comes back (loads and unloads both happen)",
                streamer.Stats.TotalUnloads > 0 && reloaded.Count > 0, $"unloads {streamer.Stats.TotalUnloads}, reloaded chunks {reloaded.Count}");
            Check("back in the middle after each round: every chunk drawn again, the same Objects (none twice)",
                streamer.Stats.Loaded == all && objectsAfter.All(o => o == objectsAll) && V32.DuplicateViews(c).Count == 0,
                $"{V32.ChunkSummary(c)} objects {string.Join(",", objectsAfter)} / {objectsAll}");
            Check("runtime meshes: after each round exactly as many as at first (nothing a freed chunk made is left behind; ver3.7's fix holds)",
                meshesAfter.All(m => m == meshesAtStart), $"{meshesAtStart} -> {string.Join(", ", meshesAfter)}; left: " + LeftBehind(namesAtStart));
            Check("GameObjects outside the chunks: as many as at first", AllTransforms() - ChunkTransforms() == outsideAtStart, $"{outsideAtStart} -> {AllTransforms() - ChunkTransforms()}");
            Check("no chunk built in a hurry by the step gate, none failed", streamer.Stats.SyncLoads == 0 && streamer.Stats.TotalFailures == 0, V32.ChunkSummary(c));
            Check("summary (official world, two rounds of eight): loads " + streamer.Stats.TotalLoads + ", unloads " + streamer.Stats.TotalUnloads + ", chunks drawn again " + reloaded.Count +
                ", average load " + streamer.Stats.AverageLoadMs.ToString("0.0") + " ms, Objects drawn in the middle " + objectsAll, streamer.Records.Count <= all + 4);
            // The far North-West corner chunk (-3,1) (x -48..-33: the world's West edge) freed and drawn again: walked to the
            // far South-East and back. One trip first, so the measure before and after is taken after the same way in
            // (the one-chunk hysteresis keeps different chunks after different ways).
            var cornerCell = V32.NearestWalkable(c, new Vector2Int(-34, 22));
            var corner = index.ChunkOfCell(cornerCell);
            var farCell = V32.NearestWalkable(c, new Vector2Int(34, -24));
            Walk(c, cornerCell, null, 300f);
            Walk(c, farCell, null, 300f);
            Walk(c, cornerCell, null, 300f);
            Settle(c, 1f);
            var cornerViews = c.Presenter.Views.Where(v => !v.IsWall && index.ChunkOfCell(v.RootCell) == corner).Select(v => v.InstanceId).OrderBy(x => x).ToList();
            var cornerLoads = streamer.Records[corner].Loads;
            var meshesHere = RuntimeMeshes();
            var namesHere = RuntimeMeshNames();
            var orphansHere = OrphanMeshes();
            Walk(c, farCell, null, 300f);
            Settle(c, 1f);
            Check($"at the far South-East {farCell}: the North-West corner's chunk {corner} is freed", streamer.StateOf(corner) == ChunkState.Unloaded && !c.Presenter.IsChunkDrawn(corner), V32.ChunkSummary(c));
            Walk(c, cornerCell, null, 300f);
            Settle(c, 1f);
            var againViews = c.Presenter.Views.Where(v => !v.IsWall && index.ChunkOfCell(v.RootCell) == corner).Select(v => v.InstanceId).OrderBy(x => x).ToList();
            Check($"back in the North-West corner {cornerCell}: its chunk drawn again (once more), with exactly the same Objects ({cornerViews.Count}), none twice",
                streamer.Records[corner].Loads == cornerLoads + 1 && againViews.SequenceEqual(cornerViews) && V32.DuplicateViews(c).Count == 0,
                $"loads {cornerLoads} -> {streamer.Records[corner].Loads}, views {cornerViews.Count} -> {againViews.Count}");
            Check("…runtime meshes as before the trip (the freed chunk's meshes destroyed, the new ones the same number), no unused one more",
                RuntimeMeshes() == meshesHere && OrphanMeshes() <= orphansHere, $"meshes {meshesHere} -> {RuntimeMeshes()}, unused {orphansHere} -> {OrphanMeshes()}; left: " + LeftBehind(namesHere));
            // Into the house and out again three times.
            c.Session.OutdoorWorld.TryGetHouseDoor(out var houseDoor);
            Walk(c, houseDoor + Vector2Int.down, null, 240f);
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
            Check("into the house and out again three times: as many runtime meshes as before (ver3.7's fix)",
                c.Session.World == c.Session.OutdoorWorld && RuntimeMeshes() == meshesOutside, $"{meshesOutside} -> {RuntimeMeshes()}; left: " + LeftBehind(namesOutside));
            // The ver3.2 256 x 256 test world: the same streamer, the camera at its corners, long walks there and back.
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
            var orphansW = OrphanMeshes();
            var monoAtStart = GC.GetTotalMemory(true);
            var b = w.Session.OutdoorIndex.Bounds;
            var offW = new List<string>();
            foreach (var cell in new[] { b.Min, b.Max, new Vector2Int(b.Min.x, b.Max.y), new Vector2Int(b.Max.x, b.Min.y) })
            {
                var stand = V32.NearestWalkable(w, cell);
                w.Session.PlaceForCheck(stand, FacingDirection.Up);
                w.Pixel.SnapToTarget();
                C6.Frame(w, 1f / 60f, Vector2Int.zero);
                var p = w.Pixel.WorldToScreenPoint(GridSpace3D.ToWorld(stand));
                if (!OnScreen(p)) offW.Add(stand + " " + p.ToString("0"));
            }
            Check("256 x 256 test world: the 2D camera keeps her on screen at its four corners (the camera rules of ver3.6 / ver3.7 untouched)", offW.Count == 0, string.Join("; ", offW));
            w.Session.PlaceForCheck(start, FacingDirection.Down);
            V32.Settle(w, 1.5f);
            for (var round = 1; round <= 3; round++)
            {
                var goal = V32.NearestWalkable(w, start + new Vector2Int(96, 0));
                var there = V32.WalkTo(w, goal, null, 0.14f);
                V32.Settle(w, 1f);
                var freed = ws.StateOf(startChunk) == ChunkState.Unloaded;
                var back = V32.WalkTo(w, start, null, 0.14f);
                V32.Settle(w, 1.5f);
                var againW = w.Presenter.Views.Where(v => !v.IsWall && w.Session.OutdoorIndex.ChunkOfCell(v.RootCell) == startChunk).Select(v => v.InstanceId).OrderBy(x => x).ToList();
                Check($"256 x 256 round trip {round} (96 cells East and back): the start's chunk freed there, drawn again with the same Objects, none twice",
                    there && back && freed && ws.StateOf(startChunk) == ChunkState.Ready && againW.SequenceEqual(startViews) && V32.DuplicateViews(w).Count == 0, V32.ChunkSummary(w));
            }
            var monoBack = GC.GetTotalMemory(true);
            Check("256 x 256 after three round trips: no runtime mesh left unused, GameObjects outside the chunks as at the start, managed memory within 8 MB",
                OrphanMeshes() <= orphansW && AllTransforms() - ChunkTransforms() == outsideW && monoBack - monoAtStart < 8L * 1024 * 1024,
                $"unused {orphansW} -> {OrphanMeshes()}, outside {outsideW} -> {AllTransforms() - ChunkTransforms()}, mono {monoAtStart / 1048576f:0.0} -> {monoBack / 1048576f:0.0} MB");
        }

        // ---------------------------------------------------------------- what was there

        private static void ExistingChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            Check("the five ver3.8 Maps as they were: Objects (first_field, East, North 21x20 with 22 water cells, West 83, South 80) and their landmarks' markers",
                index.FindRegion(V37.WestId).Map.Objects.Count == 83 && index.FindRegion(V38.SouthId).Map.Objects.Count == 80 && c.Presenter.WaterCellsDrawn == 22 &&
                index.FindRegion(V33.EastId).Map.TryGetMarker(Plaza, out var pm) && pm == new Vector2Int(16, 4) &&
                index.FindRegion(V36.NorthId).Map.TryGetMarker(Pond, out var qm) && qm == new Vector2Int(6, 8) &&
                index.FindRegion(V37.WestId).Map.TryGetMarker(Clearing, out var cm) && cm == new Vector2Int(8, 9) &&
                index.FindRegion(V38.SouthId).Map.TryGetMarker(Pinwheel, out var sm) && sm == new Vector2Int(7, 7));
            var houseObjects = c.Session.InteriorWorld.Objects.Count();
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Walk(c, door + Vector2Int.down);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("the house: in through its door, its furniture all there (" + houseObjects + ")", c.Session.World.MapId == "halka_house" &&
                c.Session.World.Objects.Count() == houseObjects && houseObjects == c.Session.World.Map.Objects.Count);
            V32.WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.14f);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("the booth from the house", c.Session.World.MapId == "halka_soundbooth");
            V32.Step(c, Vector2Int.down, 0.4f);
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Check("and out again (the forest's light, the water and the pinwheel rebuilt with the outdoor world)", c.Session.World == c.Session.OutdoorWorld &&
                c.Session.Cell == door + Vector2Int.down && c.Presenter.KomorebiCellsDrawn > 0 && c.Presenter.PinwheelBladesLoaded == 1 && c.Presenter.WaterCellsDrawn == 22);
            // The landmarks, found on foot as before (coming by the transition regions).
            Walk(c, new Vector2Int(22, 16));
            Walk(c, PlazaArrival);
            Check("the plaza found by walking there (by the North-East)", T(c).IsFound(Plaza));
            Walk(c, PondArrival);
            Check("the pond found by walking there", T(c).IsFound(Pond));
            Walk(c, new Vector2Int(-25, 15));
            Walk(c, new Vector2Int(-27, 3));
            Check("the clearing found by walking there (by the North-West)", T(c).IsFound(Clearing));
            Walk(c, new Vector2Int(-24, -17));
            Walk(c, new Vector2Int(-3, -19));
            Check("the pinwheel's place found by walking there (by the South-West)", T(c).IsFound(Pinwheel));
            // The three examine spots keep their effects.
            var leaves = c.Presenter.LeafBurstsStarted;
            var ripples = c.Presenter.RipplesStarted;
            var spins = c.Presenter.PinwheelSpinsStarted;
            c.Session.PlaceForCheck(new Vector2Int(-27, 3), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var stump = c.Session.InteractAhead();
            c.Session.PlaceForCheck(new Vector2Int(-4, 15), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var pond = c.Session.InteractAhead();
            c.Session.PlaceForCheck(new Vector2Int(-3, -19), FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var pinwheel = c.Session.InteractAhead();
            Check("きりかぶにふれる, みずをのぞく and かざぐるまをまわす as before (each its own effect, once)",
                stump && pond && pinwheel && c.Presenter.LeafBurstsStarted == leaves + 1 && c.Presenter.RipplesStarted == ripples + 1 && c.Presenter.PinwheelSpinsStarted == spins + 1);
            Settle(c, 3f);
            // AUTO next to a new border: never into the transition region by itself.
            foreach (var (label, cell, region) in new[] { ("the North area by the North-West", new Vector2Int(-9, 18), V36.NorthId), ("the East area by the South-East", new Vector2Int(24, -5), V33.EastId) })
            {
                c.Session.PlaceForCheck(cell, FacingDirection.Left);
                c.Hud.ClearMessage();
                c.Auto.SetAutoEnabled(true, C6.Clock);
                var regions = new HashSet<string>();
                var autoSteps = c.Gameplay.AutoWorld.AutoSteps;
                for (var t = 0f; t < 120f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId ?? c.Session.World.MapId); }
                c.Auto.SetAutoEnabled(false, C6.Clock);
                Check($"AUTO in {label} for 120 s: it walks, never into a transition region (nor the house)", c.Gameplay.AutoWorld.AutoSteps > autoSteps &&
                    regions.SetEquals(new[] { region }), string.Join(",", regions));
            }
            // Night in a transition region: the shared clock.
            c.Session.PlaceForCheck(Specs[2].Middle, FacingDirection.Down);
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 4f);
            Check("at night the South-West is in the night (the shared clock and lights)", c.Time.NightBlend > 0.99f && c.Session.CurrentRegionMapId == SouthWestId);
            c.Time.Override = PocPhaseOverride.Morning;
            Settle(c, 4f);
            Check("in the morning it is day again", c.Time.NightBlend < 0.01f);
            c.Time.Override = PocPhaseOverride.Auto;
            Check("the menu and its rows (そらをとぶ, セーブデータ) are there; the Life Log counts", c.Menu.LinkLabels.Contains(PocFastTravel.MenuLabel) &&
                c.Menu.LinkLabels.Contains(HarukaSaveSystem.MenuLabel) && c.LifeLog.TotalSteps > 0);
            Check("the game's version label: ver3.9", PocDebugPanel.ReleaseCandidateVersionLabel == "ver3.9");
        }

        // ---------------------------------------------------------------- what must not change

        private static void ProtectChecks()
        {
            Check("WORLD EDITOR v1.0 unchanged (tools/HalkaWorldMapEditor, since ver3.8's publication)", V32.Git("diff --quiet " + Published + " -- tools/HalkaWorldMapEditor", out _));
            Check("the five ver3.8 Maps, the house and the booth (Authoring JSON and caches) unchanged: no border cell of an existing region had to change",
                V32.Git("diff --quiet " + Published + " -- " + string.Join(" ", OldIds.Concat(new[] { "halka_house", "halka_soundbooth" })
                    .SelectMany(m => new[] { "unity-project/Assets/Content/Maps/Authoring/" + m + ".hwmap.json", "unity-project/Assets/Content/Maps/" + m + ".asset" })), out _));
            Check("the Maps folder: nothing changed or removed; the object catalog unchanged (no new Object); the registry and the themes only added to",
                V32.Git("diff --quiet --diff-filter=MDR " + Published + " -- unity-project/Assets/Content/Maps :(exclude)unity-project/Assets/Content/Maps/World/outdoor_main.hwworld.json " +
                    ":(exclude)unity-project/Assets/Content/Maps/World/direction_themes.json", out _) &&
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/Maps/Authoring/object_catalog.hwcatalog.json", out _) &&
                V36.OnlyAdded(Published, "unity-project/Assets/Content/Maps/World/outdoor_main.hwworld.json") &&
                V36.OnlyAdded(Published, "unity-project/Assets/Content/Maps/World/direction_themes.json"));
            Check("no new model or picture (Models, Content/World, the presentation catalog unchanged)",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Models unity-project/Assets/Content/World unity-project/Assets/POC2D3D/Data blender", out _));
            Check("the そらをとぶ places unchanged (five)", V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/FastTravel", out _));
            Check("the published ver3.8 (halkaworld/) unchanged", V32.Git("diff --quiet " + Published + " -- halkaworld", out _));
            Check("the crow's sprites, model and voice, and the font atlas unchanged", V36.CrowKept(Published, true));
            Check("the save system and the crow's code unchanged (no Save Schema change)",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/Save unity-project/Assets/POC2D3D/Runtime/PocCrow.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Companion.cs unity-project/Assets/POC2D3D/Runtime/PocCrow.Travel.cs unity-project/Assets/Scripts/World/CrowBehaviourCore.cs", out _));
            Check("the chunk streamer, the world index (ver3.8's dressing of the whole rectangle), the chunk presenter and the cameras unchanged",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/POC2D3D/Runtime/World/ChunkStreamer.cs unity-project/Assets/POC2D3D/Runtime/World/OutdoorWorldIndex.cs " +
                    "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.Chunks.cs unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.cs unity-project/Assets/POC2D3D/Runtime/PocOutlineTag.cs " +
                    "unity-project/Assets/POC2D3D/Runtime/PocPixelView2D.cs unity-project/Assets/POC2D3D/Runtime/PocThirdPersonCamera.cs unity-project/Assets/POC2D3D/Runtime/PocTreeFade3D.cs " +
                    "unity-project/Assets/POC2D3D/Runtime/PocEnvironmentBuilder.cs", out _));
            Check("ver3.7's mesh freeing still there (chunks and rooms)",
                File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.Chunks.cs")).Contains("CollectMeshes(root, payload.Meshes, true);") &&
                File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Runtime/PocWorldPresenter.cs")).Contains("FreeMeshes(roomMeshes);"));
            Check("the old tests are all still there (none deleted since ver3.8's publication)",
                V32.Git("diff --quiet --diff-filter=D " + Published + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _));
        }
    }
}
