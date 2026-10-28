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
    // はるかわーるど ver3.5「そらをとぶ」: the checks of the fast travel (the points, finding the plaza on
    // foot, the crow's choices and the menu row, the flight and its landing, the crow's modes, the
    // save). The game is started with the Phase 6 harness (memory storage only); every frame also runs
    // what Update does in play (the crow unless a flight holds it, the choices' watch, the chunk
    // streamer, the flight). Every check prints PASS or FAIL.
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer35Checks.RunBatch
    public static class WorldVer35Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Home = "landmark_halka_home";
        private const string Plaza = "landmark_east_flower_plaza";
        private static readonly Vector2Int HomeArrival = new Vector2Int(-7, -5);   // world = first_field cell
        private static readonly Vector2Int PlazaArrival = new Vector2Int(27, -2);  // east_area_01 (16,4)
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";
        private static int maxCrows;

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
            UnityEngine.Random.InitState(3535);
            try
            {
                Run("points", PointChecks);
                Run("unlock", UnlockChecks);
                Run("talk", TalkChecks);
                Run("menu", MenuChecks);
                Run("travel", TravelChecks);
                Run("landing", LandingChecks);
                Run("crow", CrowChecks);
                Run("save", SaveChecks);
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
            foreach (var failure in failures) Debug.LogError("VER35 FAIL " + failure);
            Debug.Log("HARUKA ver3.5 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.5 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V35_LOGS", "ver35_checks.txt");
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
            if (condition) { passes++; Debug.Log("VER35 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness

        private static PocFastTravel T(C6.Ctx c) => c.Gameplay.FastTravel;

        // The game's start (C6.Start) and the fast travel's own Awake / Start, on the checks' clocks.
        private static C6.Ctx Begin(HarukaMemoryStorage storage)
        {
            var c = C6.Start(storage);
            var t = T(c);
            C6.Invoke(t, "Awake");
            t.CrowTime = () => C6.Clock;
            t.StartPlay();
            Frame(c, 1f / 60f, Vector2Int.zero);
            return c;
        }

        // One frame as the game plays it: the session, AUTO (as its Update would: paused while play is
        // suspended or a flight is under way), the crow (unless a flight holds it), the choices' watch,
        // the views, the chunk streamer, the flight.
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
            T(c).Tick(C6.Clock);
            maxCrows = Mathf.Max(maxCrows, Crows());
        }

        private static void Settle(C6.Ctx c, float seconds, bool auto = false)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, auto);
        }

        // Plays until the flight is over (at most 6 s).
        private static bool Fly(C6.Ctx c, Action onFrame = null)
        {
            for (var i = 0; i < 360 && T(c).Busy; i++) { Frame(c, 1f / 60f, Vector2Int.zero); onFrame?.Invoke(); }
            return !T(c).Busy;
        }

        // Walks there holding the keys (as in the ver3.4 checks; the crow is pushed aside, not routed round).
        private static bool Walk(C6.Ctx c, Vector2Int goal, float maxSeconds = 60f)
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

        private static void PlaceCrow(C6.Ctx c, Vector2Int cell) =>
            typeof(CrowBehaviourCore).GetMethod("PlaceOnGround", Hidden).Invoke(c.Crow, new object[] { cell, FacingDirection.Down, C6.Clock });

        // The free crow stands still for a talk: no flight, no wander, no cooldown.
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

        // She stands next to the crow, facing it, and presses A (the ver3.4 checks' way).
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

        private static int LinkIndex(C6.Ctx c, string label)
        {
            for (var i = 0; i < c.Menu.LinkLabels.Count; i++)
                if (c.Menu.LinkLabels[i] == label) return i;
            return -1;
        }

        // The menu (☰) and its そらをとぶ row.
        private static bool OpenFromMenu(C6.Ctx c)
        {
            c.Hud.ClearMessage();
            c.Menu.SetOpen(true, C6.Clock);
            var index = LinkIndex(c, PocFastTravel.MenuLabel);
            if (!c.Menu.IsLinkShown(index)) return false;
            c.Menu.ActivateLink(index, C6.Clock);
            return T(c).Page.IsOpen;
        }

        private static string NameOf(C6.Ctx c, string id) => T(c).Registry.Find(id).name;

        private static string Where(C6.Ctx c) =>
            $"her {c.Session.Cell} {c.Session.CurrentRegionMapId ?? c.Session.World.MapId} {c.Session.Facing} crow {c.Crow.Cell} {c.Crow.CompanionState} " +
            $"mode {c.Crow.Mode} phase {T(c).Phase} found [{string.Join(",", T(c).FoundIds)}]";

        private static int Crows() => UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private static FastTravelRegistry CopyOf(FastTravelRegistry registry) => new FastTravelRegistry
        {
            points = registry.points.Select(p => JsonUtility.FromJson<FastTravelPoint>(JsonUtility.ToJson(p))).ToList()
        };

        private static bool HasError(List<string> problems, string part) =>
            problems.Any(p => p.StartsWith("ERROR", StringComparison.Ordinal) && p.Contains(part));

        // ---------------------------------------------------------------- 16.1 the points

        private static void PointChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var registry = t.Registry;
            var index = c.Session.OutdoorIndex;
            bool Door(Vector2Int cell) => c.Session.IsTransitionCell(c.Session.OutdoorWorld, cell);
            // ver3.6: more places may follow (landmark_north_blue_pond); these two stay as they were.
            Check("the registry has the two places: ハルカチャンの家 (landmark_halka_home) and ひがしのはなのひろば (landmark_east_flower_plaza)",
                registry.points.Count >= 2 && registry.Find(Home) != null && registry.Find(Plaza) != null &&
                NameOf(c, Home) == "ハルカチャンの家" && NameOf(c, Plaza) == "ひがしのはなのひろば", string.Join(",", registry.points.Select(p => p.id)));
            var problems = registry.Validate(index, Door);
            Check("the registry passes its checks against the outdoor world (no error, no warning)", problems.Count == 0, string.Join("; ", problems));
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(PocSceneBuilder.FastTravelPointsPath);
            var parsedProblems = new List<string>();
            var parsed = OutdoorWorldRegistryJson.ParseFastTravel(file.text, parsedProblems);
            Check("the scene's points are fast_travel_points.json's (format harukaworld-fast-travel v1)",
                parsed != null && parsed.points.Count == registry.points.Count &&
                parsed.points.Zip(registry.points, (a, b) => a.id == b.id && a.arrivalCell == b.arrivalCell && a.discoverCell == b.discoverCell).All(x => x),
                string.Join("; ", parsedProblems));
            var home = registry.Find(Home);
            index.TryGetHouseDoor(out var door);
            Check("the house: unlocked at the start, in first_field, outside in front of the house (-7,-5), facing down",
                home.unlockedAtStart && home.mapId == "first_field" && home.worldId == "outdoor_main" &&
                index.FindRegion("first_field").ToWorld(home.arrivalCell) == HomeArrival && home.ArrivalFacing == FacingDirection.Down);
            Check("the house's arrival cell is walkable and not the door (the door is the cell above it)",
                index.CanEnter(HomeArrival) && !Door(HomeArrival) && door == HomeArrival + Vector2Int.up, $"door {door}");
            var plaza = registry.Find(Plaza);
            var east = index.FindRegion(V33.EastId);
            Check("the plaza keeps its ver3.3 id (landmark_east_flower_plaza) and the Map's marker cell (16,4) = World (27,-2)",
                !plaza.unlockedAtStart && plaza.mapId == V33.EastId && east.Map.TryGetMarker(Plaza, out var marker) && marker == plaza.discoverCell &&
                east.ToWorld(plaza.discoverCell) == PlazaArrival && east.ToWorld(plaza.arrivalCell) == PlazaArrival);
            Check("the plaza's arrival faces the sunflowers (up; World (27,-1) is blocked)", plaza.ArrivalFacing == FacingDirection.Up && !index.CanEnter(PlazaArrival + Vector2Int.up));
            Check("every name and label is drawn by the game's font",
                new[] { PocFastTravel.MenuLabel, PocFastTravel.PageLine, PocFastTravel.CancelLabel, PocFastTravel.NextLabel, PocFastTravel.PreviousLabel,
                    PocFastTravel.MessageFound, PocFastTravel.MessageHere, PocFastTravel.MessageFailed, PocCrow.ChoiceTravel }
                    .Concat(registry.points.Select(p => p.name)).All(GameBitmapFont.HasAllGlyphs));
            // Broken registries (copies).
            var dup = CopyOf(registry); dup.points[1].id = Home;
            Check("a duplicate id is an error", HasError(dup.Validate(index, Door), "twice"));
            var badId = CopyOf(registry); badId.points[1].id = "Plaza!";
            Check("a malformed id is an error", HasError(badId.Validate(index, Door), "invalid point id"));
            var world = CopyOf(registry); world.points[1].worldId = "outdoor_other";
            Check("an unknown World ID is an error", HasError(world.Validate(index, Door), "unknown world"));
            // ver3.6: north_area_01 exists now; a region that does not.
            var region = CopyOf(registry); region.points[1].mapId = "south_area_99";
            Check("an unknown region (a south area that does not exist) is an error", HasError(region.Validate(index, Door), "unknown region"));
            var outside = CopyOf(registry); outside.points[1].arrivalCell = new Vector2Int(99, 4);
            Check("an arrival cell outside its Map is an error", HasError(outside.Validate(index, Door), "outside"));
            var blocked = CopyOf(registry); blocked.points[1].arrivalCell = new Vector2Int(16, 5);
            var blockedProblems = blocked.Validate(index, Door);
            Check("a blocked arrival cell is only a warning (the nearest safe cell is used)",
                !FastTravelRegistry.HasErrors(blockedProblems) && blockedProblems.Any(p => p.StartsWith("WARNING")), string.Join("; ", blockedProblems));
            var noStart = CopyOf(registry); noStart.points[0].unlockedAtStart = false;
            Check("a registry without a start place is an error", HasError(noStart.Validate(index, Door), "unlocked at the start"));
            var radius = CopyOf(registry); radius.points[1].landingSearchRadius = 99;
            Check("a landing search radius over the limit (8) is an error", HasError(radius.Validate(index, Door), "landing search radius"));
            Check("an unknown id is refused and changes nothing", t.TryBegin("landmark_nowhere", C6.Clock, FastTravelSource.Check) == FastTravelStart.UnknownPoint && !t.Busy);
            Check("a point is not found by id case changes (ids are exact)", registry.Find("Landmark_Halka_Home") == null);
        }

        // ---------------------------------------------------------------- 16.2 finding a place

        private static void UnlockChecks()
        {
            C6.SetLegacy(null, null);
            var storage = new HarukaMemoryStorage();
            var c = Begin(storage);
            var t = T(c);
            Check("a new game: only the house is found (written as nothing: the save has no fastTravel entry)",
                t.FoundIds.SequenceEqual(new[] { Home }) && t.SavedIds() == null && t.FoundPoints().Select(p => p.id).SequenceEqual(new[] { Home }));
            Check("the plaza cannot be flown to before it is found", t.TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.NotDiscovered && !t.Busy);
            Walk(c, new Vector2Int(12, -1));
            Check("into the East area (12,-1): the region alone does not find the plaza", !t.IsFound(Plaza) && c.Session.CurrentRegionMapId == V33.EastId, Where(c));
            var foundAt = new Vector2Int(int.MinValue, 0);
            var message = "";
            t.Discovered += point => { foundAt = c.Session.Cell; message = C6.Shown(c); };
            Walk(c, new Vector2Int(22, -1));
            Check("walking on to (22,-1): not yet (more than 2 cells from the plaza's front)", !t.IsFound(Plaza), Where(c));
            Walk(c, PlazaArrival);
            var distance = Mathf.Abs(foundAt.x - PlazaArrival.x) + Mathf.Abs(foundAt.y - PlazaArrival.y);
            Check("walking to the plaza: found on the first cell within 2 of its front (once)", t.IsFound(Plaza) && t.Discoveries == 1 && distance == 2, $"found at {foundAt}");
            Check("the message: 「そらをとべるばしょを みつけた！」", message == PocFastTravel.MessageFound, message);
            Settle(c, 1.5f);
            Check("saved within about a second: extensions.fastTravel holds both ids (sorted)",
                C6.Latest(c)?.FastTravel != null && C6.Latest(c).FastTravel.SequenceEqual(new[] { Plaza, Home }), HarukaSaveSystem.Describe(C6.Latest(c)));
            Walk(c, new Vector2Int(20, -1));
            c.Hud.ClearMessage();
            Walk(c, PlazaArrival);
            Check("walking away and back: not found again (no second message)", t.Discoveries == 1 && C6.Shown(c) != PocFastTravel.MessageFound);
            var r = Begin(storage);
            Check("the game opened again: the plaza is still found", T(r).IsFound(Plaza) && T(r).FoundPoints().Count == 2, Where(r));
            // AUTO never finds a place: an AUTO step into the radius, then a step of her own.
            var a = Begin(new HarukaMemoryStorage());
            a.Session.PlaceForCheck(new Vector2Int(25, -1), FacingDirection.Right);
            Frame(a, 1f / 60f, Vector2Int.zero);
            var stepped = a.Gameplay.AutoWorld.TryStep(Vector2Int.right);
            Settle(a, 0.4f);
            Check("an AUTO step to (26,-1), 2 cells from the plaza's front: nothing found", stepped && a.Session.Cell == new Vector2Int(26, -1) && !T(a).IsFound(Plaza) && a.Gameplay.LastStepByAuto, Where(a));
            C6.Step(a, Vector2Int.left, 0.4f);
            C6.Step(a, Vector2Int.right, 0.4f);
            Check("then her own step back into it: found", T(a).IsFound(Plaza) && !a.Gameplay.LastStepByAuto, Where(a));
            // AUTO walking on its own near the plaza (60 s idle, then its walk): no place found.
            var b = Begin(new HarukaMemoryStorage());
            b.Session.PlaceForCheck(new Vector2Int(20, 0), FacingDirection.Right);
            b.Auto.SetAutoEnabled(true, C6.Clock);
            Settle(b, 150f, true);
            Check("AUTO in the East area for 150 s: it walks, never finds the plaza", b.Gameplay.AutoWorld.AutoSteps > 0 && !T(b).IsFound(Plaza),
                $"AUTO steps {b.Gameplay.AutoWorld.AutoSteps} at {b.Session.Cell}");
            b.Auto.SetAutoEnabled(false, C6.Clock);
            // Old saves (no fastTravel entry): found at the start only if she stands at the plaza.
            var at = new HarukaMemoryStorage();
            at.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(V32.Old("ver3.3", V33.EastId, new Vector2Int(16, 3))));
            var g = Begin(at);
            Check("a ver3.3 save at the plaza (16,3): found after the load, with the message", T(g).IsFound(Plaza) && T(g).Discoveries == 1, Where(g));
            Settle(g, 1.5f);
            Check("and saved (extensions.fastTravel written)", C6.Latest(g)?.HasFastTravel == true, HarukaSaveSystem.Describe(C6.Latest(g)));
            var away = new HarukaMemoryStorage();
            away.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(V32.Old("ver3.3", V33.EastId, new Vector2Int(9, 6))));
            var h = Begin(away);
            Check("a ver3.3 save elsewhere in the East area (9,6): the plaza is not found (no guess from the past)", !T(h).IsFound(Plaza), Where(h));
            Walk(h, PlazaArrival);
            Check("until she walks there", T(h).IsFound(Plaza), Where(h));
        }

        // ---------------------------------------------------------------- 16.3 the crow's choices

        private static void TalkChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var crow = c.Crow;
            var talked = TalkToCrow(c);
            Check("a free crow: 「カァ。」 and ついてきて / そらをとぶ / やめる",
                talked && crow.ChoiceOpen && Buttons(c) == PocCrow.ChoiceFollow + "/" + PocCrow.ChoiceTravel + "/" + PocCrow.ChoiceCancel &&
                c.Menu.Page.Lines[0] == PocCrow.ChoiceLine, Buttons(c));
            var before = (c.Session.Cell, c.Session.Facing, crow.Mode, crow.Cell, Revision: C6.Latest(c)?.Revision ?? -1);
            Press(c, PocCrow.ChoiceTravel);
            Settle(c, 0.05f);
            Check("そらをとぶ: the destination list (そらをとぶ / どこへ とぶ？ / ハルカチャンの家 / やめる), the crow's choice closed without a takeoff",
                t.Page.IsOpen && c.Menu.Page.Title == PocFastTravel.PageTitle && c.Menu.Page.Lines[0] == PocFastTravel.PageLine &&
                Buttons(c) == NameOf(c, Home) + "/" + PocFastTravel.CancelLabel && !crow.ChoiceOpen && !crow.TalkTakeoffPending &&
                crow.Activity == CrowActivity.GroundIdle && t.LastSource == FastTravelSource.Crow, Buttons(c));
            Check("the list only has places found (the plaza is not there yet)", !Buttons(c).Contains(NameOf(c, Plaza)));
            // (that a key then moves nothing is the game's input rule, PocInput; checked in WebGL)
            Check("while the list is open: game input blocked, AUTO paused", c.Menu.BlocksGameplayInput && c.Auto.MenuSuspended);
            Press(c, PocFastTravel.CancelLabel);
            Settle(c, 0.5f);
            Check("やめる: the menu closes; her place, facing, the crow's mode and cell, the save: unchanged",
                c.Menu.Page == null && !c.Menu.IsOpen && c.Session.Cell == before.Item1 && c.Session.Facing == before.Item2 &&
                crow.Mode == before.Item3 && crow.Cell == before.Item4 && (C6.Latest(c)?.Revision ?? -1) == before.Revision && !t.Busy, Where(c));
            // Following: あそんでおいで / そらをとぶ / やめる; a touch outside the list closes it.
            Settle(c, 1f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoiceFollow);
            Settle(c, 1f);
            TalkToCrow(c);
            Check("following: あそんでおいで / そらをとぶ / やめる (the ver3.4 choices kept)",
                crow.Following && Buttons(c) == PocCrow.ChoicePlay + "/" + PocCrow.ChoiceTravel + "/" + PocCrow.ChoiceCancel, Buttons(c));
            Press(c, PocCrow.ChoiceTravel);
            var listOpen = t.Page.IsOpen;
            c.Menu.SetOpen(false, C6.Clock);
            Settle(c, 0.3f);
            Check("the list closed by a touch outside it: nothing changes (still following, no flight)", listOpen && crow.Following && !t.Busy && c.Menu.Page == null, Where(c));
            // Choosing a place starts the flight.
            t.SetFound(new[] { Plaza });
            Settle(c, 1f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoiceTravel);
            Check("found: the plaza is in the list after the house", Buttons(c) == NameOf(c, Home) + "/" + NameOf(c, Plaza) + "/" + PocFastTravel.CancelLabel, Buttons(c));
            Press(c, NameOf(c, Plaza));
            Check("a place chosen: the menu closes and the flight starts (from the crow)", !c.Menu.IsOpen && t.Busy && t.Phase == FastTravelPhase.DepartureAnimation &&
                t.LastSource == FastTravelSource.Crow, t.Phase.ToString());
            Check("during the flight: talking to the crow and pressing A do nothing", !c.Session.InteractAhead() && !crow.ChoiceOpen);
            Fly(c);
            Check("landed at the plaza", c.Session.Cell == PlazaArrival && !t.Busy, Where(c));
        }

        // ---------------------------------------------------------------- 16.4 the menu row

        private static void MenuChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var link = LinkIndex(c, PocFastTravel.MenuLabel);
            c.Menu.SetOpen(true, C6.Clock);
            Check("outdoors: the menu has the row そらをとぶ (and keeps セーブデータ and やめる)",
                link >= 0 && c.Menu.IsLinkShown(link) && c.Menu.LinkLabels.Contains(HarukaSaveSystem.MenuLabel) && c.Menu.CloseLabel == HarukaSaveSystem.CloseLabel &&
                c.Menu.TryGetLinkRect(link, out _), string.Join(",", c.Menu.LinkLabels));
            c.Menu.SetOpen(false, C6.Clock);
            t.SetFound(new[] { Plaza });
            // The crow far away (its own life in first_field); she is at the plaza.
            c.Session.PlaceForCheck(PlazaArrival + Vector2Int.left * 4, FacingDirection.Right);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var far = Mathf.Abs(c.Crow.Cell.x - c.Session.Cell.x) + Mathf.Abs(c.Crow.Cell.y - c.Session.Cell.y);
            Check("the menu's row opens the list wherever the crow is (here 10+ cells away, free)", far >= 10 && !c.Crow.Following && OpenFromMenu(c) &&
                Buttons(c) == NameOf(c, Home) + "/" + NameOf(c, Plaza) + "/" + PocFastTravel.CancelLabel && t.LastSource == FastTravelSource.Menu, $"crow {far} cells away; {Buttons(c)}");
            Press(c, PocFastTravel.CancelLabel);
            Settle(c, 0.2f);
            Check("やめる from the menu's list: closed, nothing changed", !c.Menu.IsOpen && !t.Busy && c.Session.Cell == PlazaArrival + Vector2Int.left * 4);
            c.Auto.SetAutoEnabled(true, C6.Clock);
            OpenFromMenu(c);
            Press(c, NameOf(c, Home));
            Check("during the flight: the menu is off (no ☰, no 3Dへ), AUTO paused (its setting kept)",
                t.Busy && !c.Menu.enabled && !c.Menu.QuickButtonShown && c.Gameplay.AutoWorld.Suspended && c.Auto.AutoEnabled);
            var autoSteps = c.Gameplay.AutoWorld.AutoSteps;
            for (var i = 0; i < 360 && t.Busy; i++) Frame(c, 1f / 60f, Vector2Int.zero, true);
            Check("AUTO took no step during the flight; she landed at the house", c.Gameplay.AutoWorld.AutoSteps == autoSteps && c.Session.Cell == HomeArrival, Where(c));
            Check("after the flight: the menu is back, AUTO still ON (not walking yet: its 60 s idle starts again)",
                c.Menu.enabled && c.Auto.AutoEnabled && !c.Auto.Active && !c.Gameplay.AutoWorld.Suspended);
            c.Auto.SetAutoEnabled(false, C6.Clock);
            // Indoors: no row, no list.
            V32.Step(c, Vector2Int.up, 0.4f);
            c.Menu.SetOpen(true, C6.Clock);
            Check("in the house: the row is not shown, its press does nothing, the list does not open",
                c.Session.World.MapId == "halka_house" && !c.Menu.IsLinkShown(link) && !c.Menu.TryGetLinkRect(link, out _) &&
                ActivateDoesNothing(c, link) && !t.Open(C6.Clock, FastTravelSource.Menu), c.Session.World.MapId);
            Check("in the house: a flight is refused (NotOutdoors)", t.TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.NotOutdoors && !t.Busy);
            c.Menu.SetOpen(false, C6.Clock);
            V32.WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.14f);
            V32.Step(c, Vector2Int.up, 0.4f);
            c.Menu.SetOpen(true, C6.Clock);
            Check("in the booth: the row is not shown either", c.Session.World.MapId == "halka_soundbooth" && !c.Menu.IsLinkShown(link), c.Session.World.MapId);
            Check("the other rows still work (せいかつきろく, セーブデータ)", OpensLifeLogAndSave(c));
            c.Menu.SetOpen(false, C6.Clock);
        }

        private static bool ActivateDoesNothing(C6.Ctx c, int link)
        {
            c.Menu.ActivateLink(link, C6.Clock);
            return c.Menu.Page == null;
        }

        private static bool OpensLifeLogAndSave(C6.Ctx c)
        {
            c.Menu.OpenLifeLog(C6.Clock);
            var life = c.Menu.IsLifeLogOpen;
            c.Menu.BackFromLifeLog(C6.Clock);
            c.Menu.ActivateLink(LinkIndex(c, HarukaSaveSystem.MenuLabel), C6.Clock);
            var save = c.Menu.Page is HarukaSaveSystem.SaveMenu;
            c.Menu.ClosePage(C6.Clock);
            return life && save;
        }

        // ---------------------------------------------------------------- 16.5 a flight

        private static void TravelChecks()
        {
            C6.SetLegacy(null, null);
            var storage = new HarukaMemoryStorage();
            var c = Begin(storage);
            var t = T(c);
            t.SetFound(new[] { Plaza });
            Settle(c, 1.5f);
            var steps = c.Session.CompletedSteps;
            var lifeSteps = c.LifeLog.TotalSteps;
            var loads = c.Session.MapLoads;
            var anchor = c.Clock.AnchorMilliseconds;
            var play = c.LifeLog.TotalPlaySeconds;
            var relocations = c.Session.Relocations;
            var departure = c.Session.Cell;
            Check("home -> east (2D): the flight starts", t.TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && t.Busy);
            var heldOk = true;
            var phases = new HashSet<FastTravelPhase>();
            var midSaveOk = true;
            Fly(c, () =>
            {
                phases.Add(t.Phase);
                if (t.Busy && (!t.TryGetHeldPlace(out var m, out var cell, out _) || m != "first_field" || cell != departure)) heldOk = false;
                var snapshot = C6.Latest(c);
                if (t.Busy && snapshot != null && snapshot.MapId != "first_field") midSaveOk = false;
            });
            Check("its phases: departure, transfer behind the sky, arrival, then idle",
                phases.Contains(FastTravelPhase.DepartureAnimation) && phases.Contains(FastTravelPhase.Transferring) && phases.Contains(FastTravelPhase.ArrivalAnimation) && !t.Busy,
                string.Join(",", phases));
            Check("about 2.4 s from the choice to control again (1.5-3 s)", t.LastFlightSeconds >= 1.5f && t.LastFlightSeconds <= 3f, t.LastFlightSeconds.ToString("0.00"));
            Check("landed: the plaza's front (27,-2), East area, facing the sunflowers", c.Session.Cell == PlazaArrival &&
                c.Session.CurrentRegionMapId == V33.EastId && c.Session.Facing == FacingDirection.Up, Where(c));
            Check("the landing was verified (place, region, chunk drawn, Collision, input back, no step, the crow)", t.LastArrivalVerified, t.LastArrivalProblem);
            Check("no step counted (session and Life Log), no map load (the outdoor world stays), one relocation",
                c.Session.CompletedSteps == steps && c.LifeLog.TotalSteps == lifeSteps && c.Session.MapLoads == loads &&
                c.Session.World == c.Session.OutdoorWorld && c.Session.Relocations == relocations + 1);
            Check("the day/night clock untouched (same anchor), play time kept (not reset)", c.Clock.AnchorMilliseconds == anchor && c.LifeLog.TotalPlaySeconds >= play);
            Check("the save held the departure place during the flight", heldOk && midSaveOk);
            Check("and saved the destination as soon as she landed", C6.Latest(c)?.MapId == V33.EastId && C6.Latest(c).CellX == 16 && C6.Latest(c).CellY == 4 &&
                c.Save.LastWriteReason == "fast travel", HarukaSaveSystem.Describe(C6.Latest(c)));
            Check("2D stays 2D; the 2D camera is at the plaza (snapped, not sliding from the house)", c.Switcher.Mode == PocViewMode.PixelView2D &&
                Mathf.Abs(c.Pixel.WorldCamera.transform.position.x - GridSpace3D.ToWorld(PlazaArrival).x) < 12f, c.Pixel.WorldCamera.transform.position.ToString());
            Check("the chunk under her is drawn and the streamer is focused on her", c.Session.IsCellReady(PlazaArrival) &&
                c.Presenter.Streamer.FocusChunk == c.Session.OutdoorIndex.ChunkOfCell(PlazaArrival));
            C6.Step(c, Vector2Int.left, 0.4f);
            Check("her next step is an ordinary one (counted once)", c.Session.CompletedSteps == steps + 1 && c.LifeLog.TotalSteps == lifeSteps + 1, Where(c));
            // east -> home in 3D.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.5f);
            Check("east -> home (3D): the flight starts", t.TryBegin(Home, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started);
            Fly(c);
            var cam = c.Third.ViewCamera.transform.position;
            var her = GridSpace3D.ToWorld(c.Session.Cell);
            Check("landed in front of the house (-7,-5), facing down, outdoors (not inside), 3D stays 3D",
                c.Session.Cell == HomeArrival && c.Session.Facing == FacingDirection.Down && c.Session.World == c.Session.OutdoorWorld &&
                c.Session.CurrentRegionMapId == "first_field" && c.Switcher.Mode == PocViewMode.ThirdPerson3D && t.LastArrivalVerified, Where(c));
            Check("the 3D camera is behind her at the house (snapped, not sliding over the world)", new Vector2(cam.x - her.x, cam.z - her.z).magnitude < 12f, $"camera {cam} her {her}");
            Settle(c, 0.4f);
            Check("she does not walk in by herself (still outside after the landing)", c.Session.World == c.Session.OutdoorWorld);
            // The place she is at: nothing to fly to.
            var revision = C6.Latest(c).Revision;
            c.Hud.ClearMessage();
            OpenFromMenu(c);
            Press(c, NameOf(c, Home));
            Settle(c, 0.2f);
            Check("choosing the place she is at: 「もう ここに いるよ。」, no flight, nothing changes", !t.Busy && c.Session.Cell == HomeArrival &&
                C6.Shown(c) == PocFastTravel.MessageHere && C6.Latest(c).Revision == revision && t.LastStart == FastTravelStart.AlreadyHere, C6.Shown(c));
            // Walking from region to region is still seamless after flights.
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Session.PlaceForCheck(new Vector2Int(10, -1), FacingDirection.Right);
            Settle(c, 0.2f);
            var loadsBefore = c.Session.MapLoads;
            var changes = c.Session.RegionChanges;
            C6.Step(c, Vector2Int.right, 0.4f);
            Check("walking East over the region border: still one ordinary step (no load, a region change)", c.Session.Cell == new Vector2Int(11, -1) &&
                c.Session.MapLoads == loadsBefore && c.Session.RegionChanges == changes + 1);
            // At night.
            c.Time.Override = PocPhaseOverride.Night;
            Settle(c, 0.2f);
            Check("at night: the flight works the same (and the night stays)", t.TryBegin(Home, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(c) &&
                c.Session.Cell == HomeArrival && c.Time.Phase == WorldPhase.Night);
            c.Time.Override = PocPhaseOverride.Morning;
            Check("only one crow the whole time", maxCrows == 1, maxCrows.ToString());
        }

        // ---------------------------------------------------------------- 16.6 landing

        private static void LandingChecks()
        {
            C6.SetLegacy(null, null);
            var storage = new HarukaMemoryStorage();
            var c = Begin(storage);
            var t = T(c);
            var index = c.Session.OutdoorIndex;
            var east = index.FindRegion(V33.EastId);
            t.SetFound(new[] { Plaza });
            Check("a free arrival cell: she lands on it", t.TryFindLanding(t.Registry.Find(Plaza), east, PlazaArrival, out var free) && free == PlazaArrival);
            // The crow stands on the plaza's front.
            HoldCrow(c);
            PlaceCrow(c, PlazaArrival);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("the crow on the arrival cell: the nearest free cell instead (1 cell away, same row first)",
                t.TryFindLanding(t.Registry.Find(Plaza), east, PlazaArrival, out var beside) && beside != PlazaArrival &&
                Mathf.Abs(beside.x - PlazaArrival.x) + Mathf.Abs(beside.y - PlazaArrival.y) == 1 && beside.y == PlazaArrival.y, beside.ToString());
            Check("and the flight lands there (never on the crow)", t.TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(c) &&
                c.Session.Cell == beside && c.Crow.Cell != c.Session.Cell, Where(c));
            // A point on a blocked cell (the sunflowers), on the door, and one with no safe cell.
            var first = index.FindRegion("first_field");
            index.TryGetHouseDoor(out var door);
            var onDoor = new FastTravelPoint { id = "check_door", name = "check", mapId = "first_field", arrivalCell = first.ToLocal(door), landingSearchRadius = 4 };
            Check("an arrival on the house's door: a cell next to it, never the door (no walking in on landing)",
                t.TryFindLanding(onDoor, first, door, out var nearDoor) && nearDoor != door && !c.Session.IsTransitionCell(c.Session.OutdoorWorld, nearDoor) &&
                index.CanEnter(nearDoor), nearDoor.ToString());
            var sunflowers = PlazaArrival + Vector2Int.up;
            var onFlowers = new FastTravelPoint { id = "check_flowers", name = "check", mapId = V33.EastId, arrivalCell = east.ToLocal(sunflowers), landingSearchRadius = 4 };
            Check("an arrival on the sunflowers (blocked): the nearest walkable cell within the search radius",
                t.TryFindLanding(onFlowers, east, sunflowers, out var nearFlowers) && index.CanEnter(nearFlowers) &&
                Mathf.Abs(nearFlowers.x - sunflowers.x) + Mathf.Abs(nearFlowers.y - sunflowers.y) <= 4, $"{nearFlowers} after {t.LastLandingCandidates} cells");
            var none = new FastTravelPoint { id = "check_none", name = "check", worldId = "outdoor_main", mapId = V33.EastId, arrivalCell = east.ToLocal(sunflowers),
                landingSearchRadius = 0, arrivalFacing = "up" };
            t.Registry.points.Add(none);
            t.SetFound(new[] { Plaza, none.id });
            c.Session.PlaceForCheck(HomeArrival, FacingDirection.Down);
            Frame(c, 1f / 60f, Vector2Int.zero);
            var place = (c.Session.Cell, c.Session.Facing);
            Check("no safe cell within the radius: the flight is refused before anything changes (NoLandingCell)",
                t.TryBegin(none.id, C6.Clock, FastTravelSource.Check) == FastTravelStart.NoLandingCell && !t.Busy && c.Session.Cell == place.Item1 && !c.Crow.TravelHeld);
            t.Registry.points.Remove(none);
            // A destination whose chunk cannot be built: she stays where she was.
            var far = new FastTravelPoint { id = "check_far_east", name = "check", worldId = "outdoor_main", mapId = V33.EastId,
                arrivalCell = east.ToLocal(new Vector2Int(34, -1)), landingSearchRadius = 2, arrivalFacing = "right" };
            t.Registry.points.Add(far);
            t.SetFound(new[] { Plaza, far.id });
            c.Session.PlaceForCheck(HomeArrival, FacingDirection.Down);
            // Only the chunks near her drawn (3D, a short reach), so the far one is released.
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Presenter.ViewHalfCells2D = new Vector2Int(8, 6);
            c.Presenter.ReachCells3D = 2;
            c.Presenter.UpdateStreaming(true);
            Settle(c, 0.5f);
            var farChunk = index.ChunkOfCell(new Vector2Int(34, -1));
            var drawnBefore = c.Presenter.IsChunkDrawn(farChunk);
            c.Presenter.FailChunkBuildForTest = coord => coord == farChunk;
            var crowCell = c.Crow.Cell;
            var revision = C6.Latest(c)?.Revision ?? -1;
            c.Hud.ClearMessage();
            var started = t.TryBegin(far.id, C6.Clock, FastTravelSource.Check);
            var failedMid = false;
            Fly(c, () => { if (t.Phase == FastTravelPhase.ArrivalAnimation && c.Session.Cell == HomeArrival) failedMid = true; });
            Check("a destination chunk that fails to build (not drawn before): the flight starts, then gives up behind the sky",
                !drawnBefore && started == FastTravelStart.Started && failedMid && t.Failures == 1, $"drawn before {drawnBefore}, failures {t.Failures}");
            Check("she is where she was (place, facing), the crow where it was, input and menu back, 「いまは とべないみたい。」",
                c.Session.Cell == HomeArrival && c.Session.Facing == FacingDirection.Down && c.Crow.Cell == crowCell && !c.Crow.TravelHeld && !c.Crow.HiddenForTravel &&
                !c.Session.InputSuspended && c.Menu.enabled && C6.Shown(c) == PocFastTravel.MessageFailed, Where(c));
            Settle(c, 1.5f);
            Check("the save still holds the house (no half-way place)", (C6.Latest(c)?.MapId ?? "first_field") == "first_field" &&
                (C6.Latest(c) == null || C6.Latest(c).CellX == HomeArrival.x && C6.Latest(c).CellY == HomeArrival.y), HarukaSaveSystem.Describe(C6.Latest(c)));
            c.Presenter.FailChunkBuildForTest = null;
            // A cold far destination (its chunks not drawn): built behind the sky, then the landing.
            var coldStart = t.TryBegin(far.id, C6.Clock, FastTravelSource.Check);
            Fly(c);
            Check("a far destination whose chunks were released: they are built behind the sky, she lands (no fixed distance)",
                coldStart == FastTravelStart.Started && c.Session.Cell == new Vector2Int(34, -1) && t.LastArrivalVerified,
                $"{Where(c)} built {t.LastPrepareBuilt} chunks in {t.LastPrepareMilliseconds:0.0} ms");
            t.Registry.points.Remove(far);
            c.Presenter.ReachCells3D = 40;
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            // The page closed in the middle of a flight: the departure place is what the save holds.
            t.SetFound(new[] { Plaza });
            c.Session.PlaceForCheck(HomeArrival + Vector2Int.right * 3, FacingDirection.Left);
            Settle(c, 1.5f);
            c.Save.Write("check", C6.Clock);
            t.TryBegin(Plaza, C6.Clock, FastTravelSource.Check);
            for (var i = 0; i < 300 && t.Phase != FastTravelPhase.Transferring; i++) Frame(c, 1f / 60f, Vector2Int.zero);
            var moved = c.Session.Cell == PlazaArrival;
            c.Save.OnPageHidden("");
            var r = Begin(storage);
            Check("the page closed while flying (she is already moved behind the sky): opened again, she is at the departure place",
                moved && r.Session.Cell == HomeArrival + Vector2Int.right * 3 && r.Session.CurrentRegionMapId == "first_field", Where(r));
        }

        // ---------------------------------------------------------------- 16.7 the crow

        private static void CrowChecks()
        {
            C6.SetLegacy(null, null);
            var c = Begin(new HarukaMemoryStorage());
            var t = T(c);
            var crow = c.Crow;
            t.SetFound(new[] { Plaza });
            TalkToCrow(c);
            Press(c, PocCrow.ChoiceFollow);
            Settle(c, 1f);
            Check("following before the flight", crow.Following);
            TalkToCrow(c);
            Press(c, PocCrow.ChoiceTravel);
            Press(c, NameOf(c, Plaza));
            var heldCell = crow.Cell;
            var steady = true;
            for (var i = 0; i < 45; i++) { Frame(c, 1f / 60f, Vector2Int.zero); if (crow.Cell != heldCell || !crow.TravelHeld || crow.HiddenForTravel) steady = false; }
            var overHer = (crow.transform.position - (c.Actor.transform.position + Vector3.up * 1.45f)).magnitude;
            Check("during the departure the crow is held (its logical cell kept, no decision of its own) and drawn flying to her (the one crow, presentation only)",
                steady && crow.Activity == CrowActivity.Flying && crow.Airborne, crow.Activity.ToString());
            Check("0.75 s in: it is over her head and she is lifted (both views draw the same positions)", overHer < 0.3f && c.Actor.PresentationLift > 0f,
                $"{overHer:0.00} units from her head, lift {c.Actor.PresentationLift:0.00}");
            Fly(c);
            Check("FollowPlayer kept: landed beside her (1-2 cells, behind or beside, standing), drawn, one crow",
                crow.Following && crow.DistanceToPlayer >= 1 && crow.DistanceToPlayer <= 2 && crow.Activity == CrowActivity.GroundIdle &&
                !crow.TravelHeld && !crow.HiddenForTravel && crow.TravelLandings == 1 && Crows() == 1 && crow.Cell != c.Session.Cell, Where(c));
            Check("never on her cell, a door or a blocked cell", c.Session.OutdoorWorld.CanEnter(crow.Cell) && !c.Session.IsTransitionCell(c.Session.OutdoorWorld, crow.Cell));
            Walk(c, PlazaArrival + new Vector2Int(-6, 1));
            Settle(c, 3f);
            Check("and walks with her in the East area as before", crow.Following && crow.DistanceToPlayer <= 2, Where(c));
            // To the house: following, then in and out of the house.
            t.TryBegin(Home, C6.Clock, FastTravelSource.Check);
            Fly(c);
            Check("following to the house: beside her in front of it", crow.Following && crow.DistanceToPlayer <= 2 && c.Session.Cell == HomeArrival, Where(c));
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("she goes in: the crow waits outside (ver3.4)", c.Session.World.MapId == "halka_house" && crow.WaitingOutside, Where(c));
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Settle(c, 0.3f);
            Check("she comes out: it follows again", c.Session.World == c.Session.OutdoorWorld && crow.Following && !crow.WaitingOutside && crow.DistanceToPlayer <= 3, Where(c));
            // 2D / 3D: the same crow, the same place.
            var crowCell = crow.Cell;
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            Settle(c, 0.1f);
            Check("switching to 3D after a flight: the same crow, mode and cell", crow.Following && Crows() == 1 && Mathf.Abs(crow.Cell.x - crowCell.x) + Mathf.Abs(crow.Cell.y - crowCell.y) <= 1);
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            // Free: あそんでおいで, then a flight from the menu to the plaza.
            Settle(c, 1f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            for (var i = 0; i < 1800 && (crow.Airborne || crow.IsMoving); i++) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("あそんでおいで: free", !crow.Following);
            c.Session.PlaceForCheck(HomeArrival + Vector2Int.right * 2, FacingDirection.Right);
            Settle(c, 0.2f);
            OpenFromMenu(c);
            Press(c, NameOf(c, Plaza));
            Fly(c);
            Check("FreeRoam kept after a flight (the flight never turns ついてきて on)", !crow.Following && crow.Mode == CrowCompanionMode.FreeRoam, Where(c));
            Check("the free crow landed beside her at the plaza and goes home a moment later", crow.DistanceToPlayer >= 1 && crow.DistanceToPlayer <= 3 && crow.TravelReturnPending, Where(c));
            var left = false;
            for (var i = 0; i < 30 * 60 && !left; i++)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field" || crow.Airborne && crow.TargetPerch != null) left = true;
            }
            Check("it flies back toward first_field (its home trees) instead of staying in the East area", left, Where(c));
            for (var i = 0; i < 40 * 60 && (crow.Airborne || crow.IsMoving) && crow.Activity != CrowActivity.Perched; i++) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("and lives its own life there (perched or on the ground in first_field)", (crow.Activity == CrowActivity.Perched || crow.Activity == CrowActivity.GroundIdle ||
                crow.Activity == CrowActivity.GroundWalk) && c.Session.OutdoorIndex.RegionAt(PositionCell(crow))?.MapId == "first_field", Where(c));
            // Free, flying home from the plaza with the menu.
            OpenFromMenu(c);
            Press(c, NameOf(c, Home));
            Fly(c);
            Check("free, from the menu back to the house: she lands, the crow lands beside her, still free, one crow",
                c.Session.Cell == HomeArrival && !crow.Following && crow.DistanceToPlayer >= 1 && crow.DistanceToPlayer <= 3 && Crows() == 1, Where(c));
            for (var i = 0; i < 4 * 60; i++) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("at home the free crow goes back to its trees too (a takeoff toward one)", crow.Airborne || crow.TalkTakeoffPending ||
                crow.Activity == CrowActivity.Perched, crow.Activity.ToString());
            Check("one crow throughout", maxCrows == 1, maxCrows.ToString());
        }

        private static Vector2Int PositionCell(PocCrow crow) => GridSpace3D.ToCell(crow.transform.position);

        // ---------------------------------------------------------------- 16.8 the save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            Check("Save Schema stays 1 (the found places are a checked extension)", HarukaSaveData.SchemaVersion == 1);
            var plain = C6.Sample();
            var found = C6.Sample();
            found.FastTravel = new List<string> { Plaza, Home };
            var both = C6.Sample();
            both.FastTravel = new List<string> { Plaza, Home };
            both.CrowMode = HarukaSaveData.CrowFollow;
            var plainText = C6.Sealed(plain);
            var foundText = C6.Sealed(found);
            var bothText = C6.Sealed(both);
            Check("only the start places: no extension (exactly the ver3.4 file)", plainText.Contains("\"extensions\": {}") && !plainText.Contains("fastTravel"));
            Check("the plaza found: extensions.fastTravel { discovered [ids], checksum }",
                foundText.Contains("\"fastTravel\": { \"discovered\": [\"" + Plaza + "\", \"" + Home + "\"], \"checksum\": \"crc32:"), foundText);
            Check("the main checksum is the same with or without it (older builds accept the file)", Sealed(plain).Checksum == Sealed(found).Checksum);
            Check("crowCompanion and fastTravel together: both written, both read back",
                bothText.Contains("\"crowCompanion\"") && bothText.Contains("\"fastTravel\"") &&
                HarukaSaveData.TryRead(bothText, out var b, out _) == HarukaSaveReadStatus.Valid && b.CrowMode == HarukaSaveData.CrowFollow &&
                b.FastTravel.SequenceEqual(new[] { Plaza, Home }));
            Check("read back alone", HarukaSaveData.TryRead(foundText, out var f, out _) == HarukaSaveReadStatus.Valid && f.FastTravel.SequenceEqual(new[] { Plaza, Home }) &&
                f.CrowMode == HarukaSaveData.CrowFree);
            var stripped = System.Text.RegularExpressions.Regex.Replace(bothText, @", ""fastTravel"": \{[^}]*\}", "");
            Check("what a ver3.4 build sees (the fastTravel entry ignored): valid, the crow's mode kept",
                HarukaSaveData.TryRead(stripped, out var old, out var p0) == HarukaSaveReadStatus.Valid && old.CrowMode == HarukaSaveData.CrowFollow && !old.HasFastTravel, p0);
            var dupe = C6.Sample();
            dupe.FastTravel = new List<string> { Plaza, Plaza, Home };
            Check("a duplicate id (its checksum right): read once", HarukaSaveData.TryRead(C6.Sealed(dupe), out var d1, out _) == HarukaSaveReadStatus.Valid &&
                d1.FastTravel.SequenceEqual(new[] { Plaza, Home }));
            var future = C6.Sample();
            future.FastTravel = new List<string> { Home, "landmark_north_lake" };
            var futureText = C6.Sealed(future);
            Check("an id this version does not know (a later place): valid, kept", HarukaSaveData.TryRead(futureText, out var fu, out _) == HarukaSaveReadStatus.Valid &&
                fu.FastTravel.Contains("landmark_north_lake"));
            var bad = new (string label, string text)[]
            {
                ("a malformed id", foundText.Replace("\"" + Plaza + "\"", "\"Plaza Square!\"")),
                ("an id changed by hand (checksum differs)", foundText.Replace("\"" + Plaza + "\"", "\"landmark_east_other\"")),
                ("discovered not a list", System.Text.RegularExpressions.Regex.Replace(foundText, @"""discovered"": \[[^\]]*\]", "\"discovered\": \"" + Plaza + "\"")),
                ("a number in the list", foundText.Replace("\"" + Plaza + "\", ", "7, ")),
                ("no checksum", System.Text.RegularExpressions.Regex.Replace(foundText, @"\], ""checksum"": ""crc32:[0-9a-f]+"" \}", "] }")),
                ("the entry not an object", System.Text.RegularExpressions.Regex.Replace(foundText, @"""fastTravel"": \{[^}]*\}", "\"fastTravel\": true")),
                ("more than 64 ids", TooMany()),
            };
            foreach (var (label, text) in bad)
                Check("a save with " + label + " is Corrupt (never loaded)", HarukaSaveData.TryRead(text, out _, out var problem) == HarukaSaveReadStatus.Corrupt, problem);
            // Old saves of every version: the house only; ver3.4's companion kept.
            var places = new (string version, string map, Vector2Int cell, bool follow)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0), false), ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1), false),
                ("ver3.2", "halka_house", new Vector2Int(-2, 0), false), ("ver3.3", V33.EastId, new Vector2Int(9, 6), false),
                ("ver3.4", V33.EastId, new Vector2Int(4, 6), true),
            };
            foreach (var (version, map, cell, follow) in places)
            {
                var storage = new HarukaMemoryStorage();
                var data = V32.Old(version == "ver3.4" ? "ver3.3" : version, map, cell);
                data.GameVersion = version;
                if (follow) data.CrowMode = HarukaSaveData.CrowFollow;
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(data));
                var g = Begin(storage);
                Settle(g, 0.5f);
                Check($"a {version} save ({map}): resumed there, only the house found" + (follow ? ", the crow still following" : ""),
                    T(g).FoundIds.SequenceEqual(new[] { Home }) && g.Session.TryGetSavePlace(out var m, out var l) && m == map && l == cell &&
                    g.Crow.Following == follow, Where(g));
            }
            // Found, following, saved, closed, opened, and flown again.
            {
                var storage = new HarukaMemoryStorage();
                var g = Begin(storage);
                Walk(g, PlazaArrival);
                TalkToCrow(g);
                Press(g, PocCrow.ChoiceFollow);
                Settle(g, 1.5f);
                T(g).TryBegin(Home, C6.Clock, FastTravelSource.Check);
                Fly(g);
                Settle(g, 0.5f);
                g.Save.OnPageHidden("");
                var latest = C6.Latest(g);
                Check("found + following + flown home: the save holds the house, both places, the companion", latest.MapId == "first_field" &&
                    latest.CellX == HomeArrival.x && latest.CellY == HomeArrival.y && latest.HasFastTravel && latest.CrowMode == HarukaSaveData.CrowFollow,
                    HarukaSaveSystem.Describe(latest));
                var r = Begin(storage);
                Check("opened again: at the house, the plaza found, the crow following beside her", r.Session.Cell == HomeArrival && T(r).IsFound(Plaza) &&
                    r.Crow.Following && r.Crow.DistanceToPlayer <= 6, Where(r));
                Check("and she can fly to the plaza again", T(r).TryBegin(Plaza, C6.Clock, FastTravelSource.Check) == FastTravelStart.Started && Fly(r) &&
                    r.Session.Cell == PlazaArrival && r.Crow.Following, Where(r));
                // Export / import.
                var exported = r.Save.ExportText();
                Check("export: the file has both entries", exported != null && exported.Contains("\"fastTravel\"") && exported.Contains("\"crowCompanion\""));
                var t2 = Begin(new HarukaMemoryStorage());
                Settle(t2, 1.2f);
                var check = t2.Save.CheckImport(exported, out var data);
                var ok = t2.Save.ApplyImport(data, C6.Clock, out var problem);
                Settle(t2, 0.5f);
                Check("import into a new game: the plaza found, the crow following, the next save keeps both",
                    check == HarukaSaveSystem.ImportCheck.Ok && ok && T(t2).IsFound(Plaza) && t2.Crow.Following && C6.Latest(t2).HasFastTravel &&
                    C6.Latest(t2).CrowMode == HarukaSaveData.CrowFollow, problem);
                var plainImport = t2.Save.CheckImport(C6.Sealed(V32.Old("ver3.3", "first_field", new Vector2Int(1, 1))), out var plainData);
                t2.Save.ApplyImport(plainData, C6.Clock, out _);
                Settle(t2, 0.5f);
                Check("import of an older file (nothing found in it): only the house again", plainImport == HarukaSaveSystem.ImportCheck.Ok &&
                    T(t2).FoundIds.SequenceEqual(new[] { Home }) && !C6.Latest(t2).HasFastTravel);
                Check("import of a file with a damaged fastTravel entry: refused, nothing changed",
                    t2.Save.CheckImport(exported.Replace("\"" + Plaza + "\"", "\"landmark_east_other\""), out _) == HarukaSaveSystem.ImportCheck.Unreadable &&
                    T(t2).FoundIds.SequenceEqual(new[] { Home }));
                // An unknown (later) id survives a round trip through this version.
                var u = new HarukaMemoryStorage();
                u.Write(HarukaSnapshotStore.SlotNames[0], futureText);
                var ug = Begin(u);
                Settle(ug, 0.3f);
                ug.Save.Write("check", C6.Clock);
                Check("a later version's place id: never offered, written back unchanged", !T(ug).FoundPoints().Any(p => p.id == "landmark_north_lake") &&
                    C6.Latest(ug).FastTravel.Contains("landmark_north_lake"), HarukaSaveSystem.Describe(C6.Latest(ug)));
            }
            // A damaged newest snapshot: the older one (with the plaza) is loaded.
            {
                var storage = new HarukaMemoryStorage();
                var g = Begin(storage);
                T(g).SetFound(new[] { Plaza });
                Settle(g, 1.5f);
                g.Save.Write("check", C6.Clock);
                var latest = C6.Latest(g);
                var newest = HarukaSnapshotStore.SlotNames.First(n => HarukaSaveData.TryRead(storage.Read(n), out var d, out _) == HarukaSaveReadStatus.Valid && d.Revision == latest.Revision);
                storage.Write(newest, storage.Read(newest).Replace("\"" + Plaza + "\"", "\"landmark_east_other\""));
                var r = Begin(storage);
                Check("the newest snapshot's fastTravel entry damaged: it is skipped (backup snapshot), the plaza still found",
                    r.Save.LoadSource == "backup snapshot" && T(r).IsFound(Plaza), r.Save.LoadSource);
            }
            Check("rollback: a ver3.5 file is Schema 1 with the ver3.4 values and checksum (an older build loads it; it forgets the found places when it saves)",
                foundText.Contains("\"schemaVersion\": 1") && Sealed(plain).Checksum == Sealed(found).Checksum);
        }

        private static HarukaSaveData Sealed(HarukaSaveData data)
        {
            var copy = data.Copy();
            copy.Seal();
            return copy;
        }

        private static string TooMany()
        {
            var data = C6.Sample();
            data.FastTravel = Enumerable.Range(0, 65).Select(i => "place_" + i).ToList();
            return C6.Sealed(data);
        }

        // ---------------------------------------------------------------- what must not change

        private static void ProtectChecks()
        {
            const string Published = "3d86685";
            Check("WORLD EDITOR v1.0 unchanged since ver3.4 (tools/HalkaWorldMapEditor)", V32.Git("diff --quiet " + Published + " -- tools/HalkaWorldMapEditor", out _));
            Check("the official Maps, their Authoring JSON, the world registry and direction_themes.json unchanged",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/Maps", out _));
            Check("the published ver3.4 (halkaworld/) unchanged", V32.Git("diff --quiet " + Published + " -- halkaworld", out _));
            Check("the crow's sprites, model and voice, and the font atlas unchanged (no new crow made)",
                V32.Git("diff --quiet " + Published + " -- unity-project/Assets/Content/World unity-project/Assets/Content/Audio unity-project/Assets/POC2D3D/Models unity-project/Assets/Resources", out _));
            Check("the old tests are all still there (none deleted)", V32.Git("diff --quiet --diff-filter=D " + Published + " -- unity-project/Assets/POC2D3D/Editor unity-project/Assets/Editor", out _));
        }
    }
}
