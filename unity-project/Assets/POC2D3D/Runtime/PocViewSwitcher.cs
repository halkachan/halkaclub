using UnityEngine;

namespace Halka.Poc2D3D
{
    public enum PocViewMode { PixelView2D, ThirdPerson3D }

    // Switching only enables one camera path. It never touches SharedWorldSession, so the
    // player cell, facing, map, step count and clock continue unchanged.
    public sealed class PocViewSwitcher : MonoBehaviour
    {
        [SerializeField] private PocPixelView2D pixelView;
        [SerializeField] private Camera presenterCamera;
        [SerializeField] private PocThirdPersonCamera thirdPerson;
        [SerializeField] private PocActorView actor;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocInput input;
        [SerializeField] private PocViewMode initialMode = PocViewMode.PixelView2D;

        private bool applied;

        public PocViewMode Mode { get; private set; }
        public int SwitchCount { get; private set; }
        public float LastSwitchMilliseconds { get; private set; }
        public float CameraTargetYaw => Mode == PocViewMode.ThirdPerson3D && thirdPerson != null
            ? thirdPerson.TargetYaw : 0f;
        public Halka.Game.Player.FacingDirection CameraHeading => thirdPerson != null
            ? thirdPerson.Heading : Halka.Game.Player.FacingDirection.Up;
        public PocPixelView2D PixelView => pixelView;
        public PocThirdPersonCamera ThirdPerson => thirdPerson;

        public void Configure(PocPixelView2D pixel, Camera presenterCam, PocThirdPersonCamera third,
            PocActorView actorView, PocWorldPresenter worldPresenter, SharedWorldSession worldSession,
            PocInput pocInput = null)
        {
            session = worldSession;
            input = pocInput;
            pixelView = pixel;
            presenterCamera = presenterCam;
            thirdPerson = third;
            actor = actorView;
            presenter = worldPresenter;
        }

        private void Start()
        {
            if (!applied) SetMode(initialMode);
        }

        public void Toggle() => SetMode(Mode == PocViewMode.PixelView2D ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D);

        public void SetMode(PocViewMode mode)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            if (applied && mode != Mode) SwitchCount++;
            Mode = mode;
            applied = true;
            var pixel = mode == PocViewMode.PixelView2D;
            pixelView.enabled = pixel;
            pixelView.WorldCamera.enabled = pixel;
            presenterCamera.enabled = pixel;
            thirdPerson.enabled = !pixel;
            thirdPerson.ViewCamera.enabled = !pixel;
            actor.TurnSeconds = pixel ? 0f : 0.09f;
            if (!pixel) actor.PixelSnap = Vector2.zero;
            // Trees made see-through for the 2D sprite are opaque again in 3D.
            if (!pixel) pixelView.RestoreAllFades();
            // Occluders hidden for the 3D camera must never stay hidden in the 2D view.
            if (pixel) thirdPerson.ShowAll();
            if (pixel) pixelView.SnapToTarget();
            else thirdPerson.SnapToTarget();
            if (input != null) input.RequireRelease();
            LastSwitchMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000.0);
        }

        // Shared picking for clicks: a ray from whichever camera is visible, tested against the
        // same Object views. The result is an Object Instance, not a camera-specific object.
        public SharedObjectInstance PickObject(Vector2 screenPosition)
        {
            var ray = Mode == PocViewMode.PixelView2D
                ? pixelView.ScreenPointToRay(screenPosition)
                : thirdPerson.ViewCamera.ScreenPointToRay(screenPosition);
            PocObjectView best = null;
            var bestDistance = float.MaxValue;
            foreach (var view in presenter.Views)
            {
                if (view == null || view.InstanceId == null || view.InstanceId.StartsWith("wall_")) continue;
                if (view.TryRaycast(ray, out var distance) && distance < bestDistance)
                {
                    best = view;
                    bestDistance = distance;
                }
            }
            return best != null ? session.World.FindInstance(best.InstanceId) : null;
        }
    }
}
