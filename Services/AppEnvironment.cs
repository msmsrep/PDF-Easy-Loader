using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// MSIXパッケージとして動いているかを判定し、保存先フォルダを決める。
/// パッケージ実行時はコンテナ内（%LOCALAPPDATA%\Packages\&lt;PFN&gt;\...）へ、
/// F5デバッグなどの非パッケージ実行時は %LOCALAPPDATA%\PDF-Easy-Loader へ振り分ける。
/// </summary>
public static class AppEnvironment
{
    private const int AppModelErrorNoPackage = 15700;

    private const string UnpackagedFolderName = "PDF-Easy-Loader";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    /// <summary>MSIXパッケージとして起動されているか</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    /// <summary>
    /// パスワードを保存するフォルダ。
    /// パッケージ時はアンインストールで一緒に消えるコンテナ内の LocalState。
    /// </summary>
    public static string LocalStateFolder { get; } = ResolveLocalStateFolder();

    /// <summary>
    /// 復号したPDFを置く一時フォルダの親。
    /// パッケージ時はコンテナ内の TempState。
    /// </summary>
    public static string TempRootFolder { get; } = ResolveTempRootFolder();

    private static bool DetectPackaged()
    {
        try
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }
        catch (Exception)
        {
            // GetCurrentPackageFullName が無いOS = パッケージではない
            return false;
        }
    }

    private static string ResolveLocalStateFolder()
    {
        string path = IsPackaged
            ? Windows.Storage.ApplicationData.Current.LocalFolder.Path
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                UnpackagedFolderName);

        Directory.CreateDirectory(path);
        return path;
    }

    private static string ResolveTempRootFolder()
    {
        string path = IsPackaged
            ? Windows.Storage.ApplicationData.Current.TemporaryFolder.Path
            : Path.Combine(Path.GetTempPath(), UnpackagedFolderName);

        Directory.CreateDirectory(path);
        return path;
    }
}
