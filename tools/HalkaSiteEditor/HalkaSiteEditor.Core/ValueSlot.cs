using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// ファイル中の「値が1つ入っている場所」。
/// パターンは3つのグループを持ち、2番目が値そのものです。読み書きは2番目の範囲だけに触れるので、
/// 前後の書式・インデント・改行は一切変わりません。
/// </summary>
public sealed class ValueSlot
{
    private readonly Regex pattern;
    private readonly int occurrence;

    public ValueSlot(string pattern, int occurrence = 0)
    {
        this.pattern = new Regex(pattern, RegexOptions.CultureInvariant);
        this.occurrence = occurrence;
    }

    public int Count(string text) => pattern.Matches(text).Count;

    public string Read(string text) => Find(text).Groups[2].Value;

    public string Write(string text, string value)
    {
        var slot = Find(text).Groups[2];
        return string.Concat(text.AsSpan(0, slot.Index), value, text.AsSpan(slot.Index + slot.Length));
    }

    /// <summary>この形の場所をすべて、ファイル内の出現順で返します。</summary>
    public static IReadOnlyList<string> ReadAll(string text, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return regex.Matches(text).Select(m => m.Groups[2].Value).ToArray();
    }

    private Match Find(string text)
    {
        var matches = pattern.Matches(text);
        if (matches.Count <= occurrence)
        {
            throw new InvalidDataException(
                $"編集する場所が見つかりませんでした（{occurrence + 1}番目 / {pattern}）。" +
                "HTMLの書き方が変わった可能性があります。");
        }
        return matches[occurrence];
    }
}
