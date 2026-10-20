using UnityEngine;

namespace Halka.Game.World
{
    // A camera-sized world sprite renders after all world art and before IMGUI.
    // It has no Collider or UI raycast target, so gameplay input remains untouched.
    [DefaultExecutionOrder(1000)]
    public sealed class WorldNightOverlay2D : MonoBehaviour
    {
        public const float FadeSeconds = 2.5f;
        public const float OutdoorNightAlpha = 0.33f;
        public const float IndoorNightAlpha = 0.16f;

        [SerializeField] private WorldTimeController worldTime;
        [SerializeField] private MapWorldController2D maps;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer artwork;

        private WorldPhase observedPhase;
        private string observedMap;
        private float fromAlpha;
        private float displayedAlpha;
        private float fadeStartedAt;
        private bool initialized;

        public float DisplayedAlpha => displayedAlpha;
        public static float TargetAlpha(WorldPhase phase, string mapId) =>
            phase == WorldPhase.Morning ? 0f :
            mapId == "halka_house" ? IndoorNightAlpha : OutdoorNightAlpha;

        private void LateUpdate()
        {
            if (worldTime == null || maps == null || worldCamera == null || artwork == null ||
                artwork.sprite == null) return;
            var phase = worldTime.CurrentPhase;
            var mapId = maps.ActiveMapId;
            var now = Time.unscaledTime;
            if (!initialized || observedMap != mapId)
            {
                observedMap = mapId;
                observedPhase = phase;
                displayedAlpha = TargetAlpha(phase, mapId);
                fromAlpha = displayedAlpha;
                fadeStartedAt = now - FadeSeconds;
                initialized = true;
            }
            else if (observedPhase != phase)
            {
                observedPhase = phase;
                fromAlpha = displayedAlpha;
                fadeStartedAt = now;
            }
            var progress = Mathf.Clamp01((now - fadeStartedAt) / FadeSeconds);
            displayedAlpha = Mathf.Lerp(fromAlpha, TargetAlpha(phase, mapId), progress);
            artwork.color = new Color(0.08f, 0.12f, 0.38f, displayedAlpha);

            var spriteSize = artwork.sprite.bounds.size;
            var halfHeight = worldCamera.orthographicSize;
            transform.localPosition = new Vector3(0f, 0f, 10f);
            transform.localScale = new Vector3(
                (2f * halfHeight * worldCamera.aspect + 0.04f) / spriteSize.x,
                (2f * halfHeight + 0.04f) / spriteSize.y, 1f);
        }
    }
}
