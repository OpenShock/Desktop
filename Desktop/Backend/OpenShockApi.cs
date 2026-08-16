using System.Collections.Concurrent;
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

    public ObservableVariable<IReadOnlyList<IOpenShockHub>> Hubs { get; } = new([]);

    /// <summary>
    /// Hubs owned by other users that have shared one or more shockers with us. Kept separate from <see cref="Hubs"/>.
    /// Flattened across all owners; see <see cref="SharedOwners"/> for the owner-grouped view used by the UI.
    /// </summary>
    public ObservableVariable<IReadOnlyList<IOpenShockHub>> SharedHubs { get; } = new([]);

    /// <summary>
    /// Shared hubs grouped by the owner that shared them, so the UI can attribute hubs to the person that owns them.
    /// </summary>
    public ObservableVariable<IReadOnlyList<SharedHubOwner>> SharedOwners { get; } = new([]);

    public ConcurrentDictionary<Guid, HubStatus> HubStates { get; } = new();

    /// <summary>
    /// Everything the control paths resolve against, rebuilt as one immutable unit per refresh and published with a
    /// single reference assignment, so readers need no lock and never see a mix of two refreshes.
    /// </summary>
    private volatile HubSnapshot _snapshot = HubSnapshot.Empty;

    /// <summary>
    /// Permissions granted to us per shared shocker. Owned shockers are not present here (they have all permissions).
    /// </summary>
    public IReadOnlyDictionary<Guid, ShockerPermissions> SharedShockerPermissions => _snapshot.SharedPermissions;

    /// <summary>
    /// Lookup from shocker id to that shocker and the hub it lives on, across owned and shared hubs. Live control
    /// resolves this for every shocker on every frame, so it is prebuilt rather than scanned.
    /// </summary>
    public IReadOnlyDictionary<Guid, ShockerLocation> ShockerLookup => _snapshot.Lookup;

    /// <summary>
    /// All hubs we can control, owned and shared, keyed by id.
    /// </summary>
    public IReadOnlyDictionary<Guid, IOpenShockHub> HubsById => _snapshot.HubsById;

    /// <summary>
    /// Fetches owned and shared hubs and publishes them as one snapshot. All or nothing, so a partially loaded state
    /// where the missing half looks deleted cannot occur.
    /// </summary>
    public async Task RefreshAllHubs()
    {
        if (Client == null)
        {
            _logger.LogError("Client is not initialized!");
            throw new Exception("Client is not initialized!");
        }

        var ownResponse = await Client.GetOwnShockers();
        var sharedResponse = await Client.GetSharedShockers();

        if (!ownResponse.IsT0 || !sharedResponse.IsT0)
        {
            _logger.LogError("We are not authenticated with the OpenShock API!");
            // TODO: handle unauthenticated error
            return;
        }

        IOpenShockHub[] ownHubs = [..ownResponse.AsT0.Value.Select(x => x.ToSdkHub(this))];
        var sharedOwners = sharedResponse.AsT0.Value.ToSdkSharedOwners(this);
        IOpenShockHub[] sharedHubs = [..sharedOwners.SelectMany(owner => owner.Hubs)];

        var snapshot = new HubSnapshot(
            [..ownHubs, ..sharedHubs],
            sharedResponse.AsT0.Value
                .SelectMany(owner => owner.Devices)
                .SelectMany(device => device.Shockers)
                .ToDictionary(shocker => shocker.Id, shocker => shocker.Permissions));

        _snapshot = snapshot;

        Hubs.Value = ownHubs;
        SharedHubs.Value = sharedHubs;
        SharedOwners.Value = sharedOwners;

        PruneDeadShockerOverrides(snapshot);
    }

    /// <summary>
    /// Whether the user has opted this shocker in. Absent from the config means disabled, owned and shared alike.
    /// </summary>
    public bool IsShockerEnabled(Guid shockerId) => ResolveShocker(shockerId).Enabled;

    /// <param name="Enabled">Whether the user has opted this shocker in.</param>
    /// <param name="Location">Where the shocker lives, or null when it sits on no known hub.</param>
    /// <param name="SharedPermissions">What its owner granted us, or null when we own it ourselves.</param>
    public readonly record struct ShockerResolution(
        bool Enabled,
        ShockerLocation? Location,
        ShockerPermissions? SharedPermissions);

    /// <summary>
    /// Resolves a shocker for a control decision. Control paths must use this rather than reading
    /// <see cref="SharedShockerPermissions"/> and <see cref="ShockerLookup"/> separately, which re-reads the volatile
    /// snapshot each time and lets one decision straddle two refreshes.
    /// </summary>
    public ShockerResolution ResolveShocker(Guid shockerId)
    {
        var snapshot = _snapshot;

        ShockerPermissions? shared =
            snapshot.SharedPermissions.TryGetValue(shockerId, out var permissions) ? permissions : null;

        var enabled = _configManager.Config.OpenShock.Shockers.TryGetValue(shockerId, out var conf) && conf.Enabled;

        snapshot.Lookup.TryGetValue(shockerId, out var location);

        return new ShockerResolution(enabled, location, shared);
    }

    /// <summary>
    /// Records an explicit opt-in choice for a shocker. Copy on write, so readers holding the previous dictionary are
    /// unaffected.
    /// </summary>
    public void SetShockerEnabled(Guid shockerId, bool enabled)
    {
        var openShock = _configManager.Config.OpenShock;

        openShock.Shockers = new Dictionary<Guid, OpenShockConf.ShockerConf>(openShock.Shockers)
        {
            [shockerId] = new() { Enabled = enabled }
        };

        _configManager.Save();
    }

    /// <summary>
    /// Drops overrides for shockers that no longer exist, forcing a fresh opt-in when a share is revoked and later
    /// granted again.
    /// </summary>
    private void PruneDeadShockerOverrides(HubSnapshot snapshot)
    {
        var openShock = _configManager.Config.OpenShock;

        if (openShock.Shockers.Count == 0) return;
        if (openShock.Shockers.Keys.All(snapshot.Lookup.ContainsKey)) return;

        openShock.Shockers = openShock.Shockers
            .Where(x => snapshot.Lookup.ContainsKey(x.Key))
            .ToDictionary(x => x.Key, x => x.Value);

        _configManager.Save();
    }

    public void Logout()
    {
        _snapshot = HubSnapshot.Empty;

        Hubs.Value = [];
        SharedHubs.Value = [];
        SharedOwners.Value = [];
    }

    /// <summary>
    /// An immutable view of the hubs from a single refresh, with the derived lookups built once up front.
    /// </summary>
    private sealed class HubSnapshot
    {
        public static readonly HubSnapshot Empty = new([], new Dictionary<Guid, ShockerPermissions>());

        public HubSnapshot(IReadOnlyList<IOpenShockHub> allHubs,
            IReadOnlyDictionary<Guid, ShockerPermissions> sharedPermissions)
        {
            SharedPermissions = sharedPermissions;

            var hubsById = new Dictionary<Guid, IOpenShockHub>();
            var lookup = new Dictionary<Guid, ShockerLocation>();

            foreach (var hub in allHubs)
            {
                hubsById.TryAdd(hub.Id, hub);

                foreach (var shocker in hub.Shockers)
                    lookup.TryAdd(shocker.Id, new ShockerLocation(hub, shocker));
            }

            HubsById = hubsById;
            Lookup = lookup;
        }

        public IReadOnlyDictionary<Guid, IOpenShockHub> HubsById { get; }
        public IReadOnlyDictionary<Guid, ShockerPermissions> SharedPermissions { get; }
        public IReadOnlyDictionary<Guid, ShockerLocation> Lookup { get; }
    }
}