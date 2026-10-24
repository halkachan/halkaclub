using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Halka.Game.World;
using Halka.Poc2D3D.World;
using Unity.Profiling;
using UnityEngine;

namespace Halka.Poc2D3D.Dev
{
    // ver3.2, development only: the World / chunk / frame numbers for the measurements, published
    // twice a second to the page as window.__halkaDev (WebGL) for the browser checks. Off unless the
    // page is opened with ?debug=1 or ?devstats=1; nothing is drawn (the debug panel shows its own
    // lines); compiled out with the scripting define HALKA_STRIP_DEV_TOOLS.
    // Memory: the page adds the WebAssembly heap (HEAPU8 size); Mono heap and GC come from here.
    // Draw calls / GC Alloc come from ProfilerRecorder, which a non-development build leaves empty
    // (published as -1, reported NOT RUN).
    [DefaultExecutionOrder(300)]
    public sealed class PocDevStats : MonoBehaviour
    {
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocWorldPresenter presenter;
        [SerializeField] private PocViewSwitcher views;

        private readonly float[] frames = new float[300];
        private readonly float[] sorted = new float[300];
        private int frameCount;
        private int frameIndex;
        private float publishAt;
        private float countAt;
        private int gameObjects;
        private readonly StringBuilder json = new StringBuilder(1024);
        private ProfilerRecorder drawCalls;
        private ProfilerRecorder gcAlloc;
        private ProfilerRecorder usedMemory;

        public bool Active { get; private set; }
        public string LastJson { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void HalkaDevPublish(string text);
#else
        private static void HalkaDevPublish(string text) { }
#endif

        public void Configure(SharedWorldSession worldSession, PocWorldPresenter worldPresenter, PocViewSwitcher switcher)
        {
            session = worldSession;
            presenter = worldPresenter;
            views = switcher;
        }

        private void Awake()
        {
#if HALKA_STRIP_DEV_TOOLS
            Active = false;
#else
            var url = Application.absoluteURL ?? "";
            Active = url.Contains("debug=1") || url.Contains("devstats=1");
#endif
            if (!Active) enabled = false;
        }

        private void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        }

        private void OnDisable()
        {
            drawCalls.Dispose();
            gcAlloc.Dispose();
            usedMemory.Dispose();
        }

        private void Update()
        {
            frames[frameIndex] = Time.unscaledDeltaTime * 1000f;
            frameIndex = (frameIndex + 1) % frames.Length;
            frameCount = Mathf.Min(frameCount + 1, frames.Length);
            var now = Time.unscaledTime;
            if (now >= countAt)
            {
                gameObjects = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                countAt = now + 2f;
            }
            if (now < publishAt) return;
            publishAt = now + 0.5f;
            LastJson = Build();
            HalkaDevPublish(LastJson);
        }

        // Dev page commands (window.__halkaDevCmd): "borders:1", "grid:0", "view:2d" / "view:3d".
        public void DevCommand(string command)
        {
            if (!Active || string.IsNullOrEmpty(command)) return;
            var parts = command.Split(':');
            var on = parts.Length > 1 && parts[1] == "1";
            switch (parts[0])
            {
                case "borders": presenter.ChunkBordersVisible = on; break;
                case "grid": presenter.GridVisible = on; break;
                case "view": views.SetMode(parts.Length > 1 && parts[1] == "3d" ? PocViewMode.ThirdPerson3D : PocViewMode.PixelView2D); break;
            }
        }

