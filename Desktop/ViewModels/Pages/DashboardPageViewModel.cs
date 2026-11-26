using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LucHeart.WebsocketLibrary;
using Microsoft.AspNetCore.SignalR.Client;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.Desktop.ModuleManager;
using OpenShock.Desktop.Navigation;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Theming;
using OpenShock.Desktop.Ui;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.Desktop.ViewModels.Pages;

/// <summary>
/// The landing page: how things stand, the hubs, the modules, and where to get help. A port of the Blazor DashboardTab.
/// </summary>
public sealed partial class DashboardPageViewModel : PageViewModel
{
    private readonly OpenShockApi _api;
    private readonly OpenShockHubClient _hubClient;
    private readonly LiveControlManager _liveControlManager;
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly ConfigManager _configManager;
    private readonly AuthService _authService;

    // Latency changes several times a second and is only on this page, so poll it instead of subscribing to every
    // client.
    private readonly DispatcherTimer _latencyRefresh;

    public DashboardPageViewModel(PageContext context) : base(context)
    {
        _api = Get<OpenShockApi>();
        _hubClient = Get<OpenShockHubClient>();
        _liveControlManager = Get<LiveControlManager>();
        _moduleManager = Get<ModuleManager.ModuleManager>();
        _configManager = Get<ConfigManager>();
        _authService = Get<AuthService>();

        Discord = new DiscordInviteViewModel("OpenShock", Get<ILogger<DiscordInviteViewModel>>());
        ShowShockOscNotice = !_configManager.Config.App.ShockOscNoticeDismissed;

        Refresh();
        _latencyRefresh = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => RefreshHubs());
        _latencyRefresh.Start();

        Track(Get<StatusHandler>().OnWebsocketStatusChanged.SubscribeAsync(() => OnUiAsync(Refresh)));
        // Connections open lazily and are pruned when idle, so GetClient flips with nothing else changing.
        Track(_liveControlManager.OnStateUpdated.SubscribeAsync(() => OnUiAsync(Refresh)));
        Track(_api.Hubs.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_api.SharedOwners.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_authService.AuthState.ValueUpdated.SubscribeAsync(_ => OnUiAsync(Refresh)));
        Track(_moduleManager.ModulesLoaded.Subscribe(() => OnUi(Refresh)));
    }

    public DiscordInviteViewModel Discord { get; }

    public string VersionText => $"Version {Constants.VersionWithoutMetadata}";

    // ===== Headline =====

    [ObservableProperty] public partial string Headline { get; set; } = string.Empty;
    [ObservableProperty] public partial string Detail { get; set; } = string.Empty;
    [ObservableProperty] public partial IBrush BeaconBrush { get; set; } = ThemeBrushes.Error;
    [ObservableProperty] public partial bool IsSettling { get; set; }
    [ObservableProperty] public partial bool ShowLogin { get; set; }

    // ===== Cards =====

    [ObservableProperty] public partial bool ShowShockOscNotice { get; set; }
    [ObservableProperty] public partial string HubCountLabel { get; set; } = "none";
    [ObservableProperty] public partial bool HasNoHubs { get; set; }
    [ObservableProperty] public partial IReadOnlyList<DashboardModuleRow> Modules { get; set; } = [];
    [ObservableProperty] public partial bool HasNoModules { get; set; }

    public ObservableCollection<HubStatusRow> Hubs { get; } = [];

    private IEnumerable<IOpenShockHub> AllHubs => _api.Hubs.Value.Concat(_api.SharedOwners.Value.SelectMany(x => x.Hubs));

    private void Refresh()
    {
        RefreshHeadline();
        RefreshHubs();

        Modules = _moduleManager.Modules.Values.OrderBy(x => x.Name).Select(DashboardModuleRow.From).ToArray();
        HasNoModules = Modules.Count == 0;
    }

    /// <summary>
    /// The one line the page exists for. Backend first: without it nothing else on the page is trustworthy, so it
    /// outranks whatever the hubs last reported.
    /// </summary>
    private void RefreshHeadline()
    {
        var hubs = AllHubs.ToArray();
        var online = hubs.Count(x => x.Status.Online);
        var live = hubs.Count(x =>
            _liveControlManager.GetClient(x.Id)?.State.Value == WebsocketConnectionState.Connected);

        var settling = _hubClient.State is HubConnectionState.Connecting or HubConnectionState.Reconnecting;
        var connected = _hubClient.State == HubConnectionState.Connected;
        var notLoggedIn = _authService.AuthState.Value == AuthStateType.NotAuthed;

        IsSettling = settling;
        ShowLogin = notLoggedIn;
        HubCountLabel = hubs.Length == 0 ? "none" : $"{online} of {hubs.Length} online";

        (Headline, Detail, BeaconBrush) = 0 switch
        {
            _ when notLoggedIn => ("Not logged in", "Log in to OpenShock to see your hubs and shockers.",
                ThemeBrushes.Warning),
            _ when settling => ("Reconnecting", "Trying to reach the OpenShock backend.", ThemeBrushes.Warning),
            _ when !connected => ("Not connected",
                "Can't reach the OpenShock backend. Check your internet connection.", ThemeBrushes.Error),
            _ when hubs.Length == 0 => ("Connected", "No hubs on your account yet.", ThemeBrushes.Info),
            _ when online == 0 => ("No hubs online", "Connected to the backend, but no hub is reachable right now.",
                ThemeBrushes.Warning),
            _ => (online == hubs.Length ? "All hubs online" : $"{online} of {hubs.Length} hubs online",
                live == 0
                    ? "Live control opens as soon as something sends a command."
                    : $"Live control open on {live} of {online}.",
                ThemeBrushes.Success)
        };
    }

    private void RefreshHubs()
    {
        var rows = _api.Hubs.Value.Select(hub => HubStatusRow.From(hub, null, _liveControlManager))
            .Concat(_api.SharedOwners.Value.SelectMany(owner =>
                owner.Hubs.Select(hub => HubStatusRow.From(hub, owner.Name, _liveControlManager))))
            .ToArray();

        HasNoHubs = rows.Length == 0;

        // Update in place where the shape is unchanged, so a latency tick does not rebuild the list.
        if (rows.Length == Hubs.Count)
        {
            for (var i = 0; i < rows.Length; i++)
                if (Hubs[i] != rows[i]) Hubs[i] = rows[i];
            return;
        }

        Hubs.Clear();
        foreach (var row in rows) Hubs.Add(row);
    }

    // ===== Commands =====

    [RelayCommand]
    private void DismissShockOscNotice()
    {
        ShowShockOscNotice = false;
        _configManager.Config.App.ShockOscNoticeDismissed = true;
        _configManager.Save();
    }

    [RelayCommand]
    private void BrowseModules() => Context.Navigate(Routes.ModulesAvailable);

    [RelayCommand]
    private void ManageModules() => Context.Navigate(Routes.Modules);

    [RelayCommand]
    private void Login() => Context.Navigate(Routes.Login);

    [RelayCommand]
    private void OpenOpenShock() => OpenUrl("https://openshock.org");

    [RelayCommand]
    private void OpenGithub() => OpenUrl("https://github.com/OpenShock/Desktop");

    private void OpenUrl(string url)
    {
        UiUtils.OpenUrl(url);
        Context.Notify("Opened URL in browser", UiNotificationSeverity.Info);
    }

    public override ValueTask DisposeAsync()
    {
        _latencyRefresh.Stop();
        return base.DisposeAsync();
    }
}

