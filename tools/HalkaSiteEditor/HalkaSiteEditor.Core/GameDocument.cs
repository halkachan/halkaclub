using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// ゲームのページで使う、ごく簡単な強調の書き方。
/// **ここ** と書くと &lt;strong&gt;ここ&lt;/strong&gt; になります。HTMLの印は画面に出しません。
/// </summary>
public static class GameMarkup
{
    private static readonly Regex Strong = new(@"<strong>([\s\S]*?)</strong>", RegexOptions.CultureInvariant);
    private static readonly Regex Marked = new(@"\*\*([\s\S]+?)\*\*", RegexOptions.CultureInvariant);

    public static string ToPlain(string html) =>
        Strong.Replace(html, match => "**" + match.Groups[1].Value + "**");

    public static string ToHtml(string plain) =>
        Marked.Replace(plain, match => "<strong>" + match.Groups[1].Value + "</strong>");

    /// <summary>画面に入力された文字として正しいか。</summary>
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "空にはできません。";
        if (Regex.Matches(value, @"\*\*").Count % 2 != 0)
            return "強調は **ここ** のように、** で囲んでください。";
        return HtmlText.Validate(value.Replace("**", ""));
    }
}

/// <summary>ゲーム集（一覧ページ）に並ぶカード1枚。</summary>
public sealed class GameCard : INotifyPropertyChanged
{
    private string title = "";
    private string description = "";
    private string tags = "";
    private string href = "";

    public GameCard(string title, string description, string tags, string href)
    {
        this.title = title;
        this.description = description;
        this.tags = tags;
        this.href = href;
    }

    public string Title
    {
        get => title;
        set { if (Set(ref title, value)) RaiseAll(); }
    }

    /// <summary>1行＝1行。保存するとき &lt;br /&gt; でつなぎます。</summary>
    public string Description
    {
        get => description;
        set { if (Set(ref description, value)) RaiseAll(); }
    }

    /// <summary>「スマホ対応」などの札。1行＝1つ。空なら札ごと出しません。</summary>
    public string Tags
    {
        get => tags;
        set { if (Set(ref tags, value)) RaiseAll(); }
    }

    /// <summary>ゲームページの場所（`gyugyu-rinchan/` の形）。</summary>
    public string Href
    {
        get => href;
        set { if (Set(ref href, value)) RaiseAll(); }
    }

    /// <summary>`gyugyu-rinchan/` から `gyugyu-rinchan` を取り出します。</summary>
    public string Slug => Href.Trim().Trim('/').Split('/').LastOrDefault() ?? "";

    public IReadOnlyList<string> TagLines() => Lines(Tags);

    public IReadOnlyList<string> DescriptionLines() => Lines(Description);

    private static IReadOnlyList<string> Lines(string value) => value.Split('\n')
        .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return "名前を入れてください。";
            if (Title.Contains('\n')) return "名前は1行で入れてください。";
            foreach (var line in DescriptionLines().Concat(TagLines()).Append(Title))
            {
                var problem = GameMarkup.Validate(line);
                if (problem != null) return problem;
            }
            return LinkUrl.Validate(Href);
        }
    }

    public bool HasError => Error != null;

    public (string, string, string, string) Snapshot() =>
        (Title.Trim(), Description, Tags, Href.Trim());

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slug)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
    }
}

