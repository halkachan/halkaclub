using UnityEngine;

namespace Halka.Poc2D3D
{
    public enum PocIndoorCamera { CloserOffset, CloserOffsetAndClamp }

    // When the camera swings behind the player (Phase 2.1). The heading is the cardinal
    // direction camera-relative keys are measured from; the player's facing is separate.
    public enum PocCameraFollow
    {
        TurnBehindWhenIdle, // After a sideways turn, once the keys are released and the player stops.
        Fixed,              // Never turns on its own (only when entering 3D or another map).
        FollowFacing,       // Phase 1 behaviour: always behind the facing (S reverses the view).
        NorthFixed          // Phase 4 (user feedback, default): always from the South looking North, as
                            // in 2D; turning or walking never changes the view, W is always North.
    }

    // Phase 4: a temporary camera composition (focus, yaw, pitch, distance) and the cardinal
    // heading that camera-relative keys use while it is held.
    public struct PocCameraPose
    {
        public Vector3 Focus;
        public float Yaw;
        public float Pitch;
        public float Distance;
        public Halka.Game.Player.FacingDirection Heading;
    }

    // Third-person view from behind the player. It follows the interpolated step position
    // (not the cell), so a one-cell step never makes the camera jump.
    [DefaultExecutionOrder(60)]
    public sealed class PocThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private PocInput input;
        [SerializeField, Min(0f)] private float idleTurnDelay = 0.35f;

        // Phase 4 (user feedback): the camera stands a little farther back than in Phase 3
        // (outdoor 2.5, indoor 2.9), so she is smaller and more of the world shows; same angles.
        public const float StandardOutdoorDistance = 3.4f;
        public const float StandardIndoorDistance = 3.6f;

        [Header("Outdoor")]
        [SerializeField] private float distance = StandardOutdoorDistance;
        [SerializeField] private float pitch = 26f;
        [Header("Indoor")]
        [SerializeField] private float indoorDistance = StandardIndoorDistance; // walls are cut away
        [SerializeField] private float indoorPitch = 52f;
        [Header("Smoothing")]
        [SerializeField] private float positionSmooth = 0.12f;
        [SerializeField] private float yawSmooth = 0.3f;
        [SerializeField] private float lookHeight = 0.5f;

        private Vector3 focus;
        private Vector3 focusVelocity;
        private float yaw;
        private float yawVelocity;
        private float targetYaw;
        private float pitchNow;
        private float pitchVelocity;
        private float boomNow;
        private float boomVelocity;
        private bool snapNext = true;

        public Halka.Game.Player.FacingDirection Heading { get; private set; } = Halka.Game.Player.FacingDirection.Up;
        public PocCameraFollow Follow { get; set; } = PocCameraFollow.NorthFixed;
        public float IdleTurnDelay { get => idleTurnDelay; set => idleTurnDelay = value; }
        public float TargetYaw => targetYaw;
        public float CurrentYaw => yaw;
        public bool HideOccluders { get; set; } = true;
        // Phase 3 interiors: the wall sides that face the camera are cut down to their base
        // (a dollhouse view), so the room, the furniture and the player stay in sight.
        public bool WallCutaway { get; set; } = true;
        public const float CutawayFacing = 0.3f; // cos 72 deg: a side counts as facing the camera
        public const float ChestHeight = 0.45f;
        public int CutWalls { get; private set; }
        public int HiddenParts { get; private set; }
        public PocIndoorCamera IndoorMode { get; set; } = PocIndoorCamera.CloserOffset;
        // The camera's distance behind the player (outdoors / indoors); the angle stays the same.
        public float OutdoorDistance { get => distance; set => distance = value; }
        public float IndoorDistance { get => indoorDistance; set => indoorDistance = value; }
        public int HiddenOccluders { get; private set; }
        public Camera ViewCamera => viewCamera;

