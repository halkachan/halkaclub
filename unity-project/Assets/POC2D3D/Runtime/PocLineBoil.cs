using UnityEngine;

namespace Halka.Poc2D3D
{
    public enum PocLineBoilMode
    {
        Off,
        Lines,
        LinesStrong,
        LinesAndPicture,
    }

    // Feedback after RC1 ("a mysterious world"): the 3D outlines twitch a little all the time, like
    // a turbulent displace on the line layer (a hand-drawn line boil). Outline3D (PocLineBoil.cginc)
    // reads three globals set here: a new random displacement field 6 times a second (slowed down
    // after feedback, was 12), at most 3/4 of a line width. The fills stay put. Not in 2D (feedback:
    // not needed there). Presentation only. Without this driver (the editor checks) the globals
    // stay 0 and every line is still, as before.
    public sealed class PocLineBoil : MonoBehaviour
    {
        public const float StandardStepsPerSecond = 6f;
        public const float StandardAmount = 1f;
        public const float StrongAmount = 1.8f;
        public static readonly int AmountId = Shader.PropertyToID("_HalkaLineBoil");
        public static readonly int StepId = Shader.PropertyToID("_HalkaLineBoilStep");
        public static readonly int PictureId = Shader.PropertyToID("_HalkaLineBoilPicture");

        public PocLineBoilMode Mode { get; set; } = PocLineBoilMode.Lines;
        public float StepsPerSecond { get; set; } = StandardStepsPerSecond;
        public int Step { get; private set; }

        public static int StepAt(float seconds, float stepsPerSecond) =>
            Mathf.FloorToInt(seconds * stepsPerSecond) % 4096;

        public static float AmountFor(PocLineBoilMode mode) =>
            mode == PocLineBoilMode.Off ? 0f : mode == PocLineBoilMode.LinesStrong ? StrongAmount : StandardAmount;

        private void Update()
        {
            Step = StepAt(Time.unscaledTime, StepsPerSecond);
            Apply(Mode, Step);
        }

        private void OnDisable() => Apply(PocLineBoilMode.Off, 0);

        // Also used by the checks and capture tools to show one step.
        public static void Apply(PocLineBoilMode mode, int step)
        {
            Shader.SetGlobalFloat(AmountId, AmountFor(mode));
            Shader.SetGlobalFloat(StepId, step);
            Shader.SetGlobalFloat(PictureId, mode == PocLineBoilMode.LinesAndPicture ? 1f : 0f);
        }
    }
}
