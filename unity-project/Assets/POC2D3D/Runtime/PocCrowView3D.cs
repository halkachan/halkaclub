using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: draws the shared crow in the 3D view with the Blender model. It reads PocCrow
    // every frame and never writes to it. Simple transform animation per activity (no Rig):
    // ground idle (breathing bob), ground walk (hop and leg swing), takeoff / leaving the perch
    // (wings open, fast beats, leaning up), flying (wingbeats), perched (wings folded, a slow
    // look-around), landing (slower beats, flaring). The wings open and fold in FoldSeconds.
    // Hidden in the 2D view, which draws the sprite instead.
    [DefaultExecutionOrder(55)]
    public sealed class PocCrowView3D : MonoBehaviour
    {
        public const float FlapCyclesPerSecond = 3.2f;
        // The folded wings lie along the body, tips back. Opening swings each wing out to its side
        // (SpreadDegrees about the vertical axis) and turns its flat side toward level
        // (TwistDegrees about its own length); a wingbeat then moves the open wing about the
        // body's front-back axis, from DownstrokeDegrees (below level) to UpstrokeDegrees.
        public const float SpreadDegrees = 80f;
        public const float TwistDegrees = 55f;
        public const float DownstrokeDegrees = -20f;
        public const float UpstrokeDegrees = 40f;
        public const float FoldSeconds = 0.15f;

        [SerializeField] private PocCrow crow;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private float turnSeconds = 0.12f;

        private GameObject instance;
        private Transform head, wingLeft, wingRight, tail, legLeft, legRight;
        private Quaternion headBase, wingLeftBase, wingRightBase, tailBase, legLeftBase, legRightBase;
        private float yaw;
        private float yawVelocity;
        private float open; // 0 folded .. 1 open
        private bool initialized;
        private bool shown = true;

        public bool Visible { get; set; } = true;
        public GameObject ModelInstance => instance;
        public float CurrentWingOpen { get; private set; }
        public float CurrentWingRaiseDegrees { get; private set; }
        public float CurrentYaw => yaw;
        public bool ModelShown => shown && instance != null && instance.activeSelf;

        public void Configure(PocCrow sharedCrow, GameObject model)
        {
            crow = sharedCrow;
            modelPrefab = model;
        }

        private void Awake()
        {
            if (modelPrefab != null && instance == null) BuildModel();
        }

        // (Re)creates the model. The scene keeps the copy built in the Editor; it is replaced here
        // (like PocActorView), so only one crow model ever exists under this view.
        public void BuildModel()
        {
            for (var i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
            instance = Instantiate(modelPrefab, transform, false);
            instance.name = modelPrefab.name;
            // Blender fronts face South; turn the model so it looks along this transform's forward.
            instance.transform.localRotation = Quaternion.Euler(0f, GridSpace3D.ModelFrontYawOffset, 0f);
            head = Find("Head");
            wingLeft = Find("WingL");
            wingRight = Find("WingR");
            tail = Find("Tail");
            legLeft = Find("LegL");
            legRight = Find("LegR");
            if (head != null) headBase = head.localRotation;
            if (wingLeft != null) wingLeftBase = wingLeft.localRotation;
            if (wingRight != null) wingRightBase = wingRight.localRotation;
            if (tail != null) tailBase = tail.localRotation;
            if (legLeft != null) legLeftBase = legLeft.localRotation;
            if (legRight != null) legRightBase = legRight.localRotation;
        }

        public bool HasParts => head != null && wingLeft != null && wingRight != null && tail != null &&
            legLeft != null && legRight != null;

        private Transform Find(string childName)
        {
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }

        // 3D clicks: does the ray hit the drawn crow (its bounds, at least 0.3 wide)?
        public bool RayHits(Ray ray, out float distance)
        {
            distance = float.MaxValue;
            if (!ModelShown) return false;
            var found = false;
            var bounds = new Bounds();
            foreach (var item in instance.GetComponentsInChildren<Renderer>())
            {
                if (!item.enabled) continue;
                if (!found) bounds = item.bounds;
                else bounds.Encapsulate(item.bounds);
                found = true;
            }
            if (!found) return false;
            bounds.size = Vector3.Max(bounds.size, Vector3.one * 0.3f);
            return bounds.IntersectRay(ray, out distance);
        }

        private void LateUpdate() => Apply(Time.deltaTime, Time.time);

        public void Apply(float deltaTime, float now)
        {
            var show = Visible && crow != null && crow.isActiveAndEnabled;
            if (instance != null && instance.activeSelf != show) instance.SetActive(show);
            shown = show;
            if (!show || instance == null) return;
            transform.position = crow.transform.position;
            var targetYaw = TargetYaw();
            var snap = !initialized || deltaTime <= 0f || turnSeconds <= 0f;
            if (snap)
            {
                yaw = targetYaw;
                yawVelocity = 0f;
                initialized = true;
            }
            else yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, turnSeconds, Mathf.Infinity, deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var activity = crow.Activity;
            var targetOpen = activity == CrowActivity.Takeoff || activity == CrowActivity.LeavingPerch ||
                activity == CrowActivity.Flying || activity == CrowActivity.LandingGround ? 1f : 0f;
            open = snap ? targetOpen : Mathf.MoveTowards(open, targetOpen, deltaTime / FoldSeconds);
            Animate(now);
        }

        // On the ground: the Grid facing. In the air: the leg's horizontal direction (perched: the
        // last flight direction, turned toward the camera side like the 2D idle-right frame).
        private float TargetYaw()
        {
            switch (crow.Activity)
            {
                case CrowActivity.GroundIdle:
                case CrowActivity.GroundWalk:
                    return GridSpace3D.FacingYaw(crow.Facing);
                case CrowActivity.Perched:
                    return yaw;
                default:
                    var delta = crow.LegEnd - crow.LegStart;
                    delta.y = 0f;
                    return delta.sqrMagnitude > 0.0004f ? Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg : yaw;
            }
        }

        private void Animate(float now)
        {
            if (!HasParts) return;
            var beat = 0f;
            var legSwing = 0f;
            var bob = 0f;
            var pitch = 0f;
            var headTurn = 0f;
            var tailLift = 0f;
            switch (crow.Activity)
            {
                case CrowActivity.GroundIdle:
                    bob = Mathf.Sin(now * 2.4f) * 0.006f;
                    headTurn = Mathf.Sin(now * 0.7f) * 12f;
                    break;
                case CrowActivity.GroundWalk:
                    var phase = crow.StepProgressNormalized;
                    bob = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * 0.03f;
                    legSwing = Mathf.Sin(phase * Mathf.PI * 2f) * 28f;
                    pitch = -6f; // leaning into the hop
                    break;
                case CrowActivity.Takeoff:
                case CrowActivity.LeavingPerch:
                    beat = Wave(now, 1.4f);
                    pitch = 14f; // nose up while climbing
                    tailLift = 10f;
                    break;
                case CrowActivity.Flying:
                    beat = Wave(now, 1f);
                    pitch = -6f;
                    tailLift = 6f;
                    break;
                case CrowActivity.LandingGround:
                    beat = Wave(now, 0.7f) * 0.8f;
                    pitch = 12f; // flaring before touchdown
                    break;
                case CrowActivity.Perched:
                    bob = Mathf.Sin(now * 2f) * 0.004f;
                    headTurn = Mathf.Sin(now * 0.9f) * 25f;
                    break;
            }
            instance.transform.localPosition = Vector3.up * bob;
            instance.transform.localRotation = Quaternion.Euler(pitch, GridSpace3D.ModelFrontYawOffset, 0f);
            PoseWings(open, beat);
            legLeft.localRotation = legLeftBase * Quaternion.Euler(legSwing, 0f, 0f);
            legRight.localRotation = legRightBase * Quaternion.Euler(-legSwing, 0f, 0f);
            head.localRotation = headBase * Quaternion.Euler(0f, headTurn, 0f);
            tail.localRotation = tailBase * Quaternion.Euler(-tailLift, 0f, 0f);
        }

        // openAmount 0 (folded) .. 1 (open); beatAmount 0 (bottom of the downstroke) .. 1 (top of
        // the upstroke). In the model's own axes (front -Z, up Y): WingL is on +X, the tips point +Z.
        public void PoseWings(float openAmount, float beatAmount)
        {
            if (!HasParts) return;
            var spread = openAmount * SpreadDegrees;
            var twist = openAmount * TwistDegrees;
            var raise = openAmount * Mathf.Lerp(DownstrokeDegrees, UpstrokeDegrees, beatAmount);
            CurrentWingOpen = openAmount;
            CurrentWingRaiseDegrees = raise;
            wingLeft.localRotation = wingLeftBase * Quaternion.Euler(0f, 0f, raise) *
                Quaternion.Euler(0f, spread, 0f) * Quaternion.Euler(0f, 0f, -twist);
            wingRight.localRotation = wingRightBase * Quaternion.Euler(0f, 0f, -raise) *
                Quaternion.Euler(0f, -spread, 0f) * Quaternion.Euler(0f, 0f, twist);
        }

        // One wingbeat: 0 (bottom of the downstroke) .. 1 (top of the upstroke); the upstroke
        // takes 45% of a beat, the (power) downstroke 55%.
        private static float Wave(float now, float speed)
        {
            var t = Mathf.Repeat(now * FlapCyclesPerSecond * speed, 1f);
            var s = t < 0.45f ? t / 0.45f : 1f - (t - 0.45f) / 0.55f;
            return Mathf.SmoothStep(0f, 1f, s);
        }
    }
}
