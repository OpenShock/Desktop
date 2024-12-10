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

    private string _liveConnectionId = string.Empty;

    public BackendHubManager(ILogger<BackendHubManager> logger,
        ConfigManager configManager,
        OpenShockHubClient openShockHubClient)
    {
        _logger = logger;
        _configManager = configManager;
        _openShockHubClient = openShockHubClient;

        _openShockHubClient.OnWelcome += s =>
        {
            _liveConnectionId = s;
            return Task.CompletedTask;
        };

        _openShockHubClient.OnLog += RemoteActivateShockers;
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

    private async Task RemoteActivateShockers(ControlLogSender sender, ICollection<ControlLog> logs)
    {
        if (sender.ConnectionId == _liveConnectionId)
        {
            _logger.LogDebug("Ignoring remote command log cause it was the local connection");
            return;
        }
    }
    
}