using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalkaSiteEditor.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace HalkaSiteEditor;

public partial class MainWindow : Window
{
    private sealed class LocalSettings
    {
        public string SiteRoot { get; set; } = "";
        public string YouTubeChannelId { get; set; } = "";
    }

    /// <summary>PC側プレビューで再現する画面の横幅（CSSピクセル）。</summary>
    private const double DesktopPreviewWidth = 1280;

    private static readonly Brush ChangedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCB, 0x75));
    private static readonly Brush QuietBrush = new SolidColorBrush(Color.FromRgb(0x58, 0x61, 0x70));

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HalkaSiteEditor", "settings.json");

    private SiteSession? session;
    private EditField? videoField;
    private PreviewServer? previewServer;
    private bool previewReady;
    private GitPublisher? publisher;
    private bool hadChanges;

    public MainWindow() => InitializeComponent();

    // --- 起動と読み込み -----------------------------------------------------

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        // XAMLに書いた初期状態は、読み込み中のイベントでは反映できないのでここで1回入れ直します。
        ApplyPreviewMode();
        PreviewToggled(this, new RoutedEventArgs());

        var saved = LoadSettings().SiteRoot;
        var root = !string.IsNullOrEmpty(saved) && SitePaths.IsSiteRoot(saved)
            ? saved
            : SitePaths.FindNear(AppContext.BaseDirectory);

        if (root == null)
        {
            StatusText.Text = "halkaclub のフォルダーが見つかりません。「フォルダーを選ぶ...」から指定してください。";
            return;
        }

        OpenSite(root);
    }

    private void OpenSite(string root)
    {
        try
        {
            session = SiteSession.Load(root);
        }
        catch (Exception error)
        {
            session = null;
            MessageBox.Show(this, error.Message, "読み込めませんでした", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = "読み込めませんでした。";
            return;
        }

        RootText.Text = root;
        SaveSettings(new LocalSettings
        {
            SiteRoot = root,
            YouTubeChannelId = LoadSettings().YouTubeChannelId,
        });
        publisher = new GitPublisher(root);

        var commission = session.Groups.Single(group => group.Title == "依頼ページ");
        var top = session.Groups.Single(group => group.Title == "トップページ");
        var colors = session.Groups.Single(group => group.Title == "サイトの基本色");
        var links = session.Groups.Single(group => group.Title == "リンク");

        CommissionNote.Text = commission.Note;
        TopNote.Text = top.Note;
        ColorNote.Text = colors.Note;
        LinkNote.Text = links.Note;

        CommissionItems.ItemsSource = commission.Pairs;
        OptionItems.ItemsSource = commission.Options;
        ColorItems.ItemsSource = colors.Fields;
        LinkItems.ItemsSource = links.Rows;

        TextPagePicker.ItemsSource = session.CommissionText.Pages;
        TextPagePicker.SelectedIndex = 0;
        foreach (var page in session.CommissionText.Pages)
        foreach (var block in page.Blocks)
            block.PropertyChanged += TextBlockChanged;

        BindOptionalGroup("はるかくらぶ", ClubTab, ClubNote, ClubItems);
        BindOptionalGroup("うたまぜ！", UtamazeTab, UtamazeNote, UtamazeItems);
        BindWorks();
        BindRelease();

        videoField = top.Fields.Single();
        VideoLabel.Text = videoField.Label;
        VideoBox.SetBinding(TextBox.TextProperty, new Binding(nameof(EditField.Value))
        {
            Source = videoField,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        foreach (var field in session.Fields) field.PropertyChanged += FieldChanged;

        UpdateVideoPreview();
        RefreshChanges();
        RefreshPublishState();
        // ここから下は読み込み後の表示だけなので、順番に意味はありません。
        StatusText.Text = $"読み込みました。{session.Fields.Count()} 項目を編集できます。";

        StartPreview(root);
    }

    // --- プレビュー ---------------------------------------------------------

    private void StartPreview(string root)
    {
        previewServer?.Dispose();
        previewServer = null;
        previewReady = false;

        try
        {
            previewServer = new PreviewServer(root);
            previewServer.Start();
        }
        catch (Exception error)
        {
            PreviewNote.Text = "プレビュー用のサーバーを開始できませんでした：" + error.Message;
            return;
        }

        var pages = SitePages.ForSite(root);
        PagePicker.ItemsSource = pages;
        PagePicker.SelectedItem = SitePages.ForGroup(pages, CurrentGroupTitle());

        _ = InitializePreviewAsync();
    }

    private async Task InitializePreviewAsync()
    {
        if (previewReady) { NavigatePreview(); return; }

        try
        {
            // ユーザーデータはEXEの隣ではなく、いつもの場所へ置きます。
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HalkaSiteEditor", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: folder);
            await DesktopView.EnsureCoreWebView2Async(environment);
            await MobileView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception error)
        {
            PreviewNote.Text =
                "プレビューを表示できません。WebView2 ランタイムが必要です（Windows 11 なら通常は入っています）。\n" +
                error.Message;
            return;
        }

        previewReady = true;
        MobileView.ZoomFactor = 1;
        ApplyDesktopZoom();
        NavigatePreview();
    }

    private string CurrentGroupTitle() => (Tabs.SelectedItem as TabItem)?.Header as string ?? "";

    private void NavigatePreview()
    {
        if (!previewReady || previewServer == null || !previewServer.IsRunning) return;
        if (PagePicker.SelectedItem is not SitePage page) return;

        var target = new Uri(previewServer.BaseUrl.TrimEnd('/') + page.Url);
        foreach (var view in new[] { DesktopView, MobileView })
        {
            if (view.Source == target) view.CoreWebView2?.Reload();
            else view.Source = target;
        }
    }

    private void RefreshPreview()
    {
        if (!previewReady) return;
        DesktopView.CoreWebView2?.Reload();
        MobileView.CoreWebView2?.Reload();
    }

    private void ApplyDesktopZoom()
    {
        if (!previewReady || DesktopView.ActualWidth < 1) return;
        var zoom = Math.Clamp(DesktopView.ActualWidth / DesktopPreviewWidth, 0.25, 1.0);
        DesktopView.ZoomFactor = zoom;
        DesktopCaption.Text = $"PC（{DesktopPreviewWidth:0}px 相当・{zoom * 100:0}% 表示）";
    }

    private void DesktopViewSizeChanged(object sender, SizeChangedEventArgs e) => ApplyDesktopZoom();

    private void PreviewModeChanged(object sender, RoutedEventArgs e) => ApplyPreviewMode();

    /// <summary>
    /// 表示（両方／PCのみ／スマホのみ）を画面へ反映します。
    /// XAMLを読んでいる途中にも Checked が飛んでくるため、そのときは何もせず、
    /// 画面ができあがってから WindowLoaded で1回呼び直します。
    /// </summary>
    private void ApplyPreviewMode()
    {
        if (DesktopBox == null || MobileBox == null) return;

        DesktopBox.Visibility = ShowMobile.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        MobileBox.Visibility = ShowDesktop.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        ApplyDesktopZoom();
    }

    private void PagePickerChanged(object sender, SelectionChangedEventArgs e) => NavigatePreview();

    private void RefreshPreviewClick(object sender, RoutedEventArgs e) => RefreshPreview();

    private void TabChanged(object sender, SelectionChangedEventArgs e)
    {
        // XAMLを読んでいる途中にも飛んでくるので、部品が揃うまでは何もしません。
        if (PagePicker == null || !ReferenceEquals(e.OriginalSource, Tabs)) return;
        if (PagePicker.ItemsSource is not IReadOnlyList<SitePage> pages) return;
        var wanted = SitePages.ForGroup(pages, CurrentGroupTitle());
        if (wanted != null && !ReferenceEquals(PagePicker.SelectedItem, wanted)) PagePicker.SelectedItem = wanted;
    }

    private void PreviewToggled(object sender, RoutedEventArgs e)
    {
        // IsChecked="True" の指定で、XAMLを読んでいる途中にも飛んできます。
        if (PreviewPanel == null || PreviewSplitter == null || PreviewColumn == null) return;

        var show = PreviewToggle.IsChecked == true;
        PreviewPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PreviewSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PreviewColumn.Width = show ? new GridLength(860) : new GridLength(0);
    }

    private void FieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditField.Value) or nameof(EditField.Changed) or nameof(EditField.Error))
            RefreshChanges();
    }

    /// <summary>サイトに無ければタブごと隠します（くらぶ・うたまぜ）。</summary>
    private void BindOptionalGroup(string title, TabItem tab, TextBlock note, ItemsControl items)
    {
        var group = session?.Groups.FirstOrDefault(candidate => candidate.Title == title);
        if (group == null)
        {
            tab.Visibility = Visibility.Collapsed;
            return;
        }
        tab.Visibility = Visibility.Visible;
        note.Text = group.Note;
        items.ItemsSource = group.Rows;
    }

    // --- 作品一覧 -----------------------------------------------------------

    private void BindWorks()
    {
        if (session == null) return;

        CategoryPicker.ItemsSource = session.Works.Categories;
        CategoryPicker.SelectedIndex = 0;

        foreach (var category in session.Works.Categories)
        {
            category.Works.CollectionChanged += WorksCollectionChanged;
            foreach (var work in category.Works) work.PropertyChanged += WorkChanged;
        }
    }

    private void WorksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (WorkItem work in e.NewItems) work.PropertyChanged += WorkChanged;
        if (e.OldItems != null)
            foreach (WorkItem work in e.OldItems) work.PropertyChanged -= WorkChanged;
        RefreshChanges();
    }

    private void WorkChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    // --- うたまぜ！のリリース -----------------------------------------------

    private void BindRelease()
    {
        var release = session?.Utamaze;
        if (release == null)
        {
            ReleaseSection.Visibility = Visibility.Collapsed;
            return;
        }

        ReleaseSection.Visibility = Visibility.Visible;
        ReleaseSection.DataContext = release;
        ReleaseItems.ItemsSource = release.Switches;
        release.PropertyChanged += ReleaseChanged;
        foreach (var step in release.Switches) step.PropertyChanged += ReleaseChanged;
        ReleaseSummary.Text = release.Summary;
    }

    private void ReleaseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (session?.Utamaze != null) ReleaseSummary.Text = session.Utamaze.Summary;
        RefreshChanges();
    }

    private void ReleaseAllClick(object sender, RoutedEventArgs e)
    {
        session?.Utamaze?.MakeAllLive();
        RefreshChanges();
    }

    private WorkCategory? SelectedCategory => CategoryPicker?.SelectedItem as WorkCategory;

    private void CategoryPickerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkItems == null) return;
        WorkItems.ItemsSource = SelectedCategory?.Works;
    }

    private void PickVideosClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;

        var saved = LoadSettings();
        var channel = string.IsNullOrWhiteSpace(saved.YouTubeChannelId)
            ? YouTubeFeed.DefaultChannelId
            : saved.YouTubeChannelId;

        var picker = new VideoPickerWindow(session, channel, SelectedCategory) { Owner = this };
        picker.ShowDialog();

        if (picker.ChannelId != channel)
            SaveSettings(new LocalSettings { SiteRoot = session.Root, YouTubeChannelId = picker.ChannelId });

        if (picker.AddedCount > 0)
        {
            StatusText.Text = $"{picker.AddedCount} 本を足しました。タイトルは直せます。保存を忘れずに。";
            RefreshChanges();
        }
    }

    private void WorkAddClick(object sender, RoutedEventArgs e)
    {
        var category = SelectedCategory;
        if (category == null) return;
        category.AddNew();
        StatusText.Text = "一番下に作品を足しました。タイトルと投稿日とURLを入れてください。";
    }

    private void WorkUpClick(object sender, RoutedEventArgs e) => MoveWork(sender, -1);

    private void WorkDownClick(object sender, RoutedEventArgs e) => MoveWork(sender, 1);

    private void MoveWork(object sender, int offset)
    {
        if (((FrameworkElement)sender).Tag is WorkItem work) SelectedCategory?.Move(work, offset);
    }

    private void WorkDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not WorkItem work) return;
        var answer = MessageBox.Show(this, $"「{work.Title}」を一覧から外します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;
        SelectedCategory?.Works.Remove(work);
    }

    private void RefreshChanges()
    {
        if (session == null) return;

        var changes = session.Changes();
        ChangeList.ItemsSource = changes;
        ChangeCountText.Text = changes.Count == 0 ? "" : $"{changes.Count} 件";

        var errors = (session.Utamaze?.Switches.Count(step => step.HasError) ?? 0)
            + session.Fields.Count(field => field.HasError)
            + session.Works.Categories.Sum(category => category.Works.Count(work => work.HasError))
            + session.CommissionText.Pages.Sum(page => page.Blocks.Count(block => block.HasError));
        SaveButton.IsEnabled = session.HasChanges && !session.HasError;
        RevertButton.IsEnabled = session.HasChanges;

        StatusText.Text = errors > 0
            ? $"直すところが {errors} 件あります。赤い文字の欄を確認してください。"
            : changes.Count == 0
                ? "変更はありません。"
                : $"{changes.Count} 件の変更があります。「保存する」でファイルに書き込みます。";

        // 保存していない変更があるうちは公開させません。
        if (session.HasChanges)
        {
            PublishButton.IsEnabled = false;
            PublishButton.ToolTip = "先に「保存する」を押してください。";
        }
        else if (hadChanges)
        {
            RefreshPublishState();   // 変更が無くなった瞬間だけ、git に聞き直します。
        }
        hadChanges = session.HasChanges;
    }

    // --- 公開 ---------------------------------------------------------------

    /// <summary>git に聞いて、公開待ちの件数をボタンに出します。</summary>
    private void RefreshPublishState()
    {
        if (session == null || publisher == null || !publisher.IsAvailable)
        {
            PublishButton.IsEnabled = false;
            PublishButton.Content = "公開する";
            PublishButton.ToolTip = "git のリポジトリではないので、ここからは公開できません。";
            return;
        }

        var pending = publisher.Pending(session.ManagedFiles);
        PublishButton.Content = pending.Count > 0 ? $"公開する（{pending.Count}）" : "公開する";
        PublishButton.IsEnabled = pending.Count > 0 && !session.HasChanges;
        PublishButton.ToolTip = pending.Count == 0
            ? "公開を待っている変更はありません。"
            : "保存した内容を halkaclub.com へ出します。";
    }

    private void PublishClick(object sender, RoutedEventArgs e)
    {
        if (session == null || publisher == null) return;

        if (session.HasChanges)
        {
            MessageBox.Show(this, "保存していない変更があります。先に「保存する」を押してください。",
                "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pending = publisher.Pending(session.ManagedFiles);
        if (pending.Count == 0)
        {
            MessageBox.Show(this, "公開を待っている変更はありません。", "確認",
                MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshPublishState();
            return;
        }

        if (session.Utamaze?.IsMixed == true)
        {
            var proceed = MessageBox.Show(this,
                "うたまぜ！のリリース設定が揃っていません。" + Environment.NewLine + Environment.NewLine +
                session.Utamaze.Summary + Environment.NewLine + Environment.NewLine +
                "一部だけ公開された状態になります。このまま進みますか？",
                "確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.OK) return;
        }

        var dialog = new PublishWindow(publisher, session.ManagedFiles, pending) { Owner = this };
        dialog.ShowDialog();
        RefreshPublishState();

        if (dialog.Published)
        {
            StatusText.Text = "公開しました。halkaclub.com に出るまで30秒ほどかかります。";
            MessageBox.Show(this,
                "公開しました。\n\nhalkaclub.com に出るまで30秒ほどかかります。\n" +
                "しばらくしてからブラウザで確かめてください。",
                "公開しました", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // --- トップページ（最新動画） -------------------------------------------

    private void VideoBoxChanged(object sender, TextChangedEventArgs e) => UpdateVideoPreview();

    private void UpdateVideoPreview()
    {
        if (videoField == null) return;

        VideoBox.BorderBrush = videoField.Changed ? ChangedBrush : QuietBrush;
        VideoError.Text = videoField.Error ?? "";

        var id = YouTubeUrl.ExtractId(videoField.Value);
        VideoIdText.Text = id == null ? "" : $"動画ID：{id}";
        VideoThumb.Source = null;
        if (id == null) return;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"https://i.ytimg.com/vi/{id}/mqdefault.jpg");
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            VideoThumb.Source = image;
        }
        catch (Exception)
        {
            // サムネイルは確認用なので、取得できなくても編集は続けられます。
        }
    }

    // --- ボタン -------------------------------------------------------------

    private void SyncEnglishClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is FieldPair pair) pair.SyncEnglishFromJapanese();
    }

    private void SyncOptionEnglishClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is OptionRow row) row.SyncEnglishFromJapanese();
    }

    // --- 依頼ページの文章 ---------------------------------------------------

    private void TextBlockChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CommissionTextBlock.Text) or nameof(CommissionTextBlock.Changed)
            or nameof(CommissionTextBlock.Error))
            RefreshChanges();
    }

    private void TextPageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextBlockItems == null) return;
        TextBlockItems.ItemsSource = (TextPagePicker.SelectedItem as CommissionTextPage)?.Blocks;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;

        var changes = session.Changes();
        var detail = string.Join("\n", changes.Take(10).Select(row => $"・{row.Label}：{row.Before} → {row.After}"));
        if (changes.Count > 10) detail += $"\n…ほか {changes.Count - 10} 件";

        var answer = MessageBox.Show(this,
            $"{changes.Count} 件の変更をファイルに書き込みます。\n\n{detail}\n\n" +
            "まだ公開はされません。GitHub Desktop でコミットすると公開されます。",
            "保存しますか？", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        // トップページのリンクを出し入れすると、リンクの数が変わります。
        // 位置がずれるので、保存のあとに読み込み直します。
        var needsReload = session.Utamaze?.StructureChanged == true;

        try
        {
            session.Save();
        }
        catch (SiteChangedOnDiskException error)
        {
            var reload = MessageBox.Show(this,
                error.Message + "\n\n読み込み直しますか？（いまの入力は失われます）",
                "保存できません", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (reload == MessageBoxResult.OK) OpenSite(session.Root);
            return;
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "保存できません", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (needsReload)
        {
            OpenSite(session.Root);
            RefreshPreview();
            StatusText.Text = $"{changes.Count} 件を保存して、読み込み直しました。右のプレビューで確かめてください。";
            return;
        }

        RefreshChanges();
        UpdateVideoPreview();
        RefreshPreview();
        RefreshPublishState();
        StatusText.Text = $"{changes.Count} 件を保存しました。右のプレビューで確かめてから「公開する」を押してください。";
    }

    private void RevertClick(object sender, RoutedEventArgs e)
    {
        if (session == null || !session.HasChanges) return;
        var answer = MessageBox.Show(this, "入力した内容をすべて元に戻します。よろしいですか？",
            "元に戻す", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        session.Revert();
        BindWorks();   // 元に戻すと作品の一覧は作り直されるので、つなぎ直します。
        RefreshChanges();
        UpdateVideoPreview();
    }

    private void ReloadClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;
        if (!ConfirmDiscard("読み込み直すと、いまの入力は失われます。")) return;
        OpenSite(session.Root);
    }

    private void ChooseFolderClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard("フォルダーを変えると、いまの入力は失われます。")) return;

        var dialog = new OpenFolderDialog
        {
            Title = "halkaclub のフォルダーを選んでください",
            InitialDirectory = session?.Root ?? "",
        };
        if (dialog.ShowDialog(this) != true) return;

        if (!SitePaths.IsSiteRoot(dialog.FolderName))
        {
            MessageBox.Show(this,
                "halkaclub のフォルダーではないようです（CNAME・index.html・style.css が必要です）。",
                "選び直してください", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        OpenSite(dialog.FolderName);
    }

    private bool ConfirmDiscard(string message)
    {
        if (session == null || !session.HasChanges) return true;
        return MessageBox.Show(this, message, "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question)
            == MessageBoxResult.OK;
    }

    private void WindowClosing(object sender, CancelEventArgs e)
    {
        if (session != null && session.HasChanges)
        {
            var answer = MessageBox.Show(this, "保存していない変更があります。閉じてよろしいですか？",
                "確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.OK) { e.Cancel = true; return; }
        }

        previewServer?.Dispose();
        previewServer = null;
    }

    // --- 設定 ---------------------------------------------------------------

    private static LocalSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(SettingsPath)) ?? new LocalSettings();
        }
        catch (Exception)
        {
            // 設定が壊れていても起動は続けます。
        }
        return new LocalSettings();
    }

    private static void SaveSettings(LocalSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch (Exception)
        {
            // 設定を書けなくても編集はできます。
        }
    }
}
