namespace PDF_Easy_Loader.Models;

/// <summary>
/// PDF1件の処理結果
/// </summary>
public enum PdfLoadStatus
{
    /// <summary>暗号化されていないのでそのまま開いた</summary>
    NotEncrypted,

    /// <summary>登録済みパスワードで復号できた</summary>
    Decrypted,

    /// <summary>その場で入力されたパスワードで復号できた</summary>
    DecryptedWithInput,

    /// <summary>パスワードが分からず処理を中止した</summary>
    PasswordUnknown,

    /// <summary>読み込みや復号に失敗した</summary>
    Failed,
}

/// <summary>
/// PDF1件の処理結果
/// </summary>
public sealed class PdfLoadResult
{
    public required PdfLoadStatus Status { get; init; }

    /// <summary>ドロップされた元ファイルのパス</summary>
    public required string SourcePath { get; init; }

    /// <summary>実際に開いたファイルのパス。失敗時は null</summary>
    public string? OpenedPath { get; init; }

    /// <summary>復号に成功したパスワードのラベル。手入力時は null</summary>
    public string? MatchedLabel { get; init; }

    public MailContent Mail { get; init; } = MailContent.Empty;

    public string? ErrorMessage { get; init; }
}
