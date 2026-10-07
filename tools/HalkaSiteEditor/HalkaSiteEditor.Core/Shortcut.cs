namespace HalkaSiteEditor.Core;

/// <summary>デスクトップとスタートメニューに、このアプリの近道を作ります。</summary>
public static class Shortcut
{
    public const string Name = "HALKA SITE EDITOR.lnk";

    public static string DesktopPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name);

    public static string StartMenuPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name);

    public sealed record Result(bool Ok, IReadOnlyList<string> Created, string? Error);

    /// <summary>2か所に作ります。すでにあれば作り直します。</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static Result Create(string exePath)
    {
        if (!File.Exists(exePath))
            return new Result(false, Array.Empty<string>(), $"EXEが見つかりません：{exePath}");

        var created = new List<string>();
        try
        {
            foreach (var path in new[] { DesktopPath, StartMenuPath })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Write(path, exePath);
                created.Add(path);
            }
        }
        catch (Exception error)
        {
            return new Result(created.Count > 0, created, error.Message);
        }
        return new Result(true, created, null);
    }

    /// <summary>Windows が持っているしくみ（WScript.Shell）で .lnk を書きます。</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void Write(string path, string exePath)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell が使えません。");
        dynamic shell = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("WScript.Shell を開けません。");

        dynamic link = shell.CreateShortcut(path);
        link.TargetPath = exePath;
        link.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
        link.IconLocation = exePath + ",0";
        link.Description = "halkaclub.com の中身を書き換えるアプリ";
        link.Save();
    }

    public static bool Exists => File.Exists(DesktopPath) || File.Exists(StartMenuPath);
}
