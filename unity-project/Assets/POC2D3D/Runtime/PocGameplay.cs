using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: connects the shared world to the game's existing systems, each used as it is:
    // AutoModeController (on the shared Grid), LifeLogController (completed steps, same save key),
    // GameHud (messages), GameMenuController (AUTO, Life Log, and the view row), and the crow's
    // life cycle (EntityRuntimeFactory2D's rule: indoors it is gone, outdoors it respawns).
    [DefaultExecutionOrder(-25)]
    public sealed class PocGameplay : MonoBehaviour
    {
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private AutoModeController autoMode;
        [SerializeField] private LifeLogController lifeLog;
        [SerializeField] private GameHud hud;
        [SerializeField] private GameMenuController menu;
        [SerializeField] private PocCrow crow;
        [Header("Phase 5")]
        [SerializeField] private PlayerFootstepAudio footsteps;

        private PocAutoWorld autoWorld;
        private ViewMenuItem viewItem;
        private bool suspended;

        public AutoModeController AutoMode => autoMode;
        public LifeLogController LifeLog => lifeLog;
        public GameHud Hud => hud;
        public GameMenuController Menu => menu;
        public PocCrow Crow => crow;
        public PlayerFootstepAudio Footsteps => footsteps;

        public void ConfigureFootsteps(PlayerFootstepAudio footstepAudio) => footsteps = footstepAudio;
        public PocAutoWorld AutoWorld => autoWorld;

        // Phone rotation guide: no input, no AUTO, no play time (the world clock keeps running).
        public bool Suspended
        {
            get => suspended;
            set
            {
                suspended = value;
                if (session != null) session.InputSuspended = value;
                if (lifeLog != null) lifeLog.ForegroundBlocked = value;
                if (autoMode != null && value) autoMode.RecordUserAction(Time.unscaledTime);
            }
        }

        public void Configure(SharedWorldSession worldSession, PocViewSwitcher viewSwitcher, AutoModeController auto,
            LifeLogController log, GameHud messageHud, GameMenuController gameMenu, PocCrow sharedCrow)
        {
            session = worldSession;
            views = viewSwitcher;
            autoMode = auto;
            lifeLog = log;
            hud = messageHud;
            menu = gameMenu;
            crow = sharedCrow;
        }

        private void Awake() => Connect();

        private bool connected;

        // Safe to call more than once (edit-mode checks call it before the first frame).
        public void Connect()
        {
            if (connected || session == null) return;
            connected = true;
            session.Initialize();
            if (crow != null)
            {
                session.GroundOccupant = crow;
                session.TalkTarget = crow;
            }
            autoWorld = new PocAutoWorld(session, crow, hud, this);
            if (autoMode != null) autoMode.UseWorld(autoWorld);
            viewItem = new ViewMenuItem(views);
            if (menu != null) menu.SetExtraItem(viewItem);
            session.MapChanged += OnMapChanged;
            session.StepCompleted += OnStepCompleted;
            session.StepStarted += OnStepStarted;
            session.MessageShown += OnMessageShown;
            ActivateCrowFor(session.World);
        }

        private void OnDestroy()
        {
            if (!connected || session == null) return;
            session.MapChanged -= OnMapChanged;
            session.StepCompleted -= OnStepCompleted;
            session.StepStarted -= OnStepStarted;
            session.MessageShown -= OnMessageShown;
        }

        private void OnMapChanged(SharedWorldModel world) => ActivateCrowFor(world);
        private void OnStepCompleted(Vector2Int cell) { if (lifeLog != null) lifeLog.RecordCompletedStep(); }
        // Manual and AUTO steps both start here (one step, one footstep; blocked steps never start).
        private void OnStepStarted(Vector2Int cell) { if (footsteps != null && footsteps.isActiveAndEnabled) footsteps.PlayStep(); }
        private void OnMessageShown(string text) { if (hud != null) hud.ShowMessage(text); }

        // Indoors the outdoor crow is switched off (and frees its cell); back outdoors it is
        // rebuilt from the map's crow_main spawn, exactly like MapRuntimeLoader2D.Build.
        private void ActivateCrowFor(SharedWorldModel world)
        {
            if (crow == null || world == null) return;
            var outdoors = world == session.OutdoorWorld;
            if (!outdoors)
            {
                crow.gameObject.SetActive(false);
                return;
            }
            crow.gameObject.SetActive(true);
            if (!crow.ApplySpawnFrom(world.Map)) crow.gameObject.SetActive(false);
        }

        // The menu row that switches the view (2D pixel / 3D third-person).
        private sealed class ViewMenuItem : IGameMenuItem
        {
            private readonly PocViewSwitcher views;
            public ViewMenuItem(PocViewSwitcher viewSwitcher) => views = viewSwitcher;
            public string Label => "ひょうじ";
            public string Value => views != null && views.Mode == PocViewMode.ThirdPerson3D ? "3D" : "2D";
            public void Activate(float now) { if (views != null) views.Toggle(); }
        }
    }

    // IAutoModeWorld on the shared world: the same AUTO (60 s idle, outdoor interests, indoor
    // destinations, no doors) steps the shared player with world directions, never camera ones.
    public sealed class PocAutoWorld : IAutoModeWorld
    {
        private readonly SharedWorldSession session;
        private readonly PocCrow crow;
        private readonly GameHud hud;
        private readonly PocGameplay owner;

        public PocAutoWorld(SharedWorldSession worldSession, PocCrow sharedCrow, GameHud messageHud, PocGameplay gameplay)
        {
            session = worldSession;
            crow = sharedCrow;
            hud = messageHud;
            owner = gameplay;
        }

        public string ActiveMapId => session.World.MapId;
        public bool HasMapRegistry => true;
        public MapDefinition GetMap(string mapId) => session.GetMap(mapId);
        public bool HasPlayer => session != null;
        public Vector2Int PlayerCell => session.Cell;
        public bool PlayerIsMoving => session.IsMoving;
        public bool PlayerMovementLocked => session.MovementLocked;
        public int AutoSteps { get; private set; }

        public bool TryStep(Vector2Int direction)
        {
            var stepped = session.TryStep(direction);
            if (stepped) AutoSteps++;
            return stepped;
        }

        public void CancelStep() => session.CancelStep();
        public bool HasActiveMessage => hud != null && hud.HasActiveMessage;
        public bool HasHouse => session.OutdoorWorld != null && session.OutdoorWorld.TryGetHouseDoor(out _);
        public Vector2Int HouseDoorCell => session.OutdoorWorld.Map.HouseDoorCell;
        public Vector2Int OutsideEntryCell => session.OutdoorWorld.Map.OutsideEntryCell;
        public bool CanEnter(Vector2Int cell) => session.World.CanEnter(cell);
        public bool CanPlayerEnter(Vector2Int cell) => session.CanPlayerEnter(cell);

        public bool TryGetCrowCell(out Vector2Int cell)
        {
            var active = crow != null && crow.isActiveAndEnabled;
            cell = active ? crow.Cell : default;
            return active;
        }

        public bool Suspended => owner != null && owner.Suspended;
    }
}
