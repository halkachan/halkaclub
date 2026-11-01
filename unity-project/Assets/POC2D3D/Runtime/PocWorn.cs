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

        // ver4.0 (the user: "too green overall, show a few other tones"): the palettes to compare.
        public sealed class Palette
        {
            public string Name;
            public Vector4 Grey;    // rgb: the grey colours are muted towards; a: how much
            public Vector4 Tint;    // rgb: the overall tint
            public Vector4 Stain;   // rgb: the stains' tint
            public Vector4 Foot;    // rgb: the colour at the foot of walls and trunks
            public Vector4 Green;   // a: extra muting of greens
            public Color Sky;       // the 3D view's daytime sky
        }

        public static readonly Palette[] Palettes =
        {
            new Palette { Name = "moss", Grey = new Vector4(1f, 1.02f, 0.9f, 0.4f), Tint = new Vector4(0.95f, 0.94f, 0.84f, 0f),
                Stain = new Vector4(0.9f, 0.98f, 0.88f, 0f), Foot = new Vector4(0.3f, 0.36f, 0.24f, 0f), Green = Vector4.zero, Sky = new Color(0.64f, 0.69f, 0.66f) },
            new Palette { Name = "sepia", Grey = new Vector4(1.06f, 0.98f, 0.86f, 0.45f), Tint = new Vector4(1f, 0.93f, 0.8f, 0f),
                Stain = new Vector4(0.95f, 0.88f, 0.8f, 0f), Foot = new Vector4(0.33f, 0.27f, 0.2f, 0f), Green = new Vector4(0, 0, 0, 0.3f), Sky = new Color(0.72f, 0.66f, 0.58f) },
            new Palette { Name = "ash", Grey = new Vector4(1f, 1f, 1f, 0.5f), Tint = new Vector4(0.95f, 0.94f, 0.93f, 0f),
                Stain = new Vector4(0.9f, 0.9f, 0.9f, 0f), Foot = new Vector4(0.3f, 0.29f, 0.28f, 0f), Green = new Vector4(0, 0, 0, 0.3f), Sky = new Color(0.66f, 0.67f, 0.68f) },
            new Palette { Name = "dusk", Grey = new Vector4(0.98f, 0.96f, 1.04f, 0.45f), Tint = new Vector4(0.94f, 0.9f, 0.95f, 0f),
                Stain = new Vector4(0.9f, 0.86f, 0.92f, 0f), Foot = new Vector4(0.27f, 0.24f, 0.3f, 0f), Green = new Vector4(0, 0, 0, 0.35f), Sky = new Color(0.66f, 0.62f, 0.7f) },
            new Palette { Name = "ochre", Grey = new Vector4(1.08f, 1.02f, 0.8f, 0.4f), Tint = new Vector4(1f, 0.95f, 0.78f, 0f),
                Stain = new Vector4(0.94f, 0.9f, 0.78f, 0f), Foot = new Vector4(0.36f, 0.3f, 0.18f, 0f), Green = new Vector4(0, 0, 0, 0.45f), Sky = new Color(0.74f, 0.7f, 0.58f) },
        };

        public const int ChosenPalette = 3;   // the user chose "D 夕暮れ" (dusk: a purplish grey) after the green moss
        public static int PaletteIndex = ChosenPalette;
        public static Palette CurrentPalette => Palettes[Mathf.Clamp(PaletteIndex, 0, Palettes.Length - 1)];

        public static void Apply(float strength = Strength)
        {
            Current = strength;
            Shader.SetGlobalFloat(StrengthId, strength);
            var p = CurrentPalette;
            Shader.SetGlobalVector("_PocWornGrey", p.Grey);
            Shader.SetGlobalVector("_PocWornTint", p.Tint);
            Shader.SetGlobalVector("_PocWornStain", p.Stain);
            Shader.SetGlobalVector("_PocWornFoot", p.Foot);
            Shader.SetGlobalVector("_PocWornGreen", p.Green);
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
