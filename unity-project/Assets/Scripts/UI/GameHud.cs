using Halka.Game.Core;
using Halka.Game.Input;
using UnityEngine;

namespace Halka.Game.UI
{
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private TouchDpad dpad;
        [SerializeField] private TouchActionButton actionButton;
        [SerializeField] private Font messageFont;
        [SerializeField, Min(0.1f)] private float messageSeconds = 2f;
        // ver3.0 Release Candidate: the label shown instead of GameVersion.Label (empty: unchanged).
        [SerializeField] private string versionLabelOverride;
        // ver3.0: the version moves into the menu (GameMenuController.versionText); the 2D game keeps it here.
        [SerializeField] private bool hideVersionLabel;

        private readonly TimedMessage message = new TimedMessage();
        private GUIStyle versionStyle;
        private GUIStyle messageStyle;

        public string VersionLabel => string.IsNullOrEmpty(versionLabelOverride) ? GameVersion.Label : versionLabelOverride;
        public void SetVersionLabel(string label) => versionLabelOverride = label;
        public bool VersionLabelShown => !hideVersionLabel;
        public void SetVersionLabelShown(bool shown) => hideVersionLabel = !shown;

        public bool HasActiveMessage => !string.IsNullOrEmpty(message.TextAt(Time.unscaledTime));

        public void ShowMessage(string text) => message.Show(text, Time.unscaledTime, messageSeconds);

        private void OnGUI()
        {
            if (versionStyle == null)
            {
                versionStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 16,
                    normal = { textColor = new Color(0.18f, 0.19f, 0.15f) }
                };
                messageStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24,
                    font = messageFont,
                    normal = { textColor = Color.black }
                };
            }
            if (!hideVersionLabel)
                GameBitmapFont.Draw(new Rect(Screen.width - 110f, 10f, 100f, 28f),
                    VersionLabel, 18, versionStyle.normal.textColor,
                    TextAnchor.MiddleRight, messageFont);

            var text = message.TextAt(Time.unscaledTime);
            if (string.IsNullOrEmpty(text)) return;
            var safe = Screen.safeArea;
            const float height = 56f;
            var width = Mathf.Min(260f, safe.width * 0.62f);
            var y = Screen.height - safe.yMin - height - 16f;
            var x = safe.center.x - width * 0.5f;
            if (dpad != null && dpad.Visible)
            {
                var controlsTop = dpad.ControlBoundsGui.yMin;
                if (actionButton != null && actionButton.Visible)
                    controlsTop = Mathf.Min(controlsTop, actionButton.ControlBoundsGui.yMin);
                if (new Rect(x, y, width, height).Overlaps(dpad.ControlBoundsGui) ||
                    actionButton != null && actionButton.Visible &&
                    new Rect(x, y, width, height).Overlaps(actionButton.ControlBoundsGui))
                    y = Mathf.Min(y, controlsTop - height - 12f);
            }
            y = Mathf.Max(Screen.height - safe.yMax + 8f, y);
            var rect = new Rect(x, y, width, height);
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var inner = new Rect(x + 2f, y + 2f, width - 4f, height - 4f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GameBitmapFont.Draw(inner, text, 26, Color.black,
                TextAnchor.MiddleCenter, messageFont);
        }
    }
}
