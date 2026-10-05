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
    private const string LatestVideo = @"(const latestVideoUrl = "")([^""]*)("";)";

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

    public string Root { get; }
    public IReadOnlyList<EditGroup> Groups { get; }

    public IEnumerable<EditField> Fields => Groups.SelectMany(group => group.Fields);
    public bool HasChanges => Fields.Any(field => field.Changed);
    public bool HasError => Fields.Any(field => field.HasError);

    private SiteSession(string root, List<SiteFile> files, IReadOnlyList<EditGroup> groups)
    {
        Root = root;
        this.files = files;
        Groups = groups;
    }

    public static SiteSession Load(string root)
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

        var groups = new List<EditGroup>
        {
            BuildCommission(root, ja, en),
            BuildTopPage(root, script),
            BuildColors(root, style),
        };

        return new SiteSession(root, files, groups);
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

        return new EditGroup(
            "依頼ページ",
            "受付状況と料金です。日本語版と英語版が並んでいるので、両方そろえて直してください。",
            fields,
            pairs);
    }

    private static EditGroup BuildTopPage(string root, SiteFile script)
    {
        var field = new EditField(
            script,
            SitePaths.Relative(root, script.Path),
            new ValueSlot(LatestVideo),
            "top.latestVideo",
            "最新動画のURL",
            FieldKind.YouTubeUrl);

        return new EditGroup(
            "トップページ",
            "サムネイル画像とリンク先は、このURLから自動で作られます。",
            new[] { field });
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
        .ToArray();

    /// <summary>入力内容をファイルへ書き込みます。</summary>
    public void Save()
    {
        if (HasError) throw new InvalidOperationException("入力に誤りがある項目があります。");

        foreach (var file in files)
        {
            if (file.ChangedOnDisk()) throw new SiteChangedOnDiskException(SitePaths.Relative(Root, file.Path));
        }

        foreach (var field in Fields) field.Apply();
        foreach (var file in files) file.Save();
        foreach (var field in Fields) field.MarkSaved();
    }

    public void Revert()
    {
        foreach (var field in Fields) field.Revert();
    }
}
