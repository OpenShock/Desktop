using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Logging;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Platforms.AvaloniaShell;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.Utils;
using OpenShock.Desktop.ViewModels;
using OpenShock.Desktop.ViewModels.Pages;

namespace OpenShock.Desktop.Views;

public partial class MainWindow : Window, IDialogHost
{
    private readonly BlazorServerHost _host = null!;
    private readonly OpenShockConfig _config = null!;
    private readonly ILogger<MainWindow> _logger = null!;
    private readonly ShellViewModel _viewModel = null!;
    private NativeWebView? _webView;
    private bool _hostReady;
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

        _viewModel = new ShellViewModel(host.Services, this);
        _viewModel.Sidebar.UpdateDialogRequested += ShowUpdateDialog;
        _viewModel.Navigator.PropertyChanged += OnNavigatorChanged;
        DataContext = _viewModel;

        // Errors logged anywhere show up as a message, as they did in the Blazor UI.
        UiLogSink.ErrorNotification = message => _viewModel.Notifier.Show(message, UiNotificationSeverity.Error);

        Opened += (_, _) => _viewModel.Sidebar.ShowPendingUpdate();
    }

    /// <summary>The page host is listening, so module pages can be shown. Call once.</summary>
    public void OnHostReady()
    {
        _hostReady = true;
        if (_viewModel.Navigator.IsWebViewActive) EnsureWebView();
    }

    // ===== Dialogs =====

    public async Task<bool> ConfirmAsync(ConfirmRequest request)
    {
        try
        {
            return await ConfirmDialog.ShowAsync(this, request);
        }
        catch (Exception e)
        {
            // Callers run this from commands and fire-and-forget handlers, which would swallow it.
            _logger.LogError(e, "Could not show the dialog {Title}", request.Title);
            return false;
        }
    }

    /// <summary>
    /// Opens the update dialog unless one is already up: the manual check and the UpdateAvailable subscription can
    /// both ask for the same update.
    /// </summary>
    public void ShowUpdateDialog()
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

    // ===== Webview, for module pages =====

    private void OnNavigatorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppNavigator.IsWebViewActive)) return;

        if (_viewModel.Navigator.IsWebViewActive) EnsureWebView();
        // Hidden rather than removed: keeping it means the module page is still there when you come back.
        if (_webView is not null) _webView.IsVisible = _viewModel.Navigator.IsWebViewActive;
    }

    /// <summary>
    /// Creates the webview the first time a module page is opened. Most sessions never open one, and the webview is
    /// a browser process of its own.
    /// </summary>
    private void EnsureWebView()
    {
        if (_webView is not null || !_hostReady) return;

        _webView = new NativeWebView();
        _webView.EnvironmentRequested += OnEnvironmentRequested;
        _webView.NavigationStarted += OnNavigationStarted;
        _webView.NewWindowRequested += OnNewWindowRequested;
        _webView.ProcessTerminated += OnProcessTerminated;
        PageHost.Children.Add(_webView);

        // Straight to the page that was asked for. The navigation the navigator queued on the bridge is delivered
        // once the page is up, and lands on the same page.
        _webView.Source = _host.EntryUri(_viewModel.Navigator.CurrentRoute);
    }

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
        if (_webView is not null) _webView.Source = _host.EntryUri(_viewModel.Navigator.CurrentRoute);
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
        if (await ConfirmAsync(new ConfirmRequest("Close?", "Do you want to close OpenShock?", "Yes")))
            Shell.Quit();
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel is not null) await _viewModel.DisposeAsync();
    }
}
