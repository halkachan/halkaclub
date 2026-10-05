using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>作品一覧の1件。</summary>
public sealed class WorkItem : INotifyPropertyChanged
{
    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}$");

    private string title = "";
    private string publishedAt = "";
    private string youTubeUrl = "";

    public WorkItem(string title, string publishedAt, string youTubeUrl)
    {
        this.title = title;
        this.publishedAt = publishedAt;
        this.youTubeUrl = youTubeUrl;
    }

    public string Title
    {
        get => title;
        set { if (Set(ref title, value)) RaiseAll(); }
    }

    public string PublishedAt
    {
        get => publishedAt;
        set { if (Set(ref publishedAt, value)) RaiseAll(); }
    }

    public string YouTubeUrl
    {
        get => youTubeUrl;
        set { if (Set(ref youTubeUrl, value)) RaiseAll(); }
    }

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return "タイトルを入れてください。";
            if (Title.Contains('\n') || Title.Contains('\r')) return "タイトルは1行で入れてください。";
            if (!DatePattern.IsMatch(PublishedAt.Trim())) return "投稿日は 2026-01-23 の形で入れてください。";
            // 同名のプロパティがあるので、判定クラスは名前空間つきで呼びます。
            if (HalkaSiteEditor.Core.YouTubeUrl.ExtractId(youTubeUrl) is null)
                return "YouTubeのURLとして読み取れません。";
            return null;
        }
    }

    public bool HasError => Error != null;

    public (string, string, string) Snapshot() => (Title, PublishedAt.Trim(), YouTubeUrl.Trim());

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set(ref string storage, string? value, [CallerMemberName] string? name = null)
    {
        var next = value ?? "";
        if (string.Equals(storage, next, StringComparison.Ordinal)) return false;
        storage = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void RaiseAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
    }
}

/// <summary>作品一覧の分類（歌ってみた・ひらがな一文字シリーズ など）。</summary>
public sealed class WorkCategory
{
    public string Name { get; }
    public ObservableCollection<WorkItem> Works { get; }

    internal WorkCategory(string name, IEnumerable<WorkItem> works)
    {
        Name = name;
        Works = new ObservableCollection<WorkItem>(works);
    }

    public override string ToString() => $"{Name}（{Works.Count}件）";

    public WorkItem AddNew()
    {
        var item = new WorkItem("新しい作品", DateTime.Now.ToString("yyyy-MM-dd"), "");
        Works.Add(item);
        return item;
    }

    public void Move(WorkItem item, int offset)
    {
        var from = Works.IndexOf(item);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= Works.Count) return;
        Works.Move(from, to);
    }
}

/// <summary>
/// works/works-data.js の読み書き。
/// 分類の id・name・description や、ファイル先頭の説明コメントには触れず、
/// 各分類の works 配列の中身だけを組み立て直します。
/// </summary>
public sealed class WorksDocument
{
    // 各分類の works 配列（    works: [ から 4つ空けた ], まで）。
    private static readonly Regex ArrayBlock = new(@"    works: \[[\s\S]*?\r?\n    \],", RegexOptions.CultureInvariant);
    private static readonly Regex NameLine = new(@"    name: ""((?:[^""\\]|\\.)*)"",", RegexOptions.CultureInvariant);
    private static readonly Regex ItemPattern = new(
        @"\{\s*title:\s*""((?:[^""\\]|\\.)*)"",\s*publishedAt:\s*""([^""]*)"",\s*youtubeUrl:\s*""([^""]*)"",?\s*\}",
        RegexOptions.CultureInvariant);

    private readonly SiteFile file;
    private readonly string newline;
    private readonly string originalText;
    private List<List<(string, string, string)>> originalSnapshots;

    public string FileRelative { get; }
    public IReadOnlyList<WorkCategory> Categories { get; private set; }

    private WorksDocument(SiteFile file, string fileRelative, IReadOnlyList<WorkCategory> categories)
    {
        this.file = file;
        FileRelative = fileRelative;
        Categories = categories;
        originalText = file.Text;
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        originalSnapshots = Snapshots();
    }

    public static WorksDocument Load(SiteFile file, string fileRelative) =>
        new(file, fileRelative, Parse(file.Text));

    private static IReadOnlyList<WorkCategory> Parse(string text)
    {
        var names = NameLine.Matches(text).Select(m => Unescape(m.Groups[1].Value)).ToArray();
        var blocks = ArrayBlock.Matches(text);
        if (names.Length != blocks.Count)
        {
            throw new InvalidDataException(
                $"works-data.js の形が想定と違います（分類名 {names.Length} 個 / works配列 {blocks.Count} 個）。");
        }

        var categories = new List<WorkCategory>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var items = ItemPattern.Matches(blocks[i].Value).Select(m => new WorkItem(
                Unescape(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value));
            categories.Add(new WorkCategory(names[i], items));
        }
        return categories;
    }

    /// <summary>いまの中身でファイル全体の文字列を作ります。配列の外は元のままです。</summary>
    public string Serialize()
    {
        var index = 0;
        return ArrayBlock.Replace(originalText, _ => Block(Categories[index++]));
    }

    private string Block(WorkCategory category)
    {
        if (category.Works.Count == 0) return "    works: [],";

        var builder = new StringBuilder("    works: [");
        foreach (var work in category.Works)
        {
            builder.Append(newline).Append("      {");
            builder.Append(newline).Append("        title: \"").Append(Escape(work.Title)).Append("\",");
            builder.Append(newline).Append("        publishedAt: \"").Append(work.PublishedAt.Trim()).Append("\",");
            builder.Append(newline).Append("        youtubeUrl: \"").Append(work.YouTubeUrl.Trim()).Append("\",");
            builder.Append(newline).Append("      },");
        }
        builder.Append(newline).Append("    ],");
        return builder.ToString();
    }

    private List<List<(string, string, string)>> Snapshots() =>
        Categories.Select(category => category.Works.Select(work => work.Snapshot()).ToList()).ToList();

    public bool HasChanges => !Snapshots()
        .Select((list, i) => list.SequenceEqual(originalSnapshots[i]))
        .All(same => same);

    public bool HasError => Categories.Any(category => category.Works.Any(work => work.HasError));

    public IReadOnlyList<ChangeRow> Changes()
    {
        var rows = new List<ChangeRow>();
        var now = Snapshots();

        for (var i = 0; i < Categories.Count; i++)
        {
            var before = originalSnapshots[i];
            var after = now[i];
            if (before.SequenceEqual(after)) continue;

            var name = Categories[i].Name;
            var added = after.Select(work => work.Item1).Except(before.Select(work => work.Item1)).ToArray();
            var removed = before.Select(work => work.Item1).Except(after.Select(work => work.Item1)).ToArray();

            foreach (var title in added) rows.Add(new ChangeRow($"{name}：作品を追加", "", title, FileRelative));
            foreach (var title in removed) rows.Add(new ChangeRow($"{name}：作品を削除", title, "", FileRelative));
            if (added.Length == 0 && removed.Length == 0)
                rows.Add(new ChangeRow($"{name}：内容または並び順を変更",
                    $"{before.Count}件", $"{after.Count}件", FileRelative));
        }
        return rows;
    }

    internal void Apply()
    {
        if (HasChanges) file.SetText(Serialize());
    }

    internal void MarkSaved() => originalSnapshots = Snapshots();

    public void Revert()
    {
        Categories = Parse(originalText);
        originalSnapshots = Snapshots();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Unescape(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");
}
