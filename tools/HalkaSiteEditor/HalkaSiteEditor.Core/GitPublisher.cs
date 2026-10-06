using System.Diagnostics;
using System.Text;

namespace HalkaSiteEditor.Core;

/// <summary>公開を待っているファイル1つ。</summary>
public sealed record PendingFile(string Path, string Status);

/// <summary>git を1回動かした結果。</summary>
public sealed record GitRun(bool Ok, string Output)
{
    /// <summary>向こうに新しいコミットがあって押し返された状態か。</summary>
    public bool RejectedByRemote =>
        !Ok && (Output.Contains("rejected") || Output.Contains("fetch first") ||
                Output.Contains("non-fast-forward") || Output.Contains("Updates were rejected"));
}

/// <summary>
/// サイトのフォルダーで git を動かして、公開（コミットしてpush）します。
/// **このツールが扱うファイルだけ**を対象にします。同じリポジトリにある
/// unity-project などの作業中の変更を、巻き込んで公開しないためです。
/// </summary>
public sealed class GitPublisher
{
    private readonly string root;

    public GitPublisher(string root) => this.root = root;

    public bool IsAvailable => Run("rev-parse", "--is-inside-work-tree").Ok;

    public string Branch()
    {
        var result = Run("rev-parse", "--abbrev-ref", "HEAD");
        return result.Ok ? result.Output.Trim() : "";
    }

    /// <summary>公開を待っている、このツールが扱うファイル。</summary>
    public IReadOnlyList<PendingFile> Pending(IEnumerable<string> managed)
    {
        var args = new List<string> { "status", "--porcelain", "--" };
        args.AddRange(managed);
        var result = Run(args.ToArray());
        if (!result.Ok) return Array.Empty<PendingFile>();

        // 1行は「XY<空白>パス」の形です（例: " M style.css"、"?? new.html"）。
        var line = new System.Text.RegularExpressions.Regex(@"^(..) (.+)$");
        var files = new List<PendingFile>();
        foreach (var raw in result.Output.Split('\n'))
        {
            var match = line.Match(raw.TrimEnd('\r'));
            if (!match.Success) continue;
            files.Add(new PendingFile(match.Groups[2].Value.Trim(), Describe(match.Groups[1].Value.Trim())));
        }
        return files;
    }

    private static string Describe(string code) => code switch
    {
        "M" or "MM" or "AM" => "変更",
        "A" => "追加",
        "D" => "削除",
        "??" => "新しいファイル",
        _ => code,
    };

    /// <summary>変わっているファイルから、コミットメッセージの下書きを作ります。</summary>
    public static string SuggestMessage(IEnumerable<PendingFile> files)
    {
        var names = new List<string>();
        void Add(string name) { if (!names.Contains(name)) names.Add(name); }

        foreach (var file in files)
        {
            var path = file.Path.Replace('\\', '/');
            if (path.StartsWith("commission/")) Add("依頼ページ");
            else if (path == "works/works-data.js") Add("作品一覧");
            else if (path == "index.html") Add("トップページ");
            else if (path == "script.js") Add("最新動画");
            else if (path == "style.css") Add("配色");
            else if (path.StartsWith("club/")) Add("はるかくらぶ");
            else if (path.StartsWith("utamaze/")) Add("うたまぜ！");
            else Add(path);
        }

        return names.Count == 0 ? "サイトを更新" : string.Join("・", names) + "を更新";
    }

    /// <summary>コミットして push します。失敗したら、何が起きたかを文字列で返します。</summary>
    public GitRun Publish(IEnumerable<string> managed, string message)
    {
        var paths = managed.ToArray();

        var add = new List<string> { "add", "--" };
        add.AddRange(paths);
        var staged = Run(add.ToArray());
        if (!staged.Ok) return staged;

        var check = new List<string> { "diff", "--cached", "--quiet", "--" };
        check.AddRange(paths);
        if (Run(check.ToArray()).Ok) return new GitRun(false, "公開するものがありません。");

        var messageFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "halka-site-editor-commit-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(messageFile, message, new UTF8Encoding(false));
            var committed = Run("commit", "-F", messageFile);
            if (!committed.Ok) return committed;
        }
        finally
        {
            try { File.Delete(messageFile); } catch (Exception) { }
        }

        return Push();
    }

    public GitRun Push()
    {
        var branch = Branch();
        if (string.IsNullOrEmpty(branch)) return new GitRun(false, "いまのブランチが分かりませんでした。");
        return Run("push", "origin", branch);
    }

    /// <summary>
    /// 向こうの新しいコミットを取り込んでから、もう一度 push します。
    /// unity-project などの作業中の変更があっても止まらないよう、--autostash で一時的に避けます
    /// （取り込みが終わると自動で戻ります）。
    /// </summary>
    public GitRun PullRebaseAndPush()
    {
        var branch = Branch();
        var pulled = Run("pull", "--rebase", "--autostash", "origin", branch);
        if (!pulled.Ok)
        {
            return new GitRun(false,
                "取り込みに失敗しました。GitHub Desktop で解決してください。\n\n" + pulled.Output);
        }
        return Push();
    }

    private GitRun Run(params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.quotepath=false");
        foreach (var argument in args) info.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(info);
            if (process == null) return new GitRun(false, "git を動かせませんでした。");

            // 両方を先に読み始めないと、出力が多いときに止まります。
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            // status の行は先頭の空白に意味があるので、前は削りません。
            var text = output.Result;
            if (error.Result.Length > 0)
                text = text.Length > 0 ? text + "\n" + error.Result : error.Result;
            return new GitRun(process.ExitCode == 0, text.TrimEnd());
        }
        catch (Exception failure)
        {
            return new GitRun(false, "git を動かせませんでした：" + failure.Message);
        }
    }
}
