using Avalonia;
using Avalonia.Controls;

namespace OpenShock.Desktop.Controls;

/// <summary>
/// Label and optional description on the left, whatever the content is on the right. The native counterpart of the
/// Blazor LabeledRow, styled in Theming/Controls.axaml.
/// </summary>
public class LabeledRow : ContentControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LabeledRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LabeledRow, string?>(nameof(Description));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
