using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 連なった &lt;p&gt; を、1行＝1段落の文字として編集できるようにします。
/// 正規表現は3つのグループを持ち、2番目が差し替える範囲です（前後はそのまま残します）。
/// </summary>
public sealed class ParagraphRun : INotifyPropertyChanged
{
    private static readonly Regex Item = new(@"([ \t]*)(<p>[\s\S]*?</p>)\r?\n");

    private readonly SiteFile file;
    private readonly Regex pattern;
    private string text;
    private string indent;

    public string Label { get; }
    public string Hint { get; }
    public string FileRelative { get; }
    public string Original { get; private set; }

    private ParagraphRun(SiteFile file, string fileRelative, string label, string hint,
        Regex pattern, string original, string indent)
    {
        this.file = file;
        this.pattern = pattern;
        this.indent = indent;
        FileRelative = fileRelative;
        Label = label;
        Hint = hint;
        Original = original;
        text = original;
    }

    /// <summary>見つからなければ null（そのページに無い場合）。</summary>
    public static ParagraphRun? Create(SiteFile file, string fileRelative, string label, string hint, Regex pattern)
    {
        var match = pattern.Match(file.Text);
        if (!match.Success) return null;

        var items = Item.Matches(match.Groups[2].Value);
        var indent = items.Count > 0 ? items[0].Groups[1].Value : "        ";
        var value = string.Join("\n", items.Select(item => Flatten(Inner(item.Groups[2].Value))));
        return new ParagraphRun(file, fileRelative, label, hint, pattern, value, indent);
    }

    private static string Inner(string element)
    {
        var open = element.IndexOf('>');
        var close = element.LastIndexOf("</", StringComparison.Ordinal);
        return open < 0 || close < 0 ? element : element[(open + 1)..close];
    }

    private static string Flatten(string inner) =>
        Regex.Replace(inner, @"\r?\n[ \t]*", "").Replace("<br />", "\n").Trim();

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
            if (Text.AsSpan().IndexOfAny('<', '>') >= 0) return "< と > は使えません。";
            if (Text.Contains('&')) return "& は使えません。";
            return null;
        }
    }

    public bool HasError => Error != null;

    public void Revert() => Text = Original;

    internal void Apply()
    {
        if (!Changed) return;
        file.SetText(pattern.Replace(file.Text, match =>
        {
            var builder = new StringBuilder(match.Groups[1].Value);
            foreach (var line in Text.Split('\n'))
                builder.Append(indent).Append("<p>").Append(line).Append("</p>\n");
            return builder.Append(match.Groups[3].Value).ToString();
        }, 1));
    }

    internal void MarkSaved()
    {
        Original = Text;
        Raise(nameof(Original));
        Raise(nameof(Changed));
    }

    public ChangeRow? Change() => Changed
        ? new ChangeRow(Label, Summary(Original), Summary(Text), FileRelative)
        : null;

    private static string Summary(string value)
    {
        var single = value.Replace("\n", " / ");
        return single.Length <= 40 ? single : single[..40] + "…";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
