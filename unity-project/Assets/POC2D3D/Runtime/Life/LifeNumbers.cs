using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: counts with no game cap (abilities, subscribers, views, money). They are
    // System.Numerics.BigInteger in memory and decimal digit strings in the save, so neither a long
    // (9.2e18) nor a double (1e308) is ever their limit. Formulas that need a curve (logarithms,
    // powers) work on the base-10 logarithm, which stays a small double for any count, and turn the
    // result back into a whole number from its digits, so a huge count gives a huge, exact-sized
    // result, never Infinity, NaN, a negative value or an exception.
    public static class LifeNumbers
    {
        public const int MaxDigits = 4000; // a save field longer than this is damage, not play

        public static bool TryParse(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            if (string.IsNullOrEmpty(text) || text.Length > MaxDigits) return false;
            foreach (var c in text)
                if (c < '0' || c > '9') return false;
            if (text.Length > 1 && text[0] == '0') return false; // canonical digits only
            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        public static string ToSave(BigInteger value) =>
            (value.Sign < 0 ? BigInteger.Zero : value).ToString(CultureInfo.InvariantCulture);

        // 12,345,678 (commas every three digits; very long numbers are written in full).
        public static string Grouped(BigInteger value)
        {
            var digits = ToSave(value);
            if (digits.Length <= 3) return digits;
            var b = new StringBuilder(digits.Length + digits.Length / 3);
            var first = digits.Length % 3;
            if (first > 0) b.Append(digits, 0, first);
            for (var i = first; i < digits.Length; i += 3)
            {
                if (b.Length > 0) b.Append(',');
                b.Append(digits, i, 3);
            }
            return b.ToString();
        }

        // For a window line: up to 12 digits grouped; beyond that 1.23e15 style (the exact value
        // stays in the save and the numbers page).
        public static string Short(BigInteger value)
        {
            var digits = ToSave(value);
            if (digits.Length <= 12) return Grouped(value);
            return digits[0] + "." + digits.Substring(1, 2) + "e" + (digits.Length - 1).ToString(CultureInfo.InvariantCulture);
        }

        // log10(1 + value), finite for every value (BigInteger.Log handles any size).
        public static double Log10OnePlus(BigInteger value)
        {
            if (value.Sign <= 0) return 0d;
            var result = BigInteger.Log10(value + BigInteger.One);
            return double.IsNaN(result) || double.IsInfinity(result) || result < 0d ? 0d : result;
        }

        // 10^exponent as a whole number (exponent >= 0, any size): 15 significant digits from the
        // fractional part, then zeros. Negative or non-finite input gives 0 / 1 safely.
        public static BigInteger Pow10(double exponent)
        {
            if (double.IsNaN(exponent) || exponent < 0d) return BigInteger.Zero;
            if (double.IsInfinity(exponent) || exponent > 1e6) exponent = 1e6;
            if (exponent < 15d) return new BigInteger(Math.Floor(Math.Pow(10d, exponent)));
            var whole = Math.Floor(exponent);
            var mantissa = Math.Pow(10d, exponent - whole);                 // 1 <= m < 10
            var significant = new BigInteger(Math.Floor(mantissa * 1e14)); // 15 digits
            return significant * BigInteger.Pow(10, (int)whole - 14);
        }

        public static BigInteger Max(BigInteger a, BigInteger b) => a >= b ? a : b;
        public static BigInteger NonNegative(BigInteger value) => value.Sign < 0 ? BigInteger.Zero : value;
    }
}
