using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    public enum PocPhaseOverride { Auto, Morning, Night }

    // One clock (the existing WorldTimeController), two night looks:
    // the 2D Pixel View keeps the existing overlay color and alpha; the 3D view dims lights.
    [DefaultExecutionOrder(40)]
    public sealed class PocTimeOfDay : MonoBehaviour
    {
        public static readonly Color NightColor = new Color(0.08f, 0.12f, 0.38f);
        public const float DaySunIntensity = 0.6f;
        public static readonly Color DayAmbient = new Color(0.5f, 0.52f, 0.55f);
        public static readonly Color DaySky = new Color(0.62f, 0.78f, 0.92f);
        public static readonly Color NightSky = new Color(0.07f, 0.09f, 0.2f);

        [SerializeField] private WorldTimeController worldTime;
        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocViewSwitcher views;
        [SerializeField] private Light sun;
        [SerializeField] private Camera skyCamera;

        private float blend;
        private float blendFrom;
        private float fadeStartedAt = -100f;
        private WorldPhase observed;
        private bool initialized;

        public PocPhaseOverride Override { get; set; } = PocPhaseOverride.Auto;
        public WorldPhase Phase => Override == PocPhaseOverride.Morning ? WorldPhase.Morning :
            Override == PocPhaseOverride.Night ? WorldPhase.Night :
            worldTime != null ? worldTime.CurrentPhase : WorldPhase.Morning;
        public float NightBlend => blend;
        public float PixelOverlayAlpha { get; private set; }

        public void Configure(WorldTimeController clock, SharedWorldSession worldSession,
            PocViewSwitcher viewSwitcher, Light directional, Camera thirdPersonCamera)
        {
            skyCamera = thirdPersonCamera;
            worldTime = clock;
            session = worldSession;
            views = viewSwitcher;
            sun = directional;
        }

        private void LateUpdate() => Apply(Time.unscaledTime);

        public void Apply(float now)
        {
            var phase = Phase;
            var targetBlend = phase == WorldPhase.Night ? 1f : 0f;
            if (!initialized)
            {
                blend = blendFrom = targetBlend;
                observed = phase;
                initialized = true;
            }
            else if (phase != observed)
            {
                observed = phase;
                blendFrom = blend;
                fadeStartedAt = now;
            }
            var progress = Mathf.Clamp01((now - fadeStartedAt) / WorldNightOverlay2D.FadeSeconds);
            blend = Mathf.Lerp(blendFrom, targetBlend, progress);

            var mapId = session != null && session.World != null ? session.World.MapId : "first_field";
            var pixelMode = views == null || views.Mode == PocViewMode.PixelView2D;
            // Same target alpha function as the shipped 2D overlay (outdoor 0.33, indoor 0.16).
            PixelOverlayAlpha = pixelMode ? blend * WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, mapId) : 0f;
            var lightNight = pixelMode ? 0f : blend;
            var indoor = mapId == "halka_house";
            if (sun != null)
            {
                sun.color = Color.Lerp(new Color(1f, 0.96f, 0.88f), new Color(0.55f, 0.62f, 1f), lightNight);
                sun.intensity = Mathf.Lerp(DaySunIntensity, indoor ? 0.42f : 0.26f, lightNight);
            }
            // Indoors the 3D view shows the map backdrop around the room, never the sky.
            if (skyCamera != null)
                skyCamera.backgroundColor = indoor && session != null && session.World != null
                    ? session.World.Map.BackdropColor
                    : Color.Lerp(DaySky, NightSky, lightNight);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(DayAmbient,
                indoor ? new Color(0.3f, 0.32f, 0.45f) : new Color(0.16f, 0.2f, 0.36f), lightNight);
        }
    }
}
