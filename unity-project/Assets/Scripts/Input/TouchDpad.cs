using System.Runtime.InteropServices;
using UnityEngine;

namespace Halka.Game.Input
{
    [DefaultExecutionOrder(-100)]
    public sealed class TouchDpad : MonoBehaviour
    {
        [SerializeField] private bool showInEditor;
        [SerializeField, Range(0.12f, 0.3f)] private float buttonFraction = 0.21f;
        [SerializeField, Min(48f)] private float minButtonPixels = 64f;
        [SerializeField, Min(64f)] private float maxButtonPixels = 152f;

        private int activeFingerId = -1;
        private bool mousePreviewHeld;
        private bool visible;
        private bool previewMouse;
        private GUIStyle buttonStyle;
        private Texture2D buttonBackground;
        private Texture2D[] arrowTextures;

        public Vector2Int Direction { get; private set; }
        public bool Visible => visible;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int HalkaTouchUiMode();
#endif

        private void Awake()
        {
            var browserMode = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            browserMode = HalkaTouchUiMode();
#endif
            visible = showInEditor || Application.isMobilePlatform || browserMode > 0;
            previewMouse = showInEditor || browserMode == 2;
        }

        private void Update()
        {
            if (!visible) return;
            var foundActiveFinger = false;
            for (var i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (activeFingerId == -1 && touch.phase == TouchPhase.Began &&
                    DirectionAt(touch.position) != Vector2Int.zero)
                    activeFingerId = touch.fingerId;
                if (touch.fingerId != activeFingerId) continue;
                foundActiveFinger = true;
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    activeFingerId = -1;
                    Direction = Vector2Int.zero;
                }
                else Direction = DirectionAt(touch.position);
            }
            if (activeFingerId != -1 && !foundActiveFinger)
            {
                activeFingerId = -1;
                Direction = Vector2Int.zero;
            }

            if (!previewMouse || UnityEngine.Input.touchCount > 0) return;
            if (UnityEngine.Input.GetMouseButtonDown(0) &&
                DirectionAt(UnityEngine.Input.mousePosition) != Vector2Int.zero)
                mousePreviewHeld = true;
            if (UnityEngine.Input.GetMouseButtonUp(0)) mousePreviewHeld = false;
            if (mousePreviewHeld) Direction = DirectionAt(UnityEngine.Input.mousePosition);
            else if (activeFingerId == -1) Direction = Vector2Int.zero;
        }

        public bool IsOverControls(Vector2 screenPosition)
        {
            if (!visible) return false;
            GetLayout(out _, out _, out _, out _, out var bounds);
            return bounds.Contains(ToGuiPosition(screenPosition));
        }

        private Vector2Int DirectionAt(Vector2 screenPosition)
        {
            GetLayout(out var up, out var down, out var left, out var right, out _);
            var point = ToGuiPosition(screenPosition);
            if (up.Contains(point)) return Vector2Int.up;
            if (down.Contains(point)) return Vector2Int.down;
            if (left.Contains(point)) return Vector2Int.left;
            if (right.Contains(point)) return Vector2Int.right;
            return Vector2Int.zero;
        }

        private static Vector2 ToGuiPosition(Vector2 screenPosition) =>
            new Vector2(screenPosition.x, Screen.height - screenPosition.y);

        private void GetLayout(out Rect up, out Rect down, out Rect left, out Rect right, out Rect bounds)
        {
            var safe = Screen.safeArea;
            var size = Mathf.Clamp(Mathf.Min(safe.width, safe.height) * buttonFraction,
                minButtonPixels, maxButtonPixels);
            var gap = Mathf.Max(4f, size * 0.07f);
            var width = size * 3f + gap * 2f;
            var height = size * 2f + gap;
            var x = Mathf.Clamp(safe.xMin + 12f, safe.xMin, safe.xMax - width);
            var bottom = Mathf.Clamp(safe.yMin + 12f, safe.yMin, safe.yMax - height);
            var y = Screen.height - bottom - height;
            up = new Rect(x + size + gap, y, size, size);
            left = new Rect(x, y + size + gap, size, size);
            down = new Rect(x + size + gap, y + size + gap, size, size);
            right = new Rect(x + (size + gap) * 2f, y + size + gap, size, size);
            bounds = new Rect(x, y, width, height);
        }

        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyle();
            GetLayout(out var up, out var down, out var left, out var right, out _);
            DrawButton(up, 0, Vector2Int.up);
            DrawButton(left, 1, Vector2Int.left);
            DrawButton(down, 2, Vector2Int.down);
            DrawButton(right, 3, Vector2Int.right);
        }

        private void DrawButton(Rect rect, int arrowIndex, Vector2Int direction)
        {
            GUI.color = Direction == direction ? Color.white : new Color(1f, 1f, 1f, 0.72f);
            GUI.Box(rect, GUIContent.none, buttonStyle);
            var iconSize = rect.width * 0.42f;
            GUI.DrawTexture(new Rect(rect.center.x - iconSize * 0.5f,
                rect.center.y - iconSize * 0.5f, iconSize, iconSize), arrowTextures[arrowIndex]);
            GUI.color = Color.white;
        }

        private void EnsureStyle()
        {
            if (buttonStyle != null) return;
            buttonBackground = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            buttonBackground.SetPixel(0, 0, new Color(0.23f, 0.28f, 0.20f, 0.55f));
            buttonBackground.Apply();
            buttonStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { background = buttonBackground }
            };
            arrowTextures = new Texture2D[4];
            for (var direction = 0; direction < arrowTextures.Length; direction++)
                arrowTextures[direction] = CreateArrow(direction);
        }

        private static Texture2D CreateArrow(int direction)
        {
            var texture = new Texture2D(9, 9, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                var filled = direction switch
                {
                    1 => IsUpArrowPixel(y, 8 - x),
                    2 => IsUpArrowPixel(x, 8 - y),
                    3 => IsUpArrowPixel(8 - y, x),
                    _ => IsUpArrowPixel(x, y)
                };
                texture.SetPixel(x, y, filled ? Color.white : Color.clear);
            }
            texture.Apply();
            return texture;
        }

        private static bool IsUpArrowPixel(int x, int y)
        {
            return y >= 5 ? Mathf.Abs(x - 4) <= 8 - y : x >= 3 && x <= 5;
        }

        private void OnDisable()
        {
            activeFingerId = -1;
            mousePreviewHeld = false;
            Direction = Vector2Int.zero;
        }

        private void OnDestroy()
        {
            if (buttonBackground != null) Destroy(buttonBackground);
            if (arrowTextures == null) return;
            foreach (var arrow in arrowTextures)
                if (arrow != null) Destroy(arrow);
        }
    }
}
