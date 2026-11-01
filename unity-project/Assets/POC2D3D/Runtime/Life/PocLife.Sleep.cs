using UnityEngine;

namespace Halka.Poc2D3D.Life
{
    // ver4.0 (the user's request after the first look): sleeping in the bed. A at the bed (its
    // sleep action point) sleeps when she has been up at least 12 game hours since the life began or
    // since she last woke: the world moves on 6 game hours (World Time's anchor goes back 5 real
    // minutes, so the sky, the clock, the day and everything that counts game time move together:
    // ニコチューブ's wait and the passers-by's stay move on as well). Otherwise 「ねむくないよ」.
    public sealed partial class PocLife
    {
        public const string BedId = "bed_basic";
        public const long AwakeMinutesToSleep = 12L * 60L;
        public const long SleepMinutes = 6L * 60L;
        public const string NotSleepyLine = "ねむくないよ";
        public const string SleptLine = "ぐっすり ねむった！";
        public const float SleepFadeSeconds = 2.6f;   // dark in 0.6 s, dark until 1.4 s, light again at 2.6 s

        public int SleepsThisSession { get; private set; }
        public int SleepRefusals { get; private set; }
        public float SleepFadeStartedAt { get; private set; } = -100f;

        public long AbsoluteMinuteNow => LifeClock.AbsoluteMinute(AnchorMs, NowMs);
        public long AwakeMinutes => State.WakeMinute < 0 ? 0 : System.Math.Max(0L, AbsoluteMinuteNow - State.WakeMinute);
        public bool Sleepy => AwakeMinutes >= AwakeMinutesToSleep;

        // The darkness over the screen while she sleeps (0..1), for the HUD.
        public float SleepFade(float now)
        {
            var t = now - SleepFadeStartedAt;
            if (t < 0f || t > SleepFadeSeconds) return 0f;
            if (t < 0.6f) return t / 0.6f;
            if (t < 1.4f) return 1f;
            return 1f - (t - 1.4f) / (SleepFadeSeconds - 1.4f);
        }

        public bool TrySleep()
        {
            if (!Sleepy)
            {
                SleepRefusals++;
                session.ShowMessage(NotSleepyLine);
                return true;
            }
            var shift = LifeClock.RealMilliseconds(SleepMinutes);
            var now = NowMs;
            if (worldTime != null) worldTime.SetAnchor(AnchorMs - shift, now);
            // what counts game time from a real moment moves on with the world
            var ch = State.Channel;
            if (ch.LastPostUnixMs > 0) ch.LastPostUnixMs -= shift;
            if (ch.BasisUnixMs > 0) ch.BasisUnixMs -= shift;
            foreach (var n in npcs) n.Record.LeaveAtUnixMs -= shift;
            State.WakeMinute = AbsoluteMinuteNow;
            State.Sleeps++;
            SleepsThisSession++;
            SleepFadeStartedAt = Time.unscaledTime;
            session.ShowMessage(SleptLine + " " + LifeClock.TimeText(MinuteOfDay));
            Changed();
            if (saveSystem != null) saveSystem.Write("sleep", Time.unscaledTime);
            return true;
        }

        // The bed answers from any side, facing it (the user's request after the second look: before,
        // only its sleep action point, the bottom-left cell, answered).
        private bool TryBed(SharedObjectInstance target)
        {
            if (target == null || target.DefinitionId != BedId || session.IsMoving) return false;
            if (!session.World.TryInteract(target, session.Cell, session.Facing, false, out _) &&
                !NextTo(target, session.Cell, session.Facing)) return false;
            return TrySleep();
        }
    }
}
