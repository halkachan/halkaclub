using UnityEngine;

namespace Halka.Poc2D3D
{
    // Draws the shared Player state. It reads the session every frame and never writes to it.
    [DefaultExecutionOrder(50)]
    public sealed class PocActorView : MonoBehaviour
    {
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private Transform model;
        [SerializeField, Min(0f)] private float bobHeight = 0.025f;

        private float yaw;
        private float yawVelocity;
        private bool initialized;

        // 0 turns instantly like the 2D sprites; the 3D view uses a short turn.
        public float TurnSeconds { get; set; }
        // Set by the 2D Pixel View (x: East step, y: North step) so the actor lands on whole
        // low-resolution pixels together with the camera. Zero disables snapping.
        public Vector2 PixelSnap { get; set; }
        public float Yaw => yaw;

        public void Configure(SharedWorldSession worldSession, Transform modelTransform)
        {
            session = worldSession;
            model = modelTransform;
        }

        private void LateUpdate() => Apply(Time.deltaTime);

        public void Apply(float deltaTime)
        {
            var position = GridSpace3D.ToWorld(session.Position);
            if (PixelSnap.x > 0f && PixelSnap.y > 0f)
            {
                position.x = Mathf.Round(position.x / PixelSnap.x) * PixelSnap.x;
                position.z = Mathf.Round(position.z / PixelSnap.y) * PixelSnap.y;
            }
            transform.position = position;
            var target = GridSpace3D.FacingYaw(session.Facing);
            if (!initialized || TurnSeconds <= 0f || deltaTime <= 0f)
            {
                yaw = target;
                yawVelocity = 0f;
                initialized = true;
            }
            else yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVelocity, TurnSeconds);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (model == null) return;
            var step = session.IsMoving ? Mathf.Repeat(session.Position.x + session.Position.y, 1f) : 0f;
            model.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(step * Mathf.PI * 2f)) * bobHeight);
        }
    }
}
