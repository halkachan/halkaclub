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
                 "utamaze/index.html",
                 "club/index.html", "utamaze/version.json",
             })
    {
        var source = Path.Combine(from, relative.Replace('/', Path.DirectorySeparatorChar));
        var destination = Path.Combine(to, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    // 画像も、差し替えの確認に使うので複写します。
    foreach (var image in Directory.GetFiles(Path.Combine(from, "assets", "ogp"), "*.png")
                 .Append(Path.Combine(from, "assets", "profile", "halgif1.gif")))
    {
        var destination = Path.Combine(to, Path.GetRelativePath(from, image));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(image, destination, overwrite: true);
    }

    // ゲームは本数が増えるので、フォルダーごと複写します。
    foreach (var page in Directory.GetFiles(Path.Combine(from, "game"), "index.html",
                 SearchOption.AllDirectories))
    {
        var destination = Path.Combine(to, Path.GetRelativePath(from, page));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(page, destination, overwrite: true);
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
    commission.Fields.Count == commission.Pairs.Count * 2 + commission.Options.Count * 4);
Check("見出しは日本語版の項目名", commission.Pairs.Skip(1).All(pair => !string.IsNullOrWhiteSpace(pair.Label)) &&
    commission.Pairs.Any(pair => pair.Label.Contains("歌ってみた")));
Check("いまの受付状況を読めている", commission.Pairs[0].Ja.Value.Length > 0 && commission.Pairs[0].En.Value.Length > 0);
Check("色を6つ読む", colors.Fields.Count == 6 && colors.Fields.All(field => CssColor.IsValid(field.Value)));
Check("最新動画を読む", YouTubeUrl.ExtractId(top.Fields.Single(field => field.Id == "top.latestVideo").Value) != null);
Check("読んだ直後は変更なし", !session.HasChanges && !session.HasError && session.Changes().Count == 0);

// --- 入力の検証 -----------------------------------------------------------

var yellow = colors.Fields.Single(field => field.Label.StartsWith("黄色"));
yellow.Value = "まっきいろ";
Check("おかしな色は誤りとして出る", yellow.HasError && session.HasError);
yellow.Revert();
Check("元に戻せる", !yellow.HasError && !session.HasChanges);

var video = top.Fields.Single(field => field.Id == "top.latestVideo");
video.Value = "https://example.com/abc";
Check("YouTube以外のURLは誤り", video.HasError);
video.Revert();

// --- 保存 -----------------------------------------------------------------

var styleBefore = Newlines(Path.Combine(sandbox, "style.css"));
var indexBefore = Newlines(Path.Combine(sandbox, "index.html"));
var untouchedBefore = File.ReadAllText(Path.Combine(sandbox, "index.html"));
var styleVersionBefore = CacheBuster.Current(untouchedBefore, "style.css");

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
// 色を変えたので、style.css を読んでいるページは ?v= だけ上がります。
// それ以外は1文字も変わりません。
static string IgnoreStyleVersion(string text) =>
    System.Text.RegularExpressions.Regex.Replace(text, @"style\.css\?v=\d+", "style.css?v=");

var untouchedAfter = File.ReadAllText(Path.Combine(sandbox, "index.html"));
Check("触っていない所は1文字も変わらない",
    IgnoreStyleVersion(untouchedAfter) == IgnoreStyleVersion(untouchedBefore));
Check("色を変えると、読んでいるページの番号が上がる",
    CacheBuster.Current(untouchedAfter, "style.css") == styleVersionBefore + 1 &&
    CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "works", "index.html")), "style.css")
        == styleVersionBefore + 1);

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
    broken.Groups.Single(group => group.Title == "トップページ").Fields.Single(field => field.Id == "top.latestVideo").Value = "だめなURL";
    broken.Save();
}));

// --- v0.3：リンク・くらぶ・うたまぜ・作品一覧 -----------------------------

var fresh = SiteSession.Load(sandbox);
var links = fresh.Groups.Single(group => group.Title == "リンク");
var club = fresh.Groups.Single(group => group.Title == "はるかくらぶ");
var utamaze = fresh.Groups.Single(group => group.Title == "うたまぜ！");

Check("大きいリンクを名前・説明・リンク先の組で読む",
    links.Rows.Count == 4 && links.Fields.Count == links.Rows.Count * 3 &&
    links.Rows.All(row => row.Cells.Count == 3) &&
    links.Rows.Any(row => row.Label == "作品一覧") &&
    links.Rows.Any(row => row.Label == "はるかくらぶ") &&
    links.Rows.Single(row => row.Label == "作品一覧").Cells[2].Field.Value == "works/index.html");
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

// --- v0.5：追加プランと依頼ページの文章 -----------------------------------

var v5 = SiteSession.Load(sandbox);
var v5Commission = v5.Groups.Single(group => group.Title == "依頼ページ");

Check("追加プランを日英の組で読む",
    v5Commission.Options.Count == 8 &&
    v5Commission.Options.All(row => row.Name.Ja.Value.Length > 0 && row.Price.Ja.Value.Length > 0) &&
    v5Commission.Options.Any(row => row.Label.Contains("ハモリ")) &&
    v5Commission.Options[0].Price.Ja.Value.Contains("4,000"));

var option = v5Commission.Options[0];
option.Price.Ja.Value = "＋9,000円";
option.SyncEnglishFromJapanese();
Check("追加プランの金額も英語へ写せる", option.Price.En.Value == "from 9,000 JPY" && v5.HasChanges);
option.Price.Ja.Revert();
option.Price.En.Revert();

var text = v5.CommissionText;
var jaPage = text.Pages.Single(page => page.Title == "依頼ページ（日本語）");
var enPage = text.Pages.Single(page => page.Title == "依頼ページ（英語）");

Check("文章のかたまりを両ページから拾う",
    text.Pages.Count == 2 && jaPage.Blocks.Count > 30 && enPage.Blocks.Count > 30 &&
    jaPage.Blocks.Any(block => block.Label.Contains("歌ってみたMIX")) &&
    jaPage.Blocks.Any(block => block.Label.Contains("テンプレート本文")));
Check("箇条書きは空行区切りで読める",
    jaPage.Blocks.First(block => block.Label.Contains("歌ってみたMIX")).Text.Contains("ピッチ補正\n\nリズム補正"));
Check("<br /> は改行として読める",
    jaPage.Blocks.Any(block => block.Text.Contains("\n") && !block.Text.Contains("<br")));
Check("HTMLの印は画面に出さない",
    jaPage.Blocks.All(block => !block.Text.Contains("<li>") && !block.Text.Contains("<p>")));

// ここが要：読んだだけなら1文字も変わらないこと。
ShowFirstDifference(text.Rebuild(jaPage), jaPage.CurrentText);
static void ShowFirstDifference(string rebuilt, string original)
{
    if (rebuilt == original) return;
    var limit = Math.Min(rebuilt.Length, original.Length);
    var at = 0;
    while (at < limit && rebuilt[at] == original[at]) at++;
    var from = Math.Max(0, at - 90);
    Console.WriteLine("  [診断] 位置 " + at);
    Console.WriteLine("  [元 ] " + original.Substring(from, Math.Min(240, original.Length - from)).Replace("\n", "\\n"));
    Console.WriteLine("  [新 ] " + rebuilt.Substring(from, Math.Min(240, rebuilt.Length - from)).Replace("\n", "\\n"));
}
Check("依頼ページ（日本語）は組み立て直しても1文字も変わらない", text.Rebuild(jaPage) == jaPage.CurrentText);
Check("依頼ページ（英語）は組み立て直しても1文字も変わらない", text.Rebuild(enPage) == enPage.CurrentText);
Check("最初は変更なし", !text.HasChanges && !v5.HasChanges);

var listBlock = jaPage.Blocks.First(block => block.Label.Contains("歌ってみたMIX"));
listBlock.Text = "ピッチ補正\n\nリズム補正\n\nあたらしい項目";
Check("箇条書きの増減が変更一覧に出る",
    text.HasChanges && v5.Changes().Any(row => row.Label.Contains("依頼ページ（日本語）")));

var paragraph = jaPage.Blocks.First(block => block.Label.Contains("リテイク"));
var paragraphWas = paragraph.Text;
paragraph.Text = "ためしの説明。\n2行目。";

Check("< > は誤りとして出る", NewError(jaPage, "<b>太字</b>"));
static bool NewError(PageTextPage page, string bad)
{
    var block = page.Blocks.First();
    var keep = block.Text;
    block.Text = bad;
    var bad1 = block.HasError;
    block.Text = keep;
    return bad1 && !block.HasError;
}

v5.Save();
Check("保存すると変更なしに戻る", !v5.HasChanges && !text.HasChanges);

var afterSave = SiteSession.Load(sandbox);
var afterJa = afterSave.CommissionText.Pages.Single(page => page.Title == "依頼ページ（日本語）");
Check("書いた箇条書きが読み直せる",
    afterJa.Blocks.First(block => block.Label.Contains("歌ってみたMIX")).Text.EndsWith("あたらしい項目"));
Check("書いた段落が読み直せる",
    afterJa.Blocks.First(block => block.Label.Contains("リテイク")).Text == "ためしの説明。\n2行目。");
