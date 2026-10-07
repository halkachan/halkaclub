using System.Windows;
using HalkaSiteEditor.Core;

namespace HalkaSiteEditor;

/// <summary>公開する前に、リンク切れや画像の欠けをまとめて見せる画面です。</summary>
public partial class CheckWindow : Window
{
    private readonly SiteSession session;

    public CheckWindow(SiteSession session)
    {
        InitializeComponent();
        this.session = session;
        Show(SiteCheck.Run(session));
    }

    private void Show(IReadOnlyList<CheckIssue> issues)
    {
        IssueList.ItemsSource = issues;

        var problems = issues.Count(issue => issue.Level == CheckLevel.Problem);
        var notices = issues.Count - problems;

        SummaryText.Text = issues.Count == 0
            ? "気になるところはありませんでした。"
            : problems == 0
                ? $"問題はありません。知らせておきたいことが {notices} 件あります。"
                : $"問題が {problems} 件、知らせておきたいことが {notices} 件あります。";
    }

    private void AgainClick(object sender, RoutedEventArgs e) => Show(SiteCheck.Run(session));

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
