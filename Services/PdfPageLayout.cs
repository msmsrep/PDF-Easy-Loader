using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// ページ上の1行。ベースラインのY座標と、行頭のX座標を持つ
/// </summary>
/// <param name="Baseline">ベースラインのY。上ほど大きい</param>
/// <param name="Left">行頭のX</param>
public sealed record PageTextLine(float Baseline, float Left, string Text);

/// <summary>
/// ページに埋め込まれた mailto: リンク注釈1個
/// </summary>
public sealed record MailtoLink(string Address, float Baseline, float Left);

/// <summary>
/// PDFのテキストを座標付きで読み出したもの（1ページ分、または全ページを縦に積んだもの）。
/// メールヘッダーは「ラベル列」と「値列」に分かれた表組みで描かれるため、
/// 座標が分かるとフィールドの範囲を文字列の当て推量なしに決められる。
/// </summary>
public sealed class PdfPageLayout
{
    /// <summary>同じ行とみなすベースラインの許容差</summary>
    private const float BaselineTolerance = 2.0f;

    /// <summary>行送りが取れなかった場合に使う既定値</summary>
    private const float DefaultLinePitch = 12.0f;

    public required IReadOnlyList<PageTextLine> Lines { get; init; }

    public required IReadOnlyList<MailtoLink> MailtoLinks { get; init; }

    /// <summary>行送り（隣接行のベースライン差）の中央値</summary>
    public required float LinePitch { get; init; }

    /// <summary>座標を落として1つのテキストにしたもの</summary>
    public string Text => string.Join("\n", Lines.Select(l => l.Text));

    public static PdfPageLayout Empty { get; } = new()
    {
        Lines = [],
        MailtoLinks = [],
        LinePitch = DefaultLinePitch,
    };

    public static PdfPageLayout Read(PdfPage page)
    {
        var collector = new LineCollector();

        try
        {
            new TolerantCanvasProcessor(collector).ProcessPageContent(page);
        }
        catch (Exception)
        {
            // ページ単位の失敗で他のページごと落とさない。ここまでに拾えた分を使う
        }

        var lines = collector.BuildLines();

        return new PdfPageLayout
        {
            Lines = lines,
            MailtoLinks = ReadMailtoLinks(page),
            LinePitch = MedianPitch(lines),
        };
    }

    /// <summary>
    /// 全ページを読み、縦に積んだ1つのレイアウトとして返す。
    /// 本文が複数ページに渡るPDFで、2ページ目以降を取りこぼさないために使う。
    /// </summary>
    public static PdfPageLayout ReadAll(PdfDocument document)
    {
        int pageCount = document.GetNumberOfPages();

        if (pageCount == 0) return Empty;
        if (pageCount == 1) return Read(document.GetPage(1));

        var lines = new List<PageTextLine>();
        var links = new List<MailtoLink>();

        // ページごとにY座標は独立している（どのページも下端が0）ため、
        // 後続ページを前ページの続きの位置へずらして1続きの座標系にする
        float offset = 0f;

        for (int i = 1; i <= pageCount; i++)
        {
            var page = Read(document.GetPage(i));

            if (page.Lines.Count == 0) continue;

            if (lines.Count > 0)
            {
                // 前ページの最終行のちょうど1行下から始める。
                // ページの変わり目を段落の切れ目と誤認させないため、余分な間隔は空けない
                offset = lines[^1].Baseline - page.LinePitch - page.Lines[0].Baseline;
            }

            foreach (var line in page.Lines)
            {
                lines.Add(line with { Baseline = line.Baseline + offset });
            }

            foreach (var link in page.MailtoLinks)
            {
                links.Add(link with { Baseline = link.Baseline + offset });
            }
        }

        return new PdfPageLayout
        {
            Lines = lines,
            MailtoLinks = links,
            LinePitch = MedianPitch(lines),
        };
    }

    /// <summary>
    /// テキストしか手に入らない場合に、座標なしのレイアウトとして扱う
    /// </summary>
    public static PdfPageLayout FromText(string text)
    {
        var lines = text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select((t, i) => new PageTextLine(-i, 0f, t))
            .ToList();

        return new PdfPageLayout
        {
            Lines = lines,
            MailtoLinks = [],
            LinePitch = 1f,
        };
    }

    private static List<MailtoLink> ReadMailtoLinks(PdfPage page)
    {
        var links = new List<MailtoLink>();

        foreach (var annotation in page.GetAnnotations())
        {
            if (annotation is not PdfLinkAnnotation link) continue;

            var action = link.GetAction();

            if (action is null) continue;
            if (!PdfName.URI.Equals(action.GetAsName(PdfName.S))) continue;

            string? uri = action.GetAsString(PdfName.URI)?.ToUnicodeString();

            if (uri is null) continue;

            string? address = ParseMailto(uri);

            if (address is null) continue;

            var rectangle = link.GetRectangle()?.ToRectangle();

            if (rectangle is null) continue;

            // 注釈の矩形の下端はテキストのベースラインに一致する
            links.Add(new MailtoLink(address, rectangle.GetY(), rectangle.GetX()));
        }

        // 読み順（上から下、左から右）に並べる
        links.Sort((a, b) => a.Baseline.Equals(b.Baseline)
            ? a.Left.CompareTo(b.Left)
            : b.Baseline.CompareTo(a.Baseline));

        return links;
    }