        // Phase 4: right after leaving a building in 3D, the camera looks at its front (the
        // player and the building together) until the next movement input; then the normal
        // camera takes over with the same heading, so no key changes meaning and nothing turns.
        public bool ExitComposition { get; set; } = true;
        // Phase 5: a key still held from walking out (for example S through the door) walks on
        // after the house has been shown this long, when the keys mean the same as before the
        // exit (the North-fixed camera and a composition looking North). Otherwise, or with 0 /
        // infinity, the Phase 4 rule: the key is ignored until released.
        public const float StandardExitHeldKeyResumeSeconds = 0.6f;
        public float ExitHeldKeyResumeSeconds { get; set; } = StandardExitHeldKeyResumeSeconds;
        public bool ExitHeldKeyResumes(PocCameraPose pose) =>
            ExitHeldKeyResumeSeconds > 0f && !float.IsInfinity(ExitHeldKeyResumeSeconds) &&
            Follow == PocCameraFollow.NorthFixed && pose.Heading == Halka.Game.Player.FacingDirection.Up;
        public bool CompositionHeld { get; private set; }
        public PocCameraPose HeldPose { get; private set; }
        public int CompositionsShown { get; private set; }
        public float CompositionReleasedAt { get; private set; } = float.NegativeInfinity;
        private bool pendingExitComposition;
        // The heading a composition sets for the map-change snap, even if a held key releases
        // the composition in the same frame (the camera then stays on the building's side).
        private Halka.Game.Player.FacingDirection? snapHeading;

        // Phase 4: trees that hide the player fade to 40% instead of being hidden. The scene keeps
        // the fade material; the fader itself is made from it on first use (also in a build).
        [SerializeField] private Material treeFadeMaterial;
        private PocTreeFade3D treeFade;

        public PocTreeFade3D TreeFade =>
            treeFade ??= treeFadeMaterial != null ? new PocTreeFade3D(treeFadeMaterial) : null;

        public void ConfigureTreeFade(Material fadeMaterial)
        {
            treeFade?.RestoreAll();
            treeFade = null;
            treeFadeMaterial = fadeMaterial;
        }

        public void Configure(Camera cameraComponent, SharedWorldSession worldSession, PocWorldPresenter worldPresenter,
            PocInput pocInput = null)
        {
            input = pocInput;
            viewCamera = cameraComponent;
            session = worldSession;
            presenter = worldPresenter;
        }

        private void OnEnable()
        {
            snapNext = true;
            if (viewCamera != null) viewCamera.enabled = true;
            if (session != null) session.MapChanged += OnMapChanged;
        }

        private void OnDisable()
        {
            if (viewCamera != null) viewCamera.enabled = false;
            if (session != null) session.MapChanged -= OnMapChanged;
            CompositionHeld = false;
            pendingExitComposition = false;
            ShowAll();
        }

        private void OnMapChanged(SharedWorldModel world)
        {
            snapNext = true;
            TreeFade?.RestoreAll();
            CompositionHeld = false;
            // Only the 3D view (this component enabled) frames the building; 2D is untouched.
            pendingExitComposition = enabled && ExitComposition && session != null &&
                session.LastTransition.Kind == SharedTransitionKind.ExitedBuilding;
        }

        public void HoldComposition(PocCameraPose pose)
        {
            HeldPose = pose;
            CompositionHeld = true;
            Heading = pose.Heading;
            snapHeading = pose.Heading;
            CompositionsShown++;
            // A key still held from walking out is ignored until released, so the composition
            // waits for the next movement input (and that input uses this heading). Phase 5: with
            // unchanged key meaning it resumes after ExitHeldKeyResumeSeconds instead.
            if (input != null)
            {
                if (ExitHeldKeyResumes(pose)) input.RequireRelease(ExitHeldKeyResumeSeconds);
                else input.RequireRelease();
            }
        }

        public void ReleaseComposition(float now)
        {
            if (!CompositionHeld) return;
            CompositionHeld = false;
            CompositionReleasedAt = now;
        }
        public void SnapToTarget() => snapNext = true;

        private void LateUpdate() => UpdateView(Time.unscaledDeltaTime, Time.unscaledTime);

        public void UpdateView(float deltaTime) => UpdateView(deltaTime, Time.unscaledTime);

