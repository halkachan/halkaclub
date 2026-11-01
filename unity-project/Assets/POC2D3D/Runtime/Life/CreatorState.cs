using System;
using System.Numerics;

namespace Halka.Poc2D3D.Life
{
    public enum CreatorSkill { Composition, Drawing, Singing }

    // ver4.0 (the user's request after the first look): each mini game in three difficulties.
    public enum MinigameDifficulty { Easy, Normal, Hard }

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
        // The difficulties (the same 20-point bands): かんたん gives less, やばい much more when played
        // well (a poor やばい game gives about what a poor ふつう game gives). ふつう is the table above.
        public static readonly string[] DifficultyNames = { "かんたん", "ふつう", "やばい" };
        public static readonly string[] DifficultyIds = { "easy", "normal", "hard" };
        public static readonly int[][] GainForBandByDifficulty =
        {
            new[] { 0, 0, 1, 1, 2, 3 },
            GainForBand,
            new[] { 0, 1, 3, 5, 8, 12 },
        };

        public readonly BigInteger[] Skills = { BigInteger.Zero, BigInteger.Zero, BigInteger.Zero };
        public readonly long[] Plays = new long[SkillCount];
        public readonly int[] Best = { -1, -1, -1 };   // -1: never played
        public readonly int[] Last = { -1, -1, -1 };

        public BigInteger this[CreatorSkill skill] => Skills[(int)skill];

        public static int GainFor(int score) => GainFor(score, MinigameDifficulty.Normal);

        public static int GainFor(int score, MinigameDifficulty difficulty)
        {
            score = Math.Max(0, Math.Min(100, score));
            var table = GainForBandByDifficulty[(int)difficulty];
            var gain = 0;
            for (var i = 0; i < GainBandFloor.Length; i++)
                if (score >= GainBandFloor[i]) gain = table[i];
            return gain;
        }

        public static int MaxGain(MinigameDifficulty difficulty) => GainForBandByDifficulty[(int)difficulty][GainBandFloor.Length - 1];

        // A finished game (never a cancelled one): the records, then the ability. Returns the gain.
        public int ApplyScore(CreatorSkill skill, int score) => ApplyScore(skill, score, MinigameDifficulty.Normal);

        public int ApplyScore(CreatorSkill skill, int score, MinigameDifficulty difficulty)
        {
            score = Math.Max(0, Math.Min(100, score));
            var i = (int)skill;
            var gain = GainFor(score, difficulty);
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