/// <summary>更新履歴の1件。</summary>
public sealed class GameChangelogEntry : INotifyPropertyChanged
{
    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}$");

    private readonly string loadedDate;
    private readonly string loadedDisplay;
    private string version = "";
    private string date = "";
    private string items = "";
    private string note = "";

    public GameChangelogEntry(string version, string date, string display, string items, string note)
    {
        this.version = version;
        this.date = date;
        this.items = items;
        this.note = note;
        loadedDate = date;
        loadedDisplay = display;
    }

    /// <summary>「ver 1.2」のような表記をそのまま持ちます。</summary>
    public string Version
    {
        get => version;
        set { if (Set(ref version, value)) RaiseAll(); }
    }

    /// <summary>2026-09-10 の形。ページには 2026.9.10 の形で出します。</summary>
    public string Date
    {
        get => date;
        set { if (Set(ref date, value)) RaiseAll(); }
    }

    /// <summary>1行＝1項目。**ここ** で強調できます。</summary>
    public string Items
    {
        get => items;
        set { if (Set(ref items, value)) RaiseAll(); }
    }

    /// <summary>項目のあとに出す補足。空なら出しません。</summary>
    public string Note
    {
        get => note;
        set { if (Set(ref note, value)) RaiseAll(); }
    }

    /// <summary>ページに出す日付。読み込んだときから変えていなければ、元の書き方をそのまま使います。</summary>
    public string Display => string.Equals(Date.Trim(), loadedDate, StringComparison.Ordinal)
        ? loadedDisplay
        : Format(Date.Trim());

    private static string Format(string value) =>
        DateTime.TryParse(value, out var parsed) ? parsed.ToString("yyyy.M.d") : value;

    public IReadOnlyList<string> ItemLines() => Items.Split('\n')
        .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Version)) return "版を入れてください（「ver 1.3」の形）。";
            if (Version.Contains('\n')) return "版は1行で入れてください。";
            if (!DatePattern.IsMatch(Date.Trim())) return "日付は 2026-09-10 の形で入れてください。";
            if (ItemLines().Count == 0) return "内容を1行以上入れてください。";
            foreach (var line in ItemLines().Append(Version))
            {
                var problem = GameMarkup.Validate(line);
                if (problem != null) return problem;
            }
            if (!string.IsNullOrWhiteSpace(Note)) return GameMarkup.Validate(Note.Replace("\n", " "));
            return null;
        }
    }

    public bool HasError => Error != null;

    public (string, string, string, string) Snapshot() =>
        (Version.Trim(), Date.Trim(), Items, Note.Trim());

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
    }
}

/// <summary>字下げの決まり。1枚ぶんの &lt;article&gt; の位置から中身の位置を決めます。</summary>
internal sealed record Indents(string Item)
{
    public string Inner => Item + "  ";
    public string Deep => Item + "    ";

    public static Indents From(string inner, string open, string fallback)
    {
        var match = Regex.Match(inner, @"\r?\n([ \t]*)" + Regex.Escape(open));
        return new Indents(match.Success ? match.Groups[1].Value : fallback);
    }
}

/// <summary>ゲーム集（game/index.html）のカード並び。カードの外側には触れません。</summary>
public sealed class GameCollection
{
    private static readonly Regex Grid =
        new(@"(<div class=""game-grid"">)([\s\S]*?)(\r?\n[ \t]*</div>)", RegexOptions.CultureInvariant);
    private static readonly Regex Card =
        new(@"<article class=""game-card"">([\s\S]*?)</article>", RegexOptions.CultureInvariant);
    private static readonly Regex Heading = new(@"<h2>([\s\S]*?)</h2>");
    private static readonly Regex Description = new(@"</h2>\s*<p>([\s\S]*?)</p>");
    private static readonly Regex TagList = new(@"<ul class=""game-card-tags""[^>]*>([\s\S]*?)</ul>");
    private static readonly Regex TagItem = new(@"<li>([\s\S]*?)</li>");
    private static readonly Regex Link = new(@"<a class=""game-card-link"" href=""([^""]*)""");

    private readonly SiteFile file;
    private readonly string newline;
    private readonly Indents indents;
    private List<(string, string, string, string)> saved;

    public string FileRelative { get; }
    public ObservableCollection<GameCard> Cards { get; }

    private GameCollection(SiteFile file, string fileRelative, IEnumerable<GameCard> cards, Indents indents)
    {
        this.file = file;
        this.indents = indents;
        FileRelative = fileRelative;
        Cards = new ObservableCollection<GameCard>(cards);
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        saved = Snapshots();
    }

    public static GameCollection? Load(SiteFile file, string fileRelative)
    {
        var grid = Grid.Match(file.Text);
        if (!grid.Success) return null;
        var inner = grid.Groups[2].Value;
        return new GameCollection(file, fileRelative, Parse(inner),
            Indents.From(inner, "<article class=\"game-card\">", "            "));
    }

    private static IEnumerable<GameCard> Parse(string inner) =>
        Card.Matches(inner).Select(card =>
        {
            var body = card.Groups[1].Value;
            var tags = TagList.Match(body);
            return new GameCard(
                Flatten(Heading.Match(body).Groups[1].Value),
                Flatten(Description.Match(body).Groups[1].Value),
                tags.Success
                    ? string.Join("\n", TagItem.Matches(tags.Groups[1].Value)
                        .Select(item => Flatten(item.Groups[1].Value)))
                    : "",
                Link.Match(body).Groups[1].Value);
        });

