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
    [DefaultExecutionOrder(-20)]
    public sealed class PocCrow : CrowBehaviourCore, ISharedGroundOccupant, ISharedTalkTarget
    {
        public const string TalkMessage = "カァ。";

        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private InteractionAudio voice;

        private readonly List<ICrowPerch> perches = new List<ICrowPerch>();
        private readonly Dictionary<PocObjectView, TreePerch> perchByTree = new Dictionary<PocObjectView, TreePerch>();

        public Sprite CurrentSprite { get; private set; }
        public bool CurrentFlipX { get; private set; }
        // In the air from takeoff to touchdown: the 2D game draws it above everything then.
        public bool Airborne => activity != CrowActivity.GroundIdle && activity != CrowActivity.GroundWalk;
        public int TalkCount { get; private set; }
        public Vector2Int LastRustleCell { get; private set; }
        public PocObjectView TargetTreeView => (targetTree as TreePerch)?.View;
        public MapDefinition ActiveMap => activeMap;

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

        protected override IReadOnlyList<ICrowPerch> PerchCandidates()
        {
            perches.Clear();
            if (presenter == null || activeMap == null) return perches;
            // A map rebuild replaces the tree views; forget the destroyed ones.
            if (perchByTree.Count > 0)
            {
                var stale = new List<PocObjectView>();
                foreach (var view in perchByTree.Keys)
                    if (view == null) stale.Add(view);
                foreach (var view in stale) perchByTree.Remove(view);
            }
            foreach (var view in presenter.Views)
            {
                if (view == null || view.PerchAnchor == null) continue;
                if (!perchByTree.TryGetValue(view, out var perch))
                {
                    perch = new TreePerch(view);
                    perchByTree[view] = perch;
                }
                if (!perch.IsUsable) continue;
                if (!activeMap.Contains(PositionToCell(perch.PerchPosition))) continue;
                perches.Add(perch);
            }
            return perches;
        }

        protected override void RustleAt(Vector2Int cell) => LastRustleCell = cell;
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
            if (!isActiveAndEnabled || !ExamineInteractable.IsInRange(playerCell, playerMoving, facing, Cell) ||
                !CanInteract) return false;
            if (session != null) session.ShowMessage(TalkMessage);
            if (voice != null) voice.Play();
            TalkCount++;
            OnExamined();
            return true;
        }

        // A Blender tree as a perch: its PerchAnchor, usable while the tree is drawn.
        private sealed class TreePerch : ICrowPerch
        {
            public readonly PocObjectView View;
            public TreePerch(PocObjectView view) => View = view;
            public bool IsUsable => View != null && View.PerchAnchor != null && View.gameObject.activeInHierarchy;
            public Vector3 PerchPosition => View.PerchAnchor.position;
        }
    }
}
