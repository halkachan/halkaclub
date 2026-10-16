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
        FollowFacing        // Phase 1 behaviour: always behind the facing (S reverses the view).
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

        [Header("Outdoor")]
        [SerializeField] private float distance = 2.5f;
        [SerializeField] private float pitch = 26f;
        [Header("Indoor")]
        [SerializeField] private float indoorDistance = 2.9f; // Phase 3: wider room view (walls are cut away)
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
        public PocCameraFollow Follow { get; set; } = PocCameraFollow.TurnBehindWhenIdle;
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
        public int HiddenOccluders { get; private set; }
        public Camera ViewCamera => viewCamera;

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
            ShowAll();
        }

        private void OnMapChanged(SharedWorldModel world) => snapNext = true;
        public void SnapToTarget() => snapNext = true;

        private void LateUpdate() => UpdateView(Time.unscaledDeltaTime, Time.unscaledTime);

        public void UpdateView(float deltaTime) => UpdateView(deltaTime, Time.unscaledTime);

        public void UpdateView(float deltaTime, float now)
        {
            UpdateHeading(now);
            targetYaw = GridSpace3D.FacingYaw(Heading);
            var desired = GridSpace3D.ToWorld(session.Position, lookHeight);
            var justSnapped = snapNext;
            if (snapNext)
            {
                focus = desired;
                focusVelocity = Vector3.zero;
                yaw = targetYaw;
                yawVelocity = 0f;
                snapNext = false;
            }
            else
            {
                focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, positionSmooth, Mathf.Infinity, deltaTime);
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmooth, Mathf.Infinity, deltaTime);
            }
            var indoor = session.World.IsInterior;
            var currentPitch = indoor ? indoorPitch : pitch;
            var currentDistance = indoor ? indoorDistance : distance;
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
        }

        // The heading changes only while nothing is pressed and the player stands still, so a held
        // key always keeps its direction. Facing the camera (after S) never spins the view 180°.
        private void UpdateHeading(float now)
        {
            var facing = session.Facing;
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
