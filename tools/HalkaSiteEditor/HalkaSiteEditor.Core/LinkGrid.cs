using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>トップページのリンク集に並ぶカード1枚。</summary>
public sealed class LinkCard : INotifyPropertyChanged
{
    private string mark = "";
    private string name = "";
    private string note = "";
    private string href = "";
    private string aria = "";
    private bool main;

    public LinkCard(string mark, string name, string note, string href, string aria, bool main)
    {
        this.mark = mark;
        this.name = name;
        this.note = note;
        this.href = href;
        this.aria = aria;
        this.main = main;
    }

    /// <summary>左の四角に出る1〜2文字（「♪」「サブ」など）。</summary>
    public string Mark
    {
        get => mark;
        set { if (Set(ref mark, value)) RaiseAll(); }
    }

    public string Name
    {
        get => name;
        set { if (Set(ref name, value)) RaiseAll(); }
    }

    public string Note
    {
        get => note;
        set { if (Set(ref note, value)) RaiseAll(); }
    }

    public string Href
    {
        get => href;
        set { if (Set(ref href, value)) RaiseAll(); }
    }

    /// <summary>読み上げ用の説明（「YouTubeを開く」）。</summary>
    public string Aria
    {
        get => aria;
        set { if (Set(ref aria, value)) RaiseAll(); }
    }

