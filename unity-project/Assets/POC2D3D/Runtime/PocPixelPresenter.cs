using UnityEngine;

namespace Halka.Poc2D3D
{
    // Draws the 2D Pixel View texture at an exact integer scale (nearest neighbour), the
    // player's original sprite and see-through trees, then the night color the existing
    // WorldNightOverlay2D uses. Attached to a camera that renders nothing itself, so the 3D
    // world is drawn once per frame.
    // Graphics.Blit is used because it handles the render-texture orientation of each
    // graphics API (Graphics.DrawTexture drew the view upside down on WebGL).
    [RequireComponent(typeof(Camera))]
    public sealed class PocPixelPresenter : MonoBehaviour
    {
        [SerializeField] private PocPixelView2D pixelView;
        [SerializeField] private PocTimeOfDay timeOfDay;
        [SerializeField] private Material overlayMaterial;
        [SerializeField] private PocMoodGrade mood;

        public void Configure(PocPixelView2D view, PocTimeOfDay time, Material overlay)
        {
            pixelView = view;
            timeOfDay = time;
            overlayMaterial = overlay;
        }

        // ver4.0: the finished 2D picture goes through the mood pass (the same one as the 3D view's).
        public void ConfigureMood(PocMoodGrade moodGrade) => mood = moodGrade;

        // Screen pixel x maps to texture pixel (x + offset) / scale: an integer scale with an
        // even crop of the few extra texture pixels, so every texel covers scale x scale pixels.
        public static void UvTransform(int screenWidth, int screenHeight, int textureWidth, int textureHeight,
            int scale, Vector2Int offset, out Vector2 uvScale, out Vector2 uvOffset)
        {
            uvScale = new Vector2(screenWidth / (float)(textureWidth * scale), screenHeight / (float)(textureHeight * scale));
            uvOffset = new Vector2(offset.x / (float)(textureWidth * scale), offset.y / (float)(textureHeight * scale));
        }

        private void OnPostRender()
        {
            if (pixelView == null || pixelView.Target == null) return;
            var texture = pixelView.ComposeOutline();
            UvTransform(Screen.width, Screen.height, texture.width, texture.height, pixelView.Scale,
                pixelView.Offset, out var uvScale, out var uvOffset);
            if (mood != null && mood.Active)
            {
                // the view, the characters and the night tint into one picture, then the mood to the screen
                var picture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                picture.filterMode = FilterMode.Point;
                Graphics.Blit(texture, picture, uvScale, uvOffset);
                var previous = RenderTexture.active;
                RenderTexture.active = picture;
                pixelView.DrawOverlays(Screen.width, Screen.height);
                DrawNight();
                RenderTexture.active = previous;
                mood.Blit(picture, null);
                RenderTexture.ReleaseTemporary(picture);
                return;
            }
            Graphics.Blit(texture, (RenderTexture)null, uvScale, uvOffset);
            pixelView.DrawOverlays(Screen.width, Screen.height);
            DrawNight();
        }

        private void DrawNight()
        {
            var alpha = timeOfDay != null ? timeOfDay.PixelOverlayAlpha : 0f;
            if (alpha <= 0.001f || overlayMaterial == null) return;
            GL.PushMatrix();
            GL.LoadOrtho();
            overlayMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(new Color(PocTimeOfDay.NightColor.r, PocTimeOfDay.NightColor.g, PocTimeOfDay.NightColor.b, alpha));
            GL.Vertex3(0f, 0f, 0f);
            GL.Vertex3(0f, 1f, 0f);
            GL.Vertex3(1f, 1f, 0f);
            GL.Vertex3(1f, 0f, 0f);
            GL.End();
            GL.PopMatrix();
        }
    }
}
