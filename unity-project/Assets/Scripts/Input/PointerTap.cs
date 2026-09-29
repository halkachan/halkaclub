using UnityEngine;

namespace Halka.Game.Input
{
    public sealed class PointerTap
    {
        private Vector2 origin;
        private float startedAt;
        private bool moved;

        public int ActiveFingerId { get; private set; } = -1;

        public bool TryBegin(int fingerId, Vector2 position, bool overControls, float now)
        {
            if (ActiveFingerId != -1 || overControls) return false;
            ActiveFingerId = fingerId;
            origin = position;
            startedAt = now;
            moved = false;
            return true;
        }

        public void Move(int fingerId, Vector2 position, float movementLimit)
        {
            if (fingerId == ActiveFingerId && Vector2.Distance(origin, position) > movementLimit)
                moved = true;
        }

        public bool End(int fingerId, Vector2 position, bool overControls, float now,
            float movementLimit, float maxSeconds)
        {
            if (fingerId != ActiveFingerId) return false;
            var tapped = !moved && !overControls &&
                Vector2.Distance(origin, position) <= movementLimit && now - startedAt <= maxSeconds;
            Reset();
            return tapped;
        }

        public void Cancel(int fingerId)
        {
            if (fingerId == ActiveFingerId) Reset();
        }

        public void Reset()
        {
            ActiveFingerId = -1;
            moved = false;
        }
    }
}
