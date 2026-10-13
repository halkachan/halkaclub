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

        private RenderTexture target;
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

        public float PixelWorldSize => GridSpace3D.CellSize / PixelsPerCell;
        public float PixelWorldNorth => SquareGround ? PixelWorldSize
            : PixelWorldSize / Mathf.Sin(PitchDegrees * Mathf.Deg2Rad);

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
            }
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
        }

        private static void Release(RenderTexture texture)
        {
            texture.Release();
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }
    }
}
