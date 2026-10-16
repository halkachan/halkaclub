using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        [Header("2D see-through trees (Phase 2.1)")]
        [SerializeField] private PocWorldPresenter worldPresenter;
        [SerializeField] private Material fadeMaterial;
        [Header("Phase 4: the crow's original sprites")]
        [SerializeField] private PocCrow crow;

        private RenderTexture target;
        private RenderTexture outlineMask;
        private RenderTexture outlined;
        private RenderTexture screenImage;
        private RenderTexture fadeLayer;
        private readonly Dictionary<PocObjectView, float> fades = new Dictionary<PocObjectView, float>();
        private readonly List<PocObjectView> fadeOrder = new List<PocObjectView>();
        private MaterialPropertyBlock fadeBlock;
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

        // Objects marked fadeInFront2D (trees) that stand in front of the player sprite fade to
        // FadedAlpha over FadeSeconds, and back when the player leaves. Unused layer 30 isolates
        // them for their own render; they keep casting their shadow on the ground.
        public const int FadeLayerIndex = 30;
        public bool FadeEnabled { get; set; } = true;
        public float FadedAlpha { get; set; } = 0.4f;
        public float FadeSeconds { get; set; } = 0.25f;
        public bool FadeReady => fadeMaterial != null && worldPresenter != null;
        public int FadingCount => fades.Count;
        public RenderTexture FadeLayer => fadeLayer;
        public float FadeOf(PocObjectView view) => view != null && fades.TryGetValue(view, out var a) ? a : 1f;

        public float PixelWorldSize => GridSpace3D.CellSize / PixelsPerCell;
        public float PixelWorldNorth => SquareGround ? PixelWorldSize
            : PixelWorldSize / Mathf.Sin(PitchDegrees * Mathf.Deg2Rad);

        public void ConfigureOutline(Shader maskShader, Material composite)
        {
            outlineMaskShader = maskShader;
            outlineComposite = composite;
        }

        public void ConfigureSprite(Material material) => spriteMaterial = material;
        public void ConfigureCrow(PocCrow sharedCrow) => crow = sharedCrow;

        // Where the crow sprite was last drawn (destination pixels), and whether it was drawn on top.
        public RectInt LastCrowRect { get; private set; }
        public bool LastCrowOnTop { get; private set; }
        public Sprite LastCrowSprite { get; private set; }

        public void ConfigureFade(PocWorldPresenter presenter, Material material)
        {
            worldPresenter = presenter;
            fadeMaterial = material;
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
            RestoreAllFades();
        }

        private void OnMapChanged(SharedWorldModel world)
        {
            snapNext = true;
            fades.Clear(); // the old map's views are gone
            fadeOrder.Clear();
        }

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
            UpdateWallCutaway();
            UpdateFades(deltaTime);
            // The mask also orders the player sprite against the world (and the faded trees are
            // depth-tested against it), so it is needed even with the outline off.
            if ((OutlinePixels > 0 || SpriteReady || fades.Count > 0) && outlineMaskShader != null) RenderOutlineMask();
            RenderFadeLayer();
        }

        // The 2D camera always looks North: interior wall sides facing South are cut down to
        // their base, like the flat wall rows of the 2D room (walls never hide the floor).
        public int CutWalls { get; private set; }

        private void UpdateWallCutaway()
        {
            CutWalls = 0;
            if (worldPresenter == null) return;
            var interior = session.World.IsInterior;
            foreach (var view in worldPresenter.Views)
            {
                if (view == null || !view.IsWall) continue;
                var cut = interior && Vector2.Dot(view.WallOutward, Vector2.down) > PocThirdPersonCamera.CutawayFacing;
                view.SetCutaway(cut);
                if (cut) CutWalls++;
            }
        }

        // ------------------------------------------------------------ see-through trees

        private void UpdateFades(float deltaTime)
        {
            if (!FadeEnabled || !FadeReady)
            {
                RestoreAllFades();
                return;
            }
            var targets = new HashSet<PocObjectView>();
            if (PlaceSprite(out _, out var spriteRect, out _, out _, out _))
            {
                foreach (var view in worldPresenter.Views)
                {
                    // In front means on the player's row or further South; trees to the North
                    // are already behind the sprite.
                    if (view == null || !view.FadeInFront2D || view.RootCell.y > session.Position.y + 0.5f) continue;
                    if (ScreenRect(view).Overlaps(spriteRect)) targets.Add(view);
                }
            }
            foreach (var view in targets)
                if (!fades.ContainsKey(view))
                {
                    fades[view] = 1f;
                    fadeOrder.Add(view);
                }
            for (var i = fadeOrder.Count - 1; i >= 0; i--)
            {
                var view = fadeOrder[i];
                var inFront = targets.Contains(view);
                // Phase 4: the view's own opacity and time (Presentation Metadata, shared with 3D).
                var opacity = view != null ? view.FadeOpacity : FadedAlpha;
                var seconds = view != null ? view.FadeSeconds : FadeSeconds;
                var step = seconds > 0f ? deltaTime / seconds * (1f - opacity) : 1f;
                var alpha = Mathf.MoveTowards(fades[view], inFront ? opacity : 1f, step);
                if (view == null || (!inFront && alpha >= 1f))
                {
                    Restore(view);
                    fades.Remove(view);
                    fadeOrder.RemoveAt(i);
                    continue;
                }
                fades[view] = alpha;
                HideFromWorld(view);
            }
        }

        // Screen-pixel rectangle covered by the view's renderers (same mapping as the sprite).
        private RectInt ScreenRect(PocObjectView view)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var item in view.Renderers)
            {
                if (item == null) continue;
                if (first) bounds = item.bounds;
                else bounds.Encapsulate(item.bounds);
                first = false;
            }
            if (first) return default;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var v = worldCamera.WorldToViewportPoint(corner);
                var x = v.x * target.width * Scale - Offset.x;
                var y = v.y * target.height * Scale - Offset.y;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            return new RectInt(Mathf.FloorToInt(minX), Mathf.FloorToInt(minY),
                Mathf.CeilToInt(maxX - minX), Mathf.CeilToInt(maxY - minY));
        }

        // Not drawn by the world camera or the mask (the sprite is no longer hidden by it), but
        // its shadow stays on the ground.
        private void HideFromWorld(PocObjectView view)
        {
            foreach (var item in view.Renderers)
            {
                if (item == null) continue;
                item.gameObject.layer = FadeLayerIndex;
                if (view.CastShadows) item.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                else item.forceRenderingOff = true;
            }
        }

        private static void Restore(PocObjectView view)
        {
            if (view == null) return;
            foreach (var item in view.Renderers)
            {
                if (item == null) continue;
                item.gameObject.layer = 0;
                item.forceRenderingOff = false;
                item.SetPropertyBlock(null);
            }
            view.SetHidden(false);
        }

        public void RestoreAllFades()
        {
            foreach (var view in fadeOrder) Restore(view);
            fades.Clear();
            fadeOrder.Clear();
        }

        // The faded objects alone, depth-tested against the rest of the world (the mask's depth
        // buffer), alpha = each one's current opacity.
        private void RenderFadeLayer()
        {
            if (fades.Count == 0 || outlineMask == null) return;
            if (fadeLayer == null || fadeLayer.width != target.width || fadeLayer.height != target.height)
            {
                Release(fadeLayer);
                fadeLayer = Create("POC 2D see-through layer", target.width, target.height, 0);
            }
            var previous = RenderTexture.active;
            RenderTexture.active = fadeLayer;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
            if (fadeBlock == null) fadeBlock = new MaterialPropertyBlock();
            foreach (var view in fadeOrder)
                foreach (var item in view.Renderers)
                {
                    if (item == null) continue;
                    item.shadowCastingMode = ShadowCastingMode.Off;
                    item.forceRenderingOff = false;
                    fadeBlock.SetFloat("_Alpha", fades[view]);
                    item.SetPropertyBlock(fadeBlock);
                }
            var mask = worldCamera.cullingMask;
            var flags = worldCamera.clearFlags;
            worldCamera.cullingMask = 1 << FadeLayerIndex;
            worldCamera.clearFlags = CameraClearFlags.Nothing;
            worldCamera.SetTargetBuffers(fadeLayer.colorBuffer, outlineMask.depthBuffer);
            worldCamera.Render();
            worldCamera.targetTexture = target;
            worldCamera.cullingMask = mask;
            worldCamera.clearFlags = flags;
            foreach (var view in fadeOrder) HideFromWorld(view);
        }

        // Over the upscaled view and the sprite: the see-through layer at its own opacity.
        public void DrawFadeLayer(int screenWidth, int screenHeight)
        {
            if (fades.Count == 0 || fadeLayer == null || fadeMaterial == null) return;
            var w = (float)fadeLayer.width * Scale;
            var h = (float)fadeLayer.height * Scale;
            fadeMaterial.mainTexture = fadeLayer;
            fadeMaterial.SetColor("_OutlineColor", OutlineColor);
            fadeMaterial.SetFloat("_Thickness", OutlineReady ? OutlinePixels : 0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, screenWidth, 0f, screenHeight);
            fadeMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            Corner(Offset.x / w, Offset.y / h, 0f, 0f);
            Corner(Offset.x / w, (screenHeight + Offset.y) / h, 0f, screenHeight);
            Corner((screenWidth + Offset.x) / w, (screenHeight + Offset.y) / h, screenWidth, screenHeight);
            Corner((screenWidth + Offset.x) / w, Offset.y / h, screenWidth, 0f);
            GL.End();
            GL.PopMatrix();
        }

        private static void Corner(float u, float v, float x, float y)
        {
            GL.TexCoord2(u, v);
            GL.Vertex3(x, y, 0f);
        }

        // Everything drawn after the upscaled view and before the night overlay and the UI.
        // Phase 4: the crow on the ground is ordered with the player by row (the one further
        // North first) and hidden by the world like the player; in the air it is drawn last, above
        // everything, like the 2D game's raised sorting order during a flight.
        public void DrawOverlays(int screenWidth, int screenHeight)
        {
            LastCrowRect = default;
            LastCrowSprite = null;
            var crowCard = CrowCard(out var crowOnTop);
            var crowFirst = crowCard.HasValue && !crowOnTop && crowCard.Value.Root.z > actor.transform.position.z;
            if (crowFirst) DrawCrow(crowCard.Value, false, screenWidth, screenHeight);
            DrawActorSprite(screenWidth, screenHeight);
            if (crowCard.HasValue && !crowOnTop && !crowFirst) DrawCrow(crowCard.Value, false, screenWidth, screenHeight);
            DrawFadeLayer(screenWidth, screenHeight);
            if (crowCard.HasValue && crowOnTop) DrawCrow(crowCard.Value, true, screenWidth, screenHeight);
        }

        // One upright sprite card: the sprite's local origin goes to Root (x East, y height, z North),
        // its 2D-game vertical offset becomes a North offset on the ground, as for the player.
        public struct SpriteCard
        {
            public Sprite Sprite;
            public Vector3 Root;
            public float OffsetNorth;
            public bool FlipX;
        }

        private SpriteCard? CrowCard(out bool onTop)
        {
            onTop = false;
            if (crow == null || !crow.isActiveAndEnabled || crow.CurrentSprite == null || target == null) return null;
            onTop = crow.Airborne;
            var root = crow.transform.position;
            if (!onTop) root.y = 0f;
            if (actor != null && actor.PixelSnap.x > 0f)
            {
                root.x = Mathf.Round(root.x / actor.PixelSnap.x) * actor.PixelSnap.x;
                root.z = Mathf.Round(root.z / actor.PixelSnap.y) * actor.PixelSnap.y;
            }
            return new SpriteCard { Sprite = crow.CurrentSprite, Root = root, OffsetNorth = 0f, FlipX = crow.CurrentFlipX };
        }

        private void DrawCrow(SpriteCard card, bool onTop, int screenWidth, int screenHeight)
        {
            if (!PlaceCard(card, out var rect, out _, out var depthBottom, out var depthTop)) return;
            LastCrowRect = rect;
            LastCrowOnTop = onTop;
            LastCrowSprite = card.Sprite;
            DrawCard(card, rect, depthBottom, depthTop, !onTop, screenWidth, screenHeight);
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
            DrawOverlays(screenWidth, screenHeight);
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
            if (!PlaceSprite(out var sprite, out var placed, out var pixelSize, out var depthBottom, out var depthTop))
                return;
            LastSpriteRect = placed;
            LastSpritePixelSize = pixelSize;
            LastSpriteDepthBottom = depthBottom;
            LastSpriteDepthTop = depthTop;
            DrawCard(new SpriteCard { Sprite = sprite }, placed, depthBottom, depthTop, true, screenWidth, screenHeight);
        }

        private void DrawCard(SpriteCard card, RectInt placed, float depthBottom, float depthTop, bool depthTest,
            int screenWidth, int screenHeight)
        {
            var sprite = card.Sprite;
            int x0 = placed.x, y0 = placed.y, width = placed.width, height = placed.height;

            var texture = sprite.texture;
            var tr = sprite.textureRect;
            var u0 = tr.xMin / texture.width;
            var u1 = tr.xMax / texture.width;
            if (card.FlipX) (u0, u1) = (u1, u0);
            var v0 = tr.yMin / texture.height;
            var v1 = tr.yMax / texture.height;
            var maskW = (float)target.width * Scale;
            var maskH = (float)target.height * Scale;
            var mx0 = (x0 + Offset.x) / maskW;
            var mx1 = (x0 + width + Offset.x) / maskW;
            var my0 = (y0 + Offset.y) / maskH;
            var my1 = (y0 + height + Offset.y) / maskH;

            var useMask = depthTest && outlineMask != null && outlineMaskShader != null;
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

        // Where (destination pixels) and at what depth the current sprite frame goes.
        private bool PlaceSprite(out Sprite sprite, out RectInt rect, out float pixelSize,
            out float depthBottom, out float depthTop)
        {
            sprite = null;
            rect = default;
            pixelSize = 0f;
            depthBottom = depthTop = 0f;
            if (!SpriteReady || target == null) return false;
            sprite = actor.CurrentSprite;
            if (sprite == null) return false;
            // Phase 4: seated, the artwork moves onto the seat (PlayerSeatController's rule).
            return PlaceCard(new SpriteCard { Sprite = sprite, Root = actor.SpriteRoot, OffsetNorth = actor.SpriteOffsetNorth },
                out rect, out pixelSize, out depthBottom, out depthTop);
        }

        private bool PlaceCard(SpriteCard spriteCard, out RectInt rect, out float pixelSize,
            out float depthBottom, out float depthTop)
        {
            rect = default;
            pixelSize = 0f;
            depthBottom = depthTop = 0f;
            var sprite = spriteCard.Sprite;
            if (sprite == null || target == null) return false;
            var unitsPerSpritePixel = 1f / sprite.pixelsPerUnit;
            pixelSize = Scale * PixelsPerCell / (GridSpace3D.CellSize * sprite.pixelsPerUnit);
            var spriteRect = sprite.rect;

            // The sprite's bottom-left corner in the 2D game (x = East, y = North), placed on the
            // ground of this view (or at its height, for a crow in the air); the card stands up
            // from there toward the camera.
            var root = spriteCard.Root;
            var corner = new Vector3(root.x - sprite.pivot.x * unitsPerSpritePixel, root.y,
                root.z + spriteCard.OffsetNorth - sprite.pivot.y * unitsPerSpritePixel);
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
            var width = Mathf.Max(1, Mathf.RoundToInt(spriteRect.width * pixelSize));
            var height = Mathf.Max(1, Mathf.RoundToInt(spriteRect.height * pixelSize));

            // Depth of an upright card whose projection is exactly this rectangle.
            var pitch = Mathf.Clamp(PitchDegrees, 20f, 89f) * Mathf.Deg2Rad;
            var planePerLowPixel = PixelWorldSize * (SquareGround ? Mathf.Sin(pitch) : 1f);
            var cardHeight = height / (float)Scale * planePerLowPixel / Mathf.Cos(pitch);
            var view = worldCamera.worldToCameraMatrix;
            const float far = FarPlane; // same packing as the mask shader (view depth / far)
            depthBottom = -view.MultiplyPoint(card).z / far;
            depthTop = -view.MultiplyPoint(card + Vector3.up * cardHeight).z / far;
            rect = new RectInt(x0, y0, width, height);
            return true;
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
            Release(fadeLayer);
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
