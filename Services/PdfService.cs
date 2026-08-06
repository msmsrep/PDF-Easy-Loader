using System.IO;
using System.Text;
using iText.Kernel.Exceptions;
using iText.Kernel.Pdf;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// iText版のPDF操作
/// </summary>
public sealed class PdfService : IPdfService
{
    public PdfService()
    {
        // FontAsian をインスタンス化することで、CJKフォントのCMapを強制的にロードさせる
        _ = new iText.FontAsian.FontAsianDummyInitializer();
    }

    public bool IsEncrypted(string path)
    {
        try
        {
            using var reader = new PdfReader(path);

            // オーナーパスワードのみの場合に開けるようにする
            reader.SetUnethicalReading(true);

            using var doc = new PdfDocument(reader);

            return reader.IsEncrypted();
        }
        catch (BadPasswordException)
        {
            // ユーザーパスワードが必要 = 暗号化されている
            return true;
        }
    }

    public bool TryDecrypt(string input, string output, string password, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            var props = new ReaderProperties()
                .SetPassword(Encoding.UTF8.GetBytes(password));

            using var reader = new PdfReader(input, props);
            reader.SetUnethicalReading(true);

            // 暗号化を指定しない = 復号された状態で保存される
            using var writer = new PdfWriter(output);
            using var doc = new PdfDocument(reader, writer);

            return true;
        }
        catch (BadPasswordException)
        {
            // パスワード違い。呼び出し側が次の候補を試せるよう、エラー扱いにはしない
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        DeleteQuietly(output);

        return false;
    }

    public MailContent ExtractMail(string pdfPath)
    {
        try
        {
            // 既にパスワードは解除されている一時ファイルを読み込む
            using var reader = new PdfReader(pdfPath);
            using var doc = new PdfDocument(reader);

            if (doc.GetNumberOfPages() == 0) return MailContent.Empty;

            // 1ページ目を座標付きで読む。ヘッダーの列位置と mailto: リンクの位置が要る
            var layout = PdfPageLayout.Read(doc.GetPage(1));
            var mail = MailHeaderParser.Parse(layout);

            // 元のメール本文が添付されていれば、ページの描画テキストより優先する
            string? embeddedBody = PdfEmbeddedBody.Read(doc);

            return embeddedBody is null ? mail : mail with { Body = embeddedBody };
        }
        catch (Exception)
        {
            // テキスト抽出の失敗でPDFを開くメイン処理を落とさない
            return MailContent.Empty;
        }
    }

    /// <summary>失敗時に残る中途半端な出力ファイルを消す</summary>
    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // なにもしない
        }
    }
}
