using System.Collections.Generic;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    public enum CrowActivity { GroundIdle, GroundWalk, Takeoff, Flying, Perched, LeavingPerch, LandingGround }

    // A small walker: one interpolated grid step, then a few seconds of rest.
    public sealed class CrowWander2D : MonoBehaviour, Halka.Game.Interaction.IInteractionAvailability
    {
        public const float StepSeconds = 0.27f;
        public const float WalkFrameSeconds = 0.12f;
        public const float FlightFrameSeconds = 0.16f;

        [SerializeField] private GridWorld2D world;
        [SerializeField] private DynamicGridOccupancy2D occupancy;
        [SerializeField] private GrassField2D grassField;
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private BoxCollider2D clickCollider;
        [SerializeField] private Sprite idleDown;
        [SerializeField] private Sprite idleUp;
        [SerializeField] private Sprite idleLeft;
        [SerializeField] private Sprite idleRight;
        [SerializeField] private Sprite[] walkDown;
        [SerializeField] private Sprite[] walkUp;
        [SerializeField] private Sprite[] walkLeft;
        [SerializeField] private Sprite[] walkRight;
        [SerializeField] private Sprite[] flightRight;
        [SerializeField] private Sprite[] flightDown;
        [SerializeField, Range(10f, 90f)] private float minimumGroundSeconds = 20f;
        [SerializeField, Range(10f, 90f)] private float maximumGroundSeconds = 40f;
        [SerializeField, Range(1f, 20f)] private float minimumPerchSeconds = 4f;
        [SerializeField, Range(1f, 20f)] private float maximumPerchSeconds = 8f;
        [SerializeField, Range(0.5f, 6f)] private float flightUnitsPerSecond = 2.2f;
        [SerializeField] private Vector2Int wanderMinimumOffset;
        [SerializeField] private Vector2Int wanderMaximumOffset;
        [SerializeField] private Vector2Int spawnCell;
        [SerializeField] private string initialFacing = "right";

        private GridStepMotion motion;
        private MapDefinition activeMap;
        private Transform treeRoot;
        private TreePerchAnchor2D targetTree;
        private TreePerchAnchor2D previousTree;
        private CrowActivity activity = CrowActivity.GroundIdle;
        private Vector3 legStart;
        private Vector3 legEnd;
        private Vector2Int landingCell;
        private float legStartedAt;
        private float legDuration;
        private float nextFlightAt;
        private float perchedUntil;
        private float nextLandingSearchAt;
        private bool flyingTowardGround;
        private int groundSortingOrder;
        private float nextActionAt;
        private float lastTickAt;

        public Vector2Int Cell => motion != null ? motion.Cell : world.WorldToCell(transform.position);
        public Vector2Int StepFromCell => motion != null ? motion.StepFromCell : Cell;
        public Vector2Int StepToCell => motion != null ? motion.StepToCell : Cell;
        public bool IsMoving => motion != null && motion.IsMoving;
        public bool GroundOccupancyActive => activity == CrowActivity.GroundIdle ||
            activity == CrowActivity.GroundWalk || activity == CrowActivity.Takeoff;
        public bool CanInteract => activity == CrowActivity.GroundIdle && !IsMoving;
        public float StepProgressNormalized => motion != null ? motion.StepProgressNormalized : 1f;
        public FacingDirection Facing { get; private set; } = FacingDirection.Right;
        public Vector2Int RangeMin => spawnCell + wanderMinimumOffset;
        public Vector2Int RangeMax => spawnCell + wanderMaximumOffset;
        public CrowActivity Activity => activity;
        public TreePerchAnchor2D TargetTree => targetTree;

        public void SetGrassField(GrassField2D field) => grassField = field;
        public void ConfigureFlightEnvironment(MapDefinition map, Transform objects)
        {
            activeMap = map;
            treeRoot = objects;
        }

        public void ApplySpawn(EntitySpawnPlacement spawn, GridWorld2D grid)
        {
            if (spawn.Definition == null || spawn.Definition.RuntimeBehavior != "crow-wander")
                throw new System.InvalidOperationException("Expected a crow-wander Entity Spawn");
            world = grid;
            if (groundSortingOrder == 0 && artwork != null)
                groundSortingOrder = artwork.sortingOrder;
            spawnCell = spawn.Cell;
            wanderMinimumOffset = spawn.Definition.WanderMinimum;
            wanderMaximumOffset = spawn.Definition.WanderMaximum;
            initialFacing = spawn.Facing;
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = world.CellToWorld(spawnCell);
            motion = new GridStepMotion(spawnCell);
            activity = CrowActivity.GroundIdle;
            targetTree = null;
            flyingTowardGround = false;
            nextActionAt = Time.time + 2f;
            nextFlightAt = Time.time + Random.Range(minimumGroundSeconds, maximumGroundSeconds);
            lastTickAt = Time.time;
            if (clickCollider != null) clickCollider.enabled = true;
            if (artwork != null)
            {
                artwork.flipX = false;
                artwork.sortingOrder = groundSortingOrder;
                RefreshVisual();
            }
        }

        public TreePerchAnchor2D[] FindPerchCandidates()
        {
            if (treeRoot == null || activeMap == null || world == null)
                return System.Array.Empty<TreePerchAnchor2D>();
            var found = new List<TreePerchAnchor2D>();
            foreach (var tree in treeRoot.GetComponentsInChildren<TreePerchAnchor2D>())
            {
                if (!tree.IsUsable) continue;
                if (!activeMap.Contains(world.WorldToCell(tree.PerchPosition))) continue;
                found.Add(tree);
            }
            return found.ToArray();
        }

        private bool TryStartTakeoff(float now)
        {
            var trees = FindPerchCandidates();
            if (trees.Length == 0 || flightRight == null || flightRight.Length < 2 ||
                flightDown == null || flightDown.Length < 2) return false;
            var first = Random.Range(0, trees.Length);
            targetTree = trees[first];
            if (trees.Length > 1 && targetTree == previousTree)
                targetTree = trees[(first + 1) % trees.Length];
            previousTree = targetTree;
            if (clickCollider != null) clickCollider.enabled = false;
            artwork.sortingOrder = Mathf.Max(groundSortingOrder + 1, 12);
            artwork.maskInteraction = SpriteMaskInteraction.None;
            activity = CrowActivity.Takeoff;
            StartLeg(transform.position, transform.position + Vector3.up * 0.32f, now, 0.45f);
            return true;
        }

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
                if (targetTree == null || !targetTree.IsUsable) { BeginGroundReturn(now); return; }
                transform.position = targetTree.PerchPosition;
                if (now >= perchedUntil)
                {
                    activity = CrowActivity.LeavingPerch;
                    StartLeg(transform.position, transform.position + Vector3.up * 0.25f, now, 0.35f);
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
                    if (targetTree == null || !targetTree.IsUsable) { BeginGroundReturn(now); break; }
                    StartLeg(transform.position, targetTree.PerchPosition, now,
                        FlightDuration(transform.position, targetTree.PerchPosition));
                    break;
                case CrowActivity.Flying:
                    if (flyingTowardGround)
                    {
                        activity = CrowActivity.LandingGround;
                        StartLeg(transform.position, world.CellToWorld(landingCell), now, 0.4f);
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
            var approach = world.CellToWorld(landingCell) + Vector3.up * 0.3f;
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
            if (world == null || !world.CanEnter(cell) ||
                occupancy != null && !occupancy.CanCrowEnter(cell)) return false;
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
            transform.position = world.CellToWorld(landingCell);
            motion = new GridStepMotion(landingCell);
            activity = CrowActivity.GroundIdle;
            flyingTowardGround = false;
            artwork.sortingOrder = groundSortingOrder;
            artwork.flipX = false;
            if (clickCollider != null) clickCollider.enabled = true;
            nextActionAt = now + Random.Range(2f, 4f);
            nextFlightAt = now + Random.Range(minimumGroundSeconds, maximumGroundSeconds);
            RefreshVisual();
        }

        private void RefreshAirVisual(float now)
        {
            if (artwork == null) return;
            if (activity == CrowActivity.GroundIdle || activity == CrowActivity.GroundWalk)
            {
                RefreshVisual();
                return;
            }
            if (activity == CrowActivity.Perched)
            {
                artwork.flipX = false;
                artwork.sprite = idleRight;
                return;
            }
            var horizontal = Mathf.Abs(legEnd.x - legStart.x) >= Mathf.Abs(legEnd.y - legStart.y);
            var frames = horizontal ? flightRight : flightDown;
            artwork.flipX = horizontal && legEnd.x < legStart.x;
            artwork.sprite = frames[Mathf.FloorToInt(now / FlightFrameSeconds) % frames.Length];
        }

        private void Awake()
        {
            groundSortingOrder = artwork != null ? artwork.sortingOrder : 3;
            motion = new GridStepMotion(world.WorldToCell(transform.position));
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = world.CellToWorld(motion.Cell);
            nextActionAt = Time.time + 2f;
            nextFlightAt = Time.time + Random.Range(minimumGroundSeconds, maximumGroundSeconds);
            lastTickAt = Time.time;
            RefreshVisual();
        }

        private void OnDisable()
        {
            targetTree = null;
            flyingTowardGround = false;
            if (clickCollider != null) clickCollider.enabled = false;
        }

        private void Update() => Tick(Time.time);

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
            transform.position = world.CellToWorld(motion.Position);
            activity = motion.IsMoving ? CrowActivity.GroundWalk : CrowActivity.GroundIdle;
            if (wasMoving && !motion.IsMoving)
            {
                if (clickCollider != null) clickCollider.enabled = true;
                nextActionAt = now + Random.Range(2f, 4f);
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
                    !world.CanEnter(target) || !occupancy.CanCrowEnter(target)) continue;
                if (!motion.TryBegin(direction,
                    cell => world.CanEnter(cell) && occupancy.CanCrowEnter(cell),
                    StepSeconds)) continue;
                Facing = FacingDirectionExtensions.FromVector(direction);
                activity = CrowActivity.GroundWalk;
                if (clickCollider != null) clickCollider.enabled = false; // Pointer interaction only while stopped.
                grassField.RustleAt(target);
                break;
            }
        }

        private void RefreshVisual()
        {
            artwork.flipX = false;
            if (!IsMoving)
            {
                artwork.sprite = Facing switch
                {
                    FacingDirection.Up => idleUp,
                    FacingDirection.Left => idleLeft,
                    FacingDirection.Right => idleRight,
                    _ => idleDown
                };
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
            artwork.sprite = frames[frame];
        }
    }
}
