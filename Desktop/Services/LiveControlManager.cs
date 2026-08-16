using System.Collections.Concurrent;
using System.Reactive.Linq;
using LucHeart.WebsocketLibrary;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.Models;
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

    /// <summary>
    /// How long a frame that arrived before the socket finished connecting stays eligible for replay. A live control
    /// frame means "right now", so one that has gone stale is dropped rather than fired late against an intent the
    /// user has long since released.
    /// </summary>
    private static readonly TimeSpan PendingFrameMaxAge = TimeSpan.FromSeconds(2);

    private readonly ILogger<LiveControlManager> _logger;
    private readonly ConfigManager _configManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly OpenShockApi _apiClient;

    private readonly ConcurrentDictionary<Guid, LiveControlConnection> _connections = new();
    private readonly Lock _connectionCreationLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _sweepTask;

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

        _sweepTask = OsTask.Run(() => SweepLoop(_cts.Token));
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
        // This runs per live control frame, so resolve through the api's shocker lookup and group by hand rather than
        // scanning every hub per shocker.
        var shockersByHub = new Dictionary<Guid, List<Guid>>();

        foreach (var shockerId in shockers)
        {
            var rejection = GetRejection(shockerId, type, out var location);
            LogRejection(shockerId, rejection);

            if (rejection != ControlRejection.None) continue;

            if (!shockersByHub.TryGetValue(location!.Hub.Id, out var hubShockers))
                shockersByHub[location.Hub.Id] = hubShockers = [];

            hubShockers.Add(shockerId);
        }

        foreach (var (hubId, hubShockers) in shockersByHub)
        {
            EnsureConnection(hubId)?.SendFrames(hubShockers, intensity, type);
        }
    }

    /// <summary>
    /// Control all enabled and online shockers with a specific intensity and control type, owned and shared alike,
    /// with <see cref="GetRejection"/> as the single gate. Opens the live control connection for the relevant hubs on
    /// demand. Uses <see cref="OpenShockApi.HubsById"/> rather than concatenating the owned and shared lists, which
    /// are two separate assignments per refresh and can be caught half updated.
    /// </summary>
    public void ControlAllShockers(byte intensity, ControlType type)
    {
        foreach (var hub in _apiClient.HubsById.Values.Where(x => x.Status.Online))
        {
            var shockers = hub.Shockers
                .Where(x => GetRejection(x.Id, type, out _) == ControlRejection.None)
                .Select(x => x.Id)
                .ToArray();

            if (shockers.Length == 0) continue;

            EnsureConnection(hub.Id)?.SendFrames(shockers, intensity, type);
        }
    }

    private enum ControlRejection
    {
        None,
        Disabled,
        NoLivePermission,
        NoControlTypePermission,
        UnknownShocker,
        Paused,
        HubOffline
    }

    /// <summary>
    /// Why a shocker may not be live controlled with <paramref name="type"/>, or <see cref="ControlRejection.None"/>
    /// when it may be, in which case <paramref name="location"/> is set to where it lives.
    /// </summary>
    private ControlRejection GetRejection(Guid shockerId, ControlType type, out ShockerLocation? location)
    {
        var resolution = _apiClient.ResolveShocker(shockerId);
        location = resolution.Location;

        if (!resolution.Enabled) return ControlRejection.Disabled;

        if (resolution.SharedPermissions is { } permissions)
        {
            if (!permissions.Live) return ControlRejection.NoLivePermission;

            // Live grants streaming at all, not any kind of frame, so the per type grant still applies.
            var permitted = type switch
            {
                // Stop can only ever end an action, so it needs no grant of its own.
                ControlType.Stop => true,
                ControlType.Shock => permissions.Shock,
                ControlType.Vibrate => permissions.Vibrate,
                ControlType.Sound => permissions.Sound,
                _ => false
            };

            if (!permitted) return ControlRejection.NoControlTypePermission;
        }

        if (location == null) return ControlRejection.UnknownShocker;

        if (location.Shocker.IsPaused) return ControlRejection.Paused;

        // Opening a socket to an offline hub would only get closed again by the next sweep.
        if (!location.Hub.Status.Online) return ControlRejection.HubOffline;

        return ControlRejection.None;
    }

    private readonly ConcurrentDictionary<Guid, ControlRejection> _lastRejection = new();

    /// <summary>
    /// Logs why a shocker was dropped from a live control frame, but only when the reason changes. Frames arrive many
    /// times per second, so logging every drop would flood the log for a single disabled shocker - yet without any
    /// logging at all a dropped shocker is completely invisible to whatever issued the control.
    /// </summary>
    private void LogRejection(Guid shockerId, ControlRejection rejection)
    {
        if (_lastRejection.GetValueOrDefault(shockerId) == rejection) return;
        _lastRejection[shockerId] = rejection;

        if (rejection != ControlRejection.None)
            _logger.LogDebug("Dropping live control frames for shocker [{ShockerId}]: {Reason}", shockerId, rejection);
    }

    /// <summary>
    /// Closes connections for hubs that are no longer online or no longer exist. Used on login/logout and on hub
    /// status changes. Never opens new connections.
    /// </summary>
    public Task RefreshConnections() => PruneConnectionsAsync(idleEviction: false);

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

            OsTask.Run(() => StartConnection(hubId, connection));

            return connection;
        }
    }

    private async Task StartConnection(Guid hubId, LiveControlConnection connection)
    {
        var client = connection.Client;

        await client.State.Updated.SubscribeAsync(async state =>
        {
            _logger.LogTrace("Live control connection for hub [{HubId}] status updated {Status}", hubId, state);

            // The socket only accepts frames once connected, so anything that arrived while it was still warming up
            // was parked. Replay it now, otherwise the first press after an idle period is silently swallowed.
            if (state == WebsocketConnectionState.Connected) connection.FlushPendingFrames();

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

    private async Task SweepLoop(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await PruneConnectionsAsync(idleEviction: true);
                }
                catch (Exception e)
                {
                    // The loop never restarts, so one bad sweep must not take it down.
                    _logger.LogError(e, "Failed to sweep live control connections");
                }
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
            var online = _apiClient.HubsById.TryGetValue(hubId, out var hub) && hub.Status.Online;
            var idle = idleEviction && connection.IdleFor > IdleTimeout;

            if (online && !idle) continue;

            if (!_connections.TryRemove(hubId, out var removed)) continue;

            _logger.LogDebug("Closing live control connection for hub [{HubId}] ({Reason})", hubId,
                !online ? "offline" : "idle");
            changed |= await removed.DisposeAsync();
        }

        if (changed)
            await _onStateUpdated.InvokeAsyncParallel();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        // Let the sweep loop observe the cancellation before the token source goes away underneath it.
        try
        {
            await _sweepTask;
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }

        _cts.Dispose();

        foreach (var hubId in _connections.Keys)
        {
            if (_connections.TryRemove(hubId, out var connection))
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// A live control socket plus the bookkeeping around it: when it was last used (for idle eviction), the frames
    /// parked while it was still connecting, and whether it has already been disposed.
    /// </summary>
    private sealed class LiveControlConnection(OpenShockLiveControlClient client)
    {
        public OpenShockLiveControlClient Client { get; } = client;

        /// <summary>
        /// Guards frame delivery against disposal, so a frame can never be handed to a client the sweeper is closing.
        /// </summary>
        private readonly Lock _lock = new();

        /// <summary>
        /// The latest frame per shocker that arrived before the socket was ready. Latest wins - live control is a
        /// continuous stream, so replaying anything but the most recent intensity would be meaningless.
        /// </summary>
        private readonly Dictionary<Guid, (ControlType Type, byte Intensity)> _pendingFrames = new();

        private long _pendingSinceTicks;
        private long _lastUsedTicks = Environment.TickCount64;
        private bool _disposed;

        public void Touch() => Interlocked.Exchange(ref _lastUsedTicks, Environment.TickCount64);

        public TimeSpan IdleFor => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastUsedTicks));

        /// <summary>
        /// Hands frames to the socket when it is ready, otherwise parks them for replay once it connects.
        /// </summary>
        public void SendFrames(IEnumerable<Guid> shockers, byte intensity, ControlType type)
        {
            lock (_lock)
            {
                if (_disposed) return;

                if (Client.State.Value == WebsocketConnectionState.Connected)
                {
                    foreach (var shocker in shockers) Client.IntakeFrame(shocker, type, intensity);
                    return;
                }

                foreach (var shocker in shockers) _pendingFrames[shocker] = (type, intensity);
                _pendingSinceTicks = Environment.TickCount64;

                // The socket can reach Connected between the check above and this park, in which case the flush that
                // ran on that transition already saw an empty queue. Re-check, otherwise the frame sits here until
                // the next transition - which for a one-shot press after an idle period never comes.
                if (Client.State.Value == WebsocketConnectionState.Connected) FlushLocked();
            }
        }

        /// <summary>
        /// Replays frames parked while the socket was connecting, unless they have gone stale.
        /// </summary>
        public void FlushPendingFrames()
        {
            lock (_lock)
            {
                FlushLocked();
            }
        }

        /// <remarks>Must be called while holding <see cref="_lock"/>.</remarks>
        private void FlushLocked()
        {
            if (_pendingFrames.Count == 0) return;

            var age = TimeSpan.FromMilliseconds(Environment.TickCount64 - _pendingSinceTicks);
            if (!_disposed && age <= PendingFrameMaxAge &&
                Client.State.Value == WebsocketConnectionState.Connected)
            {
                foreach (var (shocker, frame) in _pendingFrames)
                    Client.IntakeFrame(shocker, frame.Type, frame.Intensity);
            }

            _pendingFrames.Clear();
        }

        /// <summary>
        /// Marks the connection dead so no further frames reach the client, then closes the socket.
        /// </summary>
        /// <returns>False when another caller already claimed the disposal.</returns>
        public async ValueTask<bool> DisposeAsync()
        {
            lock (_lock)
            {
                if (_disposed) return false;
                _disposed = true;
                _pendingFrames.Clear();
            }

            await Client.DisposeAsync();
            return true;
        }
    }
}
