#if WINDOWS

using OpenShock.Desktop.Services;
using OpenShock.SDK.CSharp.Hub;
using Application = Microsoft.Maui.Controls.Application;

// ReSharper disable once CheckNamespace
namespace OpenShock.Desktop.Platforms.Windows;

public class WindowsTrayService : ITrayService, IAsyncDisposable
{
    private readonly OpenShockHubClient _apiHubClient;
    private readonly List<IAsyncDisposable> _subscriptions = new();
    private TrayIcon? _tray;

    /// <summary>
    /// Windows Tray Service
    /// </summary>
    /// <param name="apiHubClient"></param>
    public WindowsTrayService(OpenShockHubClient apiHubClient)
    {
        _apiHubClient = apiHubClient;
    }


    private Task HubStateChanged()
    {
        if (_tray == null) return Task.CompletedTask;
        _tray.MenuItems = BuildMenu();
        return Task.CompletedTask;
    }

    public async Task Initialize()
    {
        _subscriptions.Add(await _apiHubClient.OnReconnecting.SubscribeAsync(_ => HubStateChanged())
            .ConfigureAwait(false));
        _subscriptions.Add(await _apiHubClient.OnReconnected.SubscribeAsync(_ => HubStateChanged())
            .ConfigureAwait(false));
        _subscriptions.Add(await _apiHubClient.OnClosed.SubscribeAsync(_ => HubStateChanged())
            .ConfigureAwait(false));
        _subscriptions.Add(await _apiHubClient.OnConnected.SubscribeAsync(_ => HubStateChanged())
            .ConfigureAwait(false));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "openshock-icon.ico");

        _tray = new TrayIcon("OpenShock", iconPath, ShowMainWindow)
        {
            MenuItems = BuildMenu()
        };
    }

    private TrayIcon.MenuItem[] BuildMenu() =>
    [
        new("OpenShock", ShowMainWindow),
        TrayIcon.MenuItem.Separator,
        new($"State: {_apiHubClient.State}"),
        TrayIcon.MenuItem.Separator,
        new("Quit OpenShock", Quit)
    ];

    private static void ShowMainWindow()
    {
        var window = Application.Current?.Windows[0];
        var nativeWindow = window?.Handler?.PlatformView;
        if (nativeWindow == null) return;

        var appWindow = WindowUtils.GetAppWindow(nativeWindow);

        appWindow.ShowOnTop();
    }

    private static void Quit()
    {
        if (Application.Current != null)
        {
            Application.Current.Quit();
            return;
        }

        Environment.Exit(0);
    }

    private bool _disposed;


    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var subscription in _subscriptions)
        {
            await subscription.DisposeAsync();
        }

        _tray?.Dispose();

        GC.SuppressFinalize(this);
    }
}

#endif
