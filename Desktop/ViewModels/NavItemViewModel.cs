using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Theming;

namespace OpenShock.Desktop.ViewModels;

/// <summary>
/// A sidebar entry: a link to a route, or a group (a module with several pages) that expands to show its links.
/// </summary>
public sealed partial class NavItemViewModel : ObservableObject
{
    private readonly Action<string> _navigate;

    public NavItemViewModel(string title, string? route, ModuleIcon icon, Action<string> navigate,
        bool exactMatch = false, IEnumerable<NavItemViewModel>? children = null)
    {
        Title = title;
        Route = route;
        Icon = icon;
        ExactMatch = exactMatch;
        _navigate = navigate;
        Children = new ObservableCollection<NavItemViewModel>(children ?? []);
    }

    public string Title { get; }

    /// <summary>Where the link goes, null for a group.</summary>
    public string? Route { get; }

    public ModuleIcon Icon { get; }
    public Geometry? IconGeometry => Icon.Geometry;
    public IImage? IconImage => Icon.Image;
    public bool HasIconImage => Icon.Image is not null;

    /// <summary>Only active on exactly <see cref="Route"/>, not below it (the dashboard).</summary>
    public bool ExactMatch { get; }

    public ObservableCollection<NavItemViewModel> Children { get; }
    public bool IsGroup => Children.Count > 0;

    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }

    [RelayCommand]
    private void Activate()
    {
        if (IsGroup) IsExpanded = !IsExpanded;
        else if (Route is not null) _navigate(Route);
    }

    public void UpdateActive(string currentPath)
    {
        IsActive = Route is not null && Routes.Matches(currentPath, Route, ExactMatch);

        foreach (var child in Children) child.UpdateActive(currentPath);

        // Open the group whose page we are on, the way MudNavGroup does when you land on one of its links.
        if (Children.Any(c => c.IsActive)) IsExpanded = true;
    }
}
