using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D.Save
{
    // はるかわーるど Phase 6: the one place that saves and loads.
    // - Automatic: no save or load button. On start the newest good snapshot is loaded (or the
    //   ver2.9 keys are taken over), the place checked, and play resumes where it ended. While
    //   playing, the state is written after a completed step (at most every 3 s), at once (1 s)
    //   after a map change or a setting change, every 25 s for the play time, and right away
    //   when the page is hidden or loses focus. Nothing is written every frame.
    // - Safe: three generations of snapshots (HarukaSnapshotStore); damaged data is never
    //   replaced by an empty new game; a newer schema is never overwritten; a second tab that
    //   saved later stops this tab's saving.
    // - Compatible: HALKA_WORLD_life_log_v1 ("v1|steps|seconds") and
    //   HALKA_WORLD_time_cycle_anchor_v1 keep being written with every save, so ver2.9 still
    //   reads them after a rollback. The snapshot is the authority, except that a Life Log key
    //   further ahead (played in ver2.9 after a rollback) is taken over: steps never go back.
    // - Export / Import: the menu's セーブデータ page (UTF-8 JSON files, checked before anything
    //   changes, a recovery copy of the current state first).
    [DefaultExecutionOrder(-45)]
    public sealed class HarukaSaveSystem : MonoBehaviour
    {
        public const string ObjectName = "HarukaSaveSystem";
        public const string FolderName = "harukaworld_save";
        public const float ImportantSaveSeconds = 1f;
        public const float StepSaveSeconds = 3f;
        public const float PeriodicSaveSeconds = 25f;
        public const string MenuLabel = "セーブデータ";
        public const string CloseLabel = "やめる";
        public const string MessageRecovered = "セーブデータを ふくげんしました。";
        public const string MessageUnreadable = "セーブデータを よみこめませんでした。";
        public const string MessageNewer = "あたらしい ばんの セーブデータです。この ばんでは セーブしません。";
        public const string MessageOtherTab = "べつの タブで セーブされました。この タブでは セーブしません。";

        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private AutoModeController autoMode;
        [SerializeField] private LifeLogController lifeLog;
        [SerializeField] private WorldTimeController worldTime;
        [SerializeField] private GameMenuController menu;
        [SerializeField] private GameHud hud;
        [SerializeField] private string gameVersion = "ver3.0";
        // ver3.4: the crow whose companion mode is saved (none: always "free", as before).
        [SerializeField] private PocCrow crow;
        [SerializeField] private PocGameplay gameplay;
        // ver3.5: the places found for そらをとぶ (none: whatever was loaded is written back as it was).
        [SerializeField] private PocFastTravel fastTravel;

        public HarukaSnapshotStore Store { get; private set; }
        public IHarukaSaveBrowser Browser { get; private set; }
        public HarukaSaveData Loaded { get; private set; }
        public HarukaSaveData LastWritten { get; private set; }
        public string LoadSource { get; private set; } = "";
        public string LocationNote { get; private set; } = "";
        public string PendingMessage { get; private set; }
        public bool SavingStopped { get; private set; }
        public string StopReason { get; private set; } = "";
        public int Writes { get; private set; }
        public int FailedWrites { get; private set; }
        public long KnownRevision { get; private set; }
        public string LastWriteReason { get; private set; } = "";
        public string GameVersion => gameVersion;
        public bool Booted => booted;
        public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        private bool booted;
        private bool started;
        private bool firstSaveDue;
        private float lastWriteAt = -100f;
        private State saved;
        private long savedPlaySeconds = -1;
        private bool flushRequested;
        private float startedAt;
        private int messageShows;
        private SaveMenu menuPages;
        private SaveMenu saveMenu => menuPages ??= new SaveMenu(this);

        public void Configure(SharedWorldSession worldSession, PocViewSwitcher viewSwitcher, AutoModeController auto,
            LifeLogController log, WorldTimeController time, GameMenuController gameMenu, GameHud messageHud, string version)
        {
            session = worldSession;
            views = viewSwitcher;
            autoMode = auto;
            lifeLog = log;
            worldTime = time;
            menu = gameMenu;
            hud = messageHud;
            gameVersion = version;
        }

        public void ConfigureCrow(PocCrow sharedCrow, PocGameplay sharedGameplay)
        {
            crow = sharedCrow;
            gameplay = sharedGameplay;
        }

        // ver3.5: the found places are saved with the rest; during a flight the place she left is
        // what a save holds, and once she has landed the destination is saved at once.
        public void ConfigureFastTravel(PocFastTravel travel)
        {
            if (fastTravel != null) fastTravel.Completed -= OnTravelCompleted;
            fastTravel = travel;
            if (fastTravel != null) fastTravel.Completed += OnTravelCompleted;
        }

        private void OnEnable()
        {
            if (fastTravel != null) { fastTravel.Completed -= OnTravelCompleted; fastTravel.Completed += OnTravelCompleted; }
        }

        private void OnDisable()
        {
            if (fastTravel != null) fastTravel.Completed -= OnTravelCompleted;
        }

        private void OnTravelCompleted(bool landed)
        {
            if (landed && started && !SavingStopped) Write("fast travel", Time.unscaledTime);
        }

        private List<string> FastTravelNow() =>
            fastTravel != null ? fastTravel.SavedIds() : Loaded?.FastTravel != null ? new List<string>(Loaded.FastTravel) : null;

        public static bool SameIds(IList<string> a, IList<string> b)
        {
            var countA = a != null ? a.Count : 0;
            var countB = b != null ? b.Count : 0;
            if (countA != countB) return false;
            for (var i = 0; i < countA; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public static CrowCompanionMode ToCrowMode(string saved) =>
            saved == HarukaSaveData.CrowFollow ? CrowCompanionMode.FollowPlayer : CrowCompanionMode.FreeRoam;

        private string CrowModeNow() => crow != null && crow.Following ? HarukaSaveData.CrowFollow :
            crow != null ? HarukaSaveData.CrowFree : Loaded?.CrowMode ?? HarukaSaveData.CrowFree;

        // Checks: memory files and a recording browser instead of IndexedDB and the page.
        public void UseStorage(IHarukaSaveStorage storage, IHarukaSaveBrowser browser)
        {
            Store = new HarukaSnapshotStore(storage);
            Browser = browser;
        }

        private void Awake()
        {
            if (Store == null)
                UseStorage(new HarukaFileStorage(Path.Combine(Application.persistentDataPath, FolderName)), new HarukaWebGLSaveBrowser());
            Boot();
        }

        private void Start() => StartPlay(Time.unscaledTime);

        // ---------------------------------------------------------------- loading

        // Before the session's first map: decides what to resume and sets it on the components.
        public void Boot()
        {
            if (booted) return;
            booted = true;
            var scan = Store.Scan();
            var legacyLifeText = ReadPref(LifeLogController.SaveKey);
            var legacyLife = LifeLogData.Deserialize(legacyLifeText);
            HarukaSaveData data;
            if (scan.HasNewerSchema)
            {
                Stop(MessageNewer, "a snapshot has a newer schema");
                data = scan.Latest != null ? scan.Latest.Copy() : FromLegacy(legacyLife);
                LoadSource = "newer schema present (not saving)";
                PendingMessage = MessageNewer;
            }
            else if (scan.Latest != null)
            {
                data = scan.Latest.Copy();
                LoadSource = scan.RecoveredFromBackup ? "backup snapshot" : "snapshot";
                if (scan.RecoveredFromBackup) PendingMessage = MessageRecovered;
            }
            else if (scan.CorruptCount > 0 && string.IsNullOrEmpty(legacyLifeText))
            {
                // Damaged and nothing else to go on: never start an empty game over it.
                data = FromLegacy(legacyLife);
                Stop(MessageUnreadable, "every snapshot is damaged");
                LoadSource = "unreadable (not saving until an import)";
                PendingMessage = MessageUnreadable;
            }
            else
            {
                data = FromLegacy(legacyLife);
                LoadSource = scan.CorruptCount > 0 ? "ver2.9 keys after damaged snapshots" :
                    string.IsNullOrEmpty(legacyLifeText) ? "new" : "ver2.9 keys";
                if (scan.CorruptCount > 0) PendingMessage = MessageRecovered;
                firstSaveDue = true;
            }
            // A Life Log further ahead under the old key (played in ver2.9 after a rollback).
            if (!string.IsNullOrEmpty(legacyLifeText) && (legacyLife.TotalSteps > data.TotalSteps ||
                legacyLife.TotalSteps == data.TotalSteps && legacyLife.TotalPlaySeconds > data.TotalPlaySeconds))
            {
                data.TotalSteps = legacyLife.TotalSteps;
                data.TotalPlaySeconds = Math.Max(data.TotalPlaySeconds, legacyLife.TotalPlaySeconds);
                firstSaveDue = true;
            }
            Loaded = data;
            KnownRevision = Math.Max(scan.HighestRevision, Browser.ReadRevisionMarker());
            ApplyEarly(data);
            // The developer log (browser console): what was resumed, never shown in the game.
            Debug.Log("HARUKA save: start from " + LoadSource + ": " + Describe(data) + " rev " + KnownRevision +
                " place: " + LocationNote + (SavingStopped ? " (not saving: " + StopReason + ")" : ""));
        }

        public static string Describe(HarukaSaveData d) =>
            d.MapId + " (" + d.CellX + "," + d.CellY + ") " + d.Facing + " " + d.ViewMode + " AUTO " + (d.AutoEnabled ? "on" : "off") +
            " steps " + d.TotalSteps + " play " + d.TotalPlaySeconds + "s anchor " + d.TimeAnchorUnixMs + " crow " + d.CrowMode +
            " places " + (d.HasFastTravel ? string.Join(",", d.FastTravel) : "start");

        private HarukaSaveData FromLegacy(LifeLogData legacyLife)
        {
            var start = session.StartMap;
            start.TryGetEntitySpawn("player_main", out var spawn);
            return new HarukaSaveData
            {
                GameVersion = gameVersion,
                SavedAtUtc = HarukaSaveData.UtcStamp(Clock()),
                TotalSteps = legacyLife.TotalSteps,
                TotalPlaySeconds = legacyLife.TotalPlaySeconds,
                TimeAnchorUnixMs = worldTime != null && worldTime.AnchorMilliseconds > 0 ? worldTime.AnchorMilliseconds : Clock().ToUnixTimeMilliseconds(),
                AutoEnabled = autoMode == null || autoMode.EnabledByDefault,
                ViewMode = views != null && views.InitialMode == PocViewMode.ThirdPerson3D ? HarukaSaveData.View3D : HarukaSaveData.View2D,
                MapId = start.MapId,
                CellX = spawn.Cell.x,
                CellY = spawn.Cell.y,
                Facing = HarukaSaveData.FacingName(EntityRuntimeFactory2D.ParseFacing(spawn.Facing)),
            };
        }

        private void ApplyEarly(HarukaSaveData data)
        {
            var place = ResolveLocation(data.MapId, new Vector2Int(data.CellX, data.CellY), data.Facing, null);
            LocationNote = place.Note;
            session.SetStartLocation(place.MapId, place.Cell, place.Facing);
            if (views != null)
                views.SetInitialMode(data.ViewMode == HarukaSaveData.View3D ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D);
            if (lifeLog != null && (lifeLog.TotalSteps != data.TotalSteps || lifeLog.TotalPlaySeconds != data.TotalPlaySeconds))
                lifeLog.ReplaceTotals(data.TotalSteps, data.TotalPlaySeconds);
            if (worldTime != null && worldTime.AnchorMilliseconds != data.TimeAnchorUnixMs)
                worldTime.SetAnchor(data.TimeAnchorUnixMs, Clock().ToUnixTimeMilliseconds());
            // ver3.4: before the crow is placed (PocGameplay places a companion beside her).
            if (crow != null) crow.RestoreMode(ToCrowMode(data.CrowMode));
            // ver3.5: what was found (the start places when the save has none).
            if (fastTravel != null) fastTravel.SetFound(data.FastTravel);
        }

        // After every component's Awake: AUTO's setting (it starts idle: the 60 s are counted
        // again from now, so a resumed game never walks off at once), the menu rows.
        public void StartPlay(float now)
        {
            if (started) return;
            started = true;
            startedAt = now;
            if (autoMode != null && Loaded != null && autoMode.AutoEnabled != Loaded.AutoEnabled)
                autoMode.SetAutoEnabled(Loaded.AutoEnabled, now);
            if (menu != null)
            {
                menu.AddLinkItem(MenuLabel, saveMenu.Open);
                menu.SetCloseLabel(CloseLabel);
            }
            saved = Now();
            savedPlaySeconds = lifeLog != null ? lifeLog.TotalPlaySeconds : 0;
            lastWriteAt = now - StepSaveSeconds;
            // ver3.5: a place found at the start (an old save resumed at the plaza) is saved soon.
            if (fastTravel != null && Loaded != null && !SameIds(fastTravel.SavedIds(), Loaded.FastTravel)) firstSaveDue = true;
        }

        public struct Place
        {
            public string MapId;
            public Vector2Int Cell;
            public FacingDirection Facing;
            public string Note;
        }

        // The saved place if the player can stand there now; else the nearest such cell of the
        // same map that can be reached from the map's start; else the map's start; else the first
        // map's Player Start. Doors and the room's exit are never a resume place (no transition
        // on load). occupied: a cell taken by the crow (import while playing).
        public Place ResolveLocation(string mapId, Vector2Int cell, string facingName, Func<Vector2Int, bool> occupied)
        {
            var facingOk = HarukaSaveData.TryParseFacing(facingName, out var facing);
            MapDefinition map = null;
            foreach (var candidate in session.Maps) if (candidate.MapId == mapId) map = candidate;
            var start = session.StartMap;
            // ver3.3: a start without a readable Player Start (a test world, a damaged Map) faces down
            // instead of stopping the game's start.
            var hasStart = start.TryGetEntitySpawn("player_main", out var startSpawn) && HarukaSaveData.TryParseFacing(startSpawn.Facing, out _);
            var startPlace = new Place { MapId = start.MapId, Cell = startSpawn.Cell,
                Facing = hasStart ? EntityRuntimeFactory2D.ParseFacing(startSpawn.Facing) : FacingDirection.Down };
            if (map == null) { startPlace.Note = "unknown map " + mapId + ": the first map's start"; return startPlace; }
            // ver3.2: an outdoor Map is checked inside the outdoor world (world cells: the regions
            // around it count too); every answer goes back to Map ID + Map cell (Save Schema 1).
            SharedWorldModel model;
            Vector2Int target;
            if (session.TryToSessionCell(mapId, cell, out var world, out var worldCell) && world.IsOutdoorWorld)
            {
                model = world;
                target = worldCell;
            }
            else
            {
                model = new SharedWorldModel(map);
                target = cell;
            }
            bool Safe(Vector2Int c) => model.CanEnter(c) && !IsTransitionCell(model, c) && (occupied == null || !occupied(c));
            var note = facingOk ? "" : "facing reset; ";
            if (Safe(target)) return ToPlace(model, target, facing, note + "saved cell");
            var origin = MapStart(model, map, out var originFacing);
            var reachable = Reachable(model, origin, Safe);
            var best = default(Vector2Int);
            var bestDistance = int.MaxValue;
            // ver3.3: outdoors the saved Map's own region comes first (a bad cell of first_field comes
            // back inside first_field, not in the next region); other regions only if it has none.
            var home = model.IsOutdoorWorld ? model.OutdoorIndex.FindRegion(mapId) : null;
            for (var pass = home != null ? 0 : 1; pass < 2 && bestDistance == int.MaxValue; pass++)
                foreach (var c in reachable)
                {
                    if (pass == 0 && !home.ContainsWorld(c)) continue;
                    var d = Mathf.Abs(c.x - target.x) + Mathf.Abs(c.y - target.y);
                    if (d < bestDistance || d == bestDistance && (c.y > best.y || c.y == best.y && c.x < best.x)) { best = c; bestDistance = d; }
                }
            if (bestDistance != int.MaxValue)
                return ToPlace(model, best, facing, note + "nearest safe cell (saved cell not usable)");
            if (Safe(origin)) return ToPlace(model, origin, originFacing, note + "the map's start");
            startPlace.Note = note + "the first map's start";
            return startPlace;
        }

        // A model cell back to what a save holds: the region's Map and its cell outdoors.
        private static Place ToPlace(SharedWorldModel model, Vector2Int cell, FacingDirection facing, string note)
        {
            var region = model.IsOutdoorWorld ? model.OutdoorIndex.RegionAt(cell) : null;
            return new Place
            {
                MapId = region != null ? region.MapId : model.MapId,
                Cell = region != null ? region.ToLocal(cell) : cell,
                Facing = facing,
                Note = note
            };
        }

        public static bool IsTransitionCell(SharedWorldModel model, Vector2Int cell) =>
            model.TryGetHouseDoor(out var door) && door == cell ||
            model.TryGetMarker("interior_exit", out var exit) && exit == cell;

        // The Map's start in the model's cells (world cells for an outdoor region).
        private static Vector2Int MapStart(SharedWorldModel model, MapDefinition map, out FacingDirection facing)
        {
            facing = FacingDirection.Down;
            var region = model.IsOutdoorWorld ? model.OutdoorIndex.FindRegion(map.MapId) : null;
            if (map.TryGetEntitySpawn("player_main", out var spawn))
            {
                facing = EntityRuntimeFactory2D.ParseFacing(spawn.Facing);
                return region != null ? region.ToWorld(spawn.Cell) : spawn.Cell;
            }
            if (!model.IsOutdoorWorld && model.TryGetMarker("interior_entry", out var entry)) { facing = FacingDirection.Up; return entry; }
            return region != null ? region.Rect.Min : model.MinCell;
        }

        private static List<Vector2Int> Reachable(SharedWorldModel model, Vector2Int origin, Func<Vector2Int, bool> safe)
        {
            var result = new List<Vector2Int>();
            if (!model.CanEnter(origin)) return result;
            var seen = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);
            var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (safe(c)) result.Add(c);
                foreach (var s in steps)
                {
                    var n = c + s;
                    // Walk through cells one can stand on, never through a door (another map).
                    if (seen.Contains(n) || !model.CanEnter(n) || IsTransitionCell(model, n)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return result;
        }

        // ---------------------------------------------------------------- saving

        // What a save holds, compared field by field every frame (nothing allocated per frame).
        private struct State
        {
            public string Map;
            public Vector2Int Cell;
            public FacingDirection Facing;
            public long Steps;
            public PocViewMode View;
            public bool Auto;
            public string Crow;
            public int Places;

            public bool SameSettings(State other) => View == other.View && Auto == other.Auto && Crow == other.Crow &&
                Places == other.Places;
            public bool Same(State other) => SameSettings(other) && Map == other.Map && Cell == other.Cell &&
                Facing == other.Facing && Steps == other.Steps;
        }

        private State Now() => new State
        {
            Map = SavePlace(out var nowCell),
            Cell = nowCell,
            Facing = SaveFacing(),
            Steps = lifeLog != null ? lifeLog.TotalSteps : 0,
            View = views != null ? views.Mode : PocViewMode.PixelView2D,
            Auto = autoMode != null && autoMode.AutoEnabled,
            Crow = CrowModeNow(),
            Places = fastTravel != null ? fastTravel.DiscoveryVersion : 0,
        };

        public HarukaSaveData Capture()
        {
            return new HarukaSaveData
            {
                GameVersion = gameVersion,
                SavedAtUtc = HarukaSaveData.UtcStamp(Clock()),
                TotalSteps = lifeLog != null ? lifeLog.TotalSteps : Loaded?.TotalSteps ?? 0,
                TotalPlaySeconds = lifeLog != null ? lifeLog.TotalPlaySeconds : Loaded?.TotalPlaySeconds ?? 0,
                TimeAnchorUnixMs = worldTime != null && worldTime.AnchorMilliseconds > 0 ? worldTime.AnchorMilliseconds : Loaded?.TimeAnchorUnixMs ?? Clock().ToUnixTimeMilliseconds(),
                AutoEnabled = autoMode != null ? autoMode.AutoEnabled : Loaded?.AutoEnabled ?? true,
                ViewMode = views != null && views.Mode == PocViewMode.ThirdPerson3D ? HarukaSaveData.View3D : HarukaSaveData.View2D,
                // The logical cell: during a step this is still the cell it started from.
                // ver3.2: outdoors, the region's Map and the cell in it (Schema 1, as before).
                MapId = SavePlace(out var placeCell),
                CellX = placeCell.x,
                CellY = placeCell.y,
                Facing = HarukaSaveData.FacingName(SaveFacing()),
                CrowMode = CrowModeNow(),
                FastTravel = FastTravelNow(),
            };
        }

        private string SavePlace(out Vector2Int mapCell)
        {
            if (fastTravel != null && fastTravel.TryGetHeldPlace(out var heldMap, out mapCell, out _)) return heldMap;
            if (session.TryGetSavePlace(out var mapId, out mapCell)) return mapId;
            mapCell = session.Cell;
            return session.World.MapId;
        }

        private FacingDirection SaveFacing() =>
            fastTravel != null && fastTravel.TryGetHeldPlace(out _, out _, out var heldFacing) ? heldFacing : session.Facing;

        private void LateUpdate() => Tick(Time.unscaledTime);

        // Called every frame (and by the checks with their own clock).
        public void Tick(float now)
        {
            saveMenu.Update(now);
            if (!started) return;
            // A save notice stays about 6 s (three 2 s messages), from half a second after start.
            if (PendingMessage != null && hud != null && !hud.HasActiveMessage && now - startedAt >= 0.5f)
            {
                hud.ShowMessage(PendingMessage);
                if (++messageShows >= 3) { PendingMessage = null; messageShows = 0; }
            }
            if (SavingStopped) return;
            var since = now - lastWriteAt;
            var state = Now();
            var playSeconds = lifeLog != null ? lifeLog.TotalPlaySeconds : 0;
            if (flushRequested && (!state.Same(saved) || playSeconds != savedPlaySeconds || firstSaveDue))
                Write("page hidden or paused", now);
            // During a step the place is not final yet (its facing already changed): wait for
            // the step to complete (0.18 s), unless the page is going away.
            else if (session.IsMoving) { }
            else if ((firstSaveDue || !state.SameSettings(saved) || state.Map != saved.Map) && since >= ImportantSaveSeconds)
                Write(firstSaveDue ? "first save" : "map or setting changed", now);
            else if (!state.Same(saved) && since >= StepSaveSeconds)
                Write("moved", now);
            else if (playSeconds != savedPlaySeconds && since >= PeriodicSaveSeconds)
                Write("play time", now);
            flushRequested = false;
        }

        // From the page (HarukaSave.jslib: tab hidden, page closing) and from Unity (paused,
        // focus lost): save now, before the browser may freeze or close the page.
        public void OnPageHidden(string _) => Flush();
        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Flush(); }

        public void Flush()
        {
            if (!started || SavingStopped) return;
            flushRequested = true;
            Tick(Time.unscaledTime);
        }

        public bool Write(string reason, float now) => WriteData(Capture(), reason, now) != null;

        private HarukaSaveData WriteData(HarukaSaveData data, string reason, float now)
        {
            if (SavingStopped) return null;
            var marker = Browser.ReadRevisionMarker();
            if (marker > KnownRevision)
            {
                Stop(MessageOtherTab, "another tab saved revision " + marker);
                PendingMessage = MessageOtherTab;
                messageShows = 0;
                return null;
            }
            var written = Store.Write(data, KnownRevision + 1);
            if (written == null)
            {
                FailedWrites++;
                Debug.LogWarning("HARUKA save: not written (" + reason + "): " + Store.LastProblem);
                lastWriteAt = now; // try again after the interval, not every frame
                return null;
            }
            KnownRevision = written.Revision;
            Browser.WriteRevisionMarker(KnownRevision);
            try
            {
                PlayerPrefs.SetString(LifeLogController.SaveKey, "v1|" + written.TotalSteps.ToString(CultureInfo.InvariantCulture) +
                    "|" + written.TotalPlaySeconds.ToString(CultureInfo.InvariantCulture));
                PlayerPrefs.SetString(WorldTimeController.SaveKey, written.TimeAnchorUnixMs.ToString(CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
            }
            catch (Exception exception) { Debug.LogWarning("HARUKA save: old keys not written: " + exception.Message); }
            Browser.Sync();
            LastWritten = written;
            Writes++;
            LastWriteReason = reason;
            lastWriteAt = now;
            firstSaveDue = false;
            saved = Now();
            savedPlaySeconds = written.TotalPlaySeconds;
            return written;
        }

        private void Stop(string message, string reason)
        {
            SavingStopped = true;
            StopReason = reason;
            // Phase 7: the Life Log's own writes of the old key stop too. Otherwise its totals would
            // appear under the old key, and the next start would take them for ver2.9 data and begin
            // an almost empty save over the damaged ones.
            if (lifeLog != null) lifeLog.PersistenceSuspended = true;
            Debug.LogWarning("HARUKA save: saving stopped: " + reason);
        }

        private static string ReadPref(string key)
        {
            try { return PlayerPrefs.GetString(key, ""); }
            catch (Exception) { return ""; }
        }

        // ---------------------------------------------------------------- export and import

        public enum ImportCheck { Ok, Empty, TooLarge, Unreadable, NewerSchema, UnknownMap, OutOfMap }

        // Everything an imported file must pass before anything changes.
        public ImportCheck CheckImport(string text, out HarukaSaveData data)
        {
            data = null;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text.Trim())) return ImportCheck.Empty;
            if (System.Text.Encoding.UTF8.GetByteCount(text) > HarukaSaveData.MaxFileBytes) return ImportCheck.TooLarge;
            var status = HarukaSaveData.TryRead(text, out data, out _);
            if (status == HarukaSaveReadStatus.NewerSchema) return ImportCheck.NewerSchema;
            if (status != HarukaSaveReadStatus.Valid) return ImportCheck.Unreadable;
            MapDefinition map = null;
            foreach (var candidate in session.Maps) if (candidate.MapId == data.MapId) map = candidate;
            if (map == null) return ImportCheck.UnknownMap;
            if (!map.Contains(new Vector2Int(data.CellX, data.CellY))) return ImportCheck.OutOfMap;
            return ImportCheck.Ok;
        }

        // The newest good snapshot as a file (written first, so it holds the current state).
        public string ExportText()
        {
            if (!SavingStopped && (firstSaveDue || !Now().Same(saved) ||
                (lifeLog != null ? lifeLog.TotalPlaySeconds : 0) != savedPlaySeconds))
                Write("export", Time.unscaledTime);
            var latest = Store.Scan().Latest;
            return latest != null ? latest.ToJson() : null;
        }

        // Replaces the save with checked data: a recovery copy of the current state, the new
        // snapshot, then the world. Returns false (and changes nothing in play) on any failure.
        public bool ApplyImport(HarukaSaveData imported, float now, out string problem)
        {
            problem = null;
            if (SavingStopped && StopReason.StartsWith("another tab", StringComparison.Ordinal))
            {
                problem = "another tab";
                return false;
            }
            if (autoMode != null) autoMode.RecordUserAction(now); // stops AUTO's walk if any
            var current = Capture();
            if (!Store.WriteRecovery(current)) { problem = "recovery copy not written"; return false; }
            var place = ResolveLocation(imported.MapId, new Vector2Int(imported.CellX, imported.CellY), imported.Facing,
                c => !session.CanPlayerEnter(c));
            var data = imported.Copy();
            data.GameVersion = gameVersion;
            data.SavedAtUtc = HarukaSaveData.UtcStamp(Clock());
            data.MapId = place.MapId;
            data.CellX = place.Cell.x;
            data.CellY = place.Cell.y;
            data.Facing = HarukaSaveData.FacingName(place.Facing);
            // An import also ends a stop caused by unreadable data (the person chose this file).
            var wasStopped = SavingStopped;
            if (StopReason == "every snapshot is damaged")
            {
                SavingStopped = false;
                StopReason = "";
                if (lifeLog != null) lifeLog.PersistenceSuspended = false;
            }
            var written = SavingStopped ? null : WriteData(data, "import", now);
            if (written == null)
            {
                if (wasStopped && !SavingStopped)
                {
                    SavingStopped = true;
                    StopReason = "every snapshot is damaged";
                    if (lifeLog != null) lifeLog.PersistenceSuspended = true;
                }
                problem = "not written: " + Store.LastProblem;
                return false;
            }
            if (lifeLog != null) lifeLog.ReplaceTotals(written.TotalSteps, written.TotalPlaySeconds);
            if (worldTime != null) worldTime.SetAnchor(written.TimeAnchorUnixMs, Clock().ToUnixTimeMilliseconds());
            if (autoMode != null) autoMode.SetAutoEnabled(written.AutoEnabled, now);
            if (views != null)
                views.SetMode(written.ViewMode == HarukaSaveData.View3D ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D);
            session.RestoreLocationNow(place.MapId, place.Cell, place.Facing);
            if (crow != null)
            {
                crow.SetMode(ToCrowMode(written.CrowMode), Time.time);
                if (gameplay != null) gameplay.RefreshCrowAfterRestore();
            }
            if (fastTravel != null) fastTravel.SetFound(written.FastTravel);
            if (views != null)
            {
                if (views.PixelView != null) views.PixelView.SnapToTarget();
                if (views.ThirdPerson != null) views.ThirdPerson.SnapToTarget();
            }
            LocationNote = place.Note;
            Loaded = written;
            Debug.Log("HARUKA save: imported: " + Describe(written) + " rev " + written.Revision + " place: " + place.Note);
            var after = Capture();
            saved = Now();
            if (after.MapId != written.MapId || after.CellX != written.CellX || after.CellY != written.CellY ||
                after.Facing != written.Facing || after.ViewMode != written.ViewMode || after.AutoEnabled != written.AutoEnabled ||
                after.TotalSteps != written.TotalSteps || after.TimeAnchorUnixMs != written.TimeAnchorUnixMs ||
                after.CrowMode != written.CrowMode || fastTravel != null && !SameIds(after.FastTravel, Normalized(written.FastTravel)))
            {
                problem = "the world does not match the imported data";
                return false;
            }
            return true;
        }

        // The ids as the game writes them back: with the start places, sorted, once each; the start
        // places alone are written as none.
        private List<string> Normalized(List<string> ids)
        {
            if (fastTravel == null) return ids;
            var set = new SortedSet<string>(StringComparer.Ordinal);
            var beyondStart = false;
            foreach (var point in fastTravel.Registry.points)
                if (point != null && point.unlockedAtStart) set.Add(point.id);
            if (ids != null)
                foreach (var id in ids)
                {
                    if (!HarukaSaveData.ValidPlaceId(id)) continue;
                    set.Add(id);
                    var point = fastTravel.Registry.Find(id);
                    if (point == null || !point.unlockedAtStart) beyondStart = true;
                }
            return beyondStart ? new List<string>(set) : null;
        }

        public SaveMenu Menu => saveMenu;

        // The セーブデータ page and its steps (shown inside the game's menu panel).
        // The place a save names, as the menu shows it: the rooms, そと for first_field, and an outdoor
        // region of a direction by its direction's name (ver3.3: east_area_01 -> ひがし).
        public string PlaceName(string mapId)
        {
            var known = SaveMenu.PlaceName(mapId);
            if (known != mapId) return known;
            var region = session != null ? session.OutdoorIndex?.FindRegion(mapId) : null;
            var theme = region != null ? session.OutdoorIndex.Definition.ThemeOf(region.Direction) : null;
            return theme != null ? theme.name : mapId;
        }

        public sealed class SaveMenu : IGameMenuPage
        {
            public enum Stage { Main, ExportWaiting, ExportDone, ExportFailed, ImportWaiting, ImportConfirm, ImportDone, ImportFailed }

            private readonly HarukaSaveSystem owner;
            private readonly List<string> lines = new List<string>();
            private readonly List<string> buttons = new List<string>();
            private float stageSince;
            private string exportJson;
            private Rect armedExport;
            private Rect armedImport;

            public SaveMenu(HarukaSaveSystem system) => owner = system;

            // The rooms and first_field by name (the regions of the outdoor world: owner.PlaceName).
            public static string PlaceName(string mapId) => mapId == "halka_house" ? "家" : mapId == "first_field" ? "そと" :
                mapId == "halka_soundbooth" ? "ぼうおんしつ" : mapId;
            public Stage Current { get; private set; }
            public HarukaSaveData PendingImport { get; private set; }
            public string LastProblem { get; private set; } = "";
            public string Title => Current == Stage.ImportConfirm || Current == Stage.ImportDone || Current == Stage.ImportFailed ||
                Current == Stage.ImportWaiting ? "インポート" : Current == Stage.Main ? MenuLabel : "エクスポート";
            public IReadOnlyList<string> Lines => lines;
            public IReadOnlyList<string> Buttons => buttons;
            public bool IsOpen => owner.menu != null && owner.menu.Page == this;

            public void Open(float now)
            {
                Show(Stage.Main, now);
                if (owner.menu != null) owner.menu.OpenPage(this, now);
            }

            private void Show(Stage stage, float now)
            {
                Current = stage;
                stageSince = now;
                lines.Clear();
                buttons.Clear();
                switch (stage)
                {
                    case Stage.Main:
                        lines.Add(owner.SavingStopped ? "いまは セーブしていません。" : "じどうで セーブしています。");
                        lines.Add("ファイルに かきだして");
                        lines.Add("べつの ばしょに ほぞんできます。");
                        buttons.Add("エクスポート");
                        buttons.Add("インポート");
                        buttons.Add("もどる");
                        exportJson = owner.ExportText();
                        break;
                    case Stage.ExportWaiting:
                        lines.Add("かきだしています…");
                        buttons.Add("もどる");
                        break;
                    case Stage.ExportDone:
                        lines.Add("ファイルを かきだしました。");
                        lines.Add("ダウンロードを たしかめてください。");
                        buttons.Add("もどる");
                        break;
                    case Stage.ExportFailed:
                        lines.Add("かきだせませんでした。");
                        buttons.Add("もどる");
                        break;
                    case Stage.ImportWaiting:
                        lines.Add("ファイルを えらんでください。");
                        buttons.Add("もどる");
                        break;
                    case Stage.ImportConfirm:
                        var d = PendingImport;
                        lines.Add("ほぞんしたひ " + LocalStamp(d.SavedAtUtc));
                        lines.Add("あるいたかず " + d.TotalSteps.ToString("N0", CultureInfo.InvariantCulture));
                        lines.Add("プレイじかん " + PlayTime(d.TotalPlaySeconds));
                        lines.Add("ばしょ " + owner.PlaceName(d.MapId) + "  ひょうじ " + d.ViewMode);
                        lines.Add("いまの セーブデータを");
                        lines.Add("おきかえますか？");
                        buttons.Add("ふくげんする");
                        buttons.Add("やめる");
                        break;
                    case Stage.ImportDone:
                        lines.Add(MessageRecovered);
                        buttons.Add("もどる");
                        break;
                    case Stage.ImportFailed:
                        lines.Add(LastProblem);
                        lines.Add("いまの セーブデータは そのままです。");
                        buttons.Add("もどる");
                        break;
                }
            }

            public void Press(int index, float now)
            {
                if (index < 0 || index >= buttons.Count) return;
                var label = buttons[index];
                switch (Current)
                {
                    case Stage.Main when label == "エクスポート":
                        if (string.IsNullOrEmpty(exportJson)) { Show(Stage.ExportFailed, now); return; }
                        owner.Browser.ExportPressed();
                        Show(Stage.ExportWaiting, now);
                        return;
                    case Stage.Main when label == "インポート":
                        owner.Browser.ImportPressed();
                        Show(Stage.ImportWaiting, now);
                        return;
                    case Stage.ImportConfirm when label == "ふくげんする":
                        if (owner.ApplyImport(PendingImport, now, out var problem)) Show(Stage.ImportDone, now);
                        else { LastProblem = "ふくげんできませんでした。"; Debug.LogWarning("HARUKA import: " + problem); Show(Stage.ImportFailed, now); }
                        PendingImport = null;
                        return;
                    case Stage.ImportConfirm:
                        PendingImport = null;
                        Show(Stage.Main, now);
                        return;
                    case Stage.ExportWaiting:
                    case Stage.ImportWaiting:
                        owner.Browser.ResetImport();
                        Show(Stage.Main, now);
                        return;
                    case Stage.Main:
                        if (owner.menu != null) owner.menu.ClosePage(now);
                        return;
                    default:
                        Show(Stage.Main, now);
                        return;
                }
            }

            // Every frame: keep the page told where the buttons are, and follow the browser.
            public void Update(float now)
            {
                if (owner.Browser == null) return;
                var open = IsOpen;
                var exportRect = default(Rect);
                var importRect = default(Rect);
                if (open && Current == Stage.Main && owner.menu.TryGetPageButtonRect(0, out var e)) exportRect = e;
                if (open && Current == Stage.Main && owner.menu.TryGetPageButtonRect(1, out var i)) importRect = i;
                if (exportRect != armedExport) { owner.Browser.ArmExport(exportJson, exportRect); armedExport = exportRect; }
                if (importRect != armedImport) { owner.Browser.ArmImport(importRect); armedImport = importRect; }
                if (Current == Stage.ExportWaiting)
                {
                    var result = owner.Browser.TakeExportResult();
                    if (result == 1) Show(Stage.ExportDone, now);
                    else if (result == 2 || now - stageSince > 3f) Show(Stage.ExportFailed, now);
                }
                if (Current == Stage.ImportWaiting)
                {
                    var state = owner.Browser.ImportState;
                    if (state == HarukaImportState.Cancelled) { owner.Browser.ResetImport(); Show(Stage.Main, now); }
                    else if (state == HarukaImportState.Failed)
                    {
                        owner.Browser.ResetImport();
                        LastProblem = "ファイルを よめませんでした。";
                        Show(Stage.ImportFailed, now);
                    }
                    else if (state == HarukaImportState.Ready) Received(owner.Browser.TakeImportText(), now);
                }
            }

            public void Received(string text, float now)
            {
                var check = owner.CheckImport(text, out var data);
                if (check == ImportCheck.Ok) { PendingImport = data; Show(Stage.ImportConfirm, now); return; }
                LastProblem = check switch
                {
                    ImportCheck.TooLarge => "ファイルが おおきすぎます。",
                    ImportCheck.NewerSchema => "あたらしい ばんの ファイルです。",
                    ImportCheck.Empty => "ファイルが からっぽです。",
                    _ => "この ファイルは よみこめません。",
                };
                Debug.LogWarning("HARUKA import rejected: " + check);
                Show(Stage.ImportFailed, now);
            }

            private static string PlayTime(long seconds) =>
                (seconds / 3600).ToString(CultureInfo.InvariantCulture) + ":" + (seconds / 60 % 60).ToString("D2", CultureInfo.InvariantCulture) +
                ":" + (seconds % 60).ToString("D2", CultureInfo.InvariantCulture);



            private static string LocalStamp(string utc)
            {
                if (!DateTimeOffset.TryParseExact(utc, "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var time)) return utc;
                var local = time.ToOffset(TimeSpan.FromMinutes(HarukaTimeZone.OffsetMinutes()));
                return local.ToString("yyyy'/'MM'/'dd' 'HH':'mm", CultureInfo.InvariantCulture);
            }
        }
    }

    // The device's offset from UTC (the browser's in WebGL, where .NET's local time may be UTC).
    public static class HarukaTimeZone
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern int HarukaSaveTimezoneOffsetMinutes();
        public static int OffsetMinutes() => HarukaSaveTimezoneOffsetMinutes();
#else
        public static int OffsetMinutes() => (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
#endif
    }
}
