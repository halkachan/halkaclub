using System;
using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.5: where a flight is (PreparingDestination is the one frame of checks before it starts).
    public enum FastTravelPhase { Idle, PreparingDestination, DepartureAnimation, Transferring, ArrivalAnimation, Complete }

    public enum FastTravelSource { Crow, Menu, Check }

    // ver3.5: why a flight did not start (Started: it did).
    public enum FastTravelStart
    {
        Started, Busy, UnknownPoint, NotDiscovered, NotOutdoors, AlreadyHere, UnknownWorld, UnknownRegion,
        BadArrival, NoChunk, NoLandingCell
    }

    // ver3.5 「そらをとぶ」: the crow flies her to a place she has found on foot.
    // - Places come from FastTravelRegistry (fast_travel_points.json); what she has found is a set of
    //   stable ids (saved as extensions.fastTravel). The house is found from the start; the others by
    //   walking (not AUTO) within a point's discover radius, once, with a short message.
    // - It is asked for from the crow's choices (そらをとぶ) or from the menu (outdoors only, wherever
    //   the crow is); the destination list is a page of the game's menu (its window, font, touch and
    //   click rules), only places found, a page of four at a time.
    // - A flight: the checks (PreparingDestination), the crow comes to her and they rise while the
    //   screen turns to sky (DepartureAnimation), behind the sky the destination's chunks are built and
    //   she is put on the landing cell (Transferring), the sky clears over the destination and they come
    //   down (ArrivalAnimation). About 2.4 s. Input, AUTO, talking and the menu wait meanwhile.
    // - Not a walk: no step is counted, no door can fire (a landing cell is never a door), the outdoor
    //   world stays loaded (no Scene or map load), the day/night clock runs on. Walking from region to
    //   region stays exactly as before; only this flight snaps the cameras, behind the sky.
    // - A flight that cannot finish (the destination's chunk could not be built) leaves her where she
    //   was, with everything as it was, and says so.
    // - The save keeps the departure place until the landing is complete (a page closed mid-flight
    //   resumes where she left), then saves the destination at once.
    [DefaultExecutionOrder(-50)]
    public sealed class PocFastTravel : MonoBehaviour
    {
        public const string MenuLabel = "そらをとぶ";
        public const string PageTitle = "そらをとぶ";
        public const string PageLine = "どこへ とぶ？";
        public const string CancelLabel = "やめる";
        public const string NextLabel = "つぎへ";
        public const string PreviousLabel = "まえへ";
        public const string MessageFound = "そらをとべるばしょを みつけた！";
        public const string MessageHere = "もう ここに いるよ。";
        public const string MessageFailed = "いまは とべないみたい。";
        public const int PageSize = 4;
        public const int HereDistance = 3;
        public const float DepartureSeconds = 1.15f;
        public const float CoverSeconds = 0.6f;
        public const float ArrivalSeconds = 0.85f;
        public const float LiftUnits = 1.6f;

        [SerializeField] private FastTravelRegistry registry = new FastTravelRegistry();
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private PocActorView actor;
        [SerializeField] private PocGameplay gameplay;
        [SerializeField] private PocTimeOfDay timeOfDay;

        private readonly SortedSet<string> found = new SortedSet<string>(StringComparer.Ordinal);
        private DestinationPage page;
        private FastTravelPoint target;
        private Vector2Int landingCell;
        private FacingDirection landingFacing;
        private float phaseStartedAt;
        private bool flightFailed;
        private bool holdPlace;
        private string heldMapId;
        private Vector2Int heldCell;
        private FacingDirection heldFacing;
        private Vector3 crowFrom;
        private bool crowShown;
        private bool started;

        public FastTravelRegistry Registry => registry;
        public FastTravelPhase Phase { get; private set; }
        public bool Busy => Phase != FastTravelPhase.Idle;
        public FastTravelSource LastSource { get; private set; }
        public FastTravelStart LastStart { get; private set; } = FastTravelStart.Started;
        public FastTravelPoint Target => target;
        public Vector2Int LandingCell => landingCell;
        public DestinationPage Page => page ??= new DestinationPage(this);
        public int DiscoveryVersion { get; private set; }
        public IReadOnlyCollection<string> FoundIds => found;

        // The numbers of the checks and the measurements.
        public int Travels { get; private set; }
        public int Failures { get; private set; }
        public int Discoveries { get; private set; }
        public int Opens { get; private set; }
        public int LastLandingCandidates { get; private set; }
        public double LastPrepareMilliseconds { get; private set; }
        public int LastPrepareBuilt { get; private set; }
        public double LastTransferMilliseconds { get; private set; }
        public double MaxTransferMilliseconds { get; private set; }
        public bool LastArrivalVerified { get; private set; }
        public string LastArrivalProblem { get; private set; } = "";
        public long StepsAtDeparture { get; private set; }
        public float LastFlightSeconds { get; private set; }
        public Vector2 LastCrowScreen { get; private set; } = new Vector2(-1f, -1f);
        private float flightStartedAt;

        // The crow's clock (the game: Time.time; the checks run their own).
        public Func<float> CrowTime { get; set; } = () => Time.time;

        public event Action<FastTravelPoint> Discovered;
        public event Action<bool> Completed;   // true: she landed at the destination

        public void Configure(FastTravelRegistry points, SharedWorldSession worldSession, PocViewSwitcher viewSwitcher,
            PocWorldPresenter worldPresenter, PocActorView playerView, PocGameplay sharedGameplay, PocTimeOfDay time)
        {
            registry = points ?? new FastTravelRegistry();
            session = worldSession;
            views = viewSwitcher;
            presenter = worldPresenter;
            actor = playerView;
            gameplay = sharedGameplay;
            timeOfDay = time;
            ResetFound();
        }

        private void Awake()
        {
            if (found.Count == 0) ResetFound();
            if (session != null) session.StepCompleted += OnStepCompleted;
        }

        private void OnDestroy()
        {
            if (session != null) session.StepCompleted -= OnStepCompleted;
        }

        // After the save has set what was found (its Awake): the menu row, and a place she stands at
        // already (a ver3.3/3.4 save resumed at the plaza) is found now.
        private void Start() => StartPlay();

        public void StartPlay()
        {
            if (started) return;
            started = true;
            var menu = gameplay != null ? gameplay.Menu : null;
            if (menu != null) menu.AddLinkItem(MenuLabel, now => Open(now, FastTravelSource.Menu), CanOpen);
            if (session != null && session.World != null && session.World == session.OutdoorWorld) CheckDiscoveryAt(session.Cell);
        }

        // ---------------------------------------------------------------- what she has found

        private void ResetFound()
        {
            found.Clear();
            foreach (var point in registry.Ordered())
                if (point.unlockedAtStart && FastTravelRegistry.ValidId(point.id)) found.Add(point.id);
            DiscoveryVersion++;
        }

        public bool IsFound(string id) => id != null && found.Contains(id);

        // A save's ids (null: none beyond the start). Unknown well-formed ids (a later version's
        // places) are kept and written back; they are never offered as destinations.
        public void SetFound(IEnumerable<string> ids)
        {
            ResetFound();
            if (ids == null) return;
            foreach (var id in ids)
                if (FastTravelRegistry.ValidId(id)) found.Add(id);
        }

        // What a save writes: null when only the start places are found (the file is then exactly a
        // ver3.4 file), else every found id in order.
        public List<string> SavedIds()
        {
            var beyondStart = false;
            foreach (var id in found)
            {
                var point = registry.Find(id);
                if (point == null || !point.unlockedAtStart) beyondStart = true;
            }
            return beyondStart ? new List<string>(found) : null;
        }

        // The destinations she can choose: the registry's places she has found, in list order.
        public List<FastTravelPoint> FoundPoints()
        {
            var list = new List<FastTravelPoint>();
            foreach (var point in registry.Ordered())
                if (found.Contains(point.id)) list.Add(point);
            return list;
        }

        private void OnStepCompleted(Vector2Int cell)
        {
            // A place is found by walking there herself: never by an AUTO step.
            if (Busy || gameplay != null && gameplay.LastStepByAuto) return;
            if (session.World == session.OutdoorWorld) CheckDiscoveryAt(cell);
        }

        // Finds every not yet found place within its discover radius of this world cell.
        public int CheckDiscoveryAt(Vector2Int worldCell)
        {
            var index = session != null ? session.OutdoorIndex : null;
            if (index == null) return 0;
            var count = 0;
            foreach (var point in registry.Ordered())
            {
                if (found.Contains(point.id) || point.worldId != index.WorldId) continue;
                var region = index.FindRegion(point.mapId);
                if (region == null || Manhattan(worldCell, region.ToWorld(point.discoverCell)) > point.discoverRadius) continue;
                found.Add(point.id);
                DiscoveryVersion++;
                Discoveries++;
                count++;
                if (gameplay != null && gameplay.Hud != null) gameplay.Hud.ShowMessage(MessageFound);
                Discovered?.Invoke(point);
            }
            return count;
        }

        // ---------------------------------------------------------------- the destination list

        // Outdoors and not flying (the menu row is hidden otherwise).
        public bool CanOpen() =>
            !Busy && session != null && session.World != null && session.World == session.OutdoorWorld;

        public bool Open(float now, FastTravelSource source)
        {
            var menu = gameplay != null ? gameplay.Menu : null;
            if (menu == null || !CanOpen()) return false;
            LastSource = source;
            Page.Reset(FoundPoints());
            menu.OpenPage(Page, now);
            if (Page.IsOpen) Opens++;
            return Page.IsOpen;
        }

        public bool IsHere(FastTravelPoint point)
        {
            var region = session != null && session.OutdoorIndex != null ? session.OutdoorIndex.FindRegion(point.mapId) : null;
            return region != null && session.World == session.OutdoorWorld &&
                Manhattan(session.Cell, region.ToWorld(point.arrivalCell)) <= HereDistance;
        }

        private void Chosen(FastTravelPoint point, float now)
        {
            var menu = gameplay != null ? gameplay.Menu : null;
            if (menu != null) menu.SetOpen(false, now);
            var result = TryBegin(point.id, now, LastSource);
            var hud = gameplay != null ? gameplay.Hud : null;
            if (hud == null || result == FastTravelStart.Started) return;
            hud.ShowMessage(result == FastTravelStart.AlreadyHere ? MessageHere : MessageFailed);
        }

        // ---------------------------------------------------------------- a flight

        // PreparingDestination: everything is checked before anything changes; a refused flight
        // changes nothing at all.
        public FastTravelStart TryBegin(string pointId, float now, FastTravelSource source)
        {
            LastStart = Check(pointId, out var point, out var cell);
            if (LastStart != FastTravelStart.Started) return LastStart;
            LastSource = source;
            target = point;
            landingCell = cell;
            landingFacing = point.ArrivalFacing;
            flightFailed = false;
            LastArrivalVerified = false;
            LastArrivalProblem = "";
            Phase = FastTravelPhase.PreparingDestination;
            // The save keeps this place until she has landed.
            holdPlace = session.TryGetSavePlace(out heldMapId, out heldCell);
            heldFacing = session.Facing;
            StepsAtDeparture = session.CompletedSteps;
            session.Stand();
            var menu = gameplay != null ? gameplay.Menu : null;
            if (menu != null) { menu.SetOpen(false, now); menu.enabled = false; }
            if (gameplay != null)
            {
                if (gameplay.AutoMode != null) gameplay.AutoMode.RecordUserAction(now);
                gameplay.RefreshInputSuspension();
            }
            else session.InputSuspended = true;
            var crow = gameplay != null ? gameplay.Crow : null;
            crowShown = crow != null && crow.isActiveAndEnabled && crow.FlightRightFrames != null && crow.FlightRightFrames.Count >= 2;
            crowFrom = CrowStart(crow);
            if (crow != null) crow.HoldForTravel();
            Phase = FastTravelPhase.DepartureAnimation;
            phaseStartedAt = now;
            flightStartedAt = now;
            return FastTravelStart.Started;
        }

        private FastTravelStart Check(string pointId, out FastTravelPoint point, out Vector2Int cell)
        {
            cell = default;
            point = registry.Find(pointId);
            if (Busy) return FastTravelStart.Busy;
            if (point == null) return FastTravelStart.UnknownPoint;
            if (!found.Contains(point.id)) return FastTravelStart.NotDiscovered;
            if (session == null || session.World == null || session.World != session.OutdoorWorld) return FastTravelStart.NotOutdoors;
            var index = session.OutdoorIndex;
            if (index == null || index.WorldId != point.worldId) return FastTravelStart.UnknownWorld;
            var region = index.FindRegion(point.mapId);
            if (region == null) return FastTravelStart.UnknownRegion;
            var arrival = region.ToWorld(point.arrivalCell);
            if (!region.ContainsWorld(arrival)) return FastTravelStart.BadArrival;
            if (IsHere(point)) return FastTravelStart.AlreadyHere;
            if (index.ChunkAt(index.ChunkOfCell(arrival)) == null) return FastTravelStart.NoChunk;
            return TryFindLanding(point, region, arrival, out cell) ? FastTravelStart.Started : FastTravelStart.NoLandingCell;
        }

        // The arrival cell, else the nearest safe cell around it: rings of growing Manhattan distance up
        // to the point's landingSearchRadius (at most 2r(r+1)+1 cells), the same row first, left before
        // right. Safe: in the point's region, walkable, not a door or a room's exit, not the crow's
        // ground cell, in a chunk of the world.
        public bool TryFindLanding(FastTravelPoint point, OutdoorRegion region, Vector2Int arrival, out Vector2Int cell)
        {
            var index = session.OutdoorIndex;
            LastLandingCandidates = 0;
            for (var radius = 0; radius <= point.landingSearchRadius; radius++)
                foreach (var c in Ring(arrival, radius))
                {
                    LastLandingCandidates++;
                    if (!region.ContainsWorld(c) || !index.CanEnter(c) || session.IsTransitionCell(session.OutdoorWorld, c) ||
                        !session.CanPlayerEnter(c) || index.ChunkAt(index.ChunkOfCell(c)) == null) continue;
                    cell = c;
                    return true;
                }
            cell = default;
            return false;
        }

        private void Update() => Tick(Time.unscaledTime);

        public void Tick(float now)
        {
            var t = now - phaseStartedAt;
            switch (Phase)
            {
                case FastTravelPhase.DepartureAnimation:
                    SetLift(LiftAt(t, true));
                    ShowCrow(DepartureCrow(t));
                    if (t >= DepartureSeconds) Transfer(now);
                    break;
                case FastTravelPhase.Transferring:
                    if (t >= CoverSeconds) { Phase = FastTravelPhase.ArrivalAnimation; phaseStartedAt = now; }
                    break;
                case FastTravelPhase.ArrivalAnimation:
                    SetLift(LiftAt(t, false));
                    ShowCrow(ArrivalCrow(t));
                    if (t >= ArrivalSeconds) Finish(now);
                    break;
            }
        }

        // Behind the sky: the destination's chunks, her landing cell, the cameras, the crow beside her.
        private void Transfer(float now)
        {
            Phase = FastTravelPhase.Transferring;
            phaseStartedAt = now;
            SetLift(0f);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var ready = presenter == null || presenter.PrepareAround(landingCell);
            LastPrepareMilliseconds = presenter != null ? presenter.LastPrepareMilliseconds : 0;
            LastPrepareBuilt = presenter != null ? presenter.LastPrepareBuilt : 0;
            if (!ready) { flightFailed = true; LastArrivalProblem = "destination chunk not built"; }
            else if (!session.RelocateOutdoorsNow(landingCell, landingFacing)) { flightFailed = true; LastArrivalProblem = "landing cell refused"; }
            if (!flightFailed)
            {
                if (presenter != null) presenter.UpdateStreaming(true);
                if (views != null)
                {
                    if (views.PixelView != null) views.PixelView.SnapToTarget();
                    if (views.ThirdPerson != null) views.ThirdPerson.SnapToTarget();
                }
                var crow = gameplay != null ? gameplay.Crow : null;
                if (crow != null && crow.isActiveAndEnabled) crow.LandBeside(CrowTime());
            }
            watch.Stop();
            LastTransferMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (LastTransferMilliseconds > MaxTransferMilliseconds) MaxTransferMilliseconds = LastTransferMilliseconds;
        }

        private void Finish(float now)
        {
            SetLift(0f);
            var crow = gameplay != null ? gameplay.Crow : null;
            if (crow != null) crow.ReleaseAfterTravel(!flightFailed, CrowTime());
            // Complete: no longer busy from here on (input, the menu and AUTO come back below).
            Phase = FastTravelPhase.Complete;
            holdPlace = false;
            Phase = FastTravelPhase.Idle;
            if (gameplay != null) gameplay.RefreshInputSuspension();
            else session.InputSuspended = false;
            var menu = gameplay != null ? gameplay.Menu : null;
            // (the phone rotation guide keeps it off while it shows)
            if (menu != null) menu.enabled = gameplay == null || !gameplay.Suspended;
            if (gameplay != null && gameplay.AutoMode != null) gameplay.AutoMode.RecordUserAction(now);
            LastFlightSeconds = now - flightStartedAt;
            if (flightFailed)
            {
                Failures++;
                if (gameplay != null && gameplay.Hud != null) gameplay.Hud.ShowMessage(MessageFailed);
            }
            else
            {
                Travels++;
                LastArrivalVerified = VerifyArrival(out var problem);
                LastArrivalProblem = problem;
            }
            Completed?.Invoke(!flightFailed);
        }

        // Her place, region, chunk, input and the crow, once she has landed.
        private bool VerifyArrival(out string problem)
        {
            problem = "";
            if (session.Cell != landingCell) problem += "cell; ";
            if (session.CurrentRegionMapId != target.mapId) problem += "region; ";
            if (!session.IsCellReady(session.Cell)) problem += "chunk; ";
            if (!session.World.CanEnter(session.Cell)) problem += "collision; ";
            if (session.InputSuspended && (gameplay == null || !gameplay.Suspended)) problem += "input; ";
            if (session.CompletedSteps != StepsAtDeparture) problem += "steps; ";
            var crow = gameplay != null ? gameplay.Crow : null;
            if (crow != null && crow.TravelHeld) problem += "crow; ";
            return problem.Length == 0;
        }

        // While a flight is under way the save writes the place she left (a page closed mid-flight
        // resumes there); the destination is written once she has landed.
        public bool TryGetHeldPlace(out string mapId, out Vector2Int mapCell, out FacingDirection facing)
        {
            mapId = heldMapId;
            mapCell = heldCell;
            facing = heldFacing;
            return holdPlace && Busy;
        }

        private void SetLift(float lift)
        {
            if (actor != null) actor.PresentationLift = lift;
        }

        // Up while the sky closes in, down while it clears (eased).
        private static float LiftAt(float t, bool departure)
        {
            if (departure)
            {
                var k = Mathf.Clamp01((t - 0.55f) / (DepartureSeconds - 0.55f));
                return LiftUnits * k * k;
            }
            var d = Mathf.Clamp01(t / 0.6f);
            return LiftUnits * 0.6f * (1f - d) * (1f - d);
        }

        // ---------------------------------------------------------------- the picture

        // How high above her the crow flies while it lifts her (world units): above the 2D picture's
        // head, and above the 3D model's head (the 3D camera is close behind her).
        private const float HeadHeight2D = 1.45f;
        private const float HeadHeight3D = 1.15f;

        private float HeadHeight => views != null && views.Mode == PocViewMode.ThirdPerson3D ? HeadHeight3D : HeadHeight2D;

        private Vector3 HerPoint() => actor != null ? actor.transform.position : GridSpace3D.ToWorld(session.Position);

        private void ShowCrow(Vector3? point)
        {
            var crow = gameplay != null ? gameplay.Crow : null;
            if (crowShown && point.HasValue && crow != null && crow.TravelHeld) crow.ShowTravelFlight(point.Value, CrowTime());
        }

        // Departure: from where the crow is (or, out of view, from the air beside her) over to her head
        // in a little arc, then up with her.
        private Vector3? DepartureCrow(float t)
        {
            if (!crowShown) return null;
            var above = HerPoint() + Vector3.up * HeadHeight;
            if (t >= 0.55f) return above;
            var k = EaseOut(t / 0.55f);
            return Vector3.Lerp(crowFrom, above, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.8f);
        }

        // Arrival: down from the sky beside her to its landing cell.
        private Vector3? ArrivalCrow(float t)
        {
            var crow = gameplay != null ? gameplay.Crow : null;
            if (!crowShown || flightFailed || crow == null) return null;
            var ground = crow.GroundPoint;
            var from = ground + Vector3.up * 3.2f + Vector3.left * 1.2f;
            return Vector3.Lerp(from, ground, EaseOut(Mathf.Clamp01(t / ArrivalSeconds)));
        }

        // The crow's own position when it is on screen, else a point in the air beside her.
        private Vector3 CrowStart(PocCrow crow)
        {
            if (crow != null && crow.isActiveAndEnabled && (views == null || views.IsInView(crow.transform.position))) return crow.transform.position;
            return HerPoint() + new Vector3(-4f, 3.5f, 1.5f);
        }

        private Color SkyColor()
        {
            var night = timeOfDay != null ? timeOfDay.NightBlend : 0f;
            return Color.Lerp(PocTimeOfDay.DaySky, PocTimeOfDay.NightSky, night);
        }

        private Color CloudColor()
        {
            var night = timeOfDay != null ? timeOfDay.NightBlend : 0f;
            return Color.Lerp(new Color(0.97f, 0.98f, 1f), new Color(0.24f, 0.28f, 0.42f), night);
        }

        // 0..1: how much of the screen the sky covers.
        public float CoverAlpha()
        {
            var t = Time.unscaledTime - phaseStartedAt;
            switch (Phase)
            {
                case FastTravelPhase.DepartureAnimation: return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.65f) / (DepartureSeconds - 0.65f)));
                case FastTravelPhase.Transferring: return 1f;
                case FastTravelPhase.ArrivalAnimation: return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.5f));
                default: return 0f;
            }
        }

        private void OnGUI()
        {
            if (Phase != FastTravelPhase.DepartureAnimation && Phase != FastTravelPhase.Transferring &&
                Phase != FastTravelPhase.ArrivalAnimation) return;
            GUI.depth = -200;
            if (Event.current.type != EventType.Repaint) return;
            var alpha = CoverAlpha();
            if (alpha <= 0f) return;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), SkyColor(), alpha);
            var unit = SpriteScale();
            DrawClouds(Time.unscaledTime, unit, alpha);
            if (Phase == FastTravelPhase.Transferring) DrawPair(Time.unscaledTime - phaseStartedAt, unit);
        }

        private static readonly float[] CloudOffsets = { 0.05f, 0.62f, 0.31f, 0.86f, 0.47f, 0.18f };

        // Pixel clouds streaming past (the flight's speed), in blocks of the sprites' pixel size.
        private void DrawClouds(float now, float unit, float alpha)
        {
            var color = CloudColor();
            for (var i = 0; i < 6; i++)
            {
                var speed = Screen.width * (0.9f + 0.25f * (i % 3));
                var span = Screen.width + unit * 60f;
                var x = Screen.width - Mathf.Repeat(now * speed + CloudOffsets[i] * span, span);
                var y = Screen.height * (0.12f + 0.14f * i);
                var w = unit * (18f + 6f * (i % 3));
                var h = unit * 5f;
                Fill(new Rect(Mathf.Round(x), Mathf.Round(y), w, h), color, alpha * 0.85f);
                Fill(new Rect(Mathf.Round(x + w * 0.2f), Mathf.Round(y - h * 0.6f), w * 0.45f, h * 0.7f), color, alpha * 0.85f);
            }
        }

        // In the sky: the crow, and ﾊﾙｶﾁｬﾝ below it, flying across (her 2D picture in both views).
        private void DrawPair(float t, float unit)
        {
            var crow = gameplay != null ? gameplay.Crow : null;
            var center = new Vector2(Mathf.Lerp(0.36f, 0.64f, t / CoverSeconds) * Screen.width,
                Screen.height * 0.42f + Mathf.Sin(t * 9f) * unit * 2f);
            // Her front picture (the 2D game's idle, facing the screen).
            var front = actor != null && actor.Sprites != null ? actor.Sprites.Select(FacingDirection.Down, false) : null;
            var her = front != null && front.Length > 0 ? front[0] : actor != null ? actor.CurrentSprite : null;
            if (her != null)
            {
                var size = her.rect.size * unit;
                DrawSprite(her, new Rect(center.x - size.x * 0.5f, center.y + unit * 4f, size.x, size.y), false);
            }
            if (crowShown && crow != null)
            {
                var frames = crow.FlightRightFrames;
                var sprite = frames[(int)(Time.unscaledTime / 0.09f) % frames.Count];
                if (sprite != null)
                {
                    var size = sprite.rect.size * unit;
                    DrawSprite(sprite, new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y), false);
                    LastCrowScreen = new Vector2(center.x, Screen.height - center.y);
                }
            }
        }

        private static void DrawSprite(Sprite sprite, Rect rect, bool flip)
        {
            var texture = sprite.texture;
            var uv = new Rect(sprite.textureRect.x / texture.width, sprite.textureRect.y / texture.height,
                sprite.textureRect.width / texture.width, sprite.textureRect.height / texture.height);
            if (flip) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
            GUI.DrawTextureWithTexCoords(new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), rect.width, rect.height), texture, uv);
        }

        private static void Fill(Rect rect, Color color, float alpha)
        {
            color.a *= alpha;
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        // The 2D view's pixel scale (as large as the 2D game draws them); in 3D about the same.
        private float SpriteScale()
        {
            if (views != null && views.Mode == PocViewMode.PixelView2D && views.PixelView != null) return Mathf.Max(1, views.PixelView.Scale);
            return Mathf.Max(2f, Mathf.Round(Screen.height / 240f));
        }

        // ---------------------------------------------------------------- helpers

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        // The cells at this Manhattan distance, the same row first, left before right.
        private static IEnumerable<Vector2Int> Ring(Vector2Int center, int radius)
        {
            for (var dy = 0; dy <= radius; dy++)
            {
                var dx = radius - dy;
                yield return center + new Vector2Int(-dx, -dy);
                if (dx != 0) yield return center + new Vector2Int(dx, -dy);
                if (dy == 0) continue;
                yield return center + new Vector2Int(-dx, dy);
                if (dx != 0) yield return center + new Vector2Int(dx, dy);
            }
        }

        // The destination list: a page of the game's menu. Places she has found (four to a page, つぎへ /
        // まえへ when there are more), then やめる.
        public sealed class DestinationPage : IGameMenuPage
        {
            private readonly PocFastTravel owner;
            private readonly List<string> lines = new List<string> { PageLine };
            private readonly List<string> buttons = new List<string>();
            private readonly List<Action<float>> actions = new List<Action<float>>();
            private List<FastTravelPoint> points = new List<FastTravelPoint>();
            private int pageIndex;

            public DestinationPage(PocFastTravel fastTravel) => owner = fastTravel;

            public string Title => PageTitle;
            public IReadOnlyList<string> Lines => lines;
            public IReadOnlyList<string> Buttons => buttons;
            public IReadOnlyList<FastTravelPoint> Points => points;
            public int PageIndex => pageIndex;
            public bool IsOpen
            {
                get
                {
                    var menu = owner.gameplay != null ? owner.gameplay.Menu : null;
                    return menu != null && menu.Page == this;
                }
            }

            public void Reset(List<FastTravelPoint> found)
            {
                points = found ?? new List<FastTravelPoint>();
                pageIndex = 0;
                Build();
            }

            private void Build()
            {
                buttons.Clear();
                actions.Clear();
                var pages = Mathf.Max(1, (points.Count + PageSize - 1) / PageSize);
                pageIndex = Mathf.Clamp(pageIndex, 0, pages - 1);
                for (var i = pageIndex * PageSize; i < Mathf.Min(points.Count, (pageIndex + 1) * PageSize); i++)
                {
                    var point = points[i];
                    buttons.Add(point.name);
                    actions.Add(now => owner.Chosen(point, now));
                }
                if (pageIndex + 1 < pages) { buttons.Add(NextLabel); actions.Add(_ => { pageIndex++; Build(); }); }
                if (pageIndex > 0) { buttons.Add(PreviousLabel); actions.Add(_ => { pageIndex--; Build(); }); }
                buttons.Add(CancelLabel);
                actions.Add(now =>
                {
                    var menu = owner.gameplay != null ? owner.gameplay.Menu : null;
                    if (menu != null) menu.SetOpen(false, now);
                });
            }

            public void Press(int index, float now)
            {
                if (index >= 0 && index < actions.Count) actions[index](now);
            }
        }
    }
}
