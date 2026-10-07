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
public sealed class CommissionTextBlock : INotifyPropertyChanged
{
    private string text;

    internal Match Source { get; private set; }
    internal BlockShape Shape { get; }
    internal IReadOnlyList<ItemLayout> Layouts { get; private set; }

    public string Label { get; }
    public string Hint { get; }
    public string Original { get; private set; }

    internal CommissionTextBlock(string label, string hint, string original, Match source,
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
public sealed class CommissionTextPage
{
    public string Title { get; }
    public string FileRelative { get; }
    public IReadOnlyList<CommissionTextBlock> Blocks { get; }

    internal SiteFile File { get; }

    /// <summary>いまのファイルの中身（組み立て直した結果と見比べるためのもの）。</summary>
    public string CurrentText => File.Text;

    internal CommissionTextPage(string title, SiteFile file, string fileRelative,
        IReadOnlyList<CommissionTextBlock> blocks)
    {
        Title = title;
        File = file;
        FileRelative = fileRelative;
        Blocks = blocks;
    }

    public override string ToString() => $"{Title}（{Blocks.Count}か所）";
}

/// <summary>
/// 依頼ページの文章の読み書き。
/// 料金や受付状況（EditField が受け持つ場所）には触れないので、同じファイルを両方で直せます。
/// 日本語版と英語版は段落の作りが少し違うため、組にはせずページごとに扱います。
/// </summary>
public sealed class CommissionTextDocument
{
    private static readonly Regex BlockPattern = new(
        @"<pre class=""template-text"" id=""request-template-text"">(?<raw>[\s\S]*?)</pre>" +
        @"|(?<ul><ul class=""(?:dot-list|caution-list|quote-list[^""]*)"">)(?<ulInner>[\s\S]*?)</ul>" +
        @"|(?<cp><p class=""(?:request-lead|note-lead|note-strong|plan-sub-note|template-intro|template-note|payment-en)"">)(?<cpInner>[\s\S]*?)</p>" +
        @"|(?<run>(?:[ \t]*<p>(?:(?!</p>)[\s\S])*</p>\r?\n)+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex PlanName = new(@"<span class=""plan-name"">([^<]*)</span>");
    private static readonly Regex BlockTitle = new(@"<h4 class=""plan-block-title"">([^<]*)</h4>");
    private static readonly Regex SectionTitle = new(@"<h2 id=""[^""]*"">([^<]*)</h2>");
    private static readonly Regex ListItem = new(@"\n([ \t]*)(<li>[\s\S]*?</li>)");
    private static readonly Regex RunItem = new(@"([ \t]*)(<p>[\s\S]*?</p>)\r?\n");

    public IReadOnlyList<CommissionTextPage> Pages { get; }

    private CommissionTextDocument(IReadOnlyList<CommissionTextPage> pages) => Pages = pages;

    public static CommissionTextDocument Load(params (string Title, SiteFile File, string Relative)[] sources) =>
        new(sources
            .Select(source => new CommissionTextPage(source.Title, source.File, source.Relative,
                Parse(source.File.Text)))
            .ToArray());

    private static IReadOnlyList<CommissionTextBlock> Parse(string text)
    {
        var planNames = Positions(PlanName, text);
        var blockTitles = Positions(BlockTitle, text);
        var sections = Positions(SectionTitle, text);

        var blocks = new List<CommissionTextBlock>();
        foreach (Match match in BlockPattern.Matches(text))
        {
            var shape = ShapeOf(match);
            var (value, hint, layouts) = Read(match, shape);
            blocks.Add(new CommissionTextBlock(
                Label(match.Index, planNames, blockTitles, sections, shape, RoleOf(match)),
                hint, value, match, shape, layouts));
        }
        return blocks;
    }

    private static List<(int Index, string Text)> Positions(Regex pattern, string text) =>
        pattern.Matches(text).Select(m => (m.Index, m.Groups[1].Value.Trim())).ToList();

    private static string Label(int index, List<(int Index, string Text)> planNames,
        List<(int Index, string Text)> blockTitles, List<(int Index, string Text)> sections,
        BlockShape shape, string? role)
    {
        static string? Nearest(List<(int Index, string Text)> items, int before) =>
            items.Where(item => item.Index < before).Select(item => item.Text).LastOrDefault();
        static int NearestIndex(List<(int Index, string Text)> items, int before) =>
            items.Where(item => item.Index < before).Select(item => item.Index).DefaultIfEmpty(-1).Last();

        var sectionIndex = NearestIndex(sections, index);
        var planIndex = NearestIndex(planNames, index);
        var titleIndex = NearestIndex(blockTitles, index);

        var plan = planIndex > sectionIndex ? Nearest(planNames, index) : null;
        var sub = titleIndex > Math.Max(planIndex, sectionIndex) ? Nearest(blockTitles, index) : null;

        var head = plan ?? Nearest(sections, index) ?? "ページ上部";
        return $"{head}：{role ?? sub ?? shape switch
        {
            BlockShape.Raw => "テンプレート本文",
            BlockShape.List => "箇条書き",
            _ => "説明",
        }}";
    }

    /// <summary>class のついた段落は、役割が決まっているので名前で出します。</summary>
    private static string? RoleOf(Match match)
    {
        if (!match.Groups["cp"].Success) return null;
        var name = Regex.Match(match.Groups["cp"].Value, @"class=""([^""]*)""").Groups[1].Value;
        return name switch
        {
            "request-lead" => "リード文",
            "note-lead" => "書き出し",
            "note-strong" => "強調した注意",
            "plan-sub-note" => "補足",
            "template-intro" => "テンプレートの案内",
            "template-note" => "テンプレートの注意",
            "payment-en" => "英語の補足",
            _ => null,
        };
    }

    private static BlockShape ShapeOf(Match match) =>
        match.Groups["raw"].Success ? BlockShape.Raw
        : match.Groups["ul"].Success ? BlockShape.List
        : match.Groups["cp"].Success ? BlockShape.ClassedParagraph
        : BlockShape.ParagraphRun;

    private static (string Value, string Hint, IReadOnlyList<ItemLayout> Layouts) Read(Match match, BlockShape shape)
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

    /// <summary>HTMLの中身を、画面で扱う文字へ。物理的な改行と字下げは取り、&lt;br /&gt; を改行にします。</summary>
    private static string Flatten(string inner) =>
        Regex.Replace(inner, @"\r?\n[ \t]*", "").Replace("<br />", "\n").Trim();

    /// <summary>1項目を、元の体裁に合わせて組み立てます。</summary>
    private static string Element(string tag, string text, ItemLayout layout)
    {
        var lines = text.Split('\n');
        if (!layout.Multiline) return $"<{tag}>{string.Join("<br />", lines)}</{tag}>";

        var builder = new StringBuilder($"<{tag}>");
        for (var i = 0; i < lines.Length; i++)
        {
            builder.Append('\n').Append(layout.InnerIndent).Append(lines[i]);
            if (i < lines.Length - 1) builder.Append("<br />");
        }
        return builder.Append('\n').Append(layout.CloseIndent).Append($"</{tag}>").ToString();
    }

    private static ItemLayout LayoutFor(IReadOnlyList<ItemLayout> layouts, int index, string fallbackIndent) =>
        index < layouts.Count ? layouts[index]
        : layouts.Count > 0 ? layouts[0] with { Multiline = false }
        : new ItemLayout(fallbackIndent, "", "", false);

    /// <summary>画面の文字を、ファイルに書く形へ組み立て直します。</summary>
    private static string Build(CommissionTextBlock block)
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
                    builder.Append('\n').Append(layout.Indent).Append(Element("li", items[i], layout));
                }
                var close = block.Layouts.Count > 0 ? block.Layouts[0].CloseIndent : "";
                return builder.Append('\n').Append(close).Append("</ul>").ToString();
            }

