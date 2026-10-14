using UnityEngine;

namespace Halka.Poc2D3D
{
    // Draws the shared Player state. It reads the session every frame and never writes to it.
    // The model comes from the Presentation Catalog (PocActorPresentation), so the official
    // ﾊﾙｶﾁｬﾝ model can replace the temporary one without touching Grid, facing or interaction.
    [DefaultExecutionOrder(50)]
    public sealed class PocActorView : MonoBehaviour
    {
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private Transform model;
        [SerializeField] private PocPresentationCatalog catalog;
        [SerializeField, Min(0f)] private float bobHeight = 0.025f;

        private float yaw;
        private float yawVelocity;
        private bool initialized;
        private GameObject instance;
        private Transform armLeft, armRight, legLeft, legRight;
        private Quaternion armLeftBase, armRightBase, legLeftBase, legRightBase;
        private float swingWeight;
        private float swingVelocity;

        // 0 turns instantly like the 2D sprites; the 3D view uses a short turn.
        public float TurnSeconds { get; set; }
        // Set by the 2D Pixel View (x: East step, y: North step) so the actor lands on whole
        // low-resolution pixels together with the camera. Zero disables snapping.
        public Vector2 PixelSnap { get; set; }
        // Set by the 2D Pixel View: its camera pitch, used for the 2D-only face-the-camera lean.
        public float PixelViewPitch { get; set; } = 90f;
        public float CurrentLeanDegrees { get; private set; }
        public float Yaw => yaw;
        public GameObject ModelInstance => instance;
        public bool HasLimbs => armLeft != null && armRight != null && legLeft != null && legRight != null;
        public float CurrentSwingDegrees { get; private set; }

        public void Configure(SharedWorldSession worldSession, Transform modelTransform, PocPresentationCatalog presentation = null)
        {
            session = worldSession;
            model = modelTransform;
            catalog = presentation;
        }

        private void Awake()
        {
            if (catalog != null && catalog.Player != null && catalog.Player.model != null) BuildModel();
        }

        // (Re)creates the model from the catalog entry. Safe to call again after a swap.
        public void BuildModel()
        {
            var presentation = catalog.Player;
            for (var i = model.childCount - 1; i >= 0; i--) DestroyImmediate(model.GetChild(i).gameObject);
            model.localRotation = Quaternion.Euler(0f, presentation.yawOffsetDegrees, 0f);
            instance = Instantiate(presentation.model, model, false);
            instance.name = presentation.model.name;
            instance.transform.localPosition = presentation.pivotOffset;
            instance.transform.localScale = Vector3.one * presentation.uniformScale;
            PocOutlineTag.Apply(instance, PocOutlineTag.PlayerId, presentation.outlineStrength);
            shadowsOn = true;
            armLeft = Find(presentation.armLeft);
            armRight = Find(presentation.armRight);
            legLeft = Find(presentation.legLeft);
            legRight = Find(presentation.legRight);
            if (armLeft != null) armLeftBase = armLeft.localRotation;
            if (armRight != null) armRightBase = armRight.localRotation;
            if (legLeft != null) legLeftBase = legLeft.localRotation;
            if (legRight != null) legRightBase = legRight.localRotation;
        }

        private bool shadowsOn = true;

        // The hand-drawn 2D sprites have no shadow; in the Pixel View the model casts none either.
        private void SetShadows(bool on)
        {
            if (instance == null || shadowsOn == on) return;
            shadowsOn = on;
            foreach (var item in instance.GetComponentsInChildren<Renderer>(true))
                item.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Transform Find(string childName)
        {
            if (string.IsNullOrEmpty(childName) || instance == null) return null;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        private void LateUpdate() => Apply(Time.deltaTime);

        public void Apply(float deltaTime)
        {
            var position = GridSpace3D.ToWorld(session.Position);
            var pixelView = PixelSnap.x > 0f && PixelSnap.y > 0f;
            if (pixelView)
            {
                position.x = Mathf.Round(position.x / PixelSnap.x) * PixelSnap.x;
                position.z = Mathf.Round(position.z / PixelSnap.y) * PixelSnap.y;
            }
            transform.position = position;
            SetShadows(!pixelView);
            var target = GridSpace3D.FacingYaw(session.Facing);
            if (!initialized || TurnSeconds <= 0f || deltaTime <= 0f)
            {
                yaw = target;
                yawVelocity = 0f;
                initialized = true;
            }
            else yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVelocity, TurnSeconds);
            var presentationForLean = catalog != null ? catalog.Player : null;
            CurrentLeanDegrees = pixelView && presentationForLean != null
                ? (90f - PixelViewPitch) * presentationForLean.pixelViewFaceCamera : 0f;
            // The lean turns the model's front toward the 2D camera (which always looks North);
            // it rotates about the feet and changes only the drawing, never the cell or facing.
            transform.rotation = Quaternion.Euler(CurrentLeanDegrees, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f);
            if (model == null) return;
            var step = session.IsMoving ? Mathf.Repeat(session.Position.x + session.Position.y, 1f) : 0f;
            model.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(step * Mathf.PI * 2f)) * bobHeight);
            var presentation = catalog != null ? catalog.Player : null;
            if (pixelView && presentation != null && presentation.pixelViewDepthBias > 0f)
            {
                // Along the orthographic view direction: the drawn pixels stay where they are and
                // the actor root (the shared cell position) is not moved.
                var pitch = PixelViewPitch * Mathf.Deg2Rad;
                model.position += new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * presentation.pixelViewDepthBias;
            }
            if (instance != null && presentation != null)
                instance.transform.localScale = Vector3.one * presentation.uniformScale *
                    (pixelView ? presentation.pixelViewScale : 1f);
            Swing(deltaTime, presentation);
        }

        // Presentation only: limbs swing with the distance walked (one cycle every two cells,
        // i.e. two 0.18 s steps), easing out when the player stops. Position and timing of the
        // Grid step are untouched.
        private void Swing(float deltaTime, PocActorPresentation presentation)
        {
            if (!HasLimbs || presentation == null) return;
            var target = session.IsMoving ? 1f : 0f;
            swingWeight = deltaTime > 0f
                ? Mathf.SmoothDamp(swingWeight, target, ref swingVelocity, 0.06f, Mathf.Infinity, deltaTime)
                : target;
            var phase = Mathf.Repeat((Mathf.Abs(session.Position.x) + Mathf.Abs(session.Position.y)) * 0.5f, 1f);
            CurrentSwingDegrees = Mathf.Sin(phase * Mathf.PI * 2f) * presentation.swingDegrees * swingWeight;
            var a = CurrentSwingDegrees;
            armLeft.localRotation = armLeftBase * Quaternion.Euler(a, 0f, 0f);
            armRight.localRotation = armRightBase * Quaternion.Euler(-a, 0f, 0f);
            legLeft.localRotation = legLeftBase * Quaternion.Euler(-a, 0f, 0f);
            legRight.localRotation = legRightBase * Quaternion.Euler(a, 0f, 0f);
        }
    }
}
