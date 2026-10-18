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
        [Header("Phase 4: seats")]
        [SerializeField] private PocWorldPresenter presenter;

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

        // Phase 4: where the 2D sprite's origin goes (the player, or the seat when seated) and
        // its 2D-game height above that point (0.25 standing; PlayerSeatController's 10/64 on
        // the bench, scaled by the seat height for the lower cushion).
        public Vector3 SpriteRoot { get; private set; }
        public float SpriteOffsetNorth { get; private set; }
        public bool Seated { get; private set; }
        public Vector3 SeatPoint { get; private set; }
        public float HipHeight { get; private set; }
        // The bench seat anchor height (Phase 3 Blender bench), the reference for 10/64.
        public const float BenchSeatHeight = 0.22f;
        public const float SitLegDegrees = 90f;

        // Phase 5 seat display metadata: presentation only. The seat cell, the action point and the
        // furniture's SeatAnchor_L / SeatAnchor_R / SitAnchor stay as authored; the model is drawn
        // HipLift above the anchor with its legs at LegDegrees. Phase 5 measured the placeholder:
        // its thighs (0.132 thick, centred on the hips) went 0.026 into the bench seat (surface
        // 0.18) and 0.068 into the cushion (surface 0.102). An official model replaces these rows.
        // TowardCenter moves the drawn model along the seat's length toward the furniture's middle:
        // each bench seat anchor is on its Grid cell centre, 0.22 from the bench end, which put the
        // 0.4 wide placeholder flush with the end (Phase 4 images). Two seats stay two seats.
        public readonly struct SeatDisplay
        {
            public readonly float HipLift;
            public readonly float LegDegrees;
            public readonly float TowardCenter;
            public SeatDisplay(float hipLift, float legDegrees, float towardCenter = 0f)
            {
                HipLift = hipLift;
                LegDegrees = legDegrees;
                TowardCenter = towardCenter;
            }
        }

        public static readonly SeatDisplay BenchSitDisplay = new SeatDisplay(0.03f, SitLegDegrees, 0.08f);
        // On the floor cushion the legs slope down to the floor in front of it.
        public static readonly SeatDisplay CushionSitDisplay = new SeatDisplay(0.07f, 62f);
        // The sofa (feedback after RC1): seat surface 0.22 at the anchors, thighs on the seat.
        public static readonly SeatDisplay SofaSitDisplay = new SeatDisplay(0.07f, SitLegDegrees, 0.06f);
        // The display shift applied (world), for checks and reports.
        public Vector3 SeatDisplayShift { get; private set; }

        public static SeatDisplay SeatDisplayFor(string pose) => SeatDisplayFor(null, pose);

        // Per piece of furniture first (the sofa uses the bench's pose), then per pose.
        public static SeatDisplay SeatDisplayFor(string definitionId, string pose) =>
            definitionId == "sofa_basic" ? SofaSitDisplay : pose == "cushion_sit" ? CushionSitDisplay : BenchSitDisplay;

        private SeatDisplay CurrentSeatDisplayFor() =>
            SeatDisplayFor(session.SeatObject != null ? session.SeatObject.DefinitionId : null, session.SeatPose);
        public SeatDisplay CurrentSeatDisplay { get; private set; }
        public const float SitArmDegrees = 28f;

        public void ConfigureSeats(PocWorldPresenter worldPresenter) => presenter = worldPresenter;

        // 3D clicks: does the ray hit the drawn model (its bounds)? Never in 2D (sprite view).
        public bool RayHitsBody(Ray ray, out float distance)
        {
            distance = float.MaxValue;
            if (instance == null || !modelVisible) return false;
            var found = false;
            var bounds = new Bounds();
            foreach (var item in instance.GetComponentsInChildren<Renderer>())
            {
                if (!item.enabled || item.forceRenderingOff) continue;
                if (!found) bounds = item.bounds;
                else bounds.Encapsulate(item.bounds);
                found = true;
            }
            return found && bounds.IntersectRay(ray, out distance);
        }
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
            PocOutlineTag.Apply(instance, PocOutlineTag.PlayerId, 1f); // the 3D view's outline
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
            // The hip (leg pivot) height above the feet, for the sitting pose.
            HipHeight = legLeft != null ? Mathf.Max(0f, legLeft.position.y - instance.transform.position.y) : 0.25f;
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
            Seated = session.IsSeated;
            SeatDisplayShift = Vector3.zero;
            SpriteRoot = position;
            SpriteOffsetNorth = Sprites != null ? Sprites.offsetY : 0.25f;
            if (Seated)
            {
                SeatPoint = FindSeatPoint();
                var seat = GridSpace3D.ToWorld(session.SeatCell);
                if (pixelView)
                {
                    seat.x = Mathf.Round(seat.x / PixelSnap.x) * PixelSnap.x;
                    seat.z = Mathf.Round(seat.z / PixelSnap.y) * PixelSnap.y;
                }
                SpriteRoot = seat;
                SpriteOffsetNorth = PlayerSeatController.ArtworkAboveSeatCenter *
                    Mathf.Clamp(SeatPoint.y / BenchSeatHeight, 0.2f, 1.5f);
                // 3D: the model sits on the seat anchor, facing the seat's front (South), moved
                // TowardCenter along the seat's length (presentation only).
                SeatDisplayShift = SeatShift(CurrentSeatDisplayFor().TowardCenter);
                position = new Vector3(SeatPoint.x, 0f, SeatPoint.z) + SeatDisplayShift;
            }
            transform.position = position;
            SetModelVisible(!pixelView || !HasSprites);
            AnimateSprite(deltaTime);
            var target = Seated ? GridSpace3D.FacingYaw(FacingDirection.Down) : GridSpace3D.FacingYaw(session.Facing);
            if (!initialized || TurnSeconds <= 0f || deltaTime <= 0f)
            {
                yaw = target;
                yawVelocity = 0f;
                initialized = true;
            }
            else yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVelocity, TurnSeconds, Mathf.Infinity, deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (model == null) return;
            if (Seated)
            {
                // Hips on the seat, thighs forward, hands resting forward: a plain sitting pose.
                var display = CurrentSeatDisplay = CurrentSeatDisplayFor();
                model.localPosition = Vector3.up * (SeatPoint.y + display.HipLift - HipHeight);
                if (HasLimbs)
                {
                    legLeft.localRotation = legLeftBase * Quaternion.Euler(display.LegDegrees, 0f, 0f);
                    legRight.localRotation = legRightBase * Quaternion.Euler(display.LegDegrees, 0f, 0f);
                    armLeft.localRotation = armLeftBase * Quaternion.Euler(SitArmDegrees, 0f, 0f);
                    armRight.localRotation = armRightBase * Quaternion.Euler(SitArmDegrees, 0f, 0f);
                    CurrentSwingDegrees = 0f;
                    swingWeight = 0f;
                }
                return;
            }
            var step = session.IsMoving ? Mathf.Repeat(session.Position.x + session.Position.y, 1f) : 0f;
            model.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(step * Mathf.PI * 2f)) * bobHeight);
            var presentation = catalog != null ? catalog.Player : null;
            Swing(deltaTime, presentation);
        }

        // Along the furniture's own right axis toward the middle of its drawn bounds, at most amount.
        private Vector3 SeatShift(float amount)
        {
            if (amount <= 0f || presenter == null || session.SeatObject == null) return Vector3.zero;
            var view = presenter.FindView(session.SeatObject.InstanceId);
            if (view == null) return Vector3.zero;
            var found = false;
            var bounds = new Bounds();
            foreach (var item in view.GetComponentsInChildren<Renderer>())
            {
                if (!found) bounds = item.bounds;
                else bounds.Encapsulate(item.bounds);
                found = true;
            }
            if (!found) return Vector3.zero;
            var axis = view.transform.right;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f) return Vector3.zero;
            axis.Normalize();
            var along = Vector3.Dot(bounds.center - SeatPoint, axis);
            return axis * Mathf.Clamp(along, -amount, amount);
        }

        // The seat anchor (Phase 3 Blender: SeatAnchor_L / SeatAnchor_R on the bench, SitAnchor on
        // the cushion) nearest to the seat cell; without one, the seat cell at bench height.
        private Vector3 FindSeatPoint()
        {
            var cell = GridSpace3D.ToWorld(session.SeatCell);
            var fallback = cell + Vector3.up * BenchSeatHeight;
            var view = presenter != null && session.SeatObject != null ? presenter.FindView(session.SeatObject.InstanceId) : null;
            if (view == null) return fallback;
            var best = fallback;
            var bestDistance = float.MaxValue;
            foreach (var child in view.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("SeatAnchor") && child.name != "SitAnchor") continue;
                var p = child.position;
                var d = new Vector2(p.x - cell.x, p.z - cell.z).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
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
            // CharacterVisual: the seated artwork replaces every frame while seated (Phase 5: the
            // cushion has its own provisional floor-sitting artwork).
            if (session.IsSeated && Sprites.SeatSprite(session.SeatPose) != null)
            {
                CurrentSprite = Sprites.SeatSprite(session.SeatPose);
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
