using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 読み込み側に付いている `?v=番号` を1つ進めます。
///
/// GitHub Pages は `Cache-Control: max-age=600` を返すので、番号を変えずにファイルだけ
/// 差し替えると、見る人のブラウザがしばらく古いものを掴んだままになります。
/// 作品一覧のように**よく変わるファイル**は、中身を変えたら必ず番号を上げます。
/// </summary>
public static class CacheBuster
{
    /// <summary>
    /// `src="works-data.js?v=3"` の数字を1つ上げます。
    /// `?v=` がまだ無ければ `?v=1` を付けます。変えた場所の数を返します。
    ///
    /// 引用符の中にあれば場所は問いません（`src=` `href=` `content=` のほか、
    /// `script.js` の中の文字列でも上がります）。
    /// </summary>
    public static int Bump(SiteFile file, string resource)
    {
        var name = Regex.Escape(resource);
        var text = file.Text;
        var changed = 0;

        var versioned = new Regex($@"([""'][^""'<>]*{name}\?v=)(\d+)([""' &])");
        text = versioned.Replace(text, match =>
        {
            changed++;
            var next = int.TryParse(match.Groups[2].Value, out var current) ? current + 1 : 1;
            return match.Groups[1].Value + next + match.Groups[3].Value;
        });

        if (changed == 0)
        {
            var plain = new Regex($@"([""'][^""'<>]*{name})([""'])");
            text = plain.Replace(text, match =>
            {
                changed++;
                return match.Groups[1].Value + "?v=1" + match.Groups[2].Value;
            });
        }

        if (changed > 0) file.SetText(text);
        return changed;
    }

    /// <summary>いま付いている番号（無ければ 0）。</summary>
    public static int Current(string text, string resource)
    {
        var match = Regex.Match(text, $@"[""'][^""'<>]*{Regex.Escape(resource)}\?v=(\d+)[""']");
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }
}
