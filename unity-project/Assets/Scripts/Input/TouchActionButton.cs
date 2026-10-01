using UnityEngine;

namespace Halka.Game.Input
{
    [DefaultExecutionOrder(-90)]
    public sealed class TouchActionButton : MonoBehaviour
    {
        [SerializeField] private TouchDpad dpad;
        [SerializeField, Range(0.12f, 0.3f)] private float buttonFraction = 0.21f;
        [SerializeField, Min(48f)] private float minButtonPixels = 64f;
        [SerializeField, Min(64f)] private float maxButtonPixels = 152f;

        private int activeFingerId = -1;
        private bool mouseHeld;
        private bool pendingMousePress;
        private Texture2D background;
        private GUIStyle labelStyle;

        public bool Visible => dpad != null && dpad.Visible;
        public bool JustPressed { get; private set; }
        public Rect ControlBoundsGui => GetBounds();

        private void Update()
        {
            JustPressed = pendingMousePress;
            pendingMousePress = false;
            if (!Visible) return;
            var found = false;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (activeFingerId == -1 && touch.phase == TouchPhase.Began &&
                    IsOverControls(touch.position))
                {
                    activeFingerId = touch.fingerId;
                    JustPressed = true;
                }
                if (touch.fingerId != activeFingerId) continue;
                found = true;
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    activeFingerId = -1;
            }
            if (activeFingerId != -1 && !found) activeFingerId = -1;

        }

        public bool IsOverControls(Vector2 screenPosition)
        {
            if (!Visible) return false;
            return GetBounds().Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private Rect GetBounds()
        {
            var safe = Screen.safeArea;
            var size = Mathf.Clamp(Mathf.Min(safe.width, safe.height) * buttonFraction,
                minButtonPixels, maxButtonPixels);
            var x = Mathf.Clamp(safe.xMax - size - 12f, safe.xMin, safe.xMax - size);
            var bottom = Mathf.Clamp(safe.yMin + 12f, safe.yMin, safe.yMax - size);
            return new Rect(x, Screen.height - bottom - size, size, size);
        }

        private void OnGUI()
        {
            if (!Visible) return;
            if (dpad.PreviewMouse && UnityEngine.Input.touchCount == 0)
            {
                var current = Event.current;
                if (current.button == 0 && current.type == EventType.MouseDown &&
                    GetBounds().Contains(current.mousePosition))
                {
                    mouseHeld = true;
                    pendingMousePress = true;
                }
                if (current.button == 0 && current.type == EventType.MouseUp)
                    mouseHeld = false;
            }
            EnsureStyle();
            var bounds = GetBounds();
            GUI.color = activeFingerId != -1 || mouseHeld ? Color.white : new Color(1f, 1f, 1f, 0.78f);
            GUI.DrawTexture(bounds, background);
            GUI.color = Color.white;
            GUI.Label(bounds, "A", labelStyle);
        }

        private void EnsureStyle()
        {
            if (labelStyle != null) return;
            const int pixels = 64;
            background = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = (pixels - 1) * 0.5f;
            for (var y = 0; y < pixels; y++)
            for (var x = 0; x < pixels; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                background.SetPixel(x, y, distance <= center
                    ? new Color(0.23f, 0.28f, 0.20f, 0.8f) : Color.clear);
            }
            background.Apply();
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        private void OnDisable()
        {
            activeFingerId = -1;
            mouseHeld = false;
            pendingMousePress = false;
            JustPressed = false;
        }

        private void OnDestroy()
        {
            if (background != null) Destroy(background);
        }
    }
}
