using CommunityToolkit.Mvvm.ComponentModel;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.ViewModels;

/// <summary>The main window: the startup splash until the app is up, then the sidebar and the page.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly PageContext _pageContext;

    public ShellViewModel(IServiceProvider services, IDialogHost dialogs)
    {
        var bridge = services.GetRequiredService<WebUiBridge>();
        Navigator = new AppNavigator(bridge, services.GetRequiredService<ILogger<AppNavigator>>());
        Notifier = new Notifier(Navigator, bridge);
        _pageContext = new PageContext(services, Navigator, Notifier, dialogs);
        Navigator.SetPageFactory(CreatePage);

        Startup = new StartupViewModel(services.GetRequiredService<StartupService>());
        Sidebar = new SidebarViewModel(services, Navigator, Notifier);

        Startup.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StartupViewModel.IsStarted)) OnStartupChanged();
        };
        OnStartupChanged();
    }

    public AppNavigator Navigator { get; }
    public Notifier Notifier { get; }
    public StartupViewModel Startup { get; }
    public SidebarViewModel Sidebar { get; }

    [ObservableProperty] public partial bool IsStarted { get; set; }

    private void OnStartupChanged()
    {
        IsStarted = Startup.IsStarted;

        // Pages only exist once the app is up: until then the modules, the backend and the config they read from
        // are still being set up.
        if (IsStarted && Navigator.CurrentPage is null && !Navigator.IsWebViewActive) Navigator.Navigate(Routes.Dashboard);
    }

    private PageViewModel? CreatePage(string route) => Routes.PathOf(route).ToLowerInvariant() switch
    {
        Routes.Dashboard => new DashboardPageViewModel(_pageContext),
        Routes.Shockers => new ShockersPageViewModel(_pageContext),
        Routes.Modules => new ModulesPageViewModel(_pageContext,
            // OpenAvailableModules is what the Blazor dashboard used, kept for modules that link here.
            openAvailable: Routes.QueryValue(route, "tab") == "available" ||
                           Routes.QueryValue(route, "OpenAvailableModules") == "true"),
        Routes.Settings => new SettingsPageViewModel(_pageContext),
        Routes.Logs => new LogsPageViewModel(_pageContext),
        Routes.Debug => new DebugPageViewModel(_pageContext),
        Routes.Login => new LoginPageViewModel(_pageContext),
        _ => null
    };

    public override async ValueTask DisposeAsync()
    {
        Navigator.Dispose();
        await Startup.DisposeAsync();
        await Sidebar.DisposeAsync();
        await base.DisposeAsync();
    }
}
