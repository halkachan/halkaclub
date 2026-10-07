namespace HalkaSiteEditor.Core;

/// <summary>別の場所でファイルが書き換わっていたときに投げます。</summary>
public sealed class SiteChangedOnDiskException : Exception
{
    public SiteChangedOnDiskException(string path)
        : base($"別の場所で {path} が変更されています。読み込み直してからやり直してください。") { }
}

/// <summary>
/// サイト1つぶんの編集。読み込み → 入力 → 保存 を受け持ちます。
/// フェーズ1で扱うのは、依頼ページ・最新動画・基本色の3つです。
/// </summary>
public sealed class SiteSession
{
    // HTML / CSS / JS の中から「値だけ」を取り出すための形。2番目のグループが値です。
    private const string StatusBadge = @"(<p class=""request-status-badge"">)([^<]*)(</p>)";
    private const string PlanAmount = @"(<span class=""plan-amount"">)([^<]*)(</span>)";
    private const string PlanName = @"(<span class=""plan-name"">)([^<]*)(</span>)";
    private const string OptionName = @"(<span class=""plan-option-name"">)([^<]*)(</span>)";
    private const string OptionPrice = @"(<span class=""plan-option-price"">)([^<]*)(</span>)";
    private const string LatestVideo = @"(const latestVideoUrl = "")([^""]*)("";)";
    private const string NewsYear = @"(<p>)([^<]*)(</p>\r?\n[ \t]*<h1 id=""news-title"">)";
    // 大きいリンク（作品一覧・インスト置き場・依頼）と、一番下のはるかくらぶ。
    // リンク集（link-grid）の中身は LinkGrid が受け持つので、ここでは拾いません。
    private const string CtaHref = @"(<div class=""works-cta"">[\s\S]*?<a class=""link-card"" href="")([^""]*)("")";
    private const string CtaName = @"(<div class=""works-cta"">[\s\S]*?<strong>)([^<]*)(</strong>)";
    private const string CtaNote = @"(<div class=""works-cta"">[\s\S]*?<small>)([^<]*)(</small>)";
    private const string ClubLinkHref = @"(<a class=""link-card club-link"" href="")([^""]*)("")";
    private const string ClubLinkName = @"(class=""link-card club-link""[\s\S]*?<strong>)([^<]*)(</strong>)";
    private const string ClubLinkNote = @"(class=""link-card club-link""[\s\S]*?<small>)([^<]*)(</small>)";
    private const string ClubDate = @"(<p class=""club-date"">)([^<]*)(</p>)";
    private const string KoofrUrl = @"(<a class=""entrance-button"" href="")([^""]*)("")";

    private static readonly (string Key, string Label, FieldKind Kind)[] VersionFields =
    {
        ("latest_version", "最新の版", FieldKind.HtmlText),
        ("page_url", "紹介ページのURL", FieldKind.Url),
        ("download_url", "ダウンロードのURL", FieldKind.Url),
    };

    private static readonly (string Variable, string Label)[] ColorTokens =
    {
        ("--paper", "紙の白"),
        ("--desk", "外側の灰色"),
        ("--ink", "文字の黒"),
        ("--yellow", "黄色（テープ・強調）"),
        ("--pencil", "うすい文字"),
        ("--red-line", "ノートの赤い縦線"),
    };

    private readonly List<SiteFile> files = new();
    private readonly SiteFile? worksPage;

    public string Root { get; }
    public IReadOnlyList<EditGroup> Groups { get; }
    public WorksDocument Works { get; }
    public PageTextDocument CommissionText { get; }
    /// <summary>うたまぜ！の紹介ページの文章。そのページが無ければ null。</summary>
    public PageTextDocument? UtamazeText { get; }

    /// <summary>文章を直せるページを、まとめて並べます。</summary>
    public IReadOnlyList<PageTextPage> TextPages =>
        CommissionText.Pages.Concat(UtamazeText?.Pages ?? Array.Empty<PageTextPage>()).ToArray();

