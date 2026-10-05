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

Directory.Delete(sandbox, recursive: true);
Console.WriteLine($"\n{passes} passed");
