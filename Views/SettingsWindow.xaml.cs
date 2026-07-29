using System.Windows;
using System.Windows.Controls;
using PDF_Easy_Loader.ViewModels;

namespace PDF_Easy_Loader.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // 登録後にPasswordBoxを空にする
        _viewModel.PasswordCleared += OnPasswordCleared;
    }

    /// <summary>
    /// PasswordBox.Password はバインドできないため、ここだけコードビハインドでViewModelへ渡す
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.NewPassword = ((PasswordBox)sender).Password;

    private void OnPasswordCleared(object? sender, EventArgs e) => NewPasswordInput.Clear();

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PasswordCleared -= OnPasswordCleared;
        NewPasswordInput.Clear();

        base.OnClosed(e);
    }
}
