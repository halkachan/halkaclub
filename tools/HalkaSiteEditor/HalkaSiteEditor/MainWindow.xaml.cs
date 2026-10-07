using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
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
        TopRows.ItemsSource = top.Rows;

        TextPagePicker.ItemsSource = session.TextPages;
        TextPagePicker.SelectedIndex = 0;
        foreach (var page in session.TextPages)
        foreach (var block in page.Blocks)
            block.PropertyChanged += TextBlockChanged;

        BindOptionalGroup("はるかくらぶ", ClubTab, ClubNote, ClubItems);
        BindOptionalGroup("見出しと飾り", DecorTab, DecorNote, DecorItems);
        BindOptionalGroup("うたまぜ！", UtamazeTab, UtamazeNote, UtamazeItems);
        BindWorks();
        BindRelease();
        BindNews();
        BindClubUpdate();
        BindGames();
        BindMeta();
        BindLinkGrid();

        videoField = top.Fields.Single(field => field.Id == "top.latestVideo");
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

        session.Works.Categories.CollectionChanged += CategoriesCollectionChanged;
        foreach (var category in session.Works.Categories) Watch(category);
    }

    private void Watch(WorkCategory category)
    {
        category.PropertyChanged += WorkChanged;
        category.Works.CollectionChanged += WorksCollectionChanged;
        foreach (var work in category.Works) work.PropertyChanged += WorkChanged;
    }

    private void CategoriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (WorkCategory category in e.NewItems) Watch(category);
        RefreshChanges();
    }

    private void CategoryAddClick(object sender, RoutedEventArgs e)
    {
        var category = session?.Works.AddNewCategory();
        if (category == null) return;
        session?.Undo.Record("分類の追加", () =>
        {
            session.Works.Categories.Remove(category);
            CategoryPicker.SelectedIndex = 0;
        });
        CategoryPicker.SelectedItem = category;
        StatusText.Text = "一番下に分類を足しました。名前と合い言葉を入れてください。";
    }

    private void CategoryUpClick(object sender, RoutedEventArgs e) => MoveCategory(-1);

    private void CategoryDownClick(object sender, RoutedEventArgs e) => MoveCategory(1);

    private void MoveCategory(int offset)
    {
        var category = SelectedCategory;
        if (category == null || session == null) return;
        Moved("分類の並べ替え", category, session.Works.Categories.IndexOf, session.Works.MoveCategory, offset);
        CategoryPicker.SelectedItem = category;
    }

    private void CategoryDeleteClick(object sender, RoutedEventArgs e)
    {
        var category = SelectedCategory;
        if (category == null || session == null) return;
        if (session.Works.Categories.Count <= 1)
        {
            MessageBox.Show(this, "分類は1つ以上必要です。", "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(this,
            $"分類「{category.Name}」を、中の作品{category.Works.Count}件ごと一覧から外します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var at = session.Works.Categories.IndexOf(category);
        session.Works.Categories.Remove(category);
        session.Undo.Record($"分類「{category.Name}」の削除",
            () => session.Works.Categories.Insert(at, category));
        CategoryPicker.SelectedIndex = 0;
    }

    // --- トップページのリンク集 ---------------------------------------------

    private void BindLinkGrid()
    {
        var grid = session?.Links;
        if (grid == null)
        {
            LinkGridBox.Visibility = Visibility.Collapsed;
            return;
        }

        LinkGridBox.Visibility = Visibility.Visible;
        LinkCardItems.ItemsSource = grid.Cards;
        grid.Cards.CollectionChanged += LinkCardsCollectionChanged;
        foreach (var card in grid.Cards) card.PropertyChanged += LinkCardChanged;
    }

    private void LinkCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (LinkCard card in e.NewItems) card.PropertyChanged += LinkCardChanged;
        if (e.OldItems != null)
            foreach (LinkCard card in e.OldItems) card.PropertyChanged -= LinkCardChanged;
        RefreshChanges();
    }

    private void LinkCardChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    private void LinkAddClick(object sender, RoutedEventArgs e)
    {
        var added = session?.Links?.AddNew();
        if (added != null) session?.Undo.Record("リンクの追加", () => session.Links!.Cards.Remove(added));
        StatusText.Text = "一番下にリンクを足しました。名前とリンク先を入れてください。";
    }

    private void LinkUpClick(object sender, RoutedEventArgs e) => MoveLink(sender, -1);

    private void LinkDownClick(object sender, RoutedEventArgs e) => MoveLink(sender, 1);

    private void MoveLink(object sender, int offset)
    {
        var grid = session?.Links;
        if (((FrameworkElement)sender).Tag is not LinkCard card || grid == null) return;
        Moved("リンクの並べ替え", card, grid.Cards.IndexOf, grid.Move, offset);
    }

    private void LinkDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not LinkCard card) return;
        var answer = MessageBox.Show(this, $"「{card.Name}」をリンク集から外します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var grid = session?.Links;
        if (grid == null) return;
        var at = grid.Cards.IndexOf(card);
        grid.Cards.Remove(card);
        session!.Undo.Record($"リンク「{card.Name}」の削除", () => grid.Cards.Insert(at, card));
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

    // --- うれしいこと -------------------------------------------------------

    private void BindNews()
    {
        var news = session?.News;
        if (news == null) return;

        NewsItems.ItemsSource = news.Items;
        news.Items.CollectionChanged += NewsCollectionChanged;
        foreach (var item in news.Items) item.PropertyChanged += NewsChanged;
    }

    private void NewsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (NewsItem item in e.NewItems) item.PropertyChanged += NewsChanged;
        if (e.OldItems != null)
            foreach (NewsItem item in e.OldItems) item.PropertyChanged -= NewsChanged;
        RefreshChanges();
    }

    private void NewsChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    /// <summary>くらぶの更新内容（1行＝1項目）。</summary>
    private void BindClubUpdate()
    {
        var update = session?.ClubUpdate;
        if (update == null)
        {
            ClubUpdateBox.Visibility = Visibility.Collapsed;
            return;
        }

        ClubUpdateBox.Visibility = Visibility.Visible;
        ClubUpdateHint.Text = update.Hint;
        ClubUpdateText.SetBinding(TextBox.TextProperty, new Binding(nameof(ParagraphRun.Text))
        {
            Source = update,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        update.PropertyChanged += ClubUpdateChanged;
        ClubUpdateError.Text = update.Error ?? "";
    }

    private void ClubUpdateChanged(object? sender, PropertyChangedEventArgs e)
    {
        ClubUpdateError.Text = session?.ClubUpdate?.Error ?? "";
        RefreshChanges();
    }

    private void NewsAddClick(object sender, RoutedEventArgs e)
    {
        var added = session?.News?.AddNew();
        if (added != null) session?.Undo.Record("お知らせの追加", () => session.News!.Items.Remove(added));
        StatusText.Text = "一番下にお知らせを足しました。日付と見出しを入れてください。";
    }

    private void NewsUpClick(object sender, RoutedEventArgs e) => MoveNews(sender, -1);

    private void NewsDownClick(object sender, RoutedEventArgs e) => MoveNews(sender, 1);

    private void MoveNews(object sender, int offset)
    {
        var news = session?.News;
        if (((FrameworkElement)sender).Tag is not NewsItem item || news == null) return;
        Moved("お知らせの並べ替え", item, news.Items.IndexOf, news.Move, offset);
    }

    private void NewsDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not NewsItem item) return;
        var answer = MessageBox.Show(this, $"「{item.Title}」を消します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var news = session?.News;
        if (news == null) return;
        var at = news.Items.IndexOf(item);
        news.Items.Remove(item);
        session!.Undo.Record($"お知らせ「{item.Title}」の削除", () => news.Items.Insert(at, item));
    }

    // --- ページの顔（タイトルとOGP）と画像 ----------------------------------

    private void BindMeta()
    {
        var meta = session?.Meta;
        if (meta == null)
        {
            MetaTab.Visibility = Visibility.Collapsed;
        }
        else
        {
            MetaTab.Visibility = Visibility.Visible;
            MetaNote.Text = session!.Groups.Single(group => group.Title == "ページの顔").Note;
            MetaPagePicker.ItemsSource = meta.Pages;
            MetaPagePicker.SelectedIndex = 0;
            CardImagesButton.IsEnabled = CardImages.IsAvailable(session.Root);
        }

        ProfileBox.Visibility = session?.Profile == null ? Visibility.Collapsed : Visibility.Visible;
        foreach (var image in session?.Images ?? Array.Empty<ImageSlot>())
            image.PropertyChanged += ImageChanged;
        RefreshImages();
    }

    private void ImageChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    private MetaPage? SelectedMetaPage => MetaPagePicker?.SelectedItem as MetaPage;

    private void MetaPageChanged(object sender, SelectionChangedEventArgs e)
    {
        // XAMLを読んでいる途中にも飛んできます。
        if (MetaRows == null || CardBox == null) return;

        var page = SelectedMetaPage;
        MetaRows.ItemsSource = page?.Rows;
        CardBox.Visibility = page?.Card == null ? Visibility.Collapsed : Visibility.Visible;
        RefreshImages();

        if (PagePicker?.ItemsSource is IReadOnlyList<SitePage> pages && page != null)
        {
            var wanted = pages.FirstOrDefault(candidate => candidate.Url == page.Url);
            if (wanted != null) PagePicker.SelectedItem = wanted;
        }
    }

    /// <summary>画像の見本と、いまの状態の1行を出し直します。</summary>
    private void RefreshImages()
    {
        var card = SelectedMetaPage?.Card;
        if (CardSummary != null)
        {
            CardSummary.Text = card?.Summary ?? "";
            CardError.Text = card?.Error ?? "";
            CardClearButton.IsEnabled = card?.SourcePath != null;
            CardPreview.Source = LoadImage(card?.SourcePath ?? card?.Path);
        }

        var profile = session?.Profile;
        if (ProfileSummary != null && profile != null)
        {
            ProfileSummary.Text = profile.Summary;
            ProfileError.Text = profile.Error ?? "";
            ProfileClearButton.IsEnabled = profile.SourcePath != null;
            ProfilePreview.Source = LoadImage(profile.SourcePath ?? profile.Path);
        }
    }

    /// <summary>見本用。読み終わったら手を離すので、あとで差し替えられます。</summary>
    private static BitmapImage? LoadImage(string? path)
    {
        if (path == null || !File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ImageSlot? SlotFor(object sender) =>
        (((FrameworkElement)sender).Tag as string) == "profile" ? session?.Profile : SelectedMetaPage?.Card;

    private void CardChooseClick(object sender, RoutedEventArgs e)
    {
        var slot = SlotFor(sender);
        if (slot == null) return;

        var dialog = new OpenFileDialog
        {
            Title = $"{slot.Label}に使う画像を選んでください",
            Filter = slot.CardSize ? "PNG画像|*.png" : "GIF画像|*.gif",
        };
        if (dialog.ShowDialog(this) != true) return;

        slot.Choose(dialog.FileName);
        RefreshImages();
        StatusText.Text = slot.HasError
            ? slot.Error!
            : $"{slot.Label}を差し替えます。「保存する」で入れ替わります。";
    }

    private void CardClearClick(object sender, RoutedEventArgs e)
    {
        SlotFor(sender)?.Clear();
        RefreshImages();
    }

    private void CardImagesClick(object sender, RoutedEventArgs e)
    {
        if (session?.Meta == null) return;

        var cards = session.Meta.Cards.ToArray();
        var answer = MessageBox.Show(this,
            $"カード画像{cards.Length}枚を作り直します。\n\n" +
            "tools/ogp/generate-ogp.py を動かします（Python と Pillow が必要です）。\n" +
            "画像はすぐ入れ替わり、読み込み側の番号は「保存する」で上がります。",
            "作り直しますか？", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        Cursor = System.Windows.Input.Cursors.Wait;
        CardImages.Result result;
        try
        {
            result = CardImages.Run(session.Root, cards.Select(card => card.Relative));
        }
        finally
        {
            Cursor = null;
        }

        if (!result.Ok)
        {
            MessageBox.Show(this, result.Output, "作り直せませんでした",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var card in cards.Where(card => result.Changed.Contains(card.Relative)))
            card.MarkRegenerated();

        RefreshImages();
        RefreshChanges();
        RefreshPreview();
        StatusText.Text = result.Changed.Count == 0
            ? "作り直しましたが、絵は変わりませんでした。"
            : $"{result.Changed.Count}枚が変わりました。「保存する」で番号を上げてください。";
    }

    private void SitemapClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;

        var urls = Sitemap.Urls(session.Root);
        var answer = MessageBox.Show(this,
            $"いま検索に載せられるページは{urls.Count}枚です。\n\n" +
            string.Join("\n", urls.Select(url => "・" + url)) + "\n\n" +
            "この一覧で sitemap.xml と robots.txt を作り直します。",
            "作り直しますか？", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        try
        {
            var written = Sitemap.Write(session.Root);
            StatusText.Text = written.Count == 0
                ? "sitemap.xml と robots.txt は、すでに最新でした。"
                : string.Join("と", written) + " を書き直しました。「公開する」で出せます。";
            RefreshPublishState();
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "書けませんでした", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- ゲーム -------------------------------------------------------------

    /// <summary>並びの先頭が一覧ページ、そのあとがゲーム1本ずつ。</summary>
    private sealed record GameChoice(string Label, GamePage? Page, bool IsWorld = false)
    {
        public override string ToString() => Label;
    }

    private void BindGames()
    {
        var games = session?.Games;
        if (games == null)
        {
            GameTab.Visibility = Visibility.Collapsed;
            return;
        }

        GameTab.Visibility = Visibility.Visible;
        GameNote.Text = session!.Groups.Single(group => group.Title == "ゲーム").Note;

        GameLeadBox.SetBinding(TextBox.TextProperty, new Binding(nameof(EditField.Value))
        {
            Source = games.Lead,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });
        GameCardItems.ItemsSource = games.Collection.Cards;
        games.Collection.Cards.CollectionChanged += GameCardsCollectionChanged;
        foreach (var card in games.Collection.Cards) card.PropertyChanged += GameCardChanged;

        foreach (var page in games.Pages)
        {
            page.Changelog.CollectionChanged += ChangelogCollectionChanged;
            foreach (var entry in page.Changelog) entry.PropertyChanged += ChangelogChanged;
            if (page.Description != null) page.Description.PropertyChanged += ChangelogChanged;
        }

        var choices = new List<GameChoice> { new("ゲーム集（一覧ページ）", null) };
        choices.AddRange(games.Pages.Select(page => new GameChoice(page.Title.Value, page)));
        if (session.World != null) choices.Add(new GameChoice("HALKA WORLD", null, IsWorld: true));
        GamePicker.ItemsSource = choices;
        GamePicker.SelectedIndex = 0;

        BindWorld();
    }

    private void GameCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (GameCard card in e.NewItems) card.PropertyChanged += GameCardChanged;
        if (e.OldItems != null)
            foreach (GameCard card in e.OldItems) card.PropertyChanged -= GameCardChanged;
        RefreshChanges();
    }

    private void GameCardChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    private void ChangelogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (GameChangelogEntry entry in e.NewItems) entry.PropertyChanged += ChangelogChanged;
        if (e.OldItems != null)
            foreach (GameChangelogEntry entry in e.OldItems) entry.PropertyChanged -= ChangelogChanged;
        RefreshChanges();
    }

    private void ChangelogChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    private GamePage? SelectedGame => (GamePicker?.SelectedItem as GameChoice)?.Page;

    private bool WorldSelected => (GamePicker?.SelectedItem as GameChoice)?.IsWorld == true;

    // --- HALKA WORLD の更新履歴 ---------------------------------------------

    private void BindWorld()
    {
        var world = session?.World;
        if (world == null) return;

        WorldItems.ItemsSource = world.Archives;
        world.Archives.CollectionChanged += WorldCollectionChanged;
        foreach (var archive in world.Archives) WatchArchive(archive);

        if (world.CurrentVersion != null)
        {
            WorldVersionBox.SetBinding(TextBox.TextProperty, new Binding(nameof(EditField.Value))
            {
                Source = world.CurrentVersion,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });
            world.CurrentVersion.PropertyChanged += WorldChanged;
        }
    }

    private void WatchArchive(WorldArchive archive)
    {
        archive.PropertyChanged += WorldChanged;
        archive.Entries.CollectionChanged += WorldEntriesChanged;
        foreach (var entry in archive.Entries) entry.PropertyChanged += WorldChanged;
    }

    private void WorldCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (WorldArchive archive in e.NewItems) WatchArchive(archive);
        RefreshChanges();
    }

    private void WorldEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (WorldEntry entry in e.NewItems) entry.PropertyChanged += WorldChanged;
        if (e.OldItems != null)
            foreach (WorldEntry entry in e.OldItems) entry.PropertyChanged -= WorldChanged;
        RefreshChanges();
    }

    private void WorldChanged(object? sender, PropertyChangedEventArgs e) => RefreshChanges();

    /// <summary>その件が入っている箱を探します。</summary>
    private WorldArchive? ArchiveOf(WorldEntry entry) =>
        session?.World?.Archives.FirstOrDefault(archive => archive.Entries.Contains(entry));

    private void WorldUpClick(object sender, RoutedEventArgs e) => MoveWorld(sender, -1);

    private void WorldDownClick(object sender, RoutedEventArgs e) => MoveWorld(sender, 1);

    private void MoveWorld(object sender, int offset)
    {
        if (((FrameworkElement)sender).Tag is not WorldEntry entry) return;
        var archive = ArchiveOf(entry);
        if (archive == null || session?.World == null) return;
        Moved("HALKA WORLD の履歴の並べ替え", entry, archive.Entries.IndexOf,
            (item, by) => session.World.Move(archive, item, by), offset);
    }

    private void WorldDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not WorldEntry entry) return;
        var archive = ArchiveOf(entry);
        if (archive == null) return;

        var answer = MessageBox.Show(this, $"「{entry.Version}」の履歴を消します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var at = archive.Entries.IndexOf(entry);
        archive.Entries.Remove(entry);
        session?.Undo.Record($"HALKA WORLD「{entry.Version}」の削除", () => archive.Entries.Insert(at, entry));
    }

    private void WorldArchiveAddClick(object sender, RoutedEventArgs e)
    {
        var world = session?.World;
        if (world == null) return;

        var archive = world.AddNewArchive();
        session?.Undo.Record("HALKA WORLD の箱の追加", () => world.Archives.Remove(archive));
        RefreshChanges();
        StatusText.Text = "いちばん上に箱を作りました。名前（「ver3.1 ～ ver4.0」など）を入れてください。";
    }

    private void WorldAlignClick(object sender, RoutedEventArgs e)
    {
        var world = session?.World;
        if (world?.CurrentVersion == null) return;

        var before = world.CurrentVersion.Value;
        world.AlignVersion();
        if (world.CurrentVersion.Value != before)
            session?.Undo.Record("HALKA WORLD の版をそろえる", () => world.CurrentVersion!.Value = before);
        RefreshChanges();
    }


    private void GamePickerChanged(object sender, SelectionChangedEventArgs e)
    {
        // XAMLを読んでいる途中にも飛んできます。
        if (GameListPanel == null || GamePagePanel == null) return;

        var page = SelectedGame;
        var world = WorldSelected;
        GameListPanel.Visibility = page == null && !world ? Visibility.Visible : Visibility.Collapsed;
        GamePagePanel.Visibility = page == null ? Visibility.Collapsed : Visibility.Visible;
        WorldPanel.Visibility = world ? Visibility.Visible : Visibility.Collapsed;
        GameCardAddButton.Visibility = page == null && !world ? Visibility.Visible : Visibility.Collapsed;
        ChangelogAddButton.Visibility = page?.HasChangelog == true || world
            ? Visibility.Visible : Visibility.Collapsed;
        WorldArchiveAddButton.Visibility = world ? Visibility.Visible : Visibility.Collapsed;

        GameRows.ItemsSource = page?.Rows;
        GameDescription.ItemsSource = page?.Description == null
            ? null
            : new[] { page.Description };
        ChangelogSection.Visibility = page?.HasChangelog == true ? Visibility.Visible : Visibility.Collapsed;
        ChangelogItems.ItemsSource = page?.Changelog;
        RefreshVersionSummary();

        // 見ているものに合わせて、右のプレビューも移します。
        if (PagePicker?.ItemsSource is IReadOnlyList<SitePage> pages)
        {
            var url = world ? "/halkaworld/" : page == null ? "/game/" : $"/game/{page.Slug}/";
            var wanted = pages.FirstOrDefault(candidate => candidate.Url == url);
            if (wanted != null) PagePicker.SelectedItem = wanted;
        }
    }

    private void RefreshVersionSummary()
    {
        var page = SelectedGame;
        VersionSummary.Text = page?.VersionSummary ?? "";
        AlignVersionButton.IsEnabled = page?.CanAlignVersion == true;

        if (WorldSummary == null) return;
        WorldSummary.Text = session?.World?.VersionSummary ?? "";
        WorldAlignButton.IsEnabled = session?.World?.CanAlignVersion == true;
    }

    private void GameCardAddClick(object sender, RoutedEventArgs e)
    {
        var added = session?.Games?.Collection.AddNew();
        if (added != null)
            session?.Undo.Record("ゲームのカードの追加", () => session.Games!.Collection.Cards.Remove(added));
        StatusText.Text = "一番下にカードを足しました。名前とゲームページの場所を入れてください。";
    }

    private void GameCardUpClick(object sender, RoutedEventArgs e) => MoveGameCard(sender, -1);

    private void GameCardDownClick(object sender, RoutedEventArgs e) => MoveGameCard(sender, 1);

    private void MoveGameCard(object sender, int offset)
    {
        var collection = session?.Games?.Collection;
        if (((FrameworkElement)sender).Tag is not GameCard card || collection == null) return;
        Moved("ゲームのカードの並べ替え", card, collection.Cards.IndexOf, collection.Move, offset);
    }

    private void GameCardDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not GameCard card) return;
        var answer = MessageBox.Show(this,
            $"「{card.Title}」を一覧から外します。ゲームのページ自体は残ります。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var collection = session?.Games?.Collection;
        if (collection == null) return;
        var at = collection.Cards.IndexOf(card);
        collection.Cards.Remove(card);
        session!.Undo.Record($"ゲーム「{card.Title}」のカードの削除", () => collection.Cards.Insert(at, card));
    }

    private void ChangelogAddClick(object sender, RoutedEventArgs e)
    {
        if (WorldSelected)
        {
            var world = session?.World;
            var added = world?.AddNewEntry();
            if (added == null || world == null) return;
            session?.Undo.Record($"HALKA WORLD「{added.Version}」の追加",
                () => world.Archives.First().Entries.Remove(added));
            StatusText.Text = $"いちばん上に「{added.Version}」を足しました。内容を入れてください。";
            return;
        }

        var page = SelectedGame;
        var entry = page?.AddNewEntry();
        if (entry == null || page == null) return;
        session?.Undo.Record($"更新履歴「{entry.Version}」の追加", () => page.Changelog.Remove(entry));
        StatusText.Text = $"更新履歴の一番上に「{entry.Version}」を足しました。内容を入れてください。";
    }

    private void ChangelogUpClick(object sender, RoutedEventArgs e) => MoveChangelog(sender, -1);

    private void ChangelogDownClick(object sender, RoutedEventArgs e) => MoveChangelog(sender, 1);

    private void MoveChangelog(object sender, int offset)
    {
        var page = SelectedGame;
        if (((FrameworkElement)sender).Tag is not GameChangelogEntry entry || page == null) return;
        Moved("更新履歴の並べ替え", entry, page.Changelog.IndexOf, page.MoveEntry, offset);
    }

    private void ChangelogDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not GameChangelogEntry entry) return;
        var answer = MessageBox.Show(this, $"「{entry.Version}」の履歴を消します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var page = SelectedGame;
        if (page == null) return;
        var at = page.Changelog.IndexOf(entry);
        page.Changelog.Remove(entry);
        session?.Undo.Record($"更新履歴「{entry.Version}」の削除", () => page.Changelog.Insert(at, entry));
    }

    private void AlignVersionClick(object sender, RoutedEventArgs e)
    {
        var page = SelectedGame;
        if (page == null) return;

        var version = page.Version?.Value;
        var tags = page.Card?.Tags;
        page.AlignVersion();
        session?.Undo.Record("版をそろえる", () =>
        {
            if (page.Version != null && version != null) page.Version.Value = version;
            if (page.Card != null && tags != null) page.Card.Tags = tags;
        });
        RefreshChanges();
    }

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
        var release = session?.Utamaze;
        if (release == null) return;

        var before = release.Switches.Select(step => step.MakeLive).ToArray();
        release.MakeAllLive();
        session!.Undo.Record("まとめて公開の形にする", () =>
        {
            for (var i = 0; i < release.Switches.Count && i < before.Length; i++)
                release.Switches[i].MakeLive = before[i];
        });
        RefreshChanges();
    }

    private WorkCategory? SelectedCategory => CategoryPicker?.SelectedItem as WorkCategory;

    private void CategoryPickerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkItems == null || CategoryBox == null) return;
        WorkItems.ItemsSource = SelectedCategory?.Works;
        CategoryBox.DataContext = SelectedCategory;
        CategoryBox.Visibility = SelectedCategory == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PickVideosClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;

        var saved = LoadSettings();
        var channel = string.IsNullOrWhiteSpace(saved.YouTubeChannelId)
            ? YouTubeFeed.DefaultChannelId
            : saved.YouTubeChannelId;

        // 新しい動画は、どの分類でも一番下に足されます。選ぶ前の数を覚えておけば、戻せます。
        var before = session.Works.Categories.ToDictionary(
            category => category, category => category.Works.Count);

        var picker = new VideoPickerWindow(session, channel, SelectedCategory) { Owner = this };
        picker.ShowDialog();

        if (picker.ChannelId != channel)
            SaveSettings(new LocalSettings { SiteRoot = session.Root, YouTubeChannelId = picker.ChannelId });

        if (picker.AddedCount > 0)
        {
            session.Undo.Record($"動画{picker.AddedCount}本の追加", () =>
            {
                foreach (var (category, count) in before)
                    while (category.Works.Count > count) category.Works.RemoveAt(category.Works.Count - 1);
            });
            StatusText.Text = $"{picker.AddedCount} 本を足しました。タイトルは直せます。保存を忘れずに。";
            RefreshChanges();
        }
    }

    private void WorkAddClick(object sender, RoutedEventArgs e)
    {
        var category = SelectedCategory;
        if (category == null) return;
        var added = category.AddNew();
        session?.Undo.Record("作品の追加", () => category.Works.Remove(added));
        StatusText.Text = "一番下に作品を足しました。タイトルと投稿日とURLを入れてください。";
    }

    private void WorkUpClick(object sender, RoutedEventArgs e) => MoveWork(sender, -1);

    private void WorkDownClick(object sender, RoutedEventArgs e) => MoveWork(sender, 1);

    private void MoveWork(object sender, int offset)
    {
        var category = SelectedCategory;
        if (((FrameworkElement)sender).Tag is not WorkItem work || category == null) return;
        Moved("作品の並べ替え", work, category.Works.IndexOf, category.Move, offset);
    }

    private void WorkDeleteClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not WorkItem work) return;
        var answer = MessageBox.Show(this, $"「{work.Title}」を一覧から外します。よろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        var category = SelectedCategory;
        if (category == null) return;
        var at = category.Works.IndexOf(work);
        category.Works.Remove(work);
        session?.Undo.Record($"作品「{work.Title}」の削除", () => category.Works.Insert(at, work));
    }

    private void RefreshChanges()
    {
        if (session == null) return;

        var changes = session.Changes();
        ChangeList.ItemsSource = changes;
        ChangeCountText.Text = changes.Count == 0 ? "" : $"{changes.Count} 件";

        RefreshVersionSummary();
        RefreshImages();
        GameLeadError.Text = session.Games?.Lead.Error ?? "";

        var errors = (session.Utamaze?.Switches.Count(step => step.HasError) ?? 0)
            + session.Fields.Count(field => field.HasError)
            + session.Works.Categories.Sum(category => category.Works.Count(work => work.HasError))
            + session.TextPages.Sum(page => page.Blocks.Count(block => block.HasError))
            + (session.News?.Items.Count(item => item.HasError) ?? 0)
            + (session.ClubUpdate?.HasError == true ? 1 : 0)
            + (session.Games?.Collection.Cards.Count(card => card.HasError) ?? 0)
            + (session.Games?.Pages.Sum(page => page.Changelog.Count(entry => entry.HasError)) ?? 0)
            + session.Images.Count(image => image.HasError)
            + session.Works.Categories.Count(category => category.HasError)
            + (session.Links?.Cards.Count(card => card.HasError) ?? 0)
            + (session.World?.Archives.Sum(archive =>
                archive.Entries.Count(entry => entry.HasError)) ?? 0);
        SaveButton.IsEnabled = session.HasChanges && !session.HasError;
        RevertButton.IsEnabled = session.HasChanges;
        UndoButton.IsEnabled = session.Undo.CanUndo;
        UndoButton.ToolTip = session.Undo.NextLabel is { } next
            ? $"「{next}」を元に戻します。"
            : "戻せる手がありません（文字の打ち直しは、入力欄の中で Ctrl+Z です）。";
        BackupButton.IsEnabled = session.Backups.LatestLabel != null;
        BackupButton.ToolTip = session.Backups.LatestLabel is { } taken
            ? $"{taken} の保存を、書き込む前の中身に戻します。"
            : "戻せる控えがありません。";

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

    // --- 1手ずつ戻す・保存の控え・近道 --------------------------------------

    /// <summary>並べ替えは、実際に動いたときだけ覚えます（端で止まったときは覚えません）。</summary>
    private void Moved<T>(string label, T item, Func<T, int> indexOf, Action<T, int> move, int offset)
    {
        var before = indexOf(item);
        move(item, offset);
        var after = indexOf(item);
        if (after != before) session?.Undo.Record(label, () => move(item, before - after));
    }

    private void UndoClick(object sender, RoutedEventArgs e)
    {
        var label = session?.Undo.Undo();
        if (label == null) return;
        RefreshChanges();
        StatusText.Text = $"「{label}」を元に戻しました。";
    }

    private void RestoreBackupClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;

        var label = session.Backups.LatestLabel;
        if (label == null)
        {
            MessageBox.Show(this, "戻せる控えがありません。", "確認", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            $"いちばん新しい控え（{label}）を書き戻します。\n\n" +
            "いまのファイルは、保存する前の中身に戻ります。よろしいですか？",
            "保存を1つ前に戻しますか？", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) return;

        IReadOnlyList<string> restored;
        try
        {
            restored = session.Backups.Restore(session.Root);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "戻せませんでした", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var root = session.Root;
        OpenSite(root);
        RefreshPreview();
        StatusText.Text = $"{restored.Count} ファイルを保存前に戻しました（{string.Join("、", restored)}）。";
    }

    private void ShortcutClick(object sender, RoutedEventArgs e)
    {
        var exe = Environment.ProcessPath;
        if (exe == null)
        {
            MessageBox.Show(this, "EXEの場所が分かりませんでした。", "作れませんでした",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = Shortcut.Create(exe);
        if (!result.Ok)
        {
            MessageBox.Show(this, result.Error ?? "作れませんでした。", "作れませんでした",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(this,
            "デスクトップとスタートメニューに近道を作りました。\n\n" + string.Join("\n", result.Created),
            "作りました", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CheckClick(object sender, RoutedEventArgs e)
    {
        if (session == null) return;
        new CheckWindow(session) { Owner = this }.ShowDialog();
    }

    private void OpenLiveSiteClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = session == null ? "https://halkaclub.com/" : Sitemap.Origin(session.Root) + "/";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, "開けませんでした", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
        if (((FrameworkElement)sender).Tag is not FieldPair pair) return;
        var before = pair.En.Value;
        pair.SyncEnglishFromJapanese();
        if (pair.En.Value != before) session?.Undo.Record("英語を合わせる", () => pair.En.Value = before);
    }

    private void SyncOptionEnglishClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not OptionRow row) return;
        var before = row.Price.En.Value;
        row.SyncEnglishFromJapanese();
        if (row.Price.En.Value != before)
            session?.Undo.Record("英語を合わせる", () => row.Price.En.Value = before);
    }

    // --- 依頼ページの文章 ---------------------------------------------------

    private void TextBlockChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PageTextBlock.Text) or nameof(PageTextBlock.Changed)
            or nameof(PageTextBlock.Error))
            RefreshChanges();
    }

    private void TextPageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextBlockItems == null) return;
        var page = TextPagePicker.SelectedItem as PageTextPage;
        TextBlockItems.ItemsSource = page?.Blocks;

        // 見ているページに合わせて、右のプレビューも移します。
        if (page != null && PagePicker?.ItemsSource is IReadOnlyList<SitePage> pages)
        {
            var wanted = pages.FirstOrDefault(candidate => candidate.Url == page.Url);
            if (wanted != null) PagePicker.SelectedItem = wanted;
        }
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
        BindWorks();
        BindNews();   // 元に戻すと作品の一覧は作り直されるので、つなぎ直します。
        BindGames();
        BindLinkGrid();
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