Check("保存後も組み立て直しで1文字も変わらない",
    afterSave.CommissionText.Rebuild(afterJa) == afterJa.CurrentText);
Check("料金と文章を同じファイルで同時に直せる",
    File.ReadAllText(Path.Combine(sandbox, "commission", "index.html")).Contains("あたらしい項目") &&
    File.ReadAllText(Path.Combine(sandbox, "commission", "index.html")).Contains("plan-amount"));

// 2回続けて保存しても、位置を見失わないこと。
var again = afterJa.Blocks.First(block => block.Label.Contains("リテイク"));
again.Text = "もう一度ためす。";
afterSave.Save();
Check("続けて保存しても見失わない",
    SiteSession.Load(sandbox).CommissionText.Pages[0].Blocks
        .First(block => block.Label.Contains("リテイク")).Text == "もう一度ためす。");

// --- v0.6：うたまぜ！のリリース切り替え -----------------------------------

var v6 = SiteSession.Load(sandbox);
var release = v6.Utamaze ?? throw new Exception("うたまぜ！のリリース手順が読めません");

Check("リリース手順を6項目そろえる",
    release.Switches.Count == 6 &&
    release.Switches.All(step => !step.IsUnknown) &&
    release.Switches.Any(step => step.Label.Contains("検索")) &&
    release.Switches.Any(step => step.Label.Contains("PRO")) &&
    release.Switches.Any(step => step.Label.Contains("トップページ")));
Check("いまは全部準備中", release.LiveCount == 0 && !release.IsMixed &&
    release.Summary.Contains("すべて準備中"));
Check("version.json のダウンロード先を初期値にする", release.DownloadUrl.Contains("halkaclub.com"));

// URLが足りないまま公開にしようとすると、誤りとして出る。
var pro = release.Switches.First(step => step.Label.Contains("PRO"));
pro.MakeLive = true;
Check("購入URLが無いと誤りになる", pro.HasError && release.HasError && v6.HasError);
release.CheckoutUrl = "https://halka.lemonsqueezy.com/buy/xxxx";
Check("入れれば直る", !pro.HasError && !release.HasError);
pro.MakeLive = false;

// まとめて公開の形へ。
release.MakeAllLive();
Check("まとめて公開にできる", release.Switches.All(step => step.MakeLive) && release.HasChanges &&
    !release.HasError && v6.Changes().Count(row => row.Label.StartsWith("うたまぜ！")) == 6);

v6.Save();

var utamazePage = File.ReadAllText(Path.Combine(sandbox, "utamaze", "index.html"));
var topPage = File.ReadAllText(Path.Combine(sandbox, "index.html"));
Check("noindex が外れる", !utamazePage.Contains("noindex"));
Check("ダウンロードボタンがリンクになる",
    utamazePage.Contains("<a class=\"button primary\" href=\"https://halkaclub.com/utamaze/\">ダウンロード</a>") &&
    utamazePage.Contains("<a class=\"button ghost full\" href=\"https://halkaclub.com/utamaze/\">ダウンロード</a>"));
Check("購入ボタンが購入URLになる",
    utamazePage.Contains("<a class=\"button primary full\" href=\"https://halka.lemonsqueezy.com/buy/xxxx\">PROを購入する</a>"));
Check("「公開準備中です」が消える", !utamazePage.Contains("pre-release"));
Check("トップページにカードが出る",
    topPage.Contains("href=\"/utamaze/\"") && topPage.Contains("<strong>うたまぜ！</strong>") &&
    topPage.Contains("utamaze-link:start") && topPage.Contains("utamaze-link:end"));
Check("ページの作りが変わったと分かる", release.StructureChanged == false);   // 保存後は変更なしに戻る

var afterRelease = SiteSession.Load(sandbox).Utamaze!;
Check("読み直すと全部公開になっている",
    afterRelease.LiveCount == 6 && !afterRelease.IsMixed && !afterRelease.HasChanges &&
    afterRelease.Summary.Contains("すべて公開"));

// 準備中へ戻せること（間違えて公開してしまったときのため）。
foreach (var step in afterRelease.Switches) step.MakeLive = false;
Check("準備中へ戻す変更として出る", afterRelease.HasChanges && afterRelease.Changes().Count == 6);

var back = SiteSession.Load(sandbox);
foreach (var step in back.Utamaze!.Switches) step.MakeLive = false;
back.Save();

var restored = SiteSession.Load(sandbox).Utamaze!;
Check("戻すと全部準備中に戻る", restored.LiveCount == 0);
Check("戻したページは元どおり",
    File.ReadAllText(Path.Combine(sandbox, "utamaze", "index.html")).Contains("noindex") &&
    File.ReadAllText(Path.Combine(sandbox, "utamaze", "index.html")).Contains("PRO　準備中") &&
    File.ReadAllText(Path.Combine(sandbox, "utamaze", "index.html")).Contains("pre-release") &&
    !File.ReadAllText(Path.Combine(sandbox, "index.html")).Contains("href=\"/utamaze/\""));

// 一部だけ公開した状態を見分けられること。
var partial = SiteSession.Load(sandbox);
partial.Utamaze!.Switches.First(step => step.Label.Contains("検索")).MakeLive = true;
partial.Save();
var mixed = SiteSession.Load(sandbox).Utamaze!;
Check("揃っていないと分かる", mixed.IsMixed && mixed.LiveCount == 1 && mixed.Summary.Contains("揃っていません"));

// --- v0.7：YouTubeの新着から作品を足す -------------------------------------

const string feedSample = """
<?xml version="1.0" encoding="UTF-8"?>
<feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns="http://www.w3.org/2005/Atom">
  <title>HALKA</title>
  <entry>
    <id>yt:video:7dmyKo4D_3w</id>
    <yt:videoId>7dmyKo4D_3w</yt:videoId>
    <title>てすと Arrange coverの歌</title>
    <published>2026-10-04T12:00:00+00:00</published>
  </entry>
  <entry>
    <id>yt:video:XRIndSupS3A</id>
    <yt:videoId>XRIndSupS3A</yt:videoId>
    <title>脱法ロックｳﾀｯﾀ (Arrange cover)</title>
    <published>2021-10-17T09:00:00+00:00</published>
  </entry>
  <entry>
    <id>yt:video:zzzzzzzzzzz</id>
    <yt:videoId>zzzzzzzzzzz</yt:videoId>
    <title>引用"つきのタイトル</title>
    <published>2026-01-02T15:30:00+00:00</published>
  </entry>
</feed>
""";

var feed = YouTubeFeed.Parse(feedSample);
Check("新着を読み取れる",
    feed.Count == 3 && feed[0].VideoId == "7dmyKo4D_3w" &&
    feed[0].Title == "てすと Arrange coverの歌" &&
    feed[0].Url == "https://youtu.be/7dmyKo4D_3w" &&
    feed[0].PublishedAt.StartsWith("2026-10-0"));
Check("チャンネルIDの形を見る",
    YouTubeFeed.LooksLikeChannelId(YouTubeFeed.DefaultChannelId) &&
    !YouTubeFeed.LooksLikeChannelId("HALKAchan") &&
    !YouTubeFeed.LooksLikeChannelId("UC123") &&
    !YouTubeFeed.LooksLikeChannelId(null));
Check("取り出し先のURLを組み立てる",
    YouTubeFeed.FeedUrl("UCxxxx").EndsWith("channel_id=UCxxxx"));

var feedSession = SiteSession.Load(sandbox);
var known = YouTubeFeed.KnownVideoIds(feedSession.Works);
Check("もう入っている動画が分かる",
    known.Contains("XRIndSupS3A") && !known.Contains("7dmyKo4D_3w") && known.Count > 20);

// 新着のうち、まだ入っていないものだけを足せること。
var target = feedSession.Works.Categories.First();
var before = target.Works.Count;
foreach (var feedItem in feed.Where(v => !known.Contains(v.VideoId)))
{
    var row = target.AddNew();
    row.Title = feedItem.Title;
    row.PublishedAt = feedItem.PublishedAt;
    row.YouTubeUrl = feedItem.Url;
}
Check("入っていない2本だけ足される", target.Works.Count == before + 2 &&
    target.Works.All(work => !work.HasError));

feedSession.Save();
var afterFeed = SiteSession.Load(sandbox).Works.Categories.First();
Check("足した動画が読み直せる",
    afterFeed.Works.Any(work => work.YouTubeUrl == "https://youtu.be/7dmyKo4D_3w") &&
    afterFeed.Works.Any(work => work.Title == "引用\"つきのタイトル"));
Check("足したあとは重複として扱われる",
    YouTubeFeed.KnownVideoIds(SiteSession.Load(sandbox).Works).Contains("7dmyKo4D_3w"));

// --- v0.8：作品を変えたら読み込み側の ?v= を上げる --------------------------
// GitHub Pages は max-age=600 を返すので、番号を上げないと見る人が古いまま見てしまう。

var worksHtml = Path.Combine(sandbox, "works", "index.html");

var bust = SiteSession.Load(sandbox);
var versionBefore = CacheBuster.Current(File.ReadAllText(worksHtml), "works-data.js");

