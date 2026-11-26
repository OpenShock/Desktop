using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.Utils;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>How the app starts, how it looks, and where it gets modules from. A port of the Blazor AppSettingsTab.</summary>
public sealed partial class SettingsPageViewModel : PageViewModel
{
    private readonly ConfigManager _configManager;
    private readonly Updater _updater;
    private readonly RepositoryManager _repositoryManager;
    private bool _loading = true;

    public SettingsPageViewModel(PageContext context) : base(context)
    {
        _configManager = Get<ConfigManager>();
        _updater = Get<Updater>();
        _repositoryManager = Get<RepositoryManager>();

        var config = _configManager.Config;
        UpdateChannel = config.App.UpdateChannel;
        MinimizeOnLaunch = config.App.MinimizeOnLaunch;
        CloseToTray = config.App.CloseToTray;
        StartWithWindows = config.App.OSAutoStart;
        StartWithVrChat = config.App.VRCAutoStart;
        IsMidnight = config.App.Theme == Theme.Midnight;
        AutoUpdateModules = config.Modules.AutoUpdate;
        _loading = false;

        RefreshUpdater();
        RefreshRepositories();

        Track(_updater.CheckingForUpdate.ValueUpdated.SubscribeAsync(_ => OnUiAsync(RefreshUpdater)));
        Track(_updater.UpdateAvailable.ValueUpdated.SubscribeAsync(_ => OnUiAsync(RefreshUpdater)));
        Track(_repositoryManager.FetcherState.ValueUpdated.SubscribeAsync(_ => OnUiAsync(RefreshRepositories)));
        Track(_repositoryManager.FetchedRepositories.ValueUpdated.SubscribeAsync(_ => OnUiAsync(RefreshRepositories)));
        Track(_repositoryManager.RepositoriesStateChanged.SubscribeAsync(_ => OnUiAsync(RefreshRepositories)));
    }

    public bool IsWindows => OperatingSystem.IsWindows();
    public IReadOnlyList<UpdateChannel> UpdateChannels { get; } = Enum.GetValues<UpdateChannel>();

    // ===== Application =====

    [ObservableProperty] public partial UpdateChannel UpdateChannel { get; set; }
    [ObservableProperty] public partial bool MinimizeOnLaunch { get; set; }
    [ObservableProperty] public partial bool CloseToTray { get; set; }
    [ObservableProperty] public partial bool StartWithWindows { get; set; }
    [ObservableProperty] public partial bool StartWithVrChat { get; set; }
    [ObservableProperty] public partial bool IsCheckingForUpdate { get; set; }
    [ObservableProperty] public partial bool IsUpdateAvailable { get; set; }

    public bool CanCheckForUpdate => !IsCheckingForUpdate && !IsUpdateAvailable;

