using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 保存の直前に、書き換わるファイルの控えを取ります。
/// 「保存したけれど、やっぱり前のほうがよかった」を1手で戻せるようにするためのものです。
/// 控えはサイトのフォルダーの外（%LOCALAPPDATA%）に置くので、公開には混ざりません。
/// </summary>
public sealed class SaveBackups
{
    /// <summary>これより古い控えは捨てます。</summary>
    public const int Keep = 10;

    private const string Stamp = "yyyyMMdd-HHmmss";

    /// <summary>どのサイトの控えかを書いておく目印。</summary>
    private const string Marker = "site.txt";

    public string Folder { get; }

    public SaveBackups(string folder) => Folder = folder;

    /// <summary>いつもの置き場所（%LOCALAPPDATA%\HalkaSiteEditor\backup）。</summary>
    public static SaveBackups For(string root)
    {
        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalkaSiteEditor", "backup");
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var key = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(full.ToLowerInvariant())))[..12];

        var backups = new SaveBackups(Path.Combine(baseFolder, key));
        try
        {
            Directory.CreateDirectory(backups.Folder);
            File.WriteAllText(Path.Combine(backups.Folder, Marker), full);
            Tidy(baseFolder);
        }
        catch (Exception)
        {
            // 置き場所が作れなくても、編集そのものは続けられます。
        }
        return backups;
    }

    /// <summary>もう無いフォルダーの控えは片づけます（試しに開いたフォルダーなど）。</summary>
    private static void Tidy(string baseFolder)
    {
        foreach (var folder in Directory.GetDirectories(baseFolder))
        {
            try
            {
                var marker = Path.Combine(folder, Marker);
                if (!File.Exists(marker)) continue;
                if (Directory.Exists(File.ReadAllText(marker).Trim())) continue;
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception) { }
        }
    }

    /// <summary>この控えが、どのサイトのものか（分からなければ null）。</summary>
    public string? SiteRoot
    {
        get
        {
            var marker = Path.Combine(Folder, Marker);
            return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
        }
    }

    private IEnumerable<DirectoryInfo> Sets() =>
        Directory.Exists(Folder)
            ? new DirectoryInfo(Folder).GetDirectories().OrderBy(set => set.Name, StringComparer.Ordinal)
                  .Where(set => set.Name != Marker)
            : Array.Empty<DirectoryInfo>();

    /// <summary>いちばん新しい控え（無ければ null）。</summary>
    public string? Latest => Sets().LastOrDefault()?.FullName;

    /// <summary>画面に出す1行（「2026/10/07 14:03（3ファイル）」）。</summary>
    public string? LatestLabel
    {
        get
        {
            var set = Sets().LastOrDefault();
            if (set == null) return null;

            var count = set.GetFiles("*", SearchOption.AllDirectories).Length;
            var when = DateTime.TryParseExact(set.Name, Stamp, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
                ? parsed.ToString("yyyy/MM/dd HH:mm")
                : set.Name;
            return $"{when}（{count}ファイル）";
        }
    }

    /// <summary>
    /// 書き換わるファイルを控えます。1つも無ければ何もしません。
    /// 控えを取れなくても保存は続けたいので、失敗しても投げません。
    /// </summary>
    public void Take(string root, IEnumerable<string> relatives)
    {
        var list = relatives.Distinct(StringComparer.Ordinal).ToArray();
        if (list.Length == 0) return;

        try
        {
            var set = Path.Combine(Folder, DateTime.Now.ToString(Stamp));
            // 同じ秒に2回保存したときは、後のほうを使います。
            if (Directory.Exists(set)) Directory.Delete(set, recursive: true);

            foreach (var relative in list)
            {
                var from = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(from)) continue;
                var to = Path.Combine(set, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to, overwrite: true);
            }

            Trim();
        }
        catch (Exception)
        {
            // 控えが取れなくても、保存そのものは続けます。
        }
    }

    private void Trim()
    {
        var sets = Sets().ToArray();
        foreach (var old in sets.Take(Math.Max(0, sets.Length - Keep)))
        {
            try { old.Delete(recursive: true); } catch (Exception) { }
        }
    }

    /// <summary>
    /// いちばん新しい控えを書き戻します。戻したファイルの相対パスを返します。
    /// 戻したあとは、その控えを消します（同じものを2回戻さないため）。
    /// </summary>
    public IReadOnlyList<string> Restore(string root)
    {
        var set = Sets().LastOrDefault();
        if (set == null) return Array.Empty<string>();

        var restored = new List<string>();
        foreach (var file in set.GetFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(set.FullName, file.FullName);
            var to = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file.FullName, to, overwrite: true);
            restored.Add(relative.Replace(Path.DirectorySeparatorChar, '/'));
        }

        set.Delete(recursive: true);
        return restored.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }
}
