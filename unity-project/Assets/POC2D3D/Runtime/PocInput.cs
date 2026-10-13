using Halka.Game.Input;
using UnityEngine;

namespace Halka.Poc2D3D
{
    public enum Poc3DControlScheme { WorldFixed, CameraRelative }

    // The existing controls only: WASD/arrows, the existing touch D-pad and A button, and a
    // left click on an object. Nothing is added for 3D; the scheme only changes key meaning.
    [DefaultExecutionOrder(-60)]
    public sealed class PocInput : MonoBehaviour
    {
        [SerializeField] private TouchDpad dpad;
        [SerializeField] private TouchActionButton actionButton;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private PocDebugPanel debugPanel;
        [SerializeField] private SharedWorldSession session;
        [SerializeField, Min(1f)] private float clickMovementLimit = 24f;

        private bool hasLatchedYaw;
        private float latchedYaw;
        private bool mouseDown;
        private bool mouseStartedOnControls;
        private Vector2 mouseStart;
        private float lastTouchAt = -10f;

        public Poc3DControlScheme Scheme { get; set; } = Poc3DControlScheme.WorldFixed;
        public Vector2Int RawDirection { get; private set; }
        public bool ActionPressed { get; private set; }

        public void Configure(TouchDpad touchDpad, TouchActionButton button, PocViewSwitcher viewSwitcher,
            PocDebugPanel panel, SharedWorldSession worldSession)
        {
            dpad = touchDpad;
            actionButton = button;
            views = viewSwitcher;
            debugPanel = panel;
            session = worldSession;
        }

        private void Update()
        {
            if (UnityEngine.Input.touchCount > 0) lastTouchAt = Time.unscaledTime;
            RawDirection = CardinalInput.Choose(
                UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow),
                UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow),
                UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow),
                UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow),
                dpad != null ? dpad.Direction : Vector2Int.zero);
            ActionPressed = actionButton != null && actionButton.JustPressed && RawDirection == Vector2Int.zero;
        }

        // Camera-relative keys use the camera's target yaw latched at the first press, so a
        // camera that turns while a key is held cannot reverse the walking direction.
        public Vector2Int ResolveDirection(SharedWorldSession worldSession) =>
            Resolve(RawDirection, views != null && views.Mode == PocViewMode.ThirdPerson3D,
                views != null ? views.CameraTargetYaw : 0f);

        public Vector2Int Resolve(Vector2Int raw, bool thirdPerson, float cameraYaw)
        {
            if (raw == Vector2Int.zero)
            {
                hasLatchedYaw = false;
                return raw;
            }
            if (!thirdPerson || Scheme == Poc3DControlScheme.WorldFixed) return raw;
            if (!hasLatchedYaw)
            {
                latchedYaw = cameraYaw;
                hasLatchedYaw = true;
            }
            return GridSpace3D.CameraRelative(raw, latchedYaw);
        }

        private bool IsOverControls(Vector2 screenPosition) =>
            dpad != null && dpad.IsOverControls(screenPosition) ||
            actionButton != null && actionButton.IsOverControls(screenPosition) ||
            debugPanel != null && debugPanel.IsOverControls(screenPosition);

        private void OnGUI()
        {
            if (dpad != null && dpad.Visible && !dpad.PreviewMouse) return;
            if (UnityEngine.Input.touchCount > 0 || Time.unscaledTime - lastTouchAt < 0.25f) return;
            var current = Event.current;
            if (current.button != 0) return;
            var position = new Vector2(current.mousePosition.x, Screen.height - current.mousePosition.y);
            if (current.type == EventType.MouseDown)
            {
                mouseDown = true;
                mouseStart = position;
                mouseStartedOnControls = IsOverControls(position);
            }
            else if (current.type == EventType.MouseUp && mouseDown)
            {
                mouseDown = false;
                if (mouseStartedOnControls || IsOverControls(position) ||
                    Vector2.Distance(mouseStart, position) > clickMovementLimit) return;
                var target = views != null ? views.PickObject(position) : null;
                if (target != null && session != null) session.InteractWith(target);
            }
        }
    }
}