bust.Works.Categories.First().Works[0].Title = "番号が上がるか確かめる";
bust.Save();

var versionAfter = CacheBuster.Current(File.ReadAllText(worksHtml), "works-data.js");
Check("作品を変えると ?v= が上がる", versionAfter == versionBefore + 1 && versionAfter >= 1);
Check("読み込み行が壊れていない",
    File.ReadAllText(worksHtml).Contains($"works-data.js?v={versionAfter}\" defer"));

// 作品を触っていないときは上げない（意味のない差分を出さないため）。
var untouched = SiteSession.Load(sandbox);
untouched.Groups.Single(group => group.Title == "サイトの基本色")
    .Fields.Single(field => field.Label.StartsWith("黄色")).Value = "#010203";
untouched.Save();
Check("作品を触らなければ上がらない",
    CacheBuster.Current(File.ReadAllText(worksHtml), "works-data.js") == versionAfter);

// ?v= がまだ無いファイルには付ける。
Check("まだ番号が無ければ付ける", CacheBuster.Current("<script src=\"a.js\"></script>", "a.js") == 0);

// --- v0.8：うれしいこと／くらぶの更新内容 ----------------------------------

var v8 = SiteSession.Load(sandbox);
var news = v8.News ?? throw new Exception("うれしいこと欄が読めません");
var clubUpdate = v8.ClubUpdate ?? throw new Exception("くらぶの更新内容が読めません");

Check("うれしいことを読み取れる",
    news.Items.Count == 3 &&
    news.Items[0].Date == "2/21投稿" &&
    news.Items[0].Title.Contains("竹取オーバナイト") &&
    news.Items[0].Description.Contains("ボカコレ") &&
    news.Items[0].Achievements.Contains("超かぐや姫！賞"));
Check("説明が無い件も読める",
    news.Items[1].Description == "" && news.Items[1].Achievements.Contains("コンピCD"));
Check("HTMLの印は画面に出さない",
    news.Items.All(item => !item.Title.Contains("<") && !item.Achievements.Contains("<mark>")));
Check("読んだだけなら1文字も変わらない", news.Rebuild() == File.ReadAllText(Path.Combine(sandbox, "index.html")));
Check("最初は変更なし", !news.HasChanges && !clubUpdate.Changed && !v8.HasChanges);

Check("くらぶの更新内容を読める", clubUpdate.Text == "・ページ作成");

// 追加・並べ替え・削除。
var addedNews = news.AddNew();
addedNews.Date = "10/7";
addedNews.Title = "ためしのお知らせ";
addedNews.Achievements = "ためし賞　受賞\nもうひとつ";
Check("追加が変更一覧に出る",
    news.HasChanges && !news.HasError &&
    v8.Changes().Any(row => row.Label.Contains("うれしいこと：追加") && row.After == "ためしのお知らせ"));

news.Move(addedNews, -1);
Check("並べ替えられる", news.Items[2] == addedNews);

clubUpdate.Text = "・ページ作成\n・ステムを追加";
Check("くらぶの更新内容を増やせる", clubUpdate.Changed &&
    v8.Changes().Any(row => row.Label.Contains("くらぶ")));

v8.Save();

var newsSession = SiteSession.Load(sandbox);
var newsAfter = newsSession.News!;
Check("書いたお知らせが読み直せる",
    newsAfter.Items.Count == 4 &&
    newsAfter.Items[2].Title == "ためしのお知らせ" &&
    newsAfter.Items[2].Achievements == "ためし賞　受賞\nもうひとつ" &&
    newsAfter.Items[2].Description == "");
Check("保存後も組み立て直しで1文字も変わらない",
    newsAfter.Rebuild() == File.ReadAllText(Path.Combine(sandbox, "index.html")));
Check("くらぶの更新内容が読み直せる", newsSession.ClubUpdate!.Text == "・ページ作成\n・ステムを追加");
Check("説明が空なら、その行ごと出さない",
    !File.ReadAllText(Path.Combine(sandbox, "index.html")).Contains("<p class=\"news-desc\"></p>"));

// 削除して元の件数へ戻せること。
var shrink = SiteSession.Load(sandbox);
var remove = shrink.News!.Items.First(item => item.Title == "ためしのお知らせ");
shrink.News.Items.Remove(remove);
Check("削除が変更一覧に出る",
    shrink.Changes().Any(row => row.Label.Contains("削除") && row.Before == "ためしのお知らせ"));
shrink.Save();
Check("消すと元の件数に戻る", SiteSession.Load(sandbox).News!.Items.Count == 3);

// リンクの編集と同じファイルを触るので、両方残ること。
var together = SiteSession.Load(sandbox);
together.Groups.Single(group => group.Title == "リンク").Rows[0].Cells[0].Field.Value = "作品のまとめ";
together.News!.Items[0].Title = "見出しを変えた";
together.Save();
var bothSaved = SiteSession.Load(sandbox);
Check("リンクとお知らせを同時に直せる",
    bothSaved.Groups.Single(group => group.Title == "リンク").Rows[0].Cells[0].Field.Value == "作品のまとめ" &&
    bothSaved.News!.Items[0].Title == "見出しを変えた");

// --- v0.9：ゲーム集と各ゲームのページ --------------------------------------

var gameIndexPath = Path.Combine(sandbox, "game", "index.html");
var rinchanPath = Path.Combine(sandbox, "game", "gyugyu-rinchan", "index.html");

Check("強調の書き方を行き来できる",
    GameMarkup.ToPlain("<strong>あ</strong>い") == "**あ**い" &&
    GameMarkup.ToHtml("**あ**い") == "<strong>あ</strong>い" &&
    GameMarkup.Validate("**あ**い") == null &&
    GameMarkup.Validate("**あい") != null &&
    GameMarkup.Validate("<b>あ</b>") != null);

var v9 = SiteSession.Load(sandbox);
var games = v9.Games ?? throw new Exception("ゲーム集が読めません");
var rinchan = games.Pages.Single(page => page.Slug == "gyugyu-rinchan");
var kagamine = games.Pages.Single(page => page.Slug == "kagamine-challenge");

Check("転送だけのページは読まない", games.Pages.Count == 2);
Check("一覧のカードを読める",
    games.Collection.Cards.Count == 2 &&
    games.Collection.Cards[0].Title == "かがみねちゃれんじ" &&
    games.Collection.Cards[1].Slug == "gyugyu-rinchan" &&
    games.Collection.Cards[1].TagLines().Contains("ver 1.2") &&
    games.Collection.Cards[1].DescriptionLines().Count == 3);
Check("ひとことを読める", games.Lead.Value == "なんか、変なゲーム");
Check("ゲームのページを読める",
    rinchan.Title.Value == "ぎゅうぎゅうりんちゃん" &&
    rinchan.GameUrl.Value == "https://halkachan.github.io/gyugyu-rinchan/" &&
    rinchan.Version!.Value == "ver 1.2" &&
    rinchan.Description!.Lines().Count == 3);
Check("更新履歴を読める",
    rinchan.HasChangelog && rinchan.Changelog.Count == 3 &&
    rinchan.Changelog[0].Version == "ver 1.2" &&
    rinchan.Changelog[0].Date == "2026-09-10" &&
    rinchan.Changelog[0].Display == "2026.9.10" &&
    rinchan.Changelog[0].ItemLines().Count == 5 &&
    rinchan.Changelog[0].ItemLines()[0].StartsWith("**記録を X に") &&
    rinchan.Changelog[1].Note.StartsWith("ランキングはこの版からの"));
Check("更新履歴が無いゲームもある", !kagamine.HasChangelog && kagamine.Version == null);
Check("ゲーム集は読んだだけなら1文字も変わらない",
    games.Collection.Rebuild() == File.ReadAllText(gameIndexPath));
Check("更新履歴は読んだだけなら1文字も変わらない",
    rinchan.Rebuild() == File.ReadAllText(rinchanPath));
Check("ゲームを読んだ直後は変更なし", !games.HasChanges && !games.HasError && !v9.HasChanges);
Check("版はそろっている", !rinchan.CanAlignVersion && rinchan.VersionSummary.Contains("そろっています"));

// 新しい版を出す。
var entry = rinchan.AddNewEntry();
Check("前の版から1つ進める", entry.Version == "ver 1.3" && rinchan.Changelog[0] == entry);
Check("中身が空なら誤りとして出る", entry.HasError && games.HasError);
entry.Items = "**りんちゃんが増えました。**たくさん出ます\nおとの大きさを直しました";
entry.Note = "この版から記録が別になります。";
Check("入れれば誤りが消える", !entry.HasError && !games.HasError && games.HasChanges);
Check("版が食い違ったことに気づく",
    rinchan.CanAlignVersion && rinchan.VersionSummary.Contains("ver 1.3") &&
    rinchan.VersionSummary.Contains("ver 1.2"));

rinchan.AlignVersion();
Check("ページと一覧の札をそろえる",
    rinchan.Version!.Value == "ver 1.3" &&
    rinchan.Card!.TagLines().Contains("ver 1.3") &&
    !rinchan.Card.TagLines().Contains("ver 1.2") &&
    !rinchan.CanAlignVersion);
