using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace OpenShock.Desktop.Controls;

/// <summary>
/// A titled card: tinted header with icon and title, an optional control on the right of the header, content beneath.
/// The native counterpart of the Blazor SectionCard, styled in Theming/Controls.axaml.
/// </summary>
public class SectionCard : HeaderedContentControl
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SectionCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<SectionCard, object?>(nameof(HeaderContent));

    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<SectionCard, bool>(nameof(IsCollapsible));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SectionCard, bool>(nameof(IsExpanded), true);

    /// <summary>Padding around the content. Zero for content that runs edge to edge, like a table.</summary>
    public static readonly StyledProperty<Thickness> ContentPaddingProperty =
        AvaloniaProperty.Register<SectionCard, Thickness>(nameof(ContentPadding), new Thickness(16, 0, 16, 4));

    /// <summary>
    /// Caps the content and scrolls it past that height, so a list that can grow without limit cannot run the page
    /// on forever.
    /// </summary>
    public static readonly StyledProperty<double> MaxContentHeightProperty =
        AvaloniaProperty.Register<SectionCard, double>(nameof(MaxContentHeight), double.PositiveInfinity);

    /// <summary>
    /// Disabled hands the card's height to the content instead of scrolling it, for content that scrolls itself
    /// below a fixed part, like a table under its header row.
    /// </summary>
    public static readonly StyledProperty<ScrollBarVisibility> ContentScrollProperty =
        AvaloniaProperty.Register<SectionCard, ScrollBarVisibility>(nameof(ContentScroll), ScrollBarVisibility.Auto);

    public ScrollBarVisibility ContentScroll
    {
        get => GetValue(ContentScrollProperty);
        set => SetValue(ContentScrollProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public bool IsCollapsible
    {
        get => GetValue(IsCollapsibleProperty);
        set => SetValue(IsCollapsibleProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public Thickness ContentPadding
    {
        get => GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public double MaxContentHeight
    {
        get => GetValue(MaxContentHeightProperty);
        set => SetValue(MaxContentHeightProperty, value);
    }

    private Control? _header;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_header is not null) _header.PointerReleased -= OnHeaderReleased;
        _header = e.NameScope.Find<Control>("PART_Header");
        if (_header is not null) _header.PointerReleased += OnHeaderReleased;
    }

    private void OnHeaderReleased(object? sender, PointerReleasedEventArgs e)
    {
        // A click on a control the caller put in the header is that control's, not a toggle of the card.
        if (!IsCollapsible || e.Handled || e.InitialPressMouseButton != MouseButton.Left) return;

        IsExpanded = !IsExpanded;
        e.Handled = true;
    }
}
