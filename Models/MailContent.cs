namespace PDF_Easy_Loader.Models;

/// <summary>
/// PDF1ページ目から抽出したメールの内容
/// </summary>
public sealed class MailContent
{
    public string From { get; init; } = string.Empty;

    public string To { get; init; } = string.Empty;

    public string Cc { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    /// <summary>1件でも抽出できたか</summary>
    public bool HasAny =>
        !string.IsNullOrEmpty(From) ||
        !string.IsNullOrEmpty(To) ||
        !string.IsNullOrEmpty(Cc) ||
        !string.IsNullOrEmpty(Body);

    public static MailContent Empty { get; } = new();
}
