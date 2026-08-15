using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// 画面の設定の読み書き
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>保存済みの設定を取得する。未保存・破損時は既定値を返す</summary>
    AppSettings Load();

    /// <summary>設定を保存する</summary>
    void Save(AppSettings settings);
}
