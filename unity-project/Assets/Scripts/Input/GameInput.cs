using Halka.Game.Interaction;
using UnityEngine;

namespace Halka.Game.Input
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] private InteractionRouter interaction;
        [SerializeField] private TouchDpad dpad;
        [SerializeField, Min(1f)] private float tapMovementLimit = 24f;
        [SerializeField, Min(0.05f)] private float tapMaxSeconds = 0.4f;

        private readonly PointerTap worldTap = new PointerTap();
        private Vector2 mouseStart;
        private bool mouseStartedOnDpad;
        private float lastTouchAt = -10f;

        public Vector2Int Direction { get; private set; }

        private void Update()
        {
            ReadTouches();
            ReadMouse();
            Direction = CardinalInput.Choose(
                UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow),
                UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow),
                UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow),
                UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow),
                dpad.Direction);
        }

        private void ReadMouse()
        {
            if (UnityEngine.Input.touchCount > 0 || Time.unscaledTime - lastTouchAt < 0.25f) return;
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                mouseStart = UnityEngine.Input.mousePosition;
                mouseStartedOnDpad = dpad.IsOverControls(mouseStart);
            }
            if (UnityEngine.Input.GetMouseButtonUp(0) && !mouseStartedOnDpad &&
                !dpad.IsOverControls(UnityEngine.Input.mousePosition) &&
                Vector2.Distance(mouseStart, UnityEngine.Input.mousePosition) <= tapMovementLimit)
            {
                interaction.TryInteract(UnityEngine.Input.mousePosition);
            }
        }

        private void ReadTouches()
        {
            if (UnityEngine.Input.touchCount > 0) lastTouchAt = Time.unscaledTime;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    worldTap.TryBegin(touch.fingerId, touch.position,
                        dpad.IsOverControls(touch.position), Time.unscaledTime);
                    continue;
                }
                if (touch.phase == TouchPhase.Canceled)
                    worldTap.Cancel(touch.fingerId);
                else if (touch.phase == TouchPhase.Ended)
                {
                    if (worldTap.End(touch.fingerId, touch.position,
                        dpad.IsOverControls(touch.position), Time.unscaledTime,
                        tapMovementLimit, tapMaxSeconds)) interaction.TryInteract(touch.position);
                }
                else worldTap.Move(touch.fingerId, touch.position, tapMovementLimit);
            }
            if (UnityEngine.Input.touchCount == 0) worldTap.Reset();
        }

        private void OnDisable()
        {
            worldTap.Reset();
            Direction = Vector2Int.zero;
        }
    }
}
