using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

internal enum BlockShape
{
    /// <summary>&lt;ul&gt; の中の &lt;li&gt; を並べたもの。</summary>
    List,
    /// <summary>class のついた &lt;p&gt; 1つ。</summary>
    ClassedParagraph,
    /// <summary>class のない &lt;p&gt; の連なり。</summary>
    ParagraphRun,
    /// <summary>&lt;pre&gt; の中身。書いたとおりに入ります。</summary>
    Raw,
}

/// <summary>
/// ページごとの決まり。どこを文章として扱うか、見出しをどう拾うかを決めます。
/// </summary>
/// <param name="Blocks">
/// 文章のかたまりを拾う形。名前つきの組（raw / ul・ulInner / cp・cpInner / run）を持ちます。
/// </param>
/// <param name="Headings">見出しの拾い方。広いものから細かいものの順に3つまで。</param>
/// <param name="Role">class の名前から、画面に出す役割名へ。</param>
/// <param name="BreakTag">改行として書き戻す印（`&lt;br /&gt;` か `&lt;br&gt;`）。</param>
/// <param name="Editable">
/// 読み取った文字を編集させてよいか。リンクなどHTMLの印が残るものは外します
/// （外したかたまりは、組み立て直しのときも元のまま残ります）。
/// </param>
public sealed record TextProfile(
    Regex Blocks,
    IReadOnlyList<Regex> Headings,
    Func<string, string?> Role,
    string BreakTag,
    Func<string, bool> Editable,
    string FallbackHead);

/// <summary>
/// 1項目ぶんの元の体裁。
/// このページは、1行で書かれた段落と、複数行に分けて書かれた段落が混ざっているので、
/// 変えていない項目を元どおりに戻せるよう、項目ごとに覚えておきます。
/// </summary>
internal sealed record ItemLayout(string Indent, string InnerIndent, string CloseIndent, bool Multiline);

/// <summary>
/// 依頼ページの文章のかたまり1つ（箇条書き1つぶん、段落のひとつながり、テンプレート全体など）。
/// 画面では複数行の文字として扱い、**空行で区切ると別の項目**になります。
/// 項目の中の改行は、保存するときに &lt;br /&gt; へ戻します。
/// </summary>
public sealed class PageTextBlock : INotifyPropertyChanged
{
    private string text;

    internal Match Source { get; private set; }
    internal BlockShape Shape { get; }
    internal IReadOnlyList<ItemLayout> Layouts { get; private set; }

    public string Label { get; }
    public string Hint { get; }
    public string Original { get; private set; }

    internal PageTextBlock(string label, string hint, string original, Match source,
        BlockShape shape, IReadOnlyList<ItemLayout> layouts)
    {
        Label = label;
        Hint = hint;
        Original = original;
        text = original;
        Source = source;
        Shape = shape;
        Layouts = layouts;
    }

    public string Text
    {
        get => text;
        set
        {
            var next = (value ?? "").Replace("\r\n", "\n");
            if (string.Equals(text, next, StringComparison.Ordinal)) return;
            text = next;
            Raise(nameof(Text));
            Raise(nameof(Changed));
            Raise(nameof(Error));
            Raise(nameof(HasError));
        }
    }

