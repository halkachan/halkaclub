using System;
using System.Numerics;

namespace Halka.Poc2D3D.Life
{
    public enum CreatorSkill { Composition, Drawing, Singing }

    // ver4.0「そうさくせいかつ！」: the three abilities (no cap) and each mini game's records. Only the
    // mini games change them (ApplyScore); the video site reads them.
    public sealed class CreatorState
    {
        public const int SkillCount = 3;
        public static readonly string[] SkillIds = { "composition", "drawing", "singing" };
        public static readonly string[] SkillNames = { "さっきょくりょく", "がりょく", "かしょうりょく" };
        public static readonly string[] SkillNamesKanji = { "作曲力", "画力", "歌唱力" };

        // The first balance (ver4.0 指示書 4.2): score bands of 20 points, +0 to +5. A table so it
        // can be tuned without touching the code that uses it.
        public static readonly int[] GainBandFloor = { 0, 20, 40, 60, 80, 100 };
        public static readonly int[] GainForBand = { 0, 1, 2, 3, 4, 5 };

        public readonly BigInteger[] Skills = { BigInteger.Zero, BigInteger.Zero, BigInteger.Zero };
        public readonly long[] Plays = new long[SkillCount];
        public readonly int[] Best = { -1, -1, -1 };   // -1: never played
        public readonly int[] Last = { -1, -1, -1 };

        public BigInteger this[CreatorSkill skill] => Skills[(int)skill];

        public static int GainFor(int score)
        {
            score = Math.Max(0, Math.Min(100, score));
            var gain = 0;
            for (var i = 0; i < GainBandFloor.Length; i++)
                if (score >= GainBandFloor[i]) gain = GainForBand[i];
            return gain;
        }

        // A finished game (never a cancelled one): the records, then the ability. Returns the gain.
        public int ApplyScore(CreatorSkill skill, int score)
        {
            score = Math.Max(0, Math.Min(100, score));
            var i = (int)skill;
            var gain = GainFor(score);
            Plays[i] = Plays[i] == long.MaxValue ? long.MaxValue : Plays[i] + 1;
            Last[i] = score;
            if (score > Best[i]) Best[i] = score;
            Skills[i] += gain;
            return gain;
        }

        public CreatorState Copy()
        {
            var copy = new CreatorState();
            for (var i = 0; i < SkillCount; i++)
            {
                copy.Skills[i] = Skills[i];
                copy.Plays[i] = Plays[i];
                copy.Best[i] = Best[i];
                copy.Last[i] = Last[i];
            }
            return copy;
        }

        public bool SameAs(CreatorState other)
        {
            if (other == null) return false;
            for (var i = 0; i < SkillCount; i++)
                if (Skills[i] != other.Skills[i] || Plays[i] != other.Plays[i] || Best[i] != other.Best[i] || Last[i] != other.Last[i])
                    return false;
            return true;
        }
    }
}
