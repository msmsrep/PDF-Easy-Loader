using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// PDFに埋め込まれた本文HTML（body.html）を読み、プレーンテキストに変換する。
///
/// 対象のPDFはメールをPDF化するゲートウェイが生成したもので、
/// 元のメール本文が添付ファイルとして丸ごと入っている。
/// ページに描画されたテキストを拾うより確実で、
/// 「添付は Adobe Reader で開け」という定型文も混ざらない。
/// </summary>
public static partial class PdfEmbeddedBody
{
    static PdfEmbeddedBody()
    {
        // iso-2022-jp や shift_jis は .NET のランタイムに含まれないため追加する
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [GeneratedRegex(@"<script\b[^>]*>.*?</script\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptRegex { get; }

    [GeneratedRegex(@"<style\b[^>]*>.*?</style\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StyleRegex { get; }

    /// <summary>Wordが吐く条件付きコメント（&lt;!--[if gte mso 9]&gt;…）もここで落ちる</summary>
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex { get; }

    /// <summary>
    /// 改行に変換するタグ。開始タグではなく終了タグだけを見る。
    /// &lt;p&gt;A&lt;/p&gt;&lt;p&gt;B&lt;/p&gt; を1行ずつにし、
    /// 空段落 &lt;p&gt;&amp;nbsp;&lt;/p&gt; だけが空行になるようにするため
    /// </summary>
    [GeneratedRegex(@"<br\b[^>]*>|</(?:p|div|tr|li|h[1-6]|table|blockquote|ul|ol)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex { get; }

    /// <summary>表のセルの区切り。連結して「A-1005」のように読めなくなるのを防ぐ</summary>
    [GeneratedRegex(@"</(?:td|th)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex CellRegex { get; }

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagRegex { get; }

    [GeneratedRegex(@"charset\s*=\s*[""']?([\w\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetRegex { get; }

    /// <summary>タブはセルの区切りとして後から入れるので、ここでは潰さない</summary>
    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex SpacesRegex { get; }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLinesRegex { get; }

    /// <summary>
    /// 埋め込まれた本文HTMLをテキストにして返す。無ければ null
    /// </summary>
    public static string? Read(PdfDocument document)
    {
        byte[]? html = FindBodyHtml(document);

        if (html is null || html.Length == 0) return null;

        string text = ToPlainText(Decode(html));

        return text.Length == 0 ? null : text;
    }

    private static byte[]? FindBodyHtml(PdfDocument document)
    {
        var tree = document.GetCatalog().GetNameTree(PdfName.EmbeddedFiles);
        var names = tree.GetNames();

        if (names.Count == 0) return null;

        // body.html を優先し、無ければ最初のHTMLを使う
        byte[]? fallback = null;

        foreach (var entry in names)
        {
            string name = FileName(entry.Key, entry.Value);

            if (!name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            byte[]? content = ReadContent(entry.Value);

            if (content is null) continue;

            if (Path.GetFileNameWithoutExtension(name).Equals("body", StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }

            fallback ??= content;
        }

        return fallback;
    }

    /// <summary>
    /// 名前ツリーのキーは "A1" のような内部名のことがあるため、
    /// ファイル仕様辞書の /UF・/F を優先する
    /// </summary>
    private static string FileName(PdfString key, PdfObject value)
    {
        if (value is PdfDictionary spec)
        {
            string? name =
                spec.GetAsString(PdfName.UF)?.ToUnicodeString() ??
                spec.GetAsString(PdfName.F)?.ToUnicodeString();

            if (!string.IsNullOrEmpty(name)) return name;
        }

        return key.ToUnicodeString();
    }

    private static byte[]? ReadContent(PdfObject value)
    {
        if (value is not PdfDictionary spec) return null;

        var embedded = spec.GetAsDictionary(PdfName.EF);

        var stream =
            embedded?.GetAsStream(PdfName.F) ??
            embedded?.GetAsStream(PdfName.UF);

        try
        {
            return stream?.GetBytes();
        }
        catch (Exception)
        {
            // 壊れた添付で本文の抽出ごと落とさない
            return null;
        }
    }

    /// <summary>
    /// BOM、次に meta の charset を見てデコードする。判別できなければUTF-8
    /// </summary>
    private static string Decode(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        }

        if (content.Length >= 2)
        {
            if (content[0] == 0xFF && content[1] == 0xFE) return Encoding.Unicode.GetString(content);
            if (content[0] == 0xFE && content[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(content);
        }

        // meta タグ自体はASCIIの範囲で書かれているので、ASCIIとして読んで探せる
        string head = Encoding.ASCII.GetString(content, 0, Math.Min(content.Length, 2048));
        var match = CharsetRegex.Match(head);

        if (match.Success)
        {
            try
            {
                return Encoding.GetEncoding(match.Groups[1].Value).GetString(content);
            }
            catch (ArgumentException)
            {
                // 未知のエンコーディング名。UTF-8として読む
            }
        }

        return Encoding.UTF8.GetString(content);
    }

    private static string ToPlainText(string html)
    {
        string text = ScriptRegex.Replace(html, " ");
        text = StyleRegex.Replace(text, " ");
        text = CommentRegex.Replace(text, " ");

        // HTMLでは元の改行はただの空白。タグを改行に変換する前に潰しておかないと、
        // ソースの折り返しがそのまま空行になってしまう
        text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

        text = CellRegex.Replace(text, "\t");
        text = BreakRegex.Replace(text, "\n");
        text = TagRegex.Replace(text, string.Empty);

        // 実体参照を戻す前に空白を詰める。&nbsp; による意図的な空白は残したい
        text = SpacesRegex.Replace(text, " ");

        text = WebUtility.HtmlDecode(text);

        var lines = text
            .Split('\n')
            .Select(line => line.Replace(' ', ' ').Trim());

        return BlankLinesRegex.Replace(string.Join('\n', lines), "\n\n").Trim();
    }
}
