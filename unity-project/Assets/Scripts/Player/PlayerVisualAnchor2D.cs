using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class PlayerVisualAnchor2D : MonoBehaviour
    {
        [SerializeField] private PlayerMover mover;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private Transform artwork;
        [SerializeField, Range(0f, 0.4f)] private float approachTileFraction = 0.3f;
        [SerializeField, Min(0.01f)] private float settleSeconds = 0.1f;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private void LateUpdate()
        {
            var desired = Vector3.zero;
            if (!mover.IsMoving)
            {
                var cell = mover.Cell;
                var facing = Vector2Int.RoundToInt(mover.Facing);
                if (IsCardinal(facing) && world.HasObstacle(cell + facing))
                    desired = (Vector3)(Vector2)facing * (world.CellSize * approachTileFraction);
                else
                {
                    foreach (var direction in Directions)
                    {
                        if (!world.HasObstacle(cell + direction)) continue;
                        desired = (Vector3)(Vector2)direction * (world.CellSize * approachTileFraction);
                        break;
                    }
                }
            }

            artwork.localPosition = Vector3.MoveTowards(artwork.localPosition, desired,
                world.CellSize * approachTileFraction * Time.deltaTime / settleSeconds);
        }

        private static bool IsCardinal(Vector2Int value) =>
            Mathf.Abs(value.x) + Mathf.Abs(value.y) == 1;
    }
}
