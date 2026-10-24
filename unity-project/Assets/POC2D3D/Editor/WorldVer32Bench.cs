using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Halka.Game.Player;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using Halka.Poc2D3D.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // ver3.2: the chunk-size comparison in the Unity Editor (batch mode, the game's own start with
    // the Phase 6 harness). Each case walks the same route (straight, turns, back and forth over a
    // border, a long walk away and back) with the streamer as in play, and records the chunk
    // build / release times, what is drawn, GameObjects, managed memory and the collision lookup.
    // Editor numbers only: Standalone and WebGL are measured separately (report 12, 39-42).
    //   -executeMethod Halka.Poc2D3D.EditorTools.WorldVer32Bench.RunBatch
    public static class WorldVer32Bench
    {
        private sealed class Row
        {
            public string World;
            public int ChunkSize;
            public float IndexMs;
            public int Chunks;
            public float StartMs;
            public int Loads, Unloads, Sync;
            public float LoadAvg, LoadMax, UnloadAvg, UnloadMax, UpdateMax;
            public int LoadedAvg, LoadedMax, ObjectsMax, GameObjectsMax, TrianglesMax, MeshesMax;
            public double FrameAvg, FrameP95, FrameMax;
            public long ManagedDelta;
            public int GcCount;
            public double CollisionNs;
            public int Steps;
        }

        public static void RunBatch()
        {
            var ok = false;
            try { ok = Run(); }
            catch (Exception exception) { Debug.LogException(exception); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            var rows = new List<Row>();
            var keys = new[] { LifeLogController.SaveKey, WorldTimeController.SaveKey };
            var saved = keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
            try
            {
                foreach (var chunk in new[] { 8, 16, 32 })
                {
                    rows.Add(Measure("first_field", chunk, null, null));
                    foreach (var size in new[] { 64, 128, 256 })
                    {
                        // Loaded again each time: a new scene unloads an asset nothing references.
                        var fixture = WorldVer32Checks.LoadOrCreateFixture(false);
                        var world = PocTestWorldGenerator.Build(fixture, size, chunk, WorldVer32Checks.Seed, out var primary);
                        try { rows.Add(Measure(size + "x" + size, chunk, world, primary)); }
                        finally { foreach (var link in world.regions) UnityEngine.Object.DestroyImmediate(link.map); }
                    }
                }
            }
            finally
            {
                C6.DisposeHarness();
                foreach (var pair in saved)
                {
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                    else PlayerPrefs.SetString(pair.Key, pair.Value);
                }
                PlayerPrefs.Save();
            }
            var csv = new StringBuilder();
            csv.AppendLine("world,chunk,index_ms,chunks_in_world,start_ms,steps,loads,unloads,sync_loads,load_avg_ms,load_max_ms,unload_avg_ms,unload_max_ms," +
                "update_max_ms,loaded_avg,loaded_max,chunk_objects_max,gameobjects_max,triangles_max,meshes_max,frame_avg_ms,frame_p95_ms,frame_max_ms," +
                "managed_delta_bytes,gc_count,collision_ns_per_lookup");
            foreach (var r in rows)
                csv.AppendLine(string.Join(",", r.World, r.ChunkSize, F(r.IndexMs), r.Chunks, F(r.StartMs), r.Steps, r.Loads, r.Unloads, r.Sync, F(r.LoadAvg), F(r.LoadMax),
                    F(r.UnloadAvg), F(r.UnloadMax), F(r.UpdateMax), r.LoadedAvg, r.LoadedMax, r.ObjectsMax, r.GameObjectsMax, r.TrianglesMax, r.MeshesMax,
                    F(r.FrameAvg), F(r.FrameP95), F(r.FrameMax), r.ManagedDelta, r.GcCount, F(r.CollisionNs)));
            var path = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V32_LOGS", "chunk_size_bench_editor.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, csv.ToString());
            Debug.Log("HARUKA ver3.2 bench written: " + path + "\n" + csv);
            return true;
        }

        private static string F(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static Row Measure(string name, int chunkSize, OutdoorWorldDefinition world, MapDefinition primary)
        {
            C6.SetLegacy(null, null);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var row = new Row { World = name, ChunkSize = chunkSize };
            var watch = Stopwatch.StartNew();
            var c = C6.Start(new HarukaMemoryStorage(), null, s =>
            {
                if (world != null) s.UseOutdoorWorldForTest(world, primary, false);
                else
                {
                    var official = s.EffectiveOutdoorWorld;
                    s.ConfigureOutdoorWorld(new OutdoorWorldDefinition { worldId = official.worldId, chunkSize = chunkSize, regions = official.regions }, 8);
                }
            });
            row.StartMs = (float)watch.Elapsed.TotalMilliseconds;
            var index = c.Session.OutdoorIndex;
            row.IndexMs = index.BuildMilliseconds;
            row.Chunks = index.Chunks.Count;
            var streamer = c.Presenter.Streamer;
            var loads0 = streamer.Stats.TotalLoads;
            var managed0 = GC.GetTotalMemory(true);
            var gc0 = GC.CollectionCount(0);
            var frames = new List<double>();
            var loadedSum = 0L;
            var samples = 0;

            void Frame(Vector2Int raw)
            {
                var t = Stopwatch.StartNew();
                C6.Frame(c, 1f / 60f, raw);
                if (c.Crow != null && c.Crow.isActiveAndEnabled && c.Crow.gameObject.activeInHierarchy) c.Crow.Tick(C6.Clock);
                c.Presenter.UpdateStreaming(false);
                frames.Add(t.Elapsed.TotalMilliseconds);
                loadedSum += streamer.Stats.Loaded;
                samples++;
                row.LoadedMax = Math.Max(row.LoadedMax, streamer.Stats.Loaded);
                row.ObjectsMax = Math.Max(row.ObjectsMax, c.Presenter.LoadedChunkObjects);
                row.TrianglesMax = Math.Max(row.TrianglesMax, c.Presenter.BuiltTriangles);
            }

            void Hold(Vector2Int direction, int steps)
            {
                // Held key (as the long press): frames until that many steps are done or blocked.
                var target = c.Session.CompletedSteps + steps;
                var raw = direction;
                for (var i = 0; i < steps * 16 && c.Session.CompletedSteps < target; i++) Frame(raw);
                C6.Release(c);
                for (var i = 0; i < 12; i++) Frame(Vector2Int.zero);
            }

            void Count()
            {
                row.GameObjectsMax = Math.Max(row.GameObjectsMax, UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                row.MeshesMax = Math.Max(row.MeshesMax, UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            }

            var start = c.Session.Cell;
            var steps0 = c.Session.CompletedSteps;
            var far = world == null ? 9 : Math.Min(100, world.regions.Max(r => r.map.MaxCell.x + r.offset.x) - 6);
            Count();
            for (var i = 0; i < 30; i++) Frame(Vector2Int.zero);                       // standing
            Hold(Vector2Int.right, far - start.x);                                      // long press East
            Count();
            for (var i = 0; i < 8; i++) { Hold(Vector2Int.up, 2); Hold(Vector2Int.right, 1); Hold(Vector2Int.down, 2); Hold(Vector2Int.left, 1); } // quick turns
            for (var i = 0; i < 10; i++) { Hold(Vector2Int.left, 2); Hold(Vector2Int.right, 2); }                                       // border back and forth
            Hold(Vector2Int.up, world == null ? 5 : 60);
            Count();
            Hold(Vector2Int.left, world == null ? 18 : 120);
            Count();
            Hold(Vector2Int.down, world == null ? 10 : 90);
            Hold(Vector2Int.right, world == null ? 9 : 60);
            for (var i = 0; i < 8; i++) { c.Switcher.Toggle(); Frame(Vector2Int.zero); }  // view switches
            Count();
            row.Steps = (int)(c.Session.CompletedSteps - steps0);
            var s = streamer.Stats;
            row.Loads = s.TotalLoads - loads0;
            row.Unloads = s.TotalUnloads;
            row.Sync = s.SyncLoads;
            row.LoadAvg = s.AverageLoadMs;
            row.LoadMax = s.MaxLoadMs;
            row.UnloadAvg = s.AverageUnloadMs;
            row.UnloadMax = s.MaxUnloadMs;
            row.UpdateMax = s.MaxUpdateMs;
            row.LoadedAvg = samples > 0 ? (int)(loadedSum / samples) : 0;
            frames.Sort();
            row.FrameAvg = frames.Average();
            row.FrameP95 = frames[(int)Math.Min(frames.Count - 1, Math.Ceiling(frames.Count * 0.95) - 1)];
            row.FrameMax = frames[frames.Count - 1];
            row.ManagedDelta = GC.GetTotalMemory(true) - managed0;
            row.GcCount = GC.CollectionCount(0) - gc0;
            var rng = new System.Random(7);
            var bounds = index.Bounds.Expanded(8);
            var probes = new Vector2Int[200000];
            for (var i = 0; i < probes.Length; i++)
                probes[i] = new Vector2Int(rng.Next(bounds.Min.x, bounds.Max.x + 1), rng.Next(bounds.Min.y, bounds.Max.y + 1));
            var blocked = 0;
            var t0 = Stopwatch.StartNew();
            foreach (var p in probes) if (c.Session.OutdoorWorld.IsBlocked(p)) blocked++;
            row.CollisionNs = t0.Elapsed.TotalMilliseconds * 1e6 / probes.Length;
            Debug.Log($"HARUKA bench {name} chunk {chunkSize}: steps {row.Steps} loads {row.Loads} unloads {row.Unloads} load avg {row.LoadAvg:0.00} max {row.LoadMax:0.0} " +
                $"loaded avg {row.LoadedAvg} max {row.LoadedMax} GO {row.GameObjectsMax} frame p95 {row.FrameP95:0.00} max {row.FrameMax:0.0} blocked {blocked}");
            return row;
        }
    }
}
