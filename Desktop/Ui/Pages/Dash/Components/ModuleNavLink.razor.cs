// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Interfaces;
using MudBlazor.Utilities;
using System.ComponentModel;
using Color = MudBlazor.Color;
using NavigationContext = MudBlazor.NavigationContext;

namespace OpenShock.Desktop.Ui.Pages.Dash.Components;
#nullable enable

/// <summary>
/// A navigation link as part of a <see cref="MudBlazor.MudNavMenu"/>.
/// </summary>
/// <seealso cref="MudBlazor.MudNavGroup"/>
/// <seealso cref="MudBlazor.MudNavMenu"/>
public partial class ModuleNavLink : MudComponentBase
{
    protected string Classname =>
        new CssBuilder("mud-nav-item")
            .AddClass(Class)
            .Build();

    protected string LinkClassname =>
        new CssBuilder("mud-nav-link")
            .AddClass($"mud-nav-link-disabled", Disabled)
            .AddClass($"mud-ripple", Ripple && !Disabled)
            .Build();

    protected Dictionary<string, object?> Attributes
    {
        get => Disabled ? new Dictionary<string, object?>() : new Dictionary<string, object?>
        {
            { "href", Href },
            { "target", Target },
            { "rel", !string.IsNullOrWhiteSpace(Target) ? "noopener noreferrer" : string.Empty }
        };
    }

    protected int TabIndex => Disabled || NavigationContext is { Disabled: true } or { Expanded: false } ? -1 : 0;

    [Inject]
    private NavigationManager UriHelper { get; set; } = null!;

    [CascadingParameter]
    private INavigationEventReceiver? NavigationEventReceiver { get; set; }

    [CascadingParameter]
    private NavigationContext? NavigationContext { get; set; }

    /// <summary>
    /// The icon displayed for this link.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>null</c>.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string? IconImage { get; set; }



    /// <summary>
    /// Controls when this link is highlighted.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="NavLinkMatch.Prefix"/>.  This link is compared against the current URL to determine whether it is highlighted.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;

    /// <summary>
    /// The browser frame to open this link when <see cref="Href"/> is specified.
    /// </summary>
    /// <remarks>
    /// Possible values include <c>_blank</c>, <c>_self</c>, <c>_parent</c>, <c>_top</c>, or a <i>frame name</i>. <br/>
    /// Defaults to <c>null</c>.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string? Target { get; set; }

    /// <summary>
    /// The CSS applied when this link is active.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>active</c>.  Multiple classes must be separated by spaces.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string ActiveClass { get; set; } = "active";

    /// <summary>
    /// Prevents the user from interacting with this link.
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool Disabled { get; set; }

    /// <summary>
    /// Shows a ripple effect when the user clicks this link.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool Ripple { get; set; } = true;

    /// <summary>
    /// The URL to navigate to when this link is clicked.
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string? Href { get; set; }

    /// <summary>
    /// Performs a full page load during navigation.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>false</c>. When <c>true</c>, client-side routing is bypassed and the browser is forced to load the new page from the server.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool ForceLoad { get; set; }

    /// <summary>
    /// The content within this link.
    /// </summary>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Occurs when this link has been clicked.
    /// </summary>
    /// <remarks>
    /// This event only occurs when the <see cref="Href"/> property has not been set.
    /// </remarks>
    [Parameter]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    protected Task HandleNavigation()
    {
        if (!Disabled && NavigationEventReceiver != null)
        {
            return NavigationEventReceiver.OnNavigation();
        }

        return Task.CompletedTask;
    }
}