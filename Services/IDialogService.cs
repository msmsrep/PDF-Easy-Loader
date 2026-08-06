namespace PDF_Easy_Loader.Services;

/// <summary>
/// ViewModelから画面を直接触らずにダイアログを出すための窓口
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 登録済みパスワードで開けなかったときに入力を求める。
    /// キャンセルされた場合は null。入力値は保存しない。
    /// </summary>
    string? AskPassword(string fileName);

    /// <summary>
    /// ファイル選択ダイアログでPDFを選ばせる。
    /// キャンセルされた場合は空の配列。
    /// </summary>
    IReadOnlyList<string> PickPdfFiles();

    /// <summary>設定画面をモーダルで開く</summary>
    void ShowSettings();

    void ShowError(string message);
}
