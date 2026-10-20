using System.Text;
using Halka.Game.Input;
using Halka.Game.UI;
using Unity.Profiling;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 1 technical controls. This panel exists only in the POC scene; the game itself uses
    // its existing Menu (Phase 4: AUTO, Life Log and the "ひょうじ 2D / 3D" row) and the existing
    // message box. Phase 4: the panel is hidden unless the page is opened with ?debug=1, and then
    // sits below the Menu button. Labels are ASCII (the IMGUI font has no Japanese in WebGL).
    [DefaultExecutionOrder(200)]
    public sealed class PocDebugPanel : MonoBehaviour
    {
        public const string PreviewTitle = "はるかわーるど ver3.0 Phase 4 Preview";
        // Phase 5: the Release Candidate scene. Its page shows only the game; ?debug=1 brings the
        // panel back (with this title). Hidden, the component is disabled: no OnGUI, no recorders.
        public const string ReleaseCandidateName = "はるかわーるど ver3.0 Release Candidate 3";
        // RC1 feedback: the game's name is fixed as はるかわーるど (the page title shows only it).
        public const string ReleaseCandidateTitle = "はるかわーるど";
        // Phase 7: Release Candidate 3 is the release build itself, so the game shows the version
        // players will see (ver3.0); "RC3" only names the candidate (build info, staging, this title).
        public const string ReleaseCandidateVersionLabel = "ver3.0";
        public const string ReleaseCandidateShortName = "ver3.0 RC3";
        [SerializeField] private bool releaseCandidate;
        public bool ReleaseCandidate => releaseCandidate;
        public void ConfigureReleaseCandidate(bool value) => releaseCandidate = value;
        public string Title => releaseCandidate ? ReleaseCandidateTitle + " " + ReleaseCandidateShortName + " (debug)" : PreviewTitle;
        private static readonly int[] PixelOptions = { 16, 24, 32 };
        private static readonly float[] PitchOptions = { 50f, 60f, 90f, 40f };
        private static readonly Color[] OutlineColors =
            { new Color32(0x25, 0x2b, 0x39, 0xff), new Color32(0x3a, 0x2a, 0x22, 0xff), new Color32(0x00, 0x00, 0x00, 0xff) };
        private static readonly string[] OutlineColorNames = { "#252B39 blue-grey", "#3A2A22 brown", "#000000 black" };

        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private PocInput input;
        [SerializeField] private PocTimeOfDay timeOfDay;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private TouchDpad dpad;
        [SerializeField] private TouchActionButton actionButton;
        [SerializeField] private Font fallbackFont;
        [Header("Phase 4")]
        [SerializeField] private PocGameplay gameplay;
        [SerializeField] private PocOrientationGuard guard;
        [Header("After RC1")]
        [SerializeField] private PocLineBoil lineBoil;

        private ProfilerRecorder drawCalls;
        private ProfilerRecorder batches;
        private ProfilerRecorder setPass;
        private ProfilerRecorder renderedTriangles;
        private ProfilerRecorder usedMemory;
        private float fpsWindowStart;
        private int fpsFrames;
        private float fpsMaxFrame;
        private float shownFps;
        private float shownFrameMs;
        private float shownMaxMs;
        private bool expanded = true;
        private bool showStats = true;
        private Rect panelRect;
        private GUIStyle buttonStyle;
        private GUIStyle textStyle;
        private Texture2D panelBackground;
        private readonly StringBuilder text = new StringBuilder(512);

        public void Configure(SharedWorldSession worldSession, PocViewSwitcher viewSwitcher, PocInput pocInput,
            PocTimeOfDay time, PocWorldPresenter worldPresenter, TouchDpad touchDpad,
            TouchActionButton button, Font font)
        {
            session = worldSession;
            views = viewSwitcher;
            input = pocInput;
            timeOfDay = time;
            presenter = worldPresenter;
            dpad = touchDpad;
            actionButton = button;
            fallbackFont = font;
        }

        public void ConfigureGame(PocGameplay sharedGameplay, PocOrientationGuard orientationGuard)
        {
            gameplay = sharedGameplay;
            guard = orientationGuard;
        }

        public void ConfigureLineBoil(PocLineBoil boil) => lineBoil = boil;

        public PocLineBoil LineBoil => lineBoil;

        // ?debug=1 shows the POC options; the default page shows only the game's own UI.
        public bool PanelVisible { get; set; }

        private void Awake()
        {
            var url = Application.absoluteURL ?? "";
            PanelVisible = url.Contains("debug=1");
            if (releaseCandidate && !PanelVisible) enabled = false;
        }

        private void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            renderedTriangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            expanded = Mathf.Min(Screen.width, Screen.height) >= 600;
        }

        private void OnDisable()
        {
            drawCalls.Dispose();
            batches.Dispose();
            setPass.Dispose();
            renderedTriangles.Dispose();
            usedMemory.Dispose();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            fpsFrames++;
            fpsMaxFrame = Mathf.Max(fpsMaxFrame, dt);
            var elapsed = Time.unscaledTime - fpsWindowStart;
            if (elapsed < 1f) return;
            shownFps = fpsFrames / elapsed;
            shownFrameMs = elapsed * 1000f / fpsFrames;
            shownMaxMs = fpsMaxFrame * 1000f;
            fpsFrames = 0;
            fpsMaxFrame = 0f;
            fpsWindowStart = Time.unscaledTime;
        }

        public bool IsOverControls(Vector2 screenPosition) =>
            PanelVisible && panelRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        private float UiScale => Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 1f, 2.4f);

        private void EnsureStyles()
        {
            var size = Mathf.RoundToInt(13 * UiScale);
            if (buttonStyle != null && buttonStyle.fontSize == size) return;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = size, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 6, 2, 2) };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = size, richText = false,
                normal = { textColor = Color.white } };
            if (panelBackground == null)
            {
                panelBackground = new Texture2D(1, 1);
                panelBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.58f));
                panelBackground.Apply();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            var s = UiScale;
            if (guard != null && guard.GuideVisible) return;
            if (!PanelVisible)
            {
                panelRect = default;
                DrawTitle(s);
                if (gameplay == null || gameplay.Hud == null) DrawMessage();
                return;
            }
            var width = 236f * s;
            var row = 24f * s;
            var x = 8f;
            // Below the game's Menu button (GameMenuController: 12 px from the top, 48 px tall).
            var y = gameplay != null && gameplay.Menu != null ? 68f : 8f;
            var rows = expanded ? 24 : 2;
            var statsLines = showStats ? expanded ? 13 : 8 : 6;
            panelRect = new Rect(x - 4f, y - 4f, width + 8f, rows * (row + 3f) + statsLines * row * 0.78f + 12f);
            GUI.DrawTexture(panelRect, panelBackground);

            if (Button(ref y, x, width, row, "VIEW: " + (views.Mode == PocViewMode.PixelView2D ? "2D pixel   [to 3D]" : "3D 3rd-person [to 2D]")))
                views.Toggle();
            if (Button(ref y, x, width, row, expanded ? "POC options: hide" : "POC options: show"))
                expanded = !expanded;
            if (expanded)
            {
                var pixel = views.PixelView;
                if (Button(ref y, x, width, row, $"2D pixels/cell: {pixel.PixelsPerCell}"))
                    pixel.PixelsPerCell = Next(PixelOptions, pixel.PixelsPerCell);
                if (Button(ref y, x, width, row, $"2D camera pitch: {pixel.PitchDegrees:0} deg"))
                    pixel.PitchDegrees = Next(PitchOptions, pixel.PitchDegrees);
                if (Button(ref y, x, width, row, "2D square ground: " + (pixel.SquareGround ? "ON" : "OFF")))
                    pixel.SquareGround = !pixel.SquareGround;
                if (Button(ref y, x, width, row, "3D keys: " + (input.Scheme == Poc3DControlScheme.WorldFixed
                        ? "A world-fixed" : "B camera-relative")))
                    input.Scheme = input.Scheme == Poc3DControlScheme.WorldFixed
                        ? Poc3DControlScheme.CameraRelative : Poc3DControlScheme.WorldFixed;
                if (Button(ref y, x, width, row, "3D indoor cam: " + (views.ThirdPerson.IndoorMode == PocIndoorCamera.CloserOffset
                        ? "closer" : "closer+clamp")))
                    views.ThirdPerson.IndoorMode = views.ThirdPerson.IndoorMode == PocIndoorCamera.CloserOffset
                        ? PocIndoorCamera.CloserOffsetAndClamp : PocIndoorCamera.CloserOffset;
                if (Button(ref y, x, width, row, "Time: " + (timeOfDay.Override == PocPhaseOverride.Auto
                        ? "real (" + timeOfDay.Phase + ")"
                        : timeOfDay.Override == PocPhaseOverride.Morning ? "force morning" : "force night")))
                    timeOfDay.Override = (PocPhaseOverride)(((int)timeOfDay.Override + 1) % 3);
                if (Button(ref y, x, width, row, "Grid/blocked: " + (presenter.GridVisible ? "ON" : "OFF")))
                    presenter.GridVisible = !presenter.GridVisible;
                if (Button(ref y, x, width, row, "Stats: " + (showStats ? "ON" : "OFF")))
                    showStats = !showStats;
                var third = views.ThirdPerson;
                if (Button(ref y, x, width, row, "3D cam follow: " + FollowLabel(third.Follow)))
                    third.Follow = Next(FollowOrder, third.Follow);
                if (Button(ref y, x, width, row, "2D outline: " + (pixel.OutlinePixels == 0 ? "OFF" : pixel.OutlinePixels + " px")))
                    pixel.OutlinePixels = (pixel.OutlinePixels + 1) % 3;
                var colorIndex = Mathf.Max(0, System.Array.IndexOf(OutlineColors, pixel.OutlineColor));
                if (Button(ref y, x, width, row, "Outline color: " + OutlineColorNames[colorIndex]))
                    pixel.OutlineColor = OutlineColors[(colorIndex + 1) % OutlineColors.Length];
                if (Button(ref y, x, width, row, "Grass outline: " + (pixel.GrassOutline > 0f ? "soft" : "OFF")))
                    pixel.GrassOutline = pixel.GrassOutline > 0f ? 0f : 0.35f;
                if (Button(ref y, x, width, row, "Inner lines: " + (pixel.InnerLines > 0f ? "soft" : "OFF")))
                    pixel.InnerLines = pixel.InnerLines > 0f ? 0f : 0.5f;
                if (Button(ref y, x, width, row, "2D see-through trees: " + (pixel.FadeEnabled ? "ON" : "OFF")))
                    pixel.FadeEnabled = !pixel.FadeEnabled;
                // Phase 3: last row, so the rows above keep their places for the browser checks.
                if (Button(ref y, x, width, row, "3D indoor walls: " + (third.WallCutaway ? "cut camera side" : "rays only")))
                    third.WallCutaway = !third.WallCutaway;
                // Phase 4 (for comparisons with the earlier behaviour).
                var fade = third.TreeFade;
                if (Button(ref y, x, width, row, "3D see-through trees: " + (fade == null || fade.Enabled ? "ON" : "OFF (hide)")))
                    if (fade != null) fade.Enabled = !fade.Enabled;
                if (Button(ref y, x, width, row, "3D exit camera: " + (third.ExitComposition ? "house front" : "behind (Phase 3)")))
                    third.ExitComposition = !third.ExitComposition;
                if (Button(ref y, x, width, row, "3D cam distance: " + DistanceLabel(third)))
                    NextDistance(third);
                // Phase 5 (last rows, so the rows above keep their places).
                if (Button(ref y, x, width, row, "3D exit held key: " + (third.ExitHeldKeyResumeSeconds > 0f
                        ? $"walks on after {third.ExitHeldKeyResumeSeconds:0.0}s" : "waits for release (Phase 4)")))
                    third.ExitHeldKeyResumeSeconds = third.ExitHeldKeyResumeSeconds > 0f ? 0f : PocThirdPersonCamera.StandardExitHeldKeyResumeSeconds;
                if (Button(ref y, x, width, row, "2D grass: " + (pixel.OriginalGrass2D ? "2D game sprites" : "3D tufts (Phase 4)")))
                    pixel.OriginalGrass2D = !pixel.OriginalGrass2D;
                var outline3D = third.Outline;
                if (Button(ref y, x, width, row, "3D outline: " + (outline3D != null && outline3D.Enabled ? "ON" : "OFF")))
                    if (outline3D != null) outline3D.Enabled = !outline3D.Enabled;
                if (lineBoil != null && Button(ref y, x, width, row, "3D line boil: " + BoilLabel(lineBoil.Mode)))
                    lineBoil.Mode = (PocLineBoilMode)(((int)lineBoil.Mode + 1) % 4);
            }
            GUI.Label(new Rect(x, y, width, statsLines * row * 0.78f + 8f), BuildText(), textStyle);
            DrawTitle(s);
            if (gameplay == null || gameplay.Hud == null) DrawMessage();
        }

        private static string BoilLabel(PocLineBoilMode mode) => mode switch
        {
            PocLineBoilMode.Lines => "lines (standard)",
            PocLineBoilMode.LinesStrong => "lines, stronger",
            PocLineBoilMode.LinesAndPicture => "lines + picture",
            _ => "OFF (still)",
        };

        // The user-facing name is はるかわーるど; internal IDs and save keys keep their old names.
        private void DrawTitle(float scale)
        {
            var width = Mathf.Min(Screen.width - panelRect.xMax - 16f, 380f * scale);
            if (width < 120f) return;
            // Below the game's version label (GameHud, top right).
            var rect = new Rect(Screen.width - width - 10f, 40f, width, 26f * scale);
            GUI.color = new Color(1f, 1f, 1f, 0.82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GameBitmapFont.Draw(rect, Title, Mathf.RoundToInt(18 * scale), new Color(0.13f, 0.15f, 0.11f),
                TextAnchor.MiddleCenter, fallbackFont);
        }

        private bool Button(ref float y, float x, float width, float height, string label)
        {
            var clicked = GUI.Button(new Rect(x, y, width, height), label, buttonStyle);
            y += height + 3f;
            return clicked;
        }

        // Outdoor / indoor camera distances to compare (POC only): the new standard first.
        private static readonly (float outdoor, float indoor, string name)[] Distances =
        {
            (PocThirdPersonCamera.StandardOutdoorDistance, PocThirdPersonCamera.StandardIndoorDistance, "standard"),
            (2.5f, 2.9f, "Phase 3"), (3.0f, 3.3f, "a little closer"), (3.8f, 4.0f, "farther")
        };

        private static int DistanceIndex(PocThirdPersonCamera camera) => System.Array.FindIndex(Distances, d =>
            Mathf.Approximately(d.outdoor, camera.OutdoorDistance) && Mathf.Approximately(d.indoor, camera.IndoorDistance));

        private static string DistanceLabel(PocThirdPersonCamera camera)
        {
            var index = DistanceIndex(camera);
            return camera.OutdoorDistance.ToString("0.0") + "/" + camera.IndoorDistance.ToString("0.0") +
                (index >= 0 ? " " + Distances[index].name : "");
        }

        private static void NextDistance(PocThirdPersonCamera camera)
        {
            var next = Distances[(DistanceIndex(camera) + 1) % Distances.Length];
            camera.OutdoorDistance = next.outdoor;
            camera.IndoorDistance = next.indoor;
        }

        private static readonly PocCameraFollow[] FollowOrder =
        {
            PocCameraFollow.NorthFixed, PocCameraFollow.TurnBehindWhenIdle, PocCameraFollow.Fixed, PocCameraFollow.FollowFacing
        };

        private static string FollowLabel(PocCameraFollow follow) =>
            follow == PocCameraFollow.NorthFixed ? "north fixed" :
            follow == PocCameraFollow.TurnBehindWhenIdle ? "turn when idle (2.1)" :
            follow == PocCameraFollow.Fixed ? "fixed at switch" : "facing (Phase 1)";

        private static T Next<T>(T[] options, T current)
        {
            var index = System.Array.IndexOf(options, current);
            return options[(index + 1 + options.Length) % options.Length];
        }

        private string BuildText()
        {
            text.Clear();
            text.Append("Map ").Append(session.World.MapId).Append("  Cell ").Append(session.Cell.x).Append(',')
                .Append(session.Cell.y).Append("  Facing ").Append(session.Facing).Append('\n');
            if (views.Mode == PocViewMode.ThirdPerson3D)
                text.Append("Cam heading ").Append(views.ThirdPerson.Heading).Append("  keys ")
                    .Append(input.Scheme == Poc3DControlScheme.CameraRelative ? "camera" : "world").Append('\n');
            text.Append("Steps ").Append(session.CompletedSteps).Append("  Switches ").Append(views.SwitchCount)
                .Append(" (").Append(views.LastSwitchMilliseconds.ToString("0.00")).Append("ms)\n");
            text.Append("FPS ").Append(shownFps.ToString("0.0")).Append("  ").Append(shownFrameMs.ToString("0.0"))
                .Append("ms (max ").Append(shownMaxMs.ToString("0")).Append(")\n");
            text.Append("Screen ").Append(Screen.width).Append('x').Append(Screen.height);
            if (views.Mode == PocViewMode.PixelView2D && views.PixelView.Target != null)
                text.Append("  RT ").Append(views.PixelView.Target.width).Append('x')
                    .Append(views.PixelView.Target.height).Append(" x").Append(views.PixelView.Scale);
            if (!showStats) return text.ToString();
            if (!expanded)
            {
                // Collapsed (small screens such as a phone held sideways): the counters in one line.
                text.Append('\n').Append("DC ").Append(Value(drawCalls)).Append("  SetPass ").Append(Value(setPass))
                    .Append("  Tris ").Append(Value(renderedTriangles)).Append("  Mem ").Append(Memory());
                AppendGameLine();
                return text.ToString();
            }
            text.Append('\n');
            text.Append("Mesh tris (map) ").Append(presenter.BuiltTriangles);
            if (presenter.Environment != null) text.Append("  tufts ").Append(presenter.Environment.Tufts);
            text.Append('\n');
            text.Append("DrawCalls ").Append(Value(drawCalls)).Append("  Batches ").Append(Value(batches)).Append('\n');
            text.Append("SetPass ").Append(Value(setPass)).Append("  Tris/frame ").Append(Value(renderedTriangles)).Append('\n');
            text.Append("Used memory ").Append(Memory()).Append('\n');
            text.Append("3D heading ").Append(views.ThirdPerson.Heading).Append("  Yaw ")
                .Append(views.ThirdPerson.CurrentYaw.ToString("0")).Append("  hidden ")
                .Append(views.ThirdPerson.HiddenOccluders).Append("  parts ").Append(views.ThirdPerson.HiddenParts)
                .Append("  cut ").Append(views.Mode == PocViewMode.PixelView2D ? views.PixelView.CutWalls
                    : views.ThirdPerson.CutWalls).Append('\n');
            text.Append("Phase ").Append(timeOfDay.Phase).Append("  blend ").Append(timeOfDay.NightBlend.ToString("0.00"));
            AppendGameLine();
            return text.ToString();
        }

        private string Memory() => usedMemory.Valid && usedMemory.LastValue > 0
            ? (usedMemory.LastValue / (1024f * 1024f)).ToString("0.0") + " MB" : "n/a";

        private void AppendGameLine()
        {
            if (gameplay == null) return;
            var auto = gameplay.AutoMode;
            var crow = gameplay.Crow;
            text.Append('\n').Append("AUTO ").Append(auto != null && auto.AutoEnabled ? "on" : "off")
                .Append(auto != null && auto.Active ? " walking" : "").Append("  crow ")
                .Append(crow != null && crow.isActiveAndEnabled ? crow.Activity.ToString() : "-")
                .Append(session.IsSeated ? "  seated " + session.SeatPose : "")
                .Append("  fade3D ").Append(views.ThirdPerson.TreeFade != null ? views.ThirdPerson.TreeFade.FadingCount : 0)
                .Append(views.ThirdPerson.CompositionHeld ? "  exit-cam" : "");
        }

        private static string Value(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.LastValue > 0 ? recorder.LastValue.ToString() : "n/a";

        // Same message box look as the existing GameHud, drawn with the existing bitmap font.
        private void DrawMessage()
        {
            if (string.IsNullOrEmpty(session.LastMessage) || Time.unscaledTime - session.LastMessageAt > 2f) return;
            var safe = Screen.safeArea;
            var height = 56f;
            var width = Mathf.Min(300f, safe.width * 0.7f);
            var y = Screen.height - safe.yMin - height - 16f;
            if (dpad != null && dpad.Visible)
                y = Mathf.Min(y, dpad.ControlBoundsGui.yMin - height - 12f);
            var rect = new Rect(safe.center.x - width * 0.5f, y, width, height);
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GameBitmapFont.Draw(inner, session.LastMessage, 26, Color.black, TextAnchor.MiddleCenter, fallbackFont);
        }

        private void OnDestroy()
        {
            if (panelBackground != null) Destroy(panelBackground);
        }
    }
}
