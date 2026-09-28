using System;
using Halka.Game.Input;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class InputGestureChecks
    {
        [MenuItem("HALKA/Validate touch gestures")]
        public static void Run()
        {
            var gesture = new TouchGesture();
            Check(gesture.TryBegin(3, Vector2.zero, false, 0f), "first finger begins");
            Check(gesture.Move(3, new Vector2(10f, 0f), 20f, 4f) == Vector2.zero, "dead zone");
            Check(gesture.End(3, new Vector2(10f, 0f), 0.1f, 20f, 0.4f), "short tap");
            Check(gesture.Movement == Vector2.zero && gesture.ActiveFingerId == -1, "tap stops");

            Check(gesture.TryBegin(4, Vector2.zero, false, 1f), "drag begins");
            Check(!gesture.TryBegin(5, Vector2.zero, false, 1f), "second finger ignored");
            Check(gesture.Move(5, new Vector2(80f, 0f), 20f, 4f) == Vector2.zero, "foreign drag ignored");
            Check(gesture.Move(4, new Vector2(80f, 0f), 20f, 4f) == Vector2.right, "drag reaches full speed");
            Check(!gesture.End(5, Vector2.zero, 1.2f, 20f, 0.4f), "foreign end ignored");
            Check(gesture.ActiveFingerId == 4, "primary finger retained");
            Check(!gesture.End(4, new Vector2(80f, 0f), 1.2f, 20f, 0.4f), "drag not tap");
            Check(gesture.Movement == Vector2.zero, "release stops");

            Check(gesture.TryBegin(6, Vector2.zero, true, 2f), "target touch begins");
            Check(gesture.Move(6, new Vector2(80f, 0f), 20f, 4f) == Vector2.zero,
                "drag on target does not move player");
            Check(!gesture.End(6, new Vector2(80f, 0f), 2.1f, 20f, 0.4f), "target drag not tap");

            Check(gesture.TryBegin(7, Vector2.zero, false, 3f), "new finger begins");
            Check(!gesture.End(7, Vector2.zero, 3.8f, 20f, 0.4f), "long hold not tap");
            Check(gesture.TryBegin(8, Vector2.zero, false, 4f), "cancel begins");
            gesture.Cancel(8);
            Check(gesture.ActiveFingerId == -1 && gesture.Movement == Vector2.zero, "cancel stops");
            Debug.Log("HALKA touch gesture checks passed (tap, drag, pointer ID, target, cancel).");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Touch gesture check failed: " + label);
        }
    }
}
