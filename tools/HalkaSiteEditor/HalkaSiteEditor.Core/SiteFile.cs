using System.Security.Cryptography;
using System.Text;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 編集対象のテキストファイル1つ。
/// このリポジトリは改行コードがファイルごとに違う（index.html は CRLF、style.css は LF）ため、
/// 改行の正規化は一切せず、値の部分だけを差し替えて書き戻します。
/// </summary>
public sealed class SiteFile
{
    private static readonly UTF8Encoding NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public string Path { get; }
    public string Text { get; private set; }
    public bool Bom { get; }
    public bool Dirty { get; private set; }

    private string loadedHash;

    private SiteFile(string path, string text, bool bom, string hash)
    {
        Path = path;
        Text = text;
        Bom = bom;
        loadedHash = hash;
    }

    public static SiteFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var offset = bom ? 3 : 0;
        var text = NoBom.GetString(bytes, offset, bytes.Length - offset);
        return new SiteFile(path, text, bom, Hash(bytes));
    }

    public void SetText(string text)
    {
        if (string.Equals(Text, text, StringComparison.Ordinal)) return;
        Text = text;
        Dirty = true;
    }

    /// <summary>読み込んだあとに、VS Code など別の場所でファイルが変わっていないか。</summary>
    public bool ChangedOnDisk() => !File.Exists(Path) || Hash(File.ReadAllBytes(Path)) != loadedHash;

    /// <summary>一時ファイルへ書いてから置き換えます。途中で落ちても元ファイルが壊れません。</summary>
    public void Save()
    {
        if (!Dirty) return;
        var temp = Path + ".halkasiteeditor.tmp";
        File.WriteAllText(temp, Text, Bom ? WithBom : NoBom);
        File.Move(temp, Path, overwrite: true);
        loadedHash = Hash(File.ReadAllBytes(Path));
        Dirty = false;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
