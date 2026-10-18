using UnityEngine;

namespace Halka.Poc2D3D
{
    // Feedback after RC1: an outline in the 3D view too. The 3D camera's picture gets the 2D
    // view's line rule: the Object ID mask (HALKA/POC/OutlineMask, the IDs every model already
    // carries in UV2) is rendered from the same viewpoint by a hidden helper camera, then the
    // composite (HALKA/POC/Outline3D) draws #252B39 around each object where it stands in front
    // of something else. The line is as thick as the 2D view's (its 1 low-resolution pixel after
    // the integer upscale: 2 px on a 576-720 px high screen, up to 3 px). Presentation only.
    [RequireComponent(typeof(Camera))]
    public sealed class PocOutline3D : MonoBehaviour
    {
        [SerializeField] private Shader maskShader;
        [SerializeField] private Material composite;

        private Camera source;
        private Camera maskCamera;
        private RenderTexture mask;

        public bool Enabled { get; set; } = true;
        public Color OutlineColor { get; set; } = new Color32(0x25, 0x2b, 0x39, 0xff);
        public int LastRadius { get; private set; }
        public int RenderedFrames { get; private set; }
        public bool Ready => maskShader != null && composite != null;

        public void Configure(Shader outlineMaskShader, Material outlineComposite)
        {
            maskShader = outlineMaskShader;
            composite = outlineComposite;
        }

        // As thick as the 2D view's 1-texel line after its integer upscale (PocPixelView2D.ChooseScale).
        public static int RadiusFor(int screenHeight) => Mathf.Clamp(PocPixelView2D.ChooseScale(screenHeight, 16), 1, 3);

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!Enabled || !Ready)
            {
                Graphics.Blit(src, dst);
                return;
            }
            Render(src, dst);
        }

        // Also called by the checks and capture tools with their own targets.
        public void Render(RenderTexture src, RenderTexture dst)
        {
            if (source == null) source = GetComponent<Camera>();
            if (maskCamera == null)
            {
                var helper = new GameObject("3D outline mask camera") { hideFlags = HideFlags.HideAndDontSave };
                maskCamera = helper.AddComponent<Camera>();
                maskCamera.enabled = false;
            }
            if (mask == null || mask.width != src.width || mask.height != src.height)
            {
                if (mask != null) mask.Release();
                mask = new RenderTexture(src.width, src.height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "3D outline mask", filterMode = FilterMode.Point, useMipMap = false
                };
                mask.Create();
            }
            maskCamera.CopyFrom(source);
            maskCamera.clearFlags = CameraClearFlags.SolidColor;
            maskCamera.backgroundColor = new Color(0f, 0f, 1f, 1f); // ID 0, far depth
            maskCamera.targetTexture = mask;
            maskCamera.RenderWithShader(maskShader, "RenderType");
            maskCamera.targetTexture = null;
            LastRadius = RadiusFor(src.height);
            composite.SetTexture("_MaskTex", mask);
            composite.SetColor("_OutlineColor", OutlineColor);
            composite.SetFloat("_Radius", LastRadius);
            Graphics.Blit(src, dst, composite);
            RenderedFrames++;
        }

        private void OnDestroy()
        {
            if (mask != null) mask.Release();
            if (maskCamera == null) return;
            if (Application.isPlaying) Destroy(maskCamera.gameObject);
            else DestroyImmediate(maskCamera.gameObject);
        }
    }
}
