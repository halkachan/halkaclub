using UnityEngine;

namespace Halka.Game.Input
{
    public sealed class TouchGesture
    {
        private Vector2 origin;
        private float beganAt;
        private bool beganOnTarget;
        private bool dragged;

        public int ActiveFingerId { get; private set; } = -1;
        public Vector2 Movement { get; private set; }

        public bool TryBegin(int fingerId, Vector2 position, bool onTarget, float now)
        {
            if (ActiveFingerId != -1) return false;
            ActiveFingerId = fingerId;
            origin = position;
            beganAt = now;
            beganOnTarget = onTarget;
            dragged = false;
            Movement = Vector2.zero;
            return true;
        }

        public Vector2 Move(int fingerId, Vector2 position, float threshold, float fullSpeedMultiplier)
        {
            if (fingerId != ActiveFingerId) return Movement;
            var displacement = position - origin;
            if (displacement.magnitude >= threshold) dragged = true;
            Movement = dragged && !beganOnTarget
                ? Vector2.ClampMagnitude(displacement / (threshold * fullSpeedMultiplier), 1f)
                : Vector2.zero;
            return Movement;
        }

        public bool End(int fingerId, Vector2 position, float now, float threshold, float tapMaxSeconds)
        {
            if (fingerId != ActiveFingerId) return false;
            var tapped = !dragged && Vector2.Distance(origin, position) < threshold &&
                         now - beganAt <= tapMaxSeconds;
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
            Movement = Vector2.zero;
            dragged = false;
            beganOnTarget = false;
        }
    }
}
