using UnityEngine;

namespace Halka.Game.Player
{
    public enum FacingDirection { Down, Up, Left, Right }

    public static class FacingDirectionExtensions
    {
        public static Vector2Int ToVector(this FacingDirection facing) => facing switch
        {
            FacingDirection.Up => Vector2Int.up,
            FacingDirection.Left => Vector2Int.left,
            FacingDirection.Right => Vector2Int.right,
            _ => Vector2Int.down
        };

        public static FacingDirection FromVector(Vector2Int direction)
        {
            if (direction == Vector2Int.up) return FacingDirection.Up;
            if (direction == Vector2Int.down) return FacingDirection.Down;
            if (direction == Vector2Int.left) return FacingDirection.Left;
            if (direction == Vector2Int.right) return FacingDirection.Right;
            throw new System.ArgumentException("Facing must be cardinal", nameof(direction));
        }
    }
}
