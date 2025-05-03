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
    private NotifyIcon _tray;
    private ContextMenuStrip _menu;
    private ToolStripLabel? _stateLabel;

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

        _tray = new NotifyIcon();
        _tray.Icon = Icon.ExtractAssociatedIcon(@"wwwroot/images/openshock-icon.ico");
        _tray.Text = "OpenShock";

        _menu = new ContextMenuStrip();

        _menu.Items.Add("OpenShock", Image.FromFile(@"wwwroot/images/openshock-icon.ico"), OnMainClick);
        _menu.Items.Add(new ToolStripSeparator());
        _stateLabel = new ToolStripLabel($"State: {_apiHubClient.State}");
        _menu.Items.Add(_stateLabel);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Quit OpenShock", null, OnQuitClick);

        _tray.ContextMenuStrip = _menu;

        _tray.Click += OnMainClick;

        _tray.Visible = true;
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

        foreach (var subscription in _subscriptions)
        {
            await subscription.DisposeAsync();
        }
        
        _tray.Dispose();
        _menu.Dispose();
        _stateLabel?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}

#endif