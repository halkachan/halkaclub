using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Halka.Poc2D3D.Life
{
    // A character's look (the scene builder fills these from the ver4.0 models and sprites).
    [Serializable]
    public sealed class PocCharacterLook
    {
        public string id;
        public GameObject model;
        public Material material;
        public float scale = 1f;
        public int outlineId = PocOutlineTag.CharacterId;
        public float spriteOffsetNorth = -0.22f;
        public PocSpriteSet sprites = new PocSpriteSet();
    }

    // ver4.0「そうさくせいかつ！」: the one place that runs what ver4.0 adds, on top of the shared world
    // (nothing here changes how she walks, what the rooms are or how the crow lives):
    //   - the game clock (LifeClock: the world clock read as days and hours) and the day count;
    //   - the creator stations (DTM desk, drawing tablet, microphone), the three mini games
    //     (PocMinigames) and the abilities they raise (CreatorState);
    //   - ﾆｺﾁｭｰﾌﾞ (NicoTubeChannel): its page, the post, 24 game hours between posts;
    //   - the passers-by (fans and antis, PocLife.Npcs.cs), Rin and Len (PocLife.Companions.cs),
    //     hide-and-seek (PocLife.HideSeek.cs);
    //   - the roster: the crow and these characters as the one ground occupant and talk target the
    //     shared session knows (cells nobody walks into, A in front of whom);
    //   - its save block (LifeSaveBlock, one extension of the Schema 1 file).
    [DefaultExecutionOrder(20)]
    public sealed partial class PocLife : MonoBehaviour, IHarukaLifeHost, ISharedGroundOccupant, ISharedTalkTarget
    {
        public const string MenuLabel = "そうさく";
        public const string CloseLabel = "やめる";
        public const string HideSeekStopLabel = "かくれんぼをやめる";

        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocGameplay gameplay;
        [SerializeField] private GameMenuController menu;
        [SerializeField] private GameHud hud;
        [SerializeField] private WorldTimeController worldTime;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private HarukaSaveSystem saveSystem;
        [SerializeField] private PocInput input;
        [SerializeField] private PocFastTravel fastTravel;
        [SerializeField] private PocCrow crow;
        [SerializeField] private PocMinigames minigames;
        [SerializeField] private PocCharacterLook[] looks = new PocCharacterLook[0];

        public LifeSaveBlock State { get; private set; } = new LifeSaveBlock();
        public int LifeVersion { get; private set; }
        public bool Started { get; private set; }
        public string StartSource { get; private set; } = "";
        public PocMinigames Minigames => minigames;
        public SharedWorldSession Session => session;
        public PocGameplay Gameplay => gameplay;
        public GameMenuController Menu => menu;
        public IReadOnlyList<PocCharacterLook> Looks => looks;
        public long DevClockOffsetMs { get; private set; }

        public void Configure(SharedWorldSession worldSession, PocGameplay sharedGameplay, GameMenuController gameMenu, GameHud messageHud,
            WorldTimeController clock, PocViewSwitcher viewSwitcher, HarukaSaveSystem saves, PocInput playerInput, PocFastTravel travel,
            PocCrow sharedCrow, PocMinigames games, PocCharacterLook[] characterLooks)
        {
            session = worldSession;
            gameplay = sharedGameplay;
            menu = gameMenu;
            hud = messageHud;
            worldTime = clock;
            views = viewSwitcher;
            saveSystem = saves;
            input = playerInput;
            fastTravel = travel;
            crow = sharedCrow;
            minigames = games;
            looks = characterLooks ?? new PocCharacterLook[0];
        }

        public PocCharacterLook Look(string id)
        {
            foreach (var look in looks)
                if (look != null && look.id == id) return look;
            return null;
        }

        // ---------------------------------------------------------------- the clock

        // The world clock's own now (the same function World Time uses, so a development clock
        // offset moves both together).
        public long NowMs => worldTime != null ? worldTime.ClockMilliseconds() : LifeClock.UtcNowMs();
        public long AnchorMs => worldTime != null && worldTime.AnchorMilliseconds > 0 ? worldTime.AnchorMilliseconds : NowMs;
        public int MinuteOfDay => LifeClock.MinuteOfDay(AnchorMs, NowMs);
        public long AbsoluteDay => LifeClock.AbsoluteDay(AnchorMs, NowMs);
        public long ShownDay => LifeClock.ShownDay(AnchorMs, NowMs, State.DayOrigin);
        public bool IsNightNow => LifeClock.IsNight(MinuteOfDay);
        public string ClockText => LifeClock.Text(ShownDay, MinuteOfDay);

        // ---------------------------------------------------------------- start

        private void Start() => Begin();

        public void Begin()
        {
            if (Started || session == null) return;
            Started = true;
            PocWalker.Prune();
            session.Initialize();
            var loaded = saveSystem != null && saveSystem.Loaded != null ? saveSystem.Loaded.Life : null;
            if (loaded != null)
            {
                Apply(loaded.Copy(), false);
                StartSource = "save";
            }
            else
            {
                StartFresh();
                StartSource = saveSystem != null && saveSystem.Loaded != null && saveSystem.LoadSource != "new" ? "older save (first ver4.0 start)" : "new";
            }
            Hook();
            BuildCompanions();
            RestoreNpcs();
            EvaluateNpcsAtStart();
            lastDay = AbsoluteDay;
            Changed();
        }

        // ver4.0 data from its defaults: day 1 from today (the morning / night of the world clock as
        // it is), the abilities 0, the channel's first post 24 game hours from now, Rin and Len at home.
        public void StartFresh()
        {
            var now = NowMs;
            State = new LifeSaveBlock
            {
                DayOrigin = LifeClock.AbsoluteDay(AnchorMs, now),
                StartedUnixMs = now,
            };
            State.Channel.BasisUnixMs = now;
            State.Channel.Seed = (now ^ 0x5DEECE66DL) & 0x7FFFFFFFFFFFL;
            if (State.Channel.Seed == 0) State.Channel.Seed = 1;
        }

        private bool hooked;

        private void Hook()
        {
            if (hooked) return;
            hooked = true;
            if (saveSystem != null) saveSystem.LifeHost = this;
            session.GroundOccupant = this;
            session.TalkTarget = this;
            session.InteractionOverride = OnInteract;
            session.AheadOverride = OnAhead;
            session.MapChanged += OnMapChanged;
            if (crow != null) crow.OthersReserve = WalkersReserve;
            if (input != null) input.CharacterClick = OnCharacterClick;
            if (menu != null && minigames != null) menu.HiddenWhile = () => minigames.IsOpen;
            if (fastTravel != null) fastTravel.Completed += OnTravelCompleted;
            if (menu != null)
            {
                menu.AddLinkItem(MenuLabel, OpenSkills, () => !HideSeekActive);
                menu.AddLinkItem(HideSeekStopLabel, now => StopHideSeek(true), () => HideSeekActive);
            }
        }

        private void OnDestroy()
        {
            if (!hooked || session == null) return;
            session.MapChanged -= OnMapChanged;
            if (fastTravel != null) fastTravel.Completed -= OnTravelCompleted;
        }

        public void Changed() => LifeVersion++;

        // ---------------------------------------------------------------- save host

        public LifeSaveBlock CaptureLife()
        {
            var block = State.Copy();
            CaptureCompanions(block);
            CaptureNpcs(block);
            return block;
        }

        public void ApplyLife(LifeSaveBlock block, bool imported)
        {
            if (block == null)
            {
                if (imported) return; // an older file imported: ver4.0's own data stays as it is
                StartFresh();
            }
            else Apply(block.Copy(), imported);
            if (Started)
            {
                StopHideSeek(false);
                RestoreCompanions();
                RestoreNpcs();
            }
            Changed();
        }

        public bool SameAsApplied(LifeSaveBlock imported, LifeSaveBlock captured)
        {
            if (imported == null || captured == null) return imported == captured;
            return imported.DayOrigin == captured.DayOrigin && imported.Creator.SameAs(captured.Creator) &&
                imported.Channel.SameAs(captured.Channel) && imported.Rin.Mode == captured.Rin.Mode && imported.Len.Mode == captured.Len.Mode &&
                imported.HideSeek.RinFound == captured.HideSeek.RinFound && imported.HideSeek.LenFound == captured.HideSeek.LenFound &&
                imported.HideSeek.BothCleared == captured.HideSeek.BothCleared;
        }

        // A loaded or imported block, with odd values repaired (never refused).
        private void Apply(LifeSaveBlock block, bool imported)
        {
            Sanitize(block, NowMs);
            State = block;
        }

        public int Repairs { get; private set; }

        // 指示書 15.5: very large numbers stay (they are what was earned); a post time in the future
        // (a clock set back, a damaged value) becomes now, so the wait is at most one day and never
        // negative; an unknown NPC kind or region drops that NPC only; Rin's or Len's cell is checked
        // when they are placed (a blocked cell moves them to a free one nearby).
        public void Sanitize(LifeSaveBlock block, long nowMs)
        {
            var ch = block.Channel;
            if (ch.Subscribers.Sign < 0) { ch.Subscribers = BigInteger.Zero; Repairs++; }
            if (ch.TotalViews.Sign < 0) { ch.TotalViews = BigInteger.Zero; Repairs++; }
            if (ch.Money.Sign < 0) { ch.Money = BigInteger.Zero; Repairs++; }
            if (ch.Posts < 0) { ch.Posts = 0; Repairs++; }
            if (ch.BasisUnixMs <= 0 || ch.BasisUnixMs > nowMs) { ch.BasisUnixMs = nowMs; Repairs++; }
            if (ch.LastPostUnixMs < 0 || ch.LastPostUnixMs > nowMs) { ch.LastPostUnixMs = ch.LastPostUnixMs > 0 ? nowMs : 0; Repairs++; }
            if (ch.Seed == 0) { ch.Seed = 1; Repairs++; }
            for (var i = 0; i < CreatorState.SkillCount; i++)
                if (block.Creator.Skills[i].Sign < 0) { block.Creator.Skills[i] = BigInteger.Zero; Repairs++; }
            for (var i = block.Npcs.Count - 1; i >= 0; i--)
            {
                var n = block.Npcs[i];
                if (Array.IndexOf(NpcKinds, n.Kind) < 0 || session != null && session.OutdoorIndex != null &&
                    session.OutdoorIndex.FindRegion(n.Region) == null)
                {
                    block.Npcs.RemoveAt(i);
                    Repairs++;
                }
            }
        }

        // ---------------------------------------------------------------- every frame

        private long lastDay;
        // The life's own clock (Time.time in play; the checks drive Tick with theirs).
        private float lifeTime;
        public float LifeTime => lifeTime;

        private void Update()
        {
            if (Started) Tick(Time.deltaTime, Time.time);
        }

        public void Tick(float dt, float time)
        {
            if (!Started) return;
            lifeTime = time;
            PocWalker.Prune();
            var now = NowMs;
            var day = AbsoluteDay;
            if (day != lastDay)
            {
                lastDay = day;
                OnNewDay();
            }
            TickCompanions(dt);
            TickHideSeek(dt);
            TickNpcs(dt, now);
            YieldToPush();
            var outdoors = session.World != null && session.World == session.OutdoorWorld;
            var threeD = views != null && views.Mode == PocViewMode.ThirdPerson3D;
            foreach (var walker in PocWalker.Active)
            {
                walker.UpdateDrawn(outdoors, session.CellReady);
                walker.Present3D(threeD, dt);
            }
            if (stationPage != null) stationPage.Watch(Time.unscaledTime);
        }

        private void OnNewDay()
        {
            EvaluateNpcs("new day");
        }

        private void OnMapChanged(SharedWorldModel world)
        {
            CompanionsMapChanged(world);
            if (HideSeekActive && world != session.OutdoorWorld) StopHideSeek(false);
        }

        private void OnTravelCompleted(bool landed)
        {
            if (landed) CompanionsAfterTravel();
        }

        // ---------------------------------------------------------------- roster

        // ISharedGroundOccupant: the crow's and every character's cells.
        public bool ReservesGround(Vector2Int cell) =>
            crow != null && crow.ReservesGround(cell) || WalkersReserve(cell);

        public bool WalkersReserve(Vector2Int cell)
        {
            foreach (var walker in PocWalker.Active)
                if (walker != null && walker.ReservesGround(cell)) return true;
            return false;
        }

        public bool WalkersReserveExcept(Vector2Int cell, PocWalker except)
        {
            foreach (var walker in PocWalker.Active)
                if (walker != null && walker != except && walker.ReservesGround(cell)) return true;
            return false;
        }

        // ISharedTalkTarget: the crow first (as before), then Rin, Len or a passer-by in front of her.
        public bool TryTalk(Vector2Int playerCell, FacingDirection facing, bool playerMoving)
        {
            if (crow != null && crow.TryTalk(playerCell, facing, playerMoving)) return true;
            if (session.World != session.OutdoorWorld || playerMoving) return false;
            foreach (var walker in PocWalker.Active)
            {
                if (walker == null || !walker.Present || walker.HiddenByGame || walker.IsMoving) continue;
                if (!ExamineInteractable.IsInRange(playerCell, playerMoving, facing, walker.Cell)) continue;
                return TalkTo(walker);
            }
            return false;
        }

        private bool OnCharacterClick(Vector2 screenPosition)
        {
            if (views == null) return false;
            var walker = views.PickWalker(screenPosition);
            if (walker == null) return false;
            if (!ExamineInteractable.IsInRange(session.Cell, session.IsMoving, session.Facing, walker.Cell))
            {
                // A click on someone further away turns her toward them when they are next to her.
                var delta = walker.Cell - session.Cell;
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 && !session.IsMoving)
                {
                    session.SetFacing(FacingDirectionExtensions.FromVector(delta));
                    return TalkTo(walker);
                }
                return false;
            }
            return TalkTo(walker);
        }

        private bool TalkTo(PocWalker walker)
        {
            walker.Face(FacingDirectionExtensions.FromVector(session.Cell - walker.Cell));
            if (HideSeekActive && TryFind(walker)) return true;
            if (walker == rin || walker == len) return OpenCompanionChoices(walker);
            return TalkToNpc(walker);
        }

        // ---------------------------------------------------------------- creator stations

        public const string DtmDeskId = "dtm_desk_large";
        public const string TabletId = "illust_desk_tablet";
        public const string MicId = "recording_mic_basic";

        private bool OnInteract(SharedObjectInstance target)
        {
            if (target == null || session.World == null || !session.World.IsInterior) return false;
            if (target.DefinitionId == DtmDeskId || target.DefinitionId == TabletId)
            {
                // Only from where the furniture answers (its action points), as before.
                if (!session.World.TryInteract(target, session.Cell, session.Facing, session.IsMoving, out var message)) return false;
                return OpenStation(target.DefinitionId, message);
            }
            if (target.DefinitionId == MicId)
            {
                if (!NextToMic(target, session.Cell, session.Facing)) return false;
                return OpenStation(MicId, "マイクスタンド。");
            }
            return false;
        }

        private bool OnAhead(Vector2Int cell, FacingDirection facing)
        {
            if (session.World == null || !session.World.IsInterior) return false;
            foreach (var instance in session.World.Objects)
                if (instance.DefinitionId == MicId && NextToMic(instance, cell, facing)) return OpenStation(MicId, "マイクスタンド。");
            return false;
        }

        // The microphone answers from any side, facing it (it has no action point of its own).
        private static bool NextToMic(SharedObjectInstance mic, Vector2Int cell, FacingDirection facing)
        {
            foreach (var blocked in mic.BlockedCells())
                if (ExamineInteractable.IsInRange(cell, false, facing, blocked)) return true;
            return false;
        }

        private StationPage stationPage;

        private bool OpenStation(string id, string firstLine)
        {
            if (menu == null || minigames == null) return false;
            stationPage ??= new StationPage(this);
            return stationPage.Open(id, firstLine, Time.unscaledTime);
        }

        // The furniture's window (in the game's menu panel): its own text, then what can be done there.
        private sealed class StationPage : IGameMenuPage
        {
            private readonly PocLife owner;
            private readonly List<string> lines = new List<string>();
            private readonly List<string> buttons = new List<string>();
            private string station;
            private bool shown;

            public StationPage(PocLife life) => owner = life;
            public string Title { get; private set; } = "";
            public IReadOnlyList<string> Lines => lines;
            public IReadOnlyList<string> Buttons => buttons;
            public string Station => station;

            public bool Open(string id, string firstLine, float now)
            {
                station = id;
                lines.Clear();
                buttons.Clear();
                if (!string.IsNullOrEmpty(firstLine)) lines.Add(firstLine);
                if (id == DtmDeskId)
                {
                    Title = "DTMデスク";
                    lines.Add("さっきょくりょく " + LifeNumbers.Short(owner.State.Creator.Skills[0]));
                    buttons.Add("さっきょくする");
                    buttons.Add(NicoOpenLabel);
                }
                else if (id == TabletId)
                {
                    Title = "えきタブ";
                    lines.Add("がりょく " + LifeNumbers.Short(owner.State.Creator.Skills[1]));
                    buttons.Add("おえかきする");
                    buttons.Add(NicoOpenLabel);
                }
                else
                {
                    Title = "マイクスタンド";
                    lines.Add("かしょうりょく " + LifeNumbers.Short(owner.State.Creator.Skills[2]));
                    buttons.Add("うたう");
                }
                buttons.Add(CloseLabel);
                if (owner.hud != null) owner.hud.ClearMessage();
                if (owner.menu.IsOpen) owner.menu.SetOpen(false, now);
                owner.menu.OpenPage(this, now);
                shown = owner.menu.Page == this;
                return shown;
            }

            public void Press(int index, float now)
            {
                var label = index >= 0 && index < buttons.Count ? buttons[index] : CloseLabel;
                shown = false;
                owner.menu.SetOpen(false, now);
                switch (label)
                {
                    case "さっきょくする": owner.StartGame(CreatorSkill.Composition); break;
                    case "おえかきする": owner.StartGame(CreatorSkill.Drawing); break;
                    case "うたう": owner.StartGame(CreatorSkill.Singing); break;
                    case NicoOpenLabel: owner.OpenNicoTube(now); break;
                }
            }

            public void Watch(float now)
            {
                if (shown && owner.menu.Page != this) shown = false;
            }
        }

        // Where the creator stations are on screen in a room (pixels from the bottom left; the checks click them).
        private void AppendStationJson(StringBuilder b)
        {
            var inv = CultureInfo.InvariantCulture;
            b.Append(",\"stations\":[");
            var first = true;
            // (Explicit null checks: the WebGL build catches no null reference, and the 2D view's target
            // does not exist yet in the first frames.)
            var pixel = views != null ? views.PixelView : null;
            var third = views != null ? views.ThirdPerson : null;
            var twoD = views != null && views.Mode == PocViewMode.PixelView2D;
            var cam = twoD ? (pixel != null ? pixel.WorldCamera : null) : (third != null ? third.ViewCamera : null);
            var ready = cam != null && (!twoD || pixel.Target != null);
            if (ready && session.World != null && session.World.IsInterior)
            {
                var presenter = FindFirstObjectByType<PocWorldPresenter>();
                foreach (var instance in session.World.Objects)
                {
                    if (instance.DefinitionId != DtmDeskId && instance.DefinitionId != TabletId && instance.DefinitionId != MicId) continue;
                    var view = presenter != null ? presenter.FindView(instance.InstanceId) : null;
                    if (view == null) continue;
                    var bounds = new Bounds(view.transform.position, Vector3.zero);
                    if (view.Renderers != null)
                        foreach (var r in view.Renderers) if (r != null) bounds.Encapsulate(r.bounds);
                    Vector3 p;
                    if (twoD)
                    {
                        var v = cam.WorldToViewportPoint(bounds.center);
                        p = new Vector3(v.x * pixel.Target.width * pixel.Scale - pixel.Offset.x, v.y * pixel.Target.height * pixel.Scale - pixel.Offset.y, 1f);
                    }
                    else p = cam.WorldToScreenPoint(bounds.center);
                    b.Append(first ? "" : ",").Append("{\"id\":\"").Append(instance.DefinitionId).Append("\",\"x\":").Append(instance.RootCell.x.ToString(inv))
                        .Append(",\"y\":").Append(instance.RootCell.y.ToString(inv)).Append(",\"sx\":").Append(p.x.ToString("0", inv))
                        .Append(",\"sy\":").Append(p.y.ToString("0", inv)).Append('}');
                    first = false;
                }
            }
            b.Append(']');
        }

        // ---------------------------------------------------------------- mini games

        public int GamesFinished { get; private set; }
        public int GamesCancelled { get; private set; }
        public string LastGameResult { get; private set; } = "";

        public bool StartGame(CreatorSkill skill)
        {
            if (minigames == null || minigames.IsOpen) return false;
            if (gameplay != null) gameplay.ActivityOpen = true;
            return minigames.Begin(skill, OnGameFinished, OnGameClosed);
        }

        // A finished game (never a cancelled one): its records and ability, saved at once.
        private int OnGameFinished(CreatorSkill skill, int score)
        {
            var gain = State.Creator.ApplyScore(skill, score);
            GamesFinished++;
            LastGameResult = CreatorState.SkillIds[(int)skill] + " " + score + " +" + gain;
            Changed();
            if (saveSystem != null) saveSystem.Write("mini game", Time.unscaledTime);
            return gain;
        }

        private void OnGameClosed(bool cancelled)
        {
            if (cancelled) GamesCancelled++;
            if (gameplay != null) gameplay.ActivityOpen = HideSeekCounting;
        }

        // ---------------------------------------------------------------- ﾆｺﾁｭｰﾌﾞ

        // Shown in full-width katakana: the game font's half-width kana are squeezed and lose ﾌﾞ's voicing mark.
        public const string NicoName = "ニコチューブ";
        public const string NicoOpenLabel = "ニコチューブをひらく";
        public const string PostLabel = "動画を投稿する";
        public const string PostedLine = "動画を投稿した！";
        public const string PostReadyLine = "とうこうできる！";

        private NicoPage nicoPage;
        public int PostsThisSession { get; private set; }
        public int PostRefusals { get; private set; }
        public NicoTubeChannel.PostResult LastPost { get; private set; }

        public string CooldownLine(long nowMs)
        {
            var left = State.Channel.RemainingGameMinutes(nowMs);
            return left == 0 ? PostReadyLine : "つぎのとうこうまで " + LifeClock.DurationText(left);
        }

        public void OpenNicoTube(float now)
        {
            if (menu == null) return;
            nicoPage ??= new NicoPage(this);
            nicoPage.ShowChannel();
            if (hud != null) hud.ClearMessage();
            if (menu.IsOpen && menu.Page != nicoPage) menu.SetOpen(false, now);
            menu.OpenPage(nicoPage, now);
        }

        // The one post: checked, computed, every number and the time changed together, then saved at
        // once (a second press finds the 24 hours not over and changes nothing).
        public bool TryPost(out NicoTubeChannel.PostResult result)
        {
            result = default;
            var now = NowMs;
            if (!State.Channel.TryPost(State.Creator, now, out result))
            {
                PostRefusals++;
                return false;
            }
            PostsThisSession++;
            LastPost = result;
            Changed();
            if (saveSystem != null) saveSystem.Write("nicotube post", Time.unscaledTime);
            EvaluateNpcs("post");
            return true;
        }

        private sealed class NicoPage : IGameMenuPage
        {
            private readonly PocLife owner;
            private readonly List<string> lines = new List<string>();
            private readonly List<string> buttons = new List<string>();
            public bool ShowingResult { get; private set; }

            public NicoPage(PocLife life) => owner = life;
            public string Title => NicoName;
            public IReadOnlyList<string> Lines
            {
                get
                {
                    if (!ShowingResult) Fill(); // the wait counts down while the page is open
                    return lines;
                }
            }
            public IReadOnlyList<string> Buttons => buttons;

            public void ShowChannel()
            {
                ShowingResult = false;
                Fill();
            }

            private void Fill()
            {
                var ch = owner.State.Channel;
                lines.Clear();
                lines.Add("とうろくしゃ " + LifeNumbers.Short(ch.Subscribers) + "にん");
                lines.Add("さいせい " + LifeNumbers.Short(ch.TotalViews) + "かい");
                lines.Add("とうこう " + ch.Posts.ToString(CultureInfo.InvariantCulture) + "かい");
                lines.Add("おかね " + LifeNumbers.Short(ch.Money));
                lines.Add(owner.CooldownLine(owner.NowMs));
                buttons.Clear();
                buttons.Add(PostLabel);
                buttons.Add(CloseLabel);
            }

            public void Press(int index, float now)
            {
                if (ShowingResult || index != 0)
                {
                    owner.menu.SetOpen(false, now);
                    return;
                }
                if (!owner.TryPost(out var result))
                {
                    if (owner.hud != null) owner.hud.ShowMessage(owner.CooldownLine(owner.NowMs));
                    return;
                }
                ShowingResult = true;
                lines.Clear();
                lines.Add(PostedLine);
                lines.Add("さいせい +" + LifeNumbers.Short(result.Views) + "かい");
                lines.Add("とうろくしゃ +" + LifeNumbers.Short(result.NewSubscribers) + "にん");
                lines.Add("おかね +" + LifeNumbers.Short(result.Money));
                lines.Add(owner.CooldownLine(owner.NowMs));
                buttons.Clear();
                buttons.Add("とじる");
            }
        }

        // ---------------------------------------------------------------- the creator's numbers page

        private SkillsPage skillsPage;

        public void OpenSkills(float now)
        {
            if (menu == null) return;
            skillsPage ??= new SkillsPage(this);
            menu.OpenPage(skillsPage, now);
        }

        private sealed class SkillsPage : IGameMenuPage
        {
            private readonly PocLife owner;
            private readonly List<string> lines = new List<string>();
            private readonly List<string> buttons = new List<string> { CloseLabel };
            public SkillsPage(PocLife life) => owner = life;
            public string Title => "のうりょく";
            public IReadOnlyList<string> Lines
            {
                get
                {
                    var s = owner.State;
                    lines.Clear();
                    for (var i = 0; i < CreatorState.SkillCount; i++)
                        lines.Add(CreatorState.SkillNames[i] + " " + LifeNumbers.Short(s.Creator.Skills[i]));
                    lines.Add(NicoName + " " + LifeNumbers.Short(s.Channel.Subscribers) + "にん");
                    lines.Add("おかね " + LifeNumbers.Short(s.Channel.Money));
                    lines.Add(owner.ClockText);
                    return lines;
                }
            }
            public IReadOnlyList<string> Buttons => buttons;
            public void Press(int index, float now) => owner.menu.ClosePage(now);
        }

        // ---------------------------------------------------------------- shared helpers

        private bool IsDoorOrTransition(Vector2Int cell) =>
            session.OutdoorWorld != null && session.IsTransitionCell(session.OutdoorWorld, cell);

        // A cell a character may step into or stand on, outdoors: walkable, not a door, not hers, not
        // the crow's, not another character's.
        public bool CharacterCanEnter(Vector2Int cell, PocWalker who)
        {
            var world = session.OutdoorWorld;
            if (world == null || !world.CanEnter(cell) || IsDoorOrTransition(cell)) return false;
            if (session.World == world && session.PlayerReserves(cell)) return false;
            if (session.World != world && entryCell.HasValue && cell == entryCell.Value) return false;
            if (crow != null && crow.isActiveAndEnabled && crow.ReservesGround(cell)) return false;
            return !WalkersReserveExcept(cell, who);
        }

        // Breadth-first path inside a box (keeps searches small), for a character.
        private readonly List<Vector2Int> pathScratch = new List<Vector2Int>();

        public bool TryPath(PocWalker who, Vector2Int goal, int margin, List<Vector2Int> into)
        {
            into.Clear();
            var start = who.Cell;
            var minX = Mathf.Min(start.x, goal.x) - margin;
            var maxX = Mathf.Max(start.x, goal.x) + margin;
            var minY = Mathf.Min(start.y, goal.y) - margin;
            var maxY = Mathf.Max(start.y, goal.y) + margin;
            return GridPathfinder2D.TryFind(start, goal, into,
                c => c.x >= minX && c.x <= maxX && c.y >= minY && c.y <= maxY && (c == goal ? session.OutdoorWorld.CanEnter(c) : CharacterCanEnter(c, who)));
        }

        // Cells reachable on foot from her start (computed once; spawns, hiding places, landings).
        private HashSet<Vector2Int> reachable;

        public HashSet<Vector2Int> Reachable
        {
            get
            {
                if (reachable != null) return reachable;
                reachable = new HashSet<Vector2Int>();
                var world = session.OutdoorWorld;
                if (world == null) return reachable;
                Vector2Int start;
                if (world.TryGetHouseDoor(out var door)) start = door + Vector2Int.down;
                else start = Vector2Int.zero;
                var queue = new Queue<Vector2Int>();
                if (world.CanEnter(start)) { reachable.Add(start); queue.Enqueue(start); }
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var n = c + d;
                        if (reachable.Contains(n) || !world.CanEnter(n)) continue;
                        reachable.Add(n);
                        queue.Enqueue(n);
                    }
                }
                return reachable;
            }
        }

        // The cells where someone using a piece of furniture or a sign stands (kept free: passers-by
        // never stand there, hiders never hide there).
        private HashSet<Vector2Int> actionCells;

        public HashSet<Vector2Int> ActionCells
        {
            get
            {
                if (actionCells != null) return actionCells;
                actionCells = new HashSet<Vector2Int>();
                var world = session.OutdoorWorld;
                if (world == null) return actionCells;
                foreach (var instance in world.Objects)
                {
                    var definition = instance.Definition;
                    if (definition == null) continue;
                    foreach (var point in definition.ActionPoints) actionCells.Add(instance.RootCell + point.PlayerCellOffset);
                    if (definition.Behavior == WorldObjectBehavior.HouseTransition)
                        foreach (var c in instance.BlockedCells()) { actionCells.Add(c + Vector2Int.down); actionCells.Add(c + Vector2Int.down * 2); }
                }
                return actionCells;
            }
        }

        public static ulong Mix(long a, long b)
        {
            unchecked
            {
                var z = (ulong)a * 0x9E3779B97F4A7C15UL + (ulong)b * 0xC2B2AE3D27D4EB4FUL + 0x165667B19E3779F9UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public static double Unit(long a, long b) => (Mix(a, b) >> 11) * (1.0 / 9007199254740992.0);

        // ---------------------------------------------------------------- development (dev builds only)

        // Page commands for the browser checks (PocDevStats forwards "life:..." in a development build
        // or the editor; a release build has none of this: 指示書 21 「開発用の…UIを公開Buildへ混入させない」).
        public string DevCommand(string command)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var parts = command.Split(':');
            if (parts.Length < 2) return "?";
            switch (parts[1])
            {
                case "subs":
                    if (parts.Length > 2 && LifeNumbers.TryParse(parts[2], out var subs)) { State.Channel.Subscribers = subs; Changed(); EvaluateNpcs("dev"); return "ok"; }
                    return "bad";
                case "skills":
                    if (parts.Length > 2)
                    {
                        var values = parts[2].Split(',');
                        for (var i = 0; i < Math.Min(3, values.Length); i++)
                            if (LifeNumbers.TryParse(values[i], out var v)) State.Creator.Skills[i] = v;
                        Changed();
                        return "ok";
                    }
                    return "bad";
                case "ready":
                    // the 24 game hours as over (the reference time moved back by a real 20 minutes)
                    if (State.Channel.LastPostUnixMs > 0) State.Channel.LastPostUnixMs = NowMs - LifeClock.RealMilliseconds(NicoTubeTuning.CooldownGameMinutes);
                    else State.Channel.BasisUnixMs = NowMs - LifeClock.RealMilliseconds(NicoTubeTuning.CooldownGameMinutes);
                    Changed();
                    return "ok";
                case "clock":
                    // move the world clock (both World Time and this) by game minutes: "life:clock:90"
                    if (parts.Length > 2 && long.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var minutes) && worldTime != null)
                    {
                        DevClockOffsetMs += LifeClock.RealMilliseconds(Math.Abs(minutes)) * Math.Sign(minutes);
                        var offset = DevClockOffsetMs;
                        worldTime.ClockMilliseconds = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + offset;
                        return "ok";
                    }
                    return "bad";
                case "npceval": EvaluateNpcs("dev"); return "ok";
                // a passer-by of this kind 3-6 cells from her (pictures of each kind): "life:npchere:fan_male"
                case "npchere":
                    if (parts.Length > 2 && Array.IndexOf(NpcKinds, parts[2]) >= 0) return SpawnNpcNear(parts[2]) ? "ok" : "no cell";
                    return "bad";
                case "npcclear": ClearNpcs(); return "ok";
                case "hint": hideSeekHintAt = 0f; return "ok";
            }
            return "?";
#else
            return "release";
#endif
        }

        // The numbers the browser checks read (window.__halkaDev.life), development pages only.
        public string DevJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder(1024);
            var ch = State.Channel;
            var now = NowMs;
            b.Append('{');
            b.Append("\"day\":").Append(ShownDay.ToString(inv)).Append(",\"minute\":").Append(MinuteOfDay.ToString(inv));
            b.Append(",\"clock\":\"").Append(ClockText).Append("\",\"night\":").Append(IsNightNow ? 1 : 0);
            b.Append(",\"phase\":\"").Append(worldTime != null ? worldTime.CurrentPhase.ToString() : "").Append('"');
            for (var i = 0; i < CreatorState.SkillCount; i++)
                b.Append(",\"").Append(CreatorState.SkillIds[i]).Append("\":\"").Append(LifeNumbers.ToSave(State.Creator.Skills[i])).Append('"')
                    .Append(",\"").Append(CreatorState.SkillIds[i]).Append("Plays\":").Append(State.Creator.Plays[i].ToString(inv))
                    .Append(",\"").Append(CreatorState.SkillIds[i]).Append("Best\":").Append(State.Creator.Best[i].ToString(inv))
                    .Append(",\"").Append(CreatorState.SkillIds[i]).Append("Last\":").Append(State.Creator.Last[i].ToString(inv));
            b.Append(",\"subs\":\"").Append(LifeNumbers.ToSave(ch.Subscribers)).Append("\",\"views\":\"").Append(LifeNumbers.ToSave(ch.TotalViews))
                .Append("\",\"money\":\"").Append(LifeNumbers.ToSave(ch.Money)).Append("\",\"posts\":").Append(ch.Posts.ToString(inv))
                .Append(",\"cooldown\":").Append(ch.RemainingGameMinutes(now).ToString(inv)).Append(",\"canPost\":").Append(ch.CanPost(now) ? 1 : 0)
                .Append(",\"postRefusals\":").Append(PostRefusals.ToString(inv))
                .Append(",\"lastPostViews\":\"").Append(LifeNumbers.ToSave(LastPost.Views)).Append("\",\"lastPostSubs\":\"").Append(LifeNumbers.ToSave(LastPost.NewSubscribers))
                .Append("\",\"lastPostMoney\":\"").Append(LifeNumbers.ToSave(LastPost.Money)).Append('"');
            b.Append(",\"lifeVersion\":").Append(LifeVersion.ToString(inv)).Append(",\"start\":\"").Append(StartSource).Append('"');
            b.Append(",\"games\":").Append(GamesFinished.ToString(inv)).Append(",\"gamesCancelled\":").Append(GamesCancelled.ToString(inv));
            b.Append(",\"minigame\":").Append(minigames != null ? minigames.DevJson() : "null");
            AppendNpcJson(b);
            AppendCompanionJson(b);
            AppendStationJson(b);
            b.Append('}');
            return b.ToString();
        }
    }
}
