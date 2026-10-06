using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using HalkaSiteEditor.Core;

namespace HalkaSiteEditor;

/// <summary>新着の1本ぶん。選べるかどうかと、選ばれたかを持ちます。</summary>
public sealed class VideoChoice : INotifyPropertyChanged
{
    private bool selected;

    public FeedVideo Video { get; }
    public bool CanAdd { get; }
    public string Note { get; }

    public VideoChoice(FeedVideo video, bool alreadyAdded)
    {
        Video = video;
        CanAdd = !alreadyAdded;
        Note = alreadyAdded ? "もう作品一覧に入っています" : "";
    }

    public string Title => Video.Title;
    public string PublishedAt => Video.PublishedAt;

    public bool Selected
    {
        get => selected;
        set
        {
            if (selected == value || !CanAdd) return;
            selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            SelectionChanged?.Invoke();
        }
    }

    public event Action? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>YouTubeの新着から、作品一覧に足すものを選ぶ画面です。</summary>
public partial class VideoPickerWindow : Window
{
    private readonly SiteSession session;
    private List<VideoChoice> choices = new();

    /// <summary>足した本数。0 なら何もしていません。</summary>
    public int AddedCount { get; private set; }

    public VideoPickerWindow(SiteSession session, string channelId, WorkCategory? current)
    {
        InitializeComponent();
        this.session = session;

        ChannelBox.Text = channelId;
        CategoryPicker.ItemsSource = session.Works.Categories;
        CategoryPicker.SelectedItem = current ?? session.Works.Categories.FirstOrDefault();
    }

    /// <summary>画面で最後に使われたチャンネルID（覚えておくため）。</summary>
    public string ChannelId => ChannelBox.Text.Trim();

    private async void WindowLoaded(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void ReloadClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        StatusText.Text = "YouTubeから取っています…";
        VideoItems.ItemsSource = null;
        AddButton.IsEnabled = false;

        IReadOnlyList<FeedVideo> videos;
        try
        {
            videos = await YouTubeFeed.FetchAsync(ChannelBox.Text.Trim());
        }
        catch (Exception error)
        {
            StatusText.Text = "取れませんでした：" + error.Message;
            return;
        }

        var known = YouTubeFeed.KnownVideoIds(session.Works);
        choices = videos.Select(video => new VideoChoice(video, known.Contains(video.VideoId))).ToList();
        foreach (var choice in choices) choice.SelectionChanged += UpdateAddButton;

        VideoItems.ItemsSource = choices;
        var newCount = choices.Count(choice => choice.CanAdd);
        StatusText.Text = newCount == 0
            ? $"{choices.Count}本とも、もう作品一覧に入っています。"
            : $"{choices.Count}本のうち、まだ入っていないのは {newCount}本です。";
        UpdateAddButton();
    }

    private void UpdateAddButton() =>
        AddButton.IsEnabled = choices.Any(choice => choice.Selected);

    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (CategoryPicker.SelectedItem is not WorkCategory category)
        {
            MessageBox.Show(this, "足し先の分類を選んでください。", "確認",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var picked = choices.Where(choice => choice.Selected).ToArray();
        if (picked.Length == 0) return;

        foreach (var choice in picked)
        {
            var work = category.AddNew();
            work.Title = choice.Video.Title;
            work.PublishedAt = choice.Video.PublishedAt;
            work.YouTubeUrl = choice.Video.Url;
        }

        AddedCount = picked.Length;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();
}
