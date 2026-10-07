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
public sealed class WorkCategory : INotifyPropertyChanged
{
    private static readonly Regex IdPattern = new(@"^[a-z0-9][a-z0-9-]*$");

    private string id;
    private string name;
    private string description;

    /// <summary>作品一覧ページが使う合い言葉。英小文字・数字・ハイフンだけです。</summary>
    public string Id
    {
        get => id;
        set { if (Set(ref id, value)) RaiseAll(); }
    }

    public string Name
    {
        get => name;
        set { if (Set(ref name, value)) RaiseAll(); }
    }

    public string Description
    {
        get => description;
        set { if (Set(ref description, value)) RaiseAll(); }
    }

    public ObservableCollection<WorkItem> Works { get; }

    internal WorkCategory(string id, string name, string description, IEnumerable<WorkItem> works)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        Works = new ObservableCollection<WorkItem>(works);
        Works.CollectionChanged += (_, _) => RaiseAll();
    }

    /// <summary>並びの中に出す1行。名前を変えたらその場で変わります。</summary>
    public string Display => $"{Name}（{Works.Count}件）";

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "分類の名前を入れてください。";
            if (!IdPattern.IsMatch(Id.Trim()))
                return "合い言葉は英小文字・数字・ハイフンだけで入れてください（例：hiragana）。";
            foreach (var value in new[] { Name, Description })
            {
                var problem = value.Length == 0 ? null : HtmlText.Validate(value);
                if (problem != null) return problem;
            }
            if (Name.Contains('\n') || Description.Contains('\n')) return "名前と説明は1行で入れてください。";
            return null;
        }
    }

    public bool HasError => Error != null;

    public (string, string, string) Snapshot() => (Id.Trim(), Name.Trim(), Description.Trim());

    public override string ToString() => Display;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set(ref string storage, string? value, [CallerMemberName] string? field = null)
    {
        var next = (value ?? "").Replace("\r\n", "\n");
        if (string.Equals(storage, next, StringComparison.Ordinal)) return false;
        storage = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(field));
        return true;
    }

    private void RaiseAll()
    {
        foreach (var raised in new[] { nameof(Display), nameof(Error), nameof(HasError) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(raised));
    }

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
    // const worksData = [ ... ]; の中身ぜんぶ。手前の説明コメントには触れません。
    private static readonly Regex DataBlock =
        new(@"(const worksData = \[)([\s\S]*?)(\r?\n\];)", RegexOptions.CultureInvariant);
    private static readonly Regex CategoryBlock = new(
        @"\{\s*id: ""((?:[^""\\]|\\.)*)"",\s*name: ""((?:[^""\\]|\\.)*)"",\s*" +
        @"description: ""((?:[^""\\]|\\.)*)"",\s*works: \[([\s\S]*?)\r?\n    \],\s*\},",
        RegexOptions.CultureInvariant);
    private static readonly Regex ItemPattern = new(
        @"\{\s*title:\s*""((?:[^""\\]|\\.)*)"",\s*publishedAt:\s*""([^""]*)"",\s*youtubeUrl:\s*""([^""]*)"",?\s*\}",
        RegexOptions.CultureInvariant);

    private readonly SiteFile file;
    private readonly string newline;
    private List<(string, string, string)> savedCategories;
    private List<List<(string, string, string)>> savedWorks;

    public string FileRelative { get; }
    public ObservableCollection<WorkCategory> Categories { get; }

    private WorksDocument(SiteFile file, string fileRelative, IEnumerable<WorkCategory> categories)
    {
        this.file = file;
        FileRelative = fileRelative;
        Categories = new ObservableCollection<WorkCategory>(categories);
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        savedCategories = CategorySnapshots();
        savedWorks = Snapshots();
    }

    public static WorksDocument Load(SiteFile file, string fileRelative) =>
        new(file, fileRelative, Parse(file.Text));

    private static IEnumerable<WorkCategory> Parse(string text)
    {
        var block = DataBlock.Match(text);
        if (!block.Success)
            throw new InvalidDataException("works-data.js の形が想定と違います（worksData が見つかりません）。");

        return CategoryBlock.Matches(block.Groups[2].Value).Select(category => new WorkCategory(
            Unescape(category.Groups[1].Value),
            Unescape(category.Groups[2].Value),
            Unescape(category.Groups[3].Value),
            ItemPattern.Matches(category.Groups[4].Value).Select(item => new WorkItem(
                Unescape(item.Groups[1].Value), item.Groups[2].Value, item.Groups[3].Value))));
    }

    /// <summary>分類を一番下に足します。</summary>
    public WorkCategory AddNewCategory()
    {
        var category = new WorkCategory(UnusedId(), "新しい分類", "", Array.Empty<WorkItem>());
        Categories.Add(category);
        return category;
    }

    private string UnusedId()
    {
        for (var i = 1; ; i++)
        {
            var candidate = i == 1 ? "new-series" : $"new-series-{i}";
            if (Categories.All(category => category.Id != candidate)) return candidate;
        }
    }

    public void MoveCategory(WorkCategory category, int offset)
    {
        var from = Categories.IndexOf(category);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= Categories.Count) return;
        Categories.Move(from, to);
    }

    /// <summary>いまの中身でファイル全体の文字列を作ります。手前の説明コメントは元のままです。</summary>
    public string Serialize() => DataBlock.Replace(file.Text,
        match => match.Groups[1].Value + Block() + match.Groups[3].Value, 1);

    private string Block()
    {
        var builder = new StringBuilder();
        foreach (var category in Categories)
        {
            builder.Append(newline).Append("  {");
            builder.Append(newline).Append("    id: \"").Append(Escape(category.Id.Trim())).Append("\",");
            builder.Append(newline).Append("    name: \"").Append(Escape(category.Name.Trim())).Append("\",");
            builder.Append(newline).Append("    description: \"")
                   .Append(Escape(category.Description.Trim())).Append("\",");

            if (category.Works.Count == 0)
            {
                builder.Append(newline).Append("    works: [],");
            }
            else
            {
                builder.Append(newline).Append("    works: [");
                foreach (var work in category.Works)
                {
                    builder.Append(newline).Append("      {");
                    builder.Append(newline).Append("        title: \"").Append(Escape(work.Title)).Append("\",");
                    builder.Append(newline).Append("        publishedAt: \"").Append(work.PublishedAt.Trim()).Append("\",");
                    builder.Append(newline).Append("        youtubeUrl: \"").Append(work.YouTubeUrl.Trim()).Append("\",");
                    builder.Append(newline).Append("      },");
                }
                builder.Append(newline).Append("    ],");
            }

            builder.Append(newline).Append("  },");
        }
        return builder.ToString();
    }

    private List<(string, string, string)> CategorySnapshots() =>
        Categories.Select(category => category.Snapshot()).ToList();

    private List<List<(string, string, string)>> Snapshots() =>
        Categories.Select(category => category.Works.Select(work => work.Snapshot()).ToList()).ToList();

    public bool HasChanges =>
        !CategorySnapshots().SequenceEqual(savedCategories) ||
        !Snapshots().Select((list, i) => i < savedWorks.Count && list.SequenceEqual(savedWorks[i])).All(same => same);

    public bool HasError => Categories.Any(category => category.HasError) ||
        Categories.Any(category => category.Works.Any(work => work.HasError)) ||
        Categories.Select(category => category.Id.Trim()).Distinct(StringComparer.Ordinal).Count() != Categories.Count;

    public IReadOnlyList<ChangeRow> Changes()
    {
        var rows = new List<ChangeRow>();
        var now = Snapshots();

        // 分類そのものの増減と、名前の変更。
        var categories = CategorySnapshots();
        foreach (var added in categories.Select(category => category.Item1)
                     .Except(savedCategories.Select(category => category.Item1)))
            rows.Add(new ChangeRow("作品一覧：分類を追加", "", Categories.First(c => c.Id.Trim() == added).Name, FileRelative));
        foreach (var removed in savedCategories.Where(category =>
                     categories.All(now2 => now2.Item1 != category.Item1)))
            rows.Add(new ChangeRow("作品一覧：分類を削除", removed.Item2, "", FileRelative));
        foreach (var before in savedCategories)
        {
            var after = categories.FirstOrDefault(category => category.Item1 == before.Item1);
            if (after == default || after == before) continue;
            rows.Add(new ChangeRow("作品一覧：分類を変更", before.Item2, after.Item2, FileRelative));
        }
        if (categories.Count == savedCategories.Count &&
            categories.Select(category => category.Item1)
                .SequenceEqual(savedCategories.Select(category => category.Item1)) == false &&
            categories.Select(category => category.Item1).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(savedCategories.Select(category => category.Item1)
                    .OrderBy(id => id, StringComparer.Ordinal)))
            rows.Add(new ChangeRow("作品一覧：分類の並び順を変更", "", "", FileRelative));

        for (var i = 0; i < Categories.Count; i++)
        {
            var before = savedCategories.FindIndex(category => category.Item1 == Categories[i].Id.Trim());
            if (before < 0 || before >= savedWorks.Count) continue;
            var after = now[i];
            if (savedWorks[before].SequenceEqual(after)) continue;

            var name = Categories[i].Name;
            var beforeWorks = savedWorks[before];
            var added = after.Select(work => work.Item1).Except(beforeWorks.Select(work => work.Item1)).ToArray();
            var removed = beforeWorks.Select(work => work.Item1).Except(after.Select(work => work.Item1)).ToArray();

            foreach (var title in added) rows.Add(new ChangeRow($"{name}：作品を追加", "", title, FileRelative));
            foreach (var title in removed) rows.Add(new ChangeRow($"{name}：作品を削除", title, "", FileRelative));
            if (added.Length == 0 && removed.Length == 0)
                rows.Add(new ChangeRow($"{name}：内容または並び順を変更",
                    $"{beforeWorks.Count}件", $"{after.Count}件", FileRelative));
        }
        return rows;
    }

    internal void Apply()
    {
        if (HasChanges) file.SetText(Serialize());
    }

    internal void MarkSaved()
    {
        savedCategories = CategorySnapshots();
        savedWorks = Snapshots();
    }

    public void Revert()
    {
        Categories.Clear();
        foreach (var category in Parse(file.Text)) Categories.Add(category);
        MarkSaved();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Unescape(string value) => value.Replace("\\\"", "\"").Replace("\\\\", "\\");
}
