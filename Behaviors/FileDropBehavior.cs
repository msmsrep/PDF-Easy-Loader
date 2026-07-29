using System.Windows;
using System.Windows.Input;

namespace PDF_Easy_Loader.Behaviors;

/// <summary>
/// ドラッグ＆ドロップされたファイルのパス一覧をコマンドへ渡す添付ビヘイビア。
/// コードビハインドにロジックを置かないために使う。
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
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not DependencyObject element) return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        var command = GetCommand(element);

        if (command?.CanExecute(paths) == true)
        {
            command.Execute(paths);
        }

        e.Handled = true;
    }
}