    private IEnumerable<PageTextDocument> TextDocuments =>
        UtamazeText == null ? new[] { CommissionText } : new[] { CommissionText, UtamazeText };
    public UtamazeRelease? Utamaze { get; }
    public NewsDocument? News { get; }
    public ParagraphRun? ClubUpdate { get; }
    public GameDocument? Games { get; }
    public PageMetaDocument? Meta { get; }
    public LinkGrid? Links { get; }

    /// <summary>ボタンで起きたこと（追加・並べ替え・削除など）を、1手ずつ戻すための覚え書き。</summary>
    public UndoStack Undo { get; } = new();

    /// <summary>保存の直前に取る控え。「保存を1つ前に戻す」で使います。</summary>
    public SaveBackups Backups { get; private set; } = new("");
    /// <summary>トップページのﾊﾙｶﾁｬﾝ。</summary>
    public ImageSlot? Profile { get; }

    /// <summary>差し替えられる画像をまとめて。</summary>
    public IEnumerable<ImageSlot> Images =>
        (Meta?.Cards ?? Array.Empty<ImageSlot>()).Concat(
            Profile == null ? Array.Empty<ImageSlot>() : new[] { Profile });

    /// <summary>このツールが書き換えるファイル（サイトのフォルダーからの相対パス）。公開もこれだけを対象にします。</summary>
    public IReadOnlyList<string> ManagedFiles => files
        .Select(file => SitePaths.Relative(Root, file.Path))
        .Concat(Images.Select(image => image.Relative))
        .Concat(new[] { "sitemap.xml", "robots.txt" }
            .Where(name => File.Exists(Path.Combine(Root, name))))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(path => path, StringComparer.Ordinal).ToArray();

    public IEnumerable<EditField> Fields => Groups.SelectMany(group => group.Fields);
    public bool HasChanges => Fields.Any(field => field.Changed) || Works.HasChanges || TextDocuments.Any(document => document.HasChanges) || Utamaze?.HasChanges == true
        || News?.HasChanges == true || ClubUpdate?.Changed == true || Games?.HasChanges == true
        || Images.Any(image => image.Changed) || Links?.HasChanges == true;
    public bool HasError => Fields.Any(field => field.HasError) || Works.HasError || TextDocuments.Any(document => document.HasError) || Utamaze?.HasError == true
        || News?.HasError == true || ClubUpdate?.HasError == true || Games?.HasError == true
        || Images.Any(image => image.HasError) || Links?.HasError == true;

    private SiteSession(string root, List<SiteFile> files, IReadOnlyList<EditGroup> groups,
        WorksDocument works, PageTextDocument commissionText, PageTextDocument? utamazeText,
        UtamazeRelease? utamaze,
        SiteFile? worksPage, NewsDocument? news, ParagraphRun? clubUpdate, GameDocument? games,
        PageMetaDocument? meta, ImageSlot? profile, LinkGrid? links)
    {
        Root = root;
        this.files = files;
        Groups = groups;
        Works = works;
        CommissionText = commissionText;
        UtamazeText = utamazeText;
        Utamaze = utamaze;
        this.worksPage = worksPage;
        News = news;
        ClubUpdate = clubUpdate;
        Games = games;
        Meta = meta;
        Profile = profile;
        Links = links;
    }

