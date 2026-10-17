using System;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: a dynamic actor on the ground (the crow) that reserves cells, exactly like
    // DynamicGridOccupancy2D does for the 2D game.
    public interface ISharedGroundOccupant
    {
        bool ReservesGround(Vector2Int cell);
    }

    // Phase 4: an entity the player can talk to with A or a click (the crow).
    public interface ISharedTalkTarget
    {
        bool TryTalk(Vector2Int playerCell, FacingDirection facing, bool playerMoving);
    }

    public enum SharedTransitionKind { None, EnteredBuilding, ExitedBuilding }

    // What the last map change was, for presentation only (the 3D exit camera).
    public struct SharedTransition
    {
        public SharedTransitionKind Kind;
        public Vector2Int DoorCell;
        public FacingDirection ArrivalFacing;
        public int Serial;
    }

    // The one gameplay state of the POC. Both views read it; neither view owns a copy.
    // Player movement reuses the existing GridStepMotion and the 0.18 s step of PlayerMover.
    [DefaultExecutionOrder(-30)]
    public sealed class SharedWorldSession : MonoBehaviour
    {
        public const string BenchPoseKey = PlayerSeatController.BenchPoseKey;
        // The 2D game has no cushion pose yet; Phase 4 seats the player with the same artwork.
        public const string CushionPoseKey = "cushion_sit";

        [SerializeField] private MapDefinition outdoorMap;
        [SerializeField] private MapDefinition interiorMap;
        [SerializeField] private PocInput input;
        [SerializeField, Min(0.05f)] private float stepSeconds = PlayerMover.DefaultStepSeconds;

        private GridStepMotion motion;
        private SharedWorldModel outdoor;
        private SharedWorldModel interior;
        private int transitionSerial;

        public SharedWorldModel World { get; private set; }
        public SharedWorldModel OutdoorWorld => outdoor;
        public SharedWorldModel InteriorWorld => interior;
        public Vector2Int Cell => motion.Cell;
        public Vector2 Position => motion.Position;
        public bool IsMoving => motion.IsMoving;
        public Vector2Int StepFromCell => motion.StepFromCell;
        public Vector2Int StepToCell => motion.StepToCell;
        public float StepProgressNormalized => motion.StepProgressNormalized;
        public FacingDirection Facing { get; private set; } = FacingDirection.Down;
        public long CompletedSteps { get; private set; }
        public int MapLoads { get; private set; }
        public float StepSeconds => stepSeconds;
        public string LastMessage { get; private set; }
        public float LastMessageAt { get; private set; } = -100f;
        public SharedTransition LastTransition { get; private set; }

        // Phase 4: the crow (occupancy and talking); the phone rotation guide (no input).
        public ISharedGroundOccupant GroundOccupant { get; set; }
        public ISharedTalkTarget TalkTarget { get; set; }
        public bool InputSuspended { get; set; }

        // Phase 4: sitting (PlayerSeatController's rule: the player keeps the Action Point cell;
        // only the artwork moves onto the seat). Movement is locked while seated.
        public SharedObjectInstance SeatObject { get; private set; }
        public string SeatPose { get; private set; }
        public Vector2Int SeatCell { get; private set; }
        public bool IsSeated => SeatObject != null;
        public bool MovementLocked => IsSeated;

        public event Action<SharedWorldModel> MapChanged;
        // Phase 5: a step has been accepted (Grid collision passed) and starts moving; the 0.18 s
        // interpolation follows and StepCompleted ends it (Life Log). Footsteps and grass rustle.
        public event Action<Vector2Int> StepStarted;
        public event Action<Vector2Int> StepCompleted;
        public event Action<SharedObjectInstance, string> Interacted;
        public long StartedSteps { get; private set; }
        public event Action<string> MessageShown;
        public event Action SeatChanged;

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

        public MapDefinition GetMap(string mapId)
        {
            if (outdoor != null && outdoor.MapId == mapId) return outdoor.Map;
            if (interior != null && interior.MapId == mapId) return interior.Map;
            throw new ArgumentException("Unknown map ID: " + mapId);
        }

        private void Update()
        {
            if (InputSuspended)
            {
                Tick(Time.deltaTime, Vector2Int.zero);
                return;
            }
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

        // A manual or AUTO step in a world direction (PlayerMover.TryStep's rules).
        public bool TryStep(Vector2Int direction)
        {
            if (MovementLocked || motion.IsMoving || Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1) return false;
            Facing = FacingDirectionExtensions.FromVector(direction);
            if (!motion.TryBegin(direction, cell => World.CanEnter(cell) && CanPlayerEnter(cell), stepSeconds)) return false;
            StartedSteps++;
            StepStarted?.Invoke(motion.StepToCell);
            return true;
        }

        // DynamicGridOccupancy2D.CanPlayerEnter: the crow's ground cell(s) are taken.
        public bool CanPlayerEnter(Vector2Int cell) => GroundOccupant == null || !GroundOccupant.ReservesGround(cell);

        // DynamicGridOccupancy2D.IsReservedByPlayer: both ends of a step in progress.
        public bool PlayerReserves(Vector2Int cell) =>
            motion.Cell == cell || motion.IsMoving && (motion.StepFromCell == cell || motion.StepToCell == cell);

        // PlayerMover.CancelStep: AUTO stopped by the user ends on the nearer cell.
        public void CancelStep()
        {
            if (!motion.IsMoving) return;
            var cell = motion.StepProgressNormalized < 0.5f ? motion.StepFromCell : motion.StepToCell;
            motion = new GridStepMotion(cell);
        }

        // Regression checks only: place the player without counting a step.
        public void PlaceForCheck(Vector2Int cell, FacingDirection facing)
        {
            Stand();
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
            LoadMap(target, cell, facing, SharedTransitionKind.None, default);
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
                LoadMap(interior, entry, FacingDirection.Up, SharedTransitionKind.EnteredBuilding, door);
                return true;
            }
            if (World == interior && interior.TryGetMarker("interior_exit", out var exit) && motion.Cell == exit &&
                outdoor.TryGetHouseDoor(out var outsideDoor))
            {
                LoadMap(outdoor, outsideDoor + Vector2Int.down, FacingDirection.Down,
                    SharedTransitionKind.ExitedBuilding, outsideDoor);
                return true;
            }
            return false;
        }

        private void LoadMap(SharedWorldModel target, Vector2Int cell, FacingDirection facing,
            SharedTransitionKind kind, Vector2Int door)
        {
            Stand();
            World = target;
            motion = new GridStepMotion(cell);
            Facing = facing;
            MapLoads++;
            LastTransition = new SharedTransition
            {
                Kind = kind, DoorCell = door, ArrivalFacing = facing, Serial = ++transitionSerial
            };
            MapChanged?.Invoke(World);
        }

        // The A button: stand up when seated (GameInput), else talk to the crow or use the
        // object ahead (InteractionRouter.TryInteractAhead).
        public bool InteractAhead()
        {
            if (motion.IsMoving) return false;
            if (IsSeated)
            {
                Stand();
                return true;
            }
            if (TalkTarget != null && TalkTarget.TryTalk(motion.Cell, Facing, motion.IsMoving)) return true;
            var target = World.FindInteractionAhead(motion.Cell, Facing, out _);
            return target != null && InteractWith(target);
        }

        // A click on an object: seated, only the active seat answers (and stands the player up).
        public bool InteractWith(SharedObjectInstance target)
        {
            if (target == null) return false;
            if (IsSeated)
            {
                if (target != SeatObject) return false;
                Stand();
                return true;
            }
            var kind = World.ResolveInteraction(target, motion.Cell, Facing, motion.IsMoving,
                out var message, out var poseKey);
            if (kind == SharedActionKind.Sit) return TrySit(target, poseKey);
            if (kind != SharedActionKind.Message) return false;
            return Report(target, message);
        }

        public bool TalkTo(ISharedTalkTarget target) =>
            !IsSeated && target != null && target.TryTalk(motion.Cell, Facing, motion.IsMoving);

        private bool TrySit(SharedObjectInstance target, string poseKey)
        {
            if (IsSeated || motion.IsMoving || poseKey != BenchPoseKey && poseKey != CushionPoseKey) return false;
            var ahead = motion.Cell + Facing.ToVector();
            var seatCell = motion.Cell;
            foreach (var cell in target.BlockedCells())
                if (cell == ahead) seatCell = ahead;
            SeatObject = target;
            SeatPose = poseKey;
            SeatCell = seatCell;
            SeatChanged?.Invoke();
            return true;
        }

        public bool Stand()
        {
            if (!IsSeated) return false;
            SeatObject = null;
            SeatPose = null;
            SeatChanged?.Invoke();
            return true;
        }

        public void ShowMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            LastMessage = message;
            LastMessageAt = Time.unscaledTime;
            MessageShown?.Invoke(message);
        }

        private bool Report(SharedObjectInstance target, string message)
        {
            if (target == null || string.IsNullOrEmpty(message)) return false;
            ShowMessage(message);
            Interacted?.Invoke(target, message);
            return true;
        }
    }
}
