using UnityEngine;

namespace Halka.Poc2D3D
{
    // 2D Pixel View: a fixed orthographic camera renders the 3D world into a small
    // RenderTexture whose pixels map to whole screen pixels (integer scale, Point filter).
    // The WebGL template and the existing Sprite pipeline are not touched.
    [DefaultExecutionOrder(60)]
    public sealed class PocPixelView2D : MonoBehaviour
    {
        public const float VisibleCellsTall = 18f; // The existing game shows 9 units (18 cells).
        private const float CameraDistance = 30f;

        [SerializeField] private Camera worldCamera;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocActorView actor;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;
        [Header("2D outline (Phase 2.1)")]
        [SerializeField] private Shader outlineMaskShader;
        [SerializeField] private Material outlineComposite;

        private RenderTexture target;
        private RenderTexture outlineMask;
        private RenderTexture outlined;
        private Vector3 focus;
        private Vector3 velocity;
        private bool snapNext = true;

        public int PixelsPerCell { get; set; } = 16;
        public float PitchDegrees { get; set; } = 50f;
        // Keeps each ground cell square on screen, like the existing 32x32 tiles.
        public bool SquareGround { get; set; } = true;
        public RenderTexture Target => target;
        public int Scale { get; private set; } = 1;
        public Vector2Int Offset { get; private set; }
        public Camera WorldCamera => worldCamera;

        // Lines are drawn in the low-resolution texture (1 or 2 of its pixels), then upscaled.
        public int OutlinePixels { get; set; } = 1;
        public Color OutlineColor { get; set; } = new Color32(0x25, 0x2b, 0x39, 0xff);
        public float GrassOutline { get; set; }
        public float InnerLines { get; set; } = 0.5f;
        public bool OutlineReady => outlineMaskShader != null && outlineComposite != null;
        public RenderTexture OutlineMask => outlineMask;

        public float PixelWorldSize => GridSpace3D.CellSize / PixelsPerCell;
        public float PixelWorldNorth => SquareGround ? PixelWorldSize
            : PixelWorldSize / Mathf.Sin(PitchDegrees * Mathf.Deg2Rad);

        public void ConfigureOutline(Shader maskShader, Material composite)
        {
            outlineMaskShader = maskShader;
            outlineComposite = composite;
        }

        public void Configure(Camera cameraComponent, SharedWorldSession worldSession, PocActorView actorView)
        {
            worldCamera = cameraComponent;
            session = worldSession;
            actor = actorView;
        }

        private void OnEnable()
        {
            snapNext = true;
            if (worldCamera != null) worldCamera.enabled = true;
            if (session != null) session.MapChanged += OnMapChanged;
        }

        private void OnDisable()
        {
            if (worldCamera != null) worldCamera.enabled = false;
            if (session != null) session.MapChanged -= OnMapChanged;
            if (actor != null) actor.PixelSnap = Vector2.zero;
        }

        private void OnMapChanged(SharedWorldModel world) => snapNext = true;

        public void SnapToTarget() => snapNext = true;

        private void LateUpdate() => UpdateView(Time.unscaledDeltaTime, Screen.width, Screen.height);

        public void UpdateView(float deltaTime, int screenWidth, int screenHeight)
        {
            EnsureTarget(screenWidth, screenHeight);
            actor.PixelSnap = new Vector2(PixelWorldSize, PixelWorldNorth);
            actor.PixelViewPitch = Mathf.Clamp(PitchDegrees, 20f, 90f);
            // Settle the actor's 2D pose (snap, lean, scale) now, so the outline mask rendered
            // below and the color image rendered later this frame see exactly the same model.
            actor.Apply(0f);

            var desired = ClampToMap(GridSpace3D.ToWorld(session.Position));
            if (snapNext)
            {
                focus = desired;
                velocity = Vector3.zero;
                snapNext = false;
            }
            else focus = Vector3.SmoothDamp(focus, desired, ref velocity, smoothTime, Mathf.Infinity, deltaTime);

            // Move only by whole low-resolution pixels so static art never shimmers.
            var snapped = new Vector3(Mathf.Round(focus.x / PixelWorldSize) * PixelWorldSize, 0f,
                Mathf.Round(focus.z / PixelWorldNorth) * PixelWorldNorth);
            var pitch = Mathf.Clamp(PitchDegrees, 20f, 90f);
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            worldCamera.transform.SetPositionAndRotation(snapped - rotation * Vector3.forward * CameraDistance, rotation);

            var width = target.width * PixelWorldSize;
            var height = target.height * PixelWorldSize * (SquareGround ? Mathf.Sin(pitch * Mathf.Deg2Rad) : 1f);
            worldCamera.orthographic = true;
            worldCamera.projectionMatrix = Matrix4x4.Ortho(-width * 0.5f, width * 0.5f,
                -height * 0.5f, height * 0.5f, 0.3f, CameraDistance * 2f);
            if (OutlinePixels > 0 && OutlineReady) RenderOutlineMask();
        }

