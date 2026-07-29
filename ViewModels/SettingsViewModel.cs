using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDF_Easy_Loader.Models;
using PDF_Easy_Loader.Services;

namespace PDF_Easy_Loader.ViewModels;

/// <summary>
/// 一覧に出す1件分。パスワードそのものは画面に出さない。
/// </summary>
public sealed class PasswordItemViewModel(PasswordEntry entry)
{
    public PasswordEntry Entry { get; } = entry;

    public string Label => Entry.Label;

    public string LastUsedText => Entry.LastUsedAt is { } used
        ? used.LocalDateTime.ToString("yyyy/MM/dd HH:mm")
        : "未使用";
}

/// <summary>
/// パスワードの登録・削除画面。
/// 登録済みパスワードの表示は行わず、追加と削除のみを許可する。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IPasswordStore _store;

    public SettingsViewModel(IPasswordStore store)
    {
        _store = store;

        foreach (var entry in _store.Load())
        {
            Items.Add(new PasswordItemViewModel(entry));
        }
    }

    public ObservableCollection<PasswordItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewLabel { get; set; } = string.Empty;

    /// <summary>
    /// PasswordBox からコードビハインド経由で流し込まれる入力値。
    /// 保存後は必ず空にする。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; private set; } = string.Empty;

    /// <summary>PasswordBox をクリアするようView側へ知らせる</summary>
    public event EventHandler? PasswordCleared;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        string label = NewLabel.Trim();

        if (Items.Any(i => string.Equals(i.Label, label, StringComparison.OrdinalIgnoreCase)))
        {
            Message = "同じ識別名が既に登録されています。";
            return;
        }

        Items.Add(new PasswordItemViewModel(new PasswordEntry
        {
            Label = label,
            Password = NewPassword,
        }));

        Persist();

        NewLabel = string.Empty;
        NewPassword = string.Empty;
        PasswordCleared?.Invoke(this, EventArgs.Empty);
        Message = $"「{label}」を登録しました。";
    }

    private bool CanAdd() =>
        !string.IsNullOrWhiteSpace(NewLabel) && !string.IsNullOrEmpty(NewPassword);

    [RelayCommand]
    private void Remove(PasswordItemViewModel? item)
    {
        if (item is null) return;

        Items.Remove(item);
        Persist();
        Message = $"「{item.Label}」を削除しました。";
    }

    private void Persist() => _store.Save(Items.Select(i => i.Entry));
}
