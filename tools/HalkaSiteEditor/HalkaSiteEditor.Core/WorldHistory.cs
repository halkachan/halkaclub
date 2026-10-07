using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>HALKA WORLD の更新履歴の1件。</summary>
public sealed class WorldEntry : INotifyPropertyChanged
{
    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}$");

    private readonly string loadedDate;
    private readonly string loadedDisplay;
    private string version = "";
    private string date = "";
    private string items = "";

    public WorldEntry(string version, string date, string display, string items, bool usesList)
    {
        this.version = version;
        this.date = date;
        this.items = items;
        loadedDate = date;
        loadedDisplay = display;
        UsesList = usesList;
    }

    /// <summary>「ver2.2」の形。</summary>
    public string Version
    {
        get => version;
        set { if (Set(ref version, value)) RaiseAll(); }
    }

    /// <summary>2026-10-07 の形。</summary>
    public string Date
    {
        get => date;
        set { if (Set(ref date, value)) RaiseAll(); }
    }

    /// <summary>1行＝1項目。</summary>
    public string Items
    {
        get => items;
        set { if (Set(ref items, value)) RaiseAll(); }
    }

    /// <summary>
    /// もとの書き方。箇条書き（&lt;ul&gt;）か、ひとつの段落（&lt;p&gt;）か。
    /// 触っていない件を元どおりに戻すために覚えておきます。
    /// </summary>
    public bool UsesList { get; private set; }

    /// <summary>ページに出す日付。読み込んだときから変えていなければ、元の書き方のままです。</summary>
    public string Display => string.Equals(Date.Trim(), loadedDate, StringComparison.Ordinal)
        ? loadedDisplay
        : Date.Trim();

    public IReadOnlyList<string> ItemLines() => Items.Split('\n')
        .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Version)) return "版を入れてください（「ver2.3」の形）。";
            if (Version.Contains('\n')) return "版は1行で入れてください。";
            if (!DatePattern.IsMatch(Date.Trim())) return "日付は 2026-10-07 の形で入れてください。";
            if (ItemLines().Count == 0) return "内容を1行以上入れてください。";
            foreach (var line in ItemLines().Append(Version))
            {
                var problem = HtmlText.Validate(line);
                if (problem != null) return problem;
            }
            return null;
        }
    }

    public bool HasError => Error != null;

    /// <summary>項目が増えたら、ひとつの段落では収まらないので箇条書きにします。</summary>
    internal bool AsList => UsesList || ItemLines().Count > 1;

    public (string, string, string) Snapshot() => (Version.Trim(), Date.Trim(), Items);

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
        foreach (var name in new[] { nameof(Display), nameof(Error), nameof(HasError) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>更新履歴をまとめている箱（「ver2.1 ～ ver3.0」）。</summary>
public sealed class WorldArchive : INotifyPropertyChanged
{
    private string summary;

    internal WorldArchive(string summary, IEnumerable<WorldEntry> entries)
    {
        this.summary = summary;
        Entries = new ObservableCollection<WorldEntry>(entries);
    }

    public string Summary
    {
        get => summary;
        set
        {
            var next = (value ?? "").Replace("\r\n", "\n").Replace("\n", "");
            if (string.Equals(summary, next, StringComparison.Ordinal)) return;
            summary = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        }
    }

    public ObservableCollection<WorldEntry> Entries { get; }

    public string? Error => HtmlText.Validate(Summary);

    public bool HasError => Error != null || Entries.Any(entry => entry.HasError);

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// HALKA WORLD の更新履歴（halkaworld/index.html）。
/// 見出しやゲームの埋め込みには触らず、更新履歴の中身だけを組み立て直します。
/// </summary>
public sealed class WorldHistory
{
    private static readonly Regex Block = new(
        @"(<h2 id=""update-history-title"">[^<]*</h2>)([\s\S]*?)(\r?\n[ \t]*</section>)",
        RegexOptions.CultureInvariant);
    private static readonly Regex Archive = new(
        @"<details class=""update-history-archive"">\s*<summary>([^<]*)</summary>\s*" +
        @"<div class=""update-history-entries"">([\s\S]*?)\r?\n[ \t]*</div>\s*</details>",
        RegexOptions.CultureInvariant);
    private static readonly Regex Entry =
        new(@"<article class=""update-entry"">([\s\S]*?)</article>", RegexOptions.CultureInvariant);
    private static readonly Regex Head =
        new(@"<h3>([\s\S]*?)<time datetime=""([^""]*)"">([^<]*)</time>\s*</h3>");
    private static readonly Regex ListItem = new(@"<li>([\s\S]*?)</li>");
    private static readonly Regex Paragraph = new(@"<p>([\s\S]*?)</p>");

    private readonly SiteFile file;
    private readonly string newline;
    private readonly string archiveIndent;
    private readonly string entryIndent;
    private List<(string, List<(string, string, string)>)> saved;

    public string FileRelative { get; }
    public ObservableCollection<WorldArchive> Archives { get; }
    /// <summary>ページの上に出ている「いまの版」。</summary>
    public EditField? CurrentVersion { get; private set; }
    /// <summary>遊びかたの説明（キーの案内）。</summary>
    public EditField? Controls { get; private set; }

    private IEnumerable<EditField> Fields =>
        new[] { CurrentVersion, Controls }.OfType<EditField>();

    private WorldHistory(SiteFile file, string fileRelative, IEnumerable<WorldArchive> archives,
        string archiveIndent, string entryIndent)
    {
        this.file = file;
        this.archiveIndent = archiveIndent;
        this.entryIndent = entryIndent;
        FileRelative = fileRelative;
        Archives = new ObservableCollection<WorldArchive>(archives);
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        saved = Snapshots();
    }

    /// <summary>更新履歴が無いページなら null。</summary>
    public static WorldHistory? Load(SiteFile file, string fileRelative)
    {
        var block = Block.Match(file.Text);
        if (!block.Success) return null;

        var inner = block.Groups[2].Value;
        var history = new WorldHistory(file, fileRelative, Parse(inner),
            Indents.From(inner, "<details class=\"update-history-archive\">", "            ").Item,
            Indents.From(inner, "<article class=\"update-entry\">", "            ").Item);

        if (new ValueSlot(VersionSlot).Count(file.Text) > 0)
        {
            history.CurrentVersion = new EditField(file, fileRelative, new ValueSlot(VersionSlot),
                "world.version", "いまの版", FieldKind.HtmlText);
        }
        if (new ValueSlot(ControlsSlot).Count(file.Text) > 0)
        {
            history.Controls = new EditField(file, fileRelative, new ValueSlot(ControlsSlot),
                "world.controls", "遊びかたの説明", FieldKind.HtmlText);
        }
        return history;
    }

    private const string VersionSlot = @"(<p class=""game-current-version"">)([^<]*)(</p>)";
    private const string ControlsSlot = @"(<p class=""game-controls"">)([^<]*)(</p>)";

    private static IEnumerable<WorldArchive> Parse(string inner) =>
        Archive.Matches(inner).Select(archive => new WorldArchive(
            archive.Groups[1].Value.Trim(),
            Entry.Matches(archive.Groups[2].Value).Select(entry =>
            {
                var body = entry.Groups[1].Value;
                var head = Head.Match(body);
                var items = ListItem.Matches(body);
                return new WorldEntry(
                    Flatten(head.Groups[1].Value),
                    head.Groups[2].Value,
                    head.Groups[3].Value,
                    items.Count > 0
                        ? string.Join("\n", items.Select(item => Flatten(item.Groups[1].Value)))
                        : string.Join("\n", Paragraph.Matches(body).Select(item => Flatten(item.Groups[1].Value))),
                    items.Count > 0);
            })));

    private static string Flatten(string inner) =>
        Regex.Replace(inner, @"\r?\n[ \t]*", "").Replace("<br />", "\n").Trim();

    // --- 増やす・並べ替える・減らす -----------------------------------------

    /// <summary>いちばん新しい箱の先頭に、1件足します。版は前の版から1つ進めます。</summary>
    public WorldEntry? AddNewEntry()
    {
        var archive = Archives.FirstOrDefault();
        if (archive == null) return null;

        var entry = new WorldEntry(NextVersion(), DateTime.Now.ToString("yyyy-MM-dd"),
            DateTime.Now.ToString("yyyy-MM-dd"), "", usesList: true);
        archive.Entries.Insert(0, entry);
        return entry;
    }

    private string NextVersion()
    {
        var latest = Archives.FirstOrDefault()?.Entries.FirstOrDefault()?.Version.Trim();
        if (string.IsNullOrEmpty(latest)) return "ver1.0";

        var match = Regex.Match(latest, @"^(.*?)(\d+)$");
        return match.Success ? match.Groups[1].Value + (int.Parse(match.Groups[2].Value) + 1) : latest;
    }

    /// <summary>新しい箱を、いちばん上に作ります。</summary>
    public WorldArchive AddNewArchive()
    {
        var archive = new WorldArchive("新しい箱（名前を直してください）", Array.Empty<WorldEntry>());
        Archives.Insert(0, archive);
        return archive;
    }

    public void Move(WorldArchive archive, WorldEntry entry, int offset)
    {
        var from = archive.Entries.IndexOf(entry);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= archive.Entries.Count) return;
        archive.Entries.Move(from, to);
    }

    /// <summary>いちばん新しい版。</summary>
    public string LatestVersion =>
        Archives.FirstOrDefault()?.Entries.FirstOrDefault()?.Version.Trim() ?? "";

    public bool CanAlignVersion =>
        CurrentVersion != null && LatestVersion.Length > 0 && CurrentVersion.Value.Trim() != LatestVersion;

    public string VersionSummary
    {
        get
        {
            if (CurrentVersion == null || LatestVersion.Length == 0) return "";
            return CanAlignVersion
                ? $"更新履歴の一番上は「{LatestVersion}」ですが、ページの表記は「{CurrentVersion.Value.Trim()}」です。"
                : $"更新履歴とページの表記は、どちらも「{LatestVersion}」でそろっています。";
        }
    }

    public void AlignVersion()
    {
        if (CurrentVersion != null && LatestVersion.Length > 0) CurrentVersion.Value = LatestVersion;
    }

    // --- 書き戻し -----------------------------------------------------------

    public string Rebuild() => Block.Replace(file.Text,
        match => match.Groups[1].Value + BuildBlock() + match.Groups[3].Value, 1);

    private string BuildBlock()
    {
        var inner = archiveIndent + "  ";
        var entryInner = entryIndent + "  ";
        var builder = new StringBuilder();

        foreach (var archive in Archives)
        {
            builder.Append(newline).Append(archiveIndent).Append("<details class=\"update-history-archive\">");
            builder.Append(newline).Append(inner).Append("<summary>")
                   .Append(archive.Summary.Trim()).Append("</summary>");
            builder.Append(newline).Append(inner).Append("<div class=\"update-history-entries\">");

            foreach (var entry in archive.Entries)
            {
                builder.Append(newline).Append(entryIndent).Append("<article class=\"update-entry\">");
                builder.Append(newline).Append(entryInner).Append("<h3>").Append(entry.Version.Trim())
                       .Append(" <time datetime=\"").Append(entry.Date.Trim()).Append("\">")
                       .Append(entry.Display).Append("</time></h3>");

                var lines = entry.ItemLines();
                if (entry.AsList)
                {
                    builder.Append(newline).Append(entryInner).Append("<ul>");
                    foreach (var line in lines)
                        builder.Append(newline).Append(entryIndent).Append("    <li>").Append(line).Append("</li>");
                    builder.Append(newline).Append(entryInner).Append("</ul>");
                }
                else
                {
                    foreach (var line in lines)
                        builder.Append(newline).Append(entryInner).Append("<p>").Append(line).Append("</p>");
                }

                builder.Append(newline).Append(entryIndent).Append("</article>");
            }

            builder.Append(newline).Append(inner).Append("</div>");
            builder.Append(newline).Append(archiveIndent).Append("</details>");
        }
        return builder.ToString();
    }

    private List<(string, List<(string, string, string)>)> Snapshots() => Archives
        .Select(archive => (archive.Summary.Trim(),
            archive.Entries.Select(entry => entry.Snapshot()).ToList()))
        .ToList();

    public bool HasChanges
    {
        get
        {
            return Fields.Any(field => field.Changed) || ArchivesChanged;
        }
    }

    public bool HasError => Archives.Any(archive => archive.HasError) ||
        Fields.Any(field => field.HasError);

    public IReadOnlyList<ChangeRow> Changes()
    {
        if (!HasChanges) return Array.Empty<ChangeRow>();

        var now = Snapshots();
        var rows = new List<ChangeRow>();
        foreach (var field in Fields.Where(field => field.Changed))
            rows.Add(new ChangeRow($"HALKA WORLD：{field.Label}", field.Original, field.Value, FileRelative));
        var nowVersions = now.SelectMany(archive => archive.Item2).Select(entry => entry.Item1).ToArray();
        var savedVersions = saved.SelectMany(archive => archive.Item2).Select(entry => entry.Item1).ToArray();

        foreach (var version in nowVersions.Except(savedVersions))
            rows.Add(new ChangeRow("HALKA WORLD：更新履歴を追加", "", version, FileRelative));
        foreach (var version in savedVersions.Except(nowVersions))
            rows.Add(new ChangeRow("HALKA WORLD：更新履歴を削除", version, "", FileRelative));
        if (rows.Count == 0)
            rows.Add(new ChangeRow("HALKA WORLD：更新履歴を変更",
                $"{savedVersions.Length}件", $"{nowVersions.Length}件", FileRelative));
        return rows;
    }

    internal void Apply()
    {
        foreach (var field in Fields) field.Apply();
        // 更新履歴は、上の欄を書いたあとのファイルから組み立て直します（どちらも残るように）。
        if (ArchivesChanged) file.SetText(Rebuild());
    }

    private bool ArchivesChanged
    {
        get
        {
            var now = Snapshots();
            if (now.Count != saved.Count) return true;
            for (var i = 0; i < now.Count; i++)
            {
                if (now[i].Item1 != saved[i].Item1) return true;
                if (!now[i].Item2.SequenceEqual(saved[i].Item2)) return true;
            }
            return false;
        }
    }

    internal void MarkSaved()
    {
        foreach (var field in Fields) field.MarkSaved();
        saved = Snapshots();
    }

    public void Revert()
    {
        foreach (var field in Fields) field.Revert();

        var block = Block.Match(file.Text);
        Archives.Clear();
        if (block.Success)
            foreach (var archive in Parse(block.Groups[2].Value)) Archives.Add(archive);
        saved = Snapshots();
    }
}
