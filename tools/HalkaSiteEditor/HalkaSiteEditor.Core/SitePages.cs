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
        .ToArray();

    /// <summary>編集中のタブに合わせて、最初に開くページを選びます。</summary>
    public static SitePage? ForGroup(IReadOnlyList<SitePage> pages, string groupTitle)
    {
        var wanted = groupTitle switch
        {
            "依頼ページ" => "/commission/",
            _ => "/",
        };
        return pages.FirstOrDefault(page => page.Url == wanted) ?? pages.FirstOrDefault();
    }
}