        // Same camera, same pixel-snapped matrices, so the mask lines up texel for texel.
        private void RenderOutlineMask()
        {
            var background = worldCamera.backgroundColor;
            worldCamera.backgroundColor = new Color(0f, 0f, 1f, 1f); // ID 0, far depth
            worldCamera.targetTexture = outlineMask;
            worldCamera.RenderWithShader(outlineMaskShader, "RenderType");
            worldCamera.targetTexture = target;
            worldCamera.backgroundColor = background;
        }

        // Called after the world camera has drawn this frame (and by the capture tools).
        public RenderTexture ComposeOutline()
        {
            if (target == null || OutlinePixels <= 0 || !OutlineReady) return target;
            outlineComposite.SetTexture("_MaskTex", outlineMask);
            outlineComposite.SetColor("_OutlineColor", OutlineColor);
            outlineComposite.SetFloat("_Thickness", OutlinePixels);
            outlineComposite.SetFloat("_InnerStrength", InnerLines);
            outlineComposite.SetFloat("_MarkerStrength", GrassOutline);
            Graphics.Blit(target, outlined, outlineComposite);
            return outlined;
        }

        private Vector3 ClampToMap(Vector3 position)
        {
            var world = session.World;
            var halfW = target.width * PixelWorldSize * 0.5f;
            var halfH = target.height * PixelWorldNorth * 0.5f;
            const float margin = 0.8f; // Same margin as the existing CameraFollow2D bounds.
            var minX = world.MinCell.x * GridSpace3D.CellSize - margin;
            var maxX = world.MaxCell.x * GridSpace3D.CellSize + margin;
            var minZ = world.MinCell.y * GridSpace3D.CellSize - margin;
            var maxZ = world.MaxCell.y * GridSpace3D.CellSize + margin;
            var rangeX = Mathf.Max(0f, (maxX - minX) * 0.5f - halfW);
            var rangeZ = Mathf.Max(0f, (maxZ - minZ) * 0.5f - halfH);
            var cx = (minX + maxX) * 0.5f;
            var cz = (minZ + maxZ) * 0.5f;
            return new Vector3(Mathf.Clamp(position.x, cx - rangeX, cx + rangeX), 0f,
                Mathf.Clamp(position.z, cz - rangeZ, cz + rangeZ));
        }

        // Integer scale chosen so about 18 cells stay visible vertically, as in the 2D game.
        public static int ChooseScale(int screenHeight, int pixelsPerCell) =>
            Mathf.Max(1, Mathf.RoundToInt(screenHeight / (VisibleCellsTall * pixelsPerCell)));

        private void EnsureTarget(int screenWidth, int screenHeight)
        {
            Scale = ChooseScale(screenHeight, PixelsPerCell);
            var width = Mathf.CeilToInt(screenWidth / (float)Scale);
            var height = Mathf.CeilToInt(screenHeight / (float)Scale);
            Offset = new Vector2Int((width * Scale - screenWidth) / 2, (height * Scale - screenHeight) / 2);
            if (target != null && target.width == width && target.height == height) return;
            if (target != null)
            {
                worldCamera.targetTexture = null;
                Release(target);
                Release(outlineMask);
                Release(outlined);
            }
            outlineMask = Create("POC 2D outline mask", width, height, 24);
            outlined = Create("POC 2D outlined view", width, height, 0);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "POC 2D Pixel View",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false
            };
            target.Create();
            worldCamera.targetTexture = target;
        }

        // Screen pixel (origin bottom-left) to a ray through the low-resolution view.
        public Ray ScreenPointToRay(Vector2 screenPosition)
        {
            var pixel = new Vector3((screenPosition.x + Offset.x) / Scale, (screenPosition.y + Offset.y) / Scale, 0f);
            return worldCamera.ScreenPointToRay(pixel);
        }

        private void OnDestroy()
        {
            if (target == null) return;
            if (worldCamera != null) worldCamera.targetTexture = null;
            Release(target);
            Release(outlineMask);
            Release(outlined);
        }

        private static RenderTexture Create(string name, int width, int height, int depth)
        {
            var texture = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false
            };
            texture.Create();
            return texture;
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }
    }
}
