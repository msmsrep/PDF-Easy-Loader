namespace PDF_Easy_Loader.Models;

/// <summary>
/// 次回起動時へ引き継ぐ画面の設定。
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// ウィンドウを常に手前へ表示するか。
    /// 他アプリからドラッグ＆ドロップしやすいよう、既定は固定する。
    /// </summary>
    public bool AlwaysOnTop { get; set; } = true;
}
