using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

public enum CheckLevel
{
    /// <summary>直したほうがよいもの（リンク切れなど）。</summary>
    Problem,
    /// <summary>知らせておきたいもの（番号の付け忘れなど）。</summary>
    Notice,
}

/// <summary>点検で見つかったこと1つ。</summary>
public sealed record CheckIssue(CheckLevel Level, string What, string Detail, string File)
{
    public string LevelText => Level == CheckLevel.Problem ? "問題" : "気になる";
}

/// <summary>
/// 公開する前の点検。
/// リンク切れ・画像の欠け・`?v=` の付け忘れなど、**出してから気づくと直しにくいもの**を見ます。
/// サイトのファイルは読むだけで、書き換えません。
/// </summary>
public static class SiteCheck
{
    private static readonly Regex Target =
        new(@"(?:href|src)=""([^""]*)""", RegexOptions.CultureInvariant);
    private static readonly Regex OgImage =
        new(@"<meta property=""og:image"" content=""([^""]*)""", RegexOptions.CultureInvariant);
    private static readonly Regex Comment = new(@"<!--[\s\S]*?-->", RegexOptions.CultureInvariant);
    /// <summary>引用符でくくられた、道のように見えるもの（JSの中の文字列も拾います）。</summary>
    private static readonly Regex Quoted = new(@"[""']([^""'<>\r\n]+)[""']", RegexOptions.CultureInvariant);

    /// <summary>よく中身が変わるので、`?v=` が付いていてほしいもの。</summary>
    private static readonly string[] Versioned =
    {
        "style.css", "script.js", "works/works-data.js", "assets/profile/halgif1.gif",
    };

    public static IReadOnlyList<CheckIssue> Run(SiteSession session)
    {
        var issues = new List<CheckIssue>();
        var root = session.Root;

        foreach (var page in SitePaths.AllPages(root))
        {
            var relative = SitePaths.Relative(root, page);
            string text;
            try { text = File.ReadAllText(page); }
            catch (Exception) { continue; }

            // HTMLのコメントは、まだ書いていない場所の覚え書きなので見ません。
            CheckTargets(root, page, relative, Comment.Replace(text, ""), issues);
            CheckOgImage(root, relative, text, issues);
        }

        CheckVersions(root, issues);
        CheckAnalytics(root, issues);
        CheckWorks(session, issues);
        CheckSitemap(root, issues);

        if (session.Utamaze?.IsMixed == true)
        {
            issues.Add(new CheckIssue(CheckLevel.Notice, "うたまぜ！のリリースが半端です",
                session.Utamaze.Summary, "utamaze/index.html"));
        }

        return issues
            .OrderBy(issue => issue.Level)
            .ThenBy(issue => issue.File, StringComparer.Ordinal)
            .ToArray();
    }

    // --- リンクと画像の行き先 -----------------------------------------------

    private static void CheckTargets(string root, string page, string relative, string text,
        List<CheckIssue> issues)
    {
        foreach (Match match in Target.Matches(text))
        {
            var raw = match.Groups[1].Value;
            if (Skip(raw)) continue;
            if (Resolve(root, page, raw) != null) continue;

            var what = match.Value.StartsWith("src", StringComparison.Ordinal) ? "画像が見つかりません" : "リンク切れ";
            issues.Add(new CheckIssue(CheckLevel.Problem, what, raw, relative));
        }
    }

    private static bool Skip(string raw) =>
        raw.Length == 0 || raw.StartsWith('#') || raw.StartsWith("data:") ||
        raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("//", StringComparison.Ordinal);

    /// <summary>サイトの中の行き先を、実際のファイルへ。見つからなければ null。</summary>
    public static string? Resolve(string root, string page, string raw)
    {
        var path = raw.Split('#')[0].Split('?')[0];
        if (path.Length == 0) return page;

        string full;
        try
        {
            full = path.StartsWith('/')
                ? Path.GetFullPath(Path.Combine(root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)))
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(page)!,
                    path.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception)
        {
            return null;
        }

