namespace HalkaSiteEditor.Core;

/// <summary>プレビューに出すページ1つ。</summary>
public sealed record SitePage(string Label, string Url)
{
    public override string ToString() => Label;
}

/// <summary>
/// サイトにあるページを数えます。
/// **一覧は持ちません。** フォルダを見て見つけるので、あとからページが増えても、
/// ここに書き足さなくてもプレビュー・「ページの顔」・sitemap に出てきます。
/// </summary>
public static class SitePages
{
    /// <summary>名前と並び順が決まっているページ。ここに無いページも、見つけたら並べます。</summary>
    private static readonly (string Label, string Url)[] Known =
    {
        ("トップページ", "/"),
        ("依頼ページ", "/commission/"),
        ("依頼ページ（英語）", "/commission/en/"),
        ("作品一覧", "/works/"),
        ("はるかくらぶ", "/club/"),
        ("うたまぜ！", "/utamaze/"),
        ("うたまぜ！：特定商取引法", "/utamaze/legal/"),
        ("うたまぜ！：使用許諾契約", "/utamaze/eula/"),
        ("うたまぜ！：プライバシー", "/utamaze/privacy/"),
        ("ゲーム", "/game/"),
        ("HALKA WORLD", "/halkaworld/"),
    };

    public static IReadOnlyList<SitePage> ForSite(string root)
    {
        var labels = Known.ToDictionary(page => page.Url, page => page.Label, StringComparer.Ordinal);
        var order = Known.Select((page, at) => (page.Url, At: at))
            .ToDictionary(page => page.Url, page => page.At, StringComparer.Ordinal);

        return Discover(root)
            .Select(url => new SitePage(labels.TryGetValue(url, out var label) ? label : Name(url), url))
            .OrderBy(page => order.TryGetValue(page.Url, out var at) ? at : int.MaxValue)
            .ThenBy(page => page.Url, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>サイトの中のページを見つけます。</summary>
    private static IEnumerable<string> Discover(string root)
    {
        foreach (var page in SitePaths.AllPages(root))
        {
            // よそへ飛ばすだけのページと、Unity などが書き出したプレイヤーは、サイトのページとして数えません。
            if (IsRedirect(page) || IsPlayer(page)) continue;
            yield return UrlOf(root, page);
        }
    }

    private static bool IsRedirect(string page)
    {
        try { return File.ReadAllText(page).Contains("http-equiv=\"refresh\""); }
        catch (Exception) { return true; }
    }

    /// <summary>同じフォルダに `Build` があれば、書き出されたプレイヤーです。</summary>
    private static bool IsPlayer(string page)
    {
        var folder = Path.GetDirectoryName(page);
        return folder != null && Directory.Exists(Path.Combine(folder, "Build"));
    }

    /// <summary>ファイルの場所を、サイトの中のURLへ。</summary>
    private static string UrlOf(string root, string page)
    {
        var relative = SitePaths.Relative(root, page);
        if (string.Equals(relative, "index.html", StringComparison.OrdinalIgnoreCase)) return "/";
        return relative.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase)
            ? "/" + relative[..^"index.html".Length]
            : "/" + relative;
    }

    /// <summary>名前が決まっていないページの、画面に出す名前。</summary>
    private static string Name(string url)
    {
        var parts = url.Trim('/').Split('/');
        var last = parts.LastOrDefault() ?? "";
        return parts.Length > 1 && parts[0] == "game" ? $"ゲーム：{last}" : last;
    }

    /// <summary>編集中のタブに合わせて、最初に開くページを選びます。</summary>
    public static SitePage? ForGroup(IReadOnlyList<SitePage> pages, string groupTitle)
    {
        var wanted = groupTitle switch
        {
            "依頼ページ" or "ページの文章" => "/commission/",
            "作品一覧" => "/works/",
            "はるかくらぶ" => "/club/",
            "見出しと飾り" => "/",
            "うたまぜ！" => "/utamaze/",
            "ゲーム" => "/game/",
            _ => "/",
        };
        return pages.FirstOrDefault(page => page.Url == wanted) ?? pages.FirstOrDefault();
    }
}
