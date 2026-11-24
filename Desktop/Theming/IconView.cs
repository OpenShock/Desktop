using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace OpenShock.Desktop.Theming;

/// <summary>
/// Draws a Material icon geometry the way MudBlazor's MudIcon does: on a 24x24 grid, scaled to the control's size,
/// filled with the current foreground. Unlike PathIcon it does not stretch the path to its own bounds, so a small
/// glyph (a dot) stays small.
/// </summary>
public sealed class IconView : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<IconView, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<IconView>();

    static IconView()
    {
        AffectsRender<IconView>(DataProperty, ForegroundProperty);
        WidthProperty.OverrideDefaultValue<IconView>(24);
        HeightProperty.OverrideDefaultValue<IconView>(24);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null) return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24d;
        var offsetX = (Bounds.Width - 24d * scale) / 2;
        var offsetY = (Bounds.Height - 24d * scale) / 2;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(Foreground, null, Data);
        }
    }
}
