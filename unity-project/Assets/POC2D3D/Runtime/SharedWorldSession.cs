using System;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // The one gameplay state of the POC. Both views read it; neither view owns a copy.
    // Player movement reuses the existing GridStepMotion and the 0.18 s step of PlayerMover.
    [DefaultExecutionOrder(-30)]
    public sealed class SharedWorldSession : MonoBehaviour
    {
        [SerializeField] private MapDefinition outdoorMap;
        [SerializeField] private MapDefinition interiorMap;
        [SerializeField] private PocInput input;
        [SerializeField, Min(0.05f)] private float stepSeconds = PlayerMover.DefaultStepSeconds;

        private GridStepMotion motion;
        private SharedWorldModel outdoor;
        private SharedWorldModel interior;

        public SharedWorldModel World { get; private set; }
        public Vector2Int Cell => motion.Cell;
        public Vector2 Position => motion.Position;
        public bool IsMoving => motion.IsMoving;
        public Vector2Int StepToCell => motion.StepToCell;
        public FacingDirection Facing { get; private set; } = FacingDirection.Down;
        public long CompletedSteps { get; private set; }
        public int MapLoads { get; private set; }
        public float StepSeconds => stepSeconds;
        public string LastMessage { get; private set; }
        public float LastMessageAt { get; private set; } = -100f;

        public event Action<SharedWorldModel> MapChanged;
        public event Action<Vector2Int> StepCompleted;
        public event Action<SharedObjectInstance, string> Interacted;

        public void Configure(MapDefinition outdoorDefinition, MapDefinition interiorDefinition, PocInput source)
        {
            outdoorMap = outdoorDefinition;
            interiorMap = interiorDefinition;
            input = source;
        }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (World != null) return;
            outdoor = new SharedWorldModel(outdoorMap);
            interior = interiorMap != null ? new SharedWorldModel(interiorMap) : null;
            if (!outdoorMap.TryGetEntitySpawn("player_main", out var spawn))
                throw new InvalidOperationException("The outdoor map has no Player Start");
            World = outdoor;
            motion = new GridStepMotion(spawn.Cell);
            Facing = EntityRuntimeFactory2D.ParseFacing(spawn.Facing);
            MapLoads = 1;
        }

        // A test map (for example a MAP EDITOR fixture) replaces the outdoor map before Awake.
        public void UseOutdoorMapForTest(MapDefinition map)
        {
            outdoorMap = map;
            World = null;
            Initialize();
        }

        private void Update()
        {
            var direction = input != null ? input.ResolveDirection(this) : Vector2Int.zero;
            Tick(Time.deltaTime, direction);
            if (input != null && input.ActionPressed) InteractAhead();
        }

        public void Tick(float deltaTime, Vector2Int direction)
        {
            if (!motion.IsMoving) TryStep(direction);
            var wasMoving = motion.IsMoving;
            motion.Advance(deltaTime);
            if (!wasMoving || motion.IsMoving) return;
            CompletedSteps++;
            var completed = motion;
            StepCompleted?.Invoke(motion.Cell);
            if (CheckHouseTransition() || completed != motion) return;
            TryStep(direction);
        }

        public bool TryStep(Vector2Int direction)
        {
            if (motion.IsMoving || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1) return false;
            Facing = FacingDirectionExtensions.FromVector(direction);
            return motion.TryBegin(direction, World.CanEnter, stepSeconds);
        }

        // Regression checks only: place the player without counting a step.
        public void PlaceForCheck(Vector2Int cell, FacingDirection facing)
        {
            motion = new GridStepMotion(cell);
            Facing = facing;
        }

        // Phase 3 checks and captures: a MAP EDITOR copy of the interior (never the official
        // file), and a direct jump to either map that raises MapChanged like a real door.
        public void UseInteriorMapForTest(MapDefinition map)
        {
            interiorMap = map;
            World = null;
            Initialize();
        }

        public void LoadMapForCheck(bool interiorMap, Vector2Int cell, FacingDirection facing)
        {
            var target = interiorMap ? interior : outdoor;
            if (target == null) throw new InvalidOperationException("No interior map configured");
            LoadMap(target, cell, facing);
        }

        public void SetFacing(FacingDirection facing)
        {
            if (!motion.IsMoving) Facing = facing;
        }

        // House entrance and exit follow HouseArea2D: a completed step onto the door cell
        // or onto the interior exit marker swaps maps without counting an extra step.
        private bool CheckHouseTransition()
        {
            if (interior == null) return false;
            if (World == outdoor && outdoor.TryGetHouseDoor(out var door) && motion.Cell == door &&
                interior.TryGetMarker("interior_entry", out var entry))
            {
                LoadMap(interior, entry, FacingDirection.Up);
                return true;
            }
            if (World == interior && interior.TryGetMarker("interior_exit", out var exit) && motion.Cell == exit &&
                outdoor.TryGetHouseDoor(out var outsideDoor))
            {
                LoadMap(outdoor, outsideDoor + Vector2Int.down, FacingDirection.Down);
                return true;
            }
            return false;
        }

        private void LoadMap(SharedWorldModel target, Vector2Int cell, FacingDirection facing)
        {
            World = target;
            motion = new GridStepMotion(cell);
            Facing = facing;
            MapLoads++;
            MapChanged?.Invoke(World);
        }

        public bool InteractAhead()
        {
            if (motion.IsMoving) return false;
            var target = World.FindInteractionAhead(motion.Cell, Facing, out var message);
            return Report(target, message);
        }

        public bool InteractWith(SharedObjectInstance target)
        {
            if (!World.TryInteract(target, motion.Cell, Facing, motion.IsMoving, out var message)) return false;
            return Report(target, message);
        }

        private bool Report(SharedObjectInstance target, string message)
        {
            if (target == null || string.IsNullOrEmpty(message)) return false;
            LastMessage = message;
            LastMessageAt = Time.unscaledTime;
            Interacted?.Invoke(target, message);
            return true;
        }
    }
}
