namespace OpenShock.Desktop.ModuleBase.Navigation;

/// <summary>A page of a module, with its entry in the sidebar.</summary>
public sealed class NavigationItem
{
    /// <summary>The sidebar label, also the page's address. Unique within the module.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The page: an Avalonia control (shown natively) or a Blazor component (legacy, shown in a webview). See the
    /// README for how native pages are built and hosted.
    /// </summary>
    public required Type ComponentType { get; set; }

    /// <summary>
    /// The sidebar icon: an SVG string (a MudBlazor icon, for example), an embedded resource path, or an
    /// <c>avares://</c> URI.
    /// </summary>
    public IconOneOf? Icon { get; set; } = null;
}