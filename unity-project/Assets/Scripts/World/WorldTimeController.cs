using System;
using System.Globalization;
using UnityEngine;

namespace Halka.Game.World
{
    public enum WorldPhase { Morning, Night }

    public static class WorldTimeCycle
    {
        public const long PhaseMilliseconds = 600000L;
        public const long CycleMilliseconds = PhaseMilliseconds * 2L;

        public static WorldPhase PhaseAt(long anchorMilliseconds, long nowMilliseconds)
        {
            var elapsed = nowMilliseconds <= anchorMilliseconds ? 0L :
                nowMilliseconds - anchorMilliseconds;
            return elapsed / PhaseMilliseconds % 2L == 0L ? WorldPhase.Morning : WorldPhase.Night;
        }

        public static double SecondsUntilChange(long anchorMilliseconds, long nowMilliseconds)
        {
            var elapsed = nowMilliseconds <= anchorMilliseconds ? 0L :
                nowMilliseconds - anchorMilliseconds;
            return (PhaseMilliseconds - elapsed % PhaseMilliseconds) / 1000.0;
        }

        public static bool TryReadAnchor(string saved, out long anchorMilliseconds)
        {
            anchorMilliseconds = 0L;
            if (!long.TryParse(saved, NumberStyles.None, CultureInfo.InvariantCulture,
                out var parsed)) return false;
            try
            {
                DateTimeOffset.FromUnixTimeMilliseconds(parsed);
                if (parsed <= 0L) return false;
                anchorMilliseconds = parsed;
                return true;
            }
            catch (ArgumentOutOfRangeException) { return false; }
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class WorldTimeController : MonoBehaviour
    {
        public const string SaveKey = "HALKA_WORLD_time_cycle_anchor_v1";

        private long anchorMilliseconds;
        private WorldPhase currentPhase;
        private bool initialized;

        public WorldPhase CurrentPhase => currentPhase;
        public long AnchorMilliseconds => anchorMilliseconds;
        public double SecondsUntilChange => WorldTimeCycle.SecondsUntilChange(
            anchorMilliseconds, ClockMilliseconds());
        public event Action<WorldPhase> PhaseChanged;
        public Func<long> ClockMilliseconds { get; set; } = UtcNowMilliseconds;

        private static long UtcNowMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private void Awake() => LoadOrStart(ClockMilliseconds());
        private void Update() => RefreshAt(ClockMilliseconds());
        private void OnApplicationFocus(bool focused)
        {
            if (focused && initialized) RefreshAt(ClockMilliseconds());
        }

        public void LoadOrStart(long nowMilliseconds)
        {
            var saved = "";
            try { saved = PlayerPrefs.GetString(SaveKey, ""); }
            catch (Exception) { /* Storage may be unavailable in a private browser session. */ }
            if (!WorldTimeCycle.TryReadAnchor(saved, out anchorMilliseconds))
            {
                anchorMilliseconds = nowMilliseconds;
                try
                {
                    PlayerPrefs.SetString(SaveKey,
                        anchorMilliseconds.ToString(CultureInfo.InvariantCulture));
                    PlayerPrefs.Save();
                }
                catch (Exception) { /* Keep the in-memory clock running. */ }
            }
            currentPhase = WorldTimeCycle.PhaseAt(anchorMilliseconds, nowMilliseconds);
            initialized = true;
        }

        public void RefreshAt(long nowMilliseconds)
        {
            if (!initialized) { LoadOrStart(nowMilliseconds); return; }
            var next = WorldTimeCycle.PhaseAt(anchorMilliseconds, nowMilliseconds);
            if (next == currentPhase) return;
            currentPhase = next;
            PhaseChanged?.Invoke(next);
        }
    }
}
