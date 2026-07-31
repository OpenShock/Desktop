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

    /// <summary>
    /// Lookup from shocker id to that shocker and the hub it lives on, across owned and shared hubs. Rebuilt whenever
    /// a hub list is refreshed, so hot paths do not have to scan every hub per shocker - live control resolves this
    /// for every shocker on every frame.
    /// </summary>
    public IReadOnlyDictionary<Guid, ShockerLocation> ShockerLookup => _shockerLookup;
    private volatile IReadOnlyDictionary<Guid, ShockerLocation> _shockerLookup =
        new Dictionary<Guid, ShockerLocation>();

    public ConcurrentDictionary<Guid, HubStatus> HubStates { get; } = new();

    /// <summary>
    /// Whether owned / shared hubs have been fetched at least once since the last logout. The shocker config is only
    /// pruned once both halves are known, otherwise the half that has not loaded yet would look like it no longer
    /// exists and its entries (including the user's enabled choices) would be dropped.
    /// </summary>
    private bool _ownHubsLoaded;
    private bool _sharedHubsLoaded;

    /// <summary>
    /// Guards the read-modify-write of the shocker config, which is reachable from concurrent refreshes.
    /// </summary>
    private readonly Lock _shockerConfigLock = new();

    /// <summary>
    /// Refreshes owned and shared hubs together and syncs the shocker config once, after both are known. Prefer this
    /// over calling the individual refreshes back to back, which would sync against a half-populated hub list.
    /// </summary>
    public async Task RefreshAllHubs()
    {
        var ownFetched = await FetchOwnHubs();
        var sharedFetched = await FetchSharedHubs();

        if (ownFetched || sharedFetched) SyncShockerConfig();
    }

    public async Task RefreshHubs()
    {
        if (await FetchOwnHubs()) SyncShockerConfig();
    }

    public async Task RefreshSharedHubs()
    {
        if (await FetchSharedHubs()) SyncShockerConfig();
    }

    /// <returns>Whether the hub list was updated and the shocker config needs syncing.</returns>
    private async Task<bool> FetchOwnHubs()
    {
        if (Client == null)
        {
            _logger.LogError("Client is not initialized!");
            throw new Exception("Client is not initialized!");
        }
        var response = await Client.GetOwnShockers();

        return response.Match(success =>
            {
                Hubs.Value = [..success.Value.Select(x => x.ToSdkHub(this))];
                _ownHubsLoaded = true;
                RebuildShockerLookup();
                return true;
            },
        _ =>
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
            return false;
        });
    }

    /// <returns>Whether the shared hub list was updated and the shocker config needs syncing.</returns>
    private async Task<bool> FetchSharedHubs()
    {
        if (Client == null)
        {
            _logger.LogError("Client is not initialized!");
            throw new Exception("Client is not initialized!");
        }
        var response = await Client.GetSharedShockers();

        return response.Match(success =>
            {
                var owners = success.Value.ToSdkSharedOwners(this);
                SharedOwners.Value = owners;
                SharedHubs.Value = owners.SelectMany(owner => owner.Hubs).ToArray();

                _sharedShockerPermissions = success.Value
                    .SelectMany(owner => owner.Devices)
                    .SelectMany(device => device.Shockers)
                    .ToDictionary(shocker => shocker.Id, shocker => shocker.Permissions);

                _sharedHubsLoaded = true;
                RebuildShockerLookup();
                return true;
            },
        _ =>
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
            return false;
        });
    }

    private void RebuildShockerLookup()
    {
        var lookup = new Dictionary<Guid, ShockerLocation>();

        foreach (var hub in AllHubs)
        foreach (var shocker in hub.Shockers)
            lookup.TryAdd(shocker.Id, new ShockerLocation(hub, shocker));

        _shockerLookup = lookup;
    }

    /// <summary>
    /// Re-populates the per-shocker config with the currently known owned and shared shockers, preserving the enabled
    /// flag for shockers that were already present and dropping shockers that no longer exist. Newly discovered owned
    /// shockers default to enabled; newly discovered shared shockers default to disabled so another user's device is
    /// never controlled until the user explicitly opts in.
    ///
    /// Entries are only dropped once both the owned and the shared hub list have been fetched; until then unknown
    /// entries are carried over untouched so a partially loaded state cannot discard the user's choices.
    /// </summary>
    private void SyncShockerConfig()
    {
        lock (_shockerConfigLock)
        {
            var existing = _configManager.Config.OpenShock.Shockers;
            var shockerList = new Dictionary<Guid, OpenShockConf.ShockerConf>();

            foreach (var shockerId in AllHubs.SelectMany(x => x.Shockers).Select(x => x.Id))
            {
                if (shockerList.ContainsKey(shockerId)) continue;

                // Shared shockers are present in the permission map; owned shockers are not.
                var enabled = !_sharedShockerPermissions.ContainsKey(shockerId);
                if (existing.TryGetValue(shockerId, out var confShocker))
                    enabled = confShocker.Enabled;

                shockerList.Add(shockerId, new OpenShockConf.ShockerConf
                {
                    Enabled = enabled
                });
            }

            if (!_ownHubsLoaded || !_sharedHubsLoaded)
            {
                foreach (var (shockerId, confShocker) in existing)
                    shockerList.TryAdd(shockerId, confShocker);
            }

            _configManager.Config.OpenShock.Shockers = shockerList;
            _configManager.Save();
        }
    }

    public void Logout()
    {
        Hubs.Value = ImmutableArray<OpenShockHub>.Empty;
        SharedHubs.Value = ImmutableArray<OpenShockHub>.Empty;
        SharedOwners.Value = ImmutableArray<SharedHubOwner>.Empty;
        _sharedShockerPermissions = new Dictionary<Guid, ShockerPermissions>();
        _shockerLookup = new Dictionary<Guid, ShockerLocation>();
        _ownHubsLoaded = false;
        _sharedHubsLoaded = false;
    }

}