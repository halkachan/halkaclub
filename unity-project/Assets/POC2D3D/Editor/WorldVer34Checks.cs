using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;
using V32 = Halka.Poc2D3D.EditorTools.WorldVer32Checks;
using V33 = Halka.Poc2D3D.EditorTools.WorldVer33Checks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.4「カラスの相棒化」: the checks of the crow's companion mode (the talk's
    // choices, following on the ground and in the air, the regions and chunks, waiting outside the
    // house, AUTO, both views and the save). The game is started with the Phase 6 harness (memory
    // storage only); every frame also runs what Update does in play (the crow, its watch, the
    // choices' watch, the chunk streamer). Every check prints PASS or FAIL.
    //   Checks: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer34Checks.RunBatch
    public static class WorldVer34Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";

        // What every frame of a check watches: the crow never on her cells or off the ground's
        // cells, one crow, the farthest it fell behind.
        private static int overlapFrames;
        private static int badGroundFrames;
        private static int maxDistance;

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
            UnityEngine.Random.InitState(3434);
            try
            {
                Run("mode", ModeChecks);
                Run("follow", FollowChecks);
                Run("collision", CollisionChecks);
                Run("region", RegionChecks);
                Run("rooms", RoomChecks);
                Run("auto", AutoChecks);
                Run("views", ViewChecks);
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
            foreach (var failure in failures) Debug.LogError("VER34 FAIL " + failure);
            Debug.Log("HARUKA ver3.4 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.4 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V34_LOGS", "ver34_checks.txt");
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
            if (condition) { passes++; Debug.Log("VER34 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- harness

        // One frame as the game plays it: the session (input), AUTO, the views, the save, the crow
        // (its Update: the air watch and the core), the choices' watch, the chunk streamer.
        private static void Frame(C6.Ctx c, float dt, Vector2Int raw, bool auto = false)
        {
            C6.Frame(c, dt, raw, auto);
            if (c.Crow != null && c.Crow.isActiveAndEnabled && c.Crow.gameObject.activeInHierarchy)
            {
                c.Crow.WatchAir(C6.Clock);
                c.Crow.Tick(C6.Clock);
            }
            if (c.Gameplay.CrowChoices != null) c.Gameplay.CrowChoices.Watch(C6.Clock);
            if (c.CrowView != null && c.CrowView.isActiveAndEnabled) c.CrowView.Apply(dt, C6.Clock);
            c.Presenter.UpdateStreaming(false);
            Observe(c);
        }

        private static void Observe(C6.Ctx c)
        {
            var crow = c.Crow;
            if (crow == null || !crow.isActiveAndEnabled || c.Session.World != c.Session.OutdoorWorld) return;
            if (crow.GroundOccupancyActive && (c.Session.PlayerReserves(crow.Cell) || crow.IsMoving && c.Session.PlayerReserves(crow.StepToCell)))
                overlapFrames++;
            if (crow.GroundOccupancyActive && !c.Session.OutdoorWorld.CanEnter(crow.Cell)) badGroundFrames++;
            if (crow.Following) maxDistance = Mathf.Max(maxDistance, crow.DistanceToPlayer);
        }

        private static void ResetWatch()
        {
            overlapFrames = 0;
            badGroundFrames = 0;
            maxDistance = 0;
        }

        private static void Settle(C6.Ctx c, float seconds, bool auto = false)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero, auto);
        }

        // Walks there holding the keys (no pause between steps, as a person walking). The route
        // ignores the crow: when it stands in her way she keeps the key down (and it must give way).
        private static bool Walk(C6.Ctx c, Vector2Int goal, Action onFrame = null, float maxSeconds = 60f)
        {
            var frames = 0;
            var limit = Mathf.CeilToInt(maxSeconds * 60f);
            while (c.Session.Cell != goal && frames < limit)
            {
                var path = RouteThroughCrow(c, c.Session.Cell, goal);
                if (path == null || path.Count == 0)
                {
                    Frame(c, 1f / 60f, Vector2Int.zero);
                    frames++;
                    onFrame?.Invoke();
                    continue;
                }
                var next = path[0];
                var from = c.Session.Cell;
                var held = 0;
                // The key is held until the step starts (or for 3 s while the crow is in the way),
                // then let go until the step ends: one step per press, no overshoot at a turn.
                while (c.Session.Cell == from && !c.Session.IsMoving && frames < limit && held++ < 180)
                {
                    Frame(c, 1f / 60f, V32.Raw(c, next - from));
                    frames++;
                    onFrame?.Invoke();
                }
                while (c.Session.IsMoving && frames < limit)
                {
                    Frame(c, 1f / 60f, Vector2Int.zero);
                    frames++;
                    onFrame?.Invoke();
                }
            }
            C6.Release(c);
            return c.Session.Cell == goal;
        }

        // Her route over the ground (doors excluded), as if the crow were not there.
        private static List<Vector2Int> RouteThroughCrow(C6.Ctx c, Vector2Int from, Vector2Int goal)
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

        private static void Set(object target, string name, object value) =>
            typeof(CrowBehaviourCore).GetField(name, Hidden).SetValue(target, value);

        private static void SetCrow(object target, string name, object value) =>
            typeof(PocCrow).GetField(name, Hidden).SetValue(target, value);

        // Waits (playing) until the crow stands on the ground, and keeps it from pottering off.
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

        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Hidden).Invoke(target, args);

        // The free crow stands still for a talk: no flight, no wander, no cooldown.
        private static void HoldCrow(C6.Ctx c)
        {
            Set(c.Crow, "nextFlightAt", C6.Clock + 1000f);
            Set(c.Crow, "nextActionAt", C6.Clock + 1000f);
            Set(c.Crow, "interactionCooldownUntil", 0f);
        }

        // She stands next to the crow, facing it, and presses A. Returns the talk's result.
        private static bool TalkToCrow(C6.Ctx c)
        {
            if (c.Crow.Following) WaitStanding(c);
            Set(c.Crow, "interactionCooldownUntil", 0f);
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

        // The page button with this label (the menu's own press).
        private static bool Press(C6.Ctx c, string label)
        {
            var page = c.Menu.Page;
            if (page == null) return false;
            for (var i = 0; i < page.Buttons.Count; i++)
                if (page.Buttons[i] == label) { c.Menu.PressPageButton(i, C6.Clock); return true; }
            return false;
        }

        private static string Buttons(C6.Ctx c) => c.Menu.Page != null ? string.Join("/", c.Menu.Page.Buttons) : "(no page)";

        // ついてきて, from wherever the crow stands now.
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
            $"her {c.Session.Cell} crow {c.Crow.Cell} {c.Crow.CompanionState} d {c.Crow.DistanceToPlayer} mode {c.Crow.Mode}";

        private static int Crows() => UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        // ---------------------------------------------------------------- 16.1 mode and talk

        private static void ModeChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            Check("a new game: the crow is free (FreeRoam), at its spawn in first_field, one crow",
                crow.Mode == CrowCompanionMode.FreeRoam && !crow.Following && Crows() == 1 &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", Where(c));
            Check("the choices are drawn by the game's font (カラス 「カァ。」 ついてきて あそんでおいで やめる)",
                new[] { PocCrow.ChoiceTitle, PocCrow.ChoiceLine, PocCrow.ChoiceFollow, PocCrow.ChoicePlay, PocCrow.ChoiceCancel }.All(GameBitmapFont.HasAllGlyphs));
            HoldCrow(c);
            var voice = crow.GetComponent<InteractionAudio>();
            var plays = voice.PlayRequestCount;
            var talks = crow.TalkCount;
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var talked = TalkToCrow(c);
            Check("talk (A, in front, facing it): 「カァ。」, the crow voice once, the choices ついてきて / やめる (free crow)",
                talked && c.Session.LastMessage == PocCrow.TalkMessage && voice.PlayRequestCount == plays + 1 && crow.TalkCount == talks + 1 &&
                crow.ChoiceOpen && c.Menu.Page is CrowChoicePage && Buttons(c) == PocCrow.ChoiceFollow + "/" + PocCrow.ChoiceCancel &&
                c.Menu.Page.Title == PocCrow.ChoiceTitle && c.Menu.Page.Lines.Count == 1 && c.Menu.Page.Lines[0] == PocCrow.ChoiceLine, Buttons(c));
            Check("while the choices are open: the 「カァ。」 is in the window (no second message box), game input blocked, AUTO paused",
                C6.Shown(c) == null && c.Menu.BlocksGameplayInput && c.Auto.MenuSuspended);
            var cell = crow.Cell;
            Settle(c, 6f);
            Check("while the choices are open the crow waits on its cell (no walk, no flight) for 6 s",
                crow.Cell == cell && crow.Activity == CrowActivity.GroundIdle && crow.ChoiceOpen, Where(c));
            Check("a second A while the choices are open does nothing (no second voice)", !c.Session.InteractAhead() && voice.PlayRequestCount == plays + 1);
            Press(c, PocCrow.ChoiceCancel);
            Settle(c, 0.05f);
            Check("やめる: the menu closes, the crow stays free and reacts as before (a takeoff toward a tree, due or begun)",
                c.Menu.Page == null && !c.Menu.IsOpen && !crow.ChoiceOpen && !crow.Following &&
                (crow.TalkTakeoffPending || crow.Activity == CrowActivity.Takeoff), Where(c));
            Settle(c, 0.6f);
            Check("やめる (free): it takes off toward a tree (the ver3.3 reaction)", crow.Activity != CrowActivity.GroundIdle && crow.Activity != CrowActivity.GroundWalk, crow.Activity.ToString());
            Check("in the air it cannot be talked to (the old rule)", !TalkToCrowNoPlace(c));
            for (var t = 0f; t < 30f && (crow.Airborne || crow.IsMoving); t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("back on the ground (free crow)", crow.Activity == CrowActivity.GroundIdle, crow.Activity.ToString());
            // ついてきて
            HoldCrow(c);
            Settle(c, 0.1f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoiceFollow);
            Settle(c, 0.1f);
            Check("ついてきて: companion mode (FollowPlayer), the menu closed, the crow on the ground beside her",
                crow.Following && c.Menu.Page == null && crow.ModeChanges >= 1 && crow.DistanceToPlayer <= 2 && crow.Activity == CrowActivity.GroundIdle, Where(c));
            Check("the companion mode and the activity are separate (FollowPlayer while GroundIdle)", crow.Following && crow.Activity == CrowActivity.GroundIdle);
            // Talk while following.
            Settle(c, 1f);
            Set(crow, "interactionCooldownUntil", 0f);
            TalkToCrow(c);
            Check("talk while following: 「カァ。」 and the choices あそんでおいで / やめる (only the choice that changes something)",
                crow.ChoiceOpen && Buttons(c) == PocCrow.ChoicePlay + "/" + PocCrow.ChoiceCancel, Buttons(c));
            Press(c, PocCrow.ChoiceCancel);
            Settle(c, 1f);
            Check("やめる while following: still following, no flight to a tree", crow.Following && !crow.ChoiceOpen && crow.Activity == CrowActivity.GroundIdle || crow.Following && crow.Activity == CrowActivity.GroundWalk, Where(c));
            // A touch outside the window closes it: the same as やめる.
            Settle(c, 1f);
            Set(crow, "interactionCooldownUntil", 0f);
            TalkToCrow(c);
            var opened = crow.ChoiceOpen;
            c.Menu.SetOpen(false, C6.Clock);
            Settle(c, 0.1f);
            Check("the window closed by a touch outside it: nothing chosen (still following), the crow no longer waits",
                opened && !crow.ChoiceOpen && crow.Following, Where(c));
            // あそんでおいで
            Settle(c, 1f);
            Set(crow, "interactionCooldownUntil", 0f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            Settle(c, 0.05f);
            Check("あそんでおいで: free again (FreeRoam); in first_field it takes off toward a tree as before",
                !crow.Following && crow.Mode == CrowCompanionMode.FreeRoam && (crow.TalkTakeoffPending || crow.Airborne), Where(c));
            var activities = new HashSet<CrowActivity>();
            for (var t = 0f; t < 90f; t += 1f / 30f) { Frame(c, 1f / 30f, Vector2Int.zero); activities.Add(crow.Activity); }
            Check("free again for 90 s: its own life (takeoff, flight, perch, landing, ground), inside its first_field range",
                activities.Contains(CrowActivity.Perched) && activities.Contains(CrowActivity.GroundIdle) && activities.Contains(CrowActivity.Flying) &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", string.Join(",", activities));
            Check("from further away nothing happens (ExamineInteractable's range)", !crow.TryTalk(crow.Cell + new Vector2Int(-3, -3), FacingDirection.Up, false));
            Check("no menu (an old check scene): the talk reacts as before (message, takeoff), no choices",
                NoMenuTalk());
            c.Auto.SetAutoEnabled(false, C6.Clock);
        }

        // In the air: the same A from where she stands.
        private static bool TalkToCrowNoPlace(C6.Ctx c) => c.Crow.TryTalk(c.Session.Cell, c.Session.Facing, false) && c.Crow.ChoiceOpen;

        private static bool NoMenuTalk()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            var open = crow.OpenChoices;
            crow.OpenChoices = null;
            HoldCrow(c);
            var talked = TalkToCrow(c);
            var ok = talked && !crow.ChoiceOpen && crow.TalkTakeoffPending && c.Session.LastMessage == PocCrow.TalkMessage;
            crow.OpenChoices = open;
            return ok;
        }

        // ---------------------------------------------------------------- 16.2 following

        private static void FollowChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            Check("ついてきて from the crow's spawn", StartFollowing(c), Where(c));
            ResetWatch();
            // One step.
            var start = c.Session.Cell;
            var dir = C6.FreeDirection(c);
            V32.Step(c, dir, 0.1f);
            Settle(c, 2f);
            Check("one step of hers: the crow stays beside her (2 cells or less), standing", c.Session.Cell == start + dir && crow.DistanceToPlayer <= 2, Where(c));
            // A long walk: East along the road and back, holding the keys.
            var steps = crow.CompanionSteps;
            var flights = crow.CatchUpFlights;
            var searches = crow.PathSearches;
            Walk(c, new Vector2Int(9, -1));
            Settle(c, 3f);
            Check("a walk to the East road's end: the crow walked with her (ground steps) and is beside her when she stops",
                crow.CompanionSteps > steps && crow.DistanceToPlayer <= 2 && crow.Following, Where(c) + $" steps +{crow.CompanionSteps - steps}");
            steps = crow.CompanionSteps;
            Walk(c, new Vector2Int(-6, -1));
            var during = crow.DistanceToPlayer;
            Settle(c, 4f);
            Check("turning round and walking back West 15 cells: it follows on foot, beside her within 4 s of her stop",
                crow.DistanceToPlayer <= 2 && crow.CompanionSteps > steps + 6, Where(c) + " right after the walk d " + during + $" steps +{crow.CompanionSteps - steps}");
            Check("while walking it never fell further behind than a flight's distance + 2 (catch-up flights)", maxDistance <= PocCrow.FlyDistance + 3, "max " + maxDistance);
            Check("the route search ran only when needed (her cell changed or the way was taken), bounded", crow.PathSearches > searches &&
                crow.LastSearchCells <= PocCrow.MaxSearchCells + 1 && crow.MaxSearchMilliseconds < 20.0,
                $"searches {crow.PathSearches - searches} max {crow.MaxSearchMilliseconds:0.000} ms cells {crow.LastSearchCells}");
            // Standing still: mostly waiting nearby.
            var still = new Dictionary<CrowActivity, int>();
            var near = 0;
            var frames = 0;
            for (var t = 0f; t < 40f; t += 1f / 30f)
            {
                Frame(c, 1f / 30f, Vector2Int.zero);
                still[crow.Activity] = still.TryGetValue(crow.Activity, out var k) ? k + 1 : 1;
                frames++;
                if (crow.Airborne || crow.DistanceToPlayer <= 3) near++;
            }
            var calm = (still.TryGetValue(CrowActivity.GroundIdle, out var idle) ? idle : 0) + (still.TryGetValue(CrowActivity.Perched, out var rest) ? rest : 0);
            var calmShare = calm / (float)frames;
            Check("she stands 40 s: the crow stays near her (3 cells or less, or in the air nearby), mostly calm (standing or resting on a tree)",
                near == frames && calmShare > 0.6f, string.Join(",", still.Select(p => p.Key + " " + p.Value)) + $" calm {calmShare:0.00}");
            // Moving again ends a rest.
            Walk(c, new Vector2Int(2, -1));
            Settle(c, 4f);
            Check("she walks on: following again, beside her", crow.DistanceToPlayer <= 2 && crow.Following, Where(c));
            // A detour round an obstacle: from the West of the house to its East.
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            var west = V32.NearestWalkable(c, door + new Vector2Int(-3, 2));
            var east = V32.NearestWalkable(c, door + new Vector2Int(3, 2));
            Walk(c, west);
            Settle(c, 3f);
            Walk(c, east);
            Settle(c, 4f);
            Check("round the house (an obstacle between): the crow found its way, beside her", crow.DistanceToPlayer <= 2, Where(c));
            // A big gap: she is put 18 cells away: a flight.
            WaitStanding(c);
            SetCrow(crow, "nextSettleAt", C6.Clock + 1000f);
            flights = crow.CatchUpFlights;
            var far = V32.NearestWalkable(c, crow.Cell + new Vector2Int(18, 0));
            c.Session.PlaceForCheck(far, FacingDirection.Right);
            var sawFlying = false;
            for (var t = 0f; t < 6f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero); sawFlying |= crow.Activity == CrowActivity.Flying; }
            Settle(c, 2f);
            Check("18 cells apart: it flies (takeoff, flight, landing) and lands beside her", crow.CatchUpFlights > flights && sawFlying && crow.DistanceToPlayer <= 3 &&
                (crow.Activity == CrowActivity.GroundIdle || crow.Activity == CrowActivity.GroundWalk), Where(c));
            Check("the landing cell is a ground cell she can stand on, not hers, not a door", c.Session.OutdoorWorld.CanEnter(crow.Cell) &&
                !c.Session.PlayerReserves(crow.Cell) && !c.Session.IsTransitionCell(c.Session.OutdoorWorld, crow.Cell), Where(c));
            // A tree nearby while she rests.
            var trees = (IReadOnlyList<ICrowPerch>)Call(crow, "NearbyPerches");
            var treeCell = c.Session.OutdoorWorld.Objects.Where(o => o.Definition.StableId.StartsWith("tree", StringComparison.Ordinal))
                .Select(o => o.RootCell).OrderBy(r => (r - c.Session.Cell).sqrMagnitude).First();
            var stand = V32.NearestWalkable(c, treeCell + new Vector2Int(0, -2));
            Walk(c, stand);
            Settle(c, 5f);
            var perched = (bool)Call(crow, "TryPerchNearby", C6.Clock);
            var sawPerch = false;
            for (var t = 0f; t < 4f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero); sawPerch |= crow.Activity == CrowActivity.Perched; }
            Check("near a tree while she stands: it can rest on that tree (a tree within 4 cells of her), still following",
                perched && sawPerch && crow.Following && crow.Activity == CrowActivity.Perched, Where(c) + " tree " + treeCell);
            var leaveFrom = c.Session.Cell;
            var toward = C6.FreeDirection(c);
            V32.Step(c, toward, 0.05f);
            var left = false;
            for (var t = 0f; t < 1f; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero); left |= crow.Activity == CrowActivity.LeavingPerch || crow.Activity == CrowActivity.Flying; }
            Settle(c, 4f);
            Check("she moves: it leaves the tree at once and comes down beside her", left && crow.DistanceToPlayer <= 3 && !crow.Airborne, Where(c));
            // No way on foot and no landing place for a while: put down out of sight.
            var relocations = crow.Relocations;
            Call(crow, "Relocate", C6.Clock);
            Check("the out-of-sight catch-up: a free ground cell near her (4 cells or more, not shown by the camera), standing, no step counted",
                crow.Relocations == relocations + 1 && crow.DistanceToPlayer >= 4 && c.Session.OutdoorWorld.CanEnter(crow.Cell) &&
                !c.Switcher.IsInView(crow.transform.position) && crow.Activity == CrowActivity.GroundIdle, Where(c));
            Settle(c, 6f);
            Check("after it: back beside her in the usual way (walk or flight)", crow.DistanceToPlayer <= 3, Where(c));
            Check("no frame with the crow on her cell, or standing off the ground (whole follow run)", overlapFrames == 0 && badGroundFrames == 0,
                $"overlap {overlapFrames} bad ground {badGroundFrames}");
        }

        // ---------------------------------------------------------------- 16.3 collision

        private static void CollisionChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            StartFollowing(c);
            Settle(c, 2f);
            ResetWatch();
            // She walks straight into the crow and keeps the key down.
            Settle(c, 1f);
            // ver3.7: on the ground first (with a bigger world the shared random sequence differs, and the
            // crow's own "settle" roll sometimes took a short flight just then: there was nothing to walk into).
            WaitStanding(c);
            var cell = crow.Cell;
            Vector2Int push = default;
            foreach (var d in Dirs)
                if (V32.Walkable(c, cell - d) && !c.Session.PlayerReserves(cell - d)) { push = d; break; }
            c.Session.PlaceForCheck(cell - push, FacingDirectionExtensions.FromVector(push));
            SetCrow(crow, "nextSettleAt", C6.Clock + 100f);
            var yields = crow.Yields;
            var moved = false;
            for (var t = 0f; t < 2f && !moved; t += 1f / 60f) { Frame(c, 1f / 60f, V32.Raw(c, push)); moved = c.Session.Cell == cell || c.Session.IsMoving; }
            C6.Release(c);
            Settle(c, 0.5f);
            Check("she keeps walking into the crow: it steps aside (or flies up) and she walks on within 2 s",
                moved && crow.Yields > yields, Where(c) + " yields " + (crow.Yields - yields));
            // A short tap toward it only turns her (talking to it stays possible).
            Settle(c, 2f);
            var tapCell = crow.Cell;
            foreach (var d in Dirs)
                if (V32.Walkable(c, tapCell - d)) { push = d; break; }
            c.Session.PlaceForCheck(tapCell - push, FacingDirection.Down == FacingDirectionExtensions.FromVector(push) ? FacingDirection.Up : FacingDirection.Down);
            yields = crow.Yields;
            Frame(c, 1f / 60f, V32.Raw(c, push));
            Frame(c, 1f / 60f, V32.Raw(c, push));
            C6.Release(c);
            Frame(c, 1f / 60f, Vector2Int.zero);
            Check("a short tap toward the crow turns her to face it (no push: it stays, she can talk to it)",
                crow.Yields == yields && c.Session.Facing == FacingDirectionExtensions.FromVector(push) && c.Session.Cell == tapCell - push, Where(c));
            // Every landing in a long walk.
            var landings = new List<Vector2Int>();
            var wasAir = false;
            Walk(c, new Vector2Int(9, -1), () =>
            {
                if (wasAir && crow.Activity == CrowActivity.GroundIdle) landings.Add(crow.Cell);
                wasAir = crow.Airborne;
            });
            Walk(c, new Vector2Int(-8, 2), () =>
            {
                if (wasAir && crow.Activity == CrowActivity.GroundIdle) landings.Add(crow.Cell);
                wasAir = crow.Airborne;
            });
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Check("every landing on a free ground cell (no tree, rock, wall), never a door, inside the world",
                landings.All(l => c.Session.OutdoorWorld.CanEnter(l) && l != door && !c.Session.IsTransitionCell(c.Session.OutdoorWorld, l) &&
                    c.Session.OutdoorIndex.RegionAt(l) != null), string.Join(" ", landings));
            Check("no frame with the crow on her cell or on a blocked cell; she was never shut in (she reached every goal)",
                overlapFrames == 0 && badGroundFrames == 0 && c.Session.Cell == new Vector2Int(-8, 2), $"overlap {overlapFrames} bad {badGroundFrames} {Where(c)}");
            // In the air it holds no ground cell.
            c.Session.PlaceForCheck(V32.NearestWalkable(c, crow.Cell + new Vector2Int(14, 0)), FacingDirection.Right);
            var airFree = true;
            for (var t = 0f; t < 3f; t += 1f / 60f)
            {
                Frame(c, 1f / 60f, Vector2Int.zero);
                if (crow.Activity == CrowActivity.Flying && crow.ReservesGround(crow.Cell)) airFree = false;
            }
            Check("flying it does not hold a ground cell (she can walk under it)", airFree);
            // The world's edge: the crow never leaves the regions.
            Settle(c, 3f);
            Walk(c, new Vector2Int(36, -1));
            Settle(c, 4f);
            Check("at the East end of the world (36,-1): the crow stays inside the world, beside her",
                c.Session.OutdoorIndex.RegionAt(crow.Cell) != null && crow.DistanceToPlayer <= 3, Where(c));
        }

        // ---------------------------------------------------------------- 16.4 regions and chunks

        private static void RegionChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            StartFollowing(c);
            ResetWatch();
            var crowRegions = new List<string>();
            var crowXs = new HashSet<int>();
            var loadsBefore = c.Session.MapLoads;
            Walk(c, V33.FieldRoadEnd);
            Walk(c, V33.PlazaFront, () =>
            {
                var r = c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId ?? "-";
                if (crowRegions.Count == 0 || crowRegions[crowRegions.Count - 1] != r) crowRegions.Add(r);
                crowXs.Add(crow.Cell.x);
            });
            Settle(c, 4f);
            Check("to the yellow flower plaza with the crow: it crossed the region border (10|11) with her (first_field -> east_area_01)",
                crowRegions.Contains("first_field") && crowRegions.Contains(V33.EastId) && c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == V33.EastId,
                string.Join(">", crowRegions));
            Check("it crossed the chunk border (15|16) too, and is beside her at the plaza; no map load", crowXs.Contains(15) && crowXs.Contains(16) &&
                crow.DistanceToPlayer <= 3 && c.Session.MapLoads == loadsBefore, Where(c));
            Check("still one crow (no second crow in the East area), following, not back at its spawn", Crows() == 1 && crow.Following, Where(c));
            // The far East and back to the far West: chunks are released and built again.
            var unloads = c.Presenter.Streamer.Stats.TotalUnloads;
            Walk(c, new Vector2Int(35, -1));
            Settle(c, 4f);
            Check("far East (35,-1): the crow kept its state and is beside her", crow.Following && crow.DistanceToPlayer <= 3 && Crows() == 1,
                Where(c) + " " + V32.ChunkSummary(c));
            Walk(c, new Vector2Int(20, -1), null, 90f);
            Settle(c, 4f);
            // This world's 10 chunks all stay inside the keep area, so walking releases none; every
            // chunk is released here at once (as far chunks are) and the streamer builds them again.
            WaitStanding(c);
            var before = (crow.Cell, crow.Activity, crow.Mode, crow.transform.position);
            c.Presenter.Streamer.ReleaseAll();
            var released = c.Presenter.Streamer.Stats.TotalUnloads;
            var whileReleased = (crow.Cell, crow.Activity, crow.Mode, crow.transform.position);
            Settle(c, 1f);
            c.Pixel.UpdateView(0f, 1280, 720);
            c.Pixel.WorldCamera.Render();
            c.Pixel.ComposeScreen(1280, 720);
            Check("every chunk released, then built again: the crow's state untouched (cell, activity, mode, position), still one crow",
                released > unloads && whileReleased == before && crow.Following && Crows() == 1 && c.Presenter.Streamer.Stats.Loaded > 0,
                $"unloads {unloads} -> {released} {V32.ChunkSummary(c)}");
            Check("after the rebuild: the crow drawn again (2D sprite) beside her, no Object drawn twice", c.Pixel.LastCrowRect.width > 0 &&
                crow.DistanceToPlayer <= 3 && V32.DuplicateViews(c).Count == 0, Where(c));
            // Perches of the East area.
            var east = (IReadOnlyList<ICrowPerch>)Call(crow, "NearbyPerches");
            Walk(c, new Vector2Int(20, -1));
            Settle(c, 2f);
            var eastTrees = c.Session.OutdoorWorld.Objects.Where(o => o.RegionMapId == V33.EastId && o.Definition.StableId.StartsWith("tree", StringComparison.Ordinal)).ToList();
            var nearTree = eastTrees.OrderBy(o => (o.RootCell - c.Session.Cell).sqrMagnitude).FirstOrDefault();
            if (nearTree != null)
            {
                Walk(c, V32.NearestWalkable(c, nearTree.RootCell + new Vector2Int(0, -2)));
                Settle(c, 3f);
            }
            var nearby = (IReadOnlyList<ICrowPerch>)Call(crow, "NearbyPerches");
            Check("in the East area the trees near her are its perches (the East area's trees)", nearTree != null && nearby.Count > 0 &&
                nearby.All(p => c.Session.OutdoorIndex.RegionAt(GridSpace3D.ToCell(p.PerchPosition))?.MapId == V33.EastId),
                $"{nearby.Count} perches near {c.Session.Cell}");
            // Back West over the border, then back and forth over it.
            Walk(c, new Vector2Int(8, -1));
            Settle(c, 3f);
            Check("back West into first_field: the crow comes back with her", c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field" &&
                crow.DistanceToPlayer <= 3, Where(c));
            var crossings = 0;
            string last = c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId;
            for (var i = 0; i < 6; i++)
            {
                Walk(c, i % 2 == 0 ? new Vector2Int(14, -1) : new Vector2Int(7, -1), () =>
                {
                    var r = c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId;
                    if (r != null && r != last) { crossings++; last = r; }
                });
                Settle(c, 1f);
            }
            Settle(c, 3f);
            Check("three round trips over the region border (7 <-> 14): the crow crossed with her each time, one crow, beside her",
                crossings >= 4 && Crows() == 1 && crow.DistanceToPlayer <= 3, $"crossings {crossings} {Where(c)}");
            Check("no overlap, no blocked cell during the whole region walk", overlapFrames == 0 && badGroundFrames == 0, $"overlap {overlapFrames} bad {badGroundFrames}");
            // The free crow keeps its first_field life (no follow into the East).
            Set(crow, "interactionCooldownUntil", 0f);
            Settle(c, 1f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            Walk(c, new Vector2Int(25, -1));
            for (var t = 0f; t < 40f; t += 1f / 30f) Frame(c, 1f / 30f, Vector2Int.zero);
            Check("free (あそんでおいで): she goes East alone, the crow stays in first_field", !crow.Following &&
                c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", Where(c));
            // From the East area, あそんでおいで flies it home.
            Walk(c, new Vector2Int(8, -1));
            StartFollowing(c);
            Walk(c, new Vector2Int(24, -1));
            Settle(c, 3f);
            Set(crow, "interactionCooldownUntil", 0f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            for (var t = 0f; t < 30f; t += 1f / 30f) Frame(c, 1f / 30f, Vector2Int.zero);
            Check("あそんでおいで in the East area: it flies back to a tree of first_field and lives there again",
                !crow.Following && c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == "first_field", Where(c));
        }

        // ---------------------------------------------------------------- 16.5 house and booth

        private static void RoomChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            StartFollowing(c);
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            var exit = door + Vector2Int.down;
            Walk(c, exit);
            Settle(c, 2f);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("with the crow to the door and in: halka_house as before", c.Session.World.MapId == "halka_house", c.Session.World.MapId);
            var waiting = crow.Cell;
            Check("the crow waits outside: not drawn, no ground cell held in the room, companion mode kept (WaitingOutside)",
                !crow.gameObject.activeSelf && crow.WaitingOutside && crow.Following && crow.CompanionState == "WaitingOutside" &&
                !c.CrowView.ModelShown, Where(c));
            Check("its waiting cell: beside the door's outside, free ground, not the door, not her arrival cell, not the cell below it",
                waiting == crow.LastWaitingCell && c.Session.OutdoorWorld.CanEnter(waiting) && waiting != door && waiting != exit &&
                waiting != exit + Vector2Int.down && Mathf.Abs(waiting.x - exit.x) + Mathf.Abs(waiting.y - exit.y) <= 3, $"door {door} exit {exit} waiting {waiting}");
            Settle(c, 5f);
            Check("while she is in, the crow does nothing (its cell unchanged, still one crow)", crow.Cell == waiting && Crows() == 1);
            // Booth and back.
            V32.WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.14f);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("-> halka_soundbooth: the crow still waits outside, companion mode kept", c.Session.World.MapId == "halka_soundbooth" &&
                crow.WaitingOutside && crow.Following && crow.Cell == waiting);
            V32.Step(c, Vector2Int.down, 0.4f);
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Settle(c, 0.1f);
            Check("back outside: the crow is there (at its waiting cell, or a step from it), drawn, following, not respawned",
                c.Session.World == c.Session.OutdoorWorld && crow.gameObject.activeSelf && !crow.WaitingOutside && crow.Following &&
                Mathf.Abs(crow.Cell.x - waiting.x) + Mathf.Abs(crow.Cell.y - waiting.y) <= 1 && Crows() == 1, Where(c) + " waited at " + waiting);
            Check("she can walk off at once: her arrival cell and the cell below it are free", c.Session.CanPlayerEnter(exit + Vector2Int.down) &&
                c.Session.Cell == exit);
            Walk(c, exit + new Vector2Int(5, -1));
            Settle(c, 3f);
            Check("and it follows her again (re-joined)", crow.DistanceToPlayer <= 2 && crow.Following, Where(c));
            // Into the house while the crow is in the air (a flight to catch up).
            c.Session.PlaceForCheck(exit + new Vector2Int(10, -3), FacingDirection.Left);
            Settle(c, 0.6f);
            var airborne = crow.Airborne;
            c.Session.PlaceForCheck(exit, FacingDirection.Up);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("in while the crow flies: it waits on the ground by the door all the same", c.Session.World.MapId == "halka_house" &&
                crow.WaitingOutside && crow.Activity == CrowActivity.GroundIdle && c.Session.OutdoorWorld.CanEnter(crow.Cell), $"airborne before {airborne} {Where(c)}");
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            // The free crow: the old life cycle.
            Settle(c, 1f);
            Set(crow, "interactionCooldownUntil", 0f);
            TalkToCrow(c);
            Press(c, PocCrow.ChoicePlay);
            Settle(c, 1f);
            Walk(c, exit);
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("free crow, she goes in: switched off as before (no waiting)", !crow.gameObject.activeSelf && !crow.WaitingOutside && !crow.Following);
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            c.Session.OutdoorWorld.Map.TryGetEntitySpawn("crow_main", out var spawn);
            // (The harness's clock runs ahead of Time.time, so a respawned crow may already take its
            // first wander step during the step's settle time: one cell from the spawn.)
            Check("free crow, she comes out: respawned at its spawn (ver3.3; or one wander step from it), not following her",
                crow.gameObject.activeSelf && !crow.Following && Mathf.Abs(crow.Cell.x - spawn.Cell.x) + Mathf.Abs(crow.Cell.y - spawn.Cell.y) <= 1, Where(c));
        }

        // ---------------------------------------------------------------- 16.6 AUTO

        private static void AutoChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            StartFollowing(c);
            c.Auto.SetAutoEnabled(true, C6.Clock);
            c.Auto.RecordUserAction(C6.Clock - AutoModeController.IdleSeconds - 1f);
            ResetWatch();
            var autoSteps = c.Gameplay.AutoWorld.AutoSteps;
            var near = 0;
            var samples = 0;
            for (var t = 0f; t < 120f; t += 1f / 30f)
            {
                Frame(c, 1f / 30f, Vector2Int.zero, true);
                if (((int)(t * 30f)) % 15 == 0) { samples++; if (crow.DistanceToPlayer <= 4 || crow.Airborne) near++; }
            }
            Check("AUTO walks her for 2 minutes: the crow follows (near her most of the time), still following",
                c.Auto.Active && c.Gameplay.AutoWorld.AutoSteps > autoSteps + 10 && near >= samples * 0.8f && crow.Following,
                $"auto steps +{c.Gameplay.AutoWorld.AutoSteps - autoSteps} near {near}/{samples}");
            Check("no overlap during AUTO", overlapFrames == 0 && badGroundFrames == 0, $"overlap {overlapFrames} bad {badGroundFrames}");
            // A key press stops AUTO, not the companion.
            V32.Step(c, C6.FreeDirection(c), 0.2f);
            c.Auto.RecordUserAction(C6.Clock);
            Settle(c, 0.5f);
            Check("her own step stops AUTO's walk; the crow keeps following (AUTO off is not 'free')", !c.Auto.Active && crow.Following);
            // AUTO by the region border never takes her (or the crow) into the East.
            c.Session.PlaceForCheck(V33.FieldRoadEnd, FacingDirection.Right);
            Settle(c, 3f);
            c.Auto.RecordUserAction(C6.Clock - AutoModeController.IdleSeconds - 1f);
            var maxX = int.MinValue;
            for (var t = 0f; t < 120f; t += 1f / 30f) { Frame(c, 1f / 30f, Vector2Int.zero, true); maxX = Mathf.Max(maxX, c.Session.Cell.x); }
            Check("AUTO on first_field's East edge for 2 minutes with the crow: she never enters the East area by AUTO", maxX <= 10, "max x " + maxX);
            // AUTO indoors: the crow waits outside.
            c.Auto.RecordUserAction(C6.Clock);
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Walk(c, door + Vector2Int.down);
            V32.Step(c, Vector2Int.up, 0.4f);
            var waiting = crow.Cell;
            c.Auto.RecordUserAction(C6.Clock - AutoModeController.IdleSeconds - 1f);
            var rooms = new HashSet<string>();
            for (var t = 0f; t < 120f; t += 1f / 30f) { Frame(c, 1f / 30f, Vector2Int.zero, true); rooms.Add(c.Session.World.MapId); }
            Check("AUTO in the house for 2 minutes: she stays in the house (no door), the crow waits outside the whole time",
                rooms.SetEquals(new[] { "halka_house" }) && crow.WaitingOutside && crow.Cell == waiting && !crow.gameObject.activeSelf, string.Join(",", rooms));
            c.Auto.SetAutoEnabled(false, C6.Clock);
        }

        // ---------------------------------------------------------------- 16.7 views

        private static void ViewChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var crow = c.Crow;
            StartFollowing(c);
            bool Same(Vector2Int cell, CrowActivity activity, Vector3 position) =>
                crow.Cell == cell && crow.Activity == activity && crow.Following && Crows() == 1 &&
                Vector3.Distance(crow.transform.position, position) < 1e-4f;
            // Standing.
            Settle(c, 2f);
            var cell = crow.Cell; var activity = crow.Activity; var position = crow.transform.position;
            c.Menu.ActivateQuickItem(C6.Clock);
            Check("2D -> 3D while it stands: same cell, activity, position, mode; the 3D model shown at the crow", Same(cell, activity, position) &&
                c.Switcher.Mode == PocViewMode.ThirdPerson3D, Where(c));
            for (var i = 0; i < 10; i++) c.CrowView.Apply(1f / 60f, C6.Clock + i / 60f);
            Check("3D: the Blender crow is drawn where the crow is", c.CrowView.ModelShown &&
                Vector3.Distance(c.CrowView.transform.position, crow.transform.position) < 0.01f);
            // Walking in 3D.
            Walk(c, V32.NearestWalkable(c, c.Session.Cell + new Vector2Int(-6, 0)));
            var walking = false;
            // ver3.7: up to 5 s (was 2 s): a random catch-up flight may come first; the switch is checked while it walks.
            for (var t = 0f; t < 5f && !walking; t += 1f / 60f) { Frame(c, 1f / 60f, V32.Raw(c, Vector2Int.right)); walking = crow.Activity == CrowActivity.GroundWalk; }
            cell = crow.Cell; activity = crow.Activity; position = crow.transform.position;
            c.Menu.ActivateQuickItem(C6.Clock);
            Check("3D -> 2D while it walks: same cell, activity, position (the step goes on)", Same(cell, activity, position) && walking &&
                c.Switcher.Mode == PocViewMode.PixelView2D, Where(c));
            C6.Release(c);
            Settle(c, 3f);
            // Flying.
            c.Session.PlaceForCheck(V32.NearestWalkable(c, crow.Cell + new Vector2Int(14, 0)), FacingDirection.Right);
            var flying = false;
            for (var t = 0f; t < 3f && !flying; t += 1f / 60f) { Frame(c, 1f / 60f, Vector2Int.zero); flying = crow.Activity == CrowActivity.Flying; }
            cell = crow.Cell; activity = crow.Activity; position = crow.transform.position;
            var leg = crow.LegEnd;
            c.Menu.ActivateQuickItem(C6.Clock);
            Check("2D -> 3D in flight: same flight leg, position, activity (the flight goes on)", flying && Same(cell, activity, position) &&
                crow.LegEnd == leg, Where(c));
            for (var t = 0f; t < 4f; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            Check("3D: it lands beside her", crow.DistanceToPlayer <= 3 && !crow.Airborne, Where(c));
            // Perched.
            var perched = (bool)Call(crow, "TryPerchNearby", C6.Clock);
            if (!perched)
            {
                var tree = c.Session.OutdoorWorld.Objects.Where(o => o.Definition.StableId.StartsWith("tree", StringComparison.Ordinal))
                    .OrderBy(o => (o.RootCell - c.Session.Cell).sqrMagnitude).First();
                Walk(c, V32.NearestWalkable(c, tree.RootCell + new Vector2Int(0, -2)));
                Settle(c, 3f);
                perched = (bool)Call(crow, "TryPerchNearby", C6.Clock);
            }
            for (var t = 0f; t < 4f && crow.Activity != CrowActivity.Perched; t += 1f / 60f) Frame(c, 1f / 60f, Vector2Int.zero);
            cell = crow.Cell; activity = crow.Activity; position = crow.transform.position;
            c.Menu.ActivateQuickItem(C6.Clock);
            Check("switch while perched: still on the same tree, same position", perched && activity == CrowActivity.Perched && Same(cell, activity, position), Where(c));
            c.Menu.ActivateQuickItem(C6.Clock);
            // At the region border.
            Walk(c, V33.FieldRoadEnd);
            Settle(c, 3f);
            cell = crow.Cell; activity = crow.Activity; position = crow.transform.position;
            for (var i = 0; i < 4; i++) c.Menu.ActivateQuickItem(C6.Clock);
            Check("four switches at the region border: the crow unchanged (no new crow, no reset, still following)", Same(cell, activity, position), Where(c));
            Walk(c, V33.EastRoadStart + Vector2Int.right * 2);
            Settle(c, 3f);
            Check("then on over the border into the East area together", c.Session.OutdoorIndex.RegionAt(crow.Cell)?.MapId == V33.EastId || crow.DistanceToPlayer <= 3, Where(c));
            // 2D drawing.
            if (c.Switcher.Mode != PocViewMode.PixelView2D) c.Menu.ActivateQuickItem(C6.Clock);
            WaitStanding(c);
            c.Pixel.UpdateView(0f, 1280, 720);
            c.Pixel.WorldCamera.Render();
            c.Pixel.ComposeScreen(1280, 720);
            Check("2D: the crow's own sprite is drawn (on the ground, in its row)", c.Pixel.LastCrowSprite != null && c.Pixel.LastCrowRect.width > 0 && !c.Pixel.LastCrowOnTop || crow.Airborne);
        }

        // ---------------------------------------------------------------- 16.8 save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            Check("Save Schema stays 1 (the crow's mode is a checked extension, old builds read the file)", HarukaSaveData.SchemaVersion == 1);
            // The format.
            var free = C6.Sample();
            var follow = C6.Sample();
            follow.CrowMode = HarukaSaveData.CrowFollow;
            var freeText = C6.Sealed(free);
            var followText = C6.Sealed(follow);
            Check("a free crow writes no extension (the file is a ver3.3 file); a following crow writes extensions.crowCompanion",
                freeText.Contains("\"extensions\": {}") && followText.Contains("\"crowCompanion\": { \"mode\": \"follow\""), followText);
            var freeSealed = free.Copy(); freeSealed.Seal();
            var followSealed = follow.Copy(); followSealed.Seal();
            Check("the main checksum is the same for both (the mode is not in the main values: ver3.0-3.3 accept the file)",
                freeSealed.Checksum == followSealed.Checksum);
            Check("ver3.4 reads both back (free / follow)", HarukaSaveData.TryRead(freeText, out var a, out _) == HarukaSaveReadStatus.Valid && a.CrowMode == HarukaSaveData.CrowFree &&
                HarukaSaveData.TryRead(followText, out var b, out _) == HarukaSaveReadStatus.Valid && b.CrowMode == HarukaSaveData.CrowFollow);
            // An older reader: the file without the extension check (what ver3.3 does) is valid.
            var stripped = System.Text.RegularExpressions.Regex.Replace(followText, "\"extensions\": \\{.*\\},\n", "\"extensions\": {},\n");
            Check("what a ver3.3 build sees: the same file is valid without the extension (its values, its checksum)",
                HarukaSaveData.TryRead(stripped, out var older, out var p0) == HarukaSaveReadStatus.Valid && older.CrowMode == HarukaSaveData.CrowFree, p0);
            var bad = new (string label, string text)[]
            {
                ("an unknown mode", followText.Replace("\"mode\": \"follow\"", "\"mode\": \"fly\"")),
                ("the mode changed by hand (checksum differs)", followText.Replace("\"mode\": \"follow\"", "\"mode\": \"free\"")),
                ("a missing extension checksum", System.Text.RegularExpressions.Regex.Replace(followText, ", \"checksum\": \"crc32:[0-9a-f]+\" \\}", " }")),
                ("the extension not an object", System.Text.RegularExpressions.Regex.Replace(followText, "\"crowCompanion\": \\{[^}]*\\}", "\"crowCompanion\": \"follow\"")),
                ("the crow entry of another save (another place, the same mode)", CopyCrowEntry(followText)),
            };
            foreach (var (label, text) in bad)
                Check("a save with " + label + " is Corrupt (never loaded)", HarukaSaveData.TryRead(text, out _, out var problem) == HarukaSaveReadStatus.Corrupt, problem);
            // Old saves of every version: free.
            var places = new (string version, string map, Vector2Int cell)[]
            {
                ("ver3.0", "first_field", new Vector2Int(-1, 0)), ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1)),
                ("ver3.2", "halka_house", new Vector2Int(-2, 0)), ("ver3.3", V33.EastId, new Vector2Int(9, 6)),
            };
            foreach (var (version, map, cell) in places)
            {
                var storage = new HarukaMemoryStorage();
                storage.Write(HarukaSnapshotStore.SlotNames[0], C6.Sealed(V32.Old(version, map, cell)));
                var g = C6.Start(storage);
                Settle(g, 0.5f);
                Check($"a {version} save ({map}): read, resumed there, the crow free (its old life)", g.Save.Loaded.CrowMode == HarukaSaveData.CrowFree &&
                    !g.Crow.Following && g.Session.TryGetSavePlace(out var m, out var l) && m == map && l == cell, $"{g.Save.LoadSource} {g.Session.World.MapId}");
            }
            // Following, saved in the East area, the game closed and opened.
            {
                var storage = new HarukaMemoryStorage();
                var g = C6.Start(storage);
                StartFollowing(g);
                Settle(g, 1.5f);
                Check("ついてきて is saved within about a second (a setting change)", C6.Latest(g)?.CrowMode == HarukaSaveData.CrowFollow, C6.Latest(g)?.CrowMode);
                Walk(g, new Vector2Int(20, 0));
                Settle(g, 4f);
                g.Save.OnPageHidden("");
                var saved = C6.Latest(g);
                Check("in the East area: saved there (east_area_01) with the crow following", saved.MapId == V33.EastId && saved.CrowMode == HarukaSaveData.CrowFollow,
                    HarukaSaveSystem.Describe(saved));
                var r = C6.Start(storage);
                Check("opened again: she resumes in the East area, the crow following, on a free cell beside her (not at its first_field spawn)",
                    r.Session.CurrentRegionMapId == V33.EastId && r.Crow.Following && r.Crow.DistanceToPlayer >= 1 && r.Crow.DistanceToPlayer <= 6 &&
                    r.Crow.gameObject.activeSelf && r.Session.OutdoorWorld.CanEnter(r.Crow.Cell) && r.Session.OutdoorIndex.RegionAt(r.Crow.Cell)?.MapId == V33.EastId,
                    Where(r));
                Settle(r, 3f);
                Walk(r, new Vector2Int(14, -1));
                Settle(r, 3f);
                Check("and walks with her again", r.Crow.DistanceToPlayer <= 2 && r.Crow.Following, Where(r));
                // Saved in the house and the booth.
                r.Session.OutdoorWorld.TryGetHouseDoor(out var door);
                Walk(r, door + Vector2Int.down);
                V32.Step(r, Vector2Int.up, 0.4f);
                V32.WalkTo(r, WorldVer31Checks.BoothApproach, null, 0.14f);
                V32.Step(r, Vector2Int.up, 0.4f);
                r.Save.OnPageHidden("");
                Check("saved in the booth with the crow following", C6.Latest(r).MapId == "halka_soundbooth" && C6.Latest(r).CrowMode == HarukaSaveData.CrowFollow);
                var s = C6.Start(storage);
                Check("opened again in the booth: following, the crow not in the room (waiting outside by the house's door)",
                    s.Session.World.MapId == "halka_soundbooth" && s.Crow.Following && s.Crow.WaitingOutside && !s.Crow.gameObject.activeSelf &&
                    s.Session.OutdoorWorld.CanEnter(s.Crow.Cell), Where(s));
                V32.Step(s, Vector2Int.down, 0.4f);
                V32.WalkTo(s, new Vector2Int(0, -3), null, 0.14f);
                V32.Step(s, Vector2Int.down, 0.4f);
                Settle(s, 0.2f);
                Check("out of the house: the crow is beside the door and follows", s.Session.World == s.Session.OutdoorWorld && s.Crow.gameObject.activeSelf &&
                    s.Crow.Following && s.Crow.DistanceToPlayer <= 3, Where(s));
                // Export / import.
                var exported = s.Save.ExportText();
                Check("export: the file has the crow's mode", exported != null && exported.Contains("\"crowCompanion\": { \"mode\": \"follow\""));
                var other = new HarukaMemoryStorage();
                var t = C6.Start(other);
                Settle(t, 1.2f);
                var check = t.Save.CheckImport(exported, out var data);
                var imported = t.Save.ApplyImport(data, C6.Clock, out var problem);
                Settle(t, 0.5f);
                Check("import into a free game: checked, applied, the crow follows (and the next save keeps it)", check == HarukaSaveSystem.ImportCheck.Ok &&
                    imported && t.Crow.Following && C6.Latest(t).CrowMode == HarukaSaveData.CrowFollow, problem);
                var damaged = exported.Replace("\"mode\": \"follow\"", "\"mode\": \"fly\"");
                Check("import of a file with a damaged crow entry: refused, nothing changed", t.Save.CheckImport(damaged, out _) == HarukaSaveSystem.ImportCheck.Unreadable &&
                    t.Crow.Following);
            }
            // A damaged newest snapshot: the older one is loaded (the mode with it).
            {
                var storage = new HarukaMemoryStorage();
                var g = C6.Start(storage);
                StartFollowing(g);
                Settle(g, 1.5f);
                g.Save.Write("check", C6.Clock);
                var latest = C6.Latest(g);
                var names = HarukaSnapshotStore.SlotNames;
                var newest = names.First(n => HarukaSaveData.TryRead(storage.Read(n), out var d, out _) == HarukaSaveReadStatus.Valid && d.Revision == latest.Revision);
                storage.Write(newest, storage.Read(newest).Replace("\"mode\": \"follow\"", "\"mode\": \"free\""));
                var r = C6.Start(storage);
                Check("the newest snapshot's crow entry damaged: it is skipped (backup snapshot), the older one loaded, following",
                    r.Save.LoadSource == "backup snapshot" && r.Crow.Following, r.Save.LoadSource);
            }
            // Rollback: a ver3.4 file in an older build keeps its values; nothing of the older save is lost.
            Check("rollback: a ver3.4 save is Schema 1 with the ver3.3 values and checksum (an older build loads it; the crow is free there)",
                C6.Sealed(follow).Contains("\"schemaVersion\": 1") && stripped.Length < followText.Length);
        }

        // A following save whose crow entry is taken from another following save (its own values
        // and checksum are fine; the crow entry's checksum belongs to the other file).
        private static string CopyCrowEntry(string target)
        {
            var other = C6.Sample();
            other.CrowMode = HarukaSaveData.CrowFollow;
            other.CellX += 1;
            var donor = System.Text.RegularExpressions.Regex.Match(C6.Sealed(other), @"""crowCompanion"": \{[^}]*\}").Value;
            return System.Text.RegularExpressions.Regex.Replace(target, @"""crowCompanion"": \{[^}]*\}", donor.Replace("$", "$$"));
        }

        // ---------------------------------------------------------------- what must not change

        private static void ProtectChecks()
        {
            Check("WORLD EDITOR v1.0 unchanged since ver3.3 (tools/HalkaWorldMapEditor)", V32.Git("diff --quiet cae5bef -- tools/HalkaWorldMapEditor", out _));
            // ver3.6: the existing files unchanged, the catalog and the registry only added to (a new Region).
            Check("the official Maps and their Authoring JSON unchanged since ver3.3", WorldVer36Checks.MapsFolderKept("cae5bef"));
            Check("direction_themes.json and the outdoor world registry unchanged", WorldVer36Checks.RegistryKept("cae5bef"));
            Check("the published ver3.3 (halkaworld/) unchanged", V32.Git("diff --quiet cae5bef -- halkaworld", out _));
            // ver3.6: the crow's own files (new pictures and models of other Objects may be there).
            Check("the crow's sprites, model and voice unchanged (no new crow made)", WorldVer36Checks.CrowKept("cae5bef", false));
        }
    }
}