Check("ゲームの変更が一覧に出る",
    v9.Changes().Any(row => row.Label.Contains("更新履歴を追加") && row.After == "ver 1.3") &&
    v9.Changes().Any(row => row.Label.Contains("ゲーム集")));

// カードの追加と並べ替え。
games.Lead.Value = "なんか、変なゲームたち";
var card = games.Collection.AddNew();
card.Title = "ためしのゲーム";
card.Description = "1行目\n2行目";
card.Tags = "スマホ対応";
card.Href = "tameshi/";
games.Collection.Move(card, -1);
Check("カードを足して並べ替えられる", games.Collection.Cards[1] == card && !games.HasError);

v9.Save();

var after = SiteSession.Load(sandbox);
var gamesAfter = after.Games!;
var rinchanAfter = gamesAfter.Pages.Single(page => page.Slug == "gyugyu-rinchan");
Check("書いた更新履歴が読み直せる",
    rinchanAfter.Changelog.Count == 4 &&
    rinchanAfter.Changelog[0].Version == "ver 1.3" &&
    rinchanAfter.Changelog[0].ItemLines().Count == 2 &&
    rinchanAfter.Changelog[0].Note == "この版から記録が別になります。" &&
    rinchanAfter.Version!.Value == "ver 1.3");
Check("書いたカードが読み直せる",
    gamesAfter.Collection.Cards.Count == 3 &&
    gamesAfter.Collection.Cards[1].Title == "ためしのゲーム" &&
    gamesAfter.Collection.Cards[1].DescriptionLines().Count == 2 &&
    gamesAfter.Lead.Value == "なんか、変なゲームたち");

var gameIndexText = File.ReadAllText(gameIndexPath);
Check("番号は並び順どおりに振り直す",
    gameIndexText.IndexOf(">01</p>", StringComparison.Ordinal) <
    gameIndexText.IndexOf(">02</p>", StringComparison.Ordinal) &&
    gameIndexText.Contains(">03</p>"));
Check("強調はHTMLへ戻している",
    File.ReadAllText(rinchanPath)
        .Contains("<li><strong>りんちゃんが増えました。</strong>たくさん出ます</li>"));
Check("ゲームも保存後は組み立て直しで1文字も変わらない",
    gamesAfter.Collection.Rebuild() == File.ReadAllText(gameIndexPath) &&
    rinchanAfter.Rebuild() == File.ReadAllText(rinchanPath));

// 名前を変えると、ページの中の4か所が一度にそろう。
rinchanAfter.Title.Value = "ぎゅうぎゅうりんちゃん！";
after.Save();
var renamed = File.ReadAllText(rinchanPath);
Check("名前は4か所まとめて変わる",
    renamed.Contains("<title>ぎゅうぎゅうりんちゃん！ | HALKA</title>") &&
    renamed.Contains("<h1 id=\"game-title\">ぎゅうぎゅうりんちゃん！</h1>") &&
    renamed.Contains("data-game-title=\"ぎゅうぎゅうりんちゃん！\"") &&
    renamed.Contains("<p>ぎゅうぎゅうりんちゃん！</p>"));

// 消したあと、元に戻せること。
var shrinkGames = SiteSession.Load(sandbox);
var rinchanShrink = shrinkGames.Games!.Pages.Single(page => page.Slug == "gyugyu-rinchan");
shrinkGames.Games.Collection.Cards.Remove(
    shrinkGames.Games.Collection.Cards.Single(item => item.Title == "ためしのゲーム"));
rinchanShrink.Changelog.Remove(rinchanShrink.Changelog[0]);
Check("消すのも変更一覧に出る",
    shrinkGames.Changes().Any(row => row.Label.Contains("カードを削除")) &&
    shrinkGames.Changes().Any(row => row.Label.Contains("更新履歴を削除")));
shrinkGames.Revert();
Check("ゲームも元に戻せる", !shrinkGames.HasChanges &&
    shrinkGames.Games!.Collection.Cards.Count == 3 &&
    shrinkGames.Games.Pages.Single(page => page.Slug == "gyugyu-rinchan").Changelog.Count == 4);

// --- v1.0：ページの顔・画像の差し替え・sitemap -----------------------------

Check("PNGとGIFの形を読む",
    ImageInfo.Read(Path.Combine(sandbox, "assets", "ogp", "home.png")) is { Format: "png", Width: 1200, Height: 630 } &&
    ImageInfo.Read(Path.Combine(sandbox, "assets", "profile", "halgif1.gif")) is { Format: "gif" } &&
    ImageInfo.Read(Path.Combine(sandbox, "index.html")) == null);

var v10 = SiteSession.Load(sandbox);
var meta = v10.Meta ?? throw new Exception("ページの顔が読めません");
var metaHome = meta.Pages.Single(page => page.Url == "/");
var metaClub = meta.Pages.Single(page => page.Url == "/club/");

Check("OGPのあるページだけ並べる",
    meta.Pages.Count == 9 &&
    meta.Pages.Any(page => page.Url == "/game/gyugyu-rinchan/") &&
    meta.Pages.All(page => page.Url != "/game/mine-dungeon/"));
Check("ページの顔を読める",
    metaHome.Fields.Single(field => field.Id.EndsWith(".title")).Value == "HALKA" &&
    metaHome.Fields.Single(field => field.Id.EndsWith(".ogDescription")).Value.StartsWith("HALKAのリンク集。歌ってみた") &&
    metaHome.Fields.Single(field => field.Id.EndsWith(".ogAlt")).Value == "HALKA リンク集");
Check("説明が無いページは欄も出さない",
    metaClub.Fields.All(field => !field.Id.EndsWith(".description")) &&
    metaClub.Rows.All(row => row.Label != "検索結果の説明"));
Check("カード画像を見つける",
    metaHome.Card!.Relative == "assets/ogp/home.png" && metaHome.Card.Current!.Width == 1200);
Check("ﾊﾙｶﾁｬﾝも差し替えられる", v10.Profile!.Relative == "assets/profile/halgif1.gif");
Check("画像も公開の対象に入る",
    v10.ManagedFiles.Contains("assets/ogp/home.png") &&
    v10.ManagedFiles.Contains("assets/profile/halgif1.gif"));
Check("ページの顔を読んだ直後は変更なし", !v10.HasChanges && !v10.HasError);

// 文章を直す。
var ogDescription = metaHome.Fields.Single(field => field.Id.EndsWith(".ogDescription"));
ogDescription.Value = "HALKAの入口です。";
Check("カードの説明が変更一覧に出る",
    v10.Changes().Any(row => row.Label.Contains("カードの説明") && row.After == "HALKAの入口です。"));

// 画像の差し替え。大きさが違うものは断る。
var wrongSize = Path.Combine(sandbox, "wrong.png");
var header = new byte[32];
new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(header, 0);
new byte[] { 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 }.CopyTo(header, 8);
header[18] = 0x02;   // 幅 512
header[22] = 0x02;   // 高さ 512
File.WriteAllBytes(wrongSize, header);

metaHome.Card!.Choose(wrongSize);
Check("大きさが違うカード画像は断る",
    metaHome.Card.HasError && metaHome.Card.Error!.Contains("1200×630") && v10.HasError);
metaHome.Card.Choose(Path.Combine(sandbox, "assets", "ogp", "game.png"));
Check("形も大きさも合えば受け取る", !metaHome.Card.HasError && metaHome.Card.Changed && !v10.HasError);
Check("ﾊﾙｶﾁｬﾝにPNGは入れられない",
    !Throws(() => v10.Profile!.Choose(Path.Combine(sandbox, "assets", "ogp", "game.png"))) &&
    v10.Profile!.HasError && v10.Profile.Error!.Contains("GIF"));
v10.Profile.Clear();
Check("選び直しは取り消せる", !v10.Profile.Changed && !v10.HasError);

var homeCardBefore = CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "index.html")), "assets/ogp/home.png");
v10.Save();

Check("画像が写っている",
    File.ReadAllBytes(Path.Combine(sandbox, "assets", "ogp", "home.png")).SequenceEqual(
        File.ReadAllBytes(Path.Combine(sandbox, "assets", "ogp", "game.png"))));
Check("画像を替えたら番号が上がる",
    CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "index.html")), "assets/ogp/home.png")
        == homeCardBefore + 1);
Check("写したあとは待ちが無い", !v10.HasChanges && !metaHome.Card.Changed);

var metaAfter = SiteSession.Load(sandbox).Meta!;
Check("直した文章が読み直せる",
    metaAfter.Pages.Single(page => page.Url == "/").Fields
        .Single(field => field.Id.EndsWith(".ogDescription")).Value == "HALKAの入口です。");

// ﾊﾙｶﾁｬﾝは index.html と script.js の2か所から読まれている。
var gifSession = SiteSession.Load(sandbox);
var gifCopy = Path.Combine(sandbox, "newgif.gif");
File.Copy(Path.Combine(sandbox, "assets", "profile", "halgif1.gif"), gifCopy, overwrite: true);
var indexGifBefore = CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "index.html")), "assets/profile/halgif1.gif");
var scriptGifBefore = CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "script.js")), "assets/profile/halgif1.gif");
gifSession.Profile!.Choose(gifCopy);
gifSession.Save();
Check("ﾊﾙｶﾁｬﾝは読んでいる2か所とも番号が上がる",
    CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "index.html")), "assets/profile/halgif1.gif")
        == indexGifBefore + 1 &&
    CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "script.js")), "assets/profile/halgif1.gif")
        == scriptGifBefore + 1);

