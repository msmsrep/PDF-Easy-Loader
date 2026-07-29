using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// iTextによるPDFの暗号化判定・復号・テキスト抽出
/// </summary>
public interface IPdfService
{
    /// <summary>PDFが暗号化されているか判定する</summary>
    bool IsEncrypted(string path);

    /// <summary>
    /// パスワードを解除して output に書き出す。
    /// パスワード違いの場合は false を返し、errorMessage は null のままにする。
    /// </summary>
    bool TryDecrypt(string input, string output, string password, out string? errorMessage);

    /// <summary>復号済みPDFの1ページ目からメールの内容を抽出する</summary>
    MailContent ExtractMail(string pdfPath);
}
