using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.State;
using MudBlazor.Utilities;
using Color = Microsoft.Maui.Graphics.Color;

namespace OpenShock.Desktop.Ui.Pages.Dash.Components;

/// <summary>
    /// A deeper level of navigation links as part of a <see cref="MudBlazor.MudNavMenu"/>.
    /// </summary>
    /// <seealso cref="MudBlazor.MudNavLink"/>
    /// <seealso cref="MudBlazor.MudNavMenu"/>
    public partial class ModuleNavGroup : MudComponentBase
    {
        private readonly ParameterState<bool> _expandedState;
        private readonly ParameterState<bool> _disabledState;
        private readonly ParameterState<NavigationContext?> _parentNavigationContextState;
        private NavigationContext _navigationContext = new(false, true);

        public ModuleNavGroup()
        {
            using var registerScope = CreateRegisterScope();
            _disabledState = registerScope.RegisterParameter<bool>(nameof(Disabled))
                .WithParameter(() => Disabled)
                .WithChangeHandler(UpdateNavigationContext);
            _parentNavigationContextState = registerScope.RegisterParameter<NavigationContext?>(nameof(ParentNavigationContext))
                .WithParameter(() => ParentNavigationContext)
                .WithChangeHandler(UpdateNavigationContext);
            _expandedState = registerScope.RegisterParameter<bool>(nameof(Expanded))
                .WithParameter(() => Expanded)
                .WithEventCallback(() => ExpandedChanged)
                .WithChangeHandler(UpdateNavigationContext);
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            UpdateNavigationContext();
        }

        protected string Classname =>
            new CssBuilder("mud-nav-group")
                .AddClass(Class)
                .AddClass("mud-nav-group-disabled", _disabledState.Value)
                .Build();

        protected string ButtonClassname =>
            new CssBuilder("mud-nav-link")
                .AddClass($"mud-ripple", Ripple)
                .AddClass("mud-expanded", _expandedState.Value)
                .AddClass(HeaderClass)
                .Build();
        
        protected string ExpandIconClassname =>
            new CssBuilder("mud-nav-link-expand-icon")
                .AddClass("mud-transform", _expandedState.Value && _disabledState.Value is false)
                .AddClass("mud-transform-disabled", _expandedState.Value && _disabledState.Value)
                .Build();

        protected int ButtonTabIndex => _disabledState.Value || _parentNavigationContextState.Value is { Disabled: true } or { Expanded: false } ? -1 : 0;

        [CascadingParameter]
        private NavigationContext? ParentNavigationContext { get; set; }

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
        /// The CSS classes applied to this nav group title.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  You can use spaces to separate multiple classes.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Appearance)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? HeaderClass { get; set; }
        
        /// <summary>
        /// The text shown for this group.
        /// </summary>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Behavior)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string? Title { get; set; }
        
        /// <summary>
        /// Prevents the user from interacting with this group.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Behavior)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool Disabled { get; set; }

        /// <summary>
        /// Shows a ripple effect when the user clicks this group.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>true</c>.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Appearance)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool Ripple { get; set; } = true;

        /// <summary>
        /// Displays the items within this group.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.  When this value changes, <see cref="ExpandedChanged"/> occurs.  Can be bound via <c>@bind-Expanded</c>.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Behavior)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool Expanded { get; set; }

        /// <summary>
        /// Hides the expand/collapse icon.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Appearance)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool HideExpandIcon { get; set; }

        /// <summary>
        /// The maximum height, in pixels, of this group.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  When set, it will override the CSS default.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Appearance)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int? MaxHeight { get; set; }

        /// <summary>
        /// The icon for expanding and collapsing this group.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="Icons.Material.Filled.ArrowDropDown"/>.  Only shows when <see cref="HideExpandIcon"/> is <c>false</c>.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Appearance)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string ExpandIcon { get; set; } = Icons.Material.Filled.ArrowDropDown;

        /// <summary>
        /// The content within this group.
        /// </summary>
        /// <remarks>
        /// Typically contains <see cref="MudNavGroup"/> and <see cref="MudNavLink"/> components.
        /// </remarks>
        [Parameter]
        [MudBlazor.Category(CategoryTypes.NavMenu.Behavior)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RenderFragment? ChildContent { get; set; }

        /// <summary>
        /// Occurs when <see cref="Expanded"/> has changed.
        /// </summary>
        [Parameter]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public EventCallback<bool> ExpandedChanged { get; set; }

        private async Task ExpandedToggleAsync()
        {
            await _expandedState.SetValueAsync(!_expandedState.Value);
            UpdateNavigationContext();
        }

        private void UpdateNavigationContext()
            => _navigationContext = _navigationContext with
            {
                Disabled = _disabledState.Value || _parentNavigationContextState.Value is { Disabled: true },
                Expanded = _expandedState.Value
                           && _parentNavigationContextState.Value is null or { Expanded: true }
            };
    }