    /// <param name="backups">控えの置き場所。ふだんは省きます（いつもの場所を使います）。</param>
    public static SiteSession Load(string root, SaveBackups? backups = null)
    {
        if (!SitePaths.IsSiteRoot(root))
            throw new InvalidDataException($"halkaclub のフォルダーではありません：{root}");

        var files = new List<SiteFile>();
        SiteFile Open(string path)
        {
            var file = SiteFile.Load(path);
            files.Add(file);
            return file;
        }

        var style = Open(SitePaths.StyleCss(root));
        var script = Open(SitePaths.ScriptJs(root));
        var ja = Open(SitePaths.CommissionJa(root));
        var en = Open(SitePaths.CommissionEn(root));
        var home = Open(SitePaths.IndexHtml(root));
        var worksFile = Open(SitePaths.WorksData(root));
        // 作品を変えたとき、読み込み側の ?v= を上げるために開いておきます。
        var worksPage = File.Exists(SitePaths.WorksHtml(root)) ? Open(SitePaths.WorksHtml(root)) : null;

        var groups = new List<EditGroup>
        {
            BuildCommission(root, ja, en),
            BuildTopPage(root, script, home),
            BuildLinks(root, home),
            BuildColors(root, style),
        };

        // あるときだけ出すページ。
        var clubPath = SitePaths.ClubHtml(root);
        SiteFile? clubFile = null;
        if (File.Exists(clubPath))
        {
            clubFile = Open(clubPath);
            groups.Add(BuildClub(root, clubFile));
        }

        var versionPath = SitePaths.UtamazeVersion(root);
        SiteFile? versionFile = null;
        if (File.Exists(versionPath))
        {
            versionFile = Open(versionPath);
            groups.Add(BuildUtamaze(root, versionFile));
        }

        // うたまぜ！のリリース手順（準備中 ⇄ 公開の切り替え）。
        var utamazePath = SitePaths.UtamazePage(root);
        var utamazePage = File.Exists(utamazePath) ? Open(utamazePath) : null;
        var utamaze = UtamazeRelease.Load(root, utamazePage, home, versionFile);

        // ゲーム集と、各ゲームのページ。
        var games = GameDocument.Load(root, Open);
        if (games != null)
        {
            groups.Add(new EditGroup(
                "ゲーム",
                "ゲーム集の一覧と、各ゲームのページです。更新履歴は新しいものが上から並びます。",
                games.Fields.ToArray()));
        }

        // トップページのリンク集（増やす・並べ替える・減らす）。
        var links = LinkGrid.Load(home, SitePaths.Relative(root, home.Path));

        // ページの顔（タイトルとOGP）と、差し替えられる画像。
        var meta = PageMetaDocument.Load(root, Open);
        if (meta != null)
        {
            groups.Add(new EditGroup(
                "ページの顔",
                "検索結果とSNSに出る文章です。カード画像も、ここで差し替えられます。",
                meta.AllFields.ToArray()));
        }

        var profilePath = Path.Combine(root, "assets", "profile", "halgif1.gif");
        var profile = File.Exists(profilePath)
            ? new ImageSlot(root, "assets/profile/halgif1.gif", "ﾊﾙｶﾁｬﾝ", cardSize: false)
            : null;

        // トップページの「うれしいこと」と、くらぶの更新内容。
        var news = NewsDocument.Load(home, SitePaths.Relative(root, home.Path));
        var clubUpdate = clubFile == null ? null : ParagraphRun.Create(
            clubFile, SitePaths.Relative(root, clubFile.Path),
            "はるかくらぶ：更新内容", "1行＝1項目です。「・」も含めてそのまま書けます。",
            new System.Text.RegularExpressions.Regex(
                @"(<p class=""club-date"">[^<]*</p>\r?\n)((?:[ \t]*<p>[\s\S]*?</p>\r?\n)+)([ \t]*</section>)"));

        var works = WorksDocument.Load(worksFile, SitePaths.Relative(root, worksFile.Path));
        var commissionText = PageTextDocument.Load(PageTextDocument.Commission,
            ("依頼ページ（日本語）", ja, SitePaths.Relative(root, ja.Path), "/commission/"),
            ("依頼ページ（英語）", en, SitePaths.Relative(root, en.Path), "/commission/en/"));

        var utamazeText = utamazePage == null ? null : PageTextDocument.Load(PageTextDocument.Utamaze,
            ("うたまぜ！", utamazePage, SitePaths.Relative(root, utamazePage.Path), "/utamaze/"));

        var session = new SiteSession(root, files, groups, works, commissionText, utamazeText, utamaze,
            worksPage, news, clubUpdate, games, meta, profile, links);
        session.Backups = backups ?? SaveBackups.For(root);
        return session;
    }

