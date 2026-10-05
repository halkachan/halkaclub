using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace HalkaSiteEditor;

public partial class App : Application
{
    /// <summary>落ちた理由を書き出す場所。原因が分からないまま消えるのを防ぎます。</summary>
    public static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalkaSiteEditor", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Write(args.ExceptionObject as Exception);
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Write(e.Exception);
        MessageBox.Show(
            e.Exception.Message + "\n\n詳しい内容は次の場所に残しました。\n" + CrashLogPath,
            "エラーが起きました", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void Write(Exception? error)
    {
        if (error == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{error}\n\n");
        }
        catch (Exception)
        {
            // 記録できなくても、これ以上できることはありません。
        }
    }
}
