using Halka.Game.Interaction;
using UnityEngine;

namespace Halka.Game.Input
{
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] private InteractionRouter interaction;
        [SerializeField, Range(0.02f, 0.2f)] private float dragStartScreenFraction = 0.035f;
        [SerializeField, Range(2f, 8f)] private float fullSpeedThresholdMultiplier = 4f;
        [SerializeField] private float tapMaxSeconds = 0.4f;

        private readonly TouchGesture gesture = new TouchGesture();
        private Vector2 mouseOrigin;
        private float lastTouchAt = -10f;

        public Vector2 Move { get; private set; }

        private float DragThreshold => Mathf.Clamp(
            Mathf.Min(Screen.width, Screen.height) * dragStartScreenFraction, 14f, 36f);

        private void Update()
        {
            ReadTouches();
            ReadMouse();

            var keyboard = Vector2.zero;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) keyboard.x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) keyboard.x += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) keyboard.y -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) keyboard.y += 1f;
            Move = Vector2.ClampMagnitude(keyboard + gesture.Movement, 1f);
        }

        private void ReadMouse()
        {
            if (UnityEngine.Input.touchCount > 0 || Time.unscaledTime - lastTouchAt < 0.25f) return;
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                mouseOrigin = UnityEngine.Input.mousePosition;
            }
            if (UnityEngine.Input.GetMouseButtonUp(0) &&
                Vector2.Distance(mouseOrigin, UnityEngine.Input.mousePosition) < DragThreshold)
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
                if (touch.phase == TouchPhase.Began && gesture.ActiveFingerId == -1 && UnityEngine.Input.touchCount == 1)
                {
                    gesture.TryBegin(touch.fingerId, touch.position,
                        interaction.IsInteractableAt(touch.position), Time.unscaledTime);
                }
                if (touch.fingerId != gesture.ActiveFingerId) continue;

                if (touch.phase == TouchPhase.Canceled)
                {
                    gesture.Cancel(touch.fingerId);
                    continue;
                }
                if (touch.phase == TouchPhase.Ended)
                {
                    if (gesture.End(touch.fingerId, touch.position, Time.unscaledTime,
                        DragThreshold, tapMaxSeconds)) interaction.TryInteract(touch.position);
                    continue;
                }
                gesture.Move(touch.fingerId, touch.position, DragThreshold, fullSpeedThresholdMultiplier);
            }
        }

        private void OnDisable()
        {
            gesture.Reset();
            Move = Vector2.zero;
        }
    }
}
