using System.Collections.Concurrent;
using System.Collections.Immutable;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Models;
using OpenShock.Desktop.Models.BaseImpl;
using OpenShock.Desktop.ModuleBase.StableInterfaces;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp;
using OpenShock.SDK.CSharp.Models;

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
    
    public ObservableVariable<IReadOnlyList<IOpenShockHub>> Hubs { get; } = new(ImmutableArray<OpenShockHub>.Empty);

    /// <summary>
    /// Hubs owned by other users that have shared one or more shockers with us. Kept separate from <see cref="Hubs"/>.
    /// Flattened across all owners; see <see cref="SharedOwners"/> for the owner-grouped view used by the UI.
    /// </summary>
    public ObservableVariable<IReadOnlyList<IOpenShockHub>> SharedHubs { get; } = new(ImmutableArray<OpenShockHub>.Empty);

    /// <summary>
    /// Shared hubs grouped by the owner that shared them, so the UI can attribute hubs to the person that owns them.
    /// </summary>
    public ObservableVariable<IReadOnlyList<SharedHubOwner>> SharedOwners { get; } =
        new(ImmutableArray<SharedHubOwner>.Empty);

    /// <summary>
    /// Permissions granted to us per shared shocker. Owned shockers are not present here (they have all permissions).
    /// </summary>
    public IReadOnlyDictionary<Guid, ShockerPermissions> SharedShockerPermissions => _sharedShockerPermissions;
    private volatile IReadOnlyDictionary<Guid, ShockerPermissions> _sharedShockerPermissions =
        new Dictionary<Guid, ShockerPermissions>();

    /// <summary>
    /// All hubs we can control, both owned and shared.
    /// </summary>
    public IEnumerable<IOpenShockHub> AllHubs => Hubs.Value.Concat(SharedHubs.Value);

    public ConcurrentDictionary<Guid, HubStatus> HubStates { get; } = new();

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
                Hubs.Value = [..success.Value.Select(x => x.ToSdkHub(this))];
                SyncShockerConfig();
            },
        error =>
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
        });
    }

    public async Task RefreshSharedHubs()
    {
        if (Client == null)
        {
            _logger.LogError("Client is not initialized!");
            throw new Exception("Client is not initialized!");
        }
        var response = await Client.GetSharedShockers();

        response.Switch(success =>
            {
                var owners = success.Value.ToSdkSharedOwners(this);
                SharedOwners.Value = owners;
                SharedHubs.Value = owners.SelectMany(owner => owner.Hubs).ToArray();

                _sharedShockerPermissions = success.Value
                    .SelectMany(owner => owner.Devices)
                    .SelectMany(device => device.Shockers)
                    .ToDictionary(shocker => shocker.Id, shocker => shocker.Permissions);

                SyncShockerConfig();
            },
        error =>
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
        });
    }

    /// <summary>
    /// Re-populates the per-shocker config with the currently known owned and shared shockers, preserving the enabled
    /// flag for shockers that were already present and dropping shockers that no longer exist. Newly discovered owned
    /// shockers default to enabled; newly discovered shared shockers default to disabled so another user's device is
    /// never controlled until the user explicitly opts in.
    /// </summary>
    private void SyncShockerConfig()
    {
        var shockerList = new Dictionary<Guid, OpenShockConf.ShockerConf>();
        foreach (var shockerId in AllHubs.SelectMany(x => x.Shockers).Select(x => x.Id))
        {
            if (shockerList.ContainsKey(shockerId)) continue;

            // Shared shockers are present in the permission map; owned shockers are not.
            var enabled = !_sharedShockerPermissions.ContainsKey(shockerId);
            if (_configManager.Config.OpenShock.Shockers.TryGetValue(shockerId, out var confShocker))
                enabled = confShocker.Enabled;

            shockerList.Add(shockerId, new OpenShockConf.ShockerConf
            {
                Enabled = enabled
            });
        }
        _configManager.Config.OpenShock.Shockers = shockerList;
        _configManager.Save();
    }

    public void Logout()
    {
        Hubs.Value = ImmutableArray<OpenShockHub>.Empty;
        SharedHubs.Value = ImmutableArray<OpenShockHub>.Empty;
        SharedOwners.Value = ImmutableArray<SharedHubOwner>.Empty;
        _sharedShockerPermissions = new Dictionary<Guid, ShockerPermissions>();
    }

}