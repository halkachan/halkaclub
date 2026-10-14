using Halka.Game.Player;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Draws the shared Player state. It reads the session every frame and never writes to it.
    // 3D: the model from the Presentation Catalog (PocActorPresentation), so the official ﾊﾙｶﾁｬﾝ
    // model can replace the temporary one without touching Grid, facing or interaction.
    // 2D Pixel View: the model is hidden and the existing hand-drawn sprites are chosen here
    // (drawn by PocPixelView2D), with CharacterVisual's frames and timing.
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
        private bool modelVisible = true;
        private Sprite[] currentFrames;
        private float frameTimer;
        private int frameIndex;

        // 0 turns instantly like the 2D sprites; the 3D view uses a short turn.
        public float TurnSeconds { get; set; }
        // Set by the 2D Pixel View (x: East step, y: North step) so the actor lands on whole
        // low-resolution pixels together with the camera. Zero disables snapping.
        public Vector2 PixelSnap { get; set; }
        public float Yaw => yaw;
        public GameObject ModelInstance => instance;
        public bool HasLimbs => armLeft != null && armRight != null && legLeft != null && legRight != null;
        public float CurrentSwingDegrees { get; private set; }
        public bool ModelVisible => modelVisible;
        // The original sprite for this frame (null when the catalog has no sprites).
        public Sprite CurrentSprite { get; private set; }
        public PocSpriteSet Sprites => catalog != null && catalog.Player != null ? catalog.Player.sprites : null;
        public bool HasSprites => Sprites != null && Sprites.IsComplete;

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
            modelVisible = true;
            armLeft = Find(presentation.armLeft);
            armRight = Find(presentation.armRight);
            legLeft = Find(presentation.legLeft);
            legRight = Find(presentation.legRight);
            if (armLeft != null) armLeftBase = armLeft.localRotation;
            if (armRight != null) armRightBase = armRight.localRotation;
            if (legLeft != null) legLeftBase = legLeft.localRotation;
            if (legRight != null) legRightBase = legRight.localRotation;
        }

        // The 2D Pixel View draws the original sprite instead, so the model (and its shadow)
        // is not drawn there at all, not even into the outline mask.
        private void SetModelVisible(bool visible)
        {
            if (instance == null || modelVisible == visible) return;
            modelVisible = visible;
            foreach (var item in instance.GetComponentsInChildren<Renderer>(true)) item.enabled = visible;
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
            SetModelVisible(!pixelView || !HasSprites);
            AnimateSprite(deltaTime);
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
            var presentation = catalog != null ? catalog.Player : null;
            Swing(deltaTime, presentation);
        }

        // CharacterVisual.Update, frame for frame: restart a set when it changes, then advance
        // 0.15 s per walk frame or 0.4 s per idle frame. Runs in both views so switching back to
        // 2D continues the same cycle; deltaTime 0 (the Pixel View's re-apply) never advances.
        private void AnimateSprite(float deltaTime)
        {
            if (!HasSprites)
            {
                CurrentSprite = null;
                return;
            }
            var selected = Sprites.Select(session.Facing, session.IsMoving);
            if (currentFrames != selected)
            {
                currentFrames = selected;
                frameIndex = 0;
                frameTimer = 0f;
            }
            var frameSeconds = session.IsMoving ? CharacterVisual.OriginalWalkFrameSeconds
                : CharacterVisual.OriginalIdleFrameSeconds;
            if (currentFrames.Length > 1 && deltaTime > 0f)
            {
                frameTimer += deltaTime;
                while (frameTimer >= frameSeconds)
                {
                    frameTimer -= frameSeconds;
                    frameIndex = (frameIndex + 1) % currentFrames.Length;
                }
            }
            CurrentSprite = currentFrames[frameIndex];
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
