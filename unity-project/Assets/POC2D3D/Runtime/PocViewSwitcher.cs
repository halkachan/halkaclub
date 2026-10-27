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
        [Header("Phase 4: the crow")]
        [SerializeField] private PocCrow crow;
        [SerializeField] private PocCrowView3D crowView;

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
        public PocCrowView3D CrowView => crowView;

        public void ConfigureCrow(PocCrow sharedCrow, PocCrowView3D crowView3D)
        {
            crow = sharedCrow;
            crowView = crowView3D;
        }

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

        // Phase 6: the saved view, applied on the first frame instead of the scene's default.
        public void SetInitialMode(PocViewMode mode) => initialMode = mode;
        public PocViewMode InitialMode => initialMode;

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
            pixelView.ApplyTuftVisibility(pixel); // Phase 5: the 3D tufts are the 3D view's grass
            presenterCamera.enabled = pixel;
            thirdPerson.enabled = !pixel;
            thirdPerson.ViewCamera.enabled = !pixel;
            actor.TurnSeconds = pixel ? 0f : 0.09f;
            // The 3D crow model is the 3D view's; the 2D view draws the crow's original sprite.
            if (crowView != null) crowView.Visible = !pixel;
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
        // Phase 3: a wall in sight stops the click (nothing behind it is picked); small things
        // use their MinPickSize; and in 3D a click that lands on ﾊﾙｶﾁｬﾝ but on no object means
        // "what she faces" (exactly the A button's target), so a flower hidden behind her body
        // can still be examined. An object on the click ray always wins, as before.
        // Distance, facing and Action Point rules stay in SharedWorldModel.TryInteract.
        public SharedObjectInstance PickObject(Vector2 screenPosition)
        {
            var ray = Mode == PocViewMode.PixelView2D
                ? pixelView.ScreenPointToRay(screenPosition)
                : thirdPerson.ViewCamera.ScreenPointToRay(screenPosition);
            PocObjectView best = null;
            var bestDistance = float.MaxValue;
            var wallDistance = float.MaxValue;
            foreach (var view in presenter.Views)
            {
                if (view == null || view.InstanceId == null) continue;
                if (view.InstanceId.StartsWith("wall_"))
                {
                    if (view.TryRaycast(ray, out var wall) && wall < wallDistance) wallDistance = wall;
                    continue;
                }
                if (view.TryPick(ray, out var distance) && distance < bestDistance)
                {
                    best = view;
                    bestDistance = distance;
                }
            }
            if (best != null && wallDistance < bestDistance) best = null;
            LastPickUsedPlayer = false;
            if (best == null && Mode == PocViewMode.ThirdPerson3D && actor != null &&
                actor.RayHitsBody(ray, out var body) && body < wallDistance)
            {
                LastPickUsedPlayer = true;
                return session.World.FindInteractionAhead(session.Cell, session.Facing, out _);
            }
            return best != null ? session.World.FindInstance(best.InstanceId) : null;
        }

        public bool LastPickUsedPlayer { get; private set; }

        // ver3.4: whether a world point is inside the visible view (the companion crow is only ever
        // put down where this is false).
        public bool IsInView(Vector3 point)
        {
            var camera = Mode == PocViewMode.PixelView2D ? pixelView.WorldCamera : thirdPerson.ViewCamera;
            if (camera == null) return false;
            var v = camera.WorldToViewportPoint(point);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.1f;
        }

        // Phase 4: the crow under the pointer (its 2D sprite as drawn, or its 3D model), unless a
        // standing wall is nearer. Talking keeps ExamineInteractable's range rule (PocCrow.TryTalk).
        public PocCrow PickCrow(Vector2 screenPosition)
        {
            if (crow == null || !crow.isActiveAndEnabled) return null;
            if (Mode == PocViewMode.PixelView2D)
            {
                var rect = pixelView.LastCrowRect;
                return rect.width > 0 && rect.Contains(new Vector2Int(Mathf.FloorToInt(screenPosition.x),
                    Mathf.FloorToInt(screenPosition.y))) ? crow : null;
            }
            if (crowView == null) return null;
            var ray = thirdPerson.ViewCamera.ScreenPointToRay(screenPosition);
            return crowView.RayHits(ray, out _) ? crow : null;
        }
    }
}