    private static string? ParseMailto(string uri)
    {
        const string Scheme = "mailto:";

        if (!uri.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;

        string address = uri[Scheme.Length..];

        // mailto:addr?subject=... のパラメータを落とす
        int query = address.IndexOf('?');

        if (query >= 0) address = address[..query];

        try
        {
            address = Uri.UnescapeDataString(address);
        }
        catch (UriFormatException)
        {
            // エスケープが壊れていてもそのまま使う
        }

        address = address.Trim();

        return address.Length == 0 ? null : address;
    }

    /// <summary>
    /// 行送りの中央値。外れ値（段落の間隔）に引きずられないよう平均は使わない
    /// </summary>
    private static float MedianPitch(IReadOnlyList<PageTextLine> lines)
    {
        if (lines.Count < 2) return DefaultLinePitch;

        var gaps = new List<float>(lines.Count - 1);

        for (int i = 1; i < lines.Count; i++)
        {
            float gap = lines[i - 1].Baseline - lines[i].Baseline;

            if (gap > 0.1f) gaps.Add(gap);
        }

        if (gaps.Count == 0) return DefaultLinePitch;

        gaps.Sort();

        return gaps[gaps.Count / 2];
    }

    /// <summary>
    /// 演算子1個ごとの失敗を握りつぶすプロセッサ。
    ///
    /// フォントの ToUnicode が壊れたPDFでは、iTextが文字幅を測る途中で
    /// 不正なコードポイントに当たって例外を投げることがある。
    /// 既定のプロセッサはそこでページの解析ごと止まってしまうため、
    /// 問題のある描画命令だけを飛ばして残りを読み続ける。
    /// </summary>
    private sealed class TolerantCanvasProcessor(IEventListener listener) : PdfCanvasProcessor(listener)
    {
        protected override void InvokeOperator(PdfLiteral op, IList<PdfObject> operands)
        {
            try
            {
                base.InvokeOperator(op, operands);
            }
            catch (Exception)
            {
                // この命令の描画は諦めて次へ進む
            }
        }
    }

    /// <summary>
    /// 描画された文字列を座標付きで集め、ベースラインごとの行にまとめる
    /// </summary>
    private sealed class LineCollector : IEventListener
    {
        private readonly List<Chunk> _chunks = [];

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type != EventType.RENDER_TEXT) return;
            if (data is not TextRenderInfo info) return;

            string text = info.GetText();

            if (string.IsNullOrEmpty(text)) return;

            var baseline = info.GetBaseline();

            _chunks.Add(new Chunk(
                Baseline: baseline.GetStartPoint().Get(Vector.I2),
                Start: baseline.GetStartPoint().Get(Vector.I1),
                End: baseline.GetEndPoint().Get(Vector.I1),
                SpaceWidth: info.GetSingleSpaceWidth(),
                Text: text));
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_TEXT];

        public List<PageTextLine> BuildLines()
        {
            var lines = new List<PageTextLine>();

            if (_chunks.Count == 0) return lines;

            // 上から下、同じ行の中は左から右
            _chunks.Sort((a, b) => Math.Abs(a.Baseline - b.Baseline) <= BaselineTolerance
                ? a.Start.CompareTo(b.Start)
                : b.Baseline.CompareTo(a.Baseline));

            var current = new List<Chunk> { _chunks[0] };

            for (int i = 1; i < _chunks.Count; i++)
            {
                if (Math.Abs(_chunks[i].Baseline - current[0].Baseline) <= BaselineTolerance)
                {
                    current.Add(_chunks[i]);
                    continue;
                }

                Flush(lines, current);
                current = [_chunks[i]];
            }

            Flush(lines, current);

            return lines;
        }

        /// <summary>
        /// 1行分のチャンクを連結する。
        /// PDFは単語間の空白を座標のずらしで表現することがあるため、
        /// 間隔が空白1個分に届いていればこちらで空白を補う。
        /// </summary>
        private static void Flush(List<PageTextLine> lines, List<Chunk> chunks)
        {
            if (chunks.Count == 0) return;

            var text = new System.Text.StringBuilder();
            var previous = chunks[0];

            text.Append(previous.Text);

            for (int i = 1; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                float gap = chunk.Start - previous.End;

                bool needsSpace =
                    gap > previous.SpaceWidth * 0.5f &&
                    text.Length > 0 &&
                    !char.IsWhiteSpace(text[^1]) &&
                    chunk.Text.Length > 0 &&
                    !char.IsWhiteSpace(chunk.Text[0]);

                if (needsSpace) text.Append(' ');

                text.Append(chunk.Text);
                previous = chunk;
            }

            string line = text.ToString().TrimEnd();

            if (line.Length == 0) return;

            lines.Add(new PageTextLine(chunks[0].Baseline, chunks[0].Start, line));
        }

        private readonly record struct Chunk(
            float Baseline,
            float Start,
            float End,
            float SpaceWidth,
            string Text);
    }
}
