using System;
using Halka.Game.Interaction;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.Input
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameInput : MonoBehaviour
    {
        [SerializeField] private InteractionRouter interaction;
        [SerializeField] private TouchDpad dpad;
        [SerializeField] private TouchActionButton actionButton;
        [SerializeField] private AutoModeController autoMode;
        [SerializeField, Min(1f)] private float clickMovementLimit = 24f;

        private Vector2 mouseStart;
        private bool mouseDown;
        private bool mouseStartedOnControls;
        private bool pendingClick;
        private Vector2 pendingClickPosition;
        private float lastTouchAt = -10f;

        public Vector2Int Direction { get; private set; }
        public event Action UserActed;

        private void Update()
        {
            if (UnityEngine.Input.touchCount > 0) lastTouchAt = Time.unscaledTime;
            Direction = CardinalInput.Choose(
                UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow),
                UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow),
                UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow),
                UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow),
                dpad.Direction);
            if (Direction != Vector2Int.zero || actionButton.JustPressed || pendingClick ||
                UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.touchCount > 0)
                UserActed?.Invoke();
            if (actionButton.JustPressed && Direction == Vector2Int.zero)
                interaction.TryInteractAhead();
            if (pendingClick)
            {
                interaction.TryInteract(pendingClickPosition);
                pendingClick = false;
            }
        }

        private bool IsOverControls(Vector2 position) =>
            dpad.IsOverControls(position) || actionButton.IsOverControls(position) ||
            autoMode != null && autoMode.IsOverControls(position);

        private void OnGUI()
        {
            if (dpad.Visible && !dpad.PreviewMouse) return;
            if (UnityEngine.Input.touchCount > 0 || Time.unscaledTime - lastTouchAt < 0.25f) return;
            var current = Event.current;
            if (current.button == 0 && current.type == EventType.MouseDown)
            {
                mouseDown = true;
                mouseStart = new Vector2(current.mousePosition.x,
                    Screen.height - current.mousePosition.y);
                mouseStartedOnControls = IsOverControls(mouseStart);
            }
            if (current.button == 0 && current.type == EventType.MouseUp && mouseDown)
            {
                mouseDown = false;
                var position = new Vector2(current.mousePosition.x,
                    Screen.height - current.mousePosition.y);
                if (!mouseStartedOnControls && !IsOverControls(position) &&
                    Vector2.Distance(mouseStart, position) <= clickMovementLimit)
                {
                    pendingClickPosition = position;
                    pendingClick = true;
                }
            }
        }

        private void OnDisable()
        {
            Direction = Vector2Int.zero;
            mouseDown = false;
            pendingClick = false;
        }
    }
}
