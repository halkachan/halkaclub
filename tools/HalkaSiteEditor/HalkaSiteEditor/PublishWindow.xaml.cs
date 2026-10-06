using System.Windows;
using HalkaSiteEditor.Core;

namespace HalkaSiteEditor;

/// <summary>公開の前に、何がどこへ出るのかを見せて確かめる画面です。</summary>
public partial class PublishWindow : Window
{
    private readonly GitPublisher publisher;
    private readonly IReadOnlyList<string> managed;

    /// <summary>公開まで終わったか。</summary>
    public bool Published { get; private set; }

    public PublishWindow(GitPublisher publisher, IReadOnlyList<string> managed,
        IReadOnlyList<PendingFile> pending)
    {
        InitializeComponent();
        this.publisher = publisher;
        this.managed = managed;

        var branch = publisher.Branch();
        TargetText.Text = branch == "main"
            ? "送り先：GitHub の main ／ 反映までおよそ30秒かかります。"
            : $"送り先：GitHub の {branch} ／ ふだんは main です。いまのブランチで本当によいか確かめてください。";

        FileList.ItemsSource = pending;
        MessageBox.Text = GitPublisher.SuggestMessage(pending);
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();

    private void PublishClick(object sender, RoutedEventArgs e)
    {
        var message = MessageBox.Text.Trim();
        if (message.Length == 0)
        {
            System.Windows.MessageBox.Show(this, "説明を入れてください。", "確認",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Busy(true, "公開しています…");
        var result = publisher.Publish(managed, message);

        if (!result.Ok && result.RejectedByRemote)
        {
            var answer = System.Windows.MessageBox.Show(this,
                "別の場所から先に変更が入っていました。\n" +
                "向こうの変更を取り込んでから、もう一度公開しますか？\n\n" +
                "（作業中のほかのファイルは、いったん避けて自動で戻します）",
                "取り込みますか？", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) { Busy(false, "公開していません。"); return; }

            Busy(true, "取り込んでから公開しています…");
            result = publisher.PullRebaseAndPush();
        }

        Busy(false, "");

        if (!result.Ok)
        {
            System.Windows.MessageBox.Show(this,
                "公開できませんでした。\n\n" + result.Output +
                "\n\n手元のファイルはそのままです。GitHub Desktop から確かめてください。",
                "公開できません", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Published = true;
        Close();
    }

    private void Busy(bool busy, string text)
    {
        PublishButton.IsEnabled = !busy;
        BusyText.Text = text;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
        // 画面の表示を先に進めてから、時間のかかる処理に入ります。
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
    }
}
