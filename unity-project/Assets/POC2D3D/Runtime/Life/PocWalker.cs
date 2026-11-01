using System.Collections.Generic;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: one character that walks the outdoor Grid like ﾊﾙｶﾁｬﾝ (Rin, Len, a fan, an anti).
    // The logical part (cell, step, facing) is what the game uses; the drawn part follows it:
    //   - 3D: the Blender model (pivots for hands / feet or arms / legs swung while walking, a small
    //     bob), turned smoothly to its facing, outlined like the player (its own outline id);
    //   - 2D: the sprite frame (idle, or the two walk frames) that the Pixel View draws as a card.
    // The 3D model is built only while the character is drawn (its cell's chunk is drawn, outdoors);
    // a character far away keeps its logical state and has no GameObject children.
    public sealed class PocWalker : MonoBehaviour
    {
        public static readonly List<PocWalker> Active = new List<PocWalker>();

        // Characters destroyed without OnDestroy (a scene closed in the editor) leave the list.
        public static void Prune() => Active.RemoveAll(w => w == null);

        public string Id { get; private set; }
        public string Kind { get; private set; }
        public FacingDirection Facing { get; private set; } = FacingDirection.Down;
        public float StepSeconds { get; set; } = 0.24f;
        public Vector2Int Cell => motion.Cell;
        public bool IsMoving => motion.IsMoving;
        public Vector2Int StepFromCell => motion.StepFromCell;
        public Vector2Int StepToCell => motion.StepToCell;
        public Vector2 Position => motion.Position;
        public float StepProgressNormalized => motion.StepProgressNormalized;
        public bool Present { get; private set; }      // in the outdoor world and taking part
        public bool Drawn { get; private set; }        // its model / sprite are drawn now
        public bool HiddenByGame { get; set; }         // e.g. while she is indoors
        public Sprite CurrentSprite { get; private set; }
        public float SpriteOffsetNorth { get; set; } = -0.22f;
        public int StepsTaken { get; private set; }
        public float LastStepAt { get; private set; } = -100f;
        public int OutlineId { get; private set; } = PocOutlineTag.CharacterId;
        public GameObject ModelInstance => instance;

        private GridStepMotion motion = new GridStepMotion(Vector2Int.zero);
        private GameObject modelSource;
        private Material modelMaterial;
        private float modelScale = 1f;
        private float yawOffset = GridSpace3D.ModelFrontYawOffset;
        private GameObject instance;
        private Transform modelRoot;
        private readonly List<(Transform part, Quaternion rest, float sign, bool isLeg)> limbs =
            new List<(Transform, Quaternion, float, bool)>();
        private PocSpriteSet sprites;
        private float yaw;
        private float yawVelocity;
        private bool yawSet;
        private float spriteClock;
        private int walkFrame;
        private float swing;

        public void Setup(string id, string kind, GameObject model, Material material, float scale, PocSpriteSet spriteSet,
            int outlineId)
        {
            Id = id;
            Kind = kind;
            modelSource = model;
            modelMaterial = material;
            modelScale = scale;
            sprites = spriteSet;
            OutlineId = outlineId;
            if (!Active.Contains(this)) Active.Add(this);   // (OnEnable does not run in Edit Mode checks)
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); SetDrawn(false); }
        private void OnDestroy() { Active.Remove(this); DestroyModel(); }

        public void Place(Vector2Int cell, FacingDirection facing)
        {
            motion = new GridStepMotion(cell);
            Facing = facing;
            Present = true;
            yawSet = false;
        }

        public void Remove()
        {
            Present = false;
            SetDrawn(false);
        }

        public bool ReservesGround(Vector2Int cell) =>
            Present && !HiddenByGame && (motion.Cell == cell || motion.IsMoving && (motion.StepFromCell == cell || motion.StepToCell == cell));

        public bool TryStep(Vector2Int direction, System.Func<Vector2Int, bool> canEnter, float seconds = 0f)
        {
            if (!Present || motion.IsMoving || direction == Vector2Int.zero) return false;
            Facing = FacingDirectionExtensions.FromVector(direction);
            if (!motion.TryBegin(direction, canEnter, seconds > 0f ? seconds : StepSeconds)) return false;
            StepsTaken++;
            LastStepAt = Time.time;
            return true;
        }

        public void Face(FacingDirection facing) => Facing = facing;

        public void Advance(float deltaTime) => motion.Advance(deltaTime);

        // Drawn while present, outdoors, not hidden, and its cell's chunk is drawn (cellReady).
        public void UpdateDrawn(bool outdoors, System.Func<Vector2Int, bool> cellReady)
        {
            var drawn = Present && outdoors && !HiddenByGame && (cellReady == null || cellReady(motion.Cell));
            SetDrawn(drawn);
        }

        private void SetDrawn(bool drawn)
        {
            if (Drawn == drawn) return;
            Drawn = drawn;
            if (drawn) BuildModel();
            else DestroyModel();
        }

        private void BuildModel()
        {
            if (instance != null || modelSource == null) return;
            modelRoot = new GameObject("model").transform;
            modelRoot.SetParent(transform, false);
            instance = Instantiate(modelSource, modelRoot, false);
            instance.name = modelSource.name;
            instance.transform.localScale = Vector3.one * modelScale;
            if (modelMaterial != null)
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = modelMaterial;
            PocWorn.Exempt(instance);   // ver4.0: characters keep their colours in the worn world
            PocOutlineTag.Apply(instance, OutlineId, 1f);
            limbs.Clear();
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            {
                switch (child.name)
                {
                    case "ArmL": case "HandL": limbs.Add((child, child.localRotation, 1f, false)); break;
                    case "ArmR": case "HandR": limbs.Add((child, child.localRotation, -1f, false)); break;
                    case "LegL": case "FootL": limbs.Add((child, child.localRotation, -1f, true)); break;
                    case "LegR": case "FootR": limbs.Add((child, child.localRotation, 1f, true)); break;
                }
            }
            yawSet = false;
        }

        private void DestroyModel()
        {
            if (instance == null) return;
            // The outline copies belong to this model (made by PocOutlineTag.Apply without sharing).
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.name.EndsWith(PocOutlineTag.CopySuffix))
                {
                    if (Application.isPlaying) Destroy(filter.sharedMesh);
                    else DestroyImmediate(filter.sharedMesh);
                }
            if (Application.isPlaying) Destroy(modelRoot.gameObject);
            else DestroyImmediate(modelRoot.gameObject);
            instance = null;
            modelRoot = null;
            limbs.Clear();
        }

        // Presentation: where and how it is drawn this frame (3D model shown only in the 3D view).
        public void Present3D(bool threeD, float deltaTime)
        {
            var position = GridSpace3D.ToWorld(motion.Position);
            transform.position = position;
            var target = GridSpace3D.FacingYaw(Facing);
            if (!yawSet || deltaTime <= 0f)
            {
                yaw = target;
                yawVelocity = 0f;
                yawSet = true;
            }
            else yaw = Mathf.SmoothDampAngle(yaw, target, ref yawVelocity, 0.08f, Mathf.Infinity, deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (modelRoot != null)
            {
                modelRoot.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
                var step = motion.IsMoving ? motion.StepProgressNormalized : 0f;
                modelRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(step * Mathf.PI)) * 0.03f);
                var goal = motion.IsMoving ? Mathf.Sin((StepsTaken % 2 == 0 ? step : step + 1f) * Mathf.PI) : 0f;
                swing = deltaTime > 0f ? Mathf.MoveTowards(swing, goal, deltaTime * 8f) : goal;
                foreach (var (part, rest, sign, isLeg) in limbs)
                    if (part != null) part.localRotation = rest * Quaternion.Euler(swing * sign * (isLeg ? 28f : 24f), 0f, 0f);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.enabled = threeD;
            }
            AnimateSprite(deltaTime);
        }

        private void AnimateSprite(float deltaTime)
        {
            if (sprites == null || !sprites.IsComplete) { CurrentSprite = null; return; }
            var frames = sprites.Select(Facing, motion.IsMoving);
            if (frames == null || frames.Length == 0) { CurrentSprite = null; return; }
            if (motion.IsMoving)
            {
                spriteClock += deltaTime;
                if (spriteClock >= StepSeconds * 0.5f)
                {
                    spriteClock = 0f;
                    walkFrame = (walkFrame + 1) % frames.Length;
                }
            }
            else
            {
                spriteClock = 0f;
                walkFrame = 0;
            }
            CurrentSprite = frames[Mathf.Clamp(walkFrame, 0, frames.Length - 1)];
        }

        // Screen picks in 3D: the drawn model's bounds.
        public bool RayHits(Ray ray, out float distance)
        {
            distance = float.MaxValue;
            if (instance == null) return false;
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
            bounds.Expand(0.1f);
            return bounds.IntersectRay(ray, out distance);
        }
    }
}
