using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.SignalR.Client;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Models;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleManager;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Ui;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.ViewModels;

/// <summary>
/// The sidebar: navigation, the installed modules, connection status and the account bar. A port of the Blazor
/// SideBar, HubsPart and UpdateLogout components.
/// </summary>
public sealed partial class SidebarViewModel : ViewModelBase
{
    public const string DashboardRoute = Routes.Dashboard;
    public const string ShockersRoute = Routes.Shockers;
    public const string ModulesRoute = Routes.Modules;
    public const string SettingsRoute = Routes.Settings;
    public const string LogsRoute = Routes.Logs;

    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly AuthService _authService;
    private readonly OpenShockApi _api;
    private readonly OpenShockHubClient _hubClient;
    private readonly LiveControlManager _liveControlManager;
    private readonly Updater _updater;
    private readonly ModuleFileProvider _moduleFiles;
    private readonly AppNavigator _navigator;
    private readonly Notifier _notifier;
    private readonly ILogger<SidebarViewModel> _logger;

    public SidebarViewModel(IServiceProvider services, AppNavigator navigator, Notifier notifier)
    {
        _navigator = navigator;
        _notifier = notifier;
        _moduleManager = services.GetRequiredService<ModuleManager.ModuleManager>();
        _authService = services.GetRequiredService<AuthService>();
        _api = services.GetRequiredService<OpenShockApi>();
        _hubClient = services.GetRequiredService<OpenShockHubClient>();
        _liveControlManager = services.GetRequiredService<LiveControlManager>();
        _updater = services.GetRequiredService<Updater>();
        _moduleFiles = services.GetRequiredService<ModuleFileProvider>();
        _logger = services.GetRequiredService<ILogger<SidebarViewModel>>();

        MainItems =
        [
            new NavItemViewModel("Dashboard", DashboardRoute, ModuleIcon.FromGeometry(MaterialIcons.SpaceDashboard),
                Navigate, exactMatch: true),
            new NavItemViewModel("Shockers", ShockersRoute, ModuleIcon.FromGeometry(MaterialIcons.ElectricBolt), Navigate),
            new NavItemViewModel("Modules", ModulesRoute, ModuleIcon.FromGeometry(MaterialIcons.ViewModule), Navigate)
        ];

        OwnHubs = new HubsIndicatorViewModel("Hubs", "Your hubs", MaterialIcons.Router,
            // The user's own hubs need no attribution, so they go in one unnamed group.
            () => [new HubGroup(null, _api.Hubs.Value)], _liveControlManager);
        SharedHubs = new HubsIndicatorViewModel("Shared", "Shared hubs", MaterialIcons.Group,
            () => _api.SharedOwners.Value.Select(x => new HubGroup(x.Name, x.Hubs)).ToArray(), _liveControlManager);

        RebuildModuleItems();
        Refresh();
        UpdateActive(_navigator.CurrentPath);

        var statusHandler = services.GetRequiredService<StatusHandler>();
        Track(statusHandler.OnWebsocketStatusChanged.SubscribeAsync(() => OnUiAsync(Refresh)));
        Track(_liveControlManager.OnStateUpdated.SubscribeAsync(() => OnUiAsync(Refresh)));
        Track(_api.Hubs.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_api.SharedOwners.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_authService.AuthState.ValueUpdated.SubscribeAsync(HandleAuthStateChanged));
        Track(_updater.CheckingForUpdate.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_updater.UpdateAvailable.ValueUpdated.SubscribeAsync(OnUpdateAvailableChanged));
        Track(_moduleManager.ModulesLoaded.Subscribe(() => OnUi(() =>
        {
            RebuildModuleItems();
            Refresh();
        })));

        _navigator.Navigated += UpdateActive;
    }

    // ===== Navigation =====

    public ObservableCollection<NavItemViewModel> MainItems { get; }
    public ObservableCollection<NavItemViewModel> ModuleItems { get; } = [];

    [ObservableProperty] public partial bool HasModules { get; set; }
    [ObservableProperty] public partial bool IsSettingsActive { get; set; }
    [ObservableProperty] public partial bool IsLogsActive { get; set; }

    [RelayCommand]
    private void Navigate(string route) => _navigator.Navigate(route);

    private void UpdateActive(string path)
    {
        foreach (var item in MainItems) item.UpdateActive(path);
        foreach (var item in ModuleItems) item.UpdateActive(path);

        IsSettingsActive = Routes.Matches(path, SettingsRoute);
        IsLogsActive = Routes.Matches(path, LogsRoute);
    }

