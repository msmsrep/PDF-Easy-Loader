using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PDF_Easy_Loader.Converters;

/// <summary>
/// 空文字なら行ごと隠す
/// </summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
