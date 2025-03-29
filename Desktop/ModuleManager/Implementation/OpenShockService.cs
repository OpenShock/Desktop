using System.Diagnostics.CodeAnalysis;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.ModuleBase.Api;
using OpenShock.Desktop.Services;

namespace OpenShock.Desktop.ModuleManager.Implementation;

public sealed class OpenShockService : IOpenShockService, IAsyncDisposable
{
    public required IOpenShockControl Control { get; init; }
    public required IOpenShockData Data { get; init; }
    public required IOpenShockApi Api { get; init; }

    private OpenShockControl _controlInstance;
    
    [SetsRequiredMembers]
    public OpenShockService(BackendHubManager backendHubManager, LiveControlManager liveControlManager, OpenShockApi openShockApi)
    {
        Control = _controlInstance = new OpenShockControl(backendHubManager, liveControlManager);
        Data = new OpenShockData()
        {
            Hubs = openShockApi.Hubs
        };
        Api = new OpenShockApiWrapper(openShockApi);
    }

    private bool _disposed;
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await _controlInstance.DisposeAsync();
    }
}