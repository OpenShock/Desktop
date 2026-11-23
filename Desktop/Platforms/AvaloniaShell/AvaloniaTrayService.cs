#if AVALONIA
using Avalonia.Controls;
using Avalonia.Threading;
using OpenShock.Desktop.Services;
using OpenShock.SDK.CSharp.Hub;
using AvaloniaApplication = Avalonia.Application;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>
/// Tray icon through Avalonia, so every platform gets one (the MAUI build used a Win32-only one).
/// </summary>
public sealed class AvaloniaTrayService : ITrayService, IAsyncDisposable
{
    private readonly OpenShockHubClient _apiHubClient;
    private readonly List<IAsyncDisposable> _subscriptions = [];
    private TrayIcon? _tray;

    public AvaloniaTrayService(OpenShockHubClient apiHubClient)
    {
        _apiHubClient = apiHubClient;
    }

    public async Task Initialize()
    {
        _subscriptions.Add(await _apiHubClient.OnReconnecting.SubscribeAsync(_ => HubStateChanged()).ConfigureAwait(false));
        _subscriptions.Add(await _apiHubClient.OnReconnected.SubscribeAsync(_ => HubStateChanged()).ConfigureAwait(false));
        _subscriptions.Add(await _apiHubClient.OnClosed.SubscribeAsync(_ => HubStateChanged()).ConfigureAwait(false));
        _subscriptions.Add(await _apiHubClient.OnConnected.SubscribeAsync(_ => HubStateChanged()).ConfigureAwait(false));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "images",
                OperatingSystem.IsWindows() ? "openshock-icon.ico" : "Icon512.png");

            _tray = new TrayIcon
            {
                Icon = new WindowIcon(iconPath),
                ToolTipText = "OpenShock",
                Menu = BuildMenu(),
                IsVisible = true
            };
            _tray.Clicked += (_, _) => Shell.ShowMainWindow();

            TrayIcon.SetIcons(AvaloniaApplication.Current!, [_tray]);
        });
    }

    private Task HubStateChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_tray is not null) _tray.Menu = BuildMenu();
        });
        return Task.CompletedTask;
    }

    private NativeMenu BuildMenu()
    {
        var open = new NativeMenuItem("OpenShock");
        open.Click += (_, _) => Shell.ShowMainWindow();

        var quit = new NativeMenuItem("Quit OpenShock");
        quit.Click += (_, _) => Shell.Quit();

        return
        [
            open,
            new NativeMenuItemSeparator(),
            new NativeMenuItem($"State: {_apiHubClient.State}") { IsEnabled = false },
            new NativeMenuItemSeparator(),
            quit
        ];
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var subscription in _subscriptions) await subscription.DisposeAsync();

        if (_tray is not null)
        {
            var tray = _tray;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                tray.IsVisible = false;
                tray.Dispose();
            });
        }
    }
}
#endif
