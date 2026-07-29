using System.Windows;
using System.Windows.Controls;
using PDF_Easy_Loader.ViewModels;

namespace PDF_Easy_Loader.Views;

public partial class PasswordPromptWindow : Window
{
    private readonly PasswordPromptViewModel _viewModel;

    public PasswordPromptWindow(PasswordPromptViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.RequestClose += OnRequestClose;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    /// <summary>
    /// PasswordBox.Password は依存関係プロパティではなくバインドできないため、
    /// ここだけコードビハインドでViewModelへ渡す
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = ((PasswordBox)sender).Password;

    private void OnRequestClose(object? sender, bool result)
    {
        DialogResult = result;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;

        // 入力欄に平文を残さない
        PasswordInput.Clear();

        base.OnClosed(e);
    }
}
