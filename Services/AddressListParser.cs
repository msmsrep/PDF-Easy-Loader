using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// メールアドレス1件。表示名とアドレスを分けて保持する
/// </summary>
public sealed record MailAddressItem(string DisplayName, string Address);

/// <summary>
/// 「表示名 &lt;addr&gt;, "表示名" &lt;addr&gt;」形式のアドレスリストを分解・整形する。
/// 1件ごとのパースは自前の正規表現ではなく <see cref="MailAddress"/> に任せる。
/// </summary>
public static partial class AddressListParser
{
    /// <summary>表示名に含まれていたら引用符で囲う必要がある文字</summary>
    private static readonly char[] MustQuote = [',', ';', '<', '>', '"', '@', ':', '\\'];

    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9\-]+(?:\.[a-zA-Z0-9\-]+)*\.[a-zA-Z]{2,}")]
    private static partial Regex AddressRegex { get; }

    /// <summary>
    /// アドレスリストを1件ずつに分解する。アドレスで重複排除する
    /// </summary>
    public static IReadOnlyList<MailAddressItem> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        var items = new List<MailAddressItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string part in Split(value))
        {
            var item = ParseOne(part);

            if (item is null) continue;
            if (!seen.Add(item.Address)) continue;

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// 貼り付けやすいよう「; 」で連結する。
    /// 表示名がアドレスと同じ（Outlookが出力する "addr" &lt;addr&gt; 形式）なら表示名を落とす
    /// </summary>
    public static string Format(IEnumerable<MailAddressItem> items) =>
        string.Join("; ", items.Select(Format));

    public static string Format(MailAddressItem item)
    {
        string name = item.DisplayName.Trim();

        if (name.Length == 0) return item.Address;
        if (string.Equals(name, item.Address, StringComparison.OrdinalIgnoreCase)) return item.Address;

        if (name.IndexOfAny(MustQuote) >= 0)
        {
            name = $"\"{name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }

        return $"{name} <{item.Address}>";
    }

    /// <summary>
    /// 引用符と山括弧の外側にあるカンマ・セミコロンで区切る。
    /// 表示名の中のカンマ（"山田, 太郎" など）で切ってしまわないようにする
    /// </summary>
    private static IEnumerable<string> Split(string value)
    {
        var buffer = new StringBuilder();
        bool inQuote = false;
        bool inAngle = false;
        bool escaped = false;

        foreach (char c in value)
        {
            if (escaped)
            {
                buffer.Append(c);
                escaped = false;
                continue;
            }

            switch (c)
            {
                case '\\' when inQuote:
                    buffer.Append(c);
                    escaped = true;
                    continue;

                case '"':
                    inQuote = !inQuote;
                    break;

                case '<' when !inQuote:
                    inAngle = true;
                    break;

                case '>' when !inQuote:
                    inAngle = false;
                    break;

                case ',' or ';' when !inQuote && !inAngle:
                    yield return buffer.ToString();
                    buffer.Clear();
                    continue;
            }

            buffer.Append(c);
        }

        yield return buffer.ToString();
    }

    private static MailAddressItem? ParseOne(string part)
    {
        string text = part.Trim();

        if (text.Length == 0) return null;

        // まず .NET のパーサに任せる。"表示名" <addr> も 表示名 <addr> も解釈できる
        if (MailAddress.TryCreate(text, out var parsed) && parsed is not null)
        {
            return new MailAddressItem(parsed.DisplayName, parsed.Address);
        }

        // パースできない崩れた表記からは、アドレスらしき部分だけを拾う
        var match = AddressRegex.Match(text);

        if (!match.Success) return null;

        string display = string.Concat(text[..match.Index], text[(match.Index + match.Length)..]);

        return new MailAddressItem(CleanDisplayName(display), match.Value);
    }

    private static string CleanDisplayName(string display) =>
        display.Trim().Trim('<', '>', '"', ',', ';').Trim();
}
