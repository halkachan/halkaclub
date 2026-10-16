using System.Collections.Generic;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    public enum CrowActivity { GroundIdle, GroundWalk, Takeoff, Flying, Perched, LeavingPerch, LandingGround }

    // A place the crow can perch on: a tree's perch point, from its 2D sprite or its 3D model.
    public interface ICrowPerch
    {
        bool IsUsable { get; }
        Vector3 PerchPosition { get; }
    }

    // The crow's behaviour, shared by every presentation (ver3.0): ground wander on the Grid,
    // the flight to a tree, the perch rest, the return to the ground, the talk-to reaction, and
    // which frame of the existing sprites belongs to each moment. A host supplies the world
    // (Grid blocking, dynamic occupancy, trees, grass) and shows the frames; CrowWander2D is the
    // 2D game's host. Positions live in the host's space; AltitudeAxis is "up" in that space.
    public abstract class CrowBehaviourCore : MonoBehaviour,
        Halka.Game.Interaction.IInteractionAvailability, Halka.Game.Interaction.IExamineReaction
    {
        public const float StepSeconds = 0.27f;
        public const float WalkFrameSeconds = 0.12f;
        public const float FlightFrameSeconds = 0.16f;
        public const float InteractionTakeoffDelaySeconds = 0.45f;
        public const float InteractionCooldownSeconds = 0.9f;

        [SerializeField] protected Sprite idleDown;
        [SerializeField] protected Sprite idleUp;
        [SerializeField] protected Sprite idleLeft;
        [SerializeField] protected Sprite idleRight;
        [SerializeField] protected Sprite[] walkDown;
        [SerializeField] protected Sprite[] walkUp;
        [SerializeField] protected Sprite[] walkLeft;
        [SerializeField] protected Sprite[] walkRight;
        [SerializeField] protected Sprite[] flightRight;
        [SerializeField] protected Sprite[] flightDown;
        [SerializeField, Range(10f, 90f)] protected float minimumGroundSeconds = 20f;
        [SerializeField, Range(10f, 90f)] protected float maximumGroundSeconds = 40f;
        [SerializeField, Range(1f, 20f)] protected float minimumPerchSeconds = 4f;
        [SerializeField, Range(1f, 20f)] protected float maximumPerchSeconds = 8f;
        [SerializeField, Range(0.5f, 6f)] protected float flightUnitsPerSecond = 2.2f;
        [SerializeField] protected Vector2Int wanderMinimumOffset;
        [SerializeField] protected Vector2Int wanderMaximumOffset;
        [SerializeField] protected Vector2Int spawnCell;
        [SerializeField] protected string initialFacing = "right";

        protected GridStepMotion motion;
        protected MapDefinition activeMap;
        protected ICrowPerch targetTree;
        protected ICrowPerch previousTree;
        protected CrowActivity activity = CrowActivity.GroundIdle;
        protected Vector3 legStart;
        protected Vector3 legEnd;
        protected Vector2Int landingCell;
        protected float legStartedAt;
        protected float legDuration;
        protected float nextFlightAt;
        protected float perchedUntil;
        protected float nextLandingSearchAt;
        protected bool flyingTowardGround;
        protected float nextActionAt;
        protected float lastTickAt;
        protected float interactionTakeoffAt = -1f;
        protected float interactionCooldownUntil;

        public Vector2Int Cell => motion != null ? motion.Cell : PositionToCell(transform.position);
        public Vector2Int StepFromCell => motion != null ? motion.StepFromCell : Cell;
        public Vector2Int StepToCell => motion != null ? motion.StepToCell : Cell;
        public bool IsMoving => motion != null && motion.IsMoving;
        public bool GroundOccupancyActive => activity == CrowActivity.GroundIdle ||
            activity == CrowActivity.GroundWalk || activity == CrowActivity.Takeoff;
        public bool CanInteract => activity == CrowActivity.GroundIdle && !IsMoving &&
            interactionTakeoffAt < 0f && Time.time >= interactionCooldownUntil;
        public float StepProgressNormalized => motion != null ? motion.StepProgressNormalized : 1f;
        public FacingDirection Facing { get; protected set; } = FacingDirection.Right;
        public Vector2Int RangeMin => spawnCell + wanderMinimumOffset;
        public Vector2Int RangeMax => spawnCell + wanderMaximumOffset;
        public CrowActivity Activity => activity;
        public ICrowPerch TargetPerch => targetTree;
        // The current flight leg (host space) and its progress, for presenters that pose a model.
        public Vector3 LegStart => legStart;
        public Vector3 LegEnd => legEnd;
        public float LegProgress => legDuration > 0f ? Mathf.Clamp01((lastTickAt - legStartedAt) / legDuration) : 1f;
        public Vector2 GroundPosition => motion != null ? motion.Position : (Vector2)Cell;
        public bool TalkTakeoffPending => interactionTakeoffAt >= 0f;

        // ---------------------------------------------------------------- host world
        protected abstract bool CanEnterCell(Vector2Int cell);
        protected abstract bool CanCrowEnterCell(Vector2Int cell);
        protected abstract Vector3 CellToPosition(Vector2 cell);
        protected abstract Vector2Int PositionToCell(Vector3 position);
        protected abstract IReadOnlyList<ICrowPerch> PerchCandidates();
        protected abstract void RustleAt(Vector2Int cell);
        // "Up" for takeoff and landing legs: screen up in the 2D game, altitude in 3D.
        protected virtual Vector3 AltitudeAxis => Vector3.up;
        // Leg direction as the 2D game sees it (x = East, y = North plus height).
        protected virtual Vector2 ScreenDelta(Vector3 from, Vector3 to) => new Vector2(to.x - from.x, to.y - from.y);

        // ---------------------------------------------------------------- host presentation
        protected abstract bool HasVisual { get; }
        protected abstract void ShowFrame(Sprite sprite, bool flipX);
        protected virtual void SetPointerInteraction(bool enabled) { }
        protected virtual void OnTakeoffVisual() { }
        protected virtual void OnLandedVisual() { }

        protected void ApplySpawnState(EntitySpawnPlacement spawn)
        {
            spawnCell = spawn.Cell;
            wanderMinimumOffset = spawn.Definition.WanderMinimum;
            wanderMaximumOffset = spawn.Definition.WanderMaximum;
            initialFacing = spawn.Facing;
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = CellToPosition(spawnCell);
            motion = new GridStepMotion(spawnCell);
            activity = CrowActivity.GroundIdle;
            targetTree = null;
            flyingTowardGround = false;
            nextActionAt = Time.time + 2f;
            nextFlightAt = Time.time + Random.Range(minimumGroundSeconds, maximumGroundSeconds);
            interactionTakeoffAt = -1f;
            interactionCooldownUntil = 0f;
            lastTickAt = Time.time;
        }

        public static void RequireCrowSpawn(EntitySpawnPlacement spawn)
        {
            if (spawn.Definition == null || spawn.Definition.RuntimeBehavior != "crow-wander")
                throw new System.InvalidOperationException("Expected a crow-wander Entity Spawn");
        }

        public void OnExamined()
        {
            if (!CanInteract) return;
            interactionCooldownUntil = Time.time + InteractionCooldownSeconds;
            if (PerchCandidates().Count > 0)
                interactionTakeoffAt = Time.time + InteractionTakeoffDelaySeconds;
        }

        private bool TryStartTakeoff(float now, bool preferNearby = false)
        {
            var trees = PerchCandidates();
            if (trees.Count == 0 || flightRight == null || flightRight.Length < 2 ||
                flightDown == null || flightDown.Length < 2) return false;
            var first = Random.Range(0, trees.Count);
            targetTree = trees[first];
            if (preferNearby)
            {
                var nearestDistance = float.MaxValue;
                foreach (var tree in trees)
                    nearestDistance = Mathf.Min(nearestDistance,
                        Vector3.Distance(transform.position, tree.PerchPosition));
                var near = new List<ICrowPerch>();
                foreach (var tree in trees)
                    if (Vector3.Distance(transform.position, tree.PerchPosition) <=
                        nearestDistance + GridWorld2D.TileWorldSize * 2f) near.Add(tree);
                targetTree = near[Random.Range(0, near.Count)];
            }
            else if (trees.Count > 1 && targetTree == previousTree)
                targetTree = trees[(first + 1) % trees.Count];
            previousTree = targetTree;
            SetPointerInteraction(false);
            OnTakeoffVisual();
            activity = CrowActivity.Takeoff;
            StartLeg(transform.position, transform.position + AltitudeAxis * 0.32f, now, 0.45f);
            return true;
        }

        // A destroyed Unity object counts as missing, as with the original TreePerchAnchor2D field.
        private static bool IsMissing(ICrowPerch perch) =>
            perch == null || perch is Object unityObject && unityObject == null;

        private void StartLeg(Vector3 start, Vector3 end, float now, float duration)
        {
            legStart = start;
            legEnd = end;
            legStartedAt = now;
            legDuration = Mathf.Max(0.01f, duration);
        }

        private float FlightDuration(Vector3 from, Vector3 to) =>
            Mathf.Clamp(Vector3.Distance(from, to) / flightUnitsPerSecond, 0.55f, 3.5f);

        private void TickAir(float now)
        {
            if (activity == CrowActivity.Perched)
            {
                if (IsMissing(targetTree) || !targetTree.IsUsable) { BeginGroundReturn(now); return; }
                transform.position = targetTree.PerchPosition;
                if (now >= perchedUntil)
                {
                    activity = CrowActivity.LeavingPerch;
                    StartLeg(transform.position, transform.position + AltitudeAxis * 0.25f, now, 0.35f);
                }
                return;
            }
            if (activity == CrowActivity.LandingGround && legDuration <= 0f)
            {
                if (now >= nextLandingSearchAt) BeginGroundReturn(now);
                return;
            }
            var progress = Mathf.Clamp01((now - legStartedAt) / legDuration);
            transform.position = Vector3.Lerp(legStart, legEnd, progress);
            if (progress < 1f) return;
            switch (activity)
            {
                case CrowActivity.Takeoff:
                    activity = CrowActivity.Flying;
                    flyingTowardGround = false;
                    if (IsMissing(targetTree) || !targetTree.IsUsable) { BeginGroundReturn(now); break; }
                    StartLeg(transform.position, targetTree.PerchPosition, now,
                        FlightDuration(transform.position, targetTree.PerchPosition));
                    break;
                case CrowActivity.Flying:
                    if (flyingTowardGround)
                    {
                        activity = CrowActivity.LandingGround;
                        StartLeg(transform.position, CellToPosition(landingCell), now, 0.4f);
                    }
                    else
                    {
                        activity = CrowActivity.Perched;
                        perchedUntil = now + Random.Range(minimumPerchSeconds, maximumPerchSeconds);
                    }
                    break;
                case CrowActivity.LeavingPerch:
                    BeginGroundReturn(now);
                    break;
                case CrowActivity.LandingGround:
                    if (CanLand(landingCell)) FinishGroundLanding(now);
                    else BeginGroundReturn(now);
                    break;
            }
        }

        private void BeginGroundReturn(float now)
        {
            targetTree = null;
            if (!TryChooseGroundLanding(out landingCell))
            {
                activity = CrowActivity.LandingGround;
                legDuration = 0f;
                nextLandingSearchAt = now + 1f;
                return;
            }
            activity = CrowActivity.Flying;
            flyingTowardGround = true;
            var approach = CellToPosition(landingCell) + AltitudeAxis * 0.3f;
            StartLeg(transform.position, approach, now, FlightDuration(transform.position, approach));
        }

        private bool TryChooseGroundLanding(out Vector2Int cell)
        {
            var candidates = new List<Vector2Int>();
            for (var y = RangeMin.y; y <= RangeMax.y; y++)
            for (var x = RangeMin.x; x <= RangeMax.x; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (CanLand(candidate)) candidates.Add(candidate);
            }
            if (candidates.Count == 0) { cell = default; return false; }
            cell = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private bool CanLand(Vector2Int cell)
        {
            if (!CanEnterCell(cell) || !CanCrowEnterCell(cell)) return false;
            if (activeMap != null)
            {
                if (cell == activeMap.HouseDoorCell) return false;
                foreach (var transition in activeMap.AreaTransitions)
                    if (transition.SourceCell == cell) return false;
            }
            return true;
        }

        private void FinishGroundLanding(float now)
        {
            transform.position = CellToPosition(landingCell);
            motion = new GridStepMotion(landingCell);
            activity = CrowActivity.GroundIdle;
            flyingTowardGround = false;
            OnLandedVisual();
            SetPointerInteraction(true);
            nextActionAt = now + Random.Range(2f, 4f);
            nextFlightAt = now + Random.Range(minimumGroundSeconds, maximumGroundSeconds);
            RefreshVisual();
        }

        protected void RefreshAirVisual(float now)
        {
            if (!HasVisual) return;
            if (activity == CrowActivity.GroundIdle || activity == CrowActivity.GroundWalk)
            {
                RefreshVisual();
                return;
            }
            if (activity == CrowActivity.Perched)
            {
                ShowFrame(idleRight, false);
                return;
            }
            var delta = ScreenDelta(legStart, legEnd);
            var horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            var frames = horizontal ? flightRight : flightDown;
            ShowFrame(frames[Mathf.FloorToInt(now / FlightFrameSeconds) % frames.Length],
                horizontal && delta.x < 0f);
        }

        protected virtual void Awake()
        {
            motion = new GridStepMotion(PositionToCell(transform.position));
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = CellToPosition(motion.Cell);
            nextActionAt = Time.time + 2f;
            nextFlightAt = Time.time + Random.Range(minimumGroundSeconds, maximumGroundSeconds);
            lastTickAt = Time.time;
            RefreshVisual();
        }

        protected virtual void OnDisable()
        {
            targetTree = null;
            flyingTowardGround = false;
            interactionTakeoffAt = -1f;
            SetPointerInteraction(false);
        }

        protected virtual void Update() => Tick(Time.time);

        public void Tick(float now)
        {
            if (motion == null) return;
            var deltaTime = Mathf.Max(0f, now - lastTickAt);
            lastTickAt = now;
            if (activity != CrowActivity.GroundIdle && activity != CrowActivity.GroundWalk)
            {
                TickAir(now);
                RefreshAirVisual(now);
                return;
            }
            var wasMoving = motion.IsMoving;
            motion.Advance(deltaTime);
            transform.position = CellToPosition(motion.Position);
            activity = motion.IsMoving ? CrowActivity.GroundWalk : CrowActivity.GroundIdle;
            if (wasMoving && !motion.IsMoving)
            {
                SetPointerInteraction(true);
                nextActionAt = now + Random.Range(2f, 4f);
            }
            if (!motion.IsMoving && interactionTakeoffAt >= 0f)
            {
                if (now < interactionTakeoffAt) { RefreshVisual(); return; }
                interactionTakeoffAt = -1f;
                if (TryStartTakeoff(now, true))
                {
                    RefreshAirVisual(now);
                    return;
                }
            }
            if (!motion.IsMoving && now >= nextFlightAt)
            {
                if (TryStartTakeoff(now))
                {
                    RefreshAirVisual(now);
                    return;
                }
                nextFlightAt = now + 6f;
            }
            if (!motion.IsMoving && now >= nextActionAt)
                BeginWander(now);
            RefreshVisual();
        }

        private void BeginWander(float now)
        {
            nextActionAt = now + Random.Range(2f, 4f);
            if (Random.value < 0.3f) return;
            var directions = new[] { Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right };
            var first = Random.Range(0, directions.Length);
            for (var i = 0; i < directions.Length; i++)
            {
                var direction = directions[(first + i) % directions.Length];
                var target = Cell + direction;
                if (target.x < RangeMin.x || target.x > RangeMax.x ||
                    target.y < RangeMin.y || target.y > RangeMax.y ||
                    !CanEnterCell(target) || !CanCrowEnterCell(target)) continue;
                if (!motion.TryBegin(direction,
                    cell => CanEnterCell(cell) && CanCrowEnterCell(cell),
                    StepSeconds)) continue;
                Facing = FacingDirectionExtensions.FromVector(direction);
                activity = CrowActivity.GroundWalk;
                SetPointerInteraction(false); // Pointer interaction only while stopped.
                RustleAt(target);
                break;
            }
        }

        protected void RefreshVisual()
        {
            if (!IsMoving)
            {
                ShowFrame(Facing switch
                {
                    FacingDirection.Up => idleUp,
                    FacingDirection.Left => idleLeft,
                    FacingDirection.Right => idleRight,
                    _ => idleDown
                }, false);
                return;
            }
            var frames = Facing switch
            {
                FacingDirection.Up => walkUp,
                FacingDirection.Left => walkLeft,
                FacingDirection.Right => walkRight,
                _ => walkDown
            };
            if (frames == null || frames.Length != 2)
                throw new System.InvalidOperationException("Crow needs two walk frames in every direction");
            var frame = Mathf.FloorToInt(StepProgressNormalized * StepSeconds /
                WalkFrameSeconds) % frames.Length;
            ShowFrame(frames[frame], false);
        }
    }
}
