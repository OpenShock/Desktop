using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenShock.Desktop.Theming;

/// <summary>The palette brushes from OpenShockTheme.axaml, for view models that pick a colour by state.</summary>
public static class ThemeBrushes
{
    public static IBrush Primary => Get("PrimaryBrush");
    public static IBrush Success => Get("SuccessBrush");
    public static IBrush Info => Get("InfoBrush");
    public static IBrush Warning => Get("WarningBrush");
    public static IBrush Error => Get("ErrorBrush");
    public static IBrush Dark => Get("DarkBrush");
    public static IBrush TextPrimary => Get("TextPrimaryBrush");

    private static IBrush Get(string key) =>
        Application.Current?.TryFindResource(key, ThemeVariant.Dark, out var value) == true && value is IBrush brush
            ? brush
            : Brushes.Gray;
}
