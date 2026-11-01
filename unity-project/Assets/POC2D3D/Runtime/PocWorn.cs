using System.Collections.Generic;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver4.0 (the user's request: a worn, decadent world in the models and dots themselves): the
    // scenery's toon shader makes its own colours muted, stained and mossy (Shaders/PocWorn.cginc).
    // Strength is one global value; characters get a copy of their material that keeps its colours.
    public static class PocWorn
    {
        public const float Strength = 1f;
        public const string ToonShaderName = "HALKA/POC/ToonVertexColor";
        private static readonly int StrengthId = Shader.PropertyToID("_PocWorn");
        private static readonly int ExemptId = Shader.PropertyToID("_WornExempt");
        private static readonly Dictionary<Material, Material> exemptCopies = new Dictionary<Material, Material>();

        public static float Current { get; private set; } = -1f;

        public static void Apply(float strength = Strength)
        {
            Current = strength;
            Shader.SetGlobalFloat(StrengthId, strength);
        }

        // A character keeps its colours: its toon materials are swapped for copies with _WornExempt = 1.
        public static void Exempt(GameObject root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m == null || m.shader == null || m.shader.name != ToonShaderName || m.GetFloat(ExemptId) > 0.5f) continue;
                    if (!exemptCopies.TryGetValue(m, out var copy) || copy == null)
                    {
                        copy = new Material(m) { name = m.name + " (character)", hideFlags = HideFlags.DontSave };
                        copy.SetFloat(ExemptId, 1f);
                        exemptCopies[m] = copy;
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        public static bool IsExempt(Renderer renderer)
        {
            foreach (var m in renderer.sharedMaterials)
                if (m != null && m.shader != null && m.shader.name == ToonShaderName && m.GetFloat(ExemptId) < 0.5f) return false;
            return true;
        }
    }
}
