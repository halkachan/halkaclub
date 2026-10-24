using System;
using Halka.Poc2D3D.Save;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D.Dev
{
    // ver3.2, chunk test build only (HARUKA_WORLD_ver3.2_CHUNKTEST scene; never in the release
    // scene): before anything starts, the outdoor world becomes a generated test world and the
    // save system writes to memory only, so the test page never reads or writes a browser save.
    //   ?world=32|64|128|256 (default 128)  &chunk=8..64 (default 16)  &seed=N (default 3232)
    [DefaultExecutionOrder(-200)]
    public sealed class PocChunkTestBoot : MonoBehaviour
    {
        [SerializeField] private PocTestWorldFixture fixture;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private HarukaSaveSystem save;
        [SerializeField] private int defaultSize = 128;
        [SerializeField] private int defaultChunkSize = 16;

        public int WorldSize { get; private set; }
        public int ChunkSize { get; private set; }
        public int Seed { get; private set; }

        public void Configure(PocTestWorldFixture testFixture, SharedWorldSession worldSession, HarukaSaveSystem saveSystem)
        {
            fixture = testFixture;
            session = worldSession;
            save = saveSystem;
        }

        private void Awake()
        {
            var url = Application.absoluteURL ?? "";
            WorldSize = Query(url, "world", defaultSize);
            if (Array.IndexOf(PocTestWorldGenerator.Sizes, WorldSize) < 0) WorldSize = defaultSize;
            ChunkSize = Mathf.Clamp(Query(url, "chunk", defaultChunkSize), 8, 64);
            Seed = Query(url, "seed", 3232);
            if (save != null) save.UseStorage(new HarukaMemoryStorage(), new HarukaWebGLSaveBrowser());
            var world = PocTestWorldGenerator.Build(fixture, WorldSize, ChunkSize, Seed, out var primary);
            session.UseOutdoorWorldForTest(world, primary, false);
            Debug.Log($"HARUKA chunk test world {WorldSize}x{WorldSize}, chunk {ChunkSize}, seed {Seed}");
        }

        private static int Query(string url, string key, int fallback)
        {
            var q = url.IndexOf('?');
            if (q < 0) return fallback;
            foreach (var part in url.Substring(q + 1).Split('&', '#'))
            {
                var eq = part.IndexOf('=');
                if (eq > 0 && part.Substring(0, eq) == key && int.TryParse(part.Substring(eq + 1), out var value)) return value;
            }
            return fallback;
        }
    }
}
