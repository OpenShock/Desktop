using System.Collections.Immutable;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Models.BaseImpl;
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

    private readonly IAsyncDisposable _remoteControlledSubscription;
    
    public OpenShockControl(BackendHubManager backendHubManager, LiveControlManager liveControlManager)
    {
        _backendHubManager = backendHubManager;
        _liveControlManager = liveControlManager;
        
        _remoteControlledSubscription = _backendHubManager.OnRemoteControlledShocker.SubscribeAsync(args =>
        {
            return _onRemoteControlledShocker.InvokeAsyncParallel(new RemoteControlledShockerArgs()
            {
                Sender = new ControlLogSender
                {
                    Id = args.Sender.Id,
                    Name = args.Sender.Name,
                    Image = args.Sender.Image,
                    AdditionalItems = args.Sender.AdditionalItems.AsReadOnly(),
                    ConnectionId = args.Sender.ConnectionId,
                    CustomName = args.Sender.CustomName,
                },
                Logs = args.Logs.Select(log => new ControlLog
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
            });
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

    private bool _disposed;
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await _remoteControlledSubscription.DisposeAsync();
        GC.SuppressFinalize(this);
    }
    
    ~OpenShockControl()
    {
        DisposeAsync().AsTask().Wait();
    }
}