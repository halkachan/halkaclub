using Halka.Game.Core;
using Halka.Game.Input;
using UnityEngine;

namespace Halka.Game.UI
{
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private TouchDpad dpad;
        [SerializeField] private Font messageFont;
        [SerializeField, Min(0.1f)] private float messageSeconds = 2f;

        private readonly TimedMessage message = new TimedMessage();
        private GUIStyle versionStyle;
        private GUIStyle messageStyle;

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
                messageStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    font = messageFont,
                    normal = { textColor = new Color(0.18f, 0.19f, 0.15f) }
                };
            }
            GUI.Label(new Rect(Screen.width - 110f, 10f, 100f, 28f), GameVersion.Label, versionStyle);

            var text = message.TextAt(Time.unscaledTime);
            if (string.IsNullOrEmpty(text)) return;
            var safe = Screen.safeArea;
            const float height = 44f;
            var width = Mathf.Min(260f, safe.width * 0.62f);
            var y = Screen.height - safe.yMin - height - 16f;
            var x = safe.center.x - width * 0.5f;
            if (dpad != null && dpad.Visible &&
                new Rect(x, y, width, height).Overlaps(dpad.ControlBoundsGui))
                y = Mathf.Min(y, dpad.ControlBoundsGui.yMin - height - 12f);
            y = Mathf.Max(Screen.height - safe.yMax + 8f, y);
            GUI.Box(new Rect(x, y, width, height), text, messageStyle);
        }
    }
}