        // サイトのフォルダーの外は、点検の対象にしません。
        if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) return null;

        if (File.Exists(full)) return full;
        var index = Path.Combine(full, "index.html");
        return Directory.Exists(full) && File.Exists(index) ? index : null;
    }

    // --- OGP画像 -------------------------------------------------------------

    private static void CheckOgImage(string root, string relative, string text, List<CheckIssue> issues)
    {
        var match = OgImage.Match(text);
        if (!match.Success) return;

        var url = match.Groups[1].Value.Split('?')[0];
        var at = url.IndexOf("/assets/", StringComparison.Ordinal);
        if (at < 0)
        {
            issues.Add(new CheckIssue(CheckLevel.Notice, "カード画像の置き場所が違います", url, relative));
            return;
        }

        var path = Path.Combine(root, url[(at + 1)..].Replace('/', Path.DirectorySeparatorChar));
        var info = ImageInfo.Read(path);
        if (info == null)
        {
            issues.Add(new CheckIssue(CheckLevel.Problem, "カード画像が見つかりません", url, relative));
        }
        else if (info.Width != 1200 || info.Height != 630)
        {
            issues.Add(new CheckIssue(CheckLevel.Problem, "カード画像の大きさが違います",
                $"{info.Width}×{info.Height}（1200×630 が必要です）", relative));
        }
    }

    // --- ?v= の付け忘れと、食い違い -----------------------------------------

    private static void CheckVersions(string root, List<CheckIssue> issues)
    {
        var ogp = Directory.Exists(Path.Combine(root, "assets", "ogp"))
            ? Directory.GetFiles(Path.Combine(root, "assets", "ogp"), "*.png")
                .Select(path => "assets/ogp/" + Path.GetFileName(path))
            : Array.Empty<string>();

        // 見張るものを、実際のファイルの場所で覚えます
        // （`./style.css` のように、名前は同じでも別のファイルを指すことがあるため）。
        var watched = Versioned.Concat(ogp)
            .Select(resource => (Resource: resource,
                Full: Path.GetFullPath(Path.Combine(root, resource.Replace('/', Path.DirectorySeparatorChar)))))
            .Where(item => File.Exists(item.Full))
            .ToArray();

        var found = watched.ToDictionary(item => item.Resource, _ => new List<(string Page, string? Version)>());

        foreach (var page in SitePaths.AllPages(root).Concat(Scripts(root)))
        {
            var relative = SitePaths.Relative(root, page);
            string text;
            try { text = File.ReadAllText(page); }
            catch (Exception) { continue; }

            foreach (Match match in Quoted.Matches(text))
            {
                var raw = match.Groups[1].Value;
                if (Skip(raw) || raw.Contains(' ')) continue;

                var resolved = Resolve(root, page, raw);
                if (resolved == null) continue;

                var hit = watched.FirstOrDefault(item =>
                    string.Equals(item.Full, resolved, StringComparison.OrdinalIgnoreCase));
                if (hit.Resource == null) continue;

                var version = Regex.Match(raw, @"\?v=(\d+)");
                found[hit.Resource].Add((relative, version.Success ? version.Groups[1].Value : null));
            }
        }

        foreach (var (resource, numbers) in found)
        {
            foreach (var page in numbers.Where(item => item.Version == null)
                         .Select(item => item.Page).Distinct(StringComparer.Ordinal))
            {
                issues.Add(new CheckIssue(CheckLevel.Notice, "?v= が付いていません",
                    $"{resource} — 中身を変えても、見る人に届くまで時間がかかります", page));
            }

            var distinct = numbers.Where(item => item.Version != null)
                .Select(item => item.Version!).Distinct(StringComparer.Ordinal).ToArray();
            if (distinct.Length > 1)
            {
                issues.Add(new CheckIssue(CheckLevel.Notice, "?v= の番号が食い違っています",
                    $"{resource} — {string.Join(" / ", distinct.Select(number => "?v=" + number))}",
                    string.Join("、", numbers.Select(item => item.Page).Distinct(StringComparer.Ordinal))));
            }
        }
    }

    // --- アクセス解析 --------------------------------------------------------

    /// <summary>訪問を数えるための印（Cloudflare Web Analytics）。</summary>
    private const string Beacon = "static.cloudflareinsights.com";

    /// <summary>
    /// 入れているページと入れていないページが混じっていないかを見ます。
    /// **1枚も入れていないサイトでは何も言いません**（使っていない人に勧めないため）。
    /// </summary>
    private static void CheckAnalytics(string root, List<CheckIssue> issues)
    {
        var pages = SitePages.ForSite(root)
            .Select(page => PreviewServer.ResolveFile(root, page.Url))
            .OfType<string>()
            .Select(path => (Path: path, Text: Text(path)))
            .ToArray();

        if (!pages.Any(page => page.Text.Contains(Beacon))) return;

        foreach (var page in pages.Where(page => !page.Text.Contains(Beacon)))
        {
            issues.Add(new CheckIssue(CheckLevel.Notice, "アクセス解析のタグが入っていません",
                "このページへの訪問だけ、数に入りません", SitePaths.Relative(root, page.Path)));
        }
    }

    private static string Text(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception) { return ""; }
    }

    private static IEnumerable<string> Scripts(string root) =>
        new[] { SitePaths.ScriptJs(root) }.Where(File.Exists);

    // --- 作品と sitemap ------------------------------------------------------

    private static void CheckWorks(SiteSession session, List<CheckIssue> issues)
    {
        foreach (var category in session.Works.Categories)
        foreach (var work in category.Works.Where(work => work.HasError))
        {
            issues.Add(new CheckIssue(CheckLevel.Problem, $"{category.Name}：作品の入力に誤りがあります",
                $"{work.Title} — {work.Error}", session.Works.FileRelative));
        }
    }

    private static void CheckSitemap(string root, List<CheckIssue> issues)
    {
        var path = Path.Combine(root, "sitemap.xml");
        if (!File.Exists(path))
        {
            issues.Add(new CheckIssue(CheckLevel.Notice, "sitemap.xml がありません",
                "「ページの顔」タブの「sitemap を作り直す」で作れます", "sitemap.xml"));
            return;
        }

        // 改行コードの違いは、中身の違いではありません。
        static string Flat(string text) => text.Replace("\r\n", "\n");
        if (Flat(File.ReadAllText(path)) != Flat(Sitemap.BuildSitemap(root)))
        {
            issues.Add(new CheckIssue(CheckLevel.Notice, "sitemap.xml が今のページと合っていません",
                "「ページの顔」タブの「sitemap を作り直す」で合わせられます", "sitemap.xml"));
        }
    }
}
