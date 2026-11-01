using System;
using System.Numerics;

namespace Halka.Poc2D3D.Life
{
    // ver4.0 ﾆｺﾁｭｰﾌﾞ (a made-up video site): the channel's numbers and the one action, 「動画を投稿する」.
    // No genre, title, thumbnail, comments or per-video pages (指示書 9.1): a post is a number of
    // views, new subscribers and money, decided from the three abilities, the current subscribers
    // and a little luck.
    //
    // The formula (all parameters in NicoTubeTuning; written out in HARUKA_WORLD_VER40_NICOTUBE_SPEC.md):
    //   T   = composition + drawing + singing                    (any size)
    //   bal = (min + 1) / (max + 1) of the three                 (0..1: all three grown evenly = 1)
    //   r   = 0.80 .. 1.25, from the channel's seed and the post number (fixed per post)
    //   views = Base + r * ( A * (1 + T)^E  +  W * subs^F * (0.85 + 0.3 * bal) )
    //   new subscribers = floor(views * (R0 + R1 * bal))
    //   money = floor(views / 10)                                (this post's views, 指示書 9.5)
    // Powers are taken on base-10 logarithms (LifeNumbers), so huge abilities or subscribers give
    // huge whole numbers, never Infinity, NaN or a negative value.
    //
    // A post needs 24 game hours (1440 game minutes = 20 real minutes, the world clock's own
    // elapsed time) since the last post, or since the channel's basis for the first one (the moment
    // ver4.0 data began). A new day alone never unlocks it.
    public sealed class NicoTubeChannel
    {
        public BigInteger Subscribers = BigInteger.Zero;
        public BigInteger TotalViews = BigInteger.Zero;
        public BigInteger Money = BigInteger.Zero;
        public long Posts;
        public long LastPostUnixMs;   // 0: none yet
        public long BasisUnixMs;      // the first post's 24 hours count from here
        public long Seed = 1;         // the luck of every post (set once, saved)

        public long ReferenceUnixMs => LastPostUnixMs > 0 ? LastPostUnixMs : BasisUnixMs;

        public long ElapsedGameMinutes(long nowMs) => LifeClock.GameMinutes(nowMs - ReferenceUnixMs);

        // 0 when a post is possible; never negative, never more than a day (a clock set back waits
        // a whole day at most, it never shows a negative or a growing time).
        public long RemainingGameMinutes(long nowMs)
        {
            var left = NicoTubeTuning.CooldownGameMinutes - ElapsedGameMinutes(nowMs);
            return left < 0L ? 0L : left > NicoTubeTuning.CooldownGameMinutes ? NicoTubeTuning.CooldownGameMinutes : left;
        }

        public bool CanPost(long nowMs) => ReferenceUnixMs > 0 && RemainingGameMinutes(nowMs) == 0L;

        public struct PostResult
        {
            public long Number;
            public BigInteger Views;
            public BigInteger NewSubscribers;
            public BigInteger Money;
        }

        // What a post would bring now (nothing is changed).
        public PostResult Preview(CreatorState creator) => Compute(creator, Posts + 1);

        public PostResult Compute(CreatorState creator, long number)
        {
            var c = creator.Skills[0];
            var d = creator.Skills[1];
            var s = creator.Skills[2];
            var total = c + d + s;
            var min = BigInteger.Min(c, BigInteger.Min(d, s));
            var max = BigInteger.Max(c, BigInteger.Max(d, s));
            var balance = Math.Pow(10d, LifeNumbers.Log10OnePlus(min) - LifeNumbers.Log10OnePlus(max)); // 0..1
            if (double.IsNaN(balance) || balance < 0d) balance = 0d;
            if (balance > 1d) balance = 1d;
            var luck = Luck(Seed, number);
            var t = NicoTubeTuning.Default;
            // log10 of the two parts
            var abilityLog = Math.Log10(t.A) + t.E * LifeNumbers.Log10OnePlus(total);
            var fanLog = Subscribers.Sign > 0
                ? Math.Log10(t.W) + t.F * LifeNumbers.Log10OnePlus(Subscribers - BigInteger.One) + Math.Log10(0.85 + 0.3 * balance)
                : double.NegativeInfinity;
            var sumLog = AddLogs(abilityLog, fanLog) + Math.Log10(luck);
            var views = LifeNumbers.Pow10(sumLog) + t.Base;
            var rate = (long)Math.Round((t.R0 + t.R1 * balance) * 100000d);
            var gain = views * rate / 100000;
            return new PostResult
            {
                Number = number,
                Views = views,
                NewSubscribers = LifeNumbers.NonNegative(gain),
                Money = views / 10,
            };
        }

        // The post itself: every number, the count and the time change together (one value, kept
        // by the caller's save right after). Returns false (nothing changed) when it is too early.
        public bool TryPost(CreatorState creator, long nowMs, out PostResult result)
        {
            result = default;
            if (!CanPost(nowMs)) return false;
            result = Compute(creator, Posts + 1);
            Subscribers += result.NewSubscribers;
            TotalViews += result.Views;
            Money += result.Money;
            Posts = Posts == long.MaxValue ? long.MaxValue : Posts + 1;
            LastPostUnixMs = nowMs;
            return true;
        }

        // log10(10^a + 10^b), either may be -Infinity (nothing).
        private static double AddLogs(double a, double b)
        {
            if (double.IsNegativeInfinity(a)) return b;
            if (double.IsNegativeInfinity(b)) return a;
            var hi = Math.Max(a, b);
            var lo = Math.Min(a, b);
            return hi + Math.Log10(1d + Math.Pow(10d, lo - hi));
        }

        // 0.80 .. 1.25 from the seed and the post number (SplitMix64), the same on every device.
        public static double Luck(long seed, long number)
        {
            unchecked
            {
                var z = (ulong)seed * 0x9E3779B97F4A7C15UL + (ulong)number * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                var unit = (z >> 11) * (1.0 / 9007199254740992.0); // 0..1
                return NicoTubeTuning.LuckMin + (NicoTubeTuning.LuckMax - NicoTubeTuning.LuckMin) * unit;
            }
        }

        public NicoTubeChannel Copy() => (NicoTubeChannel)MemberwiseClone();

        public bool SameAs(NicoTubeChannel o) => o != null && Subscribers == o.Subscribers && TotalViews == o.TotalViews &&
            Money == o.Money && Posts == o.Posts && LastPostUnixMs == o.LastPostUnixMs && BasisUnixMs == o.BasisUnixMs && Seed == o.Seed;
    }

    // The first balance (指示書 9.4: tunable, written in the spec with the growth simulation).
    public sealed class NicoTubeTuning
    {
        public const long CooldownGameMinutes = 24L * 60L;
        public const double LuckMin = 0.80;
        public const double LuckMax = 1.25;

        public long Base = 5;       // a post with no abilities still gets a few views
        public double A = 12;       // views from the abilities: A * (1 + T)^E
        public double E = 0.9;
        public double W = 0.5;      // views from the subscribers: W * subs^F
        public double F = 0.95;
        public double R0 = 0.02;    // share of the views that subscribe: R0 + R1 * balance
        public double R1 = 0.05;

        public static readonly NicoTubeTuning Default = new NicoTubeTuning();
    }
}