    public bool Changed => !string.Equals(Text, Original, StringComparison.Ordinal);

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text)) return "空にはできません。";
            if (Shape == BlockShape.Raw) return null;
            if (Text.AsSpan().IndexOfAny('<', '>') >= 0) return "< と > は使えません（改行はそのまま改行で書けます）。";
            if (Text.Contains('&')) return "& は使えません。「と」などに言い換えてください。";
            return null;
        }
    }

    public bool HasError => Error != null;

    public void Revert() => Text = Original;

    /// <summary>保存したあと、新しいファイルの中身へ結び直します。</summary>
    internal void Rebind(string original, Match source, IReadOnlyList<ItemLayout> layouts)
    {
        Original = original;
        Source = source;
        Layouts = layouts;
        Raise(nameof(Original));
        Raise(nameof(Changed));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>依頼ページ1枚ぶんの文章。</summary>
public sealed class PageTextPage
{
    public string Title { get; }
    public string FileRelative { get; }
    /// <summary>プレビューで開くURL。</summary>
    public string Url { get; }
    public IReadOnlyList<PageTextBlock> Blocks { get; }

    internal SiteFile File { get; }

    /// <summary>いまのファイルの中身（組み立て直した結果と見比べるためのもの）。</summary>
    public string CurrentText => File.Text;

    internal PageTextPage(string title, SiteFile file, string fileRelative, string url,
        IReadOnlyList<PageTextBlock> blocks)
    {
        Title = title;
        File = file;
        FileRelative = fileRelative;
        Url = url;
        Blocks = blocks;
    }

    public override string ToString() => $"{Title}（{Blocks.Count}か所）";
}

/// <summary>
/// 依頼ページの文章の読み書き。
/// 料金や受付状況（EditField が受け持つ場所）には触れないので、同じファイルを両方で直せます。
/// 日本語版と英語版は段落の作りが少し違うため、組にはせずページごとに扱います。
/// </summary>
public sealed class PageTextDocument
{
    private static readonly Regex ListItem = new(@"\n([ \t]*)(<li>[\s\S]*?</li>)");
    private static readonly Regex RunItem = new(@"([ \t]*)(<p>[\s\S]*?</p>)\r?\n");

    /// <summary>依頼ページ（日本語版・英語版）。</summary>
    public static readonly TextProfile Commission = new(
        new Regex(
            @"<pre class=""template-text"" id=""request-template-text"">(?<raw>[\s\S]*?)</pre>" +
            @"|(?<ul><ul class=""(?:dot-list|caution-list|quote-list[^""]*)"">)(?<ulInner>[\s\S]*?)</ul>" +
            @"|(?<cp><p class=""(?:request-lead|note-lead|note-strong|plan-sub-note|template-intro|template-note|payment-en)"">)(?<cpInner>[\s\S]*?)</p>" +
            @"|(?<run>(?:[ \t]*<p>(?:(?!</p>)[\s\S])*</p>\r?\n)+)",
            RegexOptions.CultureInvariant),
        new[]
        {
            new Regex(@"<h2 id=""[^""]*"">([^<]*)</h2>"),
            new Regex(@"<span class=""plan-name"">([^<]*)</span>"),
            new Regex(@"<h4 class=""plan-block-title"">([^<]*)</h4>"),
        },
        name => name switch
        {
            "request-lead" => "リード文",
            "note-lead" => "書き出し",
            "note-strong" => "強調した注意",
            "plan-sub-note" => "補足",
            "template-intro" => "テンプレートの案内",
            "template-note" => "テンプレートの注意",
            "payment-en" => "英語の補足",
            _ => null,
        },
        "<br />",
        _ => true,
        "ページ上部");

    /// <summary>うたまぜ！の紹介ページ。</summary>
    public static readonly TextProfile Utamaze = new(
        new Regex(
            @"(?<ul><ul>)(?<ulInner>[\s\S]*?)</ul>" +
            @"|(?<cp><h2>|<h3>|<summary>|<p class=""(?:hero-lead|hero-body|technical|plan-terms|note|kicker|plan-kicker)"">)" +
            @"(?<cpInner>[\s\S]*?)</(?:h2|h3|summary|p)>" +
            @"|(?<run>(?:[ \t]*<p>(?:(?!</p>)[\s\S])*</p>\r?\n)+)",
            RegexOptions.CultureInvariant),
        new[]
        {
            new Regex(@"<p class=""kicker"">([^<]*)</p>"),
            new Regex(@"<h3>([^<]*)</h3>"),
            new Regex(@"<summary>([\s\S]*?)</summary>"),
        },
        name => name switch
        {
            "hero-lead" => "ひとこと",
            "hero-body" => "説明",
            "technical" => "英語の呼び名",
            "plan-terms" => "ライセンスの条件",
            "note" => "注意書き",
            "kicker" or "plan-kicker" => "英語の見出し",
            "h2" => "見出し",
            "h3" => "小見出し",
            _ => null,
        },
        "<br>",
        // リンクなどHTMLの印が残るものは、ここでは触りません。
        value => !value.Contains('<') && !value.Contains('&'),
        "ページ上部");

    private readonly TextProfile profile;

    public IReadOnlyList<PageTextPage> Pages { get; }

    private PageTextDocument(TextProfile profile, IReadOnlyList<PageTextPage> pages)
    {
        this.profile = profile;
        Pages = pages;
    }

    public static PageTextDocument Load(TextProfile profile,
        params (string Title, SiteFile File, string Relative, string Url)[] sources) =>
        new(profile, sources
            .Select(source => new PageTextPage(source.Title, source.File, source.Relative, source.Url,
                Parse(profile, source.File.Text)))
            .ToArray());

    private static IReadOnlyList<PageTextBlock> Parse(TextProfile profile, string text)
    {
        var levels = profile.Headings.Select(pattern => Positions(pattern, text)).ToArray();

        var blocks = new List<PageTextBlock>();
        foreach (Match match in profile.Blocks.Matches(text))
        {
            var shape = ShapeOf(match);
            var (value, hint, layouts) = Read(profile, match, shape);
            // HTMLの印が残るもの（リンクなど）は、画面に出さずそのままにします。
            if (!profile.Editable(value)) continue;
            blocks.Add(new PageTextBlock(
                Label(profile, match.Index, levels, shape, RoleOf(profile, match)),
                hint, value, match, shape, layouts));
        }
        return blocks;
    }

    private static List<(int Index, string Text)> Positions(Regex pattern, string text) =>
        pattern.Matches(text).Select(m => (m.Index, Plain(m.Groups[1].Value))).ToList();

    /// <summary>見出しの中の印（&lt;br&gt; など）を取って、1行にします。</summary>
    private static string Plain(string value) =>
        Regex.Replace(Regex.Replace(value, @"<[^>]*>", " "), @"\s+", " ").Trim();

    private static string Label(TextProfile profile, int index,
        IReadOnlyList<List<(int Index, string Text)>> levels, BlockShape shape, string? role)
    {
        // 同じ場所から始まる見出し（FAQの質問など）も、自分の見出しとして使います。
        static string? Nearest(List<(int Index, string Text)> items, int before) =>
            items.Where(item => item.Index <= before).Select(item => item.Text).LastOrDefault();
        static int NearestIndex(List<(int Index, string Text)> items, int before) =>
            items.Where(item => item.Index <= before).Select(item => item.Index).DefaultIfEmpty(-1).Last();

        List<(int Index, string Text)> Level(int at) =>
            at < levels.Count ? levels[at] : new List<(int, string)>();

        var broadIndex = NearestIndex(Level(0), index);
        var midIndex = NearestIndex(Level(1), index);
        var subIndex = NearestIndex(Level(2), index);

        var mid = midIndex > broadIndex ? Nearest(Level(1), index) : null;
        var sub = subIndex > Math.Max(midIndex, broadIndex) ? Nearest(Level(2), index) : null;

        var head = mid ?? Nearest(Level(0), index) ?? profile.FallbackHead;
        return $"{head}：{role ?? sub ?? shape switch
        {
            BlockShape.Raw => "テンプレート本文",
            BlockShape.List => "箇条書き",
            _ => "説明",
        }}";
    }

    /// <summary>class のついた段落は、役割が決まっているので名前で出します。</summary>
    private static string? RoleOf(TextProfile profile, Match match)
    {
        if (!match.Groups["cp"].Success) return null;
        var open = match.Groups["cp"].Value;
        var name = Regex.Match(open, @"class=""([^""]*)""").Groups[1].Value;
        if (name.Length == 0) name = Regex.Match(open, @"^<([a-z0-9]+)").Groups[1].Value;
        return profile.Role(name);
    }

    private static BlockShape ShapeOf(Match match) =>
        match.Groups["raw"].Success ? BlockShape.Raw
        : match.Groups["ul"].Success ? BlockShape.List
        : match.Groups["cp"].Success ? BlockShape.ClassedParagraph
        : BlockShape.ParagraphRun;

    private static (string Value, string Hint, IReadOnlyList<ItemLayout> Layouts) Read(
        TextProfile profile, Match match, BlockShape shape)
    {
        switch (shape)
        {
            case BlockShape.Raw:
                return (match.Groups["raw"].Value, "書いたとおりに入ります。", Array.Empty<ItemLayout>());

            case BlockShape.List:
            {
                var inner = match.Groups["ulInner"].Value;
                var items = ListItem.Matches(inner);
                var layouts = items.Select(item => LayoutOf(item.Groups[1].Value, item.Groups[2].Value)).ToArray();
                var closeIndent = TrailingIndent(inner);
                layouts = layouts.Select(layout => layout with { CloseIndent = closeIndent }).ToArray();
                return (string.Join("\n\n", items.Select(item => Flatten(Inner(item.Groups[2].Value, "li")))),
                    "空行で区切ると、箇条書きの別の項目になります。", layouts);
            }

            case BlockShape.ClassedParagraph:
            {
                var inner = match.Groups["cpInner"].Value;
                var layout = new ItemLayout("", FirstInnerIndent(inner), TrailingIndent(inner), inner.Contains('\n'));
                return (Flatten(inner), "改行すると、表示でも改行されます。", new[] { layout });
            }

            default:
            {
                var items = RunItem.Matches(match.Groups["run"].Value);
                var layouts = items.Select(item => LayoutOf(item.Groups[1].Value, item.Groups[2].Value)).ToArray();
                return (string.Join("\n\n", items.Select(item => Flatten(Inner(item.Groups[2].Value, "p")))),
                    "空行で区切ると、別の段落になります。", layouts);
            }
        }
    }

    private static ItemLayout LayoutOf(string indent, string element)
    {
        var inner = StripTags(element);
        return new ItemLayout(indent, FirstInnerIndent(inner), TrailingIndent(inner), inner.Contains('\n'));
    }

    private static string Inner(string element, string tag) => StripTags(element);

    private static string StripTags(string element)
    {
        var open = element.IndexOf('>');
        var close = element.LastIndexOf("</", StringComparison.Ordinal);
        return open < 0 || close < 0 || close < open ? element : element[(open + 1)..close];
    }

    private static string FirstInnerIndent(string inner)
    {
        var match = Regex.Match(inner, @"\n([ \t]*)\S");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string TrailingIndent(string inner)
    {
        var match = Regex.Match(inner, @"\n([ \t]*)$");
        return match.Success ? match.Groups[1].Value : "";
    }

    /// <summary>HTMLの中身を、画面で扱う文字へ。物理的な改行と字下げは取り、改行の印を改行にします。</summary>
    private static string Flatten(string inner) =>
        Regex.Replace(Regex.Replace(inner, @"\r?\n[ \t]*", ""), @"<br ?/?>", "\n").Trim();

    /// <summary>1項目を、元の体裁に合わせて組み立てます。</summary>
    private static string Element(TextProfile profile, string tag, string text, ItemLayout layout)
    {
        var lines = text.Split('\n');
        if (!layout.Multiline) return $"<{tag}>{string.Join(profile.BreakTag, lines)}</{tag}>";

        var builder = new StringBuilder($"<{tag}>");
        for (var i = 0; i < lines.Length; i++)
        {
            builder.Append('\n').Append(layout.InnerIndent).Append(lines[i]);
            if (i < lines.Length - 1) builder.Append(profile.BreakTag);
        }
        return builder.Append('\n').Append(layout.CloseIndent).Append($"</{tag}>").ToString();
    }

    private static ItemLayout LayoutFor(IReadOnlyList<ItemLayout> layouts, int index, string fallbackIndent) =>
        index < layouts.Count ? layouts[index]
        : layouts.Count > 0 ? layouts[0] with { Multiline = false }
        : new ItemLayout(fallbackIndent, "", "", false);

    /// <summary>画面の文字を、ファイルに書く形へ組み立て直します。</summary>
    private static string Build(TextProfile profile, PageTextBlock block)
    {
        var match = block.Source;
        var items = block.Shape is BlockShape.ClassedParagraph or BlockShape.Raw
            ? new[] { block.Text }
            : Regex.Split(block.Text.Trim(), @"\n[ \t]*\n+");

        switch (block.Shape)
        {
            case BlockShape.Raw:
                return match.Value.Replace(match.Groups["raw"].Value, block.Text);

            case BlockShape.List:
            {
                var builder = new StringBuilder(match.Groups["ul"].Value);
                for (var i = 0; i < items.Length; i++)
                {
                    var layout = LayoutFor(block.Layouts, i, "  ");
                    builder.Append('\n').Append(layout.Indent).Append(Element(profile, "li", items[i], layout));
                }
                var close = block.Layouts.Count > 0 ? block.Layouts[0].CloseIndent : "";
                return builder.Append('\n').Append(close).Append("</ul>").ToString();
            }

            case BlockShape.ClassedParagraph:
            {
                var layout = LayoutFor(block.Layouts, 0, "");
                // 開きタグから札の名前を取ります（<p class="..."> でも <h2> でも同じ形で戻せます）。
                var tag = Regex.Match(match.Groups["cp"].Value, @"^<([a-z0-9]+)").Groups[1].Value;
                var body = Element(profile, tag.Length > 0 ? tag : "p", block.Text, layout);
                // 開きタグは元のまま（class つき）を使い、中身と閉じタグだけ差し替えます。
                var afterOpen = body.IndexOf('>') + 1;
                return match.Groups["cp"].Value + body.Substring(afterOpen);
            }

            default:
            {
                var builder = new StringBuilder();
                for (var i = 0; i < items.Length; i++)
                {
                    var layout = LayoutFor(block.Layouts, i, "");
                    builder.Append(layout.Indent).Append(Element(profile, "p", items[i], layout)).Append('\n');
                }
                return builder.ToString();
            }
        }
    }

    public bool HasChanges => Pages.Any(page => page.Blocks.Any(block => block.Changed));
    public bool HasError => Pages.Any(page => page.Blocks.Any(block => block.HasError));

    public IReadOnlyList<ChangeRow> Changes() => Pages
        .SelectMany(page => page.Blocks
            .Where(block => block.Changed)
            .Select(block => new ChangeRow(
                $"{page.Title}／{block.Label}",
                Summary(block.Original), Summary(block.Text), page.FileRelative)))
        .ToArray();

    private static string Summary(string text)
    {
        var single = text.Replace("\n", " / ");
        return single.Length <= 40 ? single : single[..40] + "…";
    }

    /// <summary>
    /// いまの中身でファイルの文字を組み立て直します。
    /// EditField が先に書き換えた場所（料金・受付状況）には触れないので、そのまま残ります。
    /// </summary>
    internal void Apply()
    {
        foreach (var page in Pages)
        {
            if (!page.Blocks.Any(block => block.Changed)) continue;
            page.File.SetText(Rebuild(page));
        }
    }

    /// <summary>いまの中身で組み立て直した、ファイル全体の文字。</summary>
    public string Rebuild(PageTextPage page)
    {
        // 画面に出していないかたまり（リンク入りなど）は飛ばすので、
        // 番号で数えずに、見つかった場所そのもので対応づけます。
        var byIndex = page.Blocks.ToDictionary(block => block.Source.Index);
        return profile.Blocks.Replace(page.CurrentText, match =>
            byIndex.TryGetValue(match.Index, out var block) && block.Changed
                ? Build(profile, block)
                : match.Value);
    }

    /// <summary>保存後、書き込んだファイルを読み直して各かたまりを結び直します。</summary>
    internal void MarkSaved()
    {
        foreach (var page in Pages)
        {
            var found = new List<(Match Match, BlockShape Shape, string Value, IReadOnlyList<ItemLayout> Layouts)>();
            foreach (Match match in profile.Blocks.Matches(page.CurrentText))
            {
                var shape = ShapeOf(match);
                var (value, _, layouts) = Read(profile, match, shape);
                if (!profile.Editable(value)) continue;
                found.Add((match, shape, value, layouts));
            }

            if (found.Count != page.Blocks.Count)
            {
                throw new InvalidDataException(
                    $"{page.FileRelative} の文章の数が保存の前後で変わりました（{page.Blocks.Count} → {found.Count}）。" +
                    "読み込み直してください。");
            }

            for (var i = 0; i < found.Count; i++)
                page.Blocks[i].Rebind(found[i].Value, found[i].Match, found[i].Layouts);
        }
    }

    public void Revert()
    {
        foreach (var page in Pages)
        foreach (var block in page.Blocks)
            block.Revert();
    }
}
