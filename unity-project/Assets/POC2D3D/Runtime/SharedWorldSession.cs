using System;
using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Game.World;
using Halka.Poc2D3D.World;
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

    // ver3.1: an inner room and the map whose door Object leads into it.
    [Serializable]
    public sealed class SharedRoomLink
    {
        public string outerMapId;
        public MapDefinition room;
    }

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
        // ver3.1: rooms inside rooms (the sound booth in the house). Each is entered through the
        // door Object (HouseTransition) of its outer map, like the house from first_field.
        [SerializeField] private List<SharedRoomLink> innerRooms = new List<SharedRoomLink>();
        // ver3.2: the outdoor world (regions placed in world cells, chunk size). Empty: the outdoor
        // Map alone at offset (0,0), which is the ver3.1 world (old checks and fixtures).
        [SerializeField] private OutdoorWorldDefinition outdoorWorld = new OutdoorWorldDefinition();
        [SerializeField, Min(0)] private int decorationMargin = 8;
        [SerializeField] private PocInput input;
        [SerializeField, Min(0.05f)] private float stepSeconds = PlayerMover.DefaultStepSeconds;

        private GridStepMotion motion;
        private SharedWorldModel outdoor;
        private SharedWorldModel interior;
        private readonly List<SharedWorldModel> innerWorlds = new List<SharedWorldModel>();
        private int transitionSerial;

        public SharedWorldModel World { get; private set; }
        public SharedWorldModel OutdoorWorld => outdoor;
        public OutdoorWorldIndex OutdoorIndex => outdoor?.OutdoorIndex;
        public SharedWorldModel InteriorWorld => interior;
        // Every world of this session: outdoor, the house, then the inner rooms (ver3.1).
        public IEnumerable<SharedWorldModel> Worlds
        {
            get
            {
                if (outdoor != null) yield return outdoor;
                if (interior != null) yield return interior;
                foreach (var world in innerWorlds) yield return world;
            }
        }
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

        // ver3.1: a room entered from the door Object of outerMapId (the outer map must be one of
        // this session's maps; the room has interior_entry and interior_exit markers).
        public void ConfigureInnerRoom(string outerMapId, MapDefinition room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            innerRooms.RemoveAll(link => link.room == null || link.room.MapId == room.MapId);
            innerRooms.Add(new SharedRoomLink { outerMapId = outerMapId, room = room });
        }

        public IReadOnlyList<SharedRoomLink> InnerRooms => innerRooms;

        // ver3.2: the outdoor world (the scene builder passes the registry's regions); null or no
        // region: the outdoor Map alone at (0,0).
        public void ConfigureOutdoorWorld(OutdoorWorldDefinition definition, int dressingMargin)
        {
            outdoorWorld = definition ?? new OutdoorWorldDefinition();
            decorationMargin = Mathf.Max(0, dressingMargin);
        }

        public OutdoorWorldDefinition EffectiveOutdoorWorld =>
            outdoorWorld != null && outdoorWorld.regions != null && outdoorWorld.regions.Count > 0
                ? outdoorWorld : OutdoorWorldDefinition.Single(outdoorMap, outdoorWorld != null ? outdoorWorld.chunkSize : OutdoorWorldDefinition.DefaultChunkSize);

        // ver3.2, presentation hooks (the chunk streamer): PrepareCell is asked before a step enters a
        // cell (it makes sure the cell's chunk is drawn, at once if the prefetch was late; it never
        // refuses a step that the Grid allows), CellReady tells AUTO which cells are drawn.
        public Func<Vector2Int, bool> PrepareCell { get; set; }
        public Func<Vector2Int, bool> CellReady { get; set; }
        public bool IsCellReady(Vector2Int cell) => World != outdoor || CellReady == null || CellReady(cell);

        private void Awake() => Initialize();

        // Phase 6: where the game resumes (the save system checks the place first). Set before
        // the first Initialize; without it the player starts at the map's Player Start.
        private bool hasStart;
        private string startMapId;
        private Vector2Int startCell;
        private FacingDirection startFacing;

        // cell is a Map cell (what a save stores); outdoors it becomes a world cell at Initialize.
        public void SetStartLocation(string mapId, Vector2Int cell, FacingDirection facing)
        {
            hasStart = true;
            startMapId = mapId;
            startCell = cell;
            startFacing = facing;
        }

        // The maps this session plays (configured before Initialize; the save system reads them).
        public IEnumerable<MapDefinition> Maps
        {
            get
            {
                if (outdoorMap != null) yield return outdoorMap;
                var world = EffectiveOutdoorWorld;
                if (world.regions != null)
                    foreach (var region in world.regions)
                        if (region != null && region.map != null && region.map != outdoorMap) yield return region.map;
                if (interiorMap != null) yield return interiorMap;
                foreach (var link in innerRooms)
                    if (link.room != null) yield return link.room;
            }
        }

        public MapDefinition StartMap => outdoorMap;

        public void Initialize()
        {
            if (World != null) return;
            outdoor = new SharedWorldModel(new OutdoorWorldIndex(EffectiveOutdoorWorld, decorationMargin));
            interior = interiorMap != null ? new SharedWorldModel(interiorMap) : null;
            innerWorlds.Clear();
            foreach (var link in innerRooms)
                if (link.room != null) innerWorlds.Add(new SharedWorldModel(link.room));
            if (!outdoorMap.TryGetEntitySpawn("player_main", out var spawn))
                throw new InvalidOperationException("The outdoor map has no Player Start");
            World = outdoor;
            var spawnRegion = outdoor.OutdoorIndex.FindRegion(outdoorMap.MapId);
            motion = new GridStepMotion(spawnRegion != null ? spawnRegion.ToWorld(spawn.Cell) : spawn.Cell);
            Facing = EntityRuntimeFactory2D.ParseFacing(spawn.Facing);
            MapLoads = 1;
            if (!hasStart || !TryToSessionCell(startMapId, startCell, out var start, out var cell)) return;
            World = start;
            motion = new GridStepMotion(cell);
            Facing = startFacing;
        }

        // A world by Map ID: any region of the outdoor world finds the outdoor world.
        public SharedWorldModel FindWorld(string mapId)
        {
            foreach (var world in Worlds)
                if (world.MapId == mapId) return world;
            if (outdoor != null && outdoor.OutdoorIndex.FindRegion(mapId) != null) return outdoor;
            return null;
        }

        // ver3.2: Map ID + Map cell (what saves and imports hold) -> the world it is in and the
        // session's cell there (world cells outdoors, Map cells indoors). Builds the worlds if needed.
        public bool TryToSessionCell(string mapId, Vector2Int mapCell, out SharedWorldModel world, out Vector2Int sessionCell)
        {
            if (outdoor == null) InitializeWorldsOnly();
            world = FindWorld(mapId);
            sessionCell = mapCell;
            if (world == null) return false;
            if (world == outdoor)
            {
                var region = outdoor.OutdoorIndex.FindRegion(mapId);
                if (region == null) return false;
                sessionCell = region.ToWorld(mapCell);
            }
            return true;
        }

        // ver3.2: where the player is, as a save writes it: the Map of the region she stands in and
        // her cell in that Map (outdoors), or the room's Map ID and cell (indoors). Schema 1 as before.
        public bool TryGetSavePlace(out string mapId, out Vector2Int mapCell)
        {
            mapId = World != null ? World.MapId : null;
            mapCell = Cell;
            if (World == null) return false;
            if (World != outdoor) return true;
            var region = outdoor.OutdoorIndex.RegionAt(Cell);
            if (region == null) return false;
            mapId = region.MapId;
            mapCell = region.ToLocal(Cell);
            return true;
        }

        // The worlds without placing the player (the save system checks a place before the start).
        private void InitializeWorldsOnly()
        {
            if (outdoor != null || outdoorMap == null) return;
            outdoor = new SharedWorldModel(new OutdoorWorldIndex(EffectiveOutdoorWorld, decorationMargin));
            interior = interiorMap != null ? new SharedWorldModel(interiorMap) : null;
            innerWorlds.Clear();
            foreach (var link in innerRooms)
                if (link.room != null) innerWorlds.Add(new SharedWorldModel(link.room));
        }

        // Phase 6 import: move to a checked place now, like a door does (MapChanged), without a
        // step, a transition or an interaction. cell is a Map cell (ver3.2).
        public bool RestoreLocationNow(string mapId, Vector2Int cell, FacingDirection facing)
        {
            if (!TryToSessionCell(mapId, cell, out var target, out var sessionCell)) return false;
            LoadMap(target, sessionCell, facing, SharedTransitionKind.None, default);
            return true;
        }

        // A test map (for example a MAP EDITOR fixture) replaces the outdoor map before Awake.
        public void UseOutdoorMapForTest(MapDefinition map)
        {
            outdoorMap = map;
            outdoorWorld = new OutdoorWorldDefinition { chunkSize = outdoorWorld != null ? outdoorWorld.chunkSize : OutdoorWorldDefinition.DefaultChunkSize };
            World = null;
            outdoor = null;
            Initialize();
        }

        // ver3.2 checks and the chunk test scene: a whole outdoor world (fixture regions) replaces
        // the outdoor Map before the first frame. outdoorMapForSpawn holds the Player Start.
        public void UseOutdoorWorldForTest(OutdoorWorldDefinition definition, MapDefinition outdoorMapForSpawn)
        {
            outdoorWorld = definition;
            outdoorMap = outdoorMapForSpawn;
            World = null;
            outdoor = null;
            Initialize();
        }

        public MapDefinition GetMap(string mapId)
        {
            var world = FindWorld(mapId);
            if (world != null) return world.Map;
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
            if (!motion.TryBegin(direction, cell => World.CanEnter(cell) && CanPlayerEnter(cell) && Prepare(cell), stepSeconds)) return false;
            StartedSteps++;
            StepStarted?.Invoke(motion.StepToCell);
            return true;
        }

        // ver3.2: outdoors, the cell's chunk is drawn before she walks in (never refuses: a chunk
        // that fails to build is drawn as plain ground, so she cannot be shut in by a load).
        private bool Prepare(Vector2Int cell)
        {
            if (World == outdoor && PrepareCell != null) PrepareCell(cell);
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

        public void LoadMapForCheck(string mapId, Vector2Int cell, FacingDirection facing)
        {
            var target = FindWorld(mapId) ?? throw new InvalidOperationException("Unknown map " + mapId);
            LoadMap(target, cell, facing, SharedTransitionKind.None, default);
        }

        public void SetFacing(FacingDirection facing)
        {
            if (!motion.IsMoving) Facing = facing;
        }

        // House entrance and exit follow HouseArea2D: a completed step onto the door cell
        // or onto the interior exit marker swaps maps without counting an extra step.
        // ver3.1: the same rule for every room: first_field <-> halka_house <-> halka_soundbooth.
        private bool CheckHouseTransition()
        {
            if (TryGetRoomBehindDoor(World, motion.Cell, out var room, out var door) &&
                room.TryGetMarker("interior_entry", out var entry))
            {
                LoadMap(room, entry, FacingDirection.Up, SharedTransitionKind.EnteredBuilding, door);
                return true;
            }
            if (TryGetOuterOfExit(World, motion.Cell, out var outer, out var outsideDoor))
            {
                LoadMap(outer, outsideDoor + Vector2Int.down, FacingDirection.Down,
                    SharedTransitionKind.ExitedBuilding, outsideDoor);
                return true;
            }
            return false;
        }

        // The room whose door Object has its root on this cell of this world (the house outdoors,
        // the booth in the house).
        public bool TryGetRoomBehindDoor(SharedWorldModel world, Vector2Int cell, out SharedWorldModel room,
            out Vector2Int door)
        {
            room = null;
            if (world == null || !world.TryGetHouseDoor(out door) || cell != door) { door = default; return false; }
            if (world == outdoor) room = interior;
            else
                foreach (var link in innerRooms)
                    if (link.room != null && link.outerMapId == world.MapId) room = FindWorld(link.room.MapId);
            return room != null;
        }

        // The outer world and its door when this cell is this room's interior_exit.
        public bool TryGetOuterOfExit(SharedWorldModel world, Vector2Int cell, out SharedWorldModel outer,
            out Vector2Int outsideDoor)
        {
            outer = null;
            outsideDoor = default;
            if (world == null || !world.IsInterior || !world.TryGetMarker("interior_exit", out var exit) || cell != exit)
                return false;
            if (world == interior) outer = outdoor;
            else
                foreach (var link in innerRooms)
                    if (link.room != null && link.room.MapId == world.MapId) outer = FindWorld(link.outerMapId);
            return outer != null && outer.TryGetHouseDoor(out outsideDoor);
        }

        // A cell where a completed step changes the map: AUTO never steps there, a save never
        // resumes there.
        public bool IsTransitionCell(SharedWorldModel world, Vector2Int cell) =>
            TryGetRoomBehindDoor(world, cell, out _, out _) || TryGetOuterOfExit(world, cell, out _, out _);

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
