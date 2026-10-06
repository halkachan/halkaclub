using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>トップページ「うれしいこと」の1件。</summary>
public sealed class NewsItem : INotifyPropertyChanged
{
    private string date = "";
    private string title = "";
    private string description = "";
    private string achievements = "";

    public NewsItem(string date, string title, string description, string achievements)
    {
        this.date = date;
        this.title = title;
        this.description = description;
        this.achievements = achievements;
    }

    /// <summary>「2/21投稿」「4/25〜4/26開催」のような、書き方の決まっていない日付。</summary>
    public string Date
    {
        get => date;
        set { if (Set(ref date, value)) RaiseAll(); }
    }

    public string Title
    {
        get => title;
        set { if (Set(ref title, value)) RaiseAll(); }
    }

    /// <summary>空なら、その行ごと出しません。</summary>
    public string Description
    {
        get => description;
        set { if (Set(ref description, value)) RaiseAll(); }
    }

    /// <summary>受賞・参加など。1行＝1つ。空なら、その箱ごと出しません。</summary>
    public string Achievements
    {
        get => achievements;
        set { if (Set(ref achievements, value)) RaiseAll(); }
    }

    public string? Error
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Date)) return "日付を入れてください（「2/21投稿」のような書き方で構いません）。";
            if (string.IsNullOrWhiteSpace(Title)) return "見出しを入れてください。";
            foreach (var value in new[] { Date, Title, Description, Achievements })
            {
                if (value.AsSpan().IndexOfAny('<', '>') >= 0) return "< と > は使えません。";
                if (value.Contains('&')) return "& は使えません。";
            }
            if (Date.Contains('\n') || Title.Contains('\n') || Description.Contains('\n'))
                return "日付・見出し・説明は1行で入れてください。";
            return null;
        }
    }

    public bool HasError => Error != null;

    public (string, string, string, string) Snapshot() =>
        (Date.Trim(), Title.Trim(), Description.Trim(), Achievements.Trim());

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Error)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
    }
}

/// <summary>
/// トップページ「うれしいこと」の読み書き。
/// `news-list` の中身だけを組み立て直すので、見出しやページの他の場所には触れません。
/// </summary>
public sealed class NewsDocument
{
    private static readonly Regex ListBlock =
        new(@"(<div class=""news-list"">)([\s\S]*?)(\r?\n[ \t]*</div>)", RegexOptions.CultureInvariant);
    private static readonly Regex Card =
        new(@"<article class=""news-card"">([\s\S]*?)</article>", RegexOptions.CultureInvariant);
    private static readonly Regex DateLine = new(@"<p class=""news-date"">([\s\S]*?)</p>");
    private static readonly Regex TitleLine = new(@"<h2>([\s\S]*?)</h2>");
    private static readonly Regex DescLine = new(@"<p class=""news-desc"">([\s\S]*?)</p>");
    private static readonly Regex AchieveItem = new(@"<li><mark>([\s\S]*?)</mark></li>");

    private readonly SiteFile file;
    private readonly string newline;
    private List<(string, string, string, string)> saved;

    public string FileRelative { get; }
    public ObservableCollection<NewsItem> Items { get; }

    private NewsDocument(SiteFile file, string fileRelative, IEnumerable<NewsItem> items)
    {
        this.file = file;
        FileRelative = fileRelative;
        Items = new ObservableCollection<NewsItem>(items);
        newline = file.Text.Contains("\r\n") ? "\r\n" : "\n";
        saved = Snapshots();
    }

    /// <summary>その欄が無いページなら null。</summary>
    public static NewsDocument? Load(SiteFile file, string fileRelative)
    {
        var block = ListBlock.Match(file.Text);
        if (!block.Success) return null;
        return new NewsDocument(file, fileRelative, Parse(block.Groups[2].Value));
    }

    private static IEnumerable<NewsItem> Parse(string inner) =>
        Card.Matches(inner).Select(card =>
        {
            var body = card.Groups[1].Value;
            return new NewsItem(
                Flatten(DateLine.Match(body).Groups[1].Value),
                Flatten(TitleLine.Match(body).Groups[1].Value),
                Flatten(DescLine.Match(body).Groups[1].Value),
                string.Join("\n", AchieveItem.Matches(body).Select(item => Flatten(item.Groups[1].Value))));
        });

    private static string Flatten(string inner) =>
        Regex.Replace(inner, @"\r?\n[ \t]*", "").Replace("<br />", "\n").Trim();

    public NewsItem AddNew()
    {
        var item = new NewsItem(DateTime.Now.ToString("M/d"), "新しいお知らせ", "", "");
        Items.Add(item);
        return item;
    }

    public void Move(NewsItem item, int offset)
    {
        var from = Items.IndexOf(item);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= Items.Count) return;
        Items.Move(from, to);
    }

    /// <summary>いまの中身で、ファイル全体の文字を組み立て直します。</summary>
    public string Rebuild() => ListBlock.Replace(file.Text,
        match => match.Groups[1].Value + Block() + match.Groups[3].Value, 1);

    private string Block()
    {
        if (Items.Count == 0) return "";

        var builder = new StringBuilder();
        foreach (var item in Items)
        {
            if (builder.Length > 0) builder.Append(newline);
            builder.Append(newline).Append("            <article class=\"news-card\">");
            builder.Append(newline).Append("              <p class=\"news-date\">").Append(item.Date.Trim()).Append("</p>");
            builder.Append(newline).Append("              <h2>").Append(item.Title.Trim()).Append("</h2>");

            if (!string.IsNullOrWhiteSpace(item.Description))
                builder.Append(newline).Append("              <p class=\"news-desc\">")
                       .Append(item.Description.Trim()).Append("</p>");

            var lines = item.Achievements.Split('\n')
                .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
            if (lines.Length > 0)
            {
                builder.Append(newline).Append("              <ul class=\"news-achieve\">");
                foreach (var line in lines)
                    builder.Append(newline).Append("                <li><mark>").Append(line).Append("</mark></li>");
                builder.Append(newline).Append("              </ul>");
            }

            builder.Append(newline).Append("            </article>");
        }
        return builder.ToString();
    }

    private List<(string, string, string, string)> Snapshots() =>
        Items.Select(item => item.Snapshot()).ToList();

    public bool HasChanges => !Snapshots().SequenceEqual(saved);
    public bool HasError => Items.Any(item => item.HasError);

    public IReadOnlyList<ChangeRow> Changes()
    {
        if (!HasChanges) return Array.Empty<ChangeRow>();

        var now = Snapshots();
        var rows = new List<ChangeRow>();
        var added = now.Select(item => item.Item2).Except(saved.Select(item => item.Item2)).ToArray();
        var removed = saved.Select(item => item.Item2).Except(now.Select(item => item.Item2)).ToArray();

        foreach (var title in added) rows.Add(new ChangeRow("うれしいこと：追加", "", title, FileRelative));
        foreach (var title in removed) rows.Add(new ChangeRow("うれしいこと：削除", title, "", FileRelative));
        if (added.Length == 0 && removed.Length == 0)
            rows.Add(new ChangeRow("うれしいこと：内容または並び順を変更",
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
        var block = ListBlock.Match(file.Text);
        Items.Clear();
        if (block.Success)
            foreach (var item in Parse(block.Groups[2].Value)) Items.Add(item);
        saved = Snapshots();
    }
}
