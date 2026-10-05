using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>style.css に書ける色かどうか。</summary>
public static class CssColor
{
    private static readonly Regex Hex = new(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
    private static readonly Regex Rgb = new(
        @"^rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(?:,\s*(?:0|1|0?\.\d+)\s*)?\)$");

    public static bool IsValid(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        return Hex.IsMatch(text) || Rgb.IsMatch(text);
    }

    /// <summary>
    /// 画面の色見本用に #rrggbb を返します。
    /// rgba() は、サイトの紙の白に重ねたときの見え方へ変換します（薄い線もそれらしく見えます）。
    /// 読めない値は null。
    /// </summary>
    public static string? ToSwatchHex(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        if (Hex.IsMatch(text))
        {
            var body = text[1..];
            if (body.Length is 3 or 4) body = string.Concat(body[..3].Select(c => new string(c, 2)));
            return "#" + body[..6];
        }

        if (!Rgb.IsMatch(text)) return null;

        var parts = text[(text.IndexOf('(') + 1)..text.IndexOf(')')]
            .Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 3) return null;
        if (!byte.TryParse(parts[0], out var r) ||
            !byte.TryParse(parts[1], out var g) ||
            !byte.TryParse(parts[2], out var b)) return null;

        var alpha = 1d;
        if (parts.Length >= 4 &&
            !double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out alpha)) return null;

        // サイトの --paper（紙の白）に重ねた見え方。
        static byte Over(byte channel, byte paper, double a) =>
            (byte)Math.Round(paper + (channel - paper) * a);
        return $"#{Over(r, 0xFF, alpha):x2}{Over(g, 0xFE, alpha):x2}{Over(b, 0xF6, alpha):x2}";
    }
}

/// <summary>
/// YouTube の URL から動画IDを取り出します。
/// 判定はサイト側の youtube-thumbnail.js の extractYouTubeId と同じにしてあります。
/// ここで通らないURLは、サイトに書いてもサムネイルが出ません。
/// </summary>
public static class YouTubeUrl
{
    public static string? ExtractId(string? url)
    {
        var text = url?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed)) return null;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return null;

        var host = parsed.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? parsed.Host[4..]
            : parsed.Host;
        var segments = parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
            return segments.Length > 0 ? segments[0] : null;

        if (host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            if (parsed.AbsolutePath.StartsWith("/shorts/", StringComparison.Ordinal))
                return segments.Length > 1 ? segments[1] : null;
            return QueryValue(parsed.Query, "v");
        }

        return null;
    }

    private static string? QueryValue(string query, string key)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0) continue;
            if (!Uri.UnescapeDataString(part[..separator]).Equals(key, StringComparison.Ordinal)) continue;
            var value = Uri.UnescapeDataString(part[(separator + 1)..]);
            return value.Length == 0 ? null : value;
        }
        return null;
    }
}

/// <summary>金額の表記を日本語側から英語側へ写します。</summary>
public static class PriceText
{
    private static readonly Regex Number = new(@"\d[\d,]*");

    /// <summary>「70,000円～」→「from 70,000 JPY」。数字が無ければ null（写せない）。</summary>
    public static string? ToEnglish(string? japanese)
    {
        var match = Number.Match(japanese ?? string.Empty);
        return match.Success ? $"from {match.Value} JPY" : null;
    }
}

/// <summary>HTMLの文字列として、そのまま埋めても壊れない値かどうか。</summary>
public static class HtmlText
{
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "空にはできません。";
        if (value.AsSpan().IndexOfAny('<', '>') >= 0) return "< と > は使えません。";
        if (value.Contains('&')) return "& は使えません。「と」などに言い換えてください。";
        return null;
    }
}
