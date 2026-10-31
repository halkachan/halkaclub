using Halka.Game.UI;
using UnityEngine;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: the game clock on the screen (指示書 3.3): a sun (06:00-17:59) or a moon (18:00-05:59),
    // the time in 24 hours and the day, 「3日目 14:35」. Top centre of the safe area, between the menu
    // button (top left) and the view button (top right), small; the same in 2D and 3D, outdoors and
    // indoors. On a narrow phone it moves under the two buttons instead of between them.
    [DefaultExecutionOrder(30)]
    public sealed class PocLifeHud : MonoBehaviour
    {
        [SerializeField] private PocLife life;
        [SerializeField] private Font fallbackFont;
        private Texture2D sun;
        private Texture2D moon;

        public Rect LastRect { get; private set; }
        public string LastText { get; private set; } = "";
        public bool LastNight { get; private set; }
        public bool Visible { get; set; } = true;

        public void Configure(PocLife owner, Font font)
        {
            life = owner;
            fallbackFont = font;
        }

        // The box (GUI pixels): 14 px from the top of the safe area, between the 64 px buttons when
        // there is room (a 375 px phone has 240 px between them), else under them.
        public static Rect BoxFor(Rect safe, int screenHeight, float width)
        {
            var top = screenHeight - safe.yMax + 12f;
            var between = safe.width - 2f * (12f + GameMenuController.QuickButtonWidth + 8f);
            var x = safe.xMin + (safe.width - width) * 0.5f;
            if (between >= width) return new Rect(x, top + 6f, width, 36f);
            return new Rect(x, top + 48f + 8f, width, 36f);
        }

        private void OnGUI()
        {
            if (life == null || !life.Started || !Visible) return;
            GUI.depth = -90;
            if (sun == null) { sun = Icon(false); moon = Icon(true); }
            LastNight = life.IsNightNow;
            LastText = life.ClockText;
            var width = Mathf.Min(196f, Screen.safeArea.width - 24f);
            var rect = BoxFor(Screen.safeArea, Screen.height, width);
            LastRect = rect;
            GUI.color = new Color32(0x25, 0x2b, 0x39, 0xff);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = LastNight ? new Color(0.84f, 0.86f, 0.95f) : new Color(0.98f, 0.97f, 0.9f);
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 4f, 28f, 28f), LastNight ? moon : sun);
            GameBitmapFont.Draw(new Rect(rect.x + 38f, rect.y + 2f, rect.width - 44f, rect.height - 4f), LastText, 21,
                new Color32(0x25, 0x2b, 0x39, 0xff), TextAnchor.MiddleCenter, fallbackFont);
        }

        // 14 x 14 pixel icons (drawn with Point filtering).
        private static Texture2D Icon(bool night)
        {
            const int n = 14;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(0f, 0f, 0f, 0f);
            var ink = new Color32(0x25, 0x2b, 0x39, 0xff);
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var dx = x - 6.5f;
                    var dy = y - 6.5f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = clear;
                    if (!night)
                    {
                        if (r <= 3.6f) c = new Color(1f, 0.78f, 0.2f);
                        else if (r <= 4.4f) c = ink;
                        else if (r >= 5.2f && r <= 6.6f && (Mathf.Abs(dx) < 0.8f || Mathf.Abs(dy) < 0.8f || Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 0.8f))
                            c = new Color(1f, 0.6f, 0.15f);
                    }
                    else
                    {
                        var ox = x - 8.5f;
                        var oy = y - 8.0f;
                        var cut = Mathf.Sqrt(ox * ox + oy * oy) < 4.6f;
                        if (r <= 5.4f && !cut) c = new Color(1f, 0.93f, 0.55f);
                        else if (r <= 6.2f && !cut) c = ink;
                    }
                    t.SetPixel(x, y, c);
                }
            t.Apply(false, true);
            return t;
        }
    }
}