    partial void OnIsCheckingForUpdateChanged(bool value) => OnPropertyChanged(nameof(CanCheckForUpdate));
    partial void OnIsUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(CanCheckForUpdate));

    private void RefreshUpdater()
    {
        IsCheckingForUpdate = _updater.CheckingForUpdate.Value;
        IsUpdateAvailable = !IsCheckingForUpdate && _updater.UpdateAvailable.Value;
    }

    partial void OnUpdateChannelChanged(UpdateChannel value)
    {
        if (_loading) return;
        _configManager.Config.App.UpdateChannel = value;
        _configManager.Save();
        _ = _updater.CheckUpdate();
    }

    partial void OnMinimizeOnLaunchChanged(bool value) => Save(c => c.App.MinimizeOnLaunch = value);
    partial void OnCloseToTrayChanged(bool value) => Save(c => c.App.CloseToTray = value);

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        Save(c => c.App.OSAutoStart = value);
        Try(() => AutoStart.SetWindows(value), "Could not change starting with Windows");
    }

    partial void OnStartWithVrChatChanged(bool value)
    {
        if (_loading) return;
        Save(c => c.App.VRCAutoStart = value);
        Try(() => AutoStart.SetVrcx(value), "Could not change starting with VRChat");
    }

    [RelayCommand]
    private Task CheckForUpdate() => _updater.CheckUpdate();

    [RelayCommand]
    private void OpenUpdateDialog() => Context.Dialogs.ShowUpdateDialog();

    // ===== Appearance =====

    [ObservableProperty] public partial bool IsMidnight { get; set; }

    partial void OnIsMidnightChanged(bool value)
    {
        if (_loading) return;
        var theme = value ? Theme.Midnight : Theme.Dark;
        Save(c => c.App.Theme = theme);
        ThemeApplier.Apply(theme);
    }

    // ===== Modules =====

    [ObservableProperty] public partial bool AutoUpdateModules { get; set; }

    partial void OnAutoUpdateModulesChanged(bool value) => Save(c => c.Modules.AutoUpdate = value);

    // ===== Repositories =====

    [ObservableProperty] public partial bool IsFetching { get; set; }
    [ObservableProperty] public partial bool IsAddOpen { get; set; }
    [ObservableProperty] public partial string NewUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial IReadOnlyList<RepositoryRow> BuiltInRepositories { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<RepositoryRow> CustomRepositories { get; set; } = [];
    [ObservableProperty] public partial bool HasCustomRepositories { get; set; }

    private void RefreshRepositories()
    {
        IsFetching = _repositoryManager.FetcherState.Value != FetcherState.Idle;

        BuiltInRepositories = Constants.BuiltInModuleRepositories
            .Select(uri => new RepositoryRow(uri, true, _repositoryManager.Repositories.GetValueOrDefault(uri),
                RemoveRepository))
            .ToArray();
        CustomRepositories = _configManager.Config.Modules.ModuleRepositories
            .Select(uri => new RepositoryRow(uri, false, _repositoryManager.Repositories.GetValueOrDefault(uri),
                RemoveRepository))
            .ToArray();
        HasCustomRepositories = CustomRepositories.Count > 0;
    }

    [RelayCommand]
    private Task Refresh() => _repositoryManager.FetchRepositories();

    [RelayCommand]
    private void OpenAdd() => IsAddOpen = true;

    [RelayCommand]
    private void CancelAdd()
    {
        IsAddOpen = false;
        NewUrl = string.Empty;
    }

    [RelayCommand]
    private void AddRepository()
    {
        if (string.IsNullOrWhiteSpace(NewUrl)) return;
        if (!Uri.TryCreate(NewUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Context.Notify("The URL is not valid", UiNotificationSeverity.Error);
            return;
        }

        if (Constants.BuiltInModuleRepositories.Contains(uri))
        {
            Context.Notify("This repository is built-in and cannot be added", UiNotificationSeverity.Error);
            return;
        }

        if (!_configManager.Config.Modules.ModuleRepositories.Contains(uri))
        {
            _configManager.Config.Modules.ModuleRepositories.Add(uri);
            _configManager.Save();
        }

        CancelAdd();
        RefreshRepositories();
    }

    private void RemoveRepository(Uri uri)
    {
        _configManager.Config.Modules.ModuleRepositories.Remove(uri);
        _configManager.Save();
        RefreshRepositories();
    }

    // ===== Advanced =====

    [RelayCommand]
    private void OpenDebug() => Context.Navigate(Routes.Debug);

    // ===== Helpers =====

    private void Save(Action<OpenShockConfig> change)
    {
        if (_loading) return;
        change(_configManager.Config);
        _configManager.Save();
    }

    private void Try(Action action, string failure)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Get<ILogger<SettingsPageViewModel>>().LogError(e, "{Failure}", failure);
            Context.Notify(failure, UiNotificationSeverity.Error);
        }
    }
}

/// <summary>A module repository and how fetching it went.</summary>
public sealed partial class RepositoryRow : ObservableObject
{
    private readonly Action<Uri> _remove;

    public RepositoryRow(Uri url, bool isBuiltIn, RepositoryLoadContext? context, Action<Uri> remove)
    {
        Url = url;
        IsBuiltIn = isBuiltIn;
        _remove = remove;

        var state = context?.State.Value;
        (StatusBrush, StatusText) = state switch
        {
            null => (ThemeBrushes.Warning, "This repository has not been fetched yet"),
            RepositoryLoadContextState.Queued => (ThemeBrushes.Info, "Queued for fetching..."),
            RepositoryLoadContextState.Loading => (ThemeBrushes.Primary, "Fetching..."),
            RepositoryLoadContextState.Loaded =>
                (ThemeBrushes.Success, $"{context!.Repository?.Modules.Count ?? 0} Module(s) fetched"),
            _ => (ThemeBrushes.Error, "There was an error fetching the repository, click for more info")
        };
        IsLoading = state == RepositoryLoadContextState.Loading;
        IsFailed = state == RepositoryLoadContextState.Failed;

        if (IsFailed && context!.LastError is { } error)
        {
            ErrorStatus = error.HttpStatusCode is { } code ? $"HTTP {(int)code} {code}" : null;
            ErrorResponse = error.Response;
            ErrorException = error.Exception?.ToString();
        }
    }

    public Uri Url { get; }
    public bool IsBuiltIn { get; }
    public IBrush StatusBrush { get; }
    public string StatusText { get; }
    public bool IsLoading { get; }
    public bool IsFailed { get; }
    public bool ShowStatusIcon => !IsLoading && !IsFailed;

    public string? ErrorStatus { get; }
    public string? ErrorResponse { get; }
    public string? ErrorException { get; }
    public bool HasNoErrorDetails => ErrorStatus is null && ErrorResponse is null && ErrorException is null;

    public string RemoveTooltip => IsBuiltIn ? "This is a built-in repository, it can't be removed" : "Remove repository";

    [RelayCommand]
    private void Remove()
    {
        if (!IsBuiltIn) _remove(Url);
    }
}
