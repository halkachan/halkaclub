using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HalkaSiteEditor.Core;

public enum FieldKind
{
    /// <summary>HTMLの中に入る文字。&lt; &gt; &amp; を弾きます。</summary>
    HtmlText,
    /// <summary>style.css に書く色。</summary>
    CssColor,
    /// <summary>YouTubeの動画URL。</summary>
    YouTubeUrl,
}

/// <summary>画面の入力欄1つ。ファイル内の1か所と結びついています。</summary>
public sealed class EditField : INotifyPropertyChanged
{
    private readonly SiteFile file;
    private readonly ValueSlot slot;
    private string value;
    private string? error;

    public string Id { get; }
    public string Label { get; }
    public FieldKind Kind { get; }
    public string FileRelative { get; }
    public string Original { get; private set; }

    internal EditField(SiteFile file, string fileRelative, ValueSlot slot, string id, string label, FieldKind kind)
    {
        this.file = file;
        this.slot = slot;
        Id = id;
        Label = label;
        Kind = kind;
        FileRelative = fileRelative;
        Original = slot.Read(file.Text);
        value = Original;
    }

    public string Value
    {
        get => value;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(this.value, next, StringComparison.Ordinal)) return;
            this.value = next;
            Error = Validate(next);
            Raise();
            Raise(nameof(Changed));
            Raise(nameof(SwatchHex));
        }
    }

    public string? Error
    {
        get => error;
        private set
        {
            if (string.Equals(error, value, StringComparison.Ordinal)) return;
            error = value;
            Raise();
            Raise(nameof(HasError));
        }
    }

    public bool HasError => Error != null;
    public bool Changed => !string.Equals(Value, Original, StringComparison.Ordinal);

    /// <summary>色の欄だけ、画面に出す色見本。</summary>
    public string? SwatchHex => Kind == FieldKind.CssColor ? CssColor.ToSwatchHex(Value) : null;

    private string? Validate(string candidate) => Kind switch
    {
        FieldKind.CssColor => CssColor.IsValid(candidate)
            ? null
            : "色の書き方が違います（例：#ffdc18、rgba(232, 84, 84, 0.22)）。",
        FieldKind.YouTubeUrl => YouTubeUrl.ExtractId(candidate) is null
            ? "YouTubeのURLとして読み取れません（youtu.be / watch?v= / shorts のいずれか）。"
            : null,
        _ => HtmlText.Validate(candidate),
    };

    /// <summary>入力値をファイルの文字列へ反映します（保存はまだしません）。</summary>
    internal void Apply()
    {
        if (!Changed) return;
        file.SetText(slot.Write(file.Text, Value));
    }

    internal void MarkSaved()
    {
        Original = Value;
        Raise(nameof(Original));
        Raise(nameof(Changed));
    }

    public void Revert() => Value = Original;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>日本語版と英語版で対になっている欄。片方を直したらもう片方も、を忘れないための組です。</summary>
public sealed class FieldPair
{
    public string Label { get; }
    public EditField Ja { get; }
    public EditField En { get; }

    public FieldPair(string label, EditField ja, EditField en)
    {
        Label = label;
        Ja = ja;
        En = en;
    }

    /// <summary>日本語側の金額から英語側を作れるか。</summary>
    public bool CanSyncEnglish => PriceText.ToEnglish(Ja.Value) != null;

    /// <summary>日本語側の数字を英語表記へ写します。</summary>
    public void SyncEnglishFromJapanese()
    {
        var english = PriceText.ToEnglish(Ja.Value);
        if (english != null) En.Value = english;
    }
}

/// <summary>画面のひとかたまり（タブ1枚ぶん）。</summary>
public sealed class EditGroup
{
    public string Title { get; }
    public string Note { get; }
    public IReadOnlyList<EditField> Fields { get; }
    public IReadOnlyList<FieldPair> Pairs { get; }

    public EditGroup(string title, string note, IReadOnlyList<EditField> fields, IReadOnlyList<FieldPair>? pairs = null)
    {
        Title = title;
        Note = note;
        Fields = fields;
        Pairs = pairs ?? Array.Empty<FieldPair>();
    }
}

/// <summary>変更一覧の1行。</summary>
public sealed record ChangeRow(string Label, string Before, string After, string File);
