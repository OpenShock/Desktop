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
        if (update.UpdateType is not HubUpdateType.Created)
        {
            if(_openShockApi.Hubs.Value.All(x => x.Id != update.HubId))
            {
                _logger.LogDebug("Hub update received for none of our hubs {HubId} {Type}, ignoring", update.HubId, update.UpdateType);
                return;
            }
        }
        
        _logger.LogDebug("Device update received {DeviceId} {UpdateType}", update.HubId, update.UpdateType);
        
        await _openShockApi.RefreshHubs();
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
        var enabledShockers = _configManager.Config.OpenShock.Shockers
            .Where(y => y.Value.Enabled && 
                        _openShockApi.Hubs.Value.Any(x=> 
                            x.Shockers.Any(z => z.Id == y.Key && !z.IsPaused)))
            .Select(x => x.Key)
            .ToHashSet();
        
        var shocksToSend = shocks.Where(x => enabledShockers.Contains(x.Id));
        return _openShockHubClient.Control(shocksToSend, customName);
    }
}

public readonly struct ShockerLogEventArgs
{
    public required LogEventArgs LogEventArgs { get; init; }
    public required bool IsRemote { get; init; }
}