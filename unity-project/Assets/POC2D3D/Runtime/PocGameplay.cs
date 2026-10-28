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
    // ver3.4: the crow's choices (a page of the menu panel) and the companion crow, which waits
    // beside the house's door while she is inside instead of respawning.
    // ver3.5: そらをとぶ (PocFastTravel) in the crow's choices; while a flight is under way her input,
    // AUTO and the crow wait.
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
        private CrowChoicePage crowChoices;
        private bool suspended;
        [Header("ver3.5")]
        [SerializeField] private PocFastTravel fastTravel;

        public AutoModeController AutoMode => autoMode;
        public LifeLogController LifeLog => lifeLog;
        public GameHud Hud => hud;
        public GameMenuController Menu => menu;
        public PocCrow Crow => crow;
        public PlayerFootstepAudio Footsteps => footsteps;
        public CrowChoicePage CrowChoices => crowChoices;
        public PocFastTravel FastTravel => fastTravel;
        public bool TravelBusy => fastTravel != null && fastTravel.Busy;
        // ver3.5: whether the step that started last was AUTO's (AUTO never finds a place).
        public bool LastStepByAuto { get; private set; }

        public void ConfigureFastTravel(PocFastTravel travel)
        {
            fastTravel = travel;
            if (crowChoices != null) crowChoices.FastTravel = travel;
        }

        // ver3.5: no input while the phone guide shows or a flight is under way.
        public void RefreshInputSuspension()
        {
            if (session != null) session.InputSuspended = suspended || TravelBusy;
        }

        public void ConfigureFootsteps(PlayerFootstepAudio footstepAudio) => footsteps = footstepAudio;
        public PocAutoWorld AutoWorld => autoWorld;

        // Phone rotation guide: no input, no AUTO, no play time (the world clock keeps running).
        public bool Suspended
        {
            get => suspended;
            set
            {
                suspended = value;
                RefreshInputSuspension();
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
                crowChoices = new CrowChoicePage(crow, menu, hud) { FastTravel = fastTravel };
                if (menu != null) crow.OpenChoices = () => crowChoices.Open(Time.unscaledTime);
                if (views != null) crow.IsInView = views.IsInView;
            }
            autoWorld = new PocAutoWorld(session, crow, hud, this);
            if (autoMode != null) autoMode.UseWorld(autoWorld);
            viewItem = new ViewMenuItem(views);
            if (menu != null)
            {
                // Full screen is a button on the game's page, not a menu row (feedback after RC1).
                menu.SetExtraItem(viewItem);
                // ver3.1: the same row as a button at the top right; it names the view it goes to.
                menu.SetQuickItem(viewItem, () => views != null && views.Mode == PocViewMode.ThirdPerson3D ? "2Dへ" : "3Dへ");
            }
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
        private void OnStepStarted(Vector2Int cell)
        {
            LastStepByAuto = autoWorld != null && autoWorld.InStep;
            if (footsteps != null && footsteps.isActiveAndEnabled) footsteps.PlayStep();
        }
        // ver3.4: while the crow's choices are open, its "カァ。" is in the choice window instead.
        private void OnMessageShown(string text)
        {
            if (hud == null || crow != null && crow.ChoiceOpen && text == PocCrow.TalkMessage) return;
            hud.ShowMessage(text);
        }

        // ver3.4: the menu closed without a choice (a touch or click outside it): やめる.
        private void Update()
        {
            if (crowChoices != null) crowChoices.Watch(Time.unscaledTime);
        }

        // Indoors the outdoor crow is switched off (and frees its cell); back outdoors it is
        // rebuilt from the map's crow_main spawn, exactly like MapRuntimeLoader2D.Build.
        // ver3.4: a companion crow is not rebuilt: it waits on the ground beside the door (still
        // switched off while she is in) and is there when she comes out. A game resumed with a
        // companion starts it beside her outdoors, or beside the door when she is inside.
        private void ActivateCrowFor(SharedWorldModel world)
        {
            if (crow == null || world == null) return;
            var outdoors = world == session.OutdoorWorld;
            var now = Time.time;
            if (!outdoors)
            {
                if (crow.Following)
                {
                    if (!crow.HasPlace)
                    {
                        crow.gameObject.SetActive(true);
                        crow.ApplySpawnFrom(session.OutdoorWorld.Map);
                    }
                    if (crow.gameObject.activeSelf) crow.WaitOutside(now);
                }
                crow.gameObject.SetActive(false);
                return;
            }
            crow.gameObject.SetActive(true);
            if (crow.Following && crow.HasPlace)
            {
                crow.ReturnFromWaiting(now);
                return;
            }
            if (!crow.ApplySpawnFrom(world.Map)) { crow.gameObject.SetActive(false); return; }
            if (crow.Following) crow.JoinNear(now);
        }

        // ver3.4: a mode set after the start (an import): the companion joins her now.
        public void RefreshCrowAfterRestore()
        {
            if (crow == null || session.World != session.OutdoorWorld || !crow.isActiveAndEnabled) return;
            if (crow.Following && crow.DistanceToPlayer > PocCrow.FlyDistance) crow.JoinNear(Time.time);
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

    // ver3.4: the crow's choices, a page of the game's menu panel (its window, its font, its touch
    // and click rules, the blocked game input and AUTO's pause while it is open): 「カァ。」 and
    // ついてきて / あそんでおいで (only the one that changes something), and やめる.
    // ver3.5: そらをとぶ between them (the destination list replaces this page in the open menu).
    public sealed class CrowChoicePage : IGameMenuPage
    {
        private readonly PocCrow crow;
        private readonly GameMenuController menu;
        private readonly GameHud hud;
        private readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string> { PocCrow.ChoiceLine };
        private readonly System.Collections.Generic.List<string> buttons = new System.Collections.Generic.List<string>();
        private bool shown;

        public CrowChoicePage(PocCrow sharedCrow, GameMenuController gameMenu, GameHud messageHud)
        {
            crow = sharedCrow;
            menu = gameMenu;
            hud = messageHud;
        }

        public string Title => PocCrow.ChoiceTitle;
        public System.Collections.Generic.IReadOnlyList<string> Lines => lines;
        public System.Collections.Generic.IReadOnlyList<string> Buttons => buttons;
        public bool IsOpen => menu != null && menu.Page == this;
        public int Opens { get; private set; }
        public PocFastTravel FastTravel { get; set; }

        public bool Open(float now)
        {
            if (menu == null) return false;
            buttons.Clear();
            buttons.Add(crow.Following ? PocCrow.ChoicePlay : PocCrow.ChoiceFollow);
            if (FastTravel != null) buttons.Add(PocCrow.ChoiceTravel);
            buttons.Add(PocCrow.ChoiceCancel);
            if (hud != null) hud.ClearMessage();
            if (menu.IsOpen) menu.SetOpen(false, now);
            menu.OpenPage(this, now);
            shown = IsOpen;
            if (shown) Opens++;
            return shown;
        }

        public void Press(int index, float now)
        {
            var label = index >= 0 && index < buttons.Count ? buttons[index] : PocCrow.ChoiceCancel;
            shown = false;
            if (label == PocCrow.ChoiceTravel && FastTravel != null)
            {
                crow.Choose(CrowChoice.Travel, Time.time);
                if (!FastTravel.Open(now, FastTravelSource.Crow)) menu.SetOpen(false, now);
                return;
            }
            var choice = index != 0 ? CrowChoice.Cancel : crow.Following ? CrowChoice.Play : CrowChoice.Follow;
            crow.Choose(choice, Time.time);
            menu.SetOpen(false, now);
        }

        public void Watch(float now)
        {
            if (!shown || IsOpen) return;
            shown = false;
            crow.Choose(CrowChoice.Cancel, Time.time);
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
        // ver3.5: true while AUTO's own step is being started (its StepStarted is AUTO's).
        public bool InStep { get; private set; }

        public bool TryStep(Vector2Int direction)
        {
            InStep = true;
            bool stepped;
            try { stepped = session.TryStep(direction); }
            finally { InStep = false; }
            if (stepped) AutoSteps++;
            return stepped;
        }

        public void CancelStep() => session.CancelStep();
        public bool HasActiveMessage => hud != null && hud.HasActiveMessage;
        public bool HasHouse => session.OutdoorWorld != null && session.OutdoorWorld.TryGetHouseDoor(out _);
        // ver3.2: world cells (the house door of the outdoor world, wherever its region lies).
        public Vector2Int HouseDoorCell => session.OutdoorWorld.TryGetHouseDoor(out var door) ? door : default;
        public Vector2Int OutsideEntryCell => HouseDoorCell + Vector2Int.down;
        // ver3.2: AUTO walks only where the chunk is drawn: it never heads into an undrawn chunk, and
        // its route search stays inside the drawn area instead of the whole world.
        // ver3.3: outdoors AUTO also stays in the region she stands in: a new region is found by
        // walking there herself (AUTO never wanders into it), and within it AUTO walks as before.
        public bool CanEnter(Vector2Int cell) => session.World.CanEnter(cell) && session.IsCellReady(cell) && SameRegion(cell);

        private bool SameRegion(Vector2Int cell)
        {
            var here = session.CurrentRegion;
            return here == null || session.OutdoorIndex.RegionAt(cell) == here;
        }
        public bool CanPlayerEnter(Vector2Int cell) => session.CanPlayerEnter(cell);

        public bool TryGetCrowCell(out Vector2Int cell)
        {
            var active = crow != null && crow.isActiveAndEnabled;
            cell = active ? crow.Cell : default;
            return active;
        }

        // ver3.5: a flight pauses AUTO too (its setting is not changed).
        public bool Suspended => owner != null && (owner.Suspended || owner.TravelBusy);
    }
}
