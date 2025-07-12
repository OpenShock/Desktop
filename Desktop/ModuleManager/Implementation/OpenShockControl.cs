using System.Collections.Immutable;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.ModuleBase.Models;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public class OpenShockControl : IOpenShockControl, IAsyncDisposable
{
    private readonly BackendHubManager _backendHubManager;
    private readonly LiveControlManager _liveControlManager;

    private readonly IAsyncDisposable _onShockerLogSubscription;
    
    public OpenShockControl(BackendHubManager backendHubManager, LiveControlManager liveControlManager)
    {
        _backendHubManager = backendHubManager;
        _liveControlManager = liveControlManager;
        
        _onShockerLogSubscription = _backendHubManager.OnShockerLog.SubscribeAsync(args =>
        {
            var logEvent = args.LogEventArgs;
            var sender = logEvent.Sender;
            
            var baseArgs = new RemoteControlledShockerArgs()
            {
                Sender = new ControlLogSender
                {
                    Id = sender.Id,
                    Name = sender.Name,
                    Image = sender.Image,
                    AdditionalItems = sender.AdditionalItems.AsReadOnly(),
                    ConnectionId = sender.ConnectionId,
                    CustomName = sender.CustomName,
                },
                Logs = logEvent.Logs.Select(log => new ControlLog
                {
                    Duration = log.Duration,
                    Intensity = log.Intensity,
                    Shocker = new GenericIn
                    {
                        Id = log.Shocker.Id,
                        Name = log.Shocker.Name
                    },
                    Type = (ControlType)log.Type,
                    ExecutedAt = log.ExecutedAt
                }).ToImmutableArray()
            };

            return args.IsRemote ? _onRemoteControlledShocker.InvokeAsyncParallel(baseArgs) : _onLocalControlledShocker.InvokeAsyncParallel(baseArgs);
        }).AsTask().Result;
    }
    
    public Task Control(IEnumerable<ShockerControl> shocks, string? customName = null)
    {
        return _backendHubManager.Control(shocks.Select(SdkDtoMappings.ToSdkControl), customName);
    }

    public void LiveControl(IEnumerable<Guid> shockers, byte intensity, ControlType type)
    {
        _liveControlManager.ControlShockers(shockers, intensity, (SDK.CSharp.Models.ControlType)type);
    }

    public void ControlAllShockers(byte intensity, ControlType type)
    {
        _liveControlManager.ControlAllShockers(intensity, (SDK.CSharp.Models.ControlType)type);
    }

    public IAsyncMinimalEventObservable<RemoteControlledShockerArgs> OnRemoteControlledShocker =>
        _onRemoteControlledShocker;
    private readonly AsyncMinimalEvent<RemoteControlledShockerArgs> _onRemoteControlledShocker = new();
    
    public IAsyncMinimalEventObservable<RemoteControlledShockerArgs> OnLocalControlledShocker =>
        _onLocalControlledShocker;
    private readonly AsyncMinimalEvent<RemoteControlledShockerArgs> _onLocalControlledShocker = new();

    private bool _disposed;
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await _onShockerLogSubscription.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    
    ~OpenShockControl()
    {
        DisposeAsync().AsTask().Wait();
    }
}