using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OpenShock.Desktop.Controls;

/// <summary>
/// The icon in front of a button's text, as MudButton's StartIcon. Read by the button templates in
/// Theming/Controls.axaml.
/// </summary>
public static class ButtonAssist
{
    public static readonly AttachedProperty<Geometry?> IconProperty =
        AvaloniaProperty.RegisterAttached<Button, Geometry?>("Icon", typeof(ButtonAssist));

    public static Geometry? GetIcon(Button button) => button.GetValue(IconProperty);

    public static void SetIcon(Button button, Geometry? value) => button.SetValue(IconProperty, value);
}
