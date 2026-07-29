using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using PDF_Easy_Loader.Models;

namespace PDF_Easy_Loader.Services;

/// <summary>
/// パスワードをDPAPI(CurrentUser)で暗号化し、MSIXコンテナ内のLocalStateに保存する。
/// ファイルを他のPCや他のユーザーへコピーしても復号できない。
/// </summary>
public sealed class DpapiPasswordStore : IPasswordStore
{
    private const string FileName = "passwords.dat";

    /// <summary>DPAPIの追加エントロピー。ファイルを盗んでも同じアプリ以外では復号しづらくする</summary>
    private static readonly byte[] Entropy =
        "PDF-Easy-Loader/passwords/v1"u8.ToArray();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly string _filePath = Path.Combine(AppEnvironment.LocalStateFolder, FileName);

    private readonly Lock _sync = new();

    public IReadOnlyList<PasswordEntry> Load()
    {
        lock (_sync)
        {
            return LoadCore();
        }
    }

    public void Save(IEnumerable<PasswordEntry> entries)
    {
        lock (_sync)
        {
            SaveCore(entries.ToList());
        }
    }

    public void TouchLastUsed(string label)
    {
        lock (_sync)
        {
            var entries = LoadCore().ToList();
            var target = entries.FirstOrDefault(e => e.Label == label);

            if (target is null) return;

            target.LastUsedAt = DateTimeOffset.Now;
            SaveCore(entries);
        }
    }

    private List<PasswordEntry> LoadCore()
    {
        if (!File.Exists(_filePath)) return [];

        try
        {
            byte[] encrypted = File.ReadAllBytes(_filePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);

            var entries = JsonSerializer.Deserialize<List<PasswordEntry>>(plain, JsonOptions) ?? [];

            // 平文をメモリに残す時間を短くするため、復号したバッファは即座に潰す
            Array.Clear(plain);

            // 直近で成功したものから試したいので、最終利用日時の降順にする
            return entries
                .OrderByDescending(e => e.LastUsedAt ?? DateTimeOffset.MinValue)
                .ToList();
        }
        catch (CryptographicException)
        {
            // 別ユーザー・別PCのファイル。読めないものは無いものとして扱う
            return [];
        }
        catch (Exception)
        {
            // 破損時も業務を止めない
            return [];
        }
    }

    private void SaveCore(List<PasswordEntry> entries)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(entries, JsonOptions);

        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

            // 書き込み途中で落ちても既存データを失わないよう、一時ファイル経由で置き換える
            string tempPath = _filePath + ".tmp";
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            Array.Clear(plain);
        }
    }
}
