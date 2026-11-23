using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Services.Pipes;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>
/// The window and lifetime of the Avalonia shell, for the parts of the app that need to show the window or
/// quit (tray, second-instance forwarding, restart). Safe to call from any thread.
/// </summary>
public static class Shell
{
    private static IClassicDesktopStyleApplicationLifetime? _lifetime;
    private static MainWindow? _mainWindow;

    internal static async Task StartAsync(IClassicDesktopStyleApplicationLifetime lifetime, BlazorServerHost host)
    {
        _lifetime = lifetime;

        var config = host.Services.GetRequiredService<ConfigManager>().Config;
        _mainWindow = new MainWindow(host, config);

        // Show straight away, so there is something on screen while the host boots.
        if (!config.App.MinimizeOnLaunch) _mainWindow.Show();

        host.Services.StartOpenShockDesktopServices(false);
        await host.StartAsync();

        // A second launch forwards here over the pipe instead of starting another instance.
        await host.Services.GetRequiredService<PipeServerService>().OnMessageReceived
            .SubscribeAsync(_ =>
            {
                ShowMainWindow();
                return Task.CompletedTask;
            });

        _mainWindow.Load();
    }

    public static void ShowMainWindow() => Dispatcher.UIThread.Post(() =>
    {
        if (_mainWindow is null) return;

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();

        // Activate alone is not enough to come to the front when another app has focus.
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    });

    public static void Quit() => Dispatcher.UIThread.Post(() =>
    {
        if (_mainWindow is not null) _mainWindow.AllowClose = true;
        _lifetime?.Shutdown();
    });
}