// sitemap と robots。
var urls = Sitemap.Urls(sandbox);
Check("ドメインはCNAMEから", Sitemap.Origin(sandbox) == "https://halkaclub.com");
Check("検索に載せるページだけ並べる",
    urls.Contains("/") && urls.Contains("/works/") && urls.Contains("/game/") &&
    urls.Contains("/game/gyugyu-rinchan/") &&
    !urls.Contains("/club/") &&          // noindex なので載せない
    !urls.Contains("/game/mine-dungeon/"));   // 転送するだけのページ
Check("公開の形にしたページは載る", urls.Contains("/utamaze/"));   // 前の節で noindex を外しました

var sitemap = Sitemap.BuildSitemap(sandbox);
Check("sitemapの形",
    sitemap.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>") &&
    sitemap.Contains("<url><loc>https://halkaclub.com/</loc></url>") &&
    sitemap.TrimEnd().EndsWith("</urlset>"));
Check("robotsは載せないページを断る",
    Sitemap.BuildRobots(sandbox).Contains("Disallow: /club/") &&
    Sitemap.BuildRobots(sandbox).Contains("Sitemap: https://halkaclub.com/sitemap.xml"));

Check("2つのファイルを書き出す",
    Sitemap.Write(sandbox).OrderBy(name => name, StringComparer.Ordinal)
        .SequenceEqual(new[] { "robots.txt", "sitemap.xml" }) &&
    File.Exists(Path.Combine(sandbox, "sitemap.xml")));
Check("中身が同じなら書き直さない", Sitemap.Write(sandbox).Count == 0);

// カード画像の作り直し。本物の generate-ogp.py は Pillow などが要るので、
// ここでは同じ場所に置いた短い Python で、動かす仕組みだけを確かめます。
var pyRoot = Path.Combine(Path.GetTempPath(), "halka-site-editor-py-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(pyRoot, "tools", "ogp"));
Directory.CreateDirectory(Path.Combine(pyRoot, "assets", "ogp"));
File.WriteAllText(Path.Combine(pyRoot, "assets", "ogp", "a.png"), "まえ");
File.WriteAllText(Path.Combine(pyRoot, "assets", "ogp", "b.png"), "そのまま");

Check("スクリプトが無ければ動かさない",
    !CardImages.IsAvailable(pyRoot) &&
    !CardImages.Run(pyRoot, new[] { "assets/ogp/a.png" }).Ok);

File.WriteAllText(Path.Combine(pyRoot, "tools", "ogp", "generate-ogp.py"),
    "from pathlib import Path\n" +
    "Path('assets/ogp/a.png').write_text('あと', encoding='utf-8')\n" +
    "print('できました')\n");

var cards = CardImages.Run(pyRoot, new[] { "assets/ogp/a.png", "assets/ogp/b.png" });
if (cards.Output.Contains("Python が見つかりません"))
{
    Console.WriteLine("SKIP カード画像の作り直し（このPCに Python がありません）");
}
else
{
    Check("作り直して、変わった画像だけを返す",
        cards.Ok && cards.Output.Contains("できました") &&
        cards.Changed.SequenceEqual(new[] { "assets/ogp/a.png" }));
}
try { Directory.Delete(pyRoot, recursive: true); } catch (Exception) { }

// 作り直した印を付けると、番号を上げる対象になる。
var regen = SiteSession.Load(sandbox);
var regenCard = regen.Meta!.Pages.Single(page => page.Url == "/works/").Card!;
var worksBefore = CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "works", "index.html")),
    "assets/ogp/works.png");
regenCard.MarkRegenerated();
Check("作り直しは変更一覧に出る",
    regenCard.Changed && regen.HasChanges &&
    regen.Changes().Any(row => row.Label.Contains("作り直し")));
regen.Save();
Check("作り直したら番号が上がる",
    CacheBuster.Current(File.ReadAllText(Path.Combine(sandbox, "works", "index.html")), "assets/ogp/works.png")
        == worksBefore + 1 &&
    !regen.HasChanges);

// --- v1.1：リンク集と、作品の分類 -------------------------------------------

var v11 = SiteSession.Load(sandbox);
var grid = v11.Links ?? throw new Exception("リンク集が読めません");
var indexPath = Path.Combine(sandbox, "index.html");

Check("リンク集のカードを読める",
    grid.Cards.Count >= 10 &&
    grid.Cards[0].Name == "YouTube" && grid.Cards[0].Main &&
    grid.Cards[0].Mark == "♪" && grid.Cards[0].Note == "主な活動はここ" &&
    grid.Cards[0].External &&
    grid.Cards.Any(card => card.Name == "ゲーム置き場" && !card.External));
Check("大きいリンクとくらぶは、リンク集に入れない",
    grid.Cards.All(card => card.Name != "作品一覧" && card.Name != "はるかくらぶ"));
Check("リンク集は読んだだけなら1文字も変わらない", grid.Rebuild() == File.ReadAllText(indexPath));
Check("リンク集を読んだ直後は変更なし", !grid.HasChanges && !grid.HasError && !v11.HasChanges);

var newLink = grid.AddNew();
newLink.Mark = "新";
newLink.Name = "ためしのリンク";
newLink.Note = "ためし";
newLink.Href = "https://example.com/";
newLink.Aria = "ためしのリンクを開く";
grid.Move(newLink, -3);
Check("リンクを足して並べ替えられる",
    grid.Cards[grid.Cards.Count - 4] == newLink && !grid.HasError &&
    v11.Changes().Any(row => row.Label.Contains("リンク集：追加") && row.After == "ためしのリンク"));

newLink.Href = "だめ な URL";
Check("おかしなリンク先は誤りとして出る", newLink.HasError && v11.HasError);
newLink.Href = "https://example.com/";

// 作品の分類。
var v11Works = v11.Works;
Check("分類を読める",
    v11Works.Categories.Count == 4 &&
    v11Works.Categories[0].Id == "utattemita" &&
    v11Works.Categories[0].Name == "歌ってみた" &&
    v11Works.Categories[0].Description.StartsWith("Arrange cover") &&
    v11Works.Categories[0].Display.StartsWith("歌ってみた（"));
Check("分類ごと読んだだけなら1文字も変わらない",
    v11Works.Serialize() == File.ReadAllText(Path.Combine(sandbox, "works", "works-data.js")));

var newCategory = v11Works.AddNewCategory();
newCategory.Name = "ためしシリーズ";
newCategory.Description = "ためし";
newCategory.Id = "tameshi";
newCategory.Works.Add(new WorkItem("ためしの曲", "2026-10-07", "https://youtu.be/RFQw7HejNZ0"));
v11Works.MoveCategory(newCategory, -1);
Check("分類を足して並べ替えられる",
    v11Works.Categories[3] == newCategory && v11Works.HasChanges && !v11Works.HasError &&
    v11.Changes().Any(row => row.Label.Contains("分類を追加") && row.After == "ためしシリーズ"));

newCategory.Id = "ためし";
Check("合い言葉は英小文字だけ", newCategory.HasError && v11Works.HasError);
newCategory.Id = "tameshi";

v11Works.Categories[0].Name = "歌ってみた！";
Check("名前を変えると並びの表示も変わる",
    v11Works.Categories[0].Display.StartsWith("歌ってみた！（") &&
    v11.Changes().Any(row => row.Label.Contains("分類を変更") && row.After == "歌ってみた！"));

v11.Save();

var afterV11 = SiteSession.Load(sandbox);
Check("足したリンクが読み直せる",
    afterV11.Links!.Cards.Any(card => card.Name == "ためしのリンク" && card.Mark == "新") &&
    File.ReadAllText(indexPath).Contains(
        "<a class=\"link-card\" href=\"https://example.com/\" target=\"_blank\" rel=\"noreferrer\" aria-label=\"ためしのリンクを開く\">"));
Check("うたまぜ！の印はそのまま残る",
    File.ReadAllText(indexPath).Contains("<!-- utamaze-link:start") &&
    File.ReadAllText(indexPath).Contains("<!-- utamaze-link:end -->"));
Check("足した分類が読み直せる",
    afterV11.Works.Categories.Count == 5 &&
    afterV11.Works.Categories[3].Id == "tameshi" &&
    afterV11.Works.Categories[3].Works.Single().Title == "ためしの曲" &&
    afterV11.Works.Categories[0].Name == "歌ってみた！");
Check("保存後も組み立て直しで1文字も変わらない",
    afterV11.Links!.Rebuild() == File.ReadAllText(indexPath) &&
    afterV11.Works.Serialize() == File.ReadAllText(Path.Combine(sandbox, "works", "works-data.js")));

