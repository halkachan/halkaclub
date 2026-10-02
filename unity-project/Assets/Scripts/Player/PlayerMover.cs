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
        [SerializeField, Min(0.05f)] private float stepSeconds = DefaultStepSeconds;

        private GridStepMotion motion;

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
            transform.position = world.CellToWorld(motion.Cell);
        }

        private void Update()
        {
            Tick(Time.deltaTime, input.Direction);
        }

        private void Tick(float deltaTime, Vector2Int direction)
        {
            if (!motion.IsMoving) ApplyDirection(direction);
            var wasMoving = motion.IsMoving;
            motion.Advance(deltaTime);
            transform.position = world.CellToWorld(motion.Position);
            if (wasMoving && !motion.IsMoving)
            {
                StepCompleted?.Invoke(motion.Cell);
                ApplyDirection(direction);
            }
        }

        public void ApplyDirection(Vector2Int direction)
        {
            if (motion.IsMoving || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1) return;
            Facing = FacingDirectionExtensions.FromVector(direction);
            var target = motion.Cell + direction;
            if (motion.TryBegin(direction, world.CanEnter, stepSeconds))
                StepStarted?.Invoke(target);
        }
    }
}