            case BlockShape.ClassedParagraph:
            {
                var layout = LayoutFor(block.Layouts, 0, "");
                var body = Element("p", block.Text, layout);
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
                    builder.Append(layout.Indent).Append(Element("p", items[i], layout)).Append('\n');
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
    public string Rebuild(CommissionTextPage page)
    {
        var index = 0;
        return BlockPattern.Replace(page.CurrentText, _ =>
        {
            var block = page.Blocks[index++];
            return block.Changed ? Build(block) : block.Source.Value;
        });
    }

    /// <summary>保存後、書き込んだファイルを読み直して各かたまりを結び直します。</summary>
    internal void MarkSaved()
    {
        foreach (var page in Pages)
        {
            var matches = BlockPattern.Matches(page.CurrentText);
            if (matches.Count != page.Blocks.Count)
            {
                throw new InvalidDataException(
                    $"{page.FileRelative} の文章の数が保存の前後で変わりました（{page.Blocks.Count} → {matches.Count}）。" +
                    "読み込み直してください。");
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var shape = ShapeOf(matches[i]);
                var (value, _, layouts) = Read(matches[i], shape);
                page.Blocks[i].Rebind(value, matches[i], layouts);
            }
        }
    }

    public void Revert()
    {
        foreach (var page in Pages)
        foreach (var block in page.Blocks)
            block.Revert();
    }
}
