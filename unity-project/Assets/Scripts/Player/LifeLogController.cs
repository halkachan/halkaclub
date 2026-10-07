using System.Runtime.InteropServices;
using UnityEngine;

namespace Halka.Game.Player
{
    // Persistent HUD-side observer. It does not change movement, maps or AUTO behavior.
    [DefaultExecutionOrder(-50)]
    public sealed class LifeLogController : MonoBehaviour
    {
        public const string SaveKey = "HALKA_WORLD_life_log_v1";
        public const float SaveIntervalSeconds = 30f;

        [SerializeField] private PlayerMover player;

        private LifeLogData data;
        private double lastSampleAt;
        private float lastSaveAt;
        private bool hasFocus = true;
        private bool paused;
        private bool ready;
        private bool dirty;

        public long TotalSteps => data?.TotalSteps ?? 0;
        public long TotalPlaySeconds => data?.TotalPlaySeconds ?? 0;
        public string StepsLabel => data?.StepsLabel ?? "0";
        public string TimeLabel => data?.TimeLabel ?? "0:00:00";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int HalkaLifeLogTabVisible();
#endif

        private void Awake()
        {
            data = LifeLogData.Deserialize(PlayerPrefs.GetString(SaveKey, ""));
            lastSampleAt = Time.realtimeSinceStartupAsDouble;
            lastSaveAt = Time.unscaledTime;
        }

        private void OnEnable()
        {
            if (player != null) player.StepCompleted += OnPlayerStepCompleted;
        }

        private void Start()
        {
            // A Scene component starts after the WebGL loader has finished and the world exists.
            ready = true;
            lastSampleAt = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var foreground = ready && hasFocus && !paused && Application.isFocused;
#if UNITY_WEBGL && !UNITY_EDITOR
            foreground = foreground && HalkaLifeLogTabVisible() != 0;
#endif
            AdvanceClock(now, foreground);
            if (dirty && Time.unscaledTime - lastSaveAt >= SaveIntervalSeconds) Save();
        }

        public void AdvanceClock(double now, bool foreground)
        {
            var elapsed = now - lastSampleAt;
            lastSampleAt = now;
            if (!foreground || elapsed <= 0) return;
            data.AddForegroundSeconds(elapsed);
            dirty = true;
        }

        private void OnPlayerStepCompleted(Vector2Int _) { data.CompleteStep(); dirty = true; }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            lastSampleAt = Time.realtimeSinceStartupAsDouble;
            if (!focused) Save();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            lastSampleAt = Time.realtimeSinceStartupAsDouble;
            if (isPaused) Save();
        }

        public void Save()
        {
            if (data == null || !dirty) return;
            PlayerPrefs.SetString(SaveKey, data.Serialize());
            PlayerPrefs.Save();
            dirty = false;
            lastSaveAt = Time.unscaledTime;
        }

        private void OnDisable()
        {
            if (player != null) player.StepCompleted -= OnPlayerStepCompleted;
            Save();
        }

        private void OnApplicationQuit() => Save();
    }
}