    private static EditGroup BuildLinks(string root, SiteFile home)
    {
        var relative = SitePaths.Relative(root, home.Path);
        var names = ValueSlot.ReadAll(home.Text, CtaName);
        var count = new ValueSlot(CtaHref).Count(home.Text);
        if (names.Count != count)
        {
            throw new InvalidDataException(
                $"トップページの大きいリンクの形が想定と違います（リンク {count} 個 / 名前 {names.Count} 個）。");
        }

        var fields = new List<EditField>();
        var rows = new List<FieldRow>();

        EditField Field(string pattern, int index, string id, string label, FieldKind kind)
        {
            var field = new EditField(home, relative, new ValueSlot(pattern, index), id, label, kind);
            fields.Add(field);
            return field;
        }

        void Row(string label, string namePattern, string notePattern, string hrefPattern, int index, string id)
        {
            rows.Add(new FieldRow(label,
                new FieldCell("名前", Field(namePattern, index, $"{id}.name", $"{label}：名前", FieldKind.HtmlText), 180),
                new FieldCell("説明", Field(notePattern, index, $"{id}.note", $"{label}：説明", FieldKind.HtmlText), 220),
                new FieldCell("リンク先", Field(hrefPattern, index, $"{id}.url", $"{label}：リンク先", FieldKind.Url))));
        }

        for (var i = 0; i < count; i++) Row(names[i], CtaName, CtaNote, CtaHref, i, $"link.{i}");

        // はるかくらぶの入口（一番下）。
        if (new ValueSlot(ClubLinkHref).Count(home.Text) > 0)
            Row(ValueSlot.ReadAll(home.Text, ClubLinkName).FirstOrDefault() ?? "はるかくらぶ",
                ClubLinkName, ClubLinkNote, ClubLinkHref, 0, "link.club");

        return new EditGroup(
            "リンク",
            "トップページのリンクです。上の大きいリンクは置き場所が決まっています。下のリンク集は、追加・並べ替え・削除ができます。",
            fields, null, rows);
    }

    private static EditGroup BuildClub(string root, SiteFile club)
    {
        var relative = SitePaths.Relative(root, club.Path);
        var date = new EditField(club, relative, new ValueSlot(ClubDate),
            "club.date", "最新更新の日付", FieldKind.HtmlText);
        var koofr = new EditField(club, relative, new ValueSlot(KoofrUrl),
            "club.koofr", "Koofr の入口URL", FieldKind.Url);

        return new EditGroup(
            "はるかくらぶ",
            "くらぶページの更新日と、Koofr への入口です。パスワードはここには書かないでください。",
            new[] { date, koofr }, null,
            new[]
            {
                new FieldRow("最新更新", new FieldCell("日付（2026/07/15 の形）", date, 220)),
                new FieldRow("Koofr", new FieldCell("入口URL", koofr)),
            });
    }

    private static EditGroup BuildUtamaze(string root, SiteFile version)
    {
        var relative = SitePaths.Relative(root, version.Path);
        var fields = new List<EditField>();
        var rows = new List<FieldRow>();

        var released = new EditField(version, relative,
            new ValueSlot(@"(""released"":\s*)(true|false)()"),
            "utamaze.released", "公開済みにする", FieldKind.Boolean);
        fields.Add(released);
        rows.Add(new FieldRow("公開", new FieldCell("公開済みにする", released, 220)));

        foreach (var (key, label, kind) in VersionFields)
        {
            var field = new EditField(version, relative,
                new ValueSlot($@"(""{key}"":\s*"")([^""]*)("")"),
                $"utamaze.{key}", label, kind);
            fields.Add(field);
            rows.Add(new FieldRow(label, new FieldCell(label, field)));
        }

        return new EditGroup(
            "うたまぜ！",
            "プラグインのアップデート確認が見ているファイルです。公開済みにすると、利用者に更新が知らされます。",
            fields, null, rows);
    }

