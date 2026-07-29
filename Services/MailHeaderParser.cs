using System.Text.RegularExpressions;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// PDFから抽出した生テキストを、メールのFrom/To/Cc/Subject/本文に分解する
/// </summary>
public static partial class MailHeaderParser
{
    /// <summary>
    /// アドレス単体ではなく「"表示名" &lt;addr&gt;」「表示名 &lt;addr&gt;」といった
    /// メールヘッダー特有の記述を丸ごと捕まえる
    /// </summary>
    [GeneratedRegex(@"(?:""[^""]+""|[^,<\n]+)?\s*<?[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}>?")]
    private static partial Regex AddressRegex { get; }

    [GeneratedRegex(@"From:(.*?)(?=To:|Cc:|Subject:|Date:|\n\n|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FromSectionRegex { get; }

    [GeneratedRegex(@"To:(.*?)(?=Cc:|Subject:|From:|Date:|\n\n|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ToSectionRegex { get; }

    /// <summary>
    /// 目視できる区切り線（ハイフンやアンダースコアの3連以上）や
    /// 「This message」の開始文の手前までをCcの範囲とみなす
    /// </summary>
    [GeneratedRegex(@"Cc:(.*?)(?=Subject:|From:|To:|Date:|-{3,}|_{3,}|This message|\n\n|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CcSectionRegex { get; }

    [GeneratedRegex(@"Subject:[ \t]*(.*?)(?=\r?\n|$)", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectRegex { get; }

    [GeneratedRegex(@"(?:Cc:.*?(?:-{3,}|_{3,}|This message|\n\n)|Subject:.*?\n|Date:.*?\n)(.*)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BodyRegex { get; }

    [GeneratedRegex(@"\n\n(.*)", RegexOptions.Singleline)]
    private static partial Regex FallbackBodyRegex { get; }

    /// <summary>
    /// PDFのページテキストからメールの各要素を取り出す
    /// </summary>
    public static MailContent Parse(string pageText)
    {
        if (string.IsNullOrWhiteSpace(pageText)) return MailContent.Empty;

        return new MailContent
        {
            From = ExtractAddresses(pageText, FromSectionRegex),
            To = ExtractAddresses(pageText, ToSectionRegex),
            Cc = ExtractAddresses(pageText, CcSectionRegex),
            Subject = ExtractSubject(pageText),
            Body = ExtractBody(pageText),
        };
    }

    /// <summary>
    /// セクションを切り出し、その中のアドレスを重複を除いて「; 」で連結する
    /// </summary>
    private static string ExtractAddresses(string pageText, Regex sectionRegex)
    {
        var section = sectionRegex.Match(pageText);

        if (!section.Success) return string.Empty;

        var addresses = AddressRegex.Matches(section.Value)
            .Select(m => m.Value.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct();

        return string.Join("; ", addresses);
    }

    private static string ExtractSubject(string pageText)
    {
        var match = SubjectRegex.Match(pageText);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExtractBody(string pageText)
    {
        var match = BodyRegex.Match(pageText);

        if (match.Success) return match.Groups[1].Value.Trim();

        // ヘッダー行が途切れた「2連続改行」以降を本文としてフォールバック抽出
        var fallback = FallbackBodyRegex.Match(pageText);

        return fallback.Success ? fallback.Groups[1].Value.Trim() : string.Empty;
    }
}
