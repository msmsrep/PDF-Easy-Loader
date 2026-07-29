using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PDF_Easy_Loader.ViewModels;

/// <summary>
/// 登録済みパスワードで開けなかったときの入力ダイアログ。
/// 入力値は保存せず、その場限りで使う。
/// </summary>
public sealed partial class PasswordPromptViewModel(string fileName) : ObservableObject
{
    public string FileName { get; } = fileName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>OK / キャンセルの結果をViewへ返す（true = OK）</summary>
    public event EventHandler<bool>? RequestClose;

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok() => RequestClose?.Invoke(this, true);

    private bool CanOk() => !string.IsNullOrEmpty(Password);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, false);
}
