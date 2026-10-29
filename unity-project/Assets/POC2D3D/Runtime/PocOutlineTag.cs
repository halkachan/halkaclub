using UnityEngine;

namespace Halka.Poc2D3D
{
    // Writes an outline ID and strength into UV2 of a presentation object's meshes (per-instance
    // copies), read only by the 2D outline mask. Using vertex data instead of a property block
    // keeps runtime static batching intact. Gameplay never reads these values.
    public static class PocOutlineTag
    {
        public const int PlayerId = 251;
        public const int DecorationId = 250;
        // Feedback after RC1 (3D outline): the 3D models of ﾊﾙｶﾁｬﾝ and the crow carry IDs too.
        public const int CrowId = 252;
        // A tiny strength that the composite shader replaces by the global grass setting.
        public const float MarkerStrength = 3f / 255f;

        // The name a tagged copy carries (the presenter frees these copies with what it built).
        public const string CopySuffix = " (outline)";

        public static void Apply(GameObject root, int id, float strength)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null) continue;
                var mesh = source.name.EndsWith(CopySuffix) ? source : Object.Instantiate(source);
                mesh.name = source.name.EndsWith(CopySuffix) ? source.name : source.name + CopySuffix;
                var tags = new Vector2[mesh.vertexCount];
                var value = new Vector2(Mathf.Clamp(id, 0, 255), Mathf.Clamp01(strength));
                for (var i = 0; i < tags.Length; i++) tags[i] = value;
                mesh.uv2 = tags;
                filter.sharedMesh = mesh;
            }
        }

        // For meshes that are already unique to the map (generated ground, grass tufts).
        public static void ApplyInPlace(Mesh mesh, int id, float strength)
        {
            var tags = new Vector2[mesh.vertexCount];
            var value = new Vector2(Mathf.Clamp(id, 0, 255), Mathf.Clamp01(strength));
            for (var i = 0; i < tags.Length; i++) tags[i] = value;
            mesh.uv2 = tags;
        }
    }
}
