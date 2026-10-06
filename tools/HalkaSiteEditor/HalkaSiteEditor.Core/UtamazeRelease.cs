using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 「準備中」と「公開」を行き来する切り替え1つ。
/// いまどちらの形になっているかをファイルから読み取り、選ばれた側へ入れ替えます。
/// </summary>
public sealed class ReleaseSwitch : INotifyPropertyChanged
{
    private readonly SiteFile file;
    private readonly UtamazeRelease owner;
    private readonly Func<string, bool> hasDraft;
    private readonly Func<string, bool> hasLive;
    private readonly Func<string, UtamazeRelease, string> toLive;
    private readonly Func<string, UtamazeRelease, string> toDraft;
    private readonly Func<UtamazeRelease, string?> missing;

    private bool makeLive;

    public string Label { get; }
    public string Note { get; }
    public string FileRelative { get; }

    /// <summary>出し入れでページの作りが変わるもの（保存後に読み込み直しが要る）。</summary>
    public bool ChangesStructure { get; }

    internal ReleaseSwitch(UtamazeRelease owner, SiteFile file, string fileRelative, string label, string note,
        Func<string, bool> hasDraft, Func<string, bool> hasLive,
        Func<string, UtamazeRelease, string> toLive, Func<string, UtamazeRelease, string> toDraft,
        Func<UtamazeRelease, string?> missing, bool changesStructure = false)
    {
        this.owner = owner;
        this.file = file;
        this.hasDraft = hasDraft;
        this.hasLive = hasLive;
        this.toLive = toLive;
        this.toDraft = toDraft;
        this.missing = missing;
        Label = label;
        Note = note;
        FileRelative = fileRelative;
        ChangesStructure = changesStructure;
        makeLive = IsLive;
    }

    public bool IsLive => hasLive(file.Text) && !hasDraft(file.Text);

    /// <summary>どちらの形とも読み取れない（手で書き換えた）状態。</summary>
    public bool IsUnknown => !hasLive(file.Text) && !hasDraft(file.Text);

    public string StateText => IsUnknown ? "不明" : IsLive ? "公開" : "準備中";

    public bool MakeLive
    {
        get => makeLive;
        set
        {
            if (makeLive == value) return;
            makeLive = value;
            Raise();
            Raise(nameof(Changed));
            Raise(nameof(Missing));
            Raise(nameof(HasError));
            owner.NotifyChanged();
        }
    }

    public bool Changed => !IsUnknown && MakeLive != IsLive;

    /// <summary>公開の形にするのに足りていない入力。</summary>
    public string? Missing
    {
        get
        {
            if (IsUnknown && MakeLive != IsLive) return "ページが手で書き換えられているため、ここからは切り替えられません。";
            return MakeLive && !IsLive ? missing(owner) : null;
        }
    }

    public bool HasError => Missing != null;

    internal void Apply()
    {
        if (!Changed || HasError) return;
        file.SetText(MakeLive ? toLive(file.Text, owner) : toDraft(file.Text, owner));
    }