    private static string Flatten(string inner) => GameMarkup.ToPlain(
        Regex.Replace(inner, @"\r?\n[ \t]*", "").Replace("<br />", "\n").Trim());

    public GameCard AddNew()
    {
        var card = new GameCard("新しいゲーム", "なにかのゲーム", "スマホ対応", "");
        Cards.Add(card);
        return card;
    }

    public void Move(GameCard card, int offset)
    {
        var from = Cards.IndexOf(card);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= Cards.Count) return;
        Cards.Move(from, to);
    }

    /// <summary>いまの中身で、ファイル全体の文字を組み立て直します。</summary>
    public string Rebuild() => Grid.Replace(file.Text,
        match => match.Groups[1].Value + Block() + match.Groups[3].Value, 1);

    private string Block()
    {
        if (Cards.Count == 0) return "";

        var builder = new StringBuilder();
        for (var i = 0; i < Cards.Count; i++)
        {
            var card = Cards[i];
            if (i > 0) builder.Append(newline);
            builder.Append(newline).Append(indents.Item).Append("<article class=\"game-card\">");
            builder.Append(newline).Append(indents.Inner)
                   .Append("<p class=\"game-card-number\" aria-hidden=\"true\">")
                   .Append((i + 1).ToString("00")).Append("</p>");
            builder.Append(newline).Append(indents.Inner).Append("<h2>")
                   .Append(GameMarkup.ToHtml(card.Title.Trim())).Append("</h2>");

            var lines = card.DescriptionLines();
            if (lines.Count > 0)
            {
                builder.Append(newline).Append(indents.Inner).Append("<p>");
                for (var line = 0; line < lines.Count; line++)
                {
                    builder.Append(newline).Append(indents.Deep).Append(GameMarkup.ToHtml(lines[line]));
                    if (line < lines.Count - 1) builder.Append("<br />");
                }
                builder.Append(newline).Append(indents.Inner).Append("</p>");
            }

            var tags = card.TagLines();
            if (tags.Count > 0)
            {
                builder.Append(newline).Append(indents.Inner)
                       .Append("<ul class=\"game-card-tags\" aria-label=\"対応情報\">");
                foreach (var tag in tags)
                    builder.Append(newline).Append(indents.Deep).Append("<li>")
                           .Append(GameMarkup.ToHtml(tag)).Append("</li>");
                builder.Append(newline).Append(indents.Inner).Append("</ul>");
            }

            builder.Append(newline).Append(indents.Inner)
                   .Append("<a class=\"game-card-link\" href=\"").Append(card.Href.Trim()).Append("\">");
            builder.Append(newline).Append(indents.Deep).Append("<span>ゲームページへ</span>");
            builder.Append(newline).Append(indents.Deep).Append("<span aria-hidden=\"true\">▶</span>");
            builder.Append(newline).Append(indents.Inner).Append("</a>");
            builder.Append(newline).Append(indents.Item).Append("</article>");
        }
        return builder.ToString();
    }

    private List<(string, string, string, string)> Snapshots() =>
        Cards.Select(card => card.Snapshot()).ToList();

    public bool HasChanges => !Snapshots().SequenceEqual(saved);
    public bool HasError => Cards.Any(card => card.HasError);

    public IReadOnlyList<ChangeRow> Changes()
    {
        if (!HasChanges) return Array.Empty<ChangeRow>();

        var now = Snapshots();
        var rows = new List<ChangeRow>();
        var added = now.Select(card => card.Item1).Except(saved.Select(card => card.Item1)).ToArray();
        var removed = saved.Select(card => card.Item1).Except(now.Select(card => card.Item1)).ToArray();

        foreach (var title in added) rows.Add(new ChangeRow("ゲーム集：カードを追加", "", title, FileRelative));
        foreach (var title in removed) rows.Add(new ChangeRow("ゲーム集：カードを削除", title, "", FileRelative));
        if (added.Length == 0 && removed.Length == 0)
            rows.Add(new ChangeRow("ゲーム集：内容または並び順を変更",
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
            foreach (var card in Parse(grid.Groups[2].Value)) Cards.Add(card);
        saved = Snapshots();
    }
}

/// <summary>ゲーム1本ぶんのページ（game/&lt;名前&gt;/index.html）。</summary>
public sealed class GamePage
{
    private static readonly Regex ChangelogBlock = new(
        @"(<h2 id=""changelog-title"">[^<]*</h2>)([\s\S]*?)(\r?\n[ \t]*</section>)", RegexOptions.CultureInvariant);
    private static readonly Regex Entry =
        new(@"<article class=""changelog-entry"">([\s\S]*?)</article>", RegexOptions.CultureInvariant);
    private static readonly Regex EntryVersion = new(@"<span class=""changelog-ver"">([^<]*)</span>");
    private static readonly Regex EntryTime = new(@"<time datetime=""([^""]*)"">([^<]*)</time>");
    private static readonly Regex EntryItem = new(@"<li>([\s\S]*?)</li>");
    private static readonly Regex EntryNote = new(@"<p class=""changelog-note"">([\s\S]*?)</p>");

    private readonly SiteFile file;
    private readonly string newline;
    private readonly Indents indents;
    private readonly bool hasChangelog;
    private List<(string, string, string, string)> saved;

    public string Slug { get; }
    public string FileRelative { get; }
    public EditField Title { get; }
    public EditField GameUrl { get; }
    public EditField? Orientation { get; }
    public EditField? Version { get; }
    public BrText? Description { get; }
    public ObservableCollection<GameChangelogEntry> Changelog { get; }

    /// <summary>画面に並べる入力欄。</summary>
    public IReadOnlyList<FieldRow> Rows { get; }

    /// <summary>一覧ページの、このゲームのカード。版をそろえるときに使います。</summary>
    public GameCard? Card { get; internal set; }

    public bool HasChangelog => hasChangelog;

    public override string ToString() => Title.Value;

    private GamePage(SiteFile file, string fileRelative, string slug, EditField title, EditField gameUrl,
        EditField? orientation, EditField? version, BrText? description,
        IEnumerable<GameChangelogEntry> changelog, bool hasChangelog, Indents indents)
    {
        this.file = file;
        this.indents = indents;
        this.hasChangelog = hasChangelog;
        FileRelative = fileRelative;
        Slug = slug;
        Title = title;
        GameUrl = gameUrl;
        Orientation = orientation;
        Version = version;
        Description = description;
        Changelog = new ObservableCollection<GameChangelogEntry>(changelog);
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        saved = Snapshots();

        var rows = new List<FieldRow>
        {
            new("名前", new FieldCell("ゲームの名前（ページの中の4か所がまとめて変わります）", title)),
            new("ゲーム本体", new FieldCell("URL（「ゲームを起動」と「別タブで遊ぶ」の両方）", gameUrl)),
        };
        if (orientation != null)
            rows.Add(new FieldRow("画面の向き", new FieldCell("ページに出す一文", orientation, 260)));
        if (version != null)
            rows.Add(new FieldRow("いまの版", new FieldCell("ページに出す表記（「ver 1.3」の形）", version, 220)));
        Rows = rows;
    }

    /// <summary>ゲームのページでなければ null（移動のための置き石など）。</summary>
    internal static GamePage? Load(SiteFile file, string fileRelative, string slug)
    {
        if (!file.Text.Contains("data-game-url=\"")) return null;

        EditField? Field(string pattern, string id, string label, FieldKind kind,
            params string[] mirrors)
        {
            if (new ValueSlot(pattern).Count(file.Text) == 0) return null;
            return new EditField(file, fileRelative, new ValueSlot(pattern), $"game.{slug}.{id}", label, kind,
                mirrors.Where(mirror => new ValueSlot(mirror).Count(file.Text) > 0)
                    .Select(mirror => new ValueSlot(mirror)).ToArray());
        }

        // ゲームの名前は、見出し・ページ名・ヘッダー・起動ボタンの4か所に同じものが入ります。
        var title = Field(@"(<h1 id=""game-title"">)([^<]*)(</h1>)", "title", "ゲームの名前", FieldKind.HtmlText,
            @"(<title>)([^<|]*)( \| HALKA</title>)",
            @"(<header class=""site-header"">[\s\S]*?<p>)([^<]*)(</p>)",
            @"(data-game-title="")([^""]*)("")");
        // ゲーム本体のURLは、起動ボタンと「別タブで遊ぶ」の2か所。
        var url = Field(@"(data-game-url="")([^""]*)("")", "url", "ゲーム本体のURL", FieldKind.Url,
            @"(<a class=""game-newtab-button"" href="")([^""]*)("")");
        if (title == null || url == null) return null;

        var orientation = Field(@"(<p class=""game-orientation-note"">)([^<]*)(</p>)",
            "orientation", "画面の向きの一文", FieldKind.HtmlText);
        var version = Field(@"(<p class=""game-version"">)([^<]*)(</p>)", "version", "いまの版", FieldKind.HtmlText);

        var description = BrText.Create(file, fileRelative, "説明", "1行＝1行です。**ここ** で強調できます。",
            new Regex(@"(<div class=""game-description"">\r?\n[ \t]*<p>)([\s\S]*?)(</p>)",
                RegexOptions.CultureInvariant));

        var block = ChangelogBlock.Match(file.Text);
        var inner = block.Success ? block.Groups[2].Value : "";
        return new GamePage(file, fileRelative, slug, title, url, orientation, version, description,
            ParseChangelog(inner), block.Success,
            Indents.From(inner, "<article class=\"changelog-entry\">", "            "));
    }

    private static IEnumerable<GameChangelogEntry> ParseChangelog(string inner) =>
        Entry.Matches(inner).Select(entry =>
        {
            var body = entry.Groups[1].Value;
            var time = EntryTime.Match(body);
            var note = EntryNote.Match(body);
            return new GameChangelogEntry(
                Flatten(EntryVersion.Match(body).Groups[1].Value),
                time.Groups[1].Value,
                time.Groups[2].Value,
                string.Join("\n", EntryItem.Matches(body).Select(item => Flatten(item.Groups[1].Value))),
                note.Success ? Flatten(note.Groups[1].Value) : "");
        });

    private static string Flatten(string inner) => GameMarkup.ToPlain(
        Regex.Replace(inner, @"\r?\n[ \t]*", " ").Replace("<br />", "\n").Trim());

    /// <summary>一番上（＝いちばん新しい版）に空の1件を足します。版は前の版から1つ進めます。</summary>
    public GameChangelogEntry AddNewEntry()
    {
        var entry = new GameChangelogEntry(NextVersion(), DateTime.Now.ToString("yyyy-MM-dd"),
            DateTime.Now.ToString("yyyy.M.d"), "", "");
        Changelog.Insert(0, entry);
        return entry;
    }

    private string NextVersion()
    {
        var latest = Changelog.FirstOrDefault()?.Version.Trim();
        if (string.IsNullOrEmpty(latest)) return "ver 1.0";

        // 末尾の数字だけを1つ進めます（ver 1.2 → ver 1.3）。
        var match = Regex.Match(latest, @"^(.*?)(\d+)$");
        if (!match.Success) return latest;
        return match.Groups[1].Value + (int.Parse(match.Groups[2].Value) + 1);
    }

    public void MoveEntry(GameChangelogEntry entry, int offset)
    {
        var from = Changelog.IndexOf(entry);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= Changelog.Count) return;
        Changelog.Move(from, to);
    }

    // --- 版をそろえる -------------------------------------------------------

    /// <summary>いちばん新しい版（更新履歴の一番上）。</summary>
    public string LatestVersion => Changelog.FirstOrDefault()?.Version.Trim() ?? "";

    /// <summary>一覧ページのカードに出ている「ver 〇〇」の札。</summary>
    public string CardVersion =>
        Card?.TagLines().FirstOrDefault(tag => tag.StartsWith("ver", StringComparison.OrdinalIgnoreCase)) ?? "";

    public bool CanAlignVersion => LatestVersion.Length > 0 &&
        ((Version != null && Version.Value.Trim() != LatestVersion) ||
         (Card != null && CardVersion != LatestVersion));

    /// <summary>いまの食い違いを、画面に出す1行にまとめます。</summary>
    public string VersionSummary
    {
        get
        {
            if (!HasChangelog || LatestVersion.Length == 0) return "";
            var problems = new List<string>();
            if (Version != null && Version.Value.Trim() != LatestVersion)
                problems.Add($"このページの表記は「{Version.Value.Trim()}」");
            if (Card != null && CardVersion != LatestVersion)
            {
                problems.Add(CardVersion.Length == 0
                    ? "一覧ページの札に版が無い"
                    : $"一覧ページの札は「{CardVersion}」");
            }
            return problems.Count == 0
                ? $"更新履歴・このページ・一覧ページ、どれも「{LatestVersion}」でそろっています。"
                : $"更新履歴の一番上は「{LatestVersion}」ですが、" + string.Join("、", problems) + "です。";
        }
    }

    /// <summary>更新履歴の一番上の版に、ページの表記と一覧の札をそろえます。</summary>
    public void AlignVersion()
    {
        var latest = LatestVersion;
        if (latest.Length == 0) return;

        if (Version != null) Version.Value = latest;
        if (Card == null) return;

        var tags = Card.TagLines().ToList();
        var index = tags.FindIndex(tag => tag.StartsWith("ver", StringComparison.OrdinalIgnoreCase));
        if (index >= 0) tags[index] = latest;
        else tags.Add(latest);
        Card.Tags = string.Join("\n", tags);
    }

    // --- 更新履歴の書き戻し -------------------------------------------------

    /// <summary>いまの中身で、ファイル全体の文字を組み立て直します。</summary>
    public string Rebuild() => !hasChangelog
        ? file.Text
        : ChangelogBlock.Replace(file.Text,
            match => match.Groups[1].Value + Block() + match.Groups[3].Value, 1);

    private string Block()
    {
        if (Changelog.Count == 0) return "";

        var builder = new StringBuilder();
        for (var i = 0; i < Changelog.Count; i++)
        {
            var entry = Changelog[i];
            builder.Append(newline);
            builder.Append(newline).Append(indents.Item).Append("<article class=\"changelog-entry\">");
            builder.Append(newline).Append(indents.Inner).Append("<header>");
            builder.Append(newline).Append(indents.Deep).Append("<span class=\"changelog-ver\">")
                   .Append(GameMarkup.ToHtml(entry.Version.Trim())).Append("</span>");
            builder.Append(newline).Append(indents.Deep).Append("<time datetime=\"")
                   .Append(entry.Date.Trim()).Append("\">").Append(entry.Display).Append("</time>");
            builder.Append(newline).Append(indents.Inner).Append("</header>");

            builder.Append(newline).Append(indents.Inner).Append("<ul>");
            foreach (var line in entry.ItemLines())
                builder.Append(newline).Append(indents.Deep).Append("<li>")
                       .Append(GameMarkup.ToHtml(line)).Append("</li>");
            builder.Append(newline).Append(indents.Inner).Append("</ul>");

            if (!string.IsNullOrWhiteSpace(entry.Note))
            {
                builder.Append(newline).Append(indents.Inner).Append("<p class=\"changelog-note\">");
                builder.Append(newline).Append(indents.Deep).Append(GameMarkup.ToHtml(entry.Note.Trim()));
                builder.Append(newline).Append(indents.Inner).Append("</p>");
            }

            builder.Append(newline).Append(indents.Item).Append("</article>");
        }
        return builder.ToString();
    }

    private List<(string, string, string, string)> Snapshots() =>
        Changelog.Select(entry => entry.Snapshot()).ToList();

    public bool ChangelogChanged => !Snapshots().SequenceEqual(saved);

    public bool HasChanges => ChangelogChanged || Description?.Changed == true;

    public bool HasError => Changelog.Any(entry => entry.HasError) || Description?.HasError == true;

    public IReadOnlyList<ChangeRow> Changes()
    {
        var rows = new List<ChangeRow>();
        if (Description?.Change() is { } row) rows.Add(new ChangeRow(
            $"{Title.Original}：説明", row.Before, row.After, FileRelative));

        if (!ChangelogChanged) return rows;

        var now = Snapshots();
        var added = now.Select(entry => entry.Item1).Except(saved.Select(entry => entry.Item1)).ToArray();
        var removed = saved.Select(entry => entry.Item1).Except(now.Select(entry => entry.Item1)).ToArray();

        foreach (var version in added)
            rows.Add(new ChangeRow($"{Title.Original}：更新履歴を追加", "", version, FileRelative));
        foreach (var version in removed)
            rows.Add(new ChangeRow($"{Title.Original}：更新履歴を削除", version, "", FileRelative));
        if (added.Length == 0 && removed.Length == 0)
            rows.Add(new ChangeRow($"{Title.Original}：更新履歴を変更",
                $"{saved.Count}件", $"{now.Count}件", FileRelative));
        return rows;
    }

    internal void Apply()
    {
        Description?.Apply();
        if (ChangelogChanged) file.SetText(Rebuild());
    }

    internal void MarkSaved()
    {
        Description?.MarkSaved();
        saved = Snapshots();
    }

    public void Revert()
    {
        Description?.Revert();
        if (!hasChangelog) return;

        var block = ChangelogBlock.Match(file.Text);
        Changelog.Clear();
        if (block.Success)
            foreach (var entry in ParseChangelog(block.Groups[2].Value)) Changelog.Add(entry);
        saved = Snapshots();
    }
}