    /// <summary>1枚だけ大きく出すか（`is-main`）。</summary>
    public bool Main
    {
        get => main;
        set
        {
            if (main == value) return;
            main = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Main)));
            RaiseAll();
        }
    }

    /// <summary>よそのサイトなら、別タブで開きます。</summary>
    public bool External => Href.TrimStart().StartsWith("http", StringComparison.OrdinalIgnoreCase);

    public string? Error
    {
        get
        {
            foreach (var (value, what) in new[]
                     { (Mark, "印"), (Name, "名前"), (Note, "説明"), (Aria, "読み上げ") })
            {
                var problem = HtmlText.Validate(value);
                if (problem != null) return $"{what}：{problem}";
                if (value.Contains('\n')) return $"{what}は1行で入れてください。";
            }
            if (Mark.Trim().Length > 4) return "印は4文字までにしてください。";
            return LinkUrl.Validate(Href);
        }
    }

    public bool HasError => Error != null;

    public (string, string, string, string, string, bool) Snapshot() =>
        (Mark.Trim(), Name.Trim(), Note.Trim(), Href.Trim(), Aria.Trim(), Main);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set(ref string storage, string? value, [CallerMemberName] string? name = null)
    {
        var next = (value ?? "").Replace("\r\n", "\n");
        if (string.Equals(storage, next, StringComparison.Ordinal)) return false;
        storage = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void RaiseAll()
    {
        foreach (var raised in new[] { nameof(External), nameof(Error), nameof(HasError) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(raised));
    }
}

/// <summary>
/// トップページの `link-grid` の中身。
/// うたまぜ！の印（`&lt;!-- utamaze-link:start --&gt;` 以降）には触らず、そのまま残します。
/// </summary>
public sealed class LinkGrid
{
    private static readonly Regex Grid =
        new(@"(<div class=""link-grid"">)([\s\S]*?)(\r?\n[ \t]*</div>)", RegexOptions.CultureInvariant);
    private static readonly Regex Card =
        new(@"<a class=""link-card( is-main)?"" href=""([^""]*)""[^>]*aria-label=""([^""]*)""[^>]*>([\s\S]*?)</a>",
            RegexOptions.CultureInvariant);
    private static readonly Regex MarkSpan = new(@"<span class=""link-mark""[^>]*>([^<]*)</span>");
    private static readonly Regex NameTag = new(@"<strong>([^<]*)</strong>");
    private static readonly Regex NoteTag = new(@"<small>([^<]*)</small>");

    /// <summary>この印から後ろは、うたまぜ！のタブが受け持ちます。</summary>
    private const string UtamazeMark = "<!-- utamaze-link:start";

    private readonly SiteFile file;
    private readonly string newline;
    private readonly Indents indents;
    private List<(string, string, string, string, string, bool)> saved;

    public string FileRelative { get; }
    public ObservableCollection<LinkCard> Cards { get; }

    private LinkGrid(SiteFile file, string fileRelative, IEnumerable<LinkCard> cards, Indents indents)
    {
        this.file = file;
        this.indents = indents;
        FileRelative = fileRelative;
        Cards = new ObservableCollection<LinkCard>(cards);
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        saved = Snapshots();
    }

    public static LinkGrid? Load(SiteFile file, string fileRelative)
    {
        var grid = Grid.Match(file.Text);
        if (!grid.Success) return null;
        var head = Head(grid.Groups[2].Value);
        return new LinkGrid(file, fileRelative, Parse(head),
            Indents.From(head, "<a class=\"link-card", "            "));
    }

    /// <summary>うたまぜ！の印より前（＝ここで編集する範囲）。</summary>
    private static string Head(string inner)
    {
        var at = inner.IndexOf(UtamazeMark, StringComparison.Ordinal);
        return at < 0 ? inner : inner[..at];
    }

    /// <summary>うたまぜ！の印から後ろ（＝そのまま残す範囲）。</summary>
    private static string Tail(string inner)
    {
        var at = inner.IndexOf(UtamazeMark, StringComparison.Ordinal);
        return at < 0 ? "" : inner[at..];
    }

    private static IEnumerable<LinkCard> Parse(string head) =>
        Card.Matches(head).Select(card =>
        {
            var body = card.Groups[4].Value;
            return new LinkCard(
                MarkSpan.Match(body).Groups[1].Value.Trim(),
                NameTag.Match(body).Groups[1].Value.Trim(),
                NoteTag.Match(body).Groups[1].Value.Trim(),
                card.Groups[2].Value,
                card.Groups[3].Value,
                card.Groups[1].Success);
        });

    public LinkCard AddNew()
    {
        var card = new LinkCard("新", "新しいリンク", "説明", "https://", "新しいリンクを開く", main: false);
        Cards.Add(card);
        return card;
    }

    public void Move(LinkCard card, int offset)
    {
        var from = Cards.IndexOf(card);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= Cards.Count) return;
        Cards.Move(from, to);
    }

    /// <summary>いまの中身で、ファイル全体の文字を組み立て直します。</summary>
    public string Rebuild() => Grid.Replace(file.Text, match =>
    {
        var tail = Tail(match.Groups[2].Value);
        var block = Block();
        // 印の前に1行あけます（カードが1枚も無いときは、印だけを残します）。
        if (tail.Length > 0) block += (block.Length > 0 ? newline + newline : newline) + indents.Item + tail;
        return match.Groups[1].Value + block + match.Groups[3].Value;
    }, 1);

    private string Block()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < Cards.Count; i++)
        {
            var card = Cards[i];
            if (i > 0) builder.Append(newline);

            builder.Append(newline).Append(indents.Item)
                   .Append("<a class=\"link-card").Append(card.Main ? " is-main" : "")
                   .Append("\" href=\"").Append(card.Href.Trim()).Append('"');
            if (card.External) builder.Append(" target=\"_blank\" rel=\"noreferrer\"");
            builder.Append(" aria-label=\"").Append(card.Aria.Trim()).Append("\">");

            builder.Append(newline).Append(indents.Inner)
                   .Append("<span class=\"link-mark\" aria-hidden=\"true\">")
                   .Append(card.Mark.Trim()).Append("</span>");
            builder.Append(newline).Append(indents.Inner).Append("<span class=\"link-copy\">");
            builder.Append(newline).Append(indents.Deep).Append("<strong>")
                   .Append(card.Name.Trim()).Append("</strong>");
            builder.Append(newline).Append(indents.Deep).Append("<small>")
                   .Append(card.Note.Trim()).Append("</small>");
            builder.Append(newline).Append(indents.Inner).Append("</span>");
            builder.Append(newline).Append(indents.Inner)
                   .Append("<span class=\"link-arrow\" aria-hidden=\"true\">↗</span>");
            builder.Append(newline).Append(indents.Item).Append("</a>");
        }
        return builder.ToString();
    }

    private List<(string, string, string, string, string, bool)> Snapshots() =>
        Cards.Select(card => card.Snapshot()).ToList();

    public bool HasChanges => !Snapshots().SequenceEqual(saved);
    public bool HasError => Cards.Any(card => card.HasError);

    public IReadOnlyList<ChangeRow> Changes()
    {
        if (!HasChanges) return Array.Empty<ChangeRow>();

        var now = Snapshots();
        var rows = new List<ChangeRow>();
        var added = now.Select(card => card.Item2).Except(saved.Select(card => card.Item2)).ToArray();
        var removed = saved.Select(card => card.Item2).Except(now.Select(card => card.Item2)).ToArray();

        foreach (var name in added) rows.Add(new ChangeRow("リンク集：追加", "", name, FileRelative));
        foreach (var name in removed) rows.Add(new ChangeRow("リンク集：削除", name, "", FileRelative));
        if (added.Length == 0 && removed.Length == 0)
            rows.Add(new ChangeRow("リンク集：内容または並び順を変更",
                $"{saved.Count}件", $"{now.Count}件", FileRelative));
        return rows;
    }

    internal void Apply()
    {
        if (HasChanges) file.SetText(Rebuild());
    }

    internal void MarkSaved() => saved = Snapshots();

    public void Revert()
    {
        var grid = Grid.Match(file.Text);
        Cards.Clear();
        if (grid.Success)
            foreach (var card in Parse(Head(grid.Groups[2].Value))) Cards.Add(card);
        saved = Snapshots();
    }
}
