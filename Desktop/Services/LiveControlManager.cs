using System.Collections.Concurrent;
using System.Reactive.Linq;
using LucHeart.WebsocketLibrary;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Hub;
using OpenShock.SDK.CSharp.Live;
using OpenShock.SDK.CSharp.Models;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace OpenShock.Desktop.Services;

public sealed class LiveControlManager : IAsyncDisposable
{
    /// <summary>
    /// How long a live control connection is kept open after the last control frame before it is closed again.
    /// Live control sockets are opened lazily on demand, so this only needs to cover gaps between bursts of activity.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often we sweep for idle / offline connections to close.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly ILogger<LiveControlManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpenShockApi _apiClient;

    private readonly ConcurrentDictionary<Guid, LiveControlConnection> _connections = new();
    private readonly object _connectionCreationLock = new();
    private readonly CancellationTokenSource _cts = new();

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

        // When a hub goes offline (or the hub list changes) we only ever want to close connections, never open new
        // ones. Opening happens lazily when a control frame is actually sent.
        backendHubManager.OnHubStatusUpdated.Throttle(TimeSpan.FromMilliseconds(500)).Subscribe(_ => PruneConnections());

        hubClient.OnHubUpdate.SubscribeAsync(_ =>
        {
            _logger.LogDebug("Device update received, pruning stale live control connections");
            PruneConnections();
            return Task.CompletedTask;
        }).AsTask().Wait();

