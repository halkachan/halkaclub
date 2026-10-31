using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halka.Game.UI;
using UnityEngine;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: the three creator mini games (指示書 5-8), one window over the game (the world, World
    // Time and the crow carry on behind it; her input and AUTO wait: PocGameplay.ActivityOpen).
    //   DTM (作曲力)   4 lanes, notes fall to the line; D F J K or the arrow keys, a click or a tap
    //                  on a lane; 24 s at 120 BPM; 100 % for a hit within 0.08 s, 60 % within 0.16 s.
    //   おえかき (画力) trace the grey model (star, heart, flower, circle, cat) with the mouse or a
    //                  finger; 25 s or できた; how much of the model the line covers and how much of
    //                  the line is on the model (a finger gets a wider tolerance).
    //   うた (歌唱力)   keep the voice (a dot) on the melody bar with ↑ ↓ (W S) or by dragging; 24 s;
    //                  how close it stays. No microphone (指示書 8.3: a later addition).
    // Each: the difficulty first (かんたん / ふつう / やばい, or やめる), 0-100 points, やめる at any time
    // (nothing is given or saved), then the result (「さっきょくした！」「さっきょくりょく +4」) and とじる.
    // ふつう is the first version's game; かんたん is slower and kinder, やばい faster, tighter and
    // denser (CreatorState: かんたん +0〜3, ふつう +0〜5, やばい +0〜12 by the same score bands).
    // The sounds are made here (sine tones and a click), no recorded music.
    public sealed class PocMinigames : MonoBehaviour
    {
        public enum Phase { Closed, Intro, Playing, Result }

        public const float DtmSeconds = 24f;
        public const float DrawSeconds = 25f;
        public const float SingSeconds = 24f;
        public const float NoteTravelSeconds = 1.6f;
        public const float PerfectWindow = 0.08f;
        public const float GoodWindow = 0.16f;

        // かんたん / ふつう / やばい (ふつう: the constants above).
        private static readonly float[] TravelByDifficulty = { 2.1f, NoteTravelSeconds, 1.15f };
        private static readonly float[] PerfectByDifficulty = { 0.1f, PerfectWindow, 0.06f };
        private static readonly float[] GoodByDifficulty = { 0.2f, GoodWindow, 0.12f };
        private static readonly float[] DrawSecondsByDifficulty = { 30f, DrawSeconds, 18f };
        private static readonly float[] ToleranceByDifficulty = { 0.14f, 0.1f, 0.07f };
        private static readonly float[] SingToleranceByDifficulty = { 1.7f, 1.25f, 0.85f };
        private static readonly float[] SingGapByDifficulty = { 1.9f, 1.2f, 0.7f };
        private static readonly float[] SingGapRandomByDifficulty = { 0.6f, 0.6f, 0.4f };
        private static readonly int[] SingJumpByDifficulty = { 1, 2, 3 };

        public MinigameDifficulty Difficulty { get; private set; } = MinigameDifficulty.Normal;
        public float NoteTravel => TravelByDifficulty[(int)Difficulty];
        public float PerfectWindowNow => PerfectByDifficulty[(int)Difficulty];
        public float GoodWindowNow => GoodByDifficulty[(int)Difficulty];
        public float DrawSecondsNow => DrawSecondsByDifficulty[(int)Difficulty];
        public float SingTolerance => SingToleranceByDifficulty[(int)Difficulty];

        [SerializeField] private Font fallbackFont;

        public Phase Current { get; private set; } = Phase.Closed;
        public CreatorSkill Skill { get; private set; }
        public bool IsOpen => Current != Phase.Closed;
        public int LastScore { get; private set; } = -1;
        public int LastGain { get; private set; }
        public int Plays { get; private set; }
        public string Subject { get; private set; } = "";

        private Func<CreatorSkill, int, int> finished;
        private Action<bool> closed;
        private float startedAt;
        private bool touchUsed;
        private AudioSource audioSource;
        private AudioClip hitClip;
        private AudioClip tickClip;
        private AudioClip toneClip;
        private AudioSource toneSource;

        public void ConfigureFont(Font font) => fallbackFont = font;

        public bool Begin(CreatorSkill skill, Func<CreatorSkill, int, int> onFinished, Action<bool> onClosed)
        {
            if (IsOpen) return false;
            Skill = skill;
            finished = onFinished;
            closed = onClosed;
            Current = Phase.Intro;
            LastScore = -1;
            LastGain = 0;
            touchUsed = false;
            EnsureAudio();
            return true;
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            StopTone();
            var wasResult = Current == Phase.Result;
            Current = Phase.Closed;
            closed?.Invoke(!wasResult);
        }

        private void StartPlaying(MinigameDifficulty difficulty)
        {
            Difficulty = difficulty;
            Current = Phase.Playing;
            startedAt = Time.unscaledTime;
            Plays++;
            var seed = Plays * 7919 + (int)(DateTime.UtcNow.Ticks & 0xFFFF);
            if (Skill == CreatorSkill.Composition) SetupDtm(seed);
            else if (Skill == CreatorSkill.Drawing) SetupDrawing(seed);
            else SetupSinging(seed);
        }

        private void Finish(int score)
        {
            StopTone();
            LastScore = Mathf.Clamp(score, 0, 100);
            LastGain = finished != null ? finished(Skill, LastScore) : 0;
            Current = Phase.Result;
        }

        public float Elapsed => Current == Phase.Playing ? Time.unscaledTime - startedAt : 0f;

        // ---------------------------------------------------------------- layout (GUI pixels)

        private Rect Panel()
        {
            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            var w = Mathf.Min(620f, safe.width - 24f);
            var h = Mathf.Min(430f, safe.height - 24f);
            return new Rect(safe.xMin + (safe.width - w) * 0.5f, top + (safe.height - h) * 0.5f, w, h);
        }

        private Rect Board()
        {
            var p = Panel();
            return new Rect(p.x + 14f, p.y + 52f, p.width - 28f, p.height - 52f - 64f);
        }

        private Rect ButtonA() { var p = Panel(); return new Rect(p.x + 14f, p.yMax - 54f, p.width * 0.5f - 21f, 42f); }
        private Rect ButtonB() { var p = Panel(); return new Rect(p.center.x + 7f, p.yMax - 54f, p.width * 0.5f - 21f, 42f); }
        // The result has one button (とじる) across the bottom.
        private Rect ButtonWide() { var p = Panel(); return new Rect(p.x + 14f, p.yMax - 54f, p.width - 28f, 42f); }
        // The first screen: かんたん / ふつう / やばい in a row above やめる.
        private Rect DifficultyButton(int i)
        {
            var p = Panel();
            var w = (p.width - 28f - 20f) / 3f;
            return new Rect(p.x + 14f + i * (w + 10f), p.yMax - 54f - 52f, w, 42f);
        }

        private static Vector2 Gui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);

        // ---------------------------------------------------------------- input

        private readonly List<(int id, Vector2 gui)> began = new List<(int, Vector2)>();
        private readonly List<(int id, Vector2 gui)> held = new List<(int, Vector2)>();
        private readonly List<(int id, Vector2 gui)> ended = new List<(int, Vector2)>();
        private bool mouseHeld;

        private void ReadPointers()
        {
            began.Clear();
            held.Clear();
            ended.Clear();
            for (var i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                touchUsed = true;
                var g = Gui(t.position);
                if (t.phase == TouchPhase.Began) began.Add((t.fingerId, g));
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) ended.Add((t.fingerId, g));
                else held.Add((t.fingerId, g));
            }
            if (Input.touchCount > 0) return;
            var m = Gui(Input.mousePosition);
            if (Input.GetMouseButtonDown(0)) { began.Add((-1, m)); mouseHeld = true; }
            if (Input.GetMouseButton(0) && mouseHeld) held.Add((-1, m));
            if (Input.GetMouseButtonUp(0) && mouseHeld) { ended.Add((-1, m)); mouseHeld = false; }
        }

        private bool Pressed(Rect rect)
        {
            foreach (var e in ended)
                if (rect.Contains(e.gui)) return true;
            return false;
        }

        private void Update()
        {
            if (!IsOpen) return;
            ReadPointers();
            var escape = Input.GetKeyDown(KeyCode.Escape);
            var enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z);
            switch (Current)
            {
                case Phase.Intro:
                    if (Pressed(DifficultyButton(0)) || Input.GetKeyDown(KeyCode.Alpha1)) StartPlaying(MinigameDifficulty.Easy);
                    else if (Pressed(DifficultyButton(1)) || enter || Input.GetKeyDown(KeyCode.Alpha2)) StartPlaying(MinigameDifficulty.Normal);
                    else if (Pressed(DifficultyButton(2)) || Input.GetKeyDown(KeyCode.Alpha3)) StartPlaying(MinigameDifficulty.Hard);
                    else if (Pressed(ButtonWide()) || escape) Cancel();
                    break;
                case Phase.Playing:
                    if (Pressed(ButtonB()) || escape) { Cancel(); break; }
                    if (Skill == CreatorSkill.Composition) UpdateDtm();
                    else if (Skill == CreatorSkill.Drawing) UpdateDrawing();
                    else UpdateSinging();
                    break;
                case Phase.Result:
                    if (Pressed(ButtonWide()) || enter || escape) Cancel();
                    break;
            }
        }

        // ---------------------------------------------------------------- DTM

        public sealed class Note { public float Time; public int Lane; public int Judged; public float Error; } // Judged: 0 open, 1 perfect, 2 good, 3 miss
        private readonly List<Note> notes = new List<Note>();
        private float nextBeat;
        private static readonly KeyCode[][] LaneKeys =
        {
            new[] { KeyCode.D, KeyCode.LeftArrow }, new[] { KeyCode.F, KeyCode.DownArrow },
            new[] { KeyCode.J, KeyCode.UpArrow }, new[] { KeyCode.K, KeyCode.RightArrow },
        };
        public IReadOnlyList<Note> Notes => notes;
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Missed { get; private set; }

        private void SetupDtm(int seed)
        {
            notes.Clear();
            Perfect = Good = Missed = 0;
            var rng = new System.Random(seed);
            // 120 BPM, a bar of rest first, then notes on beats and some half beats, never two at once.
            // かんたん: beats only, fewer; やばい: more beats and half beats, and some quarter beats.
            var step = Difficulty == MinigameDifficulty.Hard ? 0.25f : 0.5f;
            for (var t = 2f; t <= DtmSeconds - 1.5f; t += step)
            {
                var quarter = Mathf.RoundToInt(t / 0.25f);
                double chance;
                if (quarter % 2 != 0) chance = 0.3;
                else if ((quarter / 2) % 2 == 0)
                    chance = Difficulty == MinigameDifficulty.Easy ? 0.7 : Difficulty == MinigameDifficulty.Hard ? 0.95 : 0.85;
                else chance = Difficulty == MinigameDifficulty.Easy ? 0.0 : Difficulty == MinigameDifficulty.Hard ? 0.6 : 0.35;
                if (rng.NextDouble() < chance)
                    notes.Add(new Note { Time = t, Lane = rng.Next(4) });
            }
            nextBeat = 0f;
        }

        private Rect LaneRect(int lane)
        {
            var b = Board();
            var w = b.width / 4f;
            return new Rect(b.x + lane * w, b.y, w, b.height);
        }

        private float JudgeY() { var b = Board(); return b.yMax - 40f; }

        private void UpdateDtm()
        {
            var t = Elapsed;
            if (t >= nextBeat)
            {
                PlayClip(tickClip, 0.35f);
                nextBeat += 0.5f;
            }
            for (var lane = 0; lane < 4; lane++)
            {
                var hit = false;
                foreach (var key in LaneKeys[lane]) hit |= Input.GetKeyDown(key);
                foreach (var e in began) hit |= LaneRect(lane).Contains(e.gui) && !ButtonB().Contains(e.gui);
                if (hit) JudgeLane(lane, t);
            }
            foreach (var n in notes)
                if (n.Judged == 0 && t > n.Time + GoodWindowNow) { n.Judged = 3; Missed++; }
            if (t >= DtmSeconds) Finish(DtmScore());
        }

        private void JudgeLane(int lane, float t)
        {
            Note best = null;
            foreach (var n in notes)
                if (n.Judged == 0 && n.Lane == lane && Mathf.Abs(n.Time - t) <= GoodWindowNow && (best == null || Mathf.Abs(n.Time - t) < Mathf.Abs(best.Time - t)))
                    best = n;
            if (best == null) return;
            best.Error = t - best.Time;
            if (Mathf.Abs(best.Error) <= PerfectWindowNow) { best.Judged = 1; Perfect++; }
            else { best.Judged = 2; Good++; }
            PlayClip(hitClip, 0.6f, 1f + best.Lane * 0.125f);
        }

        public int DtmScore()
        {
            if (notes.Count == 0) return 0;
            var sum = 0f;
            foreach (var n in notes) sum += n.Judged == 1 ? 1f : n.Judged == 2 ? 0.6f : 0f;
            return Mathf.RoundToInt(100f * sum / notes.Count);
        }

        // ---------------------------------------------------------------- drawing

        public static readonly string[] Subjects = { "ほし", "ハート", "はな", "まる", "ねこ" };
        private readonly List<Vector2> model = new List<Vector2>();      // the shape, -1..1 (y up)
        private readonly List<List<Vector2>> strokes = new List<List<Vector2>>();
        private int drawFinger = int.MinValue;
        public IReadOnlyList<Vector2> Model => model;
        public int StrokePoints { get; private set; }

        private void SetupDrawing(int seed)
        {
            var pick = (seed & 0x7FFFFFFF) % Subjects.Length;
            Subject = Subjects[pick];
            model.Clear();
            strokes.Clear();
            StrokePoints = 0;
            drawFinger = int.MinValue;
            model.AddRange(Shape(pick));
        }

        // Outlines sampled every few degrees, inside -0.85..0.85.
        public static List<Vector2> Shape(int which)
        {
            var points = new List<Vector2>();
            const int n = 160;
            for (var i = 0; i <= n; i++)
            {
                var a = i / (float)n * Mathf.PI * 2f;
                Vector2 p;
                switch (which)
                {
                    case 0: // star: radius between the points and the dents, 5 points
                    {
                        var k = (i % (n / 10)) / (float)(n / 10);
                        var tip = (i / (n / 10)) % 2 == 0;
                        var r0 = tip ? 0.85f : 0.36f;
                        var r1 = tip ? 0.36f : 0.85f;
                        var a0 = Mathf.Floor(i / (float)(n / 10)) * Mathf.PI / 5f + Mathf.PI / 2f;
                        var a1 = a0 + Mathf.PI / 5f;
                        var q0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r0;
                        var q1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
                        p = Vector2.Lerp(q0, q1, k);
                        break;
                    }
                    case 1: // heart
                    {
                        var x = 16f * Mathf.Pow(Mathf.Sin(a), 3f);
                        var y = 13f * Mathf.Cos(a) - 5f * Mathf.Cos(2f * a) - 2f * Mathf.Cos(3f * a) - Mathf.Cos(4f * a);
                        p = new Vector2(x, y + 2.5f) / 19f;
                        break;
                    }
                    case 2: // flower: five round petals
                    {
                        var r = 0.5f + 0.32f * Mathf.Abs(Mathf.Cos(2.5f * a));
                        p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        break;
                    }
                    case 3: p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.75f; break; // circle
                    default: // cat head: a round face with two ears
                    {
                        var r = 0.62f;
                        var ear = 0f;
                        foreach (var centre in new[] { Mathf.PI * 0.30f, Mathf.PI * 0.70f })
                        {
                            var d = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, centre * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                            ear = Mathf.Max(ear, Mathf.Max(0f, 1f - d / 0.28f) * 0.32f);
                        }
                        p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + ear) + new Vector2(0f, -0.08f);
                        break;
                    }
                }
                points.Add(p);
            }
            return points;
        }

        // The model's square on screen (GUI pixels).
        public Rect ModelRect()
        {
            var b = Board();
            var side = Mathf.Min(b.width, b.height) - 16f;
            return new Rect(b.center.x - side * 0.5f, b.center.y - side * 0.5f, side, side);
        }

        public Vector2 ModelToGui(Vector2 m)
        {
            var r = ModelRect();
            return new Vector2(r.center.x + m.x * r.width * 0.5f, r.center.y - m.y * r.height * 0.5f);
        }

        private Vector2 GuiToModel(Vector2 g)
        {
            var r = ModelRect();
            return new Vector2((g.x - r.center.x) / (r.width * 0.5f), -(g.y - r.center.y) / (r.height * 0.5f));
        }

        private void UpdateDrawing()
        {
            if (Pressed(ButtonA())) { Finish(DrawingScore()); return; }
            foreach (var e in began)
                if (ModelRect().Contains(e.gui) && drawFinger == int.MinValue)
                {
                    drawFinger = e.id;
                    strokes.Add(new List<Vector2> { GuiToModel(e.gui) });
                    StrokePoints++;
                }
            foreach (var h in held)
                if (h.id == drawFinger && strokes.Count > 0)
                {
                    var m = GuiToModel(h.gui);
                    var s = strokes[strokes.Count - 1];
                    if ((s[s.Count - 1] - m).sqrMagnitude > 0.0004f) { s.Add(m); StrokePoints++; }
                }
            foreach (var e in ended)
                if (e.id == drawFinger) drawFinger = int.MinValue;
            if (Elapsed >= DrawSecondsNow) Finish(DrawingScore());
        }

        public float Tolerance => ToleranceByDifficulty[(int)Difficulty] * (touchUsed ? 1.3f : 1f);

        // Coverage: share of the model's points with the line within the tolerance; precision: share
        // of the line's points within the tolerance of the model. 70 % / 30 %.
        public int DrawingScore()
        {
            var line = new List<Vector2>();
            foreach (var s in strokes)
                for (var i = 0; i < s.Count; i++)
                {
                    line.Add(s[i]);
                    if (i + 1 < s.Count) // fill gaps so a fast stroke still covers
                    {
                        var steps = Mathf.CeilToInt((s[i + 1] - s[i]).magnitude / 0.03f);
                        for (var k = 1; k < steps; k++) line.Add(Vector2.Lerp(s[i], s[i + 1], k / (float)steps));
                    }
                }
            if (line.Count < 8 || model.Count == 0) return 0;
            var tol2 = Tolerance * Tolerance;
            var covered = 0;
            foreach (var m in model)
                foreach (var p in line)
                    if ((m - p).sqrMagnitude <= tol2) { covered++; break; }
            var onModel = 0;
            foreach (var p in line)
                foreach (var m in model)
                    if ((m - p).sqrMagnitude <= tol2) { onModel++; break; }
            var coverage = covered / (float)model.Count;
            var precision = onModel / (float)line.Count;
            return Mathf.Clamp(Mathf.RoundToInt(100f * (0.7f * coverage + 0.3f * precision)), 0, 100);
        }

        // ---------------------------------------------------------------- singing

        private readonly List<(float start, float level)> melody = new List<(float, float)>();
        public float Voice { get; private set; } = 3.5f;      // 0..7
        private float closeness;
        private float counted;
        public float TargetNow { get; private set; } = -1f;
        public const float Levels = 7f;

        private void SetupSinging(int seed)
        {
            melody.Clear();
            var rng = new System.Random(seed);
            var level = 3f + rng.Next(3);
            var d = (int)Difficulty;
            for (var t = 1.5f; t < SingSeconds; t += SingGapByDifficulty[d] + (float)rng.NextDouble() * SingGapRandomByDifficulty[d])
            {
                melody.Add((t, level));
                level = Mathf.Clamp(level + rng.Next(-SingJumpByDifficulty[d], SingJumpByDifficulty[d] + 1), 0f, Levels);
            }
            Voice = 3.5f;
            closeness = counted = 0f;
            StartTone();
        }

        public float MelodyAt(float t)
        {
            var level = -1f;
            foreach (var (start, l) in melody)
                if (t >= start) level = l;
            return level;
        }

        private Rect TrackRect() => Board();

        private void UpdateSinging()
        {
            var t = Elapsed;
            var dt = Time.unscaledDeltaTime;
            var up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            var down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            if (up) Voice += dt * 5f;
            if (down) Voice -= dt * 5f;
            var track = TrackRect();
            foreach (var h in held)
                if (track.Contains(h.gui) && !ButtonB().Contains(h.gui))
                    Voice = Mathf.Lerp(Voice, (track.yMax - h.gui.y) / track.height * (Levels + 1f) - 0.5f, Mathf.Clamp01(dt * 14f));
            Voice = Mathf.Clamp(Voice, 0f, Levels);
            if (toneSource != null) toneSource.pitch = Mathf.Pow(2f, (Voice - 3.5f) * 2f / 12f);
            TargetNow = MelodyAt(t);
            if (TargetNow >= 0f)
            {
                closeness += Mathf.Clamp01(1f - Mathf.Abs(Voice - TargetNow) / SingTolerance) * dt;
                counted += dt;
            }
            if (t >= SingSeconds) Finish(SingingScore());
        }

        public int SingingScore() => counted <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(100f * closeness / counted), 0, 100);

        // ---------------------------------------------------------------- sound

        private void EnsureAudio()
        {
            if (audioSource != null) return;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            const int rate = 22050;
            hitClip = Tone("v40 hit", rate, 880f, 0.09f, 0.5f);
            tickClip = Tone("v40 tick", rate, 1760f, 0.025f, 0.35f);
            toneClip = Tone("v40 voice", rate, 440f, 0.5f, 0.18f, true);
        }

        private static AudioClip Tone(string name, int rate, float frequency, float seconds, float volume, bool loop = false)
        {
            var count = Mathf.CeilToInt(rate * seconds);
            if (loop) count = Mathf.RoundToInt(rate / frequency) * Mathf.Max(1, Mathf.RoundToInt(frequency * seconds)); // whole periods
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var env = loop ? 1f : Mathf.Clamp01(1f - i / (float)count);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / rate) * volume * env;
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void PlayClip(AudioClip clip, float volume, float pitch = 1f)
        {
            if (audioSource == null || clip == null) return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }

        private void StartTone()
        {
            if (toneSource == null)
            {
                toneSource = gameObject.AddComponent<AudioSource>();
                toneSource.loop = true;
                toneSource.playOnAwake = false;
                toneSource.volume = 0.5f;
            }
            toneSource.clip = toneClip;
            toneSource.Play();
        }

        private void StopTone()
        {
            if (toneSource != null) toneSource.Stop();
        }

        // ---------------------------------------------------------------- drawing the window

        private static readonly Color Ink = new Color32(0x25, 0x2b, 0x39, 0xff);
        private static readonly Color Paper = new Color(0.97f, 0.96f, 0.91f);

        private static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void Text(Rect r, string text, int size, TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null) =>
            GameBitmapFont.Draw(r, text, size, color ?? Ink, anchor, fallbackFont);

        private void Button(Rect r, string label)
        {
            Fill(r, Ink);
            Fill(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Color.white);
            Text(r, label, 20);
        }

        public static string GameTitle(CreatorSkill skill) =>
            skill == CreatorSkill.Composition ? "さっきょく" : skill == CreatorSkill.Drawing ? "おえかき" : "うた";

        public static string DoneLine(CreatorSkill skill) =>
            skill == CreatorSkill.Composition ? "さっきょくした！" : skill == CreatorSkill.Drawing ? "おえかきした！" : "うたった！";

        private void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -200;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
            var p = Panel();
            Fill(p, Ink);
            Fill(new Rect(p.x + 3f, p.y + 3f, p.width - 6f, p.height - 6f), Paper);
            Text(new Rect(p.x + 12f, p.y + 8f, p.width - 24f, 34f), GameTitle(Skill) + (Skill == CreatorSkill.Drawing && Current != Phase.Intro ? "：" + Subject : "") +
                (Current != Phase.Intro ? "（" + CreatorState.DifficultyNames[(int)Difficulty] + "）" : ""), 26);
            switch (Current)
            {
                case Phase.Intro: DrawIntro(); break;
                case Phase.Playing:
                    if (Skill == CreatorSkill.Composition) DrawDtm();
                    else if (Skill == CreatorSkill.Drawing) DrawDrawing();
                    else DrawSinging();
                    if (Skill == CreatorSkill.Drawing) Button(ButtonA(), "できた");
                    Button(ButtonB(), "やめる");
                    var left = Mathf.Max(0f, (Skill == CreatorSkill.Composition ? DtmSeconds : Skill == CreatorSkill.Drawing ? DrawSecondsNow : SingSeconds) - Elapsed);
                    Text(new Rect(p.xMax - 92f, p.y + 10f, 80f, 30f), Mathf.CeilToInt(left).ToString(CultureInfo.InvariantCulture) + "びょう", 18, TextAnchor.MiddleRight);
                    break;
                case Phase.Result:
                    var b = Board();
                    Text(new Rect(b.x, b.y + b.height * 0.12f, b.width, 44f), DoneLine(Skill), 34);
                    Text(new Rect(b.x, b.y + b.height * 0.12f + 56f, b.width, 36f), CreatorState.SkillNames[(int)Skill] + " +" + LastGain, 28);
                    Text(new Rect(b.x, b.y + b.height * 0.12f + 104f, b.width, 32f), "とくてん " + LastScore + "てん", 22);
                    Button(ButtonWide(), "とじる");
                    break;
            }
        }

        private void DrawIntro()
        {
            var b = Board();
            string[] lines;
            if (Skill == CreatorSkill.Composition)
                lines = new[] { "ながれてくる おとが せんに きたら", "D F J K（やじるし）か タップで たたく！", "やく24びょう" };
            else if (Skill == CreatorSkill.Drawing)
                lines = new[] { "うすい せんを なぞって かこう！", "マウスで ドラッグ / ゆびで なぞる", "できたら「できた」" };
            else
                lines = new[] { "まるを メロディの バーに あわせよう！", "↑↓（W S）か ゆびで うえしたに", "やく24びょう" };
            for (var i = 0; i < lines.Length; i++) Text(new Rect(b.x, b.y + 20f + i * 40f, b.width, 34f), lines[i], 20);
            Text(new Rect(b.x, b.y + 20f + lines.Length * 40f, b.width, 30f), "むずかしさを えらんでね", 18);
            for (var i = 0; i < 3; i++)
            {
                var r = DifficultyButton(i);
                var difficulty = (MinigameDifficulty)i;
                Text(new Rect(r.x, r.y - 26f, r.width, 24f), "+0～" + CreatorState.MaxGain(difficulty).ToString(CultureInfo.InvariantCulture), 16);
                Button(r, CreatorState.DifficultyNames[i]);
            }
            Button(ButtonWide(), "やめる");
        }

        private void DrawDtm()
        {
            var b = Board();
            Fill(b, new Color(0.18f, 0.2f, 0.27f));
            var judge = JudgeY();
            var t = Elapsed;
            for (var lane = 0; lane < 4; lane++)
            {
                var r = LaneRect(lane);
                Fill(new Rect(r.xMax - 1f, r.y, 2f, r.height), new Color(1f, 1f, 1f, 0.15f));
                Text(new Rect(r.x, judge + 8f, r.width, 28f), new[] { "D", "F", "J", "K" }[lane], 18, TextAnchor.MiddleCenter, Color.white);
            }
            Fill(new Rect(b.x, judge - 2f, b.width, 4f), new Color(0.98f, 0.85f, 0.35f));
            var colors = new[] { new Color(0.95f, 0.45f, 0.5f), new Color(0.45f, 0.8f, 0.55f), new Color(0.45f, 0.65f, 0.95f), new Color(0.95f, 0.8f, 0.4f) };
            foreach (var n in notes)
            {
                if (n.Judged != 0) continue;
                var dt = n.Time - t;
                if (dt > NoteTravel || dt < -GoodWindowNow) continue;
                var y = judge - dt / NoteTravel * (judge - b.y);
                var r = LaneRect(n.Lane);
                Fill(new Rect(r.x + 8f, y - 8f, r.width - 16f, 16f), colors[n.Lane]);
            }
            Text(new Rect(b.x + 8f, b.y + 6f, b.width - 16f, 24f), "パーフェクト " + Perfect + "  グッド " + Good + "  ミス " + Missed, 16, TextAnchor.MiddleLeft, Color.white);
        }

        private void DrawDrawing()
        {
            var b = Board();
            Fill(b, Color.white);
            for (var i = 0; i + 1 < model.Count; i += 2) Dot(ModelToGui(model[i]), 5f, new Color(0.75f, 0.77f, 0.82f));
            foreach (var s in strokes)
                for (var i = 0; i < s.Count; i++)
                {
                    Dot(ModelToGui(s[i]), 6f, Ink);
                    if (i + 1 < s.Count)
                    {
                        var a = ModelToGui(s[i]);
                        var c = ModelToGui(s[i + 1]);
                        var steps = Mathf.CeilToInt((c - a).magnitude / 3f);
                        for (var k = 1; k < steps; k++) Dot(Vector2.Lerp(a, c, k / (float)steps), 6f, Ink);
                    }
                }
        }

        private static void Dot(Vector2 g, float size, Color c) => Fill(new Rect(g.x - size * 0.5f, g.y - size * 0.5f, size, size), c);

        private void DrawSinging()
        {
            var r = TrackRect();
            Fill(r, new Color(0.93f, 0.95f, 1f));
            var now = Elapsed;
            var nowX = r.x + r.width * 0.3f;
            var step = r.height / (Levels + 1f);
            for (var l = 0; l <= (int)Levels; l++) Fill(new Rect(r.x, r.yMax - (l + 0.5f) * step, r.width, 1f), new Color(0f, 0f, 0f, 0.08f));
            const float window = 4f; // seconds across the track
            for (var x = 0f; x < r.width; x += 4f)
            {
                var t = now + (x - (nowX - r.x)) / r.width * window;
                var level = MelodyAt(t);
                if (level < 0f || t > SingSeconds) continue;
                Fill(new Rect(r.x + x, r.yMax - (level + 0.5f) * step - 5f, 4f, 10f), new Color(0.98f, 0.6f, 0.65f));
            }
            Fill(new Rect(nowX - 1f, r.y, 2f, r.height), new Color(0f, 0f, 0f, 0.25f));
            Dot(new Vector2(nowX, r.yMax - (Voice + 0.5f) * step), 18f, Ink);
            Dot(new Vector2(nowX, r.yMax - (Voice + 0.5f) * step), 12f, new Color(0.45f, 0.65f, 0.95f));
        }

        // ---------------------------------------------------------------- checks

        // For the browser checks (development pages): where things are (GUI pixels, top-left origin).
        public string DevJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder(512);
            b.Append("{\"phase\":\"").Append(Current).Append("\",\"skill\":\"").Append(Skill).Append("\",\"t\":").Append(Elapsed.ToString("0.###", inv))
                .Append(",\"score\":").Append(LastScore.ToString(inv)).Append(",\"gain\":").Append(LastGain.ToString(inv)).Append(",\"plays\":").Append(Plays.ToString(inv))
                .Append(",\"subject\":\"").Append(Subject).Append('"')
                .Append(",\"difficulty\":\"").Append(CreatorState.DifficultyIds[(int)Difficulty]).Append('"');
            void R(string name, Rect r) => b.Append(",\"").Append(name).Append("\":[").Append(r.x.ToString("0", inv)).Append(',').Append(r.y.ToString("0", inv))
                .Append(',').Append(r.width.ToString("0", inv)).Append(',').Append(r.height.ToString("0", inv)).Append(']');
            if (IsOpen)
            {
                // a: the button that goes on (ふつう on the first screen, とじる on the result); b: やめる
                R("a", Current == Phase.Result ? ButtonWide() : Current == Phase.Intro ? DifficultyButton(1) : ButtonA());
                R("b", Current == Phase.Intro ? ButtonWide() : ButtonB());
                if (Current == Phase.Intro)
                {
                    R("easy", DifficultyButton(0));
                    R("normal", DifficultyButton(1));
                    R("hard", DifficultyButton(2));
                }
                R("board", Board());
            }
            if (Current == Phase.Playing && Skill == CreatorSkill.Composition)
            {
                b.Append(",\"perfect\":").Append(Perfect.ToString(inv)).Append(",\"good\":").Append(Good.ToString(inv)).Append(",\"miss\":").Append(Missed.ToString(inv));
                b.Append(",\"notes\":[");
                var first = true;
                foreach (var n in notes)
                {
                    if (n.Judged != 0 || n.Time < Elapsed - GoodWindowNow) continue;
                    b.Append(first ? "" : ",").Append('[').Append(n.Time.ToString("0.###", inv)).Append(',').Append(n.Lane.ToString(inv)).Append(']');
                    first = false;
                }
                b.Append(']');
            }
            if (Current == Phase.Playing && Skill == CreatorSkill.Drawing)
            {
                R("model", ModelRect());
                b.Append(",\"strokePoints\":").Append(StrokePoints.ToString(inv)).Append(",\"shape\":[");
                for (var i = 0; i < model.Count; i += 4)
                {
                    var g = ModelToGui(model[i]);
                    b.Append(i > 0 ? "," : "").Append('[').Append(g.x.ToString("0", inv)).Append(',').Append(g.y.ToString("0", inv)).Append(']');
                }
                b.Append(']');
            }
            if (Current == Phase.Playing && Skill == CreatorSkill.Singing)
                b.Append(",\"voice\":").Append(Voice.ToString("0.##", inv)).Append(",\"target\":").Append(TargetNow.ToString("0.##", inv))
                    .Append(",\"melody\":[").Append(MelodyJson()).Append(']');
            b.Append('}');
            return b.ToString();
        }

        private string MelodyJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder();
            foreach (var (start, level) in melody)
                b.Append(b.Length > 0 ? "," : "").Append('[').Append(start.ToString("0.###", inv)).Append(',').Append(level.ToString("0", inv)).Append(']');
            return b.ToString();
        }

        // For the editor checks: the first screen's choice without a click (what the difficulty buttons do).
        public bool StartForCheck(MinigameDifficulty difficulty)
        {
            if (Current != Phase.Intro) return false;
            StartPlaying(difficulty);
            return true;
        }

        public int NoteCount => notes.Count;
        public int MelodyCount => melody.Count;

        // For the editor checks: the scores of made-up plays (no window, no input).
        public int ScoreDtmFor(IList<float> hitErrors, int noteCount)
        {
            notes.Clear();
            Perfect = Good = Missed = 0;
            for (var i = 0; i < noteCount; i++)
            {
                var n = new Note { Time = i, Lane = i % 4 };
                if (i < hitErrors.Count)
                {
                    var e = Mathf.Abs(hitErrors[i]);
                    n.Judged = e <= PerfectWindowNow ? 1 : e <= GoodWindowNow ? 2 : 3;
                }
                else n.Judged = 3;
                notes.Add(n);
            }
            return DtmScore();
        }

        public int ScoreDrawingFor(int shape, IList<Vector2> line, bool touch)
        {
            model.Clear();
            model.AddRange(Shape(shape));
            strokes.Clear();
            strokes.Add(new List<Vector2>(line));
            touchUsed = touch;
            return DrawingScore();
        }

        public int ScoreSingingFor(float averageError)
        {
            closeness = Mathf.Clamp01(1f - Mathf.Abs(averageError) / SingTolerance) * 10f;
            counted = 10f;
            return SingingScore();
        }
    }
}
