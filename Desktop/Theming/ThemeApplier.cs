using Avalonia;
using Avalonia.Media;
using OpenShock.Desktop.Config;

namespace OpenShock.Desktop.Theming;

/// <summary>
/// Switches between the app themes. They only differ in background and surface (as in the Blazor ThemeDefinition), and
/// everything uses those as DynamicResource, so this applies live.
/// </summary>
public static class ThemeApplier
{
    public static void Apply(Theme theme)
    {
        if (Application.Current is not { } app) return;

        if (theme == Theme.Midnight)
        {
            app.Resources["BackgroundBrush"] = new SolidColorBrush(Color.Parse("#020202"));
            app.Resources["SurfaceBrush"] = new SolidColorBrush(Color.Parse("#050505"));
        }
        else
        {
            // Falls back to the values in OpenShockTheme.axaml.
            app.Resources.Remove("BackgroundBrush");
            app.Resources.Remove("SurfaceBrush");
        }
    }
}
