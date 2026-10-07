using System.Text;

namespace HalkaSiteEditor.Core;

/// <summary>
/// `sitemap.xml` と `robots.txt` を、いまサイトにあるページから作ります。
/// 検索に載せないページ（`noindex`）と、よそへ飛ばすだけのページは入れません。
/// </summary>
public static class Sitemap
{
    /// <summary>CNAME に書いてあるドメイン。無ければ halkaclub.com。</summary>
    public static string Origin(string root)
    {
        var cname = Path.Combine(root, "CNAME");
        var host = File.Exists(cname) ? File.ReadAllText(cname).Trim() : "";
        if (host.Length == 0) host = "halkaclub.com";
        return "https://" + host;
    }

    /// <summary>検索に載せてよいページのURL（`/` から始まる形）。</summary>
    public static IReadOnlyList<string> Urls(string root) => SitePages.ForSite(root)
        .Select(page => page.Url)
        .Where(url => Indexable(root, url))
        .ToArray();

    private static bool Indexable(string root, string url)
    {
        var path = PreviewServer.ResolveFile(root, url);
        if (path == null || !File.Exists(path)) return false;

        var text = File.ReadAllText(path);
        if (text.Contains("noindex")) return false;
        if (text.Contains("http-equiv=\"refresh\"")) return false;
        return true;
    }

    public static string BuildSitemap(string root, string newline = "\n")
    {
        var origin = Origin(root);
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>").Append(newline);
        builder.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">").Append(newline);
        foreach (var url in Urls(root))
            builder.Append("  <url><loc>").Append(origin).Append(url).Append("</loc></url>").Append(newline);
        builder.Append("</urlset>").Append(newline);
        return builder.ToString();
    }

    public static string BuildRobots(string root, string newline = "\n")
    {
        var builder = new StringBuilder();
        builder.Append("User-agent: *").Append(newline);
        builder.Append("Allow: /").Append(newline);

        // 検索に載せないページは、たどらせないようにしておきます。
        foreach (var url in SitePages.ForSite(root).Select(page => page.Url)
                     .Where(url => url != "/" && !Indexable(root, url)))
            builder.Append("Disallow: ").Append(url).Append(newline);

        builder.Append(newline);
        builder.Append("Sitemap: ").Append(Origin(root)).Append("/sitemap.xml").Append(newline);
        return builder.ToString();
    }

    /// <summary>2つのファイルを書き出します。変わったファイルの相対パスを返します。</summary>
    public static IReadOnlyList<string> Write(string root)
    {
        var written = new List<string>();
        foreach (var (name, text) in new[]
                 {
                     ("sitemap.xml", BuildSitemap(root)),
                     ("robots.txt", BuildRobots(root)),
                 })
        {
            var path = Path.Combine(root, name);
            if (File.Exists(path) && File.ReadAllText(path) == text) continue;
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written.Add(name);
        }
        return written;
    }
}
