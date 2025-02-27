using System.Globalization;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Models;
using OpenShock.Desktop.Services;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Hub.Models;
using OpenShock.SDK.CSharp.Models;
using Serilog;
using Control = OpenShock.SDK.CSharp.Hub.Models.Control;

namespace OpenShock.Desktop.Backend;

public sealed class BackendHubManager
{
    private readonly ILogger<BackendHubManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly OpenShockHubClient _openShockHubClient;
    private readonly OpenShockApi _openShockApi;

    private string _liveConnectionId = string.Empty;

    public BackendHubManager(ILogger<BackendHubManager> logger,
        ConfigManager configManager,
        OpenShockHubClient openShockHubClient, OpenShockApi openShockApi)
    {
        _logger = logger;
        _configManager = configManager;
        _openShockHubClient = openShockHubClient;
        _openShockApi = openShockApi;

        _openShockHubClient.OnWelcome += s =>
        {
            _liveConnectionId = s;
            return Task.CompletedTask;
        };

        _openShockHubClient.OnLog += RemoteActivateShockers;
        _openShockHubClient.OnDeviceUpdate += DeviceUpdate;
    }

    private async Task DeviceUpdate(Guid deviceId, DeviceUpdateType updateType)
    {
        _logger.LogDebug("Device update received {DeviceId} {UpdateType}", deviceId, updateType);
                
        await _openShockApi.RefreshShockers();
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

    private Task RemoteActivateShockers(ControlLogSender sender, ICollection<ControlLog> logs)
    {
        if (sender.ConnectionId == _liveConnectionId)
        {
            _logger.LogDebug("Ignoring remote command log cause it was the local connection");
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
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