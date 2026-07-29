using System.Diagnostics;
using System.IO;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// ドロップされたPDF1件を「復号 → 抽出 → 起動」まで通す
/// </summary>
public interface IPdfLoader
{
    Task<PdfLoadResult> LoadAsync(string sourcePath);
}

/// <summary>
/// 登録済みパスワードを順に試し、全滅したらその場で入力を求める実装
/// </summary>
public sealed class PdfLoader(
    IPdfService pdf,
    IPasswordStore passwordStore,
    ITempWorkspace workspace,
    IDialogService dialogs) : IPdfLoader
{
    public async Task<PdfLoadResult> LoadAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return Fail(sourcePath, "ファイルが見つかりません。");
        }

        // 暗号化判定とiTextの読み込みは重いのでUIスレッドから逃がす
        bool encrypted;

        try
        {
            encrypted = await Task.Run(() => pdf.IsEncrypted(sourcePath));
        }
        catch (Exception ex)
        {
            return Fail(sourcePath, $"PDFを読み込めませんでした。\n{ex.Message}");
        }

        if (!encrypted)
        {
            // 暗号化されていなければ複製せず元ファイルをそのまま開く
            var mail = await Task.Run(() => pdf.ExtractMail(sourcePath));

            Open(sourcePath);

            return new PdfLoadResult
            {
                Status = PdfLoadStatus.NotEncrypted,
                SourcePath = sourcePath,
                OpenedPath = sourcePath,
                Mail = mail,
            };
        }

        return await DecryptAndOpenAsync(sourcePath);
    }

    private async Task<PdfLoadResult> DecryptAndOpenAsync(string sourcePath)
    {
        string workDir = workspace.CreateWorkDirectory();
        string fileName = Path.GetFileName(sourcePath);
        string output = Path.Combine(workDir, fileName);

        // 1. 登録済みパスワードを直近で使った順に試す
        var entries = passwordStore.Load();

        foreach (var entry in entries)
        {
            var attempt = await TryAsync(sourcePath, output, entry.Password);

            if (attempt.Error is not null) return Fail(sourcePath, attempt.Error);

            if (!attempt.Success) continue;

            passwordStore.TouchLastUsed(entry.Label);

            return await FinishAsync(sourcePath, output, PdfLoadStatus.Decrypted, entry.Label);
        }

        // 2. 全滅したらその場で入力してもらう（保存はしない）
        string? typed = dialogs.AskPassword(fileName);

        if (string.IsNullOrEmpty(typed))
        {
            return new PdfLoadResult
            {
                Status = PdfLoadStatus.PasswordUnknown,
                SourcePath = sourcePath,
                ErrorMessage = entries.Count == 0
                    ? "パスワードが登録されていません。設定画面から登録してください。"
                    : "登録済みパスワードでは開けませんでした。",
            };
        }

        var manual = await TryAsync(sourcePath, output, typed);

        if (manual.Error is not null) return Fail(sourcePath, manual.Error);

        if (!manual.Success) return Fail(sourcePath, "パスワードが正しくありません。");

        return await FinishAsync(sourcePath, output, PdfLoadStatus.DecryptedWithInput, null);
    }

    private Task<(bool Success, string? Error)> TryAsync(string input, string output, string password) =>
        Task.Run(() =>
        {
            bool ok = pdf.TryDecrypt(input, output, password, out string? error);
            return (ok, error);
        });

    /// <summary>
    /// 復号後のファイルからメールを抽出し、件名をファイル名に付けてから開く。
    /// 対象の暗号化PDFはファイル名が分かりにくいため、件名を足して見分けやすくする。
    /// </summary>
    private async Task<PdfLoadResult> FinishAsync(
        string sourcePath,
        string decryptedPath,
        PdfLoadStatus status,
        string? matchedLabel)
    {
        var mail = await Task.Run(() => pdf.ExtractMail(decryptedPath));

        string openPath = RenameWithSubject(decryptedPath, mail.Subject);

        Open(openPath);

        return new PdfLoadResult
        {
            Status = status,
            SourcePath = sourcePath,
            OpenedPath = openPath,
            MatchedLabel = matchedLabel,
            Mail = mail,
        };
    }

    private static string RenameWithSubject(string path, string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return path;

        string safeSubject = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));

        if (string.IsNullOrWhiteSpace(safeSubject)) return path;

        // 長い件名でパス長制限に当たらないよう切り詰める
        if (safeSubject.Length > 80) safeSubject = safeSubject[..80];

        string directory = Path.GetDirectoryName(path)!;
        string renamed = Path.Combine(directory, $"{safeSubject}_{Path.GetFileName(path)}");

        try
        {
            File.Move(path, renamed);
            return renamed;
        }
        catch (Exception)
        {
            // 名前が付けられなくても開けるようにする
            return path;
        }
    }

    private static void Open(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    private static PdfLoadResult Fail(string sourcePath, string message) => new()
    {
        Status = PdfLoadStatus.Failed,
        SourcePath = sourcePath,
        ErrorMessage = message,
    };
}