        public void UpdateView(float deltaTime, float now)
        {
            if (pendingExitComposition)
            {
                pendingExitComposition = false;
                if (PocExitComposition.TryCompose(session, presenter, viewCamera, out var pose)) HoldComposition(pose);
            }
            // The first movement (a held key, or any step such as AUTO's) ends the composition;
            // the step's direction was already chosen from the composition's heading.
            if (CompositionHeld && (input != null && input.AnyDirectionHeld && !input.WaitingForRelease ||
                                    session.IsMoving || session.IsSeated))
                ReleaseComposition(now);
            UpdateHeading(now);
            targetYaw = CompositionHeld ? HeldPose.Yaw : GridSpace3D.FacingYaw(Heading);
            var desired = CompositionHeld ? HeldPose.Focus : GridSpace3D.ToWorld(session.Position, lookHeight);
            var justSnapped = snapNext;
            if (snapNext)
            {
                focus = desired;
                focusVelocity = Vector3.zero;
                yaw = targetYaw;
                yawVelocity = 0f;
                snapNext = false;
                snapHeading = null;
            }
            else
            {
                focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, positionSmooth, Mathf.Infinity, deltaTime);
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmooth, Mathf.Infinity, deltaTime);
            }
            var indoor = session.World.IsInterior;
            var currentPitch = CompositionHeld ? HeldPose.Pitch : indoor ? indoorPitch : pitch;
            var currentDistance = CompositionHeld ? HeldPose.Distance : indoor ? indoorDistance : distance;
            if (indoor && IndoorMode == PocIndoorCamera.CloserOffsetAndClamp)
                ClampInsideRoom(focus, yaw, ref currentPitch, ref currentDistance);
            if (justSnapped || deltaTime <= 0f)
            {
                pitchNow = currentPitch;
                boomNow = currentDistance;
                pitchVelocity = boomVelocity = 0f;
            }
            else
            {
                pitchNow = Mathf.SmoothDamp(pitchNow, currentPitch, ref pitchVelocity, yawSmooth, Mathf.Infinity, deltaTime);
                boomNow = Mathf.SmoothDamp(boomNow, currentDistance, ref boomVelocity, yawSmooth, Mathf.Infinity, deltaTime);
            }
            var rotation = Quaternion.Euler(pitchNow, yaw, 0f);
            viewCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * boomNow, rotation);
            viewCamera.fieldOfView = viewCamera.aspect < 1f ? 58f : 42f;
            UpdateOccluders();
            if (TreeFade != null && presenter != null)
                TreeFade.Update(presenter.Views, viewCamera.transform.position, GridSpace3D.ToWorld(session.Position),
                    Quaternion.Euler(0f, yaw, 0f), deltaTime);
        }

        // The heading changes only while nothing is pressed and the player stands still, so a held
        // key always keeps its direction. Facing the camera (after S) never spins the view 180°.
        private void UpdateHeading(float now)
        {
            var facing = session.Facing;
            if (CompositionHeld) return;
            if (Follow == PocCameraFollow.NorthFixed) { Heading = Halka.Game.Player.FacingDirection.Up; return; }
            if (snapNext && snapHeading.HasValue) { Heading = snapHeading.Value; return; }
            if (snapNext || Follow == PocCameraFollow.FollowFacing) { Heading = facing; return; }
            if (Follow == PocCameraFollow.Fixed || session.IsMoving) return;
            if (input != null && (input.AnyDirectionHeld || now - input.LastReleaseAt < idleTurnDelay)) return;
            if (facing != Heading && facing != GridSpace3D.Opposite(Heading)) Heading = facing;
        }

        // Keeps the camera above the room floor instead of behind a wall: first look down more
        // steeply (the camera rises), then shorten the boom if a wall is still too close.
        private void ClampInsideRoom(Vector3 target, float cameraYaw, ref float cameraPitch, ref float boom)
        {
            var world = session.World;
            var half = GridSpace3D.CellSize * 0.5f;
            var minX = (world.MinCell.x + 1) * GridSpace3D.CellSize - half;
            var maxX = (world.MaxCell.x - 1) * GridSpace3D.CellSize + half;
            var minZ = (world.MinCell.y + 1) * GridSpace3D.CellSize - half;
            var maxZ = (world.MaxCell.y - 1) * GridSpace3D.CellSize + half;
            bool Inside(float p, float d)
            {
                var c = target - Quaternion.Euler(p, cameraYaw, 0f) * Vector3.forward * d;
                return c.x >= minX && c.x <= maxX && c.z >= minZ && c.z <= maxZ;
            }
            for (var p = cameraPitch; p <= 80f; p += 4f)
                if (Inside(p, boom)) { cameraPitch = p; return; }
            cameraPitch = 80f;
            while (boom > 0.4f && !Inside(cameraPitch, boom)) boom -= 0.05f;
        }

        private void UpdateOccluders()
        {
            HiddenOccluders = 0;
            CutWalls = 0;
            HiddenParts = 0;
            if (presenter == null) return;
            var cameraPosition = viewCamera.transform.position;
            var player = GridSpace3D.ToWorld(session.Position);
            var forward = viewCamera.transform.forward;
            var towardCamera = -new Vector2(forward.x, forward.z).normalized;
            var interior = session.World.IsInterior;
            foreach (var view in presenter.Views)
            {
                if (view == null) continue;
                if (view.IsWall)
                {
                    // Cutting away is a way of hiding occluders: off when hiding is off.
                    var cut = HideOccluders && WallCutaway && interior &&
                              Vector2.Dot(view.WallOutward, towardCamera) > CutawayFacing;
                    view.SetCutaway(cut);
                    if (cut)
                    {
                        CutWalls++;
                        HiddenOccluders++;
                        continue;
                    }
                }
                if (!view.CameraOccluder) continue;
                if (view.PartialOcclusion)
                {
                    // Only the parts that hide the player's body go: the roof alone when it is in
                    // the way, or the whole body (walls with the door, and then the roof too, so
                    // nothing floats). Standing in the entrance alcove hides nothing.
                    var target = player + Vector3.up * ChestHeight;
                    var hideBody = false;
                    var hideRoof = false;
                    foreach (var part in view.Renderers)
                    {
                        if (!HideOccluders || part == null || !BlocksPart(view, part, cameraPosition, target)) continue;
                        if (IsRoof(part)) hideRoof = true;
                        else hideBody = true;
                    }
                    hideRoof |= hideBody;
                    foreach (var part in view.Renderers)
                    {
                        var hidePart = IsRoof(part) ? hideRoof : hideBody;
                        view.SetRendererHidden(part, hidePart);
                        if (hidePart) HiddenParts++;
                    }
                    continue;
                }
                var hide = HideOccluders && (Blocks(view, cameraPosition, player + Vector3.up * 0.75f) ||
                    Blocks(view, cameraPosition, player + Vector3.up * 0.2f));
                view.SetHidden(hide);
                if (hide) HiddenOccluders++;
            }
        }

        // Blender part names: "HouseRoof" is the roof; every other part belongs to the body.
        public static bool IsRoof(Renderer part) => part != null && part.name.Contains("Roof");

        private static bool BlocksPart(PocObjectView view, Renderer part, Vector3 from, Vector3 to)
        {
            var direction = to - from;
            var length = direction.magnitude;
            var ray = new Ray(from, direction / length);
            return view.RaycastPart(part, ray, out var hit) && hit < length - 0.15f;
        }

        private static bool Blocks(PocObjectView view, Vector3 from, Vector3 to)
        {
            var direction = to - from;
            var length = direction.magnitude;
            var ray = new Ray(from, direction / length);
            return view.TryRaycast(ray, out var hit) && hit < length - 0.15f;
        }

        public void ShowAll()
        {
            TreeFade?.RestoreAll();
            if (presenter == null) return;
            foreach (var view in presenter.Views)
                if (view != null)
                {
                    view.SetHidden(false);
                    view.SetCutaway(false);
                }
            HiddenOccluders = 0;
            CutWalls = 0;
            HiddenParts = 0;
        }
    }
}
