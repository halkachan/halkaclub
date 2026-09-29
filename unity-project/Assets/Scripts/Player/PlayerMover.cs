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
        public Vector2 Facing { get; private set; } = Vector2.down;
        public bool IsMoving => motion != null && motion.IsMoving;

        private void Awake()
        {
            motion = new GridStepMotion(world.WorldToCell(transform.position));
            transform.position = world.CellToWorld(motion.Cell);
        }

        private void Update()
        {
            if (!motion.IsMoving)
            {
                var direction = input.Direction;
                if (direction != Vector2Int.zero)
                {
                    Facing = direction;
                    motion.TryBegin(direction, world.CanEnter, stepSeconds);
                }
            }

            motion.Advance(Time.deltaTime);
            transform.position = world.CellToWorld(motion.Position);
        }
    }
}
