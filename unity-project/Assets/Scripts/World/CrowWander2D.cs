using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    // A small walker: one interpolated grid step, then a few seconds of rest.
    public sealed class CrowWander2D : MonoBehaviour, Halka.Game.Interaction.IInteractionAvailability
    {
        public const float StepSeconds = 0.27f;
        public const float WalkFrameSeconds = 0.12f;

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
        [SerializeField] private Vector2Int wanderMinimumOffset;
        [SerializeField] private Vector2Int wanderMaximumOffset;
        [SerializeField] private Vector2Int spawnCell;
        [SerializeField] private string initialFacing = "right";

        private GridStepMotion motion;
        private float nextActionAt;
        private float lastTickAt;

        public Vector2Int Cell => motion != null ? motion.Cell : world.WorldToCell(transform.position);
        public Vector2Int StepFromCell => motion != null ? motion.StepFromCell : Cell;
        public Vector2Int StepToCell => motion != null ? motion.StepToCell : Cell;
        public bool IsMoving => motion != null && motion.IsMoving;
        public bool CanInteract => !IsMoving;
        public float StepProgressNormalized => motion != null ? motion.StepProgressNormalized : 1f;
        public FacingDirection Facing { get; private set; } = FacingDirection.Right;
        public Vector2Int RangeMin => spawnCell + wanderMinimumOffset;
        public Vector2Int RangeMax => spawnCell + wanderMaximumOffset;

        public void SetGrassField(GrassField2D field) => grassField = field;

        public void ApplySpawn(EntitySpawnPlacement spawn, GridWorld2D grid)
        {
            if (spawn.Definition == null || spawn.Definition.RuntimeBehavior != "crow-wander")
                throw new System.InvalidOperationException("Expected a crow-wander Entity Spawn");
            world = grid;
            spawnCell = spawn.Cell;
            wanderMinimumOffset = spawn.Definition.WanderMinimum;
            wanderMaximumOffset = spawn.Definition.WanderMaximum;
            initialFacing = spawn.Facing;
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = world.CellToWorld(spawnCell);
            if (motion != null) motion = new GridStepMotion(spawnCell);
            nextActionAt = Time.time + 2f;
            lastTickAt = Time.time;
            if (clickCollider != null) clickCollider.enabled = true;
            if (artwork != null) RefreshVisual();
        }

        private void Awake()
        {
            motion = new GridStepMotion(world.WorldToCell(transform.position));
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = world.CellToWorld(motion.Cell);
            nextActionAt = Time.time + 2f;
            lastTickAt = Time.time;
            RefreshVisual();
        }

        private void Update() => Tick(Time.time);

        public void Tick(float now)
        {
            if (motion == null) return;
            var deltaTime = Mathf.Max(0f, now - lastTickAt);
            lastTickAt = now;
            var wasMoving = motion.IsMoving;
            motion.Advance(deltaTime);
            transform.position = world.CellToWorld(motion.Position);
            if (wasMoving && !motion.IsMoving)
            {
                clickCollider.enabled = true;
                nextActionAt = now + Random.Range(2f, 4f);
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
                clickCollider.enabled = false; // Pointer interaction only while stopped.
                grassField.RustleAt(target);
                break;
            }
        }

        private void RefreshVisual()
        {
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
