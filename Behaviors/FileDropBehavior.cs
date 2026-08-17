using System.Windows;
using System.Windows.Input;
using PDF_Easy_Loader.Services;

namespace PDF_Easy_Loader.Behaviors;

/// <summary>
/// ドラッグ＆ドロップされたデータをコマンドへ渡す添付ビヘイビア。
/// コードビハインドにロジックを置かないために使う。
/// Outlookの添付のようにパスを持たないデータもあるため、
/// パスへの変換はコマンド側（AttachmentExtractor）に任せる。
/// </summary>
public static class FileDropBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(FileDropBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        element.PreviewDragOver -= OnDragOver;
        element.Drop -= OnDrop;

        if (e.NewValue is null) return;

        element.AllowDrop = true;
        element.PreviewDragOver += OnDragOver;
        element.Drop += OnDrop;
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = AttachmentExtractor.HasFiles(e.Data)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not DependencyObject element) return;

        var command = GetCommand(element);

        // ドロップ元が持つデータは、この場を離れると無効になることがある。
        // 取り出しはコマンドの同期部分で済ませる
        if (command?.CanExecute(e.Data) == true)
        {
            command.Execute(e.Data);
        }

        e.Handled = true;
    }
}
