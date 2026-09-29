using UnityEngine;

namespace Halka.Game.Input
{
    public static class CardinalInput
    {
        public static Vector2Int Choose(bool up, bool down, bool left, bool right, Vector2Int touch)
        {
            if (Mathf.Abs(touch.x) + Mathf.Abs(touch.y) == 1) return touch;
            if (up != down) return up ? Vector2Int.up : Vector2Int.down;
            if (left != right) return left ? Vector2Int.left : Vector2Int.right;
            return Vector2Int.zero;
        }
    }
}