/// <summary>One hub, spelled out. The sidebar has the compact form of this.</summary>
public sealed record HubStatusRow(
    string Name,
    string? SharedBy,
    IBrush DotBrush,
    string? Gateway,
    string ShockerLabel,
    string StateLabel,
    string LatencyText)
{
    public bool IsShared => SharedBy is not null;
    public string SharedByText => $"Shared by {SharedBy}";

    public static HubStatusRow From(IOpenShockHub hub, string? sharedBy, LiveControlManager liveControlManager)
    {
        var client = liveControlManager.GetClient(hub.Id);

        IBrush dot;
        string state;
        if (client is null)
        {
            dot = hub.Status.Online ? ThemeBrushes.Info : ThemeBrushes.Dark;
            state = hub.Status.Online ? "idle" : "offline";
        }
        else
        {
            (dot, state) = client.State.Value switch
            {
                WebsocketConnectionState.Connected => (ThemeBrushes.Success, "live"),
                WebsocketConnectionState.Connecting => (ThemeBrushes.Warning, "connecting"),
                WebsocketConnectionState.WaitingForReconnect => (ThemeBrushes.Warning, "reconnecting"),
                _ => (ThemeBrushes.Error, "offline")
            };
        }

        var latency = client is { State.Value: WebsocketConnectionState.Connected } ? $"{client.Latency.Value}ms" : "";
        var shockers = hub.Shockers.Count == 1 ? "1 shocker" : $"{hub.Shockers.Count} shockers";

        return new HubStatusRow(hub.Name.Truncate(28), sharedBy?.Truncate(20), dot,
            client?.Gateway?.GetGatewayDisplayUrl(), shockers, state, latency);
    }
}

public sealed record DashboardModuleRow(string Name, string VersionText, string? Failure, string? UpdateText)
{
    public bool IsFailed => Failure is not null;
    public bool HasUpdate => !IsFailed && UpdateText is not null;
    public bool IsUpToDate => !IsFailed && UpdateText is null;

    public static DashboardModuleRow From(LoadedModule module) => new(
        module.Name,
        $"Version {module.Version}",
        module.Fault is null
            ? null
            : $"Threw during {module.Fault.Phase.ToLowerInvariant()}. Open the module for details.",
        module.AvailableVersion is null ? null : $"{module.AvailableVersion} available");
}
