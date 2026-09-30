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
        public event Action<Vector2Int> StepStarted;

        private void Awake()
        {
            motion = new GridStepMotion(world.WorldToCell(transform.position));
            transform.position = world.CellToWorld(motion.Cell);
        }

        private void Update()
        {
            if (!motion.IsMoving) ApplyDirection(input.Direction);
            var wasMoving = motion.IsMoving;
            motion.Advance(Time.deltaTime);
            if (wasMoving && !motion.IsMoving) ApplyDirection(input.Direction);
            transform.position = world.CellToWorld(motion.Position);
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
