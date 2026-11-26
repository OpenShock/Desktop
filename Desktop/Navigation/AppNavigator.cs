using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.Navigation;

/// <summary>
/// Which page the window shows. Built-in pages are native views; module pages are the Blazor UI in the webview, which
/// is navigated through the <see cref="WebUiBridge"/>.
/// </summary>
public sealed partial class AppNavigator : ObservableObject, IDisposable
{
    private readonly WebUiBridge _bridge;
    private readonly ILogger _logger;
    private Func<string, PageViewModel?> _createPage = _ => null;
    private string? _currentRoute;

    public AppNavigator(WebUiBridge bridge, ILogger logger)
    {
        _bridge = bridge;
        _logger = logger;
        _bridge.LocationChanged += OnWebViewLocationChanged;
    }

    /// <summary>The native page on screen, null while a module page is (<see cref="IsWebViewActive"/>).</summary>
    [ObservableProperty] public partial PageViewModel? CurrentPage { get; private set; }

    [ObservableProperty] public partial bool IsWebViewActive { get; private set; }

    /// <summary>The route that was last navigated to, as given (with its query, if any).</summary>
    public string CurrentRoute => _currentRoute ?? "/";

    /// <summary>The path of the page on screen, see <see cref="Routes.PathOf"/>.</summary>
    public string CurrentPath { get; private set; } = "/";

    /// <summary>Raised on the UI thread with the new <see cref="CurrentPath"/>.</summary>
    public event Action<string>? Navigated;

    /// <summary>
    /// Builds the native page for a path, or returns null when there is none. Set once by the shell, which owns
    /// everything the pages need.
    /// </summary>
    public void SetPageFactory(Func<string, PageViewModel?> createPage) => _createPage = createPage;

    /// <summary>Shows <paramref name="route"/>. Safe to call from any thread.</summary>
    public void Navigate(string route)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Navigate(route));
            return;
        }

        var path = Routes.PathOf(route);
        if (path == "/")
        {
            route = Routes.Dashboard;
            path = route;
        }

        if (route == _currentRoute) return;

        PageViewModel? page = null;
        if (!Routes.IsModulePage(path))
        {
            page = CreatePage(route);
            if (page is null)
            {
                _logger.LogWarning("No page for {Route}, showing the dashboard", route);
                route = Routes.Dashboard;
                path = route;
                page = CreatePage(route);
            }
        }

        // The route first: the window reads it when the webview flips on below, to load the webview at it.
        _currentRoute = route;
        CurrentPath = path;

        ShowPage(page);
        IsWebViewActive = page is null;
        if (page is null) _bridge.Navigate(route);

        Navigated?.Invoke(path);
    }

    private PageViewModel? CreatePage(string route)
    {
        try
        {
            return _createPage(route);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to build the page for {Route}", route);
            return null;
        }
    }

    private void ShowPage(PageViewModel? page)
    {
        var previous = CurrentPage;
        CurrentPage = page;
        if (previous is not null) _ = previous.DisposeAsync().AsTask();
    }

    /// <summary>
    /// The webview moved on its own: a link in a module page, or a module calling NavigationManager. Module pages
    /// are followed; a built-in route is handed to the native page.
    /// </summary>
    private void OnWebViewLocationChanged(string path) => Dispatcher.UIThread.Post(() =>
    {
        if (!IsWebViewActive) return;

        if (Routes.IsModulePage(path))
        {
            if (path == CurrentPath) return;
            _currentRoute = path;
            CurrentPath = path;
            Navigated?.Invoke(path);
            return;
        }

        // "/" is where the webview starts out before the first navigation reaches it, not a request for a page.
        if (path != "/") Navigate(path);
    });

    public void Dispose()
    {
        _bridge.LocationChanged -= OnWebViewLocationChanged;
        ShowPage(null);
    }
}
