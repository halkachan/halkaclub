using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// &lt;br /&gt; で改行している1つの段落を、1行＝1行の文字として編集できるようにします。
/// 正規表現は3つのグループを持ち、2番目が差し替える範囲です（前後はそのまま残します）。
/// 字下げと閉じ括弧の位置は読み込んだときの形を覚えるので、触らなければ1文字も変わりません。
/// </summary>
public sealed class BrText : INotifyPropertyChanged
{
    private readonly SiteFile file;
    private readonly Regex pattern;
    private readonly string newline;
    private readonly string indent;
    private readonly string closeIndent;
    private string text;

    public string Label { get; }
    public string Hint { get; }
    public string FileRelative { get; }
    public string Original { get; private set; }

    private BrText(SiteFile file, string fileRelative, string label, string hint, Regex pattern,
        string original, string newline, string indent, string closeIndent)
    {
        this.file = file;
        this.pattern = pattern;
        this.newline = newline;
        this.indent = indent;
        this.closeIndent = closeIndent;
        FileRelative = fileRelative;
        Label = label;
        Hint = hint;
        Original = original;
        text = original;
    }

    /// <summary>見つからなければ null（そのページに無い場合）。</summary>
    public static BrText? Create(SiteFile file, string fileRelative, string label, string hint, Regex pattern)
    {
        var match = pattern.Match(file.Text);
        if (!match.Success) return null;

        var inner = match.Groups[2].Value;
        var newline = inner.Contains("\r\n") ? "\r\n" : "\n";
        var lines = Regex.Split(inner, @"<br />\r?\n|\r?\n")
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

        // 1行目の字下げと、閉じ括弧の前の字下げを覚えます。
        var first = Regex.Match(inner, @"^\r?\n([ \t]*)");
        var last = Regex.Match(inner, @"\r?\n([ \t]*)$");
        return new BrText(file, fileRelative, label, hint, pattern, string.Join("\n", lines), newline,
            first.Success ? first.Groups[1].Value : "", last.Success ? last.Groups[1].Value : "");
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
            foreach (var line in Lines())
            {
                var problem = HtmlText.Validate(line);
                if (problem != null) return problem;
            }
            return null;
        }
    }

    public bool HasError => Error != null;

    public IReadOnlyList<string> Lines() => Text.Split('\n')
        .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    public void Revert() => Text = Original;

    internal void Apply()
    {
        if (!Changed) return;
        file.SetText(pattern.Replace(file.Text, match =>
            match.Groups[1].Value + Build() + match.Groups[3].Value, 1));
    }

    private string Build()
    {
        var lines = Lines();
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            builder.Append(newline).Append(indent).Append(lines[i]);
            if (i < lines.Count - 1) builder.Append("<br />");
        }
        return builder.Append(newline).Append(closeIndent).ToString();
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
