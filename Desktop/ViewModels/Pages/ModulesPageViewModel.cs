using System.Diagnostics;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleManager;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.Utils;
using Semver;

namespace OpenShock.Desktop.ViewModels.Pages;

public enum ModulesTab
{
    Installed,
    Unknown,
    Available
}

/// <summary>
/// Installing, updating and removing modules. A port of the Blazor ModuleManagerTab and ModuleManagerItem.
/// </summary>
public sealed partial class ModulesPageViewModel : PageViewModel
{
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly RepositoryManager _repositoryManager;

    public ModulesPageViewModel(PageContext context, bool openAvailable) : base(context)
    {
        _moduleManager = Get<ModuleManager.ModuleManager>();
        _repositoryManager = Get<RepositoryManager>();
        Tab = openAvailable ? ModulesTab.Available : ModulesTab.Installed;

        Refresh();

        // Repositories are usually still being fetched when this page opens, and the lists are built from them.
        Track(_repositoryManager.FetcherState.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_repositoryManager.FetchedRepositories.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_repositoryManager.RepositoriesStateChanged.SubscribeAsync(_ => OnUiAsync(Refresh)));
    }

    [ObservableProperty] public partial ModulesTab Tab { get; set; }
    [ObservableProperty] public partial bool IsFetching { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ModuleItemViewModel> Installed { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<ModuleItemViewModel> Unknown { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<ModuleItemViewModel> Available { get; set; } = [];

    public bool IsInstalledTab
    {
        get => Tab == ModulesTab.Installed;
        set
        {
            if (value) Tab = ModulesTab.Installed;
        }
    }

    public bool IsUnknownTab
    {
        get => Tab == ModulesTab.Unknown;
        set
        {
            if (value) Tab = ModulesTab.Unknown;
        }
    }

    public bool IsAvailableTab
    {
        get => Tab == ModulesTab.Available;
        set
        {
            if (value) Tab = ModulesTab.Available;
        }
    }

    partial void OnTabChanged(ModulesTab value)
    {
        OnPropertyChanged(nameof(IsInstalledTab));
        OnPropertyChanged(nameof(IsUnknownTab));
        OnPropertyChanged(nameof(IsAvailableTab));
        OnPropertyChanged(nameof(CurrentItems));
        OnPropertyChanged(nameof(EmptyState));
    }

    public bool HasUnknown => Unknown.Count > 0;

    public IReadOnlyList<ModuleItemViewModel> CurrentItems => Tab switch
    {
        ModulesTab.Unknown => Unknown,
        ModulesTab.Available => Available,
        _ => Installed
    };

    /// <summary>Why the current tab is empty and what to do about it, or null when it is not empty.</summary>
    public ModulesEmptyState? EmptyState => CurrentItems.Count > 0
        ? null
        : Tab switch
        {
            ModulesTab.Available when IsFetching => new ModulesEmptyState("Fetching repositories.", null, true),
            ModulesTab.Available when Installed.Count > 0 || Unknown.Count > 0 =>
                new ModulesEmptyState("Everything on offer is already installed.", null, false),
            ModulesTab.Available => new ModulesEmptyState("No modules on offer.",
                "Repositories are configured in Settings.", false, "Open settings"),
            _ => new ModulesEmptyState("No modules installed yet.", "Open the Available tab to see what you can add.",
                false)
        };

    private void Refresh()
    {
        IsFetching = _repositoryManager.FetcherState.Value != FetcherState.Idle;

        var loaded = _moduleManager.Modules;
        var fromRepositories = _repositoryManager.Repositories.Values
            .Where(x => x.Repository is not null)
            .SelectMany(x => x.Repository!.Modules)
            .GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.First().Value);

        Installed = loaded.Values.Where(x => x.RepositoryModule is not null).OrderBy(x => x.Name)
            .Select(x => Item(x.Id, x.RepositoryModule, x)).ToArray();
        Unknown = loaded.Values.Where(x => !fromRepositories.ContainsKey(x.Id)).OrderBy(x => x.Name)
            .Select(x => Item(x.Id, null, x)).ToArray();
        Available = fromRepositories.Where(x => !loaded.ContainsKey(x.Key)).OrderBy(x => x.Value.Name)
            .Select(x => Item(x.Key, x.Value, null)).ToArray();

        if (Tab == ModulesTab.Unknown && Unknown.Count == 0) Tab = ModulesTab.Installed;

        OnPropertyChanged(nameof(HasUnknown));
        OnPropertyChanged(nameof(CurrentItems));
        OnPropertyChanged(nameof(EmptyState));
    }

    private ModuleItemViewModel Item(string id, Module? repositoryModule, LoadedModule? loadedModule) =>
        new(id, repositoryModule, loadedModule, Context);

    [RelayCommand]
    private Task RefreshRepositories() => _repositoryManager.FetchRepositories();

    [RelayCommand]
    private static void OpenAppData() =>
        Process.Start(new ProcessStartInfo { FileName = Constants.AppdataFolder, UseShellExecute = true, Verb = "open" });

    [RelayCommand]
    private void OpenSettings() => Context.Navigate(Routes.Settings);
}

public sealed record ModulesEmptyState(string Message, string? Hint, bool IsLoading, string? ActionText = null)
{
    public bool HasHint => Hint is not null;
    public bool HasAction => ActionText is not null;
    public bool IsNotLoading => !IsLoading;
}

/// <summary>One module: installed, available from a repository, or both.</summary>
public sealed partial class ModuleItemViewModel : ObservableObject
{
    private readonly string _id;
    private readonly Module? _repositoryModule;
    private readonly LoadedModule? _loadedModule;
    private readonly PageContext _context;
    private readonly ConfigManager _configManager;

    public ModuleItemViewModel(string id, Module? repositoryModule, LoadedModule? loadedModule, PageContext context)
    {
        if (repositoryModule is null && loadedModule is null)
            throw new ArgumentException("A module needs to be in a repository, loaded, or both");

        _id = id;
        _repositoryModule = repositoryModule;
        _loadedModule = loadedModule;
        _context = context;
        _configManager = context.Services.GetRequiredService<ConfigManager>();

        Name = repositoryModule?.Name ?? loadedModule!.Name;
        Versions = repositoryModule?.Versions.Keys.OrderByDescending(x => x, SemVersion.PrecedenceComparer)
            .Select(v => new VersionOption(v.ToString(), new AsyncRelayCommand(() => InstallVersionAsync(v))))
            .ToArray() ?? [];

        _ = LoadIconAsync();
    }

    public string Name { get; }

    /// <summary>
    /// Only a repository carries a description. Saying so beats "unavailable", which reads like something went wrong
    /// when nothing did.
    /// </summary>
    public string Description => _repositoryModule?.Description ?? (_loadedModule is null
        ? "No description provided."
        : "Installed from outside a repository, so there is no description.");

    public string? Failure => _loadedModule?.Fault is { } fault
        ? $"This module threw while {fault.Phase.ToLowerInvariant()} was running, and is disabled for this session."
        : null;

    public bool IsFailed => Failure is not null;

    public bool IsInstalled => _loadedModule is not null;
    public string? InstalledVersion => _loadedModule?.Version.WithoutMetadata().ToString();
    public string? InstalledVersionFull => _loadedModule?.Version.ToString();

    // ===== Icon =====

    [ObservableProperty] public partial IImage? IconImage { get; set; }
    [ObservableProperty] public partial Geometry? IconGeometry { get; set; }

    private async Task LoadIconAsync()
    {
        var logger = _context.Services.GetRequiredService<ILogger<ModuleItemViewModel>>();

        if (_repositoryModule?.IconUrl is { } iconUrl)
        {
            IconImage = await RemoteImages.LoadImageAsync(iconUrl, logger);
            return;
        }

        if (_loadedModule is null) return;
        var icon = ModuleIcon.Load(_loadedModule.Ui.Icon, _context.Services.GetRequiredService<ModuleFileProvider>(),
            logger);
        IconImage = icon.Image;
        IconGeometry = icon.Geometry;
    }

    // ===== Install state =====

    private SemVersion? LatestVersion => _repositoryModule?.Versions.Keys.Where(x => x.IsRelease)
        .OrderByDescending(x => x, SemVersion.PrecedenceComparer).FirstOrDefault();

    private SemVersion? LatestPreReleaseVersion => _repositoryModule?.Versions.Keys.Where(x => !x.IsRelease)
        .OrderByDescending(x => x, SemVersion.PrecedenceComparer).FirstOrDefault();

    private InstallState State
    {
        get
        {
            // A queued install or removal only happens on the next start.
            if (_configManager.Config.Modules.ModuleTasks.ContainsKey(_id)) return InstallState.RestartRequired;
            if (_loadedModule?.AvailableVersion is not null) return InstallState.UpdateAvailable;
            if (_loadedModule is not null) return InstallState.Installed;
            if (LatestVersion is not null) return InstallState.Installable;
            if (LatestPreReleaseVersion is not null) return InstallState.PreReleaseInstallable;
            return InstallState.NoVersionAvailable;
        }
    }

    public string ActionText => State switch
    {
        InstallState.RestartRequired => "Pending Restart",
        InstallState.UpdateAvailable => $"Update to {_loadedModule!.AvailableVersion}",
        InstallState.Installed => "Up to date",
        InstallState.Installable => $"Install {LatestVersion}",
        InstallState.PreReleaseInstallable => $"Install preview {LatestPreReleaseVersion}",
        _ => "No version available"
    };

    public Geometry ActionIcon => State switch
    {
        InstallState.RestartRequired => MaterialIcons.RestartAlt,
        InstallState.UpdateAvailable => MaterialIcons.Update,
        InstallState.Installed => MaterialIcons.DesktopWindows,
        InstallState.Installable or InstallState.PreReleaseInstallable => MaterialIcons.InstallDesktop,
        _ => MaterialIcons.CodeOff
    };

    /// <summary>The colour of the button group, as MudButtonGroup's Color.</summary>
    public IBrush ActionBrush => State switch
    {
        InstallState.RestartRequired => ThemeBrushes.Info,
        InstallState.UpdateAvailable => ThemeBrushes.Tertiary,
        InstallState.Installable => ThemeBrushes.Success,
        InstallState.PreReleaseInstallable => ThemeBrushes.Warning,
        _ => ThemeBrushes.TextPrimary
    };

    public bool IsActionEnabled => State is not (InstallState.Installed or InstallState.NoVersionAvailable);

    public IReadOnlyList<VersionOption> Versions { get; }
    public bool HasVersions => Versions.Count > 0;

    public bool CanRemove => State is InstallState.Installed or InstallState.UpdateAvailable or InstallState.RestartRequired;

    [RelayCommand]
    private async Task Action()
    {
        switch (State)
        {
            case InstallState.RestartRequired:
                await _configManager.SaveNow();
                RestartHelper.RestartApp(_context.Services.GetRequiredService<IDesktopShell>());
                break;
            case InstallState.UpdateAvailable:
                await InstallVersionAsync(_loadedModule!.AvailableVersion!);
                break;
            case InstallState.Installable:
                Install(LatestVersion!);
                break;
            case InstallState.PreReleaseInstallable:
                await InstallVersionAsync(LatestPreReleaseVersion!);
                break;
        }
    }

    private async Task InstallVersionAsync(SemVersion version)
    {
        var confirmed = await _context.Dialogs.ConfirmAsync(new ConfirmRequest(
            "Install module?",
            $"Are you sure you want to install version {version} of {Name}?",
            "Install",
            Warning: "Be careful when downgrading a module's version. This might cause new data to be lost.",
            Icon: MaterialIcons.InstallDesktop));

        if (confirmed) Install(version);
    }

    private void Install(SemVersion version)
    {
        _configManager.Config.Modules.ModuleTasks[_id] = new InstallModuleTask { Version = version };
        _configManager.Save();
        StateChanged();
    }

    [RelayCommand]
    private void Remove()
    {
        _configManager.Config.Modules.ModuleTasks[_id] = new RemoveModuleTask();
        _configManager.Save();
        _context.Notify("Module will be removed upon restart", UiNotificationSeverity.Info);
        StateChanged();
    }

    private void StateChanged()
    {
        OnPropertyChanged(nameof(ActionText));
        OnPropertyChanged(nameof(ActionIcon));
        OnPropertyChanged(nameof(ActionBrush));
        OnPropertyChanged(nameof(IsActionEnabled));
        OnPropertyChanged(nameof(CanRemove));
    }

    private enum InstallState
    {
        RestartRequired,
        UpdateAvailable,
        Installed,
        Installable,
        PreReleaseInstallable,
        NoVersionAvailable
    }
}

public sealed record VersionOption(string Text, IAsyncRelayCommand Install);
