using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using MudBlazor.Utilities;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Ui.Utils;
using OpenShock.Desktop.Utils;

namespace OpenShock.Desktop.Platforms.AvaloniaShell;

/// <summary>
/// The app window: for now a single <see cref="NativeWebView"/> showing the Blazor UI from
/// <see cref="BlazorServerHost"/>. Native pages move in here as they get ported.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly BlazorServerHost _host;
    private readonly OpenShockConfig _config;
    private readonly ILogger<MainWindow> _logger;
    private readonly NativeWebView _webView;

    /// <summary>Set when the app is really quitting, so closing is no longer intercepted.</summary>
    public bool AllowClose { get; set; }

    public MainWindow(BlazorServerHost host, OpenShockConfig config)
    {
        _host = host;
        _config = config;
        _logger = host.Services.GetRequiredService<ILogger<MainWindow>>();

        Title = "OpenShock";
        Width = 1280;
        Height = 800;
        MinWidth = 1000;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "wwwroot", "images",
            OperatingSystem.IsWindows() ? "openshock-icon.ico" : "Icon512.png"));

        // Same colour as the UI, so there is no white flash before the first paint.
        var theme = host.Services.GetRequiredService<ThemeDefinition>().GetCurrentTheme();
        Background = new SolidColorBrush(Color.Parse(theme.PaletteDark.Background.ToString(MudColorOutputFormats.Hex)));

        _webView = new NativeWebView();
        _webView.EnvironmentRequested += OnEnvironmentRequested;
        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.ProcessTerminated += OnProcessTerminated;
        Content = _webView;
    }

    /// <summary>Points the webview at the UI. Call once the host is listening.</summary>
    public void Load() => _webView.Source = _host.EntryUri();

    private void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e)
    {
#if DEBUG
        e.EnableDevTools = true;
#endif

        // The default is a folder next to the exe, which is read-only when installed under Program Files.
        if (e is WindowsWebView2EnvironmentRequestedEventArgs webView2)
            webView2.UserDataFolder = Path.Combine(Constants.AppdataFolder, "webview");
    }

    /// <summary>Links to the outside world open in the user's browser, never inside the app.</summary>
    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is not { } uri || !IsWebUri(uri) || _host.IsOwnOrigin(uri)) return;

        e.Cancel = true;
        UiUtils.OpenUrl(uri.ToString());
    }

    private void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Request is { } uri && IsWebUri(uri)) UiUtils.OpenUrl(uri.ToString());
    }

    private void OnProcessTerminated(object? sender, WebViewProcessTerminatedEventArgs e)
    {
        _logger.LogError("The webview process terminated ({Reason}), reloading", e.Reason ?? "no reason given");
        Load();
    }

    private static bool IsWebUri(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (AllowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;

        if (_config.App.CloseToTray)
        {
            Hide();
            return;
        }

        _ = ConfirmQuitAsync();
    }

    private async Task ConfirmQuitAsync()
    {
        if (await ConfirmDialog.ShowAsync(this, "Close?", "Do you want to close OpenShock?", "Yes", "Cancel"))
            Shell.Quit();
    }
}