        private string Build()
        {
            var n = frameCount;
            Array.Copy(frames, sorted, n);
            Array.Sort(sorted, 0, n);
            var sum = 0f;
            for (var i = 0; i < n; i++) sum += sorted[i];
            var avg = n > 0 ? sum / n : 0f;
            var p95 = n > 0 ? sorted[Mathf.Min(n - 1, Mathf.CeilToInt(n * 0.95f) - 1)] : 0f;
            var p99 = n > 0 ? sorted[Mathf.Min(n - 1, Mathf.CeilToInt(n * 0.99f) - 1)] : 0f;
            var max = n > 0 ? sorted[n - 1] : 0f;
            var cell = session.Cell;
            var index = session.OutdoorIndex;
            var outdoors = session.World != null && session.World.IsOutdoorWorld;
            var size = index != null ? index.ChunkSize : 16;
            var chunk = WorldGrid.ChunkOf(cell, size);
            var local = WorldGrid.LocalInChunk(cell, size);
            session.TryGetSavePlace(out var placeMap, out var placeCell);
            var stats = presenter.Streamer != null ? presenter.Streamer.Stats : null;
            json.Clear();
            json.Append('{');
            Str("map", session.World != null ? session.World.MapId : "");
            Str("world", outdoors && index != null ? index.WorldId : "");
            Num("x", cell.x); Num("y", cell.y);
            Num("outdoors", outdoors ? 1 : 0);
            Num("chunkX", outdoors ? chunk.X : 0); Num("chunkY", outdoors ? chunk.Y : 0);
            Num("localX", outdoors ? local.x : 0); Num("localY", outdoors ? local.y : 0);
            Num("chunkSize", size);
            Str("saveMap", placeMap ?? ""); Num("saveX", placeCell.x); Num("saveY", placeCell.y);
            Str("view", views.Mode == PocViewMode.PixelView2D ? "2D" : "3D");
            Num("steps", session.CompletedSteps); Num("mapLoads", session.MapLoads);
            Num("frames", n); Num("fps", avg > 0 ? 1000f / avg : 0f); Num("frameAvgMs", avg); Num("frameP95Ms", p95);
            Num("frameP99Ms", p99); Num("frameMaxMs", max);
            Num("loaded", stats?.Loaded ?? 0); Num("queued", stats?.Queued ?? 0); Num("failed", stats?.Failed ?? 0);
            Num("fallback", stats?.FallbackChunks ?? 0); Num("loads", stats?.TotalLoads ?? 0); Num("unloads", stats?.TotalUnloads ?? 0);
            Num("syncLoads", stats?.SyncLoads ?? 0); Num("immediateLoads", stats?.ImmediateLoads ?? 0); Num("cancels", stats?.TotalCancels ?? 0);
            Num("loadLastMs", stats?.LastLoadMs ?? 0); Num("loadAvgMs", stats?.AverageLoadMs ?? 0); Num("loadMaxMs", stats?.MaxLoadMs ?? 0);
            Num("unloadLastMs", stats?.LastUnloadMs ?? 0); Num("unloadAvgMs", stats?.AverageUnloadMs ?? 0); Num("unloadMaxMs", stats?.MaxUnloadMs ?? 0);
            Num("updateLastMs", stats?.LastUpdateMs ?? 0); Num("updateMaxMs", stats?.MaxUpdateMs ?? 0);
            Num("chunkObjects", presenter.LoadedChunkObjects); Num("views", presenter.Views.Count);
            Num("triangles", presenter.BuiltTriangles); Num("gameObjects", gameObjects);
            Num("drawCalls", drawCalls.Valid && drawCalls.LastValue > 0 ? drawCalls.LastValue : -1);
            Num("gcAllocFrame", gcAlloc.Valid ? gcAlloc.LastValue : -1);
            Num("usedMemory", usedMemory.Valid && usedMemory.LastValue > 0 ? usedMemory.LastValue : -1);
            Num("monoHeap", GC.GetTotalMemory(false)); Num("gcCount", GC.CollectionCount(0));
            Num("borders", presenter.ChunkBordersVisible ? 1 : 0);
            var ahead = cell + FacingVector(session.Facing);
            Str("facing", session.Facing.ToString());
            Num("aheadBlocked", session.World != null && !session.World.CanEnter(ahead) ? 1 : 0);
            Num("moving", session.IsMoving ? 1 : 0);
            var ready = new StringBuilder();
            var count = 0;
            foreach (var coord in presenter.ReadyChunks())
            {
                if (count++ >= 120) break;
                ready.Append(coord.X).Append(',').Append(coord.Y).Append(';');
            }
            Str("readyList", ready.ToString());
            Num("t", Time.unscaledTime);
            json.Length--;   // the last comma
            json.Append('}');
            return json.ToString();
        }

        private static Vector2Int FacingVector(Halka.Game.Player.FacingDirection facing) =>
            facing == Halka.Game.Player.FacingDirection.Up ? Vector2Int.up :
            facing == Halka.Game.Player.FacingDirection.Down ? Vector2Int.down :
            facing == Halka.Game.Player.FacingDirection.Left ? Vector2Int.left : Vector2Int.right;

        private void Str(string key, string value) =>
            json.Append('"').Append(key).Append("\":\"").Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",");

        private void Num(string key, double value) =>
            json.Append('"').Append(key).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
    }
}
