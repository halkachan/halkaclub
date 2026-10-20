using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.UI
{
    // An optional extra menu row with a value button, like AUTO (ver3.0 shared 2D/3D world: the
    // view). The 2D game sets none, so its menu is unchanged.
    public interface IGameMenuItem
    {
        string Label { get; }
        string Value { get; }
        void Activate(float now);
    }

    // ver3.0 Phase 6: a page shown in the menu panel instead of the main rows (the save data
    // screens): a title, a few lines of text and full-width buttons. The 2D game opens none.
    public interface IGameMenuPage
    {
        string Title { get; }
        System.Collections.Generic.IReadOnlyList<string> Lines { get; }
        System.Collections.Generic.IReadOnlyList<string> Buttons { get; }
        void Press(int index, float now);
    }

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
        private readonly System.Collections.Generic.List<IGameMenuItem> extraItems =
            new System.Collections.Generic.List<IGameMenuItem>();
        // ver3.0: the version shown at the bottom of the menu (empty: none, as in the 2D game).
        [SerializeField] private string versionText;
        // ver3.0 Phase 6 (none in the 2D game): full-width rows under せいかつきろく that open a
        // page (セーブデータ), a row that closes the menu (やめる), and the open page.
        private readonly System.Collections.Generic.List<string> linkLabels = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<System.Action<float>> linkActions =
            new System.Collections.Generic.List<System.Action<float>>();
        private string closeLabel = "";
        private IGameMenuPage page;
        // Touches that began on an open page: only these press its buttons when released (the
        // touch that opened the page must not press the button that appears under it).
        private readonly System.Collections.Generic.HashSet<int> pageTouches = new System.Collections.Generic.HashSet<int>();

        public bool IsOpen { get; private set; }
        public bool IsLifeLogOpen { get; private set; }
        public bool IsPageOpen => IsOpen && page != null;
        public IGameMenuPage Page => IsOpen ? page : null;
        public System.Collections.Generic.IReadOnlyList<string> LinkLabels => linkLabels;
        public string CloseLabel => closeLabel;

        public void AddLinkItem(string label, System.Action<float> activate)
        {
            if (string.IsNullOrEmpty(label) || activate == null) return;
            linkLabels.Add(label);
            linkActions.Add(activate);
        }

        public void SetCloseLabel(string label) => closeLabel = label ?? "";

        // Opens a page in the open menu (the menu opens if it is closed).
        public void OpenPage(IGameMenuPage menuPage, float now)
        {
            if (menuPage == null) return;
            if (!IsOpen) SetOpen(true, now);
            if (!IsOpen) return;
            page = menuPage;
            pageTouches.Clear();
            IsLifeLogOpen = false;
            pointerCaptureUntil = now + 0.25f;
        }

        public void ClosePage(float now)
        {
            if (page == null) return;
            page = null;
            pointerCaptureUntil = now + 0.25f;
        }

        public void ActivateLink(int index, float now)
        {
            if (!IsOpen || IsLifeLogOpen || page != null || index < 0 || index >= linkActions.Count) return;
            pointerCaptureUntil = now + 0.25f;
            linkActions[index](now);
        }

        public void PressPageButton(int index, float now)
        {
            if (!IsPageOpen || index < 0 || index >= page.Buttons.Count) return;
            pointerCaptureUntil = now + 0.25f;
            page.Press(index, now);
        }

        // A page button while the page shows it (GUI pixels, origin top-left).
        public bool TryGetPageButtonRect(int index, out Rect guiRect)
        {
            guiRect = default;
            if (!IsPageOpen || index < 0 || index >= page.Buttons.Count) return false;
            guiRect = PageButtonRect(index);
            return true;
        }
        public bool BlocksGameplayInput => IsOpen || suppressTouchUntilRelease ||
            Time.unscaledTime < pointerCaptureUntil;
        public IGameMenuItem ExtraItem => extraItems.Count > 0 ? extraItems[0] : null;
        public System.Collections.Generic.IReadOnlyList<IGameMenuItem> ExtraItems => extraItems;
        public string VersionText => versionText;

        public void SetExtraItem(IGameMenuItem item)
        {
            extraItems.Clear();
            if (item != null) extraItems.Add(item);
        }

        public void AddExtraItem(IGameMenuItem item) { if (item != null) extraItems.Add(item); }

        // The value button of an extra row while the menu shows it (GUI pixels, origin top-left).
        public bool TryGetExtraItemRect(int index, out Rect guiRect)
        {
            guiRect = default;
            if (!IsOpen || IsLifeLogOpen || index < 0 || index >= extraItems.Count) return false;
            guiRect = ExtraRect(index);
            return true;
        }
        public void SetVersionText(string text) => versionText = text;

        public void SetOpen(bool open, float now)
        {
            if (open && hud != null && hud.HasActiveMessage) return;
            if (IsOpen == open) return;
            IsOpen = open;
            IsLifeLogOpen = false;
            page = null;
            pointerCaptureUntil = now + 0.25f;
            if (autoMode != null) autoMode.SetMenuSuspended(open, now);
        }

        public void ToggleAuto(float now)
        {
            if (!IsOpen || IsLifeLogOpen || page != null || autoMode == null) return;
            autoMode.SetAutoEnabled(!autoMode.AutoEnabled, now);
            pointerCaptureUntil = now + 0.25f;
        }

        public void ActivateExtraItem(float now) => ActivateExtraItem(0, now);

        public void ActivateExtraItem(int index, float now)
        {
            if (!IsOpen || IsLifeLogOpen || page != null || index < 0 || index >= extraItems.Count) return;
            extraItems[index].Activate(now);
            pointerCaptureUntil = now + 0.25f;
        }

        public void OpenLifeLog(float now)
        {
            if (!IsOpen || IsLifeLogOpen || page != null) return;
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
                if (IsPageOpen && !ButtonRect().Contains(point))
                {
                    pageTouches.Add(touch.fingerId);
                    // Page buttons act on release (the page's file and download actions need the
                    // browser's gesture, which ends with the release); a touch outside closes.
                    if (!PanelRect().Contains(point)) SetOpen(false, now);
                    pointerCaptureUntil = now + 0.25f;
                    touchUiUntil = now + 0.3f;
                    suppressTouchUntilRelease = true;
                    continue;
                }
                if (ButtonRect().Contains(point)) SetOpen(!IsOpen, now);
                else if (IsOpen && IsLifeLogOpen && BackRect().Contains(point)) BackFromLifeLog(now);
                else if (IsOpen && !IsLifeLogOpen && AutoRect().Contains(point)) ToggleAuto(now);
                else if (IsOpen && !IsLifeLogOpen && ExtraIndexAt(point) >= 0)
                    ActivateExtraItem(ExtraIndexAt(point), now);
                else if (IsOpen && !IsLifeLogOpen && LifeLogRect().Contains(point)) OpenLifeLog(now);
                else if (IsOpen && !IsLifeLogOpen && LinkIndexAt(point) >= 0) ActivateLink(LinkIndexAt(point), now);
                else if (IsOpen && !IsLifeLogOpen && closeLabel.Length > 0 && CloseRect().Contains(point)) SetOpen(false, now);
                else if (IsOpen && !PanelRect().Contains(point)) SetOpen(false, now);
                else if (!IsOpen) continue;
                pointerCaptureUntil = now + 0.25f;
                touchUiUntil = now + 0.3f;
                suppressTouchUntilRelease = true;
            }
            if (!IsPageOpen) return;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) continue;
                if (!pageTouches.Remove(touch.fingerId) || touch.phase == TouchPhase.Canceled) continue;
                var index = PageButtonAt(ToGui(touch.position));
                if (index >= 0) { PressPageButton(index, Time.unscaledTime); touchUiUntil = Time.unscaledTime + 0.3f; }
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
            if (IsPageOpen)
            {
                Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 24f), page.Title, 22);
                for (var line = 0; line < page.Lines.Count; line++)
                    Label(new Rect(panel.x + 12f, panel.y + 40f + line * 24f, panel.width - 24f, 24f), page.Lines[line], 17);
                for (var index = 0; index < page.Buttons.Count; index++)
                {
                    var pageButton = PageButtonRect(index);
                    DrawTextButton(pageButton, page.Buttons[index], Color.white);
                    if (useMouse && GUI.Button(pageButton, GUIContent.none, GUIStyle.none))
                        PressPageButton(index, Time.unscaledTime);
                }
            }
            else if (IsLifeLogOpen)
            {
                Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 24f), "せいかつきろく", 22);
                Label(new Rect(panel.x + 12f, panel.y + 42f, panel.width - 24f, 24f), "あるいたかず", 20);
                Label(new Rect(panel.x + 12f, panel.y + 66f, panel.width - 24f, 26f), lifeLog != null ? lifeLog.StepsLabel : "0", 20);
                Label(new Rect(panel.x + 12f, panel.y + 104f, panel.width - 24f, 24f), "プレイじかん", 20);
                Label(new Rect(panel.x + 12f, panel.y + 128f, panel.width - 24f, 26f), lifeLog != null ? lifeLog.TimeLabel : "0:00:00", 20);
                var back = BackRect();
                DrawTextButton(back, "もどる", Color.white);
                if (useMouse && GUI.Button(back, GUIContent.none, GUIStyle.none)) BackFromLifeLog(Time.unscaledTime);
            }
            else
            {
                Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 24f), "メニュー", 22);
                Label(new Rect(panel.x + 12f, panel.y + 53f, 70f, 32f), "AUTO", 20);
                var autoButton = AutoRect();
                Fill(autoButton, autoMode != null && autoMode.AutoEnabled ? new Color(0.2f, 0.25f, 0.19f) : Color.black);
                Fill(Inset(autoButton, 2f), autoMode != null && autoMode.AutoEnabled
                    ? new Color(0.28f, 0.35f, 0.25f) : Color.white);
                valueStyle.normal.textColor = autoMode != null && autoMode.AutoEnabled ? Color.white : Color.black;
                GameBitmapFont.Draw(autoButton, autoMode != null && autoMode.AutoEnabled ? "ON" : "OFF",
                    20, valueStyle.normal.textColor, TextAnchor.MiddleCenter, font);
                if (useMouse && GUI.Button(autoButton, GUIContent.none, GUIStyle.none))
                    ToggleAuto(Time.unscaledTime);
                for (var index = 0; index < extraItems.Count; index++)
                {
                    var item = extraItems[index];
                    Label(new Rect(panel.x + 12f, panel.y + 101f + index * 48f, 96f, 32f), item.Label, 20);
                    var extraButton = ExtraRect(index);
                    DrawTextButton(extraButton, item.Value, Color.white);
                    if (useMouse && GUI.Button(extraButton, GUIContent.none, GUIStyle.none))
                        ActivateExtraItem(index, Time.unscaledTime);
                }
                var lifeButton = LifeLogRect();
                DrawTextButton(lifeButton, "せいかつきろく", Color.white);
                if (useMouse && GUI.Button(lifeButton, GUIContent.none, GUIStyle.none))
                    OpenLifeLog(Time.unscaledTime);
                var lastButton = lifeButton;
                for (var index = 0; index < linkLabels.Count; index++)
                {
                    var link = LinkRect(index);
                    DrawTextButton(link, linkLabels[index], Color.white);
                    if (useMouse && GUI.Button(link, GUIContent.none, GUIStyle.none)) ActivateLink(index, Time.unscaledTime);
                    lastButton = link;
                }
                if (closeLabel.Length > 0)
                {
                    var close = CloseRect();
                    DrawTextButton(close, closeLabel, Color.white);
                    if (useMouse && GUI.Button(close, GUIContent.none, GUIStyle.none)) SetOpen(false, Time.unscaledTime);
                    lastButton = close;
                }
                if (!string.IsNullOrEmpty(versionText))
                    GameBitmapFont.Draw(new Rect(panel.x + 12f, lastButton.yMax + 6f, panel.width - 24f, 22f),
                        versionText, 16, new Color(0.18f, 0.19f, 0.15f), TextAnchor.MiddleRight, font);
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
            var width = Mathf.Min(IsPageOpen ? 280f : IsLifeLogOpen ? 240f : 200f, Mathf.Max(120f, safe.width - 24f));
            var height = IsPageOpen ? 48f + page.Lines.Count * 24f + page.Buttons.Count * 46f + 6f :
                IsLifeLogOpen ? 208f : 150f + extraItems.Count * 48f + linkLabels.Count * 46f +
                (closeLabel.Length > 0 ? 46f : 0f) + (string.IsNullOrEmpty(versionText) ? 0f : 26f);
            var y = Mathf.Min(button.yMax + 6f, Screen.height - safe.yMin - height - 4f);
            return new Rect(button.x, y, width, height);
        }

        private Rect AutoRect()
        {
            var panel = PanelRect();
            return new Rect(panel.xMax - 80f, panel.y + 48f, 66f, 38f);
        }

        private Rect ExtraRect(int index)
        {
            var panel = PanelRect();
            return new Rect(panel.xMax - 80f, panel.y + 96f + index * 48f, 66f, 38f);
        }

        private int ExtraIndexAt(Vector2 point)
        {
            for (var index = 0; index < extraItems.Count; index++)
                if (ExtraRect(index).Contains(point)) return index;
            return -1;
        }

        private Rect LifeLogRect()
        {
            var panel = PanelRect();
            return new Rect(panel.x + 12f, panel.y + 98f + extraItems.Count * 48f, panel.width - 24f, 38f);
        }

        private Rect LinkRect(int index)
        {
            var life = LifeLogRect();
            return new Rect(life.x, life.yMax + 8f + index * 46f, life.width, 38f);
        }

        private int LinkIndexAt(Vector2 point)
        {
            for (var index = 0; index < linkLabels.Count; index++)
                if (LinkRect(index).Contains(point)) return index;
            return -1;
        }

        private Rect CloseRect()
        {
            var life = LifeLogRect();
            return new Rect(life.x, life.yMax + 8f + linkLabels.Count * 46f, life.width, 38f);
        }

        private Rect PageButtonRect(int index)
        {
            var panel = PanelRect();
            var top = panel.y + 48f + (page != null ? page.Lines.Count : 0) * 24f;
            return new Rect(panel.x + 12f, top + index * 46f, panel.width - 24f, 38f);
        }

        private int PageButtonAt(Vector2 point)
        {
            if (page == null) return -1;
            for (var index = 0; index < page.Buttons.Count; index++)
                if (PageButtonRect(index).Contains(point)) return index;
            return -1;
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
            GameBitmapFont.Draw(rect, label, 20, Color.black,
                TextAnchor.MiddleCenter, font);
        }

        private void Label(Rect rect, string text, int size) =>
            GameBitmapFont.Draw(rect, text, size, Color.black, TextAnchor.MiddleLeft, font);

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
