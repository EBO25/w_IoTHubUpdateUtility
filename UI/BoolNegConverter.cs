using System.Globalization;
using Avalonia.Data.Converters;

namespace IoTHubUpdateUtility.UI;

/// <summary>
/// Inverts a bool — used so the "folder" RadioButton binds to !IsCompressedSource.
/// </summary>
public class BoolNegConverter : IValueConverter
{
    public static readonly BoolNegConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