/// <summary>ゲーム集ぜんたい（一覧ページ＋各ゲームのページ）。</summary>
public sealed class GameDocument
{
    public GameCollection Collection { get; }
    public EditField Lead { get; }
    public IReadOnlyList<GamePage> Pages { get; }

    private GameDocument(GameCollection collection, EditField lead, IReadOnlyList<GamePage> pages)
    {
        Collection = collection;
        Lead = lead;
        Pages = pages;

        // 一覧のカードと各ページを結びつけます（版をそろえるときに両方を見ます）。
        foreach (var page in pages)
            page.Card = collection.Cards.FirstOrDefault(card => card.Slug == page.Slug);
    }

    /// <summary>ゲーム集が無いサイトなら null。<paramref name="open"/> はファイルを開いて登録する関数です。</summary>
    public static GameDocument? Load(string root, Func<string, SiteFile> open)
    {
        var indexPath = SitePaths.GameIndex(root);
        if (!File.Exists(indexPath)) return null;

        var indexFile = open(indexPath);
        var relative = SitePaths.Relative(root, indexFile.Path);
        var collection = GameCollection.Load(indexFile, relative);
        if (collection == null) return null;

        var lead = new EditField(indexFile, relative,
            new ValueSlot(@"(<p class=""game-collection-lead"">)([^<]*)(</p>)"),
            "game.lead", "ゲーム集のひとこと", FieldKind.HtmlText);

        // 一覧に並んでいる順でページを開き、そのあとに一覧へ載っていないページを足します。
        var pages = new List<GamePage>();
        var slugs = collection.Cards.Select(card => card.Slug)
            .Concat(Directory.GetDirectories(Path.GetDirectoryName(indexPath)!)
                .Select(Path.GetFileName).OfType<string>())
            .Where(slug => slug.Length > 0).Distinct(StringComparer.Ordinal);

        foreach (var slug in slugs)
        {
            var path = SitePaths.GamePage(root, slug);
            // ゲームのページでないもの（移動のための置き石など）は、開かずに飛ばします。
            if (!SitePaths.IsGamePage(path)) continue;
            var page = GamePage.Load(open(path), SitePaths.Relative(root, path), slug);
            if (page != null) pages.Add(page);
        }

        return new GameDocument(collection, lead, pages);
    }

    public IEnumerable<EditField> Fields => Pages
        .SelectMany(page => new[] { page.Title, page.GameUrl, page.Orientation, page.Version })
        .OfType<EditField>()
        .Prepend(Lead);

    public bool HasChanges => Collection.HasChanges || Pages.Any(page => page.HasChanges);
    public bool HasError => Collection.HasError || Pages.Any(page => page.HasError);

    public IReadOnlyList<ChangeRow> Changes() => Collection.Changes()
        .Concat(Pages.SelectMany(page => page.Changes())).ToArray();

    internal void Apply()
    {
        Collection.Apply();
        foreach (var page in Pages) page.Apply();
    }

    internal void MarkSaved()
    {
        Collection.MarkSaved();
        foreach (var page in Pages) page.MarkSaved();
    }

    public void Revert()
    {
        Collection.Revert();
        foreach (var page in Pages) page.Revert();
        foreach (var page in Pages)
            page.Card = Collection.Cards.FirstOrDefault(card => card.Slug == page.Slug);
    }
}
