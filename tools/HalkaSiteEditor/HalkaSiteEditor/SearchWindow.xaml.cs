using System.Windows;
using System.Windows.Controls;
using HalkaSiteEditor.Core;

namespace HalkaSiteEditor;

/// <summary>直せるところを、タブをまたいで探す画面です。</summary>
public partial class SearchWindow : Window
{
    private readonly SiteSession session;

    /// <summary>「ここへ移る」で選ばれたもの（選ばずに閉じたら null）。</summary>
    public SearchHit? Chosen { get; private set; }

    public SearchWindow(SiteSession session)
    {
        InitializeComponent();
        this.session = session;
        Loaded += (_, _) => QueryBox.Focus();
    }

    private void QueryChanged(object sender, TextChangedEventArgs e)
    {
        if (HitList == null) return;

        var hits = SiteSearch.Find(session, QueryBox.Text);
        HitList.ItemsSource = hits;

        SummaryText.Text = QueryBox.Text.Trim().Length == 0
            ? "直せるところを、タブをまたいで探します。選んで「ここへ移る」を押すと、そのタブへ移ります。"
            : hits.Count == 0
                ? "見つかりませんでした。"
                : hits.Count >= SiteSearch.Limit
                    ? $"{hits.Count} 件以上見つかりました。多すぎるので、もう少し長い文字で探してください。"
                    : $"{hits.Count} 件見つかりました。";

        if (hits.Count > 0) HitList.SelectedIndex = 0;
    }

    private void HitSelected(object sender, SelectionChangedEventArgs e) =>
        GoButton.IsEnabled = HitList.SelectedItem is SearchHit;

    private void GoClick(object sender, RoutedEventArgs e)
    {
        if (HitList.SelectedItem is not SearchHit hit) return;
        Chosen = hit;
        Close();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
