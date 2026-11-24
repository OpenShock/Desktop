using Avalonia.Controls;
using Avalonia.Platform;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Platforms.AvaloniaShell;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Utils;
using OpenShock.Desktop.ViewModels;

namespace OpenShock.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly BlazorServerHost _host = null!;
    private readonly OpenShockConfig _config = null!;
    private readonly ILogger<MainWindow> _logger = null!;
    private readonly NativeWebView _webView = null!;
    private readonly ShellViewModel _viewModel = null!;
    private UpdateDialog? _updateDialog;

    /// <summary>Set when the app is really quitting, so closing is no longer intercepted.</summary>
    public bool AllowClose { get; set; }

    /// <summary>For the XAML previewer only.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(BlazorServerHost host, OpenShockConfig config) : this()
    {
        _host = host;
        _config = config;
        _logger = host.Services.GetRequiredService<ILogger<MainWindow>>();

        Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "wwwroot", "images",
            OperatingSystem.IsWindows() ? "openshock-icon.ico" : "Icon512.png"));

        _viewModel = new ShellViewModel(host.Services);
        _viewModel.Sidebar.UpdateDialogRequested += ShowUpdateDialog;
        DataContext = _viewModel;

        _webView = new NativeWebView();
        _webView.EnvironmentRequested += OnEnvironmentRequested;
        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.ProcessTerminated += OnProcessTerminated;
        PageHost.Children.Add(_webView);

        Opened += (_, _) => _viewModel.Sidebar.ShowPendingUpdate();
    }

    /// <summary>Points the webview at the UI. Call once the host is listening.</summary>
    public void Load() => _webView.Source = _host.EntryUri();

    // ===== Update dialog =====

    /// <summary>
    /// Opens the update dialog unless one is already up: the manual check and the UpdateAvailable subscription can
    /// both ask for the same update.
    /// </summary>
    private void ShowUpdateDialog()
    {
        if (_updateDialog is not null)
        {
            _updateDialog.Activate();
            return;
        }

        var services = _host.Services;
        _updateDialog = new UpdateDialog(new UpdateDialogViewModel(services.GetRequiredService<Updater>(),
            services.GetRequiredService<ConfigManager>()));
        _updateDialog.Closed += (_, _) => _updateDialog = null;

        if (IsVisible) _updateDialog.Show(this);
        else _updateDialog.Show();
    }

    // ===== Webview =====

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

    // ===== Closing =====

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

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel is not null) await _viewModel.DisposeAsync();
    }
}
