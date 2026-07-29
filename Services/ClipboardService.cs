using System.Windows;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// クリップボードへの書き込み
/// </summary>
public interface IClipboardService
{
    /// <summary>1件だけコピーする</summary>
    void SetText(string text);

    /// <summary>
    /// 本文→Cc→To→From の順にコピーして、クリップボード履歴(Win+V)に4件並べる。
    /// 最後にコピーしたFromが最新になる。
    /// </summary>
    Task PushToHistoryAsync(MailContent mail);
}

/// <summary>
/// WPFのUIスレッド（STA）上でクリップボードを操作する実装
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    /// <summary>履歴の順序が入れ替わらないよう、書き込みの間隔を空ける</summary>
    private static readonly TimeSpan HistoryInterval = TimeSpan.FromMilliseconds(500);

    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        TrySetText(text);
    }

    public async Task PushToHistoryAsync(MailContent mail)
    {
        // 履歴では後に入れたものが上に来るので、From が先頭に来るよう逆順で流し込む
        string[] ordered = [mail.Body, mail.Cc, mail.To, mail.From];

        bool isFirst = true;

        foreach (string value in ordered)
        {
            if (string.IsNullOrEmpty(value)) continue;

            if (!isFirst) await Task.Delay(HistoryInterval);

            TrySetText(value);
            isFirst = false;
        }
    }

    private static void TrySetText(string text)
    {
        try
        {
            Clipboard.SetText(text);

            // アプリ終了後もクリップボードに残るようにする
            Clipboard.Flush();
        }
        catch (Exception)
        {
            // 他プロセスがクリップボードをロックしている場合がある。業務を止めない
        }
    }
}
