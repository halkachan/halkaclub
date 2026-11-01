using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver4.0 (the user's request after the second look): the worn, decadent mood, one full-screen pass
    // (Shaders/PocMoodGrade.shader). On the 3D camera it runs as its image effect (after the outline);
    // the 2D presenter sends its finished picture (the view, the characters, the night tint) through
    // Blit. The UI is drawn after both, so its colours stay as they were.
    public sealed class PocMoodGrade : MonoBehaviour
    {
        // The user chose "中" with grain only (no stains, no rain).
        public const float StandardStrength = 0.75f;
        public const float StandardGrain = 0.03f;

        [SerializeField] private Shader shader;
        [SerializeField] private SharedWorldSession session;
        [SerializeField, Range(0f, 1f)] private float strength = StandardStrength;
        [SerializeField, Range(0f, 0.1f)] private float grain = StandardGrain;

        private Material material;

        public float Strength { get => strength; set => strength = value; }
        public float Grain { get => grain; set => grain = value; }
        public bool Active => isActiveAndEnabled && strength > 0f && Material != null;
        public int Passes { get; private set; }

        public void Configure(Shader moodShader, SharedWorldSession worldSession)
        {
            shader = moodShader;
            session = worldSession;
        }

        public Material Material
        {
            get
            {
                if (material == null && shader != null && shader.isSupported)
                    material = new Material(shader) { name = "POC mood grade", hideFlags = HideFlags.DontSave };
                return material;
            }
        }

        // The picture through the mood (destination null: the screen).
        public void Blit(RenderTexture source, RenderTexture destination)
        {
            var m = Material;
            if (m == null || strength <= 0f)
            {
                Graphics.Blit(source, destination);
                return;
            }
            m.SetFloat("_Strength", strength);
            m.SetFloat("_Grain", grain);
            // Indoors the dark around the room stays dark (no mist at the sides).
            m.SetFloat("_Mist", session != null && session.World != null && session.World.IsInterior ? 0f : 1f);
            m.SetFloat("_Seed", Time.frameCount % 997);
            Graphics.Blit(source, destination, m);
            Passes++;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) => Blit(source, destination);

        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
