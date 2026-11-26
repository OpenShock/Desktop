using Avalonia.Data.Converters;

namespace OpenShock.Desktop.Theming;

public static class Converters
{
    /// <summary>
    /// Upper cases text content, the way MudBlazor buttons, tabs and card titles show their text. Anything that is not
    /// a string passes through.
    /// </summary>
    public static FuncValueConverter<object?, object?> Upper { get; } =
        new(value => value is string text ? text.ToUpperInvariant() : value);
}