        OsTask.Run(() => SweepLoop(_cts.Token));
    }

    public IAsyncMinimalEventObservable OnStateUpdated => _onStateUpdated;
    private readonly AsyncMinimalEvent _onStateUpdated = new();

    /// <summary>
    /// Returns the currently open live control client for a hub, or null if no live control connection is active.
    /// Connections are opened lazily when the hub is being controlled and closed again after an idle period.
    /// </summary>
    public IOpenShockLiveControlClient? GetClient(Guid hubId) =>
        _connections.TryGetValue(hubId, out var connection) ? connection.Client : null;

    /// <summary>
    /// Control shockers with a specific intensity and control type. This also checks for enabled shockers in the config.
    /// Opens the live control connection for the relevant hubs on demand.
    /// </summary>
    public void ControlShockers(IEnumerable<Guid> shockers, byte intensity, ControlType type)
    {
        var enabledShockers = shockers.Where(IsLiveControllable);

        var shockersByDevice = enabledShockers.GroupBy(
            x => _apiClient.AllHubs.FirstOrDefault(y => y.Shockers.Any(z => z.Id == x && !z.IsPaused))?.Id);

        foreach (var device in shockersByDevice)
        {
            if (device.Key == null) continue;

            var connection = EnsureConnection(device.Key.Value);
            if (connection == null) continue;

            SendFrames(device, connection, intensity, type);
        }
    }

    /// <summary>
    /// Control all enabled and online shockers with a specific intensity and control type.
    /// Opens the live control connection for the relevant hubs on demand.
    /// Only ever targets owned hubs; shared hubs are never swept by a blanket "control all" and can only be
    /// controlled via an explicit <see cref="ControlShockers"/> call that names their shocker ids.
    /// </summary>
    public void ControlAllShockers(byte intensity, ControlType type)
    {
        foreach (var hub in _apiClient.Hubs.Value.Where(x => x.Status.Online))
        {
            var shockers = hub.Shockers
                .Where(x => !x.IsPaused && IsLiveControllable(x.Id))
                .Select(x => x.Id)
                .ToArray();

            if (shockers.Length == 0) continue;

            var connection = EnsureConnection(hub.Id);
            if (connection == null) continue;

            SendFrames(shockers, connection, intensity, type);
        }
    }

    /// <summary>
    /// A shocker is live controllable when it is enabled in config and, for shared shockers, we have been granted the
    /// live control permission. Owned shockers are not present in the shared permission map and are always allowed.
    /// </summary>
    private bool IsLiveControllable(Guid shockerId)
    {
        if (!_configManager.Config.OpenShock.Shockers.TryGetValue(shockerId, out var conf) || !conf.Enabled)
            return false;

        if (_apiClient.SharedShockerPermissions.TryGetValue(shockerId, out var permissions))
            return permissions.Live;

        return true;
    }

    /// <summary>
    /// Closes connections for hubs that are no longer online or no longer exist. Used on login/logout and on hub
    /// status changes. Never opens new connections.
    /// </summary>
    public Task RefreshConnections()
    {
        PruneConnections();
        return Task.CompletedTask;
    }

    private LiveControlConnection? EnsureConnection(Guid hubId)
    {
        if (_connections.TryGetValue(hubId, out var existing))
        {
            existing.Touch();
            return existing;
        }

        lock (_connectionCreationLock)
        {
            if (_connections.TryGetValue(hubId, out existing))
            {
                existing.Touch();
                return existing;
            }

            if (_apiClient.Client == null)
            {
                _logger.LogWarning("API client is not initialized, cannot open live control connection for hub [{HubId}]", hubId);
                return null;
            }

            _logger.LogDebug("Opening live control connection for hub [{HubId}]", hubId);

            var client = new OpenShockLiveControlClient(hubId, _configManager.Config.OpenShock.Token, _apiClient.Client, _loggerFactory);
            var connection = new LiveControlConnection(client);
            _connections[hubId] = connection;

            OsTask.Run(() => StartConnection(hubId, client));

            return connection;
        }
    }

    private async Task StartConnection(Guid hubId, OpenShockLiveControlClient client)
    {
        await client.State.Updated.SubscribeAsync(async state =>
        {
            _logger.LogTrace("Live control connection for hub [{HubId}] status updated {Status}", hubId, state);
            await _onStateUpdated.InvokeAsyncParallel();
        });

        await client.OnDispose.SubscribeAsync(async () =>
        {
            if (_connections.TryRemove(hubId, out _))
                _logger.LogDebug("Live control connection for hub [{HubId}] disposed itself", hubId);
            await _onStateUpdated.InvokeAsyncParallel();
        });

        client.Start();

        // Notify listeners that a (connecting) client now exists for this hub.
        await _onStateUpdated.InvokeAsyncParallel();
    }

    private static void SendFrames(IEnumerable<Guid> shockers, LiveControlConnection connection,
        byte intensity, ControlType type)
    {
        // Frames are only accepted once the socket is connected; while it is still warming up (or reconnecting) we
        // drop them. EnsureConnection already kicked off / kept the connection alive.
        if (connection.Client.State.Value != WebsocketConnectionState.Connected) return;

        foreach (var shocker in shockers)
        {
            connection.Client.IntakeFrame(shocker, type, intensity);
        }
    }

    private async Task SweepLoop(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PruneConnectionsAsync(idleEviction: true);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }

    private void PruneConnections() => OsTask.Run(() => PruneConnectionsAsync(idleEviction: false));

    private async Task PruneConnectionsAsync(bool idleEviction)
    {
        var changed = false;

        foreach (var (hubId, connection) in _connections)
        {
            var hub = _apiClient.AllHubs.FirstOrDefault(x => x.Id == hubId);
            var online = hub?.Status.Online ?? false;
            var idle = idleEviction && connection.IdleFor > IdleTimeout;

            if (online && !idle) continue;

            if (!_connections.TryRemove(hubId, out var removed)) continue;

            _logger.LogDebug("Closing live control connection for hub [{HubId}] ({Reason})", hubId,
                !online ? "offline" : "idle");
            await removed.Client.DisposeAsync();
            changed = true;
        }

        if (changed)
            await _onStateUpdated.InvokeAsyncParallel();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();

        foreach (var hubId in _connections.Keys)
        {
            if (_connections.TryRemove(hubId, out var connection))
                await connection.Client.DisposeAsync();
        }
    }

    private sealed class LiveControlConnection(OpenShockLiveControlClient client)
    {
        public OpenShockLiveControlClient Client { get; } = client;

        private long _lastUsedTicks = Environment.TickCount64;

        public void Touch() => Interlocked.Exchange(ref _lastUsedTicks, Environment.TickCount64);

        public TimeSpan IdleFor => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastUsedTicks));
    }
}