    private static EditGroup BuildCommission(string root, SiteFile ja, SiteFile en)
    {
        var jaRelative = SitePaths.Relative(root, ja.Path);
        var enRelative = SitePaths.Relative(root, en.Path);

        var fields = new List<EditField>();
        var pairs = new List<FieldPair>();

        EditField Field(SiteFile file, string relative, string pattern, int index, string id, string label)
        {
            var field = new EditField(file, relative, new ValueSlot(pattern, index), id, label, FieldKind.HtmlText);
            fields.Add(field);
            return field;
        }

        var statusJa = Field(ja, jaRelative, StatusBadge, 0, "commission.status.ja", "受付状況（日本語）");
        var statusEn = Field(en, enRelative, StatusBadge, 0, "commission.status.en", "受付状況（英語）");
        pairs.Add(new FieldPair("受付状況", statusJa, statusEn));

        var names = ValueSlot.ReadAll(ja.Text, PlanName);
        var jaCount = new ValueSlot(PlanAmount).Count(ja.Text);
        var enCount = new ValueSlot(PlanAmount).Count(en.Text);
        if (jaCount != enCount)
        {
            throw new InvalidDataException(
                $"依頼ページの項目数が日英で違います（日本語 {jaCount} / 英語 {enCount}）。" +
                "先にHTMLを揃えてください。");
        }

        for (var i = 0; i < jaCount; i++)
        {
            var label = i < names.Count ? names[i] : $"{i + 1}番目の項目";
            var amountJa = Field(ja, jaRelative, PlanAmount, i, $"commission.price.{i}.ja", $"{label}（日本語）");
            var amountEn = Field(en, enRelative, PlanAmount, i, $"commission.price.{i}.en", $"{label}（英語）");
            pairs.Add(new FieldPair(label, amountJa, amountEn));
        }

        // 追加プラン（「＋4,000円」など）。日英で数が同じことを確かめてから組にします。
        var optionNames = ValueSlot.ReadAll(ja.Text, OptionName);
        var optionCountJa = new ValueSlot(OptionPrice).Count(ja.Text);
        var optionCountEn = new ValueSlot(OptionPrice).Count(en.Text);
        if (optionCountJa != optionCountEn || optionNames.Count != optionCountJa)
        {
            throw new InvalidDataException(
                $"追加プランの数が合いません（日本語 {optionCountJa} / 英語 {optionCountEn}）。先にHTMLを揃えてください。");
        }

        var options = new List<OptionRow>();
        for (var i = 0; i < optionCountJa; i++)
        {
            var label = optionNames[i];
            var namePair = new FieldPair(label,
                Field(ja, jaRelative, OptionName, i, $"option.{i}.name.ja", $"{label}：名前（日本語）"),
                Field(en, enRelative, OptionName, i, $"option.{i}.name.en", $"{label}：名前（英語）"));
            var pricePair = new FieldPair(label,
                Field(ja, jaRelative, OptionPrice, i, $"option.{i}.price.ja", $"{label}：金額（日本語）"),
                Field(en, enRelative, OptionPrice, i, $"option.{i}.price.en", $"{label}：金額（英語）"));
            options.Add(new OptionRow(label, namePair, pricePair));
        }

        return new EditGroup(
            "依頼ページ",
            "受付状況と料金です。日本語版と英語版が並んでいるので、両方そろえて直してください。",
            fields,
            pairs, null, options);
    }

    private static EditGroup BuildTopPage(string root, SiteFile script, SiteFile home)
    {
        var video = new EditField(
            script,
            SitePaths.Relative(root, script.Path),
            new ValueSlot(LatestVideo),
            "top.latestVideo",
            "最新動画のURL",
            FieldKind.YouTubeUrl);

        var year = new EditField(
            home,
            SitePaths.Relative(root, home.Path),
            new ValueSlot(NewsYear),
            "top.newsYear",
            "「うれしいこと」の年",
            FieldKind.HtmlText);

        return new EditGroup(
            "トップページ",
            "サムネイル画像とリンク先は、最新動画のURLから自動で作られます。",
            new[] { video, year }, null,
            new[] { new FieldRow("うれしいこと", new FieldCell("見出しに出す年", year, 160)) });
    }

