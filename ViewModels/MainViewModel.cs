using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDF_Easy_Loader.Services;

namespace PDF_Easy_Loader.ViewModels;

/// <summary>
/// メイン画面。ドロップされたPDFを1件ずつ処理する。
/// </summary>
public sealed partial class MainViewModel(
    IPdfLoader loader,
    IClipboardService clipboard,
    IDialogService dialogs) : ObservableObject
{
    private const string PdfExtension = ".pdf";

    /// <summary>新しい結果が上に来るように先頭へ挿入する</summary>
    public ObservableCollection<PdfResultViewModel> Results { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "PDFをここにドラッグ＆ドロップしてください";

    public bool HasResults => Results.Count > 0;

    /// <summary>
    /// ドロップされたファイル、または起動引数で渡されたファイルを処理する
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadFilesAsync(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0) return;

        var pdfPaths = paths
            .Where(p => PdfExtension.Equals(Path.GetExtension(p), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfPaths.Count == 0)
        {
            StatusMessage = "PDFファイルではありません";
            return;
        }

        IsBusy = true;

        try
        {
            for (int i = 0; i < pdfPaths.Count; i++)
            {
                string path = pdfPaths[i];
                StatusMessage = $"処理中: {Path.GetFileName(path)}";

                var result = await loader.LoadAsync(path);
                var item = new PdfResultViewModel(result, clipboard);

                Results.Insert(0, item);
                OnPropertyChanged(nameof(HasResults));

                // 複数ドロップ時は最後の1件だけを履歴へ流し込む（履歴が混ざらないようにする）
                if (item.IsSuccess && i == pdfPaths.Count - 1)
                {
                    await clipboard.PushToHistoryAsync(result.Mail);
                }
            }

            StatusMessage = $"{pdfPaths.Count}件を処理しました";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSettings() => dialogs.ShowSettings();

    [RelayCommand]
    private void ClearResults()
    {
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        StatusMessage = "PDFをここにドラッグ＆ドロップしてください";
    }
}
