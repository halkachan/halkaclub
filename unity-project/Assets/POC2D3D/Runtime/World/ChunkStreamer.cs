using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    public enum ChunkState { Unloaded, Queued, Ready, Failed }

    // What builds and frees one chunk's presentation (the presenter). Build may throw: the streamer
    // records the failure, retries later and finally asks for the plain fallback.
    public interface IChunkBuilder
    {
        object Build(ChunkCoord coord);
        object BuildFallback(ChunkCoord coord);
        void Release(ChunkCoord coord, object payload);
    }

    public sealed class ChunkRecord
    {
        public ChunkCoord Coord;
        public ChunkState State;
        public object Payload;
        public bool IsFallback;
        public int Failures;
        public double RetryAt;
        public float LoadMilliseconds;
        public int Loads;
        public long LastNeededTick;
    }

    public sealed class ChunkStreamerStats
    {
        public int Loaded, Queued, Failed, FallbackChunks;
        public int TotalLoads, TotalUnloads, TotalCancels, TotalFailures, SyncLoads;
        public float LastLoadMs, MaxLoadMs, LastUnloadMs, MaxUnloadMs, LastUpdateMs, MaxUpdateMs;
        public double TotalLoadMs, TotalUnloadMs;
        public float AverageLoadMs => TotalLoads > 0 ? (float)(TotalLoadMs / TotalLoads) : 0f;
        public float AverageUnloadMs => TotalUnloads > 0 ? (float)(TotalUnloadMs / TotalUnloads) : 0f;
    }

    // ver3.2: which chunks of the outdoor world are drawn. Pure C# (no Unity objects), so the checks
    // drive it frame by frame. Each update:
    //   1. Required = the chunks under the view rectangle (cells around the focus, both views'
    //      reach), one chunk further in the walking direction (prefetch), plus the root chunks of
    //      Objects that reach into them (a wide house is built with every chunk it covers).
    //   2. Keep = Required grown by HysteresisChunks: a chunk is released only when it has left the
    //      keep area, so walking back and forth over a border never rebuilds the same chunk.
    //   3. Missing required chunks are queued nearest first and built within the time budget;
    //      released chunks are freed (Release destroys their GameObjects and Meshes).
    // EnsureReady builds one chunk at once (the step gate, the first frame): no one waits on a queue.
    public sealed class ChunkStreamer
    {
        public const int MaxRetries = 2;
        public const double RetryDelaySeconds = 1.0;

        private readonly OutdoorWorldIndex index;
        private readonly IChunkBuilder builder;
        private readonly Dictionary<ChunkCoord, ChunkRecord> records = new Dictionary<ChunkCoord, ChunkRecord>();
        private readonly HashSet<ChunkCoord> required = new HashSet<ChunkCoord>();
        private readonly HashSet<ChunkCoord> keep = new HashSet<ChunkCoord>();
        private readonly List<ChunkCoord> queue = new List<ChunkCoord>();
        private readonly List<ChunkCoord> scratch = new List<ChunkCoord>();
        private long tick;
        private ChunkCoord focusChunk;

        public int HysteresisChunks { get; set; } = 1;
        public int PrefetchCells { get; set; }
        public ChunkStreamerStats Stats { get; } = new ChunkStreamerStats();
        public int Version { get; private set; }   // changes whenever the set of drawn chunks changes
        public IReadOnlyCollection<ChunkCoord> Required => required;
        public IReadOnlyDictionary<ChunkCoord, ChunkRecord> Records => records;
        public ChunkCoord FocusChunk => focusChunk;
        public CellRect LastRequiredCells { get; private set; }

        public ChunkStreamer(OutdoorWorldIndex worldIndex, IChunkBuilder chunkBuilder)
        {
            index = worldIndex ?? throw new ArgumentNullException(nameof(worldIndex));
            builder = chunkBuilder ?? throw new ArgumentNullException(nameof(chunkBuilder));
            PrefetchCells = index.ChunkSize / 2;
        }

        public int ChunkSize => index.ChunkSize;
        public bool Exists(ChunkCoord coord) => index.ChunkAt(coord) != null;
        public ChunkState StateOf(ChunkCoord coord) => records.TryGetValue(coord, out var r) ? r.State : ChunkState.Unloaded;
        public bool IsReady(ChunkCoord coord) => StateOf(coord) == ChunkState.Ready || index.ChunkAt(coord) == null;
        public bool IsCellReady(Vector2Int cell) => IsReady(index.ChunkOfCell(cell));

        // view: the cells the cameras may show around the focus (inclusive); moving: the last step's
        // direction (zero when standing).
        public void SetFocus(Vector2Int focusCell, CellRect view, Vector2Int moving)
        {
            tick++;
            focusChunk = index.ChunkOfCell(focusCell);
            var area = view;
            if (moving != Vector2Int.zero)
            {
                var ahead = moving * PrefetchCells;
                area = area.Union(new CellRect(view.Min + ahead, view.Max + ahead));
            }
            LastRequiredCells = area;
            required.Clear();
            var c0 = index.ChunkOfCell(area.Min);
            var c1 = index.ChunkOfCell(area.Max);
            for (var cy = c0.Y; cy <= c1.Y; cy++)
            for (var cx = c0.X; cx <= c1.X; cx++)
            {
                var chunk = index.ChunkAt(new ChunkCoord(cx, cy));
                if (chunk == null) continue;   // nothing there (undefined world): nothing to draw
                required.Add(chunk.Coord);
            }
            scratch.Clear();
            foreach (var coord in required)
                foreach (var owner in index.ChunkAt(coord).ReachingOwners) scratch.Add(owner);
            foreach (var owner in scratch) if (index.ChunkAt(owner) != null) required.Add(owner);
            keep.Clear();
            foreach (var coord in required)
                for (var dy = -HysteresisChunks; dy <= HysteresisChunks; dy++)
                for (var dx = -HysteresisChunks; dx <= HysteresisChunks; dx++)
                    keep.Add(new ChunkCoord(coord.X + dx, coord.Y + dy));
            foreach (var coord in required)
            {
                var record = Record(coord);
                record.LastNeededTick = tick;
                if (record.State == ChunkState.Unloaded && !queue.Contains(coord))
                {
                    record.State = ChunkState.Queued;
                    queue.Add(coord);
                }
            }
            // A queued chunk that is no longer needed is cancelled before it is built.
            for (var i = queue.Count - 1; i >= 0; i--)
                if (!keep.Contains(queue[i]))
                {
                    records[queue[i]].State = ChunkState.Unloaded;
                    queue.RemoveAt(i);
                    Stats.TotalCancels++;
                }
            var center = focusChunk;
            queue.Sort((a, b) => a.ChebyshevDistance(center).CompareTo(b.ChebyshevDistance(center)));
        }

        // Builds queued chunks until budgetMs is used (at least one per call) and frees chunks that
        // left the keep area. now: seconds (for retries).
        public void Update(float budgetMs, double now)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            Unload();
            var built = 0;
            for (var i = 0; i < queue.Count;)
            {
                if (built > 0 && (Time.realtimeSinceStartupAsDouble - started) * 1000.0 >= budgetMs) break;
                var coord = queue[i];
                var record = records[coord];
                if (record.State == ChunkState.Failed && now < record.RetryAt) { i++; continue; }
                queue.RemoveAt(i);
                Load(record, now);
                built++;
            }
            RefreshCounts();
            Stats.LastUpdateMs = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000.0);
            Stats.MaxUpdateMs = Mathf.Max(Stats.MaxUpdateMs, Stats.LastUpdateMs);
        }

        // Every required chunk now (the first frame after a map change, a test).
        public void LoadAllRequiredNow(double now)
        {
            Unload();
            foreach (var coord in new List<ChunkCoord>(queue)) EnsureReady(coord, now);
            RefreshCounts();
        }

        // The step gate: the chunk of the cell she is about to enter is drawn now if the prefetch
        // has not done it. A chunk that keeps failing becomes the plain fallback (never a wall).
        public bool EnsureReady(ChunkCoord coord, double now)
        {
            if (index.ChunkAt(coord) == null) return true;
            var record = Record(coord);
            record.LastNeededTick = tick;
            if (record.State == ChunkState.Ready) return true;
            queue.Remove(coord);
            Stats.SyncLoads++;
            Load(record, now, true);
            RefreshCounts();
            return record.State == ChunkState.Ready;
        }

        public void ReleaseAll()
        {
            foreach (var record in records.Values)
                if (record.State == ChunkState.Ready) Free(record);
            records.Clear();
            queue.Clear();
            required.Clear();
            keep.Clear();
            Version++;
            RefreshCounts();
        }

        private ChunkRecord Record(ChunkCoord coord)
        {
            if (!records.TryGetValue(coord, out var record))
            {
                record = new ChunkRecord { Coord = coord, State = ChunkState.Unloaded };
                records[coord] = record;
            }
            return record;
        }

        private void Load(ChunkRecord record, double now, bool mustSucceed = false)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            try
            {
                record.Payload = builder.Build(record.Coord);
                record.IsFallback = false;
                record.State = ChunkState.Ready;
            }
            catch (Exception exception)
            {
                record.Failures++;
                Stats.TotalFailures++;
                Debug.LogWarning("HARUKA chunk " + record.Coord + " failed to build (" + record.Failures + "): " + exception.Message);
                if (record.Failures > MaxRetries || mustSucceed)
                {
                    record.Payload = builder.BuildFallback(record.Coord);
                    record.IsFallback = true;
                    record.State = ChunkState.Ready;
                }
                else
                {
                    record.State = ChunkState.Failed;
                    record.RetryAt = now + RetryDelaySeconds * record.Failures;
                    queue.Add(record.Coord);
                    return;
                }
            }
            record.Loads++;
            record.LoadMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000.0);
            Stats.TotalLoads++;
            Stats.TotalLoadMs += record.LoadMilliseconds;
            Stats.LastLoadMs = record.LoadMilliseconds;
            Stats.MaxLoadMs = Mathf.Max(Stats.MaxLoadMs, record.LoadMilliseconds);
            Version++;
        }

        private void Unload()
        {
            scratch.Clear();
            foreach (var record in records.Values)
                if (record.State == ChunkState.Ready && !keep.Contains(record.Coord)) scratch.Add(record.Coord);
            foreach (var coord in scratch) Free(records[coord]);
        }

        private void Free(ChunkRecord record)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            builder.Release(record.Coord, record.Payload);
            record.Payload = null;
            record.State = ChunkState.Unloaded;
            record.IsFallback = false;
            var ms = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000.0);
            Stats.TotalUnloads++;
            Stats.TotalUnloadMs += ms;
            Stats.LastUnloadMs = ms;
            Stats.MaxUnloadMs = Mathf.Max(Stats.MaxUnloadMs, ms);
            Version++;
        }

        private void RefreshCounts()
        {
            Stats.Loaded = Stats.Queued = Stats.Failed = Stats.FallbackChunks = 0;
            foreach (var record in records.Values)
            {
                if (record.State == ChunkState.Ready) { Stats.Loaded++; if (record.IsFallback) Stats.FallbackChunks++; }
                else if (record.State == ChunkState.Queued) Stats.Queued++;
                else if (record.State == ChunkState.Failed) Stats.Failed++;
            }
        }

        public IEnumerable<ChunkCoord> ReadyChunks()
        {
            foreach (var record in records.Values)
                if (record.State == ChunkState.Ready) yield return record.Coord;
        }
    }
}