    private static EditGroup BuildColors(string root, SiteFile style)
    {
        var relative = SitePaths.Relative(root, style.Path);
        var fields = ColorTokens.Select(token => new EditField(
            style,
            relative,
            new ValueSlot($@"({token.Variable}:\s*)([^;\r\n]+)(;)"),
            $"color{token.Variable}",
            token.Label,
            FieldKind.CssColor)).ToArray();

        return new EditGroup(
            "サイトの基本色",
            "サイト全体の色です。ここを変えると、すべてのページの見た目が変わります。",
            fields);
    }

    public IReadOnlyList<ChangeRow> Changes() => Fields
        .Where(field => field.Changed)
        .Select(field => new ChangeRow(field.Label, field.Original, field.Value, field.FileRelative))
        .Concat(Works.Changes())
        .Concat(TextDocuments.SelectMany(document => document.Changes()))
        .Concat(Utamaze?.Changes() ?? Array.Empty<ChangeRow>())
        .Concat(News?.Changes() ?? Array.Empty<ChangeRow>())
        .Concat(ClubUpdate?.Change() is { } clubRow ? new[] { clubRow } : Array.Empty<ChangeRow>())
        .Concat(Games?.Changes() ?? Array.Empty<ChangeRow>())
        .Concat(Images.Select(image => image.Change()).OfType<ChangeRow>())
        .Concat(Links?.Changes() ?? Array.Empty<ChangeRow>())
        .ToArray();

    /// <summary>入力内容をファイルへ書き込みます。</summary>
    public void Save()
    {
        if (HasError) throw new InvalidOperationException("入力に誤りがある項目があります。");

        foreach (var file in files)
        {
            if (file.ChangedOnDisk()) throw new SiteChangedOnDiskException(SitePaths.Relative(Root, file.Path));
        }

        // 料金などの単発の値を先に入れ、そのあとで文章のかたまりを組み立て直します
        // （文章側は書き換え後のファイルを見て作るので、どちらの変更も残ります）。
        foreach (var field in Fields) field.Apply();

        // 作品一覧を変えたら、読み込み側の ?v= を上げます。
        // 上げないと、見る人のブラウザが古い works-data.js を掴んだままになります。
        if (Works.HasChanges && worksPage != null)
            CacheBuster.Bump(worksPage, "works-data.js");

        Works.Apply();
        foreach (var document in TextDocuments) document.Apply();
        Utamaze?.Apply();
        News?.Apply();
        ClubUpdate?.Apply();
        Games?.Apply();
        Links?.Apply();
        // 画像を差し替えるときは、読み込み側の `?v=` も上げます。
        // 上げないと、見る人のブラウザが古い画像をしばらく掴んだままになります。
        foreach (var image in Images) image.Bump(files);

        // 書き換わるファイルの控えを取ってから書き込みます。
        Backups.Take(Root, files
            .Where(file => file.Dirty)
            .Select(file => SitePaths.Relative(Root, file.Path))
            .Concat(Images.Where(image => image.SourcePath != null).Select(image => image.Relative)));

        foreach (var file in files) file.Save();
        // 画像を写すのは、文章の書き込みが全部すんでからにします。
        foreach (var image in Images) image.Copy();
        foreach (var field in Fields) field.MarkSaved();
        Works.MarkSaved();
        foreach (var document in TextDocuments) document.MarkSaved();
        Utamaze?.MarkSaved();
        News?.MarkSaved();
        ClubUpdate?.MarkSaved();
        Games?.MarkSaved();
        Links?.MarkSaved();
        // 保存したあとは、戻せる手を数え直します（戻しても保存前には戻らないため）。
        Undo.Clear();
    }

    public void Revert()
    {
        foreach (var field in Fields) field.Revert();
        Works.Revert();
        foreach (var document in TextDocuments) document.Revert();
        Utamaze?.Revert();
        News?.Revert();
        ClubUpdate?.Revert();
        Games?.Revert();
        Links?.Revert();
        foreach (var image in Images) image.Clear();
        Undo.Clear();
    }
}
