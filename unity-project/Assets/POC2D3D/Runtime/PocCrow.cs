using System.Collections.Generic;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: the one crow of the shared world. CrowBehaviourCore runs the 2D game's crow
    // behaviour unchanged (wander, flight, perch, landing, talk-to reaction); this host answers it
    // from the shared world: positions in 3D world space (x = East, y = altitude, z = North), the
    // outdoor Grid, the player's reservation, and the trees' Blender PerchAnchors. It draws
    // nothing: the 2D Pixel View draws CurrentSprite (the 2D game's frame choice), the 3D view
    // poses the Blender crow. Switching views never touches this state.
    // ver3.4: the crow can also walk with her (companion mode, PocCrow.Companion.cs).
    [DefaultExecutionOrder(-20)]
    public sealed partial class PocCrow : CrowBehaviourCore, ISharedGroundOccupant, ISharedTalkTarget
    {
        public const string TalkMessage = "カァ。";

        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private InteractionAudio voice;

        private readonly List<ICrowPerch> perches = new List<ICrowPerch>();
        // ver3.2: one perch per tree Object (not per drawn tree), so a tree whose chunk is released
        // stays the same perch and the crow keeps its target.
        private readonly Dictionary<string, TreePerch> perchByTree = new Dictionary<string, TreePerch>();

        public Sprite CurrentSprite { get; private set; }
        public bool CurrentFlipX { get; private set; }
        // In the air from takeoff to touchdown: the 2D game draws it above everything then.
        public bool Airborne => activity != CrowActivity.GroundIdle && activity != CrowActivity.GroundWalk;
        public int TalkCount { get; private set; }
        public Vector2Int LastRustleCell { get; private set; }
        public PocObjectView TargetTreeView => (targetTree as TreePerch)?.View;
        public MapDefinition ActiveMap => activeMap;
        // ver3.5: the flight frames (the 2D game's sprites), for the fast travel's flying crow.
        public IReadOnlyList<Sprite> FlightRightFrames => flightRight;
        public IReadOnlyList<Sprite> FlightDownFrames => flightDown;

        public void Configure(SharedWorldSession worldSession, PocWorldPresenter worldPresenter, InteractionAudio audio)
        {
            session = worldSession;
            presenter = worldPresenter;
            voice = audio;
        }

        public void ConfigureSprites(Sprite down, Sprite up, Sprite left, Sprite right, Sprite[] walkDownFrames,
            Sprite[] walkUpFrames, Sprite[] walkLeftFrames, Sprite[] walkRightFrames, Sprite[] flightRightFrames,
            Sprite[] flightDownFrames)
        {
            idleDown = down;
            idleUp = up;
            idleLeft = left;
            idleRight = right;
            walkDown = walkDownFrames;
            walkUp = walkUpFrames;
            walkLeft = walkLeftFrames;
            walkRight = walkRightFrames;
            flightRight = flightRightFrames;
            flightDown = flightDownFrames;
        }

        // EntityRuntimeFactory2D.TryActivateCrow for the shared world: the map's crow_main spawn.
        public bool ApplySpawnFrom(MapDefinition map)
        {
            if (map == null || !map.TryGetEntitySpawn("crow_main", out var spawn)) return false;
            RequireCrowSpawn(spawn);
            activeMap = map;
            ApplySpawnState(spawn);
            SetPointerInteraction(true);
            RefreshVisual();
            WaitingOutside = false;
            MarkPlaced();
            return true;
        }

        // ---------------------------------------------------------------- CrowBehaviourCore host
        protected override bool CanEnterCell(Vector2Int cell) =>
            session != null && session.OutdoorWorld != null && session.OutdoorWorld.CanEnter(cell);

        // DynamicGridOccupancy2D.CanCrowEnter: never where the player stands or steps.
        protected override bool CanCrowEnterCell(Vector2Int cell) =>
            session == null || session.World != session.OutdoorWorld || !session.PlayerReserves(cell);

        protected override Vector3 CellToPosition(Vector2 cell) => GridSpace3D.ToWorld(cell);
        protected override Vector2Int PositionToCell(Vector3 position) => GridSpace3D.ToCell(position);
        protected override Vector3 AltitudeAxis => Vector3.up;
        // The 2D game's flight frame rule reads screen up as North plus height.
        protected override Vector2 ScreenDelta(Vector3 from, Vector3 to) =>
            new Vector2(to.x - from.x, to.z - from.z + (to.y - from.y));

        // ver3.2: the trees of the crow's Map (its region of the outdoor world), from the logical
        // Objects in the Map's order (the order the drawn trees had in ver3.1); a perch point comes
        // from the drawn tree when its chunk is drawn, else from the same model's anchor offset.
        protected override IReadOnlyList<ICrowPerch> PerchCandidates()
        {
            if (Following) return NearbyPerches();
            perches.Clear();
            if (presenter == null || activeMap == null || session == null || session.OutdoorWorld == null) return perches;
            var index = session.OutdoorIndex;
            var region = index != null ? index.FindRegion(activeMap.MapId) : null;
            if (region == null) return perches;
            foreach (var instance in session.OutdoorWorld.Objects)
            {
                if (instance.RegionMapId != activeMap.MapId) continue;
                if (!presenter.TryGetPerchPosition(instance, out var point)) continue;
                if (!region.ContainsWorld(PositionToCell(point))) continue;
                if (!perchByTree.TryGetValue(instance.InstanceId, out var perch))
                {
                    perch = new TreePerch(presenter, instance);
                    perchByTree[instance.InstanceId] = perch;
                }
                perches.Add(perch);
            }
            return perches;
        }

        public event System.Action<Vector2Int> Rustled;

        protected override void RustleAt(Vector2Int cell)
        {
            LastRustleCell = cell;
            Rustled?.Invoke(cell);
        }
        protected override bool HasVisual => true;

        protected override void ShowFrame(Sprite sprite, bool flipX)
        {
            CurrentSprite = sprite;
            CurrentFlipX = flipX;
        }

        // ---------------------------------------------------------------- shared world
        public bool ReservesGround(Vector2Int cell) =>
            isActiveAndEnabled && GroundOccupancyActive &&
            (Cell == cell || IsMoving && (StepFromCell == cell || StepToCell == cell));

        // ExamineInteractable on the 2D crow: in front, facing it, standing, crow available; then
        // "カァ。", the voice, and the reaction (takeoff toward a nearby tree).
        public bool TryTalk(Vector2Int playerCell, FacingDirection facing, bool playerMoving)
        {
            if (!isActiveAndEnabled || ChoiceOpen || TravelHeld || !ExamineInteractable.IsInRange(playerCell, playerMoving, facing, Cell) ||
                !CanInteract) return false;
            // ver3.4: "カァ。" and then the choices (ついてきて / あそんでおいで, やめる) in the game's
            // menu panel; the crow waits until one is chosen. Without a menu (old checks): as before.
            ChoiceOpen = OpenChoices != null && OpenChoices();
            if (session != null) session.ShowMessage(TalkMessage);
            if (voice != null) voice.Play();
            TalkCount++;
            if (ChoiceOpen) interactionCooldownUntil = Time.time + InteractionCooldownSeconds;
            else OnExamined();
            return true;
        }

        // A Blender tree as a perch: its PerchAnchor, usable while the tree is drawn.
        private sealed class TreePerch : ICrowPerch
        {
            private readonly PocWorldPresenter presenter;
            public readonly SharedObjectInstance Tree;
            public TreePerch(PocWorldPresenter worldPresenter, SharedObjectInstance tree)
            {
                presenter = worldPresenter;
                Tree = tree;
            }
            // The drawn tree when its chunk is drawn (null otherwise).
            public PocObjectView View => presenter != null ? presenter.FindView(Tree.InstanceId) : null;
            public bool IsUsable => presenter != null && presenter.TryGetPerchPosition(Tree, out _);
            public Vector3 PerchPosition => presenter.TryGetPerchPosition(Tree, out var point) ? point : GridSpace3D.ToWorld(Tree.RootCell);
        }
    }
}
