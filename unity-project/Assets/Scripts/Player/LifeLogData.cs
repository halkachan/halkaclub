using System;
using System.Globalization;

namespace Halka.Game.Player
{
    // Only completed PlayerMover steps enter this record. Stored numbers are decimal strings
    // so WebGL PlayerPrefs never rounds a 64-bit counter through a float.
    public sealed class LifeLogData
    {
        private double fractionalSeconds;
        public long TotalSteps { get; private set; }
        public long TotalPlaySeconds { get; private set; }
        public static int SaveVersion => 1;

        public void CompleteStep()
        {
            if (TotalSteps < long.MaxValue) TotalSteps++;
        }

        public void AddForegroundSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            fractionalSeconds += seconds;
            if (fractionalSeconds < 1) return;
            var whole = Math.Floor(fractionalSeconds);
            var remaining = long.MaxValue - TotalPlaySeconds;
            TotalPlaySeconds += whole >= remaining ? remaining : (long)whole;
            fractionalSeconds -= whole;
        }

        public string StepsLabel => TotalSteps.ToString("N0", CultureInfo.InvariantCulture);

        public string TimeLabel
        {
            get
            {
                var hours = TotalPlaySeconds / 3600;
                var minutes = (TotalPlaySeconds / 60) % 60;
                var seconds = TotalPlaySeconds % 60;
                return hours.ToString(CultureInfo.InvariantCulture) + ":" +
                    minutes.ToString("D2", CultureInfo.InvariantCulture) + ":" +
                    seconds.ToString("D2", CultureInfo.InvariantCulture);
            }
        }

        public string Serialize() => "v1|" + TotalSteps.ToString(CultureInfo.InvariantCulture) +
            "|" + TotalPlaySeconds.ToString(CultureInfo.InvariantCulture);

        public static LifeLogData Deserialize(string value)
        {
            var data = new LifeLogData();
            if (string.IsNullOrEmpty(value)) return data;
            var parts = value.Split('|');
            if (parts.Length != 3 || parts[0] != "v1") return data;
            if (long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var steps))
                data.TotalSteps = steps;
            if (long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                data.TotalPlaySeconds = seconds;
            return data;
        }
    }
}
