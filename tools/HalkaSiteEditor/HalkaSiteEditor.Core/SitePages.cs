namespace HalkaSiteEditor.Core;

/// <summary>プレビューに出すページ1つ。</summary>
public sealed record SitePage(string Label, string Url)
{
    public override string ToString() => Label;
}

public static class SitePages
{
    // 左が表示名、右がサイト内のURL。実際に存在するものだけを並べます。
    private static readonly (string Label, string Url)[] Candidates =
    {
        ("トップページ", "/"),
        ("依頼ページ", "/commission/"),
        ("依頼ページ（英語）", "/commission/en/"),
        ("作品一覧", "/works/"),
        ("はるかくらぶ", "/club/"),
        ("うたまぜ！", "/utamaze/"),
        ("ゲーム", "/game/"),
        ("HALKA WORLD", "/halkaworld/"),
    };

    public static IReadOnlyList<SitePage> ForSite(string root) => Candidates
        .Where(page => PreviewServer.ResolveFile(root, page.Url) != null)
        .Select(page => new SitePage(page.Label, page.Url))
        .Concat(GamePages(root))
        .ToArray();

    /// <summary>ゲーム1本ずつのページ。フォルダーがある分だけ並べます。</summary>
    private static IEnumerable<SitePage> GamePages(string root)
    {
        var folder = Path.Combine(root, "game");
        if (!Directory.Exists(folder)) return Array.Empty<SitePage>();

        return Directory.GetDirectories(folder)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .Where(slug => SitePaths.IsGamePage(SitePaths.GamePage(root, slug)))
            .Select(slug => new SitePage($"ゲーム：{slug}", $"/game/{slug}/"))
            .ToArray();
    }

    /// <summary>編集中のタブに合わせて、最初に開くページを選びます。</summary>
    public static SitePage? ForGroup(IReadOnlyList<SitePage> pages, string groupTitle)
    {
        var wanted = groupTitle switch
        {
            "依頼ページ" or "ページの文章" => "/commission/",
            "作品一覧" => "/works/",
            "はるかくらぶ" => "/club/",
            "飾りの文字" => "/",
            "うたまぜ！" => "/utamaze/",
            "ゲーム" => "/game/",
            _ => "/",
        };
        return pages.FirstOrDefault(page => page.Url == wanted) ?? pages.FirstOrDefault();
    }
}
