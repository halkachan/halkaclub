using System;
using Halka.Game.Input;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class PlayerMover : MonoBehaviour
    {
        public const float DefaultStepSeconds = 0.18f;

        [SerializeField] private GameInput input;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private DynamicGridOccupancy2D occupancy;
        [SerializeField] private MapWorldController2D mapController;
        [SerializeField, Min(0.05f)] private float stepSeconds = DefaultStepSeconds;
        [SerializeField] private string initialFacing = "down";

        private GridStepMotion motion;
        private bool areaExitNeedsRelease;

        public Vector2Int Cell => motion != null ? motion.Cell : Vector2Int.zero;
        public FacingDirection Facing { get; private set; } = FacingDirection.Down;
        public bool IsMoving => motion != null && motion.IsMoving;
        public Vector2Int StepFromCell => motion != null ? motion.StepFromCell : Cell;
        public Vector2Int StepToCell => motion != null ? motion.StepToCell : Cell;
        public float StepProgressNormalized => motion != null ? motion.StepProgressNormalized : 1f;
        public event Action<Vector2Int> StepStarted;
        public event Action<Vector2Int> StepCompleted;

        private void Awake()
        {
            motion = new GridStepMotion(world.WorldToCell(transform.position));
            Facing = EntityRuntimeFactory2D.ParseFacing(initialFacing);
            transform.position = world.CellToWorld(motion.Cell);
        }

        private void Update()
        {
            Tick(Time.deltaTime, input.Direction);
        }

        private void Tick(float deltaTime, Vector2Int direction)
        {
            if (direction == Vector2Int.zero) areaExitNeedsRelease = false;
            if (!motion.IsMoving) ApplyDirection(direction);
            var wasMoving = motion.IsMoving;
            motion.Advance(deltaTime);
            transform.position = world.CellToWorld(motion.Position);
            if (wasMoving && !motion.IsMoving)
            {
                var completedMotion = motion;
                StepCompleted?.Invoke(motion.Cell);
                // A cell transition can replace the motion with a new area's cell.
                if (motion != completedMotion) return;
                ApplyDirection(direction);
            }
        }

        public void ApplyDirection(Vector2Int direction) => TryStepInternal(direction, true);

        public bool TryStep(Vector2Int direction) => TryStepInternal(direction, false);

        private bool TryStepInternal(Vector2Int direction, bool manualInput)
        {
            if (motion.IsMoving || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1) return false;
            Facing = FacingDirectionExtensions.FromVector(direction);
            var target = motion.Cell + direction;
            if (manualInput && !areaExitNeedsRelease && mapController != null &&
                (target.x < world.MinCell.x || target.x > world.MaxCell.x ||
                 target.y < world.MinCell.y || target.y > world.MaxCell.y) &&
                mapController.TryExit(motion.Cell, direction))
            { areaExitNeedsRelease = true; return true; }
            if (motion.TryBegin(direction,
                cell => world.CanEnter(cell) && (occupancy == null || occupancy.CanPlayerEnter(cell)),
                stepSeconds))
            {
                StepStarted?.Invoke(target);
                return true;
            }
            return false;
        }

        public void CancelStep()
        {
            if (!IsMoving) return;
            var cell = StepProgressNormalized < 0.5f ? StepFromCell : StepToCell;
            TeleportTo(cell);
        }

        public void TeleportTo(Vector2Int cell)
        {
            motion = new GridStepMotion(cell);
            transform.position = world.CellToWorld(cell);
        }

        public void SetFacing(FacingDirection facing) => Facing = facing;
        public void SetInitialFacing(FacingDirection facing)
        {
            initialFacing = facing.ToString().ToLowerInvariant();
            Facing = facing;
        }
    }
}
