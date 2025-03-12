using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Models;
using OpenShock.Desktop.ReactiveExtensions;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Hub.Models;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Models;
using OpenShock.SDK.CSharp.Utils;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace OpenShock.Desktop.Services;

public sealed class LiveControlManager
{
    private readonly ILogger<LiveControlManager> _logger;
    private readonly OpenShockApi _api;
    private readonly ConfigManager _configManager;
    private readonly ILogger<OpenShockLiveControlClient> _liveControlLogger;
    private readonly OpenShockHubClient _hubClient;
    private readonly OpenShockApi _apiClient;
    private readonly SemaphoreSlim _refreshLock = new(1, maxCount: 1);

    public LiveControlManager(
        ILogger<LiveControlManager> logger,
        OpenShockApi api, 
        ConfigManager configManager,
        ILogger<OpenShockLiveControlClient> liveControlLogger,
        OpenShockHubClient hubClient,
        OpenShockApi apiClient)
    {
        _logger = logger;
        _api = api;
        _configManager = configManager;
        _liveControlLogger = liveControlLogger;
        _hubClient = hubClient;
        _apiClient = apiClient;

        _hubClient.OnHubStatus.SubscribeAsync(async _ =>
        {
            _logger.LogDebug("Device update received, updating shockers and refreshing connections");
            await RefreshConnections();
        }).AsTask().Wait();
        _hubClient.OnHubUpdate.SubscribeAsync(async _ =>
        {
            _logger.LogDebug("Device status received, refreshing connections");
            await RefreshConnections();
        }).AsTask().Wait();
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
        foreach (var liveControlClient in LiveControlClients)
        {
            if (_api.Hubs.Value.Any(x => x.Id == liveControlClient.Key)) continue;
            if (!LiveControlClients.Remove(liveControlClient.Key, out var removedClient))
                await removedClient!.DisposeAsync();
        }

        foreach (var device in _api.Hubs.Value)
        {
            if (LiveControlClients.ContainsKey(device.Id)) continue;

            _logger.LogTrace("Creating live control client for device [{DeviceId}]", device.Id);

            _logger.LogTrace("Getting device gateway for device [{DeviceId}]", device.Id);
            var deviceGateway = await _apiClient.GetDeviceGateway(device.Id);

            if (deviceGateway.IsT0)
            {
                var gateway = deviceGateway.AsT0.Value;
                await SetupLiveControlClient(device.Id, gateway);

                continue;
            }
            
            deviceGateway.Switch(success =>
                {
                },
                found =>
                {
                    _logger.LogError(
                        "Failed to get device gateway for device [{DeviceId}], not found or no permission",
                        device.Id);
                },
                offline =>
                {
                    _logger.LogInformation("Failed to get device gateway for device [{DeviceId}], device offline",
                        device.Id);
                },
                gateway =>
                {
                    _logger.LogError(
                        "Failed to get device gateway for device [{DeviceId}], " +
                        "the device is online but its not connected to a gateway, this means the device is probably" +
                        " outdated and does not support live control. Please upgrade your device",
                        device.Id);
                }, unauthenticated =>
                {
                    _logger.LogError(
                        "Failed to get device gateway for device [{DeviceId}], we are not authenticated",
                        device.Id);
                    // TODO: Handle unauthenticated globally
                });
        }

        OsTask.Run(_onStateUpdated.InvokeAsyncParallel);
    }

    private async Task SetupLiveControlClient(Guid deviceId, LcgResponse gateway)
    {
        _logger.LogTrace("Got device gateway for device [{DeviceId}] [{Gateway}]", deviceId,
            gateway.Gateway);

        var client = new OpenShockLiveControlClient(gateway.Gateway, deviceId,
            _configManager.Config.OpenShock.Token, _liveControlLogger);
        LiveControlClients.Add(deviceId, client);

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

        // When the client shuts down, remove it from the list
        await client.OnDispose.SubscribeAsync(async () =>
        {
            _logger.LogTrace("Live control client for device [{DeviceId}] disposed, removing from list",
                deviceId);
            if (!LiveControlClients.Remove(deviceId, out var removedClient)) return;
            await removedClient.DisposeAsync(); // Dispose incase it was not disposed

            await _onStateUpdated.InvokeAsyncParallel();
        });

        await client.InitializeAsync();
    }

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
    
    private void ControlFrame(IEnumerable<Guid> shockers, IOpenShockLiveControlClient client,
        byte vibrationIntensity, ControlType type)
    {
        foreach (var shocker in shockers)
        {
            client.IntakeFrame(shocker, type, vibrationIntensity);
        }
    }
}