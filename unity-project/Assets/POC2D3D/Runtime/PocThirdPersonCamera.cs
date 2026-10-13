using UnityEngine;

namespace Halka.Poc2D3D
{
    public enum PocIndoorCamera { CloserOffset, CloserOffsetAndClamp }

    // Third-person view from behind the player. It follows the interpolated step position
    // (not the cell), so a one-cell step never makes the camera jump.
    [DefaultExecutionOrder(60)]
    public sealed class PocThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocWorldPresenter presenter;

        [Header("Outdoor")]
        [SerializeField] private float distance = 2.5f;
        [SerializeField] private float pitch = 26f;
        [Header("Indoor")]
        [SerializeField] private float indoorDistance = 2.2f;
        [SerializeField] private float indoorPitch = 50f;
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

        public float TargetYaw => targetYaw;
        public float CurrentYaw => yaw;
        public bool HideOccluders { get; set; } = true;
        public PocIndoorCamera IndoorMode { get; set; } = PocIndoorCamera.CloserOffset;
        public int HiddenOccluders { get; private set; }
        public Camera ViewCamera => viewCamera;

        public void Configure(Camera cameraComponent, SharedWorldSession worldSession, PocWorldPresenter worldPresenter)
        {
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

        private void LateUpdate() => UpdateView(Time.unscaledDeltaTime);

        public void UpdateView(float deltaTime)
        {
            targetYaw = GridSpace3D.FacingYaw(session.Facing);
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
            if (presenter == null) return;
            var cameraPosition = viewCamera.transform.position;
            var player = GridSpace3D.ToWorld(session.Position);
            foreach (var view in presenter.Views)
            {
                if (view == null || !view.CameraOccluder) continue;
                var hide = HideOccluders && (Blocks(view, cameraPosition, player + Vector3.up * 0.75f) ||
                    Blocks(view, cameraPosition, player + Vector3.up * 0.2f));
                view.SetHidden(hide);
                if (hide) HiddenOccluders++;
            }
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
                if (view != null) view.SetHidden(false);
            HiddenOccluders = 0;
        }
    }
}
