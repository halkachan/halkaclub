using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// ページ1枚の「顔」。検索結果とSNSに出る文章と、カード画像です。
/// </summary>
public sealed class MetaPage
{
    public string Label { get; }
    public string Url { get; }
    public string FileRelative { get; }
    public IReadOnlyList<EditField> Fields { get; }
    public IReadOnlyList<FieldRow> Rows { get; }
    /// <summary>SNSに出るカード画像。差し替えられないページなら null。</summary>
    public ImageSlot? Card { get; }

    public override string ToString() => Label;

    internal MetaPage(string label, string url, string fileRelative,
        IReadOnlyList<EditField> fields, IReadOnlyList<FieldRow> rows, ImageSlot? card)
    {
        Label = label;
        Url = url;
        FileRelative = fileRelative;
        Fields = fields;
        Rows = rows;
        Card = card;
    }
}

/// <summary>
/// 全ページの `&lt;title&gt;` と OGP（SNSにURLを貼ったときのカード）をまとめて扱います。
/// タグの並びや書き方には触れず、`content="..."` の中身だけを差し替えます。
/// </summary>
public sealed class PageMetaDocument
{
    private const string TitleTag = @"(<title>)([^<]*)(</title>)";
    private const string DescriptionTag = @"(<meta name=""description"" content="")([^""]*)("")";
    private const string OgTitle = @"(<meta property=""og:title"" content="")([^""]*)("")";
    private const string OgDescription = @"(<meta property=""og:description"" content="")([^""]*)("")";
    private const string OgImageAlt = @"(<meta property=""og:image:alt"" content="")([^""]*)("")";
    private static readonly Regex OgImage =
        new(@"<meta property=""og:image"" content=""([^""]*)""", RegexOptions.CultureInvariant);

    public IReadOnlyList<MetaPage> Pages { get; }

    private PageMetaDocument(IReadOnlyList<MetaPage> pages) => Pages = pages;

    /// <summary>OGPを持つページが1枚も無ければ null。<paramref name="open"/> はファイルを開いて登録する関数です。</summary>
    public static PageMetaDocument? Load(string root, Func<string, SiteFile> open)
    {
        var pages = new List<MetaPage>();

        foreach (var page in SitePages.ForSite(root))
        {
            var path = PreviewServer.ResolveFile(root, page.Url);
            // OGPを書いていないページ（ゲーム1本ずつのページなど）は出しません。
            if (path == null || !File.Exists(path) || !File.ReadAllText(path).Contains("og:title")) continue;

            var file = open(path);
            var relative = SitePaths.Relative(root, path);
            var fields = new List<EditField>();

            EditField? Field(string pattern, string id, string label)
            {
                if (new ValueSlot(pattern).Count(file.Text) == 0) return null;
                var field = new EditField(file, relative, new ValueSlot(pattern),
                    $"meta.{page.Url}.{id}", $"{page.Label}：{label}", FieldKind.HtmlText);
                fields.Add(field);
                return field;
            }

            var title = Field(TitleTag, "title", "ページ名");
            var description = Field(DescriptionTag, "description", "検索結果に出る説明");
            var ogTitle = Field(OgTitle, "ogTitle", "カードの見出し");
            var ogDescription = Field(OgDescription, "ogDescription", "カードの説明");
            var ogAlt = Field(OgImageAlt, "ogAlt", "カード画像の説明（読み上げ用）");
            if (title == null || ogTitle == null) continue;

            var rows = new List<FieldRow>
            {
                new("ページ名", new FieldCell("ブラウザのタブと検索結果に出ます", title)),
            };
            if (description != null)
                rows.Add(new FieldRow("検索結果の説明", new FieldCell("検索結果でタイトルの下に出ます", description)));
            rows.Add(new FieldRow("カードの見出し", new FieldCell("XやDiscordに貼ったときの太字", ogTitle)));
            if (ogDescription != null)
                rows.Add(new FieldRow("カードの説明", new FieldCell("その下に出る文章", ogDescription)));
            if (ogAlt != null)
                rows.Add(new FieldRow("画像の説明", new FieldCell("画像が出ないときの代わりの文字", ogAlt)));

            pages.Add(new MetaPage(page.Label, page.Url, relative, fields, rows,
                CardSlot(root, file.Text, page.Label)));
        }

        return pages.Count == 0 ? null : new PageMetaDocument(pages);
    }

    /// <summary>og:image のURLから、リポジトリの中の画像を探します。</summary>
    private static ImageSlot? CardSlot(string root, string text, string label)
    {
        var match = OgImage.Match(text);
        if (!match.Success) return null;

        var url = match.Groups[1].Value.Split('?')[0];
        var at = url.IndexOf("/assets/", StringComparison.Ordinal);
        if (at < 0) return null;

        var relative = url[(at + 1)..];
        var slot = new ImageSlot(root, relative, $"{label}のカード画像", cardSize: true);
        return File.Exists(slot.Path) ? slot : null;
    }

    public IEnumerable<EditField> AllFields => Pages.SelectMany(page => page.Fields);
    public IEnumerable<ImageSlot> Cards => Pages.Select(page => page.Card).OfType<ImageSlot>();
}