    private void RebuildModuleItems()
    {
        ModuleItems.Clear();

        foreach (var module in _moduleManager.Modules.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                ModuleItems.Add(CreateModuleItem(module));
            }
            catch (Exception e)
            {
                // Same containment the Blazor sidebar had: one bad module costs its own entry, not the sidebar.
                _logger.LogError(e, "Could not build the navigation entry for module {ModuleId}", module.Id);
                ModuleItems.Add(new NavItemViewModel(module.Name, ModuleRoute(module.Id),
                    ModuleIcon.FromGeometry(MaterialIcons.ExtensionOff), Navigate));
            }
        }

        HasModules = ModuleItems.Count > 0;
        UpdateActive(_navigator.CurrentPath);
    }

    /// <summary>Everything here comes from <see cref="LoadedModule.Ui"/>, validated at load time.</summary>
    private NavItemViewModel CreateModuleItem(LoadedModule module)
    {
        if (module.Fault is not null)
            return new NavItemViewModel(module.Name, ModuleRoute(module.Id),
                ModuleIcon.FromGeometry(MaterialIcons.ExtensionOff), Navigate);

        var icon = ModuleIcon.Load(module.Ui.Icon, _moduleFiles, _logger);

        if (module.Ui.RootComponent is not null || module.Ui.NavigationItems.Length == 0)
            return new NavItemViewModel(module.Name, ModuleRoute(module.Id), icon, Navigate);

        var children = module.Ui.NavigationItems.Select(item => new NavItemViewModel(item.Name,
            ModuleRoute(module.Id, item.Name), ModuleIcon.Load(item.Icon, _moduleFiles, _logger), Navigate));

        return new NavItemViewModel(module.Name, null, icon, Navigate, children: children);
    }

    private static string ModuleRoute(string moduleId, string? page = null) => Routes.Module(moduleId, page);

    // ===== Status =====

    public HubsIndicatorViewModel OwnHubs { get; }
    public HubsIndicatorViewModel SharedHubs { get; }

    [ObservableProperty] public partial IBrush BackendBrush { get; set; } = ThemeBrushes.Error;
    [ObservableProperty] public partial string BackendLabel { get; set; } = "offline";
    [ObservableProperty] public partial bool MissingPermissions { get; set; }
    [ObservableProperty] public partial IReadOnlyList<PermissionEntry> Permissions { get; set; } = [];

    // ===== Account =====

    [ObservableProperty] public partial AuthStateType AuthState { get; set; }
    [ObservableProperty] public partial string AccountTooltip { get; set; } = "Not Logged In";
    [ObservableProperty] public partial string? AccountName { get; set; }
    [ObservableProperty] public partial string? FailureMessage { get; set; }
    [ObservableProperty] public partial Bitmap? Avatar { get; set; }
    [ObservableProperty] public partial bool IsCheckingForUpdate { get; set; }

    public bool IsAuthed => AuthState == AuthStateType.Authed;
    public bool IsNotAuthedOrFailed => AuthState != AuthStateType.Authed;
    public bool IsNotAuthed => AuthState == AuthStateType.NotAuthed;
    public bool IsAuthFailed => AuthState == AuthStateType.FailedAuth;
    public bool IsAuthenticating => AuthState == AuthStateType.Authenticating;
    public bool ShowFailure => IsAuthFailed && FailureMessage is not null;
    public bool CanCheckForUpdate => !IsCheckingForUpdate;
    public string CheckForUpdateText => IsCheckingForUpdate ? "Checking for Updates..." : "Check for Updates";
    public string VersionText => $"v{Constants.Version}";

    partial void OnAuthStateChanged(AuthStateType value)
    {
        OnPropertyChanged(nameof(IsAuthed));
        OnPropertyChanged(nameof(IsNotAuthedOrFailed));
        OnPropertyChanged(nameof(IsNotAuthed));
        OnPropertyChanged(nameof(IsAuthFailed));
        OnPropertyChanged(nameof(IsAuthenticating));
        OnPropertyChanged(nameof(ShowFailure));
    }

    partial void OnFailureMessageChanged(string? value) => OnPropertyChanged(nameof(ShowFailure));

    partial void OnIsCheckingForUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckForUpdate));
        OnPropertyChanged(nameof(CheckForUpdateText));
    }

    /// <summary>Raised when the update dialog should open. The window owns the dialog.</summary>
    public event Action? UpdateDialogRequested;

    /// <summary>
    /// A failed login used to be a red icon and nothing else, which said nothing about whether the token expired,
    /// the backend was down, or this build is too old to read what it sent back.
    /// </summary>
    private Task HandleAuthStateChanged(AuthStateType state) => OnUiAsync(() =>
    {
        if (state == AuthStateType.FailedAuth && _authService.FailureMessage is { } failureMessage)
            _notifier.Show(failureMessage, UiNotificationSeverity.Error);

        Refresh();
    });

    private Task OnUpdateAvailableChanged(bool available) => OnUiAsync(() =>
    {
        Refresh();
        if (available && !_updater.IsPostponed) UpdateDialogRequested?.Invoke();
    });

    /// <summary>Called once the window is up, for an update that was found before the sidebar existed.</summary>
    public void ShowPendingUpdate()
    {
        if (_updater.UpdateAvailable.Value && !_updater.IsPostponed) UpdateDialogRequested?.Invoke();
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        switch (await _updater.CheckUpdate())
        {
            // Also covers an update that was already known, where the value never changes and the subscription
            // never fires.
            case Updater.UpdateCheckResult.UpdateAvailable:
                UpdateDialogRequested?.Invoke();
                break;
            case Updater.UpdateCheckResult.UpToDate:
                _notifier.Show($"You are running the latest version ({Constants.Version})", UiNotificationSeverity.Success);
                break;
            case Updater.UpdateCheckResult.Failed:
                _notifier.Show("Could not check for updates, please try again later", UiNotificationSeverity.Warning);
                break;
        }
    }

    [RelayCommand]
    private Task RetryLogin() => _authService.Authenticate();

    [RelayCommand]
    private async Task Logout()
    {
        await _authService.Logout();
        _navigator.Navigate(Routes.Dashboard);
    }

    [RelayCommand]
    private void Login() => _navigator.Navigate(Routes.Login);

    [RelayCommand]
    private void OpenDebug() => _navigator.Navigate(Routes.Debug);

    // ===== Refresh =====

    private Uri? _avatarUri;

    private void Refresh()
    {
        (BackendBrush, BackendLabel) = _hubClient.State switch
        {
            HubConnectionState.Connected => (ThemeBrushes.Success, "connected"),
            HubConnectionState.Reconnecting => (ThemeBrushes.Warning, "reconnecting"),
            HubConnectionState.Connecting => (ThemeBrushes.Warning, "connecting"),
            _ => (ThemeBrushes.Error, "offline")
        };

        OwnHubs.Refresh();
        SharedHubs.Refresh();

        MissingPermissions = _authService.MissingPermissions;
        Permissions = Constants.BasePermissions.Concat(_moduleManager.RequiredPermissions).Distinct()
            .Select(x => new PermissionEntry(PermissionTypeBindings.PermissionTypeToName[x].Name,
                !(_authService.TokenSelf?.Permissions.Contains(x) ?? false)))
            .ToArray();

        AuthState = _authService.AuthState.Value;
        AccountName = _authService.SelfResponse?.Name;
        FailureMessage = _authService.FailureMessage;
        AccountTooltip = AuthState switch
        {
            AuthStateType.Authed => AccountName ?? "Account",
            AuthStateType.Authenticating => "Logging in...",
            AuthStateType.FailedAuth => FailureMessage ?? "Login Failed",
            _ => "Not Logged In"
        };
        IsCheckingForUpdate = _updater.CheckingForUpdate.Value;

        _ = UpdateAvatarAsync(_authService.SelfResponse?.Image);
    }

    private async Task UpdateAvatarAsync(Uri? uri)
    {
        if (uri == _avatarUri) return;
        _avatarUri = uri;

        var image = uri is null ? null : await RemoteImages.LoadAsync(uri, _logger);
        if (uri == _avatarUri) Avatar = image;
    }

    public override ValueTask DisposeAsync()
    {
        _navigator.Navigated -= UpdateActive;
        return base.DisposeAsync();
    }
}

/// <summary>A permission the token needs, and whether it is missing.</summary>
public sealed record PermissionEntry(string Name, bool Missing)
{
    public IBrush Brush => Missing ? ThemeBrushes.Warning : ThemeBrushes.TextPrimary;
}
