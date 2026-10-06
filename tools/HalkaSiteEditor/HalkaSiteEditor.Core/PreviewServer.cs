using System.Net;
using System.Net.Sockets;

namespace HalkaSiteEditor.Core;

/// <summary>
/// プレビュー用の小さなHTTPサーバー。127.0.0.1 だけで待ち受け、サイトのフォルダーの中だけを配ります。
/// file:// で開くと絶対パス（/favicon.png など）が外れて本番と見え方が変わるため、
/// 本番と同じ「フォルダーの直下が / 」の形で出します。
/// </summary>
public sealed class PreviewServer : IDisposable
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".md"] = "text/plain; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".ico"] = "image/png",
        [".webp"] = "image/webp",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".mp3"] = "audio/mpeg",
        [".mp4"] = "video/mp4",
        [".wasm"] = "application/wasm",
        [".data"] = "application/octet-stream",
    };

    private readonly string root;
    private HttpListener? listener;
    private CancellationTokenSource? stopping;

    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}/";
    public bool IsRunning => listener?.IsListening == true;

    public PreviewServer(string root)
    {
        this.root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
    }

    public void Start()
    {
        if (IsRunning) return;

        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var port = FreePort();
            var candidate = new HttpListener();
            candidate.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                candidate.Start();
                listener = candidate;
                Port = port;
                stopping = new CancellationTokenSource();
                _ = Task.Run(() => Loop(candidate, stopping.Token));
                return;
            }
            catch (HttpListenerException error)
            {
                last = error;
                candidate.Close();
            }
        }
        throw new InvalidOperationException("プレビュー用のサーバーを開始できませんでした。", last);
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task Loop(HttpListener active, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await active.GetContextAsync(); }
            catch (Exception) { return; }   // 停止時はここに来ます。

            try { Respond(context); }
            catch (Exception) { /* 1件の失敗でプレビューごと止めません。 */ }
            finally { try { context.Response.Close(); } catch (Exception) { } }
        }
    }

    private void Respond(HttpListenerContext context)
    {
        var requested = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath ?? "/");
        var path = ResolveFile(root, requested);

        // 編集した結果がすぐ見えるように、キャッシュさせません。
        context.Response.Headers["Cache-Control"] = "no-store, must-revalidate";

        if (path == null)
        {
            context.Response.StatusCode = 404;
            var message = System.Text.Encoding.UTF8.GetBytes("404 " + requested);
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.OutputStream.Write(message);
            return;
        }

        var bytes = File.ReadAllBytes(path);
        context.Response.ContentType = ContentTypes.TryGetValue(Path.GetExtension(path), out var type)
            ? type
            : "application/octet-stream";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
    }

    /// <summary>
    /// URLのパスを実ファイルへ。フォルダーなら index.html。
    /// サイトのフォルダーの外へ出る指定（..）は null を返して配りません。
    /// </summary>
    public static string? ResolveFile(string root, string urlPath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var relative = urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

        string candidate;
        try { candidate = Path.GetFullPath(Path.Combine(fullRoot, relative)); }
        catch (Exception) { return null; }

        if (!candidate.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        if (Directory.Exists(candidate))
        {
            var index = Path.Combine(candidate, "index.html");
            return File.Exists(index) ? index : null;
        }

        return File.Exists(candidate) ? candidate : null;
    }

    public void Dispose()
    {
        stopping?.Cancel();
        try { listener?.Stop(); } catch (Exception) { }
        try { listener?.Close(); } catch (Exception) { }
        listener = null;
    }
}
