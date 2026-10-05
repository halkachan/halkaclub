using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalkaSiteEditor.Core;
using Microsoft.Win32;

namespace HalkaSiteEditor;

public partial class MainWindow : Window
{
    private sealed class LocalSettings
    {
        public string SiteRoot { get; set; } = "";
    }

    private static readonly Brush ChangedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCB, 0x75));
    private static readonly Brush QuietBrush = new SolidColorBrush(Color.FromRgb(0x58, 0x61, 0x70));

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HalkaSiteEditor", "settings.json");

    private SiteSession? session;
    private EditField? videoField;

    public MainWindow() => InitializeComponent();

    // --- 起動と読み込み -----------------------------------------------------

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
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
        SaveSettings(new LocalSettings { SiteRoot = root });

        var commission = session.Groups.Single(group => group.Title == "依頼ページ");
        var top = session.Groups.Single(group => group.Title == "トップページ");
        var colors = session.Groups.Single(group => group.Title == "サイトの基本色");

        CommissionNote.Text = commission.Note;
        TopNote.Text = top.Note;
        ColorNote.Text = colors.Note;

        CommissionItems.ItemsSource = commission.Pairs;
        ColorItems.ItemsSource = colors.Fields;

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
        StatusText.Text = $"読み込みました。{session.Fields.Count()} 項目を編集できます。";
    }

    private void FieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditField.Value) or nameof(EditField.Changed) or nameof(EditField.Error))
            RefreshChanges();
    }

    private void RefreshChanges()
    {
        if (session == null) return;

        var changes = session.Changes();
        ChangeList.ItemsSource = changes;
        ChangeCountText.Text = changes.Count == 0 ? "" : $"{changes.Count} 件";

        var errors = session.Fields.Count(field => field.HasError);
        SaveButton.IsEnabled = session.HasChanges && errors == 0;
        RevertButton.IsEnabled = session.HasChanges;

        StatusText.Text = errors > 0
            ? $"直すところが {errors} 件あります。赤い文字の欄を確認してください。"
            : changes.Count == 0
                ? "変更はありません。"
                : $"{changes.Count} 件の変更があります。「保存する」でファイルに書き込みます。";
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

        RefreshChanges();
        UpdateVideoPreview();
        StatusText.Text = $"{changes.Count} 件を保存しました。ブラウザで確認してから、GitHub Desktop でコミットしてください。";
    }

    private void RevertClick(object sender, RoutedEventArgs e)
    {
        if (session == null || !session.HasChanges) return;
        var answer = MessageBox.Show(this, "入力した内容をすべて元に戻します。よろしいですか？",
            "元に戻す", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        session.Revert();
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
        if (session == null || !session.HasChanges) return;
        var answer = MessageBox.Show(this, "保存していない変更があります。閉じてよろしいですか？",
            "確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) e.Cancel = true;
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
