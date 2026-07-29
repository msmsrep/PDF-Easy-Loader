namespace PDF_Easy_Loader.Models;

/// <summary>
/// 設定画面で登録するパスワード1件分
/// </summary>
public sealed class PasswordEntry
{
    /// <summary>取引先名など、利用者が識別するためのラベル</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>PDFのユーザーパスワード（保存時はDPAPIで暗号化される）</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>最後に復号へ成功した日時。次回の試行順を決めるために使う</summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}
