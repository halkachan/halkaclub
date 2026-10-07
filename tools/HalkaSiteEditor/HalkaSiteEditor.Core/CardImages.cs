using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace HalkaSiteEditor.Core;

/// <summary>
/// SNSのカード画像（`assets/ogp/*.png`）を、同梱の Python で作り直します。
/// 絵柄と文章は `tools/ogp/generate-ogp.py` の中の一覧で決まっていて、
/// このツールで直す OGP の文章とは別物です。
/// </summary>
public static class CardImages
{
    public static string ScriptPath(string root) =>
        Path.Combine(root, "tools", "ogp", "generate-ogp.py");

    public static bool IsAvailable(string root) => File.Exists(ScriptPath(root));

    public sealed record Result(bool Ok, string Output, IReadOnlyList<string> Changed);

    /// <summary>
    /// 作り直して、中身が変わった画像の相対パスを返します。
    /// Python が無いときは Ok が false になり、理由が Output に入ります。
    /// </summary>
    public static Result Run(string root, IEnumerable<string> watch)
    {
        if (!IsAvailable(root))
            return new Result(false, "tools/ogp/generate-ogp.py が見つかりません。", Array.Empty<string>());

        var before = watch.ToDictionary(relative => relative, relative => Hash(Full(root, relative)));

        var output = new StringBuilder();
        var ok = false;
        foreach (var exe in new[] { "py", "python" })
        {
            var attempt = Execute(root, exe, ScriptPath(root));
            if (attempt == null) continue;   // そのコマンドが無い
            output.Clear().Append(attempt.Value.Output);
            ok = attempt.Value.ExitCode == 0;
            break;
        }

        if (output.Length == 0 && !ok)
        {
            return new Result(false,
                "Python が見つかりません。`py -m pip install pillow fonttools brotli` のあとで試してください。",
                Array.Empty<string>());
        }

        // 足りないものがあるときは、入れ方も一緒に出します。
        if (!ok && output.ToString().Contains("ModuleNotFoundError"))
        {
            output.Append(Environment.NewLine).Append(Environment.NewLine)
                  .Append("必要なものが足りません。コマンドプロンプトで次を実行してください：").Append(Environment.NewLine)
                  .Append("py -m pip install pillow fonttools brotli");
        }

        var changed = before
            .Where(pair => Hash(Full(root, pair.Key)) != pair.Value)
            .Select(pair => pair.Key)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        return new Result(ok, output.ToString().Trim(), changed);
    }

    private static string Full(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Hash(string path) =>
        File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : "";

    private static (int ExitCode, string Output)? Execute(string root, string exe, string script)
    {
        var info = new ProcessStartInfo(exe)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add(script);
        // これを入れないと、Python は日本語をこのPCの文字コードで出すので文字化けします。
        info.Environment["PYTHONIOENCODING"] = "utf-8";

        try
        {
            using var process = Process.Start(info);
            if (process == null) return null;
            var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(120_000);
            return (process.ExitCode, text);
        }
        catch (Exception)
        {
            // そのコマンドが入っていない。次を試します。
            return null;
        }
    }
}
