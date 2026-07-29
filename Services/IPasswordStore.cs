using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// 登録済みパスワードの読み書き
/// </summary>
public interface IPasswordStore
{
    /// <summary>登録済みパスワードを、直近で使った順に取得する</summary>
    IReadOnlyList<PasswordEntry> Load();

    /// <summary>登録済みパスワードを丸ごと保存する</summary>
    void Save(IEnumerable<PasswordEntry> entries);

    /// <summary>指定ラベルのエントリの最終利用日時を更新する</summary>
    void TouchLastUsed(string label);
}