// 消して、元に戻せること。
var shrinkV11 = SiteSession.Load(sandbox);
shrinkV11.Links!.Cards.Remove(shrinkV11.Links.Cards.Single(card => card.Name == "ためしのリンク"));
shrinkV11.Works.Categories.Remove(shrinkV11.Works.Categories.Single(category => category.Id == "tameshi"));
Check("消すのも変更一覧に出る",
    shrinkV11.Changes().Any(row => row.Label.Contains("リンク集：削除")) &&
    shrinkV11.Changes().Any(row => row.Label.Contains("分類を削除")));
shrinkV11.Revert();
Check("リンクと分類も元に戻せる", !shrinkV11.HasChanges &&
    shrinkV11.Links!.Cards.Any(card => card.Name == "ためしのリンク") &&
    shrinkV11.Works.Categories.Count == 5);

// --- v1.2：うたまぜ！のページの文章 ------------------------------------------

var v12 = SiteSession.Load(sandbox);
var utamazeText = v12.UtamazeText ?? throw new Exception("うたまぜ！の文章が読めません");
var utaTextPage = utamazeText.Pages.Single();
var utamazeHtml = Path.Combine(sandbox, "utamaze", "index.html");

Check("うたまぜ！の文章を読める",
    utaTextPage.Url == "/utamaze/" && utaTextPage.Blocks.Count >= 20 &&
    utaTextPage.Blocks.Any(block => block.Text == "歌のMIXを、もっとわかりやすく。") &&
    utaTextPage.Blocks.Any(block => block.Text.StartsWith("はじめてでも触りやすく")));
Check("FAQの質問が見出しになる",
    utaTextPage.Blocks.Any(block => block.Label.Contains("PROは何台のPCで使えますか？")));
Check("見出しの中の改行も読める",
    utaTextPage.Blocks.Any(block => block.Text == "まずはかんたん。\nもっと詰めたい人にはPRO。" ||
        block.Text == "まずはかんたん。\n必要なら、ちゃんと細かく。"));
Check("リンクが入っている文章には触らない",
    utaTextPage.Blocks.All(block => !block.Text.Contains('<')) &&
    utaTextPage.Blocks.All(block => !block.Text.Contains("お問い合わせフォーム")));
Check("公開準備中の一文は、うたまぜ！タブの受け持ち",
    utaTextPage.Blocks.All(block => !block.Text.Contains("現在は公開準備中")));
Check("うたまぜ！は組み立て直しても1文字も変わらない",
    utamazeText.Rebuild(utaTextPage) == utaTextPage.CurrentText);
Check("うたまぜ！を読んだ直後は変更なし", !utamazeText.HasChanges && !v12.HasChanges);

var lead = utaTextPage.Blocks.Single(block => block.Text == "歌のMIXを、もっとわかりやすく。");
lead.Text = "歌のMIXを、もっとかんたんに。";
var heading = utaTextPage.Blocks.First(block => block.Text.Contains("もっと詰めたい人にはPRO。"));
heading.Text = "まずはFREEから。\nもっと詰めたい人にはPRO。";
Check("直すと変更一覧に出る",
    utamazeText.HasChanges && !utamazeText.HasError &&
    v12.Changes().Any(row => row.Label.Contains("うたまぜ！")));

v12.Save();

var afterV12 = SiteSession.Load(sandbox);
var afterUtamaze = afterV12.UtamazeText!.Pages.Single();
Check("直した文章が読み直せる",
    afterUtamaze.Blocks.Any(block => block.Text == "歌のMIXを、もっとかんたんに。") &&
    File.ReadAllText(utamazeHtml).Contains("<p class=\"hero-lead\">歌のMIXを、もっとかんたんに。</p>"));
Check("見出しの改行は <br> で戻る",
    File.ReadAllText(utamazeHtml).Contains("<h2>まずはFREEから。<br>もっと詰めたい人にはPRO。</h2>"));
Check("うたまぜ！も保存後は組み立て直しで1文字も変わらない",
    afterV12.UtamazeText!.Rebuild(afterUtamaze) == afterUtamaze.CurrentText);
Check("文章を直せるページが4枚ならぶ",
    afterV12.TextPages.Count == 4 &&
    afterV12.TextPages.Select(page => page.Url)
        .SequenceEqual(new[] { "/commission/", "/commission/en/", "/utamaze/", "/club/" }));

// --- v1.3：1手ずつ戻す・保存の控え・近道 ------------------------------------

var undo = new UndoStack();
Check("何も無ければ戻せない", !undo.CanUndo && undo.NextLabel == null && undo.Undo() == null);

var marks = new List<string>();
undo.Record("ひとつめ", () => marks.Add("ひとつめ"));
undo.Record("ふたつめ", () => marks.Add("ふたつめ"));
Check("新しい手から名前が出る", undo.CanUndo && undo.NextLabel == "ふたつめ" && undo.Count == 2);
Check("新しい手から戻す",
    undo.Undo() == "ふたつめ" && marks.SequenceEqual(new[] { "ふたつめ" }) && undo.NextLabel == "ひとつめ");
undo.Clear();
Check("まとめて忘れられる", !undo.CanUndo && undo.Count == 0);

for (var i = 0; i < UndoStack.Depth + 5; i++)
{
    var at = i;
    undo.Record($"{at}", () => { });
}
Check("古い手は捨てる", undo.Count == UndoStack.Depth && undo.NextLabel == $"{UndoStack.Depth + 4}");

// 実際の編集で、削除を戻せること。
var undoSession = SiteSession.Load(sandbox);
var undoCategory = undoSession.Works.Categories.First();
var dropped = undoCategory.Works[0];
var droppedAt = 0;
undoCategory.Works.RemoveAt(droppedAt);
undoSession.Undo.Record($"作品「{dropped.Title}」の削除", () => undoCategory.Works.Insert(droppedAt, dropped));
Check("消したものを戻せる",
    undoSession.HasChanges && undoSession.Undo.Undo() != null &&
    undoCategory.Works[0] == dropped && !undoSession.HasChanges);

// 保存の控え。本物の %LOCALAPPDATA% は汚さず、一時フォルダーで確かめます。
var backupFolder = Path.Combine(Path.GetTempPath(), "halka-site-editor-backup-" + Guid.NewGuid().ToString("N"));
var backups = new SaveBackups(backupFolder);
Check("はじめは控えが無い", backups.Latest == null && backups.LatestLabel == null &&
    backups.Restore(sandbox).Count == 0);

var backupSession = SiteSession.Load(sandbox, backups);
var yellowField = backupSession.Groups.Single(group => group.Title == "サイトの基本色")
    .Fields.Single(field => field.Label.StartsWith("黄色"));
var yellowBefore = yellowField.Value;
yellowField.Value = "#123456";
backupSession.Save();

Check("保存すると控えが残る",
    backups.Latest != null && backups.LatestLabel!.Contains("ファイル") &&
    File.ReadAllText(Path.Combine(backups.Latest!, "style.css")).Contains($"--yellow: {yellowBefore};"));
Check("変えた色がファイルに入っている",
    File.ReadAllText(Path.Combine(sandbox, "style.css")).Contains("--yellow: #123456;"));
Check("保存すると戻せる手は消える", !backupSession.Undo.CanUndo);

var backupRestored = backups.Restore(sandbox);
// 色を変えると、style.css を読んでいるページの ?v= も上がるので、控えはそのぶんも含みます。
Check("控えから書き戻せる",
    backupRestored.Contains("style.css") && backupRestored.Contains("index.html") &&
    File.ReadAllText(Path.Combine(sandbox, "style.css")).Contains($"--yellow: {yellowBefore};"));
Check("戻したらその控えは消える", backups.Latest == null);

// 控えは、変わるファイルだけを対象にする。
var quiet = SiteSession.Load(sandbox, backups);
quiet.Save();
Check("変更が無ければ控えも取らない", backups.Latest == null);

// 古い控えは捨てる。
for (var i = 0; i < SaveBackups.Keep + 3; i++)
{
    Directory.CreateDirectory(Path.Combine(backupFolder, $"2026010{i / 10}-00000{i % 10}"));
    File.WriteAllText(Path.Combine(backupFolder, $"2026010{i / 10}-00000{i % 10}", "a.txt"), "x");
}
backups.Take(sandbox, new[] { "style.css" });
Check("控えは決めた数だけ残す",
    Directory.GetDirectories(backupFolder).Length == SaveBackups.Keep);
try { Directory.Delete(backupFolder, recursive: true); } catch (Exception) { }

// 近道の置き場所。作るところまでは、このテストでは触りません。
// 近道そのものを作るのはWindowsのしくみを使うので、ここでは置き場所だけ確かめます。
Check("近道の置き場所",
    Shortcut.DesktopPath.EndsWith("HALKA SITE EDITOR.lnk") &&
    Shortcut.StartMenuPath.EndsWith("HALKA SITE EDITOR.lnk") &&
    Shortcut.DesktopPath != Shortcut.StartMenuPath);

// 控えは、サイトのフォルダーごとに分かれていること。
var mine = SaveBackups.For(sandbox);
var other = SaveBackups.For(Path.Combine(Path.GetTempPath(), "halka-another-site"));
Check("サイトごとに控えを分ける",
    mine.Folder != other.Folder && mine.SiteRoot == Path.GetFullPath(sandbox));

