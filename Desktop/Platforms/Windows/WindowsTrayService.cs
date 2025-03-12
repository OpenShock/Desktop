#if WINDOWS

using System.Drawing;
using System.Windows.Forms;
using OpenShock.Desktop.Services;
using OpenShock.SDK.CSharp.Hub;
using Application = Microsoft.Maui.Controls.Application;
using Image = System.Drawing.Image;

// ReSharper disable once CheckNamespace
namespace OpenShock.Desktop.Platforms.Windows;

public class WindowsTrayService : ITrayService, IAsyncDisposable
{
    private readonly OpenShockHubClient _apiHubClient;
    private readonly List<IAsyncDisposable> _subscriptions = new();

    /// <summary>
    /// Windows Tray Service
    /// </summary>
    /// <param name="apiHubClient"></param>
    public WindowsTrayService(OpenShockHubClient apiHubClient)
    {
        _apiHubClient = apiHubClient;
    }

    private ToolStripLabel? _stateLabel;

    private Task HubStateChanged()
    {
        if (_stateLabel == null) return Task.CompletedTask;
        _stateLabel.Text = $"State: {_apiHubClient.State}";
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

        var tray = new NotifyIcon();
        tray.Icon = Icon.ExtractAssociatedIcon(@"Resources\openshock-icon.ico");
        tray.Text = "OpenShock";

        var menu = new ContextMenuStrip();

        menu.Items.Add("OpenShock", Image.FromFile(@"Resources\openshock-icon.ico"), OnMainClick);
        menu.Items.Add(new ToolStripSeparator());
        _stateLabel = new ToolStripLabel($"State: {_apiHubClient.State}");
        menu.Items.Add(_stateLabel);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit OpenShock", null, OnQuitClick);

        tray.ContextMenuStrip = menu;

        tray.Click += OnMainClick;

        tray.Visible = true;
    }

    private static void OnMainClick(object? sender, EventArgs eventArgs)
    {
        if (eventArgs is MouseEventArgs mouseEventArgs && mouseEventArgs.Button != MouseButtons.Left) return;

        var window = Application.Current?.Windows[0];
        var nativeWindow = window?.Handler?.PlatformView;
        if (nativeWindow == null) return;

        var appWindow = WindowUtils.GetAppWindow(nativeWindow);

        appWindow.ShowOnTop();
    }

    private static void OnQuitClick(object? sender, EventArgs eventArgs)
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

        _stateLabel?.Dispose();

        foreach (var subscription in _subscriptions)
        {
            await subscription.DisposeAsync();
        }
    }
}

#endif