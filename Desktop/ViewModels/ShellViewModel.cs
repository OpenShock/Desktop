using CommunityToolkit.Mvvm.ComponentModel;
using OpenShock.Desktop.ModuleManager;
using OpenShock.Desktop.Modules;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.ViewModels;

/// <summary>The main window: the startup splash until the app is up, then the sidebar and the page.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly PageContext _pageContext;
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly ModuleExceptionGuard _moduleGuard;

    public ShellViewModel(IServiceProvider services, IDialogHost dialogs)
    {
        var bridge = services.GetRequiredService<WebUiBridge>();
        Navigator = new AppNavigator(bridge, services.GetRequiredService<ILogger<AppNavigator>>());
        Notifier = new Notifier(Navigator, bridge);
        _moduleManager = services.GetRequiredService<ModuleManager.ModuleManager>();
        _moduleGuard = new ModuleExceptionGuard(_moduleManager, services.GetRequiredService<ILogger<ModuleExceptionGuard>>());
        _pageContext = new PageContext(services, Navigator, Notifier, dialogs, _moduleGuard);
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

    /// <summary>The native page for <paramref name="route"/>, or null for a Blazor module page (the webview).</summary>
    private PageViewModel? CreatePage(string route) => Routes.IsModulePage(Routes.PathOf(route))
        ? CreateModulePage(Routes.PathOf(route))
        : CreateBuiltInPage(route);

    private PageViewModel? CreateBuiltInPage(string route) => Routes.PathOf(route).ToLowerInvariant() switch
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

    /// <summary>
    /// <c>/dash/module/{module}</c> or <c>/dash/module/{module}/{page}</c>. Native pages, and notices for anything that
    /// cannot be shown, are native; Blazor pages return null and go to the webview.
    /// </summary>
    private PageViewModel? CreateModulePage(string path)
    {
        var parts = path[Routes.ModulePrefix.Length..].Trim('/').Split('/', 2);
        var moduleId = Uri.UnescapeDataString(parts[0]);
        var pageName = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;

        if (moduleId.Length == 0 || !_moduleManager.Modules.TryGetValue(moduleId, out var module))
            return Notice("Module not found", $"There is no module \"{moduleId}\" installed.");

        // Its services never finished registering, so its pages would only fail in a less helpful way.
        if (module.Fault is { } fault)
            return Notice($"{module.Name} failed to start",
                $"The module threw during {fault.Phase.ToLowerInvariant()} and has been disabled for this session. " +
                "Updating or reinstalling it usually fixes this.", fault.Exception.ToString());

        if (pageName is null)
        {
            if (module.Ui.RootComponent is { } root)
                return module.Ui.RootKind == ModulePageKind.Native
                    ? new ModulePageViewModel(_pageContext, module, root, module.Name)
                    : null;

            return module.Ui.NavigationItems.Length == 0
                ? Notice($"{module.Name} does not provide any pages",
                    "This module works in the background and has nothing to show here.")
                : Notice($"{module.Name} has no main page", "Pick one of its pages in the sidebar.");
        }

        var entry = module.Ui.NavigationItems.FirstOrDefault(x =>
            x.Name.Equals(pageName, StringComparison.InvariantCultureIgnoreCase));
        if (entry is null) return Notice("Page not found", $"{module.Name} has no page called \"{pageName}\".");

        return entry.Kind == ModulePageKind.Native
            ? new ModulePageViewModel(_pageContext, module, entry.ComponentType, entry.Name)
            : null;
    }

    private ModulePageViewModel Notice(string title, string message, string? details = null) =>
        ModulePageViewModel.ForNotice(_pageContext, new ModuleNotice(title, message, details));

    public override async ValueTask DisposeAsync()
    {
        Navigator.Dispose();
        _moduleGuard.Dispose();
        await Startup.DisposeAsync();
        await Sidebar.DisposeAsync();
        await base.DisposeAsync();
    }
}
