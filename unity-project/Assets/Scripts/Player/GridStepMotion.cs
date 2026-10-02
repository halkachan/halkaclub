using System;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class GridStepMotion
    {
        private Vector2Int target;
        private float elapsed;
        private float duration;

        public Vector2Int Cell { get; private set; }
        public bool IsMoving { get; private set; }
        public Vector2Int StepFromCell => Cell;
        public Vector2Int StepToCell => IsMoving ? target : Cell;
        public float StepProgressNormalized => IsMoving ? elapsed / duration : 1f;
        public Vector2 Position => IsMoving
            ? Vector2.Lerp(Cell, target, StepProgressNormalized)
            : Cell;

        public GridStepMotion(Vector2Int startingCell)
        {
            Cell = startingCell;
        }

        public bool TryBegin(Vector2Int direction, Func<Vector2Int, bool> canEnter, float stepSeconds)
        {
            if (IsMoving || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1) return false;
            var next = Cell + direction;
            if (!canEnter(next)) return false;
            target = next;
            duration = Mathf.Max(0.01f, stepSeconds);
            elapsed = 0f;
            IsMoving = true;
            return true;
        }

        public void Advance(float deltaTime)
        {
            if (!IsMoving) return;
            elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, deltaTime));
            if (elapsed < duration) return;
            Cell = target;
            IsMoving = false;
        }
    }
}
