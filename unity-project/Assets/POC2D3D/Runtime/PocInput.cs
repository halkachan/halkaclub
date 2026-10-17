using Halka.Game.Input;
using Halka.Game.Player;
using Halka.Game.UI;
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
        [Header("Phase 4: the game's menu and AUTO")]
        [SerializeField] private GameMenuController menu;
        [SerializeField] private AutoModeController autoMode;
        [SerializeField] private PocGameplay gameplay;

        private Vector2Int latchedRaw;
        private Vector2Int latchedDirection;
        private bool requireRelease;
        // Phase 5: a held key may resume at this input time (RequireRelease with a delay).
        private float resumeHeldAt = float.PositiveInfinity;
        private float clock;
        private bool mouseDown;
        private bool mouseStartedOnControls;
        private Vector2 mouseStart;
        private float lastTouchAt = -10f;

        // Phase 2.1: 3D uses camera-relative keys by default; 2D always stays world-fixed.
        public Poc3DControlScheme Scheme { get; set; } = Poc3DControlScheme.CameraRelative;
        public Vector2Int RawDirection { get; private set; }
        public bool ActionPressed { get; private set; }
        public bool AnyDirectionHeld => RawDirection != Vector2Int.zero;
        public float LastReleaseAt { get; private set; } = float.NegativeInfinity;
        public bool WaitingForRelease => requireRelease;

        public void Configure(TouchDpad touchDpad, TouchActionButton button, PocViewSwitcher viewSwitcher,
            PocDebugPanel panel, SharedWorldSession worldSession)
        {
            dpad = touchDpad;
            actionButton = button;
            views = viewSwitcher;
            debugPanel = panel;
            session = worldSession;
        }

        public void ConfigureGame(GameMenuController gameMenu, AutoModeController auto, PocGameplay sharedGameplay)
        {
            menu = gameMenu;
            autoMode = auto;
            gameplay = sharedGameplay;
        }

        // GameInput's rules: an open menu (or a touch it just used) blocks gameplay input, and any
        // input counts as a user action that stops AUTO and restarts its 60-second idle clock.
        private bool Blocked => menu != null && menu.BlocksGameplayInput || gameplay != null && gameplay.Suspended;

        private void Update()
        {
            if (UnityEngine.Input.touchCount > 0) lastTouchAt = Time.unscaledTime;
            if (Blocked)
            {
                SetRaw(Vector2Int.zero, Time.unscaledTime);
                ActionPressed = false;
                pendingClick = false;
                return;
            }
            SetRaw(CardinalInput.Choose(
                UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow),
                UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow),
                UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow),
                UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow),
                dpad != null ? dpad.Direction : Vector2Int.zero), Time.unscaledTime);
            var justPressed = actionButton != null && actionButton.JustPressed;
            ActionPressed = justPressed && RawDirection == Vector2Int.zero;
            if (autoMode != null && (RawDirection != Vector2Int.zero || justPressed || pendingClick ||
                UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.touchCount > 0))
                autoMode.RecordUserAction(Time.unscaledTime);
            if (pendingClick)
            {
                pendingClick = false;
                Click(pendingClickPosition);
            }
        }

        private bool pendingClick;
        private Vector2 pendingClickPosition;

        // A click: the crow when the ray lands on it, else the picked object (shared rules).
        public void Click(Vector2 screenPosition)
        {
            if (views == null || session == null) return;
            if (views.PickCrow(screenPosition) is ISharedTalkTarget crow && session.TalkTo(crow)) return;
            var target = views.PickObject(screenPosition);
            if (target != null) session.InteractWith(target);
        }

        // Also used by checks to simulate the keyboard or the D-pad.
        public void SetRaw(Vector2Int raw, float now)
        {
            clock = now;
            if (raw == Vector2Int.zero && RawDirection != Vector2Int.zero) LastReleaseAt = now;
            RawDirection = raw;
        }

        // After a view switch a key that is still held is ignored until it is released, so the
        // switch can never turn an old key press into a step in a new direction.
        public void RequireRelease() => RequireRelease(float.PositiveInfinity);

        // Phase 5: the same, but a key still held after resumeAfterSeconds counts again (a new
        // press, latched with the camera heading of that moment). Infinity: wait for a release.
        public void RequireRelease(float resumeAfterSeconds)
        {
            requireRelease = RawDirection != Vector2Int.zero;
            resumeHeldAt = clock + resumeAfterSeconds;
            latchedRaw = latchedDirection = Vector2Int.zero;
        }

        public Vector2Int ResolveDirection(SharedWorldSession worldSession) =>
            Resolve(RawDirection, views != null && views.Mode == PocViewMode.ThirdPerson3D,
                views != null ? views.CameraHeading : Halka.Game.Player.FacingDirection.Up);

        // 3D camera-relative keys: the Grid direction is chosen once when a key goes down, from the
        // camera's cardinal heading, and kept until the key is released (or another key replaces
        // it). A camera that turns while a key is held, or a step in progress, never changes it.
        public Vector2Int Resolve(Vector2Int raw, bool thirdPerson, Halka.Game.Player.FacingDirection heading)
        {
            if (requireRelease)
            {
                if (raw == Vector2Int.zero) requireRelease = false;
                if (raw == Vector2Int.zero || clock < resumeHeldAt) return Vector2Int.zero;
                requireRelease = false;
            }
            if (raw == Vector2Int.zero)
            {
                latchedRaw = latchedDirection = Vector2Int.zero;
                return raw;
            }
            if (!thirdPerson || Scheme == Poc3DControlScheme.WorldFixed) return raw;
            if (raw != latchedRaw)
            {
                latchedRaw = raw;
                latchedDirection = GridSpace3D.CameraRelative(raw, GridSpace3D.FacingYaw(heading));
            }
            return latchedDirection;
        }

        // Phase 1 signature (yaw in degrees), kept for the Phase 1 regression checks.
        public Vector2Int Resolve(Vector2Int raw, bool thirdPerson, float cameraYaw) =>
            Resolve(raw, thirdPerson, GridSpace3D.HeadingFromYaw(cameraYaw));

        private bool IsOverControls(Vector2 screenPosition) =>
            dpad != null && dpad.IsOverControls(screenPosition) ||
            actionButton != null && actionButton.IsOverControls(screenPosition) ||
            debugPanel != null && debugPanel.IsOverControls(screenPosition) ||
            menu != null && menu.IsOverControls(screenPosition);

        private void OnGUI()
        {
            if (Blocked) return;
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
                // Handled in Update, like GameInput's pending click.
                pendingClickPosition = position;
                pendingClick = true;
            }
        }
    }
}
