using System.Collections.Generic;
using Halka.Game.World;
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
        [Header("Phase 5: the 2D game's grass (GrassField2D sprites and timing)")]
        [SerializeField] private Sprite grassIdle;
        [SerializeField] private Sprite[] grassRustle;
        [SerializeField] private float[] grassFrameSeconds = { 0.09f, 0.09f, 0.09f, 0.09f, 0.42f };

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

        public void ConfigureGrass(Sprite idle, Sprite[] rustle, float[] frameSeconds)
        {
            grassIdle = idle;
            grassRustle = rustle;
            grassFrameSeconds = frameSeconds;
        }

        // ------------------------------------------------------------ Phase 5: grass and feet
        // The 2D game draws its grass sprite on every cell where MapPlacementRules.HasGrass says
        // grass grows (a ground decoration under every object), rustles it when a step starts or
        // the crow hops in, and hides the bottom of the player (10 px of 64) and of the crow on the
        // ground (4 px of 32) on such cells. The 2D view does the same with the same sprites and
        // rules; the 3D grass tufts are left out of the 2D render so grass is never doubled.
        // Feedback after RC1: the 2D view keeps the Phase 4 look (3D grass tufts) by default; the
        // 2D game's grass sprites stay available as an option (?debug=1, "2D grass").
        public bool OriginalGrass2D { get; set; }
        public const int PlayerGrassHiddenPixels = Halka.Game.Player.PlayerGrassOcclusion.GrassPlayerOcclusionPixels;
        public const int CrowGrassHiddenPixels = CrowGrassOcclusion.HiddenPixels;
        public bool GrassReady => grassIdle != null;
        public int GrassCount { get { EnsureGrass(); return grassCells.Count; } }
        public int RustlingCount => grassRustles.Count;
        public int LastPlayerCropPixels { get; private set; }
        // Feedback after RC1: sprite rows hidden behind the cut-down South wall (0 elsewhere).
        public int LastWallCropPixels { get; private set; }
        public int LastCrowCropPixels { get; private set; }
        public int LastGrassDrawn { get; private set; }

        private readonly List<Vector2Int> grassCells = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> grassSet = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, Vector2> grassRustles = new Dictionary<Vector2Int, Vector2>(); // (frame, elapsed)
        private readonly List<Vector2Int> grassDone = new List<Vector2Int>();
        private SharedWorldModel grassWorld;
        private int grassChunkVersion = -1;
        private bool grassHooked;
        private PocCrow hookedCrow;

        // The 2D game's grass rule for any cell of the world (drawn or not).
        public bool HasGrass(Vector2Int cell)
        {
            EnsureGrass();
            if (session != null && session.World != null && session.World.IsOutdoorWorld) return session.World.HasGrass(cell);
            return grassSet.Contains(cell);
        }

        // ver3.2: outdoors only the drawn chunks' grass is listed (the list follows the streamer);
        // the order stays North row first across chunks, so the South rows still overlap them.
        private void EnsureGrass()
        {
            HookGrass();
            if (session == null) return;
            var chunked = session.World != null && session.World.IsOutdoorWorld && worldPresenter != null && worldPresenter.ChunkMode;
            var version = chunked ? worldPresenter.ChunkVersion : -1;
            if (session.World == grassWorld && version == grassChunkVersion) return;
            if (session.World != grassWorld) grassRustles.Clear();
            grassWorld = session.World;
            grassChunkVersion = version;
            grassCells.Clear();
            grassSet.Clear();
            if (chunked)
            {
                var index = grassWorld.OutdoorIndex;
                foreach (var coord in worldPresenter.ReadyChunks())
                {
                    var rect = World.WorldGrid.ChunkRect(coord, index.ChunkSize);
                    for (var y = rect.Min.y; y <= rect.Max.y; y++)
                    for (var x = rect.Min.x; x <= rect.Max.x; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (index.HasGrass(cell)) grassCells.Add(cell);
                    }
                }
                grassCells.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));
                foreach (var cell in grassCells) grassSet.Add(cell);
                return;
            }
            var map = grassWorld.Map;
            for (var y = map.MaxCell.y; y >= map.MinCell.y; y--) // North first: the South rows overlap them
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!MapPlacementRules.HasGrass(map, cell)) continue;
                grassCells.Add(cell);
                grassSet.Add(cell);
            }
        }

        private void HookGrass()
        {
            if (!grassHooked && session != null)
            {
                session.StepStarted += RustleGrassAt;
                grassHooked = true;
            }
            if (crow != hookedCrow)
            {
                if (hookedCrow != null) hookedCrow.Rustled -= OnCrowRustled;
                hookedCrow = crow;
                if (crow != null) crow.Rustled += OnCrowRustled;
            }
        }

        private void OnCrowRustled(Vector2Int cell) => RustleGrassAt(cell);

        // GrassField2D.RustleAt: the cell's grass plays the rustle frames once.
        public void RustleGrassAt(Vector2Int cell)
        {
            if (grassRustle == null || grassRustle.Length == 0 || !HasGrass(cell)) return;
            grassRustles[cell] = Vector2.zero;
        }

        private void AdvanceGrass(float deltaTime)
        {
            if (grassRustles.Count == 0 || grassFrameSeconds == null) return;
            grassDone.Clear();
            var keys = new List<Vector2Int>(grassRustles.Keys);
            foreach (var cell in keys)
            {
                var state = grassRustles[cell];
                var frame = (int)state.x;
                var elapsed = state.y + Mathf.Max(0f, deltaTime);
                while (frame < grassFrameSeconds.Length && elapsed >= grassFrameSeconds[frame])
                {
                    elapsed -= grassFrameSeconds[frame];
                    frame++;
                }
                if (frame >= Mathf.Min(grassFrameSeconds.Length, grassRustle.Length)) grassDone.Add(cell);
                else grassRustles[cell] = new Vector2(frame, elapsed);
            }
            foreach (var cell in grassDone) grassRustles.Remove(cell);
        }

        public Sprite GrassSpriteAt(Vector2Int cell) =>
            grassRustles.TryGetValue(cell, out var state) && grassRustle != null && (int)state.x < grassRustle.Length
                ? grassRustle[(int)state.x] : grassIdle;

        // The cell the 2D game uses for the mask: the destination once a step is half done.
        public static Vector2Int VisualCell(Vector2Int from, Vector2Int to, bool moving, float progress) =>
            moving && progress >= 0.5f ? to : from;

        public int PlayerCropPixels()
        {
            if (!OriginalGrass2D || session == null || session.IsSeated) return 0;
            var cell = VisualCell(session.StepFromCell, session.StepToCell, session.IsMoving, session.StepProgressNormalized);
            return HasGrass(cell) ? PlayerGrassHiddenPixels : 0;
        }

        public int CrowCropPixels()
        {
            if (!OriginalGrass2D || crow == null || !crow.isActiveAndEnabled || crow.Airborne) return 0;
            var cell = VisualCell(crow.StepFromCell, crow.StepToCell, crow.IsMoving, crow.StepProgressNormalized);
            return HasGrass(cell) ? CrowGrassHiddenPixels : 0;
        }

        // Feedback after RC1: in the room the South wall is cut down to its base for the 2D view,
        // and the sprite stands right behind it on the bottom row: the rows of the sprite below the
        // base's top edge (seen from the camera) are behind the wall and are left out, as the wall
        // in front of her feet would hide them. Only where a cut wall is under the sprite (not at
        // the doorway), and never while seated.
        public int WallCropPixels(Sprite sprite, RectInt placed, float pixelSize)
        {
            if (session == null || session.IsSeated || worldPresenter == null || !session.World.IsInterior ||
                sprite == null || pixelSize <= 0f) return 0;
            var world = session.World;
            // The wall cell South of her column on the room's bottom wall row (the doorway is floor).
            var column = Mathf.RoundToInt(session.Position.x);
            if (!world.IsBlocked(new Vector2Int(column, world.MinCell.y))) return 0;
            var baseTop = WallBaseTop();
            if (float.IsNaN(baseTop)) return 0;
            var x = GridSpace3D.ToWorld(new Vector2Int(column, world.MinCell.y)).x;
            var northFace = GridSpace3D.ToWorld(new Vector2Int(column, world.MinCell.y)).z + GridSpace3D.CellSize * 0.5f;
            var line = ScreenPoint(new Vector3(x, baseTop, northFace)).y;
            if (line <= placed.y) return 0;
            return Mathf.Clamp(Mathf.CeilToInt((line - placed.y) / pixelSize), 0, Mathf.RoundToInt(sprite.rect.height));
        }

        private Renderer wallBase;

        // The room's always-drawn wall base (PocInteriorBuilder), its top height; NaN outside.
        private float WallBaseTop()
        {
            if (wallBase == null || !wallBase.gameObject.activeInHierarchy)
            {
                wallBase = null;
                foreach (var item in worldPresenter.GetComponentsInChildren<MeshRenderer>())
                    if (item.name == "Interior wall base (always shown)") { wallBase = item; break; }
            }
            return wallBase != null ? wallBase.bounds.max.y : float.NaN;
        }

        // A world point in screen pixels (origin bottom-left), with the same whole-pixel rounding as the cards.
        private Vector2 ScreenPoint(Vector3 world)
        {
            var viewport = worldCamera.WorldToViewportPoint(world);
            var lowX = Mathf.Round(viewport.x * target.width + 0.25f);
            var lowY = Mathf.Round(viewport.y * target.height + 0.25f);
            return new Vector2(lowX * Scale - Offset.x, lowY * Scale - Offset.y);
        }

        // The 3D grass tufts belong to the 3D view; with the original grass the 2D view leaves them out.
        public void ApplyTuftVisibility(bool pixelViewActive)
        {
            if (worldPresenter != null && worldPresenter.ChunkMode)
            {
                worldPresenter.SetTuftsHidden(pixelViewActive && OriginalGrass2D && GrassReady);
                return;
            }
            var tufts = worldPresenter != null ? worldPresenter.TuftObject : null;
            var renderer = tufts != null ? tufts.GetComponent<Renderer>() : null;
            if (renderer != null) renderer.enabled = !(pixelViewActive && OriginalGrass2D && GrassReady);
        }

        private void DrawGrass(int screenWidth, int screenHeight)
        {
            LastGrassDrawn = 0;
            if (!OriginalGrass2D || !GrassReady || target == null) return;
            EnsureGrass();
            Texture batchTexture = null;
            var open = false;
            foreach (var cell in grassCells)
            {
                var sprite = GrassSpriteAt(cell);
                if (sprite == null) continue;
                if (!PlaceCard(new SpriteCard { Sprite = sprite, Root = GridSpace3D.ToWorld(cell) }, out var rect, out _, out _, out _))
                    continue;
                if (rect.xMax < 0 || rect.yMax < 0 || rect.x > screenWidth || rect.y > screenHeight) continue;
                if (sprite.texture != batchTexture)
                {
                    if (open) EndBatch();
                    batchTexture = sprite.texture;
                    BeginGroundBatch(batchTexture, screenWidth, screenHeight);
                    open = true;
                }
                AddQuad(sprite, rect, 0f, 0f, 0, false);
                LastGrassDrawn++;
            }
            if (open) EndBatch();
        }

        private void BeginGroundBatch(Texture texture, int screenWidth, int screenHeight)
        {
            spriteMaterial.mainTexture = texture;
            spriteMaterial.SetTexture("_MaskTex", outlineMask);
            spriteMaterial.SetFloat("_UseMask", 0f);
            spriteMaterial.SetFloat("_GroundOnly", outlineMask != null ? 1f : 0f);
            spriteMaterial.SetFloat("_Thickness", 0f);
            spriteMaterial.SetFloat("_GroundLayerId", PocOutlineTag.DecorationId);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, screenWidth, 0f, screenHeight);
            spriteMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
        }

        private static void EndBatch()
        {
            GL.End();
            GL.PopMatrix();
        }

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
            ApplyTuftVisibility(false);
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
            // ver3.2: tell the chunk streamer how many cells this view shows around her.
            if (worldPresenter != null && target != null)
                worldPresenter.ViewHalfCells2D = new Vector2Int(
                    Mathf.CeilToInt(target.width * PixelWorldSize / GridSpace3D.CellSize * 0.5f) + 2,
                    Mathf.CeilToInt(target.height * PixelWorldNorth / GridSpace3D.CellSize * 0.5f) + 4);
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
            EnsureGrass();
            AdvanceGrass(deltaTime);
            ApplyTuftVisibility(true);
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
            DrawGrass(screenWidth, screenHeight);
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
            if (crow == null || !crow.isActiveAndEnabled || crow.HiddenForTravel || crow.CurrentSprite == null || target == null) return null;
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
            LastCrowCropPixels = onTop ? 0 : CrowCropPixels();
            DrawCard(card, rect, depthBottom, depthTop, !onTop, screenWidth, screenHeight, LastCrowCropPixels);
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
            LastWallCropPixels = WallCropPixels(sprite, placed, pixelSize);
            LastPlayerCropPixels = Mathf.Max(PlayerCropPixels(), LastWallCropPixels);
            DrawCard(new SpriteCard { Sprite = sprite }, placed, depthBottom, depthTop, true, screenWidth, screenHeight,
                LastPlayerCropPixels);
        }

        private void DrawCard(SpriteCard card, RectInt placed, float depthBottom, float depthTop, bool depthTest,
            int screenWidth, int screenHeight, int cropBottomPixels = 0)
        {
            var useMask = depthTest && outlineMask != null && outlineMaskShader != null;
            spriteMaterial.mainTexture = card.Sprite.texture;
            spriteMaterial.SetTexture("_MaskTex", outlineMask);
            spriteMaterial.SetFloat("_UseMask", useMask ? 1f : 0f);
            spriteMaterial.SetFloat("_GroundOnly", 0f);
            spriteMaterial.SetColor("_OutlineColor", OutlineColor);
            spriteMaterial.SetFloat("_Thickness", OutlineReady ? OutlinePixels : 0);
            spriteMaterial.SetFloat("_MarkerStrength", GrassOutline);
            spriteMaterial.SetFloat("_GroundLayerId", PocOutlineTag.DecorationId);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, screenWidth, 0f, screenHeight);
            spriteMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            AddQuad(card.Sprite, placed, depthBottom, depthTop, cropBottomPixels, card.FlipX);
            GL.End();
            GL.PopMatrix();
        }

        // One sprite quad; cropBottomPixels sprite rows are left out at the bottom (the 2D game's
        // grass mask: the feet are hidden, the grass drawn earlier shows there).
        private void AddQuad(Sprite sprite, RectInt placed, float depthBottom, float depthTop, int cropBottomPixels, bool flipX)
        {
            int x0 = placed.x, y0 = placed.y, width = placed.width, height = placed.height;
            var texture = sprite.texture;
            var tr = sprite.textureRect;
            var u0 = tr.xMin / texture.width;
            var u1 = tr.xMax / texture.width;
            if (flipX) (u0, u1) = (u1, u0);
            var v0 = tr.yMin / texture.height;
            var v1 = tr.yMax / texture.height;
            if (cropBottomPixels > 0 && tr.height > cropBottomPixels)
            {
                var cut = Mathf.RoundToInt(height * cropBottomPixels / tr.height);
                v0 = (tr.yMin + cropBottomPixels) / texture.height;
                depthBottom = Mathf.Lerp(depthBottom, depthTop, cut / (float)height);
                y0 += cut;
                height -= cut;
            }
            var maskW = (float)target.width * Scale;
            var maskH = (float)target.height * Scale;
            var mx0 = (x0 + Offset.x) / maskW;
            var mx1 = (x0 + width + Offset.x) / maskW;
            var my0 = (y0 + Offset.y) / maskH;
            var my1 = (y0 + height + Offset.y) / maskH;
            Vertex(u0, v0, mx0, my0, depthBottom, x0, y0);
            Vertex(u0, v1, mx0, my1, depthTop, x0, y0 + height);
            Vertex(u1, v1, mx1, my1, depthTop, x0 + width, y0 + height);
            Vertex(u1, v0, mx1, my0, depthBottom, x0 + width, y0);
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
            float loX = cx - rangeX, hiX = cx + rangeX, loZ = cz - rangeZ, hiZ = cz + rangeZ;
            // ver3.3: with several outdoor regions the camera may always stand over each region's
            // centre: first_field keeps the framing it had alone (ver3.2), and the camera still
            // follows her over the region border (the range only widens, so it never jumps).
            if (world.OutdoorIndex != null)
                foreach (var region in world.OutdoorIndex.Regions)
                {
                    var centre = GridSpace3D.ToWorld(region.Rect.Min) * 0.5f + GridSpace3D.ToWorld(region.Rect.Max) * 0.5f;
                    loX = Mathf.Min(loX, centre.x); hiX = Mathf.Max(hiX, centre.x);
                    loZ = Mathf.Min(loZ, centre.z); hiZ = Mathf.Max(hiZ, centre.z);
                }
            return new Vector3(Mathf.Clamp(position.x, loX, hiX), 0f, Mathf.Clamp(position.z, loZ, hiZ));
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
        // ver3.5: where a world point is drawn on the screen (pixels, origin bottom-left; z < 0 behind).
        public Vector3 WorldToScreenPoint(Vector3 world)
        {
            var p = worldCamera.WorldToScreenPoint(world);
            return new Vector3(p.x * Scale - Offset.x, p.y * Scale - Offset.y, p.z);
        }

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
