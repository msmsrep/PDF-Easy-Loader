using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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
    IDialogService dialogs,
    IAppSettingsStore settingsStore,
    IAttachmentExtractor attachments) : ObservableObject
{
    private const string PdfExtension = ".pdf";

    private const string ReadyMessage = "PDFをドラッグ＆ドロップ、貼り付け（Ctrl+V）、またはファイルを選択してください";

    /// <summary>新しい結果が上に来るように先頭へ挿入する</summary>
    public ObservableCollection<PdfResultViewModel> Results { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = ReadyMessage;

    public bool HasResults => Results.Count > 0;

    /// <summary>
    /// ウィンドウを常に手前へ表示するか。
    /// 他アプリからドラッグ＆ドロップする間に隠れないよう、切り替え内容は次回起動へ引き継ぐ。
    /// </summary>
    [ObservableProperty]
    public partial bool IsAlwaysOnTop { get; set; } = settingsStore.Load().AlwaysOnTop;

    partial void OnIsAlwaysOnTopChanged(bool value)
    {
        // 他の設定項目を消さないよう、保存済みの内容へ上書きしてから書き戻す
        var settings = settingsStore.Load();
        settings.AlwaysOnTop = value;
        settingsStore.Save(settings);
    }

    /// <summary>
    /// ドロップされたデータを処理する。
    /// Outlookの添付はパスを持たないため、いったんファイルへ取り出してから読み込む。
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DropAsync(IDataObject? data)
    {
        if (data is null) return;

        var paths = attachments.Extract(data);

        if (paths.Count == 0)
        {
            StatusMessage = "ファイルを取り出せませんでした";
            return;
        }

        await LoadFilesAsync(paths);
    }

    /// <summary>
    /// クリップボードの内容を処理する。
    /// Outlookの添付はドラッグ＆ドロップできない場面があるため、コピー＆貼り付けでも受け付ける。
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task PasteAsync()
    {
        var paths = attachments.ExtractFromClipboard();

        if (paths.Count == 0)
        {
            StatusMessage = "クリップボードにファイルがありません";
            return;
        }

        await LoadFilesAsync(paths);
    }

    /// <summary>
    /// ファイル選択や起動引数で渡されたファイルを処理する
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadFilesAsync(IReadOnlyList<string>? paths)
    {
        // ドロップとファイル選択が同時に走らないようにする
        if (IsBusy) return;

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

    /// <summary>
    /// ファイル選択ダイアログで選ばれたPDFを処理する
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task BrowseFilesAsync() => LoadFilesAsync(dialogs.PickPdfFiles());

    [RelayCommand]
    private void OpenSettings() => dialogs.ShowSettings();

    [RelayCommand]
    private void ClearResults()
    {
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        StatusMessage = ReadyMessage;
    }
}
