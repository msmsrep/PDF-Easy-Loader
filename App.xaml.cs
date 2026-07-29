using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PDF_Easy_Loader.Services;
using PDF_Easy_Loader.ViewModels;
using PDF_Easy_Loader.Views;

namespace PDF_Easy_Loader;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = BuildServices();

        // 前回終了時に消しきれなかった復号済みPDFを片付けてから始める
        var workspace = _services.GetRequiredService<ITempWorkspace>();
        workspace.Cleanup();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // ファイルの関連付けや「送る」から起動された場合は、その場で処理する
        var files = e.Args.Where(File.Exists).ToArray();

        if (files.Length > 0)
        {
            var viewModel = (MainViewModel)window.DataContext;
            _ = viewModel.LoadFilesCommand.ExecuteAsync(files);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 復号済みPDFをディスクに残さない
        _services?.GetRequiredService<ITempWorkspace>().Cleanup();
        _services?.Dispose();

        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPdfService, PdfService>();
        services.AddSingleton<IPasswordStore, DpapiPasswordStore>();
        services.AddSingleton<ITempWorkspace, TempWorkspace>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPdfLoader, PdfLoader>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        // 開くたびに保存済みの内容を読み直す
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
