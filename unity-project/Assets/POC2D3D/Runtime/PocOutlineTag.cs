using System.Collections.Generic;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Writes an outline ID and strength into UV2 of a presentation object's meshes (per-instance
    // copies), read only by the 2D outline mask. Using vertex data instead of a property block
    // keeps runtime static batching intact. Gameplay never reads these values.
    // ver4.0 (memory): the tagged copies of one build (a chunk, a room) are shared by every Object
    // with the same model mesh, outline id and strength (OutlineCopies), and once runtime static
    // batching has copied their vertices into the combined mesh the copies no renderer uses any
    // more are destroyed (ReleaseBatched). Up to ver3.9 every Object kept its own copies for as long
    // as its chunk was drawn, beside the combined mesh holding the same vertices again.
    public static class PocOutlineTag
    {
        public const int PlayerId = 251;
        public const int DecorationId = 250;
        // Feedback after RC1 (3D outline): the 3D models of ﾊﾙｶﾁｬﾝ and the crow carry IDs too.
        public const int CrowId = 252;
        // ver4.0: Rin, Len and the fans / antis (one id for the characters that walk around).
        public const int CharacterId = 253;
        // ver4.0: furniture on a room's South wall (the kitchen): in 2D always drawn over a character
        // standing behind it (North of it), whatever the depth (PocPixelSprite "_FrontId").
        public const int FrontFurnitureId = 254;
        // A tiny strength that the composite shader replaces by the global grass setting.
        public const float MarkerStrength = 3f / 255f;

        // The name a tagged copy carries (the presenter frees these copies with what it built).
        public const string CopySuffix = " (outline)";

        // ver4.0: since the game started (dev statistics and the checks).
        public static int CopiesMade { get; private set; }
        public static int CopiesShared { get; private set; }
        public static int CopiesReleasedAfterBatching { get; private set; }
        // Development comparisons only (?outlinecopies=keep in a dev page): keep every copy as ver3.9 did.
        public static bool KeepAllCopies { get; set; }

        // The tagged copies made for one build, keyed by what decides their contents: the model
        // mesh (its instance), the outline id and the strength (as the 8-bit value the mask reads).
        public sealed class OutlineCopies
        {
            private readonly Dictionary<(int mesh, int id, int strength), Mesh> copies =
                new Dictionary<(int, int, int), Mesh>();

            public int Count => copies.Count;
            public IEnumerable<Mesh> Meshes => copies.Values;

            public Mesh Get(Mesh source, int id, float strength)
            {
                var key = (source.GetInstanceID(), Mathf.Clamp(id, 0, 255), Mathf.RoundToInt(Mathf.Clamp01(strength) * 255f));
                if (copies.TryGetValue(key, out var shared) && shared != null)
                {
                    CopiesShared++;
                    return shared;
                }
                var mesh = Tagged(source, id, strength);
                copies[key] = mesh;
                return mesh;
            }

            public void Clear() => copies.Clear();
        }

        public static void Apply(GameObject root, int id, float strength) => Apply(root, id, strength, null);

        // shared: the build's copies (null: a copy of its own, as before; the player and the crow).
        public static void Apply(GameObject root, int id, float strength, OutlineCopies shared)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null) continue;
                if (source.name.EndsWith(CopySuffix))
                {
                    WriteTags(source, id, strength);
                    continue;
                }
                filter.sharedMesh = shared != null ? shared.Get(source, id, strength) : Tagged(source, id, strength);
            }
        }

        private static Mesh Tagged(Mesh source, int id, float strength)
        {
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + CopySuffix;
            WriteTags(mesh, id, strength);
            CopiesMade++;
            return mesh;
        }

        private static void WriteTags(Mesh mesh, int id, float strength)
        {
            var tags = new Vector2[mesh.vertexCount];
            var value = new Vector2(Mathf.Clamp(id, 0, 255), Mathf.Clamp01(strength));
            for (var i = 0; i < tags.Length; i++) tags[i] = value;
            mesh.uv2 = tags;
        }

        // For meshes that are already unique to the map (generated ground, grass tufts).
        public static void ApplyInPlace(Mesh mesh, int id, float strength) => WriteTags(mesh, id, strength);

        // ver4.0: after runtime static batching. The copies a renderer under root still uses (not
        // batched: the turning pinwheel blades, a fallback chunk, Edit Mode) are kept in `keep` and
        // freed with the build; the others are destroyed now (their vertices live in the combined mesh).
        public static int ReleaseBatched(Transform root, OutlineCopies copies, List<Mesh> keep)
        {
            var used = new HashSet<Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) used.Add(filter.sharedMesh);
            var released = 0;
            foreach (var mesh in copies.Meshes)
            {
                if (mesh == null) continue;
                if (used.Contains(mesh) || KeepAllCopies)
                {
                    if (!keep.Contains(mesh)) keep.Add(mesh);
                    continue;
                }
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
                released++;
            }
            copies.Clear();
            CopiesReleasedAfterBatching += released;
            return released;
        }

        // A build that failed: every copy it made goes.
        public static void FreeAll(OutlineCopies copies)
        {
            foreach (var mesh in copies.Meshes)
                if (mesh != null)
                {
                    if (Application.isPlaying) Object.Destroy(mesh);
                    else Object.DestroyImmediate(mesh);
                }
            copies.Clear();
        }
    }
}
