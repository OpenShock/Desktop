using System.Collections.Immutable;
using OneOf;
using OneOf.Types;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp;
using OpenShock.SDK.CSharp.Models;
using OpenShock.SDK.CSharp.Utils;

namespace OpenShock.Desktop.Backend;

public sealed class OpenShockApi
{
    private readonly ILogger<OpenShockApi> _logger;
    private readonly ConfigManager _configManager;
    public OpenShockApiClient? Client { get; private set; }

    /// <summary>
    /// DI constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configManager"></param>
    public OpenShockApi(ILogger<OpenShockApi> logger, ConfigManager configManager)
    {
        _logger = logger;
        _configManager = configManager;
        SetupApiClient();
    }

    public void SetupApiClient()
    {
        Client = new OpenShockApiClient(new ApiClientOptions
        {
            Server = _configManager.Config.OpenShock.Backend,
            Token = _configManager.Config.OpenShock.Token
        });
    }
    
    public ObservableVariable<ImmutableArray<OpenShockHub>> Hubs { get; } = new(ImmutableArray<OpenShockHub>.Empty);
    
    public async Task RefreshHubs()
    {
        if (Client == null)
        {
            _logger.LogError("Client is not initialized!");
            throw new Exception("Client is not initialized!");
        }
        var response = await Client.GetOwnShockers();
        
        response.Switch(success =>
            {
                Hubs.Value = [..success.Value.Select(SdkDtoMappings.ToSdkHub)];
                
                // re-populate config with previous data if present, this also deletes any shockers that are no longer present
                var shockerList = new Dictionary<Guid, OpenShockConf.ShockerConf>();
                foreach (var shocker in success.Value.SelectMany(x => x.Shockers))
                {
                    var enabled = true;
                
                    if (_configManager.Config.OpenShock.Shockers.TryGetValue(shocker.Id, out var confShocker))
                    {
                        enabled = confShocker.Enabled;
                    }

                    shockerList.Add(shocker.Id, new OpenShockConf.ShockerConf
                    {
                        Enabled = enabled
                    });
                }
                _configManager.Config.OpenShock.Shockers = shockerList;
                _configManager.Save();
            },
        error =>
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
        });
    }

    public void Logout()
    {
        Hubs.Value = ImmutableArray<OpenShockHub>.Empty;
    }

    public
        Task<OneOf<Success<LcgResponse>, NotFound, DeviceOffline, DeviceNotConnectedToGateway, UnauthenticatedError>>
        GetDeviceGateway(Guid deviceId, CancellationToken cancellationToken = default)
    {
        if (Client == null)
        {
            _logger.LogError("Client is not initialized!");
            throw new Exception("Client is not initialized!");
        }
        
        return Client.GetDeviceGateway(deviceId, cancellationToken);
    }
        
}