using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Hub;

namespace OpenShock.Desktop.Services;

public sealed class StatusHandler
{
    
    public IAsyncMinimalEventObservable OnWebsocketStatusChanged => _onWebsocketStatusChanged;
    private readonly AsyncMinimalEvent _onWebsocketStatusChanged = new();
    private readonly ILogger<StatusHandler> _logger;

    public StatusHandler(OpenShockHubClient hubClient, LiveControlManager liveControlManager, ILogger<StatusHandler> logger)
    {
        _logger = logger;
        
        hubClient.OnReconnecting.SubscribeAsync(_ => _onWebsocketStatusChanged.InvokeAsyncParallel()).AsTask().Wait();
        hubClient.OnReconnected.SubscribeAsync(_ => _onWebsocketStatusChanged.InvokeAsyncParallel()).AsTask().Wait();
        hubClient.OnClosed.SubscribeAsync(_ => _onWebsocketStatusChanged.InvokeAsyncParallel()).AsTask().Wait();
        hubClient.OnConnected.SubscribeAsync(_ => _onWebsocketStatusChanged.InvokeAsyncParallel()).AsTask().Wait();
        liveControlManager.OnStateUpdated.SubscribeAsync(() => _onWebsocketStatusChanged.InvokeAsyncParallel()).AsTask().Wait();
    }
}