using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.UI
{
    // Persistent IMGUI HUD control. AUTO remains owned by AutoModeController.
    [DefaultExecutionOrder(-110)]
    public sealed class GameMenuController : MonoBehaviour
    {
        [SerializeField] private AutoModeController autoMode;
        [SerializeField] private LifeLogController lifeLog;
        [SerializeField] private GameHud hud;
        [SerializeField] private Font font;

        private GUIStyle titleStyle;
        private GUIStyle itemStyle;
        private GUIStyle valueStyle;
        private float pointerCaptureUntil;
        private float touchUiUntil;
        private bool suppressTouchUntilRelease;

        public bool IsOpen { get; private set; }
        public bool IsLifeLogOpen { get; private set; }
        public bool BlocksGameplayInput => IsOpen || suppressTouchUntilRelease ||
            Time.unscaledTime < pointerCaptureUntil;

        public void SetOpen(bool open, float now)
        {
            if (open && hud != null && hud.HasActiveMessage) return;
            if (IsOpen == open) return;
            IsOpen = open;
            IsLifeLogOpen = false;
            pointerCaptureUntil = now + 0.25f;
            if (autoMode != null) autoMode.SetMenuSuspended(open, now);
        }

        public void ToggleAuto(float now)
        {
            if (!IsOpen || IsLifeLogOpen || autoMode == null) return;
            autoMode.SetAutoEnabled(!autoMode.AutoEnabled, now);
            pointerCaptureUntil = now + 0.25f;
        }

        public void OpenLifeLog(float now)
        {
            if (!IsOpen || IsLifeLogOpen) return;
            IsLifeLogOpen = true;
            pointerCaptureUntil = now + 0.25f;
        }

        public void BackFromLifeLog(float now)
        {
            if (!IsOpen || !IsLifeLogOpen) return;
            IsLifeLogOpen = false;
            pointerCaptureUntil = now + 0.25f;
        }

        public bool IsOverControls(Vector2 screenPosition)
        {
            if (BlocksGameplayInput) return true;
            return ButtonRect().Contains(ToGui(screenPosition));
        }

        private void Update()
        {
            if (UnityEngine.Input.touchCount == 0) suppressTouchUntilRelease = false;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                var point = ToGui(touch.position);
                var now = Time.unscaledTime;
                if (ButtonRect().Contains(point)) SetOpen(!IsOpen, now);
                else if (IsOpen && IsLifeLogOpen && BackRect().Contains(point)) BackFromLifeLog(now);
                else if (IsOpen && !IsLifeLogOpen && AutoRect().Contains(point)) ToggleAuto(now);
                else if (IsOpen && !IsLifeLogOpen && LifeLogRect().Contains(point)) OpenLifeLog(now);
                else if (IsOpen && !PanelRect().Contains(point)) SetOpen(false, now);
                else if (!IsOpen) continue;
                pointerCaptureUntil = now + 0.25f;
                touchUiUntil = now + 0.3f;
                suppressTouchUntilRelease = true;
            }
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            EnsureStyles();
            var button = ButtonRect();
            Fill(button, Color.black);
            Fill(Inset(button, 2f), new Color(0.96f, 0.96f, 0.91f));
            for (var i = 0; i < 3; i++)
                Fill(new Rect(button.x + 15f, button.y + 13f + i * 9f, button.width - 30f, 3f), Color.black);

            var useMouse = UnityEngine.Input.touchCount == 0 && Time.unscaledTime >= touchUiUntil;
            if (useMouse && GUI.Button(button, GUIContent.none, GUIStyle.none))
                SetOpen(!IsOpen, Time.unscaledTime);
            if (!IsOpen) return;

            var panel = PanelRect();
            Fill(panel, Color.black);
            Fill(Inset(panel, 2f), new Color(0.96f, 0.96f, 0.91f));
            if (IsLifeLogOpen)
            {
                GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 24f), "せいかつきろく", titleStyle);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 42f, panel.width - 24f, 24f), "あるいたかず", itemStyle);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 66f, panel.width - 24f, 26f), lifeLog != null ? lifeLog.StepsLabel : "0", itemStyle);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 104f, panel.width - 24f, 24f), "プレイじかん", itemStyle);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 128f, panel.width - 24f, 26f), lifeLog != null ? lifeLog.TimeLabel : "0:00:00", itemStyle);
                var back = BackRect();
                DrawTextButton(back, "もどる", Color.white);
                if (useMouse && GUI.Button(back, GUIContent.none, GUIStyle.none)) BackFromLifeLog(Time.unscaledTime);
            }
            else
            {
                GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 24f), "メニュー", titleStyle);
                GUI.Label(new Rect(panel.x + 12f, panel.y + 53f, 70f, 32f), "AUTO", itemStyle);
                var autoButton = AutoRect();
                Fill(autoButton, autoMode != null && autoMode.AutoEnabled ? new Color(0.2f, 0.25f, 0.19f) : Color.black);
                Fill(Inset(autoButton, 2f), autoMode != null && autoMode.AutoEnabled
                    ? new Color(0.28f, 0.35f, 0.25f) : Color.white);
                valueStyle.normal.textColor = autoMode != null && autoMode.AutoEnabled ? Color.white : Color.black;
                GUI.Label(autoButton, autoMode != null && autoMode.AutoEnabled ? "ON" : "OFF", valueStyle);
                if (useMouse && GUI.Button(autoButton, GUIContent.none, GUIStyle.none))
                    ToggleAuto(Time.unscaledTime);
                var lifeButton = LifeLogRect();
                DrawTextButton(lifeButton, "せいかつきろく", Color.white);
                if (useMouse && GUI.Button(lifeButton, GUIContent.none, GUIStyle.none))
                    OpenLifeLog(Time.unscaledTime);
            }

            var current = Event.current;
            if (useMouse && current.type == EventType.MouseDown && current.button == 0 &&
                !button.Contains(current.mousePosition) && !panel.Contains(current.mousePosition))
            {
                SetOpen(false, Time.unscaledTime);
                current.Use();
            }
        }

        private Rect ButtonRect()
        {
            var safe = Screen.safeArea;
            return new Rect(safe.xMin + 12f, Screen.height - safe.yMax + 12f, 52f, 48f);
        }

        private Rect PanelRect()
        {
            var safe = Screen.safeArea;
            var button = ButtonRect();
            var width = Mathf.Min(IsLifeLogOpen ? 240f : 200f, Mathf.Max(120f, safe.width - 24f));
            var height = IsLifeLogOpen ? 208f : 150f;
            var y = Mathf.Min(button.yMax + 6f, Screen.height - safe.yMin - height - 4f);
            return new Rect(button.x, y, width, height);
        }

        private Rect AutoRect()
        {
            var panel = PanelRect();
            return new Rect(panel.xMax - 80f, panel.y + 48f, 66f, 38f);
        }

        private Rect LifeLogRect()
        {
            var panel = PanelRect();
            return new Rect(panel.x + 12f, panel.y + 98f, panel.width - 24f, 38f);
        }

        private Rect BackRect()
        {
            var panel = PanelRect();
            return new Rect(panel.x + 12f, panel.y + 162f, panel.width - 24f, 36f);
        }

        private void DrawTextButton(Rect rect, string label, Color background)
        {
            Fill(rect, Color.black);
            Fill(Inset(rect, 2f), background);
            valueStyle.normal.textColor = Color.black;
            GUI.Label(rect, label, valueStyle);
        }

        private static Vector2 ToGui(Vector2 screenPosition) =>
            new Vector2(screenPosition.x, Screen.height - screenPosition.y);

        private static Rect Inset(Rect rect, float amount) =>
            new Rect(rect.x + amount, rect.y + amount, rect.width - 2f * amount, rect.height - 2f * amount);

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) {
                font = font, fontSize = 20, normal = { textColor = Color.black } };
            itemStyle = new GUIStyle(GUI.skin.label) {
                font = font, fontSize = 18, normal = { textColor = Color.black } };
            valueStyle = new GUIStyle(GUI.skin.label) {
                font = font, fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }

        private void OnDisable()
        {
            if (IsOpen) SetOpen(false, Time.unscaledTime);
        }
    }
}
