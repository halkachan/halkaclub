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
        private const float NearPlane = 0.3f;
        private const float FarPlane = CameraDistance * 2f;

        [SerializeField] private Camera worldCamera;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocActorView actor;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.2f;
        [Header("2D outline (Phase 2.1)")]
        [SerializeField] private Shader outlineMaskShader;
        [SerializeField] private Material outlineComposite;
        [Header("2D player sprite (Phase 2.1)")]
        [SerializeField] private Material spriteMaterial;

        private RenderTexture target;
        private RenderTexture outlineMask;
        private RenderTexture outlined;
        private RenderTexture screenImage;
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
        public bool SpriteReady => spriteMaterial != null && actor != null && actor.HasSprites;
        // Where the player sprite was last drawn (destination pixels, origin bottom-left) and
        // how many destination pixels one sprite pixel covers.
        public RectInt LastSpriteRect { get; private set; }
        public float LastSpritePixelSize { get; private set; }
        public float LastSpriteDepthBottom { get; private set; }
        public float LastSpriteDepthTop { get; private set; }
        // World units toward the camera (South) for the sprite's draw-order card.
        public float SpriteDepthBias { get; set; } = 0.1f;

        public float PixelWorldSize => GridSpace3D.CellSize / PixelsPerCell;
        public float PixelWorldNorth => SquareGround ? PixelWorldSize
            : PixelWorldSize / Mathf.Sin(PitchDegrees * Mathf.Deg2Rad);

        public void ConfigureOutline(Shader maskShader, Material composite)
        {
            outlineMaskShader = maskShader;
            outlineComposite = composite;
        }

        public void ConfigureSprite(Material material) => spriteMaterial = material;

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
            // Settle the actor's 2D state (snapped position, hidden model, sprite frame) now, so
            // the mask rendered below and the color image rendered later agree.
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
            // Keep the camera's clip planes equal to the matrix: the mask packs depth / far.
            worldCamera.nearClipPlane = NearPlane;
            worldCamera.farClipPlane = FarPlane;
            worldCamera.projectionMatrix = Matrix4x4.Ortho(-width * 0.5f, width * 0.5f,
                -height * 0.5f, height * 0.5f, NearPlane, FarPlane);
            // The mask also orders the player sprite against the world, so it is needed even
            // with the outline off.
            if ((OutlinePixels > 0 || SpriteReady) && outlineMaskShader != null) RenderOutlineMask();
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

        // The 2D view at screen size: the low-resolution image upscaled exactly like
        // PocPixelPresenter does, plus the player sprite (used by the capture tools and checks).
        public RenderTexture ComposeScreen(int screenWidth, int screenHeight)
        {
            var texture = ComposeOutline();
            if (screenImage == null || screenImage.width != screenWidth || screenImage.height != screenHeight)
            {
                Release(screenImage);
                screenImage = Create("POC 2D screen image", screenWidth, screenHeight, 0);
            }
            PocPixelPresenter.UvTransform(screenWidth, screenHeight, texture.width, texture.height, Scale, Offset,
                out var uvScale, out var uvOffset);
            Graphics.Blit(texture, screenImage, uvScale, uvOffset);
            var previous = RenderTexture.active;
            RenderTexture.active = screenImage;
            DrawActorSprite(screenWidth, screenHeight);
            RenderTexture.active = previous;
            return screenImage;
        }

        // Draws the player's original sprite into the active target (the screen, or a screen-size
        // texture), on top of the upscaled view and before the night overlay and the UI.
        // One sprite pixel covers Scale * PixelsPerCell / 32 screen pixels: the 2D game's density
        // (64 px per unit, 32 px per cell) relative to the world, whatever the Pixel View density.
        public void DrawActorSprite(int screenWidth, int screenHeight)
        {
            LastSpriteRect = default;
            if (!SpriteReady || target == null) return;
            var sprite = actor.CurrentSprite;
            if (sprite == null) return;
            var sprites = actor.Sprites;
            var unitsPerSpritePixel = 1f / sprite.pixelsPerUnit;
            var pixelSize = Scale * PixelsPerCell / (GridSpace3D.CellSize * sprite.pixelsPerUnit);
            var rect = sprite.rect;

            // The sprite's bottom-left corner in the 2D game (x = East, y = North), placed on the
            // ground of this view; the card stands up from there toward the camera.
            var root = actor.transform.position;
            var corner = new Vector3(root.x - sprite.pivot.x * unitsPerSpritePixel, 0f,
                root.z + sprites.offsetY - sprite.pivot.y * unitsPerSpritePixel);
            // Draw order uses a card slightly in front of that edge, so a wide crown whose front
            // reaches the edge from the cell North of the player stays behind it.
            var card = corner + Vector3.back * SpriteDepthBias;
            var viewport = worldCamera.WorldToViewportPoint(corner);
            // Whole low-resolution pixels (+0.25 keeps the choice stable when a grid point falls on
            // a texel centre), then whole screen pixels.
            var lowX = Mathf.Round(viewport.x * target.width + 0.25f);
            var lowY = Mathf.Round(viewport.y * target.height + 0.25f);
            var x0 = Mathf.RoundToInt(lowX * Scale - Offset.x);
            var y0 = Mathf.RoundToInt(lowY * Scale - Offset.y);
            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width * pixelSize));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height * pixelSize));
            LastSpriteRect = new RectInt(x0, y0, width, height);
            LastSpritePixelSize = pixelSize;

            // Depth of an upright card whose projection is exactly this rectangle.
            var pitch = Mathf.Clamp(PitchDegrees, 20f, 89f) * Mathf.Deg2Rad;
            var planePerLowPixel = PixelWorldSize * (SquareGround ? Mathf.Sin(pitch) : 1f);
            var cardHeight = height / (float)Scale * planePerLowPixel / Mathf.Cos(pitch);
            var view = worldCamera.worldToCameraMatrix;
            const float far = FarPlane; // same packing as the mask shader (view depth / far)
            var depthBottom = -view.MultiplyPoint(card).z / far;
            var depthTop = -view.MultiplyPoint(card + Vector3.up * cardHeight).z / far;
            LastSpriteDepthBottom = depthBottom;
            LastSpriteDepthTop = depthTop;

            var texture = sprite.texture;
            var tr = sprite.textureRect;
            var u0 = tr.xMin / texture.width;
            var u1 = tr.xMax / texture.width;
            var v0 = tr.yMin / texture.height;
            var v1 = tr.yMax / texture.height;
            var maskW = (float)target.width * Scale;
            var maskH = (float)target.height * Scale;
            var mx0 = (x0 + Offset.x) / maskW;
            var mx1 = (x0 + width + Offset.x) / maskW;
            var my0 = (y0 + Offset.y) / maskH;
            var my1 = (y0 + height + Offset.y) / maskH;

            var useMask = outlineMask != null && outlineMaskShader != null;
            spriteMaterial.mainTexture = texture;
            spriteMaterial.SetTexture("_MaskTex", outlineMask);
            spriteMaterial.SetFloat("_UseMask", useMask ? 1f : 0f);
            spriteMaterial.SetColor("_OutlineColor", OutlineColor);
            spriteMaterial.SetFloat("_Thickness", OutlineReady ? OutlinePixels : 0);
            spriteMaterial.SetFloat("_MarkerStrength", GrassOutline);
            spriteMaterial.SetFloat("_GroundLayerId", PocOutlineTag.DecorationId);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, screenWidth, 0f, screenHeight);
            spriteMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            Vertex(u0, v0, mx0, my0, depthBottom, x0, y0);
            Vertex(u0, v1, mx0, my1, depthTop, x0, y0 + height);
            Vertex(u1, v1, mx1, my1, depthTop, x0 + width, y0 + height);
            Vertex(u1, v0, mx1, my0, depthBottom, x0 + width, y0);
            GL.End();
            GL.PopMatrix();
        }

        private static void Vertex(float u, float v, float maskU, float maskV, float depth, float x, float y)
        {
            GL.MultiTexCoord2(0, u, v);
            GL.MultiTexCoord3(1, maskU, maskV, depth);
            GL.Vertex3(x, y, 0f);
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
            Release(screenImage);
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
