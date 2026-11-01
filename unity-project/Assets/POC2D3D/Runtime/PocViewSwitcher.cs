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
            ApplyShadows(pixel);
            if (input != null) input.RequireRelease();
            LastSwitchMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000.0);
        }

        // ver4.0 (the user's look after the second round: the shadows on the bigger house were jagged
        // in 3D): in 3D the sun's shadows are filtered (soft) and reach 24 m from the camera instead of
        // the quality level's 40, so the same shadow map is finer near her (no bigger map: phones'
        // memory). The 2D view keeps hard shadows to the full distance (its camera stands 30 m away,
        // and its pixels want hard edges).
        public const float Shadow3DDistance = 24f;
        private float shadowDistance2D = -1f;
        private Light sun;

        public Light Sun
        {
            get
            {
                if (sun != null) return sun;
                sun = RenderSettings.sun;
                if (sun == null)
                    foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.type == LightType.Directional) { sun = light; break; }
                return sun;
            }
        }

        // The quality level's distance is shared by the whole process (the editor's checks run many
        // games one after another): put it back when this view goes away.
        private void OnDestroy()
        {
            if (shadowDistance2D >= 0f) QualitySettings.shadowDistance = shadowDistance2D;
        }

        private void ApplyShadows(bool pixel)
        {
            if (shadowDistance2D < 0f) shadowDistance2D = QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = pixel ? shadowDistance2D : Mathf.Min(shadowDistance2D, Shadow3DDistance);
            if (Sun != null && Sun.shadows != LightShadows.None) Sun.shadows = pixel ? LightShadows.Hard : LightShadows.Soft;
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
        // ver4.0: Rin, Len or a passer-by under the pointer (its sprite in 2D, its model in 3D).
        public Life.PocWalker PickWalker(Vector2 screenPosition)
        {
            Life.PocWalker best = null;
            var bestDistance = float.MaxValue;
            if (Mode == PocViewMode.PixelView2D)
            {
                if (pixelView == null) return null;
                var point = new Vector2Int(Mathf.FloorToInt(screenPosition.x), Mathf.FloorToInt(screenPosition.y));
                foreach (var pair in pixelView.WalkerRects)
                    if (pair.Key != null && pair.Key.Drawn && pair.Value.width > 0 && pair.Value.Contains(point))
                    {
                        var d = pair.Value.y; // the one further South (lower on screen) is in front
                        if (d < bestDistance) { bestDistance = d; best = pair.Key; }
                    }
                return best;
            }
            if (thirdPerson == null || thirdPerson.ViewCamera == null) return null;
            var ray = thirdPerson.ViewCamera.ScreenPointToRay(screenPosition);
            foreach (var walker in Life.PocWalker.Active)
                if (walker != null && walker.Drawn && walker.RayHits(ray, out var distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = walker;
                }
            return best;
        }

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
