using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PDF_Easy_Loader.ViewModels;
using PDF_Easy_Loader.Views;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// 実際にWPFのウィンドウを開くIDialogService
/// </summary>
public sealed class DialogService(IServiceProvider services) : IDialogService
{
    public string? AskPassword(string fileName)
    {
        var viewModel = new PasswordPromptViewModel(fileName);

        var window = new PasswordPromptWindow(viewModel)
        {
            Owner = ActiveOwner,
        };

        return window.ShowDialog() == true ? viewModel.Password : null;
    }

    public void ShowSettings()
    {
        var window = new SettingsWindow(services.GetRequiredService<SettingsViewModel>())
        {
            Owner = ActiveOwner,
        };

        window.ShowDialog();
    }

    public void ShowError(string message) =>
        MessageBox.Show(ActiveOwner, message, "PDF Easy Loader", MessageBoxButton.OK, MessageBoxImage.Warning);

    /// <summary>モーダルの親にするウィンドウ。起動直後などで無い場合は null のままにする</summary>
    private static Window? ActiveOwner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;
}