// --- 同じファイルを何か所からも直す ----------------------------------------
// index.html は「リンク」「うれしいこと」「ページの顔」から直せます。
// 同じファイルを2回開いていると、片方の書き込みがもう片方に消されます。

var sameFile = SiteSession.Load(sandbox);
Check("同じファイルは1回だけ開く",
    sameFile.ManagedFiles.Count(path => path == "index.html") == 1);

sameFile.Groups.Single(group => group.Title == "リンク").Rows[0].Cells[0].Field.Value = "同時に直す名前";
sameFile.Meta!.Pages.Single(page => page.Url == "/").Fields
    .Single(field => field.Id.EndsWith(".ogDescription")).Value = "同時に直す説明";
sameFile.News!.Items[0].Title = "同時に直す見出し";
sameFile.Save();

var sameFileText = File.ReadAllText(Path.Combine(sandbox, "index.html"));
Check("3か所を同時に直しても、どれも消えない",
    sameFileText.Contains("同時に直す名前") &&
    sameFileText.Contains("同時に直す説明") &&
    sameFileText.Contains("同時に直す見出し"));

// --- v1.4：公開前の点検 -----------------------------------------------------

var checkBackups = new SaveBackups(Path.Combine(Path.GetTempPath(), "halka-check-" + Guid.NewGuid().ToString("N")));
var checkSession = SiteSession.Load(sandbox, checkBackups);
var clean = SiteCheck.Run(checkSession);

Check("行き先を見つけられる",
    SiteCheck.Resolve(sandbox, Path.Combine(sandbox, "index.html"), "works/index.html") != null &&
    SiteCheck.Resolve(sandbox, Path.Combine(sandbox, "index.html"), "commission/") != null &&
    SiteCheck.Resolve(sandbox, Path.Combine(sandbox, "works", "index.html"), "../style.css") != null &&
    SiteCheck.Resolve(sandbox, Path.Combine(sandbox, "index.html"), "/favicon.png?v=2") == null &&
    SiteCheck.Resolve(sandbox, Path.Combine(sandbox, "index.html"), "../../windows/win.ini") == null);
// このサンドボックスはページだけを写したものなので、CSSなどは欠けています。
// ここで見るのは「ちゃんとある行き先を、まちがって出さないこと」です。
Check("ちゃんとある行き先は出さない",
    clean.All(issue => issue.Detail != "works/index.html" && issue.Detail != "../index.html" &&
        issue.Detail != "commission/" && issue.Detail != "../style.css"));

// わざと壊して、見つけられること。
var broken = Path.Combine(sandbox, "works", "index.html");
var brokenBefore = File.ReadAllText(broken);
File.WriteAllText(broken, brokenBefore.Replace(
    "<a class=\"small-sign\" href=\"../index.html\"", "<a class=\"small-sign\" href=\"../ないページ.html\""));
var withBroken = SiteCheck.Run(SiteSession.Load(sandbox, checkBackups));
Check("リンク切れを見つける",
    withBroken.Any(issue => issue.What == "リンク切れ" && issue.Detail == "../ないページ.html" &&
        issue.File == "works/index.html" && issue.Level == CheckLevel.Problem));
File.WriteAllText(broken, brokenBefore);

// HTMLのコメントの中は、まだ書いていない覚え書きなので見ない。
var commented = Path.Combine(sandbox, "club", "index.html");
var commentedBefore = File.ReadAllText(commented);
File.WriteAllText(commented, commentedBefore.Replace("</head>",
    "  <!-- あとで <a href=\"（これから決めるURL）\"> に替えます -->\n  </head>"));
Check("コメントの中は見ない",
    SiteCheck.Run(SiteSession.Load(sandbox, checkBackups))
        .All(issue => !issue.Detail.Contains("これから決める")));
File.WriteAllText(commented, commentedBefore);

// 名前が同じでも、別のファイルを指していれば別もの扱い。
Check("同じ名前の別ファイルは混ぜない",
    clean.All(issue => issue.File != "utamaze/index.html" || !issue.Detail.StartsWith("style.css")));

// ?v= の付け忘れを見つける。
var noVersion = Path.Combine(sandbox, "works", "index.html");
File.WriteAllText(noVersion, System.Text.RegularExpressions.Regex.Replace(
    brokenBefore, @"works-data\.js\?v=\d+", "works-data.js"));
Check("?v= の付け忘れを見つける",
    SiteCheck.Run(SiteSession.Load(sandbox, checkBackups)).Any(issue =>
        issue.What == "?v= が付いていません" && issue.Detail.StartsWith("works/works-data.js") &&
        issue.File == "works/index.html"));
File.WriteAllText(noVersion, brokenBefore);

// sitemap が合っていなければ知らせる。
var sitemapPath = Path.Combine(sandbox, "sitemap.xml");
Sitemap.Write(sandbox);
File.WriteAllText(sitemapPath, File.ReadAllText(sitemapPath)
    .Replace("</urlset>", "  <url><loc>https://halkaclub.com/nowhere/</loc></url></urlset>"));
Check("sitemapのずれを知らせる",
    SiteCheck.Run(SiteSession.Load(sandbox, checkBackups))
        .Any(issue => issue.What.Contains("sitemap.xml")));
Sitemap.Write(sandbox);
Check("作り直せば、そのお知らせは消える",
    SiteCheck.Run(SiteSession.Load(sandbox, checkBackups))
        .All(issue => !issue.What.Contains("sitemap.xml")));
Check("改行コードは変えない",
    Sitemap.Write(sandbox).Count == 0);

try { Directory.Delete(checkBackups.Folder, recursive: true); } catch (Exception) { }

// --- v1.5：くらぶの文章・飾りの文字・ゲームのカード画像 ----------------------

var v15 = SiteSession.Load(sandbox, new SaveBackups(
    Path.Combine(Path.GetTempPath(), "halka-v15-" + Guid.NewGuid().ToString("N"))));
var clubText = v15.ClubText ?? throw new Exception("くらぶの文章が読めません");
var clubPage = clubText.Pages.Single();
var clubHtml = Path.Combine(sandbox, "club", "index.html");

Check("くらぶの文章を読める",
    clubPage.Url == "/club/" && clubPage.Blocks.Count >= 6 &&
    clubPage.Blocks.Any(block => block.Text.StartsWith("ここから先はYouTubeメンバーシップ")) &&
    clubPage.Blocks.Any(block => block.Text.Contains("リンク、配布ファイルの外部共有は禁止です。")));
Check("利用ルールは箇条書きとして読める",
    clubPage.Blocks.Single(block => block.Label.Contains("【利用ルール】") && block.Label.EndsWith("箇条書き"))
        .Text.Split("\n\n").Length == 3);
Check("入口の注意は改行ごと読める",
    clubPage.Blocks.Any(block => block.Label.EndsWith("入口の注意") &&
        block.Text == "パスワードはYouTubeのメンバーシップ\n「はるかくらぶ」のコミュニティにあります"));
Check("更新内容は、くらぶタブの受け持ちなので出てこない",
    clubPage.Blocks.All(block => !block.Text.Contains("ページ作成")));
Check("くらぶは組み立て直しても1文字も変わらない",
    clubText.Rebuild(clubPage) == clubPage.CurrentText);
Check("くらぶを読んだ直後は変更なし", !clubText.HasChanges && !v15.HasChanges);

// 飾りの文字。
var decor = v15.Groups.Single(group => group.Title == "飾りの文字");
Check("テープの文字を読める",
    decor.Rows.Any(row => row.Label == "index.html" &&
        row.Cells[0].Field.Value == "HALKA" && row.Cells[1].Field.Value == "tap a note to open ↗") &&
    decor.Rows.Any(row => row.Label == "club/index.html" &&
        row.Cells[0].Field.Value == "HALKA CLUB"));
Check("トップページの下も読める",
    decor.Rows.Single(row => row.Label == "トップページの下").Cells[0].Field.Value == "© 2026 HALKA");

// 直して、保存して、読み直せること。
var rules = clubPage.Blocks.Single(block =>
    block.Label.Contains("【利用ルール】") && block.Label.EndsWith("箇条書き"));
rules.Text = rules.Text + "\n\nためしのルール。";
decor.Rows.Single(row => row.Label == "index.html").Cells[1].Field.Value = "tap a note ↗";
v15.Save();

var afterV15 = SiteSession.Load(sandbox, v15.Backups);
Check("くらぶの文章が読み直せる",
    afterV15.ClubText!.Pages.Single().Blocks.Any(block => block.Text.EndsWith("ためしのルール。")) &&
    File.ReadAllText(clubHtml).Contains("<li>ためしのルール。</li>"));
Check("飾りの文字が読み直せる",
    afterV15.Groups.Single(group => group.Title == "飾りの文字").Rows
        .Single(row => row.Label == "index.html").Cells[1].Field.Value == "tap a note ↗");
Check("くらぶも保存後は組み立て直しで1文字も変わらない",
    afterV15.ClubText!.Rebuild(afterV15.ClubText.Pages.Single()) ==
        afterV15.ClubText.Pages.Single().CurrentText);
