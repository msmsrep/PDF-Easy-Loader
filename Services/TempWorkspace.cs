using System.IO;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// 復号したPDFを置く一時フォルダを管理する
/// </summary>
public interface ITempWorkspace
{
    /// <summary>PDF1件分の作業フォルダを作って返す</summary>
    string CreateWorkDirectory();

    /// <summary>一時フォルダを丸ごと削除する</summary>
    void Cleanup();
}

/// <summary>
/// MSIXコンテナ内の一時フォルダを使う実装。
/// 復号済みPDFが残り続けないよう、起動時と終了時に全削除する。
/// </summary>
public sealed class TempWorkspace : ITempWorkspace
{
    private const string WorkFolderName = "decrypted";

    private readonly string _root = Path.Combine(AppEnvironment.TempRootFolder, WorkFolderName);

    public string CreateWorkDirectory()
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public void Cleanup()
    {
        if (!Directory.Exists(_root)) return;

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // PDFビューアが掴んだままでも業務を止めない。次回起動時に消える
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}
