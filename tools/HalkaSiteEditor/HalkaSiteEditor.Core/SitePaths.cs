namespace HalkaSiteEditor.Core;

/// <summary>halkaclub リポジトリの場所と、編集対象ファイルの位置を決めます。</summary>
public static class SitePaths
{
    /// <summary>サイトのルートかどうかは、この3つが揃っているかで判定します。</summary>
    public static bool IsSiteRoot(string path) =>
        File.Exists(Path.Combine(path, "CNAME")) &&
        File.Exists(Path.Combine(path, "index.html")) &&
        File.Exists(Path.Combine(path, "style.css"));

    /// <summary>EXEの位置から上へ辿ってサイトのルートを探します。見つからなければ null。</summary>
    public static string? FindNear(string start)
    {
        var current = new DirectoryInfo(start);
        for (var i = 0; current != null && i < 12; i++, current = current.Parent)
        {
            if (IsSiteRoot(current.FullName)) return current.FullName;
            var child = Path.Combine(current.FullName, "halkaclub");
            if (IsSiteRoot(child)) return child;
        }
        return null;
    }

    public static string IndexHtml(string root) => Path.Combine(root, "index.html");
    public static string WorksData(string root) => Path.Combine(root, "works", "works-data.js");
    public static string ClubHtml(string root) => Path.Combine(root, "club", "index.html");
    public static string UtamazeVersion(string root) => Path.Combine(root, "utamaze", "version.json");
    public static string UtamazePage(string root) => Path.Combine(root, "utamaze", "index.html");
    public static string StyleCss(string root) => Path.Combine(root, "style.css");
    public static string ScriptJs(string root) => Path.Combine(root, "script.js");
    public static string CommissionJa(string root) => Path.Combine(root, "commission", "index.html");
    public static string CommissionEn(string root) => Path.Combine(root, "commission", "en", "index.html");

    /// <summary>ルートからの相対パスを、画面表示用に "/" 区切りで返します。</summary>
    public static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