Check("くらぶの更新内容は、別のところから直せたまま",
    afterV15.ClubUpdate!.Text.Contains("ページ作成"));

// ゲームのカード画像。
Check("ゲームごとのカード画像がある",
    afterV15.Meta!.Pages.Single(page => page.Url == "/game/gyugyu-rinchan/").Card!.Relative
        == "assets/ogp/game-gyugyu-rinchan.png" &&
    afterV15.Meta.Pages.Single(page => page.Url == "/game/kagamine-challenge/").Card!.Current
        is { Width: 1200, Height: 630 });

// --- プレビュー用サーバー -------------------------------------------------

Check("URLから実ファイルへ",
    PreviewServer.ResolveFile(sandbox, "/") == Path.Combine(sandbox, "index.html") &&
    PreviewServer.ResolveFile(sandbox, "/commission/") == Path.Combine(sandbox, "commission", "index.html") &&
    PreviewServer.ResolveFile(sandbox, "/commission/en/") == Path.Combine(sandbox, "commission", "en", "index.html") &&
    PreviewServer.ResolveFile(sandbox, "/style.css") == Path.Combine(sandbox, "style.css"));
Check("無いものは配らない",
    PreviewServer.ResolveFile(sandbox, "/nothing.html") == null &&
    PreviewServer.ResolveFile(sandbox, "/halkaworld/") == null);
Check("フォルダーの外へは出られない",
    PreviewServer.ResolveFile(sandbox, "/../../windows/win.ini") == null &&
    PreviewServer.ResolveFile(sandbox, "/commission/../../..") == null);

Check("出せるページだけ並べる",
    SitePages.ForSite(sandbox).Select(page => page.Url).SequenceEqual(
        new[] { "/", "/commission/", "/commission/en/", "/works/", "/club/", "/utamaze/", "/game/",
                "/game/gyugyu-rinchan/", "/game/kagamine-challenge/" }));
Check("転送だけのページは並べない",
    SitePages.ForSite(sandbox).All(page => page.Url != "/game/mine-dungeon/"));
Check("タブに合うページを選ぶ",
    SitePages.ForGroup(SitePages.ForSite(sandbox), "依頼ページ")!.Url == "/commission/" &&
    SitePages.ForGroup(SitePages.ForSite(sandbox), "ゲーム")!.Url == "/game/" &&
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

// --- 公開（コミットしてpush） ---------------------------------------------
// 一時的に本物のgitリポジトリと、その送り先（bare）を作って確かめます。

var gitRoot = Path.Combine(Path.GetTempPath(), "halka-site-editor-git-" + Guid.NewGuid().ToString("N"));
var remote = Path.Combine(Path.GetTempPath(), "halka-site-editor-remote-" + Guid.NewGuid().ToString("N") + ".git");
CopySite(real, gitRoot);
Directory.CreateDirectory(Path.Combine(gitRoot, "unity-project"));
File.WriteAllText(Path.Combine(gitRoot, "unity-project", "other.txt"), "ユーザーの別作業\n");

Git(gitRoot, "init", "-b", "main");
Git(gitRoot, "config", "user.email", "test@example.com");
Git(gitRoot, "config", "user.name", "test");
Git(gitRoot, "add", "-A");
Git(gitRoot, "commit", "-m", "first");
Git(Path.GetTempPath(), "init", "--bare", remote);
Git(gitRoot, "remote", "add", "origin", remote);
Git(gitRoot, "push", "-u", "origin", "main");

static void Git(string cwd, params string[] args)
{
    var info = new System.Diagnostics.ProcessStartInfo("git")
    {
        WorkingDirectory = cwd, CreateNoWindow = true, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true,
    };
    foreach (var argument in args) info.ArgumentList.Add(argument);
    using var process = System.Diagnostics.Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new Exception($"テストの下ごしらえで git {string.Join(' ', args)} が失敗しました：\n" +
            output.Result + error.Result);
    }
}

var publisher = new GitPublisher(gitRoot);
var gitSession = SiteSession.Load(gitRoot);

Check("gitのリポジトリだと分かる", publisher.IsAvailable && publisher.Branch() == "main");
Check("何も変えていなければ公開するものが無い", publisher.Pending(gitSession.ManagedFiles).Count == 0);

// ツールが扱うファイルと、扱わないファイルの両方を変えます。
gitSession.Groups.Single(group => group.Title == "サイトの基本色")
    .Fields.Single(field => field.Label.StartsWith("黄色")).Value = "#123456";
gitSession.Save();
File.WriteAllText(Path.Combine(gitRoot, "unity-project", "other.txt"), "ユーザーが作業中\n");

var pending = publisher.Pending(gitSession.ManagedFiles);
// 色を変えると、style.css を読んでいるページの ?v= も上がります。
// 大事なのは、作業中の unity-project が巻き込まれないことです。
Check("公開対象はツールが扱うファイルだけ",
    pending.Any(file => file.Path == "style.css" && file.Status == "変更") &&
    pending.All(file => !file.Path.StartsWith("unity-project/")));
Check("メッセージの下書きができる",
    GitPublisher.SuggestMessage(new[] { new PendingFile("style.css", "変更") }) == "配色を更新" &&
    GitPublisher.SuggestMessage(new[]
    {
        new PendingFile("commission/index.html", "変更"),
        new PendingFile("commission/en/index.html", "変更"),
        new PendingFile("works/works-data.js", "変更"),
    }) == "依頼ページ・作品一覧を更新" &&
    GitPublisher.SuggestMessage(Array.Empty<PendingFile>()) == "サイトを更新");

var published = publisher.Publish(gitSession.ManagedFiles, "配色を更新");
Check("公開できる", published.Ok);
Check("向こうに届いている",
    RevisionCount(remote) == 2 && FileAt(remote, "style.css").Contains("--yellow: #123456;"));
Check("作業中の別ファイルは巻き込まない",
    File.ReadAllText(Path.Combine(gitRoot, "unity-project", "other.txt")).Contains("作業中") &&
    !FileAt(remote, "unity-project/other.txt").Contains("作業中"));
Check("公開したあとは待ちが無い", publisher.Pending(gitSession.ManagedFiles).Count == 0);
Check("何も無い状態では公開しない",
    !publisher.Publish(gitSession.ManagedFiles, "からっぽ").Ok);

static int RevisionCount(string repo)
{
    var info = new System.Diagnostics.ProcessStartInfo("git")
    { WorkingDirectory = repo, CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
    info.ArgumentList.Add("rev-list"); info.ArgumentList.Add("--count"); info.ArgumentList.Add("main");
    using var process = System.Diagnostics.Process.Start(info)!;
    var text = process.StandardOutput.ReadToEnd().Trim();
    process.WaitForExit();
    return int.TryParse(text, out var count) ? count : -1;
}

static string FileAt(string repo, string path)
{
    var info = new System.Diagnostics.ProcessStartInfo("git")
    {
        WorkingDirectory = repo, CreateNoWindow = true, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true,
        StandardOutputEncoding = System.Text.Encoding.UTF8,
    };
    info.ArgumentList.Add("show"); info.ArgumentList.Add("main:" + path);
    using var process = System.Diagnostics.Process.Start(info)!;
    var text = process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return text;
}

// 向こうに新しいコミットがある状態をつくり、押し返しと取り込みを確かめます。
var otherClone = Path.Combine(Path.GetTempPath(), "halka-site-editor-other-" + Guid.NewGuid().ToString("N"));
Git(Path.GetTempPath(), "clone", "-b", "main", remote, otherClone);
Git(otherClone, "config", "user.email", "other@example.com");
Git(otherClone, "config", "user.name", "other");
File.WriteAllText(Path.Combine(otherClone, "from-other.txt"), "別の場所からの追記\n");
Git(otherClone, "add", "from-other.txt");
Git(otherClone, "commit", "-m", "from other place");
Git(otherClone, "push", "origin", "main");
Check("別の場所からの変更が向こうに入っている", RevisionCount(remote) == 3);

gitSession.Groups.Single(group => group.Title == "サイトの基本色")
    .Fields.Single(field => field.Label.StartsWith("黄色")).Value = "#abcdef";
gitSession.Save();
var rejected = publisher.Publish(gitSession.ManagedFiles, "もう一度");
Check("向こうが進んでいたら押し返される", !rejected.Ok && rejected.RejectedByRemote);
Check("取り込んでから公開し直せる", publisher.PullRebaseAndPush().Ok &&
    FileAt(remote, "style.css").Contains("--yellow: #abcdef;") &&
    FileAt(remote, "from-other.txt").Contains("別の場所からの追記"));
Check("取り込んだあとも作業中のファイルは手元に残る",
    File.ReadAllText(Path.Combine(gitRoot, "unity-project", "other.txt")).Contains("作業中") &&
    !FileAt(remote, "unity-project/other.txt").Contains("作業中"));

foreach (var path in new[] { gitRoot, remote, otherClone })
{
    try { DeleteTree(path); } catch (Exception) { }
}

static void DeleteTree(string path)
{
    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        File.SetAttributes(file, FileAttributes.Normal);
    Directory.Delete(path, recursive: true);
}

Directory.Delete(sandbox, recursive: true);
Console.WriteLine($"\n{passes} passed");
