using System;
using System.Globalization;
using Halka.Game.World;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: the game's time of day and day count, read from the one world clock (WorldTimeCycle:
    // the anchor and the real time since it). No second clock: one morning/night cycle of 20 real
    // minutes is one game day of 24 hours, so a game minute is 0.8333 real seconds.
    //   - the anchor is 06:00 (the morning phase starts there, as it always has);
    //   - morning (WorldPhase.Morning) is 06:00-17:59, night 18:00-05:59: the phase and the shown
    //     time always agree, because both are the same elapsed real time;
    //   - the day goes up at 00:00 (15 real minutes into each cycle).
    // The day shown is counted from the day the ver4.0 data began (DayOrigin, saved): a new game
    // starts on day 1 at 06:00; an older save keeps its morning/night as it was and starts counting
    // at day 1 on the day it was first played in ver4.0 (no guess at the days played before).
    public static class LifeClock
    {
        public const long MinutesPerDay = 1440L;
        public const long StartMinuteOfDay = 6L * 60L;      // the anchor is 06:00
        public const long NightStartsAt = 18L * 60L;
        // 1440 game minutes per WorldTimeCycle.CycleMilliseconds (1,200,000 ms): 3 per 2,500 ms.
        public const long GameMinutesNumerator = 3L;
        public const long RealMillisecondsDenominator = 2500L;

        // Whole game minutes of a real duration (negative durations are 0: a clock set back never
        // runs the game time backwards past the anchor).
        public static long GameMinutes(long realMilliseconds) =>
            realMilliseconds <= 0L ? 0L : realMilliseconds / RealMillisecondsDenominator * GameMinutesNumerator +
                realMilliseconds % RealMillisecondsDenominator * GameMinutesNumerator / RealMillisecondsDenominator;

        // Real milliseconds for whole game minutes (rounded up: the moment the minute is reached).
        public static long RealMilliseconds(long gameMinutes) =>
            gameMinutes <= 0L ? 0L : (gameMinutes * RealMillisecondsDenominator + GameMinutesNumerator - 1L) / GameMinutesNumerator;

        // Game minutes since the anchor's 00:00 (the anchor itself is minute 360 of day 0).
        public static long AbsoluteMinute(long anchorMs, long nowMs) =>
            StartMinuteOfDay + GameMinutes(nowMs - anchorMs);

        public static long AbsoluteDay(long anchorMs, long nowMs) => AbsoluteMinute(anchorMs, nowMs) / MinutesPerDay;

        public static int MinuteOfDay(long anchorMs, long nowMs) => (int)(AbsoluteMinute(anchorMs, nowMs) % MinutesPerDay);

        public static bool IsNight(int minuteOfDay) => minuteOfDay >= NightStartsAt || minuteOfDay < StartMinuteOfDay;

        // The day shown (1 or more). dayOrigin: AbsoluteDay when ver4.0 began counting.
        public static long ShownDay(long anchorMs, long nowMs, long dayOrigin)
        {
            var day = AbsoluteDay(anchorMs, nowMs) - dayOrigin + 1L;
            return day < 1L ? 1L : day;
        }

        public static string TimeText(int minuteOfDay) =>
            (minuteOfDay / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
            (minuteOfDay % 60).ToString("00", CultureInfo.InvariantCulture);

        // 「3日目 14:35」
        public static string Text(long shownDay, int minuteOfDay) =>
            shownDay.ToString(CultureInfo.InvariantCulture) + "日目 " + TimeText(minuteOfDay);

        // Hours and minutes of a game duration, 「05:42」 (more than 99 hours are shown as 99:59).
        public static string DurationText(long gameMinutes)
        {
            if (gameMinutes < 0L) gameMinutes = 0L;
            var hours = gameMinutes / 60L;
            if (hours > 99L) return "99:59";
            return hours.ToString("00", CultureInfo.InvariantCulture) + ":" +
                (gameMinutes % 60L).ToString("00", CultureInfo.InvariantCulture);
        }

        // The phase the world clock shows for the same moment (the checks compare the two).
        public static WorldPhase PhaseOf(long anchorMs, long nowMs) => WorldTimeCycle.PhaseAt(anchorMs, nowMs);

        public static long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
