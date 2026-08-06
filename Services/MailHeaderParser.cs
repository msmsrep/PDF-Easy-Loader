using System.Text;
using System.Text.RegularExpressions;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// PDF（全ページ）のレイアウトを、メールのFrom/To/Cc/Subject/本文に分解する。
///
/// ヘッダーは「ラベル列（From: など）」と「値列」に分かれた表組みとして描かれるので、
/// 座標からフィールドの範囲を決め、アドレスはページに埋め込まれた mailto: リンク注釈から取る。
/// 注釈が無いPDFでは、同じ範囲のテキストを解析するフォールバックへ落ちる。
/// </summary>
public static partial class MailHeaderParser
{
    /// <summary>同じ列とみなすXの許容差</summary>
    private const float ColumnTolerance = 4.0f;

    /// <summary>この行数を超えて空きがあればヘッダーの終わりとみなす</summary>
    private const float BlockGapRatio = 2.5f;

    /// <summary>本文で段落の切れ目とみなす行送りの倍率</summary>
    private const float ParagraphGapRatio = 1.6f;

    [GeneratedRegex(@"^\s*(From|To|Cc|Bcc|Reply-To|Subject|Date|Sent)\s*:[ \t]*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex LabelRegex { get; }

    /// <summary>区切り線や、PDF変換ゲートウェイが差し込む定型文。ここでヘッダーは終わり</summary>
    [GeneratedRegex(@"^\s*(?:-{3,}|_{3,}|\*{3,}|This message contains)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndRegex { get; }

    /// <summary>「添付は Adobe Reader で開け」という案内。本文ではないので落とす</summary>
    [GeneratedRegex(@"^\s*This message contains embedded attachments", RegexOptions.IgnoreCase)]
    private static partial Regex NoticeStartRegex { get; }

    [GeneratedRegex(@"adobe\.com/reader", RegexOptions.IgnoreCase)]
    private static partial Regex NoticeEndRegex { get; }

    public static MailContent Parse(string pageText) =>
        string.IsNullOrWhiteSpace(pageText)
            ? MailContent.Empty
            : Parse(PdfPageLayout.FromText(pageText));

    public static MailContent Parse(PdfPageLayout layout)
    {
        if (layout.Lines.Count == 0) return MailContent.Empty;

        var header = HeaderBlock.Detect(layout);

        // メールのヘッダーが無いPDF。せめて全文を本文として渡す
        if (header is null) return new MailContent { Body = BuildBody(layout, 0) };

        header.AssignLinks(layout);

        return new MailContent
        {
            From = header.FormatAddresses("From"),
            To = header.FormatAddresses("To"),
            Cc = header.FormatAddresses("Cc"),
            Subject = header.Value("Subject"),
            Body = BuildBody(layout, header.EndIndex),
        };
    }

    /// <summary>
    /// ヘッダーブロックより下を本文として組み立てる。
    /// 行間が広く空いた箇所は段落の切れ目として空行を入れる
    /// </summary>
    private static string BuildBody(PdfPageLayout layout, int startIndex)
    {
        var body = new StringBuilder();
        PageTextLine? previous = null;

        for (int i = startIndex; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];

            if (NoticeStartRegex.IsMatch(line.Text))
            {
                i = SkipNotice(layout.Lines, i);
                previous = null;
                continue;
            }

            if (previous is not null)
            {
                float gap = previous.Baseline - line.Baseline;

                body.Append('\n');

                if (gap > layout.LinePitch * ParagraphGapRatio) body.Append('\n');
            }

            body.Append(line.Text);
            previous = line;
        }

        return body.ToString().Trim();
    }

    /// <summary>
    /// 定型文の案内を読み飛ばし、最後の行のインデックスを返す。
    /// 終わりが見つからなければ開始行だけを落とす
    /// </summary>
    private static int SkipNotice(IReadOnlyList<PageTextLine> lines, int startIndex)
    {
        const int MaxNoticeLines = 8;

        int limit = Math.Min(lines.Count, startIndex + MaxNoticeLines);

        for (int i = startIndex; i < limit; i++)
        {
            if (NoticeEndRegex.IsMatch(lines[i].Text)) return i;
        }

        return startIndex;
    }

    /// <summary>
    /// ヘッダーの1フィールド。折り返した継続行も持つ
    /// </summary>
    private sealed class HeaderField(string label)
    {
        private readonly StringBuilder _value = new();

        public string Label { get; } = label;

        public List<float> Baselines { get; } = [];

        public List<MailtoLink> Links { get; } = [];

        public string Value => _value.ToString().Trim();

        /// <summary>
        /// 継続行は空白を挟まずに繋ぐ。
        /// 折り返しはカンマの直後で起きるのが普通で、
        /// アドレスの途中で折り返された場合に空白を入れるとアドレスが壊れるため
        /// </summary>
        public void Append(string text, float baseline)
        {
            _value.Append(text);
            Baselines.Add(baseline);
        }
    }

    /// <summary>
    /// 検出したヘッダーブロック
    /// </summary>
    private sealed class HeaderBlock
    {
        private readonly List<HeaderField> _fields = [];

        /// <summary>ヘッダーの次の行（＝本文の先頭）</summary>
        public int EndIndex { get; private init; }

        public static HeaderBlock? Detect(PdfPageLayout layout)
        {
            var lines = layout.Lines;

            int start = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (!LabelRegex.IsMatch(lines[i].Text)) continue;

                start = i;
                break;
            }

            if (start < 0) return null;

            float labelColumn = lines[start].Left;

            // 値列が独立しているレイアウトかどうか。座標が無いテキストでは常に false になる。
            // 本文の字下げを列と誤認しないよう、ヘッダーになり得る範囲だけを見る
            bool hasColumns = false;
            int probeEnd = ProbeEnd(layout, start);

            for (int i = start; i < probeEnd; i++)
            {
                if (lines[i].Left <= labelColumn + ColumnTolerance) continue;

                hasColumns = true;
                break;
            }

            var block = new List<HeaderField>();
            int end = lines.Count;

            for (int i = start; i < lines.Count; i++)
            {
                var line = lines[i];

                if (i > start)
                {
                    float gap = lines[i - 1].Baseline - line.Baseline;

                    if (gap > layout.LinePitch * BlockGapRatio)
                    {
                        end = i;
                        break;
                    }
                }

                var match = LabelRegex.Match(line.Text);

                if (match.Success && Math.Abs(line.Left - labelColumn) <= ColumnTolerance)
                {
                    var field = new HeaderField(Canonical(match.Groups[1].Value));
                    field.Append(match.Groups[2].Value, line.Baseline);
                    block.Add(field);
                    continue;
                }

                bool isContinuation =
                    block.Count > 0 &&
                    !IsBlockEnd(line) &&
                    (!hasColumns || line.Left > labelColumn + ColumnTolerance);

                if (!isContinuation)
                {
                    end = i;
                    break;
                }

                block[^1].Append(line.Text, line.Baseline);
            }

            var result = new HeaderBlock { EndIndex = end };
            result._fields.AddRange(block);

            return result;
        }

        /// <summary>
        /// ヘッダーが続き得る範囲の終わり。
        /// 区切り線・定型文・大きな行間のいずれかが来たらそこから先は確実にヘッダーではない
        /// </summary>
        private static int ProbeEnd(PdfPageLayout layout, int start)
        {
            var lines = layout.Lines;

            for (int i = start + 1; i < lines.Count; i++)
            {
                if (IsBlockEnd(lines[i])) return i;
                if (lines[i - 1].Baseline - lines[i].Baseline > layout.LinePitch * BlockGapRatio) return i;
            }

            return lines.Count;
        }

        /// <summary>
        /// ここから先はヘッダーではない、と判断できる行か。
        /// 空行はヘッダーと本文の区切り（座標のあるPDFでは空行自体が現れないので、
        /// 代わりに行間の空きで判定する）
        /// </summary>
        private static bool IsBlockEnd(PageTextLine line) =>
            string.IsNullOrWhiteSpace(line.Text) || BlockEndRegex.IsMatch(line.Text);

        /// <summary>
        /// mailto: リンクを、そのY座標に最も近いヘッダー行のフィールドへ振り分ける。
        /// ヘッダーブロックの外にある署名などのリンクはここで捨てられる
        /// </summary>
        public void AssignLinks(PdfPageLayout layout)
        {
            if (layout.MailtoLinks.Count == 0) return;

            float tolerance = Math.Max(layout.LinePitch * 0.6f, 3f);

            foreach (var link in layout.MailtoLinks)
            {
                HeaderField? owner = null;
                float best = tolerance;

                foreach (var field in _fields)
                {
                    foreach (float baseline in field.Baselines)
                    {
                        float distance = Math.Abs(baseline - link.Baseline);

                        if (distance > best) continue;

                        best = distance;
                        owner = field;
                    }
                }

                owner?.Links.Add(link);
            }
        }

        public string Value(string label) =>
            _fields.FirstOrDefault(f => f.Label == label)?.Value ?? string.Empty;

        /// <summary>
        /// リンク注釈を正としつつ、表示名はテキスト側から補う。
        /// リンクが無いフィールドはテキストの解析結果だけを返す
        /// </summary>
        public string FormatAddresses(string label)
        {
            var field = _fields.FirstOrDefault(f => f.Label == label);

            if (field is null) return string.Empty;

            var fromText = AddressListParser.Parse(field.Value);
            var items = new List<MailAddressItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var link in field.Links)
            {
                if (!seen.Add(link.Address)) continue;

                var named = fromText.FirstOrDefault(
                    t => string.Equals(t.Address, link.Address, StringComparison.OrdinalIgnoreCase));

                items.Add(new MailAddressItem(named?.DisplayName ?? string.Empty, link.Address));
            }

            // リンクが張られていないアドレスを取りこぼさない
            foreach (var item in fromText)
            {
                if (seen.Add(item.Address)) items.Add(item);
            }

            return AddressListParser.Format(items);
        }

        private static string Canonical(string label) => label.ToLowerInvariant() switch
        {
            "from" => "From",
            "to" => "To",
            "cc" => "Cc",
            "bcc" => "Bcc",
            "reply-to" => "Reply-To",
            "subject" => "Subject",
            "sent" or "date" => "Date",
            _ => label,
        };
    }
}
