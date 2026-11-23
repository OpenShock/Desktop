using System.Globalization;
using Avalonia.Data.Converters;
using Humanizer;

namespace OpenShock.Spike.Helpers;

/// <summary>
/// Used from the module's XAML, so the XAML has to resolve a type that lives in a module-private
/// assembly, which in turn needs another module-private package (Humanizer).
/// </summary>
public sealed class HumanizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? "clicked " + i.ToWords() + " times" : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
