using System.IO;
using System.Text.Json;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// 画面の設定をJSONでLocalStateに保存する。
/// 秘密情報は入れないため暗号化しない（パスワードは <see cref="DpapiPasswordStore"/> 側）。
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath = Path.Combine(AppEnvironment.LocalStateFolder, FileName);

    private readonly Lock _sync = new();

    public AppSettings Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath)) return new AppSettings();

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch (Exception)
            {
                // 破損していても業務を止めない
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, JsonOptions);

                // 書き込み途中で落ちても既存データを失わないよう、一時ファイル経由で置き換える
                string tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (IOException)
            {
                // 設定が保存できないだけでアプリを落とさない
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
