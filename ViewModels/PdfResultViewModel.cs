using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDF_Easy_Loader.Models;
using PDF_Easy_Loader.Services;

namespace PDF_Easy_Loader.ViewModels;

/// <summary>
/// 処理したPDF1件分の表示
/// </summary>
public sealed partial class PdfResultViewModel : ObservableObject
{
    private readonly IClipboardService _clipboard;

    public PdfResultViewModel(PdfLoadResult result, IClipboardService clipboard)
    {
        _clipboard = clipboard;

        FileName = Path.GetFileName(result.SourcePath);
        Mail = result.Mail;
        IsSuccess = result.Status is PdfLoadStatus.NotEncrypted
            or PdfLoadStatus.Decrypted
            or PdfLoadStatus.DecryptedWithInput;
        StatusText = BuildStatusText(result);
    }

    public string FileName { get; }

    public MailContent Mail { get; }

    public bool IsSuccess { get; }

    public string StatusText { get; }

    public bool HasMail => Mail.HasAny;

    /// <summary>指定したテキストをクリップボードへコピーする</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy(string? text) => _clipboard.SetText(text!);

    private static bool CanCopy(string? text) => !string.IsNullOrEmpty(text);

    private static string BuildStatusText(PdfLoadResult result) => result.Status switch
    {
        PdfLoadStatus.NotEncrypted => "暗号化なし。そのまま開きました",
        PdfLoadStatus.Decrypted => $"復号して開きました（{result.MatchedLabel}）",
        PdfLoadStatus.DecryptedWithInput => "入力されたパスワードで復号して開きました",
        PdfLoadStatus.PasswordUnknown => result.ErrorMessage ?? "パスワードが分かりませんでした",
        _ => result.ErrorMessage ?? "処理に失敗しました",
    };
}
