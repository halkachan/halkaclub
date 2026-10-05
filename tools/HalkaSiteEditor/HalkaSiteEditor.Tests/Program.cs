using HalkaSiteEditor.Core;
using System.Text;

// 本物のリポジトリは読むだけ。書き込みの検証は、毎回つくる一時フォルダーのコピーに対して行います。
var real = SitePaths.FindNear(AppContext.BaseDirectory) ?? throw new Exception("halkaclub のフォルダーが見つかりません");
var sandbox = Path.Combine(Path.GetTempPath(), "halka-site-editor-tests-" + Guid.NewGuid().ToString("N"));
CopySite(real, sandbox);

var passes = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
    passes++;
}

static bool Throws(Action action)
{
    try { action(); return false; }
    catch { return true; }
}

static void CopySite(string from, string to)
{
    foreach (var relative in new[]
             {
                 "CNAME", "index.html", "style.css", "script.js",
                 "commission/index.html", "commission/en/index.html",
                 "works/index.html", "works/works-data.js",
                 "club/index.html", "utamaze/version.json",
             })
    {
        var source = Path.Combine(from, relative.Replace('/', Path.DirectorySeparatorChar));
        var destination = Path.Combine(to, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }
}

static string Newlines(string path)
{
    var text = File.ReadAllText(path);
    var crlf = 0;
    for (var i = 0; i + 1 < text.Length; i++) if (text[i] == '\r' && text[i + 1] == '\n') crlf++;
    var lf = text.Count(c => c == '\n') - crlf;
    return $"CRLF={crlf} LF={lf}";
}

// --- 置き換えの道具 -------------------------------------------------------

var slotSample = "<span class=\"plan-amount\">A</span><span class=\"plan-amount\">B</span>";
var second = new ValueSlot(@"(<span class=""plan-amount"">)([^<]*)(</span>)", 1);
Check("狙った1か所だけ書き換える",
    second.Read(slotSample) == "B" &&
    second.Write(slotSample, "X") == "<span class=\"plan-amount\">A</span><span class=\"plan-amount\">X</span>");
Check("場所が無ければ失敗する", Throws(() => new ValueSlot(@"(a)(b)(c)", 5).Read("abc")));

Check("色の判定", CssColor.IsValid("#ffdc18") && CssColor.IsValid("#fff") &&
    CssColor.IsValid("rgba(232, 84, 84, 0.22)") && !CssColor.IsValid("まっ黄色") && !CssColor.IsValid("#12345"));
Check("色見本",
    CssColor.ToSwatchHex("#fff") == "#ffffff" &&
    CssColor.ToSwatchHex("#ffdc18") == "#ffdc18" &&
    CssColor.ToSwatchHex("rgb(232, 84, 84)") == "#e85454" &&
    CssColor.ToSwatchHex("rgba(0, 0, 0, 0.5)") == "#807f7b" &&   // 紙の白に半分重ねた灰色
    CssColor.ToSwatchHex("rgba(232, 84, 84, 0.22)") == "#fad9d2" &&
    CssColor.ToSwatchHex("まっ黄色") == null);

Check("YouTubeのURL",
    YouTubeUrl.ExtractId("https://youtu.be/RFQw7HejNZ0") == "RFQw7HejNZ0" &&
    YouTubeUrl.ExtractId("https://www.youtube.com/watch?v=RFQw7HejNZ0&t=10") == "RFQw7HejNZ0" &&
    YouTubeUrl.ExtractId("https://youtube.com/shorts/RFQw7HejNZ0") == "RFQw7HejNZ0" &&
    YouTubeUrl.ExtractId("https://example.com/RFQw7HejNZ0") == null &&
    YouTubeUrl.ExtractId("ただの文字") == null);

Check("金額を英語へ写す",
    PriceText.ToEnglish("70,000円～") == "from 70,000 JPY" &&
    PriceText.ToEnglish("ご相談受付中") == null);

Check("HTMLに入れてよい文字",
    HtmlText.Validate("ご相談受付中") == null &&
    HtmlText.Validate("<b>") != null &&
    HtmlText.Validate("MIX & マスタリング") != null &&
    HtmlText.Validate("  ") != null);

// --- 読み込み -------------------------------------------------------------

Check("サイトのルート判定", SitePaths.IsSiteRoot(sandbox) && !SitePaths.IsSiteRoot(Path.GetTempPath()));

var session = SiteSession.Load(sandbox);
var commission = session.Groups.Single(group => group.Title == "依頼ページ");
var colors = session.Groups.Single(group => group.Title == "サイトの基本色");
var top = session.Groups.Single(group => group.Title == "トップページ");

Check("依頼ページを日英そろえて読む",
    commission.Pairs.Count >= 2 &&
    commission.Pairs[0].Label == "受付状況" &&
    commission.Fields.Count == commission.Pairs.Count * 2);
Check("見出しは日本語版の項目名", commission.Pairs.Skip(1).All(pair => !string.IsNullOrWhiteSpace(pair.Label)) &&
    commission.Pairs.Any(pair => pair.Label.Contains("歌ってみた")));
Check("いまの受付状況を読めている", commission.Pairs[0].Ja.Value.Length > 0 && commission.Pairs[0].En.Value.Length > 0);
Check("色を6つ読む", colors.Fields.Count == 6 && colors.Fields.All(field => CssColor.IsValid(field.Value)));
Check("最新動画を読む", YouTubeUrl.ExtractId(top.Fields.Single().Value) != null);
Check("読んだ直後は変更なし", !session.HasChanges && !session.HasError && session.Changes().Count == 0);

// --- 入力の検証 -----------------------------------------------------------

var yellow = colors.Fields.Single(field => field.Label.StartsWith("黄色"));
yellow.Value = "まっきいろ";
Check("おかしな色は誤りとして出る", yellow.HasError && session.HasError);
yellow.Revert();
Check("元に戻せる", !yellow.HasError && !session.HasChanges);

var video = top.Fields.Single();
video.Value = "https://example.com/abc";
Check("YouTube以外のURLは誤り", video.HasError);
video.Revert();

// --- 保存 -----------------------------------------------------------------

var styleBefore = Newlines(Path.Combine(sandbox, "style.css"));
var indexBefore = Newlines(Path.Combine(sandbox, "index.html"));
var untouchedBefore = File.ReadAllBytes(Path.Combine(sandbox, "index.html"));

var priceJa = commission.Pairs[1].Ja;
var priceEn = commission.Pairs[1].En;
var priceWas = priceJa.Value;
priceJa.Value = "123,000円～";
commission.Pairs[1].SyncEnglishFromJapanese();
Check("英語側へ写せる", priceEn.Value == "from 123,000 JPY");

yellow.Value = "#ff0000";
Check("変更一覧に出る", session.Changes().Count == 3 &&
    session.Changes().Any(row => row.Before == priceWas && row.After == "123,000円～"));

session.Save();
Check("保存すると変更なしに戻る", !session.HasChanges && session.Changes().Count == 0);

var reloaded = SiteSession.Load(sandbox);
var reloadedCommission = reloaded.Groups.Single(group => group.Title == "依頼ページ");
Check("保存した値が読み直せる",
    reloadedCommission.Pairs[1].Ja.Value == "123,000円～" &&
    reloadedCommission.Pairs[1].En.Value == "from 123,000 JPY" &&
    reloaded.Groups.Single(group => group.Title == "サイトの基本色")
        .Fields.Single(field => field.Label.StartsWith("黄色")).Value == "#ff0000");

Check("改行コードが変わらない",
    Newlines(Path.Combine(sandbox, "style.css")) == styleBefore &&
    Newlines(Path.Combine(sandbox, "index.html")) == indexBefore);
Check("触っていないファイルは1バイトも変わらない",
    File.ReadAllBytes(Path.Combine(sandbox, "index.html")).SequenceEqual(untouchedBefore));

var styleText = File.ReadAllText(Path.Combine(sandbox, "style.css"));
Check("変えた値のまわりは元のまま",
    styleText.Contains("  --yellow: #ff0000;\n") && styleText.Contains("  --paper: #fffef6;\n"));
Check("BOMは付けない", File.ReadAllBytes(Path.Combine(sandbox, "style.css"))[0] != 0xEF);

// --- 別の場所で書き換えられていたら止まる ---------------------------------

var guard = SiteSession.Load(sandbox);
var guardColor = guard.Groups.Single(group => group.Title == "サイトの基本色").Fields[0];
guardColor.Value = "#010203";
File.AppendAllText(Path.Combine(sandbox, "style.css"), "\n/* 別の場所からの追記 */\n");
Check("横から変更されていたら保存しない", Throws(() => guard.Save()));

// 保存しなかったことを確かめます。
Check("中途半端に書き込まれていない", File.ReadAllText(Path.Combine(sandbox, "style.css")).Contains("--paper: #fffef6;") &&
    !File.ReadAllText(Path.Combine(sandbox, "style.css")).Contains("#010203"));

Check("誤りがあるまま保存しない", Throws(() =>
{
    var broken = SiteSession.Load(sandbox);
    broken.Groups.Single(group => group.Title == "トップページ").Fields.Single().Value = "だめなURL";
    broken.Save();
}));

// --- v0.3：リンク・くらぶ・うたまぜ・作品一覧 -----------------------------

var fresh = SiteSession.Load(sandbox);
var links = fresh.Groups.Single(group => group.Title == "リンク");
var club = fresh.Groups.Single(group => group.Title == "はるかくらぶ");
var utamaze = fresh.Groups.Single(group => group.Title == "うたまぜ！");

Check("リンクを名前・説明・リンク先の組で読む",
    links.Rows.Count >= 10 && links.Fields.Count == links.Rows.Count * 3 &&
    links.Rows.All(row => row.Cells.Count == 3) &&
    links.Rows.Any(row => row.Label == "YouTube") &&
    links.Rows.Single(row => row.Label == "YouTube").Cells[2].Field.Value.StartsWith("https://"));
Check("サイト内の相対パスもリンク先として認める",
    LinkUrl.Validate("commission/") == null && LinkUrl.Validate("https://example.com") == null &&
    LinkUrl.Validate("") != null && LinkUrl.Validate("a b") != null && LinkUrl.Validate("\"") != null);

Check("くらぶの更新日とKoofrを読む",
    club.Fields.Count == 2 &&
    System.Text.RegularExpressions.Regex.IsMatch(club.Fields[0].Value, @"^\d{4}/\d{2}/\d{2}$") &&
    club.Fields[1].Value.Contains("k00.fr"));

var released = utamaze.Fields.Single(field => field.Id == "utamaze.released");
Check("うたまぜの版と公開状態を読む",
    utamaze.Fields.Count == 4 && released.Value is "true" or "false" &&
    utamaze.Fields.Single(field => field.Id == "utamaze.latest_version").Value.Length > 0);
released.IsOn = !released.IsOn;
Check("チェックで true/false が入れ替わる", released.Value is "true" or "false" && released.Changed);
released.Revert();

var works = fresh.Works;
var utattemita = works.Categories.First();
Check("作品一覧を分類ごとに読む",
    works.Categories.Count == 4 && utattemita.Name == "歌ってみた" && utattemita.Works.Count > 5 &&
    works.Categories.All(category => category.Works.All(work => !work.HasError)));
Check("読んだだけなら1文字も変わらない", works.Serialize() == File.ReadAllText(Path.Combine(sandbox, "works", "works-data.js")));
Check("最初は変更なし", !works.HasChanges && !fresh.HasChanges);

var firstTitle = utattemita.Works[0].Title;
utattemita.Move(utattemita.Works[0], 1);
Check("並べ替えられる", utattemita.Works[1].Title == firstTitle && works.HasChanges &&
    works.Changes().Single().Label.Contains("並び順"));
utattemita.Move(utattemita.Works[1], -1);
Check("戻せる", utattemita.Works[0].Title == firstTitle && !works.HasChanges);

var added = works.Categories.Last().AddNew();
added.Title = "てすと作品";
added.PublishedAt = "2026-10-06";
Check("追加した直後はURLが空で誤り", added.HasError && works.HasError && fresh.HasError);
added.YouTubeUrl = "https://youtu.be/RFQw7HejNZ0";
Check("URLを入れれば直る", !added.HasError && !works.HasError &&
    works.Changes().Any(row => row.Label.Contains("追加") && row.After == "てすと作品"));

fresh.Save();
var afterWorks = SiteSession.Load(sandbox).Works;
Check("追加した作品が読み直せる",
    afterWorks.Categories.Last().Works.Last().Title == "てすと作品" &&
    afterWorks.Categories.Last().Works.Last().YouTubeUrl == "https://youtu.be/RFQw7HejNZ0");
Check("書き戻しても分類の説明やコメントは残る",
    File.ReadAllText(Path.Combine(sandbox, "works", "works-data.js")).Contains("// works/works-data.js") &&
    File.ReadAllText(Path.Combine(sandbox, "works", "works-data.js")).Contains("description: \"はぐれもの\""));

var removing = afterWorks.Categories.Last();
removing.Works.Remove(removing.Works.Last());
Check("削除が変更一覧に出る",
    afterWorks.Changes().Any(row => row.Label.Contains("削除") && row.Before == "てすと作品"));

Check("タイトルの \" は壊さずに書き戻せる", RoundTripTitle(sandbox, "引用\"つき\\バックスラッシュ"));

static bool RoundTripTitle(string root, string title)
{
    var session = SiteSession.Load(root);
    session.Works.Categories.First().Works[0].Title = title;
    session.Save();
    return SiteSession.Load(root).Works.Categories.First().Works[0].Title == title;
}

// --- プレビュー用サーバー -------------------------------------------------

Check("URLから実ファイルへ",
    PreviewServer.ResolveFile(sandbox, "/") == Path.Combine(sandbox, "index.html") &&
    PreviewServer.ResolveFile(sandbox, "/commission/") == Path.Combine(sandbox, "commission", "index.html") &&
    PreviewServer.ResolveFile(sandbox, "/commission/en/") == Path.Combine(sandbox, "commission", "en", "index.html") &&
    PreviewServer.ResolveFile(sandbox, "/style.css") == Path.Combine(sandbox, "style.css"));
Check("無いものは配らない",
    PreviewServer.ResolveFile(sandbox, "/nothing.html") == null &&
    PreviewServer.ResolveFile(sandbox, "/game/") == null);
Check("フォルダーの外へは出られない",
    PreviewServer.ResolveFile(sandbox, "/../../windows/win.ini") == null &&
    PreviewServer.ResolveFile(sandbox, "/commission/../../..") == null);

Check("出せるページだけ並べる",
    SitePages.ForSite(sandbox).Select(page => page.Url).SequenceEqual(
        new[] { "/", "/commission/", "/commission/en/", "/works/", "/club/" }));
Check("タブに合うページを選ぶ",
    SitePages.ForGroup(SitePages.ForSite(sandbox), "依頼ページ")!.Url == "/commission/" &&
    SitePages.ForGroup(SitePages.ForSite(sandbox), "サイトの基本色")!.Url == "/");

using (var server = new PreviewServer(sandbox))
{
    server.Start();
    using var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };

    var home = http.GetAsync("/").Result;
    var homeBody = home.Content.ReadAsStringAsync().Result;
    Check("トップを配る", server.IsRunning && home.IsSuccessStatusCode &&
        homeBody.Contains("<html") && home.Content.Headers.ContentType?.MediaType == "text/html");

    var css = http.GetAsync("/style.css").Result;
    Check("CSSを正しい種類で配る", css.IsSuccessStatusCode &&
        css.Content.Headers.ContentType?.MediaType == "text/css" &&
        css.Content.ReadAsStringAsync().Result.Contains("--paper"));

    var page = http.GetAsync("/commission/").Result;
    Check("下の階層のページも配る", page.IsSuccessStatusCode &&
        page.Content.ReadAsStringAsync().Result.Contains("依頼一覧"));

    Check("キャッシュさせない", home.Headers.CacheControl?.NoStore == true);
    Check("無いURLは404", http.GetAsync("/nothing.html").Result.StatusCode == System.Net.HttpStatusCode.NotFound);
    Check("外へ出るURLは配らない",
        http.GetAsync("/../../../../Windows/win.ini").Result.StatusCode != System.Net.HttpStatusCode.OK);

    // 保存したものがすぐ見えること（キャッシュさせていないので、次のGETで変わります）。
    var swap = SiteSession.Load(sandbox);
    swap.Groups.Single(group => group.Title == "サイトの基本色")
        .Fields.Single(field => field.Label.StartsWith("黄色")).Value = "#00ff00";
    swap.Save();
    Check("保存した内容がすぐ出る",
        http.GetAsync("/style.css").Result.Content.ReadAsStringAsync().Result.Contains("--yellow: #00ff00;"));
}

Directory.Delete(sandbox, recursive: true);
Console.WriteLine($"\n{passes} passed");
