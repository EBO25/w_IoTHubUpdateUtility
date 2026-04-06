using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using IoTHubUpdateUtility.Models;

namespace IoTHubUpdateUtility.UI;

/// <summary>
/// Converts SourceType enum to bool for RadioButton binding.
/// </summary>
public class SourceTypeToBoolConverter : IValueConverter
{
    public static readonly SourceTypeToBoolConverter CompressedFile = new() { TargetType = SourceType.CompressedFile };
    public static readonly SourceTypeToBoolConverter Folder = new() { TargetType = SourceType.Folder };

    public SourceType TargetType { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SourceType st && st == TargetType;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? TargetType : BindingOperations.DoNothing;
}