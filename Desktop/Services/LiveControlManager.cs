using System.Reactive.Linq;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Models;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace OpenShock.Desktop.Services;

public sealed class LiveControlManager
{
    private readonly ILogger<LiveControlManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpenShockApi _apiClient;
    private readonly SemaphoreSlim _refreshLock = new(1, maxCount: 1);

    public LiveControlManager(
        ILogger<LiveControlManager> logger,
        ConfigManager configManager,
        ILoggerFactory loggerFactory,
        OpenShockHubClient hubClient,
        OpenShockApi apiClient,
        BackendHubManager backendHubManager)
    {
        _logger = logger;
        _configManager = configManager;
        _loggerFactory = loggerFactory;
        _apiClient = apiClient;

        backendHubManager.OnHubStatusUpdated.Throttle(TimeSpan.FromMilliseconds(500)).Subscribe(HubStatusUpdated);

        hubClient.OnHubStatus.SubscribeAsync(async _ =>
        {
            _logger.LogDebug("Device update received, updating shockers and refreshing connections");
            await RefreshConnections();
        }).AsTask().Wait();
        hubClient.OnHubUpdate.SubscribeAsync(async _ =>
        {
            _logger.LogDebug("Device status received, refreshing connections");
            await RefreshConnections();
        }).AsTask().Wait();
    }

    private void HubStatusUpdated(Guid? obj)
    {
        RefreshConnections().Wait();
    }

    public IAsyncMinimalEventObservable OnStateUpdated => _onStateUpdated;
    private readonly AsyncMinimalEvent _onStateUpdated = new();
    
    public Dictionary<Guid, OpenShockLiveControlClient> LiveControlClients { get; } = new();

    public async Task RefreshConnections()
    {
        await _refreshLock.WaitAsync();
        try
        {
            await RefreshInternal();
        }
        finally
        {
            _refreshLock.Release();
        }
    }
    
    private async Task RefreshInternal()
    {
        _logger.LogDebug("Refreshing live control connections");

        // Remove devices that dont exist anymore
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        // Linq would be horrible for readability here
        foreach (var liveControlClient in LiveControlClients)
        {
            // Check if the hub is online
            var hub = _apiClient.Hubs.Value.FirstOrDefault(x => x.Id == liveControlClient.Key);
            if (hub is not null && hub.Status.Online) continue; // If so, dont remove it
            
            if (!LiveControlClients.Remove(liveControlClient.Key, out var removedClient))
                await removedClient!.DisposeAsync();
        }

        foreach (var device in _apiClient.Hubs.Value.Where(x => x.Status.Online))
        {
            // Skip hubs that already have a live control client
            if (LiveControlClients.ContainsKey(device.Id)) continue;

            _logger.LogTrace("Creating live control client for device [{DeviceId}]", device.Id);
            
            // Adds the new live control client to the list
            await SetupLiveControlClient(device.Id);
        }

        OsTask.Run(_onStateUpdated.InvokeAsyncParallel);
    }

    private async Task SetupLiveControlClient(Guid deviceId)
    {
        if(_apiClient.Client == null)
        {
            _logger.LogWarning("API client is not initialized, cannot setup live control client for device [{DeviceId}]", deviceId);
            return;
        }

        var client = new OpenShockLiveControlClient(deviceId, _configManager.Config.OpenShock.Token,  _apiClient.Client, _loggerFactory);
        LiveControlClients.Add(deviceId, client);

        // Websocket connection state
        await client.State.Updated.SubscribeAsync(async state =>
        {
            _logger.LogTrace("Live control client for device [{DeviceId}] status updated {Status}",
                deviceId, state);
            await _onStateUpdated.InvokeAsyncParallel();
        });

        await client.OnHubNotConnected.SubscribeAsync(async () =>
        {
            _logger.LogInformation("Live control client for device [{DeviceId}] ending, device disconnected", deviceId);
            // Dispose the client so it gets removed from the list and co
            await client.DisposeAsync();
        });

        // Honestly, do we even need this? We control when we dispose and remove it, the client doesnt do this on its own
        // When the client shuts down, remove it from the list
        await client.OnDispose.SubscribeAsync(async () =>
        {
            _logger.LogTrace("Live control client for device [{DeviceId}] disposed, removing from list",
                deviceId);
            if (!LiveControlClients.Remove(deviceId, out var removedClient)) return;
            await removedClient.DisposeAsync(); // Dispose incase it was not disposed

            await _onStateUpdated.InvokeAsyncParallel();
        });

        client.Start();
    }

    /// <summary>
    /// Control shockers with a specific intensity and control type. This also checks for enabled shockers in the config.
    /// </summary>
    /// <param name="shockers"></param>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    public void ControlShockers(IEnumerable<Guid> shockers, byte intensity, ControlType type)
    {
        var enabledShockers = shockers.Where(x =>
            _configManager.Config.OpenShock.Shockers.Any(y => y.Key == x && y.Value.Enabled));
        
        var shockersByDevice = enabledShockers.GroupBy(
            x => _apiClient.Hubs.Value.FirstOrDefault(y => y.Shockers.Any(z => z.Id == x && !z.IsPaused))?.Id);

        foreach (var device in shockersByDevice)
        {
            if (device.Key == null) continue;
            if (!LiveControlClients.TryGetValue(device.Key.Value, out var client)) continue;

            ControlFrame(device.Select(x => x), client, intensity, type);
        }
    }

    /// <summary>
    /// Control all enabled shockers with a specific intensity and control type.
    /// </summary>
    /// <param name="intensity"></param>
    /// <param name="type"></param>
    public void ControlAllShockers(byte intensity, ControlType type)
    {
        foreach (var (deviceId, liveControlClient) in LiveControlClients)
        {
            var apiDevice = _apiClient.Hubs.Value
                .FirstOrDefault(x => x.Id == deviceId);
            if (apiDevice == null) continue;
                
            ControlFrame(apiDevice.Shockers
                .Where(x => !x.IsPaused &&
                    _configManager.Config.OpenShock.Shockers.Any(y => 
                        y.Key == x.Id && y.Value.Enabled))
                .Select(x => x.Id), liveControlClient, intensity, type);
        }
    } 
    
    private static void ControlFrame(IEnumerable<Guid> shockers, OpenShockLiveControlClient client,
        byte vibrationIntensity, ControlType type)
    {
        foreach (var shocker in shockers)
        {
            client.IntakeFrame(shocker, type, vibrationIntensity);
        }
    }
}