using Avalonia;
using Avalonia.Controls.Primitives;

namespace OpenShock.Spike.AvaloniaModule.Controls;

public sealed class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Badge, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
