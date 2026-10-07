using System.ComponentModel;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 画像ファイルの形。拡張子ではなく中身の先頭を見て判断します
/// （.png という名前の別形式を取り違えないため）。
/// </summary>
public sealed record ImageInfo(string Format, int Width, int Height)
{
    public override string ToString() => $"{Format.ToUpperInvariant()} {Width}×{Height}";

    /// <summary>読めなければ null（対応していない形式）。</summary>
    public static ImageInfo? Read(string path)
    {
        byte[] head;
        try
        {
            using var stream = File.OpenRead(path);
            head = new byte[32];
            var read = stream.Read(head, 0, head.Length);
            if (read < 24) return null;
        }
        catch (Exception)
        {
            return null;
        }

        // PNG: 8バイトの印のあと IHDR。幅と高さは上位バイトが先。
        if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
            return new ImageInfo("png", BigEndian(head, 16), BigEndian(head, 20));

        // GIF: GIF87a / GIF89a のあと、幅と高さは下位バイトが先。
        if (head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46)
            return new ImageInfo("gif", head[6] | (head[7] << 8), head[8] | (head[9] << 8));

        return null;
    }

    private static int BigEndian(byte[] bytes, int at) =>
        (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
}

/// <summary>
/// 差し替えられる画像1つ。
/// 選んだだけでは何も起きません。「保存する」で写してから、読み込み側の `?v=` を上げます。
/// </summary>
public sealed class ImageSlot : INotifyPropertyChanged
{
    private string? sourcePath;

    public string Label { get; }
    /// <summary>サイトのフォルダーからの相対パス（`assets/ogp/home.png`）。</summary>
    public string Relative { get; }
    public string Path { get; }
    /// <summary>1200×630 でなければならないか（SNSのカード画像）。</summary>
    public bool CardSize { get; }
    public ImageInfo? Current { get; private set; }

    public ImageSlot(string root, string relative, string label, bool cardSize)
    {
        Relative = relative;
        Label = label;
        CardSize = cardSize;
        Path = System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Current = ImageInfo.Read(Path);
    }

    /// <summary>選んだファイル（まだ写していません）。</summary>
    public string? SourcePath
    {
        get => sourcePath;
        private set
        {
            if (string.Equals(sourcePath, value, StringComparison.Ordinal)) return;
            sourcePath = value;
            Source = value == null ? null : ImageInfo.Read(value);
            foreach (var name in new[] { nameof(SourcePath), nameof(Source), nameof(Changed),
                         nameof(Error), nameof(HasError), nameof(Summary) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public ImageInfo? Source { get; private set; }

    /// <summary>外で作り直されたので、番号だけ上げたい状態。</summary>
    public bool Regenerated { get; private set; }

    public bool Changed => SourcePath != null || Regenerated;

    /// <summary>ツールの外で画像が作り直されたときに呼びます（番号を上げるため）。</summary>
    public void MarkRegenerated()
    {
        Regenerated = true;
        Current = ImageInfo.Read(Path);
        foreach (var name in new[] { nameof(Regenerated), nameof(Current), nameof(Changed), nameof(Summary) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public string? Error
    {
        get
        {
            if (SourcePath == null) return null;
            if (!File.Exists(SourcePath)) return "そのファイルが見つかりません。";
            if (Source == null) return "PNG か GIF の画像を選んでください。";
            if (Current != null && Source.Format != Current.Format)
                return $"いまと同じ形式（{Current.Format.ToUpperInvariant()}）の画像を選んでください。";
            if (CardSize && (Source.Width != 1200 || Source.Height != 630))
                return $"カード画像は 1200×630 です（選んだものは {Source.Width}×{Source.Height}）。";
            return null;
        }
    }

    public bool HasError => Error != null;

    /// <summary>画面に出す1行。</summary>
    public string Summary => SourcePath != null
        ? $"{Current?.ToString() ?? "？"} → {Source?.ToString() ?? "読み取れません"}"
        : Regenerated
            ? $"{Current?.ToString() ?? "？"}（作り直しました）"
            : Current?.ToString() ?? "読み取れません";

    public void Choose(string path) => SourcePath = path;

    /// <summary>選び直しをやめます。作り直した印は残します（画像はもう入れ替わっているため）。</summary>
    public void Clear() => SourcePath = null;

    public ChangeRow? Change()
    {
        if (SourcePath != null)
        {
            return new ChangeRow($"{Label}：画像を差し替え", Current?.ToString() ?? "",
                System.IO.Path.GetFileName(SourcePath) ?? "", Relative);
        }
        return Regenerated
            ? new ChangeRow($"{Label}：作り直し", "", Current?.ToString() ?? "", Relative)
            : null;
    }

    /// <summary>読み込み側の `?v=` を上げます（写すのは保存の最後です）。</summary>
    internal void Bump(IEnumerable<SiteFile> files)
    {
        if (!Changed) return;
        foreach (var file in files) CacheBuster.Bump(file, Relative);
    }

    /// <summary>選んだファイルを実際に写します。</summary>
    internal void Copy()
    {
        Regenerated = false;
        if (SourcePath == null || HasError) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.Copy(SourcePath, Path, overwrite: true);
        Current = ImageInfo.Read(Path);
        SourcePath = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
