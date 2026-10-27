using System.Collections.Generic;
using System.Diagnostics;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.4: whether the crow lives on its own (as before) or walks with her.
    public enum CrowCompanionMode { FreeRoam, FollowPlayer }

    // ver3.4: what the talk's choices answer.
    public enum CrowChoice { Follow, Play, Cancel }

    // ver3.4 companion mode. The mode is a wish ("walk with her"); the crow's activity (standing,
    // walking, taking off, flying, perched, landing) stays CrowBehaviourCore's and changes inside
    // it, so a rest on a tree never ends the companion mode. Everything is in world cells of the
    // outdoor world (any region); nothing belongs to a chunk or a region's Map.
    // - Beside her (2 cells or less) it settles: waits, turns, potters a cell or two, hops to
    //   another cell beside her, or rests on a tree near her while she stands still.
    // - Further away it walks toward a cell behind her (a bounded breadth-first search, planned
    //   again only when her cell changes or the way is blocked), never into the cells ahead of her.
    // - Far away (7 cells), now and then while she walks on (after 3 s, 35% every 3 s), or with no
    //   way on foot, it flies and lands behind her.
    // - When she keeps walking into it, it steps aside or flies up (she is never shut in).
    // - Stuck for 5 s, it is put on a free cell near her that the camera does not show.
    // - Indoors it waits on the ground beside the house's door and walks with her again outside.
    public sealed partial class PocCrow
    {
        public const string ChoiceTitle = "カラス";
        public const string ChoiceLine = "「カァ。」";
        public const string ChoiceFollow = "ついてきて";
        public const string ChoicePlay = "あそんでおいで";
        public const string ChoiceCancel = "やめる";
        public const float FollowStepSeconds = 0.2f;
        public const float FollowFlightUnitsPerSecond = 7f;
        public const int NearDistance = 2;
        public const int FlyDistance = 7;
        public const float LongWalkSeconds = 3f;
        public const float LongWalkFlightChance = 0.35f;
        public const int SearchMargin = 6;
        public const int MaxSearchCells = 900;
        public const int PerchReach = 4;
        public const float PushYieldSeconds = 0.3f;
        public const float InHerWaySeconds = 1.2f;
        public const float StuckRelocateSeconds = 5f;

        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private readonly List<Vector2Int> path = new List<Vector2Int>();
        private readonly List<ICrowPerch> nearbyPerches = new List<ICrowPerch>();
        private readonly Dictionary<Vector2Int, Vector2Int> searchFrom = new Dictionary<Vector2Int, Vector2Int>();
        private readonly Queue<Vector2Int> searchQueue = new Queue<Vector2Int>();
        private readonly HashSet<Vector2Int> slots = new HashSet<Vector2Int>();
        private readonly Stopwatch searchWatch = new Stopwatch();
        private int pathIndex;
        private Vector2Int pathPlannedFor;
        private bool pathValid;
        private Vector2Int heading = Vector2Int.down;
        private Vector2Int lastPlayerCell;
        private float playerStillSince;
        private float playerWalkingSince = -1f;
        private float lastPlayerMoveAt = -100f;
        private float nextWalkFlightAt;
        private float nextFollowStepAt;
        private float nextSettleAt;
        private float inHerWaySince = -1f;
        private float stuckSince = -1f;
        private float hoverSince = -1f;
        private bool wasNear = true;
        private Vector2Int hopOrigin = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int perchedPlayerCell;
        private bool placed;

        public CrowCompanionMode Mode { get; private set; }
        public bool Following => Mode == CrowCompanionMode.FollowPlayer;
        // The choices are open: the crow waits on its cell until one is chosen.
        public bool ChoiceOpen { get; private set; }
        // Indoors in companion mode: the crow waits (not drawn) beside the house's door.
        public bool WaitingOutside { get; private set; }
        // Set by PocGameplay: opens the choices (false: no menu, the old talk reaction instead).
        public System.Func<bool> OpenChoices { get; set; }
        // Set by PocGameplay: whether a world point is on screen in the current view.
        public System.Func<Vector3, bool> IsInView { get; set; }
        public bool HasPlace => placed;

        // The numbers of the checks and the measurements.
        public int ModeChanges { get; private set; }
        public int PathSearches { get; private set; }
        public int PathFailures { get; private set; }
        public int CatchUpFlights { get; private set; }
        public int ShortHops { get; private set; }
        public int NearbyPerchFlights { get; private set; }
        public int CompanionSteps { get; private set; }
        public int Yields { get; private set; }
        public int Relocations { get; private set; }
        public int LastSearchCells { get; private set; }
        public double LastSearchMilliseconds { get; private set; }
        public double MaxSearchMilliseconds { get; private set; }
        public double TotalSearchMilliseconds { get; private set; }
        public Vector2Int LastWaitingCell { get; private set; }
        public string CompanionState => WaitingOutside ? "WaitingOutside" : !isActiveAndEnabled ? "Inactive" : Activity.ToString();

        // Ground cells between the crow and her (-1 when she is not outdoors or the crow is away).
        public int DistanceToPlayer => PlayerOutdoors && isActiveAndEnabled ? Manhattan(Cell, session.Cell) : -1;

        private bool PlayerOutdoors => session != null && session.World != null && session.World == session.OutdoorWorld;
        private Vector2Int PlayerTarget => session.IsMoving ? session.StepToCell : session.Cell;

        // ---------------------------------------------------------------- the mode

        public void SetMode(CrowCompanionMode mode, float now)
        {
            if (Mode == mode) return;
            Mode = mode;
            ModeChanges++;
            ResetCompanion(now);
            if (mode == CrowCompanionMode.FreeRoam) RestartWanderTimers(now);
        }

        // A save or an import: the mode before the crow is placed (no reaction, no flight).
        public void RestoreMode(CrowCompanionMode mode)
        {
            Mode = mode;
            ResetCompanion(Time.time);
        }

        private void ResetCompanion(float now)
        {
            path.Clear();
            pathValid = false;
            stuckSince = -1f;
            hoverSince = -1f;
            inHerWaySince = -1f;
            nextFollowStepAt = now;
            nextSettleAt = now + Random.Range(1.5f, 3f);
            playerStillSince = now;
            // A save restores the mode before the session has placed her (the game's start order).
            if (session != null && session.World != null) { lastPlayerCell = session.Cell; heading = session.Facing.ToVector(); }
        }

        // The choice made in the menu (or the menu closed: Cancel).
        public void Choose(CrowChoice choice, float now)
        {
            if (!ChoiceOpen) return;
            ChoiceOpen = false;
            interactionCooldownUntil = now + InteractionCooldownSeconds;
            switch (choice)
            {
                case CrowChoice.Follow:
                    SetMode(CrowCompanionMode.FollowPlayer, now);
                    nextSettleAt = now + Random.Range(2f, 4f);
                    break;
                case CrowChoice.Play:
                    SetMode(CrowCompanionMode.FreeRoam, now);
                    FlyHome(now);
                    break;
                default:
                    // やめる: nothing changes. The free crow reacts as it always did (a tree nearby).
                    if (!Following && PerchCandidates().Count > 0)
                        interactionTakeoffAt = now + InteractionTakeoffDelaySeconds;
                    break;
            }
        }

        // あそんでおいで: back to its life; from another region it flies to a tree of its own.
        private void FlyHome(float now)
        {
            var home = HomeRegion;
            var trees = PerchCandidates();
            if (trees.Count == 0) return;
            if (home != null && home.ContainsWorld(Cell))
            {
                interactionTakeoffAt = now + InteractionTakeoffDelaySeconds;
                return;
            }
            var nearest = trees[0];
            foreach (var tree in trees)
                if (Vector3.Distance(transform.position, tree.PerchPosition) < Vector3.Distance(transform.position, nearest.PerchPosition))
                    nearest = tree;
            TryTakeoffToward(nearest, now);
        }

        private Halka.Poc2D3D.World.OutdoorRegion HomeRegion =>
            session != null && session.OutdoorIndex != null && activeMap != null ? session.OutdoorIndex.FindRegion(activeMap.MapId) : null;

        // ---------------------------------------------------------------- CrowBehaviourCore decisions

        protected override bool DecideOnGround(float now)
        {
            if (ChoiceOpen) return true;
            if (!Following) return false;
            if (!PlayerOutdoors) return true;
            TrackPlayer(now);
            var target = PlayerTarget;
            var distance = Manhattan(Cell, target);
            // She keeps walking into it: aside, or up.
            if (session.PushSeconds >= PushYieldSeconds && session.PushedCell == Cell)
            {
                Yield(now, target);
                return true;
            }
            if (distance >= FlyDistance)
            {
                if (StartFlight(now, false)) return true;
            }
            else if (distance >= 2 && playerWalkingSince >= 0f && now - playerWalkingSince >= LongWalkSeconds && now >= nextWalkFlightAt)
            {
                // She has walked on for a while: now and then a short flight to her (a crow's way).
                nextWalkFlightAt = now + LongWalkSeconds;
                if (Random.value < LongWalkFlightChance && StartFlight(now, false)) return true;
            }
            var inHerWay = InHerWay(Cell, target);
            if (inHerWay && !session.IsMoving)
            {
                if (inHerWaySince < 0f) inHerWaySince = now;
            }
            else inHerWaySince = -1f;
            if (distance > NearDistance || inHerWay && (session.IsMoving || now - inHerWaySince >= InHerWaySeconds))
            {
                if (wasNear)
                {
                    // A moment before following, so it never moves in step with her.
                    wasNear = false;
                    nextFollowStepAt = now + Random.Range(0.1f, 0.35f);
                }
                if (now < nextFollowStepAt) return true;
                if (TryWalkToward(target)) { stuckSince = -1f; return true; }
                if (StartFlight(now, false)) { stuckSince = -1f; return true; }
                if (stuckSince < 0f) stuckSince = now;
                if (now - stuckSince >= StuckRelocateSeconds) Relocate(now);
                return true;
            }
            wasNear = true;
            stuckSince = -1f;
            Settle(now, target);
            return true;
        }

        protected override bool PerchRestOver(float now)
        {
            if (!Following) return base.PerchRestOver(now);
            if (!PlayerOutdoors) return base.PerchRestOver(now);
            TrackPlayer(now);
            // She walks on: back down to her at once.
            return base.PerchRestOver(now) || session.IsMoving || session.Cell != perchedPlayerCell;
        }

        protected override float FlightUnitsPerSecond => Following ? FollowFlightUnitsPerSecond : base.FlightUnitsPerSecond;

        protected override bool TryChooseGroundLanding(out Vector2Int cell)
        {
            if (!Following || !PlayerOutdoors) return base.TryChooseGroundLanding(out cell);
            var target = PlayerTarget;
            var behind = target - heading * 2;
            cell = default;
            var best = float.MaxValue;
            for (var dy = -3; dy <= 3; dy++)
            for (var dx = -3; dx <= 3; dx++)
            {
                var c = target + new Vector2Int(dx, dy);
                var d = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (d < 1 || d > 3 || !CanLand(c)) continue;
                var score = Manhattan(c, behind) * 2f + (c == hopOrigin ? 6f : 0f) + Random.value;
                if (score < best) { best = score; cell = c; }
            }
            hopOrigin = new Vector2Int(int.MinValue, int.MinValue);
            return best < float.MaxValue;
        }

        protected override bool CanLand(Vector2Int cell)
        {
            if (!base.CanLand(cell)) return false;
            if (!Following || !PlayerOutdoors) return true;
            return CanStand(cell) && !InHerWay(cell, PlayerTarget);
        }

        protected override void Update()
        {
            WatchAir(Time.time);
            base.Update();
        }

        // In the air with nowhere to land for a while (the core hovers and looks again every
        // second): put it down near her out of sight.
        public void WatchAir(float now)
        {
            if (!Following || !PlayerOutdoors || Activity != CrowActivity.LandingGround || legDuration > 0f)
            {
                hoverSince = -1f;
                return;
            }
            if (hoverSince < 0f) hoverSince = now;
            if (now - hoverSince >= StuckRelocateSeconds) { hoverSince = -1f; Relocate(now); }
        }

        // ---------------------------------------------------------------- following

        private void TrackPlayer(float now)
        {
            if (session.IsMoving)
            {
                var step = session.StepToCell - session.StepFromCell;
                if (step != Vector2Int.zero) heading = step;
            }
            if (session.IsMoving || session.Cell != lastPlayerCell)
            {
                playerStillSince = now;
                // A walk goes on through the moment between two steps; a pause of 0.4 s ends it.
                if (playerWalkingSince < 0f || now - lastPlayerMoveAt > 0.4f) playerWalkingSince = now;
                lastPlayerMoveAt = now;
            }
            else if (now - lastPlayerMoveAt > 0.4f) playerWalkingSince = -1f;
            lastPlayerCell = session.Cell;
        }

        // A cell the crow may stand on beside her: the ground, drawn (unless placing it before the
        // first chunks are drawn), no door, not hers.
        private bool CanStand(Vector2Int cell, bool drawn = true) =>
            CanEnterCell(cell) && CanCrowEnterCell(cell) && (!drawn || session.IsCellReady(cell)) &&
            !session.IsTransitionCell(session.OutdoorWorld, cell);

        // The two cells ahead of her (where she is going) are never the crow's to stand on.
        private bool InHerWay(Vector2Int cell, Vector2Int target) =>
            cell == target + heading || cell == target + heading * 2;

        private bool TryWalkToward(Vector2Int target)
        {
            if (!pathValid || pathPlannedFor != target || pathIndex >= path.Count || !CanStand(path[pathIndex]))
            {
                if (!PlanPath(target)) return false;
            }
            if (pathIndex >= path.Count) return false;
            var direction = path[pathIndex] - Cell;
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1 || !TryBeginStep(direction, FollowStepSeconds))
            {
                pathValid = false;
                return false;
            }
            pathIndex++;
            CompanionSteps++;
            return true;
        }

        // The places beside her the crow walks to: behind her first, then her sides (2 cells or
        // less, never the cells ahead of her).
        private void CollectSlots(Vector2Int target)
        {
            slots.Clear();
            var side = new Vector2Int(heading.y, -heading.x);
            AddSlot(target - heading * 2, target);
            AddSlot(target - heading + side, target);
            AddSlot(target - heading - side, target);
            AddSlot(target - heading, target);
            AddSlot(target + side * 2, target);
            AddSlot(target - side * 2, target);
            AddSlot(target + side, target);
            AddSlot(target - side, target);
        }

        private void AddSlot(Vector2Int cell, Vector2Int target)
        {
            if (cell != Cell && CanStand(cell) && !InHerWay(cell, target)) slots.Add(cell);
        }

        // Breadth-first search from the crow to the nearest place beside her, inside the box
        // around the two of them (SearchMargin) and at most MaxSearchCells cells.
        private bool PlanPath(Vector2Int target)
        {
            searchWatch.Restart();
            PathSearches++;
            path.Clear();
            pathIndex = 0;
            pathValid = false;
            pathPlannedFor = target;
            CollectSlots(target);
            var start = Cell;
            var min = Vector2Int.Min(start, target) - new Vector2Int(SearchMargin, SearchMargin);
            var max = Vector2Int.Max(start, target) + new Vector2Int(SearchMargin, SearchMargin);
            searchFrom.Clear();
            searchQueue.Clear();
            searchFrom[start] = start;
            searchQueue.Enqueue(start);
            var found = false;
            var goal = start;
            while (searchQueue.Count > 0 && searchFrom.Count <= MaxSearchCells)
            {
                var c = searchQueue.Dequeue();
                if (slots.Contains(c)) { found = true; goal = c; break; }
                foreach (var step in Steps)
                {
                    var n = c + step;
                    if (n.x < min.x || n.y < min.y || n.x > max.x || n.y > max.y || searchFrom.ContainsKey(n) || !CanStand(n)) continue;
                    searchFrom[n] = c;
                    searchQueue.Enqueue(n);
                }
            }
            if (found)
            {
                for (var c = goal; c != start; c = searchFrom[c]) path.Add(c);
                path.Reverse();
                pathValid = true;
            }
            else PathFailures++;
            LastSearchCells = searchFrom.Count;
            searchWatch.Stop();
            LastSearchMilliseconds = searchWatch.Elapsed.TotalMilliseconds;
            TotalSearchMilliseconds += LastSearchMilliseconds;
            if (LastSearchMilliseconds > MaxSearchMilliseconds) MaxSearchMilliseconds = LastSearchMilliseconds;
            return found;
        }

        private bool StartFlight(float now, bool hop)
        {
            hopOrigin = hop ? Cell : new Vector2Int(int.MinValue, int.MinValue);
            if (!TryChooseGroundLanding(out _)) return false;
            hopOrigin = hop ? Cell : new Vector2Int(int.MinValue, int.MinValue);
            if (!TryTakeoffToward(null, now)) return false;
            pathValid = false;
            if (hop) ShortHops++;
            else CatchUpFlights++;
            return true;
        }

        // She keeps walking into the crow: a step that leaves her way, or else a flight.
        private void Yield(float now, Vector2Int target)
        {
            Yields++;
            var push = session.PushedCell - session.Cell;
            var side = new Vector2Int(push.y, -push.x);
            var options = Random.value < 0.5f ? new[] { side, -side, push } : new[] { -side, side, push };
            foreach (var direction in options)
            {
                var c = Cell + direction;
                if (!CanStand(c) || c == session.Cell) continue;
                if (TryBeginStep(direction, FollowStepSeconds)) { pathValid = false; return; }
            }
            StartFlight(now, true);
        }

        // Beside her and she stands still: mostly waiting; now and then a turn, a step, a hop, a tree.
        private void Settle(float now, Vector2Int target)
        {
            if (session.IsMoving || now < nextSettleAt) return;
            nextSettleAt = now + Random.Range(2.5f, 5f);
            var still = now - playerStillSince;
            var roll = Random.value;
            if (roll < 0.18f && still >= 4f && TryPerchNearby(now)) return;
            if (roll < 0.30f && still >= 2f && StartFlight(now, true)) return;
            if (roll < 0.55f && TryPotter(target)) return;
            if (roll < 0.80f) TurnTo(FacingToward(Cell, target));
        }

        // One step to another cell beside her (2 cells or less, not in her way).
        private bool TryPotter(Vector2Int target)
        {
            var first = Random.Range(0, Steps.Length);
            for (var i = 0; i < Steps.Length; i++)
            {
                var direction = Steps[(first + i) % Steps.Length];
                var c = Cell + direction;
                var d = Manhattan(c, target);
                if (d < 1 || d > NearDistance || InHerWay(c, target) || !CanStand(c)) continue;
                if (TryBeginStep(direction, FollowStepSeconds)) return true;
            }
            return false;
        }

        private bool TryPerchNearby(float now)
        {
            var trees = NearbyPerches();
            if (trees.Count == 0) return false;
            var tree = trees[Random.Range(0, trees.Count)];
            if (!TryTakeoffToward(tree, now)) return false;
            perchedPlayerCell = session.Cell;
            NearbyPerchFlights++;
            pathValid = false;
            return true;
        }

        // Trees whose perch is near her (any region: the trees of the region she walks in).
        private IReadOnlyList<ICrowPerch> NearbyPerches()
        {
            nearbyPerches.Clear();
            if (presenter == null || session == null || session.OutdoorWorld == null || !PlayerOutdoors) return nearbyPerches;
            var target = session.Cell;
            foreach (var instance in session.OutdoorWorld.Objects)
            {
                if (Manhattan(instance.RootCell, target) > PerchReach + 2) continue;
                if (!presenter.TryGetPerchPosition(instance, out var point)) continue;
                if (Manhattan(PositionToCell(point), target) > PerchReach) continue;
                if (!perchByTree.TryGetValue(instance.InstanceId, out var perch))
                {
                    perch = new TreePerch(presenter, instance);
                    perchByTree[instance.InstanceId] = perch;
                }
                nearbyPerches.Add(perch);
            }
            return nearbyPerches;
        }

        // Out of sight near her (the nearest free cell her way can reach that the camera does not
        // show; else the nearest free cell), standing, facing her. No step is counted.
        private void Relocate(float now)
        {
            if (!PlayerOutdoors) return;
            var origin = session.Cell;
            Vector2Int? hidden = null, any = null;
            searchFrom.Clear();
            searchQueue.Clear();
            searchFrom[origin] = origin;
            searchQueue.Enqueue(origin);
            while (searchQueue.Count > 0 && searchFrom.Count <= MaxSearchCells && hidden == null)
            {
                var c = searchQueue.Dequeue();
                var d = Manhattan(c, origin);
                if (d >= 2 && CanStand(c) && !InHerWay(c, origin))
                {
                    if (any == null) any = c;
                    if (d >= 4 && (IsInView == null || !IsInView(CellToPosition(c)))) hidden = c;
                }
                foreach (var step in Steps)
                {
                    var n = c + step;
                    if (searchFrom.ContainsKey(n) || !CanEnterCell(n) || session.IsTransitionCell(session.OutdoorWorld, n)) continue;
                    searchFrom[n] = c;
                    searchQueue.Enqueue(n);
                }
            }
            var cell = hidden ?? any;
            if (cell == null) return;
            PlaceOnGround(cell.Value, FacingToward(cell.Value, origin), now);
            Relocations++;
            stuckSince = -1f;
            pathValid = false;
        }

        // ---------------------------------------------------------------- outside the house

        // She goes in: the crow waits beside the door, on the ground (not drawn while she is in).
        public void WaitOutside(float now)
        {
            WaitingOutside = true;
            ChoiceOpen = false;
            if (TryFindWaitingCell(out var cell))
            {
                PlaceOnGround(cell, FacingDirection.Up, now);
                LastWaitingCell = cell;
                placed = true;
            }
        }

        // She comes out: it is where it waited (moved aside if that cell is no good now).
        public void ReturnFromWaiting(float now)
        {
            WaitingOutside = false;
            ResetCompanion(now);
            if (!CanStand(Cell, false) && TryFindWaitingCell(out var cell)) PlaceOnGround(cell, FacingDirection.Up, now);
            else PlaceOnGround(Cell, FacingToward(Cell, session.Cell), now);
        }

        // A resumed companion outdoors: on a free cell near her (never first at its spawn).
        public void JoinNear(float now)
        {
            ResetCompanion(now);
            placed = true;
            if (!PlayerOutdoors) return;
            var target = session.Cell;
            for (var radius = 2; radius <= 6; radius++)
                foreach (var c in Ring(target, radius))
                    if (CanStand(c, false) && !InHerWay(c, target))
                    {
                        PlaceOnGround(c, FacingToward(c, target), now);
                        return;
                    }
            Relocate(now);
        }

        public void MarkPlaced() => placed = true;

        // Beside the outside of the house's door: two cells left or right of where she comes out
        // (never her arrival cell, the cell below it, or the door), else the nearest such cell.
        private bool TryFindWaitingCell(out Vector2Int cell)
        {
            cell = default;
            var world = session != null ? session.OutdoorWorld : null;
            if (world == null || !world.TryGetHouseDoor(out var door)) return false;
            var exit = door + Vector2Int.down;
            for (var radius = 2; radius <= 5; radius++)
                foreach (var c in Ring(exit, radius))
                {
                    if (c == door || c == exit || c == exit + Vector2Int.down || c == exit + Vector2Int.down * 2) continue;
                    if (!world.CanEnter(c) || session.IsTransitionCell(world, c) || c.y > exit.y) continue;
                    cell = c;
                    return true;
                }
            return false;
        }

        // The cells at this Manhattan distance, the same row first, left before right.
        private static IEnumerable<Vector2Int> Ring(Vector2Int center, int radius)
        {
            for (var dy = 0; dy <= radius; dy++)
            {
                var dx = radius - dy;
                yield return center + new Vector2Int(-dx, -dy);
                if (dx != 0) yield return center + new Vector2Int(dx, -dy);
                if (dy == 0) continue;
                yield return center + new Vector2Int(-dx, dy);
                if (dx != 0) yield return center + new Vector2Int(dx, dy);
            }
        }

        private static FacingDirection FacingToward(Vector2Int from, Vector2Int to)
        {
            var d = to - from;
            if (d == Vector2Int.zero) return FacingDirection.Down;
            return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                ? d.x < 0 ? FacingDirection.Left : FacingDirection.Right
                : d.y < 0 ? FacingDirection.Down : FacingDirection.Up;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
