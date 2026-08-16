using System.Reactive.Subjects;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Models.BaseImpl;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Hub.Models;
using OpenShock.SDK.CSharp.Models;
using Serilog;

namespace OpenShock.Desktop.Backend;

public sealed class BackendHubManager
{
    private readonly ILogger<BackendHubManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly OpenShockHubClient _openShockHubClient;
    private readonly OpenShockApi _openShockApi;

    private string _currentHubConnectionId = string.Empty;

    public IAsyncMinimalEventObservable<ShockerLogEventArgs> OnShockerLog => _onShockerLog;
    private readonly AsyncMinimalEvent<ShockerLogEventArgs> _onShockerLog = new();
    
    public Subject<Guid?> OnHubStatusUpdated { get; } = new();

    public BackendHubManager(ILogger<BackendHubManager> logger,
        ConfigManager configManager,
        OpenShockHubClient openShockHubClient, OpenShockApi openShockApi)
    {
        _logger = logger;
        _configManager = configManager;
        _openShockHubClient = openShockHubClient;
        _openShockApi = openShockApi;
        
        _openShockHubClient.OnWelcome.SubscribeAsync(Welcome).AsTask().Wait();;
        _openShockHubClient.OnLog.SubscribeAsync(OnShockerLogHandler).AsTask().Wait();
        _openShockHubClient.OnHubUpdate.SubscribeAsync(DeviceUpdate).AsTask().Wait();

        _openShockHubClient.OnHubStatus.SubscribeAsync(HubStatus).AsTask().Wait();
    }

    private Task Welcome(string connectionId)
    {
        _currentHubConnectionId = connectionId;
        _openShockApi.HubStates.Clear();
        
        OnHubStatusUpdated.OnNext(null);
        return Task.CompletedTask;
    }

    private Task HubStatus(IReadOnlyList<HubOnlineState> states)
    {
        _logger.LogDebug("Hub status update received {Count} hubs", states.Count);
        
        foreach (var state in states)
        {
            _openShockApi.HubStates[state.Device] = new HubStatus
            {
                Online = state.Online
            };
            _logger.LogDebug("Hub {HubId} is now {Online}", state.Device, state.Online ? "online" : "offline");
        }
        
        OnHubStatusUpdated.OnNext(null);
        return Task.CompletedTask;
    }

    private async Task DeviceUpdate(HubUpdateEventArgs update)
    {
        _logger.LogDebug("Device update received {DeviceId} {UpdateType}", update.HubId, update.UpdateType);

        try
        {
            // Not filtered to known hubs: an update for an unknown one is a hub someone has just shared with us.
            await _openShockApi.RefreshAllHubs();
        }
        catch (HubRefreshException e)
        {
            _logger.LogWarning(e, "Failed to refresh hubs after a device update, keeping the previous hub data");
        }
    }


    public async Task SetupLiveClient()
    {
        await _openShockHubClient.Setup(new HubClientOptions
        {
            Token = _configManager.Config.OpenShock.Token,
            Server = _configManager.Config.OpenShock.Backend,
            ConfigureLogging = builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddSerilog();
            }
        });
    }

    private Task OnShockerLogHandler(LogEventArgs logEventArgs)
    {
        var eventArgs = new ShockerLogEventArgs
        {
            LogEventArgs = logEventArgs,
            IsRemote = logEventArgs.Sender.ConnectionId != _currentHubConnectionId
        };

        return _onShockerLog.InvokeAsyncParallel(eventArgs);
    }

    /// <summary>
    /// Control command via signalr
    /// </summary>
    /// <param name="shocks"></param>
    /// <param name="customName"></param>
    /// <returns></returns>
    public Task Control(IEnumerable<Control> shocks, string? customName = null)
    {
        var shocksToSend = shocks.Where(x => CanControl(x.Id, x.Type));
        return _openShockHubClient.Control(shocksToSend, customName);
    }

    /// <summary>
    /// Whether the given shocker may be controlled with the given control type: it must be enabled in config, exist and
    /// not be paused, and for shared shockers we must hold the permission matching the control type.
    /// </summary>
    private bool CanControl(Guid shockerId, ControlType type)
    {
        var resolution = _openShockApi.ResolveShocker(shockerId);

        if (!resolution.Enabled)
            return Drop(shockerId, type, "shocker is not enabled");

        if (resolution.Location is not { } location)
            return Drop(shockerId, type, "shocker is not on any known hub");

        if (location.Shocker.IsPaused)
            return Drop(shockerId, type, "shocker is paused");

        if (resolution.SharedPermissions is not { } permissions) return true;

        var permitted = type switch
        {
            // Stop can only ever end an action, so it needs no grant of its own.
            ControlType.Stop => true,
            ControlType.Shock => permissions.Shock,
            ControlType.Vibrate => permissions.Vibrate,
            ControlType.Sound => permissions.Sound,
            _ => false
        };

        return permitted || Drop(shockerId, type, "the owner has not granted this control type");
    }

    /// <summary>
    /// Logs why a control command was dropped, which is otherwise invisible to whatever issued it.
    /// </summary>
    private bool Drop(Guid shockerId, ControlType type, string reason)
    {
        _logger.LogDebug("Dropping {Type} for shocker [{ShockerId}]: {Reason}", type, shockerId, reason);
        return false;
    }
}

public readonly struct ShockerLogEventArgs
{
    public required LogEventArgs LogEventArgs { get; init; }
    public required bool IsRemote { get; init; }
}