    internal void Refresh()
    {
        makeLive = IsLive;
        Raise(nameof(IsLive));
        Raise(nameof(IsUnknown));
        Raise(nameof(StateText));
        Raise(nameof(MakeLive));
        Raise(nameof(Changed));
        Raise(nameof(Missing));
        Raise(nameof(HasError));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// うたまぜ！を「準備中」から「公開」へ切り替えるための、ひとまとまりの手順。
/// 片方だけ直して気づかない、を防ぐために、関わる場所をすべて一覧にして状態を見せます。
/// </summary>
public sealed class UtamazeRelease : INotifyPropertyChanged
{
    private static readonly Regex RobotsMeta =
        new(@"[ \t]*<meta name=""robots""[^>]*>\r?\n[ \t]*<meta name=""googlebot""[^>]*>\r?\n");
    private static readonly Regex ViewportMeta = new(@"([ \t]*<meta name=""viewport""[^>]*>\r?\n)");
    private static readonly Regex PreRelease = new(@"[ \t]*<p class=""pre-release"">[^<]*</p>\r?\n");
    private static readonly Regex HeroActions = new(@"([ \t]*<div class=""hero-actions"">[\s\S]*?\r?\n[ \t]*</div>\r?\n)");
    private static readonly Regex Markers =
        new(@"[ \t]*<!-- utamaze-link:start[^>]*-->\r?\n[\s\S]*?[ \t]*<!-- utamaze-link:end -->");

    private string downloadUrl;
    private string checkoutUrl = "";

    public IReadOnlyList<ReleaseSwitch> Switches { get; }

    private UtamazeRelease(List<ReleaseSwitch> switches, string downloadUrl)
    {
        Switches = switches;
        this.downloadUrl = downloadUrl;
    }

    /// <summary>ダウンロード先。version.json の download_url を初期値にします。</summary>
    public string DownloadUrl
    {
        get => downloadUrl;
        set { if (Set(ref downloadUrl, value)) NotifyChanged(); }
    }

    /// <summary>PROの購入先（Lemon Squeezy の共有用 Checkout URL）。</summary>
    public string CheckoutUrl
    {
        get => checkoutUrl;
        set { if (Set(ref checkoutUrl, value)) NotifyChanged(); }
    }

    public static UtamazeRelease? Load(string root, SiteFile? page, SiteFile home, SiteFile? version)
    {
        if (page == null) return null;

        var pageRelative = SitePaths.Relative(root, page.Path);
        var homeRelative = SitePaths.Relative(root, home.Path);
        var download = version == null
            ? ""
            : Regex.Match(version.Text, @"""download_url"":\s*""([^""]*)""").Groups[1].Value;

        var switches = new List<ReleaseSwitch>();
        var release = new UtamazeRelease(switches, download);

        static string? NeedsDownload(UtamazeRelease owner) =>
            string.IsNullOrWhiteSpace(owner.DownloadUrl) ? "ダウンロードのURLを入れてください。" : null;
        static string? NeedsCheckout(UtamazeRelease owner) =>
            string.IsNullOrWhiteSpace(owner.CheckoutUrl) ? "PROの購入URLを入れてください。" : null;
        static string? Nothing(UtamazeRelease owner) => null;

        // 1. 検索に載せる（noindex を外す / 戻す）
        switches.Add(new ReleaseSwitch(release, page, pageRelative,
            "検索に載せる", "noindex を外します。外すと検索結果に出るようになります。",
            text => RobotsMeta.IsMatch(text),
            text => !RobotsMeta.IsMatch(text),
            (text, _) => RobotsMeta.Replace(text, "", 1),
            (text, _) => ViewportMeta.Replace(text,
                m => m.Groups[1].Value +
                     "  <meta name=\"robots\" content=\"noindex,nofollow,noarchive\">\n" +
                     "  <meta name=\"googlebot\" content=\"noindex,nofollow,noarchive\">\n", 1),
            Nothing));

        // 2〜4. 準備中のボタンを、実際のリンクへ
        Button(release, switches, page, pageRelative,
            "上のダウンロードボタン", "「ダウンロード　準備中」を、実際のダウンロード先に変えます。",
            "button primary", "ダウンロード　準備中", "ダウンロード",
            owner => owner.DownloadUrl, NeedsDownload);

        Button(release, switches, page, pageRelative,
            "FREEのダウンロードボタン", "FREE欄の「公開準備中」を、実際のダウンロード先に変えます。",
            "button ghost full", "公開準備中", "ダウンロード",
            owner => owner.DownloadUrl, NeedsDownload);

        Button(release, switches, page, pageRelative,
            "PROの購入ボタン", "「PRO　準備中」を購入ページへのボタンに変えます。購入URLはここ1か所だけです。",
            "button primary full", "PRO　準備中", "PROを購入する",
            owner => owner.CheckoutUrl, NeedsCheckout);

        // 5. 「公開準備中です」の一文
        switches.Add(new ReleaseSwitch(release, page, pageRelative,
            "「公開準備中です」の一文", "上の方に出ている断り書きを消します。",
            text => PreRelease.IsMatch(text),
            text => !PreRelease.IsMatch(text),
            (text, _) => PreRelease.Replace(text, "", 1),
            (text, _) => HeroActions.Replace(text,
                m => m.Groups[1].Value + "\n        <p class=\"pre-release\">※ 現在は公開準備中です。</p>\n", 1),
            Nothing));

        // 6. トップページのリンク
        switches.Add(new ReleaseSwitch(release, home, homeRelative,
            "トップページにリンクを出す", "リンク欄の最後に、うたまぜ！のカードを足します。",
            text => Markers.IsMatch(text) && !text.Contains("href=\"/utamaze/\""),
            text => text.Contains("href=\"/utamaze/\""),
            (text, _) => Markers.Replace(text, _ => TopLinkBlock(), 1),
            (text, _) => Markers.Replace(text, _ => EmptyMarkers(), 1),
            Nothing, changesStructure: true));

        return release;
    }

    private static void Button(UtamazeRelease owner, List<ReleaseSwitch> switches, SiteFile file, string relative,
        string label, string note, string cssClass, string draftText, string liveText,
        Func<UtamazeRelease, string> url, Func<UtamazeRelease, string?> missing)
    {
        var draft = new Regex($@"<button class=""{Regex.Escape(cssClass)}"" disabled>{Regex.Escape(draftText)}</button>");
        var live = new Regex($@"<a class=""{Regex.Escape(cssClass)}"" href=""[^""]*"">{Regex.Escape(liveText)}</a>");

        switches.Add(new ReleaseSwitch(owner, file, relative, label, note,
            text => draft.IsMatch(text),
            text => live.IsMatch(text),
            (text, self) => draft.Replace(text, $@"<a class=""{cssClass}"" href=""{url(self)}"">{liveText}</a>", 1),
            (text, _) => live.Replace(text, $@"<button class=""{cssClass}"" disabled>{draftText}</button>", 1),
            missing));
    }

    private const string MarkerStart =
        "            <!-- utamaze-link:start うたまぜ！のリンクは HALKA SITE EDITOR の「うたまぜ！」で出し入れします。 -->";
    private const string MarkerEnd = "            <!-- utamaze-link:end -->";

    private static string EmptyMarkers() => MarkerStart + "\n" + MarkerEnd;

    private static string TopLinkBlock() =>
        MarkerStart + "\n" +
        "            <a class=\"link-card\" href=\"/utamaze/\" aria-label=\"うたまぜ！を開く\">\n" +
        "              <span class=\"link-mark\" aria-hidden=\"true\">♪</span>\n" +
        "              <span class=\"link-copy\">\n" +
        "                <strong>うたまぜ！</strong>\n" +
        "                <small>ボーカルMIXのプラグイン</small>\n" +
        "              </span>\n" +
        "              <span class=\"link-arrow\" aria-hidden=\"true\">↗</span>\n" +
        "            </a>\n" +
        MarkerEnd;

    public int LiveCount => Switches.Count(step => step.IsLive);

    public string Summary => Switches.Any(step => step.IsUnknown)
        ? $"{Switches.Count}項目のうち {LiveCount} が公開の形です（手で書き換えた所があるようです）。"
        : LiveCount == 0 ? $"{Switches.Count}項目すべて準備中です。"
        : LiveCount == Switches.Count ? $"{Switches.Count}項目すべて公開の形です。"
        : $"{Switches.Count}項目のうち {LiveCount} だけが公開の形です。揃っていません。";

    /// <summary>一部だけ公開になっている状態。公開する前の念押しに使います。</summary>
    public bool IsMixed => LiveCount != 0 && LiveCount != Switches.Count;

    public bool HasChanges => Switches.Any(step => step.Changed);
    public bool HasError => Switches.Any(step => step.HasError);
    public bool StructureChanged => Switches.Any(step => step.Changed && step.ChangesStructure);

    public IReadOnlyList<ChangeRow> Changes() => Switches
        .Where(step => step.Changed)
        .Select(step => new ChangeRow($"うたまぜ！：{step.Label}",
            step.IsLive ? "公開" : "準備中", step.MakeLive ? "公開" : "準備中", step.FileRelative))
        .ToArray();

    /// <summary>すべてを公開の形にそろえます。</summary>
    public void MakeAllLive()
    {
        foreach (var step in Switches) step.MakeLive = true;
    }

    internal void Apply()
    {
        foreach (var step in Switches) step.Apply();
    }

    internal void MarkSaved()
    {
        foreach (var step in Switches) step.Refresh();
        NotifyChanged();
    }

    public void Revert()
    {
        foreach (var step in Switches) step.Refresh();
        NotifyChanged();
    }

    internal void NotifyChanged()
    {
        Raise(nameof(Summary));
        Raise(nameof(LiveCount));
        Raise(nameof(IsMixed));
        Raise(nameof(HasChanges));
        Raise(nameof(HasError));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set(ref string storage, string? value, [CallerMemberName] string? name = null)
    {
        var next = value ?? "";
        if (string.Equals(storage, next, StringComparison.Ordinal)) return false;
        storage = next;
        Raise(name);
        return true;
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
