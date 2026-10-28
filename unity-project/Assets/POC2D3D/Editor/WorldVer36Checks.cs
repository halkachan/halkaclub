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

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.6「北・青エリア」: the checks of the North region north_area_01 (the registry, the
    // road over the region border y 6|7 and the chunk border y 15|16, the pond: its water, Collision and
    // its みずをのぞく with the ripple, the crow, そらをとぶ to きたのみずべ, the save) and of what must not
    // change. The game is started with the Phase 6 harness (memory storage only); every frame also runs
    // what Update does in play (the crow, the choices' watch, the chunk streamer, the water, the flight).
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer36Checks.RunBatch
    public static class WorldVer36Checks
    {
        public const string NorthId = "north_area_01";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Home = "landmark_halka_home";
        private const string Plaza = "landmark_east_flower_plaza";
        private const string Pond = "landmark_north_blue_pond";
        private static readonly Vector2Int NorthOffset = new Vector2Int(-10, 7);
        private static readonly Vector2Int LookSpot = new Vector2Int(-4, 15);     // local (6,8): the lily's Action Point
        private static readonly Vector2Int Lily = new Vector2Int(-4, 16);         // local (6,9)
        private static readonly Vector2Int PondArrival = new Vector2Int(-3, 15);  // local (7,8)
        private static readonly Vector2Int HomeArrival = new Vector2Int(-7, -5);
        private static readonly Vector2Int PlazaArrival = new Vector2Int(27, -2);
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";
        private static int maxCrows;
        private static int crowOnWaterFrames;
        private static int playerOnWaterFrames;

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
            UnityEngine.Random.InitState(3636);
            try
            {
                Run("region", RegionChecks);
                Run("walk", WalkChecks);
                Run("pond", PondChecks);
                Run("crow", CrowChecks);
                Run("travel", TravelChecks);
                Run("save", SaveChecks);
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
            foreach (var failure in failures) Debug.LogError("VER36 FAIL " + failure);
            Debug.Log("HARUKA ver3.6 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.6 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V36_LOGS", "ver36_checks.txt");
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
            if (condition) { passes++; Debug.Log("VER36 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness (as WorldVer35Checks)

        private static PocFastTravel T(C6.Ctx c) => c.Gameplay.FastTravel;

        private static C6.Ctx Begin(HarukaMemoryStorage storage)
        {
            var c = C6.Start(storage);
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
            T(c).Tick(C6.Clock);
            maxCrows = Mathf.Max(maxCrows, Crows());
            if (c.Session.World == c.Session.OutdoorWorld)
            {
                if (IsWater(c, c.Session.Cell)) playerOnWaterFrames++;
                if (crow != null && crow.isActiveAndEnabled && crow.GroundOccupancyActive && IsWater(c, crow.Cell)) crowOnWaterFrames++;
            }
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
        private static bool Walk(C6.Ctx c, Vector2Int goal, Action onFrame = null, float maxSeconds = 90f)
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

        private static bool IsWater(C6.Ctx c, Vector2Int cell)
        {
            var region = c.Session.OutdoorIndex.RegionAt(cell);
            var surface = region != null ? region.SurfaceAtWorld(cell) : null;
            return surface != null && surface.StableId == PocWorldPresenter.WaterSurfaceId;
        }

        private static List<Vector2Int> WaterCells(C6.Ctx c)
        {
            var north = c.Session.OutdoorIndex.FindRegion(NorthId);
            var list = new List<Vector2Int>();
            for (var y = north.Rect.Min.y; y <= north.Rect.Max.y; y++)
            for (var x = north.Rect.Min.x; x <= north.Rect.Max.x; x++)
                if (IsWater(c, new Vector2Int(x, y))) list.Add(new Vector2Int(x, y));
            return list;
        }

        private static string Where(C6.Ctx c) =>
            $"her {c.Session.Cell} {c.Session.CurrentRegionMapId ?? c.Session.World.MapId} {c.Session.Facing} crow {c.Crow.Cell} {c.Crow.CompanionState} mode {c.Crow.Mode}";

        private static int Crows() => UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private static Vector3 CameraFor(C6.Ctx c, Vector2Int cell)
        {
            c.Session.PlaceForCheck(cell, FacingDirection.Up);
            c.Pixel.SnapToTarget();
            Frame(c, 1f / 60f, Vector2Int.zero);
            return c.Pixel.WorldCamera.transform.position;
        }

        // ---------------------------------------------------------------- 17.1 the region

        private static void RegionChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var definition = index.Definition;
            var north = index.FindRegion(NorthId);
            Check("the registry: first_field, east_area_01 and north_area_01 (in that order)",
                index.Regions.Select(r => r.MapId).SequenceEqual(new[] { "first_field", V33.EastId, NorthId }), string.Join(",", index.Regions.Select(r => r.MapId)));
            Check("north_area_01: direction north, World offset (-10,7), 21 x 20 cells: World (-10,7)..(10,26)",
                north != null && north.Direction == "north" && north.Offset == NorthOffset &&
                north.Rect.Min == new Vector2Int(-10, 7) && north.Rect.Max == new Vector2Int(10, 26), north != null ? $"{north.Offset} {north.Rect.Min}..{north.Rect.Max}" : "missing");
            var theme = definition.ThemeOf("north");
            Check("its theme: きた, #5b84b1 / #3d5f86 (direction_themes.json, unchanged)", theme != null && theme.name == "きた" &&
                ColorUtility.ToHtmlStringRGB(theme.color) == "5B84B1" && ColorUtility.ToHtmlStringRGB(theme.shade) == "3D5F86");
            var problems = definition.Validate();
            Check("the world passes its checks: no region overlaps another", !OutdoorWorldDefinition.HasErrors(problems), string.Join("; ", problems));
            Check("first_field and east_area_01 did not move", index.FindRegion("first_field").Offset == Vector2Int.zero &&
                index.FindRegion(V33.EastId).Offset == new Vector2Int(11, -6));
            Check("the Map is the authored one (Map JSON v4 through the importer): 49 Surfaces, 46 Objects, its markers",
                north.Map.Surfaces.Count == 49 && north.Map.Objects.Count == 46 && north.Map.TryGetMarker(Pond, out var pondMarker) &&
                pondMarker == new Vector2Int(6, 8) && north.Map.TryGetMarker("road_south", out var rs) && rs == new Vector2Int(10, 0),
                $"{north.Map.Surfaces.Count} surfaces, {north.Map.Objects.Count} objects");
            var road = Enumerable.Range(4, 23).Select(y => new Vector2Int(0, y)).ToList();
            Check("one continuous road North: World (0,4)..(0,26) all walkable dirt, over the border (0,6)|(0,7) (first_field's road turns at (0,4))",
                road.All(cell => index.CanEnter(cell) && index.SurfaceAt(cell)?.StableId == "dirt"),
                string.Join(" ", road.Where(cell => !index.CanEnter(cell) || index.SurfaceAt(cell)?.StableId != "dirt")));
            Check("no blocking Object on the border rows next to the road (y 6 and 7, x -1..1)",
                new[] { -1, 0, 1 }.All(x => index.CanEnter(new Vector2Int(x, 6)) && index.CanEnter(new Vector2Int(x, 7))));
            Check("outside every region nothing is walkable (West, East, North of the North area)",
                !index.CanEnter(new Vector2Int(-11, 12)) && !index.CanEnter(new Vector2Int(11, 12)) && !index.CanEnter(new Vector2Int(0, 27)) &&
                index.RegionAt(new Vector2Int(11, 12)) == null);
            // Every walkable cell of the North area can be reached on foot from the road.
            var reach = new HashSet<Vector2Int> { new Vector2Int(0, 7) };
            var queue = new Queue<Vector2Int>(reach);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = cell + d;
                    if (!north.ContainsWorld(n) || reach.Contains(n) || !index.CanEnter(n)) continue;
                    reach.Add(n);
                    queue.Enqueue(n);
                }
            }
            var walkable = 0;
            for (var y = north.Rect.Min.y; y <= north.Rect.Max.y; y++)
            for (var x = north.Rect.Min.x; x <= north.Rect.Max.x; x++)
                if (index.CanEnter(new Vector2Int(x, y))) walkable++;
            Check("every walkable cell of the North area can be reached on foot (no closed-in corner)", reach.Count == walkable, $"{reach.Count} of {walkable}");
            Check("the Region border y 6|7 and the chunk border y 15|16 are different rows (chunk size 16)",
                index.ChunkOfCell(new Vector2Int(0, 6)) == index.ChunkOfCell(new Vector2Int(0, 7)) &&
                index.ChunkOfCell(new Vector2Int(0, 15)) != index.ChunkOfCell(new Vector2Int(0, 16)));
            Check("the save menu names it by its direction: きた", c.Save.PlaceName(NorthId) == "きた", c.Save.PlaceName(NorthId));
        }

        // ---------------------------------------------------------------- 17.2 walking

        private static void WalkChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            // The 2D camera: first_field and the East area as before (it does not move North in them).
            var startCam = CameraFor(c, Vector2Int.zero);
            var topCam = CameraFor(c, new Vector2Int(0, 6));
            var eastCam = CameraFor(c, new Vector2Int(20, 0));
            Check("2D camera in first_field as in ver3.5: (0.00, 22.98, -19.28) at the start", (startCam - new Vector3(0f, 22.98f, -19.28f)).magnitude < 0.02f, startCam.ToString("0.00"));
            Check("2D camera at first_field's top (0,6): the same height as at the start (it did not follow North there before)", Mathf.Abs(topCam.z - startCam.z) < 0.01f,
                $"{topCam.z:0.00} vs {startCam.z:0.00}");
            Check("2D camera in the East area: the same height as before", Mathf.Abs(eastCam.z - startCam.z) < 0.01f, eastCam.ToString("0.00"));
            var deepCam = CameraFor(c, new Vector2Int(0, 16));
            var herZ = GridSpace3D.ToWorld(new Vector2Int(0, 16)).z;
            Check("2D camera in the North area (0,16), by the pond: (nearly) centred on her (within a row)", Mathf.Abs(deepCam.z - (startCam.z + herZ)) <= 0.5f,
                $"camera z {deepCam.z:0.00}, her z {herZ:0.00}");
            var topEdgeCam = CameraFor(c, new Vector2Int(0, 26));
            var topZ = GridSpace3D.ToWorld(new Vector2Int(0, 26)).z;
            var halfView = c.Pixel.ViewHalfWorldNorth;
            Check("2D camera at the North edge (0,26): she is still on screen (the camera stops at the area's edge)",
                topZ - (topEdgeCam.z - startCam.z) < halfView, $"camera z {topEdgeCam.z:0.00}, her z {topZ:0.00}, half view {halfView:0.00}");
            // Walk North from the start over the region border.
            c.Session.PlaceForCheck(new Vector2Int(0, 5), FacingDirection.Up);
            c.Pixel.SnapToTarget();
            Settle(c, 0.3f);
            var steps = c.Session.CompletedSteps;
            var loads = c.Session.MapLoads;
            var changes = c.Session.RegionChanges;
            var lifeSteps = c.LifeLog.TotalSteps;
            C6.Step(c, Vector2Int.up, 0.4f);
            C6.Step(c, Vector2Int.up, 0.4f);
            Check("two steps North (0,5) -> (0,6) -> (0,7): the North area, facing up, one region change, no map load",
                c.Session.Cell == new Vector2Int(0, 7) && c.Session.CurrentRegionMapId == NorthId && c.Session.Facing == FacingDirection.Up &&
                c.Session.RegionChanges == changes + 1 && c.Session.MapLoads == loads && c.Session.World == c.Session.OutdoorWorld, $"{c.Session.Cell} {c.Session.CurrentRegionMapId}");
            Check("each step counted once (session and Life Log)", c.Session.CompletedSteps == steps + 2 && c.LifeLog.TotalSteps == lifeSteps + 2);
            // On along the road over the chunk border, the camera moving smoothly.
            var last = c.Pixel.WorldCamera.transform.position;
            var maxJump = 0f;
            var chunkChanges = 0;
            var lastChunk = c.Session.OutdoorIndex.ChunkOfCell(c.Session.Cell);
            Walk(c, new Vector2Int(0, 20), () =>
            {
                var p = c.Pixel.WorldCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
                var chunk = c.Session.OutdoorIndex.ChunkOfCell(c.Session.Cell);
                if (chunk != lastChunk) { chunkChanges++; lastChunk = chunk; }
            });
            Check("North on the road to (0,20): over the chunk border y 15|16, still north_area_01, no map load", c.Session.Cell == new Vector2Int(0, 20) &&
                c.Session.CurrentRegionMapId == NorthId && c.Session.MapLoads == loads && chunkChanges == 1, $"{c.Session.Cell} chunk changes {chunkChanges}");
            Check("the 2D camera moves smoothly all the way (no jump at the borders)", maxJump < 0.12f, $"max {maxJump:0.000} unit/frame");
            Check("the chunk under her and ahead of her is drawn", c.Session.IsCellReady(c.Session.Cell) && c.Session.IsCellReady(new Vector2Int(0, 24)));
            // 3D: the same place, the same facing; back South.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.5f);
            var cam3 = c.Third.ViewCamera.transform.position;
            var her3 = GridSpace3D.ToWorld(c.Session.Cell);
            Check("3D in the North area: the same cell, the camera behind her (South of her)", c.Session.Cell == new Vector2Int(0, 20) &&
                cam3.z < her3.z && new Vector2(cam3.x - her3.x, cam3.z - her3.z).magnitude < 6f, $"camera {cam3} her {her3}");
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            last = c.Pixel.WorldCamera.transform.position;
            maxJump = 0f;
            Walk(c, new Vector2Int(0, 2), () =>
            {
                var p = c.Pixel.WorldCamera.transform.position;
                maxJump = Mathf.Max(maxJump, (p - last).magnitude);
                last = p;
            });
            Check("back South into first_field (0,2): the region is first_field again, no map load, the camera smooth",
                c.Session.CurrentRegionMapId == "first_field" && c.Session.MapLoads == loads && maxJump < 0.12f, $"{c.Session.Cell} max {maxJump:0.000}");
            Check("and the 2D camera is back at its first_field height", Mathf.Abs(c.Pixel.WorldCamera.transform.position.z - startCam.z) < 0.05f);
        }

        // ---------------------------------------------------------------- 17.3 the pond

        private static void PondChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            var water = WaterCells(c);
            Check("the pond: 22 water cells (about 5 x 4, not square), all blocked, no grass", water.Count == 22 &&
                water.All(w => !index.CanEnter(w) && !index.HasGrass(w)) &&
                water.Min(w => w.x) == -7 && water.Max(w => w.x) == -2 && water.Min(w => w.y) == 16 && water.Max(w => w.y) == 20,
                $"{water.Count} cells {water.Min(w => w.x)}..{water.Max(w => w.x)} x {water.Min(w => w.y)}..{water.Max(w => w.y)}");
            Check("away from the road (never on x = 0): the road is not blocked by it", water.All(w => w.x <= -2));
            var bank = new HashSet<Vector2Int>();
            foreach (var w in water) foreach (var d in Dirs) if (!IsWater(c, w + d) && index.CanEnter(w + d)) bank.Add(w + d);
            Check("its bank can be walked: 10 or more bank cells next to the water", bank.Count >= 10, bank.Count.ToString());
            c.Session.PlaceForCheck(LookSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Settle(c, 0.5f);
            Check("the water is drawn in its chunks (all 22 cells), with its glints", c.Presenter.WaterCellsDrawn == 22 &&
                GameObject.Find("Water " + index.ChunkOfCell(Lily)) != null, c.Presenter.WaterCellsDrawn.ToString());
            var waterObject = GameObject.Find("Water " + index.ChunkOfCell(Lily));
            var bounds = waterObject.GetComponent<Renderer>().bounds;
            var lo = GridSpace3D.ToWorld(new Vector2Int(-7, 16)) - new Vector3(0.25f, 0f, 0.25f);
            var hi = GridSpace3D.ToWorld(new Vector2Int(-2, 20)) + new Vector3(0.25f, 0f, 0.25f);
            Check("the drawn water covers exactly the pond's cells (the 2D view draws these same meshes: one shape in both views)",
                Mathf.Abs(bounds.min.x - lo.x) < 0.01f && Mathf.Abs(bounds.max.x - hi.x) < 0.01f && Mathf.Abs(bounds.min.z - lo.z) < 0.01f &&
                Mathf.Abs(bounds.max.z - hi.z) < 0.01f && Mathf.Abs(bounds.center.y - PocWorldPresenter.WaterHeight) < 0.01f, $"{bounds.min} {bounds.max}");
            var glintA = GameObject.Find("Water glints A " + index.ChunkOfCell(Lily))?.GetComponent<Renderer>();
            var glintB = GameObject.Find("Water glints B " + index.ChunkOfCell(Lily))?.GetComponent<Renderer>();
            var toggles = 0;
            var alwaysOne = true;
            var previous = glintA != null && glintA.enabled;
            for (var t = 0f; t < 2f && glintA != null && glintB != null; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (glintA.enabled != previous) { toggles++; previous = glintA.enabled; }
                if (glintA.enabled == glintB.enabled) alwaysOne = false;
            }
            Check("its glints take turns (a quiet shimmer, no shader): two sets, one shown at a time, switching about every 0.7 s",
                glintA != null && glintB != null && alwaysOne && toggles >= 2 && toggles <= 3, $"{toggles} switches in 2 s");
            // Into the water: refused.
            var cell = c.Session.Cell;
            C6.Step(c, Vector2Int.up, 0.4f);
            Check("a step into the water is refused (she stays on the bank)", c.Session.Cell == cell && !c.Session.IsMoving);
            // みずをのぞく
            c.Session.PlaceForCheck(LookSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Frame(c, 1f / 60f, Vector2Int.zero);
            var ripples = c.Presenter.RipplesStarted;
            var looked = c.Session.InteractAhead();
            Check("on the bank (-4,15) facing the lily pads: A looks into the water: 「みずが きらきらしている。」", looked && c.Session.LastMessage == "みずが きらきらしている。" &&
                C6.Shown(c) == "みずが きらきらしている。", c.Session.LastMessage);
            Check("a ripple starts on the water at the lily pads", c.Presenter.RipplesStarted == ripples + 1 && c.Presenter.ActiveRipples == 1);
            Settle(c, 0.6f);
            var mid = c.Presenter.LastRippleRadius;
            Settle(c, PocWorldPresenter.RippleSeconds);
            Check("it grows and is gone after about 1.4 s", mid > 0.08f && c.Presenter.ActiveRipples == 0, $"radius mid-way {mid:0.00}");
            Check("all the while she can move again at once (nothing waits for the ripple)", !c.Session.InputSuspended && !c.Menu.BlocksGameplayInput);
            C6.Step(c, Vector2Int.right, 0.4f);
            Check("and walks on", c.Session.Cell == LookSpot + Vector2Int.right);
            c.Session.PlaceForCheck(LookSpot + Vector2Int.right, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("from beside the lily pads (another bank cell) A finds nothing to look at (the Action Point is that one cell)", !c.Session.InteractAhead());
            c.Session.PlaceForCheck(LookSpot + Vector2Int.down, FacingDirection.Up);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("nor from one cell further back", !c.Session.InteractAhead());
            // 3D: the same pond, the same look.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Session.PlaceForCheck(LookSpot, FacingDirection.Up);
            c.Hud.ClearMessage();
            Settle(c, 0.5f);
            var r3 = c.Presenter.RipplesStarted;
            Check("in 3D the same: the message and a ripple", c.Session.InteractAhead() && c.Presenter.RipplesStarted == r3 + 1);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Settle(c, 1.6f);
            // AUTO by the pond: never into the water, never out of the North area.
            c.Session.PlaceForCheck(new Vector2Int(-1, 18), FacingDirection.Up);
            c.Hud.ClearMessage();   // (a message showing keeps AUTO waiting, by design)
            c.Auto.SetAutoEnabled(true, C6.Clock);
            playerOnWaterFrames = 0;
            var regions = new HashSet<string>();
            for (var t = 0f; t < 160f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId); }
            Check("AUTO by the pond for 160 s: it walks, never into the water, never out of the North area",
                c.Gameplay.AutoWorld.AutoSteps > 0 && playerOnWaterFrames == 0 && regions.SetEquals(new[] { NorthId }),
                $"AUTO steps {c.Gameplay.AutoWorld.AutoSteps}, regions {string.Join(",", regions)}");
            c.Auto.SetAutoEnabled(false, C6.Clock);
        }

        // ---------------------------------------------------------------- 17.4 the crow

        private static void CrowChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var crow = c.Crow;
            maxCrows = 0;
            crowOnWaterFrames = 0;
            Check("ついてきて", StartFollowing(c));
            Walk(c, new Vector2Int(0, 6));
            Settle(c, 2f);
            var regions = new HashSet<string>();
            Walk(c, new Vector2Int(0, 20), () => { if (crow.isActiveAndEnabled) regions.Add(c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(crow.transform.position))?.MapId ?? "-"); });
            Settle(c, 3f);
            Check("the crow follows her North over the region border and the chunk border (one crow, still following, near her)",
                crow.Following && regions.Contains(NorthId) && crow.DistanceToPlayer <= 3 && Crows() == 1 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == NorthId, $"{Where(c)} regions {string.Join(",", regions)}");
            // By the pond: it never stands on the water.
            Walk(c, LookSpot);
            Settle(c, 3f);
            Check("by the pond: beside her on the bank, never on the water", crow.Following && crow.DistanceToPlayer <= 3 && !IsWater(c, crow.Cell) &&
                c.Session.OutdoorWorld.CanEnter(crow.Cell), Where(c));
            // She stands still a while: the crow settles near her, now and then on a tree of the grove.
            var perchedNorth = false;
            for (var t = 0f; t < 90f && !perchedNorth; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (crow.Activity == CrowActivity.Perched && c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(crow.transform.position))?.MapId == NorthId)
                    perchedNorth = true;
            }
            Check("standing by the pond, the following crow rests on a tree of the North grove near her", perchedNorth, crow.Activity.ToString());
            for (var t = 0f; t < 15f && crow.Airborne; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            // Walk round the pond: it keeps to the bank and the road.
            Walk(c, new Vector2Int(-8, 19));
            Walk(c, new Vector2Int(-1, 21));
            Settle(c, 3f);
            Check("round the pond: it found its way after her on land (or flew), never landed on the water", crowOnWaterFrames == 0 && crow.DistanceToPlayer <= 4 &&
                crow.Following, $"{Where(c)} on-water frames {crowOnWaterFrames}");
            // Many hops beside the pond: the landing cells are never water.
            var landingWater = 0;
            var core = typeof(CrowBehaviourCore);
            var choose = typeof(PocCrow).GetMethod("TryChooseGroundLanding", Hidden);
            c.Session.PlaceForCheck(LookSpot, FacingDirection.Up);
            Settle(c, 0.2f);
            for (var i = 0; i < 60; i++)
            {
                var args = new object[] { default(Vector2Int) };
                if ((bool)choose.Invoke(crow, args) && IsWater(c, (Vector2Int)args[0])) landingWater++;
            }
            Check("60 landing choices next to the pond: none on the water", landingWater == 0);
            // Home again, the crow with her.
            Walk(c, new Vector2Int(0, 2));
            Settle(c, 4f);
            Check("back South in first_field: the crow with her", crow.Following && crow.DistanceToPlayer <= 3 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", Where(c));
            // あそんでおいで in the North area: it flies back to its own trees.
            Walk(c, new Vector2Int(0, 14));
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
            Check("あそんでおいで in the North area: free again, it flies back to first_field (its own trees)", !crow.Following && home, Where(c));
            Check("one crow the whole time, never on the water", maxCrows == 1 && crowOnWaterFrames == 0, $"{maxCrows} crows, {crowOnWaterFrames} frames on water");
        }

        // ---------------------------------------------------------------- 17.5 そらをとぶ

        private static void TravelChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var point = t.Registry.Find(Pond);
            var north = c.Session.OutdoorIndex.FindRegion(NorthId);
            Check("the registry has three places; the new one: landmark_north_blue_pond 「きたのみずべ」 in north_area_01, not found at the start",
                t.Registry.points.Count == 3 && point != null && point.name == "きたのみずべ" && point.mapId == NorthId && !point.unlockedAtStart &&
                !t.IsFound(Pond) && GameBitmapFont.HasAllGlyphs(point.name));
            Check("its places: discovered within 2 of the bank (-4,15) (the Map's marker), arriving on the bank (-3,15) facing the water",
                north.ToWorld(point.discoverCell) == LookSpot && point.discoverRadius == 2 && north.ToWorld(point.arrivalCell) == PondArrival &&
                point.ArrivalFacing == FacingDirection.Up && c.Session.OutdoorIndex.CanEnter(PondArrival) && IsWater(c, PondArrival + Vector2Int.up));
            var problems = t.Registry.Validate(c.Session.OutdoorIndex, cell => c.Session.IsTransitionCell(c.Session.OutdoorWorld, cell));
            Check("the three places pass the registry's checks", problems.Count == 0, string.Join("; ", problems));
            Check("not found yet: it cannot be flown to", t.TryBegin(Pond, C6.Clock, FastTravelSource.Check) == FastTravelStart.NotDiscovered);
            Walk(c, new Vector2Int(0, 22));
            Check("walking North on the road past the pond (to (0,22)): not found (the road is 4 cells from the bank)", !t.IsFound(Pond), c.Session.Cell.ToString());
            var foundAt = new Vector2Int(int.MinValue, 0);
            var message = "";
            t.Discovered += p => { if (p.id == Pond) { foundAt = c.Session.Cell; message = C6.Shown(c); } };
            Walk(c, new Vector2Int(0, 15));
            Walk(c, LookSpot);
            Check("onto the side path to the bank: found on the first cell within 2, once, 「そらをとべるばしょを みつけた！」",
                t.IsFound(Pond) && Mathf.Abs(foundAt.x - LookSpot.x) + Mathf.Abs(foundAt.y - LookSpot.y) == 2 && message == PocFastTravel.MessageFound,
                $"found at {foundAt}: {message}");
            Walk(c, new Vector2Int(0, 15));
            c.Hud.ClearMessage();
            Walk(c, LookSpot);
            Check("walking back to it: no second message", t.Discoveries == 1 && C6.Shown(c) != PocFastTravel.MessageFound);
            // The list and the flights.
            t.SetFound(new[] { Plaza, Pond });
            c.Hud.ClearMessage();
            c.Menu.SetOpen(true, C6.Clock);
            c.Menu.ActivateLink(c.Menu.LinkLabels.ToList().IndexOf(PocFastTravel.MenuLabel), C6.Clock);
            Check("the list: ハルカチャンの家 / ひがしのはなのひろば / きたのみずべ / やめる (one page)",
                Buttons(c) == "ハルカチャンの家/ひがしのはなのひろば/きたのみずべ/やめる", Buttons(c));
            c.Menu.SetOpen(false, C6.Clock);
            Check("following for the flights", StartFollowing(c) || c.Crow.Following);
            var anchor = c.Clock.AnchorMilliseconds;
            void Leg(string label, string id, Vector2Int arrival, string region, PocViewMode view)
            {
                c.Switcher.SetMode(view);
                Settle(c, 0.3f);
                var steps = c.Session.CompletedSteps;
                var ripples = c.Presenter.RipplesStarted;
                var started = t.TryBegin(id, C6.Clock, FastTravelSource.Check);
                Fly(c);
                Settle(c, 0.5f);
                Check(label, started == FastTravelStart.Started && c.Session.Cell == arrival && c.Session.CurrentRegionMapId == region &&
                    t.LastArrivalVerified && c.Session.CompletedSteps == steps && c.Crow.Following && c.Crow.DistanceToPlayer <= 3 &&
                    !IsWater(c, c.Crow.Cell) && c.Switcher.Mode == view && c.Presenter.RipplesStarted == ripples && c.Session.IsCellReady(c.Session.Cell),
                    $"{started} {Where(c)} {t.LastArrivalProblem}");
            }
            c.Session.PlaceForCheck(HomeArrival + Vector2Int.right * 2, FacingDirection.Down);
            Settle(c, 1f);
            Leg("home -> North (2D): on the bank (-3,15), north_area_01, no step, the crow beside her on land, no ripple by itself", Pond, PondArrival, NorthId, PocViewMode.PixelView2D);
            Leg("North -> home (3D)", Home, HomeArrival, "first_field", PocViewMode.ThirdPerson3D);
            Leg("home -> East (3D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.ThirdPerson3D);
            Leg("East -> North (2D)", Pond, PondArrival, NorthId, PocViewMode.PixelView2D);
            Leg("North -> East (2D)", Plaza, PlazaArrival, V33.EastId, PocViewMode.PixelView2D);
            Leg("East -> North (3D)", Pond, PondArrival, NorthId, PocViewMode.ThirdPerson3D);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            Check("the day/night clock untouched by the flights", c.Clock.AnchorMilliseconds == anchor);
            Check("landing on the pond is never chosen: a place whose arrival is a water cell lands on the bank",
                t.TryFindLanding(new FastTravelPoint { id = "check_water", mapId = NorthId, arrivalCell = north.ToLocal(Lily), landingSearchRadius = 4 }, north, Lily, out var land) &&
                !IsWater(c, land) && c.Session.OutdoorIndex.CanEnter(land), land.ToString());
            Check("the arrival does not look into the water by itself (no ripple until A)", c.Presenter.ActiveRipples == 0);
            Check("the North chunks needed for the arrival are drawn", c.Session.IsCellReady(PondArrival) && c.Session.IsCellReady(Lily));
            // AUTO does not find it (a new game: started last, it replaces the one above).
            var a = Begin(new HarukaMemoryStorage());
            a.Session.PlaceForCheck(new Vector2Int(-1, 15), FacingDirection.Left);
            Frame(a, 1f / 60f, Vector2Int.zero);
            var stepped = a.Gameplay.AutoWorld.TryStep(Vector2Int.left);
            Settle(a, 0.4f);
            Check("an AUTO step onto (-2,15) (2 from the bank spot): nothing found", stepped && a.Session.Cell == new Vector2Int(-2, 15) && !T(a).IsFound(Pond));
        }

        // ---------------------------------------------------------------- 17.6 the save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            // Saved by the pond, following, the pond and the plaza found; closed and opened.
            {
                var storage = new HarukaMemoryStorage();
                var c = Begin(storage);
                T(c).SetFound(new[] { Plaza });
                StartFollowing(c);
                Walk(c, LookSpot);
                C6.Step(c, Vector2Int.up, 0.3f);   // (into the water: refused, she faces it)
                Settle(c, 2f);
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                Settle(c, 1.5f);
                c.Save.OnPageHidden("");
                var latest = C6.Latest(c);
                Check("saved by the pond: north_area_01 local (6,8), facing up, 3D, following, places east + home + north",
                    latest.MapId == NorthId && latest.CellX == 6 && latest.CellY == 8 && latest.Facing == "up" && latest.ViewMode == HarukaSaveData.View3D &&
                    latest.CrowMode == HarukaSaveData.CrowFollow && latest.FastTravel != null &&
                    latest.FastTravel.SequenceEqual(new[] { Plaza, Home, Pond }), HarukaSaveSystem.Describe(latest));
                var r = Begin(storage);
                Check("opened again: the same cell, facing, 3D, the crow following beside her, the places, the chunk drawn",
                    r.Session.Cell == LookSpot && r.Session.CurrentRegionMapId == NorthId && r.Session.Facing == FacingDirection.Up &&
                    r.Switcher.Mode == PocViewMode.ThirdPerson3D && r.Crow.Following && r.Crow.DistanceToPlayer <= 6 && !IsWater(r, r.Crow.Cell) &&
                    T(r).IsFound(Pond) && T(r).IsFound(Plaza) && r.Session.IsCellReady(LookSpot), Where(r));
                Check("and she can fly home and East from there", T(r).TryBegin(Home, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == HomeArrival && T(r).TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == PlazaArrival);
                // Export / import.
                var exported = C6.Sealed(latest);
                var t2 = Begin(new HarukaMemoryStorage());
                Settle(t2, 1.2f);
                var check = t2.Save.CheckImport(exported, out var data);
                var ok = t2.Save.ApplyImport(data, C6.Clock, out var problem);
                Settle(t2, 0.5f);
                Check("export / import of the North save: checked, applied by the pond with its places and the companion",
                    check == HarukaSaveSystem.ImportCheck.Ok && ok && t2.Session.Cell == LookSpot && T(t2).IsFound(Pond) && t2.Crow.Following, problem);
                var unknown = latest.Copy();
                unknown.MapId = "north_area_99";
                Check("a save naming a North area that does not exist is refused (UnknownMap)", t2.Save.CheckImport(C6.Sealed(unknown), out _) == HarukaSaveSystem.ImportCheck.UnknownMap);
                var outside = latest.Copy();
                outside.CellY = 40;
                Check("a cell outside the North Map is refused (OutOfMap)", t2.Save.CheckImport(C6.Sealed(outside), out _) == HarukaSaveSystem.ImportCheck.OutOfMap);
            }
            // A save on a water cell (damaged by hand): resumed on the nearest bank cell of the North area.
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", NorthId, new Vector2Int(6, 11));
                data.GameVersion = "ver3.6";
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("a save on a water cell: she resumes on the nearest walkable cell of the North area, not in the water",
                    g.Session.CurrentRegionMapId == NorthId && !IsWater(g, g.Session.Cell) && g.Session.OutdoorWorld.CanEnter(g.Session.Cell) &&
                    g.Save.LocationNote.Contains("nearest"), $"{g.Session.Cell} {g.Save.LocationNote}");
            }
            // Old saves: every version; the North place not found (normal), the ver3.5 places kept.
            var places = new (string version, string map, Vector2Int cell, string[] found)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0), null), ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1), null),
                ("ver3.2", "halka_house", new Vector2Int(-2, 0), null), ("ver3.3", V33.EastId, new Vector2Int(9, 6), null),
                ("ver3.4", V33.EastId, new Vector2Int(4, 6), null), ("ver3.5", "first_field", new Vector2Int(-7, -5), new[] { Plaza, Home }),
            };
            foreach (var (version, map, cell, found) in places)
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", map, cell);
                data.GameVersion = version;
                if (found != null) data.FastTravel = new List<string>(found);
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Settle(g, 0.5f);
                Check($"a {version} save ({map}): resumed there; North not found; " + (found != null ? "the house and the plaza kept" : "the house"),
                    g.Session.TryGetSavePlace(out var m, out var l) && m == map && l == cell && !T(g).IsFound(Pond) &&
                    (found == null ? T(g).FoundIds.SequenceEqual(new[] { Home }) : T(g).IsFound(Plaza) && T(g).IsFound(Home)), string.Join(",", T(g).FoundIds));
            }
            // A damaged newest snapshot: the older one (by the pond) is loaded.
            {
                var storage = new HarukaMemoryStorage();
                var g = Begin(storage);
                Walk(g, LookSpot);
                Settle(g, 2f);
                g.Save.Write("check", C6.Clock);
                var latest = C6.Latest(g);
                var newest = HarukaSnapshotStore.SlotNames.First(n => HarukaSaveData.TryRead(storage.Read(n), out var d, out _) == HarukaSaveReadStatus.Valid && d.Revision == latest.Revision);
                storage.Write(newest, storage.Read(newest).Replace("\"" + NorthId + "\"", "\"north_area_02\""));
                var r = Begin(storage);
                Check("the newest snapshot damaged: the backup snapshot is loaded, North found, by the pond",
                    r.Save.LoadSource == "backup snapshot" && T(r).IsFound(Pond) && r.Session.CurrentRegionMapId == NorthId, $"{r.Save.LoadSource} {Where(r)}");
            }
            // Rollback: a build without the North area meets this save.
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old("ver3.3", "north_area_00", new Vector2Int(6, 8));
                data.TotalSteps = 4567;
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Check("what an older build does with a North save (its Map unknown to it): the first map's start, steps and play time kept",
                    g.Session.TryGetSavePlace(out var m, out _) && m == "first_field" && g.LifeLog.TotalSteps == 4567 && g.Save.LocationNote.Contains("unknown map"),
                    g.Save.LocationNote);
            }
        }

        // ---------------------------------------------------------------- 17.7 what was there

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
            Check("and out again", c.Session.World == c.Session.OutdoorWorld && c.Session.Cell == door + Vector2Int.down);
            var east = c.Session.OutdoorIndex.FindRegion(V33.EastId);
            Check("the East area as it was: its 26 x 13 cells, the sunflowers and the plaza's marker",
                east.Rect.Min == new Vector2Int(11, -6) && east.Rect.Max == new Vector2Int(36, 6) && east.Map.TryGetMarker(Plaza, out var m) && m == new Vector2Int(16, 4) &&
                !c.Session.OutdoorIndex.CanEnter(new Vector2Int(27, -1)));
            Walk(c, PlazaArrival);
            Check("the plaza is still found by walking there", T(c).IsFound(Plaza));
            // AUTO in first_field: stays in first_field (never walks North by itself).
            c.Session.PlaceForCheck(new Vector2Int(0, 4), FacingDirection.Up);
            c.Hud.ClearMessage();
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var regions = new HashSet<string>();
            for (var t = 0f; t < 150f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero, true); regions.Add(c.Session.CurrentRegionMapId ?? c.Session.World.MapId); }
            Check("AUTO in first_field next to the North road for 150 s: it walks, but never into the North area", c.Gameplay.AutoWorld.AutoSteps > 0 &&
                !regions.Contains(NorthId), string.Join(",", regions));
            c.Auto.SetAutoEnabled(false, C6.Clock);
            Check("the menu and its rows (そらをとぶ, セーブデータ) are there", c.Menu.LinkLabels.Contains(PocFastTravel.MenuLabel) &&
                c.Menu.LinkLabels.Contains(HarukaSaveSystem.MenuLabel));
            Check("the Life Log counts steps as before", c.LifeLog.TotalSteps > 0);
        }

        // ---------------------------------------------------------------- what must not change

        private static void ProtectChecks()
        {
            const string Published = "d2441b7";
            Check("WORLD EDITOR v1.0 unchanged since ver3.5 (tools/HalkaWorldMapEditor)", V32.Git("diff --quiet " + Published + " -- tools/HalkaWorldMapEditor", out _));
            Check("first_field, east_area_01, the house and the booth Maps (Authoring JSON and caches) unchanged", OfficialMapsKept(Published));
            Check("direction_themes.json unchanged", V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/Maps/World/direction_themes.json", out _));
            Check("the Object Catalog: the existing definitions unchanged (only new ones added)", CatalogKept());
            Check("the published ver3.5 (halkaworld/) unchanged", V32.Git("diff --quiet " + Published + " -- halkaworld", out _));
            Check("the crow's sprites, model and voice, and the font atlas unchanged", CrowKept(Published, true));
            Check("the old tests are all still there (none deleted)", V32.Git("diff --quiet --diff-filter=D " + Published + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _));
        }

        // Every definition of the ver3.5 catalog is still there, unchanged: the change only adds lines
        // (two lines change only to take a comma, where the lists go on).
        private static bool CatalogKept() => OnlyAdded("d2441b7", Catalog) && V32.Git("diff --numstat d2441b7 -- " + Catalog, out var stat) && stat.Trim().Length > 0;

        // ---------------------------------------------------------------- shared with the ver3.4 / ver3.5 checks
        // Their whole-folder comparisons with an old commit fail on anything new in a folder; since
        // ver3.6 adds a Region (a Map, catalog entries, a registry line, pictures and models) they ask
        // what they meant instead: the existing files unchanged, the shared list files only added to.

        private const string Catalog = "unity-project/Assets/Content/Maps/Authoring/object_catalog.hwcatalog.json";
        private const string Registry = "unity-project/Assets/Content/Maps/World/outdoor_main.hwworld.json";

        // A JSON list file since a commit: unchanged, or lines added only (a line that ended a list may
        // come back with a comma, where the list goes on).
        internal static bool OnlyAdded(string commit, string path)
        {
            if (!V32.Git("diff -U0 " + commit + " -- " + path, out var diff)) return false;
            var lines = diff.Replace("\r", "").Split('\n');
            var added = lines.Where(l => l.StartsWith("+") && !l.StartsWith("+++")).Select(l => l.Substring(1).TrimEnd()).ToList();
            return lines.Where(l => l.StartsWith("-") && !l.StartsWith("---")).Select(l => l.Substring(1).TrimEnd()).All(l => added.Contains(l + ","));
        }

        // The Maps folder since a commit: no file changed or removed but the catalog and the world
        // registry, and those two only added to (new Maps and their caches may be there).
        internal static bool MapsFolderKept(string commit) =>
            V32.Git("diff --quiet --diff-filter=MDR " + commit + " -- unity-project/Assets/Content/Maps :(exclude)" + Catalog + " :(exclude)" + Registry, out _) &&
            OnlyAdded(commit, Catalog) && OnlyAdded(commit, Registry);

        // The world registry since a commit: only added to; direction_themes.json unchanged.
        internal static bool RegistryKept(string commit) =>
            OnlyAdded(commit, Registry) && V32.Git("diff --quiet " + commit + " -- unity-project/Assets/Content/Maps/World/direction_themes.json", out _);

        // first_field, east_area_01, the house and the booth: Authoring JSON and caches unchanged.
        internal static bool OfficialMapsKept(string commit) =>
            V32.Git("diff --quiet " + commit + " -- " + string.Join(" ", new[] { "first_field", "east_area_01", "halka_house", "halka_soundbooth" }
                .SelectMany(m => new[] { "unity-project/Assets/Content/Maps/Authoring/" + m + ".hwmap.json", "unity-project/Assets/Content/Maps/" + m + ".asset" })), out _);

        // The crow's own files (its sprites and model), the voices and (from ver3.5) the font atlas unchanged.
        // (the model must be there: a path that does not exist would compare as unchanged)
        internal static bool CrowKept(string commit, bool fontAtlas) =>
            File.Exists(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/POC2D3D/Models/Phase4/crow_basic.fbx")) &&
            V32.Git("diff --quiet " + commit + " -- unity-project/Assets/POC2D3D/Models/Phase4/crow_basic.fbx unity-project/Assets/Content/Audio" + (fontAtlas ? " unity-project/Assets/Resources" : ""), out _) &&
            V32.Git("diff --quiet " + commit + " -- " + string.Join(" ", Directory.GetFiles(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project/Assets/Content/World"), "crow*")
                .Select(f => "unity-project/Assets/Content/World/" + Path.GetFileName(f))), out _);
    }
